# forge_engine

For accepted authoring paths, use `update_vegetation_sources_with_paths` after
terrain acknowledgement. It keeps the authored vegetation document unchanged;
path clearances participate in generation, quality preparation, asynchronous
source guards and upload commit identity. `vegetation_document_hash` is then a
versioned composite projection identity. The legacy no-path update explicitly
clears accepted paths. Headless tests do not qualify GPU upload or Studio visuals.

`ProfileWarmup`, installed by `GlobalQualityPlugin`, explicitly prepares Low,
Medium, High and Ultra for the current scene/view/weather through the existing
quality request coordinator, then restores the original tier and adaptation mode.
It requires the matching application/GPU readiness and eight fresh stable cache
observations per stage. It does not change author documents, weather or camera;
runtime material budgets follow each requested global profile. It does not claim
every future scene/view variant or hitch-free
switching. Timeout is 120 real seconds; permanent shader errors fail preparation.
A newer manual quality request wins; a weather change cancels and restores only
the tier still owned by preparation. Ordinary player input is paused while warming.
Capture readiness refuses incomplete/failed preparation. The opt-in API is
`world.resource_mut::<ProfileWarmup>().request()`; idle mode does not scan the cache.
Native access: `--quality high --prewarm-quality`; Studio: “Préparer les quatre
profils”. Native metrics and Studio diagnostic exports retain the preparation
ledger. A successful ledger is technical readiness, not a complete Gate 1 verdict.

Governed forest LOD0 and LOD1 both cast shadows; only far LOD2/batches opt out.
The existing camera-distance shadow fade now ends at the medium LOD cutoff,
inside the unchanged environment shadow configuration. Ending all casters at
the near/medium switch removed canopy self-shadowing and made the same trees
appear pale before turning green on approach. This is a shared projection rule,
not a map/color adjustment. Shadow workload can increase; no performance gain
is claimed. Evidence and qualification limits: `R1_MOTION_POINT3.md`.

The optical candidate softens terrain contact over a 0.2 m view-ray segment.
It blends the already exposed/fogged undistorted transmission background after
water post-lighting, using absolute full-target pixel coordinates. No new pass
or geometry: one extra read only at contact. This coverage approximation does
not smooth the canonical terrain grid or change collisions; normal opaque water
is unchanged. Native fixed-view evidence and limits: `R1_WATER_CONTACT.md`.

An authored `WaterDocument.medium` also explicitly selects the unqualified depth
candidate. Two linear coefficient uniforms replace the legacy opacity-derived
extinction and palette scattering only in this mode: `T=exp(-(a+s)*distance)` and
scattering weight `s/(a+s)*(1-T)`, with a finite zero-extinction limit. Existing
Fresnel and bounded diffuse illumination remain approximations, not a full
volumetric integration. Diagnostics distinguish `experimental_rgb_medium_transmission_not_qualified`;
quality overrides remain pinned and water SSR disabled, as for the depth candidate.
No medium means the previous rendering path and authored hashes are unchanged.

`WaterDepthOpticsPreview` is an explicit, unqualified comparison resource inserted
before the water plugin. It routes only water through Bevy's transmissive forward
phase, with a depth-derived bounded view-ray path and absorption. It disables
water SSR and pins the candidate instead of applying authored quality overrides;
environment reflections are the remaining path, not a qualified SSR replacement.
`optical_depth_preview` identifies this state in diagnostics. Existing authored
optics resolution remains intent, not proof of the preview's applied capabilities.
Default rendering, map sources and geometry are preserved. Native CLI access is
`--water-depth-preview`; the explicit Studio route has been built and exercised
(`R1_STUDIO_LIT_CONTRAST.md`), without artistic or full-profile qualification.

Water COLOR.r retains bed depth; COLOR.gb now carries the local sampled river
direction. Only the optical candidate consumes it for two-scale advected normal
detail, with filtered frequencies and phase-reset crossfades. Its extinction uses
opacity separately from appearance colors. See `R1_WATER_FLOW_PREVIEW.md` for the
color-discontinuity investigation; do not promote this as qualified water.
The water mesh UV0 now holds metric river coordinates: cumulative horizontal
centerline distance and signed cross-stream distance. Bank UVs remain unchanged.
The optical candidate uses these coordinates for directional ripple detail,
avoiding a world-origin-dependent rotation of noise through river bends.
Detail now keeps the same metric scale across and along the stream; downstream
advection supplies flow direction without stretching all ripples into transverse
bands. This removes the former 0.35 cross-axis frequency factor, not the current.
The native comparison is separate from Studio qualification of this latest change.
This is surface animation, not a hydraulic solver; the default opaque water
still uses its existing world-space waves. See `R1_WATER_DIRECTIONAL_COORDINATES.md`
for the synthetic and visual evidence and remaining qualification limits.
The transmissive candidate excludes the water surface from the opaque shadow
atlas (`NotShadowCaster`), but keeps receiving scene shadows. Banks and default
opaque water retain their shadow behavior. This is not colored transmission or
caustics in shadow maps; see `R1_WATER_SHADOW_PREVIEW.md` for verification status.

For authored river levels, the water footprint extends under the existing bank
shoulder so terrain depth testing determines the visible shore. Heights remain
authored; banks, collision, source widths and the legacy lifted-water policy are
unchanged. This is bounded presentation overlap, not hydrology or bathymetric
refraction. Evidence and qualification limits: `R1_WATER_SHORE_OVERLAP.md` in
`docs/evidence/gate1/`.

WorldSketch road presentation uses `VisualValleyPlugin::with_road_surface`: a
terrain-bound `RoadSurfaceProjection` supplies derived material weights to both
the initial mesh and the terrain cache consumed by quality LOD generation. No
shader, geometry or collider change is involved. Its carving hash is additional
presentation identity; the terrain document hash alone does not identify this
appearance. Incremental authoring of these weights is not implemented here.

Terrain surface detail now also perturbs the PBR lighting normal, using a bounded
surface gradient of the existing filtered world-space detail samples. Cubic easing
between cache texel centres avoids discontinuous bilinear slopes. No added texture
fetches, geometry or canonical source edits; collision and shadow-bias normals are
unchanged. The standard normal prepass is not replaced, so SSR/SSAO microdetail is
not claimed. Native clear/rain evidence and limitations: `R1_TERRAIN_MICRORELIEF.md`.

