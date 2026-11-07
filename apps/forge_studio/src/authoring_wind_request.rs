//! Wind drafts produce the same reviewed zone command as other retouches.
use forge_project::{
    authoring::AuthoringRequest,
    zone_authoring::{ZoneAuthoringHead, wind::SceneWindSettings},
};

pub(super) fn draft(heading: &str, speed: &str) -> Result<SceneWindSettings, String> {
    let settings = SceneWindSettings {
        schema_version: 1,
        heading_degrees: heading
            .trim()
            .parse()
            .map_err(|_| "Direction du vent invalide")?,
        speed_mps: speed
            .trim()
            .parse()
            .map_err(|_| "Vitesse du vent invalide")?,
    };
    settings.validate()?;
    Ok(settings)
}

pub(super) fn preview(
    head: &ZoneAuthoringHead,
    settings: Option<SceneWindSettings>,
) -> Result<AuthoringRequest, String> {
    let identity = forge_core::canonical_json_sha256(&(head.content_hash()?, &settings))
        .map_err(|error| error.to_string())?;
    Ok(AuthoringRequest::PreviewZone {
        request: head.prepare_wind(
            settings,
            forge_core::CommandId::deterministic(format!("scene-wind:{identity}")),
        )?,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{authoring::new_terrain, zone_authoring::session::ZoneEditSession};

    #[test]
    fn wind_draft_validates_and_preview_preserves_other_edits() {
        for (heading, speed) in [
            ("", "1"),
            ("360", "1"),
            ("NaN", "1"),
            ("0", "-1"),
            ("0", "26"),
        ] {
            assert!(draft(heading, speed).is_err());
        }
        let wind = draft("90", "7").unwrap();
        let mut session = ZoneEditSession::from_initial_terrain(
            new_terrain("wind-ui", "wind-ui-zone", "Zone").unwrap(),
        )
        .unwrap();
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some("Retouche existante".into());
        session
            .execute(&forge_project::zone_authoring::session::ZoneRequest {
                command_id: forge_core::CommandId::deterministic("wind-ui-name"),
                expected_hash: session.head().content_hash().unwrap(),
                expected_revision: session.head().revision,
                action: forge_project::zone_authoring::session::ZoneAction::edit(corrections),
            })
            .unwrap();
        let before = session.archive_bytes().unwrap();
        let AuthoringRequest::PreviewZone { request } =
            preview(session.head(), Some(wind)).unwrap()
        else {
            panic!("Expected reviewed zone request");
        };
        let receipt = session.preview(&request).unwrap();
        assert_eq!(receipt.applied.wind, Some(wind));
        assert_eq!(receipt.applied.name.as_deref(), Some("Retouche existante"));
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
}
