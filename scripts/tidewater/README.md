# Tidewater extraction and comparison tools

Development tooling under ADR-0017. No Forge application imports these scripts.
Source pin: `1438b1abfcaee3267092b75573014f4d9b4a983c`.

`export-shore-sim.mjs SOURCE NEW_DIRECTORY` records the original beach simulation
at frames 0/1/120/540/1080, including terrain, propagation field, lace mips and
uniforms. `verify-shore-sim.py SOURCE REPEAT NEW_REPORT [--native NATIVE_OUTPUT]`
checks immutable acquisition and reports raw channel errors. Run the native
probe with `cargo run -p forge_engine --example tidewater_shore_sim_probe --
SOURCE_EXPORT NEW_OUTPUT`. It advances four fixed steps per render submission
from zero, retaining GPU state. Both exporter and native probe additionally
accept `--phase-probe` for a separate phase/hash/amplitude diagnostic and
`--hash-probe` for losslessly byte-encoded binary32 arithmetic over 73,728
integer wave numbers. These outputs are rejected as simulation evidence.
`verify-shore-hash.py SOURCE NATIVE NEW_REPORT --require-exact-hash` validates
the latter. `native-shore-hash.wgsl` preserves the source multiplication rounding;
Frame.pad0 must be zero. This adaptation is shared with water-material fixture
preparation, which records its hash and rejects nonzero padding.
[Corrected results and limits](../../docs/evidence/tidewater/2026-09-24-shore-rounding.md).

The canonical clone remains intact. Use a separate clean checkout with the
upstream locked npm dependencies (`npm ci --ignore-scripts`). `export-source.mjs`
uses the audited upstream headless WebGPU adapter because village assembly
uploads some prop buffers. It refuses a dirty/wrong source or an existing output.

```powershell
node scripts/tidewater/export-source.mjs E:/OpenRPG_Forge/.tidewater-integration/reference E:/OpenRPG_Forge/.tidewater-integration/exports/new-run
python scripts/tidewater/verify-export.py E:/OpenRPG_Forge/.tidewater-integration/exports/new-run E:/OpenRPG_Forge/.tidewater-integration/exports/independent-repeat
cargo run -p forge_world --example verify_sampled_source -- E:/OpenRPG_Forge/.tidewater-integration/exports/new-run
```

The export is immutable intermediate data, not a Forge editable zone. The manifest
lists missing systems explicitly. Eight raw prototype meshes preserve custom
vertex attributes; they are NOT ready-to-place GLBs. Geometry alone is insufficient
without the deformation, alpha masks, materials, wind and LOD implementation.

The material extractor now preserves evaluated WGSL snippets, uniform layouts,
the shared terrain/wind detail texture and every mip of the GPU-generated leaf
coverage atlas. Repeat hashes are qualified on the current adapter only.

`prepare-plant-probe.py EXPORT NEW_FIXTURE [palms|trees|shrubs|trees_far|shrubs_far]` verifies an export and prepares a
standalone fixture for `cargo run -p forge_engine --example tidewater_plant_probe
-- NEW_FIXTURE NEW_CAPTURE.png`. This developmental Bevy probe uses the source
plant deformation and surface expressions with provisional Bevy lighting. It is
not scene parity, shadow/translucency parity, an animated proof or a Studio asset.
The probe loads local shader/data files and needs no JavaScript runtime.
Fixture v2 includes the canopy lobe table, leaf atlas mips and vertical instance
scale. The Rust probe also reads v1 fixtures for the earlier palm evidence.
Far fixtures add the two original 3840×768 impostor atlases with all 12 mips,
the original far vertex/surface code and an 87 m camera offset. The extractor
now constructs upstream `Vegetation` to preserve application LOD settings and
plant-kind encoding, and exports all six packed runtime groups (65,383 records).
`vegetation/runtime.json` describes stream order, meshes and CPU LOD parameters.
These remain development exchange files, not a second Forge world format.

`prepare-canopy-probe.py EXPORT TREES_FAR_FIXTURE NEW_FIXTURE` creates a diagnostic
single-mesh far-canopy view of all 18,343 tree/shrub records. Four vertices per
card carry original iPos/iDat; the original shader still determines visibility,
thinning and frame selection. This is not the production instancing path and
contains no terrain, other vegetation groups, water or buildings.

Add `--silhouette` to the native probe to preserve deformation/alpha coverage
while replacing lighting with white on black. `render-source-plant.mjs
SOURCE_CHECKOUT FIXTURE NEW_PNG` produces the corresponding upstream coverage
oracle. `compare-coverage.py SOURCE_PNG NATIVE_PNG NEW_REPORT_JSON` (Pillow/NumPy)
measures exact mask overlap and disagreement beyond one pixel. These are fixed
pose/time measurements, with no automatic tolerance acceptance or scene parity.

`reference.html` is an explicit overlay copied into a separate reference checkout
for Vite, accessed with `?noAudio&view=coast` (or village/island/reef/canopy).
It suppresses HUD, uses all default visual systems and advances a fixed number of
steps. Record overlay hash, backend, camera, time, viewport and screenshots.
Async readbacks/precompile state still require replay qualification: a fixed
frame count alone is not a temporal determinism proof or a performance result.
`index-baseline.py BASELINE_DIR OVERLAY_HTML` hashes the six inspected captures
and checks their recorded 1920x1080 dimensions; it refuses an existing index.

`Compare-PairedCapture.ps1 -Reference SOURCE.png -Native FORGE.png -Output
RECEIPT.json -Preset Village|Seaward` records input SHA-256 and sampled sRGB
luminance/RGB differences in fixed rectangles. Run it only after independently
checking source revision, camera and rendering configuration; matching mean
brightness alone is not scene or temporal parity. The tool refuses an existing
receipt and supports only 1920×1080 PNG pairs on Windows.

Current CPU raster path: opt-in `TerrainDocument` v4 plus
`forge_world::sampled_terrain::SampledTerrainPlan`. It has no filesystem access;
the caller resolves immutable bytes against their hash. The same document keeps
sparse author corrections and supports the existing terrain session's history
and archive serialization. Old v1–v3 encodings omit the new optional field.
The production zone archive/blob resolver and Studio/render routing remain open.
`verify_sampled_source EXPORT [NEW_TERRAIN_JSON]` checks real upstream probes
through both the raw raster and the canonical document evaluator; optional output
is created without overwriting an existing file.

`prepare-canopy-probe.py EXPORT TREES_FAR_FIXTURE NEW_FIXTURE [TERRAIN_JSON]`
can include this document and its height blob. The native probe then draws
the full 2048 m terrain as 4096 tiles at 1 m spacing (8,388,608 triangles),
using a neutral gray diagnostic material. This does not reproduce the source
terrain CDLOD, procedural shading, shadows, shore or water, and is not a
performance benchmark or a Studio map. Tile generation is bounded per call;
shared edge positions/normals and source hashes are tested.

`prepare-terrain-surface.py EXPORT ISLAND_FIXTURE NEW_FIXTURE [BASELINE_CAMERA_JSON]`
adds the original terrain surface expressions and complete normal/splat/detail
mip chains. The export uses the untouched upstream GPU mip generator on identical
CPU inputs in readback-capable textures. Original source textures are unchanged.
The optional reference camera transfers position, quaternion and FOV; the probe
uses the pinned Engine camera planes (0.06 m / 60,000 m) for these poses.
Otherwise the diagnostic camera uses 45 degrees, 0.1 m / 20,000 m.

The terrain adapter preserves the source main-pass surface path with HAS_WETNESS
false. Vertex displacement remains the CPU tile grid; unused height/shore/shadow
lookup functions and direct-sun modulation are explicitly excluded from this
surface adapter. Bevy supplies provisional PBR lighting. This is neither a
lighting/shore parity result nor a numerical surface comparison. The native
loader verifies all files and exact mip sizes, and refuses inconsistent domains.

### Adaptive terrain development proof

`export-cdlod-oracle.mjs SOURCE BASELINE NEW_JSON` evaluates the untouched source
on six reference cameras and 41 coastal track positions. It records queried
height bounds, ordered nodes, frustum planes and binary64 morph rows.
`cargo run -p forge_world --example verify_cdlod_source -- ORACLE TERRAIN_JSON HEIGHTS_BIN`
compares the Rust selector and raster bounds, with exact numeric equality.
The example enables serde_json's float-roundtrip parser to avoid decimal parse
rounding in this oracle; this does not change the canonical terrain schema.

