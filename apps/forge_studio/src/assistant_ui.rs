//! Human data-sharing and source-mutation confirmations remain distinct.
use crate::assistant_protocol::{AssistantDraft, AssistantRequest, AssistantView};
use forge_core::{EntityId, Vec3};
use forge_project::{
    assistant::{
        jobs::JobState,
        selection::{DialogueLineId, Region, Selection},
    },
    authoring::AuthoringState,
};
use leptos::prelude::*;
use wasm_bindgen::JsValue;

mod recovery;
mod targets;

const DRAFT_KEY: &str = "forge.assistant.last-draft.v1";

#[derive(Clone, Copy)]
struct Controls {
    state: RwSignal<Option<AuthoringState>>,
    host_busy: RwSignal<bool>,
    view: RwSignal<Option<AssistantView>>,
    draft: RwSignal<Option<AssistantDraft>>,
    status: RwSignal<String>,
    diagnostic: RwSignal<String>,
    recovery_target: RwSignal<Option<forge_core::CommandId>>,
    working: RwSignal<bool>,
    generating: RwSignal<bool>,
    epoch: RwSignal<u64>,
    sharing: RwSignal<bool>,
    applying: RwSignal<bool>,
    on_applied: Callback<AuthoringState>,
}

impl Controls {
    fn new(
        state: RwSignal<Option<AuthoringState>>,
        host_busy: RwSignal<bool>,
        on_applied: Callback<AuthoringState>,
    ) -> Self {
        Self {
            state,
            host_busy,
            view: RwSignal::new(None),
            draft: RwSignal::new(None),
            status: RwSignal::new(String::new()),
            diagnostic: RwSignal::new(String::new()),
            recovery_target: RwSignal::new(None),
            working: RwSignal::new(false),
            generating: RwSignal::new(false),
            epoch: RwSignal::new(0),
            sharing: RwSignal::new(false),
            applying: RwSignal::new(false),
            on_applied,
        }
    }

    fn send(self, request: AssistantRequest) {
        let configuring = matches!(request, AssistantRequest::Configuration);
        let recovering = matches!(
            request,
            AssistantRequest::InspectRecoveries { .. } | AssistantRequest::RecoverJob { .. }
        );
        let generating = matches!(request, AssistantRequest::Generate { .. });
        let host_locked = matches!(
            request,
            AssistantRequest::Prepare { .. }
                | AssistantRequest::Review { .. }
                | AssistantRequest::Apply { .. }
        );
        let draft = if let AssistantRequest::Prepare { draft } = &request {
            // Keep the brief even when the first native journal save fails.
            // This stores intent only, never a sharing/apply confirmation.
            save_draft(draft);
            Some(draft.clone())
        } else {
            None
        };
        let epoch = self.epoch.get_untracked().wrapping_add(1);
        self.epoch.set(epoch);
        self.sharing.set(false);
        self.applying.set(false);
        if generating {
            self.generating.set(true);
            self.status.set("Appel du modèle ; la carte reste éditable. Toute retouche périmera cette proposition.".into());
        } else {
            self.working.set(true);
            self.status.set("Vérification de l'assistant…".into());
        }
        if host_locked {
            self.host_busy.set(true);
        }
        wasm_bindgen_futures::spawn_local(async move {
            let result = invoke(&request).await;
            if generating {
                let _ = self.generating.try_set(false);
            }
            if host_locked {
                let _ = self.host_busy.try_set(false);
            }
            if self.epoch.try_get_untracked() != Some(epoch) {
                return;
            }
            let _ = self.working.try_set(false);
            match result {
                Ok(mut view) => {
                    let _ = self.diagnostic.try_set(String::new());
                    if let Some(id) = view.recovered_job {
                        self.recovery_target.set(Some(id));
                    }
                    if let Some(mut draft) = draft
                        && let Some(job) = &view.job
                    {
                        draft.resume_job = Some(job.job_id);
                        self.recovery_target.set(None);
                        save_draft(&draft);
                        let _ = self.draft.try_set(Some(draft));
                    }
                    if let Some(authoring) = view.authoring.take() {
                        self.on_applied.run(*authoring);
                    }
                    let _ = self.status.try_set(view.message.clone());
                    let _ = self.view.try_set(Some(view));
                }
                Err(error) => {
                    if generating {
                        let _ = self.diagnostic.try_set(error);
                        let _ = self.status.try_set("Le fournisseur n’a pas fourni de proposition utilisable. Aucune modification appliquée ; consulter Diagnostics avant une nouvelle tentative.".into());
                    } else if recovering {
                        let _ = self.diagnostic.try_set(error);
                        let _ = self.status.try_set("La récupération n’a pas abouti. Consulter Diagnostics puis examiner à nouveau les versions.".into());
                    } else if configuring {
                        let _ = self.diagnostic.try_set(error);
                        let _ = self.status.try_set(
                            "Vérifier les réglages du fournisseur dans Diagnostics.".into(),
                        );
                    } else {
                        let _ = self.status.try_set(error);
                    }
                }
            }
        });
    }

