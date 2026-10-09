# DÃ©bris â€” entrÃ©e commune native et Studio

Objectif : rendre l'acquisition procÃ©durale utilisable par le transport
asynchrone de Studio, sans seconde source de placements ni dÃ©pendance Bevy
dans les documents. Cette Ã©tape ne qualifie pas l'Ã©dition dans Studio.

## Code ajoutÃ©

`crates/forge_assets/src/tidewater_debris/native.rs` expose `NativeInput` et
deux transports injectÃ©s, synchrone et asynchrone. Tous deux vÃ©rifient le
descripteur `debris-native.json`, source 1438b1ab, SHA-256
`63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216`.
Les 4 909 payloads (18 756 410 octets) sont vÃ©rifiÃ©s avant toute restitution.
Le lecteur compose les contrats Assets existants `DebrisPackage` et
`CollisionFrames` ; il renvoie aussi le programme Nature vÃ©rifiÃ©. Les crÃ©dits
et la description de dÃ©rivation du shader sont vÃ©rifiÃ©s dans le mÃªme lot.
Aucun Entity, Handle, filesystem, fetch navigateur ou politique d'Ã©dition
n'entre dans ce lecteur.

Le descripteur est bornÃ© Ã  1 Mio, chaque payload Ã  8 Mio et leur total Ã 
32 Mio. Les chemins relatifs refusent segments vides, traversÃ©es, sÃ©parateurs
Windows et prÃ©fixes avec deux-points. Le SHA figÃ© prÃ©cÃ¨de tout transport.
Le renderer doit encore vÃ©rifier/dÃ©river l'interface WGSL comme aujourd'hui.

## Validation

La session 82474 exÃ©cute rÃ©ellement :

```powershell
$env:FORGE_DEBRIS_NATIVE_FIXTURE='E:/OpenRPG_Forge/.tidewater-integration/debris-native-input-20261005-v1'
cargo +1.98.0 test -p forge_assets tidewater_debris::native::tests --offline -j 1 -- --include-ignored --nocapture
```

Le premier test exige le refus d'un mauvais descripteur avant toute lecture.
Le second charge les fichiers acquis par les deux voies, compare les frames
et le shader, vÃ©rifie les 946 propriÃ©taires de collision, puis altÃ¨re un
octet du shader et exige son refus explicite. Le transport asynchrone de ce
test utilise des futures immÃ©diatement prÃªtes ; il ne prouve pas HTTP/WebView.
La session 82474 termine code 0 : compilation 4 min 31 s, puis deux tests
PASS, zÃ©ro Ã©chec et zÃ©ro ignorÃ© (107 autres tests filtrÃ©s), exÃ©cution 0,82 s.
Le scÃ©nario acquis ignorÃ© par dÃ©faut est bien exÃ©cutÃ© avec include-ignored.
La session 50270 passe ensuite Clippy Assets --lib avec -D warnings
(48,47 s), puis cargo check Assets pour wasm32-unknown-unknown (1 min 28 s).
Le contrÃ´le du probe natif continue dans cette mÃªme session. Les SHA et
rÃ©sultats sont consignÃ©s dans le reÃ§u local
`E:/OpenRPG_Forge/.tidewater-integration/debris-shared-input-tests-20261005-v1.json`.

Formatage du nouveau module et vÃ©rification des espaces passent. Le contrÃ´le
Git global relÃ¨ve du whitespace prÃ©existant dans les tests de navigation,
hors pÃ©rimÃ¨tre modifiÃ© ici ; ces fichiers sont conservÃ©s.

## Raccord Studio restant

La bibliothÃ¨que est chargÃ©e par
`apps/forge_studio/src/authoring_assets/imported_buildings.rs` et ses champs
Candidate explicitement versionnÃ©s. La disponibilitÃ© d'une bibliothÃ¨que ne
doit jamais crÃ©er ses objets sur une zone Ã©trangÃ¨re. L'association au raster
vÃ©rifiÃ© et les snapshots hÃ©ritÃ©s restent dans
`imported_buildings/source_objects/binding.rs`.

Le raccord doit publier le lot complet aprÃ¨s validation, exposer les assets
dans le catalogue commun et projeter les placements rÃ©solus par les adapters
Engine existants. Les modifications passent par ManualPlacement/PlacementEdit,
SourceObjectState et les commandes communes, avec tombstone Removed conservÃ©
aprÃ¨s suppression de la derniÃ¨re copie, Undo/Redo et Save/Close/Open.
Les variations scannÃ©es gardent leurs prototypes et leur RNG immuables.

Le bootstrap natif `examples/tidewater_plant_probe/debris.rs` utilise maintenant
ce lecteur, avec lectures filesystem bornÃ©es et confinement des fichiers
rÃ©solus au paquet. L'interface Nature reste dÃ©rivÃ©e/vÃ©rifiÃ©e par Engine avant
adoption ; aucune surface PBR de remplacement n'est introduite. La compilation
de ce raccord reste Ã  vÃ©rifier. Le loader Studio le consomme maintenant
via le champ optionnel debris_native_sha256 et un adapter asynchrone dÃ©diÃ©.
Le ZIP portable debris-002 reste le binaire prÃ©cÃ©demment
testÃ© et n'est pas prÃ©sentÃ© comme recompilÃ© avec cette Ã©volution Assets.
La qualification UI, la projection modifiÃ©e et la paritÃ© visuelle restent
ouvertes ; aucun commit, push ni clÃ´ture de gate.

