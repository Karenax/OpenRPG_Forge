//! Presentation-only desktop. Windows retain their components; all source operations still use
//! `WorkspaceActions` and the native `WorkspaceSession`. No layout state enters the project document.

use crate::desktop_state::{DesktopApp, DesktopState, WindowRect};
use crate::workspace_api::WorkspaceCommand;
use crate::workspace_ui::{ProjectWorkspace, WorkspaceActions, create_workspace_actions};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, closure::Closure};

#[derive(Clone, Copy, PartialEq)]
enum Menu {
    Forge,
    File,
    Edit,
    Window,
    Help,
}

#[derive(Clone, Copy)]
struct Drag {
    app: DesktopApp,
    pointer: i32,
    x: f64,
    y: f64,
    rect: WindowRect,
    resize: bool,
}

#[derive(Clone, Copy)]
struct DesktopControls {
    layout: RwSignal<DesktopState>,
    menu: RwSignal<Option<Menu>>,
    drag: RwSignal<Option<Drag>>,
    diagnostics_mounted: RwSignal<bool>,
    message: RwSignal<String>,
    workspace: WorkspaceActions,
}

impl DesktopControls {
    fn open(self, app: DesktopApp) {
        self.menu.set(None);
        self.layout.update(|layout| layout.open(app));
        if app == DesktopApp::Diagnostics {
            self.diagnostics_mounted.set(true);
        }
        focus_element(title_id(app));
    }

    fn project_field(self, id: &'static str) {
        self.open(DesktopApp::Projects);
        focus_element(id);
    }

    fn hide(self, app: DesktopApp, minimize: bool) {
        self.menu.set(None);
        self.drag.set(None);
        self.layout.update(|layout| {
            if minimize {
                layout.minimize(app);
            } else {
                layout.close(app);
            }
        });
        self.message.set(
            "Fenêtre masquée. Le projet et les champs saisis sont conservés dans cette session."
                .into(),
        );
        focus_element(dock_id(app));
    }

    fn undo(self, redo: bool) {
        self.menu.set(None);
        let state = self.workspace.state.get_untracked();
        if self.workspace.unavailable()
            || if redo {
                !state.can_redo
            } else {
                !state.can_undo
            }
        {
            return;
        }
        self.workspace.send(if redo {
            WorkspaceCommand::Redo {
                base_revision: state.revision,
            }
        } else {
            WorkspaceCommand::Undo {
                base_revision: state.revision,
            }
        });
    }

    fn begin_drag(self, app: DesktopApp, resize: bool, event: &web_sys::PointerEvent) {
        if event.button() != 0 || self.layout.get_untracked().window(app).maximized {
            return;
        }
        let Some(element) = event
            .current_target()
            .and_then(|target| target.dyn_into::<web_sys::Element>().ok())
        else {
            return;
        };
        if element.set_pointer_capture(event.pointer_id()).is_err() {
            return;
        }
        event.prevent_default();
        self.layout.update(|layout| layout.focus(app));
        self.menu.set(None);
        self.drag.set(Some(Drag {
            app,
            pointer: event.pointer_id(),
            x: f64::from(event.client_x()),
            y: f64::from(event.client_y()),
            rect: self.layout.get_untracked().window(app).rect,
            resize,
        }));
    }

    fn drag_to(self, event: &web_sys::PointerEvent) {
        let Some(drag) = self
            .drag
            .get_untracked()
            .filter(|drag| drag.pointer == event.pointer_id())
        else {
            return;
        };
        let dx = f64::from(event.client_x()) - drag.x;
        let dy = f64::from(event.client_y()) - drag.y;
        self.layout.update(|layout| {
            if drag.resize {
                layout.resize_window(drag.app, drag.rect.width + dx, drag.rect.height + dy);
            } else {
                layout.move_window(drag.app, drag.rect.x + dx, drag.rect.y + dy);
            }
        });
    }
}

pub(crate) fn mount_desktop() {
    mount_to_body(|| view! { <ForgeDesktop/> });
}

