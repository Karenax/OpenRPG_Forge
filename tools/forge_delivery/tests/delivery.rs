use forge_delivery::{
    archive, hash, install,
    manifest::{Descriptor, Entry, Kind, Launch, Product, Rights, relative},
};
use std::{
    collections::BTreeMap,
    fs,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};
static NEXT: AtomicU64 = AtomicU64::new(0);

struct Fixture(PathBuf);
impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-delivery-test-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        Self(root)
    }
    fn child(&self, name: &str) -> PathBuf {
        self.0.join(name)
    }
    fn pack(&self, id: &str, version: &str, kind: Kind) -> PathBuf {
        let source = self.child(&format!("source-{id}-{version}"));
        fs::create_dir(&source).unwrap();
        fs::write(source.join("LICENSE"), b"local test fixture").unwrap();
        fs::write(source.join("payload.bin"), version).unwrap();
        let descriptor = descriptor(id, version, kind);
        let output = self.child(&format!("{id}-{version}.forgepack"));
        archive::pack(&source, descriptor, &output).unwrap();
        output
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        fs::remove_dir_all(&self.0).unwrap();
    }
}

fn descriptor(id: &str, version: &str, kind: Kind) -> Descriptor {
    let product = (kind == Kind::Applications).then(|| Product {
        schema: "forge.delivery.product.v1".into(),
        id: "test-product".into(),
        title: "Test".into(),
        apps: BTreeMap::from([("game".into(), launch()), ("editor".into(), launch())]),
        required_packs: Vec::new(),
        optional_packs: Vec::new(),
        authoring_seed: None,
    });
    Descriptor {
        schema: "forge.delivery.package.v1".into(),
        id: id.into(),
        label: id.into(),
        version: version.into(),
        kind,
        platform: "any".into(),
        source_revision: "test".into(),
        rights: Rights::PrivateLocalOnly,
        license_paths: vec!["LICENSE".into()],
        files: vec![],
        product,
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
fn rejects_portable_path_aliases_and_traversal() {
    for path in [
        "../escape",
        "/root",
        "C:/escape",
        "x\\y",
        "CON.txt",
        "a/aux",
        "lpt9.bin",
        "a/./b",
        "a//b",
        "a./b",
        "a /b",
        "a\0b",
        "a:stream",
        "a/../b",
        "a?b",
    ] {
        assert!(relative(path).is_err(), "{path:?}");
    }
    for path in [
        "a/b.glb",
        "Licences/étoile.txt",
        "content/geometry/abcdef.bin",
    ] {
        relative(path).unwrap();
    }
}

#[test]
fn rejects_duplicate_case_and_file_directory_conflicts() {
    let mut d = descriptor("assets", "1", Kind::Assets);
    d.files = vec![
        Entry {
            path: "LICENSE".into(),
            bytes: 1,
            sha256: hash(b"a"),
        },
        Entry {
            path: "license".into(),
            bytes: 1,
            sha256: hash(b"b"),
        },
    ];
    assert!(d.validate().is_err());
    d.files[1].path = "LICENSE/x".into();
    assert!(d.validate().is_err());
    d.files[1].path = "app.exe".into();
    assert!(d.validate().is_err());
}

#[test]
fn archive_roundtrip_and_corruption_leave_active_head_unchanged() {
    let f = Fixture::new();
    let archive = f.pack("applications", "1", Kind::Applications);
    let root = f.child("installed");
    let plan = install::prepare(&root, std::slice::from_ref(&archive)).unwrap();
    assert!(!root.exists(), "prepare must be read-only");
    let result = install::apply(&plan).unwrap();
    assert_eq!(result.revision, 1);
    assert_eq!(
        install::apply(&plan).unwrap().revision,
        1,
        "retry must be idempotent"
    );
    install::verify(&root).unwrap();
    let second = f.pack("applications", "2", Kind::Applications);
    let update = install::prepare(&root, std::slice::from_ref(&second)).unwrap();
    let mut bytes = fs::read(&second).unwrap();
    let last = bytes.len() - 1;
    bytes[last] ^= 1;
    fs::write(&second, bytes).unwrap();
    assert!(install::apply(&update).is_err());
    assert_eq!(install::state(&root).unwrap().revision, 1);
}

#[test]
fn stale_plan_rejected_and_rollback_retains_versions_and_user_files() {
    let f = Fixture::new();
    let root = f.child("installed");
    let first = f.pack("applications", "1", Kind::Applications);
    let second = f.pack("applications", "2", Kind::Applications);
    let stale = install::prepare(&root, std::slice::from_ref(&second)).unwrap();
    install::apply(&install::prepare(&root, &[first]).unwrap()).unwrap();
    assert!(install::apply(&stale).is_err());
    fs::write(root.join("user-save.json"), b"keep").unwrap();
    let update = install::prepare(&root, &[second]).unwrap();
    install::apply(&update).unwrap();
    let rollback = install::prepare_rollback(&root).unwrap();
    let state = install::apply(&rollback).unwrap();
    assert_eq!(state.revision, 3);
    assert_eq!(fs::read(root.join("user-save.json")).unwrap(), b"keep");
    let core = install::package_root(&root, &state.active.core).unwrap();
    assert_eq!(fs::read(core.join("payload.bin")).unwrap(), b"1");
    assert!(state.previous.is_some());
    install::verify(&root).unwrap();
}

#[test]
fn changed_plan_and_truncated_or_trailing_archives_are_rejected() {
    let f = Fixture::new();
    let file = f.pack("applications", "1", Kind::Applications);
    let mut plan = install::prepare(&f.child("installed"), std::slice::from_ref(&file)).unwrap();
    plan.root = f.child("other").to_string_lossy().into_owned();
    assert!(install::apply(&plan).is_err());
    assert!(!f.child("other").exists());
    let bytes = fs::read(&file).unwrap();
    fs::write(&file, &bytes[..bytes.len() - 1]).unwrap();
    assert!(archive::inspect(&file).is_err());
    let mut extra = bytes;
    extra.push(0);
    fs::write(&file, extra).unwrap();
    assert!(archive::inspect(&file).is_err());
}

#[test]
fn detects_installed_corruption_and_lock_contention() {
    let f = Fixture::new();
    let root = f.child("installed");
    let file = f.pack("applications", "1", Kind::Applications);
    let plan = install::prepare(&root, &[file]).unwrap();
    install::apply(&plan).unwrap();
    let other_lock = fs::OpenOptions::new()
        .read(true)
        .write(true)
        .open(root.join("installation.lock"))
        .unwrap();
    other_lock.lock().unwrap();
    assert!(install::apply(&plan).is_err());
    drop(other_lock);
    let core = install::package_root(&root, &install::state(&root).unwrap().active.core).unwrap();
    fs::write(core.join("payload.bin"), b"tamper").unwrap();
    assert!(install::verify(&root).is_err());
    assert!(install::apply(&plan).is_err());
}

#[test]
fn dependencies_are_exact_and_assets_install_separately() {
    let f = Fixture::new();
    let assets = f.pack("assets", "1", Kind::Assets);
    let (_, pin) = archive::inspect(&assets).unwrap();
    let source = f.child("core-src");
    fs::create_dir(&source).unwrap();
    fs::write(source.join("LICENSE"), "test").unwrap();
    fs::write(source.join("payload.bin"), "1").unwrap();
    let mut core = descriptor("applications", "1", Kind::Applications);
    core.product
        .as_mut()
        .unwrap()
        .required_packs
        .push(forge_delivery::manifest::PackRef {
            id: "assets".into(),
            manifest_sha256: pin,
            kind: Kind::Assets,
        });
    let application = f.child("core.forgepack");
    archive::pack(&source, core, &application).unwrap();
    let root = f.child("installed");
    assert!(install::prepare(&root, std::slice::from_ref(&application)).is_err());
    let plan = install::prepare(&root, &[assets, application]).unwrap();
    install::apply(&plan).unwrap();
    assert_eq!(install::verify(&root).unwrap().active.packs.len(), 1);
    let extra = f.pack("library", "1", Kind::Assets);
    let plan = install::prepare(&root, &[extra]).unwrap();
    install::apply(&plan).unwrap();
    assert_eq!(install::verify(&root).unwrap().active.packs.len(), 2);
}

#[test]
fn launch_arguments_are_native_without_shell_interpolation() {
    let f = Fixture::new();
    let source = f.child("launch-src");
    fs::create_dir(&source).unwrap();
    fs::write(source.join("LICENSE"), "test").unwrap();
    fs::write(source.join("payload.bin"), "test").unwrap();
    let mut core = descriptor("applications", "1", Kind::Applications);
    let app = core.product.as_mut().unwrap().apps.get_mut("game").unwrap();
    app.args = vec!["hello; $(arbitrary)".into()];
    let archive = f.child("launch.forgepack");
    archive::pack(&source, core, &archive).unwrap();
    let root = f.child("installed");
    install::apply(&install::prepare(&root, &[archive]).unwrap()).unwrap();
    // Native Command carries the complete string as one argument.
    let command = forge_delivery::launch::command(&root, "game").unwrap();
    assert_eq!(command.get_args().next().unwrap(), "hello; $(arbitrary)");
}
