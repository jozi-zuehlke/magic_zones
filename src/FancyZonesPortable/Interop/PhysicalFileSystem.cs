using System.Reflection;
using FancyZonesPortable.Core.Abstractions;

namespace FancyZonesPortable.Interop;

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

    public string GetExecutableDirectory() =>
        Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;

    public string GetAppDataPath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
