use bevy::{
    anti_alias::taa::TemporalAntiAliasing,
    asset::{RenderAssetUsages, embedded_asset},
    core_pipeline::prepass::DeferredPrepass,
    material::OpaqueRendererMethod,
    mesh::Indices,
    pbr::{ExtendedMaterial, MaterialExtension, ScreenSpaceReflections, ScreenSpaceTransmission},
    prelude::*,
    render::render_resource::{AsBindGroup, PrimitiveTopology},
    shader::ShaderRef,
};
use forge_project::{
    ResolvedWaterReflectionPath, ResolvedWaterRefractionPath, RiverElevationPolicy,
    TerrainDocument, WaterDocument, WaterDocumentError, WaterOpticsCapabilities,
    WaterOpticsCapabilityStatus, WaterOpticsResolution, WaterReflectionProfile,
    WaterRefractionProfile,
};
use forge_world::{
    RiverCenterlineSample, TerrainGenerationError, TerrainGenerationPlan, sample_river_centerline,
};
use serde::Serialize;
use std::{error::Error, fmt};

#[cfg(feature = "physics-rapier")]
pub mod authoring;
mod banks;
#[cfg(test)]
mod optical_depth_tests;
pub(crate) mod quality;
pub use quality::{ForwardReflectionObservation, WaterQualityObservation, WaterQualityRequest};

/// Minimum Gate 1 adapter clearance used until the persistent water contract owns this value.
pub const VISUAL_VALLEY_WATER_CLEARANCE_M: f32 = 0.14;

const WATER_SHADER_PATH: &str = "embedded://forge_engine/shaders/visual_valley_water.wgsl";
const WATER_SHADER_PHASE_WRAP_RADIANS: f64 = std::f64::consts::TAU;
const WATER_IOR: f32 = 1.33;
const WATER_REFLECTANCE: f32 = 0.36;

/// Explicit unqualified optical candidate. Does not change authored sources or quality policy.
#[derive(Resource)]
pub struct WaterDepthOpticsPreview;

/// Bounded native experiment; not an authored quality profile or a qualified SSR path.
#[derive(Resource)]
pub struct WaterReflectionOpticsPreview;

const RIVER_LANE_COUNT: usize = 3;
const RIVER_BANK_LANE_COUNT: usize = 2;
const RIVER_BANK_VERTICES_PER_SECTION: usize = 4;
const TRIANGLES_PER_SECTION_PAIR: usize = 4;
const BANK_AND_BED_TRIANGLES_PER_SECTION_PAIR: usize = 6;
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
    pub optical_depth_preview: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub forward_reflection_quality: Option<ForwardReflectionObservation>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub quality: Option<WaterQualityObservation>,
    pub source_hash: String,
    pub terrain_source_hash: String,
    pub road_surface_hash: Option<String>,
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
    pub elevation_policy: RiverElevationPolicy,
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
    pub requested_reflection_max_distance_m: Option<f32>,
    pub reflection_intensity_status: &'static str,
    pub reflection_max_distance_status: &'static str,
    pub requested_refraction_strength: Option<f32>,
    pub applied_surface_tint_strength: Option<f32>,
    pub refraction_status: &'static str,
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
    pub transmission_disabled_camera_entities: usize,
    pub cpu_ecs_application_status: &'static str,
    pub gpu_pass_observed: bool,
    pub native_gpu_execution_status: &'static str,
    pub webgpu_execution_status: &'static str,
    pub evidence_source: &'static str,
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
    /// Requested opacity, authored roughness, water IOR, and non-metal reflectance.
    #[uniform(100)]
    pub optical_parameters: Vec4,
    /// Linear absorption per metre; W explicitly selects the medium candidate.
    #[uniform(100)]
    pub absorption_medium: Vec4,
    /// Linear scattering per metre; W reserved as zero.
    #[uniform(100)]
    pub scattering_medium: Vec4,
    /// Experimental trace steps, maximum distance, depth tolerance, enabled flag.
    #[uniform(100)]
    pub reflection_preview: Vec4,
    /// Experimental refracted trace: steps, path limit, hit tolerance, enabled.
    #[uniform(100)]
    pub refraction_preview: Vec4,
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
    road_surface: Option<forge_world::RoadSurfaceProjection>,
}

impl VisualValleyWaterPlugin {
    #[must_use]
    pub fn new(terrain: TerrainDocument, water: WaterDocument) -> Self {
        Self {
            terrain,
            water,
            road_surface: None,
        }
    }

    pub fn with_road_surface(
        mut self,
        surface: forge_world::RoadSurfaceProjection,
    ) -> Result<Self, forge_world::RoadSurfaceError> {
        surface.validate_terrain(&self.terrain)?;
        self.road_surface = Some(surface);
        Ok(self)
    }
}

impl Plugin for VisualValleyWaterPlugin {
    fn build(&self, app: &mut App) {
        let mut prepared =
            prepare_water_surface(&self.terrain, &self.water).unwrap_or_else(|error| {
                panic!("VisualValleyWaterPlugin requires compatible terrain and water: {error}")
            });
        if let Some(surface) = &self.road_surface {
            prepared
                .apply_road_surface(surface)
                .unwrap_or_else(|error| {
                    panic!("validated bank road surface projection failed: {error}")
                });
        }
        if self.water.medium.is_some() {
            app.insert_resource(WaterDepthOpticsPreview);
        }
        if app.world().contains_resource::<WaterDepthOpticsPreview>() {
            prepared.enable_depth_optics_preview(&self.water);
            tracing::warn!(
                "Water depth optics preview: transmissive forward water; reflection path reported separately, authored SSR and full quality profiles NOT applied"
            );
        }
        if prepared.stats.optical_depth_preview
            && app
                .world()
                .contains_resource::<WaterReflectionOpticsPreview>()
        {
            prepared.water_material.extension.reflection_preview = Vec4::new(32.0, 64.0, 0.2, 1.0);
            prepared.stats.forward_material_ssr_limit =
                "experimental_forward_opaque_scene_reflections_not_qualified";
        }
        let stats = prepared.stats.clone();
        let ssr = prepared.ssr.clone();
        let material_projection = prepared.water_material.extension.clone();

        register_water_assets(app);
        app.insert_resource(stats)
            .insert_resource(WaterRuntimeState::from_profile(&self.water))
            .insert_resource(WaterMaterialProjection(material_projection))
            .insert_resource(PreparedWaterSurface(Some(prepared)))
            .insert_resource(PreparedWaterCamera { ssr })
            .add_systems(Startup, spawn_water_surface)
            .add_systems(
                Update,
                (
                    advance_water_runtime,
                    project_water_animation,
                    quality::apply_water_quality,
                )
                    .chain(),
            )
            .add_systems(
                PostStartup,
                (configure_water_cameras, observe_water_components).chain(),
            )
            .add_systems(PostUpdate, observe_water_components);
    }
}

fn register_water_assets(app: &mut App) {
    if app.is_plugin_added::<MaterialPlugin<VisualValleyWaterMaterial>>() {
        return;
    }
    embedded_asset!(app, "shaders/visual_valley_water.wgsl");
    embedded_asset!(app, "shaders/water_optical_depth.wgsl");
    embedded_asset!(app, "shaders/water_flow_detail.wgsl");
    embedded_asset!(app, "shaders/water_reflection_preview.wgsl");
    crate::visual_valley::register_terrain_material(app);
    app.add_plugins(MaterialPlugin::<VisualValleyWaterMaterial>::default());
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
    BankProjectionBudgetExceeded,
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
            Self::BankProjectionBudgetExceeded => write!(
                formatter,
                "terrain-conforming bank projection exceeds its cell or vertex budget"
            ),
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
            | Self::BankProjectionBudgetExceeded
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
    ssr: Option<ScreenSpaceReflections>,
}

