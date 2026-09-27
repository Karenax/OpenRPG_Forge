# R1-040 — coût CPU de l'application végétale isolé

11 septembre 2026. Diagnostic natif sans fenêtre ; **pas un PASS temporel Studio**.

La sonde existante `quality_preparation_cpu_probe` ne mesure que la génération
et la préparation. Une sonde explicite supplémentaire `quality_commit_cpu_probe`
exerce séparément le vrai `reconcile` des entités et assets Bevy, après création
de la projection High. Elle vérifie le niveau réellement publié et la conservation
des hashes terrain/végétation pour Low, Medium, Ultra puis High.

La sonde est ignorée par défaut car elle ne contient aucun seuil de performance
universel. Elle a été exécutée explicitement, pas présentée comme un test ignoré
qui serait réussi. Aucun chemin de production n'a changé : module `cfg(test)` seul.
Le renderer, les profils auteurs et les cartes restent intacts.

Commande : `cargo test -p forge_engine --lib quality_commit_cpu_probe --locked
--offline -- --ignored --nocapture`. Sortie 0, un test PASS, 298 filtrés,
3,73 secondes d'exécution après 1 min 33 s de compilation locale.
Clippy moteur/tests strict PASS (19,14 s), fmt et diff-check PASS. Pas de build
produit ni de nouveau smoke graphique requis pour ce module de diagnostic seul.

| Profil | Préparation CPU ms | Application CPU ms | Meshes préparés | Entités |
| --- | ---: | ---: | ---: | ---: |
| Low | 317,367 | 15,963 | 132 | 4613 |
| Medium | 537,937 | 14,356 | 132 | 5145 |
| Ultra | 870,606 | 15,868 | 132 | 6304 |
| High | 642,706 | 17,254 | 132 | 5748 |

Relevé conservé :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-commit-cpu-probe-20260911/measurement.json`.
HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, sources non committées.

## Conséquence pour la suite

La préparation synchrone domine cette sonde. Son découpage Web déjà livré reste
pertinent. L'application seule n'est pas gratuite, mais ces 14–17 ms natifs ne
prouvent ni un blocage de plusieurs secondes dans le Studio, ni une durée Web
identique. Ne pas lancer un gros refactoring de commit sur cette seule mesure.

Limites explicites : un seul cycle, préparation synchrone de diagnostic (pas le
scheduler Web), végétation procédurale (pas la projection de la forêt gouvernée),
aucune extraction GPU, aucun upload, aucune présentation de frame. Ni moyenne
FPS, ni p95 de jeu, ni GPU attribués à ces durées. Aucun gain revendiqué.

Prochaine preuve utile : essai temporel intégré Web avec les profils et la forêt
gouvernée, dont le coût par étape permettra de choisir une correction. Ne pas
répéter cette sonde native inchangée comme substitut. R1-040 et Gate 1 restent
ouverts ; les revues humaines et dérogations existantes sont inchangées.
