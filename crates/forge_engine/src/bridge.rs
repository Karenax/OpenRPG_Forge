use bevy::{
    asset::RenderAssetUsages, mesh::Indices, prelude::*, render::render_resource::PrimitiveTopology,
};
use bevy_rapier3d::prelude::Collider;
use forge_core::ContentHash;
use forge_project::{TerrainDocument, TerrainDocumentError};
use forge_world::{
    WorldSketchBridgePlacement, WorldSketchBridgePlacementDocument, WorldSketchBridgePlacementError,
};
use serde::Serialize;
use std::{error::Error, fmt};
use tracing::info;

const PAVER_COURSE_COUNT: usize = 18;
const PAVER_COLUMN_COUNT: usize = 4;
const PARAPET_POST_COUNT: usize = 9;
const ARCH_BLOCKS_PER_SPAN: usize = 17;
const PAVER_GAP_M: f32 = 0.08;
const PAVER_HEIGHT_M: f32 = 0.10;

/// Stable marker for the only walkable collider belonging to one derived bridge.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct WorldSketchBridgeDeck {
    pub placement_index: usize,
}

/// Marker for bridge parapet colliders. They constrain the deck without becoming walkable links.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct WorldSketchBridgeParapet {
    pub placement_index: usize,
}

/// Runtime projection counts. These report source mesh entities, not GPU draw calls.
#[derive(Clone, Debug, Default, Eq, PartialEq, Resource, Serialize)]
pub struct WorldSketchBridgeStats {
    pub source_hash: String,
    pub placements: usize,
    pub visual_mesh_instances: usize,
    pub deck_colliders: usize,
    pub parapet_colliders: usize,
    pub support_colliders: usize,
    pub paver_instances: usize,
    pub parapet_post_instances: usize,
    pub arch_voussoir_instances: usize,
    pub preview_water_instances: usize,
}

/// Asset-free Bevy/Rapier adapter for Forge bridge-placement documents.
pub struct WorldSketchBridgePlugin {
    document: WorldSketchBridgePlacementDocument,
    preview_camera: bool,
}

impl WorldSketchBridgePlugin {
    pub fn new(
        terrain: &TerrainDocument,
        document: WorldSketchBridgePlacementDocument,
    ) -> Result<Self, WorldSketchBridgeAdapterError> {
        document.validate()?;
        if document.placements.is_empty() {
            return Err(WorldSketchBridgeAdapterError::NoPlacements);
        }
        let terrain_hash = terrain.content_hash()?;
        if terrain_hash != document.source_carved_terrain_sha256 {
            return Err(WorldSketchBridgeAdapterError::TerrainHashMismatch {
                expected: document.source_carved_terrain_sha256,
                actual: terrain_hash,
            });
        }
        Ok(Self {
            document,
            preview_camera: false,
        })
    }

    /// Replaces the terrain overview camera with one deterministic bridge qualification view.
    #[must_use]
    pub const fn with_preview_camera(mut self, enabled: bool) -> Self {
        self.preview_camera = enabled;
        self
    }
}

impl Plugin for WorldSketchBridgePlugin {
    fn build(&self, app: &mut App) {
        let source_hash = self
            .document
            .content_hash()
            .expect("validated bridge-placement document hashes deterministically");
        app.insert_resource(BridgeRuntimeDocument(self.document.clone()))
            .insert_resource(WorldSketchBridgeStats {
                source_hash: source_hash.to_string(),
                placements: self.document.placements.len(),
                ..default()
            })
            .insert_resource(BridgePreviewCameraEnabled(self.preview_camera))
            .add_systems(Startup, spawn_world_sketch_bridges)
            .add_systems(PostStartup, log_world_sketch_bridge_projection);
    }
}

#[derive(Clone, Resource)]
struct BridgeRuntimeDocument(WorldSketchBridgePlacementDocument);

#[derive(Clone, Copy, Resource)]
struct BridgePreviewCameraEnabled(bool);

