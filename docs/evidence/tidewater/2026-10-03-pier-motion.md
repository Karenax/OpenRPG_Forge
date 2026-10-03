# Mouvement source des lanternes et du panneau

La capture `F:/OpenRPG_Forge/.build/pier-motion-source-20261003-v1`
provient du `Village.update` original au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le descripteur `pier-motion.json` porte le SHA-256
`e02acf64cbd1ddb2cacd9cd2ca10d2d71856181550ebe8da9afd374abf21097f`.
Le manifest conserve les sources, leurs empreintes et la licence MIT.

Les onze états initiaux aléatoires sont acquis sans réensemencement. Ils
ne proviennent pas des phases de scintillement lumineux existantes. La
trace couvre 240 étapes, quatre vents et des deltas de 0 à 0,25 s ; le
code original borne chaque delta à 0,1 s et emploie ses propres sous-pas.
Elle contient les états du pendule, les quaternions et les centres lumineux.
Une nouvelle acquisition ne reproduirait pas les phases aléatoires initiales.

`crates/forge_engine/src/tidewater_pier_motion.rs` porte les équations en
double précision et conserve l'ancien état lorsque les entrées sont invalides.
Le module n'est pas encore déclaré dans le moteur et ne s'exécute pas dans Studio.

Le harnais isolé `F:/OpenRPG_Forge/.build/pier-motion-validation-20261003-v1`
vérifie la fermeture du paquet avant compilation. Il compare chaque étape,
les rotations et les centres avec un seuil absolu de 1e-12, contrôle les copies
et le refus des entrées invalides sans mutation. La compilation est lancée ;
aucun verdict numérique n'est acquis à la rédaction initiale de ce rapport.
Le reçu terminal `result.json` est requis pour déclarer les tests réussis.

Le consommateur de lumières Studio dispose désormais d'un Cargo check WASM
réussi à 07:23:07 UTC, avec 1 344 sources inchangées. Sa construction suivante
est refusée avant Cargo par la garde de concurrence : le rustc du harnais
de mouvement référence le même cache. Ce refus est conservé dans
`studio-pier-light-integration-20261003-v1/continuation-result.json` et
`frontend-result.json`. La reprise préparée `Recover-AfterTargetBusy.py`
exige les mêmes sources et garde de concurrence, réutilise le script identique
et écrit un reçu frais `frontend-result-v2.json` ; elle n'est pas encore lancée.

Ces résultats ne prouvent ni l'animation automatique dans le produit, ni le
dessin des lampes, ni la parité visuelle complète.

## Paquet initial destiné au runtime

`Prepare-RuntimePackageV2.py` produit le paquet frais
`F:/OpenRPG_Forge/.build/pier-motion-runtime-package-20261003-v1`.
Son manifest porte le SHA-256
`2a93e08b6ae31548d76643214b1fb12de091d2c369e3f999554d2c1d2ad7ca54`.
Le script ferme les deux acquisitions par leurs inventaires et empreintes,
vérifie l'identité du quai et ses transformations, puis joint chacun des onze
ordinals et pivots/centres de repos avec une tolérance de 1e-12.
Les phases initiales sont copiées sans nouvelle génération aléatoire. Le pivot
local du panneau et la licence originale sont conservés avec les empreintes
du paquet géométrique et de la trace d'acquisition.

Le premier script est conservé : il refuse avant écriture parce que le littéral
d'empreinte du harnais contenait un caractère supplémentaire. V2 corrige ce
littéral à la valeur du lecteur Rust et du SHA-256 réellement mesuré ; aucun
seuil ni donnée source n'est modifié. V2 termine avec retour 0 et onze jointures.
Le paquet est dérivé uniquement : lecteur Rust, animation ECS, chargement Studio
et qualification visible restent à réaliser. Le test numérique indépendant
continue dans sa même session et ne dispose pas encore d'un reçu terminal.

## Comparaison numérique V3 réussie

V1 et V2 échouent sur le signe de la composante Z du quaternion du panneau.
L'inspection de sa construction originale montre `rotation.order = 'YXZ'`
dans Village.js, contrairement aux lanternes XYZ. V2 avait seulement explicité
la formule XYZ ; V3 applique YXZ au panneau. Les deux versions précédentes et
leurs diagnostics sont conservés, sans changement de trace ni de seuil.

`result-v3.json` clôture deux tests réussis, zéro échec et zéro test ignoré.
Les 240 étapes des onze lanternes et du panneau donnent 36 480 comparaisons,
avec erreur absolue maximale 7,105427357601002e-15, sous le seuil 1e-12.
Les copies conservent les mêmes bits et les entrées invalides sont refusées
sans modifier les états précédents. Cette preuve couvre les états, rotations
et centres source en double précision ; elle ne qualifie pas l'intégration ECS
ni le rendu Studio. Le lecteur du paquet est écrit dans un nouveau fichier
non enregistré, et ses tests synchrones/asynchrones et de corruption sont lancés.

