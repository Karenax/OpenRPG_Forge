//! Governed gameplay visuals use the same canonical zone and reviewed command history.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use crate::module_gameplay_visuals::{self, GameplayVisualBinding, VisualAssetPin};
use forge_core::{CommandId, EntityId};

/// Canonical validation proves shape and target identity. Native admission must
/// additionally resolve these exact pins against its governed asset authority.
pub(super) fn validate(
    head: &ZoneAuthoringHead,
    bindings: &[GameplayVisualBinding],
) -> Result<(), String> {
    if bindings.is_empty() {
        return Ok(());
    }
    let document = head
        .corrections
        .gameplay
        .as_deref()
        .ok_or("Gameplay visual bindings require canonical gameplay definitions")?;
    let pins = bindings
        .iter()
        .map(|binding| binding.asset)
        .collect::<Vec<_>>();
    module_gameplay_visuals::validate_for_document(bindings, document, &pins)
}

impl ZoneAuthoringHead {
    pub fn prepare_gameplay_visuals(
        &self,
        bindings: Option<Vec<GameplayVisualBinding>>,
        assets: &[VisualAssetPin],
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        if let Some(bindings) = &bindings {
            validate(self, bindings)?;
            if let Some(document) = self.corrections.gameplay.as_deref() {
                module_gameplay_visuals::validate_for_document(bindings, document, assets)?;
            }
        }
        let mut corrections = self.corrections.clone();
        corrections.gameplay_visuals = bindings;
        Ok(ZoneRequest {
            command_id,
            expected_hash: self.content_hash()?,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }

    pub fn prepare_gameplay_visual_replacement(
        &self,
        entities: &[EntityId],
        expected: VisualAssetPin,
        replacement: VisualAssetPin,
        assets: &[VisualAssetPin],
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        let bindings = self
            .corrections
            .gameplay_visuals
            .as_deref()
            .unwrap_or_default();
        let replaced = module_gameplay_visuals::replace_asset(
            bindings,
            self.base.terrain.area_id,
            entities,
            expected,
            replacement,
            assets,
        )?;
        self.prepare_gameplay_visuals(Some(replaced), assets, command_id)
    }
}
