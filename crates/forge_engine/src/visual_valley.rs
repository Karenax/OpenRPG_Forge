use super::{ProofSceneStats, RuntimeAutomation, RuntimeEvidencePlugin};
#[cfg(feature = "physics-rapier")]
use crate::physics::TerrainPhysicsPlugin;
#[cfg(feature = "physics-rapier")]
use crate::player_controller::PlayerControllerPlugin;
#[cfg(feature = "physics-rapier")]
use crate::terrain_update::{
    TerrainRuntimeBindings, TerrainRuntimeState, TerrainRuntimeUpdatePlugin,
};
use crate::vegetation::VegetationPresentation;
use bevy::{
    asset::{RenderAssetUsages, embedded_asset},
    image::{ImageAddressMode, ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    light::CascadeShadowConfigBuilder,
    material::OpaqueRendererMethod,
    mesh::Indices,
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::render_resource::{
        AsBindGroup, Extent3d, PrimitiveTopology, TextureDimension, TextureFormat,
    },
    shader::ShaderRef,
};
#[cfg(feature = "physics-rapier")]
use forge_project::PlayerControllerProfileDocument;
use forge_project::TerrainDocument;
#[cfg(feature = "physics-rapier")]
use forge_world::GeneratedTerrain;
use forge_world::{
    TERRAIN_CHUNK_COUNT, TerrainChunkCoordinate, TerrainChunkId, TerrainChunkMesh,
    TerrainGenerationError, TerrainGenerationPlan,
};
use serde::Serialize;
use std::{error::Error, fmt};

const TERRAIN_SHADER_PATH: &str = "embedded://forge_engine/shaders/visual_valley_terrain.wgsl";
const VISUAL_VALLEY_LOD_LEVEL: u8 = 0;
const DEFAULT_VISUAL_VALLEY_SHADOW_DISTANCE_M: f32 = 620.0;
#[cfg(feature = "physics-rapier")]
const DEFAULT_PLAYER_SPAWN_X_M: f32 = 16.0;
#[cfg(feature = "physics-rapier")]
const DEFAULT_PLAYER_SPAWN_Z_M: f32 = 8.0;

/// Derived logical, renderer-independent counts observed while adapting Visual Valley.
///
/// These are source mesh counts, not GPU draw calls. The draw-call limitation recorded by
/// ADR-0004 remains unchanged and is not extended to Gate 1 by this resource.
#[derive(Clone, Debug, Default, Eq, PartialEq, Resource, Serialize)]
pub struct VisualValleyStats {
    pub source_hash: String,
    pub chunks: usize,
    pub vertices: usize,
    pub triangles: usize,
    pub material_layers: usize,
    pub lod_levels: usize,
    pub lod_zero_chunks: usize,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum VisualValleyShadowObservationStatus {
    #[default]
    NotObserved,
    ObservedSingleSun,
    UnavailableNoSun,
    UnavailableMultipleSuns,
}

/// Runtime evidence copied from the `CascadeShadowConfig` attached to the actual valley sun.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct VisualValleyShadowStats {
    pub quality: Option<crate::ShadowQualityObservation>,
    pub status: VisualValleyShadowObservationStatus,
    pub sun_entities: usize,
    pub shadow_maps_enabled: Option<bool>,
    pub configured_maximum_distance_m: Option<f32>,
    pub cascade_bounds_m: Vec<f32>,
    pub evidence_source: &'static str,
}

impl Default for VisualValleyShadowStats {
    fn default() -> Self {
        Self {
            quality: None,
            status: VisualValleyShadowObservationStatus::NotObserved,
            sun_entities: 0,
            shadow_maps_enabled: None,
            configured_maximum_distance_m: None,
            cascade_bounds_m: Vec::new(),
            evidence_source: "visual_valley_sun_cascade_shadow_component",
        }
    }
}

impl VisualValleyStats {
    pub(crate) fn overlay_details(&self) -> String {
        format!(
            "{} chunks · {} vertices · {} triangles\n{} couches PBR · LOD 0 : {} chunks",
            self.chunks, self.vertices, self.triangles, self.material_layers, self.lod_zero_chunks
        )
    }
}

/// Stable engine marker attached to every derived terrain chunk entity.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct VisualValleyTerrainChunk {
    pub id: TerrainChunkId,
    pub coordinate: TerrainChunkCoordinate,
    /// Canonical full-resolution binding. Render-only LOD siblings do not carry this marker.
    pub lod_level: u8,
}

/// Runtime switch for the chunk/LOD gizmo overlay.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Resource)]
pub struct VisualValleyDebug {
    pub chunk_lod_gizmos: bool,
}

/// Forge-original PBR palette used by the procedural Visual Valley terrain.
#[derive(Asset, AsBindGroup, Clone, Debug, Reflect)]
pub struct VisualValleyTerrainExtension {
    #[uniform(100)]
    pub grass_color_roughness: Vec4,
    #[uniform(100)]
    pub earth_color_roughness: Vec4,
    #[uniform(100)]
    pub rock_color_roughness: Vec4,
    #[uniform(100)]
    pub grass_metallic_reflectance: Vec4,
    #[uniform(100)]
    pub earth_metallic_reflectance: Vec4,
    #[uniform(100)]
    pub rock_metallic_reflectance: Vec4,
    /// X is scalar wetness; Y/Z are full-wet microfacet-alpha/albedo multipliers.
    #[uniform(100)]
    pub wetness_parameters: Vec4,
    #[texture(101)]
    #[sampler(102)]
    pub detail_texture: Handle<Image>,
}

