//! Source camera-centred light maps shared by terrain and solid prop receivers.
// SPDX-License-Identifier: MIT
// Tidewater UnderwaterLighting.js, 1438b1ab; copyright 2026 DRG Software Solutions LLC.
use bevy::{
    asset::{RenderAssetUsages, UntypedAssetId, uuid_handle},
    core_pipeline::schedule::camera_driver,
    ecs::system::SystemParam,
    image::{ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    pbr::{MaterialBindGroupAllocators, PreparedMaterial},
    prelude::*,
    render::{
        RenderApp, RenderStartup,
        erased_render_asset::ErasedRenderAssets,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        gpu_readback::{Readback, ReadbackComplete},
        render_asset::RenderAssets,
        render_resource::{
            self as gpu,
            binding_types::{storage_buffer_sized, texture_storage_2d_array},
        },
        renderer::{RenderContext, RenderDevice, RenderGraph},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicU64, Ordering},
};

const SHADER: Handle<Shader> = uuid_handle!("f7b62e08-4cd7-4f16-a8ec-0fb976c901c1");
pub const NEUTRAL_FRAME: Handle<ShaderBuffer> =
    uuid_handle!("f8dc95fb-cc74-431b-832d-7bf1506b9778");
const BAKE: &str = include_str!("tidewater_underwater_lighting.wgsl");

#[derive(Clone, Debug)]
pub struct Bindings {
    pub waves: Handle<Image>,
    pub level: Handle<Image>,
    pub frame: Handle<ShaderBuffer>,
}
#[derive(Clone, Copy, Debug, Default)]
pub struct Observation {
    pub origins: [f32; 4],
    pub step: u32,
    pub time: f32,
    pub resolution: u32,
    pub layers: u32,
}
#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub bindings: Bindings,
    /// Encoded dispatches, not a GPU completion fence.
    pub encoded_frames: Arc<AtomicU64>,
    pub observation: Arc<Mutex<Observation>>,
}
#[derive(Resource, Clone, ExtractResource)]
pub struct Surface(pub UntypedAssetId);
#[derive(Resource, Clone, Copy, PartialEq)]
pub struct Control {
    pub enabled: bool,
    pub caustics_strength: f32,
    pub shadow_refraction: bool,
}
impl Default for Control {
    fn default() -> Self {
        Self {
            enabled: true,
            caustics_strength: 0.75,
            shadow_refraction: true,
        }
    }
}
#[derive(Resource)]
struct Pipeline {
    id: gpu::CachedComputePipelineId,
    layout: gpu::BindGroupLayoutDescriptor,
}
#[derive(Resource, Default)]
struct GroupCache(
    Option<(
        gpu::TextureViewId,
        gpu::TextureViewId,
        gpu::BufferId,
        gpu::BindGroup,
    )>,
);

pub fn install_neutral(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Underwater receivers require shader buffers")?;
    if buffers.get(&NEUTRAL_FRAME).is_none() {
        buffers
            .insert(NEUTRAL_FRAME.id(), ShaderBuffer::from(vec![0_u32; 8]))
            .map_err(|error| error.to_string())?;
    }
    Ok(())
}

pub fn shader_source(optical: &str) -> Result<String, String> {
    Ok(format!(
        "{}\n{BAKE}",
        crate::tidewater_underwater::surface_height_shader_source(optical)?
    ))
}
pub fn install_shader(world: &mut World, optical: &str) -> Result<(), String> {
    let source = shader_source(optical)?;
    world
        .resource_mut::<Assets<Shader>>()
        .insert(
            SHADER.id(),
            Shader::from_wgsl(source, "tidewater-underwater-light-maps.wgsl"),
        )
        .map_err(|error| error.to_string())?;
    Ok(())
}

