//! Source checks for bounded cinematic sequences.
use crate::cinematics::{
    CinematicsDefinition, MAX_CINEMATIC_CUES, MAX_CINEMATIC_DURATION_MS, MAX_CINEMATIC_TEXT_BYTES,
    MAX_CINEMATICS,
};
use crate::{GameplayDocument, GameplayError};
use forge_core::EntityId;
use std::collections::BTreeSet;

fn invalid(reason: &str) -> GameplayError {
    GameplayError::InvalidDocument(format!("cinematic: {reason}"))
}

impl CinematicsDefinition {
    pub(crate) fn validate(
        &self,
        document: &GameplayDocument,
        ids: &mut BTreeSet<EntityId>,
    ) -> Result<(), GameplayError> {
        if self.version != 1 || self.sequences.len() > MAX_CINEMATICS {
            return Err(invalid("unsupported version or sequence quota exceeded"));
        }
        for sequence in &self.sequences {
            if !ids.insert(sequence.id)
                || sequence.name.trim().is_empty()
                || sequence.name.len() > 256
                || sequence.cues.is_empty()
                || sequence.cues.len() > MAX_CINEMATIC_CUES
            {
                return Err(invalid("invalid sequence identity, name or cue count"));
            }
            let mut total_ms = 0_u64;
            for cue in &sequence.cues {
                if !ids.insert(cue.id)
                    || cue.duration_ms == 0
                    || cue.duration_ms > MAX_CINEMATIC_DURATION_MS
                    || cue
                        .speaker
                        .is_some_and(|speaker| document.actor(speaker).is_none())
                    || (cue.speaker.is_some() && cue.text.is_none())
                    || (cue.text.is_none() && cue.camera.is_none())
                    || cue.text.as_ref().is_some_and(|text| {
                        text.trim().is_empty() || text.len() > MAX_CINEMATIC_TEXT_BYTES
                    })
                {
                    return Err(invalid(
                        "invalid cue identity, duration, speaker or presentation",
                    ));
                }
                total_ms += u64::from(cue.duration_ms);
                if total_ms > u64::from(MAX_CINEMATIC_DURATION_MS) {
                    return Err(invalid("sequence exceeds its total duration limit"));
                }
                if let Some(camera) = &cue.camera {
                    crate::validation::position(camera.eye)?;
                    crate::validation::position(camera.target)?;
                    if !camera.vertical_fov_degrees.is_finite()
                        || !(15.0..=120.0).contains(&camera.vertical_fov_degrees)
                        || camera.eye == camera.target
                    {
                        return Err(invalid(
                            "camera requires distinct points and a field of view from 15 to 120 degrees",
                        ));
                    }
                }
            }
        }
        Ok(())
    }
}
