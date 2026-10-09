# Tidewater — atlas GPU source des débris scannés

Le constructeur ScannedDebris de la référence propre `1438b1ab` a assemblé
les douze images décodées par le navigateur en trois atlas GPU 2048². Le
programme source de génération des mipmaps produit douze niveaux par atlas.
Les deux acquisitions v1/v2 sont identiques octet pour octet, descripteur compris.

`scripts/tidewater/export-debris-scan-atlases.mjs` utilise le constructeur,
les quatre GLB et les 141 placements acquis. L'instrumentation ajoute uniquement
COPY_SRC aux textures dont le label commence par debrisScan_, afin de relire
les résultats GPU ; elle ne modifie ni l'assemblage ni le calcul des mipmaps.
Les hooks d'entrée alimentent le constructeur avec les pixels navigateur
déjà vérifiés. Le clone canonique reste inchangé.

Les sorties locales sont
`E:/OpenRPG_Forge/.tidewater-integration/debris-scan-atlases-20261005-v1`
et v2. Elles conservent LICENSE et CREDITS.md à l'identique.

- albedo : rgba8unorm-srgb ; normal et ARM : rgba8unorm ;
- sampler : clamp-to-edge, filtrage linéaire/trilinéaire, anisotropie 8 ;
- 36 niveaux au total, 67 108 860 octets de textures ;
- SHA-256 du descripteur : 276999b1e6d4188f76a7796b82e2443597d12f6f7707b49358bf53be52fd0c0e.

`scripts/tidewater/verify-debris-scan-atlases.py` relit indépendamment les
deux acquisitions. Il contrôle tailles, SHA de chaque niveau, formats,
sampler, provenance, alpha opaque et égalité des fichiers. Il compare aussi
chaque ligne des douze quadrants du niveau zéro aux pixels navigateur acquis.
Le reçu local `debris-scan-atlases-20261005-repeat.json` rapporte PASS :
trois atlas, 36 mipmaps, 67 108 860 octets vérifiés.

Cette preuve couvre les atlas source et leur reproductibilité locale. Le
lecteur des atlas Rust, le matériau scanned, les LOD, les draws natifs/WebGPU,
l'édition Studio et la comparaison visuelle restent à raccorder ou vérifier.
La réussite de l'acquisition ne vaut pas réussite du rendu produit.

## Contrat du matériau source

`export-debris-scan-material.mjs` appelle ensuite la fabrique originale
ScannedDebris::_createMaterial avec des wrappers Texture portant les pixels
acquis du niveau zéro. Aucune scène n'est dessinée. Le contrat conserve les
trois stages vertex/surface/shadow, les quatre modules et leurs bindings,
les attributs aAsset/iData, le varying vIData et les uniforms d'origine.
Les transitions LOD et leur discard dans la passe d'ombre, les normales par
dérivées, le sable de contact et l'effet mouillé restent dans les snippets.

Les acquisitions `debris-scan-material-20261005-v1` et v2 terminent code 0.
Une relecture Python indépendante compare tous les fichiers, leurs tailles
et SHA, les trois bindings d'atlas, les états front/opaque/depth-write et la
présence des fonctions de surface/shadow attendues. Les neuf payloads et
descripteurs sont identiques. Le reçu local
`debris-scan-material-20261005-repeat.json` rapporte PASS, SHA du descripteur
`d8aac6e8e5887f3b1ad194f550b619232c745966769c2b13a9873dd38ce6bfd5`.

Les bindings TerrainGPU initiaux servent uniquement de métadonnées de
contrat : ils ne prouvent pas l'éclairage de la scène. Les chaînes de mipmaps
restent référencées dans leur acquisition ; aucun mip n'est régénéré ici.
Ce contrat doit encore être adapté et validé avec les images résidentes,
les pipelines et les draws du renderer Forge.

## Première adaptation du shader scanned

`prepare-debris-scan-shader.py` reprend le receiver Village partagé, sans
copier une seconde pile d'éclairage. Les trois snippets source sont insérés
inchangés. Le terrain fournit ses fonctions UV/hauteur et son ombre solaire ;
cette dernière n'est appliquée qu'une fois par l'éclairage direct partagé.
Les trois atlas occupent les bindings 102–104 et leur sampler clamp/linear/
trilinear/aniso-8 le binding 105. La hauteur R32Float et les paramètres de
terrain restent des ressources à fournir par le runtime.

Le mapping de sommets garde l'ABI commune à cinq attributs : tint.x porte
aAsset et vdata porte iData (asset, random, fade, outgoing). Ce mapping
n'est pas encore alimenté par des meshes résidents scannés. Le varying
vIData et la fonction sourceShadow sont conservés ; la passe de casters
doit encore invoquer le prédicat source pour respecter le fondu des ombres.

Les préparations `debris-scan-program-20261005-v1` et v2 sont identiques,
descripteur compris. Le shader mesure 23 438 octets, SHA-256
`f1870e89558c87bf103d192fe179c27c4df901bd2d4e8a96d57ee9a8a566b0c6`.
Une relecture indépendante vérifie l'inclusion exacte des trois snippets,
le mapping des attributs, les bindings, l'absence de double modulation du
soleil et la correspondance du shader contrôlé par le reçu GPU.

Le validateur GPU existant accepte désormais explicitement le schéma scanned
et son nom de fichier, tout en conservant le chemin Nature. Le contrôle
`debris-scan-program-gpu-20261005-v1/result.json` termine PASS : une variante
uniform et un pipeline créés. Les imports Bevy caméra/mesh sont remplacés
par des stubs explicites. Aucun bind group de scène, upload des atlas dans
Forge, draw Bevy ou comparaison de pixels produit n'est exécuté.
Le reçu `debris-scan-program-20261005-repeat.json` conserve ces limites.

## Paquet autonome des ressources scannées

`package-debris-scans.py` réunit les acquisitions dans
`E:/OpenRPG_Forge/.tidewater-integration/debris-scanned-package-20261005-v1`.
Le paquet contient 83 fichiers, 68 848 404 octets. Son manifeste a SHA-256
`f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4`.
Il ne dépend pas du clone upstream pour charger les ressources acquises.

La préparation contrôle les révisions des descripteurs, les références
croisées entre géométrie, atlas et matériau, les douze meshes/141 instances,
et les 60 colliders scannés. Chaque instance a la même matrice et le même
quaternion que son propriétaire de collision. Les licences et crédits des
trois acquisitions sont identiques et conservés dans le paquet. Le sidecar
complet des collisions garde aussi les propriétaires procéduraux, sans
les déclarer comme contenu scanné.

