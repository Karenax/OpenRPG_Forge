//! GPU heights from the same prepared optical material as the visible ocean.
// SPDX-License-Identifier: MIT
// Height solver adapted from Tidewater WaterQuery.js, revision 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
use crate::{
    tidewater_ocean_optics::OpticalMaterial,
    water_height_queries::{PACKET_BYTES, Queries},
};
use bevy::{
    asset::{UntypedAssetId, uuid_handle},
    core_pipeline::schedule::camera_driver,
    pbr::{MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        RenderApp, RenderStartup,
        erased_render_asset::ErasedRenderAssets,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            BufferSize, CachedComputePipelineId, ComputePassDescriptor, ComputePipelineDescriptor,
            PipelineCache, ShaderStages, binding_types::storage_buffer_sized,
        },
        renderer::{RenderContext, RenderDevice, RenderGraph},
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicU64, Ordering},
};

const SHADER: Handle<Shader> = uuid_handle!("8272eafe-26c1-4cab-8d3d-a567fc7f0420");

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub queries: Queries,
    pub packet: Handle<ShaderBuffer>,
    pub encoded_frames: Arc<AtomicU64>,
    pub rejected_packets: Arc<AtomicU64>,
}

#[derive(Resource, Clone, ExtractResource)]
struct Surface(UntypedAssetId);

#[derive(Resource)]
struct Pipeline {
    id: CachedComputePipelineId,
    packet_layout: BindGroupLayoutDescriptor,
}

/// Preserve the existing query calculation; initialize private frame globals
/// and stamp results from the GPU uniform actually used, not a newer CPU clock.
pub(crate) fn shader_source(optical: &str) -> Result<String, String> {
    let height = crate::tidewater_underwater::surface_height_shader_source(optical)?;
    let query = crate::water_height_queries::QUERY_SHADER.replacen(
        "    let i = gid.x;",
        "    setupUnderwaterHeightFrame();\n    let i = gid.x;\n    if (i == 0u) {\n        let bits = bitcast<u32>(frame.time);\n        let exponent = (bits >> 23u) & 255u;\n        let mantissa = bits & 8388607u;\n        forgeWaterQueryPacket.stamp.x = frame.frameIndex;\n        // The running source clock is positive and normal (bootstrap starts at 2 s).\n        forgeWaterQueryPacket.stamp.z = mantissa << 29u;\n        forgeWaterQueryPacket.stamp.w = ((exponent + 896u) << 20u) | (mantissa >> 3u);\n    }",
        1,
    );
    Ok(format!("{height}\n{query}"))
}

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Targets>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none()
        || !app.world().contains_resource::<Assets<ShaderBuffer>>()
    {
        return Err("Surface queries require the existing renderer".into());
    }
    let mut packet = ShaderBuffer::from(vec![0_u32; PACKET_BYTES / 4]);
    packet.buffer_description.usage |= bevy::render::render_resource::BufferUsages::COPY_SRC;
    let packet = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(packet);
    let targets = Targets {
        queries: Queries::default(),
        packet: packet.clone(),
        encoded_frames: default(),
        rejected_packets: default(),
    };
    app.insert_resource(targets)
        .add_plugins((
            ExtractResourcePlugin::<Targets>::default(),
            ExtractResourcePlugin::<Surface>::default(),
        ))
        .add_systems(
            PostUpdate,
            request.after(bevy::transform::TransformSystems::Propagate),
        );
    app.world_mut()
        .spawn(Readback::buffer(packet))
        .observe(receive);
    app.sub_app_mut(RenderApp)
        .add_systems(RenderStartup, init_pipeline)
        .add_systems(
            RenderGraph,
            encode
                .after(crate::tidewater_ocean::OceanComputeSet)
                .after(crate::tidewater_shore_sim::ShoreComputeSet)
                .before(camera_driver)
                .run_if(resource_exists::<Surface>),
        );
    Ok(())
}

