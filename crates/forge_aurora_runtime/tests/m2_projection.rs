use forge_aurora_bridge::load_bundle;
use forge_aurora_runtime::{project_module, run_scripted_visit, validate_forge_navigation};
use forge_core::bytes_sha256;
use forge_project::module_package::{ModuleFile, ModuleFileRole, ModulePackageManifest};
use std::fs;
use std::path::{Path, PathBuf};

fn fixture_bundle() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
}

#[test]
fn projects_the_committed_bundle_and_runs_the_multi_zone_visit() {
    let imported = load_bundle(&fixture_bundle()).expect("import");
    let projected = project_module(&imported).expect("projection");
    assert_eq!(projected.zones.len(), 2);
    assert_eq!(projected.entry_area, "ab_camp");
    assert_eq!(projected.manifest.zones.len(), 2);
    for zone in projected.zones.values() {
        zone.terrain.validate().expect("terrain");
        zone.gameplay.validate().expect("gameplay");
        assert!(!zone.walkmesh.triangles.is_empty());
        assert!(!zone.doors.is_empty());
        let navigation = validate_forge_navigation(zone).expect("forge navigation");
        assert!(navigation.source_triangles > 0);
        // The committed M1 fixture has no WOK and uses a two-triangle fallback plane;
        // Recast legitimately emits zero regions for that degenerate fallback. The local
        // playable fixture with real WOK geometry emits 81 and 52 polygons.
    }
    let report = run_scripted_visit(projected.clone()).expect("visit");
    assert_eq!(report.transitions, 2);
    assert_eq!(report.doors_opened, 2);
    assert_eq!(report.entry_area, "ab_camp");
    assert_eq!(report.final_area, "ab_camp");
    assert!(report.zones_visited.contains(&"ab_camp".to_owned()));
    assert!(report.zones_visited.contains(&"ab_crypt".to_owned()));
    assert!(report.distance_m > 0.0);
    assert_eq!(report.loaded_areas, vec!["ab_camp".to_owned()]);
}

#[test]
fn writes_a_valid_forge_module_package_with_exact_inventory() {
    let imported = load_bundle(&fixture_bundle()).expect("import");
    let projected = project_module(&imported).expect("projection");
    let root = std::env::temp_dir().join(format!(
        "forge-aurora-package-{}-{}",
        std::process::id(),
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("clock")
            .as_nanos()
    ));
    let _ = fs::remove_dir_all(&root);
    let report = projected.write_package(&root).expect("package");
    assert_eq!(report.zone_count, 2);

    let manifest_bytes = fs::read(root.join("module.json")).expect("manifest");
    let manifest = ModulePackageManifest::from_bytes(&manifest_bytes).expect("valid manifest");
    assert_eq!(manifest.zones.len(), 2);
    assert_eq!(manifest.assets.len(), projected.assets.len());
    let actual = collect_files(&root);
    let actual_manifest = actual
        .iter()
        .map(|(path, bytes)| ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256: bytes_sha256(bytes),
            role: manifest
                .files
                .iter()
                .find(|file| file.path == *path)
                .map_or(ModuleFileRole::Provenance, |file| file.role),
        })
        .collect::<Vec<_>>();
    manifest
        .validate_inventory(&actual_manifest)
        .expect("exact package inventory");
    assert!(manifest.files.iter().all(|file| file.path != "module.json"));
    let _ = fs::remove_dir_all(root);
}

fn collect_files(root: &Path) -> Vec<(String, Vec<u8>)> {
    fn walk(root: &Path, directory: &Path, files: &mut Vec<(String, Vec<u8>)>) {
        let mut entries = fs::read_dir(directory)
            .expect("directory")
            .collect::<Result<Vec<_>, _>>()
            .expect("entries");
        entries.sort_by_key(std::fs::DirEntry::file_name);
        for entry in entries {
            let path = entry.path();
            if path.is_dir() {
                walk(root, &path, files);
            } else {
                let relative = path
                    .strip_prefix(root)
                    .expect("relative")
                    .to_string_lossy()
                    .replace('\\', "/");
                if relative == "module.json" {
                    continue;
                }
                files.push((relative, fs::read(&path).expect("file")));
            }
        }
    }
    let mut files = Vec::new();
    walk(root, root, &mut files);
    files
}
