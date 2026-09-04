//! The first authored workspace: one project, one module and one exterior area.
//!
//! Creation is pure. Its inverse discards only an unchanged in-memory draft; filesystem
//! persistence and any actual player placement belong to explicit consumers.

use crate::{
    HeightSamplingAlgorithm, HeightSamplingParameters, TERRAIN_DOCUMENT_SCHEMA_VERSION,
    TerrainDocument, TerrainDocumentError, TerrainExtent, TerrainMaterialLayer,
};
use forge_core::{
    AreaId, CanonicalJsonError, CommandId, ContentHash, ModuleId, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const WORKSPACE_SCHEMA: &str = "forge.workspace";
pub const WORKSPACE_SCHEMA_VERSION: u32 = 1;
pub const MAX_WORKSPACE_JSON_BYTES: usize = 1_048_576;
const MAX_NAME_BYTES: usize = 128;
const AREA_SIZE_M: f32 = 512.0;
const SAMPLE_SPACING_M: f32 = 2.0;
const MAX_SPAWN_HEIGHT_M: f32 = 1_024.0;

/// Caller-supplied identities make creation deterministic and independent of platform entropy.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NewProjectCommand {
    pub command_id: CommandId,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub project_name: String,
    pub module_name: String,
    pub area_name: String,
    pub seed: u64,
    pub spawn: Vec3,
}

