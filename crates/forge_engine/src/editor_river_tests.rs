use super::*;
use forge_core::{Revision, Vec3};
use forge_project::{
    AuthoredPath, AuthoredRiver, PathControlPoint, PathDocument, PathId, PathInterpolation,
    RiverChannelProfile, RiverChannelProfileAlgorithm, RiverDocument, RiverSurface, RoadDocument,
};
use forge_world::{
    RiverGenerationPlan, RoadGenerationPlan, TerrainChunkCoordinate, TerrainGenerationPlan,
};

const RIVER_KEYS: [&str; 2] = ["north", "south"];

#[allow(
    clippy::too_many_lines,
    reason = "the fixture assembles every canonical and derived river source in one readable place"
)]
fn request_with_rivers(
    generation: u64,
    widths_m: [Option<f32>; 2],
    full: bool,
    previous_effective: Option<&GeneratedTerrain>,
) -> (EditorTerrainRequest, GeneratedTerrain) {
    let terrain_document = forge_world::embedded_visual_valley_terrain_document().unwrap();
    let mut definitions = RIVER_KEYS
        .into_iter()
        .enumerate()
        .map(|(index, key)| {
            let z_m = if index == 0 { -40.0 } else { 40.0 };
            AuthoredPath {
                id: PathId::deterministic(format!("editor-river-viewport/{key}")),
                name: format!("Cours d’eau {key}"),
                closed: false,
                interpolation: PathInterpolation::CentripetalCatmullRomV1,
                control_points: vec![
                    PathControlPoint::new(Vec3::new(-60.0, 0.0, z_m)),
                    PathControlPoint::new(Vec3::new(60.0, 0.0, z_m)),
                ],
            }
        })
        .collect::<Vec<_>>();
    definitions.sort_by_key(|path| path.id);
    let path_document = PathDocument::new(
        terrain_document.project_id,
        terrain_document.area_id,
        Revision::new(generation),
        "Chemins de rivière",
        definitions.clone(),
    );
    let road_document = RoadDocument::new(
        terrain_document.project_id,
        terrain_document.area_id,
        Revision::new(generation),
        "Routes",
        vec![],
    );
    let mut authored_rivers = RIVER_KEYS
        .into_iter()
        .zip(widths_m)
        .filter_map(|(key, width_m)| {
            width_m.map(|width_m| AuthoredRiver {
                path_id: PathId::deterministic(format!("editor-river-viewport/{key}")),
                channel: RiverChannelProfile {
                    algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
                    surface_width_m: width_m,
                    bed_depth_m: 1.5,
                    bank_blend_width_m: 4.0,
                },
                surface: RiverSurface::ForgeWaterV1,
            })
        })
        .collect::<Vec<_>>();
    authored_rivers.sort_by_key(|river| river.path_id);
    let river_document = RiverDocument::new(
        terrain_document.project_id,
        terrain_document.area_id,
        Revision::new(generation),
        "Rivières",
        authored_rivers,
    );

    let result = RiverGenerationPlan::from_documents(
        &terrain_document,
        &path_document,
        &road_document,
        &river_document,
    )
    .unwrap()
    .generate()
    .unwrap();
    let paths =
        forge_world::PathGenerationPlan::from_documents(&result.effective_terrain, &path_document)
            .unwrap()
            .generate()
            .unwrap();
    let roads = RoadGenerationPlan::from_documents(
        &result.effective_terrain,
        &path_document,
        &road_document,
    )
    .unwrap()
    .generate()
    .unwrap();
    let mut terrain = TerrainGenerationPlan::from_document(&result.effective_terrain)
        .unwrap()
        .generate()
        .unwrap();
    let full_effective = terrain.clone();
    if !full {
        let previous =
            previous_effective.expect("a partial river request needs its presented base");
        terrain.chunks.retain(|chunk| {
            previous
                .chunks
                .iter()
                .find(|old| old.coordinate == chunk.coordinate)
                .is_none_or(|old| old != chunk)
        });
    }
    let base_source_hash = (!full).then(|| {
        previous_effective
            .expect("a partial river request needs its presented base")
            .source_document_hash
    });
    (
        EditorTerrainRequest {
            generation,
            project_id: terrain_document.project_id,
            area_id: terrain_document.area_id,
            revision: generation,
            terrain,
            vegetation: None,
            paths: Some(EditorPathRequest { generated: paths }),
            roads: Some(EditorRoadRequest { generated: roads }),
            rivers: Some(EditorRiverRequest {
                generated: result.generated,
            }),
            spawn: [0.0, 2.0, 0.0],
            base_source_hash,
        },
        full_effective,
    )
}

