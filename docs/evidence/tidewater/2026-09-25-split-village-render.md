# Tidewater — premier rendu natif du village séparé

`prepare-split-village-scene.py` transforme les 121 plages statiques en
maillages compacts. Chaque triangle conserve exactement ses attributs indexés.
Les 24 autres nœuds restent présents. Le descripteur conserve l'appartenance
des 111 morceaux aux 23 bâtiments ; les morceaux de décor restent distincts.
La scène résultante contient 145 nœuds et 210543 sommets statiques compacts.

Le chargeur natif accepte ce descripteur v2 borné et conserve le contrat v1.
Le parcours d'ombres reconnaît toujours les sept catégories sources, mais
accepte plusieurs morceaux par catégorie. Les matériaux et shaders sont les
mêmes que pour les lots fusionnés.

## Vérifications

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- `split-village-scene-001` construit depuis la partition qualifiée et la
  fixture workspace. Les sources sont vérifiées par hash ; les fichiers
  inchangés sont liés physiquement et seuls les nouveaux payloads sont écrits.
- Premier essai interrompu avant rendu par une ancienne assertion imposant
  sept dessins d'ombre. Le contrôle a été corrigé pour les morceaux séparés.
- Compilation et Clippy strict passent pour la scène et le probe d'ombres.
- `native-split-village-002.png` inspectée et comparée à
  `native-merged-village-current-001.png`, même binaire, caméra, temps et zone.
  Aucun ERROR GPU. Les tableaux d'ombres proches et lointaines sont identiques.
- `split-village-render-proof-001.json` : 14 pixels différents, maximum 2/255,
  rectangle x=1742..1750, y=453..460, sur les embruns à droite. Tout le reste
  de la capture est identique. Les particules actives sont identiques comme
  multiensemble ; leurs slots diffèrent. Cela est compatible avec la variation
  d'ordre atomique et de mélange transparent déjà observée, sans prouver à
  lui seul la cause de chaque pixel. L'image complète n'est donc pas déclarée
  bit-identique.
- Binaire : `ff7d139bc9cabcc19b2071dcead69ec92197c0ea30f5c1b8a0d62efbf6246451`.

## Limites

Le rendu séparé fonctionne dans le prototype. Les identités de bâtiment sont
encore des métadonnées de préparation ; pivots, transformations canoniques,
sélection et commandes Studio restent à raccorder. La qualification de
performance et les autres angles/transparences restent ouverts : 145 nœuds
ne constituent pas une mesure de coût GPU. Les données de zone proviennent
du workspace, mais les pièces du village restent une fixture de rendu.
Aucun commit ni publication ; parité globale toujours incomplète.
