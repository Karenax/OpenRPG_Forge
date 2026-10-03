//! Execute GGX and SH on an acquired real source cube, without substituting radiance.
//! Optional original mip generation; sky faces and a moving Studio remain separate work.
use bevy::render::{
    render_resource::ComputePipeline,
    renderer::{RenderDevice, RenderQueue},
};
use forge_core::bytes_sha256;
use serde_json::{Value, json};
use std::{
    error::Error,
    path::Path,
    sync::mpsc,
    time::{Duration, Instant},
};
type Prepared = (Value, ComputePipeline, Vec<wgpu::BindGroupLayout>);
type CubeRead = (u32, u32, u32, wgpu::Buffer);

fn copy_cube(
    gpu: &wgpu::Device,
    encoder: &mut wgpu::CommandEncoder,
    texture: &wgpu::Texture,
    levels: u32,
) -> Vec<CubeRead> {
    (0..levels)
        .map(|level| {
            let size = 128_u32 >> level;
            let row = (size * 8).div_ceil(256) * 256;
            let buffer = gpu.create_buffer(&wgpu::BufferDescriptor {
                label: Some("generated source cube readback"),
                size: u64::from(row * size * 6),
                usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                mapped_at_creation: false,
            });
            encoder.copy_texture_to_buffer(
                wgpu::TexelCopyTextureInfo {
                    texture,
                    mip_level: level,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                wgpu::TexelCopyBufferInfo {
                    buffer: &buffer,
                    layout: wgpu::TexelCopyBufferLayout {
                        offset: 0,
                        bytes_per_row: Some(row),
                        rows_per_image: Some(size),
                    },
                },
                wgpu::Extent3d {
                    width: size,
                    height: size,
                    depth_or_array_layers: 6,
                },
            );
            (level, size, row, buffer)
        })
        .collect()
}

fn compare_source_cube(
    root: &Path,
    pins: &Value,
    reference: &[Value],
    reads: Vec<CubeRead>,
    device: &RenderDevice,
) -> Result<Value, Box<dyn Error>> {
    let mut faces = Vec::new();
    let mut equal = 0;
    let mut max_error = 0_f64;
    for (level, size, row, buffer) in reads {
        let bytes = mapped(&buffer, device)?;
        for face in 0..6 {
            let mut compact = Vec::new();
            for y in 0..size {
                let offset = usize::try_from((face * size + y) * row)?;
                compact.extend_from_slice(&bytes[offset..offset + usize::try_from(size * 8)?]);
            }
            let name = reference[usize::try_from(level)?]["faces"][usize::try_from(face)?]
                .as_str()
                .ok_or("source reference face missing")?;
            let original = read(root, pins, name)?;
            if original.len() != compact.len() {
                return Err("source reference face length differs".into());
            }
            let mut error = 0_f64;
            let mut changed = 0_usize;
            let mut rgb_errors = Vec::new();
            let mut reference_peak = 0_f64;
            let mut reference_energy = 0_f64;
            let mut error_energy = 0_f64;
            for (index, (a, b)) in compact
                .chunks_exact(2)
                .zip(original.chunks_exact(2))
                .enumerate()
            {
                let a = half(u16::from_le_bytes([a[0], a[1]]));
                let b = half(u16::from_le_bytes([b[0], b[1]]));
                if !a.is_finite() || !b.is_finite() {
                    return Err("non-finite source cube output".into());
                }
                if index % 4 == 3 {
                    if a != 1.0 || b != 1.0 {
                        return Err("source cube alpha differs from one".into());
                    }
                } else {
                    let delta = (a - b).abs();
                    error = error.max(delta);
                    rgb_errors.push(delta);
                    reference_peak = reference_peak.max(b.abs());
                    reference_energy += b * b;
                    error_energy += delta * delta;
                }
                changed += usize::from(a != b);
            }
            equal += usize::from(compact == original);
            max_error = max_error.max(error);
            rgb_errors.sort_by(f64::total_cmp);
            let count = u32::try_from(rgb_errors.len())?;
            if count == 0 {
                return Err("empty source cube RGB payload".into());
            }
            let percentile_95 = rgb_errors[(rgb_errors.len() - 1) * 95 / 100];
            faces.push(json!({"level":level,"face":face,"reference":name,"sha256":bytes_sha256(&compact),"reference_sha256":bytes_sha256(&original),"byte_identical":compact==original,"changed_channels":changed,"max_rgb_absolute_error":error,"mean_rgb_absolute_error":rgb_errors.iter().sum::<f64>() / f64::from(count),"p95_rgb_absolute_error":percentile_95,"rms_rgb_absolute_error":(error_energy / f64::from(count)).sqrt(),"reference_rgb_peak":reference_peak,"relative_rgb_l2_error":if reference_energy > 0.0 {Some((error_energy / reference_energy).sqrt())} else {None}}));
        }
    }
    Ok(
        json!({"compared_faces":faces.len(),"byte_identical_faces":equal,"max_rgb_absolute_error":max_error,"faces":faces}),
    )
}

fn read(root: &Path, pins: &Value, name: &str) -> Result<Vec<u8>, Box<dyn Error>> {
    if name.contains(['/', '\\']) || name == "." || name == ".." {
        return Err("non-flat input path".into());
    }
    let bytes = std::fs::read(root.join(name))?;
    if pins[name] != bytes_sha256(&bytes).to_string() {
        return Err(format!("input hash mismatch: {name}").into());
    }
    Ok(bytes)
}
fn texture(device: &wgpu::Device, label: &str, mips: u32) -> wgpu::Texture {
    device.create_texture(&wgpu::TextureDescriptor {
        label: Some(label),
        size: wgpu::Extent3d {
            width: 128,
            height: 128,
            depth_or_array_layers: 6,
        },
        mip_level_count: mips,
        sample_count: 1,
        dimension: wgpu::TextureDimension::D2,
        format: wgpu::TextureFormat::Rgba16Float,
        usage: wgpu::TextureUsages::TEXTURE_BINDING
            | wgpu::TextureUsages::STORAGE_BINDING
            | wgpu::TextureUsages::COPY_SRC
            | wgpu::TextureUsages::COPY_DST,
        view_formats: &[],
    })
}
fn samplers(device: &wgpu::Device) -> Vec<wgpu::Sampler> {
    (0..9)
        .map(|index| {
            let mut desc = wgpu::SamplerDescriptor::default();
            if index < 6 {
                desc.mag_filter = wgpu::FilterMode::Linear;
                desc.min_filter = wgpu::FilterMode::Linear;
                desc.mipmap_filter = wgpu::MipmapFilterMode::Linear;
            }
            if [0, 3, 5, 7].contains(&index) {
                desc.address_mode_u = wgpu::AddressMode::Repeat;
                desc.address_mode_v = wgpu::AddressMode::Repeat;
            }
            if [0, 3, 5, 7].contains(&index) {
                desc.address_mode_w = wgpu::AddressMode::Repeat;
            }
            if index == 2 {
                desc.address_mode_u = wgpu::AddressMode::MirrorRepeat;
                desc.address_mode_v = wgpu::AddressMode::MirrorRepeat;
                desc.address_mode_w = wgpu::AddressMode::MirrorRepeat;
            }
            desc.anisotropy_clamp = match index {
                3 | 4 => 8,
                5 => 4,
                _ => 1,
            };
            if index == 8 {
                desc.mag_filter = wgpu::FilterMode::Linear;
                desc.min_filter = wgpu::FilterMode::Linear;
                desc.compare = Some(wgpu::CompareFunction::Less);
            }
            device.create_sampler(&desc)
        })
        .collect()
}
fn mapped(buffer: &wgpu::Buffer, device: &RenderDevice) -> Result<Vec<u8>, Box<dyn Error>> {
    let (tx, rx) = mpsc::channel();
    buffer
        .slice(..)
        .map_async(wgpu::MapMode::Read, move |result| {
            let _ = tx.send(result);
        });
    let deadline = Instant::now() + Duration::from_secs(30);
    loop {
        device.poll(wgpu::PollType::Poll)?;
        match rx.try_recv() {
            Ok(result) => {
                result?;
                break;
            }
            Err(mpsc::TryRecvError::Empty) if Instant::now() < deadline => {
                std::thread::sleep(Duration::from_millis(10))
            }
            _ => return Err("GPU readback timeout/disconnection".into()),
        }
    }
    let data = buffer.slice(..).get_mapped_range().to_vec();
    buffer.unmap();
    Ok(data)
}
fn half(bits: u16) -> f64 {
    let sign = if bits & 0x8000 == 0 { 1.0 } else { -1.0 };
    let exp = (bits >> 10) & 31;
    let mantissa = bits & 1023;
    match exp {
        0 => sign * f64::from(mantissa) / 1024.0 * 2_f64.powi(-14),
        31 => {
            if mantissa == 0 {
                sign * f64::INFINITY
            } else {
                f64::NAN
            }
        }
        _ => sign * (1.0 + f64::from(mantissa) / 1024.0) * 2_f64.powi(i32::from(exp) - 15),
    }
}

struct SkyInputs<'a> {
    root: &'a Path,
    pins: &'a Value,
    input: &'a Value,
    gpu: &'a wgpu::Device,
    queue: &'a RenderQueue,
    prepared: &'a [Prepared],
    source: &'a wgpu::Texture,
    global_entries: &'a [wgpu::BindGroupEntry<'a>],
}

