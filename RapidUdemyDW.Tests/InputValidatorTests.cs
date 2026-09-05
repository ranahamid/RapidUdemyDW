using RapidUdemyDW.Services;

namespace RapidUdemyDW.Tests;

public class InputValidatorTests
{
    // ── SanitizeFileName ─────────────────────────────────────────

    [Fact]
    public void SanitizeFileName_RemovesPathTraversal()
    {
        var result = InputValidator.SanitizeFileName("../../etc/passwd");
        Assert.DoesNotContain("..", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
    }

    [Fact]
    public void SanitizeFileName_RemovesBackslashTraversal()
    {
        var result = InputValidator.SanitizeFileName(@"..\..\windows\system32\config");
        Assert.DoesNotContain("..", result);
        Assert.DoesNotContain("\\", result);
    }

    [Fact]
    public void SanitizeFileName_RemovesForwardSlashes()
    {
        var result = InputValidator.SanitizeFileName("path/to/file");
        Assert.DoesNotContain("/", result);
    }

    [Fact]
    public void SanitizeFileName_BlocksReservedDeviceNames()
    {
        var result = InputValidator.SanitizeFileName("CON");
        Assert.NotEqual("CON", result, StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith("_", result);
    }

    [Fact]
    public void SanitizeFileName_BlocksReservedDeviceNames_CaseInsensitive()
    {
        var result = InputValidator.SanitizeFileName("nul");
        Assert.NotEqual("nul", result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeFileName_BlocksCOM1()
    {
        var result = InputValidator.SanitizeFileName("COM1");
        Assert.NotEqual("COM1", result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeFileName_RemovesInvalidCharacters()
    {
        var result = InputValidator.SanitizeFileName("file<>:\"|?*name");
        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.DoesNotContain(":", result);
        Assert.DoesNotContain("\"", result);
        Assert.DoesNotContain("|", result);
        Assert.DoesNotContain("?", result);
        Assert.DoesNotContain("*", result);
    }

    [Fact]
    public void SanitizeFileName_TrimsTrailingDots()
    {
        var result = InputValidator.SanitizeFileName("filename...");
        Assert.False(result.EndsWith('.'));
    }

    [Fact]
    public void SanitizeFileName_HandlesEmptyString()
    {
        var result = InputValidator.SanitizeFileName("");
        Assert.Equal("unnamed", result);
    }

    [Fact]
    public void SanitizeFileName_HandlesNull()
    {
        var result = InputValidator.SanitizeFileName(null!);
        Assert.Equal("unnamed", result);
    }

    [Fact]
    public void SanitizeFileName_HandlesWhitespace()
    {
        var result = InputValidator.SanitizeFileName("   ");
        Assert.Equal("unnamed", result);
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames()
    {
        var longName = new string('a', 500);
        var result = InputValidator.SanitizeFileName(longName);
        Assert.True(result.Length <= 200);
    }

    [Fact]
    public void SanitizeFileName_PreservesValidNames()
    {
        var result = InputValidator.SanitizeFileName("01 - Introduction to Python");
        Assert.Equal("01 - Introduction to Python", result);
    }

    [Fact]
    public void SanitizeFileName_PreservesUnicode()
    {
        var result = InputValidator.SanitizeFileName("講義 01 - はじめに");
        Assert.Equal("講義 01 - はじめに", result);
    }

    [Fact]
    public void SanitizeFileName_RemovesControlCharacters()
    {
        var result = InputValidator.SanitizeFileName("file\x01name\x1F");
        // Verify no control characters remain (0x00-0x1F, 0x7F)
        Assert.All(result, c => Assert.False(char.IsControl(c), $"Control character U+{(int)c:X4} found in result: \"{result}\""));
    }

    // ── ValidateUrl ──────────────────────────────────────────────

    [Fact]
    public void ValidateUrl_AcceptsHttps()
    {
        var (valid, _) = InputValidator.ValidateUrl("https://www.udemy.com/course/test");
        Assert.True(valid);
    }

    [Fact]
    public void ValidateUrl_AcceptsHttp()
    {
        var (valid, _) = InputValidator.ValidateUrl("http://example.com");
        Assert.True(valid);
    }

    [Fact]
    public void ValidateUrl_RejectsEmptyString()
    {
        var (valid, error) = InputValidator.ValidateUrl("");
        Assert.False(valid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUrl_RejectsFileScheme()
    {
        var (valid, _) = InputValidator.ValidateUrl("file:///etc/passwd");
        Assert.False(valid);
    }

    [Fact]
    public void ValidateUrl_RejectsJavascriptScheme()
    {
        var (valid, _) = InputValidator.ValidateUrl("javascript:alert(1)");
        Assert.False(valid);
    }

    [Fact]
    public void ValidateUrl_RejectsMalformed()
    {
        var (valid, _) = InputValidator.ValidateUrl("not a url at all");
        Assert.False(valid);
    }

    // ── ValidatePath ─────────────────────────────────────────────

    [Fact]
    public void ValidatePath_AcceptsAbsolutePath()
    {
        var (valid, _) = InputValidator.ValidatePath(@"C:\Users\Test\Downloads");
        Assert.True(valid);
    }

    [Fact]
    public void ValidatePath_RejectsEmptyPath()
    {
        var (valid, _) = InputValidator.ValidatePath("");
        Assert.False(valid);
    }

    [Fact]
    public void ValidatePath_DetectsPathTraversal()
    {
        var basePath = @"C:\Users\Test\Downloads";
        var (valid, error) = InputValidator.ValidatePath(@"C:\Users\Test\Downloads\..\..\Windows", basePath);
        Assert.False(valid);
        Assert.Contains("traversal", error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── RedactSecrets ────────────────────────────────────────────

    [Fact]
    public void RedactSecrets_RedactsBearerToken()
    {
        var input = "Authorization: Bearer abc123xyz456";
        var result = InputValidator.RedactSecrets(input);
        Assert.DoesNotContain("abc123xyz456", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void RedactSecrets_RedactsAccessTokenParam()
    {
        var input = "https://example.com/api?access_token=secret123&other=value";
        var result = InputValidator.RedactSecrets(input);
        Assert.DoesNotContain("secret123", result);
        Assert.Contains("access_token=[REDACTED]", result);
    }

    [Fact]
    public void RedactSecrets_RedactsCookieHeader()
    {
        var input = "Cookie: access_token=abc123; session_id=xyz456";
        var result = InputValidator.RedactSecrets(input);
        Assert.DoesNotContain("abc123", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void RedactSecrets_RedactsSignedUrlParams()
    {
        var input = "https://cdn.example.com/file.mp4?Signature=abc123&X-Amz-Credential=key456";
        var result = InputValidator.RedactSecrets(input);
        Assert.DoesNotContain("abc123", result);
        Assert.DoesNotContain("key456", result);
    }

    [Fact]
    public void RedactSecrets_PreservesNonSensitiveContent()
    {
        var input = "Downloaded file: lecture01.mp4 (150 MB)";
        var result = InputValidator.RedactSecrets(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void RedactSecrets_HandlesEmptyString()
    {
        Assert.Equal("", InputValidator.RedactSecrets(""));
    }

    [Fact]
    public void RedactSecrets_HandlesNull()
    {
        Assert.Null(InputValidator.RedactSecrets(null!));
    }
}