Une relecture indépendante du dossier produit vérifie l'inventaire fermé,
les SHA/tailles de chaque fichier et des références de descripteurs, puis
les 36 SHA des mipmaps en découpant les trois payloads acquis. Le reçu
`debris-scanned-package-20261005-verified.json` rapporte PASS.
Cela prépare les entrées autonomes du lecteur Rust ; le lecteur, les meshes
résidents, les matériaux Bevy, les collisions physiques scannées et le
parcours Studio n'ont pas encore été exécutés avec ce nouveau paquet.

## Lecteur Rust préparé, export produit en attente

`crates/forge_assets/src/tidewater_debris/scanned.rs` ajoute un lecteur sans
filesystem ni dépendance GPU. Il vérifie le manifeste épinglé, un inventaire
fermé de 82 payloads, les cinq descripteurs, un plafond total de 80 MiB et
24 MiB par fichier. Les noms relatifs sont limités aux cinq dossiers connus
et à des feuilles ASCII simples. Les transports synchrones et asynchrones
sont injectés ; chaque réponse doit avoir la taille et le SHA acquis.

Le fichier n'est pas encore déclaré dans le module tidewater_debris : le
test physique 46364 utilise encore ses dépendances stables. Un harness
temporaire local (`scanned-reader-harness-20261005`) compile directement le
fichier réel via #[path], sans copie de l'implémentation ni modification de
la bibliothèque utilisée par ce test. Il ne s'agit pas d'une nouvelle crate
produit. Les dépendances serde/serde_json sont celles épinglées du dépôt et
forge_core reste le chemin réel ; le harness conserve son propre lockfile.

Deux tests passent après correction de deux diagnostics Clippy (argument
emprunté plutôt que passé par valeur, slice de caractères byte) : le vrai
paquet charge ses 141 instances et son atlas albedo de 22 369 620 octets ;
un octet d'albedo altéré est refusé. Un manifeste invalide est refusé avant
toute lecture et les chemins de traversal sont refusés. Le rerun termine
code 0, 2 PASS, 0 ignoré, build 1,48 s et tests 1,63 s.
Clippy all/pedantic avec -D warnings termine code 0 (0,27 s).
Le check wasm32-unknown-unknown du même fichier termine code 0 (5,39 s).

Ces preuves ne remplacent pas le raccord du module forge_assets, son check
dans le workspace, un transport réseau exécuté, le bootstrap natif/Studio,
les meshes/LOD résidents ou les draws. La prochaine étape est l'export du
lecteur une fois le test physique terminé, puis l'adoption des ressources.

## Sélection LOD source et première implémentation Rust

`export-debris-scan-lod-reference.mjs` exécute les méthodes source update
et _put inchangées, avec des récepteurs de batch observés. Il n'instancie
aucun remplacement de renderer. Les 141 placements sont testés autour des
deux transitions à 22/70 mètres et du fondu final à 260 mètres, en tenant
compte du rayon multiplié par six. Les caméras orthographiques regardent
vers l'objet ou à l'opposé. Chaque cas réinitialise le cache caméra ; le
réemploi de ce cache n'est pas qualifié par cette acquisition.

Le reçu `debris-scan-lod-reference-20261005-v1/reference.json` contient
7 332 cas, 5 912 582 octets, SHA-256
`ed31bc51b6592a8917b8dfcd6744881fdbc013549831e3c4ac07ab110def7454`.
Une relecture Python indépendante vérifie 4 911 sorties de bucket, leurs
matrices source, asset/random, fade/outgoing et population des casters.
Elle calcule indépendamment l'intersection de la sphère élargie de six
mètres avec ce frustum orthographique. Les fades f32 ont une erreur
maximale de zéro contre le calcul relu ; le reçu local de vérification passe.

`crates/forge_engine/src/tidewater_debris/scanned_lod.rs` implémente les
bandes en f64 et émet les fades en f32 comme le source. L'appelant fournit
l'intersection de frustum sur la sphère élargie ; les casters source sont
limités au LOD zéro. Les distances/rayons non finis ou négatifs sont refusés.
Ce fichier reste provisoirement hors de la déclaration du module moteur
pendant le test Rapier 46364. Le harness temporaire compile le fichier réel
et le compare aux 7 332 cas acquis. Le test passe ; lecteur et LOD réunis
donnent 3 PASS, 0 ignoré, build 2,30 s et tests 1,65 s. Après correction
de deux diagnostics doc_markdown, Clippy all/pedantic -D warnings passe.

Ces résultats couvrent la sélection CPU préparée. Ils ne qualifient ni le
frustum perspective vivant, ni les modifications de placement, ni le
cache caméra, ni l'alimentation des attributs GPU, ni la passe d'ombre.

## Décodage neutre des prototypes

Le sous-module préparé scanned/geometry.rs décode les positions, normales,
UV d'atlas et indices u32 des douze prototypes. Il conserve les 141 frames
source, matrices, quaternions et rayons sans appliquer une approximation
de rotation ou de scale. Il refuse les attributs absents/non finis, indices
hors limites, dimensions incohérentes, UV hors atlas, prototypes dupliqués,
identités de placement dupliquées et frames invalides. Les prototypes sont
décodés une fois et restent distincts des placements éditables futurs.

Le premier check du harness a signalé E0583 : son import #[path] nécessitait
un chemin explicite pour le sous-module geometry. La déclaration relative
scanned/geometry.rs corrige ce raccord sans toucher aux modules actifs du
test physique. Le test courant termine code 0 : 4 PASS, 0 ignoré, build
2,45 s et tests 1,72 s. Les quatre assets retrouvent au total les populations
LOD de 10 598, 1 999 et 478 triangles. Clippy all/pedantic -D warnings
termine ensuite code 0 en 0,58 s.

Le module assets préparé et le module de sélection LOD restent hors des
exports produit tant que la session physique 46364 compile. Le harness
valide leurs fichiers réels ; il ne constitue pas un lancement du jeu.

## Vues d'atlas prêtes pour l'adapter image

Le sous-module préparé scanned/atlases.rs fournit des vues empruntées des
trois chaînes RGBA8 dans l'ordre albedo/normal/ARM. Il conserve les formats
sRGB/linear, les douze niveaux, les dimensions et le sampler clamp/linear/
trilinear/aniso-8. Il vérifie à nouveau chaque SHA de mip et le découpage
mip-major sans décoder les JPEG, refaire un atlas ou générer des mipmaps.
Les tailles, formats, sampler, références et éventuels octets de fin
inattendus sont refusés. Ces vues n'allouent aucune image GPU.

