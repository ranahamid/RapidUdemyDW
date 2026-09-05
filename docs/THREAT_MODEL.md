# Threat Model

## Application: RapidUdemy Downloader

### 1. System Overview

RapidUdemy Downloader is a .NET MAUI Blazor Hybrid desktop application that:
- Authenticates with Udemy using a user-provided access token
- Fetches course metadata and lecture content via Udemy's API
- Downloads video, article, caption, and file assets to local disk
- Persists settings, download history, and cache locally

### 2. Trust Boundaries

| Boundary | Description |
|----------|-------------|
| User ↔ Application | User provides access token; app displays content |
| Application ↔ Udemy API | HTTPS API calls with Bearer token |
| Application ↔ CDN | HTTPS downloads of media content |
| Application ↔ Local Filesystem | Read/write settings, history, cache, downloaded files |
| Application ↔ Secure Storage | Platform credential manager (DPAPI/Keychain) |

### 3. Assets

| Asset | Sensitivity | Protection |
|-------|-------------|------------|
| Access token | High | Stored in platform secure storage; never logged |
| Downloaded content | Medium (copyrighted) | Stored as regular files; user responsibility |
| Download history | Low | Local JSON with atomic writes |
| Settings | Low | Local JSON (token excluded) |
| Log files | Low | Redacted; 7-day retention |

### 4. Threats and Mitigations

#### T1: Token Theft from Logs
- **Threat**: Access token appears in log files or error messages
- **Likelihood**: Medium (was present in early versions)
- **Impact**: High (account compromise)
- **Mitigation**: ✅ All logging redacts Bearer tokens, Cookie headers, signed URL parameters
- **Residual Risk**: Low

#### T2: Token Theft from Storage
- **Threat**: Attacker reads token from disk
- **Likelihood**: Low (requires local access)
- **Impact**: High
- **Mitigation**: ✅ Token stored in Windows Credential Manager (DPAPI-protected). Other processes running as the same user can still access it.
- **Residual Risk**: Medium — same-user processes can access DPAPI. Consider encrypting at app level in future.

#### T3: Path Traversal
- **Threat**: Malicious course/lecture titles containing `../` write files outside download directory
- **Likelihood**: Low (requires malicious API response)
- **Impact**: High (arbitrary file write)
- **Mitigation**: ✅ SanitizeFileName removes `..`, `/`, `\` sequences. ValidatePath checks resolved path is within expected base.
- **Residual Risk**: Low

#### T4: Disk Exhaustion
- **Threat**: Large downloads fill disk, corrupting other applications
- **Likelihood**: Medium
- **Impact**: Medium
- **Mitigation**: ✅ Disk space check before download. Atomic writes prevent partial corruption of settings/history.
- **Residual Risk**: Low

#### T5: Man-in-the-Middle
- **Threat**: Attacker intercepts HTTPS traffic to steal tokens or inject malicious content
- **Likelihood**: Low (requires network position)
- **Impact**: High
- **Mitigation**: TLS via .NET runtime defaults. No certificate pinning.
- **Residual Risk**: Medium — certificate pinning not implemented

#### T6: Corrupted Persistence Files
- **Threat**: Power loss or crash during file write corrupts settings or history
- **Likelihood**: Medium
- **Impact**: Medium (data loss)
- **Mitigation**: ✅ Atomic writes with backup files; recovery from backup on corrupt read.
- **Residual Risk**: Low

#### T7: API Abuse / Rate Limiting
- **Threat**: Excessive API calls trigger rate limiting or account suspension
- **Likelihood**: Medium
- **Impact**: Medium
- **Mitigation**: ✅ Retry handler respects Retry-After headers. Concurrent download limits. Course caching reduces API calls.
- **Residual Risk**: Medium — no formal rate limit tracking

#### T8: Unsafe Downloaded Content
- **Threat**: Downloaded files contain executable content or malware
- **Likelihood**: Low (content comes from Udemy)
- **Impact**: Medium
- **Mitigation**: Files are saved with their original extensions. No automatic execution of downloaded content. File extension validation for allowed types.
- **Residual Risk**: Low

### 5. Items Requiring Legal Review

| Item | Risk | Recommendation |
|------|------|----------------|
| Use of Udemy's internal API (api-2.0) | High | Not a public API. Legal review required before commercial distribution. |
| Mobile User-Agent spoofing | High | Impersonates Udemy mobile app. May violate ToS. |
| Bulk content downloading | Medium | Terms of service may restrict automated downloading. |
| Content redistribution | High | Downloaded content is copyrighted. App includes disclaimers but legal review needed. |

### 6. Recommendations for Future Versions

1. **Certificate pinning** for Udemy API domain
2. **App-level encryption** for sensitive data beyond DPAPI
3. **Rate limiter** with explicit request budget tracking
4. **Content integrity verification** (checksum validation for downloads)
5. **Official API integration** if/when Udemy provides a public download API
