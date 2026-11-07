use forge_delivery::manifest::{
    Descriptor, Entry, Kind, Launch, Product, Rights, STUDIO_PRODUCT_SCHEMA,
};
#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
use forge_delivery::{archive, hash, install, launch};
use std::collections::BTreeMap;
#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
use std::{
    fs,
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
};

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
static NEXT: AtomicU64 = AtomicU64::new(0);
const EDITOR: &str = "bin/forge-studio.exe";
const RUNTIME: &str = "bin/game-export-kit/bin/forge-module.exe";
const GAME_LAUNCHER: &str = "bin/game-export-kit/bin/forge-game-launcher.exe";
const LINUX_EDITOR: &str = "bin/forge-studio";
const LINUX_RUNTIME: &str = "bin/game-export-kit/bin/forge-module";
const LINUX_GAME_LAUNCHER: &str = "bin/game-export-kit/bin/forge-game-launcher";

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
struct Fixture {
    root: PathBuf,
    user: PathBuf,
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-studio-product-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        let user = root.join("user");
        Self { root, user }
    }

    fn app(&self, version: &str, legacy: bool) -> PathBuf {
        let source = self.root.join(format!("app-source-{version}"));
        fs::create_dir_all(source.join("bin/game-export-kit/bin")).unwrap();
        fs::write(source.join(EDITOR), format!("inert Studio {version}")).unwrap();
        fs::write(source.join(RUNTIME), b"inert module runtime").unwrap();
        fs::write(source.join(GAME_LAUNCHER), b"inert game launcher").unwrap();
        fs::write(source.join("LICENSE"), b"fixture license").unwrap();
        let mut descriptor = studio_descriptor();
        descriptor.id = "forge-studio".into();
        descriptor.version = version.into();
        let product = descriptor.product.as_mut().unwrap();
        product.optional_packs.push("asset-library".into());
        product
            .apps
            .get_mut("editor")
            .unwrap()
            .env
            .insert("OPENRPG_ASSET_VAULT".into(), "{pack:asset-library}".into());
        if legacy {
            let mut product = descriptor.product.take().unwrap();
            product.schema = "forge.delivery.product.v1".into();
            product.apps.insert("game".into(), editor_launch());
            descriptor.product = Some(product);
        }
        let output = self.root.join(format!("studio-{version}.forgepack"));
        archive::pack(&source, descriptor, &output).unwrap();
        output
    }

    fn pack(&self, id: &str, kind: Kind) -> PathBuf {
        let source = self.root.join(format!("pack-source-{id}"));
        fs::create_dir(&source).unwrap();
        fs::write(source.join("LICENSE"), b"fixture license").unwrap();
        fs::write(source.join("data.bin"), b"inert content").unwrap();
        let mut descriptor = studio_descriptor();
        descriptor.id = id.into();
        descriptor.kind = kind;
        descriptor.product = None;
        let output = self.root.join(format!("{id}.forgepack"));
        archive::pack(&source, descriptor, &output).unwrap();
        output
    }

    fn install_with_user(&self, root: &Path, archives: &[PathBuf]) -> install::State {
        install::apply(&self.prepare_with_user(root, archives)).unwrap()
    }

    fn prepare_with_user(&self, root: &Path, archives: &[PathBuf]) -> install::Plan {
        let mut plan = install::prepare(root, archives).unwrap();
        plan.user_root = self.user.to_string_lossy().into_owned();
        plan.command_id.clear();
        plan.command_id = hash(&forge_delivery::json(&plan).unwrap());
        plan
    }
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
impl Drop for Fixture {
    fn drop(&mut self) {
        if self.root.is_absolute() && self.root.parent() == Some(std::env::temp_dir().as_path()) {
            let _ = fs::remove_dir_all(&self.root);
        }
    }
}

fn editor_launch() -> Launch {
    Launch {
        executable: EDITOR.into(),
        args: Vec::new(),
        env: BTreeMap::from([
            ("FORGE_AUTHORING_ROOT".into(), "{workspace}".into()),
            (
                "FORGE_NATIVE_GAME_EXECUTABLE".into(),
                format!("{{app}}/{RUNTIME}"),
            ),
            (
                "FORGE_NATIVE_GAME_CONTENT_ROOT".into(),
                "{app}/bin/game-export-kit".into(),
            ),
            (
                "FORGE_NATIVE_GAME_SESSION_ROOT".into(),
                "{user}/game-sessions".into(),
            ),
        ]),
        clear_env_prefixes: [
            "FORGE_AUTHORING_",
            "FORGE_TIDEWATER_",
            "FORGE_NATIVE_GAME_",
            "FORGE_MODULE_ASSET_",
            "OPENRPG_ASSET_VAULT",
            "WEBVIEW2_",
        ]
        .into_iter()
        .map(str::to_owned)
        .collect(),
    }
}

