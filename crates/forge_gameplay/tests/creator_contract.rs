use forge_core::{AreaId, ContentHash, EntityId, ModuleId, canonical_json_sha256};
use forge_gameplay::*;

fn document(module: &str) -> GameplayDocument {
    let player = EntityId::deterministic(format!("{module}/player"));
    serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay", "version":1,
        "module_id":ModuleId::deterministic(module), "area_id":AreaId::deterministic(module),
        "profile":{"id":"creator-test.minimal", "version":1,"interaction_range_m":3.0,
            "attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,
            "max_action_receipts":512,"max_events_per_tick":128},
        "player":player,"actors":[{"id":player,"name":"Player","role":"player",
            "position":{"x":0.0,"y":0.0,"z":0.0},"max_vitality":100,"protection":0,
            "inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
    }))
    .unwrap()
}

#[test]
fn legacy_bytes_and_functional_meaning_survive_explicit_surface_configuration() {
    let legacy = document("a");
    legacy.validate().unwrap();
    let serialized = serde_json::to_value(&legacy).unwrap();
    assert!(serialized.get("creator").is_none());
    let mut config = legacy.creator_configuration();
    config.surfaces = vec![CreatorSurface::Rules];
    config.required_capabilities = vec![GameplayCapability::Actors];
    let configured = legacy.with_creator_configuration(config).unwrap();
    assert_eq!(configured.version, 2);
    assert_eq!(
        legacy.functional_hash().unwrap(),
        configured.functional_hash().unwrap()
    );
    assert_ne!(
        canonical_json_sha256(&legacy).unwrap(),
        canonical_json_sha256(&configured).unwrap()
    );
    let restored: GameplayDocument =
        serde_json::from_slice(&serde_json::to_vec(&configured).unwrap()).unwrap();
    assert_eq!(configured, restored);
    restored.validate().unwrap();
    let old_source = ContentHash::from_bytes([8; 32]);
    let old_game = GameSession::new(legacy.clone(), old_source).unwrap();
    let old_save = old_game.save_bytes().unwrap();
    let new_source = ContentHash::from_bytes([9; 32]);
    assert!(GameSession::resume(configured.clone(), new_source, &old_save, false).is_err());
    let resumed = GameSession::resume(configured.clone(), new_source, &old_save, true).unwrap();
    assert!(resumed.state().requires_physics_revalidation);
    assert!(resumed.save_bytes().is_err());
    let game = GameSession::new(configured, ContentHash::from_bytes([9; 32])).unwrap();
    assert_eq!(game.state().actors[&legacy.player].stamina, 100);
}

#[test]
fn declarations_cannot_claim_future_runtime_capabilities_or_lose_hidden_content() {
    let mut source = document("a");
    source.items.push(ItemDefinition {
        id: EntityId::deterministic("a/key"),
        name: "Key".into(),
        max_stack: 1,
    });
    let original = source.clone();
    let mut config = source.creator_configuration();
    config.surfaces.clear();
    config.required_capabilities = vec![GameplayCapability::Actors];
    let error = source
        .with_creator_configuration(config.clone())
        .unwrap_err()
        .to_string();
    assert!(error.contains("Inventory"), "{error}");
    assert_eq!(source, original);
    config
        .required_capabilities
        .push(GameplayCapability::Inventory);
    let hidden = source.with_creator_configuration(config.clone()).unwrap();
    assert!(!hidden.surface_visible(CreatorSurface::Objects));
    assert_eq!(hidden.items, source.items);
    config
        .required_capabilities
        .push(GameplayCapability::SandboxedScripts);
    assert!(source.with_creator_configuration(config).is_ok());
    // The bounded interpreter is now implemented. An unknown future capability
    // still fails closed at the versioned document boundary.
    assert!(serde_json::from_str::<GameplayCapability>("\"future_unavailable_api\"").is_err());
}

