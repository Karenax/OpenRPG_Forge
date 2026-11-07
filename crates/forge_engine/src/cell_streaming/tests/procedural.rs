use super::*;
use bevy_rapier3d::parry::{
    math::Vector,
    query::{Ray, RayCast},
};

fn fixture() -> (Arc<TerrainGenerationPlan>, CellStreamingPolicy) {
    let document = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let terrain = Arc::new(TerrainGenerationPlan::from_document(&document).unwrap());
    let policy = CellStreamingPolicy {
        origin_xz_m: [-256.0; 2],
        cell_size_m: 64.0,
        dimensions: [8; 2],
        detail_radius: 0,
        far_radius: 2,
        max_resident_cells: 25,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 100_000,
        max_resident_bytes: 8_000_000,
        max_staging_bytes: 16_000_000,
    };
    (terrain, policy)
}

fn app() -> (App, Arc<TerrainGenerationPlan>) {
    let (terrain, policy) = fixture();
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, TransformPlugin))
        .insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
            std::time::Duration::from_secs_f64(1.0 / 60.0),
        ))
        .insert_resource(Assets::<Mesh>::default())
        .insert_resource(Assets::<StandardMaterial>::default())
        .add_plugins(
            ProceduralCellStreamingPlugin::new(
                terrain.clone(),
                policy,
                EntityId::deterministic("procedural-engine-test"),
                StandardMaterial::default(),
                [-32.0; 2],
            )
            .unwrap()
            .with_terrain_hlod(),
        );
    crate::sampled_terrain_physics::register(&mut app);
    (app, terrain)
}

#[test]
fn streamed_heightfield_matches_original_rapier_shape_and_rejects_corruption() {
    let (terrain, policy) = fixture();
    let source = ProceduralCellSource::new(terrain.clone(), policy, 8).unwrap();
    let key = CellKey { x: 3, z: 3 };
    let cell = source.build(key, CellDetail::Full).unwrap();
    let generated = terrain.generate().unwrap();
    let chunk = &generated.chunks[usize::from(key.z * 8 + key.x)];
    let expected = crate::build_terrain_heightfield_collider(chunk).unwrap();
    let (actual, center, marker) = crate::physics::build_procedural_heightfield(&cell).unwrap();
    assert_eq!(marker.id, chunk.id);
    assert_eq!(marker.coordinate, chunk.coordinate);
    assert_eq!(center, Vec3::new(-32.0, 0.0, -32.0));
    let original = expected.as_heightfield().unwrap();
    let streamed = actual.as_heightfield().unwrap();
    assert_eq!(
        original.raw.heights().nrows(),
        streamed.raw.heights().nrows()
    );
    assert_eq!(
        original.raw.heights().ncols(),
        streamed.raw.heights().ncols()
    );
    assert!(
        original
            .raw
            .heights()
            .data()
            .iter()
            .zip(streamed.raw.heights().data().iter())
            .all(|(a, b)| a.to_bits() == b.to_bits())
    );
    assert_eq!(original.raw.scale(), streamed.raw.scale());
    for (x, z) in [(-31.0, -31.0), (-1.0, 1.0), (13.0, 25.0)] {
        let ray = Ray::new(Vector::new(x, 1000.0, z), Vector::new(0.0, -1.0, 0.0));
        let original_hit = original
            .raw
            .cast_local_ray(&ray, 2000.0, true)
            .expect("canonical heightfield raycast");
        let streamed_hit = streamed
            .raw
            .cast_local_ray(&ray, 2000.0, true)
            .expect("streamed heightfield raycast");
        assert_eq!(original_hit.to_bits(), streamed_hit.to_bits());
    }
    let mut corrupted = cell.clone();
    corrupted.positions_m[0][1] = f32::NAN;
    assert!(crate::physics::build_procedural_heightfield(&corrupted).is_err());
    corrupted = cell;
    corrupted.indices.swap(0, 1);
    assert!(crate::physics::build_procedural_heightfield(&corrupted).is_err());
    assert!(
        crate::physics::build_procedural_heightfield(&source.build(key, CellDetail::Hlod).unwrap())
            .is_err()
    );
}

#[test]
fn procedural_geometry_keeps_uv_and_counts_color_conversion_staging() {
    let (terrain, policy) = fixture();
    let source = ProceduralCellSource::new(terrain.clone(), policy.clone(), 8).unwrap();
    let payload = source
        .build(CellKey { x: 3, z: 3 }, CellDetail::Full)
        .unwrap();
    let expected_uvs = payload.uvs.clone();
    let expected_colors: Vec<_> = payload
        .material_weights
        .iter()
        .map(|weights| [weights.grass, weights.earth, weights.rock, 1.0])
        .collect();
    let rgb_bytes = (payload.material_weights.capacity()
        * size_of::<forge_world::TerrainMaterialWeights>()) as u64;
    let original_bytes = payload.acknowledgement.bytes;
    let authored = procedural_geometry(payload.clone(), &policy, true).unwrap();
    let uniform = procedural_geometry(payload, &policy, false).unwrap();
    let color_bytes = match authored.mesh.attribute(Mesh::ATTRIBUTE_COLOR) {
        Some(bevy::mesh::VertexAttributeValues::Float32x4(colors)) => {
            (colors.capacity() * size_of::<[f32; 4]>()) as u64
        }
        _ => panic!("procedural terrain colors are absent"),
    };
    assert_eq!(
        authored.mesh.attribute(Mesh::ATTRIBUTE_UV_0),
        Some(&bevy::mesh::VertexAttributeValues::Float32x2(expected_uvs))
    );
    assert_eq!(
        authored.mesh.attribute(Mesh::ATTRIBUTE_COLOR),
        Some(&bevy::mesh::VertexAttributeValues::Float32x4(
            expected_colors
        ))
    );
    assert_eq!(authored.staging_bytes, original_bytes + color_bytes);
    assert_eq!(
        authored.acknowledgement.bytes,
        original_bytes + color_bytes - rgb_bytes
    );
    assert_eq!(uniform.acknowledgement.bytes, original_bytes - rgb_bytes);
    assert_eq!(uniform.staging_bytes, original_bytes);
    assert!(uniform.mesh.attribute(Mesh::ATTRIBUTE_COLOR).is_none());
    let mut undersized = policy;
    undersized.full_cell_bytes = 89_915;
    assert!(
        ProceduralCellStreamingPlugin::new(
            terrain,
            undersized,
            EntityId::deterministic("undersized"),
            StandardMaterial::default(),
            [-32.0; 2]
        )
        .is_err()
    );
}

