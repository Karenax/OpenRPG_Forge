//! Bounded read-only transport of the current terrain's immutable raster.
use crate::TerrainDocument;
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};

pub const CHUNK_BYTES: usize = 256 * 1024;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TerrainSourceChunk {
    pub document_hash: ContentHash,
    pub source_hash: ContentHash,
    pub total_bytes: usize,
    pub offset: usize,
    pub bytes: Vec<u8>,
}

/// A response is accepted only for the exact document and next requested offset.
/// Nothing is made available as a terrain until the complete hash is verified.
pub struct TerrainSourceAssembly {
    document_hash: ContentHash,
    source_hash: ContentHash,
    total_bytes: usize,
    bytes: Vec<u8>,
}
impl TerrainSourceAssembly {
    pub fn new(document: &TerrainDocument) -> Result<Self, String> {
        document.validate().map_err(|e| e.to_string())?;
        let source = document
            .sampled_source
            .as_ref()
            .ok_or("Terrain has no raster source")?;
        Ok(Self {
            document_hash: document.content_hash().map_err(|e| e.to_string())?,
            source_hash: source.samples_sha256,
            total_bytes: source.byte_length().map_err(|e| e.to_string())?,
            bytes: Vec::new(),
        })
    }
    #[must_use]
    pub fn next_offset(&self) -> Option<usize> {
        (self.bytes.len() < self.total_bytes).then_some(self.bytes.len())
    }
    pub fn accept(&mut self, chunk: TerrainSourceChunk) -> Result<(), String> {
        if chunk.document_hash != self.document_hash
            || chunk.source_hash != self.source_hash
            || chunk.total_bytes != self.total_bytes
            || chunk.offset != self.bytes.len()
            || chunk.bytes.is_empty()
            || chunk.bytes.len() != CHUNK_BYTES.min(self.total_bytes - self.bytes.len())
        {
            return Err("Terrain source chunk does not match the pending document".into());
        }
        self.bytes.extend(chunk.bytes);
        Ok(())
    }
    pub fn finish(self) -> Result<Vec<u8>, String> {
        if self.bytes.len() != self.total_bytes || bytes_sha256(&self.bytes) != self.source_hash {
            return Err("Terrain source is incomplete or has an invalid hash".into());
        }
        Ok(self.bytes)
    }
}
