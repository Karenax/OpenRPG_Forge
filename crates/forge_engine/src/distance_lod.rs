//! Reusable distance-based detail, fading and shadow selection.
//!
//! The caller supplies the asset's distances and scale factor. A validated policy
//! can be reused for every object without revalidating its configuration each frame.

pub mod fixed_bands;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Draw {
    pub lod: usize,
    pub fade: f32,
    pub outgoing: bool,
}

#[derive(Debug, PartialEq)]
pub struct Selection {
    pub draws: Vec<Draw>,
    /// Shadow selection is independent of the camera frustum.
    pub shadow: bool,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Policy {
    max_distance: f64,
    fade_start: f64,
    shadow_distance: f64,
    distance_per_unit: f64,
    transition_start_ratio: f64,
    max_distance_f32: f32,
    shadow_distance_f32: f32,
}

impl Policy {
    /// Conservative envelope for group culling; detailed selection still uses
    /// each object's own policy, including its fade and transition parameters.
    #[cfg(feature = "physics-rapier")]
    #[must_use]
    pub(crate) fn broad_phase_cover(self, other: Self) -> Self {
        Self {
            max_distance: self.max_distance.max(other.max_distance),
            shadow_distance: self.shadow_distance.max(other.shadow_distance),
            max_distance_f32: self.max_distance_f32.max(other.max_distance_f32),
            shadow_distance_f32: self.shadow_distance_f32.max(other.shadow_distance_f32),
            ..self
        }
    }

    /// Validate configuration once before it is reused by rendering systems.
    ///
    /// # Errors
    /// Rejects non-finite distances, invalid fade ranges and invalid scale factors.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Validated f32 bounds are used by the f32 broad phase."
    )]
    pub const fn new(
        max_distance: f64,
        fade_start: f64,
        shadow_distance: f64,
        distance_per_unit: f64,
        transition_start_ratio: f64,
    ) -> Result<Self, &'static str> {
        if !max_distance.is_finite()
            || max_distance <= 0.0
            || max_distance > f32::MAX as f64
            || !fade_start.is_finite()
            || fade_start < 0.0
            || fade_start > max_distance
            || !shadow_distance.is_finite()
            || shadow_distance < 0.0
            || shadow_distance > f32::MAX as f64
            || !distance_per_unit.is_finite()
            || distance_per_unit <= 0.0
            || !transition_start_ratio.is_finite()
            || transition_start_ratio < 0.0
            || transition_start_ratio >= 1.0
        {
            return Err("Invalid distance LOD policy");
        }
        Ok(Self {
            max_distance,
            fade_start,
            shadow_distance,
            distance_per_unit,
            transition_start_ratio,
            max_distance_f32: max_distance as f32,
            shadow_distance_f32: shadow_distance as f32,
        })
    }

    /// Conservative broad phase for a group enclosing every member's draw sphere.
    /// Invalid bounds keep the group active rather than incorrectly hiding it.
    #[must_use]
    pub fn group_may_contribute(
        self,
        distance: f32,
        radius: f32,
        frustum_intersects: bool,
    ) -> bool {
        if !distance.is_finite() || !radius.is_finite() || distance < 0.0 || radius < 0.0 {
            return true;
        }
        distance <= self.max_distance_f32 + radius
            && (frustum_intersects || distance < self.shadow_distance_f32 + radius)
    }

    /// Select detail levels with complementary transition and far-fade bands.
    ///
    /// # Errors
    /// Rejects non-finite or negative distances and non-positive lengths.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "GPU fade uniforms use f32; distance arithmetic remains f64."
    )]
    pub fn select(
        self,
        distance: f64,
        length: f64,
        frustum_intersects: bool,
    ) -> Result<Selection, String> {
        if !distance.is_finite() || distance < 0.0 || !length.is_finite() || length <= 0.0 {
            return Err("Invalid distance LOD inputs".into());
        }
        let mut selection = Selection {
            draws: Vec::new(),
            shadow: false,
        };
        if distance > self.max_distance {
            return Ok(selection);
        }
        selection.shadow = distance < self.shadow_distance;
        if !frustum_intersects {
            return Ok(selection);
        }
        let threshold = length * self.distance_per_unit;
        if distance > self.fade_start {
            selection.draws.push(Draw {
                lod: usize::from(distance >= threshold),
                fade: (1.0 - band(distance, self.fade_start, self.max_distance)) as f32,
                outgoing: false,
            });
        } else if distance > threshold * self.transition_start_ratio && distance < threshold {
            let fade = band(distance, threshold * self.transition_start_ratio, threshold) as f32;
            selection.draws.push(Draw {
                lod: 0,
                fade,
                outgoing: true,
            });
            selection.draws.push(Draw {
                lod: 1,
                fade,
                outgoing: false,
            });
        } else {
            selection.draws.push(Draw {
                lod: usize::from(distance >= threshold),
                fade: 1.0,
                outgoing: false,
            });
        }
        Ok(selection)
    }
}

