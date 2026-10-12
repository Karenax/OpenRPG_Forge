# Tidewater â€” suivi d'intÃ©gration complÃ¨te

## Instruction propriétaire du 10 octobre 2026 — suppression du seuil mémoire de lancement

Le propriétaire demande explicitement de supprimer la limite de 16 Gio de marge mémoire avant lancement. Cette instruction remplace les anciennes consignes de garde à 16 Gio pour la poursuite de ce chantier. Les scripts actifs ne refusent plus le lancement selon ce seuil ; les mesures mémoire restent informatives. Les preuves historiques et leurs anciens refus restent conservés.


Objectif propriÃ©taire du 24 septembre 2026 : mÃªme carte et mÃªmes effets visuels,
avec assets rÃ©utilisables, Ã©dition Studio et fonctions du plan approuvÃ©.
DÃ©cision : [ADR-0017](../ADR/ADR-0017-tidewater-fidelity-integration.md).
Ce fichier suit ce chantier ; `PROJECT_CONTEXT.md` reste le contexte canonique.

## Exigence propriétaire du 8 octobre 2026 — moteur générique

Objectif enrichi, avec toute la portée précédente conservée :

> Intégrer Tidewater dans OpenRPG Forge Rust/Bevy pour obtenir la même carte complète et les mêmes effets visuels que la référence figée, avec assets réutilisables et carte éditable dans Studio, comportements prévus au plan, validation native/WebGPU et comparaisons visuelles documentées. Préserver le travail existant et ne pas publier implicitement. Les éléments liés au moteur doivent être intégrés aux systèmes communs et utilisables par les futures cartes. Leurs noms de modules, fichiers, types, API, shaders, paramètres et contrats moteur ne doivent pas contenir d’indicatif Tidewater. Les références Tidewater sont réservées au contenu de cette carte, aux acquisitions, aux scènes de référence, aux preuves et aux mentions de provenance/licence nécessaires.

Critères ajoutés : intégrer réellement les fonctions réutilisables aux systèmes et réglages communs ; retirer les indicatifs Tidewater des noms et contrats moteur ; supprimer toute dépendance au nom/slug/assets de la carte ; vérifier leur fonctionnement sur une autre carte ou scène indépendante, sur les parcours natif/WebGPU concernés. Un simple renommage ne suffit pas. Préserver le contenu spécifique, les licences et les preuves historiques lors des déplacements et renommages.

Cette exigence reste à réaliser pour les modules actuels `tidewater_*`. L’objectif reste en pause à la demande du propriétaire ; cet amendement ne relance ni développement ni tests.

Reprise du 10 octobre 2026 : l’état de gestion de l’objectif est de nouveau ACTIVE. La pause du 8 octobre ci-dessus est historique ; la poursuite conserve intégralement l’exigence de moteur générique. Aucun statut de qualification supplémentaire n’est acquis.


Ã‰tat complÃ©mentaire du 5 octobre â€” dÃ©bris : les 805 objets procÃ©duraux et
141 objets scannÃ©s sont raccordÃ©s au probe natif par des racines communes
ManualPlacementProjection. Le chargement des atlas acquis conserve leurs
douze mips, et les quatre prototypes scannÃ©s proches alimentent le graphe
d'ombres partagÃ©. Le lancement natif et les captures rÃ©ussissent ; un
comparatif avec/sans scans confirme leur contribution aux trois cartes de
profondeur (7 502 / 425 / 10 pixels plus proches, aucun plus lointain).
Ce comparatif ne qualifie pas les diffÃ©rences RGB temporelles ni la paritÃ©
Three.js. Le test physique procÃ©dural passe sÃ©parÃ©ment.

Le checker commun reÃ§oit dÃ©sormais l'ensemble des 946 placements. AprÃ¨s
correction du recalcul excessif des cascades par les fades scannÃ©s, le replay
native-debris-walk-20261005-v3 passe : dÃ©placement 8,996 m, 319 ticks au sol,
functional_pass et cadence_pass vrais, cadence 1/2/4 acquise sur ce parcours.
La capture est inspectÃ©e ; elle ne qualifie pas toutes les collisions de
la carte ni la paritÃ© Three.js. Le candidat portable debris-002 est construit
avec le launcher Aube actuel ; son ZIP Ã  racine plate passe l'extraction et
la vÃ©rification des 7 651 fichiers. Le runtime extrait passe ensuite le
parcours court (8,996 m, 325 ticks au sol, cadence des ombres acquise).
Le clic rÃ©el Â« Jouer Â» de ce nouveau launcher et une autre machine restent
Ã  tester. L'Ã©dition/persistance Studio des dÃ©bris et la couverture complÃ¨te
de la carte restent ouvertes.
[Preuves dÃ©taillÃ©es et captures](../evidence/tidewater/2026-10-05-debris-scan-atlases.md),
[test physique procÃ©dural](../evidence/tidewater/2026-10-05-debris-native-adoption.md).

L'entrÃ©e procÃ©durale complÃ¨te dispose aussi d'un lecteur Assets commun,
synchrone/asynchrone, qui vÃ©rifie le descripteur, les 4 909 payloads,
les collisions et Nature avant publication. Deux tests sur fichiers acquis
passent, dont refus d'un shader altÃ©rÃ© ; le bootstrap natif est raccordÃ©
dans le code. Le loader Studio consomme l'entrÃ©e via un pin optionnel et
publie le lot vÃ©rifiÃ© avec la bibliothÃ¨que ; son staging sÃ©parÃ© rÃ©ussit et
le lecteur Rust y repasse ses deux tests. Le raccord actif au catalogue,
aux placements communs, au viewport, aux gizmos et aux ombres compile pour
WASM (session 71248). Un test acquis valide l'historique et la rÃ©ouverture
sur trois surfaces, avec suppressions conservÃ©es (session 98527). La liaison
du frontend, le dessin/picking et les gestes d'Ã©dition Studio restent Ã 
vÃ©rifier ; ces preuves CPU et de compilation ne qualifient pas l'Ã©dition UI.
Le frontend V2 termine ensuite avec succÃ¨s (38360), ses sources figÃ©es
intactes ; l'assemblage des 7 930 fichiers et le contrÃ´le HTTP des 4 910
fichiers procÃ©duraux passent. Le constructeur canonique valide les entrÃ©es
du candidat natif ; son build Tauri est lancÃ© (56358). Le runtime Studio
et ses gestes restent Ã  qualifier.
[Raccord et preuves](../evidence/tidewater/2026-10-05-debris-shared-input.md).

Les scans disposent ensuite d'une acquisition commune package/programme/
gÃ©omÃ©trie/collisions : deux tests acquis et Clippy/check WASM Assets passent.
Le probe natif utilise ce type dans le code ; son check passe (session 79853).
Le raccord et la qualification Studio des 141 variations restent ouverts.
[Preuves et pÃ©rimÃ¨tre](../evidence/tidewater/2026-10-05-scanned-shared-input.md).

La conversion des 141 variations en assets et poses communes est ensuite
partagÃ©e dans Assets et rÃ©utilisÃ©e par le raccord natif. Un scÃ©nario acquis
sur le binding Studio et les commandes projet couvre les quatre prototypes ;
son exÃ©cution passe (72408 : un test, quatre familles, historique et
rÃ©ouverture, aucune UI/GPU). Les contrÃ´les de compilation associÃ©s sont en cours.
[Suivi et limites](../evidence/tidewater/2026-10-05-scanned-common-authoring.md).

Les petits cailloux suivant la camÃ©ra restent une couche distincte. La
rÃ©fÃ©rence CPU de leur sÃ©lection est acquise et rÃ©pÃ©tÃ©e Ã  l'identique sur
150 poses source, avec hauteurs finales du village vÃ©rifiÃ©es et contrÃ´les
indÃ©pendants de rayon/frustum. Le sÃ©lecteur Engine Rust passe ensuite la
comparaison exacte des 150 poses (2 553 cellules), le cache/refus d'entrÃ©es
invalides et Clippy standalone strict. Son raccord runtime et son rendu
restent ouverts ; le check WASM est celui du module, sans liaison Studio.
Le dÃ©codeur du patch passe Ã©galement sur les buffers acquis : 2 864 sommets,
4 704 triangles et masque conservÃ©s octet pour octet, six refus d'entrÃ©es
invalides, mÃ©tadonnÃ©es WASM et Clippy standalone strict. L'adapter package,
le mesh, le matÃ©riau et les cellules GPU ne sont pas encore raccordÃ©s.
Le raccord au paquet vÃ©rifiÃ© passe ensuite un test natif sur les trois vrais
modules et le lecteur Assets dÃ©jÃ  construit : patch complet, 150 poses,
2 553 cellules, refus d'origine NaN et de masque altÃ©rÃ© ; Clippy standalone
strict passe. Ce contrÃ´le ne reconstruit pas Engine ni le runtime ; le
raccord graphique reste ouvert.
[Preuve et portÃ©e](../evidence/tidewater/2026-10-06-pebble-cell-reference.md).

