# Cadle reuse matrix

Date: 2026-08-30

Source boundary: package version `0.6.0` at commit
`ea0da62549980816a6a27e08b75808b1f56d70e7`. This is a planning matrix, not authorization to copy
code. Gate C copies no Cadle code or asset into Forge.

Classifications:

- **method**: retain test ideas, parameters or observable behavior only;
- **port later**: a source-level port may be considered after provenance and SPDX review;
- **rewrite**: reproduce behavior in Rust/Bevy/WGSL without copying implementation;
- **defer**: outside R0/R1;
- **exclude**: never import.

| Cadle source | Inventory / role | Decision | Future Forge destination | Attribution and proof before acceptance |
|---|---|---|---|---|
| `tools/` (234 files) | automation, invariants, capture and performance harness | method | future Forge proof tooling | Preserve scene parameters and output schema ideas only; tests must prove equivalent native/WASM captures. No screenshots copied. |
| `src/core/` (`Assets`, `Events`, `Game`, `Input`, `Noise`, `Perf`) | loop, events, deterministic seed, input and metrics | method; selective port later | `forge_core`, `forge_engine`, proof schema | File-level MIT/provenance review for any port; otherwise Rust-native tests for seed determinism, event order and metrics. |
| `src/world/Terrain.js`, `terrainKernel.js`, `terrainWorker.js`, `Biomes.js` | height, biome and worker contracts | method; math may port later; renderer rewrite | `forge_world`, `forge_engine` | Pin any upstream technique source; golden height/biome samples and cross-target determinism tests. |
| `src/world/Vegetation.js`, `Grass.js`, `EZTrees.js`, `GroundScatter.js` | placement, LOD and instancing | rewrite | Bevy ECS/render plugins | No Cadle models/textures; synthetic Forge-owned geometry; density/LOD/performance comparisons. |
| `src/render/Renderer.js`, `Lighting.js`, `Sky.js`, `PostFX.js` | Three/WebGL renderer, light, atmosphere, post effects | rewrite | Bevy/wgpu render path | Concept-only until Sky provenance is resolved; GPU captures and frame-time evidence on native/WASM. |
| `src/world/Water.js` | water surface, reflection/refraction and interaction queries | rewrite | Bevy/wgpu water system | No textures or shaders copied; deterministic water queries plus visual/performance scenes. |
| `src/player/Player.js`, `PlayerCamera.js`, `PlayerController.js` | first-person movement and camera behavior | method; rewrite | Bevy input/controller with Rapier | Record movement constants as behavioral observations; collision, slope, jump and camera tests. |
| `src/world/World.js`, `Props.js`, `Colliders.js` | orchestration, props and collisions | method for contracts; content exclude | `forge_world`, `forge_assets` | Forge-owned synthetic fixtures only; no Cadle GLB or authored layout. |
| `src/enemies/`, `src/combat/` | bodies, rigs, AI, weapons and combat | defer | later gameplay gates | Not selected for R0/R1; revisit licences and behavior only when the gate requires it. |
| `src/rpg/` | quests, inventory, drops, progression and dialogue | defer | later gameplay/data gates | No dependency from foundation or Visual Valley. |
| `src/audio/` | WebAudio, ambience, music, SFX and synth | defer; recorded content exclude | later audio gate | Code provenance review required; no recorded audio or generated Cadle media. |
| `src/ui/` and `src/site/` | HUD, maps, menus, screens, CSS/theme | defer | later Studio/runtime UX gates | Rust/Tauri/Leptos UX is independently designed; no Cadle brand or media. |
| `src/vfx/` | brushes, particles, filaments and textures | defer; likely rewrite | later VFX gate | No texture reuse; synthetic fixtures and new shaders only. |
| `public/assets/`, `docs/concepts/`, all other Cadle media | 139 explicitly excluded files plus reference/capture media elsewhere | exclude | none | Prohibited from Git, CI cache, packaging and publication. |

## R0/R1 selection

Only the harness method, terrain/biome contracts, vegetation/LOD behavior, sky/light behavior, water
behavior and player/controller behavior are selected as comparison inputs for R0/R1. Rendering,
physics and platform integration are clean Rust/Bevy/wgpu/Rapier rewrites. Combat, enemies, RPG, audio,
UI and VFX remain deferred.

Every later source-level reuse requires a separate commit that names the Cadle path and lines, records
the derivation, adds SPDX and copyright notices, and adds an objective test. The matrix must be updated
before that commit is accepted.
