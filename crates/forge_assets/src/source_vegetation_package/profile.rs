// SPDX-License-Identifier: MIT
// Prototype mapping derived from dgreenheck/tidewater 1438b1ab, Vegetation.js.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Original prototype identifiers and shader parameter ABI; not a world population.
pub const PARAMETERS: [&str; 5] = [
    "lean_azimuth_radians",
    "lean_amount",
    "stem_height_m",
    "variation_seed",
    "vertical_shape_scale",
];

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PlantPrototype {
    Palms,
    Bananas,
    YoungPalms,
    Ferns,
    Monsteras,
    ElephantEars,
    Heliconias,
    Strelitzias,
    Trees,
    Shrubs,
}

impl PlantPrototype {
    /// Exact source identifiers are data contracts, independent of the consuming module.
    #[must_use]
    pub const fn key(self) -> &'static str {
        match self {
            Self::Palms => "tidewater_palms",
            Self::Bananas => "tidewater_bananas",
            Self::YoungPalms => "tidewater_young_palms",
            Self::Ferns => "tidewater_ferns",
            Self::Monsteras => "tidewater_monsteras",
            Self::ElephantEars => "tidewater_elephant_ears",
            Self::Heliconias => "tidewater_heliconias",
            Self::Strelitzias => "tidewater_strelitzias",
            Self::Trees => "tidewater_trees",
            Self::Shrubs => "tidewater_shrubs",
        }
    }

    pub fn from_key(key: &str) -> Result<Self, String> {
        match key {
            "tidewater_palms" => Ok(Self::Palms),
            "tidewater_bananas" => Ok(Self::Bananas),
            "tidewater_young_palms" => Ok(Self::YoungPalms),
            "tidewater_ferns" => Ok(Self::Ferns),
            "tidewater_monsteras" => Ok(Self::Monsteras),
            "tidewater_elephant_ears" => Ok(Self::ElephantEars),
            "tidewater_heliconias" => Ok(Self::Heliconias),
            "tidewater_strelitzias" => Ok(Self::Strelitzias),
            "tidewater_trees" => Ok(Self::Trees),
            "tidewater_shrubs" => Ok(Self::Shrubs),
            _ => Err("Unknown Tidewater prototype".into()),
        }
    }

    #[must_use]
    pub const fn group(self) -> &'static str {
        match self {
            Self::Palms => "palms",
            Self::Bananas => "bananas",
            Self::YoungPalms | Self::Ferns => "understory",
            Self::Monsteras => "monsteras",
            Self::ElephantEars | Self::Heliconias | Self::Strelitzias => "broadleaf",
            Self::Trees | Self::Shrubs => "canopy",
        }
    }

    /// Preserve the original variant seed boundary and source query envelope.
    #[must_use]
    pub fn shader_kind_and_radius(self, seed: f64) -> (f64, f64) {
        match self {
            Self::Palms => (0.0, 0.0),
            Self::Bananas => (if seed < 0.5 { 2.0 } else { 4.0 }, 130.0),
            Self::YoungPalms => (1.0, 150.0),
            Self::Ferns => (3.0, 66.0),
            Self::Monsteras => (11.0, 115.0),
            Self::ElephantEars => (12.0, 115.0),
            Self::Heliconias => (13.0, 115.0),
            Self::Strelitzias => (14.0, 115.0),
            Self::Trees => (0.0, 75.0),
            Self::Shrubs => (0.0, 55.0),
        }
    }

    pub fn validate_parameters(names: &[String]) -> Result<(), String> {
        if !(5..=6).contains(&names.len())
            || names[..5].iter().map(String::as_str).ne(PARAMETERS)
            || (names.len() == 6 && names[5] != "source_order")
        {
            return Err("Unsupported Tidewater prototype parameter contract".into());
        }
        Ok(())
    }
}

const WORLD_SHADER_SHA256: &str =
    "b0ff80e9eb09ff1a5e9959e3228d5bf81f809790d05faf3f010114d37d4e7cc6";
#[must_use]
pub fn world_shader_digest() -> forge_core::ContentHash {
    WORLD_SHADER_SHA256.parse().expect("fixed source digest")
}
#[must_use]
pub fn supports_world_shader(hash: forge_core::ContentHash) -> bool {
    hash == world_shader_digest()
}
pub fn validate_world_shader(source: &str) -> Result<(), String> {
    if source.len() > 1_000_000
        || !supports_world_shader(forge_core::bytes_sha256(source.as_bytes()))
    {
        return Err("Unsupported world plant shader profile".into());
    }
    Ok(())
}
/// Reference acquisition qualifier; renderer mechanisms do not impose this inventory.
pub fn validate_reference_population(groups: usize, instances: usize) -> Result<(), String> {
    if groups != 6 || instances != 65_383 {
        return Err("incomplete canonical island vegetation".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn typed_source_keys_preserve_every_original_identifier() {
        for prototype in [
            PlantPrototype::Palms,
            PlantPrototype::Bananas,
            PlantPrototype::YoungPalms,
            PlantPrototype::Ferns,
            PlantPrototype::Monsteras,
            PlantPrototype::ElephantEars,
            PlantPrototype::Heliconias,
            PlantPrototype::Strelitzias,
            PlantPrototype::Trees,
            PlantPrototype::Shrubs,
        ] {
            assert_eq!(
                PlantPrototype::from_key(prototype.key()).unwrap(),
                prototype
            );
        }
    }

    #[test]
    fn reference_inventory_and_known_shader_guards_do_not_become_renderer_defaults() {
        assert!(validate_reference_population(6, 65_383).is_ok());
        for (groups, count) in [(1, 2), (6, 65_382), (7, 65_383)] {
            assert!(validate_reference_population(groups, count).is_err());
        }
        assert!(supports_world_shader(world_shader_digest()));
        assert!(!supports_world_shader(forge_core::bytes_sha256(b"unknown")));
        assert!(validate_world_shader("@fragment fn fragment() {}").is_err());
    }
}
