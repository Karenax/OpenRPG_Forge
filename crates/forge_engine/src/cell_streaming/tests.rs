use super::*;
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
};

mod procedural;
mod source_updates;

#[test]
fn shared_sampled_policy_preserves_the_grid_and_rejects_unsupported_geometry() {
    let terrain = terrain_fixture();
    let source = terrain.raster().source();
    let profile = sampled_policy(source).unwrap();
    assert_eq!(
        profile.origin_xz_m.map(f64::to_bits),
        source.origin_xz_m.map(f64::to_bits)
    );
    assert_eq!(profile.dimensions, [8; 2]);
    assert_eq!(profile.cell_size_m.to_bits(), 32.0_f64.to_bits());
    assert_eq!(
        profile.cell_at([-128.0; 2]).unwrap(),
        CellKey { x: 0, z: 0 }
    );
    assert_eq!(profile.cell_at([127.0; 2]).unwrap(), CellKey { x: 7, z: 7 });
    assert!(profile.cell_at([128.0; 2]).is_err());
    let mut invalid = source.clone();
    invalid.width = 255;
    assert!(sampled_policy(&invalid).is_err());
    invalid = source.clone();
    invalid.sample_spacing_m = f64::NAN;
    assert!(sampled_policy(&invalid).is_err());
    invalid = source.clone();
    invalid.origin_xz_m = [2_000_000.0; 2];
    assert!(sampled_policy(&invalid).is_err());
}

fn app() -> App {
    app_with_cell_budget(25)
}

#[test]
fn authored_palette_transfers_weights_to_the_mesh_while_uniform_mode_releases_them() {
    let app = app();
    let source = &app.world().resource::<State>().source;
    let payload = source
        .build(CellKey { x: 2, z: 2 }, CellDetail::Full)
        .unwrap();
    let weights = payload.material_weights.clone();
    let bytes = payload.acknowledgement.bytes;
    let paint_bytes = (payload.material_weights.capacity() * size_of::<[f32; 4]>()) as u64;
    let authored = geometry(payload.clone(), true).unwrap();
    let uniform = geometry(payload, false).unwrap();
    assert_eq!(
        authored.mesh.attribute(Mesh::ATTRIBUTE_COLOR),
        Some(&bevy::mesh::VertexAttributeValues::Float32x4(weights))
    );
    assert!(uniform.mesh.attribute(Mesh::ATTRIBUTE_COLOR).is_none());
    assert_eq!(authored.acknowledgement.bytes, bytes);
    assert_eq!(uniform.acknowledgement.bytes, bytes - paint_bytes);
    assert!(authored.collider.is_some() && uniform.collider.is_some());
}

pub(crate) fn terrain_fixture() -> Arc<SampledTerrainPlan> {
    let bytes = vec![0; 256 * 256 * 4];
    let mut document = forge_world::embedded_visual_valley_terrain_document().unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(256.0, 256.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 256,
        height: 256,
        origin_xz_m: [-128.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: 0.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: forge_core::bytes_sha256(&bytes),
    });
    Arc::new(SampledTerrainPlan::from_document(&document, &bytes).unwrap())
}

pub(crate) fn app_with_cell_budget(max_resident_cells: u16) -> App {
    let terrain = terrain_fixture();
    let policy = CellStreamingPolicy {
        origin_xz_m: [-128.0; 2],
        cell_size_m: 32.0,
        dimensions: [8; 2],
        detail_radius: 0,
        far_radius: 2,
        max_resident_cells,
        full_cell_bytes: 100_000,
        hlod_cell_bytes: 100_000,
        max_resident_bytes: if max_resident_cells > 25 {
            8_000_000
        } else {
            3_000_000
        },
        max_staging_bytes: if max_resident_cells > 25 {
            16_000_000
        } else {
            6_000_000
        },
    };
    let mut app = App::new();
    app.add_plugins(MinimalPlugins)
        .insert_resource(Assets::<Mesh>::default())
        .insert_resource(Assets::<StandardMaterial>::default())
        .add_plugins(
            SampledCellStreamingPlugin::new(
                terrain,
                policy,
                EntityId::deterministic("engine-test"),
                StandardMaterial::default(),
                [-64.0; 2],
            )
            .unwrap(),
        );
    app
}

