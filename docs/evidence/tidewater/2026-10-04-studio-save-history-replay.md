# Sauvegarde Studio — coût du rejeu des archives historiques

## État et portée

Le village v4 et la plage v1 terminent sans PASS de persistance complète :
Save final dépasse 120 s, après première sauvegarde/réouverture et dix cycles
d'édition/suppression. Leurs preuves originales restent conservées. Aucun
rejeu Studio du correctif ci-dessous n'est encore qualifié.

## Mesure et cause candidate

Le journal plage comporte 3 880 entrées, dont 920 persistées (~5,65 MiB).
Une publication Atelier ultérieure au Save en attente reçoit une réponse ;
ce constat ne suffit pas à localiser l'attente avant/après transport, les nouveaux
marqueurs ayant été ajoutés après chargement du helper de cette exécution.

Le backend `FileWorkspaceSession::save` vérifie les références de la tête et de
l'historique. Chaque lecture `ZoneObjectStore::read_with_budget` rejouait chaque
archive via `ReviewedZone::from_bytes`, même après sa validation/promotion.
La sonde native en lecture seule contre la bibliothèque projet du candidat
rejoue les 101 objets plage (164 935 723 octets) en **149 814 ms**. Ce coût dépasse
le délai du test ; il constitue une cause candidate mesurée, pas une preuve de
l'emplacement précis où le Save observé attendait.

Preuve locale : `.tidewater-integration/save-archive-read-probe-20261004-v1/`.
L'exécution termine code 0, toutes les archives sont acceptées. Elle ne sauvegarde
aucun workspace et ne change aucune archive.

## Correctif

`crates/forge_project/src/workspace/objects.rs` conserve maintenant les résultats
du rejeu dans le magasin vivant, par empreinte SHA256, avec une limite de 1 024
entrées. Aucun cache persistant ni confiance dans la seule date du fichier.
Chaque appel relit les octets, vérifie l'empreinte exacte, l'identité projet/zone/
tête et les sources référencées. Le budget cumulatif 256 MiB et la limite 4 MiB
par archive restent inchangés. Une archive inconnue subit toujours le rejeu
complet avant entrée en cache. Les reprises froides restent coûteuses.

Validation native isolée :
`.tidewater-integration/save-archive-cache-proof-20261004-v3/result.json` :
compilation, cinq tests du magasin/publication et Clippy passent. Le test du
magasin couvre le budget exact, une identité de tête incompatible, une
altération physique après remplissage du cache puis la restauration des octets.
Les deux essais de harnais précédents conservent leurs erreurs de visibilité et
de documentation publique ; aucun échec de test produit n'est effacé.

Sur les trois archives les plus lentes de la mesure initiale, la lecture du
magasin actuel prend **15 428 ms à froid, 71 ms à chaud**. Les octets retournés
restent identiques ; chaque fichier correspond à l'empreinte de son nom.
Reçu : `save-archive-cache-proof-20261004-v3/benchmark-result.json`.
Ce résultat porte sur la relecture, pas sur la durée totale de Save dans Studio.

## Suite requise

Contrôle Cargo isolé de la crate courante lancé dans
`save-archive-cargo-proof-20261004-v1` ; verdict à consigner après terminaison.
Puis reconstruire le candidat natif, rejouer Save sur une fixture isolée avec
marqueurs transport, et qualifier Save/Close/Open après suppressions sans
augmenter arbitrairement le délai du test. La parité visuelle reste distincte.

## Verdict Cargo courant

Le contrôle termine code 0 : test ciblé du magasin, puis **99 tests de la crate
forge_project** passent, zéro échec/ignoré ; Clippy `--all-targets -D warnings`
passe. Les sources des trois crates domaine et le lockfile restent identiques
aux empreintes avant exécution. Reçus `result.json` et `full-suite-result.json`
dans `save-archive-cargo-proof-20261004-v1`. Build isolé, aucune fenêtre Studio
ni cache partagé modifié. Le rejeu complet Studio reste à exécuter.

## Candidat natif apparié du correctif

Le candidat `studio-save-cache-check-20261004-v1` est construit : code 0,
1 338 sources figées intactes. Il dérive du candidat plage v1 avec uniquement
`crates/forge_project/src/workspace/objects.rs` remplacé par le correctif testé.
Frontend identique, non reconstruit ; nouveaux travaux FishProps exclus de ce
candidat de régression Save. `derivation.json` documente la différence exacte.

