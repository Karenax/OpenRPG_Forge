//! Playback replay from the common clock and retained start/stop actions.
use crate::cinematics::{CinematicEndReason, CinematicPlayback};
use crate::{GameAction, GameSession, GameplayError, GameplayEvent};

fn invalid(reason: &str) -> GameplayError {
    GameplayError::IncompatibleSave(format!("cinematic ledger: {reason}"))
}

fn is_cinematic(event: &GameplayEvent) -> bool {
    matches!(
        event,
        GameplayEvent::CinematicStarted { .. } | GameplayEvent::CinematicEnded { .. }
    )
}

impl GameSession {
    pub(crate) fn presentation_event_valid(&self, event: &GameplayEvent) -> bool {
        match event {
            GameplayEvent::ScriptMessage { script, text } => {
                self.document.script_definition(*script).is_some()
                    && !text.trim().is_empty()
                    && text.len() <= crate::MAX_SCRIPT_MESSAGE_BYTES
            }
            GameplayEvent::ScriptExecuted { script, steps } => {
                self.document.script_definition(*script).is_some()
                    && (1..=crate::MAX_SCRIPT_STEPS).contains(steps)
            }
            GameplayEvent::CinematicStarted { playback } => self
                .document
                .cinematic_definition(playback.sequence)
                .is_some(),
            GameplayEvent::CinematicEnded { sequence, .. } => {
                self.document.cinematic_definition(*sequence).is_some()
            }
            _ => false,
        }
    }

    pub(crate) fn validate_cinematic_state(&self) -> Result<(), GameplayError> {
        if self
            .state
            .contacts
            .values()
            .flat_map(|contact| &contact.events)
            .any(is_cinematic)
        {
            return Err(invalid("presentation event in a collision receipt"));
        }
        let mut expected = None;
        for receipt in &self.state.receipts {
            let time = receipt.simulation_time_ns.unwrap_or(0);
            self.expire_cinematic_projection(&mut expected, time)?;
            let events = match receipt.request.action {
                GameAction::StartCinematic { sequence } => {
                    if receipt.request.actor != self.document.player
                        || expected.is_some()
                        || receipt.simulation_time_ns.is_none()
                        || self.document.cinematic_definition(sequence).is_none()
                    {
                        return Err(invalid("invalid retained cinematic start"));
                    }
                    let playback = CinematicPlayback {
                        sequence,
                        cause: receipt.request.command_id,
                        started_at_ns: time,
                    };
                    expected = Some(playback.clone());
                    vec![GameplayEvent::CinematicStarted { playback }]
                }
                GameAction::SkipCinematic | GameAction::InterruptCinematic => {
                    if receipt.request.actor != self.document.player {
                        return Err(invalid("foreign actor stopped the player's presentation"));
                    }
                    let playback = expected
                        .take()
                        .ok_or_else(|| invalid("stop without a current sequence"))?;
                    let reason = if matches!(receipt.request.action, GameAction::SkipCinematic) {
                        CinematicEndReason::Skipped
                    } else {
                        CinematicEndReason::Interrupted
                    };
                    vec![GameplayEvent::CinematicEnded {
                        sequence: playback.sequence,
                        cause: playback.cause,
                        reason,
                        elapsed_ns: time,
                    }]
                }
                _ => {
                    if expected.is_some() && receipt.request.actor == self.document.player {
                        return Err(invalid(
                            "player gameplay action while a sequence owns control",
                        ));
                    }
                    vec![]
                }
            };
            let actual: Vec<_> = receipt
                .events
                .iter()
                .filter(|event| is_cinematic(event))
                .cloned()
                .collect();
            if actual != events {
                return Err(invalid(
                    "cinematic outputs differ from their retained action",
                ));
            }
        }
        let now = self
            .state
            .simulation
            .as_ref()
            .map_or(0, |state| state.elapsed_ns);
        self.expire_cinematic_projection(&mut expected, now)?;
        // The first profile has no player resurrection. Physics interruption on
        // defeat therefore remains observable even after its frame is historical.
        if self
            .state
            .actors
            .get(&self.document.player)
            .is_some_and(|actor| actor.vitality == 0)
        {
            expected = None;
        }
        if expected != self.state.cinematic {
            return Err(invalid(
                "saved playback differs from start/stop actions and clock",
            ));
        }
        self.validate_last_cinematic_frame()
    }

    fn expire_cinematic_projection(
        &self,
        playback: &mut Option<CinematicPlayback>,
        now: u64,
    ) -> Result<(), GameplayError> {
        if let Some(current) = playback {
            let definition = self
                .document
                .cinematic_definition(current.sequence)
                .ok_or_else(|| invalid("playback source is absent"))?;
            if definition
                .presentation_at(current, now)
                .map_err(|_| invalid("invalid playback time"))?
                .is_none()
            {
                *playback = None;
            }
        }
        Ok(())
    }

