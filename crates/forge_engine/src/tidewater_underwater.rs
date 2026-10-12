//! Tidewater underwater participating-medium post process.
#![allow(clippy::needless_pass_by_value)] // Bevy render-system parameters are owned wrappers.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/post/Underwater.js`, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see `third_party/tidewater/LICENSE`.

use bevy::{
    asset::{UntypedAssetId, load_internal_asset, uuid_handle},
    core_pipeline::{Core3dSystems, FullscreenShader, schedule::Core3d},
    ecs::query::QueryFilter,
    pbr::{MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        erased_render_asset::ErasedRenderAssets,
        extract_component::{
            ComponentUniforms, DynamicUniformIndex, ExtractComponent, ExtractComponentPlugin,
            UniformComponentPlugin,
        },
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            BindingResource, BufferSize, ColorTargetState, ColorWrites, FragmentState, Operations,
            PipelineCache, RenderPassColorAttachment, RenderPassDescriptor, RenderPipeline,
            RenderPipelineDescriptor, Sampler, SamplerBindingType, SamplerDescriptor, ShaderStages,
            ShaderType, SpecializedRenderPipeline, SpecializedRenderPipelines, TextureFormat,
            TextureSampleType, TextureViewId,
            binding_types::{sampler, storage_buffer_read_only_sized, texture_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, ViewQuery},
        storage::{GpuShaderBuffer, ShaderBuffer},
        view::{ViewDepthTexture, ViewTarget},
    },
    shader::Shader,
};

use crate::tidewater_temporal::{
    frame::ViewPacket,
    hdr::{HdrObservations, HdrRole, SourceHdrSystems},
    resolve::ResolveEnabled,
};
use crate::tidewater_water_query::{Queries, SampleBatch};
use std::sync::{
    Arc,
    atomic::{AtomicU64, Ordering},
};

const SHADER_HANDLE: Handle<Shader> = uuid_handle!("03527285-2084-48a1-b149-06db1f69079a");
const CAMERA_SAMPLE_TOLERANCE_M: f32 = 0.75;
const LENS_REACH_M: f32 = 0.35;
pub mod medium;
mod shafts;

/// Orders consumers of the current camera immersion after its observation.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct TidewaterUnderwaterUpdateSet;

/// Carries the source visible-surface medium in the opaque HDR alpha channel.
/// The immersion pass consumes it before other post passes overwrite alpha.
pub fn visible_water_mask_shader_source(source: &str) -> Result<String, String> {
    const OLD: &str = "return shadeWater(i).color;";
    const NEW: &str =
        "let result = shadeWater(i);\n return vec4f(result.color.rgb, 0.25 + 0.5 * result.mask.x);";
    if source.matches(OLD).count() != 1 {
        return Err("water optics fragment mask anchor is missing or ambiguous".into());
    }
    Ok(source.replacen(OLD, NEW, 1))
}

// Exact Eulerian solve from pinned Tidewater WaterQuery.js. The optical shader
// already contains its terrain, shoreline, attenuation and FFT dependencies.
const SURFACE_HEIGHT_WGSL: &str = r"
fn waterQueryDispAt(x0: vec2f, depth: f32) -> vec3f {
    var d = vec3f(0.0);
    d += textureSampleLevel(oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[0].x, 0, 0.0).xyz * waterSurfaceCascadeAttenuation(0, depth);
    d += textureSampleLevel(oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[1].x, 1, 0.0).xyz * waterSurfaceCascadeAttenuation(1, depth);
    d += textureSampleLevel(oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[2].x, 2, 0.0).xyz * waterSurfaceCascadeAttenuation(2, depth);
    d += textureSampleLevel(oceanDisplacement, smpLinearRepeat, x0 / ocean.sizes[3].x, 3, 2.0).xyz * waterSurfaceCascadeAttenuation(3, depth);
    d *= waterSurface.amplitude;
    d += shoreEvaluateNoNormal(x0, depth, terrainHeightAt(x0)).disp;
    return d;
}

fn waterQueryHeightAtXZ(p: vec2f) -> f32 {
    let depth = frame.seaLevel - terrainHeightAt(p);
    var x0 = p;
    for (var i = 0; i < 2; i++) {
        x0 = p - waterQueryDispAt(x0, depth).xz;
    }
    return frame.seaLevel + waterQueryDispAt(x0, depth).y;
}

fn setupUnderwaterHeightFrame() {
    frame = waterFrame;
    terrainParams.origin = -1024.0;
    terrainParams.size = 2048.0;
    terrainParams.res = 2048.0;
    terrainParams.sunBaked = 1.0;
    terrainParams.shoreRes = 512.0;
}
";

const DISTANCE_GUARD: &str = "    if distance(endpoint.xz, water_queries.points[0].xy) > 10.0 {\n        return select(0.0, 1.0, settings.state.x < water_queries.results[0].x);\n    }\n";

