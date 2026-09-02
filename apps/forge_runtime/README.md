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

## Gate 1 gameplay-stutter acquisition

`--gate1-gameplay-stutter <output-dir>` is a separate, self-bounded measurement mode. It requires
`--visual-valley --quality high --no-vsync` and an absent or empty output directory. The governed
host acquisition requires Windows, a Vulkan-capable adapter, a Bevy-observed physical 1920x1080
surface and a visible, non-minimized native window whose reported outer rectangle is at least
1920x1080. It additionally requires an absolute proof directory and a fresh, empty Cargo target
directory outside every registered Git worktree. After verifying the exact commit and clean tree,
the host script rebuilds the Release binary itself with `--locked`, embeds that commit, rechecks Git
state, and launches the resulting executable. The mode does not request any screenshot and never
pauses virtual time.

This qualifying mode keeps the exact fixed High preset and all scene/render plugins, but
automatically disables Bevy's `RenderDiagnosticsPlugin`. In Bevy 0.19 that plugin adds timestamp and
pipeline-statistics collection plus a diagnostics resolve command encoder to every frame; retaining
it would measure an instrumented workload instead of the qualifying High path without internal render
diagnostics.
`GpuPerformancePlugin`, pipeline-cache readiness, allocator observation, GPU adapter identity and the
CPU-only `FrameTimeDiagnosticsPlugin` remain active. The overlay labels that CPU timing as an
application-loop update rate, never as displayed FPS, and explicitly reports render-pass CPU/GPU
timings and pipeline statistics as `not_observed` during this acquisition.

The host passes the exact clean worktree as `BEVY_ASSET_ROOT`; Bevy therefore reads the governed
`assets/` tree in place. The script rejects an `assets/` copy beside the external Release binary,
and both runtime ledgers must report `cadle_assets_used=false`. This acquisition does not qualify a
standalone packaged-asset layout.

Acquire `PresentMon-2.4.1-x64.exe` only from the official
[PresentMon v2.4.1 release](https://github.com/GameTechDev/PresentMon/releases/tag/v2.4.1). The host
script pins its 927304-byte Intel-signed binary and SHA-256
`d74183e7ae630f72cd3690be0373ecbfdc6cbb86578148aab8fa2a7166068f34`. That official asset has no
Windows file-version resource, so its release identity is established by the release source, exact
bytes, hash and Authenticode signer rather than a PE version field. From a clean exact commit,
set `FORGE_GATE1_EXTERNAL_ROOT` to an existing absolute directory outside every Git worktree and
`FORGE_PRESENTMON_241` to the pinned executable, then run:

```powershell
$repo = (Resolve-Path .).Path
$commit = (& git rev-parse HEAD).Trim()
$externalRoot = (Resolve-Path $env:FORGE_GATE1_EXTERNAL_ROOT).Path
$presentMon = (Resolve-Path $env:FORGE_PRESENTMON_241).Path
$runId = [DateTimeOffset]::UtcNow.ToString("yyyyMMddTHHmmssfffZ")
$cargoTarget = Join-Path $externalRoot "cargo-target\$commit-$runId"
$outputRoot = Join-Path $externalRoot "evidence\gate1-stutter-$runId"
pwsh -NoProfile -File scripts/Test-Gate1GameplayStutter.ps1 `
  -RepoRoot $repo `
  -RuntimeBinary (Join-Path $cargoTarget "release\forge_runtime.exe") `
  -OutputRoot $outputRoot `
  -PresentMonPath $presentMon
```

After every governed High subsystem (terrain, shadows, vegetation, scenery, environment, water and
weather), the pipelines exercised by the process, the nine governed CC0 models and the
player/Rapier/camera bindings are ready, the runtime writes
`gate1-gameplay-stutter.ready.json`. It then waits for the host to attach the pinned PresentMon
process to that exact PID and atomically publish
`gate1-gameplay-stutter.start.signal.json`. The controller performs 900 fixed ticks forward and 900
ticks backward at 60 Hz, crosses the central seam without teleportation, stops under a bounded
watchdog, and writes `gate1-gameplay-stutter.json`. Runtime ready, start-signal and final ledgers use
schema 3. Forge's derived vegetation visibility audit runs at a bounded 100 ms cadence while Bevy
visibility, rendering, wind, interaction and controller work remain per-frame. The harness requests
monotonic out-of-cadence audit tickets at the ready and final movement boundaries and refuses to
publish either boundary until the post-visibility observer has acknowledged the exact ticket.

The runtime ledger deliberately leaves display qualification false: it can prove the uninterrupted
controller, Rapier, camera, scene and pipeline window, but it cannot verify the external ETW trace.
Its `main_world_*` counters describe Bevy application updates only; they are not render submissions or
presented-frame counts. The ready and final ledgers explicitly record that Bevy render-pass timings,
pipeline statistics and runtime presented-frame counts are `not_observed`, that the additional Bevy
diagnostics resolve encoder is disabled, and that display timing belongs to external PresentMon
`DisplayedTime`.
`scripts/Test-Gate1GameplayStutter.ps1` owns that host boundary. It accepts only the pinned,
Authenticode-valid PresentMon 2.4.1 console binary, observes a complete target-PID row before
publishing the QPC-bound start signal, captures v2 display rows and ETW-loss counters, then computes
a bounded post-signal display distribution from `DisplayedTime`. The host immediately persists the
published QPC boundary in `gate1-gameplay-stutter.host-boundary.json`, so an unsuccessful threshold
run still retains its exact host envelope. PresentMon writes through
`--output_stdout` into a shared live stream because its `--output_file` transport does not guarantee
bounded pre-signal visibility. The byte-for-byte raw stream is retained; the host deterministically
derives `presentmon.csv` and the normalized ETW-status log, and fails closed on a partial,
interleaved or malformed record. Once the runtime exits, the host stops the exact named trace session
with a bounded `--terminate_existing_session` helper, retains a separate fallback-stop log on cleanup
failures, and requires the control and capture processes to exit cleanly. The runtime GPU
backend remains Vulkan; PresentMon separately reports the Windows presentation operation and this
route requires the observed `PresentRuntime` to remain `DXGI`. The host validation uses schema 3 for
this stdout-stream transport and explicit uninstrumented-runtime contract. Bevy `FrameTime` is
retained as an application-loop diagnostic but is never substituted for a missing displayed frame.
Within an otherwise valid envelope that retains at least 120 numeric
displayed rows and the required boundary coverage, structurally valid
`DisplayLatency=NA`/`DisplayedTime=NA` pairs are counted as unresolved displays; any inside the
bounded measurement makes its thresholds fail, and the host writes the failure validation before
returning an error. If those minimum sample or coverage prerequisites are absent, acquisition fails
before metric validation and only the already-persisted host boundary is guaranteed. The 1 % low uses
the mean of the slowest `ceil(samples / 100)` displayed times. The runtime does not yet expose exact
QPC values for the first
and last controller ticks, so the host records a bounded threshold sample but deliberately leaves
`exact_runtime_qpc_bounds_observed` and `real_time_display_stutter_qualified` false. Human review,
GPU-target qualification and the overall Gate 1 route remain separate. This protocol alone does not
close `R1-048` or Gate 1, and Gate 2 remains forbidden while the remaining Gate 1 criteria are open.
