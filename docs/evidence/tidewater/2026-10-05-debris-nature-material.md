# Tidewater — Nature sur le rendu partagé Bevy

Suite de [l'adaptateur mesh Debris](2026-10-05-debris-mesh-adapter.md).
L'objectif complet reste actif. Aucun rendu de la couche entière, geste Studio,
verdict de parité ou clôture de gate n'est déclaré.

## Programme acquis et ressources de terrain

`scripts/tidewater/prepare-debris-nature-shader.py` réouvre les références
SHA/tailles du paquet Debris v3 et de la bibliothèque Village a53b6987…99e6.
Les corps vertex et surface Nature sont conservés. Les modules common,
lodFade, terrainShading et debrisStone conservent leurs calculs ; common utilise
l'adaptation de caméra/profondeur déjà bornée par le préparateur Village.
Les fonctions terrainUvOf et terrainHeightAt sont extraites sans changer leurs
corps. Les helpers TerrainGPU non appelés par Nature, dépendant des cartes
normale/splat/shore, ne sont pas ajoutés à cette interface.

Le footer, le calcul d'éclairage direct/environnement et ses raccords de scène
proviennent du shader Village partagé. Sa fonction terrainSunShadowAt est
remplacée par la fonction source utilisant les paramètres de domaine et le
flag de cuisson fourni par la scène. Cette ombre est appliquée une seule fois
dans l'éclairage direct ; aucun second multiplicateur solaire n'est ajouté.

La caméra source de LOD utilise la position de la vue. Le frameIndex Bayer
utilise shadowParams.frameIndex, puis currentShadowParams().frameIndex après
adaptation par le receiver vivant. La synchronisation du compteur dans la
scène complète reste à qualifier ; conserver ce raccord ne la prouve pas.

Le candidat `debris-nature-program-20261005-v2` conserve nature.wgsl (43 391 bytes)
et program.json avec empreintes des contrats et du préparateur. La v1, avant
l'ajout du frameIndex et l'écriture binaire canonique du WGSL, reste une étape
préparatoire non qualifiée. Le fichier v2 est écrit en bytes pour éviter une
divergence SHA provoquée par la conversion des fins de ligne Windows.

Ressources supplémentaires attendues :

- binding 100 : hauteur réelle du terrain R32Float, non filtrable ;
- binding 101 : détail terrain RGBA8Unorm et sa chaîne de mips source ;
- binding 102 : sampler répétitif linéaire avec anisotropie 4 ;
- binding 103 : domaine/résolutions et direction/flag de cuisson du soleil ;
- binding 12 existant du matériau de base : carte réelle d'ombre du relief.

La source d'acquisition des usines de matériaux utilisait un soleil non cuit
et un shore placeholder. Ces valeurs ne sont pas adoptées comme scène finale :
le matériau exige les ressources et paramètres vivants du terrain.

## Adaptateur moteur

`crates/forge_engine/src/tidewater_debris/nature.rs` expose
ExtendedMaterial<VillageMaterial, NatureExtension>. Il réutilise les receivers
du moteur pour ombres d'objets, caustiques, rebond, lumières locales et buffer
d'éclairage partagé, avec un handle shader distinct. Aucune seconde pile GPU.
Les uniformes Nature acquis restent blanc/opaque, roughness 0,8, metallic 0,
emissive nulle et intensité environnement 1. Les transforms des meshes suivent
le chemin Village commun. Les casters préparés séparément restent à adopter.

prepare_material contrôle le domaine, les résolutions et la direction/flag du
soleil. Il n'alloue aucun placeholder ; le propriétaire de la scène doit
fournir les textures réelles avec le sampler source. Ce constructeur ne prouve
pas leur format effectif, leurs pixels, leur résidence ou leur raccord Studio.
stage_shader refuse une interface dérivée différente ou déjà adaptée, avant
l'installation. Le module est formaté. Le nouveau `cargo check -p forge_engine
--locked` termine code 0 en 4 min 32 s (session 76868), avec Rust 1.98.0 fixé.
Les warnings de hardlinks du cache ont entraîné des copies de fichiers, sans
échec de compilation. Ce check n'exécute aucun draw ni test moteur.

Le nouveau test réel `cargo test -p forge_engine --lib tidewater_debris --locked
-- --include-ignored` est ensuite lancé avec les fixtures Debris v3 et Nature v2.
Il doit rejouer la projection et exporter le shader adapté par Rust dans le
dossier neuf `debris-nature-rust-prepared-20261005-v1`. Son verdict reste pending
tant que la session n'a pas terminé ; le test précédent PASS reste daté.

## Validation WebGPU bornée

`scripts/tidewater/validate-debris-nature-shader.mjs` vérifie l'empreinte et la
taille du programme et exige le clone source propre épinglé. Le device headless
source compile le shader et crée un pipeline vertex/fragment RGBA16Float,
triangles, opaque et culling arrière. Les attributs position/normale/UV/tint/
vdata conservent leur interface.

`debris-nature-gpu-20261005-v1/result.json` termine code 0 avec un pipeline
acquired/uniform créé. Les imports caméra/mesh Bevy sont explicitement remplacés
par des stubs de sonde : ce résultat ne prouve pas un draw dans Bevy. Aucun
bind group contenant les vraies maps n'est créé, aucun pixel n'est comparé.
Le script accepte aussi le programme adapté par Rust pour vérifier les chemins
uniform/storage du receiver ; cette seconde qualification reste à lancer après
le nouveau test moteur.

Prochaine preuve attendue : export du shader réellement adapté par Rust,
création des pipelines correspondants, puis staging de la couche dans la scène
avec ses maps réelles, ombres et placements éditables. PebbleField et
ScannedDebris gardent leurs travaux distincts, sans remplacement approximatif.

Suite : [adoption des placements et casters Debris dans Bevy](2026-10-05-debris-scene-staging.md).
Le test 16821 a depuis terminé code 1 sur un mélange d'interface assets/moteur
pendant la compilation ; le nouveau check cohérent et les limites sont décrits
dans cette suite. Aucun export du shader adapté n'a été obtenu par ce test.
## Résultats supplémentaires du 5 octobre

Le run moteur 15076 termine code 0 avec les trois tests Debris PASS, dont
l'adaptation Nature et son export WGSL réel par Rust. Le fichier exporté est
`debris-nature-rust-prepared-20261005-v1/nature.wgsl` dans le dossier local
`.tidewater-integration`.

Le validateur `validate-debris-nature-shader.mjs` termine code 0 sur
`debris-nature-gpu-20261005-v2` : trois pipelines passent, programme acquis avec
uniforms, programme Rust avec receivers uniform puis storage. Les imports
camera/mesh Bevy sont des stubs de contrôle. Aucun draw, bind group réel,
pixel terrain ou parité de scène complète n'est prouvé par ce contrôle.
