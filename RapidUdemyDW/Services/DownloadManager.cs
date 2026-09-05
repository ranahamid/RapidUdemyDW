using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using RapidUdemyDW.Models;
using Serilog;

namespace RapidUdemyDW.Services;

/// <summary>
/// Multi-job download manager. Supports multiple concurrent course downloads
/// running in the background while the user browses and queues more.
/// </summary>
public partial class DownloadManager
{
    private readonly UdemyApiService _api;
    private readonly HttpClient _http;
    private SemaphoreSlim _semaphore = new(AppConstants.DefaultConcurrentDownloads, AppConstants.MaxConcurrentDownloadsCap);
    private int _currentMaxDownloads = AppConstants.DefaultConcurrentDownloads;
    private long _lastNotifyTicks;

    // History tracking
    private DownloadHistoryService? _history;

    public event Action? OnStateChanged;

    /// <summary>All active/completed download jobs.</summary>
    public List<DownloadJob> Jobs { get; } = [];

    // Legacy compat — these aggregate across all jobs
    public List<DownloadTask> Tasks => Jobs.SelectMany(j => j.Tasks).ToList();
    public bool IsRunning => Jobs.Any(j => j.IsRunning);
    public int TotalTasks => Jobs.Sum(j => j.TotalTasks);
    public int CompletedTasks => Jobs.Sum(j => j.CompletedTasks);
    public int FailedTasks => Jobs.Sum(j => j.FailedTasks);
    public double OverallProgress => TotalTasks > 0 ? CompletedTasks * 100.0 / TotalTasks : 0;
    public bool IsPaused => false; // Per-job now

    public int ActiveJobCount => Jobs.Count(j => j.IsRunning);

    public DownloadManager(UdemyApiService api, HttpClient httpClient)
    {
        _api = api;
        _http = httpClient;
        _http.Timeout = TimeSpan.FromHours(2);
    }

    /// <summary>
    /// Update the max concurrent downloads limit from user settings.
    /// Replaces the semaphore so new downloads use the new limit.
    /// </summary>
    public void SetMaxConcurrentDownloads(int max)
    {
        max = Math.Clamp(max, 1, AppConstants.MaxConcurrentDownloadsCap);
        if (max == _currentMaxDownloads) return;
        _currentMaxDownloads = max;
        _semaphore = new SemaphoreSlim(max, AppConstants.MaxConcurrentDownloadsCap);
    }

    public void SetHistoryService(DownloadHistoryService history) => _history = history;

    public void SetAccessToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Create a new download job for a course and start it immediately in the background.
    /// Returns the job so the UI can track it.
    /// </summary>
    public DownloadJob StartNewJob(
        long courseId,
        string courseName,
        List<CourseChapter> chapters,
        string downloadPath,
        string preferredQuality,
        bool downloadCaptions,
        string captionLang,
        bool skipExisting)
    {
        var safeCourseName = SanitizeFileName(courseName);
        var coursePath = Path.Combine(downloadPath, safeCourseName);

        var job = new DownloadJob
        {
            CourseId = courseId,
            CourseName = courseName,
            PreferredQuality = preferredQuality,
            DownloadCaptions = downloadCaptions,
            CaptionLanguage = captionLang,
            SkipExisting = skipExisting,
            StartedAt = DateTime.UtcNow
        };

        foreach (var chapter in chapters.Where(c => c.IsSelected))
        {
            var safeChapterName = SanitizeFileName($"{chapter.Index:D2} - {chapter.Title}");
            var chapterPath = Path.Combine(coursePath, safeChapterName);

            foreach (var lecture in chapter.Lectures.Where(l => l.IsSelected))
            {
                job.Tasks.Add(new DownloadTask
                {
                    LectureId = lecture.Id,
                    ChapterTitle = chapter.Title,
                    LectureTitle = lecture.Title,
                    ChapterIndex = chapter.Index,
                    LectureIndex = lecture.Index,
                    FileName = Path.Combine(chapterPath,
                        SanitizeFileName($"{lecture.Index:D2} - {lecture.Title}"))
                });
            }
        }

        Jobs.Insert(0, job);
        NotifyStateChangedImmediate();

        // Fire and forget — runs in background
        _ = RunJobAsync(job);

        return job;
    }

