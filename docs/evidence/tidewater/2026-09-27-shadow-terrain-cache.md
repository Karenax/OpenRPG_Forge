# Cache CPU des ombres du terrain importe — 27 septembre 2026

## Modification

`terrain::authored::PreparedSource` conserve les bornes spatiales du terrain,
les octets verifies, les corrections auteur et le shader adapte pour une revision
immuable. `at(view)` recalcule la selection CDLOD et les parametres de camera.
L'adapter one-shot `prepare` conserve sa signature pour les scenes natives.

Studio reutilise cette preparation tant que l'empreinte du document et la
generation de bibliotheque ne changent pas. Une nouvelle preparation doit
reussir avant de remplacer l'ancienne ; les documents canoniques restent les
seules sources des retouches. Le cache est derive et non serialise.

Cela retire l'evaluation et la construction des bornes des quatre millions
d'echantillons a chaque generation de camera. Ce lot ne supprime pas encore
les copies des buffers vers Input, leur validation, les uploads GPU ni les
reconstructions de pipelines/ressources du graphe. Il ne qualifie pas les FPS
ou la latence interactive de Studio.

## Verification

Baseline pre-modification rejouee avec le precedent executable : 64 noeuds,
SHA noeuds 300e88af09ce7f21eea46a2611568c974d85a8eff4cd5c51a429328ea5a7c71f,
SHA shader f85c01b74c797e7eb77704d90a128277b4a6253e9d7dfe79441ae3927881d659.
Log local shadow-cache-before-001.log. Compilation WASM et Clippy cible moteur
passes. Build natif PASS (7 min 26 s, moteur et deux probes). Test CPU PASS :
les empreintes de noeuds et shader sont identiques a cette baseline.

Mesure locale unique sur huit vues : 242,152 ms sans cache, 12,607 ms avec cache
(environ 30,27 contre 1,58 ms par preparation, facteur 19,2). Construction initiale
28,744 ms. Cette mesure n'inclut pas le renderer ; aucun gain FPS revendique.
Log shadow-cache-after-001.log. Regression native PASS : la capture a ete
inspectee et les trois fichiers sont identiques octet par octet a
native-imported-viewport-lod-001 (meme scene Forge, aucune nouvelle parite upstream).
La scene fixe native consomme l'adapter one-shot ; les huit vues CPU verifient
separement le chemin avec cache. Cela ne vaut pas qualification du canvas Studio.

- PNG native-shadow-cache-001.png :
  243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
- Relief native-shadow-cache-001.shadow.bin :
  a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- Objets native-shadow-cache-001.object-depth.bin :
  0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.

Clippy Studio strict conserve uniquement l'echec preexistant atelier_ui.rs:122
(bind_main 103 lignes). Tous les processus d'essai ont termine. Empreintes des
executables et mesures dans le fichier local
E:/OpenRPG_Forge/.tidewater-integration/shadow-terrain-cache-proof-001.json.

Le probe compare toutes les donnees produites dans huit vues avec/sans cache,
un aller/retour hors frustum, l'isolation d'une nouvelle revision de relief,
les refus de raster/shader/vue invalides et la conservation du cache initial.
Les durees mesurees concernent seulement la preparation CPU, jamais le rendu
complet ni la navigation Studio.

Aucune nouvelle qualification GUI, distribution, publication Git ou cloture
de l'objectif global.
