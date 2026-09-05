# Production Release Checklist

## Pre-Release

### Legal Review
- [ ] Udemy API usage reviewed by legal counsel
- [ ] Mobile User-Agent spoofing policy reviewed
- [ ] Content redistribution terms reviewed
- [ ] EULA text finalized and approved
- [ ] Privacy notice finalized and approved
- [ ] Open-source license compliance verified for all dependencies
- [ ] Copyright notices correct and complete

### Code Quality
- [ ] All unit tests pass (`dotnet test`)
- [ ] Release build succeeds with zero warnings (`dotnet build -c Release -warnaserror`)
- [ ] No secrets, tokens, or credentials in source code
- [ ] Static analysis warnings reviewed and resolved
- [ ] Dependency audit — no known critical vulnerabilities

### Security
- [ ] Token storage verified (SecureStorage only — never in logs/plaintext)
- [ ] Log output manually reviewed for token/PII leaks
- [ ] Path traversal test: course title with `../../etc/passwd` handled safely
- [ ] Disk-full scenario tested
- [ ] Auth expiration scenario tested (expired/revoked token)
- [ ] All error messages reviewed — no internal details exposed to user

### Functionality
- [ ] Fresh install onboarding flow tested (EULA → Settings → Courses)
- [ ] Token validation works
- [ ] Course loading and caching works
- [ ] Chapter/lecture selection works
- [ ] Video download (MP4 and HLS) works
- [ ] Article saving works
- [ ] Caption download works
- [ ] Pause/resume/cancel works
- [ ] Download history persists across restarts
- [ ] Settings persist across restarts
- [ ] Data reset clears all user data
- [ ] Settings migration from previous version works

### Platform Testing
- [ ] Windows 10 (build 17763+) tested
- [ ] Windows 11 tested
- [ ] Different screen sizes / DPI tested

## Build & Package

### Windows
```powershell
# Release build
dotnet build RapidUdemyDW/RapidUdemyDW.csproj -c Release -f net10.0-windows10.0.19041.0

# Run tests
dotnet test RapidUdemyDW.Tests/RapidUdemyDW.Tests.csproj

# Package as MSIX (requires certificate)
# TODO: Configure MSIX packaging when publisher certificate is available
# dotnet publish RapidUdemyDW/RapidUdemyDW.csproj -c Release -f net10.0-windows10.0.19041.0
```

### Signing
- [ ] Code signing certificate obtained from trusted CA
- [ ] Certificate stored securely (Azure Key Vault or similar — NOT in repo)
- [ ] MSIX package signed
- [ ] Signature verified with `signtool verify`

## Post-Release

- [ ] GitHub Release created with changelog
- [ ] Download links verified
- [ ] Version numbers consistent (csproj, Package.appxmanifest, AppConstants)
- [ ] CHANGELOG.md updated
- [ ] README.md installation instructions verified
- [ ] SECURITY.md contact information populated
- [ ] PRIVACY.md contact information populated
- [ ] SUPPORT.md links populated
