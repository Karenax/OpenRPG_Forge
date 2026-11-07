use forge_core::{AssetId, CommandId, EntityId, ModuleId, Transform, Vec3, bytes_sha256};
use forge_gameplay::{
    DialogueDefinition, DialogueNode, ObjectDefinition, ObjectKind, ObjectPhysics,
};
use forge_project::{
    assistant::{
        self,
        planning::{PlanProposal, PlannedOperation},
        selection::{DialogueLineId, Region, Selection, SelectionScope},
    },
    audit::AuditStamp,
    authoring::new_terrain,
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::ZoneEditSession,
    },
};

fn id(key: &str) -> EntityId {
    EntityId::deterministic(format!("assistant-planning/{key}"))
}
fn region() -> Region {
    Region {
        minimum: Vec3::new(-10.0, -10.0, -10.0),
        maximum: Vec3::new(10.0, 10.0, 10.0),
    }
}
fn line() -> DialogueLineId {
    DialogueLineId {
        dialogue_id: id("dialogue"),
        node_id: id("node"),
    }
}
fn selection() -> Selection {
    Selection {
        placements: vec![],
        objects: vec![id("door")],
        dialogue_lines: vec![line()],
        zone_properties: true,
        create_content: false,
    }
}
fn audit() -> AuditStamp {
    AuditStamp::ai("synthetic-planner-test", "2026-10-22T13:00:00Z").unwrap()
}

fn session() -> ZoneEditSession {
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("assistant-plan", "zone", "Village").unwrap(),
    )
    .unwrap();
    let mut document: forge_gameplay::GameplayDocument = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay", "version":1, "module_id":ModuleId::deterministic("assistant-plan/module"),
        "area_id":session.head().base.terrain.area_id, "player":id("player"),
        "profile":{"id":"forge.gate3-minimal", "version":1, "interaction_range_m":3.0, "attack_range_m":2.0, "attack_damage":20, "attack_stamina_cost":5, "max_stamina":100, "max_action_receipts":512, "max_events_per_tick":128},
        "actors":[{"id":id("player"), "name":"UNSHARED_ACTOR_SECRET", "role":"player", "position":{"x":0.0,"y":0.0,"z":0.0}, "max_vitality":100, "protection":0, "inventory_capacity":12, "dialogue":null, "patrol":[], "flees_danger":false}],
        "items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    for (key, x) in [("door", 1.0), ("hidden-object", 30.0)] {
        document.objects.push(ObjectDefinition {
            id: id(key),
            name: key.into(),
            position: Vec3::new(x, 0.0, 0.0),
            kind: ObjectKind::Door {
                initially_open: false,
            },
            physics: ObjectPhysics::default(),
        });
    }
    for (key, text) in [
        ("dialogue", "Human corrected line"),
        ("hidden-dialogue", "UNSHARED_DIALOGUE_SECRET"),
    ] {
        document.dialogues.push(DialogueDefinition {
            id: id(key),
            start: id(if key == "dialogue" {
                "node"
            } else {
                "hidden-node"
            }),
            nodes: vec![DialogueNode {
                id: id(if key == "dialogue" {
                    "node"
                } else {
                    "hidden-node"
                }),
                text: text.into(),
                choices: vec![],
            }],
        });
    }
    let request = session
        .head()
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("manual-initial-data"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    session
}

fn proposal(scope: &SelectionScope, operations: Vec<PlannedOperation>) -> PlanProposal {
    PlanProposal {
        context_hash: scope.context().content_hash().unwrap(),
        summary: "Local variant".into(),
        missing_capabilities: vec![],
        operations,
    }
}

#[test]
fn selected_door_and_dialogue_use_one_transaction_then_local_variant_preserves_them() {
    let mut session = session();
    let scope = SelectionScope::new(session.head(), &selection(), region()).unwrap();
    let plan = proposal(
        &scope,
        vec![
            PlannedOperation::MoveObject {
                id: id("door"),
                position: Vec3::new(3.0, 0.0, 2.0),
            },
            PlannedOperation::SetDialogueText {
                dialogue_id: line().dialogue_id,
                node_id: line().node_id,
                text: "Reviewed replacement".into(),
            },
        ],
    );
    let request = plan
        .compile(
            session.head(),
            &scope,
            CommandId::deterministic("door-and-line"),
        )
        .unwrap();
    let review = assistant::prepare(&session, &request, scope.policy(), audit()).unwrap();
    assert_eq!(review.changes().len(), 3);
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let count = session.audit().len();
    assistant::apply(&mut session, scope.policy(), &review, &approval).unwrap();
    assert_eq!(session.audit().len(), count + 1);
    let authored = session.head().corrections.gameplay.clone();
    let local = SelectionScope::new(
        session.head(),
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        region(),
    )
    .unwrap();
    let plan = proposal(
        &local,
        vec![PlannedOperation::RenameZone {
            name: "Village variant".into(),
        }],
    );
    let request = plan
        .compile(
            session.head(),
            &local,
            CommandId::deterministic("local-variant"),
        )
        .unwrap();
    let review = assistant::prepare(&session, &request, local.policy(), audit()).unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assistant::apply(&mut session, local.policy(), &review, &approval).unwrap();
    let restored = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.head().corrections.gameplay, authored);
    assert_eq!(
        restored.head().effective().unwrap().0.name,
        "Village variant"
    );
}

