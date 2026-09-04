use super::*;
use forge_core::{ModuleId, Revision};
use forge_project::TerrainHeightDelta;

fn document() -> TerrainDocument {
    forge_world::embedded_visual_valley_terrain_document()
        .unwrap()
        .migrate_to_latest()
        .unwrap()
}
fn request(document: &TerrainDocument) -> AreaPreviewRequest {
    AreaPreviewRequest {
        key: AreaKey {
            project_id: document.project_id,
            module_id: ModuleId::deterministic("cache"),
            area_id: document.area_id,
        },
        base_revision: document.revision.get(),
    }
}
fn spawn() -> Vec3 {
    Vec3 {
        x: 0.0,
        y: 2.0,
        z: 0.0,
    }
}

#[test]
fn sparse_projection_crosses_revisions_undo_redo_and_reopen_with_exact_full_parity() {
    let source = document();
    let before = source.clone();
    let mut cache = AreaPreviewCache::default();
    let initial = cache
        .prepare(request(&source), &source, spawn(), None)
        .unwrap();
    let mut assembled = read_area_preview(&initial).unwrap().terrain;
    for (revision, offset) in [(7, 5.0), (12, 0.0), (19, 5.0), (2, -3.0), (2, -3.0)] {
        let mut target = source.clone();
        target.revision = Revision::new(revision);
        if offset != 0.0 {
            target.height_deltas = vec![TerrainHeightDelta::new(31, 31, offset)];
        }
        let old_hash = assembled.source_document_hash;
        let json = cache
            .prepare(request(&target), &target, spawn(), Some(old_hash))
            .unwrap();
        let preview = read_area_preview(&json).unwrap();
        assert_eq!(preview.base_source_hash, Some(old_hash));
        assert!(preview.terrain.chunks.len() <= 3);
        assert!(json.len() < initial.len() / 10);
        println!(
            "revision {revision}: {} chunks, {} bytes (full {})",
            preview.terrain.chunks.len(),
            json.len(),
            initial.len()
        );
        for chunk in preview.terrain.chunks {
            let index = usize::from(chunk.coordinate.z) * 8 + usize::from(chunk.coordinate.x);
            assembled.chunks[index] = chunk;
        }
        assembled.source_document_hash = preview.terrain.source_document_hash;
        let full = TerrainGenerationPlan::from_document(&target)
            .unwrap()
            .generate()
            .unwrap();
        assert_eq!(assembled, full);
        assert_eq!(cache.cached.as_ref().unwrap().1, full);
    }
    assert_eq!(source, before);
}

#[test]
fn cache_miss_and_structural_change_use_full_payload_but_corruption_is_rejected() {
    let mut source = document();
    let mut cache = AreaPreviewCache::default();
    cache
        .prepare(request(&source), &source, spawn(), None)
        .unwrap();
    let wrong_hash = ContentHash::from_bytes([0; 32]);
    let response = cache
        .prepare(request(&source), &source, spawn(), Some(wrong_hash))
        .unwrap();
    assert!(
        read_area_preview(&response)
            .unwrap()
            .base_source_hash
            .is_none()
    );
    let old_hash = source.content_hash().unwrap();
    source.seed = source.seed.wrapping_add(1);
    let response = cache
        .prepare(request(&source), &source, spawn(), Some(old_hash))
        .unwrap();
    let preview = read_area_preview(&response).unwrap();
    assert!(preview.base_source_hash.is_none());
    assert_eq!(preview.terrain.chunks.len(), 64);
    cache.cached.as_mut().unwrap().1.chunks[0].normals.clear();
    assert!(
        cache
            .prepare(
                request(&source),
                &source,
                spawn(),
                Some(source.content_hash().unwrap())
            )
            .is_err()
    );
}

#[test]
fn metadata_only_projection_keeps_all_mesh_allocations_and_validates_requests() {
    let source = document();
    let mut cache = AreaPreviewCache::default();
    cache
        .prepare(request(&source), &source, spawn(), None)
        .unwrap();
    let pointers: Vec<_> = cache
        .cached
        .as_ref()
        .unwrap()
        .1
        .chunks
        .iter()
        .map(|c| c.positions_m.as_ptr())
        .collect();
    let mut target = source.clone();
    target.revision = Revision::new(40);
    let preview = read_area_preview(
        &cache
            .prepare(
                request(&target),
                &target,
                spawn(),
                Some(source.content_hash().unwrap()),
            )
            .unwrap(),
    )
    .unwrap();
    assert!(preview.terrain.chunks.is_empty());
    assert_eq!(
        pointers,
        cache
            .cached
            .as_ref()
            .unwrap()
            .1
            .chunks
            .iter()
            .map(|c| c.positions_m.as_ptr())
            .collect::<Vec<_>>()
    );
    let mut bad = request(&target);
    bad.base_revision = 1;
    let before = cache.cached.as_ref().unwrap().1.source_document_hash;
    assert!(cache.prepare(bad, &target, spawn(), Some(before)).is_err());
    assert_eq!(
        cache.cached.as_ref().unwrap().1.source_document_hash,
        before
    );
}
