using Microsoft.Extensions.Logging;
using RapidUdemyDW.Services;

namespace RapidUdemyDW
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // ── Global exception handlers ─────────────────────────────
            // Catch any unhandled exception so the app stays alive.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AppDomain.UnhandledException] {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TaskScheduler.UnobservedTaskException] {e.Exception}");
                e.SetObserved(); // Prevent process termination
            };

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Register Udemy services
            // Use HttpClientHandler with UseCookies=false so we can send our own Cookie header
            builder.Services.AddSingleton<UdemyApiService>(sp =>
            {
                var handler = new HttpClientHandler { UseCookies = false };
                var http = new HttpClient(handler);
                return new UdemyApiService(http);
            });
            builder.Services.AddSingleton<DownloadHistoryService>();
            builder.Services.AddSingleton<DownloadManager>(sp =>
            {
                var api = sp.GetRequiredService<UdemyApiService>();
                var history = sp.GetRequiredService<DownloadHistoryService>();
                var handler = new SocketsHttpHandler
                {
                    MaxConnectionsPerServer = 16,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    EnableMultipleHttp2Connections = true,
                    UseCookies = false,
                    // Avoid DecompressionMethods.All — Brotli decoding triggers
                    // ExecutionEngineException in ParseHeadersCore on certain
                    // .NET 10 builds when CDN responses contain unusual headers.
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                           | System.Net.DecompressionMethods.Deflate,
                    ConnectTimeout = TimeSpan.FromSeconds(30),
                    ResponseDrainTimeout = TimeSpan.FromSeconds(5),
                };
                var http = new HttpClient(handler) { Timeout = TimeSpan.FromHours(2) };
                // Must use the same mobile UA to bypass Cloudflare for HLS m3u8 fetches
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "UdemyAndroid 5.5.1/515009");
                http.DefaultRequestHeaders.Add("Accept", "*/*");
                // Explicitly request only gzip/deflate so CDNs won't send Brotli
                http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
                var dm = new DownloadManager(api, http);
                dm.SetHistoryService(history);
                return dm;
            });
            builder.Services.AddSingleton<CourseCacheService>();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
