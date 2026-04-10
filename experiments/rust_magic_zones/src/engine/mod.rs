/// Engine module — core snapping logic, zone resolution, and coordinate math.

pub mod coordinate_converter;
pub mod modifier_key_state_tracker;
pub mod snap_applier;
pub mod snap_engine;
pub mod types;
pub mod window_filter;
pub mod zone_hit_tester;

pub use coordinate_converter::CoordinateConverter;
pub use snap_engine::SnapEngine;
pub use types::{Rect, ResolvedZone, SnapState, ZoneRenderInfo};
pub use zone_hit_tester::ZoneHitTester;
