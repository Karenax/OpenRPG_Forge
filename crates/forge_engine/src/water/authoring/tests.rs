use super::*;

fn head() -> ZoneAuthoringHead {
    use forge_project::zone_authoring::{
        GeneratorStamp, ZoneBase, ZoneCorrections, rivers::AuthoredRiver,
    };
    let mut terrain = forge_project::authoring::new_terrain("water", "editor", "River").unwrap();
    terrain.height_sampling.base_elevation_m = 3.0;
    let mut water = forge_world::embedded_visual_valley_water_document().unwrap();
    water.project_id = terrain.project_id;
    water.area_id = terrain.area_id;
    water.river.algorithm = forge_project::RiverPathAlgorithm::PiecewiseLinearV1;
    water.river.elevation_policy = forge_project::RiverElevationPolicy::AuthoredSurfaceV1;
    water.river.control_points = [-30.0, -10.0, 10.0, 30.0]
        .map(|x| forge_project::RiverControlPoint {
            center_m: forge_core::Vec3::new(x, 2.0, 0.0),
            surface_width_m: 8.0,
            bed_depth_m: 2.0,
        })
        .to_vec();
    if let forge_project::WaterRefractionProfile::ScreenSpace { max_depth_m, .. } =
        &mut water.optics.refraction
    {
        *max_depth_m = 2.0;
    }
    ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 4,
        revision: terrain.revision,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: terrain.content_hash().unwrap(),
            },
            terrain,
            vegetation: None,
        },
        corrections: ZoneCorrections {
            rivers: Some(vec![AuthoredRiver {
                id: EntityId::deterministic("water"),
                water,
                maximum_cut_m: 8.0,
                locked: false,
            }]),
            ..Default::default()
        },
    }
}

#[test]
fn all_editor_river_materials_use_forward_rendering_without_deferred_camera() {
    let head = head();
    let batch = PreparedAuthoringWater::prepare(&head).unwrap();
    let mut world = world();
    accept_terrain(&mut world, batch.terrain());
    replace(&mut world, head.content_hash().unwrap(), batch).unwrap();
    // Inspect installed assets, not just a camera-free preparation helper.
    assert_eq!(
        world.resource::<Assets<VisualValleyWaterMaterial>>().len(),
        1
    );
    for (_, material) in world.resource::<Assets<VisualValleyWaterMaterial>>().iter() {
        assert_eq!(
            material.base.opaque_render_method,
            OpaqueRendererMethod::Forward
        );
    }
    assert_eq!(
        world
            .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
            .len(),
        1
    );
    for (_, material) in world
        .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
        .iter()
    {
        assert_eq!(
            material.base.opaque_render_method,
            OpaqueRendererMethod::Forward
        );
    }
}

#[test]
fn preparation_projects_current_revision_without_rewriting_recipe() {
    let mut head = head();
    head.revision = forge_core::Revision::new(2);
    let original = head.to_bytes().unwrap();
    let projection = forge_world::river_projection::project(&head).unwrap();
    assert_eq!(projection.terrain.revision, head.revision);
    assert_eq!(projection.waters[0].1.revision, head.revision);
    let batch = PreparedAuthoringWater::from_projection(&projection).unwrap();
    assert_eq!(batch.river_count(), 1);
    assert_eq!(batch.source_hash(), head.content_hash().unwrap());
    assert_eq!(head.to_bytes().unwrap(), original);
    // Unrelated edits and history revisions must not make unchanged water stale.
    head.revision = forge_core::Revision::new(3);
    let original = head.to_bytes().unwrap();
    assert!(PreparedAuthoringWater::prepare(&head).is_ok());
    assert_eq!(head.to_bytes().unwrap(), original);
}

fn accept_terrain(world: &mut World, terrain: &TerrainDocument) {
    let generated = forge_world::TerrainGenerationPlan::from_document(terrain)
        .unwrap()
        .generate()
        .unwrap();
    world.insert_resource(crate::TerrainRuntimeState::new(terrain.clone(), generated));
}

fn world() -> World {
    let mut world = World::new();
    world.init_resource::<AuthoringWaterState>();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<VisualValleyWaterMaterial>>();
    world.init_resource::<Assets<crate::VisualValleyTerrainMaterial>>();
    world.init_resource::<Assets<Image>>();
    world
}

#[test]
fn clear_after_session_disposal_needs_no_terrain_and_is_idempotent() {
    let head = head();
    let mut world = world();
    let unrelated = world.spawn(Name::new("unrelated")).id();
    let batch = PreparedAuthoringWater::prepare(&head).unwrap();
    accept_terrain(&mut world, batch.terrain());
    replace(&mut world, head.content_hash().unwrap(), batch).unwrap();
    world.remove_resource::<crate::TerrainRuntimeState>();
    clear(&mut world);
    clear(&mut world);
    assert!(
        world
            .resource::<AuthoringWaterState>()
            .source_hash
            .is_none()
    );
    assert!(world.resource::<AuthoringWaterState>().owned.is_empty());
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
    assert_eq!(
        world.resource::<Assets<VisualValleyWaterMaterial>>().len(),
        0
    );
    assert_eq!(
        world
            .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
            .len(),
        0
    );
    assert_eq!(world.resource::<Assets<Image>>().len(), 0);
    assert!(world.get_entity(unrelated).is_ok());
    clear(&mut World::new());
}

