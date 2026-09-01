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
`--visual-valley`, a fixed quality tier and `--no-vsync`, and owns the camera, weather, screenshots
and exit lifecycle. The output directory must be absent or empty. Build from the clean commit that
will own the evidence so the ledger embeds its exact identity:

```powershell
$env:FORGE_SOURCE_COMMIT = (git rev-parse HEAD)
cargo build --release -p forge_runtime --locked -j1
Remove-Item Env:FORGE_SOURCE_COMMIT
```

A typical native replay is:

```text
forge_runtime --visual-valley --quality high \
  --gate1-visual-proof artifacts/gate1-visual-high --no-vsync
```

One process waits for stable pipelines, records distant and human-height views, frames the authored
river in clear weather, transitions to rain, and returns exactly to clear. It then exercises the
embedded third-person gameplay controller and Rapier terrain collision at 60 Hz: one forward command
drives a central-seam crossing, the same `ForgePlayerCamera` is sampled at 24 exact fixed-tick
checkpoints, and an immediately following stop command must settle inside 30 fixed ticks. The harness
pauses virtual time and discards fixed overstep while each checkpoint screenshot is acquired. It
writes five fixed PNGs, `movement/frame-000.png` through `frame-023.png`, and the schema-v2
`gate1-visual-proof.json` ledger.

The PowerShell validator can run an existing binary, validate all PNGs and endpoint evidence, hash
the artifacts, and assemble the movement frames as an 8 FPS MP4:

```text
pwsh -File scripts/Test-Gate1VisualProof.ps1 \
  -EvidenceRoot artifacts/gate1-visual-high \
  -RuntimePath target/release/forge_runtime.exe \
  -Quality high -NoVsync
```

The validator requires a clean worktree before and after acquisition, binds the embedded commit to
that worktree, records the requested no-VSync mode, GPU/backend/driver and runtime logs, then rejects
warning, error or panic entries. It validates the controller command sequences, stable player,
camera and Rapier identities, exact 60 Hz checkpoints, grounded/collision continuity, bounded stop,
canonical PNG paths, and the 24-frame H.264 artifact. Because the MP4 is assembled at 8 FPS from
paused checkpoints, it does not qualify real-time stutter. Screenshot acquisition proves only that
rendered artifacts were produced, while the weather ledger remains CPU/ECS projection evidence. For
`R1-026`, automation observes the mapped color-grade endpoints and only the presence of the requested
Bloom component, not numeric Bloom parameters or GPU execution. The harness and validator always
leave real-time stutter, GPU execution, comparative visual quality and human review false; their
output alone cannot close `R1-025`, `R1-026` or `R1-046` through `R1-048`.
