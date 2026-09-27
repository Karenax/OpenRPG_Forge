use super::*;
use crate::water::{
    VisualValleyWaterMaterial, VisualValleyWaterPlugin, VisualValleyWaterSurface,
    WaterProjectionStats, WaterRuntimeState,
};

fn observed_intensity(app: &App) -> Option<f32> {
    app.world()
        .resource::<WaterProjectionStats>()
        .quality
        .as_ref()
        .unwrap()
        .environment_light_intensity
}

#[test]
fn environment_image_evidence_tracks_missing_images_and_camera_ambiguity() {
    let (mut app, camera) = app();
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    let observed = |app: &App| {
        app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .environment_textures_available
    };
    assert!(!observed(&app));
    let mut image = Image::default();
    image.texture_descriptor.size = bevy::render::render_resource::Extent3d {
        width: 64,
        height: 64,
        depth_or_array_layers: 6,
    };
    let diffuse = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(image.clone());
    let specular = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(image.clone());
    app.world_mut()
        .entity_mut(camera)
        .insert(bevy::light::EnvironmentMapLight {
            diffuse_map: diffuse.clone(),
            specular_map: specular.clone(),
            ..default()
        });
    app.update();
    assert!(observed(&app));
    assert_eq!(
        observed_intensity(&app),
        Some(bevy::light::EnvironmentMapLight::default().intensity)
    );
    assert!(
        !app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .full_preset_applied
    );
    let auxiliary = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera {
                is_active: false,
                ..default()
            },
        ))
        .id();
    app.update();
    assert!(observed(&app));
    app.world_mut()
        .get_mut::<Camera>(auxiliary)
        .unwrap()
        .is_active = true;
    app.update();
    assert!(!observed(&app));
    app.world_mut().despawn(auxiliary);
    assert_eq!(observed_intensity(&app), None);
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .get_mut(&specular)
        .unwrap()
        .texture_descriptor
        .size
        .depth_or_array_layers = 1;
    app.update();
    assert!(!observed(&app));
    *app.world_mut()
        .resource_mut::<Assets<Image>>()
        .get_mut(&specular)
        .unwrap() = image;
    app.update();
    assert!(observed(&app));
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .remove(&diffuse);
    app.update();
    assert!(!observed(&app));
}

fn policy() -> VisualValleyQualityProfileDocument {
    VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap()
}

#[test]
fn effective_water_installation_preserves_sources_and_manual_previews() {
    let (mut app, camera) = app();
    app.update();
    let original = app
        .world()
        .resource::<WaterProjectionStats>()
        .source_hash
        .clone();
    let guard = crate::global_quality::test_plan::guard(&policy(), VisualQualityTier::High);
    let sources = guard.plan().source_bindings.unwrap();
    assert!(
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request_effective(&guard, std::time::Duration::ZERO, guard.identity(), sources)
    );
    app.update();
    let observation = app
        .world()
        .resource::<WaterProjectionStats>()
        .quality
        .as_ref()
        .unwrap();
    assert_eq!(
        observation.requested.refraction,
        QualityWaterRefraction::ScreenSpace
    );
    assert_eq!(
        observation.renderer_requested.refraction,
        QualityWaterRefraction::SurfaceTint
    );
    assert_eq!(
        observation.renderer_requested.reflection_max_distance_m,
        None
    );
    assert_eq!(observation.effective_request, Some(guard.identity()));
    assert!(observation.surface_tint_projected && observation.ssr_budget_projected);
    assert!(!observation.full_preset_applied);
    assert_eq!(
        app.world()
            .get::<bevy::pbr::ScreenSpaceReflections>(camera)
            .unwrap()
            .linear_steps,
        48
    );
    for disabled in [true, false] {
        {
            let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
            request.preview_surface_tint(if disabled { Some(false) } else { None });
            request.preview_reflections_disabled(disabled);
        }
        app.update();
        let observed = app
            .world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap();
        assert!(observed.surface_tint_projected && observed.material_reflectance_projected);
        assert_eq!(observed.surface_tint_strength == Some(0.0), disabled);
        assert_eq!(observed.material_reflectance == Some(0.0), disabled);
        assert_eq!(
            app.world()
                .get::<bevy::pbr::ScreenSpaceReflections>(camera)
                .is_none(),
            disabled
        );
    }
    let mut stale = sources;
    stale.water = "ab".repeat(32).parse().unwrap();
    assert!(
        !app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request_effective(&guard, std::time::Duration::ZERO, guard.identity(), stale)
    );
    assert!(
        app.world()
            .resource::<WaterQualityRequest>()
            .effective
            .is_none()
    );
    assert!(
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request_effective(&guard, std::time::Duration::ZERO, guard.identity(), sources)
    );
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    assert_eq!(stats.source_hash, original);
    assert!(stats.quality.as_ref().unwrap().effective_request.is_none());
    assert_eq!(
        app.world().resource::<WaterQualityRequest>().document,
        policy()
    );
    assert!(
        !app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request_effective(&guard, std::time::Duration::ZERO, guard.identity(), sources)
    );
}

