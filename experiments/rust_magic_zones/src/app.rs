/// Top-level application coordinator.
///
/// Wires together configuration loading, platform services, the snap engine,
/// hooks, overlay, and tray into the main event loop.

use std::path::PathBuf;
use std::sync::Arc;

use crate::config::ZonesConfig;
use crate::engine::modifier_key_state_tracker::{ActivationModifier, ModifierKeyStateTracker};
use crate::engine::snap_engine::SnapEngine;
use crate::engine::types::{Rect, ZoneRenderInfo};
use crate::engine::window_filter::WindowFilter;
use crate::hooks::KeyboardMsg;
use crate::platform::{KeyboardState, MonitorProvider, WindowManager};

/// Result of a cursor-tick poll during an active drag.
#[derive(Debug, Clone, PartialEq)]
pub enum CursorTickResult {
    /// No drag in progress.
    Idle,
    /// Modifier was just pressed — snapping overlay should appear.
    SnappingActivated,
    /// Modifier was just released — snapping overlay should hide.
    SnappingDeactivated,
    /// The hovered zone changed (or became None).
    ZoneChanged { active_index: Option<usize> },
    /// Snapping is active but the zone didn't change.
    NoChange,
}

/// Top-level application state and coordination.
pub struct App {
    config: ZonesConfig,
    config_path: PathBuf,
    engine: SnapEngine,
    window_filter: WindowFilter,
    modifier_tracker: ModifierKeyStateTracker,
    window_manager: Arc<dyn WindowManager>,
    keyboard_state: Arc<dyn KeyboardState>,
    enabled: bool,
    tracking_drag: bool,
    snapping_active: bool,
    tracked_hwnd: Option<isize>,
}

fn parse_modifier(s: &str) -> ActivationModifier {
    match s.to_lowercase().as_str() {
        "control" | "ctrl" => ActivationModifier::Control,
        "alt" => ActivationModifier::Alt,
        _ => ActivationModifier::Shift,
    }
}

impl App {
    /// Create the application with the given config and platform services.
    pub fn new(
        config: ZonesConfig,
        config_path: PathBuf,
        window_manager: Arc<dyn WindowManager>,
        keyboard_state: Arc<dyn KeyboardState>,
        monitor_provider: Arc<dyn MonitorProvider>,
    ) -> Self {
        let modifier = parse_modifier(&config.settings.activation_modifier);
        let modifier_tracker = ModifierKeyStateTracker::new(keyboard_state.clone(), modifier);
        let window_filter = WindowFilter::new(window_manager.clone());
        let mut engine = SnapEngine::new(window_manager.clone(), monitor_provider);
        engine.resolve_zones(&config);

        Self {
            config,
            config_path,
            engine,
            window_filter,
            modifier_tracker,
            window_manager,
            keyboard_state,
            enabled: true,
            tracking_drag: false,
            snapping_active: false,
            tracked_hwnd: None,
        }
    }

    /// Start tracking a window drag.
    pub fn on_move_started(&mut self, hwnd: isize) {
        if !self.enabled || self.tracking_drag {
            return;
        }
        if !self.window_filter.is_snappable(hwnd) {
            return;
        }
        self.tracking_drag = true;
        self.tracked_hwnd = Some(hwnd);
        self.engine.on_drag_start(hwnd);
    }

    /// End tracking. Returns `(hwnd, zone_bounds)` if a snap should be applied.
    pub fn on_move_ended(&mut self) -> Option<(isize, Rect)> {
        if !self.tracking_drag {
            return None;
        }

        let hwnd = self.tracked_hwnd?;
        let result = if self.snapping_active {
            self.engine
                .get_active_zone()
                .map(|z| (hwnd, z.bounds))
        } else {
            None
        };

        self.engine.cancel_drag();
        self.tracking_drag = false;
        self.snapping_active = false;
        self.tracked_hwnd = None;

        result
    }

    /// Poll cursor and modifier state. Called on a timer tick.
    pub fn on_cursor_tick(&mut self) -> CursorTickResult {
        if !self.tracking_drag {
            return CursorTickResult::Idle;
        }

        let modifier_active = self.modifier_tracker.is_modifier_active();

        if modifier_active && !self.snapping_active {
            self.snapping_active = true;
            self.engine.resolve_zones(&self.config.clone());
            return CursorTickResult::SnappingActivated;
        }

        if !modifier_active && self.snapping_active {
            self.snapping_active = false;
            return CursorTickResult::SnappingDeactivated;
        }

        if self.snapping_active {
            let prev = self.engine.active_zone_index();
            if let Some((x, y)) = self.window_manager.get_cursor_pos() {
                self.engine.on_cursor_move(x, y);
            }
            let curr = self.engine.active_zone_index();
            if curr != prev {
                return CursorTickResult::ZoneChanged {
                    active_index: curr,
                };
            }
            return CursorTickResult::NoChange;
        }

        CursorTickResult::NoChange
    }