`prepare-terrain-lod.py EXPORT SURFACE_FIXTURE NEW_FIXTURE` adds the original GPU
height lookup and lattice morph functions. The native probe selects its own nodes
from its camera, using the Rust selector, then expands their grid vertices for
this diagnostic. R32Float source heights are verified before upload. Existing
surface-only fixtures retain the fixed-grid path. Edited rasters are rejected by
this temporary GPU adapter until correction buffers are supported.

The native option `--terrain-mask` hides vegetation and returns white terrain on
black with MSAA off. `render-source-terrain.mjs SOURCE FIXTURE NEW_PNG` independently
renders the original source terrain with the same coverage output. Compare with
`compare-coverage.py SOURCE_PNG NATIVE_PNG NEW_JSON`. Coverage establishes only
visible geometry boundaries, not interior height equality, lighting, temporal
continuity or full scene parity. Selection is currently static in the native
example; production instancing, live camera updates and Studio remain separate.

### Village development scene

Source exports now include `village/render.json` and `village/textures.json`:
31 render nodes, seven materials and 14 complete mip chains. These preserve
upstream material batches, not individual authoring objects. The source bake
does not permit COPY_SRC; a textureLoad/store identity pass reads its original
RGBA8 mip levels without patching source code or texture usage. Fish props and
dynamic group animation remain explicit omissions.

`prepare-village-probe.py EXPORT LOD_FIXTURE NEW_FIXTURE` adds these source meshes,
texture bytes and WGSL expressions. `tidewater_plant_probe` loads them alongside
terrain and vegetation. The seven shader variants each bind at most four maps;
native lighting remains provisional. `--village-albedo` isolates the village's
linear albedo written to the window's sRGB target. The independent upstream
oracle is `render-source-village.mjs SOURCE FIXTURE NEW_PNG`;
`compare-albedo.py SOURCE_PNG NATIVE_PNG NEW_JSON` reports numerical differences
without declaring automatic acceptance.

The first six global baseline PNGs were found to repeat a 1280x720 tile across
their 1920x1080 output. They are retained but rejected for global comparisons.
`index-baseline.py` now checks for this defect using Pillow/NumPy. New full-scene
capture acquisition must use the procedure below. Explicit-target terrain and village oracles
are separate from this flawed presentation capture path.

### Full-scene browser capture procedure

Set the browser viewport to 1920x1080 **before loading** `reference.html`; do
not resize the frozen scene. Navigate each named view, wait for the overlay's
`ready_reference_only` status, and save its `window.forgeReference` JSON beside
the image. The overlay renders 120 fixed steps after source initialization.
Use CDP `Page.captureScreenshot` with `format: 'png'`, `fromSurface: true`,
`captureBeyondViewport: false` and decode its base64 data without re-encoding.
The generic browser screenshot API returned JPEG bytes in this environment;
renaming these to `.png` does not make them lossless reference images.

Keep each acquisition in a new directory. Run `index-baseline.py DIRECTORY
reference.html` and inspect all six images before using them. It verifies PNG
signature, Full HD dimensions, view, time, render scale and the previously
observed repeated-tile defect. It does not prove temporal determinism or general
absence of image defects. Reset the temporary viewport and close the test tab.

### Shared far-canopy mesh

`prepare-instanced-canopy.py EXPORT EXPANDED_CANOPY_FIXTURE NEW_FIXTURE` replaces
the expanded far-card vertices with the original four-vertex prototype and an
interleaved source iPos/iDat buffer. The native example uses one shared mesh and
material, `MeshTag` to address each record and Bevy's normal mesh batching path.
It still submits every far instance without the source spatial query; this is
not a performance qualification or the full near/far vegetation implementation.
Existing fixtures retain their single-mesh rendering path. Compare the new
`--silhouette` image against the expanded native and independent source images
before extending this path to other vegetation types.

### Six-group vegetation scene

`prepare-vegetation-scene.py EXPORT SCENE_FIXTURE NEW_FIXTURE` preserves all
65,383 packed records, their exact f32 matrices, eight source LOD meshes and the
three original material programs. The native probe shares meshes/materials and
addresses both records and matrices through storage buffers. It reproduces the
source horizontal near query, per-instance radii, stable near ordering, safe
palm far exclusion and six-metre far sorting buckets at the fixed camera.
Shader distance windows retain the actual near/far transitions. Selection is
initialized once; movement and temporal refresh scheduling are not implemented.

`reframe-scene-probe.py FIXTURE REFERENCE_JSON NEW_FIXTURE` selects another
daytime reference camera while keeping hash-validated payloads unchanged.
`render-source-vegetation.mjs SOURCE FIXTURE NEW_PNG` independently constructs
the original six groups and renders their coverage without grass, ground or
village. Compare with the native `--silhouette` output. That diagnostic now
hides terrain as well as the village, including when a full scene fixture is
used. Surface captures retain provisional Bevy lighting; source sky lighting,
shadows, grass, temporal filtering, Studio and editable asset contracts remain
separate work.

### Vegetation direct light diagnostic

`prepare-direct-lighting.py SOURCE FIXTURE NEW_FIXTURE` copies the pinned source
BRDF and direct illumination terms into the vegetation adapter, including
specular intensity and leaf transmission. Run the native fixture with
`--vegetation-direct`; this requires the verified source lighting payload and
hides ground/village. Compare against `render-source-vegetation.mjs SOURCE
FIXTURE NEW_PNG --direct` using `compare-albedo.py REF CAND NEW_JSON direct`.
Both use unit white sunlight and exclude environment and shadow occlusion.
The original atlas bake remains intact. This is a fixed direct-light test,
not final scene lighting or exact pixel parity; forest differences remain.

### Acquired sky illumination