Le lecteur termine ensuite avec trois tests réussis, zéro échec, zéro test
ignoré, 0,21 s. `reader-result.json` conserve ses entrées et empreintes avant/après.
Le chargement synchrone et asynchrone conserve les phases initiales ; manifest
altéré, descripteur/licence altérés ou absents sont refusés. Le manifest altéré
est refusé avant tout appel au lecteur de fichiers. Le nouveau module assets
reste non enregistré pour préserver les compilations globales actives.
La reprise frontend `Recover-AfterTargetBusy.py` est lancée après terminaison
des deux harnais ; les sorties précédentes restent conservées.

## Adaptateur des pivots auteur, en préparation

Le nouveau fichier moteur `tidewater_pier_animation.rs` résout les vrais
`PierLanternPivot` et `PierSwingPivot` sous une racine `ManualPlacementProjection`
du `ResidentPier`. La révision de l'asset, la transformation auteur et les pivots
locaux sont vérifiés. Chaque copie possède ses propres états acquis ; les
transforms des documents et la géométrie immuable restent indépendants de
l'animation. L'avance calcule tous les états avant le premier changement de
rotation ; une erreur de vent, de delta ou de hiérarchie conserve l'instance.

Le harnais `ecs-harness.rs` reprend la vraie géométrie et les émetteurs du
harnais de quai V4, avec deux placements et les 240 étapes originales. Il
contrôle les rotations des 24 pivots, original et copie, avec seuil 2e-7 pour
la projection f64 vers les transforms Bevy f32, puis conserve les contrôles
des 22 émetteurs, du remplacement et du retrait. `Run-ECS.py` est préparé,
mais n'est pas encore exécuté pendant la construction WASM active. Aucun
succès ECS de cette animation n'est revendiqué ; les modules restent non enregistrés.

La continuation lance le harnais ECS dans une sortie distincte. Le wrapper
assets exporte les types de la bibliothèque native existante et ajoute seulement
le nouveau lecteur ; aucun cache de dépendances natif n'est réécrit. Le Cargo
Studio vivant écrit ses artefacts dans `target/wasm32-unknown-unknown/release`,
tandis que le harnais lit les bibliothèques de `target/debug/deps`. Les sources
et bibliothèques du harnais restent épinglées avant/après. Session ECS 61276,
rustc 9288 ; session frontend 15995, Cargo 38268 / rustc 35056 confirmé sur
forge_project WASM. Aucun reçu terminal ECS n'est encore acquis.

## Écart de vent du consommateur courant

Inspection des sources actives : `imported_scene.rs` fournit
`Vec4::new(1.0, 0.0, 6.0, 2.0)` aux matériaux du village. Les consommateurs
plantes et terrain utilisent aussi cette valeur. Le `Frame.js` original épinglé
déclare `windSpeed = 7` et `windDir = normalize(0.35, 0.94)`.
La trace numérique réussie couvre le vent original mais ne prouve donc pas
l'égalité du vent actuellement fourni par Studio. Le raccord produit doit
utiliser un même état de vent pour matériaux et pendules et partir des valeurs
acquises ; ajouter un vent indépendant aux lanternes conserverait cet écart.
Les trois fichiers consommateurs ne sont pas modifiés pendant leurs validations
actives. Cet écart de fidélité et la vérification visible restent ouverts.

Inspection de `village-probe-header.wgsl` : le paquet source lit
`frame.time=settings.wind.w`, `frame.windDir=settings.wind.xy` et
`frame.windSpeed=settings.wind.z`. La valeur actuelle fixe donc aussi le temps
matériau à 2 s. Dans App.js original, `G.time.value += dt`.
Le nouveau module non enregistré `tidewater_wind.rs` fournit un `WindFrame`
commun : vent original à 7 m/s, direction originale normalisée, temps initial
explicite fourni par l'acquisition, avance par delta et packing GPU commun.
Les changements invalides ou non représentables en f32 sont refusés avant
mutation. Les changements de vent conservent le temps et les états de pendule.

`wind-harness.rs` et `Run-Wind.py` sont préparés pour confronter les quatre
vents et 240 deltas de la capture au packing matériau et au temps accumulé,
ainsi que les refus sans mutation. Aucun succès de ces tests ni raccord aux
trois consommateurs Studio n'est encore revendiqué. L'intégration conserve
la distinction entre le temps global et le clamp de 0,1 s propre aux pendules.

