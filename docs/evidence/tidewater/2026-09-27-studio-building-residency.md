# Preparation des batiments pour le viewport Studio

Le chargeur de bibliotheque prepare maintenant les meshes locaux et les images
avant d'adopter le paquet. Toute erreur de preparation laisse les caches actifs
intacts. Le viewport consomme ensuite cette preparation dans ses propres
Assets<Mesh> et Assets<Image>, sans second App ni device. Les ressources
ResidentMeshes et ResidentLibrary partagent les handles entre futurs placements.
Une nouvelle ouverture de viewport reconstruit la preparation depuis le paquet
verifie conserve en memoire. Les documents auteur ne sont pas modifies.

PreparedMeshes separe la validation CPU de l'allocation des handles. L'ancienne
entree ResidentMeshes::load reste disponible et utilise le meme chemin.
PreparedLibrary::stage transfere les textures et les parametres sans reconstruire
les images pour chaque instance. Le shader et les liaisons de scene, notamment
les ombres, restent a raccorder avant affichage des batiments dans Studio.

Verification WASM : cargo check -p forge_studio --lib --no-default-features
--features web --target wasm32-unknown-unknown --locked --offline -j 1 : PASS.
L'avertissement proc-macro-error2 de compatibilite future reste preexistant.
Le probe natif tidewater_package_probe a ete etendu pour verifier la preparation
sans allocation, les 111 meshes residentes, les revisions et les 14 images.
Compilation et execution natives PASS (session 39282, exit 0) : 23 batiments,
111 parties, 112569 sommets, 7 materiaux et 14 textures. Partage des handles,
revisions refusees et identites/transforms des racines verifiees par le probe.
Rapport local : studio-building-residency-proof-001.json. Pas de verdict GPU/UI.

Ces modifications sont posterieures au candidat studio-preview-candidate-001.
Ce candidat conserve ses preuves propres, mais ne contient pas cette etape.
Aucune nouvelle preuve visuelle ou de placement dans Studio n'est revendiquee.
