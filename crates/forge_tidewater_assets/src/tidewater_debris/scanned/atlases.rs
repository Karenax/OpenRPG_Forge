//! Compatibility export for the shared scanned atlases decoder.
pub use forge_assets::source_scanned_package::atlases::*;

#[cfg(test)]
mod tests {
    use super::*;
    use crate::tidewater_debris::scanned::ScannedPackage;

    #[test]
    #[ignore = "Requires the acquired standalone scanned atlas payloads"]
    fn acquired_mips_keep_source_colour_formats_and_sampler() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let raw = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&raw, |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let atlases = prepare(&package).unwrap();
        assert_eq!(atlases[0].format, Format::SrgbRgba8);
        assert_eq!(atlases[1].format, Format::LinearRgba8);
        assert_eq!(atlases[2].format, Format::LinearRgba8);
        for atlas in &atlases {
            assert_eq!(atlas.mip_offsets[1], 2048 * 2048 * 4);
            assert_eq!(atlas.mip_offsets[12], 22_369_620);
            assert_eq!(atlas.mip_offsets[12] - atlas.mip_offsets[11], 4);
            assert_eq!(atlas.mip_count, 12);
            assert_eq!(atlas.anisotropy, 8);
        }
    }
}
