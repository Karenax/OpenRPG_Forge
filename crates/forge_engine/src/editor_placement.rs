//! Atomic Bevy projection for governed, persistent editor placements.
//!
//! The project document remains authoritative. This adapter keeps the previously committed
//! projection visible while replacement glTF roots and all recursive dependencies load.
use bevy::{
    asset::{DependencyLoadState, LoadState, RecursiveDependencyLoadState},
    gltf::GltfAssetLabel,
    prelude::*,
};
use forge_core::{AreaId, AssetId, ContentHash, EntityId, ProjectId, Transform as ForgeTransform};
use forge_project::PlacementDocument;
use serde::Serialize;
use std::{
    collections::{BTreeMap, BTreeSet},
    path::{Component, Path},
    sync::{Arc, Mutex},
};

pub const MAX_EDITOR_PLACEMENTS: usize = 4_096;
pub const MAX_EDITOR_PLACEMENT_SCENES: usize = 512;
pub const MAX_EDITOR_PLACEMENT_TRACKED_BYTES: u64 = 1_073_741_824;
const MAX_SCENE_PATH_BYTES: usize = 512;

/// Conservative verified byte budget for one pinned asset subset.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct EditorPlacementAssetBudget {
    pub asset_id: AssetId,
    pub tracked_bytes: u64,
}

/// Validated renderer-neutral fields required from the persistent placement ledger.
///
/// The conversion from `forge_project::PlacementDocument` is installed once that public schema is
/// available. Keeping this type private prevents Studio from inventing a second canonical model.
#[derive(Clone, Debug, PartialEq)]
pub(crate) struct PreparedEditorPlacement {
    pub id: EntityId,
    pub asset_id: AssetId,
    pub scene_path: String,
    pub scene_hash: ContentHash,
    pub tracked_bytes: u64,
    pub transform: ForgeTransform,
}

#[derive(Clone, Debug)]
pub(crate) struct PreparedEditorPlacementRequest {
    pub generation: u64,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: u64,
    pub source_hash: ContentHash,
    pub placements: Vec<PreparedEditorPlacement>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum EditorPlacementProjectionPhase {
    Empty,
    Loading,
    Presented,
    Failed,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct EditorPlacementProjectionStatus {
    pub desired_generation: u64,
    pub presented_generation: u64,
    pub project_id: Option<ProjectId>,
    pub area_id: Option<AreaId>,
    pub revision: u64,
    pub source_hash: String,
    pub phase: EditorPlacementProjectionPhase,
    pub presented_placements: usize,
    pub staged_placements: usize,
    pub retained_placements: usize,
    pub terrain_mesh_uploads: u64,
    pub geometry_cache_rebuilds: u64,
    pub error: Option<String>,
}

impl Default for EditorPlacementProjectionStatus {
    fn default() -> Self {
        Self {
            desired_generation: 0,
            presented_generation: 0,
            project_id: None,
            area_id: None,
            revision: 0,
            source_hash: String::new(),
            phase: EditorPlacementProjectionPhase::Empty,
            presented_placements: 0,
            staged_placements: 0,
            retained_placements: 0,
            terrain_mesh_uploads: 0,
            geometry_cache_rebuilds: 0,
            error: None,
        }
    }
}

#[derive(Default)]
struct EditorPlacementBridgeState {
    desired_generation: u64,
    pending: Option<PreparedEditorPlacementRequest>,
    pending_clear: Option<u64>,
    status: EditorPlacementProjectionStatus,
}

/// Thread-safe presentation bridge. Persistent mutations stay in `forge_project` commands.
#[derive(Clone, Default, Resource)]
pub struct EditorPlacementProjectionBridge(Arc<Mutex<EditorPlacementBridgeState>>);

impl EditorPlacementProjectionBridge {
    #[must_use]
    pub fn status(&self) -> EditorPlacementProjectionStatus {
        self.0.lock().map_or_else(
            |_| EditorPlacementProjectionStatus::default(),
            |state| state.status.clone(),
        )
    }

    /// Validates and submits one complete persistent ledger projection.
    ///
    /// `asset_budgets` must contain exactly one entry for every asset referenced by a placement.
    /// Its byte count is the conservative verified sum supplied by the native governed scanner.
    /// Repeated instances sharing `(scene_path, scene_content_hash)` are counted only once.
    pub fn submit_document(
        &self,
        generation: u64,
        document: &PlacementDocument,
        asset_budgets: &[EditorPlacementAssetBudget],
    ) -> Result<(), EditorPlacementProjectionError> {
        self.submit(prepare_request(generation, document, asset_budgets)?)
    }

    /// Atomically clears every presented and staged placement for a closed project/area.
    pub fn clear(&self, generation: u64) -> Result<(), EditorPlacementProjectionError> {
        let mut state = self
            .0
            .lock()
            .map_err(|_| EditorPlacementProjectionError::BridgePoisoned)?;
        reject_stale_generation(generation, state.desired_generation)?;
        state.desired_generation = generation;
        state.pending = None;
        state.pending_clear = Some(generation);
        state.status.desired_generation = generation;
        Ok(())
    }

    pub(crate) fn submit(
        &self,
        request: PreparedEditorPlacementRequest,
    ) -> Result<(), EditorPlacementProjectionError> {
        validate_request(&request)?;
        let mut state = self
            .0
            .lock()
            .map_err(|_| EditorPlacementProjectionError::BridgePoisoned)?;
        reject_stale_generation(request.generation, state.desired_generation)?;
        state.desired_generation = request.generation;
        state.pending_clear = None;
        state.status.desired_generation = request.generation;
        state.pending = Some(request);
        Ok(())
    }
}

pub struct EditorPlacementProjectionPlugin(pub EditorPlacementProjectionBridge);

impl Plugin for EditorPlacementProjectionPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(self.0.clone())
            .init_resource::<PlacementProjection>()
            .add_systems(Update, update_editor_placement_projection);
    }
}

