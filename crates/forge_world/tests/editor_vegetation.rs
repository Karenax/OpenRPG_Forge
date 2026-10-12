use forge_project::authoring::new_terrain;
use forge_world::{VegetationGenerationPlan, editor_vegetation};

#[test]
fn reconciliation_preserves_manual_tree_across_regeneration_and_archives() {
    use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform};
    use forge_project::zone_authoring::{
        placements::ManualPlacement,
        promotion::{PromotedInstance, PromotionReconciliation},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    };
    let mut session = painted_order_session();
    let (tree, hash) = first_tree(session.head());
    let origin = PromotedInstance {
        layer_id: tree.layer_id,
        instance_id: tree.id.as_entity_id(),
        generation_hash: hash,
        placement_id: EntityId::deterministic("reconciled-manual-tree"),
    };
    let placement = ManualPlacement {
        physics: None,
        id: origin.placement_id,
        name: "Retouche verrouillée".into(),
        asset_id: AssetId::deterministic("tree"),
        asset_hash: ContentHash::from_bytes([42; 32]),
        transform: Transform::IDENTITY,
        locked: true,
    };
    let promote = session
        .head()
        .prepare_promotion(
            origin.clone(),
            placement.clone(),
            CommandId::deterministic("promote-reconciliation"),
        )
        .unwrap();
    session.execute(&promote).unwrap();
    verify_recipe_reconciliation(&session);
    let before = session.head().clone();
    let mut candidate = before.clone();
    candidate.base.vegetation.as_mut().unwrap().seed += 1;
    let (replacement, hash) = first_tree(&candidate);
    let mut next = origin;
    next.instance_id = replacement.id.as_entity_id();
    next.layer_id = replacement.layer_id;
    next.generation_hash = hash;
    let proposal = PromotionReconciliation {
        base: candidate.base,
        origins: vec![next],
    };
    let mut invalid = proposal.clone();
    invalid.origins.clear();
    assert!(
        forge_world::promotion_preflight::prepare_reconciliation(
            &before,
            invalid,
            CommandId::deterministic("drop-origin")
        )
        .is_err()
    );
    let request = forge_world::promotion_preflight::prepare_reconciliation(
        &before,
        proposal,
        CommandId::deterministic("reconcile"),
    )
    .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    let after = session.head().clone();
    assert_eq!(after.corrections.placements, Some(vec![placement]));
    forge_world::promotion_preflight::validate_origins(&after).unwrap();
    verify_reconciliation_choices(&before, &after);
    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 25),
        (session.compact_archive_bytes().unwrap(), 26),
        (session.delta_archive_bytes().unwrap(), 27),
    ] {
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), &after);
        for (action, expected) in [(ZoneAction::Undo, &before), (ZoneAction::Redo, &after)] {
            let request = ZoneRequest {
                command_id: CommandId::deterministic(format!("reconcile-{action:?}")),
                expected_revision: reopened.head().revision,
                expected_hash: reopened.head().content_hash().unwrap(),
                action,
            };
            reopened.execute(&request).unwrap();
            assert_eq!(reopened.head().base, expected.base);
            assert_eq!(reopened.head().corrections, expected.corrections);
            forge_world::promotion_preflight::validate_origins(reopened.head()).unwrap();
        }
        let mut json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        json["version"] = serde_json::json!(version - 3);
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&json).unwrap()).is_err());
    }
}

