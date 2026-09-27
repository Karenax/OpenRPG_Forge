# SeaDetail — variations de surface de l'océan

Le matériau d'eau natif utilise désormais les fonctions originales SeaDetail :
rafales, bandes lisses et modulation des petites vagues. L'intégration conserve
FFT, ShoreWaves, ShoreSim, SurfFoam et le terrain CDLOD corrigé. La comparaison
porte sur deux vues fixes à 2 s ; ce n'est pas encore une qualification animée.

## Acquisition et paramètres

`export-water-material.mjs --shore-detail` ajoute SeaDetail à la configuration
du rivage simulé. Deux acquisitions `water-detail-source-001/002` sont exactes :
68 fichiers, 17 modules. Le bruit est constitué des octets RGBA16Float produits
par le constructeur original, 256², un seul mip, sans conversion colorimétrique.
SHA-256 : `0a940eba280d1e5b4b87397ec37a6757e9999a1af4c256809e8eb249ad2aa945`.

Le rendu source `--water-detail` avance la dérive pendant 120 pas à 60 Hz.
Les 32 octets de paramètres sont enregistrés avec la capture et vérifiés par
hash lors de la préparation. Le vérificateur contrôle la dérive avec le vent
du Frame apparié : 6 m/s, direction [1,0], soit [8,4 m, 0] après deux secondes.
Les intensités restent celles du constructeur (1, 1, 0,3).
Les états ShoreSim sont identiques à l'oracle autonome au pas 120.

Rapports `sea-detail-reference-verification-001.json`,
`sea-detail-island-reference-verification-001.json` et
`sea-detail-repeat-reference-verification-001.json`. Ce dernier vérifie une
répétition exacte du PNG source, de Frame, de ShoreSim et des paramètres.

## Raccordement Rust

`water_optics.rs` charge le bruit RGBA16Float et contrôle dimensions, valeurs
finies et taille des paramètres. Les bindings 30/31 fournissent la texture et
le bloc original ; le shader utilise le sampler linéaire répétitif existant.
Les anciens modes conservent une texture provisoire inutilisée par leur shader.
La dérive native est figée aux paramètres de la capture ; son intégration dans
la boucle interactive reste à réaliser.

Fixtures `water-detail-seaward-001`, `water-detail-island-001`.
Invocation native conservée : `--water-shore-sim OCEAN_EXPORT SHORE_SIM_EXPORT`.
La présence de SeaDetail dépend du matériau préparé.

## Résultats visuels

Les deux PNG natifs sont inspectés. Le masque mesure les pixels modifiés par
SeaDetail dans la source de plus de 3 niveaux RGB, dilatés d'un pixel.

| Vue | Pixels du masque | Moyenne région /255 | P99 | Maximum | Moyenne image /255 |
|---|---:|---:|---:|---:|---:|
| Plage | 276 864 | 0,304258 | 9 | 117 | 0,174472 |
| Île | 760 371 | 0,191632 | 2 | 51 | 0,148278 |

Rapports `sea-detail-native-comparison-001.json` et
`sea-detail-native-island-comparison-001.json`.

- `native-water-detail-001.png` :
  `add57ba05d57f8a191cfffd06211267384e1c5ec2e41abd8c44a9fd7d76d965f`.
- `native-water-detail-island-001.png` :
  `29baf641ea71c0a203868bb1a9482b00a3ed47b15fba4c72981339c8dc66db62`.

Il reste des écarts locaux. À 6 m/s, la formule originale désactive les lignes
d'écume de vent (`freshWind=0`) : leur code est raccordé, mais ces captures ne
qualifient pas leur apparence à vent plus fort. Les bandes lisses et les
variations de rugosité changent effectivement les captures.

## Validation et limites

Build et Clippy ciblé `-D warnings` réussis (`sea-detail-build-001.log`,
`sea-detail-clippy-001.log`). Binaire :
`76a87ee51e8a5a6f135c3c385a12dbe0290bceb1923f8abd430c528fe395b96c`.

`sea-detail-regression-001.json` vérifie la répétition native exacte, les trois
anciens modes exacts (optique, ShoreWaves, rivage simulé), la référence source
sans SeaDetail inchangée et les 326 fichiers de l'ancienne préparation exacts.
Les six captures natives conservent leurs 21 fichiers FFT ; les quatre captures
simulées conservent leurs six fichiers ShoreSim.

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`. Pas de publication.
Animation interactive, vent fort, vagues plongeantes séparées, embruns,
sillages, immersion, atmosphère complète, Studio et package restent ouverts.
Les critères TW complets restent ouverts ; aucun verdict de parité globale.
