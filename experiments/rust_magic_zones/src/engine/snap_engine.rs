/// Core snap engine that orchestrates drag detection, zone matching, and snap decisions.

use std::sync::Arc;

use crate::config::models::ZonesConfig;
use crate::engine::types::{ResolvedZone, SnapState, ZoneRenderInfo};
use crate::engine::zone_hit_tester::ZoneHitTester;
use crate::platform::MonitorProvider;

/// The main snap engine that processes window events and determines snap targets.
pub struct SnapEngine {
    monitor_provider: Arc<dyn MonitorProvider>,
    state: SnapState,
    resolved_zones: Vec<ResolvedZone>,
    active_zone_index: Option<usize>,
    dragged_hwnd: Option<isize>,
}

impl SnapEngine {
    /// Create a new snap engine with the given platform abstractions.
    pub fn new(monitor_provider: Arc<dyn MonitorProvider>) -> Self {
        Self {
            monitor_provider,
            state: SnapState::Idle,
            resolved_zones: Vec::new(),
            active_zone_index: None,
            dragged_hwnd: None,
        }
    }

    /// Resolve zone definitions into pixel rectangles using current monitor geometry.
    pub fn resolve_zones(&mut self, config: &ZonesConfig) {
        use crate::engine::coordinate_converter::CoordinateConverter;

        self.resolved_zones.clear();
        let monitors = self.monitor_provider.get_monitor_info();

        for monitor_cfg in &config.monitors {
            // Find matching physical monitor
            let physical = monitors.iter().find(|m| {
                if monitor_cfg.match_by == "index" {
                    m.index == monitor_cfg.index
                } else {
                    m.device_name == monitor_cfg.device_name
                }
            });

            if let Some(phys) = physical {
                for zone_def in &monitor_cfg.zones {
                    let bounds = if monitor_cfg.coordinate_unit == "pixel" {
                        crate::engine::types::Rect::new(
                            zone_def.x as i32,
                            zone_def.y as i32,
                            zone_def.width as i32,
                            zone_def.height as i32,
                        )
                    } else {
                        CoordinateConverter::percentage_to_absolute(
                            zone_def.x,
                            zone_def.y,
                            zone_def.width,
                            zone_def.height,
                            &phys.work_area,
                        )
                    };
                    self.resolved_zones.push(ResolvedZone {
                        definition: zone_def.clone(),
                        bounds,
                    });
                }
            }
        }
    }

    /// Notify the engine that a window drag has started.
    pub fn on_drag_start(&mut self, hwnd: isize) {
        self.state = SnapState::DragActive;
        self.dragged_hwnd = Some(hwnd);
        self.active_zone_index = None;
    }

    /// Notify the engine that the cursor has moved during a drag.
    /// Returns the index of the currently hovered zone, if any.
    pub fn on_cursor_move(&mut self, cursor_x: i32, cursor_y: i32) -> Option<usize> {
        if self.state != SnapState::DragActive {
            return None;
        }
        self.active_zone_index = ZoneHitTester::hit_test(cursor_x, cursor_y, &self.resolved_zones);
        self.active_zone_index
    }

    /// Notify the engine that the drag has ended. Returns the zone to snap to, if any.
    #[cfg(test)]
    pub fn on_drag_end(&mut self) -> Option<ResolvedZone> {
        let result = self
            .active_zone_index
            .and_then(|i| self.resolved_zones.get(i).cloned());
        self.state = SnapState::Idle;
        self.dragged_hwnd = None;
        self.active_zone_index = None;
        result
    }

    /// Get the current snap state.
    #[cfg(test)]
    pub fn state(&self) -> SnapState {
        self.state
    }

    /// Get zone rendering info for the overlay.
    pub fn zone_render_info(&self) -> Vec<ZoneRenderInfo> {
        self.resolved_zones
            .iter()
            .map(|z| ZoneRenderInfo {
                zone_id: z.definition.id.clone(),
                name: z.definition.name.clone(),
                bounds: z.bounds,
            })
            .collect()
    }

    /// Get the currently active (hovered) zone index.
    pub fn active_zone_index(&self) -> Option<usize> {
        self.active_zone_index
    }

    /// Cancel the current drag without snapping. No-op if already idle.
    pub fn cancel_drag(&mut self) {
        self.state = SnapState::Idle;
        self.active_zone_index = None;
        self.dragged_hwnd = None;
    }

