namespace MagicZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for file system operations to enable testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Returns true if the file exists at the specified path.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    string ReadAllText(string path);

    /// <summary>
    /// Writes text to a file, creating it if it doesn't exist.
    /// </summary>
    void WriteAllText(string path, string content);

    /// <summary>
    /// Returns true if the directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Creates a directory (and any parent directories) if it doesn't exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Gets the directory of the currently executing assembly.
    /// </summary>
    string GetExecutableDirectory();

    /// <summary>
    /// Gets the user's application data folder path.
    /// </summary>
    string GetAppDataPath();
}
