//! Presentation relay: the main frontend owns gestures and draft controls.
use crate::{
    StudioCommand, StudioResponse,
    atelier_protocol::{CatalogItem, Intent, Reply, Request, SceneItem, Snapshot},
    authoring_controls::AuthoringTab,
    authoring_scene_index::{self, Target},
};
use forge_project::authoring::AuthoringState;
use leptos::prelude::*;

pub(super) fn requested() -> bool {
    web_sys::window()
        .and_then(|w| w.location().search().ok())
        .is_some_and(|q| {
            q.trim_start_matches('?')
                .split('&')
                .any(|p| p == "atelier=1")
        })
}

pub(super) async fn invoke(request: Request) -> Result<Reply, String> {
    match crate::web::invoke_tauri(&StudioCommand::Atelier {
        request: Box::new(request),
    })
    .await?
    {
        StudioResponse::Atelier { state } => Ok(*state),
        StudioResponse::Rejected { reason } => Err(reason),
        _ => Err("Réponse d’atelier inattendue".into()),
    }
}

pub(super) fn periodic(callback: impl Fn() + 'static) {
    if let Ok(handle) = leptos::leptos_dom::helpers::set_interval_with_handle(
        callback,
        std::time::Duration::from_millis(500),
    ) {
        on_cleanup(move || handle.clear());
    }
}

#[derive(Clone, Copy)]
pub(super) struct Link {
    /// Shared within this main window, including siblings of AuthoringControls.
    pub main: RwSignal<Option<MainBindings>>,
    pub open: RwSignal<bool>,
    pub map_only: RwSignal<bool>,
    pub status: RwSignal<String>,
    pub busy: RwSignal<bool>,
    pub catalog: RwSignal<Vec<CatalogItem>>,
    pub catalog_selected: RwSignal<String>,
    pub presentation: RwSignal<Reply>,
    pub drafts: crate::atelier_inspector::DraftClient,
}

impl Link {
    pub fn new() -> Self {
        Self {
            main: RwSignal::new(None),
            open: RwSignal::new(false),
            map_only: RwSignal::new(false),
            status: RwSignal::new(String::new()),
            busy: RwSignal::new(false),
            catalog: RwSignal::new(Vec::new()),
            catalog_selected: RwSignal::new(String::new()),
            presentation: RwSignal::new(Reply::default()),
            drafts: crate::atelier_inspector::DraftClient::new(),
        }
    }
}

#[component]
pub(super) fn WindowControls(link: Link) -> impl IntoView {
    view! {
        <div class="forge-window-controls">
            <button disabled=move ||link.busy.get() title="Ouvrir l’atelier sur un second écran, ou dans une fenêtre séparée"
                on:click=move |_| {
                    link.busy.set(true);
                    wasm_bindgen_futures::spawn_local(async move {
                        let request=if link.open.get_untracked() {Request::Repatriate} else {Request::Open};
                        match invoke(request).await {Ok(reply)=>{link.open.set(reply.open);link.status.set(String::new());},Err(error)=>link.status.set(error)}
                        link.busy.set(false);
                    });
                }>{move ||if link.open.get() {"Rapatrier"} else {"Deux écrans"}}</button>
            <button class="forge-map-mode" hidden=move || !link.open.get()
                aria-pressed=move || link.map_only.get().to_string()
                on:click=move |_| link.map_only.update(|value| *value = !*value)>
                {move || if link.map_only.get() { "Afficher les panneaux" } else { "Carte seule" }}
            </button>
            <output aria-live="polite">{move ||link.status.get()}</output>
        </div>
    }
}

#[derive(Clone, Copy)]
pub(super) struct MainBindings {
    pub state: RwSignal<Option<AuthoringState>>,
    pub status: RwSignal<String>,
    pub active: RwSignal<AuthoringTab>,
    pub preview: RwSignal<Option<forge_project::authoring::AuthoringRequest>>,
    pub path: RwSignal<String>,
    pub river: RwSignal<String>,
    pub building: RwSignal<String>,
    pub vegetation: RwSignal<String>,
    pub placement: RwSignal<Option<forge_core::EntityId>>,
    pub busy: RwSignal<bool>,
    pub send: Callback<Result<forge_project::authoring::AuthoringRequest, String>>,
}

