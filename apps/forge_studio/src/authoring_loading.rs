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
    OUTPUT.with(|slot| slot.set(Some(phase)));
    on_cleanup(|| OUTPUT.with(|slot| slot.set(None)));
    view! {
        <Show when=move || phase.get().is_some()>
            <div class="forge-map-loading" role="status" aria-live="polite" aria-busy="true">
                <span class="forge-loading-spinner" aria-hidden="true"></span>
                <strong>"Chargement de la carte"</strong>
                <span>{move || phase.get().unwrap_or_default()}</span>
            </div>
        </Show>
    }
}
