# Tidewater — préparation de la bibliothèque scannée dans Studio

Les quatre fichiers de `studio-scanned-library-20261006-v2/staged` préparent
le chargement des 141 instances scannées dans la bibliothèque existante,
sans modifier les sources de production utilisées par le check Studio actif.
Ils ne constituent pas une qualification de rendu ou d'édition dans Studio.

Le candidat ajoute deux références optionnelles et indissociables : révision
du paquet scanné et hash du programme final d'instances. Les anciens candidats
sans ces champs restent lisibles. Un champ seul, une révision inconnue, un
programme différent ou un ticket de chargement remplacé sont refusés avant
publication dans la bibliothèque. Le transport utilise les chemins bornés
de `fetch_building_payload` ; les validations existantes de `ScannedInput`
s'appliquent au paquet, aux atlas, aux géométries et aux propriétaires de collision.

La préparation décode les douze prototypes, les trois atlas et les collisions,
et vérifie le programme de matériau ainsi que le shader de profondeur. Chaque
variation garde son identité d'asset, sa pose et son aléa acquis. Le catalogue
commun reçoit des entrées de décoration ; les placements source passent par
`source_objects::binding`, avec association du dataset et provenance conservée.
Aucune branche d'édition spécifique au nom d'objet ou au module n'est ajoutée.
La publication reste dans la même étape finale que la bibliothèque existante.

## Contrôle exécuté et limites

`syntax-utf8-validation-v1/result.json` termine PASS : les quatre fichiers sont
analysés avec Rustfmt 1.98, sans écriture dans les sources préparées, et les
lignes UTF-8 préexistantes sont conservées. Les hashes des trois originaux et
des quatre copies restent inchangés. Ce contrôle syntaxique ne résout pas les
types, ne construit pas le WASM et n'exécute pas l'éditeur.

La première copie V1 a révélé un problème de décodage implicite Windows des
textes français. Elle est conservée comme préparation non retenue. V2 impose
UTF-8 lors de la lecture ; le contrôle ci-dessus vérifie les lignes d'origine
contenant des caractères non ASCII. Aucun texte de production n'a été altéré.

Il reste à déployer les ressources immuables dans une copie de bibliothèque,
compiler Studio avec ces changements, raccorder le rendu commun, les collisions,
la readiness, les ombres et le picking, puis effectuer réellement TRS,
duplication, suppression, Undo/Redo et sauvegarde/réouverture. Les ressources
préparées ne sont pas encore proposées dans le catalogue de l'application
en cours d'exécution. Aucun verdict d'édition, de GPU ou de parité n'est attribué.

## Routage commun préparé

`E:/OpenRPG_Forge/.tidewater-integration/studio-scanned-routing-20261006-v1`
prépare aussi `authoring_assets/placements.rs` : les placements scannés sont
extraits du même ensemble `DESIRED` que les autres objets et exclus du chargement
GLB générique. Ce dernier ne sait pas résoudre les prototypes scannés. Les
identités et poses restent celles des `ManualPlacement` communs ; aucune
politique d'édition propre à la carte n'est ajoutée.

Le reçu `result.json` confirme la syntaxe Rust par Rustfmt à code 0 et les
hashes inchangés de l'original et de la copie. Cette préparation n'est pas
adoptée pendant le check Studio qui épingle les sources. Elle dépend du loader
V2 et du futur adaptateur de rendu/readiness ; elle ne prouve ni un check de
types complet, ni un objet visible, ni son picking dans l'application.

## Retrait des collisions préparé à la frontière Engine

`E:/OpenRPG_Forge/.tidewater-integration/scanned-retirement-engine-20261006-v1`
prépare un module sous `scanned_scene`. Il conserve les racines communes et
leurs primitives explicitement possédées, vérifie toutes les identités et
parentés avant de supprimer quoi que ce soit, puis suit les couples contexte /
primitive jusqu'à leur disparition de `RapierContextColliders`. Le prédicat
de readiness ne confond pas disparition ECS et retrait du backend.

Le helper emprunte l'ensemble de racines : un refus de propriété ne consomme
pas la comptabilité du caller. Son intégration doit capturer les liens avant
la suppression des racines et conserver le suivi entre les remplacements.
Le nettoyage des draws, matériaux et soumissions d'ombre reste à raccorder
dans l'adaptateur complet ; ce helper seul n'est pas un cycle de suppression.

Une première préparation située dans Studio reste conservée mais n'est pas
retenue : Studio ne dépend pas directement de Rapier. La préparation Engine
préserve cette frontière et reprend le contrôle backend utilisé par le rendu
des débris existants. Rustfmt 1.98 termine à code 0 sur les deux fichiers
préparés ; les sources de production sont inchangées. Aucun contrôle de types
complet ou essai Rapier réel n'est encore attribué à cette préparation.

Le contrôle `scanned-retirement-engine-20261006-v1/type-check-v1` est lancé
sur le handle 68461. Il compile les déclarations courantes des deux composants
de placement, le helper préparé et des dépendances natives précompilées. Le
couple Bevy/Rapier est choisi à partir de leur empreinte Cargo de dépendance,
pas seulement du nom du fichier. C'est un contrôle de métadonnées, sans
exécution de Rapier, sans rebuild Engine complet et sans qualification WASM.
Son log n'a pas encore de résultat terminal lors de cette observation.

## Publication des draws et ombres préparée

La copie `studio-scanned-renderer-20261006-v1/staged/.../scanned/frame.rs`
prépare les draws depuis les placements communs, la caméra retournée par
`authoring_imported_terrain::shadow_source` et le `Receiver` réellement courant
de Studio. Le futur système doit s'exécuter après la publication des ombres
et la propagation de caméra/frustum. Il prépare les poses d'ombre avant de
synchroniser le buffer et les entités puis publie le frame scanné. Un refus ou
une caméra/cible absente retire le frame précédent pour son extraction.

La syntaxe est analysée par Rustfmt à code 0. Le fichier n'est pas enregistré
dans l'application : allocation initiale, remplacement, retrait des ressources,
readiness, choix d'ordre des systèmes et contrôle WASM complet restent à faire.
Cette préparation n'est pas une preuve de dessins GPU ou d'ombres visibles.

## Adaptateur complet et raccords préparés

Le reçu `studio-scanned-renderer-20261006-v1/preparation.json` épingle maintenant
les copies de `imported_scene.rs`, `authoring_imported_shadows.rs`, du nouvel
adaptateur `scanned.rs` et de sa publication de frame. Le brouillon local
`scanned/retirement.rs` reste conservé mais n'est pas déclaré par cet adaptateur :
celui-ci utilise le helper Engine décrit plus haut.

L'adaptateur prépare les douze prototypes et les atlas partagés depuis la
bibliothèque vérifiée, conserve les variations acquises et crée des racines
`ManualPlacementProjection` avec le gizmo commun et les primitives de collision.
Les révisions et propriétaires sont vérifiés avant création. Lors d'une
retouche, les racines sont remplacées en suivant le retrait Rapier ; les
ressources de dessin sont conservées dans la même génération de bibliothèque.
Le matériau suit la hauteur, le soleil et les ressources d'éclairage partagées.
La readiness exige les poses communes, la propagation des transforms et la
registration des collisions, plus le retrait de celles de l'ancienne scène.

Les raccords ajoutent ce contrôle à `imported_scene::ready`, installent le
matériau scanné et ordonnent sa publication après la mise à jour des ombres.
La visibilité de cette fonction est élargie à la crate pour exprimer cet ordre.
Le frame est retiré si le viewport ne permet plus la projection des sources.
Les cinq fichiers préparés passent Rustfmt 1.98 à code 0 ; aucun source de
production n'est modifié par cette préparation. La compilation de types de
l'adaptateur complet, la propagation des frustums, le comportement atomique
des refus, le focus, le picking et le cycle auteur réel restent à vérifier.

## Types validés et prévalidation renforcée

Le contrôle de types 68461 termine PASS à code 0, avec inputs inchangés.
Il compile le helper de retrait Engine V1 contre les vraies déclarations
courantes de `ManualPlacementProjection` et `ManualPlacementCollision` et le
couple de dépendances Bevy/Rapier cohérent identifié par Cargo. Son unique
avertissement d'import non utilisé dans le harnais est conservé. Ce PASS est
un contrôle de types natif isolé ; il ne couvre pas l'adaptateur Studio entier.

`studio-scanned-renderer-20261006-v2` conserve V1 et exclut le brouillon de
retrait situé dans Studio. Les quatre sources retenues passent Rustfmt à code
0. Les stores de ressources sont contrôlés et les références de draw et le
matériau rafraîchi sont validés avant le remplacement de racines dans la même
génération. Aucun scénario de refus atomique dans l'application n'est encore
qualifié par ce contrôle syntaxique.

`scanned-retirement-engine-20261006-v2` conserve également V1 et complète le
suivi avec les identités encore enregistrées dans les contextes Rapier, même
si leurs entités ECS ont déjà été supprimées. La disparition de l'entité n'est
donc pas utilisée comme preuve de retrait backend. Cette préparation passe
Rustfmt et exige une validation réelle du cycle de physique. Le nouveau
harnais `VerifyScannedRetirementRuntime20261006.py` couvre quatre scénarios :
retrait backend différé, primitive déjà supprimée côté ECS, refus de tout le
batch pour une mauvaise racine, et refus pour une parenté de collider changée.
Le résultat de son exécution suit séparément ; aucun PASS n'est encore déduit.

## Candidat combiné figé et incidents conservés

Le build natif 19763 termine avec retour Cargo 0, mais son contrôle d'intégrité
refuse la qualification : trois sources ont changé depuis le début du build
(`source_graph_attributes.rs`, `source_graph.rs`, `source_graph_glb.rs`).
`native-pebble-build-20261006-v1/source-drift-inspection-v1/result.json` conserve
les hashes avant/après. Cette observation n'attribue pas l'auteur des changements.
Le binaire n'est pas admis pour le walk ou pour un nouveau paquet distribuable.

`combined-scanned-candidate-20261006-v1` fige ensuite 1 093 fichiers du workspace
courant et applique 21 fichiers préparés uniquement à cette copie : réfraction,
adaptateurs temporels scannés, galets avec corrections auteur et limites aux
coins, bibliothèque et rendu scannés Studio, routage commun et retrait Engine
V2. Le reçu confirme que les originaux étaient inchangés pendant cette copie.
Il ne remplace pas les contrôles d'intégration. Le check complet Engine/probe
natif est lancé sous 33252 ; le check Studio WASM suivra sur la même copie.
Le champ textuel historique du reçu parle de 20 sources ; l'inventaire effectif
`overlays` en contient 21, conformément à la sortie du préparateur.

Le harnais physique 41311 termine en échec de compilation : son V1 omettait
`CARGO_MANIFEST_DIR`, requis par les derives Bevy. Les quatre tests n'ont donc
pas été exécutés. V1 et son FAIL sont conservés ; le harnais V2 fournit le même
contexte manifeste que le contrôle de types précédent, sans modifier les
scénarios ni les assertions. Il est relancé séparément et n'a pas encore de
verdict runtime. Aucune preuve GPU, d'édition ou de parité n'est attribuée à
ces préparations et relances.

## Cycle de retrait Rapier validé et focus préparé

Le harnais physique V2 termine en échec de liaison, avec `windows.0.52.0.lib`
absent des chemins du linker. Les tests n'ont pas été exécutés. V3 conserve
V1/V2 et fournit la bibliothèque Windows 0.52.6 réellement présente dans le
registre Cargo, épinglée par hash, ainsi que les options de lien sans PDB ni
incrémental utilisées par les harnais shader déjà validés.

V3, handle 67997, termine PASS : compilation 0, tests 0, inputs inchangés.
Les quatre tests exécutent le vrai plugin Rapier sur `FixedUpdate`. Ils
vérifient le retrait différé après disparition ECS, le suivi d'une primitive
déjà supprimée avant le helper, la préservation de tout le batch lors d'une
mauvaise identité de racine et le refus d'une parenté modifiée. Un remplacement
vide ne doit pas oublier le retrait backend encore en attente. Ce PASS couvre
le helper Engine V2 et les déclarations courantes de composants, avec les
dépendances précompilées cohérentes ; il ne couvre pas le cycle Studio complet.

`studio-scanned-renderer-20261006-v3` conserve V2 et prépare le cadrage depuis
la racine commune, sa variation acquise et le rayon ajusté à sa scale éditée.
Les positions/rayons non finis ou hors plage de la caméra sont refusés. Le
focus rejoint la demande commune de `imported_scene` ; aucune règle fondée sur
le nom du décor ou de la carte n'est ajoutée. Le système de publication passe
explicitement après `VisibilitySystems::UpdateFrusta`, dont l'API est vérifiée
dans Bevy 0.19.1 local. Les quatre fichiers passent Rustfmt à code 0.

