use super::*;
use crate::source_vegetation::{Params, world::WorldPlantMaterial};
use forge_core::{EntityId, bytes_sha256};

fn scene() -> Vegetation {
    let mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    let level = |near, range| Level {
        description: LevelDescription {
            near,
            // A source-profile capacity is data, not a reference-map population guard.
            capacity: 65_584,
            range,
            kind: 0,
            attributes: BTreeMap::new(),
            indices: String::new(),
        },
        mesh: mesh.clone(),
    };
    Vegetation {
        head_hash: Some(bytes_sha256(b"independent renderer scene")),
        groups: vec![Group {
            identities: Some(
                (0..3)
                    .map(|i| EntityId::deterministic(format!("sample-{i}")))
                    .collect(),
            ),
            name: "independent-layer".into(),
            query_radius: 12.0,
            cell_size: 7.0,
            far_exclude_near: true,
            near_range: 20.0,
            refresh_distance: 2.0,
            sort_far: true,
            records: [0.0, 5.0, 100.0]
                .map(|x| Record {
                    i_pos: Vec4::new(x, 0.0, 0.0, 1.0),
                    i_dat: Vec4::ONE,
                })
                .to_vec(),
            models: vec![Mat4::IDENTITY; 3],
            query_radii: vec![0.0; 3],
            levels: vec![
                level(true, [0.0, 10.0, 20.0]),
                level(false, [10.0, 20.0, 80.0]),
            ],
        }],
    }
}

fn world() -> World {
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<WorldPlantMaterial>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    world
}

fn material() -> WorldPlantMaterial {
    WorldPlantMaterial {
        params: Params {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
            wind: Vec4::X,
            gust_lod: Vec4::ZERO,
            sun_night: Vec4::ZERO,
            lod_end_frame: Vec4::ZERO,
        },
        detail: Handle::default(),
        leaf: Handle::default(),
        impostor_a: Handle::default(),
        impostor_b: Handle::default(),
        instances: Handle::default(),
        models: Handle::default(),
        frame: Handle::default(),
        grass_heights: None,
        grass_mask: None,
        grass_deltas: None,
        grass_field: Vec4::ZERO,
        scene_lighting: Handle::default(),
        live_shadows: Handle::default(),
        local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
        environment: None,
        hill_shadow: None,
        object_shadow: None,
        object_settings: crate::source_shadows::ObjectShadowSettings::default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        source_lighting: false,
        hill_domain: bevy::prelude::Vec4::new(-1024.0, -1024.0, 2048.0, 0.0),
        kind: 0,
    }
}

#[test]
fn world_plant_material_visits_the_shared_local_light_buffer_dependency() {
    use bevy::asset::VisitAssetDependencies;
    let material = material();
    let mut dependencies = Vec::new();
    material.visit_dependencies(&mut |id| dependencies.push(id));
    assert_eq!(
        dependencies
            .iter()
            .filter(|&&id| id
                == crate::source_lighting::local_lights::gpu::BUFFER
                    .id()
                    .untyped())
            .count(),
        1
    );
}

#[test]
fn independent_scene_uses_shared_selection_buffers_entities_and_identity_snapshots() {
    let mut world = world();
    let foreign = world.spawn_empty().id();
    let mut resident = resident::Resident::stage(&mut world, scene(), &material()).unwrap();
    let view = View {
        camera: Vec3::ZERO,
        visibility: Visibility::Visible,
    };
    let first = resident.select(&mut world, view).unwrap();
    assert_eq!(first.spawned, 3);
    assert_eq!(resident.entity_count(), 3);
    assert!(resident.is_intact(&world));
    let entities = resident.entities();
    let retained = resident.select(&mut world, view).unwrap();
    assert_eq!(
        (retained.spawned, retained.removed, retained.retained),
        (0, 0, 3)
    );
    assert_eq!(resident.entities(), entities);
    let snapshot = resident
        .picking_snapshot(&world, |_, first, count| Ok((first, count)))
        .unwrap();
    assert_eq!(snapshot.identity_count(), 3);
    assert_eq!(snapshot.visible_count(), 3);
    assert_eq!(snapshot.batches.len(), 2);
    assert_eq!(
        snapshot.resolve(1, snapshot.head).unwrap(),
        Some(EntityId::deterministic("sample-0"))
    );
    let assets = (
        world.resource::<Assets<Mesh>>().len(),
        world.resource::<Assets<ShaderBuffer>>().len(),
    );
    resident
        .select(
            &mut world,
            View {
                camera: Vec3::X * 100.0,
                ..view
            },
        )
        .unwrap();
    assert!(resident.is_intact(&world));
    resident.select(&mut world, view).unwrap();
    assert!(resident.is_intact(&world));
    assert_eq!(
        assets,
        (
            world.resource::<Assets<Mesh>>().len(),
            world.resource::<Assets<ShaderBuffer>>().len()
        )
    );
    resident.retire(&mut world);
    assert!(world.get_entity(foreign).is_ok());
    assert_eq!(world.query::<&ImportedPlant>().iter(&world).count(), 0);
}

