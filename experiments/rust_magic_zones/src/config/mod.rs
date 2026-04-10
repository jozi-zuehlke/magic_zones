/// Configuration module — loading, validation, and parsing of `zones.json`.

pub mod loader;
pub mod models;
pub mod settings_parser;
pub mod validator;
pub mod watcher;

pub use loader::{save, ConfigLoadError, ConfigLoader, LoadResult};
pub use models::{MonitorConfig, Settings, ZoneDefinition, ZonesConfig};
pub use validator::ConfigValidator;
pub use watcher::ConfigFileWatcher;
