use crate::{
    VegetationLodRanges, VegetationPlacementMarker, VegetationProjectionLod,
    VisualValleyQualityStats,
};
use bevy::{
    asset::{DependencyLoadState, LoadState, RecursiveDependencyLoadState},
    camera::visibility::{VisibilityRange, VisibilitySystems},
    gltf::GltfAssetLabel,
    prelude::*,
    transform::TransformSystems,
};
use forge_core::{CanonicalJsonError, ContentHash, canonical_json_sha256};
use forge_project::{TerrainDocument, VegetationDocument, VegetationKind, VisualQualityPreset};
use forge_world::{
    VegetationGenerationError, VegetationGenerationPlan, VegetationInstance, VegetationInstanceId,
};
use serde::{Deserialize, Serialize};
use std::{
    collections::{HashMap, HashSet},
    error::Error,
    fmt,
    path::{Component, Path},
};

const SCENERY_MANIFEST_JSON: &str = include_str!(
    "../../../assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json"
);
const SCENERY_SCHEMA: &str = "forge.asset-subset-manifest";
const SCENERY_SCHEMA_VERSION: u32 = 1;
const SCENERY_SUBSET_ID: &str = "visual-valley-quaternius-stylized-nature-standard-1.0";
const SCENERY_PACK_ID: &str = "quaternius:stylized-nature-megakit-standard-1.0";
const SCENERY_LICENSE: &str = "CC0-1.0";
const SCENERY_ARCHIVE_SHA256: &str =
    "298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9";
const SCENERY_ARCHIVE_BYTES: u64 = 104_088_529;
const SCENERY_METADATA_SHA256: &str =
    "e3286b81f3a6171eb844a83528eac55b2727b2ef8e3f96d1d4e29faddf5b458d";
const SCENERY_LICENSE_SHA256: &str =
    "120710e542c3ebaf83856c4eb55b4a1e680593b39302eba7fdc5d942dfe78c58";
const SCENERY_MODEL_COUNT: usize = 9;
const SCENERY_FILE_COUNT: usize = 27;
const SCENERY_ASSET_PREFIX: &str =
    "visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/";
const SCENERY_FALLBACK: &str =
    "procedural_vegetation_kept_until_all_cc0_models_and_dependencies_load";
const HERO_CENTER_X_M: f32 = 16.0;
const HERO_CENTER_Z_M: f32 = 8.0;
const TREE_RADIUS_M: f32 = 140.0;
const SHRUB_RADIUS_M: f32 = 100.0;
const FERN_RADIUS_M: f32 = 85.0;
const ROCK_RADIUS_M: f32 = 105.0;
const DETAIL_RADIUS_M: f32 = 70.0;
const DETAIL_CULL_DISTANCE_M: f32 = 120.0;
const MAX_SCENERY_INSTANCES: usize = 512;

/// Runtime state of the optional governed scenery overlay.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum SceneryProjectionStatus {
    Pending,
    Activated,
    ProceduralFallbackLoadFailure,
}

/// Deterministic counts by presentation role, never GPU draw-call counts.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
pub struct SceneryInstanceCounts {
    pub trees: u32,
    pub shrubs: u32,
    pub ferns: u32,
    pub rocks: u32,
    pub details: u32,
}

impl SceneryInstanceCounts {
    fn increment(&mut self, kind: SceneryModelKind) -> Result<(), SceneryAdapterError> {
        let value = match kind {
            SceneryModelKind::Tree => &mut self.trees,
            SceneryModelKind::Shrub => &mut self.shrubs,
            SceneryModelKind::Fern => &mut self.ferns,
            SceneryModelKind::Rock => &mut self.rocks,
            SceneryModelKind::Detail => &mut self.details,
        };
        *value = value
            .checked_add(1)
            .ok_or(SceneryAdapterError::CountOverflow("selected_by_kind"))?;
        Ok(())
    }

    pub(crate) fn replacement_instances(self) -> Option<usize> {
        u64::from(self.trees)
            .checked_add(u64::from(self.shrubs))?
            .checked_add(u64::from(self.ferns))?
            .try_into()
            .ok()
    }

    pub(crate) fn detail_instances(self) -> Option<usize> {
        u64::from(self.rocks)
            .checked_add(u64::from(self.details))?
            .try_into()
            .ok()
    }

    pub(crate) fn requested_instances(self) -> Option<usize> {
        self.replacement_instances()?
            .checked_add(self.detail_instances()?)
    }

    pub(crate) fn hidden_procedural_entities(self) -> Option<usize> {
        // Only the replaced tree's Near mesh stays hidden. Medium trees and individual
        // shrubs/ferns remain available as distance-gated relays to the existing HLOD.
        usize::try_from(self.trees).ok()
    }
}

/// Machine-readable evidence for the selected CC0 subset and its runtime activation.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct SceneryProjectionStats {
    pub manifest_hash: String,
    pub subset_id: String,
    pub pack_id: String,
    pub license: String,
    pub tracked_files: usize,
    pub tracked_bytes: u64,
    pub unique_models: usize,
    pub requested_instances: usize,
    pub replacement_instances: usize,
    pub detail_instances: usize,
    pub selected_by_kind: SceneryInstanceCounts,
    pub replacement_lod_start_m: f32,
    pub replacement_lod_end_m: f32,
    pub detail_cull_distance_m: f32,
    pub loaded_models: usize,
    pub failed_models: usize,
    /// Replaced Near tree entities held Hidden after activation, not distance-culled relays.
    pub hidden_procedural_entities: usize,
    pub status: SceneryProjectionStatus,
    pub fallback: &'static str,
}

impl SceneryProjectionStats {
    /// The CC0-to-procedural handoff stays inside the fully visible Medium interval.
    /// Its endpoints come from the renderer's actual crossfade ranges, not the authored labels.
    #[must_use]
    pub fn replacement_lod_range_for_quality(quality: &VisualQualityPreset) -> (f32, f32) {
        let ranges = VegetationLodRanges::from_distances(
            quality.vegetation.near_lod_distance_m,
            quality.vegetation.medium_lod_distance_m,
            quality.vegetation.hlod_distance_m,
            quality.vegetation.cull_distance_m,
        );
        replacement_lod_range(&ranges)
    }
}

/// Optional Gate 1 presentation overlay using a minimal, governed CC0 asset subset.
///
/// The canonical terrain and vegetation documents still own every replacement position. The
/// procedural projection remains visible until every referenced glTF scene and dependency has
/// loaded. If one load fails, all CC0 roots stay hidden and the procedural scene remains intact.
/// Imported glTF materials are preserved; this adapter does not add procedural wind or wetness.
pub struct VisualValleySceneryPlugin {
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq, SystemSet)]
pub(crate) enum VisualValleyScenerySet {
    ApplyQuality,
}

impl VisualValleySceneryPlugin {
    #[must_use]
    pub const fn new(terrain: TerrainDocument, vegetation: VegetationDocument) -> Self {
        Self {
            terrain,
            vegetation,
        }
    }
}