## Loader Studio et staging local

`apps/forge_studio/src/authoring_assets/imported_buildings/debris.rs` charge
le descripteur et ses payloads par le transport same-origin existant, refuse
un ticket de chargement remplacÃ©, prÃ©pare les meshes CPU et vÃ©rifie
l'interface Nature avant restitution. La publication reste dans le bloc
de publication de la bibliothÃ¨que complÃ¨te, aprÃ¨s les vÃ©rifications des
autres paquets et du ticket final. L'absence du champ optionnel publie None.
Cette acquisition ne crÃ©e ni placement ni commande dans une zone.
La session 99699 lance cargo check forge_studio --target wasm32-unknown-unknown
--no-default-features --features web --lib --offline -j 1. Elle attend le
verrou Cargo du contrÃ´le natif 50270 ; aucun processus n'est arrÃªtÃ© ou relancÃ©.
Le rÃ©sultat du frontend reste non acquis Ã  ce stade.

`prepare-building-library-site.py` accepte --debris-native-root et
--debris-native-sha256. Il vÃ©rifie le descripteur figÃ©, l'inventaire, les
bornes et chaque payload avant de crÃ©er le staging. Il recopie les sous-dossiers
dans forge-imports/<pin>, vÃ©rifie encore les fichiers copiÃ©s et ajoute le
champ optionnel au candidat.

La session 15690 termine code 0 pour
`E:/OpenRPG_Forge/.tidewater-integration/studio-debris-library-site-20261005-v1` :
821 payloads du paquet bÃ¢timents a53b6987 et 4 909 payloads de dÃ©bris,
sans toucher au site Studio existant. SHA-256 de buildings.json :
`7beee686c67b2009bb196b628752cd9a36cdf94d3f6f1505538ca9d547e05850`.
Les deux tests Assets sont ensuite rÃ©exÃ©cutÃ©s par leur vrai binaire sur le
sous-dossier de dÃ©bris stagÃ© : deux PASS, zÃ©ro ignorÃ©, 0,72 s.
La session 86913 exige un pin nul et confirme un refus avant toute crÃ©ation
du dossier nÃ©gatif (code final du contrÃ´leur 0, erreur Python attendue).

Ce staging comprend seulement bÃ¢timents et acquisition procÃ©durale ; ce
n'est pas une nouvelle scÃ¨ne complÃ¨te ni une bibliothÃ¨que Studio qualifiÃ©e
par HTTP/WebView. La sÃ©lection, le catalogue, le viewport, les ombres, les
collisions et la persistance des dÃ©bris dans Studio restent Ã  raccorder et
Ã  tester avec le cycle commun. Les 141 variantes scannÃ©es et leurs atlas
restent sÃ©parÃ©s de cette acquisition procÃ©durale.

## Cycle d'auteur sur les placements acquis â€” test en attente

`crates/forge_assets/tests/debris_common_authoring.rs` exerce directement
le binding source de production de Studio et les commandes ZoneEditSession.
Il charge le vrai NativeInput stagÃ©, vÃ©rifie 805 identitÃ©s distinctes et
leur sÃ©paration entre deux zones, puis choisit trois objets acquis Ã 
surfaces bois, tissu et Nature. Pour chacun : translation et Ã©chelle,
duplication conservant la rÃ©fÃ©rence, rÃ©ouverture d'archive avec les deux
placements, suppression de l'original et de sa derniÃ¨re copie, Undo qui
restaure uniquement la copie, Redo, puis seconde rÃ©ouverture conservant
Removed et zÃ©ro copie. Aucun algorithme de commandes ni de rÃ©solution
n'est remplacÃ© par un stub.

La session 98527 lance cargo test -p forge_assets --test debris_common_authoring
--offline -j 1 -- --include-ignored --nocapture, avec le sous-dossier de
staging comme FORGE_DEBRIS_NATIVE_FIXTURE. Elle attend le verrou Cargo,
comme la compilation frontend 99699 ; le contrÃ´le natif 50270 poursuit
son check Engine. Aucun rÃ©sultat de ce nouveau scÃ©nario n'est encore acquis.
Le test ne couvre ni HTTP, ni GPU, ni gestes Studio ; mÃªme un PASS ne
remplacerait pas le raccord de catalogue et de projection restant.

## ContrÃ´le natif terminÃ© et projection Studio prÃ©parÃ©e

La session 50270 termine code 0. AprÃ¨s Clippy Assets et check Assets WASM,
le check du vÃ©ritable exemple tidewater_plant_probe passe en 23 min 39 s.
Il conserve un warning dead_code prÃ©existant pour isolated_lod::run dans
tidewater_object_shadow_probe, hors lecteur/raccord modifiÃ©s. C'est un
contrÃ´le de compilation, sans nouveau build exÃ©cutable ni replay GPU ; le
ZIP debris-002 reste le candidat antÃ©rieurement jouÃ©.

