use super::*;
use crate::{
    PathCommandError, PathCreateCommand, PathRedrawCommand, PathSample, preview_create_path,
    preview_redraw_path,
};
use forge_core::Vec3;
use forge_project::{NewProjectCommand, WorkspaceError};
use serde_json::json;

fn project_id() -> ProjectId {
    ProjectId::deterministic("road-world-test/project")
}

fn module_id() -> ModuleId {
    ModuleId::deterministic("road-world-test/module")
}

fn area_id() -> AreaId {
    AreaId::deterministic("road-world-test/area")
}

fn path_id(key: &str) -> PathId {
    PathId::deterministic(format!("road-world-test/path/{key}"))
}

fn workspace() -> WorkspaceDocument {
    WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("road-world-test/create-workspace"),
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        project_name: "Projet routes".to_owned(),
        module_name: "Module routes".to_owned(),
        area_name: "Zone routes".to_owned(),
        seed: 0x22_22,
        spawn: Vec3::ZERO,
    })
    .unwrap()
    .document
}

fn add_path(
    workspace: &WorkspaceDocument,
    id: PathId,
    name: &str,
    trace_xz_m: Vec<[f32; 2]>,
) -> WorkspaceDocument {
    preview_create_path(
        workspace,
        PathCreateCommand {
            command_id: CommandId::deterministic(format!("road-world-test/create/{id}")),
            base_revision: workspace.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id: id,
            name: name.to_owned(),
            trace_xz_m,
        },
    )
    .unwrap()
    .edit
    .document
}

fn road(path_id: PathId, width_m: f32, crown_height_m: f32) -> AuthoredRoad {
    AuthoredRoad {
        path_id,
        profile: RoadProfile {
            algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
            width_m,
            crown_height_m,
        },
        surface: RoadSurface::CompactedEarthV1,
    }
}

fn command(
    workspace: &WorkspaceDocument,
    authored_road: AuthoredRoad,
    key: &str,
) -> RoadUpsertCommand {
    RoadUpsertCommand {
        command_id: CommandId::deterministic(format!("road-world-test/upsert/{key}")),
        base_revision: workspace.revision,
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        road: authored_road,
    }
}

fn workspace_with_road(id: PathId, trace_xz_m: Vec<[f32; 2]>) -> (WorkspaceDocument, AuthoredRoad) {
    let workspace = add_path(&workspace(), id, "Chemin routier", trace_xz_m);
    let authored_road = road(id, 4.0, 0.08);
    let document = preview_upsert_road(&workspace, command(&workspace, authored_road, "initial"))
        .unwrap()
        .edit
        .document;
    (document, authored_road)
}

fn assert_mesh_contract(
    plan: &RoadGenerationPlan,
    mesh: &GeneratedRoad,
    authored_road: AuthoredRoad,
) {
    assert_eq!(mesh.path_id, authored_road.path_id);
    assert_eq!(mesh.profile, authored_road.profile);
    assert_eq!(mesh.surface, authored_road.surface);
    assert_eq!(mesh.positions_m.len(), mesh.normals.len());
    assert_eq!(mesh.positions_m.len(), mesh.uvs.len());
    assert_eq!(mesh.positions_m.len(), mesh.longitudinal_sample_count() * 3);
    assert_eq!(
        mesh.indices.len(),
        (mesh.longitudinal_sample_count() - 1) * 12
    );
    assert_eq!(
        mesh.triangle_count(),
        (mesh.longitudinal_sample_count() - 1) * 4
    );
    assert_eq!(&mesh.indices[..12], &[0, 3, 1, 1, 3, 4, 1, 4, 2, 2, 4, 5]);
    assert!(mesh.longitudinal_sample_count() > 2);

    let (position_rows, position_remainder) = mesh.positions_m.as_chunks::<3>();
    assert!(position_remainder.is_empty());
    for (row_index, positions) in position_rows.iter().enumerate() {
        for (cross_section_index, position) in positions.iter().enumerate() {
            let terrain = plan.terrain().surface_at(position[0], position[2]).unwrap();
            let crown = if cross_section_index == 1 {
                authored_road.profile.crown_height_m
            } else {
                0.0
            };
            assert_eq!(
                position[1].to_bits(),
                (terrain.height_m + ROAD_MESH_CLEARANCE_M + crown).to_bits()
            );
            let expected_u = [0.0_f32, 0.5, 1.0][cross_section_index];
            assert_eq!(
                mesh.uvs[row_index * 3 + cross_section_index][0].to_bits(),
                expected_u.to_bits()
            );
        }
        assert_eq!(
            mesh.uvs[row_index * 3][1].to_bits(),
            mesh.uvs[row_index * 3 + 1][1].to_bits()
        );
        assert_eq!(
            mesh.uvs[row_index * 3][1].to_bits(),
            mesh.uvs[row_index * 3 + 2][1].to_bits()
        );
    }
    for centers in position_rows.windows(2) {
        let first = centers[0][1];
        let second = centers[1][1];
        let chord = f64::from(second[0] - first[0]).hypot(f64::from(second[2] - first[2]));
        assert!(chord > 0.0);
        assert!(chord <= f64::from(ROAD_MESH_MAX_LONGITUDINAL_CHORD_M));
    }
    assert!(
        mesh.normals.iter().all(|normal| {
            normal.iter().all(|component| component.is_finite()) && normal[1] > 0.0
        })
    );
}