fn app() -> (App, Entity) {
    app_with_forward_reflection(false)
}

fn app_with_forward_reflection(forward: bool) -> (App, Entity) {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, AssetPlugin::default()));
    app.init_asset::<Mesh>();
    app.init_asset::<StandardMaterial>();
    app.init_asset::<Image>();
    app.insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
        std::time::Duration::ZERO,
    ));
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let water = forge_project::WaterDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/water.v1.json"
    ))
    .unwrap();
    app.insert_resource(
        WaterQualityRequest::new(&terrain, policy(), VisualQualityTier::High).unwrap(),
    );
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
    if forward {
        app.insert_resource(crate::WaterDepthOpticsPreview);
        app.insert_resource(crate::WaterReflectionOpticsPreview);
    }
    app.add_plugins(VisualValleyWaterPlugin::new(terrain, water));
    (app, camera)
}

#[test]
fn forward_reflection_quality_cycles_without_rebuilding_or_camera_ssr() {
    let (mut app, camera) = app_with_forward_reflection(true);
    app.update();
    let initial = app.world().resource::<WaterProjectionStats>().clone();
    let material = app.world().resource::<WaterSurfaceMaterial>().0.clone();
    for (tier, steps, distance, refracted_steps, refracted_path) in [
        (VisualQualityTier::Low, 0.0, 0.0, 0.0, 0.0),
        (VisualQualityTier::Medium, 24.0, 64.0, 0.0, 0.0),
        (VisualQualityTier::Ultra, 64.0, 128.0, 40.0, 4.0),
        (VisualQualityTier::High, 48.0, 96.0, 24.0, 3.2),
    ] {
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request(tier);
        app.update();
        let stats = app.world().resource::<WaterProjectionStats>();
        let observed = stats.forward_reflection_quality.as_ref().unwrap();
        assert_eq!(observed.linear_steps, Some(steps));
        assert_eq!(observed.max_distance_m, Some(distance));
        assert_eq!(observed.refraction_linear_steps, Some(refracted_steps));
        assert_eq!(observed.refraction_max_path_m, Some(refracted_path));
        assert!(observed.projection_matches && stats.shader_uniforms_match_profile);
        assert!(observed.budgets_match());
        if tier != VisualQualityTier::Low {
            let mut stale = observed.clone();
            stale.max_distance_m = Some(distance + 1.0);
            assert!(!stale.budgets_match());
            stale = observed.clone();
            stale.requested.as_mut().unwrap().reflection_max_distance_m = None;
            assert!(!stale.budgets_match());
            stale = observed.clone();
            stale.requested.as_mut().unwrap().reflection_max_distance_m = Some(f32::INFINITY);
            stale.max_distance_m = Some(f32::INFINITY);
            assert!(!stale.budgets_match());
        }
        let policy = observed.policy_hash.as_deref();
        assert!(observed.matches_policy(policy, tier));
        assert!(!observed.matches_policy(None, tier));
        assert!(!observed.matches_policy(Some("stale-policy"), tier));
        let other_tier = if tier == VisualQualityTier::Low {
            VisualQualityTier::High
        } else {
            VisualQualityTier::Low
        };
        assert!(!observed.matches_policy(policy, other_tier));
        assert_eq!(stats.source_hash, initial.source_hash);
        assert_eq!(stats.terrain_source_hash, initial.terrain_source_hash);
        assert_eq!(stats.shader_material_assets, initial.shader_material_assets);
        assert_eq!(app.world().resource::<WaterSurfaceMaterial>().0, material);
        assert!(app.world().get::<ScreenSpaceReflections>(camera).is_none());
        assert!(stats.quality.is_none());
    }
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .preview_reflections_disabled(true);
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    let observed = stats.forward_reflection_quality.as_ref().unwrap();
    assert_eq!(observed.linear_steps, Some(0.0));
    assert_eq!(observed.material_reflectance, Some(0.0));
    assert_eq!(observed.refraction_linear_steps, Some(24.0));
    assert!(observed.projection_matches);
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .preview_reflections_disabled(false);
    app.update();
    assert_eq!(
        app.world()
            .resource::<WaterProjectionStats>()
            .forward_reflection_quality
            .as_ref()
            .unwrap()
            .linear_steps,
        Some(48.0)
    );
    app.world_mut().get_mut::<Camera>(camera).unwrap().is_active = false;
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    let observed = stats.forward_reflection_quality.as_ref().unwrap();
    assert_eq!(observed.linear_steps, Some(0.0));
    assert_eq!(observed.refraction_linear_steps, Some(0.0));
    assert!(!observed.projection_matches);
    assert!(!observed.matches_policy(observed.policy_hash.as_deref(), VisualQualityTier::High));
}

