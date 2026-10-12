# Medieval house lighting repair 019 — 10 October 2026

The imported houses became disproportionately yellow/orange at 17:45 while the surrounding village remained muted. Repair 018's SH0 ambient gain did not fix the different lighting and exposure conventions of standard glTF PBR and the acquired scene renderer.

Opaque house surfaces now use the existing scene lighting receiver: live sun and all nine diffuse SH coefficients, terrain/object/cloud shadows, ground bounce, underwater attenuation and the common final grading. Their glTF linear color factors, sRGB base textures, normal maps, roughness/metallic channels and alpha cutoff are preserved. Shared mesh/material caches retain instancing; added neutral custom vertex attributes do not change triangle geometry, UVs or collisions. The previous approximate global ambient gain is removed. Transparent window glass keeps its existing blend pass.

The content adapter appends a VILLAGE_KIND_7 surface branch when staging the verified shader. The building package bytes and its hash remain unchanged. This scoped implementation does not create a new engine or a second canonical asset/map representation. The adjacent shader license preserves the original Tidewater MIT notice (DRG Software Solutions LLC, 2026).

## Local evidence

Evidence and verified backup: `E:/OpenRPG_Forge/.tidewater-integration/house-solar-materials-20261010-019/`.

- Linked Rust runtime: `52f8fe35b04b9df90409648264bcb73b0882fe95946b2249791267050c3c0531`; no compiler warnings. Source/SDK SHA pins unchanged across compilation. Static adapter formatted with the pinned Rust 1.98 rustfmt.
- Old runtime reproduced the bright orange house at 17:45: `baseline-proof-1745/terrain-264.png`. The baseline used the stationary diagnostic camera, so its moving-camera certificate is not counted as a regression PASS.
- New runtime: native game captures at 08:00, 12:00, 17:45, plus another village viewpoint at 17:45 and 22:00. Each `solar-proof-*/village.solar.json` confirms the requested atmosphere readback completed, 15 instances have registered triangle colliders, and 97 opaque scene materials are admitted. Native images were inspected; saturated orange overlighting and black/gold material edges are absent in these views. Test profiles preserved the user's real display/graphics files.
- Separate native moving-camera regression: `walk-proof-001/village.terrain-proof.json`, PASS, 174 moving frames, all render samples ready, zero translation during injected yaw, 82 screenshots.
- Installed data, saved authoring head `7d6ef1ab75a9b3af37fbc53ee75435f2d35344e3d562aedd1891570639f25304`, GLBs, roof retouch, removals, grounding and terrain are byte-identical to the pre-repair backup.
- Installed receipt: `Tidewater-Launcher-Fullscreen-20261008/REPAIR-20261010-019.json`. Reversible runtime update: evidence `Restore-Runtime.ps1` refuses later edits and active installed processes.

## Limits

These are local native Vulkan checks on AMD RX 7800 XT, not cross-GPU qualification, WASM validation, full-map parity or Gate 3 closure. The isolated example was linked against pinned existing engine artifacts; no full workspace rebuild/Clippy/test run is asserted. This surface branch covers the current opaque UV0 house materials; it does not claim general glTF extension/UV1/emissive-texture support. User visual acceptance remains pending. Captures use an explicit diagnostic camera override; ordinary gameplay installs no override or solar-capture exit system.
