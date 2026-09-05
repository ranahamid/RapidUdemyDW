# ⚡ RapidUdemy Downloader

A .NET MAUI Blazor Hybrid application for downloading Udemy course content for offline viewing.

![.NET 10](https://img.shields.io/badge/.NET-10-blue)
![MAUI Blazor](https://img.shields.io/badge/MAUI-Blazor%20Hybrid-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-green)

---

## ⚠️ Important Legal Notice

> **This application requires your own Udemy access token. You are solely responsible for ensuring your use complies with [Udemy's Terms of Service](https://www.udemy.com/terms/) and all applicable copyright laws.**
>
> This application is for personal, offline access to content you have legitimately purchased. Do **not** redistribute, share, or sell downloaded content.
>
> **API Compliance**: This application uses Udemy's internal API, which is not a public API. Legal review is recommended before commercial distribution. See [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md).

---

## Supported Platforms

| Platform | Minimum Version | Status |
|----------|----------------|--------|
| Windows 10 | Build 17763 (1809) | ✅ Primary target |
| Windows 11 | All versions | ✅ Supported |
| Android | API 24 (7.0) | 🔧 Experimental |
| iOS / macOS | 15.0 | 🔧 Experimental |

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 📚 **Course Browser** | Browse all your enrolled Udemy courses with thumbnails, instructor names, and completion progress |
| 🔗 **Direct Link Paste** | Paste any Udemy course URL or slug to jump straight to downloading |
| 🔍 **Search** | Instant search/filter across all your courses |
| 📋 **Chapter & Lecture Selection** | Expand chapters, select/deselect individual lectures before downloading |
| 🎬 **Video Downloads** | Downloads MP4 videos with preferred quality (1080p / 720p / 480p / 360p) |
| 📡 **HLS Stream Support** | Downloads HLS-only courses by fetching and merging all stream segments |
| 📄 **Article Saving** | Saves article-type lectures as clean HTML files |
| 📎 **File & E-Book Downloads** | Downloads supplementary files and e-books |
| 💬 **Subtitles** | Downloads subtitles/captions in 11 languages (SRT format) |
| ⏸️ **Pause / Resume / Cancel** | Full control over active downloads |
| 📊 **Real-time Progress** | Per-file and overall progress bars with download speed |
| 📥 **Download History** | Persistent history of all past downloads, grouped by session — survives app restarts |
| 💾 **Course Caching** | Course list cached locally for instant startup (auto-refreshes in background) |
| ⚙️ **Configurable Settings** | Download path, quality, concurrent downloads, captions, skip-existing |
| 🔑 **Secure Token Storage** | Access token stored in Windows Credential Manager |
| 🔄 **Retry & Resilience** | Automatic retry with exponential backoff for transient failures |
| 🛡️ **No Telemetry** | Zero analytics, zero tracking, all data stays on your device |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) with MAUI workload
- Windows 10 (build 17763+) or later

### Install MAUI workload

```powershell
dotnet workload install maui
```

### Build & Run

```powershell
# Debug build
dotnet build RapidUdemyDW/RapidUdemyDW.csproj -f net10.0-windows10.0.19041.0

# Release build
dotnet build RapidUdemyDW/RapidUdemyDW.csproj -c Release -f net10.0-windows10.0.19041.0

# Run tests
dotnet test RapidUdemyDW.Tests/RapidUdemyDW.Tests.csproj
```

Or open `RapidUdemyDW.slnx` in Visual Studio 2022+ and press **F5**.

---

## 🔑 Authentication

1. Open [udemy.com](https://www.udemy.com) in your browser and log in
2. Open **Developer Tools** (`F12`) → **Application** tab → **Cookies** → `www.udemy.com`
3. Find the cookie named `access_token`
4. Copy its full value
5. Paste it into the app's **Settings** page and click **Validate Token**

Your access token is stored securely in Windows Credential Manager and is never written to plaintext files or logs.

---

## 🔒 Privacy & Data

This application does **not** collect, transmit, or share any data. See [PRIVACY.md](PRIVACY.md).

**Reset**: Settings → Privacy & Data → Reset All Data

---

## 🔧 Troubleshooting

| Issue | Solution |
|-------|----------|
| "Invalid access token" | Re-copy the token from browser cookies |
| "Authentication expired" | Get a fresh token from udemy.com |
| "Disk full" | Free disk space and retry |
| Corrupted settings | Use "Reset All Data" in Settings |

---

## ⚖️ Legal

- [Privacy Notice](PRIVACY.md) · [Security Policy](SECURITY.md) · [Support](SUPPORT.md) · [Changelog](CHANGELOG.md)
- [Threat Model](docs/THREAT_MODEL.md) · [Release Checklist](docs/RELEASE_CHECKLIST.md)

© 2026 RapidUdemy. All rights reserved.

---

## 📁 Project Structure

```
RapidUdemyDW/
├── Models/
│   └── UdemyModels.cs           # All data models (courses, chapters, lectures, history, settings)
├── Services/
│   ├── UdemyApiService.cs       # Udemy API client (courses, curriculum, streams)
│   ├── DownloadManager.cs       # Concurrent download engine with pause/resume
│   ├── HlsDownloader.cs         # HLS (m3u8) stream segment downloader
│   ├── CourseCacheService.cs    # Local course list caching
│   ├── DownloadHistoryService.cs# Persistent download history
│   └── SettingsHelper.cs        # App settings persistence
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor     # App layout shell
│   │   └── NavMenu.razor        # Sidebar navigation
│   └── Pages/
│       ├── Home.razor           # Course browser + search + direct link
│       ├── CourseDetails.razor   # Chapter/lecture selector + download progress
│       ├── Downloads.razor       # Active downloads + history tabs
│       └── Settings.razor        # Token, quality, path configuration
├── wwwroot/
│   └── app.css                  # Custom styling (Udemy purple theme)
└── MauiProgram.cs               # DI registration & app bootstrap
```

---

## ⚙️ Configuration

All settings are saved to local app data and persist across restarts:

| Setting | Default | Description |
|---------|---------|-------------|
| Download Path | `~/Downloads/UdemyCourses` | Where course files are saved |
| Video Quality | 1080p | Preferred resolution (1080/720/480/360) |
| Concurrent Downloads | 3 | Parallel download threads (1–8) |
| Download Captions | ✅ Enabled | Download subtitle files alongside videos |
| Caption Language | English | Preferred subtitle language |
| Skip Existing | ✅ Enabled | Don't re-download files that already exist |

---

## 🏗️ Tech Stack

- **[.NET 10](https://dotnet.microsoft.com/)** — Runtime
- **[MAUI Blazor Hybrid](https://learn.microsoft.com/dotnet/maui/user-interface/pages/blazorwebview)** — Native desktop app with Blazor UI
- **[Bootstrap 5](https://getbootstrap.com/)** — UI components
- **Udemy API v2.0** — Course data & streaming (mobile endpoint)

---

## 📝 Notes

- Uses the Udemy **mobile app User-Agent** (`UdemyAndroid`) to bypass Cloudflare bot protection
- HLS-only videos (no direct MP4 available) are downloaded as `.ts` files (MPEG-TS container) — playable in VLC, MPC-HC, Windows Media Player, and all modern players
- Course cache expires after 6 hours and auto-refreshes in the background
- Download history keeps the last 50 sessions

---

## 📄 License

This project is for **personal/educational use only**. Respect Udemy's Terms of Service and content creators' rights. Only download courses you have legitimately purchased.
