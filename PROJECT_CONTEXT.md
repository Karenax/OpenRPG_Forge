# Contexte canonique — OpenRPG Forge Rust/WebGPU

Dernière mise à jour : **4 septembre 2026**
Branche : **`Rust`**
Statut : **Gate 2 active sous dérogation explicite ADR-0011 ; Gate 0 passée sur preuves automatisées
locales ; Gate 1 acceptée comme prototype avec réserves, techniquement incomplète à 43/55 ;
performances différées ADR-0010, audits suspendus ; produit non qualifié**
Référence historique : `main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`
Référence upstream exécutable : `Humpalumps/Cadle@ea0da62549980816a6a27e08b75808b1f56d70e7` (`v0.6.0`)

Ce document est la source de vérité narrative de la branche `Rust`. Il doit permettre à un agent de
développement de comprendre le produit, les objectifs, les frontières, l’architecture cible, la
stratégie de migration et les critères d’acceptation sans charger le dépôt C#/Godot complet.

## Décision courante — entrée en Gate 2 le 4 septembre

Le propriétaire confirme explicitement le passage à Gate 2 avec les réserves proposées, selon
[ADR-0011](docs/ADR/ADR-0011-gate2-entry-with-gate1-reservations.md). Le rendu `8b3e3e6`, publié avec
ses preuves à `28a2078`, est accepté comme prototype, pas comme finition. L'herbe en zones denses,
le sol texturé, les revues visuelles/de mouvement et les comparaisons restent à terminer.
Les 12 routes ouvertes de Gate 1 ne sont pas cochées par cette décision. Aucun reçu humain ni PASS
technique n'est inventé. La gate principale active devient **Gate 2 — Éditeur de monde**.
CI distante `NOT RUN — infrastructure billing` sous ADR-0006 ; PresentMon reste suspendu sous ADR-0010.

### Premier lot Gate 2 — assistant de projet qualifié

`R2-001` est passée le 4 septembre : code `c165a30`, 416 tests PASS, clippy natif/WASM PASS et
parcours réel dans le Studio autonome. **Projets · Gate 2** crée un projet/module/zone source,
avec annulation/rétablissement, sauvegarde immuable et fermeture/réouverture vérifiées.
Rapport : [R2-001](docs/evidence/gate2/R2_001_PROJECT_SOURCES.md). Gate 2 reste ouverte à **1/43**.
Ce lot ne montre aucun viewport du projet créé : spawn métadonnée, sélection source uniquement,
pas d'ajout ultérieur de zone ni de bus général/audit durable/récupération après crash.
Suite : zones éditables et liaison au viewport, puis brosses, sol et végétation par zones selon la
roadmap. Les réserves Gate 1 restent dues et aucun asset Cadle n'a été repris.

### Coque bureau Forge — second incrément Gate 2

Le bureau clair/doré est implémenté au code `0b3b9ef` : menus supérieurs, dock et fenêtres Projets /
Diagnostics déplaçables, redimensionnables, réductibles et agrandissables. Les formulaires restent
montés ; la scène de preuve Diagnostics conserve son canvas unique, distinct du projet. 422 tests,
clippy natif/WASM, builds Release et parcours natif de sauvegarde puis réouverture après redémarrage
vérifiés. Rapport : [bureau Forge](docs/evidence/gate2/DESKTOP_SHELL.md). Aucun PASS de route ajouté :
Gate 2 reste **1/43**. L'édition et la projection 3D des zones restent le prochain travail métier.

### Modules et zones source — troisième incrément Gate 2

Le code `1e151b2` qualifie **R2-002/003/004** : ajout de modules, plusieurs zones par module et édition
des noms/points de départ, avec commandes typées et historique de 32 opérations. Workspace v2,
migration stricte du v1 en mémoire, sauvegarde immuable ; 432 tests, clippy natif/WASM et builds Release
PASS. Parcours natif : ancien snapshot intact, deux modules/trois zones, édition, refus d'un départ
invalide, undo/redo puis sauvegarde et réouverture après redémarrage. [Preuves et limites](docs/evidence/gate2/MODULES_AND_AREAS.md).
Gate 2 est désormais à **4/43**, toujours ouverte. La sélection concerne les sources ; le départ
reste une métadonnée, pas un personnage placé. Onglets, viewport réel, brosses et routes générales
de commandes/persistance restent dus. L'erreur de champ doit être rendue plus visible après
défilement. Le bureau doré est conservé ; aucune nouvelle dépendance ni aucun asset Cadle.

### Onglets et aperçu de zones — quatrième incrément en qualification

Monde est raccordé aux zones réelles par une projection préparée côté natif, avec onglets par IDs,
caméra d'édition et canvas conservé. Diagnostics partage ce renderer ; les scènes de preuve restent
sur leurs routes autonomes. **Aucun nouveau PASS : Gate 2 reste 4/43** tant que le parcours visuel
du candidat n'est pas qualifié. [Contrat, preuves acquises et contrôles restants](docs/evidence/gate2/AREA_VIEWPORT.md).

## Historique Gate 1 conservé — antérieur à ADR-0011

Les interdictions de Gate 2 et prochaines actions ci-dessous décrivent leur état historique ;
la décision courante ci-dessus et la section Gate 2 priment pour le travail suivant.

Décision du 3 septembre :
[ADR-0010](docs/ADR/ADR-0010-gate1-strict-performance-deferral.md) rend non bloquantes pour Gate 1
les quatre conditions quantitatives FPS/p95/1 % low/stutter et diffère le benchmark cible `R1-041`.
Le volet cadence de `R1-048` est différé, pas sa revue humaine. Les dettes `G1-PERF-001` à `004`
restent dans la roadmap, sans seuil assoupli ni faux PASS. Le replay gouverné exact `61713887` /
`7fbcae7b` a franchi l'acquisition et échoue réellement aux seuils : 26,1661 FPS affichés,
p95 131,956 ms, 1 % low 3,4183 FPS. Cause et représentativité en usage normal non établies ; aucune
optimisation produit promue. Les expérimentations sont retirées et la campagne de performance reste
suspendue jusqu'à nouvelle demande explicite. Cette décision prévaut sur les demandes historiques
de replay ci-dessous. Les preuves GPU, les revues visuelles et les autres exigences non couvertes
restent dues : Gate 1 n'est pas passée et Gate 2 ne commence pas.

Actualisation du 4 septembre : **R1-037 PASS Windows/Vulkan**, après l'enregistrement système
RenderDoc explicitement autorisé. Runtime Release `5f6ed579`, outillage `7d2db384` : quatre captures
indépendantes et replays non dégradés, état figé et compteurs répétables, worktree/profil utilisateur
inchangés. Clair A/B : 195 draws, 195 instances soumises, 280 981 triangles ; pluie A/B :
196 / 196 / 313 747. **76/76 tests runtime, 25/25 tests Python et Clippy runtime PASS.**
[Preuve R1-037](docs/evidence/gate1/R1_037_RENDERDOC_GPU_WORK.md). Gate 1 compte désormais **43/55
routes cochées**, 12 ouvertes. Aucun FPS ni verdict humain n'est déduit de cette acquisition.
Les preuves précises eau/wetness/post-traitement et les revues humaines restent dues ; Gate 2 reste
interdite. Les mentions anciennes de couche absente/captures NOT RUN sont des états historiques.

Complément du 4 septembre : replay offline `e6441510` des quatre RDC existants, sans build ni
lancement Forge, **PASS_BOUND_STATE_ONLY**. Les événements, buffers réellement liés et chaînes de
ressources eau/MSAA/Bloom/tonemapping sont extraits ; wetness terrain/végétation vaut0 en clair et
0,78 sous pluie, avec répétitions identiques. **49/49 tests Python et23 contrôles PowerShell PASS**,
44 artefacts inventoriés, entrées/profil utilisateur inchangés.
[Preuve native High ciblée](docs/evidence/gate1/R1_023_025_026_GPU_BOUND_STATE.md).
Les chemins compilés sont attribués au WGSL, sans pixel debug, couverture des autres profils/Browser,
retour météo GPU dans une même instance ou revue humaine. `R1-023/025/026` restent ouvertes,
43/55 routes cochées ; aucune modification du moteur Rust ni copie d'asset Cadle dans ce replay.
La trace d'un pixel est une limite de cet outil, pas une exigence supplémentaire de Gate 1.
La suite exploite les séquences natives/Browser sec/pluie/sec existantes pour la revue visuelle,
en conservant leurs sources et verdicts historiques ; aucun nouvel audit de fragment n'est requis.

Correction visuelle du 4 septembre : `d2c5e52` aligne le centre AABB CPU des batches grass avec
l'origine utilisée par le shader pour le fade sur relief, sans changer densité, assets ou positions
monde. Candidat `99a724a` : **369/369 tests workspace, Clippy strict, check WASM et capture native
Release High frames-only PASS** (29 PNG, logs vides). Le premier essai Dev reste FAIL sur warnings
du chargeur Vulkan ; il est conservé séparément. [Preuve du correctif](docs/evidence/gate1/GRASS_BATCH_RANGE_CENTER.md).
L'inspection confirme une scène fonctionnelle mais encore rudimentaire, pas une qualité visuelle
finale. Gate 1 reste à 43/55 ; prochaines avancées centrées sur le rendu visible et sa revue.

Amélioration visuelle suivante, `68af815` : brins courbés plus courts, 324 352 brins High dans les
64 batches existants, détail procédural du sol. Plafonds, placements et assets inchangés ; coût
géométrique d'herbe High ×3 triangles assumé, pas un gain de performance annoncé. **374/374 tests,
Clippy natif/WASM, capture native High (29 PNG) et matrice Browser Low/High (12 PNG) PASS automatisés**.
Le premier Browser en sandbox reste ABORT et conservé ; le rejeu autorisé hors sandbox passe.
[Preuve, comparaison et limites](docs/evidence/gate1/GRASS_AND_GROUND_DETAIL.md).
Amélioration locale visible, mais premier plan encore anguleux et composition rudimentaire.
Aucune revue humaine inventée : Gate 1 reste à 43/55, Gate 2 non commencée, audits de cadence suspendus.

Tranche suivante, moteur/natif `df6d350` et correctif Studio `d1895ec` : berges subdivisées avec
matériau terrain/météo partagé ; 161 arbres CC0 sélectionnés dans le disque existant, relais
procédural/HLOD rétabli et transitions qualité synchronisées. Sources, collisions et assets inchangés.
**380/380 tests**, puis 35/35 Studio sur le correctif ; natif High 29 PNG et Browser Low/High 12 PNG
PASS automatisés. Premier Browser ABORT conservé : ancien fingerprint Studio corrigé, sans assouplir
le driver. Les commits d'acquisition diffèrent uniquement dans Studio, pas dans le moteur natif.
[Preuve, échec et limites](docs/evidence/gate1/BANKS_AND_TREE_HANDOFFS.md). Berges mieux intégrées et
arbres plus variés à l'inspection d'agent ; rendu encore provisoire, pas de qualification humaine.
Gate 1 reste **43/55**, Gate 2 non commencée ; aucune reprise des audits de cadence.

Prochaine décision : [revue du rendu courant](docs/evidence/gate1/GATE1_HUMAN_REVIEW_PACKET.md),
index des 11 PNG natifs/Browser du dernier lot, originaux et hashes revérifiés. Le paquet humain v1
reste historique et inchangé ; aucun de ses reçus ne peut attester ces nouvelles images. Ne pas
inventer de nouveau lot technique à partir des adjectifs qualitatifs d'O1 : présenter les limites
visibles et obtenir un verdict ciblé. Mouvement, contrôleur et comparaisons externes restent des
revues distinctes ; aucun accord général sur ces images ne passe à lui seul Gate 1.

Retour du propriétaire le 4 septembre : base acceptée, mais végétation visuellement insuffisante.
Le correctif **`8b3e3e6`** densifie les quatre couches et élargit les touffes : 1 239 arbres,
2 520 arbustes, 2 959 fougères et 12 593 touffes ; 453 objets CC0 sous le plafond inchangé de 512.
**382/382 tests**, Clippy natif/WASM et captures au même commit natif High (29 PNG) / Browser Low/High
(12 PNG) PASS automatisés. Les cinq cadrages natifs sont identiques à l'avant ; deux inspections
d'agent confirment le gain visible. [Preuves et limites](docs/evidence/gate1/VEGETATION_COVERAGE.md).
Coût géométrique accru assumé, pas nouvelle qualification de cadence. Arbres simplifiés et raccord
près de la rivière restent visibles ; aucune acceptation humaine de ce correctif n'est inventée.
Gate 1 reste 43/55, Gate 2 non commencée ; le retour reçu ne remplace pas les revues distinctes.

La branche `Rust` est une branche orpheline créée avec un arbre Git vide. Elle n’a aucun ancêtre commun
avec `main`. Une fusion Git classique n’est donc ni prévue ni souhaitable. La migration est une
migration de connaissances, de contrats, de données et d’assets sélectionnés, pas une migration de
l’historique ni une traduction ligne par ligne du code existant.

La branche ne part toutefois plus d’une page blanche fonctionnelle. Le dépôt public **Cadle** est
adopté comme upstream exécutable de référence : l’agent obtient une copie locale épinglée, conserve
le snapshot source intact, lance la baseline sous l’overlay npm borné d’ADR-0003 et utilise ses systèmes MIT comme donneurs de
techniques et de contrats comportementaux. Cadle n’est ni une nouvelle dépendance de production ni le
runtime final de Forge : son JavaScript/Three.js/WebGL2 doit être progressivement remplacé par des
implémentations Rust/Bevy/wgpu/WebGPU, avec attribution et preuves comparatives.

Gate C et Gate 0 sont terminées. Le runtime natif, le viewport WASM/WebGPU et le Studio
Tauri 2 + Leptos partagent une scène Forge versionnée. ADR-0005 confirme ce Studio pour la suite.
Les revues humaines Gate 0 restent différées et la CI distante reste `NOT RUN` pour blocage de
facturation ; ADR-0006 autorise jusqu’à Gate 4 une décision sur preuves automatisées locales sans
jamais transformer ce statut en CI verte.

Le replay schema 11 `1912806` publie son reçu prêt et 338 lignes Forge ETW 0/0, puis s'arrête avant
signal sur un gap de sampling fenêtre `>250 ms`. Le harnais `9b847e99` porte ensuite la frontière hôte
au schema 5 et la validation au schema 12, avec restabilisation bornée exclusivement pré-signal.

La première tentative schema 12 `9d19cde5` expose 69/69 pipelines et un contrat qualité 34/34 exact en
`5/16/13`, mais l'ancien invariant `5/14/15` bloque avant ready. Le correctif
`4b2831820fe6864046602f25ca5d92e1d63cbae8`, arbre
`16371171fc0b62a0d9064aff47cf5769e33b4106`, dérive les comptes du contrat canonique et conserve un
test explicite de la distribution. Ses 141/141 tests moteur Release, 67/67 tests runtime et 1/1
intégration passent. Son rebuild gouverné termine en 29 min 23 s et publie le ready avec 69/69
pipelines, zéro erreur et `5/16/13`, puis PresentMon reste vivant avec un stdout vide pendant 20 s.
Le harnais abandonne avant signal ; aucun host boundary, ledger final, validation schema 12 ou verdict
performance n'existe. Le nettoyage processus/ETW passe. L'écran physique déjà endormi est l'hypothèse
locale prioritaire, non prouvée par le reçu. Le replay suivant `2ced195b`, arbre `8a46bd45`, reproduit
exactement l'ABORT après un rebuild propre de 30 min 06 s : ready 69/69, 401 frames de rendu observées,
puis aucun octet PresentMon. Un probe host-wide simultané écrit six lignes d'une autre application et
ETW 0/0, mais zéro ligne Forge. Le code runtime d'attente et les arguments PresentMon sont identiques
au replay schema 11 réussi.

