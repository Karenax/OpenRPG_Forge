# Tidewater — vent partagé et horloges de comparaison

La référence figée `1438b1abfcaee3267092b75573014f4d9b4a983c` définit
dans `src/engine/render/Frame.js` le vent à 7 m/s, direction normalisée
(0,35 ; 0,94). `App.js::_frame` incrémente le temps global par son delta.
La bibliothèque Rust possède déjà `WindFrame::source_defaults`, utilisé
par les matériaux importés et les pendules de la jetée.

## Écart corrigé dans le code

`authoring_imported_shadows::animate` avançait encore sa pose avec un vent
constant (1 ; 0), 6 m/s. Cette pose pilotait à la fois les feuilles visibles
et leurs déformations dans l'atlas d'ombres. Le code prend désormais direction
et vitesse depuis le `WindFrame` de la scène. Le même champ de mouvement
continue d'alimenter feuilles et ombres ; aucune géométrie source ou correction
d'auteur n'est modifiée. Le maintien de comparaison conserve sa pose figée.

Snapshot isolé : `.tidewater-integration/studio-source-wind-20261005-v1`,
1 362 fichiers issus du candidat qui a passé le cycle d'édition, avec une
seule surcharge `authoring_imported_shadows.rs` depuis le dépôt canonique.
Manifest SHA-256
`5e676a91a97adf640ab54da0e610a17ab38c677bc04739c471dfff72169e7fcd`.
Formatage ciblé et diff-check passent. Compilation WebGPU lancée ; aucune
capture du nouveau rendu ni parité visuelle n'est encore attribuée.

## Horloges : constat distinct, pas encore corrigé

Le ledger réel `stand-edit-runtime-v4/fish-stand-studio-smoke-ledger.json`
observe ces instants lors de son parcours joué, hors maintien de comparaison :

| Échantillon | Pose végétale (s) | Océan terminé (s) |
|---|---:|---:|
| Avant première marche | 4,3308 | 3,3000 |
| Après première marche | 9,3457 | 6,1000 |
| Avant seconde marche | 17,0512 | 10,0000 |
| Après seconde marche | 22,5381 | 12,8000 |

Ce sont des observations de transactions différentes, pas une lecture GPU
de tous les pixels à un même instant. Elles montrent cependant que le temps
virtuel seul ne suffit pas à établir un appariement temporel de la scène.
L'océan démarre à 2 s avec un delta plafonné à 0,1 s par transaction ; le
vent végétal accumule les écarts du temps virtuel entre ses soumissions.
Les nuages ont également leur contrôleur et leur historique propres.
La précédente capture Village n'est donc pas promue en preuve appariée.

La suite doit synchroniser les transactions et observer les temps réellement
soumis pour eau, végétation, poissons, pendules et nuages. Figer seulement
la caméra ou ajuster artificiellement un unique uniforme à 2 s ne satisfait
pas ce critère. Les dépendances GPU et l'initialisation physique doivent
pouvoir finir avant la capture. L'objectif reste actif.

## Contrôle WebGPU terminé

Le runner `Check.py` termine code 0 en 5 min 45 s, snapshot inchangé.
`result.json` limite explicitement le résultat à `cargo_checked=true` :
application non liée, GPU non exécuté, parité false. Aucun nouveau warning
de cette correction ; Cargo signale l'incompatibilité future connue de la
dépendance `proc-macro-error2 v2.0.1`. La construction du frontend lié est
lancée séparément par `BuildFrontend.py` ; aucun PASS anticipé.

Les quatre observations de temps ci-dessus sont également conservées avec
le hash du ledger d'entrée dans `observed-clock-drift.json`. Ce reçu indique
que le temps soumis des nuages n'est pas observé et ne qualifie aucun
appariement temporel.

## Delta source et observation des transactions

L'inspection complémentaire de `src/engine/Engine.js::start` établit que
le delta transmis à toute l'application source est plafonné à 0,1 s.
`SkyProClouds.js::update` applique aussi ce plafond. Le chemin végétal Rust
utilisait au contraire tout l'écart du temps virtuel entre deux soumissions
acceptées. Il plafonne désormais cet écart à 0,1 s, sans modifier le compteur
virtuel de la physique. Cette correction ne résout pas à elle seule les
origines de temps et les historiques d'acquisition indépendants.

`SubmittedFrame` expose désormais le vec4 de temps contenu dans le vrai
`Output.frame[10]` du contrôleur de nuages : index source, temps, delta,
padding. Il conserve un instantané possédé, pas une référence au contrôleur
suivant. Le test existant de préservation des uniformes vérifie aussi cette
valeur. Les diagnostics Studio publient temps virtuel, `WindFrame` partagé,
pose végétale et transaction nuage soumise avec étape encodée. Le champ
décrit explicitement des uniformes soumis et commandes encodées ; il ne
prétend pas observer leur achèvement GPU ni la parité des pixels.

Le test natif ciblé est lancé sur
`studio-clock-observation-20261005-v1` (manifest
`a283ed2405f4a8080d26801743e966897b2f372754c8eb42d7b2583a790d8435`).
Le snapshot v2 ajoute le plafond de delta végétal (manifest
`57d86be14fd9a1b07b134ce251cfbe3682a14cb16afe92c0b10a2fc1691e1218`).
Son contrôle WebGPU attend le runner de frontend PID 40672, créé le
5 octobre à 01:46:14,796397 +02:00, avec identité vérifiée en direct.
Les caches natif et WASM sont distincts ; aucun build n'est relancé dans un
cache occupé. À ce stade, aucun PASS n'est attribué à ces nouveaux contrôles.

## Contrôle d'appariement des uniformes préparé

L'overlay d'acquisition `scripts/tidewater/reference.html` enregistre désormais
les temps réels global, FFT et nuages après les 120 frames, ainsi que vitesse
et direction du vent. Les historiques de précompilation restent inchangés ;
aucun champ n'est artificiellement remis à l'heure globale. Syntaxe du module
JavaScript vérifiée avec Node, sans exécution GPU attribuée à ce contrôle.

`verify-animation-clock-pair.py` compare ces valeurs aux diagnostics natifs.
Il exige un maintien actif, un océan suspendu et drainé, aucun staging
d'ombres en attente et la transaction de nuages encodée. Il compare chaque
temps à son domaine source réel, sans supposer que les temps indépendants
doivent tous être égaux après acquisition. Six tests passent, dont refus
des métadonnées limitées au temps global, de dérives eau/nuages, d'un maintien
absent, d'un compteur non encodé, de valeurs non finies et de faux compteurs.
Même un résultat `uniform_clocks_match` laisse explicitement parité visuelle,
achèvement GPU et qualification temporelle à false : caméra, index source,
historiques, déplacement des rafales et phases des pendules restent à vérifier.

Le contrôle CLI a aussi été exécuté sur l'ancienne référence `baseline-003/village.json`
et le premier diagnostic vivant avant marche du ledger v4. Il termine code 1,
15 motifs de refus, comme attendu : ces deux observations ne constituent
pas une paire temporelle qualifiée. Le reçu avec les hashes d'entrée est
`studio-clock-observation-20261005-v2/historical-clock-pair-rejection.json`.
Les preuves originales restent intactes. Ce refus n'est pas une nouvelle
mesure des pixels de l'ancienne comparaison.

## Résultats des builds et correction de la macro JSON

Le test natif ciblé termine code 0 : exactement un test exécuté et réussi,
682 filtrés, source inchangée. Il exerce la vraie fonction `submitted_frame`
et vérifie la conservation du vec4 de temps dans son instantané possédé.
Le module moteur est identique entre les snapshots d'observation v1 et v2.
Ce PASS CPU ne qualifie pas l'application Studio ni les passes GPU.

Le frontend `studio-source-wind-20261005-v1` termine code 0, source inchangée,
après 19 min 29 s. WASM brut : 125 978 892 octets, SHA-256
`30f35a176cb67939d21ccba06c5fd3360f1d28c0c2cb8a57ab9a09810b43f044`.
Il contient la correction de direction/vitesse du vent, pas encore les
diagnostics complets ni le plafond de delta ajoutés dans les snapshots suivants.

Le contrôle WebGPU d'observation v2 termine code 101, source inchangée :
limite de récursion atteinte dans la grande macro `serde_json::json!`.
Son pipeline de candidat s'arrête avant construction. Le diagnostic détaillé
est dans `check.stdout` (messages JSON Cargo), pas dans le seul `check.stderr`.
La correction construit les quatre nouveaux champs d'horloge dans un objet
séparé puis étend l'objet existant. Aucun champ historique n'est supprimé
et aucune augmentation globale de limite n'est ajoutée.

Snapshot v3 : manifest
`42fd3d7a4270d1f13bebe2e1bb903bf45739938c957f9dec18b18c195e1e03f0`.
Son contrôle est lancé séparément et son pipeline identifie le processus
réel avant d'attendre. Il vérifie le reçu terminal avant la liaison WASM,
l'assemblage des bindings, la réutilisation du worker aux sources exactes
et la construction native. Aucun PASS V3 ni rendu visuel anticipé.

### Contrôle V3 terminé

Le contrôle V3 termine code 0 en 1 min 26 s, source inchangée. Les deux
objets JSON sont fusionnés sans dépasser la limite de macro, avec le même
schéma externe. Le plafond de delta et les diagnostics complets compilent
ensemble pour WebGPU. Aucune exécution GPU ni parité visuelle déduite.
Le runner de candidat passe alors à `BuildFrontend.py`, lancé le
5 octobre à 00:12:41 UTC. L'assemblage frontend et la construction native
restent dépendants de son résultat terminal.

## Relevé joué préparé sur une carte intacte

`Capture-AnimationClocks.ps1` est préparé pour le candidat staged avec son
frontend adjacent vérifié. Le harness refuse une sortie existante, les
chemins source/sortie superposés, un EXE ou un inventaire non épinglé, les
junctions et les ressources importées dont les pins divergent. Il copie la
fixture originale et exige les 181 identités/libellés indépendants avant
le passage joué. Il ne passe pas par le scénario d'insertion de l'étal,
qui ajouterait un objet et ne représenterait plus la carte source intacte.

Le parcours sélectionne la recette Village existante, attend simulation,
caméra et nouveaux diagnostics, puis actionne le contrôle public de maintien.
Il enregistre avant/après drainage et après attente, ainsi qu'une capture
1920×1080. Preview, Apply et Save doivent rester à zéro. Les fichiers
originaux et copiés sont relus après fermeture ; seul le processus lancé
est fermé après contrôle de son identité. Le reçu laisse parité visuelle,
achèvement GPU et qualification temporelle à false.

La syntaxe PowerShell est vérifiée. Aucun passage joué de ce nouveau harness
n'est encore attribué : le candidat contenant les champs nécessaires est
toujours en construction. Le relevé doit ensuite déterminer quels domaines
restent décalés et si les historiques continuent d'avancer pendant le maintien.

## Construction native et sources figées

Le frontend V3 termine code 0, avec sources inchangées. WASM brut :
126 013 365 octets, SHA-256
`0342ebac51407dab62b8910a2f5009e58f96dd1ceb215a7547297d1946e5cd74`.
Les bindings et le worker vérifié sont assemblés ; le frontend compte
3 020 fichiers. La première tentative native est arrêtée par le constructeur :
il compare le worker aux sources du dépôt courant, dont
`crates/forge_navigation/src/provider.rs` diffère désormais de l'instantané.
Le worker correspond toujours aux sources effectivement compilées du candidat.
Les reçus de cet échec et les sources existantes sont conservés.

`Build-StudioCandidate.ps1` accepte désormais, uniquement en `ValidateOnly`,
un répertoire source figé avec l'empreinte obligatoire de son manifeste.
Le contrôle vérifie l'ensemble exact des fichiers, leurs empreintes, l'absence
de reparse points et les chemins avant de comparer les pins du worker à cette
source. Le contrôle de branche reste sur le dépôt canonique. Les constructions
ordinaires restent vérifiées contre le dépôt courant.