fn verify_recipe_reconciliation(source: &forge_project::zone_authoring::session::ZoneEditSession) {
    use forge_project::zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest};
    use forge_world::promotion_preflight::{ReconciliationChoices, ReconciliationRecipe};
    let head = source.head();
    let original = head.clone();
    let origin = &head.corrections.promotions.as_ref().unwrap()[0];
    let recipes = [
        ReconciliationRecipe::Layer {
            layer_id: origin.layer_id.clone(),
            acceptance_probability: 0.73,
            minimum_spacing_m: 8.0,
        },
        ReconciliationRecipe::Habitat {
            layer_id: origin.layer_id.clone(),
            minimum_altitude_m: -2000.0,
            maximum_altitude_m: 2000.0,
            maximum_slope_gradient: 0.5,
        },
    ];
    let mut hashes = Vec::new();
    for recipe in recipes {
        let choices = ReconciliationChoices::for_recipe(head, recipe.clone()).unwrap();
        assert_eq!(choices.recipe, recipe);
        assert_eq!(
            choices.base.vegetation.as_ref().unwrap().seed,
            head.base.vegetation.as_ref().unwrap().seed
        );
        assert_ne!(choices.base, head.base);
        hashes.push(choices.generation_hash);
        let encoded = serde_json::to_vec(&recipe).unwrap();
        assert_eq!(
            serde_json::from_slice::<ReconciliationRecipe>(&encoded).unwrap(),
            recipe
        );
        let selected = std::collections::BTreeMap::from([(
            origin.placement_id,
            choices.trees().next().unwrap().id.as_entity_id(),
        )]);
        let request = choices.prepare(head, &selected).unwrap();
        let mut session =
            ZoneEditSession::from_archive_bytes(&source.delta_archive_bytes().unwrap()).unwrap();
        let preview = session.preview(&request).unwrap();
        assert_eq!(session.head(), head);
        assert_eq!(session.execute(&request).unwrap(), preview);
        assert_eq!(session.execute(&request).unwrap(), preview);
        let applied = session.head().clone();
        assert_eq!(applied.base, choices.base);
        let mut preserved = applied.corrections.clone();
        preserved
            .promotions
            .clone_from(&head.corrections.promotions);
        assert_eq!(preserved, head.corrections);
        forge_world::promotion_preflight::validate_origins(&applied).unwrap();
        let mut reopened =
            ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
        assert_eq!(reopened.head(), &applied);
        for (action, expected) in [(ZoneAction::Undo, head), (ZoneAction::Redo, &applied)] {
            let request = ZoneRequest {
                command_id: forge_core::CommandId::deterministic(format!("recipe-{action:?}")),
                expected_hash: reopened.head().content_hash().unwrap(),
                expected_revision: reopened.head().revision,
                action,
            };
            reopened.execute(&request).unwrap();
            assert_eq!(reopened.head().base, expected.base);
            assert_eq!(reopened.head().corrections, expected.corrections);
        }
    }
    assert_ne!(hashes[0], hashes[1]);
    let invalid = ReconciliationRecipe::Layer {
        layer_id: origin.layer_id.clone(),
        acceptance_probability: -0.1,
        minimum_spacing_m: 8.0,
    };
    assert!(ReconciliationChoices::for_recipe(head, invalid).is_err());
    let empty = ReconciliationRecipe::Layer {
        layer_id: origin.layer_id.clone(),
        acceptance_probability: 0.0,
        minimum_spacing_m: 8.0,
    };
    let choices = ReconciliationChoices::for_recipe(head, empty).unwrap();
    assert!(choices.trees().next().is_none());
    assert!(
        choices
            .prepare(head, &std::collections::BTreeMap::new())
            .is_err()
    );
    assert_eq!(head, &original);
}

#[test]
fn promotion_preflight_refuses_lost_origins_but_accepts_metadata_changes() {
    let session = painted_order_session();
    let mut head = session.head().clone();
    let (terrain, vegetation) = head.effective().unwrap();
    let plan = VegetationGenerationPlan::from_documents(&terrain, &vegetation.unwrap()).unwrap();
    let generated = plan.generate().unwrap();
    let tree = generated
        .chunks
        .iter()
        .flat_map(|c| &c.instances)
        .find(|i| i.kind == forge_project::VegetationKind::Tree)
        .unwrap();
    head.corrections.promotions = Some(vec![
        forge_project::zone_authoring::promotion::PromotedInstance {
            layer_id: tree.layer_id.clone(),
            instance_id: tree.id.as_entity_id(),
            generation_hash: plan.source_hash(),
            placement_id: forge_core::EntityId::deterministic("deleted-promoted-tree"),
        },
    ]);
    head.version = head.corrections.schema_version();
    let before = head.clone();
    forge_world::promotion_preflight::validate_origins(&head).unwrap();
    let seed = head.base.vegetation.as_ref().unwrap().seed;
    let error =
        forge_world::editor_vegetation::prepare_request(&head, seed.wrapping_add(1)).unwrap_err();
    assert!(error.contains(&tree.id.as_entity_id().to_string()));
    assert!(error.contains("objet supprimé, exclusion conservée"));
    assert!(error.contains("Aucune retouche supprimée"));
    assert_eq!(head, before);
    let mut compatible = head.base.clone();
    compatible.vegetation.as_mut().unwrap().layers[0].display_name = "Renamed layer".into();
    forge_world::promotion_preflight::prepare_regeneration(
        &head,
        compatible,
        forge_core::CommandId::deterministic("compatible-regeneration"),
    )
    .unwrap();
    assert_eq!(head, before);
}

