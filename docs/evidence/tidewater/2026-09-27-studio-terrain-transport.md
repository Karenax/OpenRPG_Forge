# Raster de la zone ouverte : transport et alignement Studio

Le protocole Studio ajoute ReadTerrainSource et TerrainSource. Le host derive
l'identite du raster du document effectif ouvert ; aucune entree UI ne choisit
un chemin ou une autre empreinte de blob. Chaque requete porte l'empreinte du
document attendu et un offset multiple de 256 Kio. Le stockage verifie la taille
et le SHA-256 complet avant de servir le premier bloc. Un seul raster verifie
est garde en cache ; la fermeture de la session le libere.

Une zone independante utilise son source-blobs ; une zone geree utilise le
source-blobs du workspace-v1. Le repertoire doit rester sous l'autorite du host.
L'assembleur commun refuse une autre empreinte/document/taille, un bloc tronque,
duplique ou hors sequence. La source complete n'est rendue qu'apres verification
finale SHA-256. Ni sauvegarde ni commande de mutation ne sont declenchees.

Studio observe le document courant, charge les blocs avec annulation logique
par generation, garde la source immuable partagee entre retouches et invalide
le plan derive a chaque changement. Apres echec, relire la session relance le
chargement. Une reponse d'une ancienne generation n'est pas adoptee.

Les chemins insertion, position precise/duplication, glissement horizontal et
poignees X/Y/Z utilisent maintenant le calcul partage de placement sur raster.
Chaque geste garde son plan prepare ; la soumission conserve le controle de
revision existant. Les placements proceduraux emploient leur calcul precedent.

## Verification

- cargo test -p forge_project --test terrain_source_transport --locked --offline
  -j 1 : PASS. Test d'une source 1 Mio en quatre blocs, rejets de requetes et de
  reponses invalides, assembleur incomplet/corrompu, session inchangee, fermeture,
  corruption sur disque puis adoption dans un workspace. La lecture geree reste
  valide apres corruption de l'ancienne copie de zone.
- cargo check -p forge_studio --bin forge-studio --locked --offline -j 1 : PASS,
  session 42288 (15.33 s).
- cargo check -p forge_studio --lib --no-default-features --features web --target
  wasm32-unknown-unknown --locked --offline -j 1 : PASS, session 41273 (16.37 s),
  apres raccordement des poignees. Avertissement futur proc-macro-error2 preexistant.
- Clippy projet strict reste en echec sur les deux needless_pass_by_value
  preexistants de zone_authoring/session.rs:349-350. Le passage non strict confirme
  que le test et le nouveau code projet n'ajoutent aucun avertissement.
- Clippy Studio strict rejoue apres le dernier raccordement (session 72128) :
  echec sur le seul avertissement preexistant atelier_ui.rs:122, bind_main
  103/100 lignes. Aucun nouveau diagnostic sur ce raccordement.

## Limites

Pas de test interactif du pont Tauri/WASM ni des gestes Studio dans ce lot.
La compilation ne prouve pas ce parcours. Le viewport de terrain importe, les
materiaux Tidewater, le catalogue et les projections d'objets doivent encore etre
completes avant que la carte soit editable dans Studio. Aucun ancien candidat
binaire ne contient ces ajouts. La parite visuelle complete reste ouverte.
