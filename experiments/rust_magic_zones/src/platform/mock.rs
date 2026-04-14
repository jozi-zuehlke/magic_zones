/// Mock implementations of platform traits for testing on non-Windows hosts.
///
/// These mocks are always compiled (not behind `#[cfg(test)]`) so they serve
/// as the Linux fallback and are available for integration tests.

use std::sync::atomic::{AtomicBool, AtomicIsize, Ordering};

use parking_lot::Mutex;

use crate::engine::types::Rect;
use crate::platform::{KeyboardState, MonitorInfo, MonitorProvider, WindowManager};

/// Configurable mock for [`WindowManager`].
pub struct MockWindowManager {
    /// Rectangles returned by `get_window_rect`, keyed by hwnd.
    pub window_rects: Mutex<std::collections::HashMap<isize, Rect>>,
    /// Rectangles returned by `get_extended_frame_bounds`, keyed by hwnd.
    pub frame_bounds: Mutex<std::collections::HashMap<isize, Rect>>,
    /// The cursor position returned by `get_cursor_pos`.
    pub cursor_pos: Mutex<Option<(i32, i32)>>,
    /// The handle returned by `get_foreground_window`.
    pub foreground_hwnd: AtomicIsize,
    /// Whether windows report as visible.
    pub windows_visible: AtomicBool,
    /// Window style bits returned by `get_window_long`.
    pub window_long_value: Mutex<i32>,
    /// Class name returned for any window.
    pub class_name: Mutex<String>,
    /// Window text returned for any window.
    pub window_text: Mutex<String>,
    /// Record of `set_window_pos` calls for assertion.
    pub set_pos_calls: Mutex<Vec<(isize, i32, i32, i32, i32, u32)>>,
}

impl MockWindowManager {
    /// Create a mock with sensible defaults.
    pub fn new() -> Self {
        Self {
            window_rects: Mutex::new(std::collections::HashMap::new()),
            frame_bounds: Mutex::new(std::collections::HashMap::new()),
            cursor_pos: Mutex::new(Some((0, 0))),
            foreground_hwnd: AtomicIsize::new(0),
            windows_visible: AtomicBool::new(true),
            window_long_value: Mutex::new(0),
            class_name: Mutex::new(String::new()),
            window_text: Mutex::new(String::new()),
            set_pos_calls: Mutex::new(Vec::new()),
        }
    }
}

impl Default for MockWindowManager {
    fn default() -> Self {
        Self::new()
    }
}

impl WindowManager for MockWindowManager {
    fn set_window_pos(&self, hwnd: isize, x: i32, y: i32, width: i32, height: i32, flags: u32) {
        self.set_pos_calls
            .lock()
            .push((hwnd, x, y, width, height, flags));
    }

    fn get_window_rect(&self, hwnd: isize) -> Option<Rect> {
        self.window_rects.lock().get(&hwnd).copied()
    }

    fn get_extended_frame_bounds(&self, hwnd: isize) -> Option<Rect> {
        self.frame_bounds.lock().get(&hwnd).copied()
    }

    fn is_zoomed(&self, _hwnd: isize) -> bool {
        false
    }

    fn show_window(&self, _hwnd: isize, _cmd: i32) {}

    fn get_cursor_pos(&self) -> Option<(i32, i32)> {
        *self.cursor_pos.lock()
    }

    fn get_foreground_window(&self) -> isize {
        self.foreground_hwnd.load(Ordering::Relaxed)
    }

    fn is_window_visible(&self, _hwnd: isize) -> bool {
        self.windows_visible.load(Ordering::Relaxed)
    }

    fn get_window_long(&self, _hwnd: isize, _index: i32) -> i32 {
        *self.window_long_value.lock()
    }

    fn get_class_name(&self, _hwnd: isize) -> String {
        self.class_name.lock().clone()
    }

    fn get_window_text(&self, _hwnd: isize) -> String {
        self.window_text.lock().clone()
    }
}

/// Configurable mock for [`KeyboardState`].
pub struct MockKeyboardState {
    /// Set of virtual-key codes currently considered "pressed".
    pub pressed_keys: Mutex<std::collections::HashSet<u16>>,
}

impl MockKeyboardState {
    pub fn new() -> Self {
        Self {
            pressed_keys: Mutex::new(std::collections::HashSet::new()),
        }
    }
}

impl Default for MockKeyboardState {
    fn default() -> Self {
        Self::new()
    }
}

impl KeyboardState for MockKeyboardState {
    fn is_key_pressed(&self, vk: u16) -> bool {
        self.pressed_keys.lock().contains(&vk)
    }

    fn get_async_key_state(&self, vk: i32) -> i16 {
        if self.pressed_keys.lock().contains(&(vk as u16)) {
            -1 // high-order bit set = key is down
        } else {
            0
        }
    }
}

/// Configurable mock for [`MonitorProvider`].
pub struct MockMonitorProvider {
    /// Monitors to return.
    pub monitors: Mutex<Vec<MonitorInfo>>,
}

impl MockMonitorProvider {
    pub fn new() -> Self {
        Self {
            monitors: Mutex::new(Vec::new()),
        }
    }

    /// Helper: add a single monitor with the given work area.
    pub fn with_monitor(self, device_name: &str, index: u32, work_area: Rect) -> Self {
        self.monitors.lock().push(MonitorInfo {
            device_name: device_name.to_string(),
            index,
            work_area,
        });
        self
    }
}

impl Default for MockMonitorProvider {
    fn default() -> Self {
        Self::new()
    }
}

impl MonitorProvider for MockMonitorProvider {
    fn get_monitor_info(&self) -> Vec<MonitorInfo> {
        self.monitors.lock().clone()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn mock_window_manager_records_set_pos() {
        let wm = MockWindowManager::new();
        wm.set_window_pos(42, 10, 20, 800, 600, 0);
        let calls = wm.set_pos_calls.lock();
        assert_eq!(calls.len(), 1);
        assert_eq!(calls[0], (42, 10, 20, 800, 600, 0));
    }

    #[test]
    fn mock_keyboard_state_tracks_keys() {
        let kb = MockKeyboardState::new();
        assert!(!kb.is_key_pressed(0x10)); // VK_SHIFT
        kb.pressed_keys.lock().insert(0x10);
        assert!(kb.is_key_pressed(0x10));
    }

    #[test]
    fn mock_monitor_provider_returns_monitors() {
        let mp = MockMonitorProvider::new()
            .with_monitor("DISPLAY1", 0, Rect::new(0, 0, 1920, 1080));
        let monitors = mp.get_monitor_info();
        assert_eq!(monitors.len(), 1);
        assert_eq!(monitors[0].device_name, "DISPLAY1");
    }
}
