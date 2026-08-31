# forge_studio

Tauri 2 desktop shell with a Leptos CSR interface and a Bevy WebGPU canvas. The hierarchy,
selection and IPC contracts are Rust types shared with the backend. UI rerenders do not recreate the
Bevy world: the viewport is started once and owns a stable canvas.

`trunk serve --config Trunk.toml` serves the browser proof. `cargo tauri dev --no-watch` launches the
same frontend inside WebView2 after the documented Gate 0 bootstrap.

The default URL remains the Gate 0 proof. Visual Valley is an explicit, reload-only viewport choice:

```powershell
pwsh -NoProfile -File scripts/Invoke-StudioTrunk.ps1 -Action serve
# Then open http://127.0.0.1:1420/?forge_scene=visual-valley
```

`forge_scene=visual-valley` loads the embedded terrain, vegetation, environment and player-controller
documents, records their individual canonical hashes, and installs their existing Bevy adapters with
the player controller owning the single requested camera. The river is intentionally not installed:
`forge_engine` exposes no water adapter at this revision. Its future installation is localized in
`install_visual_valley_plugins` after a native/WebGPU fallback has been qualified.

The dedicated `#forge-scene-telemetry-ledger` records the selected and runtime source manifests plus
the camera topology observed at Bevy's first positive real-time update. It does not claim a presented
WebGPU frame or visual inspection. Compilation and unit tests likewise prove contracts and target
compatibility only; a browser or WebView launch is still required to observe WebGPU execution.

## Gate 0 diagnostic protocol

The page exposes a machine-readable JSON ledger in `#gate0-telemetry-ledger`. Its contract is
`gate0-telemetry.schema.json`; the ledger records navigation-relative loading milestones, optional
Chromium heap data, WASM linear memory, 60 warmup frames followed by 120 measured Bevy real-time
update-loop frames, and handler-to-post-paint latency measured over two animation-frame callbacks.
GPU timestamp queries are not available in this foundation, so the frame distribution must not be
presented as GPU time.

`first_real_frame_ms` is emitted on the first positive `Time<Real>` delta observed by a Bevy
`Update` system. It proves that the Bevy loop reached an actual timed update after renderer startup;
it does not prove that the GPU presented that frame. Visual presentation still requires a capture
and inspection.

Two explicit query probes make negative paths reproducible without JavaScript business code:

- `?forge_gate0_probe=webgpu-unavailable` stops before adapter creation and renders the normal,
  clearly labelled WebGPU error path. This is deterministic fault injection, not evidence that the
  host GPU actually failed.
- `?forge_gate0_probe=tauri-permission` asks the Tauri shell to execute
  `plugin:window|set_title`. The capability explicitly denies that command; a successful proof shows
  `refus ACL confirmé`. In a standalone browser the ledger truthfully reports that no Tauri backend
  exists.

The hidden ledger is an automation surface, while the short summaries in the inspector remain the
human-readable surface. Raw browser or window captures stay under the ignored `artifacts/gate-0/`
tree unless a later evidence policy explicitly says otherwise.

The Tauri ACL probe has a dedicated configuration overlay, so the production window URL remains
untouched:

```powershell
cargo tauri build --no-bundle --config apps/forge_studio/tauri.gate0-proof.conf.json
pwsh -NoProfile -File scripts/Invoke-StudioWindowProof.ps1
```

The Windows harness launches the exact release executable, requests foreground focus, captures the
complete decorated window, checks sampled colour diversity, hashes both executable and PNG, and
writes a sidecar matching `gate0-window-capture.schema.json`. Screen capture can still be occluded;
the sidecar therefore remains `uninspected` until a human reviews the PNG.
