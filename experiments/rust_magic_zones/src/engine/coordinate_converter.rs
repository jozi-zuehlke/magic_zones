/// Converts zone coordinates between percentage-based and absolute pixel values
/// relative to a monitor's work area.

use crate::engine::types::Rect;

/// Converts zone definitions from their coordinate unit into absolute pixel rects.
pub struct CoordinateConverter;

impl CoordinateConverter {
    /// Convert a percentage-based zone rectangle to absolute pixels within the given work area.
    ///
    /// The input `zone_rect` fields are percentages (0–100). The output is pixel coordinates.
    pub fn percentage_to_absolute(
        zone_x: f64,
        zone_y: f64,
        zone_width: f64,
        zone_height: f64,
        work_area: &Rect,
    ) -> Rect {
        let x = work_area.x + ((zone_x / 100.0) * work_area.width as f64) as i32;
        let y = work_area.y + ((zone_y / 100.0) * work_area.height as f64) as i32;
        let w = ((zone_width / 100.0) * work_area.width as f64) as i32;
        let h = ((zone_height / 100.0) * work_area.height as f64) as i32;
        Rect::new(x, y, w, h)
    }

    /// Convert absolute pixel coordinates to percentage-based values relative to the work area.
    #[cfg(test)]
    pub fn absolute_to_percentage(
        rect: &Rect,
        work_area: &Rect,
    ) -> (f64, f64, f64, f64) {
        let x = ((rect.x - work_area.x) as f64 / work_area.width as f64) * 100.0;
        let y = ((rect.y - work_area.y) as f64 / work_area.height as f64) * 100.0;
        let w = (rect.width as f64 / work_area.width as f64) * 100.0;
        let h = (rect.height as f64 / work_area.height as f64) * 100.0;
        (x, y, w, h)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn percentage_to_absolute_full_screen() {
        let wa = Rect::new(0, 0, 1920, 1080);
        let result = CoordinateConverter::percentage_to_absolute(0.0, 0.0, 100.0, 100.0, &wa);
        assert_eq!(result, Rect::new(0, 0, 1920, 1080));
    }

    #[test]
    fn percentage_to_absolute_left_half() {
        let wa = Rect::new(0, 0, 1920, 1080);
        let result = CoordinateConverter::percentage_to_absolute(0.0, 0.0, 50.0, 100.0, &wa);
        assert_eq!(result, Rect::new(0, 0, 960, 1080));
    }

    #[test]
    fn percentage_to_absolute_with_offset_work_area() {
        let wa = Rect::new(100, 50, 1820, 1030);
        let result = CoordinateConverter::percentage_to_absolute(50.0, 0.0, 50.0, 100.0, &wa);
        assert_eq!(result.x, 100 + 910);
        assert_eq!(result.y, 50);
    }

    #[test]
    fn roundtrip_conversion() {
        let wa = Rect::new(0, 0, 1920, 1080);
        let abs = CoordinateConverter::percentage_to_absolute(25.0, 10.0, 50.0, 80.0, &wa);
        let (px, py, pw, ph) = CoordinateConverter::absolute_to_percentage(&abs, &wa);
        assert!((px - 25.0).abs() < 0.5);
        assert!((py - 10.0).abs() < 0.5);
        assert!((pw - 50.0).abs() < 0.5);
        assert!((ph - 80.0).abs() < 0.5);
    }
}
