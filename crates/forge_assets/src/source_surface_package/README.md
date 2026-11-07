# Source surface package codec

`SurfacePackage` retains the existing hash-pinned source-profile reader, including
its historical schema, acquisition revision, catalog and material/texture checks.
These are profile identities; they do not select a module or an area.

`load` and `load_async` use caller-owned bounded transport. The complete index is
checked before any payload request. Each payload and dependency is checked by size
and SHA-256. Source URIs are metadata and never network or filesystem authority.

`resource_pins` and `license_name` expose validated index metadata. They do not
establish full payload admission, rendering or redistribution rights. Full readers
must call `load`/`load_async`. The module resolver additionally requires exact resource
closure and the source license in the module's declared license provenance.

The source profile currently retains its 23-entry palette and original qualification
guards. Every module may reuse that admitted palette; the engine's geometry cache is
not limited to these IDs or this dataset. Native/Studio consumers use the common
`forge_engine::source_surface::package` preparation, qualified separately.
