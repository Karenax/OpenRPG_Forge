# Selected object translation handles — 2026-10-10

Selecting an editable placement now displays the X/Y/Z arrows and the XY/XZ/YZ squares automatically in the authoring viewport. Dragging an arrow constrains movement to that world axis; dragging a square constrains movement to its two axes. The filled squares take precedence over nearby arrow shafts in a foreshortened view. Rotation and nonuniform scale are retained.

The gesture previews the existing scene root without changing the authoring document. Releasing it prepares the ordinary placement transform proposal. Apply accepts it; Save persists it. Escape and leaving the viewport restore the accepted pose. Locked or unavailable placements display a read-only repère. Existing explicit local-axis, rotation and scale controls use the same placement path. No behavior depends on the module or asset name.

The screen overlay uses the active scene camera and normalized viewport coordinates. It adds no scene lighting, sky, shadows or water animation. Thumbnail captures pause while a manipulation tool is active. Camera rays reject edge-on, backward and nonfinite plane intersections.

## Verification evidence

The isolated candidate is stored under `E:\OpenRPG_Forge\.module-navigation-20261010\candidate-003`. Its `source-inputs.json` pins the preceding installed source snapshot plus the nine-file `selection-handles-overlay.json`. `studio-build-proof.json` pins the executable built from those sources. The personal workspace and the user's running installed editor are excluded from runtime testing.

The four `forge_world::placement_axis` tests passed, including all three translation planes, orientation independent of scale, snapping and degenerate ray rejection. Native and WASM builds passed. The final source manifest is `22b6f73b61f0aceef96ae6864a9144374627d1dc078cbd0f81b38699b899e242`; the tested Studio executable is `6421725dc4dbcd8d7fa79539ea0f2e34bbca2066e652d14d13cac521f0563621`.

`handles-interaction-tests.json` and `handles-runtime-proof.json` record six successful drags, preview movement before release, unchanged document during the gesture, constrained accepted coordinates, preserved rotation/scale, Save and Undo/Save restoration. Escape and mouse-leave cancellation both restore the original pose without changing the document. The test uses state-based waits with numerical tolerance rather than assuming a fixed frame latency. All changes occur in the isolated test workspace; its original object pose is restored and its window closes cleanly.

`handles-selection-regression.json` records selection by canvas click, three automatic axes and three planes, agreement with the selected map card, and loaded object thumbnails. `handles-final-view.png` was inspected visually. These checks are local runtime evidence; they do not constitute human acceptance or release qualification.

The applications-only update `0.1.0-preview.20261010.5`, manifest `538b881b76b44651eb89465f57c8096382a391dad110b6eab84e7abd261cef96`, is installed in `F:\instal test\Installation` at installation revision 6. `candidate-003/post-install-verification.json` records PASS for all 1,022 core files. The installed module/assets references, product configuration and 1,014 preceding core-file hashes are retained. No personal workspace migration is part of this update. The user's running editor PID 27396 was preserved on the preceding executable; a normal editor restart loads the updated package.

## Git publication checks — 2026-10-11

The publication tree is prepared from Git base `5d0dc86795332a452f82eebb078e22a29219aab9`, with only the module/map navigation, selection/thumbnails, fast editing and selected-object handles changes plus their required dependencies and documentation. Concurrent primary files are preserved. The previously committed transport fixes are merged with the module-model envelope, and its fixed-size magic values follow the already tested native helpers. Two obsolete Studio items are removed: an unused ECS import and an unused preview convenience wrapper. The shared native asset dependency and two explicit Bevy ECS imports are included so the isolated tree compiles without unrelated working-copy files.

On this isolated publication tree, 22 focused native tests pass: four axis/plane math tests, six model-wire tests, nine module resolver tests and three workspace host/navigation tests. Native production-library checking, native Clippy with warnings denied, formatting and WebAssembly checking pass. WebAssembly checking retains the two preexisting engine unused-import warnings. This is scoped local validation; the installed executable remains identified by the earlier build and runtime pins above. Publication does not turn these results into a remote CI or gate verdict.