Le harness courant termine code 0 : 5 PASS, 0 ignoré, build 2,02 s et tests
3,59 s. Le nouveau test contrôle les trois formats, les offsets du premier
et dernier mip, les douze niveaux et l'anisotropie acquise sur les fichiers
réels. Clippy all/pedantic -D warnings termine code 0 en 0,50 s.
Le fichier est toujours préparé hors des exports produit pendant la
compilation physique 46364. La conversion en Image Bevy, son upload,
les bind groups et les pixels de scène restent non exécutés.

## Préparation des instances et images Bevy

`derive-debris-scanned-instance-buffer.py` prépare une variante du programme
scanned qui lit iData dans un buffer storage au binding 106, indexé par
mesh_functions::get_tag. Cette API existe déjà dans les shaders Bevy du
dépôt. aAsset reste dans tint.x et les trois snippets source restent
inchangés. Le raccord évite de devoir copier les paramètres de variation
et de LOD dans la géométrie de chaque placement ; l'index MeshTag et les
records doivent encore être publiés et bornés par la scène vivante.

Le shader `debris-scan-instance-program-20261005-v1/scanned.wgsl` mesure
23 573 octets, SHA-256
`3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3`.
Le validateur crée un pipeline avec les imports caméra/mesh/tag stubés.
Le reçu indépendant vérifie le shader contrôlé, l'inclusion exacte des
trois snippets, le binding 106 et l'absence de double modulation solaire.
Aucun buffer d'instances réel, draw ou batching Bevy n'est qualifié.
Cette variante n'écrase pas le paquet standalone v1 précédemment vérifié.

`crates/forge_engine/src/tidewater_debris/scanned_images.rs` prépare la
conversion des vues d'atlas en Image Bevy : mip-zero acquis pour le
constructeur, puis chaîne complète acquise et mip_level_count source,
formats Rgba8UnormSrgb/Rgba8Unorm et sampler clamp/linear/aniso-8. Il ne
génère aucun mip et n'insère aucun asset avant préparation. Le fichier
est formaté mais demeure hors des exports moteur pendant 46364 ; il n'a
pas encore été compilé ou exercé comme adapter Bevy. La preuve WASM du
lecteur neutre ne remplace pas ce contrôle.

Le contrôle Bevy de l'adapter image est lancé dans la session 69476 : un
harness temporaire compile directement les fichiers source réels du lecteur
et de scanned_images.rs, avec les bibliothèques Rust 1.98/Bevy retenues dans
le cache. L'alias de namespace du harness expose uniquement le fichier réel
du lecteur non encore exporté ; aucune implémentation de remplacement n'est
copiée. Le test prévu compare les octets de chaque Image à la chaîne acquise,
ses dimensions, formats, mip count et anisotropie. La compilation reste
active lors de cette note : aucun PASS Image Bevy n'est anticipé. Ce contrôle
ne remplace pas une construction Cargo du module produit ou un upload GPU.

`scanned_material.rs` prépare séparément une extension du VillageMaterial
existant : hauteur au binding 100, paramètres terrain 101, albedo/normal/ARM
102–104, sampler acquis 105 et buffer d'instances 106. Les defaults source
opaque/front et roughness 0,85 sont appliqués en conservant les ressources
de lumière vivantes du matériau de base. Le shader fourni doit porter les
seams acquis exacts avant adaptation aux receivers communs. Le fichier est
formaté mais pas exporté ou compilé ; plugin, handles/images résidentes,
records MeshTag, passes temporelles et casters doivent encore être raccordés.

Le test physique 46364 reste confirmé actif. L'inventaire du 5 octobre
observe la progression CPU de son compilateur ; ni ce compteur ni la fin
d'autres compilations ne donnent de verdict de contacts. Les processus
externes sont conservés et aucune nouvelle compilation du moteur partagé
n'est lancée pendant ce contrôle.

`scanned_meshes.rs` prépare les douze meshes Bevy à partir du décodage
neutre déjà contrôlé : positions/normales acquises, UV d'atlas acquis et
indices u32 d'origine. Les quatre assets partagent leurs trois prototypes
LOD entre placements ; aucune transformation source n'est cuite dans une
copie de mesh par instance. tint.x porte l'ordinal de l'asset. L'attribut
DATA du layout commun est explicitement inutilisé par la variante shader
storage, qui lit iData par MeshTag ; il ne doit pas servir de fallback pour
un buffer résident absent. Les 141 frames restent séparées des prototypes.
Le fichier est formaté et hors des exports moteur, mais pas encore compilé
ou utilisé pour un draw. Le partage des handles et l'adoption des placements
communs doivent encore être implémentés et testés dans la scène vivante.

## Export du lecteur dans forge_assets

Après confirmation que la dépendance assets du test physique avait déjà
terminé sa compilation (sortie Compiling forge_engine observée), le lecteur
scanned est déclaré dans tidewater_debris.rs. Les bibliothèques compilées
du cache partagé restent intactes : la validation du nouvel export utilise
uniquement le cache séparé debris-reader-target-20261005-v2. Le test physique
46364 garde donc sa dépendance assets pré-export ; son verdict ne pourra
pas qualifier ce nouvel export ou les adapters moteur encore non déclarés.

Le contrôle produit `cargo test -p forge_assets --lib tidewater_debris::scanned
--locked --offline --target-dir <cache séparé> -- --include-ignored
--test-threads=1` termine code 0 dans la session 55598 : 4 PASS, 0 ignoré,
103 filtrés, build 27,79 s et tests 0,66 s. Les tests utilisent maintenant
le module réel du workspace, au lieu de sa seule déclaration dans un harness.
Clippy forge_assets --lib avec -D warnings termine code 0 en 4,95 s.
Le check WASM du module produit est lancé ensuite ; aucun lancement Studio,
upload d'image ou résultat physique n'est déduit de ces tests assets.

Le check `cargo check -p forge_assets --lib --locked --offline --target
wasm32-unknown-unknown` du nouvel export produit termine code 0 en 4,70 s.
Il confirme la compilation WASM du lecteur/décodage courant, sans prouver
un transport navigateur exécuté ni un rendu WebGPU.

## Correction du contrôle Image et préparation des placements

