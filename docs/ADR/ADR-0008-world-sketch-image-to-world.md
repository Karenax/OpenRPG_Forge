# ADR-0008 — WorldSketch comme pont image vers monde Forge

Date : 2026-08-31  
Statut : **accepté pour le pipeline artistique de Gate 1**  
Portée : branche `Rust`, `visual_valley`, préparation Blender et documents Forge  
Complète : `ADR-0001-rust-webgpu-greenfield.md`

## Contexte

Une image de concept peut définir une composition, une ambiance, des masses de forêt, un cours d'eau
et des landmarks. Elle ne fournit pas à elle seule une topographie métrique, des collisions, une
navigation, des splines éditables, une provenance exploitable ou un monde reconstructible. À
l'inverse, importer un grand mesh ou un GLB opaque rendrait les corrections humaines, les mises à
jour partielles et la génération déterministe difficiles.

Gate 1 possède déjà des documents Forge pour le terrain et la végétation, puis des projections Bevy
dérivées. Le chaînon manquant est un intermédiaire éditable entre les références visuelles, Blender et
ces documents canoniques.

## Décision

Le pipeline adopte un document strict `forge.world-sketch` version 1. Un `WorldSketch` contient :

- les références top-down, perspectives et multivues avec chemin relatif, SHA-256, dimensions,
  provenance et statut de droits ;
- la calibration métrique du plan top-down ;
- l'intention de relief et les seeds ;
- les régions sémantiques ;
- les splines de route, rivière et ruisseau ;
- les landmarks ;
- les zones d'incertitude ;
- les verrous humains ;
- les caméras fixes de validation.

Le flux de production devient :

```text
brief et contraintes de gameplay
→ plan top-down + perspectives + multivues d'assets
→ WorldSketch versionné et corrigé
→ compilation déterministe vers documents Forge
→ greybox jouable
→ Blender pour terrain, kits, végétation et bake
→ export GLB/assets + heightfields/masks/splines
→ projection Bevy/WebGPU
→ captures fixes, métriques et revue humaine
→ corrections dans WorldSketch/Blender, puis nouvelle compilation
```

`WorldSketch` ne remplace pas `TerrainDocument`, `VegetationDocument`, les futurs documents de
splines ou les placements. Il en est une source de proposition structurée et traçable. Les images ne
deviennent jamais la source de vérité runtime et un mesh Blender ne devient pas implicitement
canonique.

## Tranches livrées

Le compilateur initial de Visual Valley est volontairement borné à 512 × 512 m. Il :

- valide le document, les IDs, les bornes, les verrous et les caméras ;
- vérifie les octets, SHA-256 et dimensions PNG des références ;
- produit de manière déterministe les documents terrain et végétation existants ;
- produit une entrée Blender dérivée avec grille de hauteur et intentions éditables ;
- émet un rapport qui nomme les sorties matérialisées et les données seulement validées.

Depuis le 1er septembre 2026, la compilation produit aussi `forge.world-semantic-masks` v1 et
`forge.world-spline-carving` v1. Les cinq polygones restent éditables dans le WorldSketch et donnent
chacun un masque feathered `257 × 257`. Les trois splines donnent une grille sculptée, des profils de
centreline, des masques route/lit/berges et une exclusion végétale. Le worker Blender consomme ces
données au lieu de poser de simples rubans sur le terrain d'origine.

Le cache de carving matérialise l'intention visuelle du WorldSketch ; il ne remplace pas
`forge.water` et ne crée pas une seconde rivière runtime. Sa promotion terrain est désormais
explicite : `terrain-carved.v2.json` conserve les deltas clairsemés et passe par le générateur Forge
puis l'adapter des 64 heightfields Rapier existants. `forge.world-traversal-audit` v2 calcule la pente
maximale des triangles adjacents, applique le seuil du profil joueur, bloque l'eau et détecte les
croisements route/eau. Ce document reste un audit provider-neutral, jamais un navmesh ou un
pathfinder. `forge.world-bridge-placements` v1 dérive les atterrissages et le contrat de collision de
`bridge_south` depuis les hashes exacts du terrain et de l'audit. L'adapter Bevy/Rapier matérialise ce
pont et un probe borné avec le vrai contrôleur joueur qualifie le franchissement route/eau. Le
benchmark R1-068 retient Rerecast + Landmass comme piste préférée, mais refuse sa sélection de
production : sortie globale brute invalide, tuiles, reconstruction partielle et carving d'obstacle
dynamique absents, backend maintenu Avian plutôt que Rapier et advisory bloquante. Ce seul lien ne
qualifie pas une carte jouable complète. Ces limites restent dans le rapport de compilation et le
manifeste Blender.

## Blender et génération 3D

Blender 4.5 LTS est le DCC de production et un worker, pas un second moteur. Les scripts `bpy`
reproductibles doivent produire dans un cache dérivé :

- terrain haute résolution et versions runtime ;
- carving route/rivière et masques de matériaux ;
- kits modulaires, retopologie, UV, LOD, collisions et pivots ;
- Geometry Nodes pour scatter contrôlé ;
- turntables et rendus depuis les caméras fixes.