#[test]
fn source_model_replacement_preserves_other_plants_and_can_be_reversed() {
    let mut world = world();
    let mut resident = resident::Resident::stage(&mut world, scene(), &material()).unwrap();
    let view = View {
        camera: Vec3::ZERO,
        visibility: Visibility::Visible,
    };
    resident.select(&mut world, view).unwrap();
    let replaced = EntityId::deterministic("sample-0");
    let retained = EntityId::deterministic("sample-1");
    let retained_entity = resident
        .iter_entities()
        .find(|&entity| world.get::<ImportedPlant>(entity).unwrap().id == retained)
        .unwrap();
    let removed = resident
        .select_excluding(&mut world, view, &[replaced].into())
        .unwrap();
    assert_eq!((removed.removed, removed.total), (1, 2));
    assert!(
        resident
            .iter_entities()
            .all(|entity| world.get::<ImportedPlant>(entity).unwrap().id != replaced)
    );
    assert!(
        resident
            .iter_entities()
            .any(|entity| entity == retained_entity)
    );
    let before = resident.entities();
    assert!(
        resident
            .select_excluding(
                &mut world,
                view,
                &[EntityId::deterministic("unknown")].into()
            )
            .is_err()
    );
    assert_eq!(resident.entities(), before);
    resident
        .select_excluding(&mut world, view, &std::collections::BTreeSet::new())
        .unwrap();
    assert_eq!(resident.entity_count(), 3);
    assert!(resident.is_intact(&world));

    // A staged species adapter with no real GPU receipt must preserve the
    // entire live source, including during a pending empty visibility selection.
    let vegetation = forge_project::VegetationDocument::from_json(include_str!(
        "../../../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    let catalog = forge_assets::VegetationPrototypeCatalog {
        schema: "forge.vegetation-prototype-catalog".into(),
        schema_version: 2,
        vegetation_sha256: vegetation.content_hash().unwrap(),
        asset_kit_id: "qualification".into(),
        asset_kit_revision: 1,
        bindings: vec![],
    };
    let cache = crate::VegetationPrototypeWindCache {
        selection_catalog: Some(catalog),
        ..Default::default()
    };
    let head = resident.head_hash().unwrap();
    let mut candidate = crate::ImportedTreePresentation::prepare(
        EntityId::deterministic("pending-imported-replacement"),
        head,
        &[],
        &cache,
        &vegetation,
        world.resource::<Assets<Mesh>>(),
    )
    .unwrap();
    candidate.stage_hidden(&mut world, head, &[]).unwrap();
    let before = resident.entities();
    assert!(
        !candidate
            .commit_source_replacement(&mut world, &mut resident, head, view)
            .unwrap()
    );
    assert_eq!(resident.entities(), before);
    assert!(resident.is_intact(&world));
}

#[test]
fn invalid_caller_streams_fail_before_any_asset_or_entity_is_allocated() {
    for invalid in 0..5 {
        let mut scene = scene();
        match invalid {
            0 => scene.groups[0].query_radii.pop().map(|_| ()).unwrap(),
            1 => scene.groups[0].models[0] = Mat4::ZERO,
            2 => scene.groups[0].records[0].i_pos.x = f32::NAN,
            3 => {
                scene.groups[0].identities.as_mut().unwrap()[1] =
                    EntityId::deterministic("sample-0");
            }
            _ => scene.groups[0].levels[0].description.kind = 3,
        }
        let mut world = world();
        let foreign = world.spawn_empty().id();
        let initial_entities = world.entities().len();
        assert!(resident::Resident::stage(&mut world, scene, &material()).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<WorldPlantMaterial>>().len(), 0);
        assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), 0);
        assert_eq!(world.entities().len(), initial_entities);
    }
}
