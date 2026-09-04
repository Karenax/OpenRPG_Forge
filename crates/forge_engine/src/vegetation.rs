#[cfg(feature = "physics-rapier")]
use crate::{PlayerControllerSet, PlayerRuntimeSnapshot};
use bevy::{
    asset::{RenderAssetUsages, embedded_asset},
    camera::{
        primitives::Aabb,
        visibility::{
            NoAutoAabb, VisibilityRange, VisibilitySystems, VisibleEntities, VisibleEntityRanges,
        },
    },
    ecs::entity::EntityHashSet,
    light::NotShadowCaster,
    material::OpaqueRendererMethod,
    mesh::{Indices, MeshTag},
    pbr::{ExtendedMaterial, MaterialExtension},
    prelude::*,
    render::{
        RenderApp,
        batching::gpu_preprocessing::{GpuPreprocessingMode, GpuPreprocessingSupport},
        render_resource::{AsBindGroup, PrimitiveTopology},
        view::NoIndirectDrawing,
    },
    shader::ShaderRef,
};
use forge_project::{
    MAX_VEGETATION_EXPANDED_GRASS_BLADES, MAX_VEGETATION_PLACEMENT_ATTEMPTS, TerrainDocument,
    VEGETATION_CHUNK_SIZE_M, VegetationDensityDocument, VegetationDensityError, VegetationDocument,
    VegetationKind, VegetationPresentationProfile, VisualQualityPreset, WorkspaceArea,
};
use forge_world::{
    TERRAIN_HALF_EXTENT_M, TerrainGenerationPlan, VegetationGenerationError,
    VegetationGenerationPlan, VegetationInstance, VegetationInstanceId, VegetationKindCounts,
};
use serde::Serialize;
use std::{any::TypeId, collections::HashMap, error::Error, fmt, ops::Range, time::Duration};

const VEGETATION_SHADER_PATH: &str =
    "embedded://forge_engine/shaders/visual_valley_vegetation_deform.wgsl";
const MAX_WIND_AMPLITUDE_M: f32 = 0.55;
const MAX_INTERACTION_AMPLITUDE_M: f32 = 0.45;
const GRASS_PATCH_RADIUS_M: f32 = 2.5;
const GOLDEN_ANGLE_RADIANS: f32 = 2.399_963_1;
const TAG_PHASE_MASK: u32 = 0x00ff_ffff;
const TAG_PHASE_SCALE: f32 = 16_777_215.0;
const MAX_NEAR_MEDIUM_CROSSFADE_WIDTH_M: f32 = 6.0;
const MAX_MEDIUM_HLOD_CROSSFADE_WIDTH_M: f32 = 12.0;

/// Wall-clock cadence of the derived visibility audit.
///
/// Bevy still evaluates [`VisibilitySystems::CheckVisibility`] every frame. Only Forge's
/// diagnostic traversal of all vegetation projections is limited to ten observations per second.
pub const VEGETATION_VISIBILITY_AUDIT_INTERVAL: Duration = Duration::from_millis(100);

/// Defensive Gate 1 budgets, applied before expensive generation or mesh allocation.
pub const MAX_VEGETATION_CANDIDATE_ATTEMPTS: u64 = MAX_VEGETATION_PLACEMENT_ATTEMPTS;
pub const MAX_VEGETATION_PROJECTION_ENTITIES: u64 = 100_000;
pub const MAX_VEGETATION_GRASS_BLADES: u64 = MAX_VEGETATION_EXPANDED_GRASS_BLADES;
pub(crate) const MAX_EDITOR_VEGETATION_VERTICES: usize = 1_000_000;
pub(crate) const MAX_EDITOR_VEGETATION_VERTICES_PER_CHUNK: usize = 262_144;

/// Logical LOD carried by a derived Bevy projection. It is not persisted in a Forge document.
#[derive(Clone, Copy, Component, Debug, Eq, Hash, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum VegetationProjectionLod {
    Near,
    Medium,
    Hlod,
    GrassBatch,
}

/// Stable key used while allocating shared and per-chunk mesh handles.
///
/// Placements with the same shared key must receive the exact same `Handle<Mesh>` at runtime so
/// Bevy's normal batching and automatic instancing paths remain available.
#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
pub enum VegetationMeshKey {
    TreeNear,
    TreeMedium,
    Shrub,
    Fern,
    GrassChunk { x: u16, z: u16 },
    HlodChunk { x: u16, z: u16 },
}

/// Derived marker for one logical woody or fern placement.
#[derive(Clone, Component, Debug, Eq, PartialEq)]
pub struct VegetationPlacementMarker {
    pub id: VegetationInstanceId,
    pub kind: VegetationKind,
    pub lod: VegetationProjectionLod,
}

/// Derived marker for a chunk mesh. One batch can cover many logical placements.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct VegetationChunkBatchMarker {
    pub chunk_x: u16,
    pub chunk_z: u16,
    pub lod: VegetationProjectionLod,
    pub logical_placements: u32,
}

/// Exact 3D placement-anchor radius used to keep one HLOD chunk submitted conservatively.
#[derive(Clone, Copy, Component, Debug, PartialEq)]
pub(crate) struct VegetationHlodBatchEnvelope {
    pub(crate) maximum_anchor_radius_m: f32,
}

/// Interaction published by gameplay and consumed only by the presentation adapter.
///
/// A disabled field has a zero radius and strength. Setters clamp invalid or excessive input so a
/// gameplay bug cannot send non-finite values to the GPU.
#[derive(Clone, Copy, Debug, PartialEq, Resource)]
pub struct VegetationInteractionField {
    pub center_m: Vec3,
    pub push_direction_xz: Vec2,
    pub radius_m: f32,
    pub strength: f32,
}

impl Default for VegetationInteractionField {
    fn default() -> Self {
        Self::disabled()
    }
}

impl VegetationInteractionField {
    #[must_use]
    pub const fn disabled() -> Self {
        Self {
            center_m: Vec3::ZERO,
            push_direction_xz: Vec2::ZERO,
            radius_m: 0.0,
            strength: 0.0,
        }
    }

    #[must_use]
    pub fn bounded(
        center_m: Vec3,
        push_direction_xz: Vec2,
        radius_m: f32,
        strength: f32,
        profile: VegetationPresentationProfile,
    ) -> Self {
        if !center_m.is_finite() || !push_direction_xz.is_finite() {
            return Self::disabled();
        }
        let direction = push_direction_xz.try_normalize().unwrap_or(Vec2::ZERO);
        Self {
            center_m,
            push_direction_xz: direction,
            radius_m: finite_clamp(radius_m, 0.0, profile.interaction_radius_m),
            strength: finite_clamp(strength, 0.0, profile.interaction_strength),
        }
    }
}

/// Attempt and accepted-placement counts. `candidate_attempts` is deliberately not described as a
/// rendered object or GPU submission.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub struct VegetationPlacementStats {
    pub candidate_attempts: u64,
    pub accepted_placements: u64,
    pub accepted_by_kind: VegetationKindCounts,
}

/// ECS projection counts. Grass blades are geometry inside chunk batches, never entities.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub struct VegetationProjectionCounts {
    pub total_entities: u64,
    pub placement_entities: u64,
    pub near_entities: u64,
    pub medium_entities: u64,
    pub grass_batches: u64,
    pub hlod_batches: u64,
    pub grass_blades: u64,
    pub shared_mesh_assets: u64,
    pub chunk_mesh_assets: u64,
    pub material_assets: u64,
    pub shadow_caster_entities: u64,
    pub shadow_opt_out_entities: u64,
}

/// Per-camera counts observed from Bevy's actual range and CPU-frustum visibility tables.
///
/// `cpu_visible_for_camera` never means a draw call. `cpu_culled_after_range` is the in-range
/// remainder
/// absent from the active camera's `VisibleEntities` mesh class; it can include frustum, inherited
/// visibility, render-layer or visibility-class filtering and does not claim GPU occlusion culling.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub struct VegetationVisibilityBucket {
    pub total: u64,
    pub cpu_visible_for_camera: u64,
    pub range_culled: u64,
    pub cpu_culled_after_range: u64,
}

impl VegetationVisibilityBucket {
    fn observe(&mut self, in_range: bool, cpu_visible_for_camera: bool) {
        self.total = self.total.saturating_add(1);
        if cpu_visible_for_camera {
            self.cpu_visible_for_camera = self.cpu_visible_for_camera.saturating_add(1);
        } else if in_range {
            self.cpu_culled_after_range = self.cpu_culled_after_range.saturating_add(1);
        } else {
            self.range_culled = self.range_culled.saturating_add(1);
        }
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum VegetationVisibilityObservationStatus {
    #[default]
    NotObserved,
    ObservedSingleActiveCamera,
    UnavailableNoActiveCamera,
    UnavailableMultipleActiveCameras,
}

/// Last complete visibility observation for the single active Visual Valley camera.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub struct VegetationVisibilityStats {
    /// Number of main-world visibility boundaries observed by Forge.
    ///
    /// The historical main-world meaning is preserved for evidence-schema compatibility. It is not
    /// a presented-frame count; [`VegetationVisibilityAuditCadence::completed_observations`] reports
    /// the lower number of full projection audit passes separately.
    pub observation_frames: u64,
    pub status: VegetationVisibilityObservationStatus,
    pub active_camera_views: u64,
    pub visible_outside_range_inconsistencies: u64,
    pub total: VegetationVisibilityBucket,
    pub near: VegetationVisibilityBucket,
    pub medium: VegetationVisibilityBucket,
    pub hlod: VegetationVisibilityBucket,
    pub grass_batches: VegetationVisibilityBucket,
}

/// Strong receipt returned when a caller requests an out-of-cadence visibility observation.
///
/// Tickets are process-local diagnostic identifiers. They are monotonic and never wrap; the
/// practically unreachable `u64::MAX` value is retained rather than rolling back to zero.
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd, Serialize)]
#[serde(transparent)]
pub struct VegetationVisibilityAuditTicket(u64);

impl VegetationVisibilityAuditTicket {
    /// Numeric identifier suitable for a machine-readable qualification ledger.
    #[must_use]
    pub const fn id(self) -> u64 {
        self.0
    }
}

/// Request/receipt control for exact visibility observations at governed checkpoints.
///
/// A runtime harness may call [`Self::request_observation`] at a ready or final checkpoint, then
/// poll [`Self::request_is_completed`] in a later schedule. Multiple pending tickets may be
/// coalesced into one observation, but each ticket is acknowledged monotonically at most once.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Resource, Serialize)]
pub struct VegetationVisibilityAuditControl {
    requested_id: u64,
    completed_id: u64,
}

impl VegetationVisibilityAuditControl {
    /// Requests an observation after Bevy's next visibility pass, regardless of the 100 ms cadence.
    pub fn request_observation(&mut self) -> VegetationVisibilityAuditTicket {
        self.requested_id = self.requested_id.saturating_add(1);
        VegetationVisibilityAuditTicket(self.requested_id)
    }

    /// Latest requested diagnostic identifier.
    #[must_use]
    pub const fn requested_id(&self) -> u64 {
        self.requested_id
    }

    /// Latest identifier covered by a completed visibility observation.
    #[must_use]
    pub const fn completed_id(&self) -> u64 {
        self.completed_id
    }

    /// Returns true only after an observation has covered `ticket`.
    #[must_use]
    pub const fn request_is_completed(&self, ticket: VegetationVisibilityAuditTicket) -> bool {
        ticket.0 <= self.completed_id
    }

    const fn pending_ticket(&self) -> Option<VegetationVisibilityAuditTicket> {
        if self.completed_id < self.requested_id {
            Some(VegetationVisibilityAuditTicket(self.requested_id))
        } else {
            None
        }
    }

    fn complete_through(&mut self, ticket: Option<VegetationVisibilityAuditTicket>) {
        if let Some(ticket) = ticket {
            self.completed_id = self.completed_id.max(ticket.0.min(self.requested_id));
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct VegetationVisibilityAuditSemantics {
    active_camera_views: u64,
    single_active_camera: Option<Entity>,
    single_active_camera_disables_indirect_drawing: bool,
    visibility_range_bits: [u32; 4],
}

impl VegetationVisibilityAuditSemantics {
    fn new(
        active_camera_views: u64,
        single_active_camera: Option<Entity>,
        single_active_camera_disables_indirect_drawing: bool,
        presentation: VegetationPresentationProfile,
    ) -> Self {
        Self {
            active_camera_views,
            single_active_camera,
            single_active_camera_disables_indirect_drawing,
            visibility_range_bits: [
                presentation.near_lod_distance_m.to_bits(),
                presentation.medium_lod_distance_m.to_bits(),
                presentation.hlod_distance_m.to_bits(),
                presentation.cull_distance_m.to_bits(),
            ],
        }
    }
}

/// Runtime state of Forge's bounded vegetation visibility audit.
///
/// The resource is public so qualification code can report the effective cadence and completed
/// observation count. Its scheduling internals remain private: callers force a fresh observation
/// through [`VegetationVisibilityAuditControl`] instead of mutating the clock state.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Resource)]
pub struct VegetationVisibilityAuditCadence {
    interval: Duration,
    has_observed: bool,
    last_observed_at: Option<Duration>,
    main_world_observation_frames: u64,
    completed_observations: u64,
    last_semantics: Option<VegetationVisibilityAuditSemantics>,
}

impl Default for VegetationVisibilityAuditCadence {
    fn default() -> Self {
        Self {
            interval: VEGETATION_VISIBILITY_AUDIT_INTERVAL,
            has_observed: false,
            last_observed_at: None,
            main_world_observation_frames: 0,
            completed_observations: 0,
            last_semantics: None,
        }
    }
}

impl VegetationVisibilityAuditCadence {
    /// Effective minimum interval between ordinary visibility observations.
    #[must_use]
    pub const fn interval(&self) -> Duration {
        self.interval
    }

    /// Last real-time timestamp recorded by the audit, if a real clock was available.
    #[must_use]
    pub const fn last_observed_at(&self) -> Option<Duration> {
        self.last_observed_at
    }

    /// Number of actual full audit passes, independent of rendered or simulated frame counts.
    #[must_use]
    pub const fn completed_observations(&self) -> u64 {
        self.completed_observations
    }

    /// Number of main-world `PostUpdate` visibility boundaries observed by this system.
    ///
    /// This preserves the historical meaning of `VegetationVisibilityStats::observation_frames`
    /// even though the expensive projection traversal now runs at a lower cadence.
    #[must_use]
    pub const fn main_world_observation_frames(&self) -> u64 {
        self.main_world_observation_frames
    }

    fn record_main_world_observation_frame(&mut self) {
        self.main_world_observation_frames = self.main_world_observation_frames.saturating_add(1);
    }

    fn observation_is_due(
        &self,
        now: Option<Duration>,
        forced: bool,
        semantics: VegetationVisibilityAuditSemantics,
    ) -> bool {
        if !self.has_observed || forced || self.last_semantics != Some(semantics) {
            return true;
        }
        match (now, self.last_observed_at) {
            (Some(now), Some(last)) if now < last => true,
            (Some(now), Some(last)) => now.saturating_sub(last) >= self.interval,
            (Some(_), None) => true,
            (None, _) => false,
        }
    }

    fn record_observation(
        &mut self,
        now: Option<Duration>,
        semantics: VegetationVisibilityAuditSemantics,
    ) {
        self.has_observed = true;
        self.last_observed_at = now;
        self.completed_observations = self.completed_observations.saturating_add(1);
        self.last_semantics = Some(semantics);
    }
}

/// GPU preprocessing state resolved from Bevy's renderer support resource.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum VegetationGpuPreprocessingMode {
    #[default]
    Unknown,
    None,
    PreprocessingOnly,
    Culling,
}

/// Honest render-path capabilities. Shared handles request Bevy's standard batching; no raw draw
/// count or guarantee that all compatible instances were merged is inferred from that request.
#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct VegetationRenderPathStats {
    pub batching_request: &'static str,
    pub maximum_gpu_preprocessing: VegetationGpuPreprocessingMode,
    pub active_camera_gpu_preprocessing: VegetationGpuPreprocessingMode,
    pub culling_evidence: &'static str,
}

impl Default for VegetationRenderPathStats {
    fn default() -> Self {
        Self {
            batching_request: "bevy_shared_mesh_material_automatic",
            maximum_gpu_preprocessing: VegetationGpuPreprocessingMode::Unknown,
            active_camera_gpu_preprocessing: VegetationGpuPreprocessingMode::Unknown,
            culling_evidence: "bevy_visible_entities_and_visibility_range_cpu_tables",
        }
    }
}

/// Triangle counts for unique mesh assets and logical range selections.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub struct VegetationTriangleStats {
    pub unique_mesh_asset_triangles: u64,
    pub logically_selected_near_triangles: u64,
    pub logically_selected_medium_triangles: u64,
    pub logically_selected_hlod_triangles: u64,
}

