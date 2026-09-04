//! Project terrain projection. One renderer, latest request wins, no canonical ECS data.
use crate::vegetation::{
    MAX_EDITOR_VEGETATION_VERTICES, MAX_EDITOR_VEGETATION_VERTICES_PER_CHUNK,
    build_editor_vegetation_chunk_mesh, editor_vegetation_vertex_count,
};
use crate::visual_valley::{
    VisualValleyTerrainMaterial, build_bevy_terrain_mesh, procedural_terrain_material,
};
use bevy::{asset::embedded_asset, prelude::*};
use forge_core::{AreaId, ContentHash, ProjectId};
use forge_project::VegetationKind;
use forge_world::{
    GeneratedPaths, GeneratedTerrain, GeneratedVegetation, TERRAIN_CHUNK_COUNT,
    TERRAIN_CHUNKS_PER_AXIS, TERRAIN_EXTENT_M, TERRAIN_HALF_EXTENT_M, TerrainChunkCoordinate,
    TerrainChunkMesh, VegetationChunk, VegetationInstance, VegetationKindCounts,
};
use serde::Serialize;
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};

#[path = "editor_brush.rs"]
mod brush;
pub use brush::{EditorBrushHit, EditorBrushPointer};

#[path = "editor_path.rs"]
mod path;
pub use path::EditorPathDraft;

pub struct EditorTerrainRequest {
    pub generation: u64,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: u64,
    pub terrain: GeneratedTerrain,
    /// Complete, reconstructible vegetation output for the same accepted area sources.
    /// Historical terrain-only callers may omit it; the Studio always supplies it.
    pub vegetation: Option<EditorVegetationRequest>,
    /// Complete generated authoring paths for the accepted terrain source.
    pub paths: Option<EditorPathRequest>,
    pub spawn: [f32; 3],
    /// A partial payload is valid only against the currently presented source hash.
    pub base_source_hash: Option<ContentHash>,
}

pub struct EditorVegetationRequest {
    pub generated: GeneratedVegetation,
    pub density_source_hash: ContentHash,
}

pub struct EditorPathRequest {
    pub generated: GeneratedPaths,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct EditorCameraPose {
    pub yaw: f32,
    pub pitch: f32,
    pub distance: f32,
    pub target: [f32; 3],
}
impl Default for EditorCameraPose {
    fn default() -> Self {
        Self {
            yaw: -std::f32::consts::FRAC_PI_4,
            pitch: 0.64,
            distance: 480.0,
            target: [0.0; 3],
        }
    }
}
impl EditorCameraPose {
    fn apply(&mut self, input: EditorCameraInput) {
        if input.reset {
            *self = Self::default();
        }
        if let Some(target) = input
            .focus
            .filter(|target| target.iter().all(|v| v.is_finite()))
        {
            self.target = target;
            self.distance = 70.0;
        }
        if input
            .orbit
            .iter()
            .chain(input.pan.iter())
            .chain([&input.zoom])
            .any(|v| !v.is_finite())
        {
            return;
        }
        self.yaw = (self.yaw - input.orbit[0].clamp(-1000.0, 1000.0) * 0.006)
            .rem_euclid(std::f32::consts::TAU);
        self.pitch = (self.pitch + input.orbit[1].clamp(-1000.0, 1000.0) * 0.006).clamp(0.05, 1.52);
        self.distance =
            (self.distance * (input.zoom.clamp(-1000.0, 1000.0) * 0.001).exp()).clamp(12.0, 1100.0);
        let scale = self.distance * 0.0015;
        self.target[0] = (self.target[0]
            - (input.pan[0] * self.yaw.cos() + input.pan[1] * self.yaw.sin()) * scale)
            .clamp(-512.0, 512.0);
        self.target[2] = (self.target[2]
            + (input.pan[0] * self.yaw.sin() - input.pan[1] * self.yaw.cos()) * scale)
            .clamp(-512.0, 512.0);
    }
    fn transform(self) -> Transform {
        let target = Vec3::from_array(self.target);
        let offset = Vec3::new(
            self.yaw.sin() * self.pitch.cos(),
            self.pitch.sin(),
            self.yaw.cos() * self.pitch.cos(),
        ) * self.distance;
        Transform::from_translation(target + offset).looking_at(target, Vec3::Y)
    }
}

#[derive(Clone, Copy, Default)]
pub struct EditorCameraInput {
    pub orbit: [f32; 2],
    pub pan: [f32; 2],
    pub zoom: f32,
    pub reset: bool,
    pub focus: Option<[f32; 3]>,
}

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
pub struct EditorViewportStatus {
    pub generation: u64,
    pub area_id: Option<AreaId>,
    pub revision: u64,
    pub source_hash: String,
    pub chunks: usize,
    pub frames: u64,
    pub frame_ms: f32,
    pub camera: EditorCameraPose,
    pub error: Option<String>,
    pub project_id: Option<ProjectId>,
    pub pending_chunks: usize,
    pub rebuilt_chunks: usize,
    pub retained_chunks: usize,
    pub mesh_uploads: u64,
    pub vegetation_source_hash: String,
    pub vegetation_density_source_hash: String,
    pub vegetation_instances: usize,
    pub vegetation_rebuilt_chunks: usize,
    pub vegetation_retained_chunks: usize,
    pub vegetation_mesh_uploads: u64,
    pub path_source_hash: String,
    pub path_count: usize,
    pub path_samples: usize,
    pub update_kind: String,
}

#[derive(Default)]
pub struct EditorViewportBridge {
    pub brush_pointer: Option<EditorBrushPointer>,
    pub brush_hit: Option<EditorBrushHit>,
    pub brush_sample_serial: Option<u64>,
    pub brush_start_pointer: Option<EditorBrushPointer>,
    pub brush_start_hit: Option<EditorBrushHit>,
    pub path_draft: Option<EditorPathDraft>,
    pub renderer_starts: u32,
    pub desired_generation: u64,
    pub clear: bool,
    pub pending: Option<EditorTerrainRequest>,
    pub input: EditorCameraInput,
    pub status: EditorViewportStatus,
}

#[derive(Clone, Default, Resource)]
pub struct EditorViewport(pub Arc<Mutex<EditorViewportBridge>>);

pub struct EditorViewportPlugin(pub EditorViewport);
impl Plugin for EditorViewportPlugin {
    fn build(&self, app: &mut App) {
        embedded_asset!(app, "shaders/visual_valley_terrain.wgsl");
        app.insert_resource(self.0.clone())
            .init_resource::<EditorProjection>()
            .init_resource::<path::EditorPathOverlay>()
            .add_plugins(MaterialPlugin::<VisualValleyTerrainMaterial>::default())
            .add_systems(Startup, setup_editor)
            .add_systems(Update, update_editor)
            .add_systems(
                PostUpdate,
                brush::update_brush_cursor
                    .after(bevy::transform::TransformSystems::Propagate)
                    .after(bevy::camera::visibility::VisibilitySystems::CheckVisibility),
            );
        app.add_systems(PostUpdate, path::draw_editor_paths);
    }
}

#[derive(Component)]
pub(crate) struct EditorCamera;

#[derive(Component)]
pub(crate) struct EditorTerrainChunk;

#[derive(Component)]
pub(crate) struct EditorVegetationChunk;

struct PresentedVegetationChunk {
    source: VegetationChunk,
    object: Option<(Entity, Handle<Mesh>)>,
}

#[derive(Resource, Default)]
struct EditorProjection {
    project: Option<ProjectId>,
    active: Option<AreaId>,
    cameras: BTreeMap<AreaId, EditorCameraPose>,
    pose: EditorCameraPose,
    objects: BTreeMap<TerrainChunkCoordinate, (Entity, Handle<Mesh>)>,
    vegetation: BTreeMap<TerrainChunkCoordinate, PresentedVegetationChunk>,
    marker: Option<(Entity, Handle<Mesh>)>,
    pending: Option<PendingProjection>,
    mesh_uploads: u64,
    vegetation_mesh_uploads: u64,
    material: Handle<VisualValleyTerrainMaterial>,
    marker_material: Handle<StandardMaterial>,
    vegetation_material: Handle<StandardMaterial>,
}

struct PendingProjection {
    status: EditorViewportStatus,
    chunks: std::vec::IntoIter<TerrainChunkMesh>,
    staged: BTreeMap<TerrainChunkCoordinate, (Entity, Handle<Mesh>)>,
    vegetation_chunks: std::vec::IntoIter<VegetationChunk>,
    staged_vegetation: BTreeMap<TerrainChunkCoordinate, PresentedVegetationChunk>,
    clear_vegetation: bool,
    paths: Option<GeneratedPaths>,
    replace_paths: bool,
    spawn: [f32; 3],
}

fn setup_editor(world: &mut World) {
    if let Ok(mut bridge) = world.resource::<EditorViewport>().0.lock() {
        bridge.renderer_starts += 1;
    }
    let material = world
        .resource_mut::<Assets<VisualValleyTerrainMaterial>>()
        .add(procedural_terrain_material());
    let marker = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            base_color: Color::srgb(1.0, 0.55, 0.08),
            unlit: true,
            ..default()
        });
    let vegetation_material =
        world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial {
                base_color: Color::WHITE,
                metallic: 0.0,
                perceptual_roughness: 0.92,
                reflectance: 0.28,
                double_sided: true,
                cull_mode: None,
                ..default()
            });
    let mut projection = world.resource_mut::<EditorProjection>();
    projection.material = material;
    projection.marker_material = marker;
    projection.vegetation_material = vegetation_material;
    world.spawn((
        EditorCamera,
        Camera3d::default(),
        AmbientLight {
            color: Color::srgb(0.60, 0.67, 0.76),
            brightness: 120.0,
            ..default()
        },
        Projection::Perspective(PerspectiveProjection {
            near: 0.25,
            far: 1800.0,
            fov: 52.0_f32.to_radians(),
            ..default()
        }),
        EditorCameraPose::default().transform(),
    ));
    world.spawn((
        DirectionalLight {
            illuminance: 35_000.0,
            shadow_maps_enabled: true,
            ..default()
        },
        Transform::IDENTITY.looking_at(Vec3::new(-0.4, -1.0, -0.3), Vec3::Y),
        bevy::light::CascadeShadowConfigBuilder {
            maximum_distance: 700.0,
            ..default()
        }
        .build(),
    ));
}

