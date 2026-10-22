use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
};
use forge_world::{TERRAIN_CHUNK_COUNT, TerrainGenerationPlan, TerrainPartialUpdateRequest};

#[test]
fn authoring_projection_preserves_geometry_for_names_and_rebuilds_elevation() {
    let source = new_terrain("authoring-projection", "zone", "Initiale").unwrap();
    let plan = TerrainGenerationPlan::from_document(&source).unwrap();
    let mut cache = plan.generate().unwrap();
    let original_chunks = cache.chunks.clone();
    let mut renamed = source.clone();
    renamed.name = "Renommée".into();
    renamed.revision = source.revision.next().unwrap();
    let request = TerrainPartialUpdateRequest {
        base_revision: source.revision,
        base_document_hash: source.content_hash().unwrap(),
        target_document: renamed.clone(),
    };
    assert!(plan.apply_partial_update(&cache, request.clone()).is_err());
    let update = plan.apply_authoring_update(&cache, request).unwrap();
    assert!(update.mesh_chunks().is_empty());
    assert!(update.collider_chunks().is_empty());
    update.apply_to_cache(&mut cache).unwrap();
    assert_eq!(cache.chunks, original_chunks);
    let mut elevated = renamed.clone();
    elevated.height_sampling.base_elevation_m = 12.0;
    elevated.revision = renamed.revision.next().unwrap();
    let plan = TerrainGenerationPlan::from_document(&renamed).unwrap();
    let request = TerrainPartialUpdateRequest {
        base_revision: renamed.revision,
        base_document_hash: renamed.content_hash().unwrap(),
        target_document: elevated.clone(),
    };
    let update = plan
        .apply_authoring_update(&cache, request.clone())
        .unwrap();
    assert_eq!(update.mesh_chunks().len(), TERRAIN_CHUNK_COUNT);
    assert_eq!(update.collider_chunks().len(), TERRAIN_CHUNK_COUNT);
    update.apply_to_cache(&mut cache).unwrap();
    assert_eq!(
        cache,
        TerrainGenerationPlan::from_document(&elevated)
            .unwrap()
            .generate()
            .unwrap()
    );
    assert!(plan.apply_authoring_update(&cache, request).is_err());
}

#[test]
fn authoring_snapshots_keep_exact_hashes_and_reject_stale_or_foreign_bases() {
    let source = new_terrain("history-projection", "zone", "Source")
        .unwrap()
        .migrate_to_latest()
        .unwrap();
    let mut edited = source.clone();
    edited.revision = source.revision.next().unwrap().next().unwrap();
    edited.height_deltas = vec![forge_project::TerrainHeightDelta::new(32, 32, 5.0)];
    let source_plan = TerrainGenerationPlan::from_document(&source).unwrap();
    let mut cache = source_plan.generate().unwrap();
    let mut live = source.clone();
    for target in [&edited, &source, &edited] {
        let plan = TerrainGenerationPlan::from_document(&live).unwrap();
        let request = TerrainPartialUpdateRequest {
            base_revision: live.revision,
            base_document_hash: plan.document_hash(),
            target_document: target.clone(),
        };
        // Ordinary edits keep their next-revision contract.
        assert!(
            plan.apply_authoring_update(&cache, request.clone())
                .is_err()
        );
        assert!(plan.apply_partial_update(&cache, request.clone()).is_err());
        let mut stale = request.clone();
        stale.base_revision = live.revision.next().unwrap();
        assert!(plan.apply_authoring_snapshot(&cache, stale).is_err());
        let mut stale = request.clone();
        stale.base_document_hash = forge_core::ContentHash::from_bytes([0; 32]);
        assert!(plan.apply_authoring_snapshot(&cache, stale).is_err());
        let mut foreign = request.clone();
        foreign.target_document.area_id = forge_core::AreaId::deterministic("other-area");
        assert!(plan.apply_authoring_snapshot(&cache, foreign).is_err());
        let before = cache.clone();
        let update = plan
            .apply_authoring_snapshot(&cache, request.clone())
            .unwrap();
        assert_eq!(cache, before, "preparation is read-only");
        assert_eq!(update.target_document(), target);
        assert_eq!(
            update.target_document_hash(),
            target.content_hash().unwrap()
        );
        update.apply_to_cache(&mut cache).unwrap();
        assert_eq!(
            cache,
            TerrainGenerationPlan::from_document(target)
                .unwrap()
                .generate()
                .unwrap()
        );
        assert!(plan.apply_authoring_snapshot(&cache, request).is_err());
        live = target.clone();
    }
    let plan = TerrainGenerationPlan::from_document(&live).unwrap();
    let mut renamed = live.clone();
    renamed.name = "Même révision, autre snapshot".into();
    let request = TerrainPartialUpdateRequest {
        base_revision: live.revision,
        base_document_hash: plan.document_hash(),
        target_document: renamed.clone(),
    };
    assert!(
        plan.apply_authoring_update(&cache, request.clone())
            .is_err()
    );
    plan.apply_authoring_snapshot(&cache, request)
        .unwrap()
        .apply_to_cache(&mut cache)
        .unwrap();
    assert_eq!(cache.source_document_hash, renamed.content_hash().unwrap());
}

#[test]
fn new_editor_zone_and_reopened_edit_are_renderable_by_the_real_generator() {
    let document = new_terrain("authoring-test", "first-zone", "Zone neuve").unwrap();
    let plan = TerrainGenerationPlan::from_document(&document).unwrap();
    assert_eq!(
        plan.height_at(0.0, 0.0).unwrap().to_bits(),
        0.0_f32.to_bits()
    );
    let initial = plan.generate().unwrap();
    assert_eq!(initial.chunks.len(), TERRAIN_CHUNK_COUNT);
    let mut session = TerrainEditSession::new(document).unwrap();
    let request = TerrainEditRequest {
        command_id: CommandId::deterministic("raise-editor-terrain"),
        expected_revision: session.document().revision,
        expected_hash: session.document().content_hash().unwrap(),
        action: TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 8.0 }),
    };
    session.execute(&request).unwrap();
    let reopened =
        TerrainEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let plan = TerrainGenerationPlan::from_document(reopened.document()).unwrap();
    assert_eq!(
        plan.document_hash(),
        reopened.document().content_hash().unwrap()
    );
    for (x, z) in [(-256.0, -256.0), (0.0, 0.0), (256.0, 256.0)] {
        assert_eq!(plan.height_at(x, z).unwrap().to_bits(), 8.0_f32.to_bits());
    }
    assert_eq!(plan.generate().unwrap().chunks.len(), initial.chunks.len());
}
