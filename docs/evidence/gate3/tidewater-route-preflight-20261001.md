# Tidewater Gate3 — parcours village → forêt → House H

> **Statut : préflight de raccord, non qualifié.** Ce document rapporte les coordonnées XZ et les contrats actuellement observables dans les sources Tidewater. Il ne constitue ni une preuve de navigation, ni une preuve de collision intérieure, ni une sélection de fournisseur. Aucun build, ajout de dépendance ou changement de la carte n'est requis par ce préflight.

## Source de vérité et limites de l'archive

L'archive d'auteur actuellement exploitable est :

```text
E:\OpenRPG_Forge\.tidewater-integration\studio-tidewater-review-20260927-001\e620dd19-ead9-5913-a6d6-0f93109148c0\authoring.json
```

Elle contient une session `forge.zone-edit-session` version 4, révision 3, pour l'aire `e620dd19-ead9-5913-a6d6-0f93109148c0`. Le terrain de base est un raster `2048 × 2048`, espacement 1 m, origine XZ `(-1024,-1024)`, système droitier, Y vers le haut, avec la source float32 little-endian suivante :

```text
E:\OpenRPG_Forge\.tidewater-integration\studio-tidewater-review-20260927-001\e620dd19-ead9-5913-a6d6-0f93109148c0\source-blobs\be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3.bin
```

La taille observée est 16 777 216 octets et le SHA-256 déclaré est `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`. Une copie du même raster est présente dans le dernier candidat de package observé :

```text
E:\OpenRPG_Forge\.tidewater-integration\package026-independent-qa-20260929-01\OpenRPG-Forge-Aube-Tidewater-Win64-20260929-026\data\workspace\e620dd19-ead9-5913-a6d6-0f93109148c0\source-blobs\be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3.bin
```

Le fichier de description terrain est `E:\OpenRPG_Forge\.tidewater-integration\terrain-v4-001.json`. Dans l'archive, l'élévation de l'objet `House H` est seulement une translation d'auteur `y: 0`; ce nombre n'est pas le niveau du sol. L'archive ne contient pas de `floorY`, de hauteur de porte, d'identifiant de pièce intérieure, ni de collider de porte.

La géométrie amont correspond au checkout suivant :

```text
E:\OpenRPG_Forge\.tidewater-integration\upstream\
commit 1438b1abfcaee3267092b75573014f4d9b4a983c
```

Les fichiers qui portent les faits utilisés ci-dessous sont `src/world/WorldLayout.js`, `src/world/terrain/IslandShape.js`, `src/world/TerrainData.js`, `src/world/Village.js`, `src/world/village/Buildings.js` et `src/world/Colliders.js`.

## Parcours XZ observé

Les valeurs du tableau sont des coordonnées horizontales dans le repère Tidewater. La colonne Y reste volontairement une opération à effectuer sur le terrain canonique.

| étape | XZ canonique observé | provenance | Y | état du préflight |
| --- | --- | --- | --- | --- |
| Arrivée village / début de jeu | `(53.6, -77.0)` | `WorldLayout.start`, commentaire du boardwalk depuis le ponton | à échantillonner | point de départ horizontal, pas une preuve de support |
| Place / centre du village | `(42.0, -116.5)` puis centre `(40.0, -118.0)` | `WorldLayout.village` et premier point de la branche village → ridge | à échantillonner | corridor source de largeur `0.6 m` |
| Branche vers forêt, entrée de candidat | `(16.0, -270.0)` | `IslandShape.PATHS`, branche hiking | à échantillonner | point XZ reproductible, largeur de chemin source `0.6 m` |
| Branche vers forêt, sortie de candidat | `(11.0, -283.0)` | `IslandShape.PATHS`, même branche | à échantillonner | le segment contient 6 arbres à moins de 8 m et 32 à moins de 20 m dans le blob canonique ; c'est un indice de densité visuelle seulement |
| Retour village / porte visuelle de House H | `(57.370054, -125.903249)` | `Village.js` H, porte locale `(0, 2.6)`, yaw `-0.05` | à échantillonner | position XZ de la porte générée, aucun collider de porte identifié |
| Seuil extérieur / centre du porche | `(57.320075, -124.904499)` | `Buildings.js`, porche local `(0, 3.6)` | `floorY - 0.05` après calcul | collider amont `porch`, `walkable:true`, `solid:true` |
| Départ des marches | `(57.270096, -123.905749)` | `Buildings.js`, local `(0, 4.6)` | à échantillonner pour `stairRun` | début de l'escalier ; nombre et hauteur des marches dépendent du terrain |
| Objet statique canonique proche | `(61.010661, -129.625947)` | `House H`, `woodpile:-1`, local `(3.45,-1.3)` | à échantillonner | collider `woodpile`, demi-extents `(0.3, 0.3, 0.6)` ; aucune sémantique de charge |

