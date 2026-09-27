# Tidewater — occlusion ambiante du relief corrigé

Le calcul Rust reprend `TerrainBake.js` : moyenne 4×4 du raster en f32,
recherche d'horizon dans huit directions et neuf distances jusqu'à 100 m,
interpolation de la grille d'AO et terme de cavité à deux mètres. Les calculs
intermédiaires suivent la précision JavaScript et les frontières Float32Array.
Le canal alpha dérivé rejoint la texture de normales partagée. Les onze passes
GPU filtrent maintenant RG+A ; le masque roche B reste conservé.

Le déclenchement couvre les corrections locales et l'élévation globale, car
le seuil source à -25 m influence l'AO même si les pentes restent similaires.
Sans retouche, les données et mipmaps originaux restent utilisés.

## Vérifications exécutées

Racine : `E:/OpenRPG_Forge/.tidewater-integration/`.

- `tidewater_terrain_ao_probe` : zéro différence sur les 4194304 texels AO source.
  Hash `3204d03a81e44f8222793f72f35c7f90e987725fc073141b295b591b701d8ffe`.
  Rapport `terrain-ao-source-verification-001.json`.
- `verify-authored-ao.mjs` appelle la fonction pure du fichier JavaScript épinglé
  et vérifie son hash. Seul son import inutilisé par ce bake est retiré ; les
  expressions du calcul source restent intactes.
- Bosse locale : 35248 texels AO changent, hash exact commun JS/Rust
  `b62ef58f83738f64479ef8e523e361018c14431e58fbb88d8205769d365d74b8`.
  Le hash RGBA complet concorde également. `relief-ao-render-proof-001` contient
  les captures inspectées et les rapports. Les sorties des deux systèmes d'ombre
  restent identiques au test précédent, l'image évolue sur 23314 pixels
  (écart maximal20/255), et Undo restitue exactement la référence FHD.
  Binaire : `b47744be1df55a6bcebfdc7dc6aff986e04ba770bc99ce7349b70ff3433edf23`.
- Élévation globale +3 m : `authored-relief-states-002/global.json`, adopté dans
  `workspace-relief-global-001`, puis rendu dans `native-global-ao-001.png`.
  192247 texels AO modifiés, hash exact commun JS/Rust
  `4fb8367f336af33473716ff5abc68d31a15abe23f3407f07fb255ea1cc20059d`.
  Les deux passes annoncent 65383 plantes déplacées ; les onze passes de mipmaps
  s'exécutent. `global-ao-native-verification-001.json` conserve les preuves.
  Binaire final : `86c251012786b7339550abcde1ea974c2fd85b986f9b4de008a083aea27e0b67`.

Compilation et Clippy strict des exemples natifs réussis. Les captures sont
des essais de rendu de zones sauvegardées ; aucune édition Studio interactive
n'est revendiquée.

## Limites

Roche, splats, côte, eau, herbe, bâtiments et collisions ne suivent pas encore
toutes les corrections. L'essai global met notamment en évidence le besoin de
repositionner les bâtiments et recalculer la côte ; il ne constitue pas une
qualification de ces éléments. Les mipmaps utilisent le filtre GPU source mais
l'égalité par niveau reste séparément ouverte. La parité complète et les autres
parcours du plan ne sont pas terminés. Aucun commit ni publication.
