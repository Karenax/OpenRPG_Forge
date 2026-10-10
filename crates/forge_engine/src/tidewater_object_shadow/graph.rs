//! Shared three-cascade depth graph with generation-scoped late adoption.
#![allow(clippy::needless_pass_by_value)]
use super::{OpaqueDraw, plants, terrain};
use crate::tidewater_debris::scanned_shadow_runtime as scanned_shadow;
use crate::tidewater_fish_props::shadow as fish_shadow;
use crate::tidewater_material::ObjectShadowSettings as Settings;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        diagnostic::RecordDiagnostics,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderGraphSystems, RenderQueue},
        storage::{GpuShaderBuffer, ShaderBuffer},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};
#[path = "animation.rs"]
pub mod animation;
#[path = "fish_extract.rs"]
mod fish_extract;

pub struct Input {
    pub settings: Settings,
    pub draws: Vec<OpaqueDraw>,
    pub matrices: Vec<Mat4>,
    pub terrain: Option<terrain::Input>,
    pub plants: Option<plants::Input>,
    pub eyes: Vec<Vec4>,
}
impl Input {
    fn validate(&self) -> Result<(), String> {
        let s = &self.settings;
        if !matches!(s.enabled, 0.0 | 1.0)
            || s.count != 3
            || s.map_size.to_bits() != 2048.0_f32.to_bits()
            || [s.bias, s.fade, s.sun_angular_diameter]
                .iter()
                .any(|v| !v.is_finite())
            || !s.camera.is_finite()
            || !s.forward.is_finite()
            || s.matrices.iter().any(|m| !m.is_finite())
            || s.cascades.iter().chain(&s.blend).any(|v| !v.is_finite())
        {
            return Err("Invalid object shadow settings".into());
        }
        if s.enabled == 0.0 {
            if !self.draws.is_empty()
                || !self.matrices.is_empty()
                || !self.eyes.is_empty()
                || self.terrain.is_some()
                || self.plants.is_some()
            {
                return Err("Disabled shadows must not contain casters".into());
            }
            return Ok(());
        }
        if self.matrices.len() != 3
            || self.eyes.len() != 3
            || self.eyes.iter().any(|v| !v.is_finite())
            || s.matrices[..3] != self.matrices
        {
            return Err("Object shadow cascade mismatch".into());
        }
        super::validate(&self.draws, &self.matrices)?;
        if let Some(t) = &self.terrain {
            t.validate(&self.matrices)?;
        }
        if let Some(p) = &self.plants {
            p.validate(&self.matrices, &self.eyes)?;
        }
        Ok(())
    }
}
#[derive(Resource, Clone, ExtractResource)]
struct KernelInput(Arc<Input>);
#[derive(Resource, Clone, ExtractResource)]
pub struct Target {
    pub settings: Settings,
    pub image: Handle<Image>,
    /// Readback of the sequence and layer mask encoded by the depth graph.
    pub trace: Handle<ShaderBuffer>,
    /// Commands encoded for this generation; not a CPU completion fence.
    pub ready: Arc<AtomicBool>,
}
#[derive(Resource)]
struct Registered;
#[derive(Resource)]
struct Prepared {
    ready: Arc<AtomicBool>,
    opaque: super::OpaqueBatch,
    terrain: Option<terrain::Gpu>,
    plants: Vec<plants::GpuBatch>,
    fish: Option<(Arc<Vec<fish_shadow::MaskedMesh>>, fish_shadow::gpu::Batch)>,
    fish_pose: Option<(Vec<fish_shadow::gpu::Pose>, f32)>,
    scanned: Option<(
        Arc<Vec<scanned_shadow::MeshBytes>>,
        Arc<String>,
        scanned_shadow::gpu::Batch,
    )>,
    scanned_pose: Option<(Vec<scanned_shadow::Pose>, u32)>,
}

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if !app.world().contains_resource::<Assets<Image>>() || app.get_sub_app(RenderApp).is_none() {
        return Err("Object shadows require initialized Bevy rendering".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<KernelInput>::default(),
        ExtractResourcePlugin::<Target>::default(),
        ExtractResourcePlugin::<animation::State>::default(),
    ));
    app.sub_app_mut(RenderApp)
        .add_systems(bevy::render::ExtractSchedule, fish_extract::extract);
    app.sub_app_mut(RenderApp)
        .add_systems(bevy::render::ExtractSchedule, scanned_shadow::extract);
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        draw.before(camera_driver)
            .in_set(RenderGraphSystems::Render)
            .run_if(resource_exists::<KernelInput>)
            .run_if(resource_exists::<Target>),
    );
    app.insert_resource(Registered);
    Ok(())
}
pub fn install(app: &mut App, input: Input) -> Result<Target, String> {
    input.validate()?;
    register(app)?;
    stage(app.world_mut(), input)
}
pub fn stage(world: &mut World, input: Input) -> Result<Target, String> {
    input.validate()?;
    if !world.contains_resource::<Registered>() || !world.contains_resource::<Assets<Image>>() {
        return Err("Object shadow graph is not registered".into());
    }
    super::receiver::reset(world)?;
    let size = if input.settings.enabled > 0.5 {
        2048
    } else {
        1
    };
    let mut image = Image::new_uninit(
        gpu::Extent3d {
            width: size,
            height: size,
            depth_or_array_layers: 3,
        },
        gpu::TextureDimension::D2,
        gpu::TextureFormat::Depth32Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    image.texture_descriptor.usage = gpu::TextureUsages::RENDER_ATTACHMENT
        | gpu::TextureUsages::TEXTURE_BINDING
        | gpu::TextureUsages::COPY_SRC;
    image.texture_view_descriptor = Some(gpu::TextureViewDescriptor {
        dimension: Some(gpu::TextureViewDimension::D2Array),
        ..default()
    });
    image.sampler = bevy::image::ImageSampler::Descriptor(bevy::image::ImageSamplerDescriptor {
        compare: Some(bevy::image::ImageCompareFunction::LessEqual),
        mag_filter: bevy::image::ImageFilterMode::Linear,
        min_filter: bevy::image::ImageFilterMode::Linear,
        ..default()
    });
    let image = world.resource_mut::<Assets<Image>>().add(image);
    let trace = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(UVec4::ZERO));
    let target = Target {
        settings: input.settings.clone(),
        image,
        trace,
        ready: Arc::new(AtomicBool::new(false)),
    };
    world.insert_resource(KernelInput(Arc::new(input)));
    world.insert_resource(target.clone());
    world.insert_resource(animation::State::default());
    Ok(target)
}

