# forge_engine

Adapts the engine-neutral `SceneProofDocument` to Bevy entities. It owns rendering diagnostics and
the procedural Gate 0 scene, but never becomes the persistent source of truth.

The `native` and `web` features are built separately so Cargo cannot accidentally unify platform
requirements during qualification.
