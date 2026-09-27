# Pipeline de production de cartes AAA

Statut : **démarré dans Gate 1 — Visual Valley**  
Décision : `docs/ADR/ADR-0008-world-sketch-image-to-world.md`  
Pilote : `examples/visual_valley/world-sketch.v1.json`

## Résultat visé

Produire une zone RPG originale, lumineuse, grandiose et jouable à partir de références visuelles,
sans transformer une image ou un grand mesh en source de vérité opaque. La qualité attendue couvre en
même temps composition, relief, matériaux, végétation, eau, architecture, animation, collisions,
navigation, performance et capacité de correction.

Le pipeline officiel est :

```text
direction artistique + contraintes de gameplay
→ plan top-down métriquement calibré
→ perspectives de référence
→ multivues par asset important
→ WorldSketch éditable et versionné
→ compilation vers documents Forge
→ greybox runtime qualifiable
→ production Blender
→ exports terrain/assets gouvernés
→ Bevy/wgpu/WebGPU
→ tests + captures fixes + revue humaine
→ boucle de correction
```

## 1. Entrées visuelles

Trois familles ont des rôles différents :

1. Le plan top-down fixe les masses, l'hydrologie, les routes, les zones constructibles et l'échelle.
2. Les perspectives fixent la composition, les matériaux, la lumière, la densité et les landmarks ;
   elles ne sont pas des cartes métriques.
3. Les fiches multivues servent à fabriquer un asset isolé ; elles ne produisent pas une zone entière.

Chaque fichier possède un chemin relatif, un SHA-256, ses dimensions, le fournisseur, l'outil, le
prompt, le statut de droits et les limites connues. La publication nécessite une revue explicite des
droits, y compris pour un contenu généré.

## 2. WorldSketch

`forge.world-sketch` v1 stocke les éléments corrigeables avant leur matérialisation :

- calibration en mètres ;
- paramètres de relief ;
- régions forêt, prairie, roche, eau, marais et constructible ;
- splines de route et d'eau ;
- landmarks et rayons d'influence ;
- incertitudes visibles ou inventées ;
- verrous humains ;
- caméras de validation.

Le document est strict : champs inconnus, IDs invalides ou dupliqués, chemins absolus/traversants,
valeurs non finies, géométrie hors zone, verrous orphelins et mauvaises références de caméra sont
refusés.

Commandes du premier incrément, exécutées depuis la racine du dépôt :

```powershell
cargo run -p forge_cli -- validate-world-sketch `
  examples/visual_valley/world-sketch.v1.json --source-root .

cargo run -p forge_cli -- compile-world-sketch `
  examples/visual_valley/world-sketch.v1.json `
  E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4 --source-root .
