#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AssetId, ContentHash, bytes_sha256};
use forge_studio_frontend::asset_library_model_wire::{decode_module, encode_module};
use forge_studio_frontend::asset_library_protocol::{
    ModelReadRequest, ModuleModelReply, ModuleModelRequest,
};
use forge_studio_frontend::module_asset_native::{ModuleAssetResolver, ModuleModelError};
use serde_json::{Value, json};
use std::path::PathBuf;
use std::sync::atomic::{AtomicUsize, Ordering};

struct Fixture {
    root: PathBuf,
    id: AssetId,
    hash: ContentHash,
    bytes: Vec<u8>,
    catalog: Value,
}

impl Fixture {
    fn new(key: &str) -> Self {
        static NEXT: AtomicUsize = AtomicUsize::new(0);
        let root = std::env::temp_dir().join(format!(
            "forge-module-model-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        std::fs::create_dir(&root).unwrap();
        let id = AssetId::deterministic(key);
        let mut json = br#"{"asset":{"version":"2.0"}}"#.to_vec();
        while !json.len().is_multiple_of(4) {
            json.push(b' ');
        }
        let mut bytes = b"glTF".to_vec();
        bytes.extend(2_u32.to_le_bytes());
        bytes.extend(u32::try_from(20 + json.len()).unwrap().to_le_bytes());
        bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend(b"JSON");
        bytes.extend(json);
        std::fs::write(root.join("house.glb"), &bytes).unwrap();
        let hash = bytes_sha256(&bytes);
        let catalog = json!({"schema_version":1,"assets":[{"id":id,"hash":hash,"file":"house.glb","license":"CC0-1.0"}]});
        Self {
            root,
            id,
            hash,
            bytes,
            catalog,
        }
    }
    fn publish(&self) -> ContentHash {
        let bytes = serde_json::to_vec(&self.catalog).unwrap();
        std::fs::write(self.root.join("module-assets.json"), &bytes).unwrap();
        bytes_sha256(&bytes)
    }
    fn resolver(&self) -> ModuleAssetResolver {
        ModuleAssetResolver::new(&self.root, "module-assets.json", self.publish()).unwrap()
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        // This exact directory was exclusively created by this fixture.
        std::fs::remove_dir_all(&self.root).unwrap();
    }
}

#[test]
fn explicit_id_and_pin_work_for_unrelated_modules_without_rekeying_or_writes() {
    for key in ["module-a/house-uuid", "unrelated-module-b/object-uuid"] {
        let fixture = Fixture::new(key);
        let resolver = fixture.resolver();
        let catalog_before = std::fs::read(fixture.root.join("module-assets.json")).unwrap();
        let model_before = std::fs::read(fixture.root.join("house.glb")).unwrap();
        for _ in 0..3 {
            let model = resolver.resolve(fixture.id, fixture.hash).unwrap();
            assert_eq!(model.asset_id, fixture.id);
            assert_eq!(model.bytes, fixture.bytes);
            assert_eq!(model.catalog_sha256, bytes_sha256(&catalog_before));
            let wire = encode_module(ModuleModelReply::Model(model)).unwrap();
            assert!(matches!(
                decode_module(wire, fixture.id, fixture.hash).unwrap(),
                ModuleModelReply::Model(_)
            ));
        }
        assert_eq!(
            std::fs::read(fixture.root.join("module-assets.json")).unwrap(),
            catalog_before
        );
        assert_eq!(
            std::fs::read(fixture.root.join("house.glb")).unwrap(),
            model_before
        );
        assert_eq!(std::fs::read_dir(&fixture.root).unwrap().count(), 2);
    }
}

#[test]
fn unknown_id_is_missing_but_stale_pin_is_rejected() {
    let fixture = Fixture::new("explicit-identity");
    let resolver = fixture.resolver();
    let other = AssetId::deterministic("absent-identity");
    assert_eq!(
        resolver.resolve(other, fixture.hash).unwrap_err(),
        ModuleModelError::Missing(other)
    );
    assert!(matches!(
        resolver.resolve(fixture.id, ContentHash::from_bytes([0; 32])),
        Err(ModuleModelError::Rejected(_))
    ));
}

#[test]
fn corrupted_or_missing_model_never_becomes_missing_entry() {
    let fixture = Fixture::new("corrupt-model");
    let resolver = fixture.resolver();
    std::fs::write(fixture.root.join("house.glb"), b"changed").unwrap();
    assert!(matches!(
        resolver.resolve(fixture.id, fixture.hash),
        Err(ModuleModelError::Rejected(_))
    ));
    std::fs::remove_file(fixture.root.join("house.glb")).unwrap();
    assert!(matches!(
        resolver.resolve(fixture.id, fixture.hash),
        Err(ModuleModelError::Rejected(_))
    ));
}

#[test]
fn changed_catalog_rejects_even_unknown_identity_and_initial_wrong_pin() {
    let mut fixture = Fixture::new("changed-catalog");
    let resolver = fixture.resolver();
    assert!(matches!(
        ModuleAssetResolver::new(
            &fixture.root,
            "module-assets.json",
            ContentHash::from_bytes([0; 32])
        ),
        Err(ModuleModelError::Rejected(_))
    ));
    fixture.catalog["assets"][0]["license"] = json!("changed");
    fixture.publish();
    assert!(matches!(
        resolver.resolve(AssetId::deterministic("absent"), fixture.hash),
        Err(ModuleModelError::Rejected(_))
    ));
    assert!(matches!(
        resolver.resolve(fixture.id, fixture.hash),
        Err(ModuleModelError::Rejected(_))
    ));
}

#[test]
fn catalog_version_duplicates_unknown_fields_and_missing_license_are_refused() {
    for corruption in ["version", "duplicate", "unknown", "license", "nil"] {
        let mut fixture = Fixture::new(corruption);
        match corruption {
            "version" => fixture.catalog["schema_version"] = json!(2),
            "duplicate" => {
                let entry = fixture.catalog["assets"][0].clone();
                fixture.catalog["assets"]
                    .as_array_mut()
                    .unwrap()
                    .push(entry);
            }
            "unknown" => fixture.catalog["untrusted"] = json!("data"),
            "license" => fixture.catalog["assets"][0]["license"] = json!(" "),
            "nil" => {
                fixture.catalog["assets"][0]["id"] = json!("00000000-0000-0000-0000-000000000000");
            }
            _ => unreachable!(),
        }
        assert!(matches!(
            ModuleAssetResolver::new(&fixture.root, "module-assets.json", fixture.publish()),
            Err(ModuleModelError::Rejected(_))
        ));
    }
}

#[test]
fn path_traversal_absolute_paths_ads_and_wrong_extension_are_refused() {
    for file in [
        "../outside.glb",
        "..\\outside.glb",
        "C:\\outside.glb",
        "/outside.glb",
        "model.glb:stream",
        ".hidden.glb",
        "model.txt",
    ] {
        let mut fixture = Fixture::new(file);
        fixture.catalog["assets"][0]["file"] = json!(file);
        assert!(matches!(
            ModuleAssetResolver::new(&fixture.root, "module-assets.json", fixture.publish()),
            Err(ModuleModelError::Rejected(_))
        ));
    }
    let fixture = Fixture::new("catalog-path");
    assert!(
        ModuleAssetResolver::new(&fixture.root, "../module-assets.json", fixture.publish())
            .is_err()
    );
}

#[test]
fn malformed_glb_with_correct_hash_is_rejected_before_wire_publication() {
    let mut fixture = Fixture::new("invalid-glb");
    fixture.bytes[4..8].copy_from_slice(&1_u32.to_le_bytes());
    fixture.hash = bytes_sha256(&fixture.bytes);
    fixture.catalog["assets"][0]["hash"] = json!(fixture.hash);
    std::fs::write(fixture.root.join("house.glb"), &fixture.bytes).unwrap();
    assert!(matches!(
        fixture.resolver().resolve(fixture.id, fixture.hash),
        Err(ModuleModelError::Rejected(_))
    ));
}

#[test]
fn module_request_is_versioned_and_accepts_no_webview_paths() {
    let fixture = Fixture::new("protocol");
    let request = ModuleModelRequest::ModuleModelReference {
        schema_version: 2,
        asset_id: fixture.id,
        sha256: fixture.hash,
    };
    let json = serde_json::to_string(&request).unwrap();
    assert!(matches!(
        serde_json::from_str::<ModelReadRequest>(&json).unwrap(),
        ModelReadRequest::Module(_)
    ));
    let mut value: Value = serde_json::from_str(&json).unwrap();
    value["path"] = json!("C:/outside.glb");
    assert!(serde_json::from_value::<ModelReadRequest>(value).is_err());
    let legacy = json!({"kind":"model_reference","asset_id":fixture.id,"sha256":fixture.hash});
    assert!(matches!(
        serde_json::from_value::<ModelReadRequest>(legacy).unwrap(),
        ModelReadRequest::Vault(_)
    ));
}

#[test]
fn host_selected_installed_catalog_resolves_every_published_pin() {
    let Some(root) = std::env::var_os("FORGE_TEST_MODULE_ASSET_ROOT") else {
        println!("Installed delivery probe not configured; portable fixtures cover the resolver.");
        return;
    };
    let catalog = std::env::var("FORGE_TEST_MODULE_ASSET_CATALOG").unwrap();
    let pin = ContentHash::parse(&std::env::var("FORGE_TEST_MODULE_ASSET_CATALOG_SHA256").unwrap())
        .unwrap();
    let resolver = ModuleAssetResolver::new(std::path::Path::new(&root), &catalog, pin).unwrap();
    let before = std::fs::read(PathBuf::from(&root).join(&catalog)).unwrap();
    let parsed: Value = serde_json::from_slice(&before).unwrap();
    let entries = parsed["assets"].as_array().unwrap();
    assert!(!entries.is_empty());
    for entry in entries {
        let id: AssetId = serde_json::from_value(entry["id"].clone()).unwrap();
        let hash: ContentHash = serde_json::from_value(entry["hash"].clone()).unwrap();
        let model = resolver.resolve(id, hash).unwrap();
        let byte_count = model.bytes.len();
        let wire = encode_module(ModuleModelReply::Model(model)).unwrap();
        assert!(matches!(
            decode_module(wire, id, hash).unwrap(),
            ModuleModelReply::Model(_)
        ));
        println!("Verified installed asset {id} SHA-256 {hash} ({byte_count} bytes)");
    }
    assert_eq!(
        before,
        std::fs::read(PathBuf::from(root).join(catalog)).unwrap()
    );
    println!(
        "Verified {} installed models; catalogue SHA-256 {pin}; no writes",
        entries.len()
    );
}