    fn job_state(self) -> Option<JobState> {
        self.view
            .get()
            .and_then(|view| view.job.map(|job| job.state))
    }
    fn prepare_draft(
        self,
        fields: DraftFields,
        selected: RwSignal<Option<EntityId>>,
        assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
    ) -> Result<AssistantDraft, String> {
        let mut draft = selected_draft(self.state.get_untracked(), fields, selected, assets)?;
        draft.resume_job = self.recovery_target.get_untracked();
        Ok(draft)
    }
    fn unavailable(self) -> bool {
        self.working.get()
            || self.host_busy.get()
            || crate::authoring_native_game::paused()
            || self
                .state
                .get()
                .is_none_or(|state| state.zone_head.is_none())
    }
    fn stale(self) -> bool {
        let Some(draft) = self.draft.get() else {
            return true;
        };
        self.state
            .get()
            .and_then(|state| state.zone_head)
            .and_then(|head| head.content_hash().ok())
            != Some(draft.expected_head)
    }
}

#[component]
pub(crate) fn AssistantControls(
    state: RwSignal<Option<AuthoringState>>,
    host_busy: RwSignal<bool>,
    selected_placement: RwSignal<Option<EntityId>>,
    on_applied: Callback<AuthoringState>,
) -> impl IntoView {
    let controls = Controls::new(state, host_busy, on_applied);
    let fields = bind_fields(controls, selected_placement);
    let assets = RwSignal::new(None::<crate::assistant_asset_protocol::AssetSelection>);
    let (brief, _, _, _, _, bounds, _) = fields;
    let parameters_match = matches_prepared(fields, controls, selected_placement, assets);
    Effect::new(move |_| {
        let _ = assets.get();
        controls.sharing.set(false);
        controls.applying.set(false);
    });
    clear_assets_on_zone_change(state, assets);
    let mcp_scope = mcp_scope(fields, state, selected_placement, assets);
    controls.send(AssistantRequest::Configuration);
    let draft = move || controls.prepare_draft(fields, selected_placement, assets);
    view! {
        <details class="gameplay-module-export assistant-controls" data-forge-assistant="true">
            <summary>"Assistant IA · proposer puis relire"</summary>
            <details class="assistant-setup"><summary>"Configurer ou vérifier le fournisseur IA"</summary>
                <ol>
                    <li>"Lancez votre serveur de modèle local, ou relevez l’adresse et le nom de modèle de votre fournisseur."</li>
                    <li>"Dans forge-studio.local.json, à côté de l’exécutable Studio, ajoutez la section assistant ci-dessous. Conservez les autres sections du fichier. Adaptez l’adresse, le modèle, les plafonds et les tarifs déclarés."</li>
                    <li>"Pour une clé d’accès, indiquez seulement le nom d’une variable d’environnement dans api_key_environment. La clé reste dans l’environnement natif ; ne la copiez pas dans vos projets."</li>
                    <li>"Sauvegardez vos cartes, puis relancez le Studio pour charger ces réglages. Vérifiez ici la destination et les plafonds avant d’autoriser un envoi."</li>
                </ol>
                <p>"Exemple pour un serveur local sans clé : remplacez modele-local par le nom exact proposé par ce serveur. Les tarifs nuls correspondent uniquement à cet usage local."</p>
                <pre class="assistant-configuration-example">{r#""assistant": {
  "base_url": "http://127.0.0.1:1234/v1",
  "model": "modele-local",
  "api_kind": "chat_completions",
  "enable_reasoning": false,
  "api_key_environment": null,
  "limits": {
    "maximum_attempts": 1,
    "maximum_input_tokens": 16000,
    "maximum_output_tokens": 2048,
    "maximum_micro_usd": 0,
    "prices": {"input_per_million": 0, "output_per_million": 0}
  }
}"#}</pre>
                <button type="button" disabled=move ||controls.unavailable() ||controls.generating.get()
                    on:click=move |_|controls.send(AssistantRequest::Configuration)>"Vérifier les réglages chargés · aucun envoi au modèle"</button>
                <p>"L’édition manuelle reste disponible sans fournisseur IA. Cette vérification lit la configuration ; elle ne teste pas une génération et n’envoie aucune donnée de carte."</p>
            </details>
            <p>"Choisir les données autorisées, décrire la retouche, puis vérifier l’envoi et les modifications proposées. La proposition n’est appliquée qu’après votre confirmation."</p>
            <p>{move || controls.view.get().map_or_else(||"Fournisseur à configurer dans les réglages locaux du Studio.".into(), |view|format!("{} · {} · raisonnement {} · {} tentative(s) maximum",view.provider.destination,view.provider.model,if view.provider.reasoning {"activé"} else {"selon le fournisseur"},view.provider.limits.maximum_attempts))}</p>
            <p>{move||controls.view.get().map(|view|format!("Délai maximal par appel : {} s. Aucune relance automatique.",view.provider.maximum_request_seconds))}</p>
            <p>{move || controls.view.get().map(|view| {
                let limits = view.provider.limits;
                format!("Plafond du job : {}.{:06} USD aux tarifs déclarés · {} tokens d’entrée et {} de sortie par appel. Aucun retry automatique ; toute reprise exige une nouvelle confirmation.", limits.maximum_micro_usd / 1_000_000, limits.maximum_micro_usd % 1_000_000, limits.maximum_input_tokens, limits.maximum_output_tokens)
            })}</p>
            <label>"Décrire la retouche"<textarea rows="4" maxlength="8192" prop:value=move||brief.get() on:input=move|event|brief.set(event_target_value(&event))/></label>
            <AllowedTargets state fields selected_placement/>
            <details><summary>"Région autorisée en mètres"</summary><div class="assistant-region" style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:.5rem">
                {[(0,"X minimum"),(1,"X maximum"),(2,"Y minimum"),(3,"Y maximum"),(4,"Z minimum"),(5,"Z maximum")].into_iter().map(move|(index,label)|view!{
                    <label>{label}<input type="number" step="any" prop:value=move||bounds.get()[index].clone() on:input=move|event|bounds.update(|values|values[index]=event_target_value(&event))/></label>
                }).collect_view()}
            </div></details>
            <crate::assistant_assets_ui::AssetReferences selection=assets/>
            <button type="button" disabled=move||controls.unavailable() || controls.generating.get() on:click=move |_|match draft(){
                Ok(draft)=>controls.send(AssistantRequest::Prepare{draft}),Err(error)=>controls.status.set(error)
            }>"Préparer les données"</button>
            <button type="button" disabled=move||controls.unavailable() || controls.generating.get() on:click=move |_|{
                resume_last_job(controls, fields, assets);
            }>"Reprendre la dernière demande"</button>
            <PreparedData controls parameters_match fields selected_placement assets/>
            <recovery::RecoveryControls controls/>
            <Show when=move||controls.view.get().is_some_and(|view|view.job.is_some())>
                <p>{move||controls.view.get().and_then(|view|view.job).map(|job|if controls.generating.get(){format!("Job {} · appel du modèle en cours ; compteurs actualisés au retour.",job.job_id)}else{format!("Job {} · tentative {} · coût engagé ou réservé : {}.{:06} USD · {} appel(s) incertain(s)",job.job_id,job.attempts,job.committed_or_reserved_micro_usd/1_000_000,job.committed_or_reserved_micro_usd%1_000_000,job.uncertain_attempts)}).unwrap_or_default()}</p>
                <p>{move||controls.view.get().and_then(|view|view.job).and_then(|job|job.result).map(|result|format!("{} · {} / {} tokens déclarés",result.proposal.summary,result.usage.input_tokens,result.usage.output_tokens)).unwrap_or_default()}</p>
                <p>{move||controls.view.get().and_then(|view|view.job).and_then(|job|job.result).filter(|result|!result.proposal.missing_capabilities.is_empty()).map(|result|format!("Capacités manquantes : {}",result.proposal.missing_capabilities.join(", "))).unwrap_or_default()}</p>
                <button type="button" disabled=move||controls.unavailable() || controls.generating.get() || controls.stale() || !parameters_match.get() || controls.job_state()!=Some(JobState::Staged) on:click=move |_|{
                    if let Some(job)=controls.view.get_untracked().and_then(|view|view.job){controls.send(AssistantRequest::Review{job_id:job.job_id});}
                }>"Relire les modifications proposées"</button>
                <Show when=move||controls.view.get().is_some_and(|view|view.review_hash.is_some())>
                    <ol>{move||controls.view.get().map(source_changes)}</ol>
                    <label><input type="checkbox" prop:checked=move||controls.applying.get() on:change=move|event|controls.applying.set(event_target_checked(&event))/>"J’approuve ce diff pour cette révision"</label>
                    <button type="button" disabled=move||controls.unavailable() || controls.stale() || !parameters_match.get() || !controls.applying.get() || controls.job_state()!=Some(JobState::Staged) on:click=move |_|{
                        if let Some(view)=controls.view.get_untracked() && let(Some(job),Some(hash))=(view.job,view.review_hash){controls.send(AssistantRequest::Apply{job_id:job.job_id,expected_review_hash:hash});}
                    }>"Appliquer les modifications approuvées"</button>
                </Show>
                <button type="button" disabled=move||controls.working.get() || matches!(controls.job_state(),None|Some(JobState::Applied|JobState::Cancelled)) on:click=move |_|{
                    if let Some(job)=controls.view.get_untracked().and_then(|view|view.job){controls.send(AssistantRequest::Cancel{job_id:job.job_id});}
                }>"Annuler cette demande"</button>
                <button type="button" disabled=move||controls.unavailable() || controls.generating.get() || controls.job_state()!=Some(JobState::Running) on:click=move |_|{
                    if let Some(job)=controls.view.get_untracked().and_then(|view|view.job){controls.send(AssistantRequest::Interrupt{job_id:job.job_id,attempt:job.attempts});}
                }>"Confirmer l’interruption d’un appel perdu"</button>
            </Show>
            <output aria-live="polite">{move||controls.status.get()}</output>
            <p>"Une génération n’est pas une sauvegarde. Après application, utiliser Sauvegarder ; Annuler et Rétablir restent disponibles dans l’historique commun."</p>
            <Show when=move||!controls.diagnostic.get().is_empty()><details><summary>"Diagnostics de l’assistant"</summary><pre style="white-space:pre-wrap;overflow-wrap:anywhere">{move||controls.diagnostic.get()}</pre></details></Show>
            <crate::mcp_ui::McpControls scope=mcp_scope state host_busy on_applied/>
        </details>
    }
}

