use bevy::{
    asset::{RenderAssetUsages, embedded_asset},
    core_pipeline::prepass::DeferredPrepass,
    light::AtmosphereEnvironmentMapLight,
    material::OpaqueRendererMethod,
    mesh::Indices,
    pbr::{
        ExtendedMaterial, MaterialExtension, ScreenSpaceReflections, ScreenSpaceTransmission,
        ScreenSpaceTransmissionQuality,
    },
    prelude::*,
    render::render_resource::{AsBindGroup, PrimitiveTopology},
    shader::ShaderRef,
};
use forge_project::{
    QualityWaterReflection, QualityWaterRefraction, ResolvedWaterReflectionPath,
    ResolvedWaterRefractionPath, TerrainDocument, VisualQualityPreset, VisualQualityTier,
    WaterDocument, WaterDocumentError, WaterOpticsCapabilities, WaterOpticsCapabilityStatus,
    WaterOpticsResolution, WaterQualitySettings, WaterQualityTier, WaterReflectionFallback,
    WaterReflectionProfile, WaterRefractionFallback, WaterRefractionProfile,
};
use forge_world::{
    RiverCenterlineSample, TerrainGenerationError, TerrainGenerationPlan, sample_river_centerline,
};
use serde::Serialize;
use std::{error::Error, fmt};

/// Minimum Gate 1 adapter clearance used until the persistent water contract owns this value.
pub const VISUAL_VALLEY_WATER_CLEARANCE_M: f32 = 0.14;

const WATER_SHADER_PATH: &str = "embedded://forge_engine/shaders/visual_valley_water.wgsl";
const WATER_SHADER_PHASE_WRAP_RADIANS: f64 = std::f64::consts::TAU;
const WATER_IOR: f32 = 1.33;
const WATER_DISABLED_REFLECTANCE: f32 = 0.0;

const RIVER_LANE_COUNT: usize = 3;
const RIVER_BANK_LANE_COUNT: usize = 2;
const RIVER_BANK_VERTICES_PER_SECTION: usize = 4;
const TRIANGLES_PER_SECTION_PAIR: usize = 4;
const INDICES_PER_TRIANGLE: usize = 3;
const WATER_CLEARANCE_ABOVE_WAVE_M: f32 = 0.02;
const BANK_MESH_CLEARANCE_M: f32 = 0.015;
const MIN_TANGENT_LENGTH_SQUARED: f64 = 1.0e-12;
const MIN_TRIANGLE_NORMAL_LENGTH_SQUARED: f64 = 1.0e-18;
const MIN_POSITIVE_WINDING_Y: f64 = 1.0e-9;

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum WaterRuntimeObservationStatus {
    #[default]
    NotObserved,
    ObservedMainWorldComponents,
    ObservedIncompleteComponents,
}

/// Honest logical and ECS observations for the derived river surface.
///
/// Counts describe source samples, CPU mesh data, and main-world components. They are not draw-call
/// counts and do not prove that a native or WebGPU render pass executed.
#[derive(Clone, Debug, PartialEq, Resource, Serialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "serialized evidence keeps independent yes/no observations explicit"
)]
pub struct WaterProjectionStats {
    pub source_hash: String,
    pub terrain_source_hash: String,
    pub authored_control_points: usize,
    pub centerline_samples: usize,
    pub terrain_surface_queries: usize,
    pub water_surface_queries: usize,
    pub river_bank_surface_queries: usize,
    pub vertices: usize,
    pub triangles: usize,
    pub water_vertices: usize,
    pub water_triangles: usize,
    pub river_bank_vertices: usize,
    pub river_bank_triangles: usize,
    pub positive_winding_triangles: usize,
    pub winding_corrections: usize,
    pub tangent_fallbacks: usize,
    pub terrain_clearance_m: f32,
    pub minimum_observed_clearance_m: f32,
    pub maximum_observed_clearance_m: f32,
    pub terrain_projection_lifts: usize,
    pub downstream_monotone_lifts: usize,
    pub authored_centerline_monotone_downstream: bool,
    pub projected_lanes_monotone_downstream: bool,
    pub material_alpha_mode: &'static str,
    pub opaque_renderer_method: &'static str,
    pub requested_opacity: f32,
    pub opacity_status: &'static str,
    pub requested_wave_amplitude_m: f32,
    pub requested_wave_length_m: f32,
    pub requested_flow_speed_mps: f32,
    pub requested_normal_strength: f32,
    pub wave_motion_status: &'static str,
    pub shader_asset_path: &'static str,
    pub shader_material_status: &'static str,
    pub shader_material_assets: usize,
    pub shader_material_entities: usize,
    pub shader_uniforms_match_profile: bool,
    pub animation_updates: u64,
    pub animation_uniform_status: &'static str,
    pub minimum_sampled_bed_depth_m: f32,
    pub maximum_sampled_bed_depth_m: f32,
    pub bed_depth_status: &'static str,
    pub requested_bank_shoulder_width_m: f32,
    pub requested_bank_blend_width_m: f32,
    pub requested_bank_wet_margin_m: f32,
    pub requested_bank_horizontal_to_vertical_slope: f32,
    pub requested_bank_erosion_amplitude_m: f32,
    pub requested_bank_erosion_frequency_cycles_per_m: f32,
    pub bank_shoulder_status: &'static str,
    pub bank_blend_status: &'static str,
    pub bank_wet_margin_status: &'static str,
    pub bank_slope_status: &'static str,
    pub bank_erosion_status: &'static str,
    pub bank_material_status: &'static str,
    pub requested_reflection_intensity: Option<f32>,
    pub applied_material_reflectance: Option<f32>,
    pub requested_reflection_steps: u16,
    pub requested_reflection_max_distance_m: Option<f32>,
    pub reflection_intensity_status: &'static str,
    pub reflection_steps_status: &'static str,
    pub reflection_max_distance_status: &'static str,
    pub requested_refraction_strength: Option<f32>,
    pub requested_refraction_steps: u16,
    pub requested_refraction_max_depth_m: Option<f32>,
    pub applied_surface_tint_strength: Option<f32>,
    pub applied_specular_transmission: Option<f32>,
    pub applied_uniform_thickness_m: Option<f32>,
    pub screen_space_transmission_steps: usize,
    pub screen_space_transmission_quality: &'static str,
    pub screen_space_transmission_blur_taps: u8,
    pub refraction_status: &'static str,
    pub refraction_steps_status: &'static str,
    pub refraction_max_depth_status: &'static str,
    pub requested_ssr_component_prepared: bool,
    pub forward_material_ssr_limit: &'static str,
    pub resolved_optics: WaterOpticsResolution,
    pub runtime_observation: WaterRuntimeObservationStatus,
    pub water_mesh_entities: usize,
    pub river_bank_mesh_entities: usize,
    pub camera_3d_entities: usize,
    pub deferred_camera_entities: usize,
    pub ssr_camera_entities: usize,
    pub msaa_off_camera_entities: usize,
    pub msaa_sample4_camera_entities: usize,
    pub transmission_disabled_camera_entities: usize,
    pub transmission_enabled_camera_entities: usize,
    pub atmosphere_environment_camera_entities: usize,
    pub environment_reflection_status: &'static str,
    pub screen_space_transmission_status: &'static str,
    pub cpu_ecs_application_status: &'static str,
    pub gpu_pass_observed: bool,
    pub native_gpu_execution_status: &'static str,
    pub webgpu_execution_status: &'static str,
    pub evidence_source: &'static str,
}

#[derive(Clone, Resource)]
pub(crate) struct VisualValleyWaterProfile {
    pub(crate) document: WaterDocument,
}

#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct VisualValleyWaterSurface;

#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct VisualValleyRiverBankSurface;

/// Profile-driven parameters consumed by the Forge-original river shader.
///
/// All colours are linear. `flow_phase` stores a unit XZ downstream direction followed by the
/// current and previous wrapped phase in radians. The two phases keep Bevy motion-vector
/// prepasses deterministic without using wall-clock time in WGSL.
#[derive(Asset, AsBindGroup, Clone, Debug, PartialEq, Reflect)]
pub struct VisualValleyWaterExtension {
    /// Linear shallow RGB and the resolved surface-tint fallback strength.
    #[uniform(100)]
    pub shallow_color_surface_tint: Vec4,
    /// Linear deep RGB and the maximum sampled bed depth in metres.
    #[uniform(100)]
    pub deep_color_max_depth: Vec4,
    /// Authored amplitude, wavelength, flow speed, and normal response.
    #[uniform(100)]
    pub wave_parameters: Vec4,
    /// Downstream direction XZ and current/previous wrapped phase in radians.
    #[uniform(100)]
    pub flow_phase: Vec4,
    /// Requested opacity, authored roughness, water IOR, and applied IBL reflectance.
    #[uniform(100)]
    pub optical_parameters: Vec4,
}

impl MaterialExtension for VisualValleyWaterExtension {
    fn vertex_shader() -> ShaderRef {
        WATER_SHADER_PATH.into()
    }

    fn fragment_shader() -> ShaderRef {
        WATER_SHADER_PATH.into()
    }

    fn prepass_vertex_shader() -> ShaderRef {
        WATER_SHADER_PATH.into()
    }

    fn deferred_vertex_shader() -> ShaderRef {
        WATER_SHADER_PATH.into()
    }

    fn deferred_fragment_shader() -> ShaderRef {
        WATER_SHADER_PATH.into()
    }
}

pub type VisualValleyWaterMaterial = ExtendedMaterial<StandardMaterial, VisualValleyWaterExtension>;

/// Deterministic CPU animation state projected into the water material every Bevy update.
#[derive(Clone, Debug, Resource)]
pub struct WaterRuntimeState {
    current_phase_radians: f64,
    previous_phase_radians: f64,
    angular_speed_radians_per_second: f64,
    updates: u64,
}

impl WaterRuntimeState {
    fn from_profile(water: &WaterDocument) -> Self {
        let seed_fraction = f64::from((water.seed & 0xffff) as u16) / 65_536.0;
        let phase = seed_fraction * WATER_SHADER_PHASE_WRAP_RADIANS;
        Self {
            current_phase_radians: phase,
            previous_phase_radians: phase,
            angular_speed_radians_per_second: WATER_SHADER_PHASE_WRAP_RADIANS
                * f64::from(water.surface.flow_speed_mps)
                / f64::from(water.surface.wave_length_m),
            updates: 0,
        }
    }

    #[must_use]
    pub fn current_phase_radians(&self) -> f64 {
        self.current_phase_radians
    }

    #[must_use]
    pub fn previous_phase_radians(&self) -> f64 {
        self.previous_phase_radians
    }

    #[must_use]
    pub const fn updates(&self) -> u64 {
        self.updates
    }
}

/// Bevy adapter for the engine-neutral Visual Valley water document.
pub struct VisualValleyWaterPlugin {
    terrain: TerrainDocument,
    water: WaterDocument,
    quality: Option<(VisualQualityPreset, VisualQualityTier)>,
}

impl VisualValleyWaterPlugin {
    #[must_use]
    pub fn new(terrain: TerrainDocument, water: WaterDocument) -> Self {
        Self {
            terrain,
            water,
            quality: None,
        }
    }

    /// Applies the resolved water optics and bounded budgets for the startup tier.
    #[must_use]
    pub fn with_quality_preset(
        mut self,
        preset: VisualQualityPreset,
        tier: VisualQualityTier,
    ) -> Self {
        self.quality = Some((preset, tier));
        self
    }
}

