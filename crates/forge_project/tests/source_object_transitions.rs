use forge_core::{AssetId, CommandId, ContentHash, EntityId, ModuleId, Transform, Vec3};
use forge_project::{
    VegetationDocument,
    audit::AuditStamp,
    authoring::new_terrain,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    zone_authoring::{
        ZoneCorrections,
        placements::{ManualPlacement, PlacementEdit},
        regeneration::RegenerationConflict,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        source_objects::{SourceObjectState, resolve},
    },
};

fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(
        new_terrain("source-transitions", "zone", "Source objects").unwrap(),
    )
    .unwrap()
}

fn source() -> ManualPlacement {
    ManualPlacement {
        physics: None,
        id: EntityId::deterministic("source-door"),
        name: "Imported door".into(),
        asset_id: AssetId::deterministic("verified-door"),
        asset_hash: ContentHash::from_bytes([7; 32]),
        transform: Transform::from_translation(Vec3::new(2.0, 3.0, 4.0)),
        locked: false,
    }
}

fn imported_vegetation(session: &ZoneEditSession, marker: u8) -> VegetationDocument {
    let mut vegetation = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = session.head().base.terrain.project_id;
    vegetation.area_id = session.head().base.terrain.area_id;
    vegetation.extent_m.size_x_m = session.head().base.terrain.extent_m.size_x_m;
    vegetation.extent_m.size_z_m = session.head().base.terrain.extent_m.size_z_m;
    vegetation.schema_version = 2;
    vegetation.layers.truncate(1);
    vegetation.layers[0].material_filter = None;
    vegetation.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: vegetation.layers[0].id.to_string(),
            prototype_key: "transition_trees".into(),
            count: 1,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: ContentHash::from_bytes([marker; 32]),
            parameter_names: Vec::new(),
        }],
    });
    vegetation
}

fn imported_plant_asset(session: &ZoneEditSession) -> AssetId {
    let layer = session.head().base.vegetation.as_ref().unwrap().layers[0]
        .id
        .as_str();
    AssetId::deterministic(format!(
        "imported-instance:{}:{}:{layer}",
        session.head().base.terrain.project_id,
        session.head().base.terrain.area_id,
    ))
}

fn apply(session: &mut ZoneEditSession, label: &str, edit: PlacementEdit) {
    let request = session
        .head()
        .prepare_source_object(source(), edit, CommandId::deterministic(label))
        .unwrap();
    session.execute(&request).unwrap();
}

fn raw(session: &ZoneEditSession, label: &str, corrections: ZoneCorrections) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(label),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    }
}

fn rejects_without_mutation(session: &mut ZoneEditSession, request: &ZoneRequest, reason: &str) {
    let head = session.head().clone();
    let archive = session.archive_bytes().unwrap();
    let receipts = session.audit().to_vec();
    assert!(session.preview(request).unwrap_err().contains(reason));
    assert!(session.execute(request).unwrap_err().contains(reason));
    assert_eq!(session.head(), &head);
    assert_eq!(session.audit(), receipts);
    assert_eq!(session.archive_bytes().unwrap(), archive);
}

#[test]
fn direct_edits_cannot_rebind_existing_source_provenance() {
    let mut session = session();
    apply(
        &mut session,
        "move-source",
        PlacementEdit::Transform {
            id: source().id,
            transform: Transform::IDENTITY,
        },
    );
    for field in 0..5 {
        let mut corrections = session.head().corrections.clone();
        let record = &mut corrections.source_object_edits.as_mut().unwrap()[0];
        match field {
            0 => record.source.name = "Rebound name".into(),
            1 => record.source.transform.translation_m.x = 9.0,
            2 => record.source.transform.translation_m.y = -0.0,
            3 => {
                record.source.asset_id = AssetId::deterministic("rebound-asset");
                let SourceObjectState::Edited { placement } = &mut record.state else {
                    panic!("edited source");
                };
                placement.asset_id = record.source.asset_id;
            }
            _ => {
                record.source.asset_hash = ContentHash::from_bytes([8; 32]);
                let SourceObjectState::Edited { placement } = &mut record.state else {
                    panic!("edited source");
                };
                placement.asset_hash = record.source.asset_hash;
            }
        }
        let request = raw(&session, &format!("rebind-{field}"), corrections);
        rejects_without_mutation(&mut session, &request, "provenance");
    }
}