#[component]
fn ForgeDesktop() -> impl IntoView {
    let (width, height) = desktop_size();
    let controls = DesktopControls {
        layout: RwSignal::new(DesktopState::new(width, height)),
        menu: RwSignal::new(None),
        drag: RwSignal::new(None),
        diagnostics_mounted: RwSignal::new(false),
        message: RwSignal::new("Vos outils, dans un même atelier.".into()),
        workspace: create_workspace_actions(),
    };
    install_desktop_listeners(controls);
    view! {
        <main class="forge-desktop" data-forge-mode="desktop" on:pointermove=move |event| controls.drag_to(&event)
            on:pointerup=move |_| controls.drag.set(None) on:pointercancel=move |_| controls.drag.set(None)>
            <header class="desktop-menubar" aria-label="Menus Forge">
                <span class="desktop-mark" aria-hidden="true">"◇"</span>
                <DesktopMenu controls menu=Menu::Forge label="Forge"/>
                <span class="desktop-active-app">{move || controls.layout.get().active.map_or("Bureau", app_title)}</span>
                <DesktopMenu controls menu=Menu::File label="Fichier"/>
                <DesktopMenu controls menu=Menu::Edit label="Édition"/>
                <DesktopMenu controls menu=Menu::Window label="Fenêtre"/>
                <DesktopMenu controls menu=Menu::Help label="Aide"/>
                <span class="desktop-status" id="desktop-session-status">{move || {
                    let state = controls.workspace.state.get();
                    if controls.workspace.pending.get() { "Opération en cours…" }
                    else if state.dirty { "● Brouillon non enregistré" }
                    else if state.document_json.is_some() { "● Projet enregistré" }
                    else { "Studio · Gate 2" }
                }}</span>
            </header>
            <div class="desktop-surface" on:pointerdown=move |_| controls.menu.set(None)>
                <section class="desktop-welcome" aria-label="Bureau Forge">
                    <span class="eyebrow">"FORGE STUDIO"</span>
                    <h1>"Votre prochaine aventure."</h1>
                    <p>"Un monde à imaginer. Un atelier pour lui donner vie."</p>
                </section>
                <DesktopWindow controls app=DesktopApp::Projects>
                    <ProjectWorkspace actions=controls.workspace embedded=true/>
                </DesktopWindow>
                // This condition becomes true only once. Visibility is controlled by the outer
                // window; closing or minimizing never destroys the renderer or the form signals.
                <Show when=move || controls.diagnostics_mounted.get()>
                    <DesktopWindow controls app=DesktopApp::Diagnostics>
                        <section class="desktop-proof-notice">
                            <strong>"Laboratoire · scène Gate 0"</strong>
                            <span>"Démonstration technique indépendante. Ce n’est pas la zone de votre projet."</span>
                        </section>
                        <crate::web::Studio embedded=true/>
                    </DesktopWindow>
                </Show>
            </div>
            <div class="desktop-announcement" role="status" aria-live="polite">{move || controls.message.get()}</div>
            <nav class="desktop-dock" aria-label="Applications Forge">
                <DockButton controls app=DesktopApp::Projects/>
                <FutureApp label="Monde" kind="world"/>
                <FutureApp label="Assets" kind="assets"/>
                <FutureApp label="Personnages" kind="characters"/>
                <FutureApp label="Narration" kind="narration"/>
                <FutureApp label="Playtest" kind="playtest"/>
                <span class="dock-divider" aria-hidden="true"></span>
                <DockButton controls app=DesktopApp::Diagnostics/>
            </nav>
            <output id="desktop-window-ledger" hidden>{move || {
                let layout = controls.layout.get();
                let describe = |app| {
                    let state = layout.window(app);
                    serde_json::json!({ "open": state.open, "minimized": state.minimized, "maximized": state.maximized,
                        "x": state.rect.x, "y": state.rect.y, "width": state.rect.width, "height": state.rect.height })
                };
                serde_json::json!({ "projects": describe(DesktopApp::Projects), "diagnostics": describe(DesktopApp::Diagnostics),
                    "active": layout.active.map(app_key), "diagnostics_mounted": controls.diagnostics_mounted.get() }).to_string()
            }}</output>
        </main>
    }
}

