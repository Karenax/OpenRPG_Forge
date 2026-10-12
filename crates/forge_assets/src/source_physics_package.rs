//! Binary physics evidence transport. Expected pins come from the governed
//! library revision, never from an untrusted envelope alone.
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

const MAGIC: &[u8; 8] = b"ORPP\x01\0\0\0";
const MAX_HEADER: usize = 1024 * 1024;
const MAX_SECTION: usize = 64 * 1024 * 1024;
pub const MAX_PACKAGE_BYTES: usize = 12 + MAX_HEADER + 2 * MAX_SECTION;

pub struct SourcePhysicsPackage {
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub manifest: Vec<u8>,
    pub candidates: BTreeMap<String, Vec<u8>>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Section {
    path: String,
    byte_length: usize,
    sha256: ContentHash,
}
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct Header {
    carrier_sha256: ContentHash,
    manifest_sha256: ContentHash,
    manifest_length: usize,
    candidates: Vec<Section>,
}

fn relative(path: &str) -> bool {
    !path.is_empty()
        && !path.starts_with('/')
        && !path.contains(['\\', ':'])
        && path
            .split('/')
            .all(|part| !part.is_empty() && part != "." && part != "..")
}

impl SourcePhysicsPackage {
    /// Verify the complete caller-held package pin as well as its inner pins.
    /// Identical carrier/manifest pins do not identify candidate buffers alone.
    pub fn decode_pinned(
        wire: &[u8],
        carrier: ContentHash,
        manifest: ContentHash,
        package: ContentHash,
    ) -> Result<Self, String> {
        if wire.len() > MAX_PACKAGE_BYTES || bytes_sha256(wire) != package {
            return Err("physics package SHA-256 or budget differs".into());
        }
        Self::decode(wire, carrier, manifest)
    }

    /// Encode original bytes without JSON expansion of the binary buffers.
    pub fn encode(&self) -> Result<Vec<u8>, String> {
        if self.manifest.is_empty()
            || self.manifest.len() > MAX_SECTION
            || bytes_sha256(&self.manifest) != self.manifest_sha256
            || self.candidates.len() > 4096
        {
            return Err("physics package manifest or budget differs".into());
        }
        let mut total = 0usize;
        let mut candidates = Vec::new();
        for (path, bytes) in &self.candidates {
            total = total
                .checked_add(bytes.len())
                .ok_or("physics package overflow")?;
            if !relative(path) || bytes.is_empty() || total > MAX_SECTION {
                return Err("physics package candidate path or budget differs".into());
            }
            candidates.push(Section {
                path: path.clone(),
                byte_length: bytes.len(),
                sha256: bytes_sha256(bytes),
            });
        }
        let header = serde_json::to_vec(&Header {
            carrier_sha256: self.carrier_sha256,
            manifest_sha256: self.manifest_sha256,
            manifest_length: self.manifest.len(),
            candidates,
        })
        .map_err(|e| e.to_string())?;
        if header.len() > MAX_HEADER {
            return Err("physics package header budget exceeded".into());
        }
        let mut wire = Vec::with_capacity(12 + header.len() + self.manifest.len() + total);
        wire.extend_from_slice(MAGIC);
        wire.extend_from_slice(
            &u32::try_from(header.len())
                .map_err(|_| "physics header length")?
                .to_le_bytes(),
        );
        wire.extend(header);
        wire.extend_from_slice(&self.manifest);
        for bytes in self.candidates.values() {
            wire.extend_from_slice(bytes);
        }
        Ok(wire)
    }

    /// Verify caller-held model and manifest pins before returning any payload.
    /// Semantic source/component validation belongs to `source_physics_transport`.
    pub fn decode(
        wire: &[u8],
        carrier: ContentHash,
        manifest: ContentHash,
    ) -> Result<Self, String> {
        if wire.len() < 12 || wire.len() > MAX_PACKAGE_BYTES || &wire[..8] != MAGIC {
            return Err("physics package version or budget differs".into());
        }
        let header_length = usize::try_from(u32::from_le_bytes(
            wire[8..12].try_into().map_err(|_| "physics header")?,
        ))
        .map_err(|_| "physics header length")?;
        if header_length == 0 || header_length > MAX_HEADER || wire.len() < 12 + header_length {
            return Err("physics package header truncated".into());
        }
        let header: Header =
            serde_json::from_slice(&wire[12..12 + header_length]).map_err(|e| e.to_string())?;
        if header.carrier_sha256 != carrier
            || header.manifest_sha256 != manifest
            || header.manifest_length == 0
            || header.manifest_length > MAX_SECTION
            || header.candidates.len() > 4096
        {
            return Err("physics package pins or section budget differs".into());
        }
        let mut offset = 12 + header_length;
        let manifest_bytes = take(wire, &mut offset, header.manifest_length, manifest)?.to_vec();
        let mut candidates = BTreeMap::new();
        let mut total = 0usize;
        for section in header.candidates {
            total = total
                .checked_add(section.byte_length)
                .ok_or("physics section overflow")?;
            if !relative(&section.path)
                || section.byte_length == 0
                || total > MAX_SECTION
                || candidates.contains_key(&section.path)
            {
                return Err("physics package candidate invalid or duplicated".into());
            }
            candidates.insert(
                section.path,
                take(wire, &mut offset, section.byte_length, section.sha256)?.to_vec(),
            );
        }
        if offset != wire.len() {
            return Err("physics package trailing bytes".into());
        }
        Ok(Self {
            carrier_sha256: carrier,
            manifest_sha256: manifest,
            manifest: manifest_bytes,
            candidates,
        })
    }
}

fn take<'a>(
    wire: &'a [u8],
    offset: &mut usize,
    length: usize,
    hash: ContentHash,
) -> Result<&'a [u8], String> {
    let end = offset
        .checked_add(length)
        .ok_or("physics section overflow")?;
    let bytes = wire.get(*offset..end).ok_or("physics section truncated")?;
    if bytes_sha256(bytes) != hash {
        return Err("physics section SHA-256 differs".into());
    }
    *offset = end;
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn full_package_pin_distinguishes_candidates_with_same_carrier_and_manifest() {
        let carrier = bytes_sha256(b"carrier");
        let manifest = b"{\"rows\":[]}".to_vec();
        let manifest_hash = bytes_sha256(&manifest);
        let package = |candidate: &[u8]| SourcePhysicsPackage {
            carrier_sha256: carrier,
            manifest_sha256: manifest_hash,
            manifest: manifest.clone(),
            candidates: BTreeMap::from([("candidate.bin".into(), candidate.to_vec())]),
        };
        let original = package(b"first collider").encode().unwrap();
        let replacement = package(b"other collider").encode().unwrap();
        let expected = bytes_sha256(&original);
        assert!(SourcePhysicsPackage::decode(&replacement, carrier, manifest_hash).is_ok());
        let decoded =
            SourcePhysicsPackage::decode_pinned(&original, carrier, manifest_hash, expected)
                .unwrap();
        assert_eq!(decoded.candidates["candidate.bin"], b"first collider");
        assert!(
            SourcePhysicsPackage::decode_pinned(&replacement, carrier, manifest_hash, expected,)
                .is_err()
        );
        assert!(
            SourcePhysicsPackage::decode_pinned(
                &original,
                bytes_sha256(b"other carrier"),
                manifest_hash,
                expected,
            )
            .is_err()
        );
        assert!(
            SourcePhysicsPackage::decode_pinned(
                &original,
                carrier,
                bytes_sha256(b"other manifest"),
                expected,
            )
            .is_err()
        );
    }
}
