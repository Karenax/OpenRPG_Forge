use super::*;
use crate::{
    PathCommandError, PathCreateCommand, PathRedrawCommand, RiverCommandError, RiverGenerationPlan,
    RiverUpsertCommand, RoadCommandError, RoadUpsertCommand, preview_create_path,
    preview_redraw_path, preview_upsert_river, preview_upsert_road,
};
use forge_core::Vec3;
use forge_project::{
    AuthoredPath, AuthoredRiver, AuthoredRoad, NewProjectCommand, PathControlPoint, PathDocument,
    PathInterpolation, RiverChannelProfile, RiverChannelProfileAlgorithm, RiverSurface,
    RoadProfile, RoadProfileAlgorithm, RoadSurface, WorkspaceError,
};
use serde_json::json;

fn project_id() -> ProjectId {
    ProjectId::deterministic("wall-world-test/project")
}

fn module_id() -> ModuleId {
    ModuleId::deterministic("wall-world-test/module")
}

fn area_id() -> AreaId {
    AreaId::deterministic("wall-world-test/area")
}

fn path_id(key: &str) -> PathId {
    PathId::deterministic(format!("wall-world-test/path/{key}"))
}

fn workspace() -> WorkspaceDocument {
    WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("wall-world-test/create-workspace"),
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        project_name: "Projet murs".to_owned(),
        module_name: "Module murs".to_owned(),
        area_name: "Zone murs".to_owned(),
        seed: 0x25_25,
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
            command_id: CommandId::deterministic(format!("wall-world-test/create/{id}")),
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

fn wall(path_id: PathId, height_m: f32, thickness_m: f32) -> AuthoredWall {
    AuthoredWall {
        path_id,
        profile: WallProfile {
            algorithm: WallProfileAlgorithm::TerrainDrapedSolidV1,
            height_m,
            thickness_m,
        },
        surface: WallSurface::ForgeStoneV1,
    }
}

fn wall_command(
    workspace: &WorkspaceDocument,
    authored_wall: AuthoredWall,
    key: &str,
) -> WallUpsertCommand {
    WallUpsertCommand {
        command_id: CommandId::deterministic(format!("wall-world-test/upsert/{key}")),
        base_revision: workspace.revision,
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        wall: authored_wall,
    }
}

fn road(path_id: PathId) -> AuthoredRoad {
    AuthoredRoad {
        path_id,
        profile: RoadProfile {
            algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
            width_m: 4.0,
            crown_height_m: 0.08,
        },
        surface: RoadSurface::CompactedEarthV1,
    }
}

fn road_command(workspace: &WorkspaceDocument, authored_road: AuthoredRoad) -> RoadUpsertCommand {
    RoadUpsertCommand {
        command_id: CommandId::deterministic(format!(
            "wall-world-test/road/{}",
            authored_road.path_id
        )),
        base_revision: workspace.revision,
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        road: authored_road,
    }
}

fn river(path_id: PathId) -> AuthoredRiver {
    AuthoredRiver {
        path_id,
        channel: RiverChannelProfile {
            algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
            surface_width_m: 8.0,
            bed_depth_m: 1.5,
            bank_blend_width_m: 4.0,
        },
        surface: RiverSurface::ForgeWaterV1,
    }
}

fn river_command(
    workspace: &WorkspaceDocument,
    authored_river: AuthoredRiver,
) -> RiverUpsertCommand {
    RiverUpsertCommand {
        command_id: CommandId::deterministic(format!(
            "wall-world-test/river/{}",
            authored_river.path_id
        )),
        base_revision: workspace.revision,
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        river: authored_river,
    }
}

fn generate_walls(workspace: &WorkspaceDocument) -> (TerrainDocument, GeneratedWalls) {
    let area = workspace.area(module_id(), area_id()).unwrap();
    let effective_terrain =
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)
            .unwrap()
            .generate()
            .unwrap()
            .effective_terrain;
    let generated =
        WallGenerationPlan::from_documents(&effective_terrain, &area.paths, &area.walls)
            .unwrap()
            .generate()
            .unwrap();
    (effective_terrain, generated)
}

