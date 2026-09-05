use super::*;
use crate::{
    PathCommandError, PathCreateCommand, PathRedrawCommand, RoadCommandError, RoadUpsertCommand,
    preview_create_path, preview_redraw_path, preview_upsert_road,
};
use forge_core::Vec3;
use forge_project::{
    AuthoredRoad, NewProjectCommand, RiverChannelProfile, RoadProfile, RoadProfileAlgorithm,
    RoadSurface, WorkspaceError,
};
use serde_json::json;

fn project_id() -> ProjectId {
    ProjectId::deterministic("river-world-test/project")
}

fn module_id() -> ModuleId {
    ModuleId::deterministic("river-world-test/module")
}

fn area_id() -> AreaId {
    AreaId::deterministic("river-world-test/area")
}

fn path_id(key: &str) -> PathId {
    PathId::deterministic(format!("river-world-test/path/{key}"))
}

fn workspace() -> WorkspaceDocument {
    WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("river-world-test/create-workspace"),
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        project_name: "Projet rivières".to_owned(),
        module_name: "Module rivières".to_owned(),
        area_name: "Zone rivières".to_owned(),
        seed: 0x23_23,
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
            command_id: CommandId::deterministic(format!("river-world-test/create/{id}")),
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

fn river(path_id: PathId, width_m: f32, depth_m: f32, bank_blend_m: f32) -> AuthoredRiver {
    AuthoredRiver {
        path_id,
        channel: RiverChannelProfile {
            algorithm: RiverChannelProfileAlgorithm::TerrainCarvedChannelV1,
            surface_width_m: width_m,
            bed_depth_m: depth_m,
            bank_blend_width_m: bank_blend_m,
        },
        surface: RiverSurface::ForgeWaterV1,
    }
}

fn road(path_id: PathId, width_m: f32) -> AuthoredRoad {
    AuthoredRoad {
        path_id,
        profile: RoadProfile {
            algorithm: RoadProfileAlgorithm::TerrainDrapedCrownV1,
            width_m,
            crown_height_m: 0.08,
        },
        surface: RoadSurface::CompactedEarthV1,
    }
}

fn river_command(
    workspace: &WorkspaceDocument,
    authored_river: AuthoredRiver,
    key: &str,
) -> RiverUpsertCommand {
    RiverUpsertCommand {
        command_id: CommandId::deterministic(format!("river-world-test/upsert/{key}")),
        base_revision: workspace.revision,
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        river: authored_river,
    }
}

fn road_command(
    workspace: &WorkspaceDocument,
    authored_road: AuthoredRoad,
    key: &str,
) -> RoadUpsertCommand {
    RoadUpsertCommand {
        command_id: CommandId::deterministic(format!("river-world-test/road/{key}")),
        base_revision: workspace.revision,
        project_id: project_id(),
        module_id: module_id(),
        area_id: area_id(),
        road: authored_road,
    }
}

fn workspace_with_river(
    id: PathId,
    trace_xz_m: Vec<[f32; 2]>,
) -> (WorkspaceDocument, AuthoredRiver) {
    let source = add_path(&workspace(), id, "Cours d'eau", trace_xz_m);
    let authored_river = river(id, 8.0, 1.5, 4.0);
    let document = preview_upsert_river(&source, river_command(&source, authored_river, "initial"))
        .unwrap()
        .edit
        .document;
    (document, authored_river)
}

fn generate(workspace: &WorkspaceDocument) -> RiverGenerationResult {
    let area = workspace.area(module_id(), area_id()).unwrap();
    RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &area.rivers)
        .unwrap()
        .generate()
        .unwrap()
}

