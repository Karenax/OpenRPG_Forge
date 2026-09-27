# forge_runtime

`--quality high --prewarm-quality` explicitly prepares Low/Medium/High/Ultra for
the current scene and weather, then restores the initial quality (and adaptation
if requested), reapplying that global profile rather than individual preview overrides.
It does not edit the map. Fixed-view, bridge and domain cycle probes are rejected.
The global `--quality-cycle-proof` starts only after successful preparation, with
its own 120-second deadline. A requested screenshot waits for successful preparation;
keep a bounded run long enough for preparation plus capture. The metrics field
`profile_warmup` records each variant and the restored tier. Frame samples during
preparation are not filtered and are not a warmed performance qualification.

## Fixed-phase wind and moving quality diagnostics

`--fixed-proof-view <id> --fixed-proof-steps <4..32>` advances the existing paused
proof clock by that many quarter-second steps after kit shapes are prepared.
The completion event additionally waits for normal physics registration. Keep the
required bounded capture/exit arguments; this is not a performance clock.
`--fixed-proof-view-input <WorldSketch.json>` may change validation cameras only,
is limited to 512 KiB, and never replaces the canonical world/kit binding.

`--player-camera --walk-proof 3600:0:1 --quality-cycle-proof <fresh-directory>`
combines normal controller replay with the existing 120-second quality-cycle
watchdog. Do not add exit/capture/measurement flags in this combination. Every
stage records pose/velocity after GPU readiness; inspect them and the images.
Cycle completion need not finish the longer walk. Occlusion or a stationary
character cannot be counted as visual continuity in motion. The ordinary
standalone walk still requires `--exit-after-frames`.

## Experimental water depth comparison

`--water-medium-input profile.json` loads a strict, read-only `WaterMediumProfile`
(at most 1024 bytes) for a bounded optical WorldSketch fixed capture. It requires
the same fixed-view/capture/exit flags listed below and rejects combining with
`--water-optics-isolation`. The profile supplies `absorption_per_m` and
`scattering_per_m`, each an RGB array of finite coefficients in `[0,100]` inverse
metres. It replaces only `medium` on the loaded water copy; no source file, river
geometry, revision or palette is rewritten. Resulting water source hashes and
the experimental medium status identify this candidate. This is a proof input,
not a Studio editing or save/regeneration workflow.

For a bounded native comparison, add `--water-optics-isolation normals-off` or
`--water-optics-isolation refraction-off`. Requires `--world-sketch-visual-valley`,
`--water-depth-preview`, `--fixed-proof-view`, `--capture` and `--exit-after-frames`.
The selected loaded water copy alone is changed and the startup JSON records the
mode. No source file is written. `normals-off` zeros the normal perturbation
strength, not geometric displacement; `refraction-off` removes the offset, not
transmission/absorption. Omit the option for the unchanged baseline. This is a
native isolation tool, not an authoring command or a Studio control.

Add `--water-depth-preview` to a Visual Valley launch to select the shared
engine's unqualified transmissive water candidate. Sources and geometry remain
unchanged. Water SSR is disabled; environment reflections remain. Authored quality
overrides are not applied to this candidate and diagnostics must not be read as
a complete quality profile. Omit the flag for the existing opaque/deferred witness.
Use the same fixed proof camera and simulation phase for comparisons. This is
not a Gate 1 acceptance or Studio delivery.

## Bounded walking proof

`--world-sketch-visual-valley --player-camera --walk-proof 1400:-0.5:1
--exit-after-frames 2600` replays camera-relative movement through the normal
`PendingPlayerControlCommand` and Rapier controller, without keyboard injection,
teleportation, source edits or navigation/pathfinding. Arguments are fixed ticks
(1–3600), horizontal axis and forward axis (finite [-1,1], not both zero).
The replay waits for the kit and 120 grounded fixed ticks, walks, then submits
neutral input for 120 braking ticks. Inspect `walk_proof_started/completed` logs
and actual final pose: completion means inputs were replayed, not that a destination
was reached. A bounded runtime may exit before replay completion; a missing completed
event is an incomplete proof. Captures may be requested separately by frame.
Other controller probes, non-global cycle probes and fixed proof cameras are rejected. The ordinary
interactive sampler is disabled for bounded runs; this does not qualify physical
keyboard input, a saved itinerary, Studio authoring or an automatic navigation system.

