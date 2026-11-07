use super::*;

fn edited(source: &SampledTerrainPlan, elevation: f32) -> Arc<SampledTerrainPlan> {
    let mut document = source.document().clone();
    document.height_sampling.base_elevation_m = elevation;
    let bytes: Vec<_> = source
        .raster()
        .heights()
        .iter()
        .flat_map(|height| height.to_le_bytes())
        .collect();
    Arc::new(SampledTerrainPlan::from_document(&document, &bytes).unwrap())
}

fn wait_synchronized(app: &mut App, hash: ContentHash) {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    while !source_is_synchronized(app.world(), hash) {
        app.update();
        assert!(
            std::time::Instant::now() < deadline,
            "Source never synchronized"
        );
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
}

#[test]
fn quality_requests_reject_wrong_heads_keep_edits_and_discard_replaced_work() {
    let mut app = app();
    wait_for(&mut app, 1);
    let original = app.world().resource::<State>().source.terrain().clone();
    let hash = original.document_hash();
    let target = SampledMeshQuality::new(2, 4).unwrap();
    let coarse = SampledMeshQuality::new(16, 32).unwrap();
    assert!(
        request_mesh_quality(app.world_mut(), forge_core::bytes_sha256(b"wrong"), target).is_err()
    );
    assert!(app.world().resource::<State>().requested_source.is_none());
    request_mesh_quality(app.world_mut(), hash, coarse).unwrap();
    app.update();
    request_mesh_quality(app.world_mut(), hash, target).unwrap();
    wait_for(&mut app, 2);
    assert_eq!(admitted_mesh_quality(app.world()), Some(target));
    assert!(app.world().resource::<CellStreamingStats>().discarded_jobs >= 1);
    let next = edited(&original, 3.0);
    request_source_update(app.world_mut(), hash, next.clone()).unwrap();
    let before = app
        .world()
        .resource::<State>()
        .requested_source
        .as_ref()
        .unwrap()
        .terrain()
        .document_hash();
    assert!(request_mesh_quality(app.world_mut(), hash, coarse).is_err());
    assert_eq!(
        app.world()
            .resource::<State>()
            .requested_source
            .as_ref()
            .unwrap()
            .terrain()
            .document_hash(),
        before
    );
    wait_for(&mut app, 3);
    assert_eq!(admitted_mesh_quality(app.world()), Some(target));
    assert_eq!(
        app.world()
            .resource::<ResidentSampledTerrain>()
            .plan()
            .document_hash(),
        next.document_hash()
    );
    request_mesh_quality(app.world_mut(), next.document_hash(), coarse).unwrap();
    app.update();
    request_mesh_quality(app.world_mut(), next.document_hash(), target).unwrap();
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    while app.world().resource::<State>().pending.is_some() {
        app.update();
        assert!(std::time::Instant::now() < deadline);
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
    assert_eq!(admitted_mesh_quality(app.world()), Some(target));
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .committed_transitions,
        3
    );
}

#[test]
fn readiness_requires_backend_shapes_and_rechecks_new_sources_and_live_identity() {
    let mut without_backend = app();
    let original = without_backend
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let hash = original.document_hash();
    assert!(!source_is_synchronized(without_backend.world(), hash));
    wait_for(&mut without_backend, 1);
    // Installed ECS colliders alone cannot authorize an editing gesture.
    assert!(!source_is_synchronized(without_backend.world(), hash));
    let mut app = app();
    app.add_plugins(TransformPlugin).insert_resource(
        bevy::time::TimeUpdateStrategy::ManualDuration(std::time::Duration::from_secs_f64(
            1.0 / 60.0,
        )),
    );
    crate::sampled_terrain_physics::register(&mut app);
    wait_synchronized(&mut app, hash);
    let replacement = edited(&original, 3.0);
    let next = replacement.document_hash();
    assert!(!source_is_synchronized(app.world(), next));
    request_source_update(app.world_mut(), hash, replacement).unwrap();
    assert!(!source_is_synchronized(app.world(), hash));
    assert!(!source_is_synchronized(app.world(), next));
    wait_for(&mut app, 2);
    // Admission in Update precedes the next FixedUpdate backend synchronization.
    assert!(!source_is_synchronized(app.world(), next));
    wait_synchronized(&mut app, next);
    assert!(!source_is_synchronized(app.world(), hash));
    let entity = app
        .world()
        .resource::<State>()
        .live
        .values()
        .next()
        .unwrap()
        .entity;
    let saved = *app.world().get::<StreamedTerrainCell>(entity).unwrap();
    app.world_mut()
        .get_mut::<StreamedTerrainCell>(entity)
        .unwrap()
        .logical_id = EntityId::deterministic("unrelated-cell");
    assert!(!source_is_synchronized(app.world(), next));
    app.world_mut().entity_mut(entity).insert(saved);
    assert!(source_is_synchronized(app.world(), next));
    app.world_mut()
        .get_mut::<StreamedTerrainCell>(entity)
        .unwrap()
        .detail = if saved.detail == CellDetail::Full {
        CellDetail::Hlod
    } else {
        CellDetail::Full
    };
    assert!(!source_is_synchronized(app.world(), next));
    app.world_mut().entity_mut(entity).insert(saved);
    assert!(source_is_synchronized(app.world(), next));
    app.world_mut()
        .get_mut::<Transform>(entity)
        .unwrap()
        .translation
        .x = 1.0;
    assert!(!source_is_synchronized(app.world(), next));
}

#[test]
fn editing_replaces_meshes_and_colliders_together_without_recreating_the_world() {
    let mut app = app();
    wait_for(&mut app, 1);
    let original = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let before: BTreeMap<_, _> = app
        .world()
        .resource::<State>()
        .live
        .iter()
        .map(|(key, live)| {
            (
                *key,
                (
                    live.entity,
                    live.mesh.id(),
                    *app.world().get::<StreamedTerrainCell>(live.entity).unwrap(),
                ),
            )
        })
        .collect();
    let unrelated = app
        .world_mut()
        .spawn(Name::new("Existing authored object"))
        .id();
    let replacement = edited(&original, 3.0);
    request_source_update(
        app.world_mut(),
        original.document_hash(),
        replacement.clone(),
    )
    .unwrap();
    app.update(); // Launches preparation; admission never happens in this same update.
    assert_eq!(
        app.world()
            .resource::<ResidentSampledTerrain>()
            .plan()
            .document_hash(),
        original.document_hash()
    );
    for (entity, mesh, _) in before.values() {
        assert!(app.world().get_entity(*entity).is_ok());
        assert!(app.world().resource::<Assets<Mesh>>().get(*mesh).is_some());
    }
    wait_for(&mut app, 2);
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &replacement
    ));
    assert!(app.world().get_entity(unrelated).is_ok());
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), before.len());
    for (key, live) in &app.world().resource::<State>().live {
        let (old_entity, old_mesh, old_marker) = before[key];
        assert!(app.world().get_entity(old_entity).is_err());
        assert!(
            app.world()
                .resource::<Assets<Mesh>>()
                .get(old_mesh)
                .is_none()
        );
        let marker = app.world().get::<StreamedTerrainCell>(live.entity).unwrap();
        assert_eq!(marker.logical_id, old_marker.logical_id);
        assert_eq!(marker.detail, old_marker.detail);
        assert_eq!(marker.source, replacement.document_hash());
        let meshes = app.world().resource::<Assets<Mesh>>();
        let mesh = meshes.get(&live.mesh).unwrap();
        let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("Expected sampled cell positions");
        };
        assert!(
            positions
                .iter()
                .all(|point| point[1].to_bits() == 3.0_f32.to_bits())
        );
        if let Some(collider) = app.world().get::<Collider>(live.entity) {
            let bounds = collider.raw.compute_local_aabb();
            assert_eq!(bounds.mins.y.to_bits(), 3.0_f32.to_bits());
            assert_eq!(bounds.maxs.y.to_bits(), 3.0_f32.to_bits());
        }
    }
    assert_eq!(
        original
            .document()
            .height_sampling
            .base_elevation_m
            .to_bits(),
        0.0_f32.to_bits()
    );
}

