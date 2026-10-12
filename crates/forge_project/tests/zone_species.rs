use forge_core::{CommandId, ContentHash};
use forge_project::{
    VegetationDocument, VegetationLayerId,
    authoring::new_terrain,
    zone_authoring::{
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        species::SpeciesReference,
    },
};

fn fixture() -> (ZoneEditSession, VegetationLayerId) {
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("species", "zone", "Zone").unwrap())
            .unwrap();
    let mut base = session.head().base.clone();
    let mut vegetation = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = base.terrain.project_id;
    vegetation.area_id = base.terrain.area_id;
    vegetation.extent_m.size_x_m = base.terrain.extent_m.size_x_m;
    vegetation.extent_m.size_z_m = base.terrain.extent_m.size_z_m;
    vegetation
        .layers
        .retain(|l| l.kind == forge_project::VegetationKind::Tree);
    vegetation.layers.truncate(1);
    vegetation.layers[0].material_filter = None;
    vegetation.layers[0].density_mask = None;
    vegetation.exclusions.clear();
    let id = vegetation.layers[0].id.clone();
    base.vegetation = Some(vegetation);
    let request = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("initial-species"))
        .unwrap();
    session.execute(&request).unwrap();
    (session, id)
}
fn prototype() -> SpeciesReference {
    SpeciesReference {
        catalog_sha256: ContentHash::from_bytes([42; 32]),
        source_layer_id: VegetationLayerId::new("source-trees"),
        asset_id: "oak".into(),
    }
}

#[test]
fn promotion_persists_origin_atomically_across_codecs_undo_and_regeneration() {
    use forge_core::{AssetId, EntityId, Transform};
    use forge_project::zone_authoring::{placements::ManualPlacement, promotion::PromotedInstance};
    let (mut session, layer_id) = fixture();
    let before = session.head().clone();
    let id = EntityId::deterministic("promoted-tree");
    let origin = PromotedInstance {
        layer_id,
        instance_id: EntityId::deterministic("generated-tree"),
        generation_hash: ContentHash::from_bytes([12; 32]),
        placement_id: id,
    };
    let placement = ManualPlacement {
        physics: None,
        id,
        name: "Promoted tree".into(),
        asset_id: AssetId::deterministic("oak"),
        asset_hash: ContentHash::from_bytes([34; 32]),
        transform: Transform::IDENTITY,
        locked: false,
    };
    let request = before
        .prepare_promotion(
            origin.clone(),
            placement.clone(),
            CommandId::deterministic("promote"),
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().version, 8);
    assert!(
        session
            .head()
            .prepare_promotion(
                origin.clone(),
                placement,
                CommandId::deterministic("duplicate-promotion")
            )
            .is_err()
    );
    let accepted = session.head().corrections.clone();
    let snapshot = forge_project::authoring::playtest::PlaytestSnapshot::from_zone(
        session.head(),
        session.head().content_hash().unwrap(),
    )
    .unwrap();
    assert_eq!(snapshot.promotions(), std::slice::from_ref(&origin));
    assert_eq!(snapshot.placements(), accepted.placements.as_ref().unwrap());
    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 22),
        (session.compact_archive_bytes().unwrap(), 23),
        (session.delta_archive_bytes().unwrap(), 24),
    ] {
        let mut json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head().corrections, accepted);
        reopened
            .execute(&action(&reopened, ZoneAction::Undo, "undo-promotion"))
            .unwrap();
        assert_eq!(reopened.head().corrections, before.corrections);
        reopened
            .execute(&action(&reopened, ZoneAction::Redo, "redo-promotion"))
            .unwrap();
        assert_eq!(reopened.head().corrections, accepted);
        json["version"] = serde_json::json!(version - 3);
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&json).unwrap()).is_err());
    }
    let mut base = session.head().base.clone();
    base.vegetation.as_mut().unwrap().seed += 1;
    let regen = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("regen-promotion"))
        .unwrap();
    session.execute(&regen).unwrap();
    assert_eq!(session.head().corrections, accepted);
    let mut removed = session.head().corrections.clone();
    removed.placements = Some(Vec::new());
    session
        .execute(&action(
            &session,
            ZoneAction::edit(removed),
            "delete-promoted-placement",
        ))
        .unwrap();
    assert_eq!(session.head().corrections.promotions, Some(vec![origin]));
}

