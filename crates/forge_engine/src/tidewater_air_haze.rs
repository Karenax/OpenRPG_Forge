//! Source-derived aerial perspective for the native Tidewater scene.
#![allow(clippy::needless_pass_by_value)] // Bevy render-system parameters are owned wrappers.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/post/AirHaze.js` and `src/sky/Atmosphere.js`,
// copyright 2026 DRG Software Solutions LLC. Upstream 1438b1ab.

use bevy::{
    asset::{load_internal_asset, uuid_handle},
    core_pipeline::{Core3dSystems, FullscreenShader, schedule::Core3d, upscaling::upscaling},
    ecs::query::QueryFilter,
    prelude::*,
    render::{
        Render, RenderApp, RenderStartup, RenderSystems,
        extract_component::{
            ComponentUniforms, DynamicUniformIndex, ExtractComponent, ExtractComponentPlugin,
            UniformComponentPlugin,
        },
        extract_resource::ExtractResourcePlugin,
        render_asset::RenderAssets,
        render_resource::{
            BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, ColorTargetState,
            ColorWrites, FragmentState, Operations, PipelineCache, RenderPassColorAttachment,
            RenderPassDescriptor, RenderPipelineDescriptor, Sampler, SamplerDescriptor,
            ShaderStages, ShaderType, SpecializedRenderPipeline, SpecializedRenderPipelines,
            TextureFormat, TextureSampleType,
            binding_types::{sampler, texture_2d, texture_depth_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, ViewQuery},
        texture::GpuImage,
        view::{ViewDepthTexture, ViewTarget},
    },
    shader::Shader,
    ui_render::render_pass::ui_pass,
};

use crate::atmospheric_haze::AirHazeSettings;
use crate::tidewater_temporal::{
    frame::ViewPacket,
    hdr::{HdrObservations, HdrRole, SourceHdrSystems},
    resolve::ResolveEnabled,
};
use crate::{tidewater_atmosphere, tidewater_underwater::TidewaterUnderwaterObservation};

const SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa109f");
const CORE_SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa109e");
const VOLUME_SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa10a0");
const MARCH_SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa10a1");
const RADIAL_SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa10a2");
const CLOUD_VIEW_SHADER_HANDLE: Handle<Shader> =
    uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa10a3");
const MASK_SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa10a4");
const COMPOSITE_SHADER_HANDLE: Handle<Shader> =
    uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa10a5");

mod clock;
pub mod lighting;
pub mod moon;
pub mod passes;
mod render_volume;
pub mod scene;
pub mod targets;

pub struct TidewaterAirHazePlugin;

/// Orders consumers of the completed haze image without exposing render internals.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct TidewaterAirHazePass;

impl Plugin for TidewaterAirHazePlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            CORE_SHADER_HANDLE,
            "tidewater_air_haze_core.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            SHADER_HANDLE,
            "tidewater_air_haze.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            VOLUME_SHADER_HANDLE,
            "tidewater_air_haze_volume.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            MARCH_SHADER_HANDLE,
            "tidewater_air_haze_march.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            RADIAL_SHADER_HANDLE,
            "tidewater_air_haze_radial.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            CLOUD_VIEW_SHADER_HANDLE,
            "tidewater_air_haze_cloud_view.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            MASK_SHADER_HANDLE,
            "tidewater_air_haze_mask.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            COMPOSITE_SHADER_HANDLE,
            "tidewater_air_haze_composite.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<TidewaterAirHaze>::default(),
            ExtractComponentPlugin::<AirHazeSettings>::default(),
            ExtractComponentPlugin::<clock::Clock>::default(),
            UniformComponentPlugin::<TidewaterAirHaze>::default(),
            ExtractResourcePlugin::<crate::atmospheric_haze::scene::Inputs>::default(),
            ExtractResourcePlugin::<moon::Inputs>::default(),
        ))
        .init_resource::<crate::atmospheric_haze::scene::Inputs>()
        .init_resource::<moon::Inputs>()
        .add_systems(PostUpdate, scene::refresh)
        .add_systems(PostUpdate, moon::refresh)
        .add_systems(PostUpdate, clock::update)
        .add_systems(
            PostUpdate,
            update
                .after(bevy::camera::CameraUpdateSystems)
                .after(crate::finite_perspective::EffectivePerspectiveSystems)
                .after(bevy::transform::TransformSystems::Propagate)
                .after(crate::tidewater_underwater::TidewaterUnderwaterUpdateSet),
        );
        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app
            .init_resource::<SpecializedRenderPipelines<Pipeline>>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(RenderStartup, passes::init)
            .add_systems(RenderStartup, render_volume::init)
            .add_systems(
                Render,
                targets::prepare.in_set(RenderSystems::PrepareBindGroups),
            )
            .add_systems(
                Core3d,
                render::<false, Without<ResolveEnabled>>
                    .in_set(TidewaterAirHazePass)
                    .after(Core3dSystems::PostProcess)
                    .before(ui_pass)
                    .before(upscaling),
            )
            .add_systems(
                Core3d,
                render_volume::render
                    .in_set(SourceHdrSystems::Haze)
                    .in_set(TidewaterAirHazePass),
            );
    }
}

