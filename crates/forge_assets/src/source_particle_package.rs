//! Binary particle evidence transport. Expected pins come from the governed
//! library revision, never from an untrusted envelope alone.
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

const MAGIC: &[u8; 8] = b"ORPE\x01\0\0\0";
const MAX_HEADER: usize = 1024 * 1024;
const MAX_SECTION: usize = 64 * 1024 * 1024;
const MAX_RESOURCES: usize = 128 * 1024 * 1024;
pub const MAX_PACKAGE_BYTES: usize = 12 + 2 * MAX_HEADER + MAX_RESOURCES;

pub struct SourceParticlePackage {
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub manifest: Vec<u8>,
    pub resources: BTreeMap<String, Vec<u8>>,
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
    resources: Vec<Section>,
}

fn relative(path: &str) -> bool {
    !path.is_empty()
        && !path.starts_with('/')
        && !path.contains(['\\', ':'])
        && path
            .split('/')
            .all(|part| !part.is_empty() && part != "." && part != "..")
}

impl SourceParticlePackage {
    /// Validate source identities and reject resources with no declared owner.
    pub fn resolve_for_carrier(
        &self,
        carrier: &[u8],
    ) -> Result<crate::source_particle_transport::ResolvedSourceParticleTransport, String> {
        if bytes_sha256(carrier) != self.carrier_sha256 {
            return Err("Particle package carrier differs".into());
        }
        let resolved =
            crate::source_particle_transport::SourceParticleTransport::decode_for_carrier(
                &self.manifest,
                self.manifest_sha256,
                carrier,
                |path| {
                    self.resources
                        .get(path)
                        .cloned()
                        .ok_or("Particle resource absent".into())
                },
            )?;
        if resolved.resources.len() != self.resources.len() {
            return Err("Particle resource without declared owner".into());
        }
        Ok(resolved)
    }
    /// Verify the complete caller-held package pin as well as its inner pins.
    /// Identical carrier/manifest pins do not identify resource buffers alone.
    pub fn decode_pinned(
        wire: &[u8],
        carrier: ContentHash,
        manifest: ContentHash,
        package: ContentHash,
    ) -> Result<Self, String> {
        if wire.len() > MAX_PACKAGE_BYTES || bytes_sha256(wire) != package {
            return Err("particle package SHA-256 or budget differs".into());
        }
        Self::decode(wire, carrier, manifest)
    }

    /// Encode original bytes without JSON expansion of the binary buffers.
    pub fn encode(&self) -> Result<Vec<u8>, String> {
        if self.manifest.is_empty()
            || self.manifest.len() > MAX_HEADER
            || bytes_sha256(&self.manifest) != self.manifest_sha256
            || self.resources.len() > 4096
        {
            return Err("particle package manifest or budget differs".into());
        }
        let mut total = 0usize;
        let mut resources = Vec::new();
        for (path, bytes) in &self.resources {
            total = total
                .checked_add(bytes.len())
                .ok_or("particle package overflow")?;
            if !relative(path)
                || bytes.is_empty()
                || bytes.len() > MAX_SECTION
                || total > MAX_RESOURCES
            {
                return Err("particle package resource path or budget differs".into());
            }
            resources.push(Section {
                path: path.clone(),
                byte_length: bytes.len(),
                sha256: bytes_sha256(bytes),
            });
        }
        let header = serde_json::to_vec(&Header {
            carrier_sha256: self.carrier_sha256,
            manifest_sha256: self.manifest_sha256,
            manifest_length: self.manifest.len(),
            resources,
        })
        .map_err(|e| e.to_string())?;
        if header.len() > MAX_HEADER {
            return Err("particle package header budget exceeded".into());
        }
        let mut wire = Vec::with_capacity(12 + header.len() + self.manifest.len() + total);
        wire.extend_from_slice(MAGIC);
        wire.extend_from_slice(
            &u32::try_from(header.len())
                .map_err(|_| "particle header length")?
                .to_le_bytes(),
        );
        wire.extend(header);
        wire.extend_from_slice(&self.manifest);
        for bytes in self.resources.values() {
            wire.extend_from_slice(bytes);
        }
        Ok(wire)
    }