Add `--walk-proof-measure` to collect a stationary window after the replay:
120 render frames of settling, then exactly 300 samples, summarized with the
shared `FrameDistribution`. Movement over 2 cm or horizontal speed above 0.01 m/s
invalidates the window. `walk_measurement_completed` contains raw samples,
distribution, player/camera state, window size and final render diagnostics;
these diagnostics are not total GPU time. The process exits after measurement.
The bounded-exit option remains required as a fallback. Capture, ordinary metrics,
resize, close and adaptive quality cannot be combined with this mode. This is a
local measurement facility, not performance acceptance or a qualification of input.

`--walk-proof-measure-moving` instead measures the live commanded walk, excluding
the first observed moving frame's delta (which can overlap preparation) and the
120 braking ticks. It never freezes time or requires a stationary slope position.
The separate `forge.walk-moving-measurement` report includes all raw render deltas
and matching replay ticks, player poses, camera state and virtual timestamps;
compare actual trajectory coverage before interpreting a profile A/B. Different
render rates produce different sample counts on the same fixed-tick route. The
report is emitted only after replay completion. Empty/invalid samples, paused
simulation and more than 100,000 samples fail. Its CLI exclusions are the same as
stationary measurement; measurement modes are mutually exclusive. This measures
native traversal, not Studio, a stationary view or target-hardware acceptance.

`--walk-proof-measure-frozen` explicitly pauses virtual time after the post-walk
settling period, before sampling real render-frame durations. This isolates a
static render workload when a live character drifts; it does not relax the live
mode's movement guard. Output reports `virtual_time_frozen=true` and scope
`static_render_isolation_not_live_game_fps`. Do not report these samples as live
game performance or compare them to an unfrozen workload as an optimization gain.

Both measurement modes also export `cpu_wall_phase_names` and matching rows in
`cpu_wall_phase_samples_ms`. Native-only boundary schedules partition wall time
into First–PreUpdate, the fixed loop, Update, SpawnScene–PostUpdate, and the
interval from the previous PostUpdate boundary to the next First boundary.
That last interval includes Last, rendering/presentation and runner waits; it is
not a GPU duration or proof of a driver bottleneck. Timers use `Instant` even
when virtual time is frozen. These markers run only with walk measurement enabled,
leave the scene unchanged, and add their own small, unquantified overhead. A phase
row is boundary-to-boundary, not exactly aligned to Bevy's `Time<Real>` sampling.

`--no-pipeline-statistics` is restricted to this bounded walk measurement. It
disables only the device's `PIPELINE_STATISTICS_QUERY` feature, leaving timestamp
queries and scene quality unchanged. Reports include the actual device feature
booleans; shader invocation/primitive query counters are unavailable in that
control run. Ordinary runtime and Studio defaults are unchanged. Compare on/off
using the same binary before attributing a cost to GPU instrumentation; this
option cannot turn unavailable counters into a passed qualification.

`--walk-shadow-profile <quality-profile.json>` selects an external, fully validated
quality document for the shadow plugin only. It requires bounded walk measurement or capture and
an explicit per-domain `--shadow-quality`; global `--quality` is rejected to avoid
acknowledging a different global policy. Other domains retain their own policies.
Project, area and terrain revision must already match: no identity/revision rebinding
of external documents. No source file is modified. This is a bounded diagnostic
input, not Studio profile editing or promotion of a visual preset.
`shadow_projection` in the measurement report includes actual cascade bounds,
atlas resolution and contact-shadow observations, with the existing CPU-projection
status; those values are not proof of GPU execution or visual acceptance.

