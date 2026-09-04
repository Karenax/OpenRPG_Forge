//! Forms issue native commands only. Their local strings are unsubmitted drafts.
use super::{TextField, WorkspaceActions, WorkspaceNode, read_document};
use crate::workspace_api::WorkspaceCommand;
use forge_core::ModuleId;
use leptos::prelude::*;

fn selected_module(selected: WorkspaceNode) -> Option<ModuleId> {
    match selected {
        WorkspaceNode::Module(id) | WorkspaceNode::Area(id, _) => Some(id),
        WorkspaceNode::Project => None,
    }
}

#[component]
pub(super) fn AddSourcesForms(
    actions: WorkspaceActions,
    selected: ReadSignal<WorkspaceNode>,
) -> impl IntoView {
    let (module_name, set_module_name) = signal(String::new());
    let (name, set_name) = signal(String::new());
    let (seed, set_seed) = signal("1".to_owned());
    let (x, set_x) = signal("0".to_owned());
    let (y, set_y) = signal("2".to_owned());
    let (z, set_z) = signal("0".to_owned());
    let unavailable = move || actions.unavailable() || read_document(actions).is_none();
    view! {
        <section class="project-card" aria-labelledby="workspace-add-module-heading">
            <h2 id="workspace-add-module-heading">"Nouveau module"</h2>
            <p class="hint">"Un chapitre indépendant dans le projet ouvert. Ajoutez ensuite ses zones."</p>
            <form on:submit=move |event| {
                event.prevent_default();
                actions.send(WorkspaceCommand::AddModule {
                    base_revision: actions.state.get_untracked().revision, name: module_name.get_untracked(),
                });
            }>
                <fieldset disabled=unavailable>
                    <legend class="project-sr-only">"Créer un module"</legend>
                    <TextField id="workspace-new-module-name" label="Nom du nouveau module" value=module_name set_value=set_module_name/>
                    <button id="workspace-add-module" type="submit">"Ajouter le module"</button>
                </fieldset>
            </form>
        </section>
        <section class="project-card" aria-labelledby="workspace-add-area-heading">
            <h2 id="workspace-add-area-heading">"Nouvelle zone extérieure"</h2>
            <p class="hint" id="workspace-area-target">{move || {
                selected_module(selected.get()).and_then(|id| read_document(actions)?.module(id).map(|m| format!("Destination : {}", m.name)))
                    .unwrap_or_else(|| "Sélectionnez un module dans l’arborescence.".into())
            }}</p>
            <form on:submit=move |event| {
                event.prevent_default();
                let Some(module_id) = selected_module(selected.get_untracked()) else { return; };
                actions.send(WorkspaceCommand::AddArea {
                    base_revision: actions.state.get_untracked().revision, module_id,
                    name: name.get_untracked(), seed: seed.get_untracked(),
                    spawn_x: x.get_untracked(), spawn_y: y.get_untracked(), spawn_z: z.get_untracked(),
                });
            }>
                <fieldset disabled=move || unavailable() || selected_module(selected.get()).is_none()>
                    <legend class="project-sr-only">"Créer une zone"</legend>
                    <TextField id="workspace-new-area-name" label="Nom de la nouvelle zone" value=name set_value=set_name/>
                    <TextField id="workspace-new-area-seed" label="Seed du nouveau terrain" value=seed set_value=set_seed/>
                    <div class="project-spawn-fields">
                        <TextField id="workspace-new-area-x" label="Départ X" value=x set_value=set_x/>
                        <TextField id="workspace-new-area-y" label="Départ Y" value=y set_value=set_y/>
                        <TextField id="workspace-new-area-z" label="Départ Z" value=z set_value=set_z/>
                    </div>
                    <p class="hint">"Terrain indépendant de 512 × 512 m. Le départ reste une métadonnée ; aucun personnage n’est placé ici."</p>
                    <button id="workspace-add-area" class="project-primary" type="submit">"Ajouter la zone"</button>
                </fieldset>
            </form>
        </section>
    }
}

#[component]
pub(super) fn EditMetadataForm(
    actions: WorkspaceActions,
    selected: ReadSignal<WorkspaceNode>,
) -> impl IntoView {
    let (name, set_name) = signal(String::new());
    let (x, set_x) = signal(String::new());
    let (y, set_y) = signal(String::new());
    let (z, set_z) = signal(String::new());
    let accepted = Memo::new(move |_| read_document(actions));
    Effect::new(move |_| {
        let Some(document) = accepted.get() else {
            return;
        };
        let (name, spawn) = match selected.get() {
            WorkspaceNode::Project => (document.project.name.clone(), None),
            WorkspaceNode::Module(id) => (
                document
                    .module(id)
                    .map(|m| m.name.clone())
                    .unwrap_or_default(),
                None,
            ),
            WorkspaceNode::Area(module, id) => document
                .area(module, id)
                .map(|area| (area.name.clone(), Some(area.spawn)))
                .unwrap_or_default(),
        };
        set_name.set(name);
        set_x.set(spawn.map(|v| v.x.to_string()).unwrap_or_default());
        set_y.set(spawn.map(|v| v.y.to_string()).unwrap_or_default());
        set_z.set(spawn.map(|v| v.z.to_string()).unwrap_or_default());
    });
    view! {
        <section class="project-card" hidden=move || read_document(actions).is_none()
            aria-labelledby="workspace-edit-metadata-heading">
            <h2 id="workspace-edit-metadata-heading">"Modifier la sélection"</h2>
            <p class="hint">"Appliquer valide les champs. Changer de sélection ou de révision recharge ses valeurs enregistrées dans la session."</p>
            <form on:submit=move |event| {
                event.prevent_default();
                let base_revision = actions.state.get_untracked().revision;
                let name = name.get_untracked();
                let command = match selected.get_untracked() {
                    WorkspaceNode::Project => WorkspaceCommand::RenameProject { base_revision, name },
                    WorkspaceNode::Module(module_id) => WorkspaceCommand::RenameModule { base_revision, module_id, name },
                    WorkspaceNode::Area(module_id, area_id) => WorkspaceCommand::UpdateAreaMetadata {
                        base_revision, module_id, area_id, name,
                        spawn_x: x.get_untracked(), spawn_y: y.get_untracked(), spawn_z: z.get_untracked(),
                    },
                };
                actions.send(command);
            }>
                <fieldset disabled=move || actions.unavailable()>
                    <legend class="project-sr-only">"Métadonnées sélectionnées"</legend>
                    <TextField id="workspace-edit-name" label="Nom sélectionné" value=name set_value=set_name/>
                    <Show when=move || matches!(selected.get(), WorkspaceNode::Area(_, _))>
                        <div class="project-spawn-fields">
                            <TextField id="workspace-edit-x" label="Point de départ X" value=x set_value=set_x/>
                            <TextField id="workspace-edit-y" label="Point de départ Y" value=y set_value=set_y/>
                            <TextField id="workspace-edit-z" label="Point de départ Z" value=z set_value=set_z/>
                        </div>
                    </Show>
                    <button id="workspace-apply-metadata" type="submit">"Appliquer les métadonnées"</button>
                    <p class="hint">"Annulable dans les 32 dernières opérations. Enregistrer conserve le résultat sur disque."</p>
                </fieldset>
            </form>
        </section>
    }
}
