# forge_engine

Adapts engine-neutral Forge documents to Bevy entities. `ProofScenePlugin` keeps the Gate 0 scene
API intact. `VisualValleyPlugin` consumes a validated `TerrainDocument`, asks `forge_world` for the
deterministic derived terrain and materializes its 64 chunks without becoming a persistent source of
truth.

The `native` and `web` features are built separately so Cargo cannot accidentally unify platform
requirements during qualification.

The Gate 0 overlay and `forge.runtime-metrics` v2 report separate three different facts:

- source mesh instances materialized from the document;
- GPU pipeline statistics exposed by Bevy's `RenderDiagnosticsPlugin`, such as opaque-pass clipper
  primitives and shader invocations on supported Vulkan/DX12 adapters;
- GPU draw calls, explicitly reported as unavailable.

Bevy 0.19 has no backend-neutral global draw-call counter. Source meshes, render-phase items,
primitives and shader invocations are therefore never relabeled as draw calls. A reliable draw-call
number requires later renderer instrumentation or an external GPU capture; on Metal, WebGPU and
WebGL2, Bevy 0.19 also limits its built-in render diagnostics to CPU timings.

The metrics report records requested presentation mode, resizability, initial/final dimensions,
backend-confirmed resize events and clean close requests. Presentation mode is deliberately named a
request because a platform surface may select a documented fallback internally.

The first Gate 1 terrain adapter keeps positions, normals, global UVs and logical material weights in
each `Mesh` with both `MAIN_WORLD` and `RENDER_WORLD` usage. `COLOR.rgb` carries grass/earth/rock
weights into an original `ExtendedMaterial<StandardMaterial, _>` WGSL shader. The shader blends three
procedural PBR palettes; it does not load any texture or external asset. `VisualValleyStats` exposes
derived logical chunk, vertex, triangle, material-layer and current LOD-0 counts in both the overlay
and the optional `visual_valley` section of the metrics ledger. The shared legacy fields count one
canonical terrain document as `source_entities` and the 64 derived Bevy meshes as
`source_mesh_instances`; neither field is a GPU draw-call count. Chunk/LOD boundaries use a dedicated
opt-in gizmo group, never Bevy wireframe rendering.