#[derive(Component)]
pub struct EditorPlacementRoot {
    pub placement_id: EntityId,
}

struct PresentedPlacement {
    source: PreparedEditorPlacement,
    entity: Entity,
    handle: Handle<WorldAsset>,
}

struct PendingPlacementProjection {
    request: PreparedEditorPlacementRequest,
    retained: BTreeSet<EntityId>,
    staged: BTreeMap<EntityId, PresentedPlacement>,
}

#[derive(Default, Resource)]
struct PlacementProjection {
    active: BTreeMap<EntityId, PresentedPlacement>,
    pending: Option<PendingPlacementProjection>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum GovernedSceneLoadState {
    Pending,
    Loaded,
    Failed,
}

fn update_editor_placement_projection(world: &mut World) {
    accept_latest_request(world);
    observe_pending_projection(world);
}

fn accept_latest_request(world: &mut World) {
    let bridge = world.resource::<EditorPlacementProjectionBridge>().clone();
    let (clear, request) = {
        let Ok(mut state) = bridge.0.lock() else {
            return;
        };
        (state.pending_clear.take(), state.pending.take())
    };
    if let Some(generation) = clear {
        clear_projection(world, &bridge, generation);
        return;
    }
    let Some(request) = request else {
        return;
    };

    cancel_pending_projection(world);
    let changes_scope = !world.resource::<PlacementProjection>().active.is_empty()
        && bridge.0.lock().is_ok_and(|state| {
            state.status.project_id != Some(request.project_id)
                || state.status.area_id != Some(request.area_id)
        });
    if changes_scope {
        let active = std::mem::take(&mut world.resource_mut::<PlacementProjection>().active);
        for placement in active.into_values() {
            world.despawn(placement.entity);
        }
    }
    let asset_server =
        (!request.placements.is_empty()).then(|| world.resource::<AssetServer>().clone());
    let mut retained = BTreeSet::new();
    let mut staged = BTreeMap::new();
    for source in &request.placements {
        let unchanged = world
            .resource::<PlacementProjection>()
            .active
            .get(&source.id)
            .is_some_and(|active| active.source == *source);
        if unchanged {
            retained.insert(source.id);
            continue;
        }
        let handle = asset_server
            .as_ref()
            .expect("non-empty placement request requires Bevy AssetServer")
            .load(GltfAssetLabel::Scene(0).from_asset(source.scene_path.clone()));
        let entity = world
            .spawn((
                Name::new(format!("Editor placement {}", source.id)),
                EditorPlacementRoot {
                    placement_id: source.id,
                },
                WorldAssetRoot(handle.clone()),
                bevy_transform(source.transform),
                Visibility::Hidden,
            ))
            .id();
        staged.insert(
            source.id,
            PresentedPlacement {
                source: source.clone(),
                entity,
                handle,
            },
        );
    }
    let staged_placements = staged.len();
    world.resource_mut::<PlacementProjection>().pending = Some(PendingPlacementProjection {
        request,
        retained,
        staged,
    });
    if let Ok(mut state) = bridge.0.lock() {
        state.status.phase = EditorPlacementProjectionPhase::Loading;
        state.status.staged_placements = staged_placements;
        state.status.retained_placements = world
            .resource::<PlacementProjection>()
            .pending
            .as_ref()
            .map_or(0, |pending| pending.retained.len());
        if changes_scope {
            state.status.presented_placements = 0;
            state.status.project_id = None;
            state.status.area_id = None;
            state.status.revision = 0;
            state.status.source_hash.clear();
        }
        state.status.error = None;
    }
}

fn clear_projection(world: &mut World, bridge: &EditorPlacementProjectionBridge, generation: u64) {
    cancel_pending_projection(world);
    let active = std::mem::take(&mut world.resource_mut::<PlacementProjection>().active);
    for placement in active.into_values() {
        world.despawn(placement.entity);
    }
    if let Ok(mut state) = bridge.0.lock() {
        state.status = EditorPlacementProjectionStatus {
            desired_generation: generation,
            presented_generation: generation,
            ..default()
        };
    }
}

fn reject_stale_generation(
    submitted: u64,
    desired: u64,
) -> Result<(), EditorPlacementProjectionError> {
    if submitted <= desired {
        Err(EditorPlacementProjectionError::StaleGeneration { submitted, desired })
    } else {
        Ok(())
    }
}

fn cancel_pending_projection(world: &mut World) {
    let pending = world.resource_mut::<PlacementProjection>().pending.take();
    if let Some(pending) = pending {
        for placement in pending.staged.into_values() {
            world.despawn(placement.entity);
        }
    }
}

fn observe_pending_projection(world: &mut World) {
    let decision = {
        let projection = world.resource::<PlacementProjection>();
        let Some(pending) = projection.pending.as_ref() else {
            return;
        };
        let asset_server = world.resource::<AssetServer>();
        aggregate_load_state(pending.staged.values().map(|placement| {
            governed_load_state(asset_server.get_load_states(placement.handle.id()).as_ref())
        }))
    };
    match decision {
        GovernedSceneLoadState::Pending => {}
        GovernedSceneLoadState::Failed => fail_pending_projection(world),
        GovernedSceneLoadState::Loaded => commit_pending_projection(world),
    }
}

fn fail_pending_projection(world: &mut World) {
    let bridge = world.resource::<EditorPlacementProjectionBridge>().clone();
    let failed = world.resource_mut::<PlacementProjection>().pending.take();
    let Some(failed) = failed else {
        return;
    };
    for placement in failed.staged.into_values() {
        world.despawn(placement.entity);
    }
    if let Ok(mut state) = bridge.0.lock() {
        state.status.phase = EditorPlacementProjectionPhase::Failed;
        state.status.staged_placements = 0;
        state.status.retained_placements = 0;
        state.status.error = Some(format!(
            "placement projection generation {} failed to load a governed scene or dependency",
            failed.request.generation
        ));
    }
}

fn commit_pending_projection(world: &mut World) {
    let bridge = world.resource::<EditorPlacementProjectionBridge>().clone();
    let pending = world.resource_mut::<PlacementProjection>().pending.take();
    let Some(mut pending) = pending else {
        return;
    };

    let removed = {
        let mut projection = world.resource_mut::<PlacementProjection>();
        let removed_ids = projection
            .active
            .keys()
            .filter(|id| !pending.retained.contains(id))
            .copied()
            .collect::<Vec<_>>();
        removed_ids
            .into_iter()
            .filter_map(|id| projection.active.remove(&id))
            .collect::<Vec<_>>()
    };
    for placement in removed {
        world.despawn(placement.entity);
    }
    for placement in pending.staged.values() {
        world
            .entity_mut(placement.entity)
            .insert(Visibility::Inherited);
    }
    let staged = std::mem::take(&mut pending.staged);
    let presented_placements = {
        let mut projection = world.resource_mut::<PlacementProjection>();
        projection.active.extend(staged);
        projection.active.len()
    };
    if let Ok(mut state) = bridge.0.lock() {
        state.status.presented_generation = pending.request.generation;
        state.status.project_id = Some(pending.request.project_id);
        state.status.area_id = Some(pending.request.area_id);
        state.status.revision = pending.request.revision;
        state.status.source_hash = pending.request.source_hash.to_string();
        state.status.phase = if presented_placements == 0 {
            EditorPlacementProjectionPhase::Empty
        } else {
            EditorPlacementProjectionPhase::Presented
        };
        state.status.presented_placements = presented_placements;
        state.status.staged_placements = 0;
        state.status.retained_placements = pending.retained.len();
        state.status.error = None;
    }
}

fn aggregate_load_state(
    states: impl IntoIterator<Item = GovernedSceneLoadState>,
) -> GovernedSceneLoadState {
    let mut aggregate = GovernedSceneLoadState::Loaded;
    for state in states {
        match state {
            GovernedSceneLoadState::Failed => return GovernedSceneLoadState::Failed,
            GovernedSceneLoadState::Pending => aggregate = GovernedSceneLoadState::Pending,
            GovernedSceneLoadState::Loaded => {}
        }
    }
    aggregate
}

fn governed_load_state(
    states: Option<&(LoadState, DependencyLoadState, RecursiveDependencyLoadState)>,
) -> GovernedSceneLoadState {
    match states {
        Some(
            (LoadState::Failed(_), _, _)
            | (_, DependencyLoadState::Failed(_), _)
            | (_, _, RecursiveDependencyLoadState::Failed(_)),
        ) => GovernedSceneLoadState::Failed,
        Some((
            LoadState::Loaded,
            DependencyLoadState::Loaded,
            RecursiveDependencyLoadState::Loaded,
        )) => GovernedSceneLoadState::Loaded,
        _ => GovernedSceneLoadState::Pending,
    }
}

fn validate_request(
    request: &PreparedEditorPlacementRequest,
) -> Result<(), EditorPlacementProjectionError> {
    if request.placements.len() > MAX_EDITOR_PLACEMENTS {
        return Err(EditorPlacementProjectionError::PlacementBudgetExceeded {
            actual: request.placements.len(),
            maximum: MAX_EDITOR_PLACEMENTS,
        });
    }
    if request.source_hash.as_bytes().iter().all(|byte| *byte == 0) {
        return Err(EditorPlacementProjectionError::ZeroSourceHash);
    }
    let mut ids = BTreeSet::new();
    let mut scenes = BTreeMap::new();
    let mut assets = BTreeMap::new();
    let mut tracked_bytes = 0_u64;
    for placement in &request.placements {
        if !ids.insert(placement.id) {
            return Err(EditorPlacementProjectionError::DuplicatePlacement(
                placement.id,
            ));
        }
        validate_scene_path(&placement.scene_path)?;
        placement
            .transform
            .validate()
            .map_err(|_| EditorPlacementProjectionError::InvalidTransform(placement.id))?;
        if placement
            .scene_hash
            .as_bytes()
            .iter()
            .all(|byte| *byte == 0)
        {
            return Err(EditorPlacementProjectionError::ZeroSceneHash(placement.id));
        }
        if placement.tracked_bytes == 0 {
            return Err(EditorPlacementProjectionError::EmptyScene(placement.id));
        }
        let source = (placement.scene_hash, placement.tracked_bytes);
        if let Some(previous) = scenes.insert(placement.scene_path.as_str(), source)
            && previous != source
        {
            return Err(EditorPlacementProjectionError::ConflictingScenePin(
                placement.scene_path.clone(),
            ));
        }
        if let Some(previous) = assets.insert(placement.asset_id, (&placement.scene_path, source))
            && previous != (&placement.scene_path, source)
        {
            return Err(EditorPlacementProjectionError::ConflictingAssetPin(
                placement.asset_id,
            ));
        }
    }
    if scenes.len() > MAX_EDITOR_PLACEMENT_SCENES {
        return Err(EditorPlacementProjectionError::SceneBudgetExceeded {
            actual: scenes.len(),
            maximum: MAX_EDITOR_PLACEMENT_SCENES,
        });
    }
    for (_, tracked_scene_bytes) in scenes.values() {
        tracked_bytes = tracked_bytes.checked_add(*tracked_scene_bytes).ok_or(
            EditorPlacementProjectionError::TrackedBytesBudgetExceeded {
                actual: u64::MAX,
                maximum: MAX_EDITOR_PLACEMENT_TRACKED_BYTES,
            },
        )?;
    }
    if tracked_bytes > MAX_EDITOR_PLACEMENT_TRACKED_BYTES {
        return Err(EditorPlacementProjectionError::TrackedBytesBudgetExceeded {
            actual: tracked_bytes,
            maximum: MAX_EDITOR_PLACEMENT_TRACKED_BYTES,
        });
    }
    Ok(())
}

fn prepare_request(
    generation: u64,
    document: &PlacementDocument,
    asset_budgets: &[EditorPlacementAssetBudget],
) -> Result<PreparedEditorPlacementRequest, EditorPlacementProjectionError> {
    document
        .validate()
        .map_err(|error| EditorPlacementProjectionError::InvalidDocument(error.to_string()))?;
    let source_hash = document
        .content_hash()
        .map_err(|error| EditorPlacementProjectionError::InvalidDocument(error.to_string()))?;
    let used_assets = document
        .placements
        .iter()
        .map(|placement| placement.asset_id)
        .collect::<BTreeSet<_>>();
    let mut budgets = BTreeMap::new();
    for budget in asset_budgets {
        if budget.tracked_bytes == 0 {
            return Err(EditorPlacementProjectionError::EmptyAssetBudget(
                budget.asset_id,
            ));
        }
        if budgets
            .insert(budget.asset_id, budget.tracked_bytes)
            .is_some()
        {
            return Err(EditorPlacementProjectionError::DuplicateAssetBudget(
                budget.asset_id,
            ));
        }
        if !used_assets.contains(&budget.asset_id) {
            return Err(EditorPlacementProjectionError::UnexpectedAssetBudget(
                budget.asset_id,
            ));
        }
    }
    let mut placements = Vec::with_capacity(document.placements.len());
    for placement in &document.placements {
        let pin = document
            .asset_pins
            .binary_search_by_key(&placement.asset_id, |candidate| candidate.asset_id)
            .ok()
            .and_then(|index| document.asset_pins.get(index))
            .ok_or(EditorPlacementProjectionError::MissingAssetPin(
                placement.asset_id,
            ))?;
        let tracked_bytes = budgets.get(&placement.asset_id).copied().ok_or(
            EditorPlacementProjectionError::MissingAssetBudget(placement.asset_id),
        )?;
        // The authoring command applies `base_scale` when it creates the persistent transform.
        // Reprojection must preserve that authoritative transform byte-for-byte.
        let transform = placement.transform;
        transform
            .validate()
            .map_err(|_| EditorPlacementProjectionError::InvalidTransform(placement.id))?;
        placements.push(PreparedEditorPlacement {
            id: placement.id,
            asset_id: placement.asset_id,
            scene_path: pin.scene_path.clone(),
            scene_hash: pin.scene_content_hash,
            tracked_bytes,
            transform,
        });
    }
    let request = PreparedEditorPlacementRequest {
        generation,
        project_id: document.project_id,
        area_id: document.area_id,
        revision: document.revision.get(),
        source_hash,
        placements,
    };
    validate_request(&request)?;
    Ok(request)
}

fn validate_scene_path(path: &str) -> Result<(), EditorPlacementProjectionError> {
    if path.is_empty()
        || path.len() > MAX_SCENE_PATH_BYTES
        || path.contains('\\')
        || path.contains(['%', '?', '#'])
    {
        return Err(EditorPlacementProjectionError::UnsafeScenePath(
            path.to_owned(),
        ));
    }
    let parsed = Path::new(path);
    if parsed
        .components()
        .any(|component| !matches!(component, Component::Normal(_)))
        || parsed.extension().and_then(|extension| extension.to_str()) != Some("gltf")
        || path.to_ascii_lowercase().contains("cadle")
    {
        return Err(EditorPlacementProjectionError::UnsafeScenePath(
            path.to_owned(),
        ));
    }
    Ok(())
}

fn bevy_transform(source: ForgeTransform) -> Transform {
    Transform {
        translation: Vec3::new(
            source.translation_m.x,
            source.translation_m.y,
            source.translation_m.z,
        ),
        rotation: Quat::from_xyzw(
            source.rotation.x,
            source.rotation.y,
            source.rotation.z,
            source.rotation.w,
        ),
        scale: Vec3::new(source.scale.x, source.scale.y, source.scale.z),
    }
}

#[derive(Debug, PartialEq)]
pub enum EditorPlacementProjectionError {
    BridgePoisoned,
    InvalidDocument(String),
    StaleGeneration { submitted: u64, desired: u64 },
    PlacementBudgetExceeded { actual: usize, maximum: usize },
    SceneBudgetExceeded { actual: usize, maximum: usize },
    TrackedBytesBudgetExceeded { actual: u64, maximum: u64 },
    DuplicatePlacement(EntityId),
    MissingAssetPin(AssetId),
    MissingAssetBudget(AssetId),
    EmptyAssetBudget(AssetId),
    DuplicateAssetBudget(AssetId),
    UnexpectedAssetBudget(AssetId),
    ConflictingScenePin(String),
    ConflictingAssetPin(AssetId),
    InvalidTransform(EntityId),
    ZeroSourceHash,
    ZeroSceneHash(EntityId),
    EmptyScene(EntityId),
    UnsafeScenePath(String),
}

impl std::fmt::Display for EditorPlacementProjectionError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for EditorPlacementProjectionError {}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{Revision, UnitQuaternion, Vec3 as ForgeVec3};
    use forge_project::{AssetPin, AuthoredPlacement, asset_id_from_stable_key};

