//! Archive v6: lossless field deltas between consecutive Edit requests, not world deltas.
use super::{ZoneAction, ZoneEditSession, ZoneReceipt, ZoneRequest};
use crate::audit::AuditStamp;
use crate::zone_authoring::{
    MAX_ZONE_HEAD_BYTES, ZoneAuthoringHead, ZoneCorrections, migration::ZoneMigrationProposal,
};
use forge_core::{CommandId, ContentHash, Revision, canonical_json_bytes, canonical_json_sha256};
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
#[serde(
    tag = "kind",
    content = "value",
    rename_all = "snake_case",
    deny_unknown_fields
)]
enum Change<T> {
    Keep,
    Set(T),
}

fn difference<T: Serialize + Clone>(old: &T, new: &T) -> Result<Change<T>, String> {
    // Compare serialized values, preserving signed zero and every audited field bit.
    if canonical_json_bytes(old).map_err(|e| e.to_string())?
        == canonical_json_bytes(new).map_err(|e| e.to_string())?
    {
        Ok(Change::Keep)
    } else {
        Ok(Change::Set(new.clone()))
    }
}
fn assign<T>(change: Change<T>, target: &mut T) {
    if let Change::Set(value) = change {
        *target = value;
    }
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct CorrectionsDelta {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    gameplay_visuals:
        Option<Change<Option<Vec<crate::module_gameplay_visuals::GameplayVisualBinding>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    physics: Option<Change<Option<Box<crate::zone_authoring::physics::PhysicsSceneSettings>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    source_object_edits:
        Option<Change<Option<Vec<crate::zone_authoring::source_objects::SourceObjectEdit>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    gameplay: Option<Change<Option<Box<forge_gameplay::GameplayDocument>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    imported_plant_placements:
        Option<Change<Option<Vec<crate::zone_authoring::imported_plants::ImportedPlantPlacement>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    imported_plant_offsets:
        Option<Change<Option<Vec<crate::zone_authoring::imported_plants::ImportedPlantOffset>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    vegetation_exclusions: Option<Change<Option<Vec<crate::VegetationExclusion>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    promotions: Option<Change<Option<Vec<crate::zone_authoring::promotion::PromotedInstance>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    species: Option<Change<Option<Vec<crate::zone_authoring::species::LayerSpecies>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    start: Option<Change<Option<Box<crate::zone_authoring::start::ZoneStart>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    buildings: Option<Change<Option<Vec<crate::zone_authoring::buildings::AuthoredBuilding>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    rivers: Option<Change<Option<Vec<crate::zone_authoring::rivers::AuthoredRiver>>>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    paths: Option<Change<Option<Vec<crate::zone_authoring::paths::AuthoredPath>>>>,
    name: Change<Option<String>>,
    base_elevation_m: Change<Option<f32>>,
    height_deltas: Change<Option<Vec<crate::TerrainHeightDelta>>>,
    material_overrides: Change<Option<Vec<crate::TerrainMaterialOverride>>>,
    density: Change<Vec<crate::zone_authoring::DensityOverride>>,
    placements: Change<Option<Vec<crate::zone_authoring::placements::ManualPlacement>>>,
}
impl CorrectionsDelta {
    fn between(a: &ZoneCorrections, b: &ZoneCorrections) -> Result<Self, String> {
        Ok(Self {
            gameplay_visuals: if a.gameplay_visuals.is_some() || b.gameplay_visuals.is_some() {
                Some(difference(&a.gameplay_visuals, &b.gameplay_visuals)?)
            } else {
                None
            },
            physics: if a.physics.is_some() || b.physics.is_some() {
                Some(difference(&a.physics, &b.physics)?)
            } else {
                None
            },
            source_object_edits: if a.source_object_edits.is_some()
                || b.source_object_edits.is_some()
            {
                Some(difference(&a.source_object_edits, &b.source_object_edits)?)
            } else {
                None
            },
            gameplay: if a.gameplay.is_some() || b.gameplay.is_some() {
                Some(difference(&a.gameplay, &b.gameplay)?)
            } else {
                None
            },
            imported_plant_placements: if a.imported_plant_placements.is_some()
                || b.imported_plant_placements.is_some()
            {
                Some(difference(
                    &a.imported_plant_placements,
                    &b.imported_plant_placements,
                )?)
            } else {
                None
            },
            imported_plant_offsets: if a.imported_plant_offsets.is_some()
                || b.imported_plant_offsets.is_some()
            {
                Some(difference(
                    &a.imported_plant_offsets,
                    &b.imported_plant_offsets,
                )?)
            } else {
                None
            },
            vegetation_exclusions: if a.vegetation_exclusions.is_some()
                || b.vegetation_exclusions.is_some()
            {
                Some(difference(
                    &a.vegetation_exclusions,
                    &b.vegetation_exclusions,
                )?)
            } else {
                None
            },
            promotions: if a.promotions.is_some() || b.promotions.is_some() {
                Some(difference(&a.promotions, &b.promotions)?)
            } else {
                None
            },
            species: if a.species.is_some() || b.species.is_some() {
                Some(difference(&a.species, &b.species)?)
            } else {
                None
            },
            start: if a.start.is_some() || b.start.is_some() {
                Some(difference(&a.start, &b.start)?)
            } else {
                None
            },
            buildings: if a.buildings.is_some() || b.buildings.is_some() {
                Some(difference(&a.buildings, &b.buildings)?)
            } else {
                None
            },
            rivers: if a.rivers.is_some() || b.rivers.is_some() {
                Some(difference(&a.rivers, &b.rivers)?)
            } else {
                None
            },
            paths: if a.paths.is_some() || b.paths.is_some() {
                Some(difference(&a.paths, &b.paths)?)
            } else {
                None
            },
            name: difference(&a.name, &b.name)?,
            base_elevation_m: difference(&a.base_elevation_m, &b.base_elevation_m)?,
            height_deltas: difference(&a.height_deltas, &b.height_deltas)?,
            material_overrides: difference(&a.material_overrides, &b.material_overrides)?,
            density: difference(&a.density, &b.density)?,
            placements: difference(&a.placements, &b.placements)?,
        })
    }
    fn apply(self, target: &mut ZoneCorrections) {
        if let Some(change) = self.gameplay_visuals {
            assign(change, &mut target.gameplay_visuals);
        }
        if let Some(change) = self.physics {
            assign(change, &mut target.physics);
        }
        if let Some(change) = self.source_object_edits {
            assign(change, &mut target.source_object_edits);
        }
        if let Some(change) = self.gameplay {
            assign(change, &mut target.gameplay);
        }
        if let Some(change) = self.imported_plant_placements {
            assign(change, &mut target.imported_plant_placements);
        }
        if let Some(change) = self.imported_plant_offsets {
            assign(change, &mut target.imported_plant_offsets);
        }
        if let Some(change) = self.vegetation_exclusions {
            assign(change, &mut target.vegetation_exclusions);
        }
        if let Some(change) = self.promotions {
            assign(change, &mut target.promotions);
        }
        if let Some(change) = self.species {
            assign(change, &mut target.species);
        }
        if let Some(change) = self.start {
            assign(change, &mut target.start);
        }
        if let Some(change) = self.buildings {
            assign(change, &mut target.buildings);
        }
        if let Some(change) = self.rivers {
            assign(change, &mut target.rivers);
        }
        if let Some(change) = self.paths {
            assign(change, &mut target.paths);
        }
        assign(self.name, &mut target.name);
        assign(self.base_elevation_m, &mut target.base_elevation_m);
        assign(self.height_deltas, &mut target.height_deltas);
        assign(self.material_overrides, &mut target.material_overrides);
        assign(self.density, &mut target.density);
        assign(self.placements, &mut target.placements);
    }
}
#[derive(Serialize, Deserialize)]
#[serde(
    tag = "kind",
    content = "value",
    rename_all = "snake_case",
    deny_unknown_fields
)]
enum Action {
    Edit(Box<CorrectionsDelta>),
    Other(ZoneAction),
}
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct StoredCommand {
    command_id: CommandId,
    expected_revision: Revision,
    expected_hash: ContentHash,
    action: Action,
    #[serde(default, skip_serializing_if = "AuditStamp::is_legacy")]
    audit: AuditStamp,
    receipt_hash: ContentHash,
}
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Archive {
    schema: String,
    version: u32,
    migration: ZoneMigrationProposal,
    head_hash: ContentHash,
    commands: Vec<StoredCommand>,
}