    fn validate_last_cinematic_frame(&self) -> Result<(), GameplayError> {
        let Some(receipt) = &self.state.last_frame else {
            return Ok(());
        };
        let events: Vec<_> = receipt
            .events
            .iter()
            .filter(|event| is_cinematic(event))
            .collect();
        if self.document.cinematics.is_some() {
            let (before, now) = self.cinematic_frame_interval(receipt)?;
            let previous = self.cinematic_before_frame(receipt.frame.tick, before)?;
            if let Some(previous) = previous {
                let definition = self
                    .document
                    .cinematic_definition(previous.sequence)
                    .ok_or_else(|| invalid("playback source is absent"))?;
                let finished = definition
                    .presentation_at(&previous, now)
                    .map_err(|_| invalid("invalid presentation interval"))?
                    .is_none();
                let alive = self
                    .state
                    .actors
                    .get(&self.document.player)
                    .is_some_and(|actor| actor.vitality > 0);
                if finished && alive && events.is_empty() {
                    return Err(invalid(
                        "missing termination at the observed duration boundary",
                    ));
                }
                if events.iter().any(|event|!matches!(event,GameplayEvent::CinematicEnded {cause,..} if *cause==previous.cause)) {
                    return Err(invalid("termination differs from playback before the frame"));
                }
            } else if !events.is_empty() {
                return Err(invalid("termination without playback before its frame"));
            }
        }
        if events.len() > 1 {
            return Err(invalid("multiple cinematic terminations in one frame"));
        }
        for event in events {
            let (before, now) = self.cinematic_frame_interval(receipt)?;
            self.validate_frame_termination(event, receipt, before, now)?;
        }
        Ok(())
    }

    fn cinematic_frame_interval(
        &self,
        receipt: &crate::PhysicsFrameReceipt,
    ) -> Result<(u64, u64), GameplayError> {
        let now = self
            .state
            .simulation
            .as_ref()
            .ok_or_else(|| invalid("presentation has no clock"))?
            .elapsed_ns;
        let before = now
            .checked_sub(
                receipt
                    .frame
                    .simulation_step_ns
                    .ok_or_else(|| invalid("presentation frame has no time observation"))?,
            )
            .ok_or_else(|| invalid("presentation interval is invalid"))?;
        Ok((before, now))
    }

    fn validate_frame_termination(
        &self,
        event: &GameplayEvent,
        receipt: &crate::PhysicsFrameReceipt,
        before: u64,
        now: u64,
    ) -> Result<(), GameplayError> {
        let GameplayEvent::CinematicEnded {
            sequence,
            cause,
            reason,
            elapsed_ns,
        } = event
        else {
            return Err(invalid("cinematic started outside an action"));
        };
        if *elapsed_ns != now {
            return Err(invalid("termination time differs from its frame"));
        }
        let start = self
            .state
            .receipts
            .iter()
            .find(|start| start.request.command_id == *cause)
            .ok_or_else(|| invalid("termination has no retained start"))?;
        if !matches!(start.request.action,GameAction::StartCinematic {sequence: started} if started==*sequence)
            || start.tick >= receipt.frame.tick
        {
            return Err(invalid("termination refers to another sequence or tick"));
        }
        let playback = CinematicPlayback {
            sequence: *sequence,
            cause: *cause,
            started_at_ns: start
                .simulation_time_ns
                .ok_or_else(|| invalid("start time is absent"))?,
        };
        let definition = self
            .document
            .cinematic_definition(*sequence)
            .ok_or_else(|| invalid("terminated source is absent"))?;
        if definition
            .presentation_at(&playback, before)
            .map_err(|_| invalid("start lies after its termination interval"))?
            .is_none()
        {
            return Err(invalid("sequence already finished before this frame"));
        }
        match reason {
            CinematicEndReason::Finished => {
                if definition
                    .presentation_at(&playback, now)
                    .map_err(|_| invalid("invalid termination clock"))?
                    .is_some()
                {
                    return Err(invalid("sequence finished before its authored duration"));
                }
            }
            CinematicEndReason::Interrupted => {
                if self
                    .state
                    .actors
                    .get(&self.document.player)
                    .is_none_or(|actor| actor.vitality != 0)
                {
                    return Err(invalid("physics interruption has no defeated player"));
                }
            }
            CinematicEndReason::Skipped | CinematicEndReason::Failed => {
                return Err(invalid(
                    "termination reason lacks its action or valid source",
                ));
            }
        }
        Ok(())
    }

    fn cinematic_before_frame(
        &self,
        tick: u64,
        before: u64,
    ) -> Result<Option<CinematicPlayback>, GameplayError> {
        let mut playback = None;
        for receipt in self
            .state
            .receipts
            .iter()
            .filter(|receipt| receipt.tick < tick)
        {
            let time = receipt.simulation_time_ns.unwrap_or(0);
            self.expire_cinematic_projection(&mut playback, time)?;
            match receipt.request.action {
                GameAction::StartCinematic { sequence } => {
                    playback = Some(CinematicPlayback {
                        sequence,
                        cause: receipt.request.command_id,
                        started_at_ns: time,
                    });
                }
                GameAction::SkipCinematic | GameAction::InterruptCinematic => playback = None,
                _ => (),
            }
        }
        self.expire_cinematic_projection(&mut playback, before)?;
        Ok(playback)
    }
}