#[test]
fn newer_edit_discards_the_old_job_and_cancellation_keeps_the_admitted_source() {
    let mut app = app();
    wait_for(&mut app, 1);
    let original = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let intermediate = edited(&original, 2.0);
    request_source_update(app.world_mut(), original.document_hash(), intermediate).unwrap();
    app.update();
    let latest = edited(&original, 4.0);
    request_source_update(app.world_mut(), original.document_hash(), latest.clone()).unwrap();
    for _ in 0..1000 {
        app.update();
        let current = app
            .world()
            .resource::<ResidentSampledTerrain>()
            .plan()
            .document_hash();
        assert!(current == original.document_hash() || current == latest.document_hash());
        if current == latest.document_hash() {
            break;
        }
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
    assert_eq!(
        app.world()
            .resource::<ResidentSampledTerrain>()
            .plan()
            .document_hash(),
        latest.document_hash()
    );
    assert_eq!(
        app.world().resource::<CellStreamingStats>().discarded_jobs,
        1
    );
    let before = app.world().resource::<State>().stream.resident().clone();
    request_source_update(
        app.world_mut(),
        latest.document_hash(),
        edited(&latest, 8.0),
    )
    .unwrap();
    app.update();
    request_source_update(app.world_mut(), latest.document_hash(), latest.clone()).unwrap();
    for _ in 0..1000 {
        app.update();
        assert_eq!(app.world().resource::<State>().stream.resident(), &before);
        if !app.world().resource::<CellStreamingStats>().pending {
            break;
        }
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
    assert_eq!(
        app.world().resource::<CellStreamingStats>().discarded_jobs,
        2
    );
    assert!(!app.world().resource::<CellStreamingStats>().pending);
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .committed_transitions,
        2
    );
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &latest
    ));
}