impl Plugin for VisualValleyWaterPlugin {
    fn build(&self, app: &mut App) {
        let mut prepared =
            prepare_water_surface(&self.terrain, &self.water).unwrap_or_else(|error| {
                panic!("VisualValleyWaterPlugin requires compatible terrain and water: {error}")
            });
        if let Some((quality, tier)) = &self.quality {
            apply_quality_optics_projection(&mut prepared.stats, &self.water, quality.water, *tier);
        }
        apply_water_optics_to_material(&mut prepared.water_material, &self.water, &prepared.stats);
        let stats = prepared.stats.clone();
        let transmission = screen_space_transmission_configuration(&stats);
        let applied_optics = applied_water_optics(&stats);
        let material_projection = prepared.water_material.extension.clone();

        embedded_asset!(app, "shaders/visual_valley_water.wgsl");
        app.insert_resource(stats)
            .insert_resource(VisualValleyWaterProfile {
                document: self.water.clone(),
            })
            .insert_resource(WaterRuntimeState::from_profile(&self.water))
            .insert_resource(WaterMaterialProjection(material_projection))
            .insert_resource(AppliedWaterOpticsState(applied_optics))
            .insert_resource(PreparedWaterSurface(Some(prepared)))
            .insert_resource(PreparedWaterCamera { transmission })
            .add_plugins(MaterialPlugin::<VisualValleyWaterMaterial>::default())
            .add_systems(Startup, spawn_water_surface)
            .add_systems(
                Update,
                (advance_water_runtime, project_water_animation).chain(),
            )
            .add_systems(
                PostStartup,
                (configure_water_cameras, observe_water_components).chain(),
            )
            .add_systems(PostUpdate, observe_water_components)
            .add_systems(
                Last,
                synchronize_water_optics.after(crate::quality::VisualValleyQualitySet::Apply),
            );
    }
}

#[derive(Debug)]
pub enum WaterAdapterError {
    InvalidWater(WaterDocumentError),
    InvalidTerrain(TerrainGenerationError),
    SurfaceProjection {
        sample_index: usize,
        lane: &'static str,
        source: TerrainGenerationError,
    },
    UphillCenterline {
        sample_index: usize,
    },
    DegenerateTangent {
        sample_index: usize,
    },
    ArithmeticOverflow(&'static str),
    AllocationFailed(&'static str),
    InvalidProjectedVertex {
        sample_index: usize,
        lane: &'static str,
    },
    DegenerateTriangle {
        triangle_index: usize,
    },
}

impl fmt::Display for WaterAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidWater(error) => write!(formatter, "water document is invalid: {error}"),
            Self::InvalidTerrain(error) => write!(formatter, "terrain plan is invalid: {error}"),
            Self::SurfaceProjection {
                sample_index,
                lane,
                source,
            } => write!(
                formatter,
                "river sample {sample_index} {lane} projection failed: {source}"
            ),
            Self::UphillCenterline { sample_index } => write!(
                formatter,
                "river centreline rises between samples {sample_index} and {}",
                sample_index + 1
            ),
            Self::DegenerateTangent { sample_index } => {
                write!(
                    formatter,
                    "river sample {sample_index} has no robust plan-view tangent"
                )
            }
            Self::ArithmeticOverflow(label) => {
                write!(formatter, "river mesh arithmetic overflow for {label}")
            }
            Self::AllocationFailed(label) => {
                write!(formatter, "river mesh allocation failed for {label}")
            }
            Self::InvalidProjectedVertex { sample_index, lane } => write!(
                formatter,
                "river sample {sample_index} {lane} projection is non-finite"
            ),
            Self::DegenerateTriangle { triangle_index } => {
                write!(
                    formatter,
                    "river triangle {triangle_index} is degenerate in plan view"
                )
            }
        }
    }
}

impl Error for WaterAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::InvalidWater(error) => Some(error),
            Self::InvalidTerrain(error) | Self::SurfaceProjection { source: error, .. } => {
                Some(error)
            }
            Self::UphillCenterline { .. }
            | Self::DegenerateTangent { .. }
            | Self::ArithmeticOverflow(_)
            | Self::AllocationFailed(_)
            | Self::InvalidProjectedVertex { .. }
            | Self::DegenerateTriangle { .. } => None,
        }
    }
}

impl From<WaterDocumentError> for WaterAdapterError {
    fn from(error: WaterDocumentError) -> Self {
        Self::InvalidWater(error)
    }
}

impl From<TerrainGenerationError> for WaterAdapterError {
    fn from(error: TerrainGenerationError) -> Self {
        Self::InvalidTerrain(error)
    }
}

#[derive(Resource)]
struct PreparedWaterSurface(Option<PreparedWaterScene>);

#[derive(Resource)]
struct PreparedWaterCamera {
    transmission: ScreenSpaceTransmission,
}

#[derive(Clone, Copy, Debug, PartialEq)]
struct AppliedWaterOptics {
    reflectance: f32,
    surface_tint_strength: f32,
    specular_transmission: f32,
    uniform_thickness_m: f32,
    transmission_steps: usize,
    transmission_quality: ScreenSpaceTransmissionQuality,
    transmission_quality_name: &'static str,
    transmission_blur_taps: u8,
}

#[derive(Resource)]
struct AppliedWaterOpticsState(AppliedWaterOptics);

struct PreparedWaterScene {
    name: String,
    water_geometry: RiverSurfaceGeometry,
    river_bank_geometry: RiverSurfaceGeometry,
    water_material: VisualValleyWaterMaterial,
    river_bank_material: StandardMaterial,
    stats: WaterProjectionStats,
}

struct RiverSurfaceGeometry {
    positions: Vec<[f32; 3]>,
    terrain_heights: Vec<f32>,
    normals: Vec<[f32; 3]>,
    uvs: Vec<[f32; 2]>,
    colors: Option<Vec<[f32; 4]>>,
    indices: Vec<u32>,
}

#[derive(Clone, Copy)]
struct ProjectedLane {
    position: [f32; 3],
    terrain_height_m: f32,
}

#[derive(Clone, Copy)]
struct ProjectedSection {
    lanes: [ProjectedLane; RIVER_LANE_COUNT],
    bank_outer_lanes: [ProjectedLane; RIVER_BANK_LANE_COUNT],
    bed_depth_m: f32,
}

#[derive(Clone, Resource)]
struct WaterSurfaceMaterial(Handle<VisualValleyWaterMaterial>);

#[derive(Clone, Resource)]
struct WaterMaterialProjection(VisualValleyWaterExtension);

#[derive(Clone, Copy)]
struct MeshCounts {
    vertices: usize,
    triangles: usize,
    indices: usize,
}

fn checked_mesh_counts(sample_count: usize) -> Result<MeshCounts, WaterAdapterError> {
    checked_mesh_counts_for(sample_count, RIVER_LANE_COUNT, TRIANGLES_PER_SECTION_PAIR)
}

fn checked_bank_mesh_counts(sample_count: usize) -> Result<MeshCounts, WaterAdapterError> {
    checked_mesh_counts_for(
        sample_count,
        RIVER_BANK_VERTICES_PER_SECTION,
        TRIANGLES_PER_SECTION_PAIR,
    )
}

fn checked_mesh_counts_for(
    sample_count: usize,
    vertices_per_section: usize,
    triangles_per_section_pair: usize,
) -> Result<MeshCounts, WaterAdapterError> {
    let section_pairs = sample_count
        .checked_sub(1)
        .ok_or(WaterAdapterError::ArithmeticOverflow("section pairs"))?;
    let vertices = sample_count
        .checked_mul(vertices_per_section)
        .ok_or(WaterAdapterError::ArithmeticOverflow("vertices"))?;
    let triangles = section_pairs
        .checked_mul(triangles_per_section_pair)
        .ok_or(WaterAdapterError::ArithmeticOverflow("triangles"))?;
    let indices = triangles
        .checked_mul(INDICES_PER_TRIANGLE)
        .ok_or(WaterAdapterError::ArithmeticOverflow("indices"))?;
    u32::try_from(vertices.saturating_sub(1))
        .map_err(|_| WaterAdapterError::ArithmeticOverflow("u32 vertex indices"))?;
    Ok(MeshCounts {
        vertices,
        triangles,
        indices,
    })
}