Cette V3 n'est pas injectée dans le candidat combiné V1 pendant son check actif
33252. Le focus visible, le picking fidèle aux déformations GPU et l'ordre de
rendu effectif restent à qualifier dans un candidat ultérieur. Le check natif
V1 progresse dans les crates Forge et rerecast ; les avertissements de copie
au lieu de hardlinks dans le cache incrémental sont conservés.

## Complétude des inputs du candidat figé

Le check natif combiné V1, 33252, termine FAIL à code Cargo 101, avec inputs
figés inchangés. `forge_world` ne trouve pas huit JSON inclus à la compilation
depuis le dossier racine `examples/`, absent de la première copie. Le check
n'a donc pas encore atteint le probe complet ; aucun verdict de compilation
des nouveaux raccords n'est attribué à cet échec de préparation.

`combined-scanned-candidate-20261006-v2` conserve V1, reprend ses sources figées
et complète les macros littérales `include_str!` / `include_bytes!` avec leurs
fichiers présents dans le dépôt. Quatorze inputs supplémentaires sont épinglés,
pour 1 107 fichiers au total ; aucune référence extérieure au dépôt n'est
détectée par ce collecteur. Les includes construits dynamiquement restent à
couvrir par les checks réels. Les quatre fichiers du renderer Studio V3
remplacent leurs copies V2 après vérification des hashes des deux préparations.

Les checks complets de cette même V2 sont lancés sous 10967 (Engine et exemple
natif) et 15095 (Studio WASM/WebGPU). Ils utilisent les caches de dépendances
existants mais contrôlent tous les hashes de cette copie figée après compilation.
Le check Studio courant 24461 reste distinct et actif. Aucun source de
production n'est modifié ; aucun contrôle encore actif n'est décrit comme PASS.

## Préparation des transitions LOD et de la visibilité

`scanned-retirement-engine-20261006-v3` conserve V2 et prépare l'initialisation
des `GlobalTransform` et `InheritedVisibility` de chaque draw à partir de sa
racine commune. Le synchroniseur vérifie la présence et la finitude de la pose
avant mutation du buffer. Ce raccord vise les entités créées tardivement lors
d'un changement LOD, après la propagation normale des transforms. Leur première
pose ne doit pas dépendre d'une propagation au frame suivant. Rustfmt termine
à code 0 ; aucun dessin GPU ou cycle LOD réel n'est encore attribué.

`studio-scanned-renderer-20261006-v4` conserve V3 et prépare le filtrage de
visibilité héritée des racines dans le calcul commun des draws et poses
d'ombre. Le système est explicitement placé après propagation de visibilité
et avant sa collecte par les vues Bevy. L'ordre de ses dépendances existantes
(terrain, population et ombres) est inspecté ; l'initialisation réelle du
schedule reste à vérifier dans l'application. Les quatre fichiers passent
Rustfmt à code 0. Aucune preuve de picking fidèle aux déformations GPU n'est
déduite de la copie d'une pose ou d'un masque de visibilité.

Ces préparations ne modifient pas les 1 107 inputs du candidat V2 compilé sous
10967 et 15095. Son check natif a atteint `forge_engine` ; son check WASM attend
encore le verrou de build détenu par le check Studio courant. Les handles des
deux checks sont confirmés actifs ; aucun redémarrage n'est déclenché sur cette
attente. L'objectif de parité complète et d'édition réelle reste ouvert.

## Ressources scannées préparées pour le transport Studio

`studio-scanned-payloads-20261006-v1/result.json` confirme une copie isolée de
85 fichiers : 82 payloads du paquet, son manifeste, le programme final et le
candidat de bibliothèque. Les tailles et SHA-256 de chaque payload correspondent
au manifeste f7faf1c0 ; les descripteurs sont présents dans cet inventaire et
leurs tailles/hashes concordent. Le programme final est la révision 3d54ba12.
Le paquet déclare la référence source 1438b1ab et 141 instances de géométrie.
Les notices présentes dans l'inventaire sont copiées avec les autres payloads.

Les chemins imbriqués sont conservés sous `forge-imports/<hash>/`, selon le
transport existant. Les segments vides, de traversée, les backslashes et les
préfixes de volume sont refusés ; les inputs résolus doivent rester dans le
paquet. Les hashes de toutes les sources restent inchangés après copie.
`buildings.json` reprend le candidat du frontend shadow V2 et lui ajoute
uniquement les deux références scannées ; ses 2 122 octets restent sous la
limite de transport de 4 096 octets.

Ce dossier est un overlay de ressources, pas un frontend autonome : les autres
packages du candidat de bibliothèque restent dans le frontend de base. Le
prochain essai doit fusionner cet overlay dans une nouvelle copie complète,
utiliser le WASM réellement construit du candidat retenu, puis exécuter le
loader et le cycle auteur. Aucun remplacement de bibliothèque live, exécution
du loader Studio, rendu GPU ou verdict d'édition n'est attribué à cette copie.

## Check natif complet et réparation du frontend isolé

Le reçu `combined-scanned-candidate-20261006-v2/native-check-v1/result.json`
confirme le check Cargo complet du moteur et de l'exemple natif à code 0.
Les 1 107 inputs du candidat sont inchangés ; le SHA-256 de sa préparation est
`2e8800080e01480f27fba8fdb37a27623adc3192e34bce295863e67dbe43706f`.
Ce check ne construit pas l'exécutable final, ne vérifie pas Studio WASM et
ne prouve aucun rendu ni parité. Le build natif complet distinct, handle
66973, et les checks Studio 15095 et 24461 sont confirmés encore actifs.

La copie `frontend-preparation-v1` a échoué avec WinError 3 sur des chemins
Windows longs. Sa copie partielle reste conservée et `copy-failure.json`
consigne cet échec. Le helper `PrepareCombinedScannedFrontendLongPaths20261006.py`
utilise les chemins Windows étendus pour préparer `frontend-preparation-v2`,
sans aplatir ni exclure de payloads. Son exécution termine à code 0.

Le nouveau reçu inventorie 8 014 fichiers et 141 instances scannées. Les
inventaires et SHA-256 du frontend de base et de l'overlay sont vérifiés avant
copie, après copie et sur les outputs ; les inputs sont inchangés. Le manifeste
de bibliothèque est remplacé uniquement dans cette copie isolée. Aucun fichier
de production n'est remplacé ni aucun frontend servi par cette opération.

Le WASM retenu est explicitement l'ancien binaire, SHA-256
`32f6da6f6f9bc0282c7df9600d81a02eab55624b00a65420ae987613fafda033`.
`current_candidate_wasm_installed` et `runtime_executed` restent faux. La
construction réelle du nouveau WASM, son bindgen, la compatibilité du worker
de navigation, le loader et le cycle auteur restent à qualifier.

## Compatibilité et exécution du fournisseur de navigation conservé

`navigation-compatibility-v2/result.json` confirme que les 28 pins source
du manifeste conservé correspondent au candidat figé, de même que les trois
artifacts JS/WASM/worker. L'inventaire couvre tous les inputs utilisés par le
build.rs du fournisseur ; son hash recalculé est présent dans le WASM.
Le reçu V1 reste conservé : son faux verdict de reconstruction venait du tri
insensible à la casse de Python Path sous Windows, différent de celui du
build Rust. V2 trie explicitement les chemins relatifs de manière sensible
à la casse et reproduit l'identité effectivement embarquée.

`navigation-wasm-runtime-v2/result.json` confirme une exécution réelle du
WASM conservé sous Node 24.19.0 : génération d'une tuile plane avec maillage
polygonal et triangles de détail non vides, maintien du token 17 et de la
révision 7, rejeu identique octet par octet, refus d'une géométrie modifiée
portant l'ancien hash et refus d'un input dépassant 32 Mio. Les artifacts
sont inchangés après exécution. Les inputs et outputs sont conservés avec
leurs SHA-256. Les valeurs de la fixture sont exactement représentables et
sa sérialisation suit celle du hash Rust.

L'essai runtime V1 et son log restent conservés : il a échoué sur le texte
attendu par l'assertion, après génération et rejeu réussis. V2 exige le message
exact du fournisseur, `navigation geometry hash mismatch`, pour ce refus.
Ce contrôle prouve l'exécution de la fonction WASM, pas un échange avec le
Web Worker dans le navigateur ni la navigation effective dans Studio. Ces
deux derniers critères restent ouverts, comme le nouveau frontend Studio.

## Transport du worker et préparation de la construction Studio

`navigation-worker-runtime-v1/result.json` termine à PASS sous Node
worker_threads avec un shim de transport navigateur. Le script worker réel
du frontend est exécuté sans modification ; le bootstrap local fournit self,
postMessage et uniquement le fetch du WASM attendu. Les buffers d'entrée sont
transférés et détachés, les tokens des quatre requêtes sont conservés, le
résultat correspond octet par octet à l'essai direct du fournisseur. Le
worker refuse le JSON malformé et l'entrée dépassant 32 Mio, puis répond
correctement à une nouvelle requête valide. Les trois artifacts sont épinglés
avant et après l'essai. Le bootstrap et les reçus sont conservés.

Cet essai ne constitue pas une exécution navigateur ou une validation de
navigation Studio ; ces deux flags restent explicitement faux.

Le helper `BuildCombinedScannedStudioWasm20261006.py` est préparé et sa syntaxe
Python vérifiée. Il exige le reçu PASS du check complet WASM du même candidat
et les 1 107 hashes inchangés avant toute construction. Il conserve les sorties
Cargo JSON et stderr, identifie l'artifact WASM réellement lié, puis ne le
copie qu'après vérification des inputs et du produit. Il ne lance ni bindgen
ni runtime et ne remplace pas de fichier de production. Sa construction
n'est pas lancée tant que le check Studio 15095 attend le job 24461.

## Assemblage du frontend courant préparé, non exécuté

`AssembleCombinedScannedStudio20261006.py` est préparé avec syntaxe Python
validée. Il exige la construction WASM liée du même candidat, vérifie les
1 107 pins, le frontend préparé complet et les reçus de navigation. Il exécute
wasm-bindgen 0.2.127 dans un nouveau dossier, puis copie ses outputs dans une
nouvelle copie du frontend. L'ancien JS/WASM et ses déclarations sont exclus
de cette copie ; les nouveaux fichiers générés les remplacent ensemble. Le
frontend préparé ne comporte aucun ancien snippet.

Les chemins Windows étendus sont utilisés pour les payloads imbriqués. Le CSS
et le script de compatibilité sont pris dans les sources figées actuelles.
Chaque autre ressource est copiée et vérifiée par SHA-256 ; tout conflit avec
un nouveau fichier généré est refusé. Le reçu final doit inventorier exactement
les fichiers attendus. Les sources, le WASM brut et le frontend de base sont
recontrôlés après assemblage. Ce helper n'est pas encore exécuté : le check
Studio 15095 attend toujours le job courant 24461. Aucun frontend courant,
bindgen exécuté, rendu, lifecycle auteur ou paquet portable n'est déclaré
qualifié par cette seule préparation.

## Résultat terminal du check Studio courant et dérive observée

Le handle 24461 termine à code 1 pour le garde-fou de stabilité. Le reçu
`studio-library-release-check-20261006-v1/result.json` rapporte Cargo à code 0,
mais `inputs_unchanged: false`. Le log conserve la durée réelle de 168 min
03 s. Ce résultat ne qualifie ni la correction de libération de l'UI, ni le
cycle de réouverture, ni le candidat figé scanné.

`drift-inspection-v1/result.json` relève trois inputs actuellement différents
des pins initiaux : `crates/forge_assets/src/source_graph_attributes.rs`,
`crates/forge_engine/src/source_graph.rs` et
`crates/forge_engine/src/source_graph_glb.rs`. Les hashes avant/après et les
mtimes actuels sont conservés. L'inspection est postérieure au reçu terminal ;
elle n'attribue pas les changements à un processus ou à un instant exact.
Les changements sont préservés, sans restauration des anciens fichiers.

Le garde-fou d'adoption ancien reste applicable : le code Cargo 0 ne remplace
pas l'exigence d'inputs inchangés. Les vérifications du candidat figé et la
construction native restent distinctes ; les handles 15095 et 66973 sont
confirmés actifs après la fin de 24461. Le premier conserve une attente de
verrou dans son log ; aucune relance n'est déclenchée sur cette attente.

## Sélection explicite du binaire pour le parcours natif

`scripts/tidewater/Verify-NativeDebrisWalk.ps1` accepte désormais BinaryPath
pour tester directement l'exécutable conservé par le build figé, avec les
ressources locales épinglées. Cela évite de qualifier un exécutable du cache
Cargo potentiellement remplacé par une autre construction. DistributionRoot
et BinaryPath sont mutuellement exclusifs. Le fichier choisi doit exister et
correspondre au SHA-256 attendu avant création de preuves ou lancement ; le
reçu de parcours ajoute selected_binary_path. Les vérifications antérieures
des ressources et du binaire après exécution sont conservées.