impl Default for VisualValleyTerrainExtension {
    fn default() -> Self {
        Self {
            // RGB values are authored directly in linear space; A carries perceptual roughness.
            grass_color_roughness: Vec4::new(0.055, 0.24, 0.045, 0.92),
            earth_color_roughness: Vec4::new(0.24, 0.105, 0.035, 0.86),
            rock_color_roughness: Vec4::new(0.28, 0.31, 0.29, 0.74),
            // X carries metallic and Y reflectance. Z/W remain reserved for a later version.
            grass_metallic_reflectance: Vec4::new(0.0, 0.36, 0.0, 0.0),
            earth_metallic_reflectance: Vec4::new(0.0, 0.40, 0.0, 0.0),
            rock_metallic_reflectance: Vec4::new(0.015, 0.48, 0.0, 0.0),
            // A zero wetness scalar is an exact identity for the original material output.
            wetness_parameters: Vec4::new(0.0, 1.0, 1.0, 0.0),
            detail_texture: Handle::default(),
        }
    }
}

impl MaterialExtension for VisualValleyTerrainExtension {
    fn fragment_shader() -> ShaderRef {
        TERRAIN_SHADER_PATH.into()
    }

    fn deferred_fragment_shader() -> ShaderRef {
        TERRAIN_SHADER_PATH.into()
    }
}

pub type VisualValleyTerrainMaterial =
    ExtendedMaterial<StandardMaterial, VisualValleyTerrainExtension>;

pub(crate) fn register_terrain_material(app: &mut App) {
    if !app.is_plugin_added::<MaterialPlugin<VisualValleyTerrainMaterial>>() {
        embedded_asset!(app, "shaders/visual_valley_terrain.wgsl");
        app.add_plugins(MaterialPlugin::<VisualValleyTerrainMaterial>::default());
    }
}

/// Bevy adapter for the first asset-free Visual Valley terrain tranche.
pub struct VisualValleyPlugin {
    document: TerrainDocument,
    road_surface: Option<forge_world::RoadSurfaceProjection>,
    automation: RuntimeAutomation,
    debug: VisualValleyDebug,
    overview_camera: bool,
    #[cfg(feature = "physics-rapier")]
    player_controller: Option<PlayerControllerProfileDocument>,
    #[cfg(feature = "physics-rapier")]
    player_spawn_position: Option<Vec3>,
    #[cfg(feature = "physics-rapier")]
    player_camera_target: Option<Vec3>,
    #[cfg(feature = "physics-rapier")]
    player_interactive_input: bool,
}

impl VisualValleyPlugin {
    #[must_use]
    pub fn new(document: TerrainDocument) -> Self {
        Self {
            document,
            road_surface: None,
            automation: RuntimeAutomation {
                capture_frame: 120,
                metrics_warmup_frames: 60,
                ..default()
            },
            debug: VisualValleyDebug::default(),
            overview_camera: true,
            #[cfg(feature = "physics-rapier")]
            player_controller: None,
            #[cfg(feature = "physics-rapier")]
            player_spawn_position: None,
            #[cfg(feature = "physics-rapier")]
            player_camera_target: None,
            #[cfg(feature = "physics-rapier")]
            player_interactive_input: true,
        }
    }

    #[must_use]
    pub fn with_automation(mut self, automation: RuntimeAutomation) -> Self {
        self.automation = automation;
        self
    }

    /// Applies revision-bound, derived road palette weights without changing geometry.
    pub fn with_road_surface(
        mut self,
        projection: forge_world::RoadSurfaceProjection,
    ) -> Result<Self, forge_world::RoadSurfaceError> {
        projection.validate_terrain(&self.document)?;
        self.road_surface = Some(projection);
        Ok(self)
    }

    #[must_use]
    pub const fn with_chunk_lod_gizmos(mut self, enabled: bool) -> Self {
        self.debug.chunk_lod_gizmos = enabled;
        self
    }

    /// Enables the historical overview camera when no playable controller owns the view.
    #[must_use]
    pub const fn with_overview_camera(mut self, enabled: bool) -> Self {
        self.overview_camera = enabled;
        self
    }

    /// Adds the playable Rapier projection and reserves the only active camera for it.
    #[cfg(feature = "physics-rapier")]
    #[must_use]
    pub fn with_player_controller(mut self, profile: PlayerControllerProfileDocument) -> Self {
        self.player_controller = Some(profile);
        self.overview_camera = false;
        self
    }

    /// Overrides the default terrain-sampled capsule centre for a deterministic probe fixture.
    #[cfg(feature = "physics-rapier")]
    #[must_use]
    pub const fn with_player_spawn_position(mut self, position: Vec3) -> Self {
        self.player_spawn_position = Some(position);
        self
    }

    /// A non-persistent initial heading for scene previews, without moving the authored spawn.
    #[cfg(feature = "physics-rapier")]
    #[must_use]
    pub const fn with_player_camera_target(mut self, target: Vec3) -> Self {
        self.player_camera_target = Some(target);
        self
    }

    /// Enables OS keyboard/mouse sampling for the player while retaining probe command injection.
    #[cfg(feature = "physics-rapier")]
    #[must_use]
    pub const fn with_player_interactive_input(mut self, enabled: bool) -> Self {
        self.player_interactive_input = enabled;
        self
    }
}

