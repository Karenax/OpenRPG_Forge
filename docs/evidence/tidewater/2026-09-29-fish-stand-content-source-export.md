# FishStand content source export: Joe and the ice display

This is an additive source-acquisition result, not a native rendering or parity
claim. It preserves the existing bundles 024 and 005.

## Inputs and provenance

- Tidewater checkout: `E:/OpenRPG_Forge/.tidewater-integration/reference` at
  `1438b1abfcaee3267092b75573014f4d9b4a983c`.
- New immutable-style source record:
  `E:/OpenRPG_Forge/.tidewater-integration/fish-stand-content-source-export-003`.
- Joe is the exact upstream `public/models/characters/joe.glb`, 4,743,204
  bytes, SHA-256
  `da3efce5a452d3c17f40b8d33bf449f565c0a6bef93602dc3e74364fb64081ca`.
  `LICENSE-Rocketbox.md` records the Microsoft MIT license and is copied beside
  the model.
- The five display fish and their poses are sourced from `FishStand.iceFish`,
  `CatchDisplay`, `FishProps`, `FishGeometry`, `FishMaterial`,
  `FishSpecies`, and `FishTable`, all retained as Tidewater MIT source records.
- The crushed-ice mesh is already part of the previously acquired StallKit
  geometry in bundle 005: local centre `(-0.55, 1.245, 0.86)`, dimensions
  `0.92 x 0.46 m`, grid `36 x 22`, layer `-1`. Its clearcoat/procedural source
  material remains explicitly unported.

## Exact source display

The content descriptor records the same five fish on the ice chest: jack
`0.36 m`, red snapper `0.34 m`, yellowtail `0.30 m`, grunt `0.26 m`, and mullet
`0.33 m`. It preserves their alternating `side`/`sideFlip` pose, local offsets,
and the source height rule using `FishProps.restHeight`.

Joe's local position is `(0.2, 0.06, 0.12)` from the stand origin, rotated by
the stand yaw `1.45`; the vertical origin remains `terrain.heightAt(49.9,
-74.6)`. His source animation names are `idle_neutral_01`,
`gestic_talk_relaxed_01`, and `wave_01`.

## Additive bundle and checks

`prepare-fish-stand-content-bundle.py` created
`E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-fish-stand-content-006`
by hard-linking the untouched 005 bundle and adding the content record. Its
manifest SHA-256 is
`d6dadfa35f48926cca2d94efeceed5b2790a88a2b11214ffa13dc510be5e5112`;
it lists 1,633 files. Re-verification passed. The 005 manifest hash stayed
`58517f145b11ac760aa49002b33d19224bb18f940ad29c746a6c18d26a1a4d3b` before
and after creation.

The export script's deterministic verification passed: 11 retained source
files, 5 display fish, and Joe's recorded byte count. Node syntax and Python
compilation checks passed.

## Bundle repair

During preparation, a hard-linked inherited `scene/fixture.json` was rewritten
without first breaking its link. This invalidated bundle 005's fixture entry;
the issue was detected by comparing the file against its manifest, before any
runtime build. The fixture was reconstructed in memory from the authenticated
004 fixture using the deterministic 005 transformation, and only replaced after
its SHA-256 exactly matched the expected
`02a0364400185c91741a6bd39be7ddbb2a0d8a09c1d9449b575a9df3cbce199e`.

All 1,621 entries of 005 and all 1,633 entries of 006 now match their own
manifests. Both bundle preparation scripts now unlink the inherited fixture
before writing the target-specific descriptor. Bundle 006 retains its additive
content entry and its own valid manifest.

Khronos glTF Validator `2.0.0-dev.3.10` passed Joe with zero errors and three
warnings, recorded in `joe.gltf-validation.json`: two generated tangent-space
warnings and one skinned-mesh non-root warning. The GLB has seven animations,
one skin, two materials, 4,285 vertices, and 7,108 triangles. These warnings
mean an eventual Rust adapter must validate tangent generation and hierarchy
handling; they do not invalidate the source record.

## Remaining integration

The current Rust probe only loads `fish-stand/fish-stand.json`; it does not
load `fish-stand-content/content.json`. No existing runtime file was changed in
this increment, so no native Joe/fish rendering, animation, interaction,
collision, GPU capture, package update, Studio visibility, or visual parity is
claimed. A native continuation requires an approved adapter and a deliberate
change to the probe loader/material path, followed by the shared Cargo/GPU
validation.

## Fish index encoding repair

The first fish export copied Tidewater's `Uint16Array` index bytes while naming
them `indices.u32le.bin`; the native loader correctly rejected the resulting
out-of-range values. A fresh source export 005 now widens every source index
with `Uint32Array.from(indices)` before serialization. The additive bundle 009
is `E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-fish-stand-content-fish-009`,
manifest SHA-256
`8b4f8c6d2ba7c8eb358bd9c55af79d20dc2fd5d957f47839ab98cab6b95968e3`.

The verifier requires `bytes == index_count * 4`, a triangle-aligned count,
and matching triangle count. It rejects the old export 004 with `invalid u32
indices: jack`; the new export and all 1,655 bundle 009 manifest entries pass.

## Runtime continuation

The source-export phase described above has since been connected to the native
probe in `fish_stand.rs`: the five meshes load through a checked u32-index
preflight, Joe is instantiated from his GLB and starts `idle_neutral_01`, and
the source stand placement is retained. The first native render exposed an
independent GLB loader issue: Bevy without its JPEG feature could not load Joe's
six embedded JPEGs. `convert_joe_embedded_jpeg_to_png.py` produces a derivative
with pixel-identical decoded PNGs while preserving the original MIT-licensed
GLB in the provenance bundle; the Khronos validator reports zero errors and
three inherited warnings. `compose_joe_png_bundle.py` creates the complete
package-valid bundle 011 with 1,655 manifest entries (SHA-256
`c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335`).

The 011 native capture loads without the earlier JPEG error and shows Joe at
the stand. The fish still use a flat approximation rather than the source wet
material; trade interaction, collision, full PBR/environment response and
Studio object editing remain open. This continuation does not establish visual
parity with the browser game.
