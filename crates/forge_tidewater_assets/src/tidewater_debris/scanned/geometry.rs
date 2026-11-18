//! Compatibility export for the shared scanned geometry decoder.
pub use forge_assets::source_scanned_package::geometry::*;

#[cfg(test)]
mod tests {
    use super::*;
    use crate::tidewater_debris::scanned::ScannedPackage;

    #[test]
    #[ignore = "Requires the acquired standalone scanned package"]
    fn decoded_prototypes_preserve_all_source_lods() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let raw = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&raw, |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let geometry = prepare(&package).unwrap();
        assert_eq!(geometry.instances.len(), 141);
        for (lod, expected) in [(0, 10_598), (1, 1_999), (2, 478)] {
            assert_eq!(
                geometry
                    .prototypes
                    .iter()
                    .filter(|p| p.lod == lod)
                    .map(|p| p.indices.len() / 3)
                    .sum::<usize>(),
                expected
            );
        }
        assert_eq!(geometry.prototypes.len(), 12);
    }
}
