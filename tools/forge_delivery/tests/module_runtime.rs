use forge_core::{AreaId, AssetId, ModuleId, bytes_sha256};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    module_runtime, orpmod,
};
use forge_project::{
    authoring::new_terrain, module_package::*, zone_authoring::session::ZoneEditSession,
};
use std::{
    fs,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT: AtomicU64 = AtomicU64::new(0);

struct Fixture {
    root: PathBuf,
    source: PathBuf,
    manifest: ModulePackageManifest,
}

impl Fixture {
    fn new(key: &str) -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-module-runtime-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        let source = root.join("authoring");
        fs::create_dir_all(source.join("zones")).unwrap();
        fs::create_dir(source.join("assets")).unwrap();
        let mut zones = Vec::new();
        let mut files = Vec::new();
        let mut project_id = None;
        for area in ["first", "second"] {
            let terrain = new_terrain(key, area, area).unwrap();
            project_id = Some(terrain.project_id);
            let area_id = terrain.area_id;
            let zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
            let archive = zone.archive_bytes().unwrap();
            let path = format!("zones/{area}.json");
            fs::write(source.join(&path), &archive).unwrap();
            files.push(ModuleFile {
                path: path.clone(),
                bytes: archive.len() as u64,
                sha256: bytes_sha256(&archive),
                role: ModuleFileRole::ZoneArchive,
            });
            zones.push(ModuleZone {
                area_id,
                archive_path: path,
                archive_sha256: bytes_sha256(&archive),
                head_hash: zone.head().content_hash().unwrap(),
                gameplay_sha256: None,
                source_blobs: vec![],
            });
        }
        let id = AssetId::deterministic(format!("runtime-{key}"));
        let mut json = br#"{"asset":{"version":"2.0"}}"#.to_vec();
        while !json.len().is_multiple_of(4) {
            json.push(b' ');
        }
        let mut mesh = b"glTF".to_vec();
        mesh.extend(2_u32.to_le_bytes());
        mesh.extend(((20 + json.len()) as u32).to_le_bytes());
        mesh.extend((json.len() as u32).to_le_bytes());
        mesh.extend(b"JSON");
        mesh.extend(json);
        let asset_hash = bytes_sha256(&mesh);
        let catalog = serde_json::json!({
            "schema":"forge.governed-import-catalog", "schema_version":1,
            "assets":[{
                "id":id, "name":"Runtime structural test", "kind":"mesh",
                "payload":{"path":"assets/object.glb","bytes":mesh.len(),"sha256":asset_hash},
                "provenance":{
                    "source":"authored structural fixture", "author":"Forge tests", "license":"CC0-1.0",
                    "evidence":"LICENSE.txt", "acquired_on":"2026-10-15",
                    "rights":{"local_use":true,"redistribution":"allowed","redistribution_evidence":"LICENSE.txt"}
                },
                "derived_from":null,"transformations":[],"authored_changes":false,"durable_source":null,"thumbnail":null
            }]
        });
        for (path, bytes, role) in [
            ("assets/object.glb", mesh, ModuleFileRole::Asset),
            (
                "assets/catalog.json",
                serde_json::to_vec(&catalog).unwrap(),
                ModuleFileRole::AssetCatalog,
            ),
            (
                "LICENSE.txt",
                b"Authored test fixture: CC0-1.0".to_vec(),
                ModuleFileRole::License,
            ),
            (
                "PROVENANCE.txt",
                b"Forge test fixture".to_vec(),
                ModuleFileRole::Provenance,
            ),
            (
                "CREDITS.txt",
                b"Forge tests".to_vec(),
                ModuleFileRole::Credits,
            ),
        ] {
            fs::write(source.join(path), &bytes).unwrap();
            files.push(ModuleFile {
                path: path.into(),
                bytes: bytes.len() as u64,
                sha256: bytes_sha256(&bytes),
                role,
            });
        }
        let manifest = ModulePackageManifest {
            creator: None,
            schema: MODULE_PACKAGE_SCHEMA.into(),
            version: MODULE_PACKAGE_VERSION,
            project_id: project_id.unwrap(),
            module_id: ModuleId::deterministic(key),
            name: key.into(),
            module_version: "1.0.0".into(),
            compatibility: ModuleCompatibility {
                engine_version: orpmod::ENGINE_VERSION.into(),
                module_api_version: orpmod::MODULE_API_VERSION,
            },
            provenance: ModuleProvenance {
                publisher: "Forge tests".into(),
                source: "runtime integration fixture".into(),
                source_revision: "fixture-1".into(),
                rights: ModuleRights::PrivateLocalOnly,
                license_paths: vec!["LICENSE.txt".into()],
                proof_paths: vec!["PROVENANCE.txt".into()],
                credits_path: "CREDITS.txt".into(),
            },
            asset_catalog_path: "assets/catalog.json".into(),
            files,
            zones,
            assets: vec![ModuleAsset {
                asset_id: id,
                file_path: "assets/object.glb".into(),
                sha256: asset_hash,
                dependencies: vec![],
            }],
            gameplay_visuals: Vec::new(),
        };
        fs::write(source.join("module.json"), manifest.to_bytes().unwrap()).unwrap();
        Self {
            root,
            source,
            manifest,
        }
    }

    fn package(&self) -> PathBuf {
        let output = self.root.join("adventure.orpmod");
        let descriptor = Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: self.manifest.module_id.to_string(),
            label: self.manifest.name.clone(),
            version: self.manifest.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: self.manifest.provenance.source_revision.clone(),
            rights: Rights::PrivateLocalOnly,
            license_paths: self.manifest.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        };
        orpmod::pack(&self.source, descriptor, &output, false).unwrap();
        output
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

#[test]
fn two_modules_load_the_selected_zone_after_the_authoring_directory_is_unavailable() {
    for key in ["harbor-runtime", "mountain-runtime"] {
        let fixture = Fixture::new(key);
        let package = fixture.package();
        let moved = fixture.root.join("unavailable-authoring");
        assert!(fixture.source.starts_with(&fixture.root) && moved.starts_with(&fixture.root));
        fs::rename(&fixture.source, moved).unwrap();
        for expected in &fixture.manifest.zones {
            let loaded = module_runtime::load(
                &package,
                expected.area_id,
                Some(&fixture.root.join("Cache Élan")),
            )
            .unwrap();
            assert_eq!(loaded.manifest, fixture.manifest);
            assert_eq!(loaded.zone.head().base.terrain.area_id, expected.area_id);
            assert_eq!(bytes_sha256(&loaded.archive), expected.archive_sha256);
            assert!(loaded.source_blobs.is_empty());
            assert!(
                loaded
                    .package_root
                    .starts_with(fixture.root.join("Cache Élan"))
            );
        }
    }
}

#[test]
fn admitted_directory_uses_the_same_loader_without_a_source_workspace() {
    let fixture = Fixture::new("directory-runtime");
    let area = fixture.manifest.zones[1].area_id;
    let loaded = module_runtime::load(&fixture.source, area, None).unwrap();
    assert_eq!(loaded.zone.head().base.terrain.area_id, area);
    assert_eq!(loaded.manifest.module_id, fixture.manifest.module_id);
}

#[test]
fn missing_zone_is_refused_before_a_cache_revision_is_created() {
    let fixture = Fixture::new("zone-runtime");
    let package = fixture.package();
    let cache = fixture.root.join("cache");
    assert!(module_runtime::load(&package, AreaId::deterministic("absent"), Some(&cache)).is_err());
    assert!(!cache.exists());
}

#[test]
fn a_changed_cached_payload_is_refused_and_never_silently_replaced() {
    let fixture = Fixture::new("cache-runtime");
    let package = fixture.package();
    let area = fixture.manifest.zones[0].area_id;
    let cache = fixture.root.join("cache");
    let first = module_runtime::load(&package, area, Some(&cache)).unwrap();
    let selected = first
        .package_root
        .join(&fixture.manifest.zones[0].archive_path);
    fs::write(&selected, b"corruption retained for diagnosis").unwrap();
    assert!(module_runtime::load(&package, area, Some(&cache)).is_err());
    assert_eq!(
        fs::read(selected).unwrap(),
        b"corruption retained for diagnosis"
    );
}
