using RapidUdemyDW.Services;

namespace RapidUdemyDW.Tests;

public class SafeFileWriterTests : IDisposable
{
    private readonly string _testDir;

    public SafeFileWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"RapidUdemyDW_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* cleanup */ }
    }

    [Fact]
    public async Task WriteAllTextAtomicAsync_WritesFile()
    {
        var filePath = Path.Combine(_testDir, "test.json");
        await SafeFileWriter.WriteAllTextAtomicAsync(filePath, """{"key": "value"}""");

        Assert.True(File.Exists(filePath));
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("value", content);
    }

    [Fact]
    public async Task WriteAllTextAtomicAsync_OverwritesExisting()
    {
        var filePath = Path.Combine(_testDir, "test.json");
        await SafeFileWriter.WriteAllTextAtomicAsync(filePath, "original");
        await SafeFileWriter.WriteAllTextAtomicAsync(filePath, "updated");

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("updated", content);
    }

    [Fact]
    public async Task WriteAllTextAtomicAsync_CreatesDirectory()
    {
        var filePath = Path.Combine(_testDir, "subdir", "nested", "test.json");
        await SafeFileWriter.WriteAllTextAtomicAsync(filePath, "data");

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task WriteAllTextAtomicAsync_NoTempFileLeftOnSuccess()
    {
        var filePath = Path.Combine(_testDir, "test.json");
        await SafeFileWriter.WriteAllTextAtomicAsync(filePath, "data");

        var files = Directory.GetFiles(_testDir);
        Assert.Single(files); // Only the target file
    }

    [Fact]
    public async Task ReadWithRecoveryAsync_ReadsMainFile()
    {
        var filePath = Path.Combine(_testDir, "data.json");
        await File.WriteAllTextAsync(filePath, """{"data": true}""");

        var content = await SafeFileWriter.ReadWithRecoveryAsync(filePath);
        Assert.Contains("data", content);
    }

    [Fact]
    public async Task ReadWithRecoveryAsync_FallsBackToBackup()
    {
        var filePath = Path.Combine(_testDir, "data.json");
        var backupPath = filePath + ".bak";

        // Write only the backup file
        await File.WriteAllTextAsync(backupPath, """{"recovered": true}""");

        var content = await SafeFileWriter.ReadWithRecoveryAsync(filePath);
        Assert.Contains("recovered", content);
    }

    [Fact]
    public async Task ReadWithRecoveryAsync_ReturnsNullIfNeitherExists()
    {
        var filePath = Path.Combine(_testDir, "nonexistent.json");
        var content = await SafeFileWriter.ReadWithRecoveryAsync(filePath);
        Assert.Null(content);
    }

    [Fact]
    public async Task ReadWithRecoveryAsync_CreatesBackupOnSuccessfulRead()
    {
        var filePath = Path.Combine(_testDir, "data.json");
        await File.WriteAllTextAsync(filePath, "good data");

        await SafeFileWriter.ReadWithRecoveryAsync(filePath);

        Assert.True(File.Exists(filePath + ".bak"));
    }

    [Fact]
    public void CheckDiskSpace_ReturnsValidResult()
    {
        var (hasSpace, available) = SafeFileWriter.CheckDiskSpace(_testDir);
        // On any modern system, temp dir should have space
        Assert.True(hasSpace);
        Assert.True(available > 0);
    }

    [Fact]
    public void CheckDiskSpace_DetectsLargeRequirement()
    {
        // Request absurdly large amount — should fail on most systems
        var (hasSpace, _) = SafeFileWriter.CheckDiskSpace(_testDir, long.MaxValue / 2);
        Assert.False(hasSpace);
    }
}