`native-selected-binary-guards-20261006-v1/result.json` confirme la syntaxe
PowerShell et deux refus réels : sélection contradictoire, puis mauvais hash
sur une fixture qui ne doit jamais être exécutée. Les deux essais n'ont créé
aucun dossier de parcours ni lancé de runtime. Ce changement touche le
verifier du dépôt, pas les inputs de la copie figée en cours de compilation.
Il ne constitue pas une preuve de marche ou de rendu du nouveau binaire.

## Candidat figé V3 : poses et visibilité communes

`combined-scanned-candidate-20261006-v3/preparation.json` confirme une nouvelle
copie des 1 107 inputs du candidat V2. Les prédécesseurs de chaque overlay
sont vérifiés avant copie, de même que les manifests et les sources préparées.
Les inventaires et hashes des deux copies sont recontrôlés après préparation ;
les inputs V2 des jobs actifs restent inchangés. Aucun fichier de production
n'est adopté par cette opération.

Trois fichiers diffèrent effectivement de V2 : scanned_scene.rs côté Engine,
scanned/frame.rs et scanned.rs côté Studio. Ils regroupent les préparations
Engine V3 et Studio V4 : pose globale et visibilité héritée copiées depuis la
racine commune pour les draws tardifs, admission des draws et des ombres
conditionnée par cette visibilité, publication après propagation et frustum
mais avant collecte de visibilité. Les autres changements du candidat V2,
y compris les inputs include_str/include_bytes, sont conservés.

Cette préparation ne certifie pas la correction visuelle de ces risques.
Le check complet natif et WASM, l'initialisation réelle du schedule, les
changements de LOD, le masquage, le picking et le cycle auteur restent à
exécuter sur V3. Les résultats antérieurs de V2 ne sont pas transférés à V3.
Le check Studio V2 15095 a désormais quitté l'attente de verrou et avance
dans les crates Forge ; le build natif V2 66973 poursuit sa compilation.

## Check Studio V2 refusé et correction dans le candidat V4

Le check complet WASM V2 15095 termine avec Cargo 101, inputs inchangés.
Le moteur a passé son contrôle ; Studio rapporte une erreur E0308 dans
imported_scene/scanned.rs : le statut de collision Failed fournit un texte
emprunté et readiness doit renvoyer String. Le reçu et le log de cet échec
restent conservés ; aucune construction WASM V2 ni adoption n'est autorisée
par ce résultat.

`combined-scanned-candidate-20261006-v4/preparation.json` conserve les 1 107
inputs V3 et corrige uniquement cette conversion dans scanned.rs, en utilisant
error.to_string() au lieu de error.clone(). Les hashes avant/après et ceux du
log et du reçu d'échec sont consignés. Les corrections de pose/visibilité V3
restent incluses. La provenance de l'overlay est alignée avant son premier
check ; les copies V2/V3 restent inchangées et aucun fichier de production
n'est remplacé. Le check complet Studio V4 est réellement lancé, handle
79374, avec les mêmes options, sans réduire son périmètre ni ses garde-fous.

Le contrôle ciblé Engine V3 32326 est également actif. Il compile le vrai
scanned_scene.rs préparé et son module de retirement contre une API Engine
native déjà compilée, avec sélection de dépendances par fingerprints Cargo
cohérents et hashes des sources/artifacts avant/après. Son périmètre reste
metadata seulement : il ne qualifie ni le moteur courant complet, ni WASM,
ni l'exécution ECS ou GPU. Aucun verdict n'est attribué avant son résultat
terminal. Le build natif V2 66973 poursuit séparément sa compilation.

## Limite du contrôle ciblé et chaîne V4 préparée

Le contrôle ciblé Engine V3 32326 termine à code 1 avec inputs et artifacts
inchangés. Le log relève E0583 pour le sous-module retirement dans le contexte
du harness à path absolu, puis E0463 pour la résolution de l'API Engine mise
en cache et des imports associés. Il n'apporte donc aucune preuve de types
ou de correction de pose. Ces erreurs de montage du harness ne sont pas
présentées comme des erreurs du build Engine complet. Le reçu et le log
restent conservés ; aucun remplacement de types par des stubs n'est fait.
Les types préparés doivent être vérifiés dans le graphe complet du candidat
V4, dont le check Studio 79374 est effectivement actif.

Les helpers `BuildCombinedScannedStudioWasmV4.py` et
`AssembleCombinedScannedStudioV4.py` sont préparés avec syntaxe Python validée.
Le build exige le reçu PASS V4 et ses propres pins, sans réutiliser le résultat
V2 refusé. L'assemblage vise le build et les sources V4, tout en réutilisant
les payloads vérifiés et le worker compatible de la copie V2. Les pins du
fournisseur sont comparés aux sources V4 lors de l'assemblage ; la copie des
ressources ne devient pas une preuve de compilation ou de navigation Studio.
Ces helpers ne sont pas encore exécutés et n'adoptent rien en production.

## Contrôle natif complet V4 lancé

Le check complet natif V4 est lancé sous le handle 26325, avec le même
harness que V2 : forge_engine et l'exemple tidewater_plant_probe, offline,
locked, j1, puis recontrôle de tous les inputs du candidat. Il utilise son
propre reçu `combined-scanned-candidate-20261006-v4/native-check-v1/result.json`
et ne réutilise pas le PASS V2. Il peut attendre le verrou du build V2 66973
sans modifier ses sources ou son exécutable conservé. Le check Studio V4
79374 reste indépendant. Le contrôle ciblé refusé n'est pas utilisé comme
substitut de ces deux contrôles complets ; aucun nouveau PASS n'est déclaré
avant leurs résultats terminaux.

## Check Studio V4 complet PASS et construction WASM lancée

Le handle 79374 termine à code 0. Le reçu
`combined-scanned-candidate-20261006-v4/studio-check-v1/result.json` confirme
le check complet de forge_studio --lib pour wasm32-unknown-unknown, web,
offline et locked, avec les 1 107 inputs inchangés. La préparation épinglée
est `5e4d1863c6be847f51c26430f7ea630de70a8ff4e5220829d36ef08fe8b41aa1`.
Ce résultat couvre les types du raccord scanné intégré, y compris les
corrections de pose/visibilité et la conversion du texte de collision. Il
ne couvre ni la liaison WASM finale, ni l'exécution du schedule ou du GPU,
ni le cycle auteur, ni la parité visuelle.

Le helper BuildCombinedScannedStudioWasmV4.py est lancé après ce reçu PASS.
Il construit réellement le frontend release, conserve le log Cargo et son
JSON d'artifacts, vérifie les pins avant/après et ne copie que le WASM lié
du candidat courant. La construction native V2 et le check natif V4 restent
séparément actifs. Aucun ancien WASM n'est présenté comme ce nouveau build.

## Condition de lancement du nouveau frontend dans Studio natif

L'inspection de BuildNative.py et RunEditing.py du candidat shadow V2 confirme
que son shell Tauri est construit avec frontendDist et embarque le frontend
sélectionné. Son exécutable ancien ne permet donc pas, par simple copie de
fichiers voisins, de tester le nouveau WASM V4. La qualification native de
Studio exige un nouveau shell intégrant le frontend courant assemblé et des
reçus épinglant cette paire. Le scénario conserve une fixture auteur isolée,
les inventaires attendus et les vérifications des helpers, frontend et binaire
après exécution. Les résultats UI de l'ancien candidat ne sont pas transférés
au nouveau. Aucune nouvelle construction du shell ni exécution UI n'est
attribuée à cette inspection ; le build WASM V4 reste actif.

## Picking scanné : portée de la géométrie et cas à vérifier

L'inspection des sources V4 confirme douze meshes séparés par prototype et
LOD, avec tint.x fixé à l'ordinal du prototype. Le programme acquis scanné
conserve la position du sommet si cet ordinal correspond au record d'instance.
La variation rnd est utilisée dans la surface, sans déformation vertex dans
sourceVertex. Cela réduit le risque de triangles CPU appartenant à un autre
prototype ; cette observation n'est pas une preuve de picking à l'écran.

La sélection commune utilise MeshRayCast sur les triangles et inclut les
autres meshes visibles comme occluders. Les fragments source appliquent
lodFadeVisible et peuvent être éliminés pendant les transitions, ce que ce
raycast CPU ne reproduit pas. Les tests de clic doivent donc couvrir ces
transitions, la pose retouchée, les racines masquées et l'occlusion, sans
déduire une équivalence GPU de la seule présence de ManualPlacementPart.
Les deux niveaux de détail d'une même instance partagent son identité
commune ; aucun comportement d'inspecteur propre à une map ou à un asset
n'est ajouté par cette inspection.

## Build WASM V4 et assemblage courant : succès, runtime à vérifier

Le job de construction WASM V4 s'est terminé avec code 0. Son reçu
`combined-scanned-candidate-20261006-v4/studio-wasm-build-v1/result.json`
confirme `linked_application=true` et `candidate_inputs_unchanged=true`,
avec le manifeste de préparation
`5e4d1863c6be847f51c26430f7ea630de70a8ff4e5220829d36ef08fe8b41aa1`.
Le WASM brut mesure 129473780 octets, SHA-256
`b3c7464b9f711c54401f570c17ae0ac119c9589606e017279949caa727b8e4ac`.

L'assemblage `frontend-current-v1/result.json` a ensuite réussi : 8014 fichiers,
nouveaux outputs wasm-bindgen 0.2.127, payloads conservés et navigation compatible
vérifiée. Le statut reste
`CURRENT_FRONTEND_ASSEMBLED_REQUIRES_RUNTIME_VALIDATION` : aucune équivalence
visuelle, exécution GPU ou validation du cycle d'édition n'en est déduite.

La première préparation du host `studio-host-v1` a échoué au contrôle canonique :
le constructeur refuse un frontend qui contient déjà forge-kit lorsqu'un kit
explicite est également fourni. Ses logs sont conservés. La préparation corrigée
`studio-host-v2` fournit au contrôleur une copie vérifiée sans forge-kit et le kit
explicite intact, tout en conservant le frontend complet comme entrée du build.
Le contrôle canonique `INPUTS_VALID` passe sans modifier sa règle de refus.
Le manifeste source isolé est
`01bc53beaeb300b6d0b4fa804f37deeff6687f4da5f80cc958d4e77fc023233b`.

Le build Tauri de cette copie a été lancé ; au dernier contrôle son journal
indiquait l'attente du verrou du répertoire de build partagé. Aucun succès de
construction du host, lancement UI, picking, persistance, rendu ou performance
n'est encore établi. Les sources V4 utilisées par les autres jobs et les sources
de production ne sont pas remplacées par ces copies de préparation.

## Démarrage réel dans le navigateur : portée bornée

Le frontend V4 assemblé est servi uniquement sur `127.0.0.1:8869` depuis
`frontend-current-v1/frontend`. L'observation via Computer Use dans le navigateur
intégré confirme le démarrage de la scène de preuve par défaut : l'UI affiche
« WebGPU prêt Monde initialisé 1 fois », le canvas forge-canvas et la hiérarchie
Procedural ground / Procedural cube / Proof sun / Proof camera. Ce démarrage
ne constitue pas un rendu de l'île ni une qualification de performance.

Le lien visible vers `?authoring=1` ouvre la coque auteur. Celle-ci affiche
« Mode navigateur autonome : backend Tauri absent », aucune zone ouverte,
et les boutons Jouer / Sauvegarder désactivés. L'ouverture de la bibliothèque
affiche le même refus dans les résultats, sans catalogue chargé. Aucun backend
factice n'est injecté pour contourner cette limite. La lecture des logs console
error/warn de cet onglet ne retourne aucune entrée à cet instant.

Cette observation prouve le démarrage de l'application WASM courante et de sa
coque auteur. Elle ne prouve pas le chargement des 141 instances scannées,
leur picking, leurs ombres, le cycle de sauvegarde ou la parité Tidewater.
Ces contrôles restent à exécuter avec le nouveau host Tauri et une fixture isolée.

## Inventaire attendu V4 : 1127 identités dérivées des sources

`PrepareCombinedScannedInventoryV4.py` prépare
`combined-scanned-candidate-20261006-v4/expected-inventory-v1/result.json`.
La recette Python d'inventaire antérieure est vérifiée contre son hash revu
`24fac9af3273da169aa06c289d167ecf47b877808e69d5b16f91f6ef78fd7402` ; elle
n'est pas présentée comme un fichier du snapshot Cargo, qui ne la contient pas.
Les 986 entrées précédentes sont régénérées depuis la fixture et les packages,
puis comparées exactement au manifeste antérieur épinglé. Les 141 scans sont
ajoutés depuis le descripteur géométrique du package scanné vérifié, avec ses
82 payloads et le programme d'instances révisé. Les recettes UUID correspondent
aux contrats communs de variation d'asset et de placement importé.

