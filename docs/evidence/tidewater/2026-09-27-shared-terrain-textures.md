# Textures de terrain partagees — 27 septembre 2026

Le module forge_engine::tidewater_terrain::textures prepare le splat et les deux
variantes de detail dans des images CPU puis les adopte dans Assets<Image>.
PreparedSurface peut charger directement le TerrainPackage verifie. Les donnees
restent RGBA8 lineaires avec leur chaine de mips complete. Clamp/Repeat, filtrage
lineaire et anisotropie 4/1 sont conserves du prototype. Les deux entrees sont
validees en taille avant toute allocation d'image. Le normal/AO reste separe :
normal_image controle sa chaine et active RENDER_ATTACHMENT pour le rebake.
Aucune disponibilite GPU n'est deduite de cette preparation CPU.

Le prototype utilise maintenant cette preparation pour le materiau de surface
et l'image des normales. Le recalcul du normal/AO et son graphe de mipmaps restent
a partager ; l'adoption dans le viewport Studio n'est pas encore branchee.

Clippy moteur --lib --no-deps -D warnings : PASS, session 11788.
Compilation native : PASS, session 19704, 8m18s.
Verification CPU du paquet reel : PASS, session 19049. Octets, format RGBA8,
niveaux de mips, adressage et anisotropie conserves ; entrees tronquees rejetees.
Clippy strict de tidewater_terrain_textures_probe : PASS.
Rejeu complet natif : PASS, session 19049. Le PNG, le buffer d'ombres du relief
et celui des objets sont identiques octet par octet a native-shared-terrain-renderer-001.
Capture : E:/OpenRPG_Forge/.tidewater-integration/native-shared-terrain-textures-001.png.
Preuve : E:/OpenRPG_Forge/.tidewater-integration/shared-terrain-textures-proof-001.json.
Executable SHA-256 : 7a8c4e70cfe29fd4d46e41150559702110c48e22dfbb98ee528e65dbd4fe66ac.
Ce resultat concerne la scene fixe native Forge ; il ne prouve pas la parite
complete source ni l'edition interactive dans Studio.


## Acquisition Studio : compilation qualifiee, interaction a verifier

Le candidat de bibliotheque accepte maintenant terrain_sha256 optionnel.
Son chargement passe par le lecteur asynchrone borne et le transport meme origine
existant. Un ancien chargement remplace ne publie pas ses ressources ; toutes les
preparations doivent reussir avant le remplacement des caches. Les textures sont
adoptees dans Assets<Image> via ResidentSurface. Le cache est restaure lors de la
recreation de l'application ; une bibliotheque sans terrain retire la ressource
precedente. Aucun materiau visible n'est encore instancie par ce raccordement.

prepare-building-library-site.py accepte deux arguments additionnels (dossier
terrain, empreinte) apres ceux de l'environnement. Dossier nouveau cree :
E:/OpenRPG_Forge/.tidewater-integration/building-library-site-terrain-001.
Les trois manifests et 821 + 266 + 6 payloads ont ete relus et verifies.
Preuve : building-library-terrain-proof-001.json dans le meme dossier de travail.
Il s'agit des ressources statiques ; aucun frontend Studio compile n'est inclus.
Compilation Studio WASM : PASS, session 14207, 3m49s incluant attente du verrou.
Commande : cargo check --locked --offline -j 1 -p forge_studio --lib
--no-default-features --features web --target wasm32-unknown-unknown.
RUSTFLAGS=-C debuginfo=0. Avertissement futur proc-macro-error2 preexistant.
Parcours GUI non qualifie. Normales retouchees, ombres et effets couples doivent
encore rejoindre le viewport importe avant de revendiquer son fonctionnement.
