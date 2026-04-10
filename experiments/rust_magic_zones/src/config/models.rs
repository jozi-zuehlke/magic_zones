/// Configuration models for zone layout definitions, deserialized from `zones.json`.

use serde::{Deserialize, Serialize};

fn default_activation_modifier() -> String {
    "Shift".to_string()
}

fn default_highlight_active_color() -> String {
    "#0078D4".to_string()
}

fn default_highlight_active_opacity() -> f64 {
    0.35
}

fn default_highlight_inactive_color() -> String {
    "#CCCCCC".to_string()
}

fn default_highlight_inactive_opacity() -> f64 {
    0.20
}

fn default_toggle_hotkey() -> String {
    "Ctrl+Win+Z".to_string()
}

fn default_log_level() -> String {
    "Info".to_string()
}

fn default_auto_reload() -> bool {
    true
}

fn default_debounce() -> u64 {
    500
}

fn default_coordinate_unit() -> String {
    "percentage".to_string()
}

/// Top-level configuration structure read from `zones.json`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ZonesConfig {
    pub version: u32,
    pub settings: Settings,
    pub monitors: Vec<MonitorConfig>,
}

/// Application-wide settings controlling behaviour and appearance.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Settings {
    #[serde(default = "default_activation_modifier")]
    pub activation_modifier: String,
    #[serde(default = "default_highlight_active_color")]
    pub highlight_active_color: String,
    #[serde(default = "default_highlight_active_opacity")]
    pub highlight_active_opacity: f64,
    #[serde(default = "default_highlight_inactive_color")]
    pub highlight_inactive_color: String,
    #[serde(default = "default_highlight_inactive_opacity")]
    pub highlight_inactive_opacity: f64,
    #[serde(default = "default_toggle_hotkey")]
    pub toggle_hotkey: String,
    #[serde(default = "default_log_level")]
    pub log_level: String,
    #[serde(default = "default_auto_reload")]
    pub auto_reload_config: bool,
    #[serde(default = "default_debounce")]
    pub auto_reload_debounce_ms: u64,
}

/// Configuration for a single monitor, listing its zones.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MonitorConfig {
    pub id: String,
    #[serde(default)]
    pub match_by: String,
    #[serde(default)]
    pub device_name: String,
    #[serde(default)]
    pub index: u32,
    #[serde(default = "default_coordinate_unit")]
    pub coordinate_unit: String,
    pub zones: Vec<ZoneDefinition>,
}

/// A single zone within a monitor layout.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ZoneDefinition {
    pub id: String,
    #[serde(default)]
    pub name: String,
    #[serde(default)]
    pub priority: i32,
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn deserialize_minimal_config() {
        let json = r#"{
            "version": 1,
            "settings": {},
            "monitors": [
                {
                    "id": "primary",
                    "zones": [
                        { "id": "left", "x": 0.0, "y": 0.0, "width": 50.0, "height": 100.0 }
                    ]
                }
            ]
        }"#;
        let config: ZonesConfig = serde_json::from_str(json).unwrap();
        assert_eq!(config.version, 1);
        assert_eq!(config.settings.activation_modifier, "Shift");
        assert_eq!(config.monitors.len(), 1);
        assert_eq!(config.monitors[0].zones[0].id, "left");
    }

    #[test]
    fn settings_defaults_are_applied() {
        let json = r#"{}"#;
        let settings: Settings = serde_json::from_str(json).unwrap();
        assert_eq!(settings.activation_modifier, "Shift");
        assert_eq!(settings.highlight_active_opacity, 0.35);
        assert!(settings.auto_reload_config);
        assert_eq!(settings.auto_reload_debounce_ms, 500);
    }

    #[test]
    fn roundtrip_serialization() {
        let json = r#"{
            "version": 1,
            "settings": {},
            "monitors": [
                {
                    "id": "mon1",
                    "zones": [
                        { "id": "z1", "x": 0.0, "y": 0.0, "width": 100.0, "height": 100.0 }
                    ]
                }
            ]
        }"#;
        let config: ZonesConfig = serde_json::from_str(json).unwrap();
        let serialized = serde_json::to_string(&config).unwrap();
        let config2: ZonesConfig = serde_json::from_str(&serialized).unwrap();
        assert_eq!(config2.version, config.version);
    }
}
