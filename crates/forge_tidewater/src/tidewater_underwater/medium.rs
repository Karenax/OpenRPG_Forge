//! Per-view export of the actual underwater lens classification for air haze.
// SPDX-License-Identifier: MIT
// Tidewater 1438b1ab; copyright 2026 DRG Software Solutions LLC.
use super::{Packet, Pass, Pipeline, Settings, TidewaterUnderwaterSurfaceMaterial};
use crate::tidewater_temporal::{
    frame::{FrameTag, ViewPacket},
    hdr::{HdrObservations, HdrRole},
    resolve::ResolveEnabled,
};
use bevy::{
    pbr::{MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        erased_render_asset::ErasedRenderAssets,
        extract_component::{ComponentUniforms, DynamicUniformIndex},
        render_asset::RenderAssets,
        render_resource::{
            BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries, BufferSize,
            Extent3d, Operations, PipelineCache, RenderPassColorAttachment, RenderPassDescriptor,
            SamplerBindingType, ShaderStages, SpecializedRenderPipelines, TextureDescriptor,
            TextureDimension, TextureFormat, TextureSampleType, TextureUsages,
            binding_types::{sampler, storage_buffer_read_only_sized, texture_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, ViewQuery},
        storage::GpuShaderBuffer,
        texture::{CachedTexture, TextureCache},
        view::{ViewDepthTexture, ViewTarget},
    },
};

/// Render-only target. Its receipt proves command encoding, not GPU completion
/// or a source-paired water query; those inputs still require frame validation.
#[derive(Component)]
pub struct Target {
    pub size: UVec2,
    pub texture: CachedTexture,
    encoded: Option<FrameTag>,
}

impl Target {
    #[must_use]
    pub fn encoded_for(&self, tag: FrameTag, size: UVec2) -> bool {
        self.size == size && self.encoded == Some(tag)
    }
}

pub(super) fn layout() -> BindGroupLayoutDescriptor {
    BindGroupLayoutDescriptor::new(
        "tidewater lens medium layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                texture_2d(TextureSampleType::Depth),
                storage_buffer_read_only_sized(
                    false,
                    BufferSize::new(crate::water_height_queries::PACKET_BYTES as u64),
                ),
                uniform_buffer::<Settings>(true),
            ),
        ),
    )
}

type Views<'w, 's> = Query<
    'w,
    's,
    (Entity, &'static ViewTarget, Option<&'static Target>),
    (With<Settings>, With<ResolveEnabled>),
>;

pub(super) fn prepare(
    mut commands: Commands,
    views: Views,
    device: Res<RenderDevice>,
    mut textures: ResMut<TextureCache>,
) {
    for (entity, view, existing) in &views {
        let extent = view.main_texture().size();
        let size = UVec2::new(extent.width, extent.height);
        if size.min_element() == 0 || size.max_element() > device.limits().max_texture_dimension_2d
        {
            commands.entity(entity).remove::<Target>();
            continue;
        }
        if existing.is_some_and(|target| target.size == size) {
            continue;
        }
        let texture = textures.get(
            &device,
            TextureDescriptor {
                label: Some("tidewater lens medium"),
                size: Extent3d {
                    width: size.x,
                    height: size.y,
                    depth_or_array_layers: 1,
                },
                mip_level_count: 1,
                sample_count: 1,
                dimension: TextureDimension::D2,
                format: TextureFormat::R16Float,
                usage: TextureUsages::RENDER_ATTACHMENT
                    | TextureUsages::TEXTURE_BINDING
                    | TextureUsages::COPY_SRC,
                view_formats: &[],
            },
        );
        commands.entity(entity).insert(Target {
            size,
            texture,
            encoded: None,
        });
    }
}

#[derive(bevy::ecs::system::SystemParam)]
pub(super) struct Inputs<'w> {
    pipeline: Option<Res<'w, Pipeline>>,
    cache: Res<'w, PipelineCache>,
    specialized: ResMut<'w, SpecializedRenderPipelines<Pipeline>>,
    packet: Res<'w, Packet>,
    buffers: Res<'w, RenderAssets<GpuShaderBuffer>>,
    uniforms: Res<'w, ComponentUniforms<Settings>>,
    surface: Option<Res<'w, TidewaterUnderwaterSurfaceMaterial>>,
    materials: Option<Res<'w, ErasedRenderAssets<PreparedMaterial>>>,
    allocators: Option<Res<'w, MaterialBindGroupAllocators>>,
    hdr: Option<Res<'w, HdrObservations>>,
}

type LensView = (
    &'static ViewTarget,
    &'static ViewDepthTexture,
    &'static DynamicUniformIndex<Settings>,
    &'static ViewPacket,
    &'static mut Target,
);

pub(super) fn render(
    view: ViewQuery<LensView, With<ResolveEnabled>>,
    mut inputs: Inputs,
    mut context: RenderContext,
) {
    let (scene, depth, settings_index, frame, mut target) = view.into_inner();
    target.encoded = None;
    if !inputs
        .hdr
        .as_deref()
        .is_some_and(|bus| bus.ready_for(frame.tag, HdrRole::Haze))
        || scene.main_texture_format() != TextureFormat::Rgba16Float
    {
        return;
    }
    let Some(pipeline) = inputs.pipeline.as_deref() else {
        return;
    };
    let id = inputs.specialized.specialize(
        &inputs.cache,
        pipeline,
        (TextureFormat::R16Float, Pass::Medium),
    );
    let Some(program) = inputs.cache.get_render_pipeline(id) else {
        return;
    };
    let surface_group = if pipeline.surface_layout.is_some() {
        let (Some(surface), Some(materials), Some(allocators)) = (
            inputs.surface.as_deref(),
            inputs.materials.as_deref(),
            inputs.allocators.as_deref(),
        ) else {
            return;
        };
        let Some(material) = materials.get(surface.0) else {
            return;
        };
        let Some(group) = allocators
            .get(&surface.0.type_id())
            .and_then(|allocator| allocator.get(material.binding.group))
            .and_then(|slab| slab.bind_group())
        else {
            return;
        };
        Some(group)
    } else {
        None
    };
    let (Some(packet), Some(settings)) = (
        inputs.buffers.get(&inputs.packet.0),
        inputs.uniforms.uniforms().binding(),
    ) else {
        return;
    };
    let group = context.render_device().create_bind_group(
        "tidewater lens medium bindings",
        &inputs.cache.get_bind_group_layout(&pipeline.medium_layout),
        &BindGroupEntries::sequential((
            scene.main_texture_view(),
            &pipeline.sampler,
            depth.view(),
            packet.buffer.as_entire_binding(),
            settings,
        )),
    );
    let extent = scene.main_texture().size();
    if target.size != UVec2::new(extent.width, extent.height) {
        return;
    }
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("tidewater lens medium export"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: &target.texture.default_view,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            depth_stencil_attachment: None,
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
    pass.set_pipeline(program);
    if let Some(surface) = surface_group {
        pass.set_bind_group(1, surface, &[]);
    }
    pass.set_bind_group(
        if surface_group.is_some() { 2 } else { 0 },
        &group,
        &[settings_index.index()],
    );
    pass.draw(0..3, 0..1);
    drop(pass);
    target.encoded = Some(frame.tag);
}