Le résultat contient 1127 identités uniques : 23 placements manuels acceptés et
1104 sources héritées. Aucun objet ni retouche de la fixture n'est modifié.
Le statut `EXPECTED_INVENTORY_PREPARED_REQUIRES_UI_VALIDATION` ne prouve pas
leur chargement dans Studio : la comparaison à la hiérarchie réelle, le picking,
les collisions, les ombres et la persistance restent à exécuter.

## Construction native V2 et marche réelle sur l'île : succès borné

Le build natif V2 est terminé avec code 0 et inputs inchangés. Le manifeste de
préparation reste `2e8800080e01480f27fba8fdb37a27623adc3192e34bce295863e67dbe43706f`.
Le binaire copié et vérifié mesure 134267904 octets, SHA-256
`ffb3728bb5d7bdd413bbc1a695b00421374761409c5d6395cae7d6b249af5026`.
Il ne s'agit pas du build V4 : ses corrections de pose/visibilité Studio ne sont
pas attribuées à ce binaire V2.

`Verify-NativeDebrisWalk.ps1 -BinaryPath <binaire vérifié> -WithPebbles` a réussi
dans `combined-scanned-candidate-20261006-v2/native-walk-pebbles-v1`.
`execution.json` confirme code 0, pass=true, binaire et pins d'entrée inchangés,
ressources Pebble staged. `island.walk.json` confirme 8.995765686 mètres parcourus,
324 grounded_ticks, functional_pass, cadence_pass, shadow_cadence_pass et
cascade_navigation_pass. Le backend effectif est Vulkan sur Radeon RX 7800 XT.
La végétation canonique chargée compte 65383 éléments ; le journal indique aussi
805 objets Debris procéduraux et 141 racines scannées staged. Ces messages de
staging ne sont pas une preuve autonome de pixels visibles ou de picking.

La capture `island.png` a été inspectée : village côtier, passerelle, mer,
nuages, herbe et ombres de bâtiments visibles, avec capsule du contrôleur de test.
SHA-256 image `6e0bb9a39842ff6715ded789d86864f9b0cb9dc2df4245098504c67640feae61` ;
SHA-256 reçu de marche `a6a4cd6d1402c453a0a072f143ac3fdcc706eff90865b72515e6b142c576a5bc`.
Des avertissements de readback vers un canal fermé sont conservés à l'arrêt.
Ils ne sont pas supprimés du journal ni présentés comme une qualification sans
avertissement.

La portée du reçu reste celle des commandes du contrôleur et de la scène native :
clavier OS, traversée des bâtiments, couverture intégrale de la carte, pixels
Pebble, parité avec source1438, édition Studio et paquet portable restent ouverts.
Le check natif V4 a commencé après libération du verrou ; le host Studio attend
encore ce même répertoire de build au dernier contrôle.

## Paquet Aube et promenade depuis ZIP extrait — 2026-10-06

Build-WindowsPlaytest.ps1 a terminé avec exit 0 : OpenRPG-Forge-Aube-Tidewater-Win64-20261006-pebbles-001.zip, 7653 fichiers déclarés, 886804718 octets hors manifeste. Le paquet utilise le binaire natif V2 qualifié (ffb3728bb5d7bdd413bbc1a695b00421374761409c5d6395cae7d6b249af5026) et le launcher Aube production (434321efa0282c53acd8b24eb056c252952a91d2380bab93743eea1262ffb0c7). Ce n'est pas un runtime V4.

VerifyExtractedPebblePackage20261006.py a extrait une destination neuve et vérifié l'inventaire exact, les tailles et SHA-256 de tous les fichiers déclarés, les identités des deux exécutables et l'absence de modification du ZIP. Reçu : ../.tidewater-integration/portable-pebbles-extraction-20261006-v1.json (depuis la racine de stockage). SHA-256 du ZIP : 3b549ff56fefeac3741a2a9014813bb4cfdc7620f1ac8f3476d7b61019a6e285.

Verify-NativeDebrisWalk.ps1 -DistributionRoot portable-pebbles-extracted-20261006-v1 -WithPebbles a ensuite terminé avec exit 0 et pass=true. La promenade contrôlée a parcouru 8.995765686035156 m, avec 321 ticks au sol. Les données sont chargées depuis l'extraction. Reçu : .tidewater-integration/portable-pebbles-walk-20261006-v1/execution.json ; image SHA-256 dfb6c10c324985635d8c7f5ed52e38d20a7f600261d5961ca4502eadf819386b ; promenade SHA-256 2a1e2e7ff2fe4a55675152aead2c4aa0a03a66d55161dcd52fae0e528fd29956.

Limites : le trajet launcher graphique Jouer → lancer l'île reste à vérifier. Ce test ne qualifie ni la carte entière, ni les pixels des cailloux indépendamment, ni l'édition Studio, ni la parité avec la référence. Les trois avertissements gpu_readback à la fermeture restent conservés dans runtime.log. La collecte des notices rapporte 596 packages et 48 textes manquants ; ce point reste ouvert pour la qualification de distribution. Les builds natif V4 et host Studio V4 étaient toujours actifs lors de la préparation du paquet.

## Builds V4 et préparation des essais d'édition — 2026-10-06

Le handle native-check V4 (26325) est terminal exit 0 : contrôle Cargo complet de forge_engine/tidewater_plant_probe, candidate_inputs_unchanged=true, préparation 5e4d1863c6be847f51c26430f7ea630de70a8ff4e5220829d36ef08fe8b41aa1. Aucun rendu n'est qualifié par ce check. BuildCombinedScannedNativeV4.py lance maintenant le véritable build sur les mêmes sources gelées, avec contrôle des empreintes et copie dédiée du binaire ; résultat encore attendu.

Le host Studio V4 (58224) est terminal exit 0. Reçus dans combined-scanned-candidate-20261006-v4/studio-host-v2/native-result.json et desktop/build-proof.json. Exécutable SHA-256 1e4b2028984c039555b6bbbc569ef13818797ada92b66c6b487e0faf3999edfc, WASM embarqué 04376f7d0e2c03b038db94c98753364a24932f4f4764c7f8b6f5a0552fe8f1b0. Les seuls outputs générés Tauri sont audités et conservés dans la copie dédiée ; les sources non générées, le frontend et le candidat original sont inchangés. Qualification BUILD_ONLY, runtime_verified=false.

Tentative de lancement du paquet extrait via Computer Use (@oai/sky, launch_app) : « Computer Use app approval timed out ». L'inventaire list_windows suivant ne montre aucune fenêtre Aube/Tidewater/OpenRPG. Aucun clic Jouer ni lancement du jeu depuis le launcher n'a donc été effectué ou qualifié. Ce n'est pas une preuve de panne de l'exécutable.

PrepareStudioEditingInputsV4.py audite les sources conservées et prépare une copie intégrale du frontend à côté du nouvel exécutable pour les outils canoniques, avec inventaire attendu 1127 (986 précédents + 141 scannés). Première tentative interrompue avant copie : l'inventaire Python sans préfixe Windows long path omettait des fichiers. Correction : chemins explicites \\?\ pour lecture et staging ; nouvelle validation en cours, sans assouplir l'inventaire ni les hashes. Aucun essai UI ou cycle de sauvegarde n'est revendiqué.

La préparation corrigée est terminale exit 0 : 8014 fichiers frontend vérifiés, inventaire strict 1127, outputs Tauri conservés audités. Le validateur canonique Invoke-FishStandStudioSmoke.ps1 -ValidateOnly a accepté les entrées. Reçu : studio-host-v2/editing-input-validation-v1/result.json. app_launched=false, editing_verified=false, visual_parity=false. Ce PASS valide les entrées des essais, pas l'édition effective.

## Récupération des notices AccessKit manquantes — 2026-10-06

Le paquet qualifié contient toujours 48 packages sans texte de notice. L'inspection des archives Cargo confirme notamment l'absence de LICENSE dans accesskit 0.24.1, accesskit_consumer 0.35.0, accesskit_windows 0.32.1 et accesskit_winit 0.32.2 ; ce n'est pas un échec du parcours récursif du collecteur.

AcquireAccessKitNotices20261006.py a acquis 7 fichiers depuis le dépôt officiel AccessKit aux commits exacts des .cargo_vcs_info.json locaux : a55d3e1a18bb9ef0e4bccc9083fb13c3e0ad8969 et 1bbcf100942bac96c2c3a4a91cb67b0b20201a24. Chaque acquisition conserve URL, taille, SHA-256, identité/version/SPDX du package, empreinte du Cargo.toml réellement utilisé et metadata VCS. Reçu : .tidewater-integration/accesskit-notice-review-20261006-v1/acquisition.json, statut ACQUIRED_FOR_REVIEW_NOT_YET_PACKAGED. Le texte provient de l'amont ; aucun modèle de licence ou copyright n'a été inventé.

Les 4 packages ont maintenant des notices candidates traçables ; ils ne sont pas encore intégrés au ZIP. Les archives, manifestes et reçus précédemment qualifiés sont inchangés. Aucun verdict de conformité globale, de distribution complète ou de parité n'est acquis par cette récupération.

Le build natif V4 reste actif (handle 17943), progression observée forge_core → forge_gameplay → forge_navigation. Les avertissements de hard-link du cache sont conservés ; aucune relance ni suppression du cache.

## Première exécution Studio V4 et restrictions WebView2 — 2026-10-06

Invoke-FishStandStudioSmoke.ps1 sur le nouvel exécutable V4/inventaire1127 a réellement été exécuté dans une copie isolée (runtime-stand-v4). Résultat terminal exit1 : aucun target CDP exact joignable dans le délai60s. Le ledger conserve source_preserved=true et expected_initial_ui_objects=1127 ; aucune édition n'a été exécutée sur cette tentative.

DiagnoseStudioStartupV4.ps1 a rejoué uniquement le démarrage avec profil neuf, logs WebView2 et délai180s. Résultat terminal exit1 après181.83s. webview.log conserve9 crashes du processus GPU (exit_code=-1073741790, 0xc0000022 / accès refusé), puis FATAL GPU process isn't usable. Aucun changement de protection WebView2 ou de qualité rendu n'a été effectué ; cette trace indique une panne de démarrage sous le contexte restreint, pas une preuve de défaut du rendu Forge.

Une nouvelle exécution du test canonique avec contexte Windows normal a été autorisée par la revue automatique d'escalade (runtime-stand-v4-unrestricted-v1, handle76172). Elle a atteint le Studio réel et passé le contrôle strict de l'inventaire : initial-world-state.json expose1127 placement_options ; les identités et labels sont comparés au manifeste indépendant. La capture fish-stand-applied.png a été inspectée : viewport3D Tidewater visible, sélection Échoppe de Joe, proposition acceptée23→24placements. fish-stand-reopened.png existe ensuite ; attendre le ledger terminal avant verdict de persistance/ombres/réglages. Aucun PASS global d'édition ni parité n'est acquis pendant que ce test reste actif.

Le test Studio V4 exécuté en contexte Windows normal (76172) est terminal exit0, status=passed. Le ledger confirme inventaire initial1127/final1128, authoring_pass=true, source_preserved=true, reopened=true, persistance des réglages graphiques et mouvement résolu du joueur avant retour édition sans mutation des documents sauvegardés. Cinq captures sont conservées. L'insertion appliquée puis la réouverture de l'échoppe sont acquises pour ce scénario ; le cycle générique complet TRS/duplicate/delete/undo reste distinct.

Deux échantillons d'ombres vivantes passent : même génération résidente, encodage/wind_time et les trois couches de cascade avancent. Le scope est explicitement Browser requestAnimationFrame cadence; render-pass counters are encoded work, not GPU completion fences. Aucun verdict de pixels/profondeur GPU ou de qualité des ombres n'est déduit de ces compteurs.

Défaut majeur de fluidité : raf_fps=1.11023 puis1.10280, p95_raf_ms=902.9 puis909.9, cadence d'encodage des ombres identique. Ce résultat ne qualifie pas la performance Studio ; le test fonctionnel PASS conserve ce défaut ouvert. Préparation d'un cycle générique sur6 sources (bois/tissu/Nature +3 types scannés) et une échoppe auteur, dans .placeables-smoke-tidewater-scanned-v4-20261006-v1 ; les sélections se font par labels réellement affichés, aucune injection d'identité GPU.

## Diagnostic de cadence préparé — 2026-10-06

ProfileStudioV4.ps1 prépare une observation séparée du candidat exact : profil CPU Chromium de10s (sampling1000µs), métriques Performance avant/après, RAF/visibilityState/taille réelle du canvas et capture. Il demande aussi un adaptateur WebGPU de diagnostic et étiquette explicitement ce résultat comme distinct du GPU réellement utilisé par Bevy. Il conserve la qualité et les protections WebView2 ; aucune variante simplifiée de carte ou d'effets n'est substituée. Syntaxe PowerShell vérifiée ; aucune mesure issue de ce nouveau script n'est encore exécutée.

