//! Lot D preparation: retained ability costs join the existing item-conservation ledger.
use super::{ItemLedger, invalid};
use crate::GameplayError;
use forge_core::EntityId;

impl ItemLedger<'_> {
    pub(super) fn spend_ability_costs(
        &mut self,
        actor: EntityId,
        ability: EntityId,
    ) -> Result<(), GameplayError> {
        let costs = self
            .document
            .simulation
            .as_ref()
            .and_then(|simulation| simulation.ability(ability))
            .ok_or_else(|| invalid("retained ability cost source is absent"))?
            .item_costs
            .clone();
        for cost in costs {
            self.spend(actor, cost.item, cost.quantity)?;
        }
        Ok(())
    }
}