#[component]
fn DesktopWindow(controls: DesktopControls, app: DesktopApp, children: Children) -> impl IntoView {
    let visible = move || {
        let state = controls.layout.get().window(app);
        state.open && !state.minimized
    };
    view! {
        <section class="desktop-window" data-app=app_key(app) class:is-hidden=move || !visible()
            class:is-active=move || controls.layout.get().active == Some(app)
            inert=move || !visible() aria-hidden=move || (!visible()).to_string()
            aria-labelledby=title_id(app) style=move || {
                let layout = controls.layout.get();
                let rect = layout.window(app).rect;
                format!("left:{}px;top:{}px;width:{}px;height:{}px;z-index:{}", rect.x, rect.y, rect.width, rect.height, if layout.active == Some(app) { 4 } else { 3 })
            }
            on:pointerdown=move |_| controls.layout.update(|layout| layout.focus(app))
            on:focusin=move |_| controls.layout.update(|layout| layout.focus(app))>
            <header class="window-titlebar">
                <div class="window-controls">
                    <button class="window-control close" type="button" aria-label=format!("Masquer {}", app_title(app))
                        title="Masquer la fenêtre sans fermer le projet" on:click=move |_| controls.hide(app, false)>"×"</button>
                    <button class="window-control minimize" type="button" aria-label=format!("Réduire {}", app_title(app))
                        title="Réduire dans le dock" on:click=move |_| controls.hide(app, true)>"−"</button>
                    <button class="window-control maximize" type="button" aria-label=format!("Agrandir ou restaurer {}", app_title(app))
                        title="Agrandir ou restaurer" on:click=move |_| controls.layout.update(|layout| layout.toggle_maximize(app))>"+"</button>
                </div>
                <button class="window-title" id=title_id(app) type="button"
                    title="Déplacer : glisser ou utiliser les flèches. Double-clic : agrandir."
                    on:pointerdown=move |event| controls.begin_drag(app, false, &event)
                    on:lostpointercapture=move |_| controls.drag.set(None)
                    on:dblclick=move |_| controls.layout.update(|layout| layout.toggle_maximize(app))
                    on:keydown=move |event| geometry_key(controls, app, false, &event)>{app_title(app)}</button>
                <span class="window-caption">{if app == DesktopApp::Projects { "Sources du projet" } else { "Scène de preuve" }}</span>
            </header>
            <div class="window-content">{children()}</div>
            <button class="window-resize-handle" type="button" aria-label=format!("Redimensionner {} avec les flèches", app_title(app))
                title="Redimensionner : glisser ou utiliser les flèches"
                disabled=move || controls.layout.get().window(app).maximized
                on:pointerdown=move |event| controls.begin_drag(app, true, &event)
                on:lostpointercapture=move |_| controls.drag.set(None)
                on:keydown=move |event| geometry_key(controls, app, true, &event)>"◢"</button>
        </section>
    }
}

