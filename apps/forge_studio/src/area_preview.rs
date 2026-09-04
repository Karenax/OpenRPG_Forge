//! Read-only, bounded native-to-viewport projection. Never persisted in a workspace.
use crate::area_tabs::AreaKey;
use forge_core::{ContentHash, Vec3};
use forge_project::{TerrainDocument, WorkspaceArea};
use forge_world::{
    GeneratedTerrain, GeneratedVegetation, TerrainGenerationPlan, TerrainPartialUpdate,
    TerrainPartialUpdateError, VegetationRuntimeCache,
};
use serde::{Deserialize, Serialize};

pub const MAX_PREVIEW_JSON_BYTES: usize = 24 * 1024 * 1024;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AreaPreviewRequest {
    pub key: AreaKey,
    pub base_revision: u64,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AreaPreview {
    pub request: AreaPreviewRequest,
    pub terrain: GeneratedTerrain,
    /// Always complete: consumers never need an earlier vegetation payload to reconstruct it.
    pub vegetation: GeneratedVegetation,
    /// Density is not part of `GeneratedVegetation`, so its exact source hash travels separately.
    pub vegetation_density_source_hash: ContentHash,
    pub vegetation_regenerated: bool,
    pub vegetation_changed_chunks: usize,
    pub vegetation_retained_chunks: usize,
    pub spawn: Vec3,
    /// Some means that terrain.chunks contains only replacements against this exact cache.
    pub base_source_hash: Option<ContentHash>,
}

/// A single disposable native cache. Never persisted or used as an authoring source.
#[derive(Default)]
pub struct AreaPreviewCache {
    cached: Option<CachedAreaPreview>,
}

struct CachedAreaPreview {
    terrain_source: TerrainDocument,
    terrain: GeneratedTerrain,
    vegetation: VegetationRuntimeCache,
}

/// Run on the native blocking pool after copying the accepted source under the session lock.
pub fn prepare_area_preview(
    request: AreaPreviewRequest,
    area: &WorkspaceArea,
) -> Result<String, String> {
    AreaPreviewCache::default().prepare(request, area, None)
}

impl AreaPreviewCache {
    /// Called on the blocking pool, outside the workspace lock. Serialize before cache mutation.
    pub fn prepare(
        &mut self,
        request: AreaPreviewRequest,
        area: &WorkspaceArea,
        base_source_hash: Option<ContentHash>,
    ) -> Result<String, String> {
        validate_preview_source(request, area)?;
        self.prepare_validated(request, area, base_source_hash)
    }

    /// Projects one disposable density candidate whose revision is exactly the next revision.
    pub fn prepare_vegetation_density_candidate(
        &mut self,
        request: AreaPreviewRequest,
        area: &WorkspaceArea,
        base_source_hash: Option<ContentHash>,
    ) -> Result<String, String> {
        validate_vegetation_density_candidate_source(request, area)?;
        self.prepare_validated(request, area, base_source_hash)
    }

    fn prepare_validated(
        &mut self,
        request: AreaPreviewRequest,
        area: &WorkspaceArea,
        base_source_hash: Option<ContentHash>,
    ) -> Result<String, String> {
        let (vegetation_cache, vegetation_regenerated, changed_chunks, retained_chunks) =
            self.prepare_vegetation(area)?;
        let vegetation = vegetation_cache.snapshot();
        let vegetation_density_source_hash =
            vegetation_cache.key().vegetation_density_document_hash();
        let partial_update = self.prepare_partial_terrain(area, base_source_hash)?;
        let terrain = match &partial_update {
            Some(update) => {
                let cache = &self.cached.as_ref().expect("partial cache checked").terrain;
                GeneratedTerrain {
                    source_document_hash: update.target_document_hash(),
                    chunks: update.replacement_chunks().to_vec(),
                    origin_xz_m: cache.origin_xz_m,
                    material_layer_ids: cache.material_layer_ids.clone(),
                }
            }
            None => TerrainGenerationPlan::from_document(&area.terrain)
                .map_err(|error| error.to_string())?
                .generate()
                .map_err(|error| error.to_string())?,
        };
        let preview = AreaPreview {
            request,
            terrain,
            vegetation,
            vegetation_density_source_hash,
            vegetation_regenerated,
            vegetation_changed_chunks: changed_chunks,
            vegetation_retained_chunks: retained_chunks,
            spawn: area.spawn,
            base_source_hash: partial_update.as_ref().map(|_| {
                base_source_hash.expect("a partial update requires an exact base source hash")
            }),
        };
        let json = serialize_preview(&preview)?;
        if let Some(update) = partial_update {
            let cache = self.cached.as_mut().expect("partial cache checked");
            update
                .apply_to_cache(&mut cache.terrain)
                .map_err(|error| error.to_string())?;
            cache.terrain_source = area.terrain.clone();
            cache.vegetation = vegetation_cache;
        } else {
            self.cached = Some(CachedAreaPreview {
                terrain_source: area.terrain.clone(),
                terrain: preview.terrain,
                vegetation: vegetation_cache,
            });
        }
        Ok(json)
    }

    fn prepare_vegetation(
        &self,
        area: &WorkspaceArea,
    ) -> Result<(VegetationRuntimeCache, bool, usize, usize), String> {
        if let Some(cached) = &self.cached {
            let mut vegetation = cached.vegetation.clone();
            let update = vegetation
                .refresh_from_area(area)
                .map_err(|error| error.to_string())?;
            let changed_chunks = update.changed_chunks().len();
            let retained_chunks = update.retained_chunks();
            return Ok((
                vegetation,
                update.regenerated(),
                changed_chunks,
                retained_chunks,
            ));
        }
        let vegetation =
            VegetationRuntimeCache::from_area(area).map_err(|error| error.to_string())?;
        let changed_chunks = vegetation.chunks().len();
        Ok((vegetation, true, changed_chunks, 0))
    }

    fn prepare_partial_terrain(
        &self,
        area: &WorkspaceArea,
        base_source_hash: Option<ContentHash>,
    ) -> Result<Option<TerrainPartialUpdate>, String> {
        let Some(cache) = &self.cached else {
            return Ok(None);
        };
        if base_source_hash != Some(cache.terrain.source_document_hash) {
            return Ok(None);
        }
        let plan = TerrainGenerationPlan::from_document(&cache.terrain_source)
            .map_err(|error| error.to_string())?;
        match plan.refresh_cached_projection(&cache.terrain, area.terrain.clone()) {
            Ok(update) => Ok(Some(update)),
            // A structural change explicitly requires a full rebuild; corruption does not.
            Err(TerrainPartialUpdateError::FullRebuildRequired) => Ok(None),
            Err(error) => Err(error.to_string()),
        }
    }
}

fn validate_preview_source(
    request: AreaPreviewRequest,
    area: &WorkspaceArea,
) -> Result<(), String> {
    validate_preview_identity(request, area)?;
    let revisions = [
        area.terrain.revision.get(),
        area.vegetation.revision.get(),
        area.vegetation_density.revision.get(),
    ];
    if revisions
        .into_iter()
        .any(|revision| revision > request.base_revision)
    {
        return Err("Source d’aperçu incohérente avec la demande.".into());
    }
    Ok(())
}

fn validate_vegetation_density_candidate_source(
    request: AreaPreviewRequest,
    area: &WorkspaceArea,
) -> Result<(), String> {
    validate_preview_identity(request, area)?;
    let candidate_revision = request
        .base_revision
        .checked_add(1)
        .ok_or("Révision candidate épuisée.")?;
    if area.terrain.revision.get() > request.base_revision
        || area.vegetation.revision.get() > request.base_revision
        || area.vegetation_density.revision.get() != candidate_revision
    {
        return Err("Source candidate incohérente avec la demande d’aperçu.".into());
    }
    Ok(())
}

fn validate_preview_identity(
    request: AreaPreviewRequest,
    area: &WorkspaceArea,
) -> Result<(), String> {
    let project_id = request.key.project_id;
    let area_id = request.key.area_id;
    if area.id != area_id
        || area.terrain.project_id != project_id
        || area.terrain.area_id != area_id
        || area.vegetation.project_id != project_id
        || area.vegetation.area_id != area_id
        || area.vegetation_density.project_id != project_id
        || area.vegetation_density.area_id != area_id
        || ![area.spawn.x, area.spawn.y, area.spawn.z]
            .iter()
            .all(|value| value.is_finite())
    {
        return Err("Source d’aperçu incohérente avec la demande.".into());
    }
    Ok(())
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
        let accepted_area = session.area_preview_source(request).unwrap();
        assert_eq!(accepted_area, *area);
        let json = prepare_area_preview(request, &accepted_area).unwrap();
        let preview = read_area_preview(&json).unwrap();
        assert_eq!(preview.request, request);
        assert_eq!(preview.spawn, area.spawn);
        assert_eq!(
            preview.terrain.source_document_hash,
            accepted_area.terrain.content_hash().unwrap()
        );
        assert_eq!(preview.terrain.chunks.len(), 64);
        assert_eq!(preview.terrain.vertex_count(), 69_696);
        assert_eq!(preview.terrain.triangle_count(), 131_072);
        assert_eq!(preview.vegetation.chunks.len(), 64);
        assert_eq!(preview.vegetation_changed_chunks, 64);
        assert_eq!(preview.vegetation_retained_chunks, 0);
        assert!(preview.vegetation_regenerated);
        assert!(
            preview
                .vegetation
                .chunks
                .iter()
                .all(|chunk| chunk.instances.is_empty())
        );
        assert_eq!(
            preview.vegetation_density_source_hash,
            area.vegetation_density.content_hash().unwrap()
        );
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
