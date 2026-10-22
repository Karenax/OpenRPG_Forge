#![cfg(not(target_arch = "wasm32"))]
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, AssetTransformation, DurableAssetSource, GovernedAssetKind,
    GovernedImportCatalog, RedistributionRights,
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
            "forge-import-export-{}-{}-Élan",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir_all(&root).unwrap();
        Self {
            store: AssetImportStore::new(&root).unwrap(),
            root,
        }
    }
    fn import(&self, candidate: &ImportCandidate) -> PreparedImport {
        let plan = self.store.prepare(candidate).unwrap();
        self.store.apply(plan.command_id, plan.plan_sha256).unwrap();
        plan
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}
fn glb(generator: &str) -> Vec<u8> {
    let mut json =
        serde_json::to_vec(&serde_json::json!({"asset":{"version":"2.0","generator":generator}}))
            .unwrap();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(u32::try_from(json.len() + 20).unwrap().to_le_bytes());
    bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes
}
fn candidate(generator: &str) -> ImportCandidate {
    let bytes = glb(generator);
    ImportCandidate {
        command_id: CommandId::deterministic(generator),
        expected_revision: 0,
        asset_id: AssetId::deterministic("export-model"),
        expected_asset_sha256: None,
        derived_from: None,
        name: "Original Élan".into(),
        kind: GovernedAssetKind::Mesh,
        provenance: AssetProvenance {
            source: "Original locally authored test fixture".into(),
            author: "Forge test contributors".into(),
            license: "CC0-1.0".into(),
            evidence: "Original authorship".into(),
            acquired_on: "2026-10-15".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Unknown,
                redistribution_evidence: None,
            },
        },
        license_text: "Original test fixture. CC0-1.0 public domain dedication.".into(),
        payload: ImportUpload {
            name: "model.glb".into(),
            byte_length: u64::try_from(bytes.len()).unwrap(),
            bytes,
        },
        authored_changes: false,
        source: None,
        thumbnail: None,
    }
}

#[test]
fn empty_collection_does_not_fabricate_a_governed_catalogue() {
    let f = Fixture::new();
    let error = f.store.export_snapshot(&[], false).unwrap_err();
    assert!(error.contains("No imported assets"), "{error}");
    assert_eq!(f.store.state().unwrap().revision, 0);
}

#[test]
fn snapshot_includes_current_payload_source_and_embedded_licence_from_verified_bytes() {
    let f = Fixture::new();
    let plan = f.import(&candidate("first"));
    let before = f.store.state().unwrap();
    let snapshot = f.store.export_snapshot(&[], false).unwrap();
    assert_eq!(snapshot.catalog.assets, vec![plan.asset.clone()]);
    let asset = &snapshot.catalog.assets[0];
    assert_eq!(snapshot.files[&asset.payload.path], glb("first"));
    let Some(DurableAssetSource::AuthoredFile { file }) = &asset.durable_source else {
        panic!("durable original missing")
    };
    assert_eq!(snapshot.files[&file.path], glb("first"));
    assert_eq!(
        snapshot.files[&asset.provenance.evidence],
        candidate("first").license_text.as_bytes()
    );
    assert_eq!(f.store.state().unwrap(), before);
}