fn clear_objects(world: &mut World, projection: &mut EditorProjection) {
    clear_pending(world, projection);
    for (_, (entity, mesh)) in std::mem::take(&mut projection.objects) {
        world.despawn(entity);
        world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
    }
    clear_vegetation_objects(world, projection);
    if let Some((entity, mesh)) = projection.marker.take() {
        world.despawn(entity);
        world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
    }
    if let Some(mut overlay) = world.get_resource_mut::<path::EditorPathOverlay>() {
        overlay.canonical = None;
    }
}

fn clear_vegetation_objects(world: &mut World, projection: &mut EditorProjection) {
    for (_, presented) in std::mem::take(&mut projection.vegetation) {
        if let Some((entity, mesh)) = presented.object {
            world.despawn(entity);
            world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
        }
    }
}

fn clear_pending(world: &mut World, projection: &mut EditorProjection) {
    if let Some(pending) = projection.pending.take() {
        for (_, (entity, mesh)) in pending.staged {
            world.despawn(entity);
            world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
        }
        for (_, presented) in pending.staged_vegetation {
            if let Some((entity, mesh)) = presented.object {
                world.despawn(entity);
                world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
            }
        }
    }
}

fn validate_terrain(terrain: &GeneratedTerrain, partial: bool) -> Result<(), String> {
    if terrain.chunks.len() > TERRAIN_CHUNK_COUNT
        || (!partial && terrain.chunks.len() != TERRAIN_CHUNK_COUNT)
    {
        return Err("Nombre de chunks invalide.".into());
    }
    let mut coordinates = std::collections::BTreeSet::new();
    for chunk in &terrain.chunks {
        if !chunk.coordinate.is_in_bounds()
            || !coordinates.insert((chunk.coordinate.x, chunk.coordinate.z))
            || chunk.positions_m.len() != 1089
            || chunk.indices.len() != 6144
        {
            return Err("Topologie de terrain invalide ou dupliquée.".into());
        }
    }
    Ok(())
}

