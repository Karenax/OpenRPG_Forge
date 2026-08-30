# forge_studio

Tauri 2 desktop shell with a Leptos CSR interface and a Bevy WebGPU canvas. The hierarchy,
selection and IPC contracts are Rust types shared with the backend. UI rerenders do not recreate the
Bevy world: the viewport is started once and owns a stable canvas.

`trunk serve --config Trunk.toml` serves the browser proof. `cargo tauri dev --no-watch` launches the
same frontend inside WebView2 after the documented Gate 0 bootstrap.
