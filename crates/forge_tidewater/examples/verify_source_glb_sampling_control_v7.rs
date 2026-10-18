//! Actual common GLB importer, native image residency and material binding probe.
//! Draw the real source material; compare separately before claiming source fidelity.
use bevy::{
    app::PluginsState,
    ecs::system::SystemState,
    prelude::*,
    render::{
        RenderApp, RenderPlugin,
        pipelined_rendering::PipelinedRenderingPlugin,
        render_asset::RenderAssets,
        render_resource::{
            AsBindGroup, OwnedBindingResource, PipelineCache, PollType, TextureFormat,
            TextureUsages, WgpuFeatures, encase,
        },
        renderer::{RenderAdapter, RenderAdapterInfo, RenderDevice},
        settings::{Backends, RenderCreation, WgpuSettings},
        texture::GpuImage,
    },
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{
    source_graph::{
        SourceGraphDiffuseProbeUniforms, SourceGraphMaterial, SourceGraphPlugin,
        SourceGraphPreintegratedBrdf, SourceGraphSceneSampling, SourceGraphShaderRegistry,
        SourceGraphSkyCube, SourceGraphUniforms,
    },
    source_graph_glb::{source_graph_mesh_from_glb, source_graph_program_from_glb},
};
use std::{
    path::Path,
    sync::{Arc, Mutex},
    time::{Duration, Instant},
};

#[allow(
    clippy::cast_possible_truncation,
    reason = "Source f64 diagnostic inputs are intentionally rounded to the f32 GPU ABI"
)]
fn source_gpu_float(value: f64) -> f32 {
    value as f32
}