Précision sur la cadence précédente : chaque échantillon contient seulement7 RAF sur6305ms puis6347.5ms ; la capture de réfraction est657x399. Le test démarre avec WindowStyle Hidden puis Page.bringToFront, sans conserver document.visibilityState. La cadence~1.1FPS est bien observée, mais un éventuel bridage de fenêtre inactive reste à distinguer du coût CPU/GPU. Ce n'est pas un benchmark1920x1080 final ni une preuve de performance en utilisation visible. Le nouveau diagnostic recueillera cette distinction avant correction du rendu.

Le cycle générique V4 (65325) et le build natif V4 (17943) restent actifs, vérifiés via leurs handles. Le profilage sera exécuté après la fin du cycle pour éviter une seconde scène concurrente. Aucun verdict complet TRS/scannés/Undo ou de parité n'est encore acquis pour ce cycle en cours.

## Profil CPU V4 et correction bornée d'identités préparée — 2026-10-06

Le cycle générique V1 est terminal exit1 : QuotaExceededError sur la persistance __forge_gate2_world_audit_entries_v1. La copie du helper utilisait déjà gzip-base64 ; le quota reste dépassé. L'échec est conservé et ne devient ni un PASS d'édition ni une preuve d'erreur du modèle auteur. Une variante isolée du helper remplace uniquement le stockage du journal par IndexedDB, conserve les enregistrements et la compression existante, attend les transactions et teste un aller-retour réel de6MiB aléatoires/8MiB base64 sur une clé distincte. Les scripts produit modifiés en parallèle restent intacts. Recette world-audit-indexeddb-variant-v1.json ; nouvelle copie .placeables-smoke-tidewater-scanned-v4-20261006-v2, mêmes7cas, nouveau test actif18495. Le test d'aller-retour n'est qualifié qu'après observation terminale.

ProfileStudioV4.ps1 est terminal exit0 (91650), sur le même exécutable1e4b2028, copie isolée, après arrêt du cycle précédent. result.json : failure=null, document.visibilityState=visible, canvas1314x565, RAF9.29264FPS/p9576.4ms. C'est une vue d'édition mesurée sous profilage, pas le mode jeu du test précédent (~1.1FPS), ni une qualification finale1920x1080. L'adaptateur demandé séparément pour diagnostic rapporte AMD/rdna-3 ; le script ne l'assimile pas à une preuve du GPU Bevy. Profils et capture conservés dans studio-v4-profile-20261006-v1.

AnalyzeStudioCpuProfileV4.py analyse8221échantillons/12635.276ms de profil CPU. SHA1State::process représente29.3896% du temps échantillonné (3713.453ms), idle23.8457%. Il ne s'agit pas de timings GPU ; les lignes inclusives peuvent se recouvrir, notamment en récursion. Le code asset_ordinal recalculait effectivement les UUID déterministes par recherche linéaire à chaque comparaison ; desired_fish_props apparaît aussi dans les échantillons.

StageAssetOrdinalCacheV1.py prépare trois inverses bornés (78identités fish-props,43beach-props,27village-props) via LazyLock/BTreeMap. La première occurrence est préservée comme dans l'ancien find ; asset_id, hashes, géométrie, validation et documents sont byte-identiques hors fonction inverse. Overlay asset-ordinal-cache-20261006-v1/staged et préparation avec les empreintes. Aucun consommateur gelé V4 ni production n'est modifié. Statut STAGED_REQUIRES_CONFORMANCE_TEST_AND_PAIRED_PROFILE : gain non mesuré, compilation/conformité puis nouveau profil complet encore requis.

## Conformité des identités et candidat complet V5 — 2026-10-06

TestAssetOrdinalCacheV1.py est terminal exit0 (4438) : 12tests PASS sur les148identités des trois familles, limites/identités inconnues/croisées et appels concurrents. Le test compile les fonctions exactes extraites avant/après avec le véritable forge_core gelé ; les versions/checksums des dépendances correspondent au Cargo.lock V4 et les sources core sont inchangées. Il ne remplace pas la compilation intégrale du crate, un contrôle WASM ou une mesure de gain. Reçus et log dans asset-ordinal-cache-20261006-v1/conformance-v1.

PrepareCombinedScannedCandidateV5.py a créé une copie complète1107fichiers ; exactement3modules modifiés par les caches inverses. Le candidat V4 et sa préparation sont inchangés. Empreinte de préparation V5 : fb334e6d982c2541f8ebc03874c669e8e0ff0200ab04c8e3ca195dff50a16b61. Le contrôle Studio/WebGPU intégral est lancé sur ce V5 via le harness source-pinné (54895). La production reste inchangée ; performance_gain_verified=false et visual_parity=false. Le build natif V4 (17943) et le cycle générique avec journal IndexedDB (18495) restent actifs.

## Analyse CPU sans double comptage et préparation du parcours Aube

AnalyzeStudioCpuProfileV4V2.py est terminal exit0. Le nouveau reçu
studio-v4-profile-20261006-v1/cpu-analysis-v2.json conserve les empreintes du
profil brut, du résultat runtime et de l'analyse précédente. Sur8221échantillons,
6046 contiennent une fonction répétée dans la pile. Chaque fonction est désormais
comptée une seule fois par échantillon pour l'attribution inclusive : aucune ligne
ne dépasse100%. Les lignes inclusives distinctes continuent de se recouvrir.
L'attribution exclusive est strictement identique à la précédente ; les29.3896%
de SHA1State::process ne changent donc pas. Cette correction du diagnostic ne
mesure aucun gain du candidat V5 et ne fournit aucun timing GPU.

VerifyPortableAubeLaunchV1.ps1 est préparé et sa syntaxe PowerShell est validée.
Il utilise le paquet extrait vérifié, un profil WebView2 et LOCALAPPDATA isolés,
des clics publics par coordonnées sur Jouer puis Lancer l'île, et exige un enfant
natif du launcher possédant le chemin et SHA-256 exacts du runtime inclus.
La création de la fenêtre est un contrôle distinct des pixels du jeu, non qualifiés
par ce script. Le test attend la fin du cycle Studio pour éviter deux scènes3D
concurrentes ; aucune exécution ou réussite de ce parcours n'est encore acquise.
Le helper de test est lu depuis la copie isolée, pas remplacé en production.

BuildCombinedScannedStudioWasmV5.py est préparé, syntaxe Python validée. Il conserve
les gardes du builder complet : résultat du check V5 réussi, sources inchangées,
build release WASM intégral et copie de l'artefact réellement lié par Cargo.
Il n'est pas lancé avant le succès terminal du contrôle54895. Au dernier contrôle
des handles17943/18495/54895, les trois travaux sont toujours actifs ; le journal
du check V5 montre sa progression jusqu'à la compilation de forge_studio.

## Notices WebView2 acquises aux commits des archives Cargo

AcquireWebView2Notices20261006.py a terminé avec code0. Les trois packages
webview2-com0.38.2, webview2-com-macros0.8.1 et webview2-com-sys0.38.2 ont
chacun un texte LICENSE récupéré depuis le dépôt officiel wravery/webview2-rs,
aux commits exacts b74dc5e2b394044bea5191052868ce7a106c202c et
dffa41a8a46d3f5565eefbff2de57d38d399f158 présents dans leurs archives Cargo.
La recette vérifie également path_in_vcs et l'absence de dirty dans ces metadata.
Les notices MIT conservent le copyright amont ; aucun texte n'est inventé.

Reçu : .tidewater-integration/webview2-notice-review-20261006-v1/acquisition.json.
Les manifestes Cargo, metadata VCS, URL, tailles et SHA-256 sont conservés.
L'INDEX du paquet extrait reste byte-identique. Ces3packages s'ajoutent aux4
AccessKit déjà préparés :7packages disposent de notices candidates hors du ZIP,
qui rapporte toujours48packages sans texte embarqué. Le statut reste
ACQUIRED_FOR_REVIEW_NOT_YET_PACKAGED, sans qualification globale de distribution.
Au dernier contrôle des handles, le build natif V4, le cycle générique Studio et
le check WebGPU V5 restent actifs ; aucun travail n'est relancé sur cette attente.

## Observation non intrusive du cycle IndexedDB en cours

ObserveCommonLifecycleV2.ps1 a terminé code0 en contexte Windows normal.
Le PID41992 est vérifié contre le chemin du candidat isolé et le SHA-2561e4b2028.
Le port58060 est extrait uniquement du processus WebView2 enfant de ce host,
puis le titre et l'URL exacts de la page sont contrôlés par le helper figé.
Aucun clic, changement de focus ou appel IPC produit n'est effectué par
l'observation. Reçu : common-lifecycle-observation-20261006-v1.json.

À09:51:37UTC, document.visibilityState=visible, command_status=Session synchronisée,
audit_present=true et persistence_error=null. Le journal expose840entrées,
dont3open_managed,57preview_zone,70apply_zone,2save et2close. Ces compteurs
indiquent la progression du scénario ; ils ne remplacent pas ses assertions.
Le diagnostic storage_roundtrip, auparavant non exporté dans le ledger canonique,
est maintenant observé : passed=true,8388608caractères stockés,
6291456octets aléatoires source, SHA-256
6b0418194d68a398338710e91acfe5aeee28715689d80d33164395d3af9d38e2.
Le passage réel au-delà du quota sessionStorage est ainsi confirmé pour cette
installation du journal. Le test18495 reste actif ; aucun verdict terminal
de suppression/tombstones/Undo/Redo/réouverture complète n'est encore attribué.

## Sélection CPU réelle des arbres projetant des ombres dans Studio V4

EvaluateStudioV4PlantCasters.py termine code0 et conserve
studio-v4-plant-caster-evaluation-v1/result.json. Il extrait les deux observations
du ledger Studio terminal passed, puis exécute le véritable évaluateur
evaluate-plant-shadow-selection.py sur chacune, sans modifier le ledger ni
l'évaluateur. Les empreintes des entrées et observations sont conservées.
Les deux évaluations passent : cinq groupes identifiés, caméra courante cohérente,
source commune et comptages cohérents. Portée strictement CPU_SELECTION_PASS.

Premier échantillon : palms56soumis, plageLOD[0,120,120.0100], distancebase
max129.9836m ; bananas79soumis, [0,100,120], max125.5300m ; monsteras1soumis,
[0,85,105], max77.3606m ; broadleaf47soumis, [0,85,105], max114.2439m ;
canopy4soumis, [0,65,65.0100], max52.9604m. capacity_dropped=0 pour tous.
Respectivement22/2/0/1/0instances soumises dépassent la fin de leur plageLOD.
La transmission CPU de ces instances ne prouve pas leur conservation par le
masque shader : aucune distance de pixels d'ombre n'est déduite de ces maxima.
Le diagnostic concerne les cinq groupes de plantes arborées observés ; il
ne qualifie pas les ombres de l'herbe ni l'ensemble des récepteurs.
Le réglage demandé par le propriétaire reste à accepter sur des captures et
profondeurs GPU appariées, pas à partir de ces seuls comptages.

## Cycle commun V2 terminé :7cas exercés, dernière réouverture en échec

Le handle18495 est terminal exit1. Le ledger common-placeable-studio-ledger.json
conserve status=failed_closed, source_preserved=true, frontend_preserved=true,
cdp_listener_closed=true. Sept cas figurent dans cases et sept dans deletions.
Le scénario a franchi les assertions de transformations, verrouillage,
duplication, sauvegarde et première réouverture, puis suppression/Undo/Redo et
contrôle des tombstones du checkpoint supprimé. Checkpoint édité SHA-256
a758ddc520f40cdf3870a5d3b914e698fa873aecd5e5c60a3e99a97c4f418fb6 ;
checkpoint supprimé b4fbb3e7f09d77bf0b90d0d5b386cfb1fa23dda631ec5203c9455c988c3ba153.
Échec final : Verified imported library did not become ready within180seconds,
dernier état Vérification de la bibliothèque locale… après le calcul de
l'environnement. La réouverture finale sans résurrection n'est donc pas validée.
Le test complet reste FAIL, sans augmentation du délai ou réduction du contenu
pour le transformer en PASS. Le host41992 est fermé ; aucune scène concurrente
de ce test n'est laissée active.

## Parcours réel du launcher portable et check WebGPU V5 réussis

VerifyPortableAubeLaunchV1.ps1, tentative avec sortie portable-aube-launch-proof-
20261006-v4, termine code0 (14138). Les premières tentatives conservent leurs
échecs de harness : variable PowerShell réservée HOME avant tout lancement,
puis apostrophes typographiques interprétées comme délimiteurs dans deux chaînes
JavaScript. La correction emploie des here-strings littérales. Aucun exécutable
ou payload du paquet n'a été changé par ces réparations.

Le résultat final pass=true prouve les clics publics par coordonnées sur Jouer
puis Lancer l'île, le message de lancement, un unique runtime enfant du launcher
(PID33644, parent25564), chemin dans l'extraction et SHA-256ffb3728b exact,
puis un handle de fenêtre natif non nul. Le titre de cette fenêtre était encore
vide ; le test ne prétend pas avoir attendu ou capturé les pixels du jeu.
Trois captures Aube sont conservées, dont launch-acknowledged.png inspectée.
Le pont Tauri est présent, aucune ressource du port8848 n'est utilisée.
Exécutables/helper inchangés, processus launcher et jeu créés par le test fermés,
LOCALAPPDATA et profil WebView2 isolés. La promenade depuis le ZIP reste une
preuve distincte du rendu/contrôleur natif. Ni parité ni carte complète ne sont
qualifiées par ce parcours launcher.

