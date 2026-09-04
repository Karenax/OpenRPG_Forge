use super::*;
use crate::{
    AuthoredPath, MAX_PATH_CONTROL_POINTS, MAX_PATHS_PER_DOCUMENT,
    MAX_PROMOTED_VEGETATION_INSTANCES, MAX_VEGETATION_EXCLUSIONS, MAX_WORKSPACE_AREAS,
    MAX_WORKSPACE_MODULES, NewProjectCommand, PathControlPoint, PathDocumentError, PathId,
    PathInterpolation, PromotedVegetationInstance, TerrainMaterialLayerId, TerrainMaterialRole,
    TerrainMaterialWeightOverride, VegetationDensityError, VegetationDensityOverride,
    VegetationDocumentError, VegetationExclusion, VegetationExclusionId,
    VegetationGenerationAlgorithm, VegetationInstanceTransform, VegetationPromotionSource,
    VegetationSpeciesPreset,
};
use forge_core::{ContentHash, EntityId, ProjectId, Vec3};
use serde_json::json;

fn source() -> WorkspaceDocument {
    WorkspaceDocument::create(NewProjectCommand {
        command_id: CommandId::deterministic("create"),
        project_id: ProjectId::deterministic("project"),
        module_id: ModuleId::deterministic("module"),
        area_id: AreaId::deterministic("area"),
        project_name: "Projet".into(),
        module_name: "Module".into(),
        area_name: "Zone".into(),
        seed: u64::MAX,
        spawn: Vec3::new(12.0, 17.5, -32.0),
    })
    .unwrap()
    .document
}
fn edit(document: &WorkspaceDocument, change: WorkspaceChange) -> WorkspaceEdit {
    WorkspaceEdit {
        command_id: CommandId::deterministic("edit"),
        base_revision: document.revision,
        change,
    }
}

fn circle_exclusion(id: impl Into<String>, center_x_m: f32) -> VegetationExclusion {
    VegetationExclusion::Circle {
        id: VegetationExclusionId::new(id),
        center_x_m,
        center_z_m: 0.0,
        radius_m: 2.0,
    }
}

fn catalogue_change(document: &WorkspaceDocument, layers: Vec<VegetationLayer>) -> WorkspaceChange {
    let module = &document.project.modules[0];
    let area = &module.areas[0];
    WorkspaceChange::SetVegetationCatalog {
        module_id: module.id,
        area_id: area.id,
        expected_vegetation_hash: area.vegetation.content_hash().unwrap(),
        expected_density_hash: area.vegetation_density.content_hash().unwrap(),
        layers,
    }
}

fn promoted_instance(document: &WorkspaceDocument, key: &str) -> PromotedVegetationInstance {
    let area = &document.project.modules[0].areas[0];
    let layer = &area.vegetation.layers[0];
    let transform = VegetationInstanceTransform::new(Vec3::new(12.0, 3.0, -24.0), 0.75, 1.2);
    PromotedVegetationInstance {
        id: EntityId::deterministic(format!("workspace-promotion/{key}")),
        source: VegetationPromotionSource {
            workspace_revision: document.revision,
            generation_algorithm: VegetationGenerationAlgorithm::ForgeScatterV1,
            terrain_document_hash: area.terrain.content_hash().unwrap(),
            vegetation_document_hash: area.vegetation.content_hash().unwrap(),
            vegetation_density_document_hash: area.vegetation_density.content_hash().unwrap(),
            layer_id: layer.id.clone(),
            kind: layer.kind,
            transform,
            wind_phase_radians: 1.5,
            wind_response: layer.wind_response,
            slope_gradient_squared: 0.25,
            material_weight: Some(0.75),
        },
        authored_transform: transform,
    }
}

fn promotion_change(
    document: &WorkspaceDocument,
    promoted_instances: Vec<PromotedVegetationInstance>,
) -> WorkspaceChange {
    let module = &document.project.modules[0];
    let area = &module.areas[0];
    WorkspaceChange::SetVegetationPromotedInstances {
        module_id: module.id,
        area_id: area.id,
        expected_vegetation_hash: area.vegetation.content_hash().unwrap(),
        promoted_instances,
    }
}

fn authored_path(key: &str, offset_m: f32) -> AuthoredPath {
    AuthoredPath {
        id: PathId::deterministic(format!("workspace-path/{key}")),
        name: format!("Chemin {key}"),
        closed: false,
        interpolation: PathInterpolation::CentripetalCatmullRomV1,
        control_points: vec![
            PathControlPoint::new(Vec3::new(-8.0 + offset_m, 1.0, -4.0)),
            PathControlPoint::new(Vec3::new(offset_m, 2.0, 0.0)),
            PathControlPoint::new(Vec3::new(8.0 + offset_m, 1.0, 4.0)),
        ],
    }
}

fn path_change(document: &WorkspaceDocument, path: AuthoredPath) -> WorkspaceChange {
    let module = &document.project.modules[0];
    let area = &module.areas[0];
    WorkspaceChange::UpsertPath {
        module_id: module.id,
        area_id: area.id,
        expected_path_document_hash: area.paths.content_hash().unwrap(),
        path,
    }
}

fn legacy_json(document: &WorkspaceDocument) -> String {
    let mut value = serde_json::to_value(document).unwrap();
    value["schema_version"] = json!(1);
    let mut module = value["project"]["modules"][0].clone();
    let mut area = module["areas"][0].clone();
    area.as_object_mut().unwrap().remove("vegetation");
    area.as_object_mut().unwrap().remove("vegetation_density");
    area.as_object_mut().unwrap().remove("paths");
    module.as_object_mut().unwrap().remove("areas");
    module["area"] = area;
    value["project"].as_object_mut().unwrap().remove("modules");
    value["project"]["module"] = module;
    value.to_string()
}

fn legacy_v2_json(document: &WorkspaceDocument) -> String {
    let mut value = serde_json::to_value(document).unwrap();
    value["schema_version"] = json!(2);
    for module in value["project"]["modules"].as_array_mut().unwrap() {
        for area in module["areas"].as_array_mut().unwrap() {
            area.as_object_mut().unwrap().remove("vegetation");
            area.as_object_mut().unwrap().remove("vegetation_density");
            area.as_object_mut().unwrap().remove("paths");
        }
    }
    value.to_string()
}

fn legacy_v3_json(document: &WorkspaceDocument) -> String {
    let mut value = serde_json::to_value(document).unwrap();
    value["schema_version"] = json!(3);
    for module in value["project"]["modules"].as_array_mut().unwrap() {
        for area in module["areas"].as_array_mut().unwrap() {
            area.as_object_mut().unwrap().remove("paths");
        }
    }
    value.to_string()
}
#[test]
fn legacy_migration_preserves_every_known_field_without_mutating_input() {
    let document = source();
    let original = legacy_json(&document);
    let migrated = WorkspaceDocument::from_json(&original).unwrap();
    assert_eq!(migrated, document);
    assert_eq!(migrated.project.modules[0].areas[0].terrain.seed, u64::MAX);
    assert_eq!(legacy_json(&migrated), original);
    assert_eq!(
        WorkspaceDocument::from_json(&migrated.to_pretty_json().unwrap()).unwrap(),
        document
    );
}

