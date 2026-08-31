# ⚡ RapidUdemy DW

A fast, fully-featured Udemy course downloader with a modern GUI built on **.NET MAUI Blazor Hybrid**.

![.NET 10](https://img.shields.io/badge/.NET-10-blue)
![MAUI Blazor](https://img.shields.io/badge/MAUI-Blazor%20Hybrid-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-green)

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
| 🔑 **Token Validation** | Verify your access token before downloading |

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
cd RapidUdemyDW
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

Or open `RapidUdemyDW.slnx` in Visual Studio 2022+ and press **F5**.

---

## 🔑 Getting Your Access Token

1. Open [udemy.com](https://www.udemy.com) in your browser and log in
2. Open **Developer Tools** (`F12`) → **Application** tab → **Cookies** → `www.udemy.com`
3. Find the cookie named `access_token`
4. Copy its full value (looks like `AbCdEf123...:XyZ456...`)
5. Paste it into the app's **Settings** page

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