#[test]
fn procedural_streaming_shares_selection_hlod_and_removes_old_colliders() {
    let (mut app, terrain) = app();
    wait_for(&mut app, 1);
    for _ in 0..5 {
        app.update();
    }
    let state = app.world().resource::<State>();
    let selection = render_selection(app.world()).unwrap();
    assert_eq!(selection.source, terrain.document_hash());
    assert_eq!(selection.cells.len(), 25);
    assert_eq!(
        selection.distant.unwrap().covered_cells() + selection.cells.len(),
        64
    );
    let full = state
        .live
        .values()
        .find(|live| live.collider.is_some())
        .unwrap();
    let collider = full.collider_entity.unwrap();
    assert_ne!(collider, full.entity);
    assert!(full.is_intact(app.world()));
    assert!(
        app.world()
            .get::<Collider>(collider)
            .unwrap()
            .as_heightfield()
            .is_some()
    );
    assert!(
        app.world()
            .get::<crate::TerrainHeightfieldCollider>(collider)
            .is_some()
    );
    assert!(crate::physics::static_collider_ready(app.world(), collider));
    assert!(source_is_synchronized(app.world(), terrain.document_hash()));
    let old: Vec<_> = state
        .live
        .values()
        .map(|live| (live.entity, live.collider_entity, live.mesh.id()))
        .collect();
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [224.0; 2];
    wait_for(&mut app, 2);
    for (entity, collider, mesh) in old {
        if app.world().get_entity(entity).is_err() {
            if let Some(collider) = collider {
                assert!(app.world().get_entity(collider).is_err());
            }
            assert!(app.world().resource::<Assets<Mesh>>().get(mesh).is_none());
        }
    }
    let state = app.world().resource::<State>();
    assert!(state.live.values().all(|live| live.is_intact(app.world())));
    assert_eq!(
        state
            .hlod
            .as_ref()
            .unwrap()
            .layout()
            .unwrap()
            .covered_cells()
            + state.live.len(),
        64
    );
}

#[test]
fn quality_replacement_keeps_document_identity_and_changes_both_topologies_atomically() {
    let (mut app, terrain) = app();
    wait_for(&mut app, 1);
    let old = app.world().resource::<State>().source.residency_source();
    let quality = SampledMeshQuality::new(2, 4).unwrap();
    request_mesh_quality(app.world_mut(), terrain.document_hash(), quality).unwrap();
    assert_eq!(
        admitted_mesh_quality(app.world()),
        Some(SampledMeshQuality::new(8, 8).unwrap())
    );
    wait_for(&mut app, 2);
    let state = app.world().resource::<State>();
    assert_eq!(state.source.document_hash(), terrain.document_hash());
    assert_ne!(state.source.residency_source(), old);
    assert_eq!(state.source.mesh_quality(), quality);
    assert!(state.hlod.as_ref().unwrap().layout().is_some());
    assert!(state.live.values().all(|live| live.is_intact(app.world())));
}

#[test]
fn removing_a_procedural_heightfield_holds_the_player_pipeline() {
    let (mut app, terrain) = app();
    let profile = forge_world::embedded_visual_valley_player_controller_profile().unwrap();
    let height = terrain.surface_height_at(-32.0, -32.0).unwrap();
    let spawn = Vec3::new(-32.0, height + 3.0, -32.0);
    app.add_plugins(
        crate::PlayerControllerPlugin::new(profile, spawn)
            .with_interactive_input(false)
            .with_initial_camera_target(spawn + Vec3::NEG_Z),
    )
    .add_plugins(PlayerCellStreamingPlugin);
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    loop {
        app.update();
        let status = app.world().resource::<PlayerCellStreamingStatus>();
        assert!(status.error.is_none(), "{:?}", status.error);
        if status.ready {
            break;
        }
        assert!(
            std::time::Instant::now() < deadline,
            "procedural player did not become ready"
        );
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
    let collider = app
        .world()
        .resource::<State>()
        .live
        .values()
        .find_map(|cell| cell.collider_entity)
        .expect("a protected full-detail heightfield");
    app.world_mut().despawn(collider);
    app.update();
    assert!(!app.world().resource::<crate::PlayerSimulationReady>().0);
    assert!(
        app.world()
            .resource::<PlayerCellStreamingStatus>()
            .error
            .is_some()
    );
}
