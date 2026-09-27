//! Explicit, one-shot placement alignment. Results remain absolute authoring corrections.
use crate::{TerrainGenerationPlan, sampled_terrain::SampledTerrainPlan};
use forge_core::Transform;

#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct PlacementSnap {
    /// Horizontal world grid, anchored at (0, 0), in metres. Y is never quantized.
    pub grid_step_m: Option<f32>,
    /// Put the pivot on the supplied terrain surface plus this offset, after grid alignment.
    /// None preserves the authored Y; this does not create a live surface constraint.
    pub surface_offset_m: Option<f32>,
}

impl PlacementSnap {
    /// Axis handles only quantize the active horizontal coordinate. Surface locking
    /// may change Y for X/Z handles; it conflicts with explicit vertical dragging.
    pub fn resolve_axis(
        self,
        transform: Transform,
        axis: crate::placement_transform::Axis,
        terrain: &TerrainGenerationPlan,
    ) -> Result<Transform, String> {
        self.resolve_axis_surface(transform, axis, Surface::Procedural(terrain))
    }

    /// Align to the verified imported raster and its current authored corrections.
    pub fn resolve_axis_sampled(
        self,
        transform: Transform,
        axis: crate::placement_transform::Axis,
        terrain: &SampledTerrainPlan,
    ) -> Result<Transform, String> {
        self.resolve_axis_surface(transform, axis, Surface::Sampled(terrain))
    }

    fn resolve_axis_surface(
        self,
        transform: Transform,
        axis: crate::placement_transform::Axis,
        terrain: Surface<'_>,
    ) -> Result<Transform, String> {
        use crate::placement_transform::Axis;
        self.validate()?;
        if matches!(axis, Axis::Y) && self.surface_offset_m.is_some() {
            return Err("Disable surface alignment before dragging the vertical axis".into());
        }
        let mut snapped = Self {
            grid_step_m: self.grid_step_m,
            surface_offset_m: None,
        }
        .resolve_surface(transform, terrain)?;
        match axis {
            Axis::X => snapped.translation_m.z = transform.translation_m.z,
            Axis::Z => snapped.translation_m.x = transform.translation_m.x,
            Axis::Y => snapped.translation_m = transform.translation_m,
        }
        Self {
            grid_step_m: None,
            surface_offset_m: self.surface_offset_m,
        }
        .resolve_surface(snapped, terrain)
    }

    pub fn validate(self) -> Result<(), String> {
        if let Some(step) = self.grid_step_m
            && (!step.is_finite() || !(0.01..=128.0).contains(&step))
        {
            return Err("Grid step must be between 0.01 and 128 metres".into());
        }
        if self
            .surface_offset_m
            .is_some_and(|offset| !offset.is_finite())
        {
            return Err("Surface offset must be finite".into());
        }
        Ok(())
    }

    pub fn resolve(
        self,
        transform: Transform,
        terrain: &TerrainGenerationPlan,
    ) -> Result<Transform, String> {
        self.resolve_surface(transform, Surface::Procedural(terrain))
    }

    /// Sample after grid alignment. No fallback to procedural terrain or clamping.
    pub fn resolve_sampled(
        self,
        transform: Transform,
        terrain: &SampledTerrainPlan,
    ) -> Result<Transform, String> {
        self.resolve_surface(transform, Surface::Sampled(terrain))
    }

    fn resolve_surface(
        self,
        mut transform: Transform,
        terrain: Surface<'_>,
    ) -> Result<Transform, String> {
        self.validate()?;
        transform.validate().map_err(|e| e.to_string())?;
        if let Some(step) = self.grid_step_m {
            // f64 intermediate avoids overflow for otherwise valid f32 coordinates.
            // Halfway values round away from zero, symmetrically on both grid sides.
            // Intentional rounding back to the canonical f32 transform; validated below.
            #[allow(clippy::cast_possible_truncation)]
            let snap = |value: f32| {
                ((f64::from(value) / f64::from(step)).round() * f64::from(step)) as f32
            };
            transform.translation_m.x = snap(transform.translation_m.x);
            transform.translation_m.z = snap(transform.translation_m.z);
        }
        if let Some(offset) = self.surface_offset_m {
            transform.translation_m.y =
                terrain.height_at(transform.translation_m.x, transform.translation_m.z)? + offset;
        }
        transform.validate().map_err(|e| e.to_string())?;
        Ok(transform)
    }
}

