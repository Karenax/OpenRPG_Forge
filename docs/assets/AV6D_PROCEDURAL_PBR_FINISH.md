# AV6-D — finition PBR procédurale

Dernière mise à jour : 5 août 2026.

## Résultat

Le terrain de Asset Vault ne dépend plus d’un matériau plat limité à la couleur vertex. Le
Creator et le runtime partagent `GodotTerrainMaterialAdapter`, qui reconstruit un matériau
`procedural-pbr-vertex-blend` unique pour les 64 secteurs.

Le shader conserve les quatre couleurs de matériaux canoniques et ajoute en espace monde :

- quatre octaves de variation macro et une variation de détail ;
- modulation mousse/terre selon humidité déterministe ;
- désaturation rocheuse selon la pente ;
- litière discrète, roughness, metallic, specular et occlusion ;
- continuité parfaite entre secteurs grâce aux coordonnées monde.

Les deux routes ne sont plus composées de boîtes juxtaposées. Un ruban triangulé continu est généré
depuis chaque suite de points de contrôle, suit l’altitude canonique et reçoit un matériau de terre
PBR distinct avec grain et centre usé.

## Environnement et revue visuelle

Asset Vault emploie désormais un ciel physique, une énergie de ciel abaissée, une lumière
directionnelle moins lavée et un brouillard deux fois moins dense, repoussé vers l’horizon.

Les captures Forward+ aérienne et rapprochée ont été inspectées à chaque itération. Une couche
`patch-dirt` dispersée a été rejetée parce que ses rectangles étaient visibles. SSAO et SSIL ont
également été retirés après apparition de blocs transitoires en capture. Ces essais ne font donc pas
partie du résultat livré.

Preuves locales :

- `artifacts/verification/asset-vault-av6d-world-forward-plus.png` ;
- `artifacts/verification/asset-vault-av6d-close-v7-forward-plus.png` ;
- marqueur `OPENRPG_FORGE_AV6D_WORLD_VISUAL_PASS` ;
- smoke terrain : 16 641 vertices, 32 768 triangles, 64 secteurs, quatre couches de matériau,
  5 564 instances de biome et 32 454 polygones navigables.

## Limites explicites

AV6-D améliore nettement la lisibilité du sol et des routes tout en restant une direction stylisée.
Il ne remplace pas une passe artistique AA avec textures scannées, davantage de silhouettes de
végétation, variations saisonnières, eau avancée et habillage rapproché propre à chaque secteur. Le
retargeting entre rigs incompatibles reste lui aussi séparé.

La passe suivante de variété et d’habillage est documentée dans
[`AV6E_NATURE_DRESSING.md`](AV6E_NATURE_DRESSING.md).