#[test]
fn source_provenance_preserves_signed_zero_exactly() {
    let mut source = source();
    source.transform.translation_m.x = 0.0;
    let source_id = source.id;
    let mut session = session();
    let request = session
        .head()
        .prepare_source_object(
            source,
            PlacementEdit::SetLocked {
                id: source_id,
                locked: true,
            },
            CommandId::deterministic("signed-zero-source"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let mut retry = request.clone();
    let ZoneAction::Edit(corrections) = &mut retry.action else {
        panic!("source edit request");
    };
    corrections.source_object_edits.as_mut().unwrap()[0]
        .source
        .transform
        .translation_m
        .x = -0.0;
    rejects_without_mutation(&mut session, &retry, "different parameters");
    let mut archive: serde_json::Value =
        serde_json::from_slice(&session.archive_bytes().unwrap()).unwrap();
    archive["head"]["corrections"]["source_object_edits"][0]["source"]["transform"]["translation_m"]
        ["x"] = serde_json::json!(-0.0);
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&archive).unwrap()).is_err());
    let mut corrections = session.head().corrections.clone();
    corrections.source_object_edits.as_mut().unwrap()[0]
        .source
        .transform
        .translation_m
        .x = -0.0;
    let request = raw(&session, "rebind-signed-zero", corrections);
    rejects_without_mutation(&mut session, &request, "provenance");
}

#[test]
fn direct_edits_cannot_change_remove_or_unlock_and_move_a_locked_source() {
    let mut session = session();
    apply(
        &mut session,
        "lock-source",
        PlacementEdit::SetLocked {
            id: source().id,
            locked: true,
        },
    );
    for kind in 0..3 {
        let mut corrections = session.head().corrections.clone();
        let record = &mut corrections.source_object_edits.as_mut().unwrap()[0];
        if kind == 1 {
            record.state = SourceObjectState::Removed;
        } else {
            let SourceObjectState::Edited { placement } = &mut record.state else {
                panic!("locked source");
            };
            placement.transform.translation_m.x += 1.0;
            if kind == 2 {
                placement.locked = false;
            }
        }
        let request = raw(&session, &format!("locked-source-{kind}"), corrections);
        rejects_without_mutation(&mut session, &request, "Unlock source object");
    }
    apply(
        &mut session,
        "unlock-source",
        PlacementEdit::SetLocked {
            id: source().id,
            locked: false,
        },
    );
    apply(
        &mut session,
        "move-unlocked-source",
        PlacementEdit::Transform {
            id: source().id,
            transform: Transform::IDENTITY,
        },
    );
    assert_eq!(
        resolve(session.head(), &source()).unwrap()[0].transform,
        Transform::IDENTITY
    );
}

#[test]
fn direct_edits_respect_locks_on_copies() {
    let mut session = session();
    let copy_id = EntityId::deterministic("source-copy");
    apply(
        &mut session,
        "copy-source",
        PlacementEdit::Duplicate {
            source_id: source().id,
            id: copy_id,
            transform: Transform::IDENTITY,
        },
    );
    apply(
        &mut session,
        "lock-copy",
        PlacementEdit::SetLocked {
            id: copy_id,
            locked: true,
        },
    );
    for kind in 0..3 {
        let mut corrections = session.head().corrections.clone();
        let copies = &mut corrections.source_object_edits.as_mut().unwrap()[0].copies;
        if kind == 1 {
            copies.clear();
        } else {
            copies[0].transform.translation_m.x += 1.0;
            if kind == 2 {
                copies[0].locked = false;
            }
        }
        let request = raw(&session, &format!("locked-copy-{kind}"), corrections);
        rejects_without_mutation(&mut session, &request, "Unlock source object");
    }
    apply(
        &mut session,
        "unlock-copy",
        PlacementEdit::SetLocked {
            id: copy_id,
            locked: false,
        },
    );
    apply(
        &mut session,
        "remove-copy",
        PlacementEdit::Remove { id: copy_id },
    );
    assert!(
        session
            .head()
            .corrections
            .source_object_edits
            .as_ref()
            .unwrap()[0]
            .copies
            .is_empty()
    );
}

