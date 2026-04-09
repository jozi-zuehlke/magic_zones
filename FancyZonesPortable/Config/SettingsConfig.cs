using System.Text.Json.Serialization;

namespace FancyZonesPortable.Config;

public sealed class SettingsConfig
{
    [JsonPropertyName("activationModifier")]
    public string ActivationModifier { get; set; } = "Shift";

    [JsonPropertyName("highlightActiveZoneColor")]
    public string HighlightActiveZoneColor { get; set; } = "#0078D4";

    [JsonPropertyName("highlightActiveZoneOpacity")]
    public double HighlightActiveZoneOpacity { get; set; } = 0.5;

    [JsonPropertyName("highlightInactiveZoneColor")]
    public string HighlightInactiveZoneColor { get; set; } = "#888888";

    [JsonPropertyName("highlightInactiveZoneOpacity")]
    public double HighlightInactiveZoneOpacity { get; set; } = 0.2;

    [JsonPropertyName("toggleHotkey")]
    public string ToggleHotkey { get; set; } = "Ctrl+Win+Z";

    [JsonPropertyName("autoReloadConfig")]
    public bool AutoReloadConfig { get; set; } = true;

    [JsonPropertyName("autoReloadDebounceMs")]
    public int AutoReloadDebounceMs { get; set; } = 500;
}