#[allow(
    clippy::too_many_lines,
    reason = "the preparation transaction keeps validation, checked counts, and evidence atomic"
)]
fn prepare_water_surface(
    terrain: &TerrainDocument,
    water: &WaterDocument,
) -> Result<PreparedWaterScene, WaterAdapterError> {
    water.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
    let terrain_plan = TerrainGenerationPlan::from_document(terrain)?;
    let samples = sample_river_centerline(water)?;
    verify_authored_downstream_monotone(&samples)?;
    let water_counts = checked_mesh_counts(samples.len())?;
    let river_bank_counts = checked_bank_mesh_counts(samples.len())?;
    let water_clearance_m = (water.surface.wave_amplitude_m + WATER_CLEARANCE_ABOVE_WAVE_M)
        .max(VISUAL_VALLEY_WATER_CLEARANCE_M);

    let mut sections = Vec::new();
    sections
        .try_reserve_exact(samples.len())
        .map_err(|_| WaterAdapterError::AllocationFailed("projected sections"))?;
    let mut tangent_fallbacks = 0;
    let mut terrain_projection_lifts = 0;
    for sample_index in 0..samples.len() {
        let (tangent, used_fallback) = robust_tangent(&samples, sample_index)?;
        tangent_fallbacks += usize::from(used_fallback);
        let sample = samples[sample_index];
        let half_width_m = sample.surface_width_m * 0.5;
        let left_xz = [
            sample.center_m.x - tangent[1] * half_width_m,
            sample.center_m.z + tangent[0] * half_width_m,
        ];
        let right_xz = [
            sample.center_m.x + tangent[1] * half_width_m,
            sample.center_m.z - tangent[0] * half_width_m,
        ];
        let bank_outer_offset_m = half_width_m + water.banks.shoulder_width_m;
        let bank_outer_xz = [
            [
                sample.center_m.x - tangent[1] * bank_outer_offset_m,
                sample.center_m.z + tangent[0] * bank_outer_offset_m,
            ],
            [
                sample.center_m.x + tangent[1] * bank_outer_offset_m,
                sample.center_m.z - tangent[0] * bank_outer_offset_m,
            ],
        ];
        let lane_xz = [left_xz, [sample.center_m.x, sample.center_m.z], right_xz];
        let mut lanes = [ProjectedLane {
            position: [0.0; 3],
            terrain_height_m: 0.0,
        }; RIVER_LANE_COUNT];
        for (lane_index, xz) in lane_xz.into_iter().enumerate() {
            let lane = lane_label(lane_index);
            let surface = terrain_plan.surface_at(xz[0], xz[1]).map_err(|source| {
                WaterAdapterError::SurfaceProjection {
                    sample_index,
                    lane,
                    source,
                }
            })?;
            let required_y = surface.height_m + water_clearance_m;
            let projected_y = sample.center_m.y.max(required_y);
            terrain_projection_lifts += usize::from(required_y > sample.center_m.y);
            if !projected_y.is_finite() {
                return Err(WaterAdapterError::InvalidProjectedVertex { sample_index, lane });
            }
            lanes[lane_index] = ProjectedLane {
                position: [xz[0], projected_y, xz[1]],
                terrain_height_m: surface.height_m,
            };
        }
        let mut bank_outer_lanes = [ProjectedLane {
            position: [0.0; 3],
            terrain_height_m: 0.0,
        }; RIVER_BANK_LANE_COUNT];
        for (bank_index, xz) in bank_outer_xz.into_iter().enumerate() {
            let lane = bank_lane_label(bank_index);
            let surface = terrain_plan.surface_at(xz[0], xz[1]).map_err(|source| {
                WaterAdapterError::SurfaceProjection {
                    sample_index,
                    lane,
                    source,
                }
            })?;
            let projected_y = surface.height_m + BANK_MESH_CLEARANCE_M;
            if !projected_y.is_finite() {
                return Err(WaterAdapterError::InvalidProjectedVertex { sample_index, lane });
            }
            bank_outer_lanes[bank_index] = ProjectedLane {
                position: [xz[0], projected_y, xz[1]],
                terrain_height_m: surface.height_m,
            };
        }
        sections.push(ProjectedSection {
            lanes,
            bank_outer_lanes,
            bed_depth_m: sample.bed_depth_m,
        });
    }

    let downstream_monotone_lifts = apply_downstream_monotone_pass(&mut sections);
    let (water_geometry, water_winding_corrections) = build_geometry(&sections, water_counts)?;
    let (river_bank_geometry, river_bank_winding_corrections) =
        build_river_bank_geometry(&sections, river_bank_counts)?;
    let (minimum_observed_clearance_m, maximum_observed_clearance_m) = water_geometry
        .positions
        .iter()
        .zip(&water_geometry.terrain_heights)
        .map(|(position, terrain_height)| position[1] - terrain_height)
        .fold(
            (f32::INFINITY, f32::NEG_INFINITY),
            |(minimum, maximum), value| (minimum.min(value), maximum.max(value)),
        );
    let resolved_optics = water.resolve_optics_path(bevy_water_optics_capabilities());
    let (minimum_sampled_bed_depth_m, maximum_sampled_bed_depth_m) =
        samples.iter().map(|sample| sample.bed_depth_m).fold(
            (f32::INFINITY, f32::NEG_INFINITY),
            |(minimum, maximum), depth| (minimum.min(depth), maximum.max(depth)),
        );
    let (requested_reflection_intensity, requested_reflection_max_distance_m) =
        requested_reflection_parameters(water);
    let requested_refraction_strength = requested_refraction_strength(water);
    let flow_direction = downstream_flow_direction(&samples);
    let runtime_state = WaterRuntimeState::from_profile(water);
    let water_surface_queries = water_counts.vertices;
    let river_bank_surface_queries = samples.len().checked_mul(RIVER_BANK_LANE_COUNT).ok_or(
        WaterAdapterError::ArithmeticOverflow("river bank surface queries"),
    )?;
    let terrain_surface_queries = water_surface_queries
        .checked_add(river_bank_surface_queries)
        .ok_or(WaterAdapterError::ArithmeticOverflow(
            "terrain surface queries",
        ))?;
    let vertices = water_counts
        .vertices
        .checked_add(river_bank_counts.vertices)
        .ok_or(WaterAdapterError::ArithmeticOverflow("total vertices"))?;
    let triangles = water_counts
        .triangles
        .checked_add(river_bank_counts.triangles)
        .ok_or(WaterAdapterError::ArithmeticOverflow("total triangles"))?;
    let winding_corrections = water_winding_corrections
        .checked_add(river_bank_winding_corrections)
        .ok_or(WaterAdapterError::ArithmeticOverflow("winding corrections"))?;
    let mut stats = WaterProjectionStats {
        source_hash: water.content_hash()?.to_string(),
        terrain_source_hash: terrain_plan.document_hash().to_string(),
        authored_control_points: water.river.control_points.len(),
        centerline_samples: samples.len(),
        terrain_surface_queries,
        water_surface_queries,
        river_bank_surface_queries,
        vertices,
        triangles,
        water_vertices: water_counts.vertices,
        water_triangles: water_counts.triangles,
        river_bank_vertices: river_bank_counts.vertices,
        river_bank_triangles: river_bank_counts.triangles,
        positive_winding_triangles: triangles,
        winding_corrections,
        tangent_fallbacks,
        terrain_clearance_m: water_clearance_m,
        minimum_observed_clearance_m,
        maximum_observed_clearance_m,
        terrain_projection_lifts,
        downstream_monotone_lifts,
        authored_centerline_monotone_downstream: true,
        projected_lanes_monotone_downstream: projected_lanes_are_monotone(&sections),
        material_alpha_mode: "opaque",
        opaque_renderer_method: "forward",
        requested_opacity: water.surface.opacity,
        opacity_status: "requested_recorded_alpha_mode_opaque_bevy_transmissive_phase_is_material_driven",
        requested_wave_amplitude_m: water.surface.wave_amplitude_m,
        requested_wave_length_m: water.surface.wave_length_m,
        requested_flow_speed_mps: water.surface.flow_speed_mps,
        requested_normal_strength: water.surface.normal_strength,
        wave_motion_status: "profile_projected_to_deterministic_vertex_wave_shader",
        shader_asset_path: WATER_SHADER_PATH,
        shader_material_status: "prepared_cpu_material_asset_gpu_pipeline_not_observed",
        shader_material_assets: 0,
        shader_material_entities: 0,
        shader_uniforms_match_profile: false,
        animation_updates: 0,
        animation_uniform_status: "prepared_not_observed",
        minimum_sampled_bed_depth_m,
        maximum_sampled_bed_depth_m,
        bed_depth_status: "sampled_bed_depth_encoded_in_vertex_color_and_shader_palette",
        requested_bank_shoulder_width_m: water.banks.shoulder_width_m,
        requested_bank_blend_width_m: water.banks.blend_width_m,
        requested_bank_wet_margin_m: water.banks.wet_margin_m,
        requested_bank_horizontal_to_vertical_slope: water.banks.horizontal_to_vertical_slope,
        requested_bank_erosion_amplitude_m: water.banks.erosion_amplitude_m,
        requested_bank_erosion_frequency_cycles_per_m: water.banks.erosion_frequency_cycles_per_m,
        bank_shoulder_status: "procedural_outer_strip_projected_from_shoulder_width",
        bank_blend_status: "deferred_no_terrain_carving_adapter",
        bank_wet_margin_status: "deferred_no_wet_edge_shader",
        bank_slope_status: "deferred_strip_bridges_water_edge_to_projected_terrain",
        bank_erosion_status: "deferred_no_terrain_carving_adapter",
        bank_material_status: "opaque_forward_matches_terrain_earth_palette",
        requested_reflection_intensity,
        applied_material_reflectance: None,
        requested_reflection_steps: 0,
        requested_reflection_max_distance_m,
        reflection_intensity_status: "prepared_not_observed",
        reflection_steps_status: "prepared_not_observed",
        reflection_max_distance_status: "prepared_not_observed",
        requested_refraction_strength,
        requested_refraction_steps: 0,
        requested_refraction_max_depth_m: None,
        applied_surface_tint_strength: None,
        applied_specular_transmission: None,
        applied_uniform_thickness_m: None,
        screen_space_transmission_steps: 0,
        screen_space_transmission_quality: "disabled",
        screen_space_transmission_blur_taps: 0,
        refraction_status: "prepared_not_observed",
        refraction_steps_status: "prepared_not_observed",
        refraction_max_depth_status: "prepared_not_observed",
        requested_ssr_component_prepared: false,
        forward_material_ssr_limit: "bevy_0_19_1_deferred_ssr_unavailable_for_forward_water_environment_fallback_used",
        resolved_optics,
        runtime_observation: WaterRuntimeObservationStatus::NotObserved,
        water_mesh_entities: 0,
        river_bank_mesh_entities: 0,
        camera_3d_entities: 0,
        deferred_camera_entities: 0,
        ssr_camera_entities: 0,
        msaa_off_camera_entities: 0,
        msaa_sample4_camera_entities: 0,
        transmission_disabled_camera_entities: 0,
        transmission_enabled_camera_entities: 0,
        atmosphere_environment_camera_entities: 0,
        environment_reflection_status: "prepared_not_observed",
        screen_space_transmission_status: "prepared_not_observed",
        cpu_ecs_application_status: "prepared_cpu_assets_not_observed",
        gpu_pass_observed: false,
        native_gpu_execution_status: "not_observed_by_water_metrics",
        webgpu_execution_status: "not_observed_by_water_metrics",
        evidence_source: "cpu_geometry_and_main_world_components_not_gpu_execution",
    };
    apply_quality_optics_projection(
        &mut stats,
        water,
        authored_water_quality_settings(water),
        visual_quality_tier(water.optics.quality),
    );

    let mut material = water_material(
        water,
        maximum_sampled_bed_depth_m,
        flow_direction,
        &runtime_state,
    );
    apply_water_optics_to_material(&mut material, water, &stats);

    Ok(PreparedWaterScene {
        name: water.name.clone(),
        water_geometry,
        river_bank_geometry,
        water_material: material,
        river_bank_material: river_bank_material(),
        stats,
    })
}

fn verify_authored_downstream_monotone(
    samples: &[RiverCenterlineSample],
) -> Result<(), WaterAdapterError> {
    for (sample_index, pair) in samples.windows(2).enumerate() {
        if pair[1].center_m.y > pair[0].center_m.y {
            return Err(WaterAdapterError::UphillCenterline { sample_index });
        }
    }
    Ok(())
}

fn robust_tangent(
    samples: &[RiverCenterlineSample],
    sample_index: usize,
) -> Result<([f32; 2], bool), WaterAdapterError> {
    let current = samples[sample_index].center_m;
    let previous = samples[sample_index.saturating_sub(1)].center_m;
    let next = samples[(sample_index + 1).min(samples.len() - 1)].center_m;
    let candidates = [
        ([next.x - previous.x, next.z - previous.z], false),
        ([next.x - current.x, next.z - current.z], true),
        ([current.x - previous.x, current.z - previous.z], true),
    ];
    for (candidate, fallback) in candidates {
        let length_squared = f64::from(candidate[0]) * f64::from(candidate[0])
            + f64::from(candidate[1]) * f64::from(candidate[1]);
        if length_squared.is_finite() && length_squared > MIN_TANGENT_LENGTH_SQUARED {
            let inverse_length = length_squared.sqrt().recip();
            #[allow(clippy::cast_possible_truncation)]
            let tangent = [
                (f64::from(candidate[0]) * inverse_length) as f32,
                (f64::from(candidate[1]) * inverse_length) as f32,
            ];
            return Ok((tangent, fallback));
        }
    }
    Err(WaterAdapterError::DegenerateTangent { sample_index })
}

fn apply_downstream_monotone_pass(sections: &mut [ProjectedSection]) -> usize {
    let mut adjustments = 0;
    for lane_index in 0..RIVER_LANE_COUNT {
        let mut downstream_height = sections.last().map_or(f32::NEG_INFINITY, |section| {
            section.lanes[lane_index].position[1]
        });
        for section in sections.iter_mut().rev().skip(1) {
            let height = &mut section.lanes[lane_index].position[1];
            if *height < downstream_height {
                *height = downstream_height;
                adjustments += 1;
            } else {
                downstream_height = *height;
            }
        }
    }
    adjustments
}

fn projected_lanes_are_monotone(sections: &[ProjectedSection]) -> bool {
    (0..RIVER_LANE_COUNT).all(|lane_index| {
        sections.windows(2).all(|pair| {
            pair[1].lanes[lane_index].position[1] <= pair[0].lanes[lane_index].position[1]
        })
    })
}

fn build_geometry(
    sections: &[ProjectedSection],
    counts: MeshCounts,
) -> Result<(RiverSurfaceGeometry, usize), WaterAdapterError> {
    let mut positions = Vec::new();
    let mut terrain_heights = Vec::new();
    let mut uvs = Vec::new();
    let mut colors = Vec::new();
    let mut indices = Vec::new();
    positions
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("positions"))?;
    terrain_heights
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("terrain heights"))?;
    uvs.try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("UVs"))?;
    colors
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("depth colors"))?;
    indices
        .try_reserve_exact(counts.indices)
        .map_err(|_| WaterAdapterError::AllocationFailed("indices"))?;

    let denominator = sections.len().saturating_sub(1).max(1);
    for (sample_index, section) in sections.iter().enumerate() {
        #[allow(clippy::cast_precision_loss)]
        let v = sample_index as f32 / denominator as f32;
        for (lane_index, lane) in section.lanes.iter().enumerate() {
            positions.push(lane.position);
            terrain_heights.push(lane.terrain_height_m);
            uvs.push([lane_uv(lane_index), v]);
            let depth_m = if lane_index == 1 {
                section.bed_depth_m
            } else {
                0.0
            };
            colors.push([depth_m, 0.0, 0.0, 1.0]);
        }
    }

    let mut winding_corrections = 0;
    for section_index in 0..sections.len() - 1 {
        let row = section_index
            .checked_mul(RIVER_LANE_COUNT)
            .ok_or(WaterAdapterError::ArithmeticOverflow("row vertex offset"))?;
        let next_row =
            row.checked_add(RIVER_LANE_COUNT)
                .ok_or(WaterAdapterError::ArithmeticOverflow(
                    "next-row vertex offset",
                ))?;
        for strip in 0..2 {
            let a = row + strip;
            let b = next_row + strip;
            let c = row + strip + 1;
            let d = next_row + strip + 1;
            winding_corrections += push_positive_triangle(&positions, &mut indices, [a, b, c])?;
            winding_corrections += push_positive_triangle(&positions, &mut indices, [c, b, d])?;
        }
    }

    let normals = build_normals(&positions, &indices)?;
    Ok((
        RiverSurfaceGeometry {
            positions,
            terrain_heights,
            normals,
            uvs,
            colors: Some(colors),
            indices,
        },
        winding_corrections,
    ))
}