`apps/forge_studio/src/authoring_assets/imported_scene/debris.rs` prÃ©pare
la projection des placements rÃ©solus : mÃªmes rÃ´les material-0/1/5, vrai
shader Nature, textures de terrain acquis avec mips et sampler vÃ©rifiÃ©s,
adapter Engine PreparedScene et primitives CollisionFrames. Le remplacement
prÃ©pare le lot avant de remplacer les racines. Le retrait utilise un lot
vide du mÃªme adapter pour conserver la vÃ©rification de retraite Rapier.
Le reÃ§u exige les racines/transforms actuels et les statuts de collisions,
sans utiliser le checker global sur un sous-ensemble de placements.
Une bibliothÃ¨que disponible avec zÃ©ro placement ne requiert pas le terrain
importÃ© dans une zone Ã©trangÃ¨re.

Le fichier est volontairement **non dÃ©clarÃ© dans le parent** pendant le
check frontend en cours ; aucune projection visible nouvelle n'est attribuÃ©e
Ã  ce code. La session 52272 contrÃ´le le vrai module par rustc --emit=metadata
contre les APIs natives actuellement compilÃ©es. Son rÃ©sultat reste en attente.
Cette sonde de types n'est ni une compilation du produit WASM, ni un test
de rendu, de picking, d'ombres ou de persistance. Les raccords de catalogue,
de binding de source, de filtres communs et du collecteur d'ombres sont encore
Ã  appliquer aprÃ¨s stabilisation du frontend en cours.

Le premier harnais (52272) termine code 1 : son choix Â« dernier .rmeta Â»
mÃ©lange des configurations Bevy et produit des erreurs de traits/World entre
crates distinctes. Aucun trait produit n'est modifiÃ© pour contourner cet Ã©chec.
Le second harnais rÃ©sout chaque dÃ©pendance directe depuis les fingerprints
de forge_engine-f099e9edf341f10b (comparaison exacte des hashes Cargo).
La session 85888 termine code 0 et type-checke le vrai fichier de projection,
sans remplacer Engine ni Bevy par des stubs. Le mapping est conservÃ© dans
`studio-debris-projection-metadata-20261005-v2.json` et la sortie dans
`studio-debris-projection-check-20261005-v2.rmeta`, sous .tidewater-integration.
Le module a aussi Ã©tÃ© corrigÃ© pour importer explicitement l'alias bevy_ecs
requis par les dÃ©rivations du contexte Studio. Il reste non enregistrÃ© ;
ce PASS de types n'est pas un passage jouÃ© ou une validation GPU/UI.

## Activation du raccord Studio et historique commun

La session 99699 termine code 0 : check de la bibliothÃ¨que Studio pour
wasm32-unknown-unknown, feature web, offline, en 35 min 54 s. Cette baseline
compile l'acquisition asynchrone avant activation du renderer ; elle conserve
les warnings de visibilitÃ© du catalogue et preview::configure, ainsi que
l'avis future-incompatibility de proc-macro-error2. Ce PASS ne qualifie pas
le viewport.

La session 98527 termine code 0 : le test rÃ©el
acquired_debris_edits_copies_and_final_deletion_survive_history_and_reopen
passe (un test, zÃ©ro Ã©chec, zÃ©ro ignorÃ©, 0,36 s ; compilation 33 min 21 s).
Il couvre les 805 identitÃ©s et le cycle commun sur trois surfaces, avec
archives rouvertes et tombstones. Les gestes UI, le picking et le GPU restent
hors de cette preuve.

Le chargeur conserve maintenant PreparedDebris aprÃ¨s validation au lieu de
jeter les meshes CPU. Le catalogue commun expose les rÃ©fÃ©rences acquises ;
source_objects applique le mÃªme binding et la mÃªme association de raster
que les autres objets importÃ©s. Les placements rÃ©solus sont exclus du
transport GLB et projetÃ©s par l'adapter Engine dÃ©jÃ  utilisÃ© au natif.
Le module est dÃ©clarÃ© et installÃ© dans imported_scene ; la disponibilitÃ©
exige sa synchronisation exacte et ses collisions. Les racines reÃ§oivent
les gizmos communs et le focus suit leurs parts.

Les seuls triangles casters acquis rejoignent le collecteur d'ombres
existant, avec invalidation lorsque les racines changent. Les matÃ©riaux
Nature reÃ§oivent aussi l'upload du vent commun. Aucun nouveau format de
sauvegarde ni politique d'Ã©dition par nom de carte n'est introduit.
La visibilitÃ© de l'API catalogue est rÃ©duite au pÃ©rimÃ¨tre de son Entry.

La session 71248 contrÃ´le ce raccord actif par cargo check -p forge_studio
--target wasm32-unknown-unknown --no-default-features --features web --lib
--offline -j1. RÃ©sultat encore en attente au moment de cette entrÃ©e.
Cette activation en source n'est pas une preuve de rendu, de catalogue
affichÃ©, de picking ou de cycle Save/Open dans le produit. Les variantes
scannÃ©es restent Ã  raccorder sÃ©parÃ©ment ; le candidat Windows debris-002
n'est pas reconstruit par ce contrÃ´le.