    private async Task RunJobAsync(DownloadJob job)
    {
        job.IsRunning = true;
        job.Cts = new CancellationTokenSource();
        var ct = job.Cts.Token;

        // Create history session
        DownloadSession? session = null;
        if (_history != null)
            session = await _history.StartSessionAsync(job.CourseId, job.CourseName, job.TotalTasks);

        var queued = job.Tasks.Where(t => t.Status is DownloadStatus.Queued or DownloadStatus.Failed).ToList();
        var parallelTasks = queued.Select(t => DownloadLectureAsync(job, t, session, ct));

        try
        {
            await Task.WhenAll(parallelTasks);
        }
        catch (OperationCanceledException) { }
        finally
        {
            job.IsRunning = false;
            NotifyStateChangedImmediate();
        }
    }

    /// <summary>Retry all failed tasks in a job.</summary>
    public void RetryFailed(DownloadJob job)
    {
        if (job.IsRunning) return;

        // Reset failed tasks to Queued
        foreach (var t in job.Tasks.Where(t => t.Status == DownloadStatus.Failed))
        {
            t.Status = DownloadStatus.Queued;
            t.ProgressPercent = 0;
            t.DownloadedBytes = 0;
            t.TotalBytes = 0;
            t.ErrorMessage = null;
        }

        // Re-run the job
        _ = RunJobAsync(job);
        NotifyStateChangedImmediate();
    }

    /// <summary>Retry all failed tasks across all jobs.</summary>
    public void RetryAllFailed()
    {
        foreach (var job in Jobs.Where(j => !j.IsRunning && j.FailedTasks > 0))
            RetryFailed(job);
    }

    /// <summary>Retry a single failed task within a job.</summary>
    public void RetrySingleTask(DownloadJob job, DownloadTask task)
    {
        if (task.Status != DownloadStatus.Failed) return;

        task.Status = DownloadStatus.Queued;
        task.ProgressPercent = 0;
        task.DownloadedBytes = 0;
        task.TotalBytes = 0;
        task.ErrorMessage = null;

        // If job isn't running, start it to pick up this task
        if (!job.IsRunning)
            _ = RunJobAsync(job);

        NotifyStateChangedImmediate();
    }

    public void PauseJob(DownloadJob job)
    {
        job.IsPaused = true;
        NotifyStateChangedImmediate();
    }

    public void ResumeJob(DownloadJob job)
    {
        job.IsPaused = false;
        NotifyStateChangedImmediate();
    }

    public void CancelJob(DownloadJob job)
    {
        job.Cts?.Cancel();
        job.IsPaused = false;
        job.IsRunning = false;
        foreach (var t in job.Tasks.Where(t => t.Status is DownloadStatus.Queued or DownloadStatus.Downloading))
            t.Status = DownloadStatus.Failed;
        NotifyStateChangedImmediate();
    }

    public void RemoveJob(DownloadJob job)
    {
        if (job.IsRunning) CancelJob(job);
        Jobs.Remove(job);
        NotifyStateChangedImmediate();
    }

