//! Authored workspace with stable module/area collections and strict v1 migration.
//!
//! Creation is pure. Its inverse discards only an unchanged in-memory draft; filesystem
//! persistence and any actual player placement belong to explicit consumers.

use crate::{
    HeightSamplingAlgorithm, HeightSamplingParameters, TERRAIN_DOCUMENT_SCHEMA_VERSION,
    TerrainDocument, TerrainDocumentError, TerrainExtent, TerrainMaterialLayer,
    TerrainMaterialLayerId, VegetationDensityDocument, VegetationDensityError,
    VegetationDensityLayer, VegetationDocument, VegetationDocumentError, VegetationExtent,
    VegetationPresentationProfile, VegetationSpeciesPreset,
};
use forge_core::{
    AreaId, CanonicalJsonError, CommandId, ContentHash, ModuleId, ProjectId, Revision, Vec3,
    canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub const WORKSPACE_SCHEMA: &str = "forge.workspace";
pub const WORKSPACE_SCHEMA_VERSION: u32 = 3;
pub const MAX_WORKSPACE_MODULES: usize = 64;
pub const MAX_WORKSPACE_AREAS: usize = 256;
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

/// Canonical v3 model. Use `from_json` for explicit v1/v2 import.
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
    pub modules: Vec<WorkspaceModule>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WorkspaceModule {
    pub id: ModuleId,
    pub name: String,
    pub areas: Vec<WorkspaceArea>,
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
    pub vegetation: VegetationDocument,
    pub vegetation_density: VegetationDensityDocument,
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
        let document = Self {
            schema: WORKSPACE_SCHEMA.to_owned(),
            schema_version: WORKSPACE_SCHEMA_VERSION,
            revision: Revision::INITIAL,
            project: WorkspaceProject {
                id: command.project_id,
                name: command.project_name,
                modules: vec![WorkspaceModule {
                    id: command.module_id,
                    name: command.module_name,
                    areas: vec![new_exterior_area(
                        command.project_id,
                        command.area_id,
                        Revision::INITIAL,
                        command.area_name,
                        command.seed,
                        command.spawn,
                    )],
                }],
            },
        };
        let after_hash = document.content_hash()?;
        let receipt = WorkspaceCreationReceipt {
            command_id: command.command_id,
            base_revision: None,
            new_revision: document.revision,
            before_hash: None,
            after_hash,
            summary:
                "Création : 1 projet, 1 module, 1 zone extérieure, terrain et végétation source."
                    .to_owned(),
            inverse: WorkspaceCreationInverse::DiscardDraft {
                expected_after_hash: after_hash,
            },
        };
        Ok(CreatedWorkspace { document, receipt })
    }

    #[must_use]
    pub fn module(&self, id: ModuleId) -> Option<&WorkspaceModule> {
        self.project.modules.iter().find(|module| module.id == id)
    }

    #[must_use]
    pub fn area(&self, module_id: ModuleId, area_id: AreaId) -> Option<&WorkspaceArea> {
        self.module(module_id)?
            .areas
            .iter()
            .find(|area| area.id == area_id)
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
        if self.project.id.as_uuid().is_nil() {
            return Err(WorkspaceError::NilIdentity("project_id"));
        }
        validate_name("project_name", &self.project.name)?;
        if self.project.modules.is_empty() || self.project.modules.len() > MAX_WORKSPACE_MODULES {
            return Err(WorkspaceError::CollectionLimit("modules"));
        }
        let mut module_ids = std::collections::BTreeSet::new();
        let mut area_ids = std::collections::BTreeSet::new();
        for module in &self.project.modules {
            if module.id.as_uuid().is_nil() {
                return Err(WorkspaceError::NilIdentity("module_id"));
            }
            if !module_ids.insert(module.id) {
                return Err(WorkspaceError::DuplicateIdentity("module_id"));
            }
            validate_name("module_name", &module.name)?;
            for area in &module.areas {
                if area.id.as_uuid().is_nil() {
                    return Err(WorkspaceError::NilIdentity("area_id"));
                }
                if !area_ids.insert(area.id) {
                    return Err(WorkspaceError::DuplicateIdentity("area_id"));
                }
                if area_ids.len() > MAX_WORKSPACE_AREAS {
                    return Err(WorkspaceError::CollectionLimit("areas"));
                }
                validate_name("area_name", &area.name)?;
                validate_spawn(area.spawn)?;
                let terrain = &area.terrain;
                terrain.validate()?;
                if terrain.project_id != self.project.id || terrain.area_id != area.id {
                    return Err(WorkspaceError::TerrainOwnershipMismatch);
                }
                if terrain.revision.get() == 0 || terrain.revision > self.revision {
                    return Err(WorkspaceError::InvalidRevision("terrain"));
                }
                if terrain.schema_version != TERRAIN_DOCUMENT_SCHEMA_VERSION
                    || terrain.extent_m.size_x_m.to_bits() != AREA_SIZE_M.to_bits()
                    || terrain.extent_m.size_z_m.to_bits() != AREA_SIZE_M.to_bits()
                    || terrain.height_sampling.sample_spacing_m.to_bits()
                        != SAMPLE_SPACING_M.to_bits()
                    || terrain.material_layers.len() != 3
                {
                    return Err(WorkspaceError::UnsupportedTerrainShape);
                }
                let vegetation = &area.vegetation;
                vegetation.validate()?;
                if vegetation.project_id != self.project.id || vegetation.area_id != area.id {
                    return Err(WorkspaceError::VegetationOwnershipMismatch);
                }
                if vegetation.revision.get() == 0 || vegetation.revision > self.revision {
                    return Err(WorkspaceError::InvalidRevision("vegetation"));
                }
                if vegetation.extent_m.size_x_m.to_bits() != AREA_SIZE_M.to_bits()
                    || vegetation.extent_m.size_z_m.to_bits() != AREA_SIZE_M.to_bits()
                {
                    return Err(WorkspaceError::UnsupportedVegetationShape);
                }
                let density = &area.vegetation_density;
                density.validate()?;
                if density.project_id != self.project.id || density.area_id != area.id {
                    return Err(WorkspaceError::VegetationDensityOwnershipMismatch);
                }
                if density.revision.get() == 0 || density.revision > self.revision {
                    return Err(WorkspaceError::InvalidRevision("vegetation_density"));
                }
                if density.cells_per_chunk_axis != vegetation.cells_per_chunk_axis
                    || density.layers.len() != vegetation.layers.len()
                    || !density
                        .layers
                        .iter()
                        .zip(&vegetation.layers)
                        .all(|(density, recipe)| density.layer_id == recipe.id)
                {
                    return Err(WorkspaceError::VegetationDensityRecipeMismatch);
                }
            }
        }
        ensure_json_budget(serde_json::to_string_pretty(self)?.len())
    }

    pub fn from_json(json: &str) -> Result<Self, WorkspaceError> {
        ensure_json_budget(json.len())?;
        let document = super::workspace_legacy::read(json)?;
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

pub(crate) fn new_exterior_area(
    project_id: ProjectId,
    area_id: AreaId,
    revision: Revision,
    name: String,
    seed: u64,
    spawn: Vec3,
) -> WorkspaceArea {
    let terrain = TerrainDocument::new(
        project_id,
        area_id,
        revision,
        name.clone(),
        seed,
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
    let vegetation = new_default_vegetation(project_id, area_id, revision, name.clone(), seed);
    let vegetation_density = VegetationDensityDocument::new(
        project_id,
        area_id,
        revision,
        name.clone(),
        vegetation.cells_per_chunk_axis,
        vegetation
            .layers
            .iter()
            .map(|layer| VegetationDensityLayer::new(layer.id.clone(), 0, vec![]))
            .collect(),
    );
    WorkspaceArea {
        id: area_id,
        name,
        kind: WorkspaceAreaKind::Exterior,
        spawn,
        terrain,
        vegetation,
        vegetation_density,
    }
}

fn new_default_vegetation(
    project_id: ProjectId,
    area_id: AreaId,
    revision: Revision,
    name: String,
    seed: u64,
) -> VegetationDocument {
    let meadow_grass = || TerrainMaterialLayerId::new("meadow_grass");
    VegetationDocument::new(
        project_id,
        area_id,
        revision,
        name,
        seed,
        VegetationExtent::new(AREA_SIZE_M, AREA_SIZE_M),
        8,
        vec![
            VegetationSpeciesPreset::Tree.build_layer(
                "forge_trees",
                "Arbres Forge",
                meadow_grass(),
            ),
            VegetationSpeciesPreset::Shrub.build_layer(
                "forge_shrubs",
                "Arbustes Forge",
                meadow_grass(),
            ),
            VegetationSpeciesPreset::Fern.build_layer(
                "forge_ferns",
                "Fougères Forge",
                meadow_grass(),
            ),
            VegetationSpeciesPreset::Grass.build_layer(
                "forge_grass",
                "Nappes d'herbe Forge",
                meadow_grass(),
            ),
        ],
        vec![],
        VegetationPresentationProfile {
            near_lod_distance_m: 24.0,
            medium_lod_distance_m: 72.0,
            hlod_distance_m: 180.0,
            cull_distance_m: 384.0,
            shadow_distance_m: 128.0,
            wind_strength: 0.6,
            wind_speed_hz: 0.18,
            interaction_radius_m: 2.5,
            interaction_strength: 0.75,
            grass_blades_per_patch: 64,
        },
    )
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
    #[error("vegetation project/area identities do not match their workspace owners")]
    VegetationOwnershipMismatch,
    #[error("vegetation density project/area identities do not match their workspace owners")]
    VegetationDensityOwnershipMismatch,
    #[error(
        "workspace requires a current terrain schema, 512 m square, 2 m samples and three material layers"
    )]
    UnsupportedTerrainShape,
    #[error("workspace requires a 512 metre square vegetation recipe")]
    UnsupportedVegetationShape,
    #[error("vegetation density grid/layers do not match the area's vegetation recipe")]
    VegetationDensityRecipeMismatch,
    #[error("workspace JSON contains {bytes} bytes; maximum is {maximum}")]
    JsonBudgetExceeded { bytes: usize, maximum: usize },
    #[error("workspace collection limit exceeded or no module: {0}")]
    CollectionLimit(&'static str),
    #[error("duplicate workspace identity: {0}")]
    DuplicateIdentity(&'static str),
    #[error("workspace target not found: {0}")]
    MissingTarget(&'static str),
    #[error("stale workspace revision")]
    StaleRevision,
    #[error("workspace revision exhausted")]
    RevisionExhausted,
    #[error("workspace edit does not change the document")]
    NoChange,
    #[error("invalid workspace terrain: {0}")]
    Terrain(#[from] TerrainDocumentError),
    #[error("invalid workspace vegetation: {0}")]
    Vegetation(#[from] VegetationDocumentError),
    #[error("invalid workspace vegetation density: {0}")]
    VegetationDensity(#[from] VegetationDensityError),
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
        let area = &document.project.modules[0].areas[0];
        let terrain = &area.terrain;
        assert_eq!(document.project.id, input.project_id);
        assert_eq!(document.project.modules[0].id, input.module_id);
        assert_eq!(document.project.modules[0].areas[0].id, input.area_id);
        assert_eq!(document.project.name, input.project_name);
        assert_eq!(document.project.modules[0].name, input.module_name);
        assert_eq!(document.project.modules[0].areas[0].name, input.area_name);
        assert_eq!(
            document.project.modules[0].areas[0].kind,
            WorkspaceAreaKind::Exterior
        );
        assert_eq!(document.project.modules[0].areas[0].spawn, input.spawn);
        assert_eq!(document.revision, Revision::INITIAL);
        assert_eq!(terrain.revision, Revision::INITIAL);
        assert_eq!(terrain.project_id, input.project_id);
        assert_eq!(terrain.area_id, input.area_id);
        assert_eq!(terrain.seed, input.seed);
        assert_eq!(terrain.schema_version, TERRAIN_DOCUMENT_SCHEMA_VERSION);
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
        assert!(terrain.material_weight_overrides.is_empty());
        terrain.validate().unwrap();
        assert_eq!(area.vegetation.project_id, input.project_id);
        assert_eq!(area.vegetation.area_id, input.area_id);
        assert_eq!(area.vegetation.revision, Revision::INITIAL);
        assert_eq!(area.vegetation.seed, input.seed);
        assert_eq!(area.vegetation.layers.len(), 4);
        assert_eq!(
            area.vegetation_density.cells_per_chunk_axis,
            area.vegetation.cells_per_chunk_axis
        );
        assert_eq!(
            area.vegetation_density.layers.len(),
            area.vegetation.layers.len()
        );
        assert!(
            area.vegetation_density
                .layers
                .iter()
                .zip(&area.vegetation.layers)
                .all(|(density, recipe)| density.layer_id == recipe.id
                    && density.default_density == 0
                    && density.overrides.is_empty())
        );
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
            changed_identity.document.project.modules[0].areas[0]
                .terrain
                .area_id,
            changed_identity.document.project.modules[0].areas[0].id
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
        assert_eq!(
            document.project.modules[0].areas[0].spawn.y.to_bits(),
            17.5_f32.to_bits()
        );
    }

    #[test]
    fn current_workspace_migrates_embedded_v2_terrain_without_losing_authored_heights() {
        let mut legacy = document();
        let terrain = &mut legacy.project.modules[0].areas[0].terrain;
        terrain.schema_version = crate::TERRAIN_DOCUMENT_SCHEMA_VERSION_V2;
        terrain.height_deltas = vec![crate::TerrainHeightDelta::new(128, 128, 3.5)];
        let legacy_revision = terrain.revision;
        let legacy_ids = (terrain.project_id, terrain.area_id);
        let json = serde_json::to_string_pretty(&legacy).unwrap();

        let migrated = WorkspaceDocument::from_json(&json).unwrap();
        let terrain = &migrated.project.modules[0].areas[0].terrain;
        assert_eq!(terrain.schema_version, TERRAIN_DOCUMENT_SCHEMA_VERSION);
        assert_eq!(terrain.revision, legacy_revision);
        assert_eq!((terrain.project_id, terrain.area_id), legacy_ids);
        assert_eq!(
            terrain.height_deltas,
            vec![crate::TerrainHeightDelta::new(128, 128, 3.5)]
        );
        assert!(terrain.material_weight_overrides.is_empty());
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
            assert_eq!(restored.project.modules[0].areas[0].terrain.seed, seed);
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
                    .project
                    .modules[0]
                    .areas[0]
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
        changed.project.modules[0].areas[0].terrain.project_id =
            ProjectId::deterministic("other-project");
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::TerrainOwnershipMismatch)
        ));
        let mut changed = document();
        changed.project.modules[0].areas[0].terrain.area_id = AreaId::deterministic("other-area");
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
            changed.project.modules[0].areas[0].terrain.revision = Revision::new(revision);
            assert!(matches!(
                changed.validate(),
                Err(WorkspaceError::InvalidRevision("terrain"))
            ));
        }
        for mutation in 0..4 {
            let mut changed = document();
            let terrain = &mut changed.project.modules[0].areas[0].terrain;
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
    fn vegetation_recipe_and_density_must_share_workspace_ownership_revision_and_grid() {
        let mut changed = document();
        changed.project.modules[0].areas[0].vegetation.project_id =
            ProjectId::deterministic("other-project");
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::VegetationOwnershipMismatch)
        ));

        let mut changed = document();
        changed.project.modules[0].areas[0]
            .vegetation_density
            .area_id = AreaId::deterministic("other-area");
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::VegetationDensityOwnershipMismatch)
        ));

        for source in ["vegetation", "vegetation_density"] {
            let mut changed = document();
            if source == "vegetation" {
                changed.project.modules[0].areas[0].vegetation.revision = Revision::new(2);
            } else {
                changed.project.modules[0].areas[0]
                    .vegetation_density
                    .revision = Revision::new(2);
            }
            assert!(matches!(
                changed.validate(),
                Err(WorkspaceError::InvalidRevision(actual)) if actual == source
            ));
        }

        let mut changed = document();
        changed.project.modules[0].areas[0]
            .vegetation_density
            .layers
            .swap(0, 1);
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::VegetationDensityRecipeMismatch)
        ));
        let mut changed = document();
        changed.project.modules[0].areas[0]
            .vegetation_density
            .cells_per_chunk_axis = 4;
        assert!(matches!(
            changed.validate(),
            Err(WorkspaceError::VegetationDensityRecipeMismatch)
        ));
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
        value["schema_version"] = json!(WORKSPACE_SCHEMA_VERSION + 1);
        assert!(matches!(
            WorkspaceDocument::from_json(&value.to_string()),
            Err(WorkspaceError::UnsupportedVersion(version)) if version == WORKSPACE_SCHEMA_VERSION + 1
        ));
        for pointer in [
            "",
            "/project",
            "/project/modules/0",
            "/project/modules/0/areas/0",
            "/project/modules/0/areas/0/terrain",
            "/project/modules/0/areas/0/vegetation",
            "/project/modules/0/areas/0/vegetation_density",
        ] {
            let mut value = serde_json::to_value(document()).unwrap();
            value.pointer_mut(pointer).unwrap()["unknown_field"] = json!(true);
            assert!(matches!(
                WorkspaceDocument::from_json(&value.to_string()),
                Err(WorkspaceError::Json(_))
            ));
        }
        let mut value = serde_json::to_value(document()).unwrap();
        value["project"]["modules"][0]["areas"][0]["kind"] = json!("interior");
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
        document.project.modules[0].areas[0].terrain.height_deltas = (0_u16..15_000)
            .map(|index| crate::TerrainHeightDelta::new(index % 257, index / 257, 1.0))
            .collect();
        document.project.modules[0].areas[0]
            .terrain
            .validate()
            .unwrap();
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

    #[test]
    fn sparse_density_paint_stays_bounded_by_the_one_mib_workspace_budget() {
        let mut document = document();
        let area = &mut document.project.modules[0].areas[0];
        area.vegetation_density.layers[3].overrides = (0_u16..64)
            .flat_map(|cell_z| {
                (0_u16..64).map(move |cell_x| {
                    crate::VegetationDensityOverride::new(cell_x, cell_z, u8::MAX)
                })
            })
            .collect();
        let single_area_json = document.to_pretty_json().unwrap();
        assert!(single_area_json.len() <= MAX_WORKSPACE_JSON_BYTES);

        let template = document.project.modules[0].areas[0].clone();
        document.project.modules[0].areas = (0..8)
            .map(|index| {
                let mut area = template.clone();
                area.id = AreaId::deterministic(format!("density-budget-area-{index}"));
                area.terrain.area_id = area.id;
                area.vegetation.area_id = area.id;
                area.vegetation_density.area_id = area.id;
                area
            })
            .collect();
        assert!(serde_json::to_string_pretty(&document).unwrap().len() > MAX_WORKSPACE_JSON_BYTES);
        assert!(matches!(
            document.validate(),
            Err(WorkspaceError::JsonBudgetExceeded { .. })
        ));
    }
}