fn stored_command(
    receipt: &ZoneReceipt,
    previous: &ZoneCorrections,
) -> Result<StoredCommand, String> {
    let request = &receipt.request;
    let action = match &request.action {
        ZoneAction::Edit(value) => {
            Action::Edit(Box::new(CorrectionsDelta::between(previous, value)?))
        }
        other => Action::Other(other.clone()),
    };
    Ok(StoredCommand {
        command_id: request.command_id,
        expected_revision: request.expected_revision,
        expected_hash: request.expected_hash,
        action,
        audit: receipt.audit.clone(),
        receipt_hash: canonical_json_sha256(receipt).map_err(|error| error.to_string())?,
    })
}

/// Derived byte accounting only; archives still use the unchanged encoder.
pub(super) struct SizeCache {
    header_without_version: usize,
    commands_bytes: usize,
    expanded_bytes: usize,
    previous_edit: ZoneCorrections,
}

impl SizeCache {
    pub(super) fn new(
        migration: &ZoneMigrationProposal,
        head: &ZoneAuthoringHead,
    ) -> Result<Self, String> {
        let header = encode(migration, head, &[])?;
        Ok(Self {
            header_without_version: header.len() - version(&[]).to_string().len(),
            commands_bytes: 0,
            expanded_bytes: 0,
            previous_edit: ZoneCorrections::default(),
        })
    }