fn entry(path: &str) -> Entry {
    Entry {
        path: path.into(),
        bytes: 1,
        sha256: "a".repeat(64),
    }
}

fn studio_descriptor() -> Descriptor {
    Descriptor {
        schema: "forge.delivery.package.v1".into(),
        id: "forge-studio".into(),
        label: "Studio".into(),
        version: "1".into(),
        kind: Kind::Applications,
        platform: "windows-x64".into(),
        source_revision: "fixture".into(),
        rights: Rights::PrivateLocalOnly,
        license_paths: vec!["LICENSE".into()],
        files: ["LICENSE", EDITOR, RUNTIME, GAME_LAUNCHER]
            .into_iter()
            .map(entry)
            .collect(),
        product: Some(Product {
            schema: STUDIO_PRODUCT_SCHEMA.into(),
            id: "openrpg-forge".into(),
            title: "OpenRPG Forge Studio".into(),
            apps: BTreeMap::from([("editor".into(), editor_launch())]),
            required_packs: Vec::new(),
            optional_packs: Vec::new(),
            authoring_seed: None,
        }),
    }
}

fn linux_studio_descriptor() -> Descriptor {
    let mut descriptor = studio_descriptor();
    descriptor.platform = "linux-x64".into();
    descriptor.files = ["LICENSE", LINUX_EDITOR, LINUX_RUNTIME, LINUX_GAME_LAUNCHER]
        .into_iter()
        .map(entry)
        .collect();
    let editor = descriptor
        .product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("editor")
        .unwrap();
    editor.executable = LINUX_EDITOR.into();
    editor.env.insert(
        "FORGE_NATIVE_GAME_EXECUTABLE".into(),
        format!("{{app}}/{LINUX_RUNTIME}"),
    );
    descriptor
}

#[test]
fn studio_product_is_editor_only_with_a_private_internal_runtime() {
    let descriptor = studio_descriptor();
    descriptor.validate().unwrap();
    let mut extra_app = descriptor.clone();
    extra_app
        .product
        .as_mut()
        .unwrap()
        .apps
        .insert("game".into(), editor_launch());
    assert!(extra_app.validate().is_err());
    let mut missing_runtime = descriptor.clone();
    missing_runtime.files.retain(|entry| entry.path != RUNTIME);
    assert!(missing_runtime.validate().is_err());
    let mut missing_config = descriptor.clone();
    missing_config
        .product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("editor")
        .unwrap()
        .env
        .remove("FORGE_NATIVE_GAME_CONTENT_ROOT");
    assert!(missing_config.validate().is_err());
    let mut linux = descriptor.clone();
    linux.platform = "linux-x64".into();
    assert!(linux.validate().is_err());
    let mut legacy = descriptor;
    legacy.product.as_mut().unwrap().schema = "forge.delivery.product.v1".into();
    assert!(legacy.validate().is_err());
}

#[test]
fn linux_studio_product_requires_exact_platform_specific_internal_executables() {
    let descriptor = linux_studio_descriptor();
    descriptor.validate().unwrap();
    assert_eq!(
        descriptor.validate_host().is_ok(),
        cfg!(all(target_os = "linux", target_arch = "x86_64"))
    );

    let mut any_platform = descriptor.clone();
    any_platform.platform = "any".into();
    assert!(any_platform.validate().is_err());

    let mut windows_platform = descriptor.clone();
    windows_platform.platform = "windows-x64".into();
    assert!(windows_platform.validate().is_err());

    for path in [LINUX_RUNTIME, LINUX_GAME_LAUNCHER] {
        let mut missing = descriptor.clone();
        missing.files.retain(|entry| entry.path != path);
        assert!(missing.validate().is_err());

        let mut wrong_case = descriptor.clone();
        wrong_case
            .files
            .iter_mut()
            .find(|entry| entry.path == path)
            .unwrap()
            .path = path.to_ascii_uppercase();
        assert!(wrong_case.validate().is_err());
    }

    let mut wrong_runtime = descriptor.clone();
    wrong_runtime
        .product
        .as_mut()
        .unwrap()
        .apps
        .get_mut("editor")
        .unwrap()
        .env
        .insert(
            "FORGE_NATIVE_GAME_EXECUTABLE".into(),
            format!("{{app}}/{RUNTIME}"),
        );
    assert!(wrong_runtime.validate().is_err());

    let mut extra_app = descriptor;
    extra_app
        .product
        .as_mut()
        .unwrap()
        .apps
        .insert("game".into(), editor_launch());
    assert!(extra_app.validate().is_err());
    studio_descriptor().validate().unwrap();
}