Grass variation is seeded from the canonical vegetation instance UUID (explicit u64
fold/mixing on native and WASM), not its index in a filtered chunk batch. Reordering or
removing neighboring patches therefore preserves each surviving patch's blade geometry;
lower blade budgets preserve the existing prefix. The seed is only presentation variation,
not an identity or collision-free hash. Canonical IDs, placements, density/size ranges,
triangle budgets and source documents are unchanged. This corrects the old derived shape
mapping; previous rendered grass captures must not be presented as this implementation.

The shared grass mesh now uses 2.5–5.5 cm blade widths and a maximum 0.8 m root radius
before instance scaling, with the same height range and 3 triangles per blade. This
concentrates existing blades into slimmer tufts without increasing authored density;
it does not fill the bare spaces between patches or provide a Studio authoring tool.

## Optional weather sunlight

The environment adapter consumes the optional weather `sunlight` contract after the weather
Update and before environment observations in PostUpdate. A separate blend is advanced by the
weather simulation, independent of wetness. Only the matching area's single `VisualValleySun`
is controlled; the multiplier always uses authored illuminance, without cumulative attenuation.
The adapter restores previously controlled illumination when the option, weather/state or valid
identity/topology is lost. Unowned lights are unchanged when the option is absent.

`environment.sun.weather_response` reports source hash, blend, multiplier, authored and effective
lux, with `applied_cpu` explicitly distinguished from GPU/visual proof. Invalid/unknown response
states make the environment incomplete and block the sky-budget acknowledgement. This is not a
cloud renderer or a change to sun direction, color, ambient light, geometry or author sources.
Existing profiles do not opt in. CPU transition/release tests pass; a real native/Web candidate
cycle and visual acceptance are still required. Marker retagging, concurrent light-authoring
adapters and hot replacement of canonical profiles are not qualified by these tests.

`WaterQualityRequest::preview_screen_reflections_disabled` temporarily resolves screen-space
reflection to environment-only while retaining authored reflectance and tint. It survives
tier changes until explicitly cleared; disabling all reflections takes precedence. The
observation exposes this override separately. No source document or SSR qualification is
changed by toggling it. It supports the native optical-isolation proof, not a new Studio UI.

Global quality diagnostics include a cached ADR-0011 effective candidate plan, keyed by
source policy, tier and conservatively observed capabilities. Terrain bank constraints use
the explicit ADR-0011 exception; experimental forest projection remains unqualified.
Missing optical proof is not support.
Capability/source changes invalidate the cache and advance its local generation; removal
of the source clears the evidence. No cross-process generation guarantee is qualified.
The observer resolves a candidate; the coordinator now installs its optical settings.
GPU readiness requires current renderer prerequisites and completed submission.
Optional adaptation is wired after the global observer in `Last`: initialize from its real
guard/observation, sample focused-window `Time<Real>` frame durations only after readiness,
and send proposals through the existing seven-domain coordinator. The authored policy supplies
hysteresis, cooldown and bounds. A pending proposal waits for the coordinator's actual sequence
and effective acknowledgement; no second proposal or source write occurs during application.
Manual global `request`, including the same tier, disables adaptation. Lost domain/GPU evidence,
changed source bindings or transition timeout stops adaptation without presuming rollback.
Explicit `enable_adaptation` restarts observation after a stop; it does not reuse the stopped
controller or invent an acknowledgement. A manual request followed by enabling in the same frame
is supported; normal manual requests alone always leave adaptation disabled.
Runtime metrics/capture automation refuses adaptation even if enabled programmatically.
`GlobalQualityStats.adaptation` and transition-only logs expose its actual status, proposals,
eligible samples and acknowledged identity. This is not target-hardware performance qualification.
Water and environment now expose `request_effective` installation entry points. These consume
a pending guard against a caller-supplied current identity, source bindings and monotonic time;
rejection or a normal tier request clears the transient override without rewriting sources.
Water manual previews remain higher priority, and an absent SSR distance ceiling remains `None`.
Observations distinguish requested installation identity from actual component/material checks.
The global coordinator revalidates the cached snapshot before installation, clears stale
overrides without rewriting manual tiers, and records current CPU observations for all seven
domains. It runs the guard on real elapsed time, with a 30-second deadline and no automatic
retry of an unchanged timed-out plan. Timeout reports failure without claiming rollback;
valid derived values remain installed. A render-world queue probe now reports completion
after submission, bound to plan identity, active camera and viewport. It waits for a stable
nonempty pipeline cache and keeps one callback outstanding for its current key. Stale
callbacks cannot cross key/device resets; current pipeline failures reach the guard.
Queue completion alone cannot reach Ready. The coordinator also requires the asset, camera
and effect inventories below, fresh observations, matching render target/viewport and renderer
epoch, and all seven current CPU domain observations. This acknowledges the effective plan's
prepared/submitted renderer state, not every rendered pixel or the complete authored preset.
Web runtime and device-recovery qualification remain open.
No adaptive tier selection is enabled.
The queue probe now also inventories all current Mesh3d references, supported material
families and image dependencies, including hidden LODs, terrain detail and environment maps.
It requires prepared meshes with vertex/index/morph allocations, prepared materials with
actual bind groups, and GPU images. Missing/ambiguous source materials and unsupported
dependencies block this inventory. The native High smoke observes 501 meshes, 8 materials
and 5 images ready before queue completion. This is not yet qualification of all view-owned
buffers/bindings, shadows, atmosphere or post-processing resources.
The probe additionally requires a single matching render view with a bound view uniform
buffer, its offset, actual MeshViewBindGroup component and a depth texture covering the
viewport including its origin. Moving the viewport origin invalidates the old callback
even if its size is unchanged. `view.camera_resources_ready` covers these public camera
resources only, not shadow/atmosphere/post-process resources or rendered pixel correctness.
The effect inventory adds requested shadow atlas dimensions and complete directional cascades,
light/contact/fog uniforms, atmosphere LUT sizes and uniform/medium resources, compiled SSR
pipeline plus uniforms/prepass/noise, Bloom textures/bind groups and tonemapping pipeline/LUT.
Requirements come from the current effective plan and authored sky/fog/Bloom intent. Missing
context is not an empty successful inventory; changed requirements revoke pending callbacks.
`effects.public_resources_ready` is limited to these public prerequisites. Private atmosphere
and transient render-node bind groups are not directly inspected; normal Bevy preparation and
submission consume these prerequisites. No independent pixel-correctness claim is made.
The application guard may now reach Ready with matching current evidence, retaining the first
accepted renderer epoch. Loss of evidence invalidates that application terminally. After a
previously Ready application loses renderer proof, the coordinator may wait for fresh ready
resources with unchanged optical installation and seven matching CPU domains, then install
a new guard/sequence and require new GPU evidence. The failed guard never becomes Ready again.
Manual changes, domain mismatch, timeout, pipeline rejection and ambiguous camera are not
automatic recovery permissions. `quality_application_changed` logs state/identity transitions,
not every frame. Adaptive selection stays off and `full_preset_applied` remains false.
The native surface-tint mapping is now supported when the matching water domain and
authored shader uniforms are observed, with a finite in-range strength and no manual
tint-disable override. Its on/off/restore path has a native frozen-frame proof; this
does not certify every GPU or the current frame. Web remains unqualified separately.
Loss of those conditions changes the capability signature and invalidates the candidate.