Le check Studio/WebGPU V5 (54895) est terminal exit0, sources complètes inchangées,
préparation fb334e6d982c2541f8ebc03874c669e8e0ff0200ab04c8e3ca195dff50a16b61.
BuildCombinedScannedStudioWasmV5.py lance maintenant le véritable build release
WASM complet sur ces mêmes sources, avec copie du produit réellement lié par
Cargo après contrôle des empreintes. Le gain de fluidité reste non mesuré.

## Réouverture froide du checkpoint supprimé : réussite bornée

PrepareCommonReopenProfileV1.py prépare un runner depuis le diagnostic Studio V4
précédent, avec les helpers figés du test V2 et une nouvelle copie du véritable
authoring-root après les suppressions. Le ledger failed_closed et le checkpoint
b4fbb3e7 sont vérifiés avant préparation. Aucun profil ou checkpoint précédent
n'est remplacé. La syntaxe PowerShell est validée, puis le test82100 termine code0.

common-cold-reopen-profile-20261006-v1/result.json confirme failure=null,
source_checkpoint_unchanged=true et executable_unchanged=true. La bibliothèque
est chargée par les commandes UI canoniques, le workspace est ouvert par son
action publique exacte et le checkpoint supprimé est relu. Les assertions
Assert-CommonRemovalRecords passent. removed-checkpoint-ui.json conserve les14
identités supprimées (sept originaux et sept copies), removed_absent=true et
1121choix réellement chargés dans Studio. Le diagnostic fonctionne dans un
profil neuf ; il ne remplace pas le FAIL du cycle long de sept cas.

Le profil CPU/RAF conserve une visibilité visible et mesure9.17264FPS.
Cette scène contient les suppressions ; elle diffère de la baseline de24placements
du profil précédent et ne constitue pas un benchmark apparié du cache V5.
Le gain du candidat V5 reste à mesurer après sa construction complète.
Le résultat exclut une corruption évidente rendant ce checkpoint impossible
à rouvrir à froid ; l'origine exacte du délai de bibliothèque dans le cycle long
(journal accumulé, cycle des ressources, charge CPU ou autre) n'est pas encore
démontrée. Les deux builds17943 et85904 restent suivis sans relance.

## Adoption ciblée des caches d'identités dans les sources du projet

Après les12tests de conformité et le check complet Studio/WebGPU V5 terminal
PASS, les trois fichiers du projet Rust ont été revérifiés contre les empreintes
originales de la préparation. Ils étaient tous inchangés et déjà présents comme
fichiers non suivis du chantier. Aucune retouche concurrente n'est remplacée.
Seules leurs fonctions asset_ordinal ont été modifiées par patch ciblé.

Les empreintes après modification correspondent exactement aux trois sources
du candidat V5 testé : fish097286b5, beach867b61e7, village98a59cf9. Les148
identités restent inchangées ; l'inverse construit une table une fois et conserve
la première occurrence comme l'ancien find. Les autres fonctions et données
de ces fichiers sont byte-identiques au stade revu. rustfmt--check--edition2024
sur les trois fichiers termine code0. Reçu : asset-ordinal-cache-20261006-v1/
production-adoption-v1.json. Cette adoption source ne remplace aucun exécutable
déjà construit, ne constitue pas un gain de performance mesuré et ne modifie
pas les snapshots V4/V5 utilisés par les deux builds actifs. Aucun commit/push.

## Attribution du coût SHA1 aux appels réellement observés

AttributeStudioV4IdentityHashing.py termine code0 sur le profil brut déjà conservé.
identity-hash-ancestry-v1.json vérifie l'empreinte inchangée du profil et attribue
une seule fois chacun des2430échantillons exclusifs de Sha1State::process.
La somme reste3713.453ms. Le plus proche appel nommé est desired_fish_props pour
1801.141ms, desired_beach_props pour976.679ms et desired_village_props pour609.851ms.
Ces trois appels représentent91.227% du temps échantillonné de cette fonction
SHA1, soit26.811% du profil CPU total. Les autres appels et cinq piles complètes
sont conservés dans le reçu. L'inlining peut masquer asset_ordinal dans les piles ;
l'attribution ne prétend pas identifier une instruction source non observée.
Ce résultat confirme le ciblage des trois familles corrigées, sans mesurer le
gain réel du cache, le GPU ou la fluidité du futur Studio V5.

La suite complète V5 est préparée avec AssembleCombinedScannedStudioV5.py,
PrepareCombinedScannedStudioHostV5.py et BuildCombinedScannedStudioHostV5.py.
Les trois wrappers ont une syntaxe Python valide et conservent les constructeurs
V4 déjà exécutés, avec uniquement le candidat V5 et son module de préparation.
L'assemblage reste bloqué par ses propres gardes tant que le vrai build WASM V5
n'est pas terminal PASS. Les payloads complets et le worker vérifiés restent
ceux du frontend V2, contrôlés contre les sources V5. Aucun host V5 ou frontend
assemblé V5 n'est encore revendiqué. Les builds17943 et85904 restent actifs.

## Journal après nettoyage : absence confirmée et conservation future préparée

RecoverCommonJournalMetricsV1.ps1 copie uniquement les sept fichiers IndexedDB
du profil fermé du cycle commun V2 (1787777octets), puis les inspecte dans un
profil Aube isolé. La tentative1 conserve un échec Unexpected copied journal
size or type, sans modifier les originaux. La tentative2 est une inspection
explicite des clés et termine code0 : journal_present=false, entries=0,
stored_type=undefined, remaining_keys=[], aucune métrique temporelle récupérée.
Source_preserved=true et processus Aube créé par l'essai fermé, jeu non lancé.
Reçus : common-journal-recovery-20261006-v1/result.json et v2/result.json.
Le PASS de la seconde tentative est celui de l'inspection, pas d'une récupération
de journal ou du cycle d'édition.

Le finally du test figé appelle Restore-WorldAudit ; sa variante IndexedDB
attend les écritures puis supprime la clé persistante. Cette limite explique
l'absence constatée après nettoyage, sans établir la cause du délai180s de la
bibliothèque. Les métriques observées pendant le test et les checkpoints restent
conservés ; aucun délai n'est reconstitué à partir d'entrées inexistantes.

PrepareCommonJournalEvidenceV4.py prépare une variante isolée des trois helpers
du cycle V2 dans common-journal-evidence-20261006-v4/tools. Seul le test principal
ajoute, avant le nettoyage existant, une capture de toutes les entrées en mémoire,
compressée sans filtrage, avec SHA-256 du JSON décodé, comptages, état des requêtes
en cours et statistiques prétransport/transport/posttransport. Les données sont
figées avant toute attente asynchrone pour garantir l'accord entre comptes,
empreinte et contenu. Les délais incluent le hachage, la sérialisation,
la persistance et l'ordonnancement ; ils ne sont pas des timings isolés du disque
ou du GPU. Le premier stadeV3 non exécuté reste conservé ; utiliser leV4 pour
éviter des compteurs relus après une mutation asynchrone du journal.

Les sources parents restent inchangées, les assertions/actions/délais du cycle
et son contenu complet ne sont pas modifiés. Syntaxes PowerShell et JavaScript
validées. Cette variante n'a pas encore exécuté un cycle complet et ne corrige
pas à elle seule le défaut de réouverture. Aucun script de production concurrent
n'est remplacé. Les compilations17943 et85904 restent actives.

## V5 : compilation WASM et assemblage complets terminés

Le handle85904 termine code0. studio-wasm-build-v1/result.json vérifie
candidate_inputs_unchanged=true et la préparationfb334e6d. Le WASM brut lié
mesure129482587octets, SHA-256
1fad5c1362ef8b38d9b1148e549bf6d128bad73612ec010c076c1cb4c9df847e.
Il ne constitue pas encore une preuve de rendu ou de performance.

AssembleCombinedScannedStudioV5.py, handle4378, termine code0 avec8014fichiers,
CURRENT_FRONTEND_ASSEMBLED_REQUIRES_RUNTIME_VALIDATION. Les gardes du constructeur
vérifient sources, payloads, worker compatible et génération wasm-bindgen0.2.127.
PrepareCombinedScannedStudioHostV5.py est lancé avec le handle99034 ; son résultat
reste à attendre. Aucun exécutable Studio V5 n'est encore déclaré validé.

## Suivi natif : garde corrigée sans relance de compilation

La tentative FinishNativeV4Walk.ps1 échoue avant création de son répertoire,
sur la comparaison entre l'heure du processus et started_utc décodé par
ConvertFrom-Json. La lecture CIM actuelle confirme PID39756, le Python prévu,
la commande BuildCombinedScannedNativeV4.py et création UTC
2026-10-06T08:40:09.7211100Z. La requête affiche
2026-10-06T10:40:16.804845+02:00 ; l'écart réel est d'environ7secondes.
Le faux rejet n'indique donc pas un redémarrage du build. La représentation
PowerShell de la date décodée reste une limite du premier garde.

FinishNativeV4WalkV2.ps1 conserve le premier script, exige l'heure de création
exacte observée (tolérance1seconde), l'exécutable Python et la commande attendus,
puis les gardes de préparation, résultat et binaire dédié avant le test canonique.
Syntaxe PowerShell PASS. Handle45361 vivant : attente du processus39756 confirmée,
compilation non relancée. Le test de promenade n'a pas encore été exécuté.
Aucun gain de performance, parité visuelle ou qualification globale revendiqué.

## Native V4 terminal PASS et host V5 prêt à construire

Le handle17943 termine code0 après129m35s de Cargo. Ses gardes vérifient
candidate_inputs_unchanged=true et préparation5e4d1863. Le binaire dédié
native-build-v1/runtime/tidewater.exe mesure134270464octets, SHA-256
97353d2b6e23776dd68773c2758137cc15645957d8fd2ded545423f9a4899eca.
Les avertissements de copie du cache incremental et dead_code restent conservés.
La compilation seule ne prouve pas le rendu.

Le suivi45361 a déclenché le vrai vérificateur de promenade sur ce binaire,
avec WithPebbles. Le runtime annonce65383plants visibles, excluded=0,
density_removed=0 et l'adapter Vulkan AMD Radeon RX7800XT. Le handle reste vivant ;
aucun verdict de promenade n'est encore revendiqué.

La préparation hostV5, handle99034, termine code0 :
HOST_INPUTS_PREPARED_REQUIRES_NATIVE_BUILD, manifeste source
61adeed25ca1ce02308d21687a87dddbdd16c48ab7462378f8b8797fb925f418.
Le constructeur canonique valide les entrées. BuildCombinedScannedStudioHostV5.py
est lancé sous le handle30134. Aucun paquet portable ni source de production
supplémentaire remplacé ; performance et parité restent à vérifier.

## Promenade native V4 : PASS borné et capture inspectée

Le suivi45361 termine code0. native-walk-followup-v2/result.json a pass=true,
failure=null et compilation_restarted=false. native-walk-pebbles-v1/execution.json
vérifie le binaire97353d2b, tous les inputs inchangés, exit_code=0 et pass=true.
Le contrôleur parcourt8.995765686m avec321grounded_ticks. image_sha256 vaut
bf1e6aacb739c89ed4114dc02c35ba8b090202729b7ea80d3dc1af781a531804.
Trois avertissements de readback lors de la fermeture restent dans les logs.

island.png a été réellement inspectée : village, ponton, eau, ciel et végétation
visibles, ainsi que la capsule de diagnostic du contrôleur. Le sable paraît très
clair dans ce cadrage. Il faut encore comparer exposition et matériaux à une
capture de référence avec la même caméra ; ce jugement visuel isolé ne mesure
pas un écart apparié. Le staging des galets ne prouve pas leurs pixels visibles.
Le PASS couvre le déplacement borné et le démarrage/rendu de cette scène,
pas toutes les routes, les ombres proches/lointaines, le gameplay complet,
l'édition Studio ou la parité source. Le paquet portable reste inchangé.

## Suivi host V5 et préparation de la mesure comparable

Le handle30134 reste vivant. Une observation CIM bornée confirme le Python
BuildCombinedScannedStudioHostV5.py PID41964 et sa chaîne Cargo/Tauri, avec
Cargo build PID18400 (CPU3.203125s) et deux rustc enfants4316 et42624.
Le silence temporaire du log n'est pas traité comme un arrêt ; aucun restart.

