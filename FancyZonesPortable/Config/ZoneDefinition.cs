using System.Drawing;
using System.Text.Json.Serialization;

namespace FancyZonesPortable.Config;

public sealed class ZoneDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    /// <summary>
    /// Index of this zone in the original array, used for tie-breaking.
    /// Set during config loading.
    /// </summary>
    [JsonIgnore]
    public int ArrayIndex { get; set; }

    /// <summary>
    /// Computed absolute pixel rectangle for the zone on the monitor.
    /// Set during config loading when zones are resolved against the working area.
    /// </summary>
    [JsonIgnore]
    public Rectangle AbsoluteRect { get; set; }
}
