/// Applies a snap decision by moving/resizing the target window.

use std::sync::Arc;

use crate::engine::types::{Rect, ResolvedZone};
use crate::platform::WindowManager;

/// Moves a window to fit a resolved zone, compensating for invisible window borders.
pub struct SnapApplier {
    window_manager: Arc<dyn WindowManager>,
}

impl SnapApplier {
    /// Create a new applier with the given window manager.
    pub fn new(window_manager: Arc<dyn WindowManager>) -> Self {
        Self { window_manager }
    }

    /// Snap the window identified by `hwnd` to the given zone bounds.
    ///
    /// Compensates for extended frame bounds (invisible borders around modern
    /// Windows windows) so the visible area matches the zone exactly.
    pub fn apply(&self, hwnd: isize, zone: &ResolvedZone) {
        // Compute border compensation
        let border_offset = self.compute_border_offset(hwnd);

        let target = Rect::new(
            zone.bounds.x - border_offset.left,
            zone.bounds.y - border_offset.top,
            zone.bounds.width + border_offset.left + border_offset.right,
            zone.bounds.height + border_offset.top + border_offset.bottom,
        );

        // Restore window if maximized before repositioning
        if self.window_manager.is_zoomed(hwnd) {
            self.window_manager.show_window(hwnd, 9); // SW_RESTORE
        }

        // SWP_NOZORDER | SWP_NOACTIVATE
        let flags = 0x0004 | 0x0010;
        self.window_manager
            .set_window_pos(hwnd, target.x, target.y, target.width, target.height, flags);
    }

    /// Snap the window to the given absolute pixel bounds, with border compensation.
    ///
    /// This is a convenience method used by the message loop where only a `Rect`
    /// is available (from `App::on_move_ended`).
    pub fn apply_rect(&self, hwnd: isize, bounds: Rect) {
        let border_offset = self.compute_border_offset(hwnd);

        let target = Rect::new(
            bounds.x - border_offset.left,
            bounds.y - border_offset.top,
            bounds.width + border_offset.left + border_offset.right,
            bounds.height + border_offset.top + border_offset.bottom,
        );

        if self.window_manager.is_zoomed(hwnd) {
            self.window_manager.show_window(hwnd, 9); // SW_RESTORE
        }

        // SWP_NOZORDER | SWP_NOACTIVATE
        let flags = 0x0004 | 0x0010;
        self.window_manager
            .set_window_pos(hwnd, target.x, target.y, target.width, target.height, flags);
    }

    /// Compute the difference between the window rect and the extended frame bounds.
    fn compute_border_offset(&self, hwnd: isize) -> BorderOffset {
        let outer = self.window_manager.get_window_rect(hwnd);
        let inner = self.window_manager.get_extended_frame_bounds(hwnd);

        match (outer, inner) {
            (Some(o), Some(i)) => BorderOffset {
                left: i.x - o.x,
                top: i.y - o.y,
                right: (o.x + o.width) - (i.x + i.width),
                bottom: (o.y + o.height) - (i.y + i.height),
            },
            _ => BorderOffset::zero(),
        }
    }
}

/// Invisible border sizes on each side of a window.
struct BorderOffset {
    left: i32,
    top: i32,
    right: i32,
    bottom: i32,
}

impl BorderOffset {
    fn zero() -> Self {
        Self {
            left: 0,
            top: 0,
            right: 0,
            bottom: 0,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::models::ZoneDefinition;
    use crate::platform::mock::MockWindowManager;

    fn make_zone(bounds: Rect) -> ResolvedZone {
        ResolvedZone {
            definition: ZoneDefinition {
                id: "test".to_string(),
                name: "Test".to_string(),
                priority: 0,
                x: 0.0,
                y: 0.0,
                width: 50.0,
                height: 100.0,
            },
            bounds,
        }
    }

    #[test]
    fn apply_calls_set_window_pos() {
        let wm = Arc::new(MockWindowManager::new());
        let applier = SnapApplier::new(wm.clone());
        let zone = make_zone(Rect::new(0, 0, 960, 1080));

        applier.apply(42, &zone);

        let calls = wm.set_pos_calls.lock();
        assert_eq!(calls.len(), 1);
        assert_eq!(calls[0].0, 42); // hwnd
    }

    #[test]
    fn apply_compensates_for_borders() {
        let wm = Arc::new(MockWindowManager::new());
        // Simulate invisible borders: outer is 7px wider on each side
        wm.window_rects
            .lock()
            .insert(1, Rect::new(93, 0, 974, 1080));
        wm.frame_bounds
            .lock()
            .insert(1, Rect::new(100, 0, 960, 1080));

        let applier = SnapApplier::new(wm.clone());
        let zone = make_zone(Rect::new(0, 0, 960, 1080));

        applier.apply(1, &zone);

        let calls = wm.set_pos_calls.lock();
        assert_eq!(calls.len(), 1);
        // Should compensate: x should be -7 to account for invisible left border
        assert_eq!(calls[0].1, -7); // x adjusted
    }
}
