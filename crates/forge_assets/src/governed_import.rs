//! Versioned asset admission metadata, independent of import tools and rendering.
//!
//! These declarations do not grant rights or qualify a model, texture or sound.
//! File readers belong to adapters; this module never opens a path or URL.

use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

pub const GOVERNED_IMPORT_SCHEMA: &str = "forge.governed-import-catalog";
pub const GOVERNED_IMPORT_VERSION: u32 = 1;
pub const MAX_IMPORT_ASSETS: usize = 4_096;
pub const MAX_IMPORT_FILE_BYTES: u64 = 256 * 1024 * 1024;
pub const MAX_IMPORT_TOTAL_BYTES: u64 = 16 * 1024 * 1024 * 1024;
pub const MAX_IMPORT_THUMBNAIL_BYTES: u64 = 2 * 1024 * 1024;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum GovernedAssetKind {
    Mesh,
    Texture,
    Audio,
}

/// A content pin, not permission to resolve a client-supplied filesystem path.
/// Internal paths use portable ASCII; UI text and the adapter's root remain Unicode.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GovernedAssetFile {
    pub path: String,
    pub sha256: ContentHash,
    pub bytes: u64,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum RedistributionRights {
    Allowed,
    Forbidden,
    Unknown,
}

/// A declaration with its evidence; validation does not establish legal ownership.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetRights {
    pub local_use: bool,
    pub redistribution: RedistributionRights,
    pub redistribution_evidence: Option<String>,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetProvenance {
    pub source: String,
    pub author: String,
    /// Declared SPDX identifier/expression or `LicenseRef`; no implicit rights grant.
    pub license: String,
    pub evidence: String,
    /// Gregorian acquisition date in YYYY-MM-DD form.
    pub acquired_on: String,
    pub rights: AssetRights,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetTransformation {
    pub tool: String,
    pub version: String,
    pub parameters_sha256: ContentHash,
}

/// Retouches require a preserved source or recipe, never only a rebuildable cache.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum DurableAssetSource {
    AuthoredFile { file: GovernedAssetFile },
    Recipe { file: GovernedAssetFile },
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GovernedImportAsset {
    pub id: AssetId,
    pub name: String,
    pub kind: GovernedAssetKind,
    pub payload: GovernedAssetFile,
    pub provenance: AssetProvenance,
    /// The parent is another explicit identity in this catalogue, not a filename.
    pub derived_from: Option<AssetId>,
    pub transformations: Vec<AssetTransformation>,
    pub authored_changes: bool,
    pub durable_source: Option<DurableAssetSource>,
    pub thumbnail: Option<GovernedAssetFile>,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GovernedImportCatalog {
    pub schema: String,
    pub schema_version: u32,
    pub assets: Vec<GovernedImportAsset>,
}

#[derive(Debug, Error)]
pub enum GovernedImportError {
    #[error("asset catalogue {field}: {reason}")]
    Catalog { field: &'static str, reason: String },
    #[error("asset {asset_id}, {field}: {reason}")]
    Asset {
        asset_id: AssetId,
        field: &'static str,
        reason: String,
    },
    #[error("asset {asset_id}, file `{path}`: {reason}")]
    Payload {
        asset_id: AssetId,
        path: String,
        reason: String,
    },
}

impl GovernedImportCatalog {
    pub fn validate_for_local_use(&self) -> Result<(), GovernedImportError> {
        if self.schema != GOVERNED_IMPORT_SCHEMA || self.schema_version != GOVERNED_IMPORT_VERSION {
            return Err(catalog_error("schema", "unsupported schema or version"));
        }
        if self.assets.is_empty() || self.assets.len() > MAX_IMPORT_ASSETS {
            return Err(catalog_error("assets", "expected 1 to 4096 assets"));
        }
        let mut identities = BTreeMap::new();
        for asset in &self.assets {
            if asset.id.as_uuid().is_nil() || identities.insert(asset.id, asset).is_some() {
                return Err(asset_error(asset.id, "id", "nil or duplicate identity"));
            }
        }
        validate_lineage(&identities)?;
        let mut files = BTreeMap::new();
        let mut total_bytes = 0_u64;
        for asset in &self.assets {
            validate_asset(asset)?;
            for (file, fields) in declared_files(asset) {
                validate_file(asset.id, file, fields)?;
                if register_file(&mut files, asset.id, file, fields.path)? {
                    total_bytes = total_bytes
                        .checked_add(file.bytes)
                        .ok_or_else(|| catalog_error("bytes", "file byte accounting overflow"))?;
                    if total_bytes > MAX_IMPORT_TOTAL_BYTES {
                        return Err(catalog_error("bytes", "catalogue exceeds 16 GiB of files"));
                    }
                }
            }
        }
        Ok(())
    }

    pub fn validate_for_publication(&self) -> Result<(), GovernedImportError> {
        self.validate_for_local_use()?;
        // Every ancestor is a catalogue entry and is checked too. A derived output
        // cannot turn a parent's forbidden/unknown declaration into permission.
        for asset in &self.assets {
            let rights = &asset.provenance.rights;
            if rights.redistribution != RedistributionRights::Allowed {
                return Err(asset_error(
                    asset.id,
                    "redistribution",
                    "not explicitly allowed",
                ));
            }
            if !rights
                .redistribution_evidence
                .as_deref()
                .is_some_and(|value| valid_text(value, 2048))
            {
                return Err(asset_error(
                    asset.id,
                    "redistribution_evidence",
                    "missing or invalid evidence",
                ));
            }
        }
        Ok(())
    }

    /// Verify declared payload, thumbnail and durable-source bytes once per path.
    /// The adapter must confine reads and enforce the declared size before allocation.
    pub fn verify_payloads(
        &self,
        mut read: impl FnMut(&GovernedAssetFile) -> Result<Vec<u8>, String>,
    ) -> Result<(), GovernedImportError> {
        self.validate_for_local_use()?;
        let mut checked = BTreeSet::new();
        for asset in &self.assets {
            for (file, _) in declared_files(asset) {
                if !checked.insert(&file.path) {
                    continue;
                }
                let failure = |reason| GovernedImportError::Payload {
                    asset_id: asset.id,
                    path: file.path.clone(),
                    reason,
                };
                let bytes = read(file).map_err(failure)?;
                if u64::try_from(bytes.len()).ok() != Some(file.bytes) {
                    return Err(failure("size differs from the declared content pin".into()));
                }
                if bytes_sha256(&bytes) != file.sha256 {
                    return Err(failure(
                        "SHA-256 differs from the declared content pin".into(),
                    ));
                }
            }
        }
        Ok(())
    }
}

fn catalog_error(field: &'static str, reason: impl Into<String>) -> GovernedImportError {
    GovernedImportError::Catalog {
        field,
        reason: reason.into(),
    }
}

fn asset_error(
    asset_id: AssetId,
    field: &'static str,
    reason: impl Into<String>,
) -> GovernedImportError {
    GovernedImportError::Asset {
        asset_id,
        field,
        reason: reason.into(),
    }
}

fn valid_text(value: &str, max_bytes: usize) -> bool {
    !value.trim().is_empty()
        && value.len() <= max_bytes
        && !value.chars().any(|ch| ch.is_control() || ch == '\u{fffd}')
}

fn validate_asset(asset: &GovernedImportAsset) -> Result<(), GovernedImportError> {
    let provenance = &asset.provenance;
    for (field, value, limit) in [
        ("name", asset.name.as_str(), 256),
        ("source", provenance.source.as_str(), 2048),
        ("author", provenance.author.as_str(), 256),
        ("license", provenance.license.as_str(), 256),
        ("evidence", provenance.evidence.as_str(), 2048),
    ] {
        if !valid_text(value, limit) {
            return Err(asset_error(
                asset.id,
                field,
                "empty, invalid or oversized text",
            ));
        }
    }
    if !valid_date(&provenance.acquired_on) {
        return Err(asset_error(
            asset.id,
            "acquired_on",
            "expected a Gregorian YYYY-MM-DD date",
        ));
    }
    if !provenance.rights.local_use {
        return Err(asset_error(asset.id, "local_use", "not explicitly allowed"));
    }
    if asset.authored_changes && asset.durable_source.is_none() {
        return Err(asset_error(
            asset.id,
            "durable_source",
            "retouches require an authored file or recipe",
        ));
    }
    if asset.transformations.len() > 64
        || (asset.derived_from.is_some() && asset.transformations.is_empty())
        || asset.transformations.iter().any(|step| {
            !valid_text(&step.tool, 256)
                || !valid_text(&step.version, 128)
                || step.parameters_sha256 == ContentHash::from_bytes([0; 32])
        })
    {
        return Err(asset_error(
            asset.id,
            "transformations",
            "missing, invalid or unbounded transformation records",
        ));
    }
    Ok(())
}

fn valid_date(value: &str) -> bool {
    let bytes = value.as_bytes();
    if bytes.len() != 10
        || bytes[4] != b'-'
        || bytes[7] != b'-'
        || bytes
            .iter()
            .enumerate()
            .any(|(index, byte)| index != 4 && index != 7 && !byte.is_ascii_digit())
    {
        return false;
    }
    let year = u32::from(bytes[0] - b'0') * 1000
        + u32::from(bytes[1] - b'0') * 100
        + u32::from(bytes[2] - b'0') * 10
        + u32::from(bytes[3] - b'0');
    let month = (bytes[5] - b'0') * 10 + bytes[6] - b'0';
    let day = (bytes[8] - b'0') * 10 + bytes[9] - b'0';
    let leap = year.is_multiple_of(4) && (!year.is_multiple_of(100) || year.is_multiple_of(400));
    let days = match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 => {
            if leap {
                29
            } else {
                28
            }
        }
        _ => return false,
    };
    year > 0 && day > 0 && day <= days
}

fn validate_lineage(
    assets: &BTreeMap<AssetId, &GovernedImportAsset>,
) -> Result<(), GovernedImportError> {
    let mut completed = BTreeSet::new();
    for asset in assets.values() {
        let mut chain = BTreeSet::new();
        let mut cursor = Some(asset.id);
        while let Some(id) = cursor {
            if completed.contains(&id) {
                break;
            }
            if !chain.insert(id) {
                return Err(asset_error(
                    asset.id,
                    "derived_from",
                    "cyclic asset lineage",
                ));
            }
            let parent = assets.get(&id).ok_or_else(|| {
                asset_error(asset.id, "derived_from", format!("missing ancestor {id}"))
            })?;
            cursor = parent.derived_from;
        }
        completed.extend(chain);
    }
    Ok(())
}

#[derive(Clone, Copy)]
struct FileFields {
    path: &'static str,
    sha256: &'static str,
    bytes: &'static str,
    limit: u64,
}

fn declared_files(asset: &GovernedImportAsset) -> Vec<(&GovernedAssetFile, FileFields)> {
    let mut files = vec![(
        &asset.payload,
        FileFields {
            path: "payload.path",
            sha256: "payload.sha256",
            bytes: "payload.bytes",
            limit: MAX_IMPORT_FILE_BYTES,
        },
    )];
    if let Some(file) = &asset.thumbnail {
        files.push((
            file,
            FileFields {
                path: "thumbnail.path",
                sha256: "thumbnail.sha256",
                bytes: "thumbnail.bytes",
                limit: MAX_IMPORT_THUMBNAIL_BYTES,
            },
        ));
    }
    if let Some(source) = &asset.durable_source {
        let (DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file }) =
            source;
        files.push((
            file,
            FileFields {
                path: "durable_source.path",
                sha256: "durable_source.sha256",
                bytes: "durable_source.bytes",
                limit: MAX_IMPORT_FILE_BYTES,
            },
        ));
    }
    files
}

fn validate_file(
    id: AssetId,
    file: &GovernedAssetFile,
    fields: FileFields,
) -> Result<(), GovernedImportError> {
    if !portable_path(&file.path) {
        return Err(asset_error(
            id,
            fields.path,
            "expected a portable ASCII relative file path",
        ));
    }
    if file.sha256 == ContentHash::from_bytes([0; 32]) {
        return Err(asset_error(id, fields.sha256, "empty SHA-256 pin"));
    }
    if file.bytes == 0 || file.bytes > fields.limit {
        return Err(asset_error(id, fields.bytes, "zero or oversized file"));
    }
    Ok(())
}

fn portable_path(path: &str) -> bool {
    // Unicode case folding differs across filesystems (for example σ and ς on
    // Windows). V1 confines internal paths to ASCII, without changing UI names.
    if !path.is_ascii()
        || !valid_text(path, 240)
        || path.chars().any(|ch| "\\:<>\"|?*".contains(ch))
    {
        return false;
    }
    path.split('/').all(|part| {
        !part.is_empty()
            && part != "."
            && part != ".."
            && !part.ends_with(['.', ' '])
            && !reserved_filename(part)
    })
}

fn reserved_filename(part: &str) -> bool {
    let stem = part.split('.').next().unwrap_or_default().to_uppercase();
    matches!(
        stem.as_str(),
        "CON" | "PRN" | "AUX" | "NUL" | "CONIN$" | "CONOUT$"
    ) || ["COM", "LPT"].iter().any(|prefix| {
        stem.strip_prefix(prefix).is_some_and(|suffix| {
            matches!(
                suffix,
                "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" | "¹" | "²" | "³"
            )
        })
    })
}

fn register_file<'a>(
    files: &mut BTreeMap<String, &'a GovernedAssetFile>,
    id: AssetId,
    file: &'a GovernedAssetFile,
    field: &'static str,
) -> Result<bool, GovernedImportError> {
    let key = file.path.to_lowercase();
    if let Some(previous) = files.get(&key) {
        if *previous != file {
            return Err(asset_error(
                id,
                field,
                "conflicting file pins or portable path alias",
            ));
        }
        return Ok(false);
    }
    let mut parent = key.as_str();
    while let Some((prefix, _)) = parent.rsplit_once('/') {
        if files.contains_key(prefix) {
            return Err(asset_error(
                id,
                field,
                "parent path is already a declared file",
            ));
        }
        parent = prefix;
    }
    let descendant = format!("{key}/");
    if files
        .range(descendant.clone()..)
        .next()
        .is_some_and(|(path, _)| path.starts_with(&descendant))
    {
        return Err(asset_error(
            id,
            field,
            "file path already contains a declared descendant",
        ));
    }
    files.insert(key, file);
    Ok(true)
}