    pub(super) fn candidate(
        &self,
        receipts: &[ZoneReceipt],
        receipt: &ZoneReceipt,
    ) -> Result<(usize, usize, usize), String> {
        let mut expanded = self.expanded_bytes;
        check_expansion(&mut expanded, &receipt.request)?;
        let command = stored_command(receipt, &self.previous_edit)?;
        let command_bytes = canonical_json_bytes(&command)
            .map_err(|error| error.to_string())?
            .len();
        let new_version = version(receipts).max(version(std::slice::from_ref(receipt)));
        let total = self.header_without_version
            + new_version.to_string().len()
            + self.commands_bytes
            + command_bytes
            + receipts.len();
        Ok((total, command_bytes, expanded))
    }

    pub(super) fn accept(&mut self, receipt: &ZoneReceipt, command_bytes: usize, expanded: usize) {
        self.commands_bytes += command_bytes;
        self.expanded_bytes = expanded;
        if let ZoneAction::Edit(value) = &receipt.request.action {
            self.previous_edit = (**value).clone();
        }
    }
}

pub(super) fn encode(
    migration: &ZoneMigrationProposal,
    head: &ZoneAuthoringHead,
    receipts: &[ZoneReceipt],
) -> Result<Vec<u8>, String> {
    let mut previous = ZoneCorrections::default();
    let mut commands = Vec::with_capacity(receipts.len());
    let mut expanded_bytes = 0_usize;
    if receipts.len() > 1024 {
        return Err("Delta archive command limit exceeded".into());
    }
    for receipt in receipts {
        let request = &receipt.request;
        check_expansion(&mut expanded_bytes, request)?;
        commands.push(stored_command(receipt, &previous)?);
        if let ZoneAction::Edit(value) = &request.action {
            previous = (**value).clone();
        }
    }
    let bytes = canonical_json_bytes(&Archive {
        schema: "forge.zone-edit-session".into(),
        version: version(receipts),
        migration: migration.clone(),
        head_hash: head.content_hash()?,
        commands,
    })
    .map_err(|e| e.to_string())?;
    if bytes.len() > MAX_ZONE_HEAD_BYTES {
        return Err("Zone archive exceeds 4 MiB before mutation".into());
    }
    Ok(bytes)
}

pub(super) fn decode(bytes: &[u8]) -> Result<ZoneEditSession, String> {
    let archive: Archive = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if archive.schema != "forge.zone-edit-session"
        || !matches!(
            archive.version,
            6 | 7
                | 12
                | 15
                | 18
                | 21
                | 24
                | 27
                | 30
                | 33
                | 36
                | super::SOURCE_OBJECT_DELTA_VERSION
                | super::PHYSICS_DELTA_VERSION
                | super::PLACEMENT_PHYSICS_DELTA_VERSION
                | super::GAMEPLAY_VISUALS_DELTA_VERSION
        )
        || archive.commands.len() > 1024
    {
        return Err("Unsupported delta archive or command limit".into());
    }
    let mut session = ZoneEditSession::from_migration(archive.migration)?;
    session.delta_archive = true;
    let mut previous = ZoneCorrections::default();
    // Bound expanded requests independently of the compressed input (no unbounded replay bomb).
    let mut expanded_bytes = 0_usize;
    for command in archive.commands {
        let action = match command.action {
            Action::Edit(delta) => {
                delta.apply(&mut previous);
                ZoneAction::edit(previous.clone())
            }
            Action::Other(ZoneAction::Edit(_)) => {
                return Err("Full Edit forbidden in delta archive".into());
            }
            Action::Other(other) => other,
        };
        let request = ZoneRequest {
            command_id: command.command_id,
            expected_revision: command.expected_revision,
            expected_hash: command.expected_hash,
            action,
        };
        check_expansion(&mut expanded_bytes, &request)?;
        if session.previous(&request)?.is_some() {
            return Err("Duplicate archived receipt".into());
        }
        let receipt = session.execute_with_audit(&request, command.audit)?;
        if canonical_json_sha256(&receipt).map_err(|e| e.to_string())? != command.receipt_hash {
            return Err("Delta receipt replay mismatch".into());
        }
    }
    if session.head.content_hash()? != archive.head_hash {
        return Err("Delta archive head mismatch".into());
    }
    if archive.version != version(&session.receipts) {
        return Err("Delta archive version does not cover its commands".into());
    }
    Ok(session)
}