PrepareStudioV5Profile.py a une syntaxe Python valide. Il attend un résultat
host V5 terminal PASS, vérifie l'exécutable dédié et fige les trois helpers dans
un nouveau dossier avant de produire le runner. Il conserve la fixture V4 de
24placements, les actions UI, les délais, la résolution et les10s de profil CPU/RAF.
Il n'est pas encore exécuté. Le profil V4 original ne conserve pas les empreintes
des helpers : leur identité historique byte-identique n'est pas prouvée, limite
explicitement conservée pour la comparaison. Aucun gain de performance ni
rendu WebGPU de V5 n'est encore revendiqué. Le chantier conserve son objectif
complet, notamment parité visuelle, édition toutes familles et gameplay prévu.

## Capture native complète avec readbacks GPU, puis comparaison Coast exacte

CaptureNativeV4Static.ps1 termine code0 (handle33086). Le résultat
native-static-depth-v1/result.json est PASS pour le binaire97353d2b et les inputs
vérifiés. La capture island.png (7ccc841d) a été inspectée. Trois cascades GPU
R32Float sont récupérées dans island.object-depth.bin,50331648octets,22d62df3 ;
les readbacks terrain et nuages sont également présents. Cela prouve la
récupération de données après travail GPU, pas l'attribution des ombres à chaque
famille ou leurs distances correctes. Un avertissement readback à fermeture reste.

La caméra du bundle initial est[42,14,-90] avec quaternion[0,.995004,.099833,0],
vue diagnostic vers la mer ; elle ne correspond pas aux caméras Coast/Village
retenues dans baseline-003. Aucune métrique source n'est calculée sur cette vue.
PrepareNativeV4CoastCamera.py copie394fichiers et ne change que la caméra de
fixture.json vers la pose exacte baseline-003/coast.json au commit1438b1ab.
La première capture Coast échoue code101, grass.rs193 : les oracle_cells étaient
ceux de l'ancienne caméra. L'assertion reste intacte et l'échec est conservé dans
native-source-coast-depth-v1. Aucune réduction de végétation/effets.

L'exporteur canonique export-grass.mjs refuse ensuite Terrain differs car il
cherche l'ancien fixture.files['heights.bin'], absent du contrat actuel : le
raster est décrit par terrain.json.sampled_source.samples_sha256. Une copie
isolée export-grass-common-terrain-v1.mjs ajoute uniquement ce chemin alternatif
aux deux lectures du pin, sans supprimer la comparaison du terrain calculé au
pin. Le clone source est propre et exactement1438b1ab. L'acquisition V2 termine
code0, cellules[6,1,0]. Le pin hauteur estbe03f5db, identique au terrain commun et
à grass.json ; tous les buffers géométrie, masques et programmes exportés sont
byte-identiques aux fichiers de scène. Le premier export refusé reste conservé.

PrepareNativeV4CoastOracle.py produit une nouvelle fixtureV2, en changeant seulement
la caméra et les données d'oracle grass.camera/oracle_cells, puis le hash grass.json
référencé. Les394inputs parents restent inchangés. Le replay25267 termine code0 ;
native-source-coast-depth-v2/result.json est PASS avec les quatre sorties GPU.
La nouvelle capture3031a5bc a été réellement inspectée face à la sourcebb66a39d.

Compare-PairedCapture.ps1 -Preset Seaward termine code0 à1920x1080, même pose et
FOV62. Erreur absolue moyenne de luminance entière27.5807/255 ; moyenne source
119.467, native145.7497. Régions : ciel9.3726, eau16.283, plage23.4099.
Le temps d'animation/postFX n'est pas apparié : ces différences ne prouvent ni
parité ni la cause de chaque écart. Inspection : rendu plus clair, relief du
sable plus marqué, transitions rectangulaires/pixellisées de nuages visibles,
et l'échoppe de Joe visible à droite dans la référence est absente de ce cadrage
natif (le portique avec poissons reste visible). Il faut diagnostiquer les poses,
la visibilité et les matériaux de cette famille, plutôt que masquer cet écart
par un réglage global. Aucune correction de ces défauts encore revendiquée.

Le hostStudioV5 handle30134 reste vivant et compile ses crates. Le profil V5 et
le cycle d'édition complet attendent toujours son exécutable dédié ; aucun restart,
aucune substitution du portable et aucune qualification globale.

## Correction de la fixture Coast : échoppe et poissons voisins restaurés

L'inspection de fish_stand.rs montre que load/load_content cherchent les dossiers
fish-stand et fish-stand-fish dans scene_root.parent(). La copie cameraV1/V2 ne
reprenait que scene : elle supprimait donc involontairement ces inputs optionnels.
L'absence d'échoppe dans la captureV2 est un défaut de cette fixture de test et ne
prouve pas une régression du jeu. Les chiffresV2 restent conservés avec cette
limite ; ils ne représentent pas une scène de contenu complet.

PrepareNativeV4CoastComplete.py crée un dossierV3 neuf, copie les inputs scene
V2 inchangés et les trois dossiers fish-stand, fish-stand-content, fish-stand-fish
du bundle original.506fichiers sont hashés et vérifiés, sans retoucher V1/V2.
CaptureNativeV4Coast.ps1 deV3 termine code0 (handle66281), result.pass=true,
quatre captures/readbacks GPU récupérés. La vérification après runtime des506pins
passe, fixture-integrity.json conserve preparationSHAf266aa18 et resultSHAc4171ac7.

La captureV3 native133ccd44 est inspectée face à la source Coastbb66a39d :
l'échoppe et ses poissons reviennent à droite, position/FOV de caméra identiques.
Les écarts restants comprennent matériaux/détails de l'échoppe, eau/luminosité,
relief du sable et rectangles/pixellisation du ciel. Le comparateur Seaward
termine code0 : erreur entière24.3681/255, moyenne source119.467 et native142.7632 ;
ciel18.6653, eau23.5753, plage30.2732. Les régions changent avec l'autoexposition et
le contenu ; l'écart V2→V3 ne constitue pas un gain moteur ou une calibration.
Les horloges et le traitement temporel ne sont toujours pas appariés, et ce
comparatif ne qualifie ni la forêt ni toutes les ombres de l'île.

Le premier export Terrain differs n'avait pas démontré un terrain incompatible :
le pin hérité heights.bin était absent. L'exporteur isolé vérifie désormais le pin
be03f5db du terrain commun ; le code source figé le reproduit exactement et les
buffers/masques/programmes d'herbe restent byte-identiques. Aucune assertion
native, qualité, densité ou effet n'a été désactivé. Prochaine action de rendu :
diagnostiquer les rectangles des nuages et les matériaux avec cette fixture
complète, sans promouvoir le comparatif réduitV2. StudioV5 reste en construction
sous le handle30134 ; aucun profil V5 exécuté ni portable remplacé.

## Host Studio V5 terminal PASS et premier profil exécuté

Le handle30134 termine code0, BUILD_ONLY. Binaire dédiéa47012e9,
WASM embarqué7c5bc472, source-manifest61adeed2, frontend-manifest04546011.
Les gardes de sources originales, frontend et sorties Tauri générées passent.
PrepareStudioV5Profile.py vérifie ce reçu puis fige les trois helpers ;
ProfileStudioV5.ps1 termine code0 (72055), failure=null, executable_unchanged=true.
La source utilisée reste la fixture stand V4 de24placements. Les deux états UI
ont exactement les mêmes1128placement_options et building_options, même mode
édition sans simulation joueur, visibilitévisible et canvas1314x565.

RAF sous profilage : V49.292638, V513.649717, soit+46.887% en moyenne. Le p95
empire toutefois76.4→83.4ms : cette première observation ne qualifie pas la
fluidité, les pauses ni60FPS. Le premier delta RAF V5 est négatif (horloge du
premier callback) ; il est conservé dans les données brutes. Les timings ne sont
pas des timestamps GPU, un benchmark release ou une qualification forêt.
Les empreintes historiques des helpers du profil V4 n'étaient pas conservées.

L'analyse CPU V5 passe :6767échantillons,10283.005ms ; Sha1State::process
332.575ms(3.2342%), contre3713.453ms(29.3896%) dans le profil V4 plus long.
Le temps idle vaut48.2657% ; il ne signifie pas à lui seul un GPU saturé.
L'analyse inclusive V2 déduplique les fonctions dans chaque pile et préserve
les temps exclusifs. Les autres coûts observés incluent SHA256, création de
buffers, allocations et calculate_bounds. Aucun cache supplémentaire ajouté
sans attribution des appels et mesure correspondante.

## Cycle commun V5 lancé avec journal conservé

PrepareCommonLifecycleV5.py termine code0 (8908),8014fichiers de frontend et
copie du binaire V5 avec WASM attendu vérifiés, fixture acceptée V4 et helpers
common-journal-evidence-v4 figés. La preuve d'acceptation stand appartient au
binaire V4 explicitement identifié ; elle n'est pas attribuée au V5.
RunLifecycle.ps1 lance les six familles source et l'échoppe manuelle, avec mêmes
actions/assertions et délai180s ; journal capturé avant le nettoyage existant.
Son verdict complet reste à attendre. Aucune réduction de cas ni GPU picking
plantes revendiqué. Aucun portable remplacé.

## Limite supplémentaire du comparatif Coast : nuages calculés pour l'ancienne pose

L'inspection des données révèle que les captures CoastV2/V3 utilisent encore
clouds/environment.json caméra[42,14,-90], quaternion[0,.995004,.099833,0], et
atmosphere.json camera_y14, alors que la scène vise Coast[53.6,4.447714,-77].
cloudsSampleView combine l'histoire reconstruite dans ce frustum acquis avec
le panorama hors frustum. Les rectangles observés ont donc une piste de données
caméra incohérentes ; ils ne démontrent pas encore un bug de sampler/moteur.
Les samplers observés sont déjà linéaires. Les captures/métriques V3 restent
conservées mais ne sont pas une comparaison entièrement appariée des nuages.

L'exporteur original contrôlé export-environment.mjs --clouds acquiert depuis
le clone propre1438b1ab l'environnement Coast exact à120étapes (694fichiers),
code0, native-v4-coast-environment-source-v1. Métadonnée référence4b4a7bdb,
pose Coast et heure16.2 vérifiées ; acquisition cold_state_not_app_precompile_replay.
Le replay natif avec cette nouvelle acquisition n'est pas encore exécuté.
Il faut raccorder ensemble les uniforms d'atmosphère, l'histoire des nuages et
ses paramètres publics avant de juger les artefacts. Aucun shader/effet désactivé,
aucune correction de production spéculative.

Le cycle communV5 est suivi sous le handle1483, confirmé vivant après dispatch.
Aucun autre probe GPU n'est exécuté pendant ce cycle. La capture réelle du profil
StudioV5 profile-scene.png a été inspectée : workspace Tidewater, rendu île,
bâtiments, végétation et catalogue visibles. Les blocs de texte de catalogue
se chevauchent et certains messages d'état restent historiques ; cette capture
ne qualifie pas la finition UI. Des artefacts du ciel restent visibles, à
investiguer avec les données de caméra cohérentes avant toute conclusion de
cause commune avec le comparatif natif statique.

## Fixture Coast V4 : préconditions caméra nuages/atmosphère raccordées

PrepareNativeV4CoastClouds.py termine code0. Le dossier neuf
native-v4-source-coast-fixture-v4 conserve les506inputs complets V3, avec seuls
params.bin, cloud-view-params.bin, atmosphere.json et fixture.json actualisés.
Les autres buffers, géométries, masques, shaders et dossiers d'échoppe restent
byte-identiques. L'acquisition Coast694fichiers est copiée dans clouds avec
vérification des pins source ;1201inputs sont inventoriés dans la préparation.
La pose/quaternion/FOV sont contrôlés contre environnement-source-v1.

params.bin provient du cloud-step120-atmosphere.bin source (64octets), et les
80octets publics de cloud-step120-public.bin. atmosphere.json camera_y et son
pin sont actualisés ; le loader natif garde sa vérification par paramètres
calculés. Le runner change aussi l'argument clouds vers cette acquisition,
sans source moteur retouchée. Ce stade n'a pas encore exécuté le renderer.
L'environnement cube et les constantes SH restent ceux de l'acquisition
précédente : limite conservée, pas de comparaison globale qualifiée revendiquée.
Le replay GPU attend la fin du cycle Studio1483 pour éviter une charge concurrente.

## Observation en lecture seule du cycle Studio V5

Le handle1483 reste vivant. CIM confirme le processus42120, chemin exact du
candidat V5, CPU19.609375s et démarrage11:14:50.540685UTC. Le diagnostic
ObserveCommonLifecycleV5.ps1 lit uniquement le WebView enfant identifié, sans
clic, focus, nouvelle fenêtre ou commande produit. Code0, reçu
common-lifecycle-v5-observation-20261006-v1.json. À11:18:30.641UTC : visibilité
visible,354entrées, un open_managed,18preview_zone et18apply_zone ;
persistence_error=null, état Retouches non sauvegardées. L'aller-retour IndexedDB
passe8388608caractères/6291456octets aléatoires, SHA86bae5a7.
Ce résultat prouve la progression et l'absence d'erreur observée à cet instant,
pas la fin des sept cas, les suppressions ou la réouverture finale.
Aucun verdict du cycleV4 échoué n'est réécrit ; objectif complet actif.

