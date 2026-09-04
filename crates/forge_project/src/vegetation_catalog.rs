//! Forge-owned procedural vegetation species presets.
//!
//! A preset fixes every numeric generation parameter. Callers provide only the stable logical
//! identity, the display name and the area's grass material layer; asset bindings remain deferred
//! to Gate 4.

use crate::{
    TerrainMaterialLayerId, TerrainMaterialRole, VegetationCollisionProfile,
    VegetationCollisionShape, VegetationKind, VegetationLayer, VegetationMaterialFilter,
};
use serde::{Deserialize, Serialize};

/// Bounded procedural species currently available to the world editor.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum VegetationSpeciesPreset {
    Tree,
    Shrub,
    Fern,
    Grass,
}

impl VegetationSpeciesPreset {
    /// Builds one layer with the fixed Forge generation settings for this preset.
    #[must_use]
    pub fn build_layer(
        self,
        id: impl Into<String>,
        display_name: impl Into<String>,
        grass_material_layer_id: TerrainMaterialLayerId,
    ) -> VegetationLayer {
        match self {
            Self::Tree => VegetationLayer::new(
                id,
                display_name,
                VegetationKind::Tree,
                0.20,
                2,
                6.0,
                0.8,
                1.3,
                0.625,
                -40.0,
                64.0,
                Some(VegetationMaterialFilter::new(
                    TerrainMaterialRole::Grass,
                    grass_material_layer_id,
                    0.25,
                )),
                Some(VegetationCollisionProfile {
                    shape: VegetationCollisionShape::Capsule,
                    radius_m: 0.45,
                    height_m: 5.0,
                    activation_distance_m: 96.0,
                }),
                0.35,
            ),
            Self::Shrub => VegetationLayer::new(
                id,
                display_name,
                VegetationKind::Shrub,
                0.36,
                2,
                3.5,
                0.65,
                1.15,
                0.901,
                -40.0,
                64.0,
                Some(VegetationMaterialFilter::new(
                    TerrainMaterialRole::Grass,
                    grass_material_layer_id,
                    0.18,
                )),
                None,
                0.55,
            ),
            Self::Fern => VegetationLayer::new(
                id,
                display_name,
                VegetationKind::Fern,
                0.42,
                2,
                2.5,
                0.75,
                1.2,
                0.532,
                -40.0,
                56.0,
                Some(VegetationMaterialFilter::new(
                    TerrainMaterialRole::Grass,
                    grass_material_layer_id,
                    0.45,
                )),
                None,
                0.7,
            ),
            Self::Grass => VegetationLayer::new(
                id,
                display_name,
                VegetationKind::Grass,
                0.80,
                4,
                0.0,
                0.7,
                1.25,
                0.782,
                -40.0,
                60.0,
                Some(VegetationMaterialFilter::new(
                    TerrainMaterialRole::Grass,
                    grass_material_layer_id,
                    0.35,
                )),
                None,
                0.9,
            ),
        }
    }
}

#[cfg(test)]
#[allow(
    clippy::float_cmp,
    reason = "preset regression tests intentionally require exact authored constants"
)]
mod tests {
    use super::*;

    fn layer(preset: VegetationSpeciesPreset) -> VegetationLayer {
        preset.build_layer(
            "test_species",
            "Test species",
            TerrainMaterialLayerId::new("meadow_grass"),
        )
    }

    fn assert_common(
        preset: VegetationSpeciesPreset,
        layer: &VegetationLayer,
        kind: VegetationKind,
        minimum_weight: f32,
    ) {
        assert_eq!(
            serde_json::to_string(&preset).unwrap(),
            format!(
                "\"{}\"",
                match preset {
                    VegetationSpeciesPreset::Tree => "tree",
                    VegetationSpeciesPreset::Shrub => "shrub",
                    VegetationSpeciesPreset::Fern => "fern",
                    VegetationSpeciesPreset::Grass => "grass",
                }
            )
        );
        assert_eq!(layer.id.as_str(), "test_species");
        assert_eq!(layer.display_name, "Test species");
        assert_eq!(layer.kind, kind);
        assert_eq!(
            layer.material_filter,
            Some(VegetationMaterialFilter::new(
                TerrainMaterialRole::Grass,
                TerrainMaterialLayerId::new("meadow_grass"),
                minimum_weight,
            ))
        );
    }

