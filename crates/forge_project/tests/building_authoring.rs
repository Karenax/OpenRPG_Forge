use forge_core::{CommandId, EntityId, Vec3};
use forge_project::{
    authoring::new_terrain,
    building::RoomDimensions,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        buildings::{AuthoredBuilding, BuildingEdit},
        migration::ZoneMigrationProposal,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let terrain = new_terrain("building-test", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            hash,
            GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            None,
        )
        .unwrap(),
    )
    .unwrap()
}

fn room() -> AuthoredBuilding {
    AuthoredBuilding {
        id: EntityId::deterministic("room"),
        name: "Room".into(),
        generator_version: 1,
        origin_m: Vec3::ZERO,
        yaw_degrees: 0.0,
        locked: false,
        door_offset_override_m: None,
        dimensions: RoomDimensions {
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

fn edit(s: &mut ZoneEditSession, key: &str, edit: BuildingEdit) {
    let request = s
        .head()
        .prepare_building(CommandId::deterministic(key), edit)
        .unwrap();
    let before = s.head().content_hash().unwrap();
    let receipt = s.preview(&request).unwrap();
    let candidate = s.head().building_preview(&receipt).unwrap();
    assert_eq!(candidate.content_hash().unwrap(), receipt.resulting_hash);
    assert_eq!(before, s.head().content_hash().unwrap());
    s.execute(&request).unwrap();
}

#[test]
fn duplicate_locked_building_is_independent_and_reversible() {
    let mut s = session();
    let mut original = room();
    original.locked = true;
    original.door_offset_override_m = Some(1.5);
    edit(
        &mut s,
        "copy-insert",
        BuildingEdit::Insert {
            building: original.clone(),
        },
    );
    let new_id = EntityId::deterministic("room-copy");
    let command = BuildingEdit::Duplicate {
        id: original.id,
        new_id,
        name: "Guest room".into(),
        origin_m: Vec3::new(20.0, 0.0, 20.0),
    };
    let request = s
        .head()
        .prepare_building(CommandId::deterministic("duplicate"), command.clone())
        .unwrap();
    let before = s.head().clone();
    let preview = s
        .head()
        .building_preview(&s.preview(&request).unwrap())
        .unwrap();
    assert_eq!(s.head(), &before);
    let copy = preview
        .corrections
        .buildings
        .as_ref()
        .unwrap()
        .iter()
        .find(|b| b.id == new_id)
        .unwrap()
        .clone();
    assert!(!copy.locked);
    assert_eq!(copy.dimensions, original.dimensions);
    assert_eq!(copy.door_offset_override_m, original.door_offset_override_m);
    assert_eq!(copy.yaw_degrees.to_bits(), original.yaw_degrees.to_bits());
    s.execute(&request).unwrap();
    s.execute(&request).unwrap();
    assert_eq!(s.head().corrections.buildings.as_ref().unwrap().len(), 2);
    assert_eq!(
        s.head()
            .corrections
            .buildings
            .as_ref()
            .unwrap()
            .iter()
            .find(|b| b.id == original.id)
            .unwrap(),
        &original
    );
    assert!(
        s.head()
            .prepare_building(CommandId::deterministic("collision"), command)
            .is_err()
    );
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&s.delta_archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), s.head());
    action(&mut reopened, "copy-undo", ZoneAction::Undo);
    assert_eq!(
        reopened.head().corrections.buildings.as_ref().unwrap(),
        &[original.clone()]
    );
    action(&mut reopened, "copy-redo", ZoneAction::Redo);
    assert_eq!(
        reopened
            .head()
            .corrections
            .buildings
            .as_ref()
            .unwrap()
            .iter()
            .find(|b| b.id == new_id)
            .unwrap(),
        &copy
    );
    assert!(
        s.head()
            .prepare_building(
                CommandId::deterministic("outside-copy"),
                BuildingEdit::Duplicate {
                    id: original.id,
                    new_id: EntityId::deterministic("outside"),
                    name: "Outside".into(),
                    origin_m: Vec3::new(4096.0, 0.0, 0.0)
                }
            )
            .is_err()
    );
}

#[test]
fn building_name_edit_preserves_identity_geometry_and_archive_history() {
    let mut s = session();
    let mut original = room();
    original.door_offset_override_m = Some(1.5);
    let id = original.id;
    edit(
        &mut s,
        "name-insert",
        BuildingEdit::Insert {
            building: original.clone(),
        },
    );
    let request = s
        .head()
        .prepare_building(
            CommandId::deterministic("rename-room"),
            BuildingEdit::SetName {
                id,
                name: "Auberge — salle commune".into(),
            },
        )
        .unwrap();
    let receipt = s.preview(&request).unwrap();
    let mut renamed = original.clone();
    renamed.name = "Auberge — salle commune".into();
    assert_eq!(
        s.head()
            .building_preview(&receipt)
            .unwrap()
            .corrections
            .buildings
            .as_ref()
            .unwrap()[0],
        renamed
    );
    assert_eq!(
        s.head().corrections.buildings.as_ref().unwrap()[0],
        original
    );
    s.execute(&request).unwrap();
    let head = s.head().clone();
    s.execute(&request).unwrap();
    assert_eq!(s.head(), &head);
    for bytes in [
        s.archive_bytes().unwrap(),
        s.compact_archive_bytes().unwrap(),
        s.delta_archive_bytes().unwrap(),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), &head);
        action(&mut reopened, "name-undo", ZoneAction::Undo);
        assert_eq!(
            reopened.head().corrections.buildings.as_ref().unwrap()[0],
            original
        );
        action(&mut reopened, "name-redo", ZoneAction::Redo);
        assert_eq!(
            reopened.head().corrections.buildings.as_ref().unwrap()[0],
            renamed
        );
    }
    for name in ["   ".into(), "x".repeat(129)] {
        assert!(
            s.head()
                .prepare_building(
                    CommandId::deterministic("invalid-name"),
                    BuildingEdit::SetName { id, name }
                )
                .is_err()
        );
        assert_eq!(s.head(), &head);
    }
    edit(
        &mut s,
        "name-lock",
        BuildingEdit::SetLocked { id, locked: true },
    );
    assert!(
        s.head()
            .prepare_building(
                CommandId::deterministic("locked-name"),
                BuildingEdit::SetName {
                    id,
                    name: "Refused".into()
                }
            )
            .is_err()
    );
}

