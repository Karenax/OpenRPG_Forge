//! Loading stays visible until the accepted map and its projections are ready.
use leptos::prelude::*;
use std::cell::Cell;

thread_local! {
    static OUTPUT: Cell<Option<RwSignal<Option<String>>>> = const { Cell::new(None) };
}

pub(super) fn begin(message: &str) {
    OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(Some(message.to_owned()));
        }
    });
}

pub(super) fn finish() {
    OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(None);
        }
    });
}

pub(super) fn pending() -> bool {
    OUTPUT.with(|slot| {
        slot.get()
            .is_some_and(|output| output.get_untracked().is_some())
    })
}

#[component]
pub(super) fn MapLoading() -> impl IntoView {
    let phase = RwSignal::new(None::<String>);
    let started = StoredValue::new(None::<f64>);
    let elapsed = RwSignal::new(0_u64);
    let compact = RwSignal::new(false);
    let active_tab = expect_context::<RwSignal<super::authoring_controls::AuthoringTab>>();
    Effect::new(move |_| {
        if phase.get().is_some() {
            if started.get_value().is_none() {
                started.set_value(Some(js_sys::Date::now()));
                elapsed.set(0);
                compact.set(false);
            }
        } else {
            started.set_value(None);
            elapsed.set(0);
            compact.set(false);
        }
    });
    if let Ok(handle) = leptos::leptos_dom::helpers::set_interval_with_handle(
        move || {
            if let Some(started) = started.get_value() {
                #[allow(
                    clippy::cast_possible_truncation,
                    clippy::cast_sign_loss,
                    reason = "Nonnegative elapsed UI seconds"
                )]
                elapsed.set(((js_sys::Date::now() - started).max(0.0) / 1000.0) as u64);
            }
        },
        std::time::Duration::from_secs(1),
    ) {
        on_cleanup(move || handle.clear());
    }
    OUTPUT.with(|slot| slot.set(Some(phase)));
    on_cleanup(|| OUTPUT.with(|slot| slot.set(None)));
    view! {
        <Show when=move || phase.get().is_some()>
            <div class="forge-map-loading" data-compact=move ||compact.get().to_string() aria-busy="true">
                <span class="forge-loading-spinner" aria-hidden="true"></span>
                <strong>"Chargement de la carte"</strong>
                <span role="status" aria-live="polite">{move || phase.get().unwrap_or_default()}</span>
                <span aria-live="off">{move ||format!("Temps écoulé : {} s",elapsed.get())}</span>
                <Show when=move || { elapsed.get() >= 15 }>
                    <p>"La préparation continue sans appliquer de retouche. Les erreurs apparaissent dans le journal. Vous pouvez revenir aux modules pour ouvrir une autre carte après la commande en cours."</p>
                    <button type="button" on:click=move |_|{active_tab.set(super::authoring_controls::AuthoringTab::Project);compact.set(true);}>"Afficher les modules pendant le chargement"</button>
                </Show>
                <Show when=move ||compact.get()><button type="button" on:click=move |_|compact.set(false)>"Revenir au suivi du chargement"</button></Show>
            </div>
        </Show>
    }
}
