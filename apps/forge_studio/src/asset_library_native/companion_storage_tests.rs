//! Synthetic logical-storage checks for saved source companions.
use super::*;
use crate::asset_library_protocol::{
    LibraryParticleRequest, LibraryPhysicsRequest, LibrarySavedParticleRequest,
    LibrarySavedPhysicsRequest,
};
use forge_assets::{
    source_particle_emission::SourceSingleBurstLoopParameters,
    source_particle_package::SourceParticlePackage,
    source_particle_transport::{
        SourceParticleBlob, SourceParticleControlLayout, SourceParticleOwnerTransport,
        SourceParticleProgram, SourceParticleTransport, SourceParticleVertexTransport,
    },
    source_physics_package::SourcePhysicsPackage,
};
use forge_core::{ContentHash, bytes_sha256};
use std::{collections::BTreeMap, path::Path};

struct SyntheticVault(PathBuf);
impl Drop for SyntheticVault {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.0);
    }
}

fn vault(carrier: &[u8]) -> SyntheticVault {
    let (root, _) = tests::fixture();
    tests::set_model_fixture(&root, carrier);
    SyntheticVault(root)
}

fn register(
    root: &Path,
    field: &str,
    path: &str,
    carrier: ContentHash,
    manifest: ContentHash,
    wire: &[u8],
) {
    let pack = root.join("metadata/rev/demo/one");
    let catalog_path = root.join("normalized/demo/catalog.v1.json");
    let details: Value =
        serde_json::from_slice(&fs::read(pack.join("details.v1.json")).unwrap()).unwrap();
    let mut profiles: Value =
        serde_json::from_slice(&fs::read(pack.join("profiles.v1.json")).unwrap()).unwrap();
    let catalog: Value = serde_json::from_slice(&fs::read(&catalog_path).unwrap()).unwrap();
    profiles["profiles"][0][field] = serde_json::json!({
        "path":path, "carrier_sha256":carrier, "manifest_sha256":manifest,
        "sha256":bytes_sha256(wire)
    });
    fs::write(root.join(path), wire).unwrap();
    tests::write_pack_fixture(root, "demo", &details, &profiles, &catalog, &catalog_path);
}

fn compact(root: &Path, path: &str, wire: &[u8]) -> Vec<u8> {
    fs::create_dir_all(root.join(".forge-storage/packs")).unwrap();
    fs::write(
        root.join(".forge-storage/root.v1.json"),
        br#"{"schema":1,"vault_id":"11111111111111111111111111111111"}"#,
    )
    .unwrap();
    fs::write(
        root.join(".forge-storage/packs/33333333333333333333333333333333.pack"),
        wire,
    )
    .unwrap();
    let descriptor = forge_delivery::vault_storage::Descriptor {
        schema: 1,
        vault_id: "1".repeat(32),
        bytes: wire.len() as u64,
        sha256: sha256(wire),
        segments: vec![forge_delivery::vault_storage::Segment {
            pack: "3".repeat(32),
            offset: 0,
            stored_bytes: wire.len() as u64,
            bytes: wire.len() as u64,
            sha256: sha256(wire),
            codec: "raw".into(),
        }],
    };
    let mut envelope = forge_delivery::vault_storage::MAGIC.to_vec();
    envelope.extend(serde_json::to_vec(&descriptor).unwrap());
    fs::write(root.join(path), &envelope).unwrap();
    envelope
}

fn page(library: &mut Library) -> LibraryPage {
    let LibraryResponse::Page { page } = library
        .execute(LibraryRequest::Search {
            query: LibraryQuery::default(),
        })
        .unwrap()
    else {
        panic!("library page absent")
    };
    page
}

fn assert_export_proof(evidence: &[u8], field: &str, carrier: ContentHash, package: ContentHash) {
    let value: Value = serde_json::from_slice(evidence).unwrap();
    assert_eq!(value["schema"], "forge.private-vault-companion-proof.v1");
    assert_eq!(value["carrier_sha256"], carrier.to_string());
    assert_eq!(value["package_sha256"], package.to_string());
    assert_eq!(
        value["metadata"]["functional_profile"][field]["sha256"],
        package.to_string()
    );
    assert_eq!(
        value["selected_profile_sha256"],
        forge_core::canonical_json_sha256(&value["metadata"]["functional_profile"])
            .unwrap()
            .to_string()
    );
}

