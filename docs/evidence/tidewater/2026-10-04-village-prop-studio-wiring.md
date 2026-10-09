# Tidewater — raccordement Studio des accessoires de cour

Suite du [consommateur Bevy/Rapier](2026-10-04-village-prop-engine-adapter.md).
Le paquet épinglé contient 27 prototypes, 51 parties et 11 949 triangles.

## Changements produit

Le candidat de bibliothèque accepte le pin facultatif `village_props_sha256`.
Le transport charge le manifeste et les 296 fichiers via les limites du lecteur,
contrôle leur intégrité et la concordance avec les matériaux du village,
prépare les templates, puis les publie avec la génération complète validée.
Les requêtes remplacées conservent le refus par ticket ; les références ne
deviennent pas disponibles à partir d'un chargement partiel.

Les templates alimentent le même adaptateur de sources associé au dataset
de terrain, avec les identités exactes du paquet. Les placements utilisent
`ManualPlacement`, `PlacementEdit` et les tombstones communs : aucun nouveau
document d'auteur ou outil d'édition spécifique à la carte n'est ajouté.
La partition de rendu évite d'envoyer ces géométries procédurales au loader GLB.

La scène ajoute ce lot aux receipts de génération, comparaisons des placements,
reconstruction, contrôle Rapier, ciblage de la caméra et retrait des ressources.
Chaque racine reçoit le gizmo commun ; les parties portent l'identité commune
de sélection. Le collecteur d'ombres reçoit également les parties du nouveau
lot, avec la politique de matériau opaque existante. Cette déclaration ne
prouve pas encore les pixels, la portée des ombres ou l'animation du linge.

Le navigateur général du kit gouverné attend des GLB. Ce raccordement ne
transforme pas les paquets procéduraux en entrées GLB du navigateur : la
réutilisation depuis le catalogue produit reste à compléter et à vérifier.

## Préparation et audit du transport

`prepare-building-library-site.py` accepte une paire facultative root/pin
pour ces accessoires, vérifie commit, bibliothèque de matériaux et fichiers
avant staging, puis ajoute le pin sans changer les autres champs du candidat.
Premier site isolé : `.tidewater-integration/village-studio-site-20261004-v1`.

Audit indépendant terminé code 0 : 296 fichiers vérifiés, source inchangée,
payload volontairement altéré refusé et aucun site partiel créé dans ce cas.
Reçu : `village-studio-library-proof-20261004-v1/result.json`.
SHA-256 de son index limité de validation :
`83ccd096cc680e7051de50b94225f2a5c8c34d1eaec1941bac1d8a076108094d`.
Il n'est pas le candidat complet affiché dans Studio.

## Candidat complet en cours

Le snapshot `studio-village-candidate-20261004-v1/source` contient 1 336 fichiers,
avec vérification d'absence de changement pendant la copie. Les sources
préexistantes des autres builds ne sont pas modifiées. Le frontend se construit
en release WASM/WebGPU depuis ce snapshot, hors ligne et avec lockfile, en
réutilisant le cache Web vérifié inactif au lancement. Le WASM précédent est
préservé séparément avant le build. Session 26664, Python 26428 et son enfant
Cargo 23220 confirmés actifs ; les journaux montrent la compilation des crates
du projet après réutilisation des dépendances. Aucun rejeu n'est lancé.
Les journaux et le reçu final se trouvent sous ce candidat ; aucun PASS de
compilation n'est anticipé.

La bibliothèque complète est préparée depuis les fichiers épinglés du précédent
frontend validé, puis étendue du nouveau paquet en préservant tous les champs
existants de l'index. Session 24066 terminée code 0 : 2 084 fichiers vérifiés,
source précédente inchangée, 296 payloads village plus manifeste ajoutés,
tous les champs précédents de l'index conservés. Reçu
`studio-village-candidate-20261004-v1/library-stage.json`. Elle n'est pas déployée.

Les helpers d'assemblage bindgen, construction du worker depuis les mêmes
sources figées, staging natif et build Tauri sont préparés pour s'enchaîner
après un verdict terminal favorable du frontend. Ils ne sont pas encore exécutés.

