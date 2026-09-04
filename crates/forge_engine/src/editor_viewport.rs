//! Project terrain projection. One renderer, latest request wins, no canonical ECS data.
use crate::visual_valley::{
    VisualValleyTerrainMaterial, build_bevy_terrain_mesh, procedural_terrain_material,
};
use bevy::{asset::embedded_asset, prelude::*};
use forge_core::{AreaId, ProjectId};
use forge_world::{GeneratedTerrain, TERRAIN_CHUNK_COUNT, TerrainChunkMesh};
use serde::Serialize;
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};

#[path = "editor_brush.rs"]
mod brush;
pub use brush::{EditorBrushHit, EditorBrushPointer};

pub struct EditorTerrainRequest {
    pub generation: u64,
    pub project_id: ProjectId,
    pub area_id: AreaId,
    pub revision: u64,
    pub terrain: GeneratedTerrain,
    pub spawn: [f32; 3],
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
}

#[derive(Default)]
pub struct EditorViewportBridge {
    pub brush_pointer: Option<EditorBrushPointer>,
    pub brush_hit: Option<EditorBrushHit>,
    pub brush_sample_serial: Option<u64>,
    pub brush_start_pointer: Option<EditorBrushPointer>,
    pub brush_start_hit: Option<EditorBrushHit>,
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
            .add_plugins(MaterialPlugin::<VisualValleyTerrainMaterial>::default())
            .add_systems(Startup, setup_editor)
            .add_systems(Update, update_editor)
            .add_systems(
                PostUpdate,
                brush::update_brush_cursor.after(bevy::transform::TransformSystems::Propagate),
            );
    }
}

#[derive(Component)]
pub(crate) struct EditorCamera;

#[derive(Component)]
pub(crate) struct EditorTerrainChunk;

#[derive(Resource, Default)]
struct EditorProjection {
    project: Option<ProjectId>,
    active: Option<AreaId>,
    cameras: BTreeMap<AreaId, EditorCameraPose>,
    pose: EditorCameraPose,
    objects: Vec<(Entity, Handle<Mesh>)>,
    pending: Option<std::vec::IntoIter<TerrainChunkMesh>>,
    material: Handle<VisualValleyTerrainMaterial>,
    marker_material: Handle<StandardMaterial>,
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
    let mut projection = world.resource_mut::<EditorProjection>();
    projection.material = material;
    projection.marker_material = marker;
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
    for (entity, mesh) in projection.objects.drain(..) {
        world.despawn(entity);
        world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
    }
    projection.pending = None;
}

fn validate_terrain(terrain: &GeneratedTerrain) -> Result<(), String> {
    if terrain.chunks.len() != TERRAIN_CHUNK_COUNT {
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

fn update_editor(world: &mut World) {
    let viewport = world.resource::<EditorViewport>().clone();
    let Ok(mut bridge) = viewport.0.lock() else {
        return;
    };
    world.resource_scope(|world, mut projection: Mut<EditorProjection>| {
        if bridge.clear {
            bridge.brush_hit = None;
            bridge.brush_sample_serial = None;
            bridge.brush_start_pointer = None;
            bridge.brush_start_hit = None;
            bridge.brush_pointer = None;
            clear_objects(world, &mut projection);
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
        if let Some(request) = bridge
            .pending
            .take()
            .filter(|request| request.generation == bridge.desired_generation)
            && let Err(error) = install_request(world, &mut projection, &mut bridge, request)
        {
            bridge.status.error = Some(error);
            return;
        }
        // Two derived meshes per update; the UI and the render loop remain schedulable.
        for _ in 0..2 {
            let Some(chunk) = projection.pending.as_mut().and_then(Iterator::next) else {
                break;
            };
            let mesh = match build_bevy_terrain_mesh(&chunk) {
                Ok((mesh, _)) => mesh,
                Err(error) => {
                    clear_objects(world, &mut projection);
                    bridge.status.chunks = 0;
                    bridge.status.error = Some(error.to_string());
                    return;
                }
            };
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
            projection.objects.push((entity, mesh));
            bridge.status.chunks += 1;
        }
        if projection
            .pending
            .as_ref()
            .is_some_and(|chunks| chunks.len() == 0)
        {
            projection.pending = None;
            for (entity, _) in &projection.objects {
                world.entity_mut(*entity).insert(Visibility::Visible);
            }
        }
        projection.pose.apply(std::mem::take(&mut bridge.input));
        let pose = projection.pose;
        for mut transform in world
            .query_filtered::<&mut Transform, With<EditorCamera>>()
            .iter_mut(world)
        {
            *transform = pose.transform();
        }
        bridge.status.camera = pose;
        if bridge.status.chunks == TERRAIN_CHUNK_COUNT {
            bridge.status.frames += 1;
        }
        bridge.status.frame_ms = world.resource::<Time<Real>>().delta_secs() * 1000.0;
    });
}

fn install_request(
    world: &mut World,
    projection: &mut EditorProjection,
    bridge: &mut EditorViewportBridge,
    request: EditorTerrainRequest,
) -> Result<(), String> {
    clear_objects(world, projection);
    if projection.project != Some(request.project_id) {
        projection.cameras.clear();
        projection.project = Some(request.project_id);
    }
    validate_terrain(&request.terrain)?;
    projection.active = Some(request.area_id);
    projection.pose = projection
        .cameras
        .get(&request.area_id)
        .copied()
        .unwrap_or_default();
    bridge.status = EditorViewportStatus {
        generation: request.generation,
        area_id: Some(request.area_id),
        revision: request.revision,
        source_hash: request.terrain.source_document_hash.to_string(),
        ..default()
    };
    projection.pending = Some(request.terrain.chunks.into_iter());
    // Authored metadata marker, not a player and not snapped to the terrain.
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(3.0, 3.0, 3.0));
    let entity = world
        .spawn((
            Name::new("Point de départ — métadonnée"),
            Mesh3d(mesh.clone()),
            MeshMaterial3d(projection.marker_material.clone()),
            Transform::from_translation(Vec3::from_array(request.spawn)),
            Visibility::Hidden,
        ))
        .id();
    projection.objects.push((entity, mesh));
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn request(generation: u64, name: &str) -> EditorTerrainRequest {
        let mut document = forge_world::embedded_visual_valley_terrain_document().unwrap();
        document.area_id = AreaId::deterministic(name);
        EditorTerrainRequest {
            generation,
            project_id: document.project_id,
            area_id: document.area_id,
            revision: 1,
            spawn: [0.0, 2.0, 0.0],
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
        assert!(validate_terrain(&terrain).is_err());
        terrain.chunks.clear();
        assert!(validate_terrain(&terrain).is_err());
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
