# Assemblage du terrain importe — 27 septembre 2026

forge_engine::tidewater_terrain::scene assemble les composants partages depuis
SampledTerrainPlan, le raster verifie, TerrainPackage et la vue CDLOD. Le paquet
ne remplace pas le document canonique. Les sorties d'environnement, d'ombres,
de nuages et de rivage sont fournies par la scene appelante via Bindings.

PreparedTerrain termine les calculs CPU (geometrie, corrections shader, normales
si relief retouche et textures) avant adoption. stage exige un renderer enregistre,
les collections d'assets, les textures de dependances resolues et des parametres
finis avant allocation/remplacement. Les erreurs de preparation ou de dependance
laissent l'ancien ResidentTerrain intact. L'adoption cree le nouveau materiau et
l'entite, retire l'ancienne entite et conserve le document_hash projete ainsi que
la generation des normales. Une selection CDLOD vide peut produire aucune entite.

Ce contrat est celui des ressources CPU/ECS. Il n'acquitte ni rendu GPU complet,
ni collisions, ni sauvegarde. Les dimensions/fonctions des textures d'environnement
restent sous la responsabilite des graphes appelants. Le shader utilise des IDs
stables : ce module gere un terrain importe par World, pas plusieurs terrains
independants avec des corrections differentes.

Clippy moteur/probe : PASS, session 46352. Test natif ajoute :
tidewater_terrain_scene_probe. Il utilise des placeholders CPU pour l'eclairage,
ne lance pas le renderer et ne constitue pas une preuve visuelle. Il couvre
registration repetee, document projete, dependance manquante sans mutation,
remplacement d'entite apres retouche et normales encore en attente GPU.
Compilation du probe PASS, session 69060 (5m38s). Execution PASS :
E:/OpenRPG_Forge/.tidewater-integration/terrain-scene-assembly-cpu-001.log.
Le rejeu visible natif est qualifie ci-dessous. Aucun raccordement visible Studio revendique.


Le prototype natif terrain_lod est maintenant raccorde a PreparedTerrain::stage.
Il construit un TerrainPackage en memoire depuis ses ressources de fixture deja
verifiees ; le manifeste derive ne remplace pas la verification de la fixture
source. Les champs de materiau et leurs valeurs restent ceux de l'adapter precedent.
Les ressources du ciel, des ombres et du rivage continuent de venir de leurs graphes
existants. Le rejeu complet a confirme cette adoption, voir resultat ci-dessous.
Compilation du probe CPU : session 69060 ; controle/build de la scene native lance
apres, sans relancer la compilation du moteur deja active.


Compilation Studio WASM PASS, session 37143 (40,08 s), commande habituelle
--lib --no-default-features --features web --target wasm32-unknown-unknown,
--locked --offline -j 1, RUSTFLAGS=-C debuginfo=0. Avertissement futur
proc-macro-error2 preexistant. Adaptateur natif en verification, session 6233.


## Rejeu de la scene native complete

Controle natif et build PASS, session 6233 (10,89 s puis 1m58s).
Execution eau/embruns/nuages, vegetation workspace et batiments portables : PASS,
session 92703. Image et buffers .shadow.bin/.object-depth.bin identiques octet
par octet a native-shared-terrain-normals-001. Capture :
E:/OpenRPG_Forge/.tidewater-integration/native-terrain-scene-assembly-001.png.
Preuve avec hashes CPU, executable scene et sorties :
E:/OpenRPG_Forge/.tidewater-integration/terrain-scene-assembly-proof-001.json.
Executable scene SHA-256 : 18e9a4073fca09a75578f580bacaf5c6a4c208756549600f9b11bd3d674a6a79.

Ce rejeu confirme le chemin d'assemblage partage sur la scene fixe native.
Il ne qualifie pas encore son adoption interactive dans Studio, les collisions,
ni les mises a jour de tous les effets couples apres une retouche.
