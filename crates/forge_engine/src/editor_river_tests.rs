use super::*;
use forge_core::{Revision, Vec3};
use forge_project::{
    AuthoredPath, AuthoredRiver, AuthoredRoad, PathControlPoint, PathDocument, PathId,
    PathInterpolation, RiverChannelProfile, RiverChannelProfileAlgorithm, RiverDocument,
    RiverSurface, RoadDocument, RoadProfile, RoadProfileAlgorithm, RoadSurface,
};
use forge_world::{
    RiverGenerationPlan, RoadGenerationPlan, TerrainChunkCoordinate, TerrainGenerationPlan,
};

const RIVER_KEYS: [&str; 2] = ["north", "south"];

pub(super) fn request_with_rivers(
    generation: u64,
    widths_m: [Option<f32>; 2],
    full: bool,
    previous_effective: Option<&GeneratedTerrain>,
) -> (EditorTerrainRequest, GeneratedTerrain) {
    request_with_rivers_and_optional_ford(generation, widths_m, full, previous_effective, false)
}

pub(super) fn request_with_fords(
    generation: u64,
    widths_m: [Option<f32>; 2],
    full: bool,
    previous_effective: Option<&GeneratedTerrain>,
) -> (EditorTerrainRequest, GeneratedTerrain) {
    request_with_rivers_and_optional_ford(generation, widths_m, full, previous_effective, true)
}

#[allow(
    clippy::too_many_lines,
    reason = "the fixture assembles every canonical and derived river source in one readable place"
)]
fn request_with_rivers_and_optional_ford(
    generation: u64,
    widths_m: [Option<f32>; 2],
    full: bool,
    previous_effective: Option<&GeneratedTerrain>,
    include_ford_road: bool,
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
    let road_path_id = PathId::deterministic("editor-river-viewport/ford-road");
    if include_ford_road {
        definitions.push(AuthoredPath {
            id: road_path_id,
            name: "Route des gués".into(),
            closed: false,
            interpolation: PathInterpolation::CentripetalCatmullRomV1,
            control_points: vec![
                PathControlPoint::new(Vec3::new(0.0, 0.0, -90.0)),
                PathControlPoint::new(Vec3::new(0.0, 0.0, 90.0)),
            ],
        });
    }
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
        include_ford_road
            .then_some(AuthoredRoad {
                path_id: road_path_id,
                profile: RoadProfile {
                    algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
                    width_m: 4.0,
                    crown_height_m: 0.08,
                },
                surface: RoadSurface::CompactedEarthV1,
            })
            .into_iter()
            .collect(),
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
    let effective_terrain_document = result.effective_terrain.clone();
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
            paths: Some(EditorPathRequest {
                generated: paths,
                source_terrain: effective_terrain_document,
                source_paths: path_document,
            }),
            roads: Some(EditorRoadRequest {
                generated: roads,
                source_roads: road_document,
            }),
            rivers: Some(EditorRiverRequest {
                generated: result.generated,
                source_terrain: terrain_document,
                source_rivers: river_document,
            }),
            walls: None,
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

fn road_bindings(world: &World) -> BTreeMap<PathId, (Entity, Handle<Mesh>)> {
    world
        .resource::<EditorProjection>()
        .roads
        .iter()
        .map(|(path_id, presented)| (*path_id, presented.object.clone()))
        .collect()
}

fn terrain_bindings(world: &World) -> BTreeMap<TerrainChunkCoordinate, (Entity, Handle<Mesh>)> {
    world.resource::<EditorProjection>().objects.clone()
}

struct PresentedRiverSceneSnapshot {
    terrain: BTreeMap<TerrainChunkCoordinate, (Entity, Handle<Mesh>)>,
    paths: Option<GeneratedPaths>,
    roads: BTreeMap<PathId, (Entity, Handle<Mesh>)>,
    rivers: BTreeMap<PathId, (Entity, Handle<Mesh>)>,
    mesh_assets: usize,
    marker: Option<(Entity, Handle<Mesh>)>,
    status: EditorViewportStatus,
}

impl PresentedRiverSceneSnapshot {
    fn capture(world: &World, viewport: &EditorViewport) -> Self {
        Self {
            terrain: terrain_bindings(world),
            paths: world
                .resource::<path::EditorPathOverlay>()
                .canonical
                .clone(),
            roads: road_bindings(world),
            rivers: river_bindings(world),
            mesh_assets: world.resource::<Assets<Mesh>>().len(),
            marker: world.resource::<EditorProjection>().marker.clone(),
            status: viewport.0.lock().unwrap().status.clone(),
        }
    }

    fn assert_rejected(
        &self,
        world: &World,
        viewport: &EditorViewport,
        expected_error: Option<&str>,
    ) {
        assert_eq!(terrain_bindings(world), self.terrain);
        assert_eq!(
            world.resource::<path::EditorPathOverlay>().canonical,
            self.paths
        );
        assert_eq!(road_bindings(world), self.roads);
        assert_eq!(river_bindings(world), self.rivers);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), self.mesh_assets);
        let projection = world.resource::<EditorProjection>();
        assert!(projection.pending.is_none());
        assert_eq!(projection.marker, self.marker);
        let status = &viewport.0.lock().unwrap().status;
        assert!(status.error.is_some());
        if let Some(expected_error) = expected_error {
            assert!(
                status
                    .error
                    .as_deref()
                    .is_some_and(|error| error.contains(expected_error))
            );
        }
        assert_eq!(status.generation, self.status.generation);
        assert_eq!(status.source_hash, self.status.source_hash);
        assert_eq!(status.path_source_hash, self.status.path_source_hash);
        assert_eq!(status.road_source_hash, self.status.road_source_hash);
        assert_eq!(status.river_source_hash, self.status.river_source_hash);
        assert_eq!(
            status.canonical_terrain_source_hash,
            self.status.canonical_terrain_source_hash
        );
        assert_eq!(
            status.effective_terrain_source_hash,
            self.status.effective_terrain_source_hash
        );
    }
}