#[test]
fn physics_companion_reads_raw_and_compacted_bytes_with_exact_saved_pins() {
    let carrier = crate::source_graph_test_fixture::fixture().unwrap();
    let carrier_pin = bytes_sha256(&carrier);
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_pin,"components":[]}]
    }))
    .unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let wire = SourcePhysicsPackage {
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        manifest,
        candidates: BTreeMap::new(),
    }
    .encode()
    .unwrap();
    let vault = vault(&carrier);
    let path = "normalized/demo/physics.orpp";
    register(
        &vault.0,
        "source_physics_package",
        path,
        carrier_pin,
        manifest_pin,
        &wire,
    );
    let mut library = Library::new(&vault.0).unwrap();
    let page = page(&mut library);
    let current = LibraryPhysicsRequest {
        schema_version: 1,
        id: page.assets[0].id.clone(),
        revision: page.revision,
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
    };
    let saved = LibrarySavedPhysicsRequest {
        schema_version: 1,
        asset_id: vault_asset_id(&current.id),
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        package_sha256: bytes_sha256(&wire),
    };
    assert_eq!(library.read_physics_package(current.clone()).unwrap(), wire);
    assert_eq!(library.read_saved_physics_package(&saved).unwrap(), wire);
    let (captured, evidence) = library
        .export_saved_physics_package(&saved, "2026-11-03")
        .unwrap();
    assert_eq!(captured, wire);
    assert_export_proof(
        &evidence,
        "source_physics_package",
        carrier_pin,
        saved.package_sha256,
    );
    let envelope = compact(&vault.0, path, &wire);
    let registry = fs::read(vault.0.join("metadata/asset-import-registry.v1.json")).unwrap();
    assert_eq!(library.read_physics_package(current.clone()).unwrap(), wire);
    assert_eq!(library.read_saved_physics_package(&saved).unwrap(), wire);
    assert_eq!(
        library
            .export_saved_physics_package(&saved, "2026-11-03")
            .unwrap()
            .0,
        wire
    );
    for lane in 0..3 {
        let mut wrong = saved.clone();
        match lane {
            0 => wrong.carrier_sha256 = bytes_sha256(b"wrong carrier"),
            1 => wrong.manifest_sha256 = bytes_sha256(b"wrong manifest"),
            _ => wrong.package_sha256 = bytes_sha256(b"wrong package"),
        }
        assert!(library.read_saved_physics_package(&wrong).is_err());
    }
    assert_eq!(fs::read(vault.0.join(path)).unwrap(), envelope);
    assert_eq!(
        fs::read(vault.0.join("metadata/asset-import-registry.v1.json")).unwrap(),
        registry
    );
    fs::write(
        vault
            .0
            .join(".forge-storage/packs/33333333333333333333333333333333.pack"),
        b"changed",
    )
    .unwrap();
    assert!(library.read_physics_package(current).is_err());
    assert!(library.read_saved_physics_package(&saved).is_err());
}

#[test]
fn physics_companion_rejects_unowned_buffers_with_the_studio_error() {
    let carrier = crate::source_graph_test_fixture::fixture().unwrap();
    let carrier_pin = bytes_sha256(&carrier);
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_pin,"components":[]}]
    }))
    .unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let wire = SourcePhysicsPackage {
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        manifest,
        candidates: BTreeMap::from([("unused.bin".into(), vec![1, 2, 3])]),
    }
    .encode()
    .unwrap();
    let vault = vault(&carrier);
    register(
        &vault.0,
        "source_physics_package",
        "normalized/demo/physics.orpp",
        carrier_pin,
        manifest_pin,
        &wire,
    );
    let mut library = Library::new(&vault.0).unwrap();
    let page = page(&mut library);
    let current = LibraryPhysicsRequest {
        schema_version: 1,
        id: page.assets[0].id.clone(),
        revision: page.revision,
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
    };
    let saved = LibrarySavedPhysicsRequest {
        schema_version: 1,
        asset_id: vault_asset_id(&current.id),
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        package_sha256: bytes_sha256(&wire),
    };
    assert_eq!(
        library.read_physics_package(current).unwrap_err(),
        "Buffer physique sans propriétaire"
    );
    assert_eq!(
        library.read_saved_physics_package(&saved).unwrap_err(),
        "Buffer physique sans propriétaire"
    );
}

