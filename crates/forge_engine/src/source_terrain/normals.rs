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

/// Preserve the admitted fixed source's sea-level-zero AO behavior.
pub fn update_base(
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    bytes: &mut [u8],
) -> Result<(), String> {
    let source = plan.raster().source();
    if !super::supports_fixed_source_raster(source) {
        return Err("Source normal bake requires its admitted fixed raster profile".into());
    }
    update_base_with_water_level(plan, bytes, Some(0.0))
}

/// Recompute the base normal/AO level. `water_level_m`, when present, enables
/// the source's deep-water AO cutoff relative to that module's own water level.
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
pub fn update_base_with_water_level(
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    bytes: &mut [u8],
    water_level_m: Option<f64>,
) -> Result<(), String> {
    if water_level_m.is_some_and(|level| !level.is_finite()) {
        return Err("Nonfinite terrain AO water level".into());
    }
    let doc = plan.document();
    let source = doc
        .sampled_source
        .as_ref()
        .ok_or("Missing terrain raster")?;
    super::textures::validate(bytes, [source.width, source.height])?;
    let heights = plan.cell_center_heights();
    if heights.iter().any(|h| !h.is_finite()) {
        return Err("Nonfinite authored terrain height".into());
    }
    let width = source.width as usize;
    let height = source.height as usize;
    let inverse_step = 0.5 / source.sample_spacing_m;
    let mut changed = 0;
    for z in 0..height {
        for x in 0..width {
            let hx = (f64::from(heights[z * width + (x + 1).min(width - 1)])
                - f64::from(heights[z * width + x.saturating_sub(1)]))
                * inverse_step;
            let hz = (f64::from(heights[(z + 1).min(height - 1) * width + x])
                - f64::from(heights[z.saturating_sub(1) * width + x]))
                * inverse_step;
            let inverse = 1.0 / (hx * hx + 1.0 + hz * hz).sqrt();
            let rg = [
                ((-hx * inverse * 0.5 + 0.5) * 255.0 + 0.5) as u8,
                ((-hz * inverse * 0.5 + 0.5) * 255.0 + 0.5) as u8,
            ];
            let offset = (z * width + x) * 4;
            changed += usize::from(bytes[offset..offset + 2] != rg);
            bytes[offset..offset + 2].copy_from_slice(&rg);
        }
    }
    let ao = ao::bake(
        &heights,
        width,
        height,
        source.sample_spacing_m,
        water_level_m,
    );
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
        forge_core::bytes_sha256(&bytes[..width * height * 4])
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
        Self::stage_with_dimensions(bytes, [2048, 2048], edited, images)
    }

    /// Stage a validated complete chain for the admitted terrain dimensions.
    pub fn stage_with_dimensions(
        bytes: &[u8],
        size: [u32; 2],
        edited: bool,
        images: &mut Assets<Image>,
    ) -> Result<Self, String> {
        let image = super::textures::normal_image_with_dimensions(bytes, size)?;
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
        label: Some("Source normal mipmaps"),
        source: gpu::ShaderSource::Wgsl(MIPS.into()),
    });
    let pipeline = device.create_render_pipeline(&gpu::RawRenderPipelineDescriptor {
        label: Some("Source normal mipmaps"),
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
    for level in 1..image.texture.mip_level_count() {
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
    eprintln!(
        "authored terrain normal mipmaps: {} native passes complete",
        prepared.steps.len()
    );
}

pub(crate) const MIPS: &str = r"
struct VSOut { @builtin(position) pos:vec4f, @location(0) uv:vec2f };
@vertex fn vs(@builtin(vertex_index) i:u32)->VSOut {
    let p=vec2f(f32((i<<1u)&2u),f32(i&2u));
    var o:VSOut; o.pos=vec4f(p*2.0-1.0,0.0,1.0); o.uv=vec2f(p.x,1.0-p.y); return o;
}
@group(0) @binding(0) var src:texture_2d<f32>;
@group(0) @binding(1) var smp:sampler;
@fragment fn fs(input:VSOut)->@location(0) vec4f { return textureSampleLevel(src,smp,input.uv,0.0); }
";

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        authoring::new_terrain,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };
    use forge_world::sampled_terrain::SampledTerrainPlan;

    fn plan(spacing: f32, flat: bool) -> SampledTerrainPlan {
        let mut doc = new_terrain("normal-test", "authoring", "Rectangular terrain").unwrap();
        doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        doc.extent_m = TerrainExtent::new(7.0 * spacing, 5.0 * spacing);
        doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        doc.height_sampling.sample_spacing_m = spacing;
        doc.height_sampling.base_elevation_m = 0.0;
        doc.height_sampling.amplitude_m = 0.0;
        doc.height_sampling.base_frequency_cycles_per_m = 1.0;
        doc.height_sampling.octaves = 1;
        doc.height_sampling.lacunarity = 2.0;
        doc.height_sampling.persistence = 0.5;
        let bytes: Vec<_> = (0_u16..5)
            .flat_map(|z| {
                (0_u16..7).map(move |x| {
                    if flat {
                        10.0
                    } else {
                        (2.0 * f32::from(x) + 3.0 * f32::from(z)) * spacing
                    }
                })
            })
            .flat_map(f32::to_le_bytes)
            .collect();
        doc.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 7,
            height: 5,
            origin_xz_m: [-3.5 * f64::from(spacing), -2.5 * f64::from(spacing)],
            sample_spacing_m: f64::from(spacing),
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&bytes),
        });
        SampledTerrainPlan::from_document(&doc, &bytes).unwrap()
    }

    #[test]
    fn rectangular_normals_use_physical_spacing_and_preserve_other_channels() {
        for spacing in [0.5, 2.0] {
            let plan = plan(spacing, false);
            let mut chain = vec![77_u8; 168]; // 7x5, 3x2, 1x1 RGBA8.
            update_base_with_water_level(&plan, &mut chain, None).unwrap();
            let offset = (7 + 2) * 4;
            assert_eq!(&chain[offset..offset + 3], &[59, 25, 77]);
            assert!(chain[140..].iter().all(|&value| value == 77));
            let mut invalid = vec![91_u8; 167];
            assert!(update_base_with_water_level(&plan, &mut invalid, None).is_err());
            assert!(invalid.iter().all(|&value| value == 91));
            let mut invalid_water = vec![91_u8; 168];
            assert!(
                update_base_with_water_level(&plan, &mut invalid_water, Some(f64::NAN)).is_err()
            );
            assert!(invalid_water.iter().all(|&value| value == 91));
            assert!(update_base(&plan, &mut invalid_water).is_err());
            assert!(invalid_water.iter().all(|&value| value == 91));
        }
    }

    #[test]
    fn flat_rectangular_terrain_has_unoccluded_ao_even_with_partial_blocks() {
        let plan = plan(0.5, true);
        let mut chain = vec![0_u8; 168];
        update_base_with_water_level(&plan, &mut chain, None).unwrap();
        for pixel in chain[..140].as_chunks::<4>().0 {
            assert_eq!(pixel, &[128, 128, 0, 255]);
        }
    }
}