    fn placement(key: &str) -> PreparedEditorPlacement {
        PreparedEditorPlacement {
            id: EntityId::deterministic(format!("placement/{key}")),
            asset_id: AssetId::deterministic(format!("asset/{key}")),
            scene_path: format!("governed/{key}/scene.gltf"),
            scene_hash: ContentHash::from_bytes([2; 32]),
            tracked_bytes: 1_024,
            transform: ForgeTransform {
                translation_m: ForgeVec3::new(1.0, 2.0, 3.0),
                rotation: UnitQuaternion::IDENTITY,
                scale: ForgeVec3::ONE,
            },
        }
    }

    fn request(
        generation: u64,
        placements: Vec<PreparedEditorPlacement>,
    ) -> PreparedEditorPlacementRequest {
        PreparedEditorPlacementRequest {
            generation,
            project_id: ProjectId::deterministic("placements/project"),
            area_id: AreaId::deterministic("placements/area"),
            revision: generation,
            source_hash: ContentHash::from_bytes([1; 32]),
            placements,
        }
    }

    fn document_pin(key: &str, base_scale: f32) -> AssetPin {
        let stable_key = format!("pack/{key}");
        AssetPin {
            asset_id: asset_id_from_stable_key(&stable_key),
            stable_key,
            subset_id: "subset".to_owned(),
            pack_id: "pack".to_owned(),
            source_version: "1.0".to_owned(),
            author: "Author".to_owned(),
            source_path: format!("pack/{key}.gltf"),
            scene_path: format!("pack/{key}.gltf"),
            scene_content_hash: ContentHash::from_bytes([4; 32]),
            manifest_hash: ContentHash::from_bytes([5; 32]),
            license_spdx: "CC0-1.0".to_owned(),
            license_evidence_hash: ContentHash::from_bytes([6; 32]),
            base_scale,
        }
    }

