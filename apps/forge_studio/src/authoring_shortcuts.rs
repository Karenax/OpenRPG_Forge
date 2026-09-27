//! Scoped to the authoring workspace; text fields keep their native editing keys.
use super::editor_shortcut::{EditorShortcut, Modifiers, resolve};
use leptos::prelude::*;
use wasm_bindgen::JsCast;

thread_local! {
    static DISPATCH: std::cell::RefCell<Option<Callback<EditorShortcut>>> = const { std::cell::RefCell::new(None) };
    static CANCEL_PREVIEW: std::cell::RefCell<Option<Callback<()>>> = const { std::cell::RefCell::new(None) };
}

pub(super) fn install_cancel(callback: Callback<()>) {
    CANCEL_PREVIEW.with(|slot| slot.replace(Some(callback)));
    on_cleanup(|| {
        CANCEL_PREVIEW.with(|slot| slot.replace(None));
    });
}

pub(super) fn cancel_preview() {
    let callback = CANCEL_PREVIEW.with(|slot| *slot.borrow());
    if let Some(callback) = callback {
        callback.run(());
    }
}

pub(super) fn install(callback: Callback<EditorShortcut>) {
    DISPATCH.with(|slot| slot.replace(Some(callback)));
    on_cleanup(|| {
        DISPATCH.with(|slot| slot.replace(None));
    });
}

/// Keep shortcuts available when disabling a focused control returns focus to body.
/// Mounted only by `AuthoringWorkspace`, removed with that component, never process-global.
pub(super) fn listen_workspace() {
    let listener =
        leptos::leptos_dom::helpers::window_event_listener(leptos::ev::keydown, |event| {
            handle(&event);
        });
    on_cleanup(move || listener.remove());
}

pub(super) fn handle(event: &web_sys::KeyboardEvent) {
    if event.default_prevented() || event.is_composing() {
        return;
    }
    if event.key() == "Escape" {
        if !event.repeat() {
            cancel_preview();
        }
        // Leave native Escape behavior (select popup, pointer lock) available.
        return;
    }
    let Some(target) = event
        .target()
        .and_then(|t| t.dyn_into::<web_sys::Element>().ok())
    else {
        return;
    };
    // Conservative: custom editable descendants retain their local keyboard history.
    let editing_text = target.closest("input,textarea,select,[contenteditable]:not([contenteditable=\"false\"]),[role=\"textbox\"]").ok().flatten().is_some();
    let Some(shortcut) = resolve(
        &event.key(),
        Modifiers {
            control: event.ctrl_key(),
            shift: event.shift_key(),
            other: event.alt_key() || event.meta_key(),
        },
    ) else {
        return;
    };
    if editing_text && !super::editor_shortcut::allowed_in_text(shortcut) {
        return;
    }
    let callback = DISPATCH.with(|slot| *slot.borrow());
    if let Some(callback) = callback {
        event.prevent_default();
        event.stop_propagation();
        // Consume repeats, but never turn a held key into a burst of transactions.
        if !event.repeat() {
            callback.run(shortcut);
        }
    }
}
