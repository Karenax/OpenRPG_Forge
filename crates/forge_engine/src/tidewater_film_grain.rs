//! Tidewater film grain after native tonemapping.
#![allow(clippy::needless_pass_by_value)] // Bevy render-system parameters are owned wrappers.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/post/PostFX.js`, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see `third_party/tidewater/LICENSE`.

use bevy::{
    asset::{load_internal_asset, uuid_handle},
    core_pipeline::{Core3dSystems, FullscreenShader, schedule::Core3d, tonemapping::tonemapping},
    prelude::*,
    render::{
        RenderApp, RenderStartup,
        extract_component::{
            ComponentUniforms, DynamicUniformIndex, ExtractComponent, ExtractComponentPlugin,
            UniformComponentPlugin,
        },
        render_resource::{
            BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, ColorTargetState,
            ColorWrites, FragmentState, Operations, PipelineCache, RenderPassColorAttachment,
            RenderPassDescriptor, RenderPipelineDescriptor, ShaderStages, ShaderType,
            SpecializedRenderPipeline, SpecializedRenderPipelines, TextureFormat,
            TextureSampleType,
            binding_types::{texture_2d, uniform_buffer},
        },
        renderer::{RenderContext, ViewQuery},
        view::ViewTarget,
    },
    shader::Shader,
};

const SHADER_HANDLE: Handle<Shader> = uuid_handle!("77bc97eb-064d-4d25-8792-4f62a6d7af4f");

pub struct TidewaterFilmGrainPlugin;

impl Plugin for TidewaterFilmGrainPlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            SHADER_HANDLE,
            "tidewater_film_grain.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<TidewaterFilmGrain>::default(),
            UniformComponentPlugin::<TidewaterFilmGrain>::default(),
        ))
        .add_systems(Update, advance_frame);

        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app
            .init_resource::<SpecializedRenderPipelines<Pipeline>>()
            .add_systems(RenderStartup, init_pipeline)
            .add_systems(
                Core3d,
                // Auto exposure meters ViewTarget's fixed main view. A ping-pong
                // pass before that meter changes its input and brightens the whole
                // scene. Bevy keeps tonemapping ordered after its private meter.
                render
                    .after(crate::tidewater_gtao::GtaoCompositePass)
                    .after(tonemapping)
                    .in_set(Core3dSystems::PostProcess),
            );
    }
}

#[derive(Component, Clone, Copy, Debug, ExtractComponent, ShaderType)]
pub struct TidewaterFilmGrain {
    /// x: source grain intensity; other lanes reserved for uniform alignment.
    pub intensity: Vec4,
    /// x: frame seed; other lanes reserved for uniform alignment.
    pub frame: UVec4,
}

impl TidewaterFilmGrain {
    #[must_use]
    pub fn new(intensity: f32) -> Self {
        Self {
            intensity: Vec4::new(intensity, 0.0, 0.0, 0.0),
            frame: UVec4::ZERO,
        }
    }
}

fn advance_frame(mut cameras: Query<&mut TidewaterFilmGrain>) {
    for mut grain in &mut cameras {
        grain.frame.x = grain.frame.x.wrapping_add(1);
    }
}

#[derive(Resource)]
struct Pipeline {
    layout: BindGroupLayoutDescriptor,
    fullscreen: FullscreenShader,
}

fn init_pipeline(mut commands: Commands, fullscreen: Res<FullscreenShader>) {
    let layout = BindGroupLayoutDescriptor::new(
        "tidewater film grain layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                uniform_buffer::<TidewaterFilmGrain>(true),
            ),
        ),
    );
    commands.insert_resource(Pipeline {
        layout,
        fullscreen: fullscreen.clone(),
    });
}

impl SpecializedRenderPipeline for Pipeline {
    type Key = TextureFormat;

    fn specialize(&self, format: Self::Key) -> RenderPipelineDescriptor {
        RenderPipelineDescriptor {
            label: Some("tidewater film grain pipeline".into()),
            layout: vec![self.layout.clone()],
            vertex: self.fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader: SHADER_HANDLE,
                entry_point: Some("fragment".into()),
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

fn render(
    view: ViewQuery<(
        &ViewTarget,
        &TidewaterFilmGrain,
        &DynamicUniformIndex<TidewaterFilmGrain>,
    )>,
    pipeline: Option<Res<Pipeline>>,
    pipeline_cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    uniforms: Res<ComponentUniforms<TidewaterFilmGrain>>,
    mut context: RenderContext,
) {
    let Some(pipeline) = pipeline else { return };
    let (target, grain, grain_index) = view.into_inner();
    if grain.intensity.x <= 0.0 {
        return;
    }
    let pipeline_id =
        specialized.specialize(&pipeline_cache, &pipeline, target.main_texture_format());
    let Some(render_pipeline) = pipeline_cache.get_render_pipeline(pipeline_id) else {
        return;
    };
    let Some(uniform_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let post = target.post_process_write();
    let bind_group = context.render_device().create_bind_group(
        "tidewater film grain bind group",
        &pipeline_cache.get_bind_group_layout(&pipeline.layout),
        &BindGroupEntries::sequential((post.source, uniform_binding)),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater film grain post process"),
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
    pass.set_bind_group(0, &bind_group, &[grain_index.index()]);
    pass.draw(0..3, 0..1);
}
