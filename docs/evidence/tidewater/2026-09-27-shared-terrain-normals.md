# Normales et AO partagees — 27 septembre 2026

Le bake normal/AO et le graphe GPU des mips sont transferes du prototype vers
forge_engine::tidewater_terrain::normals. L'adapter natif utilise maintenant ce
module. Le bake exige un raster 2048 carre sur grille metrique et une chaine
RGBA8 complete, controle les hauteurs finies avant mutation et conserve B (roche)
ainsi que les mips inferieurs jusqu'au passage GPU. Le port AO conserve ses calculs
et son attribution MIT Tidewater.

Chaque Shared::stage alloue une image et un indicateur de generation distincts.
Le graphe invalide ses anciennes ressources lors du remplacement et verifie la
generation avant encodage. Un mode desactive retire la preparation precedente.
La readiness signifie commandes encodees, pas fence de terminaison GPU.
register est idempotent et refuse une application sans renderer initialise.

Le nouveau probe CPU couvre retouche/retour au document initial, preservation du
masque roche et des mips inferieurs, rejet d'une cible tronquee sans mutation,
rejet avant allocation d'image et independance des indicateurs de generation.
Execution CPU et rejeu natif passes ; voir les resultats ci-dessous.
Clippy strict moteur et probe : PASS, session 85773 (27,16 s).
Compilation native PASS dans la meme session, 7m36s.

Le raccordement au terrain visible du Studio reste ouvert. Le port partage ne
constitue pas une preuve d'edition interactive.


Test de remplacement GPU ajoute : tidewater_normals_reload_probe. Il injecte deux
images apres startup, puis copie et relit le mip 11 calcule sur le GPU. Les valeurs
RG/A attendues sont constantes et differentes entre generations ; B doit rester
celui du mip source (61). La double registration et le rejet d'une entree tronquee
avant allocation sont aussi controles. Ce test ne prouve pas les performances
interactives ni le recalcul du masque roche. Clippy strict et compilation PASS,
session 93313. Execution PASS : generations adoptees aux frames 8 et 13, mip 11
respectivement [37,71,61,200] et [91,23,61,180]. 11 passes GPU par generation.
Log : E:/OpenRPG_Forge/.tidewater-integration/shared-normal-reload-001.log.


## Resultats sur la reference locale

CPU : normale/AO de reference reconstruite sans aucun texel modifie. Une correction
de 12 m au coin (1024,1024) modifie 12 texels de normales et 21 texels d'AO.
Retour au document initial identique ; rejet d'une cible tronquee sans mutation ;
masque roche et mips inferieurs inchanges avant le passage GPU. Les deux indicateurs
de generations sont independants. Log : shared-normal-cpu-001.log.

Scene complete native : PASS, session 54959. PNG, ombre du relief et profondeur
objets identiques octet par octet a native-shared-terrain-textures-001.
Capture : E:/OpenRPG_Forge/.tidewater-integration/native-shared-terrain-normals-001.png.
Preuve et empreintes des trois executables : shared-terrain-normals-proof-001.json.
Les preuves locales sont dans E:/OpenRPG_Forge/.tidewater-integration.
Cette comparaison porte sur la vue fixe Forge, pas sur une edition Studio interactive.


Studio WASM : PASS, session 78344, 40,32 s.
Commande : cargo check --locked --offline -j 1 -p forge_studio --lib
--no-default-features --features web --target wasm32-unknown-unknown.
RUSTFLAGS=-C debuginfo=0 ; avertissement futur proc-macro-error2 preexistant.
