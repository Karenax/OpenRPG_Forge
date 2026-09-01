use crate::{VisualValleyTerrainMaterial, VisualValleyVegetationMaterial};
use bevy::{
    asset::{RenderAssetUsages, embedded_asset},
    light::{NotShadowCaster, NotShadowReceiver},
    mesh::Indices,
    pbr::{ExtendedMaterial, MaterialExtension},
    post_process::bloom::Bloom,
    prelude::*,
    render::{
        render_resource::{AsBindGroup, PrimitiveTopology},
        view::{ColorGrading, ColorGradingGlobal, ColorGradingSection},
    },
    shader::ShaderRef,
};
use forge_project::{
    ColorGradeProfile, VisualQualityPreset, VisualQualityTier, WeatherProfileDocument,
    WeatherProfileError, WeatherQualityTier, WeatherState,
};
use serde::Serialize;
use std::{error::Error, fmt};

const RAIN_SHADER_PATH: &str = "embedded://forge_engine/shaders/visual_valley_rain.wgsl";
const RAIN_VERTICES_PER_DROP: usize = 8;
const RAIN_TRIANGLES_PER_DROP: usize = 4;
const RAIN_STREAK_HALF_WIDTH_M: f32 = 0.009;
const WEATHER_EVIDENCE_SOURCE: &str = "main_world_ecs_and_cpu_material_assets_not_gpu_execution";
const WEATHER_SHADER_TIME_WRAP_SECONDS: f64 = 4_096.0;

const IMPLEMENTED_CAPABILITIES: &[&str] = &[
    "deterministic_clear_rain_clear_transition",
    "single_entity_procedural_rain_batch",
    "shader_wrapped_rain_motion",
    "terrain_wetness_uniform",
    "vegetation_wetness_uniform",
    "bevy_bloom_component",
    "bevy_color_grading_component",
];

const DEFERRED_CAPABILITIES: &[&str] = &[
    "gpu_pipeline_execution_observation",
    "native_visual_capture_and_review",
    "studio_webgpu_execution_and_review",
    "rain_ground_collision_splashes_and_occlusion",
    "wetness_puddles_and_normal_map_response",
];

/// Defensive engine-side allocation ceiling. It covers every authored Visual Valley quality tier.
pub const MAX_WEATHER_BATCH_DROPS: u32 = 16_384;

/// One derived rain mesh, irrespective of the number of authored drops.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct VisualValleyRainBatch;

/// Shader parameters for the single procedural rain batch.
#[derive(Asset, AsBindGroup, Clone, Debug, Reflect)]
pub struct VisualValleyRainExtension {
    /// X/Y are current/previous wrapped time; Z is fall speed; W is streak length.
    #[uniform(100)]
    pub time_fall: Vec4,
    /// XYZ is the camera-relative volume size; W records authored opacity.
    #[uniform(100)]
    pub volume_opacity: Vec4,
    /// XYZ is authored wind velocity in metres per second.
    #[uniform(100)]
    pub wind_velocity: Vec4,
}

impl MaterialExtension for VisualValleyRainExtension {
    fn vertex_shader() -> ShaderRef {
        RAIN_SHADER_PATH.into()
    }
}

pub type VisualValleyRainMaterial = ExtendedMaterial<StandardMaterial, VisualValleyRainExtension>;

/// Mutable execution state. The canonical weather profile remains immutable and renderer-neutral.
#[derive(Clone, Debug, Resource)]
pub struct WeatherRuntimeState {
    target_state: WeatherState,
    current_wetness: f32,
    simulation_seconds: f64,
    previous_simulation_seconds: f64,
}

impl WeatherRuntimeState {
    fn at_state(document: &WeatherProfileDocument, state: WeatherState) -> Self {
        Self {
            target_state: state,
            current_wetness: document.wetness.target_for(state),
            simulation_seconds: 0.0,
            previous_simulation_seconds: 0.0,
        }
    }

    /// Requests a deterministic transition toward clear or rain on subsequent Bevy updates.
    pub const fn request_state(&mut self, target_state: WeatherState) {
        self.target_state = target_state;
    }

    #[must_use]
    pub const fn target_state(&self) -> WeatherState {
        self.target_state
    }

    #[must_use]
    pub const fn current_wetness(&self) -> f32 {
        self.current_wetness
    }

