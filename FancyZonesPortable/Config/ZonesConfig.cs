using System.Text.Json.Serialization;

namespace FancyZonesPortable.Config;

public sealed class ZonesConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("settings")]
    public SettingsConfig Settings { get; set; } = new();

    [JsonPropertyName("monitors")]
    public List<MonitorConfig> Monitors { get; set; } = new();
}