EXE SHA256 `6507967bcda0c20543b778e01820a5710c2bdc3e4bbe6b5d20efbd1ab05029ad` ;
WASM inchangé `35408989fe85d5b69f41111b2c8086939059d5b0e95de61f39e3f734e1acaed2`.
2 520 fichiers frontend vérifiés dans une nouvelle copie indépendante ;
fixture source indépendante de 25 fichiers, parent intact. Racine du rejeu :
`.placeables-smoke-beach-save-cache-20261004-v1`.

Inspection native lancée via `InspectSaveCachePlantV1.ps1` (session 59694),
fenêtre/profil CDP propres. La compilation seule ne prouve pas Save/Close/Open.
Délai du parcours complet maintenu à 120 s, marqueurs transport actuels prévus.

Inspection corrigée terminée code 0 : capture courante
`plant-inspection-v1/plant-view.png`, SHA256
`63fdda83d5ba64eb5373398455097f5ee2a747427ee1ff0773629b40f008d49a`.
Image examinée pour le point GPU palmier (1419, 484), pas de parité visuelle
revendiquée. 25 fichiers source intacts, propre processus d'inspection fermé.

Le parcours complet `RunSaveCacheCommonEditingV1.ps1` est lancé (session 67213)
sur `common-ui-v1`, même frontend, huit familles plage, bâtiment manuel et
sélection GPU plante réelle. Délai 120 s inchangé. Aucun PASS anticipé ;
les prochains constats porteront sur les deux Save et les marqueurs transport.

Observation en lecture seule du parcours Save corrigé (port propre 53315) :
la première sauvegarde authoring 1108 est résolue entre 637 253,7 et 639 407,8 ms,
soit 2 154,1 ms audit inclus ; Close puis deuxième OpenManaged sont enregistrés.
À la seconde observation, Apply 91, Preview 77, une commande Apply transport en
attente normale, aucune erreur de persistance. La sauvegarde finale n'est pas
encore observée ; le verdict du parcours complet reste ouvert.

## Résultat terminal et décodage réel du checkpoint final

La session 67213 termine code 1 après les dix cas et dix cycles de suppression
Undo/Redo. Les deux Save répondent HTTP 200 : 2 154,1 ms et 3 772,7 ms.
L'arrêt intervient dans le lecteur de preuve V4, qui recherche directement
`head` dans l'archive compacte v39. Le ledger original reste `failed_closed` ;
il n'est pas renommé PASS.

Le lecteur Rust isolé `zone-archive-reader-20261004-v2` reconstruit le checkpoint
avec `ZoneEditSession::from_archive_bytes`. Hash archive final
`3f8b8abe5639c09180b0c78b410a835ea672dbfa0badcfeea1749de2725ac9c4`, hash head
`ef695c8aa6f13193901da62d5fc6a519fd78458fa5c6eaa7a9e6ffc9b353761d`, conforme à
la référence workspace. `removal-proof.json` vérifie les dix suppressions et
la conservation exacte des métadonnées source des plantes.

Le helper isolé V5 utilise ce lecteur épinglé pour les archives compactes.
`smoke-v5-proof.json` prouve le décodage, les suppressions et les rejets d'un
mauvais hash du lecteur ou d'un mauvais head workspace. Aucun changement du
script canonique préexistant dans cette réparation. La réouverture UI finale
reste une preuve séparée à produire ; le décodage Rust ne la remplace pas.

## Réouverture à froid et correctif suivant

Le rejeu indépendant `final-reopen-v2` termine code 1 : l'action de zone gérée
n'apparaît pas dans les 120 s. Source préservée et listener fermé. Le fichier
`authoring.lock` persiste après fermeture : son accès exclusif est vérifié avant
copie et il reste intact dans la source. L'essai v1 s'était arrêté avant lancement
par une vérification trop stricte de présence du fichier, conservée comme échec.

Le correctif suivant dans `file_session.rs` réserve d'abord les tailles physiques
des archives uniques sous le plafond cumulatif de 256 MiB, puis vérifie toutes
les références par au plus quatre workers. Chaque lecture reste bornée par sa
réservation, recontrôle le type de fichier, les bytes/hash, le replay domaine,
les identités et les blobs source. Un agrandissement concurrent dépassant la
réservation échoue. Tous les workers sont joints avant retour, même en cas
d'erreur ; aucun historique n'est ignoré et aucun format persistant n'est ajouté.