#[test]
fn schema_identity_duplicates_and_surface_dependencies_are_enforced() {
    let source = document("a");
    let mut configured = source
        .with_creator_configuration(source.creator_configuration())
        .unwrap();
    configured.version = 1;
    assert!(configured.validate().is_err());
    configured.version = 2;
    configured.creator.as_mut().unwrap().version = 999;
    assert!(configured.validate().is_err());
    let mut config = source.creator_configuration();
    config.surfaces.push(CreatorSurface::Rules);
    assert!(source.with_creator_configuration(config).is_err());
    let mut config = source.creator_configuration();
    config.required_capabilities = vec![GameplayCapability::Actors];
    assert!(source.with_creator_configuration(config).is_err());
    let mut missing = source;
    missing.version = 2;
    assert!(missing.validate().is_err());
}

#[test]
fn rule_edits_use_runtime_validation_and_reject_partial_or_unbounded_values() {
    let source = document("a");
    let profile = source.profile;
    for (key, value) in [
        (RuleKey::AttackDamage, "0"),
        (RuleKey::AttackDamage, "1.5"),
        (RuleKey::InteractionRange, "NaN"),
        (RuleKey::InteractionRange, "inf"),
        (RuleKey::MaxStamina, "4"),
        (RuleKey::MaxActionReceipts, "4097"),
        (RuleKey::MaxEventsPerTick, "-1"),
    ] {
        assert!(
            profile.with_rule_text(key, value).is_err(),
            "{key:?}/{value}"
        );
    }
    let changed = profile.with_rule_text(RuleKey::AttackDamage, "37").unwrap();
    let descriptor = changed
        .rule_fields()
        .into_iter()
        .find(|f| f.key == RuleKey::AttackDamage)
        .unwrap();
    assert_eq!(descriptor.value, "37");
    assert_eq!(descriptor.unit, RuleUnit::VitalityPoints);
    let mut changed_document = document("a");
    let old_hash = changed_document.functional_hash().unwrap();
    changed_document.profile = changed;
    assert_ne!(old_hash, changed_document.functional_hash().unwrap());
    assert_eq!(profile.attack_damage, 20);
}

#[test]
fn two_module_configurations_share_the_contract_and_keep_distinct_save_identities() {
    let a = document("a");
    let b = document("b");
    let a = a
        .with_creator_configuration(a.creator_configuration())
        .unwrap();
    let mut config = b.creator_configuration();
    config.required_capabilities = vec![GameplayCapability::Actors, GameplayCapability::Dialogue];
    config.surfaces = vec![CreatorSurface::Dialogues, CreatorSurface::Rules];
    let b = b.with_creator_configuration(config).unwrap();
    assert!(a.surface_visible(CreatorSurface::Objects));
    assert!(!b.surface_visible(CreatorSurface::Objects));
    assert_eq!(a.profile.rule_fields(), b.profile.rule_fields());
    let game = GameSession::new(a, ContentHash::from_bytes([7; 32])).unwrap();
    let save = game.save_bytes().unwrap();
    assert!(GameSession::resume(b, ContentHash::from_bytes([7; 32]), &save, true).is_err());
}

