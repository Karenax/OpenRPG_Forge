//! Read-only, pinned render companions of the currently open module.
//! Native paths never cross this boundary; resource ordinals refer to an admitted inventory.
use forge_core::{AreaId, ContentHash, ModuleId, bytes_sha256};
use forge_project::module_package::{
    ModuleEnvironmentBinding, ModuleFile, ModuleFishBinding, ModuleObjectBinding,
    ModulePlacementCompanion, ModuleTerrainBinding, ModuleVegetationBinding,
};
use serde::{Deserialize, Serialize};

pub const CHUNK_BYTES: usize = 64 * 1024;
pub const MAX_RESOURCE_BYTES: u64 = 256 * 1024 * 1024;
const CHUNK_MAGIC: &[u8; 8] = b"ORPC\x01\0\0\0";
const CHUNK_PREFIX: usize = 12;
const MAX_CHUNK_HEADER: usize = 1024;
pub const MAX_CHUNK_WIRE: usize = CHUNK_PREFIX + MAX_CHUNK_HEADER + CHUNK_BYTES;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum Request {
    Describe {
        expected_head: ContentHash,
    },
    Read {
        expected_head: ContentHash,
        expected_package: ContentHash,
        resource_index: usize,
        offset: usize,
    },
}

impl Request {
    #[must_use]
    pub fn expected_head(&self) -> ContentHash {
        match self {
            Self::Describe { expected_head } | Self::Read { expected_head, .. } => *expected_head,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Snapshot {
    pub head_hash: ContentHash,
    pub package_sha256: ContentHash,
    /// Verified installation resources for a managed map predating ORPMOD.
    #[serde(default, skip_serializing_if = "std::ops::Not::not")]
    pub legacy_reference: bool,
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub vegetation: Option<ModuleVegetationBinding>,
    pub objects: Option<ModuleObjectBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub fish_profiles: Vec<ModuleFishBinding>,
    pub environment: Option<ModuleEnvironmentBinding>,
    pub terrain: Option<ModuleTerrainBinding>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub ocean: Option<forge_project::module_package::ModuleOceanBinding>,
    pub local_lights: Vec<forge_project::module_package::ModuleLocalLightPackage>,
    /// Exact packages retained by the original module; reads still require a complete pin.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub placement_companions: Vec<ModulePlacementCompanion>,
    /// Derived by the existing resolver for this head, never a new source catalogue.
    pub species: Option<Box<Species>>,
    /// Portable package paths are metadata, never caller-selected native paths.
    pub files: Vec<ModuleFile>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Species {
    pub catalog: forge_assets::VegetationPrototypeCatalog,
    pub asset_kit: forge_assets::GovernedAssetKitBuildReport,
    pub assets: std::collections::BTreeMap<String, forge_project::module_package::ModuleFilePin>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Chunk {
    pub head_hash: ContentHash,
    pub package_sha256: ContentHash,
    pub resource_index: usize,
    pub resource_sha256: ContentHash,
    pub total_bytes: usize,
    pub offset: usize,
    pub bytes: Vec<u8>,
}

impl Chunk {
    /// Keep the existing metadata checks while moving the payload as raw bytes.
    pub fn into_wire(mut self) -> Result<Vec<u8>, String> {
        let bytes = std::mem::take(&mut self.bytes);
        let header = serde_json::to_vec(&self).map_err(|e| e.to_string())?;
        if header.len() > MAX_CHUNK_HEADER || bytes.is_empty() || bytes.len() > CHUNK_BYTES {
            return Err("Module resource chunk exceeds its binary wire bounds".into());
        }
        let mut wire = Vec::with_capacity(CHUNK_PREFIX + header.len() + bytes.len());
        wire.extend_from_slice(CHUNK_MAGIC);
        wire.extend_from_slice(&(header.len() as u32).to_le_bytes());
        wire.extend_from_slice(&header);
        wire.extend_from_slice(&bytes);
        Ok(wire)
    }

    pub fn from_wire(mut wire: Vec<u8>) -> Result<Self, String> {
        if wire.len() <= CHUNK_PREFIX
            || wire.len() > MAX_CHUNK_WIRE
            || !wire.starts_with(CHUNK_MAGIC)
        {
            return Err("Invalid module resource chunk envelope".into());
        }
        let header_len = u32::from_le_bytes(
            wire[8..CHUNK_PREFIX]
                .try_into()
                .map_err(|_| "Invalid chunk length")?,
        ) as usize;
        if header_len > MAX_CHUNK_HEADER {
            return Err("Invalid module resource chunk header length".into());
        }
        let body_start = CHUNK_PREFIX + header_len;
        if body_start >= wire.len() || wire.len() - body_start > CHUNK_BYTES {
            return Err("Invalid module resource chunk bounds".into());
        }
        let mut chunk: Self =
            serde_json::from_slice(&wire[CHUNK_PREFIX..body_start]).map_err(|e| e.to_string())?;
        if !chunk.bytes.is_empty() {
            return Err("Module resource chunk contains a duplicate body".into());
        }
        wire.drain(..body_start);
        chunk.bytes = wire;
        Ok(chunk)
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum Reply {
    Description { snapshot: Option<Box<Snapshot>> },
    Resource { chunk: Chunk },
}

/// Browser/native consumers assemble the same ordered, hash-checked resource.
pub struct Assembly {
    head: ContentHash,
    package: ContentHash,
    resource_index: usize,
    resource: ModuleFile,
    total: usize,
    bytes: Vec<u8>,
}

impl Assembly {
    pub fn new(snapshot: &Snapshot, resource_index: usize) -> Result<Self, String> {
        let resource = snapshot
            .files
            .get(resource_index)
            .ok_or("Module resource absent")?;
        let total = usize::try_from(resource.bytes).map_err(|error| error.to_string())?;
        if resource.bytes > MAX_RESOURCE_BYTES {
            return Err("Module resource exceeds its admitted budget".into());
        }
        Ok(Self {
            head: snapshot.head_hash,
            package: snapshot.package_sha256,
            resource_index,
            resource: resource.clone(),
            total,
            bytes: Vec::new(),
        })
    }

    #[must_use]
    pub fn next_offset(&self) -> Option<usize> {
        (self.bytes.len() < self.total).then_some(self.bytes.len())
    }

    pub fn accept(&mut self, chunk: Chunk) -> Result<(), String> {
        let expected = CHUNK_BYTES.min(self.total - self.bytes.len());
        if expected == 0
            || chunk.head_hash != self.head
            || chunk.package_sha256 != self.package
            || chunk.resource_index != self.resource_index
            || chunk.resource_sha256 != self.resource.sha256
            || chunk.total_bytes != self.total
            || chunk.offset != self.bytes.len()
            || chunk.bytes.len() != expected
        {
            return Err("Module resource chunk is stale, reordered or incomplete".into());
        }
        self.bytes.extend(chunk.bytes);
        Ok(())
    }

    pub fn finish(self) -> Result<Vec<u8>, String> {
        if self.bytes.len() != self.total || bytes_sha256(&self.bytes) != self.resource.sha256 {
            return Err("Module resource differs from its exact admitted pin".into());
        }
        Ok(self.bytes)
    }
}
