using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RapidUdemyDW.Models;
using Serilog;

namespace RapidUdemyDW.Services;

public class UdemyApiService
{
    private static string BaseUrl => AppConstants.UdemyBaseUrl;
    private readonly HttpClient _http;
    private string _accessToken = string.Empty;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public UdemyApiService(HttpClient httpClient)
    {
        _http = httpClient;
        // Use the Udemy mobile app User-Agent to bypass Cloudflare bot protection.
        // A browser-style UA triggers Cloudflare challenges (403).
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AppConstants.MobileUserAgent);
        _http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
    }

    public void SetAccessToken(string token)
    {
        _accessToken = token;

        // Remove any previous auth headers
        _http.DefaultRequestHeaders.Authorization = null;
        _http.DefaultRequestHeaders.Remove("Cookie");

        // Use Bearer token auth (works with mobile UA) + cookie fallback
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public bool HasToken => !string.IsNullOrWhiteSpace(_accessToken);

    /// <summary>
    /// Fetch subscribed courses page-by-page, calling onPageLoaded after each page
    /// so the UI can update progressively.
    /// </summary>
    public async Task<int> GetMyCoursesAsync(
        List<UdemyCourse> target,
        Func<int, int, Task>? onPageLoaded = null,
        int pageSize = 100,
        CancellationToken ct = default)
    {
        var page = 1;
        int totalCount = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var url = $"{BaseUrl}/users/me/subscribed-courses/" +
                      $"?page={page}&page_size={pageSize}" +
                      "&fields[course]=id,title,url,image_240x135,image_480x270," +
                      "completion_ratio,num_published_lectures,visible_instructors" +
                      "&fields[user]=title,display_name&ordering=-last_accessed";

            var resp = await _http.GetAsync(url, ct);
            EnsureAuthenticated(resp);
            resp.EnsureSuccessStatusCode();

            var data = await resp.Content.ReadFromJsonAsync<UdemyCourseListResponse>(JsonOpts, ct);

            if (data != null)
                totalCount = data.Count;

            if (data?.Results is { Count: > 0 })
                target.AddRange(data.Results);

            if (onPageLoaded != null)
                await onPageLoaded(target.Count, totalCount);

            if (string.IsNullOrEmpty(data?.Next))
                break;
            page++;
        }
        return totalCount;
    }

    /// <summary>
    /// Resolve a Udemy course URL or slug to a course object.
    /// Accepts: full URL, slug like "the-python-mega-course", or numeric ID.
    /// </summary>
    public async Task<UdemyCourse?> GetCourseByUrlAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var slug = ExtractCourseSlug(input);
            if (string.IsNullOrEmpty(slug))
                return null;

            // If it's a numeric ID, use the ID endpoint
            if (long.TryParse(slug, out var numericId))
                return await GetCourseInfoAsync(numericId, ct);

            // Use the slug-based endpoint
            var url = $"{BaseUrl}/courses/{slug}/" +
                      "?fields[course]=id,title,url,image_240x135,image_480x270," +
                      "completion_ratio,num_published_lectures,visible_instructors" +
                      "&fields[user]=title,display_name";

            var resp = await _http.GetAsync(url, ct);
            EnsureAuthenticated(resp);
            if (!resp.IsSuccessStatusCode)
                return null;

            return await resp.Content.ReadFromJsonAsync<UdemyCourse>(JsonOpts, ct);
        }
        catch (AuthenticationExpiredException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract the course slug from a Udemy URL or return the input if it's already a slug/ID.
    /// </summary>
    private static string? ExtractCourseSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        // Try parsing as a URL
        if (input.Contains("udemy.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    input = "https://" + input;

                var uri = new Uri(input);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // Pattern: /course/<slug>/...
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if (segments[i].Equals("course", StringComparison.OrdinalIgnoreCase))
                        return segments[i + 1];
                }
            }
            catch { }
        }

        // Treat as raw slug or numeric ID
        return input.Trim('/');
    }

    /// <summary>
    /// Fetch a single course's basic info by ID.
    /// </summary>
    public async Task<UdemyCourse?> GetCourseInfoAsync(long courseId, CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/courses/{courseId}/" +
                      "?fields[course]=id,title,url,image_240x135,image_480x270," +
                      "completion_ratio,num_published_lectures,visible_instructors" +
                      "&fields[user]=title,display_name";

            var resp = await _http.GetAsync(url, ct);
            EnsureAuthenticated(resp);
            if (!resp.IsSuccessStatusCode)
                return null;

            return await resp.Content.ReadFromJsonAsync<UdemyCourse>(JsonOpts, ct);
        }
        catch (AuthenticationExpiredException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetch the full curriculum (chapters + lectures) for a course.
    /// </summary>
    public async Task<List<UdemyCurriculumItem>> GetCurriculumAsync(long courseId, int pageSize = 200, CancellationToken ct = default)
    {
        var all = new List<UdemyCurriculumItem>();
        var page = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var url = $"{BaseUrl}/courses/{courseId}/cached-subscriber-curriculum-items/" +
                      $"?page={page}&page_size={pageSize}" +
                      "&fields[chapter]=id,title,sort_order,is_published" +
                      "&fields[lecture]=id,title,sort_order,is_published,asset" +
                      "&fields[asset]=id,asset_type,title,filename,time_estimation";

            var resp = await _http.GetAsync(url, ct);
            EnsureAuthenticated(resp);
            resp.EnsureSuccessStatusCode();

            var data = await resp.Content.ReadFromJsonAsync<UdemyCurriculumResponse>(JsonOpts, ct);

            if (data?.Results is { Count: > 0 })
                all.AddRange(data.Results);

            if (string.IsNullOrEmpty(data?.Next))
                break;
            page++;
        }
        return all;
    }

    /// <summary>
    /// Fetch detailed lecture data including stream URLs & captions.
    /// </summary>
    public async Task<UdemyLectureResponse?> GetLectureAsync(long courseId, long lectureId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/users/me/subscribed-courses/{courseId}/lectures/{lectureId}/" +
                  "?fields[lecture]=id,asset" +
                  "&fields[asset]=id,asset_type,title,filename,media_sources,captions," +
                  "download_urls,time_estimation,body,media_license_token" +
                  "&q=0.5";

        var resp = await _http.GetAsync(url, ct);
        EnsureAuthenticated(resp);

        if (!resp.IsSuccessStatusCode)
            return null;

        return await resp.Content.ReadFromJsonAsync<UdemyLectureResponse>(JsonOpts, ct);
    }

    /// <summary>
    /// Validate the access token by fetching current user info.
    /// </summary>
    public async Task<(bool Valid, string? DisplayName)> ValidateTokenAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/users/me/?fields[user]=title,display_name";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return (false, null);

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var name = json.TryGetProperty("display_name", out var dn)
                ? dn.GetString()
                : json.TryGetProperty("title", out var t) ? t.GetString() : "User";
            return (true, name);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Debug(ex, "Token validation failed");
            return (false, null);
        }
    }

    /// <summary>
    /// Throw <see cref="AuthenticationExpiredException"/> on 401/403 responses.
    /// </summary>
    private static void EnsureAuthenticated(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new AuthenticationExpiredException();
    }
}
