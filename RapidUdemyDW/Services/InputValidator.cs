using System.Text.RegularExpressions;

namespace RapidUdemyDW.Services;

/// <summary>
/// Centralized input validation for URLs, paths, filenames, and user input.
/// Prevents path traversal, unsafe filenames, and invalid content.
/// </summary>
public static partial class InputValidator
{
    /// <summary>Maximum filename length (excluding extension).</summary>
    private const int MaxFileNameLength = 200;

    /// <summary>Maximum total path length on Windows.</summary>
    private const int MaxPathLength = 260;

    /// <summary>
    /// Reserved device names on Windows that cannot be used as filenames.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Sanitize a filename by removing invalid characters, preventing path traversal,
    /// blocking reserved device names, and enforcing length limits.
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unnamed";

        // 1. Remove path traversal sequences FIRST
        name = name.Replace("..", "_", StringComparison.Ordinal);
        name = name.Replace("/", "_", StringComparison.Ordinal);
        name = name.Replace("\\", "_", StringComparison.Ordinal);

        // 2. Remove invalid filename characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // 3. Remove control characters
        sanitized = ControlCharRegex().Replace(sanitized, "");

        // 4. Trim whitespace and trailing dots (Windows rejects these)
        sanitized = sanitized.Trim().TrimEnd('.').TrimEnd();

        // 5. Check for reserved device names
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
        if (ReservedNames.Contains(nameWithoutExt))
            sanitized = "_" + sanitized;

        // 6. Enforce length limit
        if (sanitized.Length > MaxFileNameLength)
            sanitized = sanitized[..MaxFileNameLength];

        // 7. Final fallback if empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "unnamed";

        return sanitized;
    }

    /// <summary>
    /// Validate that a path is safe for file operations:
    /// - Must be absolute
    /// - Must not contain path traversal
    /// - Must be within expected base directory
    /// </summary>
    public static (bool Valid, string? Error) ValidatePath(string path, string? expectedBasePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Path cannot be empty.");

        try
        {
            // Normalize the path
            var fullPath = Path.GetFullPath(path);

            if (!Path.IsPathRooted(fullPath))
                return (false, "Path must be absolute.");

            // Check for path traversal: the full (resolved) path must start with the base
            if (!string.IsNullOrEmpty(expectedBasePath))
            {
                var fullBase = Path.GetFullPath(expectedBasePath);
                if (!fullPath.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                    return (false, "Path traversal detected — path is outside the expected directory.");
            }

            // Check total path length
            if (fullPath.Length > MaxPathLength)
                return (false, $"Path exceeds maximum length ({MaxPathLength} characters).");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Invalid path: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate a URL — must be HTTP(S) and well-formed.
    /// </summary>
    public static (bool Valid, string? Error) ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "URL cannot be empty.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return (false, "Invalid URL format.");

        if (uri.Scheme is not ("http" or "https"))
            return (false, "Only HTTP and HTTPS URLs are allowed.");

        return (true, null);
    }

    /// <summary>
    /// Validate a download path — must be absolute, writable, and not a system directory.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static (bool Valid, string? Error) ValidateDownloadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Download path cannot be empty.");

        try
        {
            path = Environment.ExpandEnvironmentVariables(path);

            if (!Path.IsPathRooted(path))
                return (false, "Download path must be an absolute path.");

            var fullPath = Path.GetFullPath(path);

            // Reject system directories
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (fullPath.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
                return (false, "Cannot use Windows system directory as download path.");

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
                return (false, "Cannot use Program Files directory as download path.");

            // Create directory if needed
            Directory.CreateDirectory(fullPath);

            // Test write access
            var testFile = Path.Combine(fullPath, $".write_test_{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return (true, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "No write permission to this folder.");
        }
        catch (IOException ex)
        {
            return (false, $"Path error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Invalid path: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate that a file extension is in the allowed set.
    /// </summary>
    public static bool IsAllowedFileExtension(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext is ".mp4" or ".ts" or ".html" or ".srt" or ".vtt"
            or ".pdf" or ".zip" or ".rar" or ".doc" or ".docx"
            or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt"
            or ".epub" or ".mobi" or ".py" or ".ipynb" or ".json"
            or ".csv" or ".xml" or ".js" or ".css" or ".sql"
            or ".java" or ".cs" or ".cpp" or ".c" or ".h"
            or ".rb" or ".go" or ".rs" or ".swift" or ".kt"
            or ".php" or ".r" or ".m" or ".mat" or ".png"
            or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp"
            or ".mp3" or ".wav" or ".aac" or ".flac" or ".ogg";
    }

    /// <summary>
    /// Redact sensitive values (tokens, auth headers, signed URLs) from a string
    /// before logging or displaying to the user.
    /// </summary>
    public static string RedactSecrets(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Redact Bearer tokens
        input = BearerTokenRegex().Replace(input, "Bearer [REDACTED]");

        // Redact access_token query parameters
        input = AccessTokenParamRegex().Replace(input, "access_token=[REDACTED]");

        // Redact token-like patterns in URLs (signed URLs with long query strings)
        input = SignedUrlRegex().Replace(input, "$1=[REDACTED]");

        // Redact Cookie headers
        input = CookieHeaderRegex().Replace(input, "Cookie: [REDACTED]");

        return input;
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex ControlCharRegex();

    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"access_token=[^&\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex AccessTokenParamRegex();

    [GeneratedRegex(@"(Signature|X-Amz-Credential|X-Amz-Security-Token|token|key)=[^&\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex SignedUrlRegex();

    [GeneratedRegex(@"Cookie:\s*\S+", RegexOptions.IgnoreCase)]
    private static partial Regex CookieHeaderRegex();
}
