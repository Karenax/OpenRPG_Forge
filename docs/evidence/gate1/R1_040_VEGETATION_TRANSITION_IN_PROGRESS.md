# R1-040 — Transition végétale ECS et cycle natif

2026-09-06, Rust, arbre hérité conservé, aucune publication. Gate 1/R1-040 ouverts ; CI NOT RUN.

## Implémenté

`VegetationQualityRequest::request(tier)` modifie uniquement le niveau demandé de la politique
installée avant construction du plugin. Le système exclusif Update prépare toutes les géométries
avant de toucher les bindings. Même niveau : pas de préparation ni copie de la politique par frame.
Politique/source conservées dans une ressource privée de projection, pas une nouvelle source canonique.
Le remplacement de documents en cours de jeu n'est pas couvert par cette API de qualité.

Clés typées placement-ID/LOD ou chunk/LOD. Entités survivantes réutilisées ; décoration retirée
despawnée, décoration ajoutée créée. Meshes remplacés en place aux mêmes handles, AABB rafraîchies.
Les handles de chunks temporairement absents restent retenus (ensemble borné par la zone), pour
réutilisation lors d'un retour. Les entités décoratives retirées puis réintroduites ne conservent
pas leur Entity Bevy ; leurs identités logiques restent déterministes. Aucun changement physique.
Matériau et interaction conservés, pas de remise à zéro de la météo ou de l'horloge par ce système.

Préparation invalide : aucune mutation de géométrie ou d'entités ; niveau demandé distinct du
niveau projeté et avertissement structuré. Binding mesh/entité manquant : refus avant mutation.
Le statut décrit uniquement la projection CPU, pas le téléchargement GPU ou un preset global.

## Preuve ECS exécutée

WorldSketch rev3 réel, cycle High → Low → Medium → Ultra → High :

- 636 arbres protégés, leurs 1272 entités near/medium conservent leur identité et existence ;
- tous les handles de meshes initiaux restent identiques ;
- hashes terrain/végétation identiques et compteurs High retrouvés au retour ;
- matériau, paramètres d'humidité et champ d'interaction conservés ;
- profil Low rendu incompatible : projection High et map d'entités précédentes conservées,
  requested=Low/projected=High et statut explicite de refus.

Test ciblé PASS. Clippy workspace all-targets/all-features `--locked -- -D warnings` PASS.
`cargo test --workspace --locked -q` : **377 PASS**, aucun échec.
Check moteur `--no-default-features --features web --target wasm32-unknown-unknown --locked` : PASS.
Target isolé habituel, incremental=0, `RUSTFLAGS=-C debuginfo=0`. Formatage et diff vérifiés.

## À vérifier avant qualification

Cycle natif capturé, upload GPU, rendu pendant/après changement et coût de reconstruction.
Les captures Low/Ultra du lot initial restent historiques : elles ne prouvent pas ces transitions.
La reconstruction est synchrone et peut créer un hitch ; aucun temps de transition garanti.
Pas de selector Studio, contrôleur adaptatif global, preuve physique dynamique, test visuel WASM
ou validation humaine. Les sources et la physique ne sont pas régénérées par ce système.
L'API ne prétend pas conserver chaque brin lorsque le budget change.

## Suite native exécutée — 2026-09-06 00:12 UTC

Le cycle natif est désormais exécuté : les réserves de la section précédente sur son absence
sont historiques. Le coût de transition, la continuité en mouvement et Studio restent ouverts.
Le harness existant `environment_cycle_proof.rs` accepte maintenant deux domaines exclusifs,
sans dupliquer son automate. CLI : `--vegetation-quality high --vegetation-cycle-proof <dossier-neuf>`.
Refus des automatisations concurrentes, niveau initial absent et doublons testés ; sortie avant
les cinq readbacks impossible à déclarer réussie. Événements communs `quality_cycle_*`, champ
`domain`, schéma `forge.runtime-event` v1. L'ancien flag environnement reste accepté.

Exécution WorldSketch rev3, kit/aperçu `human_bridge-clear` identiques au lot initial, météo clear,
RX 7800 XT, Vulkan, 1920×1080. Binaire SHA-256 :
`62dd6e4a8a73de6fe18202db96e38ef528042aac2988156796bd831bf1269380`.
Aucune compilation pendant le cycle. Sortie 0, runtime_errors_observed=false, zéro ERROR JSON.
Après ce raccord : **378 tests workspace PASS**, Clippy workspace all-targets/all-features PASS.
Le check moteur WASM du lot ECS reste applicable : aucun code moteur changé dans ce lot harness natif.
Les cinq étapes attendent 180 observations CPU concordantes, puis leur propre readback/fichier.
Cela ne démontre pas que tous les pipelines futurs sont préchauffés.

| Étape | Niveau | Placements / entités observés dans le PNG |
| --- | --- | --- |
| 0 | High | 7433 / 3666 |
| 1 | Low | 4043 / 2547 |
| 2 | Medium | 5689 / 3071 |
| 3 | Ultra | 9110 / 4212 |
| 4 | High | 7433 / 3666 |

Les cinq PNG sont inspectés : densité/distances changent, retour High cohérent, terrain/pont/eau
présents. Vent/eau à des phases différentes, donc pas d'identité pixel. Aspect encore stylisé,
tramage visible et eau granuleuse ; pas d'acceptation artistique humaine. Les FPS ponctuels de
l'overlay ne sont ni moyenne/p95/1% low, ni mesure du hitch de reconstruction : aucun benchmark revendiqué.
Hashes terrain/végétation identiques dans les cinq événements de capture, tels qu'épinglés au lot initial.

Dossier local : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-cycle-20260906`, log adjacent.
SHA-256 :

- harness : `410e60e7d3245745b066b2bfd4eaa6e6ff1ce10cd07da6852d147080ca13de4e` ;
- log : `ce484213862bb8b6bb0d1d260bfaa1ebaecd9c8170d534212edf95e2c4a55e6c` ;
- stage-0.png : `95124a2a6bb3053c936ff3b230bd2eb540ef130c877fb0d45c47740c3b6feff0` ;
- stage-1.png : `f5506492a478e3fa5c0c1ea2a34e3729d305d819278b501d0232498be0b214a1` ;
- stage-2.png : `f8d57c7a09cb0f4172b81250eaebdfea6a735b7ffbd6fad6a02a9e939cd3e9bf` ;
- stage-3.png : `525feae5cfc00bd1fc754165d2140ebf66b6468493d4ae5c4d4f772034b54f1f` ;
- stage-4.png : `e58c7cfcf4618833e5a3100c8760179d556cce7b5d2c06aa628e59d9d492826d`.
