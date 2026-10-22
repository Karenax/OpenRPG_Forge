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
fn factions_follow_common_preview_retry_history_and_archive_validation() {
    use forge_gameplay::{FactionDefinition, FactionsDefinition};
    let mut session = session();
    session
        .execute(&edit(&session, "factions/initialize"))
        .unwrap();
    let source = session.head().corrections.gameplay.as_deref().unwrap();
    let group = EntityId::deterministic("zone/faction");
    let candidate = source
        .with_factions_definition(Some(FactionsDefinition {
            version: 1,
            factions: vec![FactionDefinition {
                id: group,
                name: "Veilleurs".into(),
                perception_range_m: 12.0,
                protects_factions: vec![group],
                assault_delta: -10,
            }],
            memberships: std::collections::BTreeMap::from([(source.player, group)]),
            relations: vec![],
        }))
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(candidate.clone()),
            CommandId::deterministic("factions/configure"),
        )
        .unwrap();
    let before = session.archive_bytes().unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    session
        .execute(&command(&session, ZoneAction::Undo, "factions/undo"))
        .unwrap();
    assert!(
        session
            .head()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .factions
            .is_none()
    );
    session
        .execute(&command(&session, ZoneAction::Redo, "factions/redo"))
        .unwrap();
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(
            reopened.head().corrections.gameplay.as_deref(),
            Some(&candidate)
        );
        assert_eq!(reopened.audit(), session.audit());
    }
    let mut invalid = candidate;
    invalid
        .factions
        .as_mut()
        .unwrap()
        .memberships
        .insert(EntityId::deterministic("absent"), group);
    let before = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_gameplay(Some(invalid), CommandId::deterministic("factions/invalid"))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
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

