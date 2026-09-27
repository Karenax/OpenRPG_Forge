use crate::GovernedAssetKitRuntimeStats;
#[cfg(test)]
use bevy::mesh::VertexAttributeValues;
use bevy::{
    asset::LoadState,
    camera::visibility::VisibilityRange,
    gltf::GltfAssetLabel,
    light::NotShadowCaster,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use bevy_rapier3d::prelude::{Collider, RapierColliderHandle};
use forge_assets::{
    GovernedAssetKitBuildReport, GovernedAssetKitError, GovernedAssetKitRuntimePreview,
};
use forge_core::ContentHash;
use forge_project::{TerrainDocument, TerrainDocumentError};
use forge_world::{
    TerrainGenerationError, TerrainGenerationPlan, WorldSketchCamera, WorldSketchDocument,
    WorldSketchError,
};
use std::{collections::HashMap, error::Error, fmt};
use tracing::{error, info};
#[cfg(feature = "qualification")]
mod physics_probe;
mod temporal;

const NEAR_TO_MEDIUM_LOD_M: std::ops::Range<f32> = 36.0..52.0;
const MEDIUM_TO_FAR_LOD_M: std::ops::Range<f32> = 112.0..144.0;
const GOVERNED_ASSET_CULL_DISTANCE_M: f32 = 1_500.0;
const MAX_GOVERNED_ASSET_HIERARCHY_DEPTH: usize = 64;

/// Persistent proof identity carried by one transient GLB scene root.
#[derive(Clone, Component, Debug, Eq, PartialEq)]
pub struct GovernedAssetKitPlacementMarker {
    pub placement_id: String,
    pub asset_id: String,
}

#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
enum GovernedAssetKitRenderLod {
    Lod0,
    Lod1,
    Lod2,
}

#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
struct GovernedAssetKitCollisionNode;

#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
struct GovernedAssetKitCollisionFailed;

type UnconfiguredGovernedAssetNode = (
    Added<Name>,
    Without<Mesh3d>,
    Without<GovernedAssetKitRenderLod>,
    Without<GovernedAssetKitCollisionNode>,
);
type PendingGovernedAssetCollider = (
    With<GovernedAssetKitCollisionPrimitive>,
    Without<GovernedAssetKitCollisionFailed>,
    Without<Collider>,
);

#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
struct GovernedAssetKitCollisionPrimitive;

#[derive(Clone, Resource)]
struct GovernedAssetKitRuntimePlan {
    report: GovernedAssetKitBuildReport,
    preview: GovernedAssetKitRuntimePreview,
    camera: WorldSketchCamera,
    spawn_validation_camera: bool,
    placements: Vec<PreparedGovernedAssetKitPlacement>,
}

#[derive(Clone)]
struct PreparedGovernedAssetKitPlacement {
    placement_id: String,
    asset_id: String,
    asset_path: String,
    translation: Vec3,
    yaw_radians: f32,
    scale: f32,
}

#[derive(Resource)]
struct GovernedAssetKitSceneHandles(Vec<Handle<WorldAsset>>);

#[derive(Default, Resource)]
struct GovernedAssetKitLoadLogged {
    ready: bool,
    failed: bool,
}

/// Transient readiness for playtest; file loading alone does not imply physical readiness.
#[derive(Clone, Copy, Debug, Default, Resource)]
pub struct GovernedAssetKitReadiness {
    pub ready: bool,
    pub ready_instances: usize,
    pub registered_colliders: usize,
}

impl GovernedAssetKitReadiness {
    fn matches_complete_projection(self, stats: &GovernedAssetKitRuntimeStats) -> bool {
        self.prepared_for_static_view(stats)
            && self.registered_colliders == stats.configured_collision_nodes
    }

    /// Prepared scene and collider shapes, without requiring a physics tick.
    /// Only for frozen diagnostic views; this is neither playtest nor GPU readiness.
    #[must_use]
    pub fn prepared_for_static_view(self, stats: &GovernedAssetKitRuntimeStats) -> bool {
        stats.requested_scene_instances > 0
            && self.ready_instances == stats.requested_scene_instances
            && stats.all_scene_dependencies_loaded
            && stats.failed_scene_instances == 0
            && stats.configured_lod0_nodes > 0
            && stats.configured_lod1_nodes > 0
            && stats.configured_lod2_nodes > 0
            && stats.configured_collision_nodes > 0
            && stats.failed_collision_nodes == 0
            && stats.ready_rapier_colliders == stats.configured_collision_nodes
    }
}

/// Read-only Bevy/Rapier projection of a validated external Blender asset-kit candidate.
///
/// The build report, preview placements, `WorldSketch` and terrain remain the sources of truth. GLB
/// entities, LOD ranges and colliders are reconstructible runtime caches.
pub struct VisualValleyGovernedAssetKitPlugin {
    plan: GovernedAssetKitRuntimePlan,
}

impl VisualValleyGovernedAssetKitPlugin {
    pub fn new(
        terrain: &TerrainDocument,
        world_sketch: &WorldSketchDocument,
        report: GovernedAssetKitBuildReport,
        preview: GovernedAssetKitRuntimePreview,
    ) -> Result<Self, GovernedAssetKitAdapterError> {
        report.validate()?;
        preview.validate_against_build_report(&report)?;
        world_sketch.validate()?;
        terrain.validate()?;

        let world_sketch_hash = world_sketch.content_hash()?;
        if world_sketch_hash != preview.source_world_sketch_sha256 {
            return Err(GovernedAssetKitAdapterError::WorldSketchHashMismatch {
                expected: preview.source_world_sketch_sha256,
                actual: world_sketch_hash,
            });
        }
        let terrain_hash = terrain.content_hash()?;
        if terrain_hash != preview.source_terrain_sha256 {
            return Err(GovernedAssetKitAdapterError::TerrainHashMismatch {
                expected: preview.source_terrain_sha256,
                actual: terrain_hash,
            });
        }
        let camera = world_sketch
            .validation_cameras
            .iter()
            .find(|camera| camera.id == preview.camera_id)
            .cloned()
            .ok_or_else(|| {
                GovernedAssetKitAdapterError::MissingValidationCamera(preview.camera_id.clone())
            })?;

        let terrain_plan = TerrainGenerationPlan::from_document(terrain)?;
        let asset_paths = report
            .assets
            .iter()
            .map(|asset| (asset.id.as_str(), asset.glb.path.as_str()))
            .collect::<HashMap<_, _>>();
        let mut placements = Vec::with_capacity(preview.placements.len());
        for placement in &preview.placements {
            let surface = match terrain_plan.surface_at(placement.xz_m[0], placement.xz_m[1]) {
                Ok(surface) => surface,
                Err(TerrainGenerationError::SurfaceCoordinateOutOfBounds) => {
                    return Err(GovernedAssetKitAdapterError::PlacementOutsideTerrain(
                        placement.id.clone(),
                    ));
                }
                Err(error) => return Err(error.into()),
            };
            let asset_path = asset_paths
                .get(placement.asset_id.as_str())
                .copied()
                .ok_or_else(|| {
                    GovernedAssetKitAdapterError::MissingBuildAsset(placement.asset_id.clone())
                })?;
            placements.push(PreparedGovernedAssetKitPlacement {
                placement_id: placement.id.clone(),
                asset_id: placement.asset_id.clone(),
                asset_path: asset_path.to_owned(),
                translation: Vec3::new(
                    placement.xz_m[0],
                    surface.height_m + placement.surface_offset_m,
                    placement.xz_m[1],
                ),
                yaw_radians: placement.yaw_degrees.to_radians(),
                scale: placement.scale,
            });
        }

        Ok(Self {
            plan: GovernedAssetKitRuntimePlan {
                report,
                preview,
                camera,
                spawn_validation_camera: true,
                placements,
            },
        })
    }

    /// Enables the fixed `WorldSketch` camera for a dedicated asset-kit comparison.
    ///
    /// Disable it when the Visual Valley player or another plugin already owns the active camera.
    #[must_use]
    pub const fn with_validation_camera(mut self, enabled: bool) -> Self {
        self.plan.spawn_validation_camera = enabled;
        self
    }

    /// Routes validated relative paths to a caller-installed immutable asset source.
    /// Document paths and hashes remain unchanged.
    #[must_use]
    pub fn with_memory_asset_source(mut self) -> Self {
        for placement in &mut self.plan.placements {
            placement.asset_path = format!("forge-kit://{}", placement.asset_path);
        }
        self
    }
}

impl Plugin for VisualValleyGovernedAssetKitPlugin {
    fn build(&self, app: &mut App) {
        temporal::install(app);
        let preview = &self.plan.preview;
        app.insert_resource(GovernedAssetKitRuntimeStats {
            asset_kit_id: self.plan.report.asset_kit_id.clone(),
            asset_kit_revision: self.plan.report.asset_kit_revision,
            input_manifest_sha256: preview.input_manifest_sha256.to_string(),
            source_world_sketch_sha256: preview.source_world_sketch_sha256.to_string(),
            source_terrain_sha256: preview.source_terrain_sha256.to_string(),
            camera_id: preview.camera_id.clone(),
            validation_camera_spawned: self.plan.spawn_validation_camera,
            declared_assets: preview.assets.len(),
            requested_scene_instances: preview.placements.len(),
            ..default()
        })
        .insert_resource(self.plan.clone())
        .insert_resource(GovernedAssetKitSceneHandles(Vec::with_capacity(
            preview.placements.len(),
        )))
        .init_resource::<GovernedAssetKitLoadLogged>()
        .init_resource::<GovernedAssetKitReadiness>()
        .add_systems(Startup, spawn_governed_asset_kit_preview)
        .add_systems(
            Update,
            (
                configure_governed_asset_kit_nodes,
                configure_governed_asset_kit_primitives,
                materialize_governed_asset_kit_colliders,
                observe_governed_asset_kit_nodes,
                observe_governed_asset_kit_loads,
                observe_governed_asset_kit_readiness,
            )
                .chain(),
        );
        #[cfg(feature = "qualification")]
        app.add_systems(Last, physics_probe::observe);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn spawn_governed_asset_kit_preview(
    mut commands: Commands,
    plan: Res<GovernedAssetKitRuntimePlan>,
    asset_server: Res<AssetServer>,
    mut handles: ResMut<GovernedAssetKitSceneHandles>,
) {
    for placement in &plan.placements {
        let scene =
            asset_server.load(GltfAssetLabel::Scene(0).from_asset(placement.asset_path.clone()));
        commands.spawn((
            Name::new(format!(
                "Governed asset {} · {}",
                placement.asset_id, placement.placement_id
            )),
            GovernedAssetKitPlacementMarker {
                placement_id: placement.placement_id.clone(),
                asset_id: placement.asset_id.clone(),
            },
            WorldAssetRoot(scene.clone()),
            Transform::from_translation(placement.translation)
                .with_rotation(Quat::from_rotation_y(placement.yaw_radians))
                .with_scale(Vec3::splat(placement.scale)),
        ));
        handles.0.push(scene);
    }

    if !plan.spawn_validation_camera {
        return;
    }
    let camera = &plan.camera;
    let position = array_to_vec3(camera.position_m);
    let target = array_to_vec3(camera.target_m);
    commands.spawn((
        Name::new(format!("WorldSketch validation camera · {}", camera.id)),
        Camera3d::default(),
        AmbientLight {
            color: Color::srgb(0.56, 0.66, 0.78),
            brightness: 105.0,
            ..default()
        },
        Projection::Perspective(PerspectiveProjection {
            fov: camera.vertical_fov_degrees.to_radians(),
            near: camera.near_m,
            far: camera.far_m,
            ..default()
        }),
        Transform::from_translation(position).looking_at(target, Vec3::Y),
    ));
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn configure_governed_asset_kit_nodes(
    mut commands: Commands,
    plan: Res<GovernedAssetKitRuntimePlan>,
    nodes: Query<(Entity, &Name), UnconfiguredGovernedAssetNode>,
    parents: Query<&ChildOf>,
    placement_roots: Query<(), With<GovernedAssetKitPlacementMarker>>,
) {
    for (entity, name) in &nodes {
        if !has_marked_ancestor(entity, &parents, &placement_roots) {
            continue;
        }
        let Some(role) = classify_governed_asset_node(
            name.as_str(),
            plan.report.assets.iter().map(|asset| asset.id.as_str()),
        ) else {
            continue;
        };
        let mut entity_commands = commands.entity(entity);
        match role {
            GovernedAssetNodeRole::Render(GovernedAssetKitRenderLod::Lod0) => {
                entity_commands.insert((
                    GovernedAssetKitRenderLod::Lod0,
                    governed_asset_lod_range(GovernedAssetKitRenderLod::Lod0),
                ));
            }
            GovernedAssetNodeRole::Render(GovernedAssetKitRenderLod::Lod1) => {
                entity_commands.insert((
                    GovernedAssetKitRenderLod::Lod1,
                    governed_asset_lod_range(GovernedAssetKitRenderLod::Lod1),
                ));
            }
            GovernedAssetNodeRole::Render(GovernedAssetKitRenderLod::Lod2) => {
                entity_commands.insert((
                    GovernedAssetKitRenderLod::Lod2,
                    governed_asset_lod_range(GovernedAssetKitRenderLod::Lod2),
                ));
            }
            GovernedAssetNodeRole::Collision => {
                entity_commands.insert((GovernedAssetKitCollisionNode, Visibility::Hidden));
            }
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn configure_governed_asset_kit_primitives(
    mut commands: Commands,
    primitives: Query<Entity, Added<Mesh3d>>,
    parents: Query<&ChildOf>,
    render_nodes: Query<&GovernedAssetKitRenderLod>,
    collision_nodes: Query<(), With<GovernedAssetKitCollisionNode>>,
) {
    for entity in &primitives {
        if let Some(lod) = governed_render_lod_ancestor(entity, &parents, &render_nodes) {
            // Bevy does not inherit VisibilityRange from GLTF nodes. The render
            // primitive itself must carry the range for camera and shadow views.
            commands
                .entity(entity)
                .insert(governed_asset_lod_range(lod));
            if matches!(
                lod,
                GovernedAssetKitRenderLod::Lod1 | GovernedAssetKitRenderLod::Lod2
            ) {
                commands.entity(entity).insert(NotShadowCaster);
            }
        } else if has_marked_ancestor(entity, &parents, &collision_nodes) {
            commands
                .entity(entity)
                .insert(GovernedAssetKitCollisionPrimitive);
        }
    }
}

fn governed_render_lod_ancestor(
    entity: Entity,
    parents: &Query<&ChildOf>,
    render_nodes: &Query<&GovernedAssetKitRenderLod>,
) -> Option<GovernedAssetKitRenderLod> {
    let mut ancestor = entity;
    for _ in 0..MAX_GOVERNED_ASSET_HIERARCHY_DEPTH {
        let Ok(parent) = parents.get(ancestor) else {
            return None;
        };
        ancestor = parent.parent();
        if let Ok(lod) = render_nodes.get(ancestor) {
            return Some(*lod);
        }
    }
    None
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn materialize_governed_asset_kit_colliders(
    mut commands: Commands,
    meshes: Res<Assets<Mesh>>,
    collision_nodes: Query<(Entity, &Mesh3d, &Name), PendingGovernedAssetCollider>,
) {
    for (entity, mesh_handle, name) in &collision_nodes {
        let Some(mesh) = meshes.get(mesh_handle) else {
            continue;
        };
        match crate::physics::convex_collider(mesh) {
            Ok(collider) => {
                commands.entity(entity).insert(collider);
            }
            Err(reason) => mark_governed_asset_collider_failed(&mut commands, entity, name, reason),
        }
    }
}

fn mark_governed_asset_collider_failed(
    commands: &mut Commands,
    entity: Entity,
    name: &Name,
    reason: &'static str,
) {
    commands
        .entity(entity)
        .insert(GovernedAssetKitCollisionFailed);
    error!(
        target: "forge::assets",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "governed_asset_kit_collider_failed",
        node = name.as_str(),
        reason,
        "governed asset-kit collision mesh could not produce a Rapier convex hull"
    );
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn observe_governed_asset_kit_nodes(
    render_nodes: Query<&GovernedAssetKitRenderLod>,
    collision_node_roots: Query<(), With<GovernedAssetKitCollisionNode>>,
    collision_nodes: Query<
        (Option<&Collider>, Option<&GovernedAssetKitCollisionFailed>),
        With<GovernedAssetKitCollisionPrimitive>,
    >,
    mut stats: ResMut<GovernedAssetKitRuntimeStats>,
) {
    stats.configured_lod0_nodes = 0;
    stats.configured_lod1_nodes = 0;
    stats.configured_lod2_nodes = 0;
    for lod in &render_nodes {
        match lod {
            GovernedAssetKitRenderLod::Lod0 => stats.configured_lod0_nodes += 1,
            GovernedAssetKitRenderLod::Lod1 => stats.configured_lod1_nodes += 1,
            GovernedAssetKitRenderLod::Lod2 => stats.configured_lod2_nodes += 1,
        }
    }
    stats.configured_collision_nodes = collision_node_roots.iter().count();
    stats.ready_rapier_colliders = 0;
    stats.failed_collision_nodes = 0;
    for (collider, failed) in &collision_nodes {
        stats.ready_rapier_colliders += usize::from(collider.is_some());
        stats.failed_collision_nodes += usize::from(failed.is_some());
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn observe_governed_asset_kit_loads(
    asset_server: Res<AssetServer>,
    handles: Res<GovernedAssetKitSceneHandles>,
    mut stats: ResMut<GovernedAssetKitRuntimeStats>,
    mut logged: ResMut<GovernedAssetKitLoadLogged>,
) {
    let mut loaded = 0;
    let mut failed = 0;
    for handle in &handles.0 {
        if asset_server.is_loaded_with_dependencies(handle.id()) {
            loaded += 1;
        } else if matches!(asset_server.load_state(handle.id()), LoadState::Failed(_)) {
            failed += 1;
        }
    }
    stats.loaded_scene_instances = loaded;
    stats.failed_scene_instances = failed;
    stats.all_scene_dependencies_loaded = loaded == handles.0.len() && failed == 0;

    if failed > 0 && !logged.failed {
        logged.failed = true;
        error!(
            target: "forge::assets",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "governed_asset_kit_load_failed",
            failed_scene_instances = failed,
            requested_scene_instances = handles.0.len(),
            "one or more governed asset-kit GLB scenes failed to load"
        );
    }
    if stats.all_scene_dependencies_loaded && !logged.ready {
        logged.ready = true;
        info!(
            target: "forge::assets",
            schema = "forge.runtime-event",
            schema_version = 1_u64,
            event_kind = "governed_asset_kit_loaded",
            asset_kit_id = %stats.asset_kit_id,
            asset_kit_revision = stats.asset_kit_revision,
            declared_assets = stats.declared_assets,
            requested_scene_instances = stats.requested_scene_instances,
            configured_lod0_nodes = stats.configured_lod0_nodes,
            configured_lod1_nodes = stats.configured_lod1_nodes,
            configured_lod2_nodes = stats.configured_lod2_nodes,
            configured_collision_nodes = stats.configured_collision_nodes,
            "governed Blender asset-kit candidate loaded from its external proof cache"
        );
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn observe_governed_asset_kit_readiness(
    instances: Query<&WorldInstance, With<GovernedAssetKitPlacementMarker>>,
    spawner: Res<WorldInstanceSpawner>,
    colliders: Query<
        (),
        (
            With<GovernedAssetKitCollisionPrimitive>,
            With<RapierColliderHandle>,
        ),
    >,
    stats: Res<GovernedAssetKitRuntimeStats>,
    mut readiness: ResMut<GovernedAssetKitReadiness>,
) {
    let was_ready = readiness.ready;
    readiness.ready_instances = instances
        .iter()
        .filter(|instance| spawner.instance_is_ready(***instance))
        .count();
    readiness.registered_colliders = colliders.iter().count();
    readiness.ready = readiness.matches_complete_projection(&stats);
    if readiness.ready && !was_ready {
        info!(
            target: "forge::assets",
            schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "governed_asset_kit_playtest_ready",
            ready_instances = readiness.ready_instances,
            registered_colliders = readiness.registered_colliders,
            "governed kit instances and physics are ready for playtest"
        );
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum GovernedAssetNodeRole {
    Render(GovernedAssetKitRenderLod),
    Collision,
}

fn has_marked_ancestor<Marker: Component>(
    entity: Entity,
    parents: &Query<&ChildOf>,
    marked_roots: &Query<(), With<Marker>>,
) -> bool {
    let mut ancestor = entity;
    for _ in 0..MAX_GOVERNED_ASSET_HIERARCHY_DEPTH {
        let Ok(parent) = parents.get(ancestor) else {
            return false;
        };
        ancestor = parent.parent();
        if marked_roots.contains(ancestor) {
            return true;
        }
    }
    false
}

fn classify_governed_asset_node<'a>(
    name: &str,
    asset_ids: impl Iterator<Item = &'a str>,
) -> Option<GovernedAssetNodeRole> {
    for asset_id in asset_ids {
        if let Some(suffix) = name.strip_prefix(asset_id) {
            if valid_node_role_suffix(suffix, "_LOD0") {
                return Some(GovernedAssetNodeRole::Render(
                    GovernedAssetKitRenderLod::Lod0,
                ));
            }
            if valid_node_role_suffix(suffix, "_LOD1") {
                return Some(GovernedAssetNodeRole::Render(
                    GovernedAssetKitRenderLod::Lod1,
                ));
            }
            if valid_node_role_suffix(suffix, "_LOD2") {
                return Some(GovernedAssetNodeRole::Render(
                    GovernedAssetKitRenderLod::Lod2,
                ));
            }
        }
        if name
            .strip_prefix("UCX_")
            .and_then(|suffix| suffix.strip_prefix(asset_id))
            .is_some_and(|suffix| suffix.starts_with('_'))
        {
            return Some(GovernedAssetNodeRole::Collision);
        }
    }
    None
}

fn valid_node_role_suffix(suffix: &str, role: &str) -> bool {
    suffix
        .strip_prefix(role)
        .is_some_and(|remainder| remainder.is_empty() || remainder.starts_with('_'))
}

fn governed_asset_lod_range(lod: GovernedAssetKitRenderLod) -> VisibilityRange {
    match lod {
        GovernedAssetKitRenderLod::Lod0 => VisibilityRange {
            start_margin: 0.0..0.0,
            end_margin: NEAR_TO_MEDIUM_LOD_M,
            use_aabb: false,
        },
        GovernedAssetKitRenderLod::Lod1 => VisibilityRange {
            start_margin: NEAR_TO_MEDIUM_LOD_M,
            end_margin: MEDIUM_TO_FAR_LOD_M,
            use_aabb: false,
        },
        GovernedAssetKitRenderLod::Lod2 => VisibilityRange {
            start_margin: MEDIUM_TO_FAR_LOD_M,
            end_margin: GOVERNED_ASSET_CULL_DISTANCE_M..GOVERNED_ASSET_CULL_DISTANCE_M,
            use_aabb: false,
        },
    }
}

const fn array_to_vec3(value: [f32; 3]) -> Vec3 {
    Vec3::new(value[0], value[1], value[2])
}

#[derive(Debug)]
pub enum GovernedAssetKitAdapterError {
    InvalidContract(GovernedAssetKitError),
    InvalidTerrain(TerrainDocumentError),
    InvalidWorldSketch(WorldSketchError),
    TerrainGeneration(TerrainGenerationError),
    WorldSketchHashMismatch {
        expected: ContentHash,
        actual: ContentHash,
    },
    TerrainHashMismatch {
        expected: ContentHash,
        actual: ContentHash,
    },
    MissingValidationCamera(String),
    MissingBuildAsset(String),
    PlacementOutsideTerrain(String),
}

impl fmt::Display for GovernedAssetKitAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidContract(error) => {
                write!(formatter, "invalid governed asset kit: {error}")
            }
            Self::InvalidTerrain(error) => write!(formatter, "invalid preview terrain: {error}"),
            Self::InvalidWorldSketch(error) => {
                write!(formatter, "invalid preview WorldSketch: {error}")
            }
            Self::TerrainGeneration(error) => {
                write!(formatter, "cannot sample preview terrain: {error}")
            }
            Self::WorldSketchHashMismatch { expected, actual } => write!(
                formatter,
                "asset-kit preview requires WorldSketch {expected}, loaded WorldSketch is {actual}"
            ),
            Self::TerrainHashMismatch { expected, actual } => write!(
                formatter,
                "asset-kit preview requires terrain {expected}, loaded terrain is {actual}"
            ),
            Self::MissingValidationCamera(camera_id) => write!(
                formatter,
                "asset-kit preview references missing WorldSketch camera `{camera_id}`"
            ),
            Self::MissingBuildAsset(asset_id) => write!(
                formatter,
                "asset-kit preview references missing Blender build asset `{asset_id}`"
            ),
            Self::PlacementOutsideTerrain(placement_id) => write!(
                formatter,
                "asset-kit placement `{placement_id}` is outside the linked terrain"
            ),
        }
    }
}

impl Error for GovernedAssetKitAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::InvalidContract(error) => Some(error),
            Self::InvalidTerrain(error) => Some(error),
            Self::InvalidWorldSketch(error) => Some(error),
            Self::TerrainGeneration(error) => Some(error),
            Self::WorldSketchHashMismatch { .. }
            | Self::TerrainHashMismatch { .. }
            | Self::MissingValidationCamera(_)
            | Self::MissingBuildAsset(_)
            | Self::PlacementOutsideTerrain(_) => None,
        }
    }
}

impl From<GovernedAssetKitError> for GovernedAssetKitAdapterError {
    fn from(error: GovernedAssetKitError) -> Self {
        Self::InvalidContract(error)
    }
}

impl From<TerrainDocumentError> for GovernedAssetKitAdapterError {
    fn from(error: TerrainDocumentError) -> Self {
        Self::InvalidTerrain(error)
    }
}

impl From<WorldSketchError> for GovernedAssetKitAdapterError {
    fn from(error: WorldSketchError) -> Self {
        Self::InvalidWorldSketch(error)
    }
}

impl From<TerrainGenerationError> for GovernedAssetKitAdapterError {
    fn from(error: TerrainGenerationError) -> Self {
        Self::TerrainGeneration(error)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_world::{compile_world_sketch, embedded_visual_valley_world_sketch};
    use serde_json::json;

    #[test]
    fn gltf_primitives_receive_their_parent_lod_range_and_shadow_policy() {
        let mut app = App::new();
        app.add_systems(Update, configure_governed_asset_kit_primitives);
        for lod in [
            GovernedAssetKitRenderLod::Lod0,
            GovernedAssetKitRenderLod::Lod1,
            GovernedAssetKitRenderLod::Lod2,
        ] {
            let node = app
                .world_mut()
                .spawn((lod, governed_asset_lod_range(lod)))
                .id();
            let primitive = app
                .world_mut()
                .spawn((Mesh3d::default(), ChildOf(node)))
                .id();
            app.update();
            assert!(
                app.world().get::<VisibilityRange>(primitive)
                    == Some(&governed_asset_lod_range(lod))
            );
            assert_eq!(
                app.world().get::<NotShadowCaster>(primitive).is_some(),
                lod != GovernedAssetKitRenderLod::Lod0
            );
        }
        let unrelated = app.world_mut().spawn(Mesh3d::default()).id();
        app.update();
        assert!(app.world().get::<VisibilityRange>(unrelated).is_none());
    }

    pub(super) fn collision_test_mesh(positions: Vec<[f32; 3]>) -> Mesh {
        Mesh::new(
            bevy::mesh::PrimitiveTopology::TriangleList,
            bevy::asset::RenderAssetUsages::MAIN_WORLD,
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
    }

    #[test]
    fn collider_waits_for_mesh_then_materializes_without_rewriting_source() {
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .add_systems(Update, materialize_governed_asset_kit_colliders);
        let handle = app.world().resource::<Assets<Mesh>>().reserve_handle();
        let entity = app
            .world_mut()
            .spawn((
                Name::new("synthetic_collision"),
                Mesh3d(handle.clone()),
                GovernedAssetKitCollisionPrimitive,
            ))
            .id();
        app.update();
        assert!(app.world().get::<Collider>(entity).is_none());
        assert!(
            app.world()
                .get::<GovernedAssetKitCollisionFailed>(entity)
                .is_none()
        );
        let points = vec![
            [0.0, 0.0, 0.0],
            [2.0, 0.0, 0.0],
            [0.0, 3.0, 0.0],
            [0.0, 0.0, 4.0],
        ];
        app.world_mut()
            .resource_mut::<Assets<Mesh>>()
            .insert(handle.id(), collision_test_mesh(points.clone()))
            .unwrap();
        app.update();
        assert!(app.world().get::<Collider>(entity).is_some());
        assert!(
            app.world()
                .get::<GovernedAssetKitCollisionFailed>(entity)
                .is_none()
        );
        app.update();
        let assets = app.world().resource::<Assets<Mesh>>();
        let VertexAttributeValues::Float32x3(observed) = assets
            .get(&handle)
            .unwrap()
            .attribute(Mesh::ATTRIBUTE_POSITION)
            .unwrap()
        else {
            panic!()
        };
        assert_eq!(observed, &points);
        assert_eq!(
            app.world_mut()
                .query::<&Collider>()
                .iter(app.world())
                .count(),
            1
        );
    }

    #[test]
    fn invalid_collision_meshes_fail_closed_and_do_not_silently_retry() {
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .add_systems(Update, materialize_governed_asset_kit_colliders);
        let cases = [
            vec![[0.0, 0.0, 0.0]; 3],
            vec![[f32::NAN, 0.0, 0.0]; 4],
            vec![[0.0, 0.0, 0.0]; 4],
        ];
        for points in cases {
            let handle = app
                .world_mut()
                .resource_mut::<Assets<Mesh>>()
                .add(collision_test_mesh(points));
            let entity = app
                .world_mut()
                .spawn((
                    Name::new("invalid_collision"),
                    Mesh3d(handle.clone()),
                    GovernedAssetKitCollisionPrimitive,
                ))
                .id();
            app.update();
            assert!(app.world().get::<Collider>(entity).is_none());
            assert!(
                app.world()
                    .get::<GovernedAssetKitCollisionFailed>(entity)
                    .is_some()
            );
            app.world_mut()
                .resource_mut::<Assets<Mesh>>()
                .insert(
                    handle.id(),
                    collision_test_mesh(vec![
                        [0.0, 0.0, 0.0],
                        [2.0, 0.0, 0.0],
                        [0.0, 3.0, 0.0],
                        [0.0, 0.0, 4.0],
                    ]),
                )
                .unwrap();
            app.update();
            assert!(app.world().get::<Collider>(entity).is_none());
            assert!(
                app.world()
                    .get::<GovernedAssetKitCollisionFailed>(entity)
                    .is_some()
            );
        }
    }

    fn candidate_json(
        world_sketch_hash: ContentHash,
        terrain_hash: ContentHash,
    ) -> (String, String) {
        let manifest_hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        let glb_hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        let report = json!({
            "schema": "forge.blender-asset-kit-build",
            "schema_version": 1,
            "asset_kit_id": "test-kit",
            "asset_kit_revision": 1,
            "generator": { "input_manifest_sha256": manifest_hash },
            "provenance": {
                "source_kind": "procedural_original",
                "license": "LicenseRef-OpenRPG-Forge-Project-Generated",
                "cadle_content_used": false,
                "commercial_game_content_used": false
            },
            "qualification": { "technical_export_validated": true },
            "assets": [{
                "id": "test-tree",
                "kind": "tree",
                "glb": { "path": "assets/test-tree.glb", "sha256": glb_hash }
            }]
        });
        let preview = json!({
            "schema": "forge.asset-kit-runtime-preview",
            "schema_version": 1,
            "asset_kit_id": "test-kit",
            "asset_kit_revision": 1,
            "source_world_sketch_sha256": world_sketch_hash,
            "source_terrain_sha256": terrain_hash,
            "input_manifest_sha256": manifest_hash,
            "camera_id": "hero_reference_match",
            "assets": ["test-tree"],
            "placements": [{
                "id": "tree-one",
                "asset_id": "test-tree",
                "xz_m": [0.0, 0.0],
                "surface_offset_m": 0.0,
                "yaw_degrees": 0.0,
                "scale": 1.0
            }]
        });
        (report.to_string(), preview.to_string())
    }

    #[test]
    fn adapter_binds_report_preview_world_sketch_terrain_and_camera() {
        let world_sketch = embedded_visual_valley_world_sketch().unwrap();
        let compiled = compile_world_sketch(&world_sketch).unwrap();
        let (report_json, preview_json) = candidate_json(
            world_sketch.content_hash().unwrap(),
            compiled.carved_terrain.content_hash().unwrap(),
        );
        let report = GovernedAssetKitBuildReport::from_json(&report_json).unwrap();
        let preview = GovernedAssetKitRuntimePreview::from_json(&preview_json).unwrap();

        let plugin = VisualValleyGovernedAssetKitPlugin::new(
            &compiled.carved_terrain,
            &world_sketch,
            report,
            preview,
        )
        .unwrap();
        assert_eq!(plugin.plan.camera.id, "hero_reference_match");
        assert_eq!(plugin.plan.preview.placements.len(), 1);
        assert!(plugin.plan.spawn_validation_camera);
        let source_report = plugin.plan.report.clone();
        let source_preview = plugin.plan.preview.clone();
        let original_path = plugin.plan.placements[0].asset_path.clone();
        let plugin = plugin.with_memory_asset_source();
        assert_eq!(plugin.plan.report, source_report);
        assert_eq!(plugin.plan.preview, source_preview);
        assert_eq!(
            plugin.plan.placements[0].asset_path,
            format!("forge-kit://{original_path}")
        );
        assert!(
            !plugin
                .with_validation_camera(false)
                .plan
                .spawn_validation_camera
        );
    }

    #[test]
    fn playtest_waits_for_instances_and_backend_colliders_not_just_loaded_files() {
        let mut stats = GovernedAssetKitRuntimeStats {
            requested_scene_instances: 20,
            all_scene_dependencies_loaded: true,
            configured_lod0_nodes: 28,
            configured_lod1_nodes: 28,
            configured_lod2_nodes: 28,
            configured_collision_nodes: 36,
            ready_rapier_colliders: 36,
            ..default()
        };
        let mut readiness = GovernedAssetKitReadiness {
            ready_instances: 19,
            registered_colliders: 36,
            ..default()
        };
        assert!(!readiness.matches_complete_projection(&stats));
        readiness.ready_instances = 20;
        readiness.registered_colliders = 0;
        assert!(readiness.prepared_for_static_view(&stats));
        assert!(!readiness.matches_complete_projection(&stats));
        stats.ready_rapier_colliders = 35;
        assert!(!readiness.prepared_for_static_view(&stats));
        stats.ready_rapier_colliders = 36;
        readiness.registered_colliders = 35;
        assert!(!readiness.matches_complete_projection(&stats));
        readiness.registered_colliders = 36;
        assert!(readiness.matches_complete_projection(&stats));
        stats.failed_collision_nodes = 1;
        assert!(!readiness.prepared_for_static_view(&stats));
        assert!(!readiness.matches_complete_projection(&stats));
        assert!(
            !GovernedAssetKitReadiness::default()
                .matches_complete_projection(&GovernedAssetKitRuntimeStats::default())
        );
    }

    #[test]
    fn adapter_rejects_a_different_terrain_or_camera() {
        let world_sketch = embedded_visual_valley_world_sketch().unwrap();
        let compiled = compile_world_sketch(&world_sketch).unwrap();
        let (report_json, preview_json) = candidate_json(
            world_sketch.content_hash().unwrap(),
            compiled.carved_terrain.content_hash().unwrap(),
        );
        let report = GovernedAssetKitBuildReport::from_json(&report_json).unwrap();
        let preview = GovernedAssetKitRuntimePreview::from_json(&preview_json).unwrap();
        assert!(matches!(
            VisualValleyGovernedAssetKitPlugin::new(
                &compiled.terrain,
                &world_sketch,
                report.clone(),
                preview.clone()
            ),
            Err(GovernedAssetKitAdapterError::TerrainHashMismatch { .. })
        ));

        let mut outside_terrain = preview.clone();
        outside_terrain.placements[0].xz_m = [300.0, 0.0];
        assert!(matches!(
            VisualValleyGovernedAssetKitPlugin::new(
                &compiled.carved_terrain,
                &world_sketch,
                report.clone(),
                outside_terrain
            ),
            Err(GovernedAssetKitAdapterError::PlacementOutsideTerrain(id)) if id == "tree-one"
        ));

        let mut missing_camera = preview;
        missing_camera.camera_id = "missing-camera".to_owned();
        assert!(matches!(
            VisualValleyGovernedAssetKitPlugin::new(
                &compiled.carved_terrain,
                &world_sketch,
                report,
                missing_camera
            ),
            Err(GovernedAssetKitAdapterError::MissingValidationCamera(_))
        ));
    }

    #[test]
    fn glb_node_names_select_only_known_assets_and_lod_or_collision_roles() {
        let ids = ["vv-tree-oak-a", "vv-rock-moss-a"];
        assert_eq!(
            classify_governed_asset_node("vv-tree-oak-a_LOD1_Foliage", ids.iter().copied()),
            Some(GovernedAssetNodeRole::Render(
                GovernedAssetKitRenderLod::Lod1
            ))
        );
        assert_eq!(
            classify_governed_asset_node("UCX_vv-rock-moss-a_01", ids.iter().copied()),
            Some(GovernedAssetNodeRole::Collision)
        );
        assert_eq!(
            classify_governed_asset_node("other_LOD0", ids.iter().copied()),
            None
        );
        assert_eq!(
            classify_governed_asset_node("prefix-vv-tree-oak-a_LOD0", ids.iter().copied()),
            None
        );
        assert_eq!(
            classify_governed_asset_node("vv-tree-oak-a_LOD01", ids.iter().copied()),
            None
        );
        assert_eq!(
            classify_governed_asset_node("UCX_vv-tree-oak-ab_Trunk", ids.iter().copied()),
            None
        );
    }

    #[test]
    fn lod_ranges_are_complementary_and_end_at_the_bounded_cull_distance() {
        let lod0 = governed_asset_lod_range(GovernedAssetKitRenderLod::Lod0);
        let lod1 = governed_asset_lod_range(GovernedAssetKitRenderLod::Lod1);
        let lod2 = governed_asset_lod_range(GovernedAssetKitRenderLod::Lod2);
        assert_eq!(lod0.end_margin, lod1.start_margin);
        assert_eq!(lod1.end_margin, lod2.start_margin);
        assert_eq!(
            lod2.end_margin,
            GOVERNED_ASSET_CULL_DISTANCE_M..GOVERNED_ASSET_CULL_DISTANCE_M
        );
    }
}
