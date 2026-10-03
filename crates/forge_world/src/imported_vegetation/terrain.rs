//! Derived ground-following transforms; immutable imported records are never rewritten.
use super::{ImportedEntity, ImportedPopulation};
use crate::sampled_terrain::SampledTerrainPlan;
use forge_core::ContentHash;
use forge_project::zone_authoring::ZoneAuthoringHead;
use forge_project::zone_authoring::imported_plants::ImportedPlantPlacement;
use std::collections::BTreeSet;

pub struct ImportedTerrainView<'a> {
    pub head_hash: ContentHash,
    pub excluded_count: usize,
    pub density_removed_count: usize,
    pub moved_count: usize,
    pub placed_count: usize,
    pub layers: Vec<ImportedTerrainLayer<'a>>,
}

pub struct ImportedTerrainLayer<'a> {
    pub layer_id: &'a str,
    pub instances: Vec<ImportedEntity>,
}

impl ImportedPopulation {
    /// Resolve every authored copy against its immutable template, even when
    /// that template is excluded or thinned from the current view.
    pub fn validate_plant_placements(&self, head: &ZoneAuthoringHead) -> Result<(), String> {
        head.validate()?;
        let placements = head
            .corrections
            .imported_plant_placements
            .as_deref()
            .unwrap_or_default();
        if placements.is_empty() {
            return Ok(());
        }
        let base_ids: BTreeSet<_> = self
            .layers()
            .iter()
            .flat_map(|layer| layer.instances().iter().map(|entity| entity.id))
            .collect();
        let extent = self.document().extent_m;
        for placement in placements {
            if base_ids.contains(&placement.id) {
                return Err("Imported plant placement reuses a source ID".into());
            }
            let layer = self
                .layers()
                .iter()
                .find(|layer| layer.layer_id == placement.layer_id.as_str())
                .ok_or("Imported plant placement source layer disappeared")?;
            let index = layer
                .instances()
                .binary_search_by_key(&placement.source_key, |entity| entity.source.source_key)
                .map_err(|_| "Imported plant placement source key disappeared")?;
            let source = &layer.instances()[index].source;
            let factor = placement.scale_factor.unwrap_or(1.0);
            if source
                .scale
                .iter()
                .any(|value| !(*value * factor).is_finite() || *value * factor > 10_000.0)
            {
                return Err("Authored imported plant scale exceeds the source format".into());
            }
            let [x, _, z] = layer.instances()[index].source.position_m;
            let target_x = x + placement.translation_m[0];
            let target_z = z + placement.translation_m[2];
            if !target_x.is_finite()
                || !target_z.is_finite()
                || target_x.abs() > f64::from(extent.size_x_m) * 0.5
                || target_z.abs() > f64::from(extent.size_z_m) * 0.5
            {
                return Err(
                    "Authored imported plant placement lies outside the vegetation extent".into(),
                );
            }
        }
        Ok(())
    }

    /// Check all author offsets, including plants currently excluded from view.
    /// This is also used before Studio submits a zone preview.
    pub fn validate_plant_offsets(&self, head: &ZoneAuthoringHead) -> Result<(), String> {
        head.validate()?;
        let offsets = head
            .corrections
            .imported_plant_offsets
            .as_deref()
            .unwrap_or_default();
        let extent = self.document().extent_m;
        for correction in offsets {
            let Some(layer) = self
                .layers()
                .iter()
                .find(|layer| layer.layer_id == correction.layer_id.as_str())
            else {
                return Err("Imported plant offset source layer disappeared".into());
            };
            let index = layer
                .instances()
                .binary_search_by_key(&correction.source_key, |entity| entity.source.source_key)
                .map_err(|_| "Imported plant offset source key disappeared")?;
            let [x, _, z] = layer.instances()[index].source.position_m;
            let target_x = x + correction.translation_m[0];
            let target_z = z + correction.translation_m[2];
            if !target_x.is_finite()
                || !target_z.is_finite()
                || target_x.abs() > f64::from(extent.size_x_m) * 0.5
                || target_z.abs() > f64::from(extent.size_z_m) * 0.5
            {
                return Err("Authored imported plant lies outside the vegetation extent".into());
            }
        }
        Ok(())
    }

