//! Actual common GLB importer, native image residency and material binding probe.
//! This performs no drawing and does not qualify source lighting or sampling.
use bevy::{
    app::PluginsState,
    ecs::system::SystemState,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, OwnedBindingResource, PipelineCache, PollType, WgpuFeatures, encase,
        },
        renderer::{RenderAdapterInfo, RenderDevice},
        settings::{Backends, RenderCreation, WgpuSettings},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{
    source_graph::{
        SourceGraphMaterial, SourceGraphPlugin, SourceGraphShaderRegistry, SourceGraphUniforms,
    },
    source_graph_glb::{source_graph_mesh_from_glb, source_graph_program_from_glb},
};
use std::{
    path::Path,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[allow(
    clippy::too_many_lines,
    reason = "Keep the single residency and binding observation in diagnostic execution order."
)]
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().collect();
    let root = Path::new(&args[1]);
    let input: serde_json::Value =
        serde_json::from_slice(&std::fs::read(root.join("INPUT.v1.json"))?)?;
    let raw = std::fs::read(input["glb"].as_str().ok_or("GLB path required")?)?;
    let glb = forge_assets::source_graph_glb::read_source_graph_glb(&raw)?
        .ok_or("source graph required")?;
    assert_eq!(glb.materials.len(), 1);
    let (_, contract) = &glb.materials[0];
    assert_eq!(contract.textures.len(), 10);
    let frame = &glb.document()["extras"]["source_frame_binding"];
    let matrix = |key: &str| -> Result<Mat4, serde_json::Error> {
        Ok(Mat4::from_cols_array(&serde_json::from_value::<[f32; 16]>(
            frame[key].clone(),
        )?))
    };
    let uniforms = SourceGraphUniforms {
        source_object_from_target_object: matrix("source_object_from_target_object")?,
        target_object_from_source_object: matrix("target_object_from_source_object")?,
        source_world_from_target_world: Mat4::IDENTITY,
        source_absolute_world_from_target_world: Mat4::IDENTITY,
        source_time_parameters: Vec4::ZERO,
        source_previous_time_parameters: Vec4::ZERO,
        source_scale_mip_min: Vec4::new(1., 1., 0., f32::MIN_POSITIVE),
        normal_decode_modes: [Vec4::ZERO; 2],
    };
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
                    backends: Some(Backends::DX12),
                    features: WgpuFeatures::TEXTURE_COMPRESSION_BC,
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    )
    .add_plugins(SourceGraphPlugin);
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("GPU initialization timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    app.update();
    let device = app
        .get_sub_app(RenderApp)
        .unwrap()
        .world()
        .resource::<RenderDevice>()
        .clone();
    assert!(
        device
            .features()
            .contains(WgpuFeatures::TEXTURE_COMPRESSION_BC)
    );
    let errors: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));
    let sink = errors.clone();
    device
        .wgpu_device()
        .on_uncaptured_error(Arc::new(move |e| sink.lock().unwrap().push(e.to_string())));
    let program = source_graph_program_from_glb(contract, &glb.views)?;
    assert!(program.requires_captured_images());
    let world = app.world_mut();
    let mut shaders = world
        .remove_resource::<Assets<Shader>>()
        .ok_or("shader assets absent")?;
    let mut images = world
        .remove_resource::<Assets<Image>>()
        .ok_or("image assets absent")?;
    let mut registry = world
        .remove_resource::<SourceGraphShaderRegistry>()
        .ok_or("shader registry absent")?;
    let material_result = program.register_captured_images(
        &mut shaders,
        &mut registry,
        &mut images,
        &device,
        uniforms,
    );
    let material = material_result?;
    let ext = &material.extension;
    let handles = [
        &ext.texture_0,
        &ext.texture_1,
        &ext.texture_2,
        &ext.texture_3,
        &ext.texture_4,
        &ext.texture_5,
        &ext.texture_6,
        &ext.texture_7,
        &ext.texture_8,
        &ext.texture_9,
    ]
    .into_iter()
    .map(|h| h.clone().expect("Every authored texture is required"))
    .collect::<Vec<_>>();
    let mut rows = Vec::new();
    for (slot, handle) in handles.iter().enumerate() {
        let image = images.get(handle).ok_or("authored image absent")?;
        let storage = contract.textures[slot]
            .imported_storage
            .as_ref()
            .ok_or("native storage absent")?;
        let bytes = image.data.as_ref().ok_or("native image bytes absent")?;
        assert_eq!(forge_core::bytes_sha256(bytes), storage.storage.sha256);
        assert_eq!(bytes.as_slice(), glb.views[storage.storage.buffer_view]);
        assert_eq!(image.texture_descriptor.mip_level_count, 12);
        rows.push(serde_json::json!({"slot":slot,"native_bytes":bytes.len(),"byte_equal":true,
            "mips":image.texture_descriptor.mip_level_count,"format":format!("{:?}",image.texture_descriptor.format),
            "sampler":format!("{:?}",image.sampler)}));
    }
    let scene_admission = ext
        .validate_scene_dependencies(&images)
        .map_err(str::to_owned);
    let requires_brdf = ext.program_key.requires_preintegrated_brdf;
    let requires_sky = ext.program_key.requires_sky_cube;
    let mut encoded = encase::UniformBuffer::new(Vec::<u8>::new());
    encoded.write(&ext.source)?;
    let uniform_bytes = encoded.into_inner();
    assert_eq!(uniform_bytes.len(), 336);
    std::fs::write(root.join("SOURCE-UNIFORMS.bin"), &uniform_bytes)?;
    world.insert_resource(shaders);
    world.insert_resource(images);
    world.insert_resource(registry);
    let mut meshes = world.resource_mut::<Assets<Mesh>>();
    let mut mesh_rows = Vec::new();
    for index in 0..3 {
        let imported = source_graph_mesh_from_glb(&glb, index, 0)?;
        mesh_rows.push(serde_json::json!({"lod":index,"vertices":imported.mesh.count_vertices(),"material_index":imported.material_index}));
        meshes.add(imported.mesh);
    }
    let deadline = Instant::now() + Duration::from_secs(30);
    loop {
        app.update();
        let gpu = app
            .get_sub_app(RenderApp)
            .unwrap()
            .world()
            .resource::<RenderAssets<GpuImage>>();
        if handles.iter().all(|h| gpu.get(h).is_some()) {
            break;
        }
        if Instant::now() > deadline {
            return Err("Authored GPU image residency timed out".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    let render_world = app.get_sub_app_mut(RenderApp).unwrap().world_mut();
    let mut state = SystemState::<<SourceGraphMaterial as AsBindGroup>::Param>::new(render_world);
    let descriptor = SourceGraphMaterial::bind_group_layout_descriptor(&device);
    let prepared = render_world.resource_scope(|world, cache: Mut<PipelineCache>| {
        let mut params = state
            .get_mut(world)
            .expect("Material system parameters required");
        let layout = cache.get_bind_group_layout(&descriptor);
        material.unprepared_bind_group(&layout, &device, &mut params, true)
    })?;
    let gpu = render_world.resource::<RenderAssets<GpuImage>>();
    for (slot, handle) in handles.iter().enumerate() {
        let image = gpu.get(handle).unwrap();
        let binding = if slot < 9 {
            101 + u32::try_from(slot)? * 2
        } else {
            125
        };
        let texture = prepared
            .bindings
            .iter()
            .find(|(n, _)| *n == binding)
            .ok_or("texture binding absent")?;
        let sampler = prepared
            .bindings
            .iter()
            .find(|(n, _)| *n == binding + 1)
            .ok_or("sampler binding absent")?;
        match &texture.1 {
            OwnedBindingResource::TextureView(_, v) => assert_eq!(v.id(), image.texture_view.id()),
            _ => panic!("Wrong texture resource"),
        }
        match &sampler.1 {
            OwnedBindingResource::Sampler(_, s) => assert_eq!(s.id(), image.sampler.id()),
            _ => panic!("Wrong sampler resource"),
        }
        assert_eq!(image.texture_descriptor.mip_level_count, 12);
        rows[slot]["gpu_resident"] = true.into();
        rows[slot]["actual_binding_identity_verified"] = true.into();
    }
    let binding_ids = prepared
        .bindings
        .iter()
        .map(|(n, _)| *n)
        .collect::<Vec<_>>();
    let mut allocator = bevy::pbr::MaterialBindGroupAllocator::new(
        &device,
        "source-glb-resource-probe",
        None,
        descriptor.clone(),
        None,
    );
    let allocation = allocator.allocate_unprepared(prepared, &descriptor);
    allocator.prepare_bind_groups(
        &device,
        render_world.resource::<PipelineCache>(),
        render_world.resource::<bevy::pbr::FallbackBindlessResources>(),
        render_world.resource::<bevy::render::texture::FallbackImage>(),
    );
    assert!(
        allocator
            .get(allocation.group)
            .and_then(|s| s.bind_group())
            .is_some(),
        "Actual material allocator did not prepare the bind group"
    );
    device.wgpu_device().poll(PollType::Poll)?;
    let captured = errors.lock().unwrap().clone();
    assert!(captured.is_empty(), "GPU validation errors: {captured:?}");
    let info = render_world.resource::<RenderAdapterInfo>();
    let result = serde_json::json!({"status":"ten_native_glb_images_resident_and_actual_material_bind_group_created",
        "adapter":info.name,"backend":format!("{:?}",info.backend),"textures":rows,"meshes":mesh_rows,
        "source_uniform_bytes":uniform_bytes.len(),"authored_texture_substitution":false,
        "authored_sampler_policy":"Original importer policy; implicit sampling fidelity remains unqualified",
        "actual_resource_bind_groups_created":true,"binding_ids":binding_ids,
        "material_allocator":"Actual Bevy non-bindless allocator; OwnedData packed into native uniform buffers",
        "required_scene_brdf":requires_brdf,"required_scene_sky":requires_sky,
        "scene_admission":format!("{scene_admission:?}"),"scene_resources_qualified":false,
        "unused_or_unbound_scene_bindings_use_engine_fallback":true,"gpu_validation_errors":captured,
        "gpu_draw_executed":false,"source_render_parity_qualified":false,"forge_editor_verified":false,"full_import":false});
    std::fs::write(
        root.join("NATIVE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    Ok(())
}
