using FancyZonesPortable.Config;
using FancyZonesPortable.Logging;

namespace FancyZonesPortable.App;

/// <summary>
/// Watches zones.json for changes using FileSystemWatcher with configurable debounce.
/// </summary>
internal sealed class ConfigWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private System.Windows.Forms.Timer? _debounceTimer;
    private readonly SynchronizationContext _syncContext;
    private bool _disposed;

    /// <summary>
    /// Fired on the UI thread when the config file has changed (after debounce).
    /// </summary>
    public event Action? ConfigChanged;

    public ConfigWatcher()
    {
        _syncContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("ConfigWatcher must be created on a thread with a SynchronizationContext.");
    }

    /// <summary>
    /// Starts watching the specified config file.
    /// </summary>
    public void Start(string configPath, int debounceMs)
    {
        Stop();

        var dir = Path.GetDirectoryName(configPath);
        var file = Path.GetFileName(configPath);

        if (dir == null || file == null) return;

        _debounceTimer = new System.Windows.Forms.Timer { Interval = debounceMs };
        _debounceTimer.Tick += OnDebounceElapsed;

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;

        Logger.Info($"Config watcher started: {configPath} (debounce={debounceMs}ms)");
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_debounceTimer != null)
        {
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
            _debounceTimer = null;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // FileSystemWatcher fires on a thread-pool thread; marshal to UI thread for debounce
        _syncContext.Post(_ =>
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }, null);
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        Logger.Info("Config file change detected (debounced), triggering reload.");
        ConfigChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
