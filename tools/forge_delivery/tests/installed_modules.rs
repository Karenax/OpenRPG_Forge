#[path = "support/installed_fixture.rs"]
mod fixture;
use fixture::Fixture;
use forge_core::{AreaId, ContentHash, ModuleId, bytes_sha256};
use forge_delivery::{
    install,
    installed_modules::{self, ModuleSelection},
    module_runtime,
};
use std::{fs, process::Command};

#[test]
fn installed_modules_are_distinct_and_the_legacy_reference_is_not_relabelled() {
    let fixture = Fixture::new();
    let inventory = installed_modules::inventory(&fixture.install).unwrap();
    assert!(inventory.rejected.is_empty());
    assert_eq!(inventory.modules.len(), 2);
    for expected in [&fixture.first, &fixture.second] {
        let module = inventory
            .modules
            .iter()
            .find(|m| m.module_id == expected.id)
            .unwrap();
        assert_eq!(module.zones.len(), 1);
        assert_eq!(module.zones[0].area_id, expected.area);
        assert_eq!(
            module.zones[0].archive_sha256,
            bytes_sha256(&expected.archive)
        );
    }
    assert!(
        install::state(&fixture.install)
            .unwrap()
            .active
            .packs
            .contains_key("reference-module")
    );
}

#[test]
fn launch_plans_use_the_same_verified_binary_with_zone_and_transport_pins() {
    let fixture = Fixture::new();
    let mut executable = None;
    for module in [&fixture.first, &fixture.second] {
        let plan = installed_modules::plan(
            &fixture.install,
            ModuleSelection {
                module_id: module.id,
                area_id: module.area,
            },
        )
        .unwrap();
        let args = &plan.arguments;
        assert_eq!(plan.executable, fixture.core_root().join("game.bin"));
        assert_eq!(args[0], "--module");
        assert_eq!(args[1], fixture.module_root(module.id).to_str().unwrap());
        assert!(
            args.windows(2)
                .any(|p| p == ["--zone", &module.area.to_string()])
        );
        assert!(args.windows(2).any(|p| p
            == [
                "--expected-archive-sha256",
                &bytes_sha256(&module.archive).to_string()
            ]));
        assert!(args.windows(2).any(|p| p
            == [
                "--expected-package-manifest-sha256",
                &plan.package_manifest_sha256.to_string()
            ]));
        assert!(
            args.windows(2)
                .any(|p| p == ["--save-root", fixture.user.join("saves").to_str().unwrap()])
        );
        assert!(!args.iter().any(|a| a == "--walk"));
        assert!(
            !plan
                .environment
                .keys()
                .any(|k| k.starts_with("FORGE_TIDEWATER_") || k == "OPENRPG_ASSET_VAULT")
        );
        assert!(plan.environment.contains_key("FORGE_GAME_REPORT_PATH"));
        if let Some(previous) = &executable {
            assert_eq!(&plan.executable, previous);
        } else {
            executable = Some(plan.executable);
        }
    }
}

#[test]
fn corruption_is_diagnosed_per_module_and_does_not_hide_the_healthy_module() {
    let fixture = Fixture::new();
    let head = fs::read(fixture.install.join("installation.json")).unwrap();
    fs::remove_file(
        fixture
            .module_root(fixture.second.id)
            .join("assets/model.glb"),
    )
    .unwrap();
    let inventory = installed_modules::inventory(&fixture.install).unwrap();
    assert_eq!(inventory.modules.len(), 1);
    assert_eq!(inventory.modules[0].module_id, fixture.first.id);
    assert_eq!(inventory.rejected.len(), 1);
    assert_eq!(inventory.rejected[0].pack_id, fixture.second.id.to_string());
    assert!(!inventory.rejected[0].reason.is_empty());
    assert!(
        installed_modules::plan(
            &fixture.install,
            ModuleSelection {
                module_id: fixture.first.id,
                area_id: fixture.first.area
            }
        )
        .is_ok()
    );
    assert!(
        installed_modules::start(
            &fixture.install,
            ModuleSelection {
                module_id: fixture.second.id,
                area_id: fixture.second.area
            }
        )
        .is_err()
    );
    assert_eq!(
        fs::read(fixture.install.join("installation.json")).unwrap(),
        head
    );
}

#[test]
fn child_transport_pin_is_distinct_from_its_zone_archive_pin_and_rejects_a_valid_substitution() {
    let fixture = Fixture::new();
    let root = fixture.module_root(fixture.first.id);
    let loaded = module_runtime::load(&root, fixture.first.area, None).unwrap();
    let state = install::state(&fixture.install).unwrap();
    let revision =
        ContentHash::parse(&state.active.packs[&fixture.first.id.to_string()].revision).unwrap();
    module_runtime::verify_transport_revision(&loaded, revision).unwrap();
    assert_ne!(revision, bytes_sha256(&loaded.archive));
    let path = root.join("package-manifest.json");
    let mut descriptor: forge_delivery::manifest::Descriptor =
        forge_delivery::read_json(&path).unwrap();
    descriptor.label.push_str(" valid alternative descriptor");
    fs::write(&path, forge_delivery::json(&descriptor).unwrap()).unwrap();
    let substituted = module_runtime::load(&root, fixture.first.area, None).unwrap();
    assert_eq!(substituted.archive, loaded.archive);
    assert!(module_runtime::verify_transport_revision(&substituted, revision).is_err());
}