fn validate_vegetation(
    terrain: &GeneratedTerrain,
    vegetation: &GeneratedVegetation,
) -> Result<(), String> {
    if vegetation.terrain_document_hash != terrain.source_document_hash
        || vegetation
            .origin_xz_m
            .into_iter()
            .zip(terrain.origin_xz_m)
            .any(|(vegetation_origin, terrain_origin)| {
                vegetation_origin.to_bits() != terrain_origin.to_bits()
            })
        || vegetation.chunks.len() != TERRAIN_CHUNK_COUNT
        || vegetation
            .origin_xz_m
            .into_iter()
            .any(|value| !value.is_finite())
        || !vegetation.cell_size_m.is_finite()
        || vegetation.cell_size_m <= 0.0
        || vegetation.cells_per_axis == 0
        || (vegetation.cell_size_m * f32::from(vegetation.cells_per_axis) - TERRAIN_EXTENT_M).abs()
            > 0.001
    {
        return Err("Sources ou nombre de chunks de végétation incohérents.".into());
    }
    let mut coordinates = std::collections::BTreeSet::new();
    let mut instances = 0_usize;
    let mut vertices = 0_usize;
    let mut actual_counts = [0_usize; 4];
    for chunk in &vegetation.chunks {
        let mut chunk_counts = [0_usize; 4];
        for instance in &chunk.instances {
            chunk_counts[vegetation_kind_index(instance.kind)] += 1;
        }
        let chunk_vertices =
            editor_vegetation_vertex_count(&chunk.instances).map_err(|error| error.to_string())?;
        if !chunk.coordinate.is_in_bounds()
            || !coordinates.insert((chunk.coordinate.x, chunk.coordinate.z))
            || checked_kind_counts(chunk.kind_counts) != Some(chunk_counts)
            || chunk_vertices > MAX_EDITOR_VEGETATION_VERTICES_PER_CHUNK
            || chunk.instances.iter().any(|instance| {
                !instance.position_m.is_finite()
                    || instance.position_m.x.abs() > TERRAIN_HALF_EXTENT_M + 0.01
                    || instance.position_m.z.abs() > TERRAIN_HALF_EXTENT_M + 0.01
                    || instance.position_m.y.abs() > 1_000_000.0
                    || !instance.yaw_radians.is_finite()
                    || !instance.scale.is_finite()
                    || instance.scale <= 0.0
                    || instance.scale > 8.0
                    || !instance.wind_phase_radians.is_finite()
                    || !instance.wind_response.is_finite()
                    || !(0.0..=1.0).contains(&instance.wind_response)
                    || !instance.slope_gradient_squared.is_finite()
                    || instance
                        .material_weight
                        .is_some_and(|weight| !weight.is_finite() || !(0.0..=1.0).contains(&weight))
                    || !vegetation_instance_belongs_to_chunk(
                        instance,
                        chunk.coordinate,
                        vegetation.origin_xz_m,
                    )
            })
        {
            return Err("Projection de végétation invalide ou dupliquée.".into());
        }
        for (total, chunk_count) in actual_counts.iter_mut().zip(chunk_counts) {
            *total = total
                .checked_add(chunk_count)
                .ok_or_else(|| "Trop d’instances de végétation.".to_owned())?;
        }
        instances = instances
            .checked_add(chunk.instances.len())
            .ok_or_else(|| "Trop d’instances de végétation.".to_owned())?;
        vertices = vertices
            .checked_add(chunk_vertices)
            .ok_or_else(|| "Trop de sommets de végétation.".to_owned())?;
    }
    if checked_kind_counts(vegetation.kind_counts) != Some(actual_counts)
        || actual_counts
            .into_iter()
            .try_fold(0_usize, usize::checked_add)
            != Some(instances)
        || vertices > MAX_EDITOR_VEGETATION_VERTICES
    {
        return Err("Compteurs globaux de végétation incohérents.".into());
    }
    Ok(())
}

fn vegetation_instance_belongs_to_chunk(
    instance: &VegetationInstance,
    coordinate: TerrainChunkCoordinate,
    origin_xz_m: [f32; 2],
) -> bool {
    let chunk_extent_m = TERRAIN_EXTENT_M / f32::from(TERRAIN_CHUNKS_PER_AXIS);
    let minimum_x = origin_xz_m[0] + f32::from(coordinate.x) * chunk_extent_m;
    let minimum_z = origin_xz_m[1] + f32::from(coordinate.z) * chunk_extent_m;
    let maximum_x = minimum_x + chunk_extent_m;
    let maximum_z = minimum_z + chunk_extent_m;
    instance.position_m.x >= minimum_x
        && instance.position_m.x < maximum_x
        && instance.position_m.z >= minimum_z
        && instance.position_m.z < maximum_z
}

const fn vegetation_kind_index(kind: VegetationKind) -> usize {
    match kind {
        VegetationKind::Tree => 0,
        VegetationKind::Shrub => 1,
        VegetationKind::Fern => 2,
        VegetationKind::Grass => 3,
    }
}

fn checked_kind_counts(counts: VegetationKindCounts) -> Option<[usize; 4]> {
    Some([
        usize::try_from(counts.trees).ok()?,
        usize::try_from(counts.shrubs).ok()?,
        usize::try_from(counts.ferns).ok()?,
        usize::try_from(counts.grass_patches).ok()?,
    ])
}

fn reset_projection_if_requested(
    world: &mut World,
    projection: &mut EditorProjection,
    bridge: &mut EditorViewportBridge,
) {
    if !bridge.clear {
        return;
    }
    bridge.brush_hit = None;
    bridge.brush_sample_serial = None;
    bridge.brush_start_pointer = None;
    bridge.brush_start_hit = None;
    bridge.brush_pointer = None;
    bridge.path_draft = None;
    clear_objects(world, projection);
    if let Some(area) = projection.active.take() {
        let pose = projection.pose;
        if projection.cameras.len() >= 256 && !projection.cameras.contains_key(&area) {
            projection.cameras.pop_first();
        }
        projection.cameras.insert(area, pose);
    }
    bridge.status = EditorViewportStatus {
        generation: bridge.desired_generation,
        ..default()
    };
    bridge.input = EditorCameraInput::default();
    bridge.clear = false;
}

fn refresh_pending_count(projection: &EditorProjection, bridge: &mut EditorViewportBridge) {
    bridge.status.pending_chunks = projection.pending.as_ref().map_or(0, |pending| {
        pending.chunks.len() + pending.vegetation_chunks.len()
    });
}