#[test]
fn private_companion_export_pins_the_exact_historical_profile_and_rejects_ambiguity() {
    let carrier = crate::source_graph_test_fixture::fixture().unwrap();
    let carrier_pin = bytes_sha256(&carrier);
    let document = serde_json::json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_pin,"components":[]}]
    });
    let package = |manifest: Vec<u8>| {
        let manifest_pin = bytes_sha256(&manifest);
        let wire = SourcePhysicsPackage {
            carrier_sha256: carrier_pin,
            manifest_sha256: manifest_pin,
            manifest,
            candidates: BTreeMap::new(),
        }
        .encode()
        .unwrap();
        (manifest_pin, wire)
    };
    let (old_manifest, old_wire) = package(serde_json::to_vec_pretty(&document).unwrap());
    let (current_manifest, current_wire) = package(serde_json::to_vec(&document).unwrap());
    assert_ne!(bytes_sha256(&old_wire), bytes_sha256(&current_wire));
    let vault = vault(&carrier);
    register(
        &vault.0,
        "source_physics_package",
        "normalized/demo/old.orpp",
        carrier_pin,
        old_manifest,
        &old_wire,
    );
    let registry_path = vault.0.join("metadata/asset-import-registry.v1.json");
    let original: Value = serde_json::from_slice(&fs::read(&registry_path).unwrap()).unwrap();
    let mut historical = original["packs"]["demo"].clone();
    let old_root = vault.0.join("metadata/rev/demo/history-effect");
    fs::create_dir(&old_root).unwrap();
    for name in ["details.v1.json", "profiles.v1.json"] {
        fs::copy(
            vault.0.join("metadata/rev/demo/one").join(name),
            old_root.join(name),
        )
        .unwrap();
    }
    historical["path"] = "metadata/rev/demo/history-effect".into();
    historical["revision"] = bytes_sha256(b"historical-effect").to_string().into();
    register(
        &vault.0,
        "source_physics_package",
        "normalized/demo/current.orpp",
        carrier_pin,
        current_manifest,
        &current_wire,
    );
    let mut registry: Value = serde_json::from_slice(&fs::read(&registry_path).unwrap()).unwrap();
    registry["packs"]["demo"]["history"] = serde_json::json!([historical.clone()]);
    fs::write(&registry_path, serde_json::to_vec(&registry).unwrap()).unwrap();
    let request = LibrarySavedPhysicsRequest {
        schema_version: 1,
        asset_id: vault_asset_id("demo.arbre-eclaire"),
        carrier_sha256: carrier_pin,
        manifest_sha256: old_manifest,
        package_sha256: bytes_sha256(&old_wire),
    };
    let mut library = Library::new(&vault.0).unwrap();
    let (captured, evidence) = library
        .export_saved_physics_package(&request, "2026-11-03")
        .unwrap();
    assert_eq!(captured, old_wire);
    assert_export_proof(
        &evidence,
        "source_physics_package",
        carrier_pin,
        request.package_sha256,
    );

    let duplicate_root = vault.0.join("metadata/rev/demo/duplicate-effect");
    fs::create_dir(&duplicate_root).unwrap();
    for name in ["details.v1.json", "profiles.v1.json"] {
        fs::copy(old_root.join(name), duplicate_root.join(name)).unwrap();
    }
    historical["path"] = "metadata/rev/demo/duplicate-effect".into();
    historical["revision"] = bytes_sha256(b"duplicate-effect").to_string().into();
    registry["packs"]["demo"]["history"]
        .as_array_mut()
        .unwrap()
        .push(historical);
    fs::write(&registry_path, serde_json::to_vec(&registry).unwrap()).unwrap();
    assert_eq!(
        library.read_saved_physics_package(&request).unwrap(),
        old_wire
    );
    let error = library
        .export_saved_physics_package(&request, "2026-11-03")
        .unwrap_err();
    assert!(
        error.contains("Plusieurs fiches"),
        "unexpected error: {error}"
    );
}

