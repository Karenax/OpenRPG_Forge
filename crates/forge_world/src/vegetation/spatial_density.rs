use super::{CELL_X_MIX, CELL_Z_MIX, VegetationGenerationPlan, attempt_entropy, unit_f64};
use forge_project::{VegetationDensityPattern, VegetationLayer};

impl VegetationGenerationPlan {
    pub(super) fn attempt_acceptance(
        &self,
        layer: &VegetationLayer,
        x: u16,
        z: u16,
        attempt: u8,
    ) -> f64 {
        let base = f64::from(layer.acceptance_probability);
        if layer.density_pattern.is_none() && layer.density_mask.is_none() {
            return base;
        }
        let entropy = attempt_entropy(self.vegetation.seed, layer.id.as_str(), x, z, attempt);
        let (world_x, world_z) = self.candidate_xz(entropy, x, z);
        let seed = attempt_entropy(self.vegetation.seed, layer.id.as_str(), 0, 0, 0);
        let variation = layer
            .density_pattern
            .map_or(1.0, |pattern| multiplier(pattern, seed, world_x, world_z));
        let region = layer.density_mask.as_ref().map_or(1.0, |mask| {
            mask_weight(mask, self.vegetation.extent_m, world_x, world_z)
        });
        (base * variation * region).clamp(0.0, 1.0)
    }
}

/// Bilinear sampling keeps feathered forest edges continuous across chunk boundaries.
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
pub(crate) fn mask_weight(
    mask: &forge_project::VegetationDensityMask,
    extent: forge_project::VegetationExtent,
    x: f32,
    z: f32,
) -> f64 {
    let last = mask.vertices_per_axis - 1;
    let u = ((x / extent.size_x_m + 0.5) * f32::from(last)).clamp(0.0, f32::from(last));
    let v = ((z / extent.size_z_m + 0.5) * f32::from(last)).clamp(0.0, f32::from(last));
    let ix = u.floor() as u16;
    let iz = v.floor() as u16;
    let sample = |x: u16, z: u16| {
        f64::from(
            mask.samples_u8[usize::from(z) * usize::from(mask.vertices_per_axis) + usize::from(x)],
        ) / 255.0
    };
    let lerp = |a: f64, b: f64, t: f64| a + (b - a) * t;
    lerp(
        lerp(
            sample(ix, iz),
            sample((ix + 1).min(last), iz),
            f64::from(u - f32::from(ix)),
        ),
        lerp(
            sample(ix, (iz + 1).min(last)),
            sample((ix + 1).min(last), (iz + 1).min(last)),
            f64::from(u - f32::from(ix)),
        ),
        f64::from(v - f32::from(iz)),
    )
}

/// Smooth world-space value noise, independent of chunk traversal and candidate identity.
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn multiplier(pattern: VegetationDensityPattern, seed: u64, x: f32, z: f32) -> f64 {
    let x = f64::from(x) / f64::from(pattern.patch_size_m);
    let z = f64::from(z) / f64::from(pattern.patch_size_m);
    let cell_x = x.floor() as i64;
    let cell_z = z.floor() as i64;
    let smooth = |t: f64| t * t * (3.0 - 2.0 * t);
    let tx = smooth(x - x.floor());
    let tz = smooth(z - z.floor());
    let lattice = |dx: i64, dz: i64| {
        unit_f64(
            seed ^ ((cell_x + dx) as u64).wrapping_mul(CELL_X_MIX)
                ^ ((cell_z + dz) as u64).wrapping_mul(CELL_Z_MIX),
            11,
        )
    };
    let lerp = |a: f64, b: f64, t: f64| a + (b - a) * t;
    let value = lerp(
        lerp(lattice(0, 0), lattice(1, 0), tx),
        lerp(lattice(0, 1), lattice(1, 1), tx),
        tz,
    );
    lerp(
        f64::from(pattern.minimum_multiplier),
        f64::from(pattern.maximum_multiplier),
        value,
    )
}

#[cfg(test)]
mod tests {
    use super::super::exclusion_contains;
    use super::*;
    use forge_project::VegetationKind;
    use std::collections::BTreeMap;