    #[test]
    fn tree_preset_reproduces_the_forge_tree_layer() {
        let layer = layer(VegetationSpeciesPreset::Tree);
        assert_common(
            VegetationSpeciesPreset::Tree,
            &layer,
            VegetationKind::Tree,
            0.25,
        );
        assert_eq!(layer.acceptance_probability, 0.20);
        assert_eq!(layer.attempts_per_cell, 2);
        assert_eq!(layer.minimum_spacing_m, 6.0);
        assert_eq!(layer.minimum_scale, 0.8);
        assert_eq!(layer.maximum_scale, 1.3);
        assert_eq!(layer.maximum_slope_gradient, 0.625);
        assert_eq!(layer.minimum_altitude_m, -40.0);
        assert_eq!(layer.maximum_altitude_m, 64.0);
        assert_eq!(
            layer.collision,
            Some(VegetationCollisionProfile {
                shape: VegetationCollisionShape::Capsule,
                radius_m: 0.45,
                height_m: 5.0,
                activation_distance_m: 96.0,
            })
        );
        assert_eq!(layer.wind_response, 0.35);
    }

    #[test]
    fn shrub_preset_reproduces_the_forge_shrub_layer() {
        let layer = layer(VegetationSpeciesPreset::Shrub);
        assert_common(
            VegetationSpeciesPreset::Shrub,
            &layer,
            VegetationKind::Shrub,
            0.18,
        );
        assert_eq!(layer.acceptance_probability, 0.36);
        assert_eq!(layer.attempts_per_cell, 2);
        assert_eq!(layer.minimum_spacing_m, 3.5);
        assert_eq!(layer.minimum_scale, 0.65);
        assert_eq!(layer.maximum_scale, 1.15);
        assert_eq!(layer.maximum_slope_gradient, 0.901);
        assert_eq!(layer.minimum_altitude_m, -40.0);
        assert_eq!(layer.maximum_altitude_m, 64.0);
        assert_eq!(layer.collision, None);
        assert_eq!(layer.wind_response, 0.55);
    }

    #[test]
    fn fern_preset_reproduces_the_forge_fern_layer() {
        let layer = layer(VegetationSpeciesPreset::Fern);
        assert_common(
            VegetationSpeciesPreset::Fern,
            &layer,
            VegetationKind::Fern,
            0.45,
        );
        assert_eq!(layer.acceptance_probability, 0.42);
        assert_eq!(layer.attempts_per_cell, 2);
        assert_eq!(layer.minimum_spacing_m, 2.5);
        assert_eq!(layer.minimum_scale, 0.75);
        assert_eq!(layer.maximum_scale, 1.2);
        assert_eq!(layer.maximum_slope_gradient, 0.532);
        assert_eq!(layer.minimum_altitude_m, -40.0);
        assert_eq!(layer.maximum_altitude_m, 56.0);
        assert_eq!(layer.collision, None);
        assert_eq!(layer.wind_response, 0.7);
    }

    #[test]
    fn grass_preset_reproduces_the_forge_grass_layer() {
        let layer = layer(VegetationSpeciesPreset::Grass);
        assert_common(
            VegetationSpeciesPreset::Grass,
            &layer,
            VegetationKind::Grass,
            0.35,
        );
        assert_eq!(layer.acceptance_probability, 0.80);
        assert_eq!(layer.attempts_per_cell, 4);
        assert_eq!(layer.minimum_spacing_m, 0.0);
        assert_eq!(layer.minimum_scale, 0.7);
        assert_eq!(layer.maximum_scale, 1.25);
        assert_eq!(layer.maximum_slope_gradient, 0.782);
        assert_eq!(layer.minimum_altitude_m, -40.0);
        assert_eq!(layer.maximum_altitude_m, 60.0);
        assert_eq!(layer.collision, None);
        assert_eq!(layer.wind_response, 0.9);
    }
}
