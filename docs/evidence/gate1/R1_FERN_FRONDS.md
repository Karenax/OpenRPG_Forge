# R1 — Frondes de fougères découpées

2026-09-06, branche Rust, Gate 1 ouverte.

## Changement

Le mesh proche partagé conserve sept frondes radiales, désormais composées de deux segments
de tige fine et trois paires de folioles, au lieu des sept grands quads triangulaires.
224 sommets/112 triangles contre 28/14 ; coût supérieur explicite, aucune optimisation annoncée.
Poids de vent nul aux racines conservé. Aucun changement de document, ID, pose, scatter,
collider ou HLOD lointain. Aucun asset importé ni dépendance ajoutée.

## Preuves

433 tests workspace PASS, un probe timing ignoré ; Clippy strict workspace/all-targets/all-features,
build runtime et check moteur WASM PASS. Le test de payload contrôle sommets/triangles, attributs
finis, indices et poids de vent ; les scénarios existants de qualité/scatter restent verts.

Smoke natif 1920×1080, RX 7800 XT, 420 frames, capture demandée à 300, caméra joueur et kit
candidat cohérent révision 2 identiques au lot arbustes précédent. Sortie 0,
`runtime_errors_observed=false`, capture ouverte et inspectée. Les grands triangles au premier
plan ont disparu ; les frondes sont ajourées mais encore trop clairsemées pour une qualification
artistique. Correction intermédiaire, pas une fougère naturelle approuvée ni une gate passée.

Répertoire : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-fern-fronds-20260906/`.

- `player.png` SHA-256 `201058f49e37840340625fa2a8abd2d240dfac05a705e0d5a96409e1ce5ca9f0`.
- `tests.log` SHA-256 `002f0eb8e5c96a18498fe5aeb36d7892eef7b375675ac2cf82157eedb3f932da`.
- `clippy.log`, `build.log`, `wasm.log`, `runtime.log`, `metrics.json`.

Le HUD court n'est pas un benchmark avant/après ni la preuve du matériel cible. La transition
vers les anciennes représentations HLOD, le vent en mouvement et le rendu Web frais restent
à qualifier. Ne pas utiliser l'ancien bundle Web comme preuve de cette géométrie.
CI distante NOT RUN ; aucun push, aucune promotion d'asset ni ouverture de Gate 2.
