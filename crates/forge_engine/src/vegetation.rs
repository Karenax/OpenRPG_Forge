#[cfg(feature = "physics-rapier")]
use crate::{PlayerControllerSet, PlayerRuntimeSnapshot};
use bevy::{
    asset::RenderAssetUsages,
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
        storage::ShaderBuffer,
        view::NoIndirectDrawing,
    },
    shader::ShaderRef,
};
use forge_project::{
    MAX_VEGETATION_EXPANDED_GRASS_BLADES, MAX_VEGETATION_PLACEMENT_ATTEMPTS, TerrainDocument,
    VEGETATION_CHUNK_SIZE_M, VegetationDocument, VegetationKind, VegetationPresentationProfile,
};
use forge_world::{
    TERRAIN_HALF_EXTENT_M, TerrainGenerationPlan, VegetationGenerationError,
    VegetationGenerationPlan, VegetationInstance, VegetationInstanceId, VegetationKindCounts,
};
use serde::Serialize;
#[cfg(feature = "physics-rapier")]
mod collision;
#[cfg(feature = "physics-rapier")]
mod far_batches;
#[cfg(feature = "physics-rapier")]
pub use far_batches::GovernedFarBatchPreview;
#[cfg(feature = "physics-rapier")]
mod prototype_projection;
mod quality;
mod telemetry;
#[cfg(feature = "physics-rapier")]
pub(crate) use prototype_projection::quality_ready as governed_quality_ready;
#[cfg(feature = "physics-rapier")]
pub use prototype_projection::{
    VegetationPrototypeForestPreview, governed_forest_ready, stop_governed_forest,
};
pub use quality::{
    VegetationQualityObservation, VegetationQualityRequest, update_vegetation_sources,
    update_vegetation_sources_with_buildings, update_vegetation_sources_with_paths,
    update_vegetation_sources_with_promotions, update_vegetation_sources_with_waterways,
};
use std::{any::TypeId, collections::HashMap, error::Error, fmt};
pub use telemetry::VegetationLiveMeshStats;

const VEGETATION_SHADER_PATH: &str =
    "embedded://forge_engine/shaders/visual_valley_vegetation_deform.wgsl";
const MAX_WIND_AMPLITUDE_M: f32 = 0.55;
const MAX_INTERACTION_AMPLITUDE_M: f32 = 0.45;
const GRASS_PATCH_RADIUS_M: f32 = 0.8;
const GOLDEN_ANGLE_RADIANS: f32 = 2.399_963_1;
const TAG_PHASE_MASK: u32 = 0x00ff_ffff;
const TAG_PHASE_SCALE: f32 = 16_777_215.0;

/// Defensive Gate 1 budgets, applied before expensive generation or mesh allocation.
pub const MAX_VEGETATION_CANDIDATE_ATTEMPTS: u64 = MAX_VEGETATION_PLACEMENT_ATTEMPTS;
pub const MAX_VEGETATION_PROJECTION_ENTITIES: u64 = 100_000;
pub const MAX_VEGETATION_GRASS_BLADES: u64 = MAX_VEGETATION_EXPANDED_GRASS_BLADES;

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
    pub layer_id: forge_project::VegetationLayerId,
    pub kind: VegetationKind,
    pub lod: VegetationProjectionLod,
}

/// Render-only marker, also available to common native/Web telemetry without physics.
#[derive(Component)]
pub struct GovernedTreePrimitive {
    pub placement_id: VegetationInstanceId,
    pub lod: u8,
}

/// Read-only view of the exact generated projection used by the current quality tier.
/// This shares the generation result, rather than regenerating poses for a second renderer.
#[derive(Clone, Resource)]
pub struct VegetationScatterProjection {
    generated: std::sync::Arc<forge_world::GeneratedVegetation>,
}

impl VegetationScatterProjection {
    pub fn trees(&self) -> impl Iterator<Item = &VegetationInstance> {
        self.generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter(|instance| instance.kind == VegetationKind::Tree)
    }
}

/// Derived marker for a chunk mesh. One batch can cover many logical placements.
#[derive(Clone, Copy, Component, Debug, Eq, PartialEq)]
pub struct VegetationChunkBatchMarker {
    pub chunk_x: u16,
    pub chunk_z: u16,
    pub lod: VegetationProjectionLod,
    pub logical_placements: u32,
}

/// Separate from quality-owned procedural batches: never reconciled as a recipe binding.
#[derive(Component)]
pub struct GovernedFarBatch;

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
#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct VegetationPrototypePreviewStats {
    pub trees: u32,
    pub primitive_entities: usize,
    pub procedural_geometry_counts_valid: bool,
}

/// Machine-readable vegetation adapter evidence. No field is a draw-call estimate.
#[derive(Clone, Debug, Default, Eq, PartialEq, Resource, Serialize)]
pub struct VegetationProjectionStats {
    /// Live meshes for the single active camera; None when visibility is unavailable.
    pub live_meshes: Option<VegetationLiveMeshStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub prototype_preview_error: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub prototype_preview: Option<VegetationPrototypePreviewStats>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub quality: Option<VegetationQualityObservation>,
    pub terrain_document_hash: String,
    /// Source document hash without paths; versioned composite projection identity
    /// when accepted authoring paths contribute derived clearances.
    pub vegetation_document_hash: String,
    pub placements: VegetationPlacementStats,
    pub projection: VegetationProjectionCounts,
    pub triangles: VegetationTriangleStats,
    pub visibility: VegetationVisibilityStats,
    pub render_path: VegetationRenderPathStats,
}

impl VegetationProjectionStats {
    pub(crate) fn overlay_details(&self) -> String {
        if let Some(error) = &self.prototype_preview_error {
            return format!(
                "APERÇU forêt en échec : {error}\nConsulter le diagnostic de reprise ; aperçu non qualifié"
            );
        }
        if let Some(preview) = &self.prototype_preview {
            let observed = self.live_meshes.as_ref().map_or_else(
                || "visibilité indisponible sans caméra unique".to_owned(),
                |meshes| format!("végétation réelle : {} meshes · {} triangles visibles CPU · {} meshes non comptables",
                    meshes.mesh_entities, meshes.cpu_visible_triangles, meshes.uncountable_mesh_assets),
            );
            return format!(
                "APERÇU forêt : {} arbres · {} primitives LOD\n{}\nBudgets procéduraux invalides pour cet aperçu ; pas des draw calls GPU",
                preview.trees, preview.primitive_entities, observed
            );
        }
        let visibility = match self.visibility.status {
            VegetationVisibilityObservationStatus::ObservedSingleActiveCamera => format!(
                "caméra : {} visibles · {} hors plage · {} exclus CPU après plage",
                self.visibility.total.cpu_visible_for_camera,
                self.visibility.total.range_culled,
                self.visibility.total.cpu_culled_after_range,
            ),
            status => format!("caméra : métriques indisponibles ({status:?})"),
        };
        format!(
            "{} placements · {} entités projection · {} lots herbe · {} lots HLOD\n{}",
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
    /// Shared governed-mesh policy for projection, shadow uniforms and capture traces.
    pub(crate) fn from_governed_profile(profile: VegetationPresentationProfile) -> Self {
        let mut ranges = Self::from_profile(profile);
        let transition = profile.near_lod_distance_m
            ..(profile.near_lod_distance_m + 8.0).min(profile.medium_lod_distance_m);
        ranges.near.end_margin = transition.clone();
        ranges.medium.start_margin = transition;
        ranges
    }

    #[must_use]
    pub fn from_profile(profile: VegetationPresentationProfile) -> Self {
        let near_to_medium = profile
            .near_lod_distance_m
            .max(profile.medium_lod_distance_m - 8.0)
            ..profile.medium_lod_distance_m;
        let medium_to_hlod = profile
            .medium_lod_distance_m
            .max(profile.hlod_distance_m - 8.0)
            ..profile.hlod_distance_m;
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
                // A chunk must not spend most of its near range stippled. Keep the same
                // cull distance, with at most eight metres of terminal fade for grass.
                end_margin: profile
                    .near_lod_distance_m
                    .max(profile.medium_lod_distance_m - 8.0)
                    ..profile.medium_lod_distance_m,
                use_aabb: true,
            },
            hlod: VisibilityRange {
                start_margin: medium_to_hlod,
                end_margin: profile.cull_distance_m..profile.cull_distance_m,
                use_aabb: false,
            },
        }
    }
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
    /// X is scalar wetness; Y/Z are full-wet microfacet-alpha/albedo multipliers.
    #[uniform(100)]
    pub wetness_parameters: Vec4,
    /// HLOD per-placement fade-in start/end and true far cutoff. CPU batch ranges are conservative.
    #[uniform(100)]
    pub hlod_distances: Vec4,
    /// X enables governed PBR; Y is inverse normalized prototype height. Zero keeps procedural mode.
    #[uniform(100)]
    pub prototype_parameters: Vec4,
    /// Governed medium-LOD shadow fade start/end; matches the CPU caster cutoff.
    #[uniform(100)]
    pub shadow_fade_distances: Vec4,
    /// X enables the shared temporal LOD table; Y selects static authored kit materials.
    #[uniform(100)]
    pub temporal_parameters: Vec4,
    #[storage(101, read_only)]
    pub temporal_lod_buffer: Handle<ShaderBuffer>,
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
            hlod_distances: Vec4::ZERO,
            prototype_parameters: Vec4::ZERO,
            shadow_fade_distances: Vec4::ZERO,
            temporal_parameters: Vec4::ZERO,
            temporal_lod_buffer: crate::temporal_lod::render::BUFFER,
        }
    }
}

