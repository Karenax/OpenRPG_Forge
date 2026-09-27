use super::*;
use crate::environment::VisualValleyEnvironmentPlugin;
use crate::visual_valley::VisualValleySun;

#[test]
fn effective_environment_installation_retains_authored_fog_and_source() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let guard = crate::global_quality::test_plan::guard(&policy, VisualQualityTier::High);
    let sources = guard.plan().source_bindings.unwrap();
    let profile = forge_world::embedded_visual_valley_environment_profile().unwrap();
    let hash = profile.content_hash().unwrap().to_string();
    let mut app = App::new();
    app.init_resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>();
    let (camera, _) = spawn_camera_and_sun(&mut app);
    app.insert_resource(
        EnvironmentQualityRequest::new(&terrain, policy.clone(), VisualQualityTier::High).unwrap(),
    );
    app.add_plugins(VisualValleyEnvironmentPlugin::new(profile));
    app.update();
    let fog = app
        .world()
        .get::<bevy::pbr::DistanceFog>(camera)
        .unwrap()
        .clone();
    assert!(
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request_effective(&guard, std::time::Duration::ZERO, guard.identity(), sources)
    );
    app.update();
    let stats = app.world().resource::<EnvironmentProjectionStats>();
    let observed = stats.quality.as_ref().unwrap();
    assert!(observed.authored_requested.volumetric_fog_enabled);
    assert!(!observed.requested.volumetric_fog_enabled);
    assert_eq!(observed.effective_request, Some(guard.identity()));
    assert!(observed.sky_budget_projected);
    let mut invalid_sunlight = stats.clone();
    for status in [
        "identity_mismatch",
        "source_or_state_unavailable",
        "invalid_lighting_response",
        "invalid_sun_topology",
        "unknown",
    ] {
        invalid_sunlight.sun.weather_response.status = status;
        let response = app.world().resource::<EnvironmentQualityRequest>().observe(
            &invalid_sunlight,
            stats.sky.requested,
            true,
        );
        assert!(
            !response.sky_budget_projected,
            "{status} must prevent acknowledgement"
        );
    }
    assert!(!observed.full_preset_applied);
    assert_eq!(stats.source_hash, hash);
    assert_eq!(
        format!(
            "{:?}",
            app.world().get::<bevy::pbr::DistanceFog>(camera).unwrap()
        ),
        format!("{fog:?}")
    );
    assert_eq!(
        app.world().resource::<EnvironmentQualityRequest>().document,
        policy
    );
    assert!(
        !app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request_effective(
                &guard,
                std::time::Duration::from_secs(10),
                guard.identity(),
                sources
            )
    );
    app.update();
    assert!(
        app.world()
            .resource::<EnvironmentProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .effective_request
            .is_none()
    );
    assert!(
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request_effective(&guard, std::time::Duration::ZERO, guard.identity(), sources)
    );
    app.world_mut()
        .resource_mut::<EnvironmentQualityRequest>()
        .request(VisualQualityTier::Low);
    assert!(
        app.world()
            .resource::<EnvironmentQualityRequest>()
            .effective
            .is_none()
    );
}

#[test]
fn inactive_auxiliary_camera_allows_quality_cycle_but_two_active_views_do_not() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let profile = forge_world::embedded_visual_valley_environment_profile().unwrap();
    let source_hash = profile.content_hash().unwrap().to_string();
    let mut app = App::new();
    app.init_resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>();
    spawn_camera_and_sun(&mut app);
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
    app.insert_resource(
        EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
    );
    app.add_plugins(VisualValleyEnvironmentPlugin::new(profile));
    for tier in [
        VisualQualityTier::High,
        VisualQualityTier::Low,
        VisualQualityTier::Ultra,
    ] {
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request(tier);
        app.update();
        let stats = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(stats.camera_3d_entities, 2);
        assert_eq!(stats.active_camera_3d_entities, 1);
        assert_eq!(stats.source_hash, source_hash);
        assert!(stats.quality.as_ref().unwrap().sky_budget_projected);
        assert!(!stats.quality.as_ref().unwrap().full_preset_applied);
        assert!(app.world().get::<AtmosphereSettings>(auxiliary).is_none());
    }
    let previous_size = app.world().resource::<probe::SkyProbeState>().size;
    app.world_mut()
        .get_mut::<Camera>(auxiliary)
        .unwrap()
        .is_active = true;
    app.world_mut()
        .resource_mut::<EnvironmentQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    assert!(
        !app.world()
            .resource::<EnvironmentProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .sky_budget_projected
    );
    assert_eq!(
        app.world().resource::<probe::SkyProbeState>().size,
        previous_size
    );
    app.world_mut()
        .get_mut::<Camera>(auxiliary)
        .unwrap()
        .is_active = false;
    app.update();
    assert!(
        app.world()
            .resource::<EnvironmentProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .sky_budget_projected
    );
}

