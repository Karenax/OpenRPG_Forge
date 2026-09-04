use super::*;
use forge_core::Revision;
use forge_project::{TerrainDocument, TerrainHeightDelta};
use forge_world::TerrainGenerationPlan;

fn fixture() -> (World, EditorViewport, TerrainDocument, GeneratedTerrain) {
    let document = forge_world::embedded_visual_valley_terrain_document()
        .unwrap()
        .migrate_to_latest()
        .unwrap();
    let terrain = TerrainGenerationPlan::from_document(&document)
        .unwrap()
        .generate()
        .unwrap();
    let viewport = EditorViewport::default();
    let mut world = World::new();
    world.insert_resource(viewport.clone());
    world.insert_resource(EditorProjection::default());
    world.insert_resource(Assets::<Mesh>::default());
    world.insert_resource(Time::<Real>::default());
    {
        let mut bridge = viewport.0.lock().unwrap();
        bridge.desired_generation = 1;
        bridge.pending = Some(EditorTerrainRequest {
            generation: 1,
            project_id: document.project_id,
            area_id: document.area_id,
            revision: 1,
            terrain: terrain.clone(),
            vegetation: None,
            spawn: [0.0, 2.0, 0.0],
            base_source_hash: None,
        });
    }
    for _ in 0..32 {
        update_editor(&mut world);
    }
    (world, viewport, document, terrain)
}
fn partial(
    document: &TerrainDocument,
    terrain: &GeneratedTerrain,
    generation: u64,
) -> (EditorTerrainRequest, GeneratedTerrain) {
    let mut target = document.clone();
    target.revision = Revision::new(9);
    target.height_deltas = vec![TerrainHeightDelta::new(31, 31, 5.0)];
    let update = TerrainGenerationPlan::from_document(document)
        .unwrap()
        .refresh_cached_projection(terrain, target.clone())
        .unwrap();
    let full = TerrainGenerationPlan::from_document(&target)
        .unwrap()
        .generate()
        .unwrap();
    let mut payload = full.clone();
    payload.chunks = update.replacement_chunks().to_vec();
    (
        EditorTerrainRequest {
            generation,
            project_id: document.project_id,
            area_id: document.area_id,
            revision: 9,
            terrain: payload,
            vegetation: None,
            spawn: [1.0, 3.0, 0.0],
            base_source_hash: Some(terrain.source_document_hash),
        },
        full,
    )
}
fn submit(viewport: &EditorViewport, request: EditorTerrainRequest) {
    let mut bridge = viewport.0.lock().unwrap();
    bridge.desired_generation = request.generation;
    bridge.pending = Some(request);
}

#[test]
fn staged_partial_swap_retains_61_entities_and_meshes_and_matches_full_geometry() {
    let (mut world, viewport, document, terrain) = fixture();
    let before = world.resource::<EditorProjection>().objects.clone();
    viewport.0.lock().unwrap().input.orbit = [25.0, 10.0];
    update_editor(&mut world);
    let camera = viewport.0.lock().unwrap().status.camera;
    let (request, full) = partial(&document, &terrain, 2);
    let touched: Vec<_> = request
        .terrain
        .chunks
        .iter()
        .map(|c| c.coordinate)
        .collect();
    assert_eq!(touched.len(), 3);
    submit(&viewport, request);
    update_editor(&mut world);
    assert_eq!(world.resource::<EditorProjection>().objects, before);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 67);
    assert_eq!(viewport.0.lock().unwrap().status.revision, 1);
    for (entity, _) in before.values() {
        assert_eq!(world.get::<Visibility>(*entity), Some(&Visibility::Visible));
    }
    update_editor(&mut world);
    let projection = world.resource::<EditorProjection>();
    for (coordinate, (entity, handle)) in &projection.objects {
        if touched.contains(coordinate) {
            assert_ne!(*entity, before[coordinate].0);
            assert!(
                world
                    .resource::<Assets<Mesh>>()
                    .get(before[coordinate].1.id())
                    .is_none()
            );
        } else {
            assert_eq!(
                (*entity, handle),
                (before[coordinate].0, &before[coordinate].1)
            );
        }
        let index = usize::from(coordinate.z) * 8 + usize::from(coordinate.x);
        let (expected, _) = build_bevy_terrain_mesh(&full.chunks[index]).unwrap();
        let actual = world.resource::<Assets<Mesh>>().get(handle.id()).unwrap();
        for (attribute, expected_values) in expected.attributes() {
            assert_eq!(actual.attribute(attribute.id), Some(expected_values));
        }
        assert_eq!(actual.indices(), expected.indices());
    }
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 65);
    let status = &viewport.0.lock().unwrap().status;
    assert_eq!(
        (
            status.rebuilt_chunks,
            status.retained_chunks,
            status.mesh_uploads
        ),
        (3, 61, 67)
    );
    assert_eq!(status.revision, 9);
    assert_eq!(status.camera, camera);
    assert_eq!(status.source_hash, full.source_document_hash.to_string());
}

#[test]
fn superseded_or_failed_partial_projection_leaves_presented_meshes_intact() {
    let (mut world, viewport, document, terrain) = fixture();
    let before = world.resource::<EditorProjection>().objects.clone();
    submit(&viewport, partial(&document, &terrain, 2).0);
    update_editor(&mut world);
    viewport.0.lock().unwrap().desired_generation = 3;
    update_editor(&mut world);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 65);
    assert_eq!(world.resource::<EditorProjection>().objects, before);
    let (mut invalid, _) = partial(&document, &terrain, 3);
    invalid.terrain.chunks[2].normals.clear();
    submit(&viewport, invalid);
    update_editor(&mut world);
    update_editor(&mut world);
    assert!(viewport.0.lock().unwrap().status.error.is_some());
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 65);
    assert_eq!(world.resource::<EditorProjection>().objects, before);
    assert_eq!(
        viewport.0.lock().unwrap().status.source_hash,
        terrain.source_document_hash.to_string()
    );
}

#[test]
fn partial_base_mismatch_is_rejected_and_metadata_update_uploads_no_meshes() {
    let (mut world, viewport, document, terrain) = fixture();
    let before = world.resource::<EditorProjection>().objects.clone();
    let (mut request, _) = partial(&document, &terrain, 2);
    request.base_source_hash = Some(ContentHash::from_bytes([0; 32]));
    submit(&viewport, request);
    update_editor(&mut world);
    assert!(viewport.0.lock().unwrap().status.error.is_some());
    let (mut request, _) = partial(&document, &terrain, 3);
    let mut metadata_only = document.clone();
    metadata_only.revision = Revision::new(9);
    request.terrain.source_document_hash = metadata_only.content_hash().unwrap();
    request.terrain.chunks.clear();
    submit(&viewport, request);
    update_editor(&mut world);
    assert_eq!(world.resource::<EditorProjection>().objects, before);
    let status = &viewport.0.lock().unwrap().status;
    assert_eq!(
        (
            status.rebuilt_chunks,
            status.retained_chunks,
            status.mesh_uploads
        ),
        (0, 64, 64)
    );
    assert!(status.error.is_none());
}
