use super::Controls;
use bevy::prelude::*;
use bevy::render::render_resource::ShaderType;

#[test]
fn final_pass_prepares_its_own_depth_reading_and_preserves_existing_usages() {
    use bevy::render::render_resource::TextureUsages;
    let controls = Controls::for_quality(forge_project::PostProcessQualitySettings {
        tonemapping_enabled: true,
        bloom_enabled: true,
        color_grading_enabled: true,
    })
    .unwrap()
    .unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .add_systems(PostUpdate, super::prepare_depth);
    let existing = TextureUsages::RENDER_ATTACHMENT | TextureUsages::COPY_SRC;
    let source = app
        .world_mut()
        .spawn((
            Camera3d {
                depth_texture_usages: existing.into(),
                ..default()
            },
            controls,
        ))
        .id();
    let other = app
        .world_mut()
        .spawn(Camera3d {
            depth_texture_usages: existing.into(),
            ..default()
        })
        .id();
    app.update();
    let usage = TextureUsages::from(
        app.world()
            .get::<Camera3d>(source)
            .unwrap()
            .depth_texture_usages,
    );
    assert!(usage.contains(existing | TextureUsages::TEXTURE_BINDING));
    assert_eq!(
        TextureUsages::from(
            app.world()
                .get::<Camera3d>(other)
                .unwrap()
                .depth_texture_usages
        ),
        existing
    );
    app.update();
    assert_eq!(
        TextureUsages::from(
            app.world()
                .get::<Camera3d>(source)
                .unwrap()
                .depth_texture_usages
        ),
        usage
    );
}

#[test]
fn display_quality_gates_source_effects_and_refuses_an_unrepresentable_mix() {
    use forge_project::PostProcessQualitySettings as Settings;
    let controls = Controls::for_quality(Settings {
        tonemapping_enabled: true,
        bloom_enabled: true,
        color_grading_enabled: true,
    })
    .unwrap()
    .unwrap();
    assert_eq!(controls.effects.y.to_bits(), 0.05_f32.to_bits());
    assert_eq!(controls.grade.y.to_bits(), 1.06_f32.to_bits());
    let neutral = Controls::for_quality(Settings {
        tonemapping_enabled: true,
        bloom_enabled: false,
        color_grading_enabled: false,
    })
    .unwrap()
    .unwrap();
    assert_eq!(neutral.effects.y.to_bits(), 0.0_f32.to_bits());
    assert_eq!(neutral.effects.x.to_bits(), 0.0_f32.to_bits());
    assert_eq!(neutral.grade, Vec4::new(0.0, 1.0, 1.0, 0.0));
    assert!(
        Controls::for_quality(Settings {
            tonemapping_enabled: false,
            bloom_enabled: false,
            color_grading_enabled: false
        })
        .unwrap()
        .is_none()
    );
    assert!(
        Controls::for_quality(Settings {
            tonemapping_enabled: false,
            bloom_enabled: true,
            color_grading_enabled: false
        })
        .is_err()
    );
}

#[test]
fn final_submission_requires_matching_view_controls_size_and_fresh_frame() {
    use super::observation::Observations;
    let controls = Controls::for_quality(forge_project::PostProcessQualitySettings {
        tonemapping_enabled: true,
        bloom_enabled: true,
        color_grading_enabled: true,
    })
    .unwrap()
    .unwrap();
    let mut world = World::new();
    let a = world.spawn(controls).id();
    let b = world.spawn_empty().id();
    let size = UVec2::new(1280, 800);
    let bus = Observations::default();
    assert!(bus.snapshot(a).is_none());
    bus.publish(a, 20, size, &controls, 1);
    assert!(bus.snapshot(b).is_none());
    let snapshot = bus.snapshot(a).unwrap();
    assert!(snapshot.matches(&controls, size, 20));
    assert!(snapshot.matches(&controls, size, 21));
    assert!(!snapshot.matches(&controls, size, 22));
    assert!(!snapshot.matches(&controls, size, 19));
    assert!(!snapshot.matches(&controls, UVec2::ONE, 20));
    let mut changed = controls;
    changed.effects.y = 0.0;
    assert!(!snapshot.matches(&changed, size, 20));
    changed = controls;
    changed.timing.x = 0.02;
    changed.timing.y = 0.75;
    changed.frame.x = 100;
    assert!(snapshot.matches(&changed, size, 20));
    bus.publish(a, 19, size, &changed, 2);
    assert_eq!(bus.snapshot(a).unwrap(), snapshot);
    bus.publish(a, 21, size, &changed, 2);
    assert_eq!(bus.snapshot(a).unwrap().first_frame, 20);
    changed.grade.y = 0.5;
    bus.publish(a, 22, size, &changed, 3);
    assert_eq!(bus.snapshot(a).unwrap().first_frame, 22);
    world.insert_resource(bus);
    assert!(super::observation::submitted(&world, a).is_some());
    world.entity_mut(a).remove::<Controls>();
    assert!(super::observation::submitted(&world, a).is_none());
}

#[test]
#[cfg(feature = "physics-rapier")]
fn final_clock_follows_the_actual_solar_host_without_changing_display_identity() {
    let controls = Controls::for_quality(forge_project::PostProcessQualitySettings {
        tonemapping_enabled: true,
        bloom_enabled: true,
        color_grading_enabled: true,
    })
    .unwrap()
    .unwrap();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .insert_resource(crate::source_environment::host::Status {
            solar_night_bits: Some(0.75_f32.to_bits()),
            ..default()
        })
        .add_systems(PostUpdate, super::update_clock);
    let view = app.world_mut().spawn(controls).id();
    app.update();
    assert_eq!(
        app.world()
            .get::<Controls>(view)
            .unwrap()
            .timing
            .y
            .to_bits(),
        0.75_f32.to_bits()
    );
    assert_eq!(
        app.world().get::<Controls>(view).unwrap().settings_bits(),
        controls.settings_bits()
    );
    app.world_mut()
        .resource_mut::<crate::source_environment::host::Status>()
        .solar_night_bits = Some(0.25_f32.to_bits());
    app.update();
    assert_eq!(
        app.world()
            .get::<Controls>(view)
            .unwrap()
            .timing
            .y
            .to_bits(),
        0.25_f32.to_bits()
    );
}

#[test]
fn final_controls_preserve_the_uniform_abi() {
    assert_eq!(Controls::min_size().get(), 64);
    assert_eq!(std::mem::size_of::<Controls>(), 64);
}

#[test]
fn all_final_pass_shaders_validate() {
    for (name, source) in [
        ("down_first", include_str!("down_first.wgsl")),
        ("down", include_str!("down.wgsl")),
        ("up", include_str!("up.wgsl")),
        ("final", include_str!("final.wgsl")),
        ("meter", include_str!("meter.wgsl")),
    ] {
        let source = source.replace(
            "#import bevy_core_pipeline::fullscreen_vertex_shader::FullscreenVertexOutput",
            "struct FullscreenVertexOutput { @builtin(position) position: vec4f, @location(0) uv: vec2f };",
        );
        let module = naga::front::wgsl::parse_str(&source)
            .unwrap_or_else(|error| panic!("{name}: {}", error.emit_to_string(&source)));
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap_or_else(|error| panic!("{name}: {error}"));
    }
}
