using System.Drawing;
using System.Text.Json;
using FancyZonesPortable.Logging;

namespace FancyZonesPortable.Config;

/// <summary>
/// Handles config file discovery, parsing, validation, and default generation.
/// </summary>
internal static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    /// <summary>
    /// Discovers the config file path using the priority:
    /// 1. zones.json next to the executable
    /// 2. %APPDATA%\FancyZonesPortable\zones.json
    /// Returns null if neither exists.
    /// </summary>
    public static string? FindConfigPath()
    {
        var exeDir = AppContext.BaseDirectory;
        var exePath = Path.Combine(exeDir, "zones.json");
        if (File.Exists(exePath))
            return exePath;

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FancyZonesPortable");
        var appDataPath = Path.Combine(appDataDir, "zones.json");
        if (File.Exists(appDataPath))
            return appDataPath;

        return null;
    }

    /// <summary>
    /// Returns the path where a default config should be generated.
    /// Prefers the exe directory.
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        var exeDir = AppContext.BaseDirectory;
        return Path.Combine(exeDir, "zones.json");
    }

    /// <summary>
    /// Generates a default zones.json file with a common layout.
    /// </summary>
    public static string GenerateDefaultConfig(string path)
    {
        var config = new ZonesConfig
        {
            Version = 1,
            Settings = new SettingsConfig(),
            Monitors = new List<MonitorConfig>
            {
                new MonitorConfig
                {
                    Id = "primary",
                    MatchBy = "primary",
                    CoordinateUnit = "percent",
                    Zones = new List<ZoneDefinition>
                    {
                        new() { Id = "left-half", Name = "Left Half", Priority = 10, X = 0.0, Y = 0.0, Width = 0.5, Height = 1.0 },
                        new() { Id = "right-half", Name = "Right Half", Priority = 10, X = 0.5, Y = 0.0, Width = 0.5, Height = 1.0 },
                        new() { Id = "top-right-quarter", Name = "Top Right Quarter", Priority = 5, X = 0.5, Y = 0.0, Width = 0.5, Height = 0.5 },
                        new() { Id = "sidebar", Name = "Sidebar", Priority = 1, X = 0.75, Y = 0.0, Width = 0.25, Height = 1.0 }
                    }
                }
            }
        };

        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);

        Logger.Info($"Generated default config at: {path}");
        return path;
    }

    /// <summary>
    /// Loads and validates the config from the given path.
    /// Returns the parsed config and any warnings.
    /// Throws on parse failure.
    /// </summary>
    public static (ZonesConfig Config, List<string> Warnings) Load(string path)
    {
        var warnings = new List<string>();
        var json = File.ReadAllText(path);

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException("Config file deserialized to null.");

        if (config.Version != 1)
            warnings.Add($"Config version {config.Version} is not 1; some settings may be ignored.");

        // Validate and resolve zones for primary monitor
        foreach (var monitor in config.Monitors)
        {
            ValidateMonitor(monitor, warnings);
        }

        Logger.Info($"Loaded config from: {path}");
        return (config, warnings);
    }

    /// <summary>
    /// Resolves zone geometry to absolute pixel rectangles against the given working area.
    /// Also clips zones that extend outside monitor bounds.
    /// </summary>
    public static List<string> ResolveZones(ZonesConfig config, Rectangle workingArea)
    {
        var warnings = new List<string>();

        foreach (var monitor in config.Monitors)
        {
            if (monitor.MatchBy != "primary")
                continue; // v1 only processes primary monitor

            bool isPercent = monitor.CoordinateUnit.Equals("percent", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < monitor.Zones.Count; i++)
            {
                var zone = monitor.Zones[i];
                zone.ArrayIndex = i;

                int x, y, w, h;

                if (isPercent)
                {
                    x = workingArea.Left + (int)(zone.X * workingArea.Width);
                    y = workingArea.Top + (int)(zone.Y * workingArea.Height);
                    w = (int)(zone.Width * workingArea.Width);
                    h = (int)(zone.Height * workingArea.Height);
                }
                else
                {
                    x = workingArea.Left + (int)zone.X;
                    y = workingArea.Top + (int)zone.Y;
                    w = (int)zone.Width;
                    h = (int)zone.Height;
                }

                var rect = new Rectangle(x, y, w, h);

                // Clip to monitor bounds
                var clipped = Rectangle.Intersect(rect, workingArea);
                if (clipped != rect)
                {
                    warnings.Add($"Zone '{zone.Id}' extends outside monitor bounds and was clipped.");
                    rect = clipped;
                }

                zone.AbsoluteRect = rect;
            }
        }

        return warnings;
    }

    private static void ValidateMonitor(MonitorConfig monitor, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(monitor.Id))
            warnings.Add("Monitor has an empty or missing 'id'.");

        if (monitor.MatchBy == "deviceName" && string.IsNullOrWhiteSpace(monitor.DeviceName))
            warnings.Add($"Monitor '{monitor.Id}' uses matchBy=deviceName but no deviceName is set.");

        if (monitor.MatchBy == "index" && monitor.Index == null)
            warnings.Add($"Monitor '{monitor.Id}' uses matchBy=index but no index is set.");

        var unit = monitor.CoordinateUnit;
        if (unit != "pixels" && unit != "percent")
            warnings.Add($"Monitor '{monitor.Id}' has unknown coordinateUnit '{unit}'; defaulting to 'percent'.");

        var seenIds = new HashSet<string>();
        for (int i = 0; i < monitor.Zones.Count; i++)
        {
            var zone = monitor.Zones[i];
            zone.ArrayIndex = i;

            if (string.IsNullOrWhiteSpace(zone.Id))
                warnings.Add($"Zone at index {i} in monitor '{monitor.Id}' has an empty id.");
            else if (!seenIds.Add(zone.Id))
                warnings.Add($"Duplicate zone id '{zone.Id}' in monitor '{monitor.Id}'.");

            if (zone.Priority < 0)
                warnings.Add($"Zone '{zone.Id}' has negative priority {zone.Priority}.");

            if (zone.Width <= 0 || zone.Height <= 0)
                warnings.Add($"Zone '{zone.Id}' has non-positive dimensions.");
        }
    }
}
