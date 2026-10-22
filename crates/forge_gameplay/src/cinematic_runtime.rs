//! Playback mutations. Control and camera state are derived views.
use crate::cinematics::{CinematicEndReason, CinematicPlayback, CinematicPresentation};
use crate::{GameRequest, GameSession, GameState, GameplayError, GameplayEvent};
use forge_core::EntityId;

fn refused(reason: &str) -> GameplayError {
    GameplayError::ActionRefused(format!("cinematic: {reason}"))
}

impl GameSession {
    pub fn cinematic_presentation(&self) -> Result<Option<CinematicPresentation>, GameplayError> {
        let Some(playback) = &self.state.cinematic else {
            return Ok(None);
        };
        let definition = self
            .document
            .cinematic_definition(playback.sequence)
            .ok_or_else(|| refused("sequence definition is absent"))?;
        let now = self
            .state
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation clock is absent"))?
            .elapsed_ns;
        definition.presentation_at(playback, now)
    }

    #[must_use]
    pub fn cinematic_controls_player(&self) -> bool {
        self.state.cinematic.is_some()
    }

    pub(crate) fn apply_start_cinematic(
        &self,
        state: &mut GameState,
        request: &GameRequest,
        sequence: EntityId,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        if request.actor != self.document.player {
            return Err(refused("only the player starts a presentation"));
        }
        if state.cinematic.is_some() {
            return Err(refused("another sequence is already playing"));
        }
        let definition = self
            .document
            .cinematic_definition(sequence)
            .ok_or_else(|| refused("sequence definition is absent"))?;
        let now = state
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation clock is absent"))?
            .elapsed_ns;
        let playback = CinematicPlayback {
            sequence,
            cause: request.command_id,
            started_at_ns: now,
        };
        if definition.presentation_at(&playback, now)?.is_none() {
            return Err(refused("sequence has no presentation"));
        }
        state.cinematic = Some(playback.clone());
        Ok(vec![GameplayEvent::CinematicStarted { playback }])
    }

    pub(crate) fn apply_stop_cinematic(
        &self,
        state: &mut GameState,
        actor: EntityId,
        reason: CinematicEndReason,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        if actor != self.document.player {
            return Err(refused("only the player stops a presentation"));
        }
        let ended =
            end_cinematic(state, reason)?.ok_or_else(|| refused("no sequence is playing"))?;
        Ok(vec![ended])
    }

    pub(crate) fn advance_cinematic(
        &self,
        state: &mut GameState,
    ) -> Result<Vec<GameplayEvent>, GameplayError> {
        let Some(playback) = &state.cinematic else {
            return Ok(vec![]);
        };
        let now = state
            .simulation
            .as_ref()
            .ok_or_else(|| refused("simulation clock is absent"))?
            .elapsed_ns;
        let alive = state
            .actors
            .get(&self.document.player)
            .is_some_and(|actor| actor.active && actor.vitality > 0);
        let reason = if !alive {
            Some(CinematicEndReason::Interrupted)
        } else if let Some(definition) = self.document.cinematic_definition(playback.sequence) {
            if definition.presentation_at(playback, now)?.is_none() {
                Some(CinematicEndReason::Finished)
            } else {
                None
            }
        } else {
            Some(CinematicEndReason::Failed)
        };
        if let Some(reason) = reason {
            return Ok(end_cinematic(state, reason)?.into_iter().collect());
        }
        Ok(vec![])
    }
}

fn end_cinematic(
    state: &mut GameState,
    reason: CinematicEndReason,
) -> Result<Option<GameplayEvent>, GameplayError> {
    let now = state
        .simulation
        .as_ref()
        .ok_or_else(|| refused("simulation clock is absent"))?
        .elapsed_ns;
    let Some(playback) = state.cinematic.take() else {
        return Ok(None);
    };
    Ok(Some(GameplayEvent::CinematicEnded {
        sequence: playback.sequence,
        cause: playback.cause,
        reason,
        elapsed_ns: now,
    }))
}