/// Machine-readable vegetation adapter evidence. No field is a draw-call estimate.
#[derive(Clone, Debug, Default, Eq, PartialEq, Resource, Serialize)]
pub struct VegetationProjectionStats {
    pub terrain_document_hash: String,
    pub vegetation_document_hash: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub vegetation_density_document_hash: Option<String>,
    pub placements: VegetationPlacementStats,
    pub projection: VegetationProjectionCounts,
    pub triangles: VegetationTriangleStats,
    pub visibility: VegetationVisibilityStats,
    pub render_path: VegetationRenderPathStats,
}

impl VegetationProjectionStats {
    pub(crate) fn overlay_details(&self) -> String {
        let visibility = match self.visibility.status {
            VegetationVisibilityObservationStatus::ObservedSingleActiveCamera => format!(
                "camera: {} visible | {} out of range | {} CPU-culled after range",
                self.visibility.total.cpu_visible_for_camera,
                self.visibility.total.range_culled,
                self.visibility.total.cpu_culled_after_range,
            ),
            status => format!("camera: metrics unavailable ({status:?})"),
        };
        format!(
            "{} placements | {} projection entities | {} grass batches | {} HLOD batches\n{}",
            self.placements.accepted_placements,
            self.projection.total_entities,
            self.projection.grass_batches,
            self.projection.hlod_batches,
            visibility,
        )
    }
}

/// Complementary ranges used by tree LOD0/LOD1 and chunk HLOD projections.
///
/// `near.end_margin == medium.start_margin` and
/// `medium.end_margin == hlod.start_margin` preserve Bevy's dither crossfades.
#[derive(Clone, PartialEq)]
pub struct VegetationLodRanges {
    pub near: VisibilityRange,
    pub medium: VisibilityRange,
    pub individual: VisibilityRange,
    pub grass: VisibilityRange,
    pub hlod: VisibilityRange,
}

impl VegetationLodRanges {
    #[must_use]
    pub fn from_profile(profile: VegetationPresentationProfile) -> Self {
        Self::from_distances(
            profile.near_lod_distance_m,
            profile.medium_lod_distance_m,
            profile.hlod_distance_m,
            profile.cull_distance_m,
        )
    }

    /// Derives the same transition bands for procedural vegetation and CC0 replacements.
    #[must_use]
    pub fn from_distances(near: f32, medium: f32, hlod: f32, cull: f32) -> Self {
        let near_to_medium =
            centered_lod_crossfade(near, medium, MAX_NEAR_MEDIUM_CROSSFADE_WIDTH_M);
        let medium_to_hlod =
            centered_lod_crossfade(medium, hlod, MAX_MEDIUM_HLOD_CROSSFADE_WIDTH_M);
        Self {
            near: VisibilityRange {
                start_margin: 0.0..0.0,
                end_margin: near_to_medium.clone(),
                use_aabb: false,
            },
            medium: VisibilityRange {
                start_margin: near_to_medium.clone(),
                end_margin: medium_to_hlod.clone(),
                use_aabb: false,
            },
            individual: VisibilityRange {
                start_margin: 0.0..0.0,
                end_margin: medium_to_hlod.clone(),
                use_aabb: false,
            },
            grass: VisibilityRange {
                start_margin: 0.0..0.0,
                end_margin: near_to_medium,
                use_aabb: true,
            },
            hlod: VisibilityRange {
                start_margin: medium_to_hlod,
                end_margin: cull..cull,
                use_aabb: false,
            },
        }
    }

    /// Returns the CPU submission envelope for a chunk HLOD whose shader fades each placement
    /// from its own 3D anchor. The triangle inequality guarantees that an anchor visible between
    /// the authored transition and cull distances cannot be dropped with its parent chunk.
    #[must_use]
    pub(crate) fn conservative_hlod_batch_range(
        &self,
        maximum_anchor_radius_m: f32,
    ) -> VisibilityRange {
        let radius_m = maximum_anchor_radius_m.max(f32::EPSILON);
        let transition_start_m = self.hlod.start_margin.start;
        let conservative_start_m = (transition_start_m - radius_m).max(0.0);
        let conservative_end_m = self.hlod.end_margin.end + radius_m;
        VisibilityRange {
            start_margin: conservative_start_m..transition_start_m,
            end_margin: conservative_end_m..conservative_end_m,
            use_aabb: false,
        }
    }
}

fn centered_lod_crossfade(
    start_distance_m: f32,
    end_distance_m: f32,
    maximum_width_m: f32,
) -> Range<f32> {
    let interval_width_m = end_distance_m - start_distance_m;
    let half_crossfade_width_m = interval_width_m.min(maximum_width_m) * 0.5;
    let midpoint_m = start_distance_m + interval_width_m * 0.5;
    (midpoint_m - half_crossfade_width_m)..(midpoint_m + half_crossfade_width_m)
}

/// Forge-original vegetation PBR palette, wind and bounded interaction uniforms.
#[derive(Asset, AsBindGroup, Clone, Debug, Reflect)]
pub struct VisualValleyVegetationExtension {
    #[uniform(100)]
    pub primary_color_roughness: Vec4,
    #[uniform(100)]
    pub secondary_color_roughness: Vec4,
    #[uniform(100)]
    pub bark_color_roughness: Vec4,
    #[uniform(100)]
    pub metallic_reflectance: Vec4,
    /// X is maximum wind displacement, Y is cycles/second, Z is current time and W previous time.
    #[uniform(100)]
    pub wind_parameters: Vec4,
    /// XYZ is the world-space interaction center and W is its radius.
    #[uniform(100)]
    pub interaction_center_radius: Vec4,
    /// XY is a normalized fallback push direction; Z is maximum displacement.
    #[uniform(100)]
    pub interaction_direction_strength: Vec4,
    /// XYZ is a normalized horizontal wind direction.
    #[uniform(100)]
    pub wind_direction: Vec4,
    /// X is scalar wetness; Y/Z are the full-wet roughness/albedo multipliers.
    #[uniform(100)]
    pub wetness_parameters: Vec4,
    /// XY is the per-placement HLOD fade interval; Z is its authored cull distance.
    #[uniform(100)]
    pub hlod_visibility_parameters: Vec4,
}

impl Default for VisualValleyVegetationExtension {
    fn default() -> Self {
        Self {
            // RGB is authored in linear space; alpha carries perceptual roughness.
            primary_color_roughness: Vec4::new(0.035, 0.22, 0.045, 0.9),
            secondary_color_roughness: Vec4::new(0.12, 0.38, 0.055, 0.84),
            bark_color_roughness: Vec4::new(0.18, 0.075, 0.028, 0.94),
            // X metallic, Y reflectance. Z/W remain reserved.
            metallic_reflectance: Vec4::new(0.0, 0.34, 0.0, 0.0),
            wind_parameters: Vec4::ZERO,
            interaction_center_radius: Vec4::ZERO,
            interaction_direction_strength: Vec4::ZERO,
            wind_direction: Vec4::new(0.82, 0.0, 0.57, 0.0),
            // A zero wetness scalar is an exact identity for the original material output.
            wetness_parameters: Vec4::new(0.0, 1.0, 1.0, 0.0),
            hlod_visibility_parameters: Vec4::ZERO,
        }
    }
}

impl VisualValleyVegetationExtension {
    #[must_use]
    pub fn from_profile_and_interaction(
        profile: VegetationPresentationProfile,
        interaction: VegetationInteractionField,
    ) -> Self {
        let ranges = VegetationLodRanges::from_profile(profile);
        Self {
            wind_parameters: Vec4::new(
                profile.wind_strength * MAX_WIND_AMPLITUDE_M,
                profile.wind_speed_hz,
                0.0,
                0.0,
            ),
            interaction_center_radius: interaction.center_m.extend(interaction.radius_m),
            interaction_direction_strength: Vec4::new(
                interaction.push_direction_xz.x,
                interaction.push_direction_xz.y,
                interaction.strength * MAX_INTERACTION_AMPLITUDE_M,
                0.0,
            ),
            hlod_visibility_parameters: Vec4::new(
                ranges.hlod.start_margin.start,
                ranges.hlod.start_margin.end,
                ranges.hlod.end_margin.start,
                0.0,
            ),
            ..Self::default()
        }
    }
}

impl MaterialExtension for VisualValleyVegetationExtension {
    fn enable_prepass() -> bool {
        // The camera depth prepass can retain dithered, wind-deformed vegetation depth where the
        // forward color pass rejects the matching sample. Keep the material forward-only so HLOD
        // cards and grass cannot become depth-only silhouettes. The custom prepass vertex shader
        // remains available to Bevy's independent shadow-map path.
        false
    }

    fn vertex_shader() -> ShaderRef {
        VEGETATION_SHADER_PATH.into()
    }

    fn fragment_shader() -> ShaderRef {
        VEGETATION_SHADER_PATH.into()
    }

    fn prepass_vertex_shader() -> ShaderRef {
        VEGETATION_SHADER_PATH.into()
    }
}

pub type VisualValleyVegetationMaterial =
    ExtendedMaterial<StandardMaterial, VisualValleyVegetationExtension>;

/// Compact, renderer-facing placement used by the procedural mesh builders.
/// Positions are relative to the owning chunk for grass and HLOD batches.
#[derive(Clone, Copy, Debug, PartialEq)]
pub(crate) struct ProceduralVegetationPlacement {
    pub position_m: Vec3,
    pub yaw_radians: f32,
    pub scale: f32,
    pub wind_phase_radians: f32,
    pub wind_response: f32,
    pub kind: VegetationKind,
}

/// Mesh plus the explicit deformation-safe AABB installed with `NoAutoAabb`.
#[derive(Debug)]
pub(crate) struct DerivedVegetationMesh {
    pub mesh: Mesh,
    pub aabb: Aabb,
    pub triangles: usize,
}

#[derive(Debug, Clone, Eq, PartialEq)]
pub enum VegetationMeshError {
    Empty,
    NonFiniteVertex,
    IncompleteHlodAnchorAttribute,
    DegenerateTriangle,
    VertexCountOverflow,
    GrassBladeCountIsZero,
    GrassBladeSurfaceProjectionFailed,
    EditorPreviewVertexBudgetExceeded { actual: usize, maximum: usize },
}

impl fmt::Display for VegetationMeshError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Empty => formatter.write_str("vegetation mesh contains no vertices"),
            Self::NonFiniteVertex => {
                formatter.write_str("vegetation mesh contains a non-finite vertex")
            }
            Self::IncompleteHlodAnchorAttribute => formatter
                .write_str("vegetation HLOD anchor attribute does not cover every mesh vertex"),
            Self::DegenerateTriangle => {
                formatter.write_str("vegetation mesh contains a degenerate triangle")
            }
            Self::VertexCountOverflow => {
                formatter.write_str("vegetation mesh exceeds the u32 index range")
            }
            Self::GrassBladeCountIsZero => {
                formatter.write_str("grass_blades_per_patch must be non-zero")
            }
            Self::GrassBladeSurfaceProjectionFailed => {
                formatter.write_str("grass blade could not be projected onto the rendered terrain")
            }
            Self::EditorPreviewVertexBudgetExceeded { actual, maximum } => write!(
                formatter,
                "editor vegetation preview requires {actual} vertices, maximum is {maximum}"
            ),
        }
    }
}

impl Error for VegetationMeshError {}

#[derive(Default)]
struct MeshAccumulator {
    positions: Vec<[f32; 3]>,
    normals: Vec<[f32; 3]>,
    colors: Vec<[f32; 4]>,
    uvs: Vec<[f32; 2]>,
    secondary_uvs: Vec<[f32; 2]>,
    indices: Vec<u32>,
}

impl MeshAccumulator {
    fn push_quad(
        &mut self,
        points: [Vec3; 4],
        colors: [[f32; 4]; 4],
    ) -> Result<(), VegetationMeshError> {
        let normal = triangle_normal(points[0], points[1], points[2])?;
        let first = self.push_vertex(points[0], normal, colors[0], [0.0, 0.0])?;
        let second = self.push_vertex(points[1], normal, colors[1], [1.0, 0.0])?;
        let third = self.push_vertex(points[2], normal, colors[2], [1.0, 1.0])?;
        let fourth = self.push_vertex(points[3], normal, colors[3], [0.0, 1.0])?;
        self.indices
            .extend_from_slice(&[first, second, third, first, third, fourth]);
        Ok(())
    }

    fn push_hlod_quad(
        &mut self,
        points: [Vec3; 4],
        colors: [[f32; 4]; 4],
        anchor: Vec3,
        hemispheric_lighting_weight: f32,
    ) -> Result<(), VegetationMeshError> {
        let first_vertex = self.positions.len();
        if self.secondary_uvs.len() != first_vertex {
            return Err(VegetationMeshError::IncompleteHlodAnchorAttribute);
        }
        self.push_quad(points, colors)?;
        for uv in &mut self.uvs[first_vertex..] {
            // HLOD currently has no texture or lightmap. UV0/UV1 are reserved together for the
            // exact local XYZ anchor plus its impostor-lighting weight: UV0 = [Y, weight] and
            // UV1 = XZ. Other vegetation keeps normal UV0 and has no UV1.
            *uv = [anchor.y, hemispheric_lighting_weight];
        }
        self.secondary_uvs
            .extend(std::iter::repeat_n([anchor.x, anchor.z], 4));
        Ok(())
    }

    fn push_triangle(
        &mut self,
        points: [Vec3; 3],
        colors: [[f32; 4]; 3],
    ) -> Result<(), VegetationMeshError> {
        let normal = triangle_normal(points[0], points[1], points[2])?;
        let first = self.push_vertex(points[0], normal, colors[0], [0.0, 0.0])?;
        let second = self.push_vertex(points[1], normal, colors[1], [1.0, 0.0])?;
        let third = self.push_vertex(points[2], normal, colors[2], [0.5, 1.0])?;
        self.indices.extend_from_slice(&[first, second, third]);
        Ok(())
    }

    fn push_vertex(
        &mut self,
        position: Vec3,
        normal: Vec3,
        color: [f32; 4],
        uv: [f32; 2],
    ) -> Result<u32, VegetationMeshError> {
        if !position.is_finite()
            || !normal.is_finite()
            || color.into_iter().any(|value| !value.is_finite())
            || uv.into_iter().any(|value| !value.is_finite())
        {
            return Err(VegetationMeshError::NonFiniteVertex);
        }
        let index = u32::try_from(self.positions.len())
            .map_err(|_| VegetationMeshError::VertexCountOverflow)?;
        self.positions.push(position.to_array());
        self.normals.push(normal.to_array());
        self.colors.push(color);
        self.uvs.push(uv);
        Ok(index)
    }

    fn finish(
        self,
        deformation_margin_m: f32,
    ) -> Result<DerivedVegetationMesh, VegetationMeshError> {
        if self.positions.is_empty() || self.indices.is_empty() {
            return Err(VegetationMeshError::Empty);
        }
        if !self.secondary_uvs.is_empty() && self.secondary_uvs.len() != self.positions.len() {
            return Err(VegetationMeshError::IncompleteHlodAnchorAttribute);
        }
        let first = Vec3::from_array(self.positions[0]);
        let (mut minimum, mut maximum) =
            self.positions
                .iter()
                .skip(1)
                .fold((first, first), |(minimum, maximum), position| {
                    let position = Vec3::from_array(*position);
                    (minimum.min(position), maximum.max(position))
                });
        let margin = finite_clamp(deformation_margin_m, 0.0, 8.0);
        minimum.x -= margin;
        minimum.z -= margin;
        maximum.x += margin;
        maximum.z += margin;
        let aabb = Aabb::from_min_max(minimum, maximum);
        let triangles = self.indices.len() / 3;
        let mut mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, self.positions)
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, self.normals)
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, self.uvs)
        .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, self.colors)
        .with_inserted_indices(Indices::U32(self.indices));
        if !self.secondary_uvs.is_empty() {
            mesh.insert_attribute(Mesh::ATTRIBUTE_UV_1, self.secondary_uvs);
        }
        Ok(DerivedVegetationMesh {
            mesh,
            aabb,
            triangles,
        })
    }
}

#[must_use]
pub(crate) fn maximum_deformation_m(profile: VegetationPresentationProfile) -> f32 {
    profile.wind_strength * MAX_WIND_AMPLITUDE_M
        + profile.interaction_strength * MAX_INTERACTION_AMPLITUDE_M
}

pub(crate) fn build_tree_near_mesh(
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    build_tree_mesh(
        8,
        &[(1.75, 3.8, 1.35), (2.65, 4.8, 1.12), (3.55, 5.65, 0.86)],
        deformation_margin_m,
    )
}

