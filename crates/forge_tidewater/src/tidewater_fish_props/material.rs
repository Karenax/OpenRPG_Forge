#[cfg(test)]
mod tests {
    use bevy::{prelude::*, render::storage::ShaderBuffer};
    use forge_engine::{
        source_fish::{
            material::{SHADER, prepare, prepare_scene, shader_source, skin_buffer, stage_shader},
            surface,
        },
        source_lighting::SceneLighting,
        source_shadows::ObjectShadowSettings,
        source_surface::LightingBindings,
    };
    use forge_tidewater_assets::tidewater_fish_props::FishPropPackage;
    use std::io::Write;

    struct OtherModuleProfile<'a>(&'a FishPropPackage);

    impl forge_assets::source_fish_payload::SourceFishPayload for OtherModuleProfile<'_> {
        fn file(&self, name: &str) -> Option<&[u8]> {
            self.0.file(name)
        }
    }

    impl forge_assets::source_fish_payload::SourceFishProfile for OtherModuleProfile<'_> {
        fn description(&self) -> &[u8] {
            forge_assets::source_fish_payload::SourceFishProfile::description(self.0)
        }
        fn inventory(&self) -> (usize, usize) {
            forge_assets::source_fish_payload::SourceFishProfile::inventory(self.0)
        }
        fn identify(
            &self,
            ordinal: usize,
            source_id: &str,
        ) -> Result<Option<forge_core::AssetId>, String> {
            if source_id.is_empty() {
                return Err("Empty source identity".into());
            }
            Ok(Some(forge_core::AssetId::deterministic(format!(
                "other-module/fish/{ordinal}"
            ))))
        }
    }

    fn package() -> FishPropPackage {
        let root =
            std::path::PathBuf::from(std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").unwrap());
        FishPropPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            forge_tidewater_assets::tidewater_fish_props::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap()
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn skin_storage_matches_all_acquired_rows_exactly() {
        let package = package();
        let buffer = skin_buffer(&package).unwrap();
        assert_eq!(
            buffer.data.as_deref(),
            package.file("material/skin-table-f32.bin")
        );
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn every_source_instance_keeps_surface_parameters_and_scene_bindings() {
        let package = package();
        let prepared = super::super::PreparedFishProps::from_package(&package).unwrap();
        let mut images = Assets::<Image>::default();
        let lighting = LightingBindings {
            scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [3.0; 3], [[0.4; 3]; 9]).unwrap(),
            environment: Handle::default(),
            hill_shadow: Handle::default(),
            object_shadow: Handle::default(),
            object_settings: ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            wind: Vec4::ZERO,
            flags: Vec4::ZERO,
            caustics: forge_engine::source_surface::underwater::CausticMaterialInputs::disabled(
                &mut images,
            ),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        };
        for template in prepared.templates {
            let mut material =
                prepare(template.surface, Handle::default(), &lighting, 0.625).unwrap();
            assert_eq!(
                material.settings.flags.to_array().map(f32::to_bits),
                template.surface.flags.map(f32::to_bits)
            );
            assert_eq!(
                material.settings.info.to_array().map(f32::to_bits),
                [
                    template.surface.pattern_and_seed.floor(),
                    template.surface.pattern_and_seed.fract(),
                    template.surface.source_length_m,
                    0.625,
                ]
                .map(f32::to_bits)
            );
            assert_eq!(
                material.underwater_level,
                lighting.caustics.light_maps.level
            );
            assert_eq!(
                material.underwater_map_frame,
                lighting.caustics.light_maps.frame
            );
            assert_eq!(material.environment, lighting.environment);
            let mut scene = crate::tidewater_assets::materials::prepare(
                &forge_engine::source_surface::package::MaterialParameters {
                    kind: 0,
                    textures: vec!["fixture".into()],
                    color: [1.0; 4],
                    emissive: [0.0; 4],
                    surface: [0.0; 4],
                },
                &std::collections::BTreeMap::from([("fixture".into(), Handle::default())]),
                &lighting,
            )
            .unwrap();
            scene.shared_scene_lighting = true;
            scene.scene_lighting =
                SceneLighting::new([1.0, 0.0, 0.0], [0.125; 3], [[0.05; 3]; 9]).unwrap();
            scene.environment = images.add(Image::default());
            scene.object_settings.frame_index = 91;
            material.settings.lod = Vec4::new(0.35, 1.0, 0.0, 0.0);
            let info = material.settings.info;
            let flags = material.settings.flags;
            let lod = material.settings.lod;
            let skin = material.skin.clone();
            material.update_scene_receivers(&scene).unwrap();
            let direct = prepare_scene(template.surface, skin.clone(), &scene, 0.625).unwrap();
            assert!(direct.shared_scene_lighting);
            assert_eq!(direct.environment, scene.environment);
            assert_eq!(direct.settings.info, info);
            assert_eq!(direct.settings.flags, flags);
            assert!(material.shared_scene_lighting);
            assert_eq!(material.environment, scene.environment);
            assert_eq!(
                material.scene_lighting.sun_color,
                scene.scene_lighting.sun_color
            );
            assert_eq!(material.object_settings.frame_index, 91);
            assert_eq!(material.settings.info, info);
            assert_eq!(material.settings.flags, flags);
            assert_eq!(material.settings.lod, lod);
            assert_eq!(material.skin, skin);
            scene.environment = images.add(Image::default());
            scene.scene_lighting.sun_color.x = f32::NAN;
            let previous_environment = material.environment.clone();
            let previous_light = material.scene_lighting.sun_color;
            assert!(material.update_scene_receivers(&scene).is_err());
            assert!(prepare_scene(template.surface, skin.clone(), &scene, 0.625).is_err());
            assert_eq!(material.environment, previous_environment);
            assert_eq!(material.scene_lighting.sun_color, previous_light);
            assert!(prepare(template.surface, Handle::default(), &lighting, f32::NAN).is_err());
        }
    }

    fn assert_temporal_shader_contract(shader: &str) {
        let auxiliary = forge_engine::source_temporal::shaders::derive(
            forge_engine::source_temporal::shaders::Family::Fish,
            shader,
            shader,
        )
        .expect("The installed fish shader must preserve its clock and LOD in the common auxiliary pass");
        let fragment = auxiliary
            .fragment
            .rsplit("@fragment fn fragment(")
            .next()
            .unwrap();
        assert!(
            fragment.contains(
                "frame.frameIndex=select(settings.frameIndex,fishClock.y,fishClock.z!=0u);"
            )
        );
        assert!(fragment.contains(
            "frame.time=select(settings.info.w,bitcast<f32>(fishClock.x),fishClock.z!=0u);"
        ));
        assert!(fragment.contains("let surface=fishSurface(input);"));
        assert_eq!(fragment.matches("discard").count(), 1);
    }

    fn assert_common_preparation(package: &FishPropPackage, lighting: &str) {
        let hash = forge_tidewater_assets::tidewater_fish_props::package_hash();
        let expected_shader =
            shader_source(package, lighting, surface::SpecularMode::ThreeReference).unwrap();
        let expected_shadow = forge_engine::source_fish::shadow::shader_source(package).unwrap();
        let expected_skin = skin_buffer(package).unwrap().data;
        let expected_ids: Vec<_> = forge_engine::source_fish::prepare::profile(package)
            .unwrap()
            .into_iter()
            .map(|template| template.asset_id)
            .collect();
        assert_selected_preparation(package, lighting, &expected_ids);
        let missing_assets = forge_engine::source_fish::host::PreparedProfile::new(
            package,
            hash,
            lighting,
            surface::SpecularMode::ThreeReference,
        )
        .unwrap();
        let mut empty = World::new();
        assert!(missing_assets.stage(&mut empty).is_err());
        assert!(!empty.contains_resource::<Assets<Shader>>());
        assert!(!empty.contains_resource::<Assets<ShaderBuffer>>());
        let prepared = forge_engine::source_fish::host::PreparedProfile::new(
            package,
            hash,
            lighting,
            surface::SpecularMode::ThreeReference,
        )
        .unwrap();
        assert_eq!(prepared.package_sha256, hash);
        assert_eq!(
            prepared
                .templates
                .iter()
                .map(|template| template.asset_id)
                .collect::<Vec<_>>(),
            expected_ids
        );
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let staged = prepared.stage(&mut world).unwrap();
        assert_eq!(
            world
                .resource::<Assets<Shader>>()
                .get(&SHADER)
                .unwrap()
                .source
                .as_str(),
            expected_shader
        );
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&staged.skin)
                .unwrap()
                .data,
            expected_skin
        );
        assert_eq!(*staged.shadow_shader, expected_shadow);
        assert_eq!(staged.package_sha256, hash);
        assert_independent_preparation(
            package,
            lighting,
            &expected_shader,
            &expected_shadow,
            expected_skin.as_deref(),
        );
    }

    fn assert_selected_preparation(
        package: &FishPropPackage,
        lighting: &str,
        ids: &[forge_core::AssetId],
    ) {
        assert_eq!(
            forge_engine::source_fish::host::scene::profile_policy(package).unwrap(),
            super::super::lod::POLICY
        );
        let hash = forge_tidewater_assets::tidewater_fish_props::package_hash();
        let selected = std::collections::BTreeSet::from([ids[0], ids[ids.len() - 1]]);
        let prepared = forge_engine::source_fish::host::PreparedProfile::new_selected(
            package,
            hash,
            lighting,
            surface::SpecularMode::ThreeReference,
            &selected,
        )
        .unwrap();
        assert_eq!(
            prepared
                .templates
                .iter()
                .map(|item| item.asset_id)
                .collect::<std::collections::BTreeSet<_>>(),
            selected
        );
        let foreign =
            std::collections::BTreeSet::from([forge_core::AssetId::deterministic("unknown-fish")]);
        assert!(
            forge_engine::source_fish::host::PreparedProfile::new_selected(
                package,
                hash,
                lighting,
                surface::SpecularMode::ThreeReference,
                &foreign,
            )
            .is_err()
        );
    }

    fn assert_independent_preparation(
        package: &FishPropPackage,
        lighting: &str,
        expected_shader: &str,
        expected_shadow: &str,
        expected_skin: Option<&[u8]>,
    ) {
        let profile = OtherModuleProfile(package);
        let hash = forge_tidewater_assets::tidewater_fish_props::package_hash();
        let prepared = forge_engine::source_fish::host::PreparedProfile::new(
            &profile,
            hash,
            lighting,
            surface::SpecularMode::ThreeReference,
        )
        .unwrap();
        assert_eq!(prepared.templates.len(), 78);
        for (ordinal, template) in prepared.templates.iter().enumerate() {
            assert_eq!(
                template.asset_id,
                forge_core::AssetId::deterministic(format!("other-module/fish/{ordinal}"))
            );
        }
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let staged = prepared.stage(&mut world).unwrap();
        assert_eq!(
            world
                .resource::<Assets<Shader>>()
                .get(&SHADER)
                .unwrap()
                .source
                .as_str(),
            expected_shader
        );
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&staged.skin)
                .unwrap()
                .data
                .as_deref(),
            expected_skin
        );
        assert_eq!(staged.shadow_shader.as_str(), expected_shadow);
    }

    #[test]
    #[ignore = "Requires byte-pinned private fish package or acquired reference; run the documented external-fixture replay"]
    fn full_material_assembly_retains_surface_and_common_receivers() {
        let package = package();
        let lighting = std::fs::read_to_string(
            std::env::var("FORGE_TIDEWATER_SCENE_LIGHTING_SOURCE").unwrap(),
        )
        .unwrap();
        assert_common_preparation(&package, &lighting);
        let shader =
            shader_source(&package, &lighting, surface::SpecularMode::ThreeReference).unwrap();
        let files = [
            "material/module-0-common.wgsl",
            "material/module-2-fish.wgsl",
            "material/main-surface.wgsl",
            "material/module-1-lodFade.wgsl",
            "material/main-shadow.wgsl",
            "material/skin-table-f32.bin",
        ];
        let mut independent = files
            .into_iter()
            .map(|name| (name.to_owned(), package.file(name).unwrap().to_vec()))
            .collect::<std::collections::BTreeMap<_, _>>();
        assert_eq!(
            shader_source(
                &independent,
                &lighting,
                surface::SpecularMode::ThreeReference
            )
            .unwrap(),
            shader
        );
        assert_eq!(
            skin_buffer(&independent).unwrap().data,
            skin_buffer(&package).unwrap().data
        );
        assert_eq!(
            forge_engine::source_fish::shadow::shader_source(&independent).unwrap(),
            forge_engine::source_fish::shadow::shader_source(&package).unwrap()
        );
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        stage_shader(
            &mut world,
            &independent,
            &lighting,
            surface::SpecularMode::ThreeReference,
        )
        .unwrap();
        let installed = world.resource::<Assets<Shader>>().get(&SHADER).unwrap();
        assert_eq!(installed.source.as_str(), shader);
        assert_eq!(
            installed.imports,
            Shader::from_wgsl(shader.clone(), "tidewater-fish.wgsl").imports
        );
        let shaders = world.resource::<Assets<Shader>>().len();
        let buffers = world.resource::<Assets<ShaderBuffer>>().len();
        independent.remove("material/module-0-common.wgsl");
        assert!(shader_source(&independent, &lighting, surface::SpecularMode::Intended).is_err());
        assert!(
            stage_shader(
                &mut world,
                &independent,
                &lighting,
                surface::SpecularMode::ThreeReference,
            )
            .is_err()
        );
        assert_eq!(world.resource::<Assets<Shader>>().len(), shaders);
        assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), buffers);
        let body =
            std::str::from_utf8(package.file("material/main-surface.wgsl").unwrap()).unwrap();
        assert_eq!(shader.matches(body).count(), 1);
        for marker in [
            "fn fishSurface(",
            "fn fishSceneLight(",
            "fn sourceGroundBounce(",
            "fn villageLocalLights(",
            "fn currentShadowParams(",
            "FORGE_SHARED_SCENE_LIGHTING",
            "@binding(19)",
            "s.specularIntensity = 0.0;",
        ] {
            assert!(shader.contains(marker), "Missing receiver: {marker}");
        }
        assert_temporal_shader_contract(&shader);
        assert!(shader_source(&package, "", surface::SpecularMode::Intended).is_err());
        if let Ok(output) = std::env::var("FORGE_FISH_MATERIAL_SHADER_OUTPUT") {
            let mut file = std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(output)
                .unwrap();
            file.write_all(shader.as_bytes()).unwrap();
        }
    }
}
