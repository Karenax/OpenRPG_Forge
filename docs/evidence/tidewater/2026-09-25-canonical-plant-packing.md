# Tidewater — paramètres de rendu issus des instances canoniques

`forge_world::tidewater_vegetation` convertit les données de population importée
en paramètres du renderer Tidewater : matrice colonne-majeure, `iPos`, `iDat` et
rayon de requête au carré. Le module est indépendant de Bevy/wgpu.

Les règles sont dérivées de la source MIT figée `1438b1ab` : `Vegetation.js`,
`vegetation/InstanceLOD.js` et `engine/math/Matrix4.js`. L'adaptateur conserve
le calcul f64 jusqu'au stockage f32 d'origine, les six groupes, les dix espèces,
les variantes encodées dans la seed, la hauteur spéciale des bananiers et la
valeur absolue de l'échelle verticale de forme. Les notices amont restent applicables.

## Résultat vérifié

Racine : `E:/OpenRPG_Forge/.tidewater-integration/`.
Sortie Rust : `canonical-packed-001`. Rapport : `canonical-packing-verification-001.json`.
Les **65383 instances × 25 composantes f32**, soit **1634575 valeurs**, sont
**identiques bit pour bit** aux buffers originaux exportés dans `source-017`.
Zéro instance différente ; zéro mot différent. Le vérificateur valide les hashes
source, reconstruit les groupes dans l'ordre amont et apparie les résultats par
clé source, sans ajuster ni normaliser les valeurs. Clippy module/exemple passe.

## Suite nécessaire

Les blocs canoniques trient les clés pour leur validation ; cet ordre diffère de
l'ordre d'insertion amont. La comparaison par identité ne prouve donc pas l'ordre
de dessin, les sélections lors d'égalités de distance ni les pixels. Préserver
cet ordre et brancher les buffers dérivés dans le renderer est l'étape suivante.
Le réglage des transformations arbitraires d'objets placés doit aussi rester
cohérent avec les imposteurs lointains. Cette preuve porte sur la disposition
source inchangée et ne qualifie pas encore le placement Studio.