pub(crate) fn build_tree_medium_mesh(
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    build_tree_mesh(
        5,
        &[(1.8, 4.35, 1.25), (3.0, 5.5, 0.82)],
        deformation_margin_m,
    )
}

fn build_tree_mesh(
    trunk_segments: u16,
    canopy_layers: &[(f32, f32, f32)],
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    append_cylinder(
        &mut mesh,
        Vec3::ZERO,
        0.22,
        2.9,
        trunk_segments,
        bark_color(0.0),
        bark_color(0.22),
    )?;
    for (layer_index, &(base_y, tip_y, radius)) in canopy_layers.iter().enumerate() {
        append_cone(
            &mut mesh,
            Vec3::new(0.0, base_y, 0.0),
            radius,
            tip_y,
            trunk_segments + 1,
            leaf_color(layer_index, 0.35),
            leaf_color(layer_index + 1, 1.0),
        )?;
    }
    mesh.finish(deformation_margin_m)
}

pub(crate) fn build_shrub_mesh(
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    for (index, offset) in [
        Vec3::new(-0.28, 0.0, 0.04),
        Vec3::new(0.28, 0.0, 0.09),
        Vec3::new(0.0, 0.0, -0.24),
    ]
    .into_iter()
    .enumerate()
    {
        append_cone(
            &mut mesh,
            offset,
            0.58,
            [1.25, 1.33, 1.41][index],
            7,
            leaf_color(index, 0.0),
            leaf_color(index + 1, 1.0),
        )?;
    }
    mesh.finish(deformation_margin_m)
}

pub(crate) fn build_fern_mesh(
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    for leaf in 0..7_u16 {
        let angle = f32::from(leaf) * std::f32::consts::TAU / 7.0;
        let outward = Vec3::new(angle.cos(), 0.0, angle.sin());
        let right = Vec3::new(-outward.z, 0.0, outward.x) * 0.10;
        let base = Vec3::new(0.0, 0.04, 0.0);
        let shoulder = outward * 0.72 + Vec3::Y * 0.44;
        let tip = outward * 1.05 + Vec3::Y * 0.16;
        mesh.push_quad(
            [base - right, base + right, shoulder + right * 0.35, tip],
            [
                leaf_color(usize::from(leaf), 0.0),
                leaf_color(usize::from(leaf), 0.0),
                leaf_color(usize::from(leaf) + 1, 0.72),
                leaf_color(usize::from(leaf) + 1, 1.0),
            ],
        )?;
    }
    mesh.finish(deformation_margin_m)
}

pub(crate) fn build_grass_chunk_mesh(
    terrain: &TerrainGenerationPlan,
    chunk_center: Vec3,
    patches: &[ProceduralVegetationPlacement],
    grass_blades_per_patch: u16,
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    if grass_blades_per_patch == 0 {
        return Err(VegetationMeshError::GrassBladeCountIsZero);
    }
    let mut mesh = MeshAccumulator::default();
    for (patch_index, patch) in patches.iter().enumerate() {
        if patch.kind != VegetationKind::Grass {
            continue;
        }
        for blade in 0..grass_blades_per_patch {
            let radial = ((f32::from(blade) + 0.5) / f32::from(grass_blades_per_patch)).sqrt();
            let angle = patch.wind_phase_radians
                + f32::from(blade) * GOLDEN_ANGLE_RADIANS
                + unit_noise(patch_index, blade, 0) * 0.45;
            let radius = radial * GRASS_PATCH_RADIUS_M * patch.scale;
            let local_x = patch.position_m.x + angle.cos() * radius;
            let local_z = patch.position_m.z + angle.sin() * radius;
            let world_x =
                (chunk_center.x + local_x).clamp(-TERRAIN_HALF_EXTENT_M, TERRAIN_HALF_EXTENT_M);
            let world_z =
                (chunk_center.z + local_z).clamp(-TERRAIN_HALF_EXTENT_M, TERRAIN_HALF_EXTENT_M);
            let surface = terrain
                .surface_at(world_x, world_z)
                .map_err(|_| VegetationMeshError::GrassBladeSurfaceProjectionFailed)?;
            let base = Vec3::new(
                world_x - chunk_center.x,
                surface.height_m,
                world_z - chunk_center.z,
            );
            let blade_yaw = angle + unit_noise(patch_index, blade, 1) * 1.2;
            let width = (0.035 + unit_noise(patch_index, blade, 2) * 0.03) * patch.scale;
            let height = (0.25 + unit_noise(patch_index, blade, 3) * 0.25) * patch.scale;
            let right = Vec3::new(blade_yaw.cos(), 0.0, -blade_yaw.sin()) * (width * 0.5);
            let bend_yaw = blade_yaw + (unit_noise(patch_index, blade, 4) - 0.5) * 1.2;
            let bend = Vec3::new(bend_yaw.sin(), 0.0, bend_yaw.cos())
                * height
                * (0.18 + unit_noise(patch_index, blade, 5) * 0.16);
            let elbow = base + Vec3::Y * (height * 0.55) + bend * 0.22;
            let top = base + Vec3::Y * height + bend;
            let bottom_color = grass_color(blade, 0.0);
            let elbow_color = grass_color(blade, patch.wind_response * 0.55);
            let top_color = grass_color(blade, patch.wind_response);
            // A tapered lower ribbon and bent pointed tip keep the root fixed without a tall needle.
            mesh.push_quad(
                [
                    base - right,
                    base + right,
                    elbow + right * 0.5,
                    elbow - right * 0.5,
                ],
                [bottom_color, bottom_color, elbow_color, elbow_color],
            )?;
            mesh.push_triangle(
                [elbow - right * 0.5, elbow + right * 0.5, top],
                [elbow_color, elbow_color, top_color],
            )?;
        }
    }
    mesh.finish(deformation_margin_m)
}

fn recenter_grass_batch(mesh: &mut DerivedVegetationMesh, chunk_center: Vec3) -> Transform {
    // CPU/compute range checks use the AABB center, while vertex dithering uses the mesh origin.
    // Make those anchors coincide without changing world geometry or the deformation-safe extents.
    let local_center = Vec3::from(mesh.aabb.center);
    // This mesh has just been built; its MAIN_WORLD positions have not been extracted to the GPU.
    mesh.mesh.translate_by(-local_center);
    mesh.aabb.center = Vec3::ZERO.into();
    Transform::from_translation(chunk_center + local_center)
}

pub(crate) fn build_hlod_chunk_mesh(
    placements: &[ProceduralVegetationPlacement],
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    for placement in placements {
        match placement.kind {
            VegetationKind::Tree => append_crossed_tree_hlod(
                &mut mesh,
                placement.position_m,
                placement.yaw_radians,
                2.5 * placement.scale,
                5.5 * placement.scale,
                placement.wind_response,
            )?,
            VegetationKind::Shrub => append_crossed_tapered_hlod(
                &mut mesh,
                placement.position_m,
                placement.yaw_radians,
                1.15 * placement.scale,
                1.35 * placement.scale,
                placement.wind_response,
            )?,
            VegetationKind::Fern => append_crossed_tapered_hlod(
                &mut mesh,
                placement.position_m,
                placement.yaw_radians,
                0.9 * placement.scale,
                0.82 * placement.scale,
                placement.wind_response,
            )?,
            VegetationKind::Grass => {}
        }
    }
    mesh.finish(deformation_margin_m)
}

/// Builds one compact editor-only batch from the canonical generated placements of a chunk.
///
/// The game renderer keeps its full wind/LOD pipeline above. The world editor instead needs an
/// immediately replaceable preview with one entity per populated chunk, including one crossed
/// tuft per logical grass patch (never one ECS entity per blade).
pub(crate) fn build_editor_vegetation_chunk_mesh(
    instances: &[VegetationInstance],
) -> Result<Mesh, VegetationMeshError> {
    let vertices = editor_vegetation_vertex_count(instances)?;
    if vertices > MAX_EDITOR_VEGETATION_VERTICES_PER_CHUNK {
        return Err(VegetationMeshError::EditorPreviewVertexBudgetExceeded {
            actual: vertices,
            maximum: MAX_EDITOR_VEGETATION_VERTICES_PER_CHUNK,
        });
    }
    let mut mesh = MeshAccumulator::default();
    for instance in instances {
        // The canonical domain accepts every positive finite scale. Keep even subnormal authored
        // values renderable without changing their source or deterministic generated placement.
        let preview_scale = instance.scale.max(0.001);
        let base = Vec3::new(
            instance.position_m.x,
            instance.position_m.y,
            instance.position_m.z,
        );
        let variation = 0.86 + 0.14 * instance.wind_phase_radians.sin().abs();
        match instance.kind {
            VegetationKind::Tree => {
                append_editor_crossed_card(
                    &mut mesh,
                    base,
                    instance.yaw_radians,
                    0.46 * preview_scale,
                    2.9 * preview_scale,
                    1.0,
                    [0.18, 0.075, 0.025, 1.0],
                    [0.25, 0.11, 0.035, 1.0],
                )?;
                append_editor_crossed_card(
                    &mut mesh,
                    base + Vec3::Y * (1.7 * preview_scale),
                    instance.yaw_radians + 0.37,
                    2.8 * preview_scale,
                    4.2 * preview_scale,
                    0.20,
                    [0.055 * variation, 0.24 * variation, 0.045, 1.0],
                    [0.13 * variation, 0.43 * variation, 0.075, 1.0],
                )?;
            }
            VegetationKind::Shrub => append_editor_crossed_card(
                &mut mesh,
                base,
                instance.yaw_radians,
                1.55 * preview_scale,
                1.35 * preview_scale,
                0.30,
                [0.055, 0.25 * variation, 0.05, 1.0],
                [0.15, 0.46 * variation, 0.08, 1.0],
            )?,
            VegetationKind::Fern => append_editor_crossed_card(
                &mut mesh,
                base,
                instance.yaw_radians,
                1.25 * preview_scale,
                0.82 * preview_scale,
                0.08,
                [0.045, 0.22 * variation, 0.04, 1.0],
                [0.20, 0.52 * variation, 0.10, 1.0],
            )?,
            VegetationKind::Grass => {
                for offset in [-0.42_f32, 0.0, 0.47] {
                    append_editor_crossed_card(
                        &mut mesh,
                        base + Vec3::new(offset, 0.0, -offset * 0.55) * preview_scale,
                        instance.yaw_radians + offset,
                        1.65 * preview_scale,
                        (0.46 + offset.abs() * 0.16) * preview_scale,
                        0.05,
                        [0.10, 0.30 * variation, 0.045, 1.0],
                        [0.34, 0.62 * variation, 0.09, 1.0],
                    )?;
                }
            }
        }
    }
    Ok(mesh.finish(0.08)?.mesh)
}

pub(crate) fn editor_vegetation_vertex_count(
    instances: &[VegetationInstance],
) -> Result<usize, VegetationMeshError> {
    instances.iter().try_fold(0_usize, |vertices, instance| {
        let added = match instance.kind {
            VegetationKind::Tree => 16,
            VegetationKind::Shrub | VegetationKind::Fern => 8,
            VegetationKind::Grass => 24,
        };
        vertices
            .checked_add(added)
            .ok_or(VegetationMeshError::VertexCountOverflow)
    })
}

#[allow(
    clippy::too_many_arguments,
    reason = "the editor card keeps its bounded geometry and two-color palette explicit"
)]
fn append_editor_crossed_card(
    mesh: &mut MeshAccumulator,
    base: Vec3,
    yaw: f32,
    width: f32,
    height: f32,
    top_width_ratio: f32,
    bottom_color: [f32; 4],
    top_color: [f32; 4],
) -> Result<(), VegetationMeshError> {
    for quarter_turn in [0.0, std::f32::consts::FRAC_PI_2] {
        let angle = yaw + quarter_turn;
        let axis = Vec3::new(angle.cos(), 0.0, -angle.sin());
        let bottom_right = axis * (width * 0.5);
        let top_right = bottom_right * top_width_ratio;
        let top = base + Vec3::Y * height;
        mesh.push_quad(
            [
                base - bottom_right,
                base + bottom_right,
                top + top_right,
                top - top_right,
            ],
            [bottom_color, bottom_color, top_color, top_color],
        )?;
    }
    Ok(())
}

fn maximum_hlod_anchor_radius_m(placements: &[ProceduralVegetationPlacement]) -> f32 {
    placements
        .iter()
        .filter(|placement| placement.kind != VegetationKind::Grass)
        .map(|placement| placement.position_m.length())
        .fold(0.0, f32::max)
}

#[allow(
    clippy::too_many_arguments,
    reason = "the procedural primitive keeps its explicit geometry and palette inputs together"
)]
fn append_cylinder(
    mesh: &mut MeshAccumulator,
    base: Vec3,
    radius: f32,
    height: f32,
    segments: u16,
    bottom_color: [f32; 4],
    top_color: [f32; 4],
) -> Result<(), VegetationMeshError> {
    for segment in 0..segments {
        let angle_a = f32::from(segment) * std::f32::consts::TAU / f32::from(segments);
        let angle_b = f32::from(segment + 1) * std::f32::consts::TAU / f32::from(segments);
        let radial_a = Vec3::new(angle_a.cos(), 0.0, angle_a.sin()) * radius;
        let radial_b = Vec3::new(angle_b.cos(), 0.0, angle_b.sin()) * radius;
        mesh.push_quad(
            [
                base + radial_a,
                base + radial_b,
                base + radial_b + Vec3::Y * height,
                base + radial_a + Vec3::Y * height,
            ],
            [bottom_color, bottom_color, top_color, top_color],
        )?;
    }
    Ok(())
}

#[allow(
    clippy::too_many_arguments,
    reason = "the procedural primitive keeps its explicit geometry and palette inputs together"
)]
fn append_cone(
    mesh: &mut MeshAccumulator,
    base: Vec3,
    radius: f32,
    tip_y: f32,
    segments: u16,
    base_color: [f32; 4],
    tip_color: [f32; 4],
) -> Result<(), VegetationMeshError> {
    let tip = Vec3::new(base.x, tip_y, base.z);
    for segment in 0..segments {
        let angle_a = f32::from(segment) * std::f32::consts::TAU / f32::from(segments);
        let angle_b = f32::from(segment + 1) * std::f32::consts::TAU / f32::from(segments);
        let first = base + Vec3::new(angle_a.cos() * radius, 0.0, angle_a.sin() * radius);
        let second = base + Vec3::new(angle_b.cos() * radius, 0.0, angle_b.sin() * radius);
        mesh.push_triangle([first, second, tip], [base_color, base_color, tip_color])?;
    }
    Ok(())
}

fn append_crossed_tree_hlod(
    mesh: &mut MeshAccumulator,
    base: Vec3,
    yaw: f32,
    width: f32,
    height: f32,
    wind_response: f32,
) -> Result<(), VegetationMeshError> {
    for quarter_turn in [0.0, std::f32::consts::FRAC_PI_2] {
        let angle = yaw + quarter_turn;
        let axis = Vec3::new(angle.cos(), 0.0, -angle.sin());
        let trunk_right = axis * (width * 0.10);
        let trunk_top = base + Vec3::Y * (height * 0.38);
        mesh.push_hlod_quad(
            [
                base - trunk_right,
                base + trunk_right,
                trunk_top + trunk_right,
                trunk_top - trunk_right,
            ],
            [bark_color(0.0); 4],
            base,
            0.0,
        )?;

        let canopy_bottom = base + Vec3::Y * (height * 0.20);
        let canopy_top = base + Vec3::Y * height;
        let canopy_bottom_right = axis * (width * 0.50);
        let canopy_top_right = axis * (width * 0.10);
        mesh.push_hlod_quad(
            [
                canopy_bottom - canopy_bottom_right,
                canopy_bottom + canopy_bottom_right,
                canopy_top + canopy_top_right,
                canopy_top - canopy_top_right,
            ],
            [
                mixed_leaf_color(wind_response * 0.35),
                mixed_leaf_color(wind_response * 0.35),
                mixed_leaf_color(wind_response),
                mixed_leaf_color(wind_response),
            ],
            base,
            1.0,
        )?;
    }
    Ok(())
}

fn append_crossed_tapered_hlod(
    mesh: &mut MeshAccumulator,
    base: Vec3,
    yaw: f32,
    width: f32,
    height: f32,
    wind_response: f32,
) -> Result<(), VegetationMeshError> {
    for quarter_turn in [0.0, std::f32::consts::FRAC_PI_2] {
        let angle = yaw + quarter_turn;
        let axis = Vec3::new(angle.cos(), 0.0, -angle.sin());
        let bottom_right = axis * (width * 0.50);
        let top_right = axis * (width * 0.20);
        let top = base + Vec3::Y * height;
        mesh.push_hlod_quad(
            [
                base - bottom_right,
                base + bottom_right,
                top + top_right,
                top - top_right,
            ],
            [
                mixed_leaf_color(0.0),
                mixed_leaf_color(0.0),
                mixed_leaf_color(wind_response),
                mixed_leaf_color(wind_response),
            ],
            base,
            1.0,
        )?;
    }
    Ok(())
}