#[test]
fn forward_refraction_respects_manual_and_effective_fallback_without_source_mutation() {
    let (mut app, _) = app_with_forward_reflection(true);
    app.update();
    let source = app
        .world()
        .resource::<WaterProjectionStats>()
        .source_hash
        .clone();
    for (preview, steps) in [(Some(false), 0.0), (Some(true), 0.0), (None, 24.0)] {
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .preview_surface_tint(preview);
        app.update();
        let stats = app.world().resource::<WaterProjectionStats>();
        let observed = stats.forward_reflection_quality.as_ref().unwrap();
        assert_eq!(observed.refraction_linear_steps, Some(steps));
        assert_eq!(observed.linear_steps, Some(48.0));
        assert!(observed.projection_matches && stats.shader_uniforms_match_profile);
        assert_eq!(stats.source_hash, source);
        assert!(stats.quality.is_none());
    }
    let guard = crate::global_quality::test_plan::guard(&policy(), VisualQualityTier::High);
    assert!(
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request_effective(
                &guard,
                std::time::Duration::ZERO,
                guard.identity(),
                guard.plan().source_bindings.unwrap(),
            )
    );
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    let observed = stats.forward_reflection_quality.as_ref().unwrap();
    assert_eq!(observed.refraction_linear_steps, Some(0.0));
    assert!(observed.projection_matches && stats.shader_uniforms_match_profile);
    // ADR fallback removes the metric ceiling. The forward trace is then disabled:
    // matching uniforms alone must not acknowledge screen reflections as applied.
    assert!(!observed.matches_application(&guard));
    assert_eq!(stats.source_hash, source);
    let guard = crate::global_quality::test_plan::guard(&policy(), VisualQualityTier::Low);
    let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
    request.request(VisualQualityTier::Low);
    assert!(request.request_effective(
        &guard,
        std::time::Duration::ZERO,
        guard.identity(),
        guard.plan().source_bindings.unwrap(),
    ));
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    let observed = stats.forward_reflection_quality.as_ref().unwrap();
    assert!(observed.matches_application(&guard));
    let stale_guard = crate::global_quality::test_plan::guard_with_sequence(
        &policy(),
        VisualQualityTier::Low,
        guard.identity().sequence + 1,
    );
    assert!(!observed.matches_application(&stale_guard));
    let mut uninstalled = observed.clone();
    uninstalled.effective_request = None;
    assert!(!uninstalled.matches_application(&guard));
    let mut mismatched = observed.clone();
    mismatched.requested.as_mut().unwrap().reflection_steps += 1;
    assert!(!mismatched.matches_application(&guard));
    assert_eq!(stats.source_hash, source);
    assert_eq!(
        app.world().resource::<WaterQualityRequest>().document,
        policy()
    );
}

