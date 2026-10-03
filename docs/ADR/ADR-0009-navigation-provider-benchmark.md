# ADR-0009 — Benchmark du fournisseur de navigation

Date : 2026-09-01  
Statut : **accepté le 2 octobre 2026 ; intégration et qualification produit en cours**
Portée : branche `Rust`, Visual Valley, Bevy 0.19, runtime natif et cible WebAssembly  
Complète : `ADR-0001-rust-webgpu-greenfield.md` et
`ADR-0008-world-sketch-image-to-world.md`

## Acceptation propriétaire du 2 octobre 2026

Le propriétaire a répondu explicitement « Accepter et poursuivre l’intégration
(recommandé) » au dossier NAV019 et à sa proposition concrète. La décision
sélectionne Rerecast `=0.4.0` direct avec un seul fork local testé, Landmass
`=0.9.2` direct et une conversion/adaptation Forge minimale, alimentée par
les formes préparées du Rapier existant. Les wrappers Bevy Rerecast/Landmass
et leurs forks de loaders restent au laboratoire. L'entretien du fork est
accepté dans le projet OpenRPG Forge ; aucun envoi de PR upstream n'est autorisé.

Les cinq conditions machine de la décision initiale passent dans le périmètre
LAB décrit dans le [dossier NAV019](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-019/decision-packet.md).
Archive source `1e69a94372b810802aa2e7bd674e92f699d3358157831fe66a47997b31c005e2` ;
inventaire `ca65d0f9aea042bfa98c3efff73eba112d4530b4d0c0c5de28ebe57587a4830d`.
Les sources, rapports et hashes exécutés sont conservés. Les tests ont remplacé
les exécutables natifs mesurés dans le cache ; ils ne sont pas archivés et
tout nouveau replay nécessite reconstruction et revalidation. Cette limite
était incluse dans la question acceptée.

