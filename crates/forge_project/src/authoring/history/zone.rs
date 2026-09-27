//! Summaries of verified aggregate receipts, with no duplicated sample payloads.
use super::{HistoryAction, HistoryChange, HistoryEntry, HistoryPage, PAGE_SIZE};
use crate::zone_authoring::{
    DensityOverride, GeneratorStamp, ZoneBase,
    session::{ZoneAction, ZoneEditSession, ZoneReceipt},
};
use forge_core::{ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
pub mod objects;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SampleSummary {
    pub count: usize,
    pub hash: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DensitySummary {
    pub mask: SampleSummary,
    pub locked: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RecipeSummary {
    pub generator: GeneratorStamp,
    pub terrain_seed: u64,
    pub vegetation_seed: Option<u64>,
    pub hash: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "property", rename_all = "snake_case", deny_unknown_fields)]
pub enum ZoneHistoryChange {
    Objects {
        domain: objects::ObjectDomain,
        before_explicit: bool,
        after_explicit: bool,
        changes: Vec<objects::ObjectChange>,
    },
    Start {
        before: Option<crate::zone_authoring::start::ZoneStart>,
        after: Option<crate::zone_authoring::start::ZoneStart>,
    },
    Name {
        before: Option<String>,
        after: Option<String>,
    },
    Elevation {
        before: Option<f32>,
        after: Option<f32>,
    },
    Heights {
        before: Option<SampleSummary>,
        after: Option<SampleSummary>,
    },
    Materials {
        before: Option<SampleSummary>,
        after: Option<SampleSummary>,
    },
    Placements {
        before: Option<SampleSummary>,
        after: Option<SampleSummary>,
    },
    Density {
        layer_id: crate::VegetationLayerId,
        before: Option<DensitySummary>,
        after: Option<DensitySummary>,
    },
    Recipe {
        before: RecipeSummary,
        after: RecipeSummary,
    },
}

pub fn read(
    session: &ZoneEditSession,
    expected_hash: ContentHash,
    offset: usize,
) -> Result<HistoryPage, String> {
    let source_hash = session.head().content_hash()?;
    if source_hash != expected_hash {
        return Err("history changed: refresh the current zone".into());
    }
    let audit = session.audit();
    if offset > audit.len() {
        return Err("history offset exceeds journal length".into());
    }
    let entries = audit
        .iter()
        .rev()
        .skip(offset)
        .take(PAGE_SIZE)
        .map(entry)
        .collect::<Result<_, _>>()?;
    Ok(HistoryPage {
        source_hash,
        offset,
        total: audit.len(),
        entries,
    })
}

fn entry(receipt: &ZoneReceipt) -> Result<HistoryEntry, String> {
    let before = &receipt.inverse;
    let after = &receipt.applied;
    let mut changes = objects::changes(before, after)?;
    if before.start != after.start {
        changes.push(ZoneHistoryChange::Start {
            before: before.start.as_deref().copied(),
            after: after.start.as_deref().copied(),
        });
    }
    if before.name != after.name {
        changes.push(ZoneHistoryChange::Name {
            before: before.name.clone(),
            after: after.name.clone(),
        });
    }
    if before.base_elevation_m != after.base_elevation_m {
        changes.push(ZoneHistoryChange::Elevation {
            before: before.base_elevation_m,
            after: after.base_elevation_m,
        });
    }
    if before.height_deltas != after.height_deltas {
        changes.push(ZoneHistoryChange::Heights {
            before: samples(before.height_deltas.as_ref())?,
            after: samples(after.height_deltas.as_ref())?,
        });
    }
    if before.material_overrides != after.material_overrides {
        changes.push(ZoneHistoryChange::Materials {
            before: samples(before.material_overrides.as_ref())?,
            after: samples(after.material_overrides.as_ref())?,
        });
    }
    let ids: std::collections::BTreeSet<_> = before
        .density
        .iter()
        .chain(&after.density)
        .map(|d| d.layer_id.as_str())
        .collect();
    if before.placements != after.placements {
        changes.push(ZoneHistoryChange::Placements {
            before: samples(before.placements.as_ref())?,
            after: samples(after.placements.as_ref())?,
        });
    }
    for id in ids {
        let old = before.density.iter().find(|d| d.layer_id.as_str() == id);
        let new = after.density.iter().find(|d| d.layer_id.as_str() == id);
        if old != new {
            changes.push(ZoneHistoryChange::Density {
                layer_id: crate::VegetationLayerId::new(id),
                before: old.map(density).transpose()?,
                after: new.map(density).transpose()?,
            });
        }
    }
    if let Some(base) = &receipt.base_change {
        changes.push(ZoneHistoryChange::Recipe {
            before: recipe(&base.inverse)?,
            after: recipe(&base.applied)?,
        });
    }
    Ok(HistoryEntry {
        command_id: receipt.request.command_id,
        revision: receipt.resulting_revision,
        resulting_hash: receipt.resulting_hash,
        action: match receipt.request.action {
            ZoneAction::Edit(_) => HistoryAction::Edit,
            ZoneAction::Regenerate(_) | ZoneAction::ReconcilePromotions(_) => {
                HistoryAction::Regenerate
            }
            ZoneAction::Undo => HistoryAction::Undo,
            ZoneAction::Redo => HistoryAction::Redo,
        },
        change: HistoryChange::Zone { changes },
        audit: receipt.audit.clone(),
    })
}

fn samples<T: Serialize>(values: Option<&Vec<T>>) -> Result<Option<SampleSummary>, String> {
    values
        .map(|v| {
            Ok(SampleSummary {
                count: v.len(),
                hash: canonical_json_sha256(v).map_err(|e| e.to_string())?,
            })
        })
        .transpose()
}

fn density(value: &DensityOverride) -> Result<DensitySummary, String> {
    Ok(DensitySummary {
        mask: SampleSummary {
            count: value.mask.samples_u8.len(),
            hash: canonical_json_sha256(&value.mask).map_err(|e| e.to_string())?,
        },
        locked: value.locked,
    })
}

fn recipe(base: &ZoneBase) -> Result<RecipeSummary, String> {
    Ok(RecipeSummary {
        generator: base.generator.clone(),
        terrain_seed: base.terrain.seed,
        vegetation_seed: base.vegetation.as_ref().map(|v| v.seed),
        hash: canonical_json_sha256(base).map_err(|e| e.to_string())?,
    })
}