Une fixture d'édition fraîche est également préparée sous
`.placeables-smoke-village-20261004-v1` : 25 fichiers source vérifiés et
préservés, avec huit cas source prévus (les quatre précédents plus clôture,
linge, barque sur tréteaux et seau), un bâtiment manuel et une plante à
sélectionner réellement sur le GPU. Les coordonnées végétales doivent être
inspectées sur le nouveau rendu ; elles ne sont pas inventées ou reprises
comme preuve. Aucun parcours d'édition nouveau n'est encore lancé.

Compilation liée, shell natif, véritable sélection GPU, transformations,
sauvegarde/réouverture et suppression sans résurrection des nouveaux objets,
comparaison Village et performances restent à vérifier sur ce candidat.
Aucune disponibilité visuelle Studio, parité ou clôture de gate n'est déclarée.

## Compilation frontend terminée

La session 26664 est terminée code 0. Le reçu
`studio-village-candidate-20261004-v1/result.json` confirme les 1 336 sources
figées inchangées et l'application liée. WASM brut : 124 744 006 octets,
SHA-256 `38c11357c11fdd9ce551c9484d7071f5eab1247af8ab5dcf8d1229f6b939060f`.
L'assemblage avec wasm-bindgen 0.2.127 est aussi terminé code 0
(`frontend-bindgen.json`). Le worker de navigation depuis les mêmes sources
est en construction, session 54072. Le shell natif et les parcours GPU restent
à exécuter. Ce candidat ne contient pas les accessoires de plage ajoutés ensuite.

Le worker a ensuite terminé code 0, sources figées inchangées. Reçus
`navigation-worker-result.json` et `frontend-ready.json`. Le staging natif
est vérifié (`native-inputs.json`) ; le shell Tauri se construit sous session
20830, après validation des entrées par le constructeur canonique.
Une copie indépendante des 2 097 fichiers frontend est aussi vérifiée dans
la fixture d'édition village (`frontend-stage.json`), sans lancement.

## Candidat natif et première inspection

Le shell natif a terminé code 0, sources et frontend inchangés :
`native-result.json`. Exécutable SHA-256
`fe7e173eac47e798db97b3f71d9535833435481bd6e324c741de2313442b54ae`,
WASM lié SHA-256 `b90e4cae5bb04a0f22b4a4c9934a4fdcaaf8a21aecdfa6712fad2ca7e40b39be`.
Il est copié et revérifié dans la fixture (`executable-stage.json`).

L'inspection initiale échoue sur le libellé de plantes : lecture Python implicite
cp1252 d'un script UTF-8, donnant un libellé différent de celui de Studio.
Le processus propre est fermé et les 25 fichiers source sont inchangés.
Une inspection v2 fraîche avec UTF-8 explicite réussit code 0 : zone ouverte,
navigation Nature, orbit caméra changé/stabilisé et capture actuelle conservée.
`plant-inspection-v2/inspection.json`, `plant-view.png` et `cleanup.json`.

Le parcours complet d'édition est en cours, session 84197, sortie `common-ui-v1`.
Il exige huit sources (dont clôture, linge, barque sur tréteaux et seau), un objet
manuel et une plante réellement sélectionnée sur le GPU. Le point végétal est
choisi dans cette capture actuelle, sans injection d'identité. Cette inspection
de côte ne prouve pas le rendu des accessoires du village ni leur édition.

## Verdict du parcours complet v1

La session 84197 termine code 1, `failed_closed`. Les dix cas (huit sources,
un objet manuel et une plante sélectionnée réellement sur GPU) ont passé les
transformations, duplication, contrôles de verrouillage et première
sauvegarde/réouverture. Les deux premières familles passent aussi leur cycle
de suppression/Undo/Redo. Le parcours échoue à la suppression de la copie
plaza-prop:0 : une entrée PreviewZone est rejetée. Le message de rejet détaillé
n'est pas conservé par ce helper, ce qui impose une capture supplémentaire
avant de conclure sur la cause. Ne pas déclarer le cycle complet PASS.
Reçus/captures préservés dans `common-ui-v1`, dont
`common-placeable-studio-ledger.json` et `failure-detail.json`.
Le frontend et la fixture source sont conservés ; listener propre fermé
selon le ledger. Le candidat plage en construction conserve les sources
figées antérieures à ce diagnostic et ne constitue pas une correction.
Le rejeu v2 est lancé sous session 30833, sortie `common-ui-v2`, sur le même
candidat et les mêmes critères complets. Le helper V4 conserve uniquement une
observation supplémentaire de l'audit en cas d'échec, avant fermeture propre.
Aucune assertion n'est réduite et aucune correction produit n'est présumée.
Les deux nouveaux scripts PowerShell passent le parseur sans erreur.
## Cause du faux rejet et correction de l'instrumentation