`export-environment.mjs SOURCE REFERENCE_JSON NEW_DIRECTORY` runs the original
atmosphere, SkyPro clouds and environment for 120 cold frames and writes SH
irradiance plus the six-face/six-mip half-float specular cube. This is a defined
cold-state acquisition, not a replay of App precompile/readback history.
Add `--clouds` to acquire the six original default `SkyProClouds` kernels,
seven volume-noise mips, blue noise, notices, effective uniforms for all 120
frames, and twelve intermediate textures at frames 1/120 (including the two
atmosphere LUTs consumed by cloud marching). Sampling modules and atmosphere
uniforms are also captured. The helper asserts
that CPU uniform values equal the actual last uploaded GPU values.
`verify-cloud-source.py SOURCE REPEAT ENVIRONMENT_BASELINE NEW_REPORT` checks
hashes, dimensions, finite samples, temporal parameters and unchanged lighting.
`tidewater_cloud_weather_probe CLOUD_SOURCE NEW_OUTPUT` runs the weather and
conservative-bounds kernels on Bevy's device without loading source results.
`compare-cloud-weather.py CLOUD_SOURCE NATIVE REPEAT NEW_REPORT` measures errors
and independently checks all 4096 conservative cells including repeat seams.
This first cloud probe does not render volumetric clouds in the scene.
Add `--shadow` to the native probe to calculate the 256² shadow map with the
original 3D noise and 120 acquired uniform steps. It updates all rows initially,
then one quarter per step, using native weather. No source shadow is uploaded.
`compare-cloud-shadow.py CLOUD_SOURCE NATIVE REPEAT NEW_REPORT` measures the
final transmittance field. This diagnostic replays uniforms and does not yet
provide a live cloud controller or connect shadows to scene receivers.
`--trace` additionally executes the original volume march at frame 120, with
native weather/bounds/noise and paired source atmosphere LUTs. It writes 240×135
RGBA16 radiance/opacity and depth/cost metadata, stripping GPU row padding.
`compare-cloud-trace.py CLOUD_SOURCE NATIVE REPEAT NEW_REPORT` records per-channel
errors. This is an isolated march comparison: source atmosphere is explicitly
declared in the probe manifest; temporal reconstruction and scene composition
are not yet implemented. The probe currently requires the FHD acquisition.
`--history` runs all 120 native march/resolve steps from empty history textures,
alternating two 960×540 color/metadata pairs. Pipelines and immutable noise/LUTs
are shared across steps; frame uniforms use separate buffers. Source history is
never loaded. Atmosphere/settings must remain constant for this fixed replay.
`compare-cloud-history.py CLOUD_SOURCE NATIVE REPEAT NEW_REPORT` compares both
history pairs; final pair 1 is frame 120, pair 0 retains frame 119. This remains
a batched GPU diagnostic, not live scheduling or final scene composition.
The history mode also computes the original 512×160 cloud panorama: full first
dispatch, then one of sixteen lattice subsets per step. It shares weather,
volume noise, acquired atmosphere and frame uniforms with the view march.
`compare-cloud-panorama.py CLOUD_SOURCE NATIVE REPEAT NEW_REPORT` compares RGB
in-scattering and alpha transmittance (the view texture instead stores opacity).
No source panorama is uploaded; sky/environment/water consumers remain to wire.
`CLOUD_SOURCE NEW_OUTPUT --native-atmosphere ATMOSPHERE_FIXTURE` runs the full
history/panorama using the existing Bevy atmosphere graph on the same device.
Its transmittance and sky-view textures feed the cloud passes directly after
the atmosphere dispatch. Source atmosphere LUTs are neither read nor uploaded.
`prepare-native-cloud-inputs.py CLOUD_SOURCE ATMOSPHERE_SOURCE NEW_CLOUD_INPUTS
NEW_ATMOSPHERE_INPUTS` creates input-only packs (495 and 6 files), preserving
the acquisition manifests for comparison. Uniforms still replay acquired
camera/wind/sun/irradiance values; this is not yet a live native controller.
`prepare-cloud-sky.py CLEAR_SKY_SCENE CLOUD_ACQUISITION NEW_SCENE` adds original
cloud-view sampling/composition to the sky material, checking position,
quaternion and FOV. Run `tidewater_plant_probe NEW_SCENE NEW_PNG --sky-clouds
CLOUD_ACQUISITION` to compute clouds on the scene's device using its atmosphere.
The shared cloud implementation is now `tidewater_cloud_weather_probe/mod.rs`;
the headless executable is a thin wrapper over that same implementation.
The original renderer accepts `--cloud-sky=CLOUD_ACQUISITION` instead of
`--clear-sky`, using the acquired original cloud textures for isolated sky
composition. `compare-cloud-scene.py SOURCE_CLEAR SOURCE_CLOUD NATIVE_CLEAR
NATIVE_CLOUD SOURCE_REPEAT NATIVE_REPEAT NEW_REPORT` reports cloud-region error
and both renderers' repeat differences. This mode excludes water and cloud
shadow reception; the main rendered frame still uses acquired illumination.
`prepare-environment-lighting.py SOURCE DIRECT_FIXTURE ENV_EXPORT NEW_FIXTURE`
binds these data and original multiscattering terms to the native material.
Run with `--vegetation-environment`. For comparison, use the original renderer:
`render-source-vegetation.mjs SOURCE FIXTURE NEW_PNG --environment=ENV_EXPORT`,
then `compare-albedo.py REF CAND NEW_JSON environment`.
The comparison excludes scene shadows, bounce and final processing; the frozen
cube does not constitute native atmosphere/cloud simulation or Studio support.

### Shared terrain, village and vegetation illumination

`prepare-scene-lighting.py ENVIRONMENT_FIXTURE NEW_FIXTURE` applies the source
direct/environment functions to terrain and village, preserving material AO,
emissive, environment intensity and alpha. The native `--scene-lighting` mode
renders the three groups together using one shared environment image. Compare
against `render-source-vegetation.mjs SOURCE FIXTURE NEW_PNG --scene=ENV_EXPORT`.
The source oracle explicitly disables object and heightfield shadows, and
excludes village fish props. Both sides omit ocean and final scene processing;
this validates combined material integration, not the complete App appearance.

### Native heightfield shadows

`export-terrain-shadow.mjs SOURCE ENV_EXPORT NEW_DIRECTORY` captures the
original kernel, lookup, coarse mip oracle and shadow texture oracle.
`prepare-hill-shadow.py SCENE_FIXTURE SHADOW_EXPORT NEW_FIXTURE` adds the
programs and source metadata. With `--scene-lighting`, Rust rebuilds and checks
the coarse half-float mip chain, then Bevy computes the shadow texture before
the camera pass. The native result is read back beside the screenshot as
`.shadow.bin`. The source `--scene=ENV_EXPORT` oracle enables the same hill
factor when this fixture flag is present. Compare captures using mode `hill`.
Sun settings and input handles invalidate the bake; interactive world edits
and coordinated sun movement remain unqualified. Object/cloud shadows,
meadow self-shadow and final atmosphere/ocean effects remain separate work.

### Native grass patches

`export-grass.mjs SOURCE FIXTURE NEW_DIRECTORY` exports the original three
GrassField geometries, density mask, height bounds and camera-selected cells.
`prepare-grass.py SCENE_FIXTURE GRASS_EXPORT NEW_FIXTURE` adds these data and
the original grass vertex/surface programs. Native `--grass-mask` and source
`render-source-vegetation.mjs SOURCE FIXTURE NEW_PNG --grass` isolate coverage.
Native `--scene-lighting` and source `--scene=ENV_EXPORT` include grass when the
fixture flag is present. Native selection is checked against the original
cell lists. Moving-camera updates and temporal grass qualification are pending.

### Object shadow cameras and source depth oracle

`export-sun-cascades.mjs SOURCE FIXTURE NEW_JSON` records 89 original camera/
sun states, including motion, disable/enable and invalidation. Run
`cargo run -p forge_engine --example verify_sun_cascades -- ORACLE_JSON` to
compare native matrices and scheduling. `verify_sun_cascade_guards` checks
invalid input rejection without changing valid state or schedule.

`render-source-vegetation.mjs SOURCE FIXTURE NEW_PNG --shadows=ENV_EXPORT`
retains the original object caster settings and exports depth layers plus
filter code alongside the source screenshot. `capture-object-shadows.mjs`
uses a diagnostic identity compute copy; no source code is changed. These
layers are comparison oracles, not replacement textures for the native map.
Native village, terrain and vegetation depth and fixed-view receiver filtering
are covered below. Interactive and temporal qualification remains pending.

### Native opaque village shadow depth

`cargo run -p forge_engine --example tidewater_object_shadow_probe -- FIXTURE NEW_BIN`
renders a 2048×2048×3 depth array on Bevy's device, using native cascade fitting
and the extracted opaque village/sign geometry. No acquired shadow texture is
loaded. Source mode `--village-shadows=ENV_EXPORT` isolates these same casters.
`compare-object-depth.py SOURCE_PNG.shadows.json NATIVE_BIN NEW_JSON` measures
the layers, checks provenance hashes and records empty layers explicitly.
This headless example measures depth only; receiver filtering is exercised
by the visual probe below. Neither example is a Studio or runtime route.

Add native `--terrain` to render the CDLOD heightfield in those same depth
layers. Native selection uses the validated raster bounds, and GPU morphing
uses the main camera for every light cascade. Source
`--terrain-village-shadows=ENV_EXPORT` isolates the matching caster set.
The source WGSL functions are read from the hashed fixture; edited terrain
is rejected until authored deltas are supported by this development adapter.
Receiver filtering is exercised by the visual probe below.

Add native `--plants` to include terrain and the five near vegetation caster
groups. Geometry, leaf atlas, detail texture, deformation and discard programs
are read from the checked fixture. Selection uses the main camera; per-cascade
view-dependent masks use the shadow camera. Source `--shadows=ENV_EXPORT`
provides the corresponding oracle. Grass, understory and far impostors do not
cast in this source configuration. Village and forest selection/order match;
depth and fine foliage coverage still differ. Repeatable captures are not a
complete visual parity verdict. Wind/time remain fixed diagnostic parameters.

The native depth graph is shared with the visual probe:
`tidewater_plant_probe FIXTURE NEW_PNG --scene-shadow-depth` computes the same
three layers before the camera pass on the scene's Bevy device and writes
`NEW_PNG` with extension `.object-depth.bin`. With the original lighting
fixtures this mode measures coexistence only, without receiver filtering.
Village/forest depth matches the headless native results; village color and
heightfield shadow output remain byte-identical to the preceding capture.

