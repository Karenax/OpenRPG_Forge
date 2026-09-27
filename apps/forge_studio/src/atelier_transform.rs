//! Shared numeric inspector draft. Preparing it never mutates a document.
use forge_core::{Transform, Vec3};
use forge_world::placement_transform::{Axis, RotationSpace, rotate, set_scale};
use serde::{Deserialize, Serialize};

/// Revalidate against the current main session after the cross-window await.
pub fn prepare_preview(
    session: Option<&forge_project::authoring::AuthoringState>,
    draft: &crate::atelier_drafts::TransformDraft,
) -> Result<forge_project::authoring::AuthoringRequest, String> {
    use forge_project::{authoring::AuthoringRequest, zone_authoring::placements::PlacementEdit};
    let session = session.ok_or("Ouvrir une zone")?;
    if session.playtest_hash.is_some() {
        return Err("Revenir à l’édition".into());
    }
    let head = session.zone_head.as_ref().ok_or("Aucune zone éditable")?;
    if head.base.terrain.area_id.to_string() != draft.scope.area
        || head.content_hash()?.to_string() != draft.scope.base_hash
    {
        return Err("La carte a changé. Ce brouillon est conservé mais ne peut plus être appliqué à cette révision.".into());
    }
    let placement = head
        .corrections
        .placements
        .as_ref()
        .and_then(|items| {
            items
                .iter()
                .find(|p| p.id.to_string() == draft.scope.object)
        })
        .ok_or("Objet absent de la carte")?;
    if placement.locked {
        return Err("Cet objet est verrouillé".into());
    }
    let edit = PlacementEdit::Transform {
        id: placement.id,
        transform: draft.fields.resolve(placement.transform)?,
    };
    let key = serde_json::to_string(&(head.content_hash()?, &edit)).map_err(|e| e.to_string())?;
    head.prepare_placement(forge_core::CommandId::deterministic(key), edit)
        .map(|request| AuthoringRequest::PreviewZone { request })
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TransformFields {
    pub position: [String; 3],
    /// Relative world-axis angles, applied X then Y then Z about the object's pivot.
    /// Keeping a zero delta preserves the original quaternion exactly.
    pub rotation_delta: [String; 3],
    pub scale: [String; 3],
}

impl TransformFields {
    #[must_use]
    pub fn from_transform(transform: Transform) -> Self {
        let strings = |v: Vec3| [v.x, v.y, v.z].map(|n| n.to_string());
        Self {
            position: strings(transform.translation_m),
            rotation_delta: std::array::from_fn(|_| "0".into()),
            scale: strings(transform.scale),
        }
    }

    /// Incomplete text such as '-' is a valid draft, but not an applicable transform.
    #[must_use]
    pub fn bounded(&self) -> bool {
        self.position
            .iter()
            .chain(&self.rotation_delta)
            .chain(&self.scale)
            .all(|value| value.len() <= 64)
    }

    pub fn resolve(&self, source: Transform) -> Result<Transform, String> {
        if !self.bounded() {
            return Err("Champ de transformation trop long".into());
        }
        let parse = |text: &str| -> Result<f32, String> {
            let number = text
                .trim()
                .parse::<f32>()
                .map_err(|_| "Nombre incomplet ou invalide".to_owned())?;
            if !number.is_finite() {
                return Err("Nombre non fini refusé".into());
            }
            Ok(number)
        };
        let vector = |values: &[String; 3]| -> Result<Vec3, String> {
            Ok(Vec3::new(
                parse(&values[0])?,
                parse(&values[1])?,
                parse(&values[2])?,
            ))
        };
        let mut target = source;
        target.translation_m = vector(&self.position)?;
        target = set_scale(target, vector(&self.scale)?)?;
        for (axis, angle) in [Axis::X, Axis::Y, Axis::Z]
            .into_iter()
            .zip(&self.rotation_delta)
        {
            target = rotate(target, axis, parse(angle)?, RotationSpace::World)?;
        }
        target.validate().map_err(|e| e.to_string())?;
        Ok(target)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::UnitQuaternion;

    #[test]
    fn unchanged_fields_preserve_orientation_and_nonuniform_scale() {
        let source = Transform {
            translation_m: Vec3::new(1.23, -4.5, 8.9),
            rotation: UnitQuaternion::new(0.0, 1.0, 0.0, 0.0),
            scale: Vec3::new(2.0, 3.0, 4.0),
        };
        assert_eq!(
            TransformFields::from_transform(source)
                .resolve(source)
                .unwrap(),
            source
        );
    }

    #[test]
    fn rotation_is_relative_to_source_not_previous_preview() {
        let mut draft = TransformFields::from_transform(Transform::IDENTITY);
        draft.rotation_delta[1] = "90".into();
        let first = draft.resolve(Transform::IDENTITY).unwrap();
        assert_eq!(first, draft.resolve(Transform::IDENTITY).unwrap());
        assert!((first.rotation.y - std::f32::consts::FRAC_1_SQRT_2).abs() < 0.00001);
    }

    #[test]
    fn incomplete_and_oversized_fields_cannot_be_applied() {
        for bad in ["-", "NaN", "inf", "1e999", ""] {
            let mut draft = TransformFields::from_transform(Transform::IDENTITY);
            draft.position[0] = bad.into();
            assert!(draft.bounded());
            assert!(draft.resolve(Transform::IDENTITY).is_err());
        }
        let mut draft = TransformFields::from_transform(Transform::IDENTITY);
        draft.position[0] = "1".repeat(65);
        assert!(!draft.bounded());
        assert!(draft.resolve(Transform::IDENTITY).is_err());
        draft = TransformFields::from_transform(Transform::IDENTITY);
        draft.scale[1] = "0".into();
        assert!(draft.resolve(Transform::IDENTITY).is_err());
    }

    #[test]
    fn preview_uses_standard_command_and_rejects_changed_or_locked_authority() {
        use crate::atelier_drafts::{DraftScope, TransformDraft};
        use forge_project::{
            authoring::{AuthoringState, new_terrain},
            zone_authoring::{
                GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
                placements::ManualPlacement,
            },
        };
        let terrain = new_terrain("atelier-test", "zone", "Zone").unwrap();
        let id = forge_core::EntityId::deterministic("placement");
        let head = ZoneAuthoringHead {
            schema: "forge.zone-authoring".into(),
            version: 2,
            revision: terrain.revision,
            base: ZoneBase {
                generator: GeneratorStamp {
                    name: "fixture".into(),
                    version: "1".into(),
                    inputs_hash: terrain.content_hash().unwrap(),
                },
                terrain,
                vegetation: None,
            },
            corrections: ZoneCorrections {
                placements: Some(vec![ManualPlacement {
                    id,
                    name: "Arche".into(),
                    asset_id: forge_core::AssetId::deterministic("arch"),
                    asset_hash: forge_core::ContentHash::from_bytes([3; 32]),
                    transform: Transform::IDENTITY,
                    locked: false,
                }]),
                ..ZoneCorrections::default()
            },
        };
        let mut draft = TransformDraft {
            scope: DraftScope {
                area: head.base.terrain.area_id.to_string(),
                object: id.to_string(),
                base_hash: head.content_hash().unwrap().to_string(),
            },
            fields: TransformFields::from_transform(Transform::IDENTITY),
        };
        draft.fields.position[0] = "3".into();
        let mut session:AuthoringState=serde_json::from_value(serde_json::json!({"zone_head":head,"document":null,"dirty":false,"receipt":null,"playtest_hash":null})).unwrap();
        assert!(matches!(
            prepare_preview(Some(&session), &draft).unwrap(),
            forge_project::authoring::AuthoringRequest::PreviewZone { .. }
        ));
        assert_eq!(
            session
                .zone_head
                .as_ref()
                .unwrap()
                .corrections
                .placements
                .as_ref()
                .unwrap()[0]
                .transform,
            Transform::IDENTITY
        );
        let mut stale = draft.clone();
        stale.scope.base_hash = "0".repeat(64);
        assert!(prepare_preview(Some(&session), &stale).is_err());
        assert!(prepare_preview(None, &draft).is_err());
        let head = session.zone_head.as_mut().unwrap();
        head.corrections.placements.as_mut().unwrap()[0].locked = true;
        draft.scope.base_hash = head.content_hash().unwrap().to_string();
        assert!(prepare_preview(Some(&session), &draft).is_err());
    }
}