fn triangle_normal(first: Vec3, second: Vec3, third: Vec3) -> Result<Vec3, VegetationMeshError> {
    (second - first)
        .cross(third - first)
        .try_normalize()
        .ok_or(VegetationMeshError::DegenerateTriangle)
}

fn leaf_color(variation: usize, wind_weight: f32) -> [f32; 4] {
    if variation.is_multiple_of(2) {
        [1.0, 0.0, 0.0, wind_weight]
    } else {
        [0.0, 1.0, 0.0, wind_weight]
    }
}

const fn bark_color(wind_weight: f32) -> [f32; 4] {
    [0.0, 0.0, 1.0, wind_weight]
}

fn grass_color(blade: u16, wind_weight: f32) -> [f32; 4] {
    if blade.is_multiple_of(3) {
        [0.35, 0.65, 0.0, wind_weight]
    } else {
        [0.72, 0.28, 0.0, wind_weight]
    }
}

fn mixed_leaf_color(wind_weight: f32) -> [f32; 4] {
    [0.35, 0.65, 0.0, wind_weight]
}

fn finite_clamp(value: f32, minimum: f32, maximum: f32) -> f32 {
    if value.is_finite() {
        value.clamp(minimum, maximum)
    } else {
        minimum
    }
}

#[allow(
    clippy::cast_precision_loss,
    reason = "the 23-bit mantissa is exactly representable as f32"
)]
fn unit_noise(patch_index: usize, blade: u16, stream: u32) -> f32 {
    let patch = u64::try_from(patch_index).unwrap_or(u64::MAX);
    let mut value = patch
        .wrapping_mul(0x9e37_79b9_7f4a_7c15)
        .wrapping_add(u64::from(blade).wrapping_mul(0xbf58_476d_1ce4_e5b9))
        .wrapping_add(u64::from(stream).wrapping_mul(0x94d0_49bb_1331_11eb));
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^= value >> 31;
    let mantissa = u32::try_from(value >> 41).unwrap_or(0);
    mantissa as f32 / 8_388_607.0
}

/// Public Bevy adapter for one validated terrain/vegetation document pair.
///
/// The documents remain the only sources of truth. All meshes, handles, entities, ranges and
/// statistics produced by this plugin are reconstructible runtime caches.
pub struct VisualValleyVegetationPlugin {
    terrain: TerrainDocument,
    vegetation: VegetationDocument,
    vegetation_density: Option<VegetationDensityDocument>,
    interaction: VegetationInteractionField,
    quality: Option<VisualQualityPreset>,
}

impl VisualValleyVegetationPlugin {
    #[must_use]
    pub fn new(terrain: TerrainDocument, vegetation: VegetationDocument) -> Self {
        Self {
            terrain,
            vegetation,
            vegetation_density: None,
            interaction: VegetationInteractionField::disabled(),
            quality: None,
        }
    }

    /// Builds the renderer adapter from one canonical workspace area, including its density mask.
    #[must_use]
    pub fn from_area(area: &WorkspaceArea) -> Self {
        Self {
            terrain: area.terrain.clone(),
            vegetation: area.vegetation.clone(),
            vegetation_density: Some(area.vegetation_density.clone()),
            interaction: VegetationInteractionField::disabled(),
            quality: None,
        }
    }

    #[must_use]
    pub const fn with_interaction(mut self, interaction: VegetationInteractionField) -> Self {
        self.interaction = interaction;
        self
    }

    /// Applies renderer budgets from a validated quality preset without mutating the source document.
    #[must_use]
    pub fn with_quality_preset(mut self, preset: VisualQualityPreset) -> Self {
        self.quality = Some(preset);
        self
    }
}

impl Plugin for VisualValleyVegetationPlugin {
    fn build(&self, app: &mut App) {
        let prepared = prepare_vegetation_projection_with_quality(
            &self.terrain,
            &self.vegetation,
            self.vegetation_density.as_ref(),
            self.quality.as_ref(),
        )
        .unwrap_or_else(|error| panic!("vegetation plugin requires valid documents: {error}"));
        let stats = prepared.stats.clone();
        let profile = prepared.profile;
        let interaction = VegetationInteractionField::bounded(
            self.interaction.center_m,
            self.interaction.push_direction_xz,
            self.interaction.radius_m,
            self.interaction.strength,
            profile,
        );

        embedded_asset!(app, "shaders/visual_valley_vegetation_deform.wgsl");
        app.insert_resource(stats)
            .insert_resource(VegetationPresentation(profile))
            .insert_resource(interaction)
            .insert_resource(PreparedVegetation(Some(prepared)))
            .init_resource::<VegetationVisibilityAuditControl>()
            .init_resource::<VegetationVisibilityAuditCadence>()
            .add_plugins(MaterialPlugin::<VisualValleyVegetationMaterial>::default())
            .add_systems(Startup, spawn_vegetation_projection)
            .add_systems(
                PostUpdate,
                update_vegetation_interaction_uniform
                    .after(crate::quality::VisualValleyQualitySet::Apply),
            )
            .add_systems(
                PostUpdate,
                observe_vegetation_visibility.after(VisibilitySystems::CheckVisibility),
            );

        #[cfg(feature = "physics-rapier")]
        app.add_systems(
            FixedUpdate,
            publish_player_vegetation_interaction
                .after(PlayerControllerSet::Observe)
                .before(PlayerControllerSet::Camera),
        );
    }

    fn finish(&self, app: &mut App) {
        let maximum_gpu_preprocessing = app
            .get_sub_app(RenderApp)
            .and_then(|render_app| render_app.world().get_resource::<GpuPreprocessingSupport>())
            .map_or(VegetationGpuPreprocessingMode::Unknown, |support| {
                vegetation_gpu_preprocessing_mode(support.max_supported_mode)
            });
        if let Some(mut stats) = app
            .world_mut()
            .get_resource_mut::<VegetationProjectionStats>()
        {
            stats.render_path.maximum_gpu_preprocessing = maximum_gpu_preprocessing;
        }
    }
}

/// Runs the complete deterministic adapter without creating an ECS world or GPU resources.
pub fn inspect_vegetation_projection(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
) -> Result<VegetationProjectionStats, VegetationAdapterError> {
    Ok(prepare_vegetation_projection(terrain, vegetation)?.stats)
}

/// Runs the deterministic adapter for one workspace area and its canonical density mask.
pub fn inspect_area_vegetation_projection(
    area: &WorkspaceArea,
) -> Result<VegetationProjectionStats, VegetationAdapterError> {
    Ok(prepare_vegetation_projection_with_quality(
        &area.terrain,
        &area.vegetation,
        Some(&area.vegetation_density),
        None,
    )?
    .stats)
}

#[derive(Debug)]
pub enum VegetationAdapterError {
    Generation(VegetationGenerationError),
    Density(VegetationDensityError),
    Mesh {
        key: VegetationMeshKey,
        source: VegetationMeshError,
    },
    CountOverflow(&'static str),
    BudgetExceeded {
        field: &'static str,
        actual: u64,
        maximum: u64,
    },
    MissingMeshRecipe(VegetationMeshKey),
}

impl fmt::Display for VegetationAdapterError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Generation(error) => write!(formatter, "vegetation generation failed: {error}"),
            Self::Density(error) => {
                write!(formatter, "vegetation density source is invalid: {error}")
            }
            Self::Mesh { key, source } => {
                write!(formatter, "vegetation mesh {key:?} is invalid: {source}")
            }
            Self::CountOverflow(field) => {
                write!(
                    formatter,
                    "vegetation projection count `{field}` overflowed"
                )
            }
            Self::BudgetExceeded {
                field,
                actual,
                maximum,
            } => write!(
                formatter,
                "vegetation projection `{field}` is {actual}, above the Gate 1 budget {maximum}"
            ),
            Self::MissingMeshRecipe(key) => {
                write!(
                    formatter,
                    "vegetation projection references missing mesh {key:?}"
                )
            }
        }
    }
}

impl Error for VegetationAdapterError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Generation(error) => Some(error),
            Self::Density(error) => Some(error),
            Self::Mesh { source, .. } => Some(source),
            Self::CountOverflow(_) | Self::BudgetExceeded { .. } | Self::MissingMeshRecipe(_) => {
                None
            }
        }
    }
}

impl From<VegetationGenerationError> for VegetationAdapterError {
    fn from(value: VegetationGenerationError) -> Self {
        Self::Generation(value)
    }
}

impl From<VegetationDensityError> for VegetationAdapterError {
    fn from(value: VegetationDensityError) -> Self {
        Self::Density(value)
    }
}

#[derive(Resource)]
struct PreparedVegetation(Option<PreparedVegetationProjection>);

struct PreparedVegetationProjection {
    profile: VegetationPresentationProfile,
    meshes: Vec<PreparedMeshAsset>,
    entities: Vec<PreparedProjectionEntity>,
    stats: VegetationProjectionStats,
}

struct PreparedMeshAsset {
    key: VegetationMeshKey,
    value: DerivedVegetationMesh,
}

struct PreparedProjectionEntity {
    name: String,
    mesh_key: VegetationMeshKey,
    transform: Transform,
    mesh_tag: MeshTag,
    visibility_range: VisibilityRange,
    marker: PreparedProjectionMarker,
    casts_shadow: bool,
}

enum PreparedProjectionMarker {
    Placement(VegetationPlacementMarker),
    Batch(VegetationChunkBatchMarker),
    HlodBatch(VegetationChunkBatchMarker, VegetationHlodBatchEnvelope),
}

#[derive(Clone, Copy, Resource)]
pub(crate) struct VegetationPresentation(pub(crate) VegetationPresentationProfile);

#[derive(Resource)]
struct VegetationMaterialHandle(Handle<VisualValleyVegetationMaterial>);

#[allow(
    clippy::similar_names,
    clippy::too_many_lines,
    reason = "one preparation pass validates correlated mesh, entity, LOD and evidence counts"
)]
fn prepare_vegetation_projection(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    prepare_vegetation_projection_with_quality(terrain, vegetation, None, None)
}