fn resume_last_job(
    controls: Controls,
    fields: DraftFields,
    assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
) {
    let (brief, zone, placement, object, line, bounds, create) = fields;
    if let Some(mut saved) = load_draft() {
        if let Some(id) = controls.recovery_target.get_untracked() {
            saved.resume_job = Some(id);
        }
        assets.set(saved.assets.clone());
        brief.set(saved.brief.clone());
        zone.set(saved.selection.zone_properties);
        create.set(saved.selection.create_content);
        placement.set(!saved.selection.placements.is_empty());
        if let Some(id) = saved.selection.placements.first() {
            crate::authoring_assets::placements::selection::choose(Some(*id));
        }
        object.set(
            saved
                .selection
                .objects
                .first()
                .map(ToString::to_string)
                .unwrap_or_default(),
        );
        line.set(
            saved
                .selection
                .dialogue_lines
                .first()
                .map(|id| format!("{}|{}", id.dialogue_id, id.node_id))
                .unwrap_or_default(),
        );
        bounds.set([
            saved.region.minimum.x.to_string(),
            saved.region.maximum.x.to_string(),
            saved.region.minimum.y.to_string(),
            saved.region.maximum.y.to_string(),
            saved.region.minimum.z.to_string(),
            saved.region.maximum.z.to_string(),
        ]);
        controls.send(AssistantRequest::Prepare { draft: saved });
    } else {
        controls
            .status
            .set("Aucune demande mémorisée dans cette fenêtre.".into());
    }
}

