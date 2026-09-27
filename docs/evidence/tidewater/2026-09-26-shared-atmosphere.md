# Calculs atmospheriques dans le moteur partage

Le module `forge_engine::tidewater_atmosphere` execute les quatre kernels qualifies
(transmittance, multiScat, skyView, irradiance) sur le RenderDevice de Bevy.
Les trois textures RGBA16F et le buffer irradiance sont exposes aux consommateurs.
Le graphe conserve leur ordre avant `camera_driver`, sans seconde pile GPU.

L exemple natif conserve la lecture des fixtures, le controle de provenance,
la stabilisation numerique de la frontiere et les readbacks. Le moteur ne lit
aucun fichier et n accede pas au reseau. L appelant fournit les shaders qualifies.
Le constructeur borne leur taille et rejette les parametres non finis ;
installation sans renderer ou installation repetee sont refusees.
Le signal `ready` signifie commandes encodees, pas fence de completion CPU.

## Perimetre

Calcul fixe execute une fois. Mise a jour selon horloge/camera, raccordement
au Studio et qualification GPU WebGPU restent ouverts. La qualification WGSL
et de son interface incombe toujours a l acquisition verifiee ; le constructeur
ne valide pas a lui seul un shader arbitraire.

## Verification

Execution native reussie sur `atmosphere-source-004` vers
`native-shared-atmosphere-001` : les quatre sorties sont identiques a l octet
avec `native-atmosphere-stable-002` (64, 8192, 165888 et 131072 octets).
Rapport local : `E:/OpenRPG_Forge/.tidewater-integration/shared-atmosphere-proof-001.json`.
Les cas non-fini, shader vide et renderer absent sont verifies avant le run GPU.

- `cargo check -p forge_engine --example tidewater_atmosphere_probe -j 1` : passe.
- `cargo run -p forge_engine --example tidewater_atmosphere_probe -j 1 -- ...` : passe.
- `cargo check -p forge_engine --lib --no-default-features --features web --target wasm32-unknown-unknown -j 1` : passe.
- Formatage des deux modules modifies : passe.
- `cargo clippy -p forge_engine --example tidewater_atmosphere_probe --example tidewater_plant_probe --no-deps -j 1 -- -D warnings` : passe.

Aucune nouvelle capture de scene complete dans cette verification : la preuve
porte sur les quatre buffers calcules, pas sur l affichage dans Studio.
La compilation WASM ne prouve pas encore leur execution GPU dans le navigateur.
Les ecarts historiques entre la reference WebGPU et Vulkan restent ouverts,
notamment le texel de frontiere documente dans `2026-09-24-atmosphere-boundary.md`.
Aucune publication.
