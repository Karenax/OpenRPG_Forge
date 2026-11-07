use super::*;
use forge_core::bytes_sha256;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument, TerrainExtent,
    TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};
use forge_world::sampled_terrain::SampledTerrainPlan;

fn fixture(width: u16, height: u16, spacing: f32) -> SampledTerrainPlan {
    let bytes: Vec<_> = (0..usize::from(width) * usize::from(height))
        .flat_map(|_| 4.0_f32.to_le_bytes())
        .collect();
    let mut doc = new_terrain("hill-shadow", "authoring", "Hill shadow").unwrap();
    doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    doc.extent_m = TerrainExtent::new(f32::from(width) * spacing, f32::from(height) * spacing);
    doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    doc.height_sampling.sample_spacing_m = spacing;
    doc.height_sampling.base_elevation_m = 0.0;
    doc.height_sampling.amplitude_m = 0.0;
    doc.height_sampling.base_frequency_cycles_per_m = 1.0;
    doc.height_sampling.octaves = 1;
    doc.height_sampling.lacunarity = 2.0;
    doc.height_sampling.persistence = 0.5;
    doc.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: u32::from(width),
        height: u32::from(height),
        origin_xz_m: [
            -f64::from(doc.extent_m.size_x_m) * 0.5,
            -f64::from(doc.extent_m.size_z_m) * 0.5,
        ],
        sample_spacing_m: f64::from(spacing),
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&bytes),
    });
    SampledTerrainPlan::from_document(&doc, &bytes).unwrap()
}

fn half_at(bytes: &[u8], x: usize, z: usize) -> u16 {
    let offset = (z * 512 + x) * 2;
    u16::from_le_bytes(bytes[offset..offset + 2].try_into().unwrap())
}

#[test]
fn rectangular_field_keeps_world_scale_outside_height_and_authored_edits() {
    let plan = fixture(1024, 512, 1.0);
    let (mips, origin, size) = heights::for_plan(&plan).unwrap();
    assert_eq!((origin, size), (-512.0, 1024.0));
    assert_eq!(mips.len(), 699_050);
    assert_eq!(half_at(&mips, 100, 0), 0xd5a0); // -90 outside the rectangle
    assert_eq!(half_at(&mips, 100, 127), 0xd5a0);
    assert_eq!(half_at(&mips, 100, 128), 0x4400); // 4 inside, not stretched
    assert_eq!(half_at(&mips, 100, 383), 0x4400);
    assert_eq!(half_at(&mips, 100, 384), 0xd5a0);

    let mut edited = plan.document().clone();
    edited
        .height_deltas
        .push(TerrainHeightDelta::new(100, 100, 8.0));
    let edited = plan.with_document(&edited).unwrap();
    let (changed, _, _) = heights::for_plan(&edited).unwrap();
    assert_eq!(half_at(&changed, 49, 177), 0x4480); // 4.5, averaged from four samples
    assert_eq!(half_at(&mips, 49, 177), 0x4400);
    assert_eq!(half_at(&changed, 100, 0), 0xd5a0);

    // The odd source height crosses the square-grid texel boundary.
    // This texel combines 75% terrain at 4 m and 25% outside at -90 m.
    let odd = fixture(1024, 511, 1.0);
    let (odd_mips, _, _) = heights::for_plan(&odd).unwrap();
    assert_eq!(half_at(&odd_mips, 100, 128), 0xcce0); // -19.5

    let prepared = PreparedHeights::from_plan(&edited).unwrap();
    assert!(
        prepared
            .shader
            .contains("var t = terrainParams.size * 0.01171875;")
    );
    assert!(
        prepared
            .shader
            .contains("var dt = terrainParams.size * 0.0029296875;")
    );
    assert!(
        prepared
            .shader
            .contains("dt * (512.0 / terrainParams.size)")
    );
    assert!(prepared.shader.contains("var hq = -90.0;"));
    let module = naga::front::wgsl::parse_str(&prepared.shader).unwrap();
    naga::valid::Validator::new(
        naga::valid::ValidationFlags::all(),
        naga::valid::Capabilities::all(),
    )
    .validate(&module)
    .unwrap();
    assert!(prepared.with_sun(Vec3::Y).is_ok());
}

#[test]
fn small_domain_uses_bilinear_terrain_and_outside_elevation() {
    let plan = fixture(11, 19, 0.5);
    let mut document = plan.document().clone();
    document.height_sampling.base_elevation_m = 2.0;
    let plan = plan.with_document(&document).unwrap();
    let (mips, origin, size) = heights::for_plan(&plan).unwrap();
    assert_eq!((origin, size), (-4.75, 9.5));
    assert_eq!(half_at(&mips, 256, 256), 0x4600); // 6 inside
    assert_eq!(half_at(&mips, 0, 256), 0xd580); // -88 outside
    let shader = PreparedHeights::from_plan(&plan).unwrap().shader;
    assert!(shader.contains("var hq = -88.0;"));
}

