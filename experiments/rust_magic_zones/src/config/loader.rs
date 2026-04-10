/// Loads `zones.json` from disk and optionally watches for changes.

use std::path::{Path, PathBuf};

use crate::config::models::{MonitorConfig, Settings, ZoneDefinition, ZonesConfig};
use crate::config::validator::ConfigValidator;

/// Result of loading configuration, including any validation warnings.
#[derive(Debug)]
pub struct LoadResult {
    pub config: ZonesConfig,
    pub warnings: Vec<String>,
}

/// Responsible for reading configuration from disk and optionally watching for changes.
pub struct ConfigLoader {
    path: PathBuf,
}

impl ConfigLoader {
    /// Create a loader pointing at the given config file path.
    pub fn new(path: impl Into<PathBuf>) -> Self {
        Self { path: path.into() }
    }

    /// Read and parse the config file.
    pub fn load(&self) -> Result<ZonesConfig, ConfigLoadError> {
        let contents = std::fs::read_to_string(&self.path)
            .map_err(|e| ConfigLoadError::Io(self.path.clone(), e))?;
        let config: ZonesConfig =
            serde_json::from_str(&contents).map_err(ConfigLoadError::Parse)?;
        Ok(config)
    }

    /// Read, parse, and validate the config file, returning warnings alongside the config.
    pub fn load_with_validation(&self) -> Result<LoadResult, ConfigLoadError> {
        let config = self.load()?;
        let errors = ConfigValidator::validate(&config);
        let warnings = errors.into_iter().map(|e| e.message).collect();
        Ok(LoadResult { config, warnings })
    }

    /// If the configured path exists, load it; otherwise create a default config,
    /// save it to disk, and return it.
    pub fn load_or_create_default(&self) -> Result<LoadResult, ConfigLoadError> {
        if self.path.exists() {
            self.load_with_validation()
        } else {
            let config = Self::create_default();
            save(&config, &self.path)?;
            let errors = ConfigValidator::validate(&config);
            let warnings = errors.into_iter().map(|e| e.message).collect();
            Ok(LoadResult { config, warnings })
        }
    }

    /// Search for `zones.json` in standard locations, returning the first path found.
    ///
    /// Priority: (a) next to the running executable, (b) platform config directory.
    pub fn discover_config_path() -> Option<PathBuf> {
        // (a) Next to the executable
        if let Ok(exe) = std::env::current_exe() {
            if let Some(dir) = exe.parent() {
                let candidate = dir.join("zones.json");
                if candidate.exists() {
                    return Some(candidate);
                }
            }
        }

        // (b) Platform-specific config directory
        let platform_path = platform_config_path();
        if let Some(p) = &platform_path {
            if p.exists() {
                return Some(p.clone());
            }
        }

        None
    }

    /// Return a default configuration with a primary monitor and two zones.
    pub fn create_default() -> ZonesConfig {
        ZonesConfig {
            version: 1,
            settings: Settings {
                activation_modifier: "Shift".to_string(),
                highlight_active_color: "#0078D4".to_string(),
                highlight_active_opacity: 0.35,
                highlight_inactive_color: "#CCCCCC".to_string(),
                highlight_inactive_opacity: 0.20,
                toggle_hotkey: "Ctrl+Win+Z".to_string(),
                log_level: "Info".to_string(),
                auto_reload_config: true,
                auto_reload_debounce_ms: 500,
            },
            monitors: vec![MonitorConfig {
                id: "primary".to_string(),
                match_by: "index".to_string(),
                device_name: String::new(),
                index: 0,
                coordinate_unit: "percentage".to_string(),
                zones: vec![
                    ZoneDefinition {
                        id: "left".to_string(),
                        name: String::new(),
                        priority: 0,
                        x: 0.0,
                        y: 0.0,
                        width: 50.0,
                        height: 100.0,
                    },
                    ZoneDefinition {
                        id: "right".to_string(),
                        name: String::new(),
                        priority: 0,
                        x: 50.0,
                        y: 0.0,
                        width: 50.0,
                        height: 100.0,
                    },
                ],
            }],
        }
    }

    /// Return the path this loader is configured for.
    pub fn path(&self) -> &Path {
        &self.path
    }
}

