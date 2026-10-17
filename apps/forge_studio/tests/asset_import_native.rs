#![cfg(not(target_arch = "wasm32"))]
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, GovernedAssetKind, RedistributionRights,
};
use forge_core::{AssetId, CommandId, bytes_sha256};
use forge_studio_frontend::{asset_import_native::AssetImportStore, asset_import_protocol::*};
use std::{
    fs,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};
static NEXT: AtomicU64 = AtomicU64::new(0);
struct Fixture {
    root: PathBuf,
    store: AssetImportStore,
}
impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-import-{}-{}-Élan",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir_all(&root).unwrap();
        Self {
            store: AssetImportStore::new(&root).unwrap(),
            root,
        }
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}
fn upload(name: &str, bytes: Vec<u8>) -> ImportUpload {
    ImportUpload {
        name: name.into(),
        byte_length: u64::try_from(bytes.len()).unwrap(),
        bytes,
    }
}
fn glb() -> Vec<u8> {
    let mut json = br#"{"asset":{"version":"2.0"}}"#.to_vec();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut data = b"glTF".to_vec();
    data.extend(2_u32.to_le_bytes());
    data.extend(u32::try_from(json.len() + 20).unwrap().to_le_bytes());
    data.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    data.extend(b"JSON");
    data.extend(json);
    data
}
fn png() -> Vec<u8> {
    // Original one-pixel RGBA fixture [30, 90, 160, 255], valid zlib and PNG CRCs.
    vec![
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6,
        0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 144, 139, 90, 240,
        31, 0, 3, 202, 2, 24, 218, 135, 235, 249, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    ]
}
fn wav() -> Vec<u8> {
    let mut bytes = b"RIFF".to_vec();
    bytes.extend(38_u32.to_le_bytes());
    bytes.extend(b"WAVEfmt ");
    bytes.extend(16_u32.to_le_bytes());
    bytes.extend(1_u16.to_le_bytes());
    bytes.extend(1_u16.to_le_bytes());
    bytes.extend(8000_u32.to_le_bytes());
    bytes.extend(16000_u32.to_le_bytes());
    bytes.extend(2_u16.to_le_bytes());
    bytes.extend(16_u16.to_le_bytes());
    bytes.extend(b"data");
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend([0, 0]);
    bytes
}
fn candidate(name: &str, kind: GovernedAssetKind, bytes: Vec<u8>) -> ImportCandidate {
    ImportCandidate {
        command_id: CommandId::deterministic(format!("import/{name}")),
        expected_revision: 0,
        asset_id: AssetId::deterministic(format!("asset/{name}")),
        expected_asset_sha256: None,
        derived_from: None,
        name: "Objet retouchable Élan".into(),
        kind,
        provenance: AssetProvenance {
            source: "Local authored fixture".into(),
            author: "Élodie".into(),
            license: "CC0-1.0".into(),
            evidence: "Own source fixture".into(),
            acquired_on: "2026-10-15".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Unknown,
                redistribution_evidence: None,
            },
        },
        payload: upload(name, bytes),
        license_text: "Authored fixture, CC0-1.0. Original source: local test fixture.".into(),
        authored_changes: false,
        source: None,
        thumbnail: None,
    }
}
#[test]
fn binary_upload_round_trip_has_no_json_media_expansion_and_rejects_truncation() {
    let request = ImportRequest::Prepare {
        candidate: Box::new(candidate("mesh.glb", GovernedAssetKind::Mesh, glb())),
    };
    let wire = encode_request(&request).unwrap();
    assert_eq!(decode_request(&wire).unwrap(), request);
    assert!(decode_request(&wire[..wire.len() - 1]).is_err());
    let mut extra = wire.clone();
    extra.push(0);
    assert!(decode_request(&extra).is_err());
    assert!(wire.len() < 4096);
}
#[test]
fn prepare_does_not_activate_catalog_and_apply_is_idempotent_with_audit() {
    let f = Fixture::new();
    let before = f.store.state().unwrap();
    let plan = f
        .store
        .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
        .unwrap();
    assert_eq!(f.store.state().unwrap(), before);
    let receipt = f.store.apply(plan.command_id, plan.plan_sha256).unwrap();
    assert_eq!(receipt.revision, 1);
    assert_eq!(receipt.asset_id, Some(plan.asset.id));
    assert_eq!(
        f.store.apply(plan.command_id, plan.plan_sha256).unwrap(),
        receipt
    );
    let reopened = AssetImportStore::new(&f.root).unwrap().state().unwrap();
    assert_eq!(reopened.catalog.unwrap().assets, vec![plan.asset]);
    assert!(f.root.join("head.json").is_file());
}
#[test]
fn three_media_kinds_import_and_read_verified_original_bytes_without_vault() {
    for (name, kind, bytes) in [
        ("mesh.glb", GovernedAssetKind::Mesh, glb()),
        ("image.png", GovernedAssetKind::Texture, png()),
        ("audio.wav", GovernedAssetKind::Audio, wav()),
    ] {
        let f = Fixture::new();
        let plan = f
            .store
            .prepare(&candidate(name, kind, bytes.clone()))
            .unwrap();
        f.store.apply(plan.command_id, plan.plan_sha256).unwrap();
        let read = f
            .store
            .read(plan.asset.id, bytes_sha256(&bytes), ImportReadRole::Payload)
            .unwrap();
        assert_eq!(read.upload.bytes, bytes);
        assert_eq!(read.kind, kind);
        assert!(plan.asset.durable_source.is_some());
    }
}
#[test]
fn reimport_requires_old_pin_and_keeps_id_retouches_and_historical_bytes() {
    let f = Fixture::new();
    let first = candidate("mesh.glb", GovernedAssetKind::Mesh, glb());
    let original = first.payload.bytes.clone();
    let first_plan = f.store.prepare(&first).unwrap();
    f.store
        .apply(first.command_id, first_plan.plan_sha256)
        .unwrap();
    let mut revised = first;
    revised.command_id = CommandId::deterministic("reimport");
    revised.expected_revision = 1;
    revised.authored_changes = true;
    revised.source = Some(ImportSource {
        kind: ImportSourceKind::Recipe,
        upload: upload("retouch.json", br#"{"translate":[0,1,0]}"#.to_vec()),
    });
    // Use a distinct valid JSON representation, not a damaged GLB.
    revised.payload = upload("revised.glb", {
        let mut bytes = glb();
        let n = bytes.len();
        bytes[n - 1] = b'\n';
        bytes
    });
    assert!(f.store.prepare(&revised).is_err());
    revised.expected_asset_sha256 = Some(first_plan.asset.payload.sha256);
    let second_plan = f.store.prepare(&revised).unwrap();
    f.store
        .apply(revised.command_id, second_plan.plan_sha256)
        .unwrap();
    assert_eq!(second_plan.asset.id, first_plan.asset.id);
    assert_eq!(second_plan.asset.name, first_plan.asset.name);
    assert_eq!(
        f.store
            .read(
                first_plan.asset.id,
                first_plan.asset.payload.sha256,
                ImportReadRole::Payload
            )
            .unwrap()
            .upload
            .bytes,
        original
    );
    assert_eq!(f.store.state().unwrap().catalog.unwrap().assets.len(), 1);
}
#[test]
fn stale_plan_conflicting_command_and_tampered_staged_payload_never_change_head() {
    let f = Fixture::new();
    let first = f
        .store
        .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
        .unwrap();
    let second = f
        .store
        .prepare(&candidate("image.png", GovernedAssetKind::Texture, png()))
        .unwrap();
    assert!(
        f.store
            .apply(CommandId::deterministic("wrong-command"), first.plan_sha256)
            .is_err()
    );
    f.store.apply(first.command_id, first.plan_sha256).unwrap();
    let state = f.store.state().unwrap();
    assert!(
        f.store
            .apply(second.command_id, second.plan_sha256)
            .is_err()
    );
    assert_eq!(f.store.state().unwrap(), state);
    let mut next = candidate("image.png", GovernedAssetKind::Texture, png());
    next.command_id = CommandId::deterministic("image-after-first-revision");
    next.expected_revision = 1;
    let plan = f.store.prepare(&next).unwrap();
    fs::write(f.root.join(&plan.asset.payload.path), b"tampered").unwrap();
    assert!(f.store.apply(plan.command_id, plan.plan_sha256).is_err());
    assert_eq!(f.store.state().unwrap(), state);
}
#[test]
fn undo_restores_catalog_and_preserves_imported_octets_and_revision_order() {
    let f = Fixture::new();
    let p = f
        .store
        .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
        .unwrap();
    f.store.apply(p.command_id, p.plan_sha256).unwrap();
    let u = f.store.undo(CommandId::deterministic("undo"), 1).unwrap();
    assert!(u.undo);
    assert_eq!(u.revision, 2);
    assert!(f.store.state().unwrap().catalog.is_none());
    assert!(f.root.join(&p.asset.payload.path).is_file());
    assert!(
        f.store
            .undo(CommandId::deterministic("stale-undo"), 1)
            .is_err()
    );
}
#[test]
fn invalid_format_program_disguise_missing_provenance_and_missing_authored_source_are_refused() {
    let f = Fixture::new();
    for (name, kind, bytes) in [
        ("fake.glb", GovernedAssetKind::Mesh, b"MZprogram".to_vec()),
        ("bad.png", GovernedAssetKind::Texture, glb()),
        ("bad.wav", GovernedAssetKind::Audio, b"RIFFinvalid".to_vec()),
    ] {
        assert!(f.store.prepare(&candidate(name, kind, bytes)).is_err());
    }
    let mut c = candidate("mesh.glb", GovernedAssetKind::Mesh, glb());
    c.provenance.author.clear();
    assert!(f.store.prepare(&c).is_err());
    let mut c = candidate("mesh.glb", GovernedAssetKind::Mesh, glb());
    c.authored_changes = true;
    assert!(f.store.prepare(&c).is_err());
    assert_eq!(f.store.state().unwrap().revision, 0);
}
#[test]
fn thumbnail_and_authored_recipe_are_pinned_and_reopened() {
    let f = Fixture::new();
    let mut c = candidate("mesh.glb", GovernedAssetKind::Mesh, glb());
    c.authored_changes = true;
    c.source = Some(ImportSource {
        kind: ImportSourceKind::Recipe,
        upload: upload("recipe.json", b"{}".to_vec()),
    });
    c.thumbnail = Some(upload("preview.png", png()));
    let plan = f.store.prepare(&c).unwrap();
    f.store.apply(plan.command_id, plan.plan_sha256).unwrap();
    assert_eq!(
        f.store
            .read(
                plan.asset.id,
                plan.asset.payload.sha256,
                ImportReadRole::Thumbnail
            )
            .unwrap()
            .upload
            .bytes,
        png()
    );
    assert_eq!(
        f.store
            .read(
                plan.asset.id,
                plan.asset.payload.sha256,
                ImportReadRole::Source
            )
            .unwrap()
            .upload
            .bytes,
        b"{}"
    );
}
#[test]
fn user_file_names_are_not_paths_and_internal_paths_remain_ascii() {
    let f = Fixture::new();
    assert!(
        f.store
            .prepare(&candidate("../outside.glb", GovernedAssetKind::Mesh, glb()))
            .is_err()
    );
    let p = f
        .store
        .prepare(&candidate("Élan.glb", GovernedAssetKind::Mesh, glb()))
        .unwrap();
    assert!(p.asset.payload.path.is_ascii());
    assert_eq!(p.asset.name, "Objet retouchable Élan");
}

#[test]
fn model_lookup_never_falls_back_for_known_stale_or_corrupt_imports() {
    let f = Fixture::new();
    let id = AssetId::deterministic("never-imported");
    assert!(
        f.store
            .lookup_model(id, bytes_sha256(b"unknown"))
            .unwrap()
            .is_none()
    );
    let p = f
        .store
        .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
        .unwrap();
    f.store.apply(p.command_id, p.plan_sha256).unwrap();
    assert!(
        f.store
            .lookup_model(p.asset.id, bytes_sha256(b"stale"))
            .is_err()
    );
    fs::write(f.root.join(&p.asset.payload.path), b"corrupt").unwrap();
    assert!(
        f.store
            .lookup_model(p.asset.id, p.asset.payload.sha256)
            .is_err()
    );
}

#[test]
fn concurrent_writer_is_refused_without_changing_the_active_revision() {
    let f = Fixture::new();
    let lock = fs::OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .truncate(false)
        .open(f.root.join(".import.lock"))
        .unwrap();
    lock.try_lock().unwrap();
    assert!(
        f.store
            .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
            .is_err()
    );
    assert_eq!(f.store.state().unwrap().revision, 0);
    drop(lock);
    assert!(
        f.store
            .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
            .is_ok()
    );
}

#[test]
fn existing_immutable_payload_is_never_overwritten_by_prepare() {
    let f = Fixture::new();
    let bytes = glb();
    let path = f.root.join(format!("assets/{}.glb", bytes_sha256(&bytes)));
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    fs::write(&path, b"preexisting conflicting data").unwrap();
    assert!(
        f.store
            .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, bytes))
            .is_err()
    );
    assert_eq!(fs::read(path).unwrap(), b"preexisting conflicting data");
    assert_eq!(f.store.state().unwrap().revision, 0);
}

#[test]
fn reused_command_with_another_plan_does_not_replace_its_original_immutable_plan() {
    let f = Fixture::new();
    let first = f
        .store
        .prepare(&candidate("mesh.glb", GovernedAssetKind::Mesh, glb()))
        .unwrap();
    let mut second = candidate("audio.wav", GovernedAssetKind::Audio, wav());
    second.command_id = first.command_id;
    assert!(f.store.prepare(&second).is_err());
    f.store.apply(first.command_id, first.plan_sha256).unwrap();
    assert_eq!(
        f.store.state().unwrap().catalog.unwrap().assets,
        vec![first.asset]
    );
}
