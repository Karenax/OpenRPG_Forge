//! Exact Tidewater acquisition, consumed by the common scan package reader.
use forge_core::ContentHash;
use std::ops::Deref;

#[path = "scanned/atlases.rs"]
pub mod atlases;
#[path = "scanned/geometry.rs"]
pub mod geometry;
#[path = "scanned/templates.rs"]
pub mod templates;

const PIN: &str = "f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4";
/// Acquired package revision, not a rendered or authoring qualification.
///
/// # Panics
/// Only if the compiled SHA-256 literal is invalid.
#[must_use]
pub fn revision() -> ContentHash {
    PIN.parse().expect("Pinned scanned package hash is valid")
}

/// The reference adapter pins its original manifest before reading resources.
pub struct ScannedPackage(forge_assets::source_scanned_package::ScannedPackage);

impl ScannedPackage {
    /// Verify the acquired manifest and its resources.
    ///
    /// # Errors
    /// Propagates transport and package validation errors.
    pub fn load(
        manifest: &[u8],
        read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        forge_assets::source_scanned_package::ScannedPackage::load(manifest, revision(), read)
            .map(Self)
    }

    /// Verify the same acquisition with an asynchronous transport.
    ///
    /// # Errors
    /// Propagates transport and package validation errors.
    pub async fn load_async<F, Fut>(manifest: &[u8], read: F) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        forge_assets::source_scanned_package::ScannedPackage::load_async(manifest, revision(), read)
            .await
            .map(Self)
    }
}

impl Deref for ScannedPackage {
    type Target = forge_assets::source_scanned_package::ScannedPackage;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn altered_manifest_is_rejected_before_transport() {
        let mut reads = 0;
        assert!(
            ScannedPackage::load(b"{}", |_, _| {
                reads += 1;
                Ok(Vec::new())
            })
            .is_err()
        );
        assert_eq!(reads, 0);
    }

    #[test]
    #[ignore = "Requires the real standalone scanned acquisition"]
    fn acquired_package_loads_and_corrupt_texture_is_refused() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&manifest, |name, size| {
            let bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
            assert_eq!(bytes.len(), size);
            Ok(bytes)
        })
        .unwrap();
        assert_eq!(
            package.document("geometry").unwrap()["instances"]
                .as_array()
                .unwrap()
                .len(),
            141
        );
        assert_eq!(
            package
                .file("atlases/scan-albedo.mips.rgba8.bin")
                .unwrap()
                .len(),
            22_369_620
        );
        assert!(
            ScannedPackage::load(&manifest, |name, _| {
                let mut bytes = std::fs::read(root.join(name)).map_err(|e| e.to_string())?;
                if name == "atlases/scan-albedo.mips.rgba8.bin" {
                    bytes[0] ^= 1;
                }
                Ok(bytes)
            })
            .is_err()
        );
    }
}