    #[must_use]
    pub const fn simulation_seconds(&self) -> f64 {
        self.simulation_seconds
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum WeatherProjectionStatus {
    #[default]
    NotObserved,
    ObservedCpuProjection,
    InvalidCameraTopology,
    IncompleteRainBatch,
    IncompleteMaterialProjection,
}

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
pub struct WeatherRainStats {
    pub quality: Option<WeatherQualityTier>,
    pub requested_drops: u32,
    pub generated_drops: u32,
    pub batch_entities: usize,
    pub per_drop_entities: usize,
    pub vertices: usize,
    pub triangles: usize,
    pub visible: bool,
    pub camera_followed: bool,
    pub shader_animation_uniform_updated: bool,
    pub rendered_opacity: f32,
}

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
pub struct WeatherWetnessStats {
    pub current: f32,
    pub target: f32,
    pub terrain_materials: usize,
    pub vegetation_materials: usize,
    pub terrain_uniforms_match: bool,
    pub vegetation_uniforms_match: bool,
    /// Populated only when the scalar has reached the authored clear target.
    pub clear_target_restored_exactly: Option<bool>,
}

#[derive(Clone, Debug, Default, PartialEq, Serialize)]
pub struct WeatherPostProcessingStats {
    pub camera_3d_entities: usize,
    pub active_camera_3d_entities: usize,
    pub bloom_requested: bool,
    pub bloom_component_present: bool,
    pub color_grading_component_present: bool,
    pub mapped_exposure_ev: Option<f32>,
    pub mapped_temperature: Option<f32>,
    pub mapped_tint: Option<f32>,
    pub mapped_post_saturation: Option<f32>,
    pub mapped_contrast: Option<f32>,
    pub mapped_gamma: Option<f32>,
}

/// Honest CPU/ECS evidence. It never claims that Bevy compiled or executed the GPU pipelines.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
pub struct WeatherProjectionStats {
    pub source_hash: String,
    pub status: WeatherProjectionStatus,
    pub evidence_source: &'static str,
    pub target_state: WeatherState,
    pub gpu_execution_observed: bool,
    pub rain: WeatherRainStats,
    pub wetness: WeatherWetnessStats,
    pub post_processing: WeatherPostProcessingStats,
    pub implemented_capabilities: &'static [&'static str],
    pub deferred_capabilities: &'static [&'static str],
}

impl WeatherProjectionStats {
    fn unobserved(source_hash: String, state: &WeatherRuntimeState, plan: RainBatchPlan) -> Self {
        Self {
            source_hash,
            status: WeatherProjectionStatus::NotObserved,
            evidence_source: WEATHER_EVIDENCE_SOURCE,
            target_state: state.target_state,
            gpu_execution_observed: false,
            rain: WeatherRainStats {
                quality: Some(plan.quality),
                requested_drops: plan.drops,
                generated_drops: plan.drops,
                vertices: plan.vertices,
                triangles: plan.triangles,
                ..default()
            },
            wetness: WeatherWetnessStats {
                current: state.current_wetness,
                target: state.current_wetness,
                ..default()
            },
            post_processing: WeatherPostProcessingStats::default(),
            implemented_capabilities: IMPLEMENTED_CAPABILITIES,
            deferred_capabilities: DEFERRED_CAPABILITIES,
        }
    }
}

/// Typed deterministic preparation failures, kept separate from GPU/runtime qualification.
#[derive(Debug)]
pub enum WeatherAdapterError {
    Profile(WeatherProfileError),
    RainDropBudgetExceeded { requested: u32, maximum: u32 },
    CountOverflow(&'static str),
    InvalidRainDirection,
}

impl fmt::Display for WeatherAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Profile(error) => write!(formatter, "invalid weather profile: {error}"),
            Self::RainDropBudgetExceeded { requested, maximum } => write!(
                formatter,
                "rain batch requests {requested} drops, above the engine budget {maximum}"
            ),
            Self::CountOverflow(field) => {
                write!(formatter, "rain batch count `{field}` overflowed")
            }
            Self::InvalidRainDirection => write!(formatter, "rain direction is not finite"),
        }
    }
}

impl Error for WeatherAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Profile(error) => Some(error),
            Self::RainDropBudgetExceeded { .. }
            | Self::CountOverflow(_)
            | Self::InvalidRainDirection => None,
        }
    }
}

impl From<WeatherProfileError> for WeatherAdapterError {
    fn from(value: WeatherProfileError) -> Self {
        Self::Profile(value)
    }
}

/// Bevy projection for the versioned clear/rain profile. No application or runtime dependency is
/// introduced; native and WASM callers install the same plugin.
pub struct VisualValleyWeatherPlugin {
    document: WeatherProfileDocument,
    initial_state: WeatherState,
    quality: Option<VisualQualityPreset>,
    quality_tier: Option<VisualQualityTier>,
}

impl VisualValleyWeatherPlugin {
    #[must_use]
    pub fn new(document: WeatherProfileDocument) -> Self {
        let initial_state = document.initial_state;
        Self {
            document,
            initial_state,
            quality: None,
            quality_tier: None,
        }
    }

    /// Starts fully at the selected state. Later changes use [`WeatherRuntimeState::request_state`].
    #[must_use]
    pub const fn with_initial_state(mut self, initial_state: WeatherState) -> Self {
        self.initial_state = initial_state;
        self
    }

    /// Applies startup mesh and post-processing budgets from a validated quality preset.
    #[must_use]
    pub fn with_quality_preset(
        mut self,
        preset: VisualQualityPreset,
        tier: VisualQualityTier,
    ) -> Self {
        self.quality = Some(preset);
        self.quality_tier = Some(tier);
        self
    }
}