    fn placement_document(pin: AssetPin, placement_keys: &[&str]) -> PlacementDocument {
        let mut placements = placement_keys
            .iter()
            .map(|key| AuthoredPlacement {
                id: EntityId::deterministic(format!("authored/{key}")),
                asset_id: pin.asset_id,
                transform: ForgeTransform {
                    translation_m: ForgeVec3::new(1.0, 2.0, 3.0),
                    rotation: UnitQuaternion::IDENTITY,
                    scale: ForgeVec3::new(2.0, 3.0, 4.0),
                },
            })
            .collect::<Vec<_>>();
        placements.sort_by_key(|placement| placement.id);
        PlacementDocument::new(
            ProjectId::deterministic("document/project"),
            AreaId::deterministic("document/area"),
            Revision::new(7),
            "Placements",
            vec![pin],
            placements,
        )
    }

    #[test]
    fn validation_rejects_duplicates_unsafe_paths_zero_hashes_and_budgets() {
        let first = placement("tree");
        assert!(matches!(
            validate_request(&request(1, vec![first.clone(), first.clone()])),
            Err(EditorPlacementProjectionError::DuplicatePlacement(_))
        ));
        let mut unsafe_path = first.clone();
        unsafe_path.scene_path = "../outside.gltf".to_owned();
        assert!(matches!(
            validate_request(&request(1, vec![unsafe_path])),
            Err(EditorPlacementProjectionError::UnsafeScenePath(_))
        ));
        let mut forbidden = first.clone();
        forbidden.scene_path = "cadle/borrowed.gltf".to_owned();
        assert!(matches!(
            validate_request(&request(1, vec![forbidden])),
            Err(EditorPlacementProjectionError::UnsafeScenePath(_))
        ));
        for path in [
            "governed/tree#variant.gltf",
            "governed/tree?variant.gltf",
            "governed/tree%23variant.gltf",
        ] {
            let mut encoded = first.clone();
            encoded.scene_path = path.to_owned();
            assert!(matches!(
                validate_request(&request(1, vec![encoded])),
                Err(EditorPlacementProjectionError::UnsafeScenePath(_))
            ));
        }
        let mut zero_hash = first.clone();
        zero_hash.scene_hash = ContentHash::from_bytes([0; 32]);
        assert!(matches!(
            validate_request(&request(1, vec![zero_hash])),
            Err(EditorPlacementProjectionError::ZeroSceneHash(_))
        ));
        let mut oversized = first;
        oversized.tracked_bytes = MAX_EDITOR_PLACEMENT_TRACKED_BYTES + 1;
        assert!(matches!(
            validate_request(&request(1, vec![oversized])),
            Err(EditorPlacementProjectionError::TrackedBytesBudgetExceeded { .. })
        ));
    }

