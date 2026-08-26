# Benchmark qualité — génération de mondes OpenRPG Forge

## But

Ce benchmark empêche l'adoption d'un addon qui augmente la complexité sans améliorer le monde final.
Tous les fournisseurs sont testés sur la même carte, avec la même seed, le même catalogue d'assets,
les mêmes caméras et les mêmes budgets.

Les seuils ci-dessous sont des règles internes de décision. Ils ne décrivent pas des performances déjà
obtenues.

## Carte de référence v1

```yaml
id: openrpg-worldgen-quality-reference-v1
seed: 20260826
size_meters: [2000, 2000]
terrain: temperate_valley
river:
  orientation: north_south
  target_width_meters: 18
biomes:
  west: dense_temperate_forest
  east: meadow_and_farmland
settlements:
  - type: village
    building_count: 25
    position: near_river
  - type: castle
    position: northern_high_ground
landmarks:
  - type: mine
    position: mountain_foot
dungeon:
  room_count_range: [20, 30]
roads:
  required_links:
    - village_to_castle
    - village_to_mine
```

Chaque essai doit conserver les mêmes assets admissibles. Un candidat ne peut pas gagner en utilisant
un meilleur pack que les autres.

## Artefacts obligatoires

Chaque exécution écrit sous un dossier identifié par fournisseur, version, seed et fingerprint :

```text
artifacts/world-generation/qualification/<provider>/<version>/<run-id>/
```

Le dossier contient au minimum :

- `environment.json` : OS, Godot, .NET, GPU, CPU, mémoire ;
- `providers.json` : clés, versions, licences et empreintes ;
- `recipe.json` et ses fingerprints ;
- `execution-plan.json` ;
- `metrics.json` ;
- `violations.json` ;
- `manual-review.json` ;
- `scene-manifest.json` ;
- scène sauvegardée ou package dérivé ;
- captures normalisées ;
- logs du worker et du smoke runtime.

## Captures normalisées

Les caméras, exposition, heure, météo et résolution sont figées. Générer au minimum :

1. vue aérienne orthogonale ;
2. quatre vues cardinales ;
3. arrivée au village ;
4. place centrale ;
5. intersection routière principale ;
6. berge et pont ;
7. forêt proche ;
8. forêt distante et transitions de LOD ;
9. château depuis la vallée ;
10. entrée de mine ;
11. entrée et salle majeure du donjon ;
12. vue nocturne.

Aucune capture marketing manuelle ne remplace ces points de vue.

## Mesures fonctionnelles

- déterminisme de la recette, du plan et de la scène ;
- nombre de générations strictement reproductibles ;
- accessibilité des portes et entrées ;
- connexions du réseau routier ;
- passages de rivière déclarés ;
- reachability des lieux obligatoires ;
- collisions valides ;
- objets flottants ou enterrés ;
- chevauchements bâtiments, routes, eau et relief ;
- survie des nœuds verrouillés après régénération partielle ;
- sauvegarde, fermeture, réouverture et reconstruction.

## Mesures visuelles

Un panel ne connaissant pas le fournisseur note de 1 à 5 :

- crédibilité du relief ;
- continuité géologique ;
- qualité des berges ;
- cohérence des routes ;
- variété des silhouettes ;
- intégration des bâtiments au terrain ;
- densité et composition de la végétation ;
- répétition visible ;
- qualité des transitions de LOD ;
- éclairage et atmosphère ;
- lisibilité gameplay ;
- caractère mémorable des zones héroïques.

Conserver les notes brutes, la moyenne, la médiane et les commentaires. Mélanger l'ordre des captures
entre évaluateurs.

## Mesures de performance

Mesurer séparément :

- temps de compilation de la recette ;
- temps de chaque étape de génération ;
- temps de bake et de sauvegarde ;
- temps de chargement à froid et à chaud ;
- mémoire maximale ;
- taille des scènes et artefacts ;
- nombre de nœuds ;
- nombre d'instances MultiMesh ;
- triangles visibles proche, moyen et lointain ;
- draw calls ;
- frame time CPU et GPU sur parcours déterministe ;
- pics liés au streaming ;
- temps de bake navigation ;
- taille et temps des exports.

Les mesures sont répétées au moins trois fois après un warm-up documenté.

## Effort d'auteur

Mesurer le temps humain nécessaire pour :

- corriger les objets flottants ;
- rendre les portes accessibles ;
- corriger routes et intersections ;
- supprimer les répétitions évidentes ;
- finaliser la place centrale ;
- finaliser le château ;
- corriger les LOD ;
- obtenir une validation complète.

Consigner aussi le nombre d'actions manuelles et de régénérations. Une amélioration de génération qui
augmente fortement les retouches n'est pas un gain.

## Gates éliminatoires

Un candidat est rejeté s'il ne satisfait pas tous les points suivants :

- trois runs avec la même entrée donnent les mêmes fingerprints déterministes ;
- 100 % des entrées et lieux critiques sont accessibles ;
- aucune route ne traverse un bâtiment ;
- aucune traversée de rivière majeure n'existe sans pont, gué ou tunnel déclaré ;
- aucun objet flottant n'est accepté dans une zone héroïque ;
- moins de 0,5 % des props échantillonnés sont flottants ou enterrés dans les zones secondaires ;
- les retouches verrouillées survivent à une régénération partielle ;
- la scène se sauvegarde, se rouvre et se reconstruit ;
- aucune licence ou provenance n'est manquante ;
- les plateformes d'export requises passent ;
- aucune dépendance native non épinglée n'est téléchargée pendant le build.

## Gate d'amélioration

Par rapport au backend de référence, un candidat doit obtenir au moins l'un des gains suivants sans
franchir une gate éliminatoire :

- réduction d'au moins 25 % du temps de correction manuelle ;
- augmentation d'au moins 15 % du score visuel à l'aveugle.

Et simultanément :

- régression de frame time inférieure à 10 % à densité et qualité équivalentes ;
- temps de génération et taille d'artefact compatibles avec les budgets définis par le projet ;
- aucune perte de déterminisme, de gouvernance ou de reprise après redémarrage.

## Pondération indicative

Pour départager deux candidats ayant franchi les gates :

| Catégorie | Poids |
|---|---:|
| Qualité visuelle | 30 |
| Validité fonctionnelle | 25 |
| Effort d'auteur | 15 |
| Performance et streaming | 15 |
| Déterminisme et robustesse | 10 |
| Licence, maintenance et portabilité | 5 |

Une meilleure note totale ne contourne jamais une gate éliminatoire.

## Zones héroïques

Les zones suivantes exigent une revue manuelle ou assistée, puis un verrouillage :

- place centrale du village ;
- château ;
- entrée du donjon ;
- panorama principal ;
- zone de boss ;
- lieu central d'une quête.

Le procédural produit la base. Il ne constitue pas seul la définition de terminé pour ces zones.

## Rapport de décision

Le rapport final doit inclure :

- tableau comparatif avant/après ;
- captures côte à côte anonymisées ;
- violations et corrections ;
- effort d'auteur ;
- performances ;
- risques de maintenance ;
- statut de licence ;
- décision `adopt`, `experimental` ou `reject` ;
- justification et ADR associée.
