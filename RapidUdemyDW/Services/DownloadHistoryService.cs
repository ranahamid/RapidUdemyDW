using System.Text.Json;
using RapidUdemyDW.Models;
using Serilog;

namespace RapidUdemyDW.Services;

/// <summary>
/// Persists download history to a local JSON file so it survives app restarts.
/// Uses atomic writes and backup recovery to prevent data corruption.
/// </summary>
public class DownloadHistoryService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private static string HistoryPath =>
        Path.Combine(FileSystem.AppDataDirectory, "download_history.json");

    private List<DownloadSession>? _sessions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Maximum number of sessions to retain.</summary>
    private const int MaxRetainedSessions = 50;

    /// <summary>Maximum age for history entries before cleanup.</summary>
    private static readonly TimeSpan MaxRetention = TimeSpan.FromDays(90);

    /// <summary>
    /// Get all download sessions (most recent first).
    /// </summary>
    public async Task<List<DownloadSession>> GetSessionsAsync()
    {
        if (_sessions != null)
            return _sessions;

        await _lock.WaitAsync();
        try
        {
            if (_sessions != null) return _sessions;

            // Use recovery-aware read (tries backup if main file corrupt)
            var json = await SafeFileWriter.ReadWithRecoveryAsync(HistoryPath);
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = JsonSerializer.Deserialize<List<DownloadSession>>(json, JsonOpts) ?? [];

                // Deduplicate: keep only the most recent session per course
                _sessions = loaded
                    .GroupBy(s => s.CourseId)
                    .Select(g => g.OrderByDescending(s => s.StartedAt).First())
                    .OrderByDescending(s => s.StartedAt)
                    .ToList();

                // Apply retention policy
                var cutoff = DateTime.UtcNow - MaxRetention;
                var beforeCount = _sessions.Count;
                _sessions.RemoveAll(s => s.StartedAt < cutoff);

                // If we cleaned up, save
                if (_sessions.Count != loaded.Count)
                    await SaveAsync();
            }
            else
            {
                _sessions = [];
            }

            return _sessions;
        }
        catch
        {
            _sessions = [];
            return _sessions;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Start a new download session.
    /// </summary>
    public async Task<DownloadSession> StartSessionAsync(long courseId, string courseName, int totalFiles)
    {
        var sessions = await GetSessionsAsync();

        // One record per course — remove any existing sessions for this course
        sessions.RemoveAll(s => s.CourseId == courseId);

        var session = new DownloadSession
        {
            CourseId = courseId,
            CourseName = courseName,
            StartedAt = DateTime.UtcNow,
            TotalFiles = totalFiles
        };
        sessions.Insert(0, session);

        // Keep only last 50 sessions
        if (sessions.Count > 50)
            sessions.RemoveRange(50, sessions.Count - 50);

        await SaveAsync();
        return session;
    }

    /// <summary>
    /// Remove all history sessions for a given course.
    /// </summary>
    public async Task RemoveByCourseIdAsync(long courseId)
    {
        var sessions = await GetSessionsAsync();
        sessions.RemoveAll(s => s.CourseId == courseId);
        await SaveAsync();
    }

    /// <summary>
    /// Record a completed/failed/skipped file in the current session.
    /// </summary>
    public async Task RecordFileAsync(DownloadSession session, DownloadTask task)
    {
        var record = new DownloadHistoryRecord
        {
            CourseId = session.CourseId,
            LectureId = task.LectureId,
            CourseName = session.CourseName,
            LectureTitle = task.LectureTitle,
            ChapterIndex = task.ChapterIndex,
            LectureIndex = task.LectureIndex,
            Status = task.Status,
            FileSize = task.DownloadedBytes,
            FilePath = task.FileName,
            ErrorMessage = task.ErrorMessage,
            CompletedAt = DateTime.UtcNow
        };

        session.Files.Add(record);

        switch (task.Status)
        {
            case DownloadStatus.Completed:
                session.CompletedFiles++;
                session.TotalSize += task.DownloadedBytes;
                break;
            case DownloadStatus.Failed:
                session.FailedFiles++;
                break;
            case DownloadStatus.Skipped:
                session.SkippedFiles++;
                break;
        }

        await SaveAsync();
    }

    /// <summary>
    /// Delete a single session from history.
    /// </summary>
    public async Task DeleteSessionAsync(string sessionId)
    {
        var sessions = await GetSessionsAsync();
        sessions.RemoveAll(s => s.Id == sessionId);
        await SaveAsync();
    }

    /// <summary>
    /// Clear all download history.
    /// </summary>
    public async Task ClearAllAsync()
    {
        _sessions = [];
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_sessions ?? [], JsonOpts);
            await SafeFileWriter.WriteAllTextAtomicAsync(HistoryPath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save download history");
        }
        finally
        {
            _lock.Release();
        }
    }
}
