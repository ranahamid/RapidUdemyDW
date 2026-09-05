using System.Net;
using Serilog;

namespace RapidUdemyDW.Services;

/// <summary>
/// Delegating handler that adds bounded retry with exponential backoff and jitter
/// for transient HTTP failures (5xx, 408, 429, network errors).
/// </summary>
public class HttpRetryHandler : DelegatingHandler
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes =
    [
        HttpStatusCode.RequestTimeout,           // 408
        HttpStatusCode.TooManyRequests,           // 429
        HttpStatusCode.InternalServerError,       // 500
        HttpStatusCode.BadGateway,                // 502
        HttpStatusCode.ServiceUnavailable,        // 503
        HttpStatusCode.GatewayTimeout             // 504
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                // Clone the request for retry (original request content may have been consumed)
                response = await base.SendAsync(request, cancellationToken);

                if (!ShouldRetry(response) || attempt == MaxRetries)
                    return response;

                // Check for Retry-After header
                var retryAfter = GetRetryAfterDelay(response);
                var delay = retryAfter ?? CalculateDelay(attempt);

                Log.Debug("HTTP {StatusCode} — retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    (int)response.StatusCode, delay.TotalMilliseconds, attempt + 1, MaxRetries);

                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxRetries)
            {
                var delay = CalculateDelay(attempt);
                Log.Debug("HTTP request exception — retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay.TotalMilliseconds, attempt + 1, MaxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Respect user cancellation
            }
            catch (TaskCanceledException) when (attempt < MaxRetries)
            {
                // Timeout — retry
                var delay = CalculateDelay(attempt);
                Log.Debug("HTTP timeout — retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay.TotalMilliseconds, attempt + 1, MaxRetries);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // Should not reach here, but return last response as fallback
        return response!;
    }

    private static bool ShouldRetry(HttpResponseMessage response)
    {
        return RetryableStatusCodes.Contains(response.StatusCode);
    }

    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null) return null;

        if (retryAfter.Delta.HasValue)
            return retryAfter.Delta.Value;

        if (retryAfter.Date.HasValue)
        {
            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
        }

        return null;
    }

    /// <summary>
    /// Exponential backoff with jitter: delay = base * 2^attempt + random jitter
    /// </summary>
    private static TimeSpan CalculateDelay(int attempt)
    {
        var exponential = BaseDelay * Math.Pow(2, attempt);
        if (exponential > MaxDelay)
            exponential = MaxDelay;

        // Add jitter (0-50% of the delay) to avoid thundering herd
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(exponential.TotalMilliseconds * 0.5)));
        return exponential + jitter;
    }
}

/// <summary>
/// Exception thrown when authentication has expired or is invalid.
/// UI should prompt the user to re-authenticate.
/// </summary>
public class AuthenticationExpiredException : Exception
{
    public AuthenticationExpiredException()
        : base("Authentication has expired. Please update your access token in Settings.") { }

    public AuthenticationExpiredException(string message) : base(message) { }
    public AuthenticationExpiredException(string message, Exception inner) : base(message, inner) { }
}
