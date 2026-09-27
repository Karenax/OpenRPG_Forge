# Paquet des ressources de rendu du terrain

scripts/tidewater/package-terrain-assets.py construit et verifie un paquet
forge.tidewater.terrain-package version 1 depuis une fixture explicitement epinglee.
Il contient terrain.wgsl, terrain-lod.wgsl, terrain-normal.bin, terrain-splat.bin,
terrain-detail.bin et LICENSE-Tidewater.txt. Les trois textures gardent tous leurs
mips source et les dimensions exactes (2048, 2048, 512). Les donnees et la licence
sont copiees sans modification. Le raster de hauteur reste dans le workspace.

Source Tidewater : 1438b1abfcaee3267092b75573014f4d9b4a983c.
Fixture SHA-256 : 0b4437a7fa404d47ef22b430f7fa4eff573b86c5fc5f62742d25a25be5561027.
Paquet local : E:/OpenRPG_Forge/.tidewater-integration/terrain-package-001.
Manifest SHA-256 : 9d8f34a1a3780f6d0b4be698011a1a88d30fa1aa57d101ca2f881e19a7e87627.
Six payloads, 46209937 octets, plus package.json.

Le createur refuse les fichiers lies/non reguliers, applique des limites fixes
par nom et verifie chaque empreinte avant creation du dossier cible. Il n'ecrase
aucun dossier existant. Le verificateur impose un inventaire exact et les tailles
et empreintes des payloads. Une interruption d'ecriture peut laisser un dossier
partiel ; aucune validite n'est annoncee avant la verification finale.

Tests locaux : verification nominale, mauvaise empreinte du manifest, mauvaise
empreinte de fixture avant creation du dossier, sortie deja existante et
alteration d'un octet de texture. Tous les refus attendus sont observes. Texture
restauree puis paquet entier re-verifie. Preuve :
E:/OpenRPG_Forge/.tidewater-integration/terrain-package-proof-001.json.

Ce paquet ne constitue pas une qualification visuelle ni un module distribuable.
Le lecteur Rust borne est maintenant verifie (voir complement ci-dessous).
Son transport Studio et l'adoption dans le viewport restent a realiser. Les normales retouchees et les effets couples conservent leurs travaux
propres ; les ressources ci-dessus correspondent a la reference fixe.


## Lecteur Rust partage — verification du 27 septembre 2026

`forge_assets::tidewater_terrain::TerrainPackage` charge ce paquet par callbacks
synchrones ou asynchrones, sans dependance au filesystem ou au renderer. Le
manifest externe epingle est borne a 16 Kio. Inventaire exact, version, commit,
limites et tailles des chaines de mips sont verifies avant tout appel de transport.
Chaque payload est ensuite controle en taille et SHA-256 ; shaders et licence
exigent du UTF-8 non vide. L'objet complet n'est rendu disponible qu'apres succes.
Le callback reste responsable de borner la lecture physique a la limite fournie.
Ce lecteur ne qualifie pas la compilation WGSL ni le rendu GPU.

Verification executee sur `terrain-package-001` avec son empreinte ci-dessus :

- `cargo run --locked --offline -j 1 -p forge_assets --example verify_tidewater_terrain -- PACKAGE_ROOT MANIFEST_SHA256` : PASS.
- Nominal synchrone et asynchrone avec lectures temporairement Pending : PASS.
- Shader altere, dependance absente, mauvaise empreinte du manifest : rejetes.
- Inventaire incomplet, traversal de chemin, entree surdimensionnee, chaine de
  mips tronquee, version inconnue : rejetes avant lecture.
- Nouveau manifest valablement epingle contenant un shader UTF-8 invalide ou
  uniquement des espaces : rejete, aucune lecture ulterieure.
- `cargo clippy --locked --offline -j 1 -p forge_assets --lib --example verify_tidewater_terrain --no-deps -- -D warnings` : PASS.
- `cargo check --locked --offline -j 1 -p forge_assets --lib --target wasm32-unknown-unknown`
  avec RUSTFLAGS `-C debuginfo=0` : PASS.

Les donnees du paquet n'ont pas ete modifiees. Aucun parcours Studio interactif
ni nouveau rendu n'est revendique par ces controles de transport.
