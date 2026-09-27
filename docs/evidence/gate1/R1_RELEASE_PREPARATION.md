# Gate 1 — préparation du candidat release, 7 septembre 2026

## Actualisation 9 septembre — mesure du code courant après portée lointaine

Reconstruction `cargo build -p forge_runtime --release --locked -j2` réussie
en 7 min 45 s, cache/temp sur E:, offline, `RUSTFLAGS=-C debuginfo=0`, profils
Cargo inchangés. Session 55189 terminée avec code 0 ; aucune compilation parallèle.
Copie du binaire contrôlée par SHA-256 :
`4C4BB888936653294D420A86CDEBE7E69BC2AE0E86E7203B8D5E0EE43BFC25EC`.
Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-current-release-20260909`.
Ce build n'est pas une qualification sur révision Git propre.

Un seul parcours avec `scripts/Run-FoliageAtlasCandidate.ps1 -Mode measure`,
kit `r1-atlas-cropped-runtime-kit-20260909`, binaire et hash explicites.
Le fichier `walk/arguments.json` conserve les arguments réellement exécutés :
1800 ticks `1:0.22`, caméra joueur, tous les domaines demandés High, sans VSync,
eau claire avec reflets/réfraction, profil d'ombres candidat externe. Pas de
nuages expérimentaux dans ce parcours ; ce n'est pas le coût du menu combiné.
Fenêtre 1920×1080 ; simulation vivante, joueur au sol après freinage.

| Périmètre | Échantillons | FPS moyens | p95 ms | 1 % low FPS |
|---|---:|---:|---:|---:|
| Trajet entier | 2173 | 72,4741 | 21,3064 | 43,0097 |
| Tiers forestier, ticks 1201–1800 | 517 | 51,7233 | 22,2797 | 39,8967 |

Recalcul PowerShell indépendant depuis `samples_ms` et `poses[].replay_tick`,
percentile au rang plafond et moyenne des 1 % de frames les plus lentes.
Accord avec les trois métriques Rust globales à la précision affichée.
Rapport SHA-256 : `83DB1553636418DA7DCC492899EBDE728D2A94D37507FB950C0B5DDE54EEBB6D`.
Entrées épinglées vérifiées inchangées par le runner ; aucun événement ERROR.
Session 88130 sortie 0. PID 10764 terminé normalement, absence du PID et de
ses enfants directs vérifiée. Aucun navigateur ou nouvelle capture lancé.

**Verdict : seuils toujours non satisfaits**, même en release. Pas de paire
dev/release contrôlée ni de gain attribuable à une optimisation précise : les
anciens chiffres ne constituent pas le témoin de cette exécution. Un seul passage
ne qualifie ni la variance ni le GPU cible. Cette mesure actualise le déficit
forestier ; elle ne justifie pas une autre micro-optimisation d'eau et ne démontre
pas la stabilité artistique. Priorité inchangée : coût de représentation/rendu
de forêt, puis qualification finale des profils et parcours. Gate 1 ouverte.

## Actualisation 9 septembre — coût du candidat optique courant

Le release précédent ne contenait pas les derniers reflets transmissifs.
`Build-Gate1OpticalRelease-20260909.ps1` a reconstruit le Runtime actuel en
7 min 44 s, offline/locked, deux jobs, profils Cargo inchangés, cache/temp sur E:.
Empreintes sources constantes pendant la compilation. EXE :
`2C507A416D46FD39835715DB18022CC845CAD3E190FFAC40353CD92CBFB15BCA`.

Quatre parcours natifs vivants, ordre sans/avec/avec/sans reflets locaux,
**même EXE et milieu clair transmissif dans les quatre cas**. Aucun témoin opaque.
Kit courant 1662 arbres, trajet `1800:1:0.22`, 1920 × 1080, sans VSync demandée,
ombres candidates 2048/trois cascades/128 m, autres demandes High constantes.
Pas de compilation ni capture concurrente. Budget de reflets avec option :
48 pas/96 m, projection CPU concordante ; absent dans les deux témoins.

| Passage | Reflets locaux | FPS trajet | p95 ms | 1 % low FPS | FPS dernier tiers forestier |
| --- | --- | ---: | ---: | ---: | ---: |
| A1 | sans | 64,45 | 23,97 | 37,36 | 45,51 |
| B1 | avec | 64,74 | 24,41 | 38,90 | 44,85 |
| B2 | avec | 64,11 | 24,37 | 38,74 | 44,52 |
| A2 | sans | 65,04 | 24,11 | 39,68 | 45,62 |

Recalcul indépendant sur tous les échantillons et les tiers fixes : accord
avec Rust à 1e-9. 1344 ticks communs ; écarts de position, bras de caméra,
yaw et pitch nuls à ces ticks. Sources et kit épinglés inchangés, même projection
d'ombres, parcours complets et joueurs au sol après freinage. Quatre sorties 0,
aucun événement ERROR ; PIDs 32412, 384, 18364 et 33368 terminés, absence contrôlée.

Décision : sur **ce trajet**, pas de coût majeur des reflets qui expliquerait
le déficit forestier. Une petite différence existe dans le dernier tiers, mais
elle ne justifie pas une nouvelle série de micro-optimisations de l'eau. Ne pas
extrapoler à une vue remplie d'eau, aux objets hors écran ou au GPU cible. La
priorité de performance reste la charge de forêt ; p95 ≤16,7 ms et 1 % low ≥45 FPS
toujours non atteints. Aucune stabilité visuelle en mouvement démontrée par les
seuls échantillons/poses, aucune acceptation artistique ni qualification Studio.
Ne pas attribuer l'écart avec l'ancien release à une optimisation précise :
seule la comparaison avec/sans de ce binaire est contrôlée ici.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-optical-20260909/`.
Scripts externes `Measure-Gate1OpticalRelease-20260909.ps1` (syntaxe PASS) et
`Compare-Gate1OpticalRelease-20260909.py` réellement exécutés. Rapports
`walk-0.report.json` à `walk-3.report.json`, logs, source-pins, measurement-pins
et `comparison.json` conservés ; SHA-256 de la comparaison :
`594AE4087A7F9E1F572ADEF9A5279F656F4B5BABA4EE8200087AB322CEDDE345`.
Pas de changement du code produit, pas de nouvelle suite de tests répétée,
aucun navigateur ouvert, aucune publication. Gate 1 ouverte.

