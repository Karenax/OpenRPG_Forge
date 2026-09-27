# Ombres des objets — caméras natives et oracle GPU source

Le module moteur `sun_cascades` porte le calcul et l'ordonnancement des
caméras de `Shadows.js` au commit `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Il ne génère pas encore les cartes de profondeur natives et n'est pas encore
raccordé au rendu de la scène. Aucune parité d'ombre native n'est déclarée.

## Calcul Rust vérifié

- Trois cascades par défaut, 2048², séparations 10 / 60 / 400 m ; volumes
  ajustés aux tranches du frustum, rayons quantifiés au seizième de mètre,
  alignement sur les texels, bandes de mélange et biais normaux source.
- Actualisation toutes les 1 / 2 / 4 images, forçage après mouvement du
  soleil ou invalidation des objets, désactivation sous l'horizon.
- 89 états source : caméra fixe, déplacements sous-texel, rotations, FOV et
  aspect variables, soleil vertical et seuils, désactivation/réactivation.
  Les listes de cascades actualisées et leur activation sont identiques.
- 20 025 scalaires comparés ; erreur absolue maximale
  `2.8421709430404007e-13` (tolérance explicite `1e-9`).
- Premier essai rejeté : le port réinitialisait la translation avant
  alignement. `Matrix4.lookAt` source conserve la position précédente ;
  cette dépendance temporelle est maintenant reproduite.
- 18 entrées invalides rejetées. Les requêtes rejetées ne décalent pas
  l'ordonnancement et ne remplacent pas les matrices valides. Une invalidation
  force les trois couches. Clippy strict et exécution des deux vérificateurs
  réussis.

## Oracle source des cartes de profondeur

Le mode `--shadows=ENV_EXPORT` de `render-source-vegetation.mjs` garde les
castShadow d'origine. Il conserve la scène de diagnostic précédente (herbe,
terrain, village sans poissons et six groupes végétaux ; environnement froid,
t=2). Il exporte les trois couches depth32 à travers une copie compute
identitaire vers R32, car la texture d'origine n'a pas COPY_SRC.

Deux exécutions indépendantes donnent les mêmes images et profondeurs.
La troisième corrige seulement l'inventaire du nombre d'instances.
Image inspectée : maisons, palmiers et objets projettent leurs ombres.

| Couche | Texels de profondeur < 1 | SHA-256 |
|---|---:|---|
| 0 | 1 392 702 | `3bd45f5bc8a3356cfc5da914b065887f74d710bed472b0dca79cfe86bf4140ca` |
| 1 | 2 589 067 | `6fff950b513524977ab72e83500f6974d9e4ebb661afeb504a3accaacb16bdf5` |
| 2 | 1 785 788 | `27d6071d0932b5bea6ccde615cae0000d4f2f15d04790a9cdc2b36c79e87a39c` |

Neuf ensembles projettent : cinq groupes végétaux proches, terrain, opaque du
village et deux morceaux d'enseigne. L'herbe, les imposteurs lointains,
sous-bois, tissus, filets et lanternes ne projettent pas. Le callback
`village_wood` utilise tous les indices opaques pendant la passe d'ombre,
puis retrouve son sous-ensemble au rendu couleur. Porter ce comportement,
les masques végétaux, biais 2/1,5 et le filtre PCSS/PCF est encore nécessaire.

## Fichiers de preuve

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `sun-cascades-oracle-001.json`, SHA-256
  `c27c85c768ae8c92694c3878665f6154d1d9054ce5b182cbd66fedc4fcba3873`.
- `sun-cascades-verify-002.log`, `sun-cascades-clippy-004.log`,
  `sun-cascades-guards.log`, `sun-cascades-guards-clippy-002.log`.
- `verify_sun_cascades.exe` SHA-256
  `94a43c10d61d304b4503d425652bb49a1bbe78fbb82e47a71d54c3f948584cbd`.
- `source-object-shadows-004.png`, `.shadows.json`, `.shadow-filter.wgsl`
  et `.cascade-{0,1,2}.r32.bin`. Image SHA-256
  `c6361d3e942495f60e2894a6eba1da56d36b46d8d219f7ff87b8dfa48fcdecea`.
- `object-shadow-repeat-001.json` compare les captures 002/003. L'essai 001
  est rejeté : erreur WGSL dans la copie de diagnostic ; ses fichiers partiels
  sont conservés mais inutilisables.
- Capture 004 : profondeurs et image identiques à 003 ; inventaire d'instances
  complété. Régression `source-shadows-disabled-regression.png` identique à
  `source-grass-scene-002.png` (`70b5a554c19b153ebe36f0eab90ff5975d959dd344a4c3012840b4090d4962eb`).
- Vérification WASM/WebGPU réussie : `cargo check -p forge_engine --target
  wasm32-unknown-unknown --no-default-features --features web -j1`, journal
  `sun-cascades-wasm-check.log`, 12 min 07 s. C'est une compilation de la
  bibliothèque, pas une exécution ni une qualification visuelle navigateur.

Ce lot ne qualifie ni les profondeurs natives, ni le filtrage natif, ni le
parcours animé final. Les travaux Studio, océan, atmosphère et qualification
produit restent ouverts. Aucun passage de gate ni publication.
