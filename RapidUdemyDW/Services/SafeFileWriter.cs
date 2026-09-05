using Serilog;

namespace RapidUdemyDW.Services;

/// <summary>
/// Provides atomic file write operations to prevent data corruption
/// from crashes, power loss, or concurrent access.
/// Uses write-to-temp-then-rename pattern.
/// </summary>
public static class SafeFileWriter
{
    /// <summary>
    /// Atomically write text content to a file.
    /// Writes to a temporary file first, then replaces the target.
    /// </summary>
    public static async Task WriteAllTextAtomicAsync(string filePath, string content)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = filePath + $".tmp.{Guid.NewGuid():N}";

        try
        {
            // Write to temp file
            await File.WriteAllTextAsync(tempPath, content);

            // Atomic replace (on Windows, File.Move with overwrite is as atomic as possible)
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            // Clean up temp file on failure
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Read a JSON file with recovery from corruption.
    /// If the main file is corrupt, tries the backup.
    /// </summary>
    public static async Task<string?> ReadWithRecoveryAsync(string filePath)
    {
        var backupPath = filePath + ".bak";

        // Try main file first
        if (File.Exists(filePath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    // Create/update backup on successful read
                    try { File.Copy(filePath, backupPath, overwrite: true); } catch { /* best effort */ }
                    return content;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Primary file corrupt or unreadable: {FilePath}", filePath);
            }
        }

        // Fall back to backup
        if (File.Exists(backupPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(backupPath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    Log.Information("Recovered data from backup: {BackupPath}", backupPath);
                    // Restore main file from backup
                    try { File.Copy(backupPath, filePath, overwrite: true); } catch { /* best effort */ }
                    return content;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Backup file also corrupt: {BackupPath}", backupPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Check available disk space before writing.
    /// Returns (hasSpace, availableBytes).
    /// </summary>
    public static (bool HasSpace, long AvailableBytes) CheckDiskSpace(string path, long requiredBytes = 0)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
                return (true, 0); // Can't determine — assume OK

            var driveInfo = new DriveInfo(root);
            if (!driveInfo.IsReady)
                return (false, 0);

            var available = driveInfo.AvailableFreeSpace;

            // Require at least 100 MB free space as safety margin
            const long safetyMargin = 100 * 1024 * 1024;
            var needed = requiredBytes > 0 ? requiredBytes + safetyMargin : safetyMargin;

            return (available >= needed, available);
        }
        catch
        {
            return (true, 0); // Can't determine — assume OK
        }
    }
}
