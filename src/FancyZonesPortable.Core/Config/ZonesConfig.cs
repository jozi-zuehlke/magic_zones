using System.Text.Json.Serialization;

namespace FancyZonesPortable.Core.Config;

/// <summary>
/// Top-level configuration model for FancyZones Portable.
/// </summary>
public class ZonesConfig
{
    /// <summary>
    /// Configuration schema version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Application settings.
    /// </summary>
    [JsonPropertyName("settings")]
    public SettingsConfig Settings { get; set; } = new();

    /// <summary>
    /// Per-monitor zone layouts.
    /// </summary>
    [JsonPropertyName("monitors")]
    public List<MonitorConfig> Monitors { get; set; } = [];
}
