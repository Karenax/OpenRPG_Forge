//! Shared identities and ranges for source-profile graphics controls.
// SPDX-License-Identifier: MIT OR Apache-2.0
use super::Settings;

/// An individually adjustable source-profile renderer value.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Control {
    AutoExposure,
    AmbientOcclusion,
    Fxaa,
    TemporalAa,
    Exposure,
    Bloom,
    Saturation,
    Contrast,
    Sharpen,
    Vignette,
    FilmGrain,
    Haze,
    BounceLight,
    MotionBlur,
    LensFlare,
    UnderwaterShafts,
    UnderwaterLighting,
    UnderwaterCaustics,
    UnderwaterShadowRefraction,
    WaterRefractionCapture,
}

impl Control {
    /// The stable presentation order used by the existing graphics panel.
    pub const ALL: [Self; 20] = [
        Self::AutoExposure,
        Self::AmbientOcclusion,
        Self::Fxaa,
        Self::TemporalAa,
        Self::Exposure,
        Self::Bloom,
        Self::Saturation,
        Self::Contrast,
        Self::Sharpen,
        Self::Vignette,
        Self::FilmGrain,
        Self::Haze,
        Self::BounceLight,
        Self::MotionBlur,
        Self::LensFlare,
        Self::UnderwaterShafts,
        Self::UnderwaterLighting,
        Self::UnderwaterCaustics,
        Self::UnderwaterShadowRefraction,
        Self::WaterRefractionCapture,
    ];