Le test WindFrame est ensuite lancé en session 84238, rustc 47740 confirmé
vivant. Les sessions ECS 61276 et frontend 15995 restent actives ; aucune
construction n'est relancée sur un simple délai d'observation. Les tests
WindFrame écrivent seulement leur sortie isolée et lisent les bibliothèques
natives épinglées. Leur reçu terminal reste en attente.

## Consommateur commun des matériaux, non enregistré

Le nouveau fichier Studio `authoring_assets/imported_scene/wind.rs` prépare
le raccord : état WindFrame initialisé au temps global 0 de Frame.js,
avance en Update et upload après les projections des matériaux en PostUpdate.
Il renouvelle les uniforms des matériaux village, plantes, terrain et CDLOD
déjà résidents ainsi que ceux créés dans la frame, sans reconstruire les meshes
ou textures. La publication d'un nouveau paquet ne crée pas un second temps.
Le delta utilisé reste celui de Time ; l'adaptateur des pendules consommera le
même WindFrame avec sa règle d'intégration source distincte.

Le fichier est formaté mais non déclaré dans le module Studio actif : il n'a
pas encore été compilé ni exécuté. Aucun changement des sources du candidat
WASM actif n'est effectué. Le diagnostic de lenteur confirme le linker natif
45308 enfant du rustc ECS 9288 ; celui-ci n'est donc pas déclaré arrêté.
Le rustc WASM 39024 compile forge_engine et la compilation WindFrame 47740
reste présente. Les journaux ne fournissent pas encore de verdict terminal.

La compilation WindFrame initiale termine avec retour 1 : la derive Resource
panique parce que `CARGO_MANIFEST_DIR` manque au lancement direct rustc.
Ce refus du harnais précède les tests, sans défaut numérique établi.
`Run-WindV2.py` conserve les sorties initiales et fournit cette variable seulement
à ses sous-processus, avec le manifeste réel de forge_engine. Le module et
les seuils restent identiques. Session V2 67038 ; verdict en attente.

## Préparation du chargement Studio

`studio-pier-motion-integration-20261003-v1/Prepare-Overlays.py` termine avec
retour 0 : neuf overlays frais, formatés et épinglés dans
`overlay-preparation.json`. Les quatre fichiers de base utilisés sont vérifiés
contre l'inventaire du snapshot pier-light ; les cinq nouveaux modules du dépôt
sont vérifiés avant/après. Aucun fichier compilé activement n'est modifié.

L'overlay imported_buildings ajoute le champ optionnel `pier_motion_sha256`,
charge son manifest par le transport existant, vérifie l'empreinte candidate,
exige la géométrie du quai et appelle le lecteur asynchrone avec les contrôles
de ticket de chargement. La publication du paquet est dans la même phase que
la géométrie, après les vérifications et le refus d'une zone changée.
Sans référence de mouvement, le slot est remis à None lors de publication.
Les exports assets/engine sont préparés uniquement dans les overlays.

L'overlay imported_scene installe le consommateur WindFrame et inclut sa
disponibilité dans la vérification de scène. L'adaptateur d'animation est exporté
mais son ordonnancement produit et la conservation d'état pendant une retouche
du quai restent à raccorder. Le site de bibliothèque doit encore inclure le
paquet de mouvement et sa référence candidate. Ces overlays n'ont pas encore
été compilés ni exécutés ; ils ne modifient pas le frontend pier-light en cours.

## Résultats intermédiaires et corrections de référence

Le premier harnais ECS termine avec retour 101. Sa boucle des 240 étapes
et 24 pivots atteint son dernier contrôle sans échec ; ensuite, le contrôle
hérité des centres lumineux compare les lampes animées à l'acquisition au repos
et échoue. `ecs-harness-v2.rs` conserve le seuil de 5e-5 m, les contrôles
d'identité et les phases lumineuses, mais utilise les centres `live_world_m`
de la dernière étape originale. V2 est lancée en session 26582 ; aucun PASS
complet ECS n'est acquis avant son reçu terminal.

Le test WindFrame V2 termine avec retour 101 : les refus invalides passent,
mais sa comparaison bit à bit utilise le premier vent de trace, normalisé par
Math.hypot dans l'extracteur. L'original Frame.js normalise via Vector2.length
(sqrt de x*x+y*y), puis multiplie par le réciproque. Une évaluation directe du
Vector2 original donne [0.34893735911823864, 0.9371460502032696] ; commit et arbre
source propre sont revérifiés. `source-wind-default.json` conserve les valeurs
et hashes de Frame.js/Vector2.js. Le module WindFrame reproduit désormais cet
ordre d'opérations ; le harnais V3 compare ses defaults à ces valeurs et garde
les 240 variations de vent/delta et les refus sans mutation. Session 37017,
verdict en attente. Les overlays préparés précédemment doivent être actualisés
avec cette version du module avant constitution du prochain snapshot.