    /// <summary>
    /// Retry failed/missing files from a history session — no navigation, starts downloading immediately.
    /// Re-fetches the curriculum to get accurate lecture IDs, then downloads only files
    /// that are failed or missing from disk.
    /// </summary>
    public async Task<DownloadJob?> RetryFromHistoryAsync(DownloadSession session)
    {
        var settings = await SettingsHelper.LoadAsync();
        var downloadPath = string.IsNullOrWhiteSpace(settings.DownloadPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "UdemyCourses")
            : settings.DownloadPath;

        var safeCourseName = SanitizeFileName(session.CourseName);
        var coursePath = Path.Combine(downloadPath, safeCourseName);

        // Build set of files that already exist on disk
        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(coursePath))
        {
            foreach (var file in Directory.EnumerateFiles(coursePath, "*", SearchOption.AllDirectories))
            {
                if (new FileInfo(file).Length > 1024)
                    existingFiles.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        // Fetch full curriculum from API to get lecture IDs
        var curriculumItems = await _api.GetCurriculumAsync(session.CourseId);

        var job = new DownloadJob
        {
            CourseId = session.CourseId,
            CourseName = session.CourseName,
            PreferredQuality = settings.PreferredQuality,
            DownloadCaptions = settings.DownloadCaptions,
            CaptionLanguage = settings.CaptionLanguage,
            SkipExisting = true,
            StartedAt = DateTime.UtcNow
        };

        int chapterIdx = 0, lectureIdx = 0;
        string currentChapterTitle = "";
        string currentChapterPath = coursePath;

        foreach (var item in curriculumItems)
        {
            if (item.IsChapter)
            {
                chapterIdx++;
                lectureIdx = 0;
                currentChapterTitle = item.Title;
                currentChapterPath = Path.Combine(coursePath,
                    SanitizeFileName($"{chapterIdx:D2} - {item.Title}"));
            }
            else if (item.IsLecture && item.IsPublished)
            {
                lectureIdx++;
                var safeName = SanitizeFileName($"{lectureIdx:D2} - {item.Title}");

                // Skip if file already exists on disk
                if (existingFiles.Contains(safeName))
                    continue;

                job.Tasks.Add(new DownloadTask
                {
                    LectureId = item.Id,
                    ChapterTitle = currentChapterTitle,
                    LectureTitle = item.Title,
                    ChapterIndex = chapterIdx,
                    LectureIndex = lectureIdx,
                    FileName = Path.Combine(currentChapterPath, safeName)
                });
            }
        }

        if (job.Tasks.Count == 0) return null;

        Jobs.Insert(0, job);
        NotifyStateChangedImmediate();
        _ = RunJobAsync(job);
        return job;
    }

    /// <summary>
    /// Auto-retry all history sessions that have failed files. Called on app startup.
    /// Ensures the access token is loaded from settings before making API calls.
    /// </summary>
    public async Task AutoRetryFailedFromHistoryAsync()
    {
        if (_history == null) return;

        // Ensure access token is set before retrying (may not be set if user navigated here directly)
        if (_http.DefaultRequestHeaders.Authorization == null)
        {
            var settings = await SettingsHelper.LoadAsync();
            if (!string.IsNullOrWhiteSpace(settings.AccessToken))
            {
                SetAccessToken(settings.AccessToken);
                _api.SetAccessToken(settings.AccessToken);
            }
            else
            {
                return; // No token — can't retry
            }
        }

        var sessions = await _history.GetSessionsAsync();
        var activeCourseIds = Jobs.Select(j => j.CourseId).ToHashSet();

        var failedSessions = sessions
            .Where(s => s.FailedFiles > 0 && !activeCourseIds.Contains(s.CourseId))
            .ToList();

        foreach (var session in failedSessions)
        {
            try
            {
                await RetryFromHistoryAsync(session);
            }
            catch { /* non-critical — skip sessions that fail to retry */ }
        }
    }

    // Legacy compat
    public void Pause() { foreach (var j in Jobs.Where(j => j.IsRunning)) PauseJob(j); }
    public void Resume() { foreach (var j in Jobs.Where(j => j.IsPaused)) ResumeJob(j); }
    public void Cancel() { foreach (var j in Jobs.Where(j => j.IsRunning)) CancelJob(j); }

    // ── Per-task download logic ──────────────────────────────────

    private async Task DownloadLectureAsync(DownloadJob job, DownloadTask task, DownloadSession? session, CancellationToken ct)
    {
        try
        {
            while (job.IsPaused && !ct.IsCancellationRequested)
                await Task.Delay(500, ct);
            ct.ThrowIfCancellationRequested();

            // Fetch lecture metadata OUTSIDE the semaphore — this is an API call,
            // not a download. Don't waste a download slot waiting for metadata.
            task.Status = DownloadStatus.Downloading;
            NotifyStateChangedImmediate();

            var lecture = await _api.GetLectureAsync(job.CourseId, task.LectureId, ct);
            if (lecture?.Asset == null)
            {
                task.Status = DownloadStatus.Skipped;
                task.ErrorMessage = "No asset data available";
                NotifyStateChangedImmediate();
                return;
            }

            var asset = lecture.Asset;

            // Now acquire a download slot for the actual file transfer
            await _semaphore.WaitAsync(ct);
            try
            {
                if (asset.AssetType == "Video")
                    await DownloadVideoAsync(job, task, asset, ct);
                else if (asset.AssetType == "Article")
                    await SaveArticleAsync(task, asset);
                else if (asset.AssetType is "File" or "E-Book")
                    await DownloadFileAssetAsync(task, asset, job.SkipExisting, ct);
                else
                {
                    task.Status = DownloadStatus.Skipped;
                    task.ErrorMessage = $"Unsupported asset type: {asset.AssetType}";
                }

                if (job.DownloadCaptions && asset.Captions?.Count > 0)
                    await DownloadCaptionsAsync(task, asset.Captions, job.CaptionLanguage, ct);

                if (task.Status == DownloadStatus.Downloading)
                    task.Status = DownloadStatus.Completed;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = "Cancelled";
        }
        catch (AuthenticationExpiredException)
        {
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = "Authentication expired — update your token in Settings";
        }
        catch (IOException ex) when (ex.HResult == -2147024784 /* ERROR_DISK_FULL */
            || ex.Message.Contains("disk", StringComparison.OrdinalIgnoreCase))
        {
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = "Disk full — free space and retry";
            // Cancel remaining tasks in this job to avoid repeated failures
            job.Cts?.Cancel();
        }
        catch (Exception ex)
        {
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = InputValidator.RedactSecrets(ex.Message);
            Log.Warning("Download failed for lecture {LectureId}: {Error}",
                task.LectureId, InputValidator.RedactSecrets(ex.ToString()));
        }
        finally
        {
            if (_history != null && session != null &&
                task.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Skipped)
            {
                try { await _history.RecordFileAsync(session, task); } catch { }
            }

            NotifyStateChangedImmediate();
        }
    }

    private async Task DownloadVideoAsync(DownloadJob job, DownloadTask task, UdemyAsset asset, CancellationToken ct)
    {
        string? videoUrl = null;
        string? hlsUrl = null;
        string ext = ".mp4";

        // 1. Prefer direct download URLs (these are never DRM-encrypted)
        if (asset.DownloadUrls?.Video is { Count: > 0 })
        {
            var vids = asset.DownloadUrls.Video.OrderByDescending(v => ParseResolution(v.Label)).ToList();
            var preferred = vids.FirstOrDefault(v => v.Label == job.PreferredQuality) ?? vids.First();
            videoUrl = preferred.Src;
        }

        // 2. Try unprotected MP4 media sources (skip DRM-encrypted DASH/MPD sources)
        if (string.IsNullOrEmpty(videoUrl) && asset.MediaSources is { Count: > 0 })
        {
            var mp4Sources = asset.UnprotectedMediaSources
                .Where(m => m.Type == "video/mp4")
                .OrderByDescending(m => ParseResolution(m.Label)).ToList();
            if (mp4Sources.Count > 0)
            {
                var preferred = mp4Sources.FirstOrDefault(m => m.Label == job.PreferredQuality) ?? mp4Sources.First();
                videoUrl = preferred.Src;
            }
        }

        // 3. Try unprotected HLS sources (skip encrypted HLS with EXT-X-KEY)
        if (string.IsNullOrEmpty(videoUrl) && asset.MediaSources is { Count: > 0 })
        {
            var hls = asset.UnprotectedMediaSources
                .FirstOrDefault(m => m.Type == "application/x-mpegURL");
            if (hls != null) hlsUrl = hls.Src;
        }

        // 4. If no unprotected source found and asset has DRM, skip with clear message
        if (string.IsNullOrEmpty(videoUrl) && string.IsNullOrEmpty(hlsUrl) && asset.IsDrmProtected)
        {
            task.Status = DownloadStatus.Skipped;
            task.ErrorMessage = "DRM protected — cannot download encrypted video";
            Log.Information("Skipping DRM-protected lecture {LectureId}: {Title}",
                task.LectureId, task.LectureTitle);
            return;
        }

        var isHls = !string.IsNullOrEmpty(hlsUrl) && string.IsNullOrEmpty(videoUrl);
        if (isHls) ext = ".ts";

        var filePath = task.FileName + ext;
        var altPath = task.FileName + (isHls ? ".mp4" : ".ts");

        if (job.SkipExisting &&
            ((File.Exists(filePath) && new FileInfo(filePath).Length > 1024) ||
             (File.Exists(altPath) && new FileInfo(altPath).Length > 1024)))
        {
            task.Status = DownloadStatus.Completed;
            task.ProgressPercent = 100;
            task.ErrorMessage = "Already exists";
            return;
        }

        if (!string.IsNullOrEmpty(videoUrl))
            await DownloadFileWithProgressAsync(videoUrl, filePath, task, job, ct);
        else if (!string.IsNullOrEmpty(hlsUrl))
            await HlsDownloader.DownloadAsync(_http, hlsUrl, filePath, job.PreferredQuality,
                task, () => job.IsPaused, NotifyStateChanged, ct);
        else
        {
            task.Status = DownloadStatus.Skipped;
            task.ErrorMessage = "No downloadable video source found";
        }
    }

    private async Task SaveArticleAsync(DownloadTask task, UdemyAsset asset)
    {
        var filePath = task.FileName + ".html";
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var title = System.Net.WebUtility.HtmlEncode(task.LectureTitle);
        var body = asset.Body ?? "<p>No content available.</p>";
        var html = "<!DOCTYPE html><html><head>" +
            "<meta charset=\"utf-8\">" +
            $"<title>{title}</title>" +
            "<style>body{ font-family: Arial, sans-serif; max-width: 800px; margin: 20px auto; padding: 0 20px; }</style>" +
            "</head><body>" +
            $"<h1>{title}</h1>" + body +
            "</body></html>";

        await File.WriteAllTextAsync(filePath, html);
        task.Status = DownloadStatus.Completed;
        task.ProgressPercent = 100;
    }

    private async Task DownloadFileAssetAsync(DownloadTask task, UdemyAsset asset, bool skipExisting, CancellationToken ct)
    {
        string? fileUrl = null;
        string ext = Path.GetExtension(asset.Filename ?? ".file");

        if (asset.DownloadUrls?.File is { Count: > 0 })
            fileUrl = asset.DownloadUrls.File.First().FileUrl;

        if (string.IsNullOrEmpty(fileUrl))
        {
            task.Status = DownloadStatus.Skipped;
            task.ErrorMessage = "No download URL for file asset";
            return;
        }

        var filePath = task.FileName + ext;
        await DownloadFileWithProgressAsync(fileUrl, filePath, task, null, ct);
    }

    private async Task DownloadCaptionsAsync(DownloadTask task, List<UdemyCaption> captions, string captionLang, CancellationToken ct)
    {
        var caption = captions.FirstOrDefault(c =>
            c.LocaleId.StartsWith(captionLang, StringComparison.OrdinalIgnoreCase))
            ?? captions.FirstOrDefault();
        if (caption == null) return;

        try
        {
            var srtPath = task.FileName + $".{caption.LocaleId}.srt";
            var dir = Path.GetDirectoryName(srtPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var content = await _http.GetStringAsync(caption.Url, ct);
            await File.WriteAllTextAsync(srtPath, content, ct);
        }
        catch { }
    }

    private async Task DownloadFileWithProgressAsync(
        string url, string filePath, DownloadTask task, DownloadJob? job, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Validate output path is within expected directory
        var (pathValid, pathError) = InputValidator.ValidatePath(filePath, dir);
        if (!pathValid)
            throw new InvalidOperationException($"Unsafe file path: {pathError}");

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        // Handle authentication failures
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new AuthenticationExpiredException();

        response.EnsureSuccessStatusCode();

        // Check disk space before downloading
        var contentLength = response.Content.Headers.ContentLength ?? 0;
        if (contentLength > 0)
        {
            var (hasSpace, available) = SafeFileWriter.CheckDiskSpace(filePath, contentLength);
            if (!hasSpace)
                throw new IOException($"Insufficient disk space. Need {contentLength / 1048576.0:F1} MB, have {available / 1048576.0:F1} MB available.");
        }

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        task.TotalBytes = totalBytes;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, true);

        var buffer = new byte[1048576]; // 1 MB buffer
        long downloaded = 0;
        int bytesRead;
        var lastUpdate = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            while (job is { IsPaused: true } && !ct.IsCancellationRequested)
                await Task.Delay(300, ct);
            ct.ThrowIfCancellationRequested();

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloaded += bytesRead;
            task.DownloadedBytes = downloaded;
            if (totalBytes > 0) task.ProgressPercent = downloaded * 100.0 / totalBytes;

            if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds > 250)
            {
                lastUpdate = DateTime.UtcNow;
                NotifyStateChanged();
            }
        }

        task.ProgressPercent = 100;
        task.DownloadedBytes = downloaded;
        task.TotalBytes = downloaded;
    }

