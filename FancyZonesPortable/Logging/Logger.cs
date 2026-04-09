using System.Globalization;

namespace FancyZonesPortable.Logging;

/// <summary>
/// Simple rolling file logger that writes to %TEMP%\FancyZonesPortable\fzp-{date}.log.
/// Deletes log files older than 7 days on startup.
/// </summary>
internal static class Logger
{
    private static readonly object Lock = new();
    private static string? _logDir;
    private static string? _currentLogPath;
    private static StreamWriter? _writer;

    public static void Initialize()
    {
        _logDir = Path.Combine(Path.GetTempPath(), "FancyZonesPortable");
        Directory.CreateDirectory(_logDir);

        var dateStr = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _currentLogPath = Path.Combine(_logDir, $"fzp-{dateStr}.log");

        _writer = new StreamWriter(_currentLogPath, append: true)
        {
            AutoFlush = true
        };

        CleanOldLogs();
        Info("Logger initialized.");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) =>
        Write("ERROR", $"{message}\n{ex}");

    private static void Write(string level, string message)
    {
        lock (Lock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                _writer?.WriteLine($"[{timestamp}] [{level}] {message}");
            }
            catch
            {
                // Logging should never throw
            }
        }
    }

    private static void CleanOldLogs()
    {
        if (_logDir == null) return;

        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var file in Directory.GetFiles(_logDir, "fzp-*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                    Info($"Deleted old log file: {Path.GetFileName(file)}");
                }
            }
        }
        catch (Exception ex)
        {
            Error("Failed to clean old logs", ex);
        }
    }

    public static void Shutdown()
    {
        lock (Lock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
