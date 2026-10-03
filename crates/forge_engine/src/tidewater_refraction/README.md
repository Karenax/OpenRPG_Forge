# Tidewater refraction capture

Source: `dgreenheck/tidewater@1438b1ab`, `src/ocean/RefractionPass.js`,
`WaterMaterial.js`, and `UnderwaterLighting.js`. MIT; copyright 2026 DRG
Software Solutions LLC; see `third_party/tidewater/LICENSE`.

This adapter renders the existing terrain and opaque village materials into
an RGBA16F target at half the physical playtest viewport resolution. RGB is
lit color, alpha is coverage; a separate Depth32Float copy uses reversed-Z.
Fragments above sea level +0.4 m are discarded. Plants and water are absent
from the capture layer. Opaque village parts whose transformed bounds are
entirely above the clip plane are excluded before rendering. The terrain
capture samples the same photon maps without chromatic dispersion.

The render mirrors share source meshes and image/buffer handles. They have
no authoring identity, physics, or persisted document. Moving, replacing,
hiding, or removing the source updates the mirrors. Unused mirrors and typed
material handles are retired. Bevy's existing material plugins and renderer
perform the pass; this does not introduce another GPU stack.

The derived camera renders before the main view, uses its final propagated
pose and exact projection, and scales its temporal jitter to retain the same
NDC offset. It receives no skybox, post effects, or editor inspection light.
Resize keeps target handles stable. The capture depth cannot overwrite the
main opaque-depth copy. Coverage/depth failure, a disabled control, or an
unavailable capture retains the original opaque fallback in the optical shader.

Both Studio's resident optical material and the native proof's optical material
use this pass. The adapter selects its main camera before the `Synchronize`
system set and publishes `Observation.ready` to its water material afterwards.
Capture synchronization depends on the scene camera and shared terrain/village
materials, not on Studio's resident-ocean resource. Adapters must exclude
`CaptureCamera` from their main-view depth copy and camera selection. The native
water uses the same strict shader patch and color/depth/readiness bindings 51–53.

`Observation` reports CPU synchronization and encoded commands only.
`GpuObservation` reads actual color/depth textures once during a held
comparison, after 40 frames; row padding is excluded from metrics. These
asynchronous observations are not a render fence. The readbacks stop on
completion and are removed on resume. They impose no ongoing gameplay cost.

Qualification is tracked in `docs/evidence/tidewater/`. CPU tests or a
successful build do not establish visible source parity. The source's TAAU,
reefs/rocks, and transparent fish remain separate qualification work.
