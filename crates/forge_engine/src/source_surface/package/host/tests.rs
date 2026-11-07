use super::*;
use crate::source_surface::{geometry::MeshPart, tests::lighting};
use forge_core::{AssetId, EntityId, bytes_sha256};

fn fixture() -> (App, ManualPlacement, Handle<Mesh>) {
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        crate::ManualPlacementCollisionPlugin,
    ));
    crate::sampled_terrain_physics::register(&mut app);
    app.init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<SourceSurfaceMaterial>>()
        .init_resource::<Assets<Image>>()
        .init_resource::<Assets<bevy::render::storage::ShaderBuffer>>()
        .init_resource::<Status>();
    let source = ManualPlacement {
        id: EntityId::deterministic("host-edit/placement"),
        name: "Editable source".into(),
        asset_id: AssetId::deterministic("host-edit/geometry"),
        asset_hash: bytes_sha256(b"mesh"),
        transform: forge_core::Transform::IDENTITY,
        locked: false,
        particles: None,
        physics: None,
        local_lights: None,
    };
    let geometry = PreparedMeshes::from_parts(BTreeMap::from([(
        (source.asset_id, source.asset_hash),
        vec![MeshPart {
            mesh: Mesh::from(Cuboid::new(2.0, 2.0, 2.0)),
            material: "opaque".into(),
        }],
    )]))
    .stage(&mut app.world_mut().resource_mut::<Assets<Mesh>>());
    let mesh = geometry
        .resolve(source.asset_id, source.asset_hash)
        .unwrap()[0]
        .mesh
        .clone();
    let material = app
        .world_mut()
        .resource_mut::<Assets<SourceSurfaceMaterial>>()
        .add(lighting());
    let materials = BTreeMap::from([("opaque".into(), material)]);
    let prepared = PreparedScene::new(
        std::slice::from_ref(&source),
        &geometry,
        &materials,
        app.world().resource::<Assets<Mesh>>(),
        app.world().resource::<Assets<SourceSurfaceMaterial>>(),
    )
    .unwrap();
    prepared.stage(app.world_mut());
    let entities = mark_parts(app.world_mut());
    let (underwater, level) = underwater::absent_water(app.world_mut()).unwrap();
    app.insert_resource(State {
        geometry,
        _library: ResidentLibrary {
            materials: BTreeMap::new(),
            textures: BTreeMap::new(),
        },
        materials,
        entities,
        water_mode: WaterMode::Absent,
        water: WaterBindings {
            params: underwater,
            level,
            frame: underwater::NEUTRAL_FRAME,
        },
    });
    (app, source, mesh)
}

#[test]
fn live_scene_time_and_night_reach_glass_while_cloth_receives_the_full_wind() {
    let mut scene = lighting();
    scene.settings.wind = Vec4::new(0.0, 1.0, 7.0, 2.1);
    scene.settings.flags.y = 0.8;
    let mut glass = lighting();
    glass.kind = 0;
    glass.settings.wind = Vec4::new(1.0, 0.0, 3.0, 0.0);
    glass.settings.flags = Vec4::new(4.0, 0.0, 5.0, 6.0);
    let original_color = glass.settings.color;
    sync_scene_motion(&mut glass, &scene);
    assert_eq!(glass.settings.wind, Vec4::new(1.0, 0.0, 3.0, 2.1));
    assert_eq!(glass.settings.flags, Vec4::new(4.0, 0.8, 5.0, 6.0));
    assert_eq!(glass.settings.color, original_color);

    scene.settings.flags.y = 0.0;
    scene.settings.wind.w = 2.2;
    sync_scene_motion(&mut glass, &scene);
    assert_eq!(glass.settings.wind.w.to_bits(), 2.2_f32.to_bits());
    assert_eq!(glass.settings.flags.y.to_bits(), 0.0_f32.to_bits());
    for kind in [5, 6] {
        let mut material = lighting();
        material.kind = kind;
        let flags = material.settings.flags;
        sync_scene_motion(&mut material, &scene);
        assert_eq!(material.settings.wind, scene.settings.wind);
        assert_eq!(material.settings.flags, flags);
    }
    for kind in [1, 2, 3, 4, 7] {
        let mut material = lighting();
        material.kind = kind;
        let before = material.settings.clone();
        sync_scene_motion(&mut material, &scene);
        assert_eq!(material.settings, before);
    }
}

