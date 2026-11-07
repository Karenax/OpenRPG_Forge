//! Source-light dependency discovery includes authored history, not only visibility.
use super::ZoneEditSession;
use crate::zone_authoring::placements::ManualPlacement;
use forge_core::ContentHash;
use std::collections::BTreeMap;

impl ZoneEditSession {
    /// Manifest-to-descriptor pins required by checkpoints, current state and
    /// both sides of retained edits. Removing a visible companion does not
    /// remove the immutable package needed to undo that edit.
    pub fn required_local_light_packages(
        &self,
    ) -> Result<BTreeMap<ContentHash, ContentHash>, String> {
        let mut required = BTreeMap::new();
        self.visit_local_light_placements(|placement| {
            let reference = placement
                .local_lights
                .as_ref()
                .ok_or("Source-light reference absent")?;
            reference.validate(placement.asset_hash)?;
            if required
                .insert(reference.manifest_sha256, reference.descriptor_sha256)
                .is_some_and(|old| old != reference.descriptor_sha256)
            {
                return Err(
                    "Conflicting descriptors for the same immutable source-light manifest".into(),
                );
            }
            Ok(())
        })?;
        Ok(required)
    }

    /// Visit every light-bearing carrier needed by the complete archive.
    pub fn visit_local_light_placements(
        &self,
        mut visit: impl FnMut(&ManualPlacement) -> Result<(), String>,
    ) -> Result<(), String> {
        self.visit_referenced_placements(|placement| {
            if placement.local_lights.is_some() {
                visit(placement)?;
            }
            Ok(())
        })
    }
}
