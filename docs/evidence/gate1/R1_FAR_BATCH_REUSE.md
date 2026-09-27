# Réutilisation de la géométrie lointaine — 7 septembre 2026

Implémentation locale, mode regroupé toujours expérimental. Gate 1 ouverte.

## Changement

Le projecteur ne détruit plus les lots à chaque changement de qualité si leurs
entrées géométriques sont identiques. Entités et handles de meshes sont conservés ;
distances de visibilité et AABB élargies selon le vent/interaction sont recalculées.
Les primitives individuelles LOD2 réintroduites par la réconciliation sont à
nouveau masquées, sans dupliquer la géométrie visible.

L'identité couvre les placements ordonnés, leurs transformations/tags, les groupes
chunk/matériau, la hauteur normalisée, l'origine et le contenu exact des meshes
sources. Bevy `Mesh::PartialEq` compare les sources, pas seulement leurs handles.
Un snapshot CPU par mesh source unique est conservé ; aucun snapshot des meshes
fusionnés n'est ajouté. Coût mémoire et comparaison non nuls, pas de cache gratuit.
Une source changée en place ou une hauteur modifiée invalide la réutilisation à
la prochaine projection. Ceci ne raccorde pas un hot reload automatique.

Une entrée changée suit le chemin de préparation complet avant suppression des
anciens lots. Le chemin de restauration procédurale existant reste en place.

## Contrôles exécutés

Six tests ciblés initiaux PASS, dont deux nouveaux : stabilité High → Low → High
avec changement des ranges, puis invalidation d'un mesh modifié en place et d'une
hauteur changée (anciens handles et entités supprimés). Les tests existants de
préparation géométrique et de récupération sur échec passent aussi.
Un helper de ranges commun a ensuite été extrait. Validation finale : 578 tests
workspace PASS / 1 timing ignoré, Clippy strict natif/Web PASS. Le premier Clippy
refusait une comparaison flottante d'origine ; elle compare désormais les bits
exacts, sans tolérance ni suppression de lint. Premier log d'échec conservé sous
`far-reuse-clippy-first-error.log`.

Comparaison CPU du projecteur sur la même forêt synthétique (636 arbres), même
binaire/profils et ordre alterné, test dédié PASS. La reconstruction est forcée
en oubliant uniquement le snapshot d'entrées, par un helper compilé pour les tests.
La scène, les meshes sources et les paramètres ne sont pas remplacés entre chemins.
La préparation des profils en amont et la présentation GPU ne sont pas chronométrées.

| Chemin | Quatre durées CPU en ms |
|---|---|
| Reconstruction | 32,9202 ; 34,4070 ; 48,0862 ; 29,8893 |
| Réutilisation | 4,7484 ; 7,9511 ; 5,3844 ; 6,7953 |

Ce sont des mesures synthétiques de cette phase seulement : ni FPS, ni p95 du jeu,
ni absence de stutter, ni performance du kit réel. Aucune assertion de timing fragile
n'est ajoutée aux tests. Chaque échantillon vérifie aussi les identités conservées
ou reconstruites selon le chemin exercé.

## Preuves locales et reste à faire

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907` :
`far-reuse-check.log`, `far-reuse-tests.log`, `far-reuse-cpu.log`.
Logs finaux `far-reuse-workspace.log`, `far-reuse-clippy.log`, `far-reuse-wasm.log`.
Build Web PASS, WASM SHA-256
`37BADC41BFD32CBED2D018406A7EA1C79715585659EA6CBDC964A0B61121B3AA`.
Cycle Chrome High → Low → High avec kit réel exécuté, caméra `human_bridge`,
temps maintenu à 0,250 s après un pas d'initialisation physique : kit 20/20,
36 colliders prêts dans les trois diagnostics. Six compteurs live High restaurés
exactement, hashes de sources constants et aucune erreur navigateur observée.
Les deux PNG High avant/après sont ouverts et inspectés : forêt/terrain/pont présents,
pas de saut de caméra ni disparition globale. L'esthétique n'est pas qualifiée.

| Mesure CPU observée | High initial/final | Low |
|---|---:|---:|
| Entités mesh | 7 738 | 6 619 |
| Meshes uniques | 394 | 394 |
| Triangles uniques | 2 549 914 | 927 394 |
| Triangles instanciés tous LOD | 30 468 104 | 28 729 200 |
| Triangles visibles CPU | 1 200 846 | 607 004 |
| Meshes non comptables | 0 | 0 |

`Collect-Far-Reuse.ps1` PASS ; artefacts sous `far-reuse-cycle/` : diagnostics
1788783906427 / 1788783947415 / 1788783981649 et PNG 1788783916711 / 1788783997906
(préfixes `forge-diagnostic-` / `forge-viewport-`). Le cache est prouvé par les tests
d'identité ECS ; ces compteurs Web seuls ne prouvent pas les identités GPU ni
une durée de transition. Les logs info navigateur consultés n'exposent pas l'événement
de réutilisation ; aucun événement Web de ce type n'est revendiqué.
Pas de nouveau benchmark natif général, pas de qualification du vent par ce lot,
pas de publication Git.