#[component]
fn DesktopMenu(controls: DesktopControls, menu: Menu, label: &'static str) -> impl IntoView {
    let open = move || controls.menu.get() == Some(menu);
    view! {
        <div class="desktop-menu">
            <button class="desktop-menu-trigger" type="button" aria-expanded=move || open().to_string()
                on:click=move |_| controls.menu.set(if open() { None } else { Some(menu) })>{label}</button>
            <Show when=open>
                <div class="desktop-menu-panel" aria-label=label>
                    {match menu {
                        Menu::Forge => view! {
                            <button class="desktop-menu-item" on:click=move |_| controls.open(DesktopApp::Projects)>"Projets"</button>
                            <button class="desktop-menu-item" on:click=move |_| controls.open(DesktopApp::Diagnostics)>"Diagnostics · preuve Gate 0"</button>
                            <p class="desktop-menu-note">"Un seul Studio, une seule session de projet."</p>
                        }.into_any(),
                        Menu::File => view! {
                            <button class="desktop-menu-item" disabled=move || controls.workspace.unavailable() || controls.workspace.state.get().document_json.is_some()
                                on:click=move |_| controls.project_field("workspace-project-name")>"Nouveau projet…"</button>
                            <button class="desktop-menu-item" disabled=move || controls.workspace.unavailable() || controls.workspace.state.get().dirty
                                on:click=move |_| controls.project_field("workspace-open-path")>"Ouvrir un projet…"</button>
                            <button class="desktop-menu-item" disabled=move || controls.workspace.unavailable() || controls.workspace.state.get().document_json.is_none()
                                on:click=move |_| controls.project_field("workspace-save-directory")>"Enregistrer sous…" <kbd>"Ctrl S"</kbd></button>
                            <hr class="desktop-menu-separator"/>
                            <button class="desktop-menu-item" disabled=move || controls.workspace.unavailable() || controls.workspace.state.get().dirty || controls.workspace.state.get().document_json.is_none()
                                on:click=move |_| { controls.menu.set(None); controls.workspace.send(WorkspaceCommand::Close); }>"Fermer le projet enregistré"</button>
                        }.into_any(),
                        Menu::Edit => view! {
                            <button class="desktop-menu-item" disabled=move || controls.layout.get().active != Some(DesktopApp::Projects) || controls.workspace.unavailable() || !controls.workspace.state.get().can_undo
                                on:click=move |_| controls.undo(false)>"Annuler l’opération" <kbd>"Ctrl Z"</kbd></button>
                            <button class="desktop-menu-item" disabled=move || controls.layout.get().active != Some(DesktopApp::Projects) || controls.workspace.unavailable() || !controls.workspace.state.get().can_redo
                                on:click=move |_| controls.undo(true)>"Rétablir l’opération" <kbd>"Ctrl Maj Z"</kbd></button>
                            <p class="desktop-menu-note">"Dans un champ, Ctrl Z reste l’annulation du texte."</p>
                        }.into_any(),
                        Menu::Window => view! {
                            <button class="desktop-menu-item" on:click=move |_| controls.open(DesktopApp::Projects)>"Afficher Projets"</button>
                            <button class="desktop-menu-item" on:click=move |_| controls.open(DesktopApp::Diagnostics)>"Afficher Diagnostics"</button>
                            <hr class="desktop-menu-separator"/>
                            <button class="desktop-menu-item" disabled=move || controls.layout.get().active.is_none() on:click=move |_| {
                                if let Some(app) = controls.layout.get_untracked().active { controls.hide(app, true); }
                            }>"Réduire la fenêtre active"</button>
                            <button class="desktop-menu-item" disabled=move || controls.layout.get().active.is_none() on:click=move |_| {
                                if let Some(app) = controls.layout.get_untracked().active { controls.layout.update(|layout| layout.toggle_maximize(app)); }
                                controls.menu.set(None);
                            }>"Agrandir / restaurer"</button>
                        }.into_any(),
                        Menu::Help => view! {
                            <p class="desktop-menu-note">"Projets crée, enregistre et rouvre les sources. Diagnostics conserve une scène de preuve indépendante."</p>
                            <p class="desktop-menu-note">"Monde, Assets, Personnages, Narration et Playtest ne sont pas encore disponibles."</p>
                            <p class="desktop-menu-note">"Les fenêtres conservent les saisies pendant cette session. Enregistrez le projet avant de quitter le Studio."</p>
                        }.into_any(),
                    }}
                </div>
            </Show>
        </div>
    }
}

#[component]
fn DockButton(controls: DesktopControls, app: DesktopApp) -> impl IntoView {
    view! {
        <button id=dock_id(app) class="dock-item" type="button" aria-label=format!("Ouvrir {}", app_title(app))
            class:is-running=move || controls.layout.get().window(app).open
            aria-pressed=move || (controls.layout.get().active == Some(app)).to_string()
            on:click=move |_| controls.open(app)>
            <AppIcon kind=app_key(app)/><span class="dock-tooltip">{app_title(app)}</span>
        </button>
    }
}

#[component]
fn FutureApp(label: &'static str, kind: &'static str) -> impl IntoView {
    view! {
        <button class="dock-item is-unavailable" type="button" aria-disabled="true"
            aria-label=format!("{label} · indisponible pour le moment") title=format!("{label} · à venir")>
            <AppIcon kind/><span class="dock-tooltip">{label}" · à venir"</span>
        </button>
    }
}

/// Small original vectors, not assets or icons extracted from the reference site.
#[component]
fn AppIcon(kind: &'static str) -> impl IntoView {
    let path = match kind {
        "projects" => "M3 7h7l2 2h9v11H3z M3 7V4h7l2 3",
        "world" => "M2 19 9 5l5 10 3-6 5 10z M7 9l2 2 2-2",
        "assets" => "m12 3 9 5-9 5-9-5z M3 8v10l9 5 9-5V8 M12 13v10",
        "characters" => "M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0 M4 22v-3a8 8 0 0 1 16 0v3",
        "narration" => "M12 6Q7 2 2 5v15q5-3 10 1 5-4 10-1V5q-5-3-10 1v15",
        "playtest" => "m8 4 13 8-13 8z",
        _ => "M3 4h18v16H3z M5 14h3l3-6 3 9 3-5h2",
    };
    view! { <span class=format!("app-icon {kind}")><svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d=path/></svg></span> }
}