#[test]
fn building_preview_rejects_changed_receipt_and_stale_authority() {
    let mut s = session();
    let request = s
        .head()
        .prepare_building(
            CommandId::deterministic("preview"),
            BuildingEdit::Insert { building: room() },
        )
        .unwrap();
    let receipt = s.preview(&request).unwrap();
    let before = s.head().clone();
    assert!(s.head().building_preview(&receipt).is_ok());
    let mut changed = receipt.clone();
    changed.applied.buildings.as_mut().unwrap()[0].door_offset_override_m = Some(1.0);
    assert!(s.head().building_preview(&changed).is_err());
    assert_eq!(s.head(), &before);
    s.execute(&request).unwrap();
    assert!(s.head().building_preview(&receipt).is_err());
}

#[test]
fn pose_edit_preserves_door_and_checks_rotated_bounds_and_locks() {
    let mut s = session();
    let mut building = room();
    building.door_offset_override_m = Some(1.5);
    let id = building.id;
    edit(
        &mut s,
        "insert",
        BuildingEdit::Insert {
            building: building.clone(),
        },
    );
    edit(
        &mut s,
        "move",
        BuildingEdit::SetPose {
            id,
            origin_m: Vec3::new(20.0, 3.0, 20.0),
            yaw_degrees: 90.0,
        },
    );
    let moved = s.head().corrections.buildings.as_ref().unwrap()[0].clone();
    assert_eq!(
        moved.door_offset_override_m,
        building.door_offset_override_m
    );
    action(&mut s, "undo", ZoneAction::Undo);
    assert_eq!(
        s.head().corrections.buildings.as_ref().unwrap()[0],
        building
    );
    action(&mut s, "redo", ZoneAction::Redo);
    assert_eq!(s.head().corrections.buildings.as_ref().unwrap()[0], moved);
    let half_x = s.head().base.terrain.extent_m.size_x_m * 0.5;
    assert!(
        s.head()
            .prepare_building(
                CommandId::deterministic("outside"),
                BuildingEdit::SetPose {
                    id,
                    origin_m: Vec3::new(half_x - 2.0, 3.0, 0.0),
                    yaw_degrees: 90.0
                }
            )
            .is_err()
    );
    edit(&mut s, "lock", BuildingEdit::SetLocked { id, locked: true });
    assert!(
        s.head()
            .prepare_building(
                CommandId::deterministic("locked-move"),
                BuildingEdit::SetPose {
                    id,
                    origin_m: Vec3::ZERO,
                    yaw_degrees: 0.0
                }
            )
            .is_err()
    );
    assert_eq!(
        s.head().corrections.buildings.as_ref().unwrap()[0].origin_m,
        moved.origin_m
    );
}