fn river_test_world() -> (World, EditorViewport) {
    let viewport = EditorViewport::default();
    let mut materials = Assets::<StandardMaterial>::default();
    let river_material = materials.add(river::procedural_editor_river_material());
    let projection = EditorProjection {
        river_material,
        ..default()
    };
    let mut world = World::new();
    world.insert_resource(viewport.clone());
    world.insert_resource(projection);
    world.insert_resource(path::EditorPathOverlay::default());
    world.insert_resource(Assets::<Mesh>::default());
    world.insert_resource(materials);
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
    panic!("river projection did not finish within its bounded mesh count");
}

fn river_bindings(world: &World) -> BTreeMap<PathId, (Entity, Handle<Mesh>)> {
    world
        .resource::<EditorProjection>()
        .rivers
        .iter()
        .map(|(path_id, presented)| (*path_id, presented.object.clone()))
        .collect()
}

fn terrain_bindings(world: &World) -> BTreeMap<TerrainChunkCoordinate, (Entity, Handle<Mesh>)> {
    world.resource::<EditorProjection>().objects.clone()
}

#[test]
fn river_update_stages_derived_terrain_and_water_then_swaps_them_together() {
    let (mut world, viewport) = river_test_world();
    let (first, first_effective) = request_with_rivers(1, [Some(6.0), Some(8.0)], true, None);
    let canonical_hash = first
        .rivers
        .as_ref()
        .unwrap()
        .generated
        .terrain_document_hash;
    submit(&viewport, first);
    drain(&mut world);

    let before_rivers = river_bindings(&world);
    let before_terrain = terrain_bindings(&world);
    let before_assets = world.resource::<Assets<Mesh>>().len();
    let retained_id = PathId::deterministic("editor-river-viewport/south");
    let changed_id = PathId::deterministic("editor-river-viewport/north");
    let (second, _) =
        request_with_rivers(2, [Some(10.0), Some(8.0)], false, Some(&first_effective));
    let effective_hash = second.terrain.source_document_hash;
    let changed_chunks = second
        .terrain
        .chunks
        .iter()
        .map(|chunk| chunk.coordinate)
        .collect::<Vec<_>>();
    assert!(!changed_chunks.is_empty());
    submit(&viewport, second);
    update_editor(&mut world);

    assert_eq!(river_bindings(&world), before_rivers);
    assert_eq!(terrain_bindings(&world), before_terrain);
    assert_eq!(viewport.0.lock().unwrap().status.generation, 1);
    assert!(world.resource::<EditorProjection>().pending.is_some());

    drain(&mut world);
    let after_rivers = river_bindings(&world);
    let after_terrain = terrain_bindings(&world);
    assert_ne!(after_rivers[&changed_id], before_rivers[&changed_id]);
    assert_eq!(after_rivers[&retained_id], before_rivers[&retained_id]);
    for (coordinate, binding) in &after_terrain {
        if changed_chunks.contains(coordinate) {
            assert_ne!(*binding, before_terrain[coordinate]);
        } else {
            assert_eq!(*binding, before_terrain[coordinate]);
        }
    }
    assert_eq!(world.resource::<Assets<Mesh>>().len(), before_assets);
    let projection = world.resource::<EditorProjection>();
    for presented in projection.rivers.values() {
        assert_eq!(
            world.get::<Visibility>(presented.object.0),
            Some(&Visibility::Visible)
        );
        assert_eq!(
            world
                .get::<MeshMaterial3d<StandardMaterial>>(presented.object.0)
                .unwrap()
                .0
                .id(),
            projection.river_material.id()
        );
    }
    let status = &viewport.0.lock().unwrap().status;
    assert_eq!(status.source_hash, status.effective_terrain_source_hash);
    assert_eq!(
        status.canonical_terrain_source_hash,
        canonical_hash.to_string()
    );
    assert_eq!(status.source_hash, effective_hash.to_string());
    assert_ne!(status.canonical_terrain_source_hash, status.source_hash);
    assert_eq!(status.river_count, 2);
    assert_eq!(status.river_rebuilt_meshes, 1);
    assert_eq!(status.river_retained_meshes, 1);
}