La session 69476 termine code 1 avant les tests : collision StableCrateId
entre deux bibliothèques serde retenues dans le cache. Aucun résultat Image
Bevy n'est déduit de ce run. Le diagnostic de fingerprint Cargo du forge_core
retenu identifie sa dépendance serde exacte (serde-272a0b2c07569019), au lieu
de choisir le fichier au timestamp le plus récent. Le contrôle est relancé
avec cette bibliothèque dans 51425, sortie neuve probe-v2.exe. Il demeure
pending lors de cette note ; la cohérence corrigée doit encore être confirmée
par sa terminaison et l'exécution de ses tests.

`scanned_placements.rs` prépare les 141 defaults comme ManualPlacement,
avec translation, quaternion et scale d'origine convertis dans le contrat
Forge f32 et validés. Les identités sont déterministes, les placements sont
triés et validés par PlacementChanges. Chaque variation source a une identité
d'asset commune associée au propriétaire original ; ses meshes GPU restent
les prototypes partagés du même ordinal source. Une duplication commune
peut conserver le même asset/rnd sans copier ou modifier l'acquisition.
La fonction ne change aucune session auteur ni aucun document persisté.

Ce fichier est formaté, hors export moteur et non encore compilé/testé.
Il ne prouve pas l'édition Studio, le chargement dans son catalogue, les
commandes de duplication/suppression ni leur persistance. Les sources du
moteur compilé par 46364 restent stables.

## Contrôles complémentaires du raccord — 5 octobre

La session 6269 du harness `scanned-reader-harness-20261005` termine code 0 :
2 tests PASS, aucun ignoré (build 17,17 s ; tests 1,69 s). Il importe désormais
le lecteur réellement exporté par forge_assets et les contrats forge_project.
Les fichiers moteur scanned_lod.rs et scanned_placements.rs restent chargés
par chemin dans ce contrôle isolé. Le premier test couvre les 7 332 cas LOD
acquis ; le second couvre les 141 placements initiaux, la retouche, la
duplication avec conservation de la variation source et le round-trip JSON.
Ce résultat remplace la mention « non encore compilé/testé » ci-dessus pour
ces deux fichiers seulement ; il ne prouve pas leur adoption ECS ni l'UI Studio.

La session Image 51425 termine code 1 à la liaison : LNK1181 sur
windows.0.52.0.lib, sans exécution des tests. La bibliothèque existe dans
le registre Cargo windows_x86_64_msvc-0.52.6/lib. Le lancement direct rustc
est corrigé par un chemin `-L native` explicite, sortie neuve probe-v3.exe,
session 56388. Son verdict reste en attente. Aucun upload GPU ni rendu
n'est déduit d'une compilation seule.

Le test physique 46364 reste actif lors de cette note, avec son processus
link.exe observé : il n'est ni relancé ni déclaré PASS. Le contrôle Clippy
du harness actualisé est lancé séparément dans 59047 ; résultat en attente.

59047 termine code 1 : le driver Clippy par défaut était 1.99.0, tandis que
les dépendances étaient compilées avec le rustc épinglé 1.98.0 (E0514).
La relance explicite `cargo +1.98.0 clippy --all-targets --locked --offline
... -- -W clippy::all -W clippy::pedantic -D warnings` termine code 0 dans
60116 en 7,82 s. Aucun cache n'a été supprimé pour cette correction.
Le check WASM du même harness est lancé ensuite ; sa compilation ne
constituera pas une preuve d'exécution navigateur ni d'édition Studio.

2196 termine code 0 : `cargo +1.98.0 check --lib --locked --offline
--target wasm32-unknown-unknown` sur le harness actualisé, en 6,48 s.
Les contrats de préparation des placements et le calcul LOD compilent
ainsi pour le navigateur ; leur intégration dans le runtime reste à faire.

## Projection des placements vers les records shader

`scanned_draws.rs` prépare atomiquement une frame depuis les placements
communs acceptés et les variations acquises. Chaque draw porte le placement,
son transform édité, le prototype asset/LOD partagé, un tag borné vers son
record iData et la décision de caster near-LOD. Les deux LOD d'une transition
ont deux records distincts ; une duplication garde la variation random source.
Les suppressions sont représentées par leur absence des placements acceptés.
Caméra invalide, mauvaise révision, variation absente et budget supérieur à
16 384 placements sont refusés avant publication. L'intersection frustum est
injectée par l'adapter caméra ; le rayon acquis est ajusté conservativement
au scale édité et augmenté de six mètres pour ce test de visibilité.

Le harness compile le fichier réel par chemin, sans modifier le graphe du
test physique encore vivant. Session 36679 : 3 PASS, 0 ignoré, build 5,02 s,
tests 2,97 s. Le nouveau test couvre retouche/duplication, tags distincts,
fade à mi-transition, caster near seulement, exclusion frustum, retrait et
refus d'une mauvaise révision/caméra NaN. Deux diagnostics Clippy de
documentation sont corrigés ; la relance termine code 0 en 0,26 s.
Ce fichier n'est pas encore exporté dans forge_engine et ne publie pas
d'entités Bevy ou de buffer GPU : l'adoption et le rendu restent à vérifier.

Le check WASM du harness avec cette projection supplémentaire termine aussi
code 0 en 0,31 s. Aucune exécution navigateur n'est déduite de ce résultat.

## Déclaration dans le moteur et adoption de dessins préparée

Les modules scanned_draws/images/lod/material/meshes/placements et
scanned_scene sont désormais déclarés dans forge_engine::tidewater_debris.
Cette déclaration intervient après la compilation Rust de 46364, alors que
son linker demeure vivant : elle ne modifie pas les objets déjà produits pour
ce test et son verdict éventuel ne qualifiera pas les nouveaux modules.
Le `cargo +1.98.0 check -p forge_engine --lib --locked --offline` du graphe
réel est lancé dans 76529 ; il attend le verrou du cache partagé, sans verdict.

`scanned_scene::ResidentDraws` prépare le partage de douze handles de meshes,
puis synchronise les draws sous des racines ManualPlacementProjection déjà
résidentes. Il vérifie prototypes, tags, population, records, références et
transforms communs avant mutation, publie le buffer de records f32 et réutilise
les entités des LOD inchangés. Les dessins retirés disparaissent sans modifier
les documents auteur. Une frame vide conserve une entrée de buffer non lue
pour une liaison storage valide, sans objet de remplacement. Le code est
formaté, mais sa compilation produit et son exécution restent en attente.
Il n'installe pas les racines communes, la physique ou les casters custom :
les NotShadowCaster évitent une route d'ombre Bevy non conforme ; la route
source near-LOD doit encore être raccordée et prouvée avant fidélité visuelle.

