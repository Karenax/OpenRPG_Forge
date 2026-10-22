use forge_core::{AssetId, CommandId, EntityId, ModuleId, Transform, Vec3, bytes_sha256};
use forge_gameplay::*;
use forge_project::{
    authoring::new_terrain,
    module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin},
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

fn fixture() -> (ZoneEditSession, GameplayVisualBinding, [VisualAssetPin; 2]) {
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("visual-zone-history", "camp", "Le camp").unwrap(),
    )
    .unwrap();
    let player = EntityId::deterministic("visual-zone-history/player");
    let actor = EntityId::deterministic("visual-zone-history/keeper");
    let document = GameplayDocument {
        narrative: None,
        factions: None,
        simulation: None,
        scripting: None,
        cinematics: None,
        creator: None,
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("visual-zone-history/module"),
        area_id: session.head().base.terrain.area_id,
        player,
        profile: RulesProfile {
            id: "forge.gate3-minimal".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 20,
            attack_stamina_cost: 5,
            max_stamina: 100,
            max_action_receipts: 512,
            max_events_per_tick: 128,
        },
        actors: [(player, ActorRole::Player), (actor, ActorRole::Friendly)]
            .into_iter()
            .map(|(id, role)| ActorDefinition {
                id,
                role,
                name: format!("Actor {id}"),
                position: Vec3::ZERO,
                max_vitality: 100,
                protection: 0,
                inventory_capacity: 12,
                dialogue: None,
                patrol: vec![],
                flees_danger: false,
            })
            .collect(),
        objects: vec![],
        items: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };
    let request = session
        .head()
        .prepare_gameplay(
            Some(document),
            CommandId::deterministic("visual-zone/gameplay"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let assets = [b"blue".as_slice(), b"amber".as_slice()].map(|bytes| VisualAssetPin {
        asset_id: AssetId::deterministic("visual-zone/model"),
        sha256: bytes_sha256(bytes),
    });
    let binding = GameplayVisualBinding {
        area_id: session.head().base.terrain.area_id,
        entity_id: actor,
        asset: assets[0],
        local_transform: Transform::from_translation(Vec3::new(0.0, -0.5, 0.0)),
    };
    (session, binding, assets)
}

fn history(session: &ZoneEditSession, action: ZoneAction, key: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn omitted_visuals_preserve_historical_head_hash_and_archive_bytes() {
    let (session, _, _) = fixture();
    let bytes = session.archive_bytes().unwrap();
    let mut old_head = serde_json::to_value(session.head()).unwrap();
    assert!(old_head["corrections"].get("gameplay_visuals").is_none());
    old_head["corrections"]
        .as_object_mut()
        .unwrap()
        .remove("gameplay_visuals");
    assert_eq!(
        session.head().content_hash().unwrap(),
        forge_core::canonical_json_sha256(&old_head).unwrap()
    );
    let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(reopened.archive_bytes().unwrap(), bytes);
    assert_eq!(reopened.head().version, 13);
}

#[test]
fn reviewed_bindings_use_undo_redo_without_changing_gameplay() {
    let (mut session, binding, assets) = fixture();
    let gameplay = session.head().corrections.gameplay.clone();
    let old = session.archive_bytes().unwrap();
    let request = session
        .head()
        .prepare_gameplay_visuals(
            Some(vec![binding.clone()]),
            &assets,
            CommandId::deterministic("visual-zone/set"),
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), old);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().version, 17);
    assert_eq!(session.head().corrections.gameplay, gameplay);
    session
        .execute(&history(&session, ZoneAction::Undo, "visual-zone/undo"))
        .unwrap();
    assert!(session.head().corrections.gameplay_visuals.is_none());
    assert_eq!(session.head().corrections.gameplay, gameplay);
    session
        .execute(&history(&session, ZoneAction::Redo, "visual-zone/redo"))
        .unwrap();
    assert_eq!(
        session.head().corrections.gameplay_visuals,
        Some(vec![binding])
    );
    assert_eq!(session.head().corrections.gameplay, gameplay);
}

