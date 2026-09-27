//! Exclusion stage only: keeps immutable base records available for undo/reprojection.
use super::{ImportedEntity, ImportedPopulation};
use forge_core::ContentHash;
use forge_project::zone_authoring::ZoneAuthoringHead;
use std::collections::BTreeSet;

pub struct ImportedExclusionView<'a> {
    pub head_hash: ContentHash,
    pub excluded_count: usize,
    pub layers: Vec<ImportedVisibleLayer<'a>>,
}

pub struct ImportedVisibleLayer<'a> {
    pub layer_id: &'a str,
    pub instances: Vec<&'a ImportedEntity>,
}

impl ImportedPopulation {
    /// Apply effective keep-clear shapes and persistent promoted origins to the
    /// immutable population. This stage does not apply density/habitat, road or
    /// building clearances, terrain reprojection, nor create manual placements.
    pub fn exclusion_view(
        &self,
        head: &ZoneAuthoringHead,
    ) -> Result<ImportedExclusionView<'_>, String> {
        head.validate()?;
        if head.base.vegetation.as_ref() != Some(self.document()) {
            return Err("Imported population is stale for this zone base".into());
        }
        let (_, effective) = head.effective()?;
        let effective = effective.ok_or("Imported vegetation absent")?;
        let mut promoted = head
            .corrections
            .promotions
            .as_deref()
            .unwrap_or_default()
            .iter()
            .map(|origin| (origin.layer_id.as_str(), origin.instance_id))
            .collect::<BTreeSet<_>>();
        let mut excluded_count = 0;
        let mut layers = Vec::new();
        for layer in self.layers() {
            let mut instances = Vec::new();
            for entity in layer.instances() {
                let promoted_origin = promoted.remove(&(layer.layer_id.as_str(), entity.id));
                let [x, _, z] = entity.source.position_m;
                let excluded = effective
                    .exclusions
                    .iter()
                    .any(|shape| crate::vegetation::exclusion_contains_f64(shape, x, z));
                if promoted_origin || excluded {
                    excluded_count += 1;
                } else {
                    instances.push(entity);
                }
            }
            layers.push(ImportedVisibleLayer {
                layer_id: &layer.layer_id,
                instances,
            });
        }
        if !promoted.is_empty() {
            return Err("Imported promoted origins require explicit reconciliation".into());
        }
        Ok(ImportedExclusionView {
            head_hash: head.content_hash()?,
            excluded_count,
            layers,
        })
    }
}
