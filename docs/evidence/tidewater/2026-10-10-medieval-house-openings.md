# Medieval balcony consoles clear of ground floor openings

The merchant and inn assemblies placed `Prop_Support` at the centres of wall bays. The diagonal consoles crossed ground floor window frames and door glazing. Repair 020 moves their attachment points to structural wall seams on all five saved instances (three merchant houses, two inns).

## Content change

The merchant anchors move from local X `[-1, 1]` to `[-2, 0, 2]` metres; the inn anchors move from `[-2, 0, 2]` to `[-3, -1, 1, 3]` metres. One additional source console per variant retains corner and intermediate balcony support. Each console has 16 triangles. The closest lateral gap to a complete window frame is 96.6 mm.

The source piece is from Quaternius Medieval Village MegaKit Standard, CC0-1.0. Only indexed console triangles in one wood primitive change. Embedded textures, UVs, normals, materials, nodes and all other drawn triangles are preserved. Shadow/collision input payloads are regenerated from the resulting indexed geometry.

The saved map is updated with the generic `FileWorkspaceSession` preview/apply zone edit commands. Only five `ManualPlacement.asset_hash` values change. Placement IDs, transforms, names, terrain, vegetation and `Removed` tombstones remain unchanged. The 15 surviving houses retain grounded foundations. Existing roof and scene-lighting corrections are preserved.

## Local evidence

Evidence and reversible backup are under `E:/OpenRPG_Forge/.tidewater-integration/house-openings-20261010-020`.

- `model-derivatives.json` and `geometry-validation.json`: two valid GLBs; zero old console triangles drawn; original non-console triangles and embedded image bytes unchanged.
- `source-piece-audit.json`: admitted source pieces and file SHA-256 inventory.
- `map-repair-receipt.json`: reopened canonical transaction with exactly five asset pin changes.
- `invalid-pin-proof.json`: stale input pin rejected before any workspace write.
- `ground-audit.json`: exact foundation vertices compared with the canonical raster; 15 houses remain grounded.
- `solar-proof-front-merchant-{before,after}` and `solar-proof-front-inn-{before,after}`: matching native cameras at 12:00; 15 assets loaded, colliders registered, 97 scene materials, completed solar readback. Both before/after pairs were visually reviewed by the assistant.
- `walk-proof-001`: native stationary yaw regression, 82 captures and 171 moving frames; all render samples ready. This is not a full walking traversal.
- `promotion.json`: installed package manifest verification of all 8,189 entries; backup intact, runtime unchanged.

The installed runtime SHA-256 remains `52f8fe35b04b9df90409648264bcb73b0882fe95946b2249791267050c3c0531` from repair 019. No engine source or runtime rebuild is part of repair 020.

Canonical area: `e620dd19-ead9-5913-a6d6-0f93109148c0`. Head before: `7d6ef1ab75a9b3af37fbc53ee75435f2d35344e3d562aedd1891570639f25304`. Head after: `8c40c05fe5dcb02fa7bab00317414c6cdd0b7edce8c7e14f62ce1afa0718eaf6`.

`Restore-Previous-Houses.ps1` restores the prior models, catalogue and canonical map head together, refusing an active installed client or later file edits. It is supplied but was not executed. Immutable added map objects remain unreferenced after restoration.

These are local checks on AMD RX7800XT/Vulkan and assistant visual review. User visual acceptance is pending. Legitimate projecting balcony consoles can still occlude part of a facade from oblique viewing angles; their attachment points no longer occupy the ground floor openings. No Asset Vault publication, Git push, full-map qualification or Gate 3 closure is claimed.