fn stage_terrain_chunk(
    world: &mut World,
    projection: &mut EditorProjection,
    chunk: &TerrainChunkMesh,
) -> Result<(), String> {
    let (mesh, _) = build_bevy_terrain_mesh(chunk).map_err(|error| error.to_string())?;
    let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
    let entity = world
        .spawn((
            EditorTerrainChunk,
            Mesh3d(mesh.clone()),
            MeshMaterial3d(projection.material.clone()),
            Transform::IDENTITY,
            Visibility::Hidden,
        ))
        .id();
    projection.mesh_uploads += 1;
    projection
        .pending
        .as_mut()
        .expect("a staged terrain chunk belongs to a pending projection")
        .staged
        .insert(chunk.coordinate, (entity, mesh));
    Ok(())
}

fn stage_vegetation_chunk(
    world: &mut World,
    projection: &mut EditorProjection,
    chunk: VegetationChunk,
) -> Result<(), String> {
    let mesh =
        build_editor_vegetation_chunk_mesh(&chunk.instances).map_err(|error| error.to_string())?;
    let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
    let entity = world
        .spawn((
            Name::new(format!(
                "Végétation éditeur ({}, {})",
                chunk.coordinate.x, chunk.coordinate.z
            )),
            EditorVegetationChunk,
            Mesh3d(mesh.clone()),
            MeshMaterial3d(projection.vegetation_material.clone()),
            Transform::IDENTITY,
            Visibility::Hidden,
        ))
        .id();
    projection.vegetation_mesh_uploads += 1;
    projection
        .pending
        .as_mut()
        .expect("a staged vegetation chunk belongs to a pending projection")
        .staged_vegetation
        .insert(
            chunk.coordinate,
            PresentedVegetationChunk {
                source: chunk,
                object: Some((entity, mesh)),
            },
        );
    Ok(())
}

fn stage_next_projection_mesh(
    world: &mut World,
    projection: &mut EditorProjection,
) -> Result<bool, String> {
    if let Some(chunk) = projection
        .pending
        .as_mut()
        .and_then(|pending| pending.chunks.next())
    {
        stage_terrain_chunk(world, projection, &chunk)?;
        return Ok(true);
    }
    if let Some(chunk) = projection
        .pending
        .as_mut()
        .and_then(|pending| pending.vegetation_chunks.next())
    {
        stage_vegetation_chunk(world, projection, chunk)?;
        return Ok(true);
    }
    Ok(false)
}

fn update_camera_and_status(
    world: &mut World,
    projection: &mut EditorProjection,
    bridge: &mut EditorViewportBridge,
) {
    projection.pose.apply(std::mem::take(&mut bridge.input));
    let pose = projection.pose;
    for mut transform in world
        .query_filtered::<&mut Transform, With<EditorCamera>>()
        .iter_mut(world)
    {
        *transform = pose.transform();
    }
    bridge.status.camera = pose;
    bridge.status.mesh_uploads = projection.mesh_uploads;
    bridge.status.vegetation_mesh_uploads = projection.vegetation_mesh_uploads;
    if bridge.status.chunks == TERRAIN_CHUNK_COUNT {
        bridge.status.frames += 1;
    }
    bridge.status.frame_ms = world.resource::<Time<Real>>().delta_secs() * 1000.0;
}

fn update_editor(world: &mut World) {
    let viewport = world.resource::<EditorViewport>().clone();
    let Ok(mut bridge) = viewport.0.lock() else {
        return;
    };
    world.resource_scope(|world, mut projection: Mut<EditorProjection>| {
        if bridge
            .path_draft
            .as_ref()
            .is_some_and(|draft| draft.generation != bridge.desired_generation)
        {
            bridge.path_draft = None;
        }
        if projection
            .pending
            .as_ref()
            .is_some_and(|pending| pending.status.generation != bridge.desired_generation)
        {
            clear_pending(world, &mut projection);
            bridge.status.pending_chunks = 0;
            bridge.path_draft = None;
        }
        reset_projection_if_requested(world, &mut projection, &mut bridge);
        if let Some(request) = bridge
            .pending
            .take()
            .filter(|request| request.generation == bridge.desired_generation)
            && let Err(error) = install_request(world, &mut projection, &mut bridge, request)
        {
            bridge.status.error = Some(error);
            bridge.path_draft = None;
            return;
        }
        // Two derived meshes per update; the UI and render loop remain schedulable while a full
        // vegetation source is transported atomically with its terrain projection.
        for _ in 0..2 {
            match stage_next_projection_mesh(world, &mut projection) {
                Ok(true) => refresh_pending_count(&projection, &mut bridge),
                Ok(false) => break,
                Err(error) => {
                    clear_pending(world, &mut projection);
                    bridge.status.pending_chunks = 0;
                    bridge.status.error = Some(error);
                    bridge.path_draft = None;
                    return;
                }
            }
        }
        if projection.pending.as_ref().is_some_and(|pending| {
            pending.chunks.len() == 0 && pending.vegetation_chunks.len() == 0
        }) {
            finish_projection(world, &mut projection, &mut bridge);
        }
        update_camera_and_status(world, &mut projection, &mut bridge);
    });
}

struct PreparedVegetationChanges {
    source_hash: String,
    density_source_hash: String,
    instances: usize,
    rebuilt_chunks: usize,
    retained_chunks: usize,
    chunks: Vec<VegetationChunk>,
    staged: BTreeMap<TerrainChunkCoordinate, PresentedVegetationChunk>,
    clear_existing: bool,
}

fn prepare_vegetation_changes(
    projection: &EditorProjection,
    status: &EditorViewportStatus,
    request: Option<EditorVegetationRequest>,
    partial: bool,
) -> Result<PreparedVegetationChanges, String> {
    let Some(request) = request else {
        if partial && !projection.vegetation.is_empty() {
            return Err(
                "Une projection terrain partielle doit transporter la végétation courante.".into(),
            );
        }
        return Ok(PreparedVegetationChanges {
            source_hash: if partial {
                status.vegetation_source_hash.clone()
            } else {
                String::new()
            },
            density_source_hash: if partial {
                status.vegetation_density_source_hash.clone()
            } else {
                String::new()
            },
            instances: if partial {
                status.vegetation_instances
            } else {
                0
            },
            rebuilt_chunks: 0,
            retained_chunks: if partial {
                projection.vegetation.len()
            } else {
                0
            },
            chunks: vec![],
            staged: BTreeMap::new(),
            clear_existing: !partial,
        });
    };

    let source_hash = request.generated.vegetation_document_hash.to_string();
    let density_source_hash = request.density_source_hash.to_string();
    let instances = usize::try_from(request.generated.kind_counts.total())
        .map_err(|_| "Trop d’instances de végétation.".to_owned())?;
    let mut chunks = Vec::new();
    let mut staged = BTreeMap::new();
    for chunk in request.generated.chunks {
        let changed = projection
            .vegetation
            .get(&chunk.coordinate)
            .is_none_or(|presented| presented.source != chunk);
        if changed && chunk.instances.is_empty() {
            staged.insert(
                chunk.coordinate,
                PresentedVegetationChunk {
                    source: chunk,
                    object: None,
                },
            );
        } else if changed {
            chunks.push(chunk);
        }
    }
    let rebuilt_chunks = chunks.len() + staged.len();
    Ok(PreparedVegetationChanges {
        source_hash,
        density_source_hash,
        instances,
        rebuilt_chunks,
        retained_chunks: TERRAIN_CHUNK_COUNT - rebuilt_chunks,
        chunks,
        staged,
        clear_existing: false,
    })
}

