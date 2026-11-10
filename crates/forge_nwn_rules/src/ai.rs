//! Deterministic perception and melee movement helpers.

use crate::character::Position;
use serde::{Deserialize, Serialize};

pub const DEFAULT_PERCEPTION_RANGE_M: f32 = 12.0;

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Perception {
    pub range_m: f32,
}

impl Default for Perception {
    fn default() -> Self {
        Self {
            range_m: DEFAULT_PERCEPTION_RANGE_M,
        }
    }
}

impl Perception {
    #[must_use]
    pub fn new(range_m: f32) -> Self {
        Self { range_m }
    }

    #[must_use]
    pub fn can_perceive(self, distance_m: f32, line_of_sight: bool) -> bool {
        distance_m.is_finite() && distance_m >= 0.0 && distance_m <= self.range_m && line_of_sight
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum MeleeStep {
    Attack,
    Move { to: Position },
}

#[must_use]
pub fn plan_melee_step(actor: Position, target: Position, reach_m: f32, speed_m: f32) -> MeleeStep {
    let distance = actor.distance(target);
    if distance <= reach_m {
        MeleeStep::Attack
    } else {
        MeleeStep::Move {
            to: actor.toward(target, speed_m),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn perception_requires_range_and_line_of_sight() {
        let perception = Perception::new(10.0);
        assert!(perception.can_perceive(9.0, true));
        assert!(!perception.can_perceive(9.0, false));
        assert!(!perception.can_perceive(11.0, true));
        assert!(!perception.can_perceive(f32::NAN, true));
    }

    #[test]
    fn melee_step_moves_or_attacks_deterministically() {
        let actor = Position::new(0.0, 0.0);
        let target = Position::new(0.0, 6.0);
        assert_eq!(
            plan_melee_step(actor, target, 2.0, 3.0),
            MeleeStep::Move {
                to: Position::new(0.0, 3.0)
            }
        );
        assert_eq!(
            plan_melee_step(Position::new(0.0, 4.5), target, 2.0, 3.0),
            MeleeStep::Attack
        );
    }
}
