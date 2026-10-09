# Tidewater — couche Debris et décors absents

Suite du [diagnostic de projection](2026-10-05-animation-clock-and-wind.md).
La capture Studio v3 charge les 27 village_props et 43 beach_props, sans
écart CPU de placement. Les caisses près du stand et les deux tonneaux à
droite restent absents de l'image comparée à la référence v13.

## Propriétaire source identifié

À la révision `1438b1abfcaee3267092b75573014f4d9b4a983c`, `App.js` crée
`Debris` après Village, Vegetation et Rocks. Cette couche est distincte
des méthodes Village déjà acquises. `DebrisPlacement::_village` émet
notamment les groupes `crates` en X34,4/Z−114,4 et `barrels` en
X47,6/Z−114,8. Ces centres correspondent aux éléments manquants autour
du stand. Le premier export source conserve explicitement `debris` dans
sa liste des éléments manquants ; la bibliothèque du candidat v3 n'a
pas de paquet Debris.

La couche comprend aussi les décors des maisons, barques, brouettes,
bois flotté, végétaux échoués, coquilles, coraux, déchets, pierres de
crique, un champ de petits cailloux et des instances de modèles scannés.
La suite doit couvrir cette couche entière, pas seulement les deux groupes.

## Inventaire indépendant reproductible

`scripts/tidewater/export-debris-placement.mjs` observe les générateurs
CPU épinglés avec le shim headless existant. Il construit TerrainData,
Village, Vegetation et Rocks dans leur ordre source, puis DebrisPlacer,
avec sa graine originale 5150 et leurs collisions/obstacles. Les shaders
TerrainGPU de Rocks n'interviennent pas dans son placement CPU ; ce
contrôle ne reproduit pas leurs pixels.

Deux exécutions terminent code 0 sous Node v24.19.0. Leurs trois fichiers
sont identiques octet pour octet : `debris-placement-inventory-20261005-v1`
et `v2`. Le reçu `debris-placement-inventory-20261005-repeat.json` est PASS
dans cette portée CPU uniquement. Le SHA du JSON d'inventaire est
`345523dca1157f63ae6774bed0b33a716880151fe510e4aad1c4d7dd5eac8837`.

- 766 entrées du journal de placement, groupes et éléments individuels ;
- 141 instances scannées, comptage distinct qui ne doit pas être additionné
  aveuglément au journal ;
- batches bruts wood, hard, nature, net et rope, comptages et digests ;
- les deux groupes de place identifiés sont effectivement acceptés aux
  coordonnées exactes indiquées ci-dessus ;
- toutes les sources suivies restent inchangées, vérifiées par SHA et Git.

Le premier essai s'arrête avant extraction sur la protection Git de propriété
du sandbox. Le correctif emploie `safe.directory` pour cette invocation
seulement et ne modifie aucune configuration Git globale.

## Provenance et limites

La licence MIT source et les crédits des scans sont conservés. Le fichier
de crédits déclare quatre modèles Poly Haven CC0, avec géométrie LOD0/1/2
et textures JPEG séparées. Ce statut est déclaré par la source ; aucune
nouvelle qualification de redistribution n'est prononcée ici.
Blender disponible vérifié : 5.2.1 LTS ; aucune conversion Blender exécutée.

L'inventaire précède contact baking et assemblage des matériaux. Il ne
conserve pas encore les maillages sous forme de paquet runtime, ni les
programmes NatureMaterial/PebbleField, ni le masque et les atlas scannés.
Il ne charge pas les GLB scannés et ne rend pas la scène Debris.

Il reste à acquérir les géométries finales et leurs propriétaires sans
changer les tirages aléatoires, conserver le contact baking, les matériaux
et les variantes, puis raccorder les placements aux contrats communs
d'édition. Le champ de cailloux et les scans doivent également être portés.
Les contrôles runtime, ombres, édition persistante et comparaisons native /
WebGPU restent requis. Aucun verdict de parité ou de gate n'est acquis.

