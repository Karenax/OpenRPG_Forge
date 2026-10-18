// SPDX-License-Identifier: MIT
// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
// TerrainBake.js / Mipmaps.js adaptation, Tidewater DRG Software Solutions LLC.
//! Authored terrain normal/AO bake and generation-scoped GPU mip chain.
//! Readiness means commands encoded, not a GPU completion fence.
#![allow(clippy::needless_pass_by_value)]
mod ao;
use bevy::{
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::render_resource as gpu,
    render::{
        Render, RenderApp, RenderSystems,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        render_resource::{BindGroup, BindGroupEntries, RenderPipeline, TextureView},
        renderer::{RenderContext, RenderDevice, RenderGraph},
        texture::GpuImage,
    },
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)] // Normal encoding is bounded [0,255].
pub fn update_base(
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    bytes: &mut [u8],
) -> Result<(), String> {
    let doc = plan.document();
    let source = doc
        .sampled_source
        .as_ref()
        .ok_or("Missing terrain raster")?;
    if source.width != 2048
        || source.height != 2048
        || doc.extent_m.size_x_m.to_bits() != 2048.0_f32.to_bits()
        || doc.extent_m.size_z_m.to_bits() != 2048.0_f32.to_bits()
        || doc.height_sampling.sample_spacing_m.to_bits() != 1.0_f32.to_bits()
        || bytes.len() != 22_369_620
    {
        return Err(
            "Normal bake requires a 2048 unit-grid terrain and full RGBA8 mip chain".into(),
        );
    }
    let heights = plan.cell_center_heights();
    if heights.iter().any(|h| !h.is_finite()) {
        return Err("Nonfinite authored terrain height".into());
    }
    let side = 2048_usize;
    let mut changed = 0;
    for z in 0..side {
        for x in 0..side {
            let hx = (f64::from(heights[z * side + (x + 1).min(side - 1)])
                - f64::from(heights[z * side + x.saturating_sub(1)]))
                * 0.5;
            let hz = (f64::from(heights[(z + 1).min(side - 1) * side + x])
                - f64::from(heights[z.saturating_sub(1) * side + x]))
                * 0.5;
            let inverse = 1.0 / (hx * hx + 1.0 + hz * hz).sqrt();
            let rg = [
                ((-hx * inverse * 0.5 + 0.5) * 255.0 + 0.5) as u8,
                ((-hz * inverse * 0.5 + 0.5) * 255.0 + 0.5) as u8,
            ];
            let offset = (z * side + x) * 4;
            changed += usize::from(bytes[offset..offset + 2] != rg);
            bytes[offset..offset + 2].copy_from_slice(&rg);
        }
    }
    let ao = ao::bake(&heights);
    let mut ao_changed = 0;
    for (index, value) in ao.iter().copied().enumerate() {
        ao_changed += usize::from(bytes[index * 4 + 3] != value);
        bytes[index * 4 + 3] = value;
    }
    eprintln!(
        "authored terrain AO: changed_texels={ao_changed}, ao_sha256={}",
        forge_core::bytes_sha256(&ao)
    );
    eprintln!(
        "authored terrain normals: changed_texels={changed}, base_sha256={}",
        forge_core::bytes_sha256(&bytes[..side * side * 4])
    );
    Ok(())
}

#[derive(Resource, Clone, ExtractResource)]
pub struct Shared {
    pub image: Handle<Image>,
    edited: bool,
    pub ready: Arc<AtomicBool>,
}

impl Shared {
    /// Allocate a fresh image and generation for each edit; old GPU work cannot mark it ready.
    pub fn stage(bytes: &[u8], edited: bool, images: &mut Assets<Image>) -> Result<Self, String> {
        let image = super::textures::normal_image(bytes)?;
        Ok(Self {
            image: images.add(image),
            edited,
            ready: Arc::new(AtomicBool::new(!edited)),
        })
    }

    /// Explicit disabled target for scene modes without terrain.
    pub fn disabled(images: &mut Assets<Image>) -> Self {
        Self {
            image: images.add(Image::default()),
            edited: false,
            ready: Arc::new(AtomicBool::new(true)),
        }
    }
}

#[derive(Resource)]
struct Registered;

/// Register before the render app starts; payloads can be adopted or replaced later.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() || !app.world().contains_resource::<Assets<Image>>() {
        return Err("Terrain normals require initialized renderer image assets".into());
    }
    app.add_plugins(ExtractResourcePlugin::<Shared>::default());
    app.sub_app_mut(RenderApp)
        .add_systems(Render, prepare.in_set(RenderSystems::PrepareBindGroups))
        .add_systems(RenderGraph, generate.before(camera_driver));
    app.insert_resource(Registered);
    Ok(())
}

