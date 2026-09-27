use super::*;

#[test]
fn same_specular_handle_still_updates_each_lighting_binding_field() {
    let mut app = App::new();
    app.init_resource::<Assets<Image>>()
        .add_systems(Update, publish);
    let camera = app.world_mut().spawn(Camera3d::default()).id();
    let probe = request(&mut app, camera, Some(64)).unwrap();
    generated(&mut app, probe, 64);
    app.update();
    let replacement_diffuse = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(Image::default());
    for field in 0..4 {
        {
            let mut light = app
                .world_mut()
                .get_mut::<EnvironmentMapLight>(probe)
                .unwrap();
            match field {
                0 => light.intensity = 2.0,
                1 => light.rotation = Quat::from_rotation_y(0.5),
                2 => light.diffuse_map = replacement_diffuse.clone(),
                _ => light.affects_lightmapped_mesh_diffuse = false,
            }
        }
        app.update();
        let source = app.world().get::<EnvironmentMapLight>(probe).unwrap();
        let bound = app.world().get::<EnvironmentMapLight>(camera).unwrap();
        assert_eq!(
            bound.intensity.to_bits(),
            source.intensity.to_bits(),
            "field {field}"
        );
        assert_eq!(bound.rotation, source.rotation, "field {field}");
        assert_eq!(
            bound.diffuse_map.id(),
            source.diffuse_map.id(),
            "field {field}"
        );
        assert_eq!(
            bound.specular_map.id(),
            source.specular_map.id(),
            "field {field}"
        );
        assert_eq!(
            bound.affects_lightmapped_mesh_diffuse, source.affects_lightmapped_mesh_diffuse,
            "field {field}"
        );
    }
    let last_changed = app
        .world()
        .entity(camera)
        .get_ref::<EnvironmentMapLight>()
        .unwrap()
        .last_changed();
    app.update();
    assert_eq!(
        app.world()
            .entity(camera)
            .get_ref::<EnvironmentMapLight>()
            .unwrap()
            .last_changed(),
        last_changed,
        "unchanged binding must not be republished each frame"
    );
}

#[test]
fn resize_retains_previous_lighting_without_acknowledging_the_new_budget() {
    let mut app = App::new();
    app.init_resource::<Assets<Image>>();
    app.add_systems(Update, publish);
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
    let first = request(&mut app, camera, Some(64)).unwrap();
    let old_image = generated(&mut app, first, 64);
    app.update();
    let replacement = request(&mut app, camera, Some(256)).unwrap();
    app.update();
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .map(|map| map.specular_map.id()),
        Some(old_image.id())
    );
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
    generated(&mut app, replacement, 64);
    app.update();
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .unwrap()
            .specular_map
            .id(),
        old_image.id()
    );
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
    let new_image = generated(&mut app, replacement, 256);
    app.update();
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .unwrap()
            .specular_map
            .id(),
        new_image.id()
    );
    assert!(app.world().resource::<SkyProbeState>().texture_bound);
    request(&mut app, camera, None);
    app.update();
    assert!(app.world().get::<EnvironmentMapLight>(camera).is_none());
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
}

#[test]
fn sky_texture_publishes_only_to_the_single_active_camera() {
    let mut app = App::new();
    app.init_resource::<Assets<Image>>();
    app.add_systems(Update, publish);
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
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
    let probe = request(&mut app, camera, Some(64)).unwrap();
    let image = generated(&mut app, probe, 64);
    app.update();
    assert!(app.world().resource::<SkyProbeState>().texture_bound);
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .unwrap()
            .specular_map
            .id(),
        image.id()
    );
    assert!(app.world().get::<EnvironmentMapLight>(auxiliary).is_none());
    app.world_mut()
        .get_mut::<Camera>(auxiliary)
        .unwrap()
        .is_active = true;
    app.update();
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
    assert!(app.world().get::<EnvironmentMapLight>(auxiliary).is_none());
    app.world_mut()
        .get_mut::<Camera>(auxiliary)
        .unwrap()
        .is_active = false;
    app.update();
    assert!(app.world().resource::<SkyProbeState>().texture_bound);
}

fn request(app: &mut App, camera: Entity, size: Option<u32>) -> Option<Entity> {
    let mut state = app
        .world_mut()
        .remove_resource::<SkyProbeState>()
        .unwrap_or_default();
    state.request(
        &mut app.world_mut().commands(),
        camera,
        size.map(|size| AtmosphereEnvironmentMapLight {
            size: UVec2::splat(size),
            ..default()
        }),
    );
    app.world_mut().flush();
    let entity = state.entity;
    app.insert_resource(state);
    entity
}

fn generated(app: &mut App, probe: Entity, size: u32) -> Handle<Image> {
    let mut image = Image::default();
    image.texture_descriptor.size = bevy::render::render_resource::Extent3d {
        width: size,
        height: size,
        depth_or_array_layers: 6,
    };
    let handle = app.world_mut().resource_mut::<Assets<Image>>().add(image);
    app.world_mut()
        .entity_mut(probe)
        .insert(GeneratedEnvironmentMapLight {
            environment_map: handle.clone(),
            intensity: 1.0,
            rotation: Quat::IDENTITY,
            affects_lightmapped_mesh_diffuse: true,
        });
    app.world_mut()
        .entity_mut(probe)
        .insert(EnvironmentMapLight {
            diffuse_map: handle.clone(),
            specular_map: handle.clone(),
            ..default()
        });
    handle
}

#[test]
fn cache_owner_replaces_probe_and_only_binds_matching_texture() {
    let mut app = App::new();
    app.init_resource::<Assets<Image>>();
    app.add_systems(Update, publish);
    let camera = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
    let low = request(&mut app, camera, Some(64)).unwrap();
    generated(&mut app, low, 256);
    app.update();
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
    assert!(app.world().get::<EnvironmentMapLight>(camera).is_none());
    let image_low = generated(&mut app, low, 64);
    app.update();
    assert!(app.world().resource::<SkyProbeState>().texture_bound);
    assert!(
        app.world()
            .get::<GeneratedEnvironmentMapLight>(camera)
            .is_none()
    );
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .unwrap()
            .specular_map
            .id(),
        image_low.id()
    );
    assert_eq!(request(&mut app, camera, Some(64)), Some(low));
    let high = request(&mut app, camera, Some(256)).unwrap();
    assert_ne!(high, low);
    assert!(app.world().get_entity(low).is_err());
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .unwrap()
            .specular_map
            .id(),
        image_low.id()
    );
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
    let image_high = generated(&mut app, high, 256);
    app.update();
    assert_eq!(
        app.world()
            .get::<EnvironmentMapLight>(camera)
            .unwrap()
            .specular_map
            .id(),
        image_high.id()
    );
    let extra = app
        .world_mut()
        .spawn((Camera3d::default(), Camera::default()))
        .id();
    app.update();
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
    app.world_mut().despawn(extra);
    app.update();
    assert!(app.world().resource::<SkyProbeState>().texture_bound);
    assert_eq!(request(&mut app, camera, None), None);
    app.update();
    assert!(app.world().get_entity(high).is_err());
    assert!(app.world().get::<EnvironmentMapLight>(camera).is_none());
    assert!(!app.world().resource::<SkyProbeState>().texture_bound);
}