impl Plugin for VisualValleyPlugin {
    fn build(&self, app: &mut App) {
        #[cfg(feature = "physics-rapier")]
        assert!(
            self.player_controller.is_some()
                || (self.player_spawn_position.is_none() && self.player_camera_target.is_none()),
            "a Visual Valley player pose override requires a player-controller profile"
        );
        let prepared = if let Some(road) = &self.road_surface {
            info!(road_surface_hash = %road.source_hash(), "applying derived road surface weights");
            road.generate()
                .map_err(VisualValleyAdapterError::RoadSurface)
                .and_then(|generated| prepare_generated_visual_valley(&self.document, generated))
        } else {
            prepare_visual_valley(&self.document)
        };
        let mut prepared = prepared
            .unwrap_or_else(|error| panic!("VisualValleyPlugin requires valid terrain: {error}"));
        let stats = prepared.stats.clone();
        #[cfg(feature = "physics-rapier")]
        let terrain_cache = prepared
            .terrain_cache
            .take()
            .expect("the generated terrain cache is moved into runtime state exactly once");

        register_terrain_material(app);
        app.insert_resource(self.debug)
            .init_resource::<VisualValleyShadowStats>()
            .insert_resource(VisualValleyOverviewCameraEnabled(
                self.overview_camera && {
                    #[cfg(feature = "physics-rapier")]
                    {
                        self.player_controller.is_none()
                    }
                    #[cfg(not(feature = "physics-rapier"))]
                    {
                        true
                    }
                },
            ))
            .insert_resource(stats.clone())
            .insert_resource(ProofSceneStats {
                source_entities: 1,
                source_mesh_instances: stats.chunks,
                source_hash: stats.source_hash.clone(),
            })
            .insert_resource(PreparedVisualValley(Some(prepared)))
            .insert_resource(ClearColor(Color::srgb(0.32, 0.48, 0.66)))
            .add_plugins(RuntimeEvidencePlugin::new(self.automation.clone()))
            .init_gizmo_group::<VisualValleyChunkGizmos>()
            .add_systems(Startup, spawn_visual_valley)
            .add_systems(
                PostUpdate,
                observe_visual_valley_shadow_config.after(crate::shadow_quality::ShadowQualitySet),
            )
            .add_systems(Update, draw_visual_valley_chunk_debug);

        #[cfg(feature = "physics-rapier")]
        app.insert_resource(TerrainRuntimeState::new(
            self.document.clone(),
            terrain_cache,
        ))
        .init_resource::<TerrainRuntimeBindings>()
        .add_plugins((
            TerrainPhysicsPlugin::new(self.document.clone()),
            TerrainRuntimeUpdatePlugin,
        ));

        #[cfg(feature = "physics-rapier")]
        if let Some(profile) = &self.player_controller {
            let spawn_position = self
                .player_spawn_position
                .unwrap_or_else(|| default_visual_valley_player_spawn(&self.document, profile));
            let mut player = PlayerControllerPlugin::new(profile.clone(), spawn_position)
                .with_interactive_input(self.player_interactive_input);
            if let Some(target) = self.player_camera_target {
                player = player.with_initial_camera_target(target);
            }
            app.add_plugins(player);
        }
    }
}

/// Typed adapter failures used by headless preparation tests and plugin validation.
#[derive(Debug)]
pub enum VisualValleyAdapterError {
    Generation(TerrainGenerationError),
    RoadSurface(forge_world::RoadSurfaceError),
    UnexpectedChunkCount {
        expected: usize,
        actual: usize,
    },
    InvalidChunkPayload {
        coordinate: TerrainChunkCoordinate,
        reason: &'static str,
    },
}

impl fmt::Display for VisualValleyAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Generation(error) => write!(formatter, "terrain generation failed: {error}"),
            Self::RoadSurface(error) => {
                write!(formatter, "road surface projection failed: {error}")
            }
            Self::UnexpectedChunkCount { expected, actual } => write!(
                formatter,
                "terrain adapter expected {expected} chunks, found {actual}"
            ),
            Self::InvalidChunkPayload { coordinate, reason } => write!(
                formatter,
                "terrain chunk ({}, {}) has an invalid payload: {reason}",
                coordinate.x, coordinate.z
            ),
        }
    }
}

impl Error for VisualValleyAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Generation(error) => Some(error),
            Self::RoadSurface(error) => Some(error),
            Self::UnexpectedChunkCount { .. } | Self::InvalidChunkPayload { .. } => None,
        }
    }
}

impl From<TerrainGenerationError> for VisualValleyAdapterError {
    fn from(error: TerrainGenerationError) -> Self {
        Self::Generation(error)
    }
}

#[derive(Resource)]
struct PreparedVisualValley(Option<PreparedVisualValleyScene>);

struct PreparedVisualValleyScene {
    name: String,
    chunks: Vec<PreparedTerrainChunk>,
    stats: VisualValleyStats,
    minimum_height_m: f32,
    maximum_height_m: f32,
    #[cfg(feature = "physics-rapier")]
    terrain_cache: Option<GeneratedTerrain>,
}

struct PreparedTerrainChunk {
    marker: VisualValleyTerrainChunk,
    mesh: Mesh,
    debug_bounds: VisualValleyChunkDebugBounds,
}

#[derive(Clone, Copy, Component, Debug, PartialEq)]
pub(crate) struct VisualValleyChunkDebugBounds {
    center: Vec3,
    size: Vec3,
    lod_level: u8,
}

#[derive(Default, Reflect, GizmoConfigGroup)]
struct VisualValleyChunkGizmos;