/// Combines the already adapted optical shader with Tidewater's exact height
/// solver. The material bind group is reused by the fullscreen pass.
pub(crate) fn surface_height_shader_source(optical_shader: &str) -> Result<String, String> {
    let material_marker = "@group(#{MATERIAL_BIND_GROUP})";
    let water_fragment = "@fragment fn fragment(";
    if !optical_shader.contains(material_marker)
        || optical_shader.matches(water_fragment).count() != 1
        || !optical_shader.contains("fn shoreEvaluateNoNormal(")
        || !optical_shader.contains("fn waterSurfaceCascadeAttenuation(")
        || optical_shader.contains("fn waterQueryHeightAtXZ(")
    {
        return Err("optical shader cannot supply the pinned water height query".into());
    }
    let optical_shader = optical_shader
        .replace(material_marker, "@group(1)")
        .replacen(water_fragment, "@fragment fn water_surface_fragment(", 1);
    Ok(format!("{optical_shader}\n{SURFACE_HEIGHT_WGSL}"))
}

pub fn surface_query_shader_source(optical_shader: &str) -> Result<String, String> {
    let height_shader = surface_height_shader_source(optical_shader)?;
    let mut post = include_str!("tidewater_underwater.wgsl")
        .replace("\r\n", "\n")
        .replace("@group(0)", "@group(2)")
        .replace("const PI: f32", "const UNDERWATER_PI: f32")
        .replace("4.0 * PI", "4.0 * UNDERWATER_PI")
        .replace("1.0 / PI", "1.0 / UNDERWATER_PI")
        .replace(" * PI * ", " * UNDERWATER_PI * ");
    let query_start = post
        .find("fn queried_height_at(")
        .ok_or("underwater local query function is missing")?;
    let query_end = post[query_start..]
        .find("fn near_lens_in_water(")
        .map(|index| query_start + index)
        .ok_or("underwater near-lens guard is missing")?;
    post.replace_range(
        query_start..query_end,
        "fn queried_height_at(xz: vec2<f32>) -> f32 { return waterQueryHeightAtXZ(xz); }\n\n",
    );
    if post.matches("fn near_lens_in_water(").count() != 1
        || post.matches("fn lens_medium(").count() != 1
        || post.matches(DISTANCE_GUARD).count() != 1
        || post.matches("    let uv = input.uv;").count() != 1
        || post.matches("fn setup_lens_medium_frame() {}").count() != 1
    {
        return Err("underwater endpoint shader anchor changed".into());
    }
    post = post.replacen(DISTANCE_GUARD, "", 1).replacen(
        "    let uv = input.uv;",
        "    setupUnderwaterHeightFrame();\n    let uv = input.uv;",
        1,
    );
    post = post.replacen(
        "fn setup_lens_medium_frame() {}",
        "fn setup_lens_medium_frame() { setupUnderwaterHeightFrame(); }",
        1,
    );
    Ok(format!(
        "{height_shader}\n{post}\n{}",
        include_str!("tidewater_underwater_shafts.wgsl")
    ))
}

/// Reuses the prepared optical water material and its live FFT/shore inputs.
#[derive(Resource, Clone, ExtractResource)]
pub struct TidewaterUnderwaterSurfaceMaterial(pub UntypedAssetId);

#[derive(Resource, Clone)]
struct SurfaceShader {
    shader: Handle<Shader>,
    material_layout: fn(&RenderDevice) -> BindGroupLayoutDescriptor,
}

/// Source optical coefficients consumed by the post process.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct TidewaterUnderwaterOptics {
    pub absorption: Vec3,
    pub scattering: Vec3,
    pub sun_direction: Vec3,
    pub sun_color: Vec3,
    pub sky_irradiance: Vec3,
}

impl TidewaterUnderwaterOptics {
    pub fn new(
        absorption: Vec3,
        scattering: Vec3,
        sun_direction: Vec3,
        sun_color: Vec3,
        sky_irradiance: Vec3,
    ) -> Result<Self, String> {
        let optics = Self {
            absorption,
            scattering,
            sun_direction,
            sun_color,
            sky_irradiance,
        };
        if ![
            absorption,
            scattering,
            sun_direction,
            sun_color,
            sky_irradiance,
        ]
        .into_iter()
        .all(Vec3::is_finite)
        {
            return Err("underwater optics must be finite".into());
        }
        if absorption.min_element() < 0.0
            || scattering.min_element() < 0.0
            || sun_color.min_element() < 0.0
            || sky_irradiance.min_element() < 0.0
        {
            return Err("underwater optical energy cannot be negative".into());
        }
        if sun_direction.length_squared() <= f32::EPSILON {
            return Err("underwater sun direction cannot be zero".into());
        }
        Ok(optics)
    }
}

/// Marks the camera whose lens is evaluated against Tidewater's GPU surface.
#[derive(Component, Clone, Copy, Debug, Default)]
pub struct TidewaterUnderwaterCamera;

/// Runtime switch for deterministic active/bypassed comparisons.
#[derive(Resource, Clone, Copy, Debug, PartialEq)]
pub struct TidewaterUnderwaterControl {
    pub enabled: bool,
    pub shafts_strength: f32,
}

