use std::{cell::Cell, rc::Rc};
use wasm_bindgen::{JsCast, JsValue, closure::Closure};
use wasm_bindgen_futures::JsFuture;

const REQUEST_TIMEOUT_MS: i32 = 20_000;

struct Deadline {
    window: web_sys::Window,
    controller: web_sys::AbortController,
    timer: i32,
    expired: Rc<Cell<bool>>,
    _callback: Closure<dyn FnMut()>,
}

impl Deadline {
    fn new(window: web_sys::Window) -> Result<Self, String> {
        let controller = web_sys::AbortController::new().map_err(super::js_error)?;
        let abort = controller.clone();
        let expired = Rc::new(Cell::new(false));
        let flag = expired.clone();
        let callback = Closure::wrap(Box::new(move || {
            flag.set(true);
            abort.abort();
        }) as Box<dyn FnMut()>);
        let timer = window
            .set_timeout_with_callback_and_timeout_and_arguments_0(
                callback.as_ref().unchecked_ref(),
                REQUEST_TIMEOUT_MS,
            )
            .map_err(super::js_error)?;
        Ok(Self {
            window,
            controller,
            timer,
            expired,
            _callback: callback,
        })
    }

    fn error(&self, path: &str) -> String {
        network_error(path, self.expired.get())
    }
}

fn network_error(path: &str, expired: bool) -> String {
    if expired {
        format!("Kit {path} : délai de 20 secondes dépassé, téléchargement annulé")
    } else {
        format!("Kit {path} : erreur réseau ; vérifiez le serveur local puis réessayez")
    }
}

impl Drop for Deadline {
    fn drop(&mut self) {
        self.window.clear_timeout_with_handle(self.timer);
        self.controller.abort();
    }
}

pub(crate) async fn fetch(path: &str, limit: usize) -> Result<Vec<u8>, String> {
    fetch_relative(&format!("./forge-kit/{path}"), limit).await
}

pub(crate) async fn fetch_weather_candidate() -> Result<Vec<u8>, String> {
    fetch_relative("./forge-weather/candidate.json", 64 * 1024).await
}

pub(crate) async fn fetch_view_candidate() -> Result<Vec<u8>, String> {
    fetch_relative("./forge-views/world-sketch.json", 512 * 1024).await
}

pub(crate) async fn fetch_building_candidate() -> Result<Vec<u8>, String> {
    fetch_relative("./forge-imports/buildings.json", 4096).await
}

pub(crate) async fn fetch_building_payload(
    package: forge_core::ContentHash,
    name: &str,
    limit: usize,
) -> Result<Vec<u8>, String> {
    let name = js_sys::encode_uri_component(name);
    fetch_relative(&format!("./forge-imports/{package}/{name}"), limit).await
}

async fn fetch_relative(path: &str, limit: usize) -> Result<Vec<u8>, String> {
    let window = web_sys::window().ok_or("Fenêtre absente")?;
    let deadline = Deadline::new(window.clone())?;
    let options = web_sys::RequestInit::new();
    options.set_redirect(web_sys::RequestRedirect::Error);
    options.set_cache(web_sys::RequestCache::NoStore);
    options.set_signal(Some(&deadline.controller.signal()));
    let request =
        web_sys::Request::new_with_str_and_init(path, &options).map_err(super::js_error)?;
    let response: web_sys::Response = JsFuture::from(window.fetch_with_request(&request))
        .await
        .map_err(|_| deadline.error(path))?
        .dyn_into()
        .map_err(super::js_error)?;
    if !response.ok() {
        return Err(format!("Kit {path} : HTTP {}", response.status()));
    }
    let reader: web_sys::ReadableStreamDefaultReader = response
        .body()
        .ok_or("Corps HTTP absent")?
        .get_reader()
        .dyn_into()
        .map_err(|error| super::js_error(error.into()))?;
    let mut bytes = Vec::new();
    loop {
        let result = JsFuture::from(reader.read())
            .await
            .map_err(|_| deadline.error(path))?;
        if js_sys::Reflect::get(&result, &JsValue::from_str("done"))
            .map_err(super::js_error)?
            .as_bool()
            == Some(true)
        {
            reader.release_lock();
            return Ok(bytes);
        }
        let chunk: js_sys::Uint8Array = js_sys::Reflect::get(&result, &JsValue::from_str("value"))
            .map_err(super::js_error)?
            .dyn_into()
            .map_err(super::js_error)?;
        let length = chunk.length() as usize;
        if length > limit.saturating_sub(bytes.len()) {
            return Err(format!(
                "Kit {path} : limite de {limit} octets dépassée, téléchargement annulé"
            ));
        }
        let start = bytes.len();
        bytes.resize(start + length, 0);
        chunk.copy_to(&mut bytes[start..]);
    }
}

#[cfg(test)]
mod tests {
    use super::network_error;

    #[test]
    fn network_error_is_stable_and_actionable_without_browser_error_details() {
        assert_eq!(
            network_error("./forge-kit/assets/tree.glb", false),
            "Kit ./forge-kit/assets/tree.glb : erreur réseau ; vérifiez le serveur local puis réessayez"
        );
    }

    #[test]
    fn timeout_message_is_preserved() {
        assert_eq!(
            network_error("./forge-kit/build.json", true),
            "Kit ./forge-kit/build.json : délai de 20 secondes dépassé, téléchargement annulé"
        );
    }
}