#[test]
fn shared_species_draft_is_atomic_bounded_and_undoable() {
    use forge_project::zone_authoring::species::{LayerSpecies, spread_to_tree_layers};
    let (mut session, source) = fixture();
    let mut base = session.head().base.clone();
    let vegetation = base.vegetation.as_mut().unwrap();
    let mut second = vegetation.layers[0].clone();
    second.id = VegetationLayerId::new("second_trees");
    vegetation.layers.push(second);
    let request = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("second-tree-layer"))
        .unwrap();
    session.execute(&request).unwrap();
    let before = session.head().clone();
    let draft = vec![LayerSpecies {
        layer_id: source.clone(),
        prototypes: vec![prototype()],
    }];
    let choices = spread_to_tree_layers(&before, &draft, &source).unwrap();
    assert_eq!(choices.len(), 2);
    assert!(choices.windows(2).all(|w| w[0].layer_id < w[1].layer_id));
    assert!(choices.iter().all(|l| l.prototypes == draft[0].prototypes));
    assert_eq!(session.head(), &before);
    assert!(spread_to_tree_layers(&before, &[], &source).is_err());
    let mut invalid = draft.clone();
    invalid[0].layer_id = VegetationLayerId::new("missing");
    assert!(spread_to_tree_layers(&before, &invalid, &invalid[0].layer_id).is_err());
    let mut oversized = draft;
    oversized[0].prototypes = (0..33)
        .map(|i| SpeciesReference {
            asset_id: format!("tree-{i}"),
            ..prototype()
        })
        .collect();
    assert!(spread_to_tree_layers(&before, &oversized, &source).is_err());
    assert_eq!(session.head(), &before);
    let mut corrections = before.corrections.clone();
    corrections.species = Some(choices.clone());
    let request = action(&session, ZoneAction::edit(corrections), "shared-species");
    session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    session.execute(&request).unwrap();
    assert_eq!(session.head().base, before.base);
    let mut unchanged = session.head().corrections.clone();
    unchanged.species = before.corrections.species.clone();
    assert_eq!(unchanged, before.corrections);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head().corrections.species, Some(choices));
    reopened
        .execute(&action(&reopened, ZoneAction::Undo, "undo-shared-species"))
        .unwrap();
    assert_eq!(reopened.head().corrections, before.corrections);
}
fn action(session: &ZoneEditSession, action: ZoneAction, key: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn species_history_roundtrips_all_codecs_and_retains_regeneration_overrides() {
    let (mut session, id) = fixture();
    let old = session.archive_bytes().unwrap();
    assert_eq!(
        ZoneEditSession::from_archive_bytes(&old)
            .unwrap()
            .archive_bytes()
            .unwrap(),
        old
    );
    let request = session
        .head()
        .prepare_species(
            id.clone(),
            Some(vec![prototype()]),
            CommandId::deterministic("set-species"),
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), old);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().version, 7);
    let corrections = session.head().corrections.clone();
    let mut base = session.head().base.clone();
    base.vegetation.as_mut().unwrap().seed += 1;
    let regen = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("regen-species"))
        .unwrap();
    session.execute(&regen).unwrap();
    assert_eq!(session.head().corrections, corrections);
    let mut orphan = session.head().base.clone();
    orphan.vegetation = None;
    assert!(
        session
            .head()
            .prepare_regeneration(orphan, CommandId::deterministic("orphan-species"))
            .is_err()
    );
    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 19),
        (session.compact_archive_bytes().unwrap(), 20),
        (session.delta_archive_bytes().unwrap(), 21),
    ] {
        let mut json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        let remove = reopened
            .head()
            .prepare_species(id.clone(), None, CommandId::deterministic("remove-species"))
            .unwrap();
        reopened.execute(&remove).unwrap();
        let undo = action(&reopened, ZoneAction::Undo, "undo-species");
        reopened.execute(&undo).unwrap();
        assert_eq!(reopened.head().corrections, corrections);
        let redo = action(&reopened, ZoneAction::Redo, "redo-species");
        reopened.execute(&redo).unwrap();
        let final_bytes = reopened.archive_bytes().unwrap();
        assert_eq!(
            ZoneEditSession::from_archive_bytes(&final_bytes)
                .unwrap()
                .head()
                .corrections
                .species,
            Some(vec![])
        );
        json["version"] = (version - 3).into();
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&json).unwrap()).is_err());
    }
}

#[test]
fn species_invalid_proposals_do_not_mutate_the_head() {
    let (session, id) = fixture();
    let before = session.archive_bytes().unwrap();
    for values in [
        vec![],
        vec![prototype(); 2],
        vec![prototype(); 65],
        vec![SpeciesReference {
            asset_id: " ".into(),
            ..prototype()
        }],
    ] {
        assert!(
            session
                .head()
                .prepare_species(
                    id.clone(),
                    Some(values),
                    CommandId::deterministic("invalid-species")
                )
                .is_err()
        );
    }
    assert!(
        session
            .head()
            .prepare_species(
                VegetationLayerId::new("absent"),
                Some(vec![prototype()]),
                CommandId::deterministic("missing-species")
            )
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn promotion_quota_refusals_leave_the_authoritative_head_unchanged() {
    use forge_core::{AssetId, EntityId, Transform};
    use forge_project::zone_authoring::{
        placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement},
        promotion::PromotedInstance,
    };
    let (session, layer_id) = fixture();
    let placement = |i| ManualPlacement {
        physics: None,
        id: EntityId::deterministic(format!("quota-placement-{i}")),
        name: "Tree".into(),
        asset_id: AssetId::deterministic("oak"),
        asset_hash: ContentHash::from_bytes([34; 32]),
        transform: Transform::IDENTITY,
        locked: false,
    };
    let origin = |i| PromotedInstance {
        layer_id: layer_id.clone(),
        instance_id: EntityId::deterministic(format!("quota-origin-{i}")),
        generation_hash: ContentHash::from_bytes([12; 32]),
        placement_id: placement(i).id,
    };
    for retained_origins in [false, true] {
        let mut head = session.head().clone();
        if retained_origins {
            // Deleted author objects retain their exclusions, with a separate quota.
            let mut origins: Vec<_> = (0..MAX_MANUAL_PLACEMENTS).map(origin).collect();
            origins.sort_by_key(|o| o.instance_id);
            head.corrections.promotions = Some(origins);
        } else {
            let mut placements: Vec<_> = (0..MAX_MANUAL_PLACEMENTS).map(placement).collect();
            placements.sort_by_key(|p| p.id);
            head.corrections.placements = Some(placements);
        }
        head.version = head.corrections.schema_version();
        head.validate().unwrap();
        let before = head.clone();
        let refusal = head.prepare_promotion(
            origin(MAX_MANUAL_PLACEMENTS),
            placement(MAX_MANUAL_PLACEMENTS),
            CommandId::deterministic("promotion-quota"),
        );
        assert!(refusal.is_err());
        assert_eq!(head, before);
    }
}