`BuildNativeFrozenV1.py` obtient `INPUTS_VALID` sur l'instantané V3 intact,
puis lance la construction native. Le constructeur et le runner sont épinglés
dans `native-frozen-validation-request-v1.json`. Le relevé en attente
`StageClockObservationCaptureV2.py` identifie ce runner réel et exige son reçu
terminal ainsi que les empreintes des helpers avant de lancer Studio.
Aucune exécution du relevé ni parité visuelle n'est encore revendiquée.

Les trois contrôles négatifs du constructeur terminent comme attendu :
worker périmé contre le dépôt courant, manifeste omis et empreinte de manifeste
incorrecte sont tous refusés. Aucun répertoire de sortie n'est créé.
Le reçu `frozen-source-validation-tests-v1.json` associe ces refus à
l'acceptation positive de l'instantané et à l'empreinte du constructeur.
`git diff --check` sur le script modifié termine code 0.

## Acquisition des horloges source réelles

Le clone `upstream` est actuellement au commit figé `1438b1ab`, sans changement
suivi. L'acquisition utilise un nouvel export `git archive`, conserve son ZIP
et épingle tous les fichiers exportés ainsi que l'overlay et le driver CDP.
Elle ne sert pas une ancienne copie de travail expérimentale. Le viewport
1920×1080 est posé avant le chargement, puis les 120 pas de 1/60 s sont exécutés
avec attente de la queue GPU dans l'overlay.

L'essai `reference-clock-acquisition-20261005-v1` s'arrête avant le navigateur :
le contrôle comparait à tort le HTML transformé par Vite au HTML source brut.
Les fichiers exportés restent inchangés. L'essai V2 obtient un vrai PNG Village
1920×1080 et les uniforms après acquisition, mais le driver refuse la session
pour le seul HTTP 404 de `/favicon.ico`. L'exception lors de fermeture déclenche
ensuite une assertion libuv de Node ; l'essai reste conservé en échec.

Les métadonnées V2 observées donnent temps global 2,000 s, temps océan et
nuages 2,066666666666664 s, clock nuages `[123,2.066666666666664,1/60,0]`,
vent 7 m/s et direction `(0.34893735911823864,0.9371460502032696)`.
Le PNG est inspecté : le Village est rendu avec végétation et ombres ; cette
observation source ne prouve aucune parité avec Forge.

Le driver conserve maintenant toutes les erreurs et exclut des erreurs
bloquantes uniquement le 404 exact du favicon sur le serveur acquis. Il attend
la fermeture du WebSocket avant de fermer son onglet. Il relève explicitement
les propriétés de l'adapter réel de l'application. L'acquisition V3 est lancée
séparément ; aucun succès V3 ni synchronisation native n'est anticipé.

L'acquisition V3 termine maintenant code 0, export et driver inchangés.
Le seul 404 du favicon est conservé ; aucune erreur bloquante n'est relevée.
L'adapter de l'application rapporte AMD / RDNA-3, les autres identifiants étant
masqués. Le PNG V3 est inspecté et l'audit indépendant vérifie sa signature,
ses dimensions 1920×1080, l'absence des motifs de répétition historiques,
le DPR 1, les 120 pas et les empreintes des trois fichiers acquis.
Les horloges relevées concordent avec celles observées lors de V2 :
écart océan/global de 0,06666666666666643 s. Le reçu est
`reference-clock-acquisition-20261005-v3/source-clock-audit.json`.
PNG SHA-256 : `762bffb4f752368109e540e323c0b6827ef212a8aa6ff2bd1f54902048180d49`.
Il s'agit d'une nouvelle référence source vérifiée, pas d'une paire Forge
qualifiée ni d'une mesure de performances.

## Analyse des observations natives préparée

`analyze-animation-clock-observation.py` lit les trois phases avant, maintien
drainé et observation ultérieure. Il calcule les écarts de chaque temps au
temps virtuel et les changements sous maintien, en distinguant temps et
transactions des nuages. Des horloges stables mais décalées ne constituent pas
une synchronisation. Des compteurs cloud qui avancent malgré un temps immobile
ne sont pas assimilés à des historiques gelés.

Les quatre tests passent : écarts conservés malgré gel, compteurs cloud
avançant seuls, dérive de temps et refus des mesures absentes/non finies/booléennes
ou du maintien absent. Le reçu reste limité aux diagnostics, sans pixels GPU.
Le changement de viewport entre maintien et observation ultérieure est signalé
explicitement comme cause possible de nouvelles transactions.

Le runner `AnalyzeCapturedAnimationClocksV1.py` identifie le processus réel du
relevé en attente, vérifie son identité jusqu'à sa terminaison, exige un reçu
terminal réussi et épingle le ledger et le helper avant analyse. Aucun résultat
natif de cette analyse n'est encore attribué : le constructeur natif reste actif.
L'inspection de ses sources figées confirme que la continuation des nuages
retourne immédiatement pendant le maintien. La stabilité effective des dernières
transactions encodées reste à mesurer, elle ne découle pas de ce seul code.

## Candidat natif V3 construit

`BuildNativeFrozenV1.py` termine code 0 après 19 min 55 s de construction.
Les sources figées et les 3 020 fichiers frontend sont inchangés. Exécutable
`studio-clock-observation-20261005-v3/desktop/forge-studio-candidate.exe`,
SHA-256 `4abbb6d5a90a672c14e0972737d3ab66d4295ca623ade33e2967b04a86efbecd`.
Le reçu reste `desktop-frozen-build-only`, runtime et parité non qualifiés.
Le pipeline de capture peut maintenant préparer sa copie dédiée puis ouvrir
la fixture intacte ; cette réussite de construction ne vaut pas réussite du relevé.

Le relevé V1 termine code 0 avec EXE, frontend et helpers inchangés, sources
intactes et listener fermé. Son PNG Village est inspecté. L'analyse termine
néanmoins en refus : `Get-WorldState` lit un diagnostic générique vide ici,
alors que les attentes du harness lisent correctement
`data-tidewater-live-shadows`. Les trois phases enregistrées contiennent donc
`diagnostic: null`. Aucun temps ne peut être déduit de ce ledger ; la capture
visuelle ne répare pas cette absence. Les deux reçus restent conservés.

Le harness de relevé lit maintenant explicitement le diagnostic d'animations
dans `Get-AnimationWorldState`, exige ses champs et consigne l'attribut lu.
Il conserve le reste de l'audit générique. Sa syntaxe PowerShell passe.
`StageClockObservationCaptureV3.py` rejoue sur une sortie fraîche
`.placeables-smoke-clock-observation-20261005-v3-recapture1`, avec le même EXE
et les mêmes sources de carte, sans nouvelle compilation ni retouche auteur.

## Relevé corrigé et mesures réelles

Le relevé corrigé termine code 0 : candidat, frontend et helpers inchangés,
aucune Preview/Apply/Save, sources préservées et listener fermé.
`animation-clock-analysis-v2.json` termine `measured`, sans champ absent.
Son ledger d'entrée a SHA-256
`fc18b4479a2c2e97ce4a936cdc3a4f44b610d8f5dd6ff9feb14155b863348451`.

Au premier relevé : temps virtuel 6,4485 s, vent de scène 6,4485 s,
plantes 3,20000005 s, océan 3,30 s, nuages 3,41666675 s.
Après maintien drainé : temps virtuel/vent 6,7634 s, plantes 3,29999995 s,
océan 3,50 s, nuages 3,51666665 s. L'observation ultérieure conserve exactement
ces temps, le frame végétal 138, le frame source des nuages 135 et les compteurs
cloud soumis/encodés 136. Le maintien fonctionne sur ces diagnostics.
Il ne synchronise pas les horloges déjà décalées ; aucune parité n'est qualifiée.

L'inspection révèle deux alimentations différentes : le `WindFrame` de scène
avance par le delta virtuel entier à chaque update, tandis que les poses
végétales et les transactions océan/nuages plafonnent leurs deltas à 0,1 s
et peuvent attendre une soumission disponible. L'horloge océan est initialisée
à 2 s ; le vent de scène est installé à 0 s. La correction doit considérer
l'acquisition initiale et la cadence commune des consommateurs, puis être
rejouée sur le même parcours. Plafonner arbitrairement une seule horloge
ne prouverait pas cette synchronisation ni la fidélité à l'initialisation source.

## Plafond source du temps de matériaux

L'alimentation de `WindFrame::advance` est corrigée pour respecter le plafond
de 0,1 s appliqué par `src/engine/Engine.js::start` avant chaque `App.frame`.
Le delta est validé avant le plafond : NaN, infini et valeurs négatives restent
refusés sans remplacer le frame acquis. Les deltas ordinaires et la pause sont
conservés. Aucun changement du temps physique Bevy n'est introduit.

Deux tests du vrai module moteur vérifient une succession de frames normales,
lentes et nulles contre le calcul source, puis la préservation sur refus.
L'instantané `studio-source-frame-delta-20261005-v1` contient les 1 362 fichiers
V3 intacts sauf cet overlay, manifeste SHA-256
`bc52f6c6549f20e4bcad46beeb0d07ccf9a3f3254592bc70b9cbc915b4513539`.
Les tests natifs et le check WebGPU sont lancés dans deux caches distincts.
Leur résultat terminal n'est pas encore attribué.

Ce plafond corrige l'écart au contrat source pour les matériaux. Il ne règle
pas à lui seul les origines 0/2 s ni les attentes de soumission propres aux
plantes, à l'océan et aux nuages. Le raccord d'une cadence commune et les phases
d'acquisition restent à réaliser, puis à mesurer dans un nouveau candidat.

Le relevé source est étendu au véritable uniforme `uGustOffset` de
`world/vegetation/VegNodes.js`. L'acquisition V4 termine code 0, export et
driver inchangés. Elle observe `(4.6152781366039095,12.395318424021925)` après
les frames de précompilation et les 120 pas, tandis que le temps global vaut
2 s et le temps océan/nuages 2,066666666666664 s. Ces valeurs correspondent à
l'intégration source du vent sur les frames acquises. L'horloge végétale Rust
est actuellement initialisée avec un déplacement nul : conserver ce zéro
dans un futur raccord commun perdrait la phase de rafales source.
Les pièces sont dans `reference-clock-acquisition-20261005-v4/`.
Les deux validations de la correction de delta, initialement en attente du
cache de paquets, sont désormais en compilation ; aucun résultat terminal anticipé.

## Échelle de vitesse de l'océan et frames lentes

`LiveTimeline::offer` plafonnait `(delta * time_scale)` à 0,1 s. La source
plafonne d'abord le delta applicatif dans Engine.js, puis
`OceanFFT.js::update` ajoute `dt * timeScale`. Le plafond est donc déplacé
avant le multiplicateur. À vitesse 1,25 après une longue frame, le pas devient
0,125 s au lieu de 0,1 s ; les frames ordinaires restent inchangées.
Une échelle qui ne permet pas un delta GPU fini est refusée atomiquement.

Le module exact `tidewater_ocean/live_clock.rs`, indépendant de Bevy, est figé
et testé directement avec rustc 1.98. Les tests terminent code 0, copie et source
canonique identiques, SHA-256
`6444730eec0eccc0f79d8f9148779efa7624a6ea362e10a406d6afd3ca8e9ab2`.
Preuve : `ocean-source-delta-clock-20261005-v1/result.json` et `tests.log`.
Les tests couvrent les fréquences communes, la frame lente, le pending, la pause,
la reprise et les refus. Il s'agit du véritable module CPU, pas du graph GPU.
Cette correction océan n'est pas incluse dans l'instantané de validation du vent
lancé auparavant ; elle devra être intégrée au prochain candidat commun.

Résultats terminaux : neuf tests CPU de l'océan passent. Le check WebGPU du
vent termine code 0 en 7 min 57 s, sources figées inchangées. L'avertissement
de compatibilité future préexistant de `proc-macro-error2` reste consigné.
Les deux tests du vent dans le véritable module Engine sont encore en compilation
native. Aucun frontend lié ou nouveau rendu n'est qualifié par ce check.

## Phases acquises raccordées dans Studio

Le test natif du vent v1 termine code 101, sources intactes : le tableau de
deltas du nouveau test nécessite un type `f64` explicite avant l'appel à `min`.
Cette ambiguïté de test est corrigée. Le check WebGPU précédent reste réussi
avec sa portée propre ; aucun succès natif v1 n'est attribué.

