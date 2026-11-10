use forge_aurora_bridge::{load_bundle, validate_bundle};
use sha2::{Digest, Sha256};
use std::fs;
use std::path::{Path, PathBuf};

fn fixture_bundle() -> PathBuf {
    std::env::var_os("AURORA_BRIDGE_BUNDLE").map_or_else(
        || {
            PathBuf::from(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/ab_proving.module-migration-v1")
        },
        PathBuf::from,
    )
}

fn committed_fixture_bundle() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/ab_proving.module-migration-v1")
}

fn sha256(path: &Path) -> String {
    let bytes = fs::read(path).expect("file");
    let mut digest = String::with_capacity(64);
    for byte in Sha256::digest(bytes) {
        use std::fmt::Write as _;
        let _ = write!(digest, "{byte:02x}");
    }
    digest
}

fn tree_hashes(root: &Path) -> std::collections::BTreeMap<String, String> {
    let mut files = std::collections::BTreeMap::new();
    walk_hashes(root, root, &mut files);
    files
}

fn walk_hashes(
    root: &Path,
    directory: &Path,
    files: &mut std::collections::BTreeMap<String, String>,
) {
    let mut entries = fs::read_dir(directory)
        .expect("directory")
        .collect::<Result<Vec<_>, _>>()
        .expect("entries");
    entries.sort_by_key(std::fs::DirEntry::file_name);
    for entry in entries {
        let path = entry.path();
        if path.is_dir() {
            walk_hashes(root, &path, files);
        } else {
            let relative = path
                .strip_prefix(root)
                .expect("relative")
                .to_string_lossy()
                .replace('\\', "/");
            files.insert(relative, sha256(&path));
        }
    }
}

#[test]
fn loads_the_proving_grounds_bundle_in_read_only_mode() {
    let module = load_bundle(&fixture_bundle()).expect("bundle import");
    assert_eq!(
        module.manifest.schema_version,
        "module-migration-bundle@1.0.0"
    );
    assert_eq!(module.manifest.source.module_resref, "ab_proving");
    assert_eq!(module.module.identity.tag, "AB_PROVING");
    assert_eq!(module.module.entry_area_resref, "ab_camp");
    assert_eq!(module.areas.len(), 2);
    assert_eq!(module.report.counts.areas, 2);
    assert_eq!(module.report.counts.tiles, 56);
    assert_eq!(module.report.counts.instances, 9);
    assert_eq!(module.report.counts.blueprints, 10);
    assert_eq!(module.report.counts.dialogs, 1);
    assert_eq!(module.report.counts.journal_entries, 3);
    assert_eq!(module.report.counts.factions, 3);
    assert_eq!(module.report.counts.scripts_ncs, 14);
    assert_eq!(module.report.counts.scripts_nss, 14);
    assert_eq!(module.report.counts.areas, module.areas.len());
    assert!(module.report.importable);
    assert!(!module.report.complete);
    assert!(!module.report.playable);
    assert!(module.report.errors.is_empty());

    let camp = module
        .areas
        .iter()
        .find(|area| area.manifest.resref == "ab_camp")
        .expect("camp area");
    assert_eq!(camp.objects.len(), 4);
    assert!(
        camp.objects.iter().any(
            |object| object.category == "creature" && object.tag.as_deref() == Some("AB_GUIDE")
        )
    );
    let crypt = module
        .areas
        .iter()
        .find(|area| area.manifest.resref == "ab_crypt")
        .expect("crypt area");
    assert_eq!(crypt.objects.len(), 5);
    assert!(
        crypt
            .objects
            .iter()
            .any(|object| object.category == "placeable"
                && object.tag.as_deref() == Some("AB_CHEST"))
    );
    assert!(
        module
            .blueprints
            .iter()
            .any(|blueprint| blueprint.resref == "ab_guide")
    );
    assert!(
        module
            .dialogs
            .iter()
            .any(|dialog| dialog.resref == "ab_guide_dlg" && dialog.nodes == 9)
    );
    assert!(
        module
            .scripts
            .scripts
            .iter()
            .any(|script| script["resref"] == "ab_mod_load")
    );
    assert!(
        module
            .diagnostics
            .iter()
            .any(|diagnostic| diagnostic["code"] == "MIGRATION_EXTERNAL_ASSETS_EXCLUDED")
    );
}