#[test]
fn inspector_rule_value_drives_real_damage_cost_and_save_compatibility() {
    use forge_core::{CommandId, Vec3};
    let mut source = document("combat");
    let player = source.player;
    let mut enemy = source.actors[0].clone();
    enemy.id = EntityId::deterministic("combat/enemy");
    enemy.role = ActorRole::Enemy;
    enemy.position = Vec3::new(1.0, 0.0, 0.0);
    enemy.protection = 3;
    let target = enemy.id;
    source.actors.push(enemy);
    source.profile = source
        .profile
        .with_rule_text(RuleKey::AttackDamage, "37")
        .unwrap()
        .with_rule_text(RuleKey::AttackStaminaCost, "9")
        .unwrap();
    let mut game = GameSession::new(source.clone(), ContentHash::from_bytes([1; 32])).unwrap();
    game.execute(
        &GameRequest {
            command_id: CommandId::deterministic("combat/attack"),
            expected_sequence: 0,
            actor: player,
            action: GameAction::Attack { target },
        },
        ActionObservation {
            line_of_sight: true,
            confirmed_contact: true,
            water_contact: false,
        },
    )
    .unwrap();
    assert_eq!(game.state().actors[&target].vitality, 66);
    assert_eq!(game.state().actors[&player].stamina, 91);
    let bytes = game.save_bytes().unwrap();
    source.profile = source
        .profile
        .with_rule_text(RuleKey::AttackDamage, "20")
        .unwrap();
    assert!(GameSession::resume(source, ContentHash::from_bytes([2; 32]), &bytes, true).is_err());
}

fn blueprint_fixture() -> (
    GameplayDocument,
    ActorBlueprintDefinition,
    EntityId,
    EntityId,
) {
    let mut document = document("blueprints");
    let mut first = document.actors[0].clone();
    first.id = EntityId::deterministic("blueprints/first");
    first.name = "Premier garde".into();
    first.role = ActorRole::Enemy;
    first.position.x = 1.0;
    first.patrol = vec![forge_core::Vec3::new(2.0, 0.0, 0.0)];
    let mut second = first.clone();
    second.id = EntityId::deterministic("blueprints/second");
    second.name = "Second garde".into();
    second.position.x = 2.0;
    let ids = (first.id, second.id);
    let definition = ActorBlueprintDefinition {
        id: EntityId::deterministic("blueprints/guard-template"),
        version: 1,
        name: "Garde".into(),
        defaults: ActorBlueprintDefaults {
            role: ActorRole::Enemy,
            max_vitality: 100,
            protection: 3,
            inventory_capacity: 4,
            flees_danger: true,
        },
    };
    document.actors.extend([first, second]);
    let document = document
        .with_actor_blueprint(definition.clone())
        .unwrap()
        .with_actor_blueprint_binding(
            ids.0,
            definition.id,
            ActorBlueprintOverrides {
                max_vitality: Some(150),
                ..ActorBlueprintOverrides::default()
            },
        )
        .unwrap()
        .with_actor_blueprint_binding(
            ids.1,
            definition.id,
            ActorBlueprintOverrides {
                inventory_capacity: Some(9),
                ..ActorBlueprintOverrides::default()
            },
        )
        .unwrap();
    (document, definition, ids.0, ids.1)
}

#[test]
fn blueprint_revision_preserves_instance_overrides_identity_and_spatial_authoring() {
    let (source, mut definition, first, second) = blueprint_fixture();
    let original = source.clone();
    let first_before = source.actor(first).unwrap().clone();
    definition.version = 2;
    definition.defaults.max_vitality = 120;
    definition.defaults.protection = 5;
    definition.defaults.inventory_capacity = 10;
    let revised = source.with_actor_blueprint(definition).unwrap();
    assert_eq!(source, original);
    assert_eq!(revised.actor(first).unwrap().max_vitality, 150);
    assert_eq!(revised.actor(second).unwrap().max_vitality, 120);
    assert_eq!(revised.actor(first).unwrap().inventory_capacity, 10);
    assert_eq!(revised.actor(second).unwrap().inventory_capacity, 9);
    assert_eq!(revised.actor(first).unwrap().protection, 5);
    assert_eq!(revised.actor(first).unwrap().name, first_before.name);
    assert_eq!(
        revised.actor(first).unwrap().position,
        first_before.position
    );
    assert_eq!(revised.actor(first).unwrap().patrol, first_before.patrol);
    assert_eq!(revised.player, source.player);
    assert!(
        revised
            .actor_blueprints()
            .unwrap()
            .bindings
            .iter()
            .all(|b| b.blueprint_version == 2)
    );
}

