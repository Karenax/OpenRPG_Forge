//! Immutable catalog references, not a second mutable catalog or renderer document.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::{VegetationDocument, VegetationKind, VegetationLayerId};
use forge_core::{CommandId, ContentHash};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SpeciesReference {
    /// Hash of the complete immutable source catalog, including normalization metadata.
    pub catalog_sha256: ContentHash,
    pub source_layer_id: VegetationLayerId,
    pub asset_id: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LayerSpecies {
    pub layer_id: VegetationLayerId,
    pub prototypes: Vec<SpeciesReference>,
}

/// Prepare a shared tree-species draft without mutating the head or its masks.
/// Catalog bytes remain the adapter's responsibility before command submission.
pub fn spread_to_tree_layers(
    head: &ZoneAuthoringHead,
    draft: &[LayerSpecies],
    source: &VegetationLayerId,
) -> Result<Vec<LayerSpecies>, String> {
    head.validate()?;
    validate(draft, head.base.vegetation.as_ref())?;
    let prototypes = &draft
        .iter()
        .find(|layer| &layer.layer_id == source)
        .ok_or("Choisir au moins une espèce sur cette couche d’abord")?
        .prototypes;
    let vegetation = head.base.vegetation.as_ref().ok_or("Végétation absente")?;
    let mut choices: Vec<_> = vegetation
        .layers
        .iter()
        .filter(|layer| layer.kind == VegetationKind::Tree)
        .map(|layer| LayerSpecies {
            layer_id: layer.id.clone(),
            prototypes: prototypes.clone(),
        })
        .collect();
    choices.sort_by(|a, b| a.layer_id.cmp(&b.layer_id));
    validate(&choices, Some(vegetation))?;
    Ok(choices)
}

pub(super) fn validate(
    selections: &[LayerSpecies],
    vegetation: Option<&VegetationDocument>,
) -> Result<(), String> {
    if selections.len() > 64 {
        return Err("Species layer limit exceeded".into());
    }
    let mut previous = None;
    let mut count = 0;
    for selection in selections {
        let id = selection.layer_id.as_str();
        if previous.is_some_and(|p| p >= id) {
            return Err("Species layers must have unique sorted IDs".into());
        }
        previous = Some(id);
        if !vegetation.is_some_and(|v| {
            v.layers
                .iter()
                .any(|l| l.id == selection.layer_id && l.kind == VegetationKind::Tree)
        }) {
            return Err("Orphan species selection: target tree layer absent".into());
        }
        count += selection.prototypes.len();
        if selection.prototypes.is_empty() || count > 64 {
            return Err("Species selection must contain 1 to 64 prototypes in total".into());
        }
        let mut assets = std::collections::BTreeSet::new();
        for prototype in &selection.prototypes {
            for id in [
                prototype.source_layer_id.as_str(),
                prototype.asset_id.as_str(),
            ] {
                if id.is_empty()
                    || id.len() > 128
                    || id.trim() != id
                    || id.chars().any(char::is_control)
                {
                    return Err("Species source IDs must be nonempty and bounded".into());
                }
            }
            if !assets.insert(&prototype.asset_id) {
                return Err("Duplicate species asset on target layer".into());
            }
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    /// None removes the override for this layer. Actual catalog bytes are resolved by adapters.
    pub fn prepare_species(
        &self,
        layer_id: VegetationLayerId,
        prototypes: Option<Vec<SpeciesReference>>,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        let mut corrections = self.corrections.clone();
        let selections = corrections.species.get_or_insert_with(Vec::new);
        selections.retain(|s| s.layer_id != layer_id);
        if let Some(mut prototypes) = prototypes {
            prototypes.sort_by(|a, b| a.asset_id.cmp(&b.asset_id));
            selections.push(LayerSpecies {
                layer_id,
                prototypes,
            });
        }
        selections.sort_by(|a, b| a.layer_id.cmp(&b.layer_id));
        if self.corrections.species.as_deref().unwrap_or_default() == selections.as_slice() {
            return Err("Species selection is unchanged".into());
        }
        validate(selections, self.base.vegetation.as_ref())?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash: self.content_hash()?,
            action: ZoneAction::edit(corrections),
        })
    }
}