    /// Process a keyboard hook message.
    ///
    /// If Escape is pressed while a drag is being tracked, the drag is cancelled
    /// and this returns `true` so the caller can hide the overlay.
    pub fn on_key_event(&mut self, msg: &KeyboardMsg) -> bool {
        if let KeyboardMsg::KeyDown(0x1B) = msg {
            if self.tracking_drag {
                self.engine.cancel_drag();
                self.tracking_drag = false;
                self.snapping_active = false;
                self.tracked_hwnd = None;
                return true;
            }
        }
        false
    }

    /// Toggle enabled state. Returns the new state.
    pub fn on_hotkey(&mut self) -> bool {
        self.enabled = !self.enabled;
        if !self.enabled && self.tracking_drag {
            self.engine.cancel_drag();
            self.tracking_drag = false;
            self.snapping_active = false;
            self.tracked_hwnd = None;
        }
        self.enabled
    }

    /// Reload with a new configuration.
    pub fn on_config_changed(&mut self, new_config: ZonesConfig) {
        self.config = new_config;
        if self.tracking_drag && self.snapping_active {
            self.engine.resolve_zones(&self.config.clone());
        }
    }

    /// Check whether snapping is currently enabled.
    pub fn is_enabled(&self) -> bool {
        self.enabled
    }

    /// Check whether a drag is being tracked.
    pub fn is_tracking(&self) -> bool {
        self.tracking_drag
    }

    /// Check whether the snap overlay is active.
    pub fn is_snapping(&self) -> bool {
        self.snapping_active
    }

    /// Get the config file path.
    pub fn config_path(&self) -> &PathBuf {
        &self.config_path
    }

    /// Delegate zone render info to the engine.
    pub fn get_zone_render_info(&self) -> (Vec<ZoneRenderInfo>, Option<usize>) {
        self.engine.get_zone_render_info()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::engine::types::Rect;
    use crate::platform::mock::{MockKeyboardState, MockMonitorProvider, MockWindowManager};

    /// VK_LSHIFT used in tests.
    const VK_LSHIFT: u16 = 0xA0;

    fn two_zone_config() -> ZonesConfig {
        serde_json::from_str(
            r#"{
                "version": 1,
                "settings": {},
                "monitors": [{
                    "id": "m1",
                    "matchBy": "index",
                    "index": 0,
                    "zones": [
                        { "id": "left", "x": 0, "y": 0, "width": 50, "height": 100 },
                        { "id": "right", "x": 50, "y": 0, "width": 50, "height": 100 }
                    ]
                }]
            }"#,
        )
        .unwrap()
    }

    struct TestHarness {
        app: App,
        wm: Arc<MockWindowManager>,
        kb: Arc<MockKeyboardState>,
    }

    fn create_test_app() -> TestHarness {
        let wm = Arc::new(MockWindowManager::new());
        let kb = Arc::new(MockKeyboardState::new());
        let mp = Arc::new(
            MockMonitorProvider::new()
                .with_monitor("DISPLAY1", 0, Rect::new(0, 0, 1920, 1080)),
        );
        let app = App::new(
            two_zone_config(),
            PathBuf::from("zones.json"),
            wm.clone(),
            kb.clone(),
            mp,
        );
        TestHarness { app, wm, kb }
    }

    #[test]
    fn app_starts_enabled() {
        let h = create_test_app();
        assert!(h.app.is_enabled());
        assert!(!h.app.is_tracking());
        assert!(!h.app.is_snapping());
    }

