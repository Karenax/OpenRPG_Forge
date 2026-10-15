//! Pinned compute state on the actual `SourceGraph` GPU material binding.
use bevy::{
    app::PluginsState,
    asset::RenderAssetUsages,
    ecs::system::SystemState,
    image::{ImageSampler, ImageSamplerDescriptor},
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        pipelined_rendering::PipelinedRenderingPlugin,
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, Extent3d, OwnedBindingResource, PipelineCache, TextureDimension,
            TextureFormat,
        },
        renderer::RenderDevice,
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
    window::{ExitCondition, WindowPlugin},
    winit::WinitPlugin,
};
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::{
    source_graph::{
        SourceGraphExtension, SourceGraphForwardState, SourceGraphKey, SourceGraphMaterial,
        SourceGraphPlugin, SourceGraphStateBufferBinding, SourceGraphSurfacePhase,
        SourceGraphUniforms, SourceGraphUvRequirements, source_graph_material,
    },
    source_particle_compute::{
        SourceParticleComputePlugin, SourceParticleComputeQueue, SourceParticleComputeRequest,
        SourceParticleComputeStatus, register_source_particle_compute_program,
    },
};
use serde::Deserialize;
use std::{
    path::{Component, Path, PathBuf},
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Pin {
    file: String,
    sha256: ContentHash,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema_version: u32,
    shader: Pin,
    atlas: Pin,
    bank: Pin,
    initial: Pin,
    expected: Pin,
    width: u32,
    height: u32,
    entry_point: String,
}
fn read(root: &Path, pin: &Pin) -> Result<Vec<u8>, String> {
    let path = Path::new(&pin.file);
    if path.components().count() != 1
        || !matches!(path.components().next(), Some(Component::Normal(_)))
    {
        return Err("Expected flat fixture path".into());
    }
    let path = root.join(path);
    if std::fs::metadata(&path).map_err(|e| e.to_string())?.len() > 64 * 1024 * 1024 {
        return Err("Fixture exceeds proof byte budget".into());
    }
    let bytes = std::fs::read(path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != pin.sha256 {
        return Err("Fixture SHA mismatch".into());
    }
    Ok(bytes)
}
fn key() -> SourceGraphKey {
    SourceGraphKey {
        requires_source_state: true,
        source_state_ready: false,
        requires_native_srgb_encoder: false,
        native_srgb_encoder_ready: false,
        requires_deferred_srgb: false,
        deferred_srgb_ready: false,
        requires_preintegrated_brdf: false,
        preintegrated_brdf_ready: false,
        requires_sky_cube: false,
        sky_cube_ready: false,
        prepass_cull: 2,
        shadow_only: false,
        orthographic_shadow_raster: None,
        vertex: 1,
        fragment: 2,
        prepass_vertex: 3,
        prepass_fragment: 4,
        original_alpha_to_mask: false,
        requires_original_vertex_color: false,
        original_uv_requirements: SourceGraphUvRequirements::default(),
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
    }
}
fn uniforms() -> SourceGraphUniforms {
    SourceGraphUniforms {
        source_object_from_target_object: Mat4::IDENTITY,
        target_object_from_source_object: Mat4::IDENTITY,
        source_world_from_target_world: Mat4::IDENTITY,
        source_absolute_world_from_target_world: Mat4::IDENTITY,
        source_time_parameters: Vec4::ZERO,
        source_previous_time_parameters: Vec4::ZERO,
        source_scale_mip_min: Vec4::ONE,
        normal_decode_modes: [Vec4::ZERO; 2],
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Ordered private compute, material binding and readback evidence"
)]
fn run() -> Result<(), String> {
    let mut args = std::env::args().skip(1);
    let manifest_path = PathBuf::from(args.next().ok_or("Expected manifest")?);
    let hash = args
        .next()
        .ok_or("Expected SHA")?
        .parse::<ContentHash>()
        .map_err(|e| e.to_string())?;
    let evidence = PathBuf::from(args.next().ok_or("Expected fresh evidence directory")?);
    let bytes = std::fs::read(&manifest_path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes) != hash {
        return Err("Manifest SHA mismatch".into());
    }
    let m: Manifest = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    let root = manifest_path.parent().ok_or("Manifest parent absent")?;
    let shader = String::from_utf8(read(root, &m.shader)?).map_err(|e| e.to_string())?;
    let atlas = read(root, &m.atlas)?;
    let bank: [u8; 1024] = read(root, &m.bank)?
        .try_into()
        .map_err(|_| "Curve bank ABI mismatch")?;
    let initial = read(root, &m.initial)?;
    let expected = read(root, &m.expected)?;
    if m.schema_version != 1
        || initial.len() != expected.len()
        || initial.is_empty()
        || initial.len() % 16 != 0
        || m.width == 0
        || m.height == 0
        || atlas.len() as u64 != u64::from(m.width) * u64::from(m.height) * 8
    {
        return Err("Resource cardinality mismatch".into());
    }
    std::fs::create_dir(&evidence).map_err(|e| e.to_string())?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    )
    .add_plugins((SourceGraphPlugin, SourceParticleComputePlugin));
    let deadline = Instant::now() + Duration::from_secs(60);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Renderer startup timeout".into());
        }
        std::thread::sleep(Duration::from_millis(8));
    }
    app.finish();
    app.cleanup();
    app.update();
    let shader = register_source_particle_compute_program(
        &mut app.world_mut().resource_mut::<Assets<Shader>>(),
        &shader,
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
    let input = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&initial, RenderAssetUsages::default()));
    let output =
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::with_size(
                expected.len(),
                RenderAssetUsages::default(),
            ));
    let request = SourceParticleComputeRequest::new(
        shader,
        m.entry_point,
        curve,
        bank,
        input,
        output.clone(),
        1,
    )
    .map_err(str::to_owned)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(7, request.clone());
    let captured = Arc::new(Mutex::new(None));
    let sink = captured.clone();
    app.world_mut()
        .spawn(Readback::buffer(output.clone()))
        .observe(move |event: On<ReadbackComplete>| {
            if let Ok(mut slot) = sink.lock() {
                *slot = Some(event.data.clone());
            }
        });
    let data = loop {
        app.update();
        if Instant::now() > deadline {
            return Err("Source compute/readback timeout".into());
        }
        match request.status().map_err(str::to_owned)? {
            SourceParticleComputeStatus::Failed(e) => return Err(e),
            SourceParticleComputeStatus::Encoded => {
                if let Some(bytes) = captured
                    .lock()
                    .map_err(|_| "Readback mutex poisoned")?
                    .take()
                    && bytes == expected
                {
                    break bytes;
                }
            }
            _ => {}
        }
        std::thread::sleep(Duration::from_millis(8));
    };
    std::fs::write(evidence.join("resident-source-state.bin"), &data).map_err(|e| e.to_string())?;
    let mut extension = SourceGraphExtension::from_source_bindings(key(), uniforms(), 0, vec![])
        .map_err(str::to_owned)?;
    if extension
        .validate_scene_dependencies(app.world().resource::<Assets<Image>>())
        .is_ok()
    {
        return Err("Missing required state was accepted".into());
    }
    let binding = SourceGraphStateBufferBinding::new(
        output.clone(),
        app.world().resource::<Assets<ShaderBuffer>>(),
    )
    .map_err(str::to_owned)?;
    extension.set_source_state_buffer(&binding);
    extension
        .validate_source_state_buffer(app.world().resource::<Assets<ShaderBuffer>>())
        .map_err(str::to_owned)?;
    let material = source_graph_material(extension, SourceGraphSurfacePhase::Opaque);
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
        .map_err(|e| format!("Material binding not prepared: {e:?}"))?;
    let gpu = render
        .resource::<RenderAssets<GpuShaderBuffer>>()
        .get(&output)
        .ok_or("GPU source state absent")?;
    let (_, resource) = prepared
        .bindings
        .iter()
        .find(|(n, _)| *n == 130)
        .ok_or("Material state binding absent")?;
    match resource {
        OwnedBindingResource::Buffer(buffer) if buffer.id() == gpu.buffer.id() => {}
        _ => return Err("Material does not bind the actual compute state buffer".into()),
    }
    let report = serde_json::json!({"status":"GPU_COMPUTE_STATE_TO_SOURCEGRAPH_BINDING_IDENTITY_PASS","source_state_bytes":data.len(),"source_state_sha256":bytes_sha256(&data).to_string(),"actual_compute_buffer_and_material_binding_130_identical":true,"required_missing_state_rejected":true,"no_CPU_state_copy_between_compute_and_material":true,"material_draw_or_effect_visual_parity_qualified":false});
    std::fs::write(
        evidence.join("RESULT.v1.json"),
        serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    println!(
        "PASS {} native state bytes; material binding130 is the same GPU buffer",
        data.len()
    );
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
    Ok(())
}
fn main() {
    if let Err(e) = run() {
        eprintln!("FAIL {e}");
        std::process::exit(1);
    }
}