fn band(distance: f64, start: f64, end: f64) -> f64 {
    let t = ((distance - start) / (end - start).max(1e-6)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn policy() -> Policy {
        Policy::new(90.0, 81.0, 22.0, 28.0, 0.88).unwrap()
    }

    #[test]
    fn selection_preserves_detail_fades_and_shadow_boundaries() {
        let policy = policy();
        let near = policy.select(0.0, 1.0, true).unwrap();
        assert_eq!(
            near.draws,
            vec![Draw {
                lod: 0,
                fade: 1.0,
                outgoing: false
            }]
        );
        assert!(near.shadow);
        let transition = policy.select(26.0, 1.0, true).unwrap();
        assert_eq!(transition.draws.len(), 2);
        assert_eq!(
            (transition.draws[0].lod, transition.draws[0].outgoing),
            (0, true)
        );
        assert_eq!(
            (transition.draws[1].lod, transition.draws[1].outgoing),
            (1, false)
        );
        assert_eq!(
            transition.draws[0].fade.to_bits(),
            transition.draws[1].fade.to_bits()
        );
        assert!((0.0..1.0).contains(&transition.draws[0].fade));
        assert_eq!(policy.select(28.0, 1.0, true).unwrap().draws[0].lod, 1);
        assert_eq!(
            policy.select(85.5, 1.0, true).unwrap().draws[0]
                .fade
                .to_bits(),
            0.5_f32.to_bits()
        );
        assert_eq!(
            policy.select(90.0, 1.0, true).unwrap().draws[0]
                .fade
                .to_bits(),
            0.0_f32.to_bits()
        );
        assert!(policy.select(90.1, 1.0, true).unwrap().draws.is_empty());
        assert!(policy.select(21.9, 1.0, false).unwrap().shadow);
        assert!(!policy.select(22.0, 1.0, false).unwrap().shadow);
    }

    #[test]
    fn broad_phase_keeps_near_shadows_and_rejects_no_invalid_bounds() {
        let policy = policy();
        assert!(policy.group_may_contribute(100.0, 10.0, true));
        assert!(!policy.group_may_contribute(100.1, 10.0, true));
        assert!(policy.group_may_contribute(31.9, 10.0, false));
        assert!(!policy.group_may_contribute(32.0, 10.0, false));
        assert!(policy.group_may_contribute(f32::NAN, 10.0, false));
    }

    #[test]
    fn other_modules_can_use_the_same_selection_with_their_own_configuration() {
        let policy = Policy::new(120.0, 108.0, 15.0, 40.0, 0.75).unwrap();
        assert_eq!(policy.select(40.0, 1.0, true).unwrap().draws[0].lod, 1);
        assert_eq!(
            policy.select(114.0, 1.0, true).unwrap().draws[0]
                .fade
                .to_bits(),
            0.5_f32.to_bits()
        );
        assert!(!policy.select(15.0, 1.0, false).unwrap().shadow);
        assert!(policy.group_may_contribute(124.0, 5.0, true));
    }

    #[test]
    fn invalid_configuration_and_inputs_are_rejected() {
        for policy in [
            Policy::new(f64::NAN, 0.0, 0.0, 1.0, 0.5),
            Policy::new(1.0, 2.0, 0.0, 1.0, 0.5),
            Policy::new(1.0, 0.0, f64::INFINITY, 1.0, 0.5),
            Policy::new(1.0, 0.0, 0.0, 0.0, 0.5),
            Policy::new(1.0, 0.0, 0.0, 1.0, 1.0),
        ] {
            assert!(policy.is_err());
        }
        for (distance, length) in [
            (f64::NAN, 1.0),
            (-1.0, 1.0),
            (1.0, 0.0),
            (1.0, f64::INFINITY),
        ] {
            assert!(policy().select(distance, length, true).is_err());
        }
    }
}
