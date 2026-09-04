//! Read-only, bounded native-to-viewport projection. Never persisted in a workspace.
use crate::area_tabs::AreaKey;
use forge_core::{ContentHash, Vec3};
use forge_project::TerrainDocument;
use forge_world::{GeneratedTerrain, TerrainGenerationPlan, TerrainPartialUpdateError};
use serde::{Deserialize, Serialize};

pub const MAX_PREVIEW_JSON_BYTES: usize = 24 * 1024 * 1024;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AreaPreviewRequest {
    pub key: AreaKey,
    pub base_revision: u64,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AreaPreview {
    pub request: AreaPreviewRequest,
    pub terrain: GeneratedTerrain,
    pub spawn: Vec3,
    /// Some means that terrain.chunks contains only replacements against this exact cache.
    pub base_source_hash: Option<ContentHash>,
}

/// A single disposable native cache. Never persisted or used as an authoring source.
#[derive(Default)]
pub struct AreaPreviewCache {
    cached: Option<(TerrainDocument, GeneratedTerrain)>,
}

/// Run on the native blocking pool after copying the accepted source under the session lock.
pub fn prepare_area_preview(
    request: AreaPreviewRequest,
    document: &TerrainDocument,
    spawn: Vec3,
) -> Result<String, String> {
    AreaPreviewCache::default().prepare(request, document, spawn, None)
}

impl AreaPreviewCache {
    /// Called on the blocking pool, outside the workspace lock. Serialize before cache mutation.
    pub fn prepare(
        &mut self,
        request: AreaPreviewRequest,
        document: &TerrainDocument,
        spawn: Vec3,
        base_source_hash: Option<ContentHash>,
    ) -> Result<String, String> {
        if request.key.project_id != document.project_id
            || request.key.area_id != document.area_id
            || document.revision.get() > request.base_revision
            || ![spawn.x, spawn.y, spawn.z]
                .iter()
                .all(|value| value.is_finite())
        {
            return Err("Source d’aperçu incohérente avec la demande.".into());
        }
        if let Some((source, cache)) = self.cached.as_mut()
            && base_source_hash == Some(cache.source_document_hash)
        {
            let plan = TerrainGenerationPlan::from_document(source).map_err(|e| e.to_string())?;
            match plan.refresh_cached_projection(cache, document.clone()) {
                Ok(update) => {
                    let preview = AreaPreview {
                        request,
                        spawn,
                        base_source_hash,
                        terrain: GeneratedTerrain {
                            source_document_hash: update.target_document_hash(),
                            chunks: update.replacement_chunks().to_vec(),
                            origin_xz_m: cache.origin_xz_m,
                            material_layer_ids: cache.material_layer_ids.clone(),
                        },
                    };
                    let json = serialize_preview(&preview)?;
                    update.apply_to_cache(cache).map_err(|e| e.to_string())?;
                    *source = document.clone();
                    return Ok(json);
                }
                // A structural change explicitly requires a full rebuild; corruption does not.
                Err(TerrainPartialUpdateError::FullRebuildRequired) => {}
                Err(error) => return Err(error.to_string()),
            }
        }
        let terrain = TerrainGenerationPlan::from_document(document)
            .map_err(|error| error.to_string())?
            .generate()
            .map_err(|error| error.to_string())?;
        let preview = AreaPreview {
            request,
            terrain,
            spawn,
            base_source_hash: None,
        };
        let json = serialize_preview(&preview)?;
        self.cached = Some((document.clone(), preview.terrain));
        Ok(json)
    }
}

fn serialize_preview(preview: &AreaPreview) -> Result<String, String> {
    let json = serde_json::to_string(preview).map_err(|error| error.to_string())?;
    if json.len() > MAX_PREVIEW_JSON_BYTES {
        return Err("L’aperçu dépasse le budget de transfert de 24 Mio.".into());
    }
    Ok(json)
}

pub fn read_area_preview(json: &str) -> Result<AreaPreview, String> {
    if json.len() > MAX_PREVIEW_JSON_BYTES {
        return Err("Aperçu trop volumineux.".into());
    }
    serde_json::from_str(json).map_err(|error| error.to_string())
}

#[cfg(test)]
#[path = "area_preview_cache_tests.rs"]
mod cache_tests;

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{workspace_api::WorkspaceCommand, workspace_service::WorkspaceSession};
    use forge_core::{AreaId, ProjectId};
    use forge_project::WorkspaceDocument;

    #[test]
    fn native_preview_is_bounded_read_only_and_matches_the_accepted_source() {
        let mut session = WorkspaceSession::default();
        let before = session
            .execute(WorkspaceCommand::Create {
                project_name: "Preview".into(),
                module_name: "Module".into(),
                area_name: "Area".into(),
                seed: u64::MAX.to_string(),
                spawn_x: "12".into(),
                spawn_y: "2".into(),
                spawn_z: "0".into(),
            })
            .unwrap();
        let document =
            WorkspaceDocument::from_json(before.document_json.as_deref().unwrap()).unwrap();
        let module = &document.project.modules[0];
        let area = &module.areas[0];
        let request = AreaPreviewRequest {
            key: AreaKey {
                project_id: document.project.id,
                module_id: module.id,
                area_id: area.id,
            },
            base_revision: before.revision,
        };
        let (terrain, spawn) = session.area_preview_source(request).unwrap();
        let json = prepare_area_preview(request, &terrain, spawn).unwrap();
        let preview = read_area_preview(&json).unwrap();
        assert_eq!(preview.request, request);
        assert_eq!(preview.spawn, area.spawn);
        assert_eq!(
            preview.terrain.source_document_hash,
            terrain.content_hash().unwrap()
        );
        assert_eq!(preview.terrain.chunks.len(), 64);
        assert_eq!(preview.terrain.vertex_count(), 69_696);
        assert_eq!(preview.terrain.triangle_count(), 131_072);
        assert!(json.len() < MAX_PREVIEW_JSON_BYTES);
        println!("native preview JSON bytes: {}", json.len());
        let after = session.execute(WorkspaceCommand::Current).unwrap();
        assert_eq!(before.document_json, after.document_json);
        assert_eq!(before.last_receipt_json, after.last_receipt_json);
        assert_eq!(before.revision, after.revision);
        assert_eq!(before.can_undo, after.can_undo);
        assert!(
            session
                .area_preview_source(AreaPreviewRequest {
                    base_revision: 0,
                    ..request
                })
                .is_err()
        );
        let foreign = AreaPreviewRequest {
            key: AreaKey {
                project_id: ProjectId::deterministic("foreign"),
                ..request.key
            },
            ..request
        };
        assert!(session.area_preview_source(foreign).is_err());
        let absent = AreaPreviewRequest {
            key: AreaKey {
                area_id: AreaId::deterministic("absent"),
                ..request.key
            },
            ..request
        };
        assert!(session.area_preview_source(absent).is_err());
    }
}