`Prepare-Library.py` est lancé en session 95419 dans le dossier de motion
Studio. Il vérifie la fermeture et les empreintes de la bibliothèque portable
existante, conserve chaque paquet, ajoute le paquet initial de mouvement et
sa référence `pier_motion_sha256` dans un candidat frais. La géométrie référencée
doit avoir l'empreinte attendue par les états acquis. Le reçu final du site
est requis ; aucune publication ni activation dans Studio n'est revendiquée.

## Verdicts reçus et conservation lors des retouches

ECS V2 termine avec retour 0 : un test réussi, zéro échec, zéro test ignoré,
0,74 s. Les deux instances passent les 240 étapes / 24 pivots. Les 22 centres
lumineux animés présentent une erreur maximale de 0,00001546494992997757 m,
sous le seuil inchangé de 0,00005 m. Les phases, copies, refus de remplacement
et retraits passent dans leur portée ECS isolée. `ecs-result-v2.json` conserve
les entrées et empreintes avant/après ; aucun GPU ou Studio exécuté.

WindFrame V3 termine également avec retour 0 : deux tests réussis, zéro échec,
zéro test ignoré, 0,06 s. `wind-result-v3.json` ferme les inputs. Les defaults
source passent bit à bit ; les quatre vents et 240 deltas passent le packing
GPU/temps commun, ainsi que les refus invalides sans mutation.

La bibliothèque termine avec retour 0 : 1 750 fichiers vérifiés, référence
candidate `901e6e3ff3f73840095e186c500ba7f548d166181374d8cd17b426d63236502d`
et paquet de mouvement `2a93e08b6ae31548d76643214b1fb12de091d2c369e3f999554d2c1d2ad7ca54`.
Le site est préparé uniquement, non déployé et non exécuté dans Studio.

`Prepare-RebindOverlay.py` prépare un adaptateur qui résout une nouvelle
hiérarchie avant de conserver les états et réappliquer leurs rotations.
L'identité de placement doit être inchangée. L'overlay est distinct du module
root testé par V2. V3 ajoute un test de reconstruction réelle après translation
et échelle, préservation bit à bit des 24 rotations et reprise de la trace.
Sa compilation refuse le réemploi d'un PreparedPier déjà consommé par staging.
V4 refait la préparation depuis le même paquet vérifié avant le deuxième staging,
sans changer l'adaptateur ni les seuils ; elle est lancée, verdict en attente.

`Refresh-Overlays.py` clôture neuf overlays V3, en conservant les premières
sorties et en remplaçant seulement le wind testé et l'animation avec rebind
encore à qualifier. `overlay-preparation-v3.json` reste préparatoire : les
exports/chargements sont non compilés et l'ordonnancement produit reste ouvert.

ECS V4 termine ensuite avec retour 0 : deux tests réussis, zéro échec, zéro
test ignoré, 1,42 s. La reconstruction réelle des deux hiérarchies après une
translation et une échelle conserve les 24 tableaux de bits des rotations et
la reprise des deux étapes source suivantes. Le refus d'une identité étrangère
ne change pas les transformations. Les contrôles précédents des 240 étapes et
22 centres lumineux passent également. Le reçu `ecs-result-v4.json` ferme les
inputs de l'overlay rebind ; deux avertissements d'import/variable inutilisés
restent dans le harnais, sans modifier le verdict de portée ECS isolée.

Le consommateur `motion.rs` est écrit dans les overlays : même liste canonique
de placements, avance après l'horloge wind en Update, état propre par EntityId,
rebind après retouche ou recréation de la géométrie, retrait des seuls états
dérivés lors de suppression. Il attend la géométrie synchronisée et conserve
les états pendant une reconstruction. `Finalize-MotionOverlays.py` termine
avec dix fichiers épinglés dans un nouveau dossier overlays-v4. Le chargement,
les exports, le vent commun et l'ordonnancement sont raccordés dans ces sources
préparées, mais ne sont pas encore compilés ensemble ni exécutés dans Studio.

Le Cargo build release WASM du candidat pier-light (sans ces overlays motion)
termine avec succès après 44 min 04 s. Son coordinateur poursuit l'assemblage
et la fermeture du frontend ; aucun lancement natif ni nouveau dessin réel
n'est encore acquis. Les deux candidats gardent leurs portées distinctes.

## Snapshot complet du consommateur et candidat natif

`Prepare-Snapshot.py` termine avec retour 0 : 1 350 sources figées, dix overlays
exacts, inventaire du snapshot parent et empreintes revérifiés avant/après.
Le dépôt actif et le snapshot parent restent inchangés. `Run-Check.ps1` lance
le vrai Cargo check release WASM Studio du consommateur complet (session 32510).
Le verdict et la garde finale d'inventaire restent requis avant tout build.

