#[path = "support/standalone_fixture.rs"]
mod fixture;

use forge_core::AreaId;
use forge_delivery::{install, installed_modules, launch, standalone};
use std::{fs, path::PathBuf};

fn selection(game: &fixture::Game) -> installed_modules::ModuleSelection {
    installed_modules::ModuleSelection {
        module_id: game.module,
        area_id: game.area,
    }
}

#[test]
fn game_browser_profile_is_derived_from_the_verified_game_and_protected_user_root() {
    let fixture = fixture::Fixture::new();
    let root_a = fixture.installation("browser-a");
    let root_b = fixture.installation("browser-b");
    install::apply(&fixture.prepare_new(&fixture.first, "browser-a", "Jeu A")).unwrap();
    install::apply(&fixture.prepare_new(&fixture.second, "browser-b", "Jeu B")).unwrap();
    let (game_a, profile_a, user_a) =
        standalone::game_webview_profile_for(&root_a, Some(fixture.first.project))
            .unwrap()
            .unwrap();
    let (game_b, profile_b, user_b) = standalone::game_webview_profile_for(&root_b, None)
        .unwrap()
        .unwrap();
    assert_eq!(user_a, fixture.user.to_str().unwrap());
    assert_eq!(user_b, fixture.user.to_str().unwrap());
    assert_eq!(game_a, fixture.first.project);
    assert_eq!(game_b, fixture.second.project);
    assert_eq!(
        profile_a,
        fixture
            .user
            .join("launcher-webview")
            .join(fixture.first.project.to_string())
    );
    assert_eq!(
        profile_b,
        fixture
            .user
            .join("launcher-webview")
            .join(fixture.second.project.to_string())
    );
    assert_ne!(profile_a, profile_b);
    assert!(install::physically_disjoint(&profile_a, &profile_b).unwrap());
    assert!(!install::physically_disjoint(&profile_a, &fixture.user).unwrap());
    assert!(!install::physically_disjoint(&profile_a, &profile_a).unwrap());
    assert!(!profile_a.exists() && !profile_b.exists());
    assert!(standalone::game_webview_profile_for(&root_a, Some(fixture.second.project)).is_err());
    let absent = fixture.installation("browser-absent");
    assert!(standalone::game_webview_profile_for(&absent, Some(fixture.first.project)).is_err());
    assert!(!absent.exists());
    fs::create_dir_all(profile_a.parent().unwrap()).unwrap();
    fs::write(&profile_a, b"foreign file").unwrap();
    assert!(standalone::game_webview_profile_for(&root_a, Some(fixture.first.project)).is_err());
    assert_eq!(fs::read(&profile_a).unwrap(), b"foreign file");
}

fn save_root(plan: &installed_modules::ModuleLaunchPlan) -> PathBuf {
    let index = plan
        .arguments
        .iter()
        .position(|arg| arg == "--save-root")
        .unwrap();
    PathBuf::from(&plan.arguments[index + 1])
}

