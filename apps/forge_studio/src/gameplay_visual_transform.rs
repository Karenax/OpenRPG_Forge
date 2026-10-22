//! Local model adjustments share the existing placement rotation contract.
use forge_core::{Transform, Vec3};
use forge_world::placement_transform::{Axis, RotationSpace, rotate};

#[derive(Clone, Debug, PartialEq)]
pub struct VisualTransformDraft {
    pub offsets: [String; 3],
    pub scales: [String; 3],
    pub axis: String,
    pub angle: String,
}

impl VisualTransformDraft {
    #[must_use]
    pub fn from_transform(transform: Transform) -> Self {
        Self {
            offsets: [
                transform.translation_m.x,
                transform.translation_m.y,
                transform.translation_m.z,
            ]
            .map(|value| value.to_string()),
            scales: [transform.scale.x, transform.scale.y, transform.scale.z]
                .map(|value| value.to_string()),
            axis: "y".into(),
            angle: "0".into(),
        }
    }

    /// Rotation is relative to the saved model, so repeated previews do not accumulate.
    /// Its existing quaternion is composed directly, including any pitch or roll.
    pub fn apply(&self, saved: Transform) -> Result<Transform, String> {
        saved.validate().map_err(|error| error.to_string())?;
        let number = |value: &str| {
            value
                .trim()
                .parse::<f32>()
                .ok()
                .filter(|number| number.is_finite())
                .ok_or_else(|| "Saisir une valeur numérique finie pour le modèle.".to_owned())
        };
        let vector = |values: &[String; 3]| {
            Ok::<_, String>(Vec3::new(
                number(&values[0])?,
                number(&values[1])?,
                number(&values[2])?,
            ))
        };
        let source = Transform {
            translation_m: vector(&self.offsets)?,
            scale: vector(&self.scales)?,
            rotation: saved.rotation,
        };
        let axis = match self.axis.as_str() {
            "x" => Axis::X,
            "y" => Axis::Y,
            "z" => Axis::Z,
            _ => return Err("Choisir un axe de rotation X, Y ou Z.".into()),
        };
        rotate(source, axis, number(&self.angle)?, RotationSpace::World)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::UnitQuaternion;

    #[test]
    fn unchanged_fields_preserve_arbitrary_rotation_and_nonuniform_scale_exactly() {
        let saved = Transform {
            translation_m: Vec3::new(0.125, -0.9, 1.5),
            rotation: UnitQuaternion::new(0.5, 0.5, 0.5, 0.5),
            scale: Vec3::new(0.5, 1.25, 2.0),
        };
        assert_eq!(
            VisualTransformDraft::from_transform(saved).apply(saved),
            Ok(saved)
        );
    }

    #[test]
    fn preview_rotation_keeps_offset_scale_and_does_not_accumulate() {
        let saved = Transform::from_translation(Vec3::new(0.0, -1.0, 0.0));
        let mut draft = VisualTransformDraft::from_transform(saved);
        draft.angle = "90".into();
        draft.scales = ["0.5", "0.5", "0.5"].map(str::to_owned);
        let first = draft.apply(saved).unwrap();
        assert_eq!(first, draft.apply(saved).unwrap());
        assert_eq!(first.translation_m, saved.translation_m);
        assert_eq!(first.scale, Vec3::new(0.5, 0.5, 0.5));
        assert!((first.rotation.y - std::f32::consts::FRAC_1_SQRT_2).abs() < 0.00001);
        assert!(first.rotation.is_finite_and_normalized());
        assert_eq!(
            saved,
            Transform::from_translation(Vec3::new(0.0, -1.0, 0.0))
        );
    }

    #[test]
    fn invalid_numbers_mirrors_and_unknown_axes_are_refused() {
        for invalid in ["", "NaN", "inf", "1e999", "un mètre"] {
            let mut draft = VisualTransformDraft::from_transform(Transform::IDENTITY);
            draft.offsets[1] = invalid.into();
            assert!(draft.apply(Transform::IDENTITY).is_err());
            draft.offsets[1] = "0".into();
            draft.angle = invalid.into();
            assert!(draft.apply(Transform::IDENTITY).is_err());
        }
        for invalid in ["0", "-1", "NaN"] {
            let mut draft = VisualTransformDraft::from_transform(Transform::IDENTITY);
            draft.scales[0] = invalid.into();
            assert!(draft.apply(Transform::IDENTITY).is_err());
        }
        let mut draft = VisualTransformDraft::from_transform(Transform::IDENTITY);
        draft.axis = "absent".into();
        assert!(draft.apply(Transform::IDENTITY).is_err());
    }
}
