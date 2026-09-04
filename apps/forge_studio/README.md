# forge_studio

Tauri 2 desktop shell with a Leptos CSR interface and a Bevy WebGPU canvas. The hierarchy,
selection and IPC contracts are Rust types shared with the backend. UI rerenders do not recreate the
Bevy world: the viewport is started once and owns a stable canvas.

`trunk serve --config Trunk.toml` serves the browser proof. `cargo tauri dev --no-watch` launches the
same frontend inside WebView2 after the documented Gate 0 bootstrap.

## Gate 2 project sources (first authoring slice)

R2-001 is qualified by the native create/undo/redo/save/close/reopen workflow documented in
`docs/evidence/gate2/R2_001_PROJECT_SOURCES.md`. For an embedded offline Release after building the
Trunk frontend, run from the repository root:
`cargo build --release -p forge_studio --features tauri/custom-protocol --locked --offline`.
Without `tauri/custom-protocol`, direct Cargo builds retain the development-server URL.

The default URL and `?forge_editor=desktop` open the golden-glass desktop. The opt-in
`?forge_editor=project` route retains the standalone source-management assistant. Gate 2 is active
under `docs/ADR/ADR-0011-gate2-entry-with-gate1-reservations.md`; this does not qualify Gate 1 or
complete Gate 2. Explicit Gate 0 and Visual Valley proof routes remain separate.

### Shared desktop shell

The desktop adds top menus, a dock, and movable/resizable/minimizable/maximizable windows.
Projets is mounted once; hiding its window retains form fields and does not issue `Close`.
Monde owns the project viewport; Diagnostics observes that same renderer rather than starting a
second Bevy app. Subsequent hides use `visibility:hidden` and `inert`, retaining the canvas and its
positive layout dimensions. Explicit Gate 0/1 proof URLs remain independent and unchanged.
The renderer does not request initial keyboard focus. Assets/Personnages/Narration/Playtest remain
labelled unavailable.

Menu undo/redo uses the same `WorkspaceActions` as the form toolbar. File commands bring the real
forms into focus; saving still requires an explicit destination. Ctrl+S focuses that destination;
Ctrl+Z / Ctrl+Shift+Z operate the authoring history only outside editable fields in Projets or Monde.
Arrow keys on the window title move it, and on the resize handle resize it. Escape dismisses menus.
No dependencies, Tauri capabilities, remote assets, or JavaScript business logic were added.

Window geometry and unsubmitted form drafts live only for the current page lifetime. Native exit
protects accepted dirty documents, not unsubmitted form text; process termination, reload and crash
recovery remain outside this slice. Hidden Diagnostics keeps rendering; hiding is not a GPU pause.
The CSS wallpaper and vector glyphs are original local code, inspired by the user-selected style.

Creation starts with **one project, one module and one exterior area**, with a 512 × 512 m
terrain source. Project/module/area names, the terrain seed and the X/Y/Z spawn coordinates are
entered through labelled fields. Seed and coordinate strings are parsed and validated by Rust.
Spawn is authored metadata only: creation does not snap it to the ground or place a character.
The hierarchy selects the project, module or area and displays the accepted document's metadata.
The v2 authoring slice adds modules and exterior areas through the same native commands. Select a
module (or one of its areas) in the hierarchy to choose the destination of a new area. New modules
start empty. The inspector edits the selected project's/module's name, or the selected area's name
and explicit spawn coordinates. IDs, not names, identify the targets; neighbouring areas are intact.
Area tabs and the project viewport are implemented by `area_tabs.rs`, `world_ui.rs` and the shared
`forge_engine::editor_viewport` adapter. See the project viewport contract below for limits.

There is **no live 3D viewport in the standalone source assistant**. Its links open the unchanged proof scenes, not a
rendering of the newly created project. Terrain brushes, picking, gizmos and playtest are not part
of this source-management slice.

### Project viewport and area tabs

Select an area in Projets and use **Ouvrir dans Monde**, or select it in Monde's source browser.
Tabs use project/module/area IDs, deduplicate repeated opens, and reconcile against accepted source
documents after undo/redo/open/close. Closing a tab never deletes its area. The current page retains
per-area orbit cameras; tabs/cameras are presentation state, not saved project content.