#[test]
fn legacy_workspace_migrates_embedded_v1_and_v2_terrains_to_the_current_schema() {
    for terrain_version in [
        crate::TERRAIN_DOCUMENT_SCHEMA_VERSION_V1,
        crate::TERRAIN_DOCUMENT_SCHEMA_VERSION_V2,
    ] {
        let mut legacy = source();
        let terrain = &mut legacy.project.modules[0].areas[0].terrain;
        terrain.schema_version = terrain_version;
        if terrain_version == crate::TERRAIN_DOCUMENT_SCHEMA_VERSION_V2 {
            terrain.height_deltas = vec![crate::TerrainHeightDelta::new(128, 128, 3.5)];
        }
        let expected_heights = terrain.height_deltas.clone();
        let ids = (terrain.project_id, terrain.area_id);
        let revisions = (legacy.revision, terrain.revision);

        let migrated = WorkspaceDocument::from_json(&legacy_json(&legacy)).unwrap();
        let terrain = &migrated.project.modules[0].areas[0].terrain;
        assert_eq!(
            terrain.schema_version,
            crate::TERRAIN_DOCUMENT_SCHEMA_VERSION
        );
        assert_eq!(terrain.height_deltas, expected_heights);
        assert!(terrain.material_weight_overrides.is_empty());
        assert_eq!((terrain.project_id, terrain.area_id), ids);
        assert_eq!((migrated.revision, terrain.revision), revisions);
    }
}

#[test]
fn workspace_v2_migration_preserves_authored_terrain_and_synthesizes_an_empty_mask() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let terrain_hash = source
        .area(module_id, area_id)
        .unwrap()
        .terrain
        .content_hash()
        .unwrap();
    let authored = source
        .preview_edit(edit(
            &source,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: terrain_hash,
                material_weight_overrides: vec![TerrainMaterialWeightOverride::new(
                    128,
                    128,
                    [0.0, 1.0, 0.0],
                )],
            },
        ))
        .unwrap()
        .document;
    let json = legacy_v2_json(&authored);
    let migrated = WorkspaceDocument::from_json(&json).unwrap();
    let replay = WorkspaceDocument::from_json(&json).unwrap();
    let migrated_area = migrated.area(module_id, area_id).unwrap();
    let authored_area = authored.area(module_id, area_id).unwrap();

    assert_eq!(migrated.revision, authored.revision);
    assert_eq!(migrated_area.terrain, authored_area.terrain);
    assert_eq!(migrated, replay);
    assert_eq!(migrated_area.vegetation.seed, authored_area.terrain.seed);
    assert_eq!(
        migrated_area.vegetation.revision,
        authored_area.terrain.revision
    );
    assert!(
        migrated_area
            .vegetation_density
            .layers
            .iter()
            .all(|layer| layer.default_density == 0 && layer.overrides.is_empty())
    );
}

#[test]
fn workspace_v3_migration_adds_only_empty_paths_and_migrates_embedded_sources() {
    let mut legacy = source();
    let project_id = legacy.project.id;
    let module_id = legacy.project.modules[0].id;
    let area = &mut legacy.project.modules[0].areas[0];
    area.terrain.schema_version = crate::TERRAIN_DOCUMENT_SCHEMA_VERSION_V2;
    area.terrain.height_deltas = vec![crate::TerrainHeightDelta::new(128, 128, 3.5)];
    area.vegetation.schema_version = crate::VEGETATION_DOCUMENT_SCHEMA_VERSION_V1;
    let identities = (
        project_id,
        module_id,
        area.id,
        area.terrain.project_id,
        area.vegetation.area_id,
    );
    let revisions = (
        legacy.revision,
        area.terrain.revision,
        area.vegetation.revision,
        area.vegetation_density.revision,
    );
    let density = area.vegetation_density.clone();
    let json = legacy_v3_json(&legacy);
    let untouched = json.clone();

    let migrated = WorkspaceDocument::from_json(&json).unwrap();
    assert_eq!(json, untouched);
    assert_eq!(migrated.schema_version, crate::WORKSPACE_SCHEMA_VERSION);
    let area = &migrated.project.modules[0].areas[0];
    assert!(area.paths.paths.is_empty());
    assert_eq!(area.paths.project_id, migrated.project.id);
    assert_eq!(area.paths.area_id, area.id);
    assert_eq!(area.paths.name, area.name);
    assert_eq!(
        area.paths.revision,
        revisions.1.max(revisions.2).max(revisions.3)
    );
    assert_eq!(
        area.terrain.height_deltas,
        vec![crate::TerrainHeightDelta::new(128, 128, 3.5)]
    );
    assert_eq!(
        area.terrain.schema_version,
        crate::TERRAIN_DOCUMENT_SCHEMA_VERSION
    );
    assert_eq!(
        area.vegetation.schema_version,
        crate::VEGETATION_DOCUMENT_SCHEMA_VERSION
    );
    assert_eq!(area.vegetation_density, density);
    assert_eq!(
        (
            migrated.project.id,
            migrated.project.modules[0].id,
            area.id,
            area.terrain.project_id,
            area.vegetation.area_id,
        ),
        identities
    );
    assert_eq!(migrated.revision, revisions.0);
    assert_eq!(WorkspaceDocument::from_json(&json).unwrap(), migrated);
}

#[test]
fn workspace_v3_migration_preserves_current_embedded_hashes_but_has_a_new_workspace_hash() {
    let source = source();
    let source_area = &source.project.modules[0].areas[0];
    let embedded_hashes = (
        source_area.terrain.content_hash().unwrap(),
        source_area.vegetation.content_hash().unwrap(),
        source_area.vegetation_density.content_hash().unwrap(),
    );
    let json = legacy_v3_json(&source);
    let legacy_value: serde_json::Value = serde_json::from_str(&json).unwrap();
    let legacy_workspace_hash = forge_core::canonical_json_sha256(&legacy_value).unwrap();

    let migrated = WorkspaceDocument::from_json(&json).unwrap();
    let area = &migrated.project.modules[0].areas[0];
    assert_eq!(area.terrain.content_hash().unwrap(), embedded_hashes.0);
    assert_eq!(area.vegetation.content_hash().unwrap(), embedded_hashes.1);
    assert_eq!(
        area.vegetation_density.content_hash().unwrap(),
        embedded_hashes.2
    );
    assert!(area.paths.paths.is_empty());
    assert_ne!(migrated.content_hash().unwrap(), legacy_workspace_hash);
}

#[test]
fn legacy_migration_binds_the_default_recipe_to_the_preserved_grass_material_id() {
    for serialize_legacy in [
        legacy_json as fn(&WorkspaceDocument) -> String,
        legacy_v2_json as fn(&WorkspaceDocument) -> String,
    ] {
        let mut legacy = source();
        legacy.project.modules[0].areas[0].terrain.material_layers[0].id =
            TerrainMaterialLayerId::new("legacy_green");

        let migrated = WorkspaceDocument::from_json(&serialize_legacy(&legacy)).unwrap();
        let area = &migrated.project.modules[0].areas[0];
        assert!(area.vegetation.layers.iter().all(|layer| {
            layer.material_filter.as_ref().is_none_or(|filter| {
                filter.material_role != TerrainMaterialRole::Grass
                    || filter.material_layer_id.as_str() == "legacy_green"
            })
        }));
        assert_eq!(area.terrain.material_layers[0].id.as_str(), "legacy_green");
    }
}

