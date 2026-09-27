//! Initial recipe preparation, shared by adapters and independent of persistence.
use super::new_terrain;
use crate::TerrainDocument;
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NewTerrainSettings {
    pub seed: u64,
    pub elevation_m: f32,
    pub amplitude_m: f32,
    pub wavelength_m: f32,
}

impl Default for NewTerrainSettings {
    fn default() -> Self {
        Self {
            seed: 0,
            elevation_m: 0.0,
            amplitude_m: 0.0,
            wavelength_m: 100.0,
        }
    }
}

/// Prepare only. The existing Create command owns conflict checks and disk writes.
/// Extent/sampling remain the supported generator-v1 contract, not arbitrary sizes.
pub fn prepare_terrain(
    project_key: &str,
    area_key: &str,
    name: &str,
    settings: NewTerrainSettings,
) -> Result<TerrainDocument, String> {
    for (value, range, label) in [
        (
            settings.elevation_m,
            -4096.0..=4096.0,
            "Altitude : −4096 à 4096 m",
        ),
        (settings.amplitude_m, 0.0..=128.0, "Amplitude : 0 à 128 m"),
        (
            settings.wavelength_m,
            16.0..=512.0,
            "Échelle du relief : 16 à 512 m",
        ),
    ] {
        if !value.is_finite() || !range.contains(&value) {
            return Err(label.into());
        }
    }
    let mut document = new_terrain(project_key, area_key, name)?;
    document.seed = settings.seed;
    document.height_sampling.base_elevation_m = settings.elevation_m;
    document.height_sampling.amplitude_m = settings.amplitude_m;
    document.height_sampling.base_frequency_cycles_per_m = settings.wavelength_m.recip();
    document.validate().map_err(|error| error.to_string())?;
    Ok(document)
}

/// Prepare a new zone in an existing project/module without inventing project keys.
pub fn prepare_module_terrain(
    project: forge_core::ProjectId,
    module: forge_core::ModuleId,
    area_key: &str,
    name: &str,
    settings: NewTerrainSettings,
) -> Result<TerrainDocument, String> {
    let mut document = prepare_terrain(&project.to_string(), area_key, name, settings)?;
    document.project_id = project;
    document.area_id = forge_core::AreaId::deterministic(format!("module/{module}/{area_key}"));
    document.validate().map_err(|e| e.to_string())?;
    Ok(document)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn initial_recipe_is_deterministic_bounded_and_keeps_legacy_defaults() {
        let defaults = NewTerrainSettings::default();
        assert_eq!(
            prepare_terrain("p", "z", "Zone", defaults).unwrap(),
            new_terrain("p", "z", "Zone").unwrap()
        );
        let settings = NewTerrainSettings {
            seed: u64::MAX,
            elevation_m: 12.0,
            amplitude_m: 24.0,
            wavelength_m: 200.0,
        };
        let first = prepare_terrain("p", "z", "Collines", settings).unwrap();
        assert_eq!(
            first,
            prepare_terrain("p", "z", "Collines", settings).unwrap()
        );
        assert!(first.height_deltas.is_empty());
        assert_eq!(
            first.area_id,
            new_terrain("p", "z", "Zone").unwrap().area_id
        );
        assert_ne!(
            first.content_hash().unwrap(),
            new_terrain("p", "z", "Zone")
                .unwrap()
                .content_hash()
                .unwrap()
        );
        for bad in [f32::NAN, f32::INFINITY, -1.0, 129.0] {
            assert!(
                prepare_terrain(
                    "p",
                    "z",
                    "Zone",
                    NewTerrainSettings {
                        amplitude_m: bad,
                        ..defaults
                    }
                )
                .is_err()
            );
        }
        for bad in [f32::NAN, f32::NEG_INFINITY, 15.0, 513.0] {
            assert!(
                prepare_terrain(
                    "p",
                    "z",
                    "Zone",
                    NewTerrainSettings {
                        wavelength_m: bad,
                        ..defaults
                    }
                )
                .is_err()
            );
        }
        for bad in [f32::NAN, f32::INFINITY, -4097.0, 4097.0] {
            assert!(
                prepare_terrain(
                    "p",
                    "z",
                    "Zone",
                    NewTerrainSettings {
                        elevation_m: bad,
                        ..defaults
                    }
                )
                .is_err()
            );
        }
    }
}