impl Default for TidewaterUnderwaterControl {
    fn default() -> Self {
        Self {
            enabled: true,
            shafts_strength: 1.0,
        }
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum TidewaterImmersion {
    #[default]
    Unavailable,
    Air,
    SurfaceBand,
    FullyImmersed,
}

/// CPU-visible state of the last camera/sample match.
#[derive(Resource, Clone, Copy, Debug, Default, PartialEq)]
pub struct TidewaterUnderwaterObservation {
    pub immersion: TidewaterImmersion,
    pub effect_enabled: bool,
    pub water_height_m: f32,
    pub camera_height_m: f32,
    pub camera_depth_m: f32,
    pub sample_step: u32,
    pub sample_time_s: f64,
    pub encoded_frames: u64,
    pub encoded_enabled_frames: u64,
    pub encoded_bypassed_frames: u64,
    pub shafts_strength: f32,
    pub encoded_shaft_frames: u64,
}

impl TidewaterUnderwaterObservation {
    #[must_use]
    pub fn camera_underwater(self) -> bool {
        self.immersion == TidewaterImmersion::FullyImmersed
    }

    #[must_use]
    pub fn effect_active(self) -> bool {
        self.effect_enabled
            && matches!(
                self.immersion,
                TidewaterImmersion::SurfaceBand | TidewaterImmersion::FullyImmersed
            )
    }

    #[must_use]
    pub fn pipeline_ready(self) -> bool {
        self.encoded_frames > 0
    }
}

#[derive(Resource, Clone)]
struct Source {
    queries: Queries,
    optics: TidewaterUnderwaterOptics,
}

#[derive(Resource, Clone)]
struct Packet(Handle<ShaderBuffer>);

#[derive(Resource, Clone, Default)]
struct Encoding {
    frames: Arc<AtomicU64>,
    enabled_frames: Arc<AtomicU64>,
    bypassed_frames: Arc<AtomicU64>,
    shaft_frames: Arc<AtomicU64>,
}

impl Encoding {
    fn record(&self, enabled: bool) {
        self.frames.fetch_add(1, Ordering::AcqRel);
        if enabled {
            self.enabled_frames.fetch_add(1, Ordering::AcqRel);
        } else {
            self.bypassed_frames.fetch_add(1, Ordering::AcqRel);
        }
    }

    fn counts(&self) -> (u64, u64, u64) {
        (
            self.frames.load(Ordering::Acquire),
            self.enabled_frames.load(Ordering::Acquire),
            self.bypassed_frames.load(Ordering::Acquire),
        )
    }
}

/// Installs the source-derived Beer-Lambert and in-scattering pass.
pub struct TidewaterUnderwaterPlugin {
    source: Source,
    packet: Handle<ShaderBuffer>,
    control: TidewaterUnderwaterControl,
    surface_shader: Option<SurfaceShader>,
    shafts: Option<shafts::Source>,
}

impl TidewaterUnderwaterPlugin {
    #[must_use]
    pub fn new(
        queries: Queries,
        packet: Handle<ShaderBuffer>,
        optics: TidewaterUnderwaterOptics,
    ) -> Self {
        Self {
            source: Source { queries, optics },
            packet,
            control: TidewaterUnderwaterControl::default(),
            surface_shader: None,
            shafts: None,
        }
    }

    /// Controls the visual branch while keeping the pass encoded for deterministic A/B evidence.
    #[must_use]
    pub fn with_effect_enabled(mut self, enabled: bool) -> Self {
        self.control.enabled = enabled;
        self
    }

    /// Enables the source Eulerian height solve through the live water material.
    #[must_use]
    pub fn with_surface_shader(
        mut self,
        shader: Handle<Shader>,
        material_layout: fn(&RenderDevice) -> BindGroupLayoutDescriptor,
    ) -> Self {
        self.surface_shader = Some(SurfaceShader {
            shader,
            material_layout,
        });
        self
    }

    /// Source-sized photon maps from the same FFT; requires the full optical shader.
    pub fn with_caustic_maps(
        mut self,
        maps: crate::tidewater_caustics::CausticMaps,
        fine_tile_m: f32,
        broad_tile_m: f32,
    ) -> Result<Self, String> {
        if self.surface_shader.is_none()
            || !fine_tile_m.is_finite()
            || !broad_tile_m.is_finite()
            || fine_tile_m <= 0.0
            || broad_tile_m <= fine_tile_m
        {
            return Err(
                "Underwater shafts require the optical shader and valid photon tiles".into(),
            );
        }
        self.shafts = Some(shafts::Source {
            maps,
            params: Vec4::new(fine_tile_m, broad_tile_m, 0.75, 0.0),
        });
        Ok(self)
    }
}

impl Plugin for TidewaterUnderwaterPlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            SHADER_HANDLE,
            "tidewater_underwater.wgsl",
            Shader::from_wgsl
        );
        let encoding = Encoding::default();
        app.insert_resource(self.source.clone())
            .insert_resource(self.control)
            .insert_resource(encoding.clone())
            .init_resource::<TidewaterUnderwaterObservation>()
            .add_plugins((
                ExtractComponentPlugin::<Settings>::default(),
                UniformComponentPlugin::<Settings>::default(),
                ExtractResourcePlugin::<TidewaterUnderwaterSurfaceMaterial>::default(),
            ))
            .add_systems(Update, attach)
            .add_systems(
                PostUpdate,
                update
                    .after(bevy::camera::CameraUpdateSystems)
                    .after(crate::finite_perspective::EffectivePerspectiveSystems)
                    .after(bevy::transform::TransformSystems::Propagate)
                    .in_set(TidewaterUnderwaterUpdateSet),
            );
        if let Some(shafts) = &self.shafts {
            app.insert_resource(shafts.clone())
                .add_plugins(ExtractResourcePlugin::<shafts::Source>::default());
        }

        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app
            .insert_resource(Packet(self.packet.clone()))
            .insert_resource(encoding)
            .init_resource::<SpecializedRenderPipelines<Pipeline>>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(
                Render,
                shafts::prepare.in_set(RenderSystems::PrepareBindGroups),
            )
            .add_systems(
                Render,
                medium::prepare.in_set(RenderSystems::PrepareBindGroups),
            )
            .add_systems(
                Core3d,
                medium::render
                    .after(SourceHdrSystems::Gtao)
                    .before(SourceHdrSystems::Haze),
            )
            .add_systems(
                Core3d,
                (
                    render::<Without<ResolveEnabled>, false>
                        .after(Core3dSystems::MainPass)
                        .before(Core3dSystems::EarlyPostProcess),
                    render::<With<ResolveEnabled>, true>.in_set(SourceHdrSystems::Underwater),
                ),
            );
        if let Some(surface_shader) = &self.surface_shader {
            render_app.insert_resource(surface_shader.clone());
        }
    }
}