#[test]
fn migration_rejects_unknown_duplicate_and_mixed_shape_fields() {
    let legacy = legacy_json(&source());
    for pointer in [
        "",
        "/project",
        "/project/module",
        "/project/module/area",
        "/project/module/area/terrain",
    ] {
        let mut value: serde_json::Value = serde_json::from_str(&legacy).unwrap();
        value.pointer_mut(pointer).unwrap()["unknown"] = json!(true);
        assert!(WorkspaceDocument::from_json(&value.to_string()).is_err());
    }
    assert!(
        WorkspaceDocument::from_json(&legacy.replacen(
            "\"schema_version\":1",
            "\"schema_version\":1,\"schema_version\":1",
            1
        ))
        .is_err()
    );
    assert!(
        WorkspaceDocument::from_json(&legacy.replacen(
            "\"name\":\"Projet\"",
            "\"name\":\"Projet\",\"name\":\"other\"",
            1
        ))
        .is_err()
    );
    let mut mixed: serde_json::Value = serde_json::from_str(&legacy).unwrap();
    mixed["project"]["modules"] = json!([]);
    assert!(WorkspaceDocument::from_json(&mixed.to_string()).is_err());

    let current = source();
    let mut mixed_v3: serde_json::Value = serde_json::from_str(&legacy_v3_json(&current)).unwrap();
    mixed_v3["project"]["modules"][0]["areas"][0]["paths"] =
        serde_json::to_value(&current.project.modules[0].areas[0].paths).unwrap();
    assert!(WorkspaceDocument::from_json(&mixed_v3.to_string()).is_err());
}

#[test]
fn upsert_path_creates_edits_sorts_reopens_and_replays_from_its_inverse() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let source_area = source.area(module_id, area_id).unwrap().clone();
    let source_hash = source.content_hash().unwrap();

    let later_id_path = authored_path("later", 16.0);
    let first = source
        .preview_edit(edit(&source, path_change(&source, later_id_path.clone())))
        .unwrap();
    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert!(
        source
            .area(module_id, area_id)
            .unwrap()
            .paths
            .paths
            .is_empty()
    );
    assert_eq!(first.document.revision, source.revision.next().unwrap());
    let first_area = first.document.area(module_id, area_id).unwrap();
    assert_eq!(first_area.paths.revision, first.document.revision);
    assert_eq!(first_area.paths.paths, vec![later_id_path]);
    assert_eq!(first_area.terrain, source_area.terrain);
    assert_eq!(first_area.vegetation, source_area.vegetation);
    assert_eq!(
        first_area.vegetation_density,
        source_area.vegetation_density
    );
    assert_eq!(*first.receipt.inverse_snapshot, source);

    let earlier_id_path = authored_path("earlier", -16.0);
    let second_command = edit(
        &first.document,
        path_change(&first.document, earlier_id_path),
    );
    let second = first.document.preview_edit(second_command.clone()).unwrap();
    assert!(
        second
            .document
            .area(module_id, area_id)
            .unwrap()
            .paths
            .paths
            .windows(2)
            .all(|pair| pair[0].id < pair[1].id)
    );
    let reopened =
        WorkspaceDocument::from_json(&second.document.to_pretty_json().unwrap()).unwrap();
    assert_eq!(reopened, second.document);
    let undone = *second.receipt.inverse_snapshot.clone();
    assert_eq!(undone, first.document);
    let replayed = undone.preview_edit(second_command).unwrap();
    assert_eq!(replayed.document, second.document);
    assert_eq!(replayed.receipt.after_hash, second.receipt.after_hash);

    let mut edited = second
        .document
        .area(module_id, area_id)
        .unwrap()
        .paths
        .paths[0]
        .clone();
    edited.name = "Chemin retouché".into();
    edited.control_points[1].position_m.x += 1.0;
    let third = second
        .document
        .preview_edit(edit(
            &second.document,
            path_change(&second.document, edited.clone()),
        ))
        .unwrap();
    let paths = &third.document.area(module_id, area_id).unwrap().paths.paths;
    assert_eq!(
        paths.iter().find(|path| path.id == edited.id),
        Some(&edited)
    );
    assert_eq!(paths.len(), 2);
    assert_eq!(
        third.document.revision,
        second.document.revision.next().unwrap()
    );
}

#[test]
fn upsert_path_rejects_noop_stale_missing_and_invalid_inputs_atomically() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let path = authored_path("atomic", 0.0);
    let created = source
        .preview_edit(edit(&source, path_change(&source, path.clone())))
        .unwrap()
        .document;
    let created_hash = created.content_hash().unwrap();

    assert!(matches!(
        created.preview_edit(edit(&created, path_change(&created, path.clone()))),
        Err(WorkspaceError::NoChange)
    ));

    let stale_change = WorkspaceChange::UpsertPath {
        module_id,
        area_id,
        expected_path_document_hash: source
            .area(module_id, area_id)
            .unwrap()
            .paths
            .content_hash()
            .unwrap(),
        path: authored_path("stale", 8.0),
    };
    assert!(matches!(
        created.preview_edit(edit(&created, stale_change)),
        Err(WorkspaceError::StaleRevision)
    ));

    let mut invalid = authored_path("invalid", 0.0);
    invalid.control_points[1] = invalid.control_points[0];
    assert!(matches!(
        created.preview_edit(edit(&created, path_change(&created, invalid))),
        Err(WorkspaceError::Path(
            PathDocumentError::ControlPointsTooClose { .. }
        ))
    ));

    for change in [
        WorkspaceChange::UpsertPath {
            module_id: ModuleId::deterministic("missing"),
            area_id,
            expected_path_document_hash: created
                .area(module_id, area_id)
                .unwrap()
                .paths
                .content_hash()
                .unwrap(),
            path: authored_path("missing-module", 0.0),
        },
        WorkspaceChange::UpsertPath {
            module_id,
            area_id: AreaId::deterministic("missing"),
            expected_path_document_hash: created
                .area(module_id, area_id)
                .unwrap()
                .paths
                .content_hash()
                .unwrap(),
            path: authored_path("missing-area", 0.0),
        },
    ] {
        assert!(matches!(
            created.preview_edit(edit(&created, change)),
            Err(WorkspaceError::MissingTarget(_))
        ));
    }
    assert_eq!(created.content_hash().unwrap(), created_hash);
    assert_eq!(
        created.area(module_id, area_id).unwrap().paths.paths,
        vec![path]
    );
}

#[test]
fn upsert_path_enforces_document_and_per_path_limits_before_commit() {
    let mut full = source();
    let mut paths = (0..MAX_PATHS_PER_DOCUMENT)
        .map(|index| authored_path(&format!("limit-{index}"), 0.0))
        .collect::<Vec<_>>();
    paths.sort_by_key(|path| path.id);
    full.project.modules[0].areas[0].paths.paths = paths;
    full.validate().unwrap();
    let hash = full.content_hash().unwrap();
    assert!(matches!(
        full.preview_edit(edit(
            &full,
            path_change(&full, authored_path("overflow", 0.0))
        )),
        Err(WorkspaceError::Path(PathDocumentError::TooManyPaths(_)))
    ));
    assert_eq!(full.content_hash().unwrap(), hash);

    let mut too_many_points = authored_path("too-many-points", 0.0);
    too_many_points.control_points = (0_u16..=u16::try_from(MAX_PATH_CONTROL_POINTS).unwrap())
        .map(|index| PathControlPoint::new(Vec3::new(f32::from(index) - 32.0, 0.0, 0.0)))
        .collect();
    let clean = source();
    assert!(matches!(
        clean.preview_edit(edit(&clean, path_change(&clean, too_many_points))),
        Err(WorkspaceError::Path(
            PathDocumentError::TooManyControlPoints { .. }
        ))
    ));
}