fn assert_water_mesh_contract(mesh: &GeneratedRiver) {
    assert_eq!(mesh.positions_m.len(), mesh.normals.len());
    assert_eq!(mesh.positions_m.len(), mesh.uvs.len());
    assert_eq!(
        mesh.positions_m.len(),
        mesh.longitudinal_sample_count() * RIVER_CROSS_SECTION_VERTEX_COUNT
    );
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

    let (rows, remainder) = mesh.positions_m.as_chunks::<3>();
    assert!(remainder.is_empty());
    let (uv_rows, uv_remainder) = mesh.uvs.as_chunks::<3>();
    assert!(uv_remainder.is_empty());
    for (row, uvs) in rows.iter().zip(uv_rows) {
        assert_eq!(row[0][1].to_bits(), row[1][1].to_bits());
        assert_eq!(row[1][1].to_bits(), row[2][1].to_bits());
        assert_eq!(uvs[0][0].to_bits(), 0.0_f32.to_bits());
        assert_eq!(uvs[1][0].to_bits(), 0.5_f32.to_bits());
        assert_eq!(uvs[2][0].to_bits(), 1.0_f32.to_bits());
        assert_eq!(uvs[0][1].to_bits(), uvs[1][1].to_bits());
        assert_eq!(uvs[1][1].to_bits(), uvs[2][1].to_bits());
    }
    assert!(rows.windows(2).all(|pair| pair[1][1][1] <= pair[0][1][1]));
    assert!(rows.windows(2).all(|pair| {
        let chord = f64::from(pair[1][1][0] - pair[0][1][0])
            .hypot(f64::from(pair[1][1][2] - pair[0][1][2]));
        chord > 0.0 && chord <= RIVER_MESH_MAX_LONGITUDINAL_CHORD_M
    }));
    assert!(
        mesh.normals.iter().all(|normal| {
            normal.iter().all(|component| component.is_finite()) && normal[1] > 0.0
        })
    );
}

#[test]
fn generation_is_deterministic_hash_bound_sorted_and_strict() {
    let first_id = path_id("canonical-a");
    let second_id = path_id("canonical-b");
    let mut source = workspace();
    source = add_path(
        &source,
        first_id,
        "Bras nord",
        vec![[-18.0, -30.0], [18.0, -30.0]],
    );
    source = add_path(
        &source,
        second_id,
        "Bras sud",
        vec![[-18.0, 30.0], [18.0, 30.0]],
    );
    let source_hash = source.content_hash().unwrap();
    let mut insertion = [first_id, second_id];
    insertion.sort();
    insertion.reverse();
    for (index, id) in insertion.into_iter().enumerate() {
        source = preview_upsert_river(
            &source,
            river_command(
                &source,
                river(id, 6.0, 1.0, 3.0),
                &format!("canonical-{index}"),
            ),
        )
        .unwrap()
        .edit
        .document;
    }
    assert_ne!(source.content_hash().unwrap(), source_hash);

    let first = generate(&source);
    let second = generate(&source);
    assert_eq!(first, second);
    assert_eq!(
        first.generated.content_hash().unwrap(),
        second.generated.content_hash().unwrap()
    );
    assert_eq!(first.generated.generation_version, RIVER_GENERATION_VERSION);
    let area = source.area(module_id(), area_id()).unwrap();
    assert_eq!(
        first.generated.terrain_document_hash,
        area.terrain.content_hash().unwrap()
    );
    assert_eq!(
        first.generated.path_document_hash,
        area.paths.content_hash().unwrap()
    );
    assert_eq!(
        first.generated.road_document_hash,
        area.roads.content_hash().unwrap()
    );
    assert_eq!(
        first.generated.river_document_hash,
        area.rivers.content_hash().unwrap()
    );
    assert_eq!(
        first.generated.effective_terrain_document_hash,
        first.effective_terrain.content_hash().unwrap()
    );
    let ids = first
        .generated
        .rivers
        .iter()
        .map(|generated| generated.path_id)
        .collect::<Vec<_>>();
    assert!(ids.windows(2).all(|pair| pair[0] < pair[1]));
    for mesh in &first.generated.rivers {
        assert_water_mesh_contract(mesh);
    }

    let mut generated_value = serde_json::to_value(&first.generated).unwrap();
    generated_value["unknown"] = json!(true);
    assert!(serde_json::from_value::<GeneratedRivers>(generated_value).is_err());
    let mut command_value = serde_json::to_value(river_command(
        &source,
        river(first_id, 8.0, 1.5, 4.0),
        "strict",
    ))
    .unwrap();
    command_value["preview_only"] = json!(true);
    assert!(serde_json::from_value::<RiverUpsertCommand>(command_value).is_err());
}