#[test]
fn promoted_instance_filter_preserves_exact_neighbours_and_spacing() {
    use forge_core::EntityId;
    use forge_project::zone_authoring::promotion::PromotedInstance;
    let session = painted_order_session();
    let (terrain, vegetation) = session.head().effective().unwrap();
    let mut vegetation = vegetation.unwrap();
    // Exercise the density-expansion branch, not only collision-protected trees.
    for layer in &mut vegetation.layers {
        layer.collision = None;
    }
    let plan = VegetationGenerationPlan::from_documents(&terrain, &vegetation).unwrap();
    let original = plan.generate().unwrap();
    let tree = original
        .chunks
        .iter()
        .flat_map(|c| &c.instances)
        .find(|i| i.kind == forge_project::VegetationKind::Tree)
        .unwrap();
    let origin = PromotedInstance {
        layer_id: tree.layer_id.clone(),
        instance_id: tree.id.as_entity_id(),
        generation_hash: plan.source_hash(),
        placement_id: EntityId::deterministic("promoted"),
    };
    assert_eq!(
        plan.clone()
            .with_promotions(&[])
            .unwrap()
            .generate()
            .unwrap(),
        original
    );
    let filtered_plan = plan
        .clone()
        .with_promotions(std::slice::from_ref(&origin))
        .unwrap();
    assert_ne!(filtered_plan.source_hash(), plan.source_hash());
    let filtered = filtered_plan.generate().unwrap();
    verify_promotion_selection(&plan, &filtered_plan, tree, original.terrain_document_hash);
    let mut expected = original.chunks.clone();
    for chunk in &mut expected {
        let before = chunk.instances.len();
        chunk.instances.retain(|i| i.id != tree.id);
        if chunk.instances.len() != before {
            chunk.kind_counts.trees -= 1;
        }
    }
    assert_eq!(filtered.chunks, expected);
    assert_eq!(filtered.kind_counts.trees + 1, original.kind_counts.trees);
    for density in [0.1, 0.5, 1.0, 1.25, 2.0] {
        let mut expected = plan
            .generate_render_density(density)
            .unwrap()
            .generated
            .chunks;
        for chunk in &mut expected {
            let count = chunk.instances.len();
            chunk.instances.retain(|i| i.id != tree.id);
            if count != chunk.instances.len() {
                chunk.kind_counts.trees -= 1;
            }
        }
        let rendered = filtered_plan.generate_render_density(density).unwrap();
        assert_eq!(rendered.generated.chunks, expected);
        assert_eq!(rendered.canonical_placements, filtered.kind_counts.total());
        assert_eq!(
            rendered.generated.vegetation_document_hash,
            filtered_plan.source_hash()
        );
    }
    for chunk in &filtered.chunks {
        assert_eq!(
            &filtered_plan.generate_chunk(chunk.coordinate).unwrap(),
            chunk
        );
    }
    assert!(
        plan.clone()
            .with_promotions(&[origin.clone(), origin.clone()])
            .is_err()
    );
    let missing = PromotedInstance {
        instance_id: EntityId::deterministic("absent"),
        ..origin
    };
    assert!(
        plan.with_promotions(&[missing])
            .unwrap()
            .generate()
            .is_err()
    );
}

fn verify_reconciliation_choices(
    before: &forge_project::zone_authoring::ZoneAuthoringHead,
    after: &forge_project::zone_authoring::ZoneAuthoringHead,
) {
    use std::collections::BTreeMap;
    let choices = forge_world::promotion_preflight::ReconciliationChoices::for_seed(
        before,
        after.base.vegetation.as_ref().unwrap().seed,
    )
    .unwrap();
    assert!(choices.prepare(before, &BTreeMap::new()).is_err());
    let selected = after
        .corrections
        .promotions
        .as_ref()
        .unwrap()
        .iter()
        .map(|o| (o.placement_id, o.instance_id))
        .collect::<BTreeMap<_, _>>();
    let request = choices.prepare(before, &selected).unwrap();
    assert!(matches!(
        request.action,
        forge_project::zone_authoring::session::ZoneAction::ReconcilePromotions(_)
    ));
    assert!(choices.prepare(after, &selected).is_err());
    let invalid = selected
        .keys()
        .map(|id| (*id, forge_core::EntityId::deterministic("missing-choice")))
        .collect();
    assert!(choices.prepare(before, &invalid).is_err());
    verify_ranked_choices(before, &choices);
}

