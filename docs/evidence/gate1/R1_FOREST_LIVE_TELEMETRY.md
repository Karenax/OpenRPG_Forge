# Comptages réels de végétation — 7 septembre 2026

Gate 1 reste ouverte. Cette preuve porte sur les bindings mesh et la visibilité CPU,
pas sur les draw calls GPU, l'occlusion GPU, la performance ou la qualité artistique.

## Implémenté

Le relevé joint les marqueurs de végétation aux composants `Mesh3d` effectivement
présents, y compris les primitives du kit. Les placeholders procéduraux dont le mesh
a été retiré sont exclus. Les assets partagés sont comptés une fois ; le total des
instances comprend tous les LOD coexistants. Un mesh absent ou non triangulaire
incrémente un compteur d'incomplétude. Sans caméra unique, le relevé reste absent.
La catégorie historique `hlod` inclut les primitives LOD2 : elle ne prouve pas un HLOD
agrégé par chunk. Les anciens budgets procéduraux restent explicitement invalides.

Studio publie ces observations au maximum quatre fois par seconde dans son diagnostic
JSON et dans « Végétation · observation CPU ». L'export n'est pas atomique avec le rendu.

## Vérifié

Trois nouveaux tests couvrent partage d'assets, données non comptables et jointure ECS
avec exclusion des placeholders, caméra unique/absente/multiple. Passage workspace :
570 tests PASS, un timing préexistant ignoré. Clippy strict natif/Web et build Web PASS.
Le premier Clippy échoué sur un import wildcard est conservé ; corrigé avant le second
passage. Le check sans physique passe avec cinq avertissements hors de ce lot, pas un
verdict strict. L'export public du marqueur sans physique a été corrigé après le build
Web ; aucun comportement du bundle avec physique n'est modifié par ce dernier export.

Cycle réel Chrome sur le candidat compilé, caméra fixe `water_detail` :

| Observation | High initial | Low | High restauré |
|---|---:|---:|---:|
| Entités mesh | 10026 | 8907 | 10026 |
| Assets mesh uniques | 142 | 142 | 142 |
| Triangles uniques | 1907294 | 284774 | 1907294 |
| Triangles de toutes les instances/LOD | 30468104 | 28729200 | 30468104 |
| Triangles visibles CPU | 811374 | 370564 | 811374 |
| Meshes non comptables | 0 | 0 | 0 |

Sources constantes, 636 arbres/7632 primitives, sommes des catégories de visibilité
cohérentes. Blocker `vegetation` conservé et `gpu_ready=false` aux trois étapes.
Un clic d'ouverture du panneau par son rôle d'accessibilité a échoué (aucun match) ;
les lectures du ledger et exports par bouton ont réussi. Pas de nouvelle capture ou
qualification esthétique déduite de ces lectures.

## Artefacts locaux

Racine externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907`.
Script `Collect-Forest-Telemetry.ps1`, exécuté avec succès, vérifie profils, sommes,
sources, restauration exacte des compteurs et hashes des copies archivées.
Dans `forest-telemetry-cycle/` : diagnostics `1788777077985`, `1788777102454`,
`1788777138558` (préfixe `forge-diagnostic-`, extension `.json`).
Logs : `forest-telemetry-workspace.log`, `forest-telemetry-clippy-v2.log`,
`forest-telemetry-wasm.log`, `forest-telemetry-build.log`.
Bundle `web-forest-telemetry`, SHA-256 WASM :
`A09DFDF400D9CFE5B3E5BB73B1194036EF348FAC339DAE9ED8C39E8A2D1F2606`.

Suite : résoudre/qualifier la représentation lointaine de la forêt. L'inspection
[du chemin lointain](R1_FOREST_FAR_PATH_DIAGNOSIS.md) établit le refus de capacité
inconditionnel actuel et les données par arbre à préserver avant regroupement.
Aucun gain FPS,
passage de gate, validation humaine ou publication Git revendiqué.
