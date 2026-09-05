use super::*;
use forge_core::{Revision, Vec3};
use forge_project::{
    AuthoredPath, AuthoredWall, PathControlPoint, PathInterpolation, WallDocument, WallProfile,
    WallProfileAlgorithm, WallSurface,
};
use forge_world::{PathGenerationPlan, TerrainGenerationPlan, WallGenerationPlan};

fn request_with_walls(generation: u64, heights_m: &[f32], full: bool) -> EditorTerrainRequest {
    let terrain_document = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut definitions = heights_m
        .iter()
        .copied()
        .enumerate()
        .map(|(index, height_m)| {
            #[allow(clippy::cast_precision_loss)]
            let x_m = -48.0 + index as f32 * 36.0;
            (
                AuthoredPath {
                    id: PathId::deterministic(format!("editor-wall-viewport/path-{index:02}")),
                    name: format!("Mur {index}"),
                    closed: false,
                    interpolation: PathInterpolation::CentripetalCatmullRomV1,
                    control_points: vec![
                        PathControlPoint::new(Vec3::new(x_m, 0.0, -30.0)),
                        PathControlPoint::new(Vec3::new(x_m + 6.0, 0.0, 30.0)),
                    ],
                },
                height_m,
            )
        })
        .collect::<Vec<_>>();
    definitions.sort_by_key(|(path, _)| path.id);
    let path_document = forge_project::PathDocument::new(
        terrain_document.project_id,
        terrain_document.area_id,
        Revision::INITIAL,
        "Chemins de murs",
        definitions.iter().map(|(path, _)| path.clone()).collect(),
    );
    let wall_document = WallDocument::new(
        terrain_document.project_id,
        terrain_document.area_id,
        Revision::new(generation),
        "Murs de la zone",
        definitions
            .iter()
            .map(|(path, height_m)| AuthoredWall {
                path_id: path.id,
                profile: WallProfile {
                    algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                    height_m: *height_m,
                    thickness_m: 0.3,
                },
                surface: WallSurface::ForgeStoneV1,
            })
            .collect(),
    );
    let paths = PathGenerationPlan::from_documents(&terrain_document, &path_document)
        .unwrap()
        .generate()
        .unwrap();
    let walls =
        WallGenerationPlan::from_documents(&terrain_document, &path_document, &wall_document)
            .unwrap()
            .generate()
            .unwrap();
    let mut terrain = TerrainGenerationPlan::from_document(&terrain_document)
        .unwrap()
        .generate()
        .unwrap();
    let base_source_hash = (!full).then_some(terrain.source_document_hash);
    if !full {
        terrain.chunks.clear();
    }
    EditorTerrainRequest {
        generation,
        project_id: terrain_document.project_id,
        area_id: terrain_document.area_id,
        revision: generation,
        terrain,
        vegetation: None,
        paths: Some(EditorPathRequest {
            generated: paths,
            source_terrain: terrain_document,
            source_paths: path_document,
        }),
        roads: None,
        rivers: None,
        walls: Some(EditorWallRequest {
            generated: walls,
            source_walls: wall_document,
        }),
        spawn: [0.0, 2.0, 0.0],
        base_source_hash,
    }
}

fn wall_test_world() -> (World, EditorViewport) {
    let viewport = EditorViewport::default();
    let mut world = World::new();
    world.insert_resource(viewport.clone());
    world.insert_resource(EditorProjection::default());
    world.insert_resource(path::EditorPathOverlay::default());
    world.insert_resource(Assets::<Mesh>::default());
    world.insert_resource(Time::<Real>::default());
    (world, viewport)
}

fn submit(viewport: &EditorViewport, request: EditorTerrainRequest) {
    let mut bridge = viewport.0.lock().unwrap();
    bridge.desired_generation = request.generation;
    bridge.pending = Some(request);
}

fn drain(world: &mut World) {
    for _ in 0..128 {
        update_editor(world);
        if world.resource::<EditorProjection>().pending.is_none() {
            return;
        }
    }
    panic!("editor wall projection did not finish within its bounded mesh count");
}

fn wall_bindings(world: &World) -> BTreeMap<PathId, (Entity, Handle<Mesh>)> {
    world
        .resource::<EditorProjection>()
        .walls
        .iter()
        .map(|(path_id, presented)| (*path_id, presented.object.clone()))
        .collect()
}

fn attach_wall(request: &mut EditorTerrainRequest, path_id: PathId) {
    let paths = request.paths.as_ref().unwrap();
    let source_walls = WallDocument::new(
        request.project_id,
        request.area_id,
        Revision::new(request.revision),
        "Murs de la zone",
        vec![AuthoredWall {
            path_id,
            profile: WallProfile {
                algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
                height_m: 1.5,
                thickness_m: 0.3,
            },
            surface: WallSurface::ForgeStoneV1,
        }],
    );
    let generated = WallGenerationPlan::from_documents(
        &paths.source_terrain,
        &paths.source_paths,
        &source_walls,
    )
    .unwrap()
    .generate()
    .unwrap();
    request.walls = Some(EditorWallRequest {
        generated,
        source_walls,
    });
}