La session 71248 termine code 0 en 3 min 31 s : le raccord **actif**,
y compris MaterialPlugin Nature, catalogue, source binding, projection,
gizmos, focus, vent et collecte d'ombres, compile pour WASM. Le warning de
visibilitÃ© du catalogue disparaÃ®t aprÃ¨s rÃ©duction de sa portÃ©e ; le warning
preview::configure et l'avis proc-macro-error2 restent prÃ©existants.
Le reÃ§u `studio-debris-activation-20261005-v1.json` Ã©pingle les neuf fichiers
du raccord, les deux sessions et leurs pÃ©rimÃ¨tres. Le diff check ciblÃ© passe.

La prÃ©paration d'un candidat isolÃ© est lancÃ©e en session 44973 par
`prepare-studio-debris-candidate-20261005-v1.py`. Elle vÃ©rifie la bibliothÃ¨que
complÃ¨te du frontend commun v2 avant copie, conserve tous les champs du
candidat (terrain, environnement, plantes, village, plage et poissons), puis
ajoute les 4 910 fichiers de l'entrÃ©e de dÃ©bris. Elle fige Ã©galement les
sources produit courantes sur une copie du snapshot prÃ©cÃ©dent. Le nouveau
dossier est `studio-debris-candidate-20261005-v1`, sans dÃ©ploiement dans le
site Studio existant. Son rÃ©sultat terminal reste Ã  acquÃ©rir.

La session 44973 termine code 0 : 1 422 fichiers source figÃ©s,
1 418 fichiers courants repris/vÃ©rifiÃ©s et 7 910 fichiers de bibliothÃ¨que.
Le manifeste source a SHA-256
`7ce2136a01bd1f77f586347dadccbd51f72030817a885de69db812ef2c6a1e31`.
Le reÃ§u `preparation.json` conserve chaque empreinte et le parent exact.
Tous les champs prÃ©cÃ©dents du candidat sont conservÃ©s ; aucun ancien
frontend liÃ© n'est prÃ©sentÃ© comme incorporant le raccord actuel.

La liaison release du frontend figÃ© est lancÃ©e en session 97404, par
`build-studio-debris-candidate-20261005-v1.py`, dans le cache release existant
avec les mÃªmes options codegen-units=16. Le rÃ©sultat n'est pas encore acquis.
Le prÃ©contrÃ´le de navigation refuse la rÃ©utilisation de l'ancien worker :
sur 28 pins, `crates/forge_navigation/src/provider.rs` diffÃ¨re. Les anciens
artefacts sont intacts, mais incompatibles avec le snapshot courant.
La session 30637 lance donc le vÃ©ritable Build-NavigationWorker.ps1 depuis
le snapshot figÃ©, en release ; elle attend le verrou du frontend. Le script
du snapshot est comparÃ© au script courant avant exÃ©cution.

`assemble-studio-debris-candidate-20261005-v1.py` est prÃ©parÃ© et sa syntaxe
est contrÃ´lÃ©e, mais il n'est pas encore exÃ©cutÃ©. Il exige les reÃ§us de build,
les pins des sources et du worker, prÃ©serve la bibliothÃ¨que et le kit GLB
vÃ©rifiÃ© du parent, puis produit de nouveaux bindings. Il ne doit Ãªtre lancÃ©
qu'aprÃ¨s sortie terminale rÃ©ussie des deux sessions. Le dessin, le picking,
l'historique par gestes UI et la paritÃ© source restent non qualifiÃ©s.

## Inventaire indÃ©pendant pour la prochaine exÃ©cution Studio

`prepare-stand-smoke-inventory.py` accepte le pin procÃ©dural optionnel.
Il vÃ©rifie le descripteur natif, ses 4 909 payloads, le manifeste gÃ©omÃ©trique
et les frames avant de calculer les identitÃ©s depuis le contrat UUID Forge.
L'inventaire du candidat figÃ© contient 986 objets : les 181 entrÃ©es prÃ©cÃ©dentes
sont strictement conservÃ©es, avec 805 sources supplÃ©mentaires. Il garde
23 placements manuels et attend 963 sources hÃ©ritÃ©es. Ce nombre concerne
ce jeu de donnÃ©es et cette fixture, pas tous les objets possibles de l'Ã®le.

L'inventaire a SHA-256
`5ae77297c5486d26e356b898c4d7eb4052da7ca664af753d48ce6899ce208cf8`.
`verify-studio-debris-inventory-20261005-v1.py` confirme l'Ã©galitÃ© exacte de
l'ancienne baseline, l'absence de collision d'identitÃ©s et la conservation
des 181 entrÃ©es. Deux contrÃ´les nÃ©gatifs (pin nul et descripteur altÃ©rÃ©)
sont refusÃ©s avant crÃ©ation d'inventaire. RÃ©sultat code 0, reÃ§u
`studio-debris-candidate-20261005-v1/inventory-verification.json`.
Une premiÃ¨re invocation utilisait Ã  tort scene/authoring.json dans le
paquet portable ; elle Ã©choue avant toute Ã©criture. L'invocation rÃ©ussie
utilise l'archive source canonique de studio-tidewater-review-20260927-001,
dont le pin attendu reste 50bfd655.