    #[test]
    fn density_mask_interpolates_and_zero_stays_empty() {
        let mask = forge_project::VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 255, 0, 255],
        };
        let extent = forge_project::VegetationExtent::new(512.0, 512.0);
        assert_eq!(
            mask_weight(&mask, extent, -256.0, 0.0).to_bits(),
            0.0_f64.to_bits()
        );
        assert_eq!(
            mask_weight(&mask, extent, 0.0, 0.0).to_bits(),
            0.5_f64.to_bits()
        );
        assert_eq!(
            mask_weight(&mask, extent, 256.0, 0.0).to_bits(),
            1.0_f64.to_bits()
        );
        let terrain = crate::VisualValleyTerrainRecipe::default().build().unwrap();
        let mut vegetation = crate::VisualValleyVegetationRecipe::default()
            .build()
            .unwrap();
        vegetation.layers[0].density_mask = Some(forge_project::VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0; 4],
        });
        let plan = VegetationGenerationPlan::from_documents(&terrain, &vegetation).unwrap();
        assert_eq!(plan.generate().unwrap().kind_counts.trees, 0);
        assert_eq!(
            plan.generate_render_density(2.0)
                .unwrap()
                .generated
                .kind_counts
                .trees,
            0
        );
    }

    #[test]
    fn authored_forest_is_dense_tall_deterministic_and_keeps_clearings() {
        let world =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let layer = world
            .vegetation
            .layers
            .iter()
            .find(|layer| layer.kind == VegetationKind::Tree)
            .unwrap();
        let mask = layer.density_mask.as_ref().unwrap();
        let plan =
            VegetationGenerationPlan::from_documents(&world.carved_terrain, &world.vegetation)
                .unwrap();
        let generated = plan.generate().unwrap();
        assert_eq!(generated, plan.generate().unwrap());
        let trees: Vec<_> = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter(|instance| instance.kind == VegetationKind::Tree)
            .collect();
        assert!(trees.len() > 1000, "forest too sparse: {}", trees.len());
        for tree in &trees {
            assert!(
                mask_weight(
                    mask,
                    world.vegetation.extent_m,
                    tree.position_m.x,
                    tree.position_m.z
                ) > 0.0
            );
            assert!((9.0..=18.0).contains(&(tree.scale * 5.65)));
            assert!(
                !world
                    .vegetation
                    .exclusions
                    .iter()
                    .any(|exclusion| exclusion_contains(
                        exclusion,
                        tree.position_m.x,
                        tree.position_m.z
                    ))
            );
        }
        println!(
            "forest trees={}, vegetation_hash={}",
            trees.len(),
            world.vegetation.content_hash().unwrap()
        );
        let json = world.vegetation.to_pretty_json().unwrap();
        assert_eq!(
            forge_project::VegetationDocument::from_json(&json).unwrap(),
            world.vegetation
        );
    }

    #[test]
    fn field_is_bounded_continuous_and_seeded() {
        let pattern = VegetationDensityPattern {
            patch_size_m: 32.0,
            minimum_multiplier: 0.15,
            maximum_multiplier: 1.6,
        };
        let mut minimum = f64::MAX;
        let mut maximum = f64::MIN;
        for x in -256_i16..=256 {
            for z in [-256.0, -128.0, 0.0, 128.0, 256.0] {
                let value = multiplier(pattern, 1337, f32::from(x), z);
                assert!(
                    (f64::from(pattern.minimum_multiplier)..=f64::from(pattern.maximum_multiplier))
                        .contains(&value)
                );
                minimum = minimum.min(value);
                maximum = maximum.max(value);
            }
        }
        assert!(maximum - minimum > 0.8);
        for edge in [-64.0, -32.0, 0.0, 32.0, 64.0] {
            assert!(
                (multiplier(pattern, 1337, edge - 0.001, 17.0)
                    - multiplier(pattern, 1337, edge + 0.001, 17.0))
                .abs()
                    < 0.00001
            );
        }
        assert_ne!(
            multiplier(pattern, 1337, 13.0, 17.0).to_bits(),
            multiplier(pattern, 42, 13.0, 17.0).to_bits()
        );
    }

    #[test]
    fn zoned_grass_preserves_other_layers_survivor_poses_and_exclusions() {
        // Isolate this historical grass-only regression from the opt-in forest recipe.
        let mut sketch = crate::embedded_visual_valley_world_sketch().unwrap();
        sketch.forest_scatter = None;
        let world = crate::compile_world_sketch(&sketch).unwrap();
        let mut old = world.vegetation.clone();
        let grass = old
            .layers
            .iter_mut()
            .find(|layer| layer.kind == VegetationKind::Grass)
            .unwrap();
        grass.acceptance_probability = 0.32;
        grass.density_pattern = None;
        grass.attempts_per_cell = 4; // Pre-zoning settings with current authored clearings.
        assert_eq!(
            old.content_hash().unwrap().to_string(),
            "af0a4245d68e43f6d54ca2b6bdf40602fa8eb2f984f6366fbef73aedbae0a894"
        );
        let baseline = VegetationGenerationPlan::from_documents(&world.carved_terrain, &old)
            .unwrap()
            .generate()
            .unwrap();
        let plan =
            VegetationGenerationPlan::from_documents(&world.carved_terrain, &world.vegetation)
                .unwrap();
        let result = plan.generate().unwrap();
        let collect = |generated: &super::super::GeneratedVegetation| {
            generated
                .chunks
                .iter()
                .flat_map(|chunk| &chunk.instances)
                .map(|instance| (instance.id, instance.clone()))
                .collect::<BTreeMap<_, _>>()
        };
        let before = collect(&baseline);
        let after = collect(&result);
        for (id, instance) in &before {
            if instance.kind != VegetationKind::Grass || after.contains_key(id) {
                assert_eq!(after.get(id), Some(instance));
            }
        }
        for instance in after.values() {
            assert!(
                !world
                    .vegetation
                    .exclusions
                    .iter()
                    .any(|exclusion| exclusion_contains(
                        exclusion,
                        instance.position_m.x,
                        instance.position_m.z
                    ))
            );
        }
        assert!(result.kind_counts.grass_patches > baseline.kind_counts.grass_patches * 18 / 10);
        assert!(result.kind_counts.grass_patches <= 32_768);
        assert_eq!(result.kind_counts.trees, baseline.kind_counts.trees);
        assert_eq!(result.kind_counts.shrubs, baseline.kind_counts.shrubs);
        assert_eq!(result.kind_counts.ferns, baseline.kind_counts.ferns);
        assert_eq!(
            *result,
            *VegetationGenerationPlan::from_documents(&world.carved_terrain, &world.vegetation)
                .unwrap()
                .generate()
                .unwrap()
        );
        println!(
            "zoned grass: {:?}; vegetation={}",
            result.kind_counts,
            world.vegetation.content_hash().unwrap()
        );
    }

    #[test]
    fn meadow_coverage_extends_the_existing_patches_without_moving_survivors() {
        let world =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let mut old = world.vegetation.clone();
        old.layers
            .iter_mut()
            .find(|layer| layer.kind == VegetationKind::Grass)
            .unwrap()
            .attempts_per_cell = 4;
        let before = VegetationGenerationPlan::from_documents(&world.carved_terrain, &old)
            .unwrap()
            .generate()
            .unwrap();
        let after =
            VegetationGenerationPlan::from_documents(&world.carved_terrain, &world.vegetation)
                .unwrap()
                .generate()
                .unwrap();
        let current: BTreeMap<_, _> = after
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .map(|instance| (instance.id, instance))
            .collect();
        for instance in before.chunks.iter().flat_map(|chunk| &chunk.instances) {
            assert_eq!(current.get(&instance.id), Some(&instance));
        }
        assert_eq!(before.kind_counts.trees, after.kind_counts.trees);
        assert_eq!(before.kind_counts.shrubs, after.kind_counts.shrubs);
        assert_eq!(before.kind_counts.ferns, after.kind_counts.ferns);
        assert!(after.kind_counts.grass_patches > before.kind_counts.grass_patches * 19 / 10);
        assert!(after.kind_counts.grass_patches < before.kind_counts.grass_patches * 21 / 10);
        println!(
            "meadow coverage before={:?}, after={:?}",
            before.kind_counts, after.kind_counts
        );
    }

    #[test]
    fn zero_pattern_cannot_be_revived_by_quality_density() {
        let terrain = crate::VisualValleyTerrainRecipe::default().build().unwrap();
        let mut vegetation = crate::VisualValleyVegetationRecipe::default()
            .build()
            .unwrap();
        vegetation.layers[3].density_pattern = Some(VegetationDensityPattern {
            patch_size_m: 32.0,
            minimum_multiplier: 0.0,
            maximum_multiplier: 0.0,
        });
        let plan = VegetationGenerationPlan::from_documents(&terrain, &vegetation).unwrap();
        assert_eq!(plan.generate().unwrap().kind_counts.grass_patches, 0);
        assert_eq!(
            plan.generate_render_density(2.0)
                .unwrap()
                .generated
                .kind_counts
                .grass_patches,
            0
        );
    }
}
