/// Engine types used throughout the application for geometry, zone state, and rendering.

/// Axis-aligned rectangle defined by top-left corner and dimensions.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Rect {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

impl Rect {
    /// Create a new rectangle from position and size.
    pub fn new(x: i32, y: i32, width: i32, height: i32) -> Self {
        Self { x, y, width, height }
    }

    /// Returns true if the point (px, py) lies inside this rectangle.
    pub fn contains(&self, px: i32, py: i32) -> bool {
        px >= self.x && px < self.x + self.width && py >= self.y && py < self.y + self.height
    }

    /// Returns the centre point of this rectangle.
    #[cfg(test)]
    pub fn center(&self) -> (i32, i32) {
        (self.x + self.width / 2, self.y + self.height / 2)
    }
}

/// A zone definition resolved to absolute pixel coordinates on a specific monitor.
#[derive(Debug, Clone)]
pub struct ResolvedZone {
    pub definition: crate::config::models::ZoneDefinition,
    pub bounds: Rect,
}

/// The current drag/snap state of the engine.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum SnapState {
    /// No drag is in progress.
    Idle,
    /// The user is actively dragging a window.
    DragActive,
}

/// Lightweight zone info sent to the overlay for rendering.
#[derive(Debug, Clone)]
pub struct ZoneRenderInfo {
    #[allow(dead_code)]
    pub zone_id: String,
    pub name: String,
    pub bounds: Rect,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rect_contains_point_inside() {
        let r = Rect::new(10, 20, 100, 50);
        assert!(r.contains(10, 20));
        assert!(r.contains(50, 40));
    }

    #[test]
    fn rect_does_not_contain_point_outside() {
        let r = Rect::new(10, 20, 100, 50);
        assert!(!r.contains(9, 20));
        assert!(!r.contains(110, 20));
        assert!(!r.contains(10, 70));
    }

    #[test]
    fn rect_center() {
        let r = Rect::new(0, 0, 100, 200);
        assert_eq!(r.center(), (50, 100));
    }
}