#[derive(Clone, Copy, Debug)]
struct BridgeDeckProjection {
    start_surface: Vec3,
    end_surface: Vec3,
    surface_midpoint: Vec3,
    horizontal_forward: Vec3,
    across: Vec3,
    normal: Vec3,
    rotation: Quat,
    length_m: f32,
    collider_center: Vec3,
}

impl BridgeDeckProjection {
    fn from_placement(placement: &WorldSketchBridgePlacement) -> Self {
        let start_surface = array_to_vec3(placement.deck_start_surface_m);
        let end_surface = array_to_vec3(placement.deck_end_surface_m);
        let delta = end_surface - start_surface;
        let horizontal_delta = Vec3::new(delta.x, 0.0, delta.z);
        let horizontal_forward = horizontal_delta.normalize();
        let yaw = horizontal_forward.x.atan2(horizontal_forward.z);
        let slope = delta.y.atan2(horizontal_delta.length());
        let rotation = Quat::from_rotation_y(yaw) * Quat::from_rotation_x(-slope);
        let across = rotation * Vec3::X;
        let normal = rotation * Vec3::Y;
        let surface_midpoint = (start_surface + end_surface) * 0.5;
        let collider_center = surface_midpoint - normal * (placement.deck_thickness_m * 0.5);
        Self {
            start_surface,
            end_surface,
            surface_midpoint,
            horizontal_forward,
            across,
            normal,
            rotation,
            length_m: delta.length(),
            collider_center,
        }
    }