    /// Apply authored visibility, then the height difference between base and
    /// effective sampled terrain at the authored X/Z offset. Preserve each
    /// source's original ground clearance and prototype parameters. Copies may
    /// override yaw and uniform scale. This does not project manual placements or apply
    /// road/river/building modifications.
    pub fn terrain_view(
        &self,
        head: &ZoneAuthoringHead,
        terrain_bytes: &[u8],
    ) -> Result<ImportedTerrainView<'_>, String> {
        let visible = self.authored_view(head)?;
        self.validate_plant_offsets(head)?;
        self.validate_plant_placements(head)?;
        let offsets = head
            .corrections
            .imported_plant_offsets
            .as_deref()
            .unwrap_or_default();
        let (effective, _) = head.effective()?;
        let base = SampledTerrainPlan::from_document(&head.base.terrain, terrain_bytes)
            .map_err(|e| e.to_string())?;
        let edited = SampledTerrainPlan::from_document(&effective, terrain_bytes)
            .map_err(|e| e.to_string())?;
        let project = |original: &ImportedEntity, offset: [f64; 3]| {
            let [x, y, z] = original.source.position_m;
            let target_x = x + offset[0];
            let target_z = z + offset[2];
            let extent = self.document().extent_m;
            if !target_x.is_finite()
                || !target_z.is_finite()
                || target_x.abs() > f64::from(extent.size_x_m) * 0.5
                || target_z.abs() > f64::from(extent.size_z_m) * 0.5
            {
                return Err("Authored imported plant lies outside the vegetation extent".into());
            }
            let delta = edited
                .height_at(target_x, target_z)
                .map_err(|e| e.to_string())?
                - base.height_at(x, z).map_err(|e| e.to_string())?;
            let height = y + delta + offset[1];
            if !height.is_finite() || height.abs() > f64::from(f32::MAX) {
                return Err("Projected imported plant exceeds finite render height".into());
            }
            let mut entity = original.clone();
            entity.source.position_m = [target_x, height, target_z];
            Ok::<_, String>(entity)
        };
        let mut moved_count = 0;
        let mut layers = Vec::with_capacity(visible.layers.len());
        for layer in visible.layers {
            let mut instances = Vec::with_capacity(layer.instances.len());
            for original in layer.instances {
                let key = (layer.layer_id, original.source.source_key);
                let offset = offsets
                    .binary_search_by(|correction| {
                        (correction.layer_id.as_str(), correction.source_key).cmp(&key)
                    })
                    .ok()
                    .map_or([0.0; 3], |index| offsets[index].translation_m);
                let entity = project(original, offset)?;
                if entity
                    .source
                    .position_m
                    .iter()
                    .zip(original.source.position_m)
                    .any(|(after, before)| after.to_bits() != before.to_bits())
                {
                    moved_count += 1;
                }
                instances.push(entity);
            }
            layers.push(ImportedTerrainLayer {
                layer_id: layer.layer_id,
                instances,
            });
        }
        let placements = head
            .corrections
            .imported_plant_placements
            .as_deref()
            .unwrap_or_default();
        for placement in placements {
            let source = self
                .layers()
                .iter()
                .find(|layer| layer.layer_id == placement.layer_id.as_str())
                .ok_or("Imported plant placement source layer disappeared")?;
            let index = source
                .instances()
                .binary_search_by_key(&placement.source_key, |entity| entity.source.source_key)
                .map_err(|_| "Imported plant placement source key disappeared")?;
            let mut entity = project(&source.instances()[index], placement.translation_m)?;
            entity.id = placement.id;
            apply_appearance(&mut entity, placement);
            layers
                .iter_mut()
                .find(|layer| layer.layer_id == placement.layer_id.as_str())
                .ok_or("Imported plant placement output layer disappeared")?
                .instances
                .push(entity);
        }
        self.apply_source_object_edits(head, &mut layers, project)?;
        Ok(ImportedTerrainView {
            head_hash: visible.head_hash,
            excluded_count: visible.excluded_count,
            density_removed_count: visible.density_removed_count,
            moved_count,
            placed_count: placements.len(),
            layers,
        })
    }

    /// Apply sparse overrides after ground projection, preserving copies whose
    /// immutable source is hidden and reserving all imported source identities.
    fn apply_source_object_edits(
        &self,
        head: &ZoneAuthoringHead,
        layers: &mut [ImportedTerrainLayer<'_>],
        project: impl Fn(&ImportedEntity, [f64; 3]) -> Result<ImportedEntity, String>,
    ) -> Result<(), String> {
        let edits = head
            .corrections
            .source_object_edits
            .as_deref()
            .unwrap_or_default();
        let offsets = head
            .corrections
            .imported_plant_offsets
            .as_deref()
            .unwrap_or_default();
        for layer in layers.iter_mut() {
            let asset = super::editable::plant_asset_id(head, layer.layer_id);
            let visible_ids: BTreeSet<_> = layer.instances.iter().map(|entity| entity.id).collect();
            let mut hidden = Vec::new();
            for record in edits.iter().filter(|edit| {
                edit.source.asset_id == asset && !visible_ids.contains(&edit.source.id)
            }) {
                let original = self
                    .layers()
                    .iter()
                    .find(|source| source.layer_id == layer.layer_id)
                    .and_then(|source| {
                        source
                            .instances()
                            .iter()
                            .find(|entity| entity.id == record.source.id)
                    });
                if original.is_none() {
                    return Err(
                        "Edited imported object source disappeared; reconcile the import".into(),
                    );
                }
                if let Some(original) = original {
                    let key = (layer.layer_id, original.source.source_key);
                    let offset = offsets
                        .binary_search_by(|correction| {
                            (correction.layer_id.as_str(), correction.source_key).cmp(&key)
                        })
                        .ok()
                        .map_or([0.0; 3], |index| offsets[index].translation_m);
                    hidden.push(project(original, offset)?);
                }
            }
            layer.instances = super::editable::apply(
                head,
                layer.layer_id,
                std::mem::take(&mut layer.instances),
                hidden,
            )?;
        }
        let source_ids: BTreeSet<_> = self
            .layers()
            .iter()
            .flat_map(|layer| layer.instances().iter().map(|entity| entity.id))
            .collect();
        for record in edits {
            if record
                .copies
                .iter()
                .any(|copy| source_ids.contains(&copy.id))
            {
                return Err("Imported object copy reuses an immutable source identity".into());
            }
        }
        let mut identities = BTreeSet::new();
        if layers
            .iter()
            .flat_map(|layer| &layer.instances)
            .any(|entity| !identities.insert(entity.id))
        {
            return Err("Imported object identity collides with another source or copy".into());
        }
        Ok(())
    }
}

fn apply_appearance(entity: &mut ImportedEntity, placement: &ImportedPlantPlacement) {
    if let Some(yaw) = placement.yaw_degrees {
        entity.source.rotation_xyzw = rotated_yaw(entity.source.rotation_xyzw, yaw);
    }
    if let Some(factor) = placement.scale_factor {
        entity.source.scale = entity.source.scale.map(|value| value * factor);
    }
}

fn rotated_yaw(source: [f64; 4], degrees: f64) -> [f64; 4] {
    let [x, y, z, w] = source;
    let (sin, cos) = (degrees.to_radians() * 0.5).sin_cos();
    let result = [
        cos * x + sin * z,
        cos * y + sin * w,
        cos * z - sin * x,
        cos * w - sin * y,
    ];
    let length = result.iter().map(|value| value * value).sum::<f64>().sqrt();
    result.map(|value| value / length)
}
