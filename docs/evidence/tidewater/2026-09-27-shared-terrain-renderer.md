# Materiau et geometrie de terrain Tidewater partages

Le materiau terrain, ses bindings (normales, splat, detail, environnement, relief,
cascades, nuages et rivage), et l'extension CDLOD passent dans
forge_engine::tidewater_terrain. Les shaders sont installes depuis des sources
fournies par l'appelant : le fragment utilise maintenant un handle fixe au lieu
d'un chemin AssetServer. Le prototype installe son terrain.wgsl verifie et emploie
exactement ces types partages pour ses mises a jour d'eclairage/rivage.

PreparedLod prepare le maillage et deux images CPU depuis SampledTerrainPlan,
les octets du raster et une vue CDLOD. Il controle la taille, l'empreinte et le
domaine attendu avant adoption dans les Assets<Mesh>/Assets<Image> de l'App.
La selection utilise les bornes du terrain retouche. Le shader et la texture de
corrections sont ceux du module terrain_edits partage. Une selection vide ne
cree pas de terrain. Les diagonales alternees et les 40 cellules par noeud sont
conservees du prototype.

Aucun nouveau device ou format canonique. Le maillage reste expanse comme dans
le prototype ; instancing, actualisation de camera et budgets interactifs ne
sont pas qualifies. La preparation des textures de surface est maintenant partagee (voir
2026-09-27-shared-terrain-textures.md). La selection de vue de l'adapter natif
et le recalcul des normales restent encore dans le prototype.

## Validations

Clippy moteur --lib --no-deps -D warnings : PASS (session 64551). Le controle
supplementaire de la hauteur hors domaine (-90 m), identique au contrat de
l'adapter natif, est inclus dans les compilations suivantes.
Compilation native du prototype : PASS (session 25169, 9m54s).
Rejeu complet depuis preview-scene-bundle-001, eau/embruns/nuages, vegetation
canonique et building-package-003 : PASS, session 96313, code 0.

Comparaison native-shared-terrain-renderer-001 avec
native-shared-authored-terrain-001 : 0 pixel different, ecart maximal 0 ;
PNG, .shadow.bin et .object-depth.bin identiques octet par octet.

- PNG : 243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
- Relief : a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- Cascades : 0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.
- Executable : b21b30e775ed509706b1e62de08c0474029524032bef5ab77f41511693a659ea.

Preuve locale : E:/OpenRPG_Forge/.tidewater-integration/shared-terrain-renderer-proof-001.json.

Studio WASM : cargo check -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown --locked --offline -j 1 : PASS, session 11684,
44.19 s. Avertissement futur proc-macro-error2 preexistant.

Cette regression prouve la conservation du rendu fixe Forge, pas la parite
complete avec Tidewater ni un viewport Studio importe operationnel. Le paquet
terrain a ete prepare separement ; son lecteur Rust est verifie ; son adoption Studio reste ouverte.