    /// User-facing name shared by graphics panels.
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::AutoExposure => "Adaptation",
            Self::AmbientOcclusion => "Occlusion ambiante",
            Self::Fxaa => "FXAA",
            Self::TemporalAa => "TAA (expérimental)",
            Self::Exposure => "Exposition",
            Self::Bloom => "Bloom",
            Self::Saturation => "Saturation",
            Self::Contrast => "Contraste",
            Self::Sharpen => "Netteté",
            Self::Vignette => "Vignette",
            Self::FilmGrain => "Grain photo",
            Self::Haze => "Brume marine",
            Self::BounceLight => "Lumière rebond",
            Self::MotionBlur => "Flou de mouvement",
            Self::LensFlare => "Lens flare solaire",
            Self::UnderwaterShafts => "Rayons sous-marins",
            Self::UnderwaterLighting => "Surfaces immergées",
            Self::UnderwaterCaustics => "Caustiques",
            Self::UnderwaterShadowRefraction => "Réfraction des ombres",
            Self::WaterRefractionCapture => "Capture de réfraction",
        }
    }

    /// Format a control value for the shared graphics panel.
    #[must_use]
    pub fn display(self, values: Settings) -> String {
        match self {
            Self::AutoExposure => if values.auto_exposure > 0.5 {
                "Auto"
            } else {
                "Fixe"
            }
            .to_owned(),
            Self::Fxaa => if values.fxaa > 0.5 { "Actif" } else { "Arrêt" }.to_owned(),
            Self::TemporalAa => if values.temporal_aa > 0.5 {
                "Actif"
            } else {
                "Arrêt"
            }
            .to_owned(),
            Self::WaterRefractionCapture => if values.water_refraction_capture > 0.5 {
                "Actif"
            } else {
                "Arrêt"
            }
            .to_owned(),
            Self::Exposure => format!("{:+.1} EV", self.value(&values)),
            Self::MotionBlur => format!("{:.0}°", self.value(&values)),
            Self::LensFlare => format!("{:.2}", values.lens_flare),
            Self::Bloom => format!("{:.3}", values.bloom),
            Self::FilmGrain => format!("{:.3}", values.film_grain),
            _ => format!("{:.2}", self.value(&values)),
        }
    }

    /// Minimum, maximum and increment of this control.
    #[must_use]
    pub const fn bounds(self) -> (f32, f32, f32) {
        match self {
            Self::AutoExposure
            | Self::Fxaa
            | Self::TemporalAa
            | Self::UnderwaterLighting
            | Self::UnderwaterShadowRefraction
            | Self::WaterRefractionCapture => (0.0, 1.0, 1.0),
            Self::AmbientOcclusion => (0.0, 1.5, 0.01),
            Self::Exposure => (-3.0, 3.0, 0.1),
            Self::Bloom => (0.0, 0.3, 0.005),
            Self::Saturation => (0.5, 1.5, 0.01),
            Self::Contrast => (0.8, 1.3, 0.01),
            Self::Sharpen | Self::Vignette => (0.0, 1.0, 0.01),
            Self::FilmGrain => (0.0, 0.06, 0.001),
            Self::Haze | Self::UnderwaterCaustics => (0.0, 4.0, 0.05),
            Self::BounceLight => (0.0, 2.0, 0.01),
            Self::LensFlare | Self::UnderwaterShafts => (0.0, 2.0, 0.05),
            Self::MotionBlur => (0.0, 360.0, 18.0),
        }
    }

    /// Read this control from a settings value.
    #[must_use]
    pub fn value(self, values: &Settings) -> f32 {
        match self {
            Self::AutoExposure => values.auto_exposure,
            Self::AmbientOcclusion => values.ambient_occlusion,
            Self::Fxaa => values.fxaa,
            Self::TemporalAa => values.temporal_aa,
            Self::Exposure => values.exposure_ev,
            Self::Bloom => values.bloom,
            Self::Saturation => values.saturation,
            Self::Contrast => values.contrast,
            Self::Sharpen => values.sharpen,
            Self::Vignette => values.vignette,
            Self::FilmGrain => values.film_grain,
            Self::Haze => values.haze,
            Self::BounceLight => values.bounce_light,
            Self::MotionBlur => values.motion_blur_degrees,
            Self::LensFlare => values.lens_flare,
            Self::UnderwaterShafts => values.underwater_shafts,
            Self::UnderwaterLighting => values.underwater_lighting,
            Self::UnderwaterCaustics => values.underwater_caustics,
            Self::UnderwaterShadowRefraction => values.underwater_shadow_refraction,
            Self::WaterRefractionCapture => values.water_refraction_capture,
        }
    }

    fn field_mut(self, values: &mut Settings) -> &mut f32 {
        match self {
            Self::AutoExposure => &mut values.auto_exposure,
            Self::AmbientOcclusion => &mut values.ambient_occlusion,
            Self::Fxaa => &mut values.fxaa,
            Self::TemporalAa => &mut values.temporal_aa,
            Self::Exposure => &mut values.exposure_ev,
            Self::Bloom => &mut values.bloom,
            Self::Saturation => &mut values.saturation,
            Self::Contrast => &mut values.contrast,
            Self::Sharpen => &mut values.sharpen,
            Self::Vignette => &mut values.vignette,
            Self::FilmGrain => &mut values.film_grain,
            Self::Haze => &mut values.haze,
            Self::BounceLight => &mut values.bounce_light,
            Self::MotionBlur => &mut values.motion_blur_degrees,
            Self::LensFlare => &mut values.lens_flare,
            Self::UnderwaterShafts => &mut values.underwater_shafts,
            Self::UnderwaterLighting => &mut values.underwater_lighting,
            Self::UnderwaterCaustics => &mut values.underwater_caustics,
            Self::UnderwaterShadowRefraction => &mut values.underwater_shadow_refraction,
            Self::WaterRefractionCapture => &mut values.water_refraction_capture,
        }
    }

    /// Set an in-range finite value and report whether its bits changed.
    ///
    /// # Errors
    /// Refuses a non-finite or out-of-range value without changing settings.
    pub fn set(self, values: &mut Settings, next: f32) -> Result<bool, &'static str> {
        let (low, high, _) = self.bounds();
        if !next.is_finite() || !(low..=high).contains(&next) {
            return Err("Invalid source graphics control value");
        }
        let field = self.field_mut(values);
        let changed = field.to_bits() != next.to_bits();
        if changed {
            *field = next;
        }
        Ok(changed)
    }

    /// Move this value by the requested number of increments, clamped to its range.
    /// Enabling either antialiasing mode disables the other mode.
    ///
    /// # Errors
    /// Refuses invalid current values, non-finite directions or arithmetic overflow.
    pub fn step(self, values: &mut Settings, direction: f32) -> Result<bool, &'static str> {
        let (low, high, increment) = self.bounds();
        let current = self.value(values);
        if !current.is_finite() || !(low..=high).contains(&current) || !direction.is_finite() {
            return Err("Invalid source graphics control step");
        }
        let next = current + direction * increment;
        if !next.is_finite() {
            return Err("Invalid source graphics control step");
        }
        let changed = self.set(values, next.clamp(low, high))?;
        if changed {
            match self {
                Self::TemporalAa if values.temporal_aa > 0.5 => {
                    Self::Fxaa.set(values, 0.0)?;
                }
                Self::Fxaa if values.fxaa > 0.5 => {
                    Self::TemporalAa.set(values, 0.0)?;
                }
                _ => {}
            }
        }
        Ok(changed)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::BTreeSet;

    #[test]
    fn controls_cover_distinct_settings_and_enforce_their_bounds() {
        let baseline = Settings::default();
        assert!(baseline.valid());
        let original = serde_json::to_value(baseline).unwrap();
        let mut visited = BTreeSet::new();
        for control in Control::ALL {
            let (low, high, increment) = control.bounds();
            assert!(low < high && increment.is_finite() && increment > 0.0);
            let mut edited = baseline;
            let next = if control.value(&baseline).to_bits() == low.to_bits() {
                high
            } else {
                low
            };
            assert!(control.set(&mut edited, next).unwrap());
            assert!(edited.valid());
            let changed = serde_json::to_value(edited).unwrap();
            let keys: Vec<_> = original
                .as_object()
                .unwrap()
                .iter()
                .filter_map(|(key, value)| (changed[key] != *value).then_some(key.as_str()))
                .collect();
            assert_eq!(keys.len(), 1, "{control:?}");
            assert!(visited.insert(keys[0].to_owned()), "{control:?}");
            assert!(control.set(&mut edited, f32::NAN).is_err());
            assert!(control.set(&mut edited, f32::INFINITY).is_err());
            assert!(control.set(&mut edited, high + (high - low)).is_err());
            assert_eq!(control.value(&edited).to_bits(), next.to_bits());
            assert!(control.step(&mut edited, f32::NAN).is_err());
            assert_eq!(control.value(&edited).to_bits(), next.to_bits());
            control.set(&mut edited, low).unwrap();
            assert!(!control.step(&mut edited, -1.0).unwrap());
            control.set(&mut edited, high).unwrap();
            assert!(!control.step(&mut edited, 1.0).unwrap());
        }
        assert_eq!(visited.len(), original.as_object().unwrap().len());
    }

    #[test]
    fn last_enabled_antialiasing_mode_is_shared_by_all_graphics_panels() {
        let mut values = Settings::default();
        assert_eq!((values.fxaa, values.temporal_aa), (1.0, 0.0));
        assert!(Control::TemporalAa.step(&mut values, 1.0).unwrap());
        assert_eq!((values.fxaa, values.temporal_aa), (0.0, 1.0));
        assert!(Control::Fxaa.step(&mut values, 1.0).unwrap());
        assert_eq!((values.fxaa, values.temporal_aa), (1.0, 0.0));
        assert!(Control::Fxaa.step(&mut values, -1.0).unwrap());
        assert_eq!((values.fxaa, values.temporal_aa), (0.0, 0.0));
    }
}