#[test]
fn player_view_reads_only_the_verified_games_identity_and_named_areas() {
    let fixture = fixture::Fixture::new();
    let game = fixture.with_extra_area(&fixture.first);
    let extra = game.extra_area.as_ref().unwrap();
    let root = fixture.installation("player-view");
    install::apply(&fixture.prepare_new(&game, "player-view", "Les deux criques")).unwrap();

    let view = standalone::game_view(&root).unwrap();
    assert_eq!(view.game_id, game.project);
    assert_eq!(view.title, "Les deux criques");
    assert_eq!(view.entry, selection(&game));
    assert_eq!(view.modules.len(), 1);
    assert_eq!(view.modules[0].name, "harbor");
    assert_eq!(view.modules[0].zones.len(), 2);
    assert_eq!(view.modules[0].zones[0].name, "harbor");
    assert_eq!(view.modules[0].zones[0].selection, selection(&game));
    assert_eq!(view.modules[0].zones[1].name, "Crique supérieure");
    assert_eq!(view.modules[0].zones[1].selection.area_id, extra.area);
    assert_eq!(view.modules[0].zones[1].selection.module_id, game.module);
    let encoded = serde_json::to_string(&view).unwrap();
    assert!(!encoded.contains(&fixture.root().to_string_lossy().to_string()));
    assert!(!encoded.contains(&fixture.second.module.to_string()));

    let state = install::state(&root).unwrap();
    let core = install::package_root(&root, &state.active.core).unwrap();
    let application =
        standalone::VerifiedGameApplication::read(&core, &state.active.core.revision).unwrap();
    let module = standalone::VerifiedGameModule::read_archive(&game.archive).unwrap();
    assert_eq!(module.module_id(), game.module);
    assert_eq!(module.transport_revision().to_string(), game.revision);
    standalone::validate_game_closure(&application, &[module]).unwrap();
    let damaged = fixture.root().join("damaged-reader.orpmod");
    let mut bytes = fs::read(&game.archive).unwrap();
    *bytes.last_mut().unwrap() ^= 1;
    fs::write(&damaged, bytes).unwrap();
    assert!(standalone::VerifiedGameModule::read_archive(&damaged).is_err());

    fs::write(core.join("game/launcher.json"), b"{}").unwrap();
    assert!(standalone::game_view(&root).is_err());
    let absent = fixture.installation("absent-view");
    assert!(standalone::game_view(&absent).is_err());
    assert!(!absent.exists());
}

#[test]
fn player_rollback_is_bound_to_the_reviewed_game_and_head() {
    let fixture = fixture::Fixture::new();
    let root = fixture.installation("player-rollback");
    install::apply(&fixture.prepare_new(&fixture.first, "player-rollback", "Jeu un")).unwrap();
    assert!(
        standalone::prepare_game_rollback(&root, fixture.first.project)
            .unwrap()
            .is_none()
    );
    assert!(standalone::prepare_game_rollback(&root, fixture.second.project).is_err());

    let saves = fixture
        .user
        .join("games")
        .join(fixture.first.project.to_string())
        .join("saves");
    fs::create_dir_all(&saves).unwrap();
    fs::write(saves.join("progress.dat"), b"progression").unwrap();
    let second = fixture.application(&fixture.first, "rollback-second", "Deuxième version", false);
    let upgraded =
        install::apply(&install::prepare(&root, std::slice::from_ref(&second)).unwrap()).unwrap();
    let reviewed = standalone::prepare_game_rollback(&root, fixture.first.project)
        .unwrap()
        .unwrap();
    let third = fixture.application(&fixture.first, "rollback-third", "Troisième version", false);
    install::apply(&install::prepare(&root, std::slice::from_ref(&third)).unwrap()).unwrap();
    assert!(install::apply(&reviewed).is_err());
    assert!(standalone::prepare_game_rollback(&root, fixture.second.project).is_err());
    let fresh = standalone::prepare_game_rollback(&root, fixture.first.project)
        .unwrap()
        .unwrap();
    let restored = install::apply(&fresh).unwrap();
    assert_eq!(restored.active.core.revision, upgraded.active.core.revision);
    assert_eq!(
        fs::read(saves.join("progress.dat")).unwrap(),
        b"progression"
    );
}