#[test]
fn straight_road_is_three_points_four_triangles_and_exactly_terrain_draped() {
    let id = path_id("straight");
    let source = add_path(
        &workspace(),
        id,
        "Ligne droite",
        vec![[-4.0, 0.0], [4.0, 0.0]],
    );
    let source_hash = source.content_hash().unwrap();
    let authored_road = road(id, 4.0, 0.08);
    let result = preview_upsert_road(&source, command(&source, authored_road, "straight")).unwrap();

    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert_eq!(result.previous_road, None);
    assert_eq!(result.road, authored_road);
    assert_eq!(
        result.edit.receipt.new_revision,
        source.revision.next().unwrap()
    );
    assert_eq!(*result.edit.receipt.inverse_snapshot, source);

    let area = result.edit.document.area(module_id(), area_id()).unwrap();
    let plan = RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads).unwrap();
    let generated = plan.generate().unwrap();
    let replay = RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads)
        .unwrap()
        .generate()
        .unwrap();
    assert_eq!(generated, replay);
    assert_eq!(
        generated.content_hash().unwrap(),
        replay.content_hash().unwrap()
    );
    assert_eq!(generated.generation_version, ROAD_GENERATION_VERSION);
    assert_eq!(
        generated.terrain_document_hash,
        area.terrain.content_hash().unwrap()
    );
    assert_eq!(
        generated.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(
        generated.road_document_hash,
        area.roads.content_hash().unwrap()
    );

    assert_mesh_contract(&plan, &generated.roads[0], authored_road);
}

#[test]
fn generated_contract_is_strict_and_roads_are_canonical() {
    let first_id = path_id("canonical-a");
    let second_id = path_id("canonical-b");
    let mut source = workspace();
    source = add_path(
        &source,
        first_id,
        "Premier",
        vec![[-8.0, -6.0], [8.0, -6.0]],
    );
    source = add_path(&source, second_id, "Second", vec![[-8.0, 6.0], [8.0, 6.0]]);

    let mut insertion = [first_id, second_id];
    insertion.sort();
    insertion.reverse();
    for id in insertion {
        let result = preview_upsert_road(
            &source,
            command(&source, road(id, 3.0, 0.05), &id.to_string()),
        )
        .unwrap();
        source = result.edit.document;
    }
    let area = source.area(module_id(), area_id()).unwrap();
    let generated = RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads)
        .unwrap()
        .generate()
        .unwrap();
    let ids = generated
        .roads
        .iter()
        .map(|road| road.path_id)
        .collect::<Vec<_>>();
    assert!(ids.windows(2).all(|pair| pair[0] < pair[1]));

    let mut value = serde_json::to_value(&generated).unwrap();
    value["unknown"] = json!(true);
    assert!(serde_json::from_value::<GeneratedRoads>(value).is_err());
    let mut command_value =
        serde_json::to_value(command(&source, road(first_id, 5.0, 0.1), "strict")).unwrap();
    command_value["preview_only"] = json!(true);
    assert!(serde_json::from_value::<RoadUpsertCommand>(command_value).is_err());
}

