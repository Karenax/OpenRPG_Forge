fn main() {
    let desktop_enabled = std::env::var_os("CARGO_FEATURE_DESKTOP").is_some();
    if desktop_enabled && std::env::var("CARGO_CFG_TARGET_ARCH").as_deref() != Ok("wasm32") {
        tauri_build::build();
    }
}
