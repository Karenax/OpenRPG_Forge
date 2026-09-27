use forge_core::CommandId;
use forge_project::{
    authoring::{
        AuthoringRequest,
        creation::{NewTerrainSettings, prepare_terrain},
        host::AuthoringHost,
    },
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest},
};

#[test]
fn fresh_aggregate_accepts_building_edits_without_a_legacy_file_or_conversion() {
    use forge_project::zone_authoring::buildings::BuildingEdit;
    let root = std::env::temp_dir().join(format!("forge-create-aggregate-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let document = prepare_terrain(
        "aggregate",
        "new",
        "Nouvelle",
        NewTerrainSettings::default(),
    )
    .unwrap();
    let request = AuthoringRequest::CreateZone {
        document: Box::new(document.clone()),
    };
    let mut host = AuthoringHost::new(&root).unwrap();
    let mut invalid = document.clone();
    invalid.revision = invalid.revision.next().unwrap();
    assert!(
        host.execute(AuthoringRequest::CreateZone {
            document: Box::new(invalid)
        })
        .is_err()
    );
    assert!(!root.join(document.area_id.to_string()).exists());
    let created = host
        .execute(serde_json::from_slice(&serde_json::to_vec(&request).unwrap()).unwrap())
        .unwrap();
    assert!(created.dirty);
    assert!(!created.can_undo);
    assert!(created.zone_migration.is_none());
    let head = created.zone_head.unwrap();
    assert_eq!(head.base.generator.name, "forge-new-zone");
    assert_eq!(
        head.base.generator.inputs_hash,
        document.content_hash().unwrap()
    );
    let archive = root
        .join(document.area_id.to_string())
        .join("authoring.json");
    assert!(!archive.exists());
    let building = house();
    let edit = head
        .prepare_building(
            CommandId::deterministic("new-house"),
            BuildingEdit::Insert {
                building: building.clone(),
            },
        )
        .unwrap();
    let preview = host
        .execute(AuthoringRequest::PreviewZone {
            request: edit.clone(),
        })
        .unwrap();
    assert_eq!(preview.zone_head.as_ref(), Some(&head));
    assert!(!archive.exists());
    let applied = host
        .execute(AuthoringRequest::ApplyZone { request: edit })
        .unwrap()
        .zone_head
        .unwrap();
    assert_eq!(applied.corrections.buildings.as_ref().unwrap(), &[building]);
    assert!(host.execute(request.clone()).is_err());
    host.execute(AuthoringRequest::Save).unwrap();
    let bytes = std::fs::read(&archive).unwrap();
    assert_fresh_seed(&bytes, &document);
    host.execute(AuthoringRequest::Close).unwrap();
    assert!(host.execute(request).is_err());
    assert_eq!(std::fs::read(&archive).unwrap(), bytes);
    let opened = host
        .execute(AuthoringRequest::Open {
            area_id: document.area_id,
        })
        .unwrap();
    assert_eq!(opened.zone_head.as_ref(), Some(&applied));
    assert!(!opened.dirty);
    for (key, action) in [
        ("undo-new-house", TerrainAction::Undo),
        ("redo-new-house", TerrainAction::Redo),
    ] {
        let current = host
            .execute(AuthoringRequest::Inspect)
            .unwrap()
            .zone_head
            .unwrap();
        let command = current
            .prepare_terrain_action(action, CommandId::deterministic(key))
            .unwrap();
        host.execute(AuthoringRequest::ApplyZone { request: command })
            .unwrap();
    }
    let final_head = host
        .execute(AuthoringRequest::Inspect)
        .unwrap()
        .zone_head
        .unwrap();
    assert_eq!(final_head.corrections, applied.corrections);
    assert_eq!(final_head.base, applied.base);
    assert_eq!(std::fs::read(&archive).unwrap(), bytes);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn house() -> forge_project::zone_authoring::buildings::AuthoredBuilding {
    forge_project::zone_authoring::buildings::AuthoredBuilding {
        id: forge_core::EntityId::deterministic("new-zone-house"),
        name: "Maison".into(),
        generator_version: 1,
        origin_m: forge_core::Vec3::new(0.0, 0.0, 0.0),
        yaw_degrees: 0.0,
        locked: false,
        door_offset_override_m: Some(1.0),
        dimensions: forge_project::building::RoomDimensions {
            width_m: 8.0,
            depth_m: 6.0,
            clear_height_m: 3.0,
            wall_m: 0.25,
            slab_m: 0.2,
            door_width_m: 1.4,
            door_height_m: 2.2,
            door_offset_x_m: 0.0,
        },
    }
}

fn assert_fresh_seed(bytes: &[u8], document: &forge_project::TerrainDocument) {
    let value: serde_json::Value = serde_json::from_slice(bytes).unwrap();
    assert_eq!(value["schema"], "forge.zone-edit-session");
    let seed_bytes: Vec<u8> =
        serde_json::from_value(value["migration"]["legacy_archive"].clone()).unwrap();
    let seed =
        forge_project::terrain_edit::TerrainEditSession::from_archive_bytes(&seed_bytes).unwrap();
    assert!(seed.audit().is_empty());
    assert_eq!(seed.document(), document);
}

#[test]
fn configured_zone_round_trips_and_never_overwrites_an_existing_or_dirty_zone() {
    let root = std::env::temp_dir().join(format!("forge-creation-recipe-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let settings = NewTerrainSettings {
        seed: 875,
        elevation_m: 8.0,
        amplitude_m: 24.0,
        wavelength_m: 180.0,
    };
    let original = prepare_terrain("creation", "hills", "Collines", settings).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let create = AuthoringRequest::Create {
        document: Box::new(original.clone()),
    };
    let created = host.execute(create.clone()).unwrap();
    assert_eq!(created.document.as_ref(), Some(&original));
    let edit = host
        .execute(AuthoringRequest::Apply {
            request: TerrainEditRequest {
                command_id: CommandId::deterministic("creation-rename"),
                expected_hash: original.content_hash().unwrap(),
                expected_revision: original.revision,
                action: TerrainAction::Edit(TerrainEdit::Rename {
                    name: "Collines retouchées".into(),
                }),
            },
        })
        .unwrap();
    assert!(edit.dirty);
    let other = prepare_terrain("creation", "other", "Autre", settings).unwrap();
    assert!(
        host.execute(AuthoringRequest::Create {
            document: Box::new(other.clone())
        })
        .is_err()
    );
    assert!(!root.join(other.area_id.to_string()).exists());
    assert_eq!(
        host.execute(AuthoringRequest::Inspect).unwrap().document,
        edit.document
    );
    let saved = host
        .execute(AuthoringRequest::Save)
        .unwrap()
        .document
        .unwrap();
    let archive = root
        .join(original.area_id.to_string())
        .join("authoring.json");
    let bytes = std::fs::read(&archive).unwrap();
    host.execute(AuthoringRequest::Close).unwrap();
    assert!(host.execute(create).is_err());
    assert_eq!(std::fs::read(&archive).unwrap(), bytes);
    drop(host);
    let mut reopened = AuthoringHost::new(&root).unwrap();
    assert_eq!(
        reopened
            .execute(AuthoringRequest::Open {
                area_id: original.area_id
            })
            .unwrap()
            .document,
        Some(saved.clone())
    );
    let undone = reopened
        .execute(AuthoringRequest::Apply {
            request: TerrainEditRequest {
                command_id: CommandId::deterministic("creation-undo"),
                expected_hash: saved.content_hash().unwrap(),
                expected_revision: saved.revision,
                action: TerrainAction::Undo,
            },
        })
        .unwrap()
        .document
        .unwrap();
    assert_eq!(undone.name, original.name);
    assert_eq!(undone.height_sampling, original.height_sampling);
    assert_eq!(undone.seed, original.seed);
    let redone = reopened
        .execute(AuthoringRequest::Apply {
            request: TerrainEditRequest {
                command_id: CommandId::deterministic("creation-redo"),
                expected_hash: undone.content_hash().unwrap(),
                expected_revision: undone.revision,
                action: TerrainAction::Redo,
            },
        })
        .unwrap()
        .document
        .unwrap();
    assert_eq!(redone.name, saved.name);
    assert_eq!(redone.height_sampling, original.height_sampling);
    assert_eq!(std::fs::read(&archive).unwrap(), bytes);
    drop(reopened);
    std::fs::remove_dir_all(root).unwrap();
}