#[test]
fn forward_ray_override_survives_tier_changes_without_touching_reflections_or_sources() {
    let (mut app, _) = app_with_forward_reflection(true);
    app.update();
    let initial = app.world().resource::<WaterProjectionStats>().clone();
    let material = app.world().resource::<WaterSurfaceMaterial>().0.clone();
    for (tier, disabled, expected_steps, reflection_steps) in [
        (VisualQualityTier::High, true, 0.0, 48.0),
        (VisualQualityTier::Ultra, true, 0.0, 64.0),
        (VisualQualityTier::Low, false, 0.0, 0.0),
        (VisualQualityTier::High, false, 24.0, 48.0),
    ] {
        let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
        request.request(tier);
        request.preview_screen_refraction_disabled(disabled);
        app.update();
        let stats = app.world().resource::<WaterProjectionStats>();
        let observed = stats.forward_reflection_quality.as_ref().unwrap();
        assert_eq!(observed.screen_refraction_disabled_preview, disabled);
        assert_eq!(observed.refraction_linear_steps, Some(expected_steps));
        assert_eq!(observed.linear_steps, Some(reflection_steps));
        assert!(observed.projection_matches && stats.shader_uniforms_match_profile);
        assert_eq!(stats.source_hash, initial.source_hash);
        assert_eq!(stats.terrain_source_hash, initial.terrain_source_hash);
        assert_eq!(app.world().resource::<WaterSurfaceMaterial>().0, material);
        assert!(stats.quality.is_none());
    }
    let guard = crate::global_quality::test_plan::guard(&policy(), VisualQualityTier::High);
    let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
    assert!(request.request_effective(
        &guard,
        std::time::Duration::ZERO,
        guard.identity(),
        guard.plan().source_bindings.unwrap()
    ));
    request.preview_screen_refraction_disabled(true);
    request.preview_screen_refraction_disabled(false);
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    // Returning to the profile must not bypass the coordinator's effective fallback.
    assert_eq!(
        stats
            .forward_reflection_quality
            .as_ref()
            .unwrap()
            .refraction_linear_steps,
        Some(0.0)
    );
    assert_eq!(
        app.world().resource::<WaterQualityRequest>().document,
        policy()
    );
}

#[test]
fn water_startup_leaves_inactive_auxiliary_camera_optics_untouched() {
    let (mut app, camera) = app();
    let auxiliary = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera {
                is_active: false,
                ..default()
            },
            Msaa::Sample4,
            ScreenSpaceReflections {
                linear_steps: 17,
                ..default()
            },
            bevy::pbr::ScreenSpaceTransmission {
                steps: 2,
                ..default()
            },
        ))
        .id();
    // Bevy may insert required prepass components with SSR before the water plugin runs.
    let initial_deferred = app
        .world()
        .get::<bevy::core_pipeline::prepass::DeferredPrepass>(auxiliary)
        .is_some();
    let initial_taa = app.world().get::<TemporalAntiAliasing>(auxiliary).is_some();
    app.update();
    let world = app.world();
    assert_eq!(
        world
            .get::<ScreenSpaceReflections>(camera)
            .unwrap()
            .linear_steps,
        48
    );
    assert_eq!(
        world
            .get::<ScreenSpaceReflections>(auxiliary)
            .unwrap()
            .linear_steps,
        17
    );
    assert_eq!(
        world
            .get::<bevy::pbr::ScreenSpaceTransmission>(auxiliary)
            .unwrap()
            .steps,
        2
    );
    assert_eq!(*world.get::<Msaa>(auxiliary).unwrap(), Msaa::Sample4);
    assert_eq!(
        world
            .get::<bevy::core_pipeline::prepass::DeferredPrepass>(auxiliary)
            .is_some(),
        initial_deferred
    );
    assert_eq!(
        world.get::<TemporalAntiAliasing>(auxiliary).is_some(),
        initial_taa
    );
}

#[test]
fn water_startup_does_not_choose_between_two_active_cameras() {
    let (mut app, first) = app();
    let second = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
    for camera in [first, second] {
        app.world_mut().entity_mut(camera).insert((
            Msaa::Sample4,
            ScreenSpaceReflections {
                linear_steps: 17,
                ..default()
            },
            bevy::pbr::ScreenSpaceTransmission {
                steps: 2,
                ..default()
            },
        ));
    }
    app.update();
    for camera in [first, second] {
        assert_eq!(
            app.world()
                .get::<ScreenSpaceReflections>(camera)
                .unwrap()
                .linear_steps,
            17
        );
        assert_eq!(
            app.world()
                .get::<bevy::pbr::ScreenSpaceTransmission>(camera)
                .unwrap()
                .steps,
            2
        );
        assert_eq!(*app.world().get::<Msaa>(camera).unwrap(), Msaa::Sample4);
    }
    let observation = app.world().resource::<WaterProjectionStats>();
    assert!(!observation.quality.as_ref().unwrap().ssr_budget_projected);
}

