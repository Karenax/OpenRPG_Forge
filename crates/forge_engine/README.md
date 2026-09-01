# forge_engine

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
documented fields (5 applied at startup, 14 applied on tier transition and 15 explicitly not
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
glTF scenes and recursive dependencies are loaded. Only then are corresponding procedural visuals
hidden; any failed load leaves the procedural projection visible. The source documents, placement
IDs, collisions and persistence contracts are unchanged.

Applied quality tiers drive the governed replacement visibility ranges without changing the source
placements: Low `48-128 m`, Medium `60-160 m`, High `72-180 m` and Ultra `96-240 m`. The Studio scene
ledger v7 observes both the current and actually applied tier plus the expected replacement LOD
range. Visual Valley readiness remains pending until those values agree with the activated scenery
observation. These are observable contracts; final combined native GPU and browser qualification is
required separately from unit-test evidence.

On the current `d9e2670` candidate, `cargo fmt --all -- --check` and strict workspace Clippy pass,
as do 120/120 `forge_engine` library tests and **296/296** workspace/all-target tests. The exact
`forge_studio` Web/WASM check passes. Native Release and Browser clear/rain High were replayed at the
same commit: Browser passes 24/24 controls per weather and the native/Studio comparison passes 46/46.
These bounded fixed-camera runs do not qualify R1-039/R1-040 across all tiers or adaptation.