## Raw atmosphere GPU readback

`--sky-ibl-proof-output <fresh-file.bin>` is a native-only diagnostic for a bounded
valley capture (not walk measurement). At/after frame 900, it requests one Bevy GPU
readback of the sole generated raw atmosphere cubemap, only when its RGBA16F format,
six layers, dimensions and COPY_SRC usage match the source component. It does not
change the source texture, lighting or material. The observer writes with create-new
semantics and removes its readback entity; duplicate in-flight completions are ignored.
The caller must require `sky_ibl_readback_completed` and validate byte count/layout:
an early exit or missing probe is an incomplete proof, not a success. Row padding is
Bevy's GPU copy alignment, recorded dimensions determine the expected size. Raw sky
RGB is not filtered diffuse irradiance, material lighting, or artistic acceptance.
The same request logs `sky_ibl_binding_snapshot`: filtered-probe and active-camera
texture IDs, intensity, rotation and lightmapped-diffuse flag. Compare these fields
to establish CPU binding consistency; this does not read the filtered GPU pixels.

## Frozen authored proof camera

`--world-sketch-visual-valley --fixed-proof-view <camera-id> --capture <png>
--exit-after-frames <n>` reuses the Studio's shared `FixedProofViewPlugin`:
the authored camera replaces the sole view and virtual time is paused from startup.
The option rejects duplicate IDs, missing WorldSketch/capture/bounded exit, metrics
export, player movement/probes, adaptation, resizing, and effect-cycle probes.
Unknown camera IDs fail before app startup. This is a diagnostic capture, not a
performance benchmark, saved edit, stability receipt, or RenderDoc qualification.
See `docs/evidence/gate1/R1_NATIVE_FIXED_PROOF_VIEW.md` for the native smoke.
Frozen captures additionally require 30 stable main-world view/clock observations,
then 30 fresh ready-pipeline observations while that condition holds. A changed
clock invalidates the earlier view observation immediately. Ordinary captures retain
their eight-observation threshold. These are capture prerequisites, not an assertion
that the candidate's quality profile or an external GPU capture is qualified.

Experimental native cloud layer: `--cloud-layer-preview settings.json` alongside
`--visual-valley` or `--world-sketch-visual-valley`. The input is a validated
`CloudLayerSettings` object, not a saved environment profile. Disabled by default;
no cloud shadows, atmosphere-IBL integration, volumetric model or qualified quality
preset. See `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md` for current evidence.

`--weather-input <json> --weather-cycle-proof <fresh-directory>` runs a native
clear/rain/clear cycle through `WeatherRuntimeState::request_state`, using the existing
bounded readback harness (180 matching endpoint observations, 120-second real deadline).
The candidate must start clear with optional sunlight and a rain multiplier below 1.
The proof requires intermediate sunlight values in both directions, exact final sunlight,
and sunlight restoration before the ground finishes drying. A valid profile whose timing
does not satisfy this last criterion will not pass this specific protocol.
Three fresh endpoint PNGs are captured; source changes, timeout or premature closure fail.
Other automation, weather overrides, player probes and adaptive quality are excluded.
This is not a video, a smoothness benchmark, a full GPU-readiness acknowledgement or a
Studio authoring feature. See `docs/evidence/gate1/R1_WEATHER_NATIVE_CYCLE.md`.

`--weather-input <json>` loads a read-only candidate in `--visual-valley` or
`--world-sketch-visual-valley` mode. It requires a regular UTF-8 file of at most 64 KiB,
strict weather schema and matching project/area/revision before Bevy starts. External
revisions are never rebound: WorldSketch revision 3 requires weather revision 3.
Omission preserves embedded defaults and their existing revision derivation. `--weather`
still selects the initial state without editing the document. Optional `sunlight` is
consumed by the shared engine; this CLI is a qualification input, not a Studio editor,
hot reload or a clear/rain/clear transition proof. See `R1_WEATHER_INPUT_NATIVE.md`.

