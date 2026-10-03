# Occlusion GPU des plantes par un objet opaque — 28 septembre 2026

Le probe natif dessine trois vues ID synchrones de 1920 × 1080 : plantes
seules ; plantes avec terrain et village opaques ; puis les mêmes surfaces avec
un petit cuboïde `StandardMaterial` opaque placé devant des plantes visibles.
L'entité source du cuboïde est retirée avant l'extraction du rendu : seule sa
copie de profondeur existe dans la troisième passe hors écran. Ce diagnostic
ne modifie ni la carte canonique, ni la capture de scène principale.

Rejeu local depuis `preview-scene-bundle-001`, son workspace auteur et son
paquet bâtiments épinglé : code de sortie 0,
`PLANT_ID_GPU_PASS unique=37 canonical_resolution=pass terrain=1 village=140
standard=1 occluded_pixels=5108 standard_occluded_pixels=3970`. La comparaison
indépendante des deux buffers masqués retrouve 3 970 pixels de plantes retirés
par l'objet Standard et 0 ID modifié parmi les pixels de plantes restants. La
capture principale `native-picking-standard-002.png` a été inspectée : île,
ponton, maisons, plage, végétation et eau sont présents. Son SHA-256 est
strictement identique à celui de `native-picking-occluders-004.png` :
`243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`.

Preuves locales sous `E:/OpenRPG_Forge/.tidewater-integration/` :

- `native-picking-standard-002.plant-ids-plain.bin` : `0ddd1e26fbe397b17f087916405486413942d53b47e86de1de316d44545ae344` ;
- `native-picking-standard-002.plant-ids.bin` : `15ac448d92bfa427162e487b0661ece2bc29bc7e1ce73c727752ea784d1c8059` ;
- `native-picking-standard-002.plant-ids-standard.bin` : `b2c0c4bebc176cf9a1c09b4d6a7fd1a3331c2bcad5e52906ec823bfa70da8d88` ;
- `native-picking-standard-002.plant-ids.json` : `0f65a9e3170e05a341dc1e420e0bb6b352342fe22ba8870031503a7f44c36273` ;
- `native-picking-standard-002.log` : sortie GPU et diagnostics.

`cargo clippy -p forge_engine --lib --example tidewater_plant_probe --no-deps
--locked --offline -j 1 -- -D warnings`, le build natif du même exemple,
`rustfmt --check` ciblé et `git diff --check` passent. La première tentative
avait échoué avant le GPU car la visibilité héritée d'une entité créée dans la
même frame n'était pas propagée ; l'entité de diagnostic porte maintenant
`InheritedVisibility::VISIBLE` explicitement, et le rejeu complet passe.

Cette preuve qualifie le shader de profondeur Standard sur GPU natif à vue
fixe. Elle ne prouve pas encore le clic dans le Studio WebGPU, le choix d'un
objet d'auteur réel, ni l'édition persistante d'une plante importée.
