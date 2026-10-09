//! Source-derived screen-space ambient occlusion for the Tidewater camera.
#![allow(clippy::needless_pass_by_value)] // Bevy render-system parameters are owned wrappers.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/post/GTAO.js` and `src/post/PostFX.js`,
// copyright 2026 DRG Software Solutions LLC. Upstream 1438b1ab.

use crate::tidewater_temporal::{
    frame::ViewPacket,
    hdr::{HdrObservations, HdrRole, SourceHdrSystems},
    resolve::ResolveEnabled,
};
use bevy::{
    anti_alias::contrast_adaptive_sharpening::cas,
    asset::{load_internal_asset, uuid_handle},
    core_pipeline::{Core3dSystems, FullscreenShader, schedule::Core3d, tonemapping::tonemapping},
    ecs::query::QueryFilter,
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        camera::ExtractedCamera,
        extract_component::{
            ComponentUniforms, DynamicUniformIndex, ExtractComponent, ExtractComponentPlugin,
            UniformComponentPlugin,
        },
        render_resource::{
            BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, ColorTargetState,
            ColorWrites, Extent3d, FragmentState, Operations, PipelineCache,
            RenderPassColorAttachment, RenderPassDescriptor, RenderPipelineDescriptor,
            ShaderStages, ShaderType, SpecializedRenderPipeline, SpecializedRenderPipelines,
            TextureDescriptor, TextureDimension, TextureFormat, TextureSampleType, TextureUsages,
            binding_types::{texture_2d, texture_depth_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, ViewQuery},
        texture::{CachedTexture, TextureCache},
        view::{ViewDepthTexture, ViewTarget},
    },
    shader::Shader,
};

const AO_SHADER: Handle<Shader> = uuid_handle!("218197e1-3572-4d2d-85b2-8423969a5a03");
const COMPOSITE_SHADER: Handle<Shader> = uuid_handle!("a70d1a89-51dd-4bd8-83e9-72ee7e0b4c51");
const DEPTH_SHADER: Handle<Shader> = uuid_handle!("ff218a28-87a4-4fc5-a81b-e64b41b313ef");
const BLUR_SHADER: Handle<Shader> = uuid_handle!("1db54d81-6cea-47be-9f2f-79abbb448e16");

pub struct TidewaterGtaoPlugin;

#[derive(SystemSet, Debug, Clone, PartialEq, Eq, Hash)]
pub(crate) struct GtaoCompositePass;

impl Plugin for TidewaterGtaoPlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(app, AO_SHADER, "tidewater_gtao.wgsl", Shader::from_wgsl);
        load_internal_asset!(
            app,
            DEPTH_SHADER,
            "tidewater_gtao_depth.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            BLUR_SHADER,
            "tidewater_gtao_blur.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            COMPOSITE_SHADER,
            "tidewater_gtao_composite.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<TidewaterGtao>::default(),
            UniformComponentPlugin::<TidewaterGtao>::default(),
        ))
        .add_systems(
            PostUpdate,
            update
                .after(bevy::camera::CameraUpdateSystems)
                .after(crate::finite_perspective::EffectivePerspectiveSystems),
        );
        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app
            .init_resource::<SpecializedRenderPipelines<Pipeline>>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(
                Render,
                prepare_target.in_set(RenderSystems::PrepareResources),
            )
            .add_systems(
                Core3d,
                (
                    (
                        render_depth::<Without<ResolveEnabled>, false>,
                        render_ao::<Without<ResolveEnabled>, false>,
                        render_blur_x::<Without<ResolveEnabled>, false>,
                        render_blur_y::<Without<ResolveEnabled>, false>,
                    )
                        .chain()
                        .in_set(Core3dSystems::EarlyPostProcess),
                    composite::<Without<ResolveEnabled>, false>
                        // All ViewTarget ping-pong writers need a fixed order.
                        // CAS follows FXAA in Bevy; AO follows both.
                        .after(cas)
                        .after(tonemapping)
                        .before(crate::tidewater_air_haze::render::<false, Without<ResolveEnabled>>)
                        .in_set(GtaoCompositePass)
                        .in_set(Core3dSystems::PostProcess),
                    (
                        crate::tidewater_temporal::hdr::begin_selected,
                        render_depth::<With<ResolveEnabled>, true>,
                        render_ao::<With<ResolveEnabled>, true>,
                        render_blur_x::<With<ResolveEnabled>, true>,
                        render_blur_y::<With<ResolveEnabled>, true>,
                        composite::<With<ResolveEnabled>, true>,
                    )
                        .chain()
                        .in_set(SourceHdrSystems::Gtao)
                        .after(Core3dSystems::MainPass)
                        .before(Core3dSystems::EarlyPostProcess),
                ),
            );
    }
}