struct PreparedPathChanges {
    source_hash: String,
    count: usize,
    samples: usize,
    generated: Option<GeneratedPaths>,
    replace: bool,
}

fn prepare_path_changes(
    world: &World,
    status: &EditorViewportStatus,
    request: Option<EditorPathRequest>,
    partial: bool,
) -> Result<PreparedPathChanges, String> {
    if let Some(request) = request {
        let source_hash = request.generated.path_document_hash.to_string();
        let count = request.generated.paths.len();
        let samples = request
            .generated
            .paths
            .iter()
            .try_fold(0_usize, |total, path| total.checked_add(path.samples.len()))
            .ok_or_else(|| "Trop d’échantillons de chemins.".to_owned())?;
        return Ok(PreparedPathChanges {
            source_hash,
            count,
            samples,
            generated: Some(request.generated),
            replace: true,
        });
    }

    let has_presented_paths = world
        .get_resource::<path::EditorPathOverlay>()
        .is_some_and(|overlay| overlay.canonical.is_some());
    if partial && has_presented_paths {
        return Err(
            "Une projection terrain partielle doit transporter les chemins courants.".into(),
        );
    }
    Ok(PreparedPathChanges {
        source_hash: if partial {
            status.path_source_hash.clone()
        } else {
            String::new()
        },
        count: if partial { status.path_count } else { 0 },
        samples: if partial { status.path_samples } else { 0 },
        generated: None,
        replace: !partial,
    })
}

fn select_request_target(
    world: &mut World,
    projection: &mut EditorProjection,
    bridge: &mut EditorViewportBridge,
    request: &EditorTerrainRequest,
) -> Result<bool, String> {
    let partial = request.base_source_hash.is_some();
    if let Some(base_hash) = request.base_source_hash
        && (projection.project != Some(request.project_id)
            || projection.active != Some(request.area_id)
            || projection.objects.len() != TERRAIN_CHUNK_COUNT
            || bridge.status.source_hash != base_hash.to_string())
    {
        return Err("Base de projection partielle périmée ou étrangère.".into());
    }
    clear_pending(world, projection);
    if projection.project != Some(request.project_id) {
        projection.cameras.clear();
        projection.project = Some(request.project_id);
        projection.active = None;
    }
    if projection.active != Some(request.area_id) {
        projection.pose = projection
            .cameras
            .get(&request.area_id)
            .copied()
            .unwrap_or_default();
    }
    projection.active = Some(request.area_id);
    Ok(partial)
}

fn install_request(
    world: &mut World,
    projection: &mut EditorProjection,
    bridge: &mut EditorViewportBridge,
    request: EditorTerrainRequest,
) -> Result<(), String> {
    let partial = request.base_source_hash.is_some();
    validate_terrain(&request.terrain, partial)?;
    if let Some(vegetation) = &request.vegetation {
        validate_vegetation(&request.terrain, &vegetation.generated)?;
    }
    if let Some(paths) = &request.paths {
        path::validate_generated_paths(&request.terrain, &paths.generated)?;
    }
    if !request.spawn.iter().all(|value| value.is_finite()) {
        return Err("Départ de projection non fini.".into());
    }
    let partial = select_request_target(world, projection, bridge, &request)?;
    let rebuilt_chunks = request.terrain.chunks.len();
    let vegetation =
        prepare_vegetation_changes(projection, &bridge.status, request.vegetation, partial)?;
    let paths = prepare_path_changes(world, &bridge.status, request.paths, partial)?;
    let status = EditorViewportStatus {
        generation: request.generation,
        project_id: Some(request.project_id),
        area_id: Some(request.area_id),
        revision: request.revision,
        source_hash: request.terrain.source_document_hash.to_string(),
        chunks: TERRAIN_CHUNK_COUNT,
        rebuilt_chunks,
        retained_chunks: if partial {
            TERRAIN_CHUNK_COUNT - rebuilt_chunks
        } else {
            0
        },
        vegetation_source_hash: vegetation.source_hash,
        vegetation_density_source_hash: vegetation.density_source_hash,
        vegetation_instances: vegetation.instances,
        vegetation_rebuilt_chunks: vegetation.rebuilt_chunks,
        vegetation_retained_chunks: vegetation.retained_chunks,
        path_source_hash: paths.source_hash,
        path_count: paths.count,
        path_samples: paths.samples,
        update_kind: if partial { "partial" } else { "full" }.into(),
        frames: bridge.status.frames,
        ..default()
    };
    if !partial {
        bridge.status = EditorViewportStatus {
            chunks: 0,
            ..status.clone()
        };
    }
    bridge.status.error = None;
    bridge.status.pending_chunks = rebuilt_chunks + vegetation.chunks.len();
    projection.pending = Some(PendingProjection {
        status,
        chunks: request.terrain.chunks.into_iter(),
        staged: BTreeMap::new(),
        vegetation_chunks: vegetation.chunks.into_iter(),
        staged_vegetation: vegetation.staged,
        clear_vegetation: vegetation.clear_existing,
        paths: paths.generated,
        replace_paths: paths.replace,
        spawn: request.spawn,
    });
    Ok(())
}