    #[test]
    fn recursive_dependency_gate_is_atomic_and_failure_dominates() {
        assert_eq!(
            aggregate_load_state([
                GovernedSceneLoadState::Loaded,
                GovernedSceneLoadState::Pending,
            ]),
            GovernedSceneLoadState::Pending
        );
        assert_eq!(
            aggregate_load_state([
                GovernedSceneLoadState::Pending,
                GovernedSceneLoadState::Failed,
            ]),
            GovernedSceneLoadState::Failed
        );
        assert_eq!(
            aggregate_load_state(std::iter::empty()),
            GovernedSceneLoadState::Loaded
        );
    }

    #[test]
    fn bridge_is_latest_wins_and_never_reopens_an_old_generation() {
        let bridge = EditorPlacementProjectionBridge::default();
        bridge.submit(request(2, vec![placement("tree")])).unwrap();
        assert!(matches!(
            bridge.submit(request(1, vec![])),
            Err(EditorPlacementProjectionError::StaleGeneration {
                submitted: 1,
                desired: 2
            })
        ));
        bridge.submit(request(3, vec![])).unwrap();
        let state = bridge.0.lock().unwrap();
        assert_eq!(state.pending.as_ref().unwrap().generation, 3);
        assert_eq!(state.status.desired_generation, 3);
        assert_eq!(state.status.terrain_mesh_uploads, 0);
        assert_eq!(state.status.geometry_cache_rebuilds, 0);
    }