#[derive(Component, Clone, Copy, Debug, ExtractComponent, ShaderType)]
struct Settings {
    camera_world: Mat4,
    /// x: camera height, y: near, z: far, w: lens reach.
    state: Vec4,
    /// x/y: perspective projection scale, z: enabled, w: shaft strength.
    projection: Vec4,
    /// x/y: effective reverse-Z numerator and denominator offset.
    depth_projection: Vec4,
    absorption: Vec4,
    scattering: Vec4,
    sun_direction: Vec4,
    sun_color: Vec4,
    sky_irradiance: Vec4,
}

impl Settings {
    fn disabled(optics: TidewaterUnderwaterOptics) -> Self {
        Self {
            camera_world: Mat4::IDENTITY,
            state: Vec4::new(0.0, 0.1, 60_000.0, LENS_REACH_M),
            projection: Vec4::new(1.0, 1.0, 0.0, 0.0),
            depth_projection: Vec4::new(0.1, 0.0, 0.0, 0.0),
            absorption: optics.absorption.extend(0.0),
            scattering: optics.scattering.extend(0.0),
            sun_direction: optics.sun_direction.normalize().extend(0.0),
            sun_color: optics.sun_color.extend(0.0),
            sky_irradiance: optics.sky_irradiance.extend(0.0),
        }
    }
}

fn attach(
    mut commands: Commands,
    source: Res<Source>,
    cameras: Query<Entity, (With<TidewaterUnderwaterCamera>, Without<Settings>)>,
) {
    for camera in &cameras {
        commands
            .entity(camera)
            .insert(Settings::disabled(source.optics));
    }
}

