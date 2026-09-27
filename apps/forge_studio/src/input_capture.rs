//! Observes browser input capture without weakening focus or pointer-lock requirements.
use leptos::prelude::*;
use std::cell::RefCell;
use wasm_bindgen::{JsCast, closure::Closure};

type ErrorListener = (web_sys::Document, Closure<dyn FnMut(web_sys::Event)>);
thread_local! {
    static FAILED: RefCell<bool> = const { RefCell::new(false) };
    static OUTPUT: RefCell<Option<WriteSignal<&'static str>>> = const { RefCell::new(None) };
    static LISTENER: RefCell<Option<ErrorListener>> = const { RefCell::new(None) };
    static LAST: RefCell<&'static str> = const { RefCell::new("") };
    static KEY_OUTPUT: RefCell<Option<WriteSignal<&'static str>>> = const { RefCell::new(None) };
}

/// Reports only a canvas DOM event category; does not prove Bevy consumed a command.
pub fn observe_key(code: &str) {
    KEY_OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(super::input_key_observation::physical_key_label(code));
        }
    });
}

fn show(message: &'static str) {
    LAST.with(|last| {
        if *last.borrow() == message {
            return;
        }
        *last.borrow_mut() = message;
        OUTPUT.with(|output| {
            if let Some(output) = *output.borrow() {
                output.set(message);
            }
        });
    });
}

pub fn begin_request() {
    FAILED.with(|failed| *failed.borrow_mut() = false);
    show("Capture demandée · en attente du navigateur");
}

/// Gives the Winit canvas the DOM keyboard target before requesting pointer lock.
///
/// The Studio intentionally keeps `prevent_default_event_handling` disabled so
/// ordinary authoring controls retain their browser behavior. Winit consequently
/// does not focus the canvas on its pointer handler. Do this synchronously in the
/// user gesture instead: pointer lock remains the required authority checked by
/// `PlayerInputFocus`.
pub fn focus_then_request_pointer_lock(canvas: &web_sys::Element) {
    focus_then_request(
        || {
            if let Some(element) = canvas.dyn_ref::<web_sys::HtmlElement>() {
                let _ = element.focus();
            }
        },
        || canvas.request_pointer_lock(),
    );
}

fn focus_then_request(mut focus: impl FnMut(), mut request_pointer_lock: impl FnMut()) {
    focus();
    request_pointer_lock();
}

pub fn observe(active: bool, focused: bool) {
    if active {
        FAILED.with(|failed| *failed.borrow_mut() = false);
        show("Exploration active · Échap pour libérer");
    } else if FAILED.with(|failed| *failed.borrow()) {
        show("Capture refusée par le navigateur · cliquer pour réessayer dans une fenêtre active");
    } else if !focused {
        show("Entrées suspendues · fenêtre non focalisée");
    } else {
        show("Entrées libres · cliquer dans la vue pour explorer");
    }
}

#[component]
pub fn InputCaptureStatus() -> impl IntoView {
    let (status, output) = signal("Entrées libres · cliquer dans la vue pour explorer");
    let (key_status, key_output) = signal("aucune touche observée");
    KEY_OUTPUT.with(|slot| slot.replace(Some(key_output)));
    OUTPUT.with(|slot| slot.replace(Some(output)));
    LAST.with(|last| *last.borrow_mut() = "");
    FAILED.with(|failed| *failed.borrow_mut() = false);
    if let Some(document) = web_sys::window().and_then(|window| window.document()) {
        let listener = Closure::<dyn FnMut(web_sys::Event)>::new(|_| {
            FAILED.with(|failed| *failed.borrow_mut() = true);
            observe(false, true);
        });
        if document
            .add_event_listener_with_callback("pointerlockerror", listener.as_ref().unchecked_ref())
            .is_ok()
        {
            LISTENER.with(|slot| slot.replace(Some((document, listener))));
        } else {
            show("Diagnostic de capture indisponible");
        }
    }
    on_cleanup(|| {
        KEY_OUTPUT.with(|slot| slot.replace(None));
        OUTPUT.with(|slot| slot.replace(None));
        LISTENER.with(|slot| {
            if let Some((document, listener)) = slot.borrow_mut().take() {
                let _ = document.remove_event_listener_with_callback(
                    "pointerlockerror",
                    listener.as_ref().unchecked_ref(),
                );
            }
        });
    });
    view! {
        <span id="studio-input-capture-status" role="status">{move || status.get()}</span>
        <span id="studio-keyboard-event-status">" · Clavier canvas : "{move || key_status.get()}" (réception DOM seulement)"</span>
    }
}

#[cfg(test)]
mod tests {
    use super::focus_then_request;
    use std::cell::RefCell;

    #[test]
    fn focus_is_requested_before_pointer_lock() {
        let observed = RefCell::new(Vec::new());
        focus_then_request(
            || observed.borrow_mut().push("focus"),
            || observed.borrow_mut().push("pointer-lock"),
        );
        assert_eq!(*observed.borrow(), ["focus", "pointer-lock"]);
    }
}