    #[test]
    fn clear_is_latest_wins_and_removes_active_staging_and_scope() {
        let bridge = EditorPlacementProjectionBridge::default();
        bridge.submit(request(2, vec![placement("tree")])).unwrap();
        bridge.clear(3).unwrap();
        assert!(matches!(
            bridge.clear(2),
            Err(EditorPlacementProjectionError::StaleGeneration {
                submitted: 2,
                desired: 3
            })
        ));
        let active_source = placement("active");
        let staged_source = placement("staged");
        let mut world = World::new();
        world.insert_resource(bridge.clone());
        let active_entity = world.spawn(Visibility::Inherited).id();
        let staged_entity = world.spawn(Visibility::Hidden).id();
        world.insert_resource(PlacementProjection {
            active: BTreeMap::from([(active_source.id, presented(active_source, active_entity))]),
            pending: Some(PendingPlacementProjection {
                request: request(2, vec![staged_source.clone()]),
                retained: BTreeSet::new(),
                staged: BTreeMap::from([(
                    staged_source.id,
                    presented(staged_source, staged_entity),
                )]),
            }),
        });

        accept_latest_request(&mut world);

        assert!(world.resource::<PlacementProjection>().active.is_empty());
        assert!(world.resource::<PlacementProjection>().pending.is_none());
        assert!(world.get_entity(active_entity).is_err());
        assert!(world.get_entity(staged_entity).is_err());
        let status = bridge.status();
        assert_eq!(status.desired_generation, 3);
        assert_eq!(status.presented_generation, 3);
        assert_eq!(status.project_id, None);
        assert_eq!(status.area_id, None);
        assert_eq!(status.revision, 0);
        assert!(status.source_hash.is_empty());
        assert_eq!(status.phase, EditorPlacementProjectionPhase::Empty);
        assert_eq!(status.presented_placements, 0);
    }

