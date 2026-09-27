# Branchement du terrain importe dans Studio — 27 septembre 2026

La route authoring distingue maintenant le raster importe du generateur procedural.
Le lancement attend le raster canonique verifie, un paquet terrain v2 et son
environnement. La fin du chargement du raster ou de la bibliotheque relance la
requete d'ouverture en attente, sans lancer une deuxieme application Bevy.

Le nouvel adapter authoring_imported_terrain utilise le canvas de l'atelier,
le renderer Bevy existant, les graphes atmosphere/nuages/ombres du relief,
PreparedTerrain et les 256 collisions metriques. Les documents acceptes passent
par la file de projection ; seul le dernier document a afficher est conserve,
sans modifier les sources ou leur historique natif. Le raster utilise doit
correspondre exactement au document observe. Les chargements obsoletes restent
proteges par l'epoch du transport existant.

Le picking attend l'acquittement Rapier du document courant. En attente ou en
cas d'invalidation, les positions non resolues produisent des misses ; elles ne
rejoignent pas silencieusement deux portions de trace. Une erreur de projection
invalide la vue et demande son rechargement. Le remplacement de bibliotheque ou
d'environnement force une nouvelle generation de terrain.

La camera utilise son ratio reel. ViewGeometry conserve les bornes par document
pour selectionner les meshes CDLOD sans recopier les textures de hauteur ni
recalculer les collisions a chaque mouvement. Une vue sans noeuds masque le
terrain ; une vue suivante le reprojette. La reference native utilise le meme
selecteur de meshes afin de verifier l'absence de regression.

## Limites explicites

Ce branchement est un chantier de projection, pas une qualification de carte
complete ou du geste utilisateur. Le village, la vegetation, l'ocean et les
rivages restent a raccorder ; le graphe d'ombres d'objets est explicitement
desactive et les entrees de rivage restent neutres. Le ciel/nuage utilise encore
l'acquisition a heure fixe ; son comportement pendant la navigation est a
qualifier. Le playtest importe, les apercus de brosse, les routes/rivieres et la
sauvegarde/reouverture apres geste ne sont pas qualifies. La navigation de
l'atelier utilise les dimensions du terrain importe pour le deplacement et le
recul maximal (les limites procedurales precedentes sont conservees).

Le chargement fait actuellement une preparation CPU synchrone dans la boucle
Web ; les temps d'interaction et les budgets memoire restent a mesurer et a
ameliorer. Aucun verdict de rendu WebGPU ou de presentation Tauri n'est deduit
d'une compilation WASM. Aucun nouveau frontend distribue n'est produit ici.

## Verification

- Compilation Studio WASM finale PASS (session 11871 ; 8 min 11 s incluant
  l'attente du build natif). Avertissement futur proc-macro-error2 preexistant.
- Clippy Studio strict : seul echec restant, atelier_ui.rs:122 bind_main
  (103 lignes). Aucun diagnostic restant dans les fichiers modifies ici.
- Clippy moteur et probes --no-deps strict PASS. Build natif PASS, 7 min 42 s.
- Probe physique utilisant register deux fois : PASS, 256 colliders actuels,
  rejet avant synchronisation et de l'ancienne revision, retouche +12 m puis
  restauration exacte. Log imported-viewport-physics-001.log.
- Probe assemblage : PASS, camera hors de portee puis retour aux memes nombres
  de noeuds/sommets et aux memes indices ; remplacement du document, refus
  atomique de binding invalide et nouvelles normales en attente. Log
  imported-viewport-cpu-001.log. Ces tests ne qualifient pas les callbacks UI.
- Scene native complete precedente rejouee et inspectee : capture et deux
  sorties d'ombres identiques octet pour octet a native-terrain-scene-assembly-001.
  Capture native-imported-viewport-lod-001.png, SHA-256
  243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
  Ce resultat qualifie la non-regression du selecteur partage, pas le nouvel
  assemblage Studio (qui demeure incomplet et non observe dans son canvas).

Preuves et empreintes des trois executables :
E:/OpenRPG_Forge/.tidewater-integration/studio-imported-viewport-proof-001.json.
Tous les logs et captures cites sont dans ce meme dossier local. Aucun commit,
push ou frontend distribue. Les processus de test sont termines.
