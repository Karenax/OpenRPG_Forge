use forge_delivery::{
    archive, hash,
    install::{self, InstalledPack, Plan, Snapshot, State},
    json,
    manifest::{Descriptor, Kind, Launch, Product, Rights},
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
}

impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-delivery-uninstall-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        Self { root }
    }

    fn path(&self, name: &str) -> PathBuf {
        self.root.join(name)
    }

    fn application_archive(&self) -> (PathBuf, Descriptor) {
        let source = self.path("source é space");
        fs::create_dir(&source).unwrap();
        fs::write(source.join("LICENSE"), b"local license").unwrap();
        fs::write(source.join("payload.bin"), b"original payload").unwrap();
        fs::write(source.join("données é space.txt"), b"owned unicode").unwrap();
        let descriptor = Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: "applications".into(),
            label: "Applications".into(),
            version: "1".into(),
            kind: Kind::Applications,
            platform: "any".into(),
            source_revision: "test".into(),
            rights: Rights::PrivateLocalOnly,
            license_paths: vec!["LICENSE".into()],
            files: vec![],
            product: Some(Product {
                schema: "forge.delivery.product.v1".into(),
                id: "test-product".into(),
                title: "Test".into(),
                apps: BTreeMap::from([("game".into(), launch()), ("editor".into(), launch())]),
                required_packs: Vec::new(),
                optional_packs: Vec::new(),
                authoring_seed: None,
            }),
        };
        let archive_path = self.path("package é space.forgepack");
        archive::pack(&source, descriptor.clone(), &archive_path).unwrap();
        (archive_path, descriptor)
    }

    fn install_fixture(&self) -> (PathBuf, PathBuf, PathBuf, String) {
        let root = self.path("Installation é space");
        let user_root = root.join("User Data é space");
        let (archive_path, _) = self.application_archive();
        let (_, revision) = archive::inspect(&archive_path).unwrap();
        let core = InstalledPack {
            id: "applications".into(),
            revision: revision.clone(),
            kind: Kind::Applications,
        };
        let package_root = install::package_root(&root, &core).unwrap();
        fs::create_dir_all(package_root.parent().unwrap()).unwrap();
        archive::extract(&archive_path, &package_root, &revision).unwrap();
        fs::create_dir_all(&user_root).unwrap();
        fs::write(user_root.join("save é space.json"), b"user edit").unwrap();
        fs::write(package_root.join("foreign.keep"), b"foreign file").unwrap();
        fs::write(package_root.join("payload.bin"), b"user-modified payload").unwrap();
        let state = State {
            schema: "forge.delivery.installation.v1".into(),
            revision: 1,
            user_root: user_root.to_string_lossy().into_owned(),
            active: Snapshot {
                core,
                packs: BTreeMap::new(),
            },
            previous: None,
            receipts: Vec::new(),
        };
        fs::create_dir_all(&root).unwrap();
        fs::write(root.join("installation.json"), json(&state).unwrap()).unwrap();
        (root, user_root, package_root, revision)
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

fn launch() -> Launch {
    Launch {
        executable: "payload.bin".into(),
        args: Vec::new(),
        env: BTreeMap::new(),
        clear_env_prefixes: Vec::new(),
    }
}

#[test]
fn plan_file_is_absolute_create_new_and_preserves_existing_bytes() {
    let fixture = Fixture::new();
    let root = fixture.path("root");
    fs::create_dir(&root).unwrap();
    let plan = Plan {
        schema: "forge.delivery.plan.v1".into(),
        root: root.to_string_lossy().into_owned(),
        user_root: fixture.path("User Data").to_string_lossy().into_owned(),
        expected_head: hash(&[]),
        base_revision: 0,
        command_id: "command".into(),
        operation: "install".into(),
        inputs: Vec::new(),
        target: Snapshot {
            core: InstalledPack {
                id: "applications".into(),
                revision: "a".repeat(64),
                kind: Kind::Applications,
            },
            packs: BTreeMap::new(),
        },
    };
    let output = fixture.path("plan é space.json");
    install::write_plan_file(&plan, &output).unwrap();
    let before = fs::read(&output).unwrap();
    assert_eq!(before, json(&plan).unwrap());
    assert!(install::write_plan_file(&plan, &output).is_err());
    assert_eq!(fs::read(&output).unwrap(), before);
    assert!(install::write_plan_file(&plan, std::path::Path::new("relative.json")).is_err());
}

#[test]
fn uninstall_removes_only_matching_files_and_preserves_user_and_foreign_content() {
    let fixture = Fixture::new();
    let (root, user_root, package_root, _) = fixture.install_fixture();
    install::uninstall(&root).unwrap();
    assert!(!root.join("installation.json").exists());
    assert!(root.join("installation.lock").exists());
    assert_eq!(
        fs::read(user_root.join("save é space.json")).unwrap(),
        b"user edit"
    );
    assert_eq!(
        fs::read(package_root.join("payload.bin")).unwrap(),
        b"user-modified payload"
    );
    assert_eq!(
        fs::read(package_root.join("foreign.keep")).unwrap(),
        b"foreign file"
    );
    assert!(!package_root.join("LICENSE").exists());
    assert!(!package_root.join("données é space.txt").exists());
    assert!(package_root.join("package-manifest.json").exists());
    let repeat = install::uninstall(&root).unwrap();
    assert_eq!(repeat.removed_files, 0);
    assert_eq!(repeat.preserved_files, 0);
}

#[test]
fn corrupt_manifest_refuses_without_removing_head_or_package_files() {
    let fixture = Fixture::new();
    let (root, _, package_root, _) = fixture.install_fixture();
    let state_before = fs::read(root.join("installation.json")).unwrap();
    fs::write(package_root.join("package-manifest.json"), b"{}").unwrap();
    assert!(install::uninstall(&root).is_err());
    assert_eq!(
        fs::read(root.join("installation.json")).unwrap(),
        state_before
    );
    assert!(package_root.join("LICENSE").exists());
    assert!(package_root.join("payload.bin").exists());
}

#[cfg(windows)]
#[test]
fn uninstall_refuses_links_before_mutating_the_head() {
    use std::os::windows::fs::symlink_file;

    let fixture = Fixture::new();
    let (root, _, package_root, _) = fixture.install_fixture();
    let target = fixture.path("link-target.txt");
    fs::write(&target, b"target").unwrap();
    let link = package_root.join("foreign-link.txt");
    if let Err(error) = symlink_file(&target, &link) {
        if matches!(error.raw_os_error(), Some(1 | 1314)) {
            return;
        }
        panic!("cannot create link fixture: {error}");
    }
    let state_before = fs::read(root.join("installation.json")).unwrap();
    assert!(install::uninstall(&root).is_err());
    assert_eq!(
        fs::read(root.join("installation.json")).unwrap(),
        state_before
    );
    assert!(package_root.join("LICENSE").exists());
}