`prepare-object-shadows.py PINNED_SOURCE SCENE_FIXTURE NEW_FIXTURE` adapts the
original PCSS/PCF module and injects it into the direct light of terrain,
village and vegetation (including grass). The native mode
`tidewater_plant_probe NEW_FIXTURE NEW_PNG --object-shadows` generates its own
depth texture and binds that texture plus native cascade parameters to each
receiver. It does not load source oracle depths. Compare the existing paired
source `--shadows=ENV_EXPORT` capture with `compare-albedo.py REF CAND NEW_JSON
shadows`. This measures fixed views with cold acquired environment lighting;
cloud shadows, bounce, temporal filtering, ocean and final processing remain
outside the capture. The filtered fixture requires native depth mode; an
unprepared fixture is rejected by `--object-shadows`.

### Native atmosphere calculations

`export-atmosphere.mjs PINNED_SOURCE ENVIRONMENT_EXPORT NEW_DIRECTORY` captures
the original four atmosphere kernels, their parameters, three half-float LUTs
and irradiance buffer. Currently requires a daytime environment so the key
light direction is the real sun. Run `tidewater_atmosphere_probe FIXTURE
NEW_OUTPUT_DIRECTORY` to compute the same outputs on Bevy's device with
parameters constructed in Rust. It checks the packed input against the source
and never loads source LUT data as native inputs. `compare-atmosphere.py
SOURCE_EXPORT NATIVE_OUTPUT NEW_JSON` reports numerical differences.

The native adapter stabilizes the upper-boundary transmittance distance when
a positive sphere residual selects a spurious near root. Vulkan's large error
is reduced to one differing boundary texel; Dx12 outputs remain unchanged.
Other numerical discrepancies remain. Add `--raw-source` to reproduce the
unmodified kernels for diagnosis. The exporter also records `boundary.wgsl`
and `boundary.rgba16.bin` without altering the physical reference outputs.
The same atmosphere graph is now used by the clear-sky scene mode below.
Clouds, scene environment generation, Studio and browser qualification remain
separate work.

### Visible clear sky

`prepare-sky.py SHADOW_SCENE_FIXTURE ATMOSPHERE_EXPORT NEW_FIXTURE` assembles
the original atmosphere lookups and Sky functions for a Bevy background
material. The atmosphere export must include the sky/core/common modules;
camera height and sunlight must match the scene. Native
`tidewater_plant_probe NEW_FIXTURE NEW_PNG --sky` computes the atmosphere LUTs
on the scene's device, then draws the clear sky only at cleared depth. Compare
with `render-source-vegetation.mjs SOURCE FIXTURE NEW_PNG --shadows=ENV_EXPORT
--clear-sky` and comparison mode `sky`. Both include the same terrain and
object shadows; surface environment lighting still uses the acquired cube.
This is a fixed daytime sky comparison without clouds, ocean, fog or final
processing. Dark background below the horizon is atmospheric radiance, not
an ocean implementation. A prepared sky fixture requires `--sky`.

### Ocean FFT numerical reference

`export-ocean.mjs PINNED_SOURCE NEW_DIRECTORY [default|storm]` runs the original
four 256² FFT cascades for 120 submitted steps at 60 Hz. It captures frames
1, 60 and 120, all nine displacement/derivative mip levels in all four layers,
the spectrum/wave/foam buffers, packed uniforms and nine compute shaders.
The storm case applies the original UI storm wave and whitecap controls.
`verify-ocean-export.py EXPORT REPEATED_EXPORT NEW_JSON` verifies hashes,
dimensions, finite data, stable initial spectrum, foam bounds and exact repeat.

`cargo run -p forge_engine --example verify_ocean_spectrum -- EXPORT` compares
native packed parameters against the independently executed source, including
one to four cascades, changed weather and the time sequence. This establishes
parameter fidelity only. Rendered water, shore effects, Studio editing and
final visual parity remain open.

`tidewater_ocean_spectrum_probe EXPORT NEW_DIRECTORY` now executes the original
initial-spectrum, conjugate and copy kernels on Bevy's device, using those
native parameters. It reads back `h0` and `waveData` without loading reference
buffers as inputs. `compare-ocean-spectrum.py EXPORT NATIVE_OUTPUT NEW_JSON`
measures the numerical differences.

Add a final argument `1`, `60` or `120` to execute that many native steps at
60 Hz, including row/column inverse FFTs, persistent foam and all mip kernels.
The deterministic probe records the requested history on Bevy's device using
a distinct uniform buffer per step. It exports h0, waveData, foam and both
four-layer texture arrays at all nine mip levels (21 files, rows unpadded).
`compare-ocean-fft.py EXPORT NATIVE_OUTPUT NEW_JSON FRAME` compares every output
against the corresponding source snapshot. This is an offline numerical
probe; integration into a live scene and the water material remain open.

The implementation now lives in `tidewater_ocean_spectrum_probe/mod.rs` with
an `install` entry point exposing its two native image handles and completion
signals. The scene diagnostic accepts
`tidewater_plant_probe SKY_FIXTURE NEW_PNG --sky-ocean-compute OCEAN_EXPORT`.
It runs the same 120-step graph on the scene's Bevy device, stores the 21
numerical outputs in the sibling `NEW_PNG` path with extension `.ocean`, and
waits for readback completion before capturing the scene. The image samplers
use repeat addressing and linear filtering across mip levels, as in Tidewater.
This mode does not draw water yet; it verifies co-execution before connecting
the water surface and material. It still computes a fixed history once.

### Ocean grid and camera-following LOD

`export-ocean-lod.mjs PINNED_SOURCE BASELINE NEW_DIRECTORY` captures the original
32² ocean patch, alternating triangle diagonals, twelve morph rows and ordered
node selections for 70 views (six baseline cameras, a coastal track, root-cell
boundaries and several heights). It also preserves the original terrain-aware
WaterSurface vertex/module code and attenuation functions with shore/wake/detail
disabled explicitly in `surface_options`. These shader exports are preparation
artifacts, not a claim that the native scene renders the surface yet.

`cargo run -p forge_world --example verify_ocean_lod -- EXPORT` compares native
camera-following selection, `CdlodConfig::grid()` and packed morph uniforms
against that independent oracle. The reusable grid contains engine-neutral
positions and indices for the eventual ocean mesh adapter.

### Displaced ocean geometry in the scene

`prepare-ocean-geometry.py SKY_FIXTURE OCEAN_LOD_EXPORT NEW_FIXTURE` composes
the original terrain-aware WaterSurface vertex functions and a white diagnostic
fragment. Run `tidewater_plant_probe NEW_FIXTURE NEW_PNG --ocean-geometry
OCEAN_EXPORT` to draw the native camera-following grid with native FFT textures.
This includes the existing object shadows and clear sky. The 21 FFT readbacks
are saved in the sibling `.ocean` directory before the scene is captured.

For the independent reference, use `render-source-vegetation.mjs SOURCE
NEW_FIXTURE NEW_PNG --shadows=ENV_EXPORT --clear-sky --water-geometry`.
`compare-ocean-geometry.py SOURCE_PNG NATIVE_PNG NATIVE_LOG NEW_JSON` checks
ordered node selections, image errors and white-mask overlap. The white mask
is a diagnostic color classification, not an object-ID buffer. Changed cameras
require a fresh source grass oracle; do not bypass the native selection guard.
This is a fixed-time geometric comparison without the optical water material,
shore waves, wakes, shore simulation or production instancing.

### Water scene inputs and material acquisition

Newly prepared ocean geometry fixtures also support native
`--ocean-scene-color OCEAN_EXPORT` and `--ocean-scene-depth OCEAN_EXPORT`.
They display opaque scene inputs on the water surface: Bevy's transmission
color copy and a Depth32Float copy taken after opaques, before transmission.
Depth is visualized at ×100. The scene remains fixed at 1920×1080, MSAA off.
Source equivalents are `--water-scene-color` and `--water-scene-depth` as the
last argument of `render-source-vegetation.mjs`. Its opaque and water passes
are separate and copy source color/depth in between. White geometry mode is
unchanged visually. Reprepare old fixtures to obtain these shader bindings.

`compare-water-scene.py SOURCE_PNG NATIVE_PNG WHITE_GEOMETRY_PNG NEW_JSON`
reports whole-image and eroded water-interior errors. This 8-bit visualization
does not establish raw floating-point depth equality or optical water parity.