Le programme graphique acquis des cailloux est ensuite prÃ©parÃ© avec les
calculs vertex/surface source conservÃ©s. Sa construction de pipeline WebGPU
passe sans diagnostic, avec imports Bevy camÃ©ra/mesh explicitement stubÃ©s et
sans dessin. L'extension de matÃ©riau partageant les rÃ©cepteurs de scÃ¨ne est
ajoutÃ©e et formatÃ©e ; sa compilation complÃ¨te, les vrais bind groups, le mesh,
la publication des cellules et le rendu runtime/Studio restent Ã  vÃ©rifier.
[Programme, preuve et limites](../evidence/tidewater/2026-10-06-pebble-shader.md).
La construction des ressources Bevy du patch et de la densitÃ© passe ensuite sur
les donnÃ©es acquises (un test, 0,31 s, octets et sampler source conservÃ©s, sans
dessin ni reconstruction complÃ¨te d'Engine). L'adapter de publication des
cellules GPU et le lecteur Assets sync/async du programme sont ajoutÃ©s ; leur
compilation/qualification, la chaÃ®ne d'Ã©clairage adaptÃ©e et leur adoption dans
le runtime et Studio restent en cours ou ouvertes.



Suite du 2 octobre : la prÃ©paration des lumiÃ¨res par sÃ©lection explicite d'ID
permet des instances indÃ©pendantes sans exiger les ancrages des autres assets.
Les 13 tests Engine ciblÃ©s passent, dont dÃ©placement, copie, remplacement et
retrait ; le candidat selection-v2 passe Clippy sans diagnostic possÃ©dÃ© et la
compilation WASM. Deux protections de cycle de vie issues de revue sont ajoutÃ©es
au candidat selection-v3, dont les 14 tests, Clippy ciblÃ© et compilation WASM
passent. L'index Assets d'identitÃ©s source ownership-v1 est ajoutÃ© : ses 18 tests
ciblÃ©s passent et son lecteur valide les 60 records du paquet rÃ©el, dont 39
lumiÃ¨res sÃ©lectionnables via 24 clÃ©s source explicites. Le candidat ownership-v2
corrige un commentaire et passe Clippy sans diagnostic ainsi que WASM ; les 21
autres identitÃ©s de catalogue restent Ã  dÃ©clarer explicitement.
Le raccord Engine des racines de bÃ¢timents est ajoutÃ© dans building-bindings-v2
et passe les 15 tests Engine ciblÃ©s, Clippy sans diagnostic possÃ©dÃ© et la
compilation WASM. Il vÃ©rifie commit, rÃ©vision, repÃ¨re et racine canonique de
chaque placement. La revue relÃ¨ve ensuite un risque de racine ancienne ; v3
rÃ©sout la racine depuis la scÃ¨ne courante et refuse parents et doublons. La sonde
native sur les deux vrais paquets passe pour 32 lampes de 19 bÃ¢timents, y compris
copie et retrait de l'original, phases inchangÃ©es et erreur de pose maximale de
0,0031 mm. V4 corrige l'emprunt d'un argument de la sonde et passe Clippy sans
diagnostic possÃ©dÃ©, build/exÃ©cution ECS et compilation WASM ; sa garde finale
930 fichiers figÃ©s/douze fichiers possÃ©dÃ©s passe. Sa consommation par Studio,
les vrais meshes et l'attestation explicite des quatre bÃ¢timents sans Ã©metteur
observÃ© restent ouverts.
Le raccord catalogue/placement/ancre dans Studio, la publication jour/nuit et
les consommateurs shader restent ouverts. Cette preuve CPU ne qualifie aucun
nouveau rendu visible. [Preuves](../evidence/tidewater/2026-10-02-source-local-lights.md).

Suite : le rÃ©cepteur de lampes locales est ajoutÃ© au shader partagÃ© des bÃ¢timents,
avec uniforme de 448 octets, diffuse/GGX, cÃ´nes, portÃ©e et absorption sous l'eau.
Il reste neutre tant que la scÃ¨ne ne publie pas son paquet camÃ©ra. La validation
material-lights-v1 Ã©choue sur une ambiguÃ¯tÃ© de type dans le test encase ; v2
prÃ©cise le type des octets, puis v3 corrige trois closures redondantes relevÃ©es
par Clippy. V3 passe les 17 tests ciblÃ©s, Clippy sans diagnostic possÃ©dÃ© et le
check WASM, avec garde des 932 fichiers figÃ©s/seize fichiers possÃ©dÃ©s. Cette
Ã©tape ne prouve pas un rendu GPU ni une lumiÃ¨re visible en Studio.

Suite : l'horloge solaire source est portÃ©e dans `tidewater_sky::solar`, avec
48 rÃ©sultats acquis par exÃ©cution des helpers source figÃ©s (soleil/lune/key/night).
Les deux tests de concordance et cycle de vie passent, ainsi que Clippy sans
diagnostic possÃ©dÃ© et le check WASM ; garde finale 934 fichiers/dix-neuf overlays
PASS. La valeur de nuit est calculÃ©e selon la source, mais sa publication dans
Studio/Runtime et sa synchronisation avec LUTs, ombres, ciel et lampes restent
Ã  rÃ©aliser. Aucun nouveau cycle jour/nuit visible n'est qualifiÃ©.

Suite du 4 octobre : le raccord Studio publie dÃ©sormais les paramÃ¨tres solaires
vers le contrÃ´leur atmosphÃ©rique, puis utilise son dernier retour GPU terminÃ©
pour les nuages, l'environnement et l'optique d'eau. Le soleil reste Ã  l'heure
source initiale 16.2, en pause ; aucun contrÃ´le de cycle jour/nuit n'est ajoutÃ©.
Trois tests CPU passent, dont les 48 cas source et l'Ã©galitÃ© bit Ã  bit du soleil
initial avec le paquet de la carte. Les vÃ©rifications WASM passent, mais le
premier build liÃ© a Ã©chouÃ© par manque de mÃ©moire LLVM. La reprise rÃ©ussit le
lien frontend, le worker et le build natif sur les mÃªmes sources figÃ©es, avec
seize unitÃ©s de compilation pour Engine et Studio. Les expirations de lecture
observÃ©es au premier lancement ne sont pas reproduites dans les copies fraÃ®ches :
la bibliothÃ¨que complÃ¨te et les collisions du terrain y sont acquises. Le
diagnostic rÃ©vÃ¨le toutefois des ombres perpÃ©tuellement restagÃ©es, sans frame
d'animation encodÃ©e dans l'essai prolongÃ©. Le passage en jeu est en diagnostic.
Le dessin visible, la cadence des retours atmosphÃ©riques, les ombres et
la paritÃ© restent ouverts. [Preuves et reprise](../evidence/tidewater/2026-10-03-solar-environment.md).

Suite : une collecte de lampes liÃ©e Ã  l'identitÃ© Main de camÃ©ra est ajoutÃ©e et
peut Ãªtre extraite vers RenderWorld. La conversion matÃ©riau vÃ©rifie aussi le
tag complet de frame attendu. Les 18 tests ciblÃ©s passent, dont refus d'une
camÃ©ra de mÃªme pose, d'un serial diffÃ©rent et d'un inventaire incomplet, puis
rÃ©cupÃ©ration et rÃ©activation de la torche. Collection-v1 passe le check WASM ;
trois avertissements de documentation Clippy sont corrigÃ©s dans v2, en
validation. RÃ©sultat terminal v2 : Clippy sans diagnostic possÃ©dÃ©, check WASM
et garde finale 935 fichiers/vingt-et-un overlays PASS ; le texte fonctionnel
est identique Ã  v1, dont les 18 tests sont conservÃ©s. La collecte et le transfert
GPU ne sont pas encore installÃ©s dans
Studio/Runtime. Cette Ã©tape ne qualifie aucun nouvel Ã©clairage visible.

Suite du 3 octobre : la liaison 54 des bÃ¢timents utilise maintenant un buffer
storage partagÃ©, avec les mÃªmes 448 octets source. Un transport RenderWorld
extrait la collecte, vÃ©rifie MainEntity/tag/serial et Ã©crit via la queue Bevy
existante aprÃ¨s PrepareAssets. Les refus de frame remettent le paquet neutre.
Le candidat material-upload-v2 passe 19 tests ciblÃ©s, Clippy sans diagnostic
possÃ©dÃ©, check WASM et garde finale 936 fichiers/vingt-deux overlays. Il n'est
pas encore installÃ© dans Studio/Runtime ; aucune lecture GPU ou image de
bÃ¢timent Ã©clairÃ© n'est qualifiÃ©e par ces contrÃ´les de compilation et CPU.
[Contrat et preuves](../evidence/tidewater/2026-10-03-source-light-material-upload.md).

## RÃ©fÃ©rence

- Source `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
- Le `main` public observÃ© le 29 septembre est Ã 
  `4811ba48d795197de5621985f404e765c0b7c0ef` : les oracles et le paquet
  021, 023 et 024 restent Ã©pinglÃ©s Ã  `1438b1ab`. Requalifier les changements visuels
  amont avant d'affirmer une fidÃ©litÃ© Ã  la dÃ©mo actuellement publiÃ©e.
  [Audit des Ã©carts rÃ©cents](../evidence/tidewater/2026-09-29-live-source-visual-delta.md).
- DonnÃ©es locales : `E:/OpenRPG_Forge/.tidewater-integration/`.
- `upstream/` : clone intact ; `reference/` : clone de travail de rÃ©fÃ©rence.
- Patch suivi prÃ©existant et inventaire de dÃ©part conservÃ©s hors dÃ©pÃ´t.
- Forge : branche Rust existante, travail modifiÃ© conservÃ©, aucune publication.

**Erratum d'acquisition :** les six PNG initiaux de `baseline/` rÃ©pÃ¨tent
exactement leur contenu Ã  1280 px horizontalement et 720 px verticalement.
Ils sont conservÃ©s comme traces mais exclus des comparaisons finales de scÃ¨ne.
Une nouvelle acquisition PNG sans perte `baseline-003/` est inspectÃ©e et indexÃ©e ;
le contrÃ´le d'indexation rejette dÃ©sormais ce dÃ©faut. `baseline-002/` est aussi
exclu : JPEG renvoyÃ©s par l'API gÃ©nÃ©rique malgrÃ© les extensions PNG.
[ProcÃ©dure et preuves](../evidence/tidewater/2026-09-24-baseline-reacquisition.md).
Les oracles directs terrain/vÃ©gÃ©tation/village sont sÃ©parÃ©s.

## CritÃ¨res et Ã©tat (aucune paritÃ© acquise Ã  ce stade)

Suite du 2 octobre : le [lecteur Rust des shaders du bateau](../evidence/tidewater/2026-10-02-boat-shader-loader.md)
charge les sept matÃ©riaux, vingt programmes source et huit hooks du paquet 350.
Les 18 blocs/188 sites d'uniformes correspondent aux octets du packer source ;
tests natifs, Clippy, check WASM et transport aprÃ¨s dÃ©placement passent localement.
Les ressources et le dessin Engine/Studio, les contrÃ´leurs et la paritÃ© restent
ouverts ; ces preuves CPU ne clÃ´turent aucun critÃ¨re visuel.

Suite : [prÃ©paration des pipelines dans Bevy](../evidence/tidewater/2026-10-02-boat-bevy-pipelines.md).
Les vingt WGSL existants passent la crÃ©ation GPU native avec politiques SDK
dÃ©rivÃ©es et reÃ§u explicite ; la vraie variante tardive des piÃ¨ges reste Ã 
acquÃ©rir. Cette prÃ©paration ne dessine pas le bateau et ne clÃ´ture aucun
critÃ¨re de paritÃ©, de gameplay ou d'Ã©dition.

Suite : [paquet des lumiÃ¨res et projection gÃ©nÃ©rique](../evidence/tidewater/2026-10-02-source-local-lights.md).
Le paquet dÃ©placÃ© ferme 60 lumiÃ¨res, 212 sources et 214 fichiers sans clone
upstream ni Node au chargement. Quinze tests assets et neuf tests Engine,
transfert GPU exact sur douze scÃ©narios, Clippy ciblÃ© et compilation WASM passent.
Le parent de chaque objet Ã©ditable, les contrÃ´les temps/nuit, les consommateurs
des shaders et le dessin complet restent Ã  raccorder. La configuration sans
features demeure en Ã©chec ; ces preuves ne clÃ´turent aucun critÃ¨re de paritÃ©.
L'acquisition suivante conserve onze pivots de lanternes liÃ©s par identitÃ© de
vecteur, soit 19 parents source identifiÃ©s et 41 non rÃ©solus. Le nouveau paquet
passe le lecteur Rust existant et le contrÃ´le indÃ©pendant des phases/poses ;
la liaison au catalogue et aux instances Studio reste Ã  rÃ©aliser.
L'observation Ã  l'Ã©mission rattache ensuite 32 lampes de bÃ¢timents Ã  leurs vrais
repÃ¨res de construction, avec digest de Village original/observÃ© identique et
lecture Rust du paquet rÃ©ussie. Couverture source courante : 51 sur 60, neuf
bindings non rÃ©solus ; aucune qualification de dessin n'en est dÃ©duite.
La couverture source atteint ensuite 60/60 : sept lampes de passerelles et
accessoires sont observÃ©es Ã  leur gÃ©nÃ©ration, les deux repÃ¨res vendeurs viennent
de blocs de dÃ©claration source allowlistÃ©s, sans leurs chargements d'assets.
Le paquet courant et ses 60 phases/poses passent la lecture Rust et le contrÃ´le
indÃ©pendant ; les instances Studio et le rendu visible restent Ã  raccorder.

| ID | Livraison | CritÃ¨re dÃ©cisif | Ã‰tat |
|---|---|---|---|
| TW-001 | Acquisition et licences | source exacte, inventaire/hash par fichier, notices | en cours |
| TW-002 | Baseline visuelle | vues et parcours appariÃ©s, paramÃ¨tres enregistrÃ©s | en cours â€” camÃ©ra village appariÃ©e ; ancien bundle Ã  mÃ©tÃ©o couverte identifiÃ© et remplacÃ© par une acquisition normale ; exposition, nettetÃ© et FXAA comparÃ©s sur la chaÃ®ne graphique complÃ¨te ; paire village avec post-traitement et adaptation actifs : luminance globale source/native 109,23/109,62 mais dÃ©tails/ombres encore distincts ([preuve](../evidence/tidewater/2026-09-29-village-postfx-pair.md)) ; brume native rapprochant la forÃªt et les reliefs de la source sans Ã©claircir fortement le sol proche ([preuve](../evidence/tidewater/2026-09-28-air-haze.md)) ; temporalitÃ© et autres effets encore diffÃ©rents |
| TW-003 | Export complet | terrain aprÃ¨s village, masques, instances, prototypes, matÃ©riaux | en cours |
| TW-004 | Prototype arbre | mÃªme silhouette, matÃ©riaux/vent/LOD, pose Studio, save/open | en cours â€” palmier/feuillu/arbuste Bevy, vent continu partagÃ© avec les ombres dans la promenade ; copie/pose persistante avec rotation et Ã©chelle propres raccordÃ©e Ã  Nature et prouvÃ©e en projection native, geste GUI et paritÃ© animÃ©e ouverts |
| TW-005 | Sources de carte | blocs de hauteur et instances stables, migrations, commandes | en cours â€” terrain v4, vÃ©gÃ©tation v2 (65383 instances), exclusions/densitÃ© et magasin des sources workspace testÃ©s ; raccordement Studio, autres corrections et export portable ouverts |
| TW-006 | ÃŽle fidÃ¨le | terrain, village, rÃ©cif, dÃ©cors, positions et collisions | en cours â€” terrain adaptatif, village, six groupes de vÃ©gÃ©tation et herbe suivant la camÃ©ra ; trajet intÃ©rieur de 160 m validÃ© ; Ã©clairage/effets/dÃ©cors incomplets |
| TW-007 | OcÃ©an | FFT, houle, Ã©cume, vagues, ressac, embruns, sillages | en cours â€” FFT, ShoreWaves, ShoreSim/SurfFoam et sable humide comparÃ©s ; boucle FFT/rivage/embruns continue avec delta rÃ©el commun, vitesse rÃ©elle vÃ©rifiÃ©e Ã  143 et 30 FPS ; Ã©chelle temporelle portÃ©e Ã  `1,5Ã—` et qualifiÃ©e sur promenade et nage natives ; Ã©mission suivant la camÃ©ra ; Ã©carts locaux, parcours complet, sillages et paritÃ© dynamique ouverts |
| TW-008 | Optique eau | rÃ©fraction, rÃ©flexion, caustiques, vue mixte, immersion, gouttes | en cours â€” matÃ©riau de surface comparÃ©, immersion complÃ¨te Beer-Lambert/diffusion soleil-ciel et vue mixte livrÃ©s ; masque de la vraie surface visible transportÃ© dans l'alpha HDR et preuve A/B native sur trois poses ; repli de hauteur FFT par point terminal raccordÃ© au matÃ©riau d'eau et parcours natif validÃ© ; cible RG sÃ©parÃ©e, caustiques, gouttes, chaÃ®ne finale, WebGPU et paritÃ© totale restent ouverts |
| TW-009 | AtmosphÃ¨re | soleil/lune/Ã©toiles, nuages/ombres, brume, rayons | en cours â€” ciel diurne, volume nuageux, reflets et ombres reÃ§ues comparÃ©s ; continuation des nuages/ombres suivant la camÃ©ra dans la promenade native, historique GPU conservÃ© et essai 30 s validÃ© ; perspective aÃ©rienne native rÃ©glable et A/B village qualifiÃ© localement ([preuve](../evidence/tidewater/2026-09-28-air-haze.md)) ; nuit, rayons et qualification dynamique complÃ¨te ouverts |
| TW-010 | Rendu final | ombres, lumiÃ¨re indirecte, AO, temporel, bloom, exposition, flou | en cours â€” ombres relief/objets et rÃ©ception PCSS/PCF, vent et cascades suivant la camÃ©ra avec sÃ©lection terrain/plantes, rÃ©ception cohÃ©rente et cadence 1/2/4 par frame main vÃ©rifiÃ©es ; adaptation d'exposition GPU, FXAA et rÃ©glages jouables calibrÃ©s sur le village ([preuve](../evidence/tidewater/2026-09-28-default-weather-graphics.md)) ; carte de lumiÃ¨re rebond source raccordÃ©e au village et rÃ©glable, effet A/B trÃ¨s faible ([preuve](../evidence/tidewater/2026-09-28-ground-bounce.md)) ; flou de mouvement natif rÃ©glable et exercÃ© sur parcours ([preuve](../evidence/tidewater/2026-09-28-motion-blur.md)) ; grain photo natif rÃ©glable sans perturbation de l'exposition ([preuve](../evidence/tidewater/2026-09-28-film-grain.md)) ; brume native rÃ©glable, mesurÃ©e sans saut d'exposition ([preuve](../evidence/tidewater/2026-09-28-air-haze.md)) ; AO Ã©cran GTAO rÃ©glable, 12 Ã©chantillons Ã  demi-rÃ©solution, deux flous bilatÃ©raux et composition sensible Ã  la profondeur, A/B natif Ã  exposition fixe et parcours GPU validÃ©s ([preuve](../evidence/tidewater/2026-09-28-gtao.md)) ; TAA Bevy activable mais expÃ©rimental ([preuve](../evidence/tidewater/2026-09-28-temporal-aa.md)) ; flare solaire natif mesurÃ© A/B ([preuve](../evidence/tidewater/2026-09-28-lens-flare.md)) ; optimisation, filtre temporel AO et paritÃ© HDR du flare, rebond terrain/plantes, TAAU eau, paritÃ© du flou, rendu final et WebGPU ouverts |
| TW-011 | Vivant et sons | vÃ©gÃ©tation animÃ©e, poissons/oiseaux/crabes/baleine, audio | en cours â€” vent continu des plantes et ombres vÃ©rifiÃ© ; faune et audio ouverts |
| TW-012 | Jeu | dÃ©placement/nage, bateau, pÃªche, commerce, progression, sauvegarde | en cours â€” marche, pataugeage, nage, plongÃ©e, remontÃ©e, retour plage et montÃ©e eauâ†’ponton raccordÃ©s au joueur Forge/Rapier avec hauteurs GPU ; comparaison directe de 39 scÃ©narios source et parcours natifs validÃ©s ; descente d'Ã©chelle, sillages, bateau, pÃªche, commerce, progression et sauvegarde ouverts |
| TW-013 | Studio | catalogue complet, objets plaÃ§ables, retouches conservÃ©es, Undo/Redo | en cours â€” sÃ©lection GPU, dÃ©calage, copie, rotation et Ã©chelle de plante importÃ©e dans le document de zone ; catalogue des couches importÃ©es, pose par coordonnÃ©es et clic terrain raccordÃ©s Ã  Nature, archive/Undo/Redo et projection native testÃ©s, y compris le basculement en archive delta ([preuve](../evidence/tidewater/2026-09-29-imported-plant-delta-archive.md)) ; dÃ©placement de la plante sÃ©lectionnÃ©e par clic terrain compilÃ© en WASM ; geste GUI et catalogue gÃ©nÃ©rique complets ouverts |
| TW-014 | Qualification | natif/WebGPU, mÃ©triques, paritÃ© fixe et animÃ©e, revue propriÃ©taire | en cours â€” parcours natifs avec eau/nuages/vent continus, temps rÃ©el vÃ©rifiÃ© Ã  143 FPS et sous limitation 30 FPS, profondeur vÃ©gÃ©tale A/A/B contrÃ´lÃ©e ; exÃ©cution WebGPU en navigateur, benchmark complet, paritÃ© et revue propriÃ©taire Ã  qualifier |
| TW-015 | Autonomie | package local sans clone upstream ni Node, licences agrÃ©gÃ©es | en cours â€” archive Windows 024 avec lanceur Aube, runtime, Ã©choppe source partielle et 15 rÃ©glages graphiques ; CRC ZIP, identitÃ©s des binaires, manifeste et parcours depuis une extraction neuve validÃ©s localement ([preuve](../evidence/tidewater/2026-09-29-windows-playtest-package-024.md)) ; qualification sur autre machine et revue des notices manquantes ouvertes |

Ã‰tat Studio au 30 septembre : le [panneau graphique du Playtest](../evidence/tidewater/2026-09-30-studio-graphics-controls.md)
expose quatorze commandes actives, dont la [lumiÃ¨re rebond source](../evidence/tidewater/2026-09-30-studio-ground-bounce.md)
quand son paquet vÃ©rifiÃ© est disponible, et l'[occlusion et le flare WebGPU](../evidence/tidewater/2026-09-30-studio-gtao-flare.md).
La [brume marine](../evidence/tidewater/2026-09-30-studio-air-haze.md) est aussi rÃ©glable et
vÃ©rifiÃ©e dans le candidat 058 ; l'immersion Studio reste Ã  qualifier.
Le [flou de mouvement avec l'ocÃ©an](../evidence/tidewater/2026-09-30-studio-motion-blur-ocean.md)
est qualifiÃ© en Playtest dans le candidat 066 : 0Â°, 360Â° et reset Ã  180Â°,
avec marche, FFT et ShoreSim actifs. Sa paritÃ© perceptuelle reste ouverte.
Les [rÃ©glages graphiques locaux](../evidence/tidewater/2026-09-30-studio-graphics-persistence.md)
sont restaurÃ©s aprÃ¨s rechargement dans le candidat 067, puis le mÃªme parcours
valide Ã©dition, Playtest, ocÃ©an et marche ; ils ne modifient pas la zone.
L'[ocÃ©an optique](../evidence/tidewater/2026-09-30-studio-ocean-optics-preview.md)
et le [ressac ShoreSim](../evidence/tidewater/2026-09-30-studio-shore-sim.md)
sont maintenant visibles en Playtest WebGPU. Le candidat 050 les active
automatiquement lorsque les trois paquets source vÃ©rifiÃ©s sont prÃ©sents ; son
parcours passe Ã©dition, sauvegarde, rÃ©ouverture et marche avec FFT et ShoreSim
synchronisÃ©s. La fidÃ©litÃ© Ã  la dÃ©mo publique reste ouverte.
Le candidat 052 vÃ©rifie en plus le rÃ©glage du rebond de 0 Ã  2, son reset et
le mÃªme parcours jouable ; l'effet visuel de cette carte fixe est faible.
Le candidat 055 ajoute les passes AO/flare au parcours, sans blocage du
premier tick. Le flare visible reste plus discret que la rÃ©fÃ©rence.
Les [trois passerelles](../evidence/tidewater/2026-09-30-studio-boardwalk-editable-playtest.md)
sont maintenant visibles, jouables et convertibles en placements persistants.
Le [quai source complet](../evidence/tidewater/2026-09-30-pier-source-package.md),
avec arche, enseigne, lanternes et collisions, a Ã©tÃ© extrait deux fois Ã 
l'identique puis conditionnÃ© en asset portable. Son
[import et Ã©dition Studio](../evidence/tidewater/2026-09-30-studio-pier-editable-playtest.md)
passent sauvegarde, rÃ©ouverture, rendu WebGPU et marche Rapier jusqu'Ã  la tÃªte
du quai, sans saut. Un appui dÃ©rivÃ© franchit les marches ; les animations et
la comparaison visuelle appariÃ©e restent ouvertes ; TW-006 n'est pas fermÃ©.
L'[Ã©choppe FishStand](../evidence/tidewater/2026-09-30-studio-fish-stand-source.md)
apparaÃ®t Ã  son repÃ¨re source sans modifier le document de zone ; un bouton la
convertit en placement Ã©ditable. Les deux modes passent sauvegarde, rÃ©ouverture
et marche WebGPU dans le candidat 084. Les accessoires finaux du quai,
l'Ã©clairage du stand et la paritÃ© finale de l'image restent ouverts.

La [nouvelle paire village du candidat 093](../evidence/tidewater/2026-09-30-studio-village-current-pair.md)
montre 13,10 niveaux d'erreur absolue moyenne de luminance Ã  0,40 EV contre
14,02 dans la paire prÃ©cÃ©dente ; le Studio reste plus clair et la temporalitÃ©
n'est pas appariÃ©e. Les [cinq accessoires distincts de la place](../evidence/tidewater/2026-09-30-plaza-source-parts.md)
(deux bancs, un lampadaire, deux tonneaux) sont extraits deux fois avec
triangles, collisions et lumiÃ¨re source vÃ©rifiÃ©s, puis conditionnÃ©s en paquet
portable rÃ©pÃ©table avec lecteur Rust vÃ©rifiÃ©. Leur
[conversion en cinq placements Ã©ditables dans Studio](../evidence/tidewater/2026-09-30-studio-plaza-editable-playtest.md)
passe l'aperÃ§u, la sauvegarde, la rÃ©ouverture et le Playtest WebGPU dans le
candidat 095. La lumiÃ¨re locale du lampadaire, les ombres des arbres, le reste
des accessoires et le TAAU source restent des Ã©carts visuels.

Mise Ã  jour du 29 septembre : TW-002 et TW-014 disposent aussi d'une
[paire cÃ´te/ciel Ã  camÃ©ra fixe](../evidence/tidewater/2026-09-29-seaward-postfx-pair.md)
du source WebGPU et du runtime natif. La luminance globale est 127,14/125,85,
mais le ciel source reste plus clair (117,79/109,21) et de petits dÃ©cors de
plage manquent en natif ; la paritÃ© animÃ©e et la dÃ©mo publique rÃ©cente ne sont
pas qualifiÃ©es. TW-015 dispose du [paquet Windows 024](../evidence/tidewater/2026-09-29-windows-playtest-package-024.md)
dont le ZIP, les manifestes et le parcours depuis une extraction neuve passent
localement (`pass`, cadence et backend stables, 8,994 m). L'[Ã©choppe de Joe](../evidence/tidewater/2026-09-29-fish-stand-bevy-input.md)
apparaÃ®t dÃ©sormais Ã  sa pose source avec gÃ©omÃ©trie et atlases vÃ©rifiÃ©s ; son
matÃ©riau Bevy, Joe, les poissons et les interactions ne sont pas encore Ã 
paritÃ©. La qualification sur une autre machine et les notices de licence
manquantes restent ouvertes.

Studio dispose maintenant d'un [Playtest terrestre de la zone importÃ©e](../evidence/tidewater/2026-09-29-studio-imported-playtest.md) :
chargement automatique des paquets, marche et collision, vue Ã  la premiÃ¨re
personne, sauvegarde/rÃ©ouverture et retour Ã  l'Ã©dition vÃ©rifiÃ©s sur copie isolÃ©e.
Le rendu WebGPU rÃ©utilise l'exposition native et continue le calcul des nuages
avec la camÃ©ra. Six rÃ©glages visuels du Playtest sont maintenant modifiables Ã 
chaud et rÃ©initialisables ; le candidat 014 a validÃ© leur effet sur la camÃ©ra
et le retour au jeu aprÃ¨s rÃ©glage. La comparaison interne montre encore un
village plus contrastÃ© que la rÃ©fÃ©rence ; le ciel uniforme provenait du paquet
mÃ©tÃ©o trop couvert et a Ã©tÃ© corrigÃ© dans la candidate 015. Eau, effets
complÃ©mentaires, paritÃ© visuelle et qualification du parcours entier dans
Studio restent ouverts.

Le [transport des entrÃ©es FFT ocÃ©aniques dans Studio](../evidence/tidewater/2026-09-29-studio-ocean-input-transport.md)
est maintenant vÃ©rifiÃ© : paquet source hashÃ©, contrÃ´le des mÃ©tadonnÃ©es internes,
chargement WebGPU et parcours Ã©diter/sauvegarder/jouer sur la candidate 016.
Ce candidat 016 ne calcule ni n'affiche encore l'eau dans le viewport ; le port
du graphe GPU natif et du matÃ©riau optique reste Ã  faire. La correction de la mÃ©tÃ©o
normale dans Studio est documentÃ©e [sÃ©parÃ©ment](../evidence/tidewater/2026-09-29-studio-clear-weather.md).

Le [Playtest Studio calcule maintenant la FFT ocÃ©an vivante](../evidence/tidewater/2026-09-30-studio-live-ocean-fft.md)
sur le renderer WebGPU existant. Le candidat 021 et deux parcours isolÃ©s
confirment le bootstrap Ã  120 pas et une progression pendant la marche, sans
erreur navigateur capturÃ©e. Les textures ne sont pas encore raccordÃ©es Ã  un
maillage ni au matÃ©riau optique : eau visible, rive, nage et comparaison
animÃ©e restent ouvertes.

Les [ressources source de la surface d'eau](../evidence/tidewater/2026-09-30-studio-water-surface-package.md)
sont dÃ©sormais empaquetÃ©es, hashÃ©es et chargÃ©es par Studio avant publication
de la bibliothÃ¨que importÃ©e. Le candidat 023 valide ce transport dans un
parcours jouable avec la FFT continue. Le maillage et le matÃ©riau optique ne
consomment pas encore ces entrÃ©es ; le rendu de l'eau reste ouvert.

## Ordre d'exÃ©cution

PrioritÃ© du propriÃ©taire du 27 septembre : pouvoir parcourir la carte sans
attendre l'Ã©dition individuelle des arbres. Le lanceur local
`scripts/tidewater/Jouer-Tidewater.cmd` ouvre dÃ©sormais le mode promenade du
prototype Rust. Parcours automatisÃ© de 14,9 m, contact au sol, camÃ©ra et
actualisation CDLOD validÃ©s ; FFT/rivage/embruns progressent au-delÃ  des 120 pas
de rÃ©fÃ©rence. Les ressources GPU fixes des embruns sont conservÃ©es et leur
Ã©mission suit la camÃ©ra du joueur. Ceci ne qualifie ni le jeu complet, ni une
distribution Runtime, ni la paritÃ© visuelle. Les parcours clavier/souris,
la cadence sur toute l'Ã®le, les autres effets mobiles, l'Ã©dition et le packaging
restent ouverts. [Preuves et limites](../evidence/tidewater/2026-09-27-walk-integration.md).

Le feedback GPU du budget est dÃ©sormais asynchrone en promenade, avec numÃ©ro
de pas associÃ© Ã  chaque compteur. L'essai natif de 30 s termine sans perte du
contact au sol ; cadence de simulation observÃ©e 59,98 pas/s et Ã©tats GPU finis
aprÃ¨s 1500 pas. Les mesures d'affichage de cette seule vue sont consignÃ©es dans
la preuve, sans gÃ©nÃ©ralisation Ã  toute la carte ni au viewport WebGPU.

Correction du ralentissement signalÃ© par le propriÃ©taire : le live utilise
un delta rÃ©el bornÃ© commun Ã  l'ocÃ©an, au rivage, aux embruns, aux requÃªtes de
hauteur et aux matÃ©riaux. Le premier rÃ©glage produit Ã  `1,25Ã—` a Ã©tÃ© portÃ© Ã 
`1,5Ã—` aprÃ¨s retour visuel ; le parcours natif relit `1,5`, mesure 117,71 FPS
et 1,52411 seconde simulÃ©e par seconde rÃ©elle. La nage et trois paires A/B
sous-marines passent Ã©galement. Ã€ 30 Hz, l'eau mesure `1,50873Ã—`, mais le
smoke global Ã©choue faute de 120 frames dans sa fenÃªtre de preuve des ombres.
Le vent et ses ombres restent actifs ; un buffer GPU partagÃ© Ã©vite les
reconstructions de matÃ©riaux Ã  chaque image. La paritÃ© animÃ©e complÃ¨te reste
ouverte.
[Premier rÃ©glage eau](../evidence/tidewater/2026-09-27-water-realtime.md),
[rÃ©glage courant](../evidence/tidewater/2026-09-27-water-speed-150.md) et
[preuves vent](../evidence/tidewater/2026-09-27-live-wind.md).

Les cascades d'ombres suivent la camÃ©ra dans la promenade native. Le graphe
conserve les ressources et publie les mÃªmes matrices aux recepteurs terrain,
plantes, bÃ¢timents et eau. L'acquittement d'extraction permet dÃ©sormais une
soumission par frame main : 3514/3514 au nominal et 830/830 Ã  30 Hz, avec
pÃ©riodes 1/2/4 et aucune rÃ©cupÃ©ration. Cinq lectures profondeur/recepteurs/trace
GPU qualifient les masques et le suivi spatial. L'eau garde sa vitesse rÃ©elle
aux deux frÃ©quences. Le coÃ»t augmente : 120,36 FPS contre 143,72 auparavant,
p95 14,22 ms et 1% low 34,88 FPS ; les frames lentes restent Ã  optimiser.
La paritÃ© animÃ©e complÃ¨te reste ouverte. [Preuves spatiales](../evidence/tidewater/2026-09-27-live-cascades.md)
et [cadence et coÃ»ts mesurÃ©s](../evidence/tidewater/2026-09-27-shadow-cadence.md).

L'herbe suit dÃ©sormais la camÃ©ra dans la promenade : grille source complÃ¨te,
trois gÃ©omÃ©tries et buffers rÃ©utilisÃ©s, sÃ©lection identique Ã  JavaScript sur
19 vues CPU. Le trajet intÃ©rieur de 160 m atteint ses cinq points, avec 629
mises Ã  jour d'herbe, vingt Ã©chantillons spatiaux et trois lectures GPU
conformes. L'eau conserve sa vitesse rÃ©elle : ratio 1,001075. Moyenne de
95,32 FPS en intÃ©rieur, p95 31,48 ms et 1% low 24,97 FPS ; les frames lentes
restent ouvertes. Le replay statique reste identique octet par octet.
Ã‰dition Studio et recalcul de cette grille aprÃ¨s retouche ne sont pas acquis.
[Preuves herbe et navigation](../evidence/tidewater/2026-09-27-live-grass.md).

Le calcul GPU des hauteurs d'eau est raccordÃ© aux positions camÃ©ra/joueur,
avec pas, temps et positions conservÃ©s dans les lectures asynchrones. Sur
vingt points au pas 120, l'Ã©cart maximal avec la source est de 0,0361 mm
en hauteur ; les sorties ne sont pas toutes identiques bit Ã  bit. La
promenade reÃ§oit les nouveaux rÃ©sultats sans altÃ©rer le replay statique.
[Preuves des requÃªtes](../evidence/tidewater/2026-09-27-water-queries.md).

La locomotion de `Player.js` est maintenant portÃ©e et raccordÃ©e au KCC Rapier :
marche, pataugeage, saut, entrÃ©e en nage, flottement, plongÃ©e, remontÃ©e et sortie
sur la plage. L'oracle direct couvre 39 scÃ©narios / 1 110 frames sans dÃ©saccord
de mode/contact/flottement ; les rÃ©sidus numÃ©riques maximaux restent infÃ©rieurs
Ã  2,1e-6 sur les grandeurs comparÃ©es. Le parcours natif consomme la surface GPU,
change la capsule sans remplacer le backend, plonge, remonte et revient Ã  pied.
La sortie d'eau par l'Ã©chelle du ponton est aussi jouable : approche depuis
l'extÃ©rieur du rayon, montÃ©e, changement de capsule et appui sur le pont passent
sur le joueur Forge/Rapier avec l'eau GPU. La descente, les proxies non solides,
la persistance Studio, les sillages, l'optique sous-marine finale et les autres
comportements de jeu restent ouverts.
[Preuves de nage](../evidence/tidewater/2026-09-27-player-swimming.md).
[Preuves de l'Ã©chelle](../evidence/tidewater/2026-09-27-pier-ladder.md).

Une passe plein Ã©cran Bevy/WGSL applique maintenant Beer-Lambert et la
diffusion analytique soleil/ciel en immersion, ainsi qu'une ligne d'eau mixte
avec mÃ©nisque. La vraie surface visible renseigne le milieu pixel par pixel ;
le repli sans surface visible interroge maintenant les quatre cascades FFT et
les vagues de rivage au point terminal. Une preuve A/B dans le mÃªme processus
gÃ¨le camÃ©ra, projection et toutes les horloges :
le flottement entiÃ¨rement cÃ´tÃ© air reste identique, la traversÃ©e Ã  1,08 cm de
la surface change la partie immergÃ©e et l'immersion complÃ¨te change toute
l'image. Une seconde preuve A/B valide le masque de surface visible : le
contrÃ´le air reste identique, la pose Dive modifie 20,017 % des pixels et
l'immersion complÃ¨te toute l'image. Le nouveau repli passe aussi trois paires
A/B en promenade native Ã  `1,5Ã—`. La cible RG sÃ©parÃ©e de la source, la
comparaison du repli avec l'oracle, les caustiques, les effets volumÃ©triques,
les particules, la chaÃ®ne finale et WebGPU restent ouverts.
[Preuves du milieu sous-marin](../evidence/tidewater/2026-09-27-underwater-medium.md).
[Preuves du masque visible](../evidence/tidewater/2026-09-27-underwater-visible-mask.md).
[Preuve du repli FFT](../evidence/tidewater/2026-09-27-underwater-eulerian-height.md).

La gÃ©omÃ©trie des deux cartes de caustiques et le shader de projection des
photons reproduisent les paramÃ¨tres de `Caustics.js` ; Naga valide le WGSL.
Deux cibles flottantes additives et leurs mipmaps sont produites aprÃ¨s la FFT ;
la lecture GPU native montre quatre plans focaux distincts, finis et non
uniformes. Le shader d'Ã©chantillonnage des deux cartes est validÃ© par Naga et
le terrain natif le reÃ§oit dÃ©sormais avec ses entrÃ©es FFT : un A/B Ã  camÃ©ra
fixe montre un changement limitÃ© au fond immergÃ©. La carte d'Ã©clairage des
vagues et mousses du rivage, les autres matÃ©riaux sous-marins et la comparaison
visuelle WebGPU/source restent ouverts.
[Ã‰tape et limites](../evidence/tidewater/2026-09-28-caustic-photon-stage.md).

Nuages : acquisition du systÃ¨me `SkyProClouds` rÃ©ellement activÃ© par dÃ©faut,
560 fichiers reproductibles, 120 blocs temporels et sorties Ã  1/120 ; les 37
sorties d'Ã©clairage prÃ©existantes restent identiques. Calcul Bevy de couverture
1024Â² : cinq texels diffÃ¨rent d'un niveau/255 ; bornes 64Â² identiques et
conservativitÃ© vÃ©rifiÃ©e indÃ©pendamment. Volume, temporalitÃ© et raccordement
scÃ¨ne restent ouverts. [Preuves](../evidence/tidewater/2026-09-25-cloud-weather.md).

ComplÃ©ment : calcul natif des ombres de nuages 256Â² sur 120 pas d'uniformes
acquis, avec bruit 3D original et couverture calculÃ©e dans Bevy. Erreur moyenne
de transmission 2,74e-7, maximale 0,001041 ; rÃ©pÃ©tition exacte. Les nouvelles
entrÃ©es atmosphÃ©riques nÃ©cessaires au volume sont acquises et vÃ©rifiÃ©es.
[Preuves ombres](../evidence/tidewater/2026-09-25-cloud-shadows.md).

Volume : premiÃ¨re passe de marching originale exÃ©cutÃ©e dans Bevy Ã  l'image
120, avec mÃ©tÃ©o/bruit natifs et tables atmosphÃ©riques source appariÃ©es.
RÃ©sultats reproductibles mais Ã©carts locaux : opacitÃ© max 0,021485,
profondeur max 125 m. Reprojection, panorama, atmosphÃ¨re entiÃ¨rement native et
composition scÃ¨ne restent ouverts. [Preuves](../evidence/tidewater/2026-09-25-cloud-volume-trace.md).

Historique : 120 passes marching/reprojection natives depuis des textures vides,
deux paires 960Ã—540 et rÃ©pÃ©tition exacte. L'atmosphÃ¨re reste appariÃ©e Ã  la source.
Ã‰carts finaux maximum : RGB 0,041748, opacitÃ© 0,051758, profondeur historique
0,875 km. Panorama, contrÃ´leur interactif, diagnostic des Ã©carts et raccordement
scÃ¨ne restent ouverts. [Preuves](../evidence/tidewater/2026-09-25-cloud-history.md).

Panorama : la passe originale 512Ã—160 est calculÃ©e nativement depuis zÃ©ro sur
120 Ã©tapes, avec la grille partielle 4Ã—4 originale. RÃ©pÃ©tition exacte, historique
de vue inchangÃ© ; transmission max 0,004395 d'Ã©cart. Les consommateurs ciel,
environnement et eau restent Ã  raccorder, ainsi que l'atmosphÃ¨re native.
[Preuves](../evidence/tidewater/2026-09-25-cloud-panorama.md).

AtmosphÃ¨re raccordÃ©e : les six passes de nuages consomment dÃ©sormais les LUTs
produites par le graphe atmosphÃ©rique Bevy, sur le mÃªme appareil. Deux replays
rÃ©ussissent avec des packs sans aucun rÃ©sultat calculÃ© source. ParamÃ¨tres
temporels/soleil/irradiance encore acquis ; contrÃ´leur et composition scÃ¨ne ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-native-atmosphere.md).

Nuages visibles : graphe partagÃ© raccordÃ© au ciel de la scÃ¨ne Bevy, comparaison
FHD sur la vue Ã®le. RÃ©gion d'effet 91 038 pixels : erreur moyenne 0,1343/255,
P99 1/255, maximum 4/255 ; capture native rÃ©pÃ©tÃ©e identique. Eau, rÃ©ception des
ombres, dynamique et Studio restent ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-visible-sky.md).

Reflets nuageux raccordÃ©s : ciel, ocÃ©an et lÃ¨vre des dÃ©ferlantes partagent le
panorama natif. Vue cÃ´tiÃ¨re avec embruns : rÃ©pÃ©titions identiques, moyenne
0,167551/255 dans la rÃ©gion d'effet, P99 3/255, maximum local 111/255.
Les 21 buffers FFT et les particules actives par identitÃ© sont conservÃ©s ;
le rendu sans nuages reste identique. Ombres reÃ§ues, environnement dynamique,
animation longue et Studio restent ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-water-reflections.md).

Ombres nuageuses reÃ§ues : champ natif partagÃ© avec terrain/CDLOD, village,
vÃ©gÃ©tation/herbe, eau, dÃ©ferlantes et embruns. Cas couvert original Ã  0,85 :
1 300 520 pixels dans la rÃ©gion d'effet, moyenne 0,196812/255, P99 4/255,
maximum 109/255 ; rÃ©pÃ©titions identiques. La pose par dÃ©faut est claire au sol
et ne suffit pas Ã  qualifier cet effet. Environnement dynamique, longues
sÃ©quences, performance et Ã©dition restent ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-shadow-receivers.md).

ContrÃ´leur nuageux natif : Frame/Settings calculÃ©s en Rust, 360 blocs exacts
sur chacune des vues cÃ´tiÃ¨re normale, couverte et Ã®le. SÃ©quence CPU originale
de 32 transitions comparÃ©e ; lÃ©ger rÃ©sidu numÃ©rique du chemin JSON consignÃ©.
Le graphe fonctionne sans fichiers locaux de paramÃ¨tres enregistrÃ©s : dix
textures identiques au replay et image couverte inchangÃ©e. Frame global et
atmosphÃ¨re restent acquis ; boucle GPU interactive et Studio encore ouverts.
[Preuves](../evidence/tidewater/2026-09-25-cloud-native-controller.md).

Historique GPU persistant : 120 pas sur 120 invocations du graphe, ressources
conservÃ©es et dix textures identiques au calcul en lot sur deux essais.
La scÃ¨ne assemblÃ©e retrouve l'image prÃ©cÃ©dente, avec une variation de 14 pixels
(maximum 2/255) sur l'une des rÃ©pÃ©titions. Cette prÃ©paration bornÃ©e ne qualifie
pas encore l'animation interactive illimitÃ©e ni les performances.
[Preuves](../evidence/tidewater/2026-09-25-cloud-stepped-gpu.md).

1. RÃ©fÃ©rence intacte, baseline et export reproductible.
2. Palmier et arbre feuillu complets dans un prototype Forge puis Studio.
3. Contrats d'import et Ã®le complÃ¨te ; fidÃ©litÃ© des donnÃ©es avant approximation.
4. OcÃ©an/cÃ´te puis ambiance complÃ¨te ; comparaison Ã  chaque effet.
5. Faune et jeu ; catalogue et Ã©dition sur les mÃªmes contrats.
6. RÃ©gressions, performances, packaging local et revue finale.

## Dernier incrÃ©ment â€” budget autonome sur retours GPU natifs

Le contrÃ´leur est raccordÃ© au compteur GPU, sans calendrier enregistrÃ© dans
les entrÃ©es. Deux calculs isolÃ©s et deux captures de scÃ¨ne rÃ©pÃ¨tent budgets,
particules et Ã©cume. Les 119 retours utilisÃ©s pour 120 pas sont vÃ©rifiÃ©s.
La capture native reste identique au mode rejouÃ© sur la pose Ã  2 s ; les Ã©carts
avec Tidewater persistent (7 014 particules actives contre 7 013 notamment).
Le protocole attend chaque compteur pour reproduire la latence de la rÃ©fÃ©rence ;
la performance en temps rÃ©el et la paritÃ© animÃ©e restent ouvertes.
[Preuves et limites](../evidence/tidewater/2026-09-25-native-spray-budget.md).

## IncrÃ©ment prÃ©cÃ©dent â€” contrÃ´leur Rust du budget d'Ã©mission

Le calcul adaptatif original est portÃ© en Rust : historique, wrap du compteur,
lissage et bornes. Six tests passent. Sur deux acquisitions de 540 pas, chaque
budget f32 d'Ã©mission et d'aprÃ¨s Ã©mission est identique Ã  la source. Les
compteurs restent des entrÃ©es source appariÃ©es pour cette preuve ; le
raccordement aux readbacks GPU natifs reste Ã  faire et la scÃ¨ne rejoue encore
le calendrier enregistrÃ©.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-budget-controller.md).

## IncrÃ©ment prÃ©cÃ©dent â€” embruns rendus dans la scÃ¨ne native

Les sprites originaux lisent les buffers de simulation natifs ; le ruban,
l'eau et le sable partagent les crÃªtes et l'Ã©tat couplÃ© du rivage. Deux captures
FHD Ã  2 s sont exactement reproductibles. Dans la rÃ©gion affectÃ©e, l'erreur
moyenne source/natif vaut 0,150027/255, avec un maximum local de 41/255.
La scÃ¨ne sans embruns et les Ã©tats numÃ©riques antÃ©rieurs restent inchangÃ©s.
La paritÃ© animÃ©e, le budget adaptatif natif et les performances restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-sprites-native.md).

## IncrÃ©ment prÃ©cÃ©dent â€” rÃ©fÃ©rence visuelle des embruns

Les shaders, hooks et deux textures avec leurs 15 mipmaps sont acquis ; 23
fichiers de rendu se rÃ©pÃ¨tent exactement. La capture source avec embruns est
identique sur trois essais. Le vent global du probe numÃ©rique et celui de la
scÃ¨ne Ã©taient diffÃ©rents ; une acquisition appariÃ©e prouve dÃ©sormais les mÃªmes
7 013 particules source entre calcul et image. Le calcul natif appariÃ© reproduit
le dÃ©pÃ´t final de 282 unitÃ©s, mais conserve des divergences de trajectoires et
d'Ã©cume accumulÃ©e. Le rendu natif des sprites reste Ã  raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-sprite-reference.md).

## IncrÃ©ment prÃ©cÃ©dent â€” retombÃ©es des embruns dans l'Ã©cume

La boucle GPU respecte maintenant FFT â†’ rivage â†’ Ã©mission â†’ particules sur
120 pas. ShoreSim consomme les dÃ©pÃ´ts du pas prÃ©cÃ©dent dans le buffer partagÃ©.
L'effet modifie 5 478 texels d'Ã©cume natifs contre 5 504 source ; les deux essais
natifs se rÃ©pÃ¨tent exactement. Les particules, crÃªtes, compteurs et FFT restent
inchangÃ©s, comme les effets sur l'humiditÃ© et le flux (nuls dans cette pose).
L'Ã©cart local maximal d'Ã©cume vaut encore 0,080811 : la paritÃ© n'est pas acquise.
Rendu des sprites, Ã©tat couplÃ© dans la scÃ¨ne, budget adaptatif natif et Ã©carts
numÃ©riques restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-shore-coupling.md).

## IncrÃ©ment prÃ©cÃ©dent â€” historique natif des embruns

Le FFT, l'Ã©mission et le dÃ©placement s'enchaÃ®nent pendant 120 pas, depuis des
buffers vides et sans importer de rÃ©sultats FFT ou de particules. Deux essais
natifs se rÃ©pÃ¨tent exactement modulo emplacements atomiques. Les 6 886
particules actives ne prouvent cependant pas la paritÃ© : 13 identitÃ©s diffÃ¨rent,
un Ã©cart de trajectoire atteint 269,516 mm et un texel reÃ§oit quatre unitÃ©s de
dÃ©pÃ´t supplÃ©mentaires. Le budget source est encore rejouÃ© ; sa prise en compte
un pas trop tÃ´t a Ã©tÃ© corrigÃ©e. Consommation des dÃ©pÃ´ts par ShoreSim, budget
adaptatif natif et rendu des sprites restent ouverts. Les 21 buffers FFT et
les dÃ©cisions du test isolÃ© de retombÃ©es restent inchangÃ©s.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-history.md).

## IncrÃ©ment prÃ©cÃ©dent â€” progression temporelle du FFT

Le FFT natif peut avancer d'un pas par soumission en conservant ses ressources
et son temps. Ses 21 sorties restent exactement identiques au calcul groupÃ©
au premier pas et Ã  120 pas, ainsi qu'en mer agitÃ©e Ã  120 pas. La capture de
scÃ¨ne et les Ã©tats rivage/crÃªtes sont inchangÃ©s. Le raccordement temporel
des embruns et du rivage reste Ã  effectuer.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-stepped.md).

## IncrÃ©ment prÃ©cÃ©dent â€” Ã©mission et premier dÃ©placement natifs des embruns

Le kernel d'Ã©mission original tourne sur le GPU Bevy depuis des buffers vides.
Au premier pas, les 47 particules ont les mÃªmes types et identifiants ; l'Ã©cart
maximal par coordonnÃ©e vaut 0,729 mm. RÃ©pÃ©tition native identique modulo ordre
atomique. L'acquisition conserve dÃ©sormais la graine GPU effective sans
introduire de soumission intermÃ©diaire. Les entrÃ©es FFT sont encore appariÃ©es
source. Le kernel de dÃ©placement s'exÃ©cute ensuite sur les mÃªmes buffers :
47 particules concordantes, maximum de position 0,727 mm, rÃ©pÃ©tition identique.
Le dÃ©pÃ´t est nul Ã  ce premier instant. Des mises Ã  jour natives isolÃ©es Ã  2 s
et 9 s, depuis des Ã©tats initiaux source, reproduisent exactement les dÃ©cisions
de vie/mort et les dÃ©pÃ´ts pondÃ©rÃ©s de 349 et 448 gouttes dans les mÃªmes texels.
Historique natif couplÃ©, consommation par ShoreSim et rendu restent Ã  qualifier.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-emission.md).

## IncrÃ©ment prÃ©cÃ©dent â€” rÃ©fÃ©rence couplÃ©e des embruns

La simulation originale FFT â†’ rivage â†’ Ã©mission â†’ particules est acquise sur
neuf secondes, avec budget d'Ã©mission et dÃ©pÃ´ts d'Ã©cume enregistrÃ©s. Deux
exÃ©cutions donnent les mÃªmes particules actives modulo permutation atomique,
6 886 Ã  2 s et 12 462 Ã  9 s. Les effets sur l'Ã©cume et son rÃ©sidu sont mesurÃ©s.
Simulation et rendu natifs des embruns restent Ã  raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-spray-reference.md).

## IncrÃ©ment prÃ©cÃ©dent â€” ruban visible des dÃ©ferlantes

Le matÃ©riau original du ruban est rendu dans Bevy avec les crÃªtes GPU natives,
alpha prÃ©multipliÃ© et Ã©clairage du ciel partagÃ©. Sur la pose cÃ´tiÃ¨re Ã  2 s,
1 484 pixels modifiÃ©s concordent sur 1 485 ; erreur moyenne de canal dans la
rÃ©gion comparÃ©e 0,115/255. Capture rÃ©pÃ©tÃ©e identique et rÃ©gressions rÃ©ussies.
Autres poses/temps, embruns, animation et qualification globale restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-lip.md).

## IncrÃ©ment prÃ©cÃ©dent â€” crÃªtes dans la scÃ¨ne et rÃ©fÃ©rence du ruban

Le calcul partagÃ© est installÃ© dans la scÃ¨ne cÃ´tiÃ¨re ; capture, FFT et ShoreSim
restent identiques et les crÃªtes concordent exactement avec le test natif isolÃ©.
Le rendu source avec le ruban original est acquis et reproduit exactement.
Le matÃ©riau natif du ruban reste Ã  raccorder ; aucune paritÃ© visuelle nouvelle
n'est dÃ©clarÃ©e et tous les critÃ¨res TW complets restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-scene-inputs.md).

## IncrÃ©ment prÃ©cÃ©dent â€” FFT natif partagÃ© vers les crÃªtes

Les crÃªtes consomment directement la texture GPU du FFT natif Ã  2 s.
L'exÃ©cution rÃ©ussit sans fichiers de rÃ©sultats FFT/crÃªtes dans les entrÃ©es :
690 crÃªtes actives, identifiants et segments dessinables concordants ; erreur
maximale par coordonnÃ©e de racine 1,431 mm. Les rÃ©gressions FFT, ShoreSim,
calcul isolÃ© et capture cÃ´tiÃ¨re restent identiques. Le ruban visible et
l'animation restent ouverts ; aucun critÃ¨re TW complet n'est fermÃ©.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-shared-fft.md).

## IncrÃ©ment prÃ©cÃ©dent â€” calcul GPU isolÃ© des crÃªtes

Le kernel des crÃªtes tourne sur le GPU de Bevy, avec stations gÃ©nÃ©rÃ©es en Rust
et entrÃ©es FFT appariÃ©es de la rÃ©fÃ©rence. Aux trois instants contrÃ´lÃ©s, mÃªmes
crÃªtes actives, identifiants et segments dessinables ; erreurs maximales par
coordonnÃ©e de racine de 1,20 Ã  2,15 mm. RÃ©pÃ©tition, build et Clippy rÃ©ussis.
La chaÃ®ne FFT natif partagÃ© â†’ crÃªtes â†’ ruban visible reste Ã  raccorder ; ce
contrÃ´le numÃ©rique isolÃ© ne vaut pas qualification visuelle.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-compute.md).

## IncrÃ©ment prÃ©cÃ©dent â€” gÃ©omÃ©trie des vagues plongeantes

Les 577 stations de plage et le maillage de rubans sont calculÃ©s en Rust et
identiques octet pour octet Ã  la rÃ©fÃ©rence (46 080 sommets, 131 328 indices).
Le kernel original des crÃªtes et ses entrÃ©es/sorties sont acquis et reproduits
sur trois instants jusqu'Ã  9 s. Le calcul GPU natif des crÃªtes, leur matÃ©riau
visible et les embruns restent Ã  raccorder et Ã  comparer.
[Preuves et limites](../evidence/tidewater/2026-09-24-breaker-geometry.md).

## IncrÃ©ment prÃ©cÃ©dent â€” variations de surface SeaDetail

Les fonctions originales de rafales et de bandes lisses modulent maintenant
le matÃ©riau d'eau natif. Bruit et paramÃ¨tres acquis et vÃ©rifiÃ©s ; deux vues
appariÃ©es Ã  2 s comparÃ©es et inspectÃ©es. RÃ©pÃ©titions, anciens modes, Ã©tats FFT
et ShoreSim, compilation et Clippy rÃ©ussis. DÃ©rive interactive et lignes
d'Ã©cume par vent fort restent Ã  qualifier, ainsi que les effets encore absents.
[Preuves et limites](../evidence/tidewater/2026-09-24-sea-detail.md).

## IncrÃ©ment prÃ©cÃ©dent â€” sable humide raccordÃ© au terrain CDLOD affichÃ©

La copie du matÃ©riau utilisÃ©e par le terrain adaptatif reÃ§oit dÃ©sormais la
simulation du rivage. Le diagnostic direct d'humiditÃ© confirme la correction ;
l'erreur moyenne sur la rÃ©gion d'effet diminue de 1,276578 Ã  0,259991 /255
sur la plage et de 0,474857 Ã  0,124949 sur l'Ã®le. Deux vues inspectÃ©es,
rÃ©pÃ©tition, anciens modes, FFT et ShoreSim vÃ©rifiÃ©s ; build et Clippy rÃ©ussis.
Les Ã©carts locaux, la dynamique et les effets encore absents restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-lod-material.md).

## IncrÃ©ment prÃ©cÃ©dent â€” rivage simulÃ© visible dans Forge

ShoreSim utilise le graphe GPU de la scÃ¨ne et fournit une Image Bevy partagÃ©e
par l'eau et le terrain. SurfFoam et le hook original de sable mouillÃ© sont
raccordÃ©s ; deux vues natives sont comparÃ©es et inspectÃ©es. RÃ©pÃ©tition, Ã©tats
simulÃ©s, FFT et anciens modes sont vÃ©rifiÃ©s. Les Ã©carts locaux d'Ã©cume et la
qualification animÃ©e restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-material-native.md).

## IncrÃ©ment prÃ©cÃ©dent â€” rÃ©fÃ©rence des matÃ©riaux du rivage simulÃ©

Acquisition reproductible de 64 fichiers : WaterMaterial, ShoreSim, SurfFoam,
dentelle et hook de sable mouillÃ© original. Deux vues source sont capturÃ©es et
inspectÃ©es ; leur Ã©tat GPU est identique au pas 120 de l'oracle numÃ©rique.
Le rendu natif doit maintenant consommer sa propre texture ShoreSim dans les
matÃ©riaux de l'eau et du terrain, puis Ãªtre comparÃ© Ã  ces vues.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-material-reference.md).

## IncrÃ©ment prÃ©cÃ©dent â€” arrondi du rivage corrigÃ©

La contraction arithmÃ©tique du hash des vagues est corrigÃ©e dans le calcul
ShoreSim et le matÃ©riau visible. Les 73 728 valeurs contrÃ´lÃ©es sont identiques
Ã  la rÃ©fÃ©rence en Vulkan et DX12 sur le GPU testÃ©. La simulation de 18 s est
reproductible et ses Ã©carts sont fortement rÃ©duits ; deux vues de carte sont
recalculÃ©es et inspectÃ©es. Le raccordement de l'Ã©tat persistant au sable et
Ã  SurfFoam reste la prochaine Ã©tape, puis la comparaison animÃ©e.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-rounding.md).

## IncrÃ©ment prÃ©cÃ©dent â€” simulation persistante du rivage

ShoreSim s'exÃ©cute sur le GPU Bevy pendant 18 secondes depuis un Ã©tat vide,
avec conservation de l'Ã©cume, de l'humiditÃ© et des rÃ©sidus. Acquisitions et
rÃ©pÃ©tition native reproductibles, compilation et Clippy rÃ©ussis. La comparaison
rÃ©vÃ¨le une divergence du hash trigonomÃ©trique qui module les vagues ; la paritÃ©
numÃ©rique et le raccordement aux matÃ©riaux restent ouverts.
[Preuves et diagnostic](../evidence/tidewater/2026-09-24-shore-simulation.md).

## IncrÃ©ment prÃ©cÃ©dent â€” vagues de rivage et nappe de ressac

ShoreWaves est raccordÃ© au relief final et au matÃ©riau natif : dÃ©ferlement,
Ã©cume immÃ©diate et remontÃ©e sur le sable. Deux vues appariÃ©es sont comparÃ©es,
avec mesure spÃ©cifique des zones modifiÃ©es par l'effet. Les acquisitions,
la rÃ©pÃ©tition de plage et la rÃ©gression sans rivage sont exactes. Le champ de
propagation reste acquis ; sa rÃ©gÃ©nÃ©ration Rust, ShoreSim et les embruns sont ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-shore-waves.md).

## IncrÃ©ment prÃ©cÃ©dent â€” matÃ©riau optique de l'eau visible

Les fonctions WaterMaterial originales sont exÃ©cutÃ©es dans Bevy : normales,
Fresnel, reflets ciel/Ã©cran, rÃ©fraction, absorption/diffusion et mÃ©lange d'Ã©cume.
Deux vues appariÃ©es donnent un Ã©cart RGB moyen d'image de 0,177 et 0,147 /255.
La rÃ©pÃ©tition de la vue mer, la gÃ©omÃ©trie et les sorties FFT restent exactes.
La paritÃ© complÃ¨te, les vagues de cÃ´te, la vue mixte Ã  la ligne d'eau et le
rendu final restent ouverts ; les paramÃ¨tres ambiants sont encore figÃ©s pour
la comparaison. Le premier milieu entiÃ¨rement immergÃ© est qualifiÃ© sÃ©parÃ©ment.
[Preuves et limites](../evidence/tidewater/2026-09-24-water-optics.md).
[Preuves du milieu sous-marin](../evidence/tidewater/2026-09-27-underwater-medium.md).

## IncrÃ©ment prÃ©cÃ©dent â€” entrÃ©es de scÃ¨ne pour l'optique de l'eau

La surface lit maintenant couleur et profondeur opaques avant la passe d'eau,
sur le device Bevy. Comparaison indÃ©pendante sur la vue vers la mer ; gÃ©omÃ©trie
et sorties FFT inchangÃ©es. Les modules du matÃ©riau original et les onze mips
de sa texture d'Ã©cume sont acquis de maniÃ¨re reproductible. Le rendu optique
lui-mÃªme reste Ã  raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-water-scene-inputs.md).

## IncrÃ©ment prÃ©cÃ©dent â€” surface ocÃ©anique dÃ©placÃ©e visible

La surface utilise les dÃ©placements FFT natifs dans la scÃ¨ne Bevy, avec le
relief et les fonctions WaterSurface d'origine. Deux vues comparent 61 et 132
tuiles exactes ; les silhouettes blanches prÃ©sentent 1 et 14 pixels d'Ã©cart.
Les 21 sorties FFT et la rÃ©gression du ciel sont identiques aux preuves
prÃ©cÃ©dentes. Le blanc est un diagnostic gÃ©omÃ©trique ; le matÃ©riau optique,
le rivage et la boucle interactive restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-geometry.md).

## IncrÃ©ment prÃ©cÃ©dent â€” maillage et sÃ©lection ocÃ©anique exacts

La grille native rÃ©utilisable correspond aux 1 089 sommets et 6 144 indices
source. SÃ©lection des racines mobiles et transitions exactes sur 70 camÃ©ras,
8 695 tuiles ordonnÃ©es, douze lignes de morphing. Les fonctions WaterSurface
d'origine sont acquises pour le raccordement des dÃ©placements ; elles ne sont
pas encore exÃ©cutÃ©es dans un rendu de surface native.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-lod.md).

## IncrÃ©ment prÃ©cÃ©dent â€” graphe ocÃ©an partagÃ© avec la scÃ¨ne

Le calcul FFT s'installe sur le device de la scÃ¨ne Bevy et expose ses deux
textures Ã  quatre couches/neuf mips. Les 21 sorties sont identiques au test
isolÃ©, l'image de l'Ã®le est inchangÃ©e et la rÃ©gression tempÃªte passe.
La capture attend aussi la fin des lectures GPU. Ce raccordement ne dessine
pas encore de surface d'eau et conserve un historique fixe de 120 pas.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-shared-graph.md).

## IncrÃ©ment prÃ©cÃ©dent â€” FFT, Ã©cume et mips natifs

Neuf kernels d'ocÃ©an exÃ©cutÃ©s sur le device Bevy : initialisation, transformÃ©es
inverses lignes/colonnes, accumulation d'Ã©cume et neuf niveaux mipmap. Six
snapshots appariÃ©s (1/60/120 pas, dÃ©faut/tempÃªte), tous finis. Ã‰cart maximum
de dÃ©placement par cascade : 1,95 mm / 3,91 mm ; rÃ©pÃ©tition finale exacte.
La boucle de scÃ¨ne et le matÃ©riau d'eau restent Ã  raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-fft.md).

## IncrÃ©ment prÃ©cÃ©dent â€” spectre ocÃ©anique natif

Les trois kernels d'initialisation des quatre cascades ocÃ©aniques s'exÃ©cutent
sur le device Bevy avec paramÃ¨tres Rust. Douze blocs uniformes sont exacts
dans chacun des deux cas (dÃ©faut/tempÃªte). Ã‰carts GPU maximaux de 3,93e-6 et
5,78e-6 sur le spectre. RÃ©fÃ©rences temporelles complÃ¨tes acquises pour la suite,
y compris Ã©cume de tempÃªte ; pas encore de FFT animÃ©e ni de surface d'eau native.
[Preuves et limites](../evidence/tidewater/2026-09-24-ocean-spectrum.md).

## IncrÃ©ment prÃ©cÃ©dent â€” ciel clair visible et soleil natifs

Les LUTs calculÃ©es sur le mÃªme device Bevy alimentent le fond de ciel et le
disque solaire. Deux vues fixes 1920Ã—1080 comparÃ©es Ã  la source : erreur RGB
moyenne de 0,1154/255 sur l'Ã®le et 0,0315/255 vers le soleil. Les diffÃ©rences
rÃ©siduelles et une variation d'un pixel entre deux captures sont conservÃ©es.
Le village sans ciel et les quatre sorties atmosphÃ©riques restent identiques.
Le fond bleu sous l'horizon n'est pas un ocÃ©an ; eau, nuages et nuit restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-visible-sky.md).

## IncrÃ©ment prÃ©cÃ©dent â€” frontiÃ¨re atmosphÃ©rique Vulkan stabilisÃ©e

Diagnostic GPU du choix de racine Ã  la frontiÃ¨re puis correction conditionnelle
de la distance. Erreur maximale de transmission rÃ©duite de 1 Ã  0,0122 ; 94
pixels fortement diffÃ©rents deviennent un seul. Sorties Dx12 inchangÃ©es ;
Ã©carts rÃ©siduels conservÃ©s. Le ciel visible reste Ã  raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-atmosphere-boundary.md).

## IncrÃ©ment prÃ©cÃ©dent â€” kernels atmosphÃ©riques natifs

Transmission, diffusion multiple, luminance du ciel et irradiance calculÃ©es
par Bevy ; paramÃ¨tres Rust exacts Ã  deux hauteurs. Acquisitions reproductibles,
build et Clippy rÃ©ussis. Une anomalie de transmission est localisÃ©e Ã  la
derniÃ¨re ligne sous Vulkan ; le mÃªme binaire Dx12 rÃ©duit fortement l'Ã©cart.
Cause numÃ©rique prÃ©cise, raccordement au ciel visible et nuages encore ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-atmosphere-compute.md).

## IncrÃ©ment prÃ©cÃ©dent â€” rÃ©ception PCSS/PCF des ombres natives

Terrain, village, vÃ©gÃ©tation et herbe reÃ§oivent les ombres des objets calculÃ©es
par Bevy, avec le filtre source et les paramÃ¨tres de cascades Rust. Village
et forÃªt comparÃ©s et inspectÃ©s : erreurs moyennes 0,427 et 0,778 /255 par
canal ; des Ã©carts localisÃ©s subsistent. Captures reproductibles, rÃ©gression
sans filtre inchangÃ©e et refus des modes incohÃ©rents vÃ©rifiÃ©s. Animation,
rendu final et paritÃ© complÃ¨te restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-object-shadow-filter.md).

## IncrÃ©ment prÃ©cÃ©dent â€” passes d'ombre partagÃ©es avec la scÃ¨ne couleur

Le prototype visuel installe les mÃªmes passes natives que l'outil headless,
sur son device Bevy. Profondeurs village et forÃªt identiques aux captures
natives prÃ©cÃ©dentes ; couleur et ombre du relief village inchangÃ©es.
La texture est disponible dans la scÃ¨ne, mais le filtre des matÃ©riaux reste
Ã  raccorder. [Preuves et limites](../evidence/tidewater/2026-09-24-shared-shadow-graph.md).

## IncrÃ©ment prÃ©cÃ©dent â€” profondeur des ombres vÃ©gÃ©tales natives

Les cinq groupes de plantes occulteurs rejoignent terrain et village dans
les trois cascades Bevy. Deux vues comparÃ©es, avec identifiants et ordre des
instances exacts. Les captures natives sont reproductibles et la rÃ©gression
terrain/village reste identique. Des Ã©carts ponctuels de masque/profondeur
subsistent ; le filtre et la rÃ©ception dans la scÃ¨ne couleur restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-plant-shadow-depth.md).

## IncrÃ©ment prÃ©cÃ©dent â€” terrain dans les cartes d'ombre natives

Grille CDLOD instanciÃ©e et morphÃ©e depuis la camÃ©ra principale dans les trois
cascades, avec hauteurs et programmes source. Deux vues comparÃ©es : couverture
exacte ; profondeur exacte sur la vue Ã®le, 1 996 texels diffÃ©rents sur la vue
village (maximum 4,53e-6 normalisÃ©). Plantes et filtrage restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-terrain-shadow-depth.md).

## IncrÃ©ment prÃ©cÃ©dent â€” profondeur native des ombres du village

Les trois couches d'ombre sont gÃ©nÃ©rÃ©es par le GPU Bevy pour les bÃ¢timents
opaques et l'enseigne. Deux camÃ©ras comparÃ©es : profondeurs identiques Ã 
l'octet prÃ¨s aux six couches source (dont deux vides sur la vue Ã©loignÃ©e).
Terrain, plantes, filtrage et rÃ©ception dans la scÃ¨ne assemblÃ©e restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-09-24-village-shadow-depth.md).

## IncrÃ©ment prÃ©cÃ©dent â€” prÃ©paration des ombres portÃ©es

Calcul Rust des trois camÃ©ras d'ombre et de leur actualisation vÃ©rifiÃ© contre
89 Ã©tats source : 20 025 scalaires, erreur maximale 2,85e-13. Oracle GPU source
des objets acquis et rÃ©pÃ©tÃ©. Les passes de profondeur et le filtre des ombres
portÃ©es restent Ã  raccorder au rendu natif.
[Preuves et limites](../evidence/tidewater/2026-09-24-sun-cascades.md).

L'herbe est Ã©galement raccordÃ©e au prototype : trois niveaux de dÃ©tail,
masque et sÃ©lection source, contrÃ´les gÃ©omÃ©triques et scÃ¨ne assemblÃ©e.
[Preuves herbe](../evidence/tidewater/2026-09-24-grass.md).

## IncrÃ©ment prÃ©cÃ©dent â€” ombres du relief calculÃ©es sur le GPU natif

Rust reconstruit exactement la pyramide de hauteurs source ; Bevy exÃ©cute
le kernel d'ombre et partage sa sortie entre terrain, village et vÃ©gÃ©tation.
Lecture GPU comparÃ©e au source : 22 texels sur 262 144 diffÃ¨rent dans la version
Ã  soleil paramÃ©trable, hauteur maximale 0,03125 m ; distance d'occulteur exacte.
Vues village/Ã®le comparÃ©es, rÃ©gression sans ombres inchangÃ©e. Ombres des objets,
nuages et parcours de modification interactive restent Ã  qualifier.
[Preuves et limites](../evidence/tidewater/2026-09-24-hill-shadows.md).

## IncrÃ©ment prÃ©cÃ©dent â€” Ã©clairage commun terrain, village et vÃ©gÃ©tation

Les trois familles consomment le mÃªme soleil et le mÃªme environnement acquis,
avec une texture de ciel partagÃ©e. Deux vues assemblÃ©es comparÃ©es Ã  l'oracle
source sans ombres : erreur moyenne par canal 0,464934/255 au village et
0,166189/255 depuis l'Ã®le. AlbÃ©do village et vÃ©gÃ©tation isolÃ©e inchangÃ©s.
Les ombres, l'ocÃ©an, le ciel dynamique et le rendu final restent Ã  raccorder.
[Preuves et limites](../evidence/tidewater/2026-09-24-scene-lighting.md).

## IncrÃ©ment prÃ©cÃ©dent â€” Ã©clairage du feuillage par le ciel acquis

Le natif consomme les SH diffus et le cube spÃ©culaire calculÃ©s par les
programmes originaux d'atmosphÃ¨re/nuages, avec le soleil rÃ©el et la compensation
de diffusion source. Comparaisons village/forÃªt : erreurs moyennes par canal
0,056246/255 et 1,011835/255. Ã‰carts persistants, environnement figÃ© issu d'un
Ã©tat froid ; ni port du ciel dynamique ni fidÃ©litÃ© finale ne sont acquis.
[Preuves et limites](../evidence/tidewater/2026-09-24-vegetation-environment.md).

## IncrÃ©ment prÃ©cÃ©dent â€” lumiÃ¨re directe du feuillage

Le programme natif reprend les termes directs source, intensitÃ© spÃ©culaire et
transmission incluses. Sous soleil blanc unitaire, erreurs moyennes par canal
de 0,015624/255 au village et 0,339026/255 en forÃªt ; silhouettes inchangÃ©es.
Ce contrÃ´le exclut ciel, ombres et traitements finaux. Il ne qualifie pas
l'Ã©clairage complet et conserve des Ã©carts dans la forÃªt.
[Preuves et limites](../evidence/tidewater/2026-09-24-vegetation-direct-light.md).

## IncrÃ©ment prÃ©cÃ©dent â€” vÃ©gÃ©tation proche et lointaine

Les 65 383 enregistrements des six groupes sont raccordÃ©s au prototype natif,
avec huit niveaux de gÃ©omÃ©trie et leurs matÃ©riaux source. SÃ©lection initiale
Rust, matrices exactes, maillages partagÃ©s et fenÃªtres de distance GPU.
Trois vues comparÃ©es au source indÃ©pendant : IoU des silhouettes de 0,997886
Ã  0,999740 ; Ã©carts rÃ©siduels documentÃ©s. Palmiers et sous-bois apparaissent
autour du village. Ã‰clairage/ciel/ombres/temporalitÃ© restent non fidÃ¨les,
herbe absente, sÃ©lection en mouvement et Studio non raccordÃ©s.
[Preuves et limites](../evidence/tidewater/2026-09-24-vegetation-scene.md).

## IncrÃ©ment prÃ©cÃ©dent â€” rÃ©fÃ©rences reprises et canopÃ©e partagÃ©e

Les six rÃ©fÃ©rences upstream sont dÃ©sormais de vrais PNG Full HD sans mosaÃ¯que,
dans `baseline-003/`. Les acquisitions prÃ©cÃ©dentes restent exclues et conservÃ©es.
Les 18 343 arbres/arbustes lointains du prototype natif partagent un maillage et
un matÃ©riau ; leurs donnÃ©es source sont adressÃ©es par instance. Masque isolÃ©
et image combinÃ©e avec village/terrain strictement identiques au prototype
prÃ©cÃ©dent, sans qualification de performances ni paritÃ© de scÃ¨ne entiÃ¨re.
[Preuves et limites](../evidence/tidewater/2026-09-24-canopy-instancing.md).

## Premier incrÃ©ment vÃ©rifiÃ© â€” 24 septembre 2026

- Source exacte conservÃ©e ; build upstream et tests de logique rÃ©ussis.
- Six captures upstream FHD inspectÃ©es, camÃ©ras/heures/mÃ©tadonnÃ©es et hashes dans
  `.tidewater-integration/baseline/index.json` (hors dÃ©pÃ´t). Ce sont des vues fixes,
  pas encore des parcours ni une preuve de dÃ©terminisme temporel.
- Exports indÃ©pendants `source-006` et `source-007` identiques : 105 fichiers de
  donnÃ©es, terrain aprÃ¨s fondations, masques, 65 383 instances, huit prototypes,
  modules WGSL vÃ©gÃ©tation et atlas de feuilles avec tous ses mipmaps.
- Contrat raster gÃ©nÃ©rique + lecteur Rust : 1 030 sondes rÃ©elles, erreur maximale
  2,842170943040401e-14 m. Pas encore branchÃ© au document de zone ni aux commandes.
- `cargo test -p forge_world -p forge_project --locked -j1` : 416 tests rÃ©ussis.
- Clippy strict sur toutes les cibles de ces deux crates : rÃ©ussi.
- Compilation WASM de ces deux crates : rÃ©ussie, sans qualification du rendu Web.
- RÃ©paration de compatibilitÃ© d'un test existant : champ `AuditStamp::legacy()`
  ajoutÃ© au reÃ§u de `tests/path_surface.rs`, conforme au reÃ§u preview courant.
- Prototype `tidewater_plant_probe` compilÃ© et exÃ©cutÃ© : capture native FHD inspectÃ©e,
  deux exÃ©cutions avec PNG identiques ; Clippy strict rÃ©ussi. GÃ©omÃ©trie et surface
  originales, Ã©clairage Bevy provisoire, ombres/translucence/LOD temporel et Studio
  non qualifiÃ©s. [Preuves et limites](../evidence/tidewater/2026-09-24-first-increment.md).

Les journaux et donnÃ©es volumineuses restent sur E:, sous le rÃ©pertoire du chantier.
Les serveurs de rÃ©fÃ©rence ont Ã©tÃ© arrÃªtÃ©s et l'onglet temporaire fermÃ© aprÃ¨s capture.
Toutes les lignes TW restent ouvertes tant que leur critÃ¨re complet n'est pas satisfait.

DeuxiÃ¨me incrÃ©ment : exports `source-008/009` identiques (207 fichiers), paramÃ¨tres
runtime des 65 383 plantes, atlas et shaders lointains conservÃ©s. Prototype Bevy
de canopÃ©e avec les 18 343 records arbres/arbustes, capture inspectÃ©e. Mesures de
silhouettes : proche 98,99â€“99,42 %, feuillu lointain 100 % sur une pose, canopÃ©e
lointaine 99,988457 % (16 pixels diffÃ©rents). Clippy strict et rÃ©gression proche
rÃ©ussis. Ã‰clairage, animation, production/Studio et scÃ¨ne complÃ¨te non qualifiÃ©s.
Voir les preuves ci-dessus ; aucun de ces chiffres ne ferme la paritÃ© visuelle.

TroisiÃ¨me incrÃ©ment : terrain canonique v4 opt-in avec source raster hashÃ©e,
retouches sur la grille auteur existante, Undo/Redo et archive de session testÃ©s.
419 tests projet/monde rÃ©ussis, Clippy et compilation WASM de ces crates rÃ©ussis.
Assemblage natif terrain 2048 m + canopÃ©e exÃ©cutÃ© et capture inspectÃ©e ; matÃ©riau
gris et grille fixe encore diagnostiques. Export des shaders et cartes de terrain
originales (`source-010/011`, 226 fichiers identiques). La rÃ©solution des blobs
dans la zone, Studio, Rapier, CDLOD et matÃ©riaux source restent ouverts.
[Preuves et limites du terrain v4](../evidence/tidewater/2026-09-24-raster-document.md).

QuatriÃ¨me incrÃ©ment : matÃ©riaux de terrain source rendus dans Bevy avec leurs
mipmaps originaux (exports `source-012/013`, 260 fichiers identiques). Vues Ã®le et
cÃ´te inspectÃ©es ; camÃ©ra cÃ´tiÃ¨re reprise de la rÃ©fÃ©rence. Clippy strict rÃ©ussi,
capture cÃ´tiÃ¨re rÃ©pÃ©tÃ©e identique et rÃ©gression silhouette proche inchangÃ©e.
Ã‰clairage Bevy provisoire, humiditÃ©/ombre solaire/CDLOD/ocÃ©an/contenu complet et
Studio restent ouverts. [Preuves matÃ©riau](../evidence/tidewater/2026-09-24-terrain-surface.md).

CinquiÃ¨me incrÃ©ment : sÃ©lection CDLOD Rust identique Ã  la source pour 47 vues,
2 845 tuiles ordonnÃ©es, 405 bornes et neuf paramÃ¨tres de transition. DÃ©placement
GPU original raccordÃ© au prototype Bevy : masques cÃ´tiers et Ã®le identiques
pixel pour pixel en FHD, sur deux poses fixes. La cÃ´te utilise 62 tuiles et
198 400 triangles ; l'Ã®le 18 tuiles et 57 600 triangles. Ces nombres ne valent
pas benchmark ni paritÃ© des matÃ©riaux, ombres ou scÃ¨nes complÃ¨tes. Instanciation
produit, camÃ©ra mobile, retouches GPU et Studio restent ouverts.
[Preuves CDLOD](../evidence/tidewater/2026-09-24-terrain-cdlod.md).

SixiÃ¨me incrÃ©ment : village rendu dans la scÃ¨ne native avec sept matÃ©riaux source,
14 textures et leurs mipmaps, 31 Ã©lÃ©ments de rendu (bÃ¢timents/accessoires regroupÃ©s,
enseigne et lanternes). Exports indÃ©pendants `source-016/017` identiques, 503
fichiers de donnÃ©es vÃ©rifiÃ©s. Une passe d'albÃ©do directe donne un pixel de contour
diffÃ©rent sur 351 564 pixels occupÃ©s et une erreur moyenne de canal de 1,1985/255 ;
les couleurs ne sont donc pas dÃ©clarÃ©es identiques. Ã‰clairage, poissons du marchÃ©,
animation des groupes et sÃ©paration en objets Studio restent ouverts.
[Preuves village](../evidence/tidewater/2026-09-24-village.md).

## VÃ©rifications obligatoires

Adaptateur Bevy du paquet : maillages locaux/de rÃ©fÃ©rence et bibliothÃ¨que de
textures/matÃ©riaux prÃ©parables par le moteur partagÃ©. Check et Clippy ciblÃ©
passent ; exÃ©cution rÃ©ussie sur 23 bÃ¢timents et 111 morceaux. Le prototype natif
charge maintenant le paquet sans les 598 anciens fichiers de bÃ¢timents : image
et ombres identiques au rendu prÃ©cÃ©dent, en pose initiale et aprÃ¨s dÃ©placement
enregistrÃ© d'une maison. Raccordement au viewport Studio encore ouvert.
[Ã‰tat de l'adaptateur](../evidence/tidewater/2026-09-26-package-bevy-assets.md).

Racines de bÃ¢timents importÃ©s : crÃ©ation depuis le placement canonique,
maillages partagÃ©s et identitÃ© de sÃ©lection commune avec Studio. ContrÃ´le ECS
de duplication/suppression rÃ©ussi et Studio compilÃ© en WASM ; rÃ©solution du
paquet dans le contrÃ´leur Studio et qualification visuelle encore ouvertes.
[EntitÃ©s de placement](../evidence/tidewater/2026-09-26-placement-entities.md).

La construction des matÃ©riaux du village est maintenant partagÃ©e avec le moteur
et utilisÃ©e par le prototype. Capture native depuis le paquet : pixels et deux
buffers d'ombres identiques Ã  la capture prÃ©cÃ©dente.
[Construction des matÃ©riaux](../evidence/tidewater/2026-09-26-material-construction.md).

Ã‰clairage du village paramÃ©trable : soleil et coefficients diffus SH ne sont
plus figÃ©s dans le shader du paquet 003. Capture aux valeurs acquises : deux
pixels Ã  un niveau d'Ã©cart maximal, ombres identiques. Variations indÃ©pendantes
du soleil et de l'ambiance diffuse observÃ©es et mesurÃ©es ; raccordement aux
lumiÃ¨res Studio et Ã  l'horloge de scÃ¨ne encore ouvert.
[Ã‰clairage du village](../evidence/tidewater/2026-09-26-village-scene-lighting.md).

Cube HDR : chargeur partagÃ© natif/WebGPU, validation des dimensions, mipmaps et
valeurs finies. Bytes acquis conservÃ©s exactement ; nouvelle capture native
identique Ã  la prÃ©cÃ©dente, ombres comprises. Le calcul GPU du ciel reste Ã 
sortir du module d'exemple pour le raccordement Studio.
[Cube d'environnement partagÃ©](../evidence/tidewater/2026-09-26-shared-environment-cube.md).

Les quatre kernels atmosphÃ©riques sont dÃ©sormais exÃ©cutÃ©s par le module moteur
partagÃ©, sans I/O ni nouveau pÃ©riphÃ©rique GPU. Les quatre readbacks sont identiques
Ã  la preuve native stabilisÃ©e ; compilation WASM rÃ©ussie. Le calcul reste fixe :
horloge/camÃ©ra, adoption Studio et exÃ©cution WebGPU restent Ã  qualifier.
[AtmosphÃ¨re partagÃ©e](../evidence/tidewater/2026-09-26-shared-atmosphere.md).

Le matÃ©riau et la gÃ©omÃ©trie du ciel sont Ã©galement partagÃ©s. Le shader vÃ©rifiÃ©
est installÃ© en mÃ©moire, sans chemin AssetServer vers la fixture. Capture native
identique Ã  la prÃ©cÃ©dente (zÃ©ro pixel diffÃ©rent, deux ombres identiques), Clippy
et compilation WASM rÃ©ussis. Nuages GPU et raccordement Studio restent ouverts.
[Ciel partagÃ©](../evidence/tidewater/2026-09-26-shared-sky.md).

La chaÃ®ne GPU nuageuse est extraite dans le moteur partagÃ© : mÃ©tÃ©o/bornes,
ombres, volume, panorama et historique. Dix textures identiques Ã  l'octet sur
120 pas/120 passages ; capture assemblÃ©e identique, ombres inchangÃ©es.
Check natif/WASM et Clippy ciblÃ©s passent. SÃ©quence encore bornÃ©e et paramÃ¨tres
globaux acquis : boucle interactive et raccordement Studio restent ouverts.
[Nuages partagÃ©s](../evidence/tidewater/2026-09-26-shared-clouds.md).

Paquet d'environnement sÃ©parÃ© et reproductible : 266 fichiers, 2 677 437 octets,
chargement sync/async bornÃ© et Ã©pinglÃ©. La candidate Studio rÃ©fÃ©rence dÃ©sormais
ce paquet avec les bÃ¢timents, adoption en mÃ©moire aprÃ¨s vÃ©rification des deux.
Check WASM rÃ©ussi ; nouveau parcours navigateur et raccordement GPU restent
Ã  vÃ©rifier. Les ombres terrain/objets ne sont pas incluses dans ce paquet.
[Paquet d'environnement](../evidence/tidewater/2026-09-26-environment-package.md).

Le nouveau chargement conjoint est maintenant exÃ©cutÃ© dans le navigateur :
1090 rÃ©ponses attendues et statut de succÃ¨s. Un shader altÃ©rÃ© est refusÃ©,
les requÃªtes suivantes cessent et le rÃ©essai aprÃ¨s restauration rÃ©ussit.
Qualification du transport uniquement, sans backend Tauri ni scÃ¨ne rendue.
[Preuve navigateur environnement](../evidence/tidewater/2026-09-27-studio-environment-transport.md).

La prÃ©paration moteur depuis les bytes du paquet est raccordÃ©e au chargement
Studio avant adoption. Mode natif `--package` : dix textures identiques aprÃ¨s
120 pas, cube HDR conservÃ© ; donnÃ©es d'Ã©clairage/cube invalides rejetÃ©es mÃªme
avec des hashes cohÃ©rents. Check Studio WASM rÃ©ussi. Les caches prÃ©parÃ©s ne
sont pas encore installÃ©s dans le renderer Studio.
[PrÃ©paration moteur du paquet](../evidence/tidewater/2026-09-27-package-environment-engine.md).

Atmosphere chargeable apres demarrage : extraction main/render enregistree une
seule fois, remplacement des cibles depuis le monde principal. Deux generations
natives successives qualifiees, quatre sorties identiques a la reference ;
parcours initial egalement inchange. Consommation
du cache Studio encore ouverte.
[Chargement tardif de l'atmosphere](../evidence/tidewater/2026-09-27-late-atmosphere.md).

Nuages chargeables apres demarrage : entrees et atmosphere extraites ensemble,
historique et caches GPU remis a zero au remplacement. Deux generations de
120 pas qualifiees ; dix sorties identiques a la reference. Entrees invalides
refusees sans modifier les cibles actives. Consommation du cache Studio raccordee ci-dessous.
[Chargement tardif des nuages](../evidence/tidewater/2026-09-27-late-clouds.md).

Studio consomme maintenant la preparation de l'environnement dans son Update,
apres enregistrement des graphes avec les plugins de rendu. Cube, shader, LUTs,
nuages et uniforms sont conserves ensemble pour les consommateurs de scene.
Le chemin partage est qualifie nativement (dix sorties identiques), le check
WASM passe. Verification navigateur avec zone chargee et affichage encore ouverts.
[Ressources residentes Studio](../evidence/tidewater/2026-09-27-studio-resident-environment.md).

Apercu explicite du ciel de reference : camera hors ecran dans le renderer
existant, pixels affichables dans la bibliotheque sans changer la carte. Preuve
native inspectee ; les 200 premieres lignes correspondent pixel par pixel au
ciel de la scene assemblee. La partie sous l'horizon ne contient pas de terrain
ni d'eau. Action navigateur avec zone active encore a qualifier.
[Apercu du ciel](../evidence/tidewater/2026-09-27-sky-preview.md).





Lecteur Rust du paquet : 23 bÃ¢timents rÃ©solus depuis la seule racine du paquet ;
corruption, dÃ©pendance absente et mauvais hash de manifeste refusÃ©s. Consommation
par le prototype natif validÃ©e ; intÃ©gration au viewport Studio encore ouverte.
[Preuves du lecteur](../evidence/tidewater/2026-09-26-package-reader.md).

Lecteur asynchrone ajoutÃ© pour l'adaptateur navigateur : mÃªmes validations que
la voie native, suspensions et arrÃªts sur corruption vÃ©rifiÃ©s sur le paquet
complet. Compilation WASM rÃ©ussie ; transport HTTP et viewport Ã  raccorder.
[Acquisition asynchrone](../evidence/tidewater/2026-09-26-package-async.md).

Studio propose dÃ©sormais la vÃ©rification/lecture du paquet local dans la fiche
de bibliothÃ¨que et conserve les bytes vÃ©rifiÃ©s. Check WASM et relecture de la
copie de dÃ©ploiement rÃ©ussis. Parcours HTTP rÃ©el validÃ© dans Studio navigateur :
823 requÃªtes rÃ©ussies et statut des 23 bÃ¢timents vÃ©rifiÃ©s affichÃ©. Projection
visuelle encore Ã  raccorder ; Clippy Studio arrÃªtÃ© sur la longueur de `atelier_ui::bind_main`.
[Transport Studio](../evidence/tidewater/2026-09-26-studio-package-transport.md).

Paquet local des 23 bÃ¢timents : gÃ©omÃ©tries, matÃ©riaux, textures, shader adaptÃ© et
notice MIT rassemblÃ©s ; hashes et fermeture des rÃ©fÃ©rences vÃ©rifiÃ©s. Chargement
produit et ressources d'Ã©clairage dynamiques restent ouverts.
[Preuves du paquet](../evidence/tidewater/2026-09-26-building-package.md).

MatÃ©riau village et paramÃ¨tres d'ombres partagÃ©s dans `forge_engine` ; les deux
prototypes compilent avec ces types. Capture et ombres identiques aprÃ¨s une
premiÃ¨re exÃ©cution Ã©chouÃ©e ; raccordement Studio et stabilitÃ© globale ouverts.
[Ã‰tat du matÃ©riau partagÃ©](../evidence/tidewater/2026-09-26-shared-village-material.md).

Le chargeur CPU des maillages village est partagÃ© dans `forge_engine` et consommÃ©
par le prototype. Test ciblÃ© et Clippy strict passent ; capture native et buffers
d'ombres identiques aprÃ¨s refactoring. MatÃ©riaux partagÃ©s et Studio ouverts.
[Ã‰tat du chargeur partagÃ©](../evidence/tidewater/2026-09-26-shared-village-mesh.md).

Suppression native d'une maison et de son ombre vÃ©rifiÃ©e depuis un workspace
sauvegardÃ© ; Undo rÃ©tablit exactement image et buffers d'ombres. Studio ouvert.
[Preuves suppression/Undo](../evidence/tidewater/2026-09-26-building-removal-render.md).

Placements rendus : le village consomme les transformations sauvegardÃ©es et les
partage avec les ombres. Pose initiale pixel-identique ; maison au premier plan
dÃ©placÃ©e de 12 m et capture inspectÃ©e. Studio et autres transformations ouverts.
[Preuves du raccordement natif](../evidence/tidewater/2026-09-26-building-placement-render.md).

Placements canoniques : 23 bÃ¢timents insÃ©rÃ©s par commande, dÃ©placement de 12 m,
suppression, sauvegarde/relecture et annulations exactes des placements vÃ©rifiÃ©s.
Rendu et Studio ne consomment pas encore ces rÃ©fÃ©rences.
[Preuves des placements](../evidence/tidewater/2026-09-26-building-placements.md).

Catalogue importÃ© : 23 descripteurs de bÃ¢timents avec identitÃ© stable, hash et
provenance externe MIT ; rÃ©solution aprÃ¨s sÃ©rialisation, 30 tests assets et
Clippy rÃ©ussis. Il s'agit de mÃ©tadonnÃ©es ; UI et dÃ©pendances portables ouvertes.
[Preuves catalogue](../evidence/tidewater/2026-09-26-imported-catalog.md).

RepÃ¨res locaux des 23 bÃ¢timents prÃ©parÃ©s : positions et normales rÃ©versibles
Ã  moins de 1e-6 m/composante, attributs source conservÃ©s, deux sorties identiques.
Provenance MIT Ã  reprÃ©senter dans le catalogue ; placements et Studio ouverts.
[Preuves des repÃ¨res](../evidence/tidewater/2026-09-26-building-frames.md).

Village sÃ©parÃ© rendu nativement : 145 nÅ“uds, attributs indexÃ©s conservÃ©s,
ombres exactes. Capture identique hors 14 pixels d'embruns (maximum 2/255),
avec particules identiques modulo slots. Pivots, autoritÃ© d'instances et Studio
restent Ã  raccorder ; performances non qualifiÃ©es.
[Preuves du rendu sÃ©parÃ©](../evidence/tidewater/2026-09-25-split-village-render.md).

Partition statique du village : 137656 triangles conservÃ©s, attribution exacte
aux 23 bÃ¢timents ou au dÃ©cor restant par dÃ©calages de construction. 121 plages
reconstituent les flux d'indices originaux ; rendu natif sÃ©parÃ© Ã  qualifier.
[Preuves de partition](../evidence/tidewater/2026-09-25-village-partition.md).

MatÃ©riaux des bÃ¢timents sÃ©parÃ©s : remappages source appliquÃ©s avant conversion
f32 ; 65363 triangles retrouvent exactement tous leurs attributs dans les lots
acquis. Deux prÃ©parations donnent 560 fichiers identiques. Partition du dÃ©cor
restant et rendu sÃ©parÃ© encore Ã  rÃ©aliser.
[Preuves des matÃ©riaux](../evidence/tidewater/2026-09-25-building-materials.md).

Village Ã©ditable : 23 bÃ¢timents sÃ©parÃ©s avant fusion en 111 portions ; 667
fichiers identiques entre deux acquisitions. GÃ©omÃ©trie finale et terrain
inchangÃ©s par l'observation. MatÃ©riaux remappÃ©s, catalogue et instances Studio
restent Ã  raccorder.
[Preuves de sÃ©paration](../evidence/tidewater/2026-09-25-building-parts.md).

Audit de direction cÃ´tiÃ¨re : la rÃ©fÃ©rence utilise `dirOn=0` et le champ direct.
La texture filtrÃ©e optionnelle n'est donc pas un raccordement manquant pour
cette route ; les entrÃ©es qui l'activent sont dÃ©sormais refusÃ©es explicitement.
[Preuves du parcours source](../evidence/tidewater/2026-09-25-shore-direction-route.md).

Stations de dÃ©ferlement : reconstruites depuis le terrain effectif, coordonnÃ©es
exactes avec la source pour 577, 555 et zÃ©ro point. Buffers, rubans et ombres
d'embruns adaptÃ©s ; cas vide sans Ã©mission et Undo pixel-identique vÃ©rifiÃ©s.
[Preuves des stations retouchÃ©es](../evidence/tidewater/2026-09-25-authored-breaker-stations.md).

Simulations cÃ´tiÃ¨res couplÃ©es : champ et corrections de hauteur canoniques
transmis au ressac, aux crÃªtes et aux embruns. Captures locales/globales sans
erreur GPU ; Undo restitue exactement image, ressac, crÃªtes, dÃ©pÃ´ts et ensemble
des particules. Stations de dÃ©ferlement encore Ã  reconstruire sur le terrain effectif.
[Preuves des simulations retouchÃ©es](../evidence/tidewater/2026-09-25-authored-coupled-terrain.md).

Propagation cÃ´tiÃ¨re : calcul Rust identique Ã  la source sur 1048576 valeurs,
Ã©galement aprÃ¨s relief local et Ã©lÃ©vation globale ; champ dÃ©rivÃ© raccordÃ© au
matÃ©riau optique et Undo pixel-identique. Les simulations couplÃ©es doivent encore
consommer ces donnÃ©es effectives.
[Preuves du champ cÃ´tier](../evidence/tidewater/2026-09-25-authored-shore-field.md).

Rendu de l'eau aprÃ¨s retouche : hauteurs canoniques raccordÃ©es Ã  la gÃ©omÃ©trie
et Ã  l'optique ; essai local avec 203 plantes dÃ©placÃ©es, Undo pixel-identique
aprÃ¨s conservation du shader source sans correction. Le recalcul du champ
cÃ´tier, du ressac et des dÃ©ferlements reste ouvert.
[Preuves eau/terrain](../evidence/tidewater/2026-09-25-authored-water-terrain.md).

AO du relief : formule Rust identique sur les 4194304 texels source, et oracle
JavaScript exact pour bosse locale/Ã©lÃ©vation globale. Alpha raccordÃ© aux mipmaps
GPU ; Undo local pixel-identique. Roche, cÃ´te, eau et autres consommateurs restent
ouverts. [Preuves AO](../evidence/tidewater/2026-09-25-authored-terrain-ao.md).

Normales macro corrigÃ©es : 11462 texels validÃ©s indÃ©pendamment, onze passes de
mipmaps GPU, texture partagÃ©e terrain/optique de l'eau, sorties d'ombre inchangÃ©es
et Undo pixel-identique. AO, roche, autres masques et qualification des mipmaps
restent ouverts. [Preuves normales](../evidence/tidewater/2026-09-25-authored-terrain-normals.md).

Ombres lointaines du relief Ã©ditÃ© : pyramide corrigÃ©e raccordÃ©e au bake GPU,
hash contrÃ´lÃ© indÃ©pendamment, 885 valeurs de sortie modifiÃ©es et annulation
exacte de la texture et de l'image. Normales/mipmaps et autres dÃ©rivÃ©s restent ouverts.
[Preuves ombres de relief](../evidence/tidewater/2026-09-25-authored-hill-shadows.md).

Relief auteur GPU : grille de deltas sÃ©parÃ©e raccordÃ©e au terrain CDLOD et aux
ombres proches ; reprojection des plantes consommÃ©e par les deux passes.
Essai workspace : bosse locale de12m, 203 plantes dÃ©placÃ©es, Undo pixel-identique.
Normales, masques, ombres lointaines, eau, collisions et Studio restent ouverts.
[Preuves relief GPU](../evidence/tidewater/2026-09-25-authored-relief-gpu.md).

Terrain workspace : bloc source retirÃ© de la fixture, rendu identique depuis
l'autoritÃ© sauvegardÃ©e et entrÃ©e partagÃ©e avec les ombres. Projection monde des
65383 plantes sur Ã©lÃ©vation Ã©ditÃ©e testÃ©e avec sauvegarde/relecture et Undo exact ;
relief Ã©ditÃ© GPU et consommation de cette projection restent ouverts.
[Preuves terrain/reprojection](../evidence/tidewater/2026-09-25-workspace-terrain-and-plant-projection.md).

Chargement workspace du prototype : vÃ©gÃ©tation et retouches proviennent de la
rÃ©fÃ©rence de zone sauvegardÃ©e et des blobs de l'autoritÃ©. Fixture sans population
de secours, capture identique au rendu de l'archive, refus zone absente/blob
corrompu, autoritÃ© source inchangÃ©e. Les autres ressources et corrections ne
sont pas encore raccordÃ©es au workspace.
[Preuves du chargement workspace](../evidence/tidewater/2026-09-25-workspace-vegetation-render.md).

Retouches vÃ©gÃ©tales dans le prototype natif : archives de commandes relues,
exclusion locale de 515 plantes confirmÃ©e indÃ©pendamment, suppression visible
limitÃ©e Ã  la pente gauche, annulation pixel-identique Ã  la rÃ©fÃ©rence ; densitÃ©
nulle sur 65383 plantes rendue sans erreur GPU. Raccordement Studio interactif,
placements manuels et autres corrections restent ouverts.
[Preuves des retouches rendues](../evidence/tidewater/2026-09-25-authored-vegetation-render.md).

ComplÃ©ment herbe : trois gÃ©omÃ©tries et masque source raccordÃ©s au prototype
natif ; sÃ©lection village identique (9/28/90 cellules), silhouette IoU
0,9995915 Ã  camÃ©ra/temps fixes. La scÃ¨ne assemblÃ©e inclut dÃ©sormais l'herbe,
sans qualification temporelle ou Studio.
[Preuves herbe](../evidence/tidewater/2026-09-24-grass.md).

- Export rÃ©pÃ©tÃ© : mÃªmes hashes hors mÃ©tadonnÃ©es temporelles ; aucune source modifiÃ©e.
- Inventaire complet avec statut explicite des Ã©lÃ©ments non encore extraits.
- Tests invalides : taille, chemins, hash, NaN, doublons, version, quotas.
- Promotion/modification/suppression d'une instance : pas de rÃ©apparition aprÃ¨s
  rÃ©gÃ©nÃ©ration, sauvegarde, fermeture et rÃ©ouverture.
- Terrain, eau, collisions et vÃ©gÃ©tation concordent aprÃ¨s une retouche.
- MÃªme objet trouvable et plaÃ§able dans une autre carte depuis Studio.
- RÃ©gression des cartes existantes et contrÃ´les workspace pour contrats partagÃ©s.
- Captures et mÃ©triques identifient renderer, source, build, rÃ©solution et paramÃ¨tres.
- Un export technique ne vaut ni asset qualifiÃ©, ni carte Forge chargÃ©e, ni paritÃ©.

## Limites connues au lancement

VÃ©gÃ©tation limitÃ©e Ã  512 m ; placements manuels plafonnÃ©s Ã  4096 ; source de
hauteur actuelle procÃ©durale. La carte upstream couvre 2048 m avec heightmap
2048Â². Les silhouettes vÃ©gÃ©tales et textures village dÃ©pendent de shaders.
La consommation sÃ©quentielle du RNG du village participe Ã  sa composition.
Ne pas reconstruire avec des seeds arbitraires ni exporter avant les fondations.


Regroupement des entrees du prototype natif : sept groupes, 1539 fichiers,
291477343 octets controles. Rendu depuis le nouveau dossier identique au rejeu
contemporain depuis les anciennes racines ; ombres identiques. Un ecart de 14
pixels (2/255 maximum) subsiste par rapport a la capture historique et reste
documente. Le paquet n'est pas un module ni la carte Studio finalisee.
[Paquet du prototype](../evidence/tidewater/2026-09-27-preview-scene-bundle.md).


Preparation des 23 batiments dans les assets du viewport Studio : geometries et
textures validees avant adoption, partage des handles qualifie par le probe natif.
Affichage/placement Studio encore ouvert.
[Residences batiments](../evidence/tidewater/2026-09-27-studio-building-residency.md).

Ombres de relief extraites dans le moteur partage : chargement tardif et remplacement
GPU qualifies, deux generations distinctes puis retour exact aux ombres de reference.
Scene native identique a une capture Forge anterieure ; edition Studio non qualifiee.
[Ombres partagees](../evidence/tidewater/2026-09-27-shared-hill-shadow.md).

Preparation GPU des casters opaques du village partagee dans le moteur :
rejeu natif sans difference de pixel ni d'ombres. Extraction completee ensuite
par le graphe terrain/feuillages ci-dessous ; adoption Studio encore ouverte.
[Ombres opaques partagees](../evidence/tidewater/2026-09-27-shared-opaque-shadow.md).

Graphe complet des trois cascades partage : opaque, terrain et feuillages.
Adoption tardive et remplacement qualifies sur GPU natif ; rendu de scene et deux
buffers d'ombres strictement identiques a la baseline Forge precedente.
Compilation Studio WASM reussie, execution WebGPU et edition Studio non qualifiees.
[Graphe d'ombres partage](../evidence/tidewater/2026-09-27-shared-object-shadow-graph.md).

Preparation des ombres du terrain depuis le document Forge extraite dans le moteur,
avec corrections de hauteur partagees par terrain/ocean/rivage. Clippy moteur et
verification des examples passes ; test CPU des retouches passe, scene native
et ombres identiques a la baseline precedente, compilation Studio WASM reussie.
[Terrain edite et ombres](../evidence/tidewater/2026-09-27-authored-terrain-shadow.md).

Alignement des placements sur raster et retouches partage dans forge_world :
6 tests de terrain importe et 4 regressions de placement procedural passes,
Clippy strict passe. Raccordement des controles Studio au raster encore ouvert.
[Alignement sur relief importe](../evidence/tidewater/2026-09-27-sampled-placement-alignment.md).

Transport du raster de la zone ouverte ajoute au protocole natif et au client
Studio : blocs lies au document, SHA-256 complet, source workspace geree verifiee.
Insertion, position precise, glissement et poignees utilisent le sampler importe.
Tests du host et compilations natif/WASM passes ; parcours interactif non qualifie.
[Transport terrain Studio](../evidence/tidewater/2026-09-27-studio-terrain-transport.md).

Materiau et geometrie CDLOD de terrain transferes dans le moteur partage,
prototype raccorde aux nouveaux types. Scene native et deux buffers d'ombres
identiques a la baseline precedente ; compilation Studio WASM reussie.
Viewport Studio importe non qualifie.
[Terrain partage](../evidence/tidewater/2026-09-27-shared-terrain-renderer.md).

Paquet des textures/shaders de terrain cree sans dupliquer le raster canonique :
6 payloads / 46209937 octets, empreintes et refus de corruption qualifies.
Lecteur Rust synchrone/asynchrone verifie sur le paquet reel, refus de corruption
et controles stricts Clippy/WASM passes. Transport et adoption Studio encore a realiser.
[Paquet terrain](../evidence/tidewater/2026-09-27-terrain-package.md).


Preparation partagee des textures de terrain et acquisition optionnelle depuis
la bibliotheque Studio raccordees. Paquet reel, Clippy, compilation Studio WASM
et regression native passes : PNG et deux buffers d'ombres inchanges.
Aucun terrain visible/edition Studio importe n'est encore qualifie.
[Textures et acquisition terrain](../evidence/tidewater/2026-09-27-shared-terrain-textures.md).


Recalcul normal/AO et graphe des mips transferes au moteur partage, avec invalidation
des anciennes generations lors d'une retouche. Clippy strict, compilation native,
retouche/retour CPU et remplacement GPU tardif passent. Rejeu natif identique
a la baseline precedente. Raccordement au terrain visible Studio restant.
[Normales partagees](../evidence/tidewater/2026-09-27-shared-terrain-normals.md).


Assemblage prepare du terrain importe ajoute (geometrie, textures, shaders,
normales, materiau, entite et empreinte documentaire). Clippy, preuve CPU de
remplacement et compilation Studio WASM passes. Prototype natif raccorde : PNG
et deux sorties d'ombres identiques. Raccordement visible Studio encore ouvert.
[Assemblage terrain](../evidence/tidewater/2026-09-27-terrain-scene-assembly.md).


Paquet terrain v2 avec shader d'ombres du relief prepare ; v1 reste lisible.
Empreintes, inventaires par version, lecteurs synchrone/asynchrone et Studio WASM
verifies. Ressources de bibliotheque reconstruites ; pas de nouveau verdict GUI.
Le pinceau exige encore le raccordement des collisions du terrain importe.
[Paquet terrain v2](../evidence/tidewater/2026-09-27-terrain-package-v2.md).


Projection des collisions du terrain importe ajoutee : 256 tuiles metriques,
remplacement par empreinte documentaire et curseur compatible avec le sampler.
Clippy, tests natifs de formes/jonctions et de synchronisation Rapier passes ;
256 colliders, retouche +12 m et restauration exacte verifies. Compilation
Studio WASM passee. Raccordement du viewport et geste Studio encore ouverts.
[Collisions importees](../evidence/tidewater/2026-09-27-sampled-terrain-collisions.md).

Controle de synchronisation physique importe disponible et verifie : refus
avant synchronisation, pour une ancienne empreinte et une pose locale non
appliquee ; acceptation de 256 tuiles courantes. Tests natifs et Studio WASM
passes. Ce controle reste a raccorder au cycle de projection du viewport.

Branchement du viewport importe en cours : ouverture conditionnee au raster
verifie et aux paquets, projection des revisions, ciel/relief/collisions, picking
apres synchronisation, CDLOD sur bornes cachees et navigation a la taille de
l'ile. L'eau, les objets, le playtest et le geste Studio restent ouverts.
[Viewport importe](../evidence/tidewater/2026-09-27-studio-imported-viewport.md).

Verification de ce branchement : compilation WASM, tests natifs de physique et
de selection camera passes ; capture native et deux sorties d'ombres identiques
a la precedente reference Forge. La nouvelle vue Studio reste NON QUALIFIEE
visuellement, avec eau/village/vegetation encore a raccorder. Clippy Studio
strict reste limite par bind_main preexistant (atelier_ui.rs:122).

Projection des placements de batiments importes raccordee dans le code Studio :
revisions exactes, meshes partages, materiaux lies au terrain, selection/gizmos
et cadrage. Probe natif PASS sur 23 batiments / 111 colliders, transformations,
duplication, suppression, restauration et raycasts ; Studio WASM compile.
Rendu et gestes GUI non qualifies ; ombres d'objets et decors residuels ouverts.
[Projection batiments](../evidence/tidewater/2026-09-27-studio-building-projection.md).

Raccordement des ombres des batiments editables : cascades partagees, poses
courantes des racines, publication des generations terminees et conservation
des racines/collisions lors des changements lumineux. Probe GPU natif PASS sur
les 107 parties opaques des 23 batiments : ombres initiales, transformation,
suppression complete et readback des trois couches. Aucun verdict GUI Studio,
ni performance interactive, ni parite de scene complete. Vegetation/eau restent
ouvertes. [Ombres editables](../evidence/tidewater/2026-09-27-studio-live-shadows.md).

Cache des bornes/corrections terrain pour les ombres de la camera raccorde au
Studio. Huit vues, retouche isolee et retours sources verifies ; mesure CPU locale
242,152 ms sans cache contre 12,607 ms avec cache pour huit preparations (pas un
benchmark FPS). Capture native et deux cartes d'ombres identiques a la baseline
Forge precedente. Compilation WASM PASS ; qualification interactive/GPU dynamique
et reutilisation des ressources GPU toujours ouvertes.
[Cache ombres terrain](../evidence/tidewater/2026-09-27-shadow-terrain-cache.md).

Materiau plantes, attributs, selection near/far et conversion canonique deplaces
dans tidewater_plants (moteur partage). La scene native utilise ce chemin :
capture et sorties d'ombres identiques a la precedente baseline, WASM compile.
Paquet vegetal portable produit et verifie (83 fichiers, 46 590 597 octets),
sans dupliquer les buffers de placements. Lecteur Rust, transport/catalogue
Studio, identites de picking et cycle de projection vegetal restent ouverts.
[Plantes partagees](../evidence/tidewater/2026-09-27-shared-plants.md).

Lecteur Rust du paquet vegetal et acquisition optionnelle Studio raccordes :
index/fermeture/tailles/SHA controles, tests sync/async et refus invalides PASS.
Bibliotheque locale avec les quatre paquets preparee et reverifiee, WASM compile.
Ressources GPU vegetales, population, picking et gestes Studio restent ouverts.
[Lecteur vegetal](../evidence/tidewater/2026-09-27-plant-package-reader.md).

Textures vegetales partagees et preparation dans le monde Studio raccordees :
cinq images du paquet reel verifiees, mipmaps/formats conserves, generations
isolees et entrees invalides refusees. Capture native et deux sorties d'ombres
identiques a la reference Forge precedente ; Studio WASM compile. Transport de
population, projection/picking et gestes GUI restent ouverts. Aucun acquittement
GPU Studio ni parite upstream complete revendiques.
[Textures vegetales](../evidence/tidewater/2026-09-27-plant-textures.md).

Transport natif des blocs vegetaux canoniques ajoute : fragments bornes et lies
a la tete de zone, assemblage verifie, commande Studio native/WASM compilee.
Tests de refus/corruption PASS et lecture reelle de 65 383 instances en 48
fragments PASS, sans mutation canonique. Consommateur asynchrone navigateur,
projection et edition/picking restent ouverts.
[Transport vegetal](../evidence/tidewater/2026-09-27-vegetation-transport.md).

Consommateur asynchrone Studio raccorde aux blocs canoniques : acquisition
atomique, annulation des generations remplacees et reutilisation de la base
identique. Probe du coeur partage PASS sur 65 383 instances (IDs/transforms
identiques a la lecture directe), refus async PASS, compilation WASM PASS.
Parcours IPC/GUI et projection GPU/picking encore non qualifies.
[Acquisition population](../evidence/tidewater/2026-09-27-population-acquisition.md).

Projection vegetale raccordee au canvas importe : preparation depuis le paquet
portable et la population canonique, IDs attaches aux meshes, selection near/far
selon camera et reception des ombres de scene. Probe ECS PASS sur 65 383 IDs,
retour camera et correction de hauteur ; capture native et ombres inchangees,
WASM compile. GUI non observee, picking/edition et casters vegetaux ouverts.
La premiere version recree les entites a chaque deplacement camera : optimisation
et performance interactive restent requises.
[Projection des plantes](../evidence/tidewater/2026-09-27-population-scene.md).

Projection residente raccordee : ressources conservees et differences d'entites
appliquees lors des changements de vue. Probe CPU PASS : selections identiques
a la methode precedente, 8 meshes/8 materiaux/12 buffers constants, repetitions
sans creation/suppression, camera invalide sans mutation et retrait complet.
WASM compile. Performance et rendu GPU interactif non qualifies ; picking ouvert.
[Plantes residentes](../evidence/tidewater/2026-09-27-resident-plants.md).

La table de picking des plantes importees refuse maintenant les IDs absents
des lots effectivement dessines par la vue capturee. Test unitaire et probe
sur 65 383 identites PASS, avec selection A/B/A et refus d'un ID cache ou
d'une tete perimee. [IDs visibles](../evidence/tidewater/2026-09-28-plant-picking-visible-ids.md).
La passe ID GPU native ajoute maintenant le terrain et 140 meshes opaques du
village a profondeur : 5 108 pixels masques et 37 IDs restants resolus dans la
tete canonique, avec deux readbacks compares pixel par pixel. L'occlusion par
source separee, le clic, la fraicheur camera/viewport et la retouche Studio
restent ouverts. [Preuve d'occlusion](../evidence/tidewater/2026-09-28-plant-picking-occlusion.md).

Un premier chemin de sÃ©lection est raccordÃ© au canevas du Studio : bouton
Nature pour les plantes importÃ©es, lecture GPU Ã  la taille rÃ©elle de vue,
masques terrain/village/objets d'auteur opaques et refus d'une rÃ©ponse pÃ©rimÃ©e
(zone, bibliothÃ¨que, camÃ©ra, projection, viewport, geste). La compilation WASM
passe ; l'attente GPU est bornÃ©e aux shaders de cette passe avec une Ã©chÃ©ance
explicite de 120 secondes. Le shader Standard a prouvÃ© 3 970 pixels masquÃ©s
sur GPU natif avec 0 ID survivant modifiÃ© ; le clic GUI et ce shader en WebGPU
restent non observÃ©s car le lancement du serveur de test a Ã©tÃ© refusÃ© par
l'auto-review. La sÃ©lection ne modifie aucune source. La retouche par dÃ©calage
stable d'une plante importÃ©e est maintenant dans le document de zone :
Preview/Apply, archive, Undo/Redo et suivi du relief testÃ©s cÃ´tÃ© Rust, champs
de dÃ©calage raccordÃ©s au panneau Nature et compilation WASM passÃ©e. Le geste
GUI rÃ©el de la pose au pointeur reste ouvert ; rotation et Ã©chelle
indÃ©pendantes des copies ainsi que le catalogue des couches vÃ©gÃ©tales importÃ©es
sont raccordÃ©s au panneau Nature et testÃ©s cÃ´tÃ© Rust.
[Raccordement Studio](../evidence/tidewater/2026-09-28-studio-plant-selection.md).
[Retouche persistante](../evidence/tidewater/2026-09-28-imported-plant-offset.md).
[Copie et pose](../evidence/tidewater/2026-09-28-authored-plant-copy.md).
[Catalogue et pose par coordonnÃ©es](../evidence/tidewater/2026-09-28-plant-catalogue.md).
[Occlusion Standard native](../evidence/tidewater/2026-09-28-standard-plant-occlusion.md).

Complement GPU de la projection residente : selections A/B/A avant rendu,
capture finale A et deux sorties d'ombres identiques a la precedente reference
Forge. Aucun verdict sur les frames intermediaires, la navigation GUI ou les FPS.
Voir le complement dans la preuve des plantes residentes.

Le candidat de promenade 025 regroupe le panneau natif de quinze reglages
graphiques, l'echoppe FishStand, les cinq poissons exposes et Joe charge depuis
une derivee PNG du GLB source. La comparaison cotiere appariee confirme une
luminance globale voisine mais pas une parite visuelle : ciel plus sombre,
details plus durs et bois de l'echoppe encore trop sombre. Le repere tangent
des normal maps du stand est maintenant corrige ; son eclairage PBR source,
les materiaux des poissons, les interactions et l'objet editable Studio restent
ouverts. Le nouveau bundle 011 est verifie ; le paquet Windows 025 est un
candidat local dont le controle integral du ZIP, le lanceur extrait et une
promenade native extraite ont passe. Les notices de licences et la signature
restent a achever avant diffusion publique. [Preuve graphique et FishStand](../evidence/tidewater/2026-09-29-graphics-and-fish-stand-025.md).

Mise a jour du 30 septembre : le playtest Studio expose dix reglages graphiques
reliÃ©s au rendu ; le candidat 020 a passe ouverture, sauvegarde/reouverture,
changement/reset de plusieurs effets et promenade. Les passes natives GTAO,
brume et flare bloquent encore le rendu WebGPU Studio lorsqu'elles sont
activees : leurs curseurs n'y sont pas proposes tant que leur adaptation
n'est pas qualifiee. Cette tranche ne comble pas l'ecart de finition visible
avec la demo publique ni l'absence d'ocean jouable dans Studio.
[Preuve Studio et limites](../evidence/tidewater/2026-09-30-studio-graphics-controls.md).

Le candidat Studio isolÃ© 096 ajoute les ombres projetÃ©es des cinq groupes de
vÃ©gÃ©tation proche du paquet Tidewater. Les mÃªmes instances que le rendu visible
sont sÃ©lectionnÃ©es avec leurs rayons source ; le sous-bois et l'herbe ne sont pas
des Ã©metteurs gÃ©omÃ©triques. Capture village et parcours Playtest passent, mais
la mesure d'image entiÃ¨re ne dÃ©montre pas encore la paritÃ© visuelle et la
stabilitÃ© prolongÃ©e en forÃªt reste Ã  vÃ©rifier.
[Preuve des ombres proches](../evidence/tidewater/2026-09-30-studio-near-vegetation-shadows.md).

Le candidat Studio 105 anime ces Ã©metteurs et suit la camÃ©ra sans remplacer
le graphe rÃ©sident. Le rÃ©cepteur du terrain utilisait des matrices statiques
face Ã  une profondeur devenue animÃ©e ; son raccordement au buffer partagÃ©
est corrigÃ© et la capture Village retrouve 9,52/255 d'Ã©cart de luminance
Ã  +0,10 EV. Le parcours placement/save/reopen/Playtest et les trois couches
animÃ©es passent ; la cadence rAF Studio mesurÃ©e reste Ã  environ 27 images/s.
L'optimisation, la paritÃ© temporelle et la traversÃ©e prolongÃ©e en forÃªt sont
toujours ouvertes. [Preuve et rÃ©gression corrigÃ©e](../evidence/tidewater/2026-09-30-studio-live-shadow-receivers.md).

Le candidat Studio 107 supprime ensuite les Ã©vÃ©nements de modification
inutiles des matÃ©riaux qui reÃ§oivent les ombres des nuages. Le parcours
complet passe et les deux fenÃªtres de cinq secondes passent de 27,01 / 26,63
Ã  107,31 / 136,94 images/s rAF dans le canvas Studio, avec les trois cascades
toujours animÃ©es. La capture Village 1920 Ã— 1080 conserve 9,54/255 d'Ã©cart
Ã  +0,10 EV. Les Ã -coups, la cadence plein Ã©cran, la forÃªt prolongÃ©e et la
paritÃ© temporelle restent Ã  qualifier.
[Preuve de l'optimisation](../evidence/tidewater/2026-09-30-studio-cloud-material-churn.md).

Le contrÃ´le continu en plein Ã©cran du 107 rÃ©vÃ¨le une limite majeure :
143,01 images/s rAF Ã  l'arrÃªt, 20,50 pendant la marche. Le profil retrouve
la prÃ©paration coÃ»teuse des matÃ©riaux et deux maillages renouvelÃ©s Ã  chaque
image. Le candidat 112 conserve dÃ©sormais les sÃ©lections exactes du terrain
et de l'ocÃ©an ; quatre contrÃ´les CPU et les constructions passent, mais
le gain en marche reste Ã  mesurer. Les essais 109/110 ne qualifient pas la
forÃªt, car leur transition intermÃ©diaire d'audit Ã©choue.
[Preuve et correction en qualification](../evidence/tidewater/2026-10-01-studio-walking-render-churn.md).

La sÃ©rie 113 du candidat 112 dÃ©marre directement en forÃªt et passe le parcours
complet, sources intactes et retour Ã  l'Ã©dition inclus. Douze mesures pendant
la marche couvrent 60,90 s et au moins 257,65 m horizontaux, avec trois
cascades actives dans une seule gÃ©nÃ©ration et aucun Ã©vÃ©nement de rÃ©cupÃ©ration.
La cadence demeure 21,42â€“22,74 images/s hors premiÃ¨re mesure instrumentÃ©e :
la stabilitÃ© fonctionnelle est prouvÃ©e sur ce trajet, la fluiditÃ© reste ouverte.
Le profil retrouve la spÃ©cialisation des matÃ©riaux comme coÃ»t principal.
La sÃ©rie Village 114 conserve 9,52/255 d'Ã©cart de luminance Ã  +0,10 EV.

Le candidat 115 corrige l'invalidation du cache d'Ã©clairage vÃ©gÃ©tal Ã  chaque
translation de camÃ©ra. La paire forÃªt 118/119, sans compilation concurrente,
mÃªme rÃ©solution 1920 Ã— 1080 et mÃªmes effets, passe avec sources intactes,
trois cascades actives et retour Ã  l'Ã©dition. La cadence rAF pendant la marche
passe de 21,35 Ã  105,54 images/s en moyenne sur onze segments non profilÃ©s.
Le compteur reste stable pendant la marche ; la spÃ©cialisation des matÃ©riaux
passe de 25,07 % Ã  0,21 % du temps CPU Ã©chantillonnÃ©. Le Village 120 conserve
9,56/255 d'Ã©cart de luminance. Les 1 % low restent variables, Clippy est rouge,
et ces preuves locales ne qualifient ni une prÃ©sentation constante Ã  60 Hz
ni le benchmark natif complet.
[Preuve du cache d'Ã©clairage](../evidence/tidewater/2026-10-01-studio-plant-light-cache.md).

Le candidat Studio 121 ajoute les requÃªtes de hauteur GPU Ã  partir du matÃ©riau
optique rÃ©ellement visible, aprÃ¨s FFT et ressac, avec lecture de l'heure et de
l'Ã©tape du shader consommÃ©. Les contrÃ´les natif/WASM et cinq tests du registre
passent. Les sÃ©ries isolÃ©es 122/123 passent en forÃªt puis en mer, sans rejet de
paquet et avec sources intactes ; au mÃªme point marin, la hauteur varie de
10,895 cm. La marche forÃªt reste Ã  108,76 images/s rAF en moyenne. Cette tranche
prÃ©pare TW-008/TW-012 : Studio utilise encore la marche terrestre gÃ©nÃ©rique et
la passe d'immersion n'est pas activÃ©e. La nage, le masque de surface, leur
raccordement Studio et la comparaison directe Ã  l'oracle source restent ouverts.
Clippy moteur reste rouge (64 diagnostics), sans diagnostic dans le nouveau
module de requÃªtes. L'objectif complet n'est pas clos.
[Preuve des hauteurs GPU Studio](../evidence/tidewater/2026-10-01-studio-surface-water-queries.md).

Le candidat 129 raccorde le contrÃ´leur Tidewater au Playtest Studio et actualise
le point GPU du joueur depuis son ancre rÃ©elle. La sÃ©rie 131 passe marche plage,
nage, flottaison, plongÃ©e C/Ctrl, remontÃ©e Espace, retour plage et Ã©dition,
avec deux points GPU valides et sources intactes. La sÃ©rie forÃªt 132 passe le
rendu Ã  110,76 images/s rAF en moyenne et conserve les trois cascades ; elle
rÃ©vÃ¨le toutefois une accumulation anormale de vitesse verticale sur pente.
La locomotion forestiÃ¨re reste Ã  corriger/qualifier avant de conclure TW-012.
La passe d'immersion, le masque visible et les Ã©chelles Studio restent ouverts,
ainsi que les autres comportements et la paritÃ© complÃ¨te. Clippy Studio reste
rouge Ã  31 diagnostics. L'empaquetage refuse dÃ©sormais un frontend sans
dÃ©marrage Web, aprÃ¨s un premier candidat de test construit sans cette option.
[Preuve de nage et dÃ©faut sur pente](../evidence/tidewater/2026-10-01-studio-swimming.md).

Le candidat 134 corrige la perte de contact source sur les pentes oÃ¹ le KCC
Rapier confirme un appui non glissant. Les 30 tests natifs ciblÃ©s passent,
dont les rampes Ã  Â±40Â°, saut et glissement Ã  60Â°. Le parcours Studio 135 passe
avec 295 relevÃ©s, 144 appuis confirmÃ©s et zÃ©ro perte de contact/accumulation de
gravitÃ© sur ces appuis ; le rendu reste Ã  112,91 images/s rAF avec trois
cascades actives. Le parcours cÃ´tier 136 passe Ã©galement ses huit Ã©tapes,
avec requÃªtes GPU et sources intactes. Les phases de glissement raide restent
distinctes et toute l'Ã®le n'est pas qualifiÃ©e. Clippy reste rouge : 64
diagnostics moteur, 31 Studio ; aucun lint n'est dÃ©sactivÃ©. La prochaine tranche
porte sur le rendu d'immersion, toujours absent dans Studio. L'objectif complet
reste actif ; TW-008/TW-012 ne sont pas clÃ´turÃ©s.
[Preuve du contact sur pente](../evidence/tidewater/2026-10-01-studio-slope-contact.md).

Le candidat 137 raccorde la passe de milieu sous-marin et le masque visible au
Playtest Studio, avec les mÃªmes FFT, ressac, matÃ©riau et requÃªtes GPU que lâ€™eau
visible. Le rÃ©glage local est mÃ©morisÃ©, les anciennes prÃ©fÃ©rences sont conservÃ©es.
Les 8 tests du milieu, 9 dâ€™horloge et 30 du contrÃ´leur passent ; WASM, Tauri et
le contrÃ´le de construction de la promenade native passent. Lâ€™audit de nage 139
passe. Les comparaisons figÃ©es 140 passent en air, vue mixte et immersion ;
leurs rÃ©pÃ©titions donnent des PNG identiques et la simulation reprend aprÃ¨s
chaque triplet. Les sources restent intactes. Lâ€™audit physique 140 reste rouge
sur la neutralitÃ© libre car la plongÃ©e atteint le clamp du fond ; il ne remplace
pas la preuve 139. Clippy reste rouge (64 moteur, 31 Studio). Cette tranche
qualifie le raccordement local du milieu, pas la paritÃ© source complÃ¨te,
les caustiques/shafts, TAAU, les Ã©chelles Studio ou la livraison finale.
TW-008/TW-012 et lâ€™objectif complet restent ouverts.
[Preuve dâ€™immersion Studio](../evidence/tidewater/2026-10-01-studio-immersion.md).

Le candidat 141 raccorde les deux cartes de photons FFT et les rayons de
caustiques Ã  demi-rÃ©solution, avec un rÃ©glage local Â« Rayons sous-marins Â».
Neuf tests moteur, WASM, Tauri et le check de la promenade native passent.
Le parcours 142 passe mais son contrÃ´le de pixels refuse un effet trop faible
dans les vues de surface et immergÃ©e horizontale ; cet Ã©chec est conservÃ©.
La sÃ©rie 143, sur le mÃªme candidat Ã  lâ€™intensitÃ© source 1, passe la comparaison
figÃ©e dirigÃ©e vers le soleil rÃ©fractÃ© et le contrÃ´le sans effet depuis la plage.
La rÃ©pÃ©tition restitue le mÃªme PNG, et la simulation/visÃ©e reprennent ensuite.
Les prÃ©fÃ©rences migrent et les sources restent intactes. Clippy conserve
64 diagnostics moteur et 31 Studio. La paritÃ© source, les receveurs sous-marins
complets, la torche, TAAU, la qualification native des rayons seuls et le package
final restent ouverts ; ni TW-008/TW-012 ni lâ€™objectif complet ne sont clos.
[Preuve des rayons de caustiques](../evidence/tidewater/2026-10-01-studio-caustic-shafts.md).

La rÃ©gression native 145 passe aussi ses sept Ã©tapes et ses trois paires de
milieu Ã  signatures identiques, aprÃ¨s un Ã©chec 144 de prÃ©paration du gel Ã  la
surface conservÃ© au rapport. Le protocole de capture accepte dÃ©sormais des
reprises bornÃ©es avec les commandes de nage, sans Ã©largir la tolÃ©rance de 2 cm
ni modifier le moteur de locomotion. Le run 145 accepte son premier gel et
nâ€™exerce pas un rejet/reprise. Les rayons natifs seuls et la paritÃ© source
restent Ã  qualifier.

La suite du 1er octobre valide les cartes d'Ã©clairage immergÃ© en natif et dans
Studio (151), puis la rÃ©fraction des ombres : piles du ponton en natif (159),
piles et accessoires dans Studio (165), et fond sableux sÃ©parÃ©ment (166).
La collecte des casters inclut dÃ©sormais ponton, passerelles et accessoires de
place ; le reÃ§u GPU suit la camÃ©ra finale aprÃ¨s drainage de l'ocÃ©an. Les Ã©checs
antÃ©rieurs restent conservÃ©s. Chaque nouvelle preuve possÃ¨de ses comparaisons
figÃ©es, sa rÃ©pÃ©tition RGB exacte et son parcours de retour, avec source intacte.
Ces succÃ¨s locaux ne clÃ´turent ni TW-008/TW-012 ni la paritÃ© de l'Ã®le.
[Preuve des rÃ©cepteurs et du terrain](../evidence/tidewater/2026-10-01-refracted-underwater-shadows.md).

La prochaine tranche raccorde une passe de rÃ©fraction sous-marine sÃ©parÃ©e,
couleur Ã©clairÃ©e RGBA16F, couverture et profondeur reversed-Z Ã  demi-rÃ©solution.
Le terme monochrome source dÃ©signe uniquement les caustiques sans dispersion,
pas une capture en gris. Les tests CPU, lectures de textures GPU et l'effet
local du nouveau rÃ©glage passent sur les candidats 168/169 et parcours 170/171,
avec rÃ©pÃ©tition exacte et sources prÃ©servÃ©es. La qualification native GPU,
les cas d'occlusion et la performance restent Ã  complÃ©ter ; le TAAU est ouvert.
[Preuve de capture du fond](../evidence/tidewater/2026-10-01-studio-refraction-capture.md).

La mesure en mouvement 172 passe avec sa rÃ©gression complÃ¨te sur le candidat
169, mais rÃ©vÃ¨le une saccade de premier dÃ©placement jusqu'Ã  430,7 ms. Le profil
173 reproduit ce phÃ©nomÃ¨ne et montre notamment le coÃ»t de sÃ©lection/tri vÃ©gÃ©tal.
Son audit visuel reste en Ã©chec : la rÃ©gion tÃ©moin fixe touche cette fois l'eau.
La correction 174 mutualise la sÃ©lection entre LOD et cache les clÃ©s du tri,
en conservant membres, ordre et capacitÃ©s. Son candidat construit passe le
parcours complet 175, sans gain de cadence dÃ©montrÃ© ; la saccade reste prÃ©sente.
Le diagnostic 179 mesure 10,4 Go d'Ã©critures de meshes sur 50,4 s et motive une
rÃ©utilisation de gÃ©omÃ©trie opaque 180. Son parcours complet Studio 181 et ses
audits passent : dÃ©bit des grandes Ã©critures rÃ©duit de 77,51 %, cadence locale
instrumentÃ©e de 108â€“115 callbacks/s contre 64â€“68 au 179. La saccade initiale
reste Ã  476,3 ms ; ces essais sÃ©quentiels ne garantissent pas une cadence
universelle. Une modification externe de lib.rs pendant le build limite la
reproductibilitÃ© complÃ¨te des sources ; les identitÃ©s des binaires et les cinq
fichiers de la correction restent tracÃ©s. TW-008/TW-012 et la paritÃ© restent ouverts.
[Mesures et limites](../evidence/tidewater/2026-10-01-studio-refraction-motion.md).

Le raccordement natif de la capture de rÃ©fraction rÃ©utilise maintenant la
passe partagÃ©e Bevy et expose le contrÃ´le graphique natif. AprÃ¨s les Ã©checs
de prÃ©paration/observation conservÃ©s, le candidat 215 passe ses 24 tests CPU,
le triplet statique 216 et le parcours de nage/ombres 217. Les audits indÃ©pendants
confirment l'effet local, les lectures HDR/profondeur rÃ©elles, les rÃ©pÃ©titions
exactes et les critÃ¨res de locomotion inchangÃ©s. La compilation WebGPU du
module partagÃ© passe ; aucun nouveau parcours Studio n'est dÃ©duit de ce check.
Le manifeste ne reprÃ©sente que 49 entrÃ©es de produit et les archives identifiÃ©es,
pas un snapshot intÃ©gral du dÃ©pÃ´t. La paritÃ© avec une mÃªme horloge source,
la saccade initiale et les contenus/comportements restants gardent leurs critÃ¨res.
TW-008/TW-012 et l'objectif complet restent ouverts.
[Preuves natives et Ã©checs conservÃ©s](../evidence/tidewater/2026-10-01-native-refraction-capture.md).

La prochaine tranche doit produire et lire sur GPU les vitesses RGBA16F,
le masque RGBA8 et la profondeur finale aprÃ¨s l'eau avant de porter le resolve
TAAU et l'ordre beauty HDR source. Le contrat CPU Halton32 passe cinq tests
isolÃ©s ; il n'est pas exportÃ© et ne possÃ¨de encore aucun raccordement ECS/GPU.
Le mode source utilise les sommets dÃ©formÃ©s courants pour arbres/herbe/eau ;
les placements Ã©ditÃ©s restent suivis par identitÃ© et ne doivent pas disparaÃ®tre
derriÃ¨re un calcul camÃ©ra gÃ©nÃ©ral. Les cas de reef/transparence gardent leurs
contrats distincts.
[Conception vÃ©rifiÃ©e des entrÃ©es temporelles](../evidence/tidewater/2026-10-01-temporal-auxiliary-design.md).

Le 3 octobre, le transport des lampes vers le ShaderBuffer partagÃ© passe sur
le GPU Bevy existant : trois lectures vÃ©rifient le paquet valide, sa remise Ã 
zÃ©ro aprÃ¨s inventaire invalide et sa rÃ©cupÃ©ration. AprÃ¨s la sonde synthÃ©tique,
la sonde rÃ©elle projette 32 lampes sur 19 racines canoniques des bÃ¢timents et
sÃ©lectionne huit lampes pour la camÃ©ra. Les quatre bÃ¢timents sans attestation
restent signalÃ©s. Clippy de la sonde v2 termine sans diagnostic possÃ©dÃ© ; les
sources figÃ©es et les fichiers possÃ©dÃ©s restent identiques Ã  leurs pins.
Cet essai ne dessine aucun bÃ¢timent et ne raccorde pas encore Studio/Runtime :
le consommateur matÃ©riau visible, les autres owners et le publisher solaire
restent Ã  intÃ©grer. Aucun critÃ¨re de paritÃ© ou de livraison n'est clos.
[Transport GPU et portÃ©e exacte](../evidence/tidewater/2026-10-03-source-light-material-upload.md).

Le parcours natif attempt.3 vÃ©rifie ensuite une retouche de bÃ¢timent sauvegardÃ©e,
le passage en Playtest et le retour en Ã©dition : la position est relue dans
l'inspecteur commun, les bÃ¢timents et le ponton rÃ©apparaissent automatiquement,
les 22 autres placements et la base restent identiques. Il ne prouve pas le
redÃ©marrage complet ni les pixels du bÃ¢timent dÃ©placÃ© isolÃ©ment.
[Parcours et archive rÃ©ellement rÃ©fÃ©rencÃ©e](../evidence/tidewater/2026-10-03-studio-library-resume.md).

L'absence d'ocÃ©an en Ã©dition est reproduite puis corrigÃ©e dans la prÃ©paration
et la camÃ©ra. Le frontend release compile et son assemblage avec worker termine ;
le candidat natif attempt.4 est en construction. L'affichage de l'eau n'est pas
encore vÃ©rifiÃ© sur ce candidat et aucun critÃ¨re graphique n'est clos.
[Correction et continuation conservÃ©e](../evidence/tidewater/2026-10-03-studio-editor-water.md).

Les trois lampes de chemin sont acquises, converties et vÃ©rifiÃ©es avec catalogue
et lecteur Rust isolÃ©s ; elles ne sont pas encore installÃ©es dans Studio.
Les onze lanternes du quai ont leur correspondance source exacte vÃ©rifiÃ©e et un
adaptateur moteur prÃ©parÃ©. Un essai ECS sur la vraie gÃ©omÃ©trie vÃ©rifie le pivot
animÃ©, une copie dÃ©placÃ©e et agrandie, les phases et le retrait ; Clippy ciblÃ©
passe. Le module reste non dÃ©clarÃ© pendant les builds concurrents : le raccord
produit et les pixels lumineux sont encore Ã  prouver.
[Lampes de chemin](../evidence/tidewater/2026-10-03-path-light-source-acquisition.md),
[Lanternes du quai](../evidence/tidewater/2026-10-03-pier-light-bindings.md).

### Suite du 3 octobre : eau en Ã©dition et lanternes du quai

Le candidat natif attempt.4 affiche maintenant l'eau animÃ©e dans l'Ã©diteur.
Le parcours rÃ©el ouvre le workspace, charge une fois les paquets, passe au
Playtest puis revient : terrain, village, ponton et eau se restaurent sans
nouveau chargement manuel. Les pins de la carte source et de la copie sont
inchangÃ©s. [Preuve et limites](../evidence/tidewater/2026-10-03-studio-editor-water.md).
La vitesse exacte, les comparaisons numÃ©riques et la paritÃ© complÃ¨te restent
ouvertes ; ce parcours n'ajoute pas de nouvelle qualification d'Ã©dition.

Le consommateur des onze lanternes du quai est prÃ©parÃ© sur un snapshot isolÃ©
(1 344 fichiers), Ã  partir du mÃªme candidat. Il partage l'inventaire canonique
ManualPlacement et le collecteur lumineux des bÃ¢timents ; la vÃ©rification WASM
est en cours. L'essai ECS du module confirme positions, copies, mouvements de
pivot et refus d'une identitÃ© ambiguÃ«, mais ne qualifie pas les pixels Studio.
[Raccordement et preuves](../evidence/tidewater/2026-10-03-pier-light-bindings.md).

### Suite du 4 octobre : camÃ©ra Studio, ombres et Ã©dition commune

Le candidat figÃ© `studio-scene-camera-20261004-v1` construit frontend, worker et shell natif avec sources prÃ©servÃ©es. La correction du filtre de camÃ©ra exclut le captureur de rÃ©fraction des outils de scÃ¨ne ; un vrai glissement droit dans Studio confirme rotation et stabilisation. Le parcours runtime V3 observe une simulation active et vÃ©rifie cadence des trois cascades dâ€™ombres, vent et readback du receiver cohÃ©rent avec la camÃ©ra courante.

Le parcours dâ€™Ã©dition isolÃ© V3 passe six cas, dont une plante sÃ©lectionnÃ©e rÃ©ellement sur le GPU : transformations, copie/verrouillage, deux sauvegardes/rÃ©ouvertures, suppressions et annulation/rÃ©tablissement sans rÃ©surrection. Sources et mÃ©tadonnÃ©es vÃ©gÃ©tales prÃ©servÃ©es. La copie du harness adapte uniquement lâ€™attente de lâ€™inspecteur public et le budget CDP ; les critÃ¨res de persistance sont conservÃ©s. Les Ã©checs prÃ©cÃ©dents restent tracÃ©s. Cela ne clÃ´t pas la paritÃ© visuelle, les autres comportements, les performances, les gates ni le paquet final.
[Preuves, checkpoints et limites](../evidence/tidewater/2026-10-03-solar-environment.md).

### Suite du 4 octobre : accessoires de cour manquants

La comparaison native Village identifie des accessoires absents. Deux acquisitions de la source figÃ©e vÃ©rifient maintenant trente objets distincts (clÃ´tures, linge, sÃ©choirs, petits objets, barque sur trÃ©teaux, seau et trois lampes), 12 261 triangles, 422 fichiers identiques. Le vÃ©rificateur confirme indices et attributs contre source-017 et absence de chevauchement avec les bÃ¢timents. Ce contenu reste une acquisition : conversion portable, lecteurs et placements Ã©ditables Studio Ã  intÃ©grer. Les lampes dÃ©jÃ  acquises doivent rÃ©utiliser leurs identitÃ©s ; FishProps et accessoires de plage restent sÃ©parÃ©ment Ã  acquÃ©rir. [Preuves et limites](../evidence/tidewater/2026-10-04-village-prop-source-acquisition.md).

Les accessoires de cour ont ensuite deux conversions portables identiques : 27 nouveaux prototypes, 51 parties, 11 949 triangles et 297 fichiers. Les trois lampes conservent leur paquet canonique pour Ã©viter les doublons. Les triangles avec paramÃ¨tres matÃ©riau sont vÃ©rifiÃ©s contre source-017 ; audit indÃ©pendant des rÃ©fÃ©rences, bounds et collisions PASS, erreur de retour des positions 0 m. Le lecteur Rust et le consommateur Studio restent Ã  raccorder ; FishProps et accessoires de plage restent ouverts. [Conversion et limites](../evidence/tidewater/2026-10-04-village-prop-portable-package.md).

Le lecteur Rust des 27 accessoires est maintenant dÃ©clarÃ© dans forge_assets, avec une seule ligne ajoutÃ©e au registre et ses octets prÃ©existants prÃ©servÃ©s. Le vrai paquet passe les transports synchrone/asynchrone, le catalogue standard et les refus de fichier manquant/altÃ©rÃ© et manifeste changÃ© ; Clippy ciblÃ© PASS. Le mÃªme module compile en mÃ©tadonnÃ©es WASM avec dÃ©pendances exactes du graphe, sources inchangÃ©es. Ce ne sont pas des contrÃ´les Cargo complets ni une disponibilitÃ© Studio : transport frontend, gÃ©omÃ©trie Bevy/Rapier et placements communs restent Ã  raccorder. [Lecteur, preuves et limites](../evidence/tidewater/2026-10-04-village-prop-rust-reader.md).

Le consommateur Bevy/Rapier des 27 accessoires est prÃ©parÃ©. Trois objets source n'ont pas de collision propre ; un marqueur commun dÃ©rivÃ© et liÃ© Ã  l'identitÃ© permet ce cas explicite, tout en conservant le refus des primitives manquantes ou contradictoires. Validation native du contrat en cours, erreur de chemin de tests du harness diagnostiquÃ©e et rejeu frais prÃ©parÃ© ; aucun PASS nouveau ni rendu de l'adaptateur dÃ©clarÃ©. [PrÃ©paration et Ã©tat vÃ©rifiÃ©](../evidence/tidewater/2026-10-04-village-prop-engine-adapter.md).

Le suivi du consommateur est corrigÃ© pour les zÃ©ro/une/deux primitives rÃ©ellement prÃ©vues par prototype, avec contrÃ´le du rattachement Ã  chaque racine. Les identitÃ©s du lecteur concordent maintenant exactement avec les `source_id` du paquet ; les anciens reÃ§us ne couvrent pas ce hash corrigÃ©. Des tests sur les 27 prototypes rÃ©els couvrent projection commune, Rapier et collider dÃ©tachÃ©. Les essais prÃ©cÃ©dents du harness (chemin des tests, bibliothÃ¨que Windows, variantes Bevy mÃ©langÃ©es) restent conservÃ©s ; rejeux natifs en cours avec dÃ©pendances exactes. DisponibilitÃ© Studio, rendu et paritÃ© demeurent ouverts.

Les rejeux ont ensuite terminÃ© : contrat commun V3, trois tests PASS ; adaptateur V2, cinq tests moteur et un test lecteur rÃ©el PASS, zÃ©ro ignorÃ©/filtrÃ©, sources inchangÃ©es. Le consommateur est dÃ©clarÃ© dans forge_engine avec prÃ©servation exacte du registre existant. Il s'agit de tests isolÃ©s contre dÃ©pendances Ã©pinglÃ©es, sans build Cargo complet ni exÃ©cution GPU. Clippy ciblÃ© en cours ; contrÃ´le Web corrigÃ© et raccordement du paquet au transport, aux placements et Ã  l'affichage Studio restent requis.

Clippy ciblÃ© du lecteur corrigÃ© PASS ; mÃ©tadonnÃ©es WASM V4 PASS. Clippy moteur V3 PASS aprÃ¨s ajout des seules sections de documentation d'erreurs publiques, avec retour exact aux octets du module testÃ© dÃ©montrÃ©. Les tests du comportement restent ceux du V2 ; aucune capture Studio ni paritÃ© des nouveaux accessoires n'est dÃ©clarÃ©e. Le raccordement transport/catalogue, sources et retouches communes, rendu/ombres et Ã©dition persistante de ces objets reste la prochaine tranche.

Le raccordement Studio des accessoires de cour est implÃ©mentÃ© : transport Ã©pinglÃ©, templates de sources associÃ©s au terrain, partition des placements, scÃ¨ne commune, gizmo/focus, receipts, retrait et collecte des ombres. L'audit du site vÃ©rifie 296 fichiers et refuse un payload altÃ©rÃ© avant crÃ©ation de site. Candidat complet figÃ© sur 1 336 sources, compilation frontend et prÃ©paration de bibliothÃ¨que en cours. Affichage rÃ©el, Ã©dition persistante et catalogue produit rÃ©utilisable restent Ã  vÃ©rifier/complÃ©ter ; les paquets procÃ©duraux ne sont pas prÃ©sentÃ©s comme des GLB du navigateur gouvernÃ©. [Raccordement et limites](../evidence/tidewater/2026-10-04-village-prop-studio-wiring.md).

La bibliothÃ¨que complÃ¨te du candidat est ensuite terminÃ©e code 0 : 2 084 fichiers vÃ©rifiÃ©s, sources prÃ©cÃ©dentes inchangÃ©es et champs de l'index conservÃ©s. Compilation frontend toujours active (session 26664, Cargo 23220 confirmÃ©), sans PASS anticipÃ©. Assemblage/worker/shell natif prÃ©parÃ©s mais non exÃ©cutÃ©s ; contrÃ´les rÃ©els d'affichage et d'Ã©dition restent ouverts.

Pendant cette compilation, l'acquisition de plage vÃ©rifie 43 objets distincts, 78 parties et 18 807 triangles, avec 548 fichiers identiques sur deux extractions. Attributs et indices concordent exactement avec source-017, sans chevauchement avec bÃ¢timents ou accessoires de cour. Le runtime Node 22.22.3 retrouve l'empreinte globale initiale ; Node 24 est explicitement refusÃ© avant sortie aprÃ¨s diagnostic conservÃ©. Conversion, moteur, placements Studio et FishProps restent ouverts. [Preuves et limites](../evidence/tidewater/2026-10-04-beach-prop-source-acquisition.md).

La plage a ensuite deux conversions portables identiques : 43 prototypes, 78 parties, 18 807 triangles et 425 fichiers. Audit indÃ©pendant des rÃ©fÃ©rences, bounds, matÃ©riaux et collisions PASS ; erreur maximale de retour des positions 1,1921e-7 m. Lecteur, moteur, transport/catalogue et placements Studio restent Ã  raccorder ; ce paquet n'entre pas dans le candidat village encore en compilation. [Conversion et limites](../evidence/tidewater/2026-10-04-beach-prop-portable-package.md).

La compilation frontend village est ensuite terminÃ©e code 0, sources figÃ©es
inchangÃ©es. Assemblage bindgen et worker de navigation PASS ; staging natif
vÃ©rifiÃ© et shell Tauri en construction (session 20830). La fixture d'Ã©dition
dispose de 2 097 fichiers frontend vÃ©rifiÃ©s, sans parcours GPU nouveau.

Lecteur Rust et projection Bevy/Rapier des 43 accessoires de plage prÃ©parÃ©s.
Premier test du vrai paquet PASS ; Clippy courant lecteur/moteur PASS et
mÃ©tadonnÃ©es WASM lecteur PASS, inputs inchangÃ©s. Tests natifs de projection et
physique en cours (72600). Le transport du site vÃ©rifie 424 payloads et refuse
une altÃ©ration avant crÃ©ation de destination. Enregistrement des modules,
raccordement Studio et Ã©dition/rendu restent ouverts.
[Preuves et limites](../evidence/tidewater/2026-10-04-beach-prop-rust-integration.md).

Les tests natifs plage terminent ensuite code 0 : cinq tests moteur et un
lecteur rÃ©el, zÃ©ro ignorÃ©/filtrÃ©, inputs inchangÃ©s. Les deux modules sont
dÃ©clarÃ©s en prÃ©servant leurs registres. Raccordement Studio Ã©crit dans cinq
fichiers, avec sauvegardes et contrÃ´le prÃ©alable des changements concurrents :
transport, templates, placements communs, scÃ¨nes/receipts, focus/gizmo et ombres.
Le contrÃ´le Cargo/WASM du raccordement est la prochaine preuve ; rendu,
Ã©dition persistante et catalogue produit restent ouverts.

Le contrÃ´le Cargo release WASM du raccordement plage termine ensuite code 0
sur 1 338 sources figÃ©es inchangÃ©es. Le candidat village natif est aussi construit
et son inspection de cÃ´te/camÃ©ra rÃ©ussit aprÃ¨s correction d'encodage du script.
Parcours d'Ã©dition complet village en cours (84197), sans PASS anticipÃ©.

L'acquisition FishProps couvre maintenant 78 instances et 18 prototypes :
poissons entiers/ouverts/coupÃ©s, homards, glace et feuilles. Deux extractions
de 227 fichiers identiques, mÃªmes buffers GPU que la source et mÃªme gÃ©omÃ©trie
fusionnÃ©e ; vÃ©rificateur indÃ©pendant PASS, altÃ©ration identique refusÃ©e.
Le matÃ©riau dÃ©diÃ©, les dÃ©formations, LOD/proxies et placements Rust/Studio
restent Ã  porter. [Preuves et limites](../evidence/tidewater/2026-10-04-fish-prop-source-acquisition.md).

Le candidat plage complet est dÃ©sormais liÃ© et construit en natif :
1 338 sources figÃ©es inchangÃ©es, 2 520 fichiers frontend vÃ©rifiÃ©s et fixture
indÃ©pendante prÃ©parÃ©e. L'inspection/rendu et l'Ã©dition de plage restent Ã 
exÃ©cuter. Le parcours village a rÃ©vÃ©lÃ© deux dÃ©fauts d'instrumentation
(quota du journal, puis statut de fin publiÃ© pendant la compression),
corrigÃ©s et couverts par un test de persistance sans perte. Le rejeu complet
v4 reste actif, session 88574 ; aucun PASS global d'Ã©dition anticipÃ©.
[Ã‰tat du candidat plage](../evidence/tidewater/2026-10-04-beach-prop-rust-integration.md)
et [diagnostic de l'Ã©dition](../evidence/tidewater/2026-10-04-village-prop-studio-wiring.md).

FishProps dispose maintenant d'un paquet portable de 250 payloads et d'un
lecteur Rust enregistrÃ©, vÃ©rifiÃ©s nativement et en WASM. Les poses Rust
concordent avec les 169 709 sommets de la rÃ©fÃ©rence GPU, erreur maximale
7,6293945e-6 m. Le prÃ©parateur moteur enregistrÃ© conserve 78 templates,
234 maillages/LOD et les entrÃ©es du shader procÃ©dural ; tests et Clippy PASS.
MatÃ©riau dÃ©diÃ©, culling/fondu/proxies, projection commune et raccordement
Studio restent Ã  implÃ©menter. Ce travail ne remplace pas les comparaisons
visuelles de la carte complÃ¨te ni les comportements et livrables de jeu.
[Preuves dÃ©taillÃ©es et limites](../evidence/tidewater/2026-10-04-fish-prop-source-acquisition.md).
Le matÃ©riau FishProps possÃ¨de aussi une rÃ©fÃ©rence raster WebGPU complÃ¨te :
1 560 cas avec les paramÃ¨tres des 78 instances, corps surface source intact,
dÃ©rivÃ©es et discard exÃ©cutÃ©s. Deux sorties byte-identiques ; vÃ©rification
indÃ©pendante PASS (finitude, normales, clearcoat, couverture des nageoires).
Domaines plans diagnostiques seulement, sans Ã©clairage ni carte rendue ;
le matÃ©riau dÃ©diÃ© Rust et le raccordement Studio restent ouverts.
[RÃ©fÃ©rence et limites](../evidence/tidewater/2026-10-04-fish-prop-source-acquisition.md).

La bibliothÃ¨que de surface FishProps assemblÃ©e par Rust passe natif/Clippy,
WASM et comparaison raster GPU des 1 560 cas : tous les plans intended identiques
Ã  l'oracle ; le mode ThreeReference ne change que specular vers zÃ©ro.
MatÃ©riau Bevy, Ã©clairage et projection Studio restent ouverts.
Le parcours village v4 termine sans PASS : Ã©dition et dix suppressions Undo/Redo
passÃ©es, attente du dernier Save non rÃ©solue, diagnostic transport ajoutÃ©.
L'inspection native plage v2 rÃ©ussit et le parcours complet plage est lancÃ©
(session 35931). Ces rÃ©sultats ne qualifient pas la carte complÃ¨te.

FishMaterial est maintenant dÃ©fini et enregistrÃ© dans le module moteur,
avec sa table peau, les paramÃ¨tres source et la fermeture d'Ã©clairage commune.
Native isolÃ© : 14 tests PASS, Clippy ciblÃ© PASS ; WASM mÃ©tadonnÃ©es PASS ; les deux
pipelines GPU de fermeture se crÃ©ent sans erreur, imports camÃ©ra/mesh de sonde.
Aucun draw Bevy/Studio ni proxy d'ombre FishProps encore prouvÃ©. Le parcours
plage 35931 reste actif : premiÃ¨re sauvegarde/rÃ©ouverture passÃ©es, suppressions
en cours (observation authoring Apply 86). Source/candidat en cours conservÃ©s.
[Preuves et portÃ©e exacte](../evidence/tidewater/2026-10-04-fish-prop-source-acquisition.md).

Suite : parcours plage 35931 terminal en Ã©chec sur Save final, aprÃ¨s dix cas et
cycles de suppression, source/frontend conservÃ©s. Aucun PASS de persistance.
FishProps : projection moteur vers placements communs Ã©crite, 18 tests natifs
isolÃ©s et Clippy PASS ; culling source comparÃ© sur 3 432 cas. Nouveau shader de
fondu source : deux pipelines WebGPU PASS avec imports de sonde, aucun dessin
Studio. Raccord catalogue/transport Studio et ombre masquÃ©e toujours ouverts.
Voir les rapports FishProps et plage pour reÃ§us, Ã©checs conservÃ©s et limites.

Suite : [coÃ»t du rejeu historique et correctif Save](../evidence/tidewater/2026-10-04-studio-save-history-replay.md).
Le rejeu natif des 101 archives plage prend 149,8 s, au-delÃ  du dÃ©lai du test.
Le magasin conserve dÃ©sormais un rÃ©sultat de rejeu par empreinte tout en
revÃ©rifiant octets, identitÃ©s, sources et budgets. Trois archives : 15,4 s froid,
71 ms chaud ; Cargo/99 tests projet et Clippy all-targets PASS. Aucun Save Studio
corrigÃ© encore qualifiÃ©. Le rÃ©sident poissons passe aussi WASM mÃ©tadonnÃ©es.

Le candidat Save corrigÃ© est construit et inspectÃ© : EXE 6507967b, frontend
plage inchangÃ©, 1 338 sources intactes ; nouvelle fixture de 25 fichiers et
2 520 fichiers frontend vÃ©rifiÃ©s. Rejeu complet d'Ã©dition plage lancÃ©,
session 67213, dÃ©lai 120 s conservÃ©. Aucun PASS global de persistance anticipÃ©.
[Ã‰tat et reÃ§us du correctif](../evidence/tidewater/2026-10-04-studio-save-history-replay.md).

FishProps : lecteur paquet et 78 identitÃ©s source raccordÃ©s Ã  la bibliothÃ¨que
et au binding commun d'inventaire. Cargo WASM Studio code 0 sur 1 347 sources
figÃ©es intactes ; natif moteur 18 tests et Clippy PASS. BibliothÃ¨que indÃ©pendante
2 760 fichiers vÃ©rifiÃ©s, non dÃ©ployÃ©e. Projection visible/matÃ©riau/ombre encore
ouverts. Rejeu Save corrigÃ© 67213 toujours actif (59 Apply observÃ©s, aucun Save
Ã  ce point). [Raccord et limites](../evidence/tidewater/2026-10-04-fish-prop-studio-library.md).

FishProps : projection viewport dÃ©diÃ©e Ã©crite, matÃ©riau/Ã©clairage commun,
frustum/LOD, focus et readiness communs raccordÃ©s. Cargo WASM v1 code 0 sur
1 348 sources figÃ©es ; build frontend 97954 en cours, aucun draw encore prouvÃ©.
Snapshot v2 corrige l'actualisation des cascades/nuages Ã  handle constant et
attend son contrÃ´le aprÃ¨s libÃ©ration du cache. Save corrigÃ© 67213 : premier
Save rÃ©solu en 2,15 s puis Close/Open ; suppressions encore en cours (91 Apply).
Verdict final de persistance et paritÃ© toujours ouverts.

Suite du 4 octobre : builds frontend FishProps v1 et v2 terminÃ©s code 0,
contrÃ´le Cargo v2 terminÃ©, frontend v2 complet assemblÃ© avec 2 771 fichiers
Ã©pinglÃ©s et worker navigation vÃ©rifiÃ©. Aucun dessin GPU ni picking prouvÃ©.
Save 67213 termine en Ã©chec du lecteur de preuve V4 sur le format compact v39,
aprÃ¨s deux Save HTTP 200 (2,15 s et 3,77 s). DÃ©codage Rust rÃ©el : dix suppressions
vÃ©rifiÃ©es et mÃ©tadonnÃ©es source des plantes intactes. Le helper V5 accepte les
archives compactes via un dÃ©codeur Ã©pinglÃ© ; l'ancien ledger reste en Ã©chec.

RÃ©ouverture UI Ã  froid du candidat prÃ©cÃ©dent Ã©choue au dÃ©lai de 120 s. Correctif
domaines suivant : vÃ©rification parallÃ¨le bornÃ©e et compteur exact de taille
delta pour Ã©viter de resÃ©rialiser les anciens prÃ©fixes Ã  chaque commande.
223 tests projet all-targets et Clippy passent ; API d'ouverture sur une copie
de la mÃªme sauvegarde mesurÃ©e Ã  8,86 s, contre 119,23 s avec la seule vÃ©rification
parallÃ¨le. Documents source et copie inchangÃ©s. Construction native appariÃ©e v3
en cours (66672), frontend poissons v2 conservÃ© ; rÃ©ouverture UI, dessin poissons
et ombres masquÃ©es restent Ã  prouver avant un verdict complet.

Suite runtime : natif v3 construit code 0, sources/frontend intacts ; ouverture
UI 32595 passe sur une copie indÃ©pendante et les dix suppressions restent
absentes. Inventaire rÃ©el de 78 poissons observÃ© dans le sÃ©lecteur commun.
Le contrÃ´le de cadrage camÃ©ra Ã©choue (15846, 120 s, vue restant Ã  180 degrÃ©s) :
la demande FishProps consommÃ©e avant projection prÃªte est corrigÃ©e, mais le
rÃ©sultat runtime de cette correction reste Ã  Ã©tablir. Snapshot v4 en contrÃ´le
Cargo WASM (33559), sans verdict de dessin.

PrÃ©paration ombre FishProps : shader de profondeur Ã  masque source verbatim et
flux position/FISH_DATA des 78 meshes source. Validation moteur isolÃ©e : 19 tests
et Clippy passent. Raccord graphe cascades, dessin de profondeur et comparaison
visuelle restent ouverts. [Preuves et limites](../evidence/tidewater/2026-10-04-fish-prop-masked-shadow.md).

Suite : cadrage corrigÃ© contrÃ´lÃ© sur natif v4 (12981), camÃ©ra rÃ©ellement passÃ©e
de 180 Ã  329 degrÃ©s, 78 entrÃ©es poissons observÃ©es, source/frontend prÃ©servÃ©s.
Raccord du masque aux trois passes de profondeur Ã©crit dans le snapshot v6 :
upload rÃ©sident, flags de distance indÃ©pendants du frustum, poses/time/cascades
mis Ã  jour et invalidation complÃ¨te lors d'ajout/retrait/changement de pose.
Cargo WASM Studio v6 code 0, sources figÃ©es intactes ; 19 tests ciblÃ©s moteur
et Clippy passent pour l'upload/prÃ©paration. Build frontend v6 actif (5792).
Coverage GPU, picking/Ã©dition poissons, absence de doublons et paritÃ© visuelle
restent non prouvÃ©s. Les ledgers/captures prÃ©cÃ©dents gardent leur portÃ©e.


Actualisation du 6 octobre — cailloux : la chaîne des récepteurs partagés passe
quatre compositions avec les vrais imports Bevy ; trois pipelines WebGPU passent
ensuite, avec imports stubés et aucun dessin. Le check natif courant inclut les
nouveaux modules Engine et termine 0. Les deux tests du lecteur Assets passent ;
Clippy V1 révèle trois détails corrigés, les contrôles V2 sont en cours (56334).
La publication réelle des ressources/cellules dans l'île, le raccord Studio et
la fidélité visuelle restent ouverts.

Suite du 6 octobre : les deux tests acquis et Clippy Assets lib du lecteur de
programme V2 terminent 0, pins inchangés. Le candidat Studio natif V2 atteint
insertion catalogue, Apply, Save et réouverture lors de son essai hors sandbox,
mais échoue sur l'animation des ombres (`Opaque shadow caster identities changed`).
Le cycle d'édition commun des nouveaux débris n'est donc pas exécuté. Le correctif
courant unifie les identités par partie projetante entre snapshot et animation.
Test acquis, Clippy Engine et check WASM Studio sont lancés sous 26572 ; aucun
succès n'est encore attribué. Le candidat figé V2 ne contient pas ce correctif.
[Preuves et portée](../evidence/tidewater/2026-10-05-debris-shared-input.md).

Suite : le nouveau snapshot `studio-shadow-candidate-20261006-v2`, contenant
le correctif des casters, compile en WASM et son frontend de 7930 fichiers est
assemblé avec les pins vérifiés. Son build natif est lancé ; le parcours réel
Apply/Save/réouverture avec ombres animées reste à rejouer. Il ne contient pas
encore les cailloux ni le raccord des 141 variantes scannées au Studio.

Les cailloux ont désormais leur bootstrap natif et une famille temporelle
dédiée enregistrés dans le code courant. Le test V3 incluant le dispatcher réel
passe huit compositions Naga et les refus des coutures invalides. Le check de
l'exemple natif reste actif ; aucun dessin GPU de cette nouvelle intégration,
aucune comparaison visuelle et aucun nouveau portable ne sont encore qualifiés.
[Preuves et limites](../evidence/tidewater/2026-10-06-pebble-shader.md).

Le test acquis des casters termine ensuite à code 0 (20,31 s, trois pins
inchangés) ; Clippy Engine et le check WASM restent en cours de séquence.
Le build natif du Studio corrigé est lancé. Un snapshot séparé du launcher
actuel reprend la page Aube et ses 68 illustrations ; son build de production
est également lancé. Aucun de ces deux nouveaux exécutables n'est encore
validé dans son interface ni distribué.

Suite : le launcher de production termine son build et ses trois marqueurs CLI
à code 0. Son interface réelle V3 passe accueil et menu Jouer, avec ressources
embarquées et captures inspectées ; le jeu n'est pas lancé, car ce dossier
ne contient pas encore runtime/données. Le check natif Pebble V1 refuse une
liaison de matériau non mutable ; elle est corrigée, avec prévalidation du
shader temporel au chargement, et le check V2 est relancé. Le build Studio
corrigé continue. La distribution complète et la fidélité de la carte restent
non qualifiées.

Actualisation : le Studio corrigé termine son build natif et son parcours
insertion/Apply/Save/réouverture avec ombres animées et persistance graphique.
Les trois cascades et le temps de vent avancent avec 421 parties opaques ;
la cadence rAF observée est toutefois proche de 1,19 image/s et ne qualifie
pas la fluidité. Le cycle commun suivant refuse d'abord le dossier d'isolation
du runner ; une copie conforme est préparée et le scénario complet relancé.
Le check natif Pebble corrigé passe et son build exécutable est lancé.
Clippy Engine global échoue sur 92 diagnostics : sa séquence ne lance donc pas
le check WASM suivant. Ces résultats sont distincts des preuves du snapshot.

Le contrôle des passes identifie aussi les miroirs natifs de réfraction
manquants pour les matériaux Pebble, Scanned et Nature, ainsi que la contribution
temporelle native Scanned non enregistrée. Ils restent nécessaires à la fidélité
complète, avec le raccord Studio des scans et cailloux, les tests d'édition,
les comparaisons graphiques et le portable extrait.

Le cycle commun Studio V3 termine en échec partiel : quatre objets et leurs
copies sont transformés puis sauvegardés avec pins vérifiés, mais la vérification
de bibliothèque après réouverture expire à 180 secondes. Suppression/Undo/Redo
et persistance finale ne sont pas atteints. La concurrence des tickets de
restauration et de chargement explicite, ainsi que le déverrouillage `busy` en
cas de remplacement, sont à diagnostiquer avant de rejouer ce parcours.
Les corrections Clippy sont préparées dans une copie isolée, avec vérification
des hashes de production ; le runtime natif Pebble reste en compilation.

Le callback du chargement explicite libère désormais son bouton même sur ticket
remplacé et permet de relancer la vérification. Check Studio web lancé sous 24461 ;
la correction n'est pas incluse dans le candidat du cycle V3 en échec et n'est
pas encore qualifiée par réouverture réelle. Le test isolé du registre de shaders
est relancé sous 42940 avec le contexte Cargo requis par la macro Bevy, après
échec de préparation du premier harnais ; Clippy global reste en échec.

Une préparation du raccord de réfraction Nature/Pebble/Scanned est ajoutée sous
`.tidewater-integration/debris-refraction-20261006-v1`. Le filtre du commit source
1438b1ab confirme l'inclusion des trois matériaux `underwaterLighting: lite` ;
les instances passent sans filtre de boîte locale et sont coupées par fragment
à la hauteur de mer + 0,4 m. Les miroirs préparés conservent les shaders vertex,
les bindings texture/storage et le `MeshTag` de chaque instance, synchronisent
les poses courantes et retirent les sources masquées ou supprimées. Trois
compteurs distincts sont ajoutés à l'observation préparée. Les originaux Engine
restent conformes aux pins du build natif actif. Statut PREPARED_ONLY : ni
compilation de ces matériaux imbriqués, ni readback, ni image appariée acquis.
Le manifeste liste les contrôles requis avant adoption et qualification.

Le registre de shaders corrigé isolément compile et passe ses quatre tests
existants sous 42940 (réimport, mutation, collisions et atomicité), sans
modification de production. Un premier contrôle du shader de capture Pebble
est lancé sous 13789 : fonction de découpe de réfraction réelle, programme
acquis avec ses receveurs partagés et quatre variantes d'imports Bevy.
Il ne compile pas les nouveaux matériaux imbriqués et ne dessine aucune image.

Le contrôle de capture Pebble 13789 termine PASS : trois tests effectivement
exécutés, dont quatre variantes Bevy du programme acquis avec découpe sous la
mer. Les expressions éclairées restent intactes hors ajout de la découpe et
de son uniform ; aucun dessin GPU ni matériau imbriqué encore qualifié.

La contribution temporelle Scanned est préparée dans le dispatcher et les deux
adapters natifs, puis validée CPU sous 70744 : huit variantes de shaders avec
imports Bevy réels, fonctions vertex/surface/fondu conservées et refus des
interfaces modifiées. Les sources Engine de production restent inchangées.
L'adoption, compilation du crate, dessin/readback des 141 instances et raccord
Studio restent ouverts. Voir `2026-10-06-scanned-temporal.md` pour les preuves
et la portée exacte.

Les shaders de capture Nature/Scanned passent à leur tour huit variantes Bevy
CPU, avec programmes acquis, éclairage préservé et binding de découpe 200 vérifié.
Le contrôle des vrais modules de matériaux courants et du module de miroirs
préparé est actif sous 95873 ; son type de base VillageMaterial est précompilé,
donc il ne remplace pas le check Engine complet. Le raccord reste hors production.
Voir `2026-10-06-debris-refraction.md` pour la portée des preuves et les essais
ECS/GPU encore requis.

Le premier contrôle de types 95873 échoue E0463 sur le crate Engine précompilé,
avant qualification du raccord. Le contrôle V2 sous 3156 extrait à la place les
vraies définitions et bindings courants de VillageMaterial et des extensions,
ainsi que la spécialisation Material, sans dépendance au binaire Engine ancien.
Les tests du suivi/retrait des miroirs sont préparés mais conditionnés à ce check
V2 PASS ; ils ne sont pas encore exécutés. Les compilations natives et Studio
restent actives sur leurs mêmes handles, sans redémarrage.

Le contrôle de types V2 détecte l'import explicite manquant de `MeshTag`.
Une nouvelle copie de préparation le corrige sans modifier les inputs du
processus encore actif, et le check V3 est lancé sous 58133. Le manifeste de
préparation corrigée et les diagnostics antérieurs sont conservés ; aucune
adoption dans les sources Engine de production ni qualification GPU n'est faite.

Le contrôle de types V3 58133 termine PASS, inputs inchangés : vraies définitions
et bindings Village/Extensions courants, spécialisation Material et module de
miroirs corrigé. Il reste plus étroit que le crate Engine complet et ne crée
aucun pipeline GPU. Les trois tests CPU du suivi/retrait des miroirs sont
effectivement lancés sous 70620 après ce PASS ; leur résultat reste en attente.

Le premier cycle CPU des miroirs compile mais ses trois fixtures sont exclues
par leur visibilité héritée non initialisée ; échec conservé. Le cycle corrigé
87684 initialise cette visibilité et termine PASS : suivi des poses, tags et
matériaux, retrait des sources masquées/supprimées et traitement distinct des
boîtes d'instances acquis CPU. Cinq tests exécutés en comptant les deux tests
de shaders existants. Aucune modification du filtre de production ni preuve
GPU induite ; le raccord reste préparé, sans adoption pendant les builds actifs.

L'aperçu d'adoption commune capture + temporel Scanned vérifie sept fichiers et
les inputs de leurs cinq validations PASS, avec code 0 et aucune modification
de production. L'application est conditionnée aux deux compilations actives
terminées PASS et à leurs sources toujours épinglées, puis exige de nouvelles
validations du crate et du rendu. Les originaux seront conservés et tout conflit
de hash refusé ; aucune publication ou qualification complète induite.

La prochaine promenade native vérifie également l'intégrité après exécution
du binaire et de ses entrées épinglées, neutralise les modes de diagnostic hérités
et hashe ses artefacts. Le constructeur portable exige ces invariants et les
artefacts inchangés. Quatre tests négatifs du constructeur réel passent sous
PowerShell 7 (`walk-artifact-guards-20261006-v2`), sans lancer le jeu ni créer
de paquet. Cela ne qualifie pas le dessin des galets, le parcours à venir ou
la parité. Les deux builds restent actifs sur leurs handles d'origine.

Le [recalcul des limites des galets](../evidence/tidewater/2026-10-06-pebble-height-revision.md)
est préparé hors production : quatre tests CPU PASS, dont égalité bit à bit
de toute la grille acquise et resélection après révision/invalidation. Le raccord
auteur reste ouvert : le terrain affiche ses retouches via une texture séparée
aux coins, tandis que Pebble lit aujourd'hui le seul raster source. Il faut
reprendre cette correction dans le shader et ses limites conservatrices, sans
remplacer l'interpolation auteur par un raster recuit aux centres. Les variantes
temporelles/réfraction et la preuve GPU devront couvrir ce raccord.

La composition conservative raster source + extrema des corrections aux coins
est désormais préparée et vérifiée par huit tests CPU PASS, dont un pic entre
les centres des pixels. Le shader Pebble préparé reprend l'évaluateur partagé
du terrain au binding 107 ; son contrôle réel de variantes est lancé sous 83619,
sans résultat attribué. L'adoption native initiale est préparée avec contrôle
de l'identité de révision et des octets de texture affichée. Pas de modification
des sources de production, ni de refresh natif ultérieur, ni de champ Studio
qualifié ; le contrôle complet et le dessin restent à faire.

Le contrôle de types du matériau Pebble auteur et de ses captures imbriquées
est réellement lancé sous 54092, avec la texture 107 et les définitions
Village/Nature/Scanned courantes. Son résultat reste en attente ; il ne remplace
ni la compilation du loader natif ni le GPU. Les quatre validations actives
conservent leurs handles et leurs inputs, sans relance sur silence des logs.

Le contrôle de types auteur 54092 termine PASS, inputs inchangés : matériau
Pebble avec texture de corrections 107 et captures imbriquées préparées.
Le renderer et le loader complet ne sont pas exécutés par ce contrôle.

La [bibliothèque Studio des décors scannés](../evidence/tidewater/2026-10-06-studio-scanned-library.md)
est préparée en quatre fichiers isolés : chargement asynchrone atomique,
références paquet/programme, catalogue et placements source dans la façade
commune. Syntaxe Rust et conservation UTF-8 passent ; aucun check de types,
WASM ou parcours Studio n'est attribué. Les 141 instances restent à raccorder
au rendu, collisions/readiness/picking et au cycle d'édition réel avant adoption.
