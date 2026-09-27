# Terrain importé : document canonique et retouches

## Contrat livré

`TerrainDocument` v4 opt-in référence `SampledTerrainSource` par hash. Les
documents v1–v3 n'émettent aucun nouveau champ quand la source est absente.
L'algorithme raster et la source doivent être sélectionnés ensemble ; version,
domaines, dimensions et pas incompatibles sont refusés. Les paramètres de bruit
inactifs sont normalisés. Les octets manquants ou corrompus ne provoquent aucun
repli vers une génération procédurale.

`SampledTerrainPlan` est un cache dérivé du document canonique. Il applique
l'élévation de base et les `height_deltas` existants sur le raster immuable.
Grille auteur aux coins, raster aux centres : les deux alignements restent
explicites. Les tuiles dérivées sont bornées à 128 intervalles par axe, avec
positions, normales et indices partagés par les consommateurs rendu/collision.

## Contrôles CPU acquis

- `cargo test -p forge_world -p forge_project --locked -j1` : 419 tests réussis,
  journal `.tidewater-integration/raster-v4-tests.log`.
- Trois nouveaux tests : corrections/Undo/Redo/archive/réouverture avec source
  inchangée ; refus version/domaine/algorithme/bytes invalides ; bords et normales
  identiques entre tuiles, winding et quotas.
- Clippy strict toutes cibles de ces deux crates réussi après comparaison des
  bords par bits et usage de `as_chunks`, journal `raster-v4-clippy.log`.
- 1 030 sondes réelles depuis `source-008` : évaluateur du document et lecteur
  brut identiques par bits, erreur maximale face à JavaScript
  `2,842170943040401e-14 m`.
- Document réel généré sans écrasement : `terrain-v4-001.json`, SHA-256
  `f385a0e90d7c74dea6b3dec4c78901d63d3f7e42840e71247f7ceb2499dd4e3b`.
  Source de hauteurs :
  `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.

## Limites

L'archive testée est celle de `TerrainEditSession`, pas encore une archive de
zone autonome contenant le blob et ses pins. Le routage Studio, l'éditeur de
brosses, le runtime produit et Rapier restent à brancher à cet évaluateur.
Le générateur Visual Valley garde son contrat 512 m et refuse cette nouvelle
source au lieu de produire silencieusement un terrain incorrect.

Le probe graphique peut assembler le document v4 et la canopée. Son terrain
gris à grille fixe de 1 m est un contrôle géométrique : il ne remplace pas le
CDLOD ni les matériaux, ombres et effets côtiers originaux. Aucun passage de
gate, aucune parité globale et aucune performance produit ne sont acquis ici.

## Assemblage natif exécuté

Fixture `island-terrain-probe-001`, capture FHD `native-island-terrain-001.png`
inspectée : relief, fonds marins et canopée sont visibles ensemble. 4 096 tuiles
à pas 1 m, 8 388 608 triangles de terrain, plus les cartes de canopée. Les
fonds marins restent visibles sans océan. Éclairage PBR et surface grise
diagnostiques ; aucune comparaison pixel à pixel du terrain avec le CDLOD source.

- Capture SHA : `4f91995f342cf6fd3215a62efc01c24ec29bdac9b6a7ec0a7999987e8566ef5d`.
- Fixture SHA : `ca63771ed9cc1ac048a5e4c64f9d00254c3e2bcfc114cd629eb2a8839987f384`.
- Binaire SHA : `2db7e5ec3a17c9478da6dc7b5de5633ee676f619672e3a1fa3163eb7945353f8`.
- Exécution native terminée avec code 0 ; journal `island-terrain-native-001.log`.
- Clippy strict du probe réussi (`island-terrain-clippy.log`).
- Trois tests ciblés rejoués après correction stylistique : réussis
  (`raster-v4-focused-final.log`).
- Compilation WASM des crates projet/monde réussie (`raster-v4-wasm.log`).
  Cela ne qualifie pas un rendu WebGPU ni l'interface Studio.

Exports `source-010` et `source-011` répétés et identiques : 226 fichiers de
données. Ajout des modules WGSL de terrain et des paramètres originaux, cartes
normales/roche/AO et splat 2048², hauteurs d'ombrage half-float avec pyramide max.
Code source intact, GPU sans erreur de validation, hashes/tailles/finitude des
half-floats vérifiés. Le descripteur exporté liste explicitement les éléments
absents : raccordement à l'humidité du rivage, texture d'ombre solaire animée,
mips GPU normales/splat et nœuds CDLOD sélectionnés par la caméra.

Prochaine intégration graphique : matériau terrain original et rendu adaptatif,
puis océan/atmosphère et village. En parallèle logique de dépendances : résolution
des blobs dans les archives de zone et routage des commandes Studio sur la même
tête canonique. Aucun sous-agent ou traitement permanent n'a été lancé.