#[test]
fn private_companion_export_respects_an_explicit_local_use_refusal() {
    let carrier = crate::source_graph_test_fixture::fixture().unwrap();
    let carrier_pin = bytes_sha256(&carrier);
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_pin,"components":[]}]
    }))
    .unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let wire = SourcePhysicsPackage {
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        manifest,
        candidates: BTreeMap::new(),
    }
    .encode()
    .unwrap();
    let vault = vault(&carrier);
    register(
        &vault.0,
        "source_physics_package",
        "normalized/demo/physics.orpp",
        carrier_pin,
        manifest_pin,
        &wire,
    );
    let pack = vault.0.join("metadata/rev/demo/one");
    let catalog_path = vault.0.join("normalized/demo/catalog.v1.json");
    let mut details: Value =
        serde_json::from_slice(&fs::read(pack.join("details.v1.json")).unwrap()).unwrap();
    let profiles: Value =
        serde_json::from_slice(&fs::read(pack.join("profiles.v1.json")).unwrap()).unwrap();
    let catalog: Value = serde_json::from_slice(&fs::read(&catalog_path).unwrap()).unwrap();
    details["assets"][0]["rights"]["local_use"] = false.into();
    tests::write_pack_fixture(
        &vault.0,
        "demo",
        &details,
        &profiles,
        &catalog,
        &catalog_path,
    );
    let request = LibrarySavedPhysicsRequest {
        schema_version: 1,
        asset_id: vault_asset_id("demo.arbre-eclaire"),
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        package_sha256: bytes_sha256(&wire),
    };
    let mut library = Library::new(&vault.0).unwrap();
    assert_eq!(library.read_saved_physics_package(&request).unwrap(), wire);
    let error = library
        .export_saved_physics_package(&request, "2026-11-03")
        .unwrap_err();
    assert!(
        error.contains("refuse l’usage local"),
        "unexpected error: {error}"
    );
}

fn particle_fixture(carrier: &[u8]) -> (ContentHash, Vec<u8>) {
    let mut resources = BTreeMap::new();
    let mut blob = |path: &str, bytes: Vec<u8>| {
        let pin = SourceParticleBlob {
            path: path.into(),
            sha256: bytes_sha256(&bytes),
            byte_length: bytes.len(),
        };
        resources.insert(path.into(), bytes);
        pin
    };
    let compute = SourceParticleProgram {
        source: blob(
            "compute.wgsl",
            b"@compute @workgroup_size(1) fn advance() {}".to_vec(),
        ),
        entry_point: "advance".into(),
    };
    let vertex = SourceParticleProgram {
        source: blob(
            "vertex.wgsl",
            b"@vertex fn vertex() -> @builtin(position) vec4<f32> { return vec4<f32>(0.0); }"
                .to_vec(),
        ),
        entry_point: "vertex".into(),
    };
    let owner = SourceParticleOwnerTransport {
        source_component_guid: "a".repeat(32),
        source_component_file_id: "11400000".into(),
        source_node: 0,
        source_primitive: 0,
        source_material: 0,
        compute,
        curve_atlas: blob("curve.bin", vec![0; 8]),
        curve_width: 1,
        curve_height: 1,
        curve_bank: blob("bank.bin", vec![0; 1024]),
        initial_state: blob("state.bin", vec![0; 16]),
        state_prefix_bytes: 16,
        workgroups: 1,
        control: SourceParticleControlLayout {
            delta_offset: 1008,
            mode_offset: 1012,
            stamp_offset: 1016,
            count_offset: 1020,
            reset_mode: 3.0,
            allocate_mode: 2.0,
            update_mode: 1.0,
        },
        spawner: SourceSingleBurstLoopParameters {
            loop_duration_seconds: 3.0,
            loop_count: 1,
            burst_delay_seconds: 0.0,
            burst_count: 1.0,
            burst_active: true,
            initial_play: true,
            prewarm_steps: 0,
        },
        projection: SourceParticleVertexTransport {
            frame_layout: None,
            program: vertex,
            bank: blob("projection-bank.bin", vec![0; 1024]),
            native_vertices: blob("vertices.bin", vec![0; 3 * 64]),
            vertex_count: 3,
            input_stride_bytes: 64,
            output_stride_bytes: 112,
        },
    };
    let contract = SourceParticleTransport {
        schema_version: 1,
        carrier_sha256: bytes_sha256(carrier),
        source_archive_sha256: bytes_sha256(b"synthetic archive"),
        source_graph_sha256: bytes_sha256(b"synthetic graph"),
        licence_ref: "LicenseRef-local-fixture".into(),
        owners: vec![owner],
    };
    let manifest = serde_json::to_vec(&contract).unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let wire = SourceParticlePackage {
        carrier_sha256: bytes_sha256(carrier),
        manifest_sha256: manifest_pin,
        manifest,
        resources,
    }
    .encode()
    .unwrap();
    (manifest_pin, wire)
}