fn prepare(
    input: &Input,
    ready: Arc<AtomicBool>,
    device: &RenderDevice,
    queue: &RenderQueue,
) -> Prepared {
    Prepared {
        ready,
        fish: None,
        fish_pose: None,
        scanned: None,
        scanned_pose: None,
        opaque: super::prepare(&input.draws, &input.matrices, device, queue)
            .expect("validated opaque casters and cascades"),
        plants: input.plants.as_ref().map_or_else(Vec::new, |p| {
            plants::prepare(p, &input.matrices, &input.eyes, device, queue)
                .expect("validated plant shadows")
        }),
        terrain: input.terrain.as_ref().map(|t| {
            terrain::prepare(t, &input.matrices, device, queue).expect("validated terrain shadows")
        }),
    }
}

#[allow(clippy::too_many_arguments)]
#[expect(
    clippy::too_many_lines,
    reason = "Cascade draw ordering and resource lifetimes are kept together for the shared render pass"
)]
fn draw(
    mut context: RenderContext,
    mut commands: Commands,
    input: Res<KernelInput>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    images: Res<RenderAssets<GpuImage>>,
    target: Res<Target>,
    prepared: Option<ResMut<Prepared>>,
    animation: Res<animation::State>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    fish_frame: Option<Res<fish_shadow::Frame>>,
    scanned_frame: Option<Res<scanned_shadow::Frame>>,
) {
    let motion = animation
        .motion
        .filter(|_| animation.sequence > animation.encoded.load(Ordering::Acquire));
    let fish_frame = fish_frame.filter(|frame| frame.target == target.image);
    let scanned_frame =
        scanned_frame.filter(|frame| frame.target == target.image && target.settings.enabled > 0.5);
    let scanned_resident = prepared.as_ref().and_then(|p| p.scanned.as_ref());
    let scanned_changed = match (scanned_frame.as_ref(), scanned_resident) {
        (Some(frame), Some((meshes, shader, batch))) => {
            !Arc::ptr_eq(&frame.meshes, meshes)
                || !Arc::ptr_eq(&frame.shader, shader)
                || frame.poses.len() != batch.draws.len()
        }
        (None, None) => false,
        _ => true,
    };
    let scanned_pose = prepared.as_ref().and_then(|p| p.scanned_pose.as_ref());
    let scanned_pose_changed = match (scanned_frame.as_ref(), scanned_pose) {
        (Some(frame), Some((poses, _))) => frame.poses != *poses,
        (None, None) => false,
        _ => true,
    };
    let scanned_index = animation
        .frame
        .as_ref()
        .map_or(input.0.settings.frame_index, |frame| {
            frame.settings.frame_index
        });
    let scanned_phase_changed = scanned_frame.as_ref().is_some_and(|frame| {
        frame.poses.iter().any(|p| p.enabled)
            && scanned_pose.is_none_or(|(_, phase)| *phase != scanned_index)
    });
    let resident_fish = prepared
        .as_ref()
        .and_then(|prepared| prepared.fish.as_ref());
    let fish_changed = match (fish_frame.as_ref(), resident_fish) {
        (Some(frame), Some((meshes, _))) => !Arc::ptr_eq(&frame.meshes, meshes),
        (None, None) => false,
        _ => true,
    };
    let fish_pose = prepared
        .as_ref()
        .and_then(|prepared| prepared.fish_pose.as_ref());
    let fish_pose_changed = match (fish_frame.as_ref(), fish_pose) {
        (Some(frame), Some((poses, _))) => frame.poses != *poses,
        (None, None) => false,
        _ => true,
    };
    let fish_clock_changed = fish_frame.as_ref().is_some_and(|frame| {
        frame.poses.iter().any(|pose| pose.enabled)
            && fish_pose.is_none_or(|(_, time)| time.to_bits() != frame.time.to_bits())
    });
    if target.ready.load(Ordering::Acquire)
        && motion.is_none()
        && !fish_changed
        && !fish_pose_changed
        && !fish_clock_changed
        && !scanned_changed
        && !scanned_pose_changed
        && !scanned_phase_changed
    {
        return;
    }
    let Some(image) = images.get(&target.image) else {
        return;
    };
    let Some(trace) = buffers.get(target.trace.id()) else {
        return;
    };
    let Some(mut prepared) = prepared.filter(|p| Arc::ptr_eq(&p.ready, &target.ready)) else {
        commands.insert_resource(prepare(&input.0, target.ready.clone(), &device, &queue));
        return;
    };
    let live = motion.and(animation.frame.as_deref());
    let recovered = motion.is_some() && animation.needs_recovery();
    // Fish and scanned visibility and LOD fades change with the camera every frame.
    // Their current poses update only the scheduled live cascades, like source
    // vegetation; forcing all layers here destroys the 1/2/4 frame cadence.
    // Without a live frame the fallback below still refreshes all three layers.
    let layers = if recovered || fish_changed || scanned_changed {
        &[0, 1, 2][..]
    } else {
        live.map_or(&[0, 1, 2][..], |frame| frame.layers.as_slice())
    };
    if let Some(frame) = live {
        let Some(receiver) = buffers.get(super::receiver::DEFAULT_BUFFER.id()) else {
            return;
        };
        update_frame(&mut prepared, frame, layers, &device, &queue);
        // This is deliberately a render-world write: material assets and their
        // bind groups stay resident, and matrices cannot precede their depth maps.
        queue.write_buffer(
            &receiver.buffer,
            0,
            &super::receiver::bytes(frame.settings.clone()),
        );
    }
    if let Some(frame) = fish_frame {
        let matrices = animation
            .frame
            .as_ref()
            .map_or(&input.0.settings.matrices[..3], |frame| {
                &frame.settings.matrices[..3]
            });
        if fish_changed {
            prepared.fish = Some((
                frame.meshes.clone(),
                fish_shadow::gpu::prepare(
                    &frame.shader,
                    &frame.meshes,
                    &frame.poses,
                    matrices,
                    frame.time,
                    &device,
                    &queue,
                )
                .expect("validated source masked fish depth"),
            ));
        }
        if let Some((_, batch)) = &mut prepared.fish {
            batch
                .update(&frame.poses, matrices, layers, frame.time, &device, &queue)
                .expect("validated source masked fish poses");
        }
        prepared.fish_pose = Some((frame.poses.clone(), frame.time));
    } else {
        prepared.fish = None;
        prepared.fish_pose = None;
    }
    if let Some(frame) = scanned_frame {
        let matrices = animation
            .frame
            .as_ref()
            .map_or(&input.0.settings.matrices[..3], |frame| {
                &frame.settings.matrices[..3]
            });
        if scanned_changed {
            prepared.scanned = Some((
                frame.meshes.clone(),
                frame.shader.clone(),
                scanned_shadow::gpu::prepare(
                    &frame.shader,
                    &frame.meshes,
                    &frame.poses,
                    matrices,
                    scanned_index,
                    &device,
                    &queue,
                )
                .expect("validated scanned source depth frame"),
            ));
        }
        if let Some((_, _, batch)) = &mut prepared.scanned {
            batch
                .update(
                    &frame.poses,
                    matrices,
                    layers,
                    scanned_index,
                    &device,
                    &queue,
                )
                .expect("validated scanned source caster poses");
        }
        prepared.scanned_pose = Some((frame.poses.clone(), scanned_index));
    } else {
        prepared.scanned = None;
        prepared.scanned_pose = None;
    }
    // This graph encodes one pose per submission. Queue writes precede its depth
    // passes and retain the uniform buffers already referenced by the bind groups.
    if let Some(motion) = motion {
        for batch in &mut prepared.plants {
            batch.update_motion(
                motion.main_camera,
                motion.wind_time,
                motion.gust_frame,
                &device,
                &queue,
            );
        }
    }
    let diagnostics = context.diagnostic_recorder();
    let recorder = diagnostics.as_deref();
    let shadow_span = recorder.time_span(context.command_encoder(), "tidewater_object_shadow");
    for &layer in layers {
        let view = image.texture.create_view(&gpu::TextureViewDescriptor {
            dimension: Some(gpu::TextureViewDimension::D2),
            base_array_layer: u32::try_from(layer).expect("three shadow layers"),
            array_layer_count: Some(1),
            ..default()
        });
        let mut pass = context
            .command_encoder()
            .begin_render_pass(&gpu::RenderPassDescriptor {
                label: Some("Tidewater native village depth"),
                color_attachments: &[],
                depth_stencil_attachment: Some(gpu::RenderPassDepthStencilAttachment {
                    view: &view,
                    depth_ops: Some(gpu::Operations {
                        load: gpu::LoadOp::Clear(1.0),
                        store: gpu::StoreOp::Store,
                    }),
                    stencil_ops: None,
                }),
                ..default()
            });
        pass.set_pipeline(&prepared.opaque.pipeline);
        for d in &prepared.opaque.draws {
            pass.set_bind_group(0, &*d.groups[layer], &[]);
            pass.set_vertex_buffer(0, *d.vertices.slice(..));
            pass.set_index_buffer(*d.indices.slice(..), gpu::IndexFormat::Uint32);
            pass.draw_indexed(0..d.count, 0, 0..1);
        }
        if let Some(t) = &prepared.terrain {
            pass.set_pipeline(&t.pipeline);
            pass.set_bind_group(0, &*t.groups[layer], &[]);
            pass.set_vertex_buffer(0, *t.grid.slice(..));
            pass.set_vertex_buffer(1, *t.nodes.slice(..));
            pass.set_index_buffer(*t.indices.slice(..), gpu::IndexFormat::Uint32);
            pass.draw_indexed(0..9600, 0, 0..t.count);
        }
        for p in &prepared.plants {
            pass.set_pipeline(&p.pipeline);
            pass.set_bind_group(0, &*p.groups[layer], &[]);
            pass.set_vertex_buffer(0, *p.vertices.slice(..));
            pass.set_index_buffer(*p.indices.slice(..), gpu::IndexFormat::Uint32);
            pass.draw_indexed(0..p.index_count, 0, 0..p.count);
        }
        if let Some((_, fish)) = &prepared.fish {
            pass.set_pipeline(&fish.pipeline);
            for draw in &fish.draws {
                if !draw.enabled {
                    continue;
                }
                pass.set_bind_group(0, &*draw.groups[layer], &[]);
                pass.set_vertex_buffer(0, *draw.vertices.slice(..));
                pass.set_index_buffer(*draw.indices.slice(..), gpu::IndexFormat::Uint32);
                pass.draw_indexed(0..draw.count, 0, 0..1);
            }
        }
        if let Some((_, _, scanned)) = &prepared.scanned {
            pass.set_pipeline(&scanned.pipeline);
            for draw in &scanned.draws {
                if !draw.enabled {
                    continue;
                }
                let geometry = &scanned.geometry[draw.asset];
                pass.set_bind_group(0, &*draw.groups[layer], &[]);
                pass.set_vertex_buffer(0, *geometry.vertices.slice(..));
                pass.set_index_buffer(*geometry.indices.slice(..), gpu::IndexFormat::Uint32);
                pass.draw_indexed(0..geometry.count, 0, 0..1);
            }
        }
    }
    shadow_span.end(context.command_encoder());
    target.ready.store(true, Ordering::Release);
    if motion.is_some() {
        let statistics = animation.record_encoded(layers, recovered);
        write_trace(statistics, trace, &queue);
    }
}