fn build_river_bank_geometry(
    sections: &[ProjectedSection],
    counts: MeshCounts,
) -> Result<(RiverSurfaceGeometry, usize), WaterAdapterError> {
    let mut positions = Vec::new();
    let mut terrain_heights = Vec::new();
    let mut uvs = Vec::new();
    let mut indices = Vec::new();
    positions
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank positions"))?;
    terrain_heights
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank terrain heights"))?;
    uvs.try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank UVs"))?;
    indices
        .try_reserve_exact(counts.indices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank indices"))?;

    let denominator = sections.len().saturating_sub(1).max(1);
    for (sample_index, section) in sections.iter().enumerate() {
        #[allow(clippy::cast_precision_loss)]
        let v = sample_index as f32 / denominator as f32;
        let vertices = [
            section.bank_outer_lanes[0],
            section.lanes[0],
            section.lanes[2],
            section.bank_outer_lanes[1],
        ];
        for (vertex_index, vertex) in vertices.into_iter().enumerate() {
            positions.push(vertex.position);
            terrain_heights.push(vertex.terrain_height_m);
            uvs.push([bank_lane_uv(vertex_index), v]);
        }
    }

    let mut winding_corrections = 0;
    for section_index in 0..sections.len() - 1 {
        let row = section_index
            .checked_mul(RIVER_BANK_VERTICES_PER_SECTION)
            .ok_or(WaterAdapterError::ArithmeticOverflow(
                "river bank row vertex offset",
            ))?;
        let next_row = row.checked_add(RIVER_BANK_VERTICES_PER_SECTION).ok_or(
            WaterAdapterError::ArithmeticOverflow("river bank next-row vertex offset"),
        )?;
        for strip_start in [0, 2] {
            let a = row + strip_start;
            let b = next_row + strip_start;
            let c = row + strip_start + 1;
            let d = next_row + strip_start + 1;
            winding_corrections += push_positive_triangle(&positions, &mut indices, [a, b, c])?;
            winding_corrections += push_positive_triangle(&positions, &mut indices, [c, b, d])?;
        }
    }

    let normals = build_normals(&positions, &indices)?;
    Ok((
        RiverSurfaceGeometry {
            positions,
            terrain_heights,
            normals,
            uvs,
            colors: None,
            indices,
        },
        winding_corrections,
    ))
}

fn push_positive_triangle(
    positions: &[[f32; 3]],
    indices: &mut Vec<u32>,
    triangle: [usize; 3],
) -> Result<usize, WaterAdapterError> {
    let triangle_index = indices.len() / INDICES_PER_TRIANGLE;
    let normal_y = triangle_normal_y(positions, triangle);
    if !normal_y.is_finite() || normal_y.abs() <= MIN_POSITIVE_WINDING_Y {
        return Err(WaterAdapterError::DegenerateTriangle { triangle_index });
    }
    let (triangle, corrected) = if normal_y > 0.0 {
        (triangle, 0)
    } else {
        ([triangle[0], triangle[2], triangle[1]], 1)
    };
    for index in triangle {
        indices.push(
            u32::try_from(index)
                .map_err(|_| WaterAdapterError::ArithmeticOverflow("u32 triangle index"))?,
        );
    }
    Ok(corrected)
}

fn triangle_normal_y(positions: &[[f32; 3]], triangle: [usize; 3]) -> f64 {
    let a = positions[triangle[0]];
    let b = positions[triangle[1]];
    let c = positions[triangle[2]];
    let first_edge_x = f64::from(b[0]) - f64::from(a[0]);
    let first_edge_z = f64::from(b[2]) - f64::from(a[2]);
    let second_edge_x = f64::from(c[0]) - f64::from(a[0]);
    let second_edge_z = f64::from(c[2]) - f64::from(a[2]);
    first_edge_z * second_edge_x - first_edge_x * second_edge_z
}

fn build_normals(
    positions: &[[f32; 3]],
    indices: &[u32],
) -> Result<Vec<[f32; 3]>, WaterAdapterError> {
    let mut accumulated = Vec::new();
    accumulated
        .try_reserve_exact(positions.len())
        .map_err(|_| WaterAdapterError::AllocationFailed("normals"))?;
    accumulated.resize(positions.len(), [0.0_f64; 3]);
    for (triangle_index, triangle) in indices.as_chunks::<3>().0.iter().enumerate() {
        let indices = [
            usize::try_from(triangle[0])
                .map_err(|_| WaterAdapterError::ArithmeticOverflow("usize triangle index"))?,
            usize::try_from(triangle[1])
                .map_err(|_| WaterAdapterError::ArithmeticOverflow("usize triangle index"))?,
            usize::try_from(triangle[2])
                .map_err(|_| WaterAdapterError::ArithmeticOverflow("usize triangle index"))?,
        ];
        let a = positions[indices[0]];
        let b = positions[indices[1]];
        let c = positions[indices[2]];
        let ab = [
            f64::from(b[0] - a[0]),
            f64::from(b[1] - a[1]),
            f64::from(b[2] - a[2]),
        ];
        let ac = [
            f64::from(c[0] - a[0]),
            f64::from(c[1] - a[1]),
            f64::from(c[2] - a[2]),
        ];
        let normal = [
            ab[1] * ac[2] - ab[2] * ac[1],
            ab[2] * ac[0] - ab[0] * ac[2],
            ab[0] * ac[1] - ab[1] * ac[0],
        ];
        let length_squared = normal.iter().map(|value| value * value).sum::<f64>();
        if !length_squared.is_finite() || length_squared <= MIN_TRIANGLE_NORMAL_LENGTH_SQUARED {
            return Err(WaterAdapterError::DegenerateTriangle { triangle_index });
        }
        for vertex in indices {
            for axis in 0..3 {
                accumulated[vertex][axis] += normal[axis];
            }
        }
    }
    accumulated
        .into_iter()
        .map(|normal| {
            let length = normal.iter().map(|value| value * value).sum::<f64>().sqrt();
            if !length.is_finite() || length <= f64::EPSILON {
                return Err(WaterAdapterError::DegenerateTriangle { triangle_index: 0 });
            }
            #[allow(clippy::cast_possible_truncation)]
            Ok([
                (normal[0] / length) as f32,
                (normal[1] / length) as f32,
                (normal[2] / length) as f32,
            ])
        })
        .collect()
}

fn water_material(
    water: &WaterDocument,
    maximum_sampled_bed_depth_m: f32,
    flow_direction: Vec2,
    runtime_state: &WaterRuntimeState,
) -> VisualValleyWaterMaterial {
    let shallow = water.surface.shallow_color_srgb;
    VisualValleyWaterMaterial {
        base: StandardMaterial {
            base_color: Color::srgb(shallow.red, shallow.green, shallow.blue),
            alpha_mode: AlphaMode::Opaque,
            metallic: 0.0,
            perceptual_roughness: water.surface.roughness,
            reflectance: WATER_DISABLED_REFLECTANCE,
            ior: WATER_IOR,
            diffuse_transmission: 0.0,
            specular_transmission: 0.0,
            thickness: 0.0,
            opaque_render_method: OpaqueRendererMethod::Forward,
            ..default()
        },
        extension: water_extension(
            water,
            0.0,
            maximum_sampled_bed_depth_m,
            flow_direction,
            runtime_state,
        ),
    }
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "wrapped shader phases are bounded to one f32-safe revolution"
)]
fn water_extension(
    water: &WaterDocument,
    surface_tint_strength: f32,
    maximum_sampled_bed_depth_m: f32,
    flow_direction: Vec2,
    runtime_state: &WaterRuntimeState,
) -> VisualValleyWaterExtension {
    let shallow = linear_color(
        water.surface.shallow_color_srgb.red,
        water.surface.shallow_color_srgb.green,
        water.surface.shallow_color_srgb.blue,
    );
    let deep = linear_color(
        water.surface.deep_color_srgb.red,
        water.surface.deep_color_srgb.green,
        water.surface.deep_color_srgb.blue,
    );
    VisualValleyWaterExtension {
        shallow_color_surface_tint: Vec4::new(
            shallow.red,
            shallow.green,
            shallow.blue,
            surface_tint_strength,
        ),
        deep_color_max_depth: Vec4::new(
            deep.red,
            deep.green,
            deep.blue,
            maximum_sampled_bed_depth_m,
        ),
        wave_parameters: Vec4::new(
            water.surface.wave_amplitude_m,
            water.surface.wave_length_m,
            water.surface.flow_speed_mps,
            water.surface.normal_strength,
        ),
        flow_phase: Vec4::new(
            flow_direction.x,
            flow_direction.y,
            runtime_state.current_phase_radians as f32,
            runtime_state.previous_phase_radians as f32,
        ),
        optical_parameters: Vec4::new(
            water.surface.opacity,
            water.surface.roughness,
            WATER_IOR,
            WATER_DISABLED_REFLECTANCE,
        ),
    }
}

fn linear_color(red: f32, green: f32, blue: f32) -> LinearRgba {
    Color::srgb(red, green, blue).to_linear()
}

fn downstream_flow_direction(samples: &[RiverCenterlineSample]) -> Vec2 {
    let Some((source, remaining)) = samples.split_first() else {
        return Vec2::X;
    };
    let outlet = remaining.last().unwrap_or(source);
    let delta = Vec2::new(
        outlet.center_m.x - source.center_m.x,
        outlet.center_m.z - source.center_m.z,
    );
    if delta.is_finite() && delta.length_squared() > f32::EPSILON {
        delta.normalize()
    } else {
        Vec2::X
    }
}

fn river_bank_material() -> StandardMaterial {
    StandardMaterial {
        base_color: Color::linear_rgb(0.24, 0.105, 0.035),
        alpha_mode: AlphaMode::Opaque,
        metallic: 0.0,
        perceptual_roughness: 0.86,
        opaque_render_method: OpaqueRendererMethod::Forward,
        ..default()
    }
}

fn requested_reflection_parameters(water: &WaterDocument) -> (Option<f32>, Option<f32>) {
    match water.optics.reflection {
        WaterReflectionProfile::ScreenSpace {
            intensity,
            max_distance_m,
            ..
        } => (Some(intensity), Some(max_distance_m)),
        WaterReflectionProfile::Environment { intensity } => (Some(intensity), None),
        WaterReflectionProfile::Disabled => (None, None),
    }
}

fn requested_refraction_strength(water: &WaterDocument) -> Option<f32> {
    match water.optics.refraction {
        WaterRefractionProfile::SurfaceTint { strength }
        | WaterRefractionProfile::ScreenSpace { strength, .. } => Some(strength),
        WaterRefractionProfile::Disabled => None,
    }
}

fn authored_water_quality_settings(water: &WaterDocument) -> WaterQualitySettings {
    let (reflection, reflection_steps, reflection_max_distance_m) = match water.optics.reflection {
        WaterReflectionProfile::Disabled => (QualityWaterReflection::Disabled, 0, 0.0),
        WaterReflectionProfile::Environment { .. } => (QualityWaterReflection::Environment, 0, 0.0),
        WaterReflectionProfile::ScreenSpace {
            max_steps,
            max_distance_m,
            ..
        } => (
            QualityWaterReflection::ScreenSpace,
            max_steps,
            max_distance_m,
        ),
    };
    let (refraction, refraction_steps, refraction_max_depth_m) = match water.optics.refraction {
        WaterRefractionProfile::Disabled => (QualityWaterRefraction::Disabled, 0, 0.0),
        WaterRefractionProfile::SurfaceTint { .. } => (QualityWaterRefraction::SurfaceTint, 0, 0.0),
        WaterRefractionProfile::ScreenSpace {
            max_steps,
            max_depth_m,
            ..
        } => (QualityWaterRefraction::ScreenSpace, max_steps, max_depth_m),
    };
    WaterQualitySettings {
        reflection,
        reflection_steps,
        reflection_max_distance_m,
        refraction,
        refraction_steps,
        refraction_max_depth_m,
    }
}