Le frontend pier-light précédent assemble et ajoute le worker, puis son
coordinateur refuse la fermeture parce que le reçu worker ne porte pas
`validation_profile_optimized`. Ce champ appartient à son reçu parent.
L'essai Finalize V3 refuse ensuite un nom de target incorrect dans le harnais
(`forge_studio` au lieu du vrai `forge_studio_frontend`), sans reconstruction.
Finalize V4 termine à 08:36:07 UTC avec retour 0 : 1 344 sources inchangées,
1 758 fichiers finaux vérifiés, profil réel Cargo opt_level 3 et aucune assertion
debug/test, jointure de reçus parent/worker vérifiée. SHA-256 du reçu final :
`d8e114565ff59880430909b5e166e15536d9e31ce9b0e8c381cbb462b31481f6`.
WASM brut : `e44724df344aea1bb07df6ceb2e3d915fd3756683ac79ed1996a318656c8beb3`.
Les refus précédents restent conservés ; aucun WASM ou worker n'est reconstruit.

`Build-NativeV3.py` reprend les gardes natives préparées avec le reçu frontend
V4 correct. Après audit du target cargo-target libre, il est lancé en session
90968 pour attempt.5. Ses entrées natives réelles et le frontend embarqué doivent
rester inchangés avant/après. Le verdict natif et les pixels des lanternes restent
en attente ; ce candidat ne contient pas encore le nouveau mouvement complet.

## Check terminal et réparation isolée V2

Le check complet V1 termine à 08:51:05 UTC avec code Cargo 101 : une erreur
E0433 dans `motion.rs`, au derive Resource. Le module utilise `bevy::prelude`
mais ne fournit pas l'alias `bevy::ecs as bevy_ecs` attendu par la macro avec
les features web. Le module voisin `local_lights.rs` emploie déjà cet alias.
Le reçu Cargo, les sources V1 et les logs sont conservés ; ce check n'est pas
qualifié et aucun build du mouvement ne découle de cet essai.

`Prepare-RepairV2.py` prépare un snapshot V2 neuf en revérifiant les 1 350
sources V1. La seule correction prévue est cet import, sans nouvelle dépendance
ni modification du dépôt actif. Sa préparation est lancée en session 30310 ;
le verdict puis un nouveau check complet restent nécessaires.

Les scripts du pipeline build/assembly sont préparés séparément : fermeture
exacte du paquet motion (manifest, runtime-motion.json et MIT), vérification du
profil optimisé sur le reçu parent, target réel forge_studio_frontend et jointure
parent/worker. Le contrôle des entrées d'assemblage V1 est lancé en session 26131,
sans build ni wasm-bindgen. Aucun verdict visuel ne découle de cette préparation.
Le build natif pier-light en session 90968 reste actif ; rustc PID 34736 compile
le frontend desktop dans le target cargo-target. Il ne contient pas le mouvement.

Le contrôle d'assemblage V1 termine avec code 0 : 1 350 sources, 1 750 fichiers
de bibliothèque et neuf fichiers kit vérifiés. Aucun bindgen/build n'est exécuté.
Le reçu `assembly-inputs.json` fixe cette portée préparatoire.

`Continue-RepairV2.py` est lancé en session 53076. Il attend la fin réelle du
processus de préparation PID 46612, avec contrôle de sa commande, puis exige
son reçu final avant le check V2. Si le check et sa garde de sources passent,
il lance le build release et l'assemblage complet ; chaque échec conserve les
logs et bloque les étapes suivantes. Cette chaîne reste active/en attente,
sans verdict de compilation ou d'exécution acquis au moment de cette note.