#[cfg(all(target_os = "linux", target_arch = "x86_64"))]
mod linux_studio {
    use super::*;
    use forge_delivery::{archive, hash, install, launch};
    use std::{
        fs,
        os::unix::fs::PermissionsExt,
        path::{Path, PathBuf},
        process::Command,
        sync::atomic::{AtomicU64, Ordering},
    };

    static NEXT: AtomicU64 = AtomicU64::new(0);

    struct Fixture {
        root: PathBuf,
        user: PathBuf,
    }

    impl Fixture {
        fn new() -> Self {
            let root = std::env::temp_dir().join(format!(
                "forge-linux-studio-product-{}-{}",
                std::process::id(),
                NEXT.fetch_add(1, Ordering::Relaxed)
            ));
            fs::create_dir(&root).unwrap();
            let user = root.join("user");
            Self { root, user }
        }

        fn pack(&self, version: &str) -> PathBuf {
            self.pack_with_schema(version, false)
        }

        fn pack_with_schema(&self, version: &str, legacy: bool) -> PathBuf {
            let source = self.root.join(format!("source-{version}"));
            fs::create_dir_all(source.join("bin/game-export-kit/bin")).unwrap();
            for executable in [LINUX_EDITOR, LINUX_RUNTIME, LINUX_GAME_LAUNCHER] {
                fs::copy("/usr/bin/true", source.join(executable)).unwrap();
            }
            fs::write(source.join("LICENSE"), b"fixture licence").unwrap();
            fs::write(source.join("bin/game-export-kit/bin/data"), version).unwrap();
            fs::set_permissions(
                source.join("bin/game-export-kit/bin/data"),
                fs::Permissions::from_mode(0o755),
            )
            .unwrap();
            let mut descriptor = linux_studio_descriptor();
            descriptor.version = version.into();
            let product = descriptor.product.as_mut().unwrap();
            product.optional_packs.push("asset-library".into());
            product
                .apps
                .get_mut("editor")
                .unwrap()
                .env
                .insert("OPENRPG_ASSET_VAULT".into(), "{pack:asset-library}".into());
            if legacy {
                product.schema = "forge.delivery.product.v1".into();
                let editor = product.apps["editor"].clone();
                product.apps.insert("game".into(), editor);
            }
            let output = self.root.join(format!("studio-{version}.forgepack"));
            archive::pack(&source, descriptor, &output).unwrap();
            output
        }

        fn asset_pack(&self) -> PathBuf {
            let source = self.root.join("asset-source");
            fs::create_dir(&source).unwrap();
            fs::write(source.join("LICENSE"), b"fixture licence").unwrap();
            fs::write(source.join("asset.bin"), b"user content").unwrap();
            let mut descriptor = linux_studio_descriptor();
            descriptor.id = "asset-library".into();
            descriptor.kind = Kind::Assets;
            descriptor.platform = "any".into();
            descriptor.product = None;
            let output = self.root.join("asset-library.forgepack");
            archive::pack(&source, descriptor, &output).unwrap();
            output
        }

        fn install(&self, root: &Path, package: &Path) -> install::State {
            self.install_many(root, &[package.to_path_buf()])
        }

        fn install_many(&self, root: &Path, packages: &[PathBuf]) -> install::State {
            let mut plan = install::prepare(root, packages).unwrap();
            plan.user_root = self.user.to_string_lossy().into_owned();
            plan.command_id.clear();
            plan.command_id = hash(&forge_delivery::json(&plan).unwrap());
            install::apply(&plan).unwrap()
        }
    }

    impl Drop for Fixture {
        fn drop(&mut self) {
            if self.root.is_absolute() && self.root.parent() == Some(std::env::temp_dir().as_path())
            {
                let _ = fs::remove_dir_all(&self.root);
            }
        }
    }

    fn mode(path: &Path) -> u32 {
        fs::metadata(path).unwrap().permissions().mode() & 0o7777
    }

