//! Compare proposed recipe outputs against persistent authoring, before acceptance.
use super::{
    ZoneAuthoringHead, ZoneBase, ZoneCorrections,
    session::{ZoneAction, ZoneRequest},
};
use crate::VegetationLayerId;
use forge_core::{AssetId, CommandId, ContentHash, EntityId, Revision};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum RegenerationConflict {
    ZoneIdentityChanged,
    TerrainGridChanged,
    /// Current height edits are deltas; rebasing them requires an explicit preservation policy.
    TerrainHeightReferenceChanged,
    DensityLayerRemoved {
        layer_id: VegetationLayerId,
        locked: bool,
    },
    ImportedPlantSourceChanged,
    BuildingConstraintChanged {
        building_id: EntityId,
        locked: bool,
        reason: String,
    },
    InvalidEffectiveDocument {
        reason: String,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RegenerationReview {
    pub source_hash: ContentHash,
    pub source_revision: Revision,
    pub previous_base: ZoneBase,
    pub proposed_base: ZoneBase,
    pub retained_corrections: ZoneCorrections,
    pub effective: Option<ZoneAuthoringHead>,
    pub conflicts: Vec<RegenerationConflict>,
}

impl RegenerationReview {
    /// Same actionable refusal for preview preparation and raw command execution.
    /// Keep diagnostic codes without exposing a Rust debug dump as the explanation.
    pub fn refusal_message(&self) -> Option<String> {
        if self.conflicts.is_empty() {
            return None;
        }
        let details = self
            .conflicts
            .iter()
            .map(RegenerationConflict::explanation)
            .collect::<Vec<_>>()
            .join("\n");
        Some(format!(
            "Régénération refusée ; aucune retouche appliquée.\n{details}"
        ))
    }
}

impl RegenerationConflict {
    fn explanation(&self) -> String {
        match self {
            Self::ZoneIdentityChanged => "[ZoneIdentityChanged] La proposition vise une autre zone ou un autre projet. Régénérer depuis la zone courante, sans changer ses identifiants.".into(),
            Self::TerrainGridChanged => "[TerrainGridChanged] La taille, la grille ou le schéma du terrain change. Conserver ces paramètres pour garder les retouches à leurs emplacements.".into(),
            Self::TerrainHeightReferenceChanged => "[TerrainHeightReferenceChanged] Le relief possède des retouches relatives. Changer sa seed ou sa recette déplacerait leur surface de référence ; le rebasage exact n’est pas disponible. Conserver la recette de relief ; une régénération de végétation seule reste possible si ses autres contraintes sont satisfaites.".into(),
            Self::DensityLayerRemoved { layer_id, locked } => format!("[DensityLayerRemoved] La couche {layer_id} possède un masque auteur (verrou {}). La proposition la supprime. Conserver son identifiant dans la recette ; déverrouiller n’efface pas le masque et ne résout pas sa disparition.", if *locked { "actif" } else { "inactif" }),
            Self::ImportedPlantSourceChanged => "[ImportedPlantSourceChanged] Des plantes importées ont été déplacées ou copiées. La proposition change leurs blocs source ; réconcilier les clés de plantes avant de remplacer ces blocs pour conserver les retouches.".into(),
            Self::BuildingConstraintChanged { building_id, locked, reason } => format!("[BuildingConstraintChanged] Le bâtiment {building_id} ne tient plus dans les contraintes proposées (verrou du bâtiment entier {}). Adapter la proposition ou retoucher explicitement le bâtiment avant de réexaminer. Détail : {reason}", if *locked { "actif" } else { "inactif" }),
            Self::InvalidEffectiveDocument { reason } => format!("[InvalidEffectiveDocument] La recette et les retouches ne forment pas une zone valide. Corriger la proposition ou la retouche concernée, puis réexaminer. Détail : {reason}"),
        }
    }
}

fn imported_plant_asset_id(base: &ZoneBase, layer: &crate::VegetationLayerId) -> AssetId {
    AssetId::deterministic(format!(
        "imported-instance:{}:{}:{}",
        base.terrain.project_id,
        base.terrain.area_id,
        layer.as_str()
    ))
}

fn has_imported_plant_object_edits(head: &ZoneAuthoringHead) -> bool {
    let Some(vegetation) = head.base.vegetation.as_ref() else {
        return false;
    };
    let Some(edits) = head.corrections.source_object_edits.as_deref() else {
        return false;
    };
    edits.iter().any(|edit| {
        vegetation
            .layers
            .iter()
            .any(|layer| imported_plant_asset_id(&head.base, &layer.id) == edit.source.asset_id)
    })
}

impl ZoneAuthoringHead {
    pub fn review_regeneration(
        &self,
        proposed_base: ZoneBase,
    ) -> Result<RegenerationReview, String> {
        let source_hash = self.content_hash()?;
        let before = &self.base.terrain;
        let after = &proposed_base.terrain;
        let mut conflicts = Vec::new();
        if before.project_id != after.project_id || before.area_id != after.area_id {
            conflicts.push(RegenerationConflict::ZoneIdentityChanged);
        }
        if before.extent_m != after.extent_m
            || before.height_sampling.sample_spacing_m.to_bits()
                != after.height_sampling.sample_spacing_m.to_bits()
            || before.schema_version != after.schema_version
        {
            conflicts.push(RegenerationConflict::TerrainGridChanged);
        }
        if self
            .corrections
            .height_deltas
            .as_ref()
            .is_some_and(|deltas| !deltas.is_empty())
            && (before.seed != after.seed || before.height_sampling != after.height_sampling)
        {
            conflicts.push(RegenerationConflict::TerrainHeightReferenceChanged);
        }
        for correction in &self.corrections.density {
            if proposed_base
                .vegetation
                .as_ref()
                .is_none_or(|v| !v.layers.iter().any(|l| l.id == correction.layer_id))
            {
                conflicts.push(RegenerationConflict::DensityLayerRemoved {
                    layer_id: correction.layer_id.clone(),
                    locked: correction.locked,
                });
            }
        }
        if (has_imported_plant_object_edits(self)
            || self
                .corrections
                .imported_plant_offsets
                .as_ref()
                .is_some_and(|offsets| !offsets.is_empty())
            || self
                .corrections
                .imported_plant_placements
                .as_ref()
                .is_some_and(|placements| !placements.is_empty()))
            && self
                .base
                .vegetation
                .as_ref()
                .and_then(|vegetation| vegetation.imported_source.as_ref())
                != proposed_base
                    .vegetation
                    .as_ref()
                    .and_then(|vegetation| vegetation.imported_source.as_ref())
        {
            conflicts.push(RegenerationConflict::ImportedPlantSourceChanged);
        }
        let mut candidate = self.clone();
        for building in self.corrections.buildings.as_deref().unwrap_or_default() {
            if let Err(reason) =
                super::buildings::validate(std::slice::from_ref(building), &proposed_base.terrain)
            {
                conflicts.push(RegenerationConflict::BuildingConstraintChanged {
                    building_id: building.id,
                    locked: building.locked,
                    reason,
                });
            }
        }
        candidate.base = proposed_base.clone();
        if let Err(reason) = candidate.to_bytes() {
            conflicts.push(RegenerationConflict::InvalidEffectiveDocument { reason });
        }
        let effective = conflicts.is_empty().then_some(candidate);
        Ok(RegenerationReview {
            source_hash,
            source_revision: self.revision,
            previous_base: self.base.clone(),
            proposed_base,
            retained_corrections: self.corrections.clone(),
            effective,
            conflicts,
        })
    }

    pub fn prepare_regeneration(
        &self,
        proposed_base: ZoneBase,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        let review = self.review_regeneration(proposed_base)?;
        if let Some(message) = review.refusal_message() {
            return Err(message);
        }
        Ok(ZoneRequest {
            command_id,
            expected_hash: review.source_hash,
            expected_revision: review.source_revision,
            action: ZoneAction::Regenerate(Box::new(review.proposed_base)),
        })
    }
}
