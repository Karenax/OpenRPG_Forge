# Ombres des objets reçues dans la scène native

Le mode `tidewater_plant_probe --object-shadows` raccorde les trois couches
de profondeur natives aux matériaux terrain, village, plantes et herbe.
La lumière directe reçoit maintenant le facteur PCSS/PCF de Tidewater,
en plus des ombres du relief déjà présentes. L'environnement indirect
reste séparé. Les textures sont produites sur le device Bevy, sans charger
les profondeurs des oracles JavaScript.

`prepare-object-shadows.py` vérifie le commit source et les hashes du jeu
de données, extrait le module d'ombre et les deux fonctions de bruit/disque
source, puis adapte leurs bindings et accès caméra. Le filtre conserve :

- trois cascades, raccords fondus et biais normal source ;
- PCSS sur la première cascade, recherche de huit échantillons plus le
  centre et filtre de douze échantillons ;
- PCF à cinq échantillons de comparaison matérielle ailleurs ;
- diamètre solaire 0,00925 rad, biais 0,00002 et index de frame fixe 124.

Les matrices, distances, largeurs et bandes de raccord sont calculées par
`SunCascades` côté Rust et envoyées dans un uniforme partagé. La texture
Depth32Float utilise un sampler de comparaison LessEqual linéaire.
Les modes sans ombres des objets reçoivent une texture vide 1×1×3 et des
paramètres désactivés. Un jeu de shaders filtrés utilisé sans mode de
profondeur native est refusé ; le mode explicite de filtrage refuse un jeu
qui ne possède pas les programmes de réception.

## Comparaisons fixes

Deux vues 1920×1080 ont été exécutées et inspectées. Le village contient
l'herbe ; la vue forêt reprend son jeu de données sans herbe. La référence
conserve le même périmètre pour chaque caméra.

| Vue | Erreur moyenne par canal /255 | P99 du canal le plus différent /255 | Maximum /255 |
|---|---:|---:|---:|
| Village | 0,426710 | 7 | 206 |
| Forêt | 0,778127 | 30 | 176 |

Ces métriques portent sur l'union des pixels non noirs, pas une couverture
géométrique. Elles ne constituent pas une validation de parité complète.
Les différences fines de végétation et de profondeur précédemment mesurées
subsistent ; les erreurs maximales empêchent de présenter la scène comme
identique pixel par pixel. La forêt est davantage affectée localement.

L'activation du filtre modifie 295 403 pixels dans la vue village par
rapport au rendu natif sans ombres des objets, avec une différence moyenne
absolue de 5,549 par canal sur toute l'image. Les ombres sont donc réellement
appliquées. Les profondeurs calculées restent identiques aux oracles natifs
précédents pour les deux vues.

## Vérifications et preuves

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `object-shadow-scene-001`, `object-shadow-canopy-001` : shaders préparés,
  données et provenance hashées.
- `native-object-filter-002.png`, SHA-256
  `7437b23df6e3cf924f0c34d350de314b48b20a838ab1d069f818636aa70c35b6`.
- `native-object-canopy-filter-002.png`, SHA-256
  `1b5a50420fd764365b5ef96a3958fd86e0553da73e31229ea53f54ccdf92488e`.
- `object-filter-comparison-001.json`, `object-canopy-filter-comparison-001.json`
  comparent les captures 001 ; les captures finales 002 sont identiques.
- `object-filter-effect-001.json`, `object-filter-regression-002.json` :
  effet visible, répétition des deux images, profondeurs inchangées et
  refus des deux modes incohérents avant GPU (code 1, aucune capture).
- `object-filter-disabled-001.png` : retour au rendu antérieur exact,
  SHA-256 `195474da178851f6e6a6c972a4c59ae9dd55effeafb2eddc4ec186354dcf8484`.
- `object-filter-clippy-002.log` : Clippy strict réussi.
- `object-filter-build-003.log` : build réussi. L'essai 002 avait échoué
  dans MSVC `link.exe` (0xc0000005) ; la relance après fin du processus a
  réussi sans changement de code ni nettoyage.
- Binaire final SHA-256
  `ea5bde97239e6ec0d146545db7cb99e88de31ab8f8a7bca5ae791656995e9ac1`.

## Limites ouvertes

Caméra, vent, soleil et bruit du filtre sont figés. L'actualisation interactive
des cascades et instances, le TAA qui résout le bruit PCSS, les nuages et leurs
ombres, le rebond lumineux, le ciel natif, l'océan et le traitement final
restent à réaliser/qualifier. L'environnement vient toujours d'une acquisition
source froide. Cette preuve ne vaut pas qualification du parcours Studio,
du navigateur WebGPU ou d'une distribution autonome. Aucune publication.
