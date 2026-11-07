//! Bounded reading of caller-admitted fish resources, independent of storage.
//!
//! The caller verifies the manifest, source identity, provenance and rights;
//! this reader checks every declared logical resource before publishing bytes.
use crate::source_fish_payload::SourceFishPayload;
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::BTreeMap;

const MAX_RESOURCES: usize = 4096;
const MAX_INDEX_BYTES: usize = 256 * 1024;
const MAX_RESOURCE_BYTES: usize = 64 * 1024 * 1024;
const MAX_TOTAL_BYTES: usize = 128 * 1024 * 1024;

#[derive(Clone, Debug, PartialEq, Eq, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ResourcePin {
    pub bytes: usize,
    pub sha256: ContentHash,
}

/// Resource closure of an index whose bytes were pinned by its caller.
pub struct ResourceIndex {
    pub files: BTreeMap<String, ResourcePin>,
    pub license: String,
}

impl ResourceIndex {
    /// Select the exact runtime resources while retaining the original pinned
    /// index. Only explicitly named JavaScript source files may be omitted;
    /// their original sizes and hashes remain recorded in that index.
    ///
    /// # Errors
    /// Rejects unknown, repeated, unordered or non-JavaScript exclusions, and
    /// refuses to exclude the package license.
    pub fn runtime_resources(
        &self,
        excluded_source_scripts: &[String],
    ) -> Result<BTreeMap<String, ResourcePin>, String> {
        validate(&self.files)?;
        if !self.files.contains_key(&self.license) {
            return Err("Fish license absent from index".into());
        }
        if excluded_source_scripts
            .windows(2)
            .any(|pair| pair[0] >= pair[1])
        {
            return Err("Fish source script exclusions are repeated or unsorted".into());
        }
        for name in excluded_source_scripts {
            if name == &self.license
                || !name
                    .rsplit_once('.')
                    .is_some_and(|(_, extension)| extension.eq_ignore_ascii_case("js"))
                || !self.files.contains_key(name)
            {
                return Err("Invalid fish source script exclusion".into());
            }
        }
        let resources = self
            .files
            .iter()
            .filter(|(name, _)| excluded_source_scripts.binary_search(name).is_err())
            .map(|(name, pin)| (name.clone(), pin.clone()))
            .collect();
        Ok(resources)
    }
}

/// Verify an existing source index and its explicit license reference.
/// This parses only its resource contract; it does not grant distribution rights.
///
/// # Errors
/// Rejects an altered or oversized index, invalid resources or a mismatched license pin.
pub fn resource_index(bytes: &[u8], expected: ContentHash) -> Result<ResourceIndex, String> {
    #[derive(Deserialize)]
    struct Reference {
        file: String,
        bytes: usize,
        sha256: ContentHash,
    }
    #[derive(Deserialize)]
    struct Index {
        files: BTreeMap<String, ResourcePin>,
        license: Reference,
    }
    if bytes.len() > MAX_INDEX_BYTES || bytes_sha256(bytes) != expected {
        return Err("Fish index size or hash differs".into());
    }
    let index: Index = serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
    validate(&index.files)?;
    let license = index
        .files
        .get(&index.license.file)
        .ok_or("Fish license absent from index")?;
    if license.bytes != index.license.bytes || license.sha256 != index.license.sha256 {
        return Err("Fish license reference differs from index".into());
    }
    Ok(ResourceIndex {
        files: index.files,
        license: index.license.file,
    })
}

/// Verified logical bytes; paths are resource names, never filesystem authority.
pub struct PinnedFishPayload {
    files: BTreeMap<String, Vec<u8>>,
}

