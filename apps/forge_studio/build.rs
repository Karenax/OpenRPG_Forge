fn main() {
    let desktop_enabled = std::env::var_os("CARGO_FEATURE_DESKTOP").is_some();
    if desktop_enabled && std::env::var("CARGO_CFG_TARGET_ARCH").as_deref() != Ok("wasm32") {
        let app_manifest = tauri_build::AppManifest::new().commands(&[
            "execute_studio_command",
            "execute_asset_import",
            "export_module",
            "read_asset_library",
            "read_asset_model",
            "read_asset_physics",
            "launch_native_game",
            "launch_exported_module",
            "native_game_running",
            "native_game_diagnostics",
        ]);
        let attributes = tauri_build::Attributes::new().app_manifest(app_manifest);
        tauri_build::try_build(attributes)
            .unwrap_or_else(|error| panic!("failed to build the bounded Studio ACL: {error:#}"));
    } else {
        // This branch generates nothing and reads no package files. Avoid the
        // default whole-package scan; Rust source dependencies remain Cargo's.
        println!("cargo:rerun-if-changed=build.rs");
    }
}