fn assert_rejected_request_preserves_presented_walls(request: EditorTerrainRequest) {
    let (mut world, viewport) = wall_test_world();
    submit(&viewport, request_with_walls(1, &[1.5, 2.0], true));
    drain(&mut world);
    let before = wall_bindings(&world);
    let before_assets = world.resource::<Assets<Mesh>>().len();
    let before_uploads = world.resource::<EditorProjection>().wall_mesh_uploads;

    submit(&viewport, request);
    update_editor(&mut world);

    let projection = world.resource::<EditorProjection>();
    assert_eq!(wall_bindings(&world), before);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), before_assets);
    assert_eq!(projection.wall_mesh_uploads, before_uploads);
    assert!(projection.roads.is_empty());
    assert!(projection.rivers.is_empty());
    assert!(projection.pending.is_none());
    let status = &viewport.0.lock().unwrap().status;
    assert_eq!(status.generation, 1);
    assert!(status.error.is_some());
}

#[test]
fn wall_only_update_is_atomic_and_reuses_unchanged_mesh_handles() {
    let (mut world, viewport) = wall_test_world();
    submit(&viewport, request_with_walls(1, &[1.5, 2.0, 2.5], true));
    drain(&mut world);

    let before = wall_bindings(&world);
    let before_asset_count = world.resource::<Assets<Mesh>>().len();
    let (terrain_uploads, wall_uploads) = {
        let projection = world.resource::<EditorProjection>();
        (projection.mesh_uploads, projection.wall_mesh_uploads)
    };
    let changed_id = PathId::deterministic("editor-wall-viewport/path-01");
    let retained_ids = [
        PathId::deterministic("editor-wall-viewport/path-00"),
        PathId::deterministic("editor-wall-viewport/path-02"),
    ];

    submit(&viewport, request_with_walls(2, &[1.5, 3.0, 2.5], false));
    drain(&mut world);

    let after = wall_bindings(&world);
    assert_ne!(after[&changed_id], before[&changed_id]);
    for path_id in retained_ids {
        assert_eq!(after[&path_id], before[&path_id]);
    }
    let projection = world.resource::<EditorProjection>();
    assert_eq!(projection.mesh_uploads, terrain_uploads);
    assert_eq!(projection.wall_mesh_uploads, wall_uploads + 1);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), before_asset_count);
    let status = &viewport.0.lock().unwrap().status;
    assert_eq!(status.wall_count, 3);
    assert_eq!(status.wall_rebuilt_meshes, 1);
    assert_eq!(status.wall_retained_meshes, 2);
    assert_eq!(status.rebuilt_chunks, 0);
}

#[test]
fn coherently_translated_wall_payload_preserves_the_presented_scene() {
    let (mut world, viewport) = wall_test_world();
    submit(&viewport, request_with_walls(1, &[1.5, 2.0], true));
    drain(&mut world);
    let before = wall_bindings(&world);
    let before_uploads = world.resource::<EditorProjection>().wall_mesh_uploads;

    let mut forged = request_with_walls(2, &[1.5, 3.0], false);
    let walls = forged.walls.as_mut().unwrap();
    for position in &mut walls.generated.walls[0].positions_m {
        position[1] += 0.25;
    }
    submit(&viewport, forged);
    update_editor(&mut world);

    assert_eq!(wall_bindings(&world), before);
    assert_eq!(
        world.resource::<EditorProjection>().wall_mesh_uploads,
        before_uploads
    );
    assert!(viewport.0.lock().unwrap().status.error.is_some());
    assert!(world.resource::<EditorProjection>().pending.is_none());
}

#[test]
fn wall_and_road_cannot_share_a_path_at_the_viewport_boundary() {
    let (mut conflicting, _) =
        super::river_tests::request_with_fords(2, [Some(6.0), None], true, None);
    let shared_path_id = conflicting.roads.as_ref().unwrap().source_roads.roads[0].path_id;
    attach_wall(&mut conflicting, shared_path_id);
    assert_rejected_request_preserves_presented_walls(conflicting);
}

#[test]
fn wall_and_river_cannot_share_a_path_at_the_viewport_boundary() {
    let (mut conflicting, _) =
        super::river_tests::request_with_rivers(2, [Some(6.0), None], true, None);
    let shared_path_id = conflicting.rivers.as_ref().unwrap().source_rivers.rivers[0].path_id;
    attach_wall(&mut conflicting, shared_path_id);
    assert_rejected_request_preserves_presented_walls(conflicting);
}