Les outils de construction desktop prÃ©cÃ©demment exercÃ©s sont dÃ©rivÃ©s dans
le nouveau candidat, avec validation des sources figÃ©es et vÃ©rification des
empreintes du frontend. Ils ne sont pas exÃ©cutÃ©s Ã  ce stade. Le scÃ©nario
`run-studio-debris-editing-20261005-v1.py` est Ã©galement prÃ©parÃ© : inventaire
strict de 986 objets, insertion commune de l'Ã©tal et rÃ©ouverture (987 objets),
puis cycle d'Ã©dition sur group:crates (bois), group:traps (tissu), coconut
(Nature) et l'Ã©tal manuel ajoutÃ©. Les sources seront choisies dans le dropdown
public, sans injection d'identitÃ©s. Le picking GPU des dÃ©cors restera une
preuve distincte ; aucune exÃ©cution UI ni paritÃ© visuelle n'est anticipÃ©e.

## Ã‰chec de snapshot corrigÃ© dans une dÃ©rivation prÃ©servant V1

La session 97404 termine code 101, sources figÃ©es inchangÃ©es : la compilation
Engine ne trouve pas l'acquisition incluse par tidewater_wind/acquisition.rs,
`third_party/tidewater/acquisitions/animation-reference-village-1438b1ab.json`.
Le build de code courant avait accÃ¨s Ã  ce fichier ; le snapshot n'avait
pas fermÃ© ses dÃ©pendances de donnÃ©es hors crates/apps. Aucun rÃ©sultat
frontend liÃ© n'est attribuÃ© Ã  V1.

Le scan des inclusions constantes include_str!/include_bytes! du snapshot
identifie exactement cette entrÃ©e manquante. La dÃ©rivation V2 conserve
V1 intact et reprend ses 1 422 fichiers sans changement de code, puis ajoute
les 1 631 octets de l'acquisition canonique, SHA-256
`8d24e214166c6f59022e094b64257fd5fea7e34c998e97cf235d103481ee2e2e`.
L'acquisition conserve source 1438b1ab, 120 frames de warmup et ses phases
effectivement capturÃ©es. Aucun temps de vent arbitraire n'est substituÃ©.
Le manifeste des 1 423 sources V2 a SHA-256
`4f6420b3b9b7f32700d43386af50b14a5d0321d815c4ab1752045d17058f1c4c`.
Le builder de snapshot est corrigÃ© pour fermer ces inclusions avant gel.

La session 30637 termine code 0 : vÃ©ritable worker release construit en
19 min 13 s, depuis V1. Ses 28 pins de source correspondent exactement Ã 
V2 et ses artefacts sont vÃ©rifiÃ©s avant copie dans V2 ; reÃ§u
`studio-debris-candidate-20261005-v2/navigation-worker-result.json`.
Il est rÃ©utilisÃ© sous contrÃ´le dans cette dÃ©rivation et non annoncÃ© comme
recompilÃ© depuis V2.

La session 38360 lance le frontend V2 dans le cache release, mÃªmes options
et checks. Elle reste active au moment de cette entrÃ©e. La bibliothÃ¨que et
l'inventaire de 986 objets restent ceux de V1, par rÃ©fÃ©rence vÃ©rifiÃ©e ; le
renderer n'est pas modifiÃ©. Le nouveau reader ScannedInput du code courant
est hors de ce snapshot V2 et possÃ¨de des preuves distinctes dans
[son rapport](2026-10-05-scanned-shared-input.md).

Suite du 6 octobre : la session 38360 termine code 0. Les 1 423 sources
figÃ©es sont intactes ; le WASM brut liÃ© mesure 127 053 335 octets, SHA-256
`9a0fff28ffc5eed2438fbe743ab52701670792376cb98a2f2cd5900da490f035`.
L'assemblage 50885 passe avec 7 930 fichiers, neuf fichiers du kit conservÃ©s
et le worker rÃ©utilisÃ© sous contrÃ´le. wasm-bindgen 0.2.127 produit le WASM
distribuÃ© de SHA-256
`cde6acede96a961775d13c54321d8c2b36bedf7f9a55054ba62c16867edc4ca3`.

Le contrÃ´le HTTP 90706 passe sur un serveur local temporaire : 4 910 fichiers
procÃ©duraux, 19 725 262 octets, longueurs et empreintes exactes ; le WASM et
index sont accessibles, MIME WASM application/wasm, payload absent refusÃ© 404.
Le serveur est fermÃ© ensuite. Ce test ne lance ni navigateur ni Studio, et
ne qualifie pas le fetch WASM, le rendu ou les gestes d'Ã©dition.

