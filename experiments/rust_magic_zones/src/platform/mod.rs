/// Platform abstraction layer.
///
/// Defines traits that abstract Win32 API calls so the engine and other modules
/// can be compiled and tested on non-Windows platforms. Real implementations
/// live in `windows.rs`; mock implementations live in `mock.rs`.

#[cfg(windows)]
pub mod windows;

#[cfg(test)]
pub mod mock;

use crate::engine::types::Rect;

/// Information about a physical monitor.
#[derive(Debug, Clone)]
pub struct MonitorInfo {
    /// Display device name (e.g. `\\.\DISPLAY1`).
    pub device_name: String,
    /// Zero-based monitor index.
    pub index: u32,
    /// Usable work area (excludes taskbar).
    pub work_area: Rect,
}

/// Trait abstracting window management Win32 APIs.
pub trait WindowManager: Send + Sync {
    /// Move and resize a window.
    fn set_window_pos(&self, hwnd: isize, x: i32, y: i32, width: i32, height: i32, flags: u32);
    /// Get the outer bounding rectangle of a window.
    fn get_window_rect(&self, hwnd: isize) -> Option<Rect>;
    /// Get the visible frame bounds (excluding invisible borders) via DWM.
    fn get_extended_frame_bounds(&self, hwnd: isize) -> Option<Rect>;
    /// Check whether a window is maximized.
    fn is_zoomed(&self, hwnd: isize) -> bool;
    /// Show or hide a window with the given command.
    fn show_window(&self, hwnd: isize, cmd: i32);
    /// Get the current cursor position in screen coordinates.
    fn get_cursor_pos(&self) -> Option<(i32, i32)>;
    /// Get the handle of the foreground window.
    #[allow(dead_code)]
    fn get_foreground_window(&self) -> isize;
    /// Check whether a window is visible.
    fn is_window_visible(&self, hwnd: isize) -> bool;
    /// Retrieve window style / extended style bits.
    fn get_window_long(&self, hwnd: isize, index: i32) -> i32;
    /// Get the class name of a window.
    fn get_class_name(&self, hwnd: isize) -> String;
    /// Get the title text of a window.
    #[allow(dead_code)]
    fn get_window_text(&self, hwnd: isize) -> String;
}

/// Trait for querying keyboard state.
pub trait KeyboardState: Send + Sync {
    /// Returns true if the virtual-key is currently held down.
    fn is_key_pressed(&self, vk: u16) -> bool;
    /// Raw async key state query (matches Win32 `GetAsyncKeyState` semantics).
    #[allow(dead_code)]
    fn get_async_key_state(&self, vk: i32) -> i16;
}

/// Trait for enumerating monitors and their geometry.
pub trait MonitorProvider: Send + Sync {
    /// Return information about all connected monitors.
    fn get_monitor_info(&self) -> Vec<MonitorInfo>;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn monitor_info_is_constructable() {
        let mi = MonitorInfo {
            device_name: "DISPLAY1".into(),
            index: 0,
            work_area: Rect::new(0, 0, 1920, 1080),
        };
        assert_eq!(mi.index, 0);
    }
}
