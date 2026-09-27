//! Docking changes presentation only. Mounted controls retain draft state.
use super::{
    authoring_controls::AuthoringTab,
    panel_layout::{Dock, PanelPlacement, decode_preferences, encode_preferences},
};
use leptos::prelude::*;
use wasm_bindgen::JsCast;

const PREFERENCE_KEY: &str = "forge.atelier.panels.v1";

fn body_top() -> f64 {
    web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.query_selector(".authoring-body").ok().flatten())
        .map_or(64.0, |body| body.get_bounding_client_rect().top())
}

fn surface() -> (f64, f64) {
    let window = web_sys::window();
    let width = window
        .as_ref()
        .and_then(|w| w.inner_width().ok())
        .and_then(|v| v.as_f64())
        .unwrap_or(1100.0);
    let height = window
        .as_ref()
        .and_then(|w| w.inner_height().ok())
        .and_then(|v| v.as_f64())
        .unwrap_or(700.0);
    (width, height - body_top())
}

fn preferences_store() -> Option<web_sys::Storage> {
    web_sys::window()?.local_storage().ok().flatten()
}

#[derive(Clone, Copy)]
pub(super) struct PanelLayout {
    placements: RwSignal<[PanelPlacement; 8]>,
    active: RwSignal<AuthoringTab>,
}

impl PanelLayout {
    pub(super) fn new(active: RwSignal<AuthoringTab>) -> Self {
        let (width, height) = surface();
        let restored = preferences_store()
            .and_then(|s| s.get_item(PREFERENCE_KEY).ok().flatten())
            .and_then(|json| decode_preferences(&json, width, height))
            .unwrap_or([PanelPlacement::default(); 8]);
        let layout = Self {
            placements: RwSignal::new(restored),
            active,
        };
        let resize = window_event_listener(leptos::ev::resize, move |_| {
            let (width, height) = surface();
            layout.placements.update(|panels| {
                for panel in panels {
                    panel.constrain(width, height);
                }
            });
        });
        on_cleanup(move || resize.remove());
        layout
    }
    fn save(self) {
        if let Some(store) = preferences_store()
            && let Ok(json) = encode_preferences(self.placements.get_untracked())
            && store.set_item(PREFERENCE_KEY, &json).is_err()
        {
            web_sys::console::warn_1(
                &"Disposition non mémorisée : stockage local indisponible".into(),
            );
        }
    }
    pub(super) fn current(self) -> PanelPlacement {
        self.placements.get()[self.active.get().index()].for_surface(surface().1)
    }
    fn bottom_available(self) -> bool {
        // Resize updates placements, making this condition reactive as well.
        self.placements.track();
        crate::panel_layout::bottom_dock_available(surface().1)
    }
    fn edit(self, edit: impl FnOnce(&mut PanelPlacement)) {
        let index = self.active.get_untracked().index();
        self.placements.update(|panels| edit(&mut panels[index]));
        self.save();
    }
    pub(super) fn style(self) -> String {
        let panel = self.current();
        format!("--panel-x:{}px;--panel-y:{}px", panel.x, panel.y)
    }
}

#[component]
pub(super) fn PanelHeader(layout: PanelLayout) -> impl IntoView {
    let menu = NodeRef::<leptos::html::Details>::new();
    let menu_toggle = NodeRef::<leptos::html::Summary>::new();
    let close_menu = move || {
        if let Some(menu) = menu.get() {
            let _ = menu.remove_attribute("open");
        }
        // Closing details hides the focused option. Return to its visible
        // trigger so keyboard navigation continues at the relocated panel.
        if let Some(toggle) = menu_toggle.get() {
            let _ = toggle.focus();
        }
    };
    let dragging = RwSignal::new(None::<(usize, f64, f64)>);
    let motion = window_event_listener(leptos::ev::mousemove, move |event| {
        let Some((index, dx, dy)) = dragging.get_untracked() else {
            return;
        };
        if event.buttons() & 1 == 0 {
            dragging.set(None);
            return;
        }
        let (width, height) = surface();
        layout.placements.update(|panels| {
            panels[index].move_to(
                f64::from(event.client_x()) - dx,
                f64::from(event.client_y()) - dy,
                width,
                height,
            );
        });
    });
    let release = window_event_listener(leptos::ev::mouseup, move |_| {
        if dragging.get_untracked().is_some() {
            layout.save();
        }
        dragging.set(None);
    });
    on_cleanup(move || {
        motion.remove();
        release.remove();
    });
    view! {
        <header class="forge-panel-header">
            <button class="forge-panel-grip" title="Glisser pour détacher et déplacer le panneau" aria-label="Déplacer le panneau ; les boutons d’ancrage sont accessibles au clavier"
                on:mousedown=move |event| {
                    if event.button()!=0 { return; }
                    event.prevent_default(); event.stop_propagation();
                    let Some(panel) = event.current_target().and_then(|target| target.dyn_into::<web_sys::Element>().ok())
                        .and_then(|element| element.closest(".forge-topic-window").ok().flatten()) else { return; };
                    let rect=panel.get_bounding_client_rect();
                    // Coordinates follow the compact/full-height brand bar.
                    dragging.set(Some((layout.active.get_untracked().index(),f64::from(event.client_x())-rect.left()+8.0,f64::from(event.client_y())-rect.top()+body_top()+8.0)));
                }>
                <span aria-hidden="true">"⠿"</span>{move || layout.active.get().label()}
            </button>
            <button title="Réduire / déployer le panneau" aria-expanded=move || (!layout.current().collapsed).to_string()
                on:click=move |_| layout.edit(|p| p.collapsed = !p.collapsed)> {move || if layout.current().collapsed { "+" } else { "−" }} </button>
            <details class="forge-dock-menu" node_ref=menu><summary node_ref=menu_toggle title="Ancrage du panneau">"Ancrer"</summary>
                <div aria-label="Position du panneau">
                    {[(Dock::Left,"À gauche"),(Dock::Right,"À droite"),(Dock::Bottom,"En bas"),(Dock::Floating,"Flottant")].into_iter().map(move |(dock,label)| view! {
                        <button disabled=move || dock==Dock::Bottom && !layout.bottom_available()
                            title=move || if dock==Dock::Bottom && !layout.bottom_available(){"Agrandissez la fenêtre : l’ancrage inférieur doit laisser la carte visible."}else{""}
                            aria-pressed=move || (layout.current().dock==dock).to_string() on:click=move |_| {
                                layout.edit(|p| {p.dock=dock;p.collapsed=false;});
                                close_menu();
                            }>{move || if dock==Dock::Bottom && !layout.bottom_available(){"En bas · agrandir la fenêtre"}else{label}}</button>
                    }).collect_view()}
                    <button on:click=move |_| {layout.placements.set([PanelPlacement::default();8]);layout.save();close_menu();}>"Réinitialiser la disposition"</button>
                </div>
            </details>
        </header>
    }
}