pub(crate) fn stage(
    world: &mut World,
    shader: String,
    material: UntypedAssetId,
) -> Result<(), String> {
    if !world.contains_resource::<Targets>() {
        return Err("Surface queries not registered".into());
    }
    world
        .resource_mut::<Assets<Shader>>()
        .insert(
            SHADER.id(),
            Shader::from_wgsl(shader, "tidewater-surface-queries.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    world.insert_resource(Surface(material));
    Ok(())
}

fn request(world: &mut World) {
    let Some(targets) = world.get_resource::<Targets>() else {
        return;
    };
    let queries = targets.queries.clone();
    let packet = targets.packet.clone();
    let camera = world
        .query_filtered::<&GlobalTransform, With<forge_engine::ForgePlayerCamera>>()
        .iter(world)
        .next()
        .map(GlobalTransform::translation);
    if let Some(camera) = camera {
        let _ = queries.set_point(0, camera.x, camera.z);
    }
    if let Some(player) = world.get_resource::<crate::AmphibiousPlayerState>() {
        let position = player.motion.position;
        if let Err(error) = queries.set_point(player.water_query_slot(), position.x, position.z) {
            error!(%error, "Player surface query position rejected");
            return;
        }
    }
    let batch = queries.snapshot();
    let mut words = vec![0_u32; PACKET_BYTES / 4];
    words[1] = batch.count;
    for (index, point) in batch.points.iter().enumerate() {
        for (component, value) in point.iter().enumerate() {
            words[4 + index * 4 + component] = value.to_bits();
        }
    }
    if let Some(mut buffer) = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .get_mut(&packet)
    {
        buffer.set_data(words);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(event: On<ReadbackComplete>, targets: Res<Targets>) {
    if let Err(error) = targets.queries.accept(&event.data) {
        targets.rejected_packets.fetch_add(1, Ordering::Relaxed);
        warn!(%error, "Surface height packet rejected");
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn init_pipeline(mut commands: Commands, device: Res<RenderDevice>, cache: Res<PipelineCache>) {
    let packet_layout = BindGroupLayoutDescriptor::new(
        "surface query packet",
        &BindGroupLayoutEntries::single(
            ShaderStages::COMPUTE,
            storage_buffer_sized(false, BufferSize::new(PACKET_BYTES as u64)),
        ),
    );
    let id = cache.queue_compute_pipeline(ComputePipelineDescriptor {
        label: Some("Tidewater visible surface queries".into()),
        layout: vec![
            BindGroupLayoutDescriptor::new("unused surface view", &[]),
            OpticalMaterial::bind_group_layout_descriptor(&device),
            packet_layout.clone(),
        ],
        shader: SHADER,
        entry_point: Some("forge_water_queries".into()),
        ..default()
    });
    commands.insert_resource(Pipeline { id, packet_layout });
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn encode(
    state: (
        Res<Pipeline>,
        Res<PipelineCache>,
        Res<Surface>,
        Res<Targets>,
    ),
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    materials: Res<ErasedRenderAssets<PreparedMaterial>>,
    allocators: Res<MaterialBindGroupAllocators>,
    mut context: RenderContext,
) {
    let (pipeline, cache, surface, targets) = state;
    let Some(compute) = cache.get_compute_pipeline(pipeline.id) else {
        return;
    };
    let Some(packet) = buffers.get(&targets.packet) else {
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
    let packet_group = context.render_device().create_bind_group(
        "surface query packet",
        &cache.get_bind_group_layout(&pipeline.packet_layout),
        &BindGroupEntries::single(packet.buffer.as_entire_binding()),
    );
    let mut pass = context
        .command_encoder()
        .begin_compute_pass(&ComputePassDescriptor {
            label: Some("Tidewater surface heights"),
            ..default()
        });
    pass.set_pipeline(compute);
    pass.set_bind_group(1, group, &[]);
    pass.set_bind_group(2, &packet_group, &[]);
    pass.dispatch_workgroups(1, 1, 1);
    targets.encoded_frames.fetch_add(1, Ordering::Relaxed);
}