#[derive(Clone, Copy)]
enum Surface<'a> {
    Procedural(&'a TerrainGenerationPlan),
    Sampled(&'a SampledTerrainPlan),
}

impl Surface<'_> {
    #[allow(clippy::cast_possible_truncation)] // Canonical transforms are f32; final transform validation rejects overflow.
    fn height_at(self, x: f32, z: f32) -> Result<f32, String> {
        match self {
            Self::Procedural(plan) => plan.surface_height_at(x, z).map_err(|e| e.to_string()),
            Self::Sampled(plan) => {
                let extent = plan.document().extent_m;
                if x.abs() > extent.size_x_m * 0.5 || z.abs() > extent.size_z_m * 0.5 {
                    return Err("Placement surface query is outside the terrain extent".into());
                }
                plan.height_at(f64::from(x), f64::from(z))
                    .map(|height| height as f32)
                    .map_err(|e| e.to_string())
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::Vec3;

    fn terrain() -> TerrainGenerationPlan {
        let mut source = forge_project::authoring::new_terrain("snap", "zone", "Snap").unwrap();
        source.height_deltas = vec![forge_project::TerrainHeightDelta::new(129, 127, 8.0)];
        TerrainGenerationPlan::from_document(&source).unwrap()
    }

    #[test]
    fn axis_snap_preserves_inactive_coordinates_and_refuses_vertical_surface_conflict() {
        use crate::placement_transform::Axis;
        let terrain = terrain();
        let original = Transform::from_translation(Vec3::new(1.4, 8.0, -2.6));
        let grid = PlacementSnap {
            grid_step_m: Some(1.0),
            surface_offset_m: None,
        };
        assert_eq!(
            grid.resolve_axis(original, Axis::X, &terrain)
                .unwrap()
                .translation_m,
            Vec3::new(1.0, 8.0, -2.6)
        );
        assert_eq!(
            grid.resolve_axis(original, Axis::Z, &terrain)
                .unwrap()
                .translation_m,
            Vec3::new(1.4, 8.0, -3.0)
        );
        assert_eq!(
            grid.resolve_axis(original, Axis::Y, &terrain).unwrap(),
            original
        );
        let surface = PlacementSnap {
            surface_offset_m: Some(0.25),
            ..grid
        };
        let result = surface.resolve_axis(original, Axis::X, &terrain).unwrap();
        assert_eq!(
            result.translation_m.z.to_bits(),
            original.translation_m.z.to_bits()
        );
        assert_eq!(
            result.translation_m.y.to_bits(),
            (terrain.surface_height_at(1.0, -2.6).unwrap() + 0.25).to_bits()
        );
        assert!(surface.resolve_axis(original, Axis::Y, &terrain).is_err());
    }

    #[test]
    fn grid_is_symmetric_and_preserves_height_orientation_and_scale() {
        let terrain = terrain();
        let source = Transform {
            translation_m: Vec3::new(-1.25, 7.125, 1.25),
            rotation: forge_core::UnitQuaternion::new(0.0, 1.0, 0.0, 0.0),
            scale: Vec3::new(2.0, 3.0, 4.0),
        };
        assert_eq!(
            PlacementSnap::default().resolve(source, &terrain).unwrap(),
            source
        );
        let snapped = PlacementSnap {
            grid_step_m: Some(0.5),
            surface_offset_m: None,
        }
        .resolve(source, &terrain)
        .unwrap();
        assert_eq!(snapped.translation_m, Vec3::new(-1.5, 7.125, 1.5));
        assert_eq!(snapped.rotation, source.rotation);
        assert_eq!(snapped.scale, source.scale);
    }

    #[test]
    fn surface_is_sampled_after_horizontal_alignment_and_offset_is_explicit() {
        let terrain = terrain();
        let source = Transform::from_translation(Vec3::new(1.4, 99.0, -2.6));
        let snapped = PlacementSnap {
            grid_step_m: Some(1.0),
            surface_offset_m: Some(-0.25),
        }
        .resolve(source, &terrain)
        .unwrap();
        assert_eq!(snapped.translation_m.x.to_bits(), 1.0_f32.to_bits());
        assert_eq!(snapped.translation_m.z.to_bits(), (-3.0_f32).to_bits());
        assert_eq!(
            snapped.translation_m.y.to_bits(),
            (terrain.surface_height_at(1.0, -3.0).unwrap() - 0.25).to_bits()
        );
        assert_eq!(source.translation_m.y.to_bits(), 99.0_f32.to_bits());
        assert_ne!(
            terrain.surface_height_at(1.4, -2.6).unwrap().to_bits(),
            terrain.surface_height_at(1.0, -3.0).unwrap().to_bits()
        );
    }

    #[test]
    fn invalid_settings_and_surface_outside_zone_are_rejected_without_clamping() {
        let terrain = terrain();
        for step in [0.0, -1.0, 0.001, 129.0, f32::NAN, f32::INFINITY] {
            assert!(
                PlacementSnap {
                    grid_step_m: Some(step),
                    surface_offset_m: None
                }
                .resolve(Transform::IDENTITY, &terrain)
                .is_err()
            );
        }
        let snap = PlacementSnap {
            grid_step_m: None,
            surface_offset_m: Some(0.0),
        };
        assert!(
            snap.resolve(
                Transform::from_translation(Vec3::new(257.0, 0.0, 0.0)),
                &terrain
            )
            .is_err()
        );
        assert!(
            PlacementSnap {
                surface_offset_m: Some(f32::NAN),
                ..snap
            }
            .resolve(Transform::IDENTITY, &terrain)
            .is_err()
        );
        assert!(
            snap.resolve(
                Transform::from_translation(Vec3::new(f32::NAN, 0.0, 0.0)),
                &terrain
            )
            .is_err()
        );
    }
}