`StudioCommand::AreaPreview` reads the accepted native source at an explicit revision. It refuses
foreign IDs and stale revisions, copies the source under the session lock, then generates the exact
existing 64-chunk terrain on Tauri's blocking pool. One native generation is permitted at a time;
the UI coalesces rapid selection changes and discards obsolete responses. The derived JSON transfer
is capped at 24 MiB and never saved. This initial transfer includes the existing generated terrain
contract, including unused collider data; no new dependency or second terrain algorithm is added.

The Bevy adapter clears old geometry on invalidation, uploads two derived meshes per update, and
reveals the new area only when all 64 chunks are materialized. Removed mesh assets are released.
It reuses Forge's terrain PBR shader/material and starts only one app/canvas per desktop page.
Pointer input is attached to that canvas only: drag to orbit, Shift-drag to pan, wheel to zoom;
overview and spawn-focus buttons are also provided. The gold cube is authored spawn metadata,
not a character or a surface-snapped position; it may be below the terrain.

Diagnostics distinguishes renderer startup, applied request/revision/source hash, chunk count,
camera pose and CPU loop timing from GPU presentation/timestamps, which remain unobserved by the
ledger. Actual rendered output requires native captures. General command bus, entity picking,
playtest, crash recovery and performance qualification remain outside this viewport slice.

### Terrain brushes in Monde

The native desktop exposes raise/lower, smooth, flatten, seeded noise and slope controls. The
golden ring follows actual terrain mesh hits; its white vertical marker represents strength.
Select Navigation for camera-only input, or hold Alt while dragging to orbit with a brush selected.
A brush drag accumulates at most 64 stamps and commits on release as one typed command, revision
and undo entry. There is no live height deformation while dragging: only the circle/stamp count.
Escape, pointer cancellation and source-generation changes cancel a pending stroke. Start-point
sampling is preserved independently from later pointer updates. Stale hits and invalid parameters
cannot mutate the source. Brush picking uses inherited visibility after transform/visibility
propagation, excludes metadata markers, and does not require a transient per-view visibility flag.

Native mode/history/save/restart evidence is in `docs/evidence/gate2/BRUSH_VIEWPORT.md`.
This qualifies R2-007..011 and R2-013, not the whole gate. Each accepted stroke still regenerates
all 64 chunks; partial CPU caches are not connected to this renderer (R2-014 remains open).
High-amplitude noise produces sharp spikes; natural terrain composition is not claimed.
Material painting, live dry-run geometry, vegetation brushes and playtest are not implemented here.

### Native commands and draft state

`WorkspaceCommand::SculptTerrain` now exposes the shared height-brush command to the native service:
an exact area/revision request, typed brush and explicit `dry_run` flag. Dry-run computes a candidate
receipt in the returned projection without changing the session, dirty state or history. Apply
recomputes against the still-current revision and commits through the same bounded history as the
existing edits. Receipts include brush intent, resolved source patch, before/after hashes and inverse;
serialization completes before mutation. This is an in-memory receipt, not a durable audit journal.
Native service tests exercise preview/apply/undo/redo/immutable save/fresh-session reopen and IPC
round-trip. Compound pointer strokes use the same service and bounded history; the backend-only
foundation is recorded in `docs/evidence/gate2/BRUSH_COMMANDS.md`, with native UI qualification and
remaining limits in `docs/evidence/gate2/BRUSH_VIEWPORT.md`.

The native Rust `WorkspaceSession` is authoritative. Leptos holds form drafts and read projections,
not a second editable canonical project. The shared IPC envelope is
`StudioCommand::Workspace { request }` through `execute_studio_command`; responses carry
`WorkspaceState` and the canonical `forge_project::WorkspaceDocument` JSON.

- `Create` produces a validated in-memory draft, clearly marked **non enregistré**. It writes no
  file and refuses to replace an already open document.
- `Undo`, `Redo` and `Save` carry the observed `base_revision`; stale requests are rejected.
  For a fresh creation the bounded cycle is revision **1 → 2 → 3**: creation at 1, no current
  document after undo at session revision 2, then the restored document and terrain at revision 3.
  A shared history now covers creation, module/area additions and accepted metadata edits, with
  at most 32 snapshots across undo and redo. A new edit after undo invalidates redo; invalid/stale
  commands leave both stacks unchanged. Undo/redo restamp the restored document and terrain revisions.
  Unsubmitted form text is not part of this history.
  Neither undo nor redo deletes previously saved files.
