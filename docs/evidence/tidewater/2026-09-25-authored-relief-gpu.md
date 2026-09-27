# Tidewater — relief auteur GPU et plantes reprojetées

Le prototype applique désormais les corrections de hauteur du document effectif
dans son vertex shader CDLOD et dans la profondeur du terrain utilisée par les
ombres proches. La texture source reste inchangée : les deltas utilisent une
texture R32Float distincte, grille de coins 2049², tandis que la source est une
grille centrée dans les cellules 2048². L'élévation globale est ajoutée séparément.
Sans delta, une texture neutre 1×1 suffit. L'évaluation des corrections conserve
les coordonnées de `SampledTerrainPlan` ; les bornes CDLOD existantes incluent
déjà les extrema des corrections.

Les deux consommateurs de végétation utilisent maintenant `terrain_view` et
packent les transformations dérivées. Sources, identités, ordre et autres
paramètres restent conservés. Le chargement depuis une archive et les blobs du
workspace reste le parcours utilisé, sans nouvelle représentation canonique.

## Essai natif

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.
`prepare_tidewater_relief_proof` crée une bosse centrée X250/Z140, rayon60m,
hauteur maximale12m, profil `12*(1-r²/3600)²`, puis son annulation par les
commandes de zone. Archives : `authored-relief-states-001`. Adoption, sauvegarde
et réouverture dans `workspace-relief-raised-001` et `workspace-relief-undo-001`.
La fixture `workspace-terrain-scene-001` ne possède ni bloc de hauteurs ni records
de végétation : ils proviennent des autorités workspace.

`scripts/tidewater/verify-relief-render.py` a rendu les deux états en FHD :

- `relief-render-proof-001/raised.png` inspecté : relief et végétation de la
  pente gauche relevés. Les deux passes comptent 203 plantes déplacées sur 65383,
  sans exclusion. Le comptage source indépendant confirme 80 arbres/123 arbustes
  (`authored-relief-source-membership-001.json`). 45062 pixels diffèrent de la
  référence, dont44211 dans les 400 premières colonnes.
- `relief-render-proof-001/undo.png` égale exactement
  `native-canonical-vegetation-001.png` : zéro pixel changé, SHA-256 commun
  `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
- Les profondeurs d'ombres proches diffèrent sur206749 texels ; les nombres de
  nœuds terrain passent de64 à69. La texture d'ombre lointaine reste identique.
  Rapport complémentaire : `regional-and-depth-comparison.json`.

Rapport principal : `relief-render-proof-001/report.json`. Binaire exécuté :
`a17c8a00d503f8ca9a46d0c0d77fd85a4338d518a5ba62698771c44fef25cef2`.
Compilation native et Clippy strict des exemples scène, ombres et préparation
de relief réussis. Le shader WGSL adapté a effectivement été compilé et exécuté
sur le GPU pendant les captures ; l'essai ne repose pas sur le seul contrôle Rust.

## Limites encore ouvertes

Ce test qualifie un relief local dans le prototype, pas la chaîne complète des
retouches. Normales, splats, masques de côte, ombres de relief lointaines,
simulation aquatique, herbe, bâtiments et collisions doivent encore suivre le
terrain effectif. 851 pixels changent hors des 400 premières colonnes (108 avec
écart supérieur à2/255, maximum57) : les changements de sélection CDLOD/ombres
et la dynamique des embruns doivent être distingués avant un verdict de localité
stricte. L'annulation est exacte sur cette paire, pas une garantie universelle
de déterminisme GPU. Studio interactif, navigateur et parité complète restent
ouverts. Aucun commit ou push.
