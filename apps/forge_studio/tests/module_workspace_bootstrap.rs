#![cfg(not(target_arch = "wasm32"))]
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, AssetTransformation, DurableAssetSource, GOVERNED_IMPORT_SCHEMA,
    GOVERNED_IMPORT_VERSION, GovernedAssetFile, GovernedAssetKind, GovernedImportAsset,
    GovernedImportCatalog, RedistributionRights,
};
use forge_core::{
    AssetId, CommandId, EntityId, ModuleId, Transform, Vec3, bytes_sha256, canonical_json_sha256,
};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    module_builder::{ExportMetadata, build_zone, build_zone_with_origin},
    module_runtime, orpmod,
};
use forge_project::{
    authoring::new_terrain,
    module_package::{
        MODULE_PACKAGE_SCHEMA, MODULE_PACKAGE_VERSION, ModuleAsset, ModuleCompatibility,
        ModuleFile, ModuleFileRole, ModulePackageManifest, ModuleProvenance, ModuleRights,
        ModuleZone,
    },
    workspace::file_session::FileWorkspaceSession,
    zone_authoring::{
        gameplay::GameplayDocument,
        placements::{ManualPlacement, PlacementEdit},
        session::ZoneEditSession,
    },
};
use forge_studio_frontend::{
    asset_import_native::AssetImportStore,
    module_workspace_bootstrap::{
        BootstrapRequest, MAX_BOOTSTRAP_ASSETS, ModuleOriginReceipt, prepare_module_workspace,
        read_export_origin,
    },
};
use serde_json::json;
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT: AtomicU64 = AtomicU64::new(0);
struct Fixture {
    root: PathBuf,
    package: PathBuf,
    zone: ZoneEditSession,
    manifest: ModulePackageManifest,
    catalog: GovernedImportCatalog,
    payloads: BTreeMap<String, Vec<u8>>,
}
impl Fixture {
    fn new(key: &str, blobs: bool) -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-module-bootstrap-{}-{}-Élan",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        fs::create_dir(root.join("module-source")).unwrap();
        let mut payloads = BTreeMap::new();
        let mut files = Vec::new();
        let (mut zone, blob, gameplay) = fixture_zone(key, blobs);
        let assets = fixture_assets(&mut payloads, &mut files);
        fixture_placement(&mut zone, &assets);
        let archive = zone.archive_bytes().unwrap();
        put(
            &mut payloads,
            &mut files,
            "zones/camp.json",
            archive,
            ModuleFileRole::ZoneArchive,
        );
        let catalog = GovernedImportCatalog {
            schema: GOVERNED_IMPORT_SCHEMA.into(),
            schema_version: GOVERNED_IMPORT_VERSION,
            assets,
        };
        put(
            &mut payloads,
            &mut files,
            "assets/catalog.json",
            serde_json::to_vec(&catalog).unwrap(),
            ModuleFileRole::AssetCatalog,
        );
        let source_blobs = fixture_documents(blob, &mut payloads, &mut files);
        let manifest = fixture_manifest(key, &zone, &gameplay, &catalog, files, source_blobs);
        let package = pack_fixture(&root, &manifest, &payloads, "original", false);
        Self {
            root,
            package,
            zone,
            manifest,
            catalog,
            payloads,
        }
    }
    fn request(&self) -> BootstrapRequest {
        BootstrapRequest {
            package_path: self.package.clone(),
            expected_package_sha256: bytes_sha256(&fs::read(&self.package).unwrap()),
            area_id: self.manifest.zones[0].area_id,
            destination: self.root.join("Workspace Élan"),
        }
    }
    fn repack(&mut self, label: &str, pretty_manifest: bool) {
        let catalog_path = self.manifest.asset_catalog_path.clone();
        let catalog_bytes = serde_json::to_vec(&self.catalog).unwrap();
        let file = self
            .manifest
            .files
            .iter_mut()
            .find(|file| file.path == catalog_path)
            .unwrap();
        file.sha256 = bytes_sha256(&catalog_bytes);
        file.bytes = u64::try_from(catalog_bytes.len()).unwrap();
        self.payloads.insert(catalog_path, catalog_bytes);
        self.package = pack_fixture(
            &self.root,
            &self.manifest,
            &self.payloads,
            label,
            pretty_manifest,
        );
    }
    fn entries(&self) -> Vec<String> {
        let mut paths: Vec<_> = fs::read_dir(&self.root)
            .unwrap()
            .map(|p| p.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        paths.sort();
        paths
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        if self.root.parent() == Some(std::env::temp_dir().as_path()) {
            let _ = fs::remove_dir_all(&self.root);
        }
    }
}