#[derive(Component, Clone, Copy, Debug, ExtractComponent, ShaderType)]
#[require(AirHazeSettings)]
pub struct TidewaterAirHaze {
    camera_world: Mat4,
    /// x/y: effective projection scale; z: camera height above sea; w: depth numerator.
    projection: Vec4,
    /// x: density; y: camera in air; z: diagnostic; w: depth denominator offset.
    control: Vec4,
    atmosphere: [Vec4; 4],
}

impl Default for TidewaterAirHaze {
    fn default() -> Self {
        Self {
            camera_world: Mat4::IDENTITY,
            projection: Vec4::new(1.0, 1.0, 0.0, 0.1),
            control: Vec4::new(1.6, 1.0, 0.0, 0.0),
            atmosphere: [Vec4::ZERO; 4],
        }
    }
}

fn update(
    atmosphere: Option<Res<tidewater_atmosphere::Targets>>,
    immersion: Option<Res<TidewaterUnderwaterObservation>>,
    mut views: Query<(
        &GlobalTransform,
        &Camera,
        &Projection,
        &AirHazeSettings,
        &mut TidewaterAirHaze,
    )>,
) {
    let Some(atmosphere) = atmosphere else { return };
    let camera_in_air = immersion.as_ref().is_none_or(|state| {
        !matches!(
            state.immersion,
            crate::tidewater_underwater::TidewaterImmersion::FullyImmersed
        )
    });
    for (transform, camera, projection, settings, mut haze) in &mut views {
        let Projection::Perspective(_) = projection else {
            continue;
        };
        let clip = camera.clip_from_view();
        haze.camera_world = transform.to_matrix();
        haze.projection = Vec4::new(
            clip.x_axis.x,
            clip.y_axis.y,
            transform.translation().y.max(0.0),
            clip.w_axis.z,
        );
        haze.control.w = clip.z_axis.z;
        haze.control.x = if settings.enabled {
            settings.density()
        } else {
            0.0
        };
        haze.control.y = if camera_in_air { 1.0 } else { 0.0 };
        haze.control.z = if std::env::var_os("FORGE_TIDEWATER_HAZE_DEBUG_DEPTH").is_some() {
            1.0
        } else {
            0.0
        };
        haze.atmosphere = atmosphere.params;
    }
}

#[derive(Resource)]
pub(crate) struct Pipeline {
    layout: BindGroupLayoutDescriptor,
    fullscreen: FullscreenShader,
    sampler: Sampler,
}

fn init_pipeline(
    mut commands: Commands,
    fullscreen: Res<FullscreenShader>,
    device: Res<RenderDevice>,
) {
    let layout = BindGroupLayoutDescriptor::new(
        "tidewater air haze layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                texture_depth_2d(),
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(bevy::render::render_resource::SamplerBindingType::Filtering),
                uniform_buffer::<TidewaterAirHaze>(true),
            ),
        ),
    );
    commands.insert_resource(Pipeline {
        layout,
        fullscreen: fullscreen.clone(),
        sampler: device.create_sampler(&SamplerDescriptor {
            mag_filter: bevy::render::render_resource::FilterMode::Linear,
            min_filter: bevy::render::render_resource::FilterMode::Linear,
            ..default()
        }),
    });
}

