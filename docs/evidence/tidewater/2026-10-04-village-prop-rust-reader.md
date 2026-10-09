# Tidewater — lecteur Rust des accessoires de cour

Suite du [paquet portable](2026-10-04-village-prop-portable-package.md).
`crates/forge_assets/src/tidewater_village_props.rs` charge le manifeste
épinglé `d03cd51f...02da`, contrôle les tailles/SHA-256 de 296 ressources,
la licence, la bibliothèque matériaux, les 27 identités/frames, le nombre
de collisions de chaque objet, les références fermées et 11 949 triangles.
Il expose les transports synchrone/asynchrone et un catalogue standard,
sans état d'édition ni dépendance Bevy. Les IDs dérivent des identités
source `tidewater:<commit>:village-prop:<ordinal>` ; les trois lampes
existantes ne sont pas réintroduites par ce catalogue.

## Contrôles natifs sur le paquet réel

Le harness compile le vrai module contre les bibliothèques existantes
épinglées, puis exécute explicitement le test d'intégration normalement
ignoré faute de fixture. Session 18090 : code 0, un test passé, zéro échec,
zéro ignoré. Le test vérifie le chargement des 296 ressources dans les
deux transports, leurs octets concordants, les 27 entrées du catalogue,
le refus d'un fichier manquant, d'un payload altéré et du manifeste altéré
avant transport. Clippy ciblé `all`, `pedantic`, warnings en erreurs PASS ;
formatage PASS. Les six fichiers d'entrée restent identiques avant/après.

Le reçu est
`.tidewater-integration/village-reader-validation-20261004-v1/result-v3.json`.
SHA-256 du module :
`b03c105d8742ae13babeca3fbffb02001b7393bb4646a011e31fd87b8518dcfa`.
Ces contrôles sont une compilation isolée du module, pas une qualification
Cargo de tout le worktree ni une exécution graphique.

## Déclaration et compatibilité Web

Après inspection des compilations actives, le test workspace Source8 est
confirmé sur son snapshot séparé. Aucun de ses processus n'est interrompu.
Le module validé est déclaré dans `forge_assets/src/lib.rs` par une seule
ligne supplémentaire. `village-reader-registration.json` conserve les
empreintes avant/après et vérifie que supprimer cette ligne retrouve
exactement tous les octets précédents, y compris les changements existants.

Le contrôle WASM V3 compile le même module en métadonnées contre les
bibliothèques Web épinglées : code 0, entrées inchangées. Il conserve les
deux essais précédents : V1 manquait le chemin des proc-macros hôtes ; V2
mélangeait deux variantes Serde du cache. V3 résout les dépendances exactes
du graphe `forge_core` par ses fingerprints, sans changement produit.
Le reçu est `village-reader-validation-20261004-v1/wasm-check-v3/result.json`.
Il ne s'agit pas d'un lien applicatif, d'un Cargo check complet ou d'un test GPU.

## Suite requise

Correction postérieure aux reçus précédents : l'identité déterministe et le
nom logique du catalogue utilisent maintenant exactement `village-prop:<ordinal>`,
comme le `source_id` du paquet, au lieu du préfixe dupliqué `village-village-prop`.
Le test compare les 27 identités directement aux lignes du descripteur source.
Le paquet et son SHA-256 restent inchangés. Les anciens reçus natif/Clippy/WASM
portent sur l'ancien hash du module ; ils ne prouvent pas cette nouvelle version.
Le rejeu du lecteur corrigé est inclus dans la validation de l'adaptateur V2 :
session 67202 terminée code 0, test réel PASS, zéro échec/ignoré/filtré, entrées
épinglées inchangées et refus des payloads altérés conservés. Le contrôle Web
V4 du lecteur corrigé a également terminé code 0, entrées inchangées : reçu
`village-reader-validation-20261004-v1/wasm-check-v4/result.json`, module
`84619bcf2dfc8f60051b2a38a2cd5c4fb857d3c29262e8b76df13d7ee56181b5`.
Il s'agit de métadonnées WASM, sans lien applicatif ni exécution GPU. Clippy
propre au lecteur corrigé (`all`, `pedantic`, warnings en erreurs) termine
code 0, entrées inchangées : reçu
`village-engine-validation-20261004-v2/reader-clippy-result.json`.

Le transport frontend et la bibliothèque Studio ne chargent pas encore ce
paquet. Le consommateur Bevy/Rapier doit préparer les matériaux tissu/filet,
les prototypes et leurs collisions, puis les placements communs persistants.
Les instances FishProps restent distinctement à acquérir. Disponibilité
Studio, édition de ces nouveaux objets, rendu natif et parité restent ouverts.
