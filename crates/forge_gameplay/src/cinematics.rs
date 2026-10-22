//! Neutral presentation contract. Engine camera handles stay in adapters.
use crate::GameplayError;
use forge_core::{CommandId, EntityId, Vec3};
use serde::{Deserialize, Serialize};

pub const MAX_CINEMATICS: usize = 16;
pub const MAX_CINEMATIC_CUES: usize = 32;
pub const MAX_CINEMATIC_DURATION_MS: u32 = 600_000;
pub const MAX_CINEMATIC_TEXT_BYTES: usize = 2048;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CinematicCamera {
    pub eye: Vec3,
    pub target: Vec3,
    pub vertical_fov_degrees: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CinematicCue {
    pub id: EntityId,
    pub duration_ms: u32,
    pub speaker: Option<EntityId>,
    pub text: Option<String>,
    pub camera: Option<CinematicCamera>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CinematicDefinition {
    pub id: EntityId,
    pub name: String,
    pub cues: Vec<CinematicCue>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CinematicsDefinition {
    pub version: u32,
    pub sequences: Vec<CinematicDefinition>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CinematicPlayback {
    pub sequence: EntityId,
    pub cause: CommandId,
    pub started_at_ns: u64,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum CinematicEndReason {
    Finished,
    Skipped,
    Interrupted,
    Failed,
}

/// A derived view: no cursor can drift independently from the common clock.
#[derive(Clone, Debug, PartialEq)]
pub struct CinematicPresentation {
    pub sequence: EntityId,
    pub cue: EntityId,
    pub cue_elapsed_ns: u64,
    pub speaker: Option<EntityId>,
    pub text: Option<String>,
    pub camera: Option<CinematicCamera>,
}

impl CinematicDefinition {
    pub(crate) fn presentation_at(
        &self,
        playback: &CinematicPlayback,
        now_ns: u64,
    ) -> Result<Option<CinematicPresentation>, GameplayError> {
        if playback.sequence != self.id {
            return Err(GameplayError::ActionRefused(
                "cinematic sequence differs from its playback".into(),
            ));
        }
        let mut elapsed = now_ns.checked_sub(playback.started_at_ns).ok_or_else(|| {
            GameplayError::ActionRefused("cinematic clock moved backwards".into())
        })?;
        for cue in &self.cues {
            let duration = u64::from(cue.duration_ms) * 1_000_000;
            if elapsed < duration {
                return Ok(Some(CinematicPresentation {
                    sequence: self.id,
                    cue: cue.id,
                    cue_elapsed_ns: elapsed,
                    speaker: cue.speaker,
                    text: cue.text.clone(),
                    camera: cue.camera.clone(),
                }));
            }
            elapsed -= duration;
        }
        Ok(None)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (CinematicDefinition, CinematicPlayback) {
        let sequence = EntityId::deterministic("cinematic-sequence");
        (
            CinematicDefinition {
                id: sequence,
                name: "Arrival".into(),
                cues: vec![
                    CinematicCue {
                        id: EntityId::deterministic("arrival"),
                        duration_ms: 100,
                        speaker: None,
                        text: Some("Arrival".into()),
                        camera: None,
                    },
                    CinematicCue {
                        id: EntityId::deterministic("departure"),
                        duration_ms: 200,
                        speaker: None,
                        text: Some("Departure".into()),
                        camera: None,
                    },
                ],
            },
            CinematicPlayback {
                sequence,
                cause: CommandId::deterministic("start"),
                started_at_ns: 10,
            },
        )
    }

    #[test]
    fn boundaries_and_completion_follow_integer_simulation_time() {
        let (sequence, playback) = fixture();
        let first = sequence.presentation_at(&playback, 10).unwrap().unwrap();
        assert_eq!(first.cue, sequence.cues[0].id);
        assert_eq!(first.cue_elapsed_ns, 0);
        assert_eq!(
            sequence
                .presentation_at(&playback, 100_000_009)
                .unwrap()
                .unwrap()
                .cue,
            first.cue
        );
        let second = sequence
            .presentation_at(&playback, 100_000_010)
            .unwrap()
            .unwrap();
        assert_eq!(second.cue, sequence.cues[1].id);
        assert_eq!(second.cue_elapsed_ns, 0);
        assert!(
            sequence
                .presentation_at(&playback, 300_000_010)
                .unwrap()
                .is_none()
        );
        assert!(sequence.presentation_at(&playback, 9).is_err());
    }

    #[test]
    fn encoded_playback_resumes_the_same_position_without_mutating_source() {
        let (sequence, playback) = fixture();
        let before = serde_json::to_vec(&sequence).unwrap();
        let saved = serde_json::to_vec(&playback).unwrap();
        let restored: CinematicPlayback = serde_json::from_slice(&saved).unwrap();
        assert_eq!(
            sequence.presentation_at(&playback, 150_000_010).unwrap(),
            sequence.presentation_at(&restored, 150_000_010).unwrap()
        );
        assert_eq!(serde_json::to_vec(&sequence).unwrap(), before);
    }
}
