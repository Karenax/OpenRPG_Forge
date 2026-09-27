# Preparation partagee du terrain edite pour les ombres

Le moteur expose tidewater_object_shadow::terrain::authored::prepare. Le document
TerrainDocument, le raster, le shader verifie et la camera sont fournis en memoire.
La selection CDLOD, les bornes issues du terrain retouche, les parametres de morphing
et la composition du shader sont partages ; les lectures de fichiers restent dans
le petit adapter du prototype. Aucun acces filesystem dans ce chemin moteur.

Les corrections de hauteur passent par tidewater_terrain_edits::Corrections.
Le document et sa grille 2048 m / 1 m sont verifies avant allocation. Les programmes
terrain, ocean, rivage et ombres emploient maintenant ce module commun. Les chemins
de preparation propagent les erreurs ; les deux systemes de creation natifs utilisent
leur invariant de document prealablement valide.

## Validations

Clippy moteur --lib --no-deps -D warnings : PASS (session 51346).
Verification des cinq examples : PASS (session 72526). Deux imports devenus
inutiles ont ensuite ete retires. Compilation native des probes de terrain et
de scene : PASS (session 74948, 7m57s).

Le tidewater_authored_shadow_probe termine avec code 0 : correction de 12 m au
coin 2048/2048, raster source inchange, autres texels de correction nuls,
indices hors domaine et NaN refuses, aspect invalide et shader vide refuses,
retour aux noeuds/corrections/shader initiaux. Selection : 64 noeuds.

Preuve locale : E:/OpenRPG_Forge/.tidewater-integration/authored-terrain-shadow-proof-001.json.
SHA-256 executable : 52717d3f50663809ea9dec584785030d7b99eb51c0df3de295097fbebc01a4c7.
SHA-256 noeuds : 300e88af09ce7f21eea46a2611568c974d85a8eff4cd5c51a429328ea5a7c71f.
SHA-256 shader : f85c01b74c797e7eb77704d90a128277b4a6253e9d7dfe79441ae3927881d659.
Ce test valide les donnees CPU de retouche, pas leur edition graphique.

Rejeu natif complet : PASS (session 39921). Comparaison de
native-shared-authored-terrain-001 avec native-shared-object-scene-001 :
0 pixel different, maximum 0 ; PNG et deux buffers d'ombres identiques octet par octet.

- PNG SHA-256 : 243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
- Relief : a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- Cascades : 0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.
- Executable scene : 73fafc44c42d28e5466b79a04068bc359e2cb3cbc557dbc09037f9a21d554296.

Preuve locale : E:/OpenRPG_Forge/.tidewater-integration/shared-authored-terrain-scene-proof-001.json.

Studio WASM : cargo check -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown --locked --offline -j 1 : PASS (session 26149,
43.44 s). Avertissement preexistant proc-macro-error2 2.0.1 de compatibilite future.
Ce check ne vaut pas execution WebGPU.

## Limites

Ce transfert rend les donnees de terrain editees consommables par le graphe
partage. Le branchement aux mises a jour du viewport Studio et sa validation
WebGPU restent a faire. Il ne prouve pas la carte editable dans l'interface.

## Points de raccordement Studio inspectes

- authoring_assets/placements.rs::prepare resout actuellement les references par
  le catalogue GLB ; Projection et qualify attendent WorldAsset/WorldInstance.
- placements/selection.rs utilise deja ManualPlacementPart et les identites
  canoniques ; le spawn Tidewater partage emet ce meme composant.
- placement_controls/insertion.rs prepare les commandes de zone existantes,
  mais construit TerrainGenerationPlan pour l'alignement au sol. Il faudra
  utiliser le raster verifie et ses retouches pour les zones importees.
- authoring_scene.rs::projection repose sur RiverProjection ; aucun chargement
  SampledTerrainPlan/source-blobs n'a ete trouve dans le code Studio inspecte.

Ces constats orientent le raccordement ; ils ne valent pas implementation.