struct PreparedWaterScene {
    name: String,
    water_geometry: RiverSurfaceGeometry,
    river_bank_geometry: RiverSurfaceGeometry,
    water_material: VisualValleyWaterMaterial,
    river_bank_material: crate::VisualValleyTerrainMaterial,
    stats: WaterProjectionStats,
    ssr: Option<ScreenSpaceReflections>,
}

impl PreparedWaterScene {
    fn enable_depth_optics_preview(&mut self, water: &WaterDocument) {
        self.water_material.base.opaque_render_method = OpaqueRendererMethod::Forward;
        self.water_material.base.specular_transmission = 1.0;
        self.water_material.base.thickness = 1.0;
        self.ssr = None;
        self.stats.optical_depth_preview = true;
        self.stats.opaque_renderer_method = "forward_transmissive_optical_preview";
        self.stats.refraction_status = "experimental_scene_depth_transmission_not_qualified";
        if water.medium.is_some() {
            self.stats.refraction_status = "experimental_rgb_medium_transmission_not_qualified";
            self.stats.opacity_status = "rgb_extinction_coefficients_replace_legacy_opacity";
        }
        self.stats.forward_material_ssr_limit = "preview_environment_only_no_water_ssr";
        self.stats.requested_ssr_component_prepared = false;
        if let WaterRefractionProfile::ScreenSpace { max_depth_m, .. } = water.optics.refraction {
            self.water_material.extension.deep_color_max_depth.w = max_depth_m;
        }
    }