#[test]
fn carving_is_rounded_blended_earth_and_never_raises_canonical_terrain() {
    let id = path_id("carving");
    let source = add_path(
        &workspace(),
        id,
        "Lit droit",
        vec![[-16.0, 0.0], [16.0, 0.0]],
    );
    let canonical = source.area(module_id(), area_id()).unwrap().terrain.clone();
    let canonical_hash = canonical.content_hash().unwrap();
    let authored = river(id, 8.0, 1.5, 4.0);
    let edited = preview_upsert_river(&source, river_command(&source, authored, "carving"))
        .unwrap()
        .edit
        .document;
    let result = generate(&edited);
    assert_eq!(canonical.content_hash().unwrap(), canonical_hash);
    assert_ne!(result.effective_terrain, canonical);

    let canonical_plan = TerrainGenerationPlan::from_document(&canonical).unwrap();
    let effective_plan = TerrainGenerationPlan::from_document(&result.effective_terrain).unwrap();
    for sample_z in 0..=TERRAIN_GLOBAL_INTERVALS_PER_AXIS {
        for sample_x in 0..=TERRAIN_GLOBAL_INTERVALS_PER_AXIS {
            let x_m = -TERRAIN_HALF_EXTENT_M + f32::from(sample_x) * TERRAIN_SAMPLE_SPACING_M;
            let z_m = -TERRAIN_HALF_EXTENT_M + f32::from(sample_z) * TERRAIN_SAMPLE_SPACING_M;
            let before = canonical_plan.height_at(x_m, z_m).unwrap();
            let after = effective_plan.height_at(x_m, z_m).unwrap();
            assert!(after <= before, "sample ({sample_x},{sample_z}) was raised");
        }
    }

    let mesh = &result.generated.rivers[0];
    let (rows, _) = mesh.positions_m.as_chunks::<3>();
    let upstream = rows[0][1];
    let upstream_canonical_m = canonical_plan.height_at(upstream[0], upstream[2]).unwrap();
    assert!((upstream[1] - RIVER_SURFACE_CLEARANCE_M - upstream_canonical_m).abs() < 0.000_1);
    let centre_row = rows
        .iter()
        .min_by(|first, second| first[1][0].abs().total_cmp(&second[1][0].abs()))
        .unwrap();
    assert!(centre_row[1][0].abs() < 0.001);
    let water_surface_m = centre_row[1][1] - RIVER_SURFACE_CLEARANCE_M;
    let centre_height = effective_plan
        .height_at(centre_row[1][0], centre_row[1][2])
        .unwrap();
    let expected_bed_m = water_surface_m - authored.channel.bed_depth_m;
    assert!((centre_height - expected_bed_m).abs() < 0.000_1);

    let bank_z_m =
        authored.channel.surface_width_m * 0.5 + authored.channel.bank_blend_width_m * 0.5;
    let canonical_bank_m = canonical_plan
        .height_at(centre_row[1][0], bank_z_m)
        .unwrap();
    let effective_bank_m = effective_plan
        .height_at(centre_row[1][0], bank_z_m)
        .unwrap();
    let expected_bank_m = water_surface_m.midpoint(canonical_bank_m);
    assert!((effective_bank_m - expected_bank_m.min(canonical_bank_m)).abs() < 0.000_1);
    assert!(effective_bank_m > centre_height);

    let centre_material = result
        .effective_terrain
        .material_weight_overrides
        .iter()
        .find(|weights| weights.sample_x == 128 && weights.sample_z == 128)
        .unwrap();
    assert_eq!(
        centre_material.weights.map(f32::to_bits),
        [0.0, 1.0, 0.0].map(f32::to_bits)
    );
    assert!(canonical.material_weight_overrides.is_empty());
}