The global quality coordinator invalidates its previous dispatch when any of its seven
request adapters is absent. It retains the requested tier and resends it once all adapters
return, including when the tier name did not change. No partial fan-out while one is missing.
This recovers request delivery only, not GPU readiness or a complete preset. Replacement
without an observed absence and live policy migration are not qualified by this mechanism.

An explicit global `request(tier)` also resends an unchanged tier, so a later global
choice can replace individual adapter tiers. It is consumed once, not continuously:
individual changes after dispatch remain intact until another global command. Each
fan-out resets application evidence while preserving its monotonic sequence counter;
an equal plan hash cannot reuse the previous optical installation or GPU acknowledgment.
Optical preview switches remain owned by their adapters and are not reset by this command.

GPU diagnostics expose optional `memory` allocator accounting, sampled at most every five
seconds through the existing wgpu device. Allocated and reserved bytes are separate; an
unavailable backend report is null, never zero usage. Frame/time identify sample freshness.
This is not resident VRAM or total process GPU memory. Native Vulkan is observed; Web builds,
but the browser backend reports no allocator totals. See `R1_038_GPU_ALLOCATOR_MEMORY.md`.

Terrain grass shading includes bounded dry-litter tint using the existing filtered broad/medium
detail samples and authored earth palette (maximum 32% blend inside the grass layer). Canonical
material weights remain untouched. This adds no texture, sample or noise hash and keeps the
existing wetness path; it is not a claim of photorealistic ground or improved GPU timing.

Shrub near presentation now uses 48 discrete diamond leaves in a staggered rounded crown
instead of three solid cones. The mesh remains shared (192 vertices, 96 triangles versus 63/21),
with a fixed lower ring and increasing wind weights. This changes derived geometry only:
scatter, authored placements, source hashes and distant HLOD representation are unchanged.
This is not a performance optimization or an accepted natural-vegetation result.

Fern near meshes retain seven radial fronds but replace the broad solid flags with two narrow
rachis segments and three paired leaflets per frond. The shared mesh is 224 vertices/112 triangles
(formerly 28/14). Root vertices retain zero wind weight; scatter poses and far HLOD are unchanged.
The extra geometry is for silhouette detail, not a claimed performance improvement.

ADR-0010 preparation: `resolve_vegetation_prototype` reads loaded `GltfNode`/`GltfMesh`
assets and returns three LOD lists sharing the original mesh and glTF material handles.
It creates no entities or colliders. The caller must first validate the catalog and file hashes,
observe dependency load failures, and resolve once per prototype. Baked flat nodes only are
supported; hierarchy, skin and animation-root nodes are refused explicitly. Positions/UV and
transforms are checked, LOD0 bounds must match the declared height/root within 1% (minimum 2 cm).
The returned transform normalizes the prototype before the unchanged placement transform.
This resolver is preparation only. A successful resolution is not a rendered-forest qualification.

Startup loading is now available through `VegetationPrototypeLoadPlugin`: validate against
the compiled vegetation and verified kit, then optionally select the existing `forge-kit://`
memory source. It deduplicates GLB requests and publishes a cache only when every binding
resolves. `Loading`, `Ready` and `Failed` are explicit; dependency errors and a 120-second
deadline terminate preparation. Native preparation is exercised; Web startup integration remains pending.

The preparation cache also retains the existing `StandardMaterial` handles produced by
Bevy 0.19's PBR glTF handler (`MaterialN/std`, or `DefaultMaterial/std`). Native and named
memory-source paths are preserved. Materials are keyed by their glTF asset identity across
all resolved layers/LODs; neither material conversion nor texture allocation is repeated.
Missing PBR sub-assets fail preparation explicitly. These are still unmodified PBR materials:
the procedural palette shader must not be applied to them as-is.

The cache additionally prepares `render_layers`: root-normalized derived meshes using Bevy's
position/normal/tangent transformation, with the original UVs and vertex colors retained.
One derived mesh is shared for each unique source mesh and normalization transform, including
reuse between layers/LODs; there is no per-placement mesh copy. All derivations are validated
before any are added to `Assets<Mesh>`. Non-finite/degenerate transforms, shear, missing or
invalid normals are refused. Original GLB meshes and shared PBR materials stay untouched.
These root-normalized meshes are preparation for wind, not a wind or scatter implementation.

The vegetation shader now has an explicit governed-PBR mode, selected by
`VisualValleyVegetationExtension::with_governed_prototype_height`. Its squared normalized
height weight anchors the root without consuming vertex alpha; UV1 stays an authored mapping,
not an HLOD root payload. The standard PBR input is retained instead of applying the procedural
palette. The current and previous-time vertex paths use the same weight. Default procedural
mode is unchanged. Animated prototype presentation and final chunk HLODs remain unqualified.

`VegetationPrototypeWindCache` now derives shared extended materials from the prepared PBR
handles (keyed by source material and target height), selecting forward rendering explicitly.
Texture handles, alpha mode and the source materials are preserved; unlit prototypes are
refused explicitly. The common vegetation clock updates these governed materials while keeping
their height and wetness uniforms, and leaves unrelated materials alone. The governed shader
applies wetness to RGB/roughness without changing alpha. The optional forest preview consumes
this cache; root-motion bounds and wetness parity still require GPU qualification.