## Fixture Coast V5 : éclairage complet acquis à la bonne caméra

PrepareNativeV4CoastLighting.py termine code0. Le dossier neuf
native-v4-source-coast-fixture-v5 conserve les1201inputs de V4 vérifiés.
Il remplace le cube environnement (1048320octets), les neuf coefficients SH
dans les trois shaders terrain/plantes/scene-lighting, village-lighting.json
consommé par les bâtiments/échoppe, source-environment.json et les pins de
fixture.json. La direction et la couleur du soleil sont vérifiées identiques
aux précédentes ; seuls les paramètres d'environnement acquis sont raccordés.
Les shaders ne changent que leur constante envSH, avec reconstruction inverse
exacte vérifiée. Aucune logique de rendu ni géométrie n'est modifiée.

Le cube est assemblé dans l'ordre face puis mip utilisé par l'outil de
préparation original, six faces et six niveaux128→4, données RGBA16Float
finies et tailles contrôlées. Les SH viennent des36float32 source, neuf
triplets RGB à stride4 ; ils sont contrôlés contre les anciennes constantes
avant remplacement. Écart maximal des coefficients0.0029153823852539062,
cube effectivement différent. Les autres inputs restent byte-identiques.
Une seconde lecture indépendante confirme les1201pins de sortie. Les preuves
V1–V4 sont préservées. Le runner V5 vise le même binaire natif97353d2b,
les mêmes familles de contenu, et l'acquisition Coast propre1438b1ab.

Ce stade reste PREPARED, sans exécution GPU : le cycle Studio1483 reste vivant.
La limite source cold120steps contre précompilation App est conservée. Aucun
écart visuel mesuré ou correction de production n'est déduit de cette préparation.

## Cycle commun V5 : sauvegarde et première réouverture observées

Nouvelle observation en lecture seule, ObserveCommonLifecycleV5V2.ps1,
code0, reçu common-lifecycle-v5-observation-20261006-v2.json.
Même processus42120, candidat a47012e9 et port enfant55554 identifiés.
À11:26:20.546UTC :601entrées retenues, deux open_managed,43preview_zone,
42apply_zone, un save, un close, un inspect et un list_zones. Visibilité
visible, persistence_error=null, état Session synchronisée. Le roundtrip
IndexedDB passe de nouveau6291456octets, SHA772c4dc6. Ces compteurs constituent
une observation de progression, pas une preuve de réussite des sept cas.
Le handle1483 est re-pollé vivant après cette observation ; aucune relance,
augmentation de délai ni autre exécution GPU concurrente.

## Lisibilité du catalogue : débordement reproduit et CSS corrigée

La capture réelle common-edited.png du cycle V5 est inspectée : rendu île,
objets, eau et inspecteur présents ; les libellés longs du catalogue dépassent
visiblement les cartes. VerifyCatalogCardLayout.mjs reproduit le défaut avec
le CSS original dans un navigateur Edge isolé et la même structure de cartes.
Premier lancement échoue avant création de sortie (import ESM Windows sans
file://) ; chemin corrigé, exécution suivante terminale code0 (13905).

Résultat catalog-card-layout-20261006-v1/result.json PASS : cartes138px,
deux libellés originaux338.859375/344.96875px débordants, puis124px contenus.
Le libellé court et les titres complets sont préservés. after.png réellement
inspectée : textes sur deux lignes tronqués dans leur carte, catégorie lisible.
Ce contrôle CSS isolé ne qualifie pas le Studio intégré après reconstruction.

Adoption limitée à cinq lignes ajoutées dans authoring_gold.css, source
préalablement propre et SHA2d5d627b contrôlé. Original archivé avec le reçu
production-adoption.json ; trois règles limitent les cartes et libellés,
sans changer identités, gestes, outils ni persistance. diff --check ciblé PASS.
Le candidat V5 actuellement exécuté reste inchangé et n'embarque pas ce CSS.

## Cycle V5 toujours en progression et protection du prochain rendu

Observation V3 en lecture seule à11:31:39.975UTC, code0, même host42120,
823entrées retenues,51preview_zone et57apply_zone, persistence_error=null.
Save/Close/Open reste observé une fois, état Retouches non sauvegardées.
Le handle1483 est confirmé vivant ensuite ; aucun verdict terminal attribué.

RunCoastLightingV5.ps1 prépare le prochain rendu natif complet : refus si le
host du cycle est encore vivant, contrôle des1201pins avant/après exécution,
pins du manifeste et runner et reçu d'intégrité séparé du résultat GPU.
Analyse syntaxique PowerShell PASS. Ce wrapper n'a pas exécuté le rendu.

## Cycle commun Studio V5 terminal PASS

Le handle1483 termine code0 avec COMMON_PLACEABLE_STUDIO_PASS. Le ledger
runtime-lifecycle/common-placeable-studio-ledger.json indique status=passed,
failure=null, sept cas et sept suppressions doubles. Source/frontend conservés,
candidat a47012e9, WASM7c5bc472, listener CDP fermé. Caisses, pièges, noix de
coco, tronc, branche, coquillage et échoppe manuelle couvrent TRS, duplication,
contrôles de verrouillage (septtrue), Save/Close/Open, suppression originale et
copie, Undo/Redo (septtrue) et réouverture finale sans résurrection.
Checkpoint retouché4240aed7, checkpoint Removed b415fdf0. Le délai original
180s et les assertions restent inchangés. Le FAIL V4 précédent reste conservé :
ce PASS V5 ne prouve pas à lui seul la cause exacte du timeout antérieur.

Journal avant nettoyage conservé :996entrées. Décodage gzip/base64 effectué,
taille et SHA vérifiés contre le reçu ;992séquences de timings valides,
quatre entrées encore pending conservées comme telles. Compteurs :57preview,
70apply,2save,2close,3open_managed. Délais p95 avant transport2683.6ms,
transport580.7ms, après transport412.8ms. Ces étapes incluent hachage,
sérialisation, stockage et ordonnancement ; ce ne sont pas des temps GPU ni
une attribution isolée au stockage. Les durées relatives au performance.now
courant ne sont pas interprétables pour des entrées restaurées après navigation.

common-edited.png et common-removed-reopened.png réellement inspectées.
La capture finale montre les éléments supprimés absents et le catalogue présent,
mais bâtiments/végétation ne sont pas tous dessinés à cet instant de réouverture.
Le PASS porte donc sur le parcours et la persistance vérifiés, sans qualification
du chargement graphique final complet. Plantes par GPU picking NOT RUN,
playtest NOT RUN, revue propriétaire et gate toujours non qualifiés.

## Coast V5 : rendu natif et captures GPU réellement exécutés

RunCoastLightingV5.ps1 (92575) termine code0 après clôture du cycle Studio.
Résultat natif PASS, binaire97353d2b inchangé,1201pins avant/après inchangés,
preparationSHA8cdccc20 et runner96e7dcd5. Caméra Coast exacte et contenu
complet (y compris échoppe et poissons) conservés. image751f414d,
object-depth318b5cbb (50331648octets), shadowa8077ba8 (2097152octets),
cloud-shadow08f62a9c (262144octets). Une alerte closed channel à la fermeture
reste consignée ; les quatre artefacts attendus sont produits et contrôlés.

Image native et source Coast bb66a39d réellement inspectées : les rectangles
de ciel précédemment observés ne sont plus visibles sur cette capture cohérente.
Cette observation ne qualifie pas le ciel dynamique ni les autres caméras.
Compare-PairedCapture.ps1 Seaward (42757) termine code0 : erreur moyenne
luma24.1101/255, source119.467, native142.5591 ; ciel18.1914,
eau23.1306, plage29.9786. Le rendu reste trop clair ; sable, matériaux de
l'échoppe et état animé diffèrent. Parité=false, ombres proches/lointaines et
carte complète non qualifiées. Les différences cold history/exposition/temps
restent à raccorder ; aucun gain moteur causal déduit des seuls écarts V3→V5.

## Comparatif Coast : réglage utilisateur non épinglé découvert et isolé

Le loader graphics.rs du snapshot natif V4 lit
LOCALAPPDATA/OpenRPG Forge/Tidewater/graphics.json, avec valeurs par défaut
pour les champs absents. Le fichier utilisateur réellement lu contient
exposure_ev=1.0 et sharpen=.45, SHA d5a14e95 ; il n'était pas isolé dans les
comparatifs précédents. Leur résultat numérique reste conservé, mais le rendu
trop clair ne peut pas être attribué au seul moteur ni à l'acquisition lumineuse.

PrepareCoastExposureSweep.py prépare trois profils isolés avec les20champs
exactement définis dans le graphics.rs compilé, paramètres effectifs utilisateur
plus defaults du snapshot et seul exposure_ev variable. L'assertion initiale
erronée sur21champs échoue avant création de sortie ; le contrôle corrigé
compare aussi l'ensemble exact des champs du struct. Un lancement dépendant
sans manifeste avait échoué avant rendu ; ces sorties ne sont pas des probes GPU.

RunCoastExposureSweepV1.ps1 (48230) termine ensuite code0, PASS trois captures
avec le même binaire97353d2b et la fixture CoastV5 complète. LOCALAPPDATA est
redirigé uniquement dans le processus de contrôle vers trois dossiers jetables,
puis restauré ; aucun réglage utilisateur modifié. Chaque profil, runner,
source graphics, fichier utilisateur et les1201pins de fixture sont contrôlés.
Tous les effets restent identiques entre captures. Profils explicites :
6d2e03ea (+1),598edd03 (+.4),77615003 (+.1).

Compare-PairedCapture.ps1 Seaward termine code0 pour chaque capture :

| Exposition native | Luma moyenne | Erreur absolue entière | Ciel | Eau | Plage |
|---|---:|---:|---:|---:|---:|
| +1.0 EV | 142.6530 | 24.1973 | 18.3017 | 23.2206 | 30.0644 |
| +0.4 EV | 118.1563 | 12.4690 | 8.7985 | 5.8068 | 19.4032 |
| +0.1 EV | 105.4145 | 22.2585 | 21.5081 | 16.9627 | 28.2157 |

Référence entière119.467. Le contrôle +1 retrouve les métriques V5 antérieures
à environ.1niveau, sans identité temporelle revendiquée. +.4 est le meilleur
des trois réglages sur cette vue, et correspond déjà au default natif ; pas
de modification de profil produit ni utilisateur. Il n'invalide pas la
calibration historique Studio Village+.1 (autre chaîne/vue/candidat).
Capture+.4 a59e595a réellement inspectée : luminosité rapprochée, mais sable
et matériaux d'échoppe toujours distincts. Aucun verdict de parité, stabilité
globale, nuit, dynamique ou qualification de gate n'en découle.

## Échoppe : contrat complet du matériau source acquis en CPU

L'inspection confirme que le shader compact actuel tidewater_stall.wgsl reste
un aperçu : illumination diffuse simplifiée, aucune ombre activée sur ce
matériau, absence de plusieurs réponses procédurales. Les poissons du probe
sont encore StandardMaterial avec couleurs par espèce ; ils ne dessinent pas
encore le matériau complet acquis du poisson. L'exposition seule ne peut pas
réparer ces écarts ni l'ombre manquante de l'échoppe sur la plage.

AcquireStallMaterialContractV1.mjs (94260) termine code0, CPU_CONTRACT_ACQUIRED :
stall-material-source-contract-20261006-v1,15fichiers,deux modules, surface
originale7369octets. La vraie factory createStallMaterial de l'upstream propre
1438b1ab est exécutée sans modification ; attributs, varyings, defines dont
CLEARCOAT=1, uniformes, bindings, layers et atlas enregistrés. Les sept textures
réelles du paquet courant sont contrôlées en taille/SHA, dimensions et sRGB.
Licence et provenance source sont copiées avec hashes. Les objets texture
passés à la factory sont uniquement des noms CPU ; ils ne servent pas de
substituts au dessin. Source propre avant/après, aucune exécution GPU par
cette acquisition. Adaptation du matériau complet, éclairage/clearcoat/ombres,
pipeline et rendu réel natif/Studio restent à faire ; aucune source de
production de matériau n'est modifiée à cette étape.

Suite : [calcul de surface complet de l'échoppe](2026-10-06-stall-surface-parity.md).
Bibliothèque complète préparée, corps source préservé par reconstruction inverse.
Le premier contrôle GPU échoue sur les paramètres d'intensité de la factory ;
V2 les fournit explicitement et termine PASS :19couches,28champs et deux états
jour/nuit, lectures GPU source/adaptation identiques bit à bit. Vérification
indépendante des quatre lectures544768octets et SHA PASS. Cette preuve couvre
le calcul de surface isolé avec arrays réels, pas encore le matériau de scène
avec éclairage, ombres, mips, ni son installation Runtime/Studio.
