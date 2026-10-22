//! Bounded verification of the draft on a fresh party, never a live-game command.
use super::{Fields, document};
use forge_core::{CommandId, canonical_json_sha256};
use forge_gameplay::{ActionObservation, GameAction, GameRequest, GameSession};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

fn verify(fields: Fields, state: RwSignal<Option<AuthoringState>>) -> Result<(), String> {
    let source = document(state).ok_or("Ouvrir une carte avant de vérifier le script.")?;
    let draft = fields.draft.get_untracked();
    let source = source
        .with_script_definition(draft.clone())
        .map_err(|error| fields.record_error(&error))?;
    let hash = canonical_json_sha256(&source).map_err(|error| error.to_string())?;
    let mut game = GameSession::new(source, hash).map_err(|error| fields.record_error(&error))?;
    let request = GameRequest {
        command_id: CommandId::deterministic(format!("script-draft-check/{hash}/{}", draft.id)),
        expected_sequence: 0,
        actor: game.document().player,
        action: GameAction::RunScript { script: draft.id },
    };
    game.execute(
        &request,
        ActionObservation {
            line_of_sight: true,
            ..ActionObservation::default()
        },
    )
    .map_err(|error| fields.record_error(&error))?;
    Ok(())
}

#[component]
pub(super) fn DiagnosticControls(
    fields: Fields,
    state: RwSignal<Option<AuthoringState>>,
) -> impl IntoView {
    Effect::new(move |previous: Option<Option<forge_core::ContentHash>>| {
        let current = canonical_json_sha256(&fields.draft.get()).ok();
        if previous.as_ref() != Some(&current) {
            fields.diagnostic.set(None);
            fields.verification.set(None);
            fields.error.set(None);
        }
        current
    });
    view! {
        <button on:click=move |_| {
            fields.diagnostic.set(None);
            fields.verification.set(None);
            match verify(fields,state) {
                Ok(()) => {
                    fields.error.set(None);
                    fields.verification.set(Some("Vérification réussie sur une nouvelle partie temporaire. Le brouillon et la partie jouée restent inchangés.".into()));
                }
                Err(error) => fields.error.set(Some(error)),
            }
        }>"Vérifier le script sur une nouvelle partie"</button>
        <p>"Cette vérification utilise l’état initial et les règles métier, sans physique ni sauvegarde de partie. Jouer le module reste nécessaire pour vérifier les cibles visibles et les conditions réelles."</p>
        <p role="status">{move ||fields.verification.get()}</p>
        {move ||fields.diagnostic.get().map(|diagnostic| {
            let instruction=diagnostic.instruction;
            let current=diagnostic.script==fields.draft.get().id;
            view! {
                <aside role="alert">
                    <p>{format!("{} {}",diagnostic.cause,diagnostic.remedy)}</p>
                    <Show when=move ||current && instruction.is_some()>
                        <a href=move ||format!("#script-instruction-{}",fields.draft.get().id)
                            on:click=move |_| {
                                if let Some(instruction)=instruction {
                                    fields.instruction.set(instruction.to_string());
                                }
                            }>"Retoucher l’instruction signalée"</a>
                    </Show>
                </aside>
            }
        })}
    }
}
