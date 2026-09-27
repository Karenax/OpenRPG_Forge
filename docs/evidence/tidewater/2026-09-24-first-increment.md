# Tidewater — premier incrément, parité complète non atteinte

Autorisation : ADR-0017. Source figée :
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
Travail local sur Rust ; aucun commit ni publication de ce chantier.

## Données et lecteur Rust

- Clones `upstream` et `reference` toujours propres après extraction.
- Exports `source-006` et `source-007` : 105 fichiers manifestés, hashes identiques.
  Incluent les hauteurs après fondations du village, les masques, 65 383 végétaux,
  huit prototypes bruts, les modules WGSL végétaux évalués, les paramètres de
  matériaux, la texture de détail et tous les mipmaps de l'atlas de feuilles GPU.
- Vérificateur : fichiers/hash/tailles, attributs finis, triangles, IDs uniques,
  dépendances shaders, dimensions et contenu de l'atlas (ni vide ni opaque partout).
- `verify_sampled_source` : 1 030 sondes source, erreur maximale
  `2.842170943040401e-14` m. Hauteurs f32 exactes, interpolation f64 et demi-texel
  respectés. SHA-256 des hauteurs :
  `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.
- Tests `forge_project` + `forge_world` : 416 réussis, zéro échec.
- Clippy `--all-targets -- -D warnings` sur ces deux crates : réussi.
- `cargo check` de ces deux crates pour `wasm32-unknown-unknown` : réussi.
  Cela valide la compilation des données, pas le rendu WebGPU.

Le contrat raster n'est pas encore référencé par `TerrainDocument`/zone ni par les
commandes Studio. Un export source n'est pas une carte Forge chargée.

## Référence visuelle

Six PNG 1920×1080 inspectés : côte, village, relief oblique, récif, canopée et côte
de nuit. Overlay `scripts/tidewater/reference.html`, 120 pas de 1/60 s après
initialisation upstream, caméra et paramètres enregistrés. Aucun remplacement
des systèmes graphiques dans cette référence. Index et captures hors dépôt :
`E:/OpenRPG_Forge/.tidewater-integration/baseline/`.

Limites : pas encore de parcours, cadrage oblique partiel de l'île, adaptateur
non identifié dans les métadonnées, état de précompilation/readbacks asynchrones
non qualifié pour un replay temporel exact. Aucune mesure de performance.

## Premier palmier dans Bevy

Commande exécutée avec succès :

```powershell
cargo run -p forge_engine --example tidewater_plant_probe --locked -j1 -- E:/OpenRPG_Forge/.tidewater-integration/plant-probe-002 E:/OpenRPG_Forge/.tidewater-integration/native-palm-003.png
```

Fixture construite par `prepare-plant-probe.py` depuis `source-006`. Elle reprend
le premier palmier, son placement, ses attributs et les expressions originales
de déformation, découpe, albédo et rugosité. Le runtime est Rust/Bevy ; Node sert
uniquement à l'extraction source. Chargement avec contrôle des hashes et bornes.

Capture réellement inspectée en FHD ; le tronc et les frondes découpées sont
visibles. Les PNG `native-palm-002.png` et `native-palm-003.png` sont identiques :
`8eff2dd6f4037d8c9993ae0060a76169cf12633bf52090504d10502ed4902dc8`.

- Fixture : `1819ca2b9927e72becd774fcf334ee2e84f888bdc91da07f3d3d7ad2232d57be`.
- Binaire utilisé : `3cb2f94e9b6ba16f506d84c1b6ccdaef9812a27de9a8ac3f2b3039bfa77dfa1c`.
- Clippy strict du prototype : réussi.
- Capture conditionnée à la disponibilité stable des pipelines. Les états
  transitoires `ShaderNotLoaded` / `ShaderImportNotYetAvailable` attendent ; les
  vraies erreurs et le dépassement de délai font échouer le processus.

Ce prototype est **isolé** : éclairage PBR Bevy, fond uni, instant fixe. La lumière
indirecte, la translucidité éclairée/ombrée de Tidewater, les ombres, les LOD et le
traitement temporel ne sont pas reproduits. Il ne constitue pas une preuve de
parité du palmier avec la scène source, ni un asset Studio qualifié.

## Reprise

La fixture v2 et le prototype ont également été exécutés sur un feuillu et un
arbuste : captures `native-tree-002.png`, `native-shrub-001.png` et régression
`native-palm-v2-001.png` inspectées. Le shader vertex provient désormais directement
du descripteur source, avec table de lobes, atlas/mipmaps, échelle verticale et
filtrage anisotrope 8×. Clippy strict réussi. L'arbuste conserve ici la caméra du
palmier : il apparaît petit et bas dans le cadre, sans être coupé. Même réserve
sur l'éclairage Bevy provisoire. Les comparaisons ultérieures sont consignées
ci-dessous. Binaire du premier jalon archivé localement sous
`.tidewater-integration/plant-probe-v1.exe` pour conserver les preuves précédentes.

1. Comparer les déformations et silhouettes source/Bevy dans les mêmes conditions.
2. Ajouter vent animé, LOD/ombres et qualification visuelle des plantes.
3. Brancher les sources importées au contrat de zone et aux commandes Studio.
4. Poursuivre village/île complète, océan, atmosphère et rendu final selon le suivi TW.

Journaux : `.tidewater-integration/raster-project-world-tests.log`,
`raster-clippy.log`, `plant-probe-clippy.log`, `plant-probe-native-final.log`.
Échecs intermédiaires conservés pour diagnostic. Deux serveurs Vite appartenant
au chantier arrêtés après capture ; onglet de référence fermé ; prototype terminé.

## Deuxième incrément : silhouettes et canopée lointaine

Les exports `source-008` et `source-009` sont identiques (manifest compris) :
207 fichiers de données, 65 383 instances. L'extraction instancie désormais la
classe `Vegetation` originale, vérifie son scatter contre l'extraction précédente,
et conserve ses six groupes runtime, matrices float32, iPos/iDat et rayons de
requête. Le validateur compare les 65 383 positions et paramètres encodés aux
records source, y compris type de plante dans le seed et hauteur des bananiers.

Les deux atlas d'imposteurs originaux 3840×768, leurs 12 mipmaps, les géométries
proche/lointaine et les réglages LOD de l'application sont extraits. Bake GPU
sans erreur de validation ; intégrité, dimensions, finitude et indices vérifiés.
Le checkout source reste propre. La répétabilité concerne l'adaptateur courant.

Comparaison FHD des masques noir/blanc, seuil 127, temps 2 s, frame 124, mêmes
caméras. L'oracle utilise les générateurs, matériaux et renderer source ; seule
la sortie de couleur est remplacée par une couverture blanche. Le probe Bevy
désactive MSAA pour ce contrôle et conserve vertex/discard originaux.

| Cas | Intersection / union | Pixels différents | Au-delà de 1 pixel |
| --- | ---: | ---: | ---: |
| Feuillu proche | 99,358935 % | 522 | 1 |
| Palmier proche | 98,994232 % | 687 | 28 |
| Arbuste proche | 99,418188 % | 73 | 0 |
| Feuillu lointain | 100 % | 0 | 0 |
| Canopée entière lointaine | 99,988457 % | 16 | 1 |

Rapports locaux : `tree-coverage-001.json`, `palm-coverage-001.json`,
`shrub-coverage-001.json`, `tree-far-coverage-001.json`, `canopy-coverage-001.json`.
Le seuil d'un pixel est un diagnostic, pas une tolérance approuvée. Les écarts
proches restent à diagnostiquer ; cette mesure ne couvre ni lumière, ombres,
transitions LOD, mouvement, ni rendu WebGPU de Forge.

La fixture `canopy-probe-001` reprend les 18 343 records arbres/arbustes dans un
seul mesh de 36 686 triangles. Les quatre sommets de chaque carte transportent
les iPos/iDat originaux ; le shader conserve sélection de vues, réduction de
densité et suppression des arbustes à distance. Tous les records sont soumis,
mais les règles source n'en rendent pas tous visibles. Cette expansion de
cartes est un diagnostic borné, pas l'instanciation de production.

Captures `native-tree-far-002.png` et `native-canopy-001.png` inspectées : formes
et disposition insulaire visibles, éclairage provisoire sombre et fond uni.
Le masque de canopée a 138 607 pixels couverts contre 138 603 source ; 16 pixels
diffèrent. SHA source : `6267383777278c31b6400054f2cda32ff4c83ae5202d4c14a57072d99f628110` ;
SHA natif : `9b40dffef6af5a241e45900a17bb2909d982791f92c17f5f16a1d98e74bff863`.

Clippy strict du probe réussi après extension. Régression du feuillu proche :
`native-tree-mask-regression-001.png` identique au masque précédent, SHA
`49be5ddcbfcb3b7c66ec0bc3a13debeb4e78a2c7f75571477e7ac556f36021ae`.
Les builds/probes sont terminés ; aucun nouveau serveur permanent lancé.

La zone éditable Studio, l'instanciation de production, le terrain rendu,
les autres groupes de plantes, le village et les effets restent à intégrer.
Aucune ligne TW n'est fermée et aucune parité globale n'est revendiquée.