#[test]
fn blueprints_roundtrip_into_real_runtime_actor_resources_and_saved_state() {
    let (source, _, first, second) = blueprint_fixture();
    let bytes = serde_json::to_vec(&source).unwrap();
    let reopened: GameplayDocument = serde_json::from_slice(&bytes).unwrap();
    reopened.validate().unwrap();
    assert_eq!(reopened, source);
    let hash = canonical_json_sha256(&reopened).unwrap();
    let game = GameSession::new(reopened.clone(), hash).unwrap();
    assert_eq!(game.state().actors[&first].vitality, 150);
    assert_eq!(game.state().actors[&second].vitality, 100);
    let save = game.save_bytes().unwrap();
    let resumed = GameSession::resume(reopened, hash, &save, false).unwrap();
    assert_eq!(resumed.state(), game.state());
}

#[test]
fn clearing_a_blueprint_override_restores_inheritance_without_changing_the_other_instance() {
    let (source, mut definition, first, second) = blueprint_fixture();
    let restored = source
        .with_actor_blueprint_overrides(first, ActorBlueprintOverrides::default())
        .unwrap();
    assert_eq!(restored.actor(first).unwrap().max_vitality, 100);
    definition.version = 2;
    definition.defaults.max_vitality = 120;
    let revised = restored.with_actor_blueprint(definition).unwrap();
    assert_eq!(revised.actor(first).unwrap().max_vitality, 120);
    assert_eq!(revised.actor(second).unwrap().inventory_capacity, 9);
}

#[test]
fn stale_invalid_and_forged_blueprint_updates_are_refused_without_mutation() {
    let (source, definition, first, _) = blueprint_fixture();
    let original = source.clone();
    assert!(source.with_actor_blueprint(definition.clone()).is_err());
    let mut invalid = definition.clone();
    invalid.version = 2;
    invalid.defaults.inventory_capacity = 10_001;
    assert!(source.with_actor_blueprint(invalid).is_err());
    assert!(
        source
            .with_actor_blueprint_binding(
                EntityId::deterministic("missing-actor"),
                definition.id,
                ActorBlueprintOverrides::default()
            )
            .is_err()
    );
    assert!(
        source
            .with_actor_blueprint_binding(
                first,
                EntityId::deterministic("missing-blueprint"),
                ActorBlueprintOverrides::default()
            )
            .is_err()
    );
    assert_eq!(source, original);
    let mut forged = source.clone();
    forged
        .actors
        .iter_mut()
        .find(|a| a.id == first)
        .unwrap()
        .max_vitality = 999;
    assert!(forged.validate().is_err());
    let mut forged = source.clone();
    forged
        .creator
        .as_mut()
        .unwrap()
        .blueprints
        .as_mut()
        .unwrap()
        .bindings[0]
        .blueprint_version = 99;
    assert!(forged.validate().is_err());
    let mut forged = source.clone();
    forged
        .creator
        .as_mut()
        .unwrap()
        .required_capabilities
        .retain(|c| *c != GameplayCapability::Blueprints);
    assert!(forged.validate().is_err());
    let mut conflicting = definition;
    conflicting.id = source.player;
    assert!(source.with_actor_blueprint(conflicting).is_err());
}

#[test]
fn ordinary_actor_edits_capture_only_changed_fields_as_blueprint_overrides() {
    let (source, _, first, _) = blueprint_fixture();
    let before = source.actor(first).unwrap();
    let mut after = before.clone();
    after.max_vitality = 180;
    after.name = "Retouche locale".into();
    let mut overrides = source
        .actor_blueprints()
        .unwrap()
        .bindings
        .iter()
        .find(|b| b.actor == first)
        .unwrap()
        .overrides
        .clone();
    overrides.capture_changes(before, &after);
    assert_eq!(overrides.max_vitality, Some(180));
    assert_eq!(overrides.protection, None);
    assert_eq!(overrides.inventory_capacity, None);
    assert_eq!(overrides.role, None);
    let mut edited = source;
    *edited.actors.iter_mut().find(|a| a.id == first).unwrap() = after;
    let edited = edited
        .with_actor_blueprint_overrides(first, overrides)
        .unwrap();
    assert_eq!(edited.actor(first).unwrap().name, "Retouche locale");
    assert_eq!(edited.actor(first).unwrap().max_vitality, 180);
}

