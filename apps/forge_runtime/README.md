# forge_runtime

Native Bevy executable that loads a versioned Forge scene document. With no arguments it generates
the deterministic procedural Gate 0 proof. Automation options are `--capture <png>`,
`--capture-frame <n>`, `--metrics <json>` and `--exit-after-frames <n>`.

When capture and metrics are requested together, `--metrics-warmup-frames` must be greater than or
equal to `--capture-frame + PIPELINE_WARMUP_STABLE_FRAMES`. The current stability barrier is 30
frames. This keeps screenshot-pipeline creation outside the measured window. For example:

```text
--capture proof.png --capture-frame 180 --metrics metrics.json \
  --metrics-warmup-frames 240 --exit-after-frames 360
```

The primary window is resizable, requests VSync by default and keeps Bevy's normal close-button
lifecycle. `--no-vsync` requests `AutoNoVsync`. The proof harness can exercise the real window path
without UI automation:

```text
--resize-at-frame 60 --resize-width 1280 --resize-height 720
--close-at-frame 120
```

Resize evidence distinguishes the request from the later `WindowResized` event emitted by the OS
backend, and a bounded proof fails unless that backend event confirms the requested dimensions. The
close harness sends a synthetic but normal `WindowCloseRequested`; it proves Bevy's lifecycle path,
not a physical title-bar action. It is deliberately standalone so it cannot pre-empt capture,
metrics, bounded-exit or resize evidence. A real Alt+F4/title-bar close remains part of qualification.

Native logs are JSON Lines on stderr through `tracing`. Forge events carry
`schema=forge.runtime-event`, a numeric `schema_version` and a stable `event_kind`; fields such as
dimensions, frame numbers, booleans and exit codes remain typed JSON values. `RUST_LOG` controls the
filter. `--log-probe` emits a bounded machine-parseable event and exits successfully without opening
a GPU window.

## Gate 1 visual-proof acquisition

`--gate1-visual-proof <output-dir>` is a strict, self-bounded Visual Valley mode. It requires
`--visual-valley`, accepts only a fixed quality tier, and owns the camera, weather, screenshots and
exit lifecycle. The output directory must be absent or empty. A typical native replay is:

```text
forge_runtime --visual-valley --quality high \
  --gate1-visual-proof artifacts/gate1-visual-high --no-vsync
```

One process waits for stable pipelines, records distant and human-height views, frames the authored
river in clear weather, transitions to rain, returns exactly to clear, then records 24 deterministic
terrain-relative proof-camera positions. It writes five fixed PNGs, `movement/frame-000.png` through
`frame-023.png`, and `gate1-visual-proof.json`.

The PowerShell validator can run an existing binary, validate all PNGs and endpoint evidence, hash
the artifacts, and assemble the movement frames as an 8 FPS MP4:

```text
pwsh -File scripts/Test-Gate1VisualProof.ps1 \
  -EvidenceRoot artifacts/gate1-visual-high \
  -RuntimePath target/release/forge_runtime.exe \
  -Quality high -NoVsync
```

The movement sequence is an explicit proof-camera path, not a gameplay/controller claim. Screenshot
acquisition proves that rendered artifacts were produced, while the weather ledger remains CPU/ECS
projection evidence. The harness and validator always leave GPU execution qualification, comparative
visual quality and human review false; their output alone cannot close `R1-025`, `R1-026` or
`R1-046` through `R1-048`.
