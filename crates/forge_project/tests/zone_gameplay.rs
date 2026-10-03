use forge_core::{CommandId, EntityId, ModuleId};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(new_terrain("gameplay-contract", "area", "Zone").unwrap())
        .unwrap()
}

fn edit(session: &ZoneEditSession, key: &str) -> ZoneRequest {
    let player = EntityId::deterministic("gameplay-contract/player");
    let mut corrections = serde_json::to_value(&session.head().corrections).unwrap();
    corrections["gameplay"] = serde_json::json!({
        "schema":"forge.gameplay", "version":1,
        "module_id": ModuleId::deterministic("gameplay-contract/module"),
        "area_id": session.head().base.terrain.area_id, "player": player,
        "profile": { "id":"forge.gate3-minimal", "version":1, "interaction_range_m":3.0, "attack_range_m":2.0, "attack_damage":20, "attack_stamina_cost":5, "max_stamina":100, "max_action_receipts":512, "max_events_per_tick":128 },
        "actors":[{ "id":player, "name":"Player", "role":"player", "position":{"x":0.0,"y":0.0,"z":0.0}, "max_vitality":100, "protection":0, "inventory_capacity":12, "dialogue":null, "patrol":[], "flees_danger":false }],
        "items":[], "objects":[], "places":[], "dialogues":[], "quests":[]
    });
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::edit(serde_json::from_value(corrections).unwrap()),
    }
}

fn command(session: &ZoneEditSession, action: ZoneAction, key: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn gameplay_uses_common_preview_apply_retry_undo_redo_and_all_archive_codecs() {
    let mut session = session();
    let original = session.archive_bytes().unwrap();
    let request = edit(&session, "gameplay/edit");
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), original);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().version, 13);
    let authored = session.head().clone();
    let undo = command(&session, ZoneAction::Undo, "gameplay/undo");
    session.execute(&undo).unwrap();
    assert!(
        serde_json::to_value(&session.head().corrections)
            .unwrap()
            .get("gameplay")
            .is_none()
    );
    let redo = command(&session, ZoneAction::Redo, "gameplay/redo");
    session.execute(&redo).unwrap();
    assert_eq!(session.head().corrections, authored.corrections);
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
    }
}

#[test]
fn legacy_zone_roundtrip_preserves_bytes_and_does_not_add_gameplay() {
    let session = session();
    let original = session.archive_bytes().unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&original).unwrap();
    assert_eq!(reopened.archive_bytes().unwrap(), original);
    assert!(
        serde_json::to_value(&reopened.head().corrections)
            .unwrap()
            .get("gameplay")
            .is_none()
    );
}
