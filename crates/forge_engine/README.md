# forge_engine

## Editor terrain projection

`editor_viewport` projects accepted project terrain prepared by the native Studio service. It reuses
the existing terrain mesh adapter and PBR material, but does not install the embedded Visual Valley
recipe, physics, player, weather, vegetation or runtime proof harness. One camera and one renderer
serve all open areas. Its bounded bridge coalesces requests; a stale generation cannot replace the
current selection. Full loads adapt two chunks per update and reveal all 64 together. Exact-base
partial loads preserve the current scene while staging replacement meshes, swap only those chunks,
and retain the other entities/handles. Empty changes update source/revision/spawn without uploading
terrain. Cancelled staging and replaced assets are explicitly released. Status reports rebuilt,
retained and uploaded mesh counts, not GPU timings. This presentation-only path shares the pure
world cache algorithm; it does not install the separate runtime Rapier transaction/physics adapter.
Up to 256 per-area camera poses survive
tab switches in the same project/page. They are presentation-only state.

Headless tests exercise request replacement/cancellation, asset release, camera restoration and
invalid topology, exact full-build attribute/index parity, retained allocation identity and failed/
cancelled partial staging. These are ECS/CPU proofs, not evidence of GPU presentation. Studio Diagnostics
retains that distinction. The native stroke/undo/redo capture that qualifies R2-014, while explicitly
excluding GPU/performance claims, is recorded in `docs/evidence/gate2/PARTIAL_TERRAIN_REFRESH.md`.

Adapts engine-neutral Forge documents to Bevy entities. `ProofScenePlugin` keeps the Gate 0 scene
API intact. `VisualValleyPlugin` consumes a validated `TerrainDocument`, asks `forge_world` for the
deterministic derived terrain and materializes its 64 chunks without becoming a persistent source of
truth.

The `native` and `web` features are built separately so Cargo cannot accidentally unify platform
requirements during qualification.

The Gate 0 overlay and the candidate `forge.runtime-metrics` v8 ledger keep source and renderer
evidence separate:

- source mesh instances materialized from the document;
- GPU pipeline statistics exposed by Bevy's `RenderDiagnosticsPlugin`, such as opaque-pass clipper
  primitives and shader invocations on supported Vulkan/DX12 adapters;
- GPU draw calls, explicitly reported as unavailable.

The governed `--gate1-gameplay-stutter` route is the deliberate exception: it keeps the same High
scene and `GpuPerformancePlugin`, but selects
`RenderDiagnosticsPolicy::DisabledForExternalPresentationQualification`. Bevy's
`RenderDiagnosticsPlugin` is omitted so its per-frame diagnostics resolve encoder cannot bias the
external PresentMon measurement. The overlay and proof ledgers report render-pass CPU/GPU timing,
pipeline statistics and runtime presented-frame counts as `not_observed`; Bevy application-update
timing remains visible but is never relabeled as presentation timing.

The diagnostics overlay refreshes on a stable 200 ms cadence instead of rebuilding text on every
application update. Its first update is immediate, automation capture/exit/close frames force a
refresh, and displayed GPU, quality or scenery state transitions refresh it early. An unchanged
formatted value does not write the Bevy `Text` component.

Forge's derived vegetation visibility traversal is bounded to a 100 ms wall-clock cadence. Bevy's
visibility systems, rendering, vegetation wind/interaction and player control remain per-frame.
Gate 1 can force a monotonic audit ticket at a qualification boundary and only accepts the boundary
after the post-visibility observer acknowledges it. The historical `observation_frames` metric keeps
counting main-world visibility boundaries; full traversal passes are exposed separately by the audit
cadence resource.

The v8 ledger additionally records:

- optional current-device wgpu allocator suballocations and Bevy mesh-slab capacity, neither of
  which is relabeled as total VRAM or an operating-system budget;
- the render/compute pipelines exercised by the current process and their thirty-render-frame
  stability barrier, which is not a persistent pipeline cache.

Bevy 0.19 has no backend-neutral global draw-call counter. Source meshes, render-phase items,
primitives and shader invocations are therefore never relabeled as draw calls. A reliable draw-call
number requires later renderer instrumentation or an external GPU capture; on Metal, WebGPU and
WebGL2, Bevy 0.19 also limits its built-in render diagnostics to CPU timings.

The metrics report records requested presentation mode, resizability, initial/final dimensions,
backend-confirmed resize events and clean close requests. Presentation mode is deliberately named a
request because a platform surface may select a documented fallback internally.

For governed Visual Valley measurements, v8 also carries the terrain, shadows, vegetation, scenery,
environment, water, weather and quality ledgers. Report generation fails instead of silently
qualifying a run when governed scenery is pending or in procedural fallback, when the current
quality tier has not been applied, when the scenery replacement LOD range differs from the applied
quality preset, or when the pipeline cache was not ready for the whole sampling window. The full
composition is mandatory: terrain, shadows, vegetation, scenery, environment, water, weather and
quality must all be present and internally consistent. The quality ledger must contain its 34 unique
documented fields (5 applied at startup, 16 applied on tier transition and 13 explicitly not
applicable), and a governed report requires at least 120 finite positive post-warmup frame samples.