fn verify_ranked_choices(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    choices: &forge_world::promotion_preflight::ReconciliationChoices,
) {
    use forge_world::promotion_preflight::ReconciliationChoices;
    let original = head.clone();
    let object = choices.origins[0].placement_id;
    let ranked = choices.ranked_trees(head, object).unwrap();
    assert!(ranked.len() > 20);
    assert_eq!(ranked.len(), choices.trees().count());
    assert!(ranked.windows(2).all(|pair| {
        let left = pair[0].distance_m.unwrap();
        let right = pair[1].distance_m.unwrap();
        left < right
            || (left.to_bits() == right.to_bits()
                && pair[0].tree.id.as_entity_id() < pair[1].tree.id.as_entity_id())
    }));
    let seed = choices.base.vegetation.as_ref().unwrap().seed;
    let target = ranked.last().unwrap().tree;
    let mut moved = head.clone();
    let placement = &mut moved.corrections.placements.as_mut().unwrap()[0];
    placement.transform.translation_m = target.position_m;
    // Height is deliberately different: ranking is horizontal, not 3D distance.
    placement.transform.translation_m.y += 10.0;
    assert!(choices.ranked_trees(&moved, object).is_err());
    let moved_choices = ReconciliationChoices::for_seed(&moved, seed).unwrap();
    let nearby = moved_choices.ranked_trees(&moved, object).unwrap();
    assert_eq!(nearby[0].tree.id, target.id);
    assert_eq!(nearby[0].distance_m.unwrap().to_bits(), 0.0_f64.to_bits());
    assert!(
        choices
            .ranked_trees(head, forge_core::EntityId::deterministic("not-promoted"))
            .is_err()
    );
    let mut removed = head.clone();
    removed.corrections.placements = None;
    let tombstone = ReconciliationChoices::for_seed(&removed, seed).unwrap();
    let unlocated = tombstone.ranked_trees(&removed, object).unwrap();
    assert_eq!(unlocated.len(), ranked.len());
    assert!(unlocated.iter().all(|entry| entry.distance_m.is_none()));
    assert!(
        unlocated
            .windows(2)
            .all(|pair| pair[0].tree.id.as_entity_id() < pair[1].tree.id.as_entity_id())
    );
    assert_eq!(head, &original);
    // Listing never fills the author's correspondence map or mutates origins.
    assert!(
        choices
            .prepare(head, &std::collections::BTreeMap::new())
            .is_err()
    );
    assert_eq!(choices.origins, original.corrections.promotions.unwrap());
}

fn first_tree(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
) -> (forge_world::VegetationInstance, forge_core::ContentHash) {
    let (terrain, vegetation) = head.effective().unwrap();
    let plan = VegetationGenerationPlan::from_documents(&terrain, &vegetation.unwrap()).unwrap();
    let generated = plan.generate().unwrap();
    let tree = generated
        .chunks
        .iter()
        .flat_map(|c| &c.instances)
        .find(|i| i.kind == forge_project::VegetationKind::Tree)
        .unwrap()
        .clone();
    (tree, plan.source_hash())
}

fn verify_promotion_selection(
    plan: &VegetationGenerationPlan,
    filtered: &VegetationGenerationPlan,
    tree: &forge_world::VegetationInstance,
    terrain: forge_core::ContentHash,
) {
    let resolve = |candidate: &VegetationGenerationPlan, terrain_hash, vegetation_hash| {
        candidate.resolve_promotion_source(
            terrain_hash,
            vegetation_hash,
            &tree.layer_id,
            tree.id.as_entity_id(),
        )
    };
    assert_eq!(resolve(plan, terrain, plan.source_hash()).unwrap(), *tree);
    assert!(resolve(plan, plan.source_hash(), plan.source_hash()).is_err());
    assert!(resolve(plan, terrain, filtered.source_hash()).is_err());
    assert!(resolve(filtered, terrain, filtered.source_hash()).is_err());
}

fn painted_order_session() -> forge_project::zone_authoring::session::ZoneEditSession {
    use forge_core::CommandId;
    use forge_project::zone_authoring::{
        session::ZoneEditSession, vegetation_commands::VegetationEdit,
    };
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("order-mask", "zone", "Zone").unwrap())
            .unwrap();
    session
        .execute(&editor_vegetation::prepare_request(session.head(), 42).unwrap())
        .unwrap();
    let id = session.head().base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    for (name, edit) in [
        (
            "paint",
            VegetationEdit::SetDensityMask {
                layer_id: id.clone(),
                mask: forge_project::VegetationDensityMask {
                    vertices_per_axis: 2,
                    samples_u8: vec![0, 255, 128, 0],
                },
            },
        ),
        (
            "lock",
            VegetationEdit::SetDensityLock {
                layer_id: id.clone(),
                locked: true,
            },
        ),
    ] {
        let request = session
            .head()
            .prepare_vegetation_edit(edit, CommandId::deterministic(name))
            .unwrap();
        session.execute(&request).unwrap();
    }
    session
}

