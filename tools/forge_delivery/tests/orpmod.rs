use forge_assets::governed_import::*;
use forge_core::{AssetId, ModuleId, bytes_sha256};
use forge_delivery::{
    archive, install,
    manifest::{Descriptor, Kind, Launch, Product, Rights},
    orpmod,
};
use forge_project::{
    authoring::new_terrain, module_package::*, zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
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
            "forge-orpmod-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        let source = root.join("source");
        fs::create_dir_all(source.join("assets")).unwrap();
        fs::create_dir(source.join("zones")).unwrap();
        let terrain = new_terrain(key, "start", "Gate 4 fixture").unwrap();
        let project_id = terrain.project_id;
        let area_id = terrain.area_id;
        let zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        let archive = zone.archive_bytes().unwrap();
        let mesh = glb();
        let id = AssetId::deterministic(format!("{key}/mesh"));
        let catalog = GovernedImportCatalog {
            schema: GOVERNED_IMPORT_SCHEMA.into(),
            schema_version: GOVERNED_IMPORT_VERSION,
            assets: vec![GovernedImportAsset {
                id,
                name: "Fixture mesh".into(),
                kind: GovernedAssetKind::Mesh,
                payload: GovernedAssetFile {
                    path: "assets/mesh.glb".into(),
                    sha256: bytes_sha256(&mesh),
                    bytes: mesh.len() as u64,
                },
                provenance: AssetProvenance {
                    source: "Forge authored structural test".into(),
                    author: "Forge tests".into(),
                    license: "CC0-1.0".into(),
                    evidence: "LICENSE.txt".into(),
                    acquired_on: "2026-10-15".into(),
                    rights: AssetRights {
                        local_use: true,
                        redistribution: RedistributionRights::Allowed,
                        redistribution_evidence: Some("LICENSE.txt".into()),
                    },
                },
                derived_from: None,
                transformations: vec![],
                authored_changes: false,
                durable_source: None,
                thumbnail: None,
            }],
        };
        let payloads = [
            (
                "assets/catalog.json",
                serde_json::to_vec(&catalog).unwrap(),
                ModuleFileRole::AssetCatalog,
            ),
            ("assets/mesh.glb", mesh, ModuleFileRole::Asset),
            (
                "zones/start.json",
                archive.clone(),
                ModuleFileRole::ZoneArchive,
            ),
            (
                "LICENSE.txt",
                b"Authored test fixture only, CC0-1.0".to_vec(),
                ModuleFileRole::License,
            ),
            (
                "PROVENANCE.txt",
                b"Created in the integration test; no third-party content".to_vec(),
                ModuleFileRole::Provenance,
            ),
            (
                "CREDITS.txt",
                b"Forge tests".to_vec(),
                ModuleFileRole::Credits,
            ),
        ];
        let files = payloads
            .into_iter()
            .map(|(path, bytes, role)| {
                fs::write(source.join(path), &bytes).unwrap();
                ModuleFile {
                    path: path.into(),
                    bytes: bytes.len() as u64,
                    sha256: bytes_sha256(&bytes),
                    role,
                }
            })
            .collect();
        let manifest = ModulePackageManifest {
            schema: MODULE_PACKAGE_SCHEMA.into(),
            version: MODULE_PACKAGE_VERSION,
            project_id,
            module_id: ModuleId::deterministic(key),
            name: key.into(),
            module_version: "1.0.0".into(),
            compatibility: ModuleCompatibility {
                engine_version: orpmod::ENGINE_VERSION.into(),
                module_api_version: orpmod::MODULE_API_VERSION,
            },
            provenance: ModuleProvenance {
                publisher: "Forge tests".into(),
                source: "authored test".into(),
                source_revision: "fixture-1".into(),
                rights: ModuleRights::PrivateLocalOnly,
                license_paths: vec!["LICENSE.txt".into()],
                proof_paths: vec!["PROVENANCE.txt".into()],
                credits_path: "CREDITS.txt".into(),
            },
            asset_catalog_path: "assets/catalog.json".into(),
            files,
            zones: vec![ModuleZone {
                area_id,
                archive_path: "zones/start.json".into(),
                archive_sha256: bytes_sha256(&archive),
                head_hash: zone.head().content_hash().unwrap(),
                gameplay_sha256: None,
                source_blobs: vec![],
            }],
            assets: vec![ModuleAsset {
                asset_id: id,
                file_path: "assets/mesh.glb".into(),
                sha256: catalog.assets[0].payload.sha256,
                dependencies: vec![],
            }],
            gameplay_visuals: Vec::new(),
        };
        let value = Self {
            root,
            source,
            manifest,
        };
        value.save();
        value
    }
    fn save(&self) {
        fs::write(
            self.source.join("module.json"),
            serde_json::to_vec(&self.manifest).unwrap(),
        )
        .unwrap();
    }
    fn descriptor(&self) -> Descriptor {
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: self.manifest.module_id.to_string(),
            label: self.manifest.name.clone(),
            version: self.manifest.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: "fixture-1".into(),
            rights: Rights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            files: vec![],
            product: None,
        }
    }
    fn replace(&mut self, path: &str, bytes: &[u8]) {
        fs::write(self.source.join(path), bytes).unwrap();
        let file = self
            .manifest
            .files
            .iter_mut()
            .find(|f| f.path == path)
            .unwrap();
        file.bytes = bytes.len() as u64;
        file.sha256 = bytes_sha256(bytes);
        self.save();
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

// A structural GLB fixture, not a visible or artistically qualified model.
fn glb() -> Vec<u8> {
    let mut json = br#"{"asset":{"version":"2.0"}}"#.to_vec();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(((20 + json.len()) as u32).to_le_bytes());
    bytes.extend((json.len() as u32).to_le_bytes());
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes
}

#[test]
fn two_real_directories_pack_and_inspect_with_independent_identities() {
    let first = Fixture::new("harbor");
    let second = Fixture::new("mountain");
    assert_ne!(first.manifest.module_id, second.manifest.module_id);
    for f in [&first, &second] {
        assert_eq!(
            orpmod::validate_directory(&f.source, false).unwrap(),
            f.manifest
        );
        let output = f.root.join("module.orpmod");
        orpmod::pack(&f.source, f.descriptor(), &output, false).unwrap();
        assert_eq!(orpmod::inspect(&output, false).unwrap(), f.manifest);
        assert!(orpmod::inspect(&output, true).is_err());
    }
}

#[test]
fn private_rights_cannot_be_promoted_by_a_delivery_descriptor() {
    let f = Fixture::new("rights");
    let mut descriptor = f.descriptor();
    descriptor.rights = Rights::Redistributable;
    assert!(orpmod::pack(&f.source, descriptor, &f.root.join("module.orpmod"), false).is_err());
    assert!(!f.root.join("module.orpmod").exists());
}

#[test]
fn unknown_missing_extra_and_changed_files_fail_admission() {
    let f = Fixture::new("inventory");
    fs::write(f.source.join("extra.txt"), b"extra").unwrap();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
    fs::remove_file(f.source.join("extra.txt")).unwrap();
    fs::write(f.source.join("assets/mesh.glb"), b"different").unwrap();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
    fs::remove_file(f.source.join("assets/mesh.glb")).unwrap();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
}

#[test]
fn executable_disguised_as_credits_and_malformed_glb_are_refused_even_with_exact_hashes() {
    let mut f = Fixture::new("disguised");
    f.replace("CREDITS.txt", b"MZ\x90\x00executable");
    assert!(orpmod::validate_directory(&f.source, false).is_err());
    f.replace("CREDITS.txt", b"Forge tests");
    let mut malformed = glb();
    malformed[4] = 1;
    f.manifest.assets[0].sha256 = bytes_sha256(&malformed);
    let mut catalog: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(f.source.join("assets/catalog.json")).unwrap()).unwrap();
    catalog.assets[0].payload.sha256 = bytes_sha256(&malformed);
    f.replace(
        "assets/catalog.json",
        &serde_json::to_vec(&catalog).unwrap(),
    );
    f.replace("assets/mesh.glb", &malformed);
    assert!(orpmod::validate_directory(&f.source, false).is_err());
}

