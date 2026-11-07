#![cfg(not(target_arch = "wasm32"))]
#[path = "support/standalone_fixture.rs"]
mod fixture;
#[path = "game_builder/presentation.rs"]
mod presentation;

use forge_core::{ContentHash, bytes_sha256};
use forge_delivery::{
    archive,
    game_builder::{
        GameKitManifest, VerifiedGameKit, apply_candidate_install, build_candidate,
        inspect_candidate, prepare_candidate_install,
    },
    install,
    manifest::{Entry, Rights},
    module_builder::ExportedModule,
    standalone,
};
use forge_project::{
    VisualQualityTier,
    workspace::{
        WorkspaceHead, ZoneReference,
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults,
        },
    },
};
use std::{fs, path::Path};

fn put(root: &Path, path: &str, bytes: &[u8]) -> Entry {
    let destination = root.join(path);
    fs::create_dir_all(destination.parent().unwrap()).unwrap();
    fs::write(&destination, bytes).unwrap();
    Entry {
        path: path.into(),
        bytes: bytes.len() as u64,
        sha256: bytes_sha256(bytes).to_string(),
    }
}

fn kit(root: &Path) -> (GameKitManifest, Vec<u8>) {
    fs::create_dir(root).unwrap();
    let mut files = vec![
        put(root, "bin/launcher", b"inert launcher fixture"),
        put(root, "bin/runtime", b"inert runtime fixture"),
        put(
            root,
            "licenses/THIRD_PARTY_NOTICES.md",
            b"Fixture notice [licence](third_party/example/LICENSE)",
        ),
        put(
            root,
            "licenses/third_party/example/LICENSE",
            b"fixture terms",
        ),
    ];
    let text = b"fixture dependency licence";
    let text_hash = bytes_sha256(text).to_string();
    files.push(put(
        root,
        &format!("licenses/rust-runtime-candidate/texts/{text_hash}.txt"),
        text,
    ));
    let index = serde_json::to_vec(&serde_json::json!({
        "schema":"forge.rust-dependency-notices.v2", "notices":[format!("texts/{text_hash}.txt")]
    }))
    .unwrap();
    let base = bytes_sha256(&index).to_string();
    files.push(put(
        root,
        "licenses/rust-runtime-candidate/INDEX.json",
        &index,
    ));
    let supplement = serde_json::to_vec(
        &serde_json::json!({"schema":"forge.rust-notice-supplements.v1", "base_index_sha256":base}),
    )
    .unwrap();
    let supplement_hash = bytes_sha256(&supplement).to_string();
    files.push(put(
        root,
        "licenses/rust-runtime-candidate/SUPPLEMENT-NOTICES.json",
        &supplement,
    ));
    let forge = serde_json::to_vec(&serde_json::json!({"schema":"forge.original-code-notices.v1", "base_index_sha256":base, "supplement_index_sha256":supplement_hash})).unwrap();
    files.push(put(
        root,
        "licenses/rust-runtime-candidate/FORGE-ORIGINAL-NOTICES.json",
        &forge,
    ));
    let assets = serde_json::to_vec(&serde_json::json!({"schema":"forge.asset-notice-supplements.v1", "base_index_sha256":base})).unwrap();
    files.push(put(
        root,
        "licenses/rust-runtime-candidate/ASSET-NOTICES.json",
        &assets,
    ));
    let license_paths = [
        "licenses/THIRD_PARTY_NOTICES.md",
        "licenses/rust-runtime-candidate/INDEX.json",
        "licenses/rust-runtime-candidate/SUPPLEMENT-NOTICES.json",
        "licenses/rust-runtime-candidate/FORGE-ORIGINAL-NOTICES.json",
        "licenses/rust-runtime-candidate/ASSET-NOTICES.json",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    let manifest = GameKitManifest {
        schema: "forge.delivery.game-kit.v1".into(),
        platform: if cfg!(target_os = "windows") {
            "windows-x64"
        } else {
            "linux-x64"
        }
        .into(),
        version: "fixture".into(),
        source_revision: bytes_sha256(b"fixture build input").to_string(),
        launcher_contract: "forge.game-launcher.v2".into(),
        launcher: "bin/launcher".into(),
        module_runtime: "bin/runtime".into(),
        rights: Rights::PrivateLocalOnly,
        license_paths,
        files,
    };
    let bytes = serde_json::to_vec(&manifest).unwrap();
    (manifest, bytes)
}

fn inputs(
    game: &fixture::Game,
) -> (
    forge_project::workspace::launcher::DerivedGameManifest,
    ContentHash,
    ExportedModule,
) {
    inputs_with_image(game, None)
}

fn inputs_with_image(
    game: &fixture::Game,
    image: Option<forge_project::workspace::launcher::PresentationAsset>,
) -> (
    forge_project::workspace::launcher::DerivedGameManifest,
    ContentHash,
    ExportedModule,
) {
    let mut head = WorkspaceHead::default()
        .with_project(game.project, "Projet de test".into())
        .unwrap()
        .with_module(game.project, game.module, "Module de test".into())
        .unwrap();
    head.projects[0].modules[0].zones.push(ZoneReference {
        area_id: game.area,
        archive_hash: game.archive_hash,
        head_hash: game.head_hash,
    });
    let configuration = GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: "Jeu indépendant".into(),
        summary: String::new(),
        icon: image,
        logo: image,
        background: image,
        contents: vec![GameContent {
            module_id: game.module,
            areas: vec![game.area],
        }],
        entry: GameLocation {
            module_id: game.module,
            area_id: game.area,
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
        .with_game_configuration(game.project, None, Some(configuration))
        .unwrap();
    let hash = head.content_hash().unwrap();
    let derived = head.derive_game_manifest(game.project).unwrap();
    let (_, revision) = archive::inspect(&game.archive).unwrap();
    let module = ExportedModule {
        module_hash: None,
        module_id: game.module,
        area_id: game.area,
        head_hash: game.head_hash,
        package_path: game.archive.clone(),
        source_root: Path::new("").into(),
        package_sha256: ContentHash::parse(&forge_delivery::file_hash(&game.archive).unwrap())
            .unwrap(),
        transport_manifest_sha256: revision,
    };
    (derived, hash, module)
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one fixture verifies complete private packaging and safe refusals"
)]
fn private_candidate_checks_kit_notices_module_closure_and_installation() {
    let fixture = fixture::Fixture::new();
    let kit_root = fixture.root().join("game-kit");
    let (manifest, manifest_bytes) = kit(&kit_root);
    let verified = VerifiedGameKit::read(&kit_root, &manifest_bytes).unwrap();
    let (game, workspace, module) = inputs(&fixture.first);
    let output = fixture.root().join("game-candidates");
    assert!(
        build_candidate(
            &output,
            &verified,
            &game,
            bytes_sha256(b"other workspace"),
            std::slice::from_ref(&module)
        )
        .is_err()
    );
    assert!(!output.exists());
    let candidate = build_candidate(
        &output,
        &verified,
        &game,
        workspace,
        std::slice::from_ref(&module),
    )
    .unwrap();
    assert_eq!(candidate.rights, Rights::PrivateLocalOnly);
    assert!(
        !candidate.runtime_verified
            && !candidate.installation_verified
            && !candidate.distribution_qualified
    );
    let names: Vec<_> = fs::read_dir(&candidate.directory)
        .unwrap()
        .map(|entry| entry.unwrap().file_name())
        .collect();
    assert_eq!(names.len(), 4);
    assert_eq!(
        fs::read(candidate.directory.join(candidate.installer_file)).unwrap(),
        b"inert launcher fixture"
    );
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        assert_ne!(
            fs::metadata(candidate.directory.join(candidate.installer_file))
                .unwrap()
                .permissions()
                .mode()
                & 0o111,
            0
        );
    }
    assert!(!candidate.directory.join(".application-source").exists());
    let mut archives = vec![candidate.directory.join(candidate.application_file)];
    archives.extend(
        candidate
            .modules
            .iter()
            .map(|module| candidate.directory.join(&module.file)),
    );
    let install_root = fixture.installation("built-candidate");
    let mut plan = install::prepare(&install_root, &archives).unwrap();
    plan.user_root = fixture.user.to_str().unwrap().into();
    plan.command_id.clear();
    plan.command_id = forge_delivery::hash(&forge_delivery::json(&plan).unwrap());
    let state = install::apply(&plan).unwrap();
    let admitted = standalone::verify_snapshot(&install_root, &state.active).unwrap();
    assert_eq!(admitted.game().game_id(), fixture.first.project);
    assert_eq!(admitted.game().configuration().title, "Jeu indépendant");

    let launcher = kit_root.join("bin/launcher");
    fs::write(&launcher, b"changed launcher").unwrap();
    assert!(VerifiedGameKit::read(&kit_root, &manifest_bytes).is_err());
    fs::write(&launcher, b"inert launcher fixture").unwrap();
    let aube = b"FORGE_LAUNCHER_AUBE_FRONTEND_READY";
    fs::write(&launcher, aube).unwrap();
    let mut aube_manifest = manifest.clone();
    let aube_entry = aube_manifest
        .files
        .iter_mut()
        .find(|entry| entry.path == "bin/launcher")
        .unwrap();
    aube_entry.bytes = aube.len() as u64;
    aube_entry.sha256 = bytes_sha256(aube).to_string();
    assert!(
        VerifiedGameKit::read(&kit_root, &serde_json::to_vec(&aube_manifest).unwrap()).is_err()
    );
    fs::write(&launcher, b"inert launcher fixture").unwrap();
    let mut missing_notice = manifest.clone();
    missing_notice
        .files
        .retain(|entry| !entry.path.contains("/texts/"));
    assert!(
        VerifiedGameKit::read(&kit_root, &serde_json::to_vec(&missing_notice).unwrap()).is_err()
    );
    let mut public = manifest;
    public.rights = Rights::Redistributable;
    assert!(VerifiedGameKit::read(&kit_root, &serde_json::to_vec(&public).unwrap()).is_err());
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one transaction fixture exercises candidate integrity and rollback"
)]
fn candidate_bootstrap_requires_v2_rechecks_inputs_and_preserves_rollback() {
    let fixture = fixture::Fixture::new();
    let kit_root = fixture.root().join("bootstrap-kit");
    let (mut manifest, manifest_bytes) = kit(&kit_root);
    let verified = VerifiedGameKit::read(&kit_root, &manifest_bytes).unwrap();
    let (game, workspace, module) = inputs(&fixture.first);
    let output = fixture.root().join("bootstrap-exports");
    let candidate = build_candidate(
        &output,
        &verified,
        &game,
        workspace,
        std::slice::from_ref(&module),
    )
    .unwrap();
    let source = &candidate.directory;
    let bootstrap = source.join(candidate.installer_file);
    let index = source.join("candidate.json");
    let original = fs::read(&index).unwrap();
    let parsed: serde_json::Value = serde_json::from_slice(&original).unwrap();
    let admitted = inspect_candidate(source, &bootstrap).unwrap();
    assert_eq!(admitted.game_id, fixture.first.project);
    assert_eq!(admitted.title, "Jeu indépendant");
    assert!(inspect_candidate(source, &kit_root.join("bin/launcher")).is_err());
    #[cfg(windows)]
    {
        let mut shared_root_spelling = fs::canonicalize(source).unwrap().into_os_string();
        shared_root_spelling.push(std::path::MAIN_SEPARATOR_STR);
        assert_eq!(
            inspect_candidate(Path::new(&shared_root_spelling), &bootstrap).unwrap(),
            admitted
        );
    }

    let mut old = parsed.clone();
    old["schema"] = "forge.delivery.private-game-candidate.v1".into();
    old.as_object_mut().unwrap().remove("installer_file");
    old.as_object_mut().unwrap().remove("installer_sha256");
    fs::write(&index, serde_json::to_vec(&old).unwrap()).unwrap();
    assert!(inspect_candidate(source, &bootstrap).is_err());
    let mut escaped = parsed.clone();
    escaped["modules"][0]["file"] = "../module.orpmod".into();
    fs::write(&index, serde_json::to_vec(&escaped).unwrap()).unwrap();
    assert!(inspect_candidate(source, &bootstrap).is_err());
    let mut duplicated = parsed.clone();
    let duplicate = duplicated["modules"][0].clone();
    duplicated["modules"]
        .as_array_mut()
        .unwrap()
        .push(duplicate);
    fs::write(&index, serde_json::to_vec(&duplicated).unwrap()).unwrap();
    assert!(inspect_candidate(source, &bootstrap).is_err());
    let mut unknown = parsed.clone();
    unknown["unreviewed"] = true.into();
    fs::write(&index, serde_json::to_vec(&unknown).unwrap()).unwrap();
    assert!(inspect_candidate(source, &bootstrap).is_err());
    fs::write(&index, &original).unwrap();

    #[cfg(unix)]
    {
        use std::os::unix::fs::symlink;
        fs::remove_file(&bootstrap).unwrap();
        symlink(kit_root.join("bin/launcher"), &bootstrap).unwrap();
        assert!(inspect_candidate(source, &bootstrap).is_err());
        fs::remove_file(&bootstrap).unwrap();
        fs::write(&bootstrap, b"inert launcher fixture").unwrap();
    }

    let install_root = fixture.installation("bootstrap");
    let unrelated = fixture.installation("unrelated");
    fs::create_dir(&unrelated).unwrap();
    fs::write(unrelated.join("user.txt"), b"keep").unwrap();
    assert!(prepare_candidate_install(source, &bootstrap, &unrelated).is_err());
    assert_eq!(fs::read(unrelated.join("user.txt")).unwrap(), b"keep");
    assert!(prepare_candidate_install(source, &bootstrap, source).is_err());
    let reviewed = inspect_candidate(source, &bootstrap).unwrap();
    let mut rewritten = original.clone();
    rewritten.push(b' ');
    fs::write(&index, &rewritten).unwrap();
    let changed = prepare_candidate_install(source, &bootstrap, &install_root).unwrap();
    assert_ne!(&reviewed, changed.inspection());
    fs::write(&index, &original).unwrap();
    let mut trailing = install_root.clone().into_os_string();
    trailing.push(std::path::MAIN_SEPARATOR_STR);
    let mut plan = prepare_candidate_install(source, &bootstrap, Path::new(&trailing)).unwrap();
    assert_eq!(plan.root(), install_root.as_path());
    assert!(!plan.is_update());
    let module_path = source.join(&candidate.modules[0].file);
    let saved_module = fs::read(&module_path).unwrap();
    fs::write(&module_path, b"changed").unwrap();
    assert!(apply_candidate_install(&plan).is_err());
    assert!(!install_root.join("installation.json").exists());
    fs::write(&module_path, &saved_module).unwrap();
    let app_path = source.join(candidate.application_file);
    let saved_app = fs::read(&app_path).unwrap();
    fs::write(&app_path, b"changed").unwrap();
    assert!(apply_candidate_install(&plan).is_err());
    assert!(!install_root.join("installation.json").exists());
    fs::write(&app_path, &saved_app).unwrap();
    apply_candidate_install(&plan).unwrap();
    let first = install::verify(&install_root).unwrap();
    assert_eq!(
        standalone::verify_snapshot(&install_root, &first.active)
            .unwrap()
            .game()
            .game_id(),
        fixture.first.project
    );
    let reopened = prepare_candidate_install(source, &bootstrap, &install_root).unwrap();
    assert!(reopened.is_current().unwrap());
    assert_eq!(
        install::verify(&install_root).unwrap().revision,
        first.revision
    );

    manifest.version = "fixture-update".into();
    let update_kit =
        VerifiedGameKit::read(&kit_root, &serde_json::to_vec(&manifest).unwrap()).unwrap();
    let update = build_candidate(&output, &update_kit, &game, workspace, &[module]).unwrap();
    let update_bootstrap = update.directory.join(update.installer_file);
    let update_plan =
        prepare_candidate_install(&update.directory, &update_bootstrap, &install_root).unwrap();
    assert!(update_plan.is_update());
    assert!(!update_plan.is_current().unwrap());
    let update_index = update.directory.join("candidate.json");
    let saved_update_index = fs::read(&update_index).unwrap();
    fs::write(&update_index, b"changed").unwrap();
    assert!(apply_candidate_install(&update_plan).is_err());
    assert_eq!(
        install::verify(&install_root).unwrap().active.core.revision,
        first.active.core.revision
    );
    fs::write(&update_index, saved_update_index).unwrap();
    apply_candidate_install(&update_plan).unwrap();
    let updated = install::verify(&install_root).unwrap();
    assert_ne!(updated.active.core.revision, first.active.core.revision);
    assert_eq!(
        updated.previous.unwrap().core.revision,
        first.active.core.revision
    );
    assert!(
        plan.launch_installed()
            .unwrap_err()
            .contains("Game installation changed")
    );
    plan.invalidate_installed();
    plan.revalidate_installed().unwrap();
    let rollback = install::prepare_rollback(&install_root).unwrap();
    let rolled_back = install::apply(&rollback).unwrap();
    assert_eq!(rolled_back.active.core.revision, first.active.core.revision);

    let moved = source.with_file_name("export-moved-after-install");
    fs::rename(source, &moved).unwrap();
    plan.invalidate_installed();
    plan.revalidate_installed().unwrap();
    let entry = plan.ensure_player_entry().unwrap();
    assert!(entry.is_file());
    assert_eq!(
        plan.player_entry_status().unwrap(),
        install::PlayerEntryStatus::Ready
    );
    assert_eq!(plan.ensure_player_entry().unwrap(), entry);
    assert_eq!(
        install::verify(&install_root).unwrap().active.core.revision,
        first.active.core.revision
    );
}

#[test]
fn different_games_have_distinct_default_install_roots() {
    let first = forge_core::ProjectId::deterministic("installer-first");
    let second = forge_core::ProjectId::deterministic("installer-second");
    assert_ne!(
        forge_delivery::launch::default_game_root(first).unwrap(),
        forge_delivery::launch::default_game_root(second).unwrap()
    );
}