La [preuve d'acceptation](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-owner-acceptance-001/proof.json)
autorise l'intégration isolée puis la qualification des PNJ réels sur Tidewater
courant : géométrie/colliders de même révision, profil produit exact, tuiles,
obstacles/portes, absence de chemin, natif et navigateur réel. Aucun PASS produit,
Gate 3 ou revue humaine de partie n'est déduit de la sélection. Les constats
du benchmark initial ci-dessous restent datés et conservés.

## Contexte

Le contrat Forge exige une navigation extérieure et intérieure, des portes et obstacles dynamiques,
plusieurs tailles d'agents, des coûts de zones, des liens hors maillage, de l'évitement local, une
visualisation de debug, une génération asynchrone bornée, des tuiles et une reconstruction partielle.
Le terrain, l'eau bloquée, le croisement route/rivière et le pont de Visual Valley possèdent déjà des
documents canoniques et des collisions Rapier vérifiées. Aucun fournisseur ne doit être introduit
dans `forge_engine` ou `forge_runtime` avant une preuve sur ces données réelles.

## Candidats examinés

| Candidat | Atouts vérifiés | Écart décisif pour Forge | Décision |
|---|---|---|---|
| Rerecast 0.4 + Bevy Rerecast 0.5 + Landmass 0.9.2 | Bevy 0.19, génération polygon/detail, tâche Bevy asynchrone, debug, cheminement, coûts, liens, steering et évitement local ; compilation native et WASM | pas de génération native par tuiles, pas de reconstruction partielle ni de carving d'obstacle dynamique, backend physique maintenu uniquement pour Avian, sortie globale rejetée par Landmass, advisory bloquante | **pile préférée pour investigation, non sélectionnée** |
| Rerecast + `vleue_navigator` 0.16 | Bevy 0.19, WebAssembly, couches et mise à jour de navmesh | le steering reste déclaré à faire ; ne couvre donc pas le contrat d'évitement local | écarté comme pile complète |
| `oxidized_navigation` | historique Bevy/Rapier utile | dépôt déclaré déprécié et Bevy 0.19 non supporté | écarté |
| Recast/Detour natif C++ | tuiles, tile cache, crowd et liens hors maillage éprouvés | FFI, toolchain native supplémentaire, sérialisation et voie WebAssembly distincte ; augmente fortement la surface d'intégration | repli de recherche seulement |

Sources amont consultées le 1er septembre 2026 :

- [Rerecast — fonctions, backends et roadmap](https://github.com/janhohenheim/rerecast) ;
- [Landmass — navigation, steering et intégration Rerecast](https://github.com/andriyDev/landmass) ;
- [`vleue_navigator`](https://github.com/vleue/vleue_navigator) ;
- [`oxidized_navigation`](https://github.com/FrankenApps/oxidized_navigation) ;
- [Recast Navigation C++](https://github.com/recastnavigation/recastnavigation).

## Benchmark reproductible

L'outil isolé `forge_navigation_benchmark` consomme le `WorldSketch` réel. Il recompile le terrain
sculpté, retire les triangles couverts par l'eau bloquée, ajoute le tablier issu du contrat de pont et
appelle les étapes publiques de Rerecast. Aucun pathfinder Forge maison n'est introduit.

Les probes couvrent :

- crop extérieur de 96 m avec et sans pont ;
- vallée complète de 512 × 512 m ;
- intérieur avec porte ouverte, fermée puis réouverte ;
- passage de 1,2 m pour agents de rayons 0,25 m et 0,65 m ;
- lien hors maillage ;
- détour par coût de zone ;
- croisement de deux agents avec évitement local ;
- trois générations et hashes identiques pour les scènes extérieures ;
- compilation Windows et `wasm32-unknown-unknown`.

Rapport de référence hors Git :

```text
E:\OpenRPG_Forge\.gate1-evidence-runtime\navigation-benchmark-r1-068-isolated-final-20260901-190000\navigation-provider-benchmark.v1.json
SHA-256 : 208ab622a9bab445135c528a9f6356afd1a8b0b29605f4e10cb0ab8c8dd3bf19
```

## Résultats

| Probe | Résultat |
|---|---:|
| crop 96 m, 3 910 triangles source | 3 hashes identiques, chemin par le pont trouvé |
| contrôle négatif sans pont | aucun chemin |
| vallée 512 m, 74 337 triangles source | 3 hashes identiques ; 2 834 polygones, 17 203 triangles de détail |
| conversion globale brute avec height mesh | **rejetée**, triangle du polygone 2 819 horaire |
| conversion globale brute sans height mesh | **rejetée**, polygone 2 819 concave ou horaire |
| porte ouverte / fermée / réouverte | chemin oui / non / oui, hash réouvert identique |
| petits / grands agents | chemin oui / non |
| lien hors maillage | aucun chemin avant, chemin avec un lien après |
| coût de zone | détour peu coûteux choisi |
| évitement local | deux cibles atteintes, séparation minimale 0,7000608 m |

Une copie diagnostique inverse les éléments dont le winding échoue selon les mêmes opérations de
coordonnées que Landmass. Elle doit modifier **1 polygone et 18 triangles de hauteur** pour rendre le
chemin global calculable. Cette normalisation reste marquée `winding_normalization_accepted=false` :
elle localise le défaut et prouve qu'un adapter borné est envisageable, mais ne transforme pas une
sortie fournisseur invalide en qualification de production.

Les trois générations globales prennent 5,216 s, 5,528 s et 4,947 s sur la machine de preuve. Cette
mesure n'est pas une qualification de frame time : la pile Bevy prévoit une génération en tâche de
fond, mais le benchmark synchrone mesure volontairement un début et une fin explicites.

## Gouvernance des dépendances

Les licences de la pile passent la politique `MIT OR Apache-2.0`. `cargo deny check` refuse toutefois
la pile : `bevy_rerecast_core 0.5.0` dépend de `bincode 2.0.1`, déclaré non maintenu par
`RUSTSEC-2025-0141`. Cet advisory n'est pas ajouté aux exceptions du dépôt tant que le fournisseur
n'est pas sélectionné.

La dépendance transitive `getrandom 0.3.4` requise par l'évitement local active `wasm_js` uniquement
pour la cible WebAssembly du benchmark. Aucun `rustflags` global n'est ajouté.

## Décision initiale du 1er septembre 2026

Le benchmark est **terminé**, mais aucun fournisseur de production n'est sélectionné. Rerecast +
Landmass reste la pile préférée à réévaluer, pas une dépendance du runtime.

L'intégration est bloquée jusqu'à obtention de toutes les preuves suivantes :

1. sortie globale valide sans fallback silencieux, idéalement corrigée et testée en amont ;
2. stratégie fournisseur de tuiles et reconstruction partielle mesurée sur la vallée ;
3. backend Rapier maintenu ou adapter Forge alimenté par le même document de collision canonique ;
4. résolution ou décision de gouvernance explicite pour `RUSTSEC-2025-0141` ;
5. probe d'obstacle dynamique et exécution navigateur réelle, au-delà de la compilation WASM ;
6. nouvelle exécution du benchmark et acceptation explicite de l'ADR.

## Conséquences du verdict initial

- Les crates candidates restent cantonnées au laboratoire Cargo autonome
  `tools/forge_navigation_benchmark`, exclu du workspace et muni de son propre lockfile.
- `forge_engine` et `forge_runtime` ne dépendent pas de la pile candidate.
- Le document `forge.world-traversal-audit` reste un audit provider-neutral, pas un navmesh.
- Le pont Rapier déjà qualifié reste une preuve de collision et de franchissement, pas une preuve de
  navigation globale.
- Aucune carte jouable complète ou qualité AAA n'est revendiquée par ce benchmark.
