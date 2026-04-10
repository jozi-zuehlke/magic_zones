namespace MagicZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for monitoring config file changes on disk.
/// </summary>
public interface IConfigFileWatcher : IDisposable
{
    /// <summary>
    /// Fired when the watched configuration file changes.
    /// </summary>
    event EventHandler? ConfigFileChanged;

    /// <summary>
    /// Starts watching the specified file for changes.
    /// </summary>
    /// <param name="filePath">Path to the file to watch.</param>
    /// <param name="debounceMs">Debounce interval in milliseconds before firing the change event.</param>
    void Watch(string filePath, int debounceMs = 500);

    /// <summary>
    /// Stops watching.
    /// </summary>
    void Stop();
}
