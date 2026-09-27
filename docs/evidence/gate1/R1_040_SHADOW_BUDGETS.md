# R1-040 — Budgets d'ombres raccordés

2026-09-05, branche Rust, arbre local modifié, non publié. **Gate 1 et R1-040 ouverts.**

## Implémenté

`VisualValleyShadowQualityPlugin` consomme la section ombres du document de qualité existant,
après validation du contenu et de l'identité projet/zone/révision. La requête transitoire
`ShadowQualityRequest::request` ne modifie ni source, ni entité/pose/couleur/éclairement du soleil.
L'atlas étant global, l'application exige exactement une lumière directionnelle marquée soleil
de vallée et une caméra 3D active. Une topologie ambiguë ne modifie pas l'atlas et ne produit pas
de niveau déclaré projeté.

| Preset | Cascades | Résolution atlas | Distance maximale | Contact |
| --- | ---: | ---: | ---: | --- |
| Low | 2 | 1024 | 64 m | non |
| Medium | 3 | 2048 | 96 m | non |
| High | 4 | 4096 | 128 m | oui |
| Ultra | 4 | 4096 | 192 m | oui |

Contacts : composants Bevy 0.19.1 sur lumière et caméra, défaut borné à 16 pas, longueur 0,3 m,
épaisseur 0,1 m. Le depth prepass requis reste présent après désactivation, car d'autres effets
en dépendent. Un preset sans ombres garde un atlas de taille valide, désactive la lumière et vide
les bornes ; aucune texture de dimension zéro n'est demandée. Les presets livrés activent tous
les shadow maps ; le cas désactivé est synthétique et testé en ECS.

L'installation s'effectue en Update, avant les calculs de cascades Bevy en PostUpdate ;
l'observation suit ces calculs. La dernière borne est fixée exactement au budget authoré :
le test a révélé un dépassement de quelques ULP par l'espacement géométrique Bevy, corrigé
dans l'adapter sans affaiblir l'assertion.

Le Runtime expose `--shadow-quality low|medium|high|ultra` dans les deux modes vallée seulement,
refuse doublons/valeurs absentes ou inconnues et conserve le comportement antérieur sans option.
La recette embarquée est liée en mémoire à la révision compilée, comme les profils de météo.
`visual_valley_shadow.quality` distingue demandé et projeté CPU, avec hash de cette recette liée.
**Ce n'est pas une preuve d'exécution GPU isolée des contacts, ni un preset global appliqué.**

## Vérifié

- 344 tests workspace PASS ; quatre tests moteur ajoutés : cycle High/Low/Ultra/Medium/High,
  soleil et source conservés, retour exact des bornes High, topologies ambiguës puis réparation,
  désactivation/réactivation et rejet des politiques invalides/incompatibles ; un test CLI ajouté.
- Clippy workspace/all-targets/all-features, `--locked -- -D warnings` : PASS.
- Build Runtime natif et check moteur web/WASM : PASS. Pas de nouveau lancement Chrome.
- Cinq lancements natifs terminés code 0, zéro ERROR, kit 20 instances/36 colliders présent.
  Presets demandés/projetés, tailles, nombres de cascades, distances et contacts contrôlés
  dans les métriques des quatre premiers lancements ; replay High également inspecté.

Conditions : RX 7800 XT, Vulkan, pilote 26.8.1, 1920×1080, AutoVsync, caméra `human_bridge`, clair.
Runtime SHA-256 : `5d6d01bb83d9bfcf5e7fd8c10ffec058e5b547b2a29002d652fc1d1ec0450698`.
Commandes communes : `--world-sketch-visual-valley --weather clear --shadow-quality <niveau>`,
`--asset-kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`,
`--asset-kit-preview E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-normals-after-20260905/human_bridge-clear/preview.json`.