`export-water-material.mjs PINNED_SOURCE NEW_DIRECTORY` acquires the original
material snippets, dependency modules, constructor uniforms, procedural foam
kernel and all eleven 1024² RGBA16Float foam mips. Only COPY_SRC permission is
added for acquisition. The shader configuration currently has terrain enabled,
with shore/wake/detail/shore simulation/clouds/planar reflection/separate
refraction/hull masking disabled. Terrain uniforms are constructor defaults
without village flattening: use scene terrain data when adapting the material.
This export is preparation, not evidence of native optical shading.

### Original optical water material in Bevy

Capture the paired source using the final argument `--water-optics` to
`render-source-vegetation.mjs`. This uses the original WaterMaterial and foam
texture, and saves the packed frame constants in `NEW_PNG.frame.bin`.
Then run `prepare-water-optics.py OCEAN_GEOMETRY_FIXTURE WATER_MATERIAL_EXPORT
SOURCE_PNG NEW_FIXTURE` and `tidewater_plant_probe NEW_FIXTURE NEW_PNG
--water-optics OCEAN_EXPORT`. Preparation checks the pinned inputs and camera.
Native camera matrices replace the source frame matrices; other frame constants
remain frozen for the paired test. Terrain, FFT, atmosphere, shadows and scene
copies use the existing native graph. The static foam texture is acquired data.

The full original optical functions run with shore/wake/cloud/planar-reflection/
separate-refraction/hull options disabled. This includes opaque-copy refraction
and screen-space reflection. Neither underwater cameras nor storm foam are
qualified by the two daytime default-spectrum views. The source/native opaque
copies are 8-bit sRGB in this probe, before future HDR/temporal qualification.
Use `compare-water-scene.py` with the corresponding source white-geometry mask
for image/interior metrics. See `2026-09-24-water-optics.md` for evidence and limits.

### Shoreline waves

For the next material stage, `export-water-material.mjs SOURCE NEW_DIRECTORY
--shore-sim` acquires ShoreSim/SurfFoam modules, shared lace mips and the original
App.js terrain wetness hook. The source renderer accepts `--water-shore-sim` and
advances both FFT and ShoreSim for 120 steps, writing the actual state alongside
the PNG as `.shore-state.rgba16.bin`. `verify-shore-material.py MATERIAL REPEAT
SIMULATION_EXPORT CAPTURE BEFORE_CAPTURE NEW_REPORT [--repeat-capture PNG]`
checks acquisition and equality with the standalone numerical state. This is
source evidence. See [reference evidence](../../docs/evidence/tidewater/2026-09-24-shore-material-reference.md).

Native fixture preparation accepts this material export and its matching source
PNG. Run `tidewater_plant_probe FIXTURE NEW_PNG --water-shore-sim OCEAN_EXPORT
SHORE_SIM_EXPORT`. The scene computes 120 steps and shares the resulting GPU
image between water and terrain; `.shore/` contains six numerical readbacks.
`verify-shore-sim.py ... --native CAPTURE.shore --up-to 120` compares those states.
`--water-shore-compute` is a separate graph diagnostic using an old water fixture,
without the simulated-shore material. The standalone shore probe additionally
accepts a final snapshot number 1/120/540/1080. The shared module lives in
`tidewater_shore_sim_probe/mod.rs`; neither host imports a source state texture.
See [native evidence](../../docs/evidence/tidewater/2026-09-24-shore-material-native.md)
for measured rendering errors and remaining temporal/interactive limits.

For cause isolation, append `--water-debug=2`, `--water-debug=3` or
`--water-debug=11` to the source capture command (normals, foam coverage, or
foam sources). Preparation copies the paired 80-byte material uniform after
checking its hash and debug field. With ShoreSim, `--terrain-debug=wetness`
instead displays the original terrain wetness/residue function as RG. The
prepared native terrain displays the same function output. Metadata marks all
these captures as diagnostics; they are not final optical parity evidence.
Omit the diagnostic option for a normal reference. Native scene invocation
is unchanged and both standalone terrain and CDLOD material copies receive
the shared ShoreSim image and parameters.

### Sea surface variation

`export-water-material.mjs SOURCE NEW_DIRECTORY --shore-detail` adds the original
SeaDetail module and its 256² RGBA16Float noise (one mip) to the simulated-shore
configuration. The exporter preserves the original constructor-generated bytes.
Capture with `--water-detail`: the source advances its wind drift for 120 steps
and writes the paired 32-byte uniform to `NEW_PNG.sea-detail.bin`.
`verify-shore-material.py` also checks noise, paired uniform and the two-second
drift against the captured frame wind when this option is active.

Prepare with `prepare-water-optics.py` as above and render natively with
`--water-shore-sim OCEAN_EXPORT SHORE_SIM_EXPORT`. Bindings 30/31 carry the noise
and paired SeaDetail parameters. The original fragment functions modulate
short-wave slopes, slicks and windrows; the linear repeat sampler is shared
with FFT displacement. These are fixed-time comparisons: native wind drift
is frozen from the paired parameters, not yet advanced interactively.
The default captured frame wind is 6 m/s, where the original windrow activation
is zero; this does not qualify visible windrows at stronger winds.

### Plunging breaker inputs

`export-breakers.mjs SOURCE NEW_DIRECTORY` constructs the village, derives the
shore field and main-beach stations, then advances the original FFT and crest
finder for 540 steps. It preserves snapshots at 1/120/540, packed uniforms,
four-layer displacement mips, original crest WGSL, lip shader snippets,
lace mips and ribbon topology. Spray and clouds are explicitly disabled for
this acquisition; the original pinned source remains unchanged.

`verify-breakers.py EXPORT REPEAT_EXPORT OCEAN_EXPORT NEW_REPORT` checks exact
repeat acquisition, finite inputs, active crests and drawable neighbouring
segments, and frame-120 displacement against the previous FFT oracle.
The native `verify_breaker_geometry EXPORT NEW_DIRECTORY` example regenerates
stations from the terrain and rebuilds ribbon IDs/indices using
`forge_engine::breaker_geometry`, then compares the bytes to the independent
source. The height callback uses the original half-texel bilinear convention.
These checks do not qualify native GPU crests or the rendered lip.

`tidewater_breaker_probe BREAKERS_EXPORT NEW_DIRECTORY` now runs the original
crest kernel on Bevy's device for the three acquired snapshots. It regenerates
stations natively, applies the verified ShoreWaves hash rounding and writes
three crest buffers. This is explicitly `isolated_source_fft_inputs`: the FFT
snapshots are paired inputs, never native-result evidence, and source crest
results are not read or uploaded. Inactive slots start at zero here; upstream
retains stale payloads under a zero wave ID.

`compare-breaker-crests.py EXPORT NATIVE_DIRECTORY NEW_REPORT` verifies the
probe provenance and reports active IDs, drawable segments and errors in roots,
backs, height, trough, direction and breaking progress. It compares meaningful
active slots and does not claim optical parity from numerical agreement.

`tidewater_breaker_probe BREAKERS_EXPORT NEW_DIRECTORY --native-ocean OCEAN_EXPORT`
runs the shared native FFT at frame 120 before crest computation, using its GPU
texture view directly. The manifest records `shared_native_fft_frame120` and
`source_fft_textures_uploaded=false`. Reference displacement and crest files
are not required in the input directories. Native FFT readbacks go to the
sibling directory with extension `.ocean`; these are verification outputs,
not inputs to the crest calculation. The comparator accepts both modes.
This fixed-time compute check does not render the breaking lip.

`tidewater_plant_probe FIXTURE NEW_PNG --water-breaker-compute OCEAN_EXPORT
SHORE_EXPORT BREAKERS_EXPORT` attaches that crest calculation to the existing
scene App and waits for its readback before capture. Crest outputs use the
`.breakers` sibling directory. The exposed GPU buffers are ready for material
binding; this option does not yet draw the lip.

For the paired upstream image, `render-source-vegetation.mjs` accepts
`--water-breakers` in place of `--water-detail`. It adds the original breaking
lip after ocean rendering, updates crests for 120 steps and saves
`.crests.f32.bin` alongside the PNG. Spray and clouds remain disabled. Keep
the same environment and shadow-scope arguments as the no-lip reference.

