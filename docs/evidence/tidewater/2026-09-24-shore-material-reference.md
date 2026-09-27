# Référence du rivage simulé : eau, écume et sable

L'acquisition couvre maintenant le matériau WaterMaterial avec ShoreSim et
SurfFoam ainsi que le traitement du sable mouillé installé par App.js. Cette
étape prépare leur raccordement à l'état GPU calculé par Forge ; elle ne prouve
pas encore un rendu natif de ces matériaux supplémentaires.

## Acquisition reproductible

`export-water-material.mjs SOURCE NEW_DIRECTORY --shore-sim` construit le
village avant le champ côtier, puis les ShoreWaves, ShoreSim et SurfFoam
originaux. Le module de surface reçoit le même fournisseur `foamShading` que
l'application. `shore-reference-hooks.mjs` lit la fonction `terrainWetness`
directement dans App.js au pin déjà contrôlé ; aucune nouvelle formule n'est
substituée. Elle conserve humidité, résidus et bande humide hors région simulée.

`water-shore-sim-source-001/002` : **64 fichiers et manifeste identiques**,
16 modules, 11 mips d'écume optique et 10 mips de dentelle. Les modules
`shoreSim` et `surfFoam`, l'uniforme ShoreSim de 48 octets et les bindings
d'état/dentelle sont présents. Les dix mips de dentelle, le champ côtier et
l'uniforme sont identiques à ceux de `shore-sim-source-003`.

La référence reste figée sur
`1438b1abfcaee3267092b75573014f4d9b4a983c`, relief après village
`be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.
Le repli original `dirOn=0` est conservé. Les embruns et leurs dépôts restent
désactivés, comme dans la comparaison numérique précédente.

## Référence visible appariée

`render-source-vegetation.mjs ... --clear-sky --water-shore-sim` raccorde l'état
aux matériaux originaux du terrain et de l'eau, puis avance FFT et ShoreSim
pendant 120 pas de 1/60 s depuis zéro. Le temps accumulé de la simulation est
distinct de l'heure solaire. Le rendu conserve les mêmes caméra, éclairage,
ombres, géométrie et paramètres que la capture sans simulation.

La texture d'état réellement utilisée est relue et enregistrée avec le PNG.
Dans les deux vues, elle est identique bit à bit au pas 120 de l'oracle
numérique autonome :
`1f2867f23614172dac613e717ffb17c7bf604d5fba39f9c9f3405ea80e2b349e`.
Le manifeste de capture enregistre ce hash et la configuration du calcul.

| Vue source | Pixels changés de plus de 3 /255 | Variation RGB moyenne /255 |
|---|---:|---:|
| Village vers la mer | 216249 | 1,142233 |
| Île | 77066 | 0,304486 |

Il s'agit de l'effet de l'activation source, pas d'une erreur source/natif.
Les PNG ont été inspectés : mousse blanche plus étendue dans le ressac,
traitement du film d'eau et sable humide. La répétition plage reproduit
exactement le PNG, le Frame et la texture d'état ; l'île n'a pas été répétée.

- `source-water-shore-sim-002.png` :
  `a0d67c9cc101a453fb137d4af4c1f6f279bb3c1030a66d0405fea3e417f68423`.
- `source-water-shore-sim-island-001.png` :
  `b5fa926b20bff382817961044d02576f4b597e41336559647c730af851e15903`.
- Rapports `shore-material-verification-001.json` et
  `shore-material-island-verification-001.json` produits par
  `verify-shore-material.py`.
- La première capture plage `001` a les mêmes données, avant ajout du résumé
  ShoreSim dans son manifeste ; `002` est la référence avec métadonnées complètes.

Le mode sans simulation est rejoué avec le script modifié : PNG et Frame
restent identiques à `source-water-shore-001.png`. Rapport
`shore-material-source-regression-001.json`, exécution code 0. Aucun test
Rust n'est revendiqué pour cet incrément d'outillage source.

## Raccordement natif restant

Publier la texture d'état du calcul ShoreSim existant comme Image Bevy partagée
avec les matériaux, attendre son pas apparié avant la capture, puis ajouter les
fonctions originales de sampling/SurfFoam et le hook terrain. Le rendu doit lire
le résultat natif sur le même GPU, sans importer les états de référence.
LaceNearest et SurfFoam partagent la même base de dentelle ; le raccordement
devra préserver sampling/mips et respecter les limites de ressources WebGPU.

Les captures sont fixes à 2 s, sans chaîne HDR/temporelle, embruns ou immersion.
Elles ne terminent ni la parité animée, ni Studio, ni le package autonome.
Tous les critères TW complets restent ouverts. Aucun changement Rust ni
publication dans cet incrément. Artefacts sous
`E:/OpenRPG_Forge/.tidewater-integration/`.