impl Plugin for VisualValleyWeatherPlugin {
    fn build(&self, app: &mut App) {
        let mut projection_document = self.document.clone();
        if let Some(quality) = &self.quality {
            let tier = weather_quality_tier(
                self.quality_tier
                    .expect("a weather quality preset must carry its source tier"),
            );
            projection_document.rain.quality = tier;
            match tier {
                WeatherQualityTier::Low => {
                    projection_document.rain.drop_budgets.low = quality.rain.drop_budget;
                }
                WeatherQualityTier::Medium => {
                    projection_document.rain.drop_budgets.medium = quality.rain.drop_budget;
                }
                WeatherQualityTier::High => {
                    projection_document.rain.drop_budgets.high = quality.rain.drop_budget;
                }
                WeatherQualityTier::Ultra => {
                    projection_document.rain.drop_budgets.ultra = quality.rain.drop_budget;
                }
            }
            projection_document.post_processing.bloom.enabled = quality.post_process.bloom_enabled;
        }
        let prepared = prepare_rain_batch(&projection_document)
            .unwrap_or_else(|error| panic!("weather plugin requires a supported profile: {error}"));
        let source_hash = self
            .document
            .content_hash()
            .expect("a validated weather profile must have a canonical hash")
            .to_string();
        let runtime_state = WeatherRuntimeState::at_state(&projection_document, self.initial_state);
        let initial_stats =
            WeatherProjectionStats::unobserved(source_hash.clone(), &runtime_state, prepared.plan);

        embedded_asset!(app, "shaders/visual_valley_rain.wgsl");
        app.insert_resource(VisualValleyWeatherProfile {
            document: projection_document,
            source_hash,
        })
        .insert_resource(runtime_state)
        .insert_resource(prepared.plan)
        .insert_resource(PreparedRainBatchResource(Some(prepared)))
        .insert_resource(initial_stats)
        .init_resource::<WeatherCameraBinding>()
        .add_plugins(MaterialPlugin::<VisualValleyRainMaterial>::default())
        .add_systems(Startup, spawn_rain_batch)
        .add_systems(
            Update,
            (
                advance_weather_runtime,
                project_rain_batch,
                project_wetness_materials,
                project_post_processing,
            )
                .chain(),
        )
        .add_systems(PostUpdate, observe_weather_projection);
    }
}

const fn weather_quality_tier(tier: VisualQualityTier) -> WeatherQualityTier {
    match tier {
        VisualQualityTier::Low => WeatherQualityTier::Low,
        VisualQualityTier::Medium => WeatherQualityTier::Medium,
        VisualQualityTier::High => WeatherQualityTier::High,
        VisualQualityTier::Ultra => WeatherQualityTier::Ultra,
    }
}

#[derive(Clone, Debug, Resource)]
struct VisualValleyWeatherProfile {
    document: WeatherProfileDocument,
    source_hash: String,
}

#[derive(Clone, Copy, Debug, Resource)]
struct RainBatchPlan {
    quality: WeatherQualityTier,
    drops: u32,
    vertices: usize,
    triangles: usize,
}

struct PreparedRainBatch {
    mesh: Mesh,
    plan: RainBatchPlan,
}

#[derive(Resource)]
struct PreparedRainBatchResource(Option<PreparedRainBatch>);

#[derive(Resource)]
struct RainBatchMaterial(Handle<VisualValleyRainMaterial>);

#[derive(Default, Resource)]
struct WeatherCameraBinding(Option<Entity>);

fn prepare_rain_batch(
    document: &WeatherProfileDocument,
) -> Result<PreparedRainBatch, WeatherAdapterError> {
    document.validate()?;
    let drops = document
        .rain
        .drop_budgets
        .for_quality(document.rain.quality);
    if drops > MAX_WEATHER_BATCH_DROPS {
        return Err(WeatherAdapterError::RainDropBudgetExceeded {
            requested: drops,
            maximum: MAX_WEATHER_BATCH_DROPS,
        });
    }
    let drops_usize =
        usize::try_from(drops).map_err(|_| WeatherAdapterError::CountOverflow("drops"))?;
    let vertices = drops_usize
        .checked_mul(RAIN_VERTICES_PER_DROP)
        .ok_or(WeatherAdapterError::CountOverflow("vertices"))?;
    let triangles = drops_usize
        .checked_mul(RAIN_TRIANGLES_PER_DROP)
        .ok_or(WeatherAdapterError::CountOverflow("triangles"))?;
    let indices = triangles
        .checked_mul(3)
        .ok_or(WeatherAdapterError::CountOverflow("indices"))?;

    let volume = Vec3::new(
        document.rain.volume_size_m.x,
        document.rain.volume_size_m.y,
        document.rain.volume_size_m.z,
    );
    let wind = Vec3::new(
        document.rain.wind_velocity_mps.x,
        document.rain.wind_velocity_mps.y,
        document.rain.wind_velocity_mps.z,
    );
    let rain_velocity = wind - Vec3::Y * document.rain.fall_speed_mps;
    let direction = rain_velocity.normalize_or_zero();
    if !direction.is_finite() || direction.length_squared() <= f32::EPSILON {
        return Err(WeatherAdapterError::InvalidRainDirection);
    }
    let half_streak = direction * (document.rain.streak_length_m * 0.5);
    let side_x = Vec3::X * RAIN_STREAK_HALF_WIDTH_M;
    let side_z = Vec3::Z * RAIN_STREAK_HALF_WIDTH_M;

    let mut positions = Vec::with_capacity(vertices);
    let mut normals = Vec::with_capacity(vertices);
    let mut uv_0 = Vec::with_capacity(vertices);
    let mut uv_1 = Vec::with_capacity(vertices);
    let mut mesh_indices = Vec::with_capacity(indices);

    for drop in 0..drops {
        let normalized = Vec3::new(
            deterministic_unit(document.seed, drop, 0),
            deterministic_unit(document.seed, drop, 1),
            deterministic_unit(document.seed, drop, 2),
        );
        let center = (normalized - Vec3::splat(0.5)) * volume;
        append_rain_quad(
            &mut positions,
            &mut normals,
            &mut uv_0,
            &mut uv_1,
            &mut mesh_indices,
            center,
            normalized,
            half_streak,
            side_x,
        )?;
        append_rain_quad(
            &mut positions,
            &mut normals,
            &mut uv_0,
            &mut uv_1,
            &mut mesh_indices,
            center,
            normalized,
            half_streak,
            side_z,
        )?;
    }

    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, normals);
    mesh.insert_attribute(Mesh::ATTRIBUTE_UV_0, uv_0);
    mesh.insert_attribute(Mesh::ATTRIBUTE_UV_1, uv_1);
    mesh.insert_indices(Indices::U32(mesh_indices));
    Ok(PreparedRainBatch {
        mesh,
        plan: RainBatchPlan {
            quality: document.rain.quality,
            drops,
            vertices,
            triangles,
        },
    })
}

