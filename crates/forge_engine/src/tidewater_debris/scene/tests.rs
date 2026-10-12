//! Real acquisition geometry, CPU-only resource adoption; texture handles are test doubles.
use super::*;
use crate::tidewater_assets::{MaterialParameters, materials::LightingBindings};
use crate::tidewater_material::ObjectShadowSettings;
use forge_core::{ContentHash, EntityId};

fn fixture() -> PreparedDebris {
    let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_PACKAGE_FIXTURE").unwrap());
    let manifest = std::fs::read(root.join("package.json")).unwrap();
    let package = forge_assets::tidewater_debris::DebrisPackage::load(
        &manifest,
        forge_core::bytes_sha256(&manifest),
        |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
    )
    .unwrap();
    PreparedDebris::from_package(&package).unwrap()
}

fn materials(world: &mut World) -> ResidentMaterials {
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<VillageMaterial>>();
    world.init_resource::<Assets<NatureMaterial>>();
    world.init_resource::<Assets<Shader>>();
    let caustics =
        crate::tidewater_caustics::CausticMaterialInputs::disabled(&mut Assets::<Image>::default());
    let lighting = LightingBindings {
        scene_lighting: crate::tidewater_material::SceneLighting::new(
            [0.0, 1.0, 0.0],
            [1.0; 3],
            [[0.0; 3]; 9],
        )
        .unwrap(),
        environment: Handle::default(),
        hill_shadow: Handle::default(),
        object_shadow: Handle::default(),
        object_settings: ObjectShadowSettings::default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        wind: Vec4::new(1.0, 0.0, 4.0, 0.0),
        flags: Vec4::ZERO,
        caustics,
        ground_bounce: None,
        bounce_settings: Vec4::ZERO,
    };
    let textures = BTreeMap::from([("test-only".to_owned(), Handle::default())]);
    let prepare = |kind| {
        crate::tidewater_assets::materials::prepare(
            &MaterialParameters {
                kind,
                textures: vec!["test-only".into()],
                color: [1.0; 4],
                emissive: [0.0, 0.0, 0.0, 1.0],
                surface: [0.8, 0.0, 0.0, 1.0],
            },
            &textures,
            &lighting,
        )
        .unwrap()
    };
    let nature = super::super::nature::prepare_material(
        prepare(0),
        super::super::nature::NatureExtension {
            heights: Handle::default(),
            detail: Handle::default(),
            terrain_domain: Vec4::new(-1024.0, 2048.0, 2048.0, 1.0),
            terrain_sun: Vec4::new(0.0, 1.0, 0.0, 0.0),
        },
    )
    .unwrap();
    let nature = world.resource_mut::<Assets<NatureMaterial>>().add(nature);
    let handles: Vec<_> = [0, 1, 5]
        .into_iter()
        .map(|kind| {
            world
                .resource_mut::<Assets<VillageMaterial>>()
                .add(prepare(kind))
        })
        .collect();
    // Only resident-handle checks are under test. These are not shader/pixel qualification.
    let mut shaders = world.resource_mut::<Assets<Shader>>();
    shaders
        .insert(
            crate::tidewater_material::VILLAGE_SHADER.id(),
            Shader::from_wgsl("", "cpu-only-village"),
        )
        .unwrap();
    shaders
        .insert(
            super::super::nature::SHADER.id(),
            Shader::from_wgsl("", "cpu-only-nature"),
        )
        .unwrap();
    ResidentMaterials {
        wood: handles[0].clone(),
        hard: handles[1].clone(),
        fabric: handles[2].clone(),
        nature,
    }
}