fn encode_sky(
    context: SkyInputs<'_>,
    encoder: &mut wgpu::CommandEncoder,
    dispatches: &mut Vec<String>,
) -> Result<(), Box<dyn Error>> {
    let SkyInputs {
        root,
        pins,
        input,
        gpu,
        queue,
        prepared,
        source,
        global_entries,
    } = context;
    let clouds: Value = serde_json::from_slice(&read(
        root,
        pins,
        input["cloud_snapshot"]
            .as_str()
            .ok_or("cloud snapshot missing")?,
    )?)?;
    if clouds["schema"] != "forge.tidewater.cloud-acquisition" || clouds["version"] != 1 {
        return Err("unqualified cloud snapshot".into());
    }
    let frame = clouds["frames"]
        .as_array()
        .and_then(|frames| frames.last())
        .ok_or("cloud frame missing")?;
    let snapshot = clouds["snapshots"]
        .as_array()
        .and_then(|snapshots| snapshots.last())
        .ok_or("cloud textures missing")?;
    if frame["step"] != 120 || snapshot["step"] != 120 {
        return Err("final cloud inputs required".into());
    }
    let view = match frame["view"].as_u64() {
        Some(0) => "color0",
        Some(1) => "color1",
        _ => return Err("cloud history provider unavailable".into()),
    };
    let textures: Vec<_> = ["transmittance", "skyView", "shadow", "panorama", view]
        .iter()
        .map(|name| {
            let spec = &snapshot["textures"][*name];
            let width = u32::try_from(spec["width"].as_u64().ok_or("texture width missing")?)?;
            let height = u32::try_from(spec["height"].as_u64().ok_or("texture height missing")?)?;
            let (format, pixel_bytes) = match spec["format"].as_str() {
                Some("rgba16float") => (wgpu::TextureFormat::Rgba16Float, 8_u32),
                Some("r32float") => {
                    if !gpu.features().contains(wgpu::Features::FLOAT32_FILTERABLE) {
                        return Err(Box::<dyn Error>::from(
                            "original filtered R32 cloud shadow requires FLOAT32_FILTERABLE",
                        ));
                    }
                    (wgpu::TextureFormat::R32Float, 4)
                }
                _ => return Err("unqualified sky input format".into()),
            };
            let bytes = read(
                root,
                pins,
                spec["file"].as_str().ok_or("texture payload missing")?,
            )?;
            if width == 0
                || height == 0
                || bytes.len()
                    != usize::try_from(
                        u64::from(width) * u64::from(height) * u64::from(pixel_bytes),
                    )?
            {
                return Err("sky texture dimensions differ from payload".into());
            }
            let texture = gpu.create_texture(&wgpu::TextureDescriptor {
                label: Some(name),
                size: wgpu::Extent3d {
                    width,
                    height,
                    depth_or_array_layers: 1,
                },
                mip_level_count: 1,
                sample_count: 1,
                dimension: wgpu::TextureDimension::D2,
                format,
                usage: wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST,
                view_formats: &[],
            });
            queue.write_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &texture,
                    mip_level: 0,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                &bytes,
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(width * pixel_bytes),
                    rows_per_image: Some(height),
                },
                wgpu::Extent3d {
                    width,
                    height,
                    depth_or_array_layers: 1,
                },
            );
            Ok(texture.create_view(&wgpu::TextureViewDescriptor::default()))
        })
        .collect::<Result<_, Box<dyn Error>>>()?;
    let uniforms: Vec<_> = [("atmosphere", 64_usize), ("clouds", 80), ("sky", 32)]
        .iter()
        .map(|(name, size)| {
            let bytes = read(
                root,
                pins,
                input["effective_uniforms"][*name]
                    .as_str()
                    .ok_or("sky uniform missing")?,
            )?;
            if bytes.len() != *size {
                return Err("sky uniform size differs".into());
            }
            let buffer = gpu.create_buffer(&wgpu::BufferDescriptor {
                label: Some(name),
                size: u64::try_from(*size)?,
                usage: wgpu::BufferUsages::UNIFORM | wgpu::BufferUsages::COPY_DST,
                mapped_at_creation: false,
            });
            queue.write_buffer(&buffer, 0, &bytes);
            Ok(buffer)
        })
        .collect::<Result<_, Box<dyn Error>>>()?;
    let output = source.create_view(&wgpu::TextureViewDescriptor {
        dimension: Some(wgpu::TextureViewDimension::D2Array),
        base_mip_level: 0,
        mip_level_count: Some(1),
        ..Default::default()
    });
    let resources = [
        uniforms[0].as_entire_binding(),
        wgpu::BindingResource::TextureView(&textures[0]),
        wgpu::BindingResource::TextureView(&textures[1]),
        wgpu::BindingResource::TextureView(&textures[2]),
        uniforms[1].as_entire_binding(),
        wgpu::BindingResource::TextureView(&textures[3]),
        wgpu::BindingResource::TextureView(&textures[4]),
        uniforms[2].as_entire_binding(),
        wgpu::BindingResource::TextureView(&output),
    ];
    let entries: Vec<_> = resources
        .into_iter()
        .zip(0_u32..)
        .map(|(resource, binding)| wgpu::BindGroupEntry { binding, resource })
        .collect();
    for face in 0..6 {
        let name = format!("face-{face}");
        let (spec, pipeline, layouts) = prepared
            .iter()
            .find(|(spec, _, _)| spec["name"] == name)
            .ok_or("prepared sky face missing")?;
        // These exact bindings are the qualified original face layout, not an inferred auto-layout.
        let names: Vec<_> = spec["groups"][1]
            .as_array()
            .ok_or("face binding layout missing")?
            .iter()
            .map(|entry| entry["name"].as_str().ok_or("face binding name missing"))
            .collect::<Result<_, _>>()?;
        if names
            != [
                "atmosphereParams",
                "atmosphereTransmittanceLUT",
                "atmosphereSkyViewLUT",
                "cloudsShadowMap",
                "cloudsParams",
                "cloudsPanorama",
                "cloudsView",
                "skyParams",
                "outCube",
            ]
        {
            return Err("face binding order differs from acquired contract".into());
        }
        let global = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some(&name),
            layout: &layouts[0],
            entries: global_entries,
        });
        let scene = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some(&name),
            layout: &layouts[1],
            entries: &entries,
        });
        if spec["dispatch"] != json!([16, 16, 1]) {
            return Err("unqualified sky face dispatch".into());
        }
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
            label: Some(&name),
            ..Default::default()
        });
        pass.set_pipeline(pipeline);
        pass.set_bind_group(0, &global, &[]);
        pass.set_bind_group(1, &scene, &[]);
        pass.dispatch_workgroups(16, 16, 1);
        dispatches.push(name);
    }
    Ok(())
}
pub(super) fn execute(
    root: &Path,
    expected: &str,
    device: &RenderDevice,
    queue: &RenderQueue,
    prepared: &[Prepared],
    generate_mips: bool,
    generate_sky: bool,
) -> Result<Value, Box<dyn Error>> {
    let raw = std::fs::read(root.join("environment.json"))?;
    if bytes_sha256(&raw).to_string() != expected {
        return Err("live manifest hash mismatch".into());
    }
    let manifest: Value = serde_json::from_slice(&raw)?;
    if manifest["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || manifest["acquisition"]
            != "original_atmosphere_skyproclouds_environment_cold_120_frames_forced_ibl_final"
    {
        return Err("qualified live acquisition required".into());
    }
    let pins = &manifest["files"];
    for name in pins.as_object().ok_or("missing live pins")?.keys() {
        read(root, pins, name)?;
    }
    let input: Value = serde_json::from_slice(&read(root, pins, "ibl-live-inputs.json")?)?;
    if input["final_cloud_step"] != 120 || input["forced_original_environment_refresh"] != true {
        return Err("unexpected source state".into());
    }
    let frame_name = input["effective_uniforms"]["frame"]
        .as_str()
        .ok_or("source frame missing")?;
    let frame_data = read(root, pins, frame_name)?;
    if frame_data.len() != 720 {
        return Err("source Frame must be 720 bytes".into());
    }
    let source_mips = input["source_cube"]
        .as_array()
        .ok_or("source cube missing")?;
    if source_mips.len() != 8 {
        return Err("full eight-mip source cube required".into());
    }
    let reference_mips = manifest["mips"]
        .as_array()
        .ok_or("reference cube missing")?;
    if reference_mips.len() != 6 {
        return Err("six reference mips required".into());
    }
    let gpu = device.wgpu_device();
    let scope = gpu.push_error_scope(wgpu::ErrorFilter::Validation);
    let source = texture(gpu, "real acquired source radiance", 8);
    for (level, mip) in source_mips.iter().enumerate() {
        if generate_sky || (generate_mips && level > 0) {
            continue;
        }
        let level = u32::try_from(level)?;
        let size = 128 >> level;
        if mip["level"] != level || mip["width"] != size || mip["height"] != size {
            return Err("source mip dimensions differ".into());
        }
        let faces = mip["faces"].as_array().ok_or("source faces missing")?;
        if faces.len() != 6 {
            return Err("six source faces required".into());
        }
        for (face, name) in faces.iter().enumerate() {
            let bytes = read(root, pins, name.as_str().ok_or("source face name missing")?)?;
            if bytes.len() != usize::try_from(size * size * 8)? {
                return Err("source face bytes differ".into());
            }
            queue.write_texture(
                wgpu::TexelCopyTextureInfo {
                    texture: &source,
                    mip_level: level,
                    origin: wgpu::Origin3d {
                        x: 0,
                        y: 0,
                        z: u32::try_from(face)?,
                    },
                    aspect: wgpu::TextureAspect::All,
                },
                &bytes,
                wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(size * 8),
                    rows_per_image: Some(size),
                },
                wgpu::Extent3d {
                    width: size,
                    height: size,
                    depth_or_array_layers: 1,
                },
            );
        }
    }
    let frame = gpu.create_buffer(&wgpu::BufferDescriptor {
        label: Some("effective original Frame"),
        size: 720,
        usage: wgpu::BufferUsages::UNIFORM | wgpu::BufferUsages::COPY_DST,
        mapped_at_creation: false,
    });
    queue.write_buffer(&frame, 0, &frame_data);
    let samplers = samplers(gpu);
    let g0_entries: Vec<_> = std::iter::once(wgpu::BindGroupEntry {
        binding: 0,
        resource: frame.as_entire_binding(),
    })
    .chain(
        samplers
            .iter()
            .zip(1_u32..)
            .map(|(sampler, binding)| wgpu::BindGroupEntry {
                binding,
                resource: wgpu::BindingResource::Sampler(sampler),
            }),
    )
    .collect();
    let targets = [texture(gpu, "GGX back A", 6), texture(gpu, "GGX back B", 6)];
    let sh: Vec<_> = (0..2)
        .map(|_| {
            gpu.create_buffer(&wgpu::BufferDescriptor {
                label: Some("computed original SH9"),
                size: 144,
                usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
                mapped_at_creation: false,
            })
        })
        .collect();
    let mut encoder = gpu.create_command_encoder(&wgpu::CommandEncoderDescriptor {
        label: Some("original GGX and SH on real source cube"),
    });
    let mut dispatches = Vec::new();
    if generate_sky {
        encode_sky(
            SkyInputs {
                root,
                pins,
                input: &input,
                gpu,
                queue,
                prepared,
                source: &source,
                global_entries: &g0_entries,
            },
            &mut encoder,
            &mut dispatches,
        )?;
    }
    if generate_mips {
        for (name, first, last) in [("mips-a", 0_u32, 5_u32), ("mips-b", 5, 7)] {
            let (spec, pipeline, layouts) = prepared
                .iter()
                .find(|(spec, _, _)| spec["name"] == name)
                .ok_or("prepared mip program missing")?;
            let views: Vec<_> = (first..=last)
                .map(|level| {
                    source.create_view(&wgpu::TextureViewDescriptor {
                        dimension: Some(wgpu::TextureViewDimension::D2Array),
                        base_mip_level: level,
                        mip_level_count: Some(1),
                        ..Default::default()
                    })
                })
                .collect();
            let entries: Vec<_> = views
                .iter()
                .zip(0_u32..)
                .map(|(view, binding)| wgpu::BindGroupEntry {
                    binding,
                    resource: wgpu::BindingResource::TextureView(view),
                })
                .collect();
            let g0 = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some(name),
                layout: &layouts[0],
                entries: &g0_entries,
            });
            let g1 = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some(name),
                layout: &layouts[1],
                entries: &entries,
            });
            let groups = spec["dispatch"].as_array().ok_or("mip dispatch missing")?;
            if groups.len() != 3 {
                return Err("three mip dispatch dimensions required".into());
            }
            let mut dimensions = [0_u32; 3];
            for (dimension, value) in dimensions.iter_mut().zip(groups) {
                *dimension = u32::try_from(value.as_u64().ok_or("invalid mip dispatch")?)?;
            }
            let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
                label: Some(name),
                ..Default::default()
            });
            pass.set_pipeline(pipeline);
            pass.set_bind_group(0, &g0, &[]);
            pass.set_bind_group(1, &g1, &[]);
            pass.dispatch_workgroups(dimensions[0], dimensions[1], dimensions[2]);
            dispatches.push(name.to_owned());
        }
    }
    for target in 0..2 {
        for phase in 0..7 {
            let name = if phase == 6 {
                format!("sh-{target}")
            } else {
                format!("filter-{target}-{phase}")
            };
            let (spec, pipeline, layouts) = prepared
                .iter()
                .find(|(spec, _, _)| spec["name"] == name)
                .ok_or("prepared program missing")?;
            let g0 = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some(&name),
                layout: &layouts[0],
                entries: &g0_entries,
            });
            let src = source.create_view(&wgpu::TextureViewDescriptor {
                dimension: Some(if phase == 6 {
                    wgpu::TextureViewDimension::D2Array
                } else {
                    wgpu::TextureViewDimension::Cube
                }),
                ..Default::default()
            });
            let out = targets[target].create_view(&wgpu::TextureViewDescriptor {
                dimension: Some(wgpu::TextureViewDimension::D2Array),
                base_mip_level: if phase == 6 { 0 } else { u32::try_from(phase)? },
                mip_level_count: Some(1),
                ..Default::default()
            });
            let entries = [
                wgpu::BindGroupEntry {
                    binding: 0,
                    resource: wgpu::BindingResource::TextureView(&src),
                },
                wgpu::BindGroupEntry {
                    binding: 1,
                    resource: if phase == 6 {
                        sh[target].as_entire_binding()
                    } else {
                        wgpu::BindingResource::TextureView(&out)
                    },
                },
            ];
            let g1 = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
                label: Some(&name),
                layout: &layouts[1],
                entries: &entries,
            });
            let groups = spec["dispatch"]
                .as_array()
                .ok_or("dispatch dimensions missing")?;
            if groups.len() != 3 {
                return Err("three dispatch dimensions required".into());
            }
            let groups: Vec<_> = groups
                .iter()
                .map(|v| {
                    v.as_u64()
                        .ok_or_else(|| "invalid group count".into())
                        .and_then(|v| u32::try_from(v).map_err(|e| Box::<dyn Error>::from(e)))
                })
                .collect::<Result<_, Box<dyn Error>>>()?;
            let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
                label: Some(&name),
                ..Default::default()
            });
            pass.set_pipeline(pipeline);
            pass.set_bind_group(0, &g0, &[]);
            pass.set_bind_group(1, &g1, &[]);
            pass.dispatch_workgroups(groups[0], groups[1], groups[2]);
            dispatches.push(name);
        }
    }
    let mut reads = Vec::new();
    let source_reads = if generate_mips {
        copy_cube(gpu, &mut encoder, &source, 8)
    } else {
        Vec::new()
    };
    for (target, texture) in targets.iter().enumerate() {
        for level in 0..6 {
            let size = 128_u32 >> level;
            let row = (size * 8).div_ceil(256) * 256;
            let buffer = gpu.create_buffer(&wgpu::BufferDescriptor {
                label: Some("GGX readback"),
                size: u64::from(row * size * 6),
                usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                mapped_at_creation: false,
            });
            encoder.copy_texture_to_buffer(
                wgpu::TexelCopyTextureInfo {
                    texture,
                    mip_level: level,
                    origin: wgpu::Origin3d::ZERO,
                    aspect: wgpu::TextureAspect::All,
                },
                wgpu::TexelCopyBufferInfo {
                    buffer: &buffer,
                    layout: wgpu::TexelCopyBufferLayout {
                        offset: 0,
                        bytes_per_row: Some(row),
                        rows_per_image: Some(size),
                    },
                },
                wgpu::Extent3d {
                    width: size,
                    height: size,
                    depth_or_array_layers: 6,
                },
            );
            reads.push((target, level, size, row, buffer));
        }
    }
    let sh_reads: Vec<_> = sh
        .iter()
        .map(|buffer| {
            let read = gpu.create_buffer(&wgpu::BufferDescriptor {
                label: Some("SH readback"),
                size: 144,
                usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
                mapped_at_creation: false,
            });
            encoder.copy_buffer_to_buffer(buffer, 0, &read, 0, 144);
            read
        })
        .collect();
    queue.submit([encoder.finish()]);
    if let Some(error) = super::wait(scope.pop(), device)? {
        return Err(format!("IBL GPU validation: {error}").into());
    }
    let source_comparison = if generate_mips {
        Some(compare_source_cube(
            root,
            pins,
            source_mips,
            source_reads,
            device,
        )?)
    } else {
        None
    };
    let mut comparisons = Vec::new();
    let mut equal_faces = 0;
    let mut max_rgb = 0_f64;
    for (target, level, size, row, buffer) in reads {
        let bytes = mapped(&buffer, device)?;
        for face in 0..6 {
            let mut compact = Vec::new();
            for y in 0..size {
                let offset = usize::try_from((face * size + y) * row)?;
                compact.extend_from_slice(&bytes[offset..offset + usize::try_from(size * 8)?]);
            }
            let expected_name = reference_mips[usize::try_from(level)?]["faces"]
                [usize::try_from(face)?]
            .as_str()
            .ok_or("reference face missing")?;
            let original = read(root, pins, expected_name)?;
            if original.len() != compact.len() {
                return Err("reference face length differs".into());
            }
            let mut error = 0_f64;
            let mut changed = 0_usize;
            for (index, (a, b)) in compact
                .chunks_exact(2)
                .zip(original.chunks_exact(2))
                .enumerate()
            {
                let a = half(u16::from_le_bytes([a[0], a[1]]));
                let b = half(u16::from_le_bytes([b[0], b[1]]));
                if !a.is_finite() || !b.is_finite() {
                    return Err("non-finite IBL output".into());
                }
                if index % 4 != 3 {
                    error = error.max((a - b).abs());
                } else if a != 1.0 || b != 1.0 {
                    return Err("GGX output alpha differs from source one".into());
                }
                if a != b {
                    changed += 1;
                }
            }
            if compact == original {
                equal_faces += 1;
            }
            max_rgb = max_rgb.max(error);
            comparisons.push(json!({"target":target,"level":level,"face":face,"sha256":bytes_sha256(&compact),"reference_sha256":bytes_sha256(&original),"changed_channels":changed,"max_rgb_absolute_error":error}));
        }
    }
    let original_sh = read(
        root,
        pins,
        manifest["sh"].as_str().ok_or("reference SH missing")?,
    )?;
    if original_sh.len() != 144 {
        return Err("reference SH length differs".into());
    }
    let mut sh_comparisons = Vec::new();
    for (target, buffer) in sh_reads.iter().enumerate() {
        let bytes = mapped(buffer, device)?;
        let mut error = 0_f64;
        for (index, (a, b)) in bytes
            .chunks_exact(4)
            .zip(original_sh.chunks_exact(4))
            .enumerate()
        {
            let a = f64::from(f32::from_le_bytes(a.try_into()?));
            let b = f64::from(f32::from_le_bytes(b.try_into()?));
            if !a.is_finite() || !b.is_finite() {
                return Err("non-finite SH output".into());
            }
            if index % 4 == 3 && (a != 0.0 || b != 0.0) {
                return Err("SH output padding differs".into());
            }
            error = error.max((a - b).abs());
        }
        sh_comparisons.push(json!({"target":target,"sha256":bytes_sha256(&bytes),"reference_sha256":bytes_sha256(&original_sh),"max_absolute_error":error,"byte_identical":bytes==original_sh}));
    }
    Ok(
        json!({"scope":if generate_sky { "sky-mips-GGX-and-SH-from-real-acquired-LUTs-and-clouds" } else if generate_mips { "mips-GGX-and-SH-from-real-acquired-base-cube" } else { "GGX-and-SH-from-real-acquired-source-cube" },"live_manifest_sha256":expected,"dispatches":dispatches,"compared_faces":72,"byte_identical_faces":equal_faces,"max_rgb_absolute_error":max_rgb,"faces":comparisons,"sh":sh_comparisons,"source_cube_comparison":source_comparison,"source_face_generation_executed":generate_sky,"mip_generation_executed":generate_mips,"studio_executed":false,"visual_parity":false}),
    )
}