#[test]
fn water_quality_cycle_changes_only_ssr_budget_and_preserves_water_geometry_and_phase() {
    let (mut app, camera) = app();
    app.update();
    let initial_stats = app.world().resource::<WaterProjectionStats>().clone();
    let initial_phase = app
        .world()
        .resource::<WaterRuntimeState>()
        .current_phase_radians
        .to_bits();
    let mut surfaces = app.world_mut().query_filtered::<(Entity, &Mesh3d, &MeshMaterial3d<VisualValleyWaterMaterial>), With<VisualValleyWaterSurface>>();
    let (entity, mesh, material) = surfaces.single(app.world()).unwrap();
    let binding = (entity, mesh.0.id(), material.0.id());
    let positions = app
        .world()
        .resource::<Assets<Mesh>>()
        .get(binding.1)
        .unwrap()
        .attribute(Mesh::ATTRIBUTE_POSITION)
        .unwrap()
        .clone();
    let thickness = app
        .world()
        .get::<ScreenSpaceReflections>(camera)
        .unwrap()
        .thickness
        .to_bits();
    for (tier, expected_steps) in [
        (VisualQualityTier::Low, None),
        (VisualQualityTier::Medium, Some(24)),
        (VisualQualityTier::Ultra, Some(64)),
        (VisualQualityTier::High, Some(48)),
    ] {
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request(tier);
        app.update();
        let world = app.world();
        let ssr = world.get::<ScreenSpaceReflections>(camera);
        assert_eq!(ssr.map(|ssr| ssr.linear_steps), expected_steps);
        if let Some(ssr) = ssr {
            assert_eq!(ssr.thickness.to_bits(), thickness);
        }
        assert!(world.get::<TemporalAntiAliasing>(camera).is_some());
        let (entity, mesh, material) = surfaces.single(world).unwrap();
        assert_eq!((entity, mesh.0.id(), material.0.id()), binding);
        assert_eq!(
            world
                .resource::<Assets<Mesh>>()
                .get(binding.1)
                .unwrap()
                .attribute(Mesh::ATTRIBUTE_POSITION),
            Some(&positions)
        );
        assert_eq!(
            world
                .resource::<WaterRuntimeState>()
                .current_phase_radians
                .to_bits(),
            initial_phase
        );
        let stats = world.resource::<WaterProjectionStats>();
        assert_eq!(stats.source_hash, initial_stats.source_hash);
        assert_eq!(stats.terrain_source_hash, initial_stats.terrain_source_hash);
        assert!(stats.shader_uniforms_match_profile);
        let quality = stats.quality.as_ref().unwrap();
        assert_eq!(quality.requested_tier, tier);
        assert!(quality.ssr_budget_projected);
        assert!(!quality.full_preset_applied);
    }
}

#[test]
fn water_quality_requires_one_active_camera_and_rejects_foreign_policy() {
    let (mut app, camera) = app();
    app.update();
    app.world_mut().get_mut::<Camera>(camera).unwrap().is_active = false;
    app.update();
    assert!(
        !app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .ssr_budget_projected
    );
    app.world_mut().get_mut::<Camera>(camera).unwrap().is_active = true;
    let extra = app.world_mut().spawn(Camera3d::default()).id();
    app.update();
    assert!(
        !app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .ssr_budget_projected
    );
    app.world_mut().despawn(extra);
    app.update();
    assert!(
        app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .ssr_budget_projected
    );
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut policy = policy();
    policy.revision = forge_core::Revision::new(2);
    assert!(WaterQualityRequest::new(&terrain, policy, VisualQualityTier::High).is_err());
}

#[test]
fn inactive_auxiliary_camera_does_not_block_quality_changes_on_the_active_view() {
    let (mut app, camera) = app();
    app.update();
    let auxiliary = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera {
                is_active: false,
                ..default()
            },
            ScreenSpaceReflections {
                linear_steps: 17,
                ..default()
            },
        ))
        .id();
    for (tier, steps) in [
        (VisualQualityTier::Low, None),
        (VisualQualityTier::Ultra, Some(64)),
    ] {
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request(tier);
        app.update();
        assert_eq!(
            app.world()
                .get::<ScreenSpaceReflections>(camera)
                .map(|ssr| ssr.linear_steps),
            steps
        );
        assert_eq!(
            app.world()
                .get::<ScreenSpaceReflections>(auxiliary)
                .unwrap()
                .linear_steps,
            17
        );
        let observed = app
            .world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap();
        assert!(observed.ssr_budget_projected);
        assert!(!observed.full_preset_applied);
    }
}

