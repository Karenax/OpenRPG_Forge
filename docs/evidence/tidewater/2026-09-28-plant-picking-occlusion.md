# Occlusion GPU des plantes par la scène Tidewater

Le mode natif `--resident-plant-navigation` utilise maintenant deux passes ID
hors écran de 1920 × 1080 avec la même caméra et le même instantané de plantes.
La première dessine les plantes seules ; la seconde ajoute, avec écriture en
profondeur et ID zéro, les meshes visibles du terrain et du village opaque.
Les cinq surfaces de village en transparence `Blend` restent volontairement
traversables au clic jusqu'à définition d'une règle d'interaction alpha.

Exécution locale avec `preview-scene-bundle-001`, son workspace, son paquet de
bâtiments et la scène complète eau/embruns/nuages : exit 0, ligne
`PLANT_ID_GPU_PASS unique=37 canonical_resolution=pass terrain=1 village=140
occluded_pixels=5108`. Le contrôle compare chaque pixel des deux passes : un
ID conservé doit être identique à celui de la passe de référence ; les 37 IDs
restants se résolvent dans la tête canonique
`f05a68acc294b586940237565c77fe2f7679ae9d2b77f4596f0380680665d50f`.
Le compteur 5 108 correspond aux pixels de plante retirés par **l'ensemble**
terrain + village ; cette exécution ne mesure pas encore leurs contributions
séparées.

La capture native `native-picking-occluders-002.png` a été inspectée : plage,
ponton, maisons, île, végétation et mer sont présents, sans défaut nouveau
évident sur cette vue. Preuves locales sous
`E:/OpenRPG_Forge/.tidewater-integration/` :

- capture PNG SHA-256 `243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78` ;
- ID plantes seules SHA-256 `0ddd1e26fbe397b17f087916405486413942d53b47e86de1de316d44545ae344` ;
- ID avec occlusion SHA-256 `15ac448d92bfa427162e487b0661ece2bc29bc7e1ce73c727752ea784d1c8059` ;
- reçu JSON SHA-256 `614a05f2b9309682bc6c68f31b4f3b8ea8450f27ab7f0915412f98240338bb22`.

`cargo build -p forge_engine --example tidewater_plant_probe`, Clippy strict
ciblé (`--lib --example tidewater_plant_probe --no-deps -- -D warnings`),
le test unitaire `a_snapshot_resolves_only_drawn_ids_from_the_same_head`
(1 réussi, 427 filtrés),
`rustfmt --check` ciblé, `git diff --check` et `cargo check -p forge_studio
--lib --no-default-features --features web --target wasm32-unknown-unknown
--locked --offline -j 1` passent. Le check WASM prouve la compilation, pas
l'exécution WebGPU de cette passe.

Le clic dans Studio, le rejet d'une réponse après changement de caméra ou de
viewport, les commandes de pose/retouche et Undo/Redo restent à raccorder et à
vérifier dans l'interface. Le test est une preuve native à vue fixe, pas une
qualification de la carte entière ni une comparaison de parité visuelle avec
la référence Tidewater.