#[test]
fn add_module_then_area_is_deterministic_pure_and_reversible_by_receipt() {
    let original = source();
    let id = ModuleId::deterministic("second");
    let command = edit(
        &original,
        WorkspaceChange::AddModule {
            module_id: id,
            name: "Chapitre 2".into(),
        },
    );
    let result = original.preview_edit(command.clone()).unwrap();
    assert_eq!(original, source());
    assert_eq!(result, original.preview_edit(command).unwrap());
    assert_eq!(*result.receipt.inverse_snapshot, original);
    assert_eq!(result.receipt.before_hash, original.content_hash().unwrap());
    assert_eq!(
        result.receipt.after_hash,
        result.document.content_hash().unwrap()
    );
    assert!(result.document.module(id).unwrap().areas.is_empty());
    let area_id = AreaId::deterministic("new-area");
    let command = edit(
        &result.document,
        WorkspaceChange::AddExteriorArea {
            module_id: id,
            area_id,
            name: "Colline".into(),
            seed: 73,
            spawn: Vec3::new(1.0, 2.0, 3.0),
        },
    );
    let next = result.document.preview_edit(command).unwrap().document;
    assert_eq!(next.revision.get(), 3);
    assert_eq!(next.project.modules[0], original.project.modules[0]);
    let area = next.area(id, area_id).unwrap();
    assert_eq!(area.terrain.revision, next.revision);
    assert_eq!(area.terrain.seed, 73);
    assert_eq!(area.terrain.project_id, original.project.id);
    assert_eq!(area.vegetation.revision, next.revision);
    assert_eq!(area.vegetation.seed, 73);
    assert_eq!(area.vegetation_density.revision, next.revision);
    assert_eq!(area.paths.revision, next.revision);
    assert!(area.paths.paths.is_empty());
    assert!(
        area.vegetation_density
            .layers
            .iter()
            .all(|layer| layer.default_density == 0 && layer.overrides.is_empty())
    );
    assert!(next.area(original.project.modules[0].id, area_id).is_none());
    assert_eq!(
        WorkspaceDocument::from_json(&next.to_pretty_json().unwrap()).unwrap(),
        next
    );
}
#[test]
fn metadata_targets_only_selected_area_and_preserves_relief_and_ids() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let result = source
        .preview_edit(edit(
            &source,
            WorkspaceChange::UpdateAreaMetadata {
                module_id,
                area_id,
                name: "Zone renommée".into(),
                spawn: Vec3::new(-256.0, 1024.0, 256.0),
            },
        ))
        .unwrap()
        .document;
    let area = result.area(module_id, area_id).unwrap();
    let old = source.area(module_id, area_id).unwrap();
    assert_eq!(area.name, area.terrain.name);
    assert_eq!(area.name, area.vegetation.name);
    assert_eq!(area.name, area.vegetation_density.name);
    assert_eq!(area.name, area.paths.name);
    assert_eq!(area.terrain.revision, result.revision);
    assert_eq!(area.vegetation.revision, result.revision);
    assert_eq!(area.vegetation_density.revision, result.revision);
    assert_eq!(area.paths.revision, result.revision);
    assert_eq!(area.terrain.height_sampling, old.terrain.height_sampling);
    assert_eq!(area.terrain.seed, old.terrain.seed);
    assert_eq!(area.vegetation.seed, old.vegetation.seed);
    assert_eq!(area.vegetation.layers, old.vegetation.layers);
    assert_eq!(
        area.vegetation_density.layers,
        old.vegetation_density.layers
    );
    assert_eq!(area.paths.paths, old.paths.paths);
    assert_eq!(area.spawn.y.to_bits(), 1024.0_f32.to_bits());
    assert_eq!(result.revision.get(), 2);
}

#[test]
fn set_terrain_material_weights_is_pure_revisioned_and_reversible() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let terrain_hash = source
        .area(module_id, area_id)
        .unwrap()
        .terrain
        .content_hash()
        .unwrap();
    let weights = vec![
        TerrainMaterialWeightOverride::new(128, 128, [0.1, 0.8, 0.1]),
        TerrainMaterialWeightOverride::new(129, 128, [0.2, 0.3, 0.5]),
    ];

    let result = source
        .preview_edit(edit(
            &source,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: terrain_hash,
                material_weight_overrides: weights.clone(),
            },
        ))
        .unwrap();

    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert!(
        source
            .area(module_id, area_id)
            .unwrap()
            .terrain
            .material_weight_overrides
            .is_empty()
    );
    let terrain = &result.document.area(module_id, area_id).unwrap().terrain;
    assert_eq!(terrain.material_weight_overrides, weights);
    assert_eq!(result.document.revision, source.revision.next().unwrap());
    assert_eq!(terrain.revision, result.document.revision);
    assert_eq!(result.receipt.before_hash, source_hash);
    assert_eq!(
        result.receipt.after_hash,
        result.document.content_hash().unwrap()
    );
    assert_eq!(*result.receipt.inverse_snapshot, source);
    assert_eq!(
        WorkspaceDocument::from_json(&result.document.to_pretty_json().unwrap()).unwrap(),
        result.document
    );
}

#[test]
fn set_terrain_material_weights_rejects_stale_hash_without_mutating_source() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let stale_hash = ContentHash::from_bytes([0xA5; 32]);
    assert_ne!(
        stale_hash,
        source
            .area(module_id, area_id)
            .unwrap()
            .terrain
            .content_hash()
            .unwrap()
    );

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: stale_hash,
                material_weight_overrides: vec![TerrainMaterialWeightOverride::new(
                    128,
                    128,
                    [0.0, 1.0, 0.0],
                )],
            },
        )),
        Err(WorkspaceError::StaleRevision)
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}

#[test]
fn set_terrain_material_weights_rejects_invalid_payloads_without_mutating_source() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let terrain_hash = source
        .area(module_id, area_id)
        .unwrap()
        .terrain
        .content_hash()
        .unwrap();
    let invalid_payloads = [
        vec![TerrainMaterialWeightOverride::new(257, 0, [1.0, 0.0, 0.0])],
        vec![TerrainMaterialWeightOverride::new(
            128,
            128,
            [0.2, 0.2, 0.2],
        )],
        vec![
            TerrainMaterialWeightOverride::new(1, 1, [1.0, 0.0, 0.0]),
            TerrainMaterialWeightOverride::new(0, 0, [0.0, 1.0, 0.0]),
        ],
        vec![
            TerrainMaterialWeightOverride::new(0, 0, [1.0, 0.0, 0.0]),
            TerrainMaterialWeightOverride::new(0, 0, [0.0, 1.0, 0.0]),
        ],
        vec![TerrainMaterialWeightOverride::new(
            128,
            128,
            [-0.0, 1.0, 0.0],
        )],
    ];

    for material_weight_overrides in invalid_payloads {
        assert!(matches!(
            source.preview_edit(edit(
                &source,
                WorkspaceChange::SetTerrainMaterialWeights {
                    module_id,
                    area_id,
                    expected_terrain_hash: terrain_hash,
                    material_weight_overrides,
                },
            )),
            Err(WorkspaceError::Terrain(_))
        ));
        assert_eq!(source.content_hash().unwrap(), source_hash);
    }
}