#[test]
fn adaptive_mesh_resampling_bounds_chords_without_changing_the_path() {
    let id = path_id("adaptive");
    let generated_path = GeneratedPath {
        id,
        name: "Source espacee".to_owned(),
        closed: false,
        control_points: vec![Vec3::new(0.0, 0.0, 0.0), Vec3::new(5.5, 0.0, 0.0)],
        samples: vec![
            PathSample {
                position_m: Vec3::new(0.0, 0.0, 0.0),
                source_segment_index: 0,
                segment_fraction: 0.0,
            },
            PathSample {
                position_m: Vec3::new(5.5, 0.0, 0.0),
                source_segment_index: 0,
                segment_fraction: 1.0,
            },
        ],
    };
    let source = generated_path.clone();
    let rows = adaptive_centerline(&generated_path, 8).unwrap();

    assert_eq!(generated_path, source);
    assert_eq!(rows.len(), 4);
    assert!(rows.windows(2).all(|pair| {
        let chord = (pair[1].xz_m[0] - pair[0].xz_m[0]).hypot(pair[1].xz_m[1] - pair[0].xz_m[1]);
        chord > 0.0 && chord <= f64::from(ROAD_MESH_MAX_LONGITUDINAL_CHORD_M)
    }));
    assert!(matches!(
        adaptive_centerline(&generated_path, 3),
        Err(RoadGenerationError::LongitudinalSampleBudgetExceeded { .. })
    ));
}

#[test]
fn upsert_update_noop_and_context_rejections_are_atomic() {
    let id = path_id("command");
    let (source, initial) = workspace_with_road(id, vec![[-6.0, 2.0], [6.0, 2.0]]);
    let source_hash = source.content_hash().unwrap();

    assert!(matches!(
        preview_upsert_road(&source, command(&source, initial, "noop")),
        Err(RoadCommandError::Workspace(WorkspaceError::NoChange))
    ));

    let mut stale = command(&source, road(id, 5.0, 0.1), "stale");
    stale.base_revision = source.revision.next().unwrap();
    assert!(matches!(
        preview_upsert_road(&source, stale),
        Err(RoadCommandError::Workspace(WorkspaceError::StaleRevision))
    ));

    let mut wrong_project = command(&source, road(id, 5.0, 0.1), "wrong-project");
    wrong_project.project_id = ProjectId::deterministic("road-world-test/wrong-project");
    assert!(matches!(
        preview_upsert_road(&source, wrong_project),
        Err(RoadCommandError::WrongProject)
    ));

    let missing_id = path_id("missing");
    assert!(matches!(
        preview_upsert_road(
            &source,
            command(&source, road(missing_id, 4.0, 0.08), "missing")
        ),
        Err(RoadCommandError::Workspace(WorkspaceError::MissingRoadPath(id))) if id == missing_id
    ));

    let mut invalid = road(id, 4.0, 0.08);
    invalid.profile.width_m = f32::NAN;
    assert!(matches!(
        preview_upsert_road(&source, command(&source, invalid, "invalid")),
        Err(RoadCommandError::InvalidRoad(_))
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);

    let replacement = road(id, 5.0, 0.1);
    let updated = preview_upsert_road(&source, command(&source, replacement, "replace")).unwrap();
    assert_eq!(updated.previous_road, Some(initial));
    assert_eq!(
        updated.edit.receipt.new_revision,
        source.revision.next().unwrap()
    );
    assert_eq!(*updated.edit.receipt.inverse_snapshot, source);
    let before_area = source.area(module_id(), area_id()).unwrap();
    let after_area = updated.edit.document.area(module_id(), area_id()).unwrap();
    assert_eq!(before_area.terrain, after_area.terrain);
    assert_eq!(before_area.paths, after_area.paths);
    assert_ne!(before_area.roads, after_area.roads);

    let json = updated.edit.document.to_pretty_json().unwrap();
    let restored = WorkspaceDocument::from_json(&json).unwrap();
    assert_eq!(restored, updated.edit.document);
}

#[test]
fn footprint_failure_and_path_redraw_revalidation_are_atomic() {
    let id = path_id("edge");
    let safe_path = add_path(&workspace(), id, "Bord", vec![[250.0, -4.0], [250.0, 4.0]]);
    let profiled = preview_upsert_road(
        &safe_path,
        command(&safe_path, road(id, 4.0, 0.08), "safe-edge"),
    )
    .unwrap()
    .edit
    .document;
    let before_hash = profiled.content_hash().unwrap();
    let error = preview_redraw_path(
        &profiled,
        PathRedrawCommand {
            command_id: CommandId::deterministic("road-world-test/redraw/edge-invalid"),
            base_revision: profiled.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id: id,
            trace_xz_m: vec![[255.0, -4.0], [255.0, 4.0]],
        },
    )
    .unwrap_err();
    assert!(matches!(
        error,
        PathCommandError::RoadGeneration(RoadGenerationError::FootprintOutOfBounds { .. })
    ));
    assert_eq!(profiled.content_hash().unwrap(), before_hash);

    let valid = preview_redraw_path(
        &profiled,
        PathRedrawCommand {
            command_id: CommandId::deterministic("road-world-test/redraw/edge-valid"),
            base_revision: profiled.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id: id,
            trace_xz_m: vec![[249.0, -5.0], [249.0, 5.0]],
        },
    )
    .unwrap();
    assert_eq!(
        valid.edit.receipt.new_revision,
        profiled.revision.next().unwrap()
    );
    let area = valid.edit.document.area(module_id(), area_id()).unwrap();
    RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads)
        .unwrap()
        .generate()
        .unwrap();

    let outside_path = add_path(
        &workspace(),
        path_id("outside"),
        "Hors empreinte",
        vec![[255.0, -4.0], [255.0, 4.0]],
    );
    let outside_hash = outside_path.content_hash().unwrap();
    assert!(matches!(
        preview_upsert_road(
            &outside_path,
            command(
                &outside_path,
                road(path_id("outside"), 4.0, 0.08),
                "outside"
            )
        ),
        Err(RoadCommandError::Generation(
            RoadGenerationError::FootprintOutOfBounds { .. }
        ))
    ));
    assert_eq!(outside_path.content_hash().unwrap(), outside_hash);
}