#[test]
fn particle_companion_reads_raw_and_compacted_bytes_with_exact_saved_pins() {
    let state = forge_assets::source_graph_bundle::SOURCE_GRAPH_STATE_BUFFER_DECLARATION;
    let forward = format!("{state}\n{}", crate::source_graph_test_fixture::FORWARD);
    let prepass = format!(
        "{state}\n@vertex fn vertex(@location(0) p: vec3<f32>) -> @builtin(position) vec4<f32> {{ return vec4<f32>(p, 1.0); }}\n@fragment fn fragment() {{}}\n"
    );
    let carrier =
        crate::source_graph_test_fixture::fixture_with_stages(&forward, &prepass).unwrap();
    let carrier_pin = bytes_sha256(&carrier);
    let (manifest_pin, wire) = particle_fixture(&carrier);
    let vault = vault(&carrier);
    let path = "normalized/demo/particles.orpe";
    register(
        &vault.0,
        "source_particle_package",
        path,
        carrier_pin,
        manifest_pin,
        &wire,
    );
    let mut library = Library::new(&vault.0).unwrap();
    let page = page(&mut library);
    let current = LibraryParticleRequest {
        schema_version: 1,
        id: page.assets[0].id.clone(),
        revision: page.revision,
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        package_sha256: bytes_sha256(&wire),
    };
    let saved = LibrarySavedParticleRequest {
        schema_version: 1,
        asset_id: vault_asset_id(&current.id),
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        package_sha256: bytes_sha256(&wire),
    };
    assert_eq!(
        library.read_particle_package(current.clone()).unwrap(),
        wire
    );
    assert_eq!(library.read_saved_particle_package(&saved).unwrap(), wire);
    let (captured, evidence) = library
        .export_saved_particle_package(&saved, "2026-11-03")
        .unwrap();
    assert_eq!(captured, wire);
    assert_export_proof(
        &evidence,
        "source_particle_package",
        carrier_pin,
        saved.package_sha256,
    );
    let envelope = compact(&vault.0, path, &wire);
    let registry = fs::read(vault.0.join("metadata/asset-import-registry.v1.json")).unwrap();
    assert_eq!(
        library.read_particle_package(current.clone()).unwrap(),
        wire
    );
    assert_eq!(library.read_saved_particle_package(&saved).unwrap(), wire);
    assert_eq!(
        library
            .export_saved_particle_package(&saved, "2026-11-03")
            .unwrap()
            .0,
        wire
    );
    for lane in 0..3 {
        let mut wrong = saved.clone();
        match lane {
            0 => wrong.carrier_sha256 = bytes_sha256(b"wrong carrier"),
            1 => wrong.manifest_sha256 = bytes_sha256(b"wrong manifest"),
            _ => wrong.package_sha256 = bytes_sha256(b"wrong package"),
        }
        assert!(library.read_saved_particle_package(&wrong).is_err());
    }
    assert_eq!(fs::read(vault.0.join(path)).unwrap(), envelope);
    assert_eq!(
        fs::read(vault.0.join("metadata/asset-import-registry.v1.json")).unwrap(),
        registry
    );
    fs::write(
        vault
            .0
            .join(".forge-storage/packs/33333333333333333333333333333333.pack"),
        b"changed",
    )
    .unwrap();
    assert!(library.read_particle_package(current).is_err());
    assert!(library.read_saved_particle_package(&saved).is_err());
}