#[test]
fn module_snapshot_excludes_unrelated_assets_and_preserves_its_unplaced_origin_assets() {
    let f = Fixture::new();
    let mut owned = candidate("module-owned");
    owned.provenance.rights.redistribution = RedistributionRights::Allowed;
    owned.provenance.rights.redistribution_evidence = Some("Original authorship".into());
    let owned = f.import(&owned);
    let mut unrelated = candidate("other-module-private");
    unrelated.asset_id = AssetId::deterministic("other-module-model");
    unrelated.expected_revision = 1;
    let unrelated = f.import(&unrelated);
    let mut unplaced = candidate("module-unplaced");
    unplaced.asset_id = AssetId::deterministic("module-unplaced");
    unplaced.expected_revision = 2;
    unplaced.provenance.rights.redistribution = RedistributionRights::Allowed;
    unplaced.provenance.rights.redistribution_evidence = Some("Original authorship".into());
    let unplaced = f.import(&unplaced);
    let before = f.store.state().unwrap();
    assert!(f.store.export_snapshot(&[], true).is_err());
    let snapshot = f
        .store
        .export_module_snapshot(
            &[(owned.asset.id, owned.asset.payload.sha256)],
            &[unplaced.asset.id],
            true,
        )
        .unwrap();
    assert_eq!(snapshot.catalog.assets.len(), 2);
    assert!(
        snapshot
            .catalog
            .assets
            .iter()
            .any(|asset| asset.id == owned.asset.id)
    );
    assert!(
        snapshot
            .catalog
            .assets
            .iter()
            .any(|asset| asset.id == unplaced.asset.id)
    );
    assert!(
        !snapshot
            .catalog
            .assets
            .iter()
            .any(|asset| asset.id == unrelated.asset.id)
    );
    assert!(!snapshot.files.contains_key(&unrelated.asset.payload.path));
    assert_eq!(f.store.state().unwrap(), before);
}

#[test]
fn required_historical_pin_is_exported_without_silently_adopting_the_reimport() {
    let f = Fixture::new();
    let first = f.import(&candidate("first"));
    let mut next = candidate("second");
    next.expected_revision = 1;
    next.expected_asset_sha256 = Some(first.asset.payload.sha256);
    let second = f.import(&next);
    let snapshot = f
        .store
        .export_snapshot(&[(first.asset.id, first.asset.payload.sha256)], false)
        .unwrap();
    assert_eq!(snapshot.catalog.assets, vec![first.asset.clone()]);
    assert_eq!(snapshot.files[&first.asset.payload.path], glb("first"));
    assert!(!snapshot.files.contains_key(&second.asset.payload.path));
    assert_eq!(
        f.store.state().unwrap().catalog.unwrap().assets,
        vec![second.asset]
    );
}

#[test]
fn conflicting_or_unknown_required_identity_pins_are_refused() {
    let f = Fixture::new();
    let first = f.import(&candidate("first"));
    assert!(
        f.store
            .export_snapshot(
                &[
                    (first.asset.id, first.asset.payload.sha256),
                    (first.asset.id, bytes_sha256(b"other"))
                ],
                false
            )
            .is_err()
    );
    assert!(
        f.store
            .export_snapshot(
                &[(
                    AssetId::deterministic("unknown"),
                    first.asset.payload.sha256
                )],
                false
            )
            .is_err()
    );
    assert!(
        f.store
            .export_snapshot(&[(first.asset.id, bytes_sha256(b"stale"))], false)
            .is_err()
    );
}

#[test]
fn publication_requires_explicit_rights_on_the_exported_revision() {
    let f = Fixture::new();
    let first = f.import(&candidate("first"));
    assert!(f.store.export_snapshot(&[], true).is_err());
    let mut next = candidate("second");
    next.expected_revision = 1;
    next.expected_asset_sha256 = Some(first.asset.payload.sha256);
    next.provenance.rights.redistribution = RedistributionRights::Allowed;
    f.import(&next);
    f.store
        .export_snapshot(&[], true)
        .unwrap()
        .catalog
        .validate_for_publication()
        .unwrap();
    assert!(
        f.store
            .export_snapshot(&[(first.asset.id, first.asset.payload.sha256)], true)
            .is_err()
    );
}

#[test]
fn changed_payload_or_licence_never_escapes_in_the_exported_snapshot() {
    for corrupt_licence in [false, true] {
        let f = Fixture::new();
        let first = f.import(&candidate("first"));
        let path = if corrupt_licence {
            &first.asset.provenance.evidence
        } else {
            &first.asset.payload.path
        };
        fs::write(f.root.join(path), b"tampered").unwrap();
        assert!(f.store.export_snapshot(&[], false).is_err());
    }
}

