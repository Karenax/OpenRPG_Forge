//! Derived ground-following transforms; immutable imported records are never rewritten.
use super::{ImportedEntity, ImportedPopulation};
use crate::sampled_terrain::SampledTerrainPlan;
use forge_core::ContentHash;
use forge_project::zone_authoring::ZoneAuthoringHead;

pub struct ImportedTerrainView<'a> {
    pub head_hash: ContentHash,
    pub excluded_count: usize,
    pub density_removed_count: usize,
    pub moved_count: usize,
    pub layers: Vec<ImportedTerrainLayer<'a>>,
}

pub struct ImportedTerrainLayer<'a> {
    pub layer_id: &'a str,
    pub instances: Vec<ImportedEntity>,
}

impl ImportedPopulation {
    /// Apply authored visibility, then the height difference between base and
    /// effective sampled terrain. Preserve each source's original ground offset,
    /// identity, X/Z, rotation, scale and prototype parameters. This does not
    /// project manual placements or apply road/river/building modifications.
    pub fn terrain_view(
        &self,
        head: &ZoneAuthoringHead,
        terrain_bytes: &[u8],
    ) -> Result<ImportedTerrainView<'_>, String> {
        let visible = self.authored_view(head)?;
        let (effective, _) = head.effective()?;
        let base = SampledTerrainPlan::from_document(&head.base.terrain, terrain_bytes)
            .map_err(|e| e.to_string())?;
        let edited = SampledTerrainPlan::from_document(&effective, terrain_bytes)
            .map_err(|e| e.to_string())?;
        let mut moved_count = 0;
        let mut layers = Vec::with_capacity(visible.layers.len());
        for layer in visible.layers {
            let mut instances = Vec::with_capacity(layer.instances.len());
            for original in layer.instances {
                let [x, y, z] = original.source.position_m;
                let delta = edited.height_at(x, z).map_err(|e| e.to_string())?
                    - base.height_at(x, z).map_err(|e| e.to_string())?;
                let mut entity = original.clone();
                if delta != 0.0 {
                    let height = y + delta;
                    if !height.is_finite() || height.abs() > f64::from(f32::MAX) {
                        return Err("Projected imported plant exceeds finite render height".into());
                    }
                    entity.source.position_m[1] = height;
                    moved_count += 1;
                }
                instances.push(entity);
            }
            layers.push(ImportedTerrainLayer {
                layer_id: layer.layer_id,
                instances,
            });
        }
        Ok(ImportedTerrainView {
            head_hash: visible.head_hash,
            excluded_count: visible.excluded_count,
            density_removed_count: visible.density_removed_count,
            moved_count,
            layers,
        })
    }
}