#[test]
fn uninstall_helper_is_an_isolated_verified_copy_for_one_game() {
    let fixture = fixture::Fixture::new();
    let root = fixture.installation("uninstall-helper");
    let installed =
        install::apply(&fixture.prepare_new(&fixture.first, "uninstall-helper", "Jeu un")).unwrap();
    assert!(standalone::prepare_game_uninstall_helper(&root, fixture.second.project).is_err());
    let helper = standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
    let observed = standalone::read_game_uninstall_helper(&helper.executable)
        .unwrap()
        .unwrap();
    assert_eq!(observed.root, fs::canonicalize(&root).unwrap());
    assert_eq!(observed.game_id, fixture.first.project);
    assert_eq!(observed.title, "Jeu un");
    assert_eq!(observed.executable_sha256, helper.executable_sha256);
    assert_eq!(observed.receipt_sha256, helper.receipt_sha256);
    let core = install::package_root(&root, &installed.active.core).unwrap();
    assert_eq!(
        fs::read(&helper.executable).unwrap(),
        fs::read(core.join("launcher.bin")).unwrap()
    );
    let foreign_root = fixture.installation("copied-uninstall-helper");
    fs::create_dir(&foreign_root).unwrap();
    let foreign_directory =
        foreign_root.join(helper.executable.parent().unwrap().file_name().unwrap());
    fs::create_dir(&foreign_directory).unwrap();
    let foreign_executable = foreign_directory.join(helper.executable.file_name().unwrap());
    fs::copy(&helper.executable, &foreign_executable).unwrap();
    fs::copy(
        helper.executable.parent().unwrap().join("helper.json"),
        foreign_directory.join("helper.json"),
    )
    .unwrap();
    assert!(standalone::read_game_uninstall_helper(&foreign_executable).is_err());
    assert!(foreign_executable.is_file());
    fs::write(&helper.executable, b"modified").unwrap();
    assert!(standalone::read_game_uninstall_helper(&helper.executable).is_err());
    assert!(install::state(&root).is_ok());
}

#[cfg(windows)]
#[test]
fn uninstall_helper_reopens_with_different_windows_path_casing() {
    let fixture = fixture::Fixture::new();
    let root = fixture.installation("uninstall-helper-case");
    install::apply(&fixture.prepare_new(&fixture.first, "uninstall-helper-case", "Jeu un"))
        .unwrap();
    let helper = standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
    let alternate = PathBuf::from(helper.executable.to_str().unwrap().to_ascii_uppercase());
    let reopened = standalone::read_game_uninstall_helper(&alternate)
        .unwrap()
        .unwrap();
    assert_eq!(reopened.root, helper.root);
    assert_eq!(reopened.executable, helper.executable);
    assert!(
        install::game_uninstall_helper_status(&reopened)
            .unwrap()
            .is_none()
    );
    let completed = install::uninstall_game_with_helper(&reopened).unwrap();
    assert_eq!(
        install::game_uninstall_helper_status(&helper).unwrap(),
        Some(completed)
    );
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the transaction verifies two games, an upgrade, rollback and personal data"
)]
fn two_games_upgrade_and_rollback_keep_separate_user_saves() {
    let fixture = fixture::Fixture::new();
    let first_plan = fixture.prepare_new(&fixture.first, "first", "Jeu un");
    let second_plan = fixture.prepare_new(&fixture.second, "second", "Jeu deux");
    let first_root = fixture.installation("first");
    let second_root = fixture.installation("second");
    let first_state = install::apply(&first_plan).unwrap();
    install::apply(&second_plan).unwrap();

    let first_launch = installed_modules::plan(&first_root, selection(&fixture.first)).unwrap();
    let second_launch = installed_modules::plan(&second_root, selection(&fixture.second)).unwrap();
    let first_saves = save_root(&first_launch);
    let second_saves = save_root(&second_launch);
    assert_eq!(
        first_saves,
        fixture
            .user
            .join("games")
            .join(fixture.first.project.to_string())
            .join("saves")
    );
    assert_eq!(
        second_saves,
        fixture
            .user
            .join("games")
            .join(fixture.second.project.to_string())
            .join("saves")
    );
    assert_ne!(first_saves, second_saves);
    fs::create_dir_all(&first_saves).unwrap();
    fs::create_dir_all(&second_saves).unwrap();
    fs::write(first_saves.join("save.dat"), b"first game").unwrap();
    fs::write(second_saves.join("save.dat"), b"second game").unwrap();
    let first_inventory = installed_modules::inventory(&first_root).unwrap();
    assert_eq!(first_inventory.modules.len(), 1);
    assert_eq!(first_inventory.modules[0].module_id, fixture.first.module);
    assert!(installed_modules::plan(&first_root, selection(&fixture.second)).is_err());
    assert!(
        installed_modules::plan(
            &first_root,
            installed_modules::ModuleSelection {
                module_id: fixture.first.module,
                area_id: AreaId::deterministic("undeclared-area"),
            }
        )
        .is_err()
    );
    assert!(launch::command(&first_root, "launcher").is_err());
    assert!(launch::start(&first_root, "module-game").is_err());

    let upgraded = fixture.upgraded_module(&fixture.first, "first-upgrade");
    assert_ne!(upgraded.revision, fixture.first.revision);
    let upgrade_app = fixture.application(&upgraded, "first-upgrade", "Jeu un mis à jour", false);
    let upgrade = install::prepare(
        &first_root,
        &[upgrade_app.clone(), upgraded.archive.clone()],
    )
    .unwrap();
    let updated = install::apply(&upgrade).unwrap();
    assert_eq!(
        updated.active.packs[&fixture.first.module.to_string()].revision,
        upgraded.revision
    );
    assert_eq!(
        updated.previous.as_ref().unwrap().packs[&fixture.first.module.to_string()].revision,
        fixture.first.revision
    );
    assert_eq!(
        save_root(&installed_modules::plan(&first_root, selection(&fixture.first)).unwrap()),
        first_saves
    );
    let stale = install::prepare(&first_root, &[upgrade_app]).unwrap();
    let rollback = install::prepare_rollback(&first_root).unwrap();
    let restored = install::apply(&rollback).unwrap();
    assert_eq!(
        restored.active.core.revision,
        first_state.active.core.revision
    );
    assert_eq!(
        restored.active.packs[&fixture.first.module.to_string()].revision,
        fixture.first.revision
    );
    assert!(install::apply(&stale).is_err());
    assert_eq!(
        fs::read(first_saves.join("save.dat")).unwrap(),
        b"first game"
    );
    assert_eq!(
        fs::read(second_saves.join("save.dat")).unwrap(),
        b"second game"
    );
    assert!(
        install::package_root(
            &first_root,
            &updated.active.packs[&fixture.first.module.to_string()]
        )
        .unwrap()
        .is_dir()
    );
    install::verify(&first_root).unwrap();
    install::verify(&second_root).unwrap();
    install::uninstall(&first_root).unwrap();
    assert_eq!(
        fs::read(first_saves.join("save.dat")).unwrap(),
        b"first game"
    );
    assert_eq!(
        fs::read(second_saves.join("save.dat")).unwrap(),
        b"second game"
    );
}

