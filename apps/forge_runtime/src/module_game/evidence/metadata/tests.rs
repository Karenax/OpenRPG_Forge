//! In-memory trace contracts; these fixtures never claim actual GPU readiness.
use super::super::{FrameTiming, frame_times};
use super::*;
use crate::module_game::evidence_contract::GraphicsIdentity;
use forge_core::{ContentHash, EntityId};
use forge_project::VisualQualityTier;

fn graphics(frame: u32) -> GraphicsObservation {
    GraphicsObservation {
        source_terrain: None,
        identity: GraphicsIdentity {
            head: ContentHash::from_bytes([1; 32]),
            policy: ContentHash::from_bytes([2; 32]),
            water: false,
        },
        requested: VisualQualityTier::High,
        applied: Some(VisualQualityTier::High),
        persisted: Some(VisualQualityTier::High),
        request_sequence: 1,
        generation: 1,
        application_frame: 10,
        cpu_ready: true,
        ready: true,
        error: None,
        preference_error: None,
        scopes: ["terrain", "models"]
            .map(|name| GraphicsScope {
                name: name.into(),
                owner: Some(EntityId::deterministic(name)),
                identity: Some(ContentHash::from_bytes([3; 32])),
                generation: Some(1),
                epoch: 1,
                observed: frame - 1,
                completed: Some(frame - 2),
                required_frame: None,
                publication: None,
                token_current: true,
                meshes: [271, 271],
                materials: [4, 4],
                images: [8, 8],
                buffers: [3, 3],
                pipelines: [71, 0, 0],
                effect_groups: [6, 6],
                effects_ready: true,
                camera_ready: true,
            })
            .to_vec(),
        plant_completed_frame: Some(frame - 2),
        source_surfaces: None,
    }
}

#[test]
fn fences_are_not_relabelled_when_metadata_is_referenced_by_later_intervals() {
    let mut metadata = Metadata::default();
    assert_eq!(
        metadata.observe(100, Some(&graphics(100)), None),
        (Some(0), None)
    );
    assert_eq!(
        metadata.observe(159, Some(&graphics(159)), None),
        (Some(0), None)
    );
    assert_eq!(metadata.graphics[0].frame, 100);
    assert_eq!(
        metadata.graphics[0].observation.scopes[0].completed,
        Some(98)
    );
    assert_eq!(
        metadata.observe(160, Some(&graphics(160)), None),
        (Some(1), None)
    );
    assert_eq!(metadata.graphics[1].frame, 160);
    assert_eq!(
        metadata.graphics[1].observation.scopes[0].completed,
        Some(158)
    );
}

#[test]
fn revocation_epoch_dependencies_errors_and_disappearance_force_dated_snapshots() {
    for change in 0..9 {
        let mut metadata = Metadata::default();
        metadata.observe(100, Some(&graphics(100)), None);
        let mut next = graphics(101);
        match change {
            0 => next.ready = false,
            1 => next.scopes[0].epoch = 2,
            2 => next.scopes[0].token_current = false,
            3 => next.scopes[0].images[1] = 7,
            4 => next.scopes[0].effects_ready = false,
            5 => next.preference_error = Some("write refused".into()),
            6 => next.error = Some("render refused".into()),
            7 => next.request_sequence = 2,
            _ => next.generation = 2,
        }
        assert_eq!(metadata.observe(101, Some(&next), None).0, Some(1));
        assert_eq!(metadata.graphics[1].observation, next);
        assert_eq!(metadata.observe(102, None, None).0, None);
        assert_eq!(metadata.observe(103, Some(&next), None).0, Some(2));
        assert_eq!(metadata.graphics[2].frame, 103);
    }
}

#[test]
fn publication_and_source_terrain_changes_keep_their_real_observation_frame() {
    use crate::module_game::evidence_contract::graphics::{
        GraphicsPublication, SourceTerrainObservation,
    };
    let mut metadata = Metadata::default();
    let mut current = graphics(100);
    current.scopes[0].publication = Some(GraphicsPublication {
        frame: 98,
        sources: ContentHash::from_bytes([4; 32]),
        seal: ContentHash::from_bytes([5; 32]),
    });
    current.source_terrain = Some(SourceTerrainObservation {
        document: None,
        package: None,
        pending: false,
        active_tiles: 10,
        cached_tiles: 11,
        normals_ready: true,
        staged_frame: Some(10),
        ready: true,
        error: None,
    });
    assert_eq!(metadata.observe(100, Some(&current), None).0, Some(0));
    current.scopes[0].publication.as_mut().unwrap().frame = 101;
    assert_eq!(metadata.observe(101, Some(&current), None).0, Some(1));
    current.source_terrain.as_mut().unwrap().cached_tiles = 12;
    assert_eq!(metadata.observe(102, Some(&current), None).0, Some(2));
    assert_eq!(
        metadata
            .graphics
            .iter()
            .map(|item| item.frame)
            .collect::<Vec<_>>(),
        vec![100, 101, 102]
    );
}

