# Accessoires de plage : lecteur Rust, projection et transport

Le paquet indépendant contient 43 prototypes, 78 parties et 18 807 triangles.
Il conserve les matériaux partagés des bâtiments, les positions source et
les collisions. Manifest SHA-256 :
`0a08054a08b8da7309f00c278860cd43851341c008b8a4208f72c0b820838808`.

## Lecteur

`crates/forge_assets/src/tidewater_beach_props.rs` vérifie le manifeste épinglé,
les 424 payloads, la licence, la fermeture des références, l'inventaire source
et les comptes de triangles. Les ID reprennent exactement les `source_id`
`beach-prop:<ordinal>`. Les versions synchrone et asynchrone consomment les
mêmes octets ; le catalogue retourne les 43 prototypes et leur révision.

Premier test natif isolé terminé code 0 : fixture réelle entièrement lue,
catalogue contrôlé, fichiers manquants et altérés refusés, manifeste altéré
refusé. Reçu `beach-reader-validation-20261004-v1/result.json`.
Le premier Clippy a signalé uniquement la longueur de la fonction de validation.
L'inventaire constant a ensuite été déplacé au niveau du module ; validation
du lecteur courant et de la projection en cours sous
`beach-engine-validation-20261004-v1`. Aucun PASS anticipé pour ce rejeu.

## Projection moteur

`crates/forge_engine/src/tidewater_beach_props.rs` prépare les maillages et
collisions dans le monde Bevy/Rapier commun. Les placements utilisent
`ManualPlacement` et les marqueurs de projection/collision génériques.
Les matériaux et buffers passent par les mêmes `VillageMaterial` et
`VillageMeshBytes` que les bâtiments. Aucun nouveau moteur de rendu.

L'inventaire comprend 14 prototypes sans collision et une clôture à cinq
cylindres. Les tests sur fixture réelle doivent contrôler les 43 projections,
la physique effective, l'invalidation d'une primitive détachée et la libération
des ressources. Ils sont lancés explicitement avec `--include-ignored` ;
compilation/test en cours, session 72600. Clippy courant en cours, session 5755.
Les nouveaux modules ne sont pas encore enregistrés dans les crates produit.

## Transport vérifié

`prepare-building-library-site.py` accepte la paire root/pin de plage.
Commit, matériaux, empreintes et tailles sont vérifiés avant toute publication
du site. Le staging isolé `beach-studio-site-20261004-v1` est terminé code 0.
Audit `beach-studio-library-proof-20261004-v1/result.json` : 424 payloads
identiques, source inchangée, payload altéré refusé avant création de destination.

## Suite

Enregistrement des modules après validations, chargement/templates Studio,
sélection GPU, édition persistante, catalogue produit réutilisable et rendu
restent à terminer. Les barques de plage sont des accessoires statiques ;
elles ne prouvent pas le bateau contrôlable. FishProps reste un contenu distinct.
Le candidat village figé en parallèle ne contient pas cette tranche.
Aucune parité visuelle, performance ou qualification de gate n'est déclarée.

## Contrôles ciblés terminés

Clippy du lecteur et de l'adaptateur courants est terminé code 0, avec inputs
inchangés (`reader-clippy-result.json`, `clippy-v3-result.json`). Le lecteur
courant compile aussi en métadonnées WASM avec les dépendances exactes du
candidat Web (`beach-reader-wasm-check-20261004-v1/result.json`, code 0,
inputs inchangés). Il ne s'agit ni d'un build application complet ni d'un test GPU.
Le test natif de projection/physique reste en cours sous la session 72600.

## Tests natifs et enregistrement terminés

La session 72600 a ensuite terminé code 0 : cinq tests moteur et un test
lecteur réel PASS, zéro ignoré/filtré, inputs inchangés. Ils couvrent les
43 prototypes, 78 parties et 18 807 triangles, les 14 objets sans collision,
la clôture à cinq primitives, la projection commune avec Rapier réel,
l'invalidation d'un collider détaché et le retrait des ressources.
Reçu `beach-engine-validation-20261004-v1/result.json`.