#[test]
fn reflection_disable_restores_authored_material_and_preserves_shared_camera_effects() {
    let (mut app, camera) = app();
    app.update();
    let mut policy = policy();
    policy.presets.low.water.reflection = QualityWaterReflection::Disabled;
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    app.insert_resource(
        WaterQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap(),
    );
    let authored = app.world().resource::<WaterMaterialProjection>().0.clone();
    let hash = app.world().resource::<WaterQualityRequest>().hash.clone();
    let foreign = app
        .world_mut()
        .resource_mut::<Assets<VisualValleyWaterMaterial>>()
        .add(VisualValleyWaterMaterial {
            base: default(),
            extension: authored.clone(),
        });
    for (tier, expected) in [
        (VisualQualityTier::Low, 0.0),
        (VisualQualityTier::Medium, authored.optical_parameters.w),
        (VisualQualityTier::Low, 0.0),
        (VisualQualityTier::High, authored.optical_parameters.w),
    ] {
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request(tier);
        app.update();
        let world = app.world();
        let stats = world.resource::<WaterProjectionStats>();
        let quality = stats.quality.as_ref().unwrap();
        assert!(quality.ssr_budget_projected);
        assert!(quality.material_reflectance_projected);
        assert_eq!(
            quality.material_reflectance.unwrap().to_bits(),
            expected.to_bits()
        );
        assert!(!quality.full_preset_applied);
        assert_eq!(quality.policy_hash, hash);
        assert!(stats.shader_uniforms_match_profile);
        assert_eq!(world.resource::<WaterMaterialProjection>().0, authored);
        assert_eq!(
            world
                .resource::<Assets<VisualValleyWaterMaterial>>()
                .get(&foreign)
                .unwrap()
                .extension,
            authored
        );
        assert!(world.get::<TemporalAntiAliasing>(camera).is_some());
        assert!(
            world
                .get::<bevy::core_pipeline::prepass::DeferredPrepass>(camera)
                .is_some()
        );
        if tier == VisualQualityTier::Low {
            assert!(world.get::<ScreenSpaceReflections>(camera).is_none());
        }
    }
    let handle = app.world().resource::<WaterSurfaceMaterial>().0.clone();
    app.world_mut()
        .resource_mut::<Assets<VisualValleyWaterMaterial>>()
        .remove(handle.id());
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    let stats = app.world().resource::<WaterProjectionStats>();
    let quality = stats.quality.as_ref().unwrap();
    assert!(quality.ssr_budget_projected); // Camera budget is distinct from missing material.
    assert_eq!(quality.material_reflectance, None);
    assert!(!quality.material_reflectance_projected);
    assert!(!quality.full_preset_applied);
    assert!(!stats.shader_uniforms_match_profile);
}

#[test]
fn reflection_disable_does_not_mutate_material_or_cameras_when_active_view_is_ambiguous() {
    let (mut app, camera) = app();
    app.update();
    let authored = app.world().resource::<WaterMaterialProjection>().0.clone();
    let mut policy = policy();
    policy.presets.low.water.reflection = QualityWaterReflection::Disabled;
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    app.insert_resource(
        WaterQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap(),
    );
    app.world_mut()
        .spawn((Camera3d::default(), Camera::default()));
    app.update();
    let world = app.world();
    let quality = world
        .resource::<WaterProjectionStats>()
        .quality
        .as_ref()
        .unwrap();
    assert!(!quality.ssr_budget_projected);
    assert!(!quality.material_reflectance_projected);
    assert_eq!(
        quality.material_reflectance.unwrap().to_bits(),
        authored.optical_parameters.w.to_bits()
    );
    assert_eq!(
        world
            .get::<ScreenSpaceReflections>(camera)
            .unwrap()
            .linear_steps,
        48
    );
    let handle = &world.resource::<WaterSurfaceMaterial>().0;
    assert_eq!(
        world
            .resource::<Assets<VisualValleyWaterMaterial>>()
            .get(handle)
            .unwrap()
            .extension,
        authored
    );
}