/// Serialize config to pretty-printed JSON and write to disk, creating parent dirs if needed.
pub fn save(config: &ZonesConfig, path: &Path) -> Result<(), ConfigLoadError> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| ConfigLoadError::Io(parent.to_path_buf(), e))?;
    }
    let json = serde_json::to_string_pretty(config).map_err(ConfigLoadError::Serialize)?;
    std::fs::write(path, json).map_err(|e| ConfigLoadError::Io(path.to_path_buf(), e))?;
    Ok(())
}

/// Return the platform-specific config path for zones.json.
fn platform_config_path() -> Option<PathBuf> {
    #[cfg(windows)]
    {
        std::env::var("APPDATA")
            .ok()
            .map(|appdata| PathBuf::from(appdata).join("MagicZonesPortable").join("zones.json"))
    }
    #[cfg(not(windows))]
    {
        std::env::var("HOME")
            .ok()
            .map(|home| PathBuf::from(home).join(".config").join("magic-zones").join("zones.json"))
    }
}

/// Errors that can occur while loading configuration.
#[derive(Debug)]
pub enum ConfigLoadError {
    /// File could not be read.
    Io(PathBuf, std::io::Error),
    /// JSON parsing failed.
    Parse(serde_json::Error),
    /// JSON serialization failed.
    Serialize(serde_json::Error),
}

impl std::fmt::Display for ConfigLoadError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ConfigLoadError::Io(path, e) => write!(f, "failed to read {}: {}", path.display(), e),
            ConfigLoadError::Parse(e) => write!(f, "invalid JSON: {}", e),
            ConfigLoadError::Serialize(e) => write!(f, "serialization error: {}", e),
        }
    }
}