Le probe natif déclare également MaterialPlugin<ScannedMaterial>, pour que
son renderer possède le store/pipeline du matériau lors du prochain lancement.
Cette déclaration ne charge pas le package et ne constitue pas un draw observé.
Le diff ciblé est sans erreur de whitespace ; le check produit demeure en attente.

## Contrôle Image terminé et bootstrap scanné natif raccordé

56388 termine code 0 à la liaison corrigée. L'exécution de probe-v3.exe
avec la fixture scannée termine ensuite code 0 : 5 PASS, 0 ignoré, 0,62 s.
Le test Bevy vérifie les trois Image.data contre les payloads acquis, les
formats sRGB/linéaires, les dimensions 2048², les douze mips et l'anisotropie 8.
Les quatre autres tests couvrent le lecteur, les prototypes et les atlas.
Il s'agit du harness de fichiers réels ; aucun upload ni draw GPU n'est prouvé.

Le nouveau bootstrap exemple `tidewater_plant_probe/scanned_debris.rs` charge
FORGE_TIDEWATER_SCANNED_ROOT (package pin f7faf1...) et exige
FORGE_TIDEWATER_SCANNED_PROGRAM pointant vers le shader d'instances pin
3d54ba... ; le vieux shader inclus dans le package n'est pas substitué.
Les chemins des payloads sont canonicalisés et confinés au package, avec
lecture bornée. Les frames collision sont comparées aux frames geometry
avant préparation des primitives. Le setup réutilise terrain et éclairage
Village, crée les atlas Bevy et le storage, puis 141 racines communes avec
attente de colliders explicite ou waiver source vérifié.

Le système PostUpdate après UpdateFrusta calcule les LOD depuis la caméra
principale explicitement identifiée (joueur ou caméra statique du probe),
synchronise les draws sous les racines et recopie l'éclairage partagé.
Le contrôle de disponibilité du joueur inclut maintenant les placements
scannés si ce chargement est demandé. Ces raccords sont formatés et sans
erreur de whitespace dans le diff ciblé, mais non encore compilés/exécutés.
96823 contrôle l'exemple réel, après le check bibliothèque 76529 en cours.
Le casters custom scannés et la persistance Studio restent à raccorder.

## Compilation produit corrigée et couverture de profondeur WebGPU

76529 et 96823 terminent code 1 sur E0499/E0502 : le guard AssetMut du
ShaderBuffer restait vivant pendant les mutations d'entités. Un bloc local
borne maintenant cet emprunt avant ces mutations. La relance 25769
`cargo +1.98.0 check -p forge_engine --example tidewater_plant_probe --locked
--offline --target-dir <cache partagé>` termine code 0 en 1 min 03 s.
Cela couvre les modules moteur déclarés et le bootstrap scanné actuel.
Les warnings de copie du cache incremental et le dead_code préexistant
isolated_lod::run restent visibles. Ce check n'exécute pas le renderer.

Le module `scanned_shadow.rs` assemble les entrées de profondeur avec les
bodies scanned-vertex/scanned-shadow et le module lodFade acquis inchangés.
L'uniform contient deux matrices, iData et frameIndex ; les near-LOD devront
être soumis avec leur prototype asset correspondant. Le test du harness
exporte réellement le shader Rust dans scanned-depth-20261005-v1.wgsl
(1 PASS, build 3,11 s, test 1,19 s). Clippy du harness avec -D warnings
termine ensuite code 0 en 0,43 s. Le module est déclaré dans le moteur après
le check 25769 : ce check ne qualifie donc pas encore ce nouvel export.

`scripts/tidewater/validate-scanned-depth.mjs` compile ce shader exporté,
crée un pipeline depth32float sans imports Bevy simulés, dessine un quad
4×4 et relit chaque pixel de profondeur. Seize frameIndex, cinq fades et
deux directions donnent 160 cas et 2 560 pixels ; ils correspondent tous
au seuil Bayer source (profondeur 0,5 ou clear 1). Commande Node terminée
code 0, reçu local scanned-depth-gpu-20261005-v1.json PASS. La destruction
volontaire du device en fin de script produit le message device lost associé.
Ce test qualifie le discard de profondeur sur quad synthétique ; il ne
soumet ni meshes source, ni racines communes, ni cascades du graphe partagé.
Le batch GPU scanné et son raccord au graphe d'ombres restent à implémenter.

Le harness incluant scanned_shadow compile également pour
wasm32-unknown-unknown (code 0, 0,57 s). Le check du probe produit est
relancé après déclaration de ce module ; résultat à récupérer avant verdict.

## Raccord des casters scannés au graphe partagé

61743 termine code 0 après l'export scanned_shadow : check du probe en
47,76 s. Le raccord suivant ajoute scanned_shadow_runtime et son batch GPU
à la route de profondeur existante, sans seconde cible ou renderer.
Quatre prototypes near-LOD sont uploadés une fois ; les placements ont
des uniformes de matrice/iData distincts pour chacune des trois cascades.
Les poses issues du même calcul LOD que les surfaces activent seulement
les near-LOD. Le Bayer lit l'index de frame du graphe/animation partagé,
sans horloge supplémentaire. Les flux shader/meshes sont conservés en Arc ;
un changement de population reconstruit le batch, les poses inchangées
gardent les buffers, et l'extraction retire le flux lorsqu'il disparaît
du monde principal. Les updates de fade/pose et la récupération invalident
les couches nécessaires ; les changements de phase suivent les couches
du frame vivant. Cette cadence doit encore recevoir une preuve appariée.

Le bootstrap scanné publie désormais ces casters vers Target.image et exige
le graphe object-depth actif. 28529 termine code 0 : check produit du probe
avec ce raccord en 1 min 24 s. Les warnings de cache/dead_code préexistants
restent visibles. Deux tests de retrait du flux et de population/couverture
near-LOD sont ajoutés mais ne sont pas encore exécutés ni qualifiés.

5817 lance ensuite `cargo +1.98.0 build -p forge_engine --example
tidewater_plant_probe --locked --offline --target-dir <cache partagé>`.
Sa compilation/lien et le lancement natif doivent terminer avant de
déclarer un rendu scanné, une ombre observée ou une disponibilité physique.

## Premier lancement natif avec les deux lots