Les générateurs image-vers-3D restent des fournisseurs de candidats d'assets isolés. Chaque candidat
passe par sélection humaine, nettoyage Blender, retopologie, UV/PBR, LOD, collision, échelle, pivot,
provenance et validation avant un export GLB. Aucun fournisseur IA n'est une dépendance obligatoire.

## Alternatives écartées

### Importer directement une image comme heightmap

Rejet : une image de concept contient perspective, ombres, végétation et inventions ; sa luminance
n'est pas une altitude métrique.

### Générer un unique grand GLB de vallée

Rejet : mise à jour partielle, terrain, navigation, collisions, LOD, splines et édition deviennent
opaques ou coûteux.

### Conserver le plan uniquement dans Blender

Rejet : le `.blend` ne remplace pas la source Forge versionnée et vérifiable par le CLI, le Studio et
le runtime.

### Utiliser Three.js/Babylon.js pour reconstruire le monde

Rejet : cela créerait une seconde pile produit. Bevy/wgpu/WebGPU reste la projection officielle ;
Blender fournit les données et assets.

## Preuves obligatoires

Chaque incrément doit fournir :

- round-trip et hash canoniques ;
- trois compilations identiques à seed identique ;
- vérification des sources et de leur provenance ;
- greybox jouable avant finition ;
- collisions, traversées route/eau et accessibilité ;
- sauvegarde/réouverture et conservation des verrous ;
- captures normalisées depuis les caméras du WorldSketch ;
- mesures de performance ;
- inspection humaine de la composition, des matériaux et de la lisibilité.

Une belle image de concept ou un rendu Blender isolé n'est jamais une qualification de carte.

## Conséquences

### Complément local du 2026-09-05 — altitude de rivière explicite

À la suite de l'autorisation de corriger l'altitude et le creusement de `river_main`,
le contrat v1 accepte deux champs optionnels sur une spline d'eau : `surface_heights_m`
(une altitude Y en mètres par contrôle XZ, finie, dans [-1024, 1024], non croissante)
et `maximum_fill_m` (0 à 64 m, uniquement avec ce profil explicite).
L'interpolation altimétrique est linéaire entre les contrôles, sans dépassement ;
le tracé XZ Catmull-Rom et la largeur restent indépendants et inchangés.
Les champs absents préservent la recette et les hashes historiques. Un ancien lecteur
strict refuse les nouveaux champs : cette extension additive nécessite un lecteur mis à jour.
Le remblai est limité au corridor de carving existant et devient prioritaire sur le
terrain dérivé dans ce corridor, comme la coupe de rivière ; aucune nouvelle source canonique.
Les budgets ne garantissent pas l'adéquation eau/terrain : chaque candidat doit encore
passer les mesures de couverture, de berges et le probe du pont. Les commandes Studio
d'édition, undo/redo et promotion ne sont pas implémentées par ce complément Gate 1.
Le candidat 40 m n'est pas promu ; les autres éléments authorés et verrous sont conservés.

L'autorisation suivante élargit la correction au relief environnant. Un champ optionnel
`valley_blend_width_m` (16 à 192 m au-delà du corridor) permet de préparer une plaine riveraine
à 1,5 m au-dessus du profil d'eau avant le carving fin. Il exige un profil altimétrique et des
budgets explicites de coupe/remblai. Le raccord au relief d'origine utilise un smoothstep,
sans modifier les seeds, tracés, largeurs ou landmarks authorés. Les emprises des villages et
forteresses verrouillés sont protégées, avec transition de 16 m autour de leur rayon.
Les routes/ruisseaux dérivés sont rééchantillonnés sur ce relief avant leur carving : conserver
un ancien cache altimétrique serait incohérent. Le corridor fin, l'exclusion végétale et le
maillage de berge conservent leur largeur ; le raccord large ne crée pas une bande déboisée.
Les compilations et probes doivent requalifier terrain, eau, accès et pont ; l'autorisation
d'implémenter cette correction ne clôt pas la revue humaine de Gate 1.

La promotion locale incrémente la révision du WorldSketch à 2. Les copies dérivées des profils
d'environnement, d'eau et de météo sont liées explicitement à cette révision dans les deux
chargeurs natif/Studio ; les modèles embarqués restent révision 1 et leurs valeurs ne changent pas.
Les validations d'identité projet/zone/révision restent actives. L'ancienne recette est conservée
en fixture pour ses tests historiques, et un test supplémentaire couvre la source embarquée actuelle.

Le raccord Studio du pont a révélé une différence native/WASM d'un ULP sur `atan2f`, donnant
deux hashes pour des positions identiques. La pente diagnostique persistée du pont est désormais
arrondie à 0,0001 degré, après validation de la pente brute contre le contrat joueur. Les positions
et colliders ne sont pas arrondis ; les anciens documents restent lisibles. Cette précision concerne
le diagnostic du pont, pas une tolérance ajoutée au contrôle des sources ou à la géométrie.

Le pipeline devient corrigeable, traçable et partageable entre IA, Blender, CLI, Studio et runtime.
Le coût est une étape structurée supplémentaire et l'obligation de signaler ce qui reste incertain ou
non matérialisé. Cette discipline est volontaire : elle évite de confondre concept art, asset 3D,
document de monde et preuve runtime.