#[test]
fn metadata_quota_never_reuses_a_different_observation_as_current() {
    let mut metadata = Metadata::default();
    let mut current = graphics(100);
    for index in 0..MAX_SNAPSHOTS {
        current.request_sequence = u64::try_from(index).unwrap();
        assert_eq!(
            metadata.observe(100, Some(&current), None).0,
            Some(u16::try_from(index).unwrap())
        );
    }
    current.request_sequence += 1;
    assert_eq!(metadata.observe(101, Some(&current), None).0, None);
    assert!(metadata.truncated);
    assert_eq!(metadata.graphics.len(), MAX_SNAPSHOTS);
    let bytes =
        frame_times::encode(&[], false, ContentHash::from_bytes([1; 32]), &metadata).unwrap();
    let file: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(file["truncated"], true);
}

#[test]
fn twenty_thousand_intervals_fit_the_existing_byte_quota_without_losing_rows() {
    let mut metadata = Metadata::default();
    let mut samples = Vec::new();
    let mut plants = HostStatus {
        ready: true,
        detail: 12,
        selected_triangles: 65_536,
        ..HostStatus::default()
    };
    for index in 0..20_000 {
        let frame = index + 100;
        plants.blocked_ticks = u64::from(index);
        let (graphics, imported) = metadata.observe(frame, Some(&graphics(frame)), Some(&plants));
        samples.push(FrameTiming {
            graphics,
            imported,
            frame,
            tick: u64::from(index),
            frame_ms: 16.123_456_789_123_456,
            player_position: [256.123_45, 1.234_567, -256.123_45],
            camera: [1.234_567, 0.012_345, 8.765_432, 1.123_456],
            selected_triangles: Some(123_456),
            rendered_batches: Some(63),
            generation: Some(7),
            pending: Some(false),
            projection_ready: true,
            simulation_ready: Some(true),
        });
    }
    let bytes =
        frame_times::encode(&samples, false, ContentHash::from_bytes([1; 32]), &metadata).unwrap();
    assert!(bytes.len() < 8 * 1024 * 1024);
    let file: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(file["schema"], "forge.native-main-frame-intervals.v2");
    assert_eq!(file["samples"].as_array().unwrap().len(), 20_000);
    assert_eq!(metadata.graphics.len(), 334);
    assert_eq!(metadata.imported.len(), 334);
    assert!(!metadata.truncated);
    for (expected, actual) in samples.iter().zip(file["samples"].as_array().unwrap()) {
        let decoded: FrameTiming = serde_json::from_value(actual.clone()).unwrap();
        assert_eq!(expected, &decoded);
        if expected.frame == 100 {
            let mut legacy = actual.clone();
            legacy.as_object_mut().unwrap().remove("simulation_ready");
            assert_eq!(
                serde_json::from_value::<FrameTiming>(legacy)
                    .unwrap()
                    .simulation_ready,
                None
            );
        }
        assert_eq!(expected.frame_ms.to_bits(), decoded.frame_ms.to_bits());
        for (a, b) in expected
            .player_position
            .iter()
            .zip(&decoded.player_position)
            .chain(expected.camera.iter().zip(&decoded.camera))
        {
            assert_eq!(a.to_bits(), b.to_bits());
        }
        let index = usize::try_from(actual["graphics"].as_u64().unwrap()).unwrap();
        assert!(
            file["graphics_observations"][index]["frame"]
                .as_u64()
                .unwrap()
                <= u64::from(expected.frame)
        );
    }
}

#[test]
fn frame_without_static_hlod_preserves_absence_in_version_three() {
    let sample = FrameTiming {
        graphics: None,
        imported: None,
        frame: 10,
        tick: 5,
        frame_ms: 16.0,
        player_position: [0.0; 3],
        camera: [0.0; 4],
        selected_triangles: None,
        rendered_batches: None,
        generation: None,
        pending: None,
        projection_ready: true,
        simulation_ready: Some(true),
    };
    let bytes = frame_times::encode(
        std::slice::from_ref(&sample),
        false,
        ContentHash::from_bytes([1; 32]),
        &Metadata::default(),
    )
    .unwrap();
    let file: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(file["schema"], "forge.native-main-frame-intervals.v3");
    let row = &file["samples"][0];
    for key in [
        "selected_triangles",
        "rendered_batches",
        "generation",
        "pending",
    ] {
        assert!(row.get(key).is_none());
    }
    let decoded: FrameTiming = serde_json::from_value(row.clone()).unwrap();
    assert_eq!(decoded, sample);
}