    #[test]
    fn installed_studio_kit_executables_are_bounded_and_permission_changes_are_refused() {
        let fixture = Fixture::new();
        let root = fixture.root.join("installation");
        let state = fixture.install(&root, &fixture.pack("1"));
        let core = install::package_root(&root, &state.active.core).unwrap();
        for executable in [LINUX_EDITOR, LINUX_RUNTIME, LINUX_GAME_LAUNCHER] {
            let path = core.join(executable);
            assert_eq!(mode(&path), 0o755);
            assert!(Command::new(path).status().unwrap().success());
        }
        let data = core.join("bin/game-export-kit/bin/data");
        assert_eq!(mode(&data), 0o644);
        assert_eq!(mode(&core.join("LICENSE")), 0o644);
        assert!(
            launch::start(&root, "editor")
                .unwrap()
                .wait()
                .unwrap()
                .success()
        );
        assert!(launch::start(&root, "game").is_err());
        install::verify(&root).unwrap();

        for executable in [LINUX_RUNTIME, LINUX_GAME_LAUNCHER] {
            let path = core.join(executable);
            fs::set_permissions(&path, fs::Permissions::from_mode(0o644)).unwrap();
            assert!(
                install::verify(&root)
                    .unwrap_err()
                    .contains("execution permissions")
            );
            fs::set_permissions(&path, fs::Permissions::from_mode(0o755)).unwrap();
        }
        fs::set_permissions(&data, fs::Permissions::from_mode(0o755)).unwrap();
        assert!(
            install::verify(&root)
                .unwrap_err()
                .contains("execution permissions")
        );
        fs::set_permissions(&data, fs::Permissions::from_mode(0o644)).unwrap();
        let runtime = core.join(LINUX_RUNTIME);
        fs::set_permissions(&runtime, fs::Permissions::from_mode(0o4755)).unwrap();
        assert!(
            install::verify(&root)
                .unwrap_err()
                .contains("execution permissions")
        );
        fs::set_permissions(&runtime, fs::Permissions::from_mode(0o755)).unwrap();
        install::verify(&root).unwrap();
        fs::write(&runtime, b"tampered").unwrap();
        assert!(install::verify(&root).is_err());
        fs::copy("/usr/bin/true", &runtime).unwrap();
        fs::set_permissions(&runtime, fs::Permissions::from_mode(0o755)).unwrap();
        let project = fixture.user.join("workspaces/openrpg-forge/project.txt");
        fs::write(&project, b"user project").unwrap();
        install::uninstall_studio(&root).unwrap();
        assert_eq!(fs::read(project).unwrap(), b"user project");
    }

    #[test]
    fn legacy_products_do_not_gain_execution_rights_for_studio_internal_paths() {
        let fixture = Fixture::new();
        let root = fixture.root.join("installation");
        let state = fixture.install(&root, &fixture.pack_with_schema("legacy", true));
        let core = install::package_root(&root, &state.active.core).unwrap();
        assert_eq!(mode(&core.join(LINUX_EDITOR)), 0o755);
        assert_eq!(mode(&core.join(LINUX_RUNTIME)), 0o644);
        assert_eq!(mode(&core.join(LINUX_GAME_LAUNCHER)), 0o644);
        install::verify(&root).unwrap();
    }

