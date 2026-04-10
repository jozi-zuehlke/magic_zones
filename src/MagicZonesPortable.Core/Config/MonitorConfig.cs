using System.Text.Json.Serialization;

namespace MagicZonesPortable.Core.Config;

/// <summary>
/// Configuration for a single monitor's zone layout.
/// </summary>
public class MonitorConfig
{
    /// <summary>
    /// Unique identifier for this monitor configuration.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// How to match this config to a physical monitor ("primary", "deviceName", or "index").
    /// </summary>
    [JsonPropertyName("matchBy")]
    public string MatchBy { get; set; } = "primary";

    /// <summary>
    /// The device name to match (when matchBy is "deviceName").
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }

    /// <summary>
    /// The monitor index to match (when matchBy is "index").
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// The coordinate unit used for zone definitions ("percent" or "pixels").
    /// </summary>
    [JsonPropertyName("coordinateUnit")]
    public string CoordinateUnit { get; set; } = "percent";

    /// <summary>
    /// The zone definitions for this monitor.
    /// </summary>
    [JsonPropertyName("zones")]
    public List<ZoneDefinition> Zones { get; set; } = [];
}