#[test]
fn generation_rejects_cross_document_identity_missing_and_closed_paths() {
    let id = path_id("standalone");
    let source = add_path(
        &workspace(),
        id,
        "Courbe",
        vec![[-12.0, -8.0], [-2.0, 9.0], [11.0, 4.0], [18.0, -10.0]],
    );
    let area = source.area(module_id(), area_id()).unwrap();
    let valid_roads = RoadDocument::new(
        project_id(),
        area_id(),
        area.roads.revision,
        "Routes",
        vec![road(id, 4.0, 0.08)],
    );

    let mut wrong_project = valid_roads.clone();
    wrong_project.project_id = ProjectId::deterministic("road-world-test/foreign");
    assert!(matches!(
        RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &wrong_project),
        Err(RoadGenerationError::ProjectIdentityMismatch)
    ));
    let mut wrong_area = valid_roads.clone();
    wrong_area.area_id = AreaId::deterministic("road-world-test/foreign-area");
    assert!(matches!(
        RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &wrong_area),
        Err(RoadGenerationError::AreaIdentityMismatch)
    ));

    let missing_id = path_id("standalone-missing");
    let missing = RoadDocument::new(
        project_id(),
        area_id(),
        area.roads.revision,
        "Routes",
        vec![road(missing_id, 4.0, 0.08)],
    );
    assert!(matches!(
        RoadGenerationPlan::from_documents(&area.terrain, &area.paths, &missing),
        Err(RoadGenerationError::MissingPath(id)) if id == missing_id
    ));

    let mut closed_paths = area.paths.clone();
    closed_paths.paths[0].closed = true;
    closed_paths.validate().unwrap();
    assert!(matches!(
        RoadGenerationPlan::from_documents(&area.terrain, &closed_paths, &valid_roads),
        Err(RoadGenerationError::ClosedPath(closed)) if closed == id
    ));
}

#[test]
fn checked_budgets_and_invalid_triangles_fail_closed() {
    assert!(matches!(
        checked_mesh_counts(MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES + 1),
        Err(RoadGenerationError::LongitudinalSampleBudgetExceeded { .. })
    ));
    assert!(matches!(
        checked_mesh_counts(0),
        Err(RoadGenerationError::ArithmeticOverflow)
    ));
    let id = path_id("degenerate");
    assert!(matches!(
        build_normals(
            id,
            &[[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [2.0, 0.0, 0.0]],
            &[0, 1, 2]
        ),
        Err(RoadGenerationError::InvalidTriangle { path_id, .. }) if path_id == id
    ));
    assert!(matches!(
        build_normals(id, &[[0.0, 0.0, 0.0]], &[0, 0]),
        Err(RoadGenerationError::InvalidIndexCount(2))
    ));
    assert!(matches!(
        build_indices(2, 11),
        Err(RoadGenerationError::IndexCountMismatch {
            expected: 11,
            generated: 12
        })
    ));
}