fn fixture_zone(key: &str, blobs: bool) -> (ZoneEditSession, Option<Vec<u8>>, GameplayDocument) {
    let mut terrain = new_terrain(key, "camp", "Le camp original").unwrap();
    let blob = blobs.then(|| vec![0_u8; 256 * 256 * 4]);
    if let Some(bytes) = &blob {
        terrain.schema_version = 4;
        terrain.height_sampling.algorithm = forge_project::HeightSamplingAlgorithm::SampledRasterV1;
        terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
        terrain.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
            schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 256,
            height: 256,
            origin_xz_m: [-256., -256.],
            sample_spacing_m: 2.,
            outside_height_m: 0.,
            encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
            samples_sha256: bytes_sha256(bytes),
        });
    }
    let area_id = terrain.area_id;
    let module_id = ModuleId::deterministic(format!("bootstrap/{key}"));
    let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let player = EntityId::deterministic(format!("{key}/player"));
    let gameplay: GameplayDocument = serde_json::from_value(json!({
        "schema":"forge.gameplay","version":1,"module_id":module_id,"area_id":area_id,"player":player,
        "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
            "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.,"y":1.,"z":0.},
            "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
        "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    zone.execute(
        &zone
            .head()
            .prepare_gameplay(
                Some(gameplay.clone()),
                CommandId::deterministic("bootstrap/gameplay"),
            )
            .unwrap(),
    )
    .unwrap();
    (zone, blob, gameplay)
}

fn fixture_assets(
    payloads: &mut BTreeMap<String, Vec<u8>>,
    files: &mut Vec<ModuleFile>,
) -> Vec<GovernedImportAsset> {
    let blue = AssetId::deterministic("bootstrap/blue");
    let mut assets = Vec::new();
    for (label, kind, data) in [
        ("blue", GovernedAssetKind::Mesh, triangle("blue")),
        ("amber", GovernedAssetKind::Mesh, triangle("amber")),
        ("texture", GovernedAssetKind::Texture, png()),
        (
            "audio",
            GovernedAssetKind::Audio,
            include_bytes!(concat!(
                env!("CARGO_MANIFEST_DIR"),
                "/../../crates/forge_assets/tests/fixtures/media/tone.wav"
            ))
            .to_vec(),
        ),
    ] {
        let extension = match kind {
            GovernedAssetKind::Mesh => "glb",
            GovernedAssetKind::Texture => "png",
            GovernedAssetKind::Audio => "wav",
        };
        let path = format!("assets/{label}.{extension}");
        put(payloads, files, &path, data.clone(), ModuleFileRole::Asset);
        let mut asset = GovernedImportAsset {
            id: AssetId::deterministic(format!("bootstrap/{label}")),
            name: label.into(),
            kind,
            payload: GovernedAssetFile {
                path,
                bytes: u64::try_from(data.len()).unwrap(),
                sha256: bytes_sha256(&data),
            },
            provenance: AssetProvenance {
                source: "Original bootstrap fixture".into(),
                author: "Forge test contributors".into(),
                license: "CC0-1.0".into(),
                evidence: "LICENSE.txt".into(),
                acquired_on: "2026-10-15".into(),
                rights: AssetRights {
                    local_use: true,
                    redistribution: if label == "audio" {
                        RedistributionRights::Unknown
                    } else {
                        RedistributionRights::Allowed
                    },
                    redistribution_evidence: if label == "audio" {
                        None
                    } else {
                        Some("LICENSE.txt".into())
                    },
                },
            },
            derived_from: if label == "amber" { Some(blue) } else { None },
            transformations: vec![],
            authored_changes: kind == GovernedAssetKind::Mesh,
            durable_source: None,
            thumbnail: None,
        };
        if kind == GovernedAssetKind::Mesh {
            fixture_recipe(&mut asset, label, payloads, files);
        }
        assets.push(asset);
    }
    // Deliberately put the child before its parent: traversal must use lineage.
    assets.swap(0, 1);
    assets
}

fn fixture_recipe(
    asset: &mut GovernedImportAsset,
    label: &str,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    files: &mut Vec<ModuleFile>,
) {
    let recipe =
        serde_json::to_vec(&json!({"schema":"original-triangle-recipe","version":1,"color":label}))
            .unwrap();
    let path = format!("recipes/{label}.json");
    let file = GovernedAssetFile {
        path: path.clone(),
        bytes: u64::try_from(recipe.len()).unwrap(),
        sha256: bytes_sha256(&recipe),
    };
    put(
        payloads,
        files,
        &path,
        recipe.clone(),
        ModuleFileRole::AssetAuxiliary,
    );
    asset.durable_source = Some(DurableAssetSource::Recipe { file });
    asset.transformations.push(AssetTransformation {
        tool: "original-fixture".into(),
        version: "1".into(),
        parameters_sha256: bytes_sha256(&recipe),
    });
}

fn fixture_placement(zone: &mut ZoneEditSession, assets: &[GovernedImportAsset]) {
    let blue = AssetId::deterministic("bootstrap/blue");
    let blue_asset = assets.iter().find(|asset| asset.id == blue).unwrap();
    let placements = PlacementEdit::InsertMany {
        placements: vec![ManualPlacement {
            id: EntityId::deterministic("bootstrap/placement"),
            name: "Original blue triangle".into(),
            asset_id: blue,
            asset_hash: blue_asset.payload.sha256,
            physics: None,
            particles: None,
            transform: Transform::from_translation(Vec3::new(1., 0., 1.)),
            locked: false,
        }],
    };
    zone.execute(
        &zone
            .head()
            .prepare_placement(CommandId::deterministic("bootstrap/placement"), placements)
            .unwrap(),
    )
    .unwrap();
}

fn fixture_documents(
    blob: Option<Vec<u8>>,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    files: &mut Vec<ModuleFile>,
) -> Vec<forge_core::ContentHash> {
    for (path, role, data) in [
        (
            "LICENSE.txt",
            ModuleFileRole::License,
            b"Original fixture CC0-1.0".as_slice(),
        ),
        (
            "CREDITS.txt",
            ModuleFileRole::Credits,
            b"Forge test contributors".as_slice(),
        ),
        (
            "provenance/original-dialogue.json",
            ModuleFileRole::Provenance,
            b"{\"source\":\"authored historical-text stand-in\",\"version\":1}".as_slice(),
        ),
    ] {
        put(payloads, files, path, data.to_vec(), role);
    }
    if let Some(bytes) = blob {
        let hash = bytes_sha256(&bytes);
        put(
            payloads,
            files,
            "source-blobs/raster.bin",
            bytes,
            ModuleFileRole::SourceBlob,
        );
        vec![hash]
    } else {
        vec![]
    }
}

fn fixture_manifest(
    key: &str,
    zone: &ZoneEditSession,
    gameplay: &GameplayDocument,
    catalog: &GovernedImportCatalog,
    files: Vec<ModuleFile>,
    source_blobs: Vec<forge_core::ContentHash>,
) -> ModulePackageManifest {
    let terrain = &zone.head().base.terrain;
    let (project_id, area_id) = (terrain.project_id, terrain.area_id);
    let module_id = gameplay.module_id;
    let archive = zone.archive_bytes().unwrap();
    ModulePackageManifest {
        schema: MODULE_PACKAGE_SCHEMA.into(),
        version: MODULE_PACKAGE_VERSION,
        project_id,
        module_id,
        name: format!("Original {key}"),
        module_version: "1.0.0".into(),
        compatibility: ModuleCompatibility {
            engine_version: orpmod::ENGINE_VERSION.into(),
            module_api_version: orpmod::MODULE_API_VERSION,
        },
        provenance: ModuleProvenance {
            publisher: "Forge test contributors".into(),
            source: "Original bootstrap fixture".into(),
            source_revision: "fixture-v1".into(),
            rights: ModuleRights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            proof_paths: vec!["provenance/original-dialogue.json".into()],
            credits_path: "CREDITS.txt".into(),
        },
        asset_catalog_path: "assets/catalog.json".into(),
        files,
        zones: vec![ModuleZone {
            area_id,
            archive_path: "zones/camp.json".into(),
            archive_sha256: bytes_sha256(&archive),
            head_hash: zone.head().content_hash().unwrap(),
            gameplay_sha256: Some(canonical_json_sha256(gameplay).unwrap()),
            source_blobs,
        }],
        assets: catalog
            .assets
            .iter()
            .map(|asset| ModuleAsset {
                asset_id: asset.id,
                file_path: asset.payload.path.clone(),
                sha256: asset.payload.sha256,
                dependencies: asset.derived_from.into_iter().collect(),
            })
            .collect(),
        gameplay_visuals: vec![],
    }
}

fn pack_fixture(
    root: &Path,
    manifest: &ModulePackageManifest,
    payloads: &BTreeMap<String, Vec<u8>>,
    label: &str,
    pretty: bool,
) -> PathBuf {
    let source = root.join("module-source");
    for (path, data) in payloads {
        let output = source.join(path);
        fs::create_dir_all(output.parent().unwrap()).unwrap();
        fs::write(output, data).unwrap();
    }
    fs::write(
        source.join("module.json"),
        if pretty {
            serde_json::to_vec_pretty(manifest).unwrap()
        } else {
            manifest.to_bytes().unwrap()
        },
    )
    .unwrap();
    let package = root.join(format!("{label}.orpmod"));
    orpmod::pack(
        &source,
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: manifest.module_id.to_string(),
            label: manifest.name.clone(),
            version: "1.0.0".into(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: "fixture-v1".into(),
            rights: Rights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            files: vec![],
            product: None,
        },
        &package,
        false,
    )
    .unwrap();
    package
}

fn put(
    payloads: &mut BTreeMap<String, Vec<u8>>,
    files: &mut Vec<ModuleFile>,
    path: &str,
    data: Vec<u8>,
    role: ModuleFileRole,
) {
    files.push(ModuleFile {
        path: path.into(),
        bytes: u64::try_from(data.len()).unwrap(),
        sha256: bytes_sha256(&data),
        role,
    });
    payloads.insert(path.into(), data);
}
fn triangle(label: &str) -> Vec<u8> {
    let mut binary = Vec::new();
    for value in [0_f32, 0., 0., 1., 0., 0., 0., 1., 0.] {
        binary.extend(value.to_le_bytes());
    }
    let mut metadata=serde_json::to_vec(&json!({"asset":{"version":"2.0","generator":label},"buffers":[{"byteLength":36}],"bufferViews":[{"buffer":0,"byteLength":36}],
        "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}],"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0})).unwrap();
    while !metadata.len().is_multiple_of(4) {
        metadata.push(b' ');
    }
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(
        u32::try_from(28 + metadata.len() + binary.len())
            .unwrap()
            .to_le_bytes(),
    );
    bytes.extend(u32::try_from(metadata.len()).unwrap().to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(metadata);
    bytes.extend(u32::try_from(binary.len()).unwrap().to_le_bytes());
    bytes.extend(b"BIN\0");
    bytes.extend(binary);
    bytes
}
fn png() -> Vec<u8> {
    vec![
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6,
        0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 144, 139, 90, 240,
        31, 0, 3, 202, 2, 24, 218, 135, 235, 249, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130,
    ]
}

/// This setup deliberately does not depend on the bootstrap implementation.
fn origin_fixture(f: &Fixture) -> (PathBuf, forge_project::workspace::ModuleOriginPin) {
    let root = f.root.join("Origin Élan");
    let origin = root
        .join("module-imports")
        .join(f.manifest.module_id.to_string());
    fs::create_dir_all(&origin).unwrap();
    let package = fs::read(&f.package).unwrap();
    let receipt = ModuleOriginReceipt {
        schema: "forge.module-import-origin".into(),
        version: 1,
        module_id: f.manifest.module_id,
        project_id: f.manifest.project_id,
        package_sha256: bytes_sha256(&package),
        package_bytes: u64::try_from(package.len()).unwrap(),
        module_manifest_sha256: bytes_sha256(
            &fs::read(f.root.join("module-source/module.json")).unwrap(),
        ),
        origin_rights: f.manifest.provenance.rights,
    };
    let receipt = serde_json::to_vec(&receipt).unwrap();
    fs::write(origin.join("original.orpmod"), &package).unwrap();
    fs::write(origin.join("receipt.json"), &receipt).unwrap();
    (
        root,
        forge_project::workspace::ModuleOriginPin {
            package_sha256: bytes_sha256(&package),
            package_bytes: u64::try_from(package.len()).unwrap(),
            receipt_sha256: bytes_sha256(&receipt),
        },
    )
}

#[test]
fn origin_reader_returns_the_exact_admitted_original_buffer() {
    let mut f = Fixture::new("origin-buffer", false);
    f.repack("noncanonical-manifest", true);
    let (root, pin) = origin_fixture(&f);
    let actual = read_export_origin(&root, f.manifest.module_id, pin).unwrap();
    assert_eq!(actual.package_bytes.as_ref(), fs::read(&f.package).unwrap());
    assert_eq!(actual.expected_package_sha256, pin.package_sha256);
}

#[test]
fn missing_or_corrupt_origin_receipt_is_never_a_new_originless_module() {
    let f = Fixture::new("origin-receipt", false);
    let (root, pin) = origin_fixture(&f);
    let receipt = root
        .join("module-imports")
        .join(f.manifest.module_id.to_string())
        .join("receipt.json");
    fs::write(&receipt, b"{}").unwrap();
    let error = read_export_origin(&root, f.manifest.module_id, pin)
        .err()
        .expect("corrupt receipt refused");
    assert!(error.contains("receipt"), "{error}");
    fs::remove_file(&receipt).unwrap();
    let error = read_export_origin(&root, f.manifest.module_id, pin)
        .err()
        .expect("missing receipt refused");
    assert!(error.contains("receipt"), "{error}");
}

#[test]
fn origin_reader_refuses_wrong_identity_even_after_receipt_is_rehashed() {
    let f = Fixture::new("origin-identity", false);
    let (root, mut pin) = origin_fixture(&f);
    let receipt = root
        .join("module-imports")
        .join(f.manifest.module_id.to_string())
        .join("receipt.json");
    let mut value: serde_json::Value =
        serde_json::from_slice(&fs::read(&receipt).unwrap()).unwrap();
    value["module_id"] = serde_json::to_value(ModuleId::deterministic("wrong-module")).unwrap();
    let bytes = serde_json::to_vec(&value).unwrap();
    fs::write(&receipt, &bytes).unwrap();
    pin.receipt_sha256 = bytes_sha256(&bytes);
    let error = read_export_origin(&root, f.manifest.module_id, pin)
        .err()
        .expect("wrong identity refused");
    assert!(error.contains("identity"), "{error}");
    assert!(read_export_origin(&root, ModuleId::deterministic("different-path"), pin).is_err());
}

#[test]
fn two_original_modules_become_reopenable_managed_workspaces_with_all_assets_and_sources() {
    for key in ["first-adventure", "second-adventure"] {
        let f = Fixture::new(key, false);
        let request = f.request();
        let report = prepare_module_workspace(&request).unwrap();
        assert_eq!(report.workspace_root, request.destination);
        assert_eq!(report.package_sha256, request.expected_package_sha256);
        assert_eq!(report.module_id, f.manifest.module_id);
        assert_eq!(report.project_id, f.manifest.project_id);
        assert_eq!(report.imported_assets, 4);
        assert_eq!(
            fs::read(&report.preserved_package).unwrap(),
            fs::read(&f.package).unwrap()
        );
        for (path, data) in &f.payloads {
            assert_eq!(
                fs::read(report.preserved_payload_root.join(path)).unwrap(),
                *data
            );
        }
        let workspace = FileWorkspaceSession::open(&report.workspace_root).unwrap();
        let reopened = workspace.read_zone(report.area_id).unwrap();
        assert_eq!(
            reopened.archive_bytes().unwrap(),
            f.zone.archive_bytes().unwrap()
        );
        assert_eq!(
            reopened.head().content_hash().unwrap(),
            report.zone_head_sha256
        );
        assert_eq!(
            workspace.session().head().projects[0].modules[0].id,
            f.manifest.module_id
        );
        drop(workspace);
        let store = AssetImportStore::new(&report.workspace_root.join("asset-imports")).unwrap();
        let state = store.state().unwrap();
        let catalog = state.catalog.unwrap();
        assert_eq!(state.catalog_sha256, Some(report.import_catalog_sha256));
        for original in &f.catalog.assets {
            let asset = catalog
                .assets
                .iter()
                .find(|asset| asset.id == original.id)
                .unwrap();
            assert_eq!(asset.kind, original.kind);
            assert_eq!(asset.payload.sha256, original.payload.sha256);
            assert_eq!(asset.derived_from, original.derived_from);
            assert_eq!(
                asset.provenance.rights.redistribution,
                original.provenance.rights.redistribution
            );
            if asset.kind == GovernedAssetKind::Mesh {
                assert!(
                    store
                        .lookup_model(asset.id, asset.payload.sha256)
                        .unwrap()
                        .is_some()
                );
            }
            if let Some(DurableAssetSource::Recipe { file }) = &original.durable_source {
                let Some(DurableAssetSource::Recipe { file: actual }) = &asset.durable_source
                else {
                    panic!("recipe lost")
                };
                assert_eq!(actual.sha256, file.sha256);
            }
        }
    }
}

#[test]
fn managed_reopen_export_preserves_unplaced_assets_lineage_pins_and_original_rights() {
    let f = Fixture::new("roundtrip", false);
    let report = prepare_module_workspace(&f.request()).unwrap();
    let workspace = FileWorkspaceSession::open(&report.workspace_root).unwrap();
    let zone = workspace.read_zone(report.area_id).unwrap();
    drop(workspace);
    let store = AssetImportStore::new(&report.workspace_root.join("asset-imports")).unwrap();
    let snapshot = store.export_snapshot(&[], false).unwrap();
    assert!(
        store.export_snapshot(&[], true).is_err(),
        "unknown audio rights must not be elevated"
    );
    let exported = build_zone(
        &f.root.join("Reexports"),
        &zone.archive_bytes().unwrap(),
        zone.head().content_hash().unwrap(),
        ExportMetadata {
            name: "Reopened original".into(),
            version: "1.0.1".into(),
            publisher: "Forge test contributors".into(),
            license_text: "Local regression module".into(),
            rights: ModuleRights::PrivateLocalOnly,
        },
        snapshot.catalog,
        snapshot.files,
    )
    .unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        report.area_id,
        Some(&f.root.join("runtime-cache")),
    )
    .unwrap();
    assert_eq!(loaded.manifest.assets.len(), 4);
    assert_eq!(loaded.manifest.module_id, f.manifest.module_id);
    for original in &f.catalog.assets {
        let asset = loaded
            .manifest
            .assets
            .iter()
            .find(|asset| asset.asset_id == original.id)
            .unwrap();
        assert_eq!(asset.sha256, original.payload.sha256);
        assert_eq!(
            loaded
                .read_payload(&asset.file_path, 256 * 1024 * 1024)
                .unwrap(),
            f.payloads[&original.payload.path]
        );
    }
}

#[test]
fn existing_destination_is_never_overwritten_and_no_sibling_stage_is_created() {
    let f = Fixture::new("existing", false);
    let request = f.request();
    fs::create_dir(&request.destination).unwrap();
    fs::write(request.destination.join("keep.txt"), b"keep").unwrap();
    let before = f.entries();
    let error = prepare_module_workspace(&request).unwrap_err();
    assert!(
        error.contains("destination") || error.contains("exist"),
        "{error}"
    );
    assert_eq!(before, f.entries());
    assert_eq!(
        fs::read(request.destination.join("keep.txt")).unwrap(),
        b"keep"
    );
}

#[test]
fn stale_package_pin_and_corrupt_transport_are_refused_before_writes() {
    let f = Fixture::new("wrong-pin", false);
    let mut request = f.request();
    request.expected_package_sha256 = bytes_sha256(b"stale");
    let before = f.entries();
    let error = prepare_module_workspace(&request).unwrap_err();
    assert!(
        error.contains("package") && error.contains("pin"),
        "{error}"
    );
    assert_eq!(before, f.entries());
    let mut bytes = fs::read(&f.package).unwrap();
    bytes.extend(b"trailing corruption");
    fs::write(&f.package, &bytes).unwrap();
    request.expected_package_sha256 = bytes_sha256(&bytes);
    assert!(prepare_module_workspace(&request).is_err());
    assert_eq!(before, f.entries());
    assert!(!request.destination.exists());
}

#[test]
fn admitted_source_blob_profile_is_refused_explicitly_without_any_workspace_writes() {
    let f = Fixture::new("source-blob", true);
    assert!(orpmod::inspect(&f.package, false).is_ok());
    let before = f.entries();
    let request = f.request();
    let error = prepare_module_workspace(&request).unwrap_err();
    assert!(
        error.to_ascii_lowercase().contains("source blob"),
        "{error}"
    );
    assert_eq!(before, f.entries());
    assert!(!request.destination.exists());
}

#[test]
fn relative_destination_is_refused_before_writes() {
    let f = Fixture::new("relative", false);
    let mut request = f.request();
    request.destination = Path::new("bootstrap-relative-target").to_path_buf();
    let before = f.entries();
    let error = prepare_module_workspace(&request).unwrap_err();
    assert!(error.contains("absolute"), "{error}");
    assert_eq!(before, f.entries());
}

#[test]
fn profile_budget_is_refused_before_stage_and_does_not_exhaust_revision_history() {
    let mut f = Fixture::new("profile-budget", false);
    let original = f
        .catalog
        .assets
        .iter()
        .find(|asset| asset.name == "blue")
        .unwrap()
        .clone();
    for index in f.catalog.assets.len()..=MAX_BOOTSTRAP_ASSETS {
        let mut asset = original.clone();
        asset.id = AssetId::deterministic(format!("budget/{index}"));
        asset.name = format!("Budget asset {index}");
        asset.payload.path = format!("assets/budget-{index}.glb");
        let data = f.payloads[&original.payload.path].clone();
        put(
            &mut f.payloads,
            &mut f.manifest.files,
            &asset.payload.path,
            data,
            ModuleFileRole::Asset,
        );
        f.manifest.assets.push(ModuleAsset {
            asset_id: asset.id,
            file_path: asset.payload.path.clone(),
            sha256: asset.payload.sha256,
            dependencies: vec![],
        });
        f.catalog.assets.push(asset);
    }
    f.repack("over-budget", false);
    assert!(
        orpmod::inspect(&f.package, false).is_ok(),
        "valid package outside producer profile"
    );
    let before = f.entries();
    let error = prepare_module_workspace(&f.request()).unwrap_err();
    assert!(error.contains("128") && error.contains("asset"), "{error}");
    assert_eq!(before, f.entries());
}

#[test]
fn canonical_origin_prevents_publication_with_cc0_assets_and_preserves_original_documents() {
    use forge_project::authoring::{
        AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest,
    };
    let mut f = Fixture::new("private-origin-public-assets", false);
    for asset in &mut f.catalog.assets {
        asset.provenance.rights.redistribution = RedistributionRights::Allowed;
        asset.provenance.rights.redistribution_evidence = Some("LICENSE.txt".into());
    }
    f.repack("all-cc0-assets", true);
    let report = prepare_module_workspace(&f.request()).unwrap();
    let mut host = AuthoringHost::new(&report.workspace_root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    host.execute(AuthoringRequest::OpenManaged {
        area_id: report.area_id,
    })
    .unwrap();
    let pin = host
        .game_module_origin(report.zone_head_sha256)
        .unwrap()
        .expect("canonical origin anchor");
    let archive = host.game_archive(report.zone_head_sha256).unwrap();
    drop(host);
    let store = AssetImportStore::new(&report.workspace_root.join("asset-imports")).unwrap();
    let snapshot = store.export_snapshot(&[], true).unwrap();
    let metadata = |rights| ExportMetadata {
        name: "Retouched original".into(),
        version: "1.0.1".into(),
        publisher: "Test contributors".into(),
        license_text: "New edits CC0-1.0".into(),
        rights,
    };
    let output = f.root.join("origin-exports");
    let error = build_zone_with_origin(
        &output,
        &archive,
        report.zone_head_sha256,
        metadata(ModuleRights::Redistributable),
        snapshot.catalog.clone(),
        snapshot.files.clone(),
        Some(read_export_origin(&report.workspace_root, report.module_id, pin).unwrap()),
    )
    .unwrap_err();
    assert!(
        error.contains("origin") && error.contains("private"),
        "{error}"
    );
    assert!(!output.exists());
    let exported = build_zone_with_origin(
        &output,
        &archive,
        report.zone_head_sha256,
        metadata(ModuleRights::PrivateLocalOnly),
        snapshot.catalog,
        snapshot.files,
        Some(read_export_origin(&report.workspace_root, report.module_id, pin).unwrap()),
    )
    .unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        report.area_id,
        Some(&f.root.join("origin-runtime")),
    )
    .unwrap();
    for path in [
        "LICENSE.txt",
        "CREDITS.txt",
        "provenance/original-dialogue.json",
    ] {
        assert_eq!(
            loaded
                .read_payload(&format!("_forge-export/origin/files/{path}"), 1024 * 1024)
                .unwrap(),
            f.payloads[path]
        );
    }
    assert_eq!(
        loaded
            .read_payload("_forge-export/origin/module.json", 1024 * 1024)
            .unwrap(),
        fs::read(f.root.join("module-source/module.json")).unwrap()
    );
}

#[test]
fn legacy_managed_module_origin_lookup_checks_the_active_snapshot_before_returning_none() {
    use forge_core::document_store::DocumentStore;
    use forge_project::{
        authoring::{AuthoringRequest, host::AuthoringHost, workspace::WorkspaceHostRequest},
        workspace::session::{WorkspaceAction, WorkspaceRequest},
    };
    let f = Fixture::new("legacy-origin-lookup", false);
    let root = f.root.join("Legacy workspace");
    fs::create_dir(&root).unwrap();
    let area = f.manifest.zones[0].area_id;
    let directory = root.join(area.to_string());
    fs::create_dir(&directory).unwrap();
    let store = DocumentStore::open(&directory).unwrap();
    store.save(None, &f.zone.archive_bytes().unwrap()).unwrap();
    drop(store);
    let mut workspace = FileWorkspaceSession::create(&root).unwrap();
    for (key, action) in [
        (
            "legacy/project",
            WorkspaceAction::CreateProject {
                id: f.manifest.project_id,
                name: "Project".into(),
            },
        ),
        (
            "legacy/module",
            WorkspaceAction::CreateModule {
                project_id: f.manifest.project_id,
                id: f.manifest.module_id,
                name: "Module".into(),
            },
        ),
    ] {
        let request = WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_revision: workspace.session().head().revision,
            base_hash: workspace.session().head().content_hash().unwrap(),
            action,
        };
        workspace.execute(&request).unwrap();
    }
    let review = workspace
        .review_adoption(
            area,
            f.manifest.project_id,
            f.manifest.module_id,
            bytes_sha256(&f.zone.archive_bytes().unwrap()),
        )
        .unwrap();
    workspace.adopt(&review.request).unwrap();
    workspace.save().unwrap();
    drop(workspace);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(WorkspaceHostRequest::Open).unwrap();
    host.execute(AuthoringRequest::OpenManaged { area_id: area })
        .unwrap();
    assert!(
        host.game_module_origin(f.zone.head().content_hash().unwrap())
            .unwrap()
            .is_none()
    );
    assert!(
        host.game_module_origin(bytes_sha256(b"stale head"))
            .is_err()
    );
}