fn app_title(app: DesktopApp) -> &'static str {
    match app {
        DesktopApp::Projects => "Projets",
        DesktopApp::Diagnostics => "Diagnostics",
    }
}
fn app_key(app: DesktopApp) -> &'static str {
    match app {
        DesktopApp::Projects => "projects",
        DesktopApp::Diagnostics => "diagnostics",
    }
}
fn title_id(app: DesktopApp) -> &'static str {
    match app {
        DesktopApp::Projects => "window-title-projects",
        DesktopApp::Diagnostics => "window-title-diagnostics",
    }
}
fn dock_id(app: DesktopApp) -> &'static str {
    match app {
        DesktopApp::Projects => "dock-projects",
        DesktopApp::Diagnostics => "dock-diagnostics",
    }
}

fn desktop_size() -> (f64, f64) {
    let Some(window) = web_sys::window() else {
        return (1280.0, 656.0);
    };
    (
        window
            .inner_width()
            .ok()
            .and_then(|value| value.as_f64())
            .unwrap_or(1280.0),
        (window
            .inner_height()
            .ok()
            .and_then(|value| value.as_f64())
            .unwrap_or(800.0)
            - 144.0)
            .max(1.0),
    )
}

fn install_desktop_listeners(controls: DesktopControls) {
    let listener = Closure::<dyn FnMut()>::new(move || {
        let (width, height) = desktop_size();
        controls.drag.set(None);
        controls
            .layout
            .update(|layout| layout.resize_desktop(width, height));
    });
    if let Some(window) = web_sys::window()
        && window
            .add_event_listener_with_callback("resize", listener.as_ref().unchecked_ref())
            .is_ok()
    {
        // The single desktop owns the full page lifetime, like the existing Studio listeners.
        listener.forget();
    }
    // A completed command may disable its submit button and leave focus on the document body.
    // Listen on the window, not just the desktop element, so shortcuts still work in that state.
    let keyboard = Closure::<dyn FnMut(web_sys::KeyboardEvent)>::new(move |event| {
        desktop_key(controls, &event);
    });
    if let Some(window) = web_sys::window()
        && window
            .add_event_listener_with_callback("keydown", keyboard.as_ref().unchecked_ref())
            .is_ok()
    {
        keyboard.forget();
    }
}

fn focus_element(id: &'static str) {
    let callback = Closure::once_into_js(move || {
        if let Some(element) = web_sys::window()
            .and_then(|window| window.document())
            .and_then(|document| document.get_element_by_id(id))
            .and_then(|element| element.dyn_into::<web_sys::HtmlElement>().ok())
        {
            let _ = element.focus();
        }
    });
    if let Some(window) = web_sys::window() {
        let _ = window.request_animation_frame(callback.unchecked_ref());
    }
}

fn geometry_key(
    controls: DesktopControls,
    app: DesktopApp,
    resize: bool,
    event: &web_sys::KeyboardEvent,
) {
    let (dx, dy) = match event.key().as_str() {
        "ArrowLeft" => (-16.0, 0.0),
        "ArrowRight" => (16.0, 0.0),
        "ArrowUp" => (0.0, -16.0),
        "ArrowDown" => (0.0, 16.0),
        _ => return,
    };
    event.prevent_default();
    event.stop_propagation();
    controls.layout.update(|layout| {
        let rect = layout.window(app).rect;
        if resize {
            layout.resize_window(app, rect.width + dx, rect.height + dy);
        } else {
            layout.move_window(app, rect.x + dx, rect.y + dy);
        }
    });
}

fn desktop_key(controls: DesktopControls, event: &web_sys::KeyboardEvent) {
    if event.key() == "Escape" {
        controls.menu.set(None);
        controls.drag.set(None);
        return;
    }
    if !(event.ctrl_key() || event.meta_key()) || event.alt_key() || event.is_composing() {
        return;
    }
    let editable = event
        .target()
        .and_then(|target| target.dyn_into::<web_sys::Element>().ok())
        .is_some_and(|element| {
            element
                .closest("input, textarea, select, [contenteditable]")
                .ok()
                .flatten()
                .is_some()
        });
    match event.key().to_lowercase().as_str() {
        "s" => {
            event.prevent_default();
            controls.project_field("workspace-save-directory");
        }
        "z" if !editable
            && controls.layout.get_untracked().active == Some(DesktopApp::Projects) =>
        {
            event.prevent_default();
            controls.undo(event.shift_key());
        }
        _ => {}
    }
}