#[test]
#[ignore = "Requires the real Debris acquisition; no GPU draw in this test"]
#[allow(
    clippy::cast_possible_truncation,
    reason = "Source acquisition bounds translations; common domain stores f32 metres."
)]
#[allow(
    clippy::too_many_lines,
    reason = "One acquired-scene lifecycle regression preserves revision refusal, deletion tombstones and caster retirement in order."
)]
fn common_instances_reject_bad_revision_then_preserve_deletion_and_casters() {
    let geometry = fixture();
    let expected_casters = geometry
        .objects
        .iter()
        .flat_map(|object| &object.parts)
        .filter(|part| part.shadow_mesh.is_some())
        .count();
    let mut placements: Vec<_> = geometry
        .objects
        .iter()
        .map(|object| {
            let [x, y, z] = object.source_translation_m.map(|value| value as f32);
            ManualPlacement {
                physics: None,
                id: EntityId::deterministic(format!("debris-common-test:{}", object.source_id)),
                name: object.kind.clone(),
                asset_id: object.asset_id,
                asset_hash: forge_assets::tidewater_debris::package_hash(),
                transform: forge_core::Transform::from_translation(forge_core::Vec3::new(x, y, z)),
                locked: false,
            }
        })
        .collect();
    placements.sort_by_key(|placement| placement.id);
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        bevy_rapier3d::prelude::RapierPhysicsPlugin::<bevy_rapier3d::prelude::NoUserData>::default(
        )
        .in_fixed_schedule(),
    ));
    let materials = materials(app.world_mut());
    PreparedScene::new(&geometry, &placements, &materials, app.world())
        .unwrap()
        .stage(app.world_mut());
    app.update();
    let roots: Vec<_> = app
        .world()
        .resource::<ResidentScene>()
        .instances
        .iter()
        .map(|instance| instance.root)
        .collect();
    assert_eq!(roots.len(), 805);
    assert_eq!(
        app.world()
            .resource::<ResidentScene>()
            .instances
            .iter()
            .map(|instance| instance.parts.len())
            .sum::<usize>(),
        865
    );
    for &root in &roots {
        app.world_mut()
            .entity_mut(root)
            .insert(InheritedVisibility::VISIBLE);
    }
    assert_eq!(
        app.world()
            .resource::<ResidentScene>()
            .shadow_draws(app.world())
            .unwrap()
            .len(),
        expected_casters
    );
    let parts = app
        .world()
        .resource::<ResidentScene>()
        .shadow_parts(app.world())
        .unwrap();
    assert_eq!(parts.len(), expected_casters);
    let caster_root = parts.first().expect("Acquired casters exist").0;
    let root_casters = parts
        .iter()
        .filter(|(root, _)| *root == caster_root)
        .count();
    let old_transform = *app.world().get::<Transform>(caster_root).unwrap();
    let mut moved_transform = old_transform;
    moved_transform.translation += Vec3::new(1.25, 0.5, -2.0);
    app.world_mut()
        .entity_mut(caster_root)
        .insert(moved_transform);
    app.update();
    let moved = app
        .world()
        .resource::<ResidentScene>()
        .shadow_parts(app.world())
        .unwrap();
    assert_eq!(
        parts.iter().map(|(root, _)| *root).collect::<Vec<_>>(),
        moved.iter().map(|(root, _)| *root).collect::<Vec<_>>()
    );
    for (_, model) in moved.iter().filter(|(root, _)| *root == caster_root) {
        assert_eq!(
            model.to_cols_array().map(f32::to_bits),
            moved_transform
                .to_matrix()
                .to_cols_array()
                .map(f32::to_bits)
        );
    }
    app.world_mut()
        .entity_mut(caster_root)
        .insert(InheritedVisibility::HIDDEN);
    assert_eq!(
        app.world()
            .resource::<ResidentScene>()
            .shadow_parts(app.world())
            .unwrap()
            .len(),
        expected_casters - root_casters
    );
    assert_eq!(
        app.world()
            .resource::<ResidentScene>()
            .shadow_draws(app.world())
            .unwrap()
            .len(),
        expected_casters - root_casters
    );
    app.world_mut()
        .entity_mut(caster_root)
        .insert((old_transform, InheritedVisibility::VISIBLE));
    app.update();
    assert!(!crate::manual_placements_ready(
        app.world_mut(),
        &placements
    ));
    let entity_count = app.world().entities().len();
    let mesh_count = app.world().resource::<Assets<Mesh>>().len();
    let mut invalid = placements.clone();
    invalid[0].asset_hash = ContentHash::from_bytes([0; 32]);
    assert!(PreparedScene::new(&geometry, &invalid, &materials, app.world()).is_err());
    assert_eq!(entity_count, app.world().entities().len());
    assert_eq!(mesh_count, app.world().resource::<Assets<Mesh>>().len());
    for &root in &roots {
        assert!(app.world().get_entity(root).is_ok());
    }
    let collision_root =
        std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_COLLISION_FIXTURE").unwrap());
    let frames = forge_assets::tidewater_debris::collisions::CollisionFrames::load(
        &std::fs::read(collision_root.join("collision-frames.json")).unwrap(),
    )
    .unwrap();
    PreparedScene::new(&geometry, &placements, &materials, app.world())
        .unwrap()
        .with_collision_frames(&geometry, &frames)
        .unwrap()
        .stage(app.world_mut());
    assert!(!crate::manual_placements_ready(
        app.world_mut(),
        &placements
    ));
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(crate::manual_placements_ready(app.world_mut(), &placements));
    let collision_entities: Vec<_> = app
        .world()
        .resource::<ResidentScene>()
        .instances
        .iter()
        .flat_map(|instance| instance.collisions.iter().copied())
        .collect();
    assert_eq!(collision_entities.len(), 51);
    assert_eq!(
        app.world()
            .resource::<ResidentScene>()
            .instances
            .iter()
            .filter(|instance| app
                .world()
                .get::<ManualPlacementCollisionNotRequired>(instance.root)
                .is_some())
            .count(),
        762
    );
    for &entity in &collision_entities {
        let source = app
            .world()
            .get::<super::super::collisions::SourceCollision>(entity)
            .unwrap();
        assert!(source.solid);
        assert!(source.walkable.is_none_or(|walkable| !walkable));
    }
    PreparedScene::new(&geometry, &[], &materials, app.world())
        .unwrap()
        .stage(app.world_mut());
    assert!(app.world().resource::<ResidentScene>().instances.is_empty());
    assert!(
        !app.world()
            .resource::<ResidentScene>()
            .collisions_retired(app.world())
    );
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(
        app.world()
            .resource::<ResidentScene>()
            .collisions_retired(app.world())
    );
    for entity in collision_entities {
        assert!(app.world().get_entity(entity).is_err());
    }
    for &root in &roots {
        assert!(app.world().get_entity(root).is_err());
    }
    assert!(
        app.world()
            .resource::<ResidentScene>()
            .shadow_draws(app.world())
            .unwrap()
            .is_empty()
    );
}