    fn surface_at(self, fraction: f32) -> Vec3 {
        self.start_surface
            .lerp(self.end_surface, fraction.clamp(0.0, 1.0))
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn spawn_world_sketch_bridges(
    mut commands: Commands,
    document: Res<BridgeRuntimeDocument>,
    preview_camera: Res<BridgePreviewCameraEnabled>,
    mut stats: ResMut<WorldSketchBridgeStats>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<StandardMaterial>>,
) {
    let masonry = materials.add(StandardMaterial {
        base_color: Color::srgb(0.34, 0.30, 0.23),
        perceptual_roughness: 0.94,
        metallic: 0.01,
        reflectance: 0.36,
        ..default()
    });
    let paving = materials.add(StandardMaterial {
        base_color: Color::srgb(0.43, 0.39, 0.31),
        perceptual_roughness: 0.88,
        metallic: 0.015,
        reflectance: 0.4,
        ..default()
    });
    let accent = materials.add(StandardMaterial {
        base_color: Color::srgb(0.25, 0.23, 0.19),
        perceptual_roughness: 0.95,
        reflectance: 0.32,
        ..default()
    });
    let water = materials.add(StandardMaterial {
        base_color: Color::srgb(0.045, 0.27, 0.34),
        perceptual_roughness: 0.16,
        metallic: 0.04,
        reflectance: 0.72,
        ..default()
    });

    for (placement_index, placement) in document.0.placements.iter().enumerate() {
        let projection = BridgeDeckProjection::from_placement(placement);
        spawn_bridge_deck(
            &mut commands,
            &mut meshes,
            &masonry,
            placement,
            placement_index,
            projection,
        );
        spawn_bridge_pavers(&mut commands, &mut meshes, &paving, placement, projection);
        spawn_bridge_parapets(
            &mut commands,
            &mut meshes,
            &masonry,
            &accent,
            placement,
            placement_index,
            projection,
        );
        spawn_bridge_supports(&mut commands, &mut meshes, &masonry, placement, projection);
        spawn_bridge_arch_rings(
            &mut commands,
            &mut meshes,
            &masonry,
            &accent,
            placement,
            projection,
        );

        let span_count = placement.supports.len() + 1;
        let paver_instances = PAVER_COURSE_COUNT * PAVER_COLUMN_COUNT;
        let arch_instances = span_count * 2 * ARCH_BLOCKS_PER_SPAN;
        stats.deck_colliders += 1;
        stats.parapet_colliders += 2;
        stats.support_colliders += placement.supports.len();
        stats.paver_instances += paver_instances;
        stats.parapet_post_instances += PARAPET_POST_COUNT * 2;
        stats.arch_voussoir_instances += arch_instances;
        stats.visual_mesh_instances += 1
            + paver_instances
            + 2
            + PARAPET_POST_COUNT * 2
            + placement.supports.len() * 3
            + arch_instances;
    }

    if preview_camera.0 {
        let placement = &document.0.placements[0];
        spawn_bridge_preview_camera(
            &mut commands,
            BridgeDeckProjection::from_placement(placement),
        );
        spawn_bridge_preview_water(&mut commands, &mut meshes, &water, placement);
        stats.preview_water_instances = 1;
        stats.visual_mesh_instances += 1;
    }
}

fn spawn_bridge_deck(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    material: &Handle<StandardMaterial>,
    placement: &WorldSketchBridgePlacement,
    placement_index: usize,
    projection: BridgeDeckProjection,
) {
    let dimensions = Vec3::new(
        placement.deck_width_m,
        placement.deck_thickness_m,
        projection.length_m,
    );
    commands.spawn((
        Name::new(format!(
            "WorldSketch bridge deck · {}",
            placement.landmark_id
        )),
        WorldSketchBridgeDeck { placement_index },
        Mesh3d(meshes.add(Cuboid::new(dimensions.x, dimensions.y, dimensions.z))),
        MeshMaterial3d(material.clone()),
        Collider::cuboid(dimensions.x * 0.5, dimensions.y * 0.5, dimensions.z * 0.5),
        Transform::from_translation(projection.collider_center).with_rotation(projection.rotation),
    ));
}

#[allow(clippy::cast_precision_loss)]
fn spawn_bridge_pavers(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    material: &Handle<StandardMaterial>,
    placement: &WorldSketchBridgePlacement,
    projection: BridgeDeckProjection,
) {
    let paver_length = projection.length_m / PAVER_COURSE_COUNT as f32;
    let usable_width_m = placement.deck_width_m - 1.5;
    let paver_width_m = usable_width_m / PAVER_COLUMN_COUNT as f32;
    let mesh = meshes.add(Cuboid::new(
        paver_width_m - PAVER_GAP_M,
        PAVER_HEIGHT_M,
        paver_length - PAVER_GAP_M,
    ));
    for course in 0..PAVER_COURSE_COUNT {
        let fraction = (course as f32 + 0.5) / PAVER_COURSE_COUNT as f32;
        for column in 0..PAVER_COLUMN_COUNT {
            let across_fraction = (column as f32 + 0.5) / PAVER_COLUMN_COUNT as f32 - 0.5;
            let across_offset = projection.across * across_fraction * usable_width_m;
            commands.spawn((
                Name::new(format!("Bridge paving stone {course:02}-{column:02}")),
                Mesh3d(mesh.clone()),
                MeshMaterial3d(material.clone()),
                Transform::from_translation(
                    projection.surface_at(fraction)
                        + across_offset
                        + projection.normal * (PAVER_HEIGHT_M * 0.5),
                )
                .with_rotation(projection.rotation),
            ));
        }
    }
}

#[allow(clippy::cast_precision_loss, clippy::too_many_arguments)]
fn spawn_bridge_parapets(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    masonry: &Handle<StandardMaterial>,
    accent: &Handle<StandardMaterial>,
    placement: &WorldSketchBridgePlacement,
    placement_index: usize,
    projection: BridgeDeckProjection,
) {
    let parapet_mesh = meshes.add(Cuboid::new(
        placement.parapet_thickness_m,
        placement.parapet_height_m,
        projection.length_m,
    ));
    let parapet_collider = Collider::cuboid(
        placement.parapet_thickness_m * 0.5,
        placement.parapet_height_m * 0.5,
        projection.length_m * 0.5,
    );
    let post_edge_m = placement.parapet_thickness_m + 0.18;
    let post_mesh = meshes.add(Cuboid::new(
        post_edge_m,
        placement.parapet_height_m + 0.28,
        post_edge_m,
    ));
    for side in [-1.0_f32, 1.0] {
        let across_offset = projection.across
            * side
            * (placement.deck_width_m - placement.parapet_thickness_m)
            * 0.5;
        let parapet_center = projection.surface_midpoint
            + across_offset
            + projection.normal * (placement.parapet_height_m * 0.5);
        commands.spawn((
            Name::new("WorldSketch bridge parapet"),
            WorldSketchBridgeParapet { placement_index },
            Mesh3d(parapet_mesh.clone()),
            MeshMaterial3d(masonry.clone()),
            parapet_collider.clone(),
            Transform::from_translation(parapet_center).with_rotation(projection.rotation),
        ));

        for index in 0..PARAPET_POST_COUNT {
            let fraction = index as f32 / (PARAPET_POST_COUNT - 1) as f32;
            let center = projection.surface_at(fraction)
                + across_offset
                + projection.normal * f32::midpoint(placement.parapet_height_m, 0.28);
            commands.spawn((
                Name::new(format!("Bridge parapet post {index:02}")),
                Mesh3d(post_mesh.clone()),
                MeshMaterial3d(if index % 2 == 0 {
                    masonry.clone()
                } else {
                    accent.clone()
                }),
                Transform::from_translation(center).with_rotation(projection.rotation),
            ));
        }
    }
}

fn spawn_bridge_supports(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    material: &Handle<StandardMaterial>,
    placement: &WorldSketchBridgePlacement,
    projection: BridgeDeckProjection,
) {
    let yaw = projection
        .horizontal_forward
        .x
        .atan2(projection.horizontal_forward.z);
    for (index, support) in placement.supports.iter().enumerate() {
        let surface = projection.surface_at(support.center_fraction);
        let top_height_m = surface.y - placement.deck_thickness_m;
        let height_m = (top_height_m - support.ground_height_m).max(0.5);
        let pier_mesh = meshes.add(Cuboid::new(
            support.depth_across_bridge_m,
            height_m,
            support.width_along_bridge_m,
        ));
        let pier_center = Vec3::new(
            surface.x,
            support.ground_height_m + height_m * 0.5,
            surface.z,
        );
        commands.spawn((
            Name::new(format!("Bridge pier {index}")),
            Mesh3d(pier_mesh),
            MeshMaterial3d(material.clone()),
            Collider::cuboid(
                support.depth_across_bridge_m * 0.5,
                height_m * 0.5,
                support.width_along_bridge_m * 0.5,
            ),
            Transform::from_translation(pier_center).with_rotation(Quat::from_rotation_y(yaw)),
        ));

        let buttress_mesh = meshes.add(Cuboid::new(
            1.0,
            height_m * 0.76,
            support.width_along_bridge_m + 0.8,
        ));
        for side in [-1.0_f32, 1.0] {
            let center = pier_center
                + Vec3::Y * (-height_m * 0.12)
                + projection.across * side * (support.depth_across_bridge_m * 0.5 + 0.35);
            commands.spawn((
                Name::new(format!("Bridge pier buttress {index}")),
                Mesh3d(buttress_mesh.clone()),
                MeshMaterial3d(material.clone()),
                Transform::from_translation(center).with_rotation(Quat::from_rotation_y(yaw)),
            ));
        }
    }
}

#[allow(clippy::cast_precision_loss)]
fn spawn_bridge_arch_rings(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    masonry: &Handle<StandardMaterial>,
    accent: &Handle<StandardMaterial>,
    placement: &WorldSketchBridgePlacement,
    projection: BridgeDeckProjection,
) {
    let mut boundaries = Vec::with_capacity(placement.supports.len() + 2);
    boundaries.push(0.0_f32);
    boundaries.extend(
        placement
            .supports
            .iter()
            .map(|support| support.center_fraction),
    );
    boundaries.push(1.0);
    let yaw = projection
        .horizontal_forward
        .x
        .atan2(projection.horizontal_forward.z);

    for (span_index, span) in boundaries.windows(2).enumerate() {
        let start = projection.surface_at(span[0]);
        let end = projection.surface_at(span[1]);
        let horizontal_midpoint = Vec3::new(
            f32::midpoint(start.x, end.x),
            0.0,
            f32::midpoint(start.z, end.z),
        );
        let horizontal_span_m = Vec3::new(end.x - start.x, 0.0, end.z - start.z).length();
        let horizontal_radius_m = horizontal_span_m * 0.42;
        let crown_height_m = f32::midpoint(start.y, end.y) - placement.deck_thickness_m * 0.72;
        let available_height_m = (crown_height_m - placement.crossing_bed_height_m - 0.35).max(1.5);
        let vertical_radius_m = available_height_m.min(horizontal_radius_m * 0.72);
        let spring_height_m = crown_height_m - vertical_radius_m;
        let ring_thickness_m = 0.72_f32
            .min(horizontal_radius_m * 0.22)
            .min(vertical_radius_m * 0.32);
        let inner_horizontal_radius_m = horizontal_radius_m - ring_thickness_m;
        let inner_vertical_radius_m = vertical_radius_m - ring_thickness_m;

        for side in [-1.0_f32, 1.0] {
            let across_offset = projection.across * side * (placement.deck_width_m * 0.5 + 0.08);
            for block_index in 0..ARCH_BLOCKS_PER_SPAN {
                let theta_start =
                    std::f32::consts::PI * block_index as f32 / ARCH_BLOCKS_PER_SPAN as f32;
                let theta_end =
                    std::f32::consts::PI * (block_index + 1) as f32 / ARCH_BLOCKS_PER_SPAN as f32;
                let block_mesh = meshes.add(build_arch_voussoir_mesh(
                    horizontal_radius_m,
                    vertical_radius_m,
                    inner_horizontal_radius_m,
                    inner_vertical_radius_m,
                    theta_start,
                    theta_end,
                    0.54,
                ));
                let center = horizontal_midpoint + across_offset + Vec3::Y * spring_height_m;
                commands.spawn((
                    Name::new(format!(
                        "Bridge arch voussoir {span_index:02}-{block_index:02}"
                    )),
                    Mesh3d(block_mesh.clone()),
                    MeshMaterial3d(if (span_index + block_index) % 3 == 0 {
                        accent.clone()
                    } else {
                        masonry.clone()
                    }),
                    Transform::from_translation(center).with_rotation(Quat::from_rotation_y(yaw)),
                ));
            }
        }
    }
}

#[allow(clippy::too_many_arguments)]
fn build_arch_voussoir_mesh(
    outer_horizontal_radius_m: f32,
    outer_vertical_radius_m: f32,
    inner_horizontal_radius_m: f32,
    inner_vertical_radius_m: f32,
    theta_start: f32,
    theta_end: f32,
    depth_m: f32,
) -> Mesh {
    let outer_start = Vec3::new(
        0.0,
        outer_vertical_radius_m * theta_start.sin(),
        outer_horizontal_radius_m * theta_start.cos(),
    );
    let outer_end = Vec3::new(
        0.0,
        outer_vertical_radius_m * theta_end.sin(),
        outer_horizontal_radius_m * theta_end.cos(),
    );
    let inner_start = Vec3::new(
        0.0,
        inner_vertical_radius_m * theta_start.sin(),
        inner_horizontal_radius_m * theta_start.cos(),
    );
    let inner_end = Vec3::new(
        0.0,
        inner_vertical_radius_m * theta_end.sin(),
        inner_horizontal_radius_m * theta_end.cos(),
    );
    let half_depth_m = depth_m * 0.5;
    let front = Vec3::X * half_depth_m;
    let back = -front;
    let mut positions = Vec::with_capacity(24);
    let mut normals = Vec::with_capacity(24);
    let mut uvs = Vec::with_capacity(24);
    let mut indices = Vec::with_capacity(36);

    push_mesh_quad(
        &mut positions,
        &mut normals,
        &mut uvs,
        &mut indices,
        outer_start + front,
        inner_start + front,
        inner_end + front,
        outer_end + front,
    );
    push_mesh_quad(
        &mut positions,
        &mut normals,
        &mut uvs,
        &mut indices,
        outer_end + back,
        inner_end + back,
        inner_start + back,
        outer_start + back,
    );
    push_mesh_quad(
        &mut positions,
        &mut normals,
        &mut uvs,
        &mut indices,
        outer_start + back,
        outer_start + front,
        outer_end + front,
        outer_end + back,
    );
    push_mesh_quad(
        &mut positions,
        &mut normals,
        &mut uvs,
        &mut indices,
        inner_end + back,
        inner_end + front,
        inner_start + front,
        inner_start + back,
    );
    push_mesh_quad(
        &mut positions,
        &mut normals,
        &mut uvs,
        &mut indices,
        outer_start + back,
        inner_start + back,
        inner_start + front,
        outer_start + front,
    );
    push_mesh_quad(
        &mut positions,
        &mut normals,
        &mut uvs,
        &mut indices,
        outer_end + front,
        inner_end + front,
        inner_end + back,
        outer_end + back,
    );

    Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, normals)
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, uvs)
    .with_inserted_indices(Indices::U32(indices))
}