    private static int ParseResolution(string label)
    {
        var match = ResolutionRegex().Match(label);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    public static string SanitizeFileName(string name) => InputValidator.SanitizeFileName(name);

    private void NotifyStateChanged()
    {
        // Throttle UI notifications to max every 200ms to avoid re-render backpressure
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastNotifyTicks);
        if (now - last < 200) return;
        Interlocked.Exchange(ref _lastNotifyTicks, now);
        OnStateChanged?.Invoke();
    }

    /// <summary>Force a state notification regardless of throttle (for status changes like completed/failed).</summary>
    private void NotifyStateChangedImmediate()
    {
        Interlocked.Exchange(ref _lastNotifyTicks, Environment.TickCount64);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// One-click course download: fetches curriculum, skips already-downloaded files, and starts downloading everything.
    /// Returns the job, or null if all files are already downloaded.
    /// Throws on API/network errors so the caller can display them.
    /// </summary>
    public async Task<DownloadJob?> StartCourseDownloadAsync(long courseId)
    {
        var settings = await SettingsHelper.LoadAsync();
        var downloadPath = string.IsNullOrWhiteSpace(settings.DownloadPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "UdemyCourses")
            : settings.DownloadPath;

        // Fetch course name
        string courseName = $"Course_{courseId}";
        try
        {
            var courseInfo = await _api.GetCourseInfoAsync(courseId);
            if (courseInfo != null) courseName = courseInfo.Title;
        }
        catch { /* fall back to generic name */ }

        var safeCourseName = SanitizeFileName(courseName);
        var coursePath = Path.Combine(downloadPath, safeCourseName);

        // Fetch full curriculum
        var curriculumItems = await _api.GetCurriculumAsync(courseId);

        // Build chapters with all lectures selected
        var chapters = new List<CourseChapter>();
        int chapterIdx = 0, lectureIdx = 0;
        CourseChapter? currentChapter = null;

        foreach (var item in curriculumItems)
        {
            if (item.IsChapter)
            {
                chapterIdx++;
                lectureIdx = 0;
                currentChapter = new CourseChapter
                {
                    Id = item.Id,
                    Index = chapterIdx,
                    Title = item.Title,
                    IsSelected = true
                };
                chapters.Add(currentChapter);
            }
            else if (item.IsLecture && item.IsPublished)
            {
                if (currentChapter == null)
                {
                    chapterIdx++;
                    lectureIdx = 0;
                    currentChapter = new CourseChapter
                    {
                        Id = 0,
                        Index = chapterIdx,
                        Title = "Introduction",
                        IsSelected = true
                    };
                    chapters.Add(currentChapter);
                }

                lectureIdx++;
                currentChapter.Lectures.Add(new CourseLecture
                {
                    Id = item.Id,
                    Index = lectureIdx,
                    Title = item.Title,
                    AssetType = item.Asset?.AssetType ?? "Unknown",
                    DurationSeconds = item.Asset?.TimeEstimation ?? 0,
                    IsSelected = true
                });
            }
        }

        // Scan for already-downloaded files and deselect them
        if (Directory.Exists(coursePath))
        {
            var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(coursePath, "*", SearchOption.AllDirectories))
            {
                if (new FileInfo(file).Length > 1024)
                    existingFiles.Add(Path.GetFileNameWithoutExtension(file));
            }

            foreach (var chapter in chapters)
            {
                foreach (var lecture in chapter.Lectures)
                {
                    var safeName = SanitizeFileName($"{lecture.Index:D2} - {lecture.Title}");
                    if (existingFiles.Contains(safeName))
                    {
                        lecture.IsDownloaded = true;
                        lecture.IsSelected = false;
                    }
                }

                // If all lectures in chapter are downloaded, deselect chapter
                if (chapter.Lectures.All(l => !l.IsSelected))
                    chapter.IsSelected = false;
            }
        }

        // If nothing left to download, return null
        var selectedCount = chapters.Where(c => c.IsSelected).Sum(c => c.Lectures.Count(l => l.IsSelected));
        if (selectedCount == 0) return null;

        // Start the download job
        return StartNewJob(
            courseId,
            courseName,
            chapters,
            downloadPath,
            settings.PreferredQuality,
            settings.DownloadCaptions,
            settings.CaptionLanguage,
            settings.SkipExistingFiles);
    }

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex ResolutionRegex();
}
