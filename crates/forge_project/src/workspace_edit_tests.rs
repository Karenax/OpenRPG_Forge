use super::*;
use crate::{MAX_WORKSPACE_AREAS, MAX_WORKSPACE_MODULES, NewProjectCommand};
use forge_core::{ProjectId, Vec3};
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
fn legacy_json(document: &WorkspaceDocument) -> String {
    let mut value = serde_json::to_value(document).unwrap();
    value["schema_version"] = json!(1);
    let mut module = value["project"]["modules"][0].clone();
    let area = module["areas"][0].clone();
    module.as_object_mut().unwrap().remove("areas");
    module["area"] = area;
    value["project"].as_object_mut().unwrap().remove("modules");
    value["project"]["module"] = module;
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
    assert_eq!(area.terrain.height_sampling, old.terrain.height_sampling);
    assert_eq!(area.terrain.seed, old.terrain.seed);
    assert_eq!(area.spawn.y.to_bits(), 1024.0_f32.to_bits());
    assert_eq!(result.revision.get(), 2);
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
            area
        })
        .collect();
    document.validate().unwrap();
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