#[test]
fn catalog_identity_and_runtime_compatibility_are_checked_against_files() {
    let mut f = Fixture::new("refs");
    f.manifest.assets[0].asset_id = AssetId::deterministic("absent-from-catalog");
    f.save();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
    let mut f = Fixture::new("compat");
    f.manifest.compatibility.module_api_version += 1;
    f.save();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
}

#[test]
fn archive_inspection_checks_payloads_and_never_promotes_a_forged_container() {
    let f = Fixture::new("corruption");
    let output = f.root.join("unchecked.orpmod");
    archive::pack(&f.source, f.descriptor(), &output).unwrap();
    assert!(orpmod::inspect(&output, false).is_ok());
    let mut bytes = fs::read(&output).unwrap();
    *bytes.last_mut().unwrap() ^= 1;
    fs::write(&output, bytes).unwrap();
    assert!(orpmod::inspect(&output, false).is_err());
}

fn install_core(f: &Fixture) -> PathBuf {
    let source = f.root.join("applications");
    fs::create_dir(&source).unwrap();
    fs::write(
        source.join("LICENSE.txt"),
        b"Forge test executable placeholder, never launched",
    )
    .unwrap();
    fs::write(source.join("game.bin"), b"test only").unwrap();
    let mut descriptor = f.descriptor();
    descriptor.id = "applications".into();
    descriptor.kind = Kind::Applications;
    let launch = Launch {
        executable: "game.bin".into(),
        args: vec![],
        env: BTreeMap::new(),
        clear_env_prefixes: vec![],
    };
    descriptor.product = Some(Product {
        schema: "forge.delivery.product.v1".into(),
        id: "gate4-fixture".into(),
        title: "Gate 4 fixture".into(),
        apps: BTreeMap::from([("game".into(), launch.clone()), ("editor".into(), launch)]),
        required_packs: vec![],
        optional_packs: vec![],
        authoring_seed: None,
    });
    let applications = f.root.join("applications.forgepack");
    archive::pack(&source, descriptor, &applications).unwrap();
    let root = f.root.join("Installation été");
    let mut plan = install::prepare(&root, &[applications]).unwrap();
    plan.user_root = f
        .root
        .join("Données personnelles")
        .to_string_lossy()
        .into_owned();
    plan.command_id.clear();
    plan.command_id = forge_delivery::hash(&forge_delivery::json(&plan).unwrap());
    install::apply(&plan).unwrap();
    root
}