## Actualisation 9 septembre — comparaison courante terminée

Les résultats du 7 septembre ci-dessous concernent 650 arbres et ne qualifient
pas le kit courant de 1662 arbres. Après retrait de l'essai alpha nul sans gain,
reconstruction release lancée sur les sources courantes épinglées :
`Build-Gate1Release-20260909.ps1`, racine externe
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-current-20260909/`.
Deux jobs, cache/temp sur E:, aucun changement des profils Cargo ou du renderer.
Le témoin dev restauré est conservé dans `dev.exe`. Build PASS en 7 min 55 s,
empreintes sources inchangées. EXE release
`37A54163C5E9AE323DBF8070345A3789FC07D9295920FDE62C60988B653112CE`.
Ne pas attribuer les anciens résultats release à ce nouveau candidat.

`Run-Gate1Release-20260909.ps1`, syntaxe PowerShell PASS : première invocation
capture refusée avant lancement (tableau singleton PowerShell corrigé, journal
conservé), deuxième capture terminée, sortie 0, PID 26972 fermé. PNG 1920 × 1080
`Capture-0-release.png` inspecté : forêt 1662 arbres, terrain/pont/rivière présents,
ciel uni et berges encore artificiels ; aucune acceptation artistique déduite.
Comparaison dev/release/release/dev terminée sur le trajet vivant
`1800:1:0.22`, kit courant et ombres candidates 2048/trois cascades constants.
Rapports par tiers nécessaires : une moyenne de vallée ne qualifie pas la forêt.
Build terminé, pas de compilation concurrente pendant la mesure. Aucun seuil de
Gate 1 fermé par la capture ou le compteur instantané affiché.

### Résultats courants, non substituables aux résultats historiques

| Passage | FPS parcours | p95 ms | 1 % low FPS | FPS dernier tiers | p95 dernier tiers ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| dev A | 54,60 | 27,11 | 34,39 | 39,71 | 28,46 |
| release B | 62,58 | 25,15 | 36,01 | 42,50 | 27,01 |
| release B | 62,68 | 25,21 | 36,86 | 42,70 | 26,59 |
| dev A | 54,86 | 27,38 | 33,84 | 39,52 | 28,48 |

Environ +14 % sur le parcours grâce au profil de compilation déjà défini, sans
nouveau code d'optimisation. La forêt gagne environ 7–8 % seulement et reste
à 42,5–42,7 FPS ; son 1 % low release vaut 35,00/36,40 FPS. Le p95 global
reste supérieur à 16,7 ms et le 1 % low inférieur à 45 FPS. **Pas de qualification
de fluidité ni de passage de gate**, malgré la moyenne globale supérieure à 60.
GPU RX 7800 XT de développement, sept domaines High demandés mais ombres candidates
2048/3 explicitement substituées : pas le preset High canonique ni le GPU cible.

Recalcul indépendant depuis tous les échantillons et les trois tiers fixes :
accord avec les statistiques Rust à 1e-9. 1177 ticks communs aux quatre parcours,
écart de position maximal 8,4 mm, caméra yaw/pitch identiques, écart de bras
caméra inférieur à 5,1 mm. Projections d'ombres identiques, 1920 × 1080 et entrées
épinglées constantes ; les animations vivantes ne sont pas figées pixel à pixel.

Les quatre sorties sont 0, aucun événement ERROR. PIDs 30948, 32556, 11132,
30968 terminés ; capture PID 26972 également terminée. Aucun processus du
répertoire d'essai au contrôle final. Aucun navigateur ouvert, Studio non reconstruit.
Preuves : `measure.log`, `Measure-*.report.json`, `Measure-v2-pins.json`,
`comparison.json`, journaux, capture et pins de build dans la racine courante ;
analyseur externe `Compare-Gate1Release-20260909.py`.

- SHA-256 comparaison : `2622F7611D6899F8CD7DDF0988B4BB2AF7574BBDCFA0A47F7EED560BDBEF4414`.
- SHA-256 capture : `D27FA31F7C40FDCCCEFB0644AAECC573B15C0C10DD5C21D33EE16BAF50C30B91`.
- SHA-256 témoin dev : `EBEEC85E6639590318383E259DD6D11995A21C92FFFD85A4D0AE53381CC6F530`.

Décision : utiliser ce release courant comme référence pour les prochains coûts
de rendu, ne plus conclure sur le seul dev opt-level 1 ni répéter cette paire
inchangée. Le déficit en forêt reste réel en release, donc n'est pas résolu par
la compilation ; art, GPU cible et parité Studio restent à qualifier.

## Résultats historiques du 7 septembre

Statut : **compilation release PASS, smoke natif PASS ; trois mesures sans capture valides, seuils de frame locaux PASS sur RX 7800 XT uniquement**.

Les mesures existantes sont explicitement des builds dev : code Forge opt-level 1,
dépendances opt-level 3. Elles ne qualifient pas le profil release défini dans
Cargo.toml (opt-level 3, LTO thin, codegen-units 1). Aucun exécutable release n'était
présent dans le cache courant au lancement. Ne pas confondre cette optimisation
de compilation avec un gain dû aux derniers dérivés d'arbres.

Commande réellement lancée : `cargo build --locked --offline --release -p forge_runtime -j 4`.
Pas de changement des profils Cargo, de dépendances ou du code source.
Cache `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906`, temporaires
`terrain-water-temp-20260906`, incremental désactivé, RUSTFLAGS `-C debuginfo=0`.
E: disposait d'environ 197,6 Go libres avant lancement ; aucune suppression.

Lanceur : `E:/OpenRPG_Forge/.gate1-evidence-runtime/Build-Gate1Release-20260907.ps1`.
Sorties : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-candidate-20260907/`.
`source-pins.json` épingle les Rust/WGSL/JSON et manifestes Cargo sélectionnés
par le script ; `git-status.txt` et `rustc.txt` situent le worktree modifié.
Ce n'est pas un snapshot propre ou une qualification de distribution.
Après succès seulement, le lanceur vérifie les hashes et copie le binaire en preuve.
`build.log` conserve la compilation. Ne pas lancer un second build si l'observation
de ce processus expire ; vérifier le handle ou l'état des processus existants.

