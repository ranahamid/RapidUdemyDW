using System.Text.Json;
using Serilog;
using RapidUdemyDW.Models;

namespace RapidUdemyDW.Services;

public static class SettingsHelper
{
    private const string SecureTokenKey = "udemy_access_token";

    private static string FilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "udemy_dl_settings.json");

    public static async Task<AppSettings> LoadAsync()
    {
        try
        {
            AppSettings settings;
            if (File.Exists(FilePath))
            {
                var json = await File.ReadAllTextAsync(FilePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefault();
            }
            else
            {
                settings = CreateDefault();
            }

            // Load token from secure storage (platform keychain/credential manager)
            try
            {
                settings.AccessToken = await SecureStorage.Default.GetAsync(SecureTokenKey) ?? string.Empty;
            }
            catch (Exception ex)
            {
                // SecureStorage can fail on some platforms — fall back to in-memory only
                Log.Warning(ex, "SecureStorage unavailable, token will not persist");
            }

            // Migrate: if token was previously saved in the JSON file, move it to secure storage
            if (string.IsNullOrEmpty(settings.AccessToken) && !string.IsNullOrEmpty(settings._legacyToken))
            {
                settings.AccessToken = settings._legacyToken;
                await SaveTokenSecurelyAsync(settings.AccessToken);
                settings._legacyToken = null;
                await SaveSettingsFileAsync(settings);
                Log.Information("Migrated access token from plaintext to secure storage");
            }

            return settings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings");
            return CreateDefault();
        }
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        // Save token to secure storage (never to the JSON file)
        await SaveTokenSecurelyAsync(settings.AccessToken);

        // Save everything else (without the token) to JSON
        await SaveSettingsFileAsync(settings);
    }

    private static async Task SaveTokenSecurelyAsync(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                SecureStorage.Default.Remove(SecureTokenKey);
            else
                await SecureStorage.Default.SetAsync(SecureTokenKey, token);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SecureStorage write failed — token will only persist in memory");
        }
    }

    private static async Task SaveSettingsFileAsync(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Create a copy without the token for serialization
        var toSave = new AppSettingsFile
        {
            DownloadPath = settings.DownloadPath,
            PreferredQuality = settings.PreferredQuality,
            DownloadCaptions = settings.DownloadCaptions,
            CaptionLanguage = settings.CaptionLanguage,
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads,
            SkipExistingFiles = settings.SkipExistingFiles,
            EulaAcceptedVersion = settings.EulaAcceptedVersion,
        };

        var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(FilePath, json);
    }

    /// <summary>
    /// Validate that the configured download path exists and is writable.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    public static (bool Valid, string? Error) ValidateDownloadPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Download path cannot be empty.");

        try
        {
            // Expand environment variables
            path = Environment.ExpandEnvironmentVariables(path);

            if (!Path.IsPathRooted(path))
                return (false, "Download path must be an absolute path.");

            // Create directory if needed
            Directory.CreateDirectory(path);

            // Test write access with a temp file
            var testFile = Path.Combine(path, $".write_test_{Guid.NewGuid():N}");
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

    private static AppSettings CreateDefault() => new()
    {
        DownloadPath = AppConstants.DefaultDownloadPath,
        PreferredQuality = "1080",
        DownloadCaptions = true,
        CaptionLanguage = "en",
        MaxConcurrentDownloads = AppConstants.DefaultConcurrentDownloads,
        SkipExistingFiles = true
    };

    /// <summary>
    /// Internal DTO for JSON serialization (excludes sensitive token).
    /// </summary>
    private class AppSettingsFile
    {
        public string DownloadPath { get; set; } = string.Empty;
        public string PreferredQuality { get; set; } = "1080";
        public bool DownloadCaptions { get; set; } = true;
        public string CaptionLanguage { get; set; } = "en";
        public int MaxConcurrentDownloads { get; set; } = AppConstants.DefaultConcurrentDownloads;
        public bool SkipExistingFiles { get; set; } = true;
        public string? EulaAcceptedVersion { get; set; }
    }
}
