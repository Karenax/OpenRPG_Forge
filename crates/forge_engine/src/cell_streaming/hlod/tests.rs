use super::*;
use crate::cell_streaming::{
    self, CellStreamingObserver, PreparedBatch, State as CellState, geometry,
};
use bevy_rapier3d::prelude::Collider;
use std::sync::Arc;

fn app() -> App {
    let mut app = cell_streaming::tests::app_with_cell_budget(64);
    app.world_mut().resource_mut::<CellState>().hlod = Some(State::default());
    app
}

#[test]
fn quality_replaces_coarse_and_distant_meshes_atomically_and_preserves_full_shapes() {
    let mut app = app();
    app.add_plugins(TransformPlugin).insert_resource(
        bevy::time::TimeUpdateStrategy::ManualDuration(std::time::Duration::from_secs_f64(
            1.0 / 60.0,
        )),
    );
    crate::sampled_terrain_physics::register(&mut app);
    wait_for(&mut app, 1);
    let original = app.world().resource::<CellState>().source.terrain().clone();
    let key = *app
        .world()
        .resource::<CellState>()
        .stream
        .resident()
        .iter()
        .find(|(_, cell)| cell.detail == CellDetail::Full)
        .unwrap()
        .0;
    let full = app
        .world()
        .resource::<CellState>()
        .source
        .build(key, CellDetail::Full)
        .unwrap();
    let mut previous = cell_streaming::admitted_mesh_quality(app.world()).unwrap();
    for (index, (coarse, distant)) in [(16, 32), (4, 8), (2, 4), (8, 8)].into_iter().enumerate() {
        let target = forge_world::cell_streaming::SampledMeshQuality::new(coarse, distant).unwrap();
        let old: Vec<_> = app
            .world()
            .resource::<CellState>()
            .live
            .values()
            .map(|live| (live.entity, live.mesh.id()))
            .collect();
        cell_streaming::request_mesh_quality(app.world_mut(), original.document_hash(), target)
            .unwrap();
        assert_eq!(
            cell_streaming::admitted_mesh_quality(app.world()),
            Some(previous)
        );
        assert!(!cell_streaming::source_is_synchronized(
            app.world(),
            original.document_hash()
        ));
        app.update();
        for (entity, mesh) in old {
            assert!(app.world().get_entity(entity).is_ok());
            assert!(app.world().resource::<Assets<Mesh>>().get(mesh).is_some());
        }
        wait_for(&mut app, index as u64 + 2);
        for _ in 0..5 {
            app.update();
        }
        assert_eq!(
            cell_streaming::admitted_mesh_quality(app.world()),
            Some(target)
        );
        assert_coverage(&app);
        assert!(cell_streaming::source_is_synchronized(
            app.world(),
            original.document_hash()
        ));
        let state = app.world().resource::<CellState>();
        assert!(Arc::ptr_eq(state.source.terrain(), &original));
        assert!(
            state
                .hlod
                .as_ref()
                .unwrap()
                .live
                .values()
                .all(|live| live.stride == distant)
        );
        let live = &state.live[&key];
        let mesh = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&live.mesh)
            .unwrap();
        assert_eq!(
            mesh.attribute(Mesh::ATTRIBUTE_POSITION),
            Some(&bevy::mesh::VertexAttributeValues::Float32x3(
                full.positions_m.clone()
            ))
        );
        let shape = app
            .world()
            .get::<Collider>(live.entity)
            .unwrap()
            .raw
            .compute_local_aabb();
        assert_eq!(shape.mins.y.to_bits(), 0.0_f32.to_bits());
        assert_eq!(shape.maxs.y.to_bits(), 0.0_f32.to_bits());
        previous = target;
    }
    let state = app.world().resource::<CellState>();
    let distant = state.hlod.as_ref().unwrap();
    let entity = distant.live.values().next().unwrap().cell.entity;
    let original_marker = *app.world().get::<StreamedTerrainHlod>(entity).unwrap();
    app.world_mut()
        .get_mut::<StreamedTerrainHlod>(entity)
        .unwrap()
        .logical_id = EntityId::deterministic("unrelated-terrain-hlod");
    assert!(!cell_streaming::source_is_synchronized(
        app.world(),
        original.document_hash()
    ));
    app.world_mut().entity_mut(entity).insert(original_marker);
    assert!(cell_streaming::source_is_synchronized(
        app.world(),
        original.document_hash()
    ));
}

fn wait_for(app: &mut App, transitions: u64) {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    loop {
        app.update();
        let metrics = app.world().resource::<cell_streaming::CellStreamingStats>();
        assert!(metrics.last_error.is_none(), "{:?}", metrics.last_error);
        if metrics.committed_transitions >= transitions && !metrics.pending {
            return;
        }
        assert!(std::time::Instant::now() < deadline);
        std::thread::sleep(std::time::Duration::from_millis(1));
    }
}

