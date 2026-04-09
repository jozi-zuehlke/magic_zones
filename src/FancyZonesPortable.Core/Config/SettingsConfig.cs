using System.Text.Json.Serialization;

namespace FancyZonesPortable.Core.Config;

/// <summary>
/// Application-wide settings for FancyZones Portable.
/// Maps to the "settings" object in zones.json (PRD §7.3).
/// </summary>
public class SettingsConfig
{
    /// <summary>
    /// Modifier key held during drag to activate snapping.
    /// Values: "Shift", "Ctrl", "Alt".
    /// </summary>
    [JsonPropertyName("activationModifier")]
    public string ActivationModifier { get; set; } = "Shift";

    /// <summary>
    /// Hex color (RGB or ARGB) for the highlighted active zone.
    /// </summary>
    [JsonPropertyName("highlightActiveZoneColor")]
    public string HighlightActiveZoneColor { get; set; } = "#0078D4";

    /// <summary>
    /// Active zone fill opacity (0.0–1.0).
    /// </summary>
    [JsonPropertyName("highlightActiveZoneOpacity")]
    public double HighlightActiveZoneOpacity { get; set; } = 0.5;

    /// <summary>
    /// Hex color for inactive zone borders/fill.
    /// </summary>
    [JsonPropertyName("highlightInactiveZoneColor")]
    public string HighlightInactiveZoneColor { get; set; } = "#888888";

    /// <summary>
    /// Inactive zone indicator opacity (0.0–1.0).
    /// </summary>
    [JsonPropertyName("highlightInactiveZoneOpacity")]
    public double HighlightInactiveZoneOpacity { get; set; } = 0.2;

    /// <summary>
    /// Global enable/disable hotkey. Format: modifiers joined by "+", then key name.
    /// </summary>
    [JsonPropertyName("toggleHotkey")]
    public string ToggleHotkey { get; set; } = "Ctrl+Win+Z";

    /// <summary>
    /// Watch zones.json for changes and reload automatically.
    /// </summary>
    [JsonPropertyName("autoReloadConfig")]
    public bool AutoReloadConfig { get; set; } = true;

    /// <summary>
    /// Milliseconds to debounce file-change events before reloading.
    /// </summary>
    [JsonPropertyName("autoReloadDebounceMs")]
    public int AutoReloadDebounceMs { get; set; } = 500;
}