#[test]
fn authored_seed_round_trip_restores_exact_instances_after_archive_reopen() {
    use forge_project::zone_authoring::session::ZoneEditSession;
    let mut session = painted_order_session();
    let original = session.head().clone();
    let generate = |head: &forge_project::zone_authoring::ZoneAuthoringHead| {
        let (terrain, vegetation) = head.effective().unwrap();
        let generated = VegetationGenerationPlan::from_documents(&terrain, &vegetation.unwrap())
            .unwrap()
            .generate()
            .unwrap();
        // Source receipt/revision changes legitimately; compare the complete spatial output.
        generated.chunks.clone()
    };
    let initial = generate(&original);
    assert!(initial.iter().any(|chunk| !chunk.instances.is_empty()));
    assert_eq!(initial, generate(&original));
    session
        .execute(&editor_vegetation::prepare_request(session.head(), 43).unwrap())
        .unwrap();
    assert_ne!(initial, generate(session.head()));
    assert_eq!(session.head().corrections, original.corrections);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(generate(reopened.head()), generate(session.head()));
    reopened
        .execute(&editor_vegetation::prepare_request(reopened.head(), 42).unwrap())
        .unwrap();
    let final_session =
        ZoneEditSession::from_archive_bytes(&reopened.archive_bytes().unwrap()).unwrap();
    assert_ne!(final_session.head().revision, original.revision);
    assert_eq!(
        final_session.head().base.vegetation,
        original.base.vegetation
    );
    assert_eq!(final_session.head().base.terrain, original.base.terrain);
    assert_eq!(final_session.head().corrections, original.corrections);
    assert_eq!(generate(final_session.head()), initial);
    println!(
        "seed_cycle=42->43->42 exact_chunks_and_instances_restored=true locked_masks_preserved=true"
    );
}

#[test]
fn habitat_altitude_filters_generation_without_changing_neighbor_instances() {
    let mut session = painted_order_session();
    let generate = |head: &forge_project::zone_authoring::ZoneAuthoringHead| {
        let (terrain, vegetation) = head.effective().unwrap();
        let generated = VegetationGenerationPlan::from_documents(&terrain, &vegetation.unwrap())
            .unwrap()
            .generate()
            .unwrap();
        let mut instances = generated
            .chunks
            .iter()
            .flat_map(|chunk| chunk.instances.clone())
            .collect::<Vec<_>>();
        instances.sort_by_key(|instance| instance.id.as_entity_id());
        instances
    };
    let original = generate(session.head());
    let id = session.head().base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    assert!(original.iter().any(|instance| instance.layer_id == id));
    let neighbors = original
        .into_iter()
        .filter(|instance| instance.layer_id != id)
        .collect::<Vec<_>>();
    assert!(!neighbors.is_empty());
    for (minimum, maximum) in [(9000.0, 9500.0), (-9500.0, -9000.0)] {
        let request = editor_vegetation::prepare_layer_habitat_request(
            session.head(),
            &id,
            minimum,
            maximum,
            0.65,
        )
        .unwrap();
        session.execute(&request).unwrap();
        assert_eq!(generate(session.head()), neighbors);
    }
}

#[test]
fn habitat_limits_preserve_locked_masks_and_survive_history_and_reopen() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest};
    let mut session = painted_order_session();
    let before = session.head().clone();
    let archive = session.archive_bytes().unwrap();
    let vegetation = before.base.vegetation.as_ref().unwrap();
    let id = &vegetation.layers[0].id;
    for (min, max, slope) in [
        (2.0, 1.0, 1.0),
        (-10001.0, 0.0, 1.0),
        (0.0, 10001.0, 1.0),
        (0.0, 1.0, -1.0),
        (0.0, 1.0, 16.1),
        (f32::NAN, 1.0, 1.0),
        (0.0, f32::INFINITY, 1.0),
        (0.0, 1.0, f32::NAN),
    ] {
        assert!(
            editor_vegetation::prepare_layer_habitat_request(&before, id, min, max, slope).is_err()
        );
    }
    assert!(
        editor_vegetation::prepare_layer_habitat_request(
            &before,
            &forge_project::VegetationLayerId::new("absent"),
            0.0,
            1.0,
            1.0,
        )
        .is_err()
    );
    let request =
        editor_vegetation::prepare_layer_habitat_request(&before, id, 9000.0, 9500.0, 0.25)
            .unwrap();
    assert_eq!(
        request,
        editor_vegetation::prepare_layer_habitat_request(&before, id, 9000.0, 9500.0, 0.25)
            .unwrap()
    );
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), archive);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    let mut expected = vegetation.clone();
    expected.layers[0].minimum_altitude_m = 9000.0;
    expected.layers[0].maximum_altitude_m = 9500.0;
    expected.layers[0].maximum_slope_gradient = 0.25;
    assert_eq!(session.head().base.vegetation.as_ref(), Some(&expected));
    assert_eq!(session.head().corrections, before.corrections);
    assert_eq!(session.head().base.terrain, before.base.terrain);
    assert!(
        editor_vegetation::prepare_layer_habitat_request(session.head(), id, 9000.0, 9500.0, 0.25)
            .is_err()
    );
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    for (name, action) in [
        ("undo-habitat", ZoneAction::Undo),
        ("redo-habitat", ZoneAction::Redo),
    ] {
        reopened
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic(name),
                expected_hash: reopened.head().content_hash().unwrap(),
                expected_revision: reopened.head().revision,
                action,
            })
            .unwrap();
        assert_eq!(reopened.head().corrections, before.corrections);
        if name == "undo-habitat" {
            assert_eq!(reopened.head().base, before.base);
        }
    }
    assert_eq!(reopened.head().base.vegetation.as_ref(), Some(&expected));
}

