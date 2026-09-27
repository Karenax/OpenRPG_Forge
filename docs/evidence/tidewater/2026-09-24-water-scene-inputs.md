# Entrées de scène du matériau d'eau

Le prototype d'océan lit désormais la couleur et la profondeur de la scène
opaque avant le dessin de l'eau. Cette étape prépare les réflexions à l'écran
et la réfraction du matériau original. Elle ne constitue pas encore un rendu
optique de l'eau.

## Raccordement natif

`water_scene.rs` copie la profondeur Depth32Float après la passe opaque et
avant la passe transmissive, sur le même device Bevy. La surface utilise
la texture de transmission Bevy pour la couleur. Le matériau écrit toujours
sa profondeur, reste double face et passe maintenant dans la phase transmissive.
La caméra autorise COPY_SRC sur sa profondeur. La cible de ce prototype est
fixée à 1920 × 1080 ; une autre dimension est refusée explicitement.

Les modes `--ocean-scene-color` et `--ocean-scene-depth` affichent ces entrées
uniquement sur la surface océanique. La profondeur est multipliée par 100
puis encodée dans la capture sRGB. La référence exécute deux passes source
et copie ses propres textures entre les deux ; elle ne charge aucune image
native pour ces calculs.

## Comparaison indépendante

Artifacts sous `E:/OpenRPG_Forge/.tidewater-integration/` ; fixture finale
`water-scene-seaward-002`, caméra village tournée vers la mer.

| Entrée | Écart moyen sur l'image /255 | Écart moyen intérieur eau /255 | P99 intérieur | Maximum intérieur |
|---|---:|---:|---:|---:|
| Couleur opaque | 0,1396814 | 0,0583345 | 1 | 17 |
| Profondeur ×100 | 0,1401100 | 0,0607549 | 1 | 3 |

L'intérieur contient 430 517 pixels : masque blanc de la référence géométrique,
érodé d'un pixel pour séparer les discontinuités de silhouette. L'image entière
reste mesurée ; son maximum est 127 dans les deux comparaisons. Le masque
est une classification de couleur, pas un identifiant d'objet. La capture 8 bits
ne prouve pas l'égalité des profondeurs flottantes brutes.

Rapports `water-scene-color-comparison-001.json` et
`water-scene-depth-comparison-001.json`. Captures natives inspectées :
`native-water-scene-color-001.png` et `native-water-scene-depth-002.png`.
Sources correspondantes `source-water-scene-color-001.png` et
`source-water-scene-depth-001.png`. Le correctif entre les fixtures 001 et 002
renomme un identifiant WGSL réservé ; il ne modifie aucune donnée de scène.
La première capture native refusée pour ce shader est exclue des preuves.

## Acquisition du matériau original

`export-water-material.mjs` préserve 13 modules, les snippets vertex/fragment,
les déclarations/uniformes de construction et le kernel de texture d'écume.
Les onze mips RGBA16Float de l'écume 1024² sont calculés par Tidewater.
L'instrumentation ajoute uniquement COPY_SRC à cette texture pour la lecture.
Le clone source reste intact au commit `1438b1ab`.

`water-material-source-001` et `002` reproduisent exactement les 45 fichiers
et le manifeste. Dimensions et valeurs finies des onze mips vérifiées.
Leurs uniformes sont les valeurs de construction, pas les paramètres d'une
baseline complète. Le TerrainGPU de cet export n'inclut pas l'aplanissement
du village : ses fonctions sont acquises, mais ses données ne doivent pas
remplacer le relief canonique de la scène.

## Régressions et limites

- Les captures géométriques source et native après séparation des passes sont
  identiques à leurs captures précédentes, zéro pixel différent.
- Les 21 sorties FFT des trois captures natives correspondent bit à bit à
  `native-ocean-default-fft-frame120-003`.
- Rapport consolidé `water-scene-regression-001.json`.
- Compilation `water-scene-build-002.log` et Clippy `-D warnings`
  `water-scene-clippy-001.log` réussis. Les trois rendus natifs réussissent.
- Binaire scène SHA-256 :
  `bb73138b45803dcc4445e1db063e4c429b294739bfae0f6c609d9e2e927beacf`.

Les fonctions Fresnel, absorption, diffusion, normales et écume originales
restent à raccorder aux entrées natives. La passe de réfraction séparant
les objets immergés, la réflexion planaire, les nuages, les vagues de rivage,
les sillages, l'immersion et le rendu temporel restent ouverts. Ce diagnostic
fixe ne qualifie ni le chemin produit, ni Studio, ni les performances/WebGPU.
