# forge_studio

## Current authoring launch and native game

The owner's canonical entry is `E:/OpenRPG_Forge/Lancer-Editeur.cmd`. It opens
the shared `E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002/release/forge-studio.exe`.
Keep this entry and its adjacent `forge-studio.local.json` connected to the same
authoring workspace and deployed assets; do not create a different launch path
per chat. The owner confirmed this entry on 13 October and asked to preserve it.

The native shell reads the sidecar profile independently of the working
directory. Relative paths resolve against the executable directory. Explicit
installer environment overrides keep priority; incomplete module overrides
are refused rather than combined with another profile. The local sidecar and
private map/asset payloads are deployment data, not files included in this commit.

In **Modules**, select the module and then its **Carte**. An animated indicator
remains visible while terrain, models and collisions are prepared. Before
selecting another object, **Appliquer** or **Abandonner l'aperçu** for the current
preview; there is no implicit apply or save.

**Lancer le jeu** exports an immutable snapshot of the current map, including
applied unsaved edits and the authored start, and opens the configured native
runtime in a separate window. The editor keeps its map, camera, canvas and GPU
resources resident while pausing rendering. Closing the game resumes the same
viewport without reloading the page or map. A second native game cannot be
opened from the same Studio while the first is running. The embedded playtest
is retired. The current native runtime supports walking exploration; RPG quest,
dialogue, combat and save integration remain required for Gate 3.

The owner's game/return test passed on 13 October. See
[`STUDIO_RESIDENT_GAME_RETURN_20261013.md`](../../docs/evidence/gate3/STUDIO_RESIDENT_GAME_RETURN_20261013.md)
for binary identity, scope and limits. This is not P05 full-RPG acceptance.

Run the eight browser pacing/pause regression tests from the repository root:

```sh
node --test apps/forge_studio/tests/editor-gpu-pacing.test.cjs
```

## Module and map navigation

Opening an imported map also acquires its deployed terrain and scene resources.
Package hashes and the raster geometry must match the selected map before
projection; a cached library from another raster cannot supply its scene.

The Modules panel has one module selector and one map selector. Selecting a map
opens its managed authoring session directly; the existing workspace is opened
automatically on first browse. The former saved-zone chooser and technical
workspace forms are no longer part of this panel. Names come from the accepted
module references and effective map documents, including unsaved renames.

Browsing never creates a workspace, adopts a legacy map, or saves documents.
The ordinary locks and interrupted-save checks still apply. Switching maps
retains accepted edits; an unapplied preview or an active playtest disables the
selectors. The native game also suspends authoring controls while its snapshot
is being played. Save remains explicit. Missing modules and loading failures have an
empty state or retry action, rather than falling back to legacy editing.

## Lightweight editing view

Editing uses a temporary fast render policy: no sky, shadow maps, animated wind,
ocean simulation, optical water or live cloud/environment updates. Imported
water is a flat, unlit blue plane. Required texture bindings and readiness
checks remain available; selection, transforms, terrain tools and collisions
still use the accepted source content. Plain GLB materials receive simple
daylight without shadow maps, with a brighter temporary inspection ambient.
Interface blur, shadows and transitions are disabled as well.

The policy changes no map, model or saved graphics preference. The separate
native game retains its graphics pipeline. Editing freezes fish animation time
while preserving camera-driven LOD transitions, and only changed material values
are uploaded. Browser redraw callbacks are capped at 60 Hz during loading and
interaction, reduced to two updates per second at rest, and held during native
play without dropping their resources. This cap is not a measurement of
presented FPS. Resolution and pointer coordinates are unchanged.

## Terrain editor review controls

The relief brush is positioned by clicking or dragging in the map; its former
numeric X/Z fields are not part of the compact brush panel. The viewport compass
shows camera bearing and the hovered terrain point in world X/Y/Z metres. A
stroke retains the chosen brush, but another stroke waits for its preview to be
applied or abandoned. The on-screen action journal keeps the latest 32 command
results, including local and native refusals; it is presentation state, not the
canonical command audit. In dual-screen mode the detached atelier holds the
subject panels while the main window hides its dock and side panels. Apply,
Save, camera controls, the compass and the journal remain in the main window.