Les métadonnées V4 non modifiées sont retenues dans
`third_party/tidewater/acquisitions/animation-reference-village-1438b1ab.json`,
SHA-256 `8d24e214166c6f59022e094b64257fd5fea7e34c998e97cf235d103481ee2e2e`.
Le nouveau lecteur `tidewater_wind::AcquiredFrame` vérifie provenance, séquence
de warmup, valeurs finies et projection GPU avant adoption. Il conserve les
phases océan/nuages indépendantes pour leur futur raccord.

Studio initialise maintenant le vent global depuis le temps et le vent acquis,
au lieu de zéro. Les matériaux végétaux de départ et l'horloge d'ombres utilisent
la même phase globale, la direction/vitesse source et le déplacement de rafales
réel. `Clock::from_acquired` accepte explicitement cette pose mesurée ; les
defaults des anciennes sondes isolées restent inchangés. L'installation des
ombres retourne désormais ses erreurs au parcours de préparation existant.
Il n'y a aucune condition sur le nom ou le slug d'une carte dans l'éditeur.

Les six tests du module exact d'horloge végétale passent avec rustc 1.98,
dont conservation d'une phase acquise lors d'un delta nul et d'un changement
de direction. L'instantané `studio-acquired-animation-phase-20261005-v1`
contient 1 364 fichiers, manifeste SHA-256
`871c424eddd6f8eaedd695257ac32bbf0804ceadaa0442c2e96e4727e32c9c02`.
Il comprend les deux corrections de delta, le lecteur, le fixture et les
raccords Studio. Le check WebGPU et les tests du vrai module Engine sont lancés
dans des caches distincts, sans résultat terminal anticipé.

Ce raccord corrige les phases initiales du vent et des plantes. La cadence
commune des soumissions océan/nuages/ombres reste ouverte, ainsi que l'adoption
des phases initiales océan/nuages par leurs graphs et les historiques GPU.
Les premiers batches statiques d'ombres utilisent encore leurs anciens uniforms
avant le premier paquet dynamique : cette limite doit aussi être résolue avant
une qualification de démarrage complet. Aucune nouvelle parité n'est attribuée.

Le check WebGPU de ce raccord termine désormais code 0 en 2 min 26 s,
sources figées inchangées. Le lecteur du fixture, l'installation avec retour
d'erreur et les consommateurs Studio compilent ensemble. Les tests natifs
Engine sont encore actifs ; ce check ne prouve pas une exécution du lecteur
ni un rendu source/Forge apparié.

## Premiers batches d'ombres et validation native

Les trois tests natifs du vrai module Engine terminent code 0 : plafond source
du delta, refus atomique et lecture de l'acquisition réelle. L'instantané v1
reste inchangé ; journal `native-wind-clock-test.stdout`.

Les batches d'ombres peuvent maintenant recevoir une `initial_motion` validée,
avec la même caméra que le batch. Studio fournit la pose acquise dès la
préparation, avant toute soumission dynamique. Les anciennes sondes gardent
leurs uniforms par défaut lorsqu'aucune pose n'est fournie.

L'instantané `studio-acquired-animation-phase-20261005-v2` contient 1 364
fichiers, manifeste SHA-256
`e781e701ca29b7bab5a96bf5797eb82bdb47ef2352ce021898a4beb52cc1f776`.
Son check WebGPU termine code 0 en 2 min 12 s, source inchangée.
Cela ne prouve pas encore que le GPU utilise ces uniforms.

Une sonde native isolée est préparée dans
`initial-shadow-gpu-20261005-v1`, depuis cet instantané et un seul overlay
d'exemple. Elle compare ancienne pose et pose acquise sur le même arbre puis
le même palmier, avec caméra et matrices de lumière fixes. Une répétition
doit être identique. Aucune animation dynamique n'est installée : la différence
doit venir des uniforms initiaux. Le résultat GPU reste en attente ; les
historiques océan/nuages, la cadence commune et les ombres sur les récepteurs
Studio demeurent ouverts.

La lecture de l'export source V4 explique précisément les quatre pas conservés :
`src/App.js::precompile` exécute deux `frame(1/60)` pour les variantes de masque
de coque, puis `init` en exécute deux pour le warmup. Le driver d'acquisition
réinitialise ensuite seulement `G.time`, pas l'océan ni les nuages, avant ses
120 pas. Le temps global acquis vaut donc 2 s, tandis que les deux effets
conservent 124 pas et un temps de 2,066666666666664 s.

Le graph océan Forge vérifie et rejoue actuellement son acquisition dédiée
de 120 pas (`BOOTSTRAP_STEPS` dans `tidewater_ocean.rs`). Son temps de 2 s
est cohérent avec ce paquet, mais ne reproduit pas l'historique de l'acquisition
App V4. Le prochain raccord devra porter une séquence de préparation explicite
et ses historiques GPU ; modifier uniquement le timestamp de `LiveClock`
laisserait les textures temporelles dans une autre phase. Aucun compteur ou
uniform océan n'est modifié sur la base d'une simple égalité de timestamps.

## Historique source observé sur 124 frames

L'overlay observe désormais chaque appel original à `App.frame`, sans modifier
son ordre, et retient les horloges avant/après, le vent, les rafales et les
uniforms de caméra consommés par SkyProClouds. Le driver refuse une séquence
incomplète, un delta inattendu ou une discontinuité des temps océan.

L'acquisition v5 échoue : la première instrumentation utilisait les champs de
caméra de l'ancien `Clouds`, alors que l'App choisit `SkyProClouds` par défaut.
Les erreurs, l'assertion libuv à la fermeture et les exports intacts restent
dans `reference-clock-acquisition-20261005-v5`; aucune preuve n'en est tirée.
L'instrumentation est corrigée pour lire `F.position/right/up/forward`.

L'acquisition v6 termine code 0, archive exportée et driver copiés inchangés,
sans erreur navigateur bloquante. Le favicon 404 est retenu séparément.
Métadonnées : `reference-clock-acquisition-20261005-v6/capture/village.json`,
SHA-256 `3204b484afb5efe4f1d63d966824bc9c8d64c967077108960f7b8d42b6612821`.
La capture 1920x1080 sur AMD RDNA3 a été inspectée.

Les quatre premières frames consomment la caméra de côte
`(53.6,4.447713719743241,-77)`. La cinquième démarre avec le temps global
remis à zéro et la caméra village `(42,14,-90)` ; l'océan vaut alors
0,08333333333333333 s et le compteur nuages vaut 4. Les 120 frames de village
suivent sans reset de ces effets. Le lecteur intégré continue à utiliser son
fixture v4 intact : le nouveau relevé prépare le raccord des historiques, il
ne qualifie ni un replay Rust ni une parité visuelle Forge.

## Séquences de préparation déclarées dans le paquet et le graph nuages

Le paquet environnement v1 garde son contrat exact de 120 frames et 266 fichiers.
Le lecteur accepte également un v2 portant `bootstrap_frames` explicite,
borné entre 1 et 256, avec la liste exacte des ressources de chaque frame.
Une séquence de 124 frames demande 274 fichiers : une dépendance manquante,
une longueur absente ou hors limite et une longueur ajoutée à un v1 sont refusées.
Deux tests natifs du manifeste couvrent ces cas ; leur exécution reste en attente.

La préparation environnement vérifie que le nombre de sorties du contrôleur
correspond au paquet, puis adopte les paramètres publics de sa dernière frame.
Le graph nuages utilise la longueur reçue pour sa progression et son signal
ready. Le chemin sans accumulation temporelle choisit la dernière frame reçue,
au lieu de l'index fixe 119.

Une séquence explicite transporte aussi les décisions du contrôleur pour chaque
frame : reconstruction des ombres/panorama, trace de vue et index de texture.
La validation exige ces décisions pour les séquences nouvelles et vérifie leur
longueur et leurs index. Le parcours des anciens probes reste inchangé lorsqu'ils
omettent cette option. Le packageur suit le nombre de `controller_frames` fourni.

L'instantané `studio-cloud-acquired-sequence-20261005-v1` contient 1 364 fichiers,
manifeste SHA-256 `16f3e6ffd272221e4363e0aaccc84c4d34728af0368e834836bb4ad948f16746`.
Le check WebGPU est lancé dans un cache distinct du build natif d'ombres.
Aucun résultat de compilation ou d'exécution anticipé.

Ce raccord enlève les plafonds de 120 frames dans le transport et le graph ;
il ne remplace pas encore le paquet Studio courant. Les blocs global/atmosphère
effectifs de chaque frame et les décisions source devront être acquis, vérifiés
contre le contrôleur puis empaquetés. Le refus existant des paramètres atmosphère
variables dans un environnement fixe reste actif : aucune approximation n'est
acceptée silencieusement pour les quatre frames de côte.

## Blocs GPU effectifs et replay CPU du contrôleur

L'acquisition v7 termine code 0, source exportée et driver copiés intacts.
Elle retient pour chaque frame les entrées au moment exact de `SkyProClouds.update`
et les miroirs `_last` des blocs envoyés au GPU : frame 544 octets, réglages
208, paramètres publics 80, global 720 et atmosphère 64. Métadonnées SHA-256
`8815a6bd6af5bfbc27301f9500faa4d4c5fcc7efa98ec481f3a0ababc574c7d2`.

Le replay v1 compile et exécute le véritable `cloud_controller.rs`, figé,
avec les 124 entrées observées. Il relève 240 blocs différents ; les 1 080
lanes différentes sont exclusivement des zéros de signe opposé dans les
bases de caméra. JSON a normalisé les `-0` source en `0`. Le verdict strict
`DIFFERENCES` est conservé, sans tolérance ajoutée au comparateur.

L'acquisition v8 ajoute un transport float64 LE de la caméra, qui conserve les
valeurs originales et leurs zéros signés. Elle termine code 0, export et
driver inchangés. Métadonnées SHA-256
`b69d7129cb8ba7a4ed2a17045255838bb31c90f5a01e575a29386bd9d6f6cb73`.
Le replay v2 lit ce transport et exécute le même véritable contrôleur Rust,
sans correction de ses calculs. Compilation et exécution terminent code 0.
Les 372 blocs de sortie (frame, réglages, paramètres publics sur 124 frames),
soit 103 168 octets, sont identiques bit à bit aux uniforms GPU source.
Preuves : `cloud-controller-acquired-replay-20261005-v2/input-pins.json`,
`expected.bin`, `actual.bin`, `compile.log`, `run.log` et `result.json`.