`--prototype-forest-preview` requires `--prepare-vegetation-catalog` and its verified kit.
`--prototype-forest-batches` implies that preview and additionally selects the experimental
chunk/material far-batch projector. It preserves the catalog requirement and excludes
unqualified adaptive operation. Defaults remain unchanged. Local paired native measurements
and limitations are recorded in `docs/evidence/gate1/R1_NATIVE_FAR_BATCH_COST.md`:
fewer mesh entities but more allocated memory, no final HLOD or target-GPU qualification.

The individual `--prototype-forest-preview` mode:
It explicitly replaces procedural scatter trees with shared, root-normalized, wind-enabled
prototype LOD0/1/2. Other woody HLOD vegetation and canonical colliders remain. This is an
experimental preview, not promotion: final chunk HLOD batching, complete preview telemetry,
Web presentation and performance/visual qualification remain pending. The overlay and metrics
mark procedural geometry counts invalid while it is active. Omit the flag for the original
procedural comparison mode; preparation alone still does not alter the forest.

`--prepare-vegetation-catalog <json>` requires a verified `--asset-kit-build` and
`--asset-kit-preview` in WorldSketch mode. It validates the catalog against the compiled
vegetation, loads each referenced GLB once and prepares shared LOD handles. The event
`vegetation_prototypes_prepared` confirms cache readiness, **not scatter replacement**.
Failure is explicit; preparation has a 120-second timeout. The existing forest and colliders
remain unchanged in this preparation mode. See ADR-0010 and `R1_PROTOTYPE_CATALOG.md`.

Metrics schema v8 includes optional `global_quality` for `--quality` runs. It exposes the same
typed coordinator observation used by Studio, collected after its Last-schedule observer.
Absent on Gate 0; water/environment partial states are not promoted to complete success.

`--quality high --quality-cycle-proof <fresh-directory>` exercises a five-stage global hot cycle.
The Global cycle now requires current effective application Ready plus seven CPU domains and
GPU evidence at each High/Low/Medium/Ultra/High stage, not only available CPU budgets. All five
source bindings and the quality-policy hash must remain unchanged across stages. Each stage
waits for 180 matching observations and a fresh image readback; the 120-second deadline remains.
Experimental/unqualified overrides cannot pass this stronger cycle using CPU evidence alone.
`global_quality_cycle_observed` records the application sequence/hash and GPU verdict; this
does not qualify the full authored preset, adaptive performance or artistic acceptance.

`--water-quality low|medium|high|ultra --water-cycle-proof <fresh-directory>` keeps the requested tier fixed and captures
authored optics / reflections disabled / restored / tint disabled / restored,
followed by environment IBL intensity disabled / restored (seven
stages in Low). Medium/High/Ultra add SSR-only disabled / restored as stages 7/8,
without changing water reflectance, tint or environment-map intensity. This changes the
camera SSR path, not only one water material. The IBL pair leaves direct lights and water reflectance unchanged; it affects
IBL on the whole scene, not only the water. Camera and cubemap handles must remain identical,
the original positive intensity is restored, and missing images prevent capture acceptance.
The first five stages use the same transient requests as Studio; the final two are runtime-only
diagnostics. All use the existing bounded readback harness (180 matching
CPU observations per stage, 120-second deadline, incomplete exit is an error). Other cycle
proofs, global quality, player probes and single-frame automation are rejected. The directory
must not exist. Choose a water-facing validation camera through the governed preview.
Inspect images: CPU observations/readbacks alone do not qualify optical effects or performance.
Water cycles now pause virtual simulation time at startup to isolate optical changes from
moving waves/weather. Real time, GPU readbacks and the 120-second deadline keep running.
This proof-only freeze does not affect normal runtime or the other quality cycles; frozen
captures must not be used as performance benchmarks or animation qualification.
`--quality` now installs the engine request coordinator; it still does not enable automatic
adaptation by itself. All seven adapters must exist before dispatch, and observed partial effects remain
partial. Native cycling under rain passes with five inspected captures. No Studio selector yet.
See `docs/evidence/gate1/R1_040_GLOBAL_QUALITY_CYCLE.md`.

