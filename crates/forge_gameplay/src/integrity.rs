//! Validate persistent references and observation journals before activating a save.
use crate::{GameAction, GameRequest, GameSession, GameplayError, GameplayEvent, ObjectKind};
use std::collections::BTreeSet;

fn invalid(message: &str) -> GameplayError {
    GameplayError::IncompatibleSave(message.into())
}

impl GameSession {
    pub(crate) fn validate_saved_action(&self, request: &GameRequest) -> Result<(), GameplayError> {
        let valid = match request.action {
            GameAction::Pickup { object } => self
                .document
                .object(object)
                .is_some_and(|o| matches!(o.kind, ObjectKind::Pickup { .. })),
            GameAction::SetDoor { object, .. } => self
                .document
                .object(object)
                .is_some_and(|o| matches!(o.kind, ObjectKind::Door { .. })),
            GameAction::Push { object, impulse } | GameAction::Throw { object, impulse } => {
                crate::validation::position(impulse)?;
                self.document.object(object).is_some_and(|o| {
                    impulse != forge_core::Vec3::ZERO
                        && if matches!(request.action, GameAction::Throw { .. }) {
                            o.physics.throwable
                        } else {
                            o.physics.pushable
                        }
                })
            }
            GameAction::Ignite { object } | GameAction::Extinguish { object } => {
                self.document.object(object).is_some()
            }
            GameAction::Attack { target } => {
                target != request.actor && self.document.actor(target).is_some()
            }
            GameAction::ChooseDialogue {
                speaker,
                node,
                choice,
            } => self
                .document
                .actor(speaker)
                .and_then(|a| a.dialogue)
                .and_then(|id| self.document.dialogue(id))
                .and_then(|d| d.nodes.iter().find(|n| n.id == node))
                .is_some_and(|n| n.choices.iter().any(|c| c.id == choice)),
        };
        if valid {
            Ok(())
        } else {
            Err(invalid("action journal reference invalid"))
        }
    }

    pub(crate) fn validate_event(&self, event: &GameplayEvent) -> Result<(), GameplayError> {
        let valid = match *event {
            GameplayEvent::PickedUp {
                object,
                item,
                quantity,
            } => self
                .document
                .object(object)
                .is_some_and(|o| o.kind == ObjectKind::Pickup { item, quantity }),
            GameplayEvent::QuestChanged { quest, .. } => self.document.quest(quest).is_some(),
            GameplayEvent::Damage { target, amount, .. } => self
                .document
                .actor(target)
                .is_some_and(|a| amount <= a.max_vitality),
            GameplayEvent::Impact {
                source,
                target,
                initiator,
                energy_j,
            } => {
                self.document.object(source).is_some()
                    && (self.document.object(target).is_some()
                        || self.document.actor(target).is_some())
                    && initiator.is_none_or(|id| self.document.actor(id).is_some())
                    && energy_j.is_finite()
                    && energy_j >= 0.0
            }
            GameplayEvent::DoorChanged { object, .. } => self
                .document
                .object(object)
                .is_some_and(|o| matches!(o.kind, ObjectKind::Door { .. })),
            GameplayEvent::Impulse { object, impulse } => {
                crate::validation::position(impulse)?;
                self.document
                    .object(object)
                    .is_some_and(|o| o.physics.pushable || o.physics.throwable)
                    && impulse != forge_core::Vec3::ZERO
            }
            GameplayEvent::Destroyed { object } => self
                .document
                .object(object)
                .is_some_and(|o| o.physics.break_energy_j.is_some()),
            GameplayEvent::Released { object } => self
                .document
                .objects
                .iter()
                .any(|o| o.physics.supports.contains(&object)),
            GameplayEvent::Burning { object, active } => self
                .document
                .object(object)
                .is_some_and(|o| !active || o.physics.flammable),
            GameplayEvent::Fleeing { actor } => {
                self.document.actor(actor).is_some_and(|a| a.flees_danger)
            }
        };
        if valid {
            Ok(())
        } else {
            Err(invalid("event journal reference or value invalid"))
        }
    }

    pub(crate) fn validate_environment_state(&self) -> Result<(), GameplayError> {
        let quota = self.document.profile.max_events_per_tick;
        if self.state.tick_events_used > quota {
            return Err(invalid("tick event quota invalid"));
        }
        self.validate_contact_journal(quota)?;
        self.validate_last_frame(quota)
    }

    fn validate_contact_journal(&self, quota: usize) -> Result<(), GameplayError> {
        let mut contact_pairs = BTreeSet::new();
        for (id, receipt) in &self.state.contacts {
            self.validate_contact(&receipt.observation)?;
            if *id != receipt.observation.command_id
                || receipt.tick == 0
                || receipt.tick > self.state.tick
                || receipt.events.len() > quota
                || !contact_pairs.insert((
                    receipt.tick,
                    receipt.observation.source,
                    receipt.observation.target,
                ))
            {
                return Err(invalid("contact journal identity or tick invalid"));
            }
            for event in &receipt.events {
                self.validate_event(event)?;
            }
        }
        Ok(())
    }

    fn validate_last_frame(&self, quota: usize) -> Result<(), GameplayError> {
        let Some(last) = &self.state.last_frame else {
            if self.state.requires_physics_revalidation && self.state.tick_events_used == 0 {
                return Ok(());
            }
            let count = self
                .state
                .receipts
                .iter()
                .try_fold(0_usize, |count, receipt| {
                    count.checked_add(receipt.events.len())
                });
            return if self.state.tick == 0
                && self.state.contacts.is_empty()
                && count == Some(self.state.tick_events_used)
            {
                Ok(())
            } else {
                Err(invalid("physics tick has no retained observation"))
            };
        };
        if last.frame.tick != self.state.tick
            || last.frame.source_hash != self.source_hash
            || self.state.requires_physics_revalidation
            || last.events.len() > quota
            || last.frame.actors.len() > self.document.actors.len()
            || last.frame.objects.len() > self.document.objects.len()
            || last.frame.contacts.len() > quota
            || last.frame.heat.len() > quota
            || last.frame.danger.len() > quota
        {
            return Err(invalid("last physics frame bounds invalid"));
        }
        let mut observed = BTreeSet::new();
        for observation in &last.frame.actors {
            crate::validation::position(observation.position)?;
            if self.document.actor(observation.actor).is_none()
                || !observed.insert(observation.actor)
            {
                return Err(invalid("invalid saved actor observation"));
            }
        }
        for observation in &last.frame.objects {
            crate::validation::position(observation.position)?;
            crate::validation::rotation(observation.rotation_xyzw)?;
            if self.document.object(observation.object).is_none()
                || !observed.insert(observation.object)
            {
                return Err(invalid("invalid saved object observation"));
            }
        }
        for contact in &last.frame.contacts {
            self.validate_contact(contact)?;
        }
        for observation in &last.frame.heat {
            if self.document.object(observation.object).is_none()
                || self.document.actor(observation.actor).is_none()
            {
                return Err(invalid("invalid saved heat observation"));
            }
        }
        for observation in &last.frame.danger {
            if self.document.object(observation.object).is_none()
                || self.document.actor(observation.actor).is_none()
            {
                return Err(invalid("invalid saved danger observation"));
            }
        }
        for event in &last.events {
            self.validate_event(event)?;
        }
        let count = self
            .state
            .receipts
            .iter()
            .filter(|r| r.tick == self.state.tick)
            .try_fold(last.events.len(), |count, r| {
                count.checked_add(r.events.len())
            });
        if count != Some(self.state.tick_events_used) {
            return Err(invalid("tick event accounting invalid"));
        }
        Ok(())
    }
}