#[component]
fn AllowedTargets(
    state: RwSignal<Option<AuthoringState>>,
    fields: DraftFields,
    selected_placement: RwSignal<Option<EntityId>>,
) -> impl IntoView {
    let (_, zone, placement, object, line, _, create) = fields;
    view! { <>            <label><input type="checkbox" prop:checked=move||zone.get() on:change=move|event|zone.set(event_target_checked(&event))/>"Autoriser le nom et l’altitude générale de la carte"</label>
                <label><input type="checkbox" disabled=move||state.get().and_then(|state|state.zone_head).is_none_or(|head|head.corrections.gameplay.is_none()) prop:checked=move||create.get() on:change=move|event|create.set(event_target_checked(&event))/>"Autoriser l’ajout de personnages, lieux, objets, dialogues et quêtes dans cette région"</label>
                <p>"Initialiser le contenu RPG et configurer les capacités du module dans le Creator avant de créer une aventure. Les définitions existantes et les règles sont conservées."</p>
                <label><input type="checkbox" disabled=move||selected_placement.get().is_none() prop:checked=move||placement.get() on:change=move|event|placement.set(event_target_checked(&event))/>"Autoriser le déplacement du modèle actuellement sélectionné"</label>
                <targets::TargetSelect label="Objet RPG autorisé" selected=object targets=Signal::derive(move || {
                    state.get().and_then(|state|state.zone_head).and_then(|head|head.corrections.gameplay)
                        .map(|document|document.objects.into_iter().map(|object|targets::Target { label:format!("{} · {}",object.name,object.id),id:object.id.to_string() }).collect()).unwrap_or_default()
                })/>
                <targets::TargetSelect label="Réplique autorisée" selected=line targets=Signal::derive(move || {
                    state.get().and_then(|state|state.zone_head).and_then(|head|head.corrections.gameplay).map(|document| {
                        document.dialogues.iter().flat_map(|dialogue| {
                            let speaker = document.actors.iter().filter(|actor|actor.dialogue==Some(dialogue.id)).map(|actor|actor.name.clone()).collect::<Vec<_>>().join(", ");
                            let context = if speaker.is_empty() { format!("Dialogue {}",dialogue.id) } else { speaker };
                            dialogue.nodes.iter().enumerate().map(move |(index,node)|targets::Target {
                                id:format!("{}|{}",dialogue.id,node.id),
                                label:format!("{context} · réplique {} · {}",index+1,node.text),
                            })
                        }).collect()
                    }).unwrap_or_default()
                })/>
    </> }
}

