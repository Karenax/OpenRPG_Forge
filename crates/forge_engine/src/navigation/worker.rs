//! Browser transport only. The Rust provider executes in the dedicated worker.
use forge_navigation::{BuiltTile, ElapsedObservation, ObservedNavigationTile, TileInput};
use js_sys::{Array, Reflect, Uint8Array};
use std::{cell::RefCell, collections::VecDeque};
use wasm_bindgen::{JsCast, JsValue, closure::Closure};
use web_sys::{ErrorEvent, MessageEvent, Worker, WorkerOptions, WorkerType};

pub(super) struct WorkerOutput {
    pub tile: BuiltTile,
    pub generation: ElapsedObservation,
    pub wrapper: ElapsedObservation,
}
type Reply = (u64, Result<WorkerOutput, String>);
struct Transport {
    worker: Worker,
    _message: Closure<dyn FnMut(MessageEvent)>,
    _error: Closure<dyn FnMut(ErrorEvent)>,
    active: Option<u64>,
    observing: bool,
    dead: bool,
    replies: VecDeque<Reply>,
}
impl Drop for Transport {
    fn drop(&mut self) {
        // Detach old handlers before their Rust closures are dropped. Late
        // events must never touch a replacement transport's active token.
        self.worker.set_onmessage(None);
        self.worker.set_onerror(None);
        self.worker.terminate();
    }
}
thread_local! {static TRANSPORT:RefCell<Option<Transport>>=const {RefCell::new(None)};}