- `Close` clears a clean session without deleting files. `Close` and `Open` reject an unsaved draft;
  first save it or explicitly undo its creation. Reopening starts without an undo/redo history.
- Pending operations disable mutation buttons. Errors are displayed explicitly; a failed request
  is not presented as a successful creation or save.

The native window-close guard also blocks an unsaved, busy or unverifiable session. Its feedback
appears in the window title, including when a proof scene is open; no browser event permission is
added for this guard. It is not protection against process termination or a crash.

`#workspace-state-ledger` exposes the returned state, revision and latest receipt for bounded
automation. It is not a GPU, crash-recovery or full transaction-bus qualification.

### Explicit immutable snapshots

For **Enregistrer**, enter an existing local directory by absolute path. The backend validates the
path and creates `<project-id>-r<revision>.forge.json` in that directory; it does not create folders
or accept an arbitrary output filename. Path checks reject parent traversal, symlinks and reparse
points, and on Windows reject UNC/device prefixes. Each project/revision destination is immutable:
saving identical canonical content again is idempotent; conflicting content fails without overwriting it.

The implementation writes and synchronizes an exclusively created `.forge-pending-*` file in the
chosen directory, publishes it with a non-replacing hard link, then removes only its own temporary
file. A filesystem unable to perform that publication returns an error, not an overwrite fallback.
For **Ouvrir**, enter the exact absolute path of an existing `.forge.json` file. Its size, UTF-8,
schema and document invariants are checked before it becomes the current project.

Opening a v1 snapshot migrates its single module/area to v2 collections in memory, preserving IDs,
revision and terrain. It does not rewrite the file or mark the document dirty. Saving the same
project/revision into the same directory accepts semantically identical v1 content without replacing
it. A later edit writes a new v2 revision. Old v1 byte hashes are not v2 canonical hashes.

Pending files are **not a recovery journal**. They may remain after interruption; this slice does
not discover, promote or restore them automatically. No crash/power-loss recovery, durable audit
history or complete Gate 2 command bus is claimed. The latest edit receipt contains typed parameters,
base/new revisions, before/after hashes and an inverse snapshot; history is session-only.

The metadata form reloads accepted values on selection or document change, not on a save/status-only
response. Module/area creation drafts remain mounted across those operations and desktop hiding.

In a standalone Browser, the forms remain consultable but creation, undo/redo, save, open and close
are disabled with an explicit explanation that the Tauri backend is absent. There is no mock
workspace, browser-storage substitute or simulated success. A Browser UI check cannot qualify
native filesystem persistence; that requires the actual Tauri workflow and saved-file evidence.

## Embedded proof scenes

`?forge_scene=gate0` retains the standalone Gate 0 proof. Visual Valley is an explicit,
reload-only viewport choice (not an in-desktop navigation action):

```powershell
pwsh -NoProfile -File scripts/Invoke-StudioTrunk.ps1 -Action serve
# Then open http://127.0.0.1:1420/?forge_scene=visual-valley
```

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

## Gate 1 Browser weather matrix (proof-only)

The opt-in `forge_gate1_weather_proof=v1` route is reserved for the bounded R1-023/R1-025/R1-026
protocol. It is not an interactive product path. It accepts only `visual-valley`, initial `clear`
weather, and a fixed `low` or `high` quality tier. Invalid, duplicated or broader selections fail
closed before the viewport starts.

Within one Bevy/WASM instance per tier, the Rust controller holds a deterministic water-facing
camera and records `initial_clear → rain → restored_clear`. Each checkpoint requires eight
consecutive Bevy updates at the exact CPU/ECS endpoint; the external CDP driver alone requests the
two ordered transitions and performs captures. The versioned contract and non-claims are documented
in `docs/evidence/gate1/R1_023_026_BROWSER_WEATHER_MATRIX_PROTOCOL.md`.

The matrix was acquired once, Low then High, from clean exact commit
`534ce4faef69972dc1a495981387ac24a3ca1cfd`. The external driver recorded six checkpoints, six page
captures, six canvas captures, 4/4 exact served bundles, and clean console/network results. Its
automated status is `pass_pending_human_and_precise_gpu_evidence`; the evidence remains outside Git.
The controller and captures do not prove shader consumption, water draw attribution, an exact render
frame, GPU presentation or human acceptance, and they do not close any Gate 1 route.

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