#[test]
fn outgoing_context_excludes_unselected_data_and_reports_only_available_operations() {
    let session = session();
    let scope = SelectionScope::new(session.head(), &selection(), region()).unwrap();
    let json = serde_json::to_string(scope.context()).unwrap();
    assert!(!json.contains("UNSHARED_ACTOR_SECRET"));
    assert!(!json.contains("UNSHARED_DIALOGUE_SECRET"));
    assert!(!json.contains("hidden-object"));
    assert!(json.contains("Human corrected line"));
    assert!(
        scope
            .context()
            .supported_operations
            .contains(&"move_object".into())
    );
    assert!(
        !scope
            .context()
            .supported_operations
            .contains(&"move_placement".into())
    );
}

#[test]
fn unselected_id_outside_region_stale_context_and_partial_plan_are_rejected_without_mutation() {
    let mut session = session();
    let scope = SelectionScope::new(session.head(), &selection(), region()).unwrap();
    let before = session.archive_bytes().unwrap();
    for operation in [
        PlannedOperation::MoveObject {
            id: id("hidden-object"),
            position: Vec3::ZERO,
        },
        PlannedOperation::MoveObject {
            id: id("door"),
            position: Vec3::new(20.0, 0.0, 0.0),
        },
        PlannedOperation::SetDialogueText {
            dialogue_id: id("hidden-dialogue"),
            node_id: id("hidden-node"),
            text: "Do not write".into(),
        },
    ] {
        let plan = proposal(
            &scope,
            vec![
                PlannedOperation::RenameZone {
                    name: "Never partially applied".into(),
                },
                operation,
            ],
        );
        assert!(
            plan.compile(session.head(), &scope, CommandId::deterministic("invalid"))
                .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
    let plan = proposal(
        &scope,
        vec![PlannedOperation::RenameZone {
            name: "Variant".into(),
        }],
    );
    let request = plan
        .compile(
            session.head(),
            &scope,
            CommandId::deterministic("manual-change"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    assert!(
        plan.compile(session.head(), &scope, CommandId::deterministic("stale"))
            .is_err()
    );
}

#[test]
fn unavailable_capability_is_visible_and_blocks_mutation_and_shell_is_not_an_operation() {
    let session = session();
    let scope = SelectionScope::new(session.head(), &selection(), region()).unwrap();
    let mut plan = proposal(&scope, vec![]);
    plan.missing_capabilities = vec!["create_ocean".into()];
    let bytes = serde_json::to_vec(&plan).unwrap();
    let parsed = PlanProposal::from_bytes(&bytes).unwrap();
    assert_eq!(parsed.missing_capabilities, vec!["create_ocean"]);
    assert!(
        parsed
            .compile(session.head(), &scope, CommandId::deterministic("missing"))
            .is_err()
    );
    let mut json = serde_json::to_value(&plan).unwrap();
    json["operations"] = serde_json::json!([{"operation":"shell", "command":"echo forbidden"}]);
    assert!(PlanProposal::from_bytes(&serde_json::to_vec(&json).unwrap()).is_err());
}

#[test]
fn placement_move_preserves_asset_pin_rotation_scale_and_rejects_unselected_position() {
    let mut session = session();
    let placement = ManualPlacement {
        local_lights: None,
        id: id("placement"),
        name: "Human model".into(),
        asset_id: AssetId::deterministic("model"),
        asset_hash: bytes_sha256(b"model"),
        physics: None,
        particles: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    let request = session
        .head()
        .prepare_placement(
            CommandId::deterministic("human-placement"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    session.execute(&request).unwrap();
    let selected = Selection {
        placements: vec![placement.id],
        ..Selection::default()
    };
    let scope = SelectionScope::new(session.head(), &selected, region()).unwrap();
    let plan = proposal(
        &scope,
        vec![PlannedOperation::MovePlacement {
            id: placement.id,
            position: Vec3::new(4.0, 0.0, 0.0),
        }],
    );
    let request = plan
        .compile(session.head(), &scope, CommandId::deterministic("move"))
        .unwrap();
    let review = assistant::prepare(&session, &request, scope.policy(), audit()).unwrap();
    assert_eq!(
        review.changes()[0].path,
        "/corrections/placements/0/transform/translation_m/x"
    );
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    assistant::apply(&mut session, scope.policy(), &review, &approval).unwrap();
    let changed = &session.head().corrections.placements.as_ref().unwrap()[0];
    assert_eq!(changed.asset_hash, placement.asset_hash);
    assert_eq!(changed.transform.rotation, placement.transform.rotation);
    assert_eq!(changed.transform.scale, placement.transform.scale);
}

#[test]
fn invalid_duplicate_unknown_and_outside_selections_cannot_authorize_context() {
    let session = session();
    for objects in [
        vec![id("door"), id("door")],
        vec![id("absent")],
        vec![id("hidden-object")],
    ] {
        assert!(
            SelectionScope::new(
                session.head(),
                &Selection {
                    objects,
                    ..Selection::default()
                },
                region()
            )
            .is_err()
        );
    }
    let mut invalid = region();
    invalid.maximum.x = invalid.minimum.x - 1.0;
    assert!(SelectionScope::new(session.head(), &selection(), invalid).is_err());
}