impl VisualValleyVegetationExtension {
    /// Use only with root-normalized meshes, preserving their authored UVs and vertex colors.
    pub fn with_governed_prototype_height(mut self, height_m: f32) -> Result<Self, &'static str> {
        if !height_m.is_finite() || !(0.01..=1000.0).contains(&height_m) {
            return Err("governed vegetation height must be finite and within 0.01..1000 metres");
        }
        self.prototype_parameters = Vec4::new(1.0, height_m.recip(), 0.0, 0.0);
        Ok(self)
    }

    #[must_use]
    pub fn from_profile_and_interaction(
        profile: VegetationPresentationProfile,
        interaction: VegetationInteractionField,
    ) -> Self {
        let ranges = VegetationLodRanges::from_profile(profile);
        let shadow_range = VegetationLodRanges::from_governed_profile(profile)
            .medium
            .end_margin;
        Self {
            shadow_fade_distances: Vec4::new(shadow_range.start, shadow_range.end, 0.0, 0.0),
            hlod_distances: Vec4::new(
                ranges.hlod.start_margin.start,
                ranges.hlod.start_margin.end,
                profile.cull_distance_m,
                0.0,
            ),
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
            ..Self::default()
        }
    }
}

impl MaterialExtension for VisualValleyVegetationExtension {
    fn specialize(
        _pipeline: &bevy::pbr::MaterialExtensionPipeline,
        descriptor: &mut bevy::render::render_resource::RenderPipelineDescriptor,
        layout: &bevy::mesh::MeshVertexBufferLayoutRef,
        key: bevy::pbr::MaterialExtensionKey<Self>,
    ) -> Result<(), bevy::material::specialize::SpecializedMeshPipelineError> {
        use crate::vegetation_far_geometry::{ROOT_SCALE, WIND_DATA};
        use bevy::pbr::MeshPipelineKey;
        // Directional shadow passes have no normal/motion/deferred outputs.
        // Compute the fade from the LOD view origin, without depending on a
        // visibility-range storage binding absent from Bevy's shadow layout.
        if key.mesh_key.contains(MeshPipelineKey::DEPTH_PREPASS)
            && !key.mesh_key.intersects(
                MeshPipelineKey::NORMAL_PREPASS
                    | MeshPipelineKey::MOTION_VECTOR_PREPASS
                    | MeshPipelineKey::DEFERRED_PREPASS,
            )
        {
            descriptor
                .vertex
                .shader_defs
                .push("VERTEX_OUTPUT_INSTANCE_INDEX".into());
            descriptor
                .vertex
                .shader_defs
                .push("FORGE_SHADOW_FADE".into());
            descriptor.fragment = Some(bevy::render::render_resource::FragmentState {
                shader: descriptor.vertex.shader.clone(),
                shader_defs: descriptor.vertex.shader_defs.clone(),
                entry_point: Some("shadow_fragment".into()),
                targets: vec![],
            });
        }
        if layout.0.contains(ROOT_SCALE) || layout.0.contains(WIND_DATA) {
            let extra = layout.0.get_layout(&[
                ROOT_SCALE.at_shader_location(10),
                WIND_DATA.at_shader_location(11),
            ])?;
            // StandardMaterial specialization runs first and installs the mesh buffer.
            let buffer = descriptor.vertex.buffers.first_mut().expect(
                "StandardMaterial installs its vertex buffer before extension specialization",
            );
            buffer.attributes.extend(extra.attributes);
            descriptor.vertex.shader_defs.push("FORGE_FAR_BATCH".into());
        }
        Ok(())
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

pub(crate) fn install_temporal_material(app: &mut App) {
    bevy::asset::embedded_asset!(app, "shaders/visual_valley_vegetation_deform.wgsl");
    app.add_plugins(MaterialPlugin::<VisualValleyVegetationMaterial>::default())
        .add_systems(Update, configure_vegetation_prepass);
}

// Bevy 0.19's opaque depth-only prepass has no fragment stage for visibility dither.
// A normal prepass invokes the standard fragment rejection in both depth and color,
// avoiding depth-only silhouettes left behind when the forward pass discards pixels.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameter wrappers"
)]
pub(crate) fn configure_vegetation_prepass(
    mut commands: Commands,
    cameras: Query<
        Entity,
        (
            With<Camera3d>,
            Without<bevy::core_pipeline::prepass::NormalPrepass>,
        ),
    >,
) {
    for camera in &cameras {
        commands
            .entity(camera)
            .insert(bevy::core_pipeline::prepass::NormalPrepass);
    }
}

/// Compact, renderer-facing placement used by the procedural mesh builders.
/// Positions are relative to the owning chunk for grass and HLOD batches.
#[derive(Clone, Copy, Debug, PartialEq)]
pub(crate) struct ProceduralVegetationPlacement {
    /// Stable variation derived from the canonical placement ID, never its batch index.
    pub variation_seed: u64,
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
    DegenerateTriangle,
    VertexCountOverflow,
    GrassBladeCountIsZero,
    GrassBladeSurfaceProjectionFailed,
}

impl fmt::Display for VegetationMeshError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Empty => formatter.write_str("vegetation mesh contains no vertices"),
            Self::NonFiniteVertex => {
                formatter.write_str("vegetation mesh contains a non-finite vertex")
            }
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
        let mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, self.positions)
        .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, self.normals)
        .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, self.uvs)
        .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, self.colors)
        .with_inserted_indices(Indices::U32(self.indices));
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
    build_tree_mesh(8, 8, 7, deformation_margin_m)
}

pub(crate) fn build_tree_medium_mesh(
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    build_tree_mesh(5, 5, 5, deformation_margin_m)
}

fn build_tree_mesh(
    trunk_segments: u16,
    tiers: u16,
    branches: u16,
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    append_cylinder(
        &mut mesh,
        Vec3::ZERO,
        0.12,
        5.3,
        trunk_segments,
        bark_color(0.0),
        bark_color(0.22),
    )?;
    for tier in 0..tiers {
        let fraction = f32::from(tier) / f32::from(tiers - 1);
        let height = 1.85 + fraction * 3.1;
        let envelope = 1.35 * (1.0 - fraction * 0.8);
        let thickness = 1.0 - fraction * 0.4;
        for branch in 0..branches {
            let noise = unit_noise(u64::from(tier), branch, 71);
            let angle = f32::from(branch) * std::f32::consts::TAU / f32::from(branches)
                + f32::from(tier) * GOLDEN_ANGLE_RADIANS
                + (noise - 0.5) * 0.3;
            let direction = Vec3::new(angle.cos(), 0.0, angle.sin());
            let side = Vec3::new(-direction.z, 0.0, direction.x);
            let length = envelope * (0.78 + 0.22 * noise);
            let base = Vec3::Y * (height + (noise - 0.5) * 0.16);
            // Unequal, rotated sprays leave real gaps rather than a continuous cone surface.
            let points = [
                base + direction * 0.08 + Vec3::Y * (0.65 * thickness),
                base + direction * length + Vec3::Y * 0.02,
                base + direction * (length * 0.60) - side * (length * 0.40)
                    + Vec3::Y * (0.12 * thickness),
                base + direction * (length * 0.56)
                    + side * (length * 0.43)
                    + Vec3::Y * (0.50 * thickness),
            ];
            append_foliage_spray(&mut mesh, points, usize::from(tier + branch))?;
        }
    }
    append_cone(
        &mut mesh,
        Vec3::new(0.0, 4.9, 0.0),
        0.25,
        5.65,
        trunk_segments + 1,
        leaf_color(0, 0.7),
        leaf_color(1, 1.0),
    )?;
    mesh.finish(deformation_margin_m)
}

fn append_foliage_spray(
    mesh: &mut MeshAccumulator,
    points: [Vec3; 4],
    variation: usize,
) -> Result<(), VegetationMeshError> {
    let center = (points[0] + points[1] + points[2] + points[3]) * 0.25;
    let colors = [
        leaf_color(variation, 0.35),
        leaf_color(variation + 1, 1.0),
        leaf_color(variation, 0.7),
        leaf_color(variation + 1, 0.8),
    ];
    for mut indices in [[0, 1, 2], [0, 3, 1], [0, 2, 3], [1, 3, 2]] {
        let normal = triangle_normal(points[indices[0]], points[indices[1]], points[indices[2]])?;
        if normal.dot(points[indices[0]] - center) < 0.0 {
            indices.swap(1, 2);
        }
        mesh.push_triangle(
            indices.map(|index| points[index]),
            indices.map(|index| colors[index]),
        )?;
    }
    Ok(())
}