`--quality low|medium|high|ultra` selects all seven existing domain budgets at startup in valley
modes. Per-domain overrides and single-domain cycle proofs cannot be mixed with this option.
It is a combined request, not a complete preset acknowledgement: water/environment remain partial,
and automatic adaptation is not enabled by default. Native Low/Ultra under rain are inspected and measured
as short smokes. See `docs/evidence/gate1/R1_040_GLOBAL_INITIAL_QUALITY.md`.

`--quality high --adaptive-quality` explicitly enables the shared controller after a current
effective-plan GPU acknowledgement. It uses the authored sustained-frame thresholds, cooldown
and tier bounds; initial warmup and unfocused-window frames are not pressure samples. A manual
global request disables it; lost application proof stops it, with no automatic rollback.
The option rejects metrics/capture benchmarks, quality cycles, player/terrain/bridge probes
and unqualified volumetric/forest previews. `--exit-after-frames` remains available for a bounded
adaptive smoke, whose logs are not a fixed-quality performance benchmark. No default or authored
profile changes; no Studio automatic-control UI added. See the latest adaptation section in
`docs/evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md` for actual test/native results and limits.

`--vegetation-quality high --vegetation-cycle-proof <fresh-directory>` runs a bounded native
High/Low/Medium/Ultra/High cycle, one new PNG/readback per stage. It cannot be combined with the
environment cycle, other automation or a player probe. The shared harness uses `quality_cycle_*`
events with an explicit domain. Native vegetation cycling has passed; transition latency and
Studio remain unqualified. See `R1_040_VEGETATION_TRANSITION_IN_PROGRESS.md`.

Valley modes accept `--vegetation-quality low|medium|high|ultra` as an initial-only override.
It controls decorative density, LOD/HLOD/cull ranges and grass blades per patch without changing
the authored recipe or collision-bearing population. Omission retains authored presentation.
Invalid or incompatible settings fail before plugin construction. The engine now has an ECS-tested
hot-change API, but this CLI flag still selects only the initial tier; no Studio selector or
global adaptive acknowledgement is implemented by this flag. See
`docs/evidence/gate1/R1_040_VEGETATION_INITIAL_QUALITY.md`.

Native Bevy executable that loads a versioned Forge scene document. With no arguments it generates
the deterministic procedural Gate 0 proof. Automation options are `--capture <png>`,
`--capture-frame <n>`, `--metrics <json>` and `--exit-after-frames <n>`.

For single-frame captures, `--capture-frame` is the earliest eligible frame, not a promise to
capture an unready renderer at that exact frame. The capture waits for eight fresh observations
of a stable, nonempty pipeline cache with no pending/failed entries, a valid window, the complete
governed kit when present, and nonzero opaque primitives when that diagnostic is available.
The original bounded exit is not extended. Missing readiness or readback at exit is an error;
an independent 120-second watchdog bounds capture-only runs. Logs and `capture_readiness` in
metrics distinguish earliest/issued/readback frames. File size is checked only after this run's
readback, so an old output cannot qualify a capture never issued. Pixel correctness still needs
inspection. This guard does not filter frame samples or prewarm future/unseen shader variants.

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

Valley modes accept `--rain-quality low|medium|high|ultra` independently of `--weather clear|rain`.
This overrides only the derived rain mesh budget (2048/4096/8192/16384 drops in the current
profile), not the authored document or the global graphics preset. Omission retains authored
rain quality. Requested and CPU-projected rain tiers are reported separately in weather metrics;
these fields do not prove GPU upload completion or automatic global quality adaptation.