fn assert_mesh_contract(
    plan: &WallGenerationPlan,
    generated: &GeneratedWall,
    authored: AuthoredWall,
) {
    let path = plan.paths().generate_path(authored.path_id).unwrap();
    let row_count = path.samples.len();
    assert_eq!(generated.path_id, authored.path_id);
    assert_eq!(generated.profile, authored.profile);
    assert_eq!(generated.surface, authored.surface);
    assert_eq!(generated.longitudinal_sample_count(), row_count);
    assert_eq!(
        generated.positions_m.len(),
        row_count * WALL_VERTICES_PER_ROW + WALL_CAP_VERTEX_COUNT
    );
    assert_eq!(generated.normals.len(), generated.positions_m.len());
    assert_eq!(
        generated.indices.len(),
        (row_count - 1) * WALL_INDICES_PER_LONGITUDINAL_INTERVAL + WALL_CAP_INDEX_COUNT
    );
    assert_eq!(
        &generated.indices[..WALL_INDICES_PER_LONGITUDINAL_INTERVAL],
        &[
            0, 8, 1, 1, 8, 9, 2, 10, 3, 3, 10, 11, 4, 12, 5, 5, 12, 13, 6, 14, 7, 7, 14, 15,
        ]
    );
    let cap_start = u32::try_from(row_count * WALL_VERTICES_PER_ROW).unwrap();
    assert_eq!(
        &generated.indices[generated.indices.len() - WALL_CAP_INDEX_COUNT..],
        &[
            cap_start,
            cap_start + 3,
            cap_start + 2,
            cap_start,
            cap_start + 2,
            cap_start + 1,
            cap_start + 4,
            cap_start + 5,
            cap_start + 6,
            cap_start + 4,
            cap_start + 6,
            cap_start + 7,
        ]
    );

    let (rows, remainder) = generated.positions_m[..row_count * WALL_VERTICES_PER_ROW]
        .as_chunks::<WALL_VERTICES_PER_ROW>();
    assert!(remainder.is_empty());
    for row in rows {
        assert_eq!(row[0].map(f32::to_bits), row[7].map(f32::to_bits));
        assert_eq!(row[1].map(f32::to_bits), row[2].map(f32::to_bits));
        assert_eq!(row[3].map(f32::to_bits), row[4].map(f32::to_bits));
        assert_eq!(row[5].map(f32::to_bits), row[6].map(f32::to_bits));
        assert_eq!(
            row[1][1].to_bits(),
            (row[0][1] + authored.profile.height_m).to_bits()
        );
        assert_eq!(
            row[4][1].to_bits(),
            (row[5][1] + authored.profile.height_m).to_bits()
        );
        for base in [row[0], row[5]] {
            let surface = plan.terrain().surface_at(base[0], base[2]).unwrap();
            assert_eq!(
                base[1].to_bits(),
                (surface.height_m - WALL_BASE_EMBED_M).to_bits()
            );
        }
        let thickness_m = (row[5][0] - row[0][0]).hypot(row[5][2] - row[0][2]);
        assert!((thickness_m - authored.profile.thickness_m).abs() < 0.000_1);
    }
    assert!(generated.normals.iter().all(|normal| {
        let length = normal[0].hypot(normal[1]).hypot(normal[2]);
        normal.iter().all(|component| component.is_finite()) && (length - 1.0).abs() < 0.000_1
    }));
}