`scripts/Refresh-StudioGoldCandidate.ps1` rebuilds the Web frontend in the
shared E: Cargo cache before packaging the single review candidate; it refuses
to replace an executable still in use.

## Explicit profile preparation

The quality inspector's “Préparer les quatre profils” button runs the shared
engine preparation for Low/Medium/High/Ultra, then reapplies the original global
profile and adaptation setting. It does not save or edit the map. The visible
status and exported `observations.profile_warmup` distinguish warming, ready,
failure and cancellation. A newer quality/weather choice cancels preparation.
Individual preview overrides are not restored independently of the global profile.
This prepares known pipelines for the current view/weather; it is not a guarantee
that future views or weather transitions will never compile a new pipeline.

## Explicit moving-quality proof

For the distinct prewarmed protocol, package with
`-BridgeProbe -PrewarmedQualityMotionProbe` (`forge_quality_motion=prewarmed`).
The normal five-step `ProfileWarmup` must finish before the bridge starts;
failure/cancellation or the 120-second preparation deadline cannot pass. Once
walking, bridge checks and the movement clock are never paused for quality changes.
The JSON records the preparation separately from the movement phase trace.
This is functional qualification after preparation, not proof that cold-start
stutter disappeared. The cold and prewarmed build flags are mutually exclusive.

Package with `-BridgeProbe -QualityMotionProbe` to request the bounded
`forge_probe=bridge&forge_quality_motion=1` diagnostic. It observes High/Low/High
while the normal bridge controller moves, using the same manual request queue as
the quality buttons. Eight matching GPU-ready frames and nonzero physical motion
are required at each stage. No saved world or asset is changed.
The local diagnostic JSON includes `observations.quality_motion` with the three
poses, source bindings and quality receipts. Its cycle verdict is separate from
the bridge traversal verdict: a successful cycle does not erase a later bridge
watchdog failure. No timing or artistic acceptance is inferred; inspect captures
and preserve failures. Default launches do not install this probe.

The viewport bridge probe now waits for eight fresh, stable render-pipeline
observations before its physics warm-up can start the crossing. Cold WebGPU
compilation stays inside the existing 120-second preparation watchdog; movement
and stopping retain their unpaused 30-second deadline. Quality changes after
departure never suspend the controller or deadline. Native offline recordings
retain their separate render guard and explicitly non-realtime clock.

## Integrated experimental candidate

The scene menu provides “Candidat complet · kit, forêt, eau et ciel”. This reloads
WorldSketch with the governed kit, forest batches, clear-reflections water and
the existing cloud preview. The local kit must be deployed. Isolated routes remain
available; this is neither an authoring command nor a saved environment preset.
The real Tauri menu navigation and PNG/JSON exports were checked on 9 September;
see `docs/evidence/gate1/R1_CLOUD_LAYER_PREVIEW.md`. Artistic acceptance, complete
quality profiles and target performance remain unqualified.

## Experimental balanced shadows

The scene menu offers `forge_shadows=balanced-preview` in valley modes. It selects
an in-memory policy copy with High shadow resolution 2048 and three cascades;
range, contact shadows, other tiers and other domains are unchanged. The normal
route keeps the reference policy. Duplicate/unknown values and Gate 0 are rejected.
The inspector labels this as an unsaved, unqualified candidate. Every quality
adapter receives the same candidate policy, so effective-plan hashes identify it;
this does not remove the governed-forest qualification blocker or enable adaptation.
Changing Low/Medium/Ultra and returning to High retains the selected candidate
policy until a normal scene reload clears the query. No source document is saved.

The explicit menu route `forge_water=clear-medium-preview` loads the shared
synthetic `examples/water/clear-medium.preview.json` into the in-memory water
document before publishing the selected manifest. The water source hash must
identify this input. The previous `depth-preview` route remains available;
unknown/duplicate/non-valley requests are rejected. This reload-only preview
does not edit or save a map and is not a qualified default or authoring control.
See `docs/evidence/gate1/R1_STUDIO_MEDIUM_CONTACT.md` for current proof status.

