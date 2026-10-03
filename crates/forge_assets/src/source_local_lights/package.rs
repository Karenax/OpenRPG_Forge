//! Portable, immutable light acquisitions with a caller-selected manifest pin.
//! The acquisition receipt is retained as provenance; it grants no runtime or
//! visual qualification. Reads are supplied by a native or browser adapter.
use super::{SourceLightSet, SourceLocalLights, SourceLocalLightsError};
use crate::source_geometry::AssetFileReference;
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Deserializer, de::MapAccess, de::Visitor};
use serde_json::Value;
use std::{
    collections::{BTreeMap, BTreeSet},
    fmt,
    future::Future,
    marker::PhantomData,
};

const MAX_MANIFEST_BYTES: usize = 2 * 1024 * 1024;
const MAX_MEMBER_BYTES: usize = 32 * 1024 * 1024;
const MAX_PACKAGE_BYTES: usize = 256 * 1024 * 1024;
const MAX_MEMBERS: usize = 4096;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    descriptor: AssetFileReference,
    acquisition: AssetFileReference,
    license: AssetFileReference,
    #[serde(deserialize_with = "unique_map")]
    sources: BTreeMap<String, AssetFileReference>,
    #[serde(deserialize_with = "unique_map")]
    files: BTreeMap<String, Entry>,
    status: String,
    renderer_parity: bool,
    studio_qualified: bool,
}

// Producer-specific receipt details stay uninterpreted provenance. The required
// envelope is checked against the descriptor and the enclosing file references.
#[derive(Deserialize)]
#[allow(clippy::struct_excessive_bools)]
struct Acquisition {
    schema: String,
    version: u32,
    source_commit: String,
    capture_mode: String,
    gpu_initialized: bool,
    browser_required: bool,
    descriptor: AssetFileReference,
    emitters: usize,
    #[serde(deserialize_with = "unique_map")]
    registration_sets: BTreeMap<String, SourceLightSet>,
    full_emitter_inventory: bool,
    phase_reacquisition_reproducible: bool,
    full_application_executed: bool,
    renderer_parity: bool,
    studio_qualified: bool,
}

/// A verified source package, separate from its placement and renderer state.
/// The asset catalog chooses an `AssetId` outside this reader; its binding must
/// retain both `receipt_hash()` and `descriptor().content_hash()`.
pub struct SourceLocalLightsPackage {
    index: Manifest,
    files: BTreeMap<String, Vec<u8>>,
    descriptor: SourceLocalLights,
    acquisition: Value,
    receipt_hash: ContentHash,
    source_application_executed_claim: bool,
}

impl SourceLocalLightsPackage {
    /// Validate bounds, portable paths and manifest closure before any reads.
    /// The transport must bound its allocation/read to the supplied byte count;
    /// any returned response with a different size or hash is refused.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, SourceLocalLightsError> {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes).map_err(SourceLocalLightsError::Package)?;
            verify_member(name, entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(index, files, expected)
    }

    /// Same contract for browser transport; the reader creates no I/O runtime.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, SourceLocalLightsError>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes)
                .await
                .map_err(SourceLocalLightsError::Package)?;
            verify_member(name, entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(index, files, expected)
    }

    fn finish(
        index: Manifest,
        files: BTreeMap<String, Vec<u8>>,
        expected: ContentHash,
    ) -> Result<Self, SourceLocalLightsError> {
        let descriptor =
            SourceLocalLights::load(&files[&index.descriptor.file], index.descriptor.sha256)?;
        let document = descriptor.document();
        if document.source_commit != index.source_commit
            || document.source_inputs.len() != index.sources.len()
            || document.renderer_parity != index.renderer_parity
            || document.studio_qualified != index.studio_qualified
        {
            return invalid("descriptor provenance differs from its manifest");
        }
        for input in &document.source_inputs {
            let Some(reference) = index.sources.get(&input.path) else {
                return invalid("descriptor has an unclaimed original source");
            };
            if input.bytes != reference.bytes || input.sha256 != reference.sha256 {
                return invalid("descriptor original-source reference differs");
            }
        }
        descriptor.verify_source_inputs(|path| {
            index
                .sources
                .get(path)
                .and_then(|reference| files.get(&reference.file).map(Vec::as_slice))
        })?;
        let acquisition: Value = serde_json::from_slice(&files[&index.acquisition.file])?;
        // Decode from bytes, avoiding a lossy intermediate Value conversion.
        let envelope: Acquisition = serde_json::from_slice(&files[&index.acquisition.file])?;
        validate_acquisition(&envelope, &index, &descriptor)?;
        let source_application_executed_claim = envelope.full_application_executed;
        let license = std::str::from_utf8(&files[&index.license.file])
            .map_err(|_| SourceLocalLightsError::Package("license is not UTF-8".into()))?;
        if license.trim().is_empty() || license.contains('\0') {
            return invalid("original license text is empty or contains NUL");
        }
        Ok(Self {
            index,
            files,
            descriptor,
            acquisition,
            receipt_hash: expected,
            source_application_executed_claim,
        })
    }