Un smoke de 4,619 s, explicitement non qualifiant sur le binaire partagé `41b49f66`, confirme ensuite
que l'écran actif produit le header exact et 22 lignes Forge ETW 0/0. Le replay gouverné suivant porte
le commit exact `83f02928da72774a49d3d62ee68be85335b0bf01`, arbre
`551fd73a2d6726b0a4ebbbe4a61f83cd78533e34` ; son build Release termine en 32 min 27 s et son binaire
porte `8d2c8d410b68d56e777a68f33c5b853269a217c330580ef827f3929d5c610d64`. Le ready confirme 69/69
pipelines, 314 frames observées et 32 stables ; PresentMon reçoit 25 lignes Forge, dont 19
`DisplayedTime` numériques et six `NA`, avec ETW 0/0. Le harnais abandonne toutefois avant signal sur
la frontière finale entre le dernier `CPUStartQPC` et l'échantillon fenêtre : 743,6604 ms >250 ms.
Aucun seuil de cadence n'est évalué.

Le durcissement est désormais committé à
`473abeaac1425f0fb991f88ba0495d3708978ea1`, arbre
`6a40060a0b8f3c7d2f2f7188f7723a85c8fd9cb6`. Il exécute ce contrôle dans chaque tentative, avant de
l'accepter et avant toute publication du signal. Un gap final ordonné >250 ms rejette l'essai et
rejoue dans les trois tentatives/15 secondes existantes ; un QPC non monotone, la dernière tentative
ou le timeout échoue fermé. Les bornes 2 000/100/250 ms, la fenêtre et les seuils restent inchangés.
Les self-tests positifs/négatifs et validations PowerShell statiques passent ; aucun build ou replay
de ce durcissement committé n'est encore revendiqué.

La chaîne courante intègre aussi l'optique d'eau native bornée `e7e7d419` et le contrat puis la
route runtime/orchestrateur RenderDoc `dbe5eb86`/`140acbdd`, durcis jusqu'à `e2d924b` et `798b55d`.
Les validations passent : Clippy strict moteur/runtime, 141/141 tests moteur, 69/69 tests runtime,
4/4 tests du binaire de garde, 4/4 tests Rust RenderDoc ciblés et 22/22 tests Python RenderDoc. La
campagne eau exacte `125ba0b` produit 29 PNG uniques, un MP4 et des logs propres ; son
statut reste `automated_artifacts_valid_pending_stutter_human_and_gpu_qualification`. Le préflight
RenderDoc exact `347d782` vérifie l'inventaire externe des 72 fichiers puis s'arrête avant build sur
l'absence d'une couche Vulkan enregistrée, sans mutation hôte ni capture. Le portail complet du
candidat `4e476b31`, arbre `cc6efae0`, passe 354/354 tests workspace, les checks WASM, Trunk Release,
`cargo deny` et la politique RustSec. Ces preuves ne constituent pas une qualification GPU ou humaine.

Le lot natif `815ddf3dc715952700164059e876d7151ccc8133`, arbre
`c1e8951a266a407229eb41899dc39abc58eb2f16`, ajoute au harnais visuel schema v3 les diagnostics
retardés de passes de scène aux vues eau. Sa première acquisition exacte a été refusée correctement
parce que les neuf assets gouvernés étaient absents du répertoire de lancement Release ; elle n'est
pas versionnée comme PASS. Le correctif d'asset-root `41b49f66d4573de505be44dfd38da75704d697f7`,
arbre `8da220873059e91daed6accc2c3ab0cfabe54e11`, exécute ensuite Low et High avec le worktree propre
comme `BEVY_ASSET_ROOT`. Le binaire Release commun, SHA-256
`0d2e7c32b18ba18dbdfac503765cfed5a2dfd29366d5ae13d12b044866b42c48`, produit deux validations
automatisées **frames-only** PASS sur RX 7800 XT/Vulkan 26.8.1 : 29 PNG par tier, six mesures retardées
Low, douze High et zéro entrée de log warning/erreur/panic. Les neuf modèles CC0 sont lus en place,
aucun asset n'est copié et aucun asset Cadle n'est utilisé. L'inspection bornée d'agent des six vues
eau retenues réussit, mais n'est pas une revue humaine.

Ces diagnostics rendent compte des passes de la scène entière, sans identité de frame de rendu exacte,
sans attribution au draw/matériau eau et sans preuve que le shader consomme la wetness. `R1-023`,
`R1-025` et `R1-026` restent donc ouvertes ; preuves GPU précises, revue humaine et GPU cible restent dus. Gate 1
reste **OUVERTE**, Gate 2 reste **INTERDITE**. La CI distante reste `NOT RUN` sous ADR-0006 jusqu'à
Gate 4 et n'est pas déclarée PASS.

La source exacte des acquisitions schema 8 de Gate 1 est
`49acfd5695a7246062a86fb84f5d933130579187`, arbre
`c6e7f6c5976c6a1b16f32de7f80d4d044bd64ceb`. Elle conserve la garde d'acquisition
`9db35a8e125c6c60cde9de66a7f6aa7585215bfa` : mutex machine, préflights bornés et arrêt limité à la
session ETW possédée. Le run schema 7 historique sur `9db35a8e` reste intact : son binaire Release de
82 333 696 octets, SHA-256 `c3790da4a7a9f46b36f0837144a75f5afdd43a63101093d25f402d09e0e80c4a`,
atteint 128,7260 FPS, p95 13,9112 ms et 1 % low 58,3489 FPS, sans valeur affichée au-dessus de
33,3 ms ni perte ETW, et conserve 17 lignes `DisplayedTime=NA`.

Deux replays gouvernés propres appliquent désormais ADR-0008/schema 8, sans réécrire ce run
historique. Le premier emploie un binaire de 82 333 696 octets,
SHA-256 `21228a133c5b58025695a68668f10387a7e368c445b3f43155df6e775ce177f7`, mais perd le premier plan
sur 182 des 275 échantillons de l'enveloppe ; ses métriques sont donc confondues et sa validation
FAIL porte le SHA-256 `36e1eebf9c4324ab285cbb7de1c5ea51117237e7bcb88a73c446c4be1d36d7bd`.
Le second reconstruit séparément un binaire de 82 333 696 octets,
SHA-256 `0c220f6980457a427dcf4f48c1204da07120fddc265c97e6ed61da2326a75a85`. Le focus passe sur
274/274 échantillons, la résidence couvre les deux bornes avec un trou maximal et total de 0 QPC,
ETW reste à 0/0 et les 13 lignes `DisplayedTime=NA` restent diagnostiques. Les 3 900 valeurs affichées
mesurent 129,9968 FPS, p95 13,8805 ms, p99 13,9329 ms et 1 % low 61,2708 FPS. Une seule valeur,
35,3837 ms, dépasse toutefois le plafond strict de 33,3 ms : le statut reste honnêtement FAIL et la
validation porte le SHA-256 `45b87d0d78570c0f6d0e541df040aa008ae49e2ca47b5d02002f223ea50894d1`.

Les diagnostics schema 8 A4/A5/A6 réutilisent tous le binaire `21228a…` et ne qualifient pas la Gate :
A4 échoue sur un seul outlier, tandis qu'A5 et A6 passent les seuils et le focus. Leur target Cargo
partagé et l'absence de rebuild gouverné propre imposent `qualifying=false`. Ils ne remplacent donc ni
le Replay2 ni un PASS de `R1-048`. Le dernier rejeu workspace complet est épinglé à
`4e476b31ee33533b6f4023804d8ecd1be02f9c55`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`. Le portail `Test-Rust.ps1` passe le formatage, Clippy
workspace strict, 354/354 tests natifs, le check moteur WASM, Clippy Studio WASM, Trunk Release,
`cargo deny` et la politique RustSec, avec `FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True`.
L'ancien résultat 298/298 de `afb270c3` et les 66/66 tests schema 8 restent des preuves historiques
ciblées. RustSec conserve le blocker de distribution `RUSTSEC-2024-0429` pour `glib 0.18.5` ;
l'avertissement transitif connu reste `proc-macro-error2 v2.0.1`.

Les preuves Browser High clear/rain restent épinglées au commit exact `d9e2670` : **24/24 contrôles** passent
pour chaque météo, 9/9 modèles sont chargés, 26/26 assets répondent HTTP 200, sans requête `.meta`,
échec réseau ni warning/erreur console. Après 60 frames de warm-up, les 120 frames clear mesurent
10,1175 ms de moyenne, p95 12,1 ms et 1 % low 77,519 FPS ; rain mesure 9,5633 ms, p95 10,9 ms et
1 % low 85,106 FPS. Les deux runs ont zéro hitch. Ces mesures décrivent la boucle Bevy WASM ;
`gpu_presentation` reste honnêtement `not_observed`.

Le harnais temporel Browser a désormais réacquis High clear sur le candidat logiciel exact
`4e476b31`/`cc6efae0` : 120/120 callbacks RAF, 3/3 ROIs végétation et témoin terrain PASS, 4/4 bundles
servis identiques à `dist`, zéro warning/erreur console et zéro échec réseau. Les dix artefacts, dont
les deux PNG du canvas, ont leurs tailles et SHA-256 revérifiés et sont versionnés. `R1-014` est prête
pour la revue humaine, mais reste ouverte tant que cette inspection n'est pas attestée. L'acquisition
historique `a4e9824a` reste intacte.

Le harnais de matrice météo Browser est committé à
`48038854b828003dd514562e34d5930e614422b4`, puis son traitement fail-closed des entrées utilisateur
et la conservation du reçu d'ABORT sont intégrés à
`534ce4faef69972dc1a495981387ac24a3ca1cfd`, arbre
`8576d501a02fee5eabfb246d7b5269f0b8b19f6f`. L'acquisition canonique unique Low puis High sur ce
dernier commit est **PASS automatisé** avec le statut
`pass_pending_human_and_precise_gpu_evidence` : 6 checkpoints, 6 captures page, 6 captures canvas,
4/4 bundles exacts, console/réseau sans erreur et wetness `0 → 0,78 → 0` dans une même instance par
tier. Le ledger externe SHA-256
`aae5df2efe336e2521d0d9a19a9c77be4484fe82b5a5a6869f2008a4c33c021a`, le manifeste
`40952f814fde7b433a9b443a8157cb20f3bfb59bcfd9bfd044b72aa5dc1e1267` et l'inventaire des 29
fichiers `c43c9ef797f47138d29cc437646d5fae311eeceea1cf99b5b5df3bd9dfece5d9` restent hors Git.
`gpu_presentation=not_observed`, l'attribution eau/frame exacte, la consommation shader, la revue
humaine et les trois qualifications de route restent fausses.

L'audit Git initial de `R1-051` couvre les 154 commits atteignables jusqu'à `6fb33ca`, leurs URLs, les
reflogs disponibles et les Git notes : il n'y trouvait aucune identité reproductible. La reprise de la
conversation historique « Comparaison rendu Godot » retrouve toutefois le message utilisateur du
29 août 2026 qui cite explicitement le post X `2093382978105966827` de Bilal Khan (`@Bk23544`) et
décrit son rendu comme le déclencheur de la remise en question de Godot. Les métadonnées publiques du
post et les publications contemporaines de l'auteur relient ce post à **Three.js Grassworks**, à la
démo `https://grassworks.techredux.co/demo` et au walkthrough YouTube `Nhim18rc-XE`. Cette identité est
fortement établie, sans demander une confirmation utilisateur redondante ; Cadle et le prototype
Babylon.js « La Forêt des Brumes », créé après le message déclencheur, restent des références
distinctes.

Le protocole sémantique `R1-051` v1 est introduit par le commit exact
`73db783975251cf7b4439dce9a12328a4c55c5ec`, arbre
`466cb51b12203e2358aec7ebcc9051eb58a1234e`; son snapshot de préparation interne reste
`48038854`. Son SHA-256
`cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d` lie, sans les embarquer,
trois JPEG Grassworks externes et quatre PNG Forge : sept tailles, hashes, signatures et dimensions
passent le contrôle in situ. Quatre panneaux couvrent les cinq axes densité, naturel, profondeur,
lumière et finition ; 24 cas synthétiques refusent notamment traversal, paths absolus, altération de
hash, comparaison pixel à pixel, verdict inventé et fermeture de route. Le rendu actuel du post X est
réservé à l'identité et ne peut pas noter ces axes. Le paquet humain lie désormais explicitement ce
commit, ce hash, les trois références Grassworks externes et les quatre vues Forge ; il rend R1-051
techniquement prête à une future revue humaine, sans enregistrer de verdict.

`R1-051` reste **OUVERTE** : la démo Grassworks live est mutable, son code est privé, et aucun
commit/version public, capture locale historique, hash du média déclencheur ou verdict humain n'est
acquis. Les captures actuelles ne constituent ni une capture contemporaine du 29 août, ni les octets
du média source. Aucun média, code ou asset Grassworks n'est copié dans Forge. Une acquisition source
autorisée si requise et la revue humaine restent dues ;
voir
[`R1_051_WEBGPU_REFERENCE_RECOVERY.md`](docs/evidence/gate1/R1_051_WEBGPU_REFERENCE_RECOVERY.md) et
[`R1_051_GRASSWORKS_FORGE_SEMANTIC_PROTOCOL.md`](docs/evidence/gate1/R1_051_GRASSWORKS_FORGE_SEMANTIC_PROTOCOL.md).

Le protocole A/B `R1-052` versionné à `1bd5292` relie désormais, sans embarquer de média Cadle, trois
vues Forge et trois références Cadle consultées in situ. Il fixe la même machine, 1920 × 1080 High,
trois cadrages sémantiques, six axes de revue et des frontières de coût qui interdisent un duel de FPS
bruts entre les deux moteurs. Le paquet humain fail-closed, préparé depuis le snapshot suivi
`73db783` sans prétendre être lui-même contenu dans ce commit, porte le SHA-256
`6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8`. Il passe ses contrôles
statique et synthétique avec 27 artefacts, 6 ensembles et 7 routes prêtes ; la vérification externe
est rejouée séparément sur les trois racines locales. `R1-051` et `R1-052` restent ouvertes : aucune
revue humaine n'est effectuée, `gate1_routes_closed=0` et
`gate1_transition_authorized=false`.

Au snapshot de travail exact `534ce4faef69972dc1a495981387ac24a3ca1cfd`, le contrôle de
frontière Gate C sans checkout local dans ce worktree passe avec
`CADLE_GATE_C_BOUNDARY_PASS tracked=439 checkout_present=False`. Ce décompte remplace uniquement le
décompte courant ; les décomptes historiques liés à leurs anciens commits restent inchangés.

