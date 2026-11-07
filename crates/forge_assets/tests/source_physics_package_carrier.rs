use forge_assets::{
    source_graph_glb::SourceGraphGlbReadBudget, source_physics_package::SourcePhysicsPackage,
};
use forge_core::bytes_sha256;
use std::collections::BTreeMap;

#[path = "support/source_graph_fixture.rs"]
mod source_graph_fixture;

fn package(carrier: &[u8]) -> SourcePhysicsPackage {
    let carrier_sha256 = bytes_sha256(carrier);
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_sha256,"components":[]}]
    }))
    .unwrap();
    SourcePhysicsPackage {
        carrier_sha256,
        manifest_sha256: bytes_sha256(&manifest),
        manifest,
        candidates: BTreeMap::new(),
    }
}

#[test]
fn carrier_resolution_checks_pins_and_complete_candidate_ownership() {
    let carrier = source_graph_fixture::fixture().unwrap();
    let mut package = package(&carrier);
    let wire = package.encode().unwrap();
    let decoded = SourcePhysicsPackage::decode_pinned(
        &wire,
        package.carrier_sha256,
        package.manifest_sha256,
        bytes_sha256(&wire),
    )
    .unwrap();
    let (graph, components) = decoded
        .resolve_for_carrier(&carrier, SourceGraphGlbReadBudget::Standard)
        .unwrap();
    assert_eq!(graph.document()["nodes"].as_array().unwrap().len(), 1);
    assert!(components.is_empty());

    package
        .candidates
        .insert("unowned.bin".into(), b"unowned".to_vec());
    assert!(
        package
            .resolve_for_carrier(&carrier, SourceGraphGlbReadBudget::Standard)
            .err()
            .unwrap()
            .contains("without declared owner")
    );
    package.candidates.clear();
    let mut changed_carrier = carrier.clone();
    changed_carrier[0] ^= 1;
    assert!(
        package
            .resolve_for_carrier(&changed_carrier, SourceGraphGlbReadBudget::Standard)
            .is_err()
    );
    package.manifest.push(b' ');
    assert!(
        package
            .resolve_for_carrier(&carrier, SourceGraphGlbReadBudget::Standard)
            .is_err()
    );
}