impl Plugin for VisualValleySceneryPlugin {
    fn build(&self, app: &mut App) {
        let prepared =
            prepare_scenery_projection(&self.terrain, &self.vegetation).unwrap_or_else(|error| {
                panic!("scenery plugin requires valid governed inputs: {error}")
            });
        app.insert_resource(prepared.stats.clone())
            .insert_resource(PreparedScenery(Some(prepared)))
            .add_systems(Startup, spawn_scenery_projection)
            .add_systems(Update, observe_scenery_loading)
            .add_systems(
                PostUpdate,
                update_scenery_lod_visibility
                    .after(TransformSystems::Propagate)
                    .after(VisualValleyScenerySet::ApplyQuality)
                    .before(VisibilitySystems::VisibilityPropagate),
            )
            .add_systems(
                PostUpdate,
                apply_scenery_quality_lod
                    .after(crate::quality::VisualValleyQualitySet::Apply)
                    .in_set(VisualValleyScenerySet::ApplyQuality),
            );
    }
}

/// Runs deterministic selection and governance validation without creating an ECS world.
pub fn inspect_scenery_projection(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
) -> Result<SceneryProjectionStats, SceneryAdapterError> {
    Ok(prepare_scenery_projection(terrain, vegetation)?.stats)
}

#[derive(Debug)]
pub enum SceneryAdapterError {
    ManifestJson(serde_json::Error),
    Manifest(&'static str),
    UnsafePath(String),
    DuplicateFile(String),
    DuplicateModel(String),
    MissingModel(String),
    MissingModelFile(String),
    Generation(VegetationGenerationError),
    Hash(CanonicalJsonError),
    CountOverflow(&'static str),
    BudgetExceeded { actual: usize, maximum: usize },
}

impl fmt::Display for SceneryAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::ManifestJson(error) => {
                write!(formatter, "invalid scenery manifest JSON: {error}")
            }
            Self::Manifest(reason) => write!(formatter, "invalid scenery manifest: {reason}"),
            Self::UnsafePath(path) => write!(formatter, "unsafe scenery path `{path}`"),
            Self::DuplicateFile(path) => write!(formatter, "duplicate scenery file `{path}`"),
            Self::DuplicateModel(id) => write!(formatter, "duplicate scenery model `{id}`"),
            Self::MissingModel(id) => write!(formatter, "required scenery model `{id}` is missing"),
            Self::MissingModelFile(path) => {
                write!(
                    formatter,
                    "scenery model source `{path}` is not listed in files"
                )
            }
            Self::Generation(error) => write!(formatter, "vegetation generation failed: {error}"),
            Self::Hash(error) => write!(formatter, "cannot hash scenery manifest: {error}"),
            Self::CountOverflow(field) => write!(formatter, "scenery count `{field}` overflowed"),
            Self::BudgetExceeded { actual, maximum } => write!(
                formatter,
                "scenery projection requests {actual} instances, above the Gate 1 budget {maximum}"
            ),
        }
    }
}

impl Error for SceneryAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::ManifestJson(error) => Some(error),
            Self::Generation(error) => Some(error),
            Self::Hash(error) => Some(error),
            Self::Manifest(_)
            | Self::UnsafePath(_)
            | Self::DuplicateFile(_)
            | Self::DuplicateModel(_)
            | Self::MissingModel(_)
            | Self::MissingModelFile(_)
            | Self::CountOverflow(_)
            | Self::BudgetExceeded { .. } => None,
        }
    }
}

impl From<serde_json::Error> for SceneryAdapterError {
    fn from(value: serde_json::Error) -> Self {
        Self::ManifestJson(value)
    }
}

impl From<VegetationGenerationError> for SceneryAdapterError {
    fn from(value: VegetationGenerationError) -> Self {
        Self::Generation(value)
    }
}

