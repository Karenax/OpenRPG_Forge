//! Derived variable-width corridors; never materialized into authored density masks.
use forge_project::{TerrainDocument, zone_authoring::rivers::AuthoredRiver};

#[derive(Clone, Debug)]
pub(crate) struct Segment {
    start: [f32; 2],
    delta: [f32; 2],
    inverse_length_squared: f32,
    start_radius: f32,
    radius_delta: f32,
}

impl Segment {
    pub(crate) fn contains(&self, x: f32, z: f32) -> bool {
        let offset = [x - self.start[0], z - self.start[1]];
        let fraction = ((offset[0] * self.delta[0] + offset[1] * self.delta[1])
            * self.inverse_length_squared)
            .clamp(0.0, 1.0);
        let radius = self.start_radius + fraction * self.radius_delta;
        let distance_squared = (offset[0] - fraction * self.delta[0]).powi(2)
            + (offset[1] - fraction * self.delta[1]).powi(2);
        distance_squared <= radius * radius
    }
}

pub(crate) fn prepare(
    terrain: &TerrainDocument,
    rivers: &[AuthoredRiver],
) -> Result<Vec<Segment>, String> {
    forge_project::zone_authoring::rivers::validate(rivers, terrain)?;
    let mut segments = Vec::new();
    for river in rivers {
        crate::river_carving::validate(terrain, &river.water, Some(river.maximum_cut_m))?;
        let shoulder = river.water.banks.shoulder_width_m + river.water.banks.blend_width_m;
        for pair in river.water.river.control_points.windows(2) {
            let delta = [
                pair[1].center_m.x - pair[0].center_m.x,
                pair[1].center_m.z - pair[0].center_m.z,
            ];
            segments.push(Segment {
                start: [pair[0].center_m.x, pair[0].center_m.z],
                delta,
                inverse_length_squared: 1.0 / (delta[0] * delta[0] + delta[1] * delta[1]),
                start_radius: pair[0].surface_width_m * 0.5 + shoulder,
                radius_delta: (pair[1].surface_width_m - pair[0].surface_width_m) * 0.5,
            });
        }
    }
    Ok(segments)
}

#[cfg(test)]
mod tests {
    use super::Segment;
    #[test]
    fn clearance_interpolates_width_instead_of_clearing_the_maximum_everywhere() {
        let segment = Segment {
            start: [0.0, 0.0],
            delta: [100.0, 0.0],
            inverse_length_squared: 0.0001,
            start_radius: 2.0,
            radius_delta: 10.0,
        };
        assert!(segment.contains(0.0, 2.0));
        assert!(!segment.contains(0.0, 3.0));
        assert!(segment.contains(50.0, 7.0));
        assert!(!segment.contains(50.0, 8.0));
        assert!(segment.contains(100.0, 12.0));
        assert!(!segment.contains(100.0, 13.0));
        assert!(segment.contains(-2.0, 0.0));
        assert!(!segment.contains(-3.0, 0.0));
    }
}