#[test]
fn protected_cells_acquire_real_colliders_and_normalized_requests_do_not_reload() {
    let mut app = app_with_cell_budget(64);
    wait_for(&mut app, 1);
    let key = CellKey { x: 7, z: 7 };
    app.world_mut()
        .resource_mut::<CellStreamingProtection>()
        .cells = vec![key];
    wait_for(&mut app, 2);
    assert_eq!(
        app.world().resource::<CellStreamingStats>().resident_cells,
        29
    );
    let entity = app.world().resource::<State>().live[&key].entity;
    assert!(app.world().get::<Collider>(entity).is_some());
    app.world_mut()
        .resource_mut::<CellStreamingProtection>()
        .cells = vec![key, key];
    for _ in 0..3 {
        app.update();
    }
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .committed_transitions,
        2
    );
    assert_eq!(app.world().resource::<State>().live[&key].entity, entity);
    app.world_mut()
        .resource_mut::<CellStreamingProtection>()
        .cells
        .clear();
    wait_for(&mut app, 3);
    assert!(app.world().get_entity(entity).is_err());
}

#[test]
fn changed_protection_discards_in_flight_work_even_when_observer_stays_in_the_same_cell() {
    let mut app = app_with_cell_budget(64);
    app.update();
    let key = CellKey { x: 7, z: 7 };
    app.world_mut()
        .resource_mut::<CellStreamingProtection>()
        .cells = vec![key];
    wait_for(&mut app, 1);
    assert_eq!(
        app.world().resource::<CellStreamingStats>().discarded_jobs,
        1
    );
    assert!(
        app.world()
            .get::<Collider>(app.world().resource::<State>().live[&key].entity)
            .is_some()
    );
}

#[test]
fn rejected_protection_keeps_every_existing_entity_and_reports_the_budget_error() {
    let mut app = app();
    wait_for(&mut app, 1);
    let before = app.world().resource::<State>().stream.resident().clone();
    let entities: Vec<_> = app
        .world()
        .resource::<State>()
        .live
        .values()
        .map(|x| x.entity)
        .collect();
    app.world_mut()
        .resource_mut::<CellStreamingProtection>()
        .cells = vec![CellKey { x: 7, z: 7 }];
    app.update();
    assert!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .is_some()
    );
    assert_eq!(app.world().resource::<State>().stream.resident(), &before);
    assert!(
        entities
            .iter()
            .all(|&entity| app.world().get_entity(entity).is_ok())
    );
}

fn wait_for(app: &mut App, transitions: u64) {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    loop {
        app.update();
        let stats = app.world().resource::<CellStreamingStats>();
        assert!(stats.last_error.is_none(), "{:?}", stats.last_error);
        if stats.committed_transitions >= transitions && !stats.pending {
            break;
        }
        assert!(
            std::time::Instant::now() < deadline,
            "streaming task did not finish"
        );
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
}

#[test]
fn asynchronous_streaming_replaces_and_unloads_mesh_assets_and_near_colliders() {
    let mut app = app();
    wait_for(&mut app, 1);
    let old: Vec<_> = app
        .world()
        .resource::<State>()
        .live
        .values()
        .map(|live| (live.entity, live.mesh.id()))
        .collect();
    let stats = app.world().resource::<CellStreamingStats>();
    assert_eq!(stats.resident_cells, 25);
    assert_eq!(stats.detailed_cells, 1);
    assert!(stats.cpu_mesh_bytes < 1_000_000);
    for live in app.world().resource::<State>().live.values() {
        let marker = app.world().get::<StreamedTerrainCell>(live.entity).unwrap();
        assert_eq!(
            app.world().get::<Collider>(live.entity).is_some(),
            marker.detail == CellDetail::Full
        );
    }
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [96.0; 2];
    wait_for(&mut app, 2);
    let assets = app.world().resource::<Assets<Mesh>>();
    for (entity, mesh) in old {
        assert!(app.world().get_entity(entity).is_err());
        assert!(assets.get(mesh).is_none());
    }
    assert_eq!(app.world().resource::<State>().live.len(), 9);
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [-64.0; 2];
    wait_for(&mut app, 3);
    assert_eq!(app.world().resource::<State>().live.len(), 25);
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 25);
}

#[test]
fn invalid_observer_keeps_existing_world_and_retry_recovers() {
    let mut app = app();
    wait_for(&mut app, 1);
    let before = app.world().resource::<State>().stream.resident().clone();
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [f64::NAN, 0.0];
    app.update();
    assert!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .is_some()
    );
    assert_eq!(app.world().resource::<State>().stream.resident(), &before);
    let mut observer = app.world_mut().resource_mut::<CellStreamingObserver>();
    observer.position_xz_m = [-64.0; 2];
    observer.retry_sequence = 1;
    app.update();
    assert!(
        app.world()
            .resource::<CellStreamingStats>()
            .last_error
            .is_none()
    );
    assert_eq!(app.world().resource::<State>().stream.resident(), &before);
}