`VegetationScatterProjection` exposes the trees from the exact shared generation result used
by the current render-quality projection. Startup and successful quality reconciliation publish
it together with that projection; no second scatter generation or persisted pose list is created.
Individual placement markers now retain the canonical layer ID as well as the placement ID.
This supplies the layer-to-prototype join. `VegetationPrototypeForestPreview` runs after quality
reconciliation: it removes procedural tree draw components, rebuilds woody HLOD meshes without
trees, and projects shared prototype LOD0/1/2 at unchanged poses. Canonical collider entities remain.
Far trees currently use individually culled shared LOD2, not optimized chunk HLOD. Tree-only HLOD
chunks stop drawing once empty, retaining their entities and mesh bindings for quality reconciliation.
`vegetation_far_geometry::build_far_geometry` prepares an owned CPU batch for one
chunk/material, without mutating sources or allocating renderer assets. It validates
layouts, indices, finite yaw/uniform transforms and bounded vertex/index totals before
cloning, converts indices to U32, and preserves authored UV/color attributes. Separate
root/scale and wind attributes retain per-tree state. This helper is not consumed by
the default projector and does not simplify geometry or qualify HLOD. The caller
must group by material; shader integration, bounds, culling and runtime proof remain due.
The subsequent `GovernedFarBatchPreview` opt-in now consumes it in the experimental
chunk/material projector, with conservative bounds/ranges and a per-root shader payload.
Studio requests it with `forge_forest=batches` and the governed kit. A fixed Chrome
comparison is recorded in `R1_FOREST_FAR_PATH_DIAGNOSIS.md`; dynamic wind/transitions,
native execution and performance remain unqualified. Grouping increases unique mesh
geometry and is not simplification. The default forest preview stays individual.
The opt-in far projector retains batches across quality changes when the ordered
placements, material grouping, normalized heights, chunk origin and exact source
meshes are unchanged. One snapshot per unique source mesh detects in-place edits
at the next projection, without copying merged geometry. Reuse updates ranges and
deformation bounds; changed inputs or missing owned bindings trigger preparation
before replacement. This is not automatic asset hot reload or transition qualification.
Quality changes now reconcile governed draws by (placement ID, LOD, primitive index), retaining
live entities for survivors instead of recreating the entire forest. Multi-primitive trees do not
alias each other. Only removed draws are despawned; changed ranges and shadow flags are applied
to surviving bindings. Tests cover all four profiles, decorative density changes and failure
recovery. This is derived presentation state, not a persisted Bevy identity or an FPS claim.
The preview is not hot-reload/removal qualified. Procedural geometry estimates remain
invalid; `VegetationProjectionStats.live_meshes` now counts actual mesh bindings and
CPU-visible triangles, excluding replaced placeholders. Missing/non-triangle assets
are counted explicitly; absent or ambiguous cameras produce no live count. These are
not GPU draw calls or post-occlusion counts. The `hlod` visibility bucket still includes
individual governed LOD2 primitives and does not prove chunk HLOD aggregation.
See `R1_FOREST_LIVE_TELEMETRY.md` and `R1_FOREST_FAR_PATH_DIAGNOSIS.md` for the current
counts and shader constraints on future aggregation. See `R1_FOREST_PREVIEW.md` for actual proof
and the visually rejected sparse canopy; no automatic promotion or default change is implied.

If preview preparation fails, the existing quality reconciler attempts to restore procedural
draws before removing the preview primitives. Missing recovery bindings are reported explicitly,
not treated as a successful fallback. The preview stops retrying after failure; its error remains
visible in statistics and the overlay, including after later quality changes. Recovery is covered
by synthetic ECS fault injection, not yet by a GPU failure scenario or document hot reload.

The opt-in `qualification` feature exposes `BridgeCrossingProbePlugin`, shared from the former
native executable harness. It drives the existing player controller and checks bridge contact,
grounding, stable bindings and the stop on the far bank; it is not a second movement controller.
The native runtime enables it for its existing probe command. The default engine `web` feature
does not include this harness. Studio explicitly enables `qualification` and opts in via
`forge_probe=bridge`. `in_viewport()` uses a bridge-aligned camera/input axis, publishes
`BridgeCrossingProbeStatus`, and leaves the app open with neutral controls after completion/failure.
Both modes wait for optional governed-kit physics readiness, retaining the 30-second watchdog.
Automated Chrome execution is evidenced in `R1_067_SHARED_CROSSING_PROBE.md`; it does not qualify
interactive input, human acceptance or Tauri.

With `physics-rapier`, `GlobalQualityPlugin` coordinates the seven installed quality adapters.
Requests fan out in PreUpdate, observations aggregate in Last. It never treats dispatch as an
acknowledgement; policy mismatches remain pending, water/environment remain partial, and full
preset acknowledgement is disabled. The rain domain uses the existing per-tier mapping.
Water requires observed SSR budget, surface tint and material reflectance; a camera budget
alone cannot acknowledge the available water settings. Native global cycling is evidenced in
`R1_040_GLOBAL_QUALITY_CYCLE.md`; Studio manual controls exist, automatic adaptation remains open.
`QualityAdaptationController::acknowledge_applied` requires all affected presets installed.
Do not feed `available_budgets_projected` into it as though partial optics were a full preset.

