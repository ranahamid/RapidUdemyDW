using Microsoft.Extensions.Logging;
using RapidUdemyDW.Services;
using Serilog;
using Serilog.Events;

namespace RapidUdemyDW
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // ── Production-grade logging via Serilog ──────────────────
            // Logs to rolling files that survive app restarts.
            // Retained for 7 days, max 10 MB per file.
            Directory.CreateDirectory(AppConstants.LogDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
#if DEBUG
                .MinimumLevel.Debug()
#endif
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.WithProperty("App", AppConstants.AppName)
                .Enrich.WithProperty("Version", AppConstants.Version)
                .WriteTo.File(
                    path: Path.Combine(AppConstants.LogDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Starting {App} v{Version}", AppConstants.AppName, AppConstants.Version);

            // ── Global exception handlers ─────────────────────────────
            // Catch any unhandled exception so the app stays alive.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception,
                    "AppDomain.UnhandledException (terminating={IsTerminating})",
                    e.IsTerminating);
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Error(e.Exception, "TaskScheduler.UnobservedTaskException");
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

            // ── Serilog → ILogger integration ─────────────────────────
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: true);

            // Register Udemy services
            // Use HttpClientHandler with UseCookies=false so we can send our own Cookie header.
            // Wrap with HttpRetryHandler for transient failure resilience.
            builder.Services.AddSingleton<UdemyApiService>(sp =>
            {
                var innerHandler = new HttpClientHandler { UseCookies = false };
                var retryHandler = new HttpRetryHandler { InnerHandler = innerHandler };
                var http = new HttpClient(retryHandler) { Timeout = TimeSpan.FromSeconds(60) };
                return new UdemyApiService(http);
            });
            builder.Services.AddSingleton<DownloadHistoryService>();
            builder.Services.AddSingleton<CourseCacheService>();
            builder.Services.AddSingleton<DownloadManager>(sp =>
            {
                var api = sp.GetRequiredService<UdemyApiService>();
                var history = sp.GetRequiredService<DownloadHistoryService>();
                var downloadHandler = new SocketsHttpHandler
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
                // Wrap with retry handler for download resilience
                var retryHandler = new HttpRetryHandler { InnerHandler = downloadHandler };
                var http = new HttpClient(retryHandler) { Timeout = TimeSpan.FromHours(2) };
                // Must use the same mobile UA to bypass Cloudflare for HLS m3u8 fetches
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AppConstants.MobileUserAgent);
                http.DefaultRequestHeaders.Add("Accept", "*/*");
                // Explicitly request only gzip/deflate so CDNs won't send Brotli
                http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
                var dm = new DownloadManager(api, http);
                dm.SetHistoryService(history);

                // Apply saved MaxConcurrentDownloads from user settings
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var settings = await SettingsHelper.LoadAsync();
                        dm.SetMaxConcurrentDownloads(settings.MaxConcurrentDownloads);

                        if (!string.IsNullOrWhiteSpace(settings.AccessToken))
                        {
                            api.SetAccessToken(settings.AccessToken);
                            dm.SetAccessToken(settings.AccessToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to apply saved settings on startup");
                    }
                });

                return dm;
            });
            builder.Services.AddSingleton<CourseCacheService>();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

            return builder.Build();
        }
    }
}
