# Tidewater underwater light maps

Runtime derivative of the verified optical material, adapted from
`UnderwaterLighting.js` at `1438b1ab`. No map data or GPU handle is authored.

`register` takes the resident optical material layout. `install_shader` composes
the verified optical module with the bake entry, and `Surface` identifies its
prepared material. The compute pass follows `OceanComputeSet` and
`ShoreComputeSet`, before `camera_driver`. It reuses that material's FFT,
canonical terrain/corrections, shore simulation, coastal waves and sea-detail
textures. It dispatches 64 × 64 × 2 workgroups of 8 × 8 threads.

The source's four 512² RGBA16F textures use two 2D arrays, with one layer per
extent (128 m / 1 024 m). Waves contain height relative to sea level, XZ slope
and foam. Level contains the coarse mean height and local gust/slick factor.
Origins snap to 0.25 m / 2 m from the optical **GPU** camera, and are published
with its actual frame index/time into a 32-byte storage buffer. Fragment
receivers read this same buffer after the compute pass, so camera motion does
not rebuild their material bindings. The arrays occupy 8 MiB without mipmaps.

`Targets.observation` is an asynchronous readback of that GPU-written buffer;
`encoded_frames` counts command encodings and is not a completion fence. A
frozen comparison waits for the drained ocean, query and light-map timestamps
to agree. A live readback can lag and is not the current simulation state.
The native optical material uploads its camera matrices/positions after
transform propagation every render frame, independently of CDLOD selection.
Native frozen-pair qualification also requires the GPU origins to match the
actual camera snapped at both texel sizes. A shader that privately overrides
its camera from Bevy's view does not update these compute inputs by itself.

Terrain uses the full waves/slope/foam/detail lookup and the shared photon maps.
Village, piers and boardwalk materials use mean-level attenuation without
photon caustics. Plants and the optical water material keep their own lighting.
The material sampler prefers the near layer, then the far layer, and uses flat
sea beyond both; the half-texel bounds match the source.

`Control` adjusts receiver lighting and caustic strength (source default 0.75).
These presentation settings live outside authored documents. Disabled scenes
bind `NEUTRAL_FRAME` and two-layer neutral textures; explicit native diagnostic
opt-outs remain disabled. Missing pipelines/materials wait for preparation;
GPU validation failures are reported by the renderer rather than hidden.

The full/lite receivers also remap object sun-shadow lookups to the refracted
entry point at the sampled wave/mean height. `water_absorption.w` is the
runtime shadow-refraction control; XYZ retain their physical coefficients.
The source depth-dependent jitter uses the GPU map frame stamp. Its temporal
resolve/TAAU and the monochrome refraction-capture variant remain separate
work. These lighting terms do not prove complete source-image parity.
