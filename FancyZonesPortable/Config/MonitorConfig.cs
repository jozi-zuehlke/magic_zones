using System.Text.Json.Serialization;

namespace FancyZonesPortable.Config;

public sealed class MonitorConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "primary";

    [JsonPropertyName("matchBy")]
    public string MatchBy { get; set; } = "primary";

    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("coordinateUnit")]
    public string CoordinateUnit { get; set; } = "percent";

    [JsonPropertyName("zones")]
    public List<ZoneDefinition> Zones { get; set; } = new();
}