fn action(s: &mut ZoneEditSession, key: &str, action: ZoneAction) {
    s.execute(&ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_hash: s.head().content_hash().unwrap(),
        expected_revision: s.head().revision,
        action,
    })
    .unwrap();
}

#[test]
fn door_correction_survives_resize_regeneration_and_archive_replay() {
    let mut s = session();
    let room = room();
    let id = room.id;
    edit(
        &mut s,
        "insert",
        BuildingEdit::Insert {
            building: room.clone(),
        },
    );
    edit(
        &mut s,
        "door",
        BuildingEdit::SetDoorOffset {
            id,
            offset_m: Some(1.5),
        },
    );
    edit(
        &mut s,
        "resize",
        BuildingEdit::SetDimensions {
            id,
            dimensions: RoomDimensions {
                width_m: 12.0,
                door_width_m: 1.8,
                door_height_m: 2.5,
                wall_m: 0.4,
                slab_m: 0.3,
                ..room.dimensions
            },
        },
    );
    let mut base = s.head().base.clone();
    base.terrain.seed += 1;
    action(&mut s, "regenerate", ZoneAction::Regenerate(Box::new(base)));
    let expected = s.head().corrections.clone();
    let snapshot = forge_project::authoring::playtest::PlaytestSnapshot::from_zone(
        s.head(),
        s.head().content_hash().unwrap(),
    )
    .unwrap();
    assert_eq!(snapshot.buildings(), expected.buildings.as_deref().unwrap());
    let mut changed_head = s.head().clone();
    changed_head.corrections.buildings.as_mut().unwrap()[0].door_offset_override_m = Some(0.0);
    assert!(
        forge_project::authoring::playtest::PlaytestSnapshot::from_zone(
            &changed_head,
            snapshot.identity().source_hash
        )
        .is_err()
    );
    assert_eq!(snapshot.buildings(), expected.buildings.as_deref().unwrap());
    assert_eq!(
        expected.buildings.as_ref().unwrap()[0]
            .effective_dimensions()
            .unwrap()
            .door_offset_x_m
            .to_bits(),
        1.5_f32.to_bits()
    );
    for bytes in [
        s.archive_bytes().unwrap(),
        s.compact_archive_bytes().unwrap(),
        s.delta_archive_bytes().unwrap(),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), s.head());
        assert_eq!(reopened.audit().len(), s.audit().len());
        action(&mut reopened, "undo-regeneration", ZoneAction::Undo);
        action(&mut reopened, "undo-resize", ZoneAction::Undo);
        assert_eq!(
            reopened.head().corrections.buildings.as_ref().unwrap()[0].dimensions,
            room.dimensions
        );
        action(&mut reopened, "redo-resize", ZoneAction::Redo);
        assert_eq!(reopened.head().corrections, expected);
    }
}

#[test]
fn incompatible_resize_and_locked_removal_leave_head_untouched() {
    let mut s = session();
    let mut room = room();
    room.door_offset_override_m = Some(1.5);
    let id = room.id;
    edit(
        &mut s,
        "insert",
        BuildingEdit::Insert {
            building: room.clone(),
        },
    );
    let before = s.head().clone();
    assert!(
        s.head()
            .prepare_building(
                CommandId::deterministic("shrink"),
                BuildingEdit::SetDimensions {
                    id,
                    dimensions: RoomDimensions {
                        width_m: 3.0,
                        ..room.dimensions
                    }
                }
            )
            .is_err()
    );
    assert_eq!(s.head(), &before);
    edit(&mut s, "lock", BuildingEdit::SetLocked { id, locked: true });
    let mut bypass = s.head().corrections.clone();
    bypass.buildings = None;
    let request = ZoneRequest {
        command_id: CommandId::deterministic("bypass"),
        expected_hash: s.head().content_hash().unwrap(),
        expected_revision: s.head().revision,
        action: ZoneAction::edit(bypass),
    };
    let before = s.head().clone();
    assert!(s.execute(&request).is_err());
    assert_eq!(s.head(), &before);
    edit(
        &mut s,
        "unlock",
        BuildingEdit::SetLocked { id, locked: false },
    );
    edit(&mut s, "remove", BuildingEdit::Remove { id });
    assert!(s.head().corrections.buildings.as_ref().unwrap().is_empty());
}