fn spawn_camera_and_sun(app: &mut App) -> (Entity, Entity) {
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
    let sun = app
        .world_mut()
        .spawn((
            VisualValleySun,
            DirectionalLight::default(),
            Transform::IDENTITY,
        ))
        .id();
    (camera, sun)
}

#[test]
fn requested_fog_budget_tracks_quality_even_without_a_preview_volume() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let document = forge_world::embedded_visual_valley_environment_profile().unwrap();
    let source_hash = document.content_hash().unwrap().to_string();
    let mut app = App::new();
    app.init_resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>();
    spawn_camera_and_sun(&mut app);
    app.insert_resource(
        EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
    );
    app.add_plugins(VisualValleyEnvironmentPlugin::new(document));
    for (tier, steps) in [
        (VisualQualityTier::High, 64),
        (VisualQualityTier::Low, 0),
        (VisualQualityTier::Medium, 32),
        (VisualQualityTier::Ultra, 96),
        (VisualQualityTier::High, 64),
    ] {
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request(tier);
        app.update();
        let stats = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(stats.volumetric.requested_step_count, steps);
        assert!(!stats.volumetric.applied);
        assert_eq!(stats.volumetric.effective_maximum_distance_m, None);
        assert!(!stats.quality.as_ref().unwrap().full_preset_applied);
        assert_eq!(stats.source_hash, source_hash);
    }
}

#[test]
fn atmosphere_off_on_cycle_preserves_authored_scene_and_releases_probe() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    policy.presets.low.environment.atmosphere_enabled = false;
    policy.presets.low.environment.environment_map_resolution = 0;
    let document = forge_world::embedded_visual_valley_environment_profile().unwrap();
    let source_hash = document.content_hash().unwrap().to_string();
    let mut app = App::new();
    app.init_resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>();
    let (camera, sun) = spawn_camera_and_sun(&mut app);
    app.insert_resource(
        EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
    );
    app.add_plugins(VisualValleyEnvironmentPlugin::new(document));
    app.update();
    let mut atmospheres = app
        .world_mut()
        .query::<(Entity, &Atmosphere, &GlobalTransform)>();
    let (atmosphere_entity, atmosphere, transform) = atmospheres.single(app.world()).unwrap();
    let medium = atmosphere.medium.id();
    let planet_transform = *transform;
    let sun_transform = *app.world().get::<Transform>(sun).unwrap();
    let exposure = app
        .world()
        .get::<bevy::camera::Exposure>(camera)
        .unwrap()
        .ev100;
    for _ in 0..3 {
        let previous_probe = app
            .world()
            .resource::<probe::SkyProbeState>()
            .entity
            .unwrap();
        app.world_mut()
            .entity_mut(camera)
            .insert(bevy::light::GeneratedEnvironmentMapLight {
                environment_map: Handle::default(),
                intensity: 1.0,
                rotation: Quat::IDENTITY,
                affects_lightmapped_mesh_diffuse: true,
            });
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request(VisualQualityTier::Low);
        app.update();
        assert!(app.world().get_entity(previous_probe).is_err());
        assert!(app.world().get::<Atmosphere>(atmosphere_entity).is_none());
        assert!(app.world().get::<AtmosphereSettings>(camera).is_none());
        assert!(
            app.world()
                .get::<bevy::light::GeneratedEnvironmentMapLight>(camera)
                .is_none()
        );
        assert!(app.world().get::<bevy::camera::Hdr>(camera).is_some());
        assert!(app.world().get::<AmbientLight>(camera).is_some());
        assert!(app.world().get::<bevy::pbr::DistanceFog>(camera).is_some());
        let observation = app.world().resource::<EnvironmentProjectionStats>();
        assert!(observation.quality.as_ref().unwrap().sky_budget_projected);
        assert_eq!(observation.source_hash, source_hash);
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request(VisualQualityTier::High);
        app.update();
        let (entity, atmosphere, transform) = atmospheres.single(app.world()).unwrap();
        assert_eq!(entity, atmosphere_entity);
        assert_eq!(atmosphere.medium.id(), medium);
        assert_eq!(*transform, planet_transform);
        assert_eq!(*app.world().get::<Transform>(sun).unwrap(), sun_transform);
        assert_eq!(
            app.world()
                .get::<bevy::camera::Exposure>(camera)
                .unwrap()
                .ev100
                .to_bits(),
            exposure.to_bits()
        );
        assert_eq!(
            app.world()
                .resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>()
                .len(),
            1
        );
        assert!(app.world().get::<AtmosphereSettings>(camera).is_some());
        assert!(
            app.world()
                .resource::<EnvironmentProjectionStats>()
                .quality
                .as_ref()
                .unwrap()
                .sky_budget_projected
        );
        let mut probes = app
            .world_mut()
            .query_filtered::<Entity, With<probe::OwnedSkyProbe>>();
        assert_eq!(probes.iter(app.world()).count(), 1);
    }
}