#[derive(Resource)]
struct Prepared {
    pipeline: RenderPipeline,
    steps: Vec<(BindGroup, TextureView)>,
    ready: Arc<AtomicBool>,
}

#[allow(clippy::too_many_lines)] // Explicit source-equivalent render pipeline and per-mip views.
fn prepare(
    mut commands: Commands,
    shared: Option<Res<Shared>>,
    gpu: Res<RenderAssets<GpuImage>>,
    device: Res<RenderDevice>,
    existing: Option<Res<Prepared>>,
) {
    let Some(shared) = shared.filter(|s| s.edited) else {
        commands.remove_resource::<Prepared>();
        return;
    };
    if existing
        .as_ref()
        .is_some_and(|p| Arc::ptr_eq(&p.ready, &shared.ready))
    {
        return;
    }
    // Invalidate previous generation even while the new image awaits GPU upload.
    commands.remove_resource::<Prepared>();
    let Some(image) = gpu.get(&shared.image) else {
        return;
    };
    let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
        label: Some("Tidewater normal mipmaps"),
        source: gpu::ShaderSource::Wgsl(MIPS.into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Tidewater normal mipmaps"),
        layout: None,
        vertex: gpu::RawVertexState {
            module: &shader,
            entry_point: Some("vs"),
            compilation_options: default(),
            buffers: &[],
        },
        fragment: Some(gpu::RawFragmentState {
            module: &shader,
            entry_point: Some("fs"),
            compilation_options: default(),
            targets: &[Some(gpu::ColorTargetState {
                format: gpu::TextureFormat::Rgba8Unorm,
                blend: None,
                write_mask: gpu::ColorWrites::RED
                    | gpu::ColorWrites::GREEN
                    | gpu::ColorWrites::ALPHA,
            })],
        }),
        primitive: default(),
        depth_stencil: None,
        multisample: default(),
        multiview_mask: None,
        cache: None,
    });
    let layout: bevy::render::render_resource::BindGroupLayout =
        pipeline.get_bind_group_layout(0).into();
    let mut steps = Vec::new();
    for level in 1..12 {
        let view = |mip| {
            image.texture.create_view(&gpu::TextureViewDescriptor {
                dimension: Some(gpu::TextureViewDimension::D2),
                base_mip_level: mip,
                mip_level_count: Some(1),
                ..default()
            })
        };
        let src = view(level - 1);
        let dst = view(level);
        let binding = device.create_bind_group(
            "terrain normal mip",
            &layout,
            &BindGroupEntries::sequential((&src, &image.sampler)),
        );
        steps.push((binding, dst));
    }
    commands.insert_resource(Prepared {
        pipeline,
        steps,
        ready: shared.ready.clone(),
    });
}

fn generate(
    mut context: RenderContext,
    prepared: Option<Res<Prepared>>,
    shared: Option<Res<Shared>>,
) {
    let Some(prepared) = prepared else {
        return;
    };
    if !shared.is_some_and(|s| s.edited && Arc::ptr_eq(&s.ready, &prepared.ready))
        || prepared.ready.load(Ordering::Acquire)
    {
        return;
    }
    for (binding, target) in &prepared.steps {
        let attachments = [Some(gpu::RenderPassColorAttachment {
            view: target,
            depth_slice: None,
            resolve_target: None,
            ops: gpu::Operations {
                load: gpu::LoadOp::Load,
                store: gpu::StoreOp::Store,
            },
        })];
        let mut pass = context
            .command_encoder()
            .begin_render_pass(&gpu::RenderPassDescriptor {
                label: Some("terrain normal mip"),
                color_attachments: &attachments,
                ..default()
            });
        pass.set_pipeline(&prepared.pipeline);
        pass.set_bind_group(0, binding, &[]);
        pass.draw(0..3, 0..1);
    }
    prepared.ready.store(true, Ordering::Release);
    eprintln!("authored terrain normal mipmaps: 11 native passes complete");
}

const MIPS: &str = r"
struct VSOut { @builtin(position) pos:vec4f, @location(0) uv:vec2f };
@vertex fn vs(@builtin(vertex_index) i:u32)->VSOut {
    let p=vec2f(f32((i<<1u)&2u),f32(i&2u));
    var o:VSOut; o.pos=vec4f(p*2.0-1.0,0.0,1.0); o.uv=vec2f(p.x,1.0-p.y); return o;
}
@group(0) @binding(0) var src:texture_2d<f32>;
@group(0) @binding(1) var smp:sampler;
@fragment fn fs(input:VSOut)->@location(0) vec4f { return textureSampleLevel(src,smp,input.uv,0.0); }
";