pub(crate) fn build_shrub_mesh(
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    // A porous, rounded crown of discrete leaves instead of three solid cone silhouettes.
    // This shared render mesh does not change canonical placement poses or scatter density.
    for ring in 0..4_u16 {
        let height = 0.20 + f32::from(ring) * 0.29;
        let radius = [0.40, 0.62, 0.51, 0.24][usize::from(ring)];
        for leaf in 0..12_u16 {
            let noise = unit_noise(u64::from(ring), leaf, 193);
            let angle = f32::from(leaf) * std::f32::consts::TAU / 12.0
                + f32::from(ring) * GOLDEN_ANGLE_RADIANS;
            let outward = Vec3::new(angle.cos(), 0.0, angle.sin());
            let side = Vec3::new(-outward.z, 0.0, outward.x);
            let center = outward * (radius * (0.8 + noise * 0.2))
                + Vec3::Y * (height + (noise - 0.5) * 0.12);
            let axis = (outward * 0.75 + Vec3::Y * (0.25 + noise * 0.5)).normalize();
            let length = 0.20 + noise * 0.09;
            let width = length * 0.60;
            // Keep the lowest crown fixed; flexibility increases toward the top.
            let weight = f32::from(ring) / 3.0;
            mesh.push_quad(
                [
                    center - axis * length,
                    center - side * width,
                    center + axis * length,
                    center + side * width,
                ],
                [leaf_color(usize::from(leaf), weight); 4],
            )?;
        }
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
        let right = Vec3::new(-outward.z, 0.0, outward.x);
        let base = Vec3::new(0.0, 0.04, 0.0);
        let shoulder = outward * 0.55 + Vec3::Y * 0.44;
        let tip = outward * 1.05 + Vec3::Y * 0.16;
        // Two narrow rachis segments support separate paired leaflets, not one solid flag.
        for (start, end, weight) in [(base, shoulder, 0.0), (shoulder, tip, 0.5)] {
            mesh.push_quad(
                [
                    start - right * 0.012,
                    start + right * 0.012,
                    end + right * 0.007,
                    end - right * 0.007,
                ],
                [
                    leaf_color(usize::from(leaf), weight),
                    leaf_color(usize::from(leaf), weight),
                    leaf_color(usize::from(leaf), weight + 0.5),
                    leaf_color(usize::from(leaf), weight + 0.5),
                ],
            )?;
        }
        for pair in 0..3_u16 {
            let t = 0.22 + f32::from(pair) * 0.25;
            let center = if t < 0.5 {
                base.lerp(shoulder, t * 2.0)
            } else {
                shoulder.lerp(tip, (t - 0.5) * 2.0)
            };
            for sign in [-1.0, 1.0] {
                let length =
                    (0.26 - t * 0.12) * (0.85 + unit_noise(u64::from(leaf), pair, 211) * 0.15);
                let direction = right * sign + outward * 0.40;
                let end = center + direction * length - Vec3::Y * 0.035;
                let middle = center.lerp(end, 0.5);
                mesh.push_quad(
                    [
                        center,
                        middle - outward * 0.045,
                        end,
                        middle + outward * 0.045,
                    ],
                    [leaf_color(usize::from(leaf + pair), t); 4],
                )?;
            }
        }
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
    for patch in patches {
        if patch.kind != VegetationKind::Grass {
            continue;
        }
        for blade in 0..grass_blades_per_patch {
            // Uneven lobes join into a tuft instead of drawing a regular circular disc.
            let radial = unit_noise(patch.variation_seed, blade, 4).sqrt();
            let angle = patch.wind_phase_radians
                + f32::from(blade) * GOLDEN_ANGLE_RADIANS
                + unit_noise(patch.variation_seed, blade, 0) * 0.45;
            let outline = 0.72 + 0.28 * (angle * 3.0 + patch.yaw_radians).sin();
            let radius = radial * outline * GRASS_PATCH_RADIUS_M * patch.scale;
            let local_x = patch.position_m.x + angle.cos() * radius;
            let local_z = patch.position_m.z + angle.sin() * radius;
            let world_x =
                (chunk_center.x + local_x).clamp(-TERRAIN_HALF_EXTENT_M, TERRAIN_HALF_EXTENT_M);
            let world_z =
                (chunk_center.z + local_z).clamp(-TERRAIN_HALF_EXTENT_M, TERRAIN_HALF_EXTENT_M);
            let height_m = terrain
                .surface_height_at(world_x, world_z)
                .map_err(|_| VegetationMeshError::GrassBladeSurfaceProjectionFailed)?;
            let base = Vec3::new(world_x - chunk_center.x, height_m, world_z - chunk_center.z);
            let blade_yaw = angle + unit_noise(patch.variation_seed, blade, 1) * 1.2;
            // Slender blades in a tighter tuft, without extra geometry or authored placements.
            let width = (0.025 + unit_noise(patch.variation_seed, blade, 2) * 0.03) * patch.scale;
            let height = (0.22 + unit_noise(patch.variation_seed, blade, 3) * 0.43) * patch.scale;
            let right = Vec3::new(blade_yaw.cos(), 0.0, -blade_yaw.sin()) * (width * 0.5);
            let lean = Vec3::new(blade_yaw.sin(), 0.0, blade_yaw.cos())
                * height
                * (0.25 + unit_noise(patch.variation_seed, blade, 5) * 0.45);
            let middle = base + Vec3::Y * (height * 0.58) + lean * 0.25;
            let top = base + Vec3::Y * height + lean;
            let bottom_color = grass_color(blade, 0.0);
            let middle_color = grass_color(blade, patch.wind_response * 0.4);
            let top_color = grass_color(blade, patch.wind_response);
            mesh.push_quad(
                [
                    base - right,
                    base + right,
                    middle + right * 0.65,
                    middle - right * 0.65,
                ],
                [bottom_color, bottom_color, middle_color, middle_color],
            )?;
            mesh.push_triangle(
                [middle - right * 0.65, middle + right * 0.65, top],
                [middle_color, middle_color, top_color],
            )?;
        }
    }
    mesh.finish(deformation_margin_m)
}

pub(crate) fn build_hlod_chunk_mesh(
    placements: &[ProceduralVegetationPlacement],
    deformation_margin_m: f32,
) -> Result<DerivedVegetationMesh, VegetationMeshError> {
    let mut mesh = MeshAccumulator::default();
    let mut root_heights = Vec::new();
    for placement in placements {
        let first_vertex = mesh.positions.len();
        if placement.kind == VegetationKind::Tree {
            append_crossed_tree_silhouette(&mut mesh, placement)?;
        } else {
            let (width, height, bottom, top) = match placement.kind {
                VegetationKind::Shrub => (1.15, 1.35, leaf_color(0, 0.0), leaf_color(1, 0.72)),
                // Match the near fern's spreading fronds rather than a tall diamond.
                VegetationKind::Fern => (2.1, 0.44, leaf_color(1, 0.0), leaf_color(0, 0.9)),
                VegetationKind::Tree | VegetationKind::Grass => continue,
            };
            append_low_growth_silhouette(
                &mut mesh,
                placement.position_m,
                placement.yaw_radians,
                width * placement.scale,
                height * placement.scale,
                bottom,
                with_wind_weight(top, placement.wind_response),
            )?;
        }
        // This procedural material has no sampled textures. HLOD-only UV0/UV1 encode
        // the undeformed local placement root, identical for every vertex of its silhouette.
        // Individual meshes retain their normal UVs and have no UV1 attribute.
        for uv in &mut mesh.uvs[first_vertex..] {
            *uv = [placement.position_m.x, placement.position_m.z];
        }
        root_heights.resize(mesh.positions.len(), [placement.position_m.y, 0.0]);
    }
    let mut derived = mesh.finish(deformation_margin_m)?;
    derived
        .mesh
        .insert_attribute(Mesh::ATTRIBUTE_UV_1, root_heights);
    Ok(derived)
}

/// Cull the whole batch only if no placement can be in its shader-visible range.
/// The GPU fades each silhouette at its own root, not at this batch's model origin.
fn hlod_batch_visibility_range(
    placements: &[ProceduralVegetationPlacement],
    nominal: &VisibilityRange,
) -> VisibilityRange {
    let radius = placements
        .iter()
        .map(|placement| placement.position_m.length())
        .fold(0.0_f32, f32::max);
    let start = (nominal.start_margin.start - radius).max(0.0);
    VisibilityRange {
        // Keep a non-abrupt margin so Bevy enables the dither varying in both passes.
        start_margin: start..start + (nominal.start_margin.end - nominal.start_margin.start),
        end_margin: nominal.end_margin.end + radius..nominal.end_margin.end + radius,
        use_aabb: false,
    }
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
                base + radial_b,
                base + radial_a,
                base + radial_a + Vec3::Y * height,
                base + radial_b + Vec3::Y * height,
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
        // Counter-clockwise from outside: foliage normals point outward and upward.
        mesh.push_triangle([second, first, tip], [base_color, base_color, tip_color])?;
    }
    Ok(())
}