#[test]
fn direct_edits_cannot_drop_records_or_resurrect_a_deleted_source() {
    let mut session = session();
    apply(
        &mut session,
        "remove-source",
        PlacementEdit::Remove { id: source().id },
    );
    for kind in 0..3 {
        let mut corrections = session.head().corrections.clone();
        if kind == 0 {
            corrections.source_object_edits = None;
        } else {
            corrections.source_object_edits.as_mut().unwrap()[0].state = if kind == 1 {
                SourceObjectState::Inherited
            } else {
                SourceObjectState::Edited {
                    placement: source(),
                }
            };
        }
        let request = raw(&session, &format!("resurrect-{kind}"), corrections);
        rejects_without_mutation(
            &mut session,
            &request,
            if kind == 0 {
                "records persist"
            } else {
                "deletion persists"
            },
        );
    }
}

#[test]
fn deleting_the_last_copy_keeps_the_source_tombstone_across_all_codecs() {
    let mut session = session();
    let copy_id = EntityId::deterministic("last-source-copy");
    apply(
        &mut session,
        "copy-source",
        PlacementEdit::Duplicate {
            source_id: source().id,
            id: copy_id,
            transform: Transform::IDENTITY,
        },
    );
    apply(
        &mut session,
        "remove-source",
        PlacementEdit::Remove { id: source().id },
    );
    apply(
        &mut session,
        "remove-last-copy",
        PlacementEdit::Remove { id: copy_id },
    );
    assert!(resolve(session.head(), &source()).unwrap().is_empty());
    let record = &session
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap()[0];
    assert_eq!(record.source, source());
    assert_eq!(record.state, SourceObjectState::Removed);
    assert!(record.copies.is_empty());
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
        assert!(resolve(reopened.head(), &source()).unwrap().is_empty());
    }
}

#[test]
fn undo_redo_restore_reviewed_source_edits_and_retry_is_idempotent() {
    let mut session = session();
    let initial = session.head().corrections.clone();
    let request = session
        .head()
        .prepare_source_object(
            source(),
            PlacementEdit::Remove { id: source().id },
            CommandId::deterministic("delete-retry"),
        )
        .unwrap();
    let receipt = session.execute(&request).unwrap();
    assert_eq!(session.execute(&request).unwrap(), receipt);
    assert_eq!(session.audit().len(), 1);
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-source"),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    session.execute(&undo).unwrap();
    assert_eq!(session.head().corrections, initial);
    assert_eq!(resolve(session.head(), &source()).unwrap(), vec![source()]);
    let redo = ZoneRequest {
        command_id: CommandId::deterministic("redo-source"),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::Redo,
    };
    session.execute(&redo).unwrap();
    assert!(resolve(session.head(), &source()).unwrap().is_empty());
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(reopened.audit(), session.audit());
}

