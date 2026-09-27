//! Derived root exclusion for ground-floor rooms; never edits authored masks.
use forge_project::{TerrainDocument, zone_authoring::buildings::AuthoredBuilding};

#[derive(Clone, Debug, serde::Serialize)]
pub(crate) struct Footprint {
    origin: [f32; 2],
    half_size: [f32; 2],
    sin: f32,
    cos: f32,
}

impl Footprint {
    pub(crate) fn contains(&self, x: f32, z: f32) -> bool {
        let x = x - self.origin[0];
        let z = z - self.origin[1];
        let local_x = self.cos * x - self.sin * z;
        let local_z = self.sin * x + self.cos * z;
        local_x.abs() <= self.half_size[0] && local_z.abs() <= self.half_size[1]
    }
}

pub(crate) fn prepare(
    terrain: &TerrainDocument,
    buildings: &[AuthoredBuilding],
) -> Result<Vec<Footprint>, String> {
    forge_project::zone_authoring::buildings::validate(buildings, terrain)?;
    Ok(buildings
        .iter()
        .map(|building| {
            let (sin, cos) = building.yaw_degrees.to_radians().sin_cos();
            Footprint {
                origin: [building.origin_m.x, building.origin_m.z],
                half_size: [
                    building.dimensions.width_m * 0.5,
                    building.dimensions.depth_m * 0.5,
                ],
                sin,
                cos,
            }
        })
        .collect())
}

#[cfg(test)]
mod tests {
    use super::Footprint;
    #[test]
    fn building_vegetation_population_is_derived_and_empty_input_preserves_identity() {
        use crate::{
            VegetationGenerationPlan, VisualValleyTerrainRecipe, VisualValleyVegetationRecipe,
        };
        use forge_project::{
            building::RoomDimensions, zone_authoring::buildings::AuthoredBuilding,
        };
        let terrain = VisualValleyTerrainRecipe::default().build().unwrap();
        let vegetation = VisualValleyVegetationRecipe::default().build().unwrap();
        let original = vegetation.clone();
        let baseline = VegetationGenerationPlan::from_documents(&terrain, &vegetation).unwrap();
        let empty = VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation,
            &[],
            &[],
            &[],
        )
        .unwrap();
        assert_eq!(empty.source_hash(), baseline.source_hash());
        let mut building = AuthoredBuilding {
            id: forge_core::EntityId::deterministic("vegetation-room"),
            name: "Room".into(),
            generator_version: 1,
            origin_m: forge_core::Vec3::ZERO,
            yaw_degrees: 45.0,
            locked: false,
            door_offset_override_m: None,
            dimensions: RoomDimensions {
                width_m: 128.0,
                depth_m: 128.0,
                clear_height_m: 3.0,
                wall_m: 0.25,
                slab_m: 0.2,
                door_width_m: 1.4,
                door_height_m: 2.2,
                door_offset_x_m: 0.0,
            },
        };
        let buildings = std::slice::from_ref(&building);
        let footprints = super::prepare(&terrain, buildings).unwrap();
        let filtered = VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation,
            &[],
            &[],
            buildings,
        )
        .unwrap();
        assert_ne!(baseline.source_hash(), filtered.source_hash());
        let before = baseline.generate().unwrap();
        let after = filtered.generate().unwrap();
        assert!(
            before
                .chunks
                .iter()
                .flat_map(|c| &c.instances)
                .any(|p| footprints[0].contains(p.position_m.x, p.position_m.z))
        );
        assert!(
            after
                .chunks
                .iter()
                .flat_map(|c| &c.instances)
                .all(|p| !footprints[0].contains(p.position_m.x, p.position_m.z))
        );
        assert!(after.kind_counts.total() > 0);
        assert_eq!(filtered.vegetation(), &original);
        let hash = filtered.source_hash();
        building.name = "Renamed".into();
        building.locked = true;
        let unchanged = VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation,
            &[],
            &[],
            std::slice::from_ref(&building),
        )
        .unwrap();
        assert_eq!(hash, unchanged.source_hash());
        building.origin_m.x = 20.0;
        let moved = VegetationGenerationPlan::from_documents_with_buildings(
            &terrain,
            &vegetation,
            &[],
            &[],
            std::slice::from_ref(&building),
        )
        .unwrap();
        assert_ne!(hash, moved.source_hash());
        assert_eq!(vegetation, original);
    }
    #[test]
    fn rotated_footprint_does_not_clear_its_entire_axis_aligned_bounds() {
        for degrees in [0.0_f32, 45.0, 90.0, 180.0, -90.0] {
            let (sin, cos) = degrees.to_radians().sin_cos();
            let footprint = Footprint {
                origin: [20.0, -10.0],
                half_size: [4.0, 3.0],
                sin,
                cos,
            };
            for (x, z, expected) in [
                (0.0, 0.0, true),
                (3.9, 2.9, true),
                (4.1, 0.0, false),
                (0.0, 3.1, false),
            ] {
                assert_eq!(
                    footprint.contains(20.0 + cos * x + sin * z, -10.0 - sin * x + cos * z),
                    expected
                );
            }
        }
    }
}