    fn apply_road_surface(
        &mut self,
        road: &forge_world::RoadSurfaceProjection,
    ) -> Result<(), forge_world::RoadSurfaceError> {
        let bank = &mut self.river_bank_geometry;
        let colors = bank
            .colors
            .as_mut()
            .expect("prepared banks always have material weights");
        for (position, weights) in bank.positions.iter().zip(colors) {
            let coverage = road.coverage_at(position[0], position[2])?;
            weights[0] *= 1.0 - coverage;
            weights[1] = weights[1] * (1.0 - coverage) + coverage;
            weights[2] *= 1.0 - coverage;
        }
        self.stats.road_surface_hash = Some(road.source_hash().to_string());
        tracing::info!(event_kind = "bank_road_surface_projected",
            road_surface_hash = %road.source_hash(), vertices = bank.positions.len(),
            "road overlay applied to bank material weights; geometry and sources unchanged");
        Ok(())
    }
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
        BANK_AND_BED_TRIANGLES_PER_SECTION_PAIR,
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
    let origin = terrain_plan.origin_xz_m();
    let clip_to_terrain = |center: [f32; 2], outer: [f32; 2]| {
        let maximum = [
            origin[0] + terrain.extent_m.size_x_m,
            origin[1] + terrain.extent_m.size_z_m,
        ];
        let delta = [outer[0] - center[0], outer[1] - center[1]];
        let mut fraction = 1.0_f32;
        for axis in 0..2 {
            if outer[axis] > maximum[axis] && delta[axis] > 0.0 {
                fraction = fraction.min((maximum[axis] - center[axis]) / delta[axis]);
            } else if outer[axis] < origin[axis] && delta[axis] < 0.0 {
                fraction = fraction.min((origin[axis] - center[axis]) / delta[axis]);
            }
        }
        [
            center[0] + delta[0] * fraction,
            center[1] + delta[1] * fraction,
        ]
    };
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
    let preserve_authored_surface =
        water.river.elevation_policy == RiverElevationPolicy::AuthoredSurfaceV1;
    for sample_index in 0..samples.len() {
        let (tangent, used_fallback) = robust_tangent(&samples, sample_index)?;
        tangent_fallbacks += usize::from(used_fallback);
        let sample = samples[sample_index];
        let half_width_m = sample.surface_width_m * 0.5;
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
        // Fit each cross-section side along its ray, keeping a nonzero bank strip.
        // Independent coordinate clamps can collapse water and bank vertices together.
        // The authored center remains unmodified and still rejects an invalid path.
        let center = [sample.center_m.x, sample.center_m.z];
        let bank_outer_xz = bank_outer_xz.map(|outer| clip_to_terrain(center, outer));
        let inner_fraction = half_width_m / bank_outer_offset_m;
        let inner_xz = bank_outer_xz.map(|outer| {
            [
                center[0] + (outer[0] - center[0]) * inner_fraction,
                center[1] + (outer[1] - center[1]) * inner_fraction,
            ]
        });
        let lane_xz = [inner_xz[0], center, inner_xz[1]];
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
            let projected_y = if preserve_authored_surface {
                sample.center_m.y
            } else {
                sample.center_m.y.max(required_y)
            };
            terrain_projection_lifts += usize::from(projected_y > sample.center_m.y);
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

    let downstream_monotone_lifts = if preserve_authored_surface {
        0
    } else {
        apply_downstream_monotone_pass(&mut sections)
    };
    let (water_geometry, water_winding_corrections) =
        build_geometry(&sections, water_counts, preserve_authored_surface)?;
    let (river_bank_geometry, river_bank_winding_corrections) =
        build_river_bank_geometry(&sections, river_bank_counts)?;
    let (river_bank_geometry, clipped_winding_corrections) =
        banks::conform(&river_bank_geometry, &terrain_plan)?;
    let river_bank_counts = MeshCounts {
        vertices: river_bank_geometry.positions.len(),
        triangles: river_bank_geometry.indices.len() / 3,
        indices: river_bank_geometry.indices.len(),
    };
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
    let requested_refraction_strength = requested_refraction_strength(water);
    let applied_surface_tint_strength =
        if resolved_optics.refraction == ResolvedWaterRefractionPath::SurfaceTint {
            requested_refraction_strength
        } else {
            None
        };
    let (requested_reflection_intensity, requested_reflection_max_distance_m) =
        requested_reflection_parameters(water);
    let ssr = prepare_resolved_ssr(water, resolved_optics.reflection);
    let flow_direction = downstream_flow_direction(&samples);
    let runtime_state = WaterRuntimeState::from_profile(water);
    let water_surface_queries = water_counts.vertices;
    let river_bank_surface_queries = samples
        .len()
        .checked_mul(RIVER_BANK_LANE_COUNT)
        .and_then(|count| count.checked_add(river_bank_counts.vertices))
        .ok_or(WaterAdapterError::ArithmeticOverflow(
            "river bank surface queries",
        ))?;
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
        .and_then(|count| count.checked_add(clipped_winding_corrections))
        .ok_or(WaterAdapterError::ArithmeticOverflow("winding corrections"))?;
    let stats = WaterProjectionStats {
        optical_depth_preview: false,
        forward_reflection_quality: None,
        quality: None,
        source_hash: water.content_hash()?.to_string(),
        terrain_source_hash: terrain_plan.document_hash().to_string(),
        road_surface_hash: None,
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
        elevation_policy: water.river.elevation_policy,
        downstream_monotone_lifts,
        authored_centerline_monotone_downstream: true,
        projected_lanes_monotone_downstream: projected_lanes_are_monotone(&sections),
        material_alpha_mode: "opaque",
        opaque_renderer_method: "deferred",
        requested_opacity: water.surface.opacity,
        opacity_status: "requested_recorded_surface_forced_opaque_for_deferred_ssr",
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
        bank_slope_status: "strip_clipped_to_canonical_terrain_triangles_no_water_edge_loft",
        bank_erosion_status: "deferred_no_terrain_carving_adapter",
        bank_material_status: "terrain_palette_and_detail_earth_to_canonical_weights",
        requested_reflection_intensity,
        requested_reflection_max_distance_m,
        reflection_intensity_status: "requested_not_mapped_by_bevy_ssr_component",
        reflection_max_distance_status: "requested_not_mapped_by_bevy_ssr_component",
        requested_refraction_strength,
        applied_surface_tint_strength,
        refraction_status: "requested_screen_space_unavailable_for_opaque_deferred_resolved_surface_tint",
        requested_ssr_component_prepared: ssr.is_some(),
        forward_material_ssr_limit: "terrain_and_vegetation_remain_forward; ssr_component_is_not_runtime_qualification",
        resolved_optics,
        runtime_observation: WaterRuntimeObservationStatus::NotObserved,
        water_mesh_entities: 0,
        river_bank_mesh_entities: 0,
        camera_3d_entities: 0,
        deferred_camera_entities: 0,
        ssr_camera_entities: 0,
        msaa_off_camera_entities: 0,
        transmission_disabled_camera_entities: 0,
        cpu_ecs_application_status: "prepared_cpu_assets_not_observed",
        gpu_pass_observed: false,
        native_gpu_execution_status: "not_observed_by_water_metrics",
        webgpu_execution_status: "not_observed_by_water_metrics",
        evidence_source: "cpu_geometry_and_main_world_components_not_gpu_execution",
    };

    Ok(PreparedWaterScene {
        name: water.name.clone(),
        water_geometry,
        river_bank_geometry,
        water_material: water_material(
            water,
            applied_surface_tint_strength,
            maximum_sampled_bed_depth_m,
            flow_direction,
            &runtime_state,
        ),
        river_bank_material: river_bank_material(),
        stats,
        ssr,
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
    extend_under_banks: bool,
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

    let mut distance_m = 0.0;
    for (sample_index, section) in sections.iter().enumerate() {
        let center = Vec3::from_array(section.lanes[1].position).xz();
        if sample_index > 0 {
            let previous = Vec3::from_array(sections[sample_index - 1].lanes[1].position).xz();
            distance_m += center.distance(previous);
        }
        let left = section.lanes[0].position;
        let right = section.lanes[2].position;
        // Cross-section orientation comes from the sampled river tangent, including bends.
        // Reuse existing COLOR channels: no extra vertex stream or authored flow-map asset.
        let flow = Vec2::new(left[2] - right[2], right[0] - left[0]).normalize_or_zero();
        let across = Vec2::new(-flow.y, flow.x);
        for (lane_index, lane) in section.lanes.iter().enumerate() {
            // Authored water levels intersect the carved bank beyond the nominal
            // channel width. Keep the level, extend the footprint under terrain,
            // and let depth testing form the shoreline. Bank/collider geometry
            // stays independent; the legacy terrain-lift policy is unchanged.
            let projected = if extend_under_banks && lane_index != 1 {
                let outer = section.bank_outer_lanes[lane_index / 2];
                ProjectedLane {
                    position: [outer.position[0], lane.position[1], outer.position[2]],
                    terrain_height_m: outer.terrain_height_m,
                }
            } else {
                *lane
            };
            positions.push(projected.position);
            terrain_heights.push(projected.terrain_height_m);
            // Metric river coordinates: invariant to world translation, continuous
            // along the sampled route, including the under-bank extension.
            let lateral_m = (Vec3::from_array(projected.position).xz() - center).dot(across);
            uvs.push([distance_m, lateral_m]);
            let depth_m = if lane_index == 1 {
                section.bed_depth_m
            } else {
                0.0
            };
            colors.push([depth_m, flow.x, flow.y, 1.0]);
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
    let mut colors = Vec::new();
    let mut indices = Vec::new();
    positions
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank positions"))?;
    terrain_heights
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank terrain heights"))?;
    uvs.try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank UVs"))?;
    colors
        .try_reserve_exact(counts.vertices)
        .map_err(|_| WaterAdapterError::AllocationFailed("river bank colors"))?;
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
            colors.push(bank_vertex_color(
                vertex.position,
                matches!(vertex_index, 0 | 3),
            ));
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
        // Include the bed between the inner banks. Both inner vertices carry
        // full earth weight, so transparent water cannot expose a grass gap.
        // The subsequent terrain-grid clipping keeps this coating conforming;
        // it does not flatten the river bed or change collider heights.
        for strip_start in [0, 1, 2] {
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
            colors: Some(colors),
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
    applied_surface_tint_strength: Option<f32>,
    maximum_sampled_bed_depth_m: f32,
    flow_direction: Vec2,
    runtime_state: &WaterRuntimeState,
) -> VisualValleyWaterMaterial {
    let shallow = water.surface.shallow_color_srgb;
    let deep = water.surface.deep_color_srgb;
    let tint_strength = applied_surface_tint_strength.unwrap_or(0.0);
    let shallow_strength = 1.0 - tint_strength;
    VisualValleyWaterMaterial {
        base: StandardMaterial {
            base_color: Color::srgb(
                shallow.red * shallow_strength + deep.red * tint_strength,
                shallow.green * shallow_strength + deep.green * tint_strength,
                shallow.blue * shallow_strength + deep.blue * tint_strength,
            ),
            alpha_mode: AlphaMode::Opaque,
            metallic: 0.0,
            perceptual_roughness: water.surface.roughness,
            reflectance: WATER_REFLECTANCE,
            ior: WATER_IOR,
            diffuse_transmission: 0.0,
            specular_transmission: 0.0,
            opaque_render_method: OpaqueRendererMethod::Deferred,
            ..default()
        },
        extension: water_extension(
            water,
            tint_strength,
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
            WATER_REFLECTANCE,
        ),
        absorption_medium: water.medium.map_or(Vec4::ZERO, |medium| {
            Vec3::from_array(medium.absorption_per_m).extend(1.0)
        }),
        scattering_medium: water.medium.map_or(Vec4::ZERO, |medium| {
            Vec3::from_array(medium.scattering_per_m).extend(0.0)
        }),
        reflection_preview: Vec4::ZERO,
        refraction_preview: Vec4::ZERO,
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

fn river_bank_material() -> crate::VisualValleyTerrainMaterial {
    crate::VisualValleyTerrainMaterial {
        base: StandardMaterial {
            // Vertex RGB carries logical grass/earth/rock weights, like the terrain.
            base_color: Color::WHITE,
            alpha_mode: AlphaMode::Opaque,
            metallic: 0.0,
            perceptual_roughness: 0.86,
            opaque_render_method: OpaqueRendererMethod::Deferred,
            ..default()
        },
        extension: default(),
    }
}

fn bank_vertex_color(_position: [f32; 3], outer: bool) -> [f32; 4] {
    // Temporary clipping attribute: red is the distance fraction toward the water.
    // The conforming projection resolves it to terrain material weights.
    [if outer { 0.0 } else { 1.0 }, 0.0, 0.0, 1.0]
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

const fn bevy_water_optics_capabilities() -> WaterOpticsCapabilities {
    WaterOpticsCapabilities {
        // Bevy 0.19 exposes deferred SSR for native and the project's WebGPU path. This is a
        // structural capability declaration, not evidence that either GPU pipeline ran.
        screen_space_reflections: WaterOpticsCapabilityStatus::Available,
        // StandardMaterial screen-space transmission enters the transmissive forward phase.
        // This water stays opaque/deferred so SSR has its documented G-buffer inputs.
        screen_space_refraction: WaterOpticsCapabilityStatus::Unavailable,
        environment_reflections: WaterOpticsCapabilityStatus::NotQualified,
    }
}

fn prepare_resolved_ssr(
    water: &WaterDocument,
    resolved: ResolvedWaterReflectionPath,
) -> Option<ScreenSpaceReflections> {
    let ResolvedWaterReflectionPath::ScreenSpace { max_steps } = resolved else {
        return None;
    };
    let WaterReflectionProfile::ScreenSpace { thickness_m, .. } = water.optics.reflection else {
        return None;
    };
    Some(ScreenSpaceReflections {
        thickness: thickness_m,
        linear_steps: u32::from(max_steps),
        ..default()
    })
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
    mut bank_materials: ResMut<Assets<crate::VisualValleyTerrainMaterial>>,
    mut images: ResMut<Assets<Image>>,
) {
    let mut prepared = prepared
        .0
        .take()
        .expect("water preparation is consumed exactly once at startup");
    let water_mesh = mesh_from_geometry(prepared.water_geometry);
    let river_bank_mesh = mesh_from_geometry(prepared.river_bank_geometry);
    let river_bank_name = format!("{} banks", prepared.name);
    let water_material = water_materials.add(prepared.water_material);
    prepared.river_bank_material.extension.detail_texture =
        images.add(crate::visual_valley::terrain_detail_image());
    let mut water_entity = commands.spawn((
        Name::new(prepared.name),
        VisualValleyWaterSurface,
        Mesh3d(meshes.add(water_mesh)),
        MeshMaterial3d(water_material.clone()),
        Transform::IDENTITY,
    ));
    if prepared.stats.optical_depth_preview {
        // The opaque shadow atlas cannot represent transmission through water.
        // Do not black out the visible riverbed; keep receiving scene shadows.
        water_entity.insert(bevy::light::NotShadowCaster);
    }
    commands.spawn((
        Name::new(river_bank_name),
        VisualValleyRiverBankSurface,
        Mesh3d(meshes.add(river_bank_mesh)),
        MeshMaterial3d(bank_materials.add(prepared.river_bank_material)),
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
    cameras: Query<(Entity, &Camera), With<Camera3d>>,
    prepared: Res<PreparedWaterCamera>,
    preview: Option<Res<WaterDepthOpticsPreview>>,
) {
    let mut active = cameras.iter().filter(|(_, camera)| camera.is_active);
    let Some((camera, _)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        // Match hot quality changes: never choose an arbitrary view or rewrite auxiliary optics.
        return;
    }
    let mut entity = commands.entity(camera);
    entity.insert((
        Msaa::Off,
        DeferredPrepass,
        ScreenSpaceTransmission {
            steps: usize::from(preview.is_some()),
            ..default()
        },
    ));
    if preview.is_some() {
        entity.insert((
            bevy::core_pipeline::prepass::DepthPrepass,
            TemporalAntiAliasing::default(),
        ));
        entity.remove::<ScreenSpaceReflections>();
    }
    if let Some(ssr) = &prepared.ssr {
        // SSR samples a stochastic specular ray per pixel. Accumulate it across frames;
        // water and vegetation shaders already supply previous deformed positions.
        entity.insert((ssr.clone(), TemporalAntiAliasing::default()));
    }
}

type WaterQualityCameraData = (
    &'static Camera,
    Option<&'static ScreenSpaceReflections>,
    Option<&'static bevy::light::EnvironmentMapLight>,
);

type WaterTransmissionCameraData = (
    &'static ScreenSpaceTransmission,
    Option<&'static bevy::core_pipeline::prepass::DepthPrepass>,
    Option<&'static TemporalAntiAliasing>,
);

fn camera_optics_match(
    stats: &WaterProjectionStats,
    cameras: &Query<WaterTransmissionCameraData, With<Camera3d>>,
) -> bool {
    if stats.optical_depth_preview {
        stats.ssr_camera_entities == 0
            && cameras.iter().count() == stats.camera_3d_entities
            && cameras.iter().all(|(transmission, depth, taa)| {
                transmission.steps == 1 && depth.is_some() && taa.is_some()
            })
    } else {
        stats.transmission_disabled_camera_entities == stats.camera_3d_entities
            && stats.quality.as_ref().map_or_else(
                || {
                    !stats.requested_ssr_component_prepared
                        || stats.ssr_camera_entities == stats.camera_3d_entities
                },
                |quality| quality.ssr_budget_projected,
            )
    }
}

#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    reason = "Bevy system wrappers and independent ECS queries keep evidence auditable"
)]
#[allow(
    clippy::too_many_lines,
    reason = "Single observation joins legacy and forward evidence from the same frame"
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
    transmission_cameras: Query<WaterTransmissionCameraData, With<Camera3d>>,
    materials: Res<Assets<VisualValleyWaterMaterial>>,
    projection: Res<WaterMaterialProjection>,
    runtime: Res<WaterRuntimeState>,
    mut stats: ResMut<WaterProjectionStats>,
    quality: Option<Res<WaterQualityRequest>>,
    prepared: Res<PreparedWaterCamera>,
    quality_cameras: Query<WaterQualityCameraData, With<Camera3d>>,
    images: Option<Res<Assets<Image>>>,
) {
    let mut active_cameras = quality_cameras
        .iter()
        .filter(|(camera, ..)| camera.is_active);
    let active_camera = active_cameras.next();
    let unique_active_camera = if active_cameras.next().is_none() {
        active_camera
    } else {
        None
    };
    let surface_material = surface_materials
        .single()
        .ok()
        .and_then(|handle| materials.get(&handle.0));
    stats.quality = quality
        .as_deref()
        .filter(|_| !stats.optical_depth_preview)
        .map(|request| {
            let map = unique_active_camera.and_then(|(_, _, map)| map);
            quality::observe(
                request,
                &prepared,
                unique_active_camera.map(|(camera, ssr, _)| (camera, ssr)),
                &projection.0,
                surface_material,
                quality::environment_images_available(map, images.as_deref()),
                map.map(|map| map.intensity),
            )
        });
    stats.water_mesh_entities = surfaces.iter().count();
    stats.forward_reflection_quality = (projection.0.reflection_preview.w > 0.0).then(|| {
        let mut observation = quality::observe_forward_reflection(
            quality.as_deref(),
            unique_active_camera.is_some(),
            projection.0.optical_parameters.w,
            projection.0.shallow_color_surface_tint.w,
            surface_material,
        );
        let map = unique_active_camera.and_then(|(_, _, map)| map);
        observation.observe_view(
            unique_active_camera.map(|(camera, ..)| camera),
            map,
            images.as_deref(),
        );
        observation
    });
    stats.river_bank_mesh_entities = river_banks.iter().count();
    stats.camera_3d_entities = cameras.iter().count();
    stats.deferred_camera_entities = deferred_cameras.iter().count();
    stats.ssr_camera_entities = ssr_cameras.iter().count();
    stats.msaa_off_camera_entities = msaa_cameras
        .iter()
        .filter(|msaa| **msaa == Msaa::Off)
        .count();
    stats.transmission_disabled_camera_entities = transmission_cameras
        .iter()
        .filter(|(transmission, ..)| transmission.steps == 0)
        .count();
    stats.shader_material_assets = materials.len();
    stats.shader_material_entities = surface_materials.iter().count();
    let expected_extension = quality::expected_extension(
        &projection.0,
        &runtime,
        quality.as_deref(),
        unique_active_camera.is_some(),
        stats.optical_depth_preview,
    );
    stats.shader_uniforms_match_profile = stats.shader_material_entities == 1
        && surface_materials.iter().all(|handle| {
            materials.get(&handle.0).is_some_and(|material| {
                material.extension == expected_extension
                    && (!stats.optical_depth_preview
                        || (material.base.opaque_render_method == OpaqueRendererMethod::Forward
                            && material.base.specular_transmission.to_bits() == 1.0_f32.to_bits()))
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
    let camera_optics_match = camera_optics_match(&stats, &transmission_cameras);
    stats.runtime_observation = if stats.water_mesh_entities == 1
        && stats.river_bank_mesh_entities == 1
        && stats.shader_material_entities == 1
        && stats.shader_uniforms_match_profile
        && stats.camera_3d_entities > 0
        && stats.deferred_camera_entities == stats.camera_3d_entities
        && stats.msaa_off_camera_entities == stats.camera_3d_entities
        && camera_optics_match
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
    use forge_project::ResolvedWaterReflectionPath;
    use forge_world::{
        embedded_visual_valley_terrain_document, embedded_visual_valley_water_document,
    };

    #[test]
    fn mineral_coating_covers_bed_without_changing_terrain_surface() {
        let sections = [0.0, 8.0].map(|z| {
            let lane = |x| ProjectedLane {
                position: [x, 50.0, z],
                terrain_height_m: 0.0,
            };
            ProjectedSection {
                lanes: [-4.0, 0.0, 4.0].map(lane),
                bank_outer_lanes: [-8.0, 8.0].map(lane),
                bed_depth_m: 2.0,
            }
        });
        let counts = checked_bank_mesh_counts(sections.len()).unwrap();
        let (ribbon, _) = build_river_bank_geometry(&sections, counts).unwrap();
        assert_eq!(ribbon.indices.len(), counts.indices);
        assert_eq!(counts.triangles, 6);
        let terrain = TerrainGenerationPlan::from_document(
            &embedded_visual_valley_terrain_document().unwrap(),
        )
        .unwrap();
        let (coating, _) = banks::conform(&ribbon, &terrain).unwrap();
        let colors = coating.colors.as_ref().unwrap();
        let mut bed_area = 0.0;
        for triangle in coating.indices.as_chunks::<3>().0 {
            let points = triangle.map(|index| coating.positions[index as usize]);
            let center_x = points.iter().map(|p| p[0]).sum::<f32>() / 3.0;
            if center_x.abs() < 4.0 {
                bed_area += triangle_normal_y(&points, [0, 1, 2]) * 0.5;
                for index in triangle {
                    let index = *index as usize;
                    assert!(colors[index][0] < 1.0e-6, "grass gap in bed");
                    assert!(colors[index][1] > 0.99999, "bed must join earth banks");
                }
            }
        }
        assert!((bed_area - 64.0).abs() < 1.0e-5, "missing bed footprint");
        for point in &coating.positions {
            let expected = terrain.surface_height_at(point[0], point[2]).unwrap();
            assert!((point[1] - expected - BANK_MESH_CLEARANCE_M).abs() < 1.0e-5);
        }
    }

    fn prepared() -> PreparedWaterScene {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        prepare_water_surface(&terrain, &water).unwrap()
    }

    #[test]
    fn river_coordinates_are_metric_and_translation_invariant_through_a_bend() {
        let section = |center: Vec2, flow: Vec2| {
            let across = Vec2::new(-flow.y, flow.x);
            let lane = |offset: f32| {
                let point = center + across * offset;
                ProjectedLane {
                    position: [point.x, 2.0, point.y],
                    terrain_height_m: 0.0,
                }
            };
            ProjectedSection {
                lanes: [lane(2.0), lane(0.0), lane(-2.0)],
                bank_outer_lanes: [lane(4.0), lane(-4.0)],
                bed_depth_m: 2.0,
            }
        };
        let source = [
            section(Vec2::ZERO, Vec2::new(0.6, 0.8)),
            section(Vec2::new(3.0, 4.0), Vec2::X),
            section(Vec2::new(8.0, 4.0), Vec2::X),
        ];
        let mut shifted = source;
        for section in &mut shifted {
            for lane in section
                .lanes
                .iter_mut()
                .chain(section.bank_outer_lanes.iter_mut())
            {
                lane.position[0] += 1024.0;
                lane.position[2] -= 2048.0;
            }
        }
        for extended in [false, true] {
            let counts = checked_mesh_counts(3).unwrap();
            let (original, _) = build_geometry(&source, counts, extended).unwrap();
            let (translated, _) = build_geometry(&shifted, counts, extended).unwrap();
            let lateral = if extended { 4.0 } else { 2.0 };
            for (row, expected_along) in [0.0, 5.0, 10.0].into_iter().enumerate() {
                for (lane, expected_across) in [lateral, 0.0, -lateral].into_iter().enumerate() {
                    let index = row * 3 + lane;
                    for uv in [original.uvs[index], translated.uvs[index]] {
                        assert!((uv[0] - expected_along).abs() < 0.001);
                        assert!((uv[1] - expected_across).abs() < 0.001);
                    }
                }
            }
            assert_eq!(original.indices, translated.indices);
        }
    }

    #[test]
    fn authored_water_extends_under_banks_without_lifting_or_mutating_them() {
        let section = |z| ProjectedSection {
            lanes: [
                ProjectedLane {
                    position: [-3.0, 2.0, z],
                    terrain_height_m: 1.1,
                },
                ProjectedLane {
                    position: [0.0, 2.0, z],
                    terrain_height_m: -0.8,
                },
                ProjectedLane {
                    position: [3.0, 2.0, z],
                    terrain_height_m: 1.1,
                },
            ],
            bank_outer_lanes: [
                ProjectedLane {
                    position: [-6.0, 3.515, z],
                    terrain_height_m: 3.5,
                },
                ProjectedLane {
                    position: [6.0, 3.515, z],
                    terrain_height_m: 3.5,
                },
            ],
            bed_depth_m: 2.8,
        };
        let sections = [section(0.0), section(4.0)];
        let counts = checked_mesh_counts(2).unwrap();
        let (legacy, _) = build_geometry(&sections, counts, false).unwrap();
        let (authored, _) = build_geometry(&sections, counts, true).unwrap();
        assert_eq!(authored.indices, legacy.indices);
        assert_eq!(authored.positions.len(), legacy.positions.len());
        for (row, source) in sections.iter().enumerate() {
            for lane in 0..3 {
                let index = row * 3 + lane;
                assert_eq!(
                    legacy.positions[index].map(f32::to_bits),
                    source.lanes[lane].position.map(f32::to_bits)
                );
                assert_eq!(authored.positions[index][1].to_bits(), 2.0_f32.to_bits());
                if lane == 1 {
                    assert_eq!(
                        authored.positions[index].map(f32::to_bits),
                        legacy.positions[index].map(f32::to_bits)
                    );
                } else {
                    let outer = source.bank_outer_lanes[lane / 2];
                    assert_eq!(
                        authored.positions[index][0].to_bits(),
                        outer.position[0].to_bits()
                    );
                    assert_eq!(
                        authored.terrain_heights[index].to_bits(),
                        outer.terrain_height_m.to_bits()
                    );
                    assert!(authored.positions[index][1] < authored.terrain_heights[index]);
                    // The old edge was suspended above the bed, not at the shore.
                    assert!(legacy.positions[index][1] > legacy.terrain_heights[index]);
                }
            }
        }
        assert_geometry_has_positive_winding(&authored);
    }

    #[test]
    fn carved_world_sketch_water_clips_banks_but_rejects_outside_centerline() {
        let compiled = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        let spline = compiled
            .spline_carving
            .splines
            .iter()
            .find(|spline| spline.kind == forge_world::WorldSketchSplineKind::River)
            .unwrap();
        let mut water = forge_world::water_from_carved_spline(
            embedded_visual_valley_water_document().unwrap(),
            spline,
        )
        .unwrap();
        water.revision = compiled.carved_terrain.revision;
        let mut prepared = prepare_water_surface(&compiled.carved_terrain, &water).unwrap();
        let road = forge_world::RoadSurfaceProjection::from_compiled(&compiled).unwrap();
        let original_positions = prepared.river_bank_geometry.positions.clone();
        let original_colors = prepared.river_bank_geometry.colors.clone().unwrap();
        let original_indices = prepared.river_bank_geometry.indices.clone();
        let original_normals = prepared.river_bank_geometry.normals.clone();
        prepared.apply_road_surface(&road).unwrap();
        assert_eq!(
            prepared.stats.road_surface_hash,
            Some(road.source_hash().to_string())
        );
        assert_eq!(prepared.river_bank_geometry.positions, original_positions);
        assert_eq!(prepared.river_bank_geometry.indices, original_indices);
        assert_eq!(prepared.river_bank_geometry.normals, original_normals);
        let changed = original_colors
            .iter()
            .zip(prepared.river_bank_geometry.colors.as_ref().unwrap())
            .filter(|(a, b)| a.map(f32::to_bits) != b.map(f32::to_bits))
            .count();
        assert!(
            changed > 0,
            "roads must also project onto overlapping banks"
        );
        println!("bank road overlay changed {changed} vertices without geometry changes");
        for ((position, before), after) in original_positions
            .iter()
            .zip(&original_colors)
            .zip(prepared.river_bank_geometry.colors.as_ref().unwrap())
        {
            let coverage = road.coverage_at(position[0], position[2]).unwrap();
            assert!(after[1] >= before[1] - 1e-6);
            assert!((after[0] + after[1] + after[2] - 1.0).abs() < 1e-6);
            if coverage.to_bits() == 0.0_f32.to_bits() {
                assert_eq!(before.map(f32::to_bits), after.map(f32::to_bits));
            }
            if coverage.to_bits() == 1.0_f32.to_bits() {
                assert_eq!(
                    after.map(f32::to_bits),
                    [0.0_f32, 1.0, 0.0, 1.0].map(f32::to_bits)
                );
            }
        }
        assert_eq!(prepared.stats.terrain_projection_lifts, 0);
        assert_eq!(prepared.stats.downstream_monotone_lifts, 0);
        for (lanes, sample) in prepared
            .water_geometry
            .positions
            .as_chunks::<3>()
            .0
            .iter()
            .zip(sample_river_centerline(&water).unwrap())
        {
            for lane in lanes {
                assert_eq!(lane[1].to_bits(), sample.center_m.y.to_bits());
            }
        }
        for geometry in [&prepared.water_geometry, &prepared.river_bank_geometry] {
            for position in &geometry.positions {
                assert!((-256.0..=256.0).contains(&position[0]));
                assert!((-256.0..=256.0).contains(&position[2]));
            }
            assert_geometry_has_positive_winding(geometry);
        }
        assert!(
            prepared
                .river_bank_geometry
                .positions
                .iter()
                .any(|p| p[2].to_bits() == 256.0_f32.to_bits())
        );
        water.river.control_points[0].center_m.z = 257.0;
        assert!(matches!(
            prepare_water_surface(&compiled.carved_terrain, &water),
            Err(WaterAdapterError::SurfaceProjection { .. })
        ));
    }

    #[test]
    fn bank_triangles_conform_to_canonical_surface() {
        let compiled = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        let spline = compiled
            .spline_carving
            .splines
            .iter()
            .find(|spline| spline.kind == forge_world::WorldSketchSplineKind::River)
            .unwrap();
        let mut water = forge_world::water_from_carved_spline(
            embedded_visual_valley_water_document().unwrap(),
            spline,
        )
        .unwrap();
        water.revision = compiled.carved_terrain.revision;
        let prepared = prepare_water_surface(&compiled.carved_terrain, &water).unwrap();
        let terrain = TerrainGenerationPlan::from_document(&compiled.carved_terrain).unwrap();
        let geometry = prepared.river_bank_geometry;
        for (position, normal) in geometry.positions.iter().zip(&geometry.normals) {
            let (_, expected) = terrain
                .surface_with_shading_normal_at(position[0], position[2])
                .unwrap();
            assert_eq!(normal.map(f32::to_bits), expected.map(f32::to_bits));
        }
        let mut below = 0;
        let mut minimum = f32::INFINITY;
        let mut maximum = f32::NEG_INFINITY;
        for triangle in geometry.indices.as_chunks::<3>().0 {
            let vertices =
                triangle.map(|index| Vec3::from_array(geometry.positions[index as usize]));
            for weights in [
                [1.0 / 3.0; 3],
                [0.5, 0.5, 0.0],
                [0.0, 0.5, 0.5],
                [0.5, 0.0, 0.5],
                [0.6, 0.2, 0.2],
                [0.2, 0.6, 0.2],
                [0.2, 0.2, 0.6],
            ] {
                let point =
                    vertices[0] * weights[0] + vertices[1] * weights[1] + vertices[2] * weights[2];
                let height = terrain.surface_at(point.x, point.z).unwrap().height_m;
                let clearance = point.y - height;
                assert!(
                    (clearance - BANK_MESH_CLEARANCE_M).abs() < 0.001,
                    "nonconforming bank at {point:?}: {clearance}"
                );
                below += usize::from(clearance < -0.001);
                minimum = minimum.min(clearance);
                maximum = maximum.max(clearance);
            }
        }
        eprintln!(
            "BANK_SURFACE triangles={} samples_per_triangle=7 below_terrain={} minimum_m={} maximum_m={}",
            geometry.indices.len() / 3,
            below,
            minimum,
            maximum
        );
    }

    #[test]
    fn bank_colors_are_deterministic_bounded_and_opaque() {
        let first = prepared();
        let second = prepared();
        let colors = first.river_bank_geometry.colors.as_ref().unwrap();
        assert_eq!(colors.len(), first.river_bank_geometry.positions.len());
        assert_eq!(Some(colors), second.river_bank_geometry.colors.as_ref());
        assert_eq!(
            first.river_bank_geometry.positions,
            second.river_bank_geometry.positions
        );
        assert_eq!(
            first.river_bank_geometry.indices,
            second.river_bank_geometry.indices
        );
        for color in colors {
            assert!(
                color
                    .iter()
                    .all(|value| value.is_finite() && (0.0..=1.0).contains(value))
            );
            assert_eq!(color[3].to_bits(), 1.0_f32.to_bits());
        }
        // Interpolated clipped vertices retain both ends of the authored gradient.
        assert!(colors.iter().any(|color| color[1] > color[0]));
        assert!(colors.iter().any(|color| color[0] > color[1]));
        assert!(
            colors
                .windows(8)
                .any(|rows| rows[0].map(f32::to_bits) != rows[4].map(f32::to_bits))
        );
        assert_eq!(first.river_bank_material.base.base_color, Color::WHITE);
        assert_eq!(first.river_bank_material.base.alpha_mode, AlphaMode::Opaque);
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
            stats.centerline_samples * 2 + stats.river_bank_vertices
        );
        assert_eq!(
            stats.terrain_surface_queries,
            stats.water_surface_queries + stats.river_bank_surface_queries
        );
        assert_eq!(stats.water_vertices, stats.centerline_samples * 3);
        assert_eq!(stats.water_triangles, (stats.centerline_samples - 1) * 4);
        assert_eq!(
            stats.river_bank_vertices,
            prepared.river_bank_geometry.positions.len()
        );
        assert_eq!(
            stats.river_bank_triangles,
            prepared.river_bank_geometry.indices.len() / 3
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
    fn depth_preview_changes_optics_without_touching_sources_or_geometry() {
        let mut scene = prepared();
        let water = embedded_visual_valley_water_document().unwrap();
        let source_hash = scene.stats.source_hash.clone();
        let positions = scene.water_geometry.positions.clone();
        let indices = scene.water_geometry.indices.clone();
        scene.enable_depth_optics_preview(&water);
        assert!(scene.stats.optical_depth_preview);
        assert_eq!(scene.stats.source_hash, source_hash);
        assert_eq!(scene.water_geometry.positions, positions);
        assert_eq!(scene.water_geometry.indices, indices);
        assert!(scene.ssr.is_none());
        assert!(!scene.stats.requested_ssr_component_prepared);
        assert_eq!(
            scene.water_material.base.opaque_render_method,
            OpaqueRendererMethod::Forward
        );
        assert_eq!(
            scene.water_material.base.specular_transmission.to_bits(),
            1.0_f32.to_bits()
        );
        assert!(!scene.stats.gpu_pass_observed);
    }

    #[test]
    fn authored_medium_selects_transmission_and_projects_linear_coefficients() {
        let mut water = embedded_visual_valley_water_document().unwrap();
        water.medium = Some(forge_project::WaterMediumProfile {
            absorption_per_m: [0.8, 0.2, 0.1],
            scattering_per_m: [0.1, 0.2, 0.3],
        });
        let source_hash = water.content_hash().unwrap().to_string();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        app.init_asset::<Image>();
        let camera = app.world_mut().spawn(Camera3d::default()).id();
        app.add_plugins(VisualValleyWaterPlugin::new(
            embedded_visual_valley_terrain_document().unwrap(),
            water,
        ));
        assert!(app.world().contains_resource::<WaterDepthOpticsPreview>());
        let projection = &app.world().resource::<WaterMaterialProjection>().0;
        assert_eq!(projection.absorption_medium, Vec4::new(0.8, 0.2, 0.1, 1.0));
        assert_eq!(projection.scattering_medium, Vec4::new(0.1, 0.2, 0.3, 0.0));
        let stats = app.world().resource::<WaterProjectionStats>();
        assert_eq!(stats.source_hash, source_hash);
        assert!(stats.optical_depth_preview);
        assert_eq!(
            stats.refraction_status,
            "experimental_rgb_medium_transmission_not_qualified"
        );
        assert!(!stats.gpu_pass_observed);
        app.update();
        let camera = app.world().entity(camera);
        assert!(camera.contains::<bevy::core_pipeline::prepass::DepthPrepass>());
        assert_eq!(camera.get::<ScreenSpaceTransmission>().unwrap().steps, 1);
        assert!(!camera.contains::<ScreenSpaceReflections>());
    }

    #[test]
    fn material_extension_maps_profile_and_keeps_deferred_surface_opaque() {
        let prepared = prepared();
        assert!(!prepared.stats.optical_depth_preview);
        assert_eq!(prepared.water_material.base.alpha_mode, AlphaMode::Opaque);
        assert_eq!(
            prepared.water_material.base.clearcoat.to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(
            prepared.water_material.base.metallic.to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(
            prepared.water_material.base.opaque_render_method,
            OpaqueRendererMethod::Deferred
        );
        assert_eq!(
            prepared.water_material.base.specular_transmission.to_bits(),
            0.0_f32.to_bits()
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
            WATER_REFLECTANCE.to_bits()
        );
        assert_eq!(
            prepared.river_bank_material.base.alpha_mode,
            AlphaMode::Opaque
        );
        assert_eq!(prepared.stats.applied_surface_tint_strength, Some(0.18));
        let tint = prepared.water_material.base.base_color.to_srgba();
        assert!((tint.red - (0.12 * 0.82 + 0.02 * 0.18)).abs() < 1.0e-6);
        assert!((tint.green - (0.42 * 0.82 + 0.13 * 0.18)).abs() < 1.0e-6);
        assert!((tint.blue - (0.46 * 0.82 + 0.21 * 0.18)).abs() < 1.0e-6);
        let extension = &prepared.water_material.extension;
        assert_eq!(extension.absorption_medium, Vec4::ZERO);
        assert_eq!(extension.scattering_medium, Vec4::ZERO);
        assert_eq!(extension.wave_parameters, Vec4::new(0.12, 5.5, 0.72, 0.62));
        assert_eq!(
            extension.shallow_color_surface_tint.w.to_bits(),
            0.18_f32.to_bits()
        );
        assert_eq!(
            extension.deep_color_max_depth.w.to_bits(),
            prepared.stats.maximum_sampled_bed_depth_m.to_bits()
        );
        assert!((extension.flow_phase.xy().length() - 1.0).abs() < 1.0e-6);
        assert_eq!(
            extension.optical_parameters,
            Vec4::new(0.84, 0.16, WATER_IOR, WATER_REFLECTANCE)
        );
    }

    #[test]
    fn metrics_resolve_optics_without_claiming_gpu_or_webgpu_execution() {
        let prepared = prepared();
        assert!(prepared.stats.requested_ssr_component_prepared);
        let ssr = prepared.ssr.as_ref().unwrap();
        assert_eq!(ssr.linear_steps, 48);
        assert_eq!(ssr.thickness.to_bits(), 0.9_f32.to_bits());
        assert_eq!(
            prepared.stats.reflection_intensity_status,
            "requested_not_mapped_by_bevy_ssr_component"
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
            ResolvedWaterReflectionPath::ScreenSpace { max_steps: 48 }
        );
        assert!(!prepared.stats.resolved_optics.reflection_fallback_used);
        assert_eq!(
            prepared.stats.resolved_optics.refraction,
            ResolvedWaterRefractionPath::SurfaceTint
        );
        assert!(prepared.stats.resolved_optics.refraction_fallback_used);
        assert_eq!(
            prepared
                .stats
                .resolved_optics
                .capabilities
                .screen_space_reflections,
            WaterOpticsCapabilityStatus::Available
        );
        assert_eq!(
            prepared
                .stats
                .resolved_optics
                .capabilities
                .screen_space_refraction,
            WaterOpticsCapabilityStatus::Unavailable
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
    fn camera_preparation_is_observed_without_claiming_gpu_execution() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        app.init_asset::<Image>();
        app.world_mut().spawn(Camera3d::default());
        app.add_plugins(VisualValleyWaterPlugin::new(terrain, water));
        app.update();

        let stats = app.world().resource::<WaterProjectionStats>();
        assert_eq!(
            stats.runtime_observation,
            WaterRuntimeObservationStatus::ObservedMainWorldComponents
        );
        assert_eq!(stats.camera_3d_entities, 1);
        assert_eq!(stats.deferred_camera_entities, 1);
        assert_eq!(stats.ssr_camera_entities, 1);
        assert_eq!(stats.msaa_off_camera_entities, 1);
        assert_eq!(stats.transmission_disabled_camera_entities, 1);
        let world = app.world_mut();
        assert_eq!(
            world.query::<&TemporalAntiAliasing>().iter(world).count(),
            1
        );
        let stats = world.resource::<WaterProjectionStats>();
        assert_eq!(stats.shader_material_assets, 1);
        assert_eq!(stats.shader_material_entities, 1);
        assert!(stats.shader_uniforms_match_profile);
        assert_eq!(stats.animation_updates, 1);
        assert_eq!(
            stats.cpu_ecs_application_status,
            "observed_main_world_components_and_cpu_material_asset"
        );
        assert!(!stats.gpu_pass_observed);
    }

    #[test]
    fn transmissive_water_does_not_cast_opaque_shadows_but_still_receives_them() {
        for preview in [false, true] {
            let mut app = App::new();
            app.add_plugins((MinimalPlugins, AssetPlugin::default()));
            app.init_asset::<Mesh>();
            app.init_asset::<StandardMaterial>();
            app.init_asset::<Image>();
            if preview {
                app.insert_resource(WaterDepthOpticsPreview);
            }
            app.add_plugins(VisualValleyWaterPlugin::new(
                embedded_visual_valley_terrain_document().unwrap(),
                embedded_visual_valley_water_document().unwrap(),
            ));
            app.update();
            let world = app.world_mut();
            let water = world
                .query_filtered::<EntityRef, With<VisualValleyWaterSurface>>()
                .single(world)
                .unwrap();
            assert_eq!(water.contains::<bevy::light::NotShadowCaster>(), preview);
            assert!(!water.contains::<bevy::light::NotShadowReceiver>());
            let bank = world
                .query_filtered::<EntityRef, With<VisualValleyRiverBankSurface>>()
                .single(world)
                .unwrap();
            assert!(!bank.contains::<bevy::light::NotShadowCaster>());
            assert!(!bank.contains::<bevy::light::NotShadowReceiver>());
        }
    }

    #[test]
    fn optical_preview_observation_requires_its_actual_camera_and_material_contract() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()));
        app.init_asset::<Mesh>();
        app.init_asset::<StandardMaterial>();
        app.init_asset::<Image>();
        app.insert_resource(WaterDepthOpticsPreview);
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        app.insert_resource(
            WaterQualityRequest::new(&terrain, policy, forge_project::VisualQualityTier::High)
                .unwrap(),
        );
        let camera = app.world_mut().spawn(Camera3d::default()).id();
        app.add_plugins(VisualValleyWaterPlugin::new(
            terrain,
            embedded_visual_valley_water_document().unwrap(),
        ));
        app.update();
        let complete = WaterRuntimeObservationStatus::ObservedMainWorldComponents;
        let incomplete = WaterRuntimeObservationStatus::ObservedIncompleteComponents;
        let observation = |app: &App| {
            app.world()
                .resource::<WaterProjectionStats>()
                .runtime_observation
        };
        assert_eq!(observation(&app), complete);
        assert!(
            app.world()
                .resource::<WaterProjectionStats>()
                .quality
                .is_none()
        );
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .preview_surface_tint(Some(false));
        app.update();
        assert_eq!(observation(&app), complete);
        app.world_mut()
            .entity_mut(camera)
            .get_mut::<ScreenSpaceTransmission>()
            .unwrap()
            .steps = 0;
        app.update();
        assert_eq!(observation(&app), incomplete);
        app.world_mut()
            .entity_mut(camera)
            .get_mut::<ScreenSpaceTransmission>()
            .unwrap()
            .steps = 1;
        app.world_mut()
            .entity_mut(camera)
            .remove::<bevy::core_pipeline::prepass::DepthPrepass>();
        app.update();
        assert_eq!(observation(&app), incomplete);
        app.world_mut()
            .entity_mut(camera)
            .insert(bevy::core_pipeline::prepass::DepthPrepass);
        app.world_mut()
            .entity_mut(camera)
            .insert(ScreenSpaceReflections::default());
        app.update();
        assert_eq!(observation(&app), incomplete);
        app.world_mut()
            .entity_mut(camera)
            .remove::<ScreenSpaceReflections>();
        app.world_mut()
            .entity_mut(camera)
            .remove::<TemporalAntiAliasing>();
        app.update();
        assert_eq!(observation(&app), incomplete);
        app.world_mut()
            .entity_mut(camera)
            .insert(TemporalAntiAliasing::default());
        app.update();
        assert_eq!(observation(&app), complete);
        let handle = app.world().resource::<WaterSurfaceMaterial>().0.clone();
        app.world_mut()
            .resource_mut::<Assets<VisualValleyWaterMaterial>>()
            .get_mut(&handle)
            .unwrap()
            .base
            .specular_transmission = 0.0;
        app.update();
        assert_eq!(observation(&app), incomplete);
    }

    #[test]
    fn depth_preview_keeps_temporal_antialiasing_and_enables_one_transmission_layer() {
        let mut app = App::new();
        app.insert_resource(PreparedWaterCamera { ssr: None });
        app.insert_resource(WaterDepthOpticsPreview);
        let camera = app.world_mut().spawn(Camera3d::default()).id();
        app.add_systems(Update, configure_water_cameras);
        app.update();
        let entity = app.world().entity(camera);
        assert!(entity.contains::<TemporalAntiAliasing>());
        assert!(entity.contains::<bevy::core_pipeline::prepass::DepthPrepass>());
        assert!(!entity.contains::<ScreenSpaceReflections>());
        assert_eq!(entity.get::<ScreenSpaceTransmission>().unwrap().steps, 1);
    }

    #[test]
    fn bed_depth_is_encoded_at_each_center_lane_without_changing_projection() {
        let water = embedded_visual_valley_water_document().unwrap();
        let samples = sample_river_centerline(&water).unwrap();
        let prepared = prepared();
        let colors = prepared.water_geometry.colors.as_ref().unwrap();
        assert_eq!(colors.len(), samples.len() * RIVER_LANE_COUNT);
        for (index, (sample, lanes)) in samples.iter().zip(colors.as_chunks::<3>().0).enumerate() {
            let (tangent, _) = robust_tangent(&samples, index).unwrap();
            for color in lanes {
                assert!((color[1] - tangent[0]).abs() < 0.0001);
                assert!((color[2] - tangent[1]).abs() < 0.0001);
            }
            assert_eq!(lanes[0][0].to_bits(), 0.0_f32.to_bits());
            assert_eq!(lanes[1][0].to_bits(), sample.bed_depth_m.to_bits());
            assert_eq!(lanes[2][0].to_bits(), 0.0_f32.to_bits());
            assert!(
                lanes
                    .iter()
                    .all(|color| color[3].to_bits() == 1.0_f32.to_bits())
            );
        }
        assert_eq!(
            prepared.river_bank_geometry.colors.as_ref().unwrap().len(),
            prepared.river_bank_geometry.positions.len()
        );
    }

    #[test]
    fn shader_contract_contains_profile_waves_depth_normals_and_deferred_output() {
        let shader = include_str!("shaders/visual_valley_water.wgsl");
        assert!(
            shader
                .replace("\r\n", "\n")
                .contains("#ifdef PREPASS_PIPELINE\n#ifdef MOTION_VECTOR_PREPASS")
        );
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
