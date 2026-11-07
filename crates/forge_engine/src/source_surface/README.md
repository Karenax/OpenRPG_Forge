# Source-profile surfaces

This module owns execution shared by Studio, native modules and the reference.
`SourceSurfaceMaterial` retains the original material bindings, kind specialization,
culling and alpha policies. `mesh::SourceMeshBytes` validates finite ordered attributes
and indices before creating a mesh. `underwater::CausticSampleParams` is a material
input contract, independent of an ocean implementation or a map.

The shader UUID and custom attribute IDs/formats remain stable. Profile shader
defines and attribute names are retained ABI values; they do not select a module.
`neutral_scene_material` preserves admitted scene receivers while accepting only
the explicitly supported opaque plain surface properties. Unsupported effects or
textures are rejected, never discarded.

The caller admits and prepares shader/resources, installs receivers and then calls
`stage_prepared_shader`. This function adopts bytes into existing shader assets;
it does not prove shader compilation, draw completion or current GPU ownership.
It neither looks up a reference package nor supplies missing textures or lighting.
The current acquired shader integration still has generic work outside this owner.

`ImportedSunRelayPlugin` publishes the existing solar contract from resident surface
materials in `SurfaceSunPublication`. Scene effects order after this set instead
of making the engine depend on a map's effect system. The existing source-selection
policy is retained; this transfer does not create a second authoritative solar clock.

The transferred tests cover mesh data/refusals, neutral material preservation and
refusals, shader adoption and publication/refusal/recovery/retirement before a scene
consumer. Real module rendering is qualified separately. Attribution for source
shader adaptations remains in `third_party/tidewater/LICENSE`.


## Object geometry and physics

`geometry::PreparedMeshes::from_parts` takes caller-admitted parts keyed by exact
asset ID/hash. The acquisition adapter validates its package, geometry and material
names before constructing this input. Staging adopts owned geometry once; repeated
placements share handles. Revision lookup never substitutes another model.

With `physics-rapier`, `objects::PreparedScene::new` resolves all placements, material
assets and indexed finite triangle surfaces before any scene replacement. Its
existing part/triangle budgets remain. `stage` replaces derived entities; canonical
documents and undo are untouched. `ResidentScene::is_synchronized` requires current
root/part identities, transforms and backend colliders, and actual retirement of
previous backend handles. Merely despawning ECS nodes is insufficient.

`objects::placement` retains the Commands-based entity projection for callers that
own their collision policy. Source package decoding, other object families and
module-origin acquisition are separate integration work. The independent engine
tests exercise synthetic module data; real-package tests exercise 23 buildings and
GPU shadow depth. Neither establishes full color-material or Studio fidelity.


## Closed package inputs

`package` retains the existing admitted geometry and texture preparation, including
local/source-reference attribute choice, mip payloads and sampler parameters. Its
input is `forge_assets::source_surface_package::SurfacePackage`, not a map crate.
Module API 7 can bind that profile to an area with an exact resource and asset
inventory. Transport validates the profile and preserves it through origin-aware
exports. Consumers must still explicitly acquire and adopt those bytes; no palette
is inferred from terrain geometry and transport success does not certify a draw.

## Native module host (26 October)

`package::material::prepare` is the existing source parameter/texture constructor
shared by the reference adapter and modules. It preserves caller-owned lighting
bindings. `SourceSurfaceMaterial` enumerates its image and storage-buffer
dependencies for the real typed render inventory.

With physics enabled, `package::host::PreparedHost` stages the admitted palette
once, resolves only canonical placements, and uses the common object projection
and current source scene. `install_without_water` explicitly represents absent
water; authored water needs its receiver integration. The caller stages the
admitted shader. No package or environment is inferred from a reference map.

Source object primitives join common shadow coverage and static-source accounting.
Their HLOD material route retains original detailed draws until their local
coordinates, animation and transparency have a reviewed baking route. This does
not claim aggregation. The private API-7 native run exercised two house placements,
six shared meshes, four materials (kinds 0/1/2/4), twelve registered collision
primitives and current GPU inventories, with both captures inspected. Full
reference fidelity, other material kinds, Studio and performance remain separate.
Evidence: central `module-object-runtime-validation-001.json`.