`save-cold-parallel-cargo-proof-20261004-v1/result.json` : 99 tests lib et Clippy
all-targets code 0, entrées inchangées. Les tests d'intégration workspace,
workspace_adoption et workspace_objects passent également ; mesure d'ouverture
réelle lancée dans `save-cold-open-runtime-proof-20261004-v1` (session 80197).
La réduction de latence et la réussite UI restent à mesurer sur ce correctif.

## Compteur incrémental de taille et mesure suivante

L'ouverture réelle du même checkpoint avec vérification parallèle prend
119 226 ms (`save-cold-open-runtime-proof-20261004-v1/result.json`). Ce résultat
ne suffit pas à valider le délai UI de 120 s, qui inclut d'autres opérations.

Le replay de chaque commande resérialisait auparavant tout son historique pour
vérifier le plafond d'archive. `session/delta.rs` ajoute un compteur dérivé exact :
en-tête, version, commandes, virgules et budget des requêtes développées. Le
compteur emploie le même constructeur de commande et le même sérialiseur canonique
que l'encodeur existant. Les changements de corrections sont comparés avec leur
représentation exacte, y compris les zéros signés. Le format sauvegardé est inchangé.

Si le candidat delta tient dans 4 MiB, il constitue déjà un fallback accepté
pour les autres formats et évite le calcul complet de chaque ancien préfixe.
Si le compteur manque, échoue ou dépasse le plafond, la vérification complète
existante reste exécutée. L'audit, les règles de transition, les hashes des heads,
les empreintes de receipts et les blobs source ne sont pas contournés.

`save-incremental-size-cargo-proof-20261004-v1/result.json` : 223 tests all-targets
et Clippy passent, sources vérifiées intactes pendant le contrôle. Le nouveau
test compare les comptes aux bytes réellement encodés après éditions, Undo/Redo
et rechargements ; les tests de formats delta et compact restent inclus.

`save-cold-open-runtime-proof-20261004-v2/result.json` : ouverture réelle en
8 861 ms, source et documents de copie préservés. Il s'agit de l'API domaine sur
une copie indépendante, pas d'une réussite UI. Candidat natif apparié préparé
dans `studio-fish-native-check-20261004-v3` : frontend poissons v2 inchangé, quatre
fichiers domaine remplacés par ces sources testées. La première tentative de
construction s'arrête avant Cargo, car le constructeur refuse de recevoir à la
fois un kit intégré et un remplacement de kit ; une copie d'entrée sans kit est
préparée pour valider séparément le kit gouverné, sans modifier le frontend réel.

Le constructeur accepte ensuite les entrées distinctes frontend/kit et la
construction native v3 est lancée (session 66672). Le script
`StageFishReopenV3.py` est préparé pour épingler/copier ce candidat et son frontend
complet après succès, puis reprendre seulement la réouverture finale de la
sauvegarde existante dans une nouvelle copie. Il n'est pas exécuté tant que la
construction n'a pas de verdict terminal positif. Les anciens ledgers en échec
sont conservés ; aucun verdict complet d'édition ni de parité visuelle n'est ajouté.

## Réouverture UI finale sur le candidat apparié v3

Construction native 66672 terminée code 0, sources et frontend intacts.
EXE SHA256 `c38228df28f35061e1f48e885fc0e5cc41779e42361f81ba7a8972496c8537c9`.
2 771 fichiers frontend sont copiés et épinglés avec cet EXE dans
`.placeables-smoke-fish-20261004-v3/candidate`.

La session 32595 (`ResumeFishFinalReopenV3.ps1`) termine code 0. Son reçu
`final-reopen-fish-v3/reopen-result.json` indique `passed`, dix cas vérifiés,
une ouverture native `open_managed` exacte, session publique synchronisée,
source et frontend préservés, listener CDP fermé. Les dix originaux et dernières
copies supprimés restent absents du sélecteur commun après cette ouverture.
Capture examinée : `removed-reopened.png` ; aucune parité visuelle revendiquée.
Les ledgers V4 précédents restent en échec. La composition des preuves établit
les suppressions sauvegardées et leur réouverture sur ce candidat ; elle ne
transforme pas le parcours original en un PASS unique ni ne couvre tout Studio.