fn add_clearing(s: &mut ZoneEditSession) {
    let mut vegetation: forge_project::VegetationDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = s.head().base.terrain.project_id;
    vegetation.area_id = s.head().base.terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
    }
    let layer_id = vegetation.layers[0].id.clone();
    let mut base = s.head().base.clone();
    base.vegetation = Some(vegetation);
    action(s, "add-vegetation", ZoneAction::Regenerate(Box::new(base)));
    let mut corrections = s.head().corrections.clone();
    corrections
        .density
        .push(forge_project::zone_authoring::DensityOverride {
            layer_id,
            mask: forge_project::VegetationDensityMask {
                vertices_per_axis: 2,
                samples_u8: vec![0, 255, 255, 0],
            },
            locked: true,
        });
    action(s, "paint-clearing", ZoneAction::edit(corrections));
}

#[test]
fn locked_door_and_clearing_survive_regeneration_session_drop_and_disk_reopen() {
    use forge_core::document_store::DocumentStore;
    use forge_project::zone_authoring::{
        file_session::FileZoneSession, regeneration::RegenerationConflict,
    };
    let mut s = session();
    add_clearing(&mut s);
    let mut building = room();
    building.origin_m = Vec3::new(100.0, 3.0, 20.0);
    let id = building.id;
    edit(
        &mut s,
        "insert-locked-test",
        BuildingEdit::Insert { building },
    );
    edit(
        &mut s,
        "retouch-door",
        BuildingEdit::SetDoorOffset {
            id,
            offset_m: Some(1.5),
        },
    );
    edit(
        &mut s,
        "lock-door-building",
        BuildingEdit::SetLocked { id, locked: true },
    );
    let corrections = s.head().corrections.clone();
    let mut base = s.head().base.clone();
    base.terrain.seed += 1;
    let vegetation = base.vegetation.as_mut().unwrap();
    vegetation.seed += 1;
    vegetation.layers.reverse();
    let command = s
        .head()
        .prepare_regeneration(base, CommandId::deterministic("regenerate-both"))
        .unwrap();
    s.execute(&command).unwrap();
    assert_eq!(s.head().corrections, corrections);
    // The proposal reports the exact retained object and lock instead of moving it.
    let mut incompatible = s.head().base.clone();
    incompatible.terrain.extent_m.size_x_m = 128.0;
    let review = s.head().review_regeneration(incompatible.clone()).unwrap();
    assert!(review.conflicts.iter().any(|c| matches!(c,
        RegenerationConflict::BuildingConstraintChanged { building_id, locked: true, .. } if *building_id == id
    )));
    assert!(review.effective.is_none());
    let before = s.head().clone();
    let bypass = ZoneRequest {
        command_id: CommandId::deterministic("incompatible"),
        expected_hash: before.content_hash().unwrap(),
        expected_revision: before.revision,
        action: ZoneAction::Regenerate(Box::new(incompatible)),
    };
    assert!(s.execute(&bypass).is_err());
    assert_eq!(s.head(), &before);
    let bytes = s.delta_archive_bytes().unwrap();
    drop(s); // No surviving in-memory Undo session can supply these corrections.
    let directory =
        std::env::temp_dir().join(format!("forge-building-clearing-{}", std::process::id()));
    std::fs::create_dir(&directory).unwrap();
    let store = DocumentStore::open(&directory).unwrap();
    store.save(None, &bytes).unwrap();
    drop(store);
    let mut reopened = FileZoneSession::open(&directory).unwrap();
    assert_eq!(reopened.session().head(), &before);
    assert_eq!(reopened.session().head().corrections, corrections);
    reopened.save().unwrap();
    drop(reopened);
    let reopened = FileZoneSession::open(&directory).unwrap();
    assert_eq!(reopened.session().head(), &before);
    assert!(!directory.join("authoring.pending").exists());
    drop(reopened);
    std::fs::remove_dir_all(directory).unwrap();
}
