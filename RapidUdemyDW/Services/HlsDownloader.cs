using System.Text.RegularExpressions;
using RapidUdemyDW.Models;

namespace RapidUdemyDW.Services;

/// <summary>
/// Downloads HLS (m3u8) streams by fetching all .ts segments and concatenating
/// them into a single .ts file (MPEG-TS container, playable by all players).
/// </summary>
public static partial class HlsDownloader
{
    /// <summary>
    /// Download an HLS stream to a local file.
    /// </summary>
    public static async Task DownloadAsync(
        HttpClient http,
        string masterPlaylistUrl,
        string outputFilePath,
        string preferredQuality,
        DownloadTask task,
        Func<bool> isPausedFunc,
        Action? onProgress,
        CancellationToken ct)
    {
        // 1. Fetch master playlist
        var masterContent = await http.GetStringAsync(masterPlaylistUrl, ct);
        var variantUrl = SelectBestVariant(masterContent, masterPlaylistUrl, preferredQuality);

        if (string.IsNullOrEmpty(variantUrl))
        {
            task.Status = DownloadStatus.Skipped;
            task.ErrorMessage = "No suitable HLS variant found";
            return;
        }

        // 2. Fetch variant playlist (contains segment URLs)
        var variantContent = await http.GetStringAsync(variantUrl, ct);
        var segmentUrls = ParseSegmentUrls(variantContent, variantUrl);

        if (segmentUrls.Count == 0)
        {
            task.Status = DownloadStatus.Skipped;
            task.ErrorMessage = "No segments found in HLS playlist";
            return;
        }

        // 3. Download all segments and concatenate into output file
        var dir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        task.TotalBytes = segmentUrls.Count; // Use segment count as total for progress

        await using var outStream = new FileStream(
            outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        long totalDownloaded = 0;
        for (int i = 0; i < segmentUrls.Count; i++)
        {
            // Pause support
            while (isPausedFunc() && !ct.IsCancellationRequested)
                await Task.Delay(300, ct);
            ct.ThrowIfCancellationRequested();

            var segData = await http.GetByteArrayAsync(segmentUrls[i], ct);
            await outStream.WriteAsync(segData, ct);

            totalDownloaded += segData.Length;
            task.DownloadedBytes = totalDownloaded;
            task.TotalBytes = (long)(totalDownloaded / ((double)(i + 1) / segmentUrls.Count));
            task.ProgressPercent = (i + 1) * 100.0 / segmentUrls.Count;
            onProgress?.Invoke();
        }

        task.ProgressPercent = 100;
        onProgress?.Invoke();
    }

    /// <summary>
    /// Parse the master m3u8 and pick the best variant matching preferred quality.
    /// </summary>
    private static string? SelectBestVariant(string masterPlaylist, string masterUrl, string preferredQuality)
    {
        var lines = masterPlaylist.Split('\n');
        var variants = new List<(int bandwidth, int height, string url)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("#EXT-X-STREAM-INF"))
                continue;

            // Parse resolution height
            int height = 0;
            var resMatch = ResolutionRegex().Match(line);
            if (resMatch.Success)
                height = int.Parse(resMatch.Groups[2].Value);

            // Parse bandwidth
            int bandwidth = 0;
            var bwMatch = BandwidthRegex().Match(line);
            if (bwMatch.Success)
                bandwidth = int.Parse(bwMatch.Groups[1].Value);

            // Next non-comment line is the URL
            if (i + 1 < lines.Length)
            {
                var url = lines[i + 1].Trim();
                if (!string.IsNullOrEmpty(url) && !url.StartsWith("#"))
                {
                    url = ResolveUrl(url, masterUrl);
                    variants.Add((bandwidth, height, url));
                }
            }
        }

        if (variants.Count == 0)
        {
            // Not a master playlist — might be a media playlist directly
            // Check if it has #EXTINF segments
            if (masterPlaylist.Contains("#EXTINF"))
                return masterUrl; // The URL itself is the media playlist
            return null;
        }

        // Try to match preferred quality
        int preferredHeight = preferredQuality switch
        {
            "1080" => 1080,
            "720" => 720,
            "480" => 480,
            "360" => 360,
            _ => 1080
        };

        // Find exact match or closest lower
        var match = variants
            .OrderByDescending(v => v.height)
            .FirstOrDefault(v => v.height <= preferredHeight);

        if (match == default)
            match = variants.OrderByDescending(v => v.bandwidth).First();

        return match.url;
    }

    /// <summary>
    /// Parse a media m3u8 playlist and extract all segment URLs.
    /// </summary>
    private static List<string> ParseSegmentUrls(string mediaPlaylist, string playlistUrl)
    {
        var segments = new List<string>();
        foreach (var rawLine in mediaPlaylist.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            // This is a segment URL
            segments.Add(ResolveUrl(line, playlistUrl));
        }
        return segments;
    }

    /// <summary>
    /// Resolve a potentially relative URL against a base URL.
    /// </summary>
    private static string ResolveUrl(string url, string baseUrl)
    {
        if (url.StartsWith("http://") || url.StartsWith("https://"))
            return url;

        var baseUri = new Uri(baseUrl);
        return new Uri(baseUri, url).ToString();
    }

    [GeneratedRegex(@"RESOLUTION=(\d+)x(\d+)")]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"BANDWIDTH=(\d+)")]
    private static partial Regex BandwidthRegex();
}
