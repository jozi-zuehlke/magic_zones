using MagicZonesPortable.Core.Abstractions;

namespace MagicZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IFileSystem"/> using real file I/O operations.
/// </summary>
internal class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string GetExecutableDirectory()
    {
        // Environment.ProcessPath works for single-file deployments where
        // Assembly.GetEntryAssembly().Location returns empty string.
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            return Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        }

        return AppContext.BaseDirectory;
    }

    public string GetAppDataPath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