#[test]
fn invalid_game_manifest_never_becomes_active_or_erases_user_data() {
    let fixture = fixture::Fixture::new();
    fs::create_dir_all(&fixture.user).unwrap();
    fs::write(fixture.user.join("personal.txt"), b"preserve").unwrap();
    let root = fixture.installation("invalid");
    for name in ["invalid-one", "invalid-two"] {
        let application = fixture.application(&fixture.first, name, "Jeu invalide", true);
        let mut plan =
            install::prepare(&root, &[application, fixture.first.archive.clone()]).unwrap();
        plan.user_root = fixture.user.to_str().unwrap().into();
        plan.command_id.clear();
        plan.command_id = forge_delivery::hash(&forge_delivery::json(&plan).unwrap());
        assert!(install::apply(&plan).is_err());
        assert!(!root.join("installation.json").exists());
        let packages = root.join("packages");
        assert!(!packages.exists() || fs::read_dir(packages).unwrap().next().is_none());
        assert_eq!(
            fs::read(fixture.user.join("personal.txt")).unwrap(),
            b"preserve"
        );
    }
}

#[test]
fn rejected_update_cleans_only_its_new_package_and_preserves_rollback() {
    let fixture = fixture::Fixture::new();
    let root = fixture.installation("preserved");
    let initial = fixture.prepare_new(&fixture.first, "preserved", "Jeu un");
    install::apply(&initial).unwrap();
    let valid = fixture.application(&fixture.first, "preserved-valid", "Version deux", false);
    let state = install::apply(&install::prepare(&root, &[valid]).unwrap()).unwrap();
    let active_core = install::package_root(&root, &state.active.core).unwrap();
    let previous_core =
        install::package_root(&root, &state.previous.as_ref().unwrap().core).unwrap();
    let before = fs::read(root.join("installation.json")).unwrap();
    fs::create_dir_all(&fixture.user).unwrap();
    fs::write(fixture.user.join("personal.txt"), b"preserve").unwrap();
    let bad = fixture.application(&fixture.first, "preserved-bad", "Version invalide", true);
    let (descriptor, revision) = forge_delivery::archive::inspect(&bad).unwrap();
    let rejected = root.join("packages").join(descriptor.id).join(revision);
    let plan = install::prepare(&root, &[bad]).unwrap();
    assert!(install::apply(&plan).is_err());
    assert_eq!(fs::read(root.join("installation.json")).unwrap(), before);
    assert!(active_core.is_dir());
    assert!(previous_core.is_dir());
    assert!(!rejected.exists());
    assert_eq!(
        fs::read(fixture.user.join("personal.txt")).unwrap(),
        b"preserve"
    );
    let rollback = install::prepare_rollback(&root).unwrap();
    install::apply(&rollback).unwrap();
}