    #[must_use]
    pub fn descriptor(&self) -> &SourceLocalLights {
        &self.descriptor
    }

    #[must_use]
    pub const fn receipt_hash(&self) -> ContentHash {
        self.receipt_hash
    }

    #[must_use]
    pub fn acquisition(&self) -> &Value {
        &self.acquisition
    }

    /// Original verified receipt bytes for typed producer observations. Decode
    /// these directly when duplicate fields must not be lost through Value.
    #[must_use]
    pub fn acquisition_bytes(&self) -> &[u8] {
        &self.files[&self.index.acquisition.file]
    }

    /// Source-side receipt claim only. Running an upstream application still
    /// does not attest a Forge runtime import, scene draw or visual qualification.
    #[must_use]
    pub const fn source_application_executed_claim(&self) -> bool {
        self.source_application_executed_claim
    }

    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }

    #[must_use]
    pub fn source_file(&self, original_path: &str) -> Option<&[u8]> {
        self.index
            .sources
            .get(original_path)
            .and_then(|reference| self.file(&reference.file))
    }

    #[must_use]
    pub fn license(&self) -> &[u8] {
        &self.files[&self.index.license.file]
    }

    #[must_use]
    pub fn file_count(&self) -> usize {
        self.files.len()
    }
}

fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, SourceLocalLightsError> {
    if manifest.len() > MAX_MANIFEST_BYTES || bytes_sha256(manifest) != expected {
        return Err(SourceLocalLightsError::PackagePinMismatch);
    }
    let index: Manifest = serde_json::from_slice(manifest)?;
    if index.schema != "forge.source-local-lights.package"
        || index.version != 1
        || index.source_commit.len() != 40
        || !index.source_commit.bytes().all(|c| c.is_ascii_hexdigit())
        || index.status != "source-local-light-acquisition-not-runtime-import"
        || index.renderer_parity
        || index.studio_qualified
        || index.files.is_empty()
        || index.files.len() > MAX_MEMBERS
        || index.sources.is_empty()
        || index.sources.len() > MAX_MEMBERS
    {
        return invalid("package schema, bounds or acquisition-only status differs");
    }
    validate_paths(index.files.keys())?;
    validate_paths(index.sources.keys())?;
    let mut total = 0_usize;
    for entry in index.files.values() {
        total = total
            .checked_add(entry.bytes)
            .ok_or_else(|| SourceLocalLightsError::Package("package byte count overflow".into()))?;
        if entry.bytes == 0 || entry.bytes > MAX_MEMBER_BYTES || total > MAX_PACKAGE_BYTES {
            return invalid("package member or aggregate byte limit");
        }
    }
    if index.descriptor.bytes > super::MAX_BYTES || index.acquisition.bytes > MAX_MANIFEST_BYTES {
        return invalid("descriptor or acquisition byte limit");
    }
    let mut required = BTreeSet::new();
    for reference in [&index.descriptor, &index.acquisition, &index.license] {
        verify_reference(reference, &index.files)?;
        if !required.insert(reference.file.clone()) {
            return invalid("descriptor, receipt and license alias each other");
        }
    }
    let mut source_members = BTreeSet::new();
    for reference in index.sources.values() {
        verify_reference(reference, &index.files)?;
        if !source_members.insert(reference.file.clone())
            || reference.file == index.descriptor.file
            || reference.file == index.acquisition.file
        {
            return invalid("original source members alias one another or metadata");
        }
        required.insert(reference.file.clone());
    }
    if !source_members.contains(&index.license.file)
        || required != index.files.keys().cloned().collect()
    {
        return invalid("license is not original or package payload closure differs");
    }
    Ok(index)
}

