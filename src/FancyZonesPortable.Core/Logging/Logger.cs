using System.Globalization;

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

    public Logger(string? logDirectory = null, Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
        _logDirectory = logDirectory
            ?? Path.Combine(Path.GetTempPath(), "FancyZonesPortable");

        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        CleanupOldLogs();
    }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    public void Error(string message, Exception exception) =>
        Write("ERROR", $"{message} | {exception}");

    private void Write(string level, string message)
    {
        var now = _clock();
        var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{level}] {message}";
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