#[component]
fn PreparedData(
    controls: Controls,
    parameters_match: Signal<bool>,
    fields: DraftFields,
    selected_placement: RwSignal<Option<EntityId>>,
    assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
) -> impl IntoView {
    let draft = move || controls.prepare_draft(fields, selected_placement, assets);
    view! {
            <Show when=move||controls.view.get().is_some_and(|view|view.outbound_body.is_some())>
                <details><summary>"Données exactes proposées pour l’envoi"</summary><pre style="white-space:pre-wrap;overflow-wrap:anywhere;max-height:24rem;overflow:auto">{move||controls.view.get().and_then(|view|view.outbound_body).and_then(|body|serde_json::to_string_pretty(&body).ok()).unwrap_or_default()}</pre></details>
                <label><input type="checkbox" prop:checked=move||controls.sharing.get() on:change=move|event|controls.sharing.set(event_target_checked(&event))/>"J’autorise l’envoi de ces données à ce fournisseur"</label>
                <button type="button" aria-busy=move||controls.generating.get().to_string() disabled=move||controls.unavailable() || controls.generating.get() || !controls.sharing.get() || controls.stale() || !parameters_match.get() || !matches!(controls.job_state(),Some(JobState::Prepared|JobState::Interrupted)) on:click=move |_|{
                    let Some(view)=controls.view.get_untracked() else{return;};
                    let (Some(job),Some(hash))=(view.job,view.outbound_hash) else{return;};
                    let current=draft();let saved=controls.draft.get_untracked();
                    if !current.ok().zip(saved).is_some_and(|(mut current,mut saved)|{current.resume_job=None;saved.resume_job=None;current==saved}){controls.status.set("La demande ou la sélection a changé ; préparer de nouvelles données.".into());return;}
                    controls.send(AssistantRequest::Generate{job_id:job.job_id,expected_outbound_hash:hash});
                }>{move||if controls.generating.get(){"Appel du modèle en cours…"}else{"Autoriser l’envoi et générer"}}</button>
            </Show>
    }
}

