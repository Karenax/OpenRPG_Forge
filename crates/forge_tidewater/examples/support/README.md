# Shared verification harnesses

These modules belong to `forge_engine`. The named examples one directory above
remain the Cargo entry points, retaining their argument and output contracts.
This directory has no `main.rs` and is not another auto-discovered executable.

The 17 October consolidation merges only identical implementations (aside from
comments and blank lines), with baseline hashes in
`docs/evidence/architecture/EXAMPLE_CONSOLIDATION_20261017.json`.
Distinct HDR, sRGB, depth and lighting protocols retain distinct harnesses.

`source_material_layouts.rs` is the product-owned implementation of the layout
verification probe. Its historical acquisition candidate under `scripts/hivemind`
is retained as evidence, but no product target includes that candidate.

Compile checks do not prove GPU execution, source parity or Studio acceptance.