Les deux registres produit déclarent maintenant `tidewater_beach_props`, avec
une seule ligne ajoutée dans chacun. Les octets précédents sont préservés dans
les backups ; retour exact aux registres initiaux démontré par le helper.
Reçu `beach-engine-validation-20261004-v1/registration.json`.

## Raccordement Studio préparé

Les cinq fichiers du raccordement sont sauvegardés avant écriture sous
`beach-studio-wiring-20261004-v1` ; toutes les ancres et l'absence de changement
concurrent sont vérifiées avant publication de l'ensemble. Le consommateur
réutilise le transport asynchrone épinglé, la publication sous ticket/génération,
le contrôle des matériaux, les templates liés au terrain, `ManualPlacement`,
la partition des imports, le focus/gizmo, la projection commune et les receipts.
Rechargement, erreur, scène vide et retrait incluent le nouveau paquet.
Le collecteur d'ombres reçoit les nouvelles parties selon sa politique matériau
existante. La parité de cette politique reste une vérification graphique.

Les appels de projection utilisent une liste des cinq groupes pour conserver
une signature courte. Le snapshot de contrôle WASM/Cargo est en préparation,
sans preuve nouvelle de rendu ni d'édition persistante dans Studio.
Les matériaux partagés restent `VillageMaterial`/`VillageMeshBytes`.

## Contrôle du raccordement

Le contrôle Cargo release WASM de Studio est terminé code 0, session 59411,
sur 1 338 fichiers figés inchangés :
`studio-beach-check-20261004-v1/result.json`. Il couvre le lecteur enregistré,
la projection moteur, les imports et le raccordement UI/scène/ombres ensemble.
La configuration emploie le lockfile hors ligne et la cible WebGPU de Studio.
Ce contrôle ne produit pas d'application liée ni de preuve de pixels.
Les ressources de plage ne sont pas dans l'exécutable village testé en parallèle.

## Construction du candidat complet

La construction release WASM liée est lancée sous session 51590, depuis le
même snapshot de 1 338 sources utilisé pour le contrôle Cargo terminé.
Le reçu du contrôle reste intact ; la construction écrit un reçu distinct
`studio-beach-check-20261004-v1/frontend-result.json` à sa terminaison.
Le cache cible a été contrôlé sans compilation concurrente sur ce chemin.
Aucun verdict de build ni de rendu n'est anticipé.

La bibliothèque complète est préparée et vérifiée : 2 509 fichiers,
avec les imports et kits du candidat village conservés et le paquet de plage
ajouté. Les anciens champs de l'index sont conservés.
Reçu `studio-beach-check-20261004-v1/library-stage.json` ; ce staging ne
prouve pas le chargement ou l'édition des accessoires de plage dans Studio.
Les helpers d'assemblage et de compilation du worker sont préparés et
exigeront le succès de la construction avant exécution.
Les sources et artefacts du worker de navigation précédent ont été comparés
aux sources figées du candidat plage : toutes les empreintes concordent,
y compris `Cargo.lock`. Reçu `navigation-worker-source-comparison.json`.
Un helper de réutilisation vérifiée est préparé : il recontrôlera ces empreintes,
le snapshot complet et les fichiers copiés après succès de l'assemblage.
Il n'a pas encore été exécuté et ne constitue pas une validation runtime.
## Candidat plage lié et shell natif construits

La session 51590 termine code 0 : 1 338 sources figées inchangées,
WASM brut 124 795 985 octets, SHA-256
`a68436e2a21e73ad90acf9cb30dd10b78321948cafaa7674856642830e4927c3`.
Le reçu séparé `frontend-result.json` préserve le contrôle Cargo précédent.
L'assemblage bindgen termine code 0. Le worker est réutilisé après recontrôle
complet de ses pins de sources, Cargo.lock et artefacts ; aucune compilation
nouvelle du worker n'est revendiquée. Frontend final : 2 520 fichiers vérifiés.
Reçus `frontend-bindgen.json`, `navigation-worker-result.json`,
`frontend-ready.json` et `native-inputs.json` sous le même dossier candidat.

