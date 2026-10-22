use super::GameView;
use forge_gameplay::{GameAction, GameSession, GameplayEvent};
use std::fmt::Write as _;

pub(super) fn append(session: &GameSession, view: &mut GameView) {
    let document = session.document();
    if let Ok(Some(presentation)) = session.cinematic_presentation() {
        view.actions.clear();
        view.dialogues.clear();
        let name = document
            .cinematic_definition(presentation.sequence)
            .map_or("Séquence", |sequence| sequence.name.as_str());
        let _ = write!(view.summary, "\nCinématique : {name}");
        if let Some(text) = presentation.text {
            let speaker = presentation
                .speaker
                .and_then(|id| document.actor(id))
                .map_or("", |actor| actor.name.as_str());
            let _ = write!(view.summary, "\n{speaker} : {text}");
        }
        view.actions
            .push(("Passer la cinématique".into(), GameAction::SkipCinematic));
        view.actions.push((
            "Interrompre la cinématique".into(),
            GameAction::InterruptCinematic,
        ));
        return;
    }
    let player = &session.state().actors[&document.player];
    if !player.active || player.vitality == 0 {
        return;
    }
    if let Some(scripts) = &document.scripting {
        for script in &scripts.scripts {
            view.actions.push((
                format!("Exécuter : {}", script.name),
                GameAction::RunScript { script: script.id },
            ));
        }
    }
    if let Some(cinematics) = &document.cinematics {
        for sequence in &cinematics.sequences {
            view.actions.push((
                format!("Jouer : {}", sequence.name),
                GameAction::StartCinematic {
                    sequence: sequence.id,
                },
            ));
        }
    }
    if let Some(receipt) = session.state().receipts.last() {
        for event in &receipt.events {
            if let GameplayEvent::ScriptMessage { text, .. } = event {
                let _ = write!(view.summary, "\n{text}");
            }
        }
    }
}