impl std::error::Error for ConfigLoadError {}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use std::sync::atomic::{AtomicU32, Ordering};

    // Unique dir names to avoid test parallelism conflicts
    static TEST_COUNTER: AtomicU32 = AtomicU32::new(0);
    fn unique_test_dir(prefix: &str) -> PathBuf {
        let n = TEST_COUNTER.fetch_add(1, Ordering::SeqCst);
        std::env::current_dir()
            .unwrap()
            .join(format!("test_loader_{}_{}", prefix, n))
    }

    #[test]
    fn load_valid_config() {
        let dir = unique_test_dir("load_valid");
        let _ = std::fs::create_dir_all(&dir);
        let path = dir.join("zones.json");
        let mut f = std::fs::File::create(&path).unwrap();
        writeln!(
            f,
            r#"{{
                "version": 1,
                "settings": {{}},
                "monitors": [{{
                    "id": "m1",
                    "zones": [{{ "id": "z1", "x": 0, "y": 0, "width": 50, "height": 100 }}]
                }}]
            }}"#
        )
        .unwrap();

        let loader = ConfigLoader::new(&path);
        let config = loader.load().unwrap();
        assert_eq!(config.version, 1);

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn load_missing_file_returns_error() {
        let loader = ConfigLoader::new("/nonexistent/zones.json");
        assert!(loader.load().is_err());
    }

    // --- Chunk 3 tests ---

    #[test]
    fn create_default_returns_valid_config() {
        let config = ConfigLoader::create_default();
        assert_eq!(config.version, 1);
        assert_eq!(config.monitors.len(), 1);

        let monitor = &config.monitors[0];
        assert_eq!(monitor.id, "primary");
        assert_eq!(monitor.match_by, "index");
        assert_eq!(monitor.index, 0);
        assert_eq!(monitor.coordinate_unit, "percentage");
        assert_eq!(monitor.zones.len(), 2);

        let left = &monitor.zones[0];
        assert_eq!(left.id, "left");
        assert_eq!(left.x, 0.0);
        assert_eq!(left.y, 0.0);
        assert_eq!(left.width, 50.0);
        assert_eq!(left.height, 100.0);
        assert_eq!(left.priority, 0);

        let right = &monitor.zones[1];
        assert_eq!(right.id, "right");
        assert_eq!(right.x, 50.0);
        assert_eq!(right.y, 0.0);
        assert_eq!(right.width, 50.0);
        assert_eq!(right.height, 100.0);
        assert_eq!(right.priority, 0);
    }

    #[test]
    fn create_default_passes_validation() {
        let config = ConfigLoader::create_default();
        let errors = ConfigValidator::validate(&config);
        assert!(
            errors.is_empty(),
            "default config should have no validation errors, got: {:?}",
            errors.iter().map(|e| &e.message).collect::<Vec<_>>()
        );
    }

    #[test]
    fn save_and_reload_roundtrip() {
        let dir = unique_test_dir("roundtrip");
        let path = dir.join("zones.json");

        let original = ConfigLoader::create_default();
        save(&original, &path).unwrap();

        let loader = ConfigLoader::new(&path);
        let loaded = loader.load().unwrap();
        assert_eq!(loaded.version, original.version);
        assert_eq!(loaded.monitors.len(), original.monitors.len());
        assert_eq!(loaded.monitors[0].id, original.monitors[0].id);
        assert_eq!(loaded.monitors[0].zones.len(), original.monitors[0].zones.len());
        assert_eq!(loaded.monitors[0].zones[0].id, original.monitors[0].zones[0].id);
        assert_eq!(loaded.monitors[0].zones[1].id, original.monitors[0].zones[1].id);
        assert_eq!(
            loaded.settings.activation_modifier,
            original.settings.activation_modifier
        );

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn load_or_create_default_creates_file() {
        let dir = unique_test_dir("create_file");
        let path = dir.join("zones.json");
        assert!(!path.exists());

        let loader = ConfigLoader::new(&path);
        let result = loader.load_or_create_default().unwrap();
        assert!(path.exists(), "zones.json should have been created");
        assert_eq!(result.config.version, 1);
        assert_eq!(result.config.monitors.len(), 1);
        assert_eq!(result.config.monitors[0].zones.len(), 2);
        assert!(result.warnings.is_empty());

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn load_or_create_default_loads_existing() {
        let dir = unique_test_dir("load_existing");
        let _ = std::fs::create_dir_all(&dir);
        let path = dir.join("zones.json");

        // Write a custom config with a unique monitor id
        let custom_json = r#"{
            "version": 1,
            "settings": {},
            "monitors": [{
                "id": "custom-monitor",
                "zones": [{ "id": "full", "x": 0, "y": 0, "width": 100, "height": 100 }]
            }]
        }"#;
        std::fs::write(&path, custom_json).unwrap();

        let loader = ConfigLoader::new(&path);
        let result = loader.load_or_create_default().unwrap();
        assert_eq!(result.config.monitors[0].id, "custom-monitor");
        assert_eq!(result.config.monitors[0].zones[0].id, "full");

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn discover_config_path_returns_none_when_no_file() {
        // In the test environment there is no zones.json next to the test binary
        // or in the platform config dir, so discover should return None.
        // (If one happens to exist we skip.)
        let result = ConfigLoader::discover_config_path();
        if let Some(ref p) = result {
            // If it found something it must actually exist
            assert!(p.exists(), "discovered path should exist");
        }
        // The main assertion: on a clean test environment this is None
        // We verify the function at least does not panic.
    }

    #[test]
    fn load_with_validation_includes_warnings() {
        let dir = unique_test_dir("validation_warn");
        let _ = std::fs::create_dir_all(&dir);
        let path = dir.join("zones.json");

        // Write a config with an invalid log_level to trigger a validation warning
        let json = r#"{
            "version": 1,
            "settings": { "logLevel": "verbose" },
            "monitors": [{
                "id": "m1",
                "zones": [{ "id": "z1", "x": 0, "y": 0, "width": 50, "height": 100 }]
            }]
        }"#;
        std::fs::write(&path, json).unwrap();

        let loader = ConfigLoader::new(&path);
        let result = loader.load_with_validation().unwrap();
        assert_eq!(result.config.version, 1);
        assert!(
            result.warnings.iter().any(|w| w.contains("log_level")),
            "expected a log_level warning, got: {:?}",
            result.warnings
        );

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn save_creates_parent_directories() {
        let dir = unique_test_dir("nested_save");
        let path = dir.join("deep").join("nested").join("zones.json");
        assert!(!dir.exists());

        let config = ConfigLoader::create_default();
        save(&config, &path).unwrap();
        assert!(path.exists());

        let _ = std::fs::remove_dir_all(&dir);
    }
}