5817 termine code 0 : build du probe en 18 min 15 s. Exécutable SHA-256
7d3fd5b37697ffe64db40db9701952a374df82928fa8e315b389d811a1ea019f.
51651 lance ce binaire avec les inputs procéduraux/scannés ci-dessus et
preview-scene-bundle-fish-stand-content-fish-011, option water-spray-clouds,
workspace area e620dd19-ead9-5913-a6d6-0f93109148c0 et package buildings
a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6.
Le lancement termine code 0 et produit native-debris-scanned-20261005-v1/
island.png (1920×1080), object-depth.bin (50 331 648 octets), shadow.bin
et les readbacks ocean/spray. L'image est ouverte et inspectée : plage,
village, ponton, objets échoués et ombres sont effectivement visibles.
Un warning readback de fin de processus indique un canal déjà fermé ;
les sorties principales sont présentes, sans erreur/panique dans le journal.

La végétation annoncée est 65 383 visible, zéro exclusion et zéro retrait
de densité ; le head workspace reste f05a68ac... Cette capture ne prouve
pas seule que toutes les ombres visibles viennent du nouveau lot scanné,
ni une parité source ou la disponibilité physique du joueur. Une capture
procédurale seule au même point de vue est lancée ensuite, avec le même
binaire et les mêmes paramètres, sans les deux variables scannées ; son
image et sa profondeur seront comparées après terminaison.

## Contribution native des casters scannés confirmée

27527 termine code 0 avec le même binaire et les mêmes inputs, excepté
l'absence du package/shader scanné. Sa capture est également ouverte et
inspectée : les troncs/branches visibles au premier plan dans la capture
complète sont absents de la version procédurale seule.
`compare-scanned-native-depth.py` relit les deux PNG et les trois couches
de profondeur (2048² float32 chacune), vérifie tailles/finitude/plage et
conserve leurs SHA-256. Le reçu v2 termine DEPTH_ADDITIVE_PASS :
7 502 / 425 / 10 pixels sont strictement plus proches avec les scans dans
les cascades 0/1/2 ; aucun pixel n'est plus lointain. Les casters scannés
contribuent donc effectivement aux trois cartes du graphe natif.

Les RGB diffèrent sur 2 040 224 pixels, presque toute l'image : timing des
effets temporels, grain et simulation ne sont pas appariés. Ces différences
RGB ne sont pas attribuées aux seuls scans et ne constituent pas une
comparaison de fidélité source. Le premier reçu v1 PASS est conservé ;
v2 clarifie le verdict de profondeur uniquement et l'attribution RGB
UNQUALIFIED. Aucun verdict de couverture des 141 objets, de physique,
d'édition Studio ou de parité Three.js n'est ajouté par cette preuve.

## Parcours natif : échec de disponibilité et correction

64397 lance le walk-smoke avec les deux lots, même binaire et même bundle,
sortie native-debris-walk-20261005-v1. Les logs confirment 805 objets
procéduraux et 141 racines scannées, sur RX 7800 XT/Vulkan. Le run termine
code 1 après le timeout de 180 s : ticks=0, aucun déplacement ni bilan PASS.
Les fichiers de bootstrap eau existent, mais aucun succès joueur n'est déduit.

Inspection de physics/manual_projection.rs : manual_placements_ready
exige une égalité entre toutes les racines ManualPlacementProjection et
la liste accepted. Appeler cette fonction pour les 805 puis les 141
séparément refuse nécessairement un monde contenant les deux populations.
Le bootstrap combine maintenant les deux listes, les trie, puis appelle
une seule fois le checker commun exact. L'attente des roots demandés et
le contrôle de retrait backend procédural restent obligatoires. Le contrat
commun n'est pas affaibli, et un lot demandé encore absent reste indisponible.
La correction est formatée ; build et nouveau parcours doivent réussir
avant qualification joueur. Le run échoué et ses fichiers sont conservés.

La reconstruction corrigée est suivie dans 69602 ; compilation moteur puis
exemple observées, sans terminaison au dernier poll. Le pilote
scripts/tidewater/Verify-NativeDebrisWalk.ps1 prépare le replay reproductible :
il exige le SHA-256 du binaire choisi et un répertoire neuf, vérifie les pins
du package/shader scanné, restaure les variables de processus et consigne
le code retour ainsi que l'existence du bilan walk.json. Un PASS requiert
code 0, walk.pass=true et capture présente. Le parse PowerShell et le diff
ciblé passent ; le replay du binaire corrigé n'est pas encore exécuté.

Le pilote épingle aussi `debris-native.json` procédural, après relecture
SHA-256 effective : 63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216.
Son parse PowerShell passe après cet ajout. À 20:19 le 5 octobre, la session
69602 reste active en édition des liens (link PID 4340) ; le replay attend
toujours la terminaison réussie et le hash du nouvel exécutable final.

La session 69602 termine ensuite code 0 : build natif corrigé en 27 min 12 s.
Le nouveau binaire a le SHA-256
833c9d685f2fa97491d4b7aba19a726a16dd509e67aed75269fca549a5a18b18.
Le pilote lance réellement le replay complet dans la session 13955, répertoire
neuf `native-debris-walk-20261005-v2`. Son résultat reste à inspecter ; le build
réussi seul ne qualifie pas le déplacement.

13955 termine code 1 avec reçu effectivement écrit : le blocage des racines
est levé. `functional_pass=true`, 323 ticks dont 315 au sol, déplacement
8,9957657 m, caméra et backend stables, navigation des cascades et readbacks
herbe PASS. L'eau conserve time_scale=1,5 ; la simulation observée vaut
1,5239624 s/s et l'affichage moyen 73,72184 FPS (p95 15,912 ms).
Le verdict global reste FAIL : `shadow_cadence_pass=false`, ratios
soumissions/cascade0/1/2 = 1 / 1 / 0,90 / 0,85, au lieu de 1 / 1 / 0,5 / 0,25.
Le reçu `execution.json` garde pass=false, et aucune capture finale PASS
`island.png` n'est produite. La capture auxiliaire `island.wind-b.png` est
ouverte et inspectée : plage, échoppe, cabane, ponton, végétation, débris,
eau et capsule joueur sont visibles ; ce cadrage ne prouve pas la parité source.

Le graphe forçait les trois couches pour chaque changement de pose scannée,
y compris le fade/visibilité dépendant de la caméra. Le raccord est corrigé
pour publier ces poses sur les couches de la cadence live existante ; sans
frame live, le fallback conserve trois couches. Les changements de géométrie
et la récupération gardent leur rafraîchissement complet. Le contrôle
walk_shadow_rate n'est pas changé. Format/diff ciblé passent ; reconstruction
10725 en cours avant nouveau replay, donc cadence corrigée non qualifiée.