The native sampling window fingerprints the current/applied quality tier together with scenery load,
replacement and LOD state. That fingerprint must already be stable at the first measured frame and
must not change during the window. Studio uses the same canonical quality-profile SHA-256 in
`source_hashes.quality` that native metrics expose as `quality.profile_hash`. Its scene ledger v7 has
its own frame fingerprint over scene, current/applied tier and quality/scenery LOD ranges; an
incomplete or changed fingerprint resets the 60-frame warm-up and the subsequent 120-frame measure.

The first Gate 1 terrain adapter keeps positions, normals, global UVs and logical material weights in
each `Mesh` with both `MAIN_WORLD` and `RENDER_WORLD` usage. `COLOR.rgb` carries grass/earth/rock
weights into an original `ExtendedMaterial<StandardMaterial, _>` WGSL shader. The shader blends three
procedural PBR palettes; it does not load any texture or external asset. `VisualValleyStats` exposes
derived logical chunk, vertex, triangle, material-layer and current LOD-0 counts in both the overlay
and the optional `visual_valley` section of the metrics ledger. The shared legacy fields count one
canonical terrain document as `source_entities` and the 64 derived Bevy meshes as
`source_mesh_instances`; neither field is a GPU draw-call count. Chunk/LOD boundaries use a dedicated
opt-in gizmo group, never Bevy wireframe rendering.

Grass batch meshes are recentered on their deformation-safe AABB before upload, with the opposite
translation applied to their entity. This preserves world geometry and makes CPU/compute AABB range
checks agree with the vertex shader's origin-based distance fade, including on elevated terrain.
Grass roots now cover a 2.5 m patch radius before authored scale, with the same seven vertices and
three triangles per blade. Exclusions apply to patch centres: at the maximum 1.25 scale, a 3.75 m
wide root-free core remains in the movement corridor and a 10.875 m radius around spawn. These are
root bounds, not a guarantee on the animated leaf envelope. LOD distances, batch counts and defensive
budgets are unchanged; the denser source recipe increases geometry rather than claiming a free gain.

`VisualValleyWeatherPlugin` projects the versioned clear/rain contract without adding an asset or a
second particle dependency. The selected quality tier becomes one procedural cross-quad mesh and one
ECS batch entity; a WGSL vertex pass wraps every seeded streak inside a camera-relative volume. The
public `WeatherRuntimeState` accepts deterministic clear/rain targets. Its scalar wetness is copied to
terrain and vegetation material extensions, where zero is an identity and a completed dry transition
restores the exact authored clear scalar. Bloom and color grading use bounded Bevy 0.19 components.

`WeatherProjectionStats` observes main-world entities, camera components and CPU material uniforms.
It deliberately reports GPU execution, native/WebGPU visual review, splashes/occlusion and advanced
puddle response as deferred; those require the later runtime/Studio capture and qualification lot.

`VisualValleySceneryPlugin` is an optional presentation adapter layered over the deterministic
vegetation plan. It selects a bounded hero-area subset from the canonical placements and loads only
the models declared in the embedded governed-asset manifest. Imported roots remain hidden until all
glTF scenes and recursive dependencies are loaded. Only then are replaced tree Near visuals hidden;
their Medium visuals and shrub/fern Individual visuals take over at each imported root's cutoff,
before the existing complementary HLOD fade. Any failed load leaves the procedural projection
visible. The source documents, placement IDs, collisions and persistence contracts are unchanged.
Every eligible tree in the existing 140 m hero disk uses one of the three already governed CC0 tree
models; shrub/fern/detail selection and the 512-instance cap remain unchanged. Imported glTF materials
are preserved and do not inherit Forge's procedural wind or wetness shader.

Applied quality tiers drive the governed replacement visibility ranges without changing the source
placements: Low `35-82 m`, Medium `43-104 m`, High `51-120 m` and Ultra `67-162 m`. Each cutoff is
derived inside the stable Medium band using the same placement ID. CC0 roots are visible strictly
below the cutoff; procedural relay ranges include it. Ranges are reapplied after an actual quality
transition, not recomputed for every placement each frame. Tier application, procedural relay and
CC0 visibility run in that order before visibility propagation; vegetation shader uniforms consume
the applied tier in the same `PostUpdate`. `hidden_procedural_entities` counts only
permanently hidden Near tree entities; relays remain inherited. The Studio scene
ledger v7 observes both the current and actually applied tier plus the expected replacement LOD
range. Visual Valley readiness remains pending until those values agree with the activated scenery
observation. These are observable contracts; final combined native GPU and browser qualification is
required separately from unit-test evidence.

Derived river banks share the terrain PBR material and weather wetness projection. Five transverse
vertices per bank follow the canonical terrain while retaining the exact CPU water edge and the
outer 1.5 cm clearance. Earth weights blend into canonical terrain weights across the existing 5 m
shoulder (the requested 9 m blend is retained as requested, not claimed fully applied). This is a
visual ribbon, not terrain carving: colliders, river data, permanent wet-edge shading and erosion
remain unchanged. Vertex clearance does not prove triangle-wide clearance or animated wave sealing.

On the current `d9e2670` candidate, `cargo fmt --all -- --check` and strict workspace Clippy pass,
as do 120/120 `forge_engine` library tests and **296/296** workspace/all-target tests. The exact
`forge_studio` Web/WASM check passes. Native Release and Browser clear/rain High were replayed at the
same commit: Browser passes 24/24 controls per weather and the native/Studio comparison passes 46/46.
These bounded fixed-camera runs do not qualify R1-039/R1-040 across all tiers or adaptation.
