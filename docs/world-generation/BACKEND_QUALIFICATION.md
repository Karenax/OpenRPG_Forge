# Qualification des backends de génération

## Principe : un propriétaire par responsabilité

La pile de production ne doit jamais contenir deux systèmes concurrents qui écrivent librement la
même couche. OpenRPG Forge reste propriétaire de la recette, des contraintes et de la gouvernance ; un
seul backend concret produit ensuite chaque type d'artefact.

| Responsabilité | Propriétaire cible | Statut |
|---|---|---|
| Terrain extérieur final | Terrain3D | confirmé, qualification 4.7.1 à compléter |
| Relief, hydrologie et masques | OpenRPG Terrain Compiler | à construire |
| Graphe PCG général | un seul fournisseur Graph PCG | benchmark requis |
| Routes structurées | Graph PCG actuel ou Road Generator | prototype comparatif |
| Topologie de donjon | OpenRPG DungeonAssembler | à construire |
| Habillage de donjon | fournisseur Graph PCG retenu | après choix |
| Scatter de production | fournisseur Graph PCG retenu, ou ProtonScatter si gain prouvé | benchmark requis |
| Eau visible | renderer OpenRPG initial | spécialisé plus tard si nécessaire |
| Navigation | Godot natif, validée sur la géométrie finale | qualification réelle requise |
| Gameplay et quêtes | contrats OpenRPG | confirmé |
| Promotion canonique | commande OpenRPG gouvernée | à construire |

## Matrice de décision

### Terrain3D — `adopted`

Pourquoi : terrain éditable, matériaux, LOD, trous, humidité, détilement et végétation massive. Il
améliore directement le terrain final et évite de développer un renderer de terrain complet.

Conditions restantes :

- test réel sous Godot 4.7.1 ;
- sauvegarde/réouverture ;
- export Windows et Linux ;
- contrôle des matériaux et des LOD sur la carte de référence.

### Godot Flow — `current-unqualified`

Pourquoi : fournisseur réellement épinglé aujourd'hui, graphes, splines, opérations spatiales,
subgraphs et spawn de scènes.

Risques : retour d'expérience limité sur un projet de production, graphes artistiques absents du
dépôt, dépendances natives à qualifier, streaming PCG non équivalent à un système runtime complet.

Action : servir de baseline du benchmark Graph PCG.

### GF-PCGODOT — `candidate`

Pourquoi : fork enrichi, plus de nœuds et fonctions proches du PCG d'Unreal.

Risques : migration et compatibilité des graphes, extensions natives par plateforme, génération
hiérarchique et asynchrone encore incomplètes, coût de maintenance d'un fork supplémentaire.

Action : l'installer dans une fixture isolée et comparer à Godot Flow. Ne pas l'ajouter en parallèle
au projet de production.

### ProtonScatter — `candidate-scatter-only`

Pourquoi : workflow non destructif, formes positives/négatives, scènes arbitraires, cache et usage
orienté scatter.

Risques : doublon avec le Graph PCG, modèle de données supplémentaire et incohérences possibles entre
végétation, contraintes de routes et établissements.

Action : benchmark limité à la végétation et aux petits props. Rejeter si le fournisseur Graph PCG
retenu atteint les mêmes objectifs.

### Godot Road Generator — `prototype-priority`

Pourquoi : continuité géométrique, intersections, largeur, voies, collisions et adaptation Terrain3D
sont difficiles à reproduire proprement avec un graphe générique.

Risques : projet encore pré-1.0, API script à éprouver, LOD et performances à mesurer, dépendance
supplémentaire.

Action : prototype derrière `RoadGeneratorAdapter`, sans modifier les recettes canoniques. Adopter
uniquement si les routes et le temps d'auteur s'améliorent selon le benchmark.

### SimpleDungeons — `concept-source`

Pourquoi : seed, pièces préfabriquées, grille, connecteurs, A*, étages et corridors constituent une
bonne base algorithmique.

Risques : génération pouvant échouer/recommencer, maintenance amont faible, manque de contraintes RPG
et de gouvernance OpenRPG.

Action : réimplémenter les concepts utiles dans `OpenRPG DungeonAssembler`; ne pas exposer directement
l'addon au MCP.

### SimpleXTerrain — `algorithm-source`

Pourquoi : érosion, falaises, terrasses, plages, splatmaps et injection Terrain3D amélioreraient le
relief.

Risques : projet très jeune et duplication de l'éditeur de graphes, du streaming et du scatter.

Action : porter et tester les algorithmes individuellement dans l'OpenRPG Terrain Compiler.

### Procedural Forest Demo — `technique-source`

Pourquoi : vent, herbe, billboards, couleur et atmosphère influencent fortement le rendu final.

Risques : système de biomes initial, assets et licences multiples, projet démonstrateur non conçu
comme dépendance de moteur.

Action : réimplémenter les techniques retenues ; ne pas importer le projet ou ses modèles en bloc.

### Foliage3D — `idea-only`

Idée retenue : convertir près du joueur une instance MultiMesh en scène interactive complète, puis la
rendre légère à nouveau lorsqu'elle s'éloigne.

Action : construire un système OpenRPG générique d'`Interactive Instance Promotion`, utile aux arbres,
rochers destructibles, tonneaux et objets environnementaux employés par le gameplay physique.

### Roommate — `future-secondary-interiors`

Utilité possible : maisons ordinaires, caves, entrepôts et casernes. Ne jamais l'utiliser comme unique
générateur des lieux héroïques.

Action : aucun travail avant la réussite du premier monde extérieur de référence.

### Voxel Tools — `future-isolated-zones`

Utilité possible : vraies grottes, tunnels, surplombs et destruction runtime.

Action : uniquement dans des scènes séparées du terrain Terrain3D, après la pile extérieure.

### Waterways.NET — `not-selected`

Le renderer de rivière ne résout pas le lit, les berges et l'hydrologie. Plusieurs fonctions ont été
retirées en amont pour des problèmes de contrôle ou de rendu.

Action : produire d'abord une hydrologie OpenRPG fiable et un renderer simple. Reconsidérer seulement
si un besoin visuel précis reste non couvert.

### Gaea — `rejected-core`

Il double le planificateur macro, le compilateur de terrain et le Graph PCG, tout en restant jeune pour
les grands mondes 3D.

Action : aucune intégration centrale.

## Expériences requises

### Graph PCG

- forêt ;
- parcelles et village ;
- ruines ;
- contraintes d'exclusion routes/eau ;
- scatter de props ;
- régénération partielle ;
- cache, sauvegarde et reprise ;
- plateformes d'export.

### Routes

- ligne droite, virage serré et pente ;
- intersection en T et carrefour ;
- pont ;
- raccord au village ;
- aplatissement et peinture Terrain3D ;
- voies de navigation ;
- LOD et visibilité distante.

### Donjon

- 20 à 30 salles ;
- deux niveaux ;
- chemin critique ;
- boucle ;
- pièce secrète ;
- salle obligatoire ;
- porte et clé ;
- navmesh et reachability ;
- relecture de la seed.

## Sortie de qualification

Chaque candidat reçoit un statut :

- `adopted` : franchit toutes les gates et remplace clairement la baseline ;
- `experimental` : utile mais insuffisamment robuste ou limité à une couche ;
- `rejected` : pas de gain mesuré ou complexité supérieure au bénéfice.

La décision est enregistrée dans une ADR avec les versions, données de benchmark et coûts de
maintenance.