#[test]
fn layer_name_is_reviewed_reversible_and_keeps_recipe_and_painted_corrections() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest};
    let mut session = painted_order_session();
    let before = session.head().clone();
    let id = before.base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    for invalid in ["", "  ", &"é".repeat(65)] {
        assert!(editor_vegetation::prepare_layer_name_request(&before, &id, invalid).is_err());
    }
    assert!(
        editor_vegetation::prepare_layer_name_request(
            &before,
            &forge_project::VegetationLayerId::new("absent"),
            "Pins",
        )
        .is_err()
    );
    let request =
        editor_vegetation::prepare_layer_name_request(&before, &id, "  Pins de la berge  ")
            .unwrap();
    assert_eq!(
        request,
        editor_vegetation::prepare_layer_name_request(&before, &id, "Pins de la berge").unwrap()
    );
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    let mut expected = before.base.vegetation.clone().unwrap();
    expected.layers[0].display_name = "Pins de la berge".into();
    assert_eq!(session.head().base.vegetation.as_ref(), Some(&expected));
    assert_eq!(session.head().corrections, before.corrections);
    assert!(
        editor_vegetation::prepare_layer_name_request(session.head(), &id, "Pins de la berge")
            .is_err()
    );
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    for (name, action) in [
        ("undo-name", ZoneAction::Undo),
        ("redo-name", ZoneAction::Redo),
    ] {
        reopened
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic(name),
                expected_hash: reopened.head().content_hash().unwrap(),
                expected_revision: reopened.head().revision,
                action,
            })
            .unwrap();
        assert_eq!(reopened.head().corrections, before.corrections);
        if name == "undo-name" {
            assert_eq!(reopened.head().base, before.base);
        }
    }
    assert_eq!(reopened.head().base.vegetation.as_ref(), Some(&expected));
}

#[test]
fn layer_order_preserves_painted_locked_masks_and_generated_instances() {
    use editor_vegetation::LayerMembershipEdit;
    use forge_project::zone_authoring::session::ZoneEditSession;
    let mut session = painted_order_session();
    let before = session.head().clone();
    let id = before.base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    let generate = |head: &forge_project::zone_authoring::ZoneAuthoringHead| {
        let (terrain, vegetation) = head.effective().unwrap();
        let generated = VegetationGenerationPlan::from_documents(&terrain, &vegetation.unwrap())
            .unwrap()
            .generate()
            .unwrap();
        let mut instances = generated
            .chunks
            .iter()
            .flat_map(|chunk| chunk.instances.clone())
            .collect::<Vec<_>>();
        instances.sort_by_key(|instance| instance.id.as_entity_id());
        instances
    };
    let original_instances = generate(&before);
    assert!(!original_instances.is_empty());
    let removal = editor_vegetation::prepare_layer_membership_request(
        &before,
        &id,
        LayerMembershipEdit::Remove,
    );
    assert!(removal.unwrap_err().contains("DensityLayerRemoved"));
    let copy = editor_vegetation::prepare_layer_membership_request(
        &before,
        &id,
        LayerMembershipEdit::Copy,
    )
    .unwrap();
    let mut copied =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let receipt = copied.preview(&copy).unwrap();
    assert_eq!(copied.head(), &before);
    assert_eq!(copied.execute(&copy).unwrap(), receipt);
    assert_eq!(copied.execute(&copy).unwrap(), receipt);
    assert_eq!(copied.head().corrections, before.corrections);
    let old_ids = before
        .base
        .vegetation
        .as_ref()
        .unwrap()
        .layers
        .iter()
        .map(|layer| layer.id.clone())
        .collect::<Vec<_>>();
    let existing = generate(copied.head())
        .into_iter()
        .filter(|instance| old_ids.contains(&instance.layer_id))
        .collect::<Vec<_>>();
    assert_eq!(existing, original_instances);
    let new_id = copied
        .head()
        .base
        .vegetation
        .as_ref()
        .unwrap()
        .layers
        .iter()
        .find(|layer| !old_ids.contains(&layer.id))
        .unwrap()
        .id
        .clone();
    let removal = editor_vegetation::prepare_layer_membership_request(
        copied.head(),
        &new_id,
        LayerMembershipEdit::Remove,
    )
    .unwrap();
    copied.execute(&removal).unwrap();
    assert_eq!(copied.head().base.vegetation, before.base.vegetation);
    assert_eq!(copied.head().corrections, before.corrections);
    let request = editor_vegetation::prepare_layer_order_request(&before, &id, false).unwrap();
    session.execute(&request).unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head().corrections, before.corrections);
    let reordered_instances = generate(reopened.head());
    assert_eq!(reordered_instances, original_instances);
    println!(
        "instances_preserved={} painted_locked_mask_preserved=true",
        original_instances.len()
    );
}