## Acquisition des payloads CPU

Le mode optionnel `--retain-payloads` de l'export conserve désormais les cinq
batches bruts (positions, normales, UV, tint, vdata et indices), en float64 /
uint32 little-endian, sans perdre la précision CPU avant conversion runtime.
Il conserve aussi le masque RGBA8 1024×1024 du champ de cailloux et uniquement
les collisions ajoutées par Debris, après celles de Village et Rocks.
La méthode source exacte `_bakeContact` est appelée sur une copie des tints :
les couleurs après occlusion sont conservées séparément ; les batches bruts
et leurs digests restent inchangés. L'assemblage final et la partition par
objets ne sont pas encore réalisés.

Les acquisitions indépendantes `debris-source-payloads-20261005-v1` et `v2`
terminent code 0. `scripts/tidewater/verify-debris-source-payloads.py` relit
tous les payloads et vérifie tailles, SHA, valeurs finies, indices valides,
bornes de contact, masque et nombre de collisions. Les fichiers des deux
acquisitions sont identiques, et leurs inventaires sont identiques à celui
de placement initial. Le reçu local
`debris-source-payloads-20261005-repeat.json` rapporte PASS dans cette portée :

- 101 850 sommets et 112 669 triangles bruts ;
- 111 collisions ajoutées ;
- 37 payloads, dont le masque RGBA8 de 4 194 304 octets ;
- 215 826 composantes de tint assombries par le contact source.

Les entrées source sont revérifiées après acquisition par SHA et état Git.
Les fichiers binaires sont des données d'acquisition, pas des GLB qualifiés
ni un paquet déjà chargé par Forge. Les matériaux, les quatre modèles
scannés et leurs textures/LOD restent à raccorder, ainsi que le champ de
cailloux et la propriété des objets pour l'édition générique. Aucun rendu
Debris, geste Studio ou verdict de parité n'est établi par ce contrôle.

## Propriété de la géométrie pour la conversion éditable

Le mode supplémentaire `--observe-ownership` observe les frontières source
de `count`, appelées après émission des objets/groupes acceptés. Il conserve
les intervalles de sommets et indices par matériau, les identités source
déterministes, les ancres XZ lorsqu'elles sont déclarées et les bounds monde
mesurés. Les groupes composés intentionnels restent des groupes ; le nombre
de propriétaires n'est donc pas un nombre de primitives ou de meshes.

Les pierres de crique demandent une observation individuelle : leur journal
n'enregistre qu'une pierre sur dix. Une copie en mémoire du module remplace
uniquement leur appel `stone` par un observateur après émission, avec imports
absolus et SHA du module observé. Aucun fichier upstream n'est modifié et aucun
tirage aléatoire n'est ajouté. Les 11 pierres sont observées individuellement.
Les scans conservent leur transformation source et une identité propre.

Les acquisitions v3 et v4 terminent code 0. Le vérificateur relit les payloads
et contrôle la couverture exhaustive et contiguë de chaque batch, l'absence
de références d'indices entre propriétaires, les bounds recalculés, les IDs
uniques, les scans identiques à l'inventaire initial et les deux groupes
attendus près du stand. Tous les fichiers acquis sont identiques entre v3/v4 ;
l'inventaire et les maillages bruts restent identiques à la génération sans
observation. Reçu `debris-ownership-20261005-repeat.json` : PASS CPU uniquement.

- 805 propriétaires de géométrie procédurale, dont 11 pierres de crique ;
- 141 propriétaires de scans ;
- totalité des 101 850 sommets et 112 669 triangles couverte sans trou/recouvrement.