Ce résultat prouve le contrôleur CPU sur la séquence acquise, y compris le
changement de caméra. Aucun GPU Rust n'est exécuté par ce replay.
Les données source contiennent un seul bloc de réglages et aucun pas sous l'eau,
mais deux blocs atmosphère : le lane 7 (rayon d'observateur) passe d'environ
6360,004 km sur la côte à 6360,014 km dans le village. Le futur replay GPU doit
donc recalculer la sky-view pour ces deux hauteurs avant les frames concernées ;
remplacer simplement les quatre premiers blocs par les valeurs du village
ne reproduirait pas l'acquisition. Cette limite reste explicite.

## Raccord de l'observateur et lecteur de caméra exact

Le check WebGPU de `studio-cloud-acquired-sequence-20261005-v1` termine code 0,
avec instantané intact. Il contrôle la compilation, sans application liée,
exécution GPU ni verdict de parité visuelle.

Le raccord suivant autorise uniquement le changement du rayon d'observateur
dans les 64 octets atmosphère acquis : les quinze autres lanes doivent rester
identiques au bloc résident. Les valeurs non finies, non positives et les blocs
tronqués sont refusés. Les trois tests du validateur passent dans
`atmosphere-observer-tests-20261005-v1` ; SHA-256 du fichier testé :
`5623ad78ca0f92b90a3d2ee5a4ef14874de4929865e685eb44d2d3ef5388840a`.

Le graph réencode la sky-view et l'irradiance avant la frame de nuages concernée,
avec les kernels et ressources atmosphère existants. Cette possibilité exige
une séquence exécutée pas à pas, un bloc par frame et la génération partagée
active. Les paramètres de diffusion ne sont ni remplacés ni approximés.
Ce raccord remplace le refus global des hauteurs variables décrit plus haut ;
le refus de changements des paramètres statiques reste en place.

Le lecteur de caméra accepte `camera.basis_bits`, quatre colonnes de trois
patterns u64 représentant les float64 source. Il préserve les zéros signés,
refuse les colonnes manquantes, mal formées ou non finies et conserve le chemin
quaternion des anciennes acquisitions. Une base explicite invalide ne déclenche
pas de repli silencieux sur le quaternion.

Les sept tests du véritable lecteur et contrôleur passent dans
`acquired-camera-tests-20261005-v1`, dont conservation des zéros signés,
refus de données invalides et continuation. Le lecteur testé a pour SHA-256
`e51e584215c0557124e948631b5936557ec1370789afde8bfa44cda99cd39f7f`.

`cloud-adapter-acquired-replay-20261005-v1` transporte ensuite les 124 entrées
v8 dans des métadonnées JSON et exécute le véritable
`generation_and_continuation`. Résultat `EXACT`, code 0 : les 372 blocs,
103 168 octets, correspondent bit à bit aux miroirs GPU source. Les fichiers
source et d'entrée restent intacts. Cette preuve couvre le lecteur de production
et le contrôleur CPU ; elle n'exécute pas le GPU Rust.

L'instantané combiné `studio-observer-acquired-replay-20261005-v1` compte
1 365 fichiers, manifeste SHA-256
`0944741d1fd215b5061c3f6a5d09edde1fad06bbf4c2053e58fe4a5114e1d2d9`.
Son check WebGPU est lancé ; aucun verdict GPU ou visuel n'est anticipé.
Le paquet Studio courant n'est pas remplacé par ces seules preuves CPU.

## Probe natif d'ombres : correction du constructeur

Le build `initial-shadow-gpu-20261005-v1` termine code 101 : le constructeur
de plantes de l'exemple omettait le nouveau champ `initial_motion` (E0063).
Aucune capture GPU n'a été exécutée avec ce candidat. Le constructeur canonical
est corrigé avec `initial_motion: None` ; le mode acquis du probe fournit ensuite
la pose source attendue. Un nouvel instantané v2 est construit sans modifier v1.
Le build v2 reste en cours à cette étape ; les captures de profondeur et leur
comparaison ne sont pas encore acquises.

## Paquet candidat de la séquence acquise

`environment-acquired-cloud-candidate-20261005-v1/package` est produit par le
véritable packageur figé, avec les 124 `controller_frames` du replay exact,
les blocs global/atmosphère effectifs v8 et le dernier bloc public source.
Il contient 274 fichiers ; manifeste v2 SHA-256
`800b59a182ae36fcf745196afcd3361dd780cff2e5b3d5a81f8e14b5d2db2d92`.
L'ancien paquet 001 et ses ressources restent intacts. Les paramètres
atmosphériques statiques sont identiques à ceux des frames acquises ; seules
les deux hauteurs d'observateur diffèrent. Les ressources bruit/shaders gardent
les pins du paquet source figé. Aucun résultat de nuages GPU n'est embarqué.

`environment-acquired-package-tests-20261005-v1` compile les véritables modules
du lecteur de paquet, du contrôleur, du lecteur de caméra et du validateur
d'observateur. Les 14 tests passent, incluant les deux tests de manifeste v1/v2
précédemment en attente. Le programme de validation charge les 274 fichiers par
`EnvironmentPackage::load`, vérifie leurs tailles/hashes, les 124 frames,
l'égalité du dernier bloc public et le contrat d'observateur sur chaque frame.
Tests et chargement terminent code 0, fichiers source intacts.

Ce candidat conserve explicitement le cube HDR et l'éclairage de l'ancienne
acquisition froide à 120 frames. Leur acquisition avec l'historique App complet
reste à faire ; ce paquet n'est donc pas une preuve de parité d'éclairage.
Il n'est pas adopté par Studio et n'exécute pas le GPU Rust. La preuve de
chargement ne remplace pas la préparation sémantique du renderer, son exécution
native/WebGPU ni la comparaison d'images.

## Environnement HDR après les 124 frames réelles

Le check WebGPU combiné `studio-observer-acquired-replay-20261005-v1`
termine code 0, instantané intact. Il n'a ni lié l'application ni exécuté le GPU.

Le nouvel overlay `reference-environment.js` lit après le warmup les neuf
coefficients SH et le cube préfiltré de l'App original. Aucun `App.frame` n'est
appelé pendant cette lecture. Le cube source ne disposant pas de COPY_SRC,
une texture de diagnostic séparée reçoit chaque face/mip par identity load/store.
Les horloges globales, eau et nuages sont vérifiées identiques avant/après.
La capture PNG et les métadonnées de frame précèdent la lecture diagnostique.

La tentative v9 échoue au démarrage du processus GPU Chrome dans le sandbox
(refus d'accès), avant acquisition. La tentative v10 avec accès GPU échoue
sur une hypothèse erronée de huit mips. `Environment.js` fixe en réalité six
niveaux de rugosité, 128 à 4 texels ; la chaîne source-ciel possède huit niveaux.
Cette hypothèse est corrigée dans l'overlay et le driver, sans modifier les
archives ni effacer les journaux des deux échecs.

L'acquisition v11 dans un profil Chrome headless neuf termine code 0,
archive exportée et driver intacts. Elle acquiert 37 fichiers de sortie GPU
(36 faces/mips et un bloc SH), avec scope de validation GPU sans erreur.
Métadonnées environnement SHA-256
`7defde1f3078c44659e30389de4d1d5d81759c7bba8c2e843bc8130840badf75`.
Les métadonnées de frame restent identiques à v8, SHA-256
`b69d7129cb8ba7a4ed2a17045255838bb31c90f5a01e575a29386bd9d6f6cb73`.
Les entrées, bases binaires et 372 blocs frame/settings/public sont revérifiés
identiques à ceux du replay CPU déjà exact. La source PNG v11 a été inspectée.
Entre les PNG source v8/v11, 76 pixels sur 2 073 600 diffèrent, différence
moyenne par canal 0,000012217 sur l'échelle 255 ; aucun rendu Forge comparé ici.

`environment-acquired-full-candidate-20261005-v2` remplace dans un nouveau
paquet le cube, les SH et l'éclairage par cette acquisition App, ainsi que les
blocs global/atmosphère par ceux du même run v11. Les valeurs half et SH sont
finies. Le cube est rangé face puis mip selon le contrat existant : 1 048 320
octets, SHA-256
`339f1b0c6c60e71cc55d3034746f56b773cc421880d259f36968fbb459d049e0`.
Le paquet de 274 fichiers/124 frames a pour manifeste SHA-256
`b688075e65db9f46b2fefab8a5cd982adead6496096ce0c26b764d75a4e2b8df`.
Il passe le véritable lecteur Forge et ses vérifications de séquence et
d'observateur, code 0. Le candidat v1 et le paquet Studio courant sont conservés.

L'écart avec le cube froid 120 frames est mesuré : 370 826 valeurs half sur
524 160 changent, différence absolue moyenne 0,001556 et maximum 2,594727.
Les SH ont une différence absolue moyenne 0,000325 et maximum 0,002949.
Preuve `lighting-differences.json` dans le candidat v2. Ces mesures prouvent
que les acquisitions d'environnement diffèrent ; elles ne constituent pas
un verdict de beauté, une comparaison Forge/source ou une parité visuelle.
Le GPU source est exécuté ; le GPU Forge et l'adoption Studio de ce paquet
restent à vérifier.

## Candidat du probe GPU sur le paquet acquis

Le mode paquet de `tidewater_cloud_weather_probe` vérifie désormais après
l'exécution que ready est acquis et que les compteurs completed_steps/submissions
correspondent exactement au nombre déclaré dans le paquet. Il écrit
`package-execution.json` avec le pin du manifeste et les compteurs. Le test de
remplacement de génération utilise également ce nombre, au lieu de 120 fixe.
Les autres parcours historiques du probe conservent leurs acquisitions dédiées.

L'instantané `cloud-acquired-observer-gpu-20261005-v1` contient 1 365 fichiers,
pins SHA-256 `5c1bc25421207f70b87a4635bbb440ee1ab4b419e7c406f96c00e708cccda01f`.
Il reprend le candidat observateur vérifié et uniquement le module du probe
modifié. Le contrôle WebGPU de cet exemple est lancé ; le build natif est
préparé dans `BuildAcquiredCloudGpuV1.py`, sans démarrage tant que le cache
natif est occupé par le build d'ombres v2.

`RunAcquiredCloudGpuV1.py` prévoit le chargement et le remplacement de
génération du paquet HDR v2, dix readbacks bornés, valeurs finies, compteurs
124/124 et comparaison exacte des sorties entre les deux générations. Le
runner exige un build terminé code 0 et ses pins avant exécution. Il n'a pas
encore exécuté le GPU et ne compare pas ces sorties aux textures GPU source.
L'acquisition des textures nuageuses source après le même historique App et
la comparaison correspondante restent à réaliser, ainsi que la vue Studio.

## Textures nuageuses finales de l'App source

`reference-cloud-output.js` ajoute une lecture des dix textures finales par
identity load/store vers des textures de diagnostic séparées. Les usages,
allocations et uniforms des textures source ne sont pas modifiés. Le binding
textureLoad non filtrant permet la lecture r32float sans exiger une option de
filtrage float32. Les horloges sont vérifiées identiques avant et après ; aucune
frame App supplémentaire n'est exécutée.

L'acquisition v12 termine code 0, archive et driver intacts. Elle acquiert
weather/bounds, shadow, radiance/metadata, deux paires color/meta et panorama
après l'historique réel de 124 frames. Dix layouts et leurs tailles sont
vérifiés ; les hashes correspondent, toutes les valeurs sont finies. Le view
final est 1 et le scope GPU ne rapporte aucune erreur. Les métadonnées de
frame restent exactement identiques à v11. Les 37 fichiers HDR/SH et les
valeurs d'éclairage sont également identiques à l'acquisition v11 utilisée
dans le paquet candidat v2.

Preuves : `reference-clock-acquisition-20261005-v12/capture/cloud-gpu/`,
`cloud-output-validation.json`, capture source et journaux. Métadonnées des
textures SHA-256
`4fa0927a9e8d80295792b55b0277f9567627de6eb9606acefcf6439a553caf35`.
Le script `compare-acquired-cloud-output.py` est préparé pour mesurer chaque
canal des dix sorties natives et vérifier le repeat/rechargement, avec pin
du paquet et compteurs 124/124 obligatoires. Aucune tolérance ni acceptation
visuelle n'est déduite de ses futures mesures. Les sorties natives correspondantes
restent à produire ; l'ancien paragraphe sur l'acquisition source en attente
est désormais remplacé par cette preuve, sans clôturer la comparaison.

L'inspection CIM retrouve le build d'ombres v2 actif : Cargo PID 37200,
rustc de l'exemple PID 3344 créé à 05:28:24 heure locale. Le build a donc
avancé au-delà de la bibliothèque moteur. Il n'est ni relancé ni arrêté
sur la seule absence de nouvelles lignes de journal. Son résultat final reste
en attente à cette étape.

## Pose initiale des casters : six captures natives acquises

Le build `initial-shadow-gpu-20261005-v2` termine code 0, sources intactes.
Exécutable SHA-256
`853570445283aacba226546b830c0f7cdae4d388ca8caec1ecb74e84df885ebf`.
Le runner v2 exécute six véritables readbacks natifs : arbre canopy à 60 m
et palmier à 110 m, chacun avec ancienne pose, pose acquise et répétition.
Chaque sortie contient trois couches de profondeur 2048², 50 331 648 octets.
Les valeurs sont finies et dans [0,1], avec couverture non vide par couche.

L'arbre passe de 43 446 à 43 470 texels couverts par couche ; le palmier de
18 644 à 18 892. Les trois couches changent avec la pose source acquise.
Les record/model/caméra restent identiques entre les cas et la répétition
acquise est identique bit à bit. `gpu-result.json` donne `PASS`, aucun check
échoué, fixture/source/exécutable intacts. Les PNG de couverture canopy/palms
ont été inspectés : blanc commun, rouge ancienne pose, vert pose acquise.

Cette preuve valide la déformation initiale effectivement utilisée par le
shader natif de profondeur sur ces deux casters isolés. Elle ne valide pas
l'animation live, les distances dans la scène complète, les receivers du sol
ou la parité visuelle. Le candidat v1 ayant échoué reste conservé.

Le cache natif étant libéré par ce build, `BuildAcquiredCloudGpuV1.py` est
maintenant lancé après son contrôle CIM. Le contrôle WebGPU du même probe
reste actif. Aucune sortie GPU nuageuse Forge ni comparaison source/native
n'est encore anticipée.

## Cadence live : diagnostic restant ouvert

L'inspection du code courant confirme trois voies de progression différentes :
le vent des matériaux avance à chaque Update avec le delta Bevy ; le vent des
casters n'avance que lorsque leur graph peut accepter une transaction, avec
l'intervalle écoulé plafonné ; les nuages utilisent l'intervalle depuis leur
dernière soumission, tandis que l'océan offre une transaction depuis Time<Real>.
Cette organisation peut conserver des cadences différentes malgré les bons
seeds initiaux. Aucun correctif de cadence commun n'est déclaré acquis ici.
Il faut conserver les offsets source (global 2 s, eau/nuages 2,066667 s) et
qualifier un pas accepté commun, pause/reprise et changements de génération.
Changer seulement une origine d'horloge ne reproduirait pas l'historique source.

## FFT et mousse après l'historique App complet

Le wrapper de frames observe désormais aussi `app.fft.params._last`, sans
appel à `_pack` ni mutation du bloc. L'overlay `reference-ocean-output.js`
lit les buffers h0/waveData/foam et les deux textures FFT existantes (quatre
cascades, neuf mips), puis retient les neuf kernels et le module de sampling.
Les textures possèdent déjà COPY_SRC dans le constructeur original ; aucun
usage ou allocation source n'est remplacé. Les horloges restent figées pendant
les readbacks. L'option `--all-effects` complète les captures HDR/nuages.

L'acquisition v13 termine code 0, archive et driver intacts. Elle produit
209 fichiers : 124 blocs OceanParams, 72 faces/mips FFT, trois buffers et dix
programmes. Les hashes, tailles et valeurs flottantes sont vérifiés ; le scope
GPU ne rapporte aucune erreur. Chaque temps effectif à l'offset 212 du bloc
224 octets correspond exactement au float32 du temps observé après sa frame.
Les autres paramètres ne sont pas supposés constants :
la comparaison exhaustive relève en fait zéro lane statique modifiée sur
les 124 blocs (seule la lane 53, temps, varie).

Premier temps float32 : 0,0166666675 ; dernier : 2,0666666031.
Choppiness/foamBias/foamGain/foamDecay/foamAdd restent respectivement
0,9/0,58/3/0,35/2,5 (arrondis ici pour lecture). Les métadonnées de l'historique
restent exactement identiques à v12 après retrait du nouveau champ d'observation
OceanParams. Le pin de `ocean-output.json` est
`1cd92d916f4574e57ab83fc9d1c52a721be0c53bc02f5a1c98aa09d38ee9de85`.
Preuve `reference-clock-acquisition-20261005-v13/ocean-output-validation.json`.

Ces données donnent l'oracle FFT/foam de l'App réel ; elles ne remplacent pas
encore le paquet océan Studio v1, dont le contrat et le bootstrap restent à
120 frames. Le support de la séquence acquise et le replay GPU natif/WebGPU
restent à implémenter et comparer. Ajouter 0,066667 s à une horloge live seule
ne valide ni les textures FFT ni l'accumulation de mousse correspondantes.

## Paquet océan acquis : transport et paramètres CPU exacts

Le lecteur accepte désormais une version 2 avec un compteur explicite de
1 à 256 frames et les blocs OceanParams/global de chaque frame. La version 1
conserve ses 120 frames. Dépendances, hashes, tailles, delta et compteurs
interne/externe sont vérifiés. `ocean-transport-v2-tests-20261005-v1` termine
code 0 : quatre tests passent, un test historique dépendant d'un paquet local
est ignoré. Module réel SHA-256
`8d8653039e618b4b56c581af33ef76b09f48d1c50f88b328719705e0796bd274`.

Le paquet `ocean-acquired-full-candidate-20261005-v1/package` contient
260 payloads : 124 paires acquises, neuf kernels, paramètres initiaux,
métadonnées et licence. Manifeste SHA-256
`b54f26065b796c6e4731a415048739263c3416ab8bdd3a364927c26961f1656e`.
Aucun résultat FFT/mousse précalculé n'est inclus. Les entrées viennent de
v13 et le bundle épingle les métadonnées de l'oracle océan. Le bloc initial
est le premier bloc acquis avec sa lane temporelle remise à zéro.

`ocean-acquired-package-tests-20261005-v1` compile les modules réels de
transport et `ocean_spectrum`, charge ce paquet épinglé, puis compare le
bloc initial et les 124 blocs complets au packer Rust. Tests et replay
terminent code 0 : les 125 blocs sont exacts octet par octet, sources intactes.
Quatre tests passent, un test historique est ignoré. Les warnings dead_code
du harness limité ne constituent pas une validation clippy workspace.
Ce contrôle CPU ne crée aucune texture GPU.

L'horloge réelle accepte maintenant l'origine du bootstrap acquis.
`ocean-acquired-clock-tests-20261005-v1` exécute dix tests std avec succès :
reprise à la frame 125, cadence, pause/drain/reprise et entrées invalides.
Module SHA-256
`f8f914c0ed4060884f79c91978d48cce00c73948b066603576775468da20c59e`.
Le renderer utilise désormais le compteur et les blocs acquis de chaque
étape statique ; ce changement n'est pas encore compilé ou exécuté sur GPU.
La cadence commune live reste ouverte.

Le candidat `ocean-acquired-renderer-20261005-v1` fige 1365 fichiers du
candidat cloud avec seulement les trois overlays océaniques. Manifeste
SHA-256 `5219ed2480e8ca7ec42a5a054dd9ab53c09846fd815a7058a0ab66e84499d5ba`.
Les builds cloud natif/WebGPU restent suivis par leurs handles existants.
L'adoption Studio, le replay GPU FFT/mousse et les captures de scène complète
restent à faire. Les anciens paquets et preuves sont préservés.

Le contrôle WebGPU du probe cloud acquis termine ensuite code 0, sources
intactes (`cloud-acquired-observer-gpu-20261005-v1/web-check-result.json`).
Il compile le target de contrôle ; il ne lie pas une application et n'exécute
pas le GPU. Le contrôle WebGPU du candidat océan figé est alors lancé dans
le cache libéré, avec le même target example pour conserver les fingerprints
des dépendances ; la bibliothèque moteur réelle contenant le renderer océan
est contrôlée. Son résultat reste en attente. Le build cloud natif continue
sur son handle existant, sans relance.

## Probe du renderer océan résident préparé

`tidewater_resident_ocean_probe` charge le paquet via `OceanPackage` et
`PreparedOcean`, enregistre le renderer réel, puis suspend sa continuation
live avant le bootstrap. Un système RenderGraph après OceanComputeSet copie
les deux textures résidentes, quatre cascades et neuf mips, dans des buffers
de lecture indépendants. Le probe vérifie la réception de 18 readbacks et
écrit 72 fichiers sans padding, ainsi que le pin, le compteur 124 et l'état
de pause. Aucun kernel FFT alternatif n'est utilisé dans ce probe.

Le candidat `ocean-acquired-resident-gpu-20261005-v1` fige 1366 fichiers.
Manifeste SHA-256
`a5fd28699322e025459df340a588000174b23eae783ff12f5c420a3b20d64b7e`.
Source du probe SHA-256
`7375785db5dff880ba58420404661a6df787c6935cc9cef47ddf878470a9d561`.
Les drivers de build/check/run sont préparés ; le runner prévoit deux
exécutions fraîches, des contrôles de tailles/valeurs finies et une répétition
bit à bit. Le contrôle renderer précédent reste actif dans le cache WebGPU,
et le build cloud natif reste actif dans le cache natif. Le nouveau probe
n'est donc pas encore compilé ou exécuté, et les drivers préparés ne valent
pas preuve d'exécution.

`compare-acquired-ocean-output.py` mesure les écarts absolus par canal
(moyenne, maximum, percentile 99), texels différents et hashes source/repeat
sur les 72 textures acquises v13. Son verdict est MEASURED, sans seuil
d'acceptation ou qualification visuelle implicite. Les buffers bruts
h0/waveData/foam ne font pas partie de cette lecture ; ils restent dans
l'oracle source. La scène complète et l'adoption Studio restent à qualifier.

Le contrôle WebGPU `ocean-acquired-renderer-20261005-v1` termine code 0,
sources intactes. Cela compile la bibliothèque du renderer avec les entrées
124 frames ; aucune application n'est liée et aucun GPU n'est exécuté.
Après libération du cache, `CheckResidentOceanProbeWebV1.py` lance le contrôle
de l'exemple dédié sur le snapshot suivant. Ce contrôle reste actif. Le build
cloud natif est confirmé actif par son handle et par Cargo PID 392/rustc
PID 20308, sans relance ni interruption des builds étrangers.

Le contrôle du probe résident v1 termine code 101 avec E0689 : la largeur
utilisée par div_ceil avait un type entier ambigu dans le code de lecture.
Le renderer de bibliothèque reste compilé ; aucun GPU n'a été exécuté par
ce probe échoué. Les sources v1 restent intactes et l'échec est conservé.
Le probe canonique précise maintenant `256_u32` pour la largeur du mip.
Le snapshot v2 fige 1366 fichiers, manifeste SHA-256
`2106750fd7acedcfa9682c9f19307dc11b7625ff22618a997e84cc55f8d6fd6a`,
probe SHA-256 `c896022211d4900cd461e1f3ca9a9f9d915acb9c0e5855be1c45ed2d0c0fe4fd`.
Son contrôle WebGPU est lancé après terminaison du v1 ; le résultat reste
en attente. Les drivers natif/GPU v2 visent cette génération corrigée.

Le contrôle WebGPU du probe v2 termine code 0, sources intactes. Il ne lie
pas d'application et n'exécute pas le GPU. La relecture du raccord optique
révèle ensuite que `completed_frame` ne renvoyait aucune frame acquise tant
qu'aucune transaction live n'avait été acquittée : `OpticalMaterial` ne pouvait
donc pas reprendre ce temps acquis sur une pause immédiatement après chargement.

L'horloge publie maintenant le dernier bootstrap seulement après encodage
de son étape FFT finale, avant `ready`. Le compteur, le temps exact, le delta,
l'absence de transaction pending et l'absence de publication antérieure sont
contrôlés. Le probe exige aussi que `completed_frame(world)` expose la frame
124 et son temps conforme au bloc source, puis les écrit dans sa receipt.
Le comparateur contrôle ce raccord indépendamment pour les deux exécutions.
Il s'agit d'une publication d'encodage ordonné, pas d'une fence GPU ; les
readbacks restent la preuve d'exécution GPU à obtenir.

`ocean-acquired-clock-tests-20261005-v2` compile et exécute onze tests std
code 0, sources intactes. Le nouveau cas vérifie absence de publication
anticipée, rejet des blocs divergents, pause, publication unique et reprise
frame 125. Module SHA-256
`32d2784bbce24b12532f444e4d5c609042a1ddb88bdd1a7db136af341b1942f4`.
Le candidat résident v3 fige 1366 fichiers, manifeste SHA-256
`fe4b94d157cb91162b896df87cf962c1ad4ed0792f54ae51c0481061c792c848`.
Son contrôle WebGPU est lancé dans le cache libéré par v2 ; le résultat reste
en attente. Le build cloud natif reste actif. La cadence commune live et la
scène complète ne sont pas qualifiées par ce test d'horloge.

Le contrôle WebGPU v3 termine code 0, sources intactes. Il compile le
renderer, la publication après encodage et le probe qui exige cette frame
pour les matériaux. Aucun readback GPU ni application liée n'est encore
prouvé par ce contrôle.

La préparation suivante complète les readbacks avec les trois buffers
résidents h0/waveData/foam. Le renderer expose leurs handles d'acquisition ;
leur COPY_SRC provient déjà de l'allocation ShaderBuffer de Bevy. Le probe
demande 21 readbacks (18 buffers de copies des mips, trois buffers résidents)
et écrit 75 sorties, dont 72 textures. La receipt distingue ces deux nombres,
contrôle le temps acquis publié et conserve la pause. Le runner v4 et le
comparateur couvrent les 75 sorties, valeurs finies, hashes et répétition.
Le scope initial excluant les buffers bruts est donc élargi dans cette
préparation, sans prétendre qu'ils ont déjà été lus sur GPU.

Le candidat résident v4 fige 1366 fichiers, manifeste SHA-256
`7e3de3ebc3864b3657ec33d234cd5c0b035512c984b3753a53937cd9a79f1301`.
Renderer SHA-256 `455aa9c59e4f80803cd9b7c9e1356be0b05bbd19ed6df3ac167e4bf25ea1ec41` ;
probe SHA-256 `948f5ffbd378d629deb631757766c87b9d0974d554d79f95b9cab74aca82aab6`.
Son contrôle WebGPU démarre après v3 ; résultat en attente. Le driver natif
v4 est prêt et attend le cache natif encore utilisé par le build cloud.
Formatage, syntaxe Python du comparateur et diff-check ciblé terminent code 0.

Le contrôle WebGPU du candidat résident v4 termine code 0, sources intactes.
Il couvre le renderer et le probe des 75 sorties avec horloge acquise publiée,
mais ne lie ni n'exécute d'application. Le contrôle Clippy WebGPU du même
example avec `-D warnings` est ensuite lancé dans le cache libéré ; verdict
en attente. Le build cloud natif reste confirmé actif par son handle.

## Nuages : premières sorties natives et erreur de collecte du rapport

Le build cloud natif v1 termine code 0, sources intactes. Exécutable SHA-256
`27762a9643f3d5371314759517a5d22f01ae28663ebfdda1319518d0e4ddf469`.
Le runner GPU commence le cas package : les dix fichiers de textures et
`execution.json` sont écrits, avec 124 completed_steps et 124 submissions.
Le probe termine ensuite code 101 en lisant Targets après `app.run()`.
Bevy 0.19.1 remplace alors App par App::empty et transmet l'ancienne App au
runner : cette lecture n'a donc plus de World de rendu. Le cas reload n'a
pas été lancé. L'exécution reste en échec malgré les readbacks écrits.

La mesure `failed-run-source-measurements.json` conserve explicitement
MEASURED_FAILED_RUN, process exit 101, run_qualified=false et reload=false.
Les dix layouts, tailles, valeurs finies et hashes source sont vérifiés.
Bounds est exact ; weather diffère sur cinq texels (un niveau du canal rouge).
Shadow a une erreur absolue moyenne de 3,06047e-7. Les deux color histories
ont une erreur moyenne RGB maximale par canal d'environ 1,42492e-4.
Ces mesures localisent les différences sur les sorties produites avant le
panic ; elles ne remplacent ni un runner réussi, ni une acceptation visuelle.

Les probes cloud et océan capturent désormais leurs résultats avant la
sortie, dans des états Arc conservés hors du World consommé. Cloud conserve
les handles/counters de la génération réellement active, y compris reload.
Océan capture l'API completed_frame dans le World actif et ne marque finished
qu'après écriture des 75 sorties. Les compteurs sont vérifiés après run sans
lire App::empty. Ce correctif n'altère pas les kernels de rendu.

Le build océan v4 contenant ce défaut confirmé est arrêté volontairement :
seuls le driver Python 25780 et ses descendants Cargo 11536/rustc 40000 ont
été identifiés puis ciblés ; session terminale code 1, sources préservées,
aucun GPU océan exécuté. `build-aborted.json` distingue cet arrêt d'un échec
de compilation. Les processus étrangers sont préservés.

Le snapshot commun `acquired-effect-probes-20261005-v5` fige 1366 fichiers,
manifeste SHA-256
`d304b0fc497d9e671af20b663ab6d23fceac76859055975dc6b649cfc68f2807`.
Les deux probes corrigés sont construits ensemble pour partager une seule
compilation moteur dans ce snapshot. Builds natif et contrôle WebGPU lancés,
verdicts en attente. Les runners séparés cloud et océan v5 sont préparés.

Clippy WebGPU v4 avec -D warnings termine code 101 avant le renderer :
17 diagnostics dans forge_assets/source_fragment.rs et tidewater_pier_motion.rs
(littéraux, patterns, comparaisons exactes, signatures et structure booléenne).
Ces modules hérités du snapshot n'ont pas été modifiés par cette tranche.
Le renderer et le probe n'ont pas reçu de verdict Clippy ; ce contrôle n'est
pas décrit comme vert. Le formatage et diff-check des deux probes corrigés
terminent code 0. Parité visuelle, adoption Studio et cadence live restent ouvertes.

Le contrôle WebGPU v5 termine code 101, sources intactes, avec E0425 dans
run_package : la déclaration de l'Arc de capture avait été ajoutée au chemin
historique run, alors que sa lecture appartient au chemin paquet. Le bloc
est déplacé dans run_package, juste avant son appel run. Aucun kernel ni
code du renderer n'est modifié par ce déplacement.

Le build natif v5 devenu obsolète est arrêté après cet échec confirmé :
driver 5400 et descendants Cargo 17736/rustc 32048 identifiés par CIM,
session terminale code 1. `build-aborted.json` conserve la raison et les
identifiants ; les sources v5 restent intactes et aucun GPU v5 n'est exécuté.
Le snapshot v6 copie ces 1366 fichiers et ne remplace que le module du probe
cloud. Manifeste SHA-256
`b76f1ea429577dce81d8f22d2e9d6e9e0d1ad78a6fe4ca3271ac90873fe6bf09` ;
module corrigé SHA-256
`23ac7fb77ced671276d699fa3a071bc7b0d89e13319dc5ba5b0847b7e5a4016a`.
Contrôle WebGPU et construction native des deux exemples v6 lancés ; leurs
verdicts restent en attente. Le formatage et diff-check du correctif sont
code 0. Les mesures des sorties cloud v1 restent classées FAILED_RUN et
ne sont pas promues par ce correctif non encore exécuté.

Le contrôle WebGPU v6 termine code 0, sources intactes. Il contrôle les
deux probes corrigés, sans application liée ni exécution GPU. Le build natif
du même snapshot reste actif.

## Préparation de bibliothèque : acquisitions version 2 acceptées

Le script `prepare-building-library-site.py` refusait les versions 2 pour
environnement et océan, même lorsque leurs lecteurs Rust acceptaient les
paquets acquis. Il valide maintenant schema/commit, conserve le contrat v1
sans compteur à 120 frames et accepte v2 avec un compteur entier de 1 à 256.
Versions booléennes et compteurs booléens ne sont pas assimilés à des entiers.
Les pins, tailles et contrôles de confinement des chemins restent appliqués
à chaque fichier avant copie.

`acquired-library-export-tests-20261005-v1` utilise le script canonique copié
et épinglé : anciens paquets v1 acceptés, nouveaux v2 à 124 frames acceptés,
compteurs None/False/0/257 et versions True/3 rejetés. L'export réel du paquet
bâtiments 003 (821 payloads), de l'environnement acquis v2 (274) et de l'océan
acquis v2 (260) termine code 0. Les 1359 fichiers produits sont vérifiés,
manifeste et index inclus ; leurs hashes/tailles et les sources restent intacts.
Script SHA-256
`15b66a57d60c0179af3ca6d281ed2ebe363a59ce8dddc0d9eb3e6c7d4a549915` ;
index SHA-256
`e4c9b998bd84cb0c82495c76cb6b3b3db8c82bb512e541da88a533d5f87ca342`.

Cette preuve vérifie le transport de ces trois paquets et leurs liens dans
l'index de bibliothèque. Elle ne constitue pas une bibliothèque complète de
l'île : les autres paquets ne sont pas remplacés par ce site de test, qui
n'est pas déployé dans Studio. Rendu de scène, picking, cadence commune et
distribution complète restent ouverts. Les sources des builds v6 déjà figées
ne sont pas modifiées par cette correction du préparateur canonique.

## Delta live océan : vitesse des vagues distincte de l'intégration de mousse

La référence locale figée au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c` distingue les deux horloges :
App.js transmet le delta d'application dans `G.dt`, OceanFFT.update avance
son temps avec `dt * timeScale`, tandis que le kernel colonne utilise
`frame.dt` pour la décroissance et l'ajout de mousse. Multiplier également
le delta global changeait donc la mousse lors d'un réglage de vitesse.

`live_clock.rs` conserve maintenant le delta d'application borné à 0,1 s
dans `LiveStep.dt`, et applique time_scale seulement à l'avancement du temps
de simulation. La vitesse produit demandée de 1,5 reste conservée.
Le test des stalls vérifie séparément ces deux valeurs ; un nouveau test
vérifie les bits du delta pour les vitesses 0,75/1/1,25/1,5 et des deltas
1/60, 0,025 et 0,25 s (ce dernier borné à 0,1).

`ocean-acquired-clock-tests-20261005-v4/result.json` et `tests.log` prouvent
la compilation et l'exécution du module canonique copié : codes 0,
12 tests réussis, source inchangée, SHA-256
`1ffc96dc2f1c713338eccd6ad0407cb45092122840a924bdc188a089344dbb6f`.
Cette preuve CPU ne valide pas le rendu live. Le snapshot GPU v6, figé
avant cette correction, ne l'inclut pas ; son bootstrap acquis et ses
comparaisons en pause utilisent un delta source constant à 1/60.
La cadence commune de la scène et la vérification visuelle restent ouvertes.

## Bibliothèque complète du candidat : environnement et océan acquis raccordés

`studio-acquired-full-library-20261005-v1/site` reprend la bibliothèque
`studio-village-candidate-20261004-v1/library-stage`, après vérification de
chacun des fichiers contre son reçu épinglé. Seuls environment_sha256 et
ocean_sha256 changent dans l'index ; les 16 autres champs restent identiques,
notamment terrain, plantes, bâtiments, poissons/stand, ponton, accessoires
de village, éclairage et programmes. Les 2083 fichiers antérieurs hors index
restent identiques. Les dossiers des anciennes versions sont conservés dans
cette copie isolée ; aucune suppression ni modification du candidat parent.

Les nouveaux liens désignent les paquets acquis b688075e… (environnement)
et b54f2606… (océan), chacun version 2 à 124 frames. Le contrôle indépendant
`VerifyAcquiredFullLibraryV1.py` relit les 2620 fichiers du site et vérifie
les 534 payloads des deux nouveaux paquets contre leurs manifestes : PASS,
code 0. Index SHA-256
`ca5dd5616d1140ea42974d178675f3abd4a3db45894fddbbf213f9e87c84b23e` ;
reçu de staging SHA-256
`ea06d1a08bd37a4d6d465ab5dc292b6779de5ec8f7488f6f8db9533d38777868`.

Cette copie prépare le prochain essai de scène complète. Elle n'est pas
déployée dans Studio et ne prouve ni le chargement moteur complet, ni le
rendu, ni la sélection 3D des plantes, ni la distribution portable finale.

## Exécution GPU native v6 : nuages et océan, puis comparaison source

Le build natif commun v6 termine code 0, sources inchangées. Binaires copiés
dans le snapshot : cloud SHA-256
`9147863baedd2e719111356aa471e4781a8bd501482ccbcd214354251b1761c0` ;
océan SHA-256
`16e93fd7a583229c9e858186190c5b9f7455e746c356dc40e20fe930064b68fb`.
Les deux runners terminent code 0 avec exécution GPU réelle. Le défaut de
lecture du World après App.run est désormais corrigé sur ces deux chemins.

Cloud : package et package-reload produisent les dix textures après
124 frames ; les sorties après reload sont toutes exactement identiques.
`cloud-gpu-result.json` est PASS. La comparaison avec les textures originales
v12 termine code 0, statut MEASURED : bounds exact, weather cinq texels
différents, erreur absolue moyenne shadow 3,06047e-7, et erreur moyenne
maximale par canal des deux historiques couleur 1,42492e-4. La répétition
est exacte, la comparaison source ne l'est pas. Les mesures de v1 restent
classées FAILED_RUN ; ce succès v6 possède ses propres reçus.

Océan : acquired et acquired-repeat produisent chacun les 72 couches/mips
de textures et trois buffers bruts, soit 75 sorties. Compteur réel 124,
horloge publiée time=2,066666666666664, dt float32=1/60, simulation live en
pause ; les deux répétitions sont exactement identiques. Le runner termine
PASS. `ocean-source-comparison.json` mesure les sorties contre v13 :
17 fichiers sur 75 exactement identiques ; erreur absolue moyenne maximale
par canal des textures 7,62939453125e-6 (displacement-c0-m6).
Pour h0, erreur moyenne maximale par canal 4,49695e-11 ; pour waveData,
3,34496e-7. Le buffer foam est identique et nul dans cette acquisition :
ce résultat ne couvre pas une scène avec génération effective de mousse.

Ces contrôles vérifient l'exécution et la répétabilité des calculs acquis,
ainsi que leurs écarts numériques source. Aucun seuil d'acceptation de
parité n'est déduit de ces chiffres. Les récepteurs, le compositing de la
scène, le mouvement live, la carte complète dans Studio et sa comparaison
visuelle restent à vérifier séparément.

## Prochain candidat Studio complet : construction lancée

`studio-acquired-full-candidate-20261005-v1` reprend les 1366 sources figées
du snapshot GPU v6 et remplace uniquement le module live_clock et le
préparateur de bibliothèque par les versions testées ci-dessus. Manifeste
source SHA-256
`e8cff27a548508fddeba5ecc48b5ffe905093ec01ba5f20bd612f4b18f4b65eb`.
La construction release du frontend forge_studio, cible WASM et feature web,
est lancée avec -j1 dans le cache Studio contrôlé. Son ancien WASM brut
est copié avant réutilisation du cache. Le processus Cargo descendant du
driver est confirmé actif ; aucun verdict de compilation n'est encore acquis.

`PrepareAcquiredStudioFrontendV1.py` est préparé et son AST Python vérifié.
Il exige d'abord un build réussi avec sources intactes, puis générera les
bindings, copiera la bibliothèque complète vérifiée et construira un worker
navigation depuis ce même snapshot. Il n'a pas encore été exécuté et aucun
frontend prêt, lancement Studio ou résultat visuel n'est annoncé à ce stade.

La préparation du frontend produit aussi le manifeste source au format
attendu par Build-StudioCandidate.ps1. Le driver natif
`BuildAcquiredStudioNativeV1.py` est préparé (syntaxe Python contrôlée) :
validation canonique des sources figées, frontend sans kit séparé pour ce
contrôle, worker construit depuis le snapshot, puis construction Tauri avec
frontend complet et copie vérifiée des fichiers web à côté de l'exécutable.
Il refuse de commencer sans frontend prêt ; il n'est pas encore exécuté.
Le build WASM reste actif et compile forge_engine au dernier relevé.

## Bibliothèque corrigée : derniers contenus plage et poissons préservés

Le contrôle préparatoire de l'inventaire révèle que la copie bibliothèque v1
partait du candidat village du 4 octobre, antérieur aux ajouts beach_props,
fish_props et game_fish. Elle ne doit donc pas servir au prochain essai complet.
Elle reste conservée comme preuve de son périmètre réel, sans être promue.

`studio-acquired-full-library-20261005-v2` repart du frontend complet figé
`studio-clock-observation-20261005-v3`, vérifie ses pins puis conserve les
3008 fichiers de bibliothèque antérieurs hors index. Les 19 champs d'index
autres qu'environnement/océan sont identiques, y compris ces trois ajouts.
La relecture indépendante des 3545 fichiers et des 534 nouveaux payloads
environnement/océan termine PASS, code 0. Index SHA-256
`5d28bcf7783b493113668550576e237f91a7c6b53ad352b86842fa8ac2580761` ;
reçu de staging SHA-256
`27399faad94856365bef0b7d67051a832b6df993e3379336910ef195edc31a6b`.

Le préparateur indépendant d'inventaire source est exécuté sur cette copie
avec le document authoring figé : 181 objets, 23 placements acceptés et
158 objets hérités. Entrées et pins des ressources d'objets sont identiques
à l'inventaire précédent ; seul le pin de l'index change. Inventaire SHA-256
`06b2859e46225c065a839e33915a927b7df3315a53166cb449cc5aa0d98d736a`.
Le driver de préparation du frontend cible désormais cette bibliothèque v2.
Les lecteurs des trois familles supplémentaires sont présents dans le
snapshot source du build Studio en cours. Aucun lancement GPU de scène
complète ni résultat visuel n'est encore obtenu avec ce candidat.

Le driver `CaptureAcquiredStudioV1.py` est préparé pour ce candidat et son
inventaire v2. Il exige le build natif réussi, le frontend externe vérifié,
les pins exacts de l'inventaire et du document authoring. Il copie et épingle
les helpers de capture avant de lancer leur protocole : projet cloné,
observations avant/après maintien de comparaison, capture Village et
fermeture du processus/listener appartenant à l'essai. Syntaxe Python vérifiée ;
driver pas encore exécuté. La construction WASM continue sur le même handle.

La construction WASM complète termine maintenant code 0, sources figées
inchangées. Le frontend brut est copié hors du cache : 126104489 octets,
SHA-256 `ee7b3f16df94b9976350c10efda325f291893bb47f3b582a2a1076a7d5d297dc`.
`PrepareAcquiredStudioFrontendV1.py` est lancé ensuite pour les bindings,
la bibliothèque v2 et le worker navigation. Son résultat reste en attente.
Le succès du build ne constitue pas un lancement navigateur ou un verdict
sur le rendu de l'île.

La préparation du frontend termine code 0 : bindings wasm-bindgen 0.2.127,
bibliothèque v2 vérifiée copiée et worker navigation effectivement reconstruit
depuis le snapshot (cargo release code 0). Le manifeste source au format du
constructeur est SHA-256
`ce643a563cdde51aeca4e9c75d11286b194d0a2529f0110e3e175078dd9852e2`.
Le constructeur canonique en ValidateOnly rend INPUTS_VALID, 3558 fichiers,
frontend authoring, sources figées et artefacts worker vérifiés.
La construction native du candidat est ensuite lancée par
`BuildAcquiredStudioNativeV1.py`. Aucun verdict natif, lancement ou capture
de scène n'est encore acquis pour cette version.

## Candidat complet v1 : build réussi, chargement océan refusé

Le build natif termine code 0, sources et frontend inchangés. Exécutable
SHA-256 `bd918e6b31aaf72de5fa759fd39ee5c43458797b91b03c6ad373577170728f85`.
Les 3558 fichiers frontend sont copiés à côté de l'exécutable et vérifiés.
Le lancement isolé par `CaptureAcquiredStudioV1.py` termine cependant code 1 :
Studio refuse le manifeste océan v2 de 38515 octets, car le transport de cette
famille utilise encore le plafond v1 de 8192 octets. La bibliothèque n'est
pas prête ; aucune capture de scène ni comparaison visuelle n'est qualifiée.
Le listener appartenant à l'essai est fermé. Le candidat, les helpers et
l'inventaire restent inchangés, selon `capture-result.json`.

Le transport Studio de l'océan accepte désormais au plus 256 KiB, comme le
lecteur `OceanPackage`. Les vérifications SHA-256, du schéma, du commit, de
l'inventaire exact et des payloads restent actives ; le lecteur conserve sa
limite spécifique de 8 KiB pour les manifestes v1. `git diff --check` du fichier
modifié termine code 0.

`studio-acquired-full-candidate-20261005-v2` fige le même snapshot de 1366
fichiers avec cette correction supplémentaire. Son build WASM est lancé et
confirmé actif sur le handle 20859. Le candidat v1 et son échec restent
conservés. Le nouveau build et le nouveau lancement n'ont pas encore de verdict.

Le contrôle préparatoire `AuditAcquiredLibraryManifestLimitsV2.py` relit les
limites des transports directs du snapshot v2 et les manifestes présents
dans la bibliothèque v2 : 16 contrôles de taille et de pin SHA-256 passent,
dont environnement (39911 octets) et océan (38515 octets), chacun borné à
262144 octets. Reçu local `manifest-limit-audit.json`, index SHA-256
`5d28bcf7783b493113668550576e237f91a7c6b53ad352b86842fa8ac2580761`,
source du chargeur SHA-256
`a335b7051d6a05090e863be233670a100852d66f4f6b005e0f0ff375b844a647`.
Ce contrôle ne couvre pas les autres chemins de chargement, la validation
interne des lecteurs, les payloads ni le rendu. Le build WASM v2 continue
sur le même handle ; son journal confirme la compilation des crates Forge.

Le build WASM v2 termine code 0 sur le handle 20859, sources figées inchangées.
Le frontend brut est conservé hors du cache : 126104515 octets, SHA-256
`5220a070e5e52333c05f9e2ee25b7513773b69269ead7033ae1bc4541787658f`.
`PrepareAcquiredStudioFrontendV2.py` est ensuite lancé sur le handle 28480 :
bindings, bibliothèque v2 vérifiée et worker reconstruit depuis ce snapshot.
La préparation reste en cours ; aucun lancement ni verdict de rendu v2 n'est
encore acquis.

La préparation frontend v2 termine code 0 sur le handle 28480 : bindings
wasm-bindgen 0.2.127, bibliothèque v2 vérifiée et worker navigation reconstruit
depuis les sources figées. Reçu du build WASM SHA-256
`ffe1671a2914ed412b145ded1448f20800d9975b472ffb3d1b7bb8e2c2c5f69b` ;
manifeste source du constructeur SHA-256
`f8efcac93d9bbe71d9cabed8a166c9357b08a437a625c579d761a3098d5a2936`.
`BuildAcquiredStudioNativeV2.py` est ensuite lancé sur le handle 11947 pour
le contrôle canonique et la construction native. Aucun verdict natif ou
rendu de scène v2 n'est encore acquis.

Le contrôle canonique du candidat natif v2 rend `INPUTS_VALID`, 3558 fichiers,
entrée `index.html?authoring=1`, fenêtre 1920×1080. La construction native
Tauri est effectivement lancée ; elle reste active sur le handle 11947.
Ce verdict couvre les entrées du constructeur, pas l'exécution du binaire
ni le chargement GPU de la scène.

Le build natif v2 termine code 0 sur le handle 11947. Sources et frontend
restent inchangés ; les fichiers web externes sont copiés et vérifiés.
Exécutable SHA-256
`eb109b13a7bbca8b347245d4c27f33cb490cb7097ecc8e0827b91021a83f366b`.
`CaptureAcquiredStudioV2.py` est ensuite lancé sur le handle 82373, avec
projet cloné et inventaire épinglé. Son résultat reste en attente : le build
réussi ne prouve pas encore le chargement de l'île ni la parité visuelle.

## Candidat v2 : lancement et capture Village réussis

`CaptureAcquiredStudioV2.py` termine code 0 sur le handle 82373. Helpers,
exécutable et inventaire sont inchangés. Le protocole charge la bibliothèque
et l'île dans le projet cloné, observe les diagnostics avant/après maintien
et ferme son listener. `animation-clock-observation.json` a SHA-256
`c8e85184f6d37537e20c4b5ab4a0c812cc3316da642464a36423a994e33e62c2`.
L'analyse canonique termine code 0, statut `measured`, aucune défaillance :
toutes les horloges mesurées et les compteurs cloud restent stables entre
held et later. Cela ne constitue pas une fence GPU ni une qualification
temporelle.

La capture `clock-capture/village-held.png` mesure 1920×1080, SHA-256
`dea418ff4d61fe70cc296e1691c94d55c6c4eaea3ed5487c657dc799c323cfd0`.
L'inspection montre des ombres projetées de palmiers et bâtiments. Elle ne
qualifie pas la distance de tous les casters. Des accessoires de cour
visibles dans la référence restent absents de cette vue, notamment des
clôtures et des tonneaux. Leur chargement, leur placement et leur projection
doivent être diagnostiqués ; aucune cause n'est encore établie.

Les valeurs held confirment les cadences distinctes : vent 6,4448 s,
végétation 3,3 s/frame 138, océan 4,316666666666664 s, nuages
3,5833332538604736 s/index source 139, 140 soumissions et 140 encodages.
Le timeScale océan 1,5 doit rester distinct du delta global non mis à
l'échelle. Les différences ci-dessus dépassent les seuls décalages initiaux
de la référence ; le pas commun reste à implémenter.

`Compare-PairedCapture.ps1 -Preset Village` termine code 0 et produit
`village-comparison.json`, contre le PNG Village de l'acquisition v13
SHA-256 `37fe6bf1fc44ca55bb65b80c5e07d78d390e91fb252accccae394470ce25adc5`.
Échantillonnage tous les huit pixels : luminance moyenne de l'image source
109,2263/native 109,6805 ; erreur absolue moyenne de luminance 8,5536 et
RGB 9,1448 sur 255. Ces mesures sont exploratoires : les états temporels
diffèrent et aucun seuil d'acceptation n'est appliqué. Une moyenne proche
ne remplace pas les accessoires manquants ni la validation visuelle complète.

## Diagnostic des accessoires : projection CPU et visibilité à mesurer

Le diagnostic held du candidat v2 indique 254 parties opaques, contre 148
dans l'ancien candidat du 4 octobre. Le code du snapshot contient les
lecteurs et les placements communs des accessoires : 27 village_props,
43 beach_props et 78 fish_props dans l'inventaire indépendant. Le rendu
du village et de la plage passe par `stage_authored`, avec positions locales
et pivots normalisés ; le préparateur conserve un contrôle de restauration
des positions source à moins de 0,1 mm. Le nombre global de casters ne prouve
pas le dessin de chaque objet et ne suffit pas à expliquer leur absence
dans la capture.

`authoring_imported_shadows/prop_observation.rs` ajoute à l'observation
publique existante `source_prop_projection` : identités et positions
acceptées/locales/propagées, visibilité héritée et visibilité de vue des
parties, présence CPU du maillage et état du matériau. Cela n'écrit pas
les placements et n'annonce aucune exécution de draw GPU. Le diagnostic
couvre les résidents village/plage ; les poissons restent à observer
séparément si nécessaire.

Deux essais de compilation directe conservés dans
`prop-projection-observation-check-20261005-v1` et `v2` échouent avant de
valider le module : bibliothèque serde_json Windows sélectionnée en v1,
puis moteur non résolu par le harnais direct en v2. Ils ne constituent pas
des verdicts de compilation du frontend. Le contrôle passe donc par Cargo
sur le snapshot isolé `studio-acquired-full-candidate-20261005-v3`, 1367
fichiers, avec le nouveau module et son point d'appel. `cargo check` release,
cible WASM et feature web, est lancé sur le handle 13691 ; résultat en attente.
Le candidat v2, ses sources figées et sa capture restent préservés.

## Diagnostic accessoires : contrôle Cargo v3 réussi

Le handle 13691 termine code 0 : `cargo check` release WASM du snapshot v3
réussit, avec sources inchangées. Le nouveau diagnostic de projection et
visibilité des accessoires est donc vérifié par Cargo. Ce contrôle ne lie
pas une application et n'exécute pas le navigateur ; sa capture et le
diagnostic effectif des objets absents restent à faire. Aucune parité
visuelle supplémentaire n'est acquise par ce résultat.

## Candidat v3 : liaison WASM lancée pour la mesure des accessoires

`BuildPropsStudioV3.py` construit les 1367 fichiers déjà figés et contrôlés,
sans refaire le snapshot. La vérification préalable des SHA-256 et du résultat
Cargo réussi est obligatoire. Les journaux de liaison sont séparés des
journaux de check ; le précédent WASM du cache est préservé.

La session 93356 est en cours. Une inspection de processus confirme rustc
PID 27036 compilant `forge_core` dans le cache WASM prévu. Les pilotes
`PreparePropsStudioFrontendV3.py`, `BuildPropsStudioNativeV3.py` et
`CapturePropsStudioV3.py` sont préparés pour la suite séquentielle : ils
conservent la bibliothèque complète v2 et son inventaire indépendant, et
n'annoncent pas de parité sur un simple build. Ils ne sont pas encore exécutés.
Aucune mesure de projection v3 ni correction des accessoires absents n'est
acquise à ce stade. Le candidat v2 et sa capture restent préservés.

## Analyse des projections préparée, sans verdict de rendu

`scripts/tidewater/analyze-prop-projection.py` résume les familles village
et plage de la dernière observation : comptes cohérents, maillages présents,
visibilité héritée/de vue, écarts de position et matériaux absents/transparents.
Une absence de visibilité de vue peut être normale hors caméra ; aucun de
ces états CPU n'est converti en preuve de draw GPU.

L'analyse de la capture v2 est explicitement `failed_closed` : les deux
familles ne possèdent pas encore ces mesures. Trois contrôles synthétiques
passent (visibilité hors champ, rejet de comptes incohérents, signalement
d'écart de position), consignés dans `projection-analyzer-check.json` du
candidat v3. Ils ne sont pas des tests de la scène.

La session de liaison 93356 reste active lors du dernier poll. Les journaux
montrent désormais la compilation de `rerecast`, après les crates Core,
Gameplay et Project. Le prochain contrôle doit reprendre le même handle,
puis enchaîner préparation frontend/native/capture seulement après succès.

Le pilote de capture v3 exige désormais l'analyse de projection après une
observation réussie. L'analyseur est copié et épinglé parmi les outils du
candidat ; son résultat et son SHA sont consignés dans le reçu de capture.
L'absence des nouvelles mesures ne pourra donc pas être annoncée comme une
mesure réussie des accessoires. La capture n'a pas encore été exécutée.

Le dernier poll de session 93356 confirme encore la liaison active. Le
processus rustc 38752 compile `forge_engine`. Les volumes ont suffisamment
d'espace pour la préparation : environ 83,9 Go libres sur E et 2,04 To sur F.
Aucune suppression ni modification de source du snapshot pendant le build.

## Enchaînement séquentiel v3 armé sur le processus identifié

`RunPropsStudioV3Sequence.py` tourne sur la session 25994. Il a confirmé le
pilote Python PID 32436, sa commande `BuildPropsStudioV3.py` et sa date de
création exacte. Il attend la disparition de ce processus précis, puis exige
le reçu de build réussi et le SHA du WASM avant toute étape suivante. Il ne
relance pas le build et ne s'appuie pas sur un fichier seul pour déduire sa fin.

Les trois pilotes préparés sont épinglés dans `sequence-request.json` et
exécutés séquentiellement avec journaux distincts : frontend, natif, capture
et analyse CPU. Toute défaillance arrête la chaîne avec `failed_closed` sans
relance automatique. Le premier état observé est `waiting_for_identified_build`.
La session de build 93356 reste à observer également. Aucun déploiement dans
le launcher utilisateur et aucune publication ne sont effectués par cette chaîne.

## Candidat v3 : build WASM réussi, préparation démarrée

La session 93356 termine code 0 : sources figées inchangées, WASM lié
126154141 octets, SHA-256
`009ca7bdd749b3511d245a8b391c62f62838802c42a3d476c63c68c8219f086d`.
Ce build n'exécute pas le navigateur et ne qualifie pas les pixels.
La chaîne 25994 observe la fin du pilote et passe effectivement à
`PreparePropsStudioFrontendV3.py` à 09:14:07 UTC. La préparation, le build
natif et la capture ne sont pas encore validés.

## Contrôle des coordonnées des matériaux animés

Le shader village acquis calcule le vent de `fabric` et `nets` à partir des
positions reçues avant transformation du modèle. Les cinq autres branches
vertex inspectées ne déforment pas la position. Les paquets accessoires
localisent les positions avec un pivot, mais conservent les données de vent.
L'audit CPU `prop-wind-coordinate-audit-v2.json` mesure les offsets de phase
induits par ce pivot pour les parties ciblées, avec vérification des SHA des
payloads vdata et des repères identité. Par exemple, laundryLine 4 a un offset
spatial de phase -45,045 et un offset vertical de flutter 28,3133.
Cela signale un raccord de repère à vérifier pour la fidélité des animations ;
ni son effet GPU ni son rôle dans les objets absents ne sont encore démontrés.
Aucun vertex flag n'est présent dans les parties fabric de ces deux paquets.
Le premier audit vide utilisait par erreur le champ `material` (identités
material-0 à material-6) comme nom de cible : il est explicitement invalide,
et remplacé par l'audit v2 utilisant le champ `target`. Aucun verdict de parité.

## Frontend v3 préparé, construction native démarrée

La chaîne 25994 passe à `BuildPropsStudioNativeV3.py` à 09:17:44 UTC.
Le reçu frontend confirme 3558 fichiers, worker réellement reconstruit,
bindgen 0.2.127 et bibliothèque complète v2 conservée. Le WASM préparé
mesure 141408311 octets, SHA-256
`251f742295a73b551d3d2a28fd5b92fb23ae620880c95630c7b7ac23912bb1be`.
Manifest source SHA `23386621eba018f78af8456f86e3ea52704c000b8cbb33f9c83c1302c579e275`,
reçu build SHA `9a77f97e7748ce1eb3fb4e8283ad9868f05f3633499390346b4beec80480d635`.
La construction native démarre ; le candidat et la capture restent à vérifier.

L'audit supplémentaire `prop-wind-deformation-audit.json` utilise les payloads
pos, reference_position et vdata vérifiés par SHA. La formule CPU en double
précision applique les paramètres de vent réellement observés en v2 (speed 7,
direction mesurée), séparément aux deux temps observés scene_wind et wind_pose.
Sur 23 parties fabric/nets, 1110 vertices ont un poids de vent non nul. L'écart
maximal de déplacement local/source calculé est environ 0,1042 m.
Cela n'est ni une exécution WGSL ni une preuve des uniforms réellement utilisés
par ces draws GPU. Le raccord du repère spatial est à traiter pour la fidélité
animée ; la cause des accessoires absents reste distincte et non établie.

## Candidat natif v3 construit, capture effectivement démarrée

Le build natif termine code 0 en 10 min 19 s. Le reçu confirme sources figées
et frontend inchangés, ainsi que la vérification du frontend sidecar.
Exécutable `desktop/forge-studio-candidate.exe`, SHA-256
`bde0b524c03435d65a4aace2b5167ca571d026027422b77fb76959c4a4ba2e3f`.
La chaîne 25994 passe effectivement à `CapturePropsStudioV3.py` à 09:29:06 UTC.
La capture et l'analyse ne sont pas encore terminées ; le build natif seul
ne qualifie ni le chargement de cette version ni les objets visibles.

## Capture v3 terminée : familles présentes, absence à préciser

La chaîne 25994 termine code 0, `captured_and_measured`, à 09:30:29 UTC.
Capture et analyse de projection terminent code 0 : helpers, candidat et
inventaire inchangés. L'analyse est `measured`, aucune défaillance.
Village : 27 racines, 51 parties, 51 maillages CPU chargés, 51 visibilités
héritées actives, 27 parties avec visibilité de vue. Plage : 43 racines,
78 parties, 78 maillages chargés et visibilités héritées, zéro visibilité
de vue pour cette caméra village. Aucun écart de position accepté/propagé,
maillage manquant ou matériau absent/transparent n'est observé dans ces
familles. Les invisibilités de vue peuvent être normales hors caméra et
ne sont pas qualifiées comme défauts de culling par cette mesure.

L'image village v3 est inspectée avec la référence v13. Des clôtures et
éléments de linge sont effectivement visibles ; l'hypothèse générique
« accessoires village non chargés » est donc contredite par ces mesures.
Des caisses/tonneaux autour du stand et des petits éléments de la référence
restent visuellement absents, et les poissons/assiettes du stand diffèrent.
Il faut maintenant identifier leurs propriétaires exacts et vérifier la
couverture de l'extraction des objets source. Les 27 village_props et 43
beach_props ne couvrent pas automatiquement tous les décors visibles.
La mesure ne prouve aucun draw GPU individuel ni la parité de l'île.

Suite : `2026-10-05-debris-inventory.md`. Les groupes source de caisses
(X34,4/Z−114,4) et tonneaux (X47,6/Z−114,8) appartiennent à la couche
Debris distincte. Deux inventaires CPU indépendants sont identiques, avec
766 entrées de placement et 141 instances scannées. La conversion et le
rendu de cette couche entière restent à réaliser.
