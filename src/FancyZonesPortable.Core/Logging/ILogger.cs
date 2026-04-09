namespace FancyZonesPortable.Core.Logging;

/// <summary>
/// Abstraction for application logging.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void Info(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    void Error(string message);

    /// <summary>
    /// Logs an error message with an associated exception.
    /// </summary>
    void Error(string message, Exception exception);
}
