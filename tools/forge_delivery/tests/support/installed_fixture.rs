use forge_core::{AreaId, AssetId, ModuleId, bytes_sha256};
use forge_delivery::{archive, install, manifest::*, orpmod};
use forge_project::{
    authoring::new_terrain, module_package::*, zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT: AtomicU64 = AtomicU64::new(0);

#[allow(
    dead_code,
    reason = "Shared fixture fields are used by separate test targets"
)]
pub struct ModuleFixture {
    pub id: ModuleId,
    pub area: AreaId,
    pub archive: Vec<u8>,
}

#[allow(
    dead_code,
    reason = "Shared fixture fields are used by separate test targets"
)]
pub struct Fixture {
    pub root: PathBuf,
    pub install: PathBuf,
    pub user: PathBuf,
    pub first: ModuleFixture,
    pub second: ModuleFixture,
}

impl Fixture {
    pub fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-installed-modules-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        let install = root.join("Installation Élan");
        let user = root.join("Données personnelles");
        let core = root.join("core");
        fs::create_dir(&core).unwrap();
        fs::write(
            core.join("game.bin"),
            b"An inert test executable; never spawn this fixture",
        )
        .unwrap();
        fs::write(core.join("LICENSE.txt"), b"Original fixture CC0-1.0").unwrap();
        let recipe = Launch {
            executable: "game.bin".into(),
            args: vec!["--walk".into()],
            env: BTreeMap::from([(
                "FORGE_TIDEWATER_FORBIDDEN".into(),
                "not-a-module-input".into(),
            )]),
            clear_env_prefixes: vec![],
        };
        let mut applications =
            descriptor("applications", "Installed module test", Kind::Applications);
        applications.product = Some(Product {
            schema: "forge.delivery.product.v1".into(),
            id: "test-client".into(),
            title: "Unchanged client".into(),
            apps: BTreeMap::from([("game".into(), recipe.clone()), ("editor".into(), recipe)]),
            required_packs: vec![],
            optional_packs: vec![],
            authoring_seed: None,
        });
        let package = root.join("applications.forgepack");
        archive::pack(&core, applications, &package).unwrap();
        let mut initial = install::prepare(&install, &[package]).unwrap();
        initial.user_root = user.to_str().unwrap().into();
        initial.command_id.clear();
        initial.command_id = forge_delivery::hash(&forge_delivery::json(&initial).unwrap());
        install::apply(&initial).unwrap();
        let legacy = root.join("legacy");
        fs::create_dir(&legacy).unwrap();
        fs::write(legacy.join("LICENSE.txt"), b"Legacy v1 content").unwrap();
        let package = root.join("legacy.forgepack");
        archive::pack(
            &legacy,
            descriptor("reference-module", "Existing reference", Kind::Module),
            &package,
        )
        .unwrap();
        install::apply(&install::prepare(&install, &[package]).unwrap()).unwrap();
        let first = module(&root, &install, "harbor");
        let second = module(&root, &install, "mountain");
        Self {
            root,
            install,
            user,
            first,
            second,
        }
    }

    pub fn module_root(&self, module: ModuleId) -> PathBuf {
        let state = install::state(&self.install).unwrap();
        install::package_root(&self.install, &state.active.packs[&module.to_string()]).unwrap()
    }

    pub fn core_root(&self) -> PathBuf {
        let state = install::state(&self.install).unwrap();
        install::package_root(&self.install, &state.active.core).unwrap()
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        if self.root.is_absolute() && self.root.parent() == Some(std::env::temp_dir().as_path()) {
            let _ = fs::remove_dir_all(&self.root);
        }
    }
}

fn descriptor(id: &str, label: &str, kind: Kind) -> Descriptor {
    Descriptor {
        schema: "forge.delivery.package.v1".into(),
        id: id.into(),
        label: label.into(),
        version: "1.0.0".into(),
        kind,
        platform: "any".into(),
        source_revision: "authored-test".into(),
        rights: Rights::PrivateLocalOnly,
        license_paths: vec!["LICENSE.txt".into()],
        files: vec![],
        product: None,
    }
}