`VegetationQualityRequest` optionally selects initial vegetation density, LOD/HLOD/cull ranges
and blades per patch before plugin construction. Authored sources, collision-bearing layers,
wind and interaction settings remain intact. Incompatible derived presentation is rejected.
Vegetation statistics count selected presentation placements; `quality.canonical_placements`
separately records the unchanged canonical population. `initial_tier` is a CPU preparation
observation, not GPU readiness or a global preset acknowledgement. `request(tier)` now supports
CPU hot changes for this initially installed policy: surviving entities are retained,
removed decorative entities are despawned, and invalid preparation retains the prior projection.
Headless synchronous projection retains mesh handles. Renderer-backed native/Web transitions
now stage one replacement set, adding at most sixteen meshes per frame under a soft 12 MiB
budget (only the first indivisible mesh can exceed it; the next mesh is checked before adding).
This throughput adjustment follows a measured late High receipt with the initial four-mesh
limit; its runtime result remains to be qualified. Live meshes remain unchanged until all staged
vertex/index allocations exist and the renderer's submission completion callback fires.
Then logical keys/entities switch to the new derived handles; old CPU meshes are removed
and their released handles retire render allocations. These handles are not authored IDs.
Superseded requests, policy changes and invalidated tickets discard staged assets; source
hashes are rechecked before publication. No multi-profile cache is retained, but the single
pending replacement temporarily coexists with the old set. Initial startup and governed
forest HLOD rebuilding are not made incremental by this change. The global quality receipt
still checks the installed plan and its rendering resources separately; staging completion
is not full-preset, optical or performance qualification. Native/Web runtime validation of
this new path is pending; earlier captures qualify earlier binaries only.
Browser preparation now streams completed chunks into that same pending replacement while
later chunks are still being built. Mesh buffers move through one preparation-owned queue,
without cloning or exposing partial geometry. Preparation completion is required before a GPU
ticket can be issued; an abandoned/replaced preparation releases its unpublished uploads.
The native background worker still returns a complete preparation before staging. Final sources,
budgets and renderer readiness requirements remain identical; streaming is not qualification.
`requested_tier` and `projected_tier` distinguish rejection. Native cycling now passes with five
inspected captures; transition latency and seamlessness remain unqualified.
See `R1_040_VEGETATION_TRANSITION_IN_PROGRESS.md`. Four-tier preparation and native Low/Ultra evidence:
`docs/evidence/gate1/R1_040_VEGETATION_INITIAL_QUALITY.md`.

Terrain albedo/roughness detail samples a shared 128 × 128 R8 tile generated once with
a fixed seed. Three world-space scales preserve authored material weights; subpixel detail
fades to its mean. This is a renderer cache, not a source asset or displacement map.
Broad detail only modulates brightness (±6%); grass litter uses linear sub-metre
detail (0–16% earth tint, 8% unresolved mean), not thresholded broad color islands.
Authored layer weights and geometric relief remain unchanged.
Native/Web visual execution and the still-open Web performance qualification are recorded in
`docs/evidence/gate1/R1_TERRAIN_SURFACE_DETAIL.md`.

Player previews may supply an optional initial camera target. Only the horizontal heading
is derived; pitch, free controls and collision-aware camera behavior remain unchanged.
WorldSketch loaders aim at the zone centre without editing the authored spawn. Explicit
probes and the historical scene retain their default heading. Evidence:
`docs/evidence/gate1/R1_PLAYER_INITIAL_HEADING.md`.

Distant shrub/fern HLOD uses two rooted six-point silhouettes instead of pointed diamonds.
Each plant contributes 12 vertices/8 triangles (previously 8/4), with no extra batch entity,
texture, visibility range or collider. Fern HLOD keeps a spreading 2.1 m × 0.44 m envelope
before authored scale; trees and nearby meshes are unchanged. Placement-root UV metadata is
preserved for per-plant fading. These remain coarse crossed planes, not volumetric foliage.

Bank rendering clips the ribbon footprint against the canonical 2 m terrain cells and their
diagonals before sampling height. All resulting vertices use terrain height plus the existing
1.5 cm clearance; inner bank vertices no longer inherit the water surface elevation. Clipping
interpolates vertex colors and UVs and preserves the footprint. It does not change terrain,
water, authored documents or collision. Projection is bounded to one million visited cells and
one million output vertices; exceeding either budget returns an explicit adapter error.
Counts include the generated geometry and its extra terrain queries. This is a correctness
change with additional geometry, not a performance optimization; coarser terrain LOD agreement
and artistic shoreline blending require separate visual qualification.

Banks now use the terrain material extension rather than a separate flat RGB palette.
Their clipped vertices blend canonical grass/earth/rock weights toward earth at the water
edge with a smoothstep; the outer edge keeps the canonical weights exactly. World-space
detail sampling and wetness therefore use the same shader and parameters as terrain.
The bank material has a separate generated 128 × 128 R8 detail handle (same deterministic
contents), but no external image or additional shader. This is presentation only: biome
documents, water geometry and collisions are unchanged. Bank lighting now uses the normalized
interpolation of the canonical terrain vertex normals, not per-triangle bank normals.
This reuses the normals already evaluated during surface sampling. Far terrain LODs and
artistic quality still require separate visual qualification.

Water SSR cameras enable Bevy's existing temporal anti-aliasing with MSAA disabled.
Water/vegetation previous-position outputs are restricted to the motion-vector prepass;
transparent forward rain does not claim a motion-vector output. Native clear/rain capture
evidence and pending movement/WebGPU qualification are recorded in
`docs/evidence/gate1/R1_WATER_TEMPORAL_SMOOTHING.md`.

Adapts engine-neutral Forge documents to Bevy entities. `ProofScenePlugin` keeps the Gate 0 scene
API intact. `VisualValleyPlugin` consumes a validated `TerrainDocument`, asks `forge_world` for the
deterministic derived terrain and materializes its 64 chunks without becoming a persistent source of
truth.

The `native` and `web` features are built separately so Cargo cannot accidentally unify platform
requirements during qualification.

The Gate 0 overlay and `forge.runtime-metrics` report separate three different facts:

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

