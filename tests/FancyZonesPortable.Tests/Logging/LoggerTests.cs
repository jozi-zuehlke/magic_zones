using FancyZonesPortable.Core.Logging;
using Xunit;

namespace FancyZonesPortable.Tests.Logging;

public class LoggerTests : IDisposable
{
    private readonly string _tempDir;

    public LoggerTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "FancyZonesPortable.Tests",
            Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string[] GetLogFiles() =>
        Directory.GetFiles(_tempDir, "fzp-*.log");

    private string ReadSingleLogFile()
    {
        var files = GetLogFiles();
        Assert.Single(files);
        return File.ReadAllText(files[0]);
    }

    // ── Basic write tests ───────────────────────────────────────

    [Fact]
    public void Info_WritesToLogFile()
    {
        var logger = new Logger(logDirectory: _tempDir);

        logger.Info("test");

        var content = ReadSingleLogFile();
        Assert.Contains("[INFO] test", content);
    }

    [Fact]
    public void Warning_WritesToLogFile()
    {
        var logger = new Logger(logDirectory: _tempDir);

        logger.Warning("warn");

        var content = ReadSingleLogFile();
        Assert.Contains("[WARN] warn", content);
    }

    [Fact]
    public void Error_WritesToLogFile()
    {
        var logger = new Logger(logDirectory: _tempDir);

        logger.Error("err");

        var content = ReadSingleLogFile();
        Assert.Contains("[ERROR] err", content);
    }

    [Fact]
    public void ErrorWithException_IncludesExceptionDetails()
    {
        var logger = new Logger(logDirectory: _tempDir);
        var exception = new InvalidOperationException("something broke");

        logger.Error("msg", exception);

        var content = ReadSingleLogFile();
        Assert.Contains("[ERROR] msg", content);
        Assert.Contains("something broke", content);
    }

    // ── File naming & append tests ──────────────────────────────

    [Fact]
    public void Write_CreatesDateBasedLogFile()
    {
        var fixedDate = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var logger = new Logger(logDirectory: _tempDir, clock: () => fixedDate);

        logger.Info("hello");

        var files = GetLogFiles();
        Assert.Single(files);
        Assert.Equal("fzp-2025-06-15.log", Path.GetFileName(files[0]));
    }

    [Fact]
    public void Write_AppendsToExistingFile()
    {
        var logger = new Logger(logDirectory: _tempDir);

        logger.Info("first");
        logger.Info("second");

        var content = ReadSingleLogFile();
        Assert.Contains("[INFO] first", content);
        Assert.Contains("[INFO] second", content);
    }

    [Fact]
    public async Task Write_ThreadSafe_NoConcurrentWriteErrors()
    {
        var logger = new Logger(logDirectory: _tempDir);
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() => logger.Info($"thread-{i}")))
            .ToArray();

        await Task.WhenAll(tasks);

        var content = ReadSingleLogFile();
        for (int i = 0; i < 10; i++)
        {
            Assert.Contains($"thread-{i}", content);
        }
    }

    // ── Constructor tests ───────────────────────────────────────

    [Fact]
    public void Constructor_CreatesLogDirectory()
    {
        Assert.False(Directory.Exists(_tempDir));

        _ = new Logger(logDirectory: _tempDir);

        Assert.True(Directory.Exists(_tempDir));
    }

    // ── Cleanup tests ───────────────────────────────────────────

    [Fact]
    public void CleanupOldLogs_DeletesFilesOlderThan7Days()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "fzp-2020-01-01.log"), "old");
        File.WriteAllText(Path.Combine(_tempDir, "fzp-2020-06-15.log"), "old");

        var now = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = new Logger(logDirectory: _tempDir, clock: () => now);

        Assert.False(File.Exists(Path.Combine(_tempDir, "fzp-2020-01-01.log")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "fzp-2020-06-15.log")));
    }

    [Fact]
    public void CleanupOldLogs_KeepsRecentFiles()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "fzp-2025-06-30.log"), "recent");
        File.WriteAllText(Path.Combine(_tempDir, "fzp-2025-06-25.log"), "boundary");

        var now = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = new Logger(logDirectory: _tempDir, clock: () => now);

        Assert.True(File.Exists(Path.Combine(_tempDir, "fzp-2025-06-30.log")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "fzp-2025-06-25.log")));
    }

    [Fact]
    public void CleanupOldLogs_HandlesEmptyDirectory()
    {
        Directory.CreateDirectory(_tempDir);

        var exception = Record.Exception(() =>
            new Logger(logDirectory: _tempDir));

        Assert.Null(exception);
    }

    [Fact]
    public void CleanupOldLogs_IgnoresNonLogFiles()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "keep me");
        File.WriteAllText(Path.Combine(_tempDir, "data.csv"), "keep me too");
        File.WriteAllText(Path.Combine(_tempDir, "fzp-2020-01-01.log"), "old log");

        var now = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        _ = new Logger(logDirectory: _tempDir, clock: () => now);

        Assert.True(File.Exists(Path.Combine(_tempDir, "readme.txt")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "data.csv")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "fzp-2020-01-01.log")));
    }
}