`prepare-breaker-lip.py SCENE_FIXTURE BREAKERS_EXPORT SOURCE_PNG NEW_FIXTURE`
adds the original lip shader and lace mips to an existing SeaDetail fixture.
Use the resulting fixture with the native `--water-breaker-compute` option:
the prepared shader enables the transparent lip material, bound directly to
the shared native crest buffer. The unextended fixture still computes only.
`compare-breaker-lip.py SOURCE_BEFORE SOURCE_LIP NATIVE_BEFORE NATIVE_LIP NEW_REPORT`
reports both effect masks and colour errors in a one-pixel dilation of the
nonzero source effect mask. It does not declare temporal or full-scene parity.

`export-spray.mjs SOURCE NEW_DIRECTORY` acquires the original coupled FFT,
ShoreSim, Breakers emission and Spray integration for 540 steps. Snapshots at
1/120/540 include particles, atomic head, pending shore deposits, shore state,
FFT inputs and uniforms. The manifest records capacities and all emission
budget/seed values; CPU emitters, body collisions and clouds are disabled.
`verify-spray.py EXPORT REPEAT_EXPORT SHORE_WITHOUT_SPRAY NEW_REPORT` verifies
integrity, finite particles, inactive CPU slots, repeated schedules and the
multiset of active particle records. Raw particle files can differ because
GPU atomic reservation changes ring slot order. Shore foam/residue may change;
wetness and flow are checked against the no-spray acquisition. This is source
compute evidence, not native spray or rendered spray qualification.

The exporter also copies frame-one emission buffers inside the original command
encoder, before particle integration, and reads those copies after submission.
Do not insert a submission/readback between the passes: they share the final
SprayParams upload, so doing so changes the effective seed. The schedule records
`effective_gpu_seed` separately from the earlier CPU-side value.

`tidewater_spray_probe SPRAY_EXPORT NEW_DIRECTORY` runs the original emission
kernel at frame one on Bevy's device, using paired FFT inputs and zero particle
buffers. It never uploads source particle results. `compare-spray-emission.py
SOURCE NATIVE REPEAT_NATIVE NEW_REPORT` matches unique kind/tag identities,
reports numerical errors and verifies native repeat equality modulo ring slots.
This does not yet integrate particles, deposit foam or draw spray sprites.

Add `--integrate` to the native spray probe to execute Spray Update immediately
after emission on the same GPU buffers. The comparison script detects this mode
and compares `frame-0001` particle/deposit outputs. The initial deposit is zero
in both renderers, so this check does not qualify actual impacts or shore
feedback; later states and native FFT/history integration remain required.

`--replay-update 120` or `--replay-update 540` isolates actual impacts from
paired source pre-update particles and FFT textures. The manifest explicitly
marks the imported particle initial state; expected post-update particles are
never inputs. `compare-spray-update.py SOURCE NATIVE NEW_REPORT` compares slots,
life decisions, trajectories and every deposit texel. This is one native update,
not a native history replay or a coupled ShoreSim step. The source exporter
copies pre-update buffers at all three snapshot times without splitting GPU
submission.

`tidewater_ocean_spectrum_probe OCEAN_EXPORT NEW_DIRECTORY 120 --stepped`
advances one ocean simulation step per render submission, retaining GPU buffers,
textures and accumulated time. The shared graph exposes `enable_stepping(app)`
and `Targets.completed_steps` for ordered consumers after `OceanComputeSet`.
This counter identifies encoded work, not CPU-visible GPU completion. Final
readbacks still occur only at the requested snapshot. Pipelines are cached.
The default batched mode remains available; stepped FFT alone does not yet
couple the shore or spray history.

`tidewater_spray_probe SPRAY_EXPORT NEW_DIRECTORY --native-history OCEAN_EXPORT`
runs 120 native FFT/emission/integration steps from empty particle buffers.
It requires source schedule entries with `emitter_budget`, recorded before
Breakers updates its adaptive budget. The budget schedule is still replayed;
use `--native-budget` below for native adaptive feedback. No source FFT texture or
particle result is loaded in this mode. The shared ocean `permitted_step`
counter supplies consumer backpressure; `completed_steps` still denotes encoded
work, not a GPU completion fence. `history-head.u32.bin` records each step's
emission count without introducing intermediate GPU submissions.

`compare-spray-history.py SOURCE NATIVE REPEAT_NATIVE NEW_REPORT` matches
active particles by age/kind/tag and reports missing identities, trajectory
errors, count divergence and deposition differences. It verifies native repeat
equality modulo atomic slots without assuming source/native parity. Deposits
are currently cleared each step without ShoreSim consumption; sprites are not
rendered. See `docs/evidence/tidewater/2026-09-24-spray-history.md` for the
remaining numerical discrepancies and the rejected earlier budget timing.

Append `--coupled-shore SHORE_EXPORT` to the native history command to execute
ShoreSim before emission on every step. It consumes and clears the preceding
deposits through the same GPU buffer used by Spray Update. Validated terrain,
shore field and uniforms must match the spray inputs. No source shore state is
uploaded; `frame-0120-shore.rgba16.bin` is a final readback. The mode without
this option remains a particle-only history diagnostic.

For coupled runs, append `NO_SPRAY_SOURCE NO_SPRAY_NATIVE` after `NEW_REPORT`
in `compare-spray-history.py`. These are the isolated ShoreSim exports used to
measure the effect of deposition separately from existing shore discrepancies.
The report checks native repeat equality, quantifies each state channel and
requires an actual foam effect without introducing a wetness/flow effect.
This still does not qualify sprites, scene publication or source/native parity.

`export-spray.mjs SOURCE NEW_DIRECTORY [--scene-wind]` also acquires sprite
shaders, resolved wave-shadow hooks and every puff/dots texture mip. Readback
instrumentation adds CopySrc only to those two texture allocations. The optional
flag selects the fixed scene's global wind (speed 6, direction 1/0); omission
preserves the historical default numerical configuration. This global wind is
separate from FFT spectrum parameters.

`render-source-vegetation.mjs ... --clear-sky --water-spray` includes the original
sprite render and coupled foam feedback. Its sidecars retain particle buffers,
shore state and effective emission schedule. `verify-spray-sprite-reference.py
SPRITE_EXPORT REPEAT_EXPORT SCENE_EXPORT IMAGE REPEAT_IMAGE WITHOUT_SPRAY_IMAGE
NEW_REPORT` verifies asset reproducibility, matching wind and simulation states,
capture repetition and the changed image region. These are source-renderer
captures, not native sprite qualification.

`prepare-spray-sprites.py LIP_SCENE SPRAY_EXPORT SOURCE_SPRAY_PNG NEW_SCENE`
adapts the original sprite material to the existing Bevy camera/depth bindings.
Use the scene-wind export and a matching source capture. The native invocation
is `tidewater_plant_probe NEW_SCENE NEW_PNG --water-spray OCEAN_EXPORT
SHORE_EXPORT SPRAY_EXPORT`. Simulation readbacks are stored beside the PNG in
the `.spray` directory. The lip and sprites consume native GPU buffers; shore
materials consume the final coupled image. The application rejects mismatched
scene/simulation wind. The standalone spray CLI remains available.

`compare-spray-scene.py SOURCE_WITHOUT_SPRAY SOURCE_SPRAY NATIVE_WITHOUT_SPRAY
NATIVE_SPRAY NEW_REPORT` measures the combined sprite/shore effect above three
8-bit levels, with a one-pixel dilation. It requires native provenance flags
excluding imported particle, FFT and shore results. This fixed-camera proof
does not establish animated parity, production instancing or performance.

`verify_spray_budget SPRAY_EXPORT NEW_REPORT` verifies the Rust adaptive budget
controller against 540 acquired source head counters and the before/after
emission budgets. The source counter file is hash checked. Simulation time is
accumulated natively; the verifier models the acquired run's one-step feedback
latency and requires bit-exact f32 GPU budgets. This verifies the CPU controller
with paired feedback, not its connection to live native GPU readbacks. The
default `--water-spray` scene mode retains the recorded schedule for diagnostics.

Native feedback is now available explicitly: append `--native-budget` to the
standalone `--native-history` command, or use scene mode
`--water-spray-native-budget OCEAN_EXPORT SHORE_EXPORT SPRAY_EXPORT`. These modes
do not read the acquisition's budget schedule; it can be removed from the input
manifest. The existing modes keep replaying it for paired diagnostics.

