fn main() {
    let desktop_enabled = std::env::var_os("CARGO_FEATURE_DESKTOP").is_some();
    if desktop_enabled && std::env::var("CARGO_CFG_TARGET_ARCH").as_deref() != Ok("wasm32") {
        let app_manifest = tauri_build::AppManifest::new().commands(&["execute_launcher_command"]);
        let attributes = tauri_build::Attributes::new().app_manifest(app_manifest);
        tauri_build::try_build(attributes)
            .unwrap_or_else(|error| panic!("failed to build the bounded launcher ACL: {error:#}"));
    }
}