fn update(
    source: Res<Source>,
    control: Res<TidewaterUnderwaterControl>,
    encoding: Res<Encoding>,
    shafts: Option<Res<shafts::Source>>,
    mut observation: ResMut<TidewaterUnderwaterObservation>,
    mut cameras: Query<
        (&GlobalTransform, &Camera, &Projection, &mut Settings),
        With<TidewaterUnderwaterCamera>,
    >,
) {
    let latest = source.queries.latest();
    let Ok((transform, camera_view, Projection::Perspective(perspective), mut settings)) =
        cameras.single_mut()
    else {
        return;
    };
    let camera = transform.translation();
    let sample = latest
        .as_ref()
        .and_then(|batch| matched_camera_sample(batch, camera));
    let (water_height, sample_step, sample_time) = sample.map_or((0.0, 0, 0.0), |height| {
        let batch = latest.as_ref().expect("matched sample has a batch");
        (height, batch.step, batch.time)
    });
    let fresh = sample.is_some();
    let camera_depth = if fresh {
        (water_height - camera.y).max(0.0)
    } else {
        0.0
    };
    let immersion = classify_immersion(fresh, water_height, camera.y);
    let shafts_strength = if shafts.is_some()
        && control.enabled
        && fresh
        && matches!(
            immersion,
            TidewaterImmersion::SurfaceBand | TidewaterImmersion::FullyImmersed
        )
        && control.shafts_strength.is_finite()
    {
        control.shafts_strength.clamp(0.0, 2.0)
    } else {
        0.0
    };
    let clip = camera_view.clip_from_view();

    settings.camera_world = transform.to_matrix();
    settings.state = Vec4::new(camera.y, perspective.near, perspective.far, LENS_REACH_M);
    settings.projection = Vec4::new(
        clip.x_axis.x,
        clip.y_axis.y,
        if control.enabled { 1.0 } else { 0.0 },
        shafts_strength,
    );
    settings.depth_projection = Vec4::new(clip.w_axis.z, clip.z_axis.z, 0.0, 0.0);
    settings.absorption = source.optics.absorption.extend(0.0);
    settings.scattering = source.optics.scattering.extend(0.0);
    settings.sun_direction = source.optics.sun_direction.normalize().extend(0.0);
    settings.sun_color = source.optics.sun_color.extend(0.0);
    settings.sky_irradiance = source.optics.sky_irradiance.extend(0.0);

    let (encoded_frames, encoded_enabled_frames, encoded_bypassed_frames) = encoding.counts();
    *observation = TidewaterUnderwaterObservation {
        immersion,
        effect_enabled: control.enabled,
        water_height_m: water_height,
        camera_height_m: camera.y,
        camera_depth_m: camera_depth,
        sample_step,
        sample_time_s: sample_time,
        encoded_frames,
        encoded_enabled_frames,
        encoded_bypassed_frames,
        shafts_strength,
        encoded_shaft_frames: encoding.shaft_frames.load(Ordering::Acquire),
    };
}

fn classify_immersion(fresh: bool, water_height: f32, camera_height: f32) -> TidewaterImmersion {
    if !fresh {
        return TidewaterImmersion::Unavailable;
    }
    let relative_height = camera_height - water_height;
    if relative_height.abs() < LENS_REACH_M {
        TidewaterImmersion::SurfaceBand
    } else if relative_height > 0.0 {
        TidewaterImmersion::Air
    } else {
        TidewaterImmersion::FullyImmersed
    }
}

fn matched_camera_sample(batch: &SampleBatch, camera: Vec3) -> Option<f32> {
    let point = Vec2::from_array(*batch.points.first()?);
    let sample = batch.values.first()?;
    (point.distance(camera.xz()) <= CAMERA_SAMPLE_TOLERANCE_M).then_some(sample.height)
}

#[derive(Default)]
struct BindGroupCache {
    cached: Option<(TextureViewId, TextureViewId, TextureViewId, BindGroup)>,
    half: Option<shafts::Target>,
    neutral: Option<shafts::Target>,
    shaft_binding: Option<(TextureViewId, TextureViewId, BindGroup)>,
}

#[allow(clippy::too_many_arguments)] // One per-view render pass needs these Bevy render resources.
#[allow(
    clippy::type_complexity,
    reason = "This per-view render query states the exact Bevy component access contract."
)]
fn render<F: QueryFilter + 'static, const HDR: bool>(
    view: ViewQuery<
        (
            &ViewTarget,
            &ViewDepthTexture,
            &Settings,
            &DynamicUniformIndex<Settings>,
            Option<&ViewPacket>,
        ),
        F,
    >,
    pipeline: Option<Res<Pipeline>>,
    pipeline_cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    packet: Res<Packet>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    surface: Option<Res<TidewaterUnderwaterSurfaceMaterial>>,
    materials: Option<Res<ErasedRenderAssets<PreparedMaterial>>>,
    material_allocators: Option<Res<MaterialBindGroupAllocators>>,
    encoding: Res<Encoding>,
    uniforms: Res<ComponentUniforms<Settings>>,
    shafts: Option<Res<shafts::Prepared>>,
    hdr: Option<Res<HdrObservations>>,
    mut cache: Local<BindGroupCache>,
    mut context: RenderContext,
) {
    let (target, depth, settings, settings_index, frame) = view.into_inner();
    if HDR {
        let (Some(frame), Some(bus)) = (frame, hdr.as_deref()) else {
            return;
        };
        if !bus.ready_for(frame.tag, HdrRole::Underwater)
            || target.main_texture_format() != TextureFormat::Rgba16Float
        {
            return;
        }
    }
    let Some(pipeline) = pipeline else {
        return;
    };
    let pipeline_id = specialized.specialize(
        &pipeline_cache,
        &pipeline,
        (target.main_texture_format(), Pass::Composite),
    );
    let Some(render_pipeline) = pipeline_cache.get_render_pipeline(pipeline_id) else {
        return;
    };
    let surface_group = if pipeline.surface_layout.is_some() {
        let (Some(surface), Some(materials), Some(allocators)) = (
            surface.as_ref(),
            materials.as_ref(),
            material_allocators.as_ref(),
        ) else {
            return;
        };
        let id = surface.0;
        let Some(material) = materials.get(id) else {
            return;
        };
        let Some(allocator) = allocators.get(&id.type_id()) else {
            return;
        };
        let Some(group) = allocator
            .get(material.binding.group)
            .and_then(|slab| slab.bind_group())
        else {
            return;
        };
        Some(group)
    } else {
        None
    };
    let Some(settings_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let Some(packet) = buffers.get(&packet.0) else {
        return;
    };
    if cache.neutral.is_none() {
        cache.neutral = Some(shafts::Target::new(context.render_device(), 1, 1));
    }
    let inputs = RenderInputs {
        pipeline: &pipeline,
        pipeline_cache: &pipeline_cache,
        target,
        depth,
        index: settings_index.index(),
        packet,
        settings_binding,
        surface_group,
    };
    if settings.projection.w > 0.0 {
        let Some(shafts) = shafts.as_ref() else {
            return;
        };
        if !encode_shafts(&inputs, shafts, &mut specialized, &mut cache, &mut context) {
            return;
        }
        encoding.shaft_frames.fetch_add(1, Ordering::AcqRel);
    }
    let half = if settings.projection.w > 0.0 {
        cache.half.as_ref()
    } else {
        cache.neutral.as_ref()
    };
    let Some(half) = half else {
        return;
    };
    let half_view = half.view.clone();
    encode_medium(
        &inputs,
        render_pipeline,
        &half_view,
        &mut cache,
        &mut context,
    );
    encoding.record(settings.projection.z >= 0.5);
    if HDR && let Some((frame, bus)) = frame.zip(hdr.as_deref()) {
        let _ = bus.record_underwater(frame.tag, 1, u32::from(settings.projection.w > 0.0));
    }
}

struct RenderInputs<'a> {
    pipeline: &'a Pipeline,
    pipeline_cache: &'a PipelineCache,
    target: &'a ViewTarget,
    depth: &'a ViewDepthTexture,
    index: u32,
    packet: &'a GpuShaderBuffer,
    settings_binding: BindingResource<'a>,
    surface_group: Option<&'a BindGroup>,
}