fn item(target: &Target, label: String) -> SceneItem {
    let topic = match target {
        Target::Path(_) => 3,
        Target::River(_) => 4,
        Target::Building(_) => 6,
        Target::Placement(_) => 5,
        Target::Vegetation(_) => 2,
    };
    SceneItem {
        key: format!("{topic}:{}", target.id()),
        label,
        topic,
    }
}

#[path = "atelier_selection.rs"]
mod selected_objects;
use selected_objects::{SCENE_ITEM_LIMIT, preserve_selected_object_item};

#[allow(
    clippy::too_many_lines,
    reason = "The Leptos binding view keeps signal ownership in one scope"
)]
pub(super) fn bind_main(bindings: MainBindings) {
    provide_context(bindings);
    let Some(link) = use_context::<Link>() else {
        return;
    };
    link.main.set(Some(bindings));
    on_cleanup(move || {
        let _ = link.main.try_set(None);
    });
    let in_flight = RwSignal::new(false);
    let previous = RwSignal::new(None::<Snapshot>);
    // Rebuild the compact presentation only when its dependencies change, not
    // twice per second. Never clone the terrain document for a heartbeat.
    let scene = Memo::new(move |_| {
        bindings.state.with(|session| {
            let rows = session
                .as_ref()
                .and_then(|s| s.zone_head.as_ref())
                .map(|h| authoring_scene_index::rows(h, ""))
                .unwrap_or_default();
            let mut items = rows
                .iter()
                .take(SCENE_ITEM_LIMIT)
                .map(|(target, label)| item(target, label.clone()))
                .collect::<Vec<_>>();
            let selected = if let (Some(selected), Some(head)) = (
                bindings.placement.get(),
                session.as_ref().and_then(|s| s.zone_head.as_ref()),
            ) && let Ok(placement) =
                crate::authoring_assets::objects::lookup(head, selected)
            {
                Some((selected, placement.name))
            } else {
                None
            };
            preserve_selected_object_item(&mut items, selected);
            Snapshot {
                project: session
                    .as_ref()
                    .and_then(|s| s.document.as_ref())
                    .map_or_else(|| "Aucune zone ouverte".into(), |d| d.name.clone()),
                dirty: session.as_ref().is_some_and(|s| s.dirty),
                items,
                ..Snapshot::default()
            }
        })
    });
    let presentation = Memo::new(move |_| {
        let mut snapshot = scene.get();
        // Bounded presentation text only; document authority stays in the main host.
        snapshot.command_status = crate::authoring_feedback::atelier_status(
            &bindings.status.get(),
            bindings.busy.get(),
            bindings.preview.with(Option::is_some),
        );
        snapshot.catalog = link.catalog.get();
        let asset = link.catalog_selected.get();
        snapshot.catalog_selected = snapshot
            .catalog
            .iter()
            .any(|a| a.id == asset)
            .then_some(asset);
        let active = bindings.active.get();
        let selected = match active {
            AuthoringTab::Routes => bindings.path.get(),
            AuthoringTab::Water => bindings.river.get(),
            AuthoringTab::Buildings => bindings.building.get(),
            AuthoringTab::Nature => bindings.vegetation.get(),
            AuthoringTab::Objects => bindings
                .placement
                .get()
                .map(|id| id.to_string())
                .unwrap_or_default(),
            _ => String::new(),
        };
        let selected_key = format!("{}:{selected}", active.index());
        snapshot.topic = active as u8;
        snapshot.map_only = link.map_only.get();
        snapshot.selected = snapshot
            .items
            .iter()
            .any(|i| i.key == selected_key)
            .then_some(selected_key);
        snapshot.inspector = inspector_snapshot(bindings, active);
        snapshot
    });
    periodic(move || {
        if in_flight.get_untracked() || link.busy.get_untracked() {
            return;
        }
        let snapshot = presentation.get_untracked();
        let request = if previous.get_untracked().as_ref() == Some(&snapshot) {
            Request::Read
        } else {
            Request::Publish {
                snapshot: snapshot.clone(),
            }
        };
        in_flight.set(true);
        wasm_bindgen_futures::spawn_local(async move {
            match invoke(request).await {
                Ok(reply) => {
                    previous.set(Some(snapshot));
                    link.open.set(reply.open);
                    if !reply.open {
                        link.map_only.set(false);
                    }
                    if link.drafts.has_newer(&reply.drafts) {
                        crate::authoring_shortcuts::cancel_preview();
                    }
                    link.drafts.observe(&reply.drafts);
                    if link.presentation.with_untracked(|p| p != &reply) {
                        link.presentation.set(reply.clone());
                    }
                    for intent in reply.pending {
                        apply_intent(bindings, link, intent);
                    }
                }
                Err(error) => link.status.set(error),
            }
            in_flight.set(false);
        });
    });
}

