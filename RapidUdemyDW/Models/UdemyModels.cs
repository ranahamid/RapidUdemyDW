using System.Text.Json.Serialization;

namespace RapidUdemyDW.Models;

// ── Course list response ──────────────────────────────────────────
public class UdemyCourseListResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<UdemyCourse> Results { get; set; } = [];
}

public class UdemyCourse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("image_240x135")]
    public string? ImageSmall { get; set; }

    [JsonPropertyName("image_480x270")]
    public string? ImageMedium { get; set; }

    [JsonPropertyName("completion_ratio")]
    public double CompletionRatio { get; set; }

    [JsonPropertyName("num_published_lectures")]
    public int NumPublishedLectures { get; set; }

    [JsonPropertyName("visible_instructors")]
    public List<UdemyInstructor> Instructors { get; set; } = [];

    // Populated separately
    public string InstructorNames =>
        Instructors.Count > 0
            ? string.Join(", ", Instructors.Select(i => i.DisplayName))
            : "Unknown";
}

public class UdemyInstructor
{
    [JsonPropertyName("title")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? Name { get; set; }
}

// ── Curriculum / Chapters / Lectures ──────────────────────────────
public class UdemyCurriculumResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("results")]
    public List<UdemyCurriculumItem> Results { get; set; } = [];
}

public class UdemyCurriculumItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("_class")]
    public string ItemClass { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; }

    [JsonPropertyName("asset")]
    public UdemyAsset? Asset { get; set; }

    [JsonPropertyName("supplementary_assets")]
    public List<UdemyAsset>? SupplementaryAssets { get; set; }

    // Helpers
    public bool IsChapter => ItemClass == "chapter";
    public bool IsLecture => ItemClass == "lecture";
}

public class UdemyAsset
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("asset_type")]
    public string AssetType { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("time_estimation")]
    public long TimeEstimation { get; set; }

    [JsonPropertyName("media_sources")]
    public List<UdemyMediaSource>? MediaSources { get; set; }

    [JsonPropertyName("captions")]
    public List<UdemyCaption>? Captions { get; set; }

    [JsonPropertyName("download_urls")]
    public UdemyDownloadUrls? DownloadUrls { get; set; }

    // For supplementary assets / articles
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

public class UdemyMediaSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("src")]
    public string Src { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

public class UdemyCaption
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("locale_id")]
    public string LocaleId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class UdemyDownloadUrls
{
    [JsonPropertyName("Video")]
    public List<UdemyMediaSource>? Video { get; set; }

    [JsonPropertyName("File")]
    public List<UdemyFileDownload>? File { get; set; }
}

public class UdemyFileDownload
{
    [JsonPropertyName("file")]
    public string FileUrl { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

// ── Lecture stream/download response ──────────────────────────────
public class UdemyLectureResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("asset")]
    public UdemyAsset? Asset { get; set; }
}

// ── App-level models ──────────────────────────────────────────────
public class CourseChapter
{
    public long Id { get; set; }
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<CourseLecture> Lectures { get; set; } = [];
    public bool IsSelected { get; set; } = true;
}

public class CourseLecture
{
    public long Id { get; set; }
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public long DurationSeconds { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsDownloaded { get; set; }
    public bool HasCaptions { get; set; }

    public string DurationDisplay
    {
        get
        {
            if (DurationSeconds <= 0) return "";
            var ts = TimeSpan.FromSeconds(DurationSeconds);
            return ts.Hours > 0
                ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }
}

public class DownloadTask
{
    public long LectureId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public string LectureTitle { get; set; } = string.Empty;
    public int ChapterIndex { get; set; }
    public int LectureIndex { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public double ProgressPercent { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DisplayName => $"{ChapterIndex:D2}.{LectureIndex:D2} - {LectureTitle}";

    public string DownloadedDisplay =>
        TotalBytes > 0
            ? $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}"
            : DownloadedBytes > 0
                ? FormatBytes(DownloadedBytes)
                : "";

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _ => $"{bytes / 1073741824.0:F2} GB"
    };
}

/// <summary>
/// Represents an independent background download job for one course.
/// Multiple jobs can run concurrently.
/// </summary>
public class DownloadJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public List<DownloadTask> Tasks { get; set; } = [];
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public CancellationTokenSource? Cts { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // Per-job download params
    public string PreferredQuality { get; set; } = "1080";
    public bool DownloadCaptions { get; set; } = true;
    public string CaptionLanguage { get; set; } = "en";
    public bool SkipExisting { get; set; } = true;

    public int TotalTasks => Tasks.Count;
    public int CompletedTasks => Tasks.Count(t => t.Status == DownloadStatus.Completed);
    public int FailedTasks => Tasks.Count(t => t.Status == DownloadStatus.Failed);
    public int SkippedTasks => Tasks.Count(t => t.Status == DownloadStatus.Skipped);
    public double OverallProgress => TotalTasks > 0 ? CompletedTasks * 100.0 / TotalTasks : 0;
    public bool IsComplete => !IsRunning && Tasks.Count > 0 && Tasks.All(t => t.Status is not DownloadStatus.Queued and not DownloadStatus.Downloading);
}

public enum DownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Failed,
    Skipped
}

public class AppSettings
{
    public string AccessToken { get; set; } = string.Empty;
    public string DownloadPath { get; set; } = string.Empty;
    public string PreferredQuality { get; set; } = "1080";
    public bool DownloadCaptions { get; set; } = true;
    public string CaptionLanguage { get; set; } = "en";
    public int MaxConcurrentDownloads { get; set; } = 3;
    public bool SkipExistingFiles { get; set; } = true;
}

// ── Download History ──────────────────────────────────────────────
public class DownloadHistoryRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public long CourseId { get; set; }
    public long LectureId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string LectureTitle { get; set; } = string.Empty;
    public int ChapterIndex { get; set; }
    public int LectureIndex { get; set; }
    public DownloadStatus Status { get; set; }
    public long FileSize { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public string DisplayName => $"{ChapterIndex:D2}.{LectureIndex:D2} - {LectureTitle}";

    public string FileSizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1048576 => $"{FileSize / 1024.0:F1} KB",
        < 1073741824 => $"{FileSize / 1048576.0:F1} MB",
        _ => $"{FileSize / 1073741824.0:F2} GB"
    };
}

public class DownloadSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public long TotalSize { get; set; }
    public List<DownloadHistoryRecord> Files { get; set; } = [];

    public string TotalSizeDisplay => TotalSize switch
    {
        < 1024 => $"{TotalSize} B",
        < 1048576 => $"{TotalSize / 1024.0:F1} KB",
        < 1073741824 => $"{TotalSize / 1048576.0:F1} MB",
        _ => $"{TotalSize / 1073741824.0:F2} GB"
    };

    public string StatusSummary
    {
        get
        {
            var parts = new List<string>();
            if (CompletedFiles > 0) parts.Add($"✅ {CompletedFiles}");
            if (FailedFiles > 0) parts.Add($"❌ {FailedFiles}");
            if (SkippedFiles > 0) parts.Add($"⏭️ {SkippedFiles}");
            return string.Join("  ", parts);
        }
    }
}
