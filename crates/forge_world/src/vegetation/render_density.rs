use super::{
    GeneratedVegetation, SpacingIndex, VegetationGenerationError, VegetationGenerationPlan,
    VegetationKindCounts, attempt_entropy, unit_f64,
};
use std::collections::BTreeSet;

/// Derived presentation only. Gameplay and authoring continue to use `generate()`.
pub struct VegetationRenderDensityProjection {
    pub generated: GeneratedVegetation,
    pub density_multiplier: f32,
    pub canonical_placements: u32,
    pub protected_collision_placements: u32,
}

impl VegetationGenerationPlan {
    /// Thin decorations or add bounded candidates without relocating canonical instances.
    /// Collision-bearing layers never vary with graphics quality.
    pub fn generate_render_density(
        &self,
        density: f32,
    ) -> Result<VegetationRenderDensityProjection, VegetationGenerationError> {
        if !density.is_finite() || !(0.1..=2.0).contains(&density) {
            return Err(VegetationGenerationError::InvalidRenderDensity);
        }
        let canonical = self.generate()?;
        if !self.promoted.is_empty() {
            // Apply the same quality selection before removing promoted origins.
            // Reserving their spacing keeps every unpromoted neighbour identical.
            let mut source = self.clone();
            source.promoted.clear();
            source.generated = std::sync::OnceLock::new();
            let mut result = source.generate_render_density(density)?;
            for chunk in &mut result.generated.chunks {
                chunk.instances.retain(|instance| {
                    !self
                        .promoted
                        .contains(&(instance.layer_id.clone(), instance.id.as_entity_id()))
                });
            }
            recount(&mut result.generated);
            result.canonical_placements = canonical.kind_counts.total();
            result.protected_collision_placements = u32::try_from(
                result
                    .generated
                    .chunks
                    .iter()
                    .flat_map(|c| &c.instances)
                    .filter(|i| {
                        self.vegetation
                            .layers
                            .iter()
                            .any(|l| l.id == i.layer_id && l.collision.is_some())
                    })
                    .count(),
            )
            .map_err(|_| VegetationGenerationError::RenderDensityCountOverflow)?;
            return Ok(result);
        }
        let mut generated = (*canonical).clone();
        let protected: BTreeSet<_> = self
            .vegetation
            .layers
            .iter()
            .filter(|layer| layer.collision.is_some())
            .map(|layer| layer.id.clone())
            .collect();
        let protected_collision_placements = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter(|instance| protected.contains(&instance.layer_id))
            .count();
        if density < 1.0 {
            for chunk in &mut generated.chunks {
                chunk.instances.retain(|instance| {
                    let rank = instance
                        .id
                        .to_string()
                        .bytes()
                        .fold(super::FNV_OFFSET_BASIS, |hash, byte| {
                            (hash ^ u64::from(byte)).wrapping_mul(super::FNV_PRIME)
                        });
                    protected.contains(&instance.layer_id) || unit_f64(rank, 7) < f64::from(density)
                });
            }
        } else if density > 1.0 {
            self.add_render_decorations(&mut generated, density)?;
        }
        recount(&mut generated);
        Ok(VegetationRenderDensityProjection {
            generated,
            density_multiplier: density,
            canonical_placements: canonical.kind_counts.total(),
            protected_collision_placements: u32::try_from(protected_collision_placements)
                .map_err(|_| VegetationGenerationError::RenderDensityCountOverflow)?,
        })
    }

