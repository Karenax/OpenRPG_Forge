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