const fn transmission_quality_for(
    tier: WaterQualityTier,
) -> (ScreenSpaceTransmissionQuality, &'static str, u8) {
    match tier {
        WaterQualityTier::Low => (ScreenSpaceTransmissionQuality::Low, "low", 4),
        WaterQualityTier::Medium => (ScreenSpaceTransmissionQuality::Medium, "medium", 8),
        WaterQualityTier::High => (ScreenSpaceTransmissionQuality::High, "high", 16),
        WaterQualityTier::Ultra => (ScreenSpaceTransmissionQuality::Ultra, "ultra", 32),
    }
}

fn applied_water_optics(stats: &WaterProjectionStats) -> AppliedWaterOptics {
    let reflectance = if matches!(
        stats.resolved_optics.reflection,
        ResolvedWaterReflectionPath::Environment | ResolvedWaterReflectionPath::ScreenSpace { .. }
    ) {
        stats
            .requested_reflection_intensity
            .unwrap_or(WATER_DISABLED_REFLECTANCE)
            .clamp(0.0, 1.0)
    } else {
        WATER_DISABLED_REFLECTANCE
    };
    let surface_tint_strength = if matches!(
        stats.resolved_optics.refraction,
        ResolvedWaterRefractionPath::SurfaceTint
    ) {
        stats.requested_refraction_strength.unwrap_or(0.0)
    } else {
        0.0
    };
    let screen_space_transmission = matches!(
        stats.resolved_optics.refraction,
        ResolvedWaterRefractionPath::ScreenSpace { .. }
    );
    let (transmission_quality, transmission_quality_name, available_blur_taps) =
        transmission_quality_for(stats.resolved_optics.requested_quality);
    let uniform_thickness_m = if screen_space_transmission {
        stats
            .requested_refraction_max_depth_m
            .unwrap_or(0.0)
            .min(stats.maximum_sampled_bed_depth_m)
            .max(0.0)
    } else {
        0.0
    };
    AppliedWaterOptics {
        reflectance,
        surface_tint_strength,
        specular_transmission: if screen_space_transmission {
            stats.requested_refraction_strength.unwrap_or(0.0)
        } else {
            0.0
        },
        uniform_thickness_m,
        // Bevy's `steps` counts copied transmissive layers, not authored ray-march steps. One
        // river layer is exact here; quality maps only to Bevy's documented blur-tap preset.
        transmission_steps: if screen_space_transmission { 1 } else { 0 },
        transmission_quality,
        transmission_quality_name,
        transmission_blur_taps: if screen_space_transmission {
            available_blur_taps
        } else {
            0
        },
    }
}

fn screen_space_transmission_configuration(
    stats: &WaterProjectionStats,
) -> ScreenSpaceTransmission {
    let optics = applied_water_optics(stats);
    ScreenSpaceTransmission {
        steps: optics.transmission_steps,
        quality: optics.transmission_quality,
    }
}

fn apply_water_optics_to_material(
    material: &mut VisualValleyWaterMaterial,
    water: &WaterDocument,
    stats: &WaterProjectionStats,
) {
    let optics = applied_water_optics(stats);
    material.base.base_color = water_surface_color(water, optics.surface_tint_strength);
    material.base.reflectance = optics.reflectance;
    material.base.specular_transmission = optics.specular_transmission;
    material.base.thickness = optics.uniform_thickness_m;
    material.extension.shallow_color_surface_tint.w = optics.surface_tint_strength;
    material.extension.optical_parameters.w = optics.reflectance;
}

fn water_surface_color(water: &WaterDocument, surface_tint_strength: f32) -> Color {
    let shallow = water.surface.shallow_color_srgb;
    let deep = water.surface.deep_color_srgb;
    let shallow_strength = 1.0 - surface_tint_strength;
    Color::srgb(
        shallow.red * shallow_strength + deep.red * surface_tint_strength,
        shallow.green * shallow_strength + deep.green * surface_tint_strength,
        shallow.blue * shallow_strength + deep.blue * surface_tint_strength,
    )
}

const fn bevy_water_optics_capabilities() -> WaterOpticsCapabilities {
    WaterOpticsCapabilities {
        // Bevy 0.19.1's deferred SSR path emitted a validated wgpu invariant warning on the Gate 1
        // Vulkan baseline. Keep the authored request visible, but resolve it through the declared
        // fallback until a clean native and WebGPU path is qualified.
        screen_space_reflections: WaterOpticsCapabilityStatus::Unavailable,
        // Bevy's native transmissive forward phase can refract the copied opaque scene. This is a
        // component/material capability declaration; GPU execution remains separately unobserved.
        screen_space_refraction: WaterOpticsCapabilityStatus::Available,
        // The environment plugin explicitly attaches AtmosphereEnvironmentMapLight to the view.
        // Generated/filtered GPU maps remain outside this main-world capability observation.
        environment_reflections: WaterOpticsCapabilityStatus::Available,
    }
}

pub(crate) const fn quality_water_tier(tier: VisualQualityTier) -> WaterQualityTier {
    match tier {
        VisualQualityTier::Low => WaterQualityTier::Low,
        VisualQualityTier::Medium => WaterQualityTier::Medium,
        VisualQualityTier::High => WaterQualityTier::High,
        VisualQualityTier::Ultra => WaterQualityTier::Ultra,
    }
}

const fn visual_quality_tier(tier: WaterQualityTier) -> VisualQualityTier {
    match tier {
        WaterQualityTier::Low => VisualQualityTier::Low,
        WaterQualityTier::Medium => VisualQualityTier::Medium,
        WaterQualityTier::High => VisualQualityTier::High,
        WaterQualityTier::Ultra => VisualQualityTier::Ultra,
    }
}

fn resolve_quality_optics(
    quality: WaterQualitySettings,
    tier: VisualQualityTier,
    capabilities: WaterOpticsCapabilities,
    reflection_fallback: WaterReflectionFallback,
    refraction_fallback: WaterRefractionFallback,
) -> WaterOpticsResolution {
    let (reflection, reflection_fallback_used) = match quality.reflection {
        QualityWaterReflection::Disabled => (ResolvedWaterReflectionPath::Disabled, false),
        QualityWaterReflection::Environment => {
            if capabilities.environment_reflections == WaterOpticsCapabilityStatus::Available {
                (ResolvedWaterReflectionPath::Environment, false)
            } else {
                (ResolvedWaterReflectionPath::Disabled, true)
            }
        }
        QualityWaterReflection::ScreenSpace => {
            if capabilities.screen_space_reflections == WaterOpticsCapabilityStatus::Available {
                (
                    ResolvedWaterReflectionPath::ScreenSpace {
                        max_steps: quality.reflection_steps,
                    },
                    false,
                )
            } else if reflection_fallback == WaterReflectionFallback::EnvironmentThenDisabled
                && capabilities.environment_reflections == WaterOpticsCapabilityStatus::Available
            {
                (ResolvedWaterReflectionPath::Environment, true)
            } else {
                (ResolvedWaterReflectionPath::Disabled, true)
            }
        }
    };
    let (refraction, refraction_fallback_used) = match quality.refraction {
        QualityWaterRefraction::Disabled => (ResolvedWaterRefractionPath::Disabled, false),
        QualityWaterRefraction::SurfaceTint => (ResolvedWaterRefractionPath::SurfaceTint, false),
        QualityWaterRefraction::ScreenSpace => {
            if capabilities.screen_space_refraction == WaterOpticsCapabilityStatus::Available {
                (
                    ResolvedWaterRefractionPath::ScreenSpace {
                        max_steps: quality.refraction_steps,
                    },
                    false,
                )
            } else if refraction_fallback == WaterRefractionFallback::SurfaceTint {
                (ResolvedWaterRefractionPath::SurfaceTint, true)
            } else {
                (ResolvedWaterRefractionPath::Disabled, true)
            }
        }
    };
    WaterOpticsResolution {
        requested_quality: quality_water_tier(tier),
        capabilities,
        reflection,
        refraction,
        reflection_fallback_used,
        refraction_fallback_used,
    }
}

pub(crate) fn apply_quality_optics_projection(
    stats: &mut WaterProjectionStats,
    water: &WaterDocument,
    quality: WaterQualitySettings,
    tier: VisualQualityTier,
) {
    let resolved = resolve_quality_optics(
        quality,
        tier,
        stats.resolved_optics.capabilities,
        water.optics.reflection_fallback,
        water.optics.refraction_fallback,
    );
    let (authored_reflection_intensity, _) = requested_reflection_parameters(water);
    stats.requested_reflection_intensity = match quality.reflection {
        QualityWaterReflection::Disabled => None,
        QualityWaterReflection::Environment | QualityWaterReflection::ScreenSpace => {
            authored_reflection_intensity
        }
    };
    stats.requested_reflection_steps = quality.reflection_steps;
    stats.requested_reflection_max_distance_m = match quality.reflection {
        QualityWaterReflection::ScreenSpace => Some(quality.reflection_max_distance_m),
        QualityWaterReflection::Disabled | QualityWaterReflection::Environment => None,
    };
    stats.reflection_intensity_status = match (quality.reflection, resolved.reflection) {
        (QualityWaterReflection::Disabled, _) => "quality_profile_disabled",
        (_, ResolvedWaterReflectionPath::Disabled) => {
            "quality_profile_requested_unavailable_resolved_disabled"
        }
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::Environment) => {
            "quality_profile_ssr_unavailable_environment_ibl_fallback_intensity_mapped_to_material_reflectance_gpu_not_observed"
        }
        (QualityWaterReflection::Environment, ResolvedWaterReflectionPath::Environment) => {
            "quality_profile_environment_ibl_intensity_mapped_to_material_reflectance_gpu_not_observed"
        }
        (_, ResolvedWaterReflectionPath::ScreenSpace { .. }) => {
            "quality_profile_screen_space_path_cpu_configured_gpu_not_observed"
        }
    };
    stats.reflection_steps_status = match (quality.reflection, resolved.reflection) {
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::Environment) => {
            "requested_ssr_ray_steps_recorded_not_applied_environment_fallback"
        }
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::Disabled) => {
            "requested_ssr_ray_steps_recorded_not_applied_disabled_fallback"
        }
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::ScreenSpace { .. }) => {
            "requested_ssr_ray_steps_mapped_to_ssr_component"
        }
        (QualityWaterReflection::Disabled | QualityWaterReflection::Environment, _) => {
            "not_applicable"
        }
    };
    stats.reflection_max_distance_status = match (quality.reflection, resolved.reflection) {
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::Environment) => {
            "requested_ssr_distance_recorded_not_applied_environment_fallback"
        }
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::Disabled) => {
            "requested_ssr_distance_recorded_not_applied_disabled_fallback"
        }
        (QualityWaterReflection::ScreenSpace, ResolvedWaterReflectionPath::ScreenSpace { .. }) => {
            "requested_ssr_distance_recorded_gpu_not_observed"
        }
        (QualityWaterReflection::Disabled | QualityWaterReflection::Environment, _) => {
            "not_applicable"
        }
    };
    stats.requested_refraction_strength = match quality.refraction {
        QualityWaterRefraction::Disabled => None,
        QualityWaterRefraction::SurfaceTint | QualityWaterRefraction::ScreenSpace => {
            requested_refraction_strength(water)
        }
    };
    stats.requested_refraction_steps = quality.refraction_steps;
    stats.requested_refraction_max_depth_m = match quality.refraction {
        QualityWaterRefraction::ScreenSpace => Some(quality.refraction_max_depth_m),
        QualityWaterRefraction::Disabled | QualityWaterRefraction::SurfaceTint => None,
    };
    stats.applied_surface_tint_strength = matches!(
        resolved.refraction,
        ResolvedWaterRefractionPath::SurfaceTint
    )
    .then_some(stats.requested_refraction_strength)
    .flatten();
    stats.refraction_status = match (quality.refraction, resolved.refraction) {
        (QualityWaterRefraction::Disabled, _) => "quality_profile_disabled",
        (QualityWaterRefraction::SurfaceTint, ResolvedWaterRefractionPath::SurfaceTint) => {
            "quality_profile_surface_tint_applied"
        }
        (QualityWaterRefraction::ScreenSpace, ResolvedWaterRefractionPath::SurfaceTint) => {
            "quality_profile_screen_space_unavailable_resolved_surface_tint"
        }
        (_, ResolvedWaterRefractionPath::Disabled) => {
            "quality_profile_requested_unavailable_resolved_disabled"
        }
        (_, ResolvedWaterRefractionPath::ScreenSpace { .. }) => {
            "quality_profile_bevy_native_screen_space_transmission_configured_gpu_not_observed"
        }
    };
    stats.resolved_optics = resolved;
    let applied = applied_water_optics(stats);
    stats.applied_material_reflectance = matches!(
        stats.resolved_optics.reflection,
        ResolvedWaterReflectionPath::Environment | ResolvedWaterReflectionPath::ScreenSpace { .. }
    )
    .then_some(applied.reflectance);
    stats.applied_surface_tint_strength = matches!(
        stats.resolved_optics.refraction,
        ResolvedWaterRefractionPath::SurfaceTint
    )
    .then_some(applied.surface_tint_strength);
    stats.applied_specular_transmission = matches!(
        stats.resolved_optics.refraction,
        ResolvedWaterRefractionPath::ScreenSpace { .. }
    )
    .then_some(applied.specular_transmission);
    stats.applied_uniform_thickness_m = matches!(
        stats.resolved_optics.refraction,
        ResolvedWaterRefractionPath::ScreenSpace { .. }
    )
    .then_some(applied.uniform_thickness_m);
    stats.screen_space_transmission_steps = applied.transmission_steps;
    stats.screen_space_transmission_quality = applied.transmission_quality_name;
    stats.screen_space_transmission_blur_taps = applied.transmission_blur_taps;
    stats.refraction_steps_status = match quality.refraction {
        QualityWaterRefraction::ScreenSpace => {
            "requested_ray_steps_recorded_not_mapped_to_bevy_layer_copies_quality_mapped_to_blur_taps"
        }
        QualityWaterRefraction::Disabled | QualityWaterRefraction::SurfaceTint => "not_applicable",
    };
    stats.refraction_max_depth_status = match stats.resolved_optics.refraction {
        ResolvedWaterRefractionPath::ScreenSpace { .. } => {
            "requested_depth_bounded_to_sampled_bed_and_mapped_to_uniform_material_thickness"
        }
        ResolvedWaterRefractionPath::Disabled | ResolvedWaterRefractionPath::SurfaceTint => {
            "not_applicable"
        }
    };
    stats.requested_ssr_component_prepared = false;
    stats.environment_reflection_status = if matches!(
        stats.resolved_optics.reflection,
        ResolvedWaterReflectionPath::Environment
    ) {
        "ibl_source_component_not_observed_yet_gpu_not_observed"
    } else {
        "not_applicable"
    };
    stats.screen_space_transmission_status = if applied.transmission_steps > 0 {
        "camera_component_not_observed_yet_gpu_not_observed"
    } else {
        "disabled_by_resolved_quality_tier"
    };
}