fn rewrite_catalog(f: &Fixture, mutate: impl FnOnce(&mut GovernedImportCatalog)) {
    let mut catalog = f.store.state().unwrap().catalog.unwrap();
    mutate(&mut catalog);
    catalog.validate_for_local_use().unwrap();
    let bytes = serde_json::to_vec(&catalog).unwrap();
    let hash = bytes_sha256(&bytes);
    fs::write(f.root.join(format!("catalogs/{hash}.json")), bytes).unwrap();
    let path = f.root.join("head.json");
    let mut head: serde_json::Value = serde_json::from_slice(&fs::read(&path).unwrap()).unwrap();
    head["current"]["catalog_sha256"] = serde_json::json!(hash);
    head["catalogs"]
        .as_array_mut()
        .unwrap()
        .push(serde_json::json!(hash));
    fs::write(path, serde_json::to_vec(&head).unwrap()).unwrap();
}

#[test]
fn historical_lineage_is_closed_with_its_parent_revision_and_conflicts_are_explicit() {
    let f = Fixture::new();
    let parent = f.import(&candidate("parent-one"));
    let mut child_candidate = candidate("derived-one");
    child_candidate.asset_id = AssetId::deterministic("derived-model");
    child_candidate.expected_revision = 1;
    let child = f.import(&child_candidate);
    rewrite_catalog(&f, |catalog| {
        let child = catalog
            .assets
            .iter_mut()
            .find(|asset| asset.id == child.asset.id)
            .unwrap();
        child.derived_from = Some(parent.asset.id);
        child.transformations.push(AssetTransformation {
            tool: "Original test recipe".into(),
            version: "1".into(),
            parameters_sha256: bytes_sha256(b"original authored recipe"),
        });
    });
    f.store
        .undo(CommandId::deterministic("undo-derived"), 2)
        .unwrap();
    let mut new_parent = candidate("parent-two");
    new_parent.expected_revision = 3;
    new_parent.expected_asset_sha256 = Some(parent.asset.payload.sha256);
    let new_parent = f.import(&new_parent);
    let snapshot = f
        .store
        .export_snapshot(&[(child.asset.id, child.asset.payload.sha256)], false)
        .unwrap();
    assert_eq!(snapshot.catalog.assets.len(), 2);
    assert!(
        snapshot
            .catalog
            .assets
            .iter()
            .any(|asset| asset.id == parent.asset.id
                && asset.payload.sha256 == parent.asset.payload.sha256)
    );
    assert_eq!(
        snapshot.files[&parent.asset.payload.path],
        glb("parent-one")
    );
    assert!(!snapshot.files.contains_key(&new_parent.asset.payload.path));
    let scoped = f
        .store
        .export_module_snapshot(&[(child.asset.id, child.asset.payload.sha256)], &[], false)
        .unwrap();
    assert_eq!(scoped.catalog.assets.len(), 2);
    assert_eq!(scoped.files[&parent.asset.payload.path], glb("parent-one"));
    assert!(!scoped.files.contains_key(&new_parent.asset.payload.path));
    assert!(
        f.store
            .export_snapshot(
                &[
                    (child.asset.id, child.asset.payload.sha256),
                    (new_parent.asset.id, new_parent.asset.payload.sha256)
                ],
                false
            )
            .is_err()
    );
}

#[test]
fn declared_total_export_budget_is_checked_before_allocating_or_opening_payloads() {
    let f = Fixture::new();
    let first = f.import(&candidate("first"));
    rewrite_catalog(&f, |catalog| {
        catalog.assets = (0..3)
            .map(|index| {
                let mut asset = first.asset.clone();
                asset.id = AssetId::deterministic(format!("large-{index}"));
                asset.payload.path = format!("assets/large-{index}.glb");
                asset.payload.sha256 = bytes_sha256(format!("large-{index}").as_bytes());
                asset.payload.bytes = 256 * 1024 * 1024;
                asset.durable_source = None;
                asset
            })
            .collect();
    });
    let error = f.store.export_snapshot(&[], false).unwrap_err();
    assert!(error.contains("budget"), "{error}");
}