#[test]
fn global_water_status_requires_material_evidence_not_only_the_camera_budget() {
    let (mut source, _) = app();
    source.update();
    let water = source.world().resource::<WaterProjectionStats>().clone();
    let policy_hash = water.quality.as_ref().unwrap().policy_hash.clone();
    let mut app = App::new();
    app.add_plugins(crate::GlobalQualityPlugin(VisualQualityTier::High));
    app.insert_resource(crate::TerrainLodProjectionStats {
        policy_hash,
        ..default()
    });
    for (tint, reflectance, expected) in [
        (true, true, crate::QualityDomainProjection::Partial),
        (false, true, crate::QualityDomainProjection::Pending),
        (true, false, crate::QualityDomainProjection::Pending),
        (false, false, crate::QualityDomainProjection::Pending),
        (true, true, crate::QualityDomainProjection::Partial),
    ] {
        let mut observation = water.clone();
        let quality = observation.quality.as_mut().unwrap();
        assert!(quality.ssr_budget_projected);
        quality.surface_tint_projected = tint;
        quality.material_reflectance_projected = reflectance;
        app.insert_resource(observation);
        app.update();
        let global = app.world().resource::<crate::GlobalQualityStats>();
        assert_eq!(global.domains[4], expected);
        assert!(!global.full_preset_applied);
        assert!(!global.available_budgets_projected); // Other adapters absent in this fixture.
    }
}

#[test]
fn reflection_preview_survives_tier_change_and_keeps_tint_override_independent() {
    let (mut app, camera) = app();
    app.update();
    let hash = app.world().resource::<WaterQualityRequest>().hash.clone();
    let authored = app.world().resource::<WaterMaterialProjection>().0.clone();
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .preview_surface_tint(Some(false));
    for (disabled, tier, expected) in [
        (true, VisualQualityTier::High, 0.0),
        (true, VisualQualityTier::Low, 0.0),
        (
            false,
            VisualQualityTier::High,
            authored.optical_parameters.w,
        ),
    ] {
        let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
        request.preview_reflections_disabled(disabled);
        request.request(tier);
        app.update();
        let state = app
            .world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap();
        assert_eq!(state.reflections_disabled_preview, disabled);
        assert_eq!(
            state.material_reflectance.unwrap().to_bits(),
            expected.to_bits()
        );
        assert!(state.material_reflectance_projected && state.ssr_budget_projected);
        assert_eq!(state.surface_tint_preview, Some(false));
        assert_eq!(
            state.surface_tint_strength.unwrap().to_bits(),
            0.0_f32.to_bits()
        );
        assert_eq!(state.policy_hash, hash);
        assert!(!state.full_preset_applied);
        if disabled {
            assert_eq!(state.requested.reflection, QualityWaterReflection::Disabled);
            assert_eq!(state.requested.reflection_steps, 0);
            assert!(app.world().get::<ScreenSpaceReflections>(camera).is_none());
        } else {
            assert_eq!(
                app.world()
                    .get::<ScreenSpaceReflections>(camera)
                    .unwrap()
                    .linear_steps,
                48
            );
        }
    }
}

#[test]
fn screen_reflection_isolation_preserves_tint_and_reflectance_and_restores_budget() {
    let (mut app, camera) = app();
    app.update();
    let source = app.world().resource::<WaterQualityRequest>().hash.clone();
    let authored = app.world().resource::<WaterMaterialProjection>().0.clone();
    for (tier, screen_disabled, all_disabled, steps) in [
        (VisualQualityTier::High, false, false, Some(48)),
        (VisualQualityTier::High, true, false, None),
        (VisualQualityTier::Ultra, true, false, None),
        (VisualQualityTier::Low, true, false, None),
        (VisualQualityTier::High, true, true, None),
        (VisualQualityTier::High, false, false, Some(48)),
    ] {
        let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
        request.request(tier);
        request.preview_screen_reflections_disabled(screen_disabled);
        request.preview_reflections_disabled(all_disabled);
        app.update();
        let observed = app
            .world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap();
        assert_eq!(
            observed.screen_reflections_disabled_preview,
            screen_disabled
        );
        assert_eq!(observed.ssr_linear_steps, steps);
        assert_eq!(observed.policy_hash, source);
        assert_eq!(
            observed.material_reflectance.unwrap().to_bits(),
            (if all_disabled {
                0.0
            } else {
                authored.optical_parameters.w
            })
            .to_bits()
        );
        assert_eq!(
            observed.surface_tint_strength.unwrap().to_bits(),
            authored.shallow_color_surface_tint.w.to_bits()
        );
        assert!(
            observed.ssr_budget_projected
                && observed.material_reflectance_projected
                && observed.surface_tint_projected
        );
        assert!(!observed.full_preset_applied);
        assert_eq!(
            app.world()
                .get::<ScreenSpaceReflections>(camera)
                .map(|ssr| ssr.linear_steps),
            steps
        );
    }
}

