/// Validates a parsed [`ZonesConfig`] for semantic correctness.
///
/// Checks include: version compatibility, non-empty monitors, valid zone dimensions,
/// unique IDs, and sensible setting values.

use crate::config::models::ZonesConfig;

/// Validates zone configuration semantics beyond what JSON schema covers.
pub struct ConfigValidator;

impl ConfigValidator {
    /// Validate the given config, returning a list of human-readable warnings/errors.
    pub fn validate(config: &ZonesConfig) -> Vec<ValidationError> {
        let mut errors = Vec::new();

        if config.version != 1 {
            errors.push(ValidationError {
                message: format!("unsupported config version: {}", config.version),
            });
        }

        Self::validate_settings(&config.settings, &mut errors);

        if config.monitors.is_empty() {
            errors.push(ValidationError {
                message: "config must contain at least one monitor".into(),
            });
        }

        for monitor in &config.monitors {
            Self::validate_monitor(monitor, &mut errors);
        }

        errors
    }

    fn validate_settings(settings: &crate::config::models::Settings, errors: &mut Vec<ValidationError>) {
        const VALID_MODIFIERS: &[&str] = &["shift", "ctrl", "control", "alt"];
        if !VALID_MODIFIERS.contains(&settings.activation_modifier.to_lowercase().as_str()) {
            errors.push(ValidationError {
                message: format!(
                    "invalid activation_modifier '{}'; must be one of Shift, Ctrl, Control, Alt",
                    settings.activation_modifier
                ),
            });
        }

        if settings.toggle_hotkey.is_empty() {
            errors.push(ValidationError {
                message: "toggle_hotkey must not be empty".into(),
            });
        }

        const VALID_LOG_LEVELS: &[&str] = &["debug", "info", "warn", "error", "trace"];
        if !VALID_LOG_LEVELS.contains(&settings.log_level.to_lowercase().as_str()) {
            errors.push(ValidationError {
                message: format!(
                    "invalid log_level '{}'; must be one of debug, info, warn, error, trace",
                    settings.log_level
                ),
            });
        }

        if !(0.0..=1.0).contains(&settings.highlight_active_opacity) {
            errors.push(ValidationError {
                message: format!(
                    "highlight_active_opacity {} out of range [0.0, 1.0]",
                    settings.highlight_active_opacity
                ),
            });
        }

        if !(0.0..=1.0).contains(&settings.highlight_inactive_opacity) {
            errors.push(ValidationError {
                message: format!(
                    "highlight_inactive_opacity {} out of range [0.0, 1.0]",
                    settings.highlight_inactive_opacity
                ),
            });
        }

        if settings.auto_reload_config && settings.auto_reload_debounce_ms == 0 {
            errors.push(ValidationError {
                message: "auto_reload_debounce_ms must be > 0 when auto_reload_config is true".into(),
            });
        }
    }

    fn validate_monitor(monitor: &crate::config::models::MonitorConfig, errors: &mut Vec<ValidationError>) {
        if monitor.id.is_empty() {
            errors.push(ValidationError {
                message: "monitor id must not be empty".into(),
            });
        }

        const VALID_MATCH_BY: &[&str] = &["", "primary", "deviceName", "index"];
        if !VALID_MATCH_BY.contains(&monitor.match_by.as_str()) {
            errors.push(ValidationError {
                message: format!(
                    "invalid match_by '{}'; must be one of primary, deviceName, index (or empty)",
                    monitor.match_by
                ),
            });
        }

        if monitor.match_by == "deviceName" && monitor.device_name.is_empty() {
            errors.push(ValidationError {
                message: format!(
                    "device_name must not be empty when match_by is 'deviceName' for monitor '{}'",
                    monitor.id
                ),
            });
        }

        const VALID_COORD_UNITS: &[&str] = &["", "percentage", "pixel"];
        if !VALID_COORD_UNITS.contains(&monitor.coordinate_unit.as_str()) {
            errors.push(ValidationError {
                message: format!(
                    "invalid coordinate_unit '{}'; must be one of percentage, pixel (or empty)",
                    monitor.coordinate_unit
                ),
            });
        }

        if monitor.zones.is_empty() {
            errors.push(ValidationError {
                message: format!("monitor '{}' has no zones defined", monitor.id),
            });
        }

        let mut seen_ids = std::collections::HashSet::new();
        for zone in &monitor.zones {
            if zone.id.is_empty() {
                errors.push(ValidationError {
                    message: format!("zone id must not be empty in monitor '{}'", monitor.id),
                });
            }

            if !seen_ids.insert(&zone.id) {
                errors.push(ValidationError {
                    message: format!(
                        "duplicate zone id '{}' in monitor '{}'",
                        zone.id, monitor.id
                    ),
                });
            }

            if zone.width <= 0.0 || zone.height <= 0.0 {
                errors.push(ValidationError {
                    message: format!(
                        "zone '{}' in monitor '{}' has non-positive dimensions",
                        zone.id, monitor.id
                    ),
                });
            }

            if zone.priority < 0 {
                errors.push(ValidationError {
                    message: format!(
                        "zone '{}' in monitor '{}' has negative priority {}",
                        zone.id, monitor.id, zone.priority
                    ),
                });
            }
        }
    }
}

