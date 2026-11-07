//! Exact source-graph transport; rendering is qualified separately.
use super::*;
use forge_core::{CommandId, EntityId, Transform};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementEdit};

#[path = "../../../../crates/forge_assets/tests/support/source_graph_fixture.rs"]
pub(super) mod fixture;

pub(super) fn set_model(module: &mut Fixture, model: &[u8]) {
    let pin = bytes_sha256(model);
    let path = module.source.join("assets/catalog.json");
    let mut catalog: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(&path).unwrap()).unwrap();
    catalog.assets[0].payload.sha256 = pin;
    catalog.assets[0].payload.bytes = model.len() as u64;
    module.manifest.assets[0].sha256 = pin;
    module.replace("assets/mesh.glb", model);
    module.replace(
        "assets/catalog.json",
        &serde_json::to_vec(&catalog).unwrap(),
    );
}

fn set_placement(module: &mut Fixture, pin: forge_core::ContentHash) {
    let mut zone = ZoneEditSession::from_archive_bytes(
        &fs::read(module.source.join("zones/start.json")).unwrap(),
    )
    .unwrap();
    let placement = ManualPlacement {
        id: EntityId::deterministic("source-graph/placement"),
        name: "Synthetic source graph".into(),
        asset_id: module.manifest.assets[0].asset_id,
        asset_hash: pin,
        physics: None,
        particles: None,
        local_lights: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    let request = zone
        .head()
        .prepare_placement(
            CommandId::deterministic("source-graph/insert"),
            PlacementEdit::Insert { placement },
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let archive = zone.archive_bytes().unwrap();
    module.manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
    module.manifest.zones[0].archive_sha256 = bytes_sha256(&archive);
    module.replace("zones/start.json", &archive);
}

#[test]
fn source_graph_module_preserves_pins_and_rejects_modified_programs() {
    let mut module = Fixture::new("source-graph-transport");
    let model = fixture::fixture().unwrap();
    set_model(&mut module, &model);
    set_placement(&mut module, bytes_sha256(&model));

    assert_eq!(
        orpmod::validate_directory(&module.source, false).unwrap(),
        module.manifest
    );
    let output = module.root.join("source-graph.orpmod");
    orpmod::pack(&module.source, module.descriptor(), &output, false).unwrap();
    let loaded = forge_delivery::module_runtime::load(
        &output,
        module.manifest.zones[0].area_id,
        Some(&module.root.join("cache")),
    )
    .unwrap();
    assert_eq!(
        loaded.read_payload("assets/mesh.glb", model.len()).unwrap(),
        model
    );
    assert_eq!(
        loaded.zone.head().content_hash().unwrap(),
        module.manifest.zones[0].head_hash
    );

    let mut changed = model;
    let offset = changed
        .windows(fixture::FORWARD.len())
        .position(|window| window == fixture::FORWARD.as_bytes())
        .unwrap();
    changed[offset] ^= 1;
    let mut broken = Fixture::new("source-graph-broken-program");
    set_model(&mut broken, &changed);
    set_placement(&mut broken, bytes_sha256(&changed));
    let error = orpmod::validate_directory(&broken.source, false).unwrap_err();
    assert!(error.contains("material"), "unexpected refusal: {error}");
}
