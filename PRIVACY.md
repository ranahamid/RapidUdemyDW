# Privacy Notice

**RapidUdemy Downloader** — Privacy Policy  
Last updated: 2026-09-04

## Summary

RapidUdemy Downloader does **not** collect, transmit, or share any personal data or usage analytics. All data remains on your device.

## Data We Store Locally

| Data | Location | Purpose | Retention |
|------|----------|---------|-----------|
| Access token | Platform secure storage (Windows Credential Manager / Keychain) | Authenticate with content provider | Until you remove it or reset the app |
| App settings | `{AppDataDirectory}/udemy_dl_settings.json` | Preferences (download path, quality, etc.) | Until you reset the app |
| Download history | `{AppDataDirectory}/download_history.json` | Track completed/failed downloads | 90 days, max 50 sessions |
| Course cache | `{AppDataDirectory}/cache/` | Faster course list loading | 6 hours (auto-refresh) |
| Application logs | `{AppDataDirectory}/logs/` | Debugging and error diagnosis | 7 days, rolling |

## Data We Do NOT Collect

- ❌ No analytics or telemetry
- ❌ No crash reporting to external services
- ❌ No usage tracking
- ❌ No advertising identifiers
- ❌ No data transmitted to our servers (we have no servers)
- ❌ No email addresses, names, or personal profiles stored

## Network Communication

The application communicates **only** with the content provider's API servers (udemy.com) to:
- Validate your access token
- Retrieve your course list and curriculum data
- Download course content (video, articles, captions, files)

No data is sent to any other server.

## Your Rights

You can at any time:
- **View** your stored data via the About page (storage section)
- **Delete** all local data using the "Reset All Data" feature in Settings
- **Delete** specific download history entries
- **Remove** your access token from Settings
- **Clear** logs from the About page
- **Uninstall** the application, which removes all app data

## Third-Party Services

This application does not integrate with any third-party analytics, advertising, or tracking services.

## Changes to This Policy

Any changes to this privacy policy will be documented in the changelog and reflected in the EULA version, requiring re-acceptance.

## Contact

For privacy-related questions: **[TODO: Add contact email]**