#[derive(Component, Clone, Copy, Debug, ExtractComponent, ShaderType)]
pub struct TidewaterGtao {
    clip_from_view: Mat4,
    view_from_clip: Mat4,
    /// x: strength, y: world radius, z: thickness, w: GTAO exponent.
    control: Vec4,
    /// x: diagnostic AO mask output.
    options: Vec4,
}

impl TidewaterGtao {
    #[must_use]
    pub fn new(strength: f32) -> Self {
        Self {
            clip_from_view: Mat4::IDENTITY,
            view_from_clip: Mat4::IDENTITY,
            control: Vec4::new(strength, 2.2, 2.0, 1.6),
            options: Vec4::ZERO,
        }
    }

    /// Change the ambient occlusion strength without resetting camera matrices.
    pub fn set_strength(&mut self, strength: f32) {
        self.control.x = strength.clamp(0.0, 2.0);
    }

    /// Current ambient occlusion strength used by the GPU uniform.
    #[must_use]
    pub fn strength(&self) -> f32 {
        self.control.x
    }
}

fn update(mut views: Query<(&Camera, &mut TidewaterGtao)>) {
    for (camera, mut ao) in &mut views {
        let clip = camera.clip_from_view();
        ao.clip_from_view = clip;
        ao.view_from_clip = clip.inverse();
        ao.options.x = if std::env::var_os("FORGE_TIDEWATER_AO_DEBUG").is_some() {
            1.0
        } else {
            0.0
        };
    }
}

#[derive(Component)]
struct AoTarget {
    depth: CachedTexture,
    raw: CachedTexture,
    blur_x: CachedTexture,
    blur_y: CachedTexture,
}

fn prepare_target(
    mut commands: Commands,
    mut cache: ResMut<TextureCache>,
    device: Res<RenderDevice>,
    views: Query<(Entity, &ExtractedCamera), With<TidewaterGtao>>,
    mut reported: Local<u8>,
) {
    let count = views.iter().count();
    if std::env::var_os("FORGE_TIDEWATER_AO_DIAGNOSTIC").is_some()
        && (*reported == 0 || (*reported == 1 && count > 0))
    {
        info!("Tidewater GTAO prepare: extracted_views={count}");
        *reported = if count > 0 { 2 } else { 1 };
    }
    for (entity, camera) in &views {
        let Some(size) = camera.physical_viewport_size else {
            continue;
        };
        let extent = Extent3d {
            width: size.x.div_ceil(2).max(1),
            height: size.y.div_ceil(2).max(1),
            depth_or_array_layers: 1,
        };
        let mut texture = |label, format| {
            cache.get(
                &device,
                TextureDescriptor {
                    label: Some(label),
                    size: extent,
                    mip_level_count: 1,
                    sample_count: 1,
                    dimension: TextureDimension::D2,
                    format,
                    usage: TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING,
                    view_formats: &[],
                },
            )
        };
        commands.entity(entity).insert(AoTarget {
            depth: texture("tidewater gtao half depth", TextureFormat::R32Float),
            raw: texture("tidewater gtao raw", TextureFormat::R8Unorm),
            blur_x: texture("tidewater gtao blur x", TextureFormat::R16Float),
            blur_y: texture("tidewater gtao blur y", TextureFormat::R16Float),
        });
    }
}