fn lane_label(index: usize) -> &'static str {
    match index {
        0 => "left_bank",
        1 => "centre",
        2 => "right_bank",
        _ => "invalid_lane",
    }
}

fn bank_lane_label(index: usize) -> &'static str {
    match index {
        0 => "left_bank_outer",
        1 => "right_bank_outer",
        _ => "invalid_bank_lane",
    }
}

fn lane_uv(index: usize) -> f32 {
    match index {
        1 => 0.5,
        2 => 1.0,
        _ => 0.0,
    }
}

fn bank_lane_uv(index: usize) -> f32 {
    match index {
        1 | 3 => 1.0,
        _ => 0.0,
    }
}

fn spawn_water_surface(
    mut commands: Commands,
    mut prepared: ResMut<PreparedWaterSurface>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut water_materials: ResMut<Assets<VisualValleyWaterMaterial>>,
    mut standard_materials: ResMut<Assets<StandardMaterial>>,
) {
    let prepared = prepared
        .0
        .take()
        .expect("water preparation is consumed exactly once at startup");
    let water_mesh = mesh_from_geometry(prepared.water_geometry);
    let river_bank_mesh = mesh_from_geometry(prepared.river_bank_geometry);
    let river_bank_name = format!("{} banks", prepared.name);
    let water_material = water_materials.add(prepared.water_material);
    commands.spawn((
        Name::new(prepared.name),
        VisualValleyWaterSurface,
        Mesh3d(meshes.add(water_mesh)),
        MeshMaterial3d(water_material.clone()),
        Transform::IDENTITY,
    ));
    commands.spawn((
        Name::new(river_bank_name),
        VisualValleyRiverBankSurface,
        Mesh3d(meshes.add(river_bank_mesh)),
        MeshMaterial3d(standard_materials.add(prepared.river_bank_material)),
        Transform::IDENTITY,
    ));
    commands.insert_resource(WaterSurfaceMaterial(water_material));
}

fn mesh_from_geometry(geometry: RiverSurfaceGeometry) -> Mesh {
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, geometry.positions)
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, geometry.normals)
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, geometry.uvs);
    if let Some(colors) = geometry.colors {
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, colors);
    }
    mesh.with_inserted_indices(Indices::U32(geometry.indices))
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers"
)]
fn advance_water_runtime(time: Res<Time>, mut state: ResMut<WaterRuntimeState>) {
    advance_runtime_state(&mut state, time.delta_secs());
}

fn advance_runtime_state(state: &mut WaterRuntimeState, delta_seconds: f32) {
    if !delta_seconds.is_finite() || delta_seconds < 0.0 {
        return;
    }
    state.previous_phase_radians = state.current_phase_radians;
    state.current_phase_radians = (state.current_phase_radians
        + state.angular_speed_radians_per_second * f64::from(delta_seconds))
    .rem_euclid(WATER_SHADER_PHASE_WRAP_RADIANS);
    state.updates = state.updates.saturating_add(1);
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::needless_pass_by_value,
    reason = "Bevy system wrappers are owned and wrapped phases are f32-safe"
)]
fn project_water_animation(
    state: Res<WaterRuntimeState>,
    material: Option<Res<WaterSurfaceMaterial>>,
    mut materials: ResMut<Assets<VisualValleyWaterMaterial>>,
) {
    let Some(material) = material else {
        return;
    };
    let Some(mut material) = materials.get_mut(&material.0) else {
        return;
    };
    material.extension.flow_phase.z = state.current_phase_radians as f32;
    material.extension.flow_phase.w = state.previous_phase_radians as f32;
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems receive Res by value as a system parameter"
)]
fn configure_water_cameras(
    mut commands: Commands,
    cameras: Query<Entity, With<Camera3d>>,
    prepared: Res<PreparedWaterCamera>,
) {
    for camera in &cameras {
        let mut entity = commands.entity(camera);
        entity.insert((Msaa::Sample4, prepared.transmission.clone()));
        entity.remove::<(ScreenSpaceReflections, DeferredPrepass)>();
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::type_complexity,
    reason = "the water quality projection reconciles one material and independently auditable camera components"
)]
fn synchronize_water_optics(
    mut commands: Commands,
    profile: Res<VisualValleyWaterProfile>,
    stats: Res<WaterProjectionStats>,
    material: Option<Res<WaterSurfaceMaterial>>,
    mut materials: ResMut<Assets<VisualValleyWaterMaterial>>,
    mut projection: ResMut<WaterMaterialProjection>,
    mut applied: ResMut<AppliedWaterOpticsState>,
    cameras: Query<
        (
            Entity,
            Option<&ScreenSpaceTransmission>,
            Option<&Msaa>,
            Has<ScreenSpaceReflections>,
            Has<DeferredPrepass>,
        ),
        With<Camera3d>,
    >,
) {
    let expected_optics = applied_water_optics(&stats);
    if applied.0 != expected_optics {
        if let Some(material) = material
            && let Some(mut material) = materials.get_mut(&material.0)
        {
            apply_water_optics_to_material(&mut material, &profile.document, &stats);
            projection.0 = material.extension.clone();
        }
        applied.0 = expected_optics;
    }

    let expected_transmission = screen_space_transmission_configuration(&stats);
    for (camera, transmission, msaa, has_ssr, has_deferred) in &cameras {
        let mut entity = commands.entity(camera);
        if transmission.is_none()
            || transmission.is_some_and(|current| {
                current.steps != expected_transmission.steps
                    || current.quality != expected_transmission.quality
            })
        {
            entity.insert(expected_transmission.clone());
        }
        if msaa != Some(&Msaa::Sample4) {
            entity.insert(Msaa::Sample4);
        }
        if has_ssr || has_deferred {
            entity.remove::<(ScreenSpaceReflections, DeferredPrepass)>();
        }
    }
}