La branche forêt documentée relie la place vers le sud de l'île. House H est dans la zone du village, près du premier tronçon, et non au bout de cette branche. Le parcours village → forêt → House H doit donc expliciter le retour vers la zone du village ; aucun lien narratif ou waypoint intérieur n'est présent dans les sources amont.

Les points intermédiaires de la branche sont, dans l'ordre amont :

```text
(42,-116.5) → (45.5,-126) → (48,-138) → (49,-150) → (49.5,-162)
→ (47,-175) → (41,-188) → (36,-199) → (31,-212) → (33,-224)
→ (26,-236) → (20,-247) → (24,-258) → (16,-270) → (11,-283)
→ (15,-296) → (8,-309) → (4,-322)
```

Les positions de végétation proviennent du blob `cfa7b52feaa9ebbaf5b0127af3640ab6e1b0578f47ad25c9d5c2987c4045c29b.bin` de l'archive d'auteur. Les couches `vegetation` de `authoring.json` ont `collision:null`; les arbres prouvent une distribution visuelle/provenance, pas une surface bloquante ni une navigation qualifiée.

## House H : dimensions, rotation et points d'accès

La placement d'auteur de H est `c8c877fb-d10e-5dd8-abdc-0cdbdcadc6b0`, asset `a6371043-877c-5ce2-b6ba-3bcbb7f6d3a6`, avec transform :

```text
translation = (57.5, 0, -128.5)
rotation quaternion = (w 0.999687492442, x 0, y -0.0249973963954, z 0)
source yaw = -0.05 rad
scale = 1
```

La définition de `Village.js` est `w:6.0`, `d:5.2`, `yaw:-0.05`, porche de profondeur `2.0`, annexe `w:3.4`, `d:2.2`, décalage local X `0.6`. La hauteur est celle d'une maison à un niveau, avec le `storyH` par défaut de `2.95 m`. Le corps principal occupe le rectangle local X `[-3,3]`, Z `[-2.6,2.6]`; le porche est devant, local Z `[2.6,4.6]`; l'annexe est derrière, local Z `[-4.8,-2.6]`.

La conversion utilisée par `Buildings.js` est :

```text
worldX = 57.5 + localX*cos(-0.05) + localZ*sin(-0.05)
worldZ = -128.5 - localX*sin(-0.05) + localZ*cos(-0.05)
```

La porte visuelle est construite à `doorX=0`, local Z `d/2 = 2.6`; le centre de cette porte donne le XZ du tableau. Le `doorUnit` est une géométrie visuelle et ne fournit pas d'identifiant d'interaction.

Le porche et les marches sont les seuls points d'accès extérieurs explicitement portés par des colliders amont : `porch` et `stairs`, chacun marqué `walkable:true, solid:true`. Le bois de H porte le tag `woodpile`, mais l'amont n'en fait ni une charge, ni un objet récupérable, ni une interaction.

Le niveau de sol de la maison n'est pas déductible de la translation d'archive. `buildHouse` échantillonne `TerrainData.heightAt` sur le volume local du corps, de l'annexe et du porche, prend `gMax`, puis calcule :

```text
floorY = gMax + (clearance ?? 0.55)
```

Le `foundation` est aussi choisi dans `Village._layout` lorsqu'il n'est pas imposé ; l'archive actuelle ne conserve pas cette décision. Le nombre, la hauteur et la position Y des marches sont calculés dans `stairRun` à partir du terrain, avec une montée maximale par marche de `0.2 m`. Aucun chiffre Y ne doit donc être écrit dans le contrat Tidewater avant échantillonnage.

## Blocage actuel de l'intérieur

