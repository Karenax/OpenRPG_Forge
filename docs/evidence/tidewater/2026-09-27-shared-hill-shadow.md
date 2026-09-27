# Ombres de relief partagees et remplacement apres startup

Le pipeline GPU de relief utilise par le probe est maintenant dans
forge_engine::tidewater_hill_shadow : meme uniforme, bind group et dispatch
64 x 64. Le shader est fourni explicitement et installe dans Assets<Shader>,
sans chargement implicite par chemin AssetServer. Le probe de scene utilise
PreparedInput::stage pour ses ombres actives ; son ancien cas sans ombres garde
son image neutre 1x1.

La preparation des mipmaps est partagee dans heights.rs : moyenne source 4x4,
reduction par maximum et conversion half avec troncature. Les hauteurs doivent
etre 2048 carre et finies. PreparedInput valide la chaine half de 699050 octets,
le domaine, la direction solaire et la taille du shader avant adoption.
Chaque paire d'images conserve son signal de generation ; seuls les bindings
correspondants peuvent signaler les commandes encodees. Ce signal ne constitue
pas une fence de completion GPU.

## Preuve native ciblee

PASS : session 21805, exit 0. Le probe autonome attend huit frames avant de
calculer avec un soleil vertical, puis adopte la direction source a la frame 17.
Les deux sorties sont lues sur le GPU, 2097152 octets chacune.

- Generation initiale : 306d3925218344c073ae6b0d955cbc77146f850cefd7a5e066b6160129b73d20.
- Generation remplacee : a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- La seconde est strictement egale a native-bundled-scene-001.shadow.bin.
- La premiere differe, ce qui exclut une simple conservation de l'ancien resultat.
- Refus verifies : half Inf, chaine tronquee, soleil nul, shader vide et adoption
  sur renderer non enregistre sans allocation d'image.

Rapport local : E:/OpenRPG_Forge/.tidewater-integration/late-hill-shadow-proof-001.json.
Executable SHA256 : 9847b6e6d4bec3ef6c3b0d786eaa14c7fce5b8f2aa7d12ddad4f96b2b882111b.

Commande du probe : cargo run -p forge_engine --example tidewater_hill_shadow_probe
--locked --offline -j 1 -- SCENE_ROOT NEW_OUTPUT EXPECTED_SHADOW_BIN HEIGHTS_BIN.
Le blob des hauteurs est fourni explicitement depuis le workspace et son hash
est verifie contre shadow.json. Le premier lancement avait suppose a tort un
heights.bin sous scene et a echoue avant le GPU ; ce chemin a ete corrige.
Le terrain de reference est sampled_raster_v1, base 0, sans correction auteur.

## Limites et suite

Le rendu complet native-shared-hill-scene-001 a termine (session 83530, exit 0).
Image inspectee, byte-identique a native-shared-cloud-scene-001.png (SHA256
243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78).
Les ombres du relief (2097152 octets) et des objets (50331648 octets) sont
strictement identiques a native-bundled-scene-001. Le PNG differe de cette autre
capture de 14 pixels, maximum 2/255, variation deja documentee avant extraction ;
sa cause precise n'est pas etablie. Rapport shared-hill-scene-proof-001.json. Aucun verdict
Studio n'est acquis : transport auteur, ombres des objets et raccordement des
materiaux restent ouverts. Le candidat Studio 001 ne contient pas ce changement.

Pour les builds natifs, conserver RUSTFLAGS absent : les empreintes des probes
precedents utilisent rustflags=[]. Le lancement avec -C debuginfo=0 a ete arrete
explicitement, puis relance apres terminaison constatee pour reutiliser le cache.
Les builds et temporaires restent sous E:/OpenRPG_Forge/.build.

Controle Clippy cible du moteur et du nouveau probe : PASS avec --no-deps
(session 30802). Le controle incluant les dependances s'arrete sur deux
needless_pass_by_value preexistants dans forge_project/src/zone_authoring/session.rs
lignes 349-350. La seule correction locale issue du controle est le balisage
Markdown de R16Float dans un commentaire ; aucun comportement GPU n'a change
apres les captures. Le check Studio WASM final passe (session 93410), avec seulement le
avertissement de compatibilite future proc-macro-error2 deja connu.
