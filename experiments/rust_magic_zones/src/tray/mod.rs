/// Tray module — system tray icon for toggling snapping and reloading config.
///
/// Windows-only: uses `Shell_NotifyIconW`.
///
/// The [`TrayCommand`] enum is available on all platforms so the engine can
/// reference it in cross-platform code and tests.

#[cfg(windows)]
pub mod manager;

#[cfg(windows)]
pub use manager::TrayManager;

/// Commands the tray menu can emit.
///
/// This enum is deliberately **not** behind `cfg(windows)` so that the
/// engine and other cross-platform modules can match on it without
/// conditional compilation.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum TrayCommand {
    /// Toggle snapping on/off.
    ToggleSnapping,
    /// Reload configuration from disk.
    ReloadConfig,
    /// Exit the application.
    Exit,
}