#[test]
fn changed_later_archive_is_refused_before_any_package_is_copied() {
    let fixture = fixture::Fixture::new();
    let root = fixture.installation("changed-input");
    let plan = fixture.prepare_new(&fixture.first, "changed-input", "Jeu un");
    fs::write(&fixture.first.archive, b"changed after review").unwrap();
    assert!(install::apply(&plan).is_err());
    assert!(!root.join("installation.json").exists());
    assert!(!root.join("packages").exists());
}

#[test]
fn preexisting_package_with_foreign_content_is_never_cleaned_by_a_failed_attempt() {
    let fixture = fixture::Fixture::new();
    let root = fixture.installation("foreign-package");
    let app = fixture.application(&fixture.first, "foreign-package", "Jeu invalide", true);
    let (descriptor, revision) = forge_delivery::archive::inspect(&app).unwrap();
    let pack = install::InstalledPack {
        id: descriptor.id,
        revision,
        kind: forge_delivery::manifest::Kind::Applications,
    };
    let destination = install::package_root(&root, &pack).unwrap();
    fs::create_dir_all(destination.parent().unwrap()).unwrap();
    forge_delivery::archive::extract(&app, &destination, &pack.revision).unwrap();
    fs::write(destination.join("foreign.keep"), b"personal").unwrap();
    let mut plan = install::prepare(&root, &[app, fixture.first.archive.clone()]).unwrap();
    plan.user_root = fixture.user.to_str().unwrap().into();
    plan.command_id.clear();
    plan.command_id = forge_delivery::hash(&forge_delivery::json(&plan).unwrap());
    assert!(install::apply(&plan).is_err());
    assert_eq!(
        fs::read(destination.join("foreign.keep")).unwrap(),
        b"personal"
    );
    assert!(destination.join("launcher.bin").is_file());
    assert!(!root.join("installation.json").exists());
}

#[test]
fn unselected_archives_are_refused_before_installation() {
    let fixture = fixture::Fixture::new();
    let application = fixture.application(&fixture.first, "extra", "Jeu un", false);
    let root = fixture.installation("extra");
    let error = install::prepare(
        &root,
        &[
            application,
            fixture.first.archive.clone(),
            fixture.second.archive.clone(),
        ],
    )
    .unwrap_err();
    assert!(error.contains("unselected package"));
    assert!(!root.exists());
}

#[test]
fn corrupt_active_game_is_refused_before_launcher_or_module_spawn() {
    let fixture = fixture::Fixture::new();
    let plan = fixture.prepare_new(&fixture.first, "corrupt", "Jeu un");
    let root = fixture.installation("corrupt");
    let state = install::apply(&plan).unwrap();
    let core = install::package_root(&root, &state.active.core).unwrap();
    fs::write(core.join("game/launcher.json"), b"{}").unwrap();
    assert!(installed_modules::inventory(&root).is_err());
    assert!(installed_modules::plan(&root, selection(&fixture.first)).is_err());
    assert!(launch::start(&root, "launcher").is_err());
    assert!(launch::start(&root, "module-game").is_err());
}