#[allow(clippy::cast_possible_truncation, clippy::too_many_arguments)]
fn push_mesh_quad(
    positions: &mut Vec<[f32; 3]>,
    normals: &mut Vec<[f32; 3]>,
    uvs: &mut Vec<[f32; 2]>,
    indices: &mut Vec<u32>,
    a: Vec3,
    b: Vec3,
    c: Vec3,
    d: Vec3,
) {
    let first_index = positions.len() as u32;
    let normal = (b - a).cross(c - a).normalize_or_zero().to_array();
    positions.extend([a.to_array(), b.to_array(), c.to_array(), d.to_array()]);
    normals.extend([normal; 4]);
    uvs.extend([[0.0, 0.0], [0.0, 1.0], [1.0, 1.0], [1.0, 0.0]]);
    indices.extend([
        first_index,
        first_index + 1,
        first_index + 2,
        first_index,
        first_index + 2,
        first_index + 3,
    ]);
}

fn spawn_bridge_preview_water(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    material: &Handle<StandardMaterial>,
    placement: &WorldSketchBridgePlacement,
) {
    let yaw = placement.water_direction_xz[0].atan2(placement.water_direction_xz[1]);
    let water_level_m = placement.crossing_bed_height_m + 0.42;
    commands.spawn((
        Name::new("WorldSketch bridge qualification water preview"),
        Mesh3d(meshes.add(Cuboid::new(placement.water_width_m, 0.10, 72.0))),
        MeshMaterial3d(material.clone()),
        Transform::from_xyz(
            placement.crossing_center_m[0],
            water_level_m,
            placement.crossing_center_m[1],
        )
        .with_rotation(Quat::from_rotation_y(yaw)),
    ));
}