#[derive(Resource)]
struct Pipeline {
    depth_layout: BindGroupLayoutDescriptor,
    ao_layout: BindGroupLayoutDescriptor,
    blur_layout: BindGroupLayoutDescriptor,
    composite_layout: BindGroupLayoutDescriptor,
    fullscreen: FullscreenShader,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
enum PassKey {
    Depth,
    Ao,
    BlurX,
    BlurY,
    Composite(TextureFormat),
}

fn init_pipeline(mut commands: Commands, fullscreen: Res<FullscreenShader>) {
    commands.insert_resource(Pipeline {
        depth_layout: BindGroupLayoutDescriptor::new(
            "tidewater gtao depth layout",
            &BindGroupLayoutEntries::sequential(ShaderStages::FRAGMENT, (texture_depth_2d(),)),
        ),
        ao_layout: BindGroupLayoutDescriptor::new(
            "tidewater gtao layout",
            &BindGroupLayoutEntries::sequential(
                ShaderStages::FRAGMENT,
                (
                    texture_2d(TextureSampleType::Float { filterable: false }),
                    uniform_buffer::<TidewaterGtao>(true),
                ),
            ),
        ),
        blur_layout: BindGroupLayoutDescriptor::new(
            "tidewater gtao blur layout",
            &BindGroupLayoutEntries::sequential(
                ShaderStages::FRAGMENT,
                (
                    texture_2d(TextureSampleType::Float { filterable: true }),
                    texture_2d(TextureSampleType::Float { filterable: false }),
                ),
            ),
        ),
        composite_layout: BindGroupLayoutDescriptor::new(
            "tidewater gtao composite layout",
            &BindGroupLayoutEntries::sequential(
                ShaderStages::FRAGMENT,
                (
                    texture_2d(TextureSampleType::Float { filterable: true }),
                    texture_depth_2d(),
                    texture_2d(TextureSampleType::Float { filterable: true }),
                    texture_2d(TextureSampleType::Float { filterable: false }),
                    uniform_buffer::<TidewaterGtao>(true),
                ),
            ),
        ),
        fullscreen: fullscreen.clone(),
    });
}

impl SpecializedRenderPipeline for Pipeline {
    type Key = PassKey;