La préparation V2 termine avec code 0 : 1 350 sources figées, une seule réparation
(import de l'alias ECS), V1 et dépôt actif préservés. Le coordinateur session 53076
poursuit effectivement le check release WASM : Cargo PID 40620, démarré à
09:00:51 UTC. La suite build/assembly reste conditionnée au check et à la garde.

Le candidat natif pier-light dispose désormais d'un launcher préparé distinct,
`Launch-PierLight-v1.ps1`, et d'un workspace isolé `runtime-fixture-v1` : les 26
fichiers de la fixture editor-water sont revérifiés puis copiés, sans fichier lock,
avec profil WebView neuf. `launcher-preparation.json` conserve les empreintes.
Aucun lancement du candidat pier-light n'est encore exécuté ; le build natif
session 90968 reste requis avant la vérification des pixels.

Inspection du snapshot natif : `spawn_sky` fixe encore `stars_time_night` à
(0, 2, 0, 0). Le collecteur lit bien la composante z du SkyMaterial pour activer
les lampes. En conséquence, un test en plein jour ne qualifie pas leur rendu de
nuit. L'adaptateur moteur `tidewater_sky/solar.rs` existe, mais son raccordement
ordonné à l'atmosphère, au ciel et aux matériaux du Studio reste nécessaire.
Changer uniquement night sans les LUTs/le soleil ne prouverait pas la fidélité.

## Check V2 qualifié et préparation solaire isolée

Le check release WASM V2 termine à 09:06:11 UTC avec retour 0, après 5 min 17 s.
`verified-check-result.json` confirme les 1 350 sources et leur inventaire inchangés
(manifest `f6d1314f5161617f64c3848dbfb502b40237c671982af571c6d8e626c491ef55`).
Le premier build est refusé avant Cargo par la garde `Validation target active` :
le harnais solaire lit encore les dépendances natives du même cache. Les reçus
`build-pipeline-result.json` et `continuation-result.json` conservent ce refus.
Aucun échec Rust Studio supplémentaire n'est impliqué par cet arrêt.

`Recover-BuildAfterReader.py` est lancé en session 8595 : il attend le processus
réel du harnais solaire (PID 49320, commande contrôlée), exige l'absence d'un reçu
Cargo build antérieur, revérifie check/sources/scripts, puis réutilise le build
inchangé avec de nouveaux logs coordinateur. La garde de cache n'est pas modifiée.
La suite assembly/worker/finalization reste conditionnée au succès de Cargo.

En parallèle, un overlay atmosphérique CPU est préparé dans
`solar-atmosphere-input-20261003-v1`, sans toucher au dépôt ni au snapshot compilé.
`Input::with_sun` refuse toute direction non finie/non unitaire et prépare les
12 octets solaires f32, en conservant kernels, hauteur de caméra et autres
paramètres. Le harnais inclut les quatre sources fermées et le vrai Clock solaire :
48 couples heure/azimut, conservation des paramètres/shaders et refus atomiques,
plus les deux tests de Clock existants. La session 24593 compile ce harnais ;
le verdict est en attente. Aucun raccordement Studio ni GPU n'en découle.

Le build natif pier-light termine à 09:09:41 UTC avec retour 0 : 175 sources
natives, 189 fichiers source/métadonnées et 1 767 fichiers frontend embarqués
vérifiés avant/après. Exécutable attempt.5 :
`2fd6b0ed834c90d67f42241f64406fdd8e8aa177d0ecf6c2ffc0dfc8f06bc275`.
`Launch-PierLight-v1.ps1` termine avec retour 0 ; PID 42548, lancement à
09:10:17 UTC, fenêtre Computer Use 4133610. Le workspace isolé est ouvert
via Projets/modules, puis la zone via son module propriétaire. Le catalogue
montre les 33 objets/couches de la fixture. Un clic charge la bibliothèque ;
la scène côtière réelle apparaît avec terrain, ciel, eau, bâtiments et ponton.
Le calcul d'environnement est annoncé terminé. Aucun éclairement nocturne,
comparaison appariée ou mouvement du nouveau consommateur n'est acquis.

Le reçu `ui-observation-loading-v1.json` conserve la première observation de
chargement, les 26 fichiers source/copie inchangés et l'absence de retouches.
Un clic Jouer est ensuite exécuté : session playtest confirmée, sources gelées,
collisions terrain synchronisées, première capture 3D encore en préparation.
Le résultat visuel du playtest et le retour à l'édition restent à vérifier.

Le playtest affiche ensuite les pixels réels du village : terrain, bâtiments,
végétation, passerelle, ciel et ombres projetées. Le bouton Retour à l'édition
rétablit la vue côtière 3D avec ponton, eau et rivage sans recharger la bibliothèque.
`ui-observation-playtest-return-v1.json` revérifie les 26 fichiers source et copie,
conserve les reçus précédents et fixe cette portée. Aucun geste de marche,
retouche/sauvegarde, éclairement de nuit, animation du nouveau consommateur ou
parité appariée n'est revendiqué. Le candidat reste ouvert dans la fixture isolée.

## Lumière solaire depuis le readback source

Le harnais atmosphère CPU termine avec retour 0 : quatre tests réussis, 48
états solaires, sources/dépendances inchangées. La reprise du build mouvement
(session 8595) passe ensuite la garde du cache et démarre le vrai build release
WASM. Ses processus rustc sont observés actifs ; aucun verdict final n'est acquis.

`solar-readback-lighting-20261003-v1/Capture.mjs` exécute uniquement le corps
borné de `App.applyAtmosphereReadback` avec les vraies classes Color/MathUtils,
les 48 états solaires acquis et quatre jeux de lectures synthétiques f32.
HEAD `1438b1ab`, arbre propre et hashes des sources sont contrôlés avant/après.
Le reçu `oracle.json` ferme 192 résultats, sans démarrer App ni le GPU.
Le nouveau module CPU `solar_lighting.rs` conserve les opérations source :
SUN_ILLUMINANCE 11, fade -0,03/0,02, bascule soleil/lune -0,07, lune 0,12,
ambiance nocturne 0,012 et teinte linéaire 0,6/0,7/1. Le décodeur exige le
payload source de 48 octets, les trois alpha 1 et des données finies.

Deux tests natifs std seuls passent en 0,00 s : 1 728 comparaisons sur les 192
résultats avec écart absolu maximal 0, payloads incomplets/non finis refusés,
valeurs solaires et overflow de conversion GPU refusés. Ils prouvent le calcul
CPU pour ces lectures synthétiques, sans prouver une lecture matérielle.
`result.json` ferme les quatre entrées et conserve cette portée.

`atmosphere_readback.rs` est préparé à part : Readback::buffer_range(...,0,48),
état pending unique, callback Bevy ReadbackComplete, contrôle de l'identité
Arc de génération, du handle et des paramètres avant réception puis consommation,
retrait du lecteur et annulation lors de fermeture. Il ne traite jamais le booléen
ready (commandes encodées) comme une preuve de données GPU complétées.
Le check metadata inclut trois tests ECS préparés avec événements synthétiques,
plus les deux tests Clock existants ; session 86402, résultat en attente.
Ces modules restent des overlays isolés, non enregistrés dans le dépôt actif,
non publiés dans Studio. Le cycle solaire visible, les LUTs/clouds/IBL dynamiques,
les ombres et les lampes synchronisées restent à raccorder et qualifier.

Inspection de l'API Bevy pendant le check du lecteur : World::init_resource
retourne ComponentId. Le setup de test V1 chaîne donc add sur un mauvais type.
Cette erreur appartient au harnais ECS, pas au calcul CPU déjà qualifié.
`Prepare-ReaderV2.py` prépare une copie neuve : init_resource puis resource_mut,
et assertion explicite de la plage source (0,48). Le comportement du lecteur
reste inchangé ; les sources de la compilation V1 restent intactes. Les trois
fichiers V2 sont épinglés dans `reader-v2-preparation.json`, non compilés.
Le verdict terminal V1 et le nouveau check V2 restent nécessaires.

## Suite vérifiée : lecteur ECS et environnement

Le check metadata du lecteur V1 termine avec l'erreur de setup décrite ci-dessus.
Le check V2 termine avec retour 0 ; `ecs-check-result-v2.json` conserve sa portée
de compilation uniquement. Le test natif réel V2 (session 73642) passe ensuite
par rustc puis le linker MSVC, observé actif avec son fichier d'arguments propre
à la fixture. Aucun verdict d'exécution des cinq tests n'est encore acquis ici.
Ces événements de test restent synthétiques, même si leur exécution réussit.

Le build release mouvement (session 8595) progresse de forge_engine vers
forge_studio_frontend. Aucun redémarrage ni changement des sources gelées.
Le résultat final et l'assemblage restent attendus.

Le [raccord environnement](2026-10-03-solar-environment.md) qualifie le contrôleur
sur 240 frames source et acquiert les 22 programmes originaux avec leurs layouts.
Sa qualification Bevy est lancée à part ; elle ne modifie pas le candidat en
compilation ni l'état visuel déjà observé dans Studio.

Le lecteur ECS V2 termine ensuite avec retour 0 à 09:53:11 UTC : cinq tests
exécutés, zéro échec/ignore, pins source/dépendances inchangés. Le reçu
`ecs-result-v2.json` couvre la plage (0,48), réception/retrait du lecteur,
pending/refus atomiques et changements de génération avant/après réception,
ainsi que les deux tests solaires existants. Les événements restent synthétiques :
ce PASS ne qualifie pas une lecture réelle du GPU ni la publication visible.

Le build release WASM mouvement termine à 09:57:17 UTC avec retour 0, après
38 min 05 s de Cargo. `cargo-results.json` conserve les arguments offline/locked,
opt-level 3 et la cible web ; les logs mentionnent seulement l'avis de future
incompatibilité de proc-macro-error2. La session 8595 poursuit les contrôles
d'inventaire et l'assemblage frontend/worker/finalization. Aucun reçu d'assemblage
final ni nouveau candidat natif avec ces animations n'est encore acquis ici.

L'assemblage et le worker terminent ensuite avec retour 0 à 10:13:29 UTC :
`frontend-result.json` confirme les 1 350 sources inchangées, 1 761 fichiers
frontend vérifiés, profil optimisé et jointures des reçus parent/worker.
WASM brut `20c5d808d3b041072a7a39fed89f29c008244aea57c1220e15364ef32dc020c8` ;
reçu final `b51d0d2cf20f5420789c0b460cf2028bacfe3f0b83299f3f72b3bb7d8be7ed69`.
`build-recovery-result.json` clôt la reprise ; les refus précédents restent intacts.

`Prepare-Native.py` dérive un runner neuf de Build-NativeV3 sans enlever les
gardes de sources natives, metadata, dep-info et inventaire frontend embarqué.
Destination neuve `studio-motion-desktop-20261003-v1/attempt.1` ; session 20922
lance ce build natif. Il ne remplace ni ne ferme le candidat pier-light ouvert.
La fixture de lancement prépare séparément un workspace/profil WebView neufs ;
son lancement et le parcours visuel mouvement/édition restent conditionnés au
build natif vérifié. Le rendu et la parité des animations restent à qualifier.

## Candidat natif construit et lancement réparé

La session 20922 termine à 10:40:43 UTC avec retour 0. Le reçu
`native-pier-motion-v1/result.json` contrôle 175 sources natives,
189 entrées avec metadata et 1 770 fichiers frontend embarqués, inchangés.
Exécutable : `studio-motion-desktop-20261003-v1/attempt.1/forge-studio-candidate.exe`,
SHA-256 `18f1581935f6f4e037d34e75c7e7e286d3c0de6772f4f3579bdc7b82ebba6b7b`.

Le premier script de lancement contient des fins de ligne CRCRLF qui cassent
les continuations PowerShell : le processus démarre sans FORGE_AUTHORING_ROOT.
L'interface refuse correctement l'accès auteur. Aucune zone n'est ouverte.
Le processus de cet essai est arrêté après contrôle de son identité ; les logs
et le reçu V1 restent conservés. Le script V2 normalise les fins de ligne en LF,
sans changer les sources du candidat ni les fixtures.

`launch-pier-motion-v2-result.json` confirme le lancement à 10:50:34 UTC,
PID 45548, avec workspace et profil WebView isolés. Computer Use retrouve
ensuite sa fenêtre 11736732 et observe l'accueil sans refus de racine auteur.
Le workspace Tidewater est ouvert par « Projets et modules » ; le bouton
« Ouvrir Tidewater island · zone 1 » est observé puis actionné.
Cette étape ne qualifie pas encore le chargement 3D, les animations visibles,
l'édition/persistance ni le playtest ; leurs observations restent nécessaires.

La zone termine ensuite son ouverture : « Tidewater island », 33 entrées
de scène (23 bâtiments et dix couches végétales). Le parcours Objets → Fiche →
Bibliothèque de bâtiments importés → Vérifier et charger est exécuté une fois.
Après préparation, le viewport affiche le terrain, les bâtiments, la jetée,
l'océan et le ciel ; l'interface confirme les collisions synchronisées et le
calcul d'environnement terminé. L'écran noir transitoire est conservé comme
étape de chargement, pas comme verdict final. Deux captures successives montrent
une crête de vague différente. Aucun verdict de fidélité des mouvements de
l'enseigne ou des lanternes n'est acquis sur cette vue distante.
Le bouton Jouer est actionné ; l'entrée effective en playtest reste à observer.

L'observation suivante confirme « Session relue · Playtest · sources gelées,
aucune sauvegarde implicite », Jouer désactivé et Retour à l'édition actif.
La caméra passe au niveau du joueur sur la promenade, face au village et à la
montagne. Cette preuve couvre l'entrée et le rendu du playtest, sans déplacement
joueur ni fidélité des animations de jetée. Le retour est tenté par coordonnées
puis index d'accessibilité ; les observations gardent le mode Playtest et montrent
un menu d'ancrage transitoire. Le ciblage de ces entrées doit être vérifié avant
de conclure à un défaut fonctionnel du retour. Aucun verdict de retour acquis.

La reprise avec un index d'accessibilité frais confirme ensuite le retour :
Jouer actif, Retour à l'édition désactivé, « Session relue · Session synchronisée »
et outils de terrain actifs. La préparation rend d'abord un viewport noir,
puis terrain, bâtiments, jetée, ciel et océan réapparaissent sans recharger
manuellement la bibliothèque. Le reçu `runtime-fixture-v1/cycle-files-v1.json`,
daté 11:13:09 UTC, revérifie les 26 fichiers initiaux de la fixture : tous leurs
hashes sont inchangés. Cela qualifie le cycle entrée/retour et la conservation
des sources sur ce candidat ; ni le déplacement joueur ni une retouche
sauvegardée ni les animations des lanternes n'ont été vérifiés par ce cycle.