#[test]
fn transient_tint_preview_survives_quality_change_and_returns_to_unchanged_policy() {
    let (mut app, _) = app();
    app.update();
    let initial_hash = app.world().resource::<WaterQualityRequest>().hash.clone();
    for (preview, tier, expected) in [
        (Some(false), VisualQualityTier::High, 0.0_f32),
        (Some(false), VisualQualityTier::Low, 0.0),
        (Some(true), VisualQualityTier::Low, 0.18),
        (None, VisualQualityTier::High, 0.18),
    ] {
        let mut request = app.world_mut().resource_mut::<WaterQualityRequest>();
        request.preview_surface_tint(preview);
        request.request(tier);
        app.update();
        let state = app
            .world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap();
        assert_eq!(state.surface_tint_preview, preview);
        assert_eq!(
            state.surface_tint_strength.unwrap().to_bits(),
            expected.to_bits()
        );
        assert!(state.surface_tint_projected);
        assert_eq!(state.policy_hash, initial_hash);
        assert!(!state.full_preset_applied);
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "single cycle verifies restoration, isolation, ambiguous cameras and missing material"
)]
fn surface_tint_can_be_disabled_and_restored_without_rewriting_authored_projection() {
    let (mut app, _) = app();
    app.update();
    let handle = app
        .world()
        .resource::<super::super::WaterSurfaceMaterial>()
        .0
        .clone();
    let authored = app.world().resource::<WaterMaterialProjection>().0.clone();
    let original_phase = app
        .world()
        .resource::<WaterRuntimeState>()
        .current_phase_radians;
    let foreign = app
        .world_mut()
        .resource_mut::<Assets<VisualValleyWaterMaterial>>()
        .add(VisualValleyWaterMaterial {
            base: default(),
            extension: authored.clone(),
        });
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut policy = policy();
    policy.presets.low.water.refraction = QualityWaterRefraction::Disabled;
    app.insert_resource(
        WaterQualityRequest::new(&terrain, policy, VisualQualityTier::Low).unwrap(),
    );
    for (tier, expected) in [
        (VisualQualityTier::Low, 0.0),
        (
            VisualQualityTier::Medium,
            authored.shallow_color_surface_tint.w,
        ),
        (VisualQualityTier::Low, 0.0),
        (
            VisualQualityTier::High,
            authored.shallow_color_surface_tint.w,
        ),
    ] {
        app.world_mut()
            .resource_mut::<WaterQualityRequest>()
            .request(tier);
        app.update();
        let materials = app.world().resource::<Assets<VisualValleyWaterMaterial>>();
        assert_eq!(
            materials
                .get(&handle)
                .unwrap()
                .extension
                .shallow_color_surface_tint
                .w
                .to_bits(),
            expected.to_bits()
        );
        assert_eq!(materials.get(&foreign).unwrap().extension, authored);
        assert_eq!(
            app.world().resource::<WaterMaterialProjection>().0,
            authored
        );
        assert_eq!(
            app.world()
                .resource::<WaterRuntimeState>()
                .current_phase_radians
                .to_bits(),
            original_phase.to_bits()
        );
        let stats = app.world().resource::<WaterProjectionStats>();
        assert!(stats.shader_uniforms_match_profile);
        assert!(stats.quality.as_ref().unwrap().surface_tint_projected);
        assert!(!stats.quality.as_ref().unwrap().full_preset_applied);
    }
    let auxiliary = app.world_mut().spawn(Camera3d::default()).id();
    app.world_mut()
        .resource_mut::<WaterQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    assert!(
        !app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .surface_tint_projected
    );
    assert_eq!(
        app.world()
            .resource::<Assets<VisualValleyWaterMaterial>>()
            .get(&handle)
            .unwrap()
            .extension
            .shallow_color_surface_tint
            .w
            .to_bits(),
        authored.shallow_color_surface_tint.w.to_bits()
    );
    app.world_mut().despawn(auxiliary);
    app.world_mut()
        .resource_mut::<Assets<VisualValleyWaterMaterial>>()
        .remove(handle.id());
    app.update();
    assert!(
        !app.world()
            .resource::<WaterProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .surface_tint_projected
    );
}