#[test]
fn wall_command_roundtrip_undo_redo_and_generation_are_deterministic() {
    let id = path_id("deterministic");
    let source = add_path(
        &workspace(),
        id,
        "Mur principal",
        vec![[-12.0, -6.0], [12.0, 6.0]],
    );
    let source_hash = source.content_hash().unwrap();
    let authored = wall(id, 2.5, 0.6);
    let result =
        preview_upsert_wall(&source, wall_command(&source, authored, "deterministic")).unwrap();

    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert_eq!(result.previous_wall, None);
    assert_eq!(result.wall, authored);
    assert_eq!(
        result.edit.receipt.new_revision,
        source.revision.next().unwrap()
    );
    assert_eq!(*result.edit.receipt.inverse_snapshot, source);

    let redo = preview_upsert_wall(
        &result.edit.receipt.inverse_snapshot,
        wall_command(
            &result.edit.receipt.inverse_snapshot,
            authored,
            "deterministic",
        ),
    )
    .unwrap();
    assert_eq!(redo.edit.document, result.edit.document);

    let (effective_terrain, generated) = generate_walls(&result.edit.document);
    let (_, replay) = generate_walls(&result.edit.document);
    assert_eq!(generated, replay);
    assert_eq!(
        generated.content_hash().unwrap(),
        replay.content_hash().unwrap()
    );
    let area = result.edit.document.area(module_id(), area_id()).unwrap();
    assert_eq!(generated.generation_version, WALL_GENERATION_VERSION);
    assert_eq!(
        generated.terrain_document_hash,
        effective_terrain.content_hash().unwrap()
    );
    assert_eq!(
        generated.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(
        generated.wall_document_hash,
        area.walls.content_hash().unwrap()
    );
    validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &generated).unwrap();
    let plan =
        WallGenerationPlan::from_documents(&effective_terrain, &area.paths, &area.walls).unwrap();
    assert_mesh_contract(&plan, &generated.walls[0], authored);

    let json = result.edit.document.to_pretty_json().unwrap();
    let restored = WorkspaceDocument::from_json(&json).unwrap();
    let (_, restored_generated) = generate_walls(&restored);
    assert_eq!(restored, result.edit.document);
    assert_eq!(
        restored_generated.content_hash().unwrap(),
        generated.content_hash().unwrap()
    );
}

#[test]
fn generated_validator_is_exact_exhaustive_and_canonical() {
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
        source = preview_upsert_wall(
            &source,
            wall_command(&source, wall(id, 2.0, 0.4), &id.to_string()),
        )
        .unwrap()
        .edit
        .document;
    }
    let area = source.area(module_id(), area_id()).unwrap();
    let (effective_terrain, generated) = generate_walls(&source);
    validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &generated).unwrap();
    let ids = generated
        .walls
        .iter()
        .map(|wall| wall.path_id)
        .collect::<Vec<_>>();
    assert!(ids.windows(2).all(|pair| pair[0] < pair[1]));

    let mut wrong_version = generated.clone();
    wrong_version.generation_version += 1;
    assert!(
        validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &wrong_version)
            .is_err()
    );
    let mut omitted = generated.clone();
    omitted.walls.pop();
    assert!(
        validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &omitted).is_err()
    );
    let mut reordered = generated.clone();
    reordered.walls.reverse();
    assert!(
        validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &reordered).is_err()
    );
    let mut displaced = generated.clone();
    displaced.walls[0].positions_m[0][0] += 0.001;
    assert!(
        validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &displaced).is_err()
    );
    let mut retopologized = generated.clone();
    retopologized.walls[0].indices.swap(0, 1);
    assert!(
        validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &retopologized)
            .is_err()
    );
    let mut renormalized = generated.clone();
    renormalized.walls[0].normals[0][1] += 0.001;
    assert!(
        validate_generated_walls(&effective_terrain, &area.paths, &area.walls, &renormalized)
            .is_err()
    );

    let mut strict_value = serde_json::to_value(&generated).unwrap();
    strict_value["unknown"] = json!(true);
    assert!(serde_json::from_value::<GeneratedWalls>(strict_value).is_err());
}

