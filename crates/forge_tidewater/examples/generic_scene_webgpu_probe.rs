//! WebGPU executes the same procedural scenes and optical comparisons as native.
#[cfg(target_arch = "wasm32")]
#[path = "support/generic_optical_probe.rs"]
mod harness;
#[cfg(target_arch = "wasm32")]
#[wasm_bindgen::prelude::wasm_bindgen]
pub async fn run_generic_probe() -> Result<String, wasm_bindgen::JsValue> {
    console_error_panic_hook::set_once();
    let result = harness::run(None)
        .await
        .map_err(|e| wasm_bindgen::JsValue::from_str(&e.to_string()))?;
    serde_json::to_string(&result).map_err(|e| wasm_bindgen::JsValue::from_str(&e.to_string()))
}
#[cfg(not(target_arch = "wasm32"))]
fn main() {}