fn assert_coverage(app: &App) {
    let state = app.world().resource::<CellState>();
    let distant = state.hlod.as_ref().unwrap();
    let metrics = app.world().resource::<TerrainHlodStats>();
    assert!(is_synchronized(app.world(), state));
    assert!(metrics.tiles > 0 && metrics.aggregated_tiles > 0);
    assert_eq!(metrics.covered_base_cells + state.live.len(), 64);
    assert_eq!(
        app.world().resource::<Assets<Mesh>>().len(),
        state.live.len() + distant.live.len()
    );
    let fine_bytes = app
        .world()
        .resource::<cell_streaming::CellStreamingStats>()
        .cpu_mesh_bytes;
    assert!(fine_bytes + metrics.cpu_mesh_bytes <= state.policy.max_resident_bytes);
    assert!(metrics.peak_staging_mesh_bytes <= state.policy.max_staging_bytes);
    for live in distant.live.values() {
        assert!(app.world().get::<Collider>(live.cell.entity).is_none());
    }
}

#[test]
fn hierarchy_meshes_cover_the_map_and_unload_replaced_assets_on_round_trip() {
    let mut app = app();
    wait_for(&mut app, 1);
    assert_coverage(&app);
    let old: BTreeMap<_, _> = app
        .world()
        .resource::<CellState>()
        .hlod
        .as_ref()
        .unwrap()
        .live
        .iter()
        .map(|(&key, live)| (key, (live.tile, live.cell.entity, live.cell.mesh.id())))
        .collect();
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [64.0; 2];
    wait_for(&mut app, 2);
    assert_coverage(&app);
    let state = app.world().resource::<CellState>();
    let distant = state.hlod.as_ref().unwrap();
    let mut removed = 0;
    for (key, (tile, entity, mesh)) in &old {
        if distant.live.get(key).is_some_and(|live| live.tile == *tile) {
            assert_eq!(distant.live[key].cell.entity, *entity);
            assert_eq!(distant.live[key].cell.mesh.id(), *mesh);
        } else {
            removed += 1;
            assert!(app.world().get_entity(*entity).is_err());
            assert!(app.world().resource::<Assets<Mesh>>().get(*mesh).is_none());
        }
    }
    assert!(removed > 0);
    app.world_mut()
        .resource_mut::<CellStreamingObserver>()
        .position_xz_m = [-64.0; 2];
    wait_for(&mut app, 3);
    assert_coverage(&app);
    let state = app.world().resource::<CellState>();
    let distant = state.hlod.as_ref().unwrap();
    assert_eq!(old.len(), distant.live.len());
    let area = state.source.terrain().document().area_id;
    for (key, (tile, _, _)) in old {
        let live = &distant.live[&key];
        assert_eq!(live.tile, tile);
        assert_eq!(
            app.world()
                .get::<StreamedTerrainHlod>(live.cell.entity)
                .unwrap()
                .logical_id,
            key.entity_id(area)
        );
    }
}

#[test]
fn source_edit_replaces_the_hierarchy_and_fine_cells_together() {
    let mut app = app();
    wait_for(&mut app, 1);
    let state = app.world().resource::<CellState>();
    let original = state.source.terrain().clone();
    let old: Vec<_> = state
        .hlod
        .as_ref()
        .unwrap()
        .live
        .values()
        .map(|live| (live.cell.entity, live.cell.mesh.id()))
        .collect();
    let mut document = original.document().clone();
    document.height_sampling.base_elevation_m = 7.0;
    let next = Arc::new(original.with_document(&document).unwrap());
    cell_streaming::request_source_update(app.world_mut(), original.document_hash(), next.clone())
        .unwrap();
    wait_for(&mut app, 2);
    assert_coverage(&app);
    assert!(Arc::ptr_eq(
        app.world()
            .resource::<cell_streaming::ResidentSampledTerrain>()
            .plan(),
        &next
    ));
    let state = app.world().resource::<CellState>();
    for live in state.hlod.as_ref().unwrap().live.values() {
        assert_eq!(live.source, next.document_hash());
        let mesh = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&live.cell.mesh)
            .unwrap();
        let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            panic!("positions");
        };
        assert!(
            positions
                .iter()
                .all(|position| position[1].to_bits() == 7.0_f32.to_bits())
        );
    }
    for (entity, mesh) in old {
        assert!(app.world().get_entity(entity).is_err());
        assert!(app.world().resource::<Assets<Mesh>>().get(mesh).is_none());
    }
}

fn batch(state: &CellState, plan: &CellStreamPlan) -> PreparedBatch {
    PreparedBatch {
        cells: state
            .source
            .prepare(plan)
            .unwrap()
            .into_iter()
            .map(|payload| geometry(payload, false).unwrap())
            .collect(),
        hlod: request(state, plan)
            .unwrap()
            .map(|request| prepare(state.source.terrain(), request, false).unwrap()),
    }
}

