#[path = "support/installed_fixture.rs"]
#[allow(
    dead_code,
    reason = "the shared fixture exposes helpers used by other test targets"
)]
mod fixture;
use forge_core::{ModuleId, ProjectId};
use forge_delivery::{
    archive, install,
    manifest::{
        Descriptor, Entry, Kind, Launch, PackRef, Product, Rights, STANDALONE_GAME_MANIFEST,
        STANDALONE_PRODUCT_SCHEMA, Seed,
    },
};
use std::{collections::BTreeMap, fs};

fn recipe(executable: &str) -> Launch {
    Launch {
        executable: executable.into(),
        args: Vec::new(),
        env: BTreeMap::new(),
        clear_env_prefixes: Vec::new(),
    }
}

fn launcher_recipe(executable: &str) -> Launch {
    let mut launch = recipe(executable);
    launch.args = vec!["--root".into(), "{install}".into()];
    launch
}

fn descriptor() -> Descriptor {
    let project = ProjectId::deterministic("standalone-contract");
    let module = ModuleId::deterministic("standalone-contract");
    let files = [
        "launcher.bin",
        "module.bin",
        STANDALONE_GAME_MANIFEST,
        "LICENSE.txt",
    ]
    .into_iter()
    .map(|path| Entry {
        path: path.into(),
        bytes: 1,
        sha256: "0".repeat(64),
    })
    .collect();
    Descriptor {
        schema: "forge.delivery.package.v1".into(),
        id: project.to_string(),
        label: "Jeu autonome".into(),
        version: "1.0.0".into(),
        kind: Kind::Applications,
        platform: "any".into(),
        source_revision: "workspace-hash".into(),
        rights: Rights::PrivateLocalOnly,
        license_paths: vec!["LICENSE.txt".into()],
        files,
        product: Some(Product {
            schema: STANDALONE_PRODUCT_SCHEMA.into(),
            id: project.to_string(),
            title: "Jeu autonome".into(),
            apps: BTreeMap::from([
                ("launcher".into(), launcher_recipe("launcher.bin")),
                ("module-game".into(), recipe("module.bin")),
            ]),
            required_packs: vec![PackRef {
                id: module.to_string(),
                manifest_sha256: "1".repeat(64),
                kind: Kind::Module,
            }],
            optional_packs: Vec::new(),
            authoring_seed: None,
        }),
    }
}

#[test]
fn standalone_definition_has_a_distinct_version_without_weakening_v1() {
    let standalone = descriptor();
    standalone.validate().unwrap();
    let serialized = serde_json::to_vec(&standalone).unwrap();
    let decoded: Descriptor = serde_json::from_slice(&serialized).unwrap();
    decoded.validate().unwrap();

    let mut legacy = descriptor();
    legacy
        .files
        .retain(|entry| entry.path != STANDALONE_GAME_MANIFEST);
    let product = legacy.product.as_mut().unwrap();
    product.schema = "forge.delivery.product.v1".into();
    product.id = "legacy-product".into();
    product.apps = BTreeMap::from([
        ("game".into(), recipe("module.bin")),
        ("editor".into(), recipe("launcher.bin")),
    ]);
    legacy.validate().unwrap();
    legacy.validate_host().unwrap();
    legacy.product.as_mut().unwrap().apps.remove("editor");
    assert!(legacy.validate().is_err());
    legacy.product.as_mut().unwrap().schema = "forge.delivery.product.v3".into();
    assert!(legacy.validate().is_err());
}

#[test]
fn standalone_definition_refuses_foreign_or_ambiguous_shapes() {
    let mut bad = descriptor();
    bad.product.as_mut().unwrap().id = "not-a-project-id".into();
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product.as_mut().unwrap().id = "00000000-0000-0000-0000-000000000000".into();
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product.as_mut().unwrap().apps.remove("module-game");
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("launcher")
        .unwrap()
        .args
        .clear();
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product
        .as_mut()
        .unwrap()
        .apps
        .insert("editor".into(), recipe("module.bin"));
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    let pack_id = bad.product.as_ref().unwrap().required_packs[0].id.clone();
    bad.product.as_mut().unwrap().authoring_seed = Some(Seed {
        pack_id,
        path: "workspace".into(),
    });
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product
        .as_mut()
        .unwrap()
        .optional_packs
        .push("unexpected".into());
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product.as_mut().unwrap().required_packs.clear();
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("module-game")
        .unwrap()
        .args
        .push("--module".into());
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("launcher")
        .unwrap()
        .env
        .insert("OPENRPG_ASSET_VAULT".into(), "{user}".into());
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.files
        .retain(|entry| entry.path != STANDALONE_GAME_MANIFEST);
    assert!(bad.validate().is_err());
    let mut bad = descriptor();
    bad.files
        .iter_mut()
        .find(|entry| entry.path == STANDALONE_GAME_MANIFEST)
        .unwrap()
        .path = "Game/Launcher.json".into();
    assert!(bad.validate().is_err());
}