#[test]
fn reference_mips_and_kernel_stay_byte_identical() {
    let plan = fixture(2048, 2048, 1.0);
    let expected = mip_chain(&plan.cell_center_heights()).unwrap();
    let field = PreparedHeights::from_plan(&plan).unwrap();
    assert_eq!((field.origin, field.size), (-1024.0, 2048.0));
    assert_eq!(field.bytes, expected);
    assert_eq!(field.shader, SOURCE_KERNEL);
    assert!(field.with_sun(Vec3::splat(f32::NAN)).is_err());

    // Sharing the same maximum extent alone must not activate the legacy path.
    let rectangle = fixture(1024, 2048, 1.0);
    let field = PreparedHeights::from_plan(&rectangle).unwrap();
    assert_eq!(field.size.to_bits(), 2048.0_f32.to_bits());
    assert_ne!(field.shader, SOURCE_KERNEL);
    assert!(field.shader.contains("var hq = -90.0;"));
}

#[test]
#[ignore = "requires the explicitly pinned Tidewater source terrain"]
fn real_reference_heightfield_matches_the_original_mip_hash() {
    let root = std::path::PathBuf::from(std::env::var_os("FORGE_HILL_REFERENCE_ROOT").unwrap());
    let zone =
        std::fs::read(root.join("_forge-export/zones/3d5bc933-f29e-53fe-8afa-5d312baa58b4.json"))
            .unwrap();
    assert_eq!(
        bytes_sha256(&zone).to_string(),
        "974e68446657785820e48b69fa7ced9a535d685d55afdf9b97d4b0236f5d1570"
    );
    let archive: serde_json::Value = serde_json::from_slice(&zone).unwrap();
    let document: TerrainDocument =
        serde_json::from_value(archive["head"]["base"]["terrain"].clone()).unwrap();
    let heights = std::fs::read(root.join("_forge-export/source-blobs/be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3.bin")).unwrap();
    assert_eq!(
        bytes_sha256(&heights).to_string(),
        "be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3"
    );
    let plan = SampledTerrainPlan::from_document(&document, &heights).unwrap();
    let field = PreparedHeights::from_plan(&plan).unwrap();
    assert_eq!((field.origin, field.size), (-1024.0, 2048.0));
    assert_eq!(field.shader, SOURCE_KERNEL);
    assert_eq!(
        bytes_sha256(&field.bytes).to_string(),
        "9dd52cf258819846d97e8a1234066c90dfd17de0ef860f242c660d6a8a2fd18d"
    );
}

