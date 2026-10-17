use forge_core::{AreaId, AssetId, EntityId, ModuleId, Transform, Vec3, bytes_sha256};
use forge_gameplay::*;
use forge_project::module_gameplay_visuals::*;

fn fixture() -> (
    GameplayDocument,
    Vec<GameplayVisualBinding>,
    Vec<VisualAssetPin>,
) {
    let player = EntityId::deterministic("visual-player");
    let friend = EntityId::deterministic("visual-friend");
    let object = EntityId::deterministic("visual-object");
    let area = AreaId::deterministic("visual-area");
    let actor = |id, role| ActorDefinition {
        id,
        role,
        name: "Actor".into(),
        position: Vec3::ZERO,
        max_vitality: 10,
        protection: 0,
        inventory_capacity: 10,
        dialogue: None,
        patrol: vec![],
        flees_danger: false,
    };
    let document = GameplayDocument {
        schema: GAMEPLAY_SCHEMA.into(),
        version: GAMEPLAY_VERSION,
        module_id: ModuleId::deterministic("visual-module"),
        area_id: area,
        player,
        profile: RulesProfile {
            id: "forge.gate3-minimal".into(),
            version: 1,
            interaction_range_m: 3.0,
            attack_range_m: 2.0,
            attack_damage: 1,
            attack_stamina_cost: 1,
            max_stamina: 10,
            max_action_receipts: 128,
            max_events_per_tick: 128,
        },
        actors: vec![
            actor(player, ActorRole::Player),
            actor(friend, ActorRole::Friendly),
        ],
        objects: vec![ObjectDefinition {
            id: object,
            name: "Crate".into(),
            position: Vec3::new(2.0, 0.5, 0.0),
            kind: ObjectKind::Prop,
            physics: ObjectPhysics::default(),
        }],
        items: vec![],
        places: vec![],
        dialogues: vec![],
        quests: vec![],
    };
    document.validate().unwrap();
    let first = VisualAssetPin {
        asset_id: AssetId::deterministic("visual-asset"),
        sha256: bytes_sha256(b"first model"),
    };
    let variant = VisualAssetPin {
        asset_id: first.asset_id,
        sha256: bytes_sha256(b"authored variant model"),
    };
    let bindings = vec![friend, object]
        .into_iter()
        .map(|entity_id| GameplayVisualBinding {
            area_id: area,
            entity_id,
            asset: first,
            local_transform: Transform::from_translation(Vec3::new(0.0, -0.5, 0.0)),
        })
        .collect();
    (document, bindings, vec![first, variant])
}

#[test]
fn actor_and_object_bind_to_exact_catalog_pins_without_changing_gameplay() {
    let (document, bindings, assets) = fixture();
    validate_catalog(&bindings, &[document.area_id], &assets).unwrap();
    validate_for_document(&bindings, &document, &assets).unwrap();
    let wire = serde_json::to_vec(&bindings).unwrap();
    let reopened: Vec<GameplayVisualBinding> = serde_json::from_slice(&wire).unwrap();
    assert_eq!(bindings, reopened);
}

#[test]
fn missing_gameplay_identity_and_player_binding_are_refused() {
    let (document, mut bindings, assets) = fixture();
    for identity in [EntityId::deterministic("absent-target"), document.player] {
        bindings[0].entity_id = identity;
        assert!(validate_for_document(&bindings, &document, &assets).is_err());
    }
}

#[test]
fn wrong_area_hash_and_duplicate_bindings_are_refused() {
    let (document, bindings, assets) = fixture();
    let mut bad = bindings.clone();
    bad[0].area_id = AreaId::deterministic("absent-area");
    assert!(validate_catalog(&bad, &[document.area_id], &assets).is_err());
    let mut bad = bindings.clone();
    bad[0].asset.sha256 = bytes_sha256(b"unknown payload");
    assert!(validate_catalog(&bad, &[document.area_id], &assets).is_err());
    let mut bad = bindings.clone();
    bad.push(bindings[0].clone());
    assert!(validate_for_document(&bad, &document, &assets).is_err());
}

#[test]
fn replacement_keeps_identity_transform_other_bindings_and_original_snapshot() {
    let (document, bindings, assets) = fixture();
    let before = bindings.clone();
    let target = bindings[1].entity_id;
    let changed = replace_asset(
        &bindings,
        document.area_id,
        &[target],
        assets[0],
        assets[1],
        &assets,
    )
    .unwrap();
    assert_eq!(bindings, before);
    assert_eq!(changed[0], bindings[0]);
    assert_eq!(changed[1].entity_id, target);
    assert_eq!(changed[1].local_transform, bindings[1].local_transform);
    assert_eq!(changed[1].asset, assets[1]);
    validate_for_document(&changed, &document, &assets).unwrap();
    let restored = replace_asset(
        &changed,
        document.area_id,
        &[target],
        assets[1],
        assets[0],
        &assets,
    )
    .unwrap();
    assert_eq!(restored, before);
}

#[test]
fn stale_multi_target_review_and_missing_variant_are_refused_atomically() {
    let (document, bindings, assets) = fixture();
    let targets = bindings.iter().map(|b| b.entity_id).collect::<Vec<_>>();
    assert!(
        replace_asset(
            &bindings,
            document.area_id,
            &targets,
            assets[1],
            assets[0],
            &assets
        )
        .is_err()
    );
    assert!(
        replace_asset(
            &bindings,
            document.area_id,
            &targets,
            assets[0],
            assets[1],
            &assets[..1]
        )
        .is_err()
    );
    let mut missing = targets.clone();
    missing.push(EntityId::deterministic("missing"));
    assert!(
        replace_asset(
            &bindings,
            document.area_id,
            &missing,
            assets[0],
            assets[1],
            &assets
        )
        .is_err()
    );
    assert!(
        replace_asset(
            &bindings,
            document.area_id,
            &[],
            assets[0],
            assets[1],
            &assets
        )
        .is_err()
    );
    assert!(
        replace_asset(
            &bindings,
            document.area_id,
            &[targets[0], targets[0]],
            assets[0],
            assets[1],
            &assets
        )
        .is_err()
    );
}

#[test]
fn invalid_transform_and_resource_limits_are_checked() {
    let (document, mut bindings, assets) = fixture();
    bindings[0].local_transform.scale.x = 0.0;
    assert!(validate_catalog(&bindings, &[document.area_id], &assets).is_err());
    bindings[0].local_transform = Transform::IDENTITY;
    let many = vec![bindings[0].clone(); MAX_GAMEPLAY_VISUAL_BINDINGS + 1];
    assert!(validate_catalog(&many, &[document.area_id], &assets).is_err());
}