#[component]
pub(super) fn MainInspector() -> impl IntoView {
    let link = use_context::<Link>();
    let bindings = use_context::<MainBindings>();
    match (link,bindings) {
        (Some(link),Some(bindings))=>view! {
                <crate::atelier_inspector::Inspector reply=link.presentation client=link.drafts submit=Callback::new(move |intent|apply_intent(bindings,link,intent))/>
        }.into_any(),
        _=>().into_any(),
    }
}

/// Category symbols, never presented as thumbnails of the governed GLB.
pub(super) fn asset_glyph(category: &str) -> impl IntoView + use<> {
    let (class, path) = match category {
        "tree" | "Arbres végétation" => (
            "atelier-glyph-tree",
            "M11 22v-6H5l3-4H4l5-6H7l5-5 5 5h-2l5 6h-4l3 4h-6v6Z",
        ),
        "rock" | "Rochers" => (
            "atelier-glyph-rock",
            "M2 17 6 7l8-3 7 7 1 7-10 3Z M6 7l6 7 9-3 M12 14v7 M2 17l10-3",
        ),
        "architecture" | "Architecture" => (
            "atelier-glyph-architecture",
            "M3 22V10a9 9 0 0 1 18 0v12h-5V10a4 4 0 0 0-8 0v12Z M3 13h5 M16 13h5 M3 18h5 M16 18h5 M5 5l4 3 M19 5l-4 3 M12 1v5",
        ),
        _ => (
            "atelier-glyph-object",
            "m12 2 9 5v10l-9 5-9-5V7Z M3 7l9 5 9-5 M12 12v10",
        ),
    };
    view! {<svg class=class viewBox="0 0 24 24" aria-hidden="true"><path d=path/></svg>}
}

fn inspector_snapshot(
    bindings: MainBindings,
    active: AuthoringTab,
) -> Option<crate::atelier_drafts::ObjectInspector> {
    if active != AuthoringTab::Objects {
        return None;
    }
    bindings.state.with(|session| {
        let session = session.as_ref()?;
        let head = session.zone_head.as_ref()?;
        let selected = bindings.placement.get()?;
        let placement = crate::authoring_assets::objects::lookup(head, selected).ok()?;
        Some(crate::atelier_drafts::ObjectInspector {
            scope: crate::atelier_drafts::DraftScope {
                area: head.base.terrain.area_id.to_string(),
                object: placement.id.to_string(),
                base_hash: head.content_hash().ok()?.to_string(),
            },
            label: placement.name.clone(),
            locked: placement.locked || session.playtest_hash.is_some() || bindings.busy.get(),
            accepted: crate::atelier_transform::TransformFields::from_transform(
                placement.transform,
            ),
        })
    })
}

