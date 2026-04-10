using System.Text.Json;
using System.Text.Json.Serialization;
using MagicZonesPortable.Core.Abstractions;
using MagicZonesPortable.Core.Logging;

namespace MagicZonesPortable.Core.Config;

/// <summary>
/// Result of loading a configuration file.
/// </summary>
public record LoadResult(ZonesConfig Config, string? FilePath, bool IsNewDefault, List<string> ValidationErrors);

/// <summary>
/// Handles config file discovery, loading, parsing, and default config generation.
/// Searches for config in exe directory first, then falls back to %APPDATA%.
/// </summary>
public class ConfigLoader
{
    private const string ConfigFileName = "zones.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly ConfigValidator _validator = new();

    public ConfigLoader(IFileSystem fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Loads the configuration from the first discovered config file.
    /// On first run (no config found), writes default config to the exe directory.
    /// Returns a LoadResult with the config, file path, and any validation errors.
    /// </summary>
    public LoadResult Load()
    {
        var path = DiscoverConfigPath();
        if (path is null)
        {
            _logger.Info("No config file found; generating defaults.");
            var defaultConfig = CreateDefault();
            var writtenPath = Path.Combine(_fileSystem.GetExecutableDirectory(), ConfigFileName);
            Save(defaultConfig, writtenPath);
            return new LoadResult(defaultConfig, writtenPath, true, []);
        }

        try
        {
            var json = _fileSystem.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions);
            if (config is null)
            {
                _logger.Warning($"Config file at '{path}' deserialized to null; using defaults.");
                var defaultConfig = CreateDefault();
                var errors = _validator.Validate(defaultConfig);
                return new LoadResult(defaultConfig, path, false, errors);
            }

            _logger.SetMinimumLevel(SettingsParser.ParseLogLevel(config.Settings.LogLevel) ?? LogLevel.Info);
            _logger.Info($"Config loaded from '{path}'.");
            var validationErrors = _validator.Validate(config);
            return new LoadResult(config, path, false, validationErrors);
        }
        catch (JsonException ex)
        {
            _logger.Error($"Failed to parse config at '{path}': {ex.Message}");
            var defaultConfig = CreateDefault();
            return new LoadResult(defaultConfig, path, false, [$"Parse error: {ex.Message}"]);
        }
    }

    /// <summary>
    /// Discovers the config file path by checking exe directory first, then %APPDATA%.
    /// Returns null if no config file is found.
    /// </summary>
    public string? DiscoverConfigPath()
    {
        var exeDir = _fileSystem.GetExecutableDirectory();
        var exePath = Path.Combine(exeDir, ConfigFileName);
        if (_fileSystem.FileExists(exePath))
        {
            return exePath;
        }

        var appDataDir = Path.Combine(_fileSystem.GetAppDataPath(), "MagicZonesPortable");
        var appDataPath = Path.Combine(appDataDir, ConfigFileName);
        if (_fileSystem.FileExists(appDataPath))
        {
            return appDataPath;
        }

        return null;
    }

    /// <summary>
    /// Creates a default configuration with a simple two-column layout.
    /// </summary>
    public static ZonesConfig CreateDefault()
    {
        return new ZonesConfig
        {
            Version = 1,
            Settings = new SettingsConfig(),
            Monitors =
            [
                new MonitorConfig
                {
                    Id = "primary",
                    MatchBy = "primary",
                    CoordinateUnit = "percent",
                    Zones =
                    [
                        new ZoneDefinition { Id = "left", Name = "Left Half", Priority = 1, X = 0, Y = 0, Width = 0.5, Height = 1.0 },
                        new ZoneDefinition { Id = "right", Name = "Right Half", Priority = 1, X = 0.5, Y = 0, Width = 0.5, Height = 1.0 },
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Saves a configuration to the specified path.
    /// </summary>
    public void Save(ZonesConfig config, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && !_fileSystem.DirectoryExists(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        _fileSystem.WriteAllText(path, json);
        _logger.Info($"Config saved to '{path}'.");
    }
}