fn spawn_bridge_preview_camera(commands: &mut Commands, projection: BridgeDeckProjection) {
    let target = projection.surface_midpoint - Vec3::Y * 0.5;
    let position =
        target - projection.horizontal_forward * 20.0 + projection.across * 25.0 + Vec3::Y * 13.0;
    commands.spawn((
        Name::new("WorldSketch bridge preview camera"),
        Camera3d::default(),
        AmbientLight {
            color: Color::srgb(0.62, 0.72, 0.82),
            brightness: 110.0,
            ..default()
        },
        Projection::Perspective(PerspectiveProjection {
            fov: 52.0_f32.to_radians(),
            near: 0.05,
            far: 1_600.0,
            ..default()
        }),
        Transform::from_translation(position).looking_at(target, Vec3::Y),
    ));
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn log_world_sketch_bridge_projection(stats: Res<WorldSketchBridgeStats>) {
    info!(
        target: "forge::bridge",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "world_sketch_bridge_projected",
        source_hash = %stats.source_hash,
        placements = stats.placements,
        visual_mesh_instances = stats.visual_mesh_instances,
        deck_colliders = stats.deck_colliders,
        parapet_colliders = stats.parapet_colliders,
        support_colliders = stats.support_colliders,
        paver_instances = stats.paver_instances,
        parapet_post_instances = stats.parapet_post_instances,
        arch_voussoir_instances = stats.arch_voussoir_instances,
        preview_water_instances = stats.preview_water_instances,
        "Forge bridge placement projected into procedural meshes and Rapier colliders"
    );
}

const fn array_to_vec3(value: [f32; 3]) -> Vec3 {
    Vec3::new(value[0], value[1], value[2])
}

#[derive(Debug)]
pub enum WorldSketchBridgeAdapterError {
    InvalidDocument(WorldSketchBridgePlacementError),
    InvalidTerrain(TerrainDocumentError),
    NoPlacements,
    TerrainHashMismatch {
        expected: ContentHash,
        actual: ContentHash,
    },
}

impl fmt::Display for WorldSketchBridgeAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidDocument(error) => write!(formatter, "invalid bridge placements: {error}"),
            Self::InvalidTerrain(error) => write!(formatter, "invalid bridge terrain: {error}"),
            Self::NoPlacements => write!(formatter, "bridge-placement document contains no bridge"),
            Self::TerrainHashMismatch { expected, actual } => write!(
                formatter,
                "bridge placements require carved terrain {expected}, loaded terrain is {actual}"
            ),
        }
    }
}