The scene menu includes explicit experimental transmissive water via
`forge_water=depth-preview`, valley-only and reload-only. Default rendering and
map sources are unchanged; environment-only reflections and unqualified quality
profiles are stated in the UI. Source integration/tests/WASM check PASS; a new
bundle and menu interaction were exercised, exposing an observation-contract
failure. The engine observer is corrected and tested; the corrected Studio
bundle now loads through the menu and exports inspected JSON/PNG. Close water
motion, detailed optical export and profile-status wording remain unqualified.
See `docs/evidence/gate1/R1_STUDIO_WATER_OPTICS.md`.

Visual Valley initially covers the live canvas while environment/kit preparation
and the renderer's pipeline cache settle. Eight fresh stable observations are
required; temporary shader-loading states are pending, fatal errors stay visible,
and a 120-second watchdog reports failure. The observer stops after its terminal
state and does not hide later quality changes. The diagnostic `render_warmup`
ledger is CPU/render-cache evidence, not pixel validation or a GPU fence. See
`R1_STUDIO_INITIAL_RENDER_WARMUP.md` in `docs/evidence/gate1/`.

## Local candidate build (Gate 1 evidence)

Add `-CloudPreview -WaterReflectionPreview` to package the already implemented
integrated cloud/water route. This only selects the existing opt-in URL: no shader,
asset or persisted profile is changed. Omission preserves the historical default.
Use matching cloud inputs for native/Studio parity; a native cloud run and a
cloud-free Studio run are not the same candidate configuration.

From the Rust repository, run `scripts/Build-StudioCandidate.ps1 -Frontend <existing-web-directory>
-ExpectedWasmSha256 <exact-hash> -Output <fresh-E-drive-directory>
[-GovernedKit <existing-kit-directory>]`.
`-BridgeProbe` explicitly packages the existing automated crossing URL, keeping
the same world, kit and forest. The default remains interactive. Both input
validation and the build ledger identify the selected entry URL; the switch
does not qualify movement or rendering and does not accept arbitrary URLs.
`-FixedView <existing-view-id>` selects the existing frozen proof camera instead;
it is mutually exclusive with `-BridgeProbe`. Add `-CandidateView` only when the
frontend contains `forge-views/world-sketch.json`. That file is pinned, and the
Rust loader still validates that only camera fields differ from the canonical
world. These are diagnostic views, not saved edits or performance benchmarks.
Frozen views leave physics registration pending until simulation advances. Their
loading guard requires complete scenes, all LODs and valid collider shapes, but
does not wait for backend registration. Interactive playtest still requires it.
The fixed-view exception requires a valid installed matching camera and paused
virtual time; shader readiness remains mandatory. See `R1_FIXED_VIEW_WARMUP.md`.
`-ValidateOnly` checks inputs without creating output or compiling. The full command snapshots
and hashes every frontend file, uses a relative `frontendDist` to avoid Windows-drive URL
interpretation, builds the desktop shell offline/locked, and retains a build log and JSON proof.
It refuses existing output; it neither launches nor publishes. Supply the kit explicitly with
`-GovernedKit`, or use a frontend that already contains it (never both). The explicit path checks
report technical status, matching kit identities/revisions, GLB hashes, traversal and reparse
points; only the three manifests and declared GLBs are copied and included in the build pins.
These are packaging checks, not a replacement for the full runtime preflight, which remains
authoritative. `Test-StudioCandidateKit.ps1 -Output <fresh-E-drive-directory>` tests the input
checks without compiling, creating a candidate, or claiming fixture GLBs are valid assets.
This embeds an identified existing WASM bundle, not a newly compiled frontend or a claim that
frontend and shell share the latest source revision. See `R1_STUDIO_REPRODUCIBLE_BUILD.md`.

The quality panel exposes an experimental, unsaved adaptation opt-in and a return to
fixed mode at the currently requested tier. Manual Low/Medium/High/Ultra requests also
disable adaptation. Status comes from the engine, not optimistic UI state. Web water
capability blockers can leave adaptation waiting for application; this is not a successful
automatic profile transition. See `R1_STUDIO_ADAPTATION_CONTROLS.md` in the Gate 1 evidence.

