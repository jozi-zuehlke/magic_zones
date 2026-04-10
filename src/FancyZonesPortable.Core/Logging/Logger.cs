using System.Globalization;
using System.Threading;

namespace FancyZonesPortable.Core.Logging;

/// <summary>
/// Rolling file logger that writes to %TEMP%\FancyZonesPortable\.
/// Thread-safe with a lock around file writes.
/// Deletes log files older than 7 days on startup.
/// </summary>
public class Logger : ILogger
{
    private readonly string _logDirectory;
    private readonly Func<DateTime> _clock;
    private readonly object _lock = new();
    private int _minimumLevel;

    public Logger(
        string? logDirectory = null,
        Func<DateTime>? clock = null,
        LogLevel minimumLevel = LogLevel.Info)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
        _logDirectory = logDirectory
            ?? Path.Combine(Path.GetTempPath(), "FancyZonesPortable");
        _minimumLevel = (int)minimumLevel;

        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        CleanupOldLogs();
    }

    public void Debug(string message) => Write(LogLevel.Debug, "DEBUG", message);

    public void Info(string message) => Write(LogLevel.Info, "INFO", message);

    public void Warning(string message) => Write(LogLevel.Warning, "WARN", message);

    public void Error(string message) => Write(LogLevel.Error, "ERROR", message);

    public void Error(string message, Exception exception) =>
        Write(LogLevel.Error, "ERROR", $"{message} | {exception}");

    public void SetMinimumLevel(LogLevel level)
    {
        Volatile.Write(ref _minimumLevel, (int)level);
    }

    private void Write(LogLevel level, string label, string message)
    {
        if ((int)level < Volatile.Read(ref _minimumLevel))
        {
            return;
        }

        var now = _clock();
        var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{label}] {message}";
        var logFile = Path.Combine(_logDirectory, $"fzp-{now:yyyy-MM-dd}.log");

        lock (_lock)
        {
            File.AppendAllText(logFile, logLine + Environment.NewLine);
        }
    }

    private void CleanupOldLogs(int retentionDays = 7)
    {
        try
        {
            var cutoff = _clock().Date.AddDays(-retentionDays);

            foreach (var file in Directory.GetFiles(_logDirectory, "fzp-*.log"))
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // fileName is "fzp-yyyy-MM-dd"
                    if (fileName.Length == 14
                        && DateTime.TryParseExact(
                            fileName.Substring(4),
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var fileDate)
                        && fileDate < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Best-effort: skip locked or inaccessible files
                }
            }
        }
        catch
        {
            // Best-effort: don't crash if directory enumeration fails
        }
    }
}
