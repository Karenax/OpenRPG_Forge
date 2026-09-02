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

## Nonqualifying render profile

The opt-in `render-profile` Cargo feature exists only to localize CPU-side Bevy and render-scheduling
cost. It enables
Bevy 0.19's `trace_chrome` feature, leaves Bevy's `LogPlugin` enabled and deliberately skips Forge's
structured JSON tracing initialization so that Bevy can own the process-wide subscriber. The
executable prints a `NONQUALIFYING` warning before argument processing. Normal builds do not enable
this feature, keep Forge structured JSON tracing and continue to disable Bevy's `LogPlugin`.
Conversely, `--all-features` enables `render-profile` and therefore cannot produce a qualifying
runtime executable. By default, profiled builds still refuse `--log-probe`, `--gate1-visual-proof`
and `--gate1-gameplay-stutter` before any proof path is prepared. The feature-only
`--render-profile-gate1-diagnostic` flag is the sole exception: it requires the existing
`--gate1-gameplay-stutter` route and can never be combined with `--gate1-visual-proof` or
`--log-probe`. A normal build does not recognize this flag.

After the gameplay-stutter output preflight succeeds and before any Bevy plugin is added, the opt-in
atomically publishes `NONQUALIFYING_RENDER_PROFILE.json` in that output directory. Its
`forge.nonqualifying-render-profile` schema v1 contains `qualifying=false`, the
`source_commit` build identity from `option_env!("FORGE_SOURCE_COMMIT")` (or `null` when it was not embedded),
`feature="render-profile"` and the nonqualification reason. No ordinary profile route and no normal
build writes this marker. The profiled build caps formatted stderr output at `INFO` while retaining
`TRACE` spans in the Chrome layer, and disables Bevy's render-pass diagnostics plugin so the trace
does not add its separate per-frame resolve encoder to the workload.

Build and start the binary side of the diagnostic explicitly, with `FORGE_GATE1_EXTERNAL_ROOT`
resolving to an existing directory outside every Git worktree:

```powershell
$externalRoot = (Resolve-Path -LiteralPath $env:FORGE_GATE1_EXTERNAL_ROOT).Path
$profileRoot = [IO.Path]::GetFullPath((Join-Path $externalRoot 'render-profile'))
$repoRoot = (Resolve-Path -LiteralPath '.').Path
$worktreeLines = @(git worktree list --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'cannot enumerate Git worktrees' }
$worktreeRoots = @($worktreeLines | ForEach-Object {
    if ($_ -like 'worktree *') { [IO.Path]::GetFullPath($_.Substring(9)) }
})
foreach ($worktreeRoot in $worktreeRoots) {
    $prefix = $worktreeRoot.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
    if ($profileRoot.Equals($worktreeRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $profileRoot.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "render-profile root is inside Git worktree $worktreeRoot"
    }
}
New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$diagnosticOutput = Join-Path $profileRoot "gate1-stutter-$runId"
$previousCargoTarget = Get-Item Env:CARGO_TARGET_DIR -ErrorAction SilentlyContinue
$previousTraceChrome = Get-Item Env:TRACE_CHROME -ErrorAction SilentlyContinue
$previousRustLog = Get-Item Env:RUST_LOG -ErrorAction SilentlyContinue
$previousSourceCommit = Get-Item Env:FORGE_SOURCE_COMMIT -ErrorAction SilentlyContinue
$previousWgpuBackend = Get-Item Env:WGPU_BACKEND -ErrorAction SilentlyContinue
$previousAssetRoot = Get-Item Env:BEVY_ASSET_ROOT -ErrorAction SilentlyContinue
try {
    $env:CARGO_TARGET_DIR = Join-Path $profileRoot 'cargo-target'
    $env:TRACE_CHROME = Join-Path $profileRoot "trace-$runId.json"
    $env:RUST_LOG = 'trace'
    $env:FORGE_SOURCE_COMMIT = (& git rev-parse HEAD).Trim()
    $env:WGPU_BACKEND = 'vulkan'
    $env:BEVY_ASSET_ROOT = $repoRoot
    cargo build --release -p forge_runtime --features render-profile --locked -j1
    if ($LASTEXITCODE -ne 0) { throw 'render-profile build failed' }
    & (Join-Path $env:CARGO_TARGET_DIR 'release\forge_runtime.exe') `
        --visual-valley --quality high --no-vsync `
        --gate1-gameplay-stutter $diagnosticOutput `
        --render-profile-gate1-diagnostic
    if ($LASTEXITCODE -ne 0) { throw 'render-profile diagnostic failed' }
} finally {
    if ($null -eq $previousCargoTarget) { Remove-Item Env:CARGO_TARGET_DIR -ErrorAction SilentlyContinue } else { $env:CARGO_TARGET_DIR = $previousCargoTarget.Value }
    if ($null -eq $previousTraceChrome) { Remove-Item Env:TRACE_CHROME -ErrorAction SilentlyContinue } else { $env:TRACE_CHROME = $previousTraceChrome.Value }
    if ($null -eq $previousRustLog) { Remove-Item Env:RUST_LOG -ErrorAction SilentlyContinue } else { $env:RUST_LOG = $previousRustLog.Value }
    if ($null -eq $previousSourceCommit) { Remove-Item Env:FORGE_SOURCE_COMMIT -ErrorAction SilentlyContinue } else { $env:FORGE_SOURCE_COMMIT = $previousSourceCommit.Value }
    if ($null -eq $previousWgpuBackend) { Remove-Item Env:WGPU_BACKEND -ErrorAction SilentlyContinue } else { $env:WGPU_BACKEND = $previousWgpuBackend.Value }
    if ($null -eq $previousAssetRoot) { Remove-Item Env:BEVY_ASSET_ROOT -ErrorAction SilentlyContinue } else { $env:BEVY_ASSET_ROOT = $previousAssetRoot.Value }
}
```

The gameplay-stutter controller keeps its existing PresentMon attachment and atomic start-signal
contract. A diagnostic host must attach the pinned PresentMon process to the PID from
`gate1-gameplay-stutter.ready.json`, then atomically publish the existing
`gate1-gameplay-stutter.start.signal.json` contract. The direct process above waits for that signal
and fails closed if the host does not provide it. `scripts/Test-Gate1GameplayStutter.ps1`
deliberately remains the qualifying, normal-build path: it rebuilds without `render-profile`, does
not pass the opt-in flag and must not be repurposed for this diagnostic. This feature changes logging
and adds tracing instrumentation. Its executable, marker, route ledgers, logs and trace are
diagnostic evidence only: they must never be used to qualify a Gate 1 performance route or replace
the normal clean-commit Release binary.

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

`--gate1-gameplay-stutter <output-dir>` is a separate, self-bounded measurement mode with two explicit
presentation routes. The default `AutoNoVsync` route requires
`--visual-valley --quality high --no-vsync`. The diagnostic `FifoDiagnostic` route instead requires
`--visual-valley --quality high --gate1-fifo-diagnostic`, refuses `--no-vsync`, requests Bevy
`PresentMode::Fifo`, and atomically writes `NONQUALIFYING_GATE1_FIFO_DIAGNOSTIC.json`. That marker and
the ready/final protocols keep `qualification_eligible=false` for FIFO; the route can compare display
pacing but can never qualify or close Gate 1. Both routes require an absent or empty output directory. The governed
host acquisition requires Windows, a Vulkan-capable adapter, a Bevy-observed physical 1920x1080
surface and a visible, non-minimized native window whose reported outer rectangle is at least
1920x1080. Before publishing the start signal, the host requests that exact window in the foreground;
it then samples handle ownership, visibility, minimized state and foreground state every 100 ms
through the controller interval, requiring both boundary coverage and no sampling gap above 250 ms.
After the ledger is written, the runtime keeps the native window alive for a declared 500 ms
post-measurement observation hold; those frames are outside the QPC metric interval.
It additionally requires an absolute proof directory and a fresh, empty Cargo target
directory outside every registered Git worktree. After verifying the exact commit and clean tree,
the host script rebuilds the Release binary itself with `--locked`, embeds that commit, rechecks Git
state, and launches the resulting executable. The mode does not request any screenshot and never
pauses virtual time.

Both presentation routes keep the exact fixed High preset and all scene/render plugins, but
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

The command above acquires the default AutoNoVsync route. A single diagnostic FIFO acquisition uses
the same command with `-PresentationRoute FifoDiagnostic`; the host passes
`--gate1-fifo-diagnostic` instead of `--no-vsync`, retains the nonqualifying marker and records metric
failure without converting the diagnostic into qualification evidence.

After every governed High subsystem (terrain, shadows, vegetation, scenery, environment, water and
weather), the pipelines exercised by the process, the nine governed CC0 models and the
player/Rapier/camera bindings are ready, the runtime writes
`gate1-gameplay-stutter.ready.json`. It then waits for the host to attach the pinned PresentMon
process to that exact PID and atomically publish
`gate1-gameplay-stutter.start.signal.json`. The controller performs 900 fixed ticks forward and 900
ticks backward at 60 Hz, crosses the central seam without teleportation, stops under a bounded
watchdog, and writes `gate1-gameplay-stutter.json`. The ready contract uses schema 4, the start signal
remains schema 3, and the final runtime ledger uses schema 5. These versions add the explicit
presentation request/route, VSync request, qualification eligibility and observation scope while
retaining the controller's half-open
`[start, end)` Windows QPC interval. Forge's derived vegetation visibility audit runs at a bounded
100 ms cadence while Bevy
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
publishing the QPC-bound start signal, captures v2 display rows and ETW-loss counters, then computes a
display distribution correlated to the runtime controller interval. The runtime reads
`QueryPerformanceCounter` immediately before the first controller submission and after the final
movement sample, verifies the host QPC frequency, and serializes those two boundaries. The host
immediately persists the
published QPC boundary in `gate1-gameplay-stutter.host-boundary.json`, so an unsuccessful threshold
run still retains its exact host envelope. PresentMon writes through
`--output_stdout` into a shared live stream because its `--output_file` transport does not guarantee
bounded pre-signal visibility. The byte-for-byte raw stream is retained; the host deterministically
derives `presentmon.csv` and the normalized ETW-status log, and fails closed on a partial,
interleaved or malformed record. Once the runtime exits, the host stops the exact named trace session
with a bounded `--terminate_existing_session` helper, retains a separate fallback-stop log on cleanup
failures, and requires the control and capture processes to exit cleanly. The runtime GPU
backend remains Vulkan; PresentMon separately reports the Windows presentation operation and this
route requires the observed `PresentRuntime` to remain `DXGI`. The host validation uses schema 5 for
this stdout-stream transport, exact runtime-QPC contract, continuous foreground-window contract and
explicit uninstrumented-runtime contract.
Bevy `FrameTime` is
retained as an application-loop diagnostic but is never substituted for a missing displayed frame.
For every numeric PresentMon pair, the host derives the displayed interval as
`CPUStartQPC + DisplayLatency` through that start plus `DisplayedTime`; it keeps every interval that
overlaps the runtime's half-open controller window. The selected intervals must cover both runtime
boundaries and may have no internal gap larger than the QPC equivalent of two printed CSV quanta.
Within an otherwise valid envelope that retains at least 120 numeric displayed rows,
structurally valid `DisplayLatency=NA`/`DisplayedTime=NA` pairs mean that the frame was not displayed.
They are counted separately when their CPU start falls inside the controller window; any such frame
makes the thresholds fail. The display thresholds also require mean displayed FPS at least 60,
nearest-rank p95 at most 16.7 ms, 1 % low at least 45 FPS, no displayed interval above 33.3 ms and no
two consecutive displayed intervals above 16.7 ms. The host writes the failure validation before returning an error. If the
minimum sample or coverage prerequisites are absent, acquisition fails before metric validation and
only the already-persisted host boundary is guaranteed. The 1 % low uses the mean of the slowest
`ceil(samples / 100)` displayed times. The validation records
`exact_runtime_qpc_bounds_observed=true`; the display boundaries remain derived from four-decimal
PresentMon CSV metrics, so `exact_display_qpc_bounds_observed=false` remains explicit. Runtime-side
ingestion, `real_time_display_stutter_qualified`, human review,
GPU-target qualification and the overall Gate 1 route remain separate. This protocol alone does not
close `R1-048` or Gate 1, and Gate 2 remains forbidden while the remaining Gate 1 criteria are open.

`scripts/Test-Gate1PresentationAB.ps1` analyzes exactly six nonqualifying shared-binary diagnostic
summaries in counterbalanced order Auto, FIFO, FIFO, Auto, Auto, FIFO (AB/BA/AB). It requires the same
commit, tree, acquisition-helper hashes, governed script, Release-binary bytes/hash, scene identity,
instrumentation, GPU, operating system and display across all runs; each run must retain exact QPC
coverage, zero ETW loss, continuous foreground-window evidence and a stable presentation signature.
Frames are never pooled between runs and p95 values are never averaged for the verdict. Every FIFO
run must pass the display thresholds, FIFO may be no worse than its paired Auto run for p95 and 1 %
low, and at least two pairs must improve strictly. Infrastructure invalidity yields `INCONCLUSIVE`, a
metric miss yields `FAIL`, and a fully satisfied campaign yields only
`PROPOSE_FIFO_AS_CANONICAL_GATE1_PROTOCOL` with `qualifying=false` and `closes_gate=false`.