#[test]
fn material_weight_noop_is_rejected_and_success_invalidates_the_previous_hash() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let original_terrain_hash = source
        .area(module_id, area_id)
        .unwrap()
        .terrain
        .content_hash()
        .unwrap();
    let weights = vec![TerrainMaterialWeightOverride::new(
        128,
        128,
        [0.0, 1.0, 0.0],
    )];

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: original_terrain_hash,
                material_weight_overrides: vec![],
            },
        )),
        Err(WorkspaceError::NoChange)
    ));

    let painted = source
        .preview_edit(edit(
            &source,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: original_terrain_hash,
                material_weight_overrides: weights.clone(),
            },
        ))
        .unwrap()
        .document;
    let painted_terrain_hash = painted
        .area(module_id, area_id)
        .unwrap()
        .terrain
        .content_hash()
        .unwrap();
    assert_ne!(painted_terrain_hash, original_terrain_hash);

    assert!(matches!(
        painted.preview_edit(edit(
            &painted,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: painted_terrain_hash,
                material_weight_overrides: weights,
            },
        )),
        Err(WorkspaceError::NoChange)
    ));
    assert!(matches!(
        painted.preview_edit(edit(
            &painted,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: original_terrain_hash,
                material_weight_overrides: vec![],
            },
        )),
        Err(WorkspaceError::StaleRevision)
    ));

    let cleared = painted
        .preview_edit(edit(
            &painted,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: painted_terrain_hash,
                material_weight_overrides: vec![],
            },
        ))
        .unwrap()
        .document;
    assert!(
        cleared
            .area(module_id, area_id)
            .unwrap()
            .terrain
            .material_weight_overrides
            .is_empty()
    );
}

#[test]
fn set_vegetation_catalogue_is_atomic_and_retains_density_by_stable_id() {
    let mut source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    source.project.modules[0].areas[0].vegetation_density.layers[0].overrides =
        vec![VegetationDensityOverride::new(12, 14, 192)];
    source.validate().unwrap();
    let source_area = source.area(module_id, area_id).unwrap();
    let retained_id = source_area.vegetation.layers[0].id.clone();
    let before_terrain = source_area.terrain.clone();
    let before_revision = source.revision;
    let mut layers = source_area.vegetation.layers.clone();
    layers[0].display_name = "Chênes de la vallée".into();
    let removed_id = layers.remove(1).id;
    let grass_material = source_area.terrain.material_layers[0].id.clone();
    layers.push(VegetationSpeciesPreset::Fern.build_layer(
        "species_01",
        "Fougère de sous-bois",
        grass_material,
    ));
    let command = edit(&source, catalogue_change(&source, layers.clone()));

    let result = source.preview_edit(command.clone()).unwrap();
    let edited = result.document.area(module_id, area_id).unwrap();
    assert_eq!(result.document.revision, before_revision.next().unwrap());
    assert_eq!(edited.vegetation.revision, result.document.revision);
    assert_eq!(edited.vegetation_density.revision, result.document.revision);
    assert_eq!(edited.vegetation.layers, layers);
    assert_eq!(edited.terrain, before_terrain);
    assert_eq!(edited.vegetation_density.layers.len(), layers.len());
    assert!(edited.vegetation_density.layers.iter().all(|density| {
        edited
            .vegetation
            .layers
            .iter()
            .any(|layer| layer.id == density.layer_id)
    }));
    assert_eq!(
        edited
            .vegetation_density
            .layers
            .iter()
            .find(|density| density.layer_id == retained_id)
            .unwrap()
            .overrides,
        vec![VegetationDensityOverride::new(12, 14, 192)]
    );
    assert!(
        edited
            .vegetation_density
            .layers
            .iter()
            .all(|density| density.layer_id != removed_id)
    );
    let added_density = edited
        .vegetation_density
        .layers
        .iter()
        .find(|density| density.layer_id.as_str() == "species_01")
        .unwrap();
    assert_eq!(added_density.default_density, 0);
    assert!(added_density.overrides.is_empty());

    let reopened =
        WorkspaceDocument::from_json(&result.document.to_pretty_json().unwrap()).unwrap();
    assert_eq!(reopened, result.document);
    let inverse = *result.receipt.inverse_snapshot;
    assert_eq!(inverse, source);
    assert_eq!(
        inverse.preview_edit(command).unwrap().document,
        result.document
    );
}

#[test]
fn catalogue_parameter_edit_preserves_the_density_document_exactly() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let before = source.area(module_id, area_id).unwrap();
    let density = before.vegetation_density.clone();
    let mut layers = before.vegetation.layers.clone();
    layers[0].display_name = "Chênes anciens".into();

    let result = source
        .preview_edit(edit(&source, catalogue_change(&source, layers)))
        .unwrap();
    let edited = result.document.area(module_id, area_id).unwrap();
    assert_eq!(edited.vegetation_density, density);
    assert_eq!(edited.vegetation.revision, result.document.revision);
}

#[test]
fn catalogue_rejects_stale_noop_and_invalid_replacements_without_mutation() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module = &source.project.modules[0];
    let area = &module.areas[0];
    let layers = area.vegetation.layers.clone();

    assert!(matches!(
        source.preview_edit(edit(&source, catalogue_change(&source, layers.clone()))),
        Err(WorkspaceError::NoChange)
    ));
    let mut stale_vegetation = catalogue_change(&source, {
        let mut changed = layers.clone();
        changed[0].display_name = "Nom neuf".into();
        changed
    });
    if let WorkspaceChange::SetVegetationCatalog {
        expected_vegetation_hash,
        ..
    } = &mut stale_vegetation
    {
        *expected_vegetation_hash = ContentHash::from_bytes([0xA1; 32]);
    }
    assert!(matches!(
        source.preview_edit(edit(&source, stale_vegetation)),
        Err(WorkspaceError::StaleRevision)
    ));
    let mut stale_density = catalogue_change(&source, {
        let mut changed = layers.clone();
        changed[0].display_name = "Autre nom".into();
        changed
    });
    if let WorkspaceChange::SetVegetationCatalog {
        expected_density_hash,
        ..
    } = &mut stale_density
    {
        *expected_density_hash = ContentHash::from_bytes([0xB2; 32]);
    }
    assert!(matches!(
        source.preview_edit(edit(&source, stale_density)),
        Err(WorkspaceError::StaleRevision)
    ));
    let mut duplicate = layers;
    duplicate[1].id = duplicate[0].id.clone();
    assert!(matches!(
        source.preview_edit(edit(&source, catalogue_change(&source, duplicate))),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::DuplicateLayerId(_)
        ))
    ));
    assert!(matches!(
        source.preview_edit(edit(&source, catalogue_change(&source, vec![]))),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::NoLayers
        ))
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}