#[test]
fn raster_regeneration_keeps_corner_edits_in_full_and_coarse_cells() {
    let mut app = app();
    let original = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let original_lifetime = Arc::downgrade(&original);
    let mut document = original.document().clone();
    document
        .height_deltas
        .push(forge_project::TerrainHeightDelta::new(64, 64, 2.0));
    let original_bytes = vec![0; 256 * 256 * 4];
    let corrected =
        Arc::new(SampledTerrainPlan::from_document(&document, &original_bytes).unwrap());
    request_source_update(app.world_mut(), original.document_hash(), corrected.clone()).unwrap();
    wait_for(&mut app, 1);
    drop(original);
    assert!(
        original_lifetime.upgrade().is_none(),
        "Plugin bootstrap retained the old raster"
    );
    let regenerated_bytes = 3.0_f32.to_le_bytes().repeat(256 * 256);
    let raster = document.sampled_source.as_mut().unwrap();
    raster.samples_sha256 = forge_core::bytes_sha256(&regenerated_bytes);
    raster.outside_height_m = 3.0;
    let regenerated =
        Arc::new(SampledTerrainPlan::from_document(&document, &regenerated_bytes).unwrap());
    request_source_update(
        app.world_mut(),
        corrected.document_hash(),
        regenerated.clone(),
    )
    .unwrap();
    wait_for(&mut app, 2);
    let meshes = app.world().resource::<Assets<Mesh>>();
    let mut detailed_corner = false;
    let mut coarse_corner = false;
    for live in app.world().resource::<State>().live.values() {
        let mesh = meshes.get(&live.mesh).unwrap();
        let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("Expected sampled cell positions");
        };
        for point in positions.iter().filter(|point| {
            point[0].to_bits() == (-64.0_f32).to_bits()
                && point[2].to_bits() == (-64.0_f32).to_bits()
        }) {
            assert_eq!(point[1].to_bits(), 5.0_f32.to_bits());
            match app
                .world()
                .get::<StreamedTerrainCell>(live.entity)
                .unwrap()
                .detail
            {
                CellDetail::Full => detailed_corner = true,
                CellDetail::Hlod => coarse_corner = true,
            }
        }
    }
    assert!(detailed_corner && coarse_corner);
    assert_eq!(
        regenerated.document().height_deltas,
        corrected.document().height_deltas
    );
    assert_ne!(
        regenerated.raster().source().samples_sha256,
        corrected.raster().source().samples_sha256
    );
    assert!(
        corrected
            .raster()
            .heights()
            .iter()
            .all(|height| height.to_bits() == 0.0_f32.to_bits())
    );
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &regenerated
    ));
}

#[test]
fn stale_identity_foreign_zone_or_grid_is_refused_without_touching_a_pending_edit() {
    let mut app = app();
    wait_for(&mut app, 1);
    let original = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let expected = original.document_hash();
    let next = edited(&original, 2.0);
    request_source_update(app.world_mut(), expected, next.clone()).unwrap();
    let bytes = vec![0; 256 * 256 * 4];
    for fault in 0..3 {
        let mut document = original.document().clone();
        let mut pin = expected;
        match fault {
            0 => pin = forge_core::bytes_sha256(b"stale viewport"),
            1 => document.area_id = forge_core::AreaId::deterministic("another-zone"),
            _ => {
                document.extent_m = TerrainExtent::new(512.0, 512.0);
                document.height_sampling.sample_spacing_m = 2.0;
                let source = document.sampled_source.as_mut().unwrap();
                source.sample_spacing_m = 2.0;
                source.origin_xz_m = [-256.0; 2];
            }
        }
        let candidate = Arc::new(SampledTerrainPlan::from_document(&document, &bytes).unwrap());
        assert!(request_source_update(app.world_mut(), pin, candidate).is_err());
        assert!(Arc::ptr_eq(
            app.world()
                .resource::<State>()
                .requested_source
                .as_ref()
                .unwrap()
                .terrain(),
            &next
        ));
        assert_eq!(
            app.world()
                .resource::<ResidentSampledTerrain>()
                .plan()
                .document_hash(),
            expected
        );
    }
    wait_for(&mut app, 2);
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &next
    ));
}