    /// Return the window handle being dragged, or None if idle.
    #[cfg(test)]
    pub fn dragged_hwnd(&self) -> Option<isize> {
        self.dragged_hwnd
    }

    /// Return a reference to the currently hovered zone, or None.
    pub fn get_active_zone(&self) -> Option<&ResolvedZone> {
        self.active_zone_index
            .and_then(|i| self.resolved_zones.get(i))
    }

    /// Return a slice of all resolved zones.
    #[cfg(test)]
    pub fn resolved_zones(&self) -> &[ResolvedZone] {
        &self.resolved_zones
    }

    /// Return all zones as render info plus the active zone index.
    pub fn get_zone_render_info(&self) -> (Vec<ZoneRenderInfo>, Option<usize>) {
        (self.zone_render_info(), self.active_zone_index)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::engine::types::Rect;
    use crate::platform::mock::MockMonitorProvider;

    fn test_config() -> ZonesConfig {
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

    fn make_engine() -> SnapEngine {
        let mp = Arc::new(
            MockMonitorProvider::new()
                .with_monitor("DISPLAY1", 0, Rect::new(0, 0, 1920, 1080)),
        );
        SnapEngine::new(mp)
    }

    #[test]
    fn initial_state_is_idle() {
        let engine = make_engine();
        assert_eq!(engine.state(), SnapState::Idle);
    }

    #[test]
    fn resolve_zones_populates_zones() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        assert_eq!(engine.zone_render_info().len(), 2);
    }

    #[test]
    fn drag_lifecycle() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());

        engine.on_drag_start(42);
        assert_eq!(engine.state(), SnapState::DragActive);

        // Cursor in left zone
        let zone = engine.on_cursor_move(100, 540);
        assert_eq!(zone, Some(0));

        // End drag — should snap to left zone
        let snapped = engine.on_drag_end();
        assert!(snapped.is_some());
        assert_eq!(snapped.unwrap().definition.id, "left");
        assert_eq!(engine.state(), SnapState::Idle);
    }

    #[test]
    fn cursor_outside_all_zones_returns_none() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        engine.on_drag_start(42);
        let zone = engine.on_cursor_move(-100, -100);
        assert!(zone.is_none());
    }

    #[test]
    fn cancel_drag_returns_to_idle() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        engine.on_drag_start(42);
        engine.on_cursor_move(100, 540); // hover over left zone
        assert_eq!(engine.state(), SnapState::DragActive);

        engine.cancel_drag();

        assert_eq!(engine.state(), SnapState::Idle);
        assert!(engine.get_active_zone().is_none());
        assert!(engine.dragged_hwnd().is_none());
    }

    #[test]
    fn cancel_drag_while_idle_is_noop() {
        let engine_idle = make_engine();
        assert_eq!(engine_idle.state(), SnapState::Idle);
        // Should not panic
        let mut engine = engine_idle;
        engine.cancel_drag();
        assert_eq!(engine.state(), SnapState::Idle);
    }

    #[test]
    fn dragged_hwnd_returns_none_when_idle() {
        let engine = make_engine();
        assert!(engine.dragged_hwnd().is_none());
    }

    #[test]
    fn dragged_hwnd_returns_handle_during_drag() {
        let mut engine = make_engine();
        engine.on_drag_start(42);
        assert_eq!(engine.dragged_hwnd(), Some(42));
    }

    #[test]
    fn get_active_zone_returns_hovered_zone() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        engine.on_drag_start(42);
        engine.on_cursor_move(100, 540); // left zone

        let zone = engine.get_active_zone();
        assert!(zone.is_some());
        assert_eq!(zone.unwrap().definition.id, "left");
    }

    #[test]
    fn get_active_zone_returns_none_when_no_hover() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        engine.on_drag_start(42);
        engine.on_cursor_move(-100, -100); // outside all zones

        assert!(engine.get_active_zone().is_none());
    }

    #[test]
    fn resolved_zones_returns_all_zones() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        assert_eq!(engine.resolved_zones().len(), 2);
    }

    #[test]
    fn get_zone_render_info_returns_all_zones_and_active_index() {
        let mut engine = make_engine();
        engine.resolve_zones(&test_config());
        engine.on_drag_start(42);
        engine.on_cursor_move(100, 540); // left zone, index 0

        let (infos, active) = engine.get_zone_render_info();
        assert_eq!(infos.len(), 2);
        assert_eq!(active, Some(0));
        assert_eq!(infos[0].zone_id, "left");
        assert_eq!(infos[1].zone_id, "right");
    }
}