Le shell natif termine code 0 sous session 48983 : sources et frontend
inchangés, entrées vérifiées par le constructeur canonique. Exécutable
`desktop/forge-studio-candidate.exe`, SHA-256
`428b1c6c60d0f4f53311aecdf8809492a08adad26b7b3719669e1fe71c737097`.
WASM lié SHA-256
`35408989fe85d5b69f41111b2c8086939059d5b0e95de61f39e3f734e1acaed2`.
Reçu `native-result.json`. Ce candidat ne contient pas le nouveau lecteur
FishProps enregistré après le gel des sources.

Une fixture fraîche de 25 fichiers source est préparée sous
`.placeables-smoke-beach-20261004-v1` ; le staging du candidat est en cours
sous session 53089. Le futur parcours couvre huit types de plage : barque,
épave, rame, rack de filets, rack de poissons, caisse, bouée et clôture,
ainsi qu'un objet manuel et une plante à sélectionner sur GPU. Une nouvelle
inspection visuelle de ce candidat est requise avant de choisir le point GPU.
Aucun lancement ni verdict de rendu de plage n'est anticipé.
Le staging de la fixture plage termine code 0, session 53089 : 2 520 fichiers
frontend et l'exécutable sont copiés indépendamment et revérifiés. Reçus
`frontend-stage.json` et `executable-stage.json`. La nouvelle inspection
`InspectBeachCommonPlantV1.ps1` est préparée mais non lancée, pour éviter de
concurrencer le parcours d'édition village v4 actif. Aucun point de sélection
GPU issu du candidat village n'est transféré à ce nouveau candidat.
## Inspection native du candidat plage et parcours lancé

L'inspection v1 est terminale : accès au target CDP en timeout dans l'exécution
restreinte, sans capture. Cleanup : 25 sources intactes, processus possédé fermé.
Le rejeu v2 avec accès au port local réussit, capture actuelle inspectée :
`.placeables-smoke-beach-20261004-v1/plant-inspection-v2/plant-view.png`, SHA-256
228c32a7388b1d51835c000d6569763589558c77aca577d13cd026c69f3e454b.
Le rendu montre la côte, le ponton, la végétation et des props de plage ;
ce n'est pas une comparaison visuelle qualifiée avec Three.js.

Le point de plante (1419,484) vient de la canopée visible sur cette nouvelle
capture, avec caméra et WASM du candidat plage épinglés. Le parcours complet
`RunBeachCommonEditingV1.ps1` est lancé sous session 35931, sortie common-ui-v1 :
huit familles plage, un placement manuel et sélection GPU réelle requise.
Aucune identité injectée, aucun PASS anticipé. Le candidat ne contient pas
encore le port du matériau FishProps réalisé après son snapshot figé.

## État terminal du parcours plage v1

Le parcours 35931 termine en échec contrôlé : dix cas d'édition et dix cycles de
suppression Undo/Redo sont enregistrés, première sauvegarde/réouverture acquise,
mais la sauvegarde finale dépasse 120 s avec une commande authoring pending.
Checkpoint édité : `f0a86de06435f8e40dfa4c994007d8296491cdab42186756a018acef07c98cce`.
Le ledger `common-ui-v1/common-placeable-studio-ledger.json` confirme source et
frontend préservés, listener CDP fermé ; checkpoint supprimé absent. Picking GPU
plante observé, cycle persistant complet non qualifié. Aucun PASS global.
Le défaut ressemble à celui du village v4 ; les marqueurs transport ajoutés au
helper après son chargement ne figurent pas dans cette exécution. La cause
checkpoint d'audit/transport/backend reste à mesurer sur un rejeu ciblé.
