/// Determines which zone (if any) the cursor is currently over.

use crate::engine::types::{Rect, ResolvedZone};

/// Hit-tests a cursor position against a set of resolved zones.
pub struct ZoneHitTester;

impl ZoneHitTester {
    /// Find the best matching zone for the given cursor position.
    ///
    /// When multiple zones overlap at the cursor position, the one with the
    /// highest priority wins. Ties are broken by smallest area.
    pub fn hit_test(cursor_x: i32, cursor_y: i32, zones: &[ResolvedZone]) -> Option<usize> {
        let mut best: Option<(usize, i32, i64)> = None; // (index, priority, area)

        for (i, zone) in zones.iter().enumerate() {
            if zone.bounds.contains(cursor_x, cursor_y) {
                let area = zone.bounds.width as i64 * zone.bounds.height as i64;
                let priority = zone.definition.priority;
                match &best {
                    None => best = Some((i, priority, area)),
                    Some((_, bp, ba)) => {
                        if priority > *bp || (priority == *bp && area < *ba) {
                            best = Some((i, priority, area));
                        }
                    }
                }
            }
        }

        best.map(|(i, _, _)| i)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::models::ZoneDefinition;

    fn make_zone(id: &str, priority: i32, bounds: Rect) -> ResolvedZone {
        ResolvedZone {
            definition: ZoneDefinition {
                id: id.to_string(),
                name: id.to_string(),
                priority,
                x: 0.0,
                y: 0.0,
                width: 0.0,
                height: 0.0,
            },
            bounds,
        }
    }

    #[test]
    fn hit_test_returns_none_when_no_zones() {
        assert!(ZoneHitTester::hit_test(100, 100, &[]).is_none());
    }

    #[test]
    fn hit_test_returns_none_when_cursor_outside() {
        let zones = vec![make_zone("a", 0, Rect::new(0, 0, 100, 100))];
        assert!(ZoneHitTester::hit_test(200, 200, &zones).is_none());
    }

    #[test]
    fn hit_test_finds_containing_zone() {
        let zones = vec![
            make_zone("a", 0, Rect::new(0, 0, 960, 1080)),
            make_zone("b", 0, Rect::new(960, 0, 960, 1080)),
        ];
        assert_eq!(ZoneHitTester::hit_test(500, 500, &zones), Some(0));
        assert_eq!(ZoneHitTester::hit_test(1000, 500, &zones), Some(1));
    }

    #[test]
    fn hit_test_prefers_higher_priority() {
        let zones = vec![
            make_zone("big", 0, Rect::new(0, 0, 1920, 1080)),
            make_zone("small", 1, Rect::new(100, 100, 200, 200)),
        ];
        assert_eq!(ZoneHitTester::hit_test(150, 150, &zones), Some(1));
    }

    #[test]
    fn hit_test_same_priority_prefers_smaller() {
        let zones = vec![
            make_zone("big", 0, Rect::new(0, 0, 1920, 1080)),
            make_zone("small", 0, Rect::new(100, 100, 200, 200)),
        ];
        assert_eq!(ZoneHitTester::hit_test(150, 150, &zones), Some(1));
    }
}