fn module(root: &Path, installation: &Path, key: &str) -> ModuleFixture {
    let source = root.join(key);
    fs::create_dir_all(source.join("assets")).unwrap();
    fs::create_dir(source.join("zones")).unwrap();
    let terrain = new_terrain(key, "start", key).unwrap();
    let project_id = terrain.project_id;
    let area = terrain.area_id;
    let zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
    let archive = zone.archive_bytes().unwrap();
    let id = ModuleId::deterministic(key);
    let asset = AssetId::deterministic(format!("{key}/mesh"));
    let mut gltf = br#"{"asset":{"version":"2.0"}}"#.to_vec();
    while !gltf.len().is_multiple_of(4) {
        gltf.push(b' ');
    }
    let mut mesh = b"glTF".to_vec();
    mesh.extend(2_u32.to_le_bytes());
    mesh.extend(((20 + gltf.len()) as u32).to_le_bytes());
    mesh.extend((gltf.len() as u32).to_le_bytes());
    mesh.extend(b"JSON");
    mesh.extend(gltf);
    let catalog=serde_json::to_vec(&serde_json::json!({"schema":"forge.governed-import-catalog","schema_version":1,"assets":[{
        "id":asset,"name":key,"kind":"mesh","payload":{"path":"assets/model.glb","sha256":bytes_sha256(&mesh),"bytes":mesh.len()},
        "provenance":{"source":"Original fixture","author":"Forge tests","license":"CC0-1.0","evidence":"LICENSE.txt","acquired_on":"2026-10-15",
            "rights":{"local_use":true,"redistribution":"allowed","redistribution_evidence":"LICENSE.txt"}},
        "derived_from":null,"transformations":[],"authored_changes":false,"durable_source":null,"thumbnail":null}]})).unwrap();
    let mut files = vec![];
    for (path, bytes, role) in [
        (
            "zones/start.json",
            archive.clone(),
            ModuleFileRole::ZoneArchive,
        ),
        ("assets/model.glb", mesh.clone(), ModuleFileRole::Asset),
        ("assets/catalog.json", catalog, ModuleFileRole::AssetCatalog),
        (
            "LICENSE.txt",
            b"Original fixture CC0-1.0".to_vec(),
            ModuleFileRole::License,
        ),
        (
            "PROVENANCE.txt",
            b"Authored integration fixture".to_vec(),
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
        project_id,
        module_id: id,
        name: key.into(),
        module_version: "1.0.0".into(),
        compatibility: ModuleCompatibility {
            engine_version: orpmod::ENGINE_VERSION.into(),
            module_api_version: orpmod::MODULE_API_VERSION,
        },
        provenance: ModuleProvenance {
            publisher: "Forge tests".into(),
            source: "Original fixture".into(),
            source_revision: "authored-test".into(),
            rights: ModuleRights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            proof_paths: vec!["PROVENANCE.txt".into()],
            credits_path: "CREDITS.txt".into(),
        },
        asset_catalog_path: "assets/catalog.json".into(),
        files,
        zones: vec![ModuleZone {
            area_id: area,
            archive_path: "zones/start.json".into(),
            archive_sha256: bytes_sha256(&archive),
            head_hash: zone.head().content_hash().unwrap(),
            gameplay_sha256: None,
            source_blobs: vec![],
        }],
        assets: vec![ModuleAsset {
            asset_id: asset,
            file_path: "assets/model.glb".into(),
            sha256: bytes_sha256(&mesh),
            dependencies: vec![],
        }],
        gameplay_visuals: vec![],
    };
    fs::write(source.join("module.json"), manifest.to_bytes().unwrap()).unwrap();
    let package = root.join(format!("{key}.orpmod"));
    orpmod::pack(
        &source,
        descriptor(&id.to_string(), key, Kind::Module),
        &package,
        false,
    )
    .unwrap();
    install::apply(&install::prepare(installation, &[package]).unwrap()).unwrap();
    ModuleFixture { id, area, archive }
}
