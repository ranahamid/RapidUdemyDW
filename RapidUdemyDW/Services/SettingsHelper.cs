using System.Text.Json;
using RapidUdemyDW.Models;

namespace RapidUdemyDW.Services;

public static class SettingsHelper
{
    private static string FilePath =>
        Path.Combine(FileSystem.AppDataDirectory, "udemy_dl_settings.json");

    public static async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = await File.ReadAllTextAsync(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefault();
            }
        }
        catch { }
        return CreateDefault();
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(FilePath, json);
    }

    private static AppSettings CreateDefault() => new()
    {
        // DownloadPath = Path.Combine( Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "UdemyCourses"),
        DownloadPath = "D:\\Udeler",
        PreferredQuality = "1080",
        DownloadCaptions = true,
        CaptionLanguage = "en",
        MaxConcurrentDownloads = 3,
        SkipExistingFiles = true
    };
}