#[test]
fn set_vegetation_exclusions_is_pure_revisioned_reopenable_and_replayable() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let area = source.area(module_id, area_id).unwrap();
    let original_vegetation_hash = area.vegetation.content_hash().unwrap();
    let original_terrain_hash = area.terrain.content_hash().unwrap();
    let original_density_hash = area.vegetation_density.content_hash().unwrap();
    let original_terrain_revision = area.terrain.revision;
    let original_density_revision = area.vegetation_density.revision;
    let exclusions = vec![
        circle_exclusion("user_circle", 32.0),
        VegetationExclusion::CorridorSegment {
            id: VegetationExclusionId::new("user_corridor"),
            start_x_m: -48.0,
            start_z_m: -96.0,
            end_x_m: 48.0,
            end_z_m: -96.0,
            half_width_m: 4.0,
        },
    ];
    let command = edit(
        &source,
        WorkspaceChange::SetVegetationExclusions {
            module_id,
            area_id,
            expected_vegetation_hash: original_vegetation_hash,
            exclusions: exclusions.clone(),
        },
    );

    let result = source.preview_edit(command.clone()).unwrap();
    let edited = result.document.area(module_id, area_id).unwrap();
    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert!(
        source
            .area(module_id, area_id)
            .unwrap()
            .vegetation
            .exclusions
            .is_empty()
    );
    assert_eq!(edited.vegetation.exclusions, exclusions);
    assert_eq!(edited.vegetation.revision, result.document.revision);
    assert_eq!(edited.terrain.revision, original_terrain_revision);
    assert_eq!(
        edited.vegetation_density.revision,
        original_density_revision
    );
    assert_eq!(
        edited.terrain.content_hash().unwrap(),
        original_terrain_hash
    );
    assert_eq!(
        edited.vegetation_density.content_hash().unwrap(),
        original_density_hash
    );
    assert_ne!(
        edited.vegetation.content_hash().unwrap(),
        original_vegetation_hash
    );
    assert_eq!(result.receipt.before_hash, source_hash);
    assert_eq!(
        result.receipt.after_hash,
        result.document.content_hash().unwrap()
    );

    let reopened =
        WorkspaceDocument::from_json(&result.document.to_pretty_json().unwrap()).unwrap();
    assert_eq!(reopened, result.document);
    let inverse = *result.receipt.inverse_snapshot.clone();
    assert_eq!(inverse, source);
    let replay = inverse.preview_edit(command).unwrap();
    assert_eq!(replay.document, result.document);
    assert_eq!(replay.receipt.after_hash, result.receipt.after_hash);
}

#[test]
fn set_vegetation_exclusions_rejects_stale_hash_and_noop_without_mutation() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let exact_hash = source
        .area(module_id, area_id)
        .unwrap()
        .vegetation
        .content_hash()
        .unwrap();
    let exclusions = vec![circle_exclusion("user_circle", 0.0)];

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetVegetationExclusions {
                module_id,
                area_id,
                expected_vegetation_hash: ContentHash::from_bytes([0xA5; 32]),
                exclusions: exclusions.clone(),
            },
        )),
        Err(WorkspaceError::StaleRevision)
    ));
    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetVegetationExclusions {
                module_id,
                area_id,
                expected_vegetation_hash: exact_hash,
                exclusions: vec![],
            },
        )),
        Err(WorkspaceError::NoChange)
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);

    let edited = source
        .preview_edit(edit(
            &source,
            WorkspaceChange::SetVegetationExclusions {
                module_id,
                area_id,
                expected_vegetation_hash: exact_hash,
                exclusions,
            },
        ))
        .unwrap()
        .document;
    assert!(matches!(
        edited.preview_edit(edit(
            &edited,
            WorkspaceChange::SetVegetationExclusions {
                module_id,
                area_id,
                expected_vegetation_hash: exact_hash,
                exclusions: vec![],
            },
        )),
        Err(WorkspaceError::StaleRevision)
    ));
}

#[test]
fn set_vegetation_exclusions_enforces_geometry_identity_and_collection_bound() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let vegetation_hash = source
        .area(module_id, area_id)
        .unwrap()
        .vegetation
        .content_hash()
        .unwrap();
    let change = |exclusions| WorkspaceChange::SetVegetationExclusions {
        module_id,
        area_id,
        expected_vegetation_hash: vegetation_hash,
        exclusions,
    };

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            change(vec![
                circle_exclusion("duplicate", -4.0),
                circle_exclusion("duplicate", 4.0),
            ]),
        )),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::DuplicateExclusionId(_)
        ))
    ));
    assert!(matches!(
        source.preview_edit(edit(
            &source,
            change(vec![circle_exclusion("outside", 255.0)]),
        )),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::InvalidExclusionGeometry(_)
        ))
    ));

    let boundary = (0..MAX_VEGETATION_EXCLUSIONS)
        .map(|index| circle_exclusion(format!("exclusion_{index}"), 0.0))
        .collect::<Vec<_>>();
    let accepted = source
        .preview_edit(edit(&source, change(boundary.clone())))
        .unwrap();
    assert_eq!(
        accepted
            .document
            .area(module_id, area_id)
            .unwrap()
            .vegetation
            .exclusions,
        boundary
    );

    let too_many = (0..=MAX_VEGETATION_EXCLUSIONS)
        .map(|index| circle_exclusion(format!("exclusion_{index}"), 0.0))
        .collect::<Vec<_>>();
    assert!(matches!(
        source.preview_edit(edit(&source, change(too_many))),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::TooManyExclusions(count)
        )) if count == MAX_VEGETATION_EXCLUSIONS + 1
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}

#[test]
fn set_promoted_vegetation_is_pure_revisioned_reopenable_and_replayable() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let original_area = source.area(module_id, area_id).unwrap();
    let terrain = original_area.terrain.clone();
    let density = original_area.vegetation_density.clone();
    let promoted = promoted_instance(&source, "persistent");
    let command = edit(&source, promotion_change(&source, vec![promoted.clone()]));

    let result = source.preview_edit(command.clone()).unwrap();
    let edited = result.document.area(module_id, area_id).unwrap();
    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert!(
        source
            .area(module_id, area_id)
            .unwrap()
            .vegetation
            .promoted_instances
            .is_empty()
    );
    assert_eq!(edited.vegetation.promoted_instances, vec![promoted]);
    assert_eq!(edited.vegetation.revision, result.document.revision);
    assert_eq!(edited.terrain, terrain);
    assert_eq!(edited.vegetation_density, density);
    assert_eq!(*result.receipt.inverse_snapshot, source);
    assert_eq!(result.receipt.before_hash, source_hash);
    assert_eq!(
        result.receipt.after_hash,
        result.document.content_hash().unwrap()
    );

    let reopened =
        WorkspaceDocument::from_json(&result.document.to_pretty_json().unwrap()).unwrap();
    assert_eq!(reopened, result.document);
    let replay = result
        .receipt
        .inverse_snapshot
        .preview_edit(command)
        .unwrap();
    assert_eq!(replay.document, result.document);
    assert_eq!(replay.receipt.after_hash, result.receipt.after_hash);
}