#[test]
fn source_replacement_counts_the_old_meshes_in_its_staging_budget() {
    let mut policy = app().world().resource::<State>().policy.clone();
    policy.max_staging_bytes = policy.max_resident_bytes;
    let source = terrain_fixture();
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .init_resource::<Assets<Mesh>>()
        .init_resource::<Assets<StandardMaterial>>()
        .add_plugins(
            SampledCellStreamingPlugin::new(
                source.clone(),
                policy,
                EntityId::deterministic("limited-editor"),
                StandardMaterial::default(),
                [-64.0; 2],
            )
            .unwrap(),
        );
    app.world_mut()
        .resource_mut::<CellStreamingProtection>()
        .cells = (1..=3)
        .flat_map(|z| (1..=3).map(move |x| CellKey { x, z }))
        .collect();
    wait_for(&mut app, 1);
    assert_eq!(
        app.world().resource::<CellStreamingStats>().detailed_cells,
        25
    );
    let before = app.world().resource::<State>().stream.resident().clone();
    let result = request_source_update(
        app.world_mut(),
        source.document_hash(),
        edited(&source, 2.0),
    );
    assert!(result.unwrap_err().contains("staging byte budget"));
    assert_eq!(app.world().resource::<State>().stream.resident(), &before);
    assert!(app.world().resource::<State>().requested_source.is_none());
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &source
    ));
}

#[test]
fn damaged_live_geometry_cannot_publish_the_new_source() {
    let mut app = app();
    wait_for(&mut app, 1);
    let source = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    request_source_update(
        app.world_mut(),
        source.document_hash(),
        edited(&source, 3.0),
    )
    .unwrap();
    app.update();
    let entity = app
        .world()
        .resource::<State>()
        .live
        .values()
        .next()
        .unwrap()
        .entity;
    app.world_mut()
        .get_mut::<Transform>(entity)
        .unwrap()
        .translation
        .x = 1.0;
    let before = app.world().resource::<State>().stream.resident().clone();
    let meshes = app.world().resource::<Assets<Mesh>>().len();
    for _ in 0..1000 {
        app.update();
        if app
            .world()
            .resource::<CellStreamingStats>()
            .last_error
            .is_some()
        {
            break;
        }
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
    assert!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .as_ref()
            .unwrap()
            .contains("removed or replaced")
    );
    assert_eq!(app.world().resource::<State>().stream.resident(), &before);
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), meshes);
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &source
    ));
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .committed_transitions,
        1
    );
}

#[test]
fn invalid_replacement_acknowledgement_restores_the_original_stream_and_source() {
    let mut app = app();
    wait_for(&mut app, 1);
    let source = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let mut state = app.world_mut().remove_resource::<State>().unwrap();
    let before = state.stream.resident().clone();
    let next = crate::cell_streaming::source_updates::replacement(
        &state,
        SampledCellSource::new(edited(&source, 5.0), state.policy.clone(), 8).unwrap(),
    )
    .unwrap();
    let plan = next.stream.prepare([-64.0; 2]).unwrap();
    let mut prepared = next
        .source
        .prepare(&plan)
        .unwrap()
        .into_iter()
        .map(|payload| geometry(payload, false))
        .collect::<Result<Vec<_>, _>>()
        .unwrap();
    prepared[0].acknowledgement.source = source.document_hash();
    let meshes = app.world().resource::<Assets<Mesh>>().len();
    assert!(
        crate::cell_streaming::source_updates::admit_job(
            app.world_mut(),
            &mut state,
            &plan,
            PreparedBatch {
                cells: prepared,
                hlod: None
            },
            Some(next)
        )
        .is_err()
    );
    assert_eq!(state.stream.resident(), &before);
    assert!(Arc::ptr_eq(state.source.terrain(), &source));
    assert!(Arc::ptr_eq(
        app.world().resource::<ResidentSampledTerrain>().plan(),
        &source
    ));
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), meshes);
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .committed_transitions,
        1
    );
}