fn append_crossed_tree_silhouette(
    mesh: &mut MeshAccumulator,
    placement: &ProceduralVegetationPlacement,
) -> Result<(), VegetationMeshError> {
    for quarter_turn in [0.0, std::f32::consts::FRAC_PI_2] {
        let angle = placement.yaw_radians + quarter_turn;
        let right = Vec3::new(angle.cos(), 0.0, -angle.sin()) * placement.scale;
        let up = Vec3::Y * placement.scale;
        let base = placement.position_m;
        mesh.push_quad(
            [
                base - right * 0.22,
                base + right * 0.22,
                base + up * 2.9 + right * 0.22,
                base + up * 2.9 - right * 0.22,
            ],
            [
                bark_color(0.0),
                bark_color(0.0),
                bark_color(0.22),
                bark_color(0.22),
            ],
        )?;
        for (index, (bottom, tip, radius)) in
            [(1.75, 3.8, 1.35), (2.65, 4.8, 1.12), (3.55, 5.65, 0.86)]
                .into_iter()
                .enumerate()
        {
            let lower_color = leaf_color(index, 0.35 * placement.wind_response);
            let upper_color = leaf_color(index + 1, placement.wind_response);
            mesh.push_triangle(
                [
                    base + up * bottom - right * radius,
                    base + up * bottom + right * radius,
                    base + up * tip,
                ],
                [lower_color, lower_color, upper_color],
            )?;
        }
    }
    Ok(())
}

