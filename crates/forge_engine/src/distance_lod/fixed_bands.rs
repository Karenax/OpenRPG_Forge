//! Reusable three-level distance selection with cross-fades and a far fade.
//!
//! Adapted from Tidewater `ScannedDebris::update` and `LODFade::bandFade`,
//! revision 1438b1ab. Copyright 2026 DRG Software Solutions LLC;
//! see `third_party/tidewater/LICENSE`.

/// One level selected for a render instance.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Draw {
    pub lod: u8,
    pub fade: f32,
    pub outgoing: bool,
    shadow: bool,
}

impl Draw {
    #[must_use]
    pub const fn casts_shadow(self) -> bool {
        self.shadow
    }
}

/// Configurable selection policy; the module owns its distances and shadow level.
#[derive(Clone, Copy, Debug)]
pub struct Policy {
    first: f64,
    second: f64,
    maximum: f64,
    transition_width: f64,
    radius_factor: f64,
    shadow_level: Option<u8>,
}

impl Policy {
    /// Check the complete policy once before using it for many instances.
    ///
    /// # Errors
    /// Rejects nonfinite, overlapping, unordered or otherwise invalid bands.
    pub const fn new(
        first: f64,
        second: f64,
        maximum: f64,
        transition_width: f64,
        radius_factor: f64,
        shadow_level: Option<u8>,
    ) -> Result<Self, &'static str> {
        if !first.is_finite()
            || !second.is_finite()
            || !maximum.is_finite()
            || !transition_width.is_finite()
            || !radius_factor.is_finite()
            || first <= 0.0
            || second <= first
            || maximum <= second
            || transition_width <= 0.0
            || transition_width >= 1.0
            || radius_factor < 0.0
            || matches!(shadow_level, Some(level) if level > 2)
        {
            return Err("Invalid fixed-band LOD policy");
        }
        let first_end = first * (1.0 + transition_width / 2.0);
        let second_start = second * (1.0 - transition_width / 2.0);
        let second_end = second * (1.0 + transition_width / 2.0);
        let far_start = maximum * (1.0 - transition_width);
        if !first_end.is_finite()
            || !second_start.is_finite()
            || !second_end.is_finite()
            || !far_start.is_finite()
            || first_end > second_start
            || second_end > far_start
        {
            return Err("Overlapping fixed-band LOD policy");
        }
        Ok(Self {
            first,
            second,
            maximum,
            transition_width,
            radius_factor,
            shadow_level,
        })
    }

    /// Select up to two levels using the caller's instance and camera data.
    ///
    /// # Errors
    /// Rejects negative or nonfinite distance/radius and arithmetic overflow.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Distance arithmetic stays f64; the renderer consumes f32 fades."
    )]
    pub fn select(
        self,
        distance_m: f64,
        radius_m: f64,
        frustum_intersects: bool,
    ) -> Result<Vec<Draw>, String> {
        if !distance_m.is_finite() || !radius_m.is_finite() || distance_m < 0.0 || radius_m < 0.0 {
            return Err("Invalid fixed-band LOD distance or radius".into());
        }
        let mut draws = Vec::with_capacity(2);
        if !frustum_intersects {
            return Ok(draws);
        }
        let distance = distance_m - radius_m * self.radius_factor;
        if !distance.is_finite() {
            return Err("Fixed-band LOD distance overflow".into());
        }
        let width = self.transition_width;
        let t01 = super::band(
            distance,
            self.first * (1.0 - width / 2.0),
            self.first * (1.0 + width / 2.0),
        );
        let t12 = super::band(
            distance,
            self.second * (1.0 - width / 2.0),
            self.second * (1.0 + width / 2.0),
        );
        let out = super::band(distance, self.maximum * (1.0 - width), self.maximum);
        if out >= 1.0 {
            return Ok(draws);
        }
        let draw = |lod, fade: f64, outgoing| Draw {
            lod,
            fade: fade as f32,
            outgoing,
            shadow: self.shadow_level == Some(lod),
        };
        if t01 < 1.0 {
            draws.push(draw(0, t01, true));
        }
        if t01 > 0.0 && t12 < 1.0 {
            draws.push(draw(1, if t12 > 0.0 { t12 } else { t01 }, t12 > 0.0));
        }
        if t12 > 0.0 {
            draws.push(draw(2, t12 * (1.0 - out), false));
        }
        Ok(draws)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn configured_bands_preserve_transitions_and_shadow_selection() {
        let policy = Policy::new(22.0, 70.0, 260.0, 0.12, 6.0, Some(0)).unwrap();
        let near = policy.select(0.0, 0.0, true).unwrap();
        assert_eq!(near.len(), 1);
        assert_eq!(
            (near[0].lod, near[0].outgoing, near[0].casts_shadow()),
            (0, true, true)
        );
        let transition = policy.select(28.0, 1.0, true).unwrap();
        assert_eq!(transition.len(), 2);
        assert_eq!((transition[0].lod, transition[1].lod), (0, 1));
        assert!((transition[0].fade - 0.5).abs() < 1e-6);
        assert!((transition[1].fade - 0.5).abs() < 1e-6);
        assert!(transition[0].casts_shadow());
        assert!(!transition[1].casts_shadow());
        assert!(policy.select(266.0, 1.0, true).unwrap().is_empty());
        assert!(policy.select(1.0, 1.0, false).unwrap().is_empty());
    }

    #[test]
    fn another_module_can_supply_its_own_bands_and_shadow_level() {
        let policy = Policy::new(10.0, 40.0, 100.0, 0.2, 3.0, Some(2)).unwrap();
        let transition = policy.select(46.0, 2.0, true).unwrap();
        assert_eq!(transition.len(), 2);
        assert_eq!((transition[0].lod, transition[1].lod), (1, 2));
        assert!(!transition[0].casts_shadow());
        assert!(transition[1].casts_shadow());
    }

    #[test]
    fn invalid_policies_and_instance_inputs_are_refused() {
        assert!(Policy::new(1.0, 1.1, 2.0, 0.5, 1.0, None).is_err());
        assert!(Policy::new(1.0, 2.0, 3.0, 0.2, -1.0, None).is_err());
        assert!(Policy::new(1.0, 2.0, 3.0, 0.2, 1.0, Some(3)).is_err());
        let policy = Policy::new(10.0, 40.0, 100.0, 0.2, 3.0, None).unwrap();
        assert!(policy.select(f64::NAN, 1.0, true).is_err());
        assert!(policy.select(1.0, -1.0, true).is_err());
        assert!(policy.select(1.0, f64::MAX, true).is_err());
    }
}