#[test]
fn semantically_invalid_module_cannot_change_active_installation_or_user_data() {
    let mut f = Fixture::new("activation");
    let root = install_core(&f);
    let before = fs::read(root.join("installation.json")).unwrap();
    let save = f.root.join("Données personnelles/save.json");
    fs::create_dir_all(save.parent().unwrap()).unwrap();
    fs::write(&save, b"player save").unwrap();
    f.manifest.assets[0].asset_id = AssetId::deterministic("missing-catalog-identity");
    f.save();
    let bad = f.root.join("invalid.orpmod");
    archive::pack(&f.source, f.descriptor(), &bad).unwrap();
    let plan = install::prepare(&root, &[bad]).unwrap();
    assert!(
        install::apply(&plan).is_err(),
        "semantic validation must precede installation-head activation"
    );
    assert_eq!(fs::read(root.join("installation.json")).unwrap(), before);
    assert_eq!(fs::read(save).unwrap(), b"player save");
}

#[test]
fn two_valid_modules_install_in_same_client_and_rollback_keeps_user_data() {
    let first = Fixture::new("installed-harbor");
    let second = Fixture::new("installed-mountain");
    let root = install_core(&first);
    let save = first.root.join("Données personnelles/save.json");
    fs::create_dir_all(save.parent().unwrap()).unwrap();
    fs::write(&save, b"player save").unwrap();
    for f in [&first, &second] {
        let output = f.root.join("module.orpmod");
        orpmod::pack(&f.source, f.descriptor(), &output, false).unwrap();
        let plan = install::prepare(&root, &[output]).unwrap();
        let state = install::apply(&plan).unwrap();
        assert!(
            state
                .active
                .packs
                .contains_key(&f.manifest.module_id.to_string())
        );
        assert_eq!(install::apply(&plan).unwrap().revision, state.revision);
    }
    assert_eq!(install::verify(&root).unwrap().active.packs.len(), 2);
    install::apply(&install::prepare_rollback(&root).unwrap()).unwrap();
    assert_eq!(install::verify(&root).unwrap().active.packs.len(), 1);
    assert_eq!(fs::read(save).unwrap(), b"player save");
}

#[test]
fn authored_changes_keep_their_pinned_recipe_in_the_actual_package() {
    let mut f = Fixture::new("authored-source");
    let recipe = br#"{"generator":"forge.test","version":1,"parameters":{"edited":true}}"#;
    let source = GovernedAssetFile {
        path: "assets/edited.recipe.json".into(),
        bytes: recipe.len() as u64,
        sha256: bytes_sha256(recipe),
    };
    fs::write(f.source.join(&source.path), recipe).unwrap();
    f.manifest.files.push(ModuleFile {
        path: source.path.clone(),
        bytes: source.bytes,
        sha256: source.sha256,
        role: ModuleFileRole::AssetAuxiliary,
    });
    let mut catalog: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(f.source.join("assets/catalog.json")).unwrap()).unwrap();
    catalog.assets[0].authored_changes = true;
    catalog.assets[0].transformations.push(AssetTransformation {
        tool: "Forge fixture".into(),
        version: "1".into(),
        parameters_sha256: bytes_sha256(recipe),
    });
    catalog.assets[0].durable_source = Some(DurableAssetSource::Recipe { file: source });
    f.replace(
        "assets/catalog.json",
        &serde_json::to_vec(&catalog).unwrap(),
    );
    let output = f.root.join("authored.orpmod");
    orpmod::pack(&f.source, f.descriptor(), &output, false).unwrap();
    assert_eq!(orpmod::inspect(&output, false).unwrap(), f.manifest);
}