fn verify_reference(
    reference: &AssetFileReference,
    files: &BTreeMap<String, Entry>,
) -> Result<(), SourceLocalLightsError> {
    let Some(entry) = files.get(&reference.file) else {
        return invalid("package reference is not indexed");
    };
    if reference.bytes != entry.bytes || reference.sha256 != entry.sha256 {
        return invalid("package reference differs from the index");
    }
    Ok(())
}

fn verify_member(name: &str, entry: &Entry, bytes: &[u8]) -> Result<(), SourceLocalLightsError> {
    AssetFileReference {
        file: name.into(),
        bytes: entry.bytes,
        sha256: entry.sha256,
    }
    .verify(bytes)
    .map(|_| ())
    .map_err(SourceLocalLightsError::Package)
}

fn validate_acquisition(
    receipt: &Acquisition,
    index: &Manifest,
    descriptor: &SourceLocalLights,
) -> Result<(), SourceLocalLightsError> {
    let document = descriptor.document();
    let sets_match = receipt.registration_sets.len() == document.sets.len()
        && document.sets.iter().all(|(name, set)| {
            receipt.registration_sets.get(name).is_some_and(|captured| {
                captured.start_order == set.start_order && captured.count == set.count
            })
        });
    if receipt.schema != "forge.source-local-lights.factory-acquisition"
        || receipt.version != 1
        || receipt.source_commit != document.source_commit
        || receipt.capture_mode != document.capture_mode
        || receipt.gpu_initialized != document.gpu_initialized
        || receipt.browser_required != document.browser_required
        || receipt.descriptor.file != index.descriptor.file
        || receipt.descriptor.bytes != index.descriptor.bytes
        || receipt.descriptor.sha256 != index.descriptor.sha256
        || receipt.emitters != document.lights.len()
        || !sets_match
        || receipt.full_emitter_inventory != document.full_application_inventory
        || receipt.phase_reacquisition_reproducible != document.phase_reacquisition_reproducible
        || receipt.renderer_parity != index.renderer_parity
        || receipt.studio_qualified != index.studio_qualified
    {
        return invalid("acquisition envelope contradicts the pinned descriptor");
    }
    Ok(())
}

fn validate_paths<'a>(
    paths: impl Iterator<Item = &'a String>,
) -> Result<(), SourceLocalLightsError> {
    let mut folded = BTreeSet::new();
    for path in paths {
        if !super::source_path(path)
            || !folded.insert(path.to_uppercase())
            || path
                .bytes()
                .any(|c| matches!(c, b'<' | b'>' | b'"' | b'|' | b'?' | b'*'))
            || path.split('/').any(|part| {
                let stem = part
                    .split('.')
                    .next()
                    .unwrap_or_default()
                    .to_ascii_uppercase();
                part.ends_with(['.', ' '])
                    || matches!(stem.as_str(), "CON" | "PRN" | "AUX" | "NUL")
                    || stem
                        .strip_prefix("COM")
                        .or_else(|| stem.strip_prefix("LPT"))
                        .is_some_and(|suffix| {
                            matches!(
                                suffix,
                                "1" | "2"
                                    | "3"
                                    | "4"
                                    | "5"
                                    | "6"
                                    | "7"
                                    | "8"
                                    | "9"
                                    | "¹"
                                    | "²"
                                    | "³"
                            )
                        })
            })
        {
            return invalid("package path is not portable or aliases another member");
        }
    }
    Ok(())
}

// BTreeMap's default deserializer overwrites duplicate JSON keys. A pinned
// manifest still needs one unambiguous reference for every original/member.
fn unique_map<'de, D, T>(deserializer: D) -> Result<BTreeMap<String, T>, D::Error>
where
    D: Deserializer<'de>,
    T: Deserialize<'de>,
{
    struct Unique<T>(PhantomData<T>);
    impl<'de, T: Deserialize<'de>> Visitor<'de> for Unique<T> {
        type Value = BTreeMap<String, T>;
        fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
            formatter.write_str("a map with unique string keys")
        }
        fn visit_map<A: MapAccess<'de>>(self, mut entries: A) -> Result<Self::Value, A::Error> {
            let mut values = BTreeMap::new();
            while let Some((key, value)) = entries.next_entry::<String, T>()? {
                if values.insert(key, value).is_some() {
                    return Err(serde::de::Error::custom("duplicate package map key"));
                }
            }
            Ok(values)
        }
    }
    deserializer.deserialize_map(Unique(PhantomData))
}

fn invalid<T>(reason: &str) -> Result<T, SourceLocalLightsError> {
    Err(SourceLocalLightsError::Package(reason.into()))
}

#[cfg(test)]
mod tests;