#[test]
fn narrative_candidates_preserve_common_preview_history_and_all_archive_codecs() {
    use forge_gameplay::{NarrativeDefinition, NarrativeValue, NarrativeVariable};
    let mut session = session();
    session
        .execute(&edit(&session, "narrative/initialize"))
        .unwrap();
    let before = session.archive_bytes().unwrap();
    let source = session.head().corrections.gameplay.as_deref().unwrap();
    let candidate = source
        .with_narrative_definition(Some(NarrativeDefinition {
            version: 1,
            variables: vec![NarrativeVariable {
                id: EntityId::deterministic("narrative/variable"),
                name: "Witness heard".into(),
                initial: NarrativeValue::Boolean(false),
            }],
            quests: vec![],
        }))
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(candidate.clone()),
            CommandId::deterministic("narrative/configure"),
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    session
        .execute(&command(&session, ZoneAction::Undo, "narrative/undo"))
        .unwrap();
    assert!(
        session
            .head()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .narrative
            .is_none()
    );
    session
        .execute(&command(&session, ZoneAction::Redo, "narrative/redo"))
        .unwrap();
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(
            reopened.head().corrections.gameplay.as_deref(),
            Some(&candidate)
        );
        assert_eq!(reopened.audit(), session.audit());
    }
    let before = session.archive_bytes().unwrap();
    let mut invalid = candidate;
    let duplicate = invalid.narrative.as_ref().unwrap().variables[0].clone();
    invalid
        .narrative
        .as_mut()
        .unwrap()
        .variables
        .push(duplicate);
    assert!(
        session
            .head()
            .prepare_gameplay(Some(invalid), CommandId::deterministic("narrative/invalid"))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn creator_configuration_and_rule_edit_follow_preview_history_and_reopen() {
    use forge_gameplay::{CreatorSurface, GameplayCapability, RuleKey};
    let mut session = session();
    session
        .execute(&edit(&session, "creator/initialize"))
        .unwrap();
    let original = session.archive_bytes().unwrap();
    let source = session.head().corrections.gameplay.as_deref().unwrap();
    let mut configuration = source.creator_configuration();
    configuration.surfaces = vec![CreatorSurface::Rules];
    configuration.required_capabilities = vec![GameplayCapability::Actors];
    let mut configured = source.with_creator_configuration(configuration).unwrap();
    configured.profile = configured
        .profile
        .with_rule_text(RuleKey::AttackDamage, "37")
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(configured.clone()),
            CommandId::deterministic("creator/edit"),
        )
        .unwrap();
    session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), original);
    session.execute(&request).unwrap();
    let saved = session.archive_bytes().unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&saved).unwrap();
    assert_eq!(
        reopened.head().corrections.gameplay.as_deref(),
        Some(&configured)
    );
    session
        .execute(&command(&session, ZoneAction::Undo, "creator/undo"))
        .unwrap();
    assert!(
        session
            .head()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .creator
            .is_none()
    );
    assert_eq!(
        session
            .head()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .profile
            .attack_damage,
        20
    );
    session
        .execute(&command(&session, ZoneAction::Redo, "creator/redo"))
        .unwrap();
    assert_eq!(
        session.head().corrections.gameplay.as_deref(),
        Some(&configured)
    );
    let mut invalid = configured;
    invalid.creator.as_mut().unwrap().version = 999;
    let before = session.archive_bytes().unwrap();
    assert!(
        session
            .head()
            .prepare_gameplay(Some(invalid), CommandId::deterministic("creator/invalid"))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn blueprint_revisions_preserve_overrides_through_zone_history_and_all_archive_codecs() {
    use forge_gameplay::{
        ActorBlueprintDefaults, ActorBlueprintDefinition, ActorBlueprintOverrides, ActorRole,
    };
    let mut session = session();
    session
        .execute(&edit(&session, "blueprints/initialize"))
        .unwrap();
    let mut source = session
        .head()
        .corrections
        .gameplay
        .as_deref()
        .unwrap()
        .clone();
    let mut creature = source.actors[0].clone();
    creature.id = EntityId::deterministic("blueprints/creature");
    creature.name = "Garde du relais".into();
    creature.role = ActorRole::Enemy;
    creature.position.x = 1.0;
    let actor = creature.id;
    let mut definition = ActorBlueprintDefinition {
        id: EntityId::deterministic("blueprints/definition"),
        version: 1,
        name: "Garde".into(),
        defaults: ActorBlueprintDefaults::from_actor(&creature),
    };
    source.actors.push(creature);
    let bound = source
        .with_actor_blueprint(definition.clone())
        .unwrap()
        .with_actor_blueprint_binding(
            actor,
            definition.id,
            ActorBlueprintOverrides {
                protection: Some(7),
                ..ActorBlueprintOverrides::default()
            },
        )
        .unwrap();
    let attach = session
        .head()
        .prepare_gameplay(
            Some(bound.clone()),
            CommandId::deterministic("blueprints/attach"),
        )
        .unwrap();
    session.execute(&attach).unwrap();
    let original = session.archive_bytes().unwrap();
    definition.version = 2;
    definition.defaults.max_vitality = 180;
    definition.defaults.protection = 4;
    let revised = bound.with_actor_blueprint(definition).unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(revised.clone()),
            CommandId::deterministic("blueprints/revise"),
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), original);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(revised.actor(actor).unwrap().protection, 7);
    assert_eq!(revised.actor(actor).unwrap().max_vitality, 180);
    session
        .execute(&command(&session, ZoneAction::Undo, "blueprints/undo"))
        .unwrap();
    assert_eq!(session.head().corrections.gameplay.as_deref(), Some(&bound));
    session
        .execute(&command(&session, ZoneAction::Redo, "blueprints/redo"))
        .unwrap();
    assert_eq!(
        session.head().corrections.gameplay.as_deref(),
        Some(&revised)
    );
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
    }
    let before = session.archive_bytes().unwrap();
    let mut forged = revised;
    forged
        .actors
        .iter_mut()
        .find(|a| a.id == actor)
        .unwrap()
        .protection = 99;
    assert!(
        session
            .head()
            .prepare_gameplay(Some(forged), CommandId::deterministic("blueprints/forged"))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn container_contents_follow_preview_undo_redo_and_all_zone_archive_codecs() {
    use forge_gameplay::{
        ContainerDefinition, ItemDefinition, ObjectDefinition, ObjectKind, ObjectPhysics,
    };
    use std::collections::BTreeMap;
    let mut session = session();
    session
        .execute(&edit(&session, "containers/initialize"))
        .unwrap();
    let mut source = session
        .head()
        .corrections
        .gameplay
        .as_deref()
        .unwrap()
        .clone();
    let item = EntityId::deterministic("containers/zone-item");
    let chest = EntityId::deterministic("containers/zone-chest");
    source.items.push(ItemDefinition {
        id: item,
        name: "Jeton".into(),
        max_stack: 4,
    });
    source.objects.push(ObjectDefinition {
        id: chest,
        name: "Coffre".into(),
        position: forge_core::Vec3::new(1.0, 0.0, 0.0),
        kind: ObjectKind::Prop,
        physics: ObjectPhysics::default(),
    });
    let definition = ContainerDefinition {
        capacity_units: 2,
        initial_items: BTreeMap::from([(item, 1)]),
        accepts: vec![item],
        key: None,
    };
    let first = source
        .with_container_definition(chest, definition.clone())
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(first.clone()),
            CommandId::deterministic("containers/attach"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let mut definition = definition;
    definition.initial_items.insert(item, 2);
    let revised = first
        .with_container_definition(chest, definition.clone())
        .unwrap();
    let request = session
        .head()
        .prepare_gameplay(
            Some(revised.clone()),
            CommandId::deterministic("containers/revise"),
        )
        .unwrap();
    let before = session.archive_bytes().unwrap();
    session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), before);
    session.execute(&request).unwrap();
    session
        .execute(&command(&session, ZoneAction::Undo, "containers/undo"))
        .unwrap();
    assert_eq!(session.head().corrections.gameplay.as_deref(), Some(&first));
    session
        .execute(&command(&session, ZoneAction::Redo, "containers/redo"))
        .unwrap();
    assert_eq!(
        session.head().corrections.gameplay.as_deref(),
        Some(&revised)
    );
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
    }
    definition.initial_items.insert(item, 3);
    let before = session.archive_bytes().unwrap();
    assert!(
        revised
            .with_container_definition(chest, definition)
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}
