//! Physics drafts produce the same reviewed zone command as other retouches.
use forge_project::{
    authoring::AuthoringRequest,
    zone_authoring::{ZoneAuthoringHead, physics::PhysicsSceneSettings},
};

pub(super) fn draft(
    matrix: Option<[u32; 32]>,
    skin: &str,
    evidence: &str,
) -> Result<PhysicsSceneSettings, String> {
    let settings = PhysicsSceneSettings {
        schema_version: 1,
        layer_matrix: matrix.ok_or("Choisir explicitement les interactions entre couches")?,
        contact_skin_m: skin
            .trim()
            .parse()
            .map_err(|_| "Épaisseur de contact invalide")?,
        evidence: evidence.trim().to_owned(),
    };
    settings.validate()?;
    Ok(settings)
}

pub(super) fn set_pair(
    matrix: &mut [u32; 32],
    layer: usize,
    other: usize,
    enabled: bool,
) -> Result<(), String> {
    if layer >= 32 || other >= 32 {
        return Err("Couche hors de l'intervalle 0–31".into());
    }
    if enabled {
        matrix[layer] |= 1u32 << other;
        matrix[other] |= 1u32 << layer;
    } else {
        matrix[layer] &= !(1u32 << other);
        matrix[other] &= !(1u32 << layer);
    }
    Ok(())
}

pub(super) fn preview(
    head: &ZoneAuthoringHead,
    settings: Option<PhysicsSceneSettings>,
) -> Result<AuthoringRequest, String> {
    let identity = forge_core::canonical_json_sha256(&(head.content_hash()?, &settings))
        .map_err(|e| e.to_string())?;
    Ok(AuthoringRequest::PreviewZone {
        request: head.prepare_physics(
            settings,
            forge_core::CommandId::deterministic(format!("physics:{identity}")),
        )?,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{authoring::new_terrain, zone_authoring::session::ZoneEditSession};

    #[test]
    fn draft_requires_explicit_valid_values() {
        assert!(draft(None, "0", "target policy").is_err());
        for skin in ["", "oops", "NaN", "inf", "-0.01"] {
            assert!(draft(Some([0; 32]), skin, "target policy").is_err());
        }
        assert!(draft(Some([0; 32]), "0", " ").is_err());
        assert!(draft(Some([0; 32]), "0", "bad\nreference").is_err());
        let valid = draft(Some([0; 32]), " 0.025 ", " target policy ").unwrap();
        assert_eq!(valid.contact_skin_m.to_bits(), 0.025f32.to_bits());
        assert_eq!(valid.evidence, "target policy");
    }

    #[test]
    fn pair_edit_changes_both_directions_and_bounds_fail_without_mutation() {
        let mut matrix = [0; 32];
        set_pair(&mut matrix, 0, 31, true).unwrap();
        assert_eq!(matrix[0], 1u32 << 31);
        assert_eq!(matrix[31], 1);
        draft(Some(matrix), "0", "target policy").unwrap();
        let before = matrix;
        assert!(set_pair(&mut matrix, 32, 0, true).is_err());
        assert_eq!(matrix, before);
        set_pair(&mut matrix, 31, 0, false).unwrap();
        assert_eq!(matrix, [0; 32]);
    }

    #[test]
    fn preview_is_pinned_and_does_not_apply_or_drop_other_retouches() {
        let mut session = ZoneEditSession::from_initial_terrain(
            new_terrain("physics-ui", "zone", "Zone").unwrap(),
        )
        .unwrap();
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some("Retouche existante".into());
        let rename = forge_project::zone_authoring::session::ZoneRequest {
            command_id: forge_core::CommandId::deterministic("physics-ui-rename"),
            expected_hash: session.head().content_hash().unwrap(),
            expected_revision: session.head().revision,
            action: forge_project::zone_authoring::session::ZoneAction::edit(corrections),
        };
        session.execute(&rename).unwrap();
        let before = session.archive_bytes().unwrap();
        let settings = draft(Some([u32::MAX; 32]), "0", "target policy").unwrap();
        let AuthoringRequest::PreviewZone { request } =
            preview(session.head(), Some(settings.clone())).unwrap()
        else {
            panic!("Expected review command");
        };
        assert_eq!(
            request.expected_hash,
            session.head().content_hash().unwrap()
        );
        assert_eq!(request.expected_revision, session.head().revision);
        let receipt = session.preview(&request).unwrap();
        assert_eq!(session.archive_bytes().unwrap(), before);
        assert_eq!(receipt.applied.physics.as_deref(), Some(&settings));
        assert_eq!(receipt.applied.name.as_deref(), Some("Retouche existante"));
        session.execute(&request).unwrap();
        let AuthoringRequest::PreviewZone { request: remove } =
            preview(session.head(), None).unwrap()
        else {
            panic!("Expected review command");
        };
        assert!(session.preview(&remove).unwrap().applied.physics.is_none());
        assert_eq!(
            session.head().corrections.physics.as_deref(),
            Some(&settings)
        );
    }
}