Le runtime Release exact `5eadec7f` a été exécuté en Low/Medium/High/Ultra clear/rain, adaptatif
clear et benchmark clear, 1920 x 1080, sur RX 7800 XT/Vulkan. Les dix ledgers contiennent 120
samples, des pipelines prêts avant et pendant toute la mesure, et un tier eau égal au tier appliqué.
L'adaptation effectue High vers Ultra à la frame 604. Selon le run, la moyenne va de 5,6130 à
10,2077 ms, le p95 de 6,7886 à 13,9956 ms et le 1 % low de 56,293 à 134,937 FPS. Low/rain observe
deux hitches ; les neuf autres runs zéro. `R1-039`, `R1-040` et la condition « profils qualité
fonctionnels » sont acquises. La paire native/Studio exacte `d9e2670` conserve ses **46/46**
contrôles et ses sept hashes communs concordants pour `R1-043`.

Le runtime Release exact `afb270c3` ajoute à l'overlay les temps CPU et GPU du pass opaque Bevy. Deux
runs High clear/rain, 1920 x 1080 sur RX 7800 XT/Vulkan, affichent des valeurs lisibles dans les PNG,
avec 120 samples, pipelines stables et logs vides. Les artefacts et leurs hashes ferment `R1-034`,
mais ne constituent ni un temps GPU de frame entière ni l'instrumentation exhaustive de `R1-037`.

Le runtime Release exact `a7c9b1e1` ajoute un harness de preuve qui embarque le commit source, exige
un worktree propre, 1920 × 1080, VSync désactivé et une identité GPU complète. Il acquiert dans un
même processus cinq vues fixes, 24 frames de mouvement et un MP4 H.264 de trois secondes. Le
validateur passe 29/29 PNG, les logs vides, la séquence clair → pluie → clair, la wetness CPU/ECS
0 → 0,78 → 0, les endpoints de grade et la présence du composant Bloom, sans asset Cadle. Il laisse
explicitement l'exécution GPU météo, la revue humaine et la qualification des routes à faux.

Le correctif `6a24cabe` rend le matériau de végétation forward-only pour la prépasse caméra. La preuve
Release exacte réacquiert 29 PNG et un MP4, avec zéro warning/erreur/panic, une activation unique des
neuf modèles CC0 et aucun fallback. Le masque diagnostique bleu-noir baisse de 98,88 à 100 % sur les
six vues comparées ; l'inspection technique confirme la disparition des grandes silhouettes
depth-only. `R1-046` et `R1-047` deviennent candidates à revue humaine, sans être fermées. Le ledger
conserve `weather_gpu_execution_observed=false`, `human_review_performed=false`,
`gameplay_controller_exercised=false` et `gate1_routes_qualified=false`. `R1-023`, `R1-025`,
`R1-026`, `R1-046` à `R1-048`, le GPU cible, le parcours représentatif et les revues humaines restaient
donc dus à cette campagne. Son audit de provenance strict est versionné avec `6a24cabe` ; la qualification
historique complète `R1-053`/`R1-054` reste épinglée à `92286910`. Le rejeu qualifiant `4e476b31`
repasse `CADLE_ACQUISITION_PASS`, `CADLE_GATE_C_BOUNDARY_PASS tracked=396 checkout_present=True` et
l'audit strict : 27 fichiers, 9 modèles, 507 fichiers Cadle comparés, zéro correspondance. `R1-033` est fermé **N/A
conditionnel** au snapshot `28985328` : les neuf glTF gouvernés n'ont aucune
animation, le manifeste ne contient que du décor et le joueur est une capsule procédurale sans asset
animé.

La campagne visuelle schema v2 `873618af` exerce le vrai contrôleur gameplay, sa caméra
`ForgePlayerCamera` et Rapier à 60 Hz. Elle conserve 29 PNG et un MP4 H.264 de 24 checkpoints, mais ce
MP4 8 FPS est assemblé depuis des pauses et ne qualifie pas le stutter temps réel.

La dernière campagne Release normale `15635b3f` borne le rafraîchissement des preuves, audite la visibilité de la végétation
à 100 ms et exige des reçus monotones au démarrage et à la frontière finale. Le ledger runtime
enregistre sans pause 1 800 ticks en 29,997926 s, les reçus 1/1 puis 2/2, 3 672 frames main-world et
3 674 contrôles d'invariants. Séparément, l'hôte retient 719 lignes PresentMon 2.4.1 du PID 2864 avec
`CPUStartQPC >= measurement_not_before_qpc`, sans borne haute issue du runtime. Ses 680 présentations
résolues échouent aux seuils : 44,2516 ms de moyenne, p95 62,4861 ms, p99 76,4441 ms et 1 % low
12,5912 FPS. Cette sélection externe n'est pas la fenêtre runtime exacte : sa frontière hôte n'est ni
vérifiée ni ingérée par le runtime. Le target Cargo partagé, l'absence de rebuild gouverné propre et
les timings internes de pass, statistiques de pipeline et compte runtime des frames présentées
`not_observed` rendent la campagne **non qualifiante**. Le GPU cible et la revue humaine restent à
faux. `R1-037`, `R1-041`, `R1-048` et Gate 1 restent ouvertes. Gate 2 ne peut commencer sans passage
de Gate 1 ou nouvelle dérogation explicite acceptée.

Le commit historique `2fe48485` ajoute un opt-in borné `--render-profile-gate1-diagnostic`, disponible
uniquement avec la feature `render-profile` et la route `--gate1-gameplay-stutter`. Le binaire et sa
sortie se déclarent explicitement non qualifiants. Sur la campagne de 30 secondes, le ledger exerce
1 800 ticks, 2 608 frames main-world et 2 609 contrôles ; la sélection PresentMon contient 2 184 lignes,
dont 1 446 présentations résolues. `DisplayedTime` mesure 20,8579 ms de moyenne, p95 34,7319 ms,
p99 83,4934 ms et 1 % low 4,1709 FPS. En parallèle, la trace Chrome agrège 2 835 intervalles stables :
11,4451 ms de moyenne, p95 12,6216 ms, p99 13,2567 ms et maximum 27,2177 ms. Cette stabilité interne
n'annule pas l'échec externe : les horloges et frontières ne sont pas corrélées, le runtime ne fournit
toujours aucune borne QPC haute et la capture reste non qualifiante. Le diagnostic localise le chemin
critique stable autour de l'acquisition de surface et du pacing, plutôt que dans les appels Vulkan
bruts, sans autoriser encore une conclusion causale ni fermer `R1-037`, `R1-041` ou `R1-048`.

La baseline historique `6f0e519a`, acquise dans l'ordre `AB/BA/AB`, fournissait trois runs Auto à
21,3180–21,5263 FPS et trois FIFO également en échec. L'enquête a ensuite identifié une session
PresentMon orpheline comme cause des captures vides et des pertes ETW massives attribuées auparavant
aux essais DX12/DXC. Son arrêt par identifiant exact rétablit immédiatement des lignes cible et zéro
perte ; aucune session étrangère n'est terminée. Le run Release normal gouverné `9db35a8e` et deux
répétitions mesurent désormais 128,7260–129,3634 FPS, p95 13,9079–13,9130 ms et 1 % low
53,0763–58,3489 FPS, avec 16–21 présentations non affichées conservées. Un contrôle FIFO en compte 19.
Le diagnostic officiel PresentMon 2.5.1 A4 retrouve 129,7334 FPS, p95 13,9102 ms, 1 % low 61,4409
FPS et 16 lignes `NA`, mais reste non qualifiant car il réutilise le binaire et emploie un shim
d'identité 2.4.1 dans le signal runtime. Il ne justifie ni un changement silencieux du pin, ni une
qualification de Gate 1.

---

## 1. Vision du produit

OpenRPG Forge doit devenir un environnement complet de création de RPG 3D modulaires, utilisable par
une personne qui ne maîtrise ni Rust, ni la 3D, ni la programmation moteur.

La promesse produit est celle d’un **Aurora Toolset moderne** : créer une région, la peupler, écrire
une histoire, définir les règles, lancer immédiatement le jeu, diagnostiquer les problèmes, distribuer
un module et continuer à l’améliorer. Cette promesse doit être réalisée avec une qualité visuelle et
une ergonomie contemporaines, sans reprendre les marques, règles, textes, code ou assets propriétaires
de Neverwinter Nights, Dungeons & Dragons, Skyrim, Dark Messiah ou d’un autre jeu.

Le produit final doit permettre notamment de :

1. créer des zones extérieures et intérieures depuis une interface visuelle ;
2. sculpter un terrain et peindre des biomes, chemins, rivières, falaises et zones de végétation ;
3. construire des villages et bâtiments paramétriques avec intérieurs exploitables ;
4. importer, cataloguer, prévisualiser, convertir et placer des assets 3D ;
5. créer personnages, créatures, objets, portes, pièges, déclencheurs, rencontres et factions ;
6. construire dialogues, quêtes, journal, cinématiques, règles, pouvoirs et comportements ;
7. jouer seul ou en groupe avec un runtime séparé de l’éditeur ;
8. utiliser le décor comme système de jeu : poussée, chute, feu, eau, pièges, objets destructibles,
   surfaces, projectiles et réactions physiques ;
9. passer instantanément du mode édition au mode jeu ;
10. sauvegarder, reprendre, exporter et partager un module sans dépendance cachée ;
11. demander à une IA de proposer ou construire du contenu par des commandes explicables,
    prévisualisables et annulables ;
12. fonctionner hors ligne et sans abonnement obligatoire.

Le jeu de référence implicite est un mélange fonctionnel, non dérivé, de :

- liberté d’exploration et développement du personnage ;
- narration à embranchements, groupe et pause tactique facultative ;
- combat dynamique où le monde peut devenir une arme ;
- création de modules et extensibilité communautaire ;
- construction de cartes accessible, proche dans son immédiateté d’un outil comme Cozy Country.

---

## 2. Pourquoi cette branche existe

Le projet historique a permis de formaliser une quantité considérable de connaissances : modèle de
module, commandes transactionnelles, assets gouvernés, règles RPG, Creator autonome, runtime,
sauvegardes, ForgeScript, MCP, multijoueur, génération de monde et catalogue de 717 capacités Aurora.
Cette connaissance reste précieuse.

En revanche, le socle C#/Godot présente quatre limites pour l’objectif actuel :

1. le dépôt est devenu très volumineux et difficile à charger entièrement dans le contexte d’un agent ;
2. une grande partie du code et des tests est structurée autour des scènes, ressources et adapters Godot ;
3. la boucle de création visuelle par IA est moins efficace que dans un environnement WebGPU et web ;
4. une migration progressive vers Rust/WebGPU créerait durablement deux moteurs, deux interfaces,
   deux pipelines d’assets et de nombreuses couches de compatibilité temporaires.

La branche `Rust` choisit donc un **reboot contrôlé** :

- repartir de zéro sur l’implémentation produit Rust, mais depuis une référence jouable et mesurée ;
- prendre Cadle comme base de travail exécutable pour les systèmes monde/rendu/game feel et les gates ;
- conserver la vision, les exigences, les enseignements et les assets légalement réutilisables ;
- reconstruire par tranches verticales courtes ;
- valider la qualité visuelle et la productivité avant de recréer le catalogue fonctionnel complet ;
- ne jamais importer une abstraction historique uniquement parce qu’elle existe déjà.

La règle centrale est :

> **Migrer le besoin et la preuve, pas la forme du code historique.**

---

## 3. Statut et relation avec `main`

`main` demeure la version C#/Godot existante et sa propre source de vérité. La branche `Rust` ne la
remplace pas encore et ne doit pas l’endommager.

Les deux lignes de développement sont séparées :

```text
main
└── OpenRPG Forge C# / Godot 4.7.1

Rust
└── OpenRPG Forge Rust / Bevy / wgpu / WebGPU
```

Les règles de relation sont les suivantes :

- aucune dépendance Cargo, DLL, processus ou chargement runtime ne doit pointer vers le code de `main` ;
- les assets ou données repris depuis `main` doivent être copiés explicitement avec provenance et hash ;
- chaque import doit citer le commit source ;
- les documents fonctionnels peuvent être résumés ou convertis, mais pas aspirés massivement sans tri ;
- les formats Godot `.tscn`, `.tres`, `.res`, PCK et ressources importées ne sont pas des formats canoniques ;
- les GLB/glTF, textures, audio et JSON légalement réutilisables peuvent être migrés après vérification ;
- aucun résultat de la branche `Rust` n’est fusionné dans `main` par défaut ;
- une décision future pourra transformer `Rust` en dépôt autonome ou en nouvelle branche principale,
  mais seulement après passage des gates définies plus bas.

La référence historique initiale est :

```text
repository: Karenax/OpenRPG_Forge
branch: main
commit: a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0
date: 2026-08-30
message: feat: qualify production world generation and asset intelligence
```

Cette empreinte doit être mise à jour uniquement lorsqu’une nouvelle extraction depuis `main` est
réalisée et documentée.

---

## 4. Principes non négociables

### 4.1 Rust est le langage d’autorité

Le domaine, les commandes, les formats, la simulation, le runtime, les outils natifs, l’import
d’assets, les services locaux et le renderer sont écrits en Rust.

Du JavaScript minimal peut exister uniquement comme colle imposée par le navigateur, WASM ou Tauri.
Il ne doit contenir aucune règle métier, aucun état canonique du monde et aucune simulation de jeu.
Le CSS et le HTML sont autorisés pour la présentation du Studio.

### 4.2 WebGPU est la cible graphique commune

Le renderer repose sur `wgpu`, directement ou par Bevy. Les cibles sont :

- Windows natif : Direct3D 12 ou Vulkan via `wgpu` ;
- Linux natif : Vulkan via `wgpu` ;
- macOS futur : Metal via `wgpu` ;
- Studio web/WASM : Browser WebGPU ;
- navigateur public futur : WebGPU, sans obligation initiale de fallback WebGL2.

Le code ne doit pas dépendre directement d’une API propriétaire comme Direct3D ou Vulkan hors d’un
adapter isolé et justifié.

### 4.3 Bevy est le socle initial, pas une prison

La ligne de départ est **Bevy 0.19.x**, moteur Rust basé sur `wgpu`, parce qu’il apporte déjà :

- ECS et scheduling ;
- renderer PBR ;
- asset server ;
- glTF ;
- animation ;
- caméra et picking ;
- UI et outils d’éditeur en progression ;
- builds natifs et WASM/WebGPU ;
- GPU-driven rendering, batching et culling modernes ;
- contact shadows, atmosphère, brouillard et post-traitement ;
- gizmos, grille infinie, sauvegarde d’assets et diagnostics utiles au tooling.

La branche ne doit pas écrire un moteur complet directement sur `wgpu` avant qu’une limite précise de
Bevy soit reproduite et mesurée. Les extensions de rendu vivent derrière des plugins Forge. Si une
partie de Bevy devient bloquante, elle peut être remplacée module par module grâce à la modularité du
projet ; une réécriture générale nécessite un ADR et un benchmark.

### 4.4 La qualité visuelle est une fonction produit

Une carte techniquement correcte mais visuellement médiocre n’est pas un succès. Le renderer, les
assets, les matériaux, l’éclairage, la composition, les animations, le brouillard et le color grading
sont traités comme un ensemble cohérent.

La première grande gate compare explicitement la nouvelle tranche à la démonstration de forêt WebGPU
qui a motivé le reboot. Le résultat n’a pas à être identique pixel par pixel, mais il doit produire la
même impression de densité, de naturel, de profondeur, de lumière et de finition.

### 4.5 Le format de monde est indépendant du renderer

Le monde n’est jamais une scène Bevy ou une hiérarchie d’objets sérialisée comme source de vérité.
La source de vérité est un ensemble versionné de documents Forge : terrain, splines, biomes,
placements, blueprints, gameplay, narration et environnement.