`?forge_scene=world-sketch-visual-valley&forge_weather_input=candidate` explicitly
loads `./forge-weather/candidate.json` before starting the viewport. The file is an
optional locally deployed proof input, not bundled by default and not a project save.
The bounded streaming reader allows at most 64 KiB, cancels at 20 seconds and refuses
redirects; the query cannot specify arbitrary URLs. Strict UTF-8/JSON and exact
project/area/revision validation precede replacement of the in-memory weather input.
Rejected input leaves the loaded scene unchanged and blocks viewport startup.
The selected manifest is updated before the runtime starts; the weather panel displays
the candidate hash and read-only status. Existing clear/rain buttons exercise the shared
engine. Their diagnostic includes actual observed solar illuminance and optional sunlight
response (CPU evidence, not a GPU verdict). Reload without the query restores embedded
input; scene navigation also clears preview queries. No hot reload or authoring UI.

## Opening proof scenes

The top-bar **Scènes de preuve** menu opens the existing WorldSketch valley, reference valley,
or Gate 0 scene without hand-editing a URL. Links replace only the current page's query with
the explicit `forge_scene` choice, retaining its path and origin for browser/Tauri routing.
This is a full reload, not hot world replacement: temporary weather/quality choices, kit/forest
previews, fixed cameras and automated probes are reset. The menu states this before navigation.
No project source is saved or modified; the normal default remains Gate 0. This provides access
to Gate 1 proof scenes, not Gate 2 project opening or editing. Tauri navigation requires its own
runtime qualification even when the shared frontend passes a browser check.

The same menu also offers **Vallée avec kit et forêt · candidat expérimental**, an explicit
opt-in to the locally deployed governed kit and forest preview. It uses the existing validated
query path, not asset promotion or a project edit. Missing kit data remains a load error.
The actual menu click and subsequent kit/forest rendering were checked in Chrome; see
`docs/evidence/gate1/R1_INTEGRATED_CANDIDATE_ACCESS.md`. This is not artistic or Tauri acceptance.

## Fixed proof views (qualification only)