fn encode_shafts(
    inputs: &RenderInputs,
    shafts: &shafts::Prepared,
    specialized: &mut SpecializedRenderPipelines<Pipeline>,
    cache: &mut BindGroupCache,
    context: &mut RenderContext,
) -> bool {
    let RenderInputs {
        pipeline,
        pipeline_cache,
        target,
        depth,
        index,
        packet,
        settings_binding,
        surface_group,
        ..
    } = inputs;
    let id = specialized.specialize(
        pipeline_cache,
        pipeline,
        (TextureFormat::Rgba16Float, Pass::Shafts),
    );
    let Some(shaft_pipeline) = pipeline_cache.get_render_pipeline(id) else {
        return false;
    };
    let size = target.main_texture().size();
    let width = size.width.div_ceil(2);
    let height = size.height.div_ceil(2);
    if cache
        .half
        .as_ref()
        .is_none_or(|half| half.width != width || half.height != height)
    {
        cache.half = Some(shafts::Target::new(context.render_device(), width, height));
    }
    let Some(half) = cache.half.as_ref() else {
        return false;
    };
    // The shaft pass binds a distinct neutral texture at its unused read slot.
    // Binding the writable half target there would be an illegal read/write alias.
    let view = half.view.clone();
    let color_id = target.main_texture_view().id();
    let depth_id = depth.view().id();
    let group = match &mut cache.shaft_binding {
        Some((color, depth, group)) if *color == color_id && *depth == depth_id => group,
        cached => {
            let group = context.render_device().create_bind_group(
                "Tidewater shaft depth binding",
                &pipeline_cache.get_bind_group_layout(&pipeline.post_layout),
                &BindGroupEntries::sequential((
                    target.main_texture_view(),
                    &pipeline.sampler,
                    depth.view(),
                    packet.buffer.as_entire_binding(),
                    settings_binding.clone(),
                    &shafts.neutral,
                )),
            );
            &cached.insert((color_id, depth_id, group)).2
        }
    };
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("Tidewater underwater caustic shafts"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: &view,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            ..default()
        });
    pass.set_pipeline(shaft_pipeline);
    if let Some(surface_group) = *surface_group {
        pass.set_bind_group(1, surface_group, &[]);
    }
    pass.set_bind_group(2, group, &[*index]);
    pass.set_bind_group(3, &shafts.group, &[]);
    pass.draw(0..3, 0..1);
    true
}

