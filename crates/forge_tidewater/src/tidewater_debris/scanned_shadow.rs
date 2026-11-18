//! Tidewater's acquired depth program regression fixture.
pub use forge_engine::source_surface::scanned_shadow::shader_source;

#[cfg(test)]
mod tests {
    use super::*;
    use forge_tidewater_assets::tidewater_debris::scanned::ScannedPackage;
    #[test]
    #[ignore = "Requires acquired package and a fresh shader output path"]
    fn export_exact_source_depth_entries() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let package = ScannedPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            |name, _| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
        )
        .unwrap();
        let shader = shader_source(&package).unwrap();
        if let Ok(path) = std::env::var("FORGE_DEBRIS_SCANNED_DEPTH_SHADER_OUTPUT") {
            use std::io::Write;
            std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(path)
                .unwrap()
                .write_all(shader.as_bytes())
                .unwrap();
        }
        assert!(shader.contains(
            std::str::from_utf8(package.file("material/scanned-shadow.wgsl").unwrap()).unwrap()
        ));
    }
}