fn write_trace(statistics: animation::Statistics, trace: &GpuShaderBuffer, queue: &RenderQueue) {
    let words = [
        statistics.last_sequence,
        statistics.last_mask,
        u32::try_from(statistics.submissions).expect("submissions bounded by sequence"),
        u32::try_from(statistics.recovered).expect("recoveries bounded by sequence"),
    ];
    let bytes: Vec<_> = words.into_iter().flat_map(u32::to_le_bytes).collect();
    queue.write_buffer(&trace.buffer, 0, &bytes);
}

fn update_frame(
    prepared: &mut Prepared,
    frame: &super::live::Frame,
    layers: &[usize],
    device: &RenderDevice,
    queue: &RenderQueue,
) {
    let matrices = &frame.settings.matrices[..3];
    prepared
        .opaque
        .update_poses_and_cascades(
            matrices,
            layers,
            frame.opaque_models.as_deref(),
            device,
            queue,
        )
        .expect("validated resident opaque cascades");
    if let (Some(gpu), Some(stream)) = (&mut prepared.terrain, &frame.terrain) {
        gpu.update_stream(stream, device, queue)
            .expect("validated terrain stream");
        gpu.update_cascades(matrices, layers, device, queue)
            .expect("validated terrain cascades");
    }
    for (gpu, stream) in prepared.plants.iter_mut().zip(&frame.plants) {
        gpu.update_stream(stream, queue)
            .expect("validated plant stream");
        gpu.update_cascades(matrices, &frame.eyes, layers, device, queue)
            .expect("validated plant cascades");
    }
}