#[test]
fn river_removal_and_rejection_release_only_owned_objects_without_leaks() {
    let (mut world, viewport) = river_test_world();
    let (first, first_effective) = request_with_rivers(1, [Some(6.0), Some(8.0)], true, None);
    submit(&viewport, first);
    drain(&mut world);
    let before = river_bindings(&world);
    let before_assets = world.resource::<Assets<Mesh>>().len();
    let removed_id = PathId::deterministic("editor-river-viewport/south");
    let retained_id = PathId::deterministic("editor-river-viewport/north");
    let removed_entity = before[&removed_id].0;

    let (second, second_effective) =
        request_with_rivers(2, [Some(6.0), None], false, Some(&first_effective));
    submit(&viewport, second);
    drain(&mut world);
    let after = river_bindings(&world);
    assert_eq!(after.len(), 1);
    assert_eq!(after[&retained_id], before[&retained_id]);
    assert!(!after.contains_key(&removed_id));
    assert!(world.get_entity(removed_entity).is_err());
    assert_eq!(world.resource::<Assets<Mesh>>().len(), before_assets - 1);

    let stable_assets = world.resource::<Assets<Mesh>>().len();
    let stable_terrain = terrain_bindings(&world);
    let stable_rivers = river_bindings(&world);
    let stable_status = viewport.0.lock().unwrap().status.clone();
    let (mut invalid, _) =
        request_with_rivers(3, [Some(12.0), None], false, Some(&second_effective));
    invalid
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .effective_terrain_document_hash = forge_core::ContentHash::from_bytes([0x55; 32]);
    submit(&viewport, invalid);
    update_editor(&mut world);

    assert_eq!(terrain_bindings(&world), stable_terrain);
    assert_eq!(river_bindings(&world), stable_rivers);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), stable_assets);
    let status = &viewport.0.lock().unwrap().status;
    assert!(status.error.is_some());
    assert_eq!(status.generation, stable_status.generation);
    assert_eq!(status.river_source_hash, stable_status.river_source_hash);
}

#[test]
fn superseded_river_staging_is_discarded_and_latest_request_wins() {
    let (mut world, viewport) = river_test_world();
    let (first, first_effective) = request_with_rivers(1, [Some(6.0), Some(8.0)], true, None);
    submit(&viewport, first);
    drain(&mut world);
    let stable_terrain = terrain_bindings(&world);
    let stable_rivers = river_bindings(&world);
    let stable_assets = world.resource::<Assets<Mesh>>().len();

    let (superseded, _) =
        request_with_rivers(2, [Some(14.0), Some(16.0)], false, Some(&first_effective));
    submit(&viewport, superseded);
    update_editor(&mut world);
    assert!(world.resource::<Assets<Mesh>>().len() > stable_assets);
    viewport.0.lock().unwrap().desired_generation = 3;
    update_editor(&mut world);
    assert_eq!(terrain_bindings(&world), stable_terrain);
    assert_eq!(river_bindings(&world), stable_rivers);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), stable_assets);

    let (latest, _) =
        request_with_rivers(3, [Some(10.0), Some(8.0)], false, Some(&first_effective));
    submit(&viewport, latest);
    drain(&mut world);
    assert_eq!(viewport.0.lock().unwrap().status.generation, 3);
    assert_ne!(river_bindings(&world), stable_rivers);
    assert_eq!(world.resource::<Assets<Mesh>>().len(), stable_assets);
}
