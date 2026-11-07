//! Authored scene wind shared by all effects, independent of vegetation.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use forge_core::CommandId;
use serde::{Deserialize, Serialize};

/// Maximum supported by the common source deformation and its conservative bounds.
pub const MAX_SCENE_WIND_SPEED_MPS: f32 = 25.0;

/// Direction is where the wind travels: 0° is north (-Z), 90° is east (+X),
/// matching the Studio compass. Speed is horizontal metres per second.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SceneWindSettings {
    pub schema_version: u32,
    pub heading_degrees: f32,
    pub speed_mps: f32,
}

impl SceneWindSettings {
    pub fn validate(self) -> Result<(), String> {
        if self.schema_version != 1
            || !self.heading_degrees.is_finite()
            || !(0.0..360.0).contains(&self.heading_degrees)
            || !self.speed_mps.is_finite()
            || !(0.0..=MAX_SCENE_WIND_SPEED_MPS).contains(&self.speed_mps)
        {
            return Err("Direction ou vitesse du vent hors limites".into());
        }
        Ok(())
    }

    /// Convert a validated compass heading to the engine's horizontal X/Z basis.
    pub fn direction_xz(self) -> Result<[f64; 2], String> {
        self.validate()?;
        let (east, north) = f64::from(self.heading_degrees).to_radians().sin_cos();
        Ok([east, -north])
    }
}

impl ZoneAuthoringHead {
    /// Prepare the same reviewed, reversible zone edit as other authored settings.
    pub fn prepare_wind(
        &self,
        settings: Option<SceneWindSettings>,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        if let Some(settings) = settings {
            settings.validate()?;
        }
        let mut corrections = self.corrections.clone();
        corrections.wind = settings;
        Ok(ZoneRequest {
            command_id,
            expected_hash: self.content_hash()?,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{authoring::new_terrain, zone_authoring::session::ZoneEditSession};

    fn settings(heading_degrees: f32, speed_mps: f32) -> SceneWindSettings {
        SceneWindSettings {
            schema_version: 1,
            heading_degrees,
            speed_mps,
        }
    }

    #[test]
    fn compass_directions_and_invalid_values_are_explicit() {
        for (heading, expected) in [
            (0.0, [0.0, -1.0]),
            (90.0, [1.0, 0.0]),
            (180.0, [0.0, 1.0]),
            (270.0, [-1.0, 0.0]),
        ] {
            let actual = settings(heading, 7.0).direction_xz().unwrap();
            assert!((actual[0] - expected[0]).abs() < 1e-12);
            assert!((actual[1] - expected[1]).abs() < 1e-12);
        }
        for invalid in [
            settings(f32::NAN, 1.0),
            settings(360.0, 1.0),
            settings(-1.0, 1.0),
            settings(0.0, -0.1),
            settings(0.0, 25.1),
        ] {
            assert!(invalid.validate().is_err());
        }
        assert!(settings(0.0, 0.0).validate().is_ok());
    }

    #[test]
    fn edit_undo_redo_and_all_archive_encodings_preserve_wind_and_history() {
        let mut session = ZoneEditSession::from_initial_terrain(
            new_terrain("wind-project", "wind-zone", "Wind zone").unwrap(),
        )
        .unwrap();
        let old_head = session.head().clone();
        let old_archive = session.archive_bytes().unwrap();
        let request = old_head
            .prepare_wind(
                Some(settings(90.0, 7.0)),
                CommandId::deterministic("wind-edit"),
            )
            .unwrap();
        assert_eq!(
            session.preview(&request).unwrap().applied.wind,
            Some(settings(90.0, 7.0))
        );
        assert_eq!(session.archive_bytes().unwrap(), old_archive);
        session.execute(&request).unwrap();
        assert_eq!(session.head().version, 19);
        for (bytes, version) in [
            (session.archive_bytes().unwrap(), 52),
            (session.compact_archive_bytes().unwrap(), 53),
            (session.delta_archive_bytes().unwrap(), 54),
        ] {
            let json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
            assert_eq!(json["version"], version);
            let restored = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
            assert_eq!(restored.head(), session.head());
            assert!(restored.has_wind_history());
        }
        let undo = ZoneRequest {
            command_id: CommandId::deterministic("wind-undo"),
            expected_hash: session.head().content_hash().unwrap(),
            expected_revision: session.head().revision,
            action: ZoneAction::Undo,
        };
        session.execute(&undo).unwrap();
        assert!(session.head().corrections.wind.is_none());
        assert!(session.has_wind_history());
        let redone = ZoneRequest {
            command_id: CommandId::deterministic("wind-redo"),
            expected_hash: session.head().content_hash().unwrap(),
            expected_revision: session.head().revision,
            action: ZoneAction::Redo,
        };
        session.execute(&redone).unwrap();
        assert_eq!(session.head().corrections.wind, Some(settings(90.0, 7.0)));
    }
}