fn selected_draft(
    state: Option<AuthoringState>,
    fields: DraftFields,
    selected: RwSignal<Option<EntityId>>,
    assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
) -> Result<AssistantDraft, String> {
    let (brief, zone, placement, object, line, bounds, create) = fields;
    let mut draft = make_draft(
        state,
        brief.get_untracked(),
        zone.get_untracked(),
        if placement.get_untracked() {
            selected.get_untracked()
        } else {
            None
        },
        &object.get_untracked(),
        &line.get_untracked(),
        &bounds.get_untracked(),
    )?;
    draft.assets = assets.get_untracked();
    draft.selection.create_content = create.get_untracked();
    if draft.selection == Selection::default()
        && !draft
            .assets
            .as_ref()
            .is_some_and(|selected| selected.allow_placement)
    {
        return Err(
            "Autoriser une retouche ou l’ajout de modèles avant de préparer l’envoi.".into(),
        );
    }
    Ok(draft)
}

fn clear_assets_on_zone_change(
    state: RwSignal<Option<AuthoringState>>,
    assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
) {
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let area = state
            .get()
            .and_then(|state| state.zone_head)
            .map(|head| head.base.terrain.area_id);
        if previous.is_some_and(|old| old != area) {
            assets.set(None);
        }
        area
    });
}

type DraftFields = (
    RwSignal<String>,
    RwSignal<bool>,
    RwSignal<bool>,
    RwSignal<String>,
    RwSignal<String>,
    RwSignal<[String; 6]>,
    RwSignal<bool>,
);

fn mcp_scope(
    fields: DraftFields,
    state: RwSignal<Option<AuthoringState>>,
    selected: RwSignal<Option<EntityId>>,
    assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
) -> Signal<Result<crate::mcp_protocol::ScopeRequest, String>> {
    let (_, zone, placement, object, line, bounds, create) = fields;
    Signal::derive(move || {
        let mut draft = make_draft(
            state.get(),
            "Périmètre MCP".into(),
            zone.get(),
            if placement.get() {
                selected.get()
            } else {
                None
            },
            &object.get(),
            &line.get(),
            &bounds.get(),
        )?;
        draft.selection.create_content = create.get();
        Ok(crate::mcp_protocol::ScopeRequest {
            expected_head: draft.expected_head,
            selection: draft.selection,
            region: draft.region,
            assets: assets.get(),
        })
    })
}

fn bind_fields(controls: Controls, selected_placement: RwSignal<Option<EntityId>>) -> DraftFields {
    let state = controls.state;
    let brief = RwSignal::new(String::new());
    let zone = RwSignal::new(false);
    let create = RwSignal::new(false);
    let placement = RwSignal::new(false);
    let object = RwSignal::new(String::new());
    let line = RwSignal::new(String::new());
    let bounds = RwSignal::new([
        String::new(),
        String::new(),
        "-4096".into(),
        "4096".into(),
        String::new(),
        String::new(),
    ]);
    Effect::new(move |previous: Option<Option<forge_core::AreaId>>| {
        let head = state.get().and_then(|state| state.zone_head);
        let area = head.as_ref().map(|head| head.base.terrain.area_id);
        if previous != Some(area)
            && let Some(head) = head
        {
            let extent = head.base.terrain.extent_m;
            bounds.set([
                (-extent.size_x_m * 0.5).to_string(),
                (extent.size_x_m * 0.5).to_string(),
                "-4096".into(),
                "4096".into(),
                (-extent.size_z_m * 0.5).to_string(),
                (extent.size_z_m * 0.5).to_string(),
            ]);
            object.set(String::new());
            line.set(String::new());
            create.set(false);
            // Changing a zone invalidates job data, not the native provider
            // disclosure that may already have arrived during initial mount.
            controls.draft.set(None);
            controls.view.update(|view| {
                if let Some(view) = view {
                    view.job = None;
                    view.outbound_hash = None;
                    view.outbound_body = None;
                    view.review_hash = None;
                    view.changes.clear();
                    view.authoring = None;
                }
            });
        }
        area
    });
    Effect::new(move |_| {
        let _ = (
            brief.get(),
            zone.get(),
            create.get(),
            placement.get(),
            object.get(),
            line.get(),
            bounds.get(),
            selected_placement.get(),
        );
        controls.sharing.set(false);
        controls.applying.set(false);
    });
    (brief, zone, placement, object, line, bounds, create)
}

