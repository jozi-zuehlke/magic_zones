using System.Text.Json;
using FancyZonesPortable.Core.Config;
using Xunit;

namespace FancyZonesPortable.Tests.Config;

/// <summary>
/// Verifies JSON round-trip serialization of all config model classes
/// against the PRD §7.6 annotated example.
/// </summary>
public class ConfigSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Deserialize_FullValidJson_ReturnsCorrectConfig()
    {
        const string json = """
        {
          "version": 1,
          "settings": {
            "activationModifier": "Shift",
            "highlightActiveZoneColor": "#0078D4",
            "highlightActiveZoneOpacity": 0.5,
            "highlightInactiveZoneColor": "#888888",
            "highlightInactiveZoneOpacity": 0.2,
            "toggleHotkey": "Ctrl+Win+Z",
            "logLevel": "DEBUG",
            "autoReloadConfig": true,
            "autoReloadDebounceMs": 500
          },
          "monitors": [
            {
              "id": "primary",
              "matchBy": "primary",
              "coordinateUnit": "percent",
              "zones": [
                { "id": "left-half", "name": "Left Half", "priority": 10, "x": 0.0, "y": 0.0, "width": 0.5, "height": 1.0 },
                { "id": "right-half", "name": "Right Half", "priority": 10, "x": 0.5, "y": 0.0, "width": 0.5, "height": 1.0 },
                { "id": "top-right-quarter", "name": "Top Right Quarter", "priority": 5, "x": 0.5, "y": 0.0, "width": 0.5, "height": 0.5 },
                { "id": "sidebar", "name": "Sidebar", "priority": 1, "x": 0.75, "y": 0.0, "width": 0.25, "height": 1.0 }
              ]
            }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        // Top-level
        Assert.Equal(1, config.Version);

        // Settings
        Assert.Equal("Shift", config.Settings.ActivationModifier);
        Assert.Equal("#0078D4", config.Settings.HighlightActiveZoneColor);
        Assert.Equal(0.5, config.Settings.HighlightActiveZoneOpacity);
        Assert.Equal("#888888", config.Settings.HighlightInactiveZoneColor);
        Assert.Equal(0.2, config.Settings.HighlightInactiveZoneOpacity);
        Assert.Equal("Ctrl+Win+Z", config.Settings.ToggleHotkey);
        Assert.Equal("DEBUG", config.Settings.LogLevel);
        Assert.True(config.Settings.AutoReloadConfig);
        Assert.Equal(500, config.Settings.AutoReloadDebounceMs);

        // Monitor
        Assert.Single(config.Monitors);
        var monitor = config.Monitors[0];
        Assert.Equal("primary", monitor.Id);
        Assert.Equal("primary", monitor.MatchBy);
        Assert.Equal("percent", monitor.CoordinateUnit);
        Assert.Equal(4, monitor.Zones.Count);

        // Zones
        var leftHalf = monitor.Zones[0];
        Assert.Equal("left-half", leftHalf.Id);
        Assert.Equal("Left Half", leftHalf.Name);
        Assert.Equal(10, leftHalf.Priority);
        Assert.Equal(0.0, leftHalf.X);
        Assert.Equal(0.0, leftHalf.Y);
        Assert.Equal(0.5, leftHalf.Width);
        Assert.Equal(1.0, leftHalf.Height);

        var sidebar = monitor.Zones[3];
        Assert.Equal("sidebar", sidebar.Id);
        Assert.Equal("Sidebar", sidebar.Name);
        Assert.Equal(1, sidebar.Priority);
        Assert.Equal(0.75, sidebar.X);
        Assert.Equal(0.0, sidebar.Y);
        Assert.Equal(0.25, sidebar.Width);
        Assert.Equal(1.0, sidebar.Height);
    }

    [Fact]
    public void Deserialize_DefaultSettings_HasCorrectDefaults()
    {
        const string json = """{"version":1,"settings":{},"monitors":[]}""";

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        Assert.Equal(1, config.Version);
        Assert.Equal("Shift", config.Settings.ActivationModifier);
        Assert.Equal("#0078D4", config.Settings.HighlightActiveZoneColor);
        Assert.Equal(0.5, config.Settings.HighlightActiveZoneOpacity);
        Assert.Equal("#888888", config.Settings.HighlightInactiveZoneColor);
        Assert.Equal(0.2, config.Settings.HighlightInactiveZoneOpacity);
        Assert.Equal("Ctrl+Win+Z", config.Settings.ToggleHotkey);
        Assert.Equal("INFO", config.Settings.LogLevel);
        Assert.True(config.Settings.AutoReloadConfig);
        Assert.Equal(500, config.Settings.AutoReloadDebounceMs);
        Assert.Empty(config.Monitors);
    }

    [Fact]
    public void Serialize_DefaultConfig_ProducesValidJson()
    {
        var config = new ZonesConfig();

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        Assert.Equal(config.Version, roundTripped.Version);
        Assert.Equal(config.Settings.ActivationModifier, roundTripped.Settings.ActivationModifier);
        Assert.Equal(config.Settings.HighlightActiveZoneColor, roundTripped.Settings.HighlightActiveZoneColor);
        Assert.Equal(config.Settings.HighlightActiveZoneOpacity, roundTripped.Settings.HighlightActiveZoneOpacity);
        Assert.Equal(config.Settings.HighlightInactiveZoneColor, roundTripped.Settings.HighlightInactiveZoneColor);
        Assert.Equal(config.Settings.HighlightInactiveZoneOpacity, roundTripped.Settings.HighlightInactiveZoneOpacity);
        Assert.Equal(config.Settings.ToggleHotkey, roundTripped.Settings.ToggleHotkey);
        Assert.Equal(config.Settings.LogLevel, roundTripped.Settings.LogLevel);
        Assert.Equal(config.Settings.AutoReloadConfig, roundTripped.Settings.AutoReloadConfig);
        Assert.Equal(config.Settings.AutoReloadDebounceMs, roundTripped.Settings.AutoReloadDebounceMs);
    }

    [Fact]
    public void Deserialize_UnknownProperties_AreIgnored()
    {
        const string json = """
        {
          "version": 1,
          "unknownField": "should be ignored",
          "settings": {
            "activationModifier": "Shift",
            "extraSetting": 42
          },
          "monitors": []
        }
        """;

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        Assert.Equal(1, config.Version);
        Assert.Equal("Shift", config.Settings.ActivationModifier);
    }

    [Fact]
    public void Deserialize_PixelCoordinateUnit_ParsesCorrectly()
    {
        const string json = """
        {
          "version": 1,
          "settings": {},
          "monitors": [
            {
              "id": "pixel-monitor",
              "matchBy": "index",
              "coordinateUnit": "pixels",
              "zones": [
                { "id": "z1", "name": "Zone 1", "priority": 1, "x": 100, "y": 200, "width": 800, "height": 600 }
              ]
            }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        var monitor = config.Monitors[0];
        Assert.Equal("pixels", monitor.CoordinateUnit);
        Assert.Equal(100.0, monitor.Zones[0].X);
        Assert.Equal(200.0, monitor.Zones[0].Y);
        Assert.Equal(800.0, monitor.Zones[0].Width);
        Assert.Equal(600.0, monitor.Zones[0].Height);
    }

    [Fact]
    public void Deserialize_PercentZones_FractionalValues()
    {
        const string json = """
        {
          "version": 1,
          "settings": {},
          "monitors": [
            {
              "id": "pct-monitor",
              "matchBy": "primary",
              "coordinateUnit": "percent",
              "zones": [
                { "id": "z1", "name": "Third", "priority": 1, "x": 0.0, "y": 0.0, "width": 0.333, "height": 1.0 }
              ]
            }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        var zone = config.Monitors[0].Zones[0];
        Assert.Equal(0.0, zone.X);
        Assert.Equal(0.0, zone.Y);
        Assert.Equal(0.333, zone.Width, precision: 5);
        Assert.Equal(1.0, zone.Height);
    }

    [Fact]
    public void Deserialize_MissingSettingsBlock_UsesDefaults()
    {
        const string json = """{"version":1,"monitors":[]}""";

        var config = JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions)!;

        Assert.NotNull(config.Settings);
        Assert.Equal("Shift", config.Settings.ActivationModifier);
        Assert.Equal("#0078D4", config.Settings.HighlightActiveZoneColor);
        Assert.Equal(0.5, config.Settings.HighlightActiveZoneOpacity);
        Assert.Equal("#888888", config.Settings.HighlightInactiveZoneColor);
        Assert.Equal(0.2, config.Settings.HighlightInactiveZoneOpacity);
        Assert.Equal("Ctrl+Win+Z", config.Settings.ToggleHotkey);
        Assert.Equal("INFO", config.Settings.LogLevel);
        Assert.True(config.Settings.AutoReloadConfig);
        Assert.Equal(500, config.Settings.AutoReloadDebounceMs);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        const string json = """{ this is not valid json }""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ZonesConfig>(json, JsonOptions));
    }
}