#[test]
fn standalone_declared_environment_keeps_paths_within_declared_roots() {
    let allowed = [
        ("LANG", "fr_FR.UTF-8"),
        ("GRAPHICS_MODE", "balanced"),
        ("FILTER", "module/terrain=info"),
        ("UPDATE_URL", "https://example.invalid/optional"),
        ("CONFIG_FILE", "{app}/game/launcher.json"),
        ("CACHE_DIR", "{user}/game-cache"),
        ("PATH", "{app}/bin:{install}/bin"),
        ("PATH", r"{app}\bin;{install}\bin"),
    ];
    for (key, value) in allowed {
        let mut candidate = descriptor();
        candidate
            .product
            .as_mut()
            .unwrap()
            .apps
            .get_mut("launcher")
            .unwrap()
            .env
            .insert(key.into(), value.into());
        assert!(
            candidate.validate().is_ok(),
            "valid environment {key}={value}"
        );
    }
    let rejected = [
        ("PATH", "E:/OpenRPG_Forge/.build/tools"),
        ("PATH", r"C:\Users\karen\checkout\bin"),
        ("DATA_DIR", r"\\server\shared\data"),
        ("DATA_DIR", "/home/karen/OpenRPG_Forge"),
        ("OPTIONS", "--config=/home/karen/project"),
        ("OPTIONS", "--config=E:/project"),
        ("OPTIONS", r"--config=C:checkout\config.json"),
        ("OPTIONS", "prefix:C:/checkout"),
        ("OPTIONS", "prefix:/home/karen/project"),
        ("OPTIONS", "--config=\"/home/karen/project\""),
        ("OPTIONS", r#"--config="C:\checkout\config.json""#),
        ("OPTIONS", "--config=\"../checkout/config.json\""),
        ("OPTIONS", "--config=~/checkout/config.json"),
        ("PATH", "{app}/bin:/usr/local/bin"),
        ("CACHE_ROOT", "{user}/../outside"),
        ("CACHE_ROOT", r"{user}\..\outside"),
        ("CONFIG_FILE", "../checkout/config.json"),
        ("RESOURCE", "file:///home/karen/checkout/data"),
    ];
    for (key, value) in rejected {
        let mut candidate = descriptor();
        candidate
            .product
            .as_mut()
            .unwrap()
            .apps
            .get_mut("launcher")
            .unwrap()
            .env
            .insert(key.into(), value.into());
        assert!(
            candidate.validate().is_err(),
            "external environment {key}={value}"
        );
    }
}

#[test]
fn standalone_installation_requires_its_module_before_writes() {
    let temporary = std::env::temp_dir().canonicalize().unwrap();
    let root = temporary.join(format!("forge-standalone-product-{}", std::process::id()));
    fs::create_dir(&root).unwrap();
    let root = root.canonicalize().unwrap();
    assert_eq!(root.parent(), Some(temporary.as_path()));
    let source = root.join("source");
    fs::create_dir_all(source.join("game")).unwrap();
    for (path, bytes) in [
        ("launcher.bin", b"launcher".as_slice()),
        ("module.bin", b"module".as_slice()),
        (STANDALONE_GAME_MANIFEST, b"{}".as_slice()),
        ("LICENSE.txt", b"fixture".as_slice()),
    ] {
        fs::write(source.join(path), bytes).unwrap();
    }
    let mut input = descriptor();
    input.files.clear();
    let package = root.join("game.forgepack");
    archive::pack(&source, input, &package).unwrap();
    let install_root = root.join("installation");
    let error = install::prepare(&install_root, &[package]).unwrap_err();
    assert!(error.contains("Pack requis absent"));
    assert!(!install_root.exists());
    fs::remove_dir_all(&root).unwrap();
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the admission scenario verifies real packages and their exact module closure"
)]
fn installed_v2_reader_verifies_exact_module_closure_before_any_activation() {
    use forge_core::ContentHash;
    use forge_delivery::{
        module_runtime,
        standalone::{VerifiedGameApplication, VerifiedGameModule, validate_game_closure},
    };
    use forge_project::{
        VisualQualityTier,
        workspace::{
            WorkspaceHead, ZoneReference,
            launcher::{
                DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent,
                GameLocation, PlayerDefaults,
            },
        },
    };

    let fixture = fixture::Fixture::new();
    let loaded = module_runtime::load(
        &fixture.module_root(fixture.first.id),
        fixture.first.area,
        None,
    )
    .unwrap();
    let module = &loaded.manifest;
    let project = module.project_id;
    let mut head = WorkspaceHead::default()
        .with_project(project, "Projet".into())
        .unwrap()
        .with_module(project, module.module_id, "Module".into())
        .unwrap();
    head.projects[0].modules[0].zones.push(ZoneReference {
        area_id: fixture.first.area,
        archive_hash: module.zones[0].archive_sha256,
        head_hash: module.zones[0].head_hash,
    });
    let configuration = GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: "Jeu autonome".into(),
        summary: String::new(),
        icon: None,
        logo: None,
        background: None,
        contents: vec![GameContent {
            module_id: module.module_id,
            areas: vec![fixture.first.area],
        }],
        entry: GameLocation {
            module_id: module.module_id,
            area_id: fixture.first.area,
        },
        defaults: PlayerDefaults {
            quality: VisualQualityTier::High,
            display_mode: DisplayMode::Borderless,
            master_volume: 80,
            music_volume: 60,
            reduce_motion: false,
            subtitles: true,
        },
    };
    let head = head
        .with_game_configuration(project, None, Some(configuration))
        .unwrap();
    let game = head.derive_game_manifest(project).unwrap();
    let encoded = serde_json::to_vec(&game).unwrap();
    let decoded =
        forge_project::workspace::launcher::DerivedGameManifest::from_bytes(&encoded).unwrap();
    assert_eq!(decoded.game_id(), project);
    let mut tampered: serde_json::Value = serde_json::from_slice(&encoded).unwrap();
    tampered["save_namespace"] = "../another-game".into();
    assert!(
        forge_project::workspace::launcher::DerivedGameManifest::from_bytes(
            &serde_json::to_vec(&tampered).unwrap()
        )
        .is_err()
    );
    tampered["save_namespace"] = game.save_namespace().into();
    tampered["configuration"]["defaults"]["master_volume"] = 101.into();
    assert!(
        forge_project::workspace::launcher::DerivedGameManifest::from_bytes(
            &serde_json::to_vec(&tampered).unwrap()
        )
        .is_err()
    );

    let state = install::state(&fixture.install).unwrap();
    let revision = state.active.packs[&fixture.first.id.to_string()]
        .revision
        .clone();
    let mut app = descriptor();
    let product = app.product.as_mut().unwrap();
    product.id = project.to_string();
    product.title = game.configuration().title.clone();
    product.required_packs[0].id = fixture.first.id.to_string();
    product.required_packs[0].manifest_sha256 = revision.clone();
    app.files.clear();
    let source = fixture.root.join("v2-app-source");
    fs::create_dir_all(source.join("game")).unwrap();
    fs::write(source.join(STANDALONE_GAME_MANIFEST), &encoded).unwrap();
    fs::write(source.join("launcher.bin"), b"launcher").unwrap();
    fs::write(source.join("module.bin"), b"module").unwrap();
    fs::write(source.join("LICENSE.txt"), b"fixture").unwrap();
    let package = fixture.root.join("v2-app.forgepack");
    let (_, app_revision) = archive::pack(&source, app, &package).unwrap();
    let installed = fixture.root.join("v2-app-verified");
    archive::extract(&package, &installed, &app_revision).unwrap();
    let application = VerifiedGameApplication::read(&installed, &app_revision).unwrap();
    let selected =
        VerifiedGameModule::read(&fixture.module_root(fixture.first.id), &revision).unwrap();
    validate_game_closure(&application, &[selected]).unwrap();
    let second_revision = &state.active.packs[&fixture.second.id.to_string()].revision;
    let foreign =
        VerifiedGameModule::read(&fixture.module_root(fixture.second.id), second_revision).unwrap();
    assert!(validate_game_closure(&application, &[foreign]).is_err());
    assert!(
        VerifiedGameModule::read(
            &fixture.module_root(fixture.first.id),
            &ContentHash::from_bytes([0; 32]).to_string()
        )
        .is_err()
    );
    fs::write(installed.join(STANDALONE_GAME_MANIFEST), b"{}").unwrap();
    assert!(VerifiedGameApplication::read(&installed, &app_revision).is_err());
}
