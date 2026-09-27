//! Bounded proposal inputs; accepted recipes still live only in the zone head.
use forge_project::{
    VegetationLayerId,
    zone_authoring::{ZoneAuthoringHead, ZoneBase},
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum ReconciliationRecipe {
    Seed {
        seed: u64,
    },
    Layer {
        layer_id: VegetationLayerId,
        acceptance_probability: f32,
        minimum_spacing_m: f32,
    },
    Habitat {
        layer_id: VegetationLayerId,
        minimum_altitude_m: f32,
        maximum_altitude_m: f32,
        maximum_slope_gradient: f32,
    },
}

impl ReconciliationRecipe {
    pub(super) fn base(&self, head: &ZoneAuthoringHead) -> Result<ZoneBase, String> {
        match self {
            Self::Seed { seed } => {
                let mut base = head.base.clone();
                base.vegetation
                    .as_mut()
                    .ok_or("Recette végétale absente")?
                    .seed = *seed;
                Ok(base)
            }
            Self::Layer {
                layer_id,
                acceptance_probability,
                minimum_spacing_m,
            } => crate::editor_vegetation::prepare_layer_base(
                head,
                layer_id,
                *acceptance_probability,
                *minimum_spacing_m,
            ),
            Self::Habitat {
                layer_id,
                minimum_altitude_m,
                maximum_altitude_m,
                maximum_slope_gradient,
            } => crate::editor_vegetation::prepare_layer_habitat_base(
                head,
                layer_id,
                *minimum_altitude_m,
                *maximum_altitude_m,
                *maximum_slope_gradient,
            ),
        }
    }
}
