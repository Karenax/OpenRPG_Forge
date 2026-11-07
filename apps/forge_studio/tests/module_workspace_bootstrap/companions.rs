//! Real in-memory admission and native reads; no Vault or GPU is involved.
use super::*;
use forge_core::ContentHash;
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
    module_package::{
        MODULE_PLACEMENT_COMPANIONS_API_VERSION, ModuleFilePin, ModulePlacementCompanion,
        ModulePlacementCompanionKind,
    },
    zone_authoring::placements::PhysicsPackageReference,
};
use forge_studio_frontend::{
    module_resources_native::Resources,
    module_resources_protocol::{Assembly, Reply, Request},
};

fn companion_proof(
    asset_id: AssetId,
    carrier: ContentHash,
    manifest: ContentHash,
    package: ContentHash,
) -> Vec<u8> {
    let revision = bytes_sha256(b"synthetic-registry-snapshot");
    let metadata = json!({
        "schema":"forge.private-vault-package-proof.v1", "library_id":"synthetic-asset",
        "model_sha256":carrier, "library_revision":revision,
        "metadata":{"id":"synthetic-asset","model_sha256":carrier},
        "functional_profile":{"external_id":"synthetic-asset","model_content_sha256":carrier,
            "source_physics_package":{"carrier_sha256":carrier,"manifest_sha256":manifest,"sha256":package}},
        "source_catalog_entry":{"id":"synthetic-asset","sha256":carrier}
    });
    serde_json::to_vec(&json!({
        "schema":"forge.private-vault-companion-proof.v1", "kind":"physics", "asset_id":asset_id,
        "carrier_sha256":carrier, "manifest_sha256":manifest, "package_sha256":package,
        "registry_snapshot_revision":revision,
        "selected_details_sha256":canonical_json_sha256(&metadata["metadata"]).unwrap(),
        "selected_profile_sha256":canonical_json_sha256(&metadata["functional_profile"]).unwrap(),
        "selected_catalog_entry_sha256":canonical_json_sha256(&metadata["source_catalog_entry"]).unwrap(),
        "metadata":metadata
    })).unwrap()
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep the carrier, history, packet and provenance pins in one isolated fixture"
)]
fn fixture() -> (Fixture, Vec<u8>, ModulePlacementCompanion) {
    let mut f = Fixture::new("companion-origin", false);
    let model = source_graph_fixture::fixture().unwrap();
    let carrier = bytes_sha256(&model);
    let asset_id = forge_assets::governed_import::vault_asset_id("synthetic-asset");
    let mut asset = f
        .catalog
        .assets
        .iter()
        .find(|asset| asset.id == AssetId::deterministic("bootstrap/blue"))
        .unwrap()
        .clone();
    asset.id = asset_id;
    asset.name = "Synthetic source model".into();
    asset.payload = GovernedAssetFile {
        path: "assets/companion.glb".into(),
        bytes: u64::try_from(model.len()).unwrap(),
        sha256: carrier,
    };
    put(
        &mut f.payloads,
        &mut f.manifest.files,
        &asset.payload.path,
        model,
        ModuleFileRole::Asset,
    );
    f.manifest.assets.push(ModuleAsset {
        asset_id,
        file_path: asset.payload.path.clone(),
        sha256: carrier,
        dependencies: vec![],
    });
    f.catalog.assets.push(asset);

    let manifest = serde_json::to_vec(&json!({"schema":"openrpg.source-physics-transport.v1", "rows":[{"carrier_sha256":carrier,"components":[]}]})).unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let wire = forge_assets::source_physics_package::SourcePhysicsPackage {
        carrier_sha256: carrier,
        manifest_sha256: manifest_pin,
        manifest,
        candidates: BTreeMap::new(),
    }
    .encode()
    .unwrap();
    let package_pin = bytes_sha256(&wire);
    let placement = ManualPlacement {
        id: EntityId::deterministic("bootstrap/companion"),
        name: "Saved source model".into(),
        asset_id,
        asset_hash: carrier,
        physics: Some(Box::new(PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: manifest_pin,
            package_sha256: package_pin,
        })),
        particles: None,
        local_lights: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    for (name, edit) in [
        (
            "insert",
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        ),
        ("remove", PlacementEdit::Remove { id: placement.id }),
    ] {
        let command = f
            .zone
            .head()
            .prepare_placement(
                CommandId::deterministic(format!("bootstrap/companion/{name}")),
                edit,
            )
            .unwrap();
        f.zone.execute(&command).unwrap();
    }
    let archive = f.zone.archive_bytes().unwrap();
    let archive_file = f
        .manifest
        .files
        .iter_mut()
        .find(|file| file.path == "zones/camp.json")
        .unwrap();
    archive_file.bytes = u64::try_from(archive.len()).unwrap();
    archive_file.sha256 = bytes_sha256(&archive);
    f.manifest.zones[0].archive_sha256 = archive_file.sha256;
    f.manifest.zones[0].head_hash = f.zone.head().content_hash().unwrap();
    f.payloads.insert("zones/camp.json".into(), archive);

    let proof = companion_proof(asset_id, carrier, manifest_pin, package_pin);
    let companion = ModulePlacementCompanion {
        areas: vec![f.manifest.zones[0].area_id],
        kind: ModulePlacementCompanionKind::Physics,
        asset_id,
        carrier_sha256: carrier,
        manifest_sha256: manifest_pin,
        package: ModuleFilePin {
            path: "companions/physics.orpp".into(),
            sha256: package_pin,
        },
        proof: ModuleFilePin {
            path: "companions/physics.proof.json".into(),
            sha256: bytes_sha256(&proof),
        },
    };
    put(
        &mut f.payloads,
        &mut f.manifest.files,
        &companion.package.path,
        wire.clone(),
        ModuleFileRole::PlacementCompanion,
    );
    put(
        &mut f.payloads,
        &mut f.manifest.files,
        &companion.proof.path,
        proof,
        ModuleFileRole::Provenance,
    );
    f.manifest
        .provenance
        .proof_paths
        .push(companion.proof.path.clone());
    f.manifest.placement_companions.push(companion.clone());
    f.manifest.compatibility.module_api_version = MODULE_PLACEMENT_COMPANIONS_API_VERSION;
    f.repack("original-with-companion", false);
    (f, wire, companion)
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "The same lifecycle checks source independence, exact reads and damaged-origin refusal"
)]
fn bootstrap_reopens_a_history_only_companion_without_the_original_source() {
    let (f, wire, companion) = fixture();
    assert!(
        f.zone
            .head()
            .corrections
            .placements
            .as_deref()
            .unwrap_or_default()
            .iter()
            .all(|placement| placement.id != EntityId::deterministic("bootstrap/companion"))
    );
    assert!(
        f.zone
            .required_source_companions()
            .unwrap()
            .physics
            .contains(&companion.pin())
    );
    let report = prepare_module_workspace(&f.request()).unwrap();
    assert!(
        report.restored_vegetation_areas.is_empty(),
        "a physics-only package is not vegetation"
    );
    fs::rename(f.root.join("module-source"), f.root.join("unused-source")).unwrap();
    fs::rename(&f.package, f.root.join("unused-source-package.orpmod")).unwrap();

    let mut host = AuthoringHost::new(&report.workspace_root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    host.execute(AuthoringRequest::OpenManaged {
        area_id: report.area_id,
    })
    .unwrap();
    let mut reader = Resources::new();
    let reply = reader
        .execute(
            &report.workspace_root,
            &mut host,
            Request::Describe {
                expected_head: report.zone_head_sha256,
            },
        )
        .unwrap();
    let Reply::Description {
        snapshot: Some(snapshot),
    } = reply
    else {
        panic!("Retained companion is absent")
    };
    assert_eq!(snapshot.placement_companions, vec![companion.clone()]);
    let index = snapshot
        .files
        .iter()
        .position(|file| file.path == companion.package.path)
        .unwrap();
    let mut assembly = Assembly::new(&snapshot, index).unwrap();
    while let Some(offset) = assembly.next_offset() {
        let reply = reader
            .execute(
                &report.workspace_root,
                &mut host,
                Request::Read {
                    expected_head: report.zone_head_sha256,
                    expected_package: report.package_sha256,
                    resource_index: index,
                    offset,
                },
            )
            .unwrap();
        let Reply::Resource { chunk } = reply else {
            panic!("Expected companion chunk")
        };
        assembly.accept(chunk).unwrap();
    }
    assert_eq!(assembly.finish().unwrap(), wire);
    assert!(
        reader
            .execute(
                &report.workspace_root,
                &mut host,
                Request::Read {
                    expected_head: report.zone_head_sha256,
                    expected_package: bytes_sha256(b"stale package"),
                    resource_index: index,
                    offset: 0,
                }
            )
            .is_err()
    );
    let mut corrupted = fs::read(&report.preserved_package).unwrap();
    let index = corrupted.len() / 2;
    corrupted[index] ^= 1;
    fs::write(&report.preserved_package, corrupted).unwrap();
    let mut fresh_reader = Resources::new();
    assert!(
        fresh_reader
            .execute(
                &report.workspace_root,
                &mut host,
                Request::Describe {
                    expected_head: report.zone_head_sha256,
                }
            )
            .is_err(),
        "a damaged retained archive must be refused, not treated as absent"
    );
}