Schema v8 adds optional `global_quality` when the coordinator is installed. Global observation
runs before the metrics writer in Last. Seven typed domain states, requested tier, dispatched
requests and available CPU budgets remain separate from `full_preset_applied` (currently false).
Gate 0 omits the block; no inferred successful preset when the resource is absent.
See the ledger addendum in `R1_040_GLOBAL_QUALITY_CYCLE.md`.

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
Wet roughness now interprets the weather multiplier in microfacet-alpha space: the terrain,
procedural vegetation and governed foliage shaders multiply perceptual roughness by
`sqrt(mix(1, full_wet_roughness_multiplier, wetness))`. This deliberately replaces the earlier
direct perceptual multiplier, whose reduction was squared by Bevy. JSON values, albedo, alpha,
geometry and dry appearance are unchanged; existing wet captures are historical, not proof of
the new response. This is a shared rendering rule, not material porosity or puddle simulation.
Forge temperature/tint are normalized artistic controls in [-1, 1]; the adapter maps them to
CIE xy offsets with a maximum magnitude of 0.05 instead of passing normalized values directly.
Contrast above one uses a positive power curve around linear grey 0.18:
`(0.18 * (x / 0.18)^contrast)^(1 / gamma)`. The adapter implements it with
Bevy's existing gain/gamma fields, avoiding the subtractive linear contrast
that clipped dim water and other shadowed surfaces to black. Contrast <= 1
retains its previous linear response, including the intentional flat result
at zero. Authored exposure, gamma, saturation, temperature and tint remain
active; JSON is unchanged. The mapped diagnostic fields describe Bevy values,
not a verbatim copy of authored contrast/gamma. Old weather captures do not
qualify this new response; native/Studio evidence must identify the build.
`request_rain_quality` rebuilds only the derived rain mesh, in place at its existing asset ID.
The batch entity, material, simulation clock and immutable weather source remain intact; bounds
are recomputed. `with_initial_rain_quality` selects an initial budget. Requested and projected
tiers remain distinct in telemetry; neither acknowledges the unrelated global quality controller
nor claims render-world/GPU readiness. Dynamic changes are currently exercised by ECS tests,
not exposed as a Studio control.
The environment adapter attaches Bevy's atmosphere-generated environment map to the active camera
when the sky is enabled: authored map resolution, physical unit gain, ambient fallback retained.
Disabling the sky removes that map component. The environment ledger records component presence,
resolution and gain, not a claim of GPU execution or WebGPU qualification.
Environment projection runs once at startup; its observer now runs in `PostUpdate`, after
quality/weather writers in `Update`. Camera topology, shadow enablement, environment-map size
and tonemapping therefore reflect runtime changes instead of a frozen startup snapshot.
Explicit no-tonemapping is accepted only when requested by the validated post-process policy.
This observer never reapplies authored settings; fog/source parameters remain declared intent,
and structural completeness is not a full policy or GPU qualification.
`EnvironmentQualityRequest` now uses an owned probe for opt-in Runtime sky-lighting budgets.
Resizing replaces the derived probe, not the camera or source. Filtering stays on that probe;
PostUpdate binds its already-filtered `EnvironmentMapLight` to the camera, after checking CPU
raw/specular dimensions and diffuse availability. A second camera generator previously caused
a stale-mip GPU error at Ultra; it is removed. Native High/Low/Medium/Ultra/High captures now pass.
Late opt-in on a historical camera cache is still rejected. Studio and seamless-transition
qualification remain open. See `R1_040_ENVIRONMENT_NATIVE_CYCLE.md`.
During an owned probe resize on the same camera, its previous filtered lighting is retained
until a dimension-valid replacement can be published. `texture_bound` stays false while that
replacement is pending: retaining presentation is not acknowledgement of the requested budget.
Disabling the probe or changing its camera still removes the previous lighting. This avoids an
intentional empty-lighting interval; GPU readiness of the replacement remains a separate concern.
Volumetric fog remains unavailable; no complete environment preset is acknowledged.
Publishing compares both texture handles, intensity, rotation and the lightmapped-diffuse flag.
A stable specular handle does not suppress updates to the other lighting fields; identical
bindings leave the camera component change tick untouched. This is CPU binding consistency,
not evidence that the filtered GPU texture or the final forest lighting is correct.

Vegetation installs Bevy's `NormalPrepass` on 3D cameras, including cameras created later. In Bevy
0.19 the opaque depth-only prepass otherwise omits fragment visibility-range rejection, leaving
depth silhouettes where forward LOD dithering discards color. The normal prepass preserves the
standard dither and depth test in both stages, at the cost of a packed normal attachment and its
fragment work. This is a rendering correctness requirement for this adapter, not an SSAO feature.

Vegetation LOD crossfades occupy at most the final eight metres of each declared interval
(High: 64–72 m and 172–180 m). Individual trees use their model origin. HLOD silhouettes
encode their undeformed placement root in UV0 (XZ) and UV1 (Y in X); UV1 is reserved to these
owned, untextured procedural batches. The shared vertex shader uses that root for the same
signed Bevy dither in forward and normal prepass. CPU HLOD ranges use the chunk origin with
a conservative placement-radius allowance; shader cutoff remains the declared per-placement
far distance, not the enlarged batch range. The material's profile uniforms follow quality
changes. This retains 64 HLOD batches, geometry, canonical placements and physical bindings.
It does not qualify continuous movement or WebGPU rendering by itself. See
`docs/evidence/gate1/R1_VEGETATION_LOD_ANCHORS.md`.

`PlayerInputFocus` lets the host gate the existing interactive sampler at runtime. Native input
remains enabled by default; Studio initializes it disabled and enables it only for its captured,
focused viewport. Disabled input cancels held movement and queued one-shot actions, preserving
the monotonic sequence counter. Probes that disable the interactive sampler remain unaffected.

`WeatherProjectionStats` observes main-world entities, camera components and CPU material uniforms.
It deliberately reports GPU execution, native/WebGPU visual review, splashes/occlusion and advanced
puddle response as deferred; those require the later runtime/Studio capture and qualification lot.

`PostProcessQualityRequest` is an optional validated area/policy resource consumed by that same
weather projection system, not a competing writer. Quality can suppress authored bloom, replace
weather grading with neutral defaults, or select explicit no-tonemapping. Restoring a tier uses
the current weather grade without resetting precipitation, time, rain bindings or source data.
The post-processing observer checks one active camera, effective bloom presence, all grading
fields and the tonemapper before reporting a CPU-projected tier. It does not acknowledge the
global adaptive controller or claim GPU execution. See `R1_040_POST_PROCESS_BUDGETS.md` for the
native bloom-pass evidence and the limits of synthetic off/on and quality-cycle tests.

