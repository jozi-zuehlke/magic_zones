/// Filters windows to determine which are eligible for zone snapping.
///
/// Excludes system windows, tool windows, cloaked windows, etc.

use std::sync::Arc;

use crate::platform::WindowManager;

/// Windows style constants used for filtering.
const WS_EX_TOOLWINDOW: i32 = 0x00000080;
const WS_EX_NOACTIVATE: i32 = 0x08000000;
const GWL_EXSTYLE: i32 = -20;

/// Class names that should be excluded from snapping.
const EXCLUDED_CLASSES: &[&str] = &[
    "Shell_TrayWnd",
    "Shell_SecondaryTrayWnd",
    "Progman",
    "WorkerW",
    "Windows.UI.Core.CoreWindow",
];

/// Determines whether a window is a valid snap target.
pub struct WindowFilter {
    window_manager: Arc<dyn WindowManager>,
}

impl WindowFilter {
    /// Create a new filter with the given window manager.
    pub fn new(window_manager: Arc<dyn WindowManager>) -> Self {
        Self { window_manager }
    }

    /// Returns `true` if the window should be considered for zone snapping.
    pub fn is_snappable(&self, hwnd: isize) -> bool {
        if !self.window_manager.is_window_visible(hwnd) {
            return false;
        }

        let ex_style = self.window_manager.get_window_long(hwnd, GWL_EXSTYLE);
        if ex_style & WS_EX_TOOLWINDOW != 0 {
            return false;
        }
        if ex_style & WS_EX_NOACTIVATE != 0 {
            return false;
        }

        let class_name = self.window_manager.get_class_name(hwnd);
        if EXCLUDED_CLASSES.contains(&class_name.as_str()) {
            return false;
        }

        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::platform::mock::MockWindowManager;

    #[test]
    fn visible_normal_window_is_snappable() {
        let wm = Arc::new(MockWindowManager::new());
        let filter = WindowFilter::new(wm);
        assert!(filter.is_snappable(1));
    }

    #[test]
    fn invisible_window_is_not_snappable() {
        let wm = Arc::new(MockWindowManager::new());
        wm.windows_visible
            .store(false, std::sync::atomic::Ordering::Relaxed);
        let filter = WindowFilter::new(wm);
        assert!(!filter.is_snappable(1));
    }

    #[test]
    fn tool_window_is_not_snappable() {
        let wm = Arc::new(MockWindowManager::new());
        *wm.window_long_value.lock() = WS_EX_TOOLWINDOW;
        let filter = WindowFilter::new(wm);
        assert!(!filter.is_snappable(1));
    }

    #[test]
    fn excluded_class_is_not_snappable() {
        let wm = Arc::new(MockWindowManager::new());
        *wm.class_name.lock() = "Shell_TrayWnd".to_string();
        let filter = WindowFilter::new(wm);
        assert!(!filter.is_snappable(1));
    }
}
