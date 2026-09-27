use super::*;
use forge_project::{VisualQualityTier, VisualValleyQualityProfileDocument};

fn document() -> WeatherProfileDocument {
    WeatherProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/weather.v1.json"
    ))
    .unwrap()
}

fn policy() -> VisualValleyQualityProfileDocument {
    VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap()
}

fn app(policy: VisualValleyQualityProfileDocument) -> (App, Entity) {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, AssetPlugin::default()));
    app.init_asset::<Mesh>();
    app.init_asset::<VisualValleyTerrainMaterial>();
    app.init_asset::<VisualValleyVegetationMaterial>();
    app.insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
        std::time::Duration::ZERO,
    ));
    let camera = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera::default(),
            Transform::default(),
            GlobalTransform::default(),
        ))
        .id();
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    app.insert_resource(
        PostProcessQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
    );
    app.add_plugins(
        VisualValleyWeatherPlugin::new(document()).with_initial_state(WeatherState::Rain),
    );
    (app, camera)
}

#[test]
fn post_process_quality_cycle_preserves_authored_weather_and_restores_the_rain_grade() {
    let mut policy = policy();
    // Synthetic valid preset covers switches not disabled by the shipping Low fixture.
    policy.presets.low.post_process.color_grading_enabled = false;
    policy.presets.low.post_process.tonemapping_enabled = false;
    let (mut app, camera) = app(policy.clone());
    app.init_resource::<Assets<bevy::light::atmosphere::ScatteringMedium>>();
    app.world_mut().spawn((
        crate::visual_valley::VisualValleySun,
        DirectionalLight::default(),
        Transform::IDENTITY,
    ));
    app.add_plugins(crate::VisualValleyEnvironmentPlugin::new(
        forge_world::embedded_visual_valley_environment_profile().unwrap(),
    ));
    app.update();
    app.world_mut()
        .resource_mut::<WeatherRuntimeState>()
        .simulation_seconds = 123.0;
    let document = document();
    let source_hash = document.content_hash().unwrap().to_string();
    let mut batches = app
        .world_mut()
        .query_filtered::<(Entity, &Mesh3d), With<VisualValleyRainBatch>>();
    let (entity, mesh) = batches.single(app.world()).unwrap();
    let rain_binding = (entity, mesh.0.id());
    for tier in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::Ultra,
        VisualQualityTier::High,
    ] {
        app.world_mut()
            .resource_mut::<PostProcessQualityRequest>()
            .request(tier);
        app.update();
        let settings = policy.preset(tier).post_process;
        let world = app.world();
        let environment = world.resource::<crate::EnvironmentProjectionStats>();
        assert_eq!(
            environment.status,
            crate::EnvironmentProjectionStatus::ObservedApproximation
        );
        assert_eq!(
            environment.sky.tonemapping,
            Some(if settings.tonemapping_enabled {
                "tony_mc_mapface"
            } else {
                "none"
            })
        );
        assert_eq!(world.get::<Bloom>(camera).is_some(), settings.bloom_enabled);
        if let Some(bloom) = world.get::<Bloom>(camera) {
            assert_eq!(
                bloom.intensity.to_bits(),
                document.post_processing.bloom.intensity.to_bits()
            );
        }
        assert_eq!(
            world.get::<Tonemapping>(camera),
            Some(&post_process_quality::tonemapping(
                settings.tonemapping_enabled
            ))
        );
        let expected = if settings.color_grading_enabled {
            color_grading_component(document.post_processing.rain_grade)
        } else {
            ColorGrading::default()
        };
        assert!(post_process_quality::grading_matches(
            world.get::<ColorGrading>(camera).unwrap(),
            &expected
        ));
        let stats = world.resource::<WeatherProjectionStats>();
        assert_eq!(
            stats
                .post_processing
                .quality
                .as_ref()
                .unwrap()
                .projected_tier,
            Some(tier)
        );
        assert_eq!(stats.source_hash, source_hash);
        let runtime = world.resource::<WeatherRuntimeState>();
        assert_eq!(runtime.simulation_seconds.to_bits(), 123.0_f64.to_bits());
        assert_eq!(
            runtime.current_wetness.to_bits(),
            document.wetness.rain_target.to_bits()
        );
        let (entity, mesh) = batches.single(world).unwrap();
        assert_eq!((entity, mesh.0.id()), rain_binding);
        assert_eq!(
            world.resource::<VisualValleyWeatherProfile>().document,
            document
        );
    }
}

#[test]
fn post_process_quality_does_not_claim_success_with_two_active_cameras() {
    let (mut app, _) = app(policy());
    app.update();
    let extra = app
        .world_mut()
        .spawn((
            Camera3d::default(),
            Camera::default(),
            GlobalTransform::default(),
        ))
        .id();
    app.update();
    assert_eq!(
        app.world()
            .resource::<WeatherProjectionStats>()
            .post_processing
            .quality
            .as_ref()
            .unwrap()
            .projected_tier,
        None
    );
    app.world_mut().despawn(extra);
    app.update();
    assert_eq!(
        app.world()
            .resource::<WeatherProjectionStats>()
            .post_processing
            .quality
            .as_ref()
            .unwrap()
            .projected_tier,
        Some(VisualQualityTier::High)
    );
}

#[test]
fn post_process_quality_rejects_foreign_policy_and_grading_mismatches() {
    let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut policy = policy();
    policy.revision = forge_core::Revision::new(2);
    assert!(PostProcessQualityRequest::new(&terrain, policy, VisualQualityTier::High).is_err());
    let expected = ColorGrading::default();
    let mut actual = expected.clone();
    actual.global.temperature = 0.2;
    assert!(!post_process_quality::grading_matches(&actual, &expected));
    actual = expected.clone();
    actual.highlights.gamma = 1.2;
    assert!(!post_process_quality::grading_matches(&actual, &expected));
}