/// A single validation error or warning.
#[derive(Debug, Clone)]
pub struct ValidationError {
    pub message: String,
}

impl std::fmt::Display for ValidationError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.message)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::models::*;

    fn minimal_config() -> ZonesConfig {
        serde_json::from_str(
            r#"{
                "version": 1,
                "settings": {},
                "monitors": [{
                    "id": "m1",
                    "zones": [{ "id": "z1", "x": 0, "y": 0, "width": 50, "height": 100 }]
                }]
            }"#,
        )
        .unwrap()
    }

    #[test]
    fn valid_config_has_no_errors() {
        let config = minimal_config();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.is_empty());
    }

    #[test]
    fn rejects_wrong_version() {
        let mut config = minimal_config();
        config.version = 99;
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("version")));
    }

    #[test]
    fn rejects_empty_monitors() {
        let mut config = minimal_config();
        config.monitors.clear();
        let errors = ConfigValidator::validate(&config);
        assert!(errors
            .iter()
            .any(|e| e.message.contains("at least one monitor")));
    }

    #[test]
    fn rejects_duplicate_zone_ids() {
        let json = r#"{
            "version": 1,
            "settings": {},
            "monitors": [{
                "id": "m1",
                "zones": [
                    { "id": "dup", "x": 0, "y": 0, "width": 50, "height": 100 },
                    { "id": "dup", "x": 50, "y": 0, "width": 50, "height": 100 }
                ]
            }]
        }"#;
        let config: ZonesConfig = serde_json::from_str(json).unwrap();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("duplicate")));
    }

    #[test]
    fn rejects_invalid_activation_modifier() {
        let mut config = minimal_config();
        config.settings.activation_modifier = "Super".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("activation_modifier")));
    }

    #[test]
    fn accepts_case_insensitive_modifier() {
        let mut config = minimal_config();
        config.settings.activation_modifier = "shift".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.is_empty());
    }

    #[test]
    fn rejects_empty_toggle_hotkey() {
        let mut config = minimal_config();
        config.settings.toggle_hotkey = "".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("toggle_hotkey")));
    }

    #[test]
    fn rejects_invalid_log_level() {
        let mut config = minimal_config();
        config.settings.log_level = "verbose".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("log_level")));
    }

    #[test]
    fn accepts_valid_log_levels() {
        for level in &["debug", "Info", "WARN", "error", "trace"] {
            let mut config = minimal_config();
            config.settings.log_level = level.to_string();
            let errors = ConfigValidator::validate(&config);
            assert!(errors.is_empty(), "expected no errors for log_level '{level}'");
        }
    }

    #[test]
    fn rejects_opacity_out_of_range() {
        let mut config = minimal_config();
        config.settings.highlight_active_opacity = 1.5;
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("highlight_active_opacity")));

        let mut config = minimal_config();
        config.settings.highlight_inactive_opacity = -0.1;
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("highlight_inactive_opacity")));
    }

    #[test]
    fn rejects_zero_debounce_when_auto_reload() {
        let mut config = minimal_config();
        config.settings.auto_reload_config = true;
        config.settings.auto_reload_debounce_ms = 0;
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("auto_reload_debounce_ms")));
    }

    #[test]
    fn rejects_invalid_match_by() {
        let mut config = minimal_config();
        config.monitors[0].match_by = "hostname".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("match_by")));
    }

    #[test]
    fn rejects_empty_device_name_for_device_name_match() {
        let mut config = minimal_config();
        config.monitors[0].match_by = "deviceName".to_string();
        config.monitors[0].device_name = "".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("device_name")));
    }

    #[test]
    fn rejects_empty_monitor_id() {
        let mut config = minimal_config();
        config.monitors[0].id = "".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("monitor") && e.message.contains("id")));
    }

    #[test]
    fn rejects_invalid_coordinate_unit() {
        let mut config = minimal_config();
        config.monitors[0].coordinate_unit = "em".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("coordinate_unit")));
    }

    #[test]
    fn rejects_empty_zone_id() {
        let mut config = minimal_config();
        config.monitors[0].zones[0].id = "".to_string();
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("zone") && e.message.contains("id")));
    }

    #[test]
    fn rejects_negative_zone_priority() {
        let mut config = minimal_config();
        config.monitors[0].zones[0].priority = -1;
        let errors = ConfigValidator::validate(&config);
        assert!(errors.iter().any(|e| e.message.contains("priority")));
    }
}
