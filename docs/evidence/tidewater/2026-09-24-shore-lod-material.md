# ShoreSim — correction du matériau de terrain CDLOD

Le terrain CDLOD affiché utilisait encore les textures provisoires de son
matériau de base. Le raccordement précédent modifiait `Assets<TerrainMaterial>`,
alors que `terrain_lod::setup` avait déjà copié ce matériau dans
`ExtendedMaterial<TerrainMaterial, LodExtension>`. L'eau recevait correctement
ShoreSim ; le terrain affiché ne recevait ni son image ni ses paramètres.

`water_optics::setup` raccorde maintenant les deux collections de matériaux à
la même image GPU, à la même dentelle et aux mêmes paramètres. Aucun changement
du calcul, des données acquises ou des formules originales n'est nécessaire.
La précédente affirmation d'un raccordement visible complet du sable humide
est donc corrigée par cette preuve.

## Isolation et contrôle direct

Les diagnostics originaux du matériau d'eau sont exposés par
`--water-debug=2|3|11`. La préparation vérifie et copie le bloc matériau apparié
de 80 octets. Les sorties des normales et de couverture d'écume étaient déjà
proches sur les 430 517 pixels du masque intérieur d'eau : erreurs moyennes
respectives 0,055276 et 0,001219 /255. Ce masque exclut cependant une partie de
la plage ; il ne suffisait pas à localiser l'écart complet.

`--terrain-debug=wetness` affiche directement la fonction originale
`terrainWetness` en RG, des deux côtés. Sur les 9 699 pixels classés par la
sortie source R>50/B=0, l'erreur moyenne passe de 20,528955 à 0,003952 /255,
le P99 de 251 à 1 et le maximum de 255 à 2. Il s'agit d'un masque de couleur
diagnostique, pas d'identifiants d'objets. Les captures ordinaires restent
indispensables à la qualification.

Les essais d'arrondi du hash SurfFoam et de dérivées Fine/Coarse n'ont pas
modifié le PNG. Ils ne constituent pas des corrections et ne sont pas retenus
dans le shader produit. Le premier essai de dérivées `001` est exclu : erreur
de décodage UTF-8 lors de la préparation ; les essais effectifs sont `002`.
DX12 seul n'avait pas corrigé l'écart de contact avec la plage.

## Deux vues après correction

Comparaison à 2 s, avec les mêmes masques d'effet source que précédemment.

| Vue | Moyenne région avant → après /255 | P99 après | Maximum après | Moyenne image après /255 |
|---|---:|---:|---:|---:|
| Plage | 1,276578 → 0,259991 | 7 | 102 | 0,174671 |
| Île | 0,474857 → 0,124949 | 2 | 62 | 0,146891 |

Les deux captures corrigées sont inspectées. Les écarts locaux restants
interdisent encore un verdict d'égalité exacte.

- `native-shore-lod-fixed-001.png` :
  `9561dcdf2bc7b347e49e1511ce9d746be17de3ea204507012fc589be67aa40f1`.
- `native-shore-lod-fixed-island-001.png` :
  `b20bd9930c75d002e3ebe4ab623a398698a869c1a4f7bb184a0f7b36bdfb85df`.
- Rapports `shore-lod-fixed-comparison-001.json` et
  `shore-lod-fixed-island-comparison-001.json`.

## Régressions

Compilation et Clippy ciblé `-D warnings` réussis :
`shore-lod-material-build-001.log`, `shore-lod-material-clippy-001.log`.
Binaire scène :
`b1dd0e7784ccbdc20c9cb5fd262fa0ab13aa933def3aef811e2478633790ac63`.

`shore-lod-fixed-regression-001.json` vérifie :

- référence ordinaire après ajout des diagnostics : PNG, Frame et état exacts ;
- fixture nouvellement préparée et répétition native : PNG corrigé exact ;
- anciens modes ShoreWaves seul et optique seule : PNG précédents exacts ;
- six captures : 21 fichiers FFT chacune identiques au calcul qualifié ;
- quatre captures simulées : six fichiers ShoreSim chacune identiques au
  calcul natif autonome, y compris l'image publique relue.

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`. Aucun commit/push.
Le temps reste figé après 120 pas ; effets manquants, animation interactive,
Studio, WebGPU et package autonome restent ouverts. Tous les critères TW
complets restent ouverts.