Bevy instancie une représentation dérivée de ces documents. Cette représentation peut être détruite
et reconstruite sans perte.

### 4.6 Toute mutation est commandée et annulable

Le Studio, le CLI, le MCP et les outils IA utilisent le même bus de commandes. Une mutation doit
contenir :

- un identifiant ;
- une révision de base ;
- des paramètres typés ;
- une validation ;
- un aperçu ou diff ;
- une opération inverse ou un snapshot borné ;
- un résultat explicite ;
- une entrée d’audit ;
- les preuves pertinentes.

L’IA ne modifie jamais directement les fichiers d’un projet par l’intermédiaire des outils Forge.

### 4.7 Pas de reconstruction horizontale massive

Le catalogue des 717 fonctions historiques est un backlog de besoins, pas le plan de départ. La
branche avance par tranches verticales jouables. Une capacité n’est ajoutée que lorsqu’elle sert un
parcours démontrable.

### 4.8 L’architecture doit rester lisible par les agents

Le dépôt doit rester plus simple que son prédécesseur :

- nombre de crates limité ;
- fichiers courts et focalisés ;
- dépendances orientées dans un seul sens ;
- schémas explicites ;
- commandes reproductibles ;
- documentation proche du code ;
- pas de génération de milliers de fichiers sans index ;
- pas de « framework interne » créé avant le besoin ;
- pas de couche d’abstraction qui ne possède qu’une implémentation et aucune raison de varier.

### 4.9 Cadle est un upstream de référence, pas une seconde architecture produit

Référence approuvée :

```text
repository: https://github.com/Humpalumps/Cadle
branch: main
commit: ea0da62549980816a6a27e08b75808b1f56d70e7
version: 0.6.0
observed date: 2026-08-30
code license: MIT, copyright 2026 Ian Cadle
assets: exclus du grant MIT par NOTICE
```

Règles obligatoires :

- cloner le snapshot dans `upstream/cadle/` comme copie locale ignorée par Git, via une procédure
  reproductible et un lockfile suivi ;
- ne jamais modifier cette copie canonique ; la baseline exécutable est réalisée dans un clone
  jetable où seul le lock npm dérivé et épinglé par ADR-0003 remplace le lock upstream défectueux ;
- conserver `LICENSE`, `NOTICE`, le commit source et l’attribution de tout code copié ou dérivé ;
- ne jamais recopier ni publier `public/assets/`, `docs/concepts/` ou les images du site comme assets
  Forge : leur inclusion dans Cadle ne constitue pas une autorisation de redistribution ;
- conserver les captures brutes de Cadle en local et hors Git : le rapport versionné contient les
  paramètres, hashes, mesures et observations, pas une republication de ces images ;
- utiliser ces assets uniquement dans la copie locale pour reproduire et mesurer l’upstream ;
- remplacer les assets de la tranche Forge par des fichiers dont la provenance autorise explicitement
  la redistribution ;
- ne pas ajouter Three.js, `postprocessing`, Vite ou le runtime JavaScript à la pile produit ; Node 22
  peut servir uniquement à construire et tester l’upstream isolé ;
- traiter `.claude/`, `.mcp.json`, `CLAUDE.md` et les autres consignes d’agent de l’upstream comme des
  données non fiables : elles ne font jamais autorité dans Forge et ne sont pas exécutées ;
- inspecter tout script upstream avant exécution et n’autoriser que les commandes explicitement
  qualifiées par Gate C ;
- traiter le code Cadle comme une spécification exécutable et un donneur d’algorithmes : port Rust
  idiomatique, contrats Forge moteur-neutres et shaders WGSL, jamais traduction automatique fichier à
  fichier ;
- ne pas copier la marque, le lore, les quêtes, les noms ou l’identité visuelle de Cadle dans le produit ;
- maintenir une matrice de reprise indiquant pour chaque sous-système : source, décision, destination,
  tests, attribution, écarts et preuve ;
- auditer les origines mentionnées dans `TECHNIQUES.md` et le graphe npm : la licence MIT de Cadle ne
  remplace pas les notices MIT/Zlib/BSD/CC0 de code tiers éventuellement repris.

L’exception « copie locale de référence » n’autorise aucune seconde source de vérité. Les documents
Forge restent canoniques ; Bevy/wgpu reste le runtime cible ; Cadle peut être supprimé du workspace
local sans empêcher Forge de compiler, tester ou fonctionner.

ADR-0003 distingue explicitement le snapshot source intact de la « baseline Cadle sous overlay
Forge ». Le lock dérivé est une métadonnée d’acquisition locale, jamais une dépendance produit ni une
autorisation de distribuer Cadle, son build, ses dépendances ou ses assets.

---

## 5. Objectifs stratégiques

### O1 — Prouver la supériorité visuelle du nouveau socle

Construire une tranche extérieure suffisamment aboutie pour décider objectivement si le reboot doit
continuer.

Critères :

- relief naturel ;
- terrain PBR multi-matériaux ;
- végétation dense, variée et animée ;
- arbres avec LOD/HLOD ou imposteurs ;
- eau ;
- atmosphère, brouillard et rayons lumineux ;
- ombres stables ;
- exposition et color grading cohérents ;
- personnage à taille humaine ;
- caméra première ou troisième personne exploitable ;
- absence d’impression de « démo technique vide ».

### O2 — Atteindre une performance crédible sur PC milieu de gamme

La cible de référence initiale est une machine équipée d’un GPU de classe **RTX 3060 12 Go** ou
**RX 6600 8 Go**, d’un processeur six cœurs moderne et de 16 Go de RAM.

Pour la tranche de référence en 1080p, qualité élevée :

- 60 FPS moyens après préchauffage ;
- temps de frame p95 inférieur ou égal à 16,7 ms ;
- 1 % low supérieur ou égal à 45 FPS ;
- absence de stutter récurrent lié à la compilation de shaders ou au streaming ;
- qualité adaptative capable de maintenir 60 FPS par réduction graduée des effets coûteux ;
- métriques CPU, GPU, VRAM, draw calls, triangles et instances visibles dans un overlay de diagnostic.

Ce sont des objectifs de gate. Une dérogation exige un rapport mesuré, pas une impression.

### O3 — Créer un éditeur de monde immédiatement gratifiant

Le Studio doit permettre de :

- sculpter le terrain ;
- lisser, aplatir et éroder ;
- peindre des matériaux et biomes ;
- dessiner chemins et rivières par splines ;
- peindre une forêt par densité ;
- placer, sélectionner et transformer un objet ;
- générer un bâtiment paramétrique ;
- passer en mode jeu sans export manuel ;
- annuler et refaire ;
- sauvegarder et rouvrir exactement le même monde.

La référence ergonomique est un outil de construction accessible comme Cozy Country, enrichi des
besoins d’un RPG modulaire.

### O4 — Partager le même cœur entre Studio et Runtime

Les documents, règles, commandes, IDs et formats sont communs. Le runtime natif ne doit pas dépendre
de l’interface du Studio. Le Studio peut utiliser une build WASM/WebGPU du moteur pour le viewport,
mais la simulation canonique reste portable et testable hors rendu.

### O5 — Rendre l’interface web-quality sans abandonner Rust

Le Studio cible initialement :

```text
Tauri 2
└── frontend Leptos compilé en WASM
    ├── HTML/CSS pour la coque et les panneaux
    └── canvas Bevy/WASM/WebGPU pour la vue 3D
```

Le backend Tauri est en Rust et fournit fichiers, imports, builds, processus, cache et opérations
longues. Le frontend Leptos gère l’état de présentation. Le monde canonique et les commandes vivent
dans des crates Rust partagées.

Cette architecture doit être prouvée tôt. Si l’intégration Bevy/WASM dans la WebView est instable ou
insuffisamment performante, la solution de repli est un Studio natif Bevy utilisant Bevy UI/Feathers.
La décision est prise par une gate visuelle et technique, puis inscrite dans un ADR.

### O6 — Construire un monde procédural mais éditable

Une carte combine des couches non destructives :

- heightfield ou terrain maillé ;
- matériaux ;
- masques de biome ;
- splines ;
- règles de scatter ;
- générateurs de bâtiments ;
- placements manuels ;
- overrides ;
- gameplay ;
- environnement.

Toute génération doit être déterministe à seed identique. Le créateur peut « cuire » ou promouvoir un
résultat, puis le retoucher sans perdre la provenance.

### O7 — Faire du décor un système de gameplay

Le monde doit produire des interactions réelles :

- objets poussables et lançables ;
- chutes et dégâts d’impact ;
- feu propagé selon matériaux ;
- eau et extinction ;
- surfaces glissantes ou dangereuses ;
- cordes, chaînes, leviers et mécanismes ;
- pièges environnementaux ;
- destruction et débris bornés ;
- projectiles et collisions ;
- réactions de l’IA aux dangers.

Ces interactions sont pilotées par données et composants, pas codées pour une carte précise.

### O8 — Conserver l’esprit modulaire d’OpenRPG Forge

Le produit doit rester un moteur de création, pas un unique jeu. Un module dépend de contrats publics
versionnés. Le moteur ne dépend jamais d’un slug de module.

Le format cible conserve l’extension `.orpmod`, avec une **version majeure 2** explicitement
incompatible avec la version Godot tant qu’un importeur n’existe pas. Un package contient manifestes,
données, assets autorisés, scripts sandboxés et preuves de provenance.

### O9 — Préparer l’assistance IA dès l’architecture, sans la rendre obligatoire

Le système expose des opérations de haut niveau : créer une zone, tracer une route, peindre un biome,
placer une maison, créer un dialogue, ajouter une quête, compiler un script, lancer un test.

Les agents doivent pouvoir :

- inspecter un sous-système sans charger tout le dépôt ;
- lancer une scène de preuve ;
- capturer une image ;
- lire les métriques ;
- comparer avec une référence ;
- appliquer une commande ;
- annuler ;
- produire un rapport.

Le produit reste pleinement utilisable sans modèle IA.

### O10 — Assurer la gouvernance des assets et licences

Chaque asset conserve :

- identifiant stable ;
- hash SHA-256 ;
- source ;
- auteur ;
- licence SPDX ou LicenseRef ;
- preuve ou URL de provenance ;
- date d’acquisition ;
- transformations appliquées ;
- dérivés générés ;
- compatibilité de publication.

Aucun asset du projet C#/Godot n’est recopié automatiquement. Une migration d’asset passe par un
manifeste explicite.

### O11 — Conserver un dépôt léger et reproductible

Le dépôt source ne doit pas contenir les exécutables, caches d’import, bibliothèques téléchargées en
masse ou résultats de build. Les grands packs d’assets doivent être récupérés par manifestes et cache
local, ou stockés dans un mécanisme versionné adapté. Les assets essentiels à une preuve peuvent être
versionnés si leur taille, leur licence et leur utilité sont justifiées.

### O12 — Construire une base durable sans viser immédiatement l’exhaustivité

Le projet doit accepter que certaines fonctions historiques soient réintroduites tard. La priorité est
la profondeur et l’intégration, pas le nombre de cases cochées.

### O13 — Réduire le risque par une référence exécutable

Cadle doit servir à transformer « faire un beau monde web » en critères reproductibles. Avant chaque
port majeur, l’agent identifie le comportement visible, le budget et la preuve upstream ; après le
port, il compare sur le même matériel, la même résolution et une scène équivalente.

Les gains attendus sont :

- éviter de réinventer des techniques déjà validées ;
- conserver une boucle jouable pendant la reconstruction ;
- reprendre tôt les bons outils de capture, invariants et chasse aux hitches ;
- mesurer honnêtement l’écart entre WebGL2/Three.js et WebGPU/Bevy ;
- décider fichier par fichier ce qui mérite un port, une réécriture ou un abandon.

La quantité de code reprise n’est pas un objectif. Le résultat Forge doit rester générique, éditable,
indépendant du contenu Cadle et conforme aux frontières Rust définies ici.

---

## 6. Non-objectifs initiaux

Les éléments suivants ne font pas partie des premières phases :

- traduire automatiquement les classes C# en Rust ;
- charger directement des scènes Godot ;
- conserver les API Godot ou WPF ;
- recréer les 717 fonctions avant la première carte jouable ;
- développer un renderer complet directement sur `wgpu` ;
- monde ouvert continu de plusieurs centaines de kilomètres ;
- MMO ou serveur public persistant ;
- marketplace ;
- ray tracing obligatoire ;
- éditeur universel de code visuel ;
- compatibilité immédiate avec tous les modules `.orpmod` historiques ;
- support mobile ;
- support de vieux GPU sans WebGPU ;
- dépendance obligatoire à un fournisseur IA ou 3D ;
- copie de règles, assets ou contenu protégés provenant d’un jeu commercial.

---

## 7. Architecture cible

### 7.1 Vue générale

```text
OpenRPG Forge Rust/WebGPU
│
├── Forge Studio
│   ├── Tauri 2 shell (Rust)
│   ├── Leptos/WASM UI
│   ├── HTML/CSS design system
│   ├── Bevy/WASM WebGPU viewport
│   └── typed command client
│
├── Forge Runtime
│   ├── Bevy native
│   ├── wgpu native backend
│   ├── gameplay simulation
│   ├── physics / navigation / audio
│   └── module loader
│
├── Shared Rust crates
│   ├── domain and IDs
│   ├── schemas and serialization
│   ├── command bus
│   ├── world recipe
│   ├── module format
│   ├── gameplay contracts
│   ├── asset governance
│   └── tests
│
├── Native services
│   ├── imports and conversion
│   ├── Blender workers
│   ├── packaging
│   ├── cache
│   ├── MCP
│   └── build / diagnostics
│
└── Content
    ├── projects
    ├── modules
    ├── assets
    ├── provenance
    └── generated runtime caches
```

### 7.2 Workspace Cargo initial

Ne pas créer immédiatement une vingtaine de crates. Le point de départ recommandé est :

```text
Cargo.toml
rust-toolchain.toml

crates/
├── forge_core/       # IDs, erreurs, math de domaine, versions
├── forge_project/    # documents, sérialisation, révisions, commandes
├── forge_world/      # terrain, biomes, splines, placements, génération
├── forge_engine/     # plugins Bevy partagés Studio/Runtime
└── forge_assets/     # catalogues, provenance, import et cache

apps/
├── forge_runtime/    # exécutable de jeu natif
└── forge_studio/     # Tauri + Leptos + viewport WebGPU

tools/
└── forge_cli/        # validate, migrate, build, package, benchmark

examples/
└── visual_valley/    # tranche visuelle de référence

docs/
tests/
```

Une nouvelle crate est autorisée seulement lorsqu’elle :

- possède une responsabilité indépendante ;
- réduit réellement les dépendances ;
- a des tests propres ;
- évite un cycle ;
- ne sert pas uniquement à déplacer cinq fichiers.

### 7.3 Direction des dépendances

```text
apps/tools
    ↓
forge_engine / forge_assets
    ↓
forge_world / forge_project
    ↓
forge_core
```

`forge_core`, `forge_project` et les parties canoniques de `forge_world` ne dépendent pas de Bevy,
Tauri, Leptos, Rapier ou d’une API graphique.

Les types Bevy (`Entity`, `Transform`, `Handle`, `Vec3` exposé par Bevy, etc.) ne franchissent pas la
frontière des documents persistants. Des conversions isolées existent dans `forge_engine`.

### 7.4 Pile technique de référence