fn append_low_growth_silhouette(
    mesh: &mut MeshAccumulator,
    base: Vec3,
    yaw: f32,
    width: f32,
    height: f32,
    bottom_color: [f32; 4],
    top_color: [f32; 4],
) -> Result<(), VegetationMeshError> {
    for quarter_turn in [0.0, std::f32::consts::FRAC_PI_2] {
        let angle = yaw + quarter_turn;
        let right = Vec3::new(angle.cos(), 0.0, -angle.sin()) * (width * 0.5);
        // Convex six-point crown, rooted across its base rather than balanced on a tip.
        // Two crossed planes still batch all distant plants; no extra entity or texture.
        let outline = [
            (-0.45, 0.0),
            (0.45, 0.0),
            (1.0, 0.38),
            (0.50, 0.88),
            (-0.24, 1.0),
            (-1.0, 0.45),
        ];
        let normal = Vec3::new(angle.sin(), 0.0, angle.cos());
        let first = u32::try_from(mesh.positions.len())
            .map_err(|_| VegetationMeshError::VertexCountOverflow)?;
        for (horizontal, vertical) in outline {
            let color = if vertical == 0.0 {
                bottom_color
            } else {
                top_color
            };
            mesh.push_vertex(
                base + right * horizontal + Vec3::Y * (height * vertical),
                normal,
                color,
                [0.0, 0.0],
            )?;
        }
        for offset in 1..5_u32 {
            mesh.indices
                .extend_from_slice(&[first, first + offset, first + offset + 1]);
        }
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

fn with_wind_weight(mut color: [f32; 4], wind_weight: f32) -> [f32; 4] {
    color[3] = wind_weight;
    color
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
fn unit_noise(seed: u64, blade: u16, stream: u32) -> f32 {
    let mut value = seed
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
    interaction: VegetationInteractionField,
}

impl VisualValleyVegetationPlugin {
    #[must_use]
    pub fn new(terrain: TerrainDocument, vegetation: VegetationDocument) -> Self {
        Self {
            terrain,
            vegetation,
            interaction: VegetationInteractionField::disabled(),
        }
    }

    #[must_use]
    pub const fn with_interaction(mut self, interaction: VegetationInteractionField) -> Self {
        self.interaction = interaction;
        self
    }
}

impl Plugin for VisualValleyVegetationPlugin {
    fn build(&self, app: &mut App) {
        let prepared = prepare_vegetation_projection_with_quality(
            &self.terrain,
            &self.vegetation,
            app.world().get_resource::<VegetationQualityRequest>(),
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

        crate::temporal_lod::render::install(app);
        if app.world().contains_resource::<VegetationQualityRequest>() {
            let bindings = quality::VegetationQualityBindings::new(
                self.terrain.clone(),
                self.vegetation.clone(),
            );
            #[cfg(not(target_arch = "wasm32"))]
            let bindings = {
                let mut bindings = bindings;
                if app.get_sub_app(bevy::render::RenderApp).is_some() {
                    bindings.enable_background();
                }
                bindings
            };
            app.insert_resource(bindings);
            quality::install_mesh_staging(app);
            app.add_systems(Update, quality::apply_quality);
        }
        app.insert_resource(stats)
            .insert_resource(VegetationPresentation(profile))
            .insert_resource(interaction)
            .insert_resource(PreparedVegetation(Some(prepared)))
            .add_systems(Startup, spawn_vegetation_projection)
            .add_systems(
                Update,
                update_vegetation_interaction_uniform.after(quality::apply_quality),
            )
            .add_systems(
                PostUpdate,
                observe_vegetation_visibility.after(VisibilitySystems::CheckVisibility),
            );

        #[cfg(feature = "physics-rapier")]
        app.add_systems(
            Update,
            prototype_projection::project.after(quality::apply_quality),
        );
        #[cfg(feature = "physics-rapier")]
        app.add_systems(
            FixedUpdate,
            collision::update_activation.before(bevy_rapier3d::prelude::PhysicsSet::SyncBackend),
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

#[derive(Debug)]
pub enum VegetationAdapterError {
    Generation(VegetationGenerationError),
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

#[derive(Resource)]
struct PreparedVegetation(Option<PreparedVegetationProjection>);

struct PreparedVegetationProjection {
    scatter: VegetationScatterProjection,
    profile: VegetationPresentationProfile,
    meshes: Vec<PreparedMeshAsset>,
    entities: Vec<PreparedProjectionEntity>,
    stats: VegetationProjectionStats,
    #[cfg(feature = "physics-rapier")]
    tree_colliders: Vec<collision::TreeTrunkProjection>,
}

struct PreparedMeshAsset {
    key: VegetationMeshKey,
    value: DerivedVegetationMesh,
}

// A single preparation owns this transfer queue. Draining moves meshes, never clones them.
#[derive(Default)]
struct PreparedMeshStream(std::sync::Mutex<Vec<PreparedMeshAsset>>);

impl PreparedMeshStream {
    #[cfg(any(target_arch = "wasm32", test))]
    fn drain(&self) -> Vec<PreparedMeshAsset> {
        std::mem::take(
            &mut *self
                .0
                .lock()
                .expect("mesh stream lock is not held across user code"),
        )
    }

    fn append(&self, meshes: &mut Vec<PreparedMeshAsset>) {
        self.0
            .lock()
            .expect("mesh stream lock is not held across user code")
            .append(meshes);
    }
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
    prepare_vegetation_projection_with_quality(terrain, vegetation, None)
}

#[allow(
    clippy::similar_names,
    clippy::too_many_lines,
    reason = "one preparation pass validates correlated mesh, entity, LOD and evidence counts"
)]
fn prepare_vegetation_projection_with_quality(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    quality: Option<&VegetationQualityRequest>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    // The synchronous path never yields; native workers and initial loading retain
    // their existing contract. Browser quality changes poll the staged path per frame.
    bevy::tasks::block_on(prepare_vegetation_projection_staged(
        terrain, vegetation, quality, false,
    ))
}

#[allow(
    clippy::similar_names,
    clippy::too_many_lines,
    reason = "one shared geometry preparation preserves the synchronous projection contract"
)]
async fn prepare_vegetation_projection_staged(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    quality: Option<&VegetationQualityRequest>,
    staged: bool,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    let plan = VegetationGenerationPlan::from_documents(terrain, vegetation)?;
    prepare_vegetation_projection_from_plan(&plan, quality, staged, None).await
}

#[cfg(test)]
fn prepare_vegetation_projection_with_paths(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    paths: &[forge_project::zone_authoring::paths::AuthoredPath],
    quality: Option<&VegetationQualityRequest>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    prepare_vegetation_projection_with_waterways(terrain, vegetation, paths, &[], quality)
}

#[cfg(test)]
fn prepare_vegetation_projection_with_waterways(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    paths: &[forge_project::zone_authoring::paths::AuthoredPath],
    rivers: &[forge_project::zone_authoring::rivers::AuthoredRiver],
    quality: Option<&VegetationQualityRequest>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    prepare_vegetation_projection_with_buildings(terrain, vegetation, paths, rivers, &[], quality)
}

#[cfg(test)]
fn prepare_vegetation_projection_with_buildings(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    paths: &[forge_project::zone_authoring::paths::AuthoredPath],
    rivers: &[forge_project::zone_authoring::rivers::AuthoredRiver],
    buildings: &[forge_project::zone_authoring::buildings::AuthoredBuilding],
    quality: Option<&VegetationQualityRequest>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    prepare_vegetation_projection_with_promotions(
        terrain,
        vegetation,
        paths,
        rivers,
        buildings,
        &[],
        quality,
    )
}

fn prepare_vegetation_projection_with_promotions(
    terrain: &TerrainDocument,
    vegetation: &VegetationDocument,
    paths: &[forge_project::zone_authoring::paths::AuthoredPath],
    rivers: &[forge_project::zone_authoring::rivers::AuthoredRiver],
    buildings: &[forge_project::zone_authoring::buildings::AuthoredBuilding],
    promotions: &[forge_project::zone_authoring::promotion::PromotedInstance],
    quality: Option<&VegetationQualityRequest>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    let plan = VegetationGenerationPlan::from_documents_with_buildings(
        terrain, vegetation, paths, rivers, buildings,
    )?
    .with_promotions(promotions)?;
    bevy::tasks::block_on(prepare_vegetation_projection_from_plan(
        &plan, quality, false, None,
    ))
}

#[allow(
    clippy::similar_names,
    clippy::too_many_lines,
    reason = "shared geometry preparation preserves the synchronous projection contract"
)]
async fn prepare_vegetation_projection_from_plan(
    plan: &VegetationGenerationPlan,
    quality: Option<&VegetationQualityRequest>,
    staged: bool,
    mesh_stream: Option<&PreparedMeshStream>,
) -> Result<PreparedVegetationProjection, VegetationAdapterError> {
    let vegetation = plan.vegetation();
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

    let canonical = plan.generate()?;
    let generated = if let Some(request) = quality {
        std::sync::Arc::new(
            plan.generate_render_density(
                request
                    .document
                    .preset(request.tier)
                    .vegetation
                    .density_multiplier,
            )?
            .generated,
        )
    } else {
        canonical.clone()
    };
    let profile = quality.map_or(vegetation.presentation, |request| {
        request.profile(vegetation.presentation)
    });
    let mut validation_copy = vegetation.clone();
    validation_copy.presentation = profile;
    validation_copy
        .validate()
        .map_err(VegetationGenerationError::from)?;
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
            let mesh = build_grass_chunk_mesh(
                plan.terrain(),
                chunk_center,
                &grass,
                profile.grass_blades_per_patch,
                deformation_margin_m,
            )
            .map_err(|source| VegetationAdapterError::Mesh { key, source })?;
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
                transform: Transform::from_translation(chunk_center),
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
                visibility_range: hlod_batch_visibility_range(&woody, &ranges.hlod),
                marker: PreparedProjectionMarker::Batch(VegetationChunkBatchMarker {
                    chunk_x,
                    chunk_z,
                    lod: VegetationProjectionLod::Hlod,
                    logical_placements: chunk.kind_counts.total() - chunk.kind_counts.grass_patches,
                }),
                casts_shadow: false,
            });
        }

        for instance in &chunk.instances {
            append_individual_projection(instance, &ranges, &mut entities);
        }
        if staged {
            if let Some(stream) = mesh_stream {
                stream.append(&mut meshes);
            }
            // The browser owner polls a bounded batch per frame, not a microtask loop.
            // No live ECS/asset binding is modified while these chunks are prepared.
            bevy::tasks::futures_lite::future::yield_now().await;
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
        live_meshes: None,
        prototype_preview_error: None,
        prototype_preview: None,
        quality: quality.map(|request| request.observation(canonical.kind_counts.total())),
        terrain_document_hash: generated.terrain_document_hash.to_string(),
        vegetation_document_hash: generated.vegetation_document_hash.to_string(),
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
        scatter: VegetationScatterProjection { generated },
        profile,
        meshes,
        entities,
        stats,
        #[cfg(feature = "physics-rapier")]
        tree_colliders: canonical
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter_map(|instance| {
                collision::TreeTrunkProjection::from_instance(instance, vegetation)
            })
            .collect(),
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
                    layer_id: instance.layer_id.clone(),
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
                    layer_id: instance.layer_id.clone(),
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
                    layer_id: instance.layer_id.clone(),
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
        variation_seed: instance
            .id
            .as_entity_id()
            .as_uuid()
            .as_bytes()
            .iter()
            .fold(0xcbf2_9ce4_8422_2325_u64, |hash, byte| {
                (hash ^ u64::from(*byte)).wrapping_mul(0x0100_0000_01b3)
            }),
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
    bindings: Option<ResMut<quality::VegetationQualityBindings>>,
) {
    let prepared = prepared
        .0
        .take()
        .expect("vegetation projection preparation is consumed exactly once");
    let projection_stats = prepared.stats.clone();
    commands.insert_resource(prepared.scatter);
    // Physics comes from canonical placements, never from visibility or render density.
    #[cfg(feature = "physics-rapier")]
    collision::spawn_tree_trunks(&mut commands, prepared.tree_colliders);
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

    let mut bindings = bindings;
    for prepared_entity in prepared.entities {
        let binding_key = quality::VegetationQualityBindings::key(&prepared_entity.marker);
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
        }
        if !prepared_entity.casts_shadow {
            entity.insert(NotShadowCaster);
        }
        if let Some(bindings) = bindings.as_mut() {
            bindings.entities.insert(binding_key, entity.id());
        }
    }
    if let Some(bindings) = bindings.as_mut() {
        bindings.meshes = mesh_handles;
    }
    commands.insert_resource(VegetationMaterialHandle(material));
    info!(
        target: "forge::vegetation",
        schema = "forge.runtime-event",
        schema_version = 1_u64,
        event_kind = "visual_valley_vegetation_materialized",
        terrain_document_hash = %projection_stats.terrain_document_hash,
        vegetation_document_hash = %projection_stats.vegetation_document_hash,
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
    let procedural_id = material.as_ref().map(|material| material.0.id());
    let bounded = VegetationInteractionField::bounded(
        interaction.center_m,
        interaction.push_direction_xz,
        interaction.radius_m,
        interaction.strength,
        profile.0,
    );
    let mut extension =
        VisualValleyVegetationExtension::from_profile_and_interaction(profile.0, bounded);
    let wrap_period = time.wrap_period().as_secs_f32();
    let current_time = finite_clamp(time.elapsed_secs_wrapped(), 0.0, wrap_period);
    let previous_time =
        (current_time - finite_clamp(time.delta_secs(), 0.0, wrap_period)).rem_euclid(wrap_period);
    extension.wind_parameters.z = current_time;
    extension.wind_parameters.w = previous_time;
    for (id, material) in materials.iter_mut() {
        // Static kit materials retain authored PBR and do not inherit forest weather/wind.
        if material.extension.temporal_parameters.y > 0.5 {
            continue;
        }
        if Some(id) != procedural_id && material.extension.prototype_parameters.x <= 0.5 {
            continue;
        }
        let mut next = extension.clone();
        next.wetness_parameters = material.extension.wetness_parameters;
        next.prototype_parameters = material.extension.prototype_parameters;
        next.temporal_parameters = material.extension.temporal_parameters;
        next.temporal_lod_buffer = material.extension.temporal_lod_buffer.clone();
        material.extension = next;
    }
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
            &Mesh3d,
            Option<&VegetationPlacementMarker>,
            Option<&VegetationChunkBatchMarker>,
            Option<&GovernedTreePrimitive>,
            Has<GovernedFarBatch>,
        ),
        Or<(
            With<VegetationPlacementMarker>,
            With<VegetationChunkBatchMarker>,
            With<GovernedTreePrimitive>,
            With<GovernedFarBatch>,
        )>,
    >,
    visible_ranges: Res<VisibleEntityRanges>,
    mut visible_meshes: Local<EntityHashSet>,
    mut stats: ResMut<VegetationProjectionStats>,
    meshes: Res<Assets<Mesh>>,
    mut mesh_counts: Local<HashMap<bevy::asset::AssetId<Mesh>, Option<u64>>>,
) {
    stats.live_meshes = None;
    let observation_frames = stats.visibility.observation_frames.saturating_add(1);
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
    let mut observation = VegetationVisibilityStats {
        observation_frames,
        status: observation_status,
        active_camera_views,
        ..default()
    };
    let Some((camera_entity, _, camera_visible_entities, no_indirect_drawing)) = active_camera
    else {
        stats.visibility = observation;
        stats.render_path.active_camera_gpu_preprocessing = VegetationGpuPreprocessingMode::Unknown;
        return;
    };
    if additional_active_cameras > 0 {
        stats.visibility = observation;
        stats.render_path.active_camera_gpu_preprocessing = VegetationGpuPreprocessingMode::Unknown;
        return;
    }

    visible_meshes.clear();
    visible_meshes.extend(
        camera_visible_entities
            .iter(TypeId::of::<Mesh3d>())
            .copied(),
    );
    mesh_counts.clear();
    let mut live_meshes = VegetationLiveMeshStats::default();
    for (entity, mesh, placement, batch, governed, far_batch) in &projections {
        let in_range = visible_ranges.entity_is_in_range_of_view(entity, camera_entity);
        let cpu_visible_for_camera = visible_meshes.contains(&entity);
        telemetry::count_mesh(
            &mut live_meshes,
            &mut mesh_counts,
            &meshes,
            &mesh.0,
            cpu_visible_for_camera,
        );
        if cpu_visible_for_camera && !in_range {
            observation.visible_outside_range_inconsistencies = observation
                .visible_outside_range_inconsistencies
                .saturating_add(1);
        }
        observation.total.observe(in_range, cpu_visible_for_camera);
        if far_batch {
            observation.hlod.observe(in_range, cpu_visible_for_camera);
            continue;
        }
        let bucket = match (
            placement.map(|marker| marker.lod),
            batch.map(|marker| marker.lod),
            governed.map(|marker| marker.lod),
        ) {
            (Some(VegetationProjectionLod::Near), None, None) | (None, None, Some(0)) => {
                &mut observation.near
            }
            (Some(VegetationProjectionLod::Medium), None, None) | (None, None, Some(1)) => {
                &mut observation.medium
            }
            (None, Some(VegetationProjectionLod::Hlod), None) | (None, None, Some(2)) => {
                &mut observation.hlod
            }
            (None, Some(VegetationProjectionLod::GrassBatch), None) => {
                &mut observation.grass_batches
            }
            _ => continue,
        };
        bucket.observe(in_range, cpu_visible_for_camera);
    }
    stats.visibility = observation;
    stats.live_meshes = Some(live_meshes);
    stats.render_path.active_camera_gpu_preprocessing = resolve_camera_preprocessing(
        stats.render_path.maximum_gpu_preprocessing,
        no_indirect_drawing,
    );
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

    #[test]
    fn vegetation_installs_normal_prepass_once_and_covers_later_cameras() {
        use bevy::core_pipeline::prepass::NormalPrepass;
        let mut app = App::new();
        app.add_systems(Update, configure_vegetation_prepass);
        let first = app.world_mut().spawn(Camera3d::default()).id();
        let other = app.world_mut().spawn_empty().id();
        app.update();
        assert!(app.world().entity(first).contains::<NormalPrepass>());
        assert!(!app.world().entity(other).contains::<NormalPrepass>());
        let second = app.world_mut().spawn(Camera3d::default()).id();
        app.update();
        assert!(app.world().entity(second).contains::<NormalPrepass>());
        app.update();
        let mut cameras = app
            .world_mut()
            .query_filtered::<Entity, With<NormalPrepass>>();
        assert_eq!(cameras.iter(app.world()).count(), 2);
    }
    use bevy::mesh::VertexAttributeValues;
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

    fn placement(kind: VegetationKind, x: f32, z: f32) -> ProceduralVegetationPlacement {
        ProceduralVegetationPlacement {
            variation_seed: u64::from(x.to_bits()) ^ u64::from(z.to_bits()).rotate_left(32),
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
    fn volumetric_vegetation_faces_point_outward() {
        let mut trunk = MeshAccumulator::default();
        append_cylinder(
            &mut trunk,
            Vec3::ZERO,
            0.22,
            2.9,
            8,
            bark_color(0.0),
            bark_color(0.22),
        )
        .unwrap();
        let mut canopy = MeshAccumulator::default();
        append_cone(
            &mut canopy,
            Vec3::ZERO,
            1.35,
            3.8,
            9,
            leaf_color(0, 0.35),
            leaf_color(1, 1.0),
        )
        .unwrap();
        for mesh in [&trunk, &canopy] {
            for (position, normal) in mesh.positions.iter().zip(&mesh.normals) {
                let radial = Vec3::new(position[0], 0.0, position[2]);
                if radial.length_squared() > 1.0e-6 {
                    assert!(
                        radial.dot(Vec3::from_array(*normal)) > 0.0,
                        "surface normal must face away from the trunk axis"
                    );
                }
            }
        }
        assert!(canopy.normals.iter().all(|normal| normal[1] > 0.0));
    }

    #[test]
    fn branched_tree_meshes_are_repeatable_and_sprays_face_outward() {
        for builder in [build_tree_near_mesh, build_tree_medium_mesh] {
            let first = builder(0.0).unwrap();
            let second = builder(0.0).unwrap();
            for attribute in [
                Mesh::ATTRIBUTE_POSITION,
                Mesh::ATTRIBUTE_NORMAL,
                Mesh::ATTRIBUTE_COLOR,
            ] {
                assert_eq!(
                    first.mesh.attribute(attribute),
                    second.mesh.attribute(attribute)
                );
            }
            assert_eq!(first.aabb.max().y.to_bits(), 5.65_f32.to_bits());
            assert!(first.aabb.half_extents.x <= 1.35);
            assert!(first.aabb.half_extents.z <= 1.35);
        }
        let points = [
            Vec3::new(0.1, 2.0, 0.0),
            Vec3::new(1.2, 1.8, 0.0),
            Vec3::new(0.5, 1.8, -0.3),
            Vec3::new(0.6, 2.1, 0.4),
        ];
        let center = points.into_iter().sum::<Vec3>() * 0.25;
        let mut spray = MeshAccumulator::default();
        append_foliage_spray(&mut spray, points, 3).unwrap();
        assert_eq!(spray.indices.len(), 12);
        for (position, normal) in spray.positions.iter().zip(&spray.normals) {
            assert!((Vec3::from_array(*position) - center).dot(Vec3::from_array(*normal)) > 0.0);
        }
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
            assert!(mesh.aabb.half_extents.x < 16.0);
            assert!(mesh.aabb.half_extents.y < 16.0);
            assert!(mesh.aabb.half_extents.z < 16.0);
        }
        assert_eq!((near.mesh.count_vertices(), near.triangles), (731, 249));
        assert_eq!((medium.mesh.count_vertices(), medium.triangles), (338, 116));
        assert_eq!((shrub.mesh.count_vertices(), shrub.triangles), (192, 96));
        assert_eq!((fern.mesh.count_vertices(), fern.triangles), (224, 112));
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
            let tip = Vec3::from_array(blade[6]);
            let base = Vec3::new(base_x, base_y, base_z);
            assert!((0.2199..=0.6501).contains(&((tip.y - base.y) / patches[0].scale)));
            let width = Vec3::from_array(blade[0]).distance(Vec3::from_array(blade[1]));
            assert!((0.0249..=0.0551).contains(&(width / patches[0].scale)));
            assert!((tip - base).xz().length() > 0.05);
            // Both mesh sections meet exactly: no detached tip or duplicate flat blade.
            assert_eq!(blade[3].map(f32::to_bits), blade[4].map(f32::to_bits));
            assert_eq!(blade[2].map(f32::to_bits), blade[5].map(f32::to_bits));
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
        assert_eq!(
            build_grass_chunk_mesh(&terrain, Vec3::ZERO, &patches, 0, 0.0).unwrap_err(),
            VegetationMeshError::GrassBladeCountIsZero
        );
    }

    #[test]
    fn grass_variation_seed_uses_canonical_identity_not_transform_or_chunk() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = forge_world::embedded_visual_valley_vegetation_document().unwrap();
        let generated = VegetationGenerationPlan::from_documents(&terrain, &vegetation)
            .unwrap()
            .generate()
            .unwrap();
        let mut grass = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter(|instance| instance.kind == VegetationKind::Grass);
        let mut first = grass.next().unwrap().clone();
        let second = grass.next().unwrap();
        let seed = procedural_placement(&first, Vec3::ZERO).variation_seed;
        first.position_m.x += 4.0;
        first.yaw_radians += 0.5;
        first.scale *= 1.1;
        assert_eq!(
            seed,
            procedural_placement(&first, Vec3::new(64.0, 0.0, 32.0)).variation_seed
        );
        first.id = second.id;
        assert_ne!(
            seed,
            procedural_placement(&first, Vec3::ZERO).variation_seed
        );
    }

    #[test]
    fn grass_geometry_survives_patch_reordering_and_density_subsets() {
        let terrain = TerrainGenerationPlan::from_document(
            &embedded_visual_valley_terrain_document().unwrap(),
        )
        .unwrap();
        let a = placement(VegetationKind::Grass, -3.0, 0.0);
        let b = placement(VegetationKind::Grass, 3.0, 0.0);
        let positions = |patches: &[ProceduralVegetationPlacement], blades| {
            let built = build_grass_chunk_mesh(&terrain, Vec3::ZERO, patches, blades, 0.0).unwrap();
            let Some(VertexAttributeValues::Float32x3(values)) =
                built.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                panic!("expected positions");
            };
            values
                .iter()
                .map(|position| position.map(f32::to_bits))
                .collect::<Vec<_>>()
        };
        let original = positions(&[a, b], 12);
        let reordered = positions(&[b, a], 12);
        assert_eq!(
            &original[..84],
            &reordered[84..],
            "unaffected patch A changed after reorder"
        );
        assert_eq!(
            &original[84..],
            &reordered[..84],
            "unaffected patch B changed after reorder"
        );
        assert_eq!(
            &original[84..],
            positions(&[b], 12),
            "remaining patch changed after density filtering"
        );
        assert_eq!(
            &original[..42],
            positions(&[a], 6),
            "surviving blades changed at lower blade budget"
        );
    }

    #[test]
    fn grass_fade_is_terminal_without_extending_the_visibility_budget() {
        let source = profile();
        let ranges = VegetationLodRanges::from_profile(source);
        assert_eq!(ranges.grass.end_margin.start.to_bits(), 50.0_f32.to_bits());
        assert_eq!(
            ranges.grass.end_margin.end.to_bits(),
            source.medium_lod_distance_m.to_bits()
        );
        assert!(ranges.grass.use_aabb);
        let narrow = VegetationLodRanges::from_profile(VegetationPresentationProfile {
            near_lod_distance_m: 56.0,
            ..source
        });
        assert_eq!(narrow.grass.end_margin.start.to_bits(), 56.0_f32.to_bits());
        assert_eq!(narrow.near.end_margin.start.to_bits(), 56.0_f32.to_bits());
        assert_eq!(narrow.medium.start_margin, narrow.near.end_margin);
    }

    #[test]
    fn world_sketch_grass_roots_follow_carved_terrain_in_translated_chunks() {
        let sketch = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let compiled = forge_world::compile_world_sketch(&sketch).unwrap();
        let terrain = TerrainGenerationPlan::from_document(&compiled.carved_terrain).unwrap();
        let prepared =
            prepare_vegetation_projection(&compiled.carved_terrain, &compiled.vegetation).unwrap();
        let mut roots = 0;
        for entity in &prepared.entities {
            if !matches!(entity.mesh_key, VegetationMeshKey::GrassChunk { .. }) {
                continue;
            }
            let mesh = &prepared
                .meshes
                .iter()
                .find(|mesh| mesh.key == entity.mesh_key)
                .unwrap()
                .value
                .mesh;
            let Some(VertexAttributeValues::Float32x3(positions)) =
                mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                panic!("grass mesh must have positions");
            };
            for blade in positions.as_chunks::<7>().0 {
                let base = (Vec3::from_array(blade[0]) + Vec3::from_array(blade[1])) * 0.5;
                let world = entity.transform.transform_point(base);
                let surface = terrain.surface_at(world.x, world.z).unwrap();
                assert!(
                    (world.y - surface.height_m).abs() < 0.001,
                    "root={world:?} terrain={}",
                    surface.height_m
                );
                roots += 1;
            }
        }
        assert!(roots > 1_000);
        let camera = sketch
            .validation_cameras
            .iter()
            .find(|camera| camera.id == "human_bridge")
            .unwrap();
        let ground = terrain
            .surface_at(camera.position_m[0], camera.position_m[2])
            .unwrap();
        let clearance = camera.position_m[1] - ground.height_m;
        assert!(
            (1.6..=1.8).contains(&clearance),
            "human camera clearance={clearance}"
        );
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
        assert_eq!(mesh.mesh.count_vertices(), 26 + 12 + 12);
        assert_eq!(mesh.triangles, 10 + 8 + 8);
    }

    #[test]
    fn low_growth_hlod_is_rooted_bounded_and_has_no_diamond_tip() {
        for (kind, height, width) in [
            (VegetationKind::Shrub, 1.35_f32, 1.15_f32),
            (VegetationKind::Fern, 0.44_f32, 2.1_f32),
        ] {
            let mut plant = placement(kind, 0.0, 0.0);
            plant.position_m = Vec3::ZERO;
            plant.scale = 1.0;
            plant.yaw_radians = 0.0;
            let mesh = build_hlod_chunk_mesh(&[plant], 0.0).unwrap();
            assert_complete_mesh(&mesh);
            assert_eq!(mesh.triangles, 8);
            assert_eq!(mesh.mesh.count_vertices(), 12);
            assert_eq!(mesh.aabb.min().y.to_bits(), 0.0_f32.to_bits());
            assert!((mesh.aabb.max().y - height).abs() < 1e-6);
            assert!((mesh.aabb.max().x - mesh.aabb.min().x - width).abs() < 1e-6);
            let Some(VertexAttributeValues::Float32x3(positions)) =
                mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                panic!("positions");
            };
            for plane in positions.as_chunks::<6>().0 {
                assert_eq!(plane[0][1].to_bits(), 0.0_f32.to_bits());
                assert_eq!(plane[1][1].to_bits(), 0.0_f32.to_bits());
                assert!(
                    Vec3::from_array(plane[0]).distance(Vec3::from_array(plane[1])) > width * 0.4
                );
            }
            let Some(VertexAttributeValues::Float32x3(normals)) =
                mesh.mesh.attribute(Mesh::ATTRIBUTE_NORMAL)
            else {
                panic!("normals");
            };
            let Some(Indices::U32(indices)) = mesh.mesh.indices() else {
                panic!("indices");
            };
            for triangle in indices.as_chunks::<3>().0 {
                let points = [triangle[0], triangle[1], triangle[2]]
                    .map(|index| Vec3::from_array(positions[usize::try_from(index).unwrap()]));
                assert!(
                    (points[1] - points[0])
                        .cross(points[2] - points[0])
                        .dot(Vec3::from_array(
                            normals[usize::try_from(triangle[0]).unwrap()]
                        ))
                        > 0.0
                );
            }
            let replay = build_hlod_chunk_mesh(&[plant], 0.0).unwrap();
            assert_eq!(
                mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION),
                replay.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            );
            assert_eq!(mesh.mesh.indices(), replay.mesh.indices());
        }
    }

    #[test]
    fn hlod_vertices_retain_the_exact_placement_root_for_distance_fades() {
        let mut woody = [
            placement(VegetationKind::Tree, -31.0, 30.0),
            placement(VegetationKind::Shrub, 12.0, -30.0),
            placement(VegetationKind::Fern, 31.0, 10.0),
        ];
        for (index, height) in [40.0, -20.0, 8.0].into_iter().enumerate() {
            woody[index].position_m.y = height;
        }
        let mesh = build_hlod_chunk_mesh(&woody, 0.0).unwrap();
        let Some(VertexAttributeValues::Float32x2(xz)) = mesh.mesh.attribute(Mesh::ATTRIBUTE_UV_0)
        else {
            panic!("root XZ");
        };
        let Some(VertexAttributeValues::Float32x2(y)) = mesh.mesh.attribute(Mesh::ATTRIBUTE_UV_1)
        else {
            panic!("root Y");
        };
        assert_eq!(xz.len(), mesh.mesh.count_vertices());
        assert_eq!(y.len(), xz.len());
        let mut start = 0;
        for (placement, count) in woody.iter().zip([26, 12, 12]) {
            for index in start..start + count {
                assert_eq!(
                    [xz[index][0], y[index][0], xz[index][1]].map(f32::to_bits),
                    placement.position_m.to_array().map(f32::to_bits)
                );
            }
            start += count;
        }
        assert!(
            build_tree_near_mesh(0.0)
                .unwrap()
                .mesh
                .attribute(Mesh::ATTRIBUTE_UV_1)
                .is_none()
        );
    }

    #[test]
    fn hlod_cpu_batch_range_cannot_drop_a_shader_visible_placement() {
        let mut woody = [
            placement(VegetationKind::Tree, -31.0, -31.0),
            placement(VegetationKind::Tree, 31.0, 31.0),
        ];
        woody[0].position_m.y = -40.0;
        woody[1].position_m.y = 60.0;
        let nominal = VegetationLodRanges::from_profile(profile()).hlod;
        let broad = hlod_batch_visibility_range(&woody, &nominal);
        assert!(!broad.use_aabb);
        assert!(!broad.is_abrupt());
        for placement in woody {
            for direction in [
                Vec3::X,
                Vec3::NEG_X,
                Vec3::Y,
                Vec3::NEG_Y,
                Vec3::Z,
                Vec3::NEG_Z,
                Vec3::ONE.normalize(),
            ] {
                for distance in [
                    nominal.start_margin.start + 0.01,
                    nominal.start_margin.end,
                    nominal.end_margin.end - 0.01,
                ] {
                    let camera = placement.position_m + direction * distance;
                    assert!(camera.length() >= broad.start_margin.start);
                    assert!(camera.length() < broad.end_margin.end);
                }
            }
        }
    }

    #[test]
    fn tree_transitions_are_short_complementary_and_profile_bounded() {
        let source = forge_world::VisualValleyVegetationRecipe::default()
            .build()
            .unwrap()
            .presentation;
        let ranges = VegetationLodRanges::from_profile(source);
        assert_eq!(ranges.near.end_margin, 64.0..72.0);
        assert_eq!(ranges.medium.start_margin, ranges.near.end_margin);
        assert_eq!(ranges.medium.end_margin, 172.0..180.0);
        assert_eq!(ranges.hlod.start_margin, ranges.medium.end_margin);
        let extension = VisualValleyVegetationExtension::from_profile_and_interaction(
            source,
            VegetationInteractionField::default(),
        );
        assert_eq!(
            extension.hlod_distances.to_array().map(f32::to_bits),
            [172.0_f32, 180.0, 384.0, 0.0].map(f32::to_bits)
        );
    }

    #[test]
    fn far_tree_keeps_a_narrow_bark_trunk_and_the_near_canopy_envelope() {
        let mut tree = placement(VegetationKind::Tree, 0.0, 0.0);
        tree.position_m = Vec3::ZERO;
        tree.scale = 1.0;
        tree.yaw_radians = 0.0;
        let mesh = build_hlod_chunk_mesh(&[tree], 0.0).unwrap();
        let near = build_tree_near_mesh(0.0).unwrap();
        assert_eq!(mesh.aabb.max().y.to_bits(), near.aabb.max().y.to_bits());
        assert_eq!(mesh.triangles, 10);
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("positions");
        };
        let Some(VertexAttributeValues::Float32x4(colors)) =
            mesh.mesh.attribute(Mesh::ATTRIBUTE_COLOR)
        else {
            panic!("colors");
        };
        let mut bark = 0;
        let mut leaves = 0;
        for (position, color) in positions.iter().zip(colors) {
            if color[2] > 0.5 {
                assert!(position[0].hypot(position[2]) <= 0.221);
                assert!(position[1] <= 2.9);
                bark += 1;
            } else {
                assert!(position[1] >= 1.75);
                assert!(color[3] <= tree.wind_response);
                leaves += 1;
            }
        }
        assert_eq!((bark, leaves), (8, 18));
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

        assert_eq!(ranges.near.end_margin, ranges.medium.start_margin);
        assert_eq!(ranges.medium.end_margin, ranges.hlod.start_margin);
        assert_eq!(ranges.individual.end_margin, ranges.hlod.start_margin);
        assert_eq!(
            ranges.grass.end_margin.end.to_bits(),
            ranges.medium.start_margin.end.to_bits()
        );
        assert!(ranges.grass.end_margin.start >= ranges.medium.start_margin.start);
        assert_eq!(ranges.hlod.end_margin, 420.0..420.0);
        assert!(!ranges.near.is_abrupt());
        assert!(!ranges.medium.is_abrupt());
        assert!(!ranges.hlod.is_abrupt());
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
    }

    #[test]
    fn mesh_tags_are_compact_variation_only_not_canonical_ids() {
        let placement = placement(VegetationKind::Tree, 0.0, 0.0);
        let mesh_tag = mesh_tag_for(placement.wind_phase_radians, placement.wind_response);
        assert_eq!(mesh_tag.0 >> 24, 191);
        assert_ne!(mesh_tag.0 & TAG_PHASE_MASK, 0);
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
        assert!(prepared.stats.projection.hlod_batches <= 64);
        assert_eq!(
            prepared.stats.projection.grass_blades,
            u64::from(prepared.stats.placements.accepted_by_kind.grass_patches)
                * u64::from(vegetation.presentation.grass_blades_per_patch)
        );
    }

    #[test]
    fn temporal_shadow_helper_parses_and_validates_as_fragment_wgsl() {
        let shader = include_str!("shaders/visual_valley_vegetation_deform.wgsl");
        let start = shader.find("fn temporal_shadow_discard(").unwrap();
        let end = start + shader[start..].find("@fragment").unwrap();
        let source = format!(
            "const DITHER_THRESHOLD_MAP: vec4<u32> = vec4(0x0a020800u,0x060e040cu,0x09010b03u,0x050d070fu);\n{}\n@fragment fn proof() -> @location(0) vec4<f32> {{ temporal_shadow_discard(vec4(1.0), 8); return vec4(1.0); }}",
            &shader[start..end]
        );
        let module = naga::front::wgsl::parse_str(&source).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::empty(),
        )
        .validate(&module)
        .unwrap();
    }

    #[test]
    fn governed_wind_parameters_are_explicit_and_height_bounded() {
        let original = VisualValleyVegetationExtension::default();
        assert_eq!(original.prototype_parameters, Vec4::ZERO);
        let governed = original
            .clone()
            .with_governed_prototype_height(5.65)
            .unwrap();
        assert_eq!(governed.prototype_parameters.x.to_bits(), 1_f32.to_bits());
        assert_eq!(
            governed.prototype_parameters.y.to_bits(),
            5.65_f32.recip().to_bits()
        );
        assert_eq!(governed.wind_parameters, original.wind_parameters);
        for height in [0.0, -1.0, f32::NAN, f32::INFINITY, 1001.0] {
            assert!(
                original
                    .clone()
                    .with_governed_prototype_height(height)
                    .is_err()
            );
        }
        let shader = include_str!("shaders/visual_valley_vegetation_deform.wgsl");
        assert!(shader.contains("height_fraction * height_fraction"));
        assert!(shader.contains("vertex.position.y * vegetation.prototype_parameters.y"));
        assert!(shader.contains("if vegetation.prototype_parameters.x <= 0.5"));
        // Source-contract guard only; native shader compilation and a rendered prototype
        // are separate evidence, not established by these text checks.
    }

    #[test]
    fn governed_material_clock_preserves_height_wetness_and_foreign_materials() {
        use bevy::ecs::system::RunSystemOnce;
        let mut world = World::new();
        let mut time = Time::<()>::default();
        time.advance_by(std::time::Duration::from_secs(2));
        world.insert_resource(time);
        world.insert_resource(VegetationInteractionField::default());
        world.insert_resource(VegetationPresentation(
            forge_world::VisualValleyVegetationRecipe::default()
                .build()
                .unwrap()
                .presentation,
        ));
        let mut materials = Assets::<VisualValleyVegetationMaterial>::default();
        let mut extension = VisualValleyVegetationExtension::default()
            .with_governed_prototype_height(5.65)
            .unwrap();
        extension.wetness_parameters = Vec4::new(0.7, 0.4, 0.8, 0.0);
        extension.temporal_parameters = Vec4::X;
        let original = extension.clone();
        let governed = materials.add(VisualValleyVegetationMaterial {
            base: StandardMaterial::default(),
            extension,
        });
        let foreign = materials.add(VisualValleyVegetationMaterial {
            base: StandardMaterial::default(),
            extension: VisualValleyVegetationExtension::default(),
        });
        let static_extension = VisualValleyVegetationExtension {
            prototype_parameters: Vec4::X,
            temporal_parameters: Vec4::new(1.0, 1.0, 0.0, 0.0),
            ..VisualValleyVegetationExtension::default()
        };
        let static_kit = materials.add(VisualValleyVegetationMaterial {
            base: StandardMaterial::default(),
            extension: static_extension.clone(),
        });
        world.insert_resource(materials);
        world
            .run_system_once(update_vegetation_interaction_uniform)
            .unwrap();
        let materials = world.resource::<Assets<VisualValleyVegetationMaterial>>();
        let updated = &materials.get(&governed).unwrap().extension;
        assert_eq!(updated.wind_parameters.z.to_bits(), 2_f32.to_bits());
        assert_eq!(updated.wind_parameters.w.to_bits(), 0_f32.to_bits());
        assert_eq!(updated.prototype_parameters, original.prototype_parameters);
        assert_eq!(updated.wetness_parameters, original.wetness_parameters);
        assert_eq!(updated.temporal_parameters, original.temporal_parameters);
        assert_eq!(updated.temporal_lod_buffer, original.temporal_lod_buffer);
        let static_updated = &materials.get(&static_kit).unwrap().extension;
        assert_eq!(
            static_updated.wind_parameters,
            static_extension.wind_parameters
        );
        assert_eq!(
            static_updated.temporal_parameters,
            static_extension.temporal_parameters
        );
        assert_eq!(
            static_updated.wetness_parameters,
            static_extension.wetness_parameters
        );
        assert_eq!(
            materials.get(&foreign).unwrap().extension.wind_parameters,
            Vec4::ZERO
        );
    }

    #[test]
    fn temporal_shader_contract_preserves_fallback_and_all_render_passes() {
        let shader = include_str!("shaders/visual_valley_vegetation_deform.wgsl");
        assert!(shader.contains("@binding(101)"));
        assert!(shader.contains("slot < arrayLength(&temporal_lods)"));
        assert!(shader.contains("temporal_lods[slot].metadata.y"));
        assert!(shader.contains("temporal_lods[slot].metadata.x == 1u"));
        assert!(shader.contains("return spatial_fallback;"));
        assert!(shader.contains("temporal_code(u32(far_wind.w), 2u"));
        assert!(
            shader
                .contains("temporal_shadow_discard(in.position, temporal_code(slot, tag % 4u, 0))")
        );
        assert_eq!(
            shader
                .matches("pbr_prepass_functions::prepass_alpha_discard(in);")
                .count(),
            1
        );
        // Exhaustive signed endpoint contract; a stopped fade ends at solid/hidden,
        // and every intermediate texel belongs to exactly one LOD.
        for progress in 0..=16_i32 {
            for rank in 0..16_i32 {
                let outgoing = progress == 0 || (progress < 16 && rank < 16 - progress);
                let incoming_code = progress - 16;
                let incoming =
                    incoming_code == 0 || (incoming_code > -16 && rank >= -incoming_code);
                assert_ne!(outgoing, incoming);
            }
        }
    }

    #[test]
    fn shader_uses_material_time_mesh_tag_dither_and_forward_pbr_without_external_assets() {
        let shader = include_str!("shaders/visual_valley_vegetation_deform.wgsl");
        assert!(
            shader
                .replace("\r\n", "\n")
                .contains("#ifdef PREPASS_PIPELINE\n#ifdef MOTION_VECTOR_PREPASS")
        );
        assert!(!shader.contains("mesh_view_bindings::globals"));
        assert!(shader.contains("vegetation.wind_parameters.z"));
        assert!(shader.contains("vegetation.wind_parameters.w"));
        assert!(shader.contains("mesh_functions::get_tag"));
        assert!(shader.contains("VISIBILITY_RANGE_DITHER"));
        assert!(shader.contains("get_visibility_range_dither_level"));
        assert!(
            shader.contains("visibility_range_dither(in.position, in.visibility_range_dither)")
        );
        assert!(shader.contains("#import bevy_pbr::pbr_functions::visibility_range_dither;"));
        let dither = shader
            .find("visibility_range_dither(in.position, in.visibility_range_dither)")
            .unwrap();
        let pbr_input = shader
            .find("var pbr_input = pbr_input_from_standard_material")
            .unwrap();
        let alpha_discard = shader
            .find("pbr_input.material.base_color = alpha_discard")
            .unwrap();
        assert!(pbr_input < dither);
        assert!(pbr_input < alpha_discard);
        assert!(shader.contains("pbr_types::STANDARD_MATERIAL_FLAGS_ALPHA_MODE_BLEND"));
        assert!(shader.contains("1.0 - abs(f32(in.visibility_range_dither)) / 16.0"));
        assert!(shader.contains("pbr_input.material.base_color.a *= lod_opacity"));
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