`WaterQualityRequest` validates area identity and policy before the existing water plugin
consumes its SSR budget on one active 3D camera. Switching tiers preserves authored water,
mesh/material identities, simulation phase and any existing shared TAA configuration.
`WaterProjectionStats::quality` reports actual CPU SSR presence/steps separately from authored
optics. It also reports the actual `surface_tint_strength` and `surface_tint_projected`:
Disabled sets the existing depth-tint uniform to zero; SurfaceTint restores the authored
strength; unsupported ScreenSpace restores the authored fallback without claiming real
screen-space refraction. Only the governed water material is modified, with one active camera.
`shader_uniforms_match_profile` compares the authored extension plus the active quality
override and animation phase. These fields describe CPU material state, not a GPU verdict.
The supplied presets do not request Disabled; its live visual qualification is still due.
`preview_surface_tint(Some(false/true))` temporarily disables/restores authored tint;
`None` follows the selected preset again. Tier changes preserve this preview without
rewriting the policy. Studio exposes these three actions and displays the observed override
and material value, not an optimistic acknowledgement. Browser interaction is verified;
the water-pixel effect and Tauri qualification remain separate, pending checks.

The existing SSR evidence remains separate from authored
`resolved_optics` intent. Reflection distance, screen-space refraction and complete environment
reflection qualification are unavailable: `full_preset_applied` remains false, with an explicit
warning per requested tier. Reflection Disabled now removes the camera SSR component and sets
the governed water extension's reflectance to zero; Environment/ScreenSpace restore the authored
value. Shared TAA/prepasses and other material assets are retained. `material_reflectance` and
`material_reflectance_projected` observe this CPU state separately from the camera budget;
neither is proof that the GPU has suppressed reflections. The supplied presets are unchanged.
`preview_reflections_disabled(true)` temporarily selects Disabled; false resumes the current
preset. This override survives tier changes independently of surface tint. Studio exposes
disable/follow-profile without saving authored sources.
See `R1_040_WATER_SSR_BUDGETS.md` for native evidence and limits.

`VisualValleyShadowQualityPlugin` validates the area identity and quality document before
installation. `ShadowQualityRequest::request` applies the selected preset's shadow enablement,
cascade count, exact outer distance, global atlas resolution and contact-shadow components.
It requires a single directional light (the valley sun) and one active 3D camera before changing
the shared atlas. Sun entity, pose, color and illuminance remain unchanged. Updates precede
Bevy's cascade rebuild; observation follows it. Disabled shadows keep a valid atlas allocation
and remove contact shadows without removing the depth prepass used by other effects.
`VisualValleyShadowStats::quality` distinguishes requested settings and CPU-projected tier;
this is neither GPU execution evidence nor acknowledgement of a global quality preset.

Single-frame automation now gates its earliest capture frame on observed pipeline-cache and
scene readiness, tracks this request's GPU readback, and fails if the original exit deadline
arrives before completion. It installs the observer only for capture runs and leaves frame
sampling untouched. See `docs/evidence/gate1/R1_039_CAPTURE_READINESS.md`; this is not exhaustive
pipeline prewarming or pixel validation, and bridge-video capture retains its separate harness.

`VisualValleyTerrainQualityPlugin` projects the terrain section of the validated quality policy.
Canonical full meshes and physics bindings remain untouched (canonical render entities hidden).
Three derived mesh siblings per chunk are recentered at the same source bounding-box center:
Bevy's GPU dither uses model origin, so CPU-only `use_aabb` is insufficient for absolute-position
terrain meshes. Visibility ranges use that shared origin with complementary transition bands.
Source updates are observed in Update after fixed-schedule transactions. Changed chunks are
fingerprinted and all payloads/bindings validated before replacement; mesh handles remain stable.
No per-frame topology reconstruction occurs when source hash and requested tier are unchanged.
Allocated geometry increases because the canonical copy remains available for transaction proof.
`TerrainLodProjectionStats` and native `terrain_lod` metrics explicitly report CPU allocation,
not active GPU LOD counts or a global-controller acknowledgement. See
`docs/evidence/gate1/R1_040_TERRAIN_LOD_RENDERER.md` for the rejected first projection, corrected
native High smoke, partial-update proof, corrected Low horizon and remaining Studio/motion limits.
Protected bank chunks retain their full-resolution, non-morph geometry across quality-only
changes while their visibility ranges still follow the requested profile. Source edits and
protection changes invalidate that reuse. Ordinary chunks retain topology-dependent morph
reconstruction; this does not assume their LOD0 payload is invariant. No additional mesh cache.
The final fade width is capped by the first transition width, leaving a fully opaque far LOD
between its incoming band and the outer cull band. The fixture's 896m cull retains terrain
coverage from its authored cameras and all ground-level positions inside the finite valley;
this is not a general promise of coverage for arbitrary larger worlds or flying cameras.

`environment/volumetric.rs` prepares bounded Bevy fog components. Its projection runs after
transform propagation, opt-in through `VolumetricPreviewRequest` (default off), without a UI/CLI
switch enabled by default; Studio exposes an experimental toggle and Runtime `--volumetric-preview`. It owns its camera component,
volume and added sun marker, preserves foreign components, follows
the camera and cleans up on disable. It uses the sun's existing shadow maps, retaining distance fog. The conservative
inscribed cube is not a spherical ray-march cutoff. Source documents remain unchanged; actual GPU
rendering was smoke-tested but rejected visually (hard boundary, excess darkening); temporal compatibility remains unqualified. See
`docs/evidence/gate1/R1_040_VOLUMETRIC_PREPARATION.md`.

Procedural tree collision is separately derived by `vegetation/collision.rs` from canonical layer
profiles. One fixed body per tree stays independent of render LOD/density; horizontal player range
controls `ColliderDisabled` before Rapier SyncBackend. See `R1_TREE_TRUNK_COLLISIONS.md` for proof
and scope; this is not foliage collision or an editor regeneration path.

Grass roots use `TerrainGenerationPlan::surface_height_at`: the exact triangulated height
without unused normals/material evaluation. This is not the continuous recipe's `height_at`
and does not cache or approximate the terrain. Native CPU preparation measurements and
bit-exact surface regression tests are recorded in `R1_GRASS_HEIGHT_ONLY_PREPARATION.md`.

The experimental volume now samples one cached procedural 32³ R8 density texture (32 KiB texels).
Density smoothly reaches zero on all six faces over the outer quarter of each axis, while
retaining full authored density in the center. This does not rewrite the authored profile or
add any external asset. The moving, camera-bounded volume and cumulative distance fog still
require visual qualification; this is not a spherical-distance shader implementation.
The preview additionally caps the derived volume at the sun's last configured shadow cascade,
without expanding shadow maps. `effective_maximum_distance_m` reports this conservative cap,
separately from the requested environment budget. Invalid/missing shadow coverage disables the
owned projection; the distance-fog fallback remains. This cap is not proof that every point is
illuminated or inside the actual shadow-map texel footprint.