#[test]
fn cpu_budget_cycle_preserves_sources_and_rejects_cached_resize() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let policy = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let profile = forge_world::embedded_visual_valley_environment_profile().unwrap();
    let source_hash = profile.content_hash().unwrap().to_string();
    let mut app = App::new();
    app.init_resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>();
    let (camera, sun) = spawn_camera_and_sun(&mut app);
    app.insert_resource(
        EnvironmentQualityRequest::new(&terrain, policy.clone(), VisualQualityTier::Low).unwrap(),
    );
    app.add_plugins(VisualValleyEnvironmentPlugin::new(profile));
    app.update();
    let pose = *app.world().get::<Transform>(sun).unwrap();
    let mut atmospheres = app.world_mut().query::<(Entity, &Atmosphere)>();
    let (entity, atmosphere) = atmospheres.single(app.world()).unwrap();
    let binding = (entity, atmosphere.medium.id());
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::Ultra,
        VisualQualityTier::High,
    ] {
        app.world_mut()
            .resource_mut::<EnvironmentQualityRequest>()
            .request(tier);
        app.update();
        let observation = app.world().resource::<EnvironmentProjectionStats>();
        assert_eq!(observation.source_hash, source_hash);
        assert!(observation.quality.as_ref().unwrap().sky_budget_projected);
        assert!(!observation.quality.as_ref().unwrap().full_preset_applied);
        assert_eq!(*app.world().get::<Transform>(sun).unwrap(), pose);
        let (entity, atmosphere) = atmospheres.single(app.world()).unwrap();
        assert_eq!((entity, atmosphere.medium.id()), binding);
    }
    app.world_mut()
        .entity_mut(camera)
        .insert(AtmosphereEnvironmentMapLight {
            size: UVec2::splat(256),
            ..default()
        });
    app.world_mut()
        .entity_mut(camera)
        .insert(bevy::light::GeneratedEnvironmentMapLight {
            environment_map: Handle::default(),
            intensity: 1.0,
            rotation: Quat::IDENTITY,
            affects_lightmapped_mesh_diffuse: true,
        });
    app.world_mut()
        .resource_mut::<EnvironmentQualityRequest>()
        .request(VisualQualityTier::Low);
    app.update();
    assert!(
        !app.world()
            .resource::<EnvironmentProjectionStats>()
            .quality
            .as_ref()
            .unwrap()
            .sky_budget_projected
    );
    assert_eq!(
        app.world()
            .get::<AtmosphereEnvironmentMapLight>(camera)
            .unwrap()
            .size,
        UVec2::splat(256)
    );
    let mut foreign = policy;
    foreign.revision = foreign.revision.next().unwrap();
    assert!(EnvironmentQualityRequest::new(&terrain, foreign, VisualQualityTier::Low).is_err());
}