#[test]
fn cli_validates_packs_inspects_and_reports_publication_refusal_as_json() {
    let f = Fixture::new("cli");
    let executable = env!("CARGO_BIN_EXE_forge_delivery");
    let descriptor = f.root.join("descriptor.json");
    fs::write(&descriptor, serde_json::to_vec(&f.descriptor()).unwrap()).unwrap();
    let output = f.root.join("cli.orpmod");
    let result = std::process::Command::new(executable)
        .arg("orpmod-validate")
        .arg(&f.source)
        .output()
        .unwrap();
    assert!(
        result.status.success(),
        "{}",
        String::from_utf8_lossy(&result.stderr)
    );
    let result = std::process::Command::new(executable)
        .arg("orpmod-pack")
        .arg(&f.source)
        .arg(&descriptor)
        .arg(&output)
        .output()
        .unwrap();
    assert!(
        result.status.success(),
        "{}",
        String::from_utf8_lossy(&result.stderr)
    );
    let result = std::process::Command::new(executable)
        .arg("orpmod-inspect")
        .arg(&output)
        .output()
        .unwrap();
    assert!(
        result.status.success(),
        "{}",
        String::from_utf8_lossy(&result.stderr)
    );
    let decoded: ModulePackageManifest = serde_json::from_slice(&result.stdout).unwrap();
    assert_eq!(decoded, f.manifest);
    let result = std::process::Command::new(executable)
        .arg("orpmod-inspect")
        .arg(&output)
        .arg("--publish")
        .output()
        .unwrap();
    assert!(!result.status.success());
    let error: serde_json::Value = serde_json::from_slice(&result.stderr).unwrap();
    assert!(error["error"].as_str().unwrap().contains("private"));
    let before = fs::read(&output).unwrap();
    assert!(orpmod::pack(&f.source, f.descriptor(), &output, false).is_err());
    assert_eq!(fs::read(output).unwrap(), before);
}

#[test]
fn unreferenced_auxiliary_and_external_rights_evidence_are_refused() {
    let mut f = Fixture::new("unreferenced");
    fs::write(f.source.join("assets/orphan.json"), b"{}").unwrap();
    f.manifest.files.push(ModuleFile {
        path: "assets/orphan.json".into(),
        bytes: 2,
        sha256: bytes_sha256(b"{}"),
        role: ModuleFileRole::AssetAuxiliary,
    });
    f.save();
    assert!(orpmod::validate_directory(&f.source, false).is_err());
    let mut f = Fixture::new("external-evidence");
    let mut catalog: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(f.source.join("assets/catalog.json")).unwrap()).unwrap();
    catalog.assets[0].provenance.evidence = "https://example.invalid/not-embedded".into();
    f.replace(
        "assets/catalog.json",
        &serde_json::to_vec(&catalog).unwrap(),
    );
    assert!(orpmod::validate_directory(&f.source, false).is_err());
}

#[test]
fn explicitly_redistributable_authored_fixture_can_pass_publication_checks() {
    let mut f = Fixture::new("publication");
    f.manifest.provenance.rights = ModuleRights::Redistributable;
    f.save();
    let mut descriptor = f.descriptor();
    descriptor.rights = Rights::Redistributable;
    let output = f.root.join("public.orpmod");
    orpmod::pack(&f.source, descriptor, &output, true).unwrap();
    assert_eq!(orpmod::inspect(&output, true).unwrap(), f.manifest);
}

#[test]
fn renaming_a_legacy_container_does_not_make_a_semantic_module() {
    let f = Fixture::new("legacy");
    fs::remove_file(f.source.join("module.json")).unwrap();
    let output = f.root.join("renamed.orpmod");
    archive::pack(&f.source, f.descriptor(), &output).unwrap();
    assert!(archive::inspect(&output).is_err());
    assert!(orpmod::inspect(&output, false).is_err());
}