Collecte initiale : `--capture <niveau.png> --capture-frame 300 --metrics <niveau.json>
--metrics-warmup-frames 420 --exit-after-frames 1020`, dossier local
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shadow-quality-20260905`.
Captures Low/Ultra inspectées : scène rendue et ombres visibles, limites artistiques préexistantes
conservées. **La capture High initiale est noire et rejetée**, malgré le code 0 ; son overlay indique
zéro primitive opaque, alors que les métriques finales en observent 285374. Un délai fixe de
300 images ne garantit donc pas la disponibilité du rendu. Cette cause exacte n'est pas isolée ;
le préchauffage/disponibilité des pipelines reste un chantier R1-039, pas un résultat acquis.

Replay High : mêmes entrées/binaire, capture 900, warmup 1020, sortie 1620, dossier
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shadow-quality-high-late-20260905`.
Capture inspectée : scène complète et ombres visibles. Les composants contacts sont présents ;
la contribution des contacts seule n'est pas isolée par cette vue.

Mesures courtes de 600 images, après la capture, pas qualification matérielle/longue durée :

| Run | FPS moyen | p95 ms | 1 % low FPS | Images >16,7 ms |
| --- | ---: | ---: | ---: | ---: |
| Low initial | 130,86 | 8,27 | 102,28 | 0 |
| Medium initial | 123,63 | 8,85 | 97,23 | 0 |
| High initial, capture rejetée | 108,31 | 10,41 | 79,04 | 0 |
| Ultra initial | 108,46 | 9,95 | 84,86 | 0 |
| High replay tardif | 109,05 | 9,85 | 88,82 | 0 |

Ces runs ne sont pas une mesure d'optimisation avant/après. Le replay High a un préchauffage
différent ; aucune preuve de transition GPU à chaud, de non-régression globale ou de 60 FPS
sur GPU milieu de gamme n'en est déduite. Les avertissements Vulkan de couche dupliquée et
WGSL `@invariant` restent visibles dans les logs ; ils ne sont pas relabellisés en erreurs absentes.

## Pins et limites

WorldSketch fichier vérifié avant/après :
`5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.
Politique fichier inchangée : `ec4c0db5b3a0287a309a541cb889120143cecd515dec2cea748f527c0c0fd9e3`.
Politique canonique liée à la révision 3, observée :
`57c8099e9be1b6b150b5d678a0920fadd5fbbfaa50d378d7e9c43dbbbdfd2a35`.
Terrain observé : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.

| Artefact | SHA-256 |
| --- | --- |
| Low PNG | `68ae1ad581ccb3736eb0a5c7c83192e6923e8119532598e50f2bab1fbe27e63e` |
| Low métriques | `30767f2547888b5a6fba8223c9a264b034440ad54de018c167f03e128d9c32c2` |
| Medium métriques | `32ca17c0a8edde51551d84082a4af4a6e7c566df249394238b514d8b1f6296ba` |
| High PNG rejeté | `cfb398e3545d6e9c452d2af0c0a1560e2e666a5464329626e5cae36ff570ccc6` |
| High métriques initiales | `cb9d9dba9fed309b63aa09c66b3ea5529d45c959ad3d560e0d2ff82f855c4160` |
| Ultra PNG | `1b3f84c92a8fb6a8b6ff3d2a0c5e87a22db1a5c3b49864877aa72607981e978e` |
| Ultra métriques | `edd4cc2f2421578ba51400515de3b863b00f04a21fb0509449f5dd67a63dbc18` |
| High replay PNG | `482a4fdee3700b7cde2ce6c65e37724943d84220a71e8da6b1ef21fff6ea927d` |
| High replay métriques | `4d0645c7291027c00f03b7e62ddf78e80674a91bb3e6af072160c2b103fb7686` |

Pas de nouvelle dépendance ni asset. Pas de contrôleur global, de sélection Studio ou d'accusé
global automatique. Les budgets terrain/végétation/environnement/eau/post-traitement restent à
raccorder, puis l'ensemble à qualifier. CI **NOT RUN**, arbre propre ADR-0006 et revue humaine
toujours requis pour la qualification finale. Aucun passage à Gate 2.