#[test]
fn promoted_vegetation_retouch_changes_only_transform_and_survives_layer_removal() {
    let source = source();
    let initial = promoted_instance(&source, "retouch");
    let promoted = source
        .preview_edit(edit(
            &source,
            promotion_change(&source, vec![initial.clone()]),
        ))
        .unwrap()
        .document;
    let module_id = promoted.project.modules[0].id;
    let area_id = promoted.project.modules[0].areas[0].id;
    let before_area = promoted.area(module_id, area_id).unwrap();
    let terrain = before_area.terrain.clone();
    let density = before_area.vegetation_density.clone();
    let provenance = initial.source.clone();
    let mut retouched = initial;
    retouched.authored_transform =
        VegetationInstanceTransform::new(Vec3::new(24.0, 4.0, -12.0), 1.25, 1.75);

    let result = promoted
        .preview_edit(edit(
            &promoted,
            promotion_change(&promoted, vec![retouched.clone()]),
        ))
        .unwrap();
    let edited = result.document.area(module_id, area_id).unwrap();
    assert_eq!(edited.vegetation.promoted_instances[0].source, provenance);
    assert_eq!(edited.vegetation.promoted_instances[0], retouched);
    assert_eq!(edited.terrain, terrain);
    assert_eq!(edited.vegetation_density, density);

    let mut layers = edited.vegetation.layers.clone();
    layers.retain(|layer| layer.id != provenance.layer_id);
    let without_layer = result
        .document
        .preview_edit(edit(
            &result.document,
            catalogue_change(&result.document, layers),
        ))
        .unwrap()
        .document;
    assert_eq!(
        without_layer
            .area(module_id, area_id)
            .unwrap()
            .vegetation
            .promoted_instances,
        vec![retouched]
    );
}

#[test]
fn promoted_vegetation_rejects_noop_stale_forged_or_invalid_sources_atomically() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let promotion = promoted_instance(&source, "failures");
    assert!(matches!(
        source.preview_edit(edit(&source, promotion_change(&source, vec![]))),
        Err(WorkspaceError::NoChange)
    ));

    let mut stale = promotion_change(&source, vec![promotion.clone()]);
    if let WorkspaceChange::SetVegetationPromotedInstances {
        expected_vegetation_hash,
        ..
    } = &mut stale
    {
        *expected_vegetation_hash = ContentHash::from_bytes([0xA5; 32]);
    }
    assert!(matches!(
        source.preview_edit(edit(&source, stale)),
        Err(WorkspaceError::StaleRevision)
    ));

    let mut mismatched = promotion.clone();
    mismatched.source.workspace_revision = Revision::new(source.revision.get() + 1);
    assert!(matches!(
        source.preview_edit(edit(&source, promotion_change(&source, vec![mismatched]))),
        Err(WorkspaceError::VegetationPromotionSourceMismatch(_))
    ));

    let promoted = source
        .preview_edit(edit(
            &source,
            promotion_change(&source, vec![promotion.clone()]),
        ))
        .unwrap()
        .document;
    assert!(matches!(
        promoted.preview_edit(edit(&promoted, promotion_change(&promoted, vec![]))),
        Err(WorkspaceError::VegetationPromotionRemovalRequiresExplicitCommand(_))
    ));
    let mut forged = promotion.clone();
    forged.source.wind_response = 0.25;
    assert!(matches!(
        promoted.preview_edit(edit(&promoted, promotion_change(&promoted, vec![forged]))),
        Err(WorkspaceError::VegetationPromotionProvenanceChanged(_))
    ));

    let mut invalid = promotion;
    invalid.source.transform.position_m.x = 256.0;
    invalid.authored_transform = invalid.source.transform;
    assert!(matches!(
        source.preview_edit(edit(&source, promotion_change(&source, vec![invalid]))),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::InvalidPromotedInstanceValue { .. }
        ))
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}

#[test]
fn promoted_vegetation_collection_limit_is_enforced_through_the_atomic_edit() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let template = promoted_instance(&source, "limit");
    let mut instances = (0..=MAX_PROMOTED_VEGETATION_INSTANCES)
        .map(|index| {
            let mut promoted = template.clone();
            promoted.id = EntityId::deterministic(format!("workspace-promotion/{index:04}"));
            promoted
        })
        .collect::<Vec<_>>();
    instances.sort_by_key(|promoted| promoted.id);

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            promotion_change(&source, instances)
        )),
        Err(WorkspaceError::Vegetation(
            VegetationDocumentError::TooManyPromotedInstances(count)
        )) if count == MAX_PROMOTED_VEGETATION_INSTANCES + 1
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}

#[test]
fn set_vegetation_density_is_pure_revisioned_reopenable_and_replayable_after_undo() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let area = source.area(module_id, area_id).unwrap();
    let original_density_hash = area.vegetation_density.content_hash().unwrap();
    let original_terrain_hash = area.terrain.content_hash().unwrap();
    let original_recipe_hash = area.vegetation.content_hash().unwrap();
    let layer_id = area.vegetation_density.layers[3].layer_id.clone();
    let overrides = vec![
        VegetationDensityOverride::new(31, 30, 96),
        VegetationDensityOverride::new(32, 30, 192),
        VegetationDensityOverride::new(31, 31, u8::MAX),
    ];
    let command = edit(
        &source,
        WorkspaceChange::SetVegetationDensityOverrides {
            module_id,
            area_id,
            layer_id: layer_id.clone(),
            expected_density_hash: original_density_hash,
            density_overrides: overrides.clone(),
        },
    );

    let result = source.preview_edit(command.clone()).unwrap();
    let painted = result.document.area(module_id, area_id).unwrap();
    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert!(
        source
            .area(module_id, area_id)
            .unwrap()
            .vegetation_density
            .layers[3]
            .overrides
            .is_empty()
    );
    assert_eq!(painted.vegetation_density.layers[3].overrides, overrides);
    assert_eq!(
        painted.vegetation_density.density_u8(&layer_id, 32, 30),
        Some(192)
    );
    assert_eq!(
        painted.vegetation_density.revision,
        result.document.revision
    );
    assert_eq!(
        painted.terrain.content_hash().unwrap(),
        original_terrain_hash
    );
    assert_eq!(
        painted.vegetation.content_hash().unwrap(),
        original_recipe_hash
    );
    assert_eq!(result.receipt.before_hash, source_hash);
    assert_eq!(
        result.receipt.after_hash,
        result.document.content_hash().unwrap()
    );

    let reopened =
        WorkspaceDocument::from_json(&result.document.to_pretty_json().unwrap()).unwrap();
    assert_eq!(reopened, result.document);
    let undone = *result.receipt.inverse_snapshot.clone();
    assert_eq!(undone, source);
    let redone = undone.preview_edit(command).unwrap();
    assert_eq!(redone.document, result.document);
    assert_eq!(redone.receipt.after_hash, result.receipt.after_hash);
}

#[test]
fn set_vegetation_density_rejects_stale_hash_without_mutating_source() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let layer_id = source.area(module_id, area_id).unwrap().vegetation.layers[0]
        .id
        .clone();
    let stale_hash = ContentHash::from_bytes([0x5A; 32]);

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetVegetationDensityOverrides {
                module_id,
                area_id,
                layer_id,
                expected_density_hash: stale_hash,
                density_overrides: vec![VegetationDensityOverride::new(0, 0, 255)],
            },
        )),
        Err(WorkspaceError::StaleRevision)
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
}