#[test]
fn many_semantic_transitions_fit_the_expanded_bounded_trace_without_redating() {
    let mut metadata = Metadata::default();
    let mut samples = Vec::new();
    for index in 0..20_000_u32 {
        let frame = index + 100;
        let mut observation = graphics(frame);
        observation.request_sequence = u64::from(index / 5);
        observation.preference_error = Some("diagnostic".repeat(100));
        let (graphics, imported) = metadata.observe(frame, Some(&observation), None);
        samples.push(FrameTiming {
            graphics,
            imported,
            frame,
            tick: u64::from(index),
            frame_ms: 16.5,
            player_position: [0.0; 3],
            camera: [0.0; 4],
            selected_triangles: Some(1),
            rendered_batches: Some(1),
            generation: Some(1),
            pending: Some(false),
            projection_ready: true,
            simulation_ready: Some(true),
        });
    }
    assert_eq!(metadata.graphics.len(), 4000);
    assert!(!metadata.truncated);
    let bytes =
        frame_times::encode(&samples, false, ContentHash::from_bytes([1; 32]), &metadata).unwrap();
    assert!(bytes.len() > 8 * 1024 * 1024 && bytes.len() < 32 * 1024 * 1024);
    let file: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(file["truncated"], false);
    assert_eq!(file["samples"].as_array().unwrap().len(), 20_000);
    let last = file["samples"].as_array().unwrap().last().unwrap();
    let reference = usize::try_from(last["graphics"].as_u64().unwrap()).unwrap();
    assert_eq!(file["graphics_observations"][reference]["frame"], 20_095);
}

#[test]
fn changed_canonical_or_render_counts_require_their_own_dated_observation() {
    let mut metadata = Metadata::default();
    let mut plants = HostStatus {
        canonical_instances: 100,
        entities: 20,
        ..HostStatus::default()
    };
    assert_eq!(metadata.observe(10, None, Some(&plants)).1, Some(0));
    plants.canonical_instances = 99;
    assert_eq!(metadata.observe(11, None, Some(&plants)).1, Some(1));
    plants.entities = 21;
    assert_eq!(metadata.observe(12, None, Some(&plants)).1, Some(2));
    assert_eq!(metadata.imported[0].observation.canonical_instances, 100);
    assert_eq!(metadata.imported[1].observation.entities, 20);
    assert_eq!(metadata.imported[2].observation.entities, 21);
    plants.canonical_colliders = 42;
    assert_eq!(metadata.observe(13, None, Some(&plants)).1, Some(3));
    plants.colliders = 3;
    assert_eq!(metadata.observe(14, None, Some(&plants)).1, Some(4));
    assert_eq!(metadata.imported[3].observation.canonical_colliders, 42);
    assert_eq!(metadata.imported[4].observation.colliders, 3);
    plants.replacement_primitives = 17;
    assert_eq!(metadata.observe(15, None, Some(&plants)).1, Some(5));
    assert_eq!(metadata.imported[5].frame, 15);
    assert_eq!(metadata.imported[5].observation.replacement_primitives, 17);
}

#[test]
fn whole_scene_admission_captures_fresh_fences_without_redating_old_snapshots() {
    let mut metadata = Metadata::default();
    metadata.projection_boundary(false);
    assert_eq!(metadata.observe(100, Some(&graphics(100)), None).0, Some(0));
    metadata.projection_boundary(true);
    assert_eq!(metadata.observe(101, Some(&graphics(101)), None).0, Some(1));
    assert_eq!(metadata.graphics[0].frame, 100);
    assert_eq!(
        metadata.graphics[0].observation.scopes[0].completed,
        Some(98)
    );
    assert_eq!(metadata.graphics[1].frame, 101);
    assert_eq!(
        metadata.graphics[1].observation.scopes[0].completed,
        Some(99)
    );
    metadata.projection_boundary(true);
    assert_eq!(metadata.observe(102, Some(&graphics(102)), None).0, Some(1));
    metadata.projection_boundary(false);
    metadata.projection_boundary(true);
    assert_eq!(metadata.observe(103, Some(&graphics(103)), None).0, Some(2));
    assert_eq!(metadata.graphics[2].frame, 103);
    assert!(!metadata.truncated);
}

#[test]
fn common_surface_revocation_is_a_dated_boundary_without_any_plant_metadata() {
    let mut metadata = Metadata::default();
    let mut current = graphics(100);
    metadata.observe(100, Some(&current), None);
    current.source_surfaces = Some(crate::module_game::evidence_contract::SurfaceStatus {
        active: true,
        ready: true,
        generation: 1,
        plant_batches: 0,
        ..Default::default()
    });
    assert_eq!(metadata.observe(101, Some(&current), None), (Some(1), None));
    current.source_surfaces.as_mut().unwrap().generation = 2;
    assert_eq!(metadata.observe(102, Some(&current), None), (Some(2), None));
    current.source_surfaces.as_mut().unwrap().ready = false;
    assert_eq!(metadata.observe(103, Some(&current), None), (Some(3), None));
    assert!(metadata.imported.is_empty());
}