#[test]
fn validated_bundle_matches_the_contract_lock() {
    let root = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../..");
    let lock_path = root.join("docs/aurora-bridge/CONTRACT.lock.json");
    let lock: serde_json::Value =
        serde_json::from_slice(&fs::read(&lock_path).expect("contract lock")).expect("lock JSON");
    assert_eq!(lock["status"], "frozen");
    assert_eq!(lock["frozen"], true);
    assert_eq!(lock["version"], "1.0.0");
    let mirror = root.join(lock["mirroredPath"].as_str().expect("mirroredPath"));
    let digest = sha256(&mirror);
    assert_eq!(lock["mirroredSha256"], digest);
    assert_eq!(lock["frozenSha256"], digest);
    let validated = validate_bundle(&fixture_bundle()).expect("validated bundle");
    assert_eq!(
        validated.manifest.schema_version,
        "module-migration-bundle@1.0.0"
    );
}

#[test]
fn producer_generated_bundle_matches_the_committed_consumer_fixture() {
    let Some(producer) = std::env::var_os("AURORA_BRIDGE_BUNDLE").map(PathBuf::from) else {
        eprintln!("AURORA_BRIDGE_BUNDLE is not set; producer/consumer byte comparison skipped");
        return;
    };
    assert_eq!(
        tree_hashes(&producer),
        tree_hashes(&committed_fixture_bundle()),
        "the OpenNever producer bundle and the committed consumer fixture must be identical"
    );
}

#[test]
fn refuses_a_bundle_with_a_missing_payload() {
    let source = fixture_bundle();
    let destination = unique_temp_dir();
    copy_directory(&source, &destination);
    fs::remove_file(destination.join("module.json")).expect("remove module.json");
    let error = load_bundle(&destination).expect_err("missing payload must be refused");
    assert!(error.to_string().contains("module.json"), "{error}");
    let _ = fs::remove_dir_all(destination);
}

#[test]
fn refuses_an_unlisted_file_inside_a_nested_area() {
    let source = fixture_bundle();
    let destination = unique_temp_dir();
    copy_directory(&source, &destination);
    let extra = destination.join("areas/ab_camp.area-migration-v1/extra.bin");
    fs::write(&extra, b"unlisted").expect("extra area file");
    let error = load_bundle(&destination).expect_err("unlisted area file must be refused");
    assert!(
        error.to_string().contains("area inventory mismatch"),
        "{error}"
    );
    let _ = fs::remove_dir_all(destination);
}

fn unique_temp_dir() -> PathBuf {
    use std::sync::atomic::{AtomicUsize, Ordering};
    static COUNTER: AtomicUsize = AtomicUsize::new(0);
    let base = std::env::temp_dir();
    let pid = std::process::id();
    for _ in 0..1000 {
        let counter = COUNTER.fetch_add(1, Ordering::Relaxed);
        let candidate = base.join(format!("forge-aurora-bridge-{pid}-{counter}"));
        if !candidate.exists() {
            fs::create_dir_all(&candidate).expect("temporary directory");
            return candidate;
        }
    }
    panic!("cannot allocate a unique temporary directory");
}

fn copy_directory(source: &Path, destination: &Path) {
    fs::create_dir_all(destination).expect("destination directory");
    let mut entries = fs::read_dir(source)
        .expect("source directory")
        .collect::<Result<Vec<_>, _>>()
        .expect("entries");
    entries.sort_by_key(std::fs::DirEntry::file_name);
    for entry in entries {
        let path = entry.path();
        let target = destination.join(entry.file_name());
        if path.is_dir() {
            copy_directory(&path, &target);
        } else {
            fs::copy(&path, &target).expect("copy file");
        }
    }
}