`--shadow-quality low|medium|high|ultra` similarly applies only the shadow section of the
embedded quality profile, bound in memory to the compiled area revision and identity-checked.
It controls cascades, atlas resolution, shadow distance and contact shadows. Without the flag,
the existing vegetation-bounded shadow configuration is retained. It can be combined with
`--rain-quality`, but neither flag declares the other global preset domains applied.

`--post-process-quality low|medium|high|ultra` uses the existing weather adapter to project
the quality policy's bloom, color-grading and tonemapping switches. Low disables bloom;
the shipping presets retain authored weather grading and TonyMcMapface tonemapping.
Disabling grading in a custom validated policy uses neutral grading without changing the source;
disabling tonemapping selects Bevy's explicit `None` mode and does not remove HDR or other effects.
Omission keeps authored behavior. `weather.post_processing.quality` reports the requested tier
and observed CPU projection separately. The older `bloom_requested` remains authored intent,
so it can be true while a Low override makes `bloom_component_present` false.

`--water-quality low|medium|high|ultra` is a partial diagnostic override: Low removes SSR,
while Medium/High/Ultra select 24/48/64 linear steps. Authored water data and shared TAA remain
intact. A `water_quality_partial` warning explicitly reports unavailable reflection-distance
control and screen-space refraction; environment reflections remain unqualified. The nested
water quality ledger always reports `full_preset_applied=false`. Omission retains authored
behavior; this option does not acknowledge a complete water or global graphics preset.

`--environment-quality low|medium|high|ultra` selects a 64/128/256/512 sky-lighting cubemap
through an owned Bevy probe. The camera is preserved; the previous texture is detached while
the new one is prepared. `environment.quality.generated_texture_bound` verifies the CPU image
dimensions and binding, not GPU completion. Volumetric fog remains unavailable with an explicit
warning and the authored distance-fog fallback. This is not a full global preset or Studio control.

`--environment-cycle-proof fresh-directory` with `--environment-quality high` runs a bounded
High/Low/Medium/Ultra/High native cycle and captures each settled stage. Other automation and
player probes are rejected. Five fresh readbacks/files are required; early close fails the proof.
It checks CPU bindings for 180 consecutive observations, not pipeline readiness or seamless
transitions. Inspect the PNGs; this is not a performance benchmark. See the native-cycle evidence.

`--terrain-quality low|medium|high|ultra` opts into derived, distance-selected terrain LODs.
The 64 full-resolution mesh/physics bindings remain intact but their render entities are hidden;
192 separate recentered meshes provide LOD 0/1/2 with matching CPU/shader origins and dither bands.
Only changed chunks rebuild after source updates; quality changes retain entity/mesh identities.
`terrain_lod` metrics report allocated CPU geometry, not active LODs or isolated GPU draw counts.
The legacy overlay's 64 LOD-0 chunks describe canonical caches, not the active rendering levels.
Omission keeps the original rendering. This is a diagnostic per-domain override, not a global
preset. The valley fixture now retains the terrain horizon at every tier (896m outer cull,
bounded final fade); this corrects the earlier floating distant objects in Low. The other
global domains, movement review and Studio integration remain open. See the R1-040 terrain proof.

`--world-sketch-visual-valley` compiles the embedded WorldSketch (or `--world-sketch-input`
source) and automatically installs its nonempty derived bridge placements from the same
compilation as the terrain. No intermediate JSON file is required. The bounded native crossing
proof is `forge_runtime --world-sketch-visual-valley --bridge-crossing-probe`.
An explicit `--bridge-placement <json>` takes precedence; an unreadable or invalid file fails
instead of silently falling back. Gate 0 and historical `--visual-valley` do not gain a bridge.
This native controller proof does not qualify browser traversal or a navigation provider.