La règle est également relue dans le clone source : `src/engine/render/Shadows.js`
définit periods=[1,2,4] et sélectionne `(frame+i)%periods[i]==0`, sauf mouvement
du soleil ou cascade dirty. HEAD est vérifié à
1438b1abfcaee3267092b75573014f4d9b4a983c et status --porcelain vide. Le refus
initial Git pour ownership sandbox est résolu avec une option safe.directory
locale à la commande, sans changer de configuration globale ni modifier le clone.

La commande locale `scripts/tidewater/Walk-Tidewater.ps1`, appelée par
`Jouer-Tidewater.cmd`, utilisait encore preview-scene-bundle-clear-004 et
n'injectait aucun lot Debris. Elle pointe désormais vers le même bundle
fish-stand-content-fish-011 que le replay natif et configure les deux lots
et le shader scanné explicitement. Les trois pins d'entrée sont vérifiés
avant lancement ; les variables Debris/static-postfx sont restaurées à la
sortie. Le build éventuel sélectionne Rust 1.98.0. Parse PowerShell et diff
ciblé passent ; la commande utilisateur elle-même reste à exécuter après le
build corrigé. Ceci ne qualifie pas le raccord du launcher Aube ni un paquet
portable : ces deux livrables restent ouverts.

## Raccord du candidat de distribution Aube

Le checkout launcher prototype/tauri-game-launcher est vérifié propre avant
modification. `play.rs` exige maintenant les trois marqueurs de contenu sous
`content/debris/` et prépare les variables procédurales/scannées depuis le
dossier extrait, en supprimant l'override static-postfx hérité. La préparation
de commande est isolée de spawn pour contrôler ses arguments et son
environnement. `main.rs` expose `--verify-debris-runtime` seulement en
production ; le marqueur attendu est FORGE_LAUNCHER_DEBRIS_RUNTIME_V1.

Build-WindowsPlaytest.ps1 exige ce nouveau raccord et les trois entrées
explicites, vérifie leurs pins, copie tous les fichiers sous content/debris
et compare chaque SHA-256 copié. Ce contenu reste hors data/ afin de conserver
la portée exacte du manifeste de scène ; le manifeste final de distribution
le couvre. Parse PowerShell et diffs ciblés passent.

Un harness std-only compile réellement le play.rs actuel, en substituant
uniquement l'enum de réponse frontend ; il ne construit pas Tauri. Premier run
50529 : 1 PASS/1 FAIL sur une comparaison de texte de chemins Windows slash vs
antislash. Le test compare maintenant les Path, sans changer la commande.
45591 : 2 PASS/0 FAIL/0 ignoré en 9,54 s : distribution déplacée avec espaces,
environnement explicite et détection des fichiers manquants. Les exécutables
de contrat v1/v2 sont conservés sous .tidewater-integration. Le README du
lanceur décrit les nouveaux fichiers et paramètres.

Le launcher de production n'est pas encore reconstruit, aucun ZIP nouveau
n'est assemblé et aucun parcours extrait n'est qualifié. Le build moteur
10725 reste actif ; la prochaine preuve cadence doit venir du replay de son
binaire final, dans un nouveau répertoire v3.

La reconstruction de production du launcher est effectivement lancée :
9225 termine code 1, avant compilation, parce que beforeBuildCommand appelle
npm absent de l'environnement. Le script frontend ne dépend d'aucun paquet
npm ; 59230 exécute directement scripts/build-aube.mjs avec le Node fourni
et --check scripts/desktop.js, code 0. Il annonce 68 images et conserve le
HTML source SHA-256 35a7a2d5b6ab088d4a539655c5112f0eabdfaec1586cf35a5c6f0be61c104c8b.
Le dist généré avait été vérifié comme répertoire normal, sans lien/reparse,
dans le checkout launcher avant cette régénération.

36487 relance cargo-tauri build --no-bundle --features production --locked
--offline, Rust 1.98.0, même cache .build/cargo-target, avec un overlay local
launcher-debris-build-20261005-v1.json supprimant uniquement beforeBuildCommand
après la génération frontend réussie. La configuration versionnée est intacte.
Cette session reste à suivre ; aucune réussite de build production, aucun
marqueur CLI neuf et aucun ZIP extrait ne sont encore déduits.

Le contrôle du raccord est ensuite corrigé pour rester sûr avec un ancien
launcher : l'assembleur appelle `--verify-distributable --debris-runtime`.
Un ancien binaire reconnaît le premier argument et retourne son ancien
marqueur, que l'assembleur refuse ; il n'ouvre donc pas sa GUI pour un flag
inconnu. Le main actuel retourne le marqueur Debris uniquement avec ce second
argument et la feature production. L'ancien exécutable release est réellement
testé avec les deux arguments : code 0, FORGE_LAUNCHER_AUBE_FRONTEND_READY,
sans nouvelle fenêtre. README et script suivent cette forme ; l'ancien flag
standalone --verify-debris-runtime décrit plus haut n'est plus utilisé.
La session launcher 36487 était encore à l'introspection Cargo, sans rustc
launcher observé au moment de la correction. Les deux builds restent à suivre.

36487 annonce ensuite la compilation de forge_launcher_prototype v0.4.0.
Une vérification indépendante rouvre les 68 images du site Aube et les 68
copies dist/assets : inventaires, tailles et SHA-256 concordent tous.
Le reçu neuf launcher-debris-aube-copy-20261005-v1.json garde les 68 hashes,
pass=true (Node code 0). Ceci atteste la conservation des images, pas la
fenêtre Tauri ni un parcours extrait. 10725 reste actif au dernier poll.

Préparation de l'assemblage : Get-Command python ne trouve aucun exécutable
dans le PATH courant. Build-WindowsPlaytest.ps1 accepte désormais
-PythonExecutable (défaut python), résout l'application avant toute copie,
puis l'utilise pour les notices Rust et la vérification du bundle. Le Python
du runtime fourni est réellement résolu par son chemin absolu ; parse et diff
ciblé passent. Il s'agit d'un outil de construction, pas d'une nouvelle
dépendance du paquet destinataire. Le README launcher est complété.

10725 termine code 0 après 48 min 26 s. Le binaire corrigé pour la cadence
scannée a le SHA-256 b8990c25c58a9a5fe27f4ece81da3e7c8cf7813d3289df67d4c782b77e12a174.
13847 lance réellement le même pilote et le même parcours, dans le répertoire
neuf native-debris-walk-20261005-v3. Le bootstrap confirme la RX 7800 XT/Vulkan,
les 65 383 instances végétales et le head f05a68acc294b586940237565c77fe2f7679ae9d2b77f4596f0380680665d50f
inchangé. Le résultat cadence doit encore être inspecté ; 36487 reste actif.