fn version(receipts: &[ZoneReceipt]) -> u32 {
    if super::has_gameplay_visuals(receipts) {
        return super::GAMEPLAY_VISUALS_DELTA_VERSION;
    }
    if super::has_placement_physics(receipts) {
        return super::PLACEMENT_PHYSICS_DELTA_VERSION;
    }
    if super::has_physics(receipts) {
        return super::PHYSICS_DELTA_VERSION;
    }
    if super::has_source_objects(receipts) {
        return super::SOURCE_OBJECT_DELTA_VERSION;
    }
    if super::archive_version(receipts) == 34 {
        return 36;
    }
    if super::archive_version(receipts) == 31 {
        return 33;
    }
    if super::has_vegetation_exclusions(receipts) {
        return 30;
    }
    if super::has_reconciliation(receipts) {
        return 27;
    }
    if super::has_promotions(receipts) {
        24
    } else if super::has_species(receipts) {
        21
    } else if super::has_start(receipts) {
        18
    } else if super::has_buildings(receipts) {
        15
    } else if super::has_rivers(receipts) {
        12
    } else if super::has_paths(receipts) {
        7
    } else {
        6
    }
}

fn check_expansion(total: &mut usize, request: &ZoneRequest) -> Result<(), String> {
    *total = total
        .checked_add(
            canonical_json_bytes(request)
                .map_err(|e| e.to_string())?
                .len(),
        )
        .ok_or("Expanded archive size overflow")?;
    if *total > 16 * MAX_ZONE_HEAD_BYTES {
        return Err("Expanded requests exceed 64 MiB".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn incremental_size_matches_real_encoding_across_edits_undo_redo_and_reload() {
        let terrain = crate::authoring::new_terrain("count", "zone", "Size").unwrap();
        let mut session = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        for index in 0..12 {
            let mut corrections = session.head().corrections.clone();
            corrections.name = Some(format!("Édition {index} \"\\\n"));
            for action in [
                ZoneAction::edit(corrections),
                ZoneAction::Undo,
                ZoneAction::Redo,
            ] {
                let request = ZoneRequest {
                    command_id: CommandId::deterministic(format!(
                        "count-{}",
                        session.audit().len()
                    )),
                    expected_revision: session.head().revision,
                    expected_hash: session.head().content_hash().unwrap(),
                    action,
                };
                let receipt = session.preview(&request).unwrap();
                let expected = session
                    .delta_size
                    .as_ref()
                    .unwrap()
                    .candidate(session.audit(), &receipt)
                    .unwrap()
                    .0;
                session.execute(&request).unwrap();
                assert_eq!(expected, session.delta_archive_bytes().unwrap().len());
            }
            if index % 3 == 0 {
                session =
                    ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap())
                        .unwrap();
            }
        }
    }

    #[test]
    fn expansion_budget_and_signed_zero_are_exact() {
        let request = ZoneRequest {
            command_id: CommandId::deterministic("bound"),
            expected_revision: Revision::INITIAL,
            expected_hash: ContentHash::from_bytes([0; 32]),
            action: ZoneAction::Undo,
        };
        let size = canonical_json_bytes(&request).unwrap().len();
        let mut total = 16 * MAX_ZONE_HEAD_BYTES - size;
        check_expansion(&mut total, &request).unwrap();
        assert!(check_expansion(&mut total, &request).is_err());
        let mut overflow = usize::MAX;
        assert!(check_expansion(&mut overflow, &request).is_err());
        assert!(matches!(
            difference(&Some(0.0_f32), &Some(-0.0_f32)).unwrap(),
            Change::Set(_)
        ));
        let mut target = Some(1.0_f32);
        assign(Change::Set(None), &mut target);
        assert_eq!(target, None);
    }
}