fn submit_rejected_river_request(
    world: &mut World,
    viewport: &EditorViewport,
    stable: &PresentedRiverSceneSnapshot,
    request: EditorTerrainRequest,
    expected_error: Option<&str>,
) {
    submit(viewport, request);
    update_editor(world);
    stable.assert_rejected(world, viewport, expected_error);
}

fn assert_forged_or_omitted_fords_are_rejected(
    world: &mut World,
    viewport: &EditorViewport,
    stable: &PresentedRiverSceneSnapshot,
    first_effective: &GeneratedTerrain,
) {
    let (mut forged_position, _) =
        request_with_fords(2, [Some(7.0), Some(8.0)], false, Some(first_effective));
    forged_position
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .road_river_intersections[0]
        .position_xz_m[0] += 1.0;
    submit_rejected_river_request(world, viewport, stable, forged_position, None);

    let (mut omitted, _) =
        request_with_fords(3, [Some(7.0), Some(8.0)], false, Some(first_effective));
    omitted
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .road_river_intersections
        .clear();
    submit_rejected_river_request(world, viewport, stable, omitted, None);

    let (mut omitted_river_and_ford, _) =
        request_with_fords(4, [Some(7.0), Some(8.0)], false, Some(first_effective));
    let omitted_river_id = omitted_river_and_ford
        .rivers
        .as_ref()
        .unwrap()
        .generated
        .road_river_intersections[0]
        .river_path_id;
    let rivers = &mut omitted_river_and_ford.rivers.as_mut().unwrap().generated;
    rivers
        .rivers
        .retain(|river| river.path_id != omitted_river_id);
    rivers
        .road_river_intersections
        .retain(|ford| ford.river_path_id != omitted_river_id);
    submit_rejected_river_request(world, viewport, stable, omitted_river_and_ford, None);

    let (mut omitted_road_and_fords, _) =
        request_with_fords(5, [Some(7.0), Some(8.0)], false, Some(first_effective));
    omitted_road_and_fords
        .roads
        .as_mut()
        .unwrap()
        .generated
        .roads
        .clear();
    omitted_road_and_fords
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .road_river_intersections
        .clear();
    submit_rejected_river_request(world, viewport, stable, omitted_road_and_fords, None);
}

