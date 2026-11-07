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
            terrain_surfaces: Vec::new(),
            ocean_inputs: Vec::new(),
            local_light_packages: Vec::new(),
            historical_models: Vec::new(),
            placement_companions: Vec::new(),
            environments: Vec::new(),
            objects: Vec::new(),
            fish_profiles: Vec::new(),
            vegetation: Vec::new(),
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
                Some(&fixture.root.join("Cache Ã‰lan")),
            )
            .unwrap();
            assert_eq!(loaded.manifest, fixture.manifest);
            assert_eq!(loaded.zone.head().base.terrain.area_id, expected.area_id);
            assert_eq!(bytes_sha256(&loaded.archive), expected.archive_sha256);
            assert!(loaded.source_blobs.is_empty());
            assert!(
                loaded
                    .package_root
                    .starts_with(fixture.root.join("Cache Ã‰lan"))
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

fn attach_environment(fixture: &mut Fixture) {
    use std::collections::BTreeMap;
    let mut names: Vec<String> = [
        "atmosphere.json",
        "cloud-environment.json",
        "params.bin",
        "transmittance.wgsl",
        "multiScat.wgsl",
        "skyView.wgsl",
        "irradiance.wgsl",
        "sky.wgsl",
        "environment-cube.bin",
        "village-lighting.json",
        "cloud-view-params.bin",
        "cloud-weather.wgsl",
        "cloud-bounds.wgsl",
        "cloud-shadow.wgsl",
        "cloud-trace.wgsl",
        "cloud-temporal.wgsl",
        "cloud-pano.wgsl",
        "cloud-blueNoise.bin",
        "LICENSE-Tidewater.txt",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    names.extend((0..7).map(|level| format!("cloud-volume-m{level}.rgba8.bin")));
    for step in 1..=120 {
        for kind in ["global", "atmosphere"] {
            names.push(format!("cloud-step{step:03}-{kind}.bin"));
        }
    }
    fs::create_dir(fixture.source.join("environment")).unwrap();
    let mut entries = BTreeMap::new();
    let mut resources = Vec::new();
    for name in names {
        let raw = name.as_bytes();
        let hash = bytes_sha256(raw);
        let path = format!("environment/{name}");
        fs::write(fixture.source.join(&path), raw).unwrap();
        let role = if name == "LICENSE-Tidewater.txt" {
            fixture.manifest.provenance.license_paths.push(path.clone());
            ModuleFileRole::License
        } else {
            ModuleFileRole::Environment
        };
        fixture.manifest.files.push(ModuleFile {
            path: path.clone(),
            bytes: raw.len() as u64,
            sha256: hash,
            role,
        });
        entries.insert(
            name.clone(),
            serde_json::json!({"bytes":raw.len(),"sha256":hash}),
        );
        resources.push(ModuleFilePin { path, sha256: hash });
    }
    let bytes = serde_json::to_vec(
        &serde_json::json!({"schema":"forge.tidewater.environment-package","version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c","files":entries}),
    )
    .unwrap();
    let path = "environment/package.json";
    let hash = bytes_sha256(&bytes);
    fs::write(fixture.source.join(path), &bytes).unwrap();
    fixture.manifest.files.push(ModuleFile {
        path: path.into(),
        bytes: bytes.len() as u64,
        sha256: hash,
        role: ModuleFileRole::Environment,
    });
    fixture
        .manifest
        .environments
        .push(ModuleEnvironmentBinding {
            solar_clock: None,
            area_id: fixture.manifest.zones[0].area_id,
            source: ModuleEnvironmentSource::SourceProfile {
                environment: ModuleEnvironmentFiles {
                    manifest: ModuleFilePin {
                        path: path.into(),
                        sha256: hash,
                    },
                    resources,
                },
                programs: None,
            },
        });
    fixture.manifest.compatibility.module_api_version = MODULE_ENVIRONMENT_API_VERSION;
    fs::write(
        fixture.source.join("module.json"),
        fixture.manifest.to_bytes().unwrap(),
    )
    .unwrap();
}

#[test]
fn closed_environment_survives_source_removal_and_is_not_injected_into_another_area() {
    let mut fixture = Fixture::new("closed-environment");
    attach_environment(&mut fixture);
    let package = fixture.package();
    let unavailable = fixture.root.join("source-unavailable");
    assert!(fixture.source.starts_with(&fixture.root) && unavailable.starts_with(&fixture.root));
    fs::rename(&fixture.source, unavailable).unwrap();
    let loaded = module_runtime::load(
        &package,
        fixture.manifest.zones[0].area_id,
        Some(&fixture.root.join("cache")),
    )
    .unwrap();
    let resources = loaded.environment().unwrap().unwrap();
    assert_eq!(resources.environment.file_count(), 266);
    assert_eq!(resources.environment.bootstrap_frames(), 120);
    assert!(resources.programs.is_none());
    let other = module_runtime::load(
        &package,
        fixture.manifest.zones[1].area_id,
        Some(&fixture.root.join("cache")),
    )
    .unwrap();
    assert!(other.environment().unwrap().is_none());
    fs::write(loaded.package_root.join("environment/sky.wgsl"), b"corrupt").unwrap();
    assert!(loaded.environment().is_err());
}

#[test]
fn environment_index_mismatch_refuses_before_requesting_any_resource() {
    let mut fixture = Fixture::new("environment-preflight");
    attach_environment(&mut fixture);
    let ModuleEnvironmentSource::SourceProfile { environment, .. } =
        &mut fixture.manifest.environments[0].source;
    environment.resources[0].sha256 = bytes_sha256(b"foreign");
    let mut calls = 0;
    assert!(
        forge_delivery::module_environment::resolve(
            &fixture.manifest,
            fixture.manifest.zones[0].area_id,
            |_, _| {
                calls += 1;
                Err("No transport should be requested".into())
            }
        )
        .is_err()
    );
    assert_eq!(calls, 0);
    let mut fixture = Fixture::new("environment-missing-index-resource");
    attach_environment(&mut fixture);
    let ModuleEnvironmentSource::SourceProfile { environment, .. } =
        &mut fixture.manifest.environments[0].source;
    let removed = environment.resources.pop().unwrap();
    fixture
        .manifest
        .files
        .retain(|file| file.path != removed.path);
    fixture
        .manifest
        .provenance
        .license_paths
        .retain(|path| path != &removed.path);
    let mut calls = 0;
    assert!(
        forge_delivery::module_environment::resolve(
            &fixture.manifest,
            fixture.manifest.zones[0].area_id,
            |path, _| {
                calls += 1;
                fs::read(fixture.source.join(path)).map_err(|e| e.to_string())
            }
        )
        .is_err()
    );
    assert_eq!(
        calls, 1,
        "Only the bounded manifest may be read before closure mismatch"
    );
}