fn apply_intent(bindings: MainBindings, link: Link, intent: Intent) {
    if !matches!(&intent, Intent::MapOnly { .. }) {
        crate::authoring_brush_pointer::cancel();
    }
    match intent {
        Intent::PreviewTransform { draft } => {
            if bindings.busy.get_untracked() {
                bindings
                    .status
                    .set("L’éditeur traite déjà une commande. Réessayez l’aperçu.".into());
                return;
            }
            let request = bindings.state.with_untracked(|session| {
                crate::atelier_transform::prepare_preview(session.as_ref(), &draft)
            });
            bindings.send.run(request);
        }
        Intent::CatalogSelect { id } => {
            if link
                .catalog
                .with_untracked(|items| items.iter().any(|item| item.id == id))
            {
                crate::authoring_assets::cancel_preview();
                link.catalog_selected.set(id);
                bindings.active.set(AuthoringTab::Objects);
            }
        }
        Intent::Topic { index } => {
            if let Some(tab) = AuthoringTab::ALL.get(usize::from(index)) {
                bindings.active.set(*tab);
            }
        }
        Intent::MapOnly { enabled } => link.map_only.set(enabled),
        Intent::Select { key } => {
            // Revalidate after the await: the main window may have changed zones.
            let head = bindings.state.get_untracked().and_then(|s| s.zone_head);
            let rows = head
                .as_ref()
                .map(|head| authoring_scene_index::rows(head, ""))
                .unwrap_or_default();
            if let Some((target, _)) = rows.iter().find(|(t, l)| item(t, l.clone()).key == key) {
                crate::authoring_shortcuts::cancel_preview();
                match target {
                    Target::Path(id) => {
                        bindings.path.set(id.to_string());
                        bindings.active.set(AuthoringTab::Routes);
                    }
                    Target::River(id) => {
                        bindings.river.set(id.to_string());
                        bindings.active.set(AuthoringTab::Water);
                    }
                    Target::Building(id) => {
                        bindings.building.set(id.to_string());
                        bindings.active.set(AuthoringTab::Buildings);
                    }
                    Target::Vegetation(id) => {
                        bindings.vegetation.set(id.to_string());
                        bindings.active.set(AuthoringTab::Nature);
                    }
                    Target::Placement(id) => {
                        crate::authoring_assets::placements::selection::choose(Some(*id));
                        bindings.active.set(AuthoringTab::Objects);
                    }
                }
            } else if let Some(id) = selected_objects::resolve_object_key(&key, |id| {
                head.as_ref()
                    .is_some_and(|head| crate::authoring_assets::objects::lookup(head, id).is_ok())
            }) {
                crate::authoring_shortcuts::cancel_preview();
                crate::authoring_assets::placements::selection::choose(Some(id));
                bindings.active.set(AuthoringTab::Objects);
            }
        }
    }
}