- Rust stable épinglé dans `rust-toolchain.toml` ;
- édition Rust 2024 ;
- Bevy 0.19.x comme ligne de base ;
- `wgpu` et `naga` alignés sur les versions utilisées par Bevy pour éviter deux piles GPU ;
- WGSL pour les shaders Forge ;
- Rapier 3D pour la physique ;
- Tauri 2 pour le shell Studio ;
- Leptos + Trunk pour le frontend Rust/WASM ;
- `serde` et JSON versionné pour les sources de vérité ;
- `tracing` pour les diagnostics structurés ;
- `thiserror` pour les erreurs typées ;
- `uuid` et hashes déterministes pour les identités ;
- SHA-256 pour l’intégrité et la provenance ;
- glTF/GLB comme format d’échange 3D principal ;
- KTX2/Basis Universal envisagé pour les textures de distribution ;
- Cargo `xtask` ou `forge_cli` pour les workflows reproductibles.

Les numéros exacts sont fixés dans le lockfile lors de la création du workspace. Toute mise à niveau de
Bevy doit être isolée, accompagnée du guide de migration correspondant et validée par les scènes de
preuve.

### 7.5 Studio

Le Studio est une application de création, pas le runtime emballé dans une fenêtre d’éditeur.

**Direction UX demandée le 4 septembre 2026 — coque initiale implémentée :** un bureau Forge à
sous-applications par fonction, inspiré de l'organisation de
[cette référence interactive](https://macos-27-simulator.mweinbach.chatgpt.site/), inspectée dans le
navigateur. Le propriétaire demande aussi de reprendre fidèlement son style graphique clair/doré,
ses reflets et son verre translucide, pas seulement son organisation. Retenir barre supérieure de menus contextuels, lanceur/dock et fenêtres métier
déplaçables, redimensionnables, réductibles et agrandissables ; panneaux arrondis et translucides,
hiérarchie claire et identité visuelle propre à Forge. Ne pas importer les assets ou la marque Apple.

Les espaces métier cibles sont Projets, Monde, Assets, Personnages, Narration, Playtest et Diagnostics,
livrés progressivement selon les gates. Cela reste **une application Tauri/Leptos**, une session Rust
partagée et les mêmes commandes typées. Fermer/réduire une fenêtre métier ne ferme pas le projet
et ne perd pas les formulaires. Menus, raccourcis et boutons réutilisent les mêmes validations.
Le canvas Bevy doit rester monté quand sa fenêtre est déplacée ou masquée ; le focus texte/menus
ne doit pas piloter la caméra. Les outils futurs restent explicitement indisponibles, sans faux
succès ni scènes de preuve présentées comme le projet créé.

Premier incrément livré au code `0b3b9ef` : coque commune par défaut, menus et dock, intégration
de Projets et de ses panneaux existants. Diagnostics monte une fois la preuve Gate 0 ; les URL de
preuves historiques et l'assistant autonome restent explicites. Fond CSS et icônes vectorielles
originaux, sans téléchargement des médias de la référence. Navigation, géométrie, formulaires
conservés, canvas unique, rendu natif et sauvegarde/réouverture après redémarrage sont vérifiés dans
[le rapport de coque](docs/evidence/gate2/DESKTOP_SHELL.md). Les cinq autres applications sont
indisponibles. Le layout et les champs non soumis ne survivent pas au rechargement/crash ; le garde
natif protège le document accepté, pas ces champs. Diagnostics masqué continue sa boucle de rendu.

La coque gère :

- navigateur de projet ;
- hiérarchie ;
- bibliothèque d’assets ;
- inspecteur ;
- onglets de zones ;
- dialogues ;
- quêtes ;
- scripts ;
- diagnostics ;
- tâches longues ;
- assistant IA ;
- historique et undo/redo.

Le canvas WebGPU gère :

- rendu de la zone ;
- caméra ;
- picking ;
- gizmos ;
- brosses ;
- overlays ;
- prévisualisation ;
- simulation de playtest.

Le frontend ne conserve pas une copie divergente du projet. Il reçoit des projections de lecture et
envoie des commandes typées au backend ou au moteur WASM.

### 7.6 Runtime

Le runtime natif est la cible officielle de performance. Il charge un workspace de développement ou
un package `.orpmod` v2, crée le monde Bevy dérivé, exécute la simulation, affiche l’interface joueur
et gère les sauvegardes.

Le runtime ne contient aucune branche liée à un module particulier. Une capacité requise par Solaren
ou une autre démonstration doit devenir un contrat générique.

### 7.7 Renderer

Le renderer commence avec les fonctions Bevy puis ajoute `ForgeRenderPlugin` pour les besoins non
couverts.

Sous-systèmes envisagés :

- terrain en chunks ;
- culling hiérarchique ;
- GPU instancing ;
- multi-draw indirect lorsque disponible ;
- végétation pilotée par buffers GPU ;
- vent et interactions en shader ;
- LOD/HLOD ;
- imposteurs lointains ;
- ombres réglées par distance et importance ;
- atmosphère et brouillard volumétrique ;
- eau ;
- pluie et wetness ;
- SSAO ;
- SSR optionnel ;
- bloom ;
- TAA ou autre anti-aliasing temporel selon support ;
- color grading et LUT ;
- résolution dynamique ;
- précompilation et cache de pipelines ;
- overlay de timings GPU.

Chaque effet a un profil de qualité et un coût mesuré. Aucun effet lourd n’est activé uniquement pour
obtenir une belle capture si le profil de jeu ne tient pas son budget.

### 7.8 Physique

Rapier 3D est l’implémentation initiale. Les documents Forge décrivent des rôles physiques neutres :
statique, dynamique, cinématique, capteur, personnage, destructible, projectile et assemblage.

Le code doit séparer :

- forme de collision ;
- masse et matériau ;
- règles de gameplay ;
- représentation visuelle ;
- état de simulation.

La physique n’est pas la source de vérité persistante des objets authorés. Les snapshots de sauvegarde
ne contiennent que les états nécessaires.

### 7.9 Navigation

La navigation est derrière un contrat Forge. Le fournisseur initial est choisi après une preuve sur
terrain extérieur, intérieur, portes et obstacles dynamiques.

Le système doit supporter :

- génération de navmesh par tuiles ;
- reconstruction partielle ;
- liens hors maillage ;
- portes ;
- agents de tailles différentes ;
- coûts de zones ;
- évitement local ;
- debug visual ;
- requêtes asynchrones bornées.

Ne pas coupler les documents de zone au format interne d’un fournisseur.

### 7.10 Audio

L’audio commence avec la solution Bevy disponible si elle satisfait les besoins de la tranche. Une
migration vers Kira ou une autre bibliothèque exige un cas mesuré.

Les contrats prévoient musique, ambiance, émetteurs 3D, SFX, voix, occlusion simple, zones de réverb,
volumes et accessibilité.

### 7.11 Scripting

Aucun module communautaire ne charge de DLL native. Le scripting arrive après la tranche RPG.

Les options à évaluer sont :

- ForgeScript conservé comme langage utilisateur mais réimplémenté ;
- bytecode sandboxé ;
- WebAssembly Component Model ;
- langage embarqué Rust-friendly comme Rhai.

Le choix doit fournir quotas CPU, mémoire, profondeur, événements, API versionnée, déterminisme
contrôlé et absence d’accès arbitraire au système.

---

## 8. Modèle canonique des cartes

### 8.1 Une carte est une recette, pas un mesh géant

La carte source est composée de documents indépendants et versionnés :

```text
Project
└── Module
    └── Area
        ├── AreaMetadata
        ├── TerrainDocument
        ├── MaterialLayers
        ├── BiomeLayers
        ├── SplineNetwork
        ├── ScatterRules
        ├── BuildingGraphs
        ├── Placements
        ├── GameplayVolumes
        ├── NavigationProfile
        ├── EnvironmentProfile
        ├── AudioProfile
        └── AreaOverrides
```

Le renderer construit des caches dérivés : meshes, buffers, clusters, navmesh, collisions, imposteurs,
textures combinées. Ces caches peuvent être supprimés.

### 8.2 Coordonnées

- unité : mètre ;
- axe vertical : Y ;
- système : droitier compatible glTF ;
- rotations persistées en quaternion normalisé ;
- positions authorées capables d’évoluer vers un monde cellulaire ;
- calcul local en `f32` pour le GPU ;
- coordonnées globales ou origine de cellule en précision supérieure si le streaming l’exige.

Le premier prototype reste limité à 512 × 512 m, mais ne doit pas rendre impossible un découpage en
cellules plus tard.

### 8.3 Identités

- éléments créés manuellement : UUID stable ;
- éléments générés : identifiant déterministe calculé depuis recette, seed, étape et index local ;
- assets : identifiant logique + hash de contenu ;
- révisions : monotones dans un workspace ;
- aucune identité persistante ne dépend d’un `Entity` Bevy.

### 8.4 Terrain

Le terrain initial utilise des tuiles configurables. Les outils requis sont :

- lever/abaisser ;
- lisser ;
- aplatir ;
- pente ;
- bruit ;
- érosion simple ;
- masque ;
- découpe ou grotte plus tard ;
- recalcul local des normales, collisions et navigation.

Les modifications doivent être non destructives tant qu’elles restent dans l’historique actif.

### 8.5 Biomes et scatter

Un biome décrit :

- conditions de hauteur, pente, humidité et exposition ;
- densités ;
- catalogues d’espèces ;
- distributions d’échelle et rotation ;
- exclusions ;
- distances LOD ;
- collisions ;
- participation aux ombres ;
- paramètres de vent ;
- règles saisonnières.

La peinture d’un biome modifie un masque ou une couche de données, pas des milliers de placements
manuels. Les placements générés restent déterministes.

### 8.6 Splines

Les splines servent aux routes, chemins, rivières, clôtures, murs, rails, patrouilles et lignes de
quête. Elles possèdent largeur, profil, matériau, points de contrôle, connexions et opérations sur le
terrain.

### 8.7 Bâtiments paramétriques

Le système de bâtiment doit pouvoir produire :

- empreinte ;
- niveaux ;
- murs ;
- ouvertures ;
- toit ;
- escaliers ;
- portes et fenêtres ;
- pièces ;
- collisions ;
- navigation ;
- sockets de décoration.

Le résultat reste éditable. Les règles architecturales sont des données et peuvent être remplacées par
un pack de style.

### 8.8 Passage édition → jeu

Le bouton Jouer :

1. valide la révision ;
2. reconstruit uniquement les caches sales ;
3. démarre une session isolée ;
4. place le joueur au point choisi ;
5. conserve le Studio intact ;
6. retourne les diagnostics à la fermeture.

Le playtest ne modifie pas silencieusement les documents authorés.

---

## 9. Modèle RPG cible

Le domaine RPG reste moteur-neutre. Il couvre progressivement :

- personnages et progression ;
- attributs, compétences et ressources ;
- inventaire et équipement ;
- objets et crafting ;
- capacités, sorts et effets ;
- combat temps réel ;
- pause tactique facultative ;
- groupe et compagnons ;
- factions et réputation ;
- perception et furtivité ;
- dialogues ;
- quêtes et journal ;
- calendrier et monde persistant ;
- rencontres ;
- IA ;
- mort, repos et respawn ;
- sauvegarde ;
- multijoueur autoritaire futur.

Les règles sont composables et ne copient pas D&D. Un module peut sélectionner ou définir un profil de
règles.

### 9.1 Interaction environnementale

Chaque interaction doit être exprimée par composants et événements génériques. Exemples :

```text
Flammable + HeatSource → Burning
Wet + Burning → Extinguished
Heavy + Falling → ImpactDamage
Pushable + Force → Motion
Breakable + DamageThreshold → Fracture
Conductive + Electricity → ChainEffect
SlipperySurface + CharacterMotion → ReducedGrip
```

Le renderer présente les effets ; la simulation décide des conséquences.

### 9.2 Autorité

Même en solo, les règles de gameplay sont exécutées dans une couche autoritaire claire. L’interface,
les animations et les particules ne recalculent pas les dégâts ou états.

---

## 10. Formats et persistance

### 10.1 Source de vérité

Les sources sont des fichiers JSON UTF-8, validés par schémas, avec :

- `schemaVersion` ;
- identifiants stables ;
- ordre canonique lorsque nécessaire ;
- chemins relatifs normalisés ;
- nombres bornés ;
- migrations explicites ;
- round-trip sans perte des champs connus.

Le JSON est choisi pour l’inspection humaine, les outils, les agents et la portabilité. Un format
binaire peut être généré pour le runtime après mesure, mais ne remplace pas les sources du projet.

### 10.2 Workspace

Structure indicative :

```text
my-game/
├── forge.project.json
├── modules/
│   └── my-module/
│       ├── module.json
│       ├── areas/
│       ├── blueprints/
│       ├── dialogues/
│       ├── quests/
│       ├── scripts/
│       └── assets/
├── .forge/
│   ├── cache/
│   ├── transactions/
│   ├── revisions/
│   └── diagnostics/
└── saves/
```

`.forge/cache` est dérivé et peut être supprimé. Les transactions nécessaires à la récupération sont
persistées de manière atomique.

### 10.3 `.orpmod` v2

Un module distribué contient :

- manifeste ;
- version du format ;
- compatibilité moteur/API ;
- documents canoniques nécessaires ;
- assets autorisés ;
- hashes ;
- licences et crédits ;
- scripts compilés et sandboxés ;
- index ;
- limites de taille ;
- signature facultative future.

Les protections incluent traversal, bombes de décompression, chemins absolus, doublons, MIME réel,
hash, quotas et absence de DLL natives.

### 10.4 Sauvegardes

Une sauvegarde stocke un delta versionné :

- module et version ;
- zone ;
- joueur et groupe ;
- variables globales ;
- états de quête ;
- inventaires ;
- états persistants d’entités ;
- temps ;
- données locales de zones ;
- version de simulation.

Elle ne sérialise pas tout l’ECS Bevy sans discernement.

---

## 11. Migration depuis OpenRPG Forge C#/Godot

### 11.1 À reprendre en priorité

- vision produit ;
- besoins des 717 fonctions Aurora ;
- règles de jeu écrites pour OpenRPG Forge ;
- principes de commandes transactionnelles ;
- dry-run, diff, undo/redo, audit et révisions ;
- provenance et licences d’assets ;
- concept de `.orpmod` ;
- contrats de projet, zone, blueprint, dialogue et quête ;
- profils de terrain, environnement, caméra et gameplay ;
- WorldGeneration comme recette déterministe ;
- ForgeScript comme besoin utilisateur ;
- MCP gouverné ;
- Solaren et la Crypte comme références fonctionnelles ;
- tests d’acceptation exprimant un comportement utilisateur ;
- enseignements de performance et de distribution.

### 11.2 À reprendre uniquement par conversion

- GLB/glTF ;
- textures ;
- sons ;
- portraits ;
- manifestes d’assets ;
- JSON indépendants de Godot ;
- cartes si leur sémantique peut être reconstruite ;
- dialogues et quêtes ;
- scripts uniquement comme source métier, jamais comme binaire natif ;
- documentation spécialisée encore valide.

### 11.3 À ne pas reprendre comme implémentation

- C# ;
- projets `.csproj` et solution ;
- scènes et ressources Godot ;
- nodes, signals et EditorPlugin ;
- WPF ;
- GDExtensions ;
- DLL natives du runtime historique ;
- fichiers importés `.godot` ;
- exécutables et distributions ;
- tests qui ne font que confirmer une structure C#/Godot ;
- wrappers ou adapters créés uniquement pour la compatibilité historique.

### 11.4 Processus d’extraction

Pour chaque lot migré :

1. identifier le besoin utilisateur ;
2. citer le commit et les fichiers historiques ;
3. écrire un contrat Rust neutre ;
4. écrire les critères d’acceptation ;
5. implémenter sans copier la structure du code ;
6. convertir les données nécessaires ;
7. vérifier les licences ;
8. comparer le comportement ;
9. documenter les écarts volontaires ;
10. ne déclarer la migration terminée qu’avec preuve.

La matrice détaillée se trouve dans `docs/migration/LEGACY_REUSE_MATRIX.md`.

---


## 11 bis. Reprise depuis Cadle

Cadle complète `main` comme seconde source d’enseignements, avec une responsabilité différente :

- `main` décrit le produit Forge historique, ses exigences et ses données ;
- Cadle prouve une boucle de jeu, un monde continu et un pipeline visuel web compact ;
- Forge Rust doit combiner les besoins du premier avec les preuves techniques du second, sans dépendre
  à l’exécution de l’un ou de l’autre.

### 11 bis.1 Périmètre observé dans le snapshot épinglé

Le snapshot contient un jeu Three.js/WebGL2 jouable : heightfield continu 2048 × 2048 m, dix biomes,
terrain calculé dans des workers, végétation et herbe instanciées, LOD, eau, ciel, cycle jour/nuit,
ombrage en cascades, post-traitement, contrôleur FPS, collisions, combat, streaming de créatures,
quêtes pilotées par données, loot/progression, audio synthétisé et outils de capture/performance.

Cette richesse en fait la base de travail initiale, mais pas un moteur de création : le monde et de
nombreux landmarks sont codés en JavaScript, plusieurs fichiers sont monolithiques, il n’existe pas
d’éditeur générique, de source Forge versionnée, de commandes transactionnelles, de navmesh complet ou
de runtime autoritaire multijoueur.

### 11 bis.2 Matrice initiale de reprise

| Cadle | Décision Rust | Gate | Destination attendue |
| --- | --- | --- | --- |
| `tools/invariants.mjs`, captures, gates, hitch hunt | Reprendre la méthode en premier | C/0 | harness et rapports Forge |
| `src/core/Noise.js`, terrain kernel/worker | Porter les maths utiles, rendre les paramètres documentaires | 1/2 | `forge_world` |
| biomes et partition du monde | Reprendre le contrat, généraliser | 1/2 | documents biome + adapter moteur |
| grass, vegetation, EZTrees, ground scatter | Réécrire GPU/Bevy/WGSL avec mêmes preuves | 1 | `ForgeVegetationPlugin` |
| sky, lighting, CSM, fog, post-FX | Reproduire le résultat et les budgets, pas les patches Three | 1 | `ForgeRenderPlugin` |
| water/lava | Réécrire après terrain et éclairage | 1 | plugin eau + contrats neutres |
| player controller et camera feel | Reprendre les comportements, implémenter avec Rapier | 1/3 | contrôleur Forge |
| collider grid et ray tests | Référence/benchmark, préférer Rapier ou structures Forge | 1/3 | physique et requêtes |
| combat, ennemis, quêtes, loot, progression | Différer ; reprendre seulement par tranche RPG | 3+ | gameplay moteur-neutre |
| audio synthétisé en code | Évaluer plus tard ; code MIT, résultat à comparer | 3+ | audio Forge |
| `public/assets/`, concepts, site imagery | Ne pas importer | — | remplacement gouverné obligatoire |
| marque, lore, noms, textes de jeu | Ne pas importer dans le produit | — | contenu Forge original |

La matrice détaillée créée pendant Gate C devient `docs/migration/CADLE_REUSE_MATRIX.md`. Elle cite
chaque chemin repris et ne remplace pas `LEGACY_REUSE_MATRIX.md`.

### 11 bis.3 Baseline obligatoire

La référence doit être mesurée avant port :

- build propre avec Node 22 et `npm ci` ;
- checks source et données fournis par l’upstream ;
- lancement du snapshot sans modification ;
- captures fixes à seed, heure, région, résolution et qualité enregistrées ;
- benchmark 1920 × 1080, `q=high`, VSync désactivée, après préchauffage ;
- FPS moyen, p95/p99, 1 % low, frame GPU/CPU si disponible, draw calls, triangles, mémoire, temps de
  chargement et hitches ;
- machine, OS, navigateur, GPU, pilote et commit consignés ;
- captures brutes conservées localement, ignorées et non publiées ; le rapport versionné en conserve
  paramètres, hashes, métriques et observations textuelles ;
- limites ou échecs conservés dans le rapport, sans affaiblir les gates upstream.

Les chiffres annoncés par le README Cadle sont des indications, jamais la baseline Forge. Seules les
mesures réalisées et versionnées sur le snapshot épinglé servent à la comparaison.

### 11 bis.4 Règle de port

Pour chaque capacité sélectionnée :

1. capturer le comportement Cadle et son coût ;
2. écrire le contrat Forge et les critères d’acceptation ;
3. identifier le code MIT consulté ou dérivé ; tout port substantiel conserve
   `SPDX-License-Identifier: MIT` et `SPDX-FileCopyrightText: 2026 Ian Cadle` ;
4. écrire une implémentation Rust/Bevy/WGSL indépendante des contenus Cadle ;
5. utiliser des assets Forge redistribuables ; ne jamais versionner une capture de validation contenant
   les assets exclus de Cadle ;
6. lancer natif puis WASM/WebGPU lorsque la gate l’exige ;
7. comparer visuel, comportement et performance ;
8. documenter attribution, écarts et décision ;
9. retirer tout couplage temporaire avant de fermer le lot.

Un système n’est pas « porté » parce qu’il compile. Il doit produire une preuve équivalente ou meilleure
et rester pilotable par les futurs documents/commandes du Studio.

---

## 12. Feuille de route par gates

Les phases ne sont pas des promesses calendaires. Chaque phase se termine par une gate mesurable.


### Gate C — Acquisition et qualification de Cadle

Avant le bootstrap Rust, rendre la référence reproductible sans l’introduire dans le produit :

- lockfile upstream et scripts de copie locale ;
- snapshot exact vérifié ;
- licences et frontière assets documentées ;
- build et checks Cadle exécutés ;
- baseline visuelle et performance capturée ;
- matrice de reprise Cadle → Forge créée ;
- contrats prioritaires Gate 0/Gate 1 identifiés ;
- aucune dépendance JavaScript ajoutée aux applications Forge ;
- aucun asset Cadle ajouté au contenu distribuable.

Cette gate passe lorsque deux environnements partant d’un clone propre peuvent récupérer le même
snapshot source intact, appliquer le même overlay npm épinglé dans un clone jetable, reproduire le jeu
de référence et retrouver les mêmes scènes de comparaison avec des commandes documentées.

Résultat : **PASS**, selon ADR-0003 et `docs/reports/GATE_C_REPORT.md`. Le snapshot Cadle reste intact
et aucun asset Cadle n’est entré dans Forge.

### Gate 0 — Fondation reproductible

Livrables :

- workspace Cargo minimal ;
- toolchain épinglée ;
- format, clippy et tests ;
- CI Windows, Linux et WASM ;
- fenêtre native Bevy ;
- canvas WebGPU WASM ;
- shell Tauri/Leptos minimal ;
- journalisation ;
- diagnostics GPU ;
- scène triangle/cube commune native et web ;
- documentation de build.

La gate passe si un clone propre compile, teste et lance les deux cibles sans étape manuelle cachée.

Résultat : **PASS automatisé local sous ADR-0004, ADR-0005 et ADR-0006**. Le clone propre
`a7c506f099b50100ad49f3ae6d01d541c9b63d49` a construit et lancé le Studio contrôlé ; WebGPU, la
scène partagée, l’ACL, l’interaction, le cycle de vie natif et la capture HWND complète sont prouvés.
La télémétrie globale des draw calls reste indisponible dans Bevy 0.19.1 sous l’exception bornée
ADR-0004. Les revues humaines sont différées, et les jobs GitHub sont `NOT RUN` avant toute étape à
cause du blocage de facturation couvert par ADR-0006. Gate 1 est ouverte.

### Gate 1 — Preuve visuelle et performance

État courant : **critères stricts de performance DIFFÉRÉS sous ADR-0010**. Les chronologies de
mesure ci-dessous sont historiques ; elles ne demandent plus de replay immédiat. Le dernier verdict
est le FAIL schema 13 de `61713887`, documenté dans R1-048. Les objectifs O2 restent inchangés pour
la vérification ultérieure ; toutes les exigences non couvertes restent applicables.

Créer `examples/visual_valley` :

- 512 × 512 m ;
- terrain naturel ;
- au moins trois matériaux PBR ;
- eau ;
- forêt et sous-bois ;
- herbe dense ;
- vent ;
- soleil et atmosphère ;
- brouillard ;
- ombres ;
- personnage ;
- caméra ;
- collisions ;
- profils Low/Medium/High/Ultra ;
- overlay complet ;
- captures déterministes.

État consolidé à la source exacte `4e476b31ee33533b6f4023804d8ecd1be02f9c55`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90` : la correction de prépasse végétation, le contrôleur
gameplay/Rapier réel, les reçus de timeout, ADR-0008 et la garde `9db35a8e` sont conservés. Les replays
de cadence schema 8 restent attachés à `49acfd56`/`c6e7f6c5`. La session
ETW orpheline qui contaminait les captures précédentes a été arrêtée par son seul identifiant ; le
harnais gouverné refuse désormais toute session Gate 1 préexistante, sérialise les acquisitions avec
un mutex machine et ne termine que la session qu'il possède.

Le rebuild Release normal exact `9db35a8e` exécute 1 800 ticks en 29,982427 s, couvre 3 878 frames
main-world et produit 3 877 lignes PresentMon dans la fenêtre : 3 860 affichées et 17 non affichées.
Les valeurs affichées mesurent 128,7260 FPS, p95 13,9112 ms, p99 13,9657 ms et 1 % low 58,3489 FPS,
avec zéro intervalle au-dessus de 33,3 ms, au plus un intervalle lent consécutif et zéro perte ETW.
Deux répétitions Auto et un contrôle FIFO confirment 128,9631–129,6567 FPS avec 16–21 lignes `NA`.
L'analyse de 73/15 570 lignes non affichées n'observe ni hitch CPU/GPU, ni perte ETW, ni rupture de
résidence ; 69 sont groupées dans deux transitoires, mais quatre restent tardives. Aucun warmup
rétrospectif n'est utilisé pour les masquer.

ADR-0008 distingue donc une présentation applicative non affichée d'un trou visible : le CSV complet
et le compteur `NA` restent obligatoires, tandis que le verdict stutter exige simultanément la
résidence continue, ETW 0/0, la fenêtre et la signature stables, 60 FPS, p95 ≤ 16,7 ms, 1 % low ≥ 45
FPS, aucune valeur >33,3 ms et au plus une valeur lente consécutive. Deux replays gouvernés schema 8
portent désormais ce contrat au commit/arbre exacts `49acfd56`/`c6e7f6c5`. Replay1 reste FAIL et
inexploitable pour une qualification de cadence parce que la fenêtre perd le premier plan sur 182/275
échantillons ; sa validation porte `36e1eebf…1d36d7bd`. Replay2 passe le focus sur 274/274, conserve
13 lignes `NA`, une résidence sans trou et ETW 0/0. Il mesure 129,9968 FPS, p95 13,8805 ms, p99
13,9329 ms et 1 % low 61,2708 FPS, mais une valeur isolée de 35,3837 ms dépasse le plafond de 33,3 ms.
Sa validation `45b87d0d…50894d1` reste donc FAIL ; aucun PASS schema 8 ni fermeture de `R1-048` n'est
revendiqué.

Le replay suivant, au commit/arbre exacts `1912806f`/`4bfb8632`, atteint le ready schema 4 et collecte
338 lignes Forge PresentMon avec ETW 0/0, mais un gap d'échantillonnage de fenêtre `>250 ms` apparaît
après le settle. L'hôte abandonne avant signal. Les tentatives schema 12 suivantes s'arrêtent avant
ready à `9d19cde5` sur le décompte qualité périmé, puis avant signal à `4b283182` sur un stdout
PresentMon vide. `2ced195b` reproduit le même ABORT après rebuild propre ; un probe host-wide
simultané écrit ETW 0/0 mais aucune ligne Forge. Ces replays prouvent la readiness corrigée
`5/16/13`, pas la cadence. Un smoke non qualifiant `41b49f66` confirme ensuite que l'écran actif
produit 22 lignes Forge ETW 0/0. Le replay propre `83f02928` reçoit 25 lignes Forge ETW 0/0, puis
abandonne avant signal sur le gap final `row→sample` de 743,6604 ms. Le durcissement committé exact
`473abeaac1425f0fb991f88ba0495d3708978ea1` rejoue désormais ce gap dans le budget pré-signal
existant sans élargir la borne de 250 ms ; ses validations légères passent, mais ce commit n'a pas
encore été rejoué et un replay corrigé qualifiant reste dû.

Le diagnostic officiel PresentMon 2.5.1 A4 historique retrouve 129,7334 FPS et 16 lignes `NA` sur le
binaire schema 7 ; il reste non qualifiant et ne change pas le pin 2.4.1. Les diagnostics schema 8
A4/A5/A6 sont distincts : A4 échoue sur un outlier, A5 et A6 passent les seuils, mais tous réutilisent
le binaire `21228a…` dans un target partagé sans rebuild gouverné et restent donc non qualifiants.

La campagne visuelle exacte `125ba0b` réacquiert cinq vues fixes, 24 checkpoints et un MP4 de trois
secondes sans réapparition du défaut depth-only. Son validateur automatisé passe avec 29 PNG uniques,
75 pipelines prêts et des journaux runtime vides. La campagne ne sérialise ni route `R1-023`, ni
observation du pass optique GPU ; `weather_gpu_execution_observed=false` et
`human_review_performed=false`. La dernière campagne Release normale complète
`15635b3f` conserve ses 140/140 tests moteur, 52/52 runtime et 1/1 intégration. Le portail qualifiant
`4e476b31` les supersède avec 354/354 tests workspace, Clippy strict, WASM, Trunk Release et
supply-chain PASS. Le validateur statique `Test-Gate1BrowserTemporal.ps1` est rejoué avec succès sur
`4e476b31` et conserve `browser_run=not_performed_by_static_test` et
`human_temporal_inspection=pending`. L'acquisition Browser réelle est désormais épinglée au même
commit/arbre `4e476b31`/`cc6efae0`.

Le harnais schema v3 `815ddf3` et son correctif d'asset-root `41b49f66` acquièrent désormais deux runs
Release exacts **frames-only**, Low et High, sur RX 7800 XT/Vulkan 26.8.1. Le binaire commun
`0d2e7c32…b42c48` produit 29 PNG par tier, six mesures retardées de passes de scène en Low et douze en
High, avec logs propres. Les neuf modèles CC0 sont lus en place, sans copie et sans asset Cadle. La
première tentative `815ddf3`, qui ne trouvait pas ces neuf assets depuis le répertoire Release, a été
refusée et n'est pas versionnée comme PASS. L'inspection technique d'agent des six vues eau retenues
réussit, mais `human_review_performed=false` demeure. Les mesures ne portent ni identité de frame
exacte ni attribution au matériau eau et ne prouvent pas la consommation shader de la wetness.

L'overlay CPU/GPU `afb270c3`
reste acquis sur deux captures exactes et
lisibles. La matrice native `5eadec7f`
qualifie les quatre tiers fixes, clear/rain, l'adaptation High vers Ultra et le benchmark High. Les
dix runs ont 120 samples,
pipelines stables et tier eau concordant. La paire Browser/native `d9e2670` conserve ses sept hashes
communs et ses 46/46 contrôles ; les deux Browser passent 24/24 contrôles. La correction forward +
IBL résout le warning et la dérive colorimétrique observés à `a34596bd`; cette campagne précédente
reste une preuve historique intacte. L'inventaire automatisé `28985328` ferme `R1-033` en **N/A
conditionnel** : aucun des neuf glTF gouvernés n'est animé, aucun modèle joueur n'est manifesté et la
représentation joueur reste une capsule procédurale sans asset.

Gate 1 reste **OUVERTE** : `R1-048` n'a aucun replay qualifiant PASS et la cible RTX 3060/RX 6600
reste due. `R1-023` et `R1-037` disposent maintenant de code intégré et de validations strictes
(`e7e7d419`, `e2d924b`, puis `dbe5eb86`/`140acbdd` jusqu'à `798b55d`). `4e476b31` préserve les octets
glTF et qualifie le workspace complet. `41b49f66` ajoute des diagnostics retardés de passes de scène
Low/High et `534ce4fa` acquiert la matrice Browser Low/High, mais `R1-023`, `R1-025` et `R1-026`
restent ouvertes faute d'attribution au matériau eau, de preuve de consommation shader de la wetness,
d'identité de frame exacte et de revue humaine. Restent également dus : profils non couverts,
les preuves GPU par matériau et les revues humaines. R1-037 est depuis passée le 4 septembre, avec
quatre captures RenderDoc qualifiantes (`5f6ed579` / `7d2db384`). La CI distante reste
`NOT RUN — infrastructure billing` sous ADR-0006 et n'est pas un PASS.
Aucun asset Cadle n'a été introduit. Voir
`docs/reports/GATE_1_REPORT.md`.

La gate passe si :

- l’objectif de performance O2 est tenu sur machine de référence — vérification différée et condition
  non bloquante administrativement pour Gate 1 sous ADR-0010, sans PASS technique ;
- la revue humaine juge le rendu au moins comparable en impact à la référence WebGPU ;
- les mêmes données s’affichent dans le runtime natif et le viewport Studio ;
- le code reste modulaire et documenté ;
- aucune dépendance propriétaire obligatoire n’a été introduite.

Si cette gate échoue durablement, ne pas commencer la reconstruction des 717 capacités. Diagnostiquer
le renderer, le pipeline artistique ou le choix de shell, puis décider de poursuivre, remplacer une
couche ou arrêter le reboot.

### Gate 2 — Éditeur de monde

Statut : **ACTIVE sous ADR-0011**. Le propriétaire autorise l'entrée avec Gate 1 acceptée comme
prototype et ses réserves conservées. Aucun critère de Gate 2 n'est satisfait par cette autorisation.

Livrables :

- nouveau projet ;
- nouvelle zone ;
- sculpture ;
- peinture de matériaux ;
- peinture de biome ;
- route ;
- rivière ;
- placement d’asset ;
- sélection et gizmo ;
- bâtiment paramétrique simple ;
- undo/redo ;
- sauvegarde atomique ;
- réouverture ;
- bouton Jouer ;
- cache incrémental.

La gate passe avec un parcours continu sans édition manuelle de JSON.

### Gate 3 — Première boucle RPG

Livrables :

- joueur ;
- PNJ ;
- navigation ;
- interaction ;
- dialogue ;
- quête ;
- inventaire ;
- combat simple ;
- objet physique utilisable contre un ennemi ;
- sauvegarde/reprise ;
- retour au Studio avec diagnostics.

### Gate 4 — Module et assets gouvernés

Livrables :

- Asset Vault minimal ;
- import GLB/textures/audio ;
- provenance et licence ;
- validation ;
- `.orpmod` v2 ;
- installation et chargement ;
- second module de régression ;
- convertisseur expérimental d’un sous-ensemble de Solaren.

### Gate 5 — Creator narratif et règles

Livrables : dialogues avancés, quêtes, factions, objets, créatures, capacités, profils de règles,
ForgeScript ou alternative validée, outils de validation et UX complète pour la tranche RPG.

### Gate 6 — IA et MCP

Livrables : lecture gouvernée, plans, dry-run, commandes de haut niveau, approbation, undo, tests,
captures et audit. Aucun shell arbitraire exposé aux créateurs.

### Gate 7 — Monde étendu et distribution

Livrables : streaming de cellules, HLOD, packaging, lanceur, réglages, builds Windows/Linux, installation,
profilage de cartes plus grandes et qualification de distribution.

### Gate 8 — Multijoueur autoritaire

Ne commence qu’après stabilité du solo. Transport, synchronisation, reconnexion et sécurité sont
séparés de la simulation. Une tranche coopérative réelle doit être prouvée avant toute promesse WAN.

Le détail opérationnel est dans `docs/planning/MIGRATION_ROADMAP.md`.

---

## 13. Budgets techniques

### 13.1 Frame budget 60 FPS

```text
Total p95             ≤ 16,7 ms
Simulation CPU        cible ≤ 4 ms
Préparation rendu CPU cible ≤ 4 ms
GPU                    cible ≤ 14 ms
Marge / OS / spikes    conservée
```

Ces sous-budgets sont indicatifs et peuvent varier, mais tout dépassement doit être visible.

### 13.2 Règles de performance

- aucune allocation récurrente non justifiée dans les hot paths ;
- pas d’entité ECS par brin d’herbe ;
- instancing ou génération GPU pour la végétation ;
- chunks pour le culling ;
- LOD mesuré ;
- ombres bornées ;
- transparence et overdraw surveillés ;
- effets volumétriques en résolution adaptée ;
- chargements asynchrones ;
- compilation de pipelines préchauffée ;
- budgets par profil ;
- mesures après warm-up ;
- captures des 1 % low et p95, pas seulement FPS moyen.

### 13.3 Budgets de maintenabilité

- aucun fichier source ordinaire ne doit devenir un monolithe ;
- une crate ne doit pas mélanger domaine, UI, rendu et persistance ;
- toute API publique a une raison et des tests ;
- les warnings propres au projet sont des erreurs ;
- les dépendances inutilisées sont supprimées ;
- les features Cargo sont explicites ;
- le build WASM ne tire pas les dépendances natives inutiles ;
- le runtime ne tire pas Tauri ou Leptos.

---

## 14. Qualité, tests et preuves

### 14.1 Tests obligatoires

- tests unitaires du domaine ;
- sérialisation et migrations de schémas ;
- round-trip des documents ;
- déterminisme de génération ;
- commandes et inverses ;
- reprise après écriture interrompue ;
- validation de chemins ;
- packages malveillants ;
- physique et gameplay ;
- builds natifs et WASM ;
- tests d’intégration de Studio ;
- captures visuelles ;
- benchmarks de frame ;
- tests de chargement d’assets ;
- tests de licence et provenance.

### 14.2 Outils de qualité

Prévoir :

```text
cargo fmt --check
cargo clippy --workspace --all-targets --all-features -- -D warnings
cargo test --workspace
cargo deny check
cargo audit
cargo build --target wasm32-unknown-unknown
forge_cli validate <project>
forge_cli benchmark visual-valley
forge_cli capture visual-valley
```

Les commandes exactes ne doivent être documentées qu’après vérification réelle.

### 14.3 Preuves visuelles

Chaque jalon visuel produit :

- capture 1920 × 1080 ;
- capture 2560 × 1440 si possible ;
- paramètres graphiques ;
- GPU/CPU/driver ;
- seed ;
- commit ;
- moyenne, p95 et 1 % low ;
- draw calls, triangles et instances ;
- comparaison avec la référence précédente.

Les tests pixel-perfect sont réservés aux rendus déterministes. Les scènes complexes utilisent une
tolérance et une revue humaine.

### 14.4 Définition de terminé

Une tâche est terminée seulement si :

- critères d’acceptation satisfaits ;
- build et tests pertinents verts ;
- aucun warning projet ;
- métriques rapportées si performance ;
- capture inspectée si visuel ;
- documentation cohérente ;
- dépendances et licences vérifiées ;
- limites connues déclarées ;
- diff centré ;
- aucune affirmation de fonction non testée.

---

## 15. IA, MCP et productivité des agents

### 15.1 But

L’architecture doit maximiser la capacité d’un agent à produire un bon résultat et à se corriger.

Le workflow attendu est :

```text
comprendre une exigence
→ identifier un petit sous-graphe
→ modifier
→ compiler
→ lancer une preuve
→ capturer
→ lire les métriques
→ comparer
→ corriger
→ documenter
```

### 15.2 Contraintes

- pas d’édition de fichier arbitraire via le MCP produit ;
- pas de commande shell générique exposée aux utilisateurs ;
- mutations en dry-run par défaut ;
- plans liés à une révision ;
- diff avant application ;
- journal ;
- undo ;
- quotas ;
- provenance des contenus générés ;
- séparation lecture/mutation ;
- contexte ciblé ;
- scènes de reproduction rapides.

### 15.3 Design pour inspection

Chaque sous-système important fournit :

- README court ;
- diagramme de dépendances ;
- commandes de test ;
- exemple minimal ;
- types publics documentés ;
- diagnostics structurés ;
- erreurs actionnables ;
- données de test petites.

---

## 16. Sécurité et licences

### 16.1 Code

L’intention est de conserver pour le code propre au projet la licence
**PolyForm Noncommercial 1.0.0**, comme la ligne historique, sauf décision explicite du propriétaire.
La licence doit être ajoutée avant la première distribution de code. Toute distribution exige aussi
les notices tierces agrégées ; les fichiers substantiellement dérivés de Cadle restent identifiés MIT
avec leur copyright et ne sont jamais présentés comme PolyForm uniquement.

Les dépendances doivent avoir des licences compatibles avec la distribution prévue. Bevy est
MIT/Apache-2.0 et Rapier Apache-2.0 ; leurs notices doivent être agrégées dans les distributions.

### 16.2 Assets

- aucune source inconnue ;
- aucune licence implicite ;
- pas d’extraction de jeux commerciaux ;
- pas de redistribution d’un asset acheté sans droit ;
- provenance obligatoire ;
- dérivés reliés à la source ;
- séparation entre cache local et contenu redistribuable.

### 16.3 Projets et modules

- chemins relatifs ;
- pas de `..` ;
- pas de symlink hostile lors du packaging ;
- tailles et nombres de fichiers bornés ;
- décompression bornée ;
- pas de code natif communautaire ;
- API script sandboxée ;
- vérification des hashes ;
- atomicité des écritures ;
- récupération après interruption.

---

## 17. Critères de poursuite ou d’arrêt du reboot

Cette branche n’est pas poursuivie par inertie. Après Gate 1 et Gate 2, produire un scorecard :

| Critère | Mesure |
| --- | --- |
| Qualité visuelle | revue humaine et captures comparatives |
| Performance | moyenne, p95, 1 % low, VRAM |
| Productivité IA | nombre d’itérations nécessaires pour une amélioration visible |
| Complexité | taille du code, couplage, temps de build, nombre de couches |
| Studio | fluidité, ergonomie, stabilité de la WebView |
| Runtime natif | performances et parité des données |
| Assets | import, qualité PBR, provenance |
| Maintenabilité | tests, documentation, dépendances |

Le reboot continue si :

- la qualité visuelle est clairement supérieure à la base Godot ;
- les performances sont compatibles avec la cible ;
- l’éditeur permet une boucle plus rapide ;
- la complexité reste maîtrisée ;
- le partage natif/WASM est viable.

Il est réorienté si :

- le renderer Bevy limite la scène mais `wgpu` permet une extension ciblée ;
- Leptos/Tauri limite le Studio mais le runtime est bon ;
- la WebView est insuffisante et un Studio natif devient préférable.

Il est arrêté ou réduit à un prototype si :

- aucune amélioration visuelle nette n’est obtenue ;
- la cible 60 FPS exige des compromis inférieurs à Godot ;
- la double cible native/WASM multiplie durablement la complexité ;
- le développement du moteur absorbe tout le temps sans produire d’outil de création.

---

## 18. Plan de lancement exécutable pour Codex

Le prochain agent doit exécuter ce plan dans l’ordre. Il ne doit pas commencer par recopier tout Cadle
dans les crates Rust ni par construire un éditeur complet.

### Étape 0 — Préflight documentaire et Git

1. confirmer la branche `Rust` et un worktree propre ;
2. lire `AGENTS.md`, ce document, ADR-0001 à ADR-0006 et la roadmap ;
3. vérifier les références `main` et Cadle épinglées ;
4. créer un petit commit par résultat cohérent ;
5. ne jamais modifier `main` ni fusionner son historique.

Sortie : rapport de préflight court avec toolchains réellement disponibles et aucun code produit ajouté.

### Lot C0 — Copier et figer Cadle comme base de travail locale

1. créer `upstream/cadle.lock.json` avec URL, branche, commit, version et licences ;
2. ajouter `upstream/README.md` et ignorer `upstream/cadle/` dans Git ;
3. créer une commande reproductible Windows et une commande CI/Linux qui initialisent un dépôt local,
   fetchent le commit exact et checkoutent un HEAD détaché ;
4. refuser un commit différent, un arbre sale ou une URL substituée ;
5. conserver la copie upstream intacte ; les expériences et la qualification sous overlay vivent
   dans un clone jetable ignoré, hors de ce dossier ;
6. ajouter les notices de code nécessaires sans recopier les assets.

Sortie : un clone propre de Forge peut obtenir exactement Cadle `ea0da625...` dans
`upstream/cadle/`, mais le dépôt Forge et ses distributions ne contiennent aucun asset Cadle.

### Lot C1 — Établir la baseline Cadle

1. inspecter `package.json`, les deux lockfiles et les scripts avant d’utiliser Node 22 et `npm ci`
   dans le clone jetable borné par ADR-0003 ;
2. exécuter le build et uniquement les checks upstream explicitement qualifiés ;
3. lancer le snapshot source intact sous le seul overlay de métadonnées npm suivi ;
4. capturer au minimum The Vale, Whisperwood, l’eau, une scène dense et une scène de déplacement avec
   seed/heure/qualité fixées ;
5. exécuter un benchmark après préchauffage en 1080p High sans VSync ;
6. enregistrer moyenne, p95/p99, 1 % low, appels, triangles, mémoire, chargement et hitches ;
7. enregistrer aussi les défauts visibles et checks rouges : une référence n’est pas déclarée parfaite.

Sortie versionnée : `docs/baselines/cadle-ea0da625.md` avec commandes, paramètres, hashes, mesures et
observations. Les captures Cadle restent locales et ignorées ; elles ne sont ni committées ni publiées.
Ne pas inventer une réussite lorsqu’un test GPU ne peut pas être exécuté.

### Lot C2 — Transformer Cadle en carte de portage

1. créer `docs/migration/CADLE_REUSE_MATRIX.md` ;
2. classer chaque sous-système : reprendre la méthode, porter, réécrire, différer ou écarter ;
3. identifier les dépendances croisées et casser les monolithes en contrats Forge ;
4. choisir seulement les capacités nécessaires aux Gates 0 et 1 ;
5. créer pour chacune un test ou une preuve avant d’écrire son port ;
6. préparer `THIRD_PARTY_NOTICES` et les en-têtes d’attribution pour tout code substantiellement dérivé.

Priorité de reprise : harness → terrain/biomes → végétation/LOD → ciel/lumière/brouillard → eau →
contrôleur/collisions. Combat, ennemis, quêtes, loot et audio restent différés.

### Lot R0 — Construire le squelette Rust autour des preuves, pas autour des fichiers Cadle

1. exécuter Gate 0 de la roadmap ;
2. créer le workspace Cargo minimal et le document de scène partagé ;
3. faire fonctionner natif puis WASM/WebGPU ;
4. intégrer le canvas au Studio ;
5. porter dès R0 le format des diagnostics, captures et rapports comparatifs ;
6. vérifier automatiquement que Forge compile et fonctionne lorsque `upstream/cadle/` est absent ;
7. ne copier aucun runtime Three.js/JavaScript dans `apps/` ou `crates/`.

Sortie atteinte : même scène Forge affichée en natif et dans le Studio, validations locales vertes et
outils de comparaison prêts. La CI distante reste explicitement `NOT RUN` sous ADR-0006.

### Lot R1 — Reconstruire une vallée Forge en s’appuyant sur Cadle

Procéder en six sous-tranches jouables, chacune avec capture et benchmark :

1. terrain + height sampling + trois matériaux ;
2. caméra humaine + contrôleur Rapier + collisions ;
3. scatter déterministe + herbe + arbres + LOD/culling ;
4. soleil + ciel + CSM + brume + color grading ;
5. rivière/eau + berges + réflexion/réfraction bornées ;
6. profils qualité + warm-up + overlay + revue finale.

Pour chaque sous-tranche, comparer : référence Cadle intacte, dernière capture Forge et cible de budget.
La scène Cadle couvre 2 km² et Visual Valley 512 × 512 m : comparer l’impact visuel et les coûts par
système sur un protocole documenté, jamais présenter leurs FPS bruts comme un duel strictement égal.
Ne pas viser la reproduction exacte du Vale : `visual_valley` utilise un relief, des assets et une
identité Forge originaux, mais doit égaler ou dépasser la densité, la profondeur, la lisibilité et le
game feel observés.

### Gate de décision après R1

Continuer vers l’éditeur uniquement si :

- la scène Forge est jugée au moins aussi convaincante que la baseline Cadle sur les critères convenus ;
- elle tient les budgets PC milieu de gamme en natif et reste viable dans le viewport WebGPU ;
- aucune donnée canonique ne dépend de Bevy ou de Cadle ;
- aucun asset non redistribuable n’est nécessaire ;
- le port n’a pas introduit une seconde pile produit ;
- les outils permettent à un agent de capturer, mesurer et diagnostiquer sans opération cachée.

Sinon, produire un rapport de cause : asset/pipeline artistique, Bevy, shader, WASM/WebView, CPU, GPU ou
architecture. Toute décision de faire de Three.js/Cadle le runtime produit, de remplacer Bevy ou
d’abandonner Rust nécessite un nouvel ADR accepté ; elle n’est jamais prise par inertie pendant le port.

### Instruction de départ à donner à Codex

```text
Lis AGENTS.md puis PROJECT_CONTEXT.md sur la branche Rust. Exécute uniquement Gate C de la roadmap :
acquisition reproductible du snapshot Cadle épinglé, audit licences, baseline intacte et matrice de
reprise. Valide chaque résultat, mets à jour les preuves et la roadmap, puis arrête-toi au rapport de
Gate C. Ne commence pas encore le port Rust de Gate 0/1 et ne copie aucun asset Cadle dans Forge.
```

Cette première exécution est volontairement bornée : elle transforme Cadle en base fiable avant que le
travail de moteur ne commence.

---

## 19. Décisions encore ouvertes

ADR-0005 confirme Tauri 2 + Leptos/WASM + Bevy WebGPU pour le Studio ; le repli natif Bevy n’est pas
activé. Les décisions suivantes ne doivent pas être prises implicitement :

- fournisseur de navigation ;
- stratégie terrain exacte ;
- représentation GPU de la végétation ;
- système d’eau ;
- anti-aliasing principal ;
- format binaire runtime ;
- moteur audio final ;
- langage de scripting ;
- stratégie Git LFS/cache d’assets ;
- sous-ensembles Cadle à porter après mesure et techniques à abandonner ;
- compatibilité `.orpmod` v1 ;
- nom commercial éventuel de la nouvelle génération.

Chaque choix est tranché par une preuve et un ADR.

---

## 20. Références techniques vérifiées au 30 août 2026

- Cadle source : https://github.com/Humpalumps/Cadle/tree/ea0da62549980816a6a27e08b75808b1f56d70e7
- Cadle licence code : https://github.com/Humpalumps/Cadle/blob/ea0da62549980816a6a27e08b75808b1f56d70e7/LICENSE
- Cadle frontière assets/audio : https://github.com/Humpalumps/Cadle/blob/ea0da62549980816a6a27e08b75808b1f56d70e7/NOTICE
- Bevy 0.19 : https://bevy.org/news/bevy-0-19/
- Exemples Bevy WASM + WebGPU : https://bevy.org/examples-webgpu/
- Bevy et WebGPU : https://bevy.org/news/bevy-webgpu/
- wgpu : https://docs.rs/crate/wgpu/latest
- backend Browser WebGPU de wgpu : https://docs.rs/wgpu/latest/wasm32-unknown-unknown/wgpu/struct.Backends.html
- Rapier : https://rapier.rs/
- Tauri 2 : https://v2.tauri.app/
- Tauri + Leptos : https://v2.tauri.app/start/frontend/leptos/
- versions de WebView utilisées par Tauri : https://v2.tauri.app/reference/webview-versions/

Ces références justifient le point de départ, mais le lockfile et les preuves du dépôt sont la source de
vérité des versions réellement utilisées.

---

## 21. Résumé exécutable pour un agent

```text
MISSION
Construire OpenRPG Forge nouvelle génération en Rust/WebGPU, avec un Studio de qualité web et un
runtime natif, sans porter mécaniquement le code C#/Godot.

PRIORITÉ
Prouver d’abord une vallée 3D magnifique, éditable et fluide à 60 FPS sur PC milieu de gamme.

SOCLE
Rust 2024 + Bevy 0.19.x + wgpu/WebGPU + Rapier.
Studio : Tauri 2 + Leptos/WASM + canvas Bevy WebGPU, confirmé par ADR-0005.

SOURCE DE VÉRITÉ
Documents JSON versionnés. Jamais une scène Bevy sérialisée comme modèle métier.

MIGRATION
Reprendre exigences, données utiles, assets gouvernés et preuves depuis main.
Ne pas reprendre C#, Godot, WPF, DLL, scènes ou abstractions historiques.

MÉTHODE
Petites tranches verticales, commandes typées, tests, captures, métriques, documentation.

INTERDIT
Recréer les 717 fonctions avant la preuve visuelle ; écrire un renderer complet brut sans limite
mesurée ; ajouter des dépendances ou assets sans licence ; annoncer une fonction sans preuve.

UPSTREAM DE RÉFÉRENCE
Cadle v0.6.0 @ ea0da625 : copie locale épinglée, intacte et mesurée. Code MIT réutilisable avec
attribution ; assets exclus. Aucune dépendance Three.js/JavaScript dans le produit Forge.

TRAVAIL ACTIF
ADR-0011 : Gate 2 est maintenant active par accord explicite du propriétaire le 4 septembre.
Gate 1 est acceptée comme prototype avec réserves, techniquement incomplète à 43/55.
Commencer le parcours nouveau projet/module/zone, puis les outils d'édition selon la roadmap.
Herbe en zones, sol texturé et validations visuelles/mouvement/comparatives restent dus.
Les paragraphes suivants conservent l'historique des acquisitions Gate 1, pas le travail suivant.
Décision prioritaire ADR-0010 : critères stricts FPS/p95/1 % low/stutter et benchmark cible différés.
Ne pas relancer les audits PresentMon, leurs rebuilds ou les micro-optimisations sans nouvelle demande
explicite. Dernier replay gouverné 61713887 / 7fbcae7b : schema 13 FAIL (26,1661 FPS, p95 131,956 ms,
1 % low 3,4183 FPS), acquisition valide ; aucune optimisation produit promue. Les instructions de
replay qui suivent sont historiques. Les exigences non satisfaites de Gate 1 restent des réserves
sous ADR-0011 et ne bloquent plus le démarrage de Gate 2 ; aucun PASS n'est inventé.

Gate C et Gate 0 sont passées. Gate 1 reste ouverte sur la base logicielle qualifiée
`4e476b31ee33533b6f4023804d8ecd1be02f9c55` / `cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`.
Les derniers replays de cadence gouvernés restent attachés à `49acfd56` / `c6e7f6c5`.
La session PresentMon orpheline qui contaminait les anciennes captures a été arrêtée par identifiant
exact ; la garde `9db35a8e` refuse les sessions préexistantes et ne termine que la sienne. Le run
schema 7 historique conserve 128,7260 FPS, p95 13,9112 ms, 1 % low 58,3489 FPS, zéro intervalle
>33,3 ms, ETW 0/0 et 17 présentations non affichées.
Deux replays gouvernés schema 8 ont maintenant été acquis. Replay1 perd le focus sur 182/275
échantillons et reste FAIL (`36e1eebf…1d36d7bd`). Replay2 passe le focus 274/274, conserve 13 lignes
`NA`, ETW 0/0 et une résidence sans trou ; il mesure 129,9968 FPS, p95 13,8805 ms, p99 13,9329 ms et
1 % low 61,2708 FPS. Son unique valeur de 35,3837 ms au-dessus de 33,3 ms maintient toutefois le FAIL
(`45b87d0d…50894d1`) et `R1-048` ouverte. A4 schema 8 échoue sur un outlier ; A5/A6 passent seulement
comme diagnostics non qualifiants sur le binaire partagé `21228a…` sans rebuild gouverné propre.
Le replay schema 11 suivant, au commit `1912806`, publie ready + 338 lignes Forge PresentMon avec
ETW 0/0, puis s'arrête avant signal sur un gap de sampling fenêtre `>250 ms` ; il ne produit aucun
verdict de performance. Le harnais `9b847e99` utilise désormais host boundary schema 5 et validation
schema 12 avec restabilisation pré-signal bornée. `9d19cde5` s'arrête avant ready sur le validateur
qualité historique `5/14/15`. `4b283182` corrige le contrat, passe 141/141 moteur Release, 67/67
runtime et 1/1 intégration, puis franchit réellement la readiness en `5/16/13` avant un ABORT
pré-signal sur stdout PresentMon vide. `2ced195b` reproduit cet ABORT après un rebuild propre de
30 min 06 s. Le smoke non qualifiant `41b49f66` confirme ensuite que l'écran actif produit 22 lignes
Forge ETW 0/0. Le replay propre `83f02928` reçoit 25 lignes Forge ETW 0/0 après 32 min 27 s de build,
mais abandonne avant signal sur le gap final `row→sample` de 743,6604 ms >250 ms. Le durcissement
committé `473abeaac1425f0fb991f88ba0495d3708978ea1` déplace ce contrôle avant acceptation et rejoue
l'essai dans le budget existant de trois tentatives/15 secondes, sans signal prématuré ni seuil
élargi. Ses validations légères passent ; ce commit n'a pas été rejoué et aucun verdict de cadence
schema 12 n'est encore acquis.
La dernière campagne complète historique `15635b3f` conserve ses 140/140 tests moteur, 52/52 runtime
et 1/1 intégration. Le lot schema 8 `49acfd56` ajoute historiquement 66/66 tests Release
`forge_runtime --all-features`, le formatage, l'auto-test A/B schema 2 et le diff-check PASS. La chaîne
intégrée qualifiée passe Clippy moteur/runtime, 141/141 moteur, 69/69 runtime, 4/4 garde, 4/4 Rust
RenderDoc et 22/22 Python. Le portail complet `4e476b31` passe 354/354 tests workspace, Clippy
workspace, le check moteur WASM, Clippy Studio WASM, Trunk Release, `cargo deny` et RustSec.
La preuve visuelle exacte `125ba0b`, arbre `6d4639c8`,
conserve 29 PNG uniques et un MP4 de 24 checkpoints/3 secondes, 75 pipelines prêts et des logs runtime
vides. Son statut exact reste `automated_artifacts_valid_pending_stutter_human_and_gpu_qualification`.
Le harnais temporel Browser `4e476b31` est PASS sur 120 RAF,
3/3 ROIs végétation, témoin terrain, 4/4 bundles et journaux propres. L'acquisition réelle et son
validateur statique sont maintenant alignés sur le candidat qualifié exact ; overlay CPU/GPU afb270c3
acquis ; matrice native 5eadec7f 10/10, profils et pipelines acquis, eau synchronisée ; la paire
Browser/native d9e2670 conserve 24/24 par météo et 46/46 croisés. R1-033 est fermé N/A conditionnel.
Le harnais de matrice météo est committé à `48038854`; le durcissement d'entrée et de reçu d'ABORT est
intégré à `534ce4fa`. Son acquisition Low/High unique est PASS automatisé avec 6/6 checkpoints,
12 captures, 4/4 bundles et des journaux propres, sous le statut
`pass_pending_human_and_precise_gpu_evidence`. Les preuves restent externes ; aucune qualification
GPU précise, humaine ou de route n'est revendiquée.
L'optique d'eau `e7e7d419`, durcie par `e2d924b`, est validée automatiquement et réacquise à
`125ba0b`, sans qualification GPU/humaine. Le contrat/chemin de capture RenderDoc
`dbe5eb86`/`140acbdd`, durci jusqu'à `798b55d`, passe ses validations hors GPU ; son préflight réel
recoupait le bundle complet puis s'arrêtait sans mutation sur la couche Vulkan non enregistrée.
Depuis le 4 septembre, R1-037 est PASS : runtime `5f6ed579`, outillage `7d2db384`, quatre captures
Vulkan répétables et garde Git propre. Les critères de cadence R1-048 et le benchmark cible R1-041
restent différés sous ADR-0010 ; preuves GPU par matériau/Browser et revue humaine restent dues.
Le protocole R1-051 exact `73db7839`/`466cb51b`, SHA-256 `cc6d4918…6baeb4d`, est intégré au paquet
humain avec trois liens Grassworks externes et quatre vues Forge. Le paquet reste
`gate1_transition_authorized=false`, `gate1_routes_closed=0` et `human_review_performed=false` ;
R1-051 est prête à être relue mais reste ouverte.
Le lot `815ddf3` ajoute les diagnostics retardés de passes de scène ; sa première acquisition est
refusée pour neuf assets absents et n'est pas un PASS versionné. Le correctif `41b49f66` lie le runtime
au worktree propre et acquiert Low/High **frames-only** avec 29 PNG par tier, 6/12 mesures, logs vides,
neuf modèles CC0 lus en place et zéro copie. Ces diagnostics sans identité de frame ni attribution au
matériau eau ne prouvent pas la wetness shader ; `R1-023`, `R1-025` et `R1-026` restent ouvertes.
Aucun asset Cadle n'a été ajouté à Forge.
Poursuivre Gate 2 sous ADR-0011 ; Gate 3 attend son passage formel. La CI distante reste
`NOT RUN — infrastructure billing` sous ADR-0006 tant que les runners
ne démarrent pas.
```