impl From<CanonicalJsonError> for SceneryAdapterError {
    fn from(value: CanonicalJsonError) -> Self {
        Self::Hash(value)
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SceneryManifest {
    schema: String,
    schema_version: u32,
    subset_id: String,
    source: ScenerySource,
    license: SceneryLicense,
    models: Vec<SceneryModel>,
    files: Vec<SceneryFile>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct ScenerySource {
    pack_id: String,
    name: String,
    version: String,
    author: String,
    source_page: String,
    distribution_page: String,
    archive_url: String,
    retrieved_at: String,
    archive_sha256: ContentHash,
    archive_bytes: u64,
    normalized_metadata_sha256: ContentHash,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SceneryLicense {
    spdx: String,
    url: String,
    verified: bool,
    attribution_required: bool,
    evidence_path: String,
    evidence_sha256: ContentHash,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SceneryModel {
    id: String,
    kind: SceneryModelKind,
    scene_path: String,
    source_path: String,
    base_scale: f32,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
enum SceneryModelKind {
    Tree,
    Shrub,
    Fern,
    Rock,
    Detail,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct SceneryFile {
    path: String,
    source_path: String,
    bytes: u64,
    sha256: ContentHash,
    transformation: String,
}

impl SceneryManifest {
    fn embedded() -> Result<Self, SceneryAdapterError> {
        let manifest: Self = serde_json::from_str(SCENERY_MANIFEST_JSON)?;
        manifest.validate()?;
        Ok(manifest)
    }

    fn embedded_source_hash() -> Result<ContentHash, SceneryAdapterError> {
        let source: serde_json::Value = serde_json::from_str(SCENERY_MANIFEST_JSON)?;
        Ok(canonical_json_sha256(&source)?)
    }

    #[allow(
        clippy::too_many_lines,
        reason = "the manifest gate keeps all pinned identity, provenance, license, file and model checks contiguous"
    )]
    fn validate(&self) -> Result<(), SceneryAdapterError> {
        if self.schema != SCENERY_SCHEMA {
            return Err(SceneryAdapterError::Manifest("unsupported schema"));
        }
        if self.schema_version != SCENERY_SCHEMA_VERSION {
            return Err(SceneryAdapterError::Manifest("unsupported schema version"));
        }
        if self.subset_id != SCENERY_SUBSET_ID || self.source.pack_id != SCENERY_PACK_ID {
            return Err(SceneryAdapterError::Manifest("unexpected subset identity"));
        }
        if self.source.name != "Stylized Nature MegaKit"
            || self.source.version != "Standard-1.0"
            || self.source.author != "Quaternius"
            || self.source.source_page != "https://quaternius.itch.io/stylized-nature-megakit"
            || self.source.distribution_page
                != "https://store.godotengine.org/asset/quaternius/stylized-nature-megakit/"
            || self.source.archive_url
                != "https://store.godotengine.org/asset/quaternius/stylized-nature-megakit/download/31/"
            || self.source.retrieved_at != "2026-08-07T06:24:10Z"
            || self.source.archive_sha256 != pinned_hash(SCENERY_ARCHIVE_SHA256)?
            || self.source.archive_bytes != SCENERY_ARCHIVE_BYTES
            || self.source.normalized_metadata_sha256 != pinned_hash(SCENERY_METADATA_SHA256)?
        {
            return Err(SceneryAdapterError::Manifest(
                "incomplete source provenance",
            ));
        }
        if self.license.spdx != SCENERY_LICENSE
            || self.license.url != "https://creativecommons.org/publicdomain/zero/1.0/"
            || !self.license.verified
            || self.license.attribution_required
            || self.license.evidence_sha256 != pinned_hash(SCENERY_LICENSE_SHA256)?
        {
            return Err(SceneryAdapterError::Manifest(
                "license must be verified CC0-1.0 without required attribution",
            ));
        }
        if self.models.len() != SCENERY_MODEL_COUNT || self.files.len() != SCENERY_FILE_COUNT {
            return Err(SceneryAdapterError::Manifest(
                "unexpected governed subset cardinality",
            ));
        }

        let mut file_paths = HashSet::with_capacity(self.files.len());
        let mut source_paths = HashSet::with_capacity(self.files.len());
        for file in &self.files {
            validate_relative_path(&file.path)?;
            validate_relative_path(&file.source_path)?;
            reject_forbidden_identity(&file.path)?;
            reject_forbidden_identity(&file.source_path)?;
            if file.bytes == 0 || file.transformation != "byte-for-byte-copy" {
                return Err(SceneryAdapterError::Manifest(
                    "files must be non-empty byte-for-byte copies",
                ));
            }
            if !file_paths.insert(file.path.as_str()) {
                return Err(SceneryAdapterError::DuplicateFile(file.path.clone()));
            }
            if !source_paths.insert(file.source_path.as_str()) {
                return Err(SceneryAdapterError::DuplicateFile(file.source_path.clone()));
            }
        }

        let license_file = self
            .files
            .iter()
            .find(|file| file.path == self.license.evidence_path)
            .ok_or(SceneryAdapterError::Manifest(
                "missing license evidence file",
            ))?;
        if license_file.sha256 != self.license.evidence_sha256 {
            return Err(SceneryAdapterError::Manifest(
                "license evidence hash does not match the tracked file",
            ));
        }

        let mut model_ids = HashSet::with_capacity(self.models.len());
        for model in &self.models {
            validate_relative_path(&model.scene_path)?;
            validate_relative_path(&model.source_path)?;
            reject_forbidden_identity(&model.id)?;
            reject_forbidden_identity(&model.scene_path)?;
            reject_forbidden_identity(&model.source_path)?;
            if !model_ids.insert(model.id.as_str()) {
                return Err(SceneryAdapterError::DuplicateModel(model.id.clone()));
            }
            if !model.scene_path.starts_with(SCENERY_ASSET_PREFIX)
                || !model.base_scale.is_finite()
                || !(0.05..=4.0).contains(&model.base_scale)
            {
                return Err(SceneryAdapterError::Manifest(
                    "model path or base scale is outside the governed range",
                ));
            }
            let Some(relative_scene_path) = model.scene_path.strip_prefix(SCENERY_ASSET_PREFIX)
            else {
                return Err(SceneryAdapterError::Manifest("invalid model asset prefix"));
            };
            let source_file = self
                .files
                .iter()
                .find(|file| file.path == relative_scene_path)
                .ok_or_else(|| SceneryAdapterError::MissingModelFile(model.scene_path.clone()))?;
            if source_file.source_path != model.source_path {
                return Err(SceneryAdapterError::Manifest(
                    "model source path does not match its file record",
                ));
            }
        }
        RequiredModels::from_manifest(self)?;
        Ok(())
    }
}

fn pinned_hash(value: &'static str) -> Result<ContentHash, SceneryAdapterError> {
    ContentHash::parse(value)
        .map_err(|_| SceneryAdapterError::Manifest("invalid pinned provenance hash"))
}

fn validate_relative_path(value: &str) -> Result<(), SceneryAdapterError> {
    let path = Path::new(value);
    let safe = !value.is_empty()
        && !value.contains('\\')
        && !path.is_absolute()
        && path
            .components()
            .all(|component| matches!(component, Component::Normal(_)));
    if safe {
        Ok(())
    } else {
        Err(SceneryAdapterError::UnsafePath(value.to_owned()))
    }
}

fn reject_forbidden_identity(value: &str) -> Result<(), SceneryAdapterError> {
    if value.to_ascii_lowercase().contains("cadle") {
        Err(SceneryAdapterError::Manifest(
            "reference-only upstream identity is forbidden in scenery content",
        ))
    } else {
        Ok(())
    }
}

#[derive(Clone, Copy)]
struct RequiredModels {
    common_tree_1: usize,
    common_tree_3: usize,
    pine_2: usize,
    flowering_bush: usize,
    fern_1: usize,
    rock_medium_1: usize,
    rock_medium_3: usize,
    mushroom: usize,
    flowers: usize,
}

impl RequiredModels {
    fn from_manifest(manifest: &SceneryManifest) -> Result<Self, SceneryAdapterError> {
        let indices = manifest
            .models
            .iter()
            .enumerate()
            .map(|(index, model)| (model.id.as_str(), index))
            .collect::<HashMap<_, _>>();
        let required = |id: &'static str| {
            indices
                .get(id)
                .copied()
                .ok_or_else(|| SceneryAdapterError::MissingModel(id.to_owned()))
        };
        Ok(Self {
            common_tree_1: required("common-tree-1")?,
            common_tree_3: required("common-tree-3")?,
            pine_2: required("pine-2")?,
            flowering_bush: required("flowering-bush")?,
            fern_1: required("fern-1")?,
            rock_medium_1: required("rock-medium-1")?,
            rock_medium_3: required("rock-medium-3")?,
            mushroom: required("laetiporus-mushroom")?,
            flowers: required("flower-group-4")?,
        })
    }
}

#[derive(Resource)]
struct PreparedScenery(Option<PreparedSceneryProjection>);

#[derive(Clone, Debug, PartialEq)]
struct PreparedSceneryInstance {
    source_id: VegetationInstanceId,
    model_index: usize,
    translation: Vec3,
    yaw_radians: f32,
    scale: f32,
    replaces_procedural: bool,
    maximum_visibility_distance_m: f32,
}

struct PreparedSceneryProjection {
    manifest: SceneryManifest,
    instances: Vec<PreparedSceneryInstance>,
    replacement_ids: HashSet<VegetationInstanceId>,
    stats: SceneryProjectionStats,
}

#[derive(Component)]
struct SceneryRoot {
    replacement_lod_phase: Option<f32>,
    maximum_visibility_distance_m: f32,
}

/// A reversible presentation override; the procedural LOD entities are never destroyed.
#[derive(Component)]
struct SceneryProceduralReplacement {
    lod_phase: f32,
}

#[derive(Resource)]
struct SceneryLoading {
    handles: Vec<Handle<WorldAsset>>,
    replacement_ids: HashSet<VegetationInstanceId>,
    terminal: bool,
}

fn prepare_scenery_projection(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
) -> Result<PreparedSceneryProjection, SceneryAdapterError> {
    let manifest = SceneryManifest::embedded()?;
    let required = RequiredModels::from_manifest(&manifest)?;
    let generated = VegetationGenerationPlan::from_documents(terrain, vegetation)?.generate()?;
    let mut instances = Vec::new();
    let mut replacement_ids = HashSet::new();
    let mut selected_by_kind = SceneryInstanceCounts::default();
    let (replacement_lod_start_m, replacement_lod_end_m) =
        scenery_replacement_lod_range(vegetation, None);

    for source in generated
        .chunks
        .iter()
        .flat_map(|chunk| chunk.instances.iter())
    {
        let Some(selection) = select_model(source, required) else {
            continue;
        };
        if instances.len() >= MAX_SCENERY_INSTANCES {
            return Err(SceneryAdapterError::BudgetExceeded {
                actual: instances.len().saturating_add(1),
                maximum: MAX_SCENERY_INSTANCES,
            });
        }
        let model = &manifest.models[selection.model_index];
        selected_by_kind.increment(model.kind)?;
        if selection.replaces_procedural {
            replacement_ids.insert(source.id);
        }
        instances.push(PreparedSceneryInstance {
            source_id: source.id,
            model_index: selection.model_index,
            translation: Vec3::new(
                source.position_m.x,
                source.position_m.y,
                source.position_m.z,
            ),
            yaw_radians: source.yaw_radians,
            scale: source.scale * model.base_scale,
            replaces_procedural: selection.replaces_procedural,
            maximum_visibility_distance_m: if selection.replaces_procedural {
                replacement_maximum_visibility_distance(
                    replacement_lod_start_m,
                    replacement_lod_end_m,
                    stable_lod_phase(source.id),
                )
            } else {
                DETAIL_CULL_DISTANCE_M
            },
        });
    }

    let tracked_bytes = manifest.files.iter().try_fold(0_u64, |total, file| {
        total
            .checked_add(file.bytes)
            .ok_or(SceneryAdapterError::CountOverflow("tracked_bytes"))
    })?;
    let replacement_instances = instances
        .iter()
        .filter(|instance| instance.replaces_procedural)
        .count();
    let detail_instances = instances.len().saturating_sub(replacement_instances);
    let stats = SceneryProjectionStats {
        manifest_hash: SceneryManifest::embedded_source_hash()?.to_string(),
        subset_id: manifest.subset_id.clone(),
        pack_id: manifest.source.pack_id.clone(),
        license: manifest.license.spdx.clone(),
        tracked_files: manifest.files.len(),
        tracked_bytes,
        unique_models: manifest.models.len(),
        requested_instances: instances.len(),
        replacement_instances,
        detail_instances,
        selected_by_kind,
        replacement_lod_start_m,
        replacement_lod_end_m,
        detail_cull_distance_m: DETAIL_CULL_DISTANCE_M,
        loaded_models: 0,
        failed_models: 0,
        hidden_procedural_entities: 0,
        status: SceneryProjectionStatus::Pending,
        fallback: SCENERY_FALLBACK,
    };

    Ok(PreparedSceneryProjection {
        manifest,
        instances,
        replacement_ids,
        stats,
    })
}

#[derive(Clone, Copy)]
struct ModelSelection {
    model_index: usize,
    replaces_procedural: bool,
}

fn select_model(source: &VegetationInstance, required: RequiredModels) -> Option<ModelSelection> {
    let selector = stable_selector(source.id);
    let replacement = |model_index| {
        Some(ModelSelection {
            model_index,
            replaces_procedural: true,
        })
    };
    let detail = |model_index| {
        Some(ModelSelection {
            model_index,
            replaces_procedural: false,
        })
    };

    match source.kind {
        VegetationKind::Tree if inside_hero_radius(source, TREE_RADIUS_M) => {
            let tree = match (selector / 3) % 3 {
                0 => required.common_tree_1,
                1 => required.common_tree_3,
                _ => required.pine_2,
            };
            replacement(tree)
        }
        VegetationKind::Shrub
            if inside_hero_radius(source, SHRUB_RADIUS_M) && selector.is_multiple_of(4) =>
        {
            replacement(required.flowering_bush)
        }
        VegetationKind::Fern
            if inside_hero_radius(source, FERN_RADIUS_M) && selector.is_multiple_of(5) =>
        {
            replacement(required.fern_1)
        }
        VegetationKind::Grass
            if inside_hero_radius(source, ROCK_RADIUS_M) && selector.is_multiple_of(37) =>
        {
            let rock = if (selector / 37).is_multiple_of(2) {
                required.rock_medium_1
            } else {
                required.rock_medium_3
            };
            detail(rock)
        }
        VegetationKind::Grass
            if inside_hero_radius(source, DETAIL_RADIUS_M) && selector.is_multiple_of(53) =>
        {
            let ground_detail = if (selector / 53).is_multiple_of(2) {
                required.mushroom
            } else {
                required.flowers
            };
            detail(ground_detail)
        }
        _ => None,
    }
}

fn inside_hero_radius(instance: &VegetationInstance, radius_m: f32) -> bool {
    let offset_x = instance.position_m.x - HERO_CENTER_X_M;
    let offset_z = instance.position_m.z - HERO_CENTER_Z_M;
    offset_x.mul_add(offset_x, offset_z * offset_z) <= radius_m * radius_m
}

fn stable_selector(id: VegetationInstanceId) -> u64 {
    let uuid = id.as_entity_id().as_uuid();
    let bytes = uuid.as_bytes();
    u64::from_le_bytes([
        bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7],
    ])
}

fn stable_lod_phase(id: VegetationInstanceId) -> f32 {
    let bytes = id.as_entity_id().as_uuid().into_bytes();
    f32::from(u16::from_le_bytes([bytes[8], bytes[9]])) / f32::from(u16::MAX)
}

fn scenery_replacement_lod_range(
    vegetation: &VegetationDocument,
    quality: Option<&VisualQualityPreset>,
) -> (f32, f32) {
    quality.map_or_else(
        || replacement_lod_range(&VegetationLodRanges::from_profile(vegetation.presentation)),
        SceneryProjectionStats::replacement_lod_range_for_quality,
    )
}

fn replacement_lod_range(ranges: &VegetationLodRanges) -> (f32, f32) {
    (
        ranges.medium.start_margin.end,
        ranges.hlod.start_margin.start,
    )
}

fn replacement_maximum_visibility_distance(start_m: f32, end_m: f32, phase: f32) -> f32 {
    start_m + phase * (end_m - start_m)
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn spawn_scenery_projection(
    mut commands: Commands,
    asset_server: Res<AssetServer>,
    mut prepared: ResMut<PreparedScenery>,
) {
    let prepared = prepared
        .0
        .take()
        .expect("scenery projection preparation is consumed exactly once");
    let handles = prepared
        .manifest
        .models
        .iter()
        .map(|model| {
            asset_server.load(GltfAssetLabel::Scene(0).from_asset(model.scene_path.clone()))
        })
        .collect::<Vec<Handle<WorldAsset>>>();

    for instance in prepared.instances {
        let model = &prepared.manifest.models[instance.model_index];
        commands.spawn((
            Name::new(format!(
                "CC0 scenery {} from {}",
                model.id, instance.source_id
            )),
            SceneryRoot {
                replacement_lod_phase: instance
                    .replaces_procedural
                    .then(|| stable_lod_phase(instance.source_id)),
                maximum_visibility_distance_m: instance.maximum_visibility_distance_m,
            },
            WorldAssetRoot(handles[instance.model_index].clone()),
            Transform::from_translation(instance.translation)
                .with_rotation(Quat::from_rotation_y(instance.yaw_radians))
                .with_scale(Vec3::splat(instance.scale)),
            Visibility::Hidden,
        ));
    }
    commands.insert_resource(SceneryLoading {
        handles,
        replacement_ids: prepared.replacement_ids,
        terminal: false,
    });
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn apply_scenery_quality_lod(
    quality: Option<Res<VisualValleyQualityStats>>,
    mut stats: ResMut<SceneryProjectionStats>,
    mut roots: Query<&mut SceneryRoot>,
    mut procedural: Query<(&SceneryProceduralReplacement, &mut VisibilityRange)>,
    mut last_applied_tier: Local<Option<forge_project::VisualQualityTier>>,
) {
    let Some(quality) = quality.as_deref() else {
        return;
    };
    if quality.dynamically_applied_tier != Some(quality.current_tier) {
        return;
    }
    let (replacement_lod_start_m, replacement_lod_end_m) =
        SceneryProjectionStats::replacement_lod_range_for_quality(&quality.requested_preset);
    let changed = stats.replacement_lod_start_m.to_bits() != replacement_lod_start_m.to_bits()
        || stats.replacement_lod_end_m.to_bits() != replacement_lod_end_m.to_bits();
    if !changed && *last_applied_tier == Some(quality.current_tier) {
        return;
    }

    for mut root in &mut roots {
        let Some(phase) = root.replacement_lod_phase else {
            continue;
        };
        root.maximum_visibility_distance_m = replacement_maximum_visibility_distance(
            replacement_lod_start_m,
            replacement_lod_end_m,
            phase,
        );
    }
    // These markers only exist after successful loading. Quality first restores each base
    // procedural range; then this adapter reinstalls the exact, reversible CC0 handoff.
    if stats.status == SceneryProjectionStatus::Activated {
        for (replacement, mut range) in &mut procedural {
            let threshold = replacement_maximum_visibility_distance(
                replacement_lod_start_m,
                replacement_lod_end_m,
                replacement.lod_phase,
            );
            range.start_margin = threshold..threshold;
        }
    }
    stats.replacement_lod_start_m = replacement_lod_start_m;
    stats.replacement_lod_end_m = replacement_lod_end_m;
    *last_applied_tier = Some(quality.current_tier);
}

fn desired_root_visibility(
    status: SceneryProjectionStatus,
    distance_squared_m: f32,
    maximum_visibility_distance_m: f32,
) -> Visibility {
    if status == SceneryProjectionStatus::Activated
        && distance_squared_m.is_finite()
        && distance_squared_m < maximum_visibility_distance_m * maximum_visibility_distance_m
    {
        Visibility::Inherited
    } else {
        Visibility::Hidden
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn update_scenery_lod_visibility(
    stats: Res<SceneryProjectionStats>,
    cameras: Query<(&Camera, &GlobalTransform), With<Camera3d>>,
    mut roots: Query<(&SceneryRoot, &GlobalTransform, &mut Visibility)>,
) {
    for (root, transform, mut visibility) in &mut roots {
        let root_position = transform.translation();
        let distance_squared_m = cameras
            .iter()
            .filter(|(camera, _)| camera.is_active)
            .map(|(_, camera_transform)| {
                camera_transform
                    .translation()
                    .distance_squared(root_position)
            })
            .fold(f32::INFINITY, f32::min);
        let desired = desired_root_visibility(
            stats.status,
            distance_squared_m,
            root.maximum_visibility_distance_m,
        );
        if *visibility != desired {
            *visibility = desired;
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum GovernedLoadState {
    Pending,
    Loaded,
    Failed,
}

fn configure_procedural_replacement(
    marker: &VegetationPlacementMarker,
    range: &mut VisibilityRange,
    visibility: &mut Visibility,
    handoff_start_m: f32,
    handoff_end_m: f32,
) -> Option<SceneryProceduralReplacement> {
    if marker.kind == VegetationKind::Tree && marker.lod == VegetationProjectionLod::Near {
        *visibility = Visibility::Hidden;
        return None;
    }
    let lod_phase = stable_lod_phase(marker.id);
    let threshold =
        replacement_maximum_visibility_distance(handoff_start_m, handoff_end_m, lod_phase);
    // Keep the procedural end fade, so the shader's existing complementary HLOD fade is intact.
    // CC0 roots are visible strictly below the same threshold; this range includes its start.
    range.start_margin = threshold..threshold;
    *visibility = Visibility::Inherited;
    Some(SceneryProceduralReplacement { lod_phase })
}

fn governed_load_state(
    states: Option<&(LoadState, DependencyLoadState, RecursiveDependencyLoadState)>,
) -> GovernedLoadState {
    match states {
        Some(
            (LoadState::Failed(_), _, _)
            | (_, DependencyLoadState::Failed(_), _)
            | (_, _, RecursiveDependencyLoadState::Failed(_)),
        ) => GovernedLoadState::Failed,
        Some((
            LoadState::Loaded,
            DependencyLoadState::Loaded,
            RecursiveDependencyLoadState::Loaded,
        )) => GovernedLoadState::Loaded,
        _ => GovernedLoadState::Pending,
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy systems own their parameter wrappers and this ParamSet prevents visibility aliasing"
)]
fn observe_scenery_loading(
    mut commands: Commands,
    asset_server: Res<AssetServer>,
    mut loading: Option<ResMut<SceneryLoading>>,
    mut stats: ResMut<SceneryProjectionStats>,
    mut visibility: ParamSet<(
        Query<&mut Visibility, With<SceneryRoot>>,
        Query<
            (
                Entity,
                &VegetationPlacementMarker,
                &mut VisibilityRange,
                &mut Visibility,
            ),
            Without<SceneryRoot>,
        >,
    )>,
) {
    let Some(loading) = loading.as_deref_mut() else {
        return;
    };
    if loading.terminal {
        return;
    }

    let mut loaded_models = 0_usize;
    let mut failed_models = 0_usize;
    for handle in &loading.handles {
        match governed_load_state(asset_server.get_load_states(handle.id()).as_ref()) {
            GovernedLoadState::Pending => {}
            GovernedLoadState::Loaded => loaded_models += 1,
            GovernedLoadState::Failed => failed_models += 1,
        }
    }
    stats.loaded_models = loaded_models;
    stats.failed_models = failed_models;

    if failed_models > 0 {
        stats.status = SceneryProjectionStatus::ProceduralFallbackLoadFailure;
        loading.terminal = true;
        warn!(
            target: "forge::scenery",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "visual_valley_scenery_fallback",
            loaded_models,
            failed_models,
            fallback = SCENERY_FALLBACK,
            "CC0 scenery loading failed; the procedural vegetation remains authoritative and visible"
        );
        return;
    }
    if loaded_models != loading.handles.len() {
        return;
    }

    for mut root_visibility in &mut visibility.p0() {
        *root_visibility = Visibility::Inherited;
    }
    let mut hidden_procedural_entities = 0_usize;
    for (entity, marker, mut range, mut procedural_visibility) in &mut visibility.p1() {
        if loading.replacement_ids.contains(&marker.id) {
            if let Some(replacement) = configure_procedural_replacement(
                marker,
                &mut range,
                &mut procedural_visibility,
                stats.replacement_lod_start_m,
                stats.replacement_lod_end_m,
            ) {
                commands.entity(entity).insert(replacement);
            } else {
                hidden_procedural_entities += 1;
            }
        }
    }
    stats.hidden_procedural_entities = hidden_procedural_entities;
    stats.status = SceneryProjectionStatus::Activated;
    loading.terminal = true;
    info!(
        target: "forge::scenery",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "visual_valley_scenery_activated",
        manifest_hash = %stats.manifest_hash,
        subset_id = %stats.subset_id,
        pack_id = %stats.pack_id,
        license = %stats.license,
        tracked_files = stats.tracked_files,
        tracked_bytes = stats.tracked_bytes,
        unique_models = stats.unique_models,
        requested_instances = stats.requested_instances,
        replacement_instances = stats.replacement_instances,
        detail_instances = stats.detail_instances,
        replacement_lod_start_m = stats.replacement_lod_start_m,
        replacement_lod_end_m = stats.replacement_lod_end_m,
        detail_cull_distance_m = stats.detail_cull_distance_m,
        hidden_procedural_entities,
        "governed CC0 scenery activated atomically after dependency loading"
    );
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::asset::AssetLoadError;
    use forge_world::{
        embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    };
    use sha2::{Digest, Sha256};
    use std::{fs, path::PathBuf, sync::Arc};

    fn asset_subset_root() -> PathBuf {
        Path::new(env!("CARGO_MANIFEST_DIR")).join(
            "../../assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0",
        )
    }

    #[test]
    fn embedded_manifest_is_strict_and_governed() {
        let manifest = SceneryManifest::embedded().unwrap();
        assert_eq!(manifest.schema_version, SCENERY_SCHEMA_VERSION);
        assert_eq!(manifest.source.pack_id, SCENERY_PACK_ID);
        assert_eq!(manifest.license.spdx, SCENERY_LICENSE);
        assert!(manifest.license.verified);
        assert!(!manifest.license.attribution_required);
        assert_eq!(manifest.models.len(), SCENERY_MODEL_COUNT);
        assert_eq!(manifest.files.len(), SCENERY_FILE_COUNT);
        assert!(
            manifest
                .models
                .iter()
                .all(|model| !model.scene_path.to_ascii_lowercase().contains("cadle"))
        );
    }

    #[test]
    fn embedded_manifest_hash_preserves_the_canonical_source_document() {
        assert_eq!(
            SceneryManifest::embedded_source_hash().unwrap().to_string(),
            "6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f"
        );
    }

    #[test]
    fn tracked_subset_matches_every_declared_byte_and_dependency() {
        let manifest = SceneryManifest::embedded().unwrap();
        let root = asset_subset_root();
        let declared = manifest
            .files
            .iter()
            .map(|file| file.path.as_str())
            .collect::<HashSet<_>>();

        for file in &manifest.files {
            let path = root.join(&file.path);
            let bytes = fs::read(&path).unwrap_or_else(|error| {
                panic!(
                    "cannot read declared scenery file {}: {error}",
                    path.display()
                )
            });
            assert_eq!(
                u64::try_from(bytes.len()).unwrap(),
                file.bytes,
                "{}",
                file.path
            );
            let actual = ContentHash::from_bytes(Sha256::digest(&bytes).into());
            assert_eq!(actual, file.sha256, "{}", file.path);

            if Path::new(&file.path)
                .extension()
                .is_some_and(|extension| extension.eq_ignore_ascii_case("gltf"))
            {
                let json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
                let dependencies = json["buffers"]
                    .as_array()
                    .into_iter()
                    .flatten()
                    .chain(json["images"].as_array().into_iter().flatten())
                    .filter_map(|entry| entry["uri"].as_str());
                for dependency in dependencies {
                    assert!(
                        declared.contains(dependency),
                        "{} references undeclared dependency {dependency}",
                        file.path
                    );
                }
            }
        }

        let mut actual = fs::read_dir(&root)
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect::<Vec<_>>();
        actual.sort();
        let mut expected = manifest
            .files
            .iter()
            .map(|file| file.path.clone())
            .chain(["subset.manifest.json".to_owned()])
            .collect::<Vec<_>>();
        expected.sort();
        assert_eq!(actual, expected);
    }

    #[test]
    fn selection_is_deterministic_bounded_and_uses_every_model() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let first = prepare_scenery_projection(&terrain, &vegetation).unwrap();
        let second = prepare_scenery_projection(&terrain, &vegetation).unwrap();

        assert_eq!(first.instances, second.instances);
        assert_eq!(first.stats.manifest_hash, second.stats.manifest_hash);
        assert_eq!(
            first.stats.manifest_hash,
            "6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f"
        );
        assert_eq!(first.stats.requested_instances, first.instances.len());
        assert!(first.stats.requested_instances <= MAX_SCENERY_INSTANCES);
        assert!(first.stats.replacement_instances > 0);
        assert!(first.stats.detail_instances > 0);
        assert_eq!(
            first.stats.replacement_instances,
            first.replacement_ids.len()
        );
        assert_eq!(
            (
                first.stats.replacement_lod_start_m,
                first.stats.replacement_lod_end_m
            ),
            replacement_lod_range(&VegetationLodRanges::from_profile(vegetation.presentation))
        );
        let used = first
            .instances
            .iter()
            .map(|instance| instance.model_index)
            .collect::<HashSet<_>>();
        assert_eq!(used.len(), first.manifest.models.len());
        assert!(first.instances.iter().all(|instance| {
            instance.translation.is_finite()
                && instance.yaw_radians.is_finite()
                && instance.scale.is_finite()
                && instance.scale > 0.0
                && instance.maximum_visibility_distance_m.is_finite()
                && if instance.replaces_procedural {
                    (first.stats.replacement_lod_start_m..=first.stats.replacement_lod_end_m)
                        .contains(&instance.maximum_visibility_distance_m)
                } else {
                    (instance.maximum_visibility_distance_m - first.stats.detail_cull_distance_m)
                        .abs()
                        <= f32::EPSILON
                }
        }));
    }

    #[test]
    fn every_quality_tier_drives_the_cc0_replacement_lod_range() {
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let profile = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();

        for (tier, expected_start_m, expected_end_m) in [
            (forge_project::VisualQualityTier::Low, 35.0_f32, 82.0_f32),
            (
                forge_project::VisualQualityTier::Medium,
                43.0_f32,
                104.0_f32,
            ),
            (forge_project::VisualQualityTier::High, 51.0_f32, 120.0_f32),
            (forge_project::VisualQualityTier::Ultra, 67.0_f32, 162.0_f32),
        ] {
            let (start_m, end_m) =
                scenery_replacement_lod_range(&vegetation, Some(profile.preset(tier)));
            assert_eq!(start_m.to_bits(), expected_start_m.to_bits());
            assert_eq!(end_m.to_bits(), expected_end_m.to_bits());
            assert_eq!(
                replacement_maximum_visibility_distance(start_m, end_m, 0.0).to_bits(),
                expected_start_m.to_bits()
            );
            assert_eq!(
                replacement_maximum_visibility_distance(start_m, end_m, 1.0).to_bits(),
                expected_end_m.to_bits()
            );
        }
    }

    #[test]
    fn hero_selection_uses_every_eligible_tree_without_moving_the_source() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let prepared = prepare_scenery_projection(&terrain, &vegetation).unwrap();
        let generated = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        let mut eligible_trees = 0_u32;
        for source in generated.chunks.iter().flat_map(|chunk| &chunk.instances) {
            if source.kind != VegetationKind::Tree {
                continue;
            }
            let selected = prepared
                .instances
                .iter()
                .find(|instance| instance.source_id == source.id);
            if !inside_hero_radius(source, TREE_RADIUS_M) {
                assert!(selected.is_none());
                continue;
            }
            eligible_trees += 1;
            let selected = selected.unwrap();
            assert!(selected.replaces_procedural);
            let model = &prepared.manifest.models[selected.model_index];
            assert!(matches!(
                model.id.as_str(),
                "common-tree-1" | "common-tree-3" | "pine-2"
            ));
            assert_eq!(
                selected.translation,
                Vec3::new(
                    source.position_m.x,
                    source.position_m.y,
                    source.position_m.z
                )
            );
            assert_eq!(selected.yaw_radians.to_bits(), source.yaw_radians.to_bits());
            assert_eq!(
                selected.scale.to_bits(),
                (source.scale * model.base_scale).to_bits()
            );
        }
        assert_eq!(prepared.stats.selected_by_kind.trees, eligible_trees);
        assert!(eligible_trees > 48);
        assert_eq!(prepared.stats.selected_by_kind.shrubs, 33);
        assert_eq!(prepared.stats.selected_by_kind.ferns, 18);
        assert_eq!(prepared.stats.selected_by_kind.rocks, 22);
        assert_eq!(prepared.stats.selected_by_kind.details, 4);
        assert_eq!(
            prepared.stats.selected_by_kind.hidden_procedural_entities(),
            usize::try_from(eligible_trees).ok()
        );
        assert!(prepared.instances.len() <= MAX_SCENERY_INSTANCES);
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "the four tiers share one complete replacement-kind transition matrix"
    )]
    fn procedural_relay_keeps_hlod_fades_and_only_hides_near_tree_meshes() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let generated = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        let profile = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        for tier in [
            forge_project::VisualQualityTier::Low,
            forge_project::VisualQualityTier::Medium,
            forge_project::VisualQualityTier::High,
            forge_project::VisualQualityTier::Ultra,
        ] {
            let preset = profile.preset(tier);
            let ranges = VegetationLodRanges::from_distances(
                preset.vegetation.near_lod_distance_m,
                preset.vegetation.medium_lod_distance_m,
                preset.vegetation.hlod_distance_m,
                preset.vegetation.cull_distance_m,
            );
            let (start, end) = replacement_lod_range(&ranges);
            for (kind, lod, original) in [
                (
                    VegetationKind::Tree,
                    VegetationProjectionLod::Near,
                    ranges.near.clone(),
                ),
                (
                    VegetationKind::Tree,
                    VegetationProjectionLod::Medium,
                    ranges.medium.clone(),
                ),
                (
                    VegetationKind::Shrub,
                    VegetationProjectionLod::Near,
                    ranges.individual.clone(),
                ),
                (
                    VegetationKind::Fern,
                    VegetationProjectionLod::Near,
                    ranges.individual.clone(),
                ),
            ] {
                let source = generated
                    .chunks
                    .iter()
                    .flat_map(|chunk| &chunk.instances)
                    .find(|source| source.kind == kind)
                    .unwrap();
                let marker = VegetationPlacementMarker {
                    id: source.id,
                    kind,
                    lod,
                };
                let mut range = original.clone();
                let mut visibility = Visibility::Inherited;
                let replacement = configure_procedural_replacement(
                    &marker,
                    &mut range,
                    &mut visibility,
                    start,
                    end,
                );
                if kind == VegetationKind::Tree && lod == VegetationProjectionLod::Near {
                    assert!(replacement.is_none());
                    assert_eq!(visibility, Visibility::Hidden);
                    assert!(range == original);
                    continue;
                }
                let threshold = replacement_maximum_visibility_distance(
                    start,
                    end,
                    replacement.unwrap().lod_phase,
                );
                assert_eq!(visibility, Visibility::Inherited);
                assert_eq!(range.start_margin, threshold..threshold);
                assert_eq!(range.end_margin, ranges.hlod.start_margin);
                assert!(!range.use_aabb);
                for distance in [
                    threshold - 0.01,
                    threshold,
                    threshold + 0.01,
                    threshold,
                    threshold - 0.01,
                ] {
                    let cc0 = desired_root_visibility(
                        SceneryProjectionStatus::Activated,
                        distance * distance,
                        threshold,
                    ) == Visibility::Inherited;
                    assert_ne!(cc0, range.is_visible_at_all(distance));
                }
                assert!(range.is_visible_at_all(ranges.hlod.start_margin.start));
                assert!(!range.is_visible_at_all(ranges.hlod.start_margin.end));
            }
        }
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "one transaction test covers roots, procedural relays and restoration"
    )]
    fn applied_ultra_quality_updates_existing_cc0_roots_after_the_quality_transaction() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let mut prepared = prepare_scenery_projection(&terrain, &vegetation).unwrap();
        prepared.stats.status = SceneryProjectionStatus::Activated;
        let profile = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .insert_resource(prepared.stats)
            .insert_resource(crate::vegetation::VegetationPresentation(
                vegetation.presentation,
            ))
            .add_plugins(crate::VisualValleyQualityPlugin::new(
                profile,
                crate::VisualValleyQualitySelection::Fixed(forge_project::VisualQualityTier::Ultra),
            ))
            .add_systems(
                PostUpdate,
                apply_scenery_quality_lod
                    .after(crate::quality::VisualValleyQualitySet::Apply)
                    .in_set(VisualValleyScenerySet::ApplyQuality),
            )
            .add_systems(
                PostUpdate,
                update_scenery_lod_visibility
                    .after(TransformSystems::Propagate)
                    .after(VisualValleyScenerySet::ApplyQuality)
                    .before(VisibilitySystems::VisibilityPropagate),
            );
        app.world_mut().spawn((
            Camera3d::default(),
            GlobalTransform::from_translation(Vec3::X * 80.0),
        ));
        let replacement = app
            .world_mut()
            .spawn((
                SceneryRoot {
                    replacement_lod_phase: Some(0.5),
                    maximum_visibility_distance_m: 1.0,
                },
                GlobalTransform::IDENTITY,
                Visibility::Hidden,
            ))
            .id();
        let detail = app
            .world_mut()
            .spawn(SceneryRoot {
                replacement_lod_phase: None,
                maximum_visibility_distance_m: DETAIL_CULL_DISTANCE_M,
            })
            .id();
        let source = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        let tree = source
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .find(|source| source.kind == VegetationKind::Tree)
            .unwrap();
        let relay = app
            .world_mut()
            .spawn((
                SceneryProceduralReplacement { lod_phase: 0.5 },
                VegetationPlacementMarker {
                    id: tree.id,
                    kind: VegetationKind::Tree,
                    lod: VegetationProjectionLod::Medium,
                },
                VegetationLodRanges::from_profile(vegetation.presentation).medium,
            ))
            .id();

        app.update();

        let world = app.world();
        let stats = world.resource::<SceneryProjectionStats>();
        assert_eq!(stats.replacement_lod_start_m.to_bits(), 67.0_f32.to_bits());
        assert_eq!(stats.replacement_lod_end_m.to_bits(), 162.0_f32.to_bits());
        assert_eq!(
            world
                .get::<SceneryRoot>(replacement)
                .unwrap()
                .maximum_visibility_distance_m
                .to_bits(),
            114.5_f32.to_bits()
        );
        assert_eq!(
            world
                .get::<SceneryRoot>(detail)
                .unwrap()
                .maximum_visibility_distance_m
                .to_bits(),
            DETAIL_CULL_DISTANCE_M.to_bits()
        );
        assert_eq!(
            world.get::<VisibilityRange>(relay).unwrap().start_margin,
            114.5..114.5
        );
        assert_eq!(
            world.get::<VisibilityRange>(relay).unwrap().end_margin,
            162.0..174.0
        );
        assert_eq!(
            world.get::<Visibility>(replacement),
            Some(&Visibility::Inherited)
        );
        assert!(
            !world
                .get::<VisibilityRange>(relay)
                .unwrap()
                .is_visible_at_all(80.0)
        );

        for (tier, threshold, end_margin) in [
            (forge_project::VisualQualityTier::Low, 58.5, 82.0..94.0),
            (forge_project::VisualQualityTier::Ultra, 114.5, 162.0..174.0),
            (forge_project::VisualQualityTier::Low, 58.5, 82.0..94.0),
        ] {
            crate::quality::set_quality_tier_for_test(app.world_mut(), tier);
            app.update();
            let range = app.world().get::<VisibilityRange>(relay).unwrap();
            assert_eq!(range.start_margin, threshold..threshold);
            assert_eq!(range.end_margin, end_margin);
            assert_eq!(
                app.world()
                    .get::<SceneryRoot>(replacement)
                    .unwrap()
                    .maximum_visibility_distance_m
                    .to_bits(),
                threshold.to_bits()
            );
            let cc0_visible =
                app.world().get::<Visibility>(replacement) == Some(&Visibility::Inherited);
            assert_eq!(cc0_visible, 80.0 < threshold);
            assert_ne!(cc0_visible, range.is_visible_at_all(80.0));
        }
    }

    #[test]
    fn root_visibility_is_bounded_and_requires_activation() {
        assert_eq!(
            desired_root_visibility(SceneryProjectionStatus::Pending, 1.0, 120.0),
            Visibility::Hidden
        );
        assert_eq!(
            desired_root_visibility(SceneryProjectionStatus::Activated, 119.0 * 119.0, 120.0),
            Visibility::Inherited
        );
        assert_eq!(
            desired_root_visibility(SceneryProjectionStatus::Activated, 121.0 * 121.0, 120.0),
            Visibility::Hidden
        );
        assert_eq!(
            desired_root_visibility(SceneryProjectionStatus::Activated, 120.0 * 120.0, 120.0),
            Visibility::Hidden
        );
        assert_eq!(
            desired_root_visibility(
                SceneryProjectionStatus::ProceduralFallbackLoadFailure,
                1.0,
                120.0,
            ),
            Visibility::Hidden
        );
    }

    #[test]
    fn unsafe_unlicensed_or_unpinned_manifest_changes_are_rejected() {
        let mut unsafe_manifest = SceneryManifest::embedded().unwrap();
        unsafe_manifest.files[0].path = "../outside.png".to_owned();
        assert!(matches!(
            unsafe_manifest.validate(),
            Err(SceneryAdapterError::UnsafePath(_))
        ));

        let mut unlicensed = SceneryManifest::embedded().unwrap();
        unlicensed.license.verified = false;
        assert!(matches!(
            unlicensed.validate(),
            Err(SceneryAdapterError::Manifest(_))
        ));

        let mut forbidden_identity = SceneryManifest::embedded().unwrap();
        forbidden_identity.models[0].id = "cadle-tree".to_owned();
        assert!(matches!(
            forbidden_identity.validate(),
            Err(SceneryAdapterError::Manifest(_))
        ));

        let mut changed_archive = SceneryManifest::embedded().unwrap();
        changed_archive.source.archive_bytes += 1;
        assert!(matches!(
            changed_archive.validate(),
            Err(SceneryAdapterError::Manifest(_))
        ));

        let mut oversized_subset = SceneryManifest::embedded().unwrap();
        let duplicate_file = oversized_subset.files[0].clone();
        oversized_subset.files.push(duplicate_file);
        assert!(matches!(
            oversized_subset.validate(),
            Err(SceneryAdapterError::Manifest(_))
        ));
    }

    #[test]
    fn load_state_requires_root_and_recursive_dependencies() {
        let failure = Arc::new(AssetLoadError::MissingAssetLoader {
            asset_type_id: None,
            asset_path: "missing.gltf".to_owned(),
        });
        assert_eq!(governed_load_state(None), GovernedLoadState::Pending);
        assert_eq!(
            governed_load_state(Some(&(
                LoadState::Loaded,
                DependencyLoadState::Loading,
                RecursiveDependencyLoadState::Loading,
            ))),
            GovernedLoadState::Pending
        );
        assert_eq!(
            governed_load_state(Some(&(
                LoadState::Loaded,
                DependencyLoadState::Loaded,
                RecursiveDependencyLoadState::Loaded,
            ))),
            GovernedLoadState::Loaded
        );
        assert_eq!(
            governed_load_state(Some(&(
                LoadState::Failed(failure.clone()),
                DependencyLoadState::Loaded,
                RecursiveDependencyLoadState::Loaded,
            ))),
            GovernedLoadState::Failed
        );
        assert_eq!(
            governed_load_state(Some(&(
                LoadState::Loaded,
                DependencyLoadState::Failed(failure.clone()),
                RecursiveDependencyLoadState::Loaded,
            ))),
            GovernedLoadState::Failed
        );
        assert_eq!(
            governed_load_state(Some(&(
                LoadState::Loaded,
                DependencyLoadState::Loaded,
                RecursiveDependencyLoadState::Failed(failure),
            ))),
            GovernedLoadState::Failed
        );
    }
}