#[allow(
    clippy::too_many_lines,
    reason = "One ordered native diagnostic retains resource, draw, readback and refusal evidence together"
)]
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().collect();
    let root = Path::new(&args[1]);
    let input: serde_json::Value =
        serde_json::from_slice(&std::fs::read(root.join("INPUT.v1.json"))?)?;
    let context: serde_json::Value = serde_json::from_slice(&std::fs::read(
        input["source_context"]
            .as_str()
            .ok_or("Source context required")?,
    )?)?;
    let vector = |field: &str| -> Vec3 {
        let v = &context[field];
        Vec3::new(
            source_gpu_float(v["x"].as_f64().unwrap()),
            source_gpu_float(v["y"].as_f64().unwrap()),
            source_gpu_float(v["z"].as_f64().unwrap()),
        )
    };
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
        source_world_from_target_world: matrix("source_object_from_target_object")?,
        source_absolute_world_from_target_world: matrix("source_object_from_target_object")?,
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
    let policy = match context["anisotropic_filtering"]
        .as_str()
        .ok_or("Source sampling context absent")?
    {
        // Explicit diagnostic override, compare separately to source Disable.
        "ForceEnable" | "Enable" => SourceGraphSceneSampling::Authored,
        other => return Err(format!("Unqualified scene sampling policy: {other}").into()),
    };
    let adapter = app
        .get_sub_app(RenderApp)
        .unwrap()
        .world()
        .resource::<RenderAdapter>();
    let program = source_graph_program_from_glb(contract, &glb.views)?
        .with_scene_sampling(policy, Some(adapter))?;
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
    let mut material = material_result?;
    let values = context["actual_gpu_ambient_coefficients"]
        .as_array()
        .ok_or("Measured GPU probe absent")?;
    if values.len() != 28 {
        return Err("Measured GPU probe length differs".into());
    }
    let mut coefficients = [Vec4::ZERO; 7];
    for i in 0..7 {
        for ch in 0..4 {
            coefficients[i][ch] =
                source_gpu_float(values[i * 4 + ch].as_f64().ok_or("Non-numeric probe")?);
        }
    }
    material.extension.diffuse_probe = SourceGraphDiffuseProbeUniforms::from_preconvolved(
        coefficients,
        matrix("source_object_from_target_object")?,
    )?;
    let brdf_bytes = std::fs::read(
        input["brdf_path"]
            .as_str()
            .ok_or("Source BRDF path absent")?,
    )?;
    let brdf_sha = forge_core::bytes_sha256(&brdf_bytes);
    if serde_json::to_value(brdf_sha)? != input["brdf_sha256"] {
        return Err("Source BRDF SHA differs".into());
    }
    let binding = SourceGraphPreintegratedBrdf::from_rgba32f_le(&brdf_bytes, brdf_sha, 64, 64)?
        .install_unqualified(&mut images);
    material.extension.set_preintegrated_brdf(&binding);
    let sky_bytes = std::fs::read(
        input["sky_path"]
            .as_str()
            .ok_or("Measured sky path absent")?,
    )?;
    let sky_sha = forge_core::bytes_sha256(&sky_bytes);
    if serde_json::to_value(sky_sha)? != input["sky_sha256"] {
        return Err("Measured sky SHA differs".into());
    }
    let sky_binding =
        SourceGraphSkyCube::from_mip_major_rgba16f_le(&sky_bytes, sky_sha, 256, 1, 9)?
            .install_unqualified(&mut images);
    sky_binding.validate_residency(&images)?;
    material.extension.set_sky_cube(&sky_binding);

    material.extension.validate_scene_dependencies(&images)?;
    let mut ambient_encoded = encase::UniformBuffer::new(Vec::<u8>::new());
    ambient_encoded.write(&material.extension.diffuse_probe)?;
    std::fs::write(
        root.join("AMBIENT-UNIFORMS.bin"),
        ambient_encoded.into_inner(),
    )?;

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
    let _scene_admission = ext
        .validate_scene_dependencies(&images)
        .map_err(str::to_owned);
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
    let mut mesh_handles = Vec::new();
    for index in 0..3 {
        let imported = source_graph_mesh_from_glb(&glb, index, 0)?;
        mesh_rows.push(serde_json::json!({"lod":index,"vertices":imported.mesh.count_vertices(),"material_index":imported.material_index}));
        mesh_handles.push(meshes.add(imported.mesh));
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
    let adapter_name = info.name.clone();
    let backend = format!("{:?}", info.backend);
    let native_binding_result = serde_json::json!({"adapter":adapter_name,"backend":backend,"textures":rows,"meshes":mesh_rows,
        "source_uniform_bytes":uniform_bytes.len(),"binding_ids":binding_ids,"gpu_validation_errors":captured});
    std::fs::write(
        root.join("RESOURCE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&native_binding_result)?,
    )?;
    let world = app.world_mut();
    world.insert_resource(GlobalAmbientLight::NONE);
    let material_handle = world
        .resource_mut::<Assets<SourceGraphMaterial>>()
        .add(material);
    world.spawn((
        Mesh3d(mesh_handles[0].clone()),
        MeshMaterial3d(material_handle),
        Transform::IDENTITY,
    ));
    let mut target = Image::new_target_texture(512, 512, TextureFormat::Rgba16Float, None);
    target.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target_handle = world.resource_mut::<Assets<Image>>().add(target);
    let reflection = Mat4::from_scale(Vec3::new(-1., 1., 1.));
    let centre = reflection.transform_point3(vector("bounds_center"));
    let position = reflection.transform_point3(vector("camera_position"));
    let camera_transform = Transform::from_translation(position).looking_at(centre, Vec3::Y);
    world.spawn((
        Camera3d::default(),
        Camera {
            clear_color: ClearColorConfig::Custom(Color::linear_rgba(0.0, 0.0, 0.0, 0.0)),
            ..default()
        },
        bevy::camera::RenderTarget::Image(target_handle.clone().into()),
        bevy::camera::Hdr,
        Projection::Perspective(PerspectiveProjection {
            fov: source_gpu_float(context["fov"].as_f64().unwrap()).to_radians(),
            aspect_ratio: 1.,
            near: source_gpu_float(context["near"].as_f64().unwrap()),
            far: source_gpu_float(context["far"].as_f64().unwrap()),
            ..default()
        }),
        bevy::camera::Exposure {
            ev100: source_gpu_float(context["exposure_ev"].as_f64().unwrap()),
        },
        bevy::core_pipeline::tonemapping::Tonemapping::None,
        Msaa::Off,
        bevy::core_pipeline::prepass::DepthPrepass,
        camera_transform,
    ));
    world.spawn((
        DirectionalLight {
            illuminance: source_gpu_float(context["light_intensity"].as_f64().unwrap()),
            shadow_maps_enabled: false,
            ..default()
        },
        camera_transform,
    ));
    let pixels: Arc<Mutex<Option<Vec<u8>>>> = Arc::new(Mutex::new(None));
    let received = pixels.clone();
    world
        .spawn(bevy::render::gpu_readback::Readback::texture(target_handle))
        .observe(
            move |event: On<bevy::render::gpu_readback::ReadbackComplete>| {
                *received.lock().unwrap() = Some(event.data.clone());
            },
        );
    let deadline = Instant::now() + Duration::from_secs(90);
    let mut frames = 0;
    let mut previous: Option<Vec<u8>> = None;
    let mut stable = 0;
    let captured_image = loop {
        app.update();
        frames += 1;
        device.wgpu_device().poll(PollType::Poll)?;
        let cache = app
            .get_sub_app(RenderApp)
            .unwrap()
            .world()
            .resource::<PipelineCache>();
        let pending = cache.waiting_pipelines().count();
        if frames > 30
            && pending == 0
            && let Some(bytes) = pixels.lock().unwrap().take()
        {
            assert_eq!(bytes.len(), 512 * 512 * 8);
            let varied = bytes
                .as_chunks::<8>()
                .0
                .iter()
                .filter(|p| *p != &bytes[..8])
                .count();
            if varied > 100 {
                if previous.as_ref() == Some(&bytes) {
                    stable += 1;
                } else {
                    stable = 0;
                }
                previous = Some(bytes.clone());
                if stable >= 2 {
                    break bytes;
                }
            }
        }
        if Instant::now() > deadline {
            return Err("No stable nonblank source-material camera draw in 90 seconds".into());
        }
        std::thread::sleep(Duration::from_millis(20));
    };
    assert!(
        errors.lock().unwrap().is_empty(),
        "GPU validation errors after drawing"
    );
    std::fs::write(root.join("FORGE-LINEAR.rgba16f"), &captured_image)?;
    let result = serde_json::json!({"status":"actual_source_glb_lod0_camera_draw_and_stable_gpu_readback",
        "adapter":adapter_name,"backend":backend,"lod":0,"frames":frames,"consecutive_equal_readbacks":stable+1,
        "native_images":10,"native_mips":120,"authored_texture_substitution":false,
        "diagnostic_scene_sampling_override":"Authored; source reference context retained; compare to separate Disable control","scene_sampling_policy":format!("{policy:?}"),"source_context":input["source_context"],
        "camera_position":position.to_array(),"camera_target":centre.to_array(),"camera_ev100":context["exposure_ev"],
        "source_state":"Actual common material state; depth equal with original custom depth prepass",
        "measured_sky_reflection_applied":true,"source_camera_lit_shader_mode":context["actual_lit_shader_mode"],"source_sky_sha256":input["sky_sha256"],"source_ssao_qualified":false,"measured_gpu_ambient_applied":true,"source_gpu_ambient_coefficients":context["actual_gpu_ambient_coefficients"],"source_brdf_sha256":input["brdf_sha256"],"gpu_draw_executed":true,"image":"FORGE-LINEAR.rgba16f","image_bytes":captured_image.len(),
        "target_format":"Rgba16Float","tonemapping":"None","source_reference_tonemapping":"None",
        "source_ambient_light_qualified":false,"source_sampling_parity_qualified":false,"source_lighting_parity_qualified":false,
        "source_render_parity_qualified":false,"forge_editor_verified":false,"full_import":false});
    std::fs::write(
        root.join("NATIVE-RESULT.v1.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    Ok(())
}
