//! Captured game fish, separate from the village's immutable prop inventory.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use serde_json::Value;
use std::collections::BTreeMap;

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d";

#[must_use]
pub fn asset_id(ordinal: usize) -> Option<AssetId> {
    (ordinal < 23)
        .then(|| AssetId::deterministic(format!("tidewater:{COMMIT}:game-fish:{ordinal}")))
}

/// Immutable acquisition pin; random source seeds are part of this revision.
///
/// # Panics
/// Panics only if the compiled digest is malformed.
#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned game fish digest is valid")
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Reference {
    file: String,
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    game: Reference,
    material: Reference,
    license: Reference,
    material_source_package_sha256: ContentHash,
    source_validation_sha256: ContentHash,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

/// Verified geometry, records, material closure and attribution; no renderer state.
pub struct GameFishPackage {
    files: BTreeMap<String, Vec<u8>>,
}

impl GameFishPackage {
    /// Verify every payload before publishing a usable package.
    ///
    /// # Errors
    /// Rejects unknown revisions, malformed inventory or any changed payload.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files)
    }

    /// Verify the identical package over an asynchronous transport.
    ///
    /// # Errors
    /// Rejects the same invalid inputs as `Self::load`.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files)
    }

    fn finish(index: &Manifest, files: BTreeMap<String, Vec<u8>>) -> Result<Self, String> {
        for reference in [&index.game, &index.material, &index.license] {
            let bytes = files
                .get(&reference.file)
                .ok_or("Game fish reference absent")?;
            if bytes.len() != reference.bytes || bytes_sha256(bytes) != reference.sha256 {
                return Err("Game fish reference differs".into());
            }
        }
        let game: Value =
            serde_json::from_slice(&files[&index.game.file]).map_err(|error| error.to_string())?;
        let material: Value = serde_json::from_slice(&files[&index.material.file])
            .map_err(|error| error.to_string())?;
        let rows = game["instances"]
            .as_array()
            .ok_or("Game fish inventory absent")?;
        if game["schema"] != "forge.tidewater.game-fish-source"
            || game["version"] != 1
            || game["source_commit"] != COMMIT
            || game["source_gpu_records_identical"] != true
            || rows.len() != 23
            || game["prototypes"].as_array().is_none_or(|p| p.len() != 18)
            || material["schema"] != "forge.tidewater.fish-material-source"
            || material["version"] != 1
            || material["source_commit"] != COMMIT
            || material["specular_mode"] != "intended"
            || !files[&index.license.file].starts_with(b"MIT License")
        {
            return Err("Game fish source inventory differs".into());
        }
        for (ordinal, row) in rows.iter().enumerate() {
            let record = row["record_f32"]
                .as_array()
                .ok_or("Game fish record absent")?;
            if row["source_id"] != format!("tidewater:{COMMIT}:game-fish:{ordinal}")
                || row["parked_catch_slot"] != (ordinal < 18)
                || record.len() != 16
                || record
                    .iter()
                    .any(|v| v.as_f64().is_none_or(|n| !n.is_finite()))
                || record[3].as_f64().is_none_or(|n| n <= 0.0)
            {
                return Err("Game fish instance differs".into());
            }
        }
        Ok(Self { files })
    }

    #[must_use]
    pub fn description(&self) -> &[u8] {
        &self.files["geometry/game-fish.json"]
    }

    #[must_use]
    pub fn material_description(&self) -> &[u8] {
        &self.files["material/fish-material.json"]
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    /// Only the five displayed fish are offered as static authoring assets.
    /// The parked catch slots are runtime templates, not default map placements.
    ///
    /// # Errors
    /// Rejects invalid catalog metadata.
    pub fn catalog(&self) -> Result<crate::AssetCatalog, String> {
        let assets = (18..23)
            .map(|ordinal| {
                Ok(crate::AssetRecord {
                    id: asset_id(ordinal).ok_or("Game fish ordinal out of range")?,
                    logical_name: format!("tidewater:{COMMIT}:game-fish:{ordinal}"),
                    kind: crate::AssetKind::Mesh,
                    source: crate::AssetSource::External {
                        uri: format!("https://github.com/dgreenheck/tidewater/tree/{COMMIT}"),
                        declared_license: Some("MIT".into()),
                    },
                    content_hash: package_hash(),
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let catalog = crate::AssetCatalog {
            schema_version: 1,
            assets,
        };
        catalog.validate_import_metadata()?;
        Ok(catalog)
    }
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 128 * 1024
        || bytes_sha256(manifest) != expected
        || expected.to_string() != PACKAGE
    {
        return Err("Game fish manifest pin or size differs".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|error| error.to_string())?;
    if index.schema != "forge.tidewater.game-fish-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.game.file != "geometry/game-fish.json"
        || index.material.file != "material/fish-material.json"
        || index.license.file != "LICENSE-Tidewater.txt"
        || index.material_source_package_sha256 != crate::tidewater_fish_props::package_hash()
        || index.source_validation_sha256.to_string()
            != "8d80f449971966aa7934b9c6c15991ca139cb970dea563d3cbee44474224e98a"
        || index.qualification
            != "source-captured-package; Rust integration and GPU visual parity pending"
        || index.files.len() != 248
        || index.files.iter().any(|(name, entry)| {
            name.contains(['\\', ':'])
                || name.split('/').any(|p| matches!(p, "" | "." | ".."))
                || entry.bytes == 0
                || entry.bytes > 2 * 1024 * 1024
        })
        || index.files.values().map(|entry| entry.bytes).sum::<usize>() > 20 * 1024 * 1024
    {
        return Err("Game fish manifest contract differs".into());
    }
    Ok(index)
}

fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Game fish payload hash or byte count differs".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::future::Future;
    use std::path::PathBuf;
    use std::task::{Context, Poll, Waker};

    fn fixture() -> PathBuf {
        std::env::var_os("FORGE_TIDEWATER_GAME_FISH_PACKAGE")
            .map(PathBuf::from)
            .expect("Captured fixture must be explicitly supplied")
    }

    #[test]
    fn game_identities_are_distinct_from_village_props() {
        let game: std::collections::BTreeSet<_> = (0..23).map(|n| asset_id(n).unwrap()).collect();
        let village: std::collections::BTreeSet<_> = (0..78)
            .map(|n| crate::tidewater_fish_props::asset_id(n).unwrap())
            .collect();
        assert_eq!(game.len(), 23);
        assert!(game.is_disjoint(&village));
        assert!(asset_id(23).is_none());
    }

    #[test]
    #[ignore = "requires the captured GameFish source package"]
    fn captured_package_loads_and_catalog_excludes_parked_slots() {
        let root = fixture();
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let mut calls = 0;
        let package = GameFishPackage::load(&manifest, package_hash(), |name, limit| {
            calls += 1;
            let bytes = std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
            assert_eq!(bytes.len(), limit);
            Ok(bytes)
        })
        .unwrap();
        assert_eq!(calls, 248);
        let catalog = package.catalog().unwrap();
        assert_eq!(catalog.assets.len(), 5);
        for (asset, ordinal) in catalog.assets.iter().zip(18..23) {
            assert_eq!(Some(asset.id), asset_id(ordinal));
            assert_eq!(asset.content_hash, package_hash());
        }
        assert_eq!(
            package.description(),
            std::fs::read(root.join("geometry/game-fish.json")).unwrap()
        );
        assert!(package.file("material/skin-table-f32.bin").is_some());
    }

    fn ready<T>(future: impl Future<Output = T>) -> T {
        let mut context = Context::from_waker(Waker::noop());
        let mut future = std::pin::pin!(future);
        match future.as_mut().poll(&mut context) {
            Poll::Ready(value) => value,
            Poll::Pending => panic!("Fixture transport unexpectedly yielded"),
        }
    }

    #[test]
    #[ignore = "requires the captured GameFish source package"]
    fn async_transport_preserves_the_same_verified_payloads() {
        let root = fixture();
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let package = ready(GameFishPackage::load_async(
            &manifest,
            package_hash(),
            |name, limit| {
                let result = std::fs::read(root.join(name)).map_err(|error| error.to_string());
                async move {
                    let bytes = result?;
                    assert_eq!(bytes.len(), limit);
                    Ok(bytes)
                }
            },
        ))
        .unwrap();
        assert_eq!(package.catalog().unwrap().assets.len(), 5);
        assert_eq!(
            package.material_description(),
            std::fs::read(root.join("material/fish-material.json")).unwrap()
        );
    }

    #[test]
    #[ignore = "requires the captured GameFish source package"]
    fn changed_surface_payload_is_refused() {
        let root = fixture();
        let manifest = std::fs::read(root.join("package.json")).unwrap();
        let mut changed = false;
        let result = GameFishPackage::load(&manifest, package_hash(), |name, _| {
            let mut bytes = std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
            if name == "material/skin-table-f32.bin" {
                bytes[0] ^= 1;
                changed = true;
            }
            Ok(bytes)
        });
        assert!(changed);
        assert!(result.is_err());
    }

    #[test]
    #[ignore = "requires the captured GameFish source package"]
    fn changed_manifest_does_not_start_payload_acquisition() {
        let root = fixture();
        let mut manifest = std::fs::read(root.join("package.json")).unwrap();
        manifest.push(b' ');
        let mut called = false;
        assert!(
            GameFishPackage::load(&manifest, bytes_sha256(&manifest), |_, _| {
                called = true;
                Err("Unexpected acquisition".into())
            })
            .is_err()
        );
        assert!(!called);
    }
}
