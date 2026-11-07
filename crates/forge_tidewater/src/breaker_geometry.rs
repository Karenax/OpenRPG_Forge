// SPDX-License-Identifier: MIT
// Adapted from Tidewater Breakers.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1ab; see third_party/tidewater/LICENSE.
//! Derived beach stations and lip topology, rebuilt from the caller's terrain.
//! These are render inputs, not another canonical world representation.

pub const STATION_SPACING: f64 = 0.6;

/// Apply the reference map's scan settings to the shared shoreline algorithm.
#[must_use]
pub fn beach_stations(height_at: impl Fn(f64, f64) -> f64) -> Vec<[f32; 4]> {
    forge_engine::source_breaker_geometry::shoreline_stations(
        forge_engine::source_breaker_geometry::ShorelineScan {
            origin_xz: [0.0, 0.0],
            rotation_radians: 0.0,
            x_start: -175.0,
            x_steps: 740,
            x_step: 0.5,
            z_start: 12.0,
            z_steps: 244,
            z_step: 0.5,
            water_probe_height: -0.3,
            shoreline_height: 0.0,
            maximum_depth_jump: 3.0,
            maximum_x_gap: 1.01,
            bisections: 16,
            smoothing_passes: 30,
            station_spacing: STATION_SPACING,
        },
        height_at,
    )
    .expect("finite reference terrain and bounded shoreline")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn open_water_and_land_without_a_waterfront_have_no_stations() {
        assert!(beach_stations(|_, _| -10.0).is_empty());
        assert!(beach_stations(|_, _| 10.0).is_empty());
    }

    #[test]
    fn straight_beach_has_seaward_normals_and_equal_spacing() {
        let stations = beach_stations(|_, z| -z);
        assert!(stations.len() > 600);
        for s in &stations {
            assert!(s[1].abs() < 0.00001 && s[2].abs() < 0.00001 && (s[3] - 1.0).abs() < 0.00001);
        }
        for edge in stations.windows(2) {
            assert!((edge[1][0] - edge[0][0] - 0.6).abs() < 0.00002);
        }
    }
}
