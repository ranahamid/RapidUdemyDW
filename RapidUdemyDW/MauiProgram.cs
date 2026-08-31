using Microsoft.Extensions.Logging;
using RapidUdemyDW.Services;

namespace RapidUdemyDW
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
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
                var handler = new HttpClientHandler { UseCookies = false };
                var http = new HttpClient(handler) { Timeout = TimeSpan.FromHours(2) };
                // Must use the same mobile UA to bypass Cloudflare for HLS m3u8 fetches
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "UdemyAndroid 5.5.1/515009");
                http.DefaultRequestHeaders.Add("Accept", "*/*");
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