#[test]
fn layer_order_is_reversible_and_keeps_layer_values_and_corrections() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest};
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("order", "zone", "Zone").unwrap())
            .unwrap();
    session
        .execute(&editor_vegetation::prepare_request(session.head(), 42).unwrap())
        .unwrap();
    let before = session.head().clone();
    let id = before.base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    assert!(editor_vegetation::prepare_layer_order_request(&before, &id, true).is_err());
    assert!(
        editor_vegetation::prepare_layer_order_request(
            &before,
            &forge_project::VegetationLayerId::new("absent"),
            false
        )
        .is_err()
    );
    let request = editor_vegetation::prepare_layer_order_request(&before, &id, false).unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    let mut expected = before.base.vegetation.clone().unwrap();
    expected.layers.swap(0, 1);
    assert_eq!(session.head().base.vegetation.as_ref(), Some(&expected));
    assert_eq!(session.head().corrections, before.corrections);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    for (name, action) in [
        ("undo-order", ZoneAction::Undo),
        ("redo-order", ZoneAction::Redo),
    ] {
        reopened
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic(name),
                expected_hash: reopened.head().content_hash().unwrap(),
                expected_revision: reopened.head().revision,
                action,
            })
            .unwrap();
        assert_eq!(reopened.head().corrections, before.corrections);
        if name == "undo-order" {
            assert_eq!(reopened.head().base, before.base);
        }
    }
    assert_eq!(reopened.head().base.vegetation.as_ref(), Some(&expected));
}

#[test]
fn recipe_uses_the_authored_zone_and_its_material_ids() {
    let terrain = new_terrain("other-project", "other-area", "New zone").unwrap();
    let original = terrain.clone();
    let source = editor_vegetation::prepare(&terrain, 42).unwrap();
    assert_eq!(source, editor_vegetation::prepare(&terrain, 42).unwrap());
    assert_eq!(source.project_id, terrain.project_id);
    assert_eq!(source.area_id, terrain.area_id);
    assert_eq!(source.revision, terrain.revision);
    assert!(source.exclusions.is_empty());
    assert!(
        source
            .layers
            .iter()
            .all(|layer| layer.density_mask.is_none())
    );
    VegetationGenerationPlan::from_documents(&terrain, &source).unwrap();
    assert_eq!(terrain, original);
    let another_seed = editor_vegetation::prepare(&terrain, 43).unwrap();
    assert_eq!(source.layers, another_seed.layers);
    assert_ne!(
        source.content_hash().unwrap(),
        another_seed.content_hash().unwrap()
    );
}

#[test]
fn invalid_source_is_rejected_before_recipe_creation() {
    let mut terrain = new_terrain("project", "area", "Zone").unwrap();
    terrain.height_sampling.base_elevation_m = f32::NAN;
    assert!(editor_vegetation::prepare(&terrain, 42).is_err());
}

