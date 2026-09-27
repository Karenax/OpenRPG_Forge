# Projection canonique des plantes dans le canvas Studio

Date : 2026-09-27. Qualification native ciblee ; aucune parite finale revendiquee.

Le moteur prepare desormais les streams vegetaux a partir d'une population
deja verifiee et de la tete de zone, sans fabriquer une archive JSON intermediaire.
Les IDs canoniques restent associes aux records dans l'ordre source, y compris
apres les filtres auteur et la correction de hauteur. Chaque mesh projete porte
un composant ImportedPlant avec l'ID et le hash de tete : ce composant ne
constitue pas encore un backend de picking.

La scene peut etre preparee depuis le paquet vegetal portable et la population
du workspace. Le chemin historique des probes natifs utilise la meme preparation,
en conservant son lecteur de fixture pour les comparaisons de non-regression.

Studio installe cette projection dans le canvas du terrain importe. Elle attend
la disponibilite de la population, des textures, de l'environnement et du terrain
acquitte ; la tete effective doit correspondre au terrain affiche. Un changement
de source retire les anciennes entites. La navigation recalcule la selection
near/far ; les changements d'ombres mettent a jour les materiaux existants.

## Etat des verifications

- Clippy moteur et probes strict --no-deps : PASS (37,45 s).
- Build natif : PASS (9 min 26 s).
- Probe ECS avec paquet portable reel : PASS. Les 65 383 identites correspondent
  aux records ; tous les meshes des deux selections camera portent un ID de
  cette population et le hash de tete. Le retour a la premiere camera retrouve
  la meme sequence. Une hausse auteur de 1 m deplace les 65 383 plantes et change
  les streams/hash de tete, tout en conservant exactement les identifiants.
  Etat canonique du host inchange avant/apres.
- Studio WASM compile : PASS (9 min 55 s, attente du build natif incluse).
- Clippy Studio strict : seul diagnostic preexistant atelier_ui.rs:122,
  bind_main 103 lignes pour une limite de 100. Aucun strict PASS global.
- Capture native inspectee et deux sorties d'ombres identiques a
  native-plant-textures-001. Cette execution utilise le fixture et le workspace
  canonique ; elle qualifie une non-regression Forge fixe, pas le canvas Studio.
- Aucun parcours GUI ou acquittement GPU Studio observe.

Preuves locales sous E:/OpenRPG_Forge/.tidewater-integration :
population-scene-proof-001.json, population-scene-run-001.log,
population-scene-clippy-001.log, population-scene-build-001.log,
population-scene-wasm-001.log, population-scene-studio-clippy-001.log,
native-population-scene-001.log et ses sorties :

- PNG : 243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
- Ombres relief : a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- Profondeurs objets : 0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.

Tous les processus de cette verification sont termines.

## Limites explicites

Cette premiere projection recree ses entites et ressources de geometrie lors
d'un changement de position camera. Reutilisation des buffers/meshes et mesure
des performances interactives restent requises. Les IDs ne donnent pas encore
la selection, les colliders, la promotion ou la retouche individuelle.
Les ombres recues sont raccordees ; les plantes ne sont pas encore ajoutees
aux casters dynamiques Studio. L'herbe, l'animation continue, les corrections
route/riviere et les autres effets de la carte restent ouverts.
Aucun nouveau frontend distribue, aucun commit ni push.
