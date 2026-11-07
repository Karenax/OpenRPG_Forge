//! Exact placement and companion dependencies across the retained archive.
use super::{ZoneAction, ZoneEditSession};
use crate::zone_authoring::{
    ZoneCorrections, imported_plants,
    placements::{self, ManualPlacement},
    source_objects::SourceObjectState,
};
use forge_core::{AssetId, ContentHash};
use std::collections::BTreeSet;

/// A saved companion belongs to this exact asset and logical model revision.
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd)]
pub struct SourceCompanionPin {
    pub asset_id: AssetId,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub package_sha256: ContentHash,
}

/// Both kinds retain every revision required by undo and redo.
#[derive(Debug, Default, Eq, PartialEq)]
pub struct SourceCompanionRequirements {
    pub physics: BTreeSet<SourceCompanionPin>,
    pub particles: BTreeSet<SourceCompanionPin>,
}

impl ZoneEditSession {
    /// Visit the current head, checkpoint and both sides of every retained edit.
    /// A placement may be visited more than once; callers own deduplication.
    pub fn visit_referenced_placements(
        &self,
        mut visit: impl FnMut(&ManualPlacement) -> Result<(), String>,
    ) -> Result<(), String> {
        self.visit_placements(|placement, _| visit(placement))
    }

    /// Model dependencies exclude exact imported instance-block references from
    /// source edits. Ordinary placements still require a declared model, even
    /// when they borrow an imported instance identity. Companion traversal uses
    /// the unfiltered visitor above.
    pub fn visit_referenced_model_placements(
        &self,
        mut visit: impl FnMut(&ManualPlacement) -> Result<(), String>,
    ) -> Result<(), String> {
        self.visit_placements(|placement, source_instance| {
            if self.requires_model(placement, source_instance) {
                visit(placement)
            } else {
                Ok(())
            }
        })
    }

    /// The playable catalogue uses the current map, independently of the journal.
    pub fn visit_current_model_placements(
        &self,
        mut visit: impl FnMut(&ManualPlacement) -> Result<(), String>,
    ) -> Result<(), String> {
        collect(&self.head.corrections, &mut |placement, source_instance| {
            if self.requires_model(placement, source_instance) {
                visit(placement)
            } else {
                Ok(())
            }
        })
    }

    fn requires_model(&self, placement: &ManualPlacement, source_instance: bool) -> bool {
        !source_instance
            || !imported_plants::is_source_asset(
                &self.head,
                placement.asset_id,
                placement.asset_hash,
            )
    }

    fn visit_placements(
        &self,
        mut visit: impl FnMut(&ManualPlacement, bool) -> Result<(), String>,
    ) -> Result<(), String> {
        collect(&self.migration.head().corrections, &mut visit)?;
        collect(&self.head.corrections, &mut visit)?;
        for receipt in &self.receipts {
            collect(&receipt.applied, &mut visit)?;
            collect(&receipt.inverse, &mut visit)?;
            if let ZoneAction::Edit(corrections) = &receipt.request.action {
                collect(corrections, &mut visit)?;
            }
        }
        Ok(())
    }

    /// Collect immutable physical and particle packages without reading the Vault.
    pub fn required_source_companions(&self) -> Result<SourceCompanionRequirements, String> {
        let mut required = SourceCompanionRequirements::default();
        self.visit_referenced_placements(|placement| {
            if placement.physics.is_none() && placement.particles.is_none() {
                return Ok(());
            }
            placements::validate(std::slice::from_ref(placement))?;
            if let Some(reference) = &placement.physics {
                required.physics.insert(SourceCompanionPin {
                    asset_id: placement.asset_id,
                    carrier_sha256: reference.carrier_sha256,
                    manifest_sha256: reference.manifest_sha256,
                    package_sha256: reference.package_sha256,
                });
            }
            if let Some(reference) = &placement.particles {
                required.particles.insert(SourceCompanionPin {
                    asset_id: placement.asset_id,
                    carrier_sha256: reference.carrier_sha256,
                    manifest_sha256: reference.manifest_sha256,
                    package_sha256: reference.package_sha256,
                });
            }
            Ok(())
        })?;
        Ok(required)
    }
}

fn collect(
    corrections: &ZoneCorrections,
    visit: &mut impl FnMut(&ManualPlacement, bool) -> Result<(), String>,
) -> Result<(), String> {
    for placement in corrections.placements.as_deref().unwrap_or_default() {
        visit(placement, false)?;
    }
    for edit in corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
    {
        visit(&edit.source, true)?;
        if let SourceObjectState::Edited { placement } = &edit.state {
            visit(placement, true)?;
        }
        for copy in &edit.copies {
            visit(copy, true)?;
        }
    }
    Ok(())
}
