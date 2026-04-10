using MagicZonesPortable.Core.Abstractions;

namespace MagicZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IConfigFileWatcher"/> using <see cref="System.IO.FileSystemWatcher"/>.
/// </summary>
internal class ConfigFileWatcher : IConfigFileWatcher
{
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private int _debounceMs;

    public event EventHandler? ConfigFileChanged;

    public void Watch(string filePath, int debounceMs = 500)
    {
        Stop();

        _debounceMs = debounceMs;
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var fileName = Path.GetFileName(filePath);

        _debounceTimer = new System.Threading.Timer(OnDebounceElapsed);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += (_, _) => ResetDebounce();
    }

    private void OnFileEvent(object? sender, FileSystemEventArgs e)
    {
        ResetDebounce();
    }

    private void ResetDebounce()
    {
        _debounceTimer?.Change(_debounceMs, Timeout.Infinite);
    }

    private void OnDebounceElapsed(object? state)
    {
        ConfigFileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