#[test]
#[ignore = "requires a hardware GPU"]
#[allow(clippy::too_many_lines)]
fn rectangular_hill_shadow_kernel_uses_world_scale_and_outside_height_on_gpu() {
    use wgpu::util::DeviceExt;
    bevy::tasks::block_on(async {
        let plan = fixture(1024, 512, 1.0);
        let light = Vec3::new(
            std::f32::consts::FRAC_1_SQRT_2,
            std::f32::consts::FRAC_1_SQRT_2,
            0.0,
        );
        let input = PreparedHeights::from_plan(&plan)
            .unwrap()
            .with_sun(light)
            .unwrap();
        let instance = wgpu::Instance::new(wgpu::InstanceDescriptor::new_without_display_handle());
        let adapter = instance
            .request_adapter(&wgpu::RequestAdapterOptions {
                power_preference: wgpu::PowerPreference::HighPerformance,
                force_fallback_adapter: false,
                compatible_surface: None,
            })
            .await
            .unwrap();
        let info = adapter.get_info();
        assert_ne!(info.device_type, wgpu::DeviceType::Cpu);
        eprintln!(
            "GPU: {} ({:?}, {:?})",
            info.name, info.backend, info.device_type
        );
        let (device, queue) = adapter
            .request_device(&wgpu::DeviceDescriptor::default())
            .await
            .unwrap();
        let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
            label: Some("prepared rectangular hill shadow"),
            source: wgpu::ShaderSource::Wgsl(input.shader.into()),
        });
        let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
            label: Some("rectangular hill shadow"),
            layout: None,
            module: &shader,
            entry_point: Some("main"),
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            cache: None,
        });
        let extent = wgpu::Extent3d {
            width: 512,
            height: 512,
            depth_or_array_layers: 1,
        };
        let heights = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("prepared rectangular height mips"),
            size: extent,
            mip_level_count: 10,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::R16Float,
            usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
            view_formats: &[],
        });
        let mut offset = 0_usize;
        for level in 0..10_u32 {
            let width = 512_u32 >> level;
            let len = usize::try_from(width * width * 2).unwrap();
            queue.write_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &heights,
                    mip_level: level,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                &input.bytes[offset..offset + len],
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(width * 2),
                    rows_per_image: Some(width),
                },
                wgpu::Extent3d {
                    width,
                    height: width,
                    depth_or_array_layers: 1,
                },
            );
            offset += len;
        }
        assert_eq!(offset, input.bytes.len());
        let output = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("hill shadow output"),
            size: extent,
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba16Float,
            usage: wgpu::TextureUsages::STORAGE_BINDING | wgpu::TextureUsages::COPY_SRC,
            view_formats: &[],
        });
        let height_view = heights.create_view(&wgpu::TextureViewDescriptor::default());
        let output_view = output.create_view(&wgpu::TextureViewDescriptor::default());
        let sampler = device.create_sampler(&wgpu::SamplerDescriptor {
            label: Some("linear height sampler"),
            address_mode_u: wgpu::AddressMode::ClampToEdge,
            address_mode_v: wgpu::AddressMode::ClampToEdge,
            mag_filter: wgpu::FilterMode::Linear,
            min_filter: wgpu::FilterMode::Linear,
            mipmap_filter: wgpu::MipmapFilterMode::Linear,
            ..Default::default()
        });
        let params: Vec<_> = [
            input.settings.origin,
            input.settings.size,
            0.0,
            0.0,
            light.x,
            light.y,
            light.z,
            0.0,
        ]
        .into_iter()
        .flat_map(f32::to_le_bytes)
        .collect();
        let uniform = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("hill domain and sun"),
            contents: &params,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        let layout = pipeline.get_bind_group_layout(0);
        let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("hill bindings"),
            layout: &layout,
            entries: &[
                wgpu::BindGroupEntry {
                    binding: 0,
                    resource: wgpu::BindingResource::TextureView(&height_view),
                },
                wgpu::BindGroupEntry {
                    binding: 1,
                    resource: wgpu::BindingResource::Sampler(&sampler),
                },
                wgpu::BindGroupEntry {
                    binding: 2,
                    resource: wgpu::BindingResource::TextureView(&output_view),
                },
                wgpu::BindGroupEntry {
                    binding: 3,
                    resource: uniform.as_entire_binding(),
                },
            ],
        });
        let readback = device.create_buffer(&wgpu::BufferDescriptor {
            label: Some("hill readback"),
            size: 512 * 512 * 8,
            usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
            mapped_at_creation: false,
        });
        let mut encoder = device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
        {
            let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
            pass.set_pipeline(&pipeline);
            pass.set_bind_group(0, &group, &[]);
            pass.dispatch_workgroups(64, 64, 1);
        }
        encoder.copy_texture_to_buffer(
            wgpu::TexelCopyTextureInfo {
                texture: &output,
                mip_level: 0,
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            wgpu::TexelCopyBufferInfo {
                buffer: &readback,
                layout: wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(4096),
                    rows_per_image: Some(512),
                },
            },
            extent,
        );
        let submission = queue.submit([encoder.finish()]);
        let (tx, rx) = std::sync::mpsc::channel();
        readback
            .slice(..)
            .map_async(wgpu::MapMode::Read, move |r| tx.send(r).unwrap());
        device
            .poll(wgpu::PollType::Wait {
                submission_index: Some(submission),
                timeout: Some(std::time::Duration::from_secs(30)),
            })
            .unwrap();
        rx.recv_timeout(std::time::Duration::from_secs(5))
            .unwrap()
            .unwrap();
        let data = readback.slice(..).get_mapped_range();
        let read = |x: usize, z: usize, channel: usize| {
            let i = (z * 512 + x) * 8 + channel * 2;
            u16::from_le_bytes([data[i], data[i + 1]])
        };
        let half = |bits: u16| {
            let sign = if bits & 0x8000 == 0 { 1.0 } else { -1.0 };
            let exponent = (bits >> 10) & 0x1f;
            let fraction = f32::from(bits & 0x03ff) / 1024.0;
            match exponent {
                0 => sign * fraction * 2.0_f32.powi(-14),
                31 => f32::NAN,
                _ => sign * (1.0 + fraction) * 2.0_f32.powi(i32::from(exponent) - 15),
            }
        };
        let cases = [
            (256, 256, -8.0_f32), // 4 m terrain minus 12 m ray rise
            (256, 0, -102.0),     // -90 m outside minus the ray rise
            (511, 256, -102.0),   // ray exits the long axis; do not clamp to terrain
        ];
        for (x, z, expected) in cases {
            let top = half(read(x, z, 0));
            let distance = half(read(x, z, 1));
            assert!(
                (top - expected).abs() <= 0.125,
                "top at {x},{z}: {top}, expected {expected}"
            );
            assert!(
                (distance - 12.0).abs() <= 0.125,
                "distance at {x},{z}: {distance}"
            );
        }
        eprintln!(
            "verified three 1024 x 512 m samples within 0.125 m of the expected hill-shadow values"
        );
    });
}