impl PinnedFishPayload {
    /// Validate the whole declared inventory before the first synchronous read.
    ///
    /// # Errors
    /// Refuses unsafe names, oversized inventories and missing or altered bytes.
    pub fn load(
        pins: &BTreeMap<String, ResourcePin>,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        validate(pins)?;
        let mut files = BTreeMap::new();
        for (name, pin) in pins {
            let bytes = read(name, pin.bytes)?;
            verify(pin, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Ok(Self { files })
    }

    /// Validate the same inventory over an asynchronous, sequential transport.
    ///
    /// # Errors
    /// Refuses the same invalid resources as `Self::load`.
    pub async fn load_async<F, Fut>(
        pins: &BTreeMap<String, ResourcePin>,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        validate(pins)?;
        let mut files = BTreeMap::new();
        for (name, pin) in pins {
            let bytes = read(name.clone(), pin.bytes).await?;
            verify(pin, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Ok(Self { files })
    }

    /// Transfer ownership without duplicating resource bytes.
    #[must_use]
    pub fn into_files(self) -> BTreeMap<String, Vec<u8>> {
        self.files
    }
}

impl SourceFishPayload for PinnedFishPayload {
    fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
}

fn validate(pins: &BTreeMap<String, ResourcePin>) -> Result<(), String> {
    if pins.is_empty() || pins.len() > MAX_RESOURCES {
        return Err("Invalid fish resource count".into());
    }
    let mut total = 0_usize;
    for (name, pin) in pins {
        if name.len() > 256
            || !name.bytes().all(|byte| {
                byte.is_ascii_alphanumeric() || matches!(byte, b'_' | b'-' | b'.' | b'/')
            })
            || name.split('/').any(|part| matches!(part, "" | "." | ".."))
            || pin.bytes == 0
            || pin.bytes > MAX_RESOURCE_BYTES
        {
            return Err("Invalid fish resource name or size".into());
        }
        total = total
            .checked_add(pin.bytes)
            .ok_or("Fish resource byte count overflow")?;
        if total > MAX_TOTAL_BYTES {
            return Err("Fish resource byte budget exceeded".into());
        }
    }
    Ok(())
}

fn verify(pin: &ResourcePin, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != pin.bytes || bytes_sha256(bytes) != pin.sha256 {
        return Err("Fish resource size or hash differs".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        cell::Cell,
        future::{Future, ready},
        task::{Context, Poll, Waker},
    };

    fn complete<T>(future: impl Future<Output = T>) -> T {
        let mut future = std::pin::pin!(future);
        match future
            .as_mut()
            .poll(&mut Context::from_waker(Waker::noop()))
        {
            Poll::Ready(value) => value,
            Poll::Pending => panic!("Fixture transport unexpectedly waited"),
        }
    }

    #[test]
    fn synchronous_and_async_readers_use_the_same_verified_logical_bytes() {
        let bytes = b"verified fish resource".to_vec();
        let pins = BTreeMap::from([(
            "geometry/fish.bin".to_owned(),
            ResourcePin {
                bytes: bytes.len(),
                sha256: bytes_sha256(&bytes),
            },
        )]);
        let direct = PinnedFishPayload::load(&pins, |name, limit| {
            assert_eq!(name, "geometry/fish.bin");
            assert_eq!(limit, bytes.len());
            Ok(bytes.clone())
        })
        .unwrap();
        let asynchronous = complete(PinnedFishPayload::load_async(&pins, |name, limit| {
            assert_eq!(name, "geometry/fish.bin");
            assert_eq!(limit, bytes.len());
            ready(Ok(bytes.clone()))
        }))
        .unwrap();
        assert_eq!(direct.file("geometry/fish.bin"), Some(bytes.as_slice()));
        assert_eq!(direct.into_files(), asynchronous.into_files());
    }

    #[test]
    fn unsafe_inventory_is_rejected_before_io_and_changed_bytes_are_rejected() {
        let bytes = b"fish".to_vec();
        let pin = ResourcePin {
            bytes: bytes.len(),
            sha256: bytes_sha256(&bytes),
        };
        let calls = Cell::new(0);
        let invalid = BTreeMap::from([("../fish.bin".into(), pin.clone())]);
        assert!(
            PinnedFishPayload::load(&invalid, |_, _| {
                calls.set(calls.get() + 1);
                Ok(bytes.clone())
            })
            .is_err()
        );
        assert_eq!(calls.get(), 0);
        let valid = BTreeMap::from([("geometry/fish.bin".into(), pin)]);
        assert!(PinnedFishPayload::load(&valid, |_, _| Ok(b"other".to_vec())).is_err());
        assert!(
            complete(PinnedFishPayload::load_async(&valid, |_, _| ready(Ok(
                b"other".to_vec()
            ))))
            .is_err()
        );
    }

    #[test]
    fn index_requires_the_exact_pin_and_a_declared_license() {
        let data = b"license";
        let pin = bytes_sha256(data);
        let value = serde_json::json!({
            "files": {"LICENSE.txt": {"bytes":data.len(), "sha256":pin}},
            "license": {"file":"LICENSE.txt", "bytes":data.len(), "sha256":pin}
        });
        let encoded = serde_json::to_vec(&value).unwrap();
        let index = resource_index(&encoded, bytes_sha256(&encoded)).unwrap();
        assert_eq!(index.license, "LICENSE.txt");
        assert!(resource_index(&encoded, bytes_sha256(b"other")).is_err());
        let mut wrong = value;
        wrong["license"]["sha256"] = serde_json::json!(bytes_sha256(b"other"));
        let encoded = serde_json::to_vec(&wrong).unwrap();
        assert!(resource_index(&encoded, bytes_sha256(&encoded)).is_err());
    }

    #[test]
    fn runtime_projection_omits_only_explicit_indexed_scripts_and_keeps_license() {
        let pin = ResourcePin {
            bytes: 1,
            sha256: bytes_sha256(b"x"),
        };
        let index = ResourceIndex {
            files: BTreeMap::from([
                ("LICENSE.txt".into(), pin.clone()),
                ("geometry/fish.bin".into(), pin.clone()),
                ("geometry/source.js".into(), pin.clone()),
            ]),
            license: "LICENSE.txt".into(),
        };
        let excluded = vec!["geometry/source.js".to_owned()];
        let runtime = index.runtime_resources(&excluded).unwrap();
        assert_eq!(runtime.len(), 2);
        assert!(runtime.contains_key("LICENSE.txt"));
        assert!(runtime.contains_key("geometry/fish.bin"));
        assert!(!runtime.contains_key("geometry/source.js"));
        for invalid in [
            vec!["geometry/fish.bin".into()],
            vec!["geometry/unknown.js".into()],
            vec!["geometry/source.js".into(), "geometry/source.js".into()],
        ] {
            assert!(index.runtime_resources(&invalid).is_err());
        }
        let mut licensed_script = index;
        licensed_script.license = "geometry/source.js".into();
        assert!(licensed_script.runtime_resources(&excluded).is_err());
    }
}