```

La compilation refuse un dossier de sortie existant. Elle produit `terrain.v1.json`,
`terrain-carved.v2.json`, `vegetation.v1.json`, `semantic-masks.v1.json`,
`spline-carving.v1.json`, `traversal-audit.v2.json`, `bridge-placements.v1.json`,
`blender-input.v4.json` et `world-sketch.compilation.v4.json`. L'entrée Blender contient la grille
source, les cinq masques, le terrain sculpté, l'audit de praticabilité, le contrat du pont, les
landmarks et les caméras. Elle reste dérivée du WorldSketch. Le rapport distingue les sorties
matérialisées des qualifications runtime externes à la compilation.

Les masques utilisent la grille terrain `257 × 257`, une valeur `u8` par région et un feathering
versionné de 6 à 12 m. Le carving échantillonne les trois centrelines, aplanit la route avec
coupe/remblai bornés, creuse rivière et ruisseau, produit les bandes de berges et agrège une exclusion
de végétation. Les polygones et splines du WorldSketch restent les sources éditables ; les rasters et
heightfields sont des caches reconstructibles.

## 3. Greybox avant art

Avant de créer les assets :

- vérifier l'échelle et la route jouable ;
- vérifier les pentes, le pont, les berges et les franchissements ;
- tester les silhouettes depuis toutes les caméras fixes ;
- confirmer le village, le keep et la cascade comme landmarks lisibles ;
- préserver les zones de gameplay et les verrous.

Une correction de composition retourne dans le WorldSketch avant la production détaillée.

## 4. Production Blender

Blender 4.5 LTS reçoit les données dérivées du WorldSketch et produit des caches reconstructibles.
La voie par défaut est un script `bpy` déterministe et headless ; l'interface graphique est réservée
aux actions qui exigent réellement une intervention manuelle.

Ordre recommandé :

1. relief haute résolution et érosion bornée ;
2. carving rivière, ruisseau et route ;
3. splat/masks PBR et berges ;
4. blockout du village, du pont, du keep et de la cascade ;
5. kit architectural modulaire ;
6. rochers, arbres, arbustes et sous-bois ;
7. Geometry Nodes pour le scatter, puis composition manuelle des landmarks ;
8. UV, PBR, LOD/HLOD, collisions, pivots, échelle et validation ;
9. exports et rendus depuis les caméras du WorldSketch.

Le terrain ne sort pas comme un unique GLB canonique. Les données conservées sont heightfield,
masques, chunks, splines et paramètres ; les assets isolés utilisent glTF/GLB.

## 5. Fabrique d'assets

Chaque objet suit :

```text
concept/multivue
→ trois candidats maximum
→ sélection humaine
→ nettoyage Blender
→ retopologie
→ UV et textures PBR
→ LOD/HLOD
→ collision
→ pivot et échelle
→ turntable
→ manifeste de provenance
→ GLB validé
```

Catégories :

- `hero` : pont, keep, bâtiments majeurs, arbres remarquables ; finition et composition manuelles ;
- `mid` : maisons modulaires, rochers, troncs, variantes d'arbres ; génération assistée puis cleanup ;
- `clutter` : petites pierres, branches, fougères, fleurs ; procédural déterministe et instancié.

## 6. Validation

Les cinq gates du lot sont cumulatives :

| Gate | Preuve minimale |
| --- | --- |
| Technique | schémas, hashes, build, export, rechargement |
| Gameplay | route, collisions, pentes, pont, eau, caméra humaine |
| Performance | moyenne, p95, 1 % low, CPU/GPU, VRAM, instances, hitches |
| Reproductibilité | trois runs identiques, save/reopen, verrous conservés |
| Artistique | douze vues fixes, mouvement, matériaux, lumière, composition et revue humaine |

La revue artistique compare la scène runtime, pas seulement le concept. Les arbres, berges, rochers,
répétitions, transitions LOD, ombres et zones vides sont inspectés à la résolution originale.

## 7. État du pilote au 1er septembre 2026

Preuves : `docs/evidence/gate1/R1_056_062_WORLD_SKETCH_BLENDER.md` puis
`docs/evidence/gate1/R1_060_061_SEMANTIC_MASKS_SPLINE_CARVING.md` puis
`docs/evidence/gate1/R1_066_CARVED_TERRAIN_COLLISION_TRAVERSAL_AUDIT.md` puis
`docs/evidence/gate1/R1_067_BRIDGE_PLACEMENT_RAPIER_CROSSING.md` puis
`docs/evidence/gate1/R1_068_NAVIGATION_PROVIDER_BENCHMARK.md`.

Livré dans le pilote actuel :

- deux références originales générées et traçables : plan top-down et vue héroïque ;
- `WorldSketch` v1 avec 5 régions, 3 splines, 5 landmarks, 2 incertitudes, 6 verrous et 4 caméras ;
- validation stricte des données et des sources PNG ;
- compilation déterministe vers les documents terrain et végétation déjà consommés par Bevy et vers
  cinq masques sémantiques, un cache de carving et une entrée Blender reconstruisible ;
- terrain sculpté à 2 m : route aplatie, rivière et ruisseau creusés, berges matérialisées et
  exclusion végétale appliquée au scatter ;
- terrain sculpté promu en `forge.terrain` v2 avec 8 722 deltas, consommé sans nouvel adapter par les
  64 heightfields Rapier ; raycast sur un point réellement sculpté vérifié contre la surface Forge ;
- audit `forge.world-traversal-audit` v2 : pente maximale des triangles, seuil joueur de 45°, eau
  bloquée, composantes terrestres, continuité des routes et croisement en attente d'un probe runtime ;
- placement dérivé `forge.world-bridge-placements` v1 lié aux hashes du terrain et de l'audit ;
- présentation Bevy sans asset, cinq colliders Rapier et franchissement réel du pont avec 613/613
  ticks au sol, sans remplacement d'identité terrain, joueur ou pont ;
- benchmark isolé Rerecast + Landmass : tous les probes locaux passent et les sorties extérieures
  sont déterministes, mais l'intégration est refusée car la vallée brute contient un winding rejeté,
  les tuiles, la reconstruction partielle et le carving d'obstacle dynamique manquent, aucun backend
  Rapier maintenu n'est fourni et `cargo-deny` bloque `RUSTSEC-2025-0141` ;
- worker Blender 4.5 version 7 reproductible qui consomme le même contrat et matérialise visuellement
  terrain, régions, trois splines, pont incliné, village, keep, cascade, 260 arbres et 140 rochers ;
- `.blend`, GLB avec couleur sémantique `COLOR_0`, trois atlas de masques, manifeste de hashes et
  quatre captures fixes 1920 × 1080 ;
- deux reconstructions Blender : GLB identique octet par octet et pixels RGBA identiques pour les
  quatre caméras et trois atlas ; les conteneurs PNG/BLEND peuvent différer par métadonnées internes ;
- rapport explicite de ce qui reste non matérialisé ou non qualifié dans le runtime.

Étapes suivantes, dans l'ordre :

1. lever les blocages du fournisseur de navigation ou benchmarker une autre pile ; aucune
   intégration de navmesh n'est autorisée par le résultat R1-068 ;
2. produire le premier kit gouverné rochers/arbres/architecture avec LOD, collisions et turntables ;
3. intégrer les placements artistiques gouvernés dans Bevy et comparer les mêmes caméras à la vue
   héroïque ;
4. corriger la composition, puis qualifier performance, traversabilité globale et revue artistique
   runtime.

Le pilote n'est pas encore une carte AAA. Il établit le pipeline reproductible qui permet d'y arriver
sans recommencer une scène opaque à chaque itération.
