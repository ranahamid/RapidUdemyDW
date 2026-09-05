using System.Text.Json;
using RapidUdemyDW.Models;

namespace RapidUdemyDW.Services;

/// <summary>
/// Caches the course list locally so the UI loads instantly on repeat visits.
/// Courses are refreshed in the background when stale (older than CacheMaxAge).
/// </summary>
public class CourseCacheService
{
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static string CacheDir =>
        Path.Combine(FileSystem.AppDataDirectory, "cache");

    private static string CourseCachePath =>
        Path.Combine(CacheDir, "courses.json");

    private static string CacheMetaPath =>
        Path.Combine(CacheDir, "courses_meta.json");

    /// <summary>
    /// Load courses from local cache. Returns empty list if no cache exists.
    /// </summary>
    public async Task<List<UdemyCourse>> LoadFromCacheAsync()
    {
        try
        {
            if (!File.Exists(CourseCachePath))
                return [];

            var json = await File.ReadAllTextAsync(CourseCachePath);
            return JsonSerializer.Deserialize<List<UdemyCourse>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Save courses to local cache.
    /// </summary>
    public async Task SaveToCacheAsync(List<UdemyCourse> courses)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var json = JsonSerializer.Serialize(courses, JsonOpts);
            await File.WriteAllTextAsync(CourseCachePath, json);

            // Write metadata
            var meta = new CacheMeta { UpdatedAt = DateTime.UtcNow, Count = courses.Count };
            var metaJson = JsonSerializer.Serialize(meta);
            await File.WriteAllTextAsync(CacheMetaPath, metaJson);
        }
        catch { /* non-critical */ }
    }

    /// <summary>
    /// Check if the cache is stale (older than CacheMaxAge) or doesn't exist.
    /// </summary>
    public bool IsCacheStale()
    {
        try
        {
            if (!File.Exists(CacheMetaPath))
                return true;

            var json = File.ReadAllText(CacheMetaPath);
            var meta = JsonSerializer.Deserialize<CacheMeta>(json);
            if (meta == null) return true;

            return (DateTime.UtcNow - meta.UpdatedAt) > CacheMaxAge;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Clear the cache (e.g. on token change).
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (File.Exists(CourseCachePath)) File.Delete(CourseCachePath);
            if (File.Exists(CacheMetaPath)) File.Delete(CacheMetaPath);
        }
        catch { }
    }

    private sealed class CacheMeta
    {
        public DateTime UpdatedAt { get; set; }
        public int Count { get; set; }
    }
}
