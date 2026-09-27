//! Shared three-cascade depth graph with generation-scoped late adoption.
#![allow(clippy::needless_pass_by_value)]
use super::{OpaqueDraw, plants, terrain};
use crate::tidewater_material::ObjectShadowSettings as Settings;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource as gpu,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderQueue},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

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
    ));
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        draw.before(camera_driver)
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
    let target = Target {
        settings: input.settings.clone(),
        image,
        ready: Arc::new(AtomicBool::new(false)),
    };
    world.insert_resource(KernelInput(Arc::new(input)));
    world.insert_resource(target.clone());
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
        opaque: super::prepare(&input.draws, &input.matrices, device, queue)
            .expect("validated opaque casters and cascades"),
        plants: input.plants.as_ref().map_or_else(Vec::new, |p| {
            plants::prepare(p, &input.matrices, &input.eyes, device, queue)
                .expect("validated plant shadows")
        }),
        terrain: input.terrain.as_ref().filter(|t| t.count > 0).map(|t| {
            terrain::prepare(t, &input.matrices, device, queue).expect("validated terrain shadows")
        }),
    }
}

#[allow(clippy::too_many_arguments)]
fn draw(
    mut context: RenderContext,
    mut commands: Commands,
    input: Res<KernelInput>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    images: Res<RenderAssets<GpuImage>>,
    target: Res<Target>,
    prepared: Option<Res<Prepared>>,
) {
    if target.ready.load(Ordering::Acquire) {
        return;
    }
    let Some(image) = images.get(&target.image) else {
        return;
    };
    let Some(prepared) = prepared.filter(|p| Arc::ptr_eq(&p.ready, &target.ready)) else {
        commands.insert_resource(prepare(&input.0, target.ready.clone(), &device, &queue));
        return;
    };
    for layer in 0..3 {
        let view = image.texture.create_view(&gpu::TextureViewDescriptor {
            dimension: Some(gpu::TextureViewDimension::D2),
            base_array_layer: layer,
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
            pass.set_bind_group(0, &*d.groups[layer as usize], &[]);
            pass.set_vertex_buffer(0, *d.vertices.slice(..));
            pass.set_index_buffer(*d.indices.slice(..), gpu::IndexFormat::Uint32);
            pass.draw_indexed(0..d.count, 0, 0..1);
        }
        if let Some(t) = &prepared.terrain {
            pass.set_pipeline(&t.pipeline);
            pass.set_bind_group(0, &*t.groups[layer as usize], &[]);
            pass.set_vertex_buffer(0, *t.grid.slice(..));
            pass.set_vertex_buffer(1, *t.nodes.slice(..));
            pass.set_index_buffer(*t.indices.slice(..), gpu::IndexFormat::Uint32);
            pass.draw_indexed(0..9600, 0, 0..t.count);
        }
        for p in &prepared.plants {
            pass.set_pipeline(&p.pipeline);
            pass.set_bind_group(0, &*p.groups[layer as usize], &[]);
            pass.set_vertex_buffer(0, *p.vertices.slice(..));
            pass.set_index_buffer(*p.indices.slice(..), gpu::IndexFormat::Uint32);
            pass.draw_indexed(0..p.index_count, 0, 0..p.count);
        }
    }
    target.ready.store(true, Ordering::Release);
}