#[derive(Component)]
struct VisualValleyOverviewCamera;

#[derive(Clone, Copy, Resource)]
struct VisualValleyOverviewCameraEnabled(bool);

#[derive(Component)]
pub(crate) struct VisualValleySun;

fn prepare_visual_valley(
    document: &TerrainDocument,
) -> Result<PreparedVisualValleyScene, VisualValleyAdapterError> {
    let generated = TerrainGenerationPlan::from_document(document)?.generate()?;
    prepare_generated_visual_valley(document, generated)
}

fn prepare_generated_visual_valley(
    document: &TerrainDocument,
    generated: forge_world::GeneratedTerrain,
) -> Result<PreparedVisualValleyScene, VisualValleyAdapterError> {
    if generated.chunks.len() != TERRAIN_CHUNK_COUNT {
        return Err(VisualValleyAdapterError::UnexpectedChunkCount {
            expected: TERRAIN_CHUNK_COUNT,
            actual: generated.chunks.len(),
        });
    }

    let mut chunks = Vec::with_capacity(generated.chunks.len());
    let mut minimum_height_m = f32::INFINITY;
    let mut maximum_height_m = f32::NEG_INFINITY;
    for chunk in &generated.chunks {
        let (mesh, debug_bounds) = build_bevy_terrain_mesh(chunk)?;
        minimum_height_m = minimum_height_m.min(debug_bounds.center.y - debug_bounds.size.y * 0.5);
        maximum_height_m = maximum_height_m.max(debug_bounds.center.y + debug_bounds.size.y * 0.5);
        chunks.push(PreparedTerrainChunk {
            marker: VisualValleyTerrainChunk {
                id: chunk.id,
                coordinate: chunk.coordinate,
                lod_level: VISUAL_VALLEY_LOD_LEVEL,
            },
            mesh,
            debug_bounds,
        });
    }

    let stats = VisualValleyStats {
        source_hash: generated.source_document_hash.to_string(),
        chunks: generated.chunks.len(),
        vertices: generated.vertex_count(),
        triangles: generated.triangle_count(),
        material_layers: generated.material_layer_ids.len(),
        lod_levels: 1,
        lod_zero_chunks: generated.chunks.len(),
    };

    Ok(PreparedVisualValleyScene {
        name: document.name.clone(),
        chunks,
        stats,
        minimum_height_m,
        maximum_height_m,
        #[cfg(feature = "physics-rapier")]
        terrain_cache: Some(generated),
    })
}

pub(crate) fn build_bevy_terrain_mesh(
    chunk: &TerrainChunkMesh,
) -> Result<(Mesh, VisualValleyChunkDebugBounds), VisualValleyAdapterError> {
    let vertex_count = chunk.positions_m.len();
    if vertex_count == 0 {
        return Err(invalid_chunk(chunk, "positions are empty"));
    }
    if chunk.normals.len() != vertex_count
        || chunk.uvs.len() != vertex_count
        || chunk.material_weights.len() != vertex_count
    {
        return Err(invalid_chunk(
            chunk,
            "position, normal, UV and material-weight counts differ",
        ));
    }
    if chunk.indices.is_empty() || !chunk.indices.len().is_multiple_of(3) {
        return Err(invalid_chunk(
            chunk,
            "indices are empty or not divisible by three",
        ));
    }
    if chunk.indices.iter().copied().any(|index| {
        usize::try_from(index)
            .ok()
            .is_none_or(|index| index >= vertex_count)
    }) {
        return Err(invalid_chunk(chunk, "an index exceeds the vertex count"));
    }
    if chunk
        .positions_m
        .iter()
        .flatten()
        .any(|value| !value.is_finite())
        || chunk
            .normals
            .iter()
            .flatten()
            .any(|value| !value.is_finite())
        || chunk.uvs.iter().flatten().any(|value| !value.is_finite())
    {
        return Err(invalid_chunk(chunk, "a vertex attribute is non-finite"));
    }
    if chunk.material_weights.iter().any(|weights| {
        let values = weights.as_array();
        let sum = values.iter().sum::<f32>();
        values
            .iter()
            .any(|value| !value.is_finite() || *value < 0.0)
            || (sum - 1.0).abs() > 0.001
    }) {
        return Err(invalid_chunk(
            chunk,
            "material weights are not finite, non-negative and normalized",
        ));
    }

    let colors = chunk
        .material_weights
        .iter()
        .map(|weights| [weights.grass, weights.earth, weights.rock, 1.0])
        .collect::<Vec<_>>();
    let mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, chunk.positions_m.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, chunk.normals.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, chunk.uvs.clone())
    .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, colors)
    .with_inserted_indices(Indices::U32(chunk.indices.clone()));

    let first_height = chunk.positions_m[0][1];
    let (minimum_height_m, maximum_height_m) = chunk.positions_m.iter().fold(
        (first_height, first_height),
        |(minimum, maximum), position| (minimum.min(position[1]), maximum.max(position[1])),
    );
    let center = Vec3::new(
        f32::midpoint(chunk.minimum_xz_m[0], chunk.maximum_xz_m[0]),
        f32::midpoint(minimum_height_m, maximum_height_m),
        f32::midpoint(chunk.minimum_xz_m[1], chunk.maximum_xz_m[1]),
    );
    let size = Vec3::new(
        chunk.maximum_xz_m[0] - chunk.minimum_xz_m[0],
        (maximum_height_m - minimum_height_m).max(0.25),
        chunk.maximum_xz_m[1] - chunk.minimum_xz_m[1],
    );
    if center.is_nan() || !center.is_finite() || size.cmple(Vec3::ZERO).any() || !size.is_finite() {
        return Err(invalid_chunk(chunk, "chunk bounds are invalid"));
    }

    Ok((
        mesh,
        VisualValleyChunkDebugBounds {
            center,
            size,
            lod_level: VISUAL_VALLEY_LOD_LEVEL,
        },
    ))
}