fn finish_projection(
    world: &mut World,
    projection: &mut EditorProjection,
    bridge: &mut EditorViewportBridge,
) {
    let Some(pending) = projection.pending.take() else {
        return;
    };
    // All replacements have been validated/adapted while hidden. Publish them together.
    for (coordinate, (entity, mesh)) in pending.staged {
        world.entity_mut(entity).insert(Visibility::Visible);
        if let Some((old_entity, old_mesh)) = projection.objects.insert(coordinate, (entity, mesh))
        {
            world.despawn(old_entity);
            world.resource_mut::<Assets<Mesh>>().remove(old_mesh.id());
        }
    }
    if pending.clear_vegetation {
        clear_vegetation_objects(world, projection);
    }
    for (coordinate, presented) in pending.staged_vegetation {
        if let Some((entity, _)) = presented.object {
            world.entity_mut(entity).insert(Visibility::Visible);
        }
        if let Some(old) = projection.vegetation.insert(coordinate, presented)
            && let Some((old_entity, old_mesh)) = old.object
        {
            world.despawn(old_entity);
            world.resource_mut::<Assets<Mesh>>().remove(old_mesh.id());
        }
    }
    if pending.replace_paths {
        if let Some(mut overlay) = world.get_resource_mut::<path::EditorPathOverlay>() {
            overlay.canonical = pending.paths;
        } else if let Some(paths) = pending.paths {
            world.insert_resource(path::EditorPathOverlay {
                canonical: Some(paths),
            });
        }
        bridge.path_draft = None;
    }
    let transform = Transform::from_translation(Vec3::from_array(pending.spawn));
    if let Some((entity, _)) = &projection.marker {
        world.entity_mut(*entity).insert(transform);
    } else {
        let mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(Cuboid::new(3.0, 3.0, 3.0));
        let entity = world
            .spawn((
                Name::new("Point de départ — métadonnée"),
                Mesh3d(mesh.clone()),
                MeshMaterial3d(projection.marker_material.clone()),
                transform,
                Visibility::Visible,
            ))
            .id();
        projection.marker = Some((entity, mesh));
    }
    bridge.status = pending.status;
}

