# FishStandStall Bevy input bundle

This evidence records a bounded Bevy input adapter for the upstream Tidewater
FishStandStall.  It does not qualify a Studio asset, a GLB, a production map,
or pixel equivalence.

## Source and placement

- Upstream repository: `https://github.com/dgreenheck/tidewater`.
- Pinned revision: `1438b1abfcaee3267092b75573014f4d9b4a983c`.
- Source export: `E:\OpenRPG_Forge\.tidewater-integration\fish-stand-source-export-001`.
- Exact local placement retained by the input descriptor: `(49.9,
  terrain.heightAt(x,z), -74.6)`, yaw `1.45` radians.
- Geometry retained without GLB conversion: 32,333 vertices and 40,259
  indexed triangles.  `aLayer` and `aUV2` occupy their own Bevy mesh slots.

## Bundle

The new additive bundle is
`E:\OpenRPG_Forge\.tidewater-integration\preview-scene-bundle-fish-stand-005`.
It hard-links unchanged source-bundle files and writes only new manifests and
FishStand-derived payloads.  Neither `preview-scene-bundle-clear-004` nor the
Windows package023 was changed.

- Internal manifest SHA-256:
  `58517f145b11ac760aa49002b33d19224bb18f940ad29c746a6c18d26a1a4d3b`.
- It contains 1,621 indexed files and seven arrays: `surf_a`, `surf_n`,
  `surf_r`, `prop_a`, `prop_n`, `prop_r`, and `signs`.
- `scene/fixture.json` now references `seaward-clear004` with SHA-256
  `db8c7019315897be00c8018591bbf0ac05949b11b1be8fe77dcd417529868a95`.
  Its original `lighting_and_scene_not_matched: true` is retained.
- The original JPEG/PNG texture files, `StallKit.js`, `FishStand.js`, Tidewater
  MIT license and the props CC0 credits remain in `fish-stand/source/`.

`package-preview-scene.py` accepts `fish-stand` as an additive folder while
continuing to require every historical folder.  It validates clear004 and this
bundle.  `prepare-fish-stand-bundle.py` rechecks the geometry, each array size
and SHA-256, and refuses a changed source bundle.

## Bevy adapter status

`crates/forge_engine/src/tidewater_stall.rs` creates a Bevy mesh with exact
source vertex arrays, 2D-array images, and `FishStandMaterial` bindings.
`tidewater_stall.wgsl` samples the source albedo, normal and roughness arrays
selected by `aLayer`, and samples the sign atlas via `aUV2`.

The material is deliberately marked
`bevy_base_array_shader_not_source_parity`.  It has no visual-equivalence
claim.  These source behaviours remain unported: source GPU mip generation,
`textureSampleGrad`, source anisotropic filtering, TBN normal-map response and
corrugation response, source PBR/environment lighting, lantern night flicker,
ice clearcoat/translucency, and the sign/chalk/dial, rope, float and plain
procedural branches.  It therefore has no valid A/B pixel-equivalence verdict.

The source reference for the future visual comparison is
`E:\OpenRPG_Forge\.tidewater-integration\source-seaward-clear004-20260929-021753\seaward-clear004.png`;
the missing stall region is near pixel `(450, 850)`.

The source `FishStand.js` also owns content outside `FishStandStall`: Joe's
Rocketbox `joe.glb`, fish placement through `CatchDisplay`, the buyer
interaction, and the stall/prop collision boxes.  They are not part of this
mesh-and-material increment.  The current bundle repairs only the stall's
silhouette and its source material inputs.

## Native GPU capture

`tidewater_plant_probe` registers `FishStandMaterial`, loads the bundle's
`fish-stand` descriptor, and spawns the prepared mesh at `(49.9,
terrain.height_at(49.9, -74.6), -74.6)` with yaw `1.45`. The first capture
request, `fish-stand-seaward-native-001.png`, correctly failed before GPU
initialisation because it used `--sky` against a fixture that requires the
spray material mode. No file was produced.

The complete water/spray/clouds invocation then completed with exit code zero
and wrote
`E:\OpenRPG_Forge\.tidewater-integration\fish-stand-seaward-native-003.png`
(2,325,010 bytes, SHA-256
`b01a5ba48f78dde9ed5a36a8c27be1c84b51c043b87a130b66e17233b501a247`).
Inspection against `seaward-clear004.png` confirms that the small wooden
stall is present in the lower-left foreground at the intended placement and
orientation. It is an actual Bevy GPU material pass; it does not report a
WGSL validation or pipeline error.

That `003` image was captured without the static post-processing profile, so
it is visibly brighter than the source and cannot be used as the paired A/B
verdict. The calibrated repeat sets `FORGE_TIDEWATER_STATIC_POSTFX=1` and uses
`paired-seaward-postfx-profile-20260929-001` as `LOCALAPPDATA`, whose
`graphics.json` fixes `auto_exposure=1`, `temporal_aa=0`, and
`exposure_ev=0.4`.

The first static attempt exposed a real prepass validation failure: the custom
forward vertex layout had been substituted for the default prepass layout. The
adapter now substitutes its `aLayer`/`aUV2` layout only when the pipeline uses
the FishStand forward shader; the Bevy prepass keeps its own layout and depth
write. This is not a transparent-material workaround.

`fish-stand-seaward-native-005-postfx.png` completed with exit code zero under
that profile (2,634,078 bytes, SHA-256
`eccc35710f112ed36415af2f2900bce4bf9da54b7b1522d3db1cc48e5e734c18`).
Its checked comparison receipt is
`E:\OpenRPG_Forge\.tidewater-integration\fish-stand-seaward-native-005-postfx.metrics.json`.
The full-image sampled mean luminance is 127.14 source / 125.82 native, with
8.21 absolute luma error; the beach region is 122.27 / 117.60 with 10.02
error. Visual inspection shows no depth-order artefact on the stall roof,
windows, or dark interior, and retains the stand in the expected lower-left
foreground placement. These measurements compare the source camera and pinned
revision, but do not establish pixel or scene parity.

Even after calibration, source Joe, CatchDisplay fish, debris, interaction and
collision props remain absent; the result is an integrated stall-material
proof, not scene parity.