#[test]
fn removed_live_projection_prevents_partial_commit() {
    let mut app = app();
    wait_for(&mut app, 1);
    let mut state = app.world_mut().remove_resource::<State>().unwrap();
    let plan = state.stream.prepare([96.0; 2]).unwrap();
    let prepared = state
        .source
        .prepare(&plan)
        .unwrap()
        .into_iter()
        .map(|payload| geometry(payload, false))
        .collect::<Result<Vec<_>, _>>()
        .unwrap();
    let before = state.stream.resident().clone();
    let entity = state.live.values().next().unwrap().entity;
    app.world_mut().despawn(entity);
    let meshes_before = app.world().resource::<Assets<Mesh>>().len();
    assert!(
        admit(
            app.world_mut(),
            &mut state,
            &plan,
            PreparedBatch {
                cells: prepared,
                hlod: None
            }
        )
        .is_err()
    );
    assert_eq!(state.stream.resident(), &before);
    assert_eq!(app.world().resource::<Assets<Mesh>>().len(), meshes_before);
    assert_eq!(
        app.world()
            .resource::<CellStreamingStats>()
            .committed_transitions,
        1
    );
}

#[test]
fn superseded_background_job_is_discarded_before_publishing() {
    let mut app = app();
    app.update(); // first background task owns the initial request.
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [96.0; 2];
    wait_for(&mut app, 1);
    let stats = app.world().resource::<CellStreamingStats>();
    assert_eq!(stats.discarded_jobs, 1);
    assert_eq!(stats.resident_cells, 9);
    assert_eq!(stats.committed_transitions, 1);
}

#[test]
fn terrain_retry_changes_only_one_failed_request_and_rejects_stale_clicks() {
    let mut app = app();
    wait_for(&mut app, 1);
    let world = app.world_mut();
    let resident = world.resource::<State>().stream.resident().clone();
    let protection = world.resource::<CellStreamingProtection>().cells.clone();
    let original = *world.resource::<CellStreamingObserver>();
    assert_eq!(
        terrain_retry_availability(world),
        TerrainRetryAvailability::NoFailure
    );
    assert!(!retry_failed_terrain(world));
    world.resource_mut::<CellStreamingStats>().last_error = Some("controlled failure".into());
    assert_eq!(
        terrain_retry_availability(world),
        TerrainRetryAvailability::Retryable
    );
    assert!(retry_failed_terrain(world));
    assert_eq!(
        world.resource::<CellStreamingObserver>().retry_sequence,
        original.retry_sequence + 1
    );
    assert_eq!(
        world
            .resource::<CellStreamingObserver>()
            .position_xz_m
            .map(f64::to_bits),
        original.position_xz_m.map(f64::to_bits)
    );
    assert_eq!(world.resource::<State>().stream.resident(), &resident);
    assert_eq!(
        world.resource::<CellStreamingProtection>().cells,
        protection
    );
    assert_eq!(
        world.resource::<CellStreamingStats>().last_error.as_deref(),
        Some("controlled failure")
    );
    assert!(!retry_failed_terrain(world));
    assert_eq!(
        world.resource::<CellStreamingObserver>().retry_sequence,
        original.retry_sequence + 1
    );
    world.resource_mut::<CellStreamingObserver>().retry_sequence = u64::MAX;
    let current = requested(world, world.resource::<State>()).unwrap();
    world.resource_mut::<State>().last_request = Some(current);
    assert_eq!(
        terrain_retry_availability(world),
        TerrainRetryAvailability::Unavailable
    );
    assert!(!retry_failed_terrain(world));
}

#[test]
fn terrain_retry_never_overrides_a_new_or_pending_source_request() {
    let mut app = app();
    wait_for(&mut app, 1);
    let source = app
        .world()
        .resource::<ResidentSampledTerrain>()
        .plan()
        .clone();
    let mut document = source.document().clone();
    document.name.push_str(" / queued source");
    let next = Arc::new(source.with_document(&document).unwrap());
    app.world_mut()
        .resource_mut::<CellStreamingStats>()
        .last_error = Some("controlled failure".into());
    request_source_update(app.world_mut(), source.document_hash(), next.clone()).unwrap();
    let sequence = app
        .world()
        .resource::<CellStreamingObserver>()
        .retry_sequence;
    assert_eq!(
        terrain_retry_availability(app.world()),
        TerrainRetryAvailability::Unavailable
    );
    assert!(!retry_failed_terrain(app.world_mut()));
    assert_eq!(
        app.world()
            .resource::<CellStreamingObserver>()
            .retry_sequence,
        sequence
    );
    assert_eq!(
        app.world()
            .resource::<State>()
            .requested_source
            .as_ref()
            .unwrap()
            .document_hash(),
        next.document_hash()
    );
    app.update();
    assert!(app.world().resource::<State>().pending.is_some());
    assert_eq!(
        terrain_retry_availability(app.world()),
        TerrainRetryAvailability::Waiting
    );
    assert!(!retry_failed_terrain(app.world_mut()));
}
