# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | ✅ Current |

## Security Design

### Credential Storage
- Access tokens are stored exclusively in the platform's secure storage facility:
  - **Windows**: Windows Credential Manager (DPAPI-protected)
  - **Android**: Android Keystore
  - **iOS/macOS**: Keychain
- Tokens are **never** written to plaintext files, logs, or transmitted to third parties.
- Legacy plaintext tokens from older versions are automatically migrated to secure storage and the plaintext copy is removed.

### Logging
- All log output is redacted to remove:
  - Bearer tokens and authorization headers
  - Signed URL parameters (Signature, X-Amz-Credential, etc.)
  - Cookie values
  - Access token query parameters
- Log files are stored locally in the app's data directory with a 7-day rolling retention policy and 10 MB size cap.

### Network Security
- All API communication uses HTTPS exclusively.
- TLS certificate validation is handled by the .NET runtime defaults (no certificate pinning bypasses).
- HTTP responses with authentication errors (401/403) trigger user-facing re-authentication prompts rather than silent retries.

### Input Validation
- File names are sanitized to prevent path traversal attacks (`..`, `/`, `\` sequences removed).
- Windows reserved device names (CON, PRN, NUL, COM1–9, LPT1–9) are blocked.
- Download paths are validated to be absolute, writable, and outside system directories.
- URL inputs are validated for HTTP/HTTPS scheme.

### Data Integrity
- Settings and download history use atomic write operations (write-to-temp-then-rename).
- Backup files are maintained for automatic recovery from corruption.

## Reporting a Vulnerability

If you discover a security vulnerability, please report it responsibly:

1. **Do not** open a public GitHub issue for security vulnerabilities.
2. Email: **[TODO: Add security contact email]**
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

We aim to acknowledge reports within 48 hours and provide a fix within 7 days for critical issues.

## Known Limitations

- The application does not implement certificate pinning. Man-in-the-middle attacks on the network level are mitigated by TLS but not fully prevented.
- The Windows Credential Manager is accessible to any process running under the same user account.
- Downloaded content files are not encrypted at rest on disk.

## Threat Model

See [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) for the full threat model document.
