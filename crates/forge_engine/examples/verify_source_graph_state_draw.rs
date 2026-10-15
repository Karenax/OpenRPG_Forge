//! Private diagnostic draw of externally pinned source meshes and resident state.
//! The fragment is a coverage diagnostic, not source material qualification.
#[path = "verify_source_graph_state_draw/emission.rs"]
mod emission;
#[path = "verify_source_graph_state_draw/scene_probe.rs"]
mod scene_probe;
#[path = "verify_source_graph_state_draw/texture_mips.rs"]
mod texture_mips;
#[path = "verify_source_graph_state_draw/timelines.rs"]
mod timelines;
#[path = "verify_source_graph_state_draw/vertex_projection.rs"]
mod vertex_projection;
use bevy::{
    app::PluginsState,
    asset::RenderAssetUsages,
    camera::{Hdr, RenderTarget, visibility::NoFrustumCulling},
    core_pipeline::tonemapping::Tonemapping,
    ecs::system::SystemState,
    image::{ImageSampler, ImageSamplerDescriptor},
    mesh::Indices,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        gpu_readback::{Readback, ReadbackComplete},
        pipelined_rendering::PipelinedRenderingPlugin,
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, Extent3d, OwnedBindingResource, PipelineCache, PrimitiveTopology,
            TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::{RenderAdapter, RenderDevice},
        settings::{RenderCreation, WgpuFeatures, WgpuSettings},
        storage::ShaderBuffer,
        texture::GpuImage,
    },
    window::{ExitCondition, WindowPlugin},
    winit::WinitPlugin,
};
use forge_assets::source_graph_bundle::{
    SourceGraphImportedStorageFormat, SourceGraphImportedStorageMip,
    SourceGraphResolvedStorageSampler,
};
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::{
    source_graph::{
        SOURCE_GRAPH_NORMAL, SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS, SOURCE_GRAPH_TANGENT,
        SOURCE_GRAPH_UV0, SourceGraphForwardState, SourceGraphImportedMip,
        SourceGraphImportedTextureFormat, SourceGraphImportedTextureMetadata, SourceGraphMaterial,
        SourceGraphMaterialProgram, SourceGraphPlugin, SourceGraphSamplerSettings,
        SourceGraphScalarUniforms, SourceGraphSceneSampling, SourceGraphShaderArtifact,
        SourceGraphShaderRegistry, SourceGraphStateBufferBinding, SourceGraphUniforms,
        prepare_source_graph_imported_texture, register_source_graph_material,
        source_graph_image_sampler_in_scene,
    },
    source_particle_compute::{
        SourceParticleComputeQueue, SourceParticleComputeSequence, SourceParticleProjectionPlugin,
        register_source_particle_compute_program,
    },
};
use serde::Deserialize;
use std::{
    path::{Component, Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[derive(Clone, Deserialize)]
#[serde(deny_unknown_fields)]
struct Pin {
    file: String,
    sha256: ContentHash,
}
#[derive(Clone, Deserialize)]
#[serde(deny_unknown_fields)]
struct Step {
    delta_time: f32,
    /// Explicit legacy dispatch; absent when the admitted source clock schedules phases.
    mode: Option<u16>,
    #[serde(default)]
    events: Vec<forge_assets::source_particle_emission::SourceEmissionEvent>,
    #[serde(default)]
    reinitialize: bool,
    label: String,
    expected: Option<Pin>,
    draw: bool,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Texture {
    storage: Pin,
    width: u32,
    height: u32,
    format: SourceGraphImportedStorageFormat,
    mips: Vec<SourceGraphImportedStorageMip>,
    sampler: SourceGraphResolvedStorageSampler,
    scene_force_anisotropy: bool,
    #[serde(default)]
    diagnostic_lod_max_override: Option<f32>,
    #[serde(default)]
    diagnostic_anisotropy_override: Option<u16>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    #[serde(default)]
    governed_owner: Option<GovernedOwner>,
    schema_version: u32,
    shader: Pin,
    atlas: Pin,
    bank: Pin,
    initial: Pin,
    width: u32,
    height: u32,
    entry_point: String,
    mesh: Pin,
    indices: Pin,
    render_shader: Pin,
    render_parameters: Pin,
    steps: Vec<Step>,
    #[serde(default)]
    source_spawner: Option<forge_assets::source_particle_emission::SourceSingleBurstLoopParameters>,
    #[serde(default)]
    textures: Vec<Texture>,
    #[serde(default = "default_target_size")]
    target_width: u32,
    #[serde(default = "default_target_size")]
    target_height: u32,
    #[serde(default)]
    readback_texture_mips: bool,
    #[serde(default)]
    vertex_projection: Option<vertex_projection::Spec>,
}
#[derive(serde::Deserialize)]
#[serde(deny_unknown_fields)]
struct GovernedOwner {
    manifest: Pin,
    carrier: Pin,
}
fn default_target_size() -> u32 {
    512
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Source sampler mip bias has a Float32 GPU ABI"
)]
fn texture_image(root: &Path, texture: &Texture, anisotropic: bool) -> Result<Image, String> {
    let format = match texture.format {
        SourceGraphImportedStorageFormat::Bc1RgbaUnorm => {
            SourceGraphImportedTextureFormat::Bc1RgbaUnorm
        }
        SourceGraphImportedStorageFormat::Bc1RgbaUnormSrgb => {
            SourceGraphImportedTextureFormat::Bc1RgbaUnormSrgb
        }
        SourceGraphImportedStorageFormat::Bc3RgbaUnorm => {
            SourceGraphImportedTextureFormat::Bc3RgbaUnorm
        }
        SourceGraphImportedStorageFormat::Bc3RgbaUnormSrgb => {
            SourceGraphImportedTextureFormat::Bc3RgbaUnormSrgb
        }
        SourceGraphImportedStorageFormat::Bc4RUnorm => SourceGraphImportedTextureFormat::Bc4RUnorm,
        SourceGraphImportedStorageFormat::Rgb8Unorm => SourceGraphImportedTextureFormat::Rgb8Unorm,
        SourceGraphImportedStorageFormat::Rgba8Unorm => {
            SourceGraphImportedTextureFormat::Rgba8Unorm
        }
        SourceGraphImportedStorageFormat::Rgb10a2Unorm => {
            SourceGraphImportedTextureFormat::Rgb10a2Unorm
        }
    };
    let s = &texture.sampler;
    let sampler = SourceGraphSamplerSettings {
        filter_mode: s.filter_mode,
        wrap_u: s.wrap[0],
        wrap_v: s.wrap[1],
        wrap_w: s.wrap[2],
        anisotropy: u16::from(s.anisotropy),
        mip_bias: s.mip_bias as f32,
        source_mipmaps_enabled: s.source_mipmaps_enabled,
    };
    let metadata = SourceGraphImportedTextureMetadata {
        storage_sha256: texture.storage.sha256,
        width: texture.width,
        height: texture.height,
        format,
        mips: texture
            .mips
            .iter()
            .map(|m| {
                Ok(SourceGraphImportedMip {
                    width: m.width,
                    height: m.height,
                    offset: usize::try_from(m.offset).map_err(|_| "Mip offset overflow")?,
                    bytes: usize::try_from(m.bytes).map_err(|_| "Mip length overflow")?,
                    sha256: m.sha256,
                })
            })
            .collect::<Result<Vec<_>, String>>()?,
        sampler,
    };
    let mut image =
        prepare_source_graph_imported_texture(&read(root, &texture.storage)?, &metadata)?
            .into_unqualified_image();
    let scene = if texture.scene_force_anisotropy {
        SourceGraphSceneSampling::UnityForceEnableD3d
    } else {
        SourceGraphSceneSampling::Authored
    };
    image.sampler = ImageSampler::Descriptor(
        source_graph_image_sampler_in_scene(
            sampler,
            texture.width,
            texture.height,
            u32::try_from(texture.mips.len()).map_err(|_| "Mip count overflow")?,
            scene,
            anisotropic,
        )
        .map_err(str::to_owned)?,
    );
    diagnostic_sampler_controls(&mut image, texture, anisotropic)?;
    Ok(image)
}
fn diagnostic_sampler_controls(
    image: &mut Image,
    texture: &Texture,
    anisotropic: bool,
) -> Result<(), String> {
    let ImageSampler::Descriptor(sampler) = &mut image.sampler else {
        return Err("Explicit source sampler required".into());
    };
    if let Some(maximum) = texture.diagnostic_lod_max_override {
        if !maximum.is_finite() || maximum < 0.0 {
            return Err("Diagnostic sampler LOD maximum is invalid".into());
        }
        if maximum < sampler.lod_min_clamp {
            return Err("Diagnostic sampler LOD range is inverted".into());
        }
        sampler.lod_max_clamp = maximum;
    }
    if let Some(maximum) = texture.diagnostic_anisotropy_override {
        if !(1..=16).contains(&maximum) || maximum > 1 && !anisotropic {
            return Err("Diagnostic sampler anisotropy is invalid or unsupported".into());
        }
        if maximum > 1
            && [
                sampler.min_filter,
                sampler.mag_filter,
                sampler.mipmap_filter,
            ]
            .iter()
            .any(|filter| *filter != bevy::image::ImageFilterMode::Linear)
        {
            return Err("Diagnostic anisotropy requires linear filtering".into());
        }
        sampler.anisotropy_clamp = maximum;
    }
    Ok(())
}
fn verify_texture_bindings(
    app: &mut App,
    material: &Handle<SourceGraphMaterial>,
    images: &[Handle<Image>],
) -> Result<Vec<serde_json::Value>, String> {
    let sampler_descriptors: Vec<_> = images
        .iter()
        .map(|handle| {
            app.world()
                .resource::<Assets<Image>>()
                .get(handle)
                .map(|image| format!("{:?}", image.sampler))
                .ok_or("Source image descriptor missing")
        })
        .collect::<Result<_, _>>()?;
    let material = app
        .world()
        .resource::<Assets<SourceGraphMaterial>>()
        .get(material)
        .ok_or("Material missing")?
        .clone();
    let render = app
        .get_sub_app_mut(RenderApp)
        .ok_or("Renderer absent")?
        .world_mut();
    let device = render.resource::<RenderDevice>().clone();
    let mut state = SystemState::<<SourceGraphMaterial as AsBindGroup>::Param>::new(render);
    let descriptor = SourceGraphMaterial::bind_group_layout_descriptor(&device);
    let prepared = render
        .resource_scope(|world, cache: Mut<PipelineCache>| {
            let mut params = state.get_mut(world).expect("Material parameters required");
            material.unprepared_bind_group(
                &cache.get_bind_group_layout(&descriptor),
                &device,
                &mut params,
                true,
            )
        })
        .map_err(|e| format!("Material resources not prepared: {e:?}"))?;
    let mut rows = Vec::new();
    for (i, handle) in images.iter().enumerate() {
        let binding = forge_assets::source_graph_bundle::SOURCE_GRAPH_TEXTURE_BINDINGS[i];
        let gpu = render
            .resource::<RenderAssets<GpuImage>>()
            .get(handle)
            .ok_or("Source texture absent on GPU")?;
        let resource = |n| {
            prepared
                .bindings
                .iter()
                .find(|(slot, _)| *slot == n)
                .map(|(_, r)| r)
                .ok_or("Source texture/sampler binding absent")
        };
        if !matches!(resource(binding.0)?, OwnedBindingResource::TextureView(_,v) if v.id()==gpu.texture_view.id())
            || !matches!(resource(binding.1)?, OwnedBindingResource::Sampler(_,v) if v.id()==gpu.sampler.id())
        {
            return Err("Material texture/sampler identity mismatch".into());
        }
        rows.push(serde_json::json!({"slot":i,"GPU_format":format!("{:?}",gpu.texture_descriptor.format),"GPU_mips":gpu.texture_descriptor.mip_level_count,"sampler_descriptor_used":sampler_descriptors[i],"actual_texture_and_sampler_binding_identity":true}));
    }
    Ok(rows)
}
fn read(root: &Path, pin: &Pin) -> Result<Vec<u8>, String> {
    let p = Path::new(&pin.file);
    if p.components().count() != 1 || !matches!(p.components().next(), Some(Component::Normal(_))) {
        return Err("Expected flat fixture path".into());
    }
    let p = root.join(p);
    if std::fs::metadata(&p).map_err(|e| e.to_string())?.len() > 64 * 1024 * 1024 {
        return Err("Private fixture byte budget exceeded".into());
    }
    let b = std::fs::read(p).map_err(|e| e.to_string())?;
    if bytes_sha256(&b) != pin.sha256 {
        return Err("Fixture SHA mismatch".into());
    }
    Ok(b)
}
fn f32_at(b: &[u8], offset: usize) -> f32 {
    f32::from_le_bytes(
        b[offset..offset + 4]
            .try_into()
            .expect("Checked mesh stride"),
    )
}
fn u32_at(b: &[u8], offset: usize) -> u32 {
    u32::from_le_bytes(
        b[offset..offset + 4]
            .try_into()
            .expect("Checked record stride"),
    )
}
fn source_mesh(b: &[u8], indices: &[u8]) -> Result<Mesh, String> {
    if b.is_empty()
        || !b.len().is_multiple_of(64)
        || indices.is_empty()
        || !indices.len().is_multiple_of(6)
    {
        return Err("Expected source Float32x16 vertices and UInt16 triangles".into());
    }
    let records: Vec<_> = b
        .as_chunks::<64>()
        .0
        .iter()
        .map(<[u8; 64]>::as_slice)
        .collect();
    if records
        .iter()
        .any(|r| (0..16).any(|n| !f32_at(r, n * 4).is_finite()))
    {
        return Err("Non-finite source vertex".into());
    }
    let index: Vec<_> = indices
        .as_chunks::<2>()
        .0
        .iter()
        .map(|v| u16::from_le_bytes(*v))
        .collect();
    if index.iter().any(|v| usize::from(*v) >= records.len()) {
        return Err("Source mesh index out of range".into());
    }
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    for (attribute, n) in [(Mesh::ATTRIBUTE_POSITION, 0), (SOURCE_GRAPH_NORMAL, 3)] {
        mesh.insert_attribute(
            attribute,
            records
                .iter()
                .map(|r| {
                    [
                        f32_at(r, n * 4),
                        f32_at(r, (n + 1) * 4),
                        f32_at(r, (n + 2) * 4),
                    ]
                })
                .collect::<Vec<_>>(),
        );
    }
    mesh.insert_attribute(
        SOURCE_GRAPH_TANGENT,
        records
            .iter()
            .map(|r| std::array::from_fn::<_, 4, _>(|n| f32_at(r, (n + 6) * 4)))
            .collect::<Vec<_>>(),
    );
    mesh.insert_attribute(
        Mesh::ATTRIBUTE_UV_0,
        records
            .iter()
            .map(|r| [f32_at(r, 40), f32_at(r, 44)])
            .collect::<Vec<_>>(),
    );
    for (attribute, n) in [
        (SOURCE_GRAPH_UV0, 10),
        (SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[0], 12),
        (SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS[1], 14),
    ] {
        // Native input reflection declares Float32x2; D3D fills missing z/w as 0/1.
        mesh.insert_attribute(
            attribute,
            records
                .iter()
                .map(|r| [f32_at(r, n * 4), f32_at(r, (n + 1) * 4), 0., 1.])
                .collect::<Vec<_>>(),
        );
    }
    mesh.insert_indices(Indices::U16(index));
    Ok(mesh)
}
type BytesSlot = Arc<Mutex<Option<Vec<u8>>>>;
fn receive_buffer(app: &mut App, buffer: Handle<ShaderBuffer>) -> (Entity, BytesSlot) {
    let data = Arc::new(Mutex::new(None));
    let sink = data.clone();
    let e = app
        .world_mut()
        .spawn(Readback::buffer(buffer))
        .observe(move |event: On<ReadbackComplete>| {
            if let Ok(mut slot) = sink.lock() {
                *slot = Some(event.data.clone());
            }
        })
        .id();
    (e, data)
}
fn verify_state(data: &[u8], expected: &[u8], initial: &[u8], step: u16) -> Result<(), String> {
    if !matches!(initial.len(), 160 | 320)
        || data.len() != initial.len()
        || expected.len() != initial.len() / 160 * 52
    {
        return Err("Native state cardinality mismatch".into());
    }
    for p in 0..initial.len() / 160 {
        let n = p * 160;
        if data[n..n + 52] != expected[p * 52..p * 52 + 52]
            || data[n + 64..n + 160] != initial[n + 64..n + 160]
            || u32_at(data, n + 56) != u32::from(step)
            || u32_at(data, n + 60) != 0x5253_5441
        {
            return Err("Resident source checkpoint or retained properties differ".into());
        }
    }
    Ok(())
}
fn capture(app: &mut App, slot: &BytesSlot, target_bytes: usize) -> Result<Vec<u8>, String> {
    let deadline = Instant::now() + Duration::from_secs(90);
    let mut previous = None;
    let mut stable = 0;
    for frame in 0.. {
        app.update();
        if Instant::now() > deadline {
            return Err("Source draw/readback timeout".into());
        }
        let pending = app
            .get_sub_app(RenderApp)
            .ok_or("Render app absent")?
            .world()
            .resource::<PipelineCache>()
            .waiting_pipelines()
            .count();
        if frame > 30
            && pending == 0
            && let Some(bytes) = slot.lock().map_err(|_| "Image mutex poisoned")?.take()
        {
            if bytes.len() != target_bytes {
                return Err("Target readback cardinality mismatch".into());
            }
            if previous.as_ref() == Some(&bytes) {
                stable += 1;
            } else {
                stable = 0;
            }
            previous = Some(bytes.clone());
            if stable >= 3 {
                return Ok(bytes);
            }
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    unreachable!("Unbounded capture loop returns on timeout")
}
#[allow(
    clippy::too_many_lines,
    reason = "Ordered private source admission, simulation and draw evidence"
)]
fn run() -> Result<(), String> {
    let mut args = std::env::args().skip(1);
    let path = PathBuf::from(args.next().ok_or("Expected manifest")?);
    let hash = args
        .next()
        .ok_or("Expected manifest SHA")?
        .parse::<ContentHash>()
        .map_err(|e| e.to_string())?;
    let output = PathBuf::from(args.next().ok_or("Expected fresh evidence directory")?);
    let bytes = std::fs::read(&path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != hash {
        return Err("Manifest SHA mismatch".into());
    }
    let m: Manifest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    let companion = timelines::load_companion(&mut args, &bytes)?;
    if m.schema_version != 1
        || m.textures.len() > forge_assets::source_graph_bundle::SOURCE_GRAPH_MAX_TEXTURES
        || m.steps.is_empty()
        || m.steps.len() > 4096
        || m.width == 0
        || m.height == 0
        || !(1..=2048).contains(&m.target_width)
        || !(1..=2048).contains(&m.target_height)
        || !m.target_width.is_multiple_of(32)
    {
        return Err("Invalid diagnostic manifest".into());
    }
    let root = path.parent().ok_or("Manifest parent absent")?;
    let compute = String::from_utf8(read(root, &m.shader)?).map_err(|e| e.to_string())?;
    let draw = String::from_utf8(read(root, &m.render_shader)?).map_err(|e| e.to_string())?;
    let atlas = read(root, &m.atlas)?;
    let bank: [u8; 1024] = read(root, &m.bank)?
        .try_into()
        .map_err(|_| "Curve bank ABI mismatch")?;
    let initial = read(root, &m.initial)?;
    let parameters = read(root, &m.render_parameters)?;
    let vertices = read(root, &m.mesh)?;
    let indices = read(root, &m.indices)?;
    let mesh = source_mesh(&vertices, &indices)?;
    if !matches!(initial.len(), 160 | 320)
        || parameters.len() != 256
        || atlas.len() as u64 != u64::from(m.width) * u64::from(m.height) * 8
    {
        return Err("Source resource cardinality mismatch".into());
    }
    if m.vertex_projection
        .as_ref()
        .is_some_and(|s| s.state_prefix_bytes != initial.len())
    {
        return Err("Projection and resident state prefixes differ".into());
    }
    let expected: Vec<_> = m
        .steps
        .iter()
        .map(|s| s.expected.as_ref().map(|p| read(root, p)).transpose())
        .collect::<Result<_, _>>()?;
    let governed = m.governed_owner.as_ref().map(|package| {
        let wire=read(root,&package.manifest)?;
        let carrier=read(root,&package.carrier)?;
        let resolved=forge_assets::source_particle_transport::SourceParticleTransport::decode_for_carrier(
            &wire,package.manifest.sha256,&carrier,|name| {
                let path=root.join(name);
                let canonical=path.canonicalize().map_err(|e|e.to_string())?;
                if !canonical.starts_with(root.canonicalize().map_err(|e|e.to_string())?) {
                    return Err("Governed resource outside fixture root".into());
                }
                if std::fs::metadata(&canonical).map_err(|e|e.to_string())?.len()>64*1024*1024 {
                    return Err("Governed resource exceeds fixture budget".into());
                }
                std::fs::read(canonical).map_err(|e|e.to_string())
            })?;
        if resolved.contract.owners.len()!=1 {return Err("GPU fixture requires one governed owner definition".into());}
        let declaration=&resolved.contract.owners[0];
        if declaration.state_prefix_bytes!=initial.len() || declaration.projection.vertex_count!=vertices.len()/64
            || declaration.compute.source.sha256!=m.shader.sha256 || declaration.compute.entry_point!=m.entry_point
            || declaration.curve_atlas.sha256!=m.atlas.sha256 || declaration.curve_width!=m.width || declaration.curve_height!=m.height
            || m.vertex_projection.as_ref().is_none_or(|p| p.shader.sha256!=declaration.projection.program.source.sha256
                || p.entry_point!=declaration.projection.program.entry_point || p.bank.sha256!=declaration.projection.bank.sha256)
            || declaration.spawner!=m.source_spawner.ok_or("Governed fixture spawner absent")?
            || resolved.resources.get(&declaration.initial_state.path)!=Some(&initial)
            || resolved.resources.get(&declaration.curve_bank.path).map(Vec::as_slice)!=Some(bank.as_slice())
            || resolved.resources.get(&declaration.projection.native_vertices.path)!=Some(&vertices)
        {return Err("Governed owner differs from native draw fixture".into());}
        forge_engine::source_particle_transport::PreparedSourceParticleOwner::prepare(declaration,&resolved.resources)?;
        Ok::<_,String>(resolved)
    }).transpose()?;
    emission::validate(&m.steps, m.source_spawner)?;
    std::fs::create_dir(&output).map_err(|e| e.to_string())?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    features: WgpuFeatures::TEXTURE_COMPRESSION_BC,
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    )
    .add_plugins((SourceGraphPlugin, SourceParticleProjectionPlugin));
    let deadline = Instant::now() + Duration::from_secs(240);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Renderer initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    app.finish();
    app.cleanup();
    app.update();
    let render = app.get_sub_app(RenderApp).ok_or("Renderer absent")?.world();
    let adapter_info = render.resource::<RenderAdapter>().get_info();
    if !render
        .resource::<RenderDevice>()
        .features()
        .contains(WgpuFeatures::TEXTURE_COMPRESSION_BC)
    {
        return Err("Actual device lacks required source BC storage".into());
    }
    let anisotropic = render
        .resource::<RenderAdapter>()
        .get_downlevel_capabilities()
        .flags
        .contains(wgpu::DownlevelFlags::ANISOTROPIC_FILTERING);
    let mut texture_handles = Vec::new();
    for texture in &m.textures {
        let mut image = texture_image(root, texture, anisotropic)?;
        if m.readback_texture_mips {
            image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
        }
        texture_handles.push(app.world_mut().resource_mut::<Assets<Image>>().add(image));
    }
    let shader = register_source_particle_compute_program(
        &mut app.world_mut().resource_mut::<Assets<Shader>>(),
        &compute,
        m.shader.sha256,
        &m.entry_point,
    )?;
    let mut image = Image::new(
        Extent3d {
            width: m.width,
            height: m.height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        atlas,
        TextureFormat::Rgba16Float,
        RenderAssetUsages::default(),
    );
    image.sampler = ImageSampler::Descriptor(ImageSamplerDescriptor::linear());
    let curve = app.world_mut().resource_mut::<Assets<Image>>().add(image);
    let mut vertex_projection = m
        .vertex_projection
        .as_ref()
        .map(|spec| {
            vertex_projection::Projection::new(&mut app, root, spec, curve.clone(), &vertices)
        })
        .transpose()?;
    let buffers = std::array::from_fn(|_| {
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(&initial, RenderAssetUsages::default()))
    });
    let published = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&initial, RenderAssetUsages::default()));
    let sequence = SourceParticleComputeSequence::new(
        7,
        shader.clone(),
        m.entry_point.clone(),
        curve.clone(),
        buffers,
        1,
    )
    .map_err(str::to_owned)?
    .with_publication_buffer(published, initial.len() as u64)
    .map_err(str::to_owned)?;
    let sequence = emission::SimulationSequence::new(&mut app, sequence, m.source_spawner, &bank)?;
    let primary_directory = if companion.is_some() {
        output.join("owner-0")
    } else {
        output.clone()
    };
    let mut owners = vec![timelines::Timeline::new(
        sequence,
        m.steps.clone(),
        expected,
        primary_directory,
    )?];
    if let Some(companion) = companion {
        let buffers = std::array::from_fn(|_| {
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(ShaderBuffer::new(&initial, RenderAssetUsages::default()))
        });
        let published = app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(&initial, RenderAssetUsages::default()));
        let compute = SourceParticleComputeSequence::new(
            9,
            shader.clone(),
            m.entry_point.clone(),
            curve.clone(),
            buffers,
            1,
        )
        .map_err(str::to_owned)?
        .with_publication_buffer(published, initial.len() as u64)
        .map_err(str::to_owned)?;
        let sequence =
            emission::SimulationSequence::new(&mut app, compute, m.source_spawner, &bank)?;
        owners.push(timelines::Timeline::new(
            sequence,
            companion.steps,
            companion.expected,
            output.join("owner-1"),
        )?);
    }
    let mut material = app
        .world_mut()
        .resource_scope(|world, mut registry: Mut<SourceGraphShaderRegistry>| {
            register_source_graph_material(
                &mut world.resource_mut::<Assets<Shader>>(),
                &mut registry,
                SourceGraphMaterialProgram {
                    prepass_cull: 2,
                    shaders: vec![SourceGraphShaderArtifact {
                        source: draw,
                        expected_sha256: m.render_shader.sha256,
                    }],
                    forward_index: 0,
                    prepass_index: 0,
                    required_texture_count: texture_handles.len(),
                    color_write_mask: 15,
                    forward_state: SourceGraphForwardState {
                        color_src: 1,
                        color_dst: 0,
                        alpha_src: 1,
                        alpha_dst: 0,
                        cull: 2,
                        compare: 4,
                        write_depth: true,
                    },
                    original_transparent: false,
                    original_alpha_test: false,
                    original_alpha_to_mask: false,
                    requires_original_vertex_color: false,
                },
                SourceGraphUniforms {
                    source_object_from_target_object: Mat4::IDENTITY,
                    target_object_from_source_object: Mat4::IDENTITY,
                    source_world_from_target_world: Mat4::IDENTITY,
                    source_absolute_world_from_target_world: Mat4::IDENTITY,
                    source_time_parameters: Vec4::ZERO,
                    source_previous_time_parameters: Vec4::ZERO,
                    source_scale_mip_min: Vec4::ONE,
                    normal_decode_modes: [Vec4::ZERO; 2],
                },
                texture_handles.clone(),
            )
        })
        .map_err(str::to_owned)?;
    material.extension.scalars = SourceGraphScalarUniforms {
        lanes: std::array::from_fn(|i| {
            Vec4::from_array(std::array::from_fn(|j| f32_at(&parameters, i * 16 + j * 4)))
        }),
    };
    let mesh = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
    let material_handle = app
        .world_mut()
        .resource_mut::<Assets<SourceGraphMaterial>>()
        .add(material);
    let mut material_handles = vec![material_handle.clone()];
    let mut draw_entities = Vec::new();
    let mut continuous_projections = vec![vertex_projection.take()];
    for (index, owner) in owners.iter().enumerate() {
        if index > 0 {
            let clone = app
                .world()
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material_handle)
                .ok_or("Primary material absent")?
                .clone();
            material_handles.push(
                app.world_mut()
                    .resource_mut::<Assets<SourceGraphMaterial>>()
                    .add(clone),
            );
            continuous_projections.push(
                m.vertex_projection
                    .as_ref()
                    .map(|spec| {
                        vertex_projection::Projection::new(
                            &mut app,
                            root,
                            spec,
                            curve.clone(),
                            &vertices,
                        )
                    })
                    .transpose()?,
            );
        }
        let draw = app
            .world_mut()
            .spawn((
                Mesh3d(mesh.clone()),
                MeshMaterial3d(material_handles[index].clone()),
                Transform::IDENTITY,
                Visibility::Hidden,
                NoFrustumCulling,
            ))
            .id();
        if let Some(owner_entity) = owner.sequence.entity() {
            app.world_mut()
                .entity_mut(draw)
                .insert(ChildOf(owner_entity));
        }
        draw_entities.push(draw);
        if let Some(projection) = &mut continuous_projections[index]
            && let Some(owner_entity) = owner.sequence.entity()
        {
            let producer = app
                .world()
                .get::<forge_engine::source_particle_compute::SourceParticleSceneOwner>(
                    owner_entity,
                )
                .ok_or("Scene owner absent")?
                .slot();
            let published_state = owner.sequence.current_buffer(&app)?;
            if let Some(governed) = &governed {
                let prepared =
                    forge_engine::source_particle_transport::PreparedSourceParticleOwner::prepare(
                        &governed.contract.owners[0],
                        &governed.resources,
                    )?;
                let instance =
                    app.world_mut()
                        .resource_scope(|world, mut shaders: Mut<Assets<Shader>>| {
                            world.resource_scope(|world, mut images: Mut<Assets<Image>>| {
                                prepared.instantiate(
                                    &mut shaders,
                                    &mut images,
                                    &mut world.resource_mut::<Assets<ShaderBuffer>>(),
                                    owner_entity,
                                    material_handles[index].clone(),
                                    producer,
                                    u64::try_from(index + 1)
                                        .map_err(|_| "Projection slot overflow")?,
                                )
                            })
                        })?;
                projection.attach_governed(&mut app, owner_entity, instance)?;
            } else {
                projection.install(
                    &mut app,
                    owner_entity,
                    published_state,
                    producer,
                    u64::try_from(index + 1).map_err(|_| "Projection slot overflow")?,
                    material_handles[index].clone(),
                )?;
            }
        }
    }
    let target_bytes = usize::try_from(u64::from(m.target_width) * u64::from(m.target_height) * 8)
        .map_err(|_| "Target byte size overflow")?;
    let mut target = Image::new_target_texture(
        m.target_width,
        m.target_height,
        TextureFormat::Rgba16Float,
        None,
    );
    target.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = app.world_mut().resource_mut::<Assets<Image>>().add(target);
    app.world_mut().spawn((
        Camera3d::default(),
        Camera {
            clear_color: ClearColorConfig::Custom(Color::BLACK),
            ..default()
        },
        RenderTarget::Image(target.clone().into()),
        Hdr,
        Tonemapping::None,
        Msaa::Off,
        Transform::from_xyz(0., 0., 3.).looking_at(Vec3::ZERO, Vec3::Y),
    ));
    let pixels: BytesSlot = Arc::new(Mutex::new(None));
    let sink = pixels.clone();
    app.world_mut()
        .spawn(Readback::texture(target))
        .observe(move |event: On<ReadbackComplete>| {
            if let Ok(mut slot) = sink.lock() {
                *slot = Some(event.data.clone());
            }
        });
    let rounds = owners
        .iter()
        .map(|owner| owner.steps.len())
        .max()
        .unwrap_or(0);
    for i in 0..rounds {
        let stamp = u16::try_from(i + 1).map_err(|_| "Step stamp overflow")?;
        let before: Vec<_> = owners
            .iter()
            .map(|owner| owner.sequence.encoded_steps(&app))
            .collect::<Result<_, _>>()?;
        // Submit every owner before waiting: distinct clocks share one render graph.
        for owner in &mut owners {
            let Some(step) = owner.steps.get(i) else {
                continue;
            };
            if !owner.sequence.submit_step(&mut app, step, &bank, stamp)? {
                return Err("Unexpected pending source owner step".into());
            }
        }
        for owner in &mut owners {
            if i >= owner.steps.len() {
                continue;
            }
            while !owner.sequence.poll(&mut app)? {
                app.update();
                if Instant::now() > deadline {
                    return Err("Resident owner timeline timeout".into());
                }
                std::thread::sleep(Duration::from_millis(2));
            }
        }
        for (owner_index, owner) in owners.iter_mut().enumerate() {
            let Some(step) = owner.steps.get(i) else {
                continue;
            };
            let phases = owner.sequence.encoded_steps(&app)? - before[owner_index];
            if let Some(frame) = owner.sequence.last_frame(&app)? {
                owner.emission_frames.push(serde_json::json!({"step":stamp,"label":step.label,"reset_particles":frame.reset_particles,"spawn_request":frame.spawn_request,"phases":phases,"snapshot":frame.snapshot}));
            }
            if let Some(reference) = &owner.expected[i] {
                let current_buffer = owner.sequence.current_buffer(&app)?;
                let (readback, slot) = receive_buffer(&mut app, current_buffer.clone());
                let data = loop {
                    app.update();
                    if Instant::now() > deadline {
                        return Err("Native checkpoint timeout".into());
                    }
                    if let Some(data) = slot.lock().map_err(|_| "State mutex poisoned")?.take()
                        && data.len() == initial.len()
                        && u32_at(&data, 56) == u32::from(stamp)
                    {
                        break data;
                    }
                    std::thread::sleep(Duration::from_millis(2));
                };
                std::fs::write(
                    owner.directory.join(format!("step-{stamp}.state.bin")),
                    &data,
                )
                .map_err(|e| e.to_string())?;
                verify_state(&data, reference, &initial, stamp)?;
                owner.native_checkpoints += 1;
                app.world_mut().entity_mut(readback).remove::<Readback>();
                if step.draw {
                    let source_buffer =
                        if let Some(projection) = &mut continuous_projections[owner_index] {
                            projection.update(
                                &mut app,
                                &current_buffer,
                                &data,
                                stamp,
                                &owner.directory,
                            )?
                        } else {
                            current_buffer
                        };
                    if continuous_projections[owner_index].is_none()
                        || owner.sequence.entity().is_none()
                    {
                        let binding = SourceGraphStateBufferBinding::new(
                            source_buffer.clone(),
                            app.world().resource::<Assets<ShaderBuffer>>(),
                        )
                        .map_err(str::to_owned)?;
                        app.world_mut()
                            .resource_mut::<Assets<SourceGraphMaterial>>()
                            .get_mut(&material_handles[owner_index])
                            .ok_or("Source material absent")?
                            .extension
                            .set_source_state_buffer(&binding);
                    } else if !app
                        .world()
                        .resource::<Assets<SourceGraphMaterial>>()
                        .get(&material_handles[owner_index])
                        .ok_or("Source material absent")?
                        .extension
                        .is_source_state_buffer(&source_buffer)
                    {
                        return Err(
                            "Continuous projection material was not bound by its engine component"
                                .into(),
                        );
                    }
                    for (index, entity) in draw_entities.iter().enumerate() {
                        app.world_mut()
                            .entity_mut(*entity)
                            .insert(if index == owner_index {
                                Visibility::Visible
                            } else {
                                Visibility::Hidden
                            });
                    }
                    let image = capture(&mut app, &pixels, target_bytes)?;
                    if let Some(projection) = &continuous_projections[owner_index] {
                        owner.projections.push(projection.verify_material_binding(
                            &mut app,
                            &material_handles[owner_index],
                            stamp,
                        )?);
                    }
                    let covered = image
                        .as_chunks::<8>()
                        .0
                        .iter()
                        .filter(|p| p[..6] != [0; 6])
                        .count();
                    let alive = u32_at(&data, 48) != 0;
                    let filename = format!("step-{stamp}.rgba16le.bin");
                    std::fs::write(owner.directory.join(&filename), &image)
                        .map_err(|e| e.to_string())?;
                    owner.draws.push(serde_json::json!({"step":stamp,"label":step.label,"alive":alive,"covered_pixels":covered,"file":filename,"sha256":bytes_sha256(&image).to_string()}));
                    println!("DRAW step{stamp} alive={alive} coverage={covered}");
                    if alive && covered < 100 || !alive && covered != 0 {
                        return Err("Live/dead source coverage mismatch".into());
                    }
                }
            }
        }
        timelines::verify_distinct_gpu_buffers(&app, &owners, initial.len() as u64)?;
    }
    for owner in &owners {
        owner.verify_draws()?;
    }
    let owner_reports: Vec<_> = owners
        .iter()
        .map(|owner| owner.report(&app))
        .collect::<Result<_, _>>()?;
    let primary = &owners[0];
    let total_phases: u64 = owners
        .iter()
        .map(|owner| owner.sequence.encoded_steps(&app))
        .collect::<Result<Vec<_>, _>>()?
        .into_iter()
        .sum();
    let total_frames: usize = owners.iter().map(|owner| owner.steps.len()).sum();
    let native_checkpoints: u32 = owners.iter().map(|owner| owner.native_checkpoints).sum();
    let texture_bindings = verify_texture_bindings(&mut app, &material_handle, &texture_handles)?;
    let texture_mip_readbacks = if m.readback_texture_mips {
        texture_mips::read(&app, &texture_handles, &m.textures, &output)?
    } else {
        Vec::new()
    };
    let report = serde_json::json!({"status":"GPU_RESIDENT_SOURCE_MESH_DIAGNOSTIC_DRAW_MOTION_DEATH_PASS","renderer_adapter":{"name":adapter_info.name,"backend":format!("{:?}",adapter_info.backend),"vendor":adapter_info.vendor,"device":adapter_info.device,"driver":adapter_info.driver,"driver_info":adapter_info.driver_info,"device_type":format!("{:?}",adapter_info.device_type),"anisotropic_filtering_supported":anisotropic},"target_width":m.target_width,"target_height":m.target_height,"steps":m.steps.len(),"native_checkpoints":native_checkpoints,"source_vertex_count":vertices.len()/64,"source_triangles":indices.len()/6,"CPU_state_uploads_between_steps":0,"source_material_or_full_effect_qualified":false,"source_textures":texture_bindings,"texture_mip_GPU_readbacks":texture_mip_readbacks,"resident_vertex_projection_draws":primary.projections,"draws":primary.draws});
    let mut report = report;
    report["encoded_compute_phases"] = serde_json::json!(primary.sequence.encoded_steps(&app)?);
    report["source_emission_frames"] = serde_json::json!(primary.emission_frames);
    report["owners"] = serde_json::json!(owner_reports);
    report["total_logical_frames"] = serde_json::json!(total_frames);
    report["total_encoded_compute_phases"] = serde_json::json!(total_phases);
    report["shared_render_rounds"] = serde_json::json!(rounds);
    report["distinct_owner_physical_GPU_buffer_checks"] = serde_json::json!(rounds);
    report["stable_GPU_publication_used"] = serde_json::json!(true);
    report["continuous_vertex_projection_frames"] = serde_json::json!(
        continuous_projections
            .iter()
            .map(|projection| projection
                .as_ref()
                .map(|projection| projection.encoded_frames(&app))
                .transpose())
            .collect::<Result<Vec<_>, _>>()?
    );
    report["projection_dispatches_triggered_by_checkpoints"] = serde_json::json!(false);
    report["scene_owner_native_wait_probe"] = if let Some(parameters) = m.source_spawner {
        let reference_index = m
            .steps
            .iter()
            .position(|step| step.expected.is_some())
            .ok_or("Initial native oracle absent")?;
        if !parameters.initial_play
            || m.steps[..=reference_index].iter().any(|step| {
                step.delta_time.to_bits() != 0 || !step.events.is_empty() || step.reinitialize
            })
        {
            return Err("Native waiting probe requires a pinned zero-delta initial oracle".into());
        }
        let reference = read(
            root,
            m.steps[reference_index]
                .expected
                .as_ref()
                .ok_or("Initial native oracle pin absent")?,
        )?;
        scene_probe::verify(
            &mut app,
            &shader,
            &curve,
            &m.entry_point,
            parameters,
            &bank,
            &initial,
            &reference,
            &output,
        )?
    } else {
        serde_json::Value::Null
    };
    let entities: Vec<_> = app
        .world_mut()
        .query_filtered::<Entity, With<Readback>>()
        .iter(app.world())
        .collect();
    for entity in entities {
        app.world_mut().entity_mut(entity).remove::<Readback>();
    }
    for _ in 0..4 {
        app.update();
    }
    let owner_entities: Vec<_> = owners
        .iter()
        .filter_map(|owner| owner.sequence.entity())
        .collect();
    for owner_entity in &owner_entities {
        app.world_mut().despawn(*owner_entity);
    }
    for _ in 0..4 {
        app.update();
    }
    if owner_entities
        .iter()
        .any(|entity| app.world().entities().contains(*entity))
        || !owner_entities.is_empty()
            && [1, 2, 7, 9].iter().any(|slot| {
                app.world()
                    .resource::<SourceParticleComputeQueue>()
                    .contains_slot(*slot)
            })
    {
        return Err("Retired scene owner entity or compute slot remains active".into());
    }
    report["scene_owner_entities_retired"] = serde_json::json!(owner_entities.len());
    report["scene_owner_compute_slots_removed"] = serde_json::json!(true);
    report["continuous_projection_compute_slots_removed"] = serde_json::json!(true);
    std::fs::write(
        output.join("RESULT.v1.json"),
        serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    println!("PASS resident source mesh draw, motion and death; source fragment still pending");
    Ok(())
}
fn main() {
    if let Err(e) = run() {
        eprintln!("FAIL {e}");
        std::process::exit(1);
    }
}