Les essais v1 (crique groupée) et v2 (libellé de frontière générique) sont
conservés comme étapes intermédiaires, pas comme version finale de ce contrôle.
Les intervalles de propriété ne sont pas encore des assets runtime : restent
la conversion des pivots/matériaux, le rattachement précis des collisions,
les scans/LOD/textures, le champ de cailloux et le raccord aux placements et
retouches communs. La disponibilité dans Studio et la parité restent ouvertes.

## Assemblage final et contrats de matériaux acquis

`scripts/tidewater/export-debris-assembly.mjs` reconstruit les batches bruts
vérifiés puis appelle l'assemblage source exact `_assemble`, qui replie rope
vers hard et net vers fabric, applique le contact et réordonne les triangles
Nature pour les ombres. Les quatre géométries finales et plages de dessin
sont conservées. Les factories source génèrent les contrats wood, hard,
fabric, nature et pebbles : attributs, varyings, stages WGSL, modules,
uniforms et bindings. Les pixels des textures ne sont pas acquis ici.

Le vrai constructeur PebbleField est exécuté avec le masque acquis et le
terrain source, pour conserver la géométrie de patch, les flags d'occupation
et les bornes de hauteur des cellules. La mise à jour suivant la caméra n'est
pas exécutée. TerrainGPU est construit avec ses valeurs initiales : ombre
solaire non cuite et shore field placeholder. Cette portée acquiert les
contrats générés, pas une scène appariée. Aucun chargement réseau des scans.

Deux acquisitions `debris-assembly-20261005-v1` / `v2` terminent code 0,
sources inchangées. Leur fermeture volontaire du device WebGPU émet
`device was destroyed` ; ce message suit une acquisition réussie et n'est
pas une compilation ou un rendu qualifié.

`scripts/tidewater/verify-debris-assembly.py` reconstruit indépendamment les
règles de folding, contact et ordre des indices à partir des payloads bruts.
Il compare exactement les attributs float32, indices et plages de dessin
assemblés, les populations de casters, le masque et l'occupation des cellules.
Tous les fichiers des deux acquisitions sont identiques. Reçu local
`debris-assembly-20261005-repeat.json` : PASS dans cette portée uniquement.

- 112 669 triangles procéduraux conservés ;
- 3 900 triangles Nature sélectionnés pour les ombres ;
- 50 522 triangles dans la plage source des ombres ;
- 4 704 triangles dans le patch de cailloux ;
- 10 415 cellules de terrain occupées, sans prétendre les dessiner ensemble ;
- 60 payloads, cinq contrats de matériaux et quatre meshes statiques.

L'acquisition finale permet la conversion par propriétaires en conservant
les paramètres des shaders. Elle ne fournit pas encore un lecteur/adaptateur
Rust Debris, ses textures runtime, les GLB scannés, le suivi de caméra des
cailloux ni l'édition Studio. Ces éléments et leur validation visuelle restent
requis avant toute annonce de couche intégrée.

## Repères locaux et paquet intermédiaire portable

`scripts/tidewater/package-debris-frames.py` produit les repères locaux de
tous les propriétaires observés : centre XZ des bounds mesurés et minimum Y,
axes identité, métrique conservée. Il conserve positions source float32,
normales, UV, tints après contact et vdata pliés selon l'assemblage source.
Chaque partie possède aussi ses indices d'ombre exacts ; les petites parties
Nature ne deviennent pas toutes des casters. Les positions de référence
restent disponibles pour adapter les phases de vent en repère source.

Le paquet conserve les contrats générés des cinq matériaux, les dépendances
WGSL, les données et attributs de patch du champ de cailloux, son masque et
ses cellules. Les références sont adressées par SHA et fermées dans le paquet.
Les 111 collisions source restent un payload distinct, explicitement non
rattaché aux propriétaires ; aucun rattachement spatial approximatif n'est
promu en preuve. Les transformations des 141 scans sont conservées, mais les
modèles et textures scannés ne sont pas encore inclus.