    #[test]
    fn cross_area_staging_never_leaves_the_previous_area_visible() {
        let bridge = EditorPlacementProjectionBridge::default();
        let old_source = placement("old-area");
        let mut world = World::new();
        world.insert_resource(bridge.clone());
        let old_entity = world.spawn(Visibility::Inherited).id();
        world.insert_resource(PlacementProjection {
            active: BTreeMap::from([(old_source.id, presented(old_source, old_entity))]),
            pending: None,
        });
        let mut replacement = request(2, vec![]);
        replacement.project_id = ProjectId::deterministic("placements/other-project");
        replacement.area_id = AreaId::deterministic("placements/other-area");
        {
            let mut state = bridge.0.lock().unwrap();
            state.desired_generation = 2;
            state.pending = Some(replacement);
            state.status = EditorPlacementProjectionStatus {
                desired_generation: 2,
                presented_generation: 1,
                project_id: Some(ProjectId::deterministic("placements/project")),
                area_id: Some(AreaId::deterministic("placements/area")),
                revision: 1,
                source_hash: ContentHash::from_bytes([9; 32]).to_string(),
                phase: EditorPlacementProjectionPhase::Presented,
                presented_placements: 1,
                ..default()
            };
        }

        accept_latest_request(&mut world);

        assert!(world.resource::<PlacementProjection>().active.is_empty());
        assert!(world.get_entity(old_entity).is_err());
        assert!(world.resource::<PlacementProjection>().pending.is_some());
        let status = bridge.status();
        assert_eq!(status.phase, EditorPlacementProjectionPhase::Loading);
        assert_eq!(status.presented_placements, 0);
        assert_eq!(status.project_id, None);
        assert_eq!(status.area_id, None);
    }

    #[test]
    fn public_document_submission_binds_hash_budget_and_preserves_authored_scale() {
        let pin = document_pin("tree", 0.5);
        let document = placement_document(pin.clone(), &["one", "two"]);
        let budget = EditorPlacementAssetBudget {
            asset_id: pin.asset_id,
            tracked_bytes: MAX_EDITOR_PLACEMENT_TRACKED_BYTES,
        };
        let prepared = prepare_request(9, &document, &[budget]).unwrap();

        assert_eq!(prepared.source_hash, document.content_hash().unwrap());
        assert_eq!(prepared.revision, 7);
        assert_eq!(prepared.placements.len(), 2);
        assert!(
            prepared
                .placements
                .iter()
                .all(|placement| placement.tracked_bytes == budget.tracked_bytes)
        );
        assert_eq!(
            prepared.placements[0].transform.scale,
            ForgeVec3::new(2.0, 3.0, 4.0)
        );
        assert!(matches!(
            prepare_request(10, &document, &[]),
            Err(EditorPlacementProjectionError::MissingAssetBudget(id)) if id == pin.asset_id
        ));
        assert!(matches!(
            prepare_request(10, &document, &[budget, budget]),
            Err(EditorPlacementProjectionError::DuplicateAssetBudget(id)) if id == pin.asset_id
        ));
    }

    #[test]
    fn unchanged_fingerprint_includes_handle_source_transform_and_bytes() {
        let original = placement("tree");
        assert_eq!(original, original.clone());
        let mut moved = original.clone();
        moved.transform.translation_m.x += 1.0;
        assert_ne!(original, moved);
        let mut repinned = original.clone();
        repinned.scene_hash = ContentHash::from_bytes([3; 32]);
        assert_ne!(original, repinned);
        let mut resized = original.clone();
        resized.tracked_bytes += 1;
        assert_ne!(original, resized);
    }

    fn presented(source: PreparedEditorPlacement, entity: Entity) -> PresentedPlacement {
        PresentedPlacement {
            source,
            entity,
            handle: Handle::default(),
        }
    }