#[test]
fn reviewed_asset_replacement_materializes_atomically_and_undo_restores_the_source() {
    fn replacement(session: &ZoneEditSession, label: &str) -> ZoneRequest {
        let mut candidate = session.head().clone();
        candidate.corrections.source_object_edits.as_mut().unwrap()[0].state =
            SourceObjectState::Removed;
        candidate.corrections.placements = Some(vec![source()]);
        let request = candidate
            .prepare_placement(
                CommandId::deterministic(label),
                PlacementEdit::ReplaceAsset {
                    ids: vec![source().id],
                    expected_asset_id: source().asset_id,
                    expected_asset_hash: source().asset_hash,
                    asset_id: AssetId::deterministic("replacement-door"),
                    asset_hash: ContentHash::from_bytes([42; 32]),
                },
            )
            .unwrap();
        let ZoneAction::Edit(corrections) = request.action else {
            panic!("asset replacement edit");
        };
        raw(session, label, *corrections)
    }

    let mut session = session();
    apply(
        &mut session,
        "lock-before-replacement",
        PlacementEdit::SetLocked {
            id: source().id,
            locked: true,
        },
    );
    let forbidden = replacement(&session, "locked-materialization");
    rejects_without_mutation(&mut session, &forbidden, "Unlock source object");
    apply(
        &mut session,
        "unlock-before-replacement",
        PlacementEdit::SetLocked {
            id: source().id,
            locked: false,
        },
    );
    let original = session.head().corrections.clone();
    let request = replacement(&session, "replace-materialized-source");
    session.execute(&request).unwrap();
    let record = &session
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap()[0];
    assert_eq!(record.source, source());
    assert_eq!(record.state, SourceObjectState::Removed);
    assert!(resolve(session.head(), &source()).unwrap().is_empty());
    let authored = &session.head().corrections.placements.as_ref().unwrap()[0];
    assert_eq!(authored.id, source().id);
    assert_eq!(authored.name, source().name);
    assert_eq!(authored.transform, source().transform);
    assert_eq!(
        authored.asset_id,
        AssetId::deterministic("replacement-door")
    );
    assert_eq!(authored.asset_hash, ContentHash::from_bytes([42; 32]));
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());

    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-materialized-source"),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    session.execute(&undo).unwrap();
    assert_eq!(session.head().corrections, original);
    assert_eq!(resolve(session.head(), &source()).unwrap(), vec![source()]);
}

#[test]
fn source_archive_family_covers_audit_and_retains_its_version_after_undo() {
    for audit in [
        AuditStamp::legacy(),
        AuditStamp::human("source-author", "2026-10-01T14:00:00Z").unwrap(),
    ] {
        let mut session = session();
        let request = session
            .head()
            .prepare_source_object(
                source(),
                PlacementEdit::Remove { id: source().id },
                CommandId::deterministic("versioned-source-delete"),
            )
            .unwrap();
        session.execute_with_audit(&request, audit.clone()).unwrap();
        assert_eq!(session.head().version, 14);
        let undo = ZoneRequest {
            command_id: CommandId::deterministic("undo-versioned-source"),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action: ZoneAction::Undo,
        };
        session.execute_with_audit(&undo, audit.clone()).unwrap();
        assert_eq!(session.head().version, 1);
        assert!(session.head().corrections.source_object_edits.is_none());
        // Delta encoding tracks the previous Edit request, not the Undo head.
        // This subsequent Edit must encode Set(None), preserving the old source receipt.
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some("After source Undo".into());
        let next = raw(&session, "edit-after-source-undo", corrections);
        session.execute_with_audit(&next, audit.clone()).unwrap();

        for (bytes, expected_version, previous_versions) in [
            (session.archive_bytes().unwrap(), 37, vec![1, 4, 31, 34]),
            (
                session.compact_archive_bytes().unwrap(),
                38,
                vec![3, 5, 32, 35],
            ),
            (
                session.delta_archive_bytes().unwrap(),
                39,
                vec![6, 30, 33, 36],
            ),
        ] {
            let archive: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
            assert_eq!(archive["version"], expected_version);
            let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
            assert_eq!(reopened.head(), session.head());
            assert_eq!(reopened.audit(), session.audit());
            assert_eq!(reopened.audit()[0].audit, audit);
            assert_eq!(reopened.audit()[1].audit, audit);
            assert_eq!(reopened.audit()[2].audit, audit);
            let reencoded = match expected_version {
                37 => reopened.archive_bytes().unwrap(),
                38 => reopened.compact_archive_bytes().unwrap(),
                _ => reopened.delta_archive_bytes().unwrap(),
            };
            assert_eq!(reencoded, bytes);
            for version in previous_versions {
                let mut forged = archive.clone();
                forged["version"] = serde_json::json!(version);
                assert!(
                    ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&forged).unwrap())
                        .is_err()
                );
            }
        }
    }
}