The next simulation step waits for a native GPU head readback. This controlled
one-step feedback latency matches the source acquisition and is not a real-time
benchmark. `native-budget.json` records before/after emission budgets and the
actual sample used. `verify-native-spray-budget.py NATIVE REPEAT_NATIVE NEW_REPORT`
checks those decisions against the independent head trace and reconstructs the
controller arithmetic. Native scene comparisons distinguish this mode from
source-schedule replay. See the 2026-09-25 native-budget evidence for limits.

Add `--shore` to `export-water-material.mjs` to acquire the original ShoreWaves
module/parameters and the 512² RGBA32Float propagation field. The exporter
creates the village before deriving this field and records the terrain hash.
Capture the source with `--water-shore` instead of `--water-optics`.
`prepare-water-optics.py` accepts this configuration, checks the terrain hash
and camera, and binds the original shoreline functions. Native invocation
remains `--water-optics OCEAN_EXPORT`. The no-shore configuration remains valid.

`compare-shore.py SOURCE_WITHOUT_SHORE SOURCE_WITH_SHORE NATIVE_WITH_SHORE NEW_JSON`
measures the region where the source effect changes more than 3 levels in an
8-bit capture, dilated by one pixel, alongside the complete image. This includes
newly covered beach pixels that the earlier white ocean mask can exclude.
The current test has a fixed time of 2 s. The field is imported, not yet rebuilt
in Rust after terrain edits; ShoreSim, SurfFoam, the separate plunging lip and
spray remain outside these captures.

Cloud reflections in the assembled water scene:
`prepare-cloud-reference.py REFERENCE WATER_FIXTURE NEW_JSON` aligns pose and
wind before `export-environment.mjs SOURCE NEW_JSON NEW_ACQUISITION --clouds`.
Prepare the cloud sky fixture, then run
`prepare-cloud-water.py CLOUD_SKY_SCENE CLOUD_ACQUISITION NEW_SCENE`.
Native mode: `--water-spray-clouds OCEAN SHORE SPRAY CLOUD_ACQUISITION`.
The sky, water and breaker lip share the native cloud panorama. Source capture
uses `--cloud-sky=ACQUISITION --water-spray` with matching object shadows.
`compare-cloud-scene.py` reports the effect region and a diagnostic water screen
band, which includes geometry and must not be treated as a water segmentation.
See `2026-09-25-cloud-water-reflections.md` for fixed-pose evidence and limits.

Cloud shadow receivers: `prepare-cloud-shadow-scene.py CLOUD_WATER_SCENE
CLOUD_ACQUISITION NEW_SCENE` adds original shadow sampling to terrain, plants
(including grass), village, ocean, breaker lip and spray. The native invocation
remains `--water-spray-clouds`; source rendering activates receivers from the
prepared fixture marker. `export-environment.mjs` accepts an optional reference
JSON `cloud_coverage` in [0,1], records it, and sets the original controller.
The default coastal pose has clear ground, so the receiver evidence includes a
separate 0.85 coverage case. See `2026-09-25-cloud-shadow-receivers.md`.

Native cloud CPU controller:
`tidewater_cloud_controller_probe CLOUD_ACQUISITION NEW_OUTPUT` reconstructs
120 cloud Frame/Settings/public blocks from camera, wind, light and coverage
metadata; source blocks are comparison targets only. The independent source
transition oracle is `export-cloud-controller.mjs SOURCE NEW_OUTPUT`: original
CPU update, intercepted compute dispatches, no noise loading. Its 32 steps cover
movement/cuts, light/coverage changes, underwater, resize/scale, dt clamp and
explicit history invalidation. The same Rust probe accepts its controller.json.
`verify-cloud-controller.py SOURCE NATIVE NEW_REPORT` measures every output word.

The shared scene cloud graph uses this controller. The headless cloud probe
selects it with `--native-controller ATMOSPHERE_INPUT`. Add `--native-controller`
to `prepare-native-cloud-inputs.py` to omit all acquired local cloud Frame and
Settings files. Global scene Frame and atmosphere parameters remain acquired;
this is not yet an autonomous scene clock or an interactive GPU animation loop.

Persistent cloud GPU stepping: the shared scene graph advances one of its 120
warmup steps per render-graph invocation. Volume/blue-noise textures, samplers,
settings and shadow/trace/temporal/panorama pipelines persist across invocations;
weather/bounds run once. The headless equivalent is
`--native-stepped ATMOSPHERE_INPUT`; `--native-controller` retains batch mode.
`execution.json` records completed steps and graph submissions.
`verify-cloud-stepped.py BATCH STEPPED REPEAT NEW_REPORT` requires 120 submissions
and exact agreement of all ten output textures. This does not qualify unlimited
animation, camera changes on the GPU, or performance.

Canonical imported vegetation:
`prepare-imported-vegetation.py SOURCE_EXPORT TERRAIN_DOCUMENT NEW_DIRECTORY`
creates ten immutable placement blocks and a vegetation v2 document. It preserves
65,383 source keys, positions, scales and prototype parameters. Blocks use f64
transforms and a content-addressed binary encoding; they are not manual placements.
`verify-imported-vegetation.py SOURCE_EXPORT FIRST REPEAT NEW_REPORT` compares
every record and all repeated files. The Rust example
`cargo run -p forge_project --example verify_imported_vegetation -- IMPORT_DIRECTORY TERRAIN_DOCUMENT NEW_REPORT`
validates decoding, unique entity identities and the zone metadata archive.
This does not register prototypes in the asset catalog, embed blobs in a module,
or qualify placement/editing/rendering in Studio.

World population resolution:
`cargo run -p forge_world --example verify_imported_population -- IMPORT_DIRECTORY NEW_REPORT`
loads each content-addressed block through a bounded storage callback, validates
all records, and checks that every entity belongs to exactly one 64 m chunk.
The derived population preserves the immutable base transforms and parameters;
authoring corrections and renderer adapters are separate pending steps.

The optional third argument `TERRAIN_DOCUMENT` on `verify_imported_population`
also archives an authored clearing at (27, -379), radius 50 m, and compares its
membership with a direct calculation from every source position. The world
`exclusion_view` applies effective authored exclusion shapes and retained promoted
origins without changing base records. This is an exclusion stage, not the full
density/habitat/terrain/manual-placement projection.

The same optional terrain verification now also paints density 128, 64, 255 and
0 on all ten layers, archives/reopens the half-density result, checks nested
survivors and clears every override. `ImportedPopulation::authored_view` combines
exclusions/promotions with these authored masks. Imported placement records remain
unchanged; masks use bilinear world-coordinate sampling and a stable entity rank.
Procedural recipe fields do not regenerate imported placements.

Workspace source persistence: append `NEW_AUTHORITY HEIGHT_BLOB` to the project
`verify_imported_vegetation` example. It publishes terrain and placement bytes in
`source-blobs/<sha256>.bin`, publishes the zone object, then reopens and decodes
using only the destination authority. Workspace zone-object publication/read now
requires all immutable sources referenced by the current state and its history.
This is not yet a Studio import dialog or a portable module export.

`cargo run -p forge_project --example verify_imported_adoption -- SOURCE_AUTHORITY ARCHIVE_FILE NEW_WORKSPACE NEW_REPORT`
stages a complete saved zone, exercises the existing reviewed workspace adoption
route, then saves and reopens it. Adoption review validates source blobs without
copying; apply copies the complete history closure before attaching the zone.
The source zone's `authoring.json` remains unchanged. This qualifies the host
storage route, not the Studio viewport or a cross-workspace transfer UI.

Canonical-to-render packing:
`cargo run -p forge_world --example verify_tidewater_packing -- IMPORT_DIRECTORY NEW_OUTPUT`
derives instance matrices, iPos/iDat and query radii from canonical records.
`verify-canonical-packing.py SOURCE_EXPORT PACKED_OUTPUT NEW_REPORT` compares all
25 f32 components of every instance against original GPU streams by source key.
This qualifies values; preserving the original group ordering and hooking the
adapter into scene/Studio rendering remain separate work.

Ordered canonical rendering: prepared imports now retain a sixth `source_order`
parameter. The native packing probe emits six ordered group streams; the Python
verifier compares their complete bytes to the upstream GPU buffers. Older
five-parameter blocks remain accepted by the data adapter.
`prepare-canonical-vegetation-scene.py SCENE_FIXTURE IMPORT_DIRECTORY NEW_FIXTURE`
removes the 24 acquired vegetation streams and embeds only the canonical document
and ten blocks. The scene and its plant shadow casters use the same Rust adapter
to reconstruct those streams. Use the existing scene CLI/render mode unchanged.
This path currently requires the complete original population; authored filtering
and Studio rendering are not yet connected to it.