const fn invalid_chunk(chunk: &TerrainChunkMesh, reason: &'static str) -> VisualValleyAdapterError {
    VisualValleyAdapterError::InvalidChunkPayload {
        coordinate: chunk.coordinate,
        reason,
    }
}

/// Small derived GPU cache, generated once and shared by every terrain chunk.
pub(crate) fn terrain_detail_image() -> Image {
    let mut state = 0x466f_7267_u32;
    let data = (0..128 * 128)
        .map(|_| {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            state.to_le_bytes()[3]
        })
        .collect();
    let mut image = Image::new(
        Extent3d {
            width: 128,
            height: 128,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        data,
        TextureFormat::R8Unorm,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        address_mode_u: ImageAddressMode::Repeat,
        address_mode_v: ImageAddressMode::Repeat,
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        ..default()
    });
    image
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn spawn_visual_valley(
    mut commands: Commands,
    mut prepared: ResMut<PreparedVisualValley>,
    overview_camera_enabled: Res<VisualValleyOverviewCameraEnabled>,
    vegetation_presentation: Option<Res<VegetationPresentation>>,
    (mut meshes, mut images): (ResMut<Assets<Mesh>>, ResMut<Assets<Image>>),
    mut materials: ResMut<Assets<VisualValleyTerrainMaterial>>,
    #[cfg(feature = "physics-rapier")] mut bindings: ResMut<TerrainRuntimeBindings>,
) {
    let prepared = prepared
        .0
        .take()
        .expect("Visual Valley preparation is consumed exactly once at startup");
    let material = materials.add(VisualValleyTerrainMaterial {
        base: StandardMaterial {
            base_color: Color::WHITE,
            metallic: 0.0,
            perceptual_roughness: 1.0,
            opaque_render_method: OpaqueRendererMethod::Auto,
            ..default()
        },
        extension: VisualValleyTerrainExtension {
            detail_texture: images.add(terrain_detail_image()),
            ..default()
        },
    });

    for chunk in prepared.chunks {
        let mesh_handle = meshes.add(chunk.mesh);
        let entity = commands
            .spawn((
                Name::new(format!(
                    "Visual Valley terrain chunk ({}, {}) · LOD {}",
                    chunk.marker.coordinate.x, chunk.marker.coordinate.z, chunk.marker.lod_level
                )),
                chunk.marker,
                chunk.debug_bounds,
                Mesh3d(mesh_handle.clone()),
                MeshMaterial3d(material.clone()),
                Transform::IDENTITY,
            ))
            .id();
        #[cfg(feature = "physics-rapier")]
        bindings.register_render(
            chunk.marker.id,
            chunk.marker.coordinate,
            entity,
            mesh_handle,
        );
    }

    let shadow_config = visual_valley_shadow_config(vegetation_presentation.as_deref());
    commands.spawn((
        Name::new("Visual Valley sun"),
        VisualValleySun,
        DirectionalLight {
            color: Color::srgb(1.0, 0.93, 0.78),
            illuminance: 55_000.0,
            shadow_maps_enabled: true,
            ..default()
        },
        Transform::IDENTITY.looking_at(Vec3::new(-0.45, -1.0, -0.28), Vec3::Y),
        shadow_config,
    ));

    if overview_camera_enabled.0 {
        let focus_height_m = f32::midpoint(prepared.minimum_height_m, prepared.maximum_height_m);
        let camera_position = Vec3::new(-210.0, prepared.maximum_height_m + 125.0, 210.0);
        commands.spawn((
            Name::new("Visual Valley overview camera"),
            VisualValleyOverviewCamera,
            Camera3d::default(),
            AmbientLight {
                color: Color::srgb(0.48, 0.58, 0.72),
                brightness: 95.0,
                ..default()
            },
            Projection::Perspective(PerspectiveProjection {
                fov: 52.0_f32.to_radians(),
                near: 0.25,
                far: 1_600.0,
                ..default()
            }),
            Transform::from_translation(camera_position)
                .looking_at(Vec3::new(0.0, focus_height_m, 0.0), Vec3::Y),
        ));
    }

    info!(
        target: "forge::terrain",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "visual_valley_terrain_materialized",
        scene = %prepared.name,
        source_hash = %prepared.stats.source_hash,
        chunks = prepared.stats.chunks,
        vertices = prepared.stats.vertices,
        triangles = prepared.stats.triangles,
        material_layers = prepared.stats.material_layers,
        lod_zero_chunks = prepared.stats.lod_zero_chunks,
        "Visual Valley terrain materialized from the canonical document"
    );
}

fn visual_valley_shadow_config(
    vegetation_presentation: Option<&VegetationPresentation>,
) -> bevy::light::CascadeShadowConfig {
    let maximum_distance = vegetation_presentation
        .map_or(DEFAULT_VISUAL_VALLEY_SHADOW_DISTANCE_M, |presentation| {
            presentation.0.shadow_distance_m
        });
    let first_cascade_far_bound = (maximum_distance * 0.5).min(24.0);
    let mut builder = CascadeShadowConfigBuilder {
        minimum_distance: 0.0,
        maximum_distance,
        first_cascade_far_bound,
        ..default()
    };
    if first_cascade_far_bound == 0.0 {
        builder.num_cascades = 1;
    }
    builder.build()
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn observe_visual_valley_shadow_config(
    suns: Query<(&DirectionalLight, &bevy::light::CascadeShadowConfig), With<VisualValleySun>>,
    mut stats: ResMut<VisualValleyShadowStats>,
) {
    stats.sun_entities = suns.iter().count();
    let Ok((light, config)) = suns.single() else {
        stats.status = if stats.sun_entities == 0 {
            VisualValleyShadowObservationStatus::UnavailableNoSun
        } else {
            VisualValleyShadowObservationStatus::UnavailableMultipleSuns
        };
        stats.shadow_maps_enabled = None;
        stats.configured_maximum_distance_m = None;
        stats.cascade_bounds_m.clear();
        return;
    };
    stats.status = VisualValleyShadowObservationStatus::ObservedSingleSun;
    stats.shadow_maps_enabled = Some(light.shadow_maps_enabled);
    stats.configured_maximum_distance_m = config.bounds.last().copied();
    stats.cascade_bounds_m.clone_from(&config.bounds);
}

#[cfg(feature = "physics-rapier")]
fn default_visual_valley_player_spawn(
    document: &TerrainDocument,
    profile: &PlayerControllerProfileDocument,
) -> Vec3 {
    let surface_height = TerrainGenerationPlan::from_document(document)
        .and_then(|plan| plan.height_at(DEFAULT_PLAYER_SPAWN_X_M, DEFAULT_PLAYER_SPAWN_Z_M))
        .unwrap_or_else(|error| panic!("cannot sample the Visual Valley player spawn: {error}"));
    let capsule_half_height =
        profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
    Vec3::new(
        DEFAULT_PLAYER_SPAWN_X_M,
        surface_height + capsule_half_height + 0.02,
        DEFAULT_PLAYER_SPAWN_Z_M,
    )
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn draw_visual_valley_chunk_debug(
    debug: Res<VisualValleyDebug>,
    chunks: Query<&VisualValleyChunkDebugBounds>,
    mut gizmos: Gizmos<VisualValleyChunkGizmos>,
) {
    if !debug.chunk_lod_gizmos {
        return;
    }
    for chunk in &chunks {
        let color = match chunk.lod_level {
            0 => Color::srgba(0.20, 0.92, 0.88, 0.52),
            1 => Color::srgba(0.98, 0.76, 0.18, 0.52),
            _ => Color::srgba(0.94, 0.30, 0.22, 0.52),
        };
        gizmos.cube(
            Transform::from_translation(chunk.center).with_scale(chunk.size),
            color,
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;
    #[cfg(feature = "physics-rapier")]
    use bevy::{time::TimeUpdateStrategy, transform::TransformPlugin};
    #[cfg(feature = "physics-rapier")]
    use forge_world::{
        TERRAIN_CHUNKS_PER_AXIS, TERRAIN_EXTENT_M, TERRAIN_HALF_EXTENT_M,
        VisualValleyPlayerControllerRecipe,
    };
    use forge_world::{
        TERRAIN_TRIANGLE_COUNT, TERRAIN_VERTEX_COUNT, embedded_visual_valley_terrain_document,
        embedded_visual_valley_vegetation_document,
    };
    #[cfg(feature = "physics-rapier")]
    use std::time::Duration;

    #[test]
    fn vegetation_profile_bounds_the_visual_valley_shadow_cascades() {
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let mut short_profile = vegetation.presentation;
        short_profile.shadow_distance_m = 12.0;
        let presentation = VegetationPresentation(vegetation.presentation);
        let configured = visual_valley_shadow_config(Some(&presentation));
        let short = visual_valley_shadow_config(Some(&VegetationPresentation(short_profile)));
        let fallback = visual_valley_shadow_config(None);

        assert!((configured.bounds.last().unwrap() - 128.0).abs() <= 0.001);
        assert!(configured.bounds[0] <= 24.0);
        assert!((short.bounds.last().unwrap() - 12.0).abs() <= 0.001);
        assert!(short.bounds[0] < 12.0);
        assert!(
            (fallback.bounds.last().unwrap() - DEFAULT_VISUAL_VALLEY_SHADOW_DISTANCE_M).abs()
                <= 0.001
        );
    }

    #[cfg(feature = "physics-rapier")]
    #[test]
    fn default_player_spawn_is_inside_one_chunk_and_grounds_on_generated_terrain() {
        // A vertical KCC grounding request is projected onto the sampled triangle normal, so a
        // sub-millimetre lateral correction is expected on this intentionally non-flat terrain.
        const MAX_GROUNDING_LATERAL_CORRECTION_M: f32 = 0.001;

        let document = embedded_visual_valley_terrain_document().unwrap();
        let profile = VisualValleyPlayerControllerRecipe.build().unwrap();
        let spawn = default_visual_valley_player_spawn(&document, &profile);
        let chunk_size_m = TERRAIN_EXTENT_M / f32::from(TERRAIN_CHUNKS_PER_AXIS);
        let expected_surface = TerrainGenerationPlan::from_document(&document)
            .and_then(|plan| plan.height_at(spawn.x, spawn.z))
            .unwrap();
        let capsule_half_height =
            profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;

        assert_eq!(spawn.x.to_bits(), DEFAULT_PLAYER_SPAWN_X_M.to_bits());
        assert_eq!(spawn.z.to_bits(), DEFAULT_PLAYER_SPAWN_Z_M.to_bits());
        assert!(spawn.x.rem_euclid(chunk_size_m) > 0.05);
        assert!(spawn.z.rem_euclid(chunk_size_m) > 0.05);
        assert!(spawn.x.abs() < TERRAIN_HALF_EXTENT_M);
        assert!(spawn.z.abs() < TERRAIN_HALF_EXTENT_M);
        assert!((spawn.y - (expected_surface + capsule_half_height + 0.02)).abs() <= f32::EPSILON);

        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .add_plugins(TransformPlugin)
            .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
                1.0 / 60.0,
            )))
            .init_resource::<TerrainRuntimeBindings>()
            .add_plugins(TerrainPhysicsPlugin::new(document))
            .add_plugins(PlayerControllerPlugin::new(profile, spawn));
        app.update();
        for _ in 0..8 {
            let before = app
                .world()
                .resource::<crate::player_controller::PlayerRuntimeSnapshot>()
                .fixed_tick;
            app.update();
            let after = app
                .world()
                .resource::<crate::player_controller::PlayerRuntimeSnapshot>()
                .fixed_tick;
            assert_eq!(
                after,
                before + 1,
                "one manually advanced app update must execute exactly one fixed player tick"
            );
        }

        let snapshot = app
            .world()
            .resource::<crate::player_controller::PlayerRuntimeSnapshot>();
        assert!(
            snapshot.grounded,
            "default Visual Valley spawn did not ground"
        );
        assert_eq!(snapshot.horizontal_velocity, Vec3::ZERO);
        assert!(!snapshot.sliding_down_slope);
        assert!(snapshot.collisions > 0);
        assert!(
            (snapshot.player_position.x - spawn.x).abs() <= MAX_GROUNDING_LATERAL_CORRECTION_M,
            "default spawn drifted on X: spawn={spawn:?}, snapshot={snapshot:?}"
        );
        assert!(
            (snapshot.player_position.z - spawn.z).abs() <= MAX_GROUNDING_LATERAL_CORRECTION_M,
            "default spawn drifted on Z: spawn={spawn:?}, snapshot={snapshot:?}"
        );
    }

    #[cfg(feature = "physics-rapier")]
    #[test]
    fn world_sketch_authored_spawn_grounds_with_real_player_controller() {
        let sketch = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let compiled = forge_world::compile_world_sketch(&sketch).unwrap();
        let profile = VisualValleyPlayerControllerRecipe.build().unwrap();
        let spawn = Vec3::from_array(
            forge_world::world_sketch_player_spawn(&sketch, &compiled.carved_terrain, &profile)
                .unwrap(),
        );
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin))
            .insert_resource(TimeUpdateStrategy::ManualDuration(Duration::from_secs_f64(
                1.0 / 60.0,
            )))
            .init_resource::<TerrainRuntimeBindings>()
            .add_plugins(TerrainPhysicsPlugin::new(compiled.carved_terrain))
            .add_plugins(
                PlayerControllerPlugin::new(profile, spawn).with_initial_camera_target(Vec3::ZERO),
            );
        for _ in 0..60 {
            app.update();
        }
        let snapshot = app
            .world()
            .resource::<crate::player_controller::PlayerRuntimeSnapshot>();
        assert!(snapshot.grounded);
        assert!(snapshot.collisions > 0);
        assert!(!snapshot.sliding_down_slope);
        assert!((snapshot.player_position - spawn).length() < 0.1);
        assert!(snapshot.camera_query_succeeded);
        let forward = Quat::from_rotation_y(snapshot.camera_yaw_radians) * Vec3::NEG_Z;
        let towards_centre = Vec3::new(-spawn.x, 0.0, -spawn.z).normalize();
        assert!(forward.dot(towards_centre) > 0.999);
    }

    #[test]
    fn headless_adapter_prepares_exact_visual_valley_counts() {
        let document = embedded_visual_valley_terrain_document().unwrap();
        let prepared = prepare_visual_valley(&document).unwrap();

        assert_eq!(prepared.stats.chunks, TERRAIN_CHUNK_COUNT);
        assert_eq!(prepared.stats.vertices, TERRAIN_VERTEX_COUNT);
        assert_eq!(prepared.stats.triangles, TERRAIN_TRIANGLE_COUNT);
        assert_eq!(prepared.stats.material_layers, 3);
        assert_eq!(prepared.stats.lod_levels, 1);
        assert_eq!(prepared.stats.lod_zero_chunks, TERRAIN_CHUNK_COUNT);
        assert_eq!(prepared.chunks.len(), TERRAIN_CHUNK_COUNT);
        assert!(prepared.minimum_height_m.is_finite());
        assert!(prepared.maximum_height_m > prepared.minimum_height_m);
    }

    #[test]
    fn every_chunk_mesh_keeps_cpu_and_gpu_data_with_all_required_attributes() {
        let document = embedded_visual_valley_terrain_document().unwrap();
        let prepared = prepare_visual_valley(&document).unwrap();
        let expected_usage = RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD;

        for chunk in &prepared.chunks {
            assert_eq!(chunk.mesh.asset_usage, expected_usage);
            assert_eq!(chunk.mesh.count_vertices(), 1_089);
            assert!(matches!(
                chunk.mesh.attribute(Mesh::ATTRIBUTE_POSITION),
                Some(VertexAttributeValues::Float32x3(values)) if values.len() == 1_089
            ));
            assert!(matches!(
                chunk.mesh.attribute(Mesh::ATTRIBUTE_NORMAL),
                Some(VertexAttributeValues::Float32x3(values)) if values.len() == 1_089
            ));
            assert!(matches!(
                chunk.mesh.attribute(Mesh::ATTRIBUTE_UV_0),
                Some(VertexAttributeValues::Float32x2(values)) if values.len() == 1_089
            ));
            assert!(matches!(
                chunk.mesh.attribute(Mesh::ATTRIBUTE_COLOR),
                Some(VertexAttributeValues::Float32x4(values)) if values.len() == 1_089
            ));
            assert!(matches!(
                chunk.mesh.indices(),
                Some(Indices::U32(values)) if values.len() == 6_144
            ));
        }
    }

    #[test]
    fn color_rgb_contains_normalized_three_layer_weights() {
        let document = embedded_visual_valley_terrain_document().unwrap();
        let prepared = prepare_visual_valley(&document).unwrap();

        for chunk in &prepared.chunks {
            let Some(VertexAttributeValues::Float32x4(colors)) =
                chunk.mesh.attribute(Mesh::ATTRIBUTE_COLOR)
            else {
                panic!("terrain COLOR attribute must be Float32x4");
            };
            for color in colors {
                assert!((color[0] + color[1] + color[2] - 1.0).abs() <= 0.001);
                assert_eq!(color[3].to_bits(), 1.0_f32.to_bits());
            }
        }
    }

    #[test]
    fn prepared_chunks_keep_row_major_coordinates_and_lod_zero() {
        let document = embedded_visual_valley_terrain_document().unwrap();
        let prepared = prepare_visual_valley(&document).unwrap();

        for (index, chunk) in prepared.chunks.iter().enumerate() {
            let expected_x = u16::try_from(index % 8).unwrap();
            let expected_z = u16::try_from(index / 8).unwrap();
            assert_eq!(chunk.marker.coordinate.x, expected_x);
            assert_eq!(chunk.marker.coordinate.z, expected_z);
            assert_eq!(chunk.marker.lod_level, 0);
            assert!(chunk.debug_bounds.size.cmpgt(Vec3::ZERO).all());
        }
    }

    #[test]
    fn original_shader_consumes_color_weights_and_three_pbr_palettes() {
        let shader = include_str!("shaders/visual_valley_terrain.wgsl");

        assert!(shader.contains("normalized_material_weights(in.color.rgb)"));
        assert!(shader.contains("let world_xz = in.world_position.xz"));
        assert!(shader.contains("smoothstep(0.35, 1.25, footprint_m * cells_per_m)"));
        assert!(shader.contains("weights.x * layer_detail.x"));
        assert!(shader.contains("let dry_litter = clamp(0.08 + medium_detail * 0.16, 0.0, 0.16)"));
        assert_eq!(shader.matches("1.0 + broad_detail * 0.12").count(), 3);
        assert!(shader.contains("grass_surface * weights.x * layer_detail.x"));
        assert!(shader.contains("weights.y * layer_detail.y"));
        assert!(shader.contains("weights.z * layer_detail.z"));
        assert!(shader.contains("textureSample(terrain_detail, terrain_detail_sampler"));
        // Wiring checks only: native/Web execution must compile and inspect the shader.
        assert!(shader.contains("let relief_height_m = medium_detail * dot(weights,"));
        assert!(shader.contains("let eased = fraction * fraction * (vec2(3.0) - 2.0 * fraction)"));
        assert!(shader.contains("dpdx(relief_height_m) * reciprocal_x"));
        assert!(shader.contains("length(gradient_numerator) / 0.65"));
        assert!(shader.contains("pbr_input.N = normalize(normal - gradient)"));
        assert!(!shader.contains("pbr_input.world_normal ="));
        assert_eq!(shader.matches("textureSample(").count(), 1);
        assert!(!shader.contains("terrain_value_noise"));
        assert!(shader.contains("grass_color_roughness"));
        assert!(shader.contains("earth_color_roughness"));
        assert!(shader.contains("rock_color_roughness"));
        assert!(shader.contains("pbr_input.material.perceptual_roughness"));
        assert!(shader.contains("pbr_input.material.metallic"));
        assert!(shader.contains("pbr_input.material.reflectance = vec3("));
        assert!(!shader.to_ascii_lowercase().contains("cadle"));
    }

    #[test]
    fn terrain_detail_cache_is_reproducible_and_bounded() {
        let image = terrain_detail_image();
        assert_eq!(image.data, terrain_detail_image().data);
        let data = image.data.as_ref().unwrap();
        assert_eq!(data.len(), 16_384);
        assert_eq!(image.texture_descriptor.format, TextureFormat::R8Unorm);
        assert_eq!(image.texture_descriptor.size.width, 128);
        assert!(
            data.iter()
                .copied()
                .collect::<std::collections::HashSet<_>>()
                .len()
                > 200
        );
        let ImageSampler::Descriptor(sampler) = image.sampler else {
            panic!("explicit sampler required")
        };
        assert_eq!(sampler.address_mode_u, ImageAddressMode::Repeat);
        assert_eq!(sampler.address_mode_v, ImageAddressMode::Repeat);
        assert_eq!(sampler.mag_filter, ImageFilterMode::Linear);
    }

    #[test]
    fn chunk_gizmos_are_opt_in_for_unobstructed_visual_captures() {
        assert!(!VisualValleyDebug::default().chunk_lod_gizmos);
    }
}
