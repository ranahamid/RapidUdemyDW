using System.Text.Json;
using Serilog;
using RapidUdemyDW.Models;

namespace RapidUdemyDW.Services;

public static class SettingsHelper
{
    private const string SecureTokenKey = "udemy_access_token";
    private const int CurrentSchemaVersion = 2;
    private static readonly JsonSerializerOptions IndentedJsonOpts = new() { WriteIndented = true };

    private static string FilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "udemy_dl_settings.json");

    public static async Task<AppSettings> LoadAsync()
    {
        try
        {
            AppSettings settings;
            var json = await SafeFileWriter.ReadWithRecoveryAsync(FilePath);
            if (!string.IsNullOrEmpty(json))
            {
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
            if (string.IsNullOrEmpty(settings.AccessToken) && !string.IsNullOrEmpty(settings.LegacyToken))
            {
                settings.AccessToken = settings.LegacyToken;
                await SaveTokenSecurelyAsync(settings.AccessToken);
                settings.LegacyToken = null;
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
        // Create a copy without the token for serialization
        var toSave = new AppSettingsFile
        {
            SchemaVersion = CurrentSchemaVersion,
            DownloadPath = settings.DownloadPath,
            PreferredQuality = settings.PreferredQuality,
            DownloadCaptions = settings.DownloadCaptions,
            CaptionLanguage = settings.CaptionLanguage,
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads,
            SkipExistingFiles = settings.SkipExistingFiles,
            EulaAcceptedVersion = settings.EulaAcceptedVersion,
        };

        var json = JsonSerializer.Serialize(toSave, IndentedJsonOpts);
        await SafeFileWriter.WriteAllTextAtomicAsync(FilePath, json);
    }

    /// <summary>
    /// Validate that the configured download path exists and is writable.
    /// Creates the directory if it doesn't exist.
    /// Delegates to InputValidator for comprehensive validation.
    /// </summary>
    public static (bool Valid, string? Error) ValidateDownloadPath(string path)
        => InputValidator.ValidateDownloadPath(path);

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
    /// Delete all user data — settings, history, cache, tokens.
    /// </summary>
    public static async Task ResetAllDataAsync()
    {
        try { SecureStorage.Default.RemoveAll(); } catch { /* best effort */ }

        var filesToDelete = new[]
        {
            FilePath,
            FilePath + ".bak",
            Path.Combine(FileSystem.AppDataDirectory, "download_history.json"),
            Path.Combine(FileSystem.AppDataDirectory, "download_history.json.bak"),
        };

        foreach (var file in filesToDelete)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { /* best effort */ }
        }

        var dirsToDelete = new[]
        {
            Path.Combine(FileSystem.AppDataDirectory, "cache"),
            AppConstants.LogDirectory,
        };

        foreach (var dir in dirsToDelete)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }

        Log.Information("All user data has been reset");
    }

    /// <summary>
    /// Internal DTO for JSON serialization (excludes sensitive token).
    /// Includes schema version for safe migration.
    /// </summary>
    private sealed class AppSettingsFile
    {
        public int SchemaVersion { get; set; } = 1;
        public string DownloadPath { get; set; } = string.Empty;
        public string PreferredQuality { get; set; } = "1080";
        public bool DownloadCaptions { get; set; } = true;
        public string CaptionLanguage { get; set; } = "en";
        public int MaxConcurrentDownloads { get; set; } = AppConstants.DefaultConcurrentDownloads;
        public bool SkipExistingFiles { get; set; } = true;
        public string? EulaAcceptedVersion { get; set; }
    }
}
