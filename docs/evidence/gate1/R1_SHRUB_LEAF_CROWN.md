# R1 — Couronne ajourée des arbustes

2026-09-06, Rust, Gate 1 ouverte. Correction du défaut de pyramides observé dans le Studio Web.

## Implémenté

Le mesh proche partagé des arbustes remplace trois cônes pleins par 48 feuilles en losange,
réparties sur quatre couronnes décalées avec variation déterministe. 192 sommets/96 triangles,
contre 63/21 auparavant. Rangée basse fixe, poids de vent croissant vers le haut.
Ni document, densité, ID, pose ou collider changé ; HLOD lointain inchangé. Aucun asset externe.
Ce coût géométrique supérieur est explicite : aucune optimisation ou accélération revendiquée.

## Vérification

433 tests workspace PASS, un probe timing ignoré ; Clippy strict workspace/all-targets/all-features,
build runtime et check moteur WASM PASS. Le premier test a refusé l'absence de poids de vent nul ;
la géométrie a été corrigée, pas l'assertion. Les logs d'échec restent conservés.

Smoke natif RX 7800 XT, 1920×1080, 420 frames, capture demandée à 300/reçue à 303,
`--world-sketch-visual-valley --player-camera --prototype-forest-preview`, kit candidat cohérent
révision 2 inchangé. Sortie 0, `runtime_errors_observed=false`, capture et métriques écrites.
La capture à hauteur joueur a été ouverte et inspectée : arbustes proches ajourés effectivement
visibles au lieu des cônes pleins. Leur structure reste régulière ; fougères anguleuses, terrain
uniforme et silhouettes d'arbres répétitives restent non qualifiés. Aucune acceptation humaine.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shrub-leaf-crown-20260906/`.

- `player.png` SHA-256 `e81f81074fbad49739c1f862f99e81e021aa4c3000bb5630510311689d454cda`.
- `tests-final.log` SHA-256 `4230d514e4738f72071d7c702722a80df2f37bf60c7a0be00ad78c6d7c46402e`.
- `runtime.log`, `metrics.json`, `clippy-final.log`, `build.log`, `wasm.log`.

Le HUD court ne prouve pas le budget GPU cible ni un avant/après contrôlé. Le bundle Studio Web
précédent n'a pas été reconstruit avec cette correction ; sa capture reste celle des cônes.
Transition proche/HLOD et vent en mouvement restent à qualifier. Aucun push ni passage de gate.