#[test]
fn absent_source_domain_keeps_historical_archive_families_and_canonical_bytes() {
    for audit in [
        AuditStamp::legacy(),
        AuditStamp::human("legacy-author", "2026-10-01T14:00:01Z").unwrap(),
    ] {
        let mut session = session();
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some("Historical edit".into());
        let request = raw(&session, "historical-domain-edit", corrections);
        session.execute_with_audit(&request, audit.clone()).unwrap();
        assert_eq!(session.head().version, 1);
        assert!(session.head().corrections.source_object_edits.is_none());
        let expected_versions = if audit.is_legacy() {
            [1, 3, 6]
        } else {
            [31, 32, 33]
        };
        for ((bytes, unsupported_source_version), expected_version) in [
            (session.archive_bytes().unwrap(), 37),
            (session.compact_archive_bytes().unwrap(), 38),
            (session.delta_archive_bytes().unwrap(), 39),
        ]
        .into_iter()
        .zip(expected_versions)
        {
            assert!(
                !std::str::from_utf8(&bytes)
                    .unwrap()
                    .contains("source_object_edits")
            );
            let archive: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
            assert_eq!(archive["version"], expected_version);
            let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
            assert_eq!(
                reopened.head().content_hash().unwrap(),
                session.head().content_hash().unwrap()
            );
            assert_eq!(reopened.audit(), session.audit());
            let reencoded = match unsupported_source_version {
                37 => reopened.archive_bytes().unwrap(),
                38 => reopened.compact_archive_bytes().unwrap(),
                _ => reopened.delta_archive_bytes().unwrap(),
            };
            assert_eq!(reencoded, bytes);
            let mut forged = archive;
            forged["version"] = serde_json::json!(unsupported_source_version);
            assert!(
                ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&forged).unwrap()).is_err()
            );
        }
    }
}