La prÃ©paration native 58841 Ã©choue avant construction : le Git enfant du
constructeur canonique refuse le propriÃ©taire du dÃ©pÃ´t, malgrÃ© la branche
Rust dÃ©jÃ  contrÃ´lÃ©e avec safe.directory. BuildNative transmet dÃ©sormais
cette exception exacte au seul environnement enfant, sans configuration
globale ni changement de source figÃ©e. Le premier Ã©chec est conservÃ© dans
native-validation-failure-v1.json. La reprise 56358 obtient INPUTS_VALID
pour les 7 930 fichiers et lance rÃ©ellement le build Tauri, qui attend le
verrou Cargo des contrÃ´les dÃ©jÃ  actifs. Aucun nouveau runtime Studio n'est
encore qualifiÃ©.


## Native V2 — terminaison et sorties Tauri, 6 octobre

La construction native 56358 termine réellement 0 en 91 min 30 s. Sa garde de
wrapper termine 1 : quatre fichiers `gen/schemas` sont régénérés par Tauri.
L'audit ne relève aucun autre changement source ni aucun changement des
7 930 fichiers frontend. Les fichiers générés sont archivés séparément, leurs
fonctions d'écriture vérifiées dans tauri-build/tauri-utils, puis les octets du
snapshot original sont restaurés. Aucun nouveau build Rust n'est lancé.

Le finalizer conserve ses deux échecs intermédiaires : formats ANSI du log puis
nom annoncé par la CLI (`forge_studio.exe`, absent). Le vrai target Cargo est
`forge-studio.exe`, neuf, avec depfile référençant la source V2 figée. Ce nom
était correct dans le wrapper initial ; l'audit intermédiaire sur les noms est
corrigé explicitement dans `native-finalizer-failure-v2.json`.

Le finalizer termine 0, copie le vrai exe et le frontend, et contrôle tous les
pins. Exécutable candidat SHA-256 :
`4e5f34caddf3d11c8e4ded53729085a51e48d385536d690df4140cc16fa10479`.
`native-result.json` distingue honnêtement l'arbre modifié pendant le build,
les inputs de code intacts et l'arbre restauré ; il ne transforme pas la garde
initiale en succès. Les quatre sorties générées et le premier échec sont gardés.

Le runner d'interface vérifie cette preuve, tous les fichiers sources restaurés
et les quatre archives générées avant lancement. Le scénario catalogue/stand,
persistance graphique et ombres réelles est lancé sous 45639 ; le cycle commun
wood/fabric/Nature n'est déclenché que si ce premier scénario passe. Ce candidat
805 ne contient pas les nouveaux modules de cailloux ni le raccord Studio des
141 scans. Aucun verdict GPU/UI ni parité n'est encore attribué.

## Essai UI et correction des identités de casters — 6 octobre

Le premier essai 45639 ne trouve pas le endpoint WebView CDP dans le sandbox.
Une sonde du même exécutable hors sandbox obtient immédiatement la page Studio ;
le scénario est donc repris hors sandbox avec une nouvelle copie isolée (71529).
Il atteint l'insertion catalogue de l'échoppe, Apply, Save et la réouverture,
avec captures `runtime-stand-v2/fish-stand-applied.png` et
`runtime-stand-v2/fish-stand-reopened.png` dans le candidat V2 du workspace.

Le scénario termine néanmoins 1 sur les ombres animées : le journal indique
`Opaque shadow caster identities changed`. Le cycle commun wood/fabric/Nature
n'est pas exécuté. Exécutable, frontend et helpers restent inchangés. Les
captures attestent l'interface atteinte, pas une qualification graphique.

Le snapshot recensait toutes les racines Debris, tandis que l'animation ne
recensait que les casters des bâtiments. Une racine Debris peut porter zéro ou
plusieurs casters. Le correctif courant utilise une même énumération, dans
l'ordre réel des draws, pour le snapshot et l'animation ; les transforms sont
lues sur les racines vivantes et les racines cachées ne projettent pas.
Le test acquis couvre également déplacement, ordre des identités et retrait
des parties projetantes d'une racine masquée. Ses validations sont lancées dans
`shadow-caster-validation-20261006-v2` ; aucun succès n'est encore attribué.
Ce correctif n'est pas contenu dans l'exécutable V2 figé. Un candidat corrigé
et un nouvel essai UI restent nécessaires avant tout verdict sur les ombres.

La préparation du nouveau candidat conserve une première tentative refusée :
le dossier source V2 contient aussi une permission Tauri générée, absente du
manifeste épinglé (`permissions/autogenerated/read_asset_model.toml`). La copie
complète est refusée ; aucun frontend n'est construit dans cette tentative.
La deuxième préparation copie uniquement les 1 423 fichiers du manifeste,
puis remplace les trois fichiers du correctif. Les autres pins et le parent
restent inchangés ; 28 pins source du worker et ses trois artifacts sont
revérifiés. La bibliothèque acquise est réutilisée par référence.

Le nouveau snapshot est `studio-shadow-candidate-20261006-v2`, manifeste
`aca7ee3b2b271696f078f8b90e5db2705adb63e48d24c301ad5b3d54fbc9e354`.
Sa construction frontend release WASM est lancée sous 68207, dans le cache
frontend séparé. Le test acquis Engine reste actif sous 26572. Aucun succès de
ces travaux ni rendu corrigé n'est encore attribué ; aucune reconstruction de
l'exécutable natif n'a encore été lancée pour ce snapshot.