Deux conversions indépendantes `debris-frames-20261005-v2` / `v3` terminent
code 0. La v1 intermédiaire, avec références de shader non remappées et sans
données complètes de PebbleField, est conservée sans qualification.
`scripts/tidewater/verify-debris-frames.py` réouvre les deux paquets, compare
chaque partie aux intervalles de l'acquisition, remet les positions en monde,
compare les autres attributs et indices exactement, vérifie les masques
d'ombre, les bounds, les contrats de matériaux et PebbleField, provenance,
collisions conservées et fermeture exhaustive des références.

Reçu `debris-frames-20261005-repeat.json` : PASS dans cette portée CPU.

- 805 repères, 865 parties, 112 669 triangles conservés ;
- 50 522 triangles d'ombre conservés ;
- erreur maximale de remise en monde : 5,960464477539063e−8 m ;
- 4 905 fichiers identiques entre les deux paquets, manifeste compris ;
- SHA package : `0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a`.

Ce format intermédiaire suit les payloads de matériaux custom employés par
les acquisitions Forge. Il n'est pas déclaré GLB normalisé, asset qualifié
au Vault ou couche disponible dans Studio. Le lecteur/adaptateur Rust, les
textures runtime, les scans, les collisions attachées et les gestes d'édition
restent à réaliser et vérifier. Aucune parité visuelle n'est acquise.

## Lecteur Rust du paquet intermédiaire

`crates/forge_assets/src/tidewater_debris.rs` est maintenant exposé par
`forge_assets`. Il charge le paquet exact `0a84592f…55189a` via un transport
synchrone ou asynchrone injecté, sans filesystem, ECS ou représentation
canonique de placement dans ses types de production. Les IDs déterministes
des 805 propriétaires restent distincts des futurs placements auteur.

Le lecteur vérifie SHA/tailles, limites de manifeste/payload/volume,
références content-addressed et fermeture exhaustive, identité source,
repères, comptes d'objets/triangles/ombres, dimensions et finitude des
attributs, indices bornés et remise en monde des positions dans la tolérance
de 0,1 mm. Les contrats source et données de PebbleField sont exposés sans
prétendre être des shaders adaptés ou une scène rendue.

Contrôles terminés code 0 sur le worktree courant, Rust 1.98.0 explicitement
fixé, cache privé `debris-reader-target-20261005-v2` :

- `cargo test -p forge_assets tidewater_debris -- --include-ignored` : deux
  tests PASS, zéro ignoré parmi les tests sélectionnés, 99 autres filtrés.
  Fixture réelle `FORGE_DEBRIS_PACKAGE_FIXTURE=…/debris-frames-20261005-v3`.
  Les chemins synchrone/asynchrone chargent les 4 904 payloads identiques ;
  manifeste invalide, payload absent/altéré et repère incorrect sont refusés.
- `cargo clippy -p forge_assets --lib -- -D warnings` : PASS.
- `cargo check -p forge_assets --target wasm32-unknown-unknown` : PASS.

Le premier essai de tests échoue avant compilation du lecteur : dépendances
produites par rustc 1.99.0 alors que forge_core emploie 1.98.0 (E0514).
Le cache est conservé. La correction fixe RUSTC au binaire 1.98.0, désactive
les wrappers pour ce processus et utilise un autre cache ; aucun nettoyage
de cache existant. Clippy signale ensuite deux conversions chunks dans le
nouveau lecteur, corrigées avec `as_chunks`, et un `#[must_use]` manquant
sur `source_graph_glb::document`, ajouté sans changement de comportement.
Les tests sont rejoués après ces corrections et passent.

Ces contrôles concernent le lecteur et la bibliothèque assets, pas les tests
workspace complets, la CI distante ni l'application Studio. L'adaptateur
Bevy, les textures et shaders runtime, les scans et les collisions attachées
restent requis avant un premier rendu Debris et ses comparaisons.

Suite : [projection CPU Bevy et scans conservés](2026-10-05-debris-mesh-adapter.md).