fn assert_noncanonical_paths_are_rejected(
    world: &mut World,
    viewport: &EditorViewport,
    stable: &PresentedRiverSceneSnapshot,
    first_effective: &GeneratedTerrain,
) {
    let (mut omitted_unassigned_path, _) =
        request_with_rivers(6, [Some(7.0), None], false, Some(first_effective));
    let retained_river_id = omitted_unassigned_path
        .rivers
        .as_ref()
        .unwrap()
        .generated
        .rivers[0]
        .path_id;
    omitted_unassigned_path
        .paths
        .as_mut()
        .unwrap()
        .generated
        .paths
        .retain(|path| path.id == retained_river_id);
    submit_rejected_river_request(
        world,
        viewport,
        stable,
        omitted_unassigned_path,
        Some("Chemins générés différents"),
    );

    let (mut forged_path_and_river, _) =
        request_with_rivers(7, [Some(7.0), Some(8.0)], false, Some(first_effective));
    let forged_path_id = forged_path_and_river
        .rivers
        .as_ref()
        .unwrap()
        .generated
        .rivers[0]
        .path_id;
    let path = forged_path_and_river
        .paths
        .as_mut()
        .unwrap()
        .generated
        .paths
        .iter_mut()
        .find(|path| path.id == forged_path_id)
        .unwrap();
    for control in &mut path.control_points {
        control.x += 0.25;
    }
    for sample in &mut path.samples {
        sample.position_m.x += 0.25;
    }
    let river = forged_path_and_river
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .rivers
        .iter_mut()
        .find(|river| river.path_id == forged_path_id)
        .unwrap();
    for position in &mut river.positions_m {
        position[0] += 0.25;
    }
    submit_rejected_river_request(
        world,
        viewport,
        stable,
        forged_path_and_river,
        Some("Chemins générés différents"),
    );
}

fn assert_stale_ford_source_is_rejected(
    world: &mut World,
    viewport: &EditorViewport,
    stable: &PresentedRiverSceneSnapshot,
    first_effective: &GeneratedTerrain,
) {
    let (mut request, _) =
        request_with_fords(8, [Some(7.0), Some(8.0)], false, Some(first_effective));
    request
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .road_document_hash = forge_core::ContentHash::from_bytes([0x44; 32]);
    submit_rejected_river_request(world, viewport, stable, request, None);
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
fn forged_canonical_terrain_hash_is_rejected_with_the_presented_scene_intact() {
    let (mut world, viewport) = river_test_world();
    let (first, first_effective) = request_with_rivers(1, [Some(6.0), Some(8.0)], true, None);
    submit(&viewport, first);
    drain(&mut world);
    let stable = PresentedRiverSceneSnapshot::capture(&world, &viewport);

    let (mut forged, _) =
        request_with_rivers(2, [Some(7.0), Some(8.0)], false, Some(&first_effective));
    forged
        .rivers
        .as_mut()
        .unwrap()
        .generated
        .terrain_document_hash = forge_core::ContentHash::from_bytes([0x66; 32]);
    submit_rejected_river_request(&mut world, &viewport, &stable, forged, None);
}

#[test]
fn forged_omitted_or_stale_ford_payload_preserves_the_presented_scene() {
    let (mut world, viewport) = river_test_world();
    let (first, first_effective) = request_with_fords(1, [Some(6.0), Some(8.0)], true, None);
    assert_eq!(
        first
            .rivers
            .as_ref()
            .unwrap()
            .generated
            .road_river_intersections
            .len(),
        2
    );
    submit(&viewport, first);
    drain(&mut world);

    let stable = PresentedRiverSceneSnapshot::capture(&world, &viewport);
    assert_forged_or_omitted_fords_are_rejected(&mut world, &viewport, &stable, &first_effective);
    assert_noncanonical_paths_are_rejected(&mut world, &viewport, &stable, &first_effective);
    assert_stale_ford_source_is_rejected(&mut world, &viewport, &stable, &first_effective);
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