#[test]
fn public_cli_discovers_and_plans_by_stable_ids_and_refuses_unknown_selections() {
    let fixture = Fixture::new();
    let list = Command::new(env!("CARGO_BIN_EXE_forge_delivery"))
        .arg("modules")
        .arg(&fixture.install)
        .output()
        .unwrap();
    assert!(
        list.status.success(),
        "{}",
        String::from_utf8_lossy(&list.stderr)
    );
    let list: serde_json::Value = serde_json::from_slice(&list.stdout).unwrap();
    assert_eq!(list["modules"].as_array().unwrap().len(), 2);
    let planned = Command::new(env!("CARGO_BIN_EXE_forge_delivery"))
        .arg("module-launch-plan")
        .arg(&fixture.install)
        .arg(fixture.second.id.to_string())
        .arg(fixture.second.area.to_string())
        .output()
        .unwrap();
    assert!(
        planned.status.success(),
        "{}",
        String::from_utf8_lossy(&planned.stderr)
    );
    let value: serde_json::Value = serde_json::from_slice(&planned.stdout).unwrap();
    assert_eq!(
        value["selection"]["module_id"],
        fixture.second.id.to_string()
    );
    for (module, area) in [
        (ModuleId::deterministic("absent"), fixture.first.area),
        (fixture.first.id, AreaId::deterministic("absent")),
    ] {
        let refused = Command::new(env!("CARGO_BIN_EXE_forge_delivery"))
            .arg("module-launch")
            .arg(&fixture.install)
            .arg(module.to_string())
            .arg(area.to_string())
            .output()
            .unwrap();
        assert!(!refused.status.success());
    }
}

#[test]
fn module_check_cli_reports_exact_loaded_documents_without_gpu_and_refuses_bad_inputs() {
    let fixture = Fixture::new();
    let check = |package: &std::path::Path, area: AreaId, cache: &std::path::Path| {
        Command::new(env!("CARGO_BIN_EXE_forge_delivery"))
            .arg("module-check")
            .arg(package)
            .arg(area.to_string())
            .arg(cache)
            .output()
            .unwrap()
    };
    let package = fixture.root.join("harbor.orpmod");
    let cache = fixture.root.join("Diagnostic Cache Élan");
    let checked = check(&package, fixture.first.area, &cache);
    assert!(
        checked.status.success(),
        "{}",
        String::from_utf8_lossy(&checked.stderr)
    );
    let value: serde_json::Value = serde_json::from_slice(&checked.stdout).unwrap();
    assert_eq!(value["module_id"], fixture.first.id.to_string());
    assert_eq!(value["area_id"], fixture.first.area.to_string());
    assert_eq!(
        value["archive_sha256"],
        bytes_sha256(&fixture.first.archive).to_string()
    );
    assert!(std::path::Path::new(value["package_root"].as_str().unwrap()).is_dir());
    assert!(
        !check(
            &package,
            AreaId::deterministic("absent"),
            &fixture.root.join("Missing Zone Cache")
        )
        .status
        .success()
    );
    assert!(!fixture.root.join("Missing Zone Cache").exists());
    fs::write(&package, b"corrupt transport").unwrap();
    assert!(
        !check(
            &package,
            fixture.first.area,
            &fixture.root.join("Corrupt Cache")
        )
        .status
        .success()
    );
    assert!(!fixture.root.join("Corrupt Cache").exists());
    assert!(
        !check(
            &fixture.module_root(fixture.first.id),
            fixture.first.area,
            std::path::Path::new("relative-cache")
        )
        .status
        .success()
    );
}

#[test]
#[cfg(windows)]
fn an_accepted_windows_executable_recipe_can_differ_only_in_ascii_case() {
    let fixture = Fixture::new();
    let mut descriptor: forge_delivery::manifest::Descriptor =
        forge_delivery::read_json(&fixture.core_root().join("package-manifest.json")).unwrap();
    descriptor
        .product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("game")
        .unwrap()
        .executable = "GAME.BIN".into();
    let package = fixture.root.join("case-applications.forgepack");
    forge_delivery::archive::pack(&fixture.root.join("core"), descriptor, &package).unwrap();
    install::apply(&install::prepare(&fixture.install, &[package]).unwrap()).unwrap();
    let plan = installed_modules::plan(
        &fixture.install,
        ModuleSelection {
            module_id: fixture.first.id,
            area_id: fixture.first.area,
        },
    )
    .unwrap();
    assert!(plan.executable.is_file());
}