impl SpecializedRenderPipeline for Pipeline {
    type Key = (TextureFormat, bool);

    fn specialize(&self, (format, hdr): Self::Key) -> RenderPipelineDescriptor {
        RenderPipelineDescriptor {
            label: Some("tidewater air haze pipeline".into()),
            layout: vec![self.layout.clone()],
            vertex: self.fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader: SHADER_HANDLE,
                entry_point: Some("fragment".into()),
                shader_defs: if hdr {
                    vec!["TIDEWATER_SOURCE_HDR".into()]
                } else {
                    Vec::new()
                },
                targets: vec![Some(ColorTargetState {
                    format,
                    blend: None,
                    write_mask: ColorWrites::ALL,
                })],
            }),
            ..default()
        }
    }
}

#[allow(clippy::too_many_arguments)] // A native per-view pass needs its GPU resources.
#[allow(
    clippy::type_complexity,
    reason = "This per-view render query states the exact Bevy component access contract."
)]
pub(crate) fn render<const HDR: bool, F: QueryFilter + 'static>(
    view: ViewQuery<
        (
            &ViewTarget,
            &ViewDepthTexture,
            &TidewaterAirHaze,
            &DynamicUniformIndex<TidewaterAirHaze>,
            Option<&ViewPacket>,
        ),
        F,
    >,
    pipeline: Option<Res<Pipeline>>,
    pipeline_cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    atmosphere: Option<Res<tidewater_atmosphere::Targets>>,
    images: Res<RenderAssets<GpuImage>>,
    uniforms: Res<ComponentUniforms<TidewaterAirHaze>>,
    hdr: Option<Res<HdrObservations>>,
    mut reported: Local<bool>,
    mut context: RenderContext,
) {
    let (target, depth, haze, haze_index, frame) = view.into_inner();
    if HDR {
        let (Some(frame), Some(bus)) = (frame, hdr.as_deref()) else {
            return;
        };
        if !bus.ready_for(frame.tag, HdrRole::Haze) {
            return;
        }
        if haze.control.y <= 0.0 {
            let _ = bus.record_neutral_haze(frame.tag);
            return;
        }
        if target.main_texture_format() != TextureFormat::Rgba16Float {
            return;
        }
    }
    let (Some(pipeline), Some(atmosphere)) = (pipeline, atmosphere) else {
        return;
    };
    if haze.control.y <= 0.0 {
        return;
    }
    let Some(sky) = images.get(&atmosphere.images[2]) else {
        return;
    };
    let Some(uniform_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let id = specialized.specialize(
        &pipeline_cache,
        &pipeline,
        (target.main_texture_format(), HDR),
    );
    let Some(render_pipeline) = pipeline_cache.get_render_pipeline(id) else {
        return;
    };
    let post = target.post_process_write();
    let group = context.render_device().create_bind_group(
        "tidewater air haze bind group",
        &pipeline_cache.get_bind_group_layout(&pipeline.layout),
        &BindGroupEntries::sequential((
            post.source,
            depth.view(),
            &sky.texture_view,
            &pipeline.sampler,
            uniform_binding,
        )),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater air haze post process"),
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
    pass.set_bind_group(0, &group, &[haze_index.index()]);
    pass.draw(0..3, 0..1);
    if HDR && let Some((frame, bus)) = frame.zip(hdr.as_deref()) {
        let _ = bus.record(frame.tag, HdrRole::Haze, 1);
    }
    if !*reported && std::env::var_os("FORGE_TIDEWATER_HAZE_DIAGNOSTIC").is_some() {
        info!(
            "native Tidewater air haze encoded: density={} camera_air={} camera_height={} depth_debug={}",
            haze.control.x, haze.control.y, haze.projection.z, haze.control.z
        );
        *reported = true;
    }
}
