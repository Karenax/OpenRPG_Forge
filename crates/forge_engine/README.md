# forge_engine

Adapts the engine-neutral `SceneProofDocument` to Bevy entities. It owns rendering diagnostics and
the procedural Gate 0 scene, but never becomes the persistent source of truth.

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