#[test]
fn required_water_bindings_follow_current_shared_inputs_and_refuse_removal() {
    let mut world = World::new();
    assert!(water_bindings(&world).is_err());
    assert!(validate_water_mode(&world, WaterMode::Required).is_err());
    assert!(validate_water_mode(&world, WaterMode::Absent).is_ok());
    world.init_resource::<Assets<Image>>();
    let mut input =
        underwater::CausticMaterialInputs::disabled(&mut world.resource_mut::<Assets<Image>>());
    input.params = underwater::CausticSampleParams::new(2.0, 4.0, Vec3::Y, 0.75)
        .unwrap()
        .with_underwater([32.0, 16.0, 8.0, 4.0], 1.0, 3.0, Vec3::ONE, Vec3::ONE)
        .unwrap();
    let expected = input.params.clone();
    let level = input.light_maps.level.clone();
    let frame = input.light_maps.frame.clone();
    world.insert_resource(input);
    assert!(validate_water_mode(&world, WaterMode::Required).is_ok());
    assert!(validate_water_mode(&world, WaterMode::Absent).is_err());
    let actual = water_bindings(&world).unwrap();
    assert_eq!(actual.params, expected);
    assert_eq!(actual.level, level);
    assert_eq!(actual.frame, frame);
    let mut material = lighting();
    let original_kind = material.kind;
    apply_water(&mut material, &actual);
    assert_eq!(material.underwater_params, expected);
    assert_eq!(material.underwater_level, level);
    assert_eq!(material.underwater_map_frame, frame);
    assert_eq!(material.kind, original_kind);
    world
        .resource_mut::<underwater::CausticMaterialInputs>()
        .params
        .water_scattering
        .w = 0.0;
    assert_eq!(
        water_bindings(&world)
            .unwrap()
            .params
            .water_scattering
            .w
            .to_bits(),
        0.0_f32.to_bits()
    );
    world.remove_resource::<underwater::CausticMaterialInputs>();
    assert!(water_bindings(&world).is_err());
    assert!(validate_water_mode(&world, WaterMode::Required).is_err());
}

#[test]
fn host_stops_when_water_inputs_appear_or_disappear() {
    let (mut app, _, _) = fixture();
    let input = underwater::CausticMaterialInputs::disabled(
        &mut app.world_mut().resource_mut::<Assets<Image>>(),
    );
    app.insert_resource(input);
    update(app.world_mut());
    assert!(
        app.world()
            .resource::<Status>()
            .error
            .as_ref()
            .is_some_and(|error| error.contains("cannot ignore"))
    );

    app.world_mut().resource_mut::<Status>().error = None;
    app.world_mut().resource_mut::<State>().water_mode = WaterMode::Required;
    app.world_mut()
        .remove_resource::<underwater::CausticMaterialInputs>();
    update(app.world_mut());
    assert!(
        app.world()
            .resource::<Status>()
            .error
            .as_ref()
            .is_some_and(|error| error.contains("require"))
    );
}

#[test]
fn authoring_replacement_reuses_geometry_and_refuses_stale_or_unresolved_sources() {
    let (mut app, source, mesh) = fixture();
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(
        app.world()
            .resource::<ResidentScene>()
            .is_synchronized(app.world())
    );
    let original_root = app.world().resource::<ResidentScene>().instances[0].root;
    let mut next = source.clone();
    next.transform.translation_m.x = 12.0;
    assert!(replace_placements(app.world_mut(), &[], std::slice::from_ref(&next)).is_err());
    let mut invalid = next.clone();
    invalid.asset_hash = bytes_sha256(b"foreign mesh");
    assert!(
        replace_placements(
            app.world_mut(),
            std::slice::from_ref(&source),
            std::slice::from_ref(&invalid)
        )
        .is_err()
    );
    assert_eq!(
        app.world().resource::<ResidentScene>().instances[0].root,
        original_root
    );
    replace_placements(
        app.world_mut(),
        std::slice::from_ref(&source),
        std::slice::from_ref(&next),
    )
    .unwrap();
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    let scene = app.world().resource::<ResidentScene>();
    assert!(scene.is_synchronized(app.world()));
    assert_eq!(scene.instances[0].source, next);
    assert!(app.world().get_entity(original_root).is_err());
    assert_eq!(
        app.world()
            .resource::<State>()
            .geometry
            .resolve(source.asset_id, source.asset_hash)
            .unwrap()[0]
            .mesh,
        mesh
    );
    assert!(
        scene.instances[0]
            .parts
            .iter()
            .all(|&part| owns(app.world(), part))
    );
}