/// Version 1 intentionally supports exactly one module and one area, not placeholder collections.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceDocument {
    pub schema: String,
    pub schema_version: u32,
    pub revision: Revision,
    pub project: WorkspaceProject,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceProject {
    pub id: ProjectId,
    pub name: String,
    pub module: WorkspaceModule,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceModule {
    pub id: ModuleId,
    pub name: String,
    pub area: WorkspaceArea,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceArea {
    pub id: AreaId,
    pub name: String,
    pub kind: WorkspaceAreaKind,
    /// Authored metadata only; creation neither snaps Y nor starts a gameplay controller.
    pub spawn: Vec3,
    pub terrain: TerrainDocument,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WorkspaceAreaKind {
    Exterior,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CreatedWorkspace {
    pub document: WorkspaceDocument,
    pub receipt: WorkspaceCreationReceipt,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceCreationReceipt {
    pub command_id: CommandId,
    pub base_revision: Option<Revision>,
    pub new_revision: Revision,
    pub before_hash: Option<ContentHash>,
    pub after_hash: ContentHash,
    pub summary: String,
    pub inverse: WorkspaceCreationInverse,
}

/// A consumer may clear its draft only after checking this hash; this is never a disk-delete request.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
pub enum WorkspaceCreationInverse {
    DiscardDraft { expected_after_hash: ContentHash },
}

impl WorkspaceDocument {
    pub fn create(command: NewProjectCommand) -> Result<CreatedWorkspace, WorkspaceError> {
        if command.command_id.as_uuid().is_nil() {
            return Err(WorkspaceError::NilIdentity("command_id"));
        }
        let terrain = TerrainDocument::new(
            command.project_id,
            command.area_id,
            Revision::INITIAL,
            command.area_name.clone(),
            command.seed,
            TerrainExtent::new(AREA_SIZE_M, AREA_SIZE_M),
            HeightSamplingParameters {
                algorithm: HeightSamplingAlgorithm::FractalValueNoiseV1,
                sample_spacing_m: SAMPLE_SPACING_M,
                base_elevation_m: 0.0,
                amplitude_m: 12.0,
                base_frequency_cycles_per_m: 1.0 / 256.0,
                octaves: 5,
                lacunarity: 2.0,
                persistence: 0.5,
            },
            vec![
                TerrainMaterialLayer::new("meadow_grass", "Herbe"),
                TerrainMaterialLayer::new("valley_earth", "Terre"),
                TerrainMaterialLayer::new("ridge_rock", "Roche"),
            ],
        );
        let document = Self {
            schema: WORKSPACE_SCHEMA.to_owned(),
            schema_version: WORKSPACE_SCHEMA_VERSION,
            revision: Revision::INITIAL,
            project: WorkspaceProject {
                id: command.project_id,
                name: command.project_name,
                module: WorkspaceModule {
                    id: command.module_id,
                    name: command.module_name,
                    area: WorkspaceArea {
                        id: command.area_id,
                        name: command.area_name,
                        kind: WorkspaceAreaKind::Exterior,
                        spawn: command.spawn,
                        terrain,
                    },
                },
            },
        };
        let after_hash = document.content_hash()?;
        let receipt = WorkspaceCreationReceipt {
            command_id: command.command_id,
            base_revision: None,
            new_revision: document.revision,
            before_hash: None,
            after_hash,
            summary: "Création : 1 projet, 1 module, 1 zone extérieure et son terrain source."
                .to_owned(),
            inverse: WorkspaceCreationInverse::DiscardDraft {
                expected_after_hash: after_hash,
            },
        };
        Ok(CreatedWorkspace { document, receipt })
    }

    #[must_use]
    pub const fn module(&self) -> &WorkspaceModule {
        &self.project.module
    }

    #[must_use]
    pub const fn area(&self) -> &WorkspaceArea {
        &self.project.module.area
    }

    pub fn validate(&self) -> Result<(), WorkspaceError> {
        if self.schema != WORKSPACE_SCHEMA {
            return Err(WorkspaceError::InvalidSchema(self.schema.clone()));
        }
        if self.schema_version != WORKSPACE_SCHEMA_VERSION {
            return Err(WorkspaceError::UnsupportedVersion(self.schema_version));
        }
        if self.revision.get() == 0 {
            return Err(WorkspaceError::InvalidRevision("workspace"));
        }
        for (field, nil) in [
            ("project_id", self.project.id.as_uuid().is_nil()),
            ("module_id", self.module().id.as_uuid().is_nil()),
            ("area_id", self.area().id.as_uuid().is_nil()),
        ] {
            if nil {
                return Err(WorkspaceError::NilIdentity(field));
            }
        }
        for (field, name) in [
            ("project_name", self.project.name.as_str()),
            ("module_name", self.module().name.as_str()),
            ("area_name", self.area().name.as_str()),
        ] {
            validate_name(field, name)?;
        }
        validate_spawn(self.area().spawn)?;
        let terrain = &self.area().terrain;
        terrain.validate()?;
        if terrain.project_id != self.project.id || terrain.area_id != self.area().id {
            return Err(WorkspaceError::TerrainOwnershipMismatch);
        }
        if terrain.revision.get() == 0 || terrain.revision > self.revision {
            return Err(WorkspaceError::InvalidRevision("terrain"));
        }
        if terrain.schema_version != TERRAIN_DOCUMENT_SCHEMA_VERSION
            || terrain.extent_m.size_x_m.to_bits() != AREA_SIZE_M.to_bits()
            || terrain.extent_m.size_z_m.to_bits() != AREA_SIZE_M.to_bits()
            || terrain.height_sampling.sample_spacing_m.to_bits() != SAMPLE_SPACING_M.to_bits()
            || terrain.material_layers.len() != 3
        {
            return Err(WorkspaceError::UnsupportedTerrainShape);
        }
        // A valid document must fit its actual save representation, not only compact input.
        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, WorkspaceError> {
        ensure_json_budget(json.len())?;
        let document: Self = serde_json::from_str(json)?;
        document.validate()?;
        Ok(document)
    }

    pub fn to_pretty_json(&self) -> Result<String, WorkspaceError> {
        self.validate()?;
        let json = serde_json::to_string_pretty(self)?;
        ensure_json_budget(json.len())?;
        Ok(json)
    }

    pub fn content_hash(&self) -> Result<ContentHash, WorkspaceError> {
        self.validate()?;
        Ok(canonical_json_sha256(self)?)
    }
}

fn validate_name(field: &'static str, name: &str) -> Result<(), WorkspaceError> {
    if name.trim().is_empty() || name.chars().any(char::is_control) {
        return Err(WorkspaceError::InvalidName(field));
    }
    if name.len() > MAX_NAME_BYTES {
        return Err(WorkspaceError::NameTooLong {
            field,
            bytes: name.len(),
        });
    }
    Ok(())
}

fn validate_spawn(spawn: Vec3) -> Result<(), WorkspaceError> {
    for (axis, value, limit) in [
        ("x", spawn.x, AREA_SIZE_M * 0.5),
        ("y", spawn.y, MAX_SPAWN_HEIGHT_M),
        ("z", spawn.z, AREA_SIZE_M * 0.5),
    ] {
        if !value.is_finite() || value.abs() > limit {
            return Err(WorkspaceError::InvalidSpawn { axis, limit });
        }
    }
    Ok(())
}

fn ensure_json_budget(bytes: usize) -> Result<(), WorkspaceError> {
    if bytes > MAX_WORKSPACE_JSON_BYTES {
        Err(WorkspaceError::JsonBudgetExceeded {
            bytes,
            maximum: MAX_WORKSPACE_JSON_BYTES,
        })
    } else {
        Ok(())
    }
}

#[derive(Debug, Error)]
pub enum WorkspaceError {
    #[error("invalid workspace schema `{0}`")]
    InvalidSchema(String),
    #[error("unsupported workspace version {0}")]
    UnsupportedVersion(u32),
    #[error("{0} cannot be blank or contain control characters")]
    InvalidName(&'static str),
    #[error("{field} contains {bytes} bytes; maximum is {MAX_NAME_BYTES}")]
    NameTooLong { field: &'static str, bytes: usize },
    #[error("{0} cannot be the nil UUID")]
    NilIdentity(&'static str),
    #[error("spawn {axis} must be finite and inside [-{limit}, {limit}] metres")]
    InvalidSpawn { axis: &'static str, limit: f32 },
    #[error("invalid {0} revision")]
    InvalidRevision(&'static str),
    #[error("terrain project/area identities do not match their workspace owners")]
    TerrainOwnershipMismatch,
    #[error(
        "workspace v1 requires a v2 terrain, 512 m square, 2 m samples and three material layers"
    )]
    UnsupportedTerrainShape,
    #[error("workspace JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonBudgetExceeded { bytes: usize, maximum: usize },
    #[error("invalid workspace terrain: {0}")]
    Terrain(#[from] TerrainDocumentError),
    #[error("invalid workspace JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash workspace: {0}")]
    Hash(#[from] CanonicalJsonError),
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn command() -> NewProjectCommand {
        NewProjectCommand {
            command_id: CommandId::deterministic("workspace-test/create"),
            project_id: ProjectId::deterministic("workspace-test/project"),
            module_id: ModuleId::deterministic("workspace-test/module"),
            area_id: AreaId::deterministic("workspace-test/area"),
            project_name: "Mon jeu".to_owned(),
            module_name: "Premier module".to_owned(),
            area_name: "Clairière".to_owned(),
            seed: 42,
            spawn: Vec3::new(3.0, 17.5, -8.0),
        }
    }

    fn document() -> WorkspaceDocument {
        WorkspaceDocument::create(command()).unwrap().document
    }

    #[test]
    fn creation_preserves_injected_ids_names_seed_and_explicit_spawn() {
        let input = command();
        let created = WorkspaceDocument::create(input.clone()).unwrap();
        let document = &created.document;
        let terrain = &document.area().terrain;
        assert_eq!(document.project.id, input.project_id);
        assert_eq!(document.module().id, input.module_id);
        assert_eq!(document.area().id, input.area_id);
        assert_eq!(document.project.name, input.project_name);
        assert_eq!(document.module().name, input.module_name);
        assert_eq!(document.area().name, input.area_name);
        assert_eq!(document.area().kind, WorkspaceAreaKind::Exterior);
        assert_eq!(document.area().spawn, input.spawn);
        assert_eq!(document.revision, Revision::INITIAL);
        assert_eq!(terrain.revision, Revision::INITIAL);
        assert_eq!(terrain.project_id, input.project_id);
        assert_eq!(terrain.area_id, input.area_id);
        assert_eq!(terrain.seed, input.seed);
        assert_eq!(terrain.schema_version, 2);
        assert_eq!(terrain.extent_m, TerrainExtent::new(512.0, 512.0));
        assert_eq!(
            terrain.height_sampling.sample_spacing_m.to_bits(),
            2.0_f32.to_bits()
        );
        assert_eq!(
            terrain.height_sampling.algorithm,
            HeightSamplingAlgorithm::FractalValueNoiseV1
        );
        assert_eq!(terrain.material_layers.len(), 3);
        assert!(terrain.height_deltas.is_empty());
        terrain.validate().unwrap();
        assert_ne!(
            terrain.project_id,
            ProjectId::deterministic("visual-valley-project")
        );
        assert_ne!(terrain.area_id, AreaId::deterministic("visual-valley-area"));
    }

    #[test]
    fn creation_receipt_describes_absent_before_and_draft_only_inverse() {
        let created = WorkspaceDocument::create(command()).unwrap();
        let hash = created.document.content_hash().unwrap();
        assert_eq!(created.receipt.command_id, command().command_id);
        assert_eq!(created.receipt.base_revision, None);
        assert_eq!(created.receipt.before_hash, None);
        assert_eq!(created.receipt.new_revision, Revision::INITIAL);
        assert_eq!(created.receipt.after_hash, hash);
        assert_eq!(
            created.receipt.inverse,
            WorkspaceCreationInverse::DiscardDraft {
                expected_after_hash: hash
            }
        );
        assert!(!created.receipt.summary.is_empty());
        assert!(created.receipt.summary.len() <= 128);
        let json = serde_json::to_string(&created).unwrap();
        assert_eq!(
            serde_json::from_str::<CreatedWorkspace>(&json).unwrap(),
            created
        );
        assert!(json.contains("discard_draft"));
        assert!(!json.contains("path"));
    }

    #[test]
    fn identical_commands_are_deterministic_but_identity_and_seed_changes_are_not_lost() {
        let first = WorkspaceDocument::create(command()).unwrap();
        let replay = WorkspaceDocument::create(command()).unwrap();
        assert_eq!(first, replay);
        let mut changed_command = command();
        changed_command.command_id = CommandId::deterministic("workspace-test/other-command");
        let other_receipt = WorkspaceDocument::create(changed_command).unwrap();
        assert_eq!(first.document, other_receipt.document);
        assert_ne!(first.receipt.command_id, other_receipt.receipt.command_id);
        let mut changed_seed = command();
        changed_seed.seed += 1;
        let changed_seed = WorkspaceDocument::create(changed_seed).unwrap();
        assert_ne!(first.receipt.after_hash, changed_seed.receipt.after_hash);
        let mut changed_identity = command();
        changed_identity.area_id = AreaId::deterministic("workspace-test/other-area");
        let changed_identity = WorkspaceDocument::create(changed_identity).unwrap();
        assert_ne!(
            first.receipt.after_hash,
            changed_identity.receipt.after_hash
        );
        assert_eq!(
            changed_identity.document.area().terrain.area_id,
            changed_identity.document.area().id
        );
    }

    #[test]
    fn workspace_round_trip_and_hash_are_independent_of_json_whitespace() {
        let document = document();
        let pretty = document.to_pretty_json().unwrap();
        let compact = serde_json::to_string(&document).unwrap();
        for json in [pretty, compact] {
            let restored = WorkspaceDocument::from_json(&json).unwrap();
            assert_eq!(restored, document);
            assert_eq!(
                restored.content_hash().unwrap(),
                document.content_hash().unwrap()
            );
        }
        assert_eq!(document.area().spawn.y.to_bits(), 17.5_f32.to_bits());
    }

    #[test]
    fn names_are_bounded_nonblank_and_do_not_accept_control_characters() {
        for field in ["project_name", "module_name", "area_name"] {
            for value in ["", "   ", "\n", "A\0B", "A\tB"] {
                let mut input = serde_json::to_value(command()).unwrap();
                input[field] = json!(value);
                let input: NewProjectCommand = serde_json::from_value(input).unwrap();
                assert!(
                    matches!(WorkspaceDocument::create(input), Err(WorkspaceError::InvalidName(actual)) if actual == field)
                );
            }
            let mut input = serde_json::to_value(command()).unwrap();
            input[field] = json!("é".repeat(65));
            let input: NewProjectCommand = serde_json::from_value(input).unwrap();
            assert!(
                matches!(WorkspaceDocument::create(input), Err(WorkspaceError::NameTooLong { field: actual, bytes: 130 }) if actual == field)
            );
        }
        let mut input = command();
        input.area_name = "é".repeat(64);
        WorkspaceDocument::create(input).unwrap();
    }

    #[test]
    fn all_u64_seeds_are_allowed_without_numeric_coercion() {
        for seed in [0, u64::MAX] {
            let mut input = command();
            input.seed = seed;
            let document = WorkspaceDocument::create(input).unwrap().document;
            let restored =
                WorkspaceDocument::from_json(&document.to_pretty_json().unwrap()).unwrap();
            assert_eq!(restored.area().terrain.seed, seed);
        }
        for seed in [json!(-1), json!(1.5), json!("18446744073709551616")] {
            let mut input = serde_json::to_value(command()).unwrap();
            input["seed"] = seed;
            assert!(serde_json::from_value::<NewProjectCommand>(input).is_err());
        }
    }

    #[test]
    fn spawn_preserves_boundaries_and_rejects_nonfinite_or_outside_values() {
        for spawn in [
            Vec3::new(-256.0, -1024.0, -256.0),
            Vec3::new(256.0, 1024.0, 256.0),
        ] {
            let mut input = command();
            input.spawn = spawn;
            assert_eq!(
                WorkspaceDocument::create(input)
                    .unwrap()
                    .document
                    .area()
                    .spawn,
                spawn
            );
        }
        for spawn in [
            Vec3::new(256.1, 0.0, 0.0),
            Vec3::new(-256.1, 0.0, 0.0),
            Vec3::new(0.0, 1024.1, 0.0),
            Vec3::new(0.0, -1024.1, 0.0),
            Vec3::new(0.0, 0.0, 256.1),
            Vec3::new(0.0, 0.0, -256.1),
            Vec3::new(f32::NAN, 0.0, 0.0),
            Vec3::new(0.0, f32::INFINITY, 0.0),
            Vec3::new(0.0, 0.0, f32::NEG_INFINITY),
        ] {
            let mut input = command();
            input.spawn = spawn;
            assert!(matches!(
                WorkspaceDocument::create(input),
                Err(WorkspaceError::InvalidSpawn { .. })
            ));
        }
    }

    #[test]
    fn nil_ids_and_inconsistent_terrain_owners_are_rejected() {
        for field in ["command_id", "project_id", "module_id", "area_id"] {
            let mut input = serde_json::to_value(command()).unwrap();
            input[field] = json!("00000000-0000-0000-0000-000000000000");
            let input: NewProjectCommand = serde_json::from_value(input).unwrap();
            assert!(
                matches!(WorkspaceDocument::create(input), Err(WorkspaceError::NilIdentity(actual)) if actual == field)
            );
        }
        let mut changed = document();
        changed.project.module.area.terrain.project_id = ProjectId::deterministic("other-project");
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::TerrainOwnershipMismatch)
        ));
        let mut changed = document();
        changed.project.module.area.terrain.area_id = AreaId::deterministic("other-area");
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::TerrainOwnershipMismatch)
        ));
    }

    #[test]
    fn invalid_revisions_and_unsupported_terrain_shapes_are_rejected() {
        let mut changed = document();
        changed.revision = Revision::new(0);
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::InvalidRevision("workspace"))
        ));
        for revision in [0, 2] {
            let mut changed = document();
            changed.project.module.area.terrain.revision = Revision::new(revision);
            assert!(matches!(
                changed.validate(),
                Err(WorkspaceError::InvalidRevision("terrain"))
            ));
        }
        for mutation in 0..4 {
            let mut changed = document();
            let terrain = &mut changed.project.module.area.terrain;
            match mutation {
                0 => terrain.schema_version = 1,
                1 => terrain.extent_m.size_x_m = 256.0,
                2 => terrain.height_sampling.sample_spacing_m = 4.0,
                _ => {
                    terrain.material_layers.pop();
                }
            }
            assert!(matches!(
                changed.validate(),
                Err(WorkspaceError::UnsupportedTerrainShape)
            ));
        }
    }

    #[test]
    fn schemas_unknown_fields_and_future_area_kinds_fail_closed() {
        let mut value = serde_json::to_value(document()).unwrap();
        value["schema"] = json!("other.workspace");
        assert!(matches!(
            WorkspaceDocument::from_json(&value.to_string()),
            Err(WorkspaceError::InvalidSchema(_))
        ));
        value["schema"] = json!(WORKSPACE_SCHEMA);
        value["schema_version"] = json!(2);
        assert!(matches!(
            WorkspaceDocument::from_json(&value.to_string()),
            Err(WorkspaceError::UnsupportedVersion(2))
        ));
        for pointer in [
            "",
            "/project",
            "/project/module",
            "/project/module/area",
            "/project/module/area/terrain",
        ] {
            let mut value = serde_json::to_value(document()).unwrap();
            value.pointer_mut(pointer).unwrap()["unknown_field"] = json!(true);
            assert!(matches!(
                WorkspaceDocument::from_json(&value.to_string()),
                Err(WorkspaceError::Json(_))
            ));
        }
        let mut value = serde_json::to_value(document()).unwrap();
        value["project"]["module"]["area"]["kind"] = json!("interior");
        assert!(WorkspaceDocument::from_json(&value.to_string()).is_err());
        let mut input = serde_json::to_value(command()).unwrap();
        input["free_asset_path"] = json!("../other-file");
        assert!(serde_json::from_value::<NewProjectCommand>(input).is_err());
    }

    #[test]
    fn oversized_input_is_rejected_before_json_parsing() {
        let oversized = " ".repeat(MAX_WORKSPACE_JSON_BYTES + 1);
        assert!(matches!(
            WorkspaceDocument::from_json(&oversized),
            Err(WorkspaceError::JsonBudgetExceeded { .. })
        ));
        let compact = serde_json::to_string(&document()).unwrap();
        let exact = format!(
            "{compact}{}",
            " ".repeat(MAX_WORKSPACE_JSON_BYTES - compact.len())
        );
        WorkspaceDocument::from_json(&exact).unwrap();
    }

    #[test]
    fn compact_input_cannot_admit_a_document_too_large_to_save() {
        let mut document = document();
        document.project.module.area.terrain.height_deltas = (0_u16..15_000)
            .map(|index| crate::TerrainHeightDelta::new(index % 257, index / 257, 1.0))
            .collect();
        document.area().terrain.validate().unwrap();
        let compact = serde_json::to_string(&document).unwrap();
        assert!(compact.len() < MAX_WORKSPACE_JSON_BYTES);
        assert!(serde_json::to_string_pretty(&document).unwrap().len() > MAX_WORKSPACE_JSON_BYTES);
        assert!(matches!(
            document.validate(),
            Err(WorkspaceError::JsonBudgetExceeded { .. })
        ));
        assert!(matches!(
            WorkspaceDocument::from_json(&compact),
            Err(WorkspaceError::JsonBudgetExceeded { .. })
        ));
        assert!(matches!(
            document.to_pretty_json(),
            Err(WorkspaceError::JsonBudgetExceeded { .. })
        ));
        assert!(matches!(
            document.content_hash(),
            Err(WorkspaceError::JsonBudgetExceeded { .. })
        ));
    }
}