#[test]
fn blueprint_metadata_only_revision_preserves_save_meaning_but_inherited_stats_do_not() {
    let (source, mut definition, _, _) = blueprint_fixture();
    let original = source.functional_hash().unwrap();
    definition.version = 2;
    definition.name = "Garde renommé".into();
    let renamed = source.with_actor_blueprint(definition.clone()).unwrap();
    assert_eq!(renamed.functional_hash().unwrap(), original);
    definition.version = 3;
    definition.defaults.protection = 7;
    let changed = renamed.with_actor_blueprint(definition).unwrap();
    assert_ne!(changed.functional_hash().unwrap(), original);
}

#[test]
fn detaching_preserves_resolved_actor_and_definition_removal_refuses_live_references() {
    let (source, mut definition, first, second) = blueprint_fixture();
    let before = source.clone();
    assert!(source.without_actor_blueprint(definition.id).is_err());
    assert_eq!(source, before);
    let detached = source.without_actor_blueprint_binding(first).unwrap();
    assert_eq!(detached.actor(first), source.actor(first));
    assert_eq!(
        detached.functional_hash().unwrap(),
        source.functional_hash().unwrap()
    );
    assert!(detached.without_actor_blueprint_binding(first).is_err());
    definition.version = 2;
    definition.defaults.max_vitality = 120;
    let revised = detached.with_actor_blueprint(definition.clone()).unwrap();
    assert_eq!(revised.actor(first).unwrap().max_vitality, 150);
    assert_eq!(revised.actor(second).unwrap().max_vitality, 120);
    let unbound = revised.without_actor_blueprint_binding(second).unwrap();
    let removed = unbound.without_actor_blueprint(definition.id).unwrap();
    assert!(removed.actor_blueprints().unwrap().definitions.is_empty());
    assert_eq!(removed.actors, revised.actors);
    assert!(removed.without_actor_blueprint(definition.id).is_err());
    removed.validate().unwrap();
}

#[test]
fn a_shared_module_profile_materializes_two_areas_without_copying_local_blueprints() {
    let (first, _, actor, _) = blueprint_fixture();
    let mut second = document("shared-profile/second");
    second.module_id = first.module_id;
    second.profile.attack_damage = 22;
    let original_first = first.clone();
    let original_second = second.clone();
    let profile = first
        .profile
        .with_rule_text(RuleKey::AttackDamage, "37")
        .unwrap();
    let shared = ModuleCreatorProfile::for_documents(
        first.module_id,
        profile,
        vec![CreatorSurface::Rules],
        &[&first, &second],
    )
    .unwrap();
    assert!(
        shared
            .required_capabilities
            .contains(&GameplayCapability::Blueprints)
    );
    assert!(
        shared
            .required_capabilities
            .contains(&GameplayCapability::Combat)
    );
    let a = shared.apply_to(&first).unwrap();
    let b = shared.apply_to(&second).unwrap();
    assert_eq!(first, original_first);
    assert_eq!(second, original_second);
    assert_eq!(a.profile.attack_damage, 37);
    assert_eq!(a.profile, b.profile);
    assert_eq!(a.profile.rule_fields(), b.profile.rule_fields());
    assert_eq!(a.actor_blueprints(), first.actor_blueprints());
    assert_eq!(a.actor(actor), first.actor(actor));
    assert!(b.actor_blueprints().is_none());
    assert_ne!(a.area_id, b.area_id);
    assert!(!a.surface_visible(CreatorSurface::Actors));
    shared.validate_document(&a).unwrap();
    shared.validate_document(&b).unwrap();
    let mut inconsistent = b.clone();
    inconsistent.profile.attack_damage = 99;
    assert!(shared.validate_document(&inconsistent).is_err());
    let mut inconsistent = b;
    inconsistent
        .creator
        .as_mut()
        .unwrap()
        .surfaces
        .push(CreatorSurface::Actors);
    assert!(shared.validate_document(&inconsistent).is_err());
}

