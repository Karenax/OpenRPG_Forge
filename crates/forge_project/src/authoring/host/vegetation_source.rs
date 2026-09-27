use super::{AuthoringHost, session::ActiveSession};
use crate::{
    authoring::vegetation_source::{self, CHUNK_BYTES, VegetationSourceChunk},
    workspace::source_blobs::SourceBlobStore,
};
use forge_core::ContentHash;

pub(super) struct CachedBlock {
    hash: ContentHash,
    authority: std::path::PathBuf,
    bytes: Vec<u8>,
}
impl AuthoringHost {
    /// The caller selects an index in the current source; paths and blob hashes
    /// are resolved exclusively from the active canonical head.
    pub fn read_vegetation_source(
        &mut self,
        expected_head: ContentHash,
        block_index: usize,
        offset: usize,
    ) -> Result<VegetationSourceChunk, String> {
        let active = self.active.as_ref().ok_or("No authoring session open")?;
        let head = active.head().ok_or("No aggregate zone open")?;
        let head_hash = head.content_hash()?;
        if head_hash != expected_head {
            return Err("Vegetation source request is stale".into());
        }
        let source = vegetation_source::block(head, block_index)?;
        let total = source.byte_len()?;
        let hash = source.instances_sha256;
        if offset >= total || !offset.is_multiple_of(CHUNK_BYTES) {
            return Err("Invalid vegetation source chunk offset".into());
        }
        let authority = if matches!(active, ActiveSession::Managed { .. }) {
            let path = self
                .root
                .join("workspace-v1")
                .canonicalize()
                .map_err(|e| e.to_string())?;
            if path.parent() != Some(self.root.as_path()) {
                return Err("Workspace source path leaves authorized root".into());
            }
            path
        } else {
            self.directory(head.base.terrain.area_id)?
        };
        if !self
            .vegetation_source_cache
            .as_ref()
            .is_some_and(|v| v.hash == hash && v.authority == authority && v.bytes.len() == total)
        {
            let bytes = SourceBlobStore::open_existing(&authority)?.read(hash, total)?;
            self.vegetation_source_cache = Some(CachedBlock {
                hash,
                authority,
                bytes,
            });
        }
        let cache = self
            .vegetation_source_cache
            .as_ref()
            .ok_or("Vegetation cache unavailable")?;
        Ok(VegetationSourceChunk {
            head_hash,
            block_index,
            source_hash: hash,
            total_bytes: total,
            offset,
            bytes: cache.bytes[offset..(offset + CHUNK_BYTES).min(total)].to_vec(),
        })
    }
}