    fn specialize(&self, key: Self::Key) -> RenderPipelineDescriptor {
        let (layout, shader, entry_point, format) = match key {
            PassKey::Depth => (
                &self.depth_layout,
                &DEPTH_SHADER,
                "half_depth",
                TextureFormat::R32Float,
            ),
            PassKey::Ao => (&self.ao_layout, &AO_SHADER, "ao", TextureFormat::R8Unorm),
            PassKey::BlurX => (
                &self.blur_layout,
                &BLUR_SHADER,
                "blur_x",
                TextureFormat::R16Float,
            ),
            PassKey::BlurY => (
                &self.blur_layout,
                &BLUR_SHADER,
                "blur_y",
                TextureFormat::R16Float,
            ),
            PassKey::Composite(format) => (
                &self.composite_layout,
                &COMPOSITE_SHADER,
                "composite",
                format,
            ),
        };
        RenderPipelineDescriptor {
            label: Some("tidewater gtao pipeline".into()),
            layout: vec![layout.clone()],
            vertex: self.fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader: shader.clone(),
                entry_point: Some(entry_point.into()),
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

fn render_depth<F: QueryFilter + 'static, const HDR: bool>(
    view: ViewQuery<(&ViewDepthTexture, &AoTarget, Option<&ViewPacket>), F>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    hdr: Option<Res<HdrObservations>>,
    mut context: RenderContext,
) {
    let (depth, ao, frame) = view.into_inner();
    if HDR && !ready(frame, hdr.as_deref(), HdrRole::GtaoDepth) {
        return;
    }
    let Some(pipeline) = pipeline else { return };
    let id = specialized.specialize(&cache, &pipeline, PassKey::Depth);
    let Some(render_pipeline) = cache.get_render_pipeline(id) else {
        return;
    };
    let group = context.render_device().create_bind_group(
        "tidewater gtao depth bind group",
        &cache.get_bind_group_layout(&pipeline.depth_layout),
        &BindGroupEntries::sequential((depth.view(),)),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater gtao half depth"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: &ao.depth.default_view,
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
    pass.set_bind_group(0, &group, &[]);
    pass.draw(0..3, 0..1);
    if HDR {
        record(frame, hdr.as_deref(), HdrRole::GtaoDepth);
    }
}

#[allow(
    clippy::too_many_arguments,
    reason = "The Bevy render pass receives its independent GPU system parameters."
)]
fn render_ao<F: QueryFilter + 'static, const HDR: bool>(
    view: ViewQuery<
        (
            &AoTarget,
            &DynamicUniformIndex<TidewaterGtao>,
            Option<&ViewPacket>,
        ),
        F,
    >,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    uniforms: Res<ComponentUniforms<TidewaterGtao>>,
    hdr: Option<Res<HdrObservations>>,
    mut reported: Local<bool>,
    mut context: RenderContext,
) {
    let (ao, index, frame) = view.into_inner();
    if HDR && !ready(frame, hdr.as_deref(), HdrRole::GtaoRaw) {
        return;
    }
    let Some(pipeline) = pipeline else { return };
    let Some(uniform_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let id = specialized.specialize(&cache, &pipeline, PassKey::Ao);
    let Some(render_pipeline) = cache.get_render_pipeline(id) else {
        return;
    };
    let group = context.render_device().create_bind_group(
        "tidewater gtao bind group",
        &cache.get_bind_group_layout(&pipeline.ao_layout),
        &BindGroupEntries::sequential((&ao.depth.default_view, uniform_binding)),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater gtao half-resolution"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: &ao.raw.default_view,
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
    pass.set_bind_group(0, &group, &[index.index()]);
    pass.draw(0..3, 0..1);
    if HDR {
        record(frame, hdr.as_deref(), HdrRole::GtaoRaw);
    }
    if !*reported && std::env::var_os("FORGE_TIDEWATER_AO_DIAGNOSTIC").is_some() {
        info!("Tidewater GTAO render encoded");
        *reported = true;
    }
}

fn render_blur_x<F: QueryFilter + 'static, const HDR: bool>(
    view: ViewQuery<(&AoTarget, Option<&ViewPacket>), F>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    hdr: Option<Res<HdrObservations>>,
    mut context: RenderContext,
) {
    let (ao, frame) = view.into_inner();
    if HDR && !ready(frame, hdr.as_deref(), HdrRole::GtaoBlurX) {
        return;
    }
    let Some(pipeline) = pipeline else { return };
    let encoded = draw_blur(
        &pipeline,
        &cache,
        &mut specialized,
        &mut context,
        &ao.raw.default_view,
        &ao.depth.default_view,
        &ao.blur_x.default_view,
        PassKey::BlurX,
    );
    if HDR && encoded {
        record(frame, hdr.as_deref(), HdrRole::GtaoBlurX);
    }
}

fn render_blur_y<F: QueryFilter + 'static, const HDR: bool>(
    view: ViewQuery<(&AoTarget, Option<&ViewPacket>), F>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    hdr: Option<Res<HdrObservations>>,
    mut context: RenderContext,
) {
    let (ao, frame) = view.into_inner();
    if HDR && !ready(frame, hdr.as_deref(), HdrRole::GtaoBlurY) {
        return;
    }
    let Some(pipeline) = pipeline else { return };
    let encoded = draw_blur(
        &pipeline,
        &cache,
        &mut specialized,
        &mut context,
        &ao.blur_x.default_view,
        &ao.depth.default_view,
        &ao.blur_y.default_view,
        PassKey::BlurY,
    );
    if HDR && encoded {
        record(frame, hdr.as_deref(), HdrRole::GtaoBlurY);
    }
}

#[allow(clippy::too_many_arguments)] // Inputs and output are explicit to prevent texture aliasing.
fn draw_blur(
    pipeline: &Pipeline,
    cache: &PipelineCache,
    specialized: &mut SpecializedRenderPipelines<Pipeline>,
    context: &mut RenderContext,
    source: &bevy::render::render_resource::TextureView,
    depth: &bevy::render::render_resource::TextureView,
    destination: &bevy::render::render_resource::TextureView,
    key: PassKey,
) -> bool {
    let id = specialized.specialize(cache, pipeline, key);
    let Some(render_pipeline) = cache.get_render_pipeline(id) else {
        return false;
    };
    let group = context.render_device().create_bind_group(
        "tidewater gtao blur bind group",
        &cache.get_bind_group_layout(&pipeline.blur_layout),
        &BindGroupEntries::sequential((source, depth)),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater gtao bilateral blur"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: destination,
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
    pass.set_bind_group(0, &group, &[]);
    pass.draw(0..3, 0..1);
    true
}

#[allow(clippy::too_many_arguments)] // A fullscreen per-view pass needs its GPU resources.
#[allow(
    clippy::type_complexity,
    reason = "This per-view render query states the exact Bevy component access contract."
)]
fn composite<F: QueryFilter + 'static, const HDR: bool>(
    view: ViewQuery<
        (
            &ViewTarget,
            &ViewDepthTexture,
            &AoTarget,
            &DynamicUniformIndex<TidewaterGtao>,
            Option<&ViewPacket>,
        ),
        F,
    >,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    uniforms: Res<ComponentUniforms<TidewaterGtao>>,
    hdr: Option<Res<HdrObservations>>,
    mut reported: Local<bool>,
    mut context: RenderContext,
) {
    let (target, depth, ao, index, frame) = view.into_inner();
    if HDR && !ready(frame, hdr.as_deref(), HdrRole::GtaoComposite) {
        return;
    }
    if HDR && target.main_texture_format() != TextureFormat::Rgba16Float {
        if let Some((frame, bus)) = frame.zip(hdr.as_deref()) {
            bus.reject(
                frame.tag,
                HdrRole::GtaoComposite,
                "selected HDR target must be RGBA16Float",
            );
        }
        return;
    }
    let Some(pipeline) = pipeline else { return };
    let Some(uniform_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let id = specialized.specialize(
        &cache,
        &pipeline,
        PassKey::Composite(target.main_texture_format()),
    );
    let Some(render_pipeline) = cache.get_render_pipeline(id) else {
        return;
    };
    let post = target.post_process_write();
    let group = context.render_device().create_bind_group(
        "tidewater gtao composite bind group",
        &cache.get_bind_group_layout(&pipeline.composite_layout),
        &BindGroupEntries::sequential((
            post.source,
            depth.view(),
            &ao.blur_y.default_view,
            &ao.depth.default_view,
            uniform_binding,
        )),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater gtao composite"),
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
    pass.set_bind_group(0, &group, &[index.index()]);
    pass.draw(0..3, 0..1);
    if HDR {
        record(frame, hdr.as_deref(), HdrRole::GtaoComposite);
    }
    if !*reported && std::env::var_os("FORGE_TIDEWATER_AO_DIAGNOSTIC").is_some() {
        info!("Tidewater GTAO composite encoded");
        *reported = true;
    }
}

fn ready(frame: Option<&ViewPacket>, bus: Option<&HdrObservations>, role: HdrRole) -> bool {
    frame
        .zip(bus)
        .is_some_and(|(frame, bus)| bus.ready_for(frame.tag, role))
}

fn record(frame: Option<&ViewPacket>, bus: Option<&HdrObservations>, role: HdrRole) {
    if let Some((frame, bus)) = frame.zip(bus) {
        let _ = bus.record(frame.tag, role, 1);
    }
}
