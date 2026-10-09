# Authored texture slots in source-graph materials

The versioned material contract preserves declaration order and every source
image. Schemas 2–5 admit at most seven authored textures. Schema 6 admits ten;
like schemas 3–5, it requires measured imported storage for every texture.
Schema 6 does not change the matrix/time uniform or normal-decoder numeric ABI.

| Slot | Texture binding | Sampler binding |
|---|---|---|
| 0 | 101 | 102 |
| 1 | 103 | 104 |
| 2 | 105 | 106 |
| 3 | 107 | 108 |
| 4 | 109 | 110 |
| 5 | 111 | 112 |
| 6 | 113 | 114 |
| 7 | 115 | 116 |
| 8 | 117 | 118 |
| 9 | 125 | 126 |

Bindings 120–124 remain reserved for scalars, diffuse probe, BRDF and sky.
Producers must use `SOURCE_GRAPH_TEXTURE_BINDINGS`, rather than extrapolating
`101 + 2 * slot` for the tenth slot. Missing, reordered, colliding or excess
bindings are refused. Required images cannot be supplied by padding.

This is wire and structural admission, not GPU readiness. The common material
extension currently reserves ten slots for every program, including historical
programs using fewer images. This increases its declared GPU resource budget;
the unchanged old wire limit does not prove unchanged device compatibility.
The complete pipeline must account for view, mesh and material groups, including
unused declarations, and be admitted against the actual device limits.
Native creation is not browser qualification; a WASM build alone is also not.

Keep the original compressed bytes, mip order, sRGB state, importer metadata
and effective sampler settings. Readability overrides belong only to isolated
derivatives and must be recorded, restored and compared with the source.
Sampling probes qualify only their recorded coordinates and mip levels.
Normal packing remains unknown until qualified; do not infer it from filenames
or assign a default decoder. The existing eight decoder uniform components and
their numeric values remain unchanged.

Local validation on 2026-10-09: 25 wire-contract tests passed. Engine binding
tests, actual modified-material GPU layouts, source/Forge pixel comparison and
Studio usage remain separate validation steps.