On the WorldSketch scene, append `&forge_view=human_bridge` (or another ID already
declared in that document's `validation_cameras`) to reuse the sole active camera with
the authored pose and perspective. Virtual time is paused from startup; player sampling
is disabled. Reload without `forge_view` restores exploration. Unknown IDs, duplicate
parameters, other scene types and combination with `forge_probe` are refused.
No camera/terrain document is edited and no second camera is spawned. The status is
included in diagnostic exports. This frozen mode is **not a performance benchmark**;
UI actions, real-time deadlines and rendering continue. Web optical qualification is
separate from successful camera installation or CPU observations.

To reuse a native proof camera not embedded in the app, additionally opt in with
`&forge_view_input=candidate` and deploy `forge-views/world-sketch.json` locally.
Only its validated camera list may differ from the embedded WorldSketch: every other
field must be identical. The same bounded streaming reader refuses redirects, aborts
after 20 seconds and caps this input at 512 KiB. The candidate's byte hash appears in
the fixed-view status/export. Missing, malformed or different map data block startup;
no source, revision, placement or lock is changed. See `R1_STUDIO_CANDIDATE_VIEWS.md`.

Fixed views also expose an unsaved scene-IBL isolation probe: cut/restore only the sole
active camera's `EnvironmentMapLight.intensity`. Camera and diffuse/specular map identities
must remain unchanged; missing/ambiguous cameras or invalid initial intensity refuse writes.
Direct lighting, water reflectance and documents are untouched. The fixed-view diagnostic
reports intensity observed before application separately from the requested value; this is
CPU evidence only. For an optical IBL comparison, disable SSR using the existing water
control first and retain identical camera, profile and source hashes across all three states.
This probe affects IBL for the whole scene, not only water. Reload restores normal lighting.

The quality inspector includes a collapsible “Profil effectif et limites” diagnostic.
The downloaded diagnostic also includes `observations.volumetric`, copied from the
existing engine-observation ledger: requested steps, applied preview, distance bound
when present, and fallback. An absent ledger exports null, never a successful effect.
These are CPU observations, not executed GPU steps or artistic acceptance.
It presents the engine's ADR-0011 candidate substitutions and outstanding capabilities,
not an installed profile or GPU acknowledgement. No source document is changed.
See `docs/evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md` at repository root for qualification.

## Experimental governed forest (wiring verified, browser qualification pending)

Add `&forge_forest=preview` to the WorldSketch `forge_kit=governed` URL to request the
same shared forest projector as the native runtime. Deploy `prototype-catalog.json` alongside
`forge-kit/build.json` and `preview.json`; it must pin that exact kit revision, GLB hashes and
vegetation hash. The existing bounded, same-origin transport and GLB preflight remain in use.
No extra mesh download, canonical placement copy or source mutation is introduced.
Without this option the manual kit preview stays unchanged. Missing/duplicate/invalid options
are refused. The status distinguishes preparation, failure and projected primitive counts;
projection is explicitly labelled unqualified. A catalog failure is not a successful fallback.

Rust/WASM checks and query parser tests cover the wiring. A fresh full Web build and Chrome launch
now observe 636 projected trees/7,632 primitives with the revision-2 candidate kit. Capture,
material/LOD/animation inspection and native/Web comparison are still required; textual ECS
readiness is not GPU presentation. No Tauri qualification claimed.

## Automated bridge qualification (not interactive acceptance)

Add `&forge_probe=bridge` to the governed WorldSketch kit preview URL below to run the shared
engine crossing probe. Keep the tab foreground. It waits for kit physics readiness, retains its
30-second watchdog, displays the result in `studio-bridge-probe-status`, and leaves the scene open.
Manual movement is disabled for this explicit mode; reload without `forge_probe` to explore.
No project edits, save, import UI, navigation qualification or human acceptance are implied.

## Governed kit preview (Gate 1, opt-in)

`?forge_scene=world-sketch-visual-valley&forge_kit=governed` loads a locally deployed kit before
starting Bevy. Place the original build report at `forge-kit/build.json`, the matching preview
at `forge-kit/preview.json`, and each GLB at its declared relative path below `forge-kit/`, beside
the served `index.html`. This is a read-only development preview, not a general import UI or
a distributable module package. The normal URL does not fetch a kit.

Contracts/provenance, source pins, SHA-256 and the shared self-contained GLB check run before
rendering. Redirects and HTTP cache reuse are disabled. Bevy reads only the verified bytes through
its memory asset reader; the deployment files are not fetched a second time by the renderer.
Input remains gated until instances and physical colliders are ready. `studio-kit-status` shows
readiness or an explicit refusal. A bad kit prevents viewport startup instead of silently falling
back to a scene without it.

Limits: 1 MiB per report, 64 MiB per GLB, 256 MiB total admitted GLBs. Each response is consumed
as a stream with a per-file size check before appending a chunk to the WASM buffer. An abort
controller cancels failures and a 20-second browser timer covers headers and body together.
Browser networking/chunk buffers are outside this WASM buffer limit; background timer throttling
may delay the deadline. There is not yet a folder picker, hot replacement, save command or Tauri deployment proof.
The bundled kit must be locally deployed separately; no GLB is added to Git by this implementation.
See `docs/evidence/gate1/R1_064_STUDIO_GOVERNED_KIT.md` for browser evidence and remaining limits.

Valley modes now install High quality initially and expose Low/Medium/High/Ultra preview buttons.
They request the existing engine coordinator without saving sources or recreating the viewport.
The status distinguishes pending budgets from available CPU projections and explicitly retains
partial water/environment support. `studio-quality-ledger` exposes those typed observations.
Water tint can also be previewed on/off or returned to the selected profile without saving.
In the forward optical candidate, independent refraction controls select the approximate
transmission fallback or return to the effective profile's ray budget. They preserve
reflection settings and authored sources. `studio-water-refraction-status` reports the
observed CPU material budget, not GPU qualification; the diagnostic ledger includes
`screen_refraction_disabled_preview`. Legacy tint controls remain unavailable there.
The three buttons report observed material state through `studio-water-tint-status`, not
an optimistic click acknowledgement. Reflection preview now offers disable/follow-profile
through `studio-water-reflection-status`. Tint and reflection have separate pending requests
so changing one cannot discard a pending request for the other. Both survive tier changes
without altering the policy; reload restores the normal initial profile. Browser interaction
is verified, but the close-up water-pixel comparison remains due.
The real embedded-browser cycle and weather coexistence are verified in
`docs/evidence/gate1/R1_040_STUDIO_QUALITY.md`; Full HD, Tauri and automatic adaptation remain open.

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

`?forge_scene=world-sketch-visual-valley` is a separate explicit choice for the locked embedded
WorldSketch. It uses the shared compiler's carved terrain, scatter and primary-river conversion,
then the same six engine adapters. Clear/rain preview and player input also apply. Source hashes
are checked against the actual runtime projections, and the prepared scene is loaded only once.
This is a diagnostic source-parity path. The embedded WorldSketch revision 2 now includes the
authorized broad valley grading and explicit river altitudes, qualified technically in native runs.
It does not load the rejected 40 m cut-only candidate or the external Blender kit.
It now retains and installs the bridge placements returned by that same compilation, using the
existing Bevy/Rapier bridge adapter without a preview camera or duplicate water mesh. No bridge
JSON path is required in the browser. Gate 0 and the historical Visual Valley mode remain unchanged.
The inspector exposes a separate `studio-bridge-projection-ledger` with the observed engine hash
and CPU spawn counts. This does not extend the six-profile parity manifest or claim that a player
crossed the bridge in the browser; `browser_crossing_verified` remains false.
Artistic acceptance is not implied by a successful launch; Gate 1 remains open.

`forge_scene=visual-valley` loads the embedded terrain, vegetation, environment, player-controller,
water and weather documents, records their individual canonical hashes, and installs the same Bevy
adapters used by the native runtime. The player controller owns the single requested camera. Clear is
the default weather state; append `&forge_weather=rain` to start directly in the authored rain state:

```text
http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=rain
```

`forge_weather` is strict: only `clear` and `rain` are accepted, the parameter may appear only once,
and it is rejected outside `visual-valley`. An invalid query is rendered as a configuration error and
the viewport is not started; it never falls back silently to clear weather.

Visual Valley now connects the existing player controls to the viewport. Bring the browser tab to
the foreground and click the canvas to request pointer lock. Physical WASD keys (ZQSD on AZERTY)
move, Shift sprints, Space jumps, mouse motion looks, and V switches the existing camera mode.
Escape explicitly releases pointer lock; panels regain normal input. The shared engine sampler is
enabled only while this canvas owns pointer lock and the document has focus. Loss of ownership
clears both held and queued actions without resetting command sequence numbers. A rejected pointer
lock request never enables controls. This explores the embedded proof scene only; it is not Gate 2
playtest authoring, project persistence, or a new canonical world state.

The Visual Valley inspector also offers clear/rain preview buttons after runtime readiness.
They request the existing engine weather transition without reloading the world or changing
the source profile. Only the latest request pending consumption is retained. The live CPU/ECS
projection is published at up to 4 Hz in `#studio-weather-preview-ledger`, separately from the
startup scene manifest. The visible wetness value is observed, not an optimistic button state.
Preview changes are not saved and are not project edits; reload uses the original query/profile.

The dedicated `#forge-scene-telemetry-ledger` records the selected and runtime source manifests, the
first positive Bevy real-time update timestamp, then the camera topology and projections observed when
the runtime contract becomes ready. The runtime manifest is rebuilt from the source hashes exposed by
the engine resources actually installed in the Bevy world, rather than copied from the selected
manifest. Invalid water, weather or environment statuses fail immediately; missing or `not_observed`
projections fail after a bounded 10-second timeout measured from the first pending observation.

This CPU/ECS event is deliberately labelled as a runtime observation. Its serialized GPU presentation
status remains `not_observed`; it does not claim a presented WebGPU frame or visual inspection. The
legacy Gate 0 ledger keeps its existing `WebGPU prêt` milestone for the unchanged Gate 0 proof path,
while Gate 1 reports `Runtime ECS observé · présentation GPU non observée`. Compilation and unit tests
prove contracts and target compatibility only; a browser or WebView launch plus a dedicated
presentation/capture proof is still required to observe GPU presentation.

## Local qualification exports

Gate 1 starts with the large in-viewport metrics overlay hidden. The inspector's
`Afficher les métriques dans la vue` / `Masquer les métriques dans la vue` button
changes presentation only; diagnostic collection and the JSON ledgers stay active.
Its pressed state follows the engine resource, not an assumed completed click.
No scene rebuild or project write occurs. Native and Gate 0 retain the visible default.

The inspector exposes diagnostic JSON and viewport PNG download requests. JSON
snapshots existing ledgers with explicit nulls and never promotes partial or failed
observations to gate acceptance. Input and serialized output are limited to 1 MiB.
PNG uses Bevy's screenshot observer, one pending request, at most 3840 × 2160,
and a 15-second observed runtime timeout. Neither action saves a project.

The UI deliberately says download requested, not file saved. JSON and PNG are not
an atomic pair. Browser GPU readback has been observed; receipt of downloaded
files remains unverified. See `docs/evidence/gate1/R1_STUDIO_PROOF_EXPORT.md` at
repository root for exact tests and limitations.

## Gate 0 diagnostic protocol

The page exposes a machine-readable JSON ledger in `#gate0-telemetry-ledger`. Its contract is
`gate0-telemetry.schema.json`; the ledger records navigation-relative loading milestones, optional
Chromium heap data, WASM linear memory, 60 warmup frames followed by 120 measured Bevy real-time
update-loop frames, and handler-to-post-paint latency measured over two animation-frame callbacks.
GPU timestamp queries are not available in this foundation, so the frame distribution must not be
presented as GPU time.

The viewport toolbar shows the last observed CSS client size and canvas rendering-buffer size,
not a fixed 1080p label. Historical `canvas_width_px`/`canvas_height_px` fields describe CSS pixels;
optional `render_width_px`/`render_height_px` read the canvas `width`/`height` attributes. Missing
or zero dimensions are displayed as unmeasured. Old v1 ledgers remain readable without the new
fields. Measurements are refreshed by existing telemetry/interaction events, not a continuous
resize observer, and the toolbar explicitly says "dernier relevé".

`first_real_frame_ms` is emitted on the first positive `Time<Real>` delta observed by a Bevy
`Update` system. It proves that the Bevy loop reached an actual timed update after renderer startup;
it does not prove that the GPU presented that frame. Visual presentation still requires a capture
and inspection.

`runtime_ready_ms` is separate: it records when the selected scene contract, source hashes, camera
topology, and required CPU/ECS projections became observable. It must never replace or rewrite the
first-frame timestamp, and neither field claims GPU presentation.

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
# Bibliothèque privée d’assets

Le bouton **Bibliothèque d’assets** de l’atelier principal et secondaire ouvre un
écran dédié, sans démonter la carte ni perdre les retouches. Une ouverture directe
est également possible avec `?asset_library=1` dans le frontend Tauri.

La recherche couvre les assets déjà indexés du Vault désigné au démarrage par
`OPENRPG_ASSET_VAULT` (sur cette machine : `F:\OpenRPG_AssetVault`). Elle combine
texte sans distinction d’accents, pack, famille, type, sous-type et état de
vérification, ainsi qu’un filtre de disponibilité distinguant les modèles GLB
utilisables des sources conservées ; tri par nom, triangles ou hauteur ; pagination
de 24 vignettes.
Chaque fiche expose descriptions, dimensions, pivot proposé, points d’attache,
confiance, vérifications enregistrées, licence, provenance et métadonnées complètes.
Une source sans géométrie visible ou une dépendance incomplète reste trouvable avec
son FBX et sa provenance, mais porte explicitement le statut non plaçable et ne reçoit
pas de faux GLB.
Les dimensions et triangles mesurés directement dans un FBX sont étiquetés « source ».
Les aperçus neutres de géométrie n'impliquent ni reproduction des matériaux Unity
ni qualification du prefab complet.

Le pont `read_asset_library` est en lecture seule : IDs et filtres, aucune écriture
ou import dans le module. Les fichiers restent dans le Vault privé, hors bundle.
Les métadonnées et PNG sont contrôlés par leurs empreintes enregistrées ; les
sources conservées sont contrôlées par SHA-256 à l'ouverture de leur fiche, sans
relire tous les FBX pendant la recherche. Le contrôle intégral reste la commande
`source_support_import.py verify`. Les requêtes de détail/vignette sont liées à
la révision du registre. « Actualiser »
relit le catalogue et son registre immuable de sources conservées. Une fiche complète
ne vaut ni validation de placement ni qualification dans le moteur ; la collection
source peut contenir d’autres assets qui ne sont pas encore convertis/indexés.

Le contrôle opt-in de toute la collection est :

```powershell
$env:OPENRPG_ASSET_VAULT = 'F:\OpenRPG_AssetVault'
cargo test -p forge_studio --lib --no-default-features live_vault_library --offline -- --ignored --nocapture
```

# Aperçu du ciel (Gate 1, expérimental)

Le menu des scènes propose « Vallée avec ciel expérimental ». L'option explicite
`forge_clouds=preview` peut aussi compléter une URL de vallée avec kit/forêt et vue
fixe. Elle est refusée sur Gate 0, avec une valeur inconnue ou si elle est répétée.
Sans ce paramètre, aucun plugin nuageux n'est installé.

Les réglages embarqués proviennent de `examples/visual_valley/cloud-layer-preview.json` ;
le runtime peut lire ce même fichier via `--cloud-layer-preview`. Ce candidat n'est
pas un profil d'environnement persistant ni une mutation du projet. Le diagnostic
JSON inclut `observations.cloud_preview` et son statut affiché, ou null si absent.
La visibilité CPU et le décalage UV ne prouvent pas une exécution GPU : le champ
`gpu_execution_verified` reste false. Aucune acceptation artistique ou de gate déduite.

### Transport physique de la bibliothèque privée

`read_asset_physics` lit seulement un paquet déclaré par la révision de la fiche,
avec les pins SHA du modèle et du manifeste. Il n'accepte aucun chemin du navigateur.
Le lecteur vérifie le paquet, les buffers et les observations du carrier ; le bouton
« Données de collision » affiche les composants conservés sans qualifier la simulation.

Le host partagé propose `install_physics_package_with_settings`. Les paramètres de
scène cible exigent une matrice symétrique de 32 couches, une épaisseur de contact
explicite et une référence de preuve. Les valeurs de matériau mesurées sont reprises
uniquement lorsque frictions statique/dynamique sont identiques et les combinaisons
Average. Matériaux explicites non transportés et surcharges de couches restent refusés.
Le contact offset source reste conservé ; il ne devient pas implicitement contact skin.
Le parcours d'aperçu transmet le paquet déclaré au même host, sous les mêmes nodes,
avec les paramètres explicites partagés par la scène. Sans ces paramètres, le refus
précède la création du root. Les réglages sont conservés dans `ZoneCorrections.physics`
par la commande `prepare_physics` du même `ZoneAuthoringHead` (version 15 lorsqu'ils
sont présents). Les archives normales, compactes et delta utilisent respectivement
les versions 40, 41 et 42 ; l'absence du champ conserve les formats antérieurs.
Validation, réouverture et annulation/rétablissement passent par la session existante.
Studio projette les réglages acceptés vers la ressource de scène et invalide l'aperçu
lorsqu'ils changent. L'onglet Projet contient « Collisions de la scène » : choix
explicite de la matrice, interactions symétriques par couche, épaisseur et origine
du réglage. « Examiner » produit une proposition ; Appliquer puis Sauvegarder
conservent le réglage, et le retrait suit le même parcours. Aucun brouillon n'écrit
directement dans la tête acceptée. L'utilisation réelle du formulaire, le placement, le rendu et
équivalence de simulation Unity restent à qualifier ; le placement d'un modèle avec
paquet déclaré reste refusé tant que sa réouverture physique n'est pas raccordée.
Aucun réglage du projet privé d'acquisition n'est
présenté comme un réglage du projet original du pack.