    #[test]
    fn failure_destroys_only_staging_and_keeps_previous_projection_bit_stable() {
        let bridge = EditorPlacementProjectionBridge::default();
        let mut world = World::new();
        world.insert_resource(bridge.clone());
        let active_source = placement("active");
        let active_entity = world.spawn(Visibility::Inherited).id();
        let staged_entity = world.spawn(Visibility::Hidden).id();
        let active_id = active_source.id;
        let mut active = BTreeMap::new();
        active.insert(active_id, presented(active_source.clone(), active_entity));
        let staged_source = placement("staged");
        let mut staged = BTreeMap::new();
        staged.insert(
            staged_source.id,
            presented(staged_source.clone(), staged_entity),
        );
        world.insert_resource(PlacementProjection {
            active,
            pending: Some(PendingPlacementProjection {
                request: request(2, vec![staged_source]),
                retained: BTreeSet::new(),
                staged,
            }),
        });

        fail_pending_projection(&mut world);

        let projection = world.resource::<PlacementProjection>();
        let retained = projection.active.get(&active_id).unwrap();
        assert_eq!(retained.entity, active_entity);
        assert_eq!(retained.source, active_source);
        assert!(world.get_entity(active_entity).is_ok());
        assert!(world.get_entity(staged_entity).is_err());
        assert_eq!(
            bridge.status().phase,
            EditorPlacementProjectionPhase::Failed
        );
    }

    #[test]
    fn atomic_swap_retains_unchanged_entity_removes_undo_and_adds_redo_without_mesh_uploads() {
        let bridge = EditorPlacementProjectionBridge::default();
        let mut world = World::new();
        world.insert_resource(bridge.clone());
        let mut meshes = Assets::<Mesh>::default();
        meshes.add(Cuboid::new(1.0, 1.0, 1.0));
        world.insert_resource(meshes);

        let retained_source = placement("retained");
        let removed_source = placement("undo-removed");
        let added_source = placement("redo-added");
        let retained_entity = world.spawn(Visibility::Inherited).id();
        let removed_entity = world.spawn(Visibility::Inherited).id();
        let added_entity = world.spawn(Visibility::Hidden).id();
        let mut active = BTreeMap::new();
        active.insert(
            retained_source.id,
            presented(retained_source.clone(), retained_entity),
        );
        active.insert(removed_source.id, presented(removed_source, removed_entity));
        let mut staged = BTreeMap::new();
        staged.insert(
            added_source.id,
            presented(added_source.clone(), added_entity),
        );
        world.insert_resource(PlacementProjection {
            active,
            pending: Some(PendingPlacementProjection {
                request: request(2, vec![retained_source.clone(), added_source.clone()]),
                retained: BTreeSet::from([retained_source.id]),
                staged,
            }),
        });
        let mesh_count = world.resource::<Assets<Mesh>>().len();

        commit_pending_projection(&mut world);

        let projection = world.resource::<PlacementProjection>();
        assert_eq!(projection.active.len(), 2);
        assert_eq!(
            projection.active.get(&retained_source.id).unwrap().entity,
            retained_entity
        );
        assert_eq!(
            projection.active.get(&added_source.id).unwrap().entity,
            added_entity
        );
        assert!(world.get_entity(removed_entity).is_err());
        assert_eq!(
            world.get::<Visibility>(added_entity),
            Some(&Visibility::Inherited)
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), mesh_count);
        let status = bridge.status();
        assert_eq!(status.presented_generation, 2);
        assert_eq!(status.retained_placements, 1);
        assert_eq!(status.terrain_mesh_uploads, 0);
        assert_eq!(status.geometry_cache_rebuilds, 0);

        let all_active = world
            .resource::<PlacementProjection>()
            .active
            .keys()
            .copied()
            .collect::<BTreeSet<_>>();
        world.resource_mut::<PlacementProjection>().pending = Some(PendingPlacementProjection {
            request: request(3, vec![]),
            retained: BTreeSet::new(),
            staged: BTreeMap::new(),
        });
        commit_pending_projection(&mut world);
        assert!(world.resource::<PlacementProjection>().active.is_empty());
        for id in all_active {
            assert!(
                world
                    .query::<&EditorPlacementRoot>()
                    .iter(&world)
                    .all(|root| root.placement_id != id)
            );
        }
        assert_eq!(world.resource::<Assets<Mesh>>().len(), mesh_count);

        let restored_retained_entity = world.spawn(Visibility::Hidden).id();
        let restored_added_entity = world.spawn(Visibility::Hidden).id();
        world.resource_mut::<PlacementProjection>().pending = Some(PendingPlacementProjection {
            request: request(4, vec![retained_source.clone(), added_source.clone()]),
            retained: BTreeSet::new(),
            staged: BTreeMap::from([
                (
                    retained_source.id,
                    presented(retained_source.clone(), restored_retained_entity),
                ),
                (
                    added_source.id,
                    presented(added_source.clone(), restored_added_entity),
                ),
            ]),
        });
        commit_pending_projection(&mut world);
        let projection = world.resource::<PlacementProjection>();
        assert_eq!(projection.active.len(), 2);
        assert_eq!(
            projection.active.get(&retained_source.id).unwrap().entity,
            restored_retained_entity
        );
        assert_eq!(
            projection.active.get(&added_source.id).unwrap().entity,
            restored_added_entity
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), mesh_count);
    }
}