#[test]
fn replacement_and_removal_retire_only_owned_resources() {
    let mut head = head();
    let source = head.to_bytes().unwrap();
    let mut world = world();
    let unrelated = world.spawn(Name::new("unrelated")).id();
    for _ in 0..2 {
        let batch = PreparedAuthoringWater::prepare(&head).unwrap();
        assert_eq!(batch.river_count(), 1);
        accept_terrain(&mut world, batch.terrain());
        replace(&mut world, head.content_hash().unwrap(), batch).unwrap();
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 2);
        assert_eq!(
            world.resource::<Assets<VisualValleyWaterMaterial>>().len(),
            1
        );
        assert_eq!(
            world
                .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
                .len(),
            1
        );
        assert_eq!(world.resource::<Assets<Image>>().len(), 1);
        assert_eq!(world.resource::<AuthoringWaterState>().owned.len(), 1);
        assert!(world.get_entity(unrelated).is_ok());
    }
    assert_eq!(head.to_bytes().unwrap(), source);
    head.corrections.rivers = Some(vec![]);
    let empty = PreparedAuthoringWater::prepare(&head).unwrap();
    accept_terrain(&mut world, empty.terrain());
    replace(&mut world, head.content_hash().unwrap(), empty).unwrap();
    assert!(world.resource::<Assets<Mesh>>().is_empty());
    assert!(
        world
            .resource::<Assets<VisualValleyWaterMaterial>>()
            .is_empty()
    );
    assert!(
        world
            .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
            .is_empty()
    );
    assert!(world.resource::<Assets<Image>>().is_empty());
    assert!(world.get_entity(unrelated).is_ok());
}

#[test]
fn stale_or_unacknowledged_batches_leave_live_state_unchanged() {
    let head = head();
    let mut world = world();
    let batch = PreparedAuthoringWater::prepare(&head).unwrap();
    let key = batch.source_hash();
    assert!(
        replace(&mut world, key, batch)
            .unwrap_err()
            .contains("exact terrain")
    );
    let batch = PreparedAuthoringWater::prepare(&head).unwrap();
    accept_terrain(&mut world, batch.terrain());
    assert!(
        replace(&mut world, head.base.terrain.content_hash().unwrap(), batch)
            .unwrap_err()
            .contains("Stale")
    );
    assert!(world.resource::<Assets<Mesh>>().is_empty());
    assert_eq!(world.resource::<AuthoringWaterState>().source_hash(), None);
}

#[test]
fn readiness_requires_exact_identity_and_live_entities_and_assets() {
    let head = head();
    let mut world = world();
    let batch = PreparedAuthoringWater::prepare(&head).unwrap();
    let terrain = batch.terrain_hash();
    let hash = head.content_hash().unwrap();
    let ids = [head.corrections.rivers.as_ref().unwrap()[0].id];
    accept_terrain(&mut world, batch.terrain());
    assert!(!ready(&world, hash, terrain, &ids));
    replace(&mut world, hash, batch).unwrap();
    assert!(ready(&world, hash, terrain, &ids));
    assert!(!ready(&world, terrain, terrain, &ids));
    assert!(!ready(&world, hash, hash, &ids));
    assert!(!ready(
        &world,
        hash,
        terrain,
        &[EntityId::deterministic("other")]
    ));
    assert!(!ready(&world, hash, terrain, &[]));
    let entity = world.resource::<AuthoringWaterState>().owned[0].entities[0];
    world
        .entity_mut(entity)
        .insert(Transform::from_xyz(1.0, 0.0, 0.0));
    assert!(!ready(&world, hash, terrain, &ids));
    world.entity_mut(entity).insert(Transform::IDENTITY);
    assert!(ready(&world, hash, terrain, &ids));
    let mesh = world.resource::<AuthoringWaterState>().owned[0].meshes[0].clone();
    let removed = world
        .resource_mut::<Assets<Mesh>>()
        .remove(mesh.id())
        .unwrap();
    assert!(!ready(&world, hash, terrain, &ids));
    world
        .resource_mut::<Assets<Mesh>>()
        .insert(mesh.id(), removed)
        .unwrap();
    assert!(ready(&world, hash, terrain, &ids));
    world.despawn(entity);
    assert!(!ready(&world, hash, terrain, &ids));
    clear(&mut world);
    assert!(!ready(&world, hash, terrain, &ids));
}
