# Crêtes alimentées par le FFT natif partagé

Le mode `tidewater_breaker_probe BREAKERS_EXPORT NEW_OUTPUT --native-ocean
OCEAN_EXPORT` calcule maintenant les vagues puis les crêtes sur le même
RenderDevice Bevy. `OceanComputeSet` ordonne les passes. Le kernel des crêtes
utilise directement la TextureView de déplacement publiée par le FFT natif,
sans lecture CPU ni réimport de la texture. Les paramètres océaniques sont
dérivés du calcul natif et contrôlés contre les paramètres appariés.

La portée reste le pas 120, à 2 s. Terrain, champ de propagation, constantes
et shaders proviennent de l'acquisition figée. Ni ruban visible, ni embruns,
ni animation interactive ne sont qualifiés par ce calcul.

## Preuve sans résultats source dans les entrées

Les répertoires `breakers-native-inputs-001` (10 fichiers) et
`ocean-native-inputs-001` (13 fichiers) ne contiennent aucun fichier de
déplacement FFT ni de crêtes. L'exécution finale avec ces entrées réussit.
Son buffer est identique à celui de la première exécution avec les exports
complets. Les listes exactes sont dans `breaker-shared-regression-001.json`.
Le manifeste indique `shared_native_fft_frame120`,
`source_fft_textures_uploaded=false` et `source_crest_results_uploaded=false`.

Comparaison indépendante avec `breakers-source-002`, rapport
`breaker-shared-stripped-comparison-001.json` :

- 690 crêtes actives dans chaque moteur ; aucun écart de masque actif,
  d'identifiant de vague ou de masque des segments dessinables.
- Erreur par coordonnée de racine : moyenne 0,095365 mm, maximum 1,430511 mm.
- SHA-256 du buffer natif :
  `d7200dcefaec5248d176e6cefe9447a473d5017de0ee8daa3fc4f9bc6e0d0164`.

Les autres attributs sont mesurés dans le rapport. Ces écarts ne permettent
pas de déclarer une égalité flottante ou une parité visuelle.

## Régressions et compilation

`breaker-shared-regression-001.json` confirme :

- Trois sorties FFT de 21 fichiers chacune identiques à la référence native
  précédemment qualifiée ; six fichiers ShoreSim inchangés dans la scène.
- Trois buffers du mode isolé identiques aux sorties précédentes.
- Capture `native-breaker-shared-scene-regression-001.png` identique à
  `native-water-detail-001.png`, SHA-256
  `add57ba05d57f8a191cfffd06211267384e1c5ec2e41abd8c44a9fd7d76d965f`.

Clippy strict et compilation des deux exemples réussis, journaux
`breaker-shared-clippy-002.log` et `breaker-shared-build-002.log`.
Binaire crêtes : `88fc1d9b3e5f20c426928e45b19b865d8f6ca28890930fb8527b3a9abff4be98`.
Binaire scène : `4b3a038a400099fddc19033926deacb9eb76ca13d702e6ce4a41cb0f82630596`.

Les acquisitions et résultats restent sous `E:/OpenRPG_Forge/.tidewater-integration/`.
Tous les critères TW complets restent ouverts. Prochaine étape : raccorder
le buffer au ruban visible et comparer son rendu à la source.