Le constructeur amont ajoute après les éléments visuels un collider rectangulaire couvrant tout le corps principal, taggé `house`, avec des demi-extents horizontales d'environ `w/2 + 0.06` et `d/2 + 0.06`. Cette boîte couvre donc le volume derrière la porte. Il n'existe dans la source observée ni découpe de doorway, ni collider intérieur par pièce, ni porte ouvrable.

Conséquence : le chemin jusqu'au seuil/porche est un candidat extérieur mesurable ; le passage dans l'intérieur de H est **NO-GO pour Gate3** tant qu'un contrat d'auteur générique ne fournit pas un volume intérieur et une ouverture de porte, avec leurs colliders et leur hauteur de sol. Il faut rattacher ces données à la placement/asset canonique de H, sans ajouter une branche moteur dépendante du slug Tidewater.

Les objets pouvant porter un raccord générique sont donc limités à :

| rôle générique | porteur canonique disponible | limite |
| --- | --- | --- |
| porte / point d'interaction | placement `c8c877fb-d10e-5dd8-abdc-0cdbdcadc6b0`, asset `a6371043-877c-5ce2-b6ba-3bcbb7f6d3a6` | la porte visuelle n'a pas d'ID gameplay ni d'ouverture physique |
| support de franchissement | colliders `porch` et `stairs` de `buildHouse` | Y à recalculer ; pas de preuve Rapier/nav à l'échelle de Gate3 |
| support d'objet / charge | collider `woodpile` et sa position locale connue | pas de contrat amont d'objet récupérable ou transportable |
| intérieur | aucun porteur archive vérifié | contrat intérieur/doorway manquant |

## API pour obtenir les vrais Y dans Rust

Pour les données Tidewater importées, le chemin de référence est le raster autorisé, pas la hauteur `y:0` du placement :

1. `crates/forge_project/src/authoring/host/terrain_source.rs::AuthoringHost::read_terrain_source` lit les blocs `source-blobs` autorisés.
2. `crates/forge_project/src/authoring/terrain_source.rs::TerrainSourceAssembly::{new,accept,finish}` vérifie le document, le SHA, la taille et l'assemblage séquentiel.
3. `crates/forge_world/src/sampled_terrain/plan.rs::SampledTerrainPlan::from_document` construit le plan raster et `SampledTerrainPlan::height_at(x,z)` fournit l'élévation canonique à la coordonnée XZ.
4. `crates/forge_world/src/sampled_terrain/mesh.rs::SampledTerrainPlan::build_patch` est le chemin de géométrie associé ; `apps/forge_studio/src/authoring_raster.rs` montre le chargement Studio du même contrat.
5. Pour une vérification physique locale, `crates/forge_engine/src/sampled_terrain_physics.rs` découpe le plan en heightfields Rapier et `crates/forge_engine/src/tidewater_player.rs` combine `surface.terrain.height_at(...)` avec le `ReadRapierContext`/raycast. Cette vérification ne crée pas à elle seule le collider intérieur de H.

Pour reproduire exactement le `floorY` amont, il faut en plus exécuter les opérations de `TerrainData.heightAt` et `Village._flattenPads` dans le même ordre que `Village.js`, puis appliquer les échantillons de `buildHouse`; le simple `SampledTerrainPlan::height_at` au centre de H ne suffit pas, car `gMax` porte sur les extents du bâtiment, de l'annexe et du porche.

Attention aux helpers Studio actuels : `apps/forge_studio/src/authoring_paths.rs::frame_selected` et `authoring_path_guides.rs` passent par `TerrainGenerationPlan`, qui correspond au terrain procédural Visual Valley. Ils ne doivent pas être utilisés comme oracle Y pour cette route Tidewater tant qu'ils ne sont pas raccordés au `SampledTerrainPlan` importé.

## Oracles et décision de suite

Le prochain préflight concret doit enregistrer, pour chaque point du tableau, le couple `(XZ, height_at(XZ))`, puis les contacts Rapier de la surface et des colliders `porch`/`stairs`. Pour H, il doit aussi enregistrer la décision explicite suivante :

```text
extérieur/porche : testable après échantillonnage Y et preuve de collision ;
intérieur : NO-GO tant qu'une ouverture et un volume intérieur génériques ne sont pas dans la source d'auteur.
```

Ces mesures sont une condition préalable à une boucle village → forêt → retour → House H ; elles ne transforment pas les coordonnées source en qualification Gate3 et ne justifient aucun choix de fournisseur de navigation.