    #[test]
    fn on_move_started_begins_tracking() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        assert!(h.app.is_tracking());
    }

    #[test]
    fn on_move_started_ignores_when_disabled() {
        let mut h = create_test_app();
        h.app.on_hotkey(); // disable
        assert!(!h.app.is_enabled());
        h.app.on_move_started(100);
        assert!(!h.app.is_tracking());
    }

    #[test]
    fn on_move_started_ignores_non_snappable() {
        let mut h = create_test_app();
        // Set WS_EX_TOOLWINDOW style
        *h.wm.window_long_value.lock() = 0x80;
        h.app.on_move_started(100);
        assert!(!h.app.is_tracking());
    }

    #[test]
    fn on_move_ended_returns_zone_when_snapping() {
        let mut h = create_test_app();
        // Start drag
        h.app.on_move_started(100);
        // Activate modifier
        h.kb.pressed_keys.lock().insert(VK_LSHIFT);
        let result = h.app.on_cursor_tick();
        assert_eq!(result, CursorTickResult::SnappingActivated);
        // Move cursor into left zone
        *h.wm.cursor_pos.lock() = Some((100, 540));
        h.app.on_cursor_tick();
        // End drag — should snap
        let snap = h.app.on_move_ended();
        assert!(snap.is_some());
        let (hwnd, bounds) = snap.unwrap();
        assert_eq!(hwnd, 100);
        // Left zone: 0..50% of 1920 = 0..960, 0..100% of 1080 = 0..1080
        assert_eq!(bounds, Rect::new(0, 0, 960, 1080));
    }

    #[test]
    fn on_move_ended_returns_none_when_not_snapping() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        // No modifier pressed — snapping never activated
        let snap = h.app.on_move_ended();
        assert!(snap.is_none());
    }

    #[test]
    fn on_cursor_tick_activates_snapping_when_modifier_pressed() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        h.kb.pressed_keys.lock().insert(VK_LSHIFT);
        let result = h.app.on_cursor_tick();
        assert_eq!(result, CursorTickResult::SnappingActivated);
        assert!(h.app.is_snapping());
    }

    #[test]
    fn on_cursor_tick_deactivates_when_modifier_released() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        // Activate
        h.kb.pressed_keys.lock().insert(VK_LSHIFT);
        h.app.on_cursor_tick();
        assert!(h.app.is_snapping());
        // Release
        h.kb.pressed_keys.lock().remove(&VK_LSHIFT);
        let result = h.app.on_cursor_tick();
        assert_eq!(result, CursorTickResult::SnappingDeactivated);
        assert!(!h.app.is_snapping());
    }

    #[test]
    fn on_cursor_tick_updates_zone() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        h.kb.pressed_keys.lock().insert(VK_LSHIFT);
        h.app.on_cursor_tick(); // SnappingActivated
        // Move cursor into left zone
        *h.wm.cursor_pos.lock() = Some((100, 540));
        let result = h.app.on_cursor_tick();
        assert_eq!(
            result,
            CursorTickResult::ZoneChanged {
                active_index: Some(0)
            }
        );
        // Move cursor to right zone
        *h.wm.cursor_pos.lock() = Some((1500, 540));
        let result = h.app.on_cursor_tick();
        assert_eq!(
            result,
            CursorTickResult::ZoneChanged {
                active_index: Some(1)
            }
        );
    }

    #[test]
    fn on_hotkey_toggles_state() {
        let mut h = create_test_app();
        assert!(h.app.is_enabled());
        let new_state = h.app.on_hotkey();
        assert!(!new_state);
        assert!(!h.app.is_enabled());
        let new_state = h.app.on_hotkey();
        assert!(new_state);
        assert!(h.app.is_enabled());
    }

    #[test]
    fn on_config_changed_updates_config() {
        let mut h = create_test_app();
        let mut new_config = two_zone_config();
        new_config.monitors[0].zones.pop(); // remove right zone
        h.app.on_config_changed(new_config);
        let (zones, _) = h.app.get_zone_render_info();
        // Not tracking+snapping, so zones aren't re-resolved yet.
        // The stored config is updated though.
        assert_eq!(h.app.config.monitors[0].zones.len(), 1);
    }

    #[test]
    fn move_started_then_ended_resets_state() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        assert!(h.app.is_tracking());
        h.app.on_move_ended();
        assert!(!h.app.is_tracking());
        assert!(!h.app.is_snapping());
        assert_eq!(h.app.tracked_hwnd, None);
    }

    #[test]
    fn on_key_event_escape_cancels_snapping() {
        let mut h = create_test_app();
        h.app.on_move_started(100);
        // Activate snapping via modifier
        h.kb.pressed_keys.lock().insert(VK_LSHIFT);
        let result = h.app.on_cursor_tick();
        assert_eq!(result, CursorTickResult::SnappingActivated);
        assert!(h.app.is_tracking());
        assert!(h.app.is_snapping());

        // Press Escape
        let cancelled = h.app.on_key_event(&KeyboardMsg::KeyDown(0x1B));
        assert!(cancelled);
        assert!(!h.app.is_tracking());
        assert!(!h.app.is_snapping());
        assert_eq!(h.app.tracked_hwnd, None);
    }

    #[test]
    fn on_key_event_escape_noop_when_not_tracking() {
        let mut h = create_test_app();
        assert!(!h.app.is_tracking());
        // Press Escape while idle — should not panic and return false
        let cancelled = h.app.on_key_event(&KeyboardMsg::KeyDown(0x1B));
        assert!(!cancelled);
        assert!(!h.app.is_tracking());
    }
}
