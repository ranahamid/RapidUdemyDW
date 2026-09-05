# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-04

### Added
- **Security**
  - Path traversal prevention in filename sanitization
  - Windows reserved device name blocking (CON, PRN, NUL, etc.)
  - Secret redaction in all log output and error messages
  - Atomic file writes with backup recovery for settings and history
  - Disk space validation before downloads
  - Input validation for URLs, file paths, and download directories
  - Authentication expiration detection and user-friendly prompts
  - Data deletion/reset functionality in Settings

- **Reliability**
  - HTTP retry handler with exponential backoff and jitter for transient failures
  - Retry-After header support (HTTP 429 rate limiting)
  - Cancellation token propagation through all network operations
  - Disk-full error handling with job cancellation
  - Settings schema versioning for safe migration
  - Corrupted file recovery from backup
  - 90-day retention policy for download history

- **Documentation**
  - SECURITY.md — security policy and vulnerability reporting
  - PRIVACY.md — privacy notice (no telemetry, local-only data)
  - SUPPORT.md — support channels and bug reporting guidelines
  - CHANGELOG.md — this file
  - Threat model document
  - Production release checklist
  - CI/CD workflow with build, test, and package stages
  - Updated README with legal, privacy, and platform requirements

- **Testing**
  - Unit test project (RapidUdemyDW.Tests)
  - Tests for filename sanitization and path traversal prevention
  - Tests for URL validation
  - Tests for secret redaction
  - Tests for settings serialization and migration
  - Tests for download state transitions
  - Tests for retry and cancellation behavior
  - Tests for history persistence and recovery
  - Tests for disk space checking

- **Build & CI**
  - GitHub Actions CI workflow
  - Code analysis enabled (latest-recommended)
  - TreatWarningsAsErrors in Release configuration
  - Release build configuration

### Changed
- Settings file writes now use atomic temp-file-and-rename pattern
- Download history writes now use atomic pattern with backup
- All API calls now accept and propagate CancellationToken
- Error messages shown to users are redacted to remove tokens/URLs
- EULA dialog now includes privacy and acceptable-use terms

### Fixed
- Path traversal vulnerability in SanitizeFileName
- Potential token leak in error messages and log output
- Race condition in download history concurrent saves
- Missing disk-full error handling during downloads
- Silent swallowing of authentication failures
