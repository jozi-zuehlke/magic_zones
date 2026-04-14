/// Configuration module — loading, validation, and parsing of `zones.json`.

pub mod loader;
pub mod models;
pub mod settings_parser;
pub mod validator;
pub mod watcher;

pub use loader::{ConfigLoader, LoadResult};
pub use models::{Settings, ZonesConfig};
pub use watcher::ConfigFileWatcher;