FishStand content continuation (pinned Tidewater commit above):
`export-fish-stand-content.mjs` keeps the original Rocketbox Joe GLB and source
records; `export-fish-stand-fish.mjs` serializes the five display fish with
u32 triangle indices. `prepare-fish-stand-content-bundle.py` and
`prepare-fish-stand-fish-bundle.py` produce additive, hashed preview inputs.
For a Bevy build without JPEG support,
`convert_joe_embedded_jpeg_to_png.py --integration-root E:\OpenRPG_Forge\.tidewater-integration`
creates the additive PNG derivative (bundle 010) and pixel-identity/Khronos
provenance. `compose_joe_png_bundle.py --integration-root E:\OpenRPG_Forge\.tidewater-integration`
copies bundle 009 plus that derivative into a full package-valid bundle 011;
it preserves both inputs and writes conversion provenance beside the output.
The FishStand shader remains a bounded material adapter, not the source PBR
renderer, and this bundle does not register an editable Studio object.

Studio ocean FFT inputs (no baked GPU results):
`package-ocean-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM NEW_OUTPUT`
checks the pinned preview manifest, each selected source file, the nested
`ocean.json` hashes and the Tidewater MIT notice. It writes a 14-file
`forge.tidewater.ocean-input-package` consumed by `OceanPackage`. Add
`--ocean-package-root PACKAGE --ocean-package-sha256 SHA256` to
`prepare-building-library-site.py` to stage it beside the existing building,
environment, terrain, plant and FishStand packages. The resulting optional
`ocean_sha256` is loaded by Studio. That initial transport-only result is
recorded in `docs/evidence/tidewater/2026-09-29-studio-ocean-input-transport.md`.
The current imported Playtest also runs the resident four-cascade FFT.
`package-water-surface-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM
NEW_OUTPUT` packages the source surface inputs; stage them with
`--water-surface-package-root` and `--water-surface-package-sha256` on
`prepare-building-library-site.py`. Add `forge_water_geometry=1` to the
authoring URL for a white geometry diagnostic, or `forge_water_optics=1` for
the blue source-material preview. The latter implies geometry and currently
suspends the Bevy motion-blur pass. Its successful isolated Playtest and
remaining visual limits are recorded in
`docs/evidence/tidewater/2026-09-30-studio-ocean-optics-preview.md`.

Studio ShoreSim inputs (no baked simulation texture):
`package-shore-sim-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM
NEW_OUTPUT` checks the pinned preview manifest and the original ShoreSim
descriptor, then packages the kernel, source heights, shore field, direction,
simulation/terrain parameters and ten lace mips with the MIT notice. Stage it
with `--shore-sim-package-root` and `--shore-sim-package-sha256` on
`prepare-building-library-site.py`. When the verified FFT, optical surface and
ShoreSim packages are all present, the imported Playtest activates the optical
ocean and resident GPU ShoreSim by default. They bind live state to both the
water and beach materials. `forge_water_optics=0` or `forge_shore_sim=0`
disables either path for diagnosis; `=1` explicitly requests a path on a
partial library. The authored height deltas and shore field are recomputed for
the edited terrain before each renderer generation.

Studio ground-bounce input (fixed source sun):
`package-ground-bounce-assets.py FULL_PREVIEW_BUNDLE BUNDLE_SHA256 UPSTREAM
NEW_OUTPUT` checks the pinned scene manifest and the original 512×512 RGBA16F
filtered bounce map, then packages the descriptor, map and MIT notice. Stage
it with `--ground-bounce-package-root` and
`--ground-bounce-package-sha256` on `prepare-building-library-site.py`.
Studio validates the map against the imported environment's sun and binds it
to the village material in Authoring and Playtest. The Playtest graphics panel
offers a 0–2 strength slider when the package is available. This fixed-sun
map is an input for the current Tidewater scene, not a dynamic rebake for
changed sun or terrain.

The imported Playtest also enables GTAO and the source-derived solar flare
post passes. Both sample the Bevy main depth texture, so the player camera
requests `TEXTURE_BINDING` before the first WebGPU view allocation; optical
water additionally retains `COPY_SRC`. The graphics panel exposes live 0–2
strength controls. `Invoke-FishStandStudioSmoke.ps1` can check them with
`-RequireAmbientOcclusion -RequireLensFlare`; `-CaptureSunFlarePair` captures
the flare at 0 and 2 while looking toward the pinned source sun. The smoke
captures still need source-aligned weather, time and exposure for parity.

The imported Playtest also applies source-derived atmospheric haze, using the
resident atmosphere lookup texture. The graphics panel offers a live 0–4
"Brume marine" control (default 1.6). The fish-stand smoke can validate its
0/default pair with `-RequireAirHaze`; this does not establish visual parity
with the public demo or underwater behavior.

With the optical ocean enabled, the Studio Playtest motion-blur slider is
active. `Invoke-FishStandStudioSmoke.ps1 -RequireMotionBlurWithOcean` checks
0°, 360° and reset to 180° while the ocean remains live. The WebGPU frontend
rewrites the four implicit-LOD samples in Bevy 0.19's composed motion-blur
shader to explicit LOD 0; this is guarded against unexpected shader changes.

Studio keeps the Playtest graphics values in versioned local WebView storage,
separate from the authored zone. The smoke option `-RequireGraphicsPersistence`
changes exposure, reloads the page in the same isolated profile, verifies the
restored value, resets it, and then runs the normal edit/playtest path.


Path-light source conversion (three props toward the upper houses):
`export-path-lights.mjs UPSTREAM NEW_RAW_OUTPUT` observes the pinned village,
verifies the unchanged whole scene, and preserves geometry, source material data,
colliders, light-vector identity and provenance. `package-path-lights.py
RAW_PARTS SOURCE_RENDER BUILDING_PACKAGE UPSTREAM NEW_OUTPUT` folds verified
triangles onto village materials and local pivots. `verify-path-light-package.py
RAW_PARTS PACKAGE EXPECTED_PACKAGE_SHA256 NEW_REPORT` independently checks
closed payloads, source attributes and reconstructed positions/colliders/lights.
This package still requires a Rust reader, catalogue/transport and editable-root
light binding before Studio/runtime qualification. See the
[path-light evidence](../../docs/evidence/tidewater/2026-10-03-path-light-source-acquisition.md).

`prepare-path-light-catalog.py PACKAGE EXPECTED_PACKAGE_SHA256 NEW_OUTPUT`
prepares three standard AssetCatalog records and immutable source-binding
metadata in a separate directory. It does not install a library or create
authored placements. The exact source IDs remain the import join keys.

The prepared Rust reader is `forge_assets/src/tidewater_path_lights.rs`;
it has isolated native sync/async package and tamper checks. It is not yet
registered in the crate or connected to the Studio library. Its real-package
test requires `FORGE_TIDEWATER_PATH_LIGHT_PACKAGE` and explicit `--ignored`.

`export-environment-programs.mjs UPSTREAM NEW_OUTPUT` acquires the original
22 environment compute programs, explicit binding layouts, constructor uniform
bytes and resource identities from the clean pinned source. It validates original
pipeline creation but does not dispatch them. Uniform bytes and resolved texture
identities are constructor snapshots; the renderer must supply live providers.
The native Engine example `verify_tidewater_environment_programs PACKAGE
MANIFEST_SHA256 FRESH_REPORT` checks integrity and creates those pipelines on
its existing Bevy RenderDevice, without supplying substitute resources or
claiming rendered parity. See the
[solar environment evidence](../../docs/evidence/tidewater/2026-10-03-solar-environment.md).

Optional arguments `LIVE_ROOT LIVE_MANIFEST_SHA256` execute GGX and SH from a
qualified, real source cube. Append `--generate-mips` to upload only its base
level and generate the seven remaining levels on Bevy. Append `--generate-sky`
instead to generate all six base faces from acquired atmosphere LUTs and live
cloud snapshots before mips/GGX/SH. These extended modes are isolated GPU
qualification tools, not the Studio environment publisher. The current code
compares generated source levels as well as final GGX/SH outputs; report flags
distinguish which stages actually executed. Filtered R32 input requires the
original `FLOAT32_FILTERABLE` feature and is never silently substituted.