#[test]
fn bindings_survive_plain_compact_delta_and_cannot_be_downgraded() {
    let (mut session, binding, assets) = fixture();
    let request = session
        .head()
        .prepare_gameplay_visuals(
            Some(vec![binding]),
            &assets,
            CommandId::deterministic("visual-zone/codecs"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    for (bytes, expected, downgraded) in [
        (session.archive_bytes().unwrap(), 46, 34),
        (session.compact_archive_bytes().unwrap(), 47, 35),
        (session.delta_archive_bytes().unwrap(), 48, 36),
    ] {
        let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["version"], expected);
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
        value["version"] = downgraded.into();
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    }
}

#[test]
fn visual_variant_replacement_is_reviewed_stale_safe_and_keeps_source_document() {
    let (mut session, binding, assets) = fixture();
    let gameplay = session.head().corrections.gameplay.clone();
    let request = session
        .head()
        .prepare_gameplay_visuals(
            Some(vec![binding.clone()]),
            &assets,
            CommandId::deterministic("visual-zone/initial"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let replacement = session
        .head()
        .prepare_gameplay_visual_replacement(
            &[binding.entity_id],
            assets[0],
            assets[1],
            &assets,
            CommandId::deterministic("visual-zone/replace"),
        )
        .unwrap();
    let before = session.archive_bytes().unwrap();
    session.preview(&replacement).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), before);
    session.execute(&replacement).unwrap();
    let applied = session
        .head()
        .corrections
        .gameplay_visuals
        .as_ref()
        .unwrap();
    assert_eq!(applied[0].asset, assets[1]);
    assert_eq!(applied[0].entity_id, binding.entity_id);
    assert_eq!(applied[0].local_transform, binding.local_transform);
    assert_eq!(session.head().corrections.gameplay, gameplay);
    assert!(
        session
            .head()
            .prepare_gameplay_visual_replacement(
                &[binding.entity_id],
                assets[0],
                assets[1],
                &assets,
                CommandId::deterministic("visual-zone/stale")
            )
            .is_err()
    );
    assert_eq!(session.head().corrections.gameplay, gameplay);
}

#[test]
fn empty_bindings_clear_explicitly_and_removing_gameplay_requires_clearing_bindings() {
    let (mut session, binding, assets) = fixture();
    let request = session
        .head()
        .prepare_gameplay_visuals(
            Some(vec![binding]),
            &assets,
            CommandId::deterministic("visual-zone/clear-initial"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let broken = session.head().prepare_gameplay(
        None,
        CommandId::deterministic("visual-zone/remove-gameplay"),
    );
    assert!(broken.is_err() || session.preview(&broken.unwrap()).is_err());
    let clear = session
        .head()
        .prepare_gameplay_visuals(
            Some(vec![]),
            &assets,
            CommandId::deterministic("visual-zone/clear"),
        )
        .unwrap();
    session.execute(&clear).unwrap();
    assert_eq!(session.head().version, 17);
    assert_eq!(session.head().corrections.gameplay_visuals, Some(vec![]));
    let remove = session
        .head()
        .prepare_gameplay(
            None,
            CommandId::deterministic("visual-zone/remove-after-clear"),
        )
        .unwrap();
    session.execute(&remove).unwrap();
    assert!(session.head().corrections.gameplay.is_none());
}

#[test]
fn invalid_identity_area_transform_and_catalog_pin_are_refused_without_mutation() {
    let (session, binding, assets) = fixture();
    let before = session.archive_bytes().unwrap();
    for fault in 0..5 {
        let mut bad = binding.clone();
        match fault {
            0 => bad.entity_id = session.head().corrections.gameplay.as_ref().unwrap().player,
            1 => bad.entity_id = EntityId::deterministic("visual-zone/missing"),
            2 => bad.area_id = forge_core::AreaId::deterministic("visual-zone/wrong-area"),
            3 => bad.local_transform.scale.x = 0.0,
            _ => bad.asset.sha256 = bytes_sha256(b"missing model"),
        }
        assert!(
            session
                .head()
                .prepare_gameplay_visuals(
                    Some(vec![bad]),
                    &assets,
                    CommandId::deterministic("visual-zone/invalid")
                )
                .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
}

#[test]
fn raw_edit_cannot_bypass_canonical_visual_reference_validation() {
    let (session, mut binding, _) = fixture();
    binding.entity_id = EntityId::deterministic("visual-zone/raw-missing-target");
    let mut corrections = session.head().corrections.clone();
    corrections.gameplay_visuals = Some(vec![binding]);
    let request = history(
        &session,
        ZoneAction::edit(corrections),
        "visual-zone/raw-invalid",
    );
    let bytes = session.archive_bytes().unwrap();
    assert!(session.preview(&request).is_err());
    assert_eq!(session.archive_bytes().unwrap(), bytes);
}