#[component]
pub(super) fn SecondaryWorkspace() -> impl IntoView {
    let reply = RwSignal::new(Reply::default());
    let status = RwSignal::new("Connexion à l’atelier principal…".to_owned());
    // A successful background read must not erase an action refusal.
    let action_status = RwSignal::new(String::new());
    let busy = RwSignal::new(false);
    let filter = RwSignal::new(String::new());
    let asset_filter = RwSignal::new(String::new());
    let drafts = crate::atelier_inspector::DraftClient::new();
    let close_listener = window_event_listener(
        leptos::ev::Custom::new("forge-atelier-close-requested"),
        move |_: web_sys::Event| drafts.close(),
    );
    on_cleanup(move || close_listener.remove());
    periodic(move || {
        if busy.get_untracked() {
            return;
        }
        busy.set(true);
        wasm_bindgen_futures::spawn_local(async move {
            match invoke(Request::Read).await {
                Ok(state) => {
                    drafts.observe(&state.drafts);
                    if reply.with_untracked(|previous| previous != &state) {
                        reply.set(state);
                    }
                    if !status.with_untracked(String::is_empty) {
                        status.set(String::new());
                    }
                }
                Err(error) => status.set(error),
            }
            busy.set(false);
        });
    });
    let submit = Callback::new(move |intent| {
        let revision = reply.get_untracked().revision;
        wasm_bindgen_futures::spawn_local(async move {
            match invoke(Request::Submit { revision, intent }).await {
                Ok(_) => action_status.set(String::new()),
                Err(error) => action_status.set(error),
            }
        });
    });
    view! {
        <main class="authoring-shell atelier-secondary">
            <style>{include_str!("authoring_gold.css")}</style>
            <header class="topbar"><h1>"OpenRPG Forge"</h1><span>"Atelier secondaire"</span>
                <crate::asset_library_ui::LibraryLauncher/>
                <button class="atelier-map-mode" aria-pressed=move || reply.get().snapshot.map_only.to_string()
                    on:click=move |_|submit.run(Intent::MapOnly { enabled: !reply.get_untracked().snapshot.map_only })>
                    {move || if reply.get().snapshot.map_only { "Afficher les panneaux sur la carte" } else { "Carte seule sur l’écran principal" }}
                </button>
                <button on:click=move |_| drafts.close()>"Fermer le deuxième écran"</button>
            </header>
            <div class="atelier-secondary-body">
                <aside class="atelier-secondary-scene">
                    <span class="atelier-eyebrow">"PROJET LIÉ"</span><h2>{move ||reply.get().snapshot.project}</h2>
                    <label>"Rechercher dans la scène"<input prop:value=move ||filter.get() on:input=move |e|filter.set(event_target_value(&e))/></label>
                    <ul>{move || {let snapshot=reply.get().snapshot;let query=filter.get().to_lowercase();snapshot.items.into_iter().filter(|i|i.label.to_lowercase().contains(&query)).map(|item| {
                        let key=item.key.clone();let selected=key.clone();
                        view! {<li><button aria-pressed=move ||(reply.get().snapshot.selected.as_ref()==Some(&selected)).to_string()
                            on:click=move |_|submit.run(Intent::Select {key:key.clone()})>{item.label}</button></li>}
                    }).collect_view()}}</ul>
                </aside>
                <section class="atelier-secondary-tools"><span class="atelier-eyebrow">"ESPACES DE TRAVAIL"</span><h2>"Un monde, deux écrans."</h2>
                    <p>"Choisissez un sujet ici : ses réglages s’ouvrent dans le panneau latéral de la carte. Le bouton Carte seule peut masquer ou réafficher ces panneaux."</p>
                    <div class="atelier-topic-grid">{AuthoringTab::ALL.into_iter().map(move |tab|view! {
                        <button aria-pressed=move ||(reply.get().snapshot.topic==tab as u8).to_string() on:click=move |_|submit.run(Intent::Topic {index:tab as u8})><span>{tab.label()}</span><small>"Ouvrir le panneau"</small></button>
                    }).collect_view()}</div>
                    <h2 class="atelier-library-heading">"Bibliothèque partagée"</h2>
                    <label>"Rechercher un objet"<input placeholder="Nom ou catégorie…" prop:value=move ||asset_filter.get() on:input=move |e|asset_filter.set(event_target_value(&e))/></label>
                    <div class="atelier-detached-assets">{move || {
                        let snapshot=reply.get().snapshot;let query=asset_filter.get().to_lowercase();
                        snapshot.catalog.into_iter().filter(|a|format!("{} {}",a.id,a.category).to_lowercase().contains(&query)).map(|asset| {
                            let id=asset.id.clone();let selected=id.clone();
                            view! {<button aria-pressed=move ||(reply.get().snapshot.catalog_selected.as_ref()==Some(&selected)).to_string()
                                on:click=move |_|submit.run(Intent::CatalogSelect {id:id.clone()})>
                                {asset_glyph(&asset.category)}
                                <strong>{asset.id}</strong><small>{asset.category}</small>
                            </button>}
                        }).collect_view()
                    }}</div>
                    <p class="atelier-stage-note">"Choisissez un asset ici, puis placez-le dans la carte principale. Pour retoucher un objet déjà placé, sélectionnez-le dans la scène à gauche. Les pictogrammes ne sont pas des aperçus des modèles."</p>
                </section>
                <aside class="atelier-secondary-inspector"><span class="atelier-eyebrow">"SÉLECTION PARTAGÉE"</span>
                    <h2>{move || {let s=reply.get().snapshot;s.items.iter().find(|i|Some(&i.key)==s.selected.as_ref()).map_or_else(||"Aucun objet sélectionné".into(), |i|i.label.clone())}}</h2>
                    <crate::atelier_inspector::Inspector reply=reply client=drafts submit=submit/>
                    <output>{move ||if reply.get().snapshot.dirty {"Modifications à enregistrer"} else {"Aucune retouche non enregistrée"}}</output>
                </aside>
            </div>
            <footer class="atelier-secondary-status" aria-live="polite"><span>{move ||status.get()}</span><span>{move ||action_status.get()}</span><span>{move ||reply.get().snapshot.command_status}</span></footer>
        </main>
    }
}