#[cfg(test)]
#[path = "editor_partial_tests.rs"]
mod partial_tests;

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;
    use forge_project::{
        AuthoredPath, NewProjectCommand, PathControlPoint, PathDocument, PathId, PathInterpolation,
        VegetationDensityOverride, VegetationKind, WorkspaceDocument,
    };

    fn request(generation: u64, name: &str) -> EditorTerrainRequest {
        let mut document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        document.area_id = AreaId::deterministic(name);
        EditorTerrainRequest {
            generation,
            project_id: document.project_id,
            area_id: document.area_id,
            revision: 1,
            spawn: [0.0, 2.0, 0.0],
            base_source_hash: None,
            vegetation: None,
            paths: None,
            terrain: forge_world::TerrainGenerationPlan::from_document(&document)
                .unwrap()
                .generate()
                .unwrap(),
        }
    }

    #[test]
    fn latest_projection_replaces_old_meshes_and_clear_releases_assets() {
        let viewport = EditorViewport::default();
        let mut world = World::new();
        world.insert_resource(viewport.clone());
        world.insert_resource(EditorProjection::default());
        world.insert_resource(Assets::<Mesh>::default());
        world.insert_resource(Time::<Real>::default());
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 1;
            bridge.pending = Some(request(1, "first"));
            bridge.clear = true;
        }
        for _ in 0..34 {
            update_editor(&mut world);
        }
        assert_eq!(viewport.0.lock().unwrap().status.chunks, 64);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 65);
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.input.orbit = [30.0, 10.0];
        }
        update_editor(&mut world);
        let pose = viewport.0.lock().unwrap().status.camera;
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 2;
            bridge.clear = true;
            bridge.pending = Some(request(1, "obsolete"));
        }
        update_editor(&mut world);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(viewport.0.lock().unwrap().status.chunks, 0);
        viewport.0.lock().unwrap().pending = Some(request(2, "second"));
        for _ in 0..34 {
            update_editor(&mut world);
        }
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 65);
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 3;
            bridge.clear = true;
            bridge.pending = Some(request(3, "first"));
        }
        update_editor(&mut world);
        assert_eq!(viewport.0.lock().unwrap().status.camera, pose);
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 4;
            bridge.clear = true;
        }
        update_editor(&mut world);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert!(world.resource::<EditorProjection>().pending.is_none());
    }

    #[test]
    fn malformed_topology_is_rejected_without_partial_presentation() {
        let mut terrain = request(1, "invalid").terrain;
        terrain.chunks[1].coordinate = terrain.chunks[0].coordinate;
        assert!(validate_terrain(&terrain, false).is_err());
        terrain.chunks.clear();
        assert!(validate_terrain(&terrain, false).is_err());
    }

    fn request_with_path(generation: u64, second_x: f32) -> EditorTerrainRequest {
        let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let paths = PathDocument::new(
            document.project_id,
            document.area_id,
            forge_core::Revision::new(generation),
            "Chemins de la zone",
            vec![AuthoredPath {
                id: PathId::deterministic("editor-path"),
                name: "Chemin 1".into(),
                closed: false,
                interpolation: PathInterpolation::CentripetalCatmullRomV1,
                control_points: vec![
                    PathControlPoint::new(forge_core::Vec3::new(-16.0, 0.0, -8.0)),
                    PathControlPoint::new(forge_core::Vec3::new(second_x, 0.0, 8.0)),
                ],
            }],
        );
        let generated_paths = forge_world::PathGenerationPlan::from_documents(&document, &paths)
            .unwrap()
            .generate()
            .unwrap();
        EditorTerrainRequest {
            generation,
            project_id: document.project_id,
            area_id: document.area_id,
            revision: generation,
            terrain: forge_world::TerrainGenerationPlan::from_document(&document)
                .unwrap()
                .generate()
                .unwrap(),
            vegetation: None,
            paths: Some(EditorPathRequest {
                generated: generated_paths,
            }),
            spawn: [0.0, 2.0, 0.0],
            base_source_hash: None,
        }
    }

    #[test]
    fn path_only_projection_atomically_replaces_overlay_without_mesh_upload() {
        let viewport = EditorViewport::default();
        let mut world = World::new();
        world.insert_resource(viewport.clone());
        world.insert_resource(EditorProjection::default());
        world.insert_resource(path::EditorPathOverlay::default());
        world.insert_resource(Assets::<Mesh>::default());
        world.insert_resource(Time::<Real>::default());

        let first = request_with_path(1, 16.0);
        let terrain_hash = first.terrain.source_document_hash;
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 1;
            bridge.pending = Some(first);
        }
        for _ in 0..34 {
            update_editor(&mut world);
        }
        let uploads = world.resource::<EditorProjection>().mesh_uploads;
        let first_hash = viewport.0.lock().unwrap().status.path_source_hash.clone();
        assert_eq!(viewport.0.lock().unwrap().status.path_count, 1);

        let mut second = request_with_path(2, 32.0);
        second.terrain.chunks.clear();
        second.base_source_hash = Some(terrain_hash);
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 2;
            bridge.pending = Some(second);
        }
        update_editor(&mut world);

        let overlay = world.resource::<path::EditorPathOverlay>();
        let generated = overlay.canonical.as_ref().unwrap();
        assert_eq!(
            generated.paths[0].control_points[1].x.to_bits(),
            32.0_f32.to_bits()
        );
        assert_eq!(world.resource::<EditorProjection>().mesh_uploads, uploads);
        let status = &viewport.0.lock().unwrap().status;
        assert_eq!((status.path_count, status.rebuilt_chunks), (1, 0));
        assert_ne!(status.path_source_hash, first_hash);
    }

    #[test]
    fn invalid_path_projection_leaves_presented_overlay_intact() {
        let viewport = EditorViewport::default();
        let mut world = World::new();
        world.insert_resource(viewport.clone());
        world.insert_resource(EditorProjection::default());
        world.insert_resource(path::EditorPathOverlay::default());
        world.insert_resource(Assets::<Mesh>::default());
        world.insert_resource(Time::<Real>::default());
        let first = request_with_path(1, 16.0);
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.desired_generation = 1;
            bridge.pending = Some(first);
        }
        for _ in 0..34 {
            update_editor(&mut world);
        }
        let before = world
            .resource::<path::EditorPathOverlay>()
            .canonical
            .clone()
            .unwrap();

        let mut invalid = request_with_path(2, 32.0);
        invalid
            .paths
            .as_mut()
            .unwrap()
            .generated
            .terrain_document_hash = ContentHash::from_bytes([0x55; 32]);
        {
            let mut bridge = viewport.0.lock().unwrap();
            bridge.path_draft = Some(EditorPathDraft {
                generation: 1,
                points_m: vec![[0.0, 1.0, 0.0], [8.0, 1.0, 8.0]],
            });
            bridge.desired_generation = 2;
            bridge.pending = Some(invalid);
        }
        update_editor(&mut world);
        let bridge = viewport.0.lock().unwrap();
        assert!(bridge.status.error.is_some());
        assert!(bridge.path_draft.is_none());
        drop(bridge);
        assert_eq!(
            world
                .resource::<path::EditorPathOverlay>()
                .canonical
                .as_ref(),
            Some(&before)
        );
    }

    struct EditorVegetationFixture {
        world: World,
        viewport: EditorViewport,
        terrain: GeneratedTerrain,
        generated: std::sync::Arc<GeneratedVegetation>,
        density_source_hash: ContentHash,
        project_id: ProjectId,
        area_id: AreaId,
    }

    impl EditorVegetationFixture {
        fn submit(&mut self, generation: u64, full: bool) {
            let mut terrain = self.terrain.clone();
            if !full {
                terrain.chunks.clear();
            }
            let request = EditorTerrainRequest {
                generation,
                project_id: self.project_id,
                area_id: self.area_id,
                revision: generation,
                terrain,
                vegetation: Some(EditorVegetationRequest {
                    generated: self.generated.as_ref().clone(),
                    density_source_hash: self.density_source_hash,
                }),
                paths: None,
                spawn: [0.0, 2.0, 0.0],
                base_source_hash: (!full).then_some(self.terrain.source_document_hash),
            };
            let mut bridge = self.viewport.0.lock().unwrap();
            bridge.desired_generation = generation;
            bridge.pending = Some(request);
        }

        fn drain(&mut self) {
            for _ in 0..128 {
                update_editor(&mut self.world);
                if self.world.resource::<EditorProjection>().pending.is_none() {
                    return;
                }
            }
            panic!("editor projection did not finish within its bounded chunk count");
        }
    }

    fn editor_vegetation_fixture() -> EditorVegetationFixture {
        let mut document = WorkspaceDocument::create(NewProjectCommand {
            command_id: forge_core::CommandId::deterministic("editor-vegetation-create"),
            project_id: ProjectId::deterministic("editor-vegetation-project"),
            module_id: forge_core::ModuleId::deterministic("editor-vegetation-module"),
            area_id: AreaId::deterministic("editor-vegetation-area"),
            project_name: "Projet végétation".into(),
            module_name: "Module".into(),
            area_name: "Zone".into(),
            seed: 73,
            spawn: forge_core::Vec3::new(0.0, 2.0, 0.0),
        })
        .unwrap()
        .document;
        let area = &mut document.project.modules[0].areas[0];
        let grass_layer = area
            .vegetation
            .layers
            .iter()
            .find(|layer| layer.kind == VegetationKind::Grass)
            .unwrap()
            .id
            .clone();
        area.vegetation_density
            .layers
            .iter_mut()
            .find(|layer| layer.layer_id == grass_layer)
            .unwrap()
            .overrides = vec![VegetationDensityOverride::new(32, 32, u8::MAX)];
        area.vegetation_density.validate().unwrap();
        let terrain = forge_world::TerrainGenerationPlan::from_document(&area.terrain)
            .unwrap()
            .generate()
            .unwrap();
        let generated = forge_world::VegetationGenerationPlan::from_documents_with_density(
            &area.terrain,
            &area.vegetation,
            &area.vegetation_density,
        )
        .unwrap()
        .generate()
        .unwrap();
        assert!(generated.kind_counts.grass_patches > 0);
        let density_source_hash = area.vegetation_density.content_hash().unwrap();
        let viewport = EditorViewport::default();
        let mut world = World::new();
        world.insert_resource(viewport.clone());
        world.insert_resource(EditorProjection::default());
        world.insert_resource(Assets::<Mesh>::default());
        world.insert_resource(Time::<Real>::default());
        EditorVegetationFixture {
            world,
            viewport,
            terrain,
            generated,
            density_source_hash,
            project_id: area.terrain.project_id,
            area_id: area.id,
        }
    }

    #[test]
    fn editor_vegetation_is_chunk_batched_and_published_visible() {
        let mut fixture = editor_vegetation_fixture();
        fixture.submit(1, true);
        fixture.drain();
        let projection = fixture.world.resource::<EditorProjection>();
        let populated_chunks = fixture
            .generated
            .chunks
            .iter()
            .filter(|chunk| !chunk.instances.is_empty())
            .count();
        assert_eq!(projection.vegetation.len(), TERRAIN_CHUNK_COUNT);
        assert_eq!(
            projection
                .vegetation
                .values()
                .filter(|presented| presented.object.is_some())
                .count(),
            populated_chunks
        );
        for presented in projection.vegetation.values() {
            if let Some((entity, _)) = presented.object {
                assert_eq!(
                    fixture.world.get::<Visibility>(entity),
                    Some(&Visibility::Visible)
                );
            }
        }
        let status = fixture.viewport.0.lock().unwrap().status.clone();
        assert_eq!(status.vegetation_rebuilt_chunks, TERRAIN_CHUNK_COUNT);
        assert_eq!(status.vegetation_retained_chunks, 0);
        assert_eq!(
            status.vegetation_instances,
            usize::try_from(fixture.generated.kind_counts.total()).unwrap()
        );
    }

    #[test]
    fn unchanged_editor_vegetation_reuses_every_chunk_without_upload() {
        let mut fixture = editor_vegetation_fixture();
        fixture.submit(1, true);
        fixture.drain();
        let vegetation_uploads = fixture
            .world
            .resource::<EditorProjection>()
            .vegetation_mesh_uploads;
        fixture.submit(2, false);
        fixture.drain();
        let projection = fixture.world.resource::<EditorProjection>();
        assert_eq!(projection.vegetation_mesh_uploads, vegetation_uploads);
        let status = &fixture.viewport.0.lock().unwrap().status;
        assert_eq!(status.vegetation_rebuilt_chunks, 0);
        assert_eq!(status.vegetation_retained_chunks, TERRAIN_CHUNK_COUNT);
    }

    #[test]
    fn editor_batch_keeps_a_domain_valid_subnormal_scale_renderable() {
        let fixture = editor_vegetation_fixture();
        let mut instance = fixture
            .generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .next()
            .unwrap()
            .clone();
        instance.scale = f32::MIN_POSITIVE;
        let mesh = build_editor_vegetation_chunk_mesh(&[instance]).unwrap();
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("editor vegetation requires Float32x3 positions");
        };
        let minimum = positions
            .iter()
            .copied()
            .reduce(|left, right| std::array::from_fn(|axis| left[axis].min(right[axis])))
            .unwrap();
        let maximum = positions
            .iter()
            .copied()
            .reduce(|left, right| std::array::from_fn(|axis| left[axis].max(right[axis])))
            .unwrap();
        let extent = std::array::from_fn::<_, 3, _>(|axis| maximum[axis] - minimum[axis]);
        assert!(extent.into_iter().all(|value| value > 0.0 && value < 0.01));
    }

    #[test]
    fn forged_vegetation_counts_are_rejected_without_overflow() {
        let fixture = editor_vegetation_fixture();
        let mut generated = fixture.generated.as_ref().clone();
        generated.kind_counts.trees = u32::MAX;
        assert!(validate_vegetation(&fixture.terrain, &generated).is_err());
    }

    #[test]
    fn vegetation_requires_the_terrain_grid_and_exact_chunk_ownership() {
        let fixture = editor_vegetation_fixture();

        let mut shifted_origin = fixture.generated.as_ref().clone();
        shifted_origin.origin_xz_m[0] += 1.0;
        assert!(validate_vegetation(&fixture.terrain, &shifted_origin).is_err());

        let mut malformed_grid = fixture.generated.as_ref().clone();
        malformed_grid.cell_size_m *= 0.5;
        assert!(validate_vegetation(&fixture.terrain, &malformed_grid).is_err());

        let mut misplaced = fixture.generated.as_ref().clone();
        let chunk = misplaced
            .chunks
            .iter_mut()
            .find(|chunk| !chunk.instances.is_empty())
            .unwrap();
        let wrong_chunk_x = (chunk.coordinate.x + 1) % TERRAIN_CHUNKS_PER_AXIS;
        chunk.instances[0].position_m.x =
            misplaced.origin_xz_m[0] + f32::from(wrong_chunk_x) * 64.0 + 1.0;
        assert!(validate_vegetation(&fixture.terrain, &misplaced).is_err());
    }

    #[test]
    fn terrain_only_partial_cannot_leave_presented_vegetation_stale() {
        let mut fixture = editor_vegetation_fixture();
        fixture.submit(1, true);
        fixture.drain();
        let uploads = fixture
            .world
            .resource::<EditorProjection>()
            .vegetation_mesh_uploads;
        let mut terrain = fixture.terrain.clone();
        terrain.chunks.clear();
        let request = EditorTerrainRequest {
            generation: 2,
            project_id: fixture.project_id,
            area_id: fixture.area_id,
            revision: 2,
            terrain,
            vegetation: None,
            paths: None,
            spawn: [0.0, 2.0, 0.0],
            base_source_hash: Some(fixture.terrain.source_document_hash),
        };
        {
            let mut bridge = fixture.viewport.0.lock().unwrap();
            bridge.desired_generation = 2;
            bridge.pending = Some(request);
        }
        update_editor(&mut fixture.world);
        assert!(fixture.viewport.0.lock().unwrap().status.error.is_some());
        let projection = fixture.world.resource::<EditorProjection>();
        assert_eq!(projection.vegetation.len(), TERRAIN_CHUNK_COUNT);
        assert_eq!(projection.vegetation_mesh_uploads, uploads);
    }

    #[test]
    fn camera_bounds_and_invalid_input_never_make_invalid_transform() {
        let mut pose = EditorCameraPose::default();
        pose.apply(EditorCameraInput {
            orbit: [1e20, -1e20],
            pan: [1e20, -1e20],
            zoom: 1e20,
            ..default()
        });
        assert!((12.0..=1100.0).contains(&pose.distance));
        assert!((0.05..=1.52).contains(&pose.pitch));
        assert!(pose.transform().translation.is_finite());
        let before = pose;
        pose.apply(EditorCameraInput {
            orbit: [f32::NAN, 0.0],
            ..default()
        });
        assert_eq!(pose, before);
        pose.apply(EditorCameraInput {
            reset: true,
            ..default()
        });
        assert!((pose.distance - 480.0).abs() < f32::EPSILON);
    }
}