The explicit ignored test `quality_commit_cpu_probe` separates procedural vegetation
CPU preparation from the real ECS/asset reconciliation. Run it with `--ignored
--nocapture` when diagnosing that phase. It measures neither governed forest batches,
browser scheduling, GPU upload nor frame presentation; it has no universal timing threshold.
It also counts vertex/index payloads identical to the preceding live projection,
outside those timers. That comparison is diagnostic only, not a production mesh
equivalence rule or proof of avoided GPU transfers.

Web vegetation quality preparation retains one immutable generation plan keyed
by both terrain and vegetation content hashes. Its canonical scatter cache is
reused across tiers, while render density and geometry still use the shared
preparation code. Source changes replace the plan; a pending stale result is
still rejected before touching live bindings. This retains one source plan,
not meshes for every quality tier, and does not change native worker behavior.

Web terrain and vegetation preparation cooperate at existing chunk boundaries:
each adapter call stops after 4 ms of CPU wall time or eight steps, whichever
comes first. This is a soft budget checked after a chunk, not preemption of a
large chunk and not a shared frame/GPU budget. Cheap chunks no longer each wait
an entire frame. Previous live projections remain intact until completion;
source/policy cancellation and error handling are unchanged. Native workers
and synchronous preparation do not use this browser scheduling budget.

Governed forest preparation scans bounds once per unique mesh per preparation,
not once per primitive instance. The small cache borrows mesh assets immutably
and is dropped after the call; edits/removals are observed by the next preparation.
Missing geometry remains an error. No persistent invalidation heuristic or GPU
geometry change is introduced by this bounds reuse.

`QualityCpuTracePlugin` is opt-in diagnostic instrumentation used by the Studio motion
qualification URL. It clears six main-thread phase samples each frame: terrain preparation
poll/commit, procedural vegetation preparation poll/commit, governed forest inclusive and
far batches. Missing phases remain null. Forest-inclusive contains far-batch work and must
not be added to it. Native preparation poll time excludes detached worker work. It does not
measure bank-bound discovery, asset extraction, GPU uploads or GPU execution. The Studio
retains eight slow frame intervals per stage and phase maxima, with preceding-frame CPU
samples explicitly identified; interval time also includes uninstrumented work and waits.
No readiness criterion, geometry, source document or normal Studio collection is changed.

Terrain LOD transitions now use Bevy mesh morph targets for position and normal deltas,
prepared from the common refinement of both endpoint surfaces. Only one terrain LOD is
visible at a distance; its morph finishes before the next topology starts. The original
terrain/colliders remain untouched. A single active camera updates scalar weights, not
vertex arrays. The native heroic clear/rain captures no longer show the large stippled
terrain patches. This is partial qualification: preparation/storage grows, material-color
morphing is not implemented, bank intersections and moving/Web transitions remain open.

Changed-chunk LOD preparation uses the existing Bevy compute pool with scoped, borrowed
geometry inputs. Directly spawned tasks retain input order; every result is validated before
the existing serial mesh commit. No ECS/asset access occurs inside workers and no job survives
the scope, so source edits cannot race a detached result. Algorithms, morph payloads, bank
protection and source/collider bindings are unchanged. The single-thread path remains valid
for WASM. `quality_projection_cpu` logs isolate terrain and vegetation preparation/application
costs during actual changes, without logging idle frames or labelling these CPU times as GPU work.

Canonical bank triangle bounds now constrain intersecting terrain chunks to full resolution
in all three exclusive distance phases. Other chunks retain morph LODs. This render-only
override reports `bank_constrained_chunks`; the heroic native fixture constrains 20 of 64
chunks, increasing derived geometry to 149789 vertices / 274416 triangles. Two fixed native
captures and targeted bank tests pass. In-place mesh edits crossing a chunk boundary and
recovery after invalid indices are tested, preserving terrain handles and the source hash.
Workspace regression, strict Clippy and Studio WASM check pass; movement and visual Web
qualification remain pending. The integration still uses the prototype bank marker.

Water wave gradients are now evaluated per fragment before PBR preparation in both shader
branches, not interpolated across river strip vertices. Authored wave parameters and vertex
displacement are unchanged. Native heroic clear/rain captures and 26 targeted water tests
pass; see `R1_WATER_FRAGMENT_NORMALS.md` for cold-pipeline failure, build-cache relocation,
evidence pins and remaining motion/Web/performance limits.

The current candidate adds two short-wave normal gradients, derived from the authored
wave scale and faded by pixel phase footprint. Geometry, collisions and large-wave
displacement remain unchanged. Native fixed/close captures show finer near-water detail;
the same detail is built and captured in Studio. The pattern is still regular,
and motion/performance/artistic acceptance remain open. See
`R1_WATER_RIPPLE_DETAIL.md`; this does not qualify water or Gate 1.

`VisualValleyWaterPlugin::with_road_surface` optionally applies the compiled road
mask to bank material weights, after validating the terrain identity. Runtime and
Studio WorldSketch pass the same projection to terrain and water. Bank positions,
normals and indices are unchanged; `bank_road_surface_projected` records the mask
identity. This is initial scene projection, not incremental road editing. Partial
mask interpolation does not promise pixel-identical blending at every seam.

Browser terrain quality preparation is staged across main frames, one canonical
chunk per call. It reuses the existing LOD/morph calculation, retaining the live
projection until the complete result passes source/policy/tier/bank identity and
binding checks. Superseded work is dropped; a malformed chunk cannot publish a
partial result. Native background preparation is unchanged. Snapshot copying,
bank intersection checks and the final mesh application are not made incremental
by this path; it does not guarantee hitch-free frames or faster total preparation.

The player controller keeps a downward collision probe while grounded at rest,
but disables sliding contact resolution for that idle state to avoid accumulating
Rapier's normal nudge along walkable slopes. Horizontal motion, upward motion,
airborne state or an observed steep-slope slide restores sliding. No transform
is pinned and no map-specific coordinate is used. The slope regression runs
30 seconds without input after settling; movement/jump/steep-slope tests remain
part of the controller qualification.
