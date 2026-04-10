/// Overlay module — transparent overlay window for zone highlighting.
///
/// Windows-only: uses layered windows with per-pixel alpha for zone preview.

#[cfg(windows)]
pub mod window;

#[cfg(windows)]
pub use window::OverlayWindow;
