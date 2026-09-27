# R1-040 — répétition des payloads végétaux : piste écartée

11 septembre 2026. Diagnostic CPU uniquement, **aucune optimisation de production**.

Les pics Studio restants coïncident avec l'application d'un profil. Le chemin
`vegetation/quality/transition.rs::reconcile_inner` remplace chaque mesh préparé,
même quand ses données sont identiques. Avant d'ajouter un cache, la sonde
ignorée explicite `quality_commit_cpu_probe` a été enrichie d'un comptage exact
des attributs/sommets/indices identiques aux bindings vivants précédents.

Comparaison hors des chronomètres de préparation et commit. Elle ne constitue
pas une politique générale d'équivalence Mesh : morph targets, métadonnées et
assets gouvernés ne sont pas couverts. Aucun saut de mise à jour en production.

## Exécution

`cargo test -p forge_engine --all-features quality_commit_cpu_probe --locked
--offline -- --ignored --nocapture` : un test PASS, 321 filtrés, 3,60 s après
compilation de 1 min 37 s. App headless, sources compilées de WorldSketch actuel,
aucun renderer/navigateur. Les timings ne sont pas ceux du Web, de ses jobs
incrémentaux ou de la forêt gouvernée. Clippy strict forge_engine/all-targets/
all-features PASS (22,84 s), fmt et diff-check PASS.

| Transition vers | Meshes préparés | Payloads identiques | Sommets identiques | Sommets préparés | Préparation CPU ms | Commit CPU ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Low | 132 | 4 | 1485 | 1136965 | 300,586 | 15,636 |
| Medium | 132 | 4 | 1485 | 3302261 | 524,278 | 9,511 |
| Ultra | 132 | 4 | 1485 | 5142441 | 843,542 | 15,136 |
| High | 132 | 4 | 1485 | 4416729 | 630,835 | 17,400 |

Moins de **0,14 % des sommets préparés** sont identiques dans chacune de ces
transitions. Éviter les quatre remplacements correspondants ne constitue pas
une piste proportionnée aux pics de 147/271 ms. **Cache d'égalité végétale non
implémenté**, aucun gain GPU/FPS revendiqué. Le grand volume reconstruit est
observé ; sa contribution exacte aux délais GPU n'est pas encore mesurée.

## Suite et limites

Ne pas rejouer cette sonde inchangée ou ajouter un cache multiformat sur ce seul
constat. Le terrain remplace également trois LOD ensemble, mais son LOD0 contient
un morph target dépendant du LOD suivant : LOD0 ne peut pas être considéré
inchangé sur le seul compte des sommets. Cette piste nécessite une comparaison
des payloads complets, notamment des morph targets et des chunks de berge protégés,
avant de modifier le chemin de production.

Le seul fichier Rust modifié ici est le module de diagnostic de test. Les
executables natif/Studio qualifiés précédemment n'ont pas changé. Les anciens
manifests de sources incluant ce module ne sont pas réécrits ou prétendus
identiques au nouveau test ; leur acquisition historique conserve ses pins.
Pas de suite globale Rust annoncée rejouée, d'asset changé, de processus graphique,
de publication ou de passage Gate 2. Gate 1 reste ouverte.