#[test]
fn wall_invalidities_and_path_redraw_fail_atomically() {
    let id = path_id("atomic-edge");
    let source = add_path(
        &workspace(),
        id,
        "Mur de bord",
        vec![[250.0, -6.0], [250.0, 6.0]],
    );
    let authored = wall(id, 2.0, 2.0);
    let profiled = preview_upsert_wall(&source, wall_command(&source, authored, "initial"))
        .unwrap()
        .edit
        .document;
    let source_hash = profiled.content_hash().unwrap();

    assert!(matches!(
        preview_upsert_wall(&profiled, wall_command(&profiled, authored, "noop")),
        Err(WallCommandError::Workspace(WorkspaceError::NoChange))
    ));
    let mut stale = wall_command(&profiled, wall(id, 2.5, 2.0), "stale");
    stale.base_revision = profiled.revision.next().unwrap();
    assert!(matches!(
        preview_upsert_wall(&profiled, stale),
        Err(WallCommandError::Workspace(WorkspaceError::StaleRevision))
    ));
    let mut wrong_project = wall_command(&profiled, wall(id, 2.5, 2.0), "wrong-project");
    wrong_project.project_id = ProjectId::deterministic("wall-world-test/foreign");
    assert!(matches!(
        preview_upsert_wall(&profiled, wrong_project),
        Err(WallCommandError::WrongProject)
    ));
    let missing_id = path_id("missing");
    assert!(matches!(
        preview_upsert_wall(
            &profiled,
            wall_command(&profiled, wall(missing_id, 2.0, 0.4), "missing")
        ),
        Err(WallCommandError::Workspace(WorkspaceError::MissingWallPath(found)))
            if found == missing_id
    ));
    let invalid = wall(id, f32::NAN, 0.4);
    assert!(matches!(
        preview_upsert_wall(&profiled, wall_command(&profiled, invalid, "invalid")),
        Err(WallCommandError::InvalidWall(_))
    ));

    let redraw_error = preview_redraw_path(
        &profiled,
        PathRedrawCommand {
            command_id: CommandId::deterministic("wall-world-test/redraw/outside"),
            base_revision: profiled.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id: id,
            trace_xz_m: vec![[255.5, -6.0], [255.5, 6.0]],
        },
    )
    .unwrap_err();
    assert!(matches!(
        redraw_error,
        PathCommandError::WallGeneration(WallGenerationError::FootprintOutOfBounds { .. })
    ));
    assert_eq!(profiled.content_hash().unwrap(), source_hash);

    let replacement = wall(id, 3.0, 1.0);
    let updated = preview_upsert_wall(
        &profiled,
        wall_command(&profiled, replacement, "replacement"),
    )
    .unwrap();
    assert_eq!(updated.previous_wall, Some(authored));
    assert_eq!(*updated.edit.receipt.inverse_snapshot, profiled);
}

fn assert_document_boundaries_fail_closed(area: &WorkspaceArea, id: PathId) {
    let valid = WallDocument::new(
        project_id(),
        area_id(),
        area.walls.revision,
        "Murs",
        vec![wall(id, 2.0, 0.4)],
    );
    let mut wrong_project = valid.clone();
    wrong_project.project_id = ProjectId::deterministic("wall-world-test/foreign-project");
    assert!(matches!(
        WallGenerationPlan::from_documents(&area.terrain, &area.paths, &wrong_project),
        Err(WallGenerationError::ProjectIdentityMismatch)
    ));
    let mut wrong_area = valid.clone();
    wrong_area.area_id = AreaId::deterministic("wall-world-test/foreign-area");
    assert!(matches!(
        WallGenerationPlan::from_documents(&area.terrain, &area.paths, &wrong_area),
        Err(WallGenerationError::AreaIdentityMismatch)
    ));
    let missing_id = path_id("documents-missing");
    let missing = WallDocument::new(
        project_id(),
        area_id(),
        area.walls.revision,
        "Murs",
        vec![wall(missing_id, 2.0, 0.4)],
    );
    assert!(matches!(
        WallGenerationPlan::from_documents(&area.terrain, &area.paths, &missing),
        Err(WallGenerationError::MissingPath(found)) if found == missing_id
    ));
    let mut closed_paths = area.paths.clone();
    closed_paths.paths[0].closed = true;
    closed_paths.paths[0]
        .control_points
        .push(PathControlPoint::new(Vec3::new(0.0, 0.0, 8.0)));
    closed_paths.validate().unwrap();
    assert!(matches!(
        WallGenerationPlan::from_documents(&area.terrain, &closed_paths, &valid),
        Err(WallGenerationError::ClosedPath(found)) if found == id
    ));
}