fn matches_prepared(
    fields: DraftFields,
    controls: Controls,
    selected_placement: RwSignal<Option<EntityId>>,
    assets: RwSignal<Option<crate::assistant_asset_protocol::AssetSelection>>,
) -> Signal<bool> {
    let (brief, zone, placement, object, line, bounds, create) = fields;
    Signal::derive(move || {
        let current = make_draft(
            controls.state.get(),
            brief.get(),
            zone.get(),
            if placement.get() {
                selected_placement.get()
            } else {
                None
            },
            &object.get(),
            &line.get(),
            &bounds.get(),
        )
        .map(|mut draft| {
            draft.assets = assets.get();
            draft.selection.create_content = create.get();
            draft
        });
        current
            .ok()
            .zip(controls.draft.get())
            .is_some_and(|(mut current, mut saved)| {
                current.resume_job = None;
                saved.resume_job = None;
                current == saved
            })
    })
}

fn make_draft(
    state: Option<AuthoringState>,
    brief: String,
    zone_properties: bool,
    placement: Option<EntityId>,
    object: &str,
    line: &str,
    bounds: &[String; 6],
) -> Result<AssistantDraft, String> {
    if brief.trim().is_empty() {
        return Err("Décrire la retouche souhaitée avant de préparer l'envoi.".into());
    }
    let head = state
        .and_then(|state| state.zone_head)
        .ok_or("Ouvrir une carte pour préparer l'assistant.")?;
    let number = |index: usize| {
        bounds[index]
            .parse::<f32>()
            .map_err(|_| "Saisir six limites de région numériques.".to_owned())
    };
    let objects = if object.is_empty() {
        Vec::new()
    } else {
        vec![object.parse().map_err(|_| "Objet sélectionné invalide.")?]
    };
    let dialogue_lines = if line.is_empty() {
        Vec::new()
    } else {
        let (dialogue, node) = line
            .split_once('|')
            .ok_or("Réplique sélectionnée invalide.")?;
        vec![DialogueLineId {
            dialogue_id: dialogue.parse().map_err(|_| "Dialogue invalide.")?,
            node_id: node.parse().map_err(|_| "Réplique invalide.")?,
        }]
    };
    Ok(AssistantDraft {
        expected_head: head.content_hash()?,
        brief,
        selection: Selection {
            placements: placement.into_iter().collect(),
            objects,
            dialogue_lines,
            zone_properties,
            create_content: false,
        },
        region: Region {
            minimum: Vec3::new(number(0)?, number(2)?, number(4)?),
            maximum: Vec3::new(number(1)?, number(3)?, number(5)?),
        },
        resume_job: None,
        assets: None,
    })
}

pub(crate) fn change_label(path: &str) -> String {
    if let Some(label) = match path {
        "/corrections/gameplay/actors" => Some("Personnages du RPG"),
        "/corrections/gameplay/items" => Some("Objets de l’inventaire"),
        "/corrections/gameplay/objects" => Some("Objets RPG"),
        "/corrections/gameplay/places" => Some("Lieux et liaisons"),
        "/corrections/gameplay/dialogues" => Some("Dialogues"),
        "/corrections/gameplay/quests" => Some("Quêtes"),
        "/corrections/gameplay/narrative" => Some("Variables et solutions de quête"),
        "/corrections/placements" => Some("Modèles placés sur la carte"),
        _ => None,
    } {
        return label.into();
    }
    let label = if path == "/corrections/name" {
        "Nom de la carte"
    } else if path == "/corrections/base_elevation_m" {
        "Altitude générale"
    } else if path.starts_with("/corrections/placements/") {
        "Position du modèle sélectionné"
    } else if path.starts_with("/corrections/gameplay/objects/") {
        "Position de l'objet RPG sélectionné"
    } else if path.starts_with("/corrections/gameplay/dialogues/") {
        "Texte de la réplique sélectionnée"
    } else {
        "Donnée du projet"
    };
    match path.rsplit('/').next() {
        Some(axis @ ("x" | "y" | "z")) => format!("{label} · {}", axis.to_uppercase()),
        _ => label.into(),
    }
}