    #[test]
    fn linux_studio_upgrade_rollback_and_uninstall_preserve_the_user_workspace() {
        let fixture = Fixture::new();
        let root = fixture.root.join("installation");
        let first = fixture.install_many(&root, &[fixture.pack("1"), fixture.asset_pack()]);
        assert!(first.active.packs.contains_key("asset-library"));
        let workspace = fixture.user.join("workspaces/openrpg-forge");
        let project = workspace.join("project.txt");
        fs::write(&project, b"user-owned project").unwrap();

        let second = fixture.install(&root, &fixture.pack("2"));
        assert_ne!(first.active.core.revision, second.active.core.revision);
        assert!(second.active.packs.contains_key("asset-library"));
        assert_eq!(fs::read(&project).unwrap(), b"user-owned project");
        install::verify(&root).unwrap();
        let rollback = install::apply(&install::prepare_rollback(&root).unwrap()).unwrap();
        assert_eq!(rollback.active.core.revision, first.active.core.revision);
        assert!(rollback.active.packs.contains_key("asset-library"));
        assert_eq!(fs::read(&project).unwrap(), b"user-owned project");
        install::verify(&root).unwrap();
        let removed = install::uninstall_studio(&root).unwrap();
        assert!(removed.removed_files > 0);
        assert_eq!(removed.preserved_packages, 1);
        let asset = install::package_root(&root, &rollback.active.packs["asset-library"]).unwrap();
        assert_eq!(fs::read(asset.join("asset.bin")).unwrap(), b"user content");
        assert_eq!(fs::read(project).unwrap(), b"user-owned project");
    }
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn fresh_studio_install_creates_a_workspace_and_verifies_the_internal_kit() {
    let fixture = Fixture::new();
    let app = fixture.app("fresh", false);
    let root = fixture.root.join("installation");
    let state = fixture.install_with_user(&root, &[app]);
    assert!(fixture.user.join("workspaces/openrpg-forge").is_dir());
    assert!(state.active.packs.is_empty());
    assert!(launch::command(&root, "editor").is_err());
    assert!(launch::command(&root, "game").is_err());
    install::verify(&root).unwrap();
    let core = install::package_root(&root, &state.active.core).unwrap();
    fs::write(core.join(RUNTIME), b"tampered").unwrap();
    assert!(install::verify(&root).is_err());
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn upgrade_and_rollback_preserve_workspace_optional_assets_and_previous_packs() {
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    let legacy = fixture.app("legacy", true);
    let assets = fixture.pack("asset-library", Kind::Assets);
    let reference = fixture.pack("reference-module", Kind::Module);
    let first = fixture.install_with_user(&root, &[legacy, assets, reference]);
    let workspace = fixture.user.join("workspaces/openrpg-forge");
    fs::create_dir_all(&workspace).unwrap();
    fs::write(workspace.join("my-project.txt"), b"keep my changes").unwrap();
    let app = fixture.app("new", false);
    let upgrade = install::prepare(&root, std::slice::from_ref(&app)).unwrap();
    let next = install::apply(&upgrade).unwrap();
    assert!(next.active.packs.contains_key("asset-library"));
    assert!(!next.active.packs.contains_key("reference-module"));
    assert!(
        next.previous
            .as_ref()
            .unwrap()
            .packs
            .contains_key("reference-module")
    );
    assert!(
        install::package_root(&root, &first.active.packs["reference-module"])
            .unwrap()
            .exists()
    );
    assert_eq!(
        fs::read(workspace.join("my-project.txt")).unwrap(),
        b"keep my changes"
    );
    let retry = install::prepare(&root, &[app]).unwrap();
    assert_ne!(retry.command_id, upgrade.command_id);
    let resumed = install::apply(&retry).unwrap();
    assert_eq!(resumed.revision, next.revision);
    assert_eq!(resumed.previous, next.previous);
    let stale = install::prepare(&root, &[fixture.app("stale", false)]).unwrap();
    let rolled_back = install::apply(&install::prepare_rollback(&root).unwrap()).unwrap();
    assert_eq!(rolled_back.active.core.revision, first.active.core.revision);
    assert!(rolled_back.active.packs.contains_key("asset-library"));
    assert!(rolled_back.active.packs.contains_key("reference-module"));
    assert!(install::apply(&stale).is_err());
    assert_eq!(
        fs::read(workspace.join("my-project.txt")).unwrap(),
        b"keep my changes"
    );
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn studio_uninstall_removes_only_studio_revisions_and_keeps_workspace() {
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    fixture.install_with_user(&root, &[fixture.app("one", false)]);
    fixture.install_with_user(&root, &[fixture.app("two", false)]);
    let workspace = fixture.user.join("workspaces/openrpg-forge");
    fs::create_dir_all(&workspace).unwrap();
    fs::write(workspace.join("project.txt"), b"my project").unwrap();
    let result = install::uninstall_studio(&root).unwrap();
    assert!(result.removed_files > 0);
    assert!(!root.join("installation.json").exists());
    install::check_studio_reinstall_root(&root, None).unwrap();
    let foreign = root.join("packages/foreign.txt");
    fs::write(&foreign, b"keep").unwrap();
    assert!(install::check_studio_reinstall_root(&root, None).is_err());
    assert_eq!(fs::read(&foreign).unwrap(), b"keep");
    fs::remove_file(foreign).unwrap();
    fixture.install_with_user(&root, &[fixture.app("three", false)]);
    assert_eq!(
        fs::read(workspace.join("project.txt")).unwrap(),
        b"my project"
    );
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn unmarked_first_studio_activation_accepts_only_its_verified_candidate() {
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    let candidate = fixture.app("first", false);
    let state = fixture.install_with_user(&root, std::slice::from_ref(&candidate));
    let project = fixture.user.join("workspaces/openrpg-forge/project.txt");
    fs::write(&project, b"keep this project").unwrap();

    assert!(install::check_studio_reinstall_root(&root, None).is_err());
    let different = fixture.app("different", false);
    assert!(install::check_studio_reinstall_root(&root, Some(&different)).is_err());
    install::check_studio_reinstall_root(&root, Some(&candidate)).unwrap();
    let plan = install::prepare(&root, std::slice::from_ref(&candidate)).unwrap();
    let resumed = install::apply(&plan).unwrap();
    assert_eq!(resumed.revision, state.revision);
    assert!(resumed.previous.is_none());
    assert_eq!(fs::read(project).unwrap(), b"keep this project");

    let package = install::package_root(&root, &state.active.core).unwrap();
    let foreign = package.with_file_name("foreign-revision");
    fs::create_dir(&foreign).unwrap();
    assert!(install::check_studio_reinstall_root(&root, Some(&candidate)).is_err());
    assert!(foreign.is_dir());
    fs::remove_dir(foreign).unwrap();
    fs::write(package.join(EDITOR), b"tampered").unwrap();
    assert!(install::check_studio_reinstall_root(&root, Some(&candidate)).is_err());
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn studio_installer_reuses_the_registered_legacy_user_root() {
    let fixture = Fixture::new();
    let legacy = fixture.root.join("Forge installation historique");
    let legacy_package = fixture.app("legacy", true);
    let legacy_module = fixture.pack("legacy-module", Kind::Module);
    let asset_library = fixture.pack("asset-library", Kind::Assets);
    let reference_source = fixture.root.join("reference-source");
    fs::create_dir_all(reference_source.join("data/scene")).unwrap();
    fs::write(
        reference_source.join("data/scene/static-assets.json"),
        br#"{"assets":[]}"#,
    )
    .unwrap();
    fs::write(reference_source.join("LICENSE"), b"fixture license").unwrap();
    let (mut reference_descriptor, _) = archive::inspect(&asset_library).unwrap();
    reference_descriptor.id = "reference-assets".into();
    let reference = fixture.root.join("reference-assets.forgepack");
    archive::pack(&reference_source, reference_descriptor, &reference).unwrap();
    let legacy_state = fixture.install_with_user(
        &legacy,
        &[
            legacy_package,
            legacy_module.clone(),
            asset_library.clone(),
            reference,
        ],
    );
    let workspace = fixture.user.join("workspaces/openrpg-forge");
    fs::create_dir_all(&workspace).unwrap();
    let project = workspace.join("project.txt");
    fs::write(&project, b"custom-root project").unwrap();
    let legacy_head = fs::read(legacy.join("installation.json")).unwrap();

    let root = fixture.root.join("StudioInstallation");
    let studio = fixture.app("studio", false);
    let (mut descriptor, _) = archive::inspect(&studio).unwrap();
    descriptor
        .product
        .as_mut()
        .unwrap()
        .optional_packs
        .push("reference-assets".into());
    let candidate = fixture.root.join("studio-with-reference.forgepack");
    archive::pack(&fixture.root.join("app-source-studio"), descriptor, &candidate).unwrap();
    let prepare = |name: &str, registered: &Path, explicit_user: Option<&Path>| {
        let path = fixture.root.join(name);
        let mut command = std::process::Command::new(env!("CARGO_BIN_EXE_forge_delivery"));
        command
            .arg("plan-studio-installer")
            .arg(&root)
            .arg(&path)
            .arg(&candidate)
            .env("FORGE_STUDIO_LEGACY_INSTALL_ROOT", registered)
            .env("LOCALAPPDATA", fixture.root.join("local-app-data"))
            .env_remove("FORGE_DELIVERY_USER_ROOT");
        if let Some(explicit_user) = explicit_user {
            command.env("FORGE_DELIVERY_USER_ROOT", explicit_user);
        }
        let output = command.output().unwrap();
        assert!(
            output.status.success(),
            "{}",
            String::from_utf8_lossy(&output.stderr)
        );
        forge_delivery::read_json::<install::Plan>(&path).unwrap()
    };
    let missing = fixture.root.join("ancienne installation absente");
    let fresh = prepare("fresh.json", &missing, None);
    assert_eq!(
        Path::new(&fresh.user_root),
        fixture.root.join("local-app-data/OpenRPG Forge/UserData")
    );
    let explicit = fixture.root.join("explicit-user");
    let explicit_plan = prepare("explicit.json", &legacy, Some(&explicit));
    assert_eq!(Path::new(&explicit_plan.user_root), explicit);
    assert_eq!(explicit_plan.inputs.len(), 1);
    let migration = prepare("migration.json", &legacy, None);
    assert_eq!(Path::new(&migration.user_root), fixture.user);
    assert_eq!(migration.inputs.len(), 3);
    let state = install::apply_studio_installer(&migration).unwrap();
    assert_eq!(Path::new(&state.user_root), fixture.user);
    for id in ["asset-library", "reference-assets"] {
        assert_eq!(state.active.packs[id], legacy_state.active.packs[id]);
    }
    let recover = |registered: &Path| {
        let output = std::process::Command::new(env!("CARGO_BIN_EXE_forge_delivery"))
            .arg("recover-legacy-modules")
            .arg(registered)
            .arg(&root)
            .output()
            .unwrap();
        assert!(
            output.status.success(),
            "{}",
            String::from_utf8_lossy(&output.stderr)
        );
        serde_json::from_slice::<Vec<serde_json::Value>>(&output.stdout).unwrap()
    };
    let recovered = recover(&legacy);
    assert_eq!(recovered.len(), 1);
    let package = Path::new(recovered[0]["package"].as_str().unwrap());
    assert_eq!(package.parent().unwrap(), fixture.user.join("recovered-modules"));
    assert_eq!(fs::read(package).unwrap(), fs::read(legacy_module).unwrap());
    assert!(recover(&missing).is_empty());
    assert_eq!(fs::read(&project).unwrap(), b"custom-root project");
    assert_eq!(
        fs::read(legacy.join("installation.json")).unwrap(),
        legacy_head
    );
    let update = prepare("update.json", &missing, Some(&explicit));
    assert_eq!(update.user_root, state.user_root);
    let repeated = prepare("repeated.json", &legacy, None);
    assert_eq!(repeated.inputs.len(), 1);
    assert_eq!(
        install::apply_studio_installer(&repeated).unwrap().revision,
        state.revision
    );
    install::uninstall(&legacy).unwrap();
    install::verify(&root).unwrap();
    let library = install::package_root(&root, &state.active.packs["asset-library"]).unwrap();
    let (descriptor, _) = archive::inspect(&asset_library).unwrap();
    assert_eq!(
        fs::read(library.join("package-manifest.json")).unwrap(),
        forge_delivery::json(&descriptor).unwrap()
    );
    assert_eq!(fs::read(library.join("data.bin")).unwrap(), b"inert content");
    let removed = install::uninstall_studio(&root).unwrap();
    assert_eq!(removed.preserved_packages, 2);
    let reinstallation = prepare("reinstall.json", &missing, None);
    assert_eq!(reinstallation.inputs.len(), 1);
    assert_eq!(reinstallation.target.packs, state.active.packs);
    let reinstalled = install::apply_studio_installer(&reinstallation).unwrap();
    assert_eq!(reinstalled.active.packs, state.active.packs);
    assert_eq!(reinstalled.user_root, state.user_root);
    install::verify(&root).unwrap();
    assert_eq!(fs::read(project).unwrap(), b"custom-root project");
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn studio_installer_retries_after_staging_fails_before_activation() {
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    let candidate = fixture.app("first", false);
    let marker = root.join(".studio-classic-installer-owned");
    let foreign = root.join("foreign.txt");
    fs::create_dir(&root).unwrap();
    fs::write(&foreign, b"foreign content").unwrap();
    let plan = fixture.prepare_with_user(&root, std::slice::from_ref(&candidate));
    assert!(install::apply_studio_installer(&plan).is_err());
    assert!(!marker.exists());
    assert_eq!(fs::read(&foreign).unwrap(), b"foreign content");
    fs::remove_file(foreign).unwrap();

    let bytes = fs::read(&candidate).unwrap();
    let mut corrupt = bytes.clone();
    *corrupt.last_mut().unwrap() ^= 1;
    fs::write(&candidate, corrupt).unwrap();
    let interrupted = fixture.prepare_with_user(&root, std::slice::from_ref(&candidate));
    assert!(install::apply_studio_installer(&interrupted).is_err());
    assert!(!root.join("installation.json").exists());
    assert_eq!(fs::read(&marker).unwrap(), b"pending\n");
    let stage_parent = root.join("packages").join(&interrupted.target.core.id);
    let stage = fs::read_dir(stage_parent).unwrap().next().unwrap().unwrap();
    assert!(stage.file_name().to_string_lossy().starts_with(".stage-"));

    fs::write(&candidate, bytes).unwrap();
    let retry = fixture.prepare_with_user(&root, &[candidate]);
    let resumed = install::apply_studio_installer(&retry).unwrap();
    assert_eq!(resumed.revision, 1);
    assert!(resumed.previous.is_none());
    assert!(stage.path().is_dir());
    install::verify(&root).unwrap();
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn studio_uninstall_resumes_after_a_locked_file_without_losing_workspace() {
    use std::os::windows::fs::OpenOptionsExt;
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    let state = fixture.install_with_user(
        &root,
        &[
            fixture.app("locked", false),
            fixture.pack("asset-library", Kind::Assets),
        ],
    );
    let library = install::package_root(&root, &state.active.packs["asset-library"]).unwrap();
    let workspace = fixture.user.join("workspaces/openrpg-forge");
    fs::create_dir_all(&workspace).unwrap();
    fs::write(workspace.join("project.txt"), b"my project").unwrap();
    let editor = install::package_root(&root, &state.active.core)
        .unwrap()
        .join(EDITOR);
    let handle = fs::OpenOptions::new()
        .read(true)
        // Permit inventory/hash reads while refusing Windows file deletion.
        .share_mode(0x0000_0001 | 0x0000_0002)
        .open(&editor)
        .unwrap();
    assert!(install::uninstall_studio(&root).is_err());
    assert!(root.join(".forge-uninstall.pending.json").is_file());
    assert!(install::state(&root).is_err());
    assert_eq!(fs::read(library.join("data.bin")).unwrap(), b"inert content");
    assert_eq!(
        fs::read(workspace.join("project.txt")).unwrap(),
        b"my project"
    );
    drop(handle);
    install::uninstall_studio(&root).unwrap();
    assert!(!root.join(".forge-uninstall.pending.json").exists());
    assert!(!root.join("installation.json").exists());
    assert_eq!(fs::read(library.join("data.bin")).unwrap(), b"inert content");
    install::check_studio_reinstall_root(&root, None).unwrap();
    assert_eq!(
        fs::read(workspace.join("project.txt")).unwrap(),
        b"my project"
    );
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn studio_uninstall_refuses_previous_legacy_revision_before_removing_anything() {
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    let original = fixture.install_with_user(&root, &[fixture.app("legacy", true)]);
    let current = fixture.install_with_user(&root, &[fixture.app("current", false)]);
    let old_path = install::package_root(&root, &original.active.core).unwrap();
    let new_path = install::package_root(&root, &current.active.core).unwrap();
    let before = fs::read(root.join("installation.json")).unwrap();
    assert!(install::uninstall_studio(&root).is_err());
    assert_eq!(fs::read(root.join("installation.json")).unwrap(), before);
    assert!(old_path.join(EDITOR).exists());
    assert!(new_path.join(EDITOR).exists());
}

#[cfg(all(target_os = "windows", target_arch = "x86_64"))]
#[test]
fn studio_uninstall_preserves_the_library_and_refuses_unknown_revisions() {
    let fixture = Fixture::new();
    let root = fixture.root.join("installation");
    let state = fixture.install_with_user(
        &root,
        &[
            fixture.app("with-assets", false),
            fixture.pack("asset-library", Kind::Assets),
        ],
    );
    let content = install::package_root(&root, &state.active.packs["asset-library"]).unwrap();
    let editor = install::package_root(&root, &state.active.core).unwrap();
    let report = install::uninstall_studio(&root).unwrap();
    assert_eq!(report.preserved_packages, 1);
    assert!(!root.join("installation.json").exists());
    assert!(!editor.exists());
    assert_eq!(
        fs::read(content.join("data.bin")).unwrap(),
        b"inert content"
    );
    assert_eq!(install::uninstall_studio(&root).unwrap().preserved_packages, 1);
    let v1_receipt = format!(
        r#"{{"schema":"forge.delivery.retained-studio-library.v1","user_root":{},"pack":{}}}"#,
        serde_json::to_string(&state.user_root).unwrap(),
        serde_json::to_string(&state.active.packs["asset-library"]).unwrap(),
    );
    fs::write(root.join(".forge-studio-library.json"), v1_receipt).unwrap();
    let candidate = fixture.app("reinstalled", false);
    install::check_studio_reinstall_root(&root, Some(&candidate)).unwrap();
    let plan = install::prepare_studio_installer(&root, &candidate, None).unwrap();
    assert_eq!(plan.inputs.len(), 1);
    assert_eq!(Path::new(&plan.user_root), fixture.user);
    assert_eq!(
        plan.target.packs["asset-library"],
        state.active.packs["asset-library"]
    );
    let asset = content.join("data.bin");
    fs::write(&asset, b"changed during uninstall residue").unwrap();
    assert!(install::apply_studio_installer(&plan).is_err());
    assert!(!root.join("installation.json").exists());
    assert_eq!(fs::read(&asset).unwrap(), b"changed during uninstall residue");
    fs::write(&asset, b"inert content").unwrap();
    install::apply_studio_installer(&plan).unwrap();
    install::verify(&root).unwrap();
    assert_eq!(fs::read(asset).unwrap(), b"inert content");

    let other_root = fixture.root.join("other-installation");
    let other = fixture.install_with_user(&other_root, &[fixture.app("unknown", false)]);
    let unknown = other_root.join("packages/forge-studio/unrecognized");
    fs::create_dir(&unknown).unwrap();
    fs::write(unknown.join("keep.txt"), b"unrecognized data").unwrap();
    let before = fs::read(other_root.join("installation.json")).unwrap();
    assert!(install::uninstall_studio(&other_root).is_err());
    assert_eq!(
        fs::read(other_root.join("installation.json")).unwrap(),
        before
    );
    assert_eq!(
        fs::read(unknown.join("keep.txt")).unwrap(),
        b"unrecognized data"
    );
    assert!(
        install::package_root(&other_root, &other.active.core)
            .unwrap()
            .join(EDITOR)
            .exists()
    );
}
