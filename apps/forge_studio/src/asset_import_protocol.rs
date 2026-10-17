//! Typed user-library imports. Uploads carry bytes, never client filesystem paths.
use forge_assets::governed_import::{
    AssetProvenance, GovernedAssetKind, GovernedImportAsset, GovernedImportCatalog,
};
use forge_core::{AssetId, CommandId, ContentHash};
use serde::{Deserialize, Serialize};

pub const MAX_UPLOAD_BYTES: usize = 256 * 1024 * 1024;
pub const MAX_THUMBNAIL_BYTES: usize = 2 * 1024 * 1024;
pub const MAX_HEADER_BYTES: usize = 16 * 1024 * 1024;
pub const MAX_PACKET_BYTES: usize =
    MAX_UPLOAD_BYTES * 2 + MAX_THUMBNAIL_BYTES + MAX_HEADER_BYTES + 12;
const MAGIC: &[u8; 8] = b"ORAI\x01\0\0\0";

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportUpload {
    /// Original name for format admission only; no filesystem authority.
    pub name: String,
    pub byte_length: u64,
    #[serde(skip)]
    pub bytes: Vec<u8>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ImportSourceKind {
    AuthoredFile,
    Recipe,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportSource {
    pub kind: ImportSourceKind,
    pub upload: ImportUpload,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportCandidate {
    pub command_id: CommandId,
    pub expected_revision: u64,
    pub asset_id: AssetId,
    /// Required to replace an existing asset; never updates saved placement pins.
    pub expected_asset_sha256: Option<ContentHash>,
    /// A new variant names its preserved parent. Reimports retain their lineage.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub derived_from: Option<AssetId>,
    pub name: String,
    pub kind: GovernedAssetKind,
    pub provenance: AssetProvenance,
    /// Preserved UTF-8 licence/provenance evidence for subsequent package export.
    pub license_text: String,
    pub payload: ImportUpload,
    pub authored_changes: bool,
    pub source: Option<ImportSource>,
    pub thumbnail: Option<ImportUpload>,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ImportReadRole {
    Payload,
    Thumbnail,
    Source,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ImportRequest {
    State,
    Prepare {
        candidate: Box<ImportCandidate>,
    },
    Apply {
        command_id: CommandId,
        plan_sha256: ContentHash,
    },
    Undo {
        command_id: CommandId,
        expected_revision: u64,
    },
    Read {
        asset_id: AssetId,
        sha256: ContentHash,
        role: ImportReadRole,
    },
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportState {
    pub revision: u64,
    pub catalog_sha256: Option<ContentHash>,
    pub catalog: Option<GovernedImportCatalog>,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PreparedImport {
    pub command_id: CommandId,
    pub plan_sha256: ContentHash,
    pub base_revision: u64,
    pub previous_asset_sha256: Option<ContentHash>,
    pub asset: GovernedImportAsset,
    pub catalog_sha256: ContentHash,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportReceipt {
    pub command_id: CommandId,
    pub revision: u64,
    pub previous_catalog_sha256: Option<ContentHash>,
    pub catalog_sha256: Option<ContentHash>,
    pub asset_id: Option<AssetId>,
    pub previous_asset_sha256: Option<ContentHash>,
    pub asset_sha256: Option<ContentHash>,
    pub undo: bool,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ImportedFile {
    pub asset_id: AssetId,
    pub sha256: ContentHash,
    pub catalog_sha256: ContentHash,
    pub kind: GovernedAssetKind,
    pub mime_type: String,
    pub upload: ImportUpload,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ImportResponse {
    State { state: ImportState },
    Prepared { plan: Box<PreparedImport> },
    Applied { receipt: ImportReceipt },
    File { file: ImportedFile },
}

/// JSON metadata is bounded independently of the binary media body.
pub fn encode_request(request: &ImportRequest) -> Result<Vec<u8>, String> {
    let uploads = match request {
        ImportRequest::Prepare { candidate } => candidate_uploads(candidate),
        _ => Vec::new(),
    };
    encode(request, &uploads)
}

pub fn decode_request(wire: &[u8]) -> Result<ImportRequest, String> {
    let (mut request, mut body) = decode::<ImportRequest>(wire)?;
    if let ImportRequest::Prepare { candidate } = &mut request {
        take_upload(&mut candidate.payload, &mut body, MAX_UPLOAD_BYTES)?;
        if let Some(source) = &mut candidate.source {
            take_upload(&mut source.upload, &mut body, MAX_UPLOAD_BYTES)?;
        }
        if let Some(thumbnail) = &mut candidate.thumbnail {
            take_upload(thumbnail, &mut body, MAX_THUMBNAIL_BYTES)?;
        }
    }
    if !body.is_empty() {
        return Err("Unexpected import binary tail".into());
    }
    Ok(request)
}

pub fn encode_response(response: &ImportResponse) -> Result<Vec<u8>, String> {
    let uploads = if let ImportResponse::File { file } = response {
        vec![&file.upload]
    } else {
        Vec::new()
    };
    encode(response, &uploads)
}

pub fn decode_response(wire: &[u8]) -> Result<ImportResponse, String> {
    let (mut response, mut body) = decode::<ImportResponse>(wire)?;
    if let ImportResponse::File { file } = &mut response {
        take_upload(&mut file.upload, &mut body, MAX_UPLOAD_BYTES)?;
    }
    if !body.is_empty() {
        return Err("Unexpected import response tail".into());
    }
    Ok(response)
}

fn candidate_uploads(candidate: &ImportCandidate) -> Vec<&ImportUpload> {
    let mut values = vec![&candidate.payload];
    if let Some(source) = &candidate.source {
        values.push(&source.upload);
    }
    if let Some(thumbnail) = &candidate.thumbnail {
        values.push(thumbnail);
    }
    values
}

fn encode(value: &impl Serialize, uploads: &[&ImportUpload]) -> Result<Vec<u8>, String> {
    let header = serde_json::to_vec(value).map_err(|e| e.to_string())?;
    if header.len() > MAX_HEADER_BYTES {
        return Err("Import metadata exceeds its budget".into());
    }
    let mut total = 12 + header.len();
    for upload in uploads {
        if upload.bytes.len() > MAX_UPLOAD_BYTES
            || u64::try_from(upload.bytes.len()).ok() != Some(upload.byte_length)
        {
            return Err("Import upload size differs from its declaration".into());
        }
        total = total
            .checked_add(upload.bytes.len())
            .ok_or("Import size overflow")?;
    }
    if total > MAX_PACKET_BYTES {
        return Err("Import packet exceeds its budget".into());
    }
    let mut wire = Vec::with_capacity(total);
    wire.extend(MAGIC);
    wire.extend(
        u32::try_from(header.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    wire.extend(header);
    for upload in uploads {
        wire.extend(&upload.bytes);
    }
    Ok(wire)
}

fn decode<T: serde::de::DeserializeOwned>(wire: &[u8]) -> Result<(T, &[u8]), String> {
    if wire.len() < 12 || wire.len() > MAX_PACKET_BYTES || &wire[..8] != MAGIC {
        return Err("Invalid import envelope".into());
    }
    let length = usize::try_from(u32::from_le_bytes(
        wire[8..12]
            .try_into()
            .map_err(|_| "Invalid import prefix")?,
    ))
    .map_err(|e| e.to_string())?;
    if length > MAX_HEADER_BYTES || length > wire.len() - 12 {
        return Err("Invalid import metadata length".into());
    }
    let value = serde_json::from_slice(&wire[12..12 + length]).map_err(|e| e.to_string())?;
    Ok((value, &wire[12 + length..]))
}

fn take_upload(upload: &mut ImportUpload, body: &mut &[u8], limit: usize) -> Result<(), String> {
    let length = usize::try_from(upload.byte_length).map_err(|e| e.to_string())?;
    if length == 0 || length > limit || length > body.len() {
        return Err("Invalid import upload length".into());
    }
    upload.bytes = body[..length].to_vec();
    *body = &body[length..];
    Ok(())
}