#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    reason = "Bevy system wrappers and independent ECS queries keep evidence auditable"
)]
fn observe_water_components(
    surfaces: Query<Entity, With<VisualValleyWaterSurface>>,
    surface_materials: Query<
        &MeshMaterial3d<VisualValleyWaterMaterial>,
        With<VisualValleyWaterSurface>,
    >,
    river_banks: Query<Entity, With<VisualValleyRiverBankSurface>>,
    cameras: Query<Entity, With<Camera3d>>,
    deferred_cameras: Query<Entity, (With<Camera3d>, With<DeferredPrepass>)>,
    ssr_cameras: Query<Entity, (With<Camera3d>, With<ScreenSpaceReflections>)>,
    msaa_cameras: Query<&Msaa, With<Camera3d>>,
    transmission_cameras: Query<&ScreenSpaceTransmission, With<Camera3d>>,
    atmosphere_environment_cameras: Query<
        Entity,
        (With<Camera3d>, With<AtmosphereEnvironmentMapLight>),
    >,
    materials: Res<Assets<VisualValleyWaterMaterial>>,
    profile: Res<VisualValleyWaterProfile>,
    projection: Res<WaterMaterialProjection>,
    runtime: Res<WaterRuntimeState>,
    mut stats: ResMut<WaterProjectionStats>,
) {
    stats.water_mesh_entities = surfaces.iter().count();
    stats.river_bank_mesh_entities = river_banks.iter().count();
    stats.camera_3d_entities = cameras.iter().count();
    stats.deferred_camera_entities = deferred_cameras.iter().count();
    stats.ssr_camera_entities = ssr_cameras.iter().count();
    stats.msaa_off_camera_entities = msaa_cameras
        .iter()
        .filter(|msaa| **msaa == Msaa::Off)
        .count();
    stats.msaa_sample4_camera_entities = msaa_cameras
        .iter()
        .filter(|msaa| **msaa == Msaa::Sample4)
        .count();
    stats.transmission_disabled_camera_entities = transmission_cameras
        .iter()
        .filter(|transmission| transmission.steps == 0)
        .count();
    stats.transmission_enabled_camera_entities = transmission_cameras
        .iter()
        .filter(|transmission| transmission.steps > 0)
        .count();
    stats.atmosphere_environment_camera_entities = atmosphere_environment_cameras.iter().count();
    stats.shader_material_assets = materials.len();
    stats.shader_material_entities = surface_materials.iter().count();
    let expected_extension = projected_water_extension(&projection.0, &runtime);
    let expected_optics = applied_water_optics(&stats);
    let expected_surface_color =
        water_surface_color(&profile.document, expected_optics.surface_tint_strength);
    stats.shader_uniforms_match_profile = stats.shader_material_entities == 1
        && surface_materials.iter().all(|handle| {
            materials.get(&handle.0).is_some_and(|material| {
                material.extension == expected_extension
                    && material.base.base_color == expected_surface_color
                    && material.base.reflectance.to_bits() == expected_optics.reflectance.to_bits()
                    && material.base.specular_transmission.to_bits()
                        == expected_optics.specular_transmission.to_bits()
                    && material.base.thickness.to_bits()
                        == expected_optics.uniform_thickness_m.to_bits()
            })
        });
    stats.animation_updates = runtime.updates;
    stats.animation_uniform_status = if stats.shader_uniforms_match_profile {
        "cpu_uniform_projected_to_material_asset_gpu_not_observed"
    } else {
        "cpu_uniform_projection_not_observed"
    };
    stats.shader_material_status = if stats.shader_uniforms_match_profile {
        "observed_cpu_material_asset_gpu_pipeline_not_observed"
    } else {
        "cpu_material_asset_incomplete_or_not_observed"
    };
    let expected_transmission = screen_space_transmission_configuration(&stats);
    let transmission_cameras_match = transmission_cameras.iter().count()
        == stats.camera_3d_entities
        && transmission_cameras.iter().all(|transmission| {
            transmission.steps == expected_transmission.steps
                && transmission.quality == expected_transmission.quality
        });
    stats.screen_space_transmission_status = if transmission_cameras_match {
        if expected_transmission.steps > 0 {
            "observed_main_world_bevy_screen_space_transmission_component_gpu_not_observed"
        } else {
            "observed_main_world_screen_space_transmission_disabled"
        }
    } else {
        "main_world_screen_space_transmission_component_incomplete_gpu_not_observed"
    };
    let environment_reflection_expected = matches!(
        stats.resolved_optics.reflection,
        ResolvedWaterReflectionPath::Environment
    );
    let environment_sources_match = !environment_reflection_expected
        || stats.atmosphere_environment_camera_entities == stats.camera_3d_entities;
    stats.environment_reflection_status = if !environment_reflection_expected {
        "not_applicable"
    } else if !environment_sources_match {
        "main_world_atmosphere_ibl_source_component_incomplete_gpu_not_observed"
    } else {
        "observed_main_world_atmosphere_ibl_source_component_filtering_not_observed_gpu_not_observed"
    };
    let forward_camera_components_match = stats.deferred_camera_entities == 0
        && stats.ssr_camera_entities == 0
        && stats.msaa_off_camera_entities == 0
        && stats.msaa_sample4_camera_entities == stats.camera_3d_entities;
    stats.runtime_observation = if stats.water_mesh_entities == 1
        && stats.river_bank_mesh_entities == 1
        && stats.shader_material_entities == 1
        && stats.shader_uniforms_match_profile
        && stats.camera_3d_entities > 0
        && forward_camera_components_match
        && transmission_cameras_match
        && environment_sources_match
    {
        WaterRuntimeObservationStatus::ObservedMainWorldComponents
    } else {
        WaterRuntimeObservationStatus::ObservedIncompleteComponents
    };
    stats.cpu_ecs_application_status = if stats.runtime_observation
        == WaterRuntimeObservationStatus::ObservedMainWorldComponents
    {
        "observed_main_world_components_and_cpu_material_asset"
    } else {
        "incomplete_main_world_or_cpu_material_observation"
    };
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "wrapped shader phases are bounded to one f32-safe revolution"
)]
fn projected_water_extension(
    projection: &VisualValleyWaterExtension,
    runtime: &WaterRuntimeState,
) -> VisualValleyWaterExtension {
    let mut projected = projection.clone();
    projected.flow_phase.z = runtime.current_phase_radians as f32;
    projected.flow_phase.w = runtime.previous_phase_radians as f32;
    projected
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{VisualValleyQualityPlugin, VisualValleyQualitySelection};
    use forge_project::{
        ResolvedWaterReflectionPath, VisualQualityTier, VisualValleyQualityProfileDocument,
    };
    use forge_world::{
        embedded_visual_valley_terrain_document, embedded_visual_valley_water_document,
    };

    fn prepared() -> PreparedWaterScene {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        prepare_water_surface(&terrain, &water).unwrap()
    }

    #[test]
    fn real_sixty_two_control_route_projects_three_lanes_independently() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let terrain_plan = TerrainGenerationPlan::from_document(&terrain).unwrap();
        let prepared = prepared();
        let stats = &prepared.stats;

        assert_eq!(stats.authored_control_points, 62);
        assert_eq!(stats.centerline_samples, 137);
        assert_eq!(stats.water_surface_queries, stats.centerline_samples * 3);
        assert_eq!(
            stats.river_bank_surface_queries,
            stats.centerline_samples * 2
        );
        assert_eq!(
            stats.terrain_surface_queries,
            stats.water_surface_queries + stats.river_bank_surface_queries
        );
        assert_eq!(stats.water_vertices, stats.centerline_samples * 3);
        assert_eq!(stats.water_triangles, (stats.centerline_samples - 1) * 4);
        assert_eq!(stats.river_bank_vertices, stats.centerline_samples * 4);
        assert_eq!(
            stats.river_bank_triangles,
            (stats.centerline_samples - 1) * 4
        );
        assert_eq!(
            stats.vertices,
            stats.water_vertices + stats.river_bank_vertices
        );
        assert_eq!(
            stats.triangles,
            stats.water_triangles + stats.river_bank_triangles
        );
        assert!(stats.terrain_projection_lifts > 0);
        assert!((stats.terrain_clearance_m - VISUAL_VALLEY_WATER_CLEARANCE_M).abs() < f32::EPSILON);
        assert!(stats.terrain_clearance_m > stats.requested_wave_amplitude_m);
        assert!(stats.minimum_observed_clearance_m + 1.0e-5 >= stats.terrain_clearance_m);

        for (position, recorded_height) in prepared
            .water_geometry
            .positions
            .iter()
            .zip(&prepared.water_geometry.terrain_heights)
        {
            let sampled = terrain_plan.surface_at(position[0], position[2]).unwrap();
            assert_eq!(sampled.height_m.to_bits(), recorded_height.to_bits());
            assert!(position[1] - sampled.height_m + 1.0e-5 >= stats.terrain_clearance_m);
        }
        for (position, recorded_height) in prepared
            .river_bank_geometry
            .positions
            .iter()
            .zip(&prepared.river_bank_geometry.terrain_heights)
        {
            let sampled = terrain_plan.surface_at(position[0], position[2]).unwrap();
            assert_eq!(sampled.height_m.to_bits(), recorded_height.to_bits());
        }
        assert!(
            prepared
                .water_geometry
                .terrain_heights
                .as_chunks::<3>()
                .0
                .iter()
                .any(|lanes| {
                    lanes[0].to_bits() != lanes[1].to_bits()
                        || lanes[1].to_bits() != lanes[2].to_bits()
                })
        );
    }

    #[test]
    fn projected_lanes_are_monotone_and_every_triangle_has_positive_winding() {
        let prepared = prepared();
        assert!(prepared.stats.authored_centerline_monotone_downstream);
        assert!(prepared.stats.projected_lanes_monotone_downstream);
        assert_eq!(
            prepared.stats.positive_winding_triangles,
            prepared.stats.triangles
        );
        assert_geometry_has_positive_winding(&prepared.water_geometry);
        assert_geometry_has_positive_winding(&prepared.river_bank_geometry);
    }

    #[test]
    fn tangent_fallback_handles_a_reversing_neighbourhood() {
        let sample = |x: f32| RiverCenterlineSample {
            center_m: forge_core::Vec3::new(x, 1.0, 0.0),
            surface_width_m: 1.0,
            bed_depth_m: 1.0,
            source_segment_index: 0,
            segment_fraction: 0.0,
        };
        let samples = [sample(-1.0), sample(0.0), sample(-1.0)];
        let (tangent, fallback) = robust_tangent(&samples, 1).unwrap();
        assert!(fallback);
        assert!((tangent[0] + 1.0).abs() < f32::EPSILON);
        assert!(tangent[1].abs() < f32::EPSILON);
    }

    #[test]
    fn checked_mesh_arithmetic_rejects_impossible_sizes() {
        assert!(matches!(
            checked_mesh_counts(usize::MAX),
            Err(WaterAdapterError::ArithmeticOverflow(_))
        ));
    }

    #[test]
    fn material_extension_maps_ibl_and_native_transmission_without_claiming_gpu_execution() {
        let prepared = prepared();
        assert_eq!(prepared.water_material.base.alpha_mode, AlphaMode::Opaque);
        assert_eq!(
            prepared.water_material.base.opaque_render_method,
            OpaqueRendererMethod::Forward
        );
        assert_eq!(
            prepared.water_material.base.specular_transmission.to_bits(),
            0.18_f32.to_bits()
        );
        assert_eq!(
            prepared.water_material.base.diffuse_transmission.to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(
            prepared.water_material.base.ior.to_bits(),
            WATER_IOR.to_bits()
        );
        assert_eq!(
            prepared.water_material.base.reflectance.to_bits(),
            0.72_f32.to_bits()
        );
        assert_eq!(
            prepared.water_material.base.thickness.to_bits(),
            3.2_f32.to_bits()
        );
        assert_eq!(prepared.river_bank_material.alpha_mode, AlphaMode::Opaque);
        let bank = prepared.river_bank_material.base_color.to_linear();
        assert_eq!(bank.red.to_bits(), 0.24_f32.to_bits());
        assert_eq!(bank.green.to_bits(), 0.105_f32.to_bits());
        assert_eq!(bank.blue.to_bits(), 0.035_f32.to_bits());
        assert_eq!(bank.alpha.to_bits(), 1.0_f32.to_bits());
        assert_eq!(
            prepared.stats.bank_material_status,
            "opaque_forward_matches_terrain_earth_palette"
        );
        assert_eq!(prepared.stats.applied_surface_tint_strength, None);
        assert_eq!(prepared.stats.applied_material_reflectance, Some(0.72));
        assert_eq!(prepared.stats.applied_specular_transmission, Some(0.18));
        assert_eq!(prepared.stats.applied_uniform_thickness_m, Some(3.2));
        let tint = prepared.water_material.base.base_color.to_srgba();
        assert!((tint.red - 0.12).abs() < 1.0e-6);
        assert!((tint.green - 0.42).abs() < 1.0e-6);
        assert!((tint.blue - 0.46).abs() < 1.0e-6);
        let extension = &prepared.water_material.extension;
        assert_eq!(extension.wave_parameters, Vec4::new(0.12, 5.5, 0.72, 0.62));
        assert_eq!(
            extension.shallow_color_surface_tint.w.to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(
            extension.deep_color_max_depth.w.to_bits(),
            prepared.stats.maximum_sampled_bed_depth_m.to_bits()
        );
        assert!((extension.flow_phase.xy().length() - 1.0).abs() < 1.0e-6);
        assert_eq!(
            extension.optical_parameters,
            Vec4::new(0.84, 0.16, WATER_IOR, 0.72)
        );
    }

    #[test]
    fn metrics_resolve_optics_without_claiming_gpu_or_webgpu_execution() {
        let prepared = prepared();
        assert!(!prepared.stats.requested_ssr_component_prepared);
        assert_eq!(
            prepared.stats.reflection_intensity_status,
            "quality_profile_ssr_unavailable_environment_ibl_fallback_intensity_mapped_to_material_reflectance_gpu_not_observed"
        );
        assert_eq!(
            prepared.stats.wave_motion_status,
            "profile_projected_to_deterministic_vertex_wave_shader"
        );
        assert_eq!(prepared.stats.shader_asset_path, WATER_SHADER_PATH);
        assert_eq!(
            prepared.stats.bed_depth_status,
            "sampled_bed_depth_encoded_in_vertex_color_and_shader_palette"
        );
        assert_eq!(
            prepared.stats.bank_shoulder_status,
            "procedural_outer_strip_projected_from_shoulder_width"
        );
        assert_eq!(
            prepared.stats.bank_erosion_status,
            "deferred_no_terrain_carving_adapter"
        );
        assert!(!prepared.stats.gpu_pass_observed);
        assert_eq!(
            prepared.stats.native_gpu_execution_status,
            "not_observed_by_water_metrics"
        );
        assert_eq!(
            prepared.stats.webgpu_execution_status,
            "not_observed_by_water_metrics"
        );
        assert_eq!(
            prepared.stats.resolved_optics.reflection,
            ResolvedWaterReflectionPath::Environment
        );
        assert!(prepared.stats.resolved_optics.reflection_fallback_used);
        assert_eq!(
            prepared.stats.resolved_optics.refraction,
            ResolvedWaterRefractionPath::ScreenSpace { max_steps: 24 }
        );
        assert!(!prepared.stats.resolved_optics.refraction_fallback_used);
        assert_eq!(prepared.stats.requested_reflection_steps, 48);
        assert_eq!(prepared.stats.requested_refraction_steps, 24);
        assert_eq!(prepared.stats.requested_refraction_max_depth_m, Some(3.2));
        assert_eq!(prepared.stats.screen_space_transmission_steps, 1);
        assert_eq!(prepared.stats.screen_space_transmission_quality, "high");
        assert_eq!(prepared.stats.screen_space_transmission_blur_taps, 16);
        assert_eq!(
            prepared
                .stats
                .resolved_optics
                .capabilities
                .screen_space_reflections,
            WaterOpticsCapabilityStatus::Unavailable
        );
        assert_eq!(
            prepared
                .stats
                .resolved_optics
                .capabilities
                .screen_space_refraction,
            WaterOpticsCapabilityStatus::Available
        );
        assert_eq!(
            prepared
                .stats
                .resolved_optics
                .capabilities
                .environment_reflections,
            WaterOpticsCapabilityStatus::Available
        );
        let serialized = serde_json::to_value(&prepared.stats).unwrap();
        assert_eq!(serialized["gpu_pass_observed"], false);
        assert_eq!(
            serialized["native_gpu_execution_status"],
            "not_observed_by_water_metrics"
        );
        assert_eq!(
            serialized["webgpu_execution_status"],
            "not_observed_by_water_metrics"
        );
        assert!(serialized.get("draw_calls").is_none());
    }

    #[test]
    fn quality_optics_projection_tracks_all_four_tiers_without_inventing_gpu_paths() {
        let quality = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        for (
            tier,
            expected,
            expected_reflection_steps,
            expected_distance,
            reflection_fallback,
            expected_refraction,
            expected_refraction_steps,
            expected_transmission_steps,
            expected_transmission_quality,
            expected_blur_taps,
        ) in [
            (
                VisualQualityTier::Low,
                WaterQualityTier::Low,
                0,
                None,
                false,
                ResolvedWaterRefractionPath::SurfaceTint,
                0,
                0,
                "low",
                0,
            ),
            (
                VisualQualityTier::Medium,
                WaterQualityTier::Medium,
                24,
                Some(64.0),
                true,
                ResolvedWaterRefractionPath::SurfaceTint,
                0,
                0,
                "medium",
                0,
            ),
            (
                VisualQualityTier::High,
                WaterQualityTier::High,
                48,
                Some(96.0),
                true,
                ResolvedWaterRefractionPath::ScreenSpace { max_steps: 24 },
                24,
                1,
                "high",
                16,
            ),
            (
                VisualQualityTier::Ultra,
                WaterQualityTier::Ultra,
                64,
                Some(128.0),
                true,
                ResolvedWaterRefractionPath::ScreenSpace { max_steps: 40 },
                40,
                1,
                "ultra",
                32,
            ),
        ] {
            let mut stats = prepared().stats;
            apply_quality_optics_projection(&mut stats, &water, quality.preset(tier).water, tier);
            assert_eq!(stats.resolved_optics.requested_quality, expected);
            assert_eq!(stats.requested_reflection_steps, expected_reflection_steps);
            assert_eq!(stats.requested_reflection_max_distance_m, expected_distance);
            assert_eq!(
                stats.resolved_optics.reflection,
                ResolvedWaterReflectionPath::Environment
            );
            assert_eq!(
                stats.resolved_optics.reflection_fallback_used,
                reflection_fallback
            );
            assert_eq!(stats.resolved_optics.refraction, expected_refraction);
            assert!(!stats.resolved_optics.refraction_fallback_used);
            assert_eq!(stats.requested_refraction_steps, expected_refraction_steps);
            assert_eq!(
                stats.screen_space_transmission_steps,
                expected_transmission_steps
            );
            assert_eq!(
                stats.screen_space_transmission_quality,
                expected_transmission_quality
            );
            assert_eq!(
                stats.screen_space_transmission_blur_taps,
                expected_blur_taps
            );
            assert!(!stats.requested_ssr_component_prepared);
        }
    }

    #[test]
    fn camera_preparation_is_observed_without_claiming_gpu_execution() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                AtmosphereEnvironmentMapLight::default(),
                DeferredPrepass,
                ScreenSpaceReflections::default(),
            ))
            .id();
        app.add_plugins(VisualValleyWaterPlugin::new(terrain, water));
        app.update();

        let stats = app.world().resource::<WaterProjectionStats>();
        assert_eq!(
            stats.runtime_observation,
            WaterRuntimeObservationStatus::ObservedMainWorldComponents
        );
        assert_eq!(stats.camera_3d_entities, 1);
        assert_eq!(stats.deferred_camera_entities, 0);
        assert_eq!(stats.ssr_camera_entities, 0);
        assert_eq!(stats.msaa_off_camera_entities, 0);
        assert_eq!(stats.msaa_sample4_camera_entities, 1);
        assert_eq!(stats.transmission_disabled_camera_entities, 0);
        assert_eq!(stats.transmission_enabled_camera_entities, 1);
        assert_eq!(stats.atmosphere_environment_camera_entities, 1);
        assert_eq!(stats.shader_material_assets, 1);
        assert_eq!(stats.shader_material_entities, 1);
        assert!(stats.shader_uniforms_match_profile);
        assert_eq!(stats.animation_updates, 1);
        assert_eq!(
            stats.cpu_ecs_application_status,
            "observed_main_world_components_and_cpu_material_asset"
        );
        assert!(!stats.gpu_pass_observed);
        assert_eq!(app.world().get::<Msaa>(camera), Some(&Msaa::Sample4));
        let transmission = app.world().get::<ScreenSpaceTransmission>(camera).unwrap();
        assert_eq!(transmission.steps, 1);
        assert_eq!(transmission.quality, ScreenSpaceTransmissionQuality::High);
    }

    #[test]
    fn native_transmission_preparation_keeps_forward_msaa_and_removes_deferred_ssr() {
        let mut app = App::new();
        app.add_plugins(MinimalPlugins);
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                DeferredPrepass,
                ScreenSpaceReflections::default(),
            ))
            .id();
        app.insert_resource(PreparedWaterCamera {
            transmission: ScreenSpaceTransmission {
                steps: 1,
                quality: ScreenSpaceTransmissionQuality::Ultra,
            },
        });
        app.add_systems(Update, configure_water_cameras);
        app.update();

        let world = app.world();
        assert_eq!(world.get::<Msaa>(camera), Some(&Msaa::Sample4));
        assert!(world.get::<DeferredPrepass>(camera).is_none());
        assert!(world.get::<ScreenSpaceReflections>(camera).is_none());
        assert_eq!(
            world
                .get::<ScreenSpaceTransmission>(camera)
                .map(|transmission| transmission.steps),
            Some(1)
        );
        assert_eq!(
            world
                .get::<ScreenSpaceTransmission>(camera)
                .map(|transmission| transmission.quality),
            Some(ScreenSpaceTransmissionQuality::Ultra)
        );
    }

    #[test]
    fn high_quality_uses_environment_fallback_and_native_transmission_without_ssr() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        let quality = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                AtmosphereEnvironmentMapLight::default(),
                DeferredPrepass,
                ScreenSpaceReflections::default(),
            ))
            .id();
        app.add_plugins((
            VisualValleyWaterPlugin::new(terrain, water),
            VisualValleyQualityPlugin::new(
                quality,
                VisualValleyQualitySelection::Fixed(VisualQualityTier::High),
            ),
        ));
        app.update();
        app.update();

        let world = app.world();
        assert!(world.get::<ScreenSpaceReflections>(camera).is_none());
        assert!(world.get::<DeferredPrepass>(camera).is_none());
        let stats = world.resource::<WaterProjectionStats>();
        assert_eq!(
            stats.runtime_observation,
            WaterRuntimeObservationStatus::ObservedMainWorldComponents
        );
        assert_eq!(stats.ssr_camera_entities, 0);
        assert_eq!(stats.deferred_camera_entities, 0);
        assert_eq!(stats.msaa_off_camera_entities, 0);
        assert_eq!(stats.msaa_sample4_camera_entities, 1);
        assert_eq!(stats.transmission_enabled_camera_entities, 1);
        assert_eq!(stats.screen_space_transmission_steps, 1);
        assert_eq!(stats.screen_space_transmission_quality, "high");
        assert_eq!(world.get::<Msaa>(camera), Some(&Msaa::Sample4));
        assert_eq!(
            world
                .get::<ScreenSpaceTransmission>(camera)
                .map(|transmission| (transmission.steps, transmission.quality)),
            Some((1, ScreenSpaceTransmissionQuality::High))
        );
    }

    #[test]
    fn bed_depth_is_encoded_at_each_center_lane_without_changing_projection() {
        let water = embedded_visual_valley_water_document().unwrap();
        let samples = sample_river_centerline(&water).unwrap();
        let prepared = prepared();
        let colors = prepared.water_geometry.colors.as_ref().unwrap();
        assert_eq!(colors.len(), samples.len() * RIVER_LANE_COUNT);
        for (sample, lanes) in samples.iter().zip(colors.as_chunks::<3>().0) {
            assert_eq!(lanes[0][0].to_bits(), 0.0_f32.to_bits());
            assert_eq!(lanes[1][0].to_bits(), sample.bed_depth_m.to_bits());
            assert_eq!(lanes[2][0].to_bits(), 0.0_f32.to_bits());
            assert!(
                lanes
                    .iter()
                    .all(|color| color[3].to_bits() == 1.0_f32.to_bits())
            );
        }
        assert!(prepared.river_bank_geometry.colors.is_none());
    }

    #[test]
    fn shader_contract_contains_profile_waves_depth_normals_and_deferred_output() {
        let shader = include_str!("shaders/visual_valley_water.wgsl");
        for required in [
            "struct VisualValleyWaterExtension",
            "fn wave_response",
            "@vertex",
            "@fragment",
            "MOTION_VECTOR_PREPASS",
            "previous_world_position",
            "sampled_bed_depth",
            "depth_response",
            "normal_strength",
            "deferred_output",
            "apply_pbr_lighting",
        ] {
            assert!(
                shader.contains(required),
                "missing shader contract: {required}"
            );
        }
        assert!(!shader.contains("textureSample"));
    }

    #[test]
    fn runtime_phase_is_seeded_repeatable_wrapped_and_profile_driven() {
        let water = embedded_visual_valley_water_document().unwrap();
        let mut first = WaterRuntimeState::from_profile(&water);
        let mut second = WaterRuntimeState::from_profile(&water);
        assert_eq!(
            first.current_phase_radians.to_bits(),
            second.current_phase_radians.to_bits()
        );
        let initial = first.current_phase_radians;
        advance_runtime_state(&mut first, 1.25);
        advance_runtime_state(&mut second, 1.25);
        assert_eq!(
            first.current_phase_radians.to_bits(),
            second.current_phase_radians.to_bits()
        );
        assert_eq!(first.previous_phase_radians.to_bits(), initial.to_bits());
        let expected = (initial
            + WATER_SHADER_PHASE_WRAP_RADIANS * f64::from(water.surface.flow_speed_mps)
                / f64::from(water.surface.wave_length_m)
                * 1.25)
            .rem_euclid(WATER_SHADER_PHASE_WRAP_RADIANS);
        assert!((first.current_phase_radians - expected).abs() < f64::EPSILON);
        assert_eq!(first.updates, 1);

        let before_invalid = first.clone();
        advance_runtime_state(&mut first, -1.0);
        assert_eq!(
            first.current_phase_radians.to_bits(),
            before_invalid.current_phase_radians.to_bits()
        );
        assert_eq!(first.updates, before_invalid.updates);
        advance_runtime_state(&mut first, 100_000.0);
        assert!((0.0..WATER_SHADER_PHASE_WRAP_RADIANS).contains(&first.current_phase_radians));
    }

    fn assert_geometry_has_positive_winding(geometry: &RiverSurfaceGeometry) {
        for triangle in geometry.indices.as_chunks::<3>().0 {
            let triangle = [
                usize::try_from(triangle[0]).unwrap(),
                usize::try_from(triangle[1]).unwrap(),
                usize::try_from(triangle[2]).unwrap(),
            ];
            assert!(triangle_normal_y(&geometry.positions, triangle) > 0.0);
        }
        assert!(
            geometry
                .normals
                .iter()
                .all(|normal| { normal.iter().all(|value| value.is_finite()) && normal[1] > 0.0 })
        );
    }
}
