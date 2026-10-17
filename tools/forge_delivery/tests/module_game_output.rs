#[path = "../../../crates/forge_engine/examples/module_game/output.rs"]
mod output;
use std::{
    fs,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};
static NEXT: AtomicU64 = AtomicU64::new(0);
struct Temp(PathBuf);
impl Temp {
    fn new() -> Self {
        let path = std::env::temp_dir().join(format!(
            "forge-module-output-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&path).unwrap();
        Self(path)
    }
}
impl Drop for Temp {
    fn drop(&mut self) {
        if self.0.is_absolute() && self.0.parent() == Some(std::env::temp_dir().as_path()) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }
}

#[test]
fn ordinary_package_paths_and_canonical_output_paths_cannot_bypass_installed_source_protection() {
    let temp = Temp::new();
    let module = temp.0.join("Installed Élan");
    fs::create_dir(&module).unwrap();
    let save = module.join("user-saves");
    assert!(output::prepare(&save, None, &module).is_err());
    assert!(!save.exists());
    let save = temp.0.join("external-save");
    let capture = module.join("captures/frame.png");
    assert!(output::prepare(&save, Some(&capture), &module).is_err());
    assert!(!save.exists() && !capture.parent().unwrap().exists());
}

#[test]
fn external_unicode_outputs_are_allowed_but_an_existing_capture_is_preserved() {
    let temp = Temp::new();
    let module = temp.0.join("Installed Élan");
    fs::create_dir(&module).unwrap();
    let save = temp.0.join("Données Élan");
    let capture = temp.0.join("Captures Élan/frame.png");
    output::prepare(&save, Some(&capture), &module).unwrap();
    assert!(save.is_dir());
    fs::write(&capture, b"existing evidence").unwrap();
    assert!(output::prepare(&save, Some(&capture), &module).is_err());
    assert_eq!(fs::read(&capture).unwrap(), b"existing evidence");
}