#[allow(
    clippy::similar_names,
    clippy::too_many_lines,
    reason = "one preparation pass validates correlated mesh, entity, LOD and evidence counts"
)]
fn prepare_vegetation_projection_with_quality(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    vegetation_density: Option<&VegetationDensityDocument>,
    quality: Option<&VisualQualityPreset>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    let vegetation_density_document_hash = vegetation_density
        .map(VegetationDensityDocument::content_hash)
        .transpose()?
        .map(|hash| hash.to_string());
    let plan = if let Some(density) = vegetation_density {
        VegetationGenerationPlan::from_documents_with_density(terrain, vegetation, density)?
    } else {
        VegetationGenerationPlan::from_documents(terrain, vegetation)?
    };
    let cells_per_axis = u64::from(plan.cells_per_axis());
    let attempts_per_cell = vegetation
        .layers
        .iter()
        .map(|layer| u64::from(layer.attempts_per_cell))
        .sum::<u64>();
    let candidate_attempts = cells_per_axis
        .checked_mul(cells_per_axis)
        .and_then(|cells| cells.checked_mul(attempts_per_cell))
        .ok_or(VegetationAdapterError::CountOverflow("candidate_attempts"))?;
    ensure_budget(
        "candidate_attempts",
        candidate_attempts,
        MAX_VEGETATION_CANDIDATE_ATTEMPTS,
    )?;

    let generated = plan.generate()?;
    let mut profile = vegetation.presentation;
    if let Some(quality) = quality {
        profile.near_lod_distance_m = quality.vegetation.near_lod_distance_m;
        profile.medium_lod_distance_m = quality.vegetation.medium_lod_distance_m;
        profile.hlod_distance_m = quality.vegetation.hlod_distance_m;
        profile.cull_distance_m = quality.vegetation.cull_distance_m;
        profile.shadow_distance_m = quality.shadows.maximum_distance_m;
        profile.grass_blades_per_patch = quality.vegetation.grass_blades_per_patch;
    }
    let kind_counts = generated.kind_counts;
    let trees = u64::from(kind_counts.trees);
    let shrubs = u64::from(kind_counts.shrubs);
    let ferns = u64::from(kind_counts.ferns);
    let grass_patches = u64::from(kind_counts.grass_patches);
    let placement_entities = trees
        .checked_mul(2)
        .and_then(|value| value.checked_add(shrubs))
        .and_then(|value| value.checked_add(ferns))
        .ok_or(VegetationAdapterError::CountOverflow("placement_entities"))?;
    let grass_batch_entities = u64_count(
        generated
            .chunks
            .iter()
            .filter(|chunk| chunk.kind_counts.grass_patches > 0)
            .count(),
        "grass_batch_entities",
    )?;
    let hlod_batch_entities = u64_count(
        generated
            .chunks
            .iter()
            .filter(|chunk| chunk.kind_counts.total() > chunk.kind_counts.grass_patches)
            .count(),
        "hlod_batch_entities",
    )?;
    let total_entities = placement_entities
        .checked_add(grass_batch_entities)
        .and_then(|value| value.checked_add(hlod_batch_entities))
        .ok_or(VegetationAdapterError::CountOverflow("total_entities"))?;
    ensure_budget(
        "total_entities",
        total_entities,
        MAX_VEGETATION_PROJECTION_ENTITIES,
    )?;
    let grass_blades = grass_patches
        .checked_mul(u64::from(profile.grass_blades_per_patch))
        .ok_or(VegetationAdapterError::CountOverflow("grass_blades"))?;
    ensure_budget("grass_blades", grass_blades, MAX_VEGETATION_GRASS_BLADES)?;
    let deformation_margin_m = maximum_deformation_m(profile);
    let ranges = VegetationLodRanges::from_profile(profile);

    let tree_near = build_tree_near_mesh(deformation_margin_m).map_err(|source| {
        VegetationAdapterError::Mesh {
            key: VegetationMeshKey::TreeNear,
            source,
        }
    })?;
    let tree_near_triangles = u64_count(tree_near.triangles, "tree_near_triangles")?;
    let tree_medium = build_tree_medium_mesh(deformation_margin_m).map_err(|source| {
        VegetationAdapterError::Mesh {
            key: VegetationMeshKey::TreeMedium,
            source,
        }
    })?;
    let tree_medium_triangles = u64_count(tree_medium.triangles, "tree_medium_triangles")?;
    let shrub =
        build_shrub_mesh(deformation_margin_m).map_err(|source| VegetationAdapterError::Mesh {
            key: VegetationMeshKey::Shrub,
            source,
        })?;
    let shrub_triangles = u64_count(shrub.triangles, "shrub_triangles")?;
    let fern =
        build_fern_mesh(deformation_margin_m).map_err(|source| VegetationAdapterError::Mesh {
            key: VegetationMeshKey::Fern,
            source,
        })?;
    let fern_triangles = u64_count(fern.triangles, "fern_triangles")?;

    let mut unique_mesh_asset_triangles = tree_near_triangles
        .checked_add(tree_medium_triangles)
        .and_then(|value| value.checked_add(shrub_triangles))
        .and_then(|value| value.checked_add(fern_triangles))
        .ok_or(VegetationAdapterError::CountOverflow(
            "unique_mesh_asset_triangles",
        ))?;
    let mut meshes = vec![
        PreparedMeshAsset {
            key: VegetationMeshKey::TreeNear,
            value: tree_near,
        },
        PreparedMeshAsset {
            key: VegetationMeshKey::TreeMedium,
            value: tree_medium,
        },
        PreparedMeshAsset {
            key: VegetationMeshKey::Shrub,
            value: shrub,
        },
        PreparedMeshAsset {
            key: VegetationMeshKey::Fern,
            value: fern,
        },
    ];
    let mut entities = Vec::new();
    let mut grass_batches = 0_u64;
    let mut hlod_batches = 0_u64;
    let mut chunk_mesh_assets = 0_u64;
    let mut grass_triangles = 0_u64;
    let mut hlod_triangles = 0_u64;

    for chunk in &generated.chunks {
        let chunk_x = chunk.coordinate.x;
        let chunk_z = chunk.coordinate.z;
        let chunk_center = Vec3::new(
            generated.origin_xz_m[0] + (f32::from(chunk_x) + 0.5) * VEGETATION_CHUNK_SIZE_M,
            0.0,
            generated.origin_xz_m[1] + (f32::from(chunk_z) + 0.5) * VEGETATION_CHUNK_SIZE_M,
        );
        let local = chunk
            .instances
            .iter()
            .map(|instance| procedural_placement(instance, chunk_center))
            .collect::<Vec<_>>();
        let grass = local
            .iter()
            .copied()
            .filter(|placement| placement.kind == VegetationKind::Grass)
            .collect::<Vec<_>>();
        if !grass.is_empty() {
            let key = VegetationMeshKey::GrassChunk {
                x: chunk_x,
                z: chunk_z,
            };
            let mut mesh = build_grass_chunk_mesh(
                plan.terrain(),
                chunk_center,
                &grass,
                profile.grass_blades_per_patch,
                deformation_margin_m,
            )
            .map_err(|source| VegetationAdapterError::Mesh { key, source })?;
            let transform = recenter_grass_batch(&mut mesh, chunk_center);
            let triangles = u64_count(mesh.triangles, "grass_triangles")?;
            grass_triangles = grass_triangles
                .checked_add(triangles)
                .ok_or(VegetationAdapterError::CountOverflow("grass_triangles"))?;
            unique_mesh_asset_triangles =
                unique_mesh_asset_triangles.checked_add(triangles).ok_or(
                    VegetationAdapterError::CountOverflow("unique_mesh_asset_triangles"),
                )?;
            chunk_mesh_assets = chunk_mesh_assets
                .checked_add(1)
                .ok_or(VegetationAdapterError::CountOverflow("chunk_mesh_assets"))?;
            grass_batches = grass_batches
                .checked_add(1)
                .ok_or(VegetationAdapterError::CountOverflow("grass_batches"))?;
            meshes.push(PreparedMeshAsset { key, value: mesh });
            entities.push(PreparedProjectionEntity {
                name: format!("Vegetation grass batch ({chunk_x}, {chunk_z})"),
                mesh_key: key,
                transform,
                mesh_tag: chunk_mesh_tag(chunk_x, chunk_z, 0x0047_0000),
                visibility_range: ranges.grass.clone(),
                marker: PreparedProjectionMarker::Batch(VegetationChunkBatchMarker {
                    chunk_x,
                    chunk_z,
                    lod: VegetationProjectionLod::GrassBatch,
                    logical_placements: chunk.kind_counts.grass_patches,
                }),
                casts_shadow: false,
            });
        }

        let woody = local
            .iter()
            .copied()
            .filter(|placement| placement.kind != VegetationKind::Grass)
            .collect::<Vec<_>>();
        if !woody.is_empty() {
            let key = VegetationMeshKey::HlodChunk {
                x: chunk_x,
                z: chunk_z,
            };
            let maximum_anchor_radius_m = maximum_hlod_anchor_radius_m(&woody);
            let mesh = build_hlod_chunk_mesh(&woody, deformation_margin_m)
                .map_err(|source| VegetationAdapterError::Mesh { key, source })?;
            let triangles = u64_count(mesh.triangles, "hlod_triangles")?;
            hlod_triangles = hlod_triangles
                .checked_add(triangles)
                .ok_or(VegetationAdapterError::CountOverflow("hlod_triangles"))?;
            unique_mesh_asset_triangles =
                unique_mesh_asset_triangles.checked_add(triangles).ok_or(
                    VegetationAdapterError::CountOverflow("unique_mesh_asset_triangles"),
                )?;
            chunk_mesh_assets = chunk_mesh_assets
                .checked_add(1)
                .ok_or(VegetationAdapterError::CountOverflow("chunk_mesh_assets"))?;
            hlod_batches = hlod_batches
                .checked_add(1)
                .ok_or(VegetationAdapterError::CountOverflow("hlod_batches"))?;
            meshes.push(PreparedMeshAsset { key, value: mesh });
            entities.push(PreparedProjectionEntity {
                name: format!("Vegetation HLOD batch ({chunk_x}, {chunk_z})"),
                mesh_key: key,
                transform: Transform::from_translation(chunk_center),
                mesh_tag: chunk_mesh_tag(chunk_x, chunk_z, 0x0048_0000),
                visibility_range: ranges.conservative_hlod_batch_range(maximum_anchor_radius_m),
                marker: PreparedProjectionMarker::HlodBatch(
                    VegetationChunkBatchMarker {
                        chunk_x,
                        chunk_z,
                        lod: VegetationProjectionLod::Hlod,
                        logical_placements: chunk.kind_counts.total()
                            - chunk.kind_counts.grass_patches,
                    },
                    VegetationHlodBatchEnvelope {
                        maximum_anchor_radius_m,
                    },
                ),
                casts_shadow: false,
            });
        }

        for instance in &chunk.instances {
            append_individual_projection(instance, &ranges, &mut entities);
        }
    }

    let near_entities = trees
        .checked_add(shrubs)
        .and_then(|value| value.checked_add(ferns))
        .ok_or(VegetationAdapterError::CountOverflow("near_entities"))?;
    let logically_selected_near_triangles = trees
        .checked_mul(tree_near_triangles)
        .and_then(|value| value.checked_add(shrubs.checked_mul(shrub_triangles)?))
        .and_then(|value| value.checked_add(ferns.checked_mul(fern_triangles)?))
        .and_then(|value| value.checked_add(grass_triangles))
        .ok_or(VegetationAdapterError::CountOverflow(
            "logically_selected_near_triangles",
        ))?;
    let logically_selected_medium_triangles = trees
        .checked_mul(tree_medium_triangles)
        .and_then(|value| value.checked_add(shrubs.checked_mul(shrub_triangles)?))
        .and_then(|value| value.checked_add(ferns.checked_mul(fern_triangles)?))
        .ok_or(VegetationAdapterError::CountOverflow(
            "logically_selected_medium_triangles",
        ))?;
    let shadow_caster_entities = u64_count(
        entities.iter().filter(|entity| entity.casts_shadow).count(),
        "shadow_caster_entities",
    )?;
    let shadow_opt_out_entities = total_entities.checked_sub(shadow_caster_entities).ok_or(
        VegetationAdapterError::CountOverflow("shadow_opt_out_entities"),
    )?;
    let stats = VegetationProjectionStats {
        terrain_document_hash: generated.terrain_document_hash.to_string(),
        vegetation_document_hash: generated.vegetation_document_hash.to_string(),
        vegetation_density_document_hash,
        placements: VegetationPlacementStats {
            candidate_attempts,
            accepted_placements: u64::from(kind_counts.total()),
            accepted_by_kind: kind_counts,
        },
        projection: VegetationProjectionCounts {
            total_entities,
            placement_entities,
            near_entities,
            medium_entities: trees,
            grass_batches,
            hlod_batches,
            grass_blades,
            shared_mesh_assets: 4,
            chunk_mesh_assets,
            material_assets: 1,
            shadow_caster_entities,
            shadow_opt_out_entities,
        },
        triangles: VegetationTriangleStats {
            unique_mesh_asset_triangles,
            logically_selected_near_triangles,
            logically_selected_medium_triangles,
            logically_selected_hlod_triangles: hlod_triangles,
        },
        visibility: VegetationVisibilityStats::default(),
        render_path: VegetationRenderPathStats::default(),
    };

    debug_assert_eq!(grass_batches, grass_batch_entities);
    debug_assert_eq!(hlod_batches, hlod_batch_entities);
    debug_assert_eq!(u64::try_from(entities.len()).ok(), Some(total_entities));

    Ok(PreparedVegetationProjection {
        profile,
        meshes,
        entities,
        stats,
    })
}

fn append_individual_projection(
    instance: &VegetationInstance,
    ranges: &VegetationLodRanges,
    entities: &mut Vec<PreparedProjectionEntity>,
) {
    if instance.kind == VegetationKind::Grass {
        return;
    }
    let translation = Vec3::new(
        instance.position_m.x,
        instance.position_m.y,
        instance.position_m.z,
    );
    let transform = Transform::from_translation(translation)
        .with_rotation(Quat::from_rotation_y(instance.yaw_radians))
        .with_scale(Vec3::splat(instance.scale));
    let mesh_tag = mesh_tag_for(instance.wind_phase_radians, instance.wind_response);
    let base_name = format!("Vegetation {} {}", kind_label(instance.kind), instance.id);
    match instance.kind {
        VegetationKind::Tree => {
            entities.push(PreparedProjectionEntity {
                name: format!("{base_name} near"),
                mesh_key: VegetationMeshKey::TreeNear,
                transform,
                mesh_tag: mesh_tag.clone(),
                visibility_range: ranges.near.clone(),
                marker: PreparedProjectionMarker::Placement(VegetationPlacementMarker {
                    id: instance.id,
                    kind: instance.kind,
                    lod: VegetationProjectionLod::Near,
                }),
                casts_shadow: true,
            });
            entities.push(PreparedProjectionEntity {
                name: format!("{base_name} medium"),
                mesh_key: VegetationMeshKey::TreeMedium,
                transform,
                mesh_tag,
                visibility_range: ranges.medium.clone(),
                marker: PreparedProjectionMarker::Placement(VegetationPlacementMarker {
                    id: instance.id,
                    kind: instance.kind,
                    lod: VegetationProjectionLod::Medium,
                }),
                casts_shadow: false,
            });
        }
        VegetationKind::Shrub | VegetationKind::Fern => {
            let mesh_key = if instance.kind == VegetationKind::Shrub {
                VegetationMeshKey::Shrub
            } else {
                VegetationMeshKey::Fern
            };
            entities.push(PreparedProjectionEntity {
                name: base_name,
                mesh_key,
                transform,
                mesh_tag,
                visibility_range: ranges.individual.clone(),
                marker: PreparedProjectionMarker::Placement(VegetationPlacementMarker {
                    id: instance.id,
                    kind: instance.kind,
                    lod: VegetationProjectionLod::Near,
                }),
                // Only near tree LODs cast in the bounded Gate 1 prototype.
                casts_shadow: false,
            });
        }
        VegetationKind::Grass => {}
    }
}

fn procedural_placement(
    instance: &VegetationInstance,
    chunk_center: Vec3,
) -> ProceduralVegetationPlacement {
    ProceduralVegetationPlacement {
        position_m: Vec3::new(
            instance.position_m.x - chunk_center.x,
            instance.position_m.y,
            instance.position_m.z - chunk_center.z,
        ),
        yaw_radians: instance.yaw_radians,
        scale: instance.scale,
        wind_phase_radians: instance.wind_phase_radians,
        wind_response: instance.wind_response,
        kind: instance.kind,
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn spawn_vegetation_projection(
    mut commands: Commands,
    mut prepared: ResMut<PreparedVegetation>,
    profile: Res<VegetationPresentation>,
    interaction: Res<VegetationInteractionField>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<VisualValleyVegetationMaterial>>,
) {
    let prepared = prepared
        .0
        .take()
        .expect("vegetation projection preparation is consumed exactly once");
    let projection_stats = prepared.stats.clone();
    let material = materials.add(VisualValleyVegetationMaterial {
        base: StandardMaterial {
            base_color: Color::WHITE,
            metallic: 0.0,
            perceptual_roughness: 0.9,
            reflectance: 0.35,
            double_sided: true,
            cull_mode: None,
            opaque_render_method: OpaqueRendererMethod::Forward,
            ..default()
        },
        extension: VisualValleyVegetationExtension::from_profile_and_interaction(
            profile.0,
            *interaction,
        ),
    });

    let mut mesh_handles = HashMap::with_capacity(prepared.meshes.len());
    for prepared_mesh in prepared.meshes {
        let aabb = prepared_mesh.value.aabb;
        let handle = meshes.add(prepared_mesh.value.mesh);
        mesh_handles.insert(prepared_mesh.key, (handle, aabb));
    }

    for prepared_entity in prepared.entities {
        let Some((mesh, aabb)) = mesh_handles.get(&prepared_entity.mesh_key) else {
            panic!(
                "{}",
                VegetationAdapterError::MissingMeshRecipe(prepared_entity.mesh_key)
            );
        };
        let mut entity = commands.spawn((
            Name::new(prepared_entity.name),
            Mesh3d(mesh.clone()),
            MeshMaterial3d(material.clone()),
            prepared_entity.mesh_tag,
            prepared_entity.transform,
            prepared_entity.visibility_range,
            *aabb,
            NoAutoAabb,
        ));
        match prepared_entity.marker {
            PreparedProjectionMarker::Placement(marker) => {
                entity.insert(marker);
            }
            PreparedProjectionMarker::Batch(marker) => {
                entity.insert(marker);
            }
            PreparedProjectionMarker::HlodBatch(marker, envelope) => {
                entity.insert((marker, envelope));
            }
        }
        if !prepared_entity.casts_shadow {
            entity.insert(NotShadowCaster);
        }
    }
    commands.insert_resource(VegetationMaterialHandle(material));
    info!(
        target: "forge::vegetation",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "visual_valley_vegetation_materialized",
        terrain_document_hash = %projection_stats.terrain_document_hash,
        vegetation_document_hash = %projection_stats.vegetation_document_hash,
        vegetation_density_document_hash = %projection_stats
            .vegetation_density_document_hash
            .as_deref()
            .unwrap_or("implicit_full_density"),
        accepted_placements = projection_stats.placements.accepted_placements,
        projection_entities = projection_stats.projection.total_entities,
        grass_batches = projection_stats.projection.grass_batches,
        hlod_batches = projection_stats.projection.hlod_batches,
        grass_blades = projection_stats.projection.grass_blades,
        "Visual Valley vegetation materialized from the canonical documents"
    );
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn update_vegetation_interaction_uniform(
    time: Res<Time>,
    interaction: Res<VegetationInteractionField>,
    profile: Res<VegetationPresentation>,
    material: Option<Res<VegetationMaterialHandle>>,
    mut materials: ResMut<Assets<VisualValleyVegetationMaterial>>,
) {
    let Some(material) = material else {
        return;
    };
    let Some(mut material) = materials.get_mut(&material.0) else {
        return;
    };
    let bounded = VegetationInteractionField::bounded(
        interaction.center_m,
        interaction.push_direction_xz,
        interaction.radius_m,
        interaction.strength,
        profile.0,
    );
    let mut extension =
        VisualValleyVegetationExtension::from_profile_and_interaction(profile.0, bounded);
    extension.wetness_parameters = material.extension.wetness_parameters;
    let wrap_period = time.wrap_period().as_secs_f32();
    let current_time = finite_clamp(time.elapsed_secs_wrapped(), 0.0, wrap_period);
    let previous_time =
        (current_time - finite_clamp(time.delta_secs(), 0.0, wrap_period)).rem_euclid(wrap_period);
    extension.wind_parameters.z = current_time;
    extension.wind_parameters.w = previous_time;
    material.extension = extension;
}

#[cfg(feature = "physics-rapier")]
fn player_vegetation_interaction(
    snapshot: &PlayerRuntimeSnapshot,
    profile: VegetationPresentationProfile,
) -> VegetationInteractionField {
    let horizontal_velocity = Vec2::new(
        snapshot.horizontal_velocity.x,
        snapshot.horizontal_velocity.z,
    );
    let camera_forward = Vec2::new(
        -snapshot.camera_yaw_radians.sin(),
        -snapshot.camera_yaw_radians.cos(),
    );
    let push_direction = horizontal_velocity
        .try_normalize()
        .unwrap_or(camera_forward);
    VegetationInteractionField::bounded(
        snapshot.player_position,
        push_direction,
        profile.interaction_radius_m,
        profile.interaction_strength,
        profile,
    )
}

#[cfg(feature = "physics-rapier")]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems own their system parameter wrappers"
)]
fn publish_player_vegetation_interaction(
    snapshot: Option<Res<PlayerRuntimeSnapshot>>,
    profile: Res<VegetationPresentation>,
    mut interaction: ResMut<VegetationInteractionField>,
) {
    let Some(snapshot) = snapshot else {
        return;
    };
    let projected = player_vegetation_interaction(&snapshot, profile.0);
    if *interaction != projected {
        *interaction = projected;
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::type_complexity,
    reason = "the observation joins Bevy's active view, range table and Forge projection markers"
)]
fn observe_vegetation_visibility(
    cameras: Query<(Entity, &Camera, &VisibleEntities, Has<NoIndirectDrawing>), With<Camera3d>>,
    projections: Query<
        (
            Entity,
            Option<&VegetationPlacementMarker>,
            Option<&VegetationChunkBatchMarker>,
        ),
        Or<(
            With<VegetationPlacementMarker>,
            With<VegetationChunkBatchMarker>,
        )>,
    >,
    visible_ranges: Res<VisibleEntityRanges>,
    time: Option<Res<Time<Real>>>,
    presentation: Res<VegetationPresentation>,
    mut visible_meshes: Local<EntityHashSet>,
    mut audit_control: ResMut<VegetationVisibilityAuditControl>,
    mut audit_cadence: ResMut<VegetationVisibilityAuditCadence>,
    mut stats: ResMut<VegetationProjectionStats>,
) {
    audit_cadence.record_main_world_observation_frame();
    let mut active_cameras = cameras.iter().filter(|(_, camera, _, _)| camera.is_active);
    let active_camera = active_cameras.next();
    let additional_active_cameras = active_cameras.count();
    let active_camera_views =
        u64::try_from(usize::from(active_camera.is_some()) + additional_active_cameras)
            .unwrap_or(u64::MAX);
    let observation_status = match active_camera_views {
        0 => VegetationVisibilityObservationStatus::UnavailableNoActiveCamera,
        1 => VegetationVisibilityObservationStatus::ObservedSingleActiveCamera,
        _ => VegetationVisibilityObservationStatus::UnavailableMultipleActiveCameras,
    };
    let single_active_camera = if active_camera_views == 1 {
        active_camera.map(|(entity, _, _, _)| entity)
    } else {
        None
    };
    let single_active_camera_disables_indirect_drawing = active_camera_views == 1
        && active_camera.is_some_and(|(_, _, _, no_indirect_drawing)| no_indirect_drawing);
    let semantics = VegetationVisibilityAuditSemantics::new(
        active_camera_views,
        single_active_camera,
        single_active_camera_disables_indirect_drawing,
        presentation.0,
    );
    let pending_ticket = audit_control.pending_ticket();
    let now = time.as_deref().map(Time::elapsed);
    if !audit_cadence.observation_is_due(now, pending_ticket.is_some(), semantics) {
        return;
    }

    let mut observation = VegetationVisibilityStats {
        observation_frames: audit_cadence.main_world_observation_frames(),
        status: observation_status,
        active_camera_views,
        ..default()
    };
    if let Some((camera_entity, _, camera_visible_entities, no_indirect_drawing)) =
        active_camera.filter(|_| additional_active_cameras == 0)
    {
        visible_meshes.clear();
        visible_meshes.extend(
            camera_visible_entities
                .iter(TypeId::of::<Mesh3d>())
                .copied(),
        );
        for (entity, placement, batch) in &projections {
            let in_range = visible_ranges.entity_is_in_range_of_view(entity, camera_entity);
            let cpu_visible_for_camera = visible_meshes.contains(&entity);
            if cpu_visible_for_camera && !in_range {
                observation.visible_outside_range_inconsistencies = observation
                    .visible_outside_range_inconsistencies
                    .saturating_add(1);
            }
            observation.total.observe(in_range, cpu_visible_for_camera);
            let bucket = match (
                placement.map(|marker| marker.lod),
                batch.map(|marker| marker.lod),
            ) {
                (Some(VegetationProjectionLod::Near), None) => &mut observation.near,
                (Some(VegetationProjectionLod::Medium), None) => &mut observation.medium,
                (None, Some(VegetationProjectionLod::Hlod)) => &mut observation.hlod,
                (None, Some(VegetationProjectionLod::GrassBatch)) => &mut observation.grass_batches,
                _ => continue,
            };
            bucket.observe(in_range, cpu_visible_for_camera);
        }
        stats.render_path.active_camera_gpu_preprocessing = resolve_camera_preprocessing(
            stats.render_path.maximum_gpu_preprocessing,
            no_indirect_drawing,
        );
    } else {
        stats.render_path.active_camera_gpu_preprocessing = VegetationGpuPreprocessingMode::Unknown;
    }
    stats.visibility = observation;
    audit_cadence.record_observation(now, semantics);
    audit_control.complete_through(pending_ticket);
}

const fn vegetation_gpu_preprocessing_mode(
    mode: GpuPreprocessingMode,
) -> VegetationGpuPreprocessingMode {
    match mode {
        GpuPreprocessingMode::None => VegetationGpuPreprocessingMode::None,
        GpuPreprocessingMode::PreprocessingOnly => {
            VegetationGpuPreprocessingMode::PreprocessingOnly
        }
        GpuPreprocessingMode::Culling => VegetationGpuPreprocessingMode::Culling,
    }
}

const fn resolve_camera_preprocessing(
    maximum: VegetationGpuPreprocessingMode,
    no_indirect_drawing: bool,
) -> VegetationGpuPreprocessingMode {
    match (maximum, no_indirect_drawing) {
        (VegetationGpuPreprocessingMode::Unknown, _) => VegetationGpuPreprocessingMode::Unknown,
        (VegetationGpuPreprocessingMode::None, _) => VegetationGpuPreprocessingMode::None,
        (VegetationGpuPreprocessingMode::PreprocessingOnly, _) | (_, true) => {
            VegetationGpuPreprocessingMode::PreprocessingOnly
        }
        (VegetationGpuPreprocessingMode::Culling, false) => VegetationGpuPreprocessingMode::Culling,
    }
}

fn u64_count(value: usize, field: &'static str) -> Result<u64, VegetationAdapterError> {
    u64::try_from(value).map_err(|_| VegetationAdapterError::CountOverflow(field))
}

fn ensure_budget(
    field: &'static str,
    actual: u64,
    maximum: u64,
) -> Result<(), VegetationAdapterError> {
    if actual > maximum {
        return Err(VegetationAdapterError::BudgetExceeded {
            field,
            actual,
            maximum,
        });
    }
    Ok(())
}

fn chunk_mesh_tag(x: u16, z: u16, phase_prefix: u32) -> MeshTag {
    MeshTag(0xff00_0000 | phase_prefix | u32::from(x) << 8 | u32::from(z))
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "finite normalized phase and response are deliberately quantized into MeshTag bits"
)]
fn mesh_tag_for(phase_radians: f32, wind_response: f32) -> MeshTag {
    let normalized_phase = phase_radians.rem_euclid(std::f32::consts::TAU) / std::f32::consts::TAU;
    let phase = (normalized_phase * TAG_PHASE_SCALE).round() as u32;
    let response = (finite_clamp(wind_response, 0.0, 1.0) * 255.0).round() as u32;
    MeshTag(response << 24 | (phase & TAG_PHASE_MASK))
}