Une fois terminé : smoke du kit/candidat sur le nouveau binaire, puis mesures
sans capture, entrées et résolution contrôlées. Aucune extrapolation automatique
aux GPU cibles RTX 3060/RX 6600 ni au Web/Tauri. Les critères artistiques et humains
restent ouverts. Les mesures dev antérieures et leurs échecs restent conservés.
Gate 1 ouverte, CI distante NOT RUN, aucun passage de gate ou publication Git.

Protocole préparé pendant la compilation :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/Run-Gate1Release-20260907.ps1`, modes
`capture` (420 frames) et `measure` (trois fois 6 600 frames, dont 600 de chauffe).
Syntaxe PowerShell contrôlée PASS ; script désormais exécuté pour le smoke. Il épingle
le binaire du build terminé et le candidat conifère rev.4, contrôle les sources,
1080p initial/final et refuse plus de deux événements de resize (initialisation
Windows observée auparavant). Il sépare seuils locaux et `gate_qualified=false`.
Le contrôle précédent seulement initial/final pouvait manquer un redimensionnement
intermédiaire restauré : ce nouveau garde-fou a accepté le smoke sans resize supplémentaire.

## Résultats release acquis

Compilation terminée avec code 0 en 12 min 50 s ; source-pins inchangés.
Binaire SHA-256 `d0f953620505ce8b19ce36fad749bbffb2564fd7ea58886a4566859117afe6c2`.
Le premier smoke (`capture/`) a échoué : fermeture à la frame 420 avant le retour
GPU de la capture. Log conservé, aucun PNG ou rapport de métriques qualifié.
Le protocole de capture a donc été allongé : chauffe 600, capture demandée 1200,
sortie 2400. Le moteur et le protocole de mesure sans capture restent inchangés.

Deuxième smoke (`capture-warmed/`) : code 0, 1799 échantillons, 1920×1080 initial/final,
20 instances de kit, 36 colliders, 650 arbres, hashes de documents et entrées conservés.
Moyenne 69,846 FPS, p95 15,754 ms, 1 % low 56,463 FPS, aucun hitch >33,3 ms.
Ces chiffres incluent une capture : ils ne remplacent pas les trois mesures sans capture.
PNG inspecté : terrain, pont et forêt présents ; ciel uni, sol orange et conifères
sombres encore visibles. Aucune acceptation artistique déduite.
GPU RX 7800 XT ; `full_preset_applied=false`, `gate_qualified=false`.

Capture SHA-256 `3d5820274f654d7105e561c68d46bd777e6324c840fd175f62a86e16c34d92ce`.
116/116 pipelines prêts ; capture émise à 1200, callback reçu à 1203.

## Trois mesures sans capture terminées

Mode `measure` : trois runs sans capture, chauffe 600, sortie 6600,
5999 échantillons acquis chacun, codes de sortie 0. Fichiers `measure/run-1.json`
à `run-3.json`, logs associés, `pins.json` et `summary.json` dans le dossier de preuve.

| Run | FPS moyens | p95 ms | 1 % low FPS | Frames >33,3 ms |
|---|---:|---:|---:|---:|
| 1 | 69,711 | 15,902 | 57,076 | 0 |
| 2 | 70,620 | 15,335 | 58,276 | 0 |
| 3 | 71,055 | 15,256 | 58,607 | 0 |

Chaque run : résolution initiale/finale 1920×1080, deux événements resize
d'initialisation seulement, qualité demandée High, adaptation désactivée, 650 arbres,
20 instances du kit et 36 colliders ; entrées épinglées inchangées. Aucun ERROR/panic
dans ces logs. Les warnings de profils partiels et de position WGSL non invariant
restent visibles. Aucun build/capture concurrent lancé par cette tâche.

Décision : seuils de cadence atteints de façon répétée sur la RX 7800 XT locale.
Ne plus utiliser les seules mesures dev pour justifier des micro-optimisations CPU.
Pas de gain causal attribué au dérivé de conifère : aucune comparaison release A/B.
Pas de qualification RTX 3060/RX 6600, Web/Tauri, transitions, toutes météos/caméras,
ni preset complet (`full_preset_applied=false`). Gate 1 reste ouverte.