#[allow(
    clippy::too_many_arguments,
    reason = "the helper appends four synchronized mesh attribute streams plus indices"
)]
fn append_rain_quad(
    positions: &mut Vec<[f32; 3]>,
    normals: &mut Vec<[f32; 3]>,
    uv_0: &mut Vec<[f32; 2]>,
    uv_1: &mut Vec<[f32; 2]>,
    indices: &mut Vec<u32>,
    center: Vec3,
    normalized_center: Vec3,
    half_streak: Vec3,
    half_side: Vec3,
) -> Result<(), WeatherAdapterError> {
    let base = u32::try_from(positions.len())
        .map_err(|_| WeatherAdapterError::CountOverflow("vertex_index"))?;
    let normal = half_streak.cross(half_side).normalize_or_zero();
    if !normal.is_finite() || normal.length_squared() <= f32::EPSILON {
        return Err(WeatherAdapterError::InvalidRainDirection);
    }
    for position in [
        center - half_streak - half_side,
        center + half_streak - half_side,
        center + half_streak + half_side,
        center - half_streak + half_side,
    ] {
        positions.push(position.to_array());
        normals.push(normal.to_array());
        uv_0.push([normalized_center.x, normalized_center.z]);
        uv_1.push([normalized_center.y, 0.0]);
    }
    indices.extend_from_slice(&[base, base + 1, base + 2, base, base + 2, base + 3]);
    Ok(())
}

