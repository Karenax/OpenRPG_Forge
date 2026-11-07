//! Manual quest settings retain the same canonical definition and narrative graph.
use forge_gameplay::{ActorRole, GameplayDocument, QuestDefinition};

pub(crate) fn retouch(
    source: &GameplayDocument,
    definition: QuestDefinition,
) -> Result<GameplayDocument, String> {
    source.validate().map_err(|error| error.to_string())?;
    let index = source
        .quests
        .iter()
        .position(|quest| quest.id == definition.id)
        .ok_or("Choisir une quête présente dans cette carte.")?;
    if definition.name.trim().is_empty() || definition.name.len() > 256 {
        return Err("Nom de la quête : saisir un nom non vide et plus court.".into());
    }
    if !source
        .actor(definition.giver)
        .is_some_and(|actor| actor.role == ActorRole::Friendly)
    {
        return Err("PNJ donneur : choisir un personnage amical présent dans la carte.".into());
    }
    let required = source
        .item(definition.required_item)
        .ok_or("Objet demandé : choisir un objet d’inventaire disponible.")?;
    quantity(
        definition.required_quantity,
        required.max_stack,
        "Quantité demandée",
    )?;
    if let Some(item) = definition.reward_item {
        let reward = source
            .item(item)
            .ok_or("Récompense : choisir un objet d’inventaire disponible.")?;
        quantity(
            definition.reward_quantity,
            reward.max_stack,
            "Quantité de la récompense",
        )?;
    } else if definition.reward_quantity != 0 {
        return Err("Choisir une récompense avant d’indiquer sa quantité.".into());
    }
    let mut candidate = source.clone();
    candidate.quests[index] = definition;
    candidate.validate().map_err(|error| error.to_string())?;
    Ok(candidate)
}

fn quantity(value: u32, maximum: u32, label: &str) -> Result<(), String> {
    if value == 0 || value > maximum {
        return Err(format!(
            "{label} : saisir une quantité entre 1 et {maximum}."
        ));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{AreaId, EntityId, ModuleId};
    use forge_gameplay::{Condition, QuestConsumption, QuestFlow, QuestSolution};

    fn id(name: &str) -> EntityId {
        EntityId::deterministic(format!("quest-settings/{name}"))
    }

    fn fixture() -> GameplayDocument {
        let actors = ["player", "giver", "witness"].map(|name| {
            serde_json::json!({"id":id(name),"name":name,"role":if name=="player" {"player"} else {"friendly"},
                "position":{"x":0.0,"y":0.85,"z":0.0},"max_vitality":100,"protection":0,
                "inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false})
        });
        let items = ["proof", "badge", "seal"]
            .map(|name| serde_json::json!({"id":id(name),"name":name,"max_stack":10}));
        let source: GameplayDocument = serde_json::from_value(serde_json::json!({
            "schema":"forge.gameplay","version":1,
            "module_id":ModuleId::deterministic("quest-settings"),"area_id":AreaId::deterministic("quest-settings"),
            "profile":{"id":"quest-settings.minimal","version":1,"interaction_range_m":3.0,
                "attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,
                "max_action_receipts":512,"max_events_per_tick":128},
            "player":id("player"),"actors":actors,"items":items,"objects":[],"places":[],"dialogues":[],
            "quests":[{"id":id("quest"),"name":"Refuge","giver":id("giver"),"required_item":id("proof"),
                "required_quantity":1,"reward_item":id("badge"),"reward_quantity":1},
                {"id":id("other"),"name":"Other","giver":id("witness"),"required_item":id("proof"),
                "required_quantity":2,"reward_item":null,"reward_quantity":0}]
        }))
        .unwrap();
        source
            .with_quest_flow(QuestFlow {
                quest: id("quest"),
                prerequisites: vec![],
                solutions: vec![
                    QuestSolution {
                        id: id("collection"),
                        name: "Collect".into(),
                        condition: Condition::HasItem {
                            item: id("proof"),
                            quantity: 1,
                        },
                        consume: vec![QuestConsumption {
                            item: id("proof"),
                            quantity: 1,
                        }],
                    },
                    QuestSolution {
                        id: id("testimony"),
                        name: "Testify".into(),
                        condition: Condition::Always,
                        consume: vec![],
                    },
                ],
                failure_condition: None,
                journal_available: "Available".into(),
                journal_active: "Active".into(),
                journal_completed: "Completed".into(),
                journal_failed: "Failed".into(),
            })
            .unwrap()
    }

    #[test]
    fn manual_definition_retouch_preserves_both_solutions_and_every_other_record() {
        let source = fixture();
        let original = source.clone();
        let mut definition = source.quests[0].clone();
        definition.name = "Refuge retouché".into();
        definition.giver = id("witness");
        definition.required_quantity = 2;
        definition.reward_item = Some(id("seal"));
        definition.reward_quantity = 3;
        let result = retouch(&source, definition.clone()).unwrap();
        let mut expected = source.clone();
        expected.quests[0] = definition;
        assert_eq!(result, expected);
        assert_eq!(source, original);
        let reopened: GameplayDocument =
            serde_json::from_slice(&serde_json::to_vec(&result).unwrap()).unwrap();
        assert_eq!(reopened, result);
    }

    #[test]
    fn invalid_quantities_references_and_names_are_refused_without_changing_the_source() {
        let source = fixture();
        let original = source.clone();
        let initial = source.quests[0].clone();
        for field in 0..9 {
            let mut definition = initial.clone();
            match field {
                0 => definition.name.clear(),
                1 => definition.giver = id("player"),
                2 => definition.required_item = id("missing"),
                3 => definition.required_quantity = 0,
                4 => definition.required_quantity = 11,
                5 => definition.reward_item = Some(id("missing")),
                6 => definition.reward_quantity = 0,
                7 => definition.reward_quantity = 11,
                _ => definition.reward_item = None,
            }
            assert!(retouch(&source, definition).is_err(), "case {field}");
            assert_eq!(source, original);
        }
    }

    #[test]
    fn removal_of_reward_is_explicit_and_an_absent_quest_cannot_be_inserted() {
        let source = fixture();
        let mut definition = source.quests[0].clone();
        definition.reward_item = None;
        definition.reward_quantity = 0;
        let result = retouch(&source, definition.clone()).unwrap();
        assert_eq!(result.narrative, source.narrative);
        assert_eq!(result.quests[0].reward_item, None);
        assert_eq!(result.quests[0].reward_quantity, 0);
        definition.id = id("absent");
        assert!(retouch(&source, definition).is_err());
    }
}