fn encode_medium(
    inputs: &RenderInputs,
    render_pipeline: &RenderPipeline,
    half_view: &bevy::render::render_resource::TextureView,
    cache: &mut BindGroupCache,
    context: &mut RenderContext,
) {
    let RenderInputs {
        pipeline,
        pipeline_cache,
        target,
        depth,
        index,
        packet,
        settings_binding,
        surface_group,
        ..
    } = inputs;

    let half_id = half_view.id();
    let post = target.post_process_write();
    let color_id = post.source.id();
    let depth_id = depth.view().id();
    let bind_group = match &mut cache.cached {
        Some((cached_color, cached_depth, cached_half, bind_group))
            if *cached_color == color_id
                && *cached_depth == depth_id
                && *cached_half == half_id =>
        {
            bind_group
        }
        cached => {
            let bind_group = context.render_device().create_bind_group(
                "tidewater underwater bind group",
                &pipeline_cache.get_bind_group_layout(&pipeline.post_layout),
                &BindGroupEntries::sequential((
                    post.source,
                    &pipeline.sampler,
                    depth.view(),
                    packet.buffer.as_entire_binding(),
                    settings_binding.clone(),
                    half_view,
                )),
            );
            let (_, _, _, bind_group) = cached.insert((color_id, depth_id, half_id, bind_group));
            bind_group
        }
    };
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater underwater post process"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: post.destination,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            depth_stencil_attachment: None,
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
    pass.set_pipeline(render_pipeline);
    if let Some(surface_group) = *surface_group {
        pass.set_bind_group(1, surface_group, &[]);
    }
    pass.set_bind_group(
        if pipeline.surface_layout.is_some() {
            2
        } else {
            0
        },
        bind_group,
        &[*index],
    );
    pass.draw(0..3, 0..1);
}

#[derive(Resource)]
struct Pipeline {
    post_layout: BindGroupLayoutDescriptor,
    medium_layout: BindGroupLayoutDescriptor,
    surface_layout: Option<BindGroupLayoutDescriptor>,
    surface_shader: Option<Handle<Shader>>,
    sampler: Sampler,
    fullscreen: FullscreenShader,
}

fn init_pipeline(
    mut commands: Commands,
    device: Res<RenderDevice>,
    fullscreen: Res<FullscreenShader>,
    surface_shader: Option<Res<SurfaceShader>>,
) {
    let post_layout = BindGroupLayoutDescriptor::new(
        "tidewater underwater layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                texture_2d(TextureSampleType::Depth),
                storage_buffer_read_only_sized(
                    false,
                    BufferSize::new(crate::tidewater_water_query::PACKET_BYTES as u64),
                ),
                uniform_buffer::<Settings>(true),
                texture_2d(TextureSampleType::Float { filterable: true }),
            ),
        ),
    );
    let sampler = device.create_sampler(&SamplerDescriptor::default());
    commands.insert_resource(Pipeline {
        medium_layout: medium::layout(),
        post_layout,
        surface_layout: surface_shader
            .as_ref()
            .map(|shader| (shader.material_layout)(&device)),
        surface_shader: surface_shader.map(|shader| shader.shader.clone()),
        sampler,
        fullscreen: fullscreen.clone(),
    });
}

#[derive(Clone, Copy, PartialEq, Eq, Hash)]
enum Pass {
    Composite,
    Shafts,
    Medium,
}

impl SpecializedRenderPipeline for Pipeline {
    type Key = (TextureFormat, Pass);

    fn specialize(&self, (format, kind): Self::Key) -> RenderPipelineDescriptor {
        let post_layout = if kind == Pass::Medium {
            &self.medium_layout
        } else {
            &self.post_layout
        };
        let mut layout = if let Some(surface_layout) = &self.surface_layout {
            vec![
                BindGroupLayoutDescriptor::new("unused tidewater view layout", &[]),
                surface_layout.clone(),
                post_layout.clone(),
            ]
        } else {
            vec![post_layout.clone()]
        };
        if kind == Pass::Shafts {
            layout.push(shafts::layout());
        }
        RenderPipelineDescriptor {
            label: Some("tidewater underwater pipeline".into()),
            layout,
            vertex: self.fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader: self.surface_shader.clone().unwrap_or(SHADER_HANDLE),
                entry_point: Some(
                    match kind {
                        Pass::Composite => "fragment",
                        Pass::Shafts => "shaft_fragment",
                        Pass::Medium => "lens_medium_fragment",
                    }
                    .into(),
                ),
                targets: vec![Some(ColorTargetState {
                    format,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                })],
                ..default()
            }),
            ..default()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::tidewater_water_query::Sample;

    fn batch(point: [f32; 2], height: f32) -> SampleBatch {
        SampleBatch {
            step: 7,
            time: 1.25,
            count: 1,
            points: vec![point],
            values: vec![Sample {
                height,
                nx: 0.0,
                nz: 0.0,
                floor: -4.0,
            }],
        }
    }

    #[test]
    fn camera_sample_requires_the_gpu_result_to_follow_the_camera() {
        let sample = batch([12.0, -7.0], 0.4);
        assert_eq!(
            matched_camera_sample(&sample, Vec3::new(12.2, -1.0, -7.1)),
            Some(0.4)
        );
        assert_eq!(
            matched_camera_sample(&sample, Vec3::new(13.0, -1.0, -7.0)),
            None
        );
    }

    #[test]
    fn optical_inputs_reject_negative_energy_and_zero_light_direction() {
        assert!(
            TidewaterUnderwaterOptics::new(
                Vec3::new(-0.1, 0.1, 0.1),
                Vec3::ONE,
                Vec3::Y,
                Vec3::ONE,
                Vec3::ONE,
            )
            .is_err()
        );
        assert!(
            TidewaterUnderwaterOptics::new(Vec3::ONE, Vec3::ONE, Vec3::ZERO, Vec3::ONE, Vec3::ONE,)
                .is_err()
        );
    }