#[test]
fn layer_recipe_edit_is_reviewed_reversible_and_preserves_masks_and_other_layers() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::{
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        vegetation_commands::VegetationEdit,
    };
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("layers", "zone", "Zone").unwrap())
            .unwrap();
    let init = editor_vegetation::prepare_request(session.head(), 42).unwrap();
    session.execute(&init).unwrap();
    let layer_id = session.head().base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    let mask = session
        .head()
        .prepare_vegetation_edit(
            VegetationEdit::SetDensityMask {
                layer_id: layer_id.clone(),
                mask: forge_project::VegetationDensityMask {
                    vertices_per_axis: 2,
                    samples_u8: vec![0, 255, 255, 0],
                },
            },
            CommandId::deterministic("paint-layer"),
        )
        .unwrap();
    session.execute(&mask).unwrap();
    let lock = session
        .head()
        .prepare_vegetation_edit(
            VegetationEdit::SetDensityLock {
                layer_id: layer_id.clone(),
                locked: true,
            },
            CommandId::deterministic("lock-layer"),
        )
        .unwrap();
    session.execute(&lock).unwrap();
    let before = session.head().clone();
    let request = editor_vegetation::prepare_layer_request(&before, &layer_id, 0.7, 3.0).unwrap();
    assert_eq!(
        request,
        editor_vegetation::prepare_layer_request(&before, &layer_id, 0.7, 3.0).unwrap()
    );
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().corrections, before.corrections);
    let vegetation = session.head().base.vegetation.as_ref().unwrap();
    assert_eq!(
        vegetation.layers[0].acceptance_probability.to_bits(),
        0.7_f32.to_bits()
    );
    assert_eq!(
        vegetation.layers[0].minimum_spacing_m.to_bits(),
        3.0_f32.to_bits()
    );
    assert_eq!(
        vegetation.layers[1],
        before.base.vegetation.as_ref().unwrap().layers[1]
    );
    assert_eq!(vegetation.seed, 42);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-layer"),
        expected_hash: reopened.head().content_hash().unwrap(),
        expected_revision: reopened.head().revision,
        action: ZoneAction::Undo,
    };
    reopened.execute(&undo).unwrap();
    assert_eq!(reopened.head().base, before.base);
    assert_eq!(reopened.head().corrections, before.corrections);
    for (density, spacing) in [
        (f32::NAN, 3.0),
        (1.1, 3.0),
        (-0.1, 3.0),
        (0.5, 0.0),
        (0.5, f32::INFINITY),
    ] {
        assert!(
            editor_vegetation::prepare_layer_request(&before, &layer_id, density, spacing).is_err()
        );
    }
    assert!(
        editor_vegetation::prepare_layer_request(
            &before,
            &forge_project::VegetationLayerId::new("absent"),
            0.5,
            3.0
        )
        .is_err()
    );
    assert_eq!(session.head().corrections, before.corrections);
}

#[test]
fn recipe_commands_initialize_and_reseed_without_erasing_corrections() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        vegetation_commands::VegetationEdit,
    };
    let terrain = new_terrain("recipe-command", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let legacy = forge_project::terrain_edit::TerrainEditSession::new(terrain).unwrap();
    let proposal = ZoneMigrationProposal::prepare(
        &legacy.archive_bytes().unwrap(),
        hash,
        GeneratorStamp {
            name: "fixture".into(),
            version: "1".into(),
            inputs_hash: hash,
        },
        None,
    )
    .unwrap();
    let mut session = ZoneEditSession::from_migration(proposal).unwrap();
    let original = session.head().clone();
    let request = editor_vegetation::prepare_request(session.head(), 42).unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &original);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().base.terrain, original.base.terrain);
    let layer_id = session.head().base.vegetation.as_ref().unwrap().layers[0]
        .id
        .clone();
    let density = session
        .head()
        .prepare_vegetation_edit(
            VegetationEdit::SetDensityMask {
                layer_id: layer_id.clone(),
                mask: forge_project::VegetationDensityMask {
                    vertices_per_axis: 2,
                    samples_u8: vec![0, 0, 255, 255],
                },
            },
            CommandId::deterministic("mask"),
        )
        .unwrap();
    session.execute(&density).unwrap();
    let lock = session
        .head()
        .prepare_vegetation_edit(
            VegetationEdit::SetDensityLock {
                layer_id,
                locked: true,
            },
            CommandId::deterministic("lock"),
        )
        .unwrap();
    session.execute(&lock).unwrap();
    let before = session.head().clone();
    let reseed = editor_vegetation::prepare_request(session.head(), 43).unwrap();
    session.execute(&reseed).unwrap();
    assert_eq!(session.head().corrections, before.corrections);
    assert_eq!(
        session.head().base.vegetation.as_ref().unwrap().layers,
        before.base.vegetation.as_ref().unwrap().layers
    );
    assert!(editor_vegetation::prepare_request(session.head(), 43).is_err());
    let reopened = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-seed"),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action: ZoneAction::Undo,
    };
    session.execute(&undo).unwrap();
    assert_eq!(session.head().base, before.base);
    assert_eq!(session.head().corrections, before.corrections);
}
