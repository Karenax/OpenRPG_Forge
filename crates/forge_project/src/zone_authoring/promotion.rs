//! Persistent generated origins; the source resolver lives in the world adapter.
use super::{
    ZoneAuthoringHead,
    placements::ManualPlacement,
    session::{ZoneAction, ZoneRequest},
};
use crate::{VegetationKind, VegetationLayerId};
use forge_core::{CommandId, ContentHash, EntityId};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PromotedInstance {
    pub layer_id: VegetationLayerId,
    pub instance_id: EntityId,
    /// Evidence of the generated source at promotion, not a mutable source cache.
    pub generation_hash: ContentHash,
    /// Retained even if the author later deletes the manual placement.
    pub placement_id: EntityId,
}

/// Explicit replacements in a new recipe; manual placements are never part of this payload.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PromotionReconciliation {
    pub base: super::ZoneBase,
    pub origins: Vec<PromotedInstance>,
}

pub(super) fn reconcile(
    head: &ZoneAuthoringHead,
    proposal: &PromotionReconciliation,
) -> Result<super::ZoneCorrections, String> {
    let review = head.review_regeneration(proposal.base.clone())?;
    if let Some(reason) = review.refusal_message() {
        return Err(reason);
    }
    let previous = head.corrections.promotions.as_deref().unwrap_or_default();
    let identities = |origins: &[PromotedInstance]| {
        origins
            .iter()
            .map(|o| o.placement_id)
            .collect::<std::collections::BTreeSet<_>>()
    };
    if previous.is_empty()
        || identities(previous) != identities(&proposal.origins)
        || previous.len() != proposal.origins.len()
    {
        return Err("Reconciliation must retain every promoted placement identity".into());
    }
    let mut candidate = head.clone();
    candidate.base = proposal.base.clone();
    candidate.corrections.promotions = Some(proposal.origins.clone());
    candidate.validate()?;
    Ok(candidate.corrections)
}

pub(super) fn validate(
    head: &ZoneAuthoringHead,
    origins: &[PromotedInstance],
) -> Result<(), String> {
    if origins.len() > super::placements::MAX_MANUAL_PLACEMENTS {
        return Err("Generated promotion limit exceeded".into());
    }
    let mut previous = None;
    let mut placements = std::collections::BTreeSet::new();
    for origin in origins {
        let key = (&origin.layer_id, origin.instance_id);
        if previous.is_some_and(|old| old >= key) || !placements.insert(origin.placement_id) {
            return Err(
                "Promoted origins must be sorted and unique, with distinct placements".into(),
            );
        }
        previous = Some(key);
        if !head.base.vegetation.as_ref().is_some_and(|v| {
            v.layers
                .iter()
                .any(|l| l.id == origin.layer_id && l.kind == VegetationKind::Tree)
        }) {
            return Err("Promoted tree source layer is absent".into());
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    /// Replace only origin correspondences, together with the reviewed recipe.
    /// The world adapter must verify the selected new generated identities.
    pub fn prepare_promotion_reconciliation(
        &self,
        proposal: PromotionReconciliation,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        reconcile(self, &proposal)?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash: self.content_hash()?,
            action: ZoneAction::ReconcilePromotions(Box::new(proposal)),
        })
    }

    /// Atomic proposal only. Caller must resolve the generated instance and asset pin.
    pub fn prepare_promotion(
        &self,
        origin: PromotedInstance,
        placement: ManualPlacement,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        if origin.placement_id != placement.id {
            return Err("Promotion and placement identities differ".into());
        }
        let mut corrections = self.corrections.clone();
        let placements = corrections.placements.get_or_insert_with(Vec::new);
        if placements.iter().any(|p| p.id == placement.id) {
            return Err("Promotion placement already exists".into());
        }
        placements.push(placement);
        placements.sort_by_key(|p| p.id);
        let origins = corrections.promotions.get_or_insert_with(Vec::new);
        origins.push(origin);
        origins.sort_by(|a, b| (&a.layer_id, a.instance_id).cmp(&(&b.layer_id, b.instance_id)));
        let mut candidate = self.clone();
        candidate.corrections = corrections.clone();
        candidate.version = corrections.schema_version();
        candidate.validate()?;
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash: self.content_hash()?,
            action: ZoneAction::edit(corrections),
        })
    }
}