#[test]
fn empty_river_document_preserves_the_canonical_terrain_exactly() {
    let source = workspace();
    let area = source.area(module_id(), area_id()).unwrap();
    let result = generate(&source);
    assert_eq!(result.effective_terrain, area.terrain);
    assert!(result.generated.rivers.is_empty());
    assert_eq!(
        result.generated.terrain_document_hash,
        result.generated.effective_terrain_document_hash
    );
}

#[test]
fn upsert_noop_context_invalid_and_missing_rejections_are_atomic() {
    let id = path_id("command");
    let (source, initial) = workspace_with_river(id, vec![[-12.0, 10.0], [12.0, 10.0]]);
    let source_hash = source.content_hash().unwrap();

    assert!(matches!(
        preview_upsert_river(&source, river_command(&source, initial, "noop")),
        Err(RiverCommandError::Workspace(WorkspaceError::NoChange))
    ));

    let mut stale = river_command(&source, river(id, 9.0, 1.5, 4.0), "stale");
    stale.base_revision = source.revision.next().unwrap();
    assert!(matches!(
        preview_upsert_river(&source, stale),
        Err(RiverCommandError::Workspace(WorkspaceError::StaleRevision))
    ));

    let mut wrong_project = river_command(&source, river(id, 9.0, 1.5, 4.0), "wrong-project");
    wrong_project.project_id = ProjectId::deterministic("river-world-test/foreign-project");
    assert!(matches!(
        preview_upsert_river(&source, wrong_project),
        Err(RiverCommandError::WrongProject)
    ));

    let mut wrong_area = river_command(&source, river(id, 9.0, 1.5, 4.0), "wrong-area");
    wrong_area.area_id = AreaId::deterministic("river-world-test/foreign-area");
    assert!(matches!(
        preview_upsert_river(&source, wrong_area),
        Err(RiverCommandError::Workspace(WorkspaceError::MissingTarget(
            "area"
        )))
    ));

    let missing_id = path_id("missing");
    assert!(matches!(
        preview_upsert_river(
            &source,
            river_command(&source, river(missing_id, 8.0, 1.5, 4.0), "missing")
        ),
        Err(RiverCommandError::Workspace(WorkspaceError::MissingRiverPath(found)))
            if found == missing_id
    ));

    let mut invalid = river(id, 8.0, 1.5, 4.0);
    invalid.channel.bed_depth_m = f32::NAN;
    assert!(matches!(
        preview_upsert_river(&source, river_command(&source, invalid, "invalid")),
        Err(RiverCommandError::InvalidRiver(_))
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);

    let replacement = river(id, 10.0, 2.0, 5.0);
    let updated =
        preview_upsert_river(&source, river_command(&source, replacement, "replacement")).unwrap();
    assert_eq!(updated.previous_river, Some(initial));
    assert_eq!(updated.river, replacement);
    assert_eq!(
        updated.edit.receipt.new_revision,
        source.revision.next().unwrap()
    );
    assert_eq!(*updated.edit.receipt.inverse_snapshot, source);
    let before_area = source.area(module_id(), area_id()).unwrap();
    let after_area = updated.edit.document.area(module_id(), area_id()).unwrap();
    assert_eq!(before_area.terrain, after_area.terrain);
    assert_eq!(before_area.paths, after_area.paths);
    assert_eq!(before_area.roads, after_area.roads);
    assert_ne!(before_area.rivers, after_area.rivers);
    let json = updated.edit.document.to_pretty_json().unwrap();
    assert_eq!(
        WorkspaceDocument::from_json(&json).unwrap(),
        updated.edit.document
    );
}

#[test]
fn river_river_and_river_road_overlaps_fail_atomically_in_both_edit_directions() {
    let first_id = path_id("overlap-river-a");
    let second_id = path_id("overlap-river-b");
    let road_id = path_id("overlap-road");
    let mut source = workspace();
    source = add_path(
        &source,
        first_id,
        "Rivière A",
        vec![[-20.0, 0.0], [20.0, 0.0]],
    );
    source = add_path(
        &source,
        second_id,
        "Rivière B",
        vec![[-20.0, 5.0], [20.0, 5.0]],
    );
    source = add_path(
        &source,
        road_id,
        "Route transverse",
        vec![[0.0, -20.0], [0.0, 20.0]],
    );
    let with_first = preview_upsert_river(
        &source,
        river_command(&source, river(first_id, 4.0, 1.0, 2.0), "first"),
    )
    .unwrap()
    .edit
    .document;
    let first_hash = with_first.content_hash().unwrap();
    assert!(matches!(
        preview_upsert_river(
            &with_first,
            river_command(
                &with_first,
                river(second_id, 4.0, 1.0, 2.0),
                "second-overlap"
            )
        ),
        Err(RiverCommandError::Generation(
            RiverGenerationError::RiverOverlap { .. }
        ))
    ));
    assert!(matches!(
        preview_upsert_road(
            &with_first,
            road_command(&with_first, road(road_id, 4.0), "road-after-river")
        ),
        Err(RoadCommandError::RiverGeneration(
            RiverGenerationError::RiverRoadOverlap { .. }
        ))
    ));
    assert_eq!(with_first.content_hash().unwrap(), first_hash);

    let with_road = preview_upsert_road(
        &source,
        road_command(&source, road(road_id, 4.0), "road-first"),
    )
    .unwrap()
    .edit
    .document;
    let road_hash = with_road.content_hash().unwrap();
    assert!(matches!(
        preview_upsert_river(
            &with_road,
            river_command(
                &with_road,
                river(first_id, 4.0, 1.0, 2.0),
                "river-after-road"
            )
        ),
        Err(RiverCommandError::Generation(
            RiverGenerationError::RiverRoadOverlap { .. }
        ))
    ));
    assert_eq!(with_road.content_hash().unwrap(), road_hash);
}

#[test]
fn footprint_and_path_redraw_are_revalidated_before_a_revision_exists() {
    let id = path_id("edge");
    let safe = add_path(
        &workspace(),
        id,
        "Rivière de bord",
        vec![[244.0, -12.0], [244.0, 12.0]],
    );
    let profiled = preview_upsert_river(
        &safe,
        river_command(&safe, river(id, 8.0, 1.5, 4.0), "edge-safe"),
    )
    .unwrap()
    .edit
    .document;
    let before_hash = profiled.content_hash().unwrap();
    let error = preview_redraw_path(
        &profiled,
        PathRedrawCommand {
            command_id: CommandId::deterministic("river-world-test/redraw/edge-invalid"),
            base_revision: profiled.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id: id,
            trace_xz_m: vec![[250.0, -12.0], [250.0, 12.0]],
        },
    )
    .unwrap_err();
    assert!(matches!(
        error,
        PathCommandError::RiverGeneration(RiverGenerationError::FootprintOutOfBounds { .. })
    ));
    assert_eq!(profiled.content_hash().unwrap(), before_hash);

    let valid = preview_redraw_path(
        &profiled,
        PathRedrawCommand {
            command_id: CommandId::deterministic("river-world-test/redraw/edge-valid"),
            base_revision: profiled.revision,
            project_id: project_id(),
            module_id: module_id(),
            area_id: area_id(),
            path_id: id,
            trace_xz_m: vec![[243.0, -14.0], [243.0, 14.0]],
        },
    )
    .unwrap();
    assert_eq!(
        valid.edit.receipt.new_revision,
        profiled.revision.next().unwrap()
    );
    generate(&valid.edit.document);

    let cap_id = path_id("longitudinal-cap");
    let cap_source = add_path(
        &workspace(),
        cap_id,
        "Cap longitudinal",
        vec![[-252.0, 30.0], [-232.0, 30.0]],
    );
    let cap_hash = cap_source.content_hash().unwrap();
    assert!(matches!(
        preview_upsert_river(
            &cap_source,
            river_command(
                &cap_source,
                river(cap_id, 8.0, 1.5, 4.0),
                "longitudinal-cap"
            )
        ),
        Err(RiverCommandError::Generation(
            RiverGenerationError::FootprintOutOfBounds { .. }
        ))
    ));
    assert_eq!(cap_source.content_hash().unwrap(), cap_hash);
}

#[test]
fn generation_rejects_identity_missing_and_closed_sources() {
    let id = path_id("documents");
    let source = add_path(
        &workspace(),
        id,
        "Document",
        vec![[-12.0, -8.0], [0.0, -8.0], [12.0, -8.0]],
    );
    let area = source.area(module_id(), area_id()).unwrap();
    let valid = RiverDocument::new(
        project_id(),
        area_id(),
        area.rivers.revision,
        "Rivières",
        vec![river(id, 8.0, 1.5, 4.0)],
    );

    let mut wrong_project = valid.clone();
    wrong_project.project_id = ProjectId::deterministic("river-world-test/foreign");
    assert!(matches!(
        RiverGenerationPlan::from_documents(
            &area.terrain,
            &area.paths,
            &area.roads,
            &wrong_project
        ),
        Err(RiverGenerationError::ProjectIdentityMismatch)
    ));
    let mut wrong_area = valid.clone();
    wrong_area.area_id = AreaId::deterministic("river-world-test/foreign-area");
    assert!(matches!(
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &wrong_area),
        Err(RiverGenerationError::AreaIdentityMismatch)
    ));

    let missing_id = path_id("documents-missing");
    let missing = RiverDocument::new(
        project_id(),
        area_id(),
        area.rivers.revision,
        "Rivières",
        vec![river(missing_id, 8.0, 1.5, 4.0)],
    );
    assert!(matches!(
        RiverGenerationPlan::from_documents(&area.terrain, &area.paths, &area.roads, &missing),
        Err(RiverGenerationError::MissingPath(found)) if found == missing_id
    ));

    let mut closed_paths = area.paths.clone();
    closed_paths.paths[0].closed = true;
    closed_paths.validate().unwrap();
    assert!(matches!(
        RiverGenerationPlan::from_documents(
            &area.terrain,
            &closed_paths,
            &area.roads,
            &valid
        ),
        Err(RiverGenerationError::ClosedPath(found)) if found == id
    ));
}