    #[test]
    fn effect_activation_covers_the_surface_band_without_reclassifying_the_camera() {
        let mut observation = TidewaterUnderwaterObservation {
            immersion: TidewaterImmersion::FullyImmersed,
            ..default()
        };
        assert!(!observation.effect_active());

        observation.effect_enabled = true;
        assert!(observation.effect_active());

        observation.immersion = TidewaterImmersion::SurfaceBand;
        assert!(observation.effect_active());
        assert!(!observation.camera_underwater());

        observation.immersion = TidewaterImmersion::FullyImmersed;
        assert!(observation.camera_underwater());
    }

    #[test]
    fn immersion_uses_the_source_straddle_on_both_sides_of_the_surface() {
        assert_eq!(
            classify_immersion(false, 0.0, -1.0),
            TidewaterImmersion::Unavailable
        );
        assert_eq!(
            classify_immersion(true, 0.0, 0.2),
            TidewaterImmersion::SurfaceBand
        );
        assert_eq!(
            classify_immersion(true, 0.0, -0.2),
            TidewaterImmersion::SurfaceBand
        );
        assert_eq!(classify_immersion(true, 0.0, 0.35), TidewaterImmersion::Air);
        assert_eq!(
            classify_immersion(true, 0.0, -0.35),
            TidewaterImmersion::FullyImmersed
        );
    }

    #[test]
    fn encoding_counts_enabled_and_bypassed_draws() {
        let encoding = Encoding::default();
        encoding.record(true);
        encoding.record(false);
        encoding.record(true);
        assert_eq!(encoding.counts(), (3, 2, 1));
    }

    #[test]
    fn runtime_control_defaults_to_the_builder_enabled_state() {
        assert!(TidewaterUnderwaterControl::default().enabled);
    }

    #[test]
    fn combined_surface_shader_keeps_the_near_lens_guard() {
        let optical = "@group(#{MATERIAL_BIND_GROUP})\nfn shoreEvaluateNoNormal() {}\nfn waterSurfaceCascadeAttenuation() {}\n@fragment fn fragment() {}";
        let source = surface_query_shader_source(optical).expect("combined surface shader");
        assert!(source.contains("fn near_lens_in_water("));
        assert!(source.contains("fn lens_medium("));
        assert!(source.contains("return waterQueryHeightAtXZ(xz);"));
        assert!(source.contains("setupUnderwaterHeightFrame();"));
        assert!(!source.contains("if distance(endpoint.xz,"));
        assert!(source.contains("fn setup_lens_medium_frame() { setupUnderwaterHeightFrame(); }"));
        assert!(source.contains("fn lens_medium_fragment("));
    }

    #[test]
    fn mixed_view_shader_parses_and_validates_with_the_bevy_interface_fixture() {
        let source = include_str!("tidewater_underwater.wgsl").replacen(
            "#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput",
            "struct FullscreenVertexOutput {\n    @builtin(position) position: vec4<f32>,\n    @location(0) uv: vec2<f32>,\n}",
            1,
        );
        let module = naga::front::wgsl::parse_str(&source)
            .unwrap_or_else(|error| panic!("{}", error.emit_to_string(&source)));
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::empty(),
        )
        .validate(&module)
        .unwrap_or_else(|error| panic!("{error:?}"));
    }

    #[test]
    fn shaft_entry_and_depth_upsample_validate_together() {
        let post = include_str!("tidewater_underwater.wgsl")
            .replace("#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput",
                "struct FullscreenVertexOutput { @builtin(position) position: vec4f, @location(0) uv: vec2f }")
            .replace("@group(0)", "@group(2)")
            .replace("const PI: f32", "const UNDERWATER_PI: f32")
            .replace("4.0 * PI", "4.0 * UNDERWATER_PI")
            .replace("1.0 / PI", "1.0 / UNDERWATER_PI")
            .replace(" * PI * ", " * UNDERWATER_PI * ");
        // Only the optical interface is stubbed; both real post entry points,
        // photon lookup, resource layouts, march and upsample are validated.
        let fixture = "struct FrameFixture { frameIndex:u32 }; var<private> frame:FrameFixture;\n\
            struct DetailFixture { gust:f32, slick:f32 };\n\
            fn setupUnderwaterHeightFrame() { frame.frameIndex = 0u; }\n\
            fn seaDetailSample(xz:vec2f)->DetailFixture { return DetailFixture(1.0, 0.0); }";
        let source = format!(
            "{post}\n{fixture}\n{}",
            include_str!("tidewater_underwater_shafts.wgsl")
        );
        let module = naga::front::wgsl::parse_str(&source)
            .unwrap_or_else(|error| panic!("{}", error.emit_to_string(&source)));
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::empty(),
        )
        .validate(&module)
        .unwrap_or_else(|error| panic!("{error:?}"));
    }
}
