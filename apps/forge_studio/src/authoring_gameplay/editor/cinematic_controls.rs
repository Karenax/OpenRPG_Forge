//! Sequence drafts promoted through the common source command boundary.
use super::{Edit, edit, id, source_hash};
use forge_core::{EntityId, Vec3, canonical_json_sha256};
use forge_gameplay::{
    CinematicCamera, CinematicCue, CinematicDefinition, CreatorSurface, GameplayDocument,
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

fn document(state: RwSignal<Option<AuthoringState>>) -> Option<GameplayDocument> {
    state
        .get()?
        .zone_head?
        .corrections
        .gameplay
        .map(|source| *source)
}
fn count(value: &str) -> Result<u32, String> {
    value
        .trim()
        .parse()
        .map_err(|_| "Saisir une durée entière en millisecondes.".into())
}
fn vector(fields: [RwSignal<String>; 3]) -> Result<Vec3, String> {
    let values = fields.map(|field| {
        field
            .get_untracked()
            .trim()
            .parse::<f32>()
            .map_err(|_| "Saisir une coordonnée en mètres.".to_owned())
    });
    let [x, y, z] = values;
    Ok(Vec3::new(x?, y?, z?))
}

#[derive(Clone, Copy)]
struct Fields {
    selected: RwSignal<String>,
    name: RwSignal<String>,
    cues: RwSignal<Vec<CinematicCue>>,
    error: RwSignal<Option<String>>,
}

impl Fields {
    fn preview(self, state: Option<AuthoringState>, create: bool) -> Edit {
        edit(state, move |_, source| {
            let sequence = if create {
                let hash = canonical_json_sha256(source).map_err(|error| error.to_string())?;
                EntityId::deterministic(format!("cinematic/{}/{hash}", source.area_id))
            } else {
                id(&self.selected.get_untracked())?
            };
            if !create && source.cinematic_definition(sequence).is_none() {
                return Err("Choisir une séquence courante.".into());
            }
            *source = source
                .with_cinematic_definition(CinematicDefinition {
                    id: sequence,
                    name: self.name.get_untracked(),
                    cues: self.cues.get_untracked(),
                })
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
    fn remove(self, state: Option<AuthoringState>) -> Edit {
        edit(state, move |_, source| {
            let sequence = id(&self.selected.get_untracked())?;
            let mut definitions = source
                .cinematics
                .clone()
                .ok_or("Aucune séquence à retoucher.")?;
            if !definitions
                .sequences
                .iter()
                .any(|entry| entry.id == sequence)
            {
                return Err("Choisir une séquence courante.".into());
            }
            definitions.sequences.retain(|entry| entry.id != sequence);
            *source = source
                .with_cinematics_definition(Some(definitions))
                .map_err(|error| error.to_string())?;
            Ok(())
        })
    }
}

#[component]
pub(super) fn CinematicControls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    send: Callback<Edit>,
) -> impl IntoView {
    let fields = Fields {
        selected: RwSignal::new(String::new()),
        name: RwSignal::new("Nouvelle séquence".into()),
        cues: RwSignal::new(vec![]),
        error: RwSignal::new(None),
    };
    Effect::new(move |previous: Option<super::FieldSource>| {
        let key = (fields.selected.get(), source_hash(state));
        if previous.as_ref() != Some(&key) {
            let current = document(state).and_then(|source| {
                id(&key.0)
                    .ok()
                    .and_then(|id| source.cinematic_definition(id).cloned())
            });
            if let Some(current) = current {
                fields.name.set(current.name);
                fields.cues.set(current.cues);
            } else {
                fields.name.set("Nouvelle séquence".into());
                fields.cues.set(vec![]);
            }
            fields.error.set(None);
        }
        key
    });
    let disabled = move || {
        busy.get()
            || state
                .get()
                .is_none_or(|state| state.zone_head.is_none() || state.playtest_hash.is_some())
    };
    view! {
        <Show when=move ||document(state).is_some_and(|source|source.surface_visible(CreatorSurface::Cinematics))>
            <details><summary>"Séquences cinématiques"</summary><fieldset disabled=disabled>
                <p>"Composer des répliques et plans de caméra. La lecture utilise l’horloge de simulation ; le joueur peut toujours la sauter. Les séquences de ce profil ne distribuent aucune récompense et ne changent pas les quêtes."</p>
                <label>"Séquence à retoucher"<select prop:value=move ||fields.selected.get() on:change=move |event|fields.selected.set(event_target_value(&event))>
                    <option value="">"Préparer une nouvelle séquence"</option>
                    {move ||document(state).and_then(|source|source.cinematics).map(|definitions|definitions.sequences.into_iter().map(|sequence|view!{<option value=sequence.id.to_string()>{sequence.name}</option>}).collect_view())}
                </select></label>
                <label>"Nom de la séquence"<input maxlength="256" prop:value=move ||fields.name.get() on:input=move |event|fields.name.set(event_target_value(&event))/></label>
                <p>{move ||format!("{} segments ; durée totale : {} ms",fields.cues.get().len(),fields.cues.get().iter().map(|cue|u64::from(cue.duration_ms)).sum::<u64>())}</p>
                <CueList fields=fields/>
                <CueControls fields=fields state=state/>
                <p role="alert">{move ||fields.error.get()}</p>
                <button disabled=move ||!fields.selected.get().is_empty() on:click=move |_|send.run(fields.preview(state.get_untracked(),true))>"Examiner une nouvelle séquence"</button>
                <button disabled=move ||fields.selected.get().is_empty() on:click=move |_|send.run(fields.preview(state.get_untracked(),false))>"Examiner la retouche de la séquence"</button>
                <button disabled=move ||fields.selected.get().is_empty() on:click=move |_|send.run(fields.remove(state.get_untracked()))>"Examiner la suppression de la séquence"</button>
                <p>"Les segments ci-dessus restent un brouillon avant l’aperçu et l’application. Modifier l’ordre, les durées ou la caméra peut rendre une partie sauvegardée incompatible."</p>
            </fieldset></details>
        </Show>
    }
}

#[component]
fn CueList(fields: Fields) -> impl IntoView {
    view! {{move ||fields.cues.get().into_iter().enumerate().map(|(index,cue)|{
        let caption=cue.text.as_ref().map_or_else(||"Plan de caméra".into(),|text|text.chars().take(70).collect::<String>());
        view!{<p>{format!("{}. {} — {} ms",index+1,caption,cue.duration_ms)}
            <button disabled={index==0} on:click=move |_|fields.cues.update(|cues|{if index>0&&index<cues.len(){cues.swap(index,index-1);}})>"Monter"</button>
            <button on:click=move |_|fields.cues.update(|cues|{if index+1<cues.len(){cues.swap(index,index+1);}})>"Descendre"</button>
            <button on:click=move |_|fields.cues.update(|cues|{if index<cues.len(){cues.remove(index);}})>"Retirer du brouillon"</button>
        </p>}
    }).collect_view()}}
}

#[derive(Clone, Copy)]
struct CueFields {
    selected: RwSignal<String>,
    duration: RwSignal<String>,
    speaker: RwSignal<String>,
    text: RwSignal<String>,
    camera: RwSignal<bool>,
    eye: [RwSignal<String>; 3],
    target: [RwSignal<String>; 3],
    fov: RwSignal<String>,
}

impl CueFields {
    fn load(self, current: Option<CinematicCue>) {
        let cue = current.unwrap_or(CinematicCue {
            id: EntityId::deterministic("uncommitted-cue"),
            duration_ms: 2000,
            speaker: None,
            text: Some("Nouvelle réplique".into()),
            camera: None,
        });
        self.duration.set(cue.duration_ms.to_string());
        self.speaker.set(
            cue.speaker
                .map_or_else(String::new, |speaker| speaker.to_string()),
        );
        self.text.set(cue.text.unwrap_or_default());
        self.camera.set(cue.camera.is_some());
        let camera = cue.camera.unwrap_or(CinematicCamera {
            eye: Vec3::new(0.0, 2.0, -4.0),
            target: Vec3::ZERO,
            vertical_fov_degrees: 60.0,
        });
        for (field, value) in self
            .eye
            .into_iter()
            .zip([camera.eye.x, camera.eye.y, camera.eye.z])
        {
            field.set(value.to_string());
        }
        for (field, value) in
            self.target
                .into_iter()
                .zip([camera.target.x, camera.target.y, camera.target.z])
        {
            field.set(value.to_string());
        }
        self.fov.set(camera.vertical_fov_degrees.to_string());
    }
    fn stage(self, fields: Fields, state: RwSignal<Option<AuthoringState>>) -> Result<(), String> {
        let source = document(state).ok_or("Ouvrir une carte avant de préparer un segment.")?;
        let selected = self.selected.get_untracked();
        let mut cues = fields.cues.get_untracked();
        let cue_id = if selected.is_empty() {
            let hash = canonical_json_sha256(&(source.area_id, &cues, fields.name.get_untracked()))
                .map_err(|error| error.to_string())?;
            EntityId::deterministic(format!("cinematic-cue/{hash}"))
        } else {
            id(&selected)?
        };
        let speaker = self.speaker.get_untracked();
        let text = self.text.get_untracked();
        let cue = CinematicCue {
            id: cue_id,
            duration_ms: count(&self.duration.get_untracked())?,
            speaker: if speaker.is_empty() {
                None
            } else {
                Some(id(&speaker)?)
            },
            text: if text.trim().is_empty() {
                None
            } else {
                Some(text)
            },
            camera: if self.camera.get_untracked() {
                Some(CinematicCamera {
                    eye: vector(self.eye)?,
                    target: vector(self.target)?,
                    vertical_fov_degrees: self
                        .fov
                        .get_untracked()
                        .trim()
                        .parse()
                        .map_err(|_| "Saisir un angle en degrés.".to_owned())?,
                })
            } else {
                None
            },
        };
        if selected.is_empty() {
            if cues.len() >= forge_gameplay::MAX_CINEMATIC_CUES {
                return Err("Au plus 32 segments par séquence.".into());
            }
            cues.push(cue);
        } else {
            *cues
                .iter_mut()
                .find(|entry| entry.id == cue_id)
                .ok_or("Choisir un segment du brouillon courant.")? = cue;
        }
        fields.cues.set(cues);
        Ok(())
    }
}

#[component]
fn CueControls(fields: Fields, state: RwSignal<Option<AuthoringState>>) -> impl IntoView {
    let cue = CueFields {
        selected: RwSignal::new(String::new()),
        duration: RwSignal::new("2000".into()),
        speaker: RwSignal::new(String::new()),
        text: RwSignal::new("Nouvelle réplique".into()),
        camera: RwSignal::new(false),
        eye: [
            RwSignal::new("0".into()),
            RwSignal::new("2".into()),
            RwSignal::new("-4".into()),
        ],
        target: [
            RwSignal::new("0".into()),
            RwSignal::new("0".into()),
            RwSignal::new("0".into()),
        ],
        fov: RwSignal::new("60".into()),
    };
    Effect::new(
        move |previous: Option<(String, Option<forge_core::ContentHash>)>| {
            let cues = fields.cues.get();
            let key = (cue.selected.get(), canonical_json_sha256(&cues).ok());
            if previous.as_ref() != Some(&key) {
                cue.load(
                    id(&key.0)
                        .ok()
                        .and_then(|id| cues.into_iter().find(|entry| entry.id == id)),
                );
            }
            key
        },
    );
    view! {
        <label>"Segment du brouillon à retoucher"<select prop:value=move ||cue.selected.get() on:change=move |event|cue.selected.set(event_target_value(&event))>
            <option value="">"Préparer un nouveau segment"</option>
            {move ||fields.cues.get().into_iter().enumerate().map(|(index,cue)|view!{<option value=cue.id.to_string()>{format!("Segment {}",index+1)}</option>}).collect_view()}
        </select></label>
        <label>"Durée (ms de simulation)"<input type="number" min="1" max=forge_gameplay::MAX_CINEMATIC_DURATION_MS.to_string() step="1" prop:value=move ||cue.duration.get() on:input=move |event|cue.duration.set(event_target_value(&event))/></label>
        <label>"Personnage qui parle"<select prop:value=move ||cue.speaker.get() on:change=move |event|cue.speaker.set(event_target_value(&event))>
            <option value="">"Sans personnage"</option>
            {move ||document(state).map(|source|source.actors.into_iter().map(|actor|view!{<option value=actor.id.to_string()>{actor.name}</option>}).collect_view())}
        </select></label>
        <label>"Réplique ou texte (facultatif pour un plan de caméra)"<textarea maxlength=forge_gameplay::MAX_CINEMATIC_TEXT_BYTES.to_string() prop:value=move ||cue.text.get() on:input=move |event|cue.text.set(event_target_value(&event))></textarea></label>
        <label><input type="checkbox" prop:checked=move ||cue.camera.get() on:change=move |event|cue.camera.set(event_target_checked(&event)) />"Ajouter un plan de caméra"</label>
        <Show when=move ||cue.camera.get()>
            <CameraCoordinates values=cue.eye label="Position de la caméra (m)"/>
            <CameraCoordinates values=cue.target label="Point regardé (m)"/>
            <label>"Champ vertical (degrés)"<input type="number" min="15" max="120" step="1" prop:value=move ||cue.fov.get() on:input=move |event|cue.fov.set(event_target_value(&event))/></label>
        </Show>
        <button on:click=move |_|fields.error.set(cue.stage(fields,state).err())>"Ajouter ou retoucher ce segment dans le brouillon"</button>
    }
}

#[component]
fn CameraCoordinates(values: [RwSignal<String>; 3], label: &'static str) -> impl IntoView {
    view! {<fieldset><legend>{label}</legend>{values.into_iter().zip(["X","Y","Z"]).map(|(value,axis)|view!{
        <label>{axis}<input type="number" step="0.1" min="-1000000" max="1000000" prop:value=move ||value.get() on:input=move |event|value.set(event_target_value(&event))/></label>
    }).collect_view()}</fieldset>}
}