fn assert_aggregate_budgets_fail_closed(area: &WorkspaceArea, id: PathId) {
    let mut authored_paths = (0..64)
        .map(|index| {
            let id = path_id(&format!("budget-{index:02}"));
            AuthoredPath {
                id,
                name: format!("Budget {index:02}"),
                closed: false,
                interpolation: PathInterpolation::CentripetalCatmullRomV1,
                control_points: vec![
                    PathControlPoint::new(Vec3::new(-255.75, 0.0, 0.0)),
                    PathControlPoint::new(Vec3::new(255.75, 0.0, 0.0)),
                ],
            }
        })
        .collect::<Vec<_>>();
    authored_paths.sort_by_key(|path| path.id);
    let authored_walls = authored_paths
        .iter()
        .map(|path| wall(path.id, 2.0, 0.1))
        .collect::<Vec<_>>();
    let paths = PathDocument::new(
        project_id(),
        area_id(),
        Revision::INITIAL,
        "Budget chemins",
        authored_paths,
    );
    let walls = WallDocument::new(
        project_id(),
        area_id(),
        Revision::INITIAL,
        "Budget murs",
        authored_walls,
    );
    paths.validate().unwrap();
    walls.validate().unwrap();
    let plan = WallGenerationPlan::from_documents(&area.terrain, &paths, &walls).unwrap();
    assert!(matches!(
        plan.generate(),
        Err(WallGenerationError::LongitudinalSampleBudgetExceeded {
            requested: 16_448,
            maximum: MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES,
        })
    ));
    assert!(matches!(
        checked_mesh_counts(MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES + 1),
        Err(WallGenerationError::LongitudinalSampleBudgetExceeded { .. })
    ));
    assert!(matches!(
        build_normals(
            id,
            &[[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [2.0, 0.0, 0.0]],
            &[0, 1, 2]
        ),
        Err(WallGenerationError::InvalidTriangle { .. })
    ));
}

#[test]
fn document_boundaries_and_aggregate_budgets_fail_closed() {
    let id = path_id("documents");
    let source = add_path(
        &workspace(),
        id,
        "Source documentaire",
        vec![[-12.0, -8.0], [12.0, -8.0]],
    );
    let area = source.area(module_id(), area_id()).unwrap();
    assert_document_boundaries_fail_closed(area, id);
    assert_aggregate_budgets_fail_closed(area, id);
}

#[test]
fn river_effective_terrain_is_used_and_revalidated_without_persisting_it() {
    let river_id = path_id("effective-river");
    let wall_id = path_id("effective-wall");
    let mut source = workspace();
    source = add_path(
        &source,
        river_id,
        "Rivière",
        vec![[-30.0, 0.0], [30.0, 0.0]],
    );
    source = add_path(
        &source,
        wall_id,
        "Mur traversant",
        vec![[0.0, -12.0], [0.0, 12.0]],
    );
    let with_wall = preview_upsert_wall(
        &source,
        wall_command(&source, wall(wall_id, 2.0, 0.4), "before-river"),
    )
    .unwrap()
    .edit
    .document;
    let before_area = with_wall.area(module_id(), area_id()).unwrap();
    let canonical_hash = before_area.terrain.content_hash().unwrap();
    let canonical_wall = WallGenerationPlan::from_documents(
        &before_area.terrain,
        &before_area.paths,
        &before_area.walls,
    )
    .unwrap()
    .generate()
    .unwrap();

    let river_result =
        preview_upsert_river(&with_wall, river_command(&with_wall, river(river_id))).unwrap();
    assert_eq!(*river_result.edit.receipt.inverse_snapshot, with_wall);
    let final_area = river_result
        .edit
        .document
        .area(module_id(), area_id())
        .unwrap();
    assert_eq!(final_area.terrain.content_hash().unwrap(), canonical_hash);
    let river_projection = RiverGenerationPlan::from_documents(
        &final_area.terrain,
        &final_area.paths,
        &final_area.roads,
        &final_area.rivers,
    )
    .unwrap()
    .generate()
    .unwrap();
    let effective_plan = WallGenerationPlan::from_documents(
        &river_projection.effective_terrain,
        &final_area.paths,
        &final_area.walls,
    )
    .unwrap();
    let effective_wall = effective_plan.generate().unwrap();
    validate_generated_walls(
        &river_projection.effective_terrain,
        &final_area.paths,
        &final_area.walls,
        &effective_wall,
    )
    .unwrap();
    let canonical_rows = canonical_wall.walls[0].longitudinal_sample_count();
    let effective_rows = effective_wall.walls[0].longitudinal_sample_count();
    assert_eq!(canonical_rows, effective_rows);
    assert!((0..effective_rows).any(|row| {
        let offset = row * WALL_VERTICES_PER_ROW;
        canonical_wall.walls[0].positions_m[offset][1].to_bits()
            != effective_wall.walls[0].positions_m[offset][1].to_bits()
    }));
    assert_mesh_contract(
        &effective_plan,
        &effective_wall.walls[0],
        wall(wall_id, 2.0, 0.4),
    );
}

#[test]
fn empty_wall_validation_returns_before_effective_terrain_is_inspected() {
    let source = workspace();
    let area = source.area(module_id(), area_id()).unwrap();
    assert!(area.walls.walls.is_empty());

    let mut unrelated_terrain = area.terrain.clone();
    unrelated_terrain.project_id = ProjectId::deterministic("wall-world-test/unrelated-project");

    validate_area_walls_with_effective_terrain(area, &unrelated_terrain).unwrap();
}

#[test]
fn distinct_geometry_contacts_are_allowed_and_same_path_roles_are_rejected() {
    let road_id = path_id("contact-road");
    let river_id = path_id("contact-river");
    let crossing_wall_id = path_id("contact-wall-crossing");
    let endpoint_wall_id = path_id("contact-wall-endpoint");
    let collinear_wall_id = path_id("contact-wall-collinear");
    let mut source = workspace();
    for (id, name, trace) in [
        (road_id, "Route", vec![[-30.0, 20.0], [30.0, 20.0]]),
        (river_id, "Rivière", vec![[-30.0, -20.0], [30.0, -20.0]]),
        (
            crossing_wall_id,
            "Mur traversant",
            vec![[0.0, -30.0], [0.0, 30.0]],
        ),
        (
            endpoint_wall_id,
            "Mur endpoint",
            vec![[0.0, 30.0], [20.0, 30.0]],
        ),
        (
            collinear_wall_id,
            "Mur collinéaire",
            vec![[-10.0, 30.0], [10.0, 30.0]],
        ),
    ] {
        source = add_path(&source, id, name, trace);
    }
    source = preview_upsert_road(&source, road_command(&source, road(road_id)))
        .unwrap()
        .edit
        .document;
    source = preview_upsert_river(&source, river_command(&source, river(river_id)))
        .unwrap()
        .edit
        .document;
    for id in [crossing_wall_id, endpoint_wall_id, collinear_wall_id] {
        source = preview_upsert_wall(
            &source,
            wall_command(&source, wall(id, 2.0, 0.4), &id.to_string()),
        )
        .unwrap()
        .edit
        .document;
    }
    let (_, generated) = generate_walls(&source);
    assert_eq!(generated.walls.len(), 3);

    let source_hash = source.content_hash().unwrap();
    assert!(matches!(
        preview_upsert_road(
            &source,
            road_command(&source, road(crossing_wall_id))
        ),
        Err(RoadCommandError::Workspace(WorkspaceError::WallRoadConflict(found)))
            if found == crossing_wall_id
    ));
    assert!(matches!(
        preview_upsert_river(
            &source,
            river_command(&source, river(crossing_wall_id))
        ),
        Err(RiverCommandError::Workspace(WorkspaceError::WallRiverConflict(found)))
            if found == crossing_wall_id
    ));
    assert!(matches!(
        preview_upsert_wall(
            &source,
            wall_command(&source, wall(road_id, 2.0, 0.4), "road-conflict")
        ),
        Err(WallCommandError::Workspace(WorkspaceError::WallRoadConflict(found)))
            if found == road_id
    ));
    assert!(matches!(
        preview_upsert_wall(
            &source,
            wall_command(&source, wall(river_id, 2.0, 0.4), "river-conflict")
        ),
        Err(WallCommandError::Workspace(WorkspaceError::WallRiverConflict(found)))
            if found == river_id
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}
