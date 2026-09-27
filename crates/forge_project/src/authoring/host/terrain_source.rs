use super::{AuthoringHost, session::ActiveSession};
use crate::{
    authoring::terrain_source::{CHUNK_BYTES, TerrainSourceChunk},
    workspace::source_blobs::SourceBlobStore,
};
use forge_core::ContentHash;

pub(super) struct CachedRaster {
    hash: ContentHash,
    bytes: Vec<u8>,
}
impl AuthoringHost {
    /// No caller-supplied path or blob hash: the open canonical document is authority.
    pub fn read_terrain_source(
        &mut self,
        expected_document: ContentHash,
        offset: usize,
    ) -> Result<TerrainSourceChunk, String> {
        let active = self.active.as_ref().ok_or("No authoring session open")?;
        let document = active.document()?;
        let document_hash = document.content_hash().map_err(|e| e.to_string())?;
        if document_hash != expected_document {
            return Err("Terrain source request is stale".into());
        }
        let source = document
            .sampled_source
            .as_ref()
            .ok_or("Terrain has no raster source")?;
        let total = source.byte_length().map_err(|e| e.to_string())?;
        if offset >= total || !offset.is_multiple_of(CHUNK_BYTES) {
            return Err("Invalid terrain source chunk offset".into());
        }
        if !self
            .terrain_source_cache
            .as_ref()
            .is_some_and(|v| v.hash == source.samples_sha256 && v.bytes.len() == total)
        {
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
                self.directory(document.area_id)?
            };
            let bytes =
                SourceBlobStore::open_existing(&authority)?.read(source.samples_sha256, total)?;
            self.terrain_source_cache = Some(CachedRaster {
                hash: source.samples_sha256,
                bytes,
            });
        }
        let cache = self
            .terrain_source_cache
            .as_ref()
            .ok_or("Terrain source cache unavailable")?;
        Ok(TerrainSourceChunk {
            document_hash,
            source_hash: source.samples_sha256,
            total_bytes: total,
            offset,
            bytes: cache.bytes[offset..(offset + CHUNK_BYTES).min(total)].to_vec(),
        })
    }
}
