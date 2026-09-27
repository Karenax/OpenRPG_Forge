//! Bounded immutable population transport, authorized by the exact open zone head.
use crate::{imported_vegetation::ImportedVegetationBlock, zone_authoring::ZoneAuthoringHead};
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};

pub const CHUNK_BYTES: usize = 256 * 1024;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VegetationSourceChunk {
    pub head_hash: ContentHash,
    pub block_index: usize,
    pub source_hash: ContentHash,
    pub total_bytes: usize,
    pub offset: usize,
    pub bytes: Vec<u8>,
}

pub fn block(head: &ZoneAuthoringHead, index: usize) -> Result<&ImportedVegetationBlock, String> {
    head.base
        .vegetation
        .as_ref()
        .and_then(|v| v.imported_source.as_ref())
        .and_then(|s| s.blocks.get(index))
        .ok_or_else(|| "No imported vegetation block at this index".into())
}

pub struct VegetationSourceAssembly {
    head_hash: ContentHash,
    block_index: usize,
    source_hash: ContentHash,
    total_bytes: usize,
    bytes: Vec<u8>,
}
impl VegetationSourceAssembly {
    pub fn new(head: &ZoneAuthoringHead, block_index: usize) -> Result<Self, String> {
        head.validate()?;
        let source = block(head, block_index)?;
        Ok(Self {
            head_hash: head.content_hash()?,
            block_index,
            source_hash: source.instances_sha256,
            total_bytes: source.byte_len()?,
            bytes: Vec::new(),
        })
    }
    #[must_use]
    pub fn next_offset(&self) -> Option<usize> {
        (self.bytes.len() < self.total_bytes).then_some(self.bytes.len())
    }
    pub fn accept(&mut self, chunk: VegetationSourceChunk) -> Result<(), String> {
        if chunk.head_hash != self.head_hash
            || chunk.block_index != self.block_index
            || chunk.source_hash != self.source_hash
            || chunk.total_bytes != self.total_bytes
            || chunk.offset != self.bytes.len()
            || chunk.bytes.is_empty()
            || chunk.bytes.len() != CHUNK_BYTES.min(self.total_bytes - self.bytes.len())
        {
            return Err("Vegetation source chunk does not match the pending zone and block".into());
        }
        self.bytes.extend(chunk.bytes);
        Ok(())
    }
    pub fn finish(self) -> Result<Vec<u8>, String> {
        if self.bytes.len() != self.total_bytes || bytes_sha256(&self.bytes) != self.source_hash {
            return Err("Vegetation source is incomplete or has an invalid hash".into());
        }
        Ok(self.bytes)
    }
}