#[test]
fn gameplay_and_source_edits_coexist_without_renumbering_gameplay_archives() {
    let mut session = session();
    let player = EntityId::deterministic("source-gameplay/player");
    let mut corrections = session.head().corrections.clone();
    corrections.gameplay = Some(Box::new(
        serde_json::from_value(serde_json::json!({
            "schema": "forge.gameplay", "version": 1,
            "module_id": ModuleId::deterministic("source-gameplay/module"),
            "area_id": session.head().base.terrain.area_id, "player": player,
            "profile": { "id": "forge.gate3-minimal", "version": 1, "interaction_range_m": 3.0,
                "attack_range_m": 2.0, "attack_damage": 20, "attack_stamina_cost": 5,
                "max_stamina": 100, "max_action_receipts": 512, "max_events_per_tick": 128 },
            "actors": [{ "id": player, "name": "Player", "role": "player",
                "position": {"x": 0.0, "y": 0.0, "z": 0.0}, "max_vitality": 100,
                "protection": 0, "inventory_capacity": 12, "dialogue": null,
                "patrol": [], "flees_danger": false }],
            "items": [], "objects": [], "places": [], "dialogues": [], "quests": []
        }))
        .unwrap(),
    ));
    let gameplay = corrections.gameplay.clone();
    let request = raw(&session, "gameplay-before-source", corrections);
    session.execute(&request).unwrap();
    assert_eq!(session.head().version, 13);
    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 34),
        (session.compact_archive_bytes().unwrap(), 35),
        (session.delta_archive_bytes().unwrap(), 36),
    ] {
        assert_eq!(
            serde_json::from_slice::<serde_json::Value>(&bytes).unwrap()["version"],
            version
        );
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
    }

    apply(
        &mut session,
        "source-with-gameplay",
        PlacementEdit::Remove { id: source().id },
    );
    assert_eq!(session.head().version, 14);
    assert_eq!(session.head().corrections.gameplay, gameplay);
    let mut downgraded_head = serde_json::to_value(session.head()).unwrap();
    downgraded_head["version"] = serde_json::json!(13);
    assert!(
        forge_project::zone_authoring::ZoneAuthoringHead::from_bytes(
            &serde_json::to_vec(&downgraded_head).unwrap(),
        )
        .is_err()
    );
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-source-keep-gameplay"),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    session.execute(&undo).unwrap();
    assert_eq!(session.head().version, 13);
    assert_eq!(session.head().corrections.gameplay, gameplay);
    assert!(session.head().corrections.source_object_edits.is_none());
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some("Gameplay retained after source Undo".into());
    let request = raw(&session, "gameplay-after-source-undo", corrections);
    session.execute(&request).unwrap();
    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 37),
        (session.compact_archive_bytes().unwrap(), 38),
        (session.delta_archive_bytes().unwrap(), 39),
    ] {
        assert_eq!(
            serde_json::from_slice::<serde_json::Value>(&bytes).unwrap()["version"],
            version
        );
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
        assert_eq!(reopened.head().corrections.gameplay, gameplay);
    }
}

#[test]
fn static_source_edits_do_not_block_imported_source_change_but_plant_edits_do() {
    let mut static_session = session();
    apply(
        &mut static_session,
        "static-before-imported-source-change",
        PlacementEdit::Transform {
            id: source().id,
            transform: Transform::IDENTITY,
        },
    );
    let mut proposed = static_session.head().base.clone();
    proposed.vegetation = Some(imported_vegetation(&static_session, 1));
    let review = static_session.head().review_regeneration(proposed).unwrap();
    assert!(review.effective.is_some(), "{:?}", review.conflicts);
    assert!(
        !review
            .conflicts
            .iter()
            .any(|conflict| matches!(conflict, RegenerationConflict::ImportedPlantSourceChanged))
    );

    let mut plant_session = session();
    let vegetation = imported_vegetation(&plant_session, 1);
    let mut base = plant_session.head().base.clone();
    base.vegetation = Some(vegetation);
    let request = plant_session
        .head()
        .prepare_regeneration(
            base,
            CommandId::deterministic("imported-source-before-plant-edit"),
        )
        .unwrap();
    plant_session.execute(&request).unwrap();
    let plant_source = ManualPlacement {
        physics: None,
        id: EntityId::deterministic("imported-plant-source"),
        name: "Imported plant".into(),
        asset_id: imported_plant_asset(&plant_session),
        asset_hash: ContentHash::from_bytes([7; 32]),
        transform: Transform::from_translation(Vec3::new(2.0, 3.0, 4.0)),
        locked: false,
    };
    let request = plant_session
        .head()
        .prepare_source_object(
            plant_source.clone(),
            PlacementEdit::Transform {
                id: plant_source.id,
                transform: Transform::IDENTITY,
            },
            CommandId::deterministic("retouch-imported-plant"),
        )
        .unwrap();
    plant_session.execute(&request).unwrap();
    let mut changed = plant_session.head().base.clone();
    changed
        .vegetation
        .as_mut()
        .unwrap()
        .imported_source
        .as_mut()
        .unwrap()
        .blocks[0]
        .instances_sha256 = ContentHash::from_bytes([2; 32]);
    let review = plant_session.head().review_regeneration(changed).unwrap();
    assert!(
        review
            .conflicts
            .iter()
            .any(|conflict| matches!(conflict, RegenerationConflict::ImportedPlantSourceChanged))
    );
}