impl Error for WorldSketchBridgeAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::InvalidDocument(error) => Some(error),
            Self::InvalidTerrain(error) => Some(error),
            Self::NoPlacements | Self::TerrainHashMismatch { .. } => None,
        }
    }
}

impl From<WorldSketchBridgePlacementError> for WorldSketchBridgeAdapterError {
    fn from(error: WorldSketchBridgePlacementError) -> Self {
        Self::InvalidDocument(error)
    }
}

impl From<TerrainDocumentError> for WorldSketchBridgeAdapterError {
    fn from(error: TerrainDocumentError) -> Self {
        Self::InvalidTerrain(error)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_world::{compile_world_sketch, embedded_visual_valley_world_sketch};

    #[test]
    fn deck_projection_maps_local_top_endpoints_to_the_exact_forge_landings() {
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        let placement = &compiled.bridge_placements.placements[0];
        let projection = BridgeDeckProjection::from_placement(placement);
        let local_start = Vec3::new(
            0.0,
            placement.deck_thickness_m * 0.5,
            -projection.length_m * 0.5,
        );
        let local_end = Vec3::new(
            0.0,
            placement.deck_thickness_m * 0.5,
            projection.length_m * 0.5,
        );
        let projected_start = projection.collider_center + projection.rotation * local_start;
        let projected_end = projection.collider_center + projection.rotation * local_end;

        assert!(projected_start.abs_diff_eq(projection.start_surface, 1.0e-4));
        assert!(projected_end.abs_diff_eq(projection.end_surface, 1.0e-4));
        assert!((projection.rotation * Vec3::Z).abs_diff_eq(
            (projection.end_surface - projection.start_surface).normalize(),
            1.0e-5
        ));
    }

    #[test]
    fn adapter_binds_the_exact_carved_terrain_hash_and_rejects_base_terrain() {
        let compiled =
            compile_world_sketch(&embedded_visual_valley_world_sketch().unwrap()).unwrap();
        assert!(
            WorldSketchBridgePlugin::new(
                &compiled.carved_terrain,
                compiled.bridge_placements.clone()
            )
            .is_ok()
        );
        assert!(matches!(
            WorldSketchBridgePlugin::new(&compiled.terrain, compiled.bridge_placements),
            Err(WorldSketchBridgeAdapterError::TerrainHashMismatch { .. })
        ));
    }
}
