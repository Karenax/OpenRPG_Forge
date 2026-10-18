#[path = "../../../crates/forge_tidewater/examples/module_game/evidence_contract.rs"]
mod evidence;
use evidence::*;
use forge_core::{AreaId, AssetId, EntityId, ModuleId, Transform, bytes_sha256};
use forge_project::module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin};

fn fixture() -> (ExpectedProjection, ProjectionObservation) {
    let module_id = ModuleId::deterministic("evidence/module");
    let area_id = AreaId::deterministic("evidence/area");
    let source_hash = bytes_sha256(b"canonical zone");
    let functional_hash = bytes_sha256(b"unchanged gameplay");
    let id = EntityId::deterministic("evidence/keeper");
    let asset = VisualAssetPin {
        asset_id: AssetId::deterministic("evidence/blue"),
        sha256: bytes_sha256(b"blue model"),
    };
    let manual = ExpectedManual {
        entity_id: EntityId::deterministic("evidence/cairn"),
        asset,
        transform: Transform::IDENTITY,
    };
    let expected = ExpectedProjection {
        module_id,
        area_id,
        source_hash,
        functional_hash,
        visuals: vec![GameplayVisualBinding {
            area_id,
            entity_id: id,
            asset,
            local_transform: Transform::IDENTITY,
        }],
        manual: vec![manual.clone()],
    };
    let observed = ProjectionObservation {
        pid: 42,
        frame: 300,
        module_id: Some(module_id),
        area_id: Some(area_id),
        source_hash: Some(source_hash),
        functional_hash: Some(functional_hash),
        gameplay_ready: true,
        tick: 120,
        simulation_error: None,
        visual_error: None,
        visual_expected: 1,
        visual_spawned: 1,
        visual_ready: 1,
        manual_projection_ready: true,
        visuals: vec![ObservedVisual {
            entity_id: id,
            asset,
            transform: Transform::IDENTITY,
            parent_identity: Some(id),
            parent_is_gameplay_body: true,
            parent_has_collider: true,
            scene_ready: true,
            mesh_primitives: 2,
        }],
        manual: vec![ObservedManual {
            identity: manual,
            mesh_primitives: 2,
            registered_primitives: 2,
        }],
        renderer: RendererObservation {
            observed: true,
            frame: 299,
            pipelines: 24,
            pending: 0,
            failed: 0,
        },
    };
    (expected, observed)
}

#[test]
fn exact_live_projection_includes_identity_pins_physics_and_ready_renderer() {
    let (expected, observed) = fixture();
    verify(&expected, &observed).unwrap();
    let wire = serde_json::to_vec(&observed).unwrap();
    let reopened = serde_json::from_slice(&wire).unwrap();
    verify(&expected, &reopened).unwrap();
}

#[test]
fn process_success_or_counters_without_actual_instances_never_prove_readiness() {
    let (expected, mut observed) = fixture();
    observed.visuals.clear();
    observed.manual.clear();
    assert!(verify(&expected, &observed).is_err());
    observed.gameplay_ready = false;
    observed.tick = 0;
    assert!(verify(&expected, &observed).is_err());
}

#[test]
fn wrong_source_variant_transform_parent_collider_or_renderer_is_refused() {
    let (expected, observed) = fixture();
    for fault in 0..15 {
        let mut bad = observed.clone();
        match fault {
            0 => bad.source_hash = Some(bytes_sha256(b"wrong source")),
            1 => bad.functional_hash = Some(bytes_sha256(b"changed gameplay")),
            2 => bad.visuals[0].asset.sha256 = bytes_sha256(b"other variant"),
            3 => bad.visuals[0].transform.translation_m.y = 1.0,
            4 => bad.visuals[0].parent_identity = Some(EntityId::deterministic("wrong parent")),
            5 => bad.visuals[0].parent_has_collider = false,
            6 => bad.visuals[0].scene_ready = false,
            7 => bad.visuals[0].mesh_primitives = 0,
            8 => bad.manual[0].registered_primitives = 1,
            9 => bad.renderer.observed = false,
            10 => bad.renderer.pending = 1,
            11 => bad.renderer.failed = 1,
            12 => bad.renderer.frame = 200,
            13 => bad.manual_projection_ready = false,
            _ => bad.simulation_error = Some("physical projection failed".into()),
        }
        assert!(verify(&expected, &bad).is_err(), "fault {fault}");
    }
}

#[test]
fn duplicate_or_extra_visual_and_manual_identities_do_not_satisfy_counts() {
    let (expected, observed) = fixture();
    let mut bad = observed.clone();
    bad.visuals.push(bad.visuals[0].clone());
    assert!(verify(&expected, &bad).is_err());
    let mut bad = observed.clone();
    bad.manual.push(bad.manual[0].clone());
    assert!(verify(&expected, &bad).is_err());
    let mut bad = observed;
    bad.visual_ready = 2;
    assert!(verify(&expected, &bad).is_err());
}
