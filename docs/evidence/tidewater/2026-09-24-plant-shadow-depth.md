# Végétation dans les passes de profondeur natives

Le prototype `tidewater_object_shadow_probe --plants` calcule les trois
cascades pour le terrain, le village et les cinq groupes de plantes qui
projettent effectivement une ombre dans la source. Les instances, matrices,
géométries, atlas et fonctions de déformation/masque proviennent des fichiers
vérifiés du commit `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le rendu utilise le device Bevy ; aucune profondeur acquise dans Tidewater
n'est utilisée comme texture de remplacement.

Les imposteurs lointains, le sous-bois et l'herbe ne deviennent pas des
occulteurs supplémentaires. La caméra principale contrôle la sélection et
le LOD, tandis que la caméra de chaque cascade contrôle la géométrie vue
depuis la lumière. Les feuilles conservent leur masque et le rendu double
face. Le vent reste celui du diagnostic fixe (direction 1/0, force 6,
temps 2, rafale 0) ; ce travail ne qualifie pas une animation interactive.

## Sélection et profondeur

Les identifiants **et leur ordre** correspondent exactement à la source :

| Vue | Palmiers | Bananiers | Monsteras | Feuilles larges | Canopée |
|---|---:|---:|---:|---:|---:|
| Village | 65 | 83 | 3 | 48 | 4 |
| Forêt | 0 | 25 | 85 | 0 | 361 |

Les profondeurs ne sont pas identiques. Chaque couche contient 4 194 304
texels. Les métriques suivantes concernent l'ensemble des occulteurs,
terrain et village compris, et non uniquement les silhouettes végétales.

| Vue / couche | Désaccord de couverture | Intersection / union | Profondeurs différentes | Écart maximal normalisé |
|---|---:|---:|---:|---:|
| Village 0 | 1 899 | 0,99863736 | 5 125 | 0,178230 |
| Village 1 | 915 | 0,99964665 | 4 093 | 0,553752 |
| Village 2 | 24 | 0,99998656 | 1 292 | 0,565701 |
| Forêt 0 | 3 596 | 0,99785608 | 8 964 | 0,049422 |
| Forêt 1 | 971 | 0,99964055 | 16 585 | 0,399929 |
| Forêt 2 | 78 | 0,99992761 | 1 121 | 0,640994 |

L'inspection d'un agrandissement de la forêt montre des différences
ponctuelles dans les contours fins des feuilles. Certains texels changent
d'occulteur ou de fond ; les écarts ne se réduisent donc pas à un epsilon
numérique. Leur cause exacte reste ouverte. La substitution de constantes
de vent par des uniformes n'a pas changé les résultats. Aucun verdict de
parité visuelle ou de filtrage ne découle de ces taux de couverture.

## Vérifications

- Build et Clippy strict réussis pour le binaire final.
- Deux acquisitions natives identiques à l'octet pour chaque vue.
- Désactivation des plantes : profondeur terrain/village inchangée.
- Capacité d'instances nulle dans une copie au manifeste rehashé : refus
  sémantique avant GPU, code 1, aucune sortie de profondeur produite.
- Les premiers essais ont corrigé la lecture CRLF du programme et les
  bindings de texture de rafale. Leurs traces restent distinctes des
  captures finales retenues.

## Preuves locales

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `source-object-shadows-004.png.shadows.json` et ses trois couches source.
- `source-object-canopy-depth-001.png.shadows.json` et ses trois couches source.
- `native-object-depth-005.bin`, SHA-256
  `68c86016224c690f6c1891dd6780eae9a3f4933940931380914b7790a1c5000d`.
- `native-object-canopy-depth-002.bin`, SHA-256
  `1a1906f797cda5e2c7b7c0b2df503ce2ec4b300adee1f97ba73084e717b04f7d`.
- `object-depth-comparison-005.json`, `object-canopy-depth-comparison-002.json`.
- `plant-shadow-selection-repeat-001.json`, `object-canopy-depth-diagnostic.png`.
- `plants-disabled-terrain-regression.bin`, SHA-256
  `1dbce19e8b15d745710d92c945e6689ae48270e4b988130c5be1fac34dc4867f`.
- `plant-shadow-invalid-001`, `native-plant-invalid.log`.
- `plant-depth-clippy-005.log`, `plant-depth-build-005.log`.
- Binaire SHA-256
  `b8c3cd30f81892db4ca8bda0561ea9977743c80709d2312039495b702e030a22`.

Le filtrage PCSS/PCF et la réception de ces ombres dans la scène couleur
assemblée restent à raccorder. La mise à jour interactive, le parcours Studio,
la qualification WebGPU visuelle et l'identité de la carte complète restent
ouverts. Aucune publication.
