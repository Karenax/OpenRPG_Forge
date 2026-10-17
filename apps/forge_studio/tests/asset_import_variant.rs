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
    parent: PreparedImport,
}
impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-variant-{}-{}-Élan",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir_all(&root).unwrap();
        let store = AssetImportStore::new(&root).unwrap();
        let parent = store.prepare(&candidate("parent")).unwrap();
        store.apply(parent.command_id, parent.plan_sha256).unwrap();
        Self {
            root,
            store,
            parent,
        }
    }
    fn variant(&self) -> ImportCandidate {
        let mut child = candidate("variant");
        child.expected_revision = 1;
        child.derived_from = Some(self.parent.asset.id);
        child.authored_changes = true;
        child.source = Some(ImportSource {
            kind: ImportSourceKind::Recipe,
            upload: upload("recipe.json", br#"{"translate":[0,1,0]}"#.to_vec()),
        });
        child
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
fn candidate(name: &str) -> ImportCandidate {
    let mut json =
        serde_json::to_vec(&serde_json::json!({"asset":{"version":"2.0","generator":name}}))
            .unwrap();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(u32::try_from(20 + json.len()).unwrap().to_le_bytes());
    bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(json);
    ImportCandidate {
        command_id: CommandId::deterministic(name),
        expected_revision: 0,
        asset_id: AssetId::deterministic(name),
        expected_asset_sha256: None,
        derived_from: None,
        name: format!("Asset {name}"),
        kind: GovernedAssetKind::Mesh,
        provenance: AssetProvenance {
            source: "Original local fixture".into(),
            author: "Forge test contributors".into(),
            license: "CC0-1.0".into(),
            evidence: "Authored original".into(),
            acquired_on: "2026-10-15".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Unknown,
                redistribution_evidence: None,
            },
        },
        license_text: "Original authored test fixture, CC0-1.0 public domain dedication.".into(),
        payload: upload("model.glb", bytes),
        authored_changes: false,
        source: None,
        thumbnail: None,
    }
}

#[test]
fn explicit_variant_gets_a_new_id_preserved_parent_and_durable_retouch_source() {
    let f = Fixture::new();
    let before = f.store.state().unwrap();
    let request = ImportRequest::Prepare {
        candidate: Box::new(f.variant()),
    };
    assert_eq!(
        decode_request(&encode_request(&request).unwrap()).unwrap(),
        request
    );
    let plan = f.store.prepare(&f.variant()).unwrap();
    assert_ne!(plan.asset.id, f.parent.asset.id);
    assert_eq!(plan.asset.derived_from, Some(f.parent.asset.id));
    assert!(plan.asset.authored_changes);
    assert!(plan.asset.durable_source.is_some());
    assert!(!plan.asset.transformations.is_empty());
    assert_eq!(f.store.state().unwrap(), before);
    f.store.apply(plan.command_id, plan.plan_sha256).unwrap();
    let assets = f.store.state().unwrap().catalog.unwrap().assets;
    assert_eq!(assets.len(), 2);
    assert!(assets.contains(&f.parent.asset));
    let snapshot = f
        .store
        .export_snapshot(&[(plan.asset.id, plan.asset.payload.sha256)], false)
        .unwrap();
    assert_eq!(snapshot.catalog.assets.len(), 2);
}

#[test]
fn variant_requires_known_distinct_parent_same_type_and_explicit_durable_retouches() {
    let f = Fixture::new();
    for case in 0..6 {
        let mut child = f.variant();
        let expected = match case {
            0 => {
                child.derived_from = Some(AssetId::deterministic("unknown-parent"));
                "variant parent"
            }
            1 => {
                child.asset_id = f.parent.asset.id;
                child.expected_asset_sha256 = Some(f.parent.asset.payload.sha256);
                "variant identity"
            }
            2 => {
                child.kind = GovernedAssetKind::Audio;
                "variant media type"
            }
            3 => {
                child.authored_changes = false;
                "variant retouches"
            }
            4 => {
                child.source = None;
                "variant retouches"
            }
            _ => {
                child.provenance.rights.local_use = false;
                "variant local rights"
            }
        };
        let error = f.store.prepare(&child).unwrap_err();
        assert!(error.contains(expected), "{error}");
    }
    assert_eq!(f.store.state().unwrap().revision, 1);
}

#[test]
fn reimport_of_variant_keeps_parent_and_id_and_cannot_reparent_or_escalate_publication() {
    let f = Fixture::new();
    let first = f.store.prepare(&f.variant()).unwrap();
    f.store.apply(first.command_id, first.plan_sha256).unwrap();
    let mut revised = f.variant();
    revised.command_id = CommandId::deterministic("variant-reimport");
    revised.expected_revision = 2;
    revised.expected_asset_sha256 = Some(first.asset.payload.sha256);
    revised.derived_from = None; // Existing and older clients cannot silently clear lineage.
    revised.provenance.rights.redistribution = RedistributionRights::Allowed;
    revised.payload = candidate("variant-retouched").payload;
    let plan = f.store.prepare(&revised).unwrap();
    assert_eq!(plan.asset.id, first.asset.id);
    assert_eq!(plan.asset.derived_from, first.asset.derived_from);
    f.store.apply(plan.command_id, plan.plan_sha256).unwrap();
    assert!(f.store.export_snapshot(&[], true).is_err()); // Parent's unknown rights remain binding.
    revised.command_id = CommandId::deterministic("reparent");
    revised.expected_revision = 3;
    revised.expected_asset_sha256 = Some(plan.asset.payload.sha256);
    revised.derived_from = Some(AssetId::deterministic("another-parent"));
    assert!(
        f.store
            .prepare(&revised)
            .unwrap_err()
            .contains("variant parent")
    );
    assert_eq!(
        f.store
            .read(
                first.asset.id,
                first.asset.payload.sha256,
                ImportReadRole::Payload
            )
            .unwrap()
            .sha256,
        bytes_sha256(&candidate("variant").payload.bytes)
    );
}
