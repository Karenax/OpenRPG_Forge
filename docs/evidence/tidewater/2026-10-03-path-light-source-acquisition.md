# Acquisition des trois lampes des chemins

La recette `scripts/tidewater/export-path-lights.mjs` observe les trois appels
`Props.pathLight` du village figé au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`. Elle conserve les arguments,
les attributs source, indices, données de matériaux en précision source,
colliders et positions lumineuses, sans modifier le clone source.

La capture est sous `F:/OpenRPG_Forge/.build/path-light-source-20261003-v1` :
63 payloads binaires, neuf parts de matériaux, 426 sommets et 312 triangles.
Le contrôle relit tous les payloads : SHA-256, tailles, dimensions des
attributs, valeurs finies et indices dans leurs propres sommets.
Les originaux Village.js, Props.js et LICENSE sont conservés avec leurs SHA.

La scène complète observée et celle construite sans instrumentation sont
identiques : géométrie fusionnée, terrain, bâtiments, footprints et colliders.
Le digest de scène est
`e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1`.
Le clone source reste inchangé après exécution.

Les trois IDs source sont joints exactement au paquet de lampes AEC déjà
fermé et vérifié. Les matrices capturées, positions monde, couleurs et
intensités correspondent sans écart. Les phases et paramètres photométriques
restent ceux du paquet lumineux existant ; ils ne sont pas réacquis par ce script.
Le reçu `acquisition-verification.json` conserve les IDs lumineux et hashes.

Deux essais préalables ont été refusés : le tag collider `pathLight` est aussi
employé par trois lampes de passerelle. La recette corrigée limite l'observation
à l'appel `_villageProps` puis vérifie l'ordre des appels et la même identité de
vecteur lumineux. Elle ne sélectionne pas un collider par proximité spatiale.
Les six colliders du village restent conservés dans la comparaison globale.

## Limites et suite

Il s'agit d'une acquisition de géométrie source, pas d'un asset converti ou
indexé. La conversion doit encore relocaliser les sommets sur l'origine propre
de chaque lampe, préserver les matériaux, vérifier le round-trip et produire
le package catalogue. Ensuite, le binding lumineux doit suivre la racine
`ManualPlacement` réelle pour déplacement, copies et suppression.
Aucun raccordement Studio, dessin GPU, qualification runtime ou verdict de
parité visuelle n'est acquis par cette capture. Les anciennes bibliothèques
et les snapshots de compilation restent inchangés.

## Mise en paquet après acquisition

`package-path-lights.py` convertit les trois objets sur leurs origines locales,
replie le verre sur le contrat de matériau village et vérifie les 312 triangles
contre le maillage acquis `source-017`, attributs compris. Les données source
de matériau sont repliées depuis leur précision double, puis stockées en f32.
La bibliothèque matériaux est épinglée à 390950ef et le paquet bâtiment à A53.
Les positions, normales, UV, teintes, indices, colliders et lumières sont préservés.

Les sorties `F:/OpenRPG_Forge/.build/path-light-package-20261003-v1` et `v2`
sont identiques octet pour octet : 41 fichiers, dont 40 payloads et le manifeste.
Son SHA-256 est
`2c270ef9c0ab9e471160f1e92d95ee99255833804583e2807a117c1eeee71789`.
L'erreur de retour des positions est 0 m sur les sommets de ces trois lampes.
`path-light-package-repeatability-20261003-v1.json` conserve cette comparaison.

`verify-path-light-package.py` relit indépendamment la fermeture et les hashes,
puis compare les payloads convertis aux octets source : normales/UV/teintes/indices,
repli matière, coordonnées reconstruites, bornes, colliders et lumières.
Le contrôle passe ; son reçu est
`F:/OpenRPG_Forge/.build/path-light-package-proof-20261003-v1.json`.
La licence MIT est copiée et comparée à l'original conservé.

Cette preuve termine la conversion des payloads locaux de ces trois lampes.
Le lecteur Rust, les fiches de catalogue, le transport, les racines éditables
et le raccordement des émetteurs restent à intégrer et à tester dans Studio.
Le paquet n'est ajouté à aucune bibliothèque existante ni à la compilation
en cours. Aucun rendu GPU ou verdict de parité visuelle n'est acquis.

## Catalogue préparé et contrôlé avec les types Rust

`prepare-path-light-catalog.py` produit un catalogue `AssetCatalog` v1 séparé
pour les trois objets, avec une identité déterministe issue de chaque
`source_object_id`, le hash du paquet complet comme révision et la provenance
MIT au commit source. Les données d'import restent immuables ; aucun placement
de zone n'est créé par cette préparation. Les descriptions FR/EN sont issues
de la source et les bornes sont mesurées sur les sommets convertis.

Les sorties sont sous `F:/OpenRPG_Forge/.build/path-light-catalog-20261003-v1`.
Un harness contre les vraies bibliothèques natives Forge déjà construites V8
relit le catalogue et exécute `validate_import_metadata`, les trois calculs
`AssetId::deterministic`, les trois recherches `resolve_import_pin`, trois
refus de révision incorrecte et le round-trip serde. Tous réussissent.
Les sources courantes d'imported_catalog.rs et de forge_core/lib.rs sont
comparées à celles du snapshot V8 et sont identiques. Cela ne qualifie pas
une reconstruction Cargo complète du worktree ni le lecteur du paquet mesh.

Le reçu `F:/OpenRPG_Forge/.build/path-light-catalog-validation-20261003-v1/result.json`
conserve les hashes du harness, de l'exécutable et du catalogue.
Le hash du catalogue est
`6c6d62e4b6fb3b5e5f416e2cbb991b580e9ff855fc26afc41c91d1d83593e5ba`.
La bibliothèque Studio existante n'est pas modifiée. Le lecteur mesh Rust,
le transport et l'installation dans Studio restent à raccorder ; le catalogue
préparé seul ne rend pas les lampes trouvables ou plaçables dans le produit.

## Lecteur Rust préparé et validé isolément

Le module `crates/forge_assets/src/tidewater_path_lights.rs` vérifie le manifeste
épinglé, les tailles/SHA-256 des 40 payloads, la fermeture des références,
les trois identités source, les neuf parts, les 312 triangles, la licence et
la bibliothèque matériaux. Il propose les transports synchrone/asynchrone
et le catalogue standard avec les mêmes IDs que la préparation précédente.
Aucun état d'édition n'est stocké dans ce lecteur.

Le harness V3 compile le vrai module source contre les bibliothèques natives
existantes, puis exécute explicitement le test ignoré nécessitant le paquet réel :
un test passé, zéro échec et zéro test ignoré dans cette invocation.
Il contrôle les deux transports (40 lectures asynchrones immédiates), la même
fermeture de contenu, les trois IDs du catalogue, le refus d'un payload changé
et le refus du manifeste altéré avant transport. Le test reste ignoré par
les exécutions unitaires ordinaires qui n'ont pas ce fixture externe.

Clippy ciblé avec `all`, `pedantic` et warnings en erreurs passe ; le formatage
passe. La garde des six entrées du harness avant/après est identique.
Les premières validations restent documentées dans le reçu : la compilation
asynchrone initiale a refusé le déplacement du manifeste encore emprunté par
la future du test ; le test altère désormais une copie. Cinq diagnostics de
documentation ont ensuite été corrigés avant le Clippy final réussi.

Le reçu est `F:/OpenRPG_Forge/.build/path-light-reader-validation-20261003-v1/result-v3.json`.
Le module validé a le SHA-256
`5516f148ec69a1eeb7feab4f17a128b43f47d59a54cd353ba4da81a80795761a`.
Il n'est pas encore déclaré dans lib.rs, pour conserver les entrées des builds
concurrents inchangées. Ces contrôles isolés ne sont donc pas un Cargo test du
worktree complet. Le branchement de crate, le transport frontend, la géométrie
Bevy/Rapier, la bibliothèque et les lumières sur racines éditables restent
à intégrer. Aucun résultat WASM/runtime ou visuel de ce lecteur n'est revendiqué.