Le rejeu v2 termine code 1, cette fois lors d'un Redo. L'audit complet est
conservé dans `common-ui-v2/failure-audit.json`. L'entrée ApplyZone 2419
contient une réponse HTTP 200 et un reçu de révision 71, puis une exception
`QuotaExceededError` sur `__forge_gate2_world_audit_entries_v1` transforme
la réponse en rejet. Le quota est dépassé par l'observateur de test, pas par
une erreur du serveur d'édition. La session v1 ne conservait pas ce détail.

Le helper canonique `Invoke-Gate2WorldEditingProof.ps1` conserve désormais
les requêtes/réponses d'auteur exactes dans une compression gzip/base64 sans
perte. Les observations Atelier read restent en mémoire et ne gonflent plus
le checkpoint de rechargement. La persistance est attendue avant de rendre
la réponse et une erreur de stockage est exposée séparément : elle bloque
le snapshot de preuve, sans rejeter la réponse réussie du produit.
Les anciens checkpoints JSON restent lisibles ; les indices des entrées
restaurées ne sont pas réécrits.

Le test `scripts/tidewater/tests/test_world_audit_persistence.mjs` exécute le
vrai corps d'installation avec quota simulé : 90 requêtes d'auteur dépassent
le quota non compressé, survivent exactement au reload, 400 observations
restent en mémoire ; une panne forcée de stockage préserve HTTP 200 et
bloque le verdict de preuve. PASS sous Node 22.22.3, parseur PowerShell PASS.
Le rejeu complet v3 est lancé sous session 64006, sortie `common-ui-v3`,
critères inchangés. Aucun PASS runtime n'est encore revendiqué.
Le rejeu v3 termine code 1 sur une lecture trop précoce de la liste après
la première duplication. L'audit conserve HTTP 200, révision 8 et la requête
contenant la copie attendue. La première correction introduisait une fenêtre
asynchrone où l'entrée mémoire était `resolved` pendant la compression,
avant que le fetch rende la réponse à Studio. L'entrée demeure maintenant
`pending` jusqu'à la fin de la persistance ; seule la copie durable reçoit
l'état final pendant cette attente, puis l'état mémoire est publié avant
le retour, sans nouvelle suspension.

Le test exécute également une assertion pendant `setItem` pour interdire
ce statut mémoire prématuré. Reçu courant PASS code 0, inputs inchangés :
`world-audit-persistence-proof-20261004-v1/result.json`.
Le parcours complet v4 est lancé sous session 88574, sortie `common-ui-v4`,
sur le même candidat et avec les mêmes critères. Le v3 n'est pas promu en PASS.
## Fin du parcours v4 : sauvegarde finale non acquise

La session 88574 est terminale, code 1. Les dix cas ont terminé leur édition
et leur duplication, puis leur suppression avec Undo/Redo. Le premier
Save/Close/Open est passé, checkpoint b4b41b851f920f1aabae6ae6ec80bf70a2ff6ae387c42cfdb982f9d424cc88b5.
Le deuxième Save ne répond pas dans les 120 secondes ; pas de checkpoint final
ni de verdict PASS global. `common-ui-v4/common-placeable-studio-ledger.json`
atteste source/frontend préservés et listener proprement fermé.

L'audit conserve Save index 4307 pending, aucune tentative/réponse enregistrée.
Un ancien Atelier publish index 1250 reste également pending après le reload ;
il est exclu du compteur authoring. Le journal ne permet pas encore de distinguer
attente du checkpoint d'instrumentation avant transport, attente native et
lecture de réponse. Ne pas attribuer cet échec à la sauvegarde produit sans
preuve supplémentaire. Deux marqueurs diagnostiques sont ajoutés au helper,
avant le fetch natif et à sa réponse. Aucun critère ni timeout n'est assoupli.
Le test canonique de persistance repasse : 90 commandes restaurées sans perte,
400 observations en mémoire, panne de stockage sans rejet produit.