/// Use the existing renderer and the caller's prepared optical material layout.
pub fn register(
    app: &mut App,
    optical_layout: fn(&RenderDevice) -> gpu::BindGroupLayoutDescriptor,
) -> Result<(), String> {
    if app.world().contains_resource::<Targets>() {
        return Ok(());
    }
    install_neutral(app.world_mut())?;
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Light maps require the resident renderer".into());
    }
    let (waves, level) = {
        let mut images = app.world_mut().resource_mut::<Assets<Image>>();
        (images.add(map_image()), images.add(map_image()))
    };
    let mut frame = ShaderBuffer::from(vec![0_u32; 8]);
    frame.buffer_description.usage |= gpu::BufferUsages::COPY_SRC;
    let frame = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(frame);
    app.world_mut()
        .spawn(Readback::buffer(frame.clone()))
        .observe(receive);
    app.insert_resource(Targets {
        bindings: Bindings {
            waves,
            level,
            frame,
        },
        encoded_frames: default(),
        observation: default(),
    })
    .init_resource::<Control>()
    .add_systems(PostUpdate, apply_control)
    .add_plugins((
        ExtractResourcePlugin::<Targets>::default(),
        ExtractResourcePlugin::<Surface>::default(),
    ));
    app.sub_app_mut(RenderApp)
        .init_resource::<GroupCache>()
        .add_systems(
            RenderStartup,
            move |commands: Commands, device: Res<RenderDevice>, cache: Res<gpu::PipelineCache>| {
                init_pipeline(commands, &device, &cache, optical_layout);
            },
        )
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
fn map_image() -> Image {
    let mut image = Image::new_uninit(
        gpu::Extent3d {
            width: 512,
            height: 512,
            depth_or_array_layers: 2,
        },
        gpu::TextureDimension::D2,
        gpu::TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.texture_descriptor.usage = gpu::TextureUsages::TEXTURE_BINDING
        | gpu::TextureUsages::STORAGE_BINDING
        | gpu::TextureUsages::COPY_SRC;
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor {
        mag_filter: ImageFilterMode::Linear,
        min_filter: ImageFilterMode::Linear,
        ..default()
    });
    image
}
fn init_pipeline(
    mut commands: Commands,
    device: &RenderDevice,
    cache: &gpu::PipelineCache,
    optical_layout: fn(&RenderDevice) -> gpu::BindGroupLayoutDescriptor,
) {
    let layout = gpu::BindGroupLayoutDescriptor::new(
        "underwater light map outputs",
        &gpu::BindGroupLayoutEntries::sequential(
            gpu::ShaderStages::COMPUTE,
            (
                storage_buffer_sized(false, gpu::BufferSize::new(32)),
                texture_storage_2d_array(
                    gpu::TextureFormat::Rgba16Float,
                    gpu::StorageTextureAccess::WriteOnly,
                ),
                texture_storage_2d_array(
                    gpu::TextureFormat::Rgba16Float,
                    gpu::StorageTextureAccess::WriteOnly,
                ),
            ),
        ),
    );
    let id = cache.queue_compute_pipeline(gpu::ComputePipelineDescriptor {
        label: Some("Tidewater underwater light maps".into()),
        layout: vec![
            gpu::BindGroupLayoutDescriptor::new("unused underwater view", &[]),
            optical_layout(device),
            layout.clone(),
        ],
        shader: SHADER,
        entry_point: Some("forge_underwater_light_maps".into()),
        ..default()
    });
    commands.insert_resource(Pipeline { id, layout });
}
#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(event: On<ReadbackComplete>, targets: Res<Targets>) {
    let Some(observation) = decode(&event.data) else {
        return;
    };
    if let Ok(mut current) = targets.observation.lock() {
        *current = observation;
    }
}
fn decode(bytes: &[u8]) -> Option<Observation> {
    let chunks = bytes.get(..32)?.as_chunks::<4>().0;
    let words: [u32; 8] = std::array::from_fn(|index| u32::from_le_bytes(chunks[index]));
    let origins: [f32; 4] = std::array::from_fn(|index| f32::from_bits(words[index]));
    let time = f32::from_bits(words[5]);
    if words[4] == 0
        || words[6] != 512
        || words[7] != 2
        || !time.is_finite()
        || origins.iter().any(|value| !value.is_finite())
    {
        return None;
    }
    Some(Observation {
        origins,
        step: words[4],
        time,
        resolution: words[6],
        layers: words[7],
    })
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn apply_control(
    control: Res<Control>,
    world_materials: Option<ResMut<Assets<crate::tidewater_terrain::lod::LodMaterial>>>,
    terrain: Option<ResMut<Assets<crate::tidewater_terrain::TerrainMaterial>>>,
    village: Option<ResMut<Assets<crate::tidewater_material::VillageMaterial>>>,
    inputs: Option<ResMut<crate::tidewater_caustics::CausticMaterialInputs>>,
) {
    if !control.is_changed() {
        return;
    }
    let apply = |params: &mut crate::tidewater_caustics::CausticSampleParams| {
        if params.wave_tiles_m.x <= 0.0 || params.water_scattering.w < 0.0 {
            return;
        }
        params.water_scattering.w = f32::from(control.enabled);
        params.sun_and_strength.w = control.caustics_strength.clamp(0.0, 4.0);
        params.water_absorption.w = f32::from(control.shadow_refraction);
    };
    if let Some(mut world_materials) = world_materials {
        for (_, material) in world_materials.iter_mut() {
            apply(&mut material.base.caustic_params);
        }
    }
    if let Some(mut terrain) = terrain {
        for (_, material) in terrain.iter_mut() {
            apply(&mut material.caustic_params);
        }
    }
    if let Some(mut village) = village {
        for (_, material) in village.iter_mut() {
            apply(&mut material.underwater_params);
        }
    }
    if let Some(mut inputs) = inputs {
        apply(&mut inputs.params);
    }
}
#[derive(SystemParam)]
struct RenderInputs<'w> {
    images: Res<'w, RenderAssets<GpuImage>>,
    buffers: Res<'w, RenderAssets<GpuShaderBuffer>>,
    materials: Res<'w, ErasedRenderAssets<PreparedMaterial>>,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn encode(
    state: (
        Res<Pipeline>,
        Res<gpu::PipelineCache>,
        Res<Surface>,
        Res<Targets>,
    ),
    assets: RenderInputs,
    allocators: Res<MaterialBindGroupAllocators>,
    mut groups: ResMut<GroupCache>,
    mut context: RenderContext,
) {
    let (pipeline, cache, surface, targets) = state;
    let RenderInputs {
        images,
        buffers,
        materials,
    } = assets;
    let Some(compute) = cache.get_compute_pipeline(pipeline.id) else {
        return;
    };
    let (Some(waves), Some(level), Some(frame), Some(material)) = (
        images.get(&targets.bindings.waves),
        images.get(&targets.bindings.level),
        buffers.get(&targets.bindings.frame),
        materials.get(surface.0),
    ) else {
        return;
    };
    let Some(group) = allocators
        .get(&surface.0.type_id())
        .and_then(|allocator| allocator.get(material.binding.group))
        .and_then(|slab| slab.bind_group())
    else {
        return;
    };
    let key = (
        waves.texture_view.id(),
        level.texture_view.id(),
        frame.buffer.id(),
    );
    if groups
        .0
        .as_ref()
        .is_none_or(|previous| (previous.0, previous.1, previous.2) != key)
    {
        groups.0 = Some((
            key.0,
            key.1,
            key.2,
            context.render_device().create_bind_group(
                "underwater map outputs",
                &cache.get_bind_group_layout(&pipeline.layout),
                &gpu::BindGroupEntries::sequential((
                    frame.buffer.as_entire_binding(),
                    &waves.texture_view,
                    &level.texture_view,
                )),
            ),
        ));
    }
    let Some((_, _, _, outputs)) = &groups.0 else {
        return;
    };
    let mut pass = context
        .command_encoder()
        .begin_compute_pass(&gpu::ComputePassDescriptor {
            label: Some("Tidewater underwater light maps"),
            ..default()
        });
    pass.set_pipeline(compute);
    pass.set_bind_group(1, group, &[]);
    pass.set_bind_group(2, outputs, &[]);
    pass.dispatch_workgroups(64, 64, 2);
    targets.encoded_frames.fetch_add(1, Ordering::Relaxed);
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact packed source origin coordinates in the GPU stamp."
    )]
    fn rejects_uninitialized_and_nonfinite_gpu_stamps() {
        assert!(decode(&[0; 32]).is_none());
        let packet = |time: f32| {
            [
                (-64.0_f32).to_bits(),
                (-64.0_f32).to_bits(),
                (-512.0_f32).to_bits(),
                (-512.0_f32).to_bits(),
                120,
                time.to_bits(),
                512,
                2,
            ]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect::<Vec<_>>()
        };
        let observed = decode(&packet(2.0)).unwrap();
        assert_eq!(observed.origins, [-64.0, -64.0, -512.0, -512.0]);
        assert_eq!(observed.step, 120);
        assert!(decode(&packet(f32::NAN)).is_none());
    }
    #[test]
    fn baked_array_kernel_and_gpu_receipt_validate_together() {
        // Only the optical interface is stubbed; validate the actual bake entry,
        // two storage arrays, snapped origins and readback-buffer writes.
        let interface = r"
struct Frame { seaLevel: f32, cameraPos: vec3f, frameIndex: u32, time: f32 };
var<private> frame: Frame;
struct Ocean { sizes: array<vec4f, 4> };
@group(1) @binding(0) var<uniform> ocean: Ocean;
@group(1) @binding(4) var oceanDisplacement: texture_2d_array<f32>;
@group(1) @binding(5) var smpLinearRepeat: sampler;
@group(1) @binding(8) var oceanDerivatives: texture_2d_array<f32>;
struct ShoreSample { disp: vec3f, nShore: vec3f, foam: f32 };
struct Detail { gust: f32, slick: f32 };
fn setupUnderwaterHeightFrame() { frame.seaLevel = 0.0; }
fn terrainHeightAt(p: vec2f) -> f32 { return -1.0; }
fn waterSurfaceCascadeAttenuation(c: i32, depth: f32) -> f32 { return 1.0; }
fn shoreEvaluate(p: vec2f, d: f32, h: f32) -> ShoreSample { return ShoreSample(vec3f(0), vec3f(0,1,0), 0.0); }
fn shoreSimSample(p: vec2f) -> vec4f { return vec4f(0); }
fn seaDetailSample(p: vec2f) -> Detail { return Detail(0.5, 0.0); }
";
        let shader = naga::front::wgsl::parse_str(&format!("{interface}\n{BAKE}")).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&shader)
        .unwrap();
    }
}