#[test]
fn budgets_invalid_geometry_and_overlap_work_are_checked() {
    assert!(matches!(
        checked_mesh_counts(MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES + 1),
        Err(RiverGenerationError::LongitudinalSampleBudgetExceeded { .. })
    ));
    assert!(matches!(
        checked_mesh_counts(0),
        Err(RiverGenerationError::ArithmeticOverflow)
    ));
    assert!(ensure_effective_sample_budget(MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES).is_ok());
    assert!(matches!(
        ensure_effective_sample_budget(MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES + 1),
        Err(RiverGenerationError::EffectiveTerrainSampleBudgetExceeded { .. })
    ));
    assert!(matches!(
        f32_from_derived(f64::MAX, path_id("non-finite"), "test"),
        Err(RiverGenerationError::InvalidDerivedValue { .. })
    ));
    assert!(matches!(
        grid_index_range(f64::NAN, 0.0),
        Err(RiverGenerationError::InvalidGridBounds)
    ));
    let id = path_id("degenerate");
    assert!(matches!(
        build_normals(
            id,
            &[[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [2.0, 0.0, 0.0]],
            &[0, 1, 2]
        ),
        Err(RiverGenerationError::InvalidTriangle { path_id, .. }) if path_id == id
    ));
    assert!(matches!(
        build_indices(2, 11),
        Err(RiverGenerationError::IndexCountMismatch {
            expected: 11,
            generated: 12
        })
    ));

    let mut comparisons = MAX_RIVER_OVERLAP_COMPARISONS;
    assert!(matches!(
        polylines_overlap(
            &[[0.0, 0.0], [10.0, 0.0]],
            1.0,
            &[[5.0, -5.0], [5.0, 5.0]],
            1.0,
            &mut comparisons
        ),
        Err(RiverGenerationError::OverlapComparisonBudgetExceeded { .. })
    ));
}
