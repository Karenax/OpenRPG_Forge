use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct CanonicalTransform {
    pub position: [f32; 3],
    pub rotation: [f32; 4],
}

/// NWN Z-up `[x, y, z]` to the bundle's right-handed Y-up `[x, z, -y]`.
#[must_use]
pub fn canonical_position(source: [f32; 3]) -> Option<[f32; 3]> {
    source
        .iter()
        .all(|value| value.is_finite())
        .then_some([source[0], source[2], -source[1]])
}

#[must_use]
pub fn source_position(canonical: [f32; 3]) -> Option<[f32; 3]> {
    canonical.iter().all(|value| value.is_finite()).then_some([
        canonical[0],
        -canonical[2],
        canonical[1],
    ])
}

#[must_use]
pub fn canonical_quaternion(source: [f32; 4]) -> Option<[f32; 4]> {
    source
        .iter()
        .all(|value| value.is_finite())
        .then_some([source[0], source[2], -source[1], source[3]])
}

#[must_use]
pub fn source_quaternion(canonical: [f32; 4]) -> Option<[f32; 4]> {
    canonical.iter().all(|value| value.is_finite()).then_some([
        canonical[0],
        -canonical[2],
        canonical[1],
        canonical[3],
    ])
}

#[must_use]
pub fn canonical_yaw(yaw_radians: f32) -> Option<[f32; 4]> {
    yaw_radians.is_finite().then(|| {
        let half = yaw_radians * 0.5;
        [0.0, half.sin(), 0.0, half.cos()]
    })
}

#[must_use]
pub fn canonical_quarter_turn(orientation: u32) -> [f32; 4] {
    let yaw = match orientation % 4 {
        0 => 0.0,
        1 => std::f32::consts::FRAC_PI_2,
        2 => std::f32::consts::PI,
        _ => 3.0 * std::f32::consts::FRAC_PI_2,
    };
    canonical_yaw(yaw).expect("a finite quarter-turn always has a quaternion")
}

/// Applies the NWN-to-canonical transform exactly once.
#[must_use]
pub fn canonical_transform(position: [f32; 3], yaw_radians: f32) -> Option<CanonicalTransform> {
    Some(CanonicalTransform {
        position: canonical_position(position)?,
        rotation: canonical_yaw(yaw_radians)?,
    })
}

#[cfg(test)]
#[allow(clippy::float_cmp)]
mod tests {
    use super::*;

    #[test]
    fn coordinate_basis_round_trips() {
        let source = [3.0, -7.0, 2.5];
        let canonical = canonical_position(source).expect("finite");
        assert_eq!(canonical, [3.0, 2.5, 7.0]);
        assert_eq!(source_position(canonical), Some(source));
    }

    #[test]
    fn quarter_turns_are_deterministic() {
        assert_eq!(canonical_quarter_turn(0), [0.0, 0.0, 0.0, 1.0]);
        assert_eq!(canonical_quarter_turn(1), canonical_quarter_turn(5));
    }

    #[test]
    fn transform_is_applied_once() {
        let transform = canonical_transform([4.0, 7.0, 1.5], std::f32::consts::FRAC_PI_2)
            .expect("finite transform");
        assert_eq!(transform.position, [4.0, 1.5, -7.0]);
        assert!((transform.rotation[1] - std::f32::consts::FRAC_1_SQRT_2).abs() < 1e-6);
        assert_eq!(source_position(transform.position), Some([4.0, 7.0, 1.5]));
    }

    #[test]
    fn rejects_non_finite_values() {
        assert!(canonical_position([0.0, f32::NAN, 0.0]).is_none());
        assert!(canonical_yaw(f32::INFINITY).is_none());
        assert!(canonical_quaternion([0.0, 0.0, 0.0, f32::NAN]).is_none());
    }
}