Le scénario UI du candidat corrigé est préparé dans `RunEditing.py` avec reçu
`editing-preparation.json`. Il dérive du scénario V3 déjà exécuté, avec seul
changement de racine du candidat, et conserve les contrôles d'intégrité du
frontend, de l'exécutable, de l'inventaire et des helpers. Les ombres vivantes
restent exigées avant le cycle commun transform/duplicate/lock/delete/undo/redo/
save/reopen sur wood, fabric et Nature. Ce scénario n'est pas encore exécuté ;
il nécessite un frontend et un exécutable natif terminés et vérifiés. Le picking
GPU des nouveaux débris et la parité visuelle restent des validations séparées.

La préparation native corrigée dispose ensuite d'un audit explicite des sorties
Tauri. Le résultat Cargo est conservé avant la finalisation. Seuls les quatre
fichiers schemas connus et l'unique permission générée `read_asset_model.toml`
peuvent différer du snapshot ; toute autre mutation refuse l'assemblage. Les
octets générés sont archivés avant restauration des schemas et retrait de la
permission de cette copie de travail propre au scénario. L'inventaire source
complet, pas seulement les pins présents, doit retrouver le manifeste.
L'exécutable et son depfile doivent être neufs et référencer ce snapshot ; tous
les fichiers frontend sont revérifiés. Le runner UI vérifie également ces
archives et cet inventaire avant lancement. Ces helpers passent l'analyse
syntaxique Python ; leur exécution native n'a pas encore eu lieu. Le reçu
`native-audit-preparation.json` remplace les pins de helpers de la préparation UI
initiale, qui documentait uniquement le changement de racine précédent.

## Candidate ombres V2 — compilation frontend terminée

`studio-shadow-candidate-20261006-v2/frontend-result.json` confirme code 0,
application WASM liée et sources figées inchangées. Le WASM produit mesure
127024825 octets, SHA-256
`f46f8fc94188fff38590c29e115c64a20904b4c18a27e2e21894979832d04b57`.
L'assemblage du frontend est lancé après vérification de ce reçu. Cette preuve
ne comprend ni lancement du navigateur, ni exécution du Studio natif, ni
validation visuelle du correctif des identités des casters.

L'assemblage termine ensuite à code 0 : 7930 fichiers, neuf fichiers de kit
préservés, worker de navigation réutilisé après vérification de ses pins. La
préparation du frontend distinct pour le constructeur natif termine à code 0.
`BuildNative.py` est lancé sur ce snapshot et ce frontend ; son résultat et
l'audit des sorties générées restent attendus. Aucune exécution de cette nouvelle
application ni correction visuellement acceptée n'est encore attribuée.

Le test acquis du correctif courant termine ensuite à code 0 :
`common_instances_reject_bad_revision_then_preserve_deletion_and_casters`,
un test réussi en 20,31 s après 46 min 31 s de construction. Le reçu
`shadow-caster-validation-20261006-v2/result.json` confirme les trois pins
inchangés. Cette portée vérifie les casters par partie, le déplacement, le
masquage/restauration et la suppression commune sur les données acquises ;
elle n'exécute pas le renderer. Clippy Engine est lancé ensuite ; le check
WASM courant suit seulement son succès. Le build Tauri du snapshot corrigé
a franchi la vérification des entrées et attend le verrou Cargo partagé.

## Studio corrigé — exécution du 6 octobre

Le build natif du snapshot `studio-shadow-candidate-20261006-v2` termine à
code 0 après 25 min 34 s. L'audit confirme les entrées de code et frontend
inchangées. Tauri a régénéré quatre schémas et une permission attendue : leurs
bytes sont archivés, les schémas initiaux restaurés et la permission supplémentaire
retirée uniquement de cette copie de validation. L'inventaire source redevient
exactement celui du manifeste. Le binaire conservé a pour SHA-256
`4c7d2c3a7f833d39bfadb748d38270556f2e0dc8e4534a9e2de23005b382aee9`.

Le parcours réel `runtime-stand-v2/fish-stand-studio-smoke-ledger.json` est
`passed`, sans erreur navigateur et avec fixture originale préservée. L'insertion
catalogue, Apply, Save, réouverture et persistance graphique sont vérifiées.
Les deux mesures d'ombres restent dans la même génération, sans attente, avec
421 parties opaques et progression des trois couches et du temps de vent.
Le résumé `live-shadow-summary-v2.json` conserve les valeurs : couches
[11,8,5] vers [19,12,7], puis [46,25,15] vers [53,29,16]. La capture de playtest
a été ouverte et inspectée ; l'erreur précédente d'identités n'est pas observée.

La cadence rAF mesurée est cependant d'environ 1,19 image/s sur chacun des deux
intervalles. Ces compteurs mesurent des commandes encodées et la cadence de la
WebView, pas des fences GPU ni une parité des pixels. Ils ne qualifient pas la
fluidité et ne permettent pas d'attribuer seuls sa cause. Les conditions de
visibilité/focus de la fenêtre et la performance du rendu doivent être contrôlées
avant toute comparaison de performance avec la référence.