    /// Verify caller-held model and manifest pins before returning any payload.
    /// Semantic source/component validation belongs to `source_particle_transport`.
    pub fn decode(
        wire: &[u8],
        carrier: ContentHash,
        manifest: ContentHash,
    ) -> Result<Self, String> {
        if wire.len() < 12 || wire.len() > MAX_PACKAGE_BYTES || &wire[..8] != MAGIC {
            return Err("particle package version or budget differs".into());
        }
        let header_length = usize::try_from(u32::from_le_bytes(
            wire[8..12].try_into().map_err(|_| "particle header")?,
        ))
        .map_err(|_| "particle header length")?;
        if header_length == 0 || header_length > MAX_HEADER || wire.len() < 12 + header_length {
            return Err("particle package header truncated".into());
        }
        let header: Header =
            serde_json::from_slice(&wire[12..12 + header_length]).map_err(|e| e.to_string())?;
        if header.carrier_sha256 != carrier
            || header.manifest_sha256 != manifest
            || header.manifest_length == 0
            || header.manifest_length > MAX_HEADER
            || header.resources.len() > 4096
        {
            return Err("particle package pins or section budget differs".into());
        }
        let mut offset = 12 + header_length;
        let manifest_bytes = take(wire, &mut offset, header.manifest_length, manifest)?.to_vec();
        let mut resources = BTreeMap::new();
        let mut total = 0usize;
        for section in header.resources {
            total = total
                .checked_add(section.byte_length)
                .ok_or("particle section overflow")?;
            if !relative(&section.path)
                || section.byte_length == 0
                || section.byte_length > MAX_SECTION
                || total > MAX_RESOURCES
                || resources.contains_key(&section.path)
            {
                return Err("particle package resource invalid or duplicated".into());
            }
            resources.insert(
                section.path,
                take(wire, &mut offset, section.byte_length, section.sha256)?.to_vec(),
            );
        }
        if offset != wire.len() {
            return Err("particle package trailing bytes".into());
        }
        Ok(Self {
            carrier_sha256: carrier,
            manifest_sha256: manifest,
            manifest: manifest_bytes,
            resources,
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
        .ok_or("particle section overflow")?;
    let bytes = wire.get(*offset..end).ok_or("particle section truncated")?;
    if bytes_sha256(bytes) != hash {
        return Err("particle section SHA-256 differs".into());
    }
    *offset = end;
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn malformed_sections_trailing_bytes_and_untrusted_paths_are_refused() {
        let manifest = b"{}".to_vec();
        let mut package = SourceParticlePackage {
            carrier_sha256: bytes_sha256(b"carrier"),
            manifest_sha256: bytes_sha256(&manifest),
            manifest,
            resources: BTreeMap::from([("state.bin".into(), vec![1; 16])]),
        };
        let wire = package.encode().unwrap();
        for length in [0, 8, 12, wire.len() - 1] {
            assert!(
                SourceParticlePackage::decode(
                    &wire[..length],
                    package.carrier_sha256,
                    package.manifest_sha256
                )
                .is_err()
            );
        }
        let mut corrupt = wire.clone();
        *corrupt.last_mut().unwrap() ^= 1;
        assert!(
            SourceParticlePackage::decode(
                &corrupt,
                package.carrier_sha256,
                package.manifest_sha256
            )
            .is_err()
        );
        let mut trailing = wire;
        trailing.push(0);
        assert!(
            SourceParticlePackage::decode(
                &trailing,
                package.carrier_sha256,
                package.manifest_sha256
            )
            .is_err()
        );
        package.resources = BTreeMap::from([("../state.bin".into(), vec![1; 16])]);
        assert!(package.encode().is_err());
        let oversized: Vec<u8> = vec![0; MAX_HEADER + 1];
        package.manifest_sha256 = bytes_sha256(&oversized);
        package.manifest = oversized;
        assert!(package.encode().is_err());
    }

    #[test]
    fn full_package_pin_distinguishes_resources_with_same_carrier_and_manifest() {
        let carrier = bytes_sha256(b"carrier");
        let manifest = b"{\"owners\":[]}".to_vec();
        let manifest_hash = bytes_sha256(&manifest);
        let package = |resource: &[u8]| SourceParticlePackage {
            carrier_sha256: carrier,
            manifest_sha256: manifest_hash,
            manifest: manifest.clone(),
            resources: BTreeMap::from([("resource.bin".into(), resource.to_vec())]),
        };
        let original = package(b"first state").encode().unwrap();
        let replacement = package(b"other state").encode().unwrap();
        let expected = bytes_sha256(&original);
        assert!(SourceParticlePackage::decode(&replacement, carrier, manifest_hash).is_ok());
        let decoded =
            SourceParticlePackage::decode_pinned(&original, carrier, manifest_hash, expected)
                .unwrap();
        assert_eq!(decoded.resources["resource.bin"], b"first state");
        assert!(
            SourceParticlePackage::decode_pinned(&replacement, carrier, manifest_hash, expected,)
                .is_err()
        );
        assert!(
            SourceParticlePackage::decode_pinned(
                &original,
                bytes_sha256(b"other carrier"),
                manifest_hash,
                expected,
            )
            .is_err()
        );
        assert!(
            SourceParticlePackage::decode_pinned(
                &original,
                carrier,
                bytes_sha256(b"other manifest"),
                expected,
            )
            .is_err()
        );
    }
}