#[test]
fn shared_profile_identity_and_capability_failures_never_mutate_a_zone() {
    let source = document("shared-profile/source");
    let foreign = document("shared-profile/foreign");
    let shared = ModuleCreatorProfile::for_documents(
        source.module_id,
        source.profile.clone(),
        vec![CreatorSurface::Rules],
        &[&source],
    )
    .unwrap();
    assert!(shared.apply_to(&foreign).is_err());
    assert!(
        ModuleCreatorProfile::for_documents(
            source.module_id,
            source.profile.clone(),
            vec![CreatorSurface::Rules],
            &[&source, &foreign]
        )
        .is_err()
    );
    let mut unsupported = shared.clone();
    unsupported.version = 999;
    assert!(unsupported.apply_to(&source).is_err());
    let mut duplicate = shared.clone();
    duplicate.surfaces.push(CreatorSurface::Rules);
    assert!(duplicate.validate().is_err());
    let mut missing = shared;
    missing.required_capabilities.clear();
    assert!(missing.apply_to(&source).is_err());
    assert_eq!(source, document("shared-profile/source"));
    assert_eq!(foreign, document("shared-profile/foreign"));
}

#[test]
fn shared_profile_pin_is_canonical_bounded_and_rejects_unsupported_versions() {
    let source = document("shared-profile/codec");
    let shared = ModuleCreatorProfile::for_documents(
        source.module_id,
        source.profile.clone(),
        CreatorSurface::ALL.to_vec(),
        &[&source],
    )
    .unwrap();
    let bytes = shared.to_bytes().unwrap();
    let decoded = ModuleCreatorProfile::from_bytes(&bytes).unwrap();
    assert_eq!(
        decoded.content_hash().unwrap(),
        shared.content_hash().unwrap()
    );
    let mut reordered = shared.clone();
    reordered.surfaces.reverse();
    reordered.required_capabilities.reverse();
    assert_eq!(reordered.to_bytes().unwrap(), bytes);
    let mut unknown = shared;
    unknown.version += 1;
    assert!(unknown.to_bytes().is_err());
    assert!(ModuleCreatorProfile::from_bytes(&vec![b' '; MAX_MODULE_CREATOR_BYTES + 1]).is_err());
}

#[test]
fn shared_surface_changes_preserve_save_meaning_but_rule_changes_require_a_new_game() {
    let source = document("shared-profile/save");
    let shared = ModuleCreatorProfile::for_documents(
        source.module_id,
        source.profile.clone(),
        vec![CreatorSurface::Rules],
        &[&source],
    )
    .unwrap();
    let configured = shared.apply_to(&source).unwrap();
    assert_eq!(
        configured.functional_hash().unwrap(),
        source.functional_hash().unwrap()
    );
    let old_hash = canonical_json_sha256(&source).unwrap();
    let game = GameSession::new(source.clone(), old_hash).unwrap();
    let save = game.save_bytes().unwrap();
    let new_hash = canonical_json_sha256(&configured).unwrap();
    let resumed = GameSession::resume(configured, new_hash, &save, true).unwrap();
    assert!(resumed.state().requires_physics_revalidation);
    let mut changed = shared;
    changed.profile.attack_damage = 37;
    let changed = changed.apply_to(&source).unwrap();
    assert!(GameSession::resume(changed, new_hash, &save, true).is_err());
}