    fn add_render_decorations(
        &self,
        generated: &mut GeneratedVegetation,
        density: f32,
    ) -> Result<(), VegetationGenerationError> {
        let existing: BTreeSet<_> = generated
            .chunks
            .iter()
            .flat_map(|chunk| chunk.instances.iter().map(|instance| instance.id))
            .collect();
        for (layer_index, layer) in self.vegetation.layers.iter().enumerate() {
            if layer.collision.is_some() || layer.acceptance_probability == 0.0 {
                continue;
            }
            let mut spacing = SpacingIndex::new(layer.minimum_spacing_m);
            for instance in generated
                .chunks
                .iter()
                .flat_map(|chunk| &chunk.instances)
                .filter(|instance| instance.layer_id == layer.id)
            {
                spacing.accept([instance.position_m.x, instance.position_m.z]);
            }
            let mut candidates = Vec::new();
            for z in 0..self.cells_per_axis {
                for x in 0..self.cells_per_axis {
                    for attempt in 0..layer.attempts_per_cell {
                        let acceptance = self.attempt_acceptance(layer, x, z, attempt);
                        if acceptance <= 0.0 {
                            continue;
                        }
                        let rank = unit_f64(
                            attempt_entropy(self.vegetation.seed, layer.id.as_str(), x, z, attempt),
                            0,
                        ) / acceptance;
                        if rank >= f64::from(density) {
                            continue;
                        }
                        if let Some(instance) = self.generate_attempt_with_acceptance(
                            layer_index,
                            layer,
                            x,
                            z,
                            attempt,
                            1.0,
                        )? && !existing.contains(&instance.id)
                        {
                            candidates.push((rank, self.owner_chunk(x, z), instance));
                        }
                    }
                }
            }
            // Fixed rank order makes increasing density append candidates, not displace them.
            candidates.sort_by(|left, right| left.0.total_cmp(&right.0));
            for (_, coordinate, instance) in candidates {
                if spacing.accept([instance.position_m.x, instance.position_m.z]) {
                    let index =
                        usize::from(coordinate.z * super::TERRAIN_CHUNKS_PER_AXIS + coordinate.x);
                    generated.chunks[index].instances.push(instance);
                }
            }
        }
        Ok(())
    }
}

fn recount(generated: &mut GeneratedVegetation) {
    generated.kind_counts = VegetationKindCounts::default();
    for chunk in &mut generated.chunks {
        chunk.kind_counts = VegetationKindCounts::default();
        for instance in &chunk.instances {
            chunk.kind_counts.increment(instance.kind);
        }
        generated.kind_counts.add(chunk.kind_counts);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn density_is_nested_preserves_colliders_and_never_changes_sources() {
        let world =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let plan =
            VegetationGenerationPlan::from_documents(&world.carved_terrain, &world.vegetation)
                .unwrap();
        let canonical = plan.generate().unwrap();
        let base_hash = canonical.content_hash().unwrap();
        let protected: BTreeSet<_> = world
            .vegetation
            .layers
            .iter()
            .filter(|layer| layer.collision.is_some())
            .map(|layer| layer.id.clone())
            .collect();
        let mut previous = std::collections::BTreeMap::new();
        for density in [0.1, 0.5, 0.75, 1.0, 1.25, 2.0] {
            let selection = plan.generate_render_density(density).unwrap();
            let instances: std::collections::BTreeMap<_, _> = selection
                .generated
                .chunks
                .iter()
                .flat_map(|chunk| {
                    chunk
                        .instances
                        .iter()
                        .map(|instance| (instance.id, instance.clone()))
                })
                .collect();
            assert_eq!(
                instances.len(),
                selection.generated.kind_counts.total() as usize
            );
            for (id, instance) in &previous {
                assert_eq!(instances.get(id), Some(instance));
            }
            for instance in canonical
                .chunks
                .iter()
                .flat_map(|chunk| &chunk.instances)
                .filter(|instance| protected.contains(&instance.layer_id))
            {
                assert_eq!(instances.get(&instance.id), Some(instance));
            }
            assert_eq!(
                selection.generated.vegetation_document_hash,
                canonical.vegetation_document_hash
            );
            assert_eq!(
                selection.generated.terrain_document_hash,
                canonical.terrain_document_hash
            );
            if density.to_bits() == 1.0_f32.to_bits() {
                assert_eq!(selection.generated.content_hash().unwrap(), base_hash);
            }
            assert_eq!(
                selection.generated.content_hash().unwrap(),
                plan.generate_render_density(density)
                    .unwrap()
                    .generated
                    .content_hash()
                    .unwrap()
            );
            previous = instances;
            println!(
                "density={density} canonical={} selected={} protected={}",
                selection.canonical_placements,
                selection.generated.kind_counts.total(),
                selection.protected_collision_placements
            );
        }
        assert!(previous.len() > canonical.kind_counts.total() as usize);
        assert_eq!(plan.generate().unwrap().content_hash().unwrap(), base_hash);
        for density in [f32::NAN, f32::INFINITY, -1.0, 0.0, 2.1] {
            assert!(plan.generate_render_density(density).is_err());
        }
    }
}
