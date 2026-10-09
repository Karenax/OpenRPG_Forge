//! Native, source-derived optical flare for the Tidewater sun.
#![allow(clippy::needless_pass_by_value)]
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/post/LensFlare.js`, copyright 2026 DRG Software
// Solutions LLC. Upstream 1438b1ab; see `third_party/tidewater/LICENSE`.

use crate::{
    tidewater_material::VillageMaterial,
    tidewater_underwater::{TidewaterImmersion, TidewaterUnderwaterObservation},
};
use bevy::{
    asset::{load_internal_asset, uuid_handle},
    core_pipeline::{Core3dSystems, FullscreenShader, schedule::Core3d},
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

const SHADER_HANDLE: Handle<Shader> = uuid_handle!("0df67ebd-3f5f-4bc3-86c2-d11e90c4785b");
const VISIBILITY_SHADER_HANDLE: Handle<Shader> =
    uuid_handle!("d830e89b-c170-4c56-b682-3bd116c51dc2");

pub struct TidewaterLensFlarePlugin;

/// Orders consumers of the composed flare before their final grading pass.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct TidewaterLensFlarePass;
impl Plugin for TidewaterLensFlarePlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            SHADER_HANDLE,
            "tidewater_lens_flare.wgsl",
            Shader::from_wgsl
        );
        load_internal_asset!(
            app,
            VISIBILITY_SHADER_HANDLE,
            "tidewater_lens_flare_visibility.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<TidewaterLensFlare>::default(),
            UniformComponentPlugin::<TidewaterLensFlare>::default(),
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
                Render,
                prepare_target.in_set(RenderSystems::PrepareResources),
            )
            .add_systems(
                Core3d,
                (
                    render_visibility.in_set(Core3dSystems::EarlyPostProcess),
                    // The static GPU probe produced an unlit output when this
                    // pass ran before PostProcess. Keep it after the resolved
                    // post chain and before the final air-haze/upscale pass.
                    render
                        .in_set(TidewaterLensFlarePass)
                        .after(Core3dSystems::PostProcess)
                        .before(
                            crate::tidewater_air_haze::render::<
                                false,
                                Without<crate::tidewater_temporal::resolve::ResolveEnabled>,
                            >,
                        ),
                ),
            );
    }
}

/// Remains attached at zero strength, preserving `ViewTarget` ping-pong topology.
#[derive(Component, Clone, Copy, Debug, ExtractComponent, ShaderType)]
pub struct TidewaterLensFlare {
    sun: Vec4,
    optics: Vec4,
    light: Vec4,
}
impl TidewaterLensFlare {
    #[must_use]
    pub fn new(strength: f32) -> Self {
        Self {
            sun: Vec4::new(0.5, 0.5, 0.0, strength),
            optics: Vec4::new(16.0 / 9.0, 6.0, 1.0, 0.0),
            light: Vec4::ONE,
        }
    }

    /// Change the solar flare intensity without resetting its projected sun.
    pub fn set_strength(&mut self, strength: f32) {
        self.sun.w = strength.clamp(0.0, 2.0);
    }

    /// Current intensity supplied to the optical pass.
    #[must_use]
    pub fn strength(&self) -> f32 {
        self.sun.w
    }
}

fn update(
    lights: Query<(&DirectionalLight, &GlobalTransform)>,
    village_materials: Res<Assets<VillageMaterial>>,
    immersion: Option<Res<TidewaterUnderwaterObservation>>,
    mut views: Query<(
        &GlobalTransform,
        &Camera,
        &Projection,
        &mut TidewaterLensFlare,
    )>,
    mut reported: Local<bool>,
) {
    // Imported scene lighting is the same HDR atmosphere sample used by the
    // Tidewater materials. A native directional light is only a fallback for
    // scenes without imported materials, and there can be more than one light.
    let source_sun = village_materials.iter().next().map(|(_, material)| {
        (
            material.scene_lighting.sun_direction.truncate(),
            material.scene_lighting.sun_color.truncate(),
        )
    });
    let Some((sun_direction, sun_color)) = source_sun.or_else(|| {
        lights
            .iter()
            .max_by(|(a, _), (b, _)| a.illuminance.total_cmp(&b.illuminance))
            .map(|(light, transform)| {
                (
                    -transform.forward().as_vec3(),
                    light.color.to_linear().to_vec3() * (light.illuminance / 1000.0),
                )
            })
    }) else {
        return;
    };
    let diagnostic = match std::env::var("FORGE_TIDEWATER_FLARE_DEBUG").as_deref() {
        Ok("visibility") => 2.0,
        Ok("depth") => 3.0,
        _ => 1.0,
    };
    let above_water = immersion
        .as_ref()
        .is_none_or(|state| !matches!(state.immersion, TidewaterImmersion::FullyImmersed));
    for (camera, camera_view, projection, mut flare) in &mut views {
        let clip = camera_view.clip_from_view()
            * camera
                .to_matrix()
                .inverse()
                .transform_vector3(sun_direction)
                .extend(0.0);
        let ndc = clip.truncate() / clip.w.max(f32::MIN_POSITIVE);
        let in_view = if camera.forward().dot(sun_direction) > 0.05 {
            ((1.0 - ndc.x.abs().max(ndc.y.abs())) / 0.1).clamp(0.0, 1.0)
        } else {
            0.0
        };
        flare.sun = Vec4::new(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5, in_view, flare.sun.w);
        if let Projection::Perspective(perspective) = projection {
            flare.optics.x = perspective.aspect_ratio;
            flare.optics.y = (0.004_625 / (perspective.fov * 0.5).tan() * 540.0).max(1.5);
        }
        flare.optics.z = f32::from(above_water);
        flare.light = sun_color.extend(diagnostic);
        if diagnostic > 1.0 && !*reported && source_sun.is_some() {
            eprintln!(
                "TIDEWATER_FLARE_DIAGNOSTIC lights={} sun={sun_direction:?} uv={:?} in_view={in_view} above_water={above_water} color={sun_color:?}",
                lights.iter().count(),
                flare.sun.xy(),
            );
            *reported = true;
        }
    }
}

#[derive(Component)]
struct VisibilityTarget(CachedTexture);
fn prepare_target(
    mut commands: Commands,
    mut cache: ResMut<TextureCache>,
    device: Res<RenderDevice>,
    views: Query<(Entity, &ExtractedCamera), With<TidewaterLensFlare>>,
) {
    for (entity, _) in &views {
        commands.entity(entity).insert(VisibilityTarget(cache.get(
            &device,
            TextureDescriptor {
                label: Some("tidewater lens flare visibility"),
                size: Extent3d {
                    width: 1,
                    height: 1,
                    depth_or_array_layers: 1,
                },
                mip_level_count: 1,
                sample_count: 1,
                dimension: TextureDimension::D2,
                format: TextureFormat::R16Float,
                usage: TextureUsages::RENDER_ATTACHMENT | TextureUsages::TEXTURE_BINDING,
                view_formats: &[],
            },
        )));
    }
}
#[derive(Resource)]
struct Pipeline {
    visibility_layout: BindGroupLayoutDescriptor,
    composite_layout: BindGroupLayoutDescriptor,
    fullscreen: FullscreenShader,
}
#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
enum PassKey {
    Visibility,
    Composite(TextureFormat),
}
fn init_pipeline(mut commands: Commands, fullscreen: Res<FullscreenShader>) {
    commands.insert_resource(Pipeline {
        visibility_layout: BindGroupLayoutDescriptor::new(
            "tidewater lens flare visibility layout",
            &BindGroupLayoutEntries::sequential(
                ShaderStages::FRAGMENT,
                (
                    texture_depth_2d(),
                    uniform_buffer::<TidewaterLensFlare>(true),
                ),
            ),
        ),
        composite_layout: BindGroupLayoutDescriptor::new(
            "tidewater lens flare layout",
            &BindGroupLayoutEntries::sequential(
                ShaderStages::FRAGMENT,
                (
                    texture_2d(TextureSampleType::Float { filterable: true }),
                    texture_2d(TextureSampleType::Float { filterable: false }),
                    uniform_buffer::<TidewaterLensFlare>(true),
                ),
            ),
        ),
        fullscreen: fullscreen.clone(),
    });
}
impl SpecializedRenderPipeline for Pipeline {
    type Key = PassKey;
    fn specialize(&self, key: Self::Key) -> RenderPipelineDescriptor {
        let (layout, entry, format) = match key {
            PassKey::Visibility => (
                &self.visibility_layout,
                "visibility",
                TextureFormat::R16Float,
            ),
            PassKey::Composite(format) => (&self.composite_layout, "fragment", format),
        };
        RenderPipelineDescriptor {
            label: Some("tidewater lens flare pipeline".into()),
            layout: vec![layout.clone()],
            vertex: self.fullscreen.to_vertex_state(),
            fragment: Some(FragmentState {
                shader: match key {
                    PassKey::Visibility => VISIBILITY_SHADER_HANDLE,
                    PassKey::Composite(_) => SHADER_HANDLE,
                },
                entry_point: Some(entry.into()),
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
fn render_visibility(
    view: ViewQuery<(
        &ViewDepthTexture,
        &VisibilityTarget,
        &DynamicUniformIndex<TidewaterLensFlare>,
    )>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    uniforms: Res<ComponentUniforms<TidewaterLensFlare>>,
    mut context: RenderContext,
) {
    let (Some(pipeline), Some(uniform)) = (pipeline, uniforms.uniforms().binding()) else {
        return;
    };
    let id = specialized.specialize(&cache, &pipeline, PassKey::Visibility);
    let Some(render_pipeline) = cache.get_render_pipeline(id) else {
        return;
    };
    let (depth, visibility, index) = view.into_inner();
    let group = context.render_device().create_bind_group(
        "tidewater lens flare visibility bind group",
        &cache.get_bind_group_layout(&pipeline.visibility_layout),
        &BindGroupEntries::sequential((depth.view(), uniform)),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater lens flare visibility"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: &visibility.0.default_view,
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
}
fn render(
    view: ViewQuery<(
        &ViewTarget,
        &VisibilityTarget,
        &DynamicUniformIndex<TidewaterLensFlare>,
    )>,
    pipeline: Option<Res<Pipeline>>,
    cache: Res<PipelineCache>,
    mut specialized: ResMut<SpecializedRenderPipelines<Pipeline>>,
    uniforms: Res<ComponentUniforms<TidewaterLensFlare>>,
    mut context: RenderContext,
) {
    let (Some(pipeline), Some(uniform)) = (pipeline, uniforms.uniforms().binding()) else {
        return;
    };
    let (target, visibility, index) = view.into_inner();
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
        "tidewater lens flare bind group",
        &cache.get_bind_group_layout(&pipeline.composite_layout),
        &BindGroupEntries::sequential((post.source, &visibility.0.default_view, uniform)),
    );
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater lens flare post process"),
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
}
