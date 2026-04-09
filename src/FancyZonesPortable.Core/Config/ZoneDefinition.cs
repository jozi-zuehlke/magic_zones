using System.Text.Json.Serialization;

namespace FancyZonesPortable.Core.Config;

/// <summary>
/// Defines a single snap zone's geometry and metadata.
/// </summary>
public class ZoneDefinition
{
    /// <summary>
    /// Unique identifier for this zone.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this zone.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Priority for overlap resolution (lower value = higher precedence).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// X coordinate (0.0–1.0 fraction for percent mode, or absolute pixels).
    /// </summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>
    /// Y coordinate (0.0–1.0 fraction for percent mode, or absolute pixels).
    /// </summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>
    /// Width (0.0–1.0 fraction for percent mode, or absolute pixels).
    /// </summary>
    [JsonPropertyName("width")]
    public double Width { get; set; }

    /// <summary>
    /// Height (0.0–1.0 fraction for percent mode, or absolute pixels).
    /// </summary>
    [JsonPropertyName("height")]
    public double Height { get; set; }
}
