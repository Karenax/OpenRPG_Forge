# Qualification des backends de génération

## Principe : un propriétaire par responsabilité

La pile de production ne doit jamais contenir deux systèmes concurrents qui écrivent librement la
même couche. OpenRPG Forge reste propriétaire de la recette, des contraintes et de la gouvernance ; un
seul backend concret produit ensuite chaque type d'artefact.

| Responsabilité | Propriétaire cible | Statut |
|---|---|---|
| Terrain extérieur final | Terrain3D | qualifié localement sous Godot 4.7.1/Windows ; exports à qualifier |
| Relief, hydrologie et masques | OpenRPG Terrain Compiler | à construire |
| Graphe PCG général | Godot Flow baseline ; GF-PCGODOT expérimental | proxy 2 km comparé ; choix artistique différé |
| Routes structurées | Graph PCG actuel ou Road Generator | prototype comparatif |
| Topologie de donjon | OpenRPG DungeonAssembler | à construire |
| Habillage de donjon | fournisseur Graph PCG retenu | après choix |
| Scatter de production | fournisseur Graph PCG retenu, ou ProtonScatter si gain prouvé | benchmark requis |
| Eau visible | renderer OpenRPG initial | spécialisé plus tard si nécessaire |
| Navigation | Godot natif, validée sur la géométrie finale | proxy technique réel validé ; module de production requis |
| Gameplay et quêtes | contrats OpenRPG | confirmé |
| Promotion canonique | commande OpenRPG gouvernée | implémentée, testée et réversible |

## Matrice de décision

### Terrain3D — `adopted`

Pourquoi : terrain éditable, matériaux, LOD, trous, humidité, détilement et végétation massive. Il
améliore directement le terrain final et évite de développer un renderer de terrain complet.

Preuve obtenue : l'addon `v1.0.2-stable` est installé avec reçu et SHA-256, chargé sous Godot
`4.7.1-stable` Windows, exécuté trois fois sans fallback, sauvegardé puis rouvert avec collision et
navigation. La scène de promotion produite est provider-free.

Conditions restantes :

- export Windows et Linux ;
- exécution du job CI Linux déjà configuré ;
- contrôle artistique des matériaux et des LOD sur un vrai module extérieur.

### Godot Flow — `baseline-qualified-windows`

Pourquoi : fournisseur réellement épinglé aujourd'hui, graphes, splines, opérations spatiales,
subgraphs et spawn de scènes.

Risques : retour d'expérience limité sur un projet de production, graphes artistiques absents du
dépôt, dépendances natives à qualifier, streaming PCG non équivalent à un système runtime complet.

Résultat : le commit `284231f060099a8943eb56567c21732b7cd678d3` passe le smoke addon et le proxy
2 km sous Godot 4.7.1 Windows avec 5 904 instances, zéro fallback, une route de navigation à 131
points, sauvegarde/réouverture, cinq captures et candidat de promotion neutre.

Action : conserver ce fournisseur comme baseline actuelle. Cette preuve technique ne vaut ni choix
artistique final ni qualification Linux/export.

### GF-PCGODOT — `experimental-qualified-windows`

Pourquoi : fork enrichi, plus de nœuds et fonctions proches du PCG d'Unreal.

Risques : migration et compatibilité des graphes, extensions natives par plateforme, génération
hiérarchique et asynchrone encore incomplètes, coût de maintenance d'un fork supplémentaire.

Résultat : le commit `91471c10d671ecf9e9214d801e8f5b68d3a7df07` a été installé dans une fixture
isolée et comparé sur les mêmes recette, plan, seed et proxy 2 km. Les empreintes de contenu, les
5 904 instances, la navigation, les cinq captures et le candidat de promotion sont identiques à la
baseline. Sur l'unique mesure Windows finale à neuf étapes, le bake live a duré 14 435 ms contre
12 770 ms pour Godot Flow, soit +13,04 % ; ce nombre n'est pas un benchmark statistique.

Action : garder GF-PCGODOT expérimental et hors du projet de production. Il n'a démontré ni gain
visuel aveugle de 15 %, ni réduction de 25 % des retouches, donc Godot Flow reste inchangé.

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

## Preuves locales du 27 août 2026

Le harnais reproductible `scripts/Invoke-WorldGenerationAddonQualification.ps1` a produit, pour les
deux fournisseurs, les artefacts `environment.json`, `providers.json`, reçu d'installation,
recette/plan, métriques, violations, manifeste de scène, scène provider-free, rapport de validation,
candidat de promotion, cinq captures et journaux. Le script
`scripts/Compare-WorldGenerationAddonQualifications.ps1` refuse des entrées différentes et vérifie
les parités fonctionnelle, déterministe, visuelle et de candidat canonique.

Résultat du proxy 2 km Windows :

| Mesure | Godot Flow | GF-PCGODOT |
|---|---:|---:|
| Exécutions déterministes | 3 | 3 |
| Fallback | 0 | 0 |
| Instances | 5 904 | 5 904 |
| Points du chemin de navigation | 131 | 131 |
| Captures normalisées | 5 | 5 |
| Références fournisseur dans la scène promouvable | 0 | 0 |
| Bake live, échantillon Windows | 12 770 ms | 14 435 ms |

La parité des cinq captures montre que les deux adaptateurs ont produit le même proxy technique ;
elle ne prouve pas que ce proxy est beau. Le scoring aveugle, le temps de correction manuelle, un
vrai module artistique, les exports Windows/Linux et l'exécution CI Linux restent ouverts. Aucune
nouvelle ADR de sélection n'est donc créée.
