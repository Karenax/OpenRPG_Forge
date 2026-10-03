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
        RenderApp, RenderStartup,
        extract_component::{
            ComponentUniforms, DynamicUniformIndex, ExtractComponent, ExtractComponentPlugin,
            UniformComponentPlugin,
        },
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

use crate::tidewater_temporal::{
    frame::ViewPacket,
    hdr::{HdrObservations, HdrRole, SourceHdrSystems},
    resolve::ResolveEnabled,
};
use crate::{tidewater_atmosphere, tidewater_underwater::TidewaterUnderwaterObservation};

const SHADER_HANDLE: Handle<Shader> = uuid_handle!("b9056f9a-02e2-4edc-bf75-511e66aa109f");

pub struct TidewaterAirHazePlugin;

impl Plugin for TidewaterAirHazePlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            SHADER_HANDLE,
            "tidewater_air_haze.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<TidewaterAirHaze>::default(),
            UniformComponentPlugin::<TidewaterAirHaze>::default(),
        ))
        .add_systems(
            PostUpdate,
            update
                .after(bevy::transform::TransformSystems::Propagate)
                .after(crate::tidewater_underwater::TidewaterUnderwaterUpdateSet),
        );
        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app
            .init_resource::<SpecializedRenderPipelines<Pipeline>>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(
                Core3d,
                render::<false, Without<ResolveEnabled>>
                    .after(Core3dSystems::PostProcess)
                    .before(ui_pass)
                    .before(upscaling),
            )
            .add_systems(
                Core3d,
                render::<true, With<ResolveEnabled>>.in_set(SourceHdrSystems::Haze),
            );
    }
}

#[derive(Component, Clone, Copy, Debug, ExtractComponent, ShaderType)]
pub struct TidewaterAirHaze {
    camera_world: Mat4,
    /// x/y: projection scale; z: camera height above sea; w: near plane.
    projection: Vec4,
    /// x: density (source default 1.6); y: camera in air.
    control: Vec4,
    atmosphere: [Vec4; 4],
}

impl TidewaterAirHaze {
    #[must_use]
    pub fn new(density: f32) -> Self {
        Self {
            camera_world: Mat4::IDENTITY,
            projection: Vec4::new(1.0, 1.0, 0.0, 0.1),
            control: Vec4::new(density, 1.0, 0.0, 0.0),
            atmosphere: [Vec4::ZERO; 4],
        }
    }

    /// Change the aerial-perspective density without resetting camera data.
    pub fn set_density(&mut self, density: f32) {
        self.control.x = density.clamp(0.0, 4.0);
    }

    /// Current density supplied to the atmosphere post pass.
    #[must_use]
    pub fn density(&self) -> f32 {
        self.control.x
    }
}

fn update(
    atmosphere: Option<Res<tidewater_atmosphere::Targets>>,
    immersion: Option<Res<TidewaterUnderwaterObservation>>,
    mut views: Query<(&GlobalTransform, &Projection, &mut TidewaterAirHaze)>,
) {
    let Some(atmosphere) = atmosphere else { return };
    let camera_in_air = immersion.as_ref().is_none_or(|state| {
        !matches!(
            state.immersion,
            crate::tidewater_underwater::TidewaterImmersion::FullyImmersed
        )
    });
    for (transform, projection, mut haze) in &mut views {
        let Projection::Perspective(perspective) = projection else {
            continue;
        };
        let y = 1.0 / (perspective.fov * 0.5).tan();
        haze.camera_world = transform.to_matrix();
        haze.projection = Vec4::new(
            y / perspective.aspect_ratio,
            y,
            transform.translation().y.max(0.0),
            perspective.near,
        );
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
