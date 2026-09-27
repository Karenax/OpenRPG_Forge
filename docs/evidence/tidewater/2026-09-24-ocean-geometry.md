# Surface océanique déplacée dans la scène Bevy

La scène dessine maintenant le maillage CDLOD mobile avec les fonctions
WaterSurface d'origine et les déplacements FFT calculés sur son device Bevy.
La couleur blanche sert exclusivement au contrôle de silhouette : le matériau
optique de l'eau reste à intégrer. Référence figée : `1438b1ab`.

## Implémentation

`prepare-ocean-geometry.py` compose les fonctions source de relief, CDLOD,
atténuation et déplacement. Le matériau natif lie le relief réel, les paramètres
natifs du spectre et la texture de déplacement à quatre couches et neuf mips.
La sélection des tuiles utilise la caméra courante et les bornes verticales
[-25, 25]. Le prototype développe les sommets par tuile ; ce n'est pas encore
le chemin d'instanciation produit. La surface est double face et écrit sa profondeur.

Le mode `--ocean-geometry OCEAN_EXPORT` conserve terrain, végétation, village,
ombres et ciel clair. Il attend les 120 pas FFT et leurs lectures avant capture.
La référence JavaScript exécute indépendamment les mêmes fonctions sur son
device, avec 120 soumissions. Les sélections ordonnées sont comparées.

## Résultats

Artifacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.

| Vue | Tuiles exactes | Pixels de silhouette différents | IoU du masque blanc | Écart RGB moyen /255 |
|---|---:|---:|---:|---:|
| Île | 61 | 1 | 0,9999992974 | 0,0371644 |
| Village vers la mer | 132 | 14 | 0,9999682886 | 0,1272791 |

- Île : `source-ocean-geometry-island-001.png`,
  `native-ocean-geometry-island-002.png`,
  `ocean-geometry-island-comparison-002.json`.
- Mer : `source-ocean-geometry-seaward-001.png`,
  `native-ocean-geometry-seaward-004.png`,
  `ocean-geometry-seaward-comparison-004.json`.
- Les 21 sorties FFT de chacune des scènes correspondent bit à bit à
  `native-ocean-default-fft-frame120-003`.
- La régression `--sky` reproduit exactement `native-sky-island-003.png`,
  zéro pixel différent. Rapport `ocean-geometry-regression-001.json`.
- Compilation `ocean-geometry-build-final.log` et Clippy des deux exemples
  avec `-D warnings`, `ocean-geometry-clippy-002.log`, réussis.
- Une fixture de géométrie lancée avec `--sky` seul est refusée avant rendu,
  code 1, aucune image créée (`ocean-geometry-invalid-mode-001.log`).

SHA-256 du binaire scène :
`a3aeeee9af11fffff50e0651a395c667faf65e465ee46a21dc8ca8eb0c5d1a9f`.
PNG île :
`1f7098373909cc883e275ed82a20aa9ca469f48392e200afea7f5bc8e85bac6b`.
PNG mer :
`b86de38f975e64565b32141b02b955189f870c2083dddaa5aba3c973b437c83d`.

## Corrections et portée

Le premier essai vers la mer a correctement refusé l'oracle d'herbe de la
caméra tournée vers l'intérieur. `grass-seaward-source-001` réexporte les
cellules originales pour cette orientation (7/12/0). La fixture finale
`ocean-geometry-seaward-002` conserve les mêmes données visuelles et remplace
l'oracle de caméra après contrôle des hashes. Le garde-fou reste actif.
La référence source calcule directement ces cellules ; son image reste valable.
La vue village tournée vers l'intérieur, sans eau visible, est exclue des preuves
de silhouette océanique. Les captures natives finales terminent avec code 0.

Le masque blanc est une classification de couleur, pas un identifiant d'objet.
La seconde caméra est un diagnostic supplémentaire, pas une des six baseline.
Les deux images natives ont été inspectées. Cette mesure porte sur une scène
fixe, au temps 2 s, sans vagues de rivage, sillages, détail de surface, simulation
de côte ou écume de plage. L'optique, les nuages, le rendu temporel, les mises
à jour interactives et la qualification navigateur restent ouverts. Aucune
parité complète, performance ou édition Studio n'est acquise ici.