#[test]
fn set_vegetation_density_rejects_noncanonical_or_out_of_bounds_data() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let density = &source.area(module_id, area_id).unwrap().vegetation_density;
    let density_hash = density.content_hash().unwrap();
    let layer_id = density.layers[0].layer_id.clone();
    let invalid_payloads = [
        vec![VegetationDensityOverride::new(64, 0, 255)],
        vec![
            VegetationDensityOverride::new(1, 1, 255),
            VegetationDensityOverride::new(0, 0, 255),
        ],
        vec![
            VegetationDensityOverride::new(0, 0, 255),
            VegetationDensityOverride::new(0, 0, 128),
        ],
        vec![VegetationDensityOverride::new(0, 0, 0)],
    ];

    for density_overrides in invalid_payloads {
        assert!(matches!(
            source.preview_edit(edit(
                &source,
                WorkspaceChange::SetVegetationDensityOverrides {
                    module_id,
                    area_id,
                    layer_id: layer_id.clone(),
                    expected_density_hash: density_hash,
                    density_overrides,
                },
            )),
            Err(WorkspaceError::VegetationDensity(
                VegetationDensityError::CellOutOfBounds { .. }
                    | VegetationDensityError::OverridesNotCanonical(_)
                    | VegetationDensityError::RedundantOverride { .. }
            ))
        ));
        assert_eq!(source.content_hash().unwrap(), source_hash);
    }
}

#[test]
fn vegetation_density_noop_is_rejected_and_a_success_invalidates_the_old_hash() {
    let source = source();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let density = &source.area(module_id, area_id).unwrap().vegetation_density;
    let original_hash = density.content_hash().unwrap();
    let layer_id = density.layers[0].layer_id.clone();

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetVegetationDensityOverrides {
                module_id,
                area_id,
                layer_id: layer_id.clone(),
                expected_density_hash: original_hash,
                density_overrides: vec![],
            },
        )),
        Err(WorkspaceError::NoChange)
    ));
    let painted = source
        .preview_edit(edit(
            &source,
            WorkspaceChange::SetVegetationDensityOverrides {
                module_id,
                area_id,
                layer_id: layer_id.clone(),
                expected_density_hash: original_hash,
                density_overrides: vec![VegetationDensityOverride::new(0, 0, 255)],
            },
        ))
        .unwrap()
        .document;
    let painted_hash = painted
        .area(module_id, area_id)
        .unwrap()
        .vegetation_density
        .content_hash()
        .unwrap();
    assert_ne!(painted_hash, original_hash);
    assert!(matches!(
        painted.preview_edit(edit(
            &painted,
            WorkspaceChange::SetVegetationDensityOverrides {
                module_id,
                area_id,
                layer_id,
                expected_density_hash: original_hash,
                density_overrides: vec![],
            },
        )),
        Err(WorkspaceError::StaleRevision)
    ));
}

#[test]
fn oversized_material_patch_is_rejected_before_the_source_or_receipt_can_change() {
    let source = source();
    let source_hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    let terrain_hash = source
        .area(module_id, area_id)
        .unwrap()
        .terrain
        .content_hash()
        .unwrap();
    let weights = (0_u16..15_000)
        .map(|index| TerrainMaterialWeightOverride::new(index % 257, index / 257, [0.0, 1.0, 0.0]))
        .collect();

    assert!(matches!(
        source.preview_edit(edit(
            &source,
            WorkspaceChange::SetTerrainMaterialWeights {
                module_id,
                area_id,
                expected_terrain_hash: terrain_hash,
                material_weight_overrides: weights,
            },
        )),
        Err(WorkspaceError::JsonBudgetExceeded { .. })
    ));
    assert_eq!(source.content_hash().unwrap(), source_hash);
    assert!(
        source
            .area(module_id, area_id)
            .unwrap()
            .terrain
            .material_weight_overrides
            .is_empty()
    );
}

#[test]
fn invalid_edits_are_rejected_without_changing_source() {
    let source = source();
    let hash = source.content_hash().unwrap();
    let module_id = source.project.modules[0].id;
    let area_id = source.project.modules[0].areas[0].id;
    for change in [
        WorkspaceChange::AddModule {
            module_id,
            name: "Doublon".into(),
        },
        WorkspaceChange::AddModule {
            module_id: ModuleId::deterministic("valid"),
            name: "\n".into(),
        },
        WorkspaceChange::AddExteriorArea {
            module_id,
            area_id,
            name: "Doublon zone".into(),
            seed: 0,
            spawn: Vec3::new(0.0, 0.0, 0.0),
        },
        WorkspaceChange::RenameModule {
            module_id: ModuleId::deterministic("missing"),
            name: "Nom".into(),
        },
        WorkspaceChange::UpdateAreaMetadata {
            module_id,
            area_id,
            name: "Nom".into(),
            spawn: Vec3::new(f32::NAN, 0.0, 0.0),
        },
        WorkspaceChange::UpdateAreaMetadata {
            module_id,
            area_id: AreaId::deterministic("missing"),
            name: "Nom".into(),
            spawn: Vec3::new(0.0, 0.0, 0.0),
        },
        WorkspaceChange::RenameProject {
            name: source.project.name.clone(),
        },
    ] {
        assert!(source.preview_edit(edit(&source, change)).is_err());
        assert_eq!(source.content_hash().unwrap(), hash);
    }
    let mut command = edit(
        &source,
        WorkspaceChange::RenameProject {
            name: "Renommé".into(),
        },
    );
    command.base_revision = Revision::new(0);
    assert!(matches!(
        source.preview_edit(command),
        Err(WorkspaceError::StaleRevision)
    ));
    let mut exhausted = source.clone();
    exhausted.revision = Revision::new(u64::MAX);
    assert!(matches!(
        exhausted.preview_edit(edit(
            &exhausted,
            WorkspaceChange::RenameProject { name: "Nom".into() }
        )),
        Err(WorkspaceError::RevisionExhausted)
    ));
}
#[test]
fn collection_budgets_and_global_area_identity_are_validated() {
    let mut document = source();
    let template = document.project.modules[0].clone();
    document.project.modules = (0..MAX_WORKSPACE_MODULES)
        .map(|i| WorkspaceModule {
            id: ModuleId::deterministic(format!("module-{i}")),
            name: format!("Module {i}"),
            areas: vec![],
        })
        .collect();
    document.validate().unwrap();
    document.project.modules.push(template.clone());
    assert!(matches!(
        document.validate(),
        Err(WorkspaceError::CollectionLimit("modules"))
    ));
    document.project.modules = vec![template.clone()];
    document.project.modules[0].areas = (0..MAX_WORKSPACE_AREAS)
        .map(|i| {
            let mut area = template.areas[0].clone();
            area.id = AreaId::deterministic(format!("area-{i}"));
            area.terrain.area_id = area.id;
            area.vegetation.area_id = area.id;
            area.vegetation_density.area_id = area.id;
            area.paths.area_id = area.id;
            area
        })
        .collect();
    assert!(matches!(
        document.validate(),
        Err(WorkspaceError::JsonBudgetExceeded { .. })
    ));
    document.project.modules[0]
        .areas
        .push(template.areas[0].clone());
    assert!(matches!(
        document.validate(),
        Err(WorkspaceError::CollectionLimit("areas"))
    ));
    document.project.modules = vec![
        template.clone(),
        WorkspaceModule {
            id: ModuleId::deterministic("different-module"),
            ..template
        },
    ];
    assert!(matches!(
        document.validate(),
        Err(WorkspaceError::DuplicateIdentity("area_id"))
    ));
    document.project.modules.clear();
    assert!(document.validate().is_err());
}