Le cycle commun suivant n'atteint pas l'application : le verifier refuse le
dossier du build comme IsolationRoot, exigeant un dossier dédié sous
`.editor-placeables-*` ou `.placeables-smoke-*`. Le résultat global du runner
porte donc stand 0 / lifecycle 1 ; `common_lifecycle_executed` y désigne l'appel
du script, pas une édition effectivement accomplie. Cette limite est conservée.

La préparation V3 copie et vérifie le même exécutable, les 7930 fichiers web et
la fixture sauvegardée sous `.editor-placeables-tidewater-debris-20261006-v3`,
avec candidat, source et sortie séparés. Le même verifier est relancé sous 97182
pour le cycle wood/fabric/Nature et stand manuel, sans assouplir sa politique.
Aucun succès de ce nouveau cycle n'est encore attribué.

Le contrôle Clippy Engine global du worktree courant termine par ailleurs à
code 101 : 92 diagnostics sur 27 fichiers, incluant des modules Tidewater et
de graphe source. Le check WASM suivant n'est pas exécuté par cette séquence.
Le test acquis des casters reste PASS. Une copie isolée des 27 fichiers recense
les erreurs et prépare 16 corrections de backticks dans la documentation ;
aucune modification de production ni suppression globale de lint n'est faite
pendant le build du runtime. Les autres diagnostics restent à résoudre.

### Résultat du cycle commun V3 — 6 octobre

Le processus 97182 termine avec code 1. Le ledger réel
`.editor-placeables-tidewater-debris-20261006-v3/proof/common-placeable-studio-ledger.json`
porte `failed_closed`. Les quatre cas (crates, traps, coconut et échoppe manuelle)
ont atteint translation, rotation, échelle, verrouillage, duplication et
déverrouillage. Le checkpoint sauvegardé vérifie les identités, pins et
transformations des originaux et copies avant la réouverture.

La réouverture native de la zone aboutit à une session synchronisée, mais
`Wait-CommonLibrary` expire après 180 secondes avec le statut public
« Vérification de la bibliothèque locale… ». L'essai ne valide donc ni
l'inspecteur après réouverture, ni suppression/Undo/Redo, ni la seconde
persistance des suppressions. La capture `common-failure.png` est inspectée :
la carte et le catalogue sont visibles, mais le bouton de vérification est
désactivé et le statut reste en attente. Ce rendu ne prouve pas l'achèvement
du chargement demandé. Source et frontend restent inchangés, et le listener
CDP du processus possédé est fermé.

L'inspection du code identifie un risque à investiguer : la restauration
automatique et le chargement explicite utilisent le même compteur de tickets,
mais seul le premier utilise `PROJECTION_LOADING`. Le callback explicite quitte
sur ticket remplacé avant de libérer son indicateur `busy`. L'expiration réelle
ne suffit pas à attribuer sa cause à cette concurrence ; il faut instrumenter
les tickets ou vérifier la correction dans le parcours effectif. Aucune
augmentation du délai ni réussite supposée n'est utilisée pour contourner l'échec.

Les corrections Clippy restent isolées sous
`.tidewater-integration/engine-clippy-fixes-20261006-v3` : patrons PNG,
conversion entière sans perte, accès `must_use`, admission atomique des shaders
par entrée de map et renommage des variables locales des pendules. Le manifeste
`semantic-preparation.json` vérifie les hashes des 27 originaux de production.
La compilation de test du registre corrigé est lancée séparément sous 69518 ;
elle ne qualifie ni le crate complet, ni le rendu.

Le callback explicite de `LibraryControls` libère maintenant son `busy` avec
`try_set` avant de vérifier le ticket, et affiche une interruption relançable
si ce ticket est remplacé. `try_set` permet aussi la disparition du panneau
pendant l'attente. Cette correction de production est limitée au contrôle UI :
elle ne publie aucun résultat ancien et ne prouve pas la cause de l'expiration.
Le check réel du crate Studio web est lancé sous 24461, avec ses sources épinglées
dans `studio-library-release-check-20261006-v1`. Le candidat Studio précédemment
testé ne contient pas cette correction ; un nouveau rendu et cycle restent requis.

Le test isolé du registre sous 69518 échoue avant compilation à cause du harnais :
la macro Bevy `Resource` exige `CARGO_MANIFEST_DIR`. V1 reste conservée.
`VerifyRegistryV2.py` fournit le contexte Cargo Engine réel et son hash, puis
relance compilation et quatre tests existants sous 42940, sans remplacer le
crate ou modifier les sources utilisées par le build natif 19763.

Le processus 42940 termine avec code 0. V2 compile le module de registre isolé
et exécute ses quatre tests existants : réimport idempotent/refus de mutation,
batch invalide sans insertion partielle, namespace natif/privé et collision
UUID/full-SHA. Quatre PASS, zéro filtré, 0,02 s d'exécution ; sources et libs
précompilées épinglées restent inchangées. Cette preuve porte sur le module
préparé, avec dépendances Bevy/Core précompilées et contexte Cargo du dépôt.
La correction du registre n'est pas encore appliquée à la production et le
Clippy Engine global n'est pas rejoué.
