# Vagues de rivage et nappe de ressac

Le matériau natif exécute désormais ShoreWaves avec WaterSurface/WaterMaterial :
propagation suivant le relief, variation des crêtes, déferlement, normales,
écume immédiate, nappe de remontée sur le sable et milieu trouble du rivage.
Il s'agit des fonctions WGSL originales, dans la configuration sans ShoreSim,
SurfFoam, Breakers ou Spray. La simulation persistante du sable mouillé et des
dépôts d'écume reste à réaliser.

## Données et intégration

`export-water-material.mjs --shore` construit d'abord le village puis calcule
le champ de propagation 512² avec la direction WORLD.swellDir d'origine.
Les 49 fichiers et le manifeste sont identiques entre `water-shore-source-001`
et `002`. Les 1 048 576 valeurs du champ sont finies. Ses temps 100000 sont
les sentinelles d'origine et sont conservés.

- Relief : `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`,
  identique à la carte canonique après village.
- Champ RGBA32Float :
  `573846d4d3bfb0145951b9b560ccfa8d2b2cf6cb64a2f19d1866895cfe6ef833`.

La préparation vérifie la correspondance du relief, de la caméra et des options
source/natif. Le matériau lie le champ, les 64 octets ShoreParams et le placeholder
de direction original ; dirOn=0 utilise le champ terrain. Le cache de direction
de ShoreSim n'est pas utilisé. Le champ est encore une donnée acquise : son
recalcul Rust après une retouche Studio reste ouvert.

## Comparaisons

Artifacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.
Deux caméras, temps 2 s, spectre par défaut après 120 pas.

| Vue | Écart RGB moyen image /255 | Zone affectée : moyenne /255 | P99 zone | Maximum zone |
|---|---:|---:|---:|---:|
| Village vers la mer | 0,1775175 | 0,2399666 | 5 | 113 |
| Île | 0,1478660 | 0,1823442 | 2 | 62 |

La zone affectée provient de la différence source avec/sans ShoreWaves,
seuil supérieur à 3 puis dilatation d'un pixel. Elle couvre respectivement
447 579 et 333 121 pixels, dont 404 785 et 174 639 changent au-delà du seuil.
Cette mesure inclut la plage nouvellement recouverte ; les anciens masques
blancs d'océan servent seulement aux métriques complémentaires.
Les maxima de l'image entière sont 127 et 135 : aucune égalité visuelle exacte.

- Sources `source-water-shore-001.png`, `source-water-shore-island-001.png`.
- Natifs `native-water-shore-002.png`, `native-water-shore-island-001.png`.
- Fixtures `water-shore-seaward-002`, `water-shore-island-001`.
- Rapports `shore-effect-comparison-001.json`,
  `shore-effect-island-comparison-001.json` et `water-shore-*-comparison-001.json`.
- PNG natif plage inspecté :
  `8b575f9aae18e3a8e72bd4f214245f078d48fa05fa2fa9c6774bea2c935b5131`.
- PNG natif île :
  `78f292b2d83d49999a427a3ea77dd15be3f2f98df601797fd28b079dc5d4bde4`.

## Régressions

La répétition du rendu de plage est identique. La régression sans ShoreWaves
reproduit exactement le PNG optique précédent. Les 21 sorties FFT de chaque
capture sont identiques à `native-ocean-default-fft-frame120-003`.
Rapport `water-shore-regression-001.json`.
Compilation `water-shore-build-001.log` et Clippy `-D warnings`
`water-shore-clippy-001.log` réussis ; captures finales code 0.
Le premier essai shader, refusé pour une dépendance terrain manquante, est
exclu des preuves ; les fonctions originales de normale explicite ont été ajoutées.

Binaire : `48cc386f76cbc3b119841e2eb3437f467cd7764adb407ac58728427a80008117`.

## Limites

La comparaison reste statique, en 8 bits sRGB et sans chaîne HDR/temporelle.
Elle n'exerce pas un cycle complet de montée/retrait. ShoreSim, SurfFoam,
la lèvre séparée des déferlantes, les embruns, les sillages et les détails de
vent restent ouverts. Les nuages, l'immersion, les paramètres interactifs,
Studio et la qualification WebGPU/performance restent également à terminer.
Tous les critères TW complets restent ouverts, aucune publication.