The governed kit also supports player exploration: combine `--player-camera` with
`--world-sketch-visual-valley --asset-kit-build <build.json> --asset-kit-preview <preview.json>`.
The player then owns the only camera; the kit retains its placements, provenance checks,
LOD and Rapier colliders but does not spawn its fixed validation camera. Without player or
bridge camera modes, the fixed comparison camera remains the default. The specialized
`--player-camera-probe` remains incompatible with the external kit. A normal interactive run
(without capture/metrics/bounded-exit flags) enables keyboard/mouse controls.
The kit is also compatible with `--bridge-crossing-probe`, with that probe owning the camera.
Interactive native kit playtests wait for all Bevy world instances and registered Rapier
colliders before sampling player input. Loss of readiness blocks input again. File-dependency
loading and physical readiness have separate log events; neither changes source documents.
See the playtest addendum in `docs/evidence/gate1/R1_064_GOVERNED_ASSET_RUNTIME_INTEGRATION.md`.

For repeatable multi-view evidence, `scripts/Capture-WorldSketch.py` runs all existing authored
WorldSketch cameras with clear/rain overrides and the governed asset kit. It requires explicit
runtime/source/kit/preview paths and a new output directory. See
`docs/evidence/gate1/R1_065_FIXED_VIEWS.md` for the executed command and known visual failures.

For offline movement evidence, add `--bridge-frames <fresh-directory>` to
`--bridge-crossing-probe`. The parent directory must exist; an existing destination is refused.
Only this mode uses a fixed 1/30-second rendered-frame clock, a forward-facing bridge camera,
120 render frames of warmup and kit-readiness gating. It waits for screenshot callbacks before
exiting, fails on runtime ERROR, and is bounded to 600 captures and 180 real seconds. Normal
crossing probes retain their real clock, camera and 30-second watchdog. This is not a performance
measurement; a visible label explicitly identifies offline capture. It cannot be combined with
metrics, external exits or the ordinary single-frame capture options.
`scripts/Capture-BridgeVideo.py` orchestrates the run and an explicitly supplied local FFmpeg,
validates consecutive Full HD PNGs, decodes/counts the resulting MP4 frames with FFprobe, hashes
inputs/outputs and preserves a manifest even on failure. FFmpeg is development tooling only,
not a new runtime dependency. Executed clear/rain evidence and review limits:
`docs/evidence/gate1/R1_048_BRIDGE_MOVEMENT_VIDEO.md`.

Automated runs (capture, metrics, bounded exit, resize or close harness) now return code 1 if
any tracing ERROR was emitted during the run but Bevy otherwise returned success. Existing
nonzero exit codes are preserved. An independent ERROR-only subscriber observes failures even
when `RUST_LOG=off`; console filtering still applies to printed logs. WARN alone does not fail a
proof. The guard resolves the verdict at shutdown, not by interrupting the renderer or changing
the interactive window-close lifecycle. It does not detect silent rendering defects. The final
`runtime_stopped` event includes `runtime_errors_observed` and `automated_run`.

The bounded `--walk-proof-measure-moving` report also samples existing Bevy
`render/*/elapsed_gpu` and `elapsed_cpu` diagnostics once per 60 replay ticks,
up to 64 snapshots and 128 paths per snapshot. It records the reading tick/frame,
measurement age and original units, preserving missing/invalid values rather
than turning them into zero. These are asynchronous latest-pass observations,
not GPU frame time or an atomic match to the reading pose. Collection overhead
is included in this instrumented run. No new GPU query or runtime renderer
feature is installed; normal interactive play does not collect these snapshots.
The same moving report reuses the stationary harness's five CPU wall-clock phase
boundaries and stores their latest values beside each pose. An unavailable sample
stays null. `outside_first_to_post_update` includes Last, rendering, presentation
and runner waits; it must not be labeled GPU time or the cost of one subsystem.

The pinned water-medium candidate also supports `--walk-proof-measure`:
stationary player after the bounded walk, with live simulation. Frozen-time
measurements remain rejected for this combined candidate. Capture, metrics,
isolation and adaptation exclusions remain enforced; use a separate capture
run. This admits an acquisition mode, not a successful performance verdict.