fn start() -> Result<Transport, String> {
    let options = WorkerOptions::new();
    options.set_type(WorkerType::Module);
    let worker = Worker::new_with_options("forge-navigation-worker.js", &options)
        .map_err(|error| js_error(&error))?;
    let message = Closure::<dyn FnMut(MessageEvent)>::new(|event: MessageEvent| {
        let data = event.data();
        let parsed = (|| {
            let token = Reflect::get(&data, &JsValue::from_str("token"))
                .map_err(|error| js_error(&error))?
                .as_string()
                .ok_or("missing navigation worker token")?
                .parse::<u64>()
                .map_err(|error| error.to_string())?;
            let error = Reflect::get(&data, &JsValue::from_str("error"))
                .map_err(|error| js_error(&error))?
                .as_string();
            let result = if let Some(error) = error {
                Err(error)
            } else {
                let buffer = Reflect::get(&data, &JsValue::from_str("buffer"))
                    .map_err(|error| js_error(&error))?;
                let bytes = Uint8Array::new(&buffer);
                if bytes.length() > 8 * 1024 * 1024 {
                    return Err("navigation worker output byte budget exceeded".into());
                }
                let observing = TRANSPORT
                    .with(|state| state.borrow().as_ref().is_some_and(|state| state.observing));
                parse_output(&data, &bytes.to_vec(), observing)
            };
            Ok::<_, String>((token, result))
        })();
        TRANSPORT.with(|state| {
            if let Some(state) = state.borrow_mut().as_mut() {
                match parsed {
                    Ok((token, result)) => {
                        if let Some(expected) = state.active.take() {
                            let result = if token == expected {
                                result
                            } else {
                                Err("navigation worker envelope token mismatch".into())
                            };
                            state.replies.push_back((expected, result));
                        }
                    }
                    Err(error) => {
                        if let Some(token) = state.active.take() {
                            state.replies.push_back((token, Err(error)));
                        }
                    }
                }
                while state.replies.len() > 2 {
                    state.replies.pop_front();
                }
            }
        });
    });
    let error = Closure::<dyn FnMut(ErrorEvent)>::new(|event: ErrorEvent| {
        TRANSPORT.with(|state| {
            if let Some(state) = state.borrow_mut().as_mut() {
                state.dead = true;
                if let Some(token) = state.active.take() {
                    state.replies.push_back((token, Err(event.message())));
                }
            }
        });
    });
    worker.set_onmessage(Some(message.as_ref().unchecked_ref()));
    worker.set_onerror(Some(error.as_ref().unchecked_ref()));
    Ok(Transport {
        worker,
        _message: message,
        _error: error,
        active: None,
        observing: false,
        dead: false,
        replies: VecDeque::new(),
    })
}
pub(super) fn dispatch(input: &TileInput, observe_timing: bool) -> Result<(), String> {
    let bytes = serde_json::to_vec(input).map_err(|error| error.to_string())?;
    if bytes.len() > 32 * 1024 * 1024 {
        return Err("navigation worker input byte budget exceeded".into());
    }
    TRANSPORT.with(|slot| {
        let mut slot = slot.borrow_mut();
        if slot.as_ref().is_some_and(|state| state.dead) {
            if slot.as_ref().is_some_and(|state| !state.replies.is_empty()) {
                return Err("poll the failed navigation worker response before dispatch".into());
            }
            drop(slot.take());
        }
        if slot.is_none() {
            *slot = Some(start()?);
        }
        let state = slot.as_mut().ok_or("navigation worker unavailable")?;
        if state.active.is_some() {
            return Err("navigation worker already active".into());
        }
        let array = Uint8Array::from(bytes.as_slice());
        let envelope = js_sys::Object::new();
        Reflect::set(
            &envelope,
            &JsValue::from_str("token"),
            &JsValue::from_str(&input.token.to_string()),
        )
        .map_err(|error| js_error(&error))?;
        Reflect::set(&envelope, &JsValue::from_str("buffer"), &array.buffer())
            .map_err(|error| js_error(&error))?;
        Reflect::set(
            &envelope,
            &JsValue::from_str("observe_timing"),
            &JsValue::from_bool(observe_timing),
        )
        .map_err(|error| js_error(&error))?;
        let transfer = Array::new();
        transfer.push(&array.buffer());
        state
            .worker
            .post_message_with_transfer(&envelope, &transfer)
            .map_err(|error| js_error(&error))?;
        state.active = Some(input.token);
        state.observing = observe_timing;
        Ok(())
    })
}
pub(super) fn poll() -> Vec<Reply> {
    TRANSPORT.with(|slot| {
        let mut slot = slot.borrow_mut();
        let replies = slot
            .as_mut()
            .map_or_else(Vec::new, |state| state.replies.drain(..).collect());
        if slot.as_ref().is_some_and(|state| state.dead)
            && let Some(state) = slot.take()
        {
            // Keep closures alive until the callback has returned. Its fatal
            // reply was drained above and is still returned to the job owner.
            drop(state);
        }
        replies
    })
}
pub(super) fn cancel(token: u64) {
    TRANSPORT.with(|slot| {
        let mut slot = slot.borrow_mut();
        if slot
            .as_ref()
            .is_some_and(|state| state.active == Some(token))
            && let Some(state) = slot.take()
        {
            drop(state);
        }
    });
}
pub(super) fn reset() {
    TRANSPORT.with(|slot| {
        if let Some(state) = slot.borrow_mut().take() {
            drop(state);
        }
    });
}
pub(super) fn now_ms() -> f64 {
    web_sys::window()
        .and_then(|window| window.performance())
        .map_or(0.0, |performance| performance.now())
}
fn js_error(value: &JsValue) -> String {
    value.as_string().unwrap_or_else(|| format!("{value:?}"))
}

fn parse_output(data: &JsValue, bytes: &[u8], observing: bool) -> Result<WorkerOutput, String> {
    let observed = Reflect::get(data, &JsValue::from_str("observed"))
        .map_err(|error| js_error(&error))?
        .as_bool()
        .unwrap_or(false);
    if observed != observing {
        return Err("navigation worker timing mode mismatch".into());
    }
    if !observing {
        return serde_json::from_slice::<BuiltTile>(bytes)
            .map(|tile| WorkerOutput {
                tile,
                generation: ElapsedObservation::not_requested(),
                wrapper: ElapsedObservation::not_requested(),
            })
            .map_err(|error| error.to_string());
    }
    let envelope: ObservedNavigationTile =
        serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
    if envelope.schema != "forge.navigation-worker-observation" || envelope.version != 1 {
        return Err("unsupported navigation worker timing observation".into());
    }
    envelope.generation.validate()?;
    let milliseconds = Reflect::get(data, &JsValue::from_str("wrapper_ms"))
        .map_err(|error| js_error(&error))?
        .as_f64()
        .unwrap_or(f64::NAN);
    Ok(WorkerOutput {
        tile: envelope.tile,
        generation: envelope.generation,
        wrapper: ElapsedObservation::measured(milliseconds, true),
    })
}