const fn kind_label(kind: VegetationKind) -> &'static str {
    match kind {
        VegetationKind::Tree => "tree",
        VegetationKind::Shrub => "shrub",
        VegetationKind::Fern => "fern",
        VegetationKind::Grass => "grass",
    }
}

/// Components deliberately kept visible here because tests assert that the projection uses them
/// instead of disabling batching or culling. Runtime spawning happens in the adapter section below.
#[allow(dead_code)]
fn projection_policy_markers() -> (NoAutoAabb, NotShadowCaster) {
    (NoAutoAabb, NotShadowCaster)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::mesh::VertexAttributeValues;
    use forge_project::{NewProjectCommand, VegetationDensityOverride, WorkspaceDocument};
    use forge_world::{
        embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
    };
    use std::collections::HashSet;

    fn profile() -> VegetationPresentationProfile {
        VegetationPresentationProfile {
            near_lod_distance_m: 42.0,
            medium_lod_distance_m: 58.0,
            hlod_distance_m: 180.0,
            cull_distance_m: 420.0,
            shadow_distance_m: 96.0,
            wind_strength: 0.7,
            wind_speed_hz: 0.22,
            interaction_radius_m: 2.4,
            interaction_strength: 0.8,
            grass_blades_per_patch: 12,
        }
    }

    fn visibility_semantics(
        camera: Option<Entity>,
        presentation: VegetationPresentationProfile,
    ) -> VegetationVisibilityAuditSemantics {
        VegetationVisibilityAuditSemantics::new(
            u64::from(camera.is_some()),
            camera,
            false,
            presentation,
        )
    }

    #[test]
    fn visibility_audit_is_due_immediately_without_a_real_clock() {
        let semantics = visibility_semantics(None, profile());
        let mut cadence = VegetationVisibilityAuditCadence::default();

        assert_eq!(cadence.main_world_observation_frames(), 0);
        cadence.record_main_world_observation_frame();
        assert_eq!(cadence.main_world_observation_frames(), 1);
        assert!(cadence.observation_is_due(None, false, semantics));
        cadence.record_observation(None, semantics);
        assert!(!cadence.observation_is_due(None, false, semantics));
        assert!(cadence.observation_is_due(None, true, semantics));
        assert_eq!(cadence.interval(), Duration::from_millis(100));
        assert_eq!(cadence.completed_observations(), 1);
    }

    #[test]
    fn visibility_audit_deadline_is_exact_and_uses_real_elapsed_time() {
        let semantics = visibility_semantics(None, profile());
        let mut cadence = VegetationVisibilityAuditCadence::default();
        cadence.record_observation(Some(Duration::from_secs(3)), semantics);

        assert!(!cadence.observation_is_due(Some(Duration::from_millis(3_099)), false, semantics,));
        assert!(cadence.observation_is_due(Some(Duration::from_millis(3_100)), false, semantics,));
        assert_eq!(cadence.last_observed_at(), Some(Duration::from_secs(3)));
    }

    #[test]
    fn forced_visibility_audit_ticket_is_monotonic_and_consumed_once() {
        let semantics = visibility_semantics(None, profile());
        let mut cadence = VegetationVisibilityAuditCadence::default();
        cadence.record_observation(Some(Duration::ZERO), semantics);
        let mut control = VegetationVisibilityAuditControl::default();

        let first = control.request_observation();
        assert_eq!(first.id(), 1);
        assert_eq!(control.requested_id(), 1);
        assert_eq!(control.completed_id(), 0);
        assert!(!control.request_is_completed(first));
        assert!(cadence.observation_is_due(
            Some(Duration::from_millis(1)),
            control.pending_ticket().is_some(),
            semantics,
        ));

        cadence.record_observation(Some(Duration::from_millis(1)), semantics);
        control.complete_through(control.pending_ticket());
        assert!(control.request_is_completed(first));
        assert_eq!(control.completed_id(), 1);
        assert!(!cadence.observation_is_due(
            Some(Duration::from_millis(2)),
            control.pending_ticket().is_some(),
            semantics,
        ));

        let second = control.request_observation();
        assert_eq!(second.id(), 2);
        assert!(second > first);
        assert!(!control.request_is_completed(second));
        assert!(cadence.observation_is_due(
            Some(Duration::from_millis(2)),
            control.pending_ticket().is_some(),
            semantics,
        ));
    }

    #[test]
    fn relevant_camera_or_quality_semantics_force_an_early_visibility_audit() {
        let camera = Entity::from_raw_u32(7).unwrap();
        let original = visibility_semantics(Some(camera), profile());
        let mut cadence = VegetationVisibilityAuditCadence::default();
        cadence.record_observation(Some(Duration::from_secs(1)), original);

        assert!(!cadence.observation_is_due(Some(Duration::from_millis(1_001)), false, original,));
        assert!(cadence.observation_is_due(
            Some(Duration::from_millis(1_001)),
            false,
            visibility_semantics(None, profile()),
        ));

        let mut lower_distance = profile();
        lower_distance.cull_distance_m -= 1.0;
        assert!(cadence.observation_is_due(
            Some(Duration::from_millis(1_001)),
            false,
            visibility_semantics(Some(camera), lower_distance),
        ));
    }

    fn placement(kind: VegetationKind, x: f32, z: f32) -> ProceduralVegetationPlacement {
        ProceduralVegetationPlacement {
            position_m: Vec3::new(x, 1.25, z),
            yaw_radians: 0.37,
            scale: 1.1,
            wind_phase_radians: 2.4,
            wind_response: 0.75,
            kind,
        }
    }

    fn assert_complete_mesh(mesh: &DerivedVegetationMesh) {
        let vertices = mesh.mesh.count_vertices();
        assert!(vertices > 0);
        assert!(mesh.triangles > 0);
        assert!(matches!(
            mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION),
            Some(VertexAttributeValues::Float32x3(values)) if values.len() == vertices
        ));
        assert!(matches!(
            mesh.mesh.attribute(Mesh::ATTRIBUTE_NORMAL),
            Some(VertexAttributeValues::Float32x3(values)) if values.len() == vertices
        ));
        assert!(matches!(
            mesh.mesh.attribute(Mesh::ATTRIBUTE_COLOR),
            Some(VertexAttributeValues::Float32x4(values)) if values.len() == vertices
        ));
        let Some(VertexAttributeValues::Float32x4(colors)) =
            mesh.mesh.attribute(Mesh::ATTRIBUTE_COLOR)
        else {
            panic!("vegetation COLOR must be Float32x4");
        };
        for color in colors {
            assert!((color[0] + color[1] + color[2] - 1.0).abs() <= f32::EPSILON);
            assert!((0.0..=1.0).contains(&color[3]));
        }
        assert!(
            colors
                .iter()
                .any(|color| color[3].to_bits() == 0.0_f32.to_bits())
        );
        assert!(matches!(
            mesh.mesh.indices(),
            Some(Indices::U32(values)) if values.len() == mesh.triangles * 3
        ));
    }

    #[test]
    fn shared_meshes_have_complete_finite_indexed_payloads() {
        let margin = maximum_deformation_m(profile());
        let near = build_tree_near_mesh(margin).unwrap();
        let medium = build_tree_medium_mesh(margin).unwrap();
        let shrub = build_shrub_mesh(margin).unwrap();
        let fern = build_fern_mesh(margin).unwrap();

        for mesh in [&near, &medium, &shrub, &fern] {
            assert_complete_mesh(mesh);
            assert!(mesh.mesh.attribute(Mesh::ATTRIBUTE_UV_1).is_none());
            assert!(mesh.aabb.half_extents.x < 16.0);
            assert!(mesh.aabb.half_extents.y < 16.0);
            assert!(mesh.aabb.half_extents.z < 16.0);
        }
        assert_eq!((near.mesh.count_vertices(), near.triangles), (113, 43));
        assert_eq!((medium.mesh.count_vertices(), medium.triangles), (56, 22));
        assert_eq!((shrub.mesh.count_vertices(), shrub.triangles), (63, 21));
        assert_eq!((fern.mesh.count_vertices(), fern.triangles), (28, 14));
    }

    #[test]
    fn grass_is_one_merged_mesh_with_exact_blade_counts() {
        let terrain_document = embedded_visual_valley_terrain_document().unwrap();
        let terrain = TerrainGenerationPlan::from_document(&terrain_document).unwrap();
        let patches = [
            placement(VegetationKind::Grass, -1.0, 0.0),
            placement(VegetationKind::Grass, 1.0, 0.0),
        ];
        let mesh = build_grass_chunk_mesh(
            &terrain,
            Vec3::ZERO,
            &patches,
            12,
            maximum_deformation_m(profile()),
        )
        .unwrap();

        assert_complete_mesh(&mesh);
        assert_eq!(mesh.mesh.count_vertices(), 2 * 12 * 7);
        assert_eq!(mesh.triangles, 2 * 12 * 3);
        let positions = match mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION) {
            Some(VertexAttributeValues::Float32x3(values)) => values,
            other => panic!("unexpected grass positions: {other:?}"),
        };
        for blade in positions.as_chunks::<7>().0 {
            let base_x = f32::midpoint(blade[0][0], blade[1][0]);
            let base_y = f32::midpoint(blade[0][1], blade[1][1]);
            let base_z = f32::midpoint(blade[0][2], blade[1][2]);
            let surface = terrain.surface_at(base_x, base_z).unwrap();
            assert!((base_y - surface.height_m).abs() <= 1.0e-4);
        }

        let duplicate = build_grass_chunk_mesh(
            &terrain,
            Vec3::ZERO,
            &patches,
            12,
            maximum_deformation_m(profile()),
        )
        .unwrap();
        assert_eq!(
            mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION),
            duplicate.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        );
        assert_eq!(mesh.mesh.indices(), duplicate.mesh.indices());
        for attribute in [Mesh::ATTRIBUTE_NORMAL, Mesh::ATTRIBUTE_COLOR] {
            assert_eq!(
                mesh.mesh.attribute(attribute),
                duplicate.mesh.attribute(attribute)
            );
        }
        assert_eq!(
            build_grass_chunk_mesh(&terrain, Vec3::ZERO, &patches, 0, 0.0).unwrap_err(),
            VegetationMeshError::GrassBladeCountIsZero
        );
    }

    #[test]
    fn grass_blades_are_short_tapered_bent_and_rooted_with_continuous_wind_weights() {
        let terrain_document = embedded_visual_valley_terrain_document().unwrap();
        let terrain = TerrainGenerationPlan::from_document(&terrain_document).unwrap();
        for scale in [0.65, 1.0, 1.4] {
            let mut patch = placement(VegetationKind::Grass, 0.0, 0.0);
            patch.scale = scale;
            let mesh = build_grass_chunk_mesh(&terrain, Vec3::ZERO, &[patch], 24, 0.0).unwrap();
            let Some(VertexAttributeValues::Float32x3(positions)) =
                mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                panic!("grass positions must be Float32x3");
            };
            let Some(VertexAttributeValues::Float32x4(colors)) =
                mesh.mesh.attribute(Mesh::ATTRIBUTE_COLOR)
            else {
                panic!("grass colors must be Float32x4");
            };
            let mut maximum_root_radius = 0.0_f32;
            for (blade, weights) in positions
                .as_chunks::<7>()
                .0
                .iter()
                .zip(colors.as_chunks::<7>().0)
            {
                let points = blade.map(Vec3::from);
                let base = points[0].midpoint(points[1]);
                let elbow = points[2].midpoint(points[3]);
                let tip = points[6];
                let height = tip.y - base.y;
                let width = points[0].distance(points[1]);
                assert!((0.25 * scale..=0.5 * scale).contains(&height));
                assert!((0.035 * scale..=0.065 * scale).contains(&width));
                assert!((elbow.y - base.y - height * 0.55).abs() < 1.0e-5);
                assert!((points[2].distance(points[3]) - width * 0.5).abs() < 1.0e-5);
                assert_eq!(points[3], points[4]);
                assert_eq!(points[2], points[5]);
                let tip_bend = (tip - base).with_y(0.0);
                assert!((0.18 * height..=0.34 * height).contains(&tip_bend.length()));
                assert!(
                    (elbow - base)
                        .with_y(0.0)
                        .abs_diff_eq(tip_bend * 0.22, 1.0e-5)
                );
                assert!(base.with_y(0.0).length() <= GRASS_PATCH_RADIUS_M * scale);
                maximum_root_radius = maximum_root_radius.max(base.with_y(0.0).length());
                assert!(
                    (base.y - terrain.surface_at(base.x, base.z).unwrap().height_m).abs() < 1.0e-4
                );
                assert_eq!(weights[0][3].to_bits(), 0.0_f32.to_bits());
                assert_eq!(weights[1][3].to_bits(), 0.0_f32.to_bits());
                for weight in &weights[2..6] {
                    assert_eq!(weight[3].to_bits(), (patch.wind_response * 0.55).to_bits());
                }
                assert_eq!(weights[6][3].to_bits(), patch.wind_response.to_bits());
            }
            assert!((2.45 * scale..2.5 * scale).contains(&maximum_root_radius));
        }
    }

    #[test]
    fn grass_footprint_preserves_root_clearance_at_maximum_authored_scale() {
        let terrain_document = embedded_visual_valley_terrain_document().unwrap();
        let terrain = TerrainGenerationPlan::from_document(&terrain_document).unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let grass = vegetation
            .layers
            .iter()
            .find(|layer| layer.kind == VegetationKind::Grass)
            .unwrap();
        let radius = GRASS_PATCH_RADIUS_M * grass.maximum_scale;
        let blades = vegetation.presentation.grass_blades_per_patch;
        assert_eq!(GRASS_PATCH_RADIUS_M.to_bits(), 2.5_f32.to_bits());
        assert_eq!(radius.to_bits(), 3.125_f32.to_bits());

        for exclusion in &vegetation.exclusions {
            let (center, exclusion_radius, circular) = match exclusion {
                forge_project::VegetationExclusion::Circle {
                    center_x_m,
                    center_z_m,
                    radius_m,
                    ..
                } => (Vec3::new(*center_x_m, 0.0, *center_z_m), *radius_m, true),
                forge_project::VegetationExclusion::CorridorSegment {
                    start_x_m,
                    start_z_m,
                    end_x_m,
                    end_z_m,
                    half_width_m,
                    ..
                } => {
                    assert_eq!(start_x_m.to_bits(), end_x_m.to_bits());
                    (
                        Vec3::new(*start_x_m, 0.0, f32::midpoint(*start_z_m, *end_z_m)),
                        *half_width_m,
                        false,
                    )
                }
            };
            // Exclusions reject patch centres, not individual blades. The wider footprint
            // may enter the authored margin but retains this smaller root-free core.
            let root_clearance = exclusion_radius - radius;
            let expected_clearance = if circular { 10.875_f32 } else { 1.875_f32 };
            assert_eq!(root_clearance.to_bits(), expected_clearance.to_bits());
            for side in [-1.0, 1.0] {
                let patch_center = center + Vec3::X * side * (exclusion_radius + 0.001);
                let mut patch = placement(VegetationKind::Grass, patch_center.x, patch_center.z);
                patch.scale = grass.maximum_scale;
                let mesh =
                    build_grass_chunk_mesh(&terrain, Vec3::ZERO, &[patch], blades, 0.0).unwrap();
                assert_eq!(mesh.mesh.count_vertices(), usize::from(blades) * 7);
                assert_eq!(mesh.triangles, usize::from(blades) * 3);
                let Some(VertexAttributeValues::Float32x3(positions)) =
                    mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
                else {
                    panic!("grass positions must be Float32x3");
                };
                let mut maximum_root_radius = 0.0_f32;
                for blade in positions.as_chunks::<7>().0 {
                    let base = Vec3::from(blade[0]).midpoint(Vec3::from(blade[1]));
                    let root_radius = (base - patch_center).with_y(0.0).length();
                    assert!(root_radius <= radius + 1.0e-4);
                    maximum_root_radius = maximum_root_radius.max(root_radius);
                    let clearance = if circular {
                        (base - center).with_y(0.0).length()
                    } else {
                        (base.x - center.x).abs()
                    };
                    assert!(clearance >= root_clearance);
                    assert!(
                        (base.y - terrain.surface_at(base.x, base.z).unwrap().height_m).abs()
                            < 1.0e-4
                    );
                }
                let expected_outer_radius =
                    radius * ((f32::from(blades) - 0.5) / f32::from(blades)).sqrt();
                assert!((maximum_root_radius - expected_outer_radius).abs() < 1.0e-4);
                assert!(maximum_root_radius > radius * 0.99);
            }
        }
    }

    #[test]
    fn bent_grass_aabb_contains_all_vertices_and_full_horizontal_deformation() {
        let terrain_document = embedded_visual_valley_terrain_document().unwrap();
        let terrain = TerrainGenerationPlan::from_document(&terrain_document).unwrap();
        let chunk_center = Vec3::new(32.0, 0.0, -96.0);
        let patches = [
            placement(VegetationKind::Grass, -12.0, 7.0),
            placement(VegetationKind::Grass, 2.0, -3.0),
        ];
        let margin = maximum_deformation_m(profile());
        let mut mesh =
            build_grass_chunk_mesh(&terrain, chunk_center, &patches, 24, margin).unwrap();
        let _transform = recenter_grass_batch(&mut mesh, chunk_center);
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("grass positions must be Float32x3");
        };
        let min = Vec3::from(mesh.aabb.min()) - Vec3::splat(1.0e-5);
        let max = Vec3::from(mesh.aabb.max()) + Vec3::splat(1.0e-5);
        for position in positions {
            for x in [-margin, margin] {
                for z in [-margin, margin] {
                    let deformed = Vec3::from(*position) + Vec3::new(x, 0.0, z);
                    assert!(deformed.cmpge(min).all() && deformed.cmple(max).all());
                }
            }
        }
    }

    #[test]
    fn grass_recentering_preserves_world_geometry_and_non_position_attributes() {
        let terrain_document = embedded_visual_valley_terrain_document().unwrap();
        let terrain = TerrainGenerationPlan::from_document(&terrain_document).unwrap();
        let chunk_center = Vec3::new(32.0, 0.0, -96.0);
        let patches = [
            placement(VegetationKind::Grass, -12.0, 7.0),
            placement(VegetationKind::Grass, 3.0, 18.0),
        ];
        let mut mesh = build_grass_chunk_mesh(
            &terrain,
            chunk_center,
            &patches,
            12,
            maximum_deformation_m(profile()),
        )
        .unwrap();
        let original = mesh.mesh.clone();
        let original_center = mesh.aabb.center;
        let original_half_extents = mesh.aabb.half_extents;
        let original_triangles = mesh.triangles;
        let transform = recenter_grass_batch(&mut mesh, chunk_center);

        let Some(VertexAttributeValues::Float32x3(before)) =
            original.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("grass positions must be Float32x3");
        };
        let Some(VertexAttributeValues::Float32x3(after)) =
            mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("grass positions must be Float32x3");
        };
        assert_eq!(before.len(), after.len());
        for (before, after) in before.iter().zip(after) {
            let original_world = chunk_center + Vec3::from_array(*before);
            let recentered_world = transform.transform_point(Vec3::from_array(*after));
            assert!(original_world.abs_diff_eq(recentered_world, 0.0001));
        }
        for attribute in [
            Mesh::ATTRIBUTE_NORMAL,
            Mesh::ATTRIBUTE_UV_0,
            Mesh::ATTRIBUTE_UV_1,
            Mesh::ATTRIBUTE_COLOR,
        ] {
            assert_eq!(
                original.attribute(attribute),
                mesh.mesh.attribute(attribute)
            );
        }
        assert_eq!(original.indices(), mesh.mesh.indices());
        assert_eq!(original_triangles, mesh.triangles);
        assert_eq!(mesh.aabb.half_extents, original_half_extents);
        assert_eq!(mesh.aabb.center, bevy::math::Vec3A::ZERO);
        assert_eq!(
            transform.translation,
            chunk_center + Vec3::from(original_center)
        );
        assert_eq!(transform.rotation, Quat::IDENTITY);
        assert_eq!(transform.scale, Vec3::ONE);
    }

    #[test]
    fn grass_recentering_aligns_cpu_and_shader_range_centers_on_elevated_ground() {
        let mut accumulator = MeshAccumulator::default();
        accumulator
            .push_quad(
                [
                    Vec3::new(12.0, 35.0, -8.0),
                    Vec3::new(14.0, 35.0, -8.0),
                    Vec3::new(14.0, 37.0, -8.0),
                    Vec3::new(12.0, 37.0, -8.0),
                ],
                [grass_color(0, 0.0); 4],
            )
            .unwrap();
        let mut mesh = accumulator.finish(0.5).unwrap();
        let chunk_center = Vec3::new(192.0, 0.0, 128.0);
        let original_cpu_center = chunk_center + Vec3::from(mesh.aabb.center);
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let range = VegetationLodRanges::from_profile(vegetation.presentation).grass;
        assert!(range.use_aabb);
        assert_eq!(range.end_margin, 45.0..51.0);
        let transform = recenter_grass_batch(&mut mesh, chunk_center);
        let cpu_center = Vec3::from(
            GlobalTransform::from(transform)
                .affine()
                .transform_point3a(mesh.aabb.center),
        );
        let shader_center = transform.translation;
        assert_eq!(cpu_center, original_cpu_center);
        assert_eq!(cpu_center, shader_center);
        for distance in [0.0, 44.0, 45.0, 48.0, 51.0, 52.0] {
            let camera = original_cpu_center + Vec3::X * distance;
            let cpu_distance = camera.distance(cpu_center);
            let shader_distance = camera.distance(shader_center);
            assert!((cpu_distance - shader_distance).abs() <= f32::EPSILON);
            assert_eq!(
                range.is_visible_at_all(cpu_distance),
                range.is_visible_at_all(shader_distance)
            );
        }
        let camera = original_cpu_center + Vec3::X * 48.0;
        assert!(range.is_visible_at_all(camera.distance(cpu_center)));
        assert!(camera.distance(chunk_center) > range.end_margin.end);
    }

    #[test]
    fn hlod_is_one_mesh_and_ignores_grass_placements() {
        let woody = [
            placement(VegetationKind::Tree, -2.0, 1.0),
            placement(VegetationKind::Shrub, 0.0, 1.0),
            placement(VegetationKind::Fern, 2.0, 1.0),
            placement(VegetationKind::Grass, 4.0, 1.0),
        ];
        let mesh = build_hlod_chunk_mesh(&woody, maximum_deformation_m(profile())).unwrap();

        assert_complete_mesh(&mesh);
        assert_eq!(mesh.mesh.count_vertices(), 16 + 8 + 8);
        assert_eq!(mesh.triangles, 8 + 4 + 4);
    }

    #[test]
    fn tree_hlod_separates_a_trunk_from_a_tapered_canopy_at_one_anchor() {
        const POSITION_EPSILON_M: f32 = 1.0e-6;
        let mut accumulator = MeshAccumulator::default();
        let anchor = Vec3::new(3.0, 1.25, -4.0);
        append_crossed_tree_hlod(&mut accumulator, anchor, 0.0, 2.0, 10.0, 0.8).unwrap();
        let mesh = accumulator.finish(0.0).unwrap();
        let positions = match mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION) {
            Some(VertexAttributeValues::Float32x3(values)) => values,
            other => panic!("unexpected HLOD positions: {other:?}"),
        };
        let primary_uvs = match mesh.mesh.attribute(Mesh::ATTRIBUTE_UV_0) {
            Some(VertexAttributeValues::Float32x2(values)) => values,
            other => panic!("unexpected HLOD primary UVs: {other:?}"),
        };
        let anchors_xz = match mesh.mesh.attribute(Mesh::ATTRIBUTE_UV_1) {
            Some(VertexAttributeValues::Float32x2(values)) => values,
            other => panic!("unexpected HLOD anchor UVs: {other:?}"),
        };
        let colors = match mesh.mesh.attribute(Mesh::ATTRIBUTE_COLOR) {
            Some(VertexAttributeValues::Float32x4(values)) => values,
            other => panic!("unexpected HLOD colors: {other:?}"),
        };

        assert_eq!(mesh.mesh.count_vertices(), 2 * 2 * 4);
        assert_eq!(mesh.triangles, 2 * 2 * 2);
        let expected_trunk = [
            anchor + Vec3::new(-0.2, 0.0, 0.0),
            anchor + Vec3::new(0.2, 0.0, 0.0),
            anchor + Vec3::new(0.2, 3.8, 0.0),
            anchor + Vec3::new(-0.2, 3.8, 0.0),
        ];
        for (actual, expected) in positions.iter().take(4).zip(expected_trunk) {
            assert!(
                Vec3::from_array(*actual).distance_squared(expected)
                    <= POSITION_EPSILON_M * POSITION_EPSILON_M
            );
        }
        let expected_canopy = [
            anchor + Vec3::new(-1.0, 2.0, 0.0),
            anchor + Vec3::new(1.0, 2.0, 0.0),
            anchor + Vec3::new(0.2, 10.0, 0.0),
            anchor + Vec3::new(-0.2, 10.0, 0.0),
        ];
        for (actual, expected) in positions.iter().skip(4).take(4).zip(expected_canopy) {
            assert!(
                Vec3::from_array(*actual).distance_squared(expected)
                    <= POSITION_EPSILON_M * POSITION_EPSILON_M
            );
        }
        assert_eq!(primary_uvs.len(), 16);
        assert_eq!(anchors_xz.len(), 16);
        assert!(
            anchors_xz
                .iter()
                .all(|uv| { (*uv).map(f32::to_bits) == [anchor.x, anchor.z].map(f32::to_bits) })
        );
        for (index, uv) in primary_uvs.iter().enumerate() {
            let expected_weight: f32 = if index % 8 < 4 { 0.0 } else { 1.0 };
            assert_eq!(uv[0].to_bits(), anchor.y.to_bits());
            assert_eq!(uv[1].to_bits(), expected_weight.to_bits());
        }
        assert!(
            colors
                .iter()
                .take(4)
                .all(|color| color[..3] == [0.0, 0.0, 1.0])
        );
        assert!(
            colors
                .iter()
                .skip(4)
                .take(4)
                .all(|color| color[..3] == [0.35, 0.65, 0.0])
        );
    }

    #[test]
    fn explicit_aabb_contains_the_shader_deformation_margin() {
        const AABB_EPSILON_M: f32 = 1.0e-5;
        let margin = maximum_deformation_m(profile());
        let without = build_tree_near_mesh(0.0).unwrap();
        let with = build_tree_near_mesh(margin).unwrap();

        assert!((with.aabb.min().x - (without.aabb.min().x - margin)).abs() <= AABB_EPSILON_M);
        assert!((with.aabb.min().z - (without.aabb.min().z - margin)).abs() <= AABB_EPSILON_M);
        assert!((with.aabb.max().x - (without.aabb.max().x + margin)).abs() <= AABB_EPSILON_M);
        assert!((with.aabb.max().z - (without.aabb.max().z + margin)).abs() <= AABB_EPSILON_M);
        assert_eq!(with.aabb.min().y.to_bits(), without.aabb.min().y.to_bits());
        assert_eq!(with.aabb.max().y.to_bits(), without.aabb.max().y.to_bits());
    }

    #[test]
    fn lod_ranges_are_complementary_at_both_dither_margins() {
        let ranges = VegetationLodRanges::from_profile(profile());

        assert_eq!(ranges.near.end_margin, 47.0..53.0);
        assert_eq!(ranges.medium.end_margin, 113.0..125.0);
        assert_eq!(ranges.near.end_margin, ranges.medium.start_margin);
        assert_eq!(ranges.medium.end_margin, ranges.hlod.start_margin);
        assert_eq!(ranges.individual.end_margin, ranges.hlod.start_margin);
        assert_eq!(ranges.grass.end_margin, ranges.medium.start_margin);
        assert_eq!(ranges.hlod.end_margin, 420.0..420.0);
        assert!(!ranges.near.is_abrupt());
        assert!(!ranges.medium.is_abrupt());
        assert!(!ranges.hlod.is_abrupt());
        assert!(!ranges.hlod.use_aabb);

        let cpu_range = ranges.conservative_hlod_batch_range(45.0);
        assert_eq!(cpu_range.start_margin, 68.0..113.0);
        assert_eq!(cpu_range.end_margin, 465.0..465.0);
        assert!(!cpu_range.use_aabb);
        for anchor_distance_m in [113.0, 119.0, 124.999] {
            for signed_radius_m in [-45.0, 45.0] {
                let chunk_distance_m = anchor_distance_m + signed_radius_m;
                if chunk_distance_m >= 0.0 {
                    assert!(cpu_range.is_visible_at_all(chunk_distance_m));
                }
            }
        }
        assert!(cpu_range.is_visible_at_all(419.999 + 45.0));
        assert!(!cpu_range.is_visible_at_all(420.0 + 45.0));
    }

    #[test]
    fn medium_and_hlod_dither_masks_are_exact_complements() {
        for quantized_fade in 0..=16 {
            let medium_level = quantized_fade;
            let hlod_level = quantized_fade - 16;
            for threshold in 0..16 {
                let medium_visible = medium_level < 16 && medium_level + threshold < 16;
                let hlod_visible =
                    hlod_level == 0 || (hlod_level > -16 && 1 + hlod_level + threshold > 0);
                assert_ne!(medium_visible, hlod_visible);
            }
        }
    }

    #[test]
    fn near_crossfade_keeps_short_authored_intervals_intact() {
        assert_eq!(centered_lod_crossfade(42.0, 46.0, 6.0), 42.0..46.0);
    }

    #[test]
    fn interaction_uniforms_are_finite_and_bounded_by_the_profile() {
        let profile = profile();
        let interaction = VegetationInteractionField::bounded(
            Vec3::new(2.0, 1.0, -3.0),
            Vec2::new(3.0, 4.0),
            99.0,
            99.0,
            profile,
        );
        let extension =
            VisualValleyVegetationExtension::from_profile_and_interaction(profile, interaction);

        assert!((interaction.radius_m - profile.interaction_radius_m).abs() <= f32::EPSILON);
        assert!((interaction.strength - profile.interaction_strength).abs() <= f32::EPSILON);
        assert!((interaction.push_direction_xz.length() - 1.0).abs() <= f32::EPSILON);
        assert!(
            (extension.wind_parameters.x - profile.wind_strength * MAX_WIND_AMPLITUDE_M).abs()
                <= f32::EPSILON
        );
        assert!(
            (extension.interaction_direction_strength.z
                - profile.interaction_strength * MAX_INTERACTION_AMPLITUDE_M)
                .abs()
                <= f32::EPSILON
        );
        assert_eq!(
            extension
                .hlod_visibility_parameters
                .to_array()
                .map(f32::to_bits),
            [113.0, 125.0, 420.0, 0.0].map(f32::to_bits)
        );
    }

    #[test]
    fn mesh_tags_are_compact_variation_only_not_canonical_ids() {
        let placement = placement(VegetationKind::Tree, 0.0, 0.0);
        let mesh_tag = mesh_tag_for(placement.wind_phase_radians, placement.wind_response);
        assert_eq!(mesh_tag.0 >> 24, 191);
        assert_ne!(mesh_tag.0 & TAG_PHASE_MASK, 0);
    }

    #[test]
    fn workspace_adapter_reports_the_exact_density_source_hash() {
        let mut document = WorkspaceDocument::create(NewProjectCommand {
            command_id: forge_core::CommandId::deterministic("vegetation-adapter-create"),
            project_id: forge_core::ProjectId::deterministic("vegetation-adapter-project"),
            module_id: forge_core::ModuleId::deterministic("vegetation-adapter-module"),
            area_id: forge_core::AreaId::deterministic("vegetation-adapter-area"),
            project_name: "Projet".into(),
            module_name: "Module".into(),
            area_name: "Zone".into(),
            seed: 81,
            spawn: forge_core::Vec3::new(0.0, 2.0, 0.0),
        })
        .unwrap()
        .document;
        let area = &mut document.project.modules[0].areas[0];
        let zero_hash = area.vegetation_density.content_hash().unwrap();
        let zero = inspect_area_vegetation_projection(area).unwrap();
        assert_eq!(
            zero.vegetation_density_document_hash,
            Some(zero_hash.to_string())
        );
        assert_eq!(zero.placements.accepted_placements, 0);

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
        let painted_hash = area.vegetation_density.content_hash().unwrap();
        let painted = inspect_area_vegetation_projection(area).unwrap();
        assert_ne!(painted_hash, zero_hash);
        assert_eq!(
            painted.vegetation_density_document_hash,
            Some(painted_hash.to_string())
        );
        assert!(painted.placements.accepted_placements > 0);
        assert_eq!(
            inspect_vegetation_projection(&area.terrain, &area.vegetation)
                .unwrap()
                .vegetation_density_document_hash,
            None
        );
    }

    #[test]
    fn complete_grass_projection_uses_centered_meshes_and_preserves_batch_tags() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let prepared = prepare_vegetation_projection(&terrain, &vegetation).unwrap();
        let mut grass_batches = 0;
        for entity in &prepared.entities {
            let PreparedProjectionMarker::Batch(marker) = &entity.marker else {
                continue;
            };
            grass_batches += 1;
            let mesh = prepared
                .meshes
                .iter()
                .find(|mesh| mesh.key == entity.mesh_key)
                .unwrap();
            assert_eq!(mesh.value.aabb.center, bevy::math::Vec3A::ZERO);
            assert_eq!(entity.transform.rotation, Quat::IDENTITY);
            assert_eq!(entity.transform.scale, Vec3::ONE);
            assert!(entity.visibility_range.use_aabb);
            let blades = usize::try_from(marker.logical_placements).unwrap()
                * usize::from(prepared.profile.grass_blades_per_patch);
            assert_eq!(mesh.value.mesh.count_vertices(), blades * 7);
            assert_eq!(mesh.value.triangles, blades * 3);
            assert_eq!(
                entity.mesh_tag.0,
                chunk_mesh_tag(marker.chunk_x, marker.chunk_z, 0x0047_0000).0
            );
        }
        assert_eq!(grass_batches, prepared.stats.projection.grass_batches);
        assert!(grass_batches > 0);
    }

    #[test]
    fn complete_projection_reuses_shared_keys_and_never_projects_a_grass_patch_entity() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let prepared = prepare_vegetation_projection(&terrain, &vegetation).unwrap();

        let unique_mesh_keys = prepared
            .meshes
            .iter()
            .map(|mesh| mesh.key)
            .collect::<HashSet<_>>();
        assert_eq!(unique_mesh_keys.len(), prepared.meshes.len());
        for shared in [
            VegetationMeshKey::TreeNear,
            VegetationMeshKey::TreeMedium,
            VegetationMeshKey::Shrub,
            VegetationMeshKey::Fern,
        ] {
            assert_eq!(
                prepared
                    .meshes
                    .iter()
                    .filter(|mesh| mesh.key == shared)
                    .count(),
                1
            );
        }

        let mut placement_entities = 0_u64;
        let mut near_tree_entities = 0_u64;
        let mut medium_tree_entities = 0_u64;
        for entity in &prepared.entities {
            match &entity.marker {
                PreparedProjectionMarker::Placement(marker) => {
                    placement_entities += 1;
                    assert_ne!(marker.kind, VegetationKind::Grass);
                    if marker.kind == VegetationKind::Tree {
                        match marker.lod {
                            VegetationProjectionLod::Near => {
                                near_tree_entities += 1;
                                assert_eq!(entity.mesh_key, VegetationMeshKey::TreeNear);
                                assert!(entity.casts_shadow);
                            }
                            VegetationProjectionLod::Medium => {
                                medium_tree_entities += 1;
                                assert_eq!(entity.mesh_key, VegetationMeshKey::TreeMedium);
                                assert!(!entity.casts_shadow);
                            }
                            VegetationProjectionLod::Hlod | VegetationProjectionLod::GrassBatch => {
                                panic!("a tree placement cannot carry a chunk-batch LOD")
                            }
                        }
                    } else {
                        assert!(!entity.casts_shadow);
                    }
                }
                PreparedProjectionMarker::Batch(marker) => {
                    assert!(!entity.casts_shadow);
                    assert!(marker.logical_placements > 0);
                }
                PreparedProjectionMarker::HlodBatch(marker, envelope) => {
                    assert!(!entity.casts_shadow);
                    assert_eq!(marker.lod, VegetationProjectionLod::Hlod);
                    assert!(marker.logical_placements > 0);
                    assert!(envelope.maximum_anchor_radius_m.is_finite());
                    assert!(envelope.maximum_anchor_radius_m > 0.0);
                    assert!(
                        entity.visibility_range
                            == VegetationLodRanges::from_profile(prepared.profile)
                                .conservative_hlod_batch_range(envelope.maximum_anchor_radius_m,)
                    );
                }
            }
        }
        assert_eq!(
            placement_entities,
            prepared.stats.projection.placement_entities
        );
        assert_eq!(
            u64::try_from(prepared.entities.len()).unwrap(),
            prepared.stats.projection.total_entities
        );
        assert_eq!(near_tree_entities, medium_tree_entities);
        assert_eq!(
            near_tree_entities,
            u64::from(prepared.stats.placements.accepted_by_kind.trees)
        );
        assert_eq!(
            prepared.stats.projection.shadow_caster_entities,
            near_tree_entities
        );
        assert_eq!(
            prepared.stats.projection.shadow_caster_entities
                + prepared.stats.projection.shadow_opt_out_entities,
            prepared.stats.projection.total_entities
        );
        assert!(prepared.stats.projection.grass_batches <= 64);
        assert_eq!(prepared.stats.projection.hlod_batches, 64);
        assert_eq!(
            prepared.stats.projection.grass_blades,
            u64::from(prepared.stats.placements.accepted_by_kind.grass_patches)
                * u64::from(vegetation.presentation.grass_blades_per_patch)
        );
    }

    #[test]
    fn vegetation_material_is_forward_only_to_avoid_depth_only_silhouettes() {
        assert!(!VisualValleyVegetationExtension::enable_prepass());
    }

    #[test]
    fn shader_uses_material_time_mesh_tag_dither_and_forward_pbr_without_external_assets() {
        let shader = include_str!("shaders/visual_valley_vegetation_deform.wgsl");
        assert!(!shader.contains("mesh_view_bindings::globals"));
        assert!(shader.contains("vegetation.wind_parameters.z"));
        assert!(shader.contains("vegetation.wind_parameters.w"));
        assert!(shader.contains("mesh_functions::get_tag"));
        assert!(shader.contains("VISIBILITY_RANGE_DITHER"));
        assert!(shader.contains("get_visibility_range_dither_level"));
        assert!(shader.contains("#ifdef VERTEX_UVS_B"));
        assert!(shader.contains("vertex.uv_b.x, vertex.uv.x, vertex.uv_b.y"));
        assert!(shader.contains("view.lod_view_world_position.xyz - world_anchor"));
        assert!(shader.contains("return quantized_fade - 16;"));
        assert!(shader.contains("hlod_visibility_parameters: vec4<f32>"));
        assert!(shader.contains("let hlod_hemispheric_normal = normalize"));
        assert!(shader.contains("pbr_input.world_normal = hlod_lighting_normal"));
        assert!(
            shader.contains("visibility_range_dither(in.position, in.visibility_range_dither)")
        );
        assert!(shader.contains("pbr_functions::visibility_range_dither"));
        let dither = shader
            .find("visibility_range_dither(in.position, in.visibility_range_dither)")
            .unwrap();
        let pbr_input = shader
            .find("var pbr_input = pbr_input_from_standard_material")
            .unwrap();
        let alpha_discard = shader
            .find("pbr_input.material.base_color = alpha_discard")
            .unwrap();
        assert!(dither < pbr_input);
        assert!(pbr_input < alpha_discard);
        assert!(shader.contains("bounded_deformation"));
        assert!(shader.contains("maximum_displacement"));
        assert!(shader.contains("pbr_input_from_standard_material"));
        assert!(shader.contains("apply_pbr_lighting"));
        assert!(shader.contains("COLOR.rgb carries palette weights"));
        assert!(
            !shader
                .to_ascii_lowercase()
                .contains(&["ca", "dle"].concat())
        );
        assert!(!shader.contains("texture_"));
    }

    #[test]
    fn projection_budgets_fail_closed_before_unbounded_mesh_allocation() {
        assert!(
            ensure_budget(
                "grass_blades",
                MAX_VEGETATION_GRASS_BLADES,
                MAX_VEGETATION_GRASS_BLADES
            )
            .is_ok()
        );
        assert!(matches!(
            ensure_budget(
                "grass_blades",
                MAX_VEGETATION_GRASS_BLADES + 1,
                MAX_VEGETATION_GRASS_BLADES,
            ),
            Err(VegetationAdapterError::BudgetExceeded {
                field: "grass_blades",
                actual,
                maximum,
            }) if actual == MAX_VEGETATION_GRASS_BLADES + 1
                && maximum == MAX_VEGETATION_GRASS_BLADES
        ));
    }

    #[test]
    fn policy_uses_explicit_aabb_and_shadow_opt_out_without_disabling_batching() {
        let (no_auto_aabb, no_shadow) = projection_policy_markers();
        assert_eq!(std::mem::size_of_val(&no_auto_aabb), 0);
        assert_eq!(std::mem::size_of_val(&no_shadow), 0);
        let source = include_str!("vegetation.rs");
        assert!(!source.contains(&["NoAutomatic", "Batching"].concat()));
        assert!(!source.contains(&["NoCpu", "Culling"].concat()));
    }

    #[test]
    fn visibility_buckets_separate_range_and_cpu_culling_without_draw_call_claims() {
        let mut bucket = VegetationVisibilityBucket::default();
        bucket.observe(true, true);
        bucket.observe(true, false);
        bucket.observe(false, false);

        assert_eq!(bucket.total, 3);
        assert_eq!(bucket.cpu_visible_for_camera, 1);
        assert_eq!(bucket.range_culled, 1);
        assert_eq!(bucket.cpu_culled_after_range, 1);
        assert_eq!(
            bucket.cpu_visible_for_camera + bucket.range_culled + bucket.cpu_culled_after_range,
            bucket.total
        );
        let serialized = serde_json::to_string(&bucket).unwrap();
        assert!(!serialized.contains("draw_call"));
    }

    #[test]
    fn preprocessing_resolution_preserves_renderer_fallbacks() {
        assert_eq!(
            resolve_camera_preprocessing(VegetationGpuPreprocessingMode::Culling, false),
            VegetationGpuPreprocessingMode::Culling
        );
        assert_eq!(
            resolve_camera_preprocessing(VegetationGpuPreprocessingMode::Culling, true),
            VegetationGpuPreprocessingMode::PreprocessingOnly
        );
        assert_eq!(
            resolve_camera_preprocessing(VegetationGpuPreprocessingMode::PreprocessingOnly, false),
            VegetationGpuPreprocessingMode::PreprocessingOnly
        );
        assert_eq!(
            resolve_camera_preprocessing(VegetationGpuPreprocessingMode::None, false),
            VegetationGpuPreprocessingMode::None
        );
    }

    #[cfg(feature = "physics-rapier")]
    #[test]
    fn player_interaction_uses_the_observed_post_physics_pose_and_bounded_profile() {
        let profile = profile();
        let snapshot = PlayerRuntimeSnapshot {
            player_position: Vec3::new(3.0, 2.0, -4.0),
            horizontal_velocity: Vec3::new(3.0, 0.0, 4.0),
            camera_yaw_radians: 0.7,
            ..default()
        };
        let interaction = player_vegetation_interaction(&snapshot, profile);

        assert_eq!(interaction.center_m, snapshot.player_position);
        assert_eq!(interaction.push_direction_xz, Vec2::new(0.6, 0.8));
        assert!((interaction.radius_m - profile.interaction_radius_m).abs() <= f32::EPSILON);
        assert!((interaction.strength - profile.interaction_strength).abs() <= f32::EPSILON);
    }
}