#[test]
fn incomplete_or_stale_hlod_acknowledgements_cannot_publish_fine_cells() {
    for fault in 0..4 {
        let mut app = app();
        wait_for(&mut app, 1);
        let mut state = app.world_mut().remove_resource::<CellState>().unwrap();
        let before = state.stream.resident().clone();
        let meshes = app.world().resource::<Assets<Mesh>>().len();
        let plan = state.stream.prepare([64.0; 2]).unwrap();
        let mut prepared = batch(&state, &plan);
        let distant = prepared.hlod.as_mut().unwrap();
        match fault {
            0 => {
                distant.meshes.pop_first();
            }
            1 => {
                distant.request.source = forge_core::bytes_sha256(b"wrong source");
            }
            2 => {
                distant.meshes.first_entry().unwrap().get_mut().bytes = u64::MAX;
            }
            3 => {
                prepared.hlod = None;
            }
            _ => unreachable!(),
        }
        assert!(cell_streaming::admit(app.world_mut(), &mut state, &plan, prepared).is_err());
        assert_eq!(state.stream.resident(), &before);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), meshes);
        assert!(is_synchronized(app.world(), &state));
    }
}

#[test]
fn combined_budgets_refuse_before_preparation_and_count_old_meshes_during_admission() {
    let mut app = app();
    wait_for(&mut app, 1);
    let mut state = app.world_mut().remove_resource::<CellState>().unwrap();
    let plan = state.stream.prepare([64.0; 2]).unwrap();
    let original_limit = state.policy.max_resident_bytes;
    state.policy.max_resident_bytes = plan.estimated_resident_bytes;
    assert!(request(&state, &plan).unwrap_err().contains("budget"));
    state.policy.max_resident_bytes = original_limit;
    let before = state.stream.resident().clone();
    let prepared = batch(&state, &plan);
    let old_bytes: u64 = state.live.values().map(|cell| cell.bytes).sum::<u64>()
        + state
            .hlod
            .as_ref()
            .unwrap()
            .live
            .values()
            .map(|cell| cell.cell.bytes)
            .sum::<u64>();
    let new_resident_bytes = prepared
        .cells
        .iter()
        .map(|cell| cell.acknowledgement.bytes)
        .sum::<u64>()
        + prepared
            .hlod
            .as_ref()
            .unwrap()
            .meshes
            .values()
            .map(|cell| cell.bytes)
            .sum::<u64>();
    let new_staging_bytes = prepared
        .cells
        .iter()
        .map(|cell| cell.staging_bytes)
        .sum::<u64>()
        + prepared
            .hlod
            .as_ref()
            .unwrap()
            .meshes
            .values()
            .map(|cell| cell.staging_bytes)
            .sum::<u64>();
    // Uniform rendering discards paint buffers after preparation, but they
    // still coexist with the old meshes during the transition.
    assert!(new_staging_bytes > new_resident_bytes);
    let peak = old_bytes + new_staging_bytes;
    assert_eq!(
        validate(app.world(), &state, &plan, &prepared).unwrap(),
        peak
    );
    let previous_peak = app
        .world()
        .resource::<TerrainHlodStats>()
        .peak_staging_mesh_bytes;
    let original_staging_limit = state.policy.max_staging_bytes;
    // Counting even the retiring meshes as resident is a conservative upper
    // bound, and still leaves room below the actual conversion peak.
    let resident_upper_bound = old_bytes + new_resident_bytes;
    assert!(resident_upper_bound < peak);
    state.policy.max_resident_bytes = resident_upper_bound;
    state.policy.max_staging_bytes = peak - 1;
    state.policy.validate().unwrap();
    assert!(
        check_bytes(&state, resident_upper_bound, new_staging_bytes, 0, 0)
            .unwrap_err()
            .contains("budget")
    );
    assert_eq!(state.stream.resident(), &before);
    state.policy.max_resident_bytes = original_limit;
    state.policy.max_staging_bytes = original_staging_limit;
    cell_streaming::admit(app.world_mut(), &mut state, &plan, prepared).unwrap();
    assert_eq!(
        app.world()
            .resource::<TerrainHlodStats>()
            .peak_staging_mesh_bytes,
        previous_peak.max(peak)
    );
}

#[test]
fn altered_distant_projection_refuses_the_whole_transition() {
    for fault in 0..3 {
        let mut app = app();
        wait_for(&mut app, 1);
        let mut state = app.world_mut().remove_resource::<CellState>().unwrap();
        let before = state.stream.resident().clone();
        let plan = state.stream.prepare([64.0; 2]).unwrap();
        let prepared = batch(&state, &plan);
        let live = state.hlod.as_ref().unwrap().live.values().next().unwrap();
        let entity = live.cell.entity;
        match fault {
            0 => {
                app.world_mut()
                    .get_mut::<Transform>(entity)
                    .unwrap()
                    .translation
                    .y = 1.0;
            }
            1 => {
                app.world_mut()
                    .get_mut::<StreamedTerrainHlod>(entity)
                    .unwrap()
                    .source = forge_core::bytes_sha256(b"wrong");
            }
            2 => {
                app.world_mut()
                    .entity_mut(entity)
                    .insert(Collider::ball(1.0));
            }
            _ => unreachable!(),
        }
        assert!(!is_synchronized(app.world(), &state));
        let meshes = app.world().resource::<Assets<Mesh>>().len();
        assert!(cell_streaming::admit(app.world_mut(), &mut state, &plan, prepared).is_err());
        assert_eq!(state.stream.resident(), &before);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), meshes);
    }
}
