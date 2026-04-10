using MagicZonesPortable.Core.Config;

namespace MagicZonesPortable.Tests.Helpers;

/// <summary>
/// Shared test utilities and config builders for use across test classes.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a simple monitor config with the specified zones in percent coordinates.
    /// </summary>
    public static MonitorConfig CreatePercentMonitor(string id = "test-monitor", params ZoneDefinition[] zones)
    {
        return new MonitorConfig
        {
            Id = id,
            MatchBy = "primary",
            CoordinateUnit = "percent",
            Zones = [.. zones],
        };
    }

    /// <summary>
    /// Creates a monitor config with zones defined in absolute pixel coordinates.
    /// </summary>
    public static MonitorConfig CreatePixelMonitor(string id, params ZoneDefinition[] zones)
    {
        return new MonitorConfig
        {
            Id = id,
            MatchBy = "primary",
            CoordinateUnit = "pixels",
            Zones = [.. zones],
        };
    }

    /// <summary>
    /// Creates a simple zone definition.
    /// </summary>
    public static ZoneDefinition CreateZone(
        string id,
        string name,
        double x,
        double y,
        double width,
        double height,
        int priority = 1)
    {
        return new ZoneDefinition
        {
            Id = id,
            Name = name,
            Priority = priority,
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
    }

    /// <summary>
    /// Creates a valid default <see cref="ZonesConfig"/> with PRD-aligned defaults.
    /// </summary>
    public static ZonesConfig CreateValidConfig()
    {
        return new ZonesConfig
        {
            Version = 1,
            Settings = new SettingsConfig(),
            Monitors =
            [
                CreatePercentMonitor("primary",
                    CreateZone("left-half", "Left Half", 0, 0, 0.5, 1.0, priority: 10),
                    CreateZone("right-half", "Right Half", 0.5, 0, 0.5, 1.0, priority: 10)),
            ],
        };
    }

    /// <summary>
    /// Creates the exact PRD §7.6 annotated example configuration.
    /// </summary>
    public static ZonesConfig CreatePRDExampleConfig()
    {
        return new ZonesConfig
        {
            Version = 1,
            Settings = new SettingsConfig(),
            Monitors =
            [
                CreatePercentMonitor("primary",
                    CreateZone("left-half", "Left Half", 0, 0, 0.5, 1.0, priority: 10),
                    CreateZone("right-half", "Right Half", 0.5, 0, 0.5, 1.0, priority: 10),
                    CreateZone("top-right-quarter", "Top-Right Quarter", 0.5, 0, 0.5, 0.5, priority: 5),
                    CreateZone("sidebar", "Sidebar", 0.75, 0, 0.25, 1.0, priority: 1)),
            ],
        };
    }
}
