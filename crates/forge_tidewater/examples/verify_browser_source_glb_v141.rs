//! Async Web test of the actual common loader; no purchased bytes embedded.
#[cfg(target_arch = "wasm32")]
mod browser {
    //! Actual common GLB importer, native image residency and material binding probe.
    //! Draw the real source material; compare separately before claiming source fidelity.
    use bevy::{
        app::PluginsState,
        ecs::system::SystemState,
        prelude::*,
        render::{
            RenderApp, RenderPlugin,
            render_asset::RenderAssets,
            render_resource::*,
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
        time::Duration,
    };

    async fn run_inner(
        input_json: String,
        raw: Vec<u8>,
        brdf_bytes: Vec<u8>,
        sky_bytes: Vec<u8>,
    ) -> Result<String, Box<dyn std::error::Error>> {
        let root = Path::new("local-capture-keys");
        let mut captures = std::collections::BTreeMap::<String, Vec<u8>>::new();
        let input: serde_json::Value = serde_json::from_str(&input_json)?;
        let context = input["context"].clone();
        let vector = |field: &str| -> Vec3 {
            let v = &context[field];
            Vec3::new(
                v["x"].as_f64().unwrap() as f32,
                v["y"].as_f64().unwrap() as f32,
                v["z"].as_f64().unwrap() as f32,
            )
        };
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
            source_time_parameters: Vec4::new(
                0.0,
                0.0,
                0.0,
                input["diagnostic_layer"]
                    .as_u64()
                    .filter(|v| *v < 4)
                    .ok_or("Lighting layer 0..3 required")? as f32,
            ),
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
                        backends: Some(Backends::BROWSER_WEBGPU),
                        features: WgpuFeatures::TEXTURE_COMPRESSION_BC
                            | WgpuFeatures::RG11B10UFLOAT_RENDERABLE,
                        ..default()
                    })),
                    ..default()
                })
                .disable::<WinitPlugin>(),
        )
        .add_plugins(SourceGraphPlugin);
        let deadline = Instant::now() + Duration::from_secs(30);
        while app.plugins_state() != PluginsState::Ready {
            if Instant::now() > deadline {
                return Err("GPU initialization timed out".into());
            }
            next_frame().await?;
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
        assert!(device.features().contains(
            WgpuFeatures::TEXTURE_COMPRESSION_BC | WgpuFeatures::RG11B10UFLOAT_RENDERABLE
        ));
        let errors: Arc<Mutex<Vec<String>>> = Arc::new(Mutex::new(Vec::new()));
        let sink = errors.clone();
        device
            .wgpu_device()
            .on_uncaptured_error(Arc::new(move |e| sink.lock().unwrap().push(e.to_string())));
        let policy = match context["anisotropic_filtering"]
            .as_str()
            .ok_or("Source sampling context absent")?
        {
            "ForceEnable" => SourceGraphSceneSampling::UnityForceEnableD3d,
            "Enable" => SourceGraphSceneSampling::Authored,
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
                    values[i * 4 + ch].as_f64().ok_or("Non-numeric probe")? as f32;
            }
        }
        material.extension.diffuse_probe = SourceGraphDiffuseProbeUniforms::from_preconvolved(
            coefficients,
            matrix("source_object_from_target_object")?,
        )?;

        let brdf_sha = forge_core::bytes_sha256(&brdf_bytes);
        if serde_json::to_value(brdf_sha)? != input["brdf_sha256"] {
            return Err("Source BRDF SHA differs".into());
        }
        let binding = SourceGraphPreintegratedBrdf::from_rgba32f_le(&brdf_bytes, brdf_sha, 64, 64)?
            .install_unqualified(&mut images);
        material.extension.set_preintegrated_brdf(&binding);

        let sky_sha = forge_core::bytes_sha256(&sky_bytes);
        if serde_json::to_value(sky_sha)? != input["sky_sha256"] {
            return Err("Measured sky SHA differs".into());
        }
        let sky_binding =
            SourceGraphSkyCube::from_mip_major_rgba16f_le(&sky_bytes, sky_sha, 256, 1, 9)?
                .install_unqualified(&mut images);
        sky_binding.validate_residency(&images)?;
        material.extension.set_sky_cube(&sky_binding);

        // The product GLB loader installs the declared decoder.
        assert!(material.extension.program_key.requires_native_srgb_encoder);
        assert!(
            material
                .extension
                .validate_scene_dependencies(&images)
                .is_err()
        );
        let mut ambient_encoded = encase::UniformBuffer::new(Vec::<u8>::new());
        ambient_encoded.write(&material.extension.diffuse_probe)?;
        capture(
            &mut captures,
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

        let mut encoded = encase::UniformBuffer::new(Vec::<u8>::new());
        encoded.write(&ext.source)?;
        let uniform_bytes = encoded.into_inner();
        assert_eq!(uniform_bytes.len(), 336);
        capture(
            &mut captures,
            root.join("SOURCE-UNIFORMS.bin"),
            &uniform_bytes,
        )?;
        world.insert_resource(shaders);
        world.insert_resource(images);
        world.insert_resource(registry);
        let calibration_material = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(material.clone());
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
            let authored_images_resident = handles.iter().all(|h| gpu.get(h).is_some());
            // WASM calibration progresses through asynchronous GPU readbacks.
            // Texture residency alone does not establish the device's sRGB curve.
            let calibration_ready = app
                .world()
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&calibration_material)
                .is_some_and(|candidate| {
                    candidate
                        .extension
                        .validate_scene_dependencies(app.world().resource::<Assets<Image>>())
                        .is_ok()
                        && forge_engine::source_graph::SourceGraphKey::from(&candidate.extension)
                            .native_srgb_encoder_ready
                });
            if authored_images_resident && calibration_ready {
                break;
            }
            if Instant::now() > deadline {
                return Err(
                    "Authored GPU image residency or native sRGB calibration timed out".into(),
                );
            }
            next_frame().await?;
        }
        material = app
            .world()
            .resource::<Assets<SourceGraphMaterial>>()
            .get(&calibration_material)
            .ok_or("Calibration material absent")?
            .clone();
        material
            .extension
            .validate_scene_dependencies(app.world().resource::<Assets<Image>>())?;
        assert!(
            forge_engine::source_graph::SourceGraphKey::from(&material.extension)
                .native_srgb_encoder_ready
        );
        let descriptor = SourceGraphMaterial::bind_group_layout_descriptor(&device);
        let binding_deadline = Instant::now() + Duration::from_secs(30);
        let prepared = loop {
            let attempt = {
                let render_world = app.get_sub_app_mut(RenderApp).unwrap().world_mut();
                let mut state =
                    SystemState::<<SourceGraphMaterial as AsBindGroup>::Param>::new(render_world);
                render_world.resource_scope(|world, cache: Mut<PipelineCache>| {
                    let mut params = state.get_mut(world).expect("Material parameters required");
                    let layout = cache.get_bind_group_layout(&descriptor);
                    material.unprepared_bind_group(&layout, &device, &mut params, true)
                })
            };
            match attempt {
                Ok(prepared) => break prepared,
                Err(AsBindGroupError::RetryNextUpdate) => {
                    if Instant::now() > binding_deadline {
                        return Err(
                            "Required material GPU resources did not become resident".into()
                        );
                    }
                    app.update();
                    next_frame().await?;
                }
                Err(error) => return Err(error.into()),
            }
        };
        let render_world = app.get_sub_app_mut(RenderApp).unwrap().world_mut();
        let gpu = render_world.resource::<RenderAssets<GpuImage>>();
        for (slot, handle) in handles.iter().enumerate() {
            let image = gpu.get(handle).unwrap();
            let binding = if slot < 9 { 101 + slot as u32 * 2 } else { 125 };
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
                OwnedBindingResource::TextureView(_, v) => {
                    assert_eq!(v.id(), image.texture_view.id())
                }
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
        let threshold_view = match &prepared
            .bindings
            .iter()
            .find(|(n, _)| *n == 128)
            .ok_or("Actual native sRGB binding absent")?
            .1
        {
            OwnedBindingResource::TextureView(_, view) => view,
            _ => return Err("Native sRGB binding is not a texture".into()),
        };
        let (threshold_id, threshold_image) = gpu
            .iter()
            .find(|(_, image)| image.texture_view.id() == threshold_view.id())
            .ok_or("Native sRGB binding is not the resident GPU image")?;
        assert_eq!(
            threshold_image.texture_descriptor.format,
            TextureFormat::R32Float
        );
        assert_eq!(threshold_image.texture_descriptor.size.width, 256);
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
        let captured = errors.lock().unwrap().clone();
        assert!(captured.is_empty(), "GPU validation errors: {captured:?}");
        let info = render_world.resource::<RenderAdapterInfo>();
        let adapter_name = info.name.clone();
        let backend = format!("{:?}", info.backend);
        let native_binding_result = serde_json::json!({"adapter":adapter_name,"backend":backend,"textures":rows,"meshes":mesh_rows,
        "native_srgb_required_and_ready":true,"native_srgb_actual_binding_identity_verified":true,"native_srgb_format":"R32Float","native_srgb_calibration_inputs_from_files":0,"source_uniform_bytes":uniform_bytes.len(),"binding_ids":binding_ids,"gpu_validation_errors":captured});
        capture(
            &mut captures,
            root.join("RESOURCE-RESULT.v1.json"),
            serde_json::to_vec_pretty(&native_binding_result)?,
        )?;
        let threshold_cpu = app
            .world()
            .resource::<Assets<Image>>()
            .get(threshold_id)
            .ok_or("Native sRGB CPU image absent")?;
        capture(
            &mut captures,
            root.join("MATERIAL-CURVE.r32f"),
            threshold_cpu
                .data
                .as_ref()
                .ok_or("Native sRGB table data absent")?,
        )?;
        let world = app.world_mut();
        world.insert_resource(GlobalAmbientLight::NONE);
        let material_handle = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(material);
        world.spawn((
            Mesh3d(mesh_handles[0].clone()),
            MeshMaterial3d(material_handle.clone()),
            Transform::IDENTITY,
        ));
        let mut target = Image::new_target_texture(512, 512, TextureFormat::Rg11b10Ufloat, None);
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
                fov: (context["fov"].as_f64().unwrap() as f32).to_radians(),
                aspect_ratio: 1.,
                near: context["near"].as_f64().unwrap() as f32,
                far: context["far"].as_f64().unwrap() as f32,
                ..default()
            }),
            bevy::camera::Exposure {
                ev100: context["exposure_ev"].as_f64().unwrap() as f32,
            },
            bevy::core_pipeline::tonemapping::Tonemapping::None,
            Msaa::Off,
            bevy::core_pipeline::prepass::DepthPrepass,
            camera_transform,
        ));
        world.spawn((
            DirectionalLight {
                illuminance: context["light_intensity"].as_f64().unwrap() as f32,
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
            let cache = app
                .get_sub_app(RenderApp)
                .unwrap()
                .world()
                .resource::<PipelineCache>();
            let pending = cache.waiting_pipelines().count();
            if frames > 30 && pending == 0 {
                if let Some(bytes) = pixels.lock().unwrap().take() {
                    assert_eq!(bytes.len(), 512 * 512 * 4);
                    let varied = bytes.chunks_exact(4).filter(|p| *p != &bytes[..4]).count();
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
            }
            if Instant::now() > deadline {
                return Err("No stable nonblank source-material camera draw in 90 seconds".into());
            }
            next_frame().await?;
        };
        assert!(
            errors.lock().unwrap().is_empty(),
            "GPU validation errors after drawing"
        );
        // Exercise the real Last-schedule lifecycle without submitting a fake frame.
        app.world_mut()
            .resource_mut::<Assets<Image>>()
            .remove(threshold_id);
        app.world_mut().run_schedule(Last);
        {
            let world = app.world();
            let m = world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material_handle)
                .unwrap();
            assert!(
                !forge_engine::source_graph::SourceGraphKey::from(&m.extension)
                    .native_srgb_encoder_ready
            );
            assert!(
                m.extension
                    .validate_scene_dependencies(world.resource::<Assets<Image>>())
                    .is_err()
            );
        }
        let held_device = app
            .world_mut()
            .remove_resource::<RenderDevice>()
            .ok_or("Main-world GPU device absent")?;
        app.world_mut().run_schedule(Last);
        app.world_mut().insert_resource(held_device);
        let recovery_deadline = Instant::now() + Duration::from_secs(30);
        loop {
            app.world_mut().run_schedule(Last);
            let current = app
                .world()
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material_handle)
                .ok_or("Recovery material absent")?;
            if forge_engine::source_graph::SourceGraphKey::from(&current.extension)
                .native_srgb_encoder_ready
            {
                break;
            }
            if Instant::now() > recovery_deadline {
                return Err("Asynchronous runtime recovery timed out".into());
            }
            next_frame().await?;
        }
        {
            let world = app.world();
            let m = world
                .resource::<Assets<SourceGraphMaterial>>()
                .get(&material_handle)
                .unwrap();
            assert!(
                forge_engine::source_graph::SourceGraphKey::from(&m.extension)
                    .native_srgb_encoder_ready
            );
            m.extension
                .validate_scene_dependencies(world.resource::<Assets<Image>>())?;
            let images: Vec<_> = world
                .resource::<Assets<Image>>()
                .iter()
                .filter(|(_, i)| {
                    i.texture_descriptor.format == TextureFormat::R32Float
                        && i.texture_descriptor.size.width == 256
                        && i.texture_descriptor.size.height == 1
                })
                .collect();
            assert_eq!(images.len(), 1);
            capture(
                &mut captures,
                root.join("RECOVERED-MATERIAL-CURVE.r32f"),
                images[0]
                    .1
                    .data
                    .as_ref()
                    .ok_or("Recovered native sRGB curve has no bytes")?,
            )?;
        }
        // Require fresh stable GPU frames after the actual runtime recovery.
        pixels.lock().unwrap().take();
        let recovery_draw_deadline = Instant::now() + Duration::from_secs(90);
        let mut recovery_frames = 0u32;
        let mut recovery_stable = 0u32;
        let mut recovery_previous: Option<Vec<u8>> = None;
        loop {
            app.update();
            recovery_frames += 1;
            let pending = app
                .get_sub_app(RenderApp)
                .unwrap()
                .world()
                .resource::<PipelineCache>()
                .waiting_pipelines()
                .count();
            if recovery_frames > 30 && pending == 0 {
                if let Some(bytes) = pixels.lock().unwrap().take() {
                    if bytes.len() != 512 * 512 * 4 {
                        return Err("Recovery GPU image size differs".into());
                    }
                    if recovery_previous.as_ref() == Some(&bytes) {
                        recovery_stable += 1;
                    } else {
                        recovery_stable = 0;
                    }
                    recovery_previous = Some(bytes.clone());
                    if recovery_stable >= 2 {
                        if bytes != captured_image {
                            return Err(
                                "Recovered draw differs from the same-device baseline".into()
                            );
                        }
                        break;
                    }
                }
            }
            if Instant::now() > recovery_draw_deadline {
                return Err("Recovered material did not produce stable GPU frames".into());
            }
            next_frame().await?;
        }
        if !errors.lock().unwrap().is_empty() {
            return Err("GPU errors after recovery draw".into());
        }
        capture(
            &mut captures,
            root.join("FORGE-PACKED.rg11b10"),
            &captured_image,
        )?;
        let result = serde_json::json!({"status":"actual_source_glb_lod0_camera_draw_and_stable_gpu_readback",
        "adapter":adapter_name,"backend":backend,"lod":0,"frames":frames,"consecutive_equal_readbacks":stable+1,
        "native_images":10,"native_mips":120,"authored_texture_substitution":false,
        "scene_sampling_policy":format!("{policy:?}"),"source_context":input["source_context"],
        "camera_position":position.to_array(),"camera_target":centre.to_array(),"camera_ev100":context["exposure_ev"],
        "source_state":"Actual common material state; depth equal with original custom depth prepass",
        "measured_sky_reflection_applied":true,"source_camera_lit_shader_mode":context["actual_lit_shader_mode"],"source_sky_sha256":input["sky_sha256"],"source_ssao_qualified":false,"measured_gpu_ambient_applied":true,"source_gpu_ambient_coefficients":context["actual_gpu_ambient_coefficients"],"source_brdf_sha256":input["brdf_sha256"],"native_srgb_missing_image_refused":true,"native_srgb_main_device_absence_and_recovery_verified":true,"gpu_frame_after_recovery_verified":true,"gpu_draw_executed":true,"image":"FORGE-PACKED.rg11b10","image_bytes":captured_image.len(),
        "target_format":"Rg11b10Ufloat","raw_readback_preserved":true,"cpu_color_adjustment":false,"hdr_intermediate_format":"Bevy Hdr internal Rgba16Float; final target Rg11b10Ufloat","tonemapping":"None","source_reference_tonemapping":"None",
        "source_ambient_light_qualified":false,"source_sampling_parity_qualified":false,"source_lighting_parity_qualified":false,
        "source_render_parity_qualified":false,"forge_editor_verified":false,"full_import":false});
        capture(
            &mut captures,
            root.join("NATIVE-RESULT.v1.json"),
            serde_json::to_vec_pretty(&result)?,
        )?;
        let bindings: serde_json::Value =
            serde_json::from_slice(&captures["RESOURCE-RESULT.v1.json"])?;
        let draw: serde_json::Value = serde_json::from_slice(&captures["NATIVE-RESULT.v1.json"])?;
        Ok(serde_json::json!({"status":"actual_common_browser_glb_draw_binding_and_recovery_verified",
        "browser_gpu_executed":true,"resource_receipt":bindings,"draw_receipt":draw,
        "packed_image":captures["FORGE-PACKED.rg11b10"],
        "material_curve":captures["MATERIAL-CURVE.r32f"],
        "recovered_material_curve":captures["RECOVERED-MATERIAL-CURVE.r32f"],
        "recovery_frames":recovery_frames,"recovery_consecutive_equal_readbacks":recovery_stable+1,
        "recovery_pixels_byte_equal":true,"gpu_errors":errors.lock().unwrap().clone(),
        "browser_material_draw_verified":true,"editor_visual_verified":false,"full_import":false}).to_string())
    }

    use bevy::platform::time::Instant;
    use wasm_bindgen::{JsCast, prelude::*};

    fn capture(
        path_map: &mut std::collections::BTreeMap<String, Vec<u8>>,
        path: impl AsRef<Path>,
        bytes: impl AsRef<[u8]>,
    ) -> Result<(), std::io::Error> {
        let key = path
            .as_ref()
            .file_name()
            .unwrap()
            .to_string_lossy()
            .into_owned();
        path_map.insert(key, bytes.as_ref().to_vec());
        Ok(())
    }
    async fn next_frame() -> Result<(), Box<dyn std::error::Error>> {
        let promise = js_sys::Promise::new(&mut |resolve, reject| {
            let callback = Closure::once_into_js(move |_time: f64| {
                let _ = resolve.call0(&JsValue::NULL);
            });
            if let Some(window) = web_sys::window() {
                if let Err(error) = window.request_animation_frame(callback.unchecked_ref()) {
                    let _ = reject.call1(&JsValue::NULL, &error);
                }
            } else {
                let _ = reject.call1(&JsValue::NULL, &JsValue::from_str("Browser window absent"));
            }
        });
        wasm_bindgen_futures::JsFuture::from(promise)
            .await
            .map_err(|error| format!("Browser frame callback failed: {error:?}"))?;
        Ok(())
    }
    #[wasm_bindgen]
    pub async fn run_glb_probe(
        input: String,
        raw: Vec<u8>,
        brdf: Vec<u8>,
        sky: Vec<u8>,
    ) -> Result<String, JsValue> {
        console_error_panic_hook::set_once();
        run_inner(input, raw, brdf, sky)
            .await
            .map_err(|error| JsValue::from_str(&error.to_string()))
    }
}
