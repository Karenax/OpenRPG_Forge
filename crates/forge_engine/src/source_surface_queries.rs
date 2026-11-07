//! GPU heights from the same prepared optical material as the visible ocean.
// SPDX-License-Identifier: MIT
// Height solver adapted from Tidewater WaterQuery.js, revision 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
use crate::source_water_optics::OpticalMaterial;
use crate::source_water_queries::{PACKET_BYTES, Queries};
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

/// Adds a caller-owned query point before the current GPU packet is captured.
pub type QueryInputProvider = fn(&World, &Queries) -> Result<(), String>;

#[derive(Resource, Default)]
struct InputProviders(Vec<(&'static str, QueryInputProvider)>);

/// Register a named input provider. Providers run after the camera and before
/// the packet snapshot. Duplicate names and invalid inputs are refused.
pub fn add_query_input(
    world: &mut World,
    name: &'static str,
    provider: QueryInputProvider,
) -> Result<(), String> {
    if name.is_empty() {
        return Err("Water query input name is empty".into());
    }
    world.init_resource::<InputProviders>();
    let mut providers = world.resource_mut::<InputProviders>();
    if providers.0.iter().any(|(existing, _)| *existing == name) {
        return Err("Water query input name is already registered".into());
    }
    providers.0.push((name, provider));
    Ok(())
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
    let height = crate::source_underwater::surface_height_shader_source(optical)?;
    let query = crate::source_water_queries::QUERY_SHADER.replacen(
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
                .after(crate::source_water_schedule::OceanComputeSet)
                .after(crate::source_water_schedule::ShoreComputeSet)
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
    install_shader(world, shader)?;
    world.insert_resource(Surface(material));
    Ok(())
}

pub(crate) fn install_shader(world: &mut World, shader: String) -> Result<(), String> {
    world
        .resource_mut::<Assets<Shader>>()
        .insert(
            SHADER.id(),
            Shader::from_wgsl(shader, "source-surface-queries.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

fn request(world: &mut World) {
    let Some(targets) = world.get_resource::<Targets>() else {
        return;
    };
    let queries = targets.queries.clone();
    let packet = targets.packet.clone();
    #[cfg(feature = "physics-rapier")]
    let camera = world
        .query_filtered::<&GlobalTransform, With<crate::ForgePlayerCamera>>()
        .iter(world)
        .next()
        .map(GlobalTransform::translation);
    #[cfg(not(feature = "physics-rapier"))]
    let camera = crate::scene_camera::primary(world)
        .ok()
        .map(|(_, position)| position);
    if let Some(camera) = camera {
        let _ = queries.set_point(0, camera.x, camera.z);
    }
    if let Some(providers) = world.get_resource::<InputProviders>() {
        for (_, provider) in &providers.0 {
            if let Err(error) = provider(world, &queries) {
                error!(%error, "Water surface query input rejected");
                return;
            }
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
        label: Some("Source visible surface queries".into()),
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
            label: Some("Source surface heights"),
            ..default()
        });
    pass.set_pipeline(compute);
    pass.set_bind_group(1, group, &[]);
    pass.set_bind_group(2, &packet_group, &[]);
    pass.dispatch_workgroups(1, 1, 1);
    targets.encoded_frames.fetch_add(1, Ordering::Relaxed);
}

#[cfg(all(test, not(feature = "physics-rapier")))]
mod minimal_tests {
    use super::*;

    #[test]
    fn camera_query_uses_the_active_scene_view_without_physics() {
        let mut world = World::new();
        world.insert_resource(Assets::<ShaderBuffer>::default());
        let packet = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![0_u32; PACKET_BYTES / 4]));
        world.insert_resource(Targets {
            queries: Queries::default(),
            packet: packet.clone(),
            encoded_frames: default(),
            rejected_packets: default(),
        });
        world.spawn((Camera3d::default(), Transform::from_xyz(4.0, 5.0, 6.0)));
        request(&mut world);
        let bytes = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&packet)
            .unwrap()
            .data
            .as_ref()
            .unwrap();
        let word =
            |index: usize| u32::from_le_bytes(bytes[index * 4..index * 4 + 4].try_into().unwrap());
        assert_eq!(word(4), 4.0_f32.to_bits());
        assert_eq!(word(5), 6.0_f32.to_bits());
    }
}

#[cfg(all(test, feature = "physics-rapier"))]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicBool, AtomicUsize};

    #[derive(Resource)]
    struct Input {
        slot: u32,
        invalid: AtomicBool,
        calls: AtomicUsize,
    }

    fn gameplay_input(world: &World, queries: &Queries) -> Result<(), String> {
        let input = world.resource::<Input>();
        input.calls.fetch_add(1, Ordering::Relaxed);
        assert_eq!(
            queries.snapshot().points[0].map(f32::to_bits),
            [4.0_f32, 6.0, 0.0, 0.0].map(f32::to_bits)
        );
        let x = if input.invalid.load(Ordering::Relaxed) {
            f32::NAN
        } else {
            12.0
        };
        queries.set_point(input.slot, x, 14.0)
    }

    #[test]
    fn gameplay_input_precedes_packet_and_invalid_input_preserves_the_previous_packet() {
        let mut world = World::new();
        world.insert_resource(Assets::<ShaderBuffer>::default());
        let packet = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![0_u32; PACKET_BYTES / 4]));
        let queries = Queries::default();
        let slot = queries.allocate("gameplay", 1).unwrap();
        world.insert_resource(Targets {
            queries,
            packet: packet.clone(),
            encoded_frames: default(),
            rejected_packets: default(),
        });
        world.insert_resource(Input {
            slot,
            invalid: AtomicBool::new(false),
            calls: AtomicUsize::new(0),
        });
        world.spawn((
            crate::ForgePlayerCamera,
            GlobalTransform::from(Transform::from_xyz(4.0, 5.0, 6.0)),
        ));
        add_query_input(&mut world, "gameplay", gameplay_input).unwrap();
        assert!(add_query_input(&mut world, "gameplay", gameplay_input).is_err());
        request(&mut world);
        let bytes = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&packet)
            .unwrap()
            .data
            .clone()
            .unwrap();
        let read_u32 =
            |index: usize| u32::from_le_bytes(bytes[index * 4..index * 4 + 4].try_into().unwrap());
        assert_eq!(read_u32(1), 2);
        assert_eq!(read_u32(4), 4.0_f32.to_bits());
        assert_eq!(read_u32(5), 6.0_f32.to_bits());
        assert_eq!(read_u32(8), 12.0_f32.to_bits());
        assert_eq!(read_u32(9), 14.0_f32.to_bits());
        assert_eq!(world.resource::<Input>().calls.load(Ordering::Relaxed), 1);
        world
            .resource::<Input>()
            .invalid
            .store(true, Ordering::Relaxed);
        request(&mut world);
        assert_eq!(world.resource::<Input>().calls.load(Ordering::Relaxed), 2);
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&packet)
                .unwrap()
                .data
                .as_ref(),
            Some(&bytes)
        );
    }
}
