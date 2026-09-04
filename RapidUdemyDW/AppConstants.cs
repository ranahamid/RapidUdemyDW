using System.Reflection;

namespace RapidUdemyDW;

/// <summary>
/// Centralized app constants — branding, versioning, shared configuration.
/// Single source of truth for values used across the app.
/// </summary>
public static class AppConstants
{
    // ── Branding ────────────────────────────────────────────────
    public const string AppName = "RapidUdemy Downloader";
    public const string AppId = "com.rapidudemy.downloader";
    public const string PublisherName = "RapidUdemy";

    // ── Versioning (read from assembly at runtime) ──────────────
    public static string Version =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+')[0]  // Strip build metadata
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";

    public static string FullVersion =>
        $"{AppName} v{Version}";

    // ── HTTP ────────────────────────────────────────────────────
    public const string MobileUserAgent = "UdemyAndroid 5.5.1/515009";
    public const string UdemyBaseUrl = "https://www.udemy.com/api-2.0";

    // ── Download defaults ───────────────────────────────────────
    public const int DefaultConcurrentDownloads = 3;
    public const int MaxConcurrentDownloadsCap = 10;
    public const int HlsSegmentBatchSize = 8;
    public const int HlsSegmentMaxRetries = 3;

    // ── File paths ──────────────────────────────────────────────
    public static string DefaultDownloadPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "UdemyCourses");

    public static string LogDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "logs");

    // ── EULA / Legal ────────────────────────────────────────────
    public const string EulaVersion = "1.0";

    // ── Developer Info ──────────────────────────────────────────
    public const string DeveloperName = "Rana Hamid";
    public const string DeveloperTitle = "Lead .NET Developer | Dynamics 365 Specialist";
    public const string DeveloperLocation = "Dhaka, Bangladesh";
    public const string DeveloperCompany = "MyMedicalHUB";
    public const string DeveloperEducation = "Bangladesh University of Engineering and Technology (BUET)";
    public const string DeveloperLinkedIn = "https://www.linkedin.com/in/ranahamid007";
    public const string DeveloperLeetCode = "https://leetcode.com/ranahamid";

    // Awards / Certifications (for About page)
    public static readonly string[] DeveloperAwards =
    [
        "🥇 Prime Minister's Gold Medal (2014)",
        "🏅 Agrani Bank University Gold Medal — Faculty First (Rank #1)",
        "📜 ITEE-FE Certified (JICA)",
    ];
}
