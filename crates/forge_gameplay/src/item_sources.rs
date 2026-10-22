//! Structural authoring hints. This does not prove collision/navigation reachability.
use crate::{DialogueEffect, GameplayDocument, ObjectKind};
use forge_core::EntityId;
use std::collections::{BTreeMap, BTreeSet};

impl GameplayDocument {
    /// Potential quantities from authored pickups, unlockable containers and
    /// unique quest rewards, excluding the quest whose own prerequisite is tested.
    #[must_use]
    pub fn potential_item_quantities(
        &self,
        excluded_object: Option<EntityId>,
        blocked_quest: EntityId,
        target_item: EntityId,
        target_quantity: u64,
    ) -> BTreeMap<EntityId, u64> {
        let mut quantities = BTreeMap::new();
        for object in &self.objects {
            if Some(object.id) == excluded_object {
                continue;
            }
            if let ObjectKind::Pickup { item, quantity } = object.kind {
                *quantities.entry(item).or_default() += u64::from(quantity);
            }
        }
        let mut opened = BTreeSet::new();
        let mut rewarded = BTreeSet::new();
        loop {
            if quantities.get(&target_item).copied().unwrap_or_default() >= target_quantity {
                break;
            }
            let mut added_source = false;
            for object in &self.objects {
                if Some(object.id) == excluded_object || opened.contains(&object.id) {
                    continue;
                }
                let ObjectKind::Container { definition } = &object.kind else {
                    continue;
                };
                if definition
                    .key
                    .is_some_and(|key| quantities.get(&key).copied().unwrap_or_default() == 0)
                {
                    continue;
                }
                for (&item, &quantity) in &definition.initial_items {
                    *quantities.entry(item).or_default() += u64::from(quantity);
                }
                opened.insert(object.id);
                added_source = true;
            }
            // Optional quests must not consume an already sufficient direct source.
            if quantities.get(&target_item).copied().unwrap_or_default() >= target_quantity {
                break;
            }
            for quest in &self.quests {
                if quest.id == blocked_quest
                    || rewarded.contains(&quest.id)
                    || quest.reward_quantity == 0
                    || !self.quest_has_item_path(quest.id, quest.giver)
                {
                    continue;
                }
                let Some(reward_item) = quest.reward_item else {
                    continue;
                };
                if quantities
                    .get(&quest.required_item)
                    .copied()
                    .unwrap_or_default()
                    < u64::from(quest.required_quantity)
                {
                    continue;
                }
                *quantities.entry(quest.required_item).or_default() -=
                    u64::from(quest.required_quantity);
                *quantities.entry(reward_item).or_default() += u64::from(quest.reward_quantity);
                rewarded.insert(quest.id);
                added_source = true;
                if quantities.get(&target_item).copied().unwrap_or_default() >= target_quantity {
                    return quantities;
                }
            }
            if !added_source {
                break;
            }
        }
        quantities
    }

    fn quest_has_item_path(&self, quest: EntityId, giver: EntityId) -> bool {
        let Some(dialogue) = self
            .actor(giver)
            .and_then(|a| a.dialogue)
            .and_then(|id| self.dialogue(id))
        else {
            return false;
        };
        let effects = || {
            dialogue
                .nodes
                .iter()
                .flat_map(|node| &node.choices)
                .flat_map(|choice| &choice.effects)
        };
        effects()
            .any(|effect| matches!(effect,DialogueEffect::AcceptQuest {quest:id} if *id == quest))
            && effects().any(
                |effect| matches!(effect,DialogueEffect::CompleteQuest {quest:id} if *id == quest),
            )
    }
}