fn source_changes(view: AssistantView) -> impl IntoView {
    view.changes.into_iter().map(move |change| {
        let label = change_label(&change.path);
        let before = format!("Avant : {}", change_before(&change, view.zone_name_before.as_deref()));
        let after = format!("Après : {}", change.after.as_ref().map_or_else(|| "absent".into(), ToString::to_string));
        let addition = crate::assistant_protocol::placement_additions(&change);
        let content = if let Some((retained, added)) = addition {
            let summary = format!("{} modèle(s) ajouté(s) · {retained} existant(s) conservé(s) sans modification.", added.len());
            view! {
                <p>{summary}</p>
                <ul>{added.into_iter().map(|placement| {
                    let transform = placement.transform;
                    let position = transform.translation_m;
                    let scale = transform.scale;
                    let rotation = transform.rotation;
                    view! {
                        <li><strong>{placement.name}</strong>
                            <p>{format!("Position : X {} · Y {} · Z {} m. Échelle : {} · {} · {}.", position.x, position.y, position.z, scale.x, scale.y, scale.z)}</p>
                            <details><summary>"Modèle et orientation"</summary>
                                <p style="overflow-wrap:anywhere">{format!("Objet : {} · asset : {} · SHA-256 : {}", placement.id, placement.asset_id, placement.asset_hash)}</p>
                                <p>{format!("Rotation (x, y, z, w) : {}, {}, {}, {}. Verrouillé : {}.", rotation.x, rotation.y, rotation.z, rotation.w, placement.locked)}</p>
                            </details>
                        </li>
                    }
                }).collect_view()}</ul>
                <details><summary>"Diff exact et identité des données"</summary>
                    <code style="overflow-wrap:anywhere">{change.path.clone()}</code>
                    <p style="overflow-wrap:anywhere">{before}</p><p style="overflow-wrap:anywhere">{after}</p>
                </details>
            }.into_any()
        } else {
            view! {
                <p style="overflow-wrap:anywhere">{before}</p><p style="overflow-wrap:anywhere">{after}</p>
                <details><summary>"Identité de la donnée"</summary><code style="overflow-wrap:anywhere">{change.path}</code></details>
            }.into_any()
        };
        view! { <li><strong>{label}</strong>{content}</li> }
    }).collect_view()
}

pub(crate) fn change_before(
    change: &forge_project::assistant::SourceChange,
    zone_name: Option<&str>,
) -> String {
    if change.before.is_none()
        && change.path == "/corrections/name"
        && let Some(name) = zone_name
    {
        return serde_json::Value::String(name.into()).to_string();
    }
    change
        .before
        .as_ref()
        .map_or_else(|| "absent".into(), ToString::to_string)
}

fn save_draft(draft: &AssistantDraft) {
    if let Some(storage) =
        web_sys::window().and_then(|window| window.local_storage().ok().flatten())
        && let Ok(json) = serde_json::to_string(draft)
    {
        let _ = storage.set_item(DRAFT_KEY, &json);
    }
}
fn load_draft() -> Option<AssistantDraft> {
    let json = web_sys::window()?
        .local_storage()
        .ok()??
        .get_item(DRAFT_KEY)
        .ok()??;
    if json.len() > 32 * 1024 {
        return None;
    }
    serde_json::from_str(&json).ok()
}

async fn invoke(request: &AssistantRequest) -> Result<AssistantView, String> {
    let arguments = js_sys::Object::new();
    js_sys::Reflect::set(
        &arguments,
        &JsValue::from_str("request"),
        &JsValue::from_str(&serde_json::to_string(request).map_err(|error| error.to_string())?),
    )
    .map_err(|_| "Préparation de la requête d'assistant impossible.")?;
    let value = crate::web::invoke_tauri_raw("execute_assistant", arguments.as_ref()).await?;
    serde_wasm_bindgen::from_value(value).map_err(|error| error.to_string())
}