#[allow(
    clippy::cast_precision_loss,
    reason = "the selected 24-bit mantissa is exactly representable as f32"
)]
fn deterministic_unit(seed: u64, drop: u32, stream: u32) -> f32 {
    let mut value = seed
        .wrapping_add(u64::from(drop).wrapping_mul(0x9e37_79b9_7f4a_7c15))
        .wrapping_add(u64::from(stream).wrapping_mul(0xbf58_476d_1ce4_e5b9));
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^= value >> 31;
    let mantissa = u32::try_from(value >> 40).unwrap_or(0);
    mantissa as f32 / 16_777_216.0
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn spawn_rain_batch(
    mut commands: Commands,
    mut prepared: ResMut<PreparedRainBatchResource>,
    profile: Res<VisualValleyWeatherProfile>,
    state: Res<WeatherRuntimeState>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<VisualValleyRainMaterial>>,
) {
    let prepared = prepared
        .0
        .take()
        .expect("rain batch preparation is consumed exactly once at startup");
    let precipitation = precipitation_factor(&profile.document, state.current_wetness);
    let rain = &profile.document.rain;
    let material = materials.add(VisualValleyRainMaterial {
        base: StandardMaterial {
            base_color: Color::srgba(
                rain.color_srgb.red,
                rain.color_srgb.green,
                rain.color_srgb.blue,
                rain.opacity * precipitation,
            ),
            alpha_mode: AlphaMode::Blend,
            unlit: true,
            double_sided: true,
            cull_mode: None,
            ..default()
        },
        extension: rain_extension(&profile.document, &state),
    });
    let mesh = meshes.add(prepared.mesh);
    commands.spawn((
        Name::new(format!(
            "Visual Valley procedural rain · {} drops · one batch",
            prepared.plan.drops
        )),
        VisualValleyRainBatch,
        Mesh3d(mesh),
        MeshMaterial3d(material.clone()),
        Transform::IDENTITY,
        if precipitation > 0.0 {
            Visibility::Visible
        } else {
            Visibility::Hidden
        },
        NotShadowCaster,
        NotShadowReceiver,
    ));
    commands.insert_resource(RainBatchMaterial(material));
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn advance_weather_runtime(
    time: Res<Time>,
    profile: Res<VisualValleyWeatherProfile>,
    mut state: ResMut<WeatherRuntimeState>,
) {
    advance_runtime(&profile.document, &mut state, time.delta_secs());
}

fn advance_runtime(
    document: &WeatherProfileDocument,
    state: &mut WeatherRuntimeState,
    delta_seconds: f32,
) {
    if !delta_seconds.is_finite() || delta_seconds < 0.0 {
        return;
    }
    state.previous_simulation_seconds = state.simulation_seconds;
    state.simulation_seconds += f64::from(delta_seconds);
    state.current_wetness = document
        .advance_wetness(state.current_wetness, state.target_state, delta_seconds)
        .expect("validated profile and finite Bevy delta must produce a weather transition");
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn project_rain_batch(
    profile: Res<VisualValleyWeatherProfile>,
    state: Res<WeatherRuntimeState>,
    material_handle: Option<Res<RainBatchMaterial>>,
    mut materials: ResMut<Assets<VisualValleyRainMaterial>>,
    cameras: Query<(&Camera, &GlobalTransform), With<Camera3d>>,
    mut batches: Query<(&mut Transform, &mut Visibility), With<VisualValleyRainBatch>>,
) {
    let precipitation = precipitation_factor(&profile.document, state.current_wetness);
    if let Some(material_handle) = material_handle
        && let Some(mut material) = materials.get_mut(&material_handle.0)
    {
        let extension = rain_extension(&profile.document, &state);
        material.extension = extension;
        let rain = &profile.document.rain;
        material.base.base_color = Color::srgba(
            rain.color_srgb.red,
            rain.color_srgb.green,
            rain.color_srgb.blue,
            rain.opacity * precipitation,
        );
    }

    let mut active_cameras = cameras
        .iter()
        .filter(|(camera, _)| camera.is_active)
        .map(|(_, transform)| transform.translation());
    let active_camera = active_cameras.next();
    let single_active_camera = active_camera.filter(|_| active_cameras.next().is_none());
    for (mut transform, mut visibility) in &mut batches {
        *visibility = if precipitation > 0.0 {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
        if let Some(camera_translation) = single_active_camera {
            transform.translation =
                camera_translation + Vec3::Y * (profile.document.rain.volume_size_m.y * 0.25);
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn project_wetness_materials(
    profile: Res<VisualValleyWeatherProfile>,
    state: Res<WeatherRuntimeState>,
    terrain_materials: Option<ResMut<Assets<VisualValleyTerrainMaterial>>>,
    vegetation_materials: Option<ResMut<Assets<VisualValleyVegetationMaterial>>>,
) {
    let uniform = wetness_uniform(&profile.document, state.current_wetness);
    if let Some(mut materials) = terrain_materials {
        for (_, material) in materials.iter_mut() {
            material.extension.wetness_parameters = uniform;
        }
    }
    if let Some(mut materials) = vegetation_materials {
        for (_, material) in materials.iter_mut() {
            material.extension.wetness_parameters = uniform;
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn project_post_processing(
    mut commands: Commands,
    profile: Res<VisualValleyWeatherProfile>,
    state: Res<WeatherRuntimeState>,
    cameras: Query<(Entity, &Camera), With<Camera3d>>,
    mut binding: ResMut<WeatherCameraBinding>,
) {
    let mut active_cameras = cameras
        .iter()
        .filter_map(|(entity, camera)| camera.is_active.then_some(entity));
    let active_camera = active_cameras.next();
    let single_active_camera = active_camera.filter(|_| active_cameras.next().is_none());

    if binding.0 != single_active_camera {
        if let Some(previous) = binding.0 {
            commands.entity(previous).remove::<(Bloom, ColorGrading)>();
        }
        binding.0 = single_active_camera;
    }
    let Some(camera) = single_active_camera else {
        return;
    };

    let precipitation = precipitation_factor(&profile.document, state.current_wetness);
    let grade = interpolated_grade(&profile.document, precipitation);
    commands
        .entity(camera)
        .insert(color_grading_component(grade));
    if profile.document.post_processing.bloom.enabled {
        let bloom = profile.document.post_processing.bloom;
        commands.entity(camera).insert(Bloom {
            intensity: bloom.intensity,
            low_frequency_boost: bloom.low_frequency_boost,
            high_pass_frequency: bloom.high_pass_frequency,
            ..Bloom::NATURAL
        });
    } else {
        commands.entity(camera).remove::<Bloom>();
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    reason = "the observer records independent ECS and material evidence without GPU claims"
)]
fn observe_weather_projection(
    profile: Res<VisualValleyWeatherProfile>,
    runtime: Res<WeatherRuntimeState>,
    plan: Res<RainBatchPlan>,
    cameras: Query<
        (
            &Camera,
            &GlobalTransform,
            Option<&Bloom>,
            Option<&ColorGrading>,
        ),
        With<Camera3d>,
    >,
    batches: Query<(&Transform, &Visibility), With<VisualValleyRainBatch>>,
    rain_materials: Res<Assets<VisualValleyRainMaterial>>,
    terrain_materials: Option<Res<Assets<VisualValleyTerrainMaterial>>>,
    vegetation_materials: Option<Res<Assets<VisualValleyVegetationMaterial>>>,
    mut projection: ResMut<WeatherProjectionStats>,
) {
    let camera_3d_entities = cameras.iter().count();
    let active = cameras
        .iter()
        .filter(|(camera, ..)| camera.is_active)
        .collect::<Vec<_>>();
    let active_camera_3d_entities = active.len();
    let batch_entities = batches.iter().count();
    let batch = batches.iter().next();
    let expected_uniform = wetness_uniform(&profile.document, runtime.current_wetness);
    let terrain_materials = terrain_materials.as_deref();
    let vegetation_materials = vegetation_materials.as_deref();
    let terrain_count = terrain_materials.map_or(0, Assets::len);
    let vegetation_count = vegetation_materials.map_or(0, Assets::len);
    let terrain_uniforms_match = terrain_materials.is_some_and(|materials| {
        materials
            .iter()
            .all(|(_, material)| material.extension.wetness_parameters == expected_uniform)
    }) && terrain_count > 0;
    let vegetation_uniforms_match = vegetation_materials.is_some_and(|materials| {
        materials
            .iter()
            .all(|(_, material)| material.extension.wetness_parameters == expected_uniform)
    }) && vegetation_count > 0;
    let precipitation = precipitation_factor(&profile.document, runtime.current_wetness);
    let expected_rain_extension = rain_extension(&profile.document, &runtime);
    let shader_animation_uniform_updated = rain_materials.iter().any(|(_, material)| {
        material.extension.time_fall == expected_rain_extension.time_fall
            && material.extension.volume_opacity == expected_rain_extension.volume_opacity
            && material.extension.wind_velocity == expected_rain_extension.wind_velocity
    });
    let camera_followed = active.first().zip(batch).is_some_and(
        |((_, camera_transform, _, _), (batch_transform, _))| {
            batch_transform.translation
                == camera_transform.translation()
                    + Vec3::Y * (profile.document.rain.volume_size_m.y * 0.25)
        },
    ) && active_camera_3d_entities == 1;
    let grade = active.first().and_then(|(_, _, _, grade)| grade.as_deref());
    let section = grade.map(|grade| &grade.midtones);
    let at_clear =
        runtime.current_wetness.to_bits() == profile.document.wetness.clear_target.to_bits();
    let restored_exactly = at_clear.then_some(
        terrain_uniforms_match
            && vegetation_uniforms_match
            && expected_uniform.x.to_bits() == profile.document.wetness.clear_target.to_bits(),
    );
    let status = if active_camera_3d_entities != 1 {
        WeatherProjectionStatus::InvalidCameraTopology
    } else if batch_entities != 1 || !shader_animation_uniform_updated {
        WeatherProjectionStatus::IncompleteRainBatch
    } else if !terrain_uniforms_match || !vegetation_uniforms_match {
        WeatherProjectionStatus::IncompleteMaterialProjection
    } else {
        WeatherProjectionStatus::ObservedCpuProjection
    };

    *projection = WeatherProjectionStats {
        source_hash: profile.source_hash.clone(),
        status,
        evidence_source: WEATHER_EVIDENCE_SOURCE,
        target_state: runtime.target_state,
        gpu_execution_observed: false,
        rain: WeatherRainStats {
            quality: Some(plan.quality),
            requested_drops: plan.drops,
            generated_drops: plan.drops,
            batch_entities,
            per_drop_entities: 0,
            vertices: plan.vertices,
            triangles: plan.triangles,
            visible: batch.is_some_and(|(_, visibility)| *visibility == Visibility::Visible),
            camera_followed,
            shader_animation_uniform_updated,
            rendered_opacity: profile.document.rain.opacity * precipitation,
        },
        wetness: WeatherWetnessStats {
            current: runtime.current_wetness,
            target: profile.document.wetness.target_for(runtime.target_state),
            terrain_materials: terrain_count,
            vegetation_materials: vegetation_count,
            terrain_uniforms_match,
            vegetation_uniforms_match,
            clear_target_restored_exactly: restored_exactly,
        },
        post_processing: WeatherPostProcessingStats {
            camera_3d_entities,
            active_camera_3d_entities,
            bloom_requested: profile.document.post_processing.bloom.enabled,
            bloom_component_present: active
                .first()
                .is_some_and(|(_, _, bloom, _)| bloom.is_some()),
            color_grading_component_present: grade.is_some(),
            mapped_exposure_ev: grade.map(|grade| grade.global.exposure),
            mapped_temperature: grade.map(|grade| grade.global.temperature),
            mapped_tint: grade.map(|grade| grade.global.tint),
            mapped_post_saturation: grade.map(|grade| grade.global.post_saturation),
            mapped_contrast: section.map(|section| section.contrast),
            mapped_gamma: section.map(|section| section.gamma),
        },
        implemented_capabilities: IMPLEMENTED_CAPABILITIES,
        deferred_capabilities: DEFERRED_CAPABILITIES,
    };
}

fn precipitation_factor(document: &WeatherProfileDocument, wetness: f32) -> f32 {
    let span = document.wetness.rain_target - document.wetness.clear_target;
    ((wetness - document.wetness.clear_target) / span).clamp(0.0, 1.0)
}

fn wetness_uniform(document: &WeatherProfileDocument, wetness: f32) -> Vec4 {
    Vec4::new(
        wetness,
        document.wetness.full_wet_roughness_multiplier,
        document.wetness.full_wet_albedo_multiplier,
        0.0,
    )
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "shader time is deliberately wrapped to a small exactly bounded interval"
)]
fn shader_time(seconds: f64) -> f32 {
    seconds.rem_euclid(WEATHER_SHADER_TIME_WRAP_SECONDS) as f32
}

fn rain_extension(
    document: &WeatherProfileDocument,
    state: &WeatherRuntimeState,
) -> VisualValleyRainExtension {
    let rain = &document.rain;
    VisualValleyRainExtension {
        time_fall: Vec4::new(
            shader_time(state.simulation_seconds),
            shader_time(state.previous_simulation_seconds),
            rain.fall_speed_mps,
            rain.streak_length_m,
        ),
        volume_opacity: Vec4::new(
            rain.volume_size_m.x,
            rain.volume_size_m.y,
            rain.volume_size_m.z,
            rain.opacity,
        ),
        wind_velocity: Vec4::new(
            rain.wind_velocity_mps.x,
            rain.wind_velocity_mps.y,
            rain.wind_velocity_mps.z,
            0.0,
        ),
    }
}

fn interpolated_grade(document: &WeatherProfileDocument, precipitation: f32) -> ColorGradeProfile {
    let clear = document.post_processing.clear_grade;
    let rain = document.post_processing.rain_grade;
    if precipitation <= 0.0 {
        return clear;
    }
    if precipitation >= 1.0 {
        return rain;
    }
    ColorGradeProfile {
        exposure_ev: clear.exposure_ev.lerp(rain.exposure_ev, precipitation),
        temperature: clear.temperature.lerp(rain.temperature, precipitation),
        tint: clear.tint.lerp(rain.tint, precipitation),
        saturation: clear.saturation.lerp(rain.saturation, precipitation),
        contrast: clear.contrast.lerp(rain.contrast, precipitation),
        gamma: clear.gamma.lerp(rain.gamma, precipitation),
    }
}

fn color_grading_component(grade: ColorGradeProfile) -> ColorGrading {
    ColorGrading::with_identical_sections(
        ColorGradingGlobal {
            exposure: grade.exposure_ev,
            temperature: grade.temperature,
            tint: grade.tint,
            post_saturation: grade.saturation,
            ..default()
        },
        ColorGradingSection {
            contrast: grade.contrast,
            gamma: grade.gamma,
            ..default()
        },
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::AssetPlugin, mesh::VertexAttributeValues};
    use forge_world::embedded_visual_valley_weather_profile;

    fn profile() -> WeatherProfileDocument {
        embedded_visual_valley_weather_profile().unwrap()
    }

    fn positions(mesh: &Mesh) -> &Vec<[f32; 3]> {
        match mesh.attribute(Mesh::ATTRIBUTE_POSITION).unwrap() {
            VertexAttributeValues::Float32x3(values) => values,
            other => panic!("unexpected position format: {other:?}"),
        }
    }

    #[test]
    fn high_profile_builds_one_deterministic_batch_without_drop_entities() {
        let first = prepare_rain_batch(&profile()).unwrap();
        let replay = prepare_rain_batch(&profile()).unwrap();

        assert_eq!(first.plan.drops, 8_192);
        assert_eq!(first.plan.vertices, 8_192 * RAIN_VERTICES_PER_DROP);
        assert_eq!(first.plan.triangles, 8_192 * RAIN_TRIANGLES_PER_DROP);
        assert_eq!(first.mesh.count_vertices(), first.plan.vertices);
        assert_eq!(first.mesh.indices(), replay.mesh.indices());
        assert_eq!(positions(&first.mesh), positions(&replay.mesh));
        assert_eq!(
            first.mesh.attribute(Mesh::ATTRIBUTE_UV_0),
            replay.mesh.attribute(Mesh::ATTRIBUTE_UV_0)
        );
        assert_eq!(
            first.mesh.attribute(Mesh::ATTRIBUTE_UV_1),
            replay.mesh.attribute(Mesh::ATTRIBUTE_UV_1)
        );
    }

    #[test]
    fn changing_only_the_seed_changes_geometry_but_not_budget() {
        let original = profile();
        let mut changed = original.clone();
        changed.seed = changed.seed.wrapping_add(1);
        let first = prepare_rain_batch(&original).unwrap();
        let second = prepare_rain_batch(&changed).unwrap();

        assert_eq!(first.plan.drops, second.plan.drops);
        assert_ne!(positions(&first.mesh), positions(&second.mesh));
    }

    #[test]
    fn fixed_steps_replay_and_restore_the_exact_clear_target() {
        let profile = profile();
        let mut first = WeatherRuntimeState::at_state(&profile, WeatherState::Clear);
        let mut replay = first.clone();
        first.request_state(WeatherState::Rain);
        replay.request_state(WeatherState::Rain);
        for _ in 0..60 {
            advance_runtime(&profile, &mut first, 0.1);
            advance_runtime(&profile, &mut replay, 0.1);
        }
        assert_eq!(
            first.current_wetness.to_bits(),
            profile.wetness.rain_target.to_bits()
        );
        assert_eq!(
            first.current_wetness.to_bits(),
            replay.current_wetness.to_bits()
        );

        first.request_state(WeatherState::Clear);
        replay.request_state(WeatherState::Clear);
        for _ in 0..120 {
            advance_runtime(&profile, &mut first, 0.1);
            advance_runtime(&profile, &mut replay, 0.1);
        }
        assert_eq!(
            first.current_wetness.to_bits(),
            profile.wetness.clear_target.to_bits()
        );
        assert_eq!(
            first.current_wetness.to_bits(),
            replay.current_wetness.to_bits()
        );
        assert_eq!(
            first.simulation_seconds.to_bits(),
            replay.simulation_seconds.to_bits()
        );
    }

    #[test]
    fn initial_state_builder_contract_can_start_fully_clear_or_raining() {
        let profile = profile();
        let clear = WeatherRuntimeState::at_state(&profile, WeatherState::Clear);
        let rain = WeatherRuntimeState::at_state(&profile, WeatherState::Rain);

        assert_eq!(clear.target_state(), WeatherState::Clear);
        assert_eq!(
            clear.current_wetness().to_bits(),
            profile.wetness.clear_target.to_bits()
        );
        assert_eq!(rain.target_state(), WeatherState::Rain);
        assert_eq!(
            rain.current_wetness().to_bits(),
            profile.wetness.rain_target.to_bits()
        );
        let _plugin =
            VisualValleyWeatherPlugin::new(profile).with_initial_state(WeatherState::Rain);
    }

    #[test]
    fn wetness_uniform_is_reversible_without_mutating_authored_palettes() {
        let profile = profile();
        let mut terrain = crate::VisualValleyTerrainExtension::default();
        let mut vegetation = crate::VisualValleyVegetationExtension::default();
        let terrain_palette = [
            terrain.grass_color_roughness,
            terrain.earth_color_roughness,
            terrain.rock_color_roughness,
        ];
        let vegetation_palette = [
            vegetation.primary_color_roughness,
            vegetation.secondary_color_roughness,
            vegetation.bark_color_roughness,
        ];

        let rain = wetness_uniform(&profile, profile.wetness.rain_target);
        terrain.wetness_parameters = rain;
        vegetation.wetness_parameters = rain;
        assert!(terrain.wetness_parameters.x > 0.0);
        assert!(vegetation.wetness_parameters.x > 0.0);

        let clear = wetness_uniform(&profile, profile.wetness.clear_target);
        terrain.wetness_parameters = clear;
        vegetation.wetness_parameters = clear;
        assert_eq!(terrain.wetness_parameters.x.to_bits(), 0.0_f32.to_bits());
        assert_eq!(vegetation.wetness_parameters.x.to_bits(), 0.0_f32.to_bits());
        assert_eq!(
            terrain_palette,
            [
                terrain.grass_color_roughness,
                terrain.earth_color_roughness,
                terrain.rock_color_roughness,
            ]
        );
        assert_eq!(
            vegetation_palette,
            [
                vegetation.primary_color_roughness,
                vegetation.secondary_color_roughness,
                vegetation.bark_color_roughness,
            ]
        );
    }

    #[test]
    fn color_grade_mapping_is_bounded_and_returns_exact_clear_values() {
        let profile = profile();
        let clear = interpolated_grade(&profile, 0.0);
        let rain = interpolated_grade(&profile, 1.0);
        assert_eq!(clear, profile.post_processing.clear_grade);
        assert_eq!(rain, profile.post_processing.rain_grade);

        let component = color_grading_component(clear);
        assert_eq!(
            component.global.exposure.to_bits(),
            clear.exposure_ev.to_bits()
        );
        assert_eq!(
            component.global.post_saturation.to_bits(),
            clear.saturation.to_bits()
        );
        assert_eq!(
            component.midtones.contrast.to_bits(),
            clear.contrast.to_bits()
        );
        assert_eq!(component.midtones.gamma.to_bits(), clear.gamma.to_bits());
        assert_eq!(component.shadows, component.midtones);
        assert_eq!(component.highlights, component.midtones);
    }

    #[test]
    fn metrics_never_claim_gpu_execution_and_list_deferred_proofs() {
        let profile = profile();
        let prepared = prepare_rain_batch(&profile).unwrap();
        let runtime = WeatherRuntimeState::at_state(&profile, WeatherState::Clear);
        let projection = WeatherProjectionStats::unobserved(
            profile.content_hash().unwrap().to_string(),
            &runtime,
            prepared.plan,
        );

        assert!(!projection.gpu_execution_observed);
        assert_eq!(projection.rain.per_drop_entities, 0);
        assert!(
            projection
                .deferred_capabilities
                .contains(&"studio_webgpu_execution_and_review")
        );
        assert_eq!(projection.evidence_source, WEATHER_EVIDENCE_SOURCE);
    }

    #[test]
    fn headless_plugin_projects_one_rain_batch_materials_and_camera_components() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<VisualValleyTerrainMaterial>();
        app.init_asset::<VisualValleyVegetationMaterial>();
        let _terrain_material = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyTerrainMaterial>>()
            .add(VisualValleyTerrainMaterial {
                base: StandardMaterial::default(),
                extension: crate::VisualValleyTerrainExtension::default(),
            });
        let _vegetation_material = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .add(VisualValleyVegetationMaterial {
                base: StandardMaterial::default(),
                extension: crate::VisualValleyVegetationExtension::default(),
            });
        app.world_mut().spawn((
            Camera3d::default(),
            Camera::default(),
            Transform::from_xyz(3.0, 4.0, 5.0),
            GlobalTransform::from_xyz(3.0, 4.0, 5.0),
        ));
        app.add_plugins(
            VisualValleyWeatherPlugin::new(profile()).with_initial_state(WeatherState::Rain),
        );
        app.update();

        let stats = app.world().resource::<WeatherProjectionStats>();
        assert_eq!(stats.status, WeatherProjectionStatus::ObservedCpuProjection);
        assert_eq!(stats.rain.batch_entities, 1);
        assert_eq!(stats.rain.per_drop_entities, 0);
        assert_eq!(stats.rain.generated_drops, 8_192);
        assert!(stats.rain.visible);
        assert!(stats.rain.camera_followed);
        assert!(stats.rain.shader_animation_uniform_updated);
        assert!(stats.wetness.terrain_uniforms_match);
        assert!(stats.wetness.vegetation_uniforms_match);
        assert!(stats.post_processing.bloom_component_present);
        assert!(stats.post_processing.color_grading_component_present);
        assert!(!stats.gpu_execution_observed);
    }
}