13847 termine code 0. Les reçus island.walk.json et execution.json sont
réellement relus : pass=true, functional_pass=true, cadence_pass=true,
327 ticks dont 319 au sol, 8,9957657 m parcourus, caméra/backend stables.
shadow_cadence_pass=true ; ratios soumission/cascade0/1/2 =
1 / 1 / 0,5018868 / 0,2490566. La cadence source est donc acquise pour ce
parcours avec les deux lots, sans changement des critères du smoke.
Eau time_scale=1,5, simulation observée 1,5264336 s/s ; affichage moyen
75,93891 FPS sur 275 intervalles, p95 14,8595 ms, 1% low 22,84452 FPS.
Ces chiffres sont des intervalles main-frame, pas des timestamps GPU.

island.png est produite et ouverte : cabane, échoppe, ponton, plage,
végétation, débris, eau et capsule de test visibles. La capture qualifie
ce parcours de contrôleur natif, pas un personnage final, toutes les
collisions de la carte, un parcours clavier OS, la parité Three.js ou
l'édition Studio. Les logs confirment 805 objets et 141 racines scannées.
Les warnings de readbacks fermés à l'arrêt sont conservés ; le bilan est PASS.
Le launcher 36487 reste actif, aucun nouveau ZIP n'est encore qualifié.

Le harness du play.rs actuel passe aussi clippy-driver 1.98.0 avec all,
pedantic et -D warnings (code 0). Cette preuve reste ciblée, sans compiler
le frontend Tauri du harness. 36487 termine ensuite code 0 : build Tauri
release de production en 34 min 38 s. Le CLI annonce un nom underscore
inexistant ; l'inventaire du dossier identifie le vrai bin explicite
forge-launcher-prototype.exe, 230 950 400 octets, modifié le 5 octobre.
Son SHA-256 est 25724b8407cc0d0f77cf9ee8d707c59a01e131e7496954fdefa3574c70ba0478.
Les deux commandes CLI réelles terminent code 0 et retournent respectivement
FORGE_LAUNCHER_AUBE_FRONTEND_READY et FORGE_LAUNCHER_DEBRIS_RUNTIME_V1.
La tentative sur le chemin annoncé inexistant n'est pas une validation ;
le contrôle final utilise ErrorActionPreference=Stop et le fichier inventorié.

85065 lance réellement Build-WindowsPlaytest.ps1 avec le runtime b8990c25,
ce launcher, le bundle fish-stand-content-fish-011, les deux lots, le shader
instance acquis et le Python fourni. Destination neuve
OpenRPG-Forge-Aube-Tidewater-Win64-20261005-debris-001 ; assemblage/ZIP en cours,
à suivre avant extraction et parcours. Aucun PASS portable n'est encore acquis.

85065 termine code 0 : candidat 001, 7 651 fichiers manifestés,
884 902 278 octets, ZIP SHA-256
1388960770c27e2288f2450f2cf12630ded9ccdd5955725023e380fa083220d7.
Le bundle de scène interne vérifie 1 655 fichiers / 427 187 561 octets,
manifest SHA b5d94519dd2c6534847e20410a0014579762a7ac7177087720208cb52b65d765.
Les notices couvrent 596 packages et signalent 48 sans texte trouvé ; cette
limite est conservée, sans qualification juridique ajoutée.

92009 vérifie le pin ZIP et commence une extraction neuve. Elle échoue
code 1 sur un source-blob dont le chemin dépasse la limite Windows : le ZIP
ajoutait le nom long du candidat à l'intérieur du dossier d'extraction.
L'extraction partielle v1 et le ZIP 001 sont conservés comme preuves FAIL.
L'assembleur crée désormais un ZIP à fichiers racine, sans ce dossier
englobant redondant. Le contenu relatif et les exécutables ne changent pas.
74780 reconstruit un candidat 002 neuf ; résultat encore à suivre.

verify-windows-playtest.py vérifie quotas, chemins, liens/doublons, SHA du ZIP,
inventaire/tailles/hashes manifestés et pins des deux exécutables et débris.
Il reconnaît l'ancien format englobant et le nouveau format racine.
Verify-NativeDebrisWalk.ps1 accepte maintenant -DistributionRoot : seul
le binaire extrait est contrôlé, inputs sous data/content, cwd du paquet,
variables/restauration habituelles. Cette voie attend une extraction complète
avant exécution ; aucun PASS extrait n'est encore déclaré.

### Candidat portable 002 : extraction et runtime extrait PASS

Les sessions 74780 (assemblage) et 98543 (extraction) terminent code 0.
Le ZIP `OpenRPG-Forge-Aube-Tidewater-Win64-20261005-debris-002.zip` conserve
les 7 651 fichiers manifestés, 884 902 278 octets de payload, et porte SHA-256
`e246f537cff4869e425be77335f6cfc87395b6efbbb2b3868fc43895a7dbba76`.
Le format à racine plate supprime le segment de chemin responsable de
l'échec du candidat 001, dont le ZIP et l'extraction partielle sont conservés.
Le reçu `portable-debris-extraction-20261005-v2.json` confirme tous les
inventaires, tailles, SHA et cinq pins de composants. Aucun override de
chemin long n'a été nécessaire pour cette extraction.

La session 55677 termine code 0, depuis
`E:/OpenRPG_Forge/.tidewater-integration/portable-debris-extracted-20261005-v2`.
Les deux marqueurs CLI du launcher sont présents ; le helper utilise ensuite
directement le binaire extrait avec les arguments et variables du raccord jeu.
`portable-debris-walk-20261005-v1/execution.json` déclare pass=true et le
binaire `b8990c25c58a9a5fe27f4ece81da3e7c8cf7813d3289df67d4c782b77e12a174`.
Le reçu walk confirme pass/functional/cadence/shadow_cadence=true,
8,995765686 m de déplacement, 325 ticks au sol, water_time_scale=1,5 et
1,532156028 seconde simulée par seconde observée. Moyenne 77,63685 FPS,
1 % low 22,12099 FPS sur ce parcours local, sans qualification GPU timestamp
ni benchmark global. `island.png` est réellement inspectée : cabane, étal,
jetée, végétation, débris et eau visibles, capsule bleue du contrôleur de test.

Cette preuve n'est pas un clic GUI du nouveau launcher, une exécution sur
une autre machine, une couverture de toute la carte, une validation Studio
des débris ou une comparaison visuelle appariée avec Three.js. Les warnings
readback à la fermeture restent dans le log. Aucun commit, push ou gate clos.
