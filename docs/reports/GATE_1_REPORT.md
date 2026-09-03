# Rapport Gate 1 — Visual Valley

Date de consolidation : **2026-09-03**

Verdict : **POURSUIVRE GATE 1 HORS AUDITS DE CADENCE — PERFORMANCE DIFFÉRÉE ADR-0010, GATE 1 OUVERTE**

Gate suivante : **Gate 2 interdite sans passage de Gate 1 ou nouvelle dérogation explicite acceptée**

## Résumé de décision

Le propriétaire accepte explicitement le report des critères stricts de performance selon
[ADR-0010](../ADR/ADR-0010-gate1-strict-performance-deferral.md). Les quatre conditions
FPS/p95/1 % low/stutter et le benchmark cible `R1-041` sont différés, non bloquants administrativement
pour Gate 1, mais jamais déclarés PASS. Le volet cadence de `R1-048` est différé ; les revues humaines,
la vidéo et les preuves d'exécution GPU restent dues. Les 42/55 routes cochées sont inchangées.

Le dernier replay exact `61713887` / `7fbcae7b` franchit l'acquisition et produit un **FAIL schema 13** :
26,1661 FPS affichés, p95 131,956 ms, 1 % low 3,4183 FPS, fenêtre stable et ETW 0/0. Voir la
[preuve et les hashes](../evidence/gate1/R1_048_GAMEPLAY_STUTTER_PRESENTMON.md).
Les expériences n'établissent ni cause certaine ni optimisation produit ; leurs modifications sont
retirées. Les audits de performance et leurs rebuilds restent suspendus jusqu'à nouvelle demande.
Cet état courant prévaut sur les demandes de replay de la chronologie historique ci-dessous.

### Preuves acquises et historique conservé

Visual Valley conserve la paire native/Browser exacte `d9e2670`, les captures fixes, les ledgers
versionnés et l'audit de provenance CC0/Cadle. Aucun asset Cadle n'est copié dans Forge. Les lots
`6a24cabe` et `873618af` préservent respectivement la correction du défaut depth-only puis le vrai
contrôleur gameplay, la caméra `ForgePlayerCamera` et Rapier ; la campagne exacte `125ba0b` les
réacquiert avec l'optique d'eau courante. La matrice native `5eadec7f` et le harnais temporel Browser
réacquis sur le candidat exact `4e476b31` restent valides dans leurs frontières documentées ; ils ne
remplacent ni le benchmark GPU cible ni la revue humaine.

La chaîne de travail courante intègre aussi l'optique d'eau native bornée `e7e7d419`, puis le contrat,
le modèle et la route runtime/orchestrateur RenderDoc `dbe5eb86`/`140acbdd`, durcis jusqu'à
`347d782`. Le lot `e2d924b` remet le moteur eau/qualité en conformité avec Clippy strict sans changer
le contrat fonctionnel : 141/141 tests bibliothèque moteur et le test Low → Ultra → Low passent. Le
lot `798b55d` durcit ensuite la machine d'état RenderDoc et sa garde de profil : 69/69 tests runtime,
4/4 tests du binaire de garde, 4/4 tests Rust RenderDoc ciblés et 22/22 tests Python RenderDoc passent.
Le correctif de reproductibilité Windows `4e476b31` classe enfin les neuf descripteurs `.gltf`
gouvernés comme binaires afin que Git préserve exactement leurs octets. Sur ce commit, le portail
workspace complet `Test-Rust.ps1` passe : formatage, Clippy workspace strict, 354 tests natifs,
check moteur WASM, Clippy Studio WASM, build Trunk Release, `cargo deny` et politique RustSec. Le
marqueur final observé est `FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True`.

La campagne eau exacte `125ba0b` passe son validateur automatisé avec 29 PNG uniques, une vidéo et des
logs propres. Le préflight RenderDoc réel vérifie les 72 fichiers du bundle puis refuse, avant build,
l'absence d'une couche Vulkan enregistrée. Ces résultats ne ferment encore aucune
route : les qualifications GPU/humaine de l'eau et les quatre captures Vulkan indépendantes restent
dues. Aucun résultat GPU ou humain n'est déduit de la seule présence du code ou de composants ECS.

Le lot natif schema v3 `815ddf3` ajoute ensuite des diagnostics retardés de passes de scène aux vues
eau. Sa première acquisition exacte est refusée, sans PASS versionné, car les neuf assets gouvernés ne
sont pas trouvés depuis le répertoire du binaire Release. Le correctif d'asset-root `41b49f66`, arbre
`8da220873059e91daed6accc2c3ab0cfabe54e11`, relance le même binaire exact en Low puis High sur RX
7800 XT/Vulkan 26.8.1. Les deux validations automatisées **frames-only** passent avec 29 PNG chacune,
des logs sans warning/erreur/panic et respectivement six et douze mesures de passes synchronisées dans
les fenêtres de settle. Les neuf modèles CC0 sont lus en place et aucun asset, notamment Cadle, n'est
copié près du binaire. Cette preuve porte sur la scène entière, sans identité de frame de rendu ni
attribution au matériau eau ou au shader de wetness ; l'inspection bornée d'agent n'est pas une revue
humaine. `R1-023`, `R1-025` et `R1-026` restent ouvertes.

Le paquet de revue humaine fail-closed
[`gate1-human-review-package.v1.json`](../evidence/gate1/gate1-human-review-package.v1.json), SHA-256
`6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8`, référence désormais 27
artefacts contrôlés sans incorporer de média Grassworks ni d'asset Cadle. Son
`prepared_from.commit=73db783975251cf7b4439dce9a12328a4c55c5ec` désigne le dernier commit
existant contenant tous les artefacts suivis référencés ; il ne prétend pas être l'identité Git de
cette révision encore non committée du paquet. Son validateur passe en contrôle statique, self-test
synthétique et contrôle externe complet. `R1-014`, `R1-046`, `R1-047`, `R1-049`, `R1-050`, `R1-051`
et `R1-052` sont prêtes à recevoir un verdict humain ; le paquet ferme zéro route et conserve
`human_review_performed=false` et `gate1_transition_authorized=false`.

Le harnais de matrice météo Browser est committé à
`48038854b828003dd514562e34d5930e614422b4` et son garde d'entrée ainsi que son reçu d'ABORT sont
durcis à `534ce4faef69972dc1a495981387ac24a3ca1cfd`. L'acquisition canonique unique Low puis High sur
ce commit est **PASS automatisé** avec le statut `pass_pending_human_and_precise_gpu_evidence` : six
checkpoints, douze captures, 4/4 bundles exacts et console/réseau propres. Le ledger, le manifeste et
les médias restent externes ; aucune preuve GPU précise, revue humaine ou fermeture de route n'en est
déduite.

Les diagnostics historiques `15635b3f`, `2fe48485` et la matrice de pacing `6f0e519a` sont conservés.
Ils ont échoué ou sont restés non qualifiants selon leurs protocoles d'origine. Le lot `06b5fd40`
isole séparément le timeout DX12/FXC à la compilation de pipelines. Les captures DX12/DXC vides et
massivement perdues n'étaient toutefois pas une preuve de régression DX12 : l'enquête ultérieure a
identifié une session PresentMon ETW orpheline encore active comme cause racine locale.

L'historique de la chaîne source commence à
`2bf5e44f5d06529b26208a1ac7c9a4c158d48aad`, qui ajoute un profil de spans de présentation borné,
opt-in et explicitement non qualifiant. `e1e818fc1ed8655b851e34007039f4b288941377` expose les noms de
systèmes Bevy nécessaires à ce profil via `bevy/debug`, uniquement dans la feature diagnostique.
`9db35a8e125c6c60cde9de66a7f6aa7585215bfa` durcit ensuite le harnais gouverné avec un mutex machine,
deux préflights ETW, un postflight et un arrêt de secours limité à la session possédée par le run.

La session orpheline exacte
`OpenRPGForge-Gate1-Diagnostic-85a698103aaf4186a480beabd1840099` a été arrêtée par son seul
identifiant avec l'outil officiel épinglé, puis son absence a été vérifiée. Le premier rejeu propre
reçoit 11 lignes cible, sept avant signal, avec zéro perte ETW. Aucune session étrangère n'est arrêtée.
Cette récupération invalide l'hypothèse historique d'un simple défaut de taille ou de nombre de
buffers ETW.

Le run Release normal gouverné `9db35a8e`, schema 7, reconstruit dans un target Cargo propre, atteint
128,7260 FPS affichés, p95 13,9112 ms, p99 13,9657 ms et 1 % low 58,3489 FPS. Il ne contient aucune
valeur affichée au-dessus de 33,3 ms, au plus une valeur consécutive au-dessus de 16,7 ms, zéro perte
ETW et une résidence dérivée sans trou sur toute la fenêtre. Il conserve 17 lignes
`DisplayedTime=NA`. Son ancien contrat exigeait en plus zéro ligne non affichée ; le reçu schema 7
reste donc honnêtement `runtime_qpc_display_overlap_thresholds_failed` et `gate1_qualified=false`.

Deux répétitions du même binaire comptent 16 et 21 lignes non affichées ; le contrôle FIFO en compte
19. Sur les quatre runs, 73 des 15 570 lignes CPU bornées sont non affichées. Soixante-neuf se
concentrent dans deux transitoires, mais quatre restent tardives : aucune sous-fenêtre favorable n'est
retenue. Le diagnostic A4 avec le PresentMon officiel 2.5.1 reproduit 16 lignes non affichées et une
cadence voisine ; il reste **NONQUALIFYING** à cause du target réutilisé, de l'absence de rebuild
gouverné et du shim d'identité 2.4.1 dans le signal runtime.

ADR-0008 clarifie désormais qu'une présentation applicative non affichée reste un diagnostic
obligatoire mais n'est pas, à elle seule, un trou de résidence visible. Le code de ce lot porte cette
décision en schema 8 et l'analyse A/B en schema 2. Deux replays gouvernés ont désormais été acquis au
commit `49acfd5695a7246062a86fb84f5d933130579187`, arbre
`c6e7f6c5976c6a1b16f32de7f80d4d044bd64ceb`. Replay1 perd le premier plan sur 182/275 échantillons
de l'enveloppe et reste FAIL ; Replay2 passe le focus sur 274/274, conserve une résidence sans trou,
13 lignes `DisplayedTime=NA` et ETW 0/0. Il atteint 129,9968 FPS, p95 13,8805 ms, p99 13,9329 ms et
1 % low 61,2708 FPS, mais une valeur isolée de 35,3837 ms dépasse 33,3 ms. Aucun PASS schema 8 n'est
donc revendiqué et `R1-048` reste ouverte.

Un nouveau rebuild/rejeu gouverné est ensuite lancé au commit exact
`1912806fc6e90277cd779a21a2557e6c820d5d44`, arbre
`4bfb863255dd865432093c5e7291287b8e78dc43`, avec le harnais schema 11. Le runtime publie le reçu
prêt schema 4 et PresentMon collecte 338 lignes Forge avec `EventsLost=0` et `BuffersLost=0`. Après le
settle de premier plan, le sampling de la fenêtre native présente toutefois un trou strictement
supérieur à 250 ms. Le harnais abandonne **avant** le signal : aucun host boundary, aucun ledger final
et aucune validation ne sont publiés. Cette acquisition a donc le verdict
**ABORT INFRASTRUCTURE AVANT SIGNAL — AUCUN VERDICT PERFORMANCE**.

Le commit `9b847e996c39e76a9620e18cc1d7c5b2440e6145` porte désormais la frontière hôte au schema 5 et la
validation au schema 12. Il autorise au plus trois tentatives de restabilisation dans un budget global
de 15 secondes, exclusivement avant le signal, et publie un reçu d'abandon v1 en cas d'échec. Ses
self-tests, la syntaxe PowerShell et le diff-check passent.

Quatre tentatives schema 12 ont ensuite été acquises sans atteindre le signal. À `9d19cde5`, le runtime
observe 69/69 pipelines et un ledger qualité 34/34 exact en `5/16/13`, mais l'attente historique
`5/14/15` bloque la readiness. Le correctif `4b283182` dérive les comptes attendus du contrat canonique
et conserve une assertion explicite `5/16/13` ; 141/141 tests moteur Release, 67/67 runtime et 1/1
intégration passent. Son rebuild propre termine en 29 min 23 s et franchit réellement la readiness,
mais PresentMon reste vivant avec un stdout vide pendant 20 secondes. Le harnais abandonne avant
signal, sans host boundary, ledger final, validation schema 12 ou verdict performance. Les processus
et la session ETW sont nettoyés. Un écran déjà endormi est l'hypothèse locale prioritaire, non prouvée
par le reçu. Le replay `2ced195b`, arbre `8a46bd45`, reconstruit ensuite en 30 min 06 s et reproduit
l'ABORT avant signal : ready 69/69, 401 frames de rendu observées, puis stdout PresentMon vide. Un
probe host-wide simultané écrit six lignes d'une autre application avec ETW 0/0, mais aucune ligne
Forge.

Un smoke court explicitement non qualifiant sur le binaire partagé `41b49f66` confirme ensuite que
l'écran actif produit le header exact et 22 lignes Forge ETW 0/0. Le replay 4 reconstruit le commit
exact `83f02928da72774a49d3d62ee68be85335b0bf01`, arbre
`551fd73a2d6726b0a4ebbbe4a61f83cd78533e34`, en 32 min 27 s. Son binaire de 82 668 032 octets porte
`8d2c8d410b68d56e777a68f33c5b853269a217c330580ef827f3929d5c610d64`. Le ready confirme 69/69
pipelines, zéro erreur, 314 frames observées et 32 stables ; PresentMon conserve 25 lignes Forge,
19 `DisplayedTime` numériques, six `NA` et ETW 0/0. Le replay s'arrête néanmoins avant signal : le
dernier `CPUStartQPC` complet `7685712407723` précède l'échantillon fenêtre final
`7685719844327` de 743,6604 ms, au-delà de 250 ms. Aucun seuil de performance n'est évalué.

Le durcissement est committé à `473abeaac1425f0fb991f88ba0495d3708978ea1`, arbre
`6a40060a0b8f3c7d2f2f7188f7723a85c8fd9cb6`, et classe désormais cette frontière finale dans la
tentative pré-signal, avant son acceptation. Un gap final strictement ordonné >250 ms enregistre un rejet sans
signal et rejoue dans les limites existantes de trois tentatives et 15 secondes ; un QPC non monotone,
la dernière tentative ou le timeout échoue fermé. Les seuils, le settle de 2 000 ms, l'échantillonnage
de 100 ms et la fenêtre restent inchangés. Parseur PowerShell, self-tests positifs/négatifs et contrôle
statique de l'ordre pré-signal passent. Ce commit n'a fait l'objet d'aucun rebuild ni replay corrigé ; `R1-048`
reste ouverte.

La campagne eau exacte `125ba0b8bb1fe04b2b34cae5abc41c26d1919b00`, arbre
`6d4639c824ccfa81c3d5ed911903cec2bb3c5074`, produit 29 PNG aux hashes uniques et un MP4 de 24
frames/3 secondes. Son validateur schema 2 passe, les 75 pipelines sont prêts et les logs runtime ne
contiennent aucune entrée warning/erreur/panic. Les trois vues eau clair/pluie/retour clair sont
cohérentes à l'inspection d'agent. Les tests du chemin CPU/ECS résolvent le SSR indisponible vers
Environment IBL et la réfraction vers `ScreenSpaceTransmission` High. La campagne ne sérialise ni
route `R1-023`, ni observation du pass optique GPU ; `weather_gpu_execution_observed=false` et
`human_review_performed=false`. `R1-023`, `R1-025` et `R1-026` restent honnêtement ouvertes.

Au commit intégré `347d78264e52363c769e2f2807303d84c65ea946`, arbre
`4d6121f3dbc85fb090c1663c8257bee47c64300a`, le préflight RenderDoc réel recoupe 72 fichiers et
210 237 254 octets avec l'inventaire externe épinglé, puis s'arrête sur la couche Vulkan non
enregistrée sans créer de build, profil jetable, runtime ou capture. Son reçu de FAIL de sécurité
porte le SHA-256 `E99FEA9E92DE493D0C85A3BB1F57D066B29428333509A702B3FA658076EB3B26`.

Le verdict `R1-055` reste **poursuivre Gate 1**. La route `R1-037` reste ouverte : son contrat, son
workload runtime et son chemin d'acquisition sont intégrés et testés, mais l'exécution QRenderDoc et
les captures finales restent `NOT RUN` ;
`R1-041`, `R1-048`, le GPU
cible RTX 3060/RX 6600, la revue humaine et la CI distante restent dus. Gate 1 reste ouverte ; ce
rapport ne constitue ni un passage administratif de Gate 1, ni une autorisation implicite de commencer
Gate 2.

## Identités exactes et frontières de preuve

| Portée | Commit | Tree | Usage dans ce rapport |
|---|---|---|---|
| garde Browser, reçu d'ABORT et acquisition automatisée | `534ce4faef69972dc1a495981387ac24a3ca1cfd` | `8576d501a02fee5eabfb246d7b5269f0b8b19f6f` | durcissement du harnais `48038854`; matrice Low/High PASS automatisé, preuves GPU précises et humaines absentes |
| protocole sémantique R1-051 | `73db783975251cf7b4439dce9a12328a4c55c5ec` | `466cb51b12203e2358aec7ebcc9051eb58a1234e` | protocole JSON SHA-256 `cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d`, guide, sept références visuelles et cinq axes ; aucune revue humaine ni fermeture |
| harnais de matrice météo Browser | `48038854b828003dd514562e34d5930e614422b4` | `f3fc51386cd1e46d241342d791d501953e4f384d` | protocole Low/High clear/rain/clear et validations statiques ; acquisition exacte portée par `534ce4fa` |
| durcissement PresentMon final | `473abeaac1425f0fb991f88ba0495d3708978ea1` | `6a40060a0b8f3c7d2f2f7188f7723a85c8fd9cb6` | rejet/retry pré-signal du gap final `row→sample`; validations statiques acquises, aucun rebuild/replay de ce commit |
| préservation exacte des octets glTF sous Windows et qualification workspace | `4e476b31ee33533b6f4023804d8ecd1be02f9c55` | `cc6efae0289b10dcf12f97e6bb4d97b3ead29f90` | ajoute une règle `.gitattributes` binaire bornée aux neuf descripteurs glTF gouvernés ; checkout Windows 9/9 en octets canoniques ; `Test-Rust.ps1` PASS avec 354 tests natifs, WASM, Trunk Release, `cargo deny` et politique RustSec ; marqueur final `FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True` |
| acquisitions Low/High des diagnostics de passes de scène | `41b49f66d4573de505be44dfd38da75704d697f7` | `8da220873059e91daed6accc2c3ab0cfabe54e11` | correctif d'asset-root et acquisitions exactes **frames-only** du binaire Release SHA-256 `0d2e7c32b18ba18dbdfac503765cfed5a2dfd29366d5ae13d12b044866b42c48` ; 29 PNG par tier, 6 mesures Low, 12 mesures High, logs propres, assets CC0 lus en place ; aucune fermeture de route |
| instrumentation native de passes de scène | `815ddf3dc715952700164059e876d7151ccc8133` | `c1e8951a266a407229eb41899dc39abc58eb2f16` | schema v3 et diagnostics retardés Bevy ; première acquisition refusée sur neuf assets absents, non versionnée comme PASS ; correction portée par `41b49f66` |
| qualification stricte de la route runtime RenderDoc | `798b55ddbd903c1f5740d8fa23350268220430bf` | `f40c048befe2b5892f460e8ff4904da6fa192d0e` | Clippy strict runtime PASS ; 69/69 tests runtime + 4/4 tests du binaire de garde, 4/4 tests Rust RenderDoc ciblés et 22/22 Python PASS ; aucune capture Vulkan qualifiante acquise |
| qualification stricte eau et profils qualité | `e2d924b20fa9709b4455584aee513536b0f07f44` | `0cc0d1166dcd8d775771d2d52ffbc5b235ec89cf` | Clippy strict moteur PASS ; 141/141 tests bibliothèque moteur et 1/1 transition Low → Ultra → Low PASS ; aucune qualification GPU ou humaine ajoutée |
| quatrième replay schema 12 et base du durcissement final | `83f02928da72774a49d3d62ee68be85335b0bf01` | `551fd73a2d6726b0a4ebbbe4a61f83cd78533e34` | rebuild propre 32 min 27 s ; ready 69/69, 25 lignes Forge et ETW 0/0 ; ABORT avant signal sur gap final `row→sample` 743,6604 ms >250 ms ; correctif ensuite committé à `473abea`, non rejoué |
| troisième replay schema 12 sans flux PresentMon | `2ced195bc2e396b599d1ed66680422e97cea7753` | `8a46bd4573863aa9eff91fe902423299bfe72add` | rebuild propre 30 min 06 s ; ready 69/69 et 401 frames de rendu observées ; ABORT avant signal sur stdout PresentMon vide ; probe host-wide ETW 0/0 sans ligne Forge ; aucun verdict performance |
| correctif readiness et replay schema 12 sans flux PresentMon | `4b2831820fe6864046602f25ca5d92e1d63cbae8` | `16371171fc0b62a0d9064aff47cf5769e33b4106` | 141/141 moteur Release, 67/67 runtime + 1/1 intégration ; rebuild propre ; ready 69/69 et qualité `5/16/13` ; ABORT avant signal sur stdout PresentMon vide, aucun verdict performance |
| premier replay schema 12, validateur qualité périmé | `9d19cde5a442ecfa7694a864aad0967c4acd65c9` | `1d7649bcda5862623ced8adcafd6b2a71cc31b41` | 34/34 champs exacts et distribution `5/16/13`, mais attente `5/14/15` ; ABORT avant ready, PresentMon non lancé, aucun verdict performance |
| préflight RenderDoc intégré et durci, non qualifié | `347d78264e52363c769e2f2807303d84c65ea946` | `4d6121f3dbc85fb090c1663c8257bee47c64300a` | 22/22 Python, 67/67 runtime + 1/1 intégration ; inventaire des 72 fichiers contrôlé ; arrêt fail-closed sur couche Vulkan non enregistrée, quatre captures encore dues |
| preuve eau native automatisée, non qualifiée | `125ba0b8bb1fe04b2b34cae5abc41c26d1919b00` | `6d4639c824ccfa81c3d5ed911903cec2bb3c5074` | 14/14 tests eau, 29 PNG uniques, MP4, 75 pipelines prêts et logs propres ; pass optique GPU non instrumenté, aucune route R1-023 sérialisée, revue humaine due |
| optique d'eau native bornée intégrée | `e7e7d41951a3579936a1c9f8b8601fa639898679` | `5e7e5ec10c5f654c84d52e4d8bbc4f951dcfe83d` | implémentation R1-023 : Environment IBL fallback et ScreenSpaceTransmission High |
| harnais de restabilisation pré-signal | `9b847e996c39e76a9620e18cc1d7c5b2440e6145` | `9ee511f7d529098d003a78795e0d9cc94fcc3726` | host boundary schema 5, validation schema 12, trois tentatives/15 s maximum avant signal et reçu d'abandon v1 ; pas encore rejoué de façon qualifiante |
| replay schema 11 gouverné avorté avant signal | `1912806fc6e90277cd779a21a2557e6c820d5d44` | `4bfb863255dd865432093c5e7291287b8e78dc43` | ready schema 4 et 338 lignes Forge PresentMon, ETW 0/0 ; gap de sampling fenêtre `>250 ms`, aucun signal/host boundary/ledger/validation et aucun verdict performance |
| source et replays gouvernés schema 8 | `49acfd5695a7246062a86fb84f5d933130579187` | `c6e7f6c5976c6a1b16f32de7f80d4d044bd64ceb` | deux rebuilds/replays propres distincts ; Replay1 FAIL avec focus perdu, Replay2 FAIL sur un unique `DisplayedTime` de 35,3837 ms ; aucun PASS de `R1-048` |
| garde ETW et run gouverné schema 7 immuable | `9db35a8e125c6c60cde9de66a7f6aa7585215bfa` | `656feb155c4a30371210229bf6c5dea5bbf8fcd3` | harnais schema 7 avec mutex/préflights/postflight ; rebuild Release normal propre et capture 2.4.1 complète ; 17 lignes non affichées, cadence conforme, statut schema 7 encore FAIL |
| noms Bevy du profil de présentation | `e1e818fc1ed8655b851e34007039f4b288941377` | `274e7c51006eaee0cf0de300a393f332afe504d1` | ajoute `bevy/debug` à la seule feature diagnostique et les tests de noms réels ; binaire instrumenté et target partagé NONQUALIFYING |
| spans de présentation bornés | `2bf5e44f5d06529b26208a1ac7c9a4c158d48aad` | `29683ad294242e6582ea61076538be26cba9531f` | profil `presentation-profile` opt-in ; spans applicatifs uniquement, jamais assimilés à `DisplayedTime` ou au FPS écran |
| diagnostic de timeout historique | `06b5fd404bb4c3918b4b7ea154c8d7878609fa8d` | `a195b2b865c45502f3b412a43bfb20f4d53f9491` | runtime Release 82 333 696 octets, SHA-256 `b1f0f17b…60e60ab` ; 2/2 tests Release ciblés, Clippy Release strict, formatage et diff-check PASS ; DX12/FXC isolé à la compilation de pipelines ; aucune métrique qualifiante |
| baseline Release et pacing | `6f0e519afd01a701dd4d772d0d3837dd5cbf9814` | `cfbe45ecd3464c16f47ef1b2da2bf7fd5b78d9c7` | trois runs Auto et trois FIFO valides côté infrastructure ; analyse `5026e75d…81dd3e` ; verdict FAIL, aucune proposition de pacing, `qualifying=false` |
| durcissement hôte publié de base | `a340494939c82a8800999c14a44c06a797d74f5d` | `b6d18b992ac0e1f84724cd301da6dc7c08cb9ed7` | base exacte du lot `06b5fd40` ; activation PresentMon durcie, sans verdict de performance propre |
| diagnostic opt-in `render-profile` historique | `2fe48485086e925422c667f49c38a80f96e1a81a` | `378f13f68e7689a6790aa544f1e5b4a1be30ad16` | runtime Release 90 245 632 octets ; checks normal/feature, Clippy strict normal/all-features, 2/2 unitaires, guard 4/4, formatage, diff-check et provenance PASS ; trace et PresentMon NONQUALIFYING, seuils FAIL |
| source publiée et cadence de preuve historique | `15635b3f85b623654e498b17ed299674928313e6` | `187ae357e24c47403b9c638c0a0737ed01215e33` | 140/140 moteur Release, 52/52 runtime Release, 1/1 intégration Release, Clippy strict et provenance PASS ; diagnostic PresentMon non qualifiant historique, seuils FAIL ; CI `33584774630` attachée à ce commit |
| acquisition visuelle et contrôleur réel | `873618af59eb2e2b6426b91d4fff0ec615fc70d8` | `9ba6455fd632d6532bda27457312c7d229d46e2f` | build Release, 29 PNG + MP4 ; contrôleur/Rapier/caméra acquis, stutter/humain/GPU en attente |
| correctif prépasse | `6a24cabe69e242ff551b265d44da67ca8d017c98` | `38a6990ce02baa651773d23d1038b3ef9ff9e76d` | défaut depth-only corrigé et réacquisition historique ; qualification humaine/GPU en attente |
| temporel Browser du candidat qualifié | `4e476b31ee33533b6f4023804d8ecd1be02f9c55` | `cc6efae0289b10dcf12f97e6bb4d97b3ead29f90` | acquisition temporelle automatisée PASS réacquise : 120 RAF, deux PNG, 3/3 ROIs végétation, témoin terrain, 4/4 bundles et journaux propres ; revue humaine due |
| harness visuel natif | `a7c9b1e1d5e32f826b606778434a5fae91cc947d` | `2066fce699e4dd178c3d679cf4b0168d2e727d3d` | acquisition native exact-commit de 29 PNG, séquence clair/pluie/clair et MP4 3 s ; qualification refusée |
| overlay CPU/GPU | `afb270c328b7095a84d452b93b9fdd38e2737f33` | `5eeb9098b8300dead8679de7d478f5096a572fd1` | temps CPU/GPU du pass opaque visibles, fallback indisponible testé, deux ledgers/captures High exacts |
| matrice profils | `5eadec7f6f9f9b5b6734ce62ea3518f0297496f9` | `a2fb4df0b7e2eff4d686dda575d4a2bf18cb12a1` | 10 ledgers/captures natifs, quatre tiers, adaptation High vers Ultra, benchmark High et eau synchronisée |
| campagne Browser exacte | `d9e2670111624aa836ab138afc35cddf441865ef` | `d6e5a40f73b47934444bda4514d5cdcb1dab093d` | Browser clear/rain, manifeste Studio, assets HTTP et captures Browser |
| natif performance/captures exact-commit | `d9e2670111624aa836ab138afc35cddf441865ef` | `d6e5a40f73b47934444bda4514d5cdcb1dab093d` | RX 7800 XT/Vulkan, métriques v8, captures et logs natifs exacts |
| campagne graphique historique | `a34596bd733131699c70028dab86cf2c612b896a` | `8ccdf8cb2cb6a6aabd3a4569f22f1778e164ced6` | ancienne paire exacte conservée byte-for-byte ; warning et dérive visuelle reproduits |
| provenance/indépendance | `92286910a9bab622d5d54905551c0f03172e98e7` | `6d7cd4ab8612686e7f73f01abeb5b8ad0174ebbe` | audit CC0, comparaison Cadle et absence de dépendance produit |
| inventaire conditionnel `R1-033` | `28985328f0d2de1fa79c705d5c3aa1bc522dbe35` | `ec71501d231ea6469eb97dc0af3d0230f4daf6eb` | inventaire automatisé des glTF gouvernés, rôles du manifeste et représentation procédurale du joueur |
| référence Cadle extérieure | `ea0da62549980816a6a27e08b75808b1f56d70e7` | `f0d52e434454fe302052d22eafde203bc361cc1f` | comparaison de provenance uniquement, clone local détaché et propre |

Le natif et le Browser forment une paire exacte d9. Le hash scenery
`6b41ec73efde5b8a40ca0132e1d32462176d6b4810104f6bed69496569f81f5f` est identique aux deux
manifestes Browser. Les preuves `a34596bd` et `92286910` restent historiques et ne sont pas utilisées
pour conclure la parité courante `R1-043`.

## Acquisition gouvernée courante et résultats schema 8

### Identité et garde ETW `9db35a8e`

Le run gouverné immuable est stocké hors dépôt sous :

```text
E:\OpenRPG_Forge\.gate1-tools\presentmon\diagnostics\vulkan-governed-normal-9db35a8-20260902T163730202Z
```

Il utilise les features normales `default,native`, sans `presentation-profile`, `bevy/trace` ni
`bevy/debug`. Le binaire Release de 82 333 696 octets porte le SHA-256
`C3790DA4A7A9F46B36F0837144A75F5AFDD43A63101093D25F402D09E0E80C4A`. Le worktree est propre
avant et après le rebuild. La fenêtre reste stable, visible, jamais minimisée et au premier plan sur
toute la fenêtre ; le runtime ne publie aucun warning, erreur ou panic.

Le garde refuse toute session Gate 1 préexistante sans la terminer. Il acquiert le mutex machine
`Global\OpenRPGForge-Gate1-PresentMon-Guard`, répète le préflight immédiatement avant le lancement,
marque la session possédée avant de démarrer PresentMon et ne peut arrêter en secours que cet
identifiant exact. Le postflight vérifie son absence. L'appel `logman query -ets` est borné et le
parseur des seuls préfixes Gate 1 attendus est insensible à la casse.

### Résultat schema 7 et hashes

| Invariant ou mesure | Résultat |
|---|---:|
| durée QPC runtime | 29,9824273 s, intervalle demi-ouvert exact |
| contrôleur | 1 800 ticks, 900 avant + 900 arrière, zéro catch-up |
| frames main-world | 3 878 |
| lignes CPU PresentMon bornées | 3 877 |
| valeurs affichées / lignes non affichées | 3 860 / **17** |
| `DisplayedTime` moyen / FPS moyen | 7,7684 ms / **128,7260 FPS** |
| p95 / p99 | **13,9112 / 13,9657 ms** |
| 1 % low | **58,3489 FPS** |
| valeurs `>16,7 ms` / `>33,3 ms` | 17 / **0** |
| maximum consécutif `>16,7 ms` | **1** |
| pertes ETW événements / buffers | **0 / 0** |
| résidence couvre début et fin / trou maximal et total | oui / **0 et 0 QPC** |

| Artefact | SHA-256 |
|---|---|
| validation schema 7 | `61223F1362C85CC7F0990279DF2DD77C9CA6A71B8A9B9EB9C834D697D76FD571` |
| CSV PresentMon complet | `12052E2EFB218837B1985528B06B68F6E44FE22AF1E25F91608C102BA31C9F7B` |
| ledger runtime | `ACF0A7BD21034335EB090F52D8F9E9C7702724664D0B9F24E71E708C62B1ECA1` |

Le schema 7 compte correctement les lignes non affichées et conserve le CSV complet. Son seul
sur-critère de stutter est `bounded_sample_has_no_not_displayed_frames=false`, qui entraîne le statut
`runtime_qpc_display_overlap_thresholds_failed`. Ce reçu n'est pas réécrit après ADR-0008 : il reste
la preuve exacte de l'ancien contrat.

### Répétitions et comparaison PresentMon 2.5.1

| Route | Lignes CPU | `DisplayedTime=NA` | FPS moyen | p95 | 1 % low | `>33,3 ms` | max. consécutif `>16,7 ms` |
|---|---:|---:|---:|---:|---:|---:|---:|
| Auto gouverné 2.4.1 | 3 877 | 17 | 128,7260 | 13,9112 ms | 58,3489 FPS | 0 | 1 |
| Auto répétition A1 | 3 896 | 16 | 129,3634 | 13,9130 ms | 53,0763 FPS | 0 | 2 |
| Auto répétition A2 | 3 890 | 21 | 128,9631 | 13,9079 ms | 54,9590 FPS | 0 | 1 |
| FIFO diagnostic | 3 907 | 19 | 129,6567 | 13,9083 ms | 57,3937 FPS | 0 | 1 |
| Auto PresentMon 2.5.1 A4 | 3 908 | 16 | 129,7334 | 13,9102 ms | 61,4409 FPS | 0 | 1 |

Les quatre runs 2.4.1 totalisent 73 lignes non affichées sur 15 570 lignes CPU bornées. Trente-sept
se trouvent entre `+0,0096` et `+0,7417 s`, 32 entre `+2,8528` et `+3,1949 s`, puis quatre à
`+16,005`, `+23,071`, `+25,619` et `+25,676 s`. Les lignes non affichées ont elles-mêmes un
`FrameTime` de 7,67 à 11,37 ms et un `GPUTime` d'environ 5,36 à 9,05 ms. Cette concentration informe
le diagnostic sans autoriser une coupe rétrospective de la fenêtre.

Les synthèses A1, A2 et FIFO portent respectivement les SHA-256
`53F96593670ED0C42A13B64C06E42BFCD59695FBD84BCDEFE6604D201FB0E533`,
`98F45110B53AD4E22917F54A26DC3D3EAD66B6B241277AFD417A21A6514E86F8` et
`15B0B6363ECFFF9ECA2CE1942C0B251C71FAB10BDCF7495C9D9442048BFFE5CD`.

Le binaire officiel PresentMon 2.5.1 mesure 956 768 octets, porte le SHA-256
`9BEC3083069F58F911E6A512F4806DB51A27BD096103087BC1D05EF54C80A191` et une signature Intel
valide. A4 est volontairement **NONQUALIFYING** : le target Cargo est réutilisé, aucun rebuild
gouverné n'est effectué et le signal runtime emploie un shim d'identité 2.4.1 alors que le collecteur
réel est 2.5.1. Sa synthèse porte
`AAC662F7177FAEEF2B26E923061517BF6CF4B80A80543E8DE31862FE3DE17B28`, son CSV
`5053DAE9491C5665077D430741258D2CADF1EBCCE627506820E137EA8C5DB2D2` et son ledger
`718270959D9CCB1BD6711CC6BCAE6E1CC3DF70B4F987EBA5A9AE16A6E646E95F`.

### ADR-0008 et replays gouvernés schema 8

ADR-0008 est accepté et versionné dans ce lot. Il conserve chaque ligne `DisplayedTime=NA` dans le
CSV et comme compteur diagnostique, mais sépare ce compteur de la continuité de résidence dérivée.
Un verdict stutter exige simultanément résidence complète sans trou mesurable, ETW 0/0, fenêtre et
signature stables, FPS moyen au moins 60, p95 au plus 16,7 ms, 1 % low au moins 45 FPS, aucune valeur
affichée au-dessus de 33,3 ms et au plus une valeur consécutive au-dessus de 16,7 ms.

Le code de ce lot élève la validation gameplay-stutter au schema 8, publie séparément le compteur
non affiché, la continuité de résidence et les seuils temporels, puis élève l'analyse A/B au schema 2.
Ses self-tests en mémoire couvrent un compteur non nul avec résidence continue, une rupture de
couverture et chaque seuil temporel. Le run schema 7 reste immuable : les résultats suivants sont de
nouvelles acquisitions au commit/arbre exacts
`49acfd5695a7246062a86fb84f5d933130579187`/`c6e7f6c5976c6a1b16f32de7f80d4d044bd64ceb`.

| Replay gouverné | Binaire Release | Focus | Sélection affichée / `NA` | FPS moyen | p95 / p99 | 1 % low | `>33,3 ms` | ETW / trou de résidence | Verdict |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| Replay1 | 82 333 696 octets, `21228a133c5b58025695a68668f10387a7e368c445b3f43155df6e775ce177f7` | 182/275 échantillons hors premier plan | 2 257 / 155 | 75,2425 | 20,9013 / 34,7203 ms | 12,3978 FPS | 29 | 0/0 ; 0 QPC | **FAIL**, performance confondue par la perte de focus |
| Replay2 | 82 333 696 octets, `0c220f6980457a427dcf4f48c1204da07120fddc265c97e6ed61da2326a75a85` | **274/274 au premier plan** | 3 900 / **13** | **129,9968** | **13,8805 / 13,9329 ms** | **61,2708 FPS** | **1** | **0/0 ; 0 QPC** | **FAIL**, plafond absolu dépassé une fois |

Replay1 porte une validation de 79 290 octets, SHA-256
`36e1eebf9c4324ab285cbb7de1c5ea51117237e7bcb88a73c446c4be1d36d7bd`. Sa perte de premier plan
interdit de présenter ses métriques comme une régression produit qualifiée. Replay2 porte une
validation de 16 111 octets, SHA-256
`45b87d0d78570c0f6d0e541df040aa008ae49e2ca47b5d02002f223ea50894d1`. Son infrastructure passe :
fenêtre stable et visible, focus 274/274, bornes QPC runtime exactes, signature PresentMon stable,
résidence couvrant début et fin sans trou et ETW 0/0. Le seul seuil automatisé directement fautif est
`no_displayed_time_over_33_3_ms=false`, causé par une valeur de **35,3837 ms** au sample 18. Le FAIL
reste donc réel même si les autres seuils sont largement tenus ; aucune cause racine moteur n'est
déduite de ce seul outlier.

Les diagnostics schema 8 A4/A5/A6 sont distincts du diagnostic PresentMon 2.5.1 A4 historique décrit
plus haut. Tous trois réutilisent le binaire `21228a…` dans un target Cargo partagé, sans rebuild
gouverné propre ; ils portent donc `qualifying=false` et ne peuvent fermer `R1-048`. A4 est **FAIL**
sur un outlier au-dessus de 33,3 ms. A5 et A6 passent les seuils et le focus, avec respectivement
129,2931 et 128,2562 FPS, p95 13,9101 et 13,8968 ms, aucune valeur au-dessus de 33,3 ms, ETW 0/0 et
une résidence sans trou. Ces répétitions éclairent la variabilité, mais ne remplacent pas un replay
gouverné PASS sur un binaire propre.

## Validation source

Le dernier rejeu workspace complet est épinglé à la source exacte
`4e476b31ee33533b6f4023804d8ecd1be02f9c55`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`. Il est exécuté dans le clone de qualification propre
sur la branche `Rust`, avec `HEAD == origin/Rust` et une divergence `0/0` :

| Contrôle | Résultat |
|---|---|
| `cargo fmt --all -- --check` | **PASS** |
| `cargo clippy --workspace --all-targets --locked -- -D warnings` | **PASS**, zéro warning projet |
| `cargo test --workspace --all-targets --locked` | **354/354 PASS** dans 13 harnesses ; 141 moteur, 68 runtime, 52 monde, 44 projet et suites CLI/Studio/assets/intégration |
| check `forge_engine` `wasm32-unknown-unknown`, feature `web` | **PASS**, 12 min 17 s |
| Clippy strict `forge_studio` `wasm32-unknown-unknown`, feature `web` | **PASS**, 21 min 17 s |
| `trunk build --release --config Trunk.toml` | **PASS** ; compilation Release 26 min 37 s puis optimisation/assemblage WASM |
| `cargo deny check licenses bans sources advisories` | **PASS** — `advisories ok, bans ok, licenses ok, sources ok` |
| `scripts/Test-RustSec.ps1` | **PASS politique** — `vulnerabilities=0 warnings=20 distribution_blockers=1` ; blocker conservé `RUSTSEC-2024-0429`, `glib 0.18.5` |
| `scripts/Test-CadleGateC.ps1` | **PASS courant au snapshot 534** — `CADLE_GATE_C_BOUNDARY_PASS tracked=439 checkout_present=False`; le worktree documentaire ne contient pas de checkout Cadle local et les paquets documentaires ne sont pas classés comme configurations de distribution. Le replay historique `-RequireCheckout` reste attribué à son commit d'origine |
| `scripts/Test-Gate1Provenance.ps1` SelfTest + Audit strict | **PASS** — 7 cas ; 1 manifeste, 27 fichiers, 9 modèles ; 507 fichiers Cadle comparés, 0 correspondance |
| `scripts/Test-Gate1BrowserTemporal.ps1` | **PASS du test statique du harnais** ; ce test ne lance pas Browser (`browser_run=not_performed_by_static_test`), mais l'acquisition réelle a été réacquise séparément sur `4e476b31` ; inspection temporelle humaine toujours `pending` |
| `scripts/Test-Gate1PresentationAB.ps1 -SelfTest` | **PASS** |
| tests Python RenderDoc | **22/22 PASS** |
| marqueur portail | **`FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True`** |

Le check et le build WASM conservent l'avertissement de future incompatibilité transitive connu pour
`proc-macro-error2 v2.0.1`. Il reste distinct d'un warning projet. Cette validation couvre la source,
les contrats, le bundle WebGPU Release et la politique supply-chain ; elle ne produit aucune capture
GPU, revue humaine ou mesure de performance cible. Le précédent rejeu workspace `afb270c3`, la preuve
overlay afb, la matrice native 5e et la paire Browser/native d9 restent des campagnes historiques
exactes distinctes.

Au lot natif `a7c9b1e1`, la validation proportionnée du nouveau harness passe 37/37 tests
`forge_runtime`, le formatage, Clippy strict du runtime, la syntaxe PowerShell et l'acquisition native
exacte. Un rejeu workspace a été commencé, puis interrompu pendant la recompilation des dépendances
pour éviter un nouveau cycle d'environ une heure ; il n'a produit aucun verdict et n'est pas présenté
comme PASS. Le résultat 298/298 reste donc attribué uniquement à `afb270c3`.

Au commit Browser `a4e9824a`, la validation ciblée supplémentaire passe 28/28 tests unitaires Studio,
`cargo fmt --all -- --check`, le check WASM, Clippy strict Studio/WASM, les trois validations
PowerShell du harnais et l'acquisition Browser exacte. L'avertissement de future incompatibilité
transitive `proc-macro-error2 v2.0.1` reste distinct d'un warning projet. Aucun nouveau rejeu workspace
complet n'est revendiqué à ce commit.

Au commit source `873618af`, les 6 tests R1-048 ciblés et les 40 tests runtime passent en
Release. La matrice proportionnée moteur/runtime/intégration passe **167/167** en profil de test ;
`cargo check --release --tests`, Clippy strict runtime, le formatage, la syntaxe PowerShell et les
tests positifs/négatifs du watchdog passent également. Le build Release exact et l'acquisition native
schema v2 sont rejoués depuis le worktree propre. Aucun rejeu workspace/all-targets complet ni nouveau
check Studio WASM n'est revendiqué à `873618af`.

Au commit publié historique `15635b3f`, les tests Release passent **140/140** pour le moteur,
**52/52** pour le runtime et **1/1** pour l'intégration. La commande
`cargo clippy --release --locked -p forge_engine -p forge_runtime --all-targets --all-features -- -D warnings`
passe. Ce résultat est borné aux packages `forge_engine` et `forge_runtime` : aucun Clippy workspace
complet ni Clippy/check Studio WASM n'est revendiqué à ce commit. L'audit de provenance exact passe
sur 378 fichiers suivis, 27 assets et neuf modèles CC0 ; il compare 39 fichiers Forge aux 507 fichiers
Cadle sans copie exacte ni ligne normalisée commune. Aucun rejeu workspace all-targets complet ni CI
distante n'est revendiqué à ce commit.

Au commit opt-in `2fe48485086e925422c667f49c38a80f96e1a81a`, les checks `forge_runtime` en
configuration normale puis avec la feature `render-profile` passent. Clippy strict passe pour
`forge_runtime --all-targets` en configuration normale puis avec `--all-features`. Les tests unitaires
ciblés passent **2/2**, le guard d'intégration passe **4/4**, le formatage et `git diff --check` passent,
et l'audit de provenance est **PASS** avec le SHA-256
`22c1d795f94eb8f247e1b7c173ae06602ff17dd664f0c8d0ef2f4aea4a383892`. Le marqueur
schema 1 `qualifying=false` porte le SHA-256
`5f365295110fca0a1f47c5419f05e6a1ff1c33d1fe29619a27f4c9f9c34b3e48`. Ces validations prouvent
l'opt-in et son garde-fou, pas une cadence Release qualifiante.

La chaîne plus récente `2bf5e44f`/`e1e818fc` reste distincte de cet ancien `render-profile`.
`2bf5e44f` borne l'enregistrement des spans à la fenêtre gameplay et publie un marqueur non
qualifiant. Le premier rejeu ETW propre valide l'acquisition mais révèle que les noms Bevy sont
opaques. `e1e818fc` ajoute alors `bevy/debug` à la seule feature `presentation-profile` et ses tests
ciblés emploient les vrais noms de systèmes ; le diagnostic corrigé produit une synthèse
`qualifying=false`, SHA-256
`BA7AFEA882A5B44095AD1E53BBFF87EC053BED2D1C1FE075D30BD2DE775BA668`.

À `9db35a8e`, la syntaxe PowerShell, le self-test du garde, l'appel `logman` borné, la création du
mutex global et `git diff --check` passent avant le commit. Le rebuild Release gouverné et le run
schema 7 documentés plus haut restent une validation historique distincte. À `49acfd56`, deux builds
Release propres et deux replays PresentMon schema 8 sont exécutés : le premier est invalidé pour la
qualification par 182/275 échantillons hors premier plan ; le second valide l'infrastructure mais
échoue au plafond absolu sur l'unique valeur de 35,3837 ms. Ces exécutions retirent la mention
pré-rejeu, sans produire un PASS `R1-048`.

La validation proportionnée finale du lot exécute
`cargo test --release --locked -p forge_runtime --all-features -j 1` : **62/62 tests unitaires** et
**4/4 tests d'intégration** passent, soit **66/66**, sans échec. `cargo fmt --all -- --check`, l'auto-test
`Test-Gate1PresentationAB.ps1 -SelfTest` (10 cas négatifs, migration legacy fail-closed) et
`git diff --check` passent également. Aucun nouveau workspace `--all-targets`, aucun Clippy workspace
courant, aucune instrumentation exhaustive `R1-037`, aucune mesure GPU cible et aucune CI distante ne
sont revendiqués pour ce lot.

## Overlay CPU/GPU exact `afb270c3`

Le binaire Release exact mesure 81 745 408 octets et porte le SHA-256
`9c7a85d9719101e53a25f79d8846462561c81dd9f7e5b1f72743d0964f2caffa`. Deux runs High,
clear puis rain, ont produit 120 samples chacun, des pipelines 69/69 puis 71/71 et des logs vides.
Les captures affichent respectivement CPU 0,010 / GPU 0,674 ms et CPU 0,012 / GPU 0,661 ms pour le
pass opaque. Les ledgers mesurent 0,0101 / 0,68072 ms et 0,0153 / 0,7246 ms à des frames différentes.

Les six artefacts, leurs hashes et le contrôle d'absence de référence Cadle sont versionnés sous
[`native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay`](../evidence/gate1/native/afb270c328b7095a84d452b93b9fdd38e2737f33/overlay).
Cette preuve ferme `R1-034`. Elle ne fournit ni temps GPU de frame entière, ni compteur global de draw
calls, triangles ou instances ; `R1-037` reste donc ouverte.

## Matrice native courante `5eadec7f`

Le binaire Release courant mesure 81 743 872 octets et porte le SHA-256
`33b3ea91dff8d5d0aa4af054aacf6f575bc1dc449572b914452518acdeb1c3eb`. Dix runs 1920 x 1080,
VSync off, caméra joueur, ont été retenus sur RX 7800 XT/Vulkan : quatre tiers clear/rain,
adaptatif clear et benchmark clear.

Les dix ledgers v8 contiennent chacun 120 samples, 9/9 modèles CC0, des pipelines prêts avant et
pendant la mesure et un tier eau identique au tier appliqué. Les comptes pipelines vont de 59/59 à
74/74. L'adaptation effectue High vers Ultra à la frame 604 ; benchmark reste High. Les moyennes vont
de 5,6130 à 10,2077 ms, les p95 de 6,7886 à 13,9956 ms et les 1 % low de 56,293 à 134,937 FPS.
Low/rain contient deux hitches ; les neuf autres runs zéro.

Les vingt artefacts, leur manifeste SHA-256, les deux séries rejetées et la revue visuelle bornée
sont consignés dans
[`R1_039_040_NATIVE_QUALITY_MATRIX.md`](../evidence/gate1/R1_039_040_NATIVE_QUALITY_MATRIX.md).
Cette preuve ferme `R1-039`, `R1-040` et la condition « profils qualité fonctionnels ». Elle ne ferme
ni le GPU cible, ni le parcours représentatif, ni l'acceptation artistique.

## Acquisitions Low/High des diagnostics de passes de scène `41b49f66`

L'implémentation `815ddf3dc715952700164059e876d7151ccc8133`, arbre
`c1e8951a266a407229eb41899dc39abc58eb2f16`, fait passer le ledger visuel au schema v3 et échantillonne
les diagnostics `elapsed_gpu` retardés de Bevy aux checkpoints `water-clear`, `water-rain` et
`water-restored`. La première acquisition exacte est arrêtée par le validateur parce que les neuf
assets gouvernés sont absents du répertoire de lancement Release. Ce refus est le comportement attendu
et aucun artefact de cette tentative n'est versionné ou présenté comme PASS.

Le correctif `41b49f66d4573de505be44dfd38da75704d697f7`, arbre
`8da220873059e91daed6accc2c3ab0cfabe54e11`, impose au processus enfant le worktree propre comme
répertoire courant et `BEVY_ASSET_ROOT`, et demande le backend Vulkan. Le binaire Release commun porte
le SHA-256 `0d2e7c32b18ba18dbdfac503765cfed5a2dfd29366d5ae13d12b044866b42c48`. Sur AMD Radeon RX 7800 XT,
pilote 26.8.1 (LLPC), les acquisitions Low et High passent chacune leur validation automatisée
**frames-only** avec 29 PNG 1920 x 1080 non uniformes, une séquence clair → pluie → clair et zéro entrée
warning/erreur/panic dans les logs. Low fournit six mesures attendues ; High en fournit douze. Les
mesures sont finies, non négatives et synchronisées dans les fenêtres de settle de huit frames.

Les neuf modèles CC0 du manifeste Forge sont résolus 9/9 directement depuis le worktree ; aucun asset
n'est copié dans le répertoire Release et aucun asset Cadle n'est utilisé. La sélection versionnée
conserve les ledgers, validations, logs et trois vues eau par tier. L'inspection technique bornée de ces
six PNG par un agent confirme qu'ils sont exploitables et distincts, mais elle ne vaut pas acceptation
humaine et `human_review_performed=false` reste inchangé.

La portée demeure stricte : `DiagnosticMeasurement.time` décrit la synchronisation retardée dans le
monde principal, pas la frame GPU exacte. Les passes mesurées rendent la scène entière et n'isolent ni
un draw ni le matériau eau ; elles ne prouvent pas non plus que le shader consomme la valeur de
wetness. Les preuves GPU Browser précises, la revue humaine et la mesure sur GPU cible restent dues. Ce lot ne ferme
donc ni `R1-023`, ni `R1-025`, ni `R1-026`, et n'autorise ni le passage de Gate 1 ni le démarrage de
Gate 2. La CI distante reste `NOT RUN` sous la dérogation ADR-0006 jusqu'à Gate 4 ; ce statut n'est pas
un PASS.

## Acquisition visuelle native initiale `a7c9b1e1`

Le harness courant lie le binaire Release au commit exact
`a7c9b1e1d5e32f826b606778434a5fae91cc947d` et refuse une acquisition depuis un worktree sale. Le
binaire de 81 944 064 octets porte le SHA-256
`5047b8e8016f1897882fc55ef9c78a03bdc25c4aabe4d94a7ebd968b93b07a4c`. Le run 1920 × 1080 High,
VSync désactivé, sur RX 7800 XT/Vulkan a produit 29 PNG, une vidéo H.264 de 24 frames à 8 FPS pendant
trois secondes et deux logs vides.

Le validateur passe l'identité source, la résolution physique, le present mode, l'identité
GPU/backend/pilote, l'absence d'asset Cadle, la propreté avant/après et l'absence de
warning/erreur/panic. Il observe côté CPU/ECS la séquence clair → pluie → clair, la wetness exacte
0 → 0,78 → 0, les endpoints de grade et la présence du composant Bloom. Il laisse explicitement
`weather_gpu_execution_observed=false`, `human_review_performed=false` et
`gate1_routes_qualified=false`.

L'inspection des PNG originaux refuse la qualification : végétation sombre et fortement crénelée,
silhouettes lointaines en blocs, grands aplats et eau étroite/masquée par les berges. La vidéo utilise
une caméra déterministe de preuve avec `gameplay_controller_exercised=false` ; elle ne qualifie ni un
parcours gameplay ni le stutter. L'acquisition avance donc la preuve sans fermer `R1-023`, `R1-025`,
`R1-026` ou `R1-046` à `R1-048`.

Le protocole, les hashes, la sélection versionnée de 13 artefacts et la décision visuelle sont
consignés dans
[`R1_025_026_046_048_NATIVE_VISUAL_PROOF.md`](../evidence/gate1/R1_025_026_046_048_NATIVE_VISUAL_PROOF.md).

## Réacquisition corrective Release exacte `6a24cabe`

Le matériau de végétation est désormais exclu de la prépasse caméra. Le commit propre
`6a24cabe69e242ff551b265d44da67ca8d017c98`, arbre
`38a6990ce02baa651773d23d1038b3ef9ff9e76d`, passe le formatage, Clippy strict, 127 tests moteur, 36
tests runtime et un test d'intégration. Le binaire Release de 81 970 688 octets porte le SHA-256
`af63ad658557b706159caffbf7e05fb517d72cdf495eed1c234c4243ce187ddf`.

Sur RX 7800 XT/Vulkan en 1920 x 1080 High sans VSync, le harnais acquiert 29 PNG et un MP4 H.264 de
trois secondes. Les logs contiennent zéro warning/erreur/panic ; le décor gouverné est activé exactement
une fois avec neuf modèles, 27 fichiers, 16 652 493 octets et aucun fallback. L'audit strict confirme
un manifeste CC0 et aucun asset Cadle copié.

Le masque bleu-noir baisse de 5 737 à 64 pixels sur la vue lointaine, 4 735 à 6 sur la vue humaine et
1 426 à 0 sur la vue eau ; les trois frames de mouvement retenues baissent de 99,50 à 99,98 %. Cette
mesure et l'inspection technique confirment la correction du symptôme, pas une acceptation artistique.
Le ledger garde l'exécution GPU météo, la revue humaine, le contrôleur gameplay et la qualification
des routes à faux. La preuve est documentée dans
[`R1_046_048_VEGETATION_PREPASS_CORRECTION.md`](../evidence/gate1/R1_046_048_VEGETATION_PREPASS_CORRECTION.md).

## Acquisition contrôleur gameplay réelle `873618af`

Le commit propre `873618af59eb2e2b6426b91d4fff0ec615fc70d8`, arbre
`9ba6455fd632d6532bda27457312c7d229d46e2f`, produit un binaire Release de **82 042 880 octets**,
SHA-256 `ba55fd0c33a3a072d685cd93018e4e638d0f3f725df91978fe9c5ca7a8dec5b4`. Sur RX 7800 XT/Vulkan,
1920 x 1080 High sans VSync, le harness schema v2 acquiert 29 PNG et un MP4 H.264 de 24 frames,
8 FPS et 3 secondes. Les 103 événements runtime sont INFO ; aucun warning, erreur ou panic n'est
présent. L'audit strict passe avec un manifeste, 27 fichiers, neuf modèles CC0 et aucun asset Cadle.

Le ledger lie 24 checkpoints au même contrôleur, au même joueur, à la même caméra `ForgePlayerCamera`
et aux mêmes identités Rapier. À 60 Hz, les 173 ticks de déplacement restent tous grounded et avec
collision. Le joueur avance de 13,544837 m vers -Z, dérive de 0,172029 m en X, franchit la couture
centrale puis s'arrête en 11 ticks après la commande stop immédiatement suivante. Les PNG 000, 012 et
023 montrent le personnage-capsule et un déplacement net de la caméra ; les cinq vues fixes conservent
la correction bleu-noir et le cycle clair → pluie → clair.

Le statut runtime reste `automated_capture_complete_pending_stutter_and_qualification` et le statut
hôte `automated_artifacts_valid_pending_stutter_human_and_gpu_qualification`. Les checkpoints sont
pausés pendant la capture et le MP4 est assemblé ensuite : `real_time_stutter_qualified=false`.
`weather_gpu_execution_observed`, `human_review_performed`, `visual_quality_accepted` et
`gate1_routes_qualified` restent également faux. Le lot borné de 18 artefacts est versionné sous
[`native/873618af59eb2e2b6426b91d4fff0ec615fc70d8/visual-proof`](../evidence/gate1/native/873618af59eb2e2b6426b91d4fff0ec615fc70d8/visual-proof).

## Diagnostic PresentMon continu `15635b3f`

Le commit publié `15635b3f85b623654e498b17ed299674928313e6`, arbre
`187ae357e24c47403b9c638c0a0737ed01215e33`, produit le runtime SHA-256
`65f0c24b76c644ddd2a4eef8e772c4b881d7e5f7000cd4465b190edc3ee1dd50`. Le lot borne l'overlay et
l'audit de visibilité végétation. Le ledger runtime enregistre une route sans pause de 1 800 ticks en
29,997926 s, couvre 3 672 frames main-world, effectue 3 674 contrôles d'invariants et reçoit 1/1 au
démarrage puis 2/2 à la frontière finale.

La campagne externe `cadence-15635b3-final-20260902T030459251Z` est explicitement
**non qualifiante** : elle réutilise un target Cargo partagé et n'effectue aucun rebuild gouverné en
target propre. Séparément du ledger, l'hôte retient 719 lignes PresentMon du PID 2864 avec
`CPUStartQPC >= measurement_not_before_qpc`, sans borne haute issue du runtime. Trente-neuf lignes
n'ont pas de présentation résolue. Les 680 DisplayedTime résolus mesurent 44,2516 ms de moyenne,
p95 62,4861 ms, p99 76,4441 ms, maximum 83,3786 ms et 1 % low 12,5912 FPS. Ce 1 % low vaut
`1000 / moyenne des 7 DisplayedTime les plus lents`. 672/680 dépassent 16,7 ms et 600/680 dépassent
33,3 ms, avec séquences maximales de 87 et 64 présentations. Le diagnostic échoue donc aux seuils ;
`R1-048` reste ouverte.

Les temps CPU/GPU de pass, les statistiques de pipeline et le compte runtime des frames présentées
restent `not_observed`. `FrameTime` et `CPUBusy` proviennent de PresentMon externe ; `GPUTime` n'est
pas une instrumentation exhaustive de Forge. La source d'affichage est uniquement
`external_presentmon_displayed_time`. Le runtime conserve `runtime_verified_capture=false` et
`display_metrics_ingested=false` : sans borne haute runtime, la frontière hôte n'est ni vérifiée ni
ingérée et la qualification QPC exacte n'est pas revendiquée. La machine est
une RX 7800 XT/Vulkan de développement, pas la RTX 3060/RX 6600 cible, et aucune revue humaine n'a été
effectuée. Le protocole, les métriques et les SHA-256 sont consignés dans
[`R1_048_GAMEPLAY_STUTTER_PRESENTMON.md`](../evidence/gate1/R1_048_GAMEPLAY_STUTTER_PRESENTMON.md).

## Diagnostic opt-in `render-profile` `2fe48485`

Le commit `2fe48485086e925422c667f49c38a80f96e1a81a`, arbre
`378f13f68e7689a6790aa544f1e5b4a1be30ad16`, produit un runtime Release de 90 245 632 octets,
SHA-256 `e03bd61db8b5f8cf418390a5a78f3b5dc7b4e94dfb123ee6668abcf0d8e755cd`. La campagne externe
est conservée sous :

```text
E:\OpenRPG_Forge\.gate1-tools\presentmon\diagnostics\2fe48485-render-profile-20260902T060816358Z
```

Le marqueur `forge.nonqualifying-render-profile` schema 1 pose `qualifying=false` et porte le SHA-256
`5f365295110fca0a1f47c5419f05e6a1ff1c33d1fe29619a27f4c9f9c34b3e48`. La feature modifie les logs
et l'ordonnancement du rendu ; le target Cargo partagé est réutilisé et aucun rebuild gouverné en target
propre n'est effectué. La campagne entière est donc **NONQUALIFYING**.

Sur RX 7800 XT/Vulkan, pilote `26.8.1`, le ledger couvre 1 800 ticks en 29,995543 s, dont 2 ticks de
catch-up, 2 608 frames main-world et 2 609 contrôles d'invariants. Le mouvement gameplay est réel,
9/9 modèles sont chargés, le runtime sort avec le code 0 et ses logs comptent zéro warning et zéro
erreur. La revue humaine reste fausse. Ces comptes prouvent la route CPU/ECS ; ils ne sont pas des
frames présentées.

PresentMon retient 2 184 lignes après la borne QPC basse, sans borne haute runtime ni ingestion. Parmi
elles, 738 `DisplayedTime` sont non résolus et 1 446 sont résolus. Les pertes ETW sont nulles : zéro
événement et zéro buffer perdus.

| Mesure | Moyenne | p95 | p99 | Maximum |
|---|---:|---:|---:|---:|
| FrameTime, 2 184 valeurs | 13,8155 ms | 22,7558 ms | 50,3844 ms | 620,4266 ms |
| CPUBusy, 2 184 valeurs | 13,7076 ms | 22,6474 ms | 50,2758 ms | 620,3147 ms |
| GPUTime, 2 184 valeurs | 6,9942 ms | 11,3378 ms | 12,0886 ms | 13,0470 ms |
| DisplayedTime, 1 446 valeurs | 20,8579 ms | 34,7319 ms | 83,4934 ms | 625,0145 ms |

Le 1 % low est **4,1709 FPS**. Sur les 1 446 présentations résolues, **680** dépassent 16,667 ms et
**111** dépassent 33,333 ms. La moyenne, le p95, le 1 % low et le stutter échouent donc aux seuils de
Gate 1.

La trace interne de 6 073 239 151 octets, SHA-256
`79763e8306818b7b4f9ba36886a012c7b1cfee10afd830a2f92146b73084fb71`, est analysée deux fois sur
22 604 221 lignes par passe. Elle contient 9 148 749 paires `B/E` complètes, zéro anomalie et huit
scopes ouverts à la coupure, exclus. Les hashes de la synthèse JSON, de la synthèse Markdown, de la
preuve brute et du parseur sont respectivement
`5a35dff595fa02dee801ea8c99b5d7b857d558d8d9219580c6a7c9c2b044657e`,
`28287a840f16528833baa4d3466d8c21608b5f93d8ebe221a1ab24508eba73bc`,
`9e9ee27a8522e13aa049f79d3c780df99fec832303593db2dc9e51938a2512f7` et
`64d3c90f4977c4f4ab7bec86a4af819a370587f59f1b8f4eb4b4591f155ef657`.

La cadence stable interne vaut 11,445115 ms, soit 87,3735 FPS équivalents profilés, avec p95
12,621560 ms, p99 13,256662 ms et maximum 27,217700 ms. Le chemin stable `Render` vaut 10,845757 ms
inclusif et 6,514969 ms exclusif ; `Surface::get_current_texture` vaut 3,539034 ms exclusif,
`prepare_windows` 3,649687 ms inclusif, `main_render_schedule` 3,006376 ms et `Core3d` 1,738853 ms.
La queue `PostUpdate` atteint 4,676880 ms p95 exclusif. Les moyennes Vulkan brutes restent faibles :
0,077400 ms pour acquire, 0,049984 ms par submit et 0,143264 ms pour present.

Cette trace stable suit de près la distribution à faible interférence `8b`, mais elle mesure une
instrumentation et une frontière différentes. Elle ne remplace pas le **FAIL PresentMon**. La
divergence entre 11,445115 ms internes et 20,8579 ms affichées reste non réconciliée ; aucun correctif
n'est appliqué dans ce lot documentaire. `R1-037`, `R1-041` et `R1-048` restent ouvertes,
`human_review_performed=false`, Gate 1 reste ouverte et Gate 2 reste interdite.

## Baseline Release `6f0e519a` et diagnostic de readiness `06b5fd40`

La campagne contrebalancée `6f0e519a-foreground-f87b293b` exécute le binaire Release non instrumenté
de 82 328 064 octets, SHA-256
`c20c2b727b5103f677aaea0ea04984c214526e0455487f30865200d6974ece0a`. Les six runs `AB/BA/AB`
sont valides côté infrastructure, avec identité scène/GPU/OS/affichage concordante et signatures de
présentation stables. Les trois runs de baseline `AutoNoVsync` échouent tous :

| Mesure, unité par run | Minimum | Médiane | Maximum |
|---|---:|---:|---:|
| FPS affichés moyens | 21,3180 | 21,4731 | 21,5263 |
| p95 affiché | 69,4547 ms | 69,4566 ms | 69,4584 ms |
| 1 % low | 6,9036 FPS | 6,9289 FPS | 7,0982 FPS |
| présentations non affichées | 16 | 19 | 27 |
| présentations au-dessus de 33,3 ms | 546 | 554 | 556 |
| maximum consécutif au-dessus de 16,7 ms | 65 | 88 | 116 |

Les trois FIFO échouent aussi et ne sont strictement meilleurs que dans une paire sur trois. Le fichier
`presentation-ab-analysis.json`, SHA-256
`5026e75d1b47824a47381bd05ab8ae273111948b87d6ffbc804f0cecab81dd3e`, refuse donc toute
proposition : verdict **FAIL**, `qualifying=false`, `closes_gate=false`.

Le commit `06b5fd404bb4c3918b4b7ea154c8d7878609fa8d`, arbre
`a195b2b865c45502f3b412a43bfb20f4d53f9491`, ajoute exclusivement un reçu atomique de timeout
avant l'état prêt. Il conserve 1 200 ticks d'attente et ne change aucun critère ou schéma qualifiant. Le
binaire Release exact mesure 82 333 696 octets, SHA-256
`b1f0f17bb2499a798445e78d49dfb57db8182a2896dd36f13c48ed44360e60ab`; 2/2 tests Release
ciblés, Clippy Release strict, formatage et diff-check passent.

Le smoke réel DX12/FXC expire à 1 200 ticks : les huit prédicats autres que le pipeline sont vrais et
`pipeline_ready=false`, avec 40/69 pipelines prêts, 29 en création et zéro erreur. Le reçu
`forge.gate1-gameplay-stutter-readiness-timeout` v1, `qualifying=false`, porte le SHA-256
`77e96f54e5a4aa8ddfbb5d42cc20a079efcea47abb0851d3a59b2074d01c179b`. La cause du timeout
FXC est donc isolée à la compilation de pipelines.

Avec le DXC Microsoft signé du SDK Windows 10.0.26100.0, trois pilotes atteignent 69/69 pipelines
entre les ticks 209 et 294. Le PresentMon 2.4.1 épinglé ne produit toutefois aucune ligne et perd
jusqu'à 263 726 événements ETW ; une variante affichage seul perd 261 138 événements. Le PresentMon
signé livré par AMD reproduit alors zéro ligne et 58 122 événements perdus en cinq secondes. La cause
racine locale identifiée ensuite est la session PresentMon orpheline
`OpenRPGForge-Gate1-Diagnostic-85a698103aaf4186a480beabd1840099`, pas un défaut DX12 démontré.
Après son arrêt ciblé, l'acquisition 2.4.1 redevient complète avec ETW 0/0. Les essais antérieurs
restent invalides et ne sont pas requalifiés rétrospectivement. La route DXC statique locale n'est pas
promue : son link échoue sur `atls.lib`, composant ATL/MFC absent. Il n'existe donc aucune mesure de
performance DX12 recevable ; seule la disponibilité plus rapide des 69 pipelines avec DXC est établie.

Un prototype externe de PresentMon officiel `v2.4.1` / `43bd0333` double la taille de buffer ETW à
128 KiB et le minimum à 512, sans dépasser le maximum amont de 1 024. Le diff binaire hashé
`5041bead…3b0835` ne touche que deux fichiers. Son unique build x64 Release échoue avant le link,
faute des dépendances vcpkg `cli11`, `concurrentqueue`, `cereal` et `boost-asio`. Aucun exécutable ni
A/B de ce prototype n'est produit et rien n'est promu dans Forge. Cette piste buffers est supplantée
par l'identification et l'arrêt de la session concurrente.

L'instrumentation interne bornée `presentation-profile` issue de `2bf5e44f`/`e1e818fc` mesure des
spans applicatifs de préparation/acquisition/présentation. Ces valeurs décrivent la cadence des
demandes de présentation, pas `DisplayedTime`, la composition ou le scan-out ; elles restent
explicitement non qualifiantes et ne remplacent pas PresentMon.

## Preuve temporelle Browser exacte du candidat `4e476b31`

Le harnais préenregistré a exécuté Edge headed `152.0.4191.53` sur la route High/clear exacte, depuis
un worktree propre avant et après. La page reste `1920 x 1080` sans scrollbar globale ; le canvas
`1408 x 966` et son backing store sont stables entre les captures. L'identité embarquée correspond au
commit candidat qualifié `4e476b31ee33533b6f4023804d8ecd1be02f9c55` et à l'arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`.

Le verdict automatisé est **PASS** : 120/120 callbacks RAF, 3/3 ROIs végétation PASS, témoin terrain
PASS, 4/4 bundles servis byte-for-byte identiques à `dist`, zéro erreur/warning console et zéro échec
réseau. Les dix entrées du manifeste ont été revérifiées contre leurs tailles et SHA-256. Le ledger
principal porte le SHA-256
`c2b32eb15f7a05e67e3687fe3fdf7e128051dfb32460b6b6f4416875a581ff1b` et le manifeste
`3ec9f4372026cabc5d9b35ff67852c322d3213f354241bde49bbceadebf4f9bc`.

Les copies versionnées sont sous
[`browser/4e476b31ee33533b6f4023804d8ecd1be02f9c55/temporal`](../evidence/gate1/browser/4e476b31ee33533b6f4023804d8ecd1be02f9c55/temporal).
Les deux PNG ont été ouverts à leur résolution originale pendant la consolidation technique. Ils
montrent une variation temporelle dans les trois ROIs végétation, avec caméra et témoin terrain
stables. Le ledger conserve `human_temporal_inspection=required_not_performed_by_harness` : la route
est prête pour une revue humaine, mais `R1-014` et Gate 1 restent ouvertes. L'acquisition historique
`a4e9824a` reste versionnée séparément et intacte.

## Preuve Browser d9 clear/rain

Les artefacts locaux sont sous :

```text
E:\OpenRPG_Forge\.gate1-browser-smoke-artifacts\d9e2670111624aa836ab138afc35cddf441865ef
```

Le lot documentaire contient leurs copies byte-for-byte sous
`docs/evidence/gate1/browser/d9e2670111624aa836ab138afc35cddf441865ef/` et les deux PNG sous
`docs/evidence/gate1/captures/`. Leur cartographie complète est fournie par
[`R1_BROWSER_WEBGPU_SMOKE.md`](../evidence/gate1/R1_BROWSER_WEBGPU_SMOKE.md).

Le manifeste externe mesure 2 503 octets et porte le SHA-256
`c566fe7d3b8f36ea27e4f428301236e659488d3ed909c9d50ccf2ff26b2b50c7`. Les principaux fichiers :

| Artefact Browser | Taille | SHA-256 |
|---|---:|---|
| `validation.json` | 3 507 octets | `cd6b196e06ca1e02a970c94d47ed64a6aa613f017284cd92616d8040ee6a792f` |
| `visual-valley-clear-high.browser.png` | 1 293 570 octets | `819bd9994fafc8f4694d2e65300528bbf2d2b05f6e8946ae515305b58beb33d4` |
| `visual-valley-rain-high.browser.png` | 1 323 858 octets | `7703f0c4207052010343585d12b4e6bc94ca076114f6e39a4acf4cd10be76e5a` |
| ledger scène clear | 4 043 octets | `83093be5827ff053489a8636ba470b00d437ae928324af7ce06a65f37ba4383f` |
| ledger scène rain | 4 038 octets | `30716870ca853948657bf05f3167e43221cc377ef1d5c06e0322dda3063d32c3` |
| ledger frame clear | 1 771 octets | `1f606f2540a29b5d21c41188256d9464c9e8e6ee9ef565e363fd5cf383fd2e2f` |
| ledger frame rain | 1 796 octets | `d8c8829c6c1297f8d5ed12ad8584869878a9aa5780c987f1c9e07f4370d3d21f` |

### Résultats automatisés

- clear : **24/24 PASS**, zéro contrôle échoué ;
- rain : **24/24 PASS**, zéro contrôle échoué ;
- `selected_manifest == runtime_manifest` et `parity_status=source_manifest_matched` dans chaque run ;
- huit hashes de source présents et identiques entre sélection et observation Studio ;
- une caméra 3D active sur une attendue ;
- eau `observed_main_world_components`, météo `observed_cpu_projection` et tier High appliqué ;
- décor `activated`, 9/9 modèles chargés, 0 échec, 125 instances demandées, dont 99 remplacements et
  26 détails ;
- 26/26 requêtes d'assets répondent HTTP 200 pour chaque météo ;
- aucune requête `.meta`, aucun échec réseau, aucun warning/erreur console et aucun message DOM
  d'indisponibilité ou d'erreur runtime.

Les PNG ont une taille de 1920 × 1080 et capturent la page Browser complète. Le canvas Bevy observé
mesure 1 393 × 1 067 ; les chiffres Browser ne doivent donc pas être décrits comme un benchmark GPU
plein écran 1920 × 1080.

### Mesures de boucle WASM

| Mesure après 60 frames de warm-up | Clear | Rain |
|---|---:|---:|
| échantillons | 120 | 120 |
| durée | 1 214,1 ms | 1 147,6 ms |
| moyenne | 10,1175 ms | 9,5633 ms |
| p50 / p95 / p99 | 9,8 / 12,1 / 12,8 ms | 9,3 / 10,9 / 11,6 ms |
| FPS moyen | 98,839 | 104,566 |
| 1 % low | 77,519 FPS | 85,106 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| histogramme `<=16,7` / `16,7..33,3` / `>33,3` | 120 / 0 / 0 | 120 / 0 / 0 |

Ces mesures proviennent de la boucle d'update Bevy WASM, pas d'une timestamp query GPU. Le ledger
conserve `webgpu.status=checking`, `webgpu_ready_ms=null` et `gpu_presentation=not_observed`. Le PASS
automatisé valide le chargement, les invariants CPU/ECS, le réseau, le DOM, les captures et les
métriques de boucle ; il ne doit pas être renommé « présentation GPU WebGPU qualifiée ».

## Preuve native d9 RX 7800 XT exact-commit

Le binaire Release reconstruit et exécuté sur `d9e2670` utilise une AMD Radeon RX 7800 XT, Vulkan
et le pilote AMD `26.8.1 (LLPC)`, en 1920 × 1080 High fixe, VSync off. Le build Release termine en
3 min 24 s ; le binaire de 81 735 680 octets porte le SHA-256
`e44bc941b70eabab8b7e6155a262ab93491d9c2d4763454cb09bfd480e24cc75`. Les deux ledgers
`forge.runtime-metrics` v8 contiennent 120 échantillons après warm-up :

| Mesure native | Clear | Rain |
|---|---:|---:|
| moyenne | 8,3478 ms / 119,792 FPS | 8,2920 ms / 120,598 FPS |
| p95 | 8,7132 ms | 9,0950 ms |
| 1 % low | 113,775 FPS | 96,354 FPS |
| hitches `>16,7 ms` / `>33,3 ms` | 0 / 0 | 0 / 0 |
| mémoire wgpu allouée / réservée | 530 273 124 / 876 609 552 octets | 534 407 108 / 876 609 552 octets |
| pipelines prêts | 69/69 | 71/71 |

La fenêtre de mesure native dure environ 0,94 seconde à caméra fixe. Les valeurs mémoire décrivent
les sous-allocations wgpu, pas la VRAM physique totale. `gpu_draw_calls` reste `null` et aucun total
exhaustif de draw calls/triangles/instances GPU n'est disponible.

Les deux logs natifs d9 sont vides : aucun warning `wgpu_core::validation` ni `ERROR` n'y est
enregistré. Cette absence qualifie ces deux exécutions seulement ; elle n'est pas extrapolée à tous
les backends ou GPU.

Les commandes, 46 contrôles, six hashes natifs et limites détaillées sont consignés dans
[`R1_043_NATIVE_STUDIO_EXACT_COMMIT.md`](../evidence/gate1/R1_043_NATIVE_STUDIO_EXACT_COMMIT.md),
complété par
[`R1_034_043_PERFORMANCE_PARITY.md`](../evidence/gate1/R1_034_043_PERFORMANCE_PARITY.md).

## Provenance, CC0 et indépendance

Le rejeu qualifiant de provenance sur `4e476b31`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`, est **PASS automatisé local** :

- sous-ensemble Quaternius Stylized Nature MegaKit Standard 1.0, licence `CC0-1.0` ;
- 27 fichiers suivis, 9 modèles et 16 652 493 octets, tous déclarés et hashés ;
- 27/27 copies byte-for-byte depuis le coffre normalisé ;
- 507 fichiers Cadle hachés, 39 fichiers Forge canoniques comparés ;
- 0 copie exacte, 0 ligne normalisée commune d'au moins 40 caractères, 0 payload Cadle suivi et
  0 dépendance produit vers le clone ;
- l'acquisition épinglée et Gate C repassent avec `CADLE_ACQUISITION_PASS` puis
  `CADLE_GATE_C_BOUNDARY_PASS tracked=396 checkout_present=True` ;
- Visual Valley fonctionne sans clone Cadle produit ; le checkout ignoré n'est qu'une référence de
  comparaison en lecture seule. La preuve JSON externe porte le SHA-256
  `7d28d2df849a3243773b92993f4876280088c4fdb967fd33923a5255374923c6`.

Cela qualifie `R1-053`, `R1-054` et la condition « code sans dépendance propriétaire obligatoire ».
Cela ne constitue ni une expertise juridique exhaustive, ni une revue artistique, ni une preuve GPU.
Voir [`R1_053_054_PROVENANCE_INDEPENDENCE.md`](../evidence/gate1/R1_053_054_PROVENANCE_INDEPENDENCE.md)
et [`R1_CC0_SCENERY_OVERLAY.md`](../evidence/gate1/R1_CC0_SCENERY_OVERLAY.md).

## R1-033 — N/A conditionnel au snapshot courant

Le contrôle documentaire automatisé exact `28985328`, arbre `ec71501d`, établit que les neuf glTF
gouvernés correspondent aux neuf modèles du manifeste et totalisent **0 animation**. Les seules
catégories déclarées sont `tree`, `shrub`, `fern`, `rock` et `detail` : aucun personnage ou joueur
n'est disponible. Le joueur courant est construit sans asset avec `Capsule3d::new`, sous le nom
`Forge player capsule`, et son adapter ne référence aucune API d'animation Bevy.

La condition « si asset disponible » de `R1-033` est donc fausse pour cette tranche. La route est
fermée **N/A conditionnel**, et non PASS comme implémentation d'une animation. Elle se rouvre dès
l'introduction d'un asset de personnage animé gouverné ou le remplacement de la capsule procédurale.
La commande, les neuf résultats et les assertions de réouverture sont versionnés dans
[`R1_033_PLAYER_ANIMATION_NA.md`](../evidence/gate1/R1_033_PLAYER_ANIMATION_NA.md).

## État conservateur des routes

Nouvelles cases fermées par les preuves consolidées :

- `R1-019`, `R1-020`, `R1-021`, `R1-022` et `R1-024` ;
- `R1-035`, `R1-036`, `R1-038` et `R1-042` ;
- `R1-039` et `R1-040` par la matrice native exacte 10/10 et la transition High vers Ultra ;
- `R1-034` par l'overlay CPU/GPU exact `afb270c3` et ses deux captures lisibles ;
- `R1-043` pour la paire native/Studio exact-commit et les sept hashes communs concordants ;
- `R1-044` et `R1-045` pour l'acquisition de captures fixes ;
- `R1-053` et `R1-054` pour provenance et indépendance ;
- `R1-033` comme **N/A conditionnel**, faute d'asset joueur animé gouverné dans le snapshot courant ;
- `R1-055` par le présent rapport, avec verdict « poursuivre Gate 1 ».

Restent explicitement ouvertes, soit **13 routes** :

- `R1-014`, `R1-023`, `R1-025` et `R1-026` ;
- `R1-037`, `R1-041` et `R1-048` ;
- `R1-046`, `R1-047` et `R1-049` à `R1-052`.

### Paquet de revue humaine fail-closed

Le manifeste v1 sépare la revue humaine des ledgers automatisés, qui restent intacts avec
`human_review_performed=false`. Il est rattaché à la base technique qualifiée
`4e476b31ee33533b6f4023804d8ecd1be02f9c55`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`, et recense 27 références : douze preuves Forge
versionnées, neuf preuves Forge externes hashées, trois observations Grassworks externes et trois
captures Cadle consultées uniquement in situ. Le champ
`prepared_from.commit=73db783975251cf7b4439dce9a12328a4c55c5ec` désigne le dernier snapshot
Git existant contenant les douze artefacts suivis référencés ; il ne prétend pas contenir la présente
révision encore non committée du paquet. Le validateur confirme `artifacts=27`, `evidence_sets=6`,
`exact_baseline_visual_artifacts=2`, `human_routes=11`, `review_ready=7`,
`external_verified=true`, `cadle_assets_embedded=0` et `routes_closed=0`; ses deux cas positifs
synthétiques et ses dix-neuf refus attendus passent, soit vingt-et-un cas au total.

- prêtes à la revue humaine : `R1-014`, `R1-046`, `R1-047`, `R1-049`, `R1-050`, `R1-051`, `R1-052` ;
- attribution GPU spécifique au matériau eau, consommation shader de la wetness et revue humaine
  requises : `R1-023`, `R1-025`, `R1-026` ; la matrice Browser automatisée `534ce4fa` est acquise,
  mais ses captures et diagnostics CPU/ECS ne satisfont pas ces critères ;
- qualification quantitative PresentMon différée sous ADR-0010 ; vidéo et revue humaine temps réel
  toujours requises : `R1-048`. Le paquet historique reste inchangé, sans faux reçu humain ;
- identité de la référence WebGPU `R1-051` fortement établie mais source non figée : le message
  utilisateur historique du 29 août 2026 cite le post X `2093382978105966827` de Bilal Khan
  (`@Bk23544`), relié publiquement à **Three.js Grassworks**, à
  `https://grassworks.techredux.co/demo` et au walkthrough YouTube `Nhim18rc-XE`. L'audit Git initial
  n'avait pas accès à cette provenance conversationnelle. Le protocole sémantique v1 est introduit
  au commit exact `73db783975251cf7b4439dce9a12328a4c55c5ec`, arbre
  `466cb51b12203e2358aec7ebcc9051eb58a1234e`, depuis la base interne `48038854`; son SHA-256
  `cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d`, relie maintenant trois JPEG
  Grassworks externes et quatre PNG Forge `125ba0b`. Les sept tailles, hashes, signatures et dimensions
  passent in situ ; quatre panneaux couvrent densité, naturel, profondeur, lumière et finition, et
  24 cas synthétiques vérifient ses refus fail-closed. Le rendu actuel du post historique reste un
  contexte d'identité exclu du verdict. Le paquet humain lie désormais explicitement ce commit, ce
  hash et les sept références, puis impose cinq observations d'axe et une reconnaissance des limites
  de source. `R1-051` reste **OUVERTE** : live mutable,
  code privé, aucun commit/version public, capture locale historique, hash du média déclencheur ou
  verdict humain. Deux captures live courantes Sunny/Rain et un
  rendu actuel du post X historique avec frame vidéo sont figés et hashés hors dépôt, sans constituer
  une capture contemporaine du 29 août ni le média source. Aucun média ou asset externe n'est
  embarqué ; `human_review_performed=false`, `gate1_transition_authorized=false`, acquisition source
  autorisée si requise et revue humaine encore due ;
  [reprise](../evidence/gate1/R1_051_WEBGPU_REFERENCE_RECOVERY.md),
  [protocole](../evidence/gate1/R1_051_GRASSWORKS_FORGE_SEMANTIC_PROTOCOL.md) ;
- protocole A/B Forge/Cadle aligné acquis et validé ; verdict humain versionné encore requis :
  `R1-052`.

`R1-037` et `R1-041` restent volontairement hors du reçu humain : la première attend la couche Vulkan
RenderDoc et quatre captures indépendantes ; la seconde attend le GPU cible. Le guide
[`GATE1_HUMAN_REVIEW_PACKET.md`](../evidence/gate1/GATE1_HUMAN_REVIEW_PACKET.md) décrit le contrôle et
le reçu séparé. Même accepté humainement, ce reçu ne peut ni muter les ledgers machine, ni autoriser la
transition Gate 1.

`R1-023` et `R1-037` ont désormais du code intégré et des validations ciblées dans la chaîne courante,
sans changer leur état : la matrice Browser Low/High `534ce4fa` est PASS automatisé, mais n'est pas une
attribution GPU au matériau eau ; la consommation shader de la wetness n'est pas prouvée, la revue
humaine de l'eau reste due et les quatre captures RenderDoc finales ne sont pas acquises. Une route ouverte avec une implémentation
validée seulement sous une portée plus étroite reste une route ouverte.

Les fichiers `R1-046` à `R1-048` sont réacquis au commit exact `125ba0b`. Le principal défaut
depth-only reste corrigé, les vues eau ciblées sont inspectables et la vidéo exerce le contrôleur
gameplay réel. À `15635b3f`, le ledger
runtime et la première sélection PresentMon hôte sont deux observations historiques distinctes ; la
seconde n'a pas de borne haute runtime, n'est ni vérifiée ni ingérée, et échoue aux seuils.

Le diagnostic historique `2fe48485` confirme une route gameplay réelle, mais sa sélection PresentMon
retient 1 446 présentations résolues sur 2 184 lignes après la borne basse et échoue encore : moyenne
20,8579 ms, p95 34,7319 ms, 1 % low 4,1709 FPS, 680 dépassements de 16,667 ms et 111 de 33,333 ms. La
trace interne stable à 11,445115 ms, proche de `8b`, reste NONQUALIFYING et ne remplace pas ce FAIL.
La baseline historique `6f0e519a` conserve son FAIL sur trois runs Auto valides, tandis que le
diagnostic `06b5fd40` isole le timeout DX12/FXC. Les essais DX12/DXC historiques restent invalides,
mais la session ETW orpheline explique désormais leur acquisition défaillante. Le run gouverné
schema 7 `9db35a8e` remplit les seuils temporels locaux et la résidence continue. Les deux replays
schema 8 exacts `49acfd56` sont maintenant acquis, mais aucun n'est PASS : Replay1 perd le focus et
Replay2 conserve un outlier de 35,3837 ms. Les PASS A5/A6 restent diagnostiques non qualifiants sur le
binaire partagé `21228a…`. Le rejeu suivant `1912806` est un ABORT avant signal sur un gap de sampling
fenêtre `>250 ms` ; il conserve ready + PresentMon ETW 0/0 mais n'ajoute aucune métrique qualifiante.
Les tentatives schema 12 suivantes s'arrêtent avant ready sur le validateur qualité périmé à
`9d19cde5`, puis avant signal sur un flux PresentMon vide à `4b283182` et de nouveau à `2ced195b`.
Un smoke non qualifiant confirme ensuite l'écran actif, puis `83f02928` reçoit 25 lignes Forge ETW
0/0 avant d'abandonner sans signal sur le gap final `row→sample` de 743,6604 ms. Les trois derniers
replays prouvent la readiness corrigée `5/16/13`, pas la cadence ; le durcissement committé exact
`473abeaac1425f0fb991f88ba0495d3708978ea1`, qui rejoue ce gap dans le budget pré-signal existant,
est validé statiquement mais n'a pas été rejoué.
« Acquis » ne signifie pas « qualifié » et
`human_review_performed=false`.

`R1-043` est fermée : les deux cibles utilisent `d9e2670`, le scenery hash concorde et
46/46 assertions croisées passent. Le ledger natif n'expose pas le hash autonome du contrôleur
présent dans le manifeste Studio ; cette limite est documentée et ne doit pas être transformée en un
huitième match de ledger.

## Conditions de passage Gate 1

| Condition | État | Motif |
|---|---|---|
| qualité visuelle clairement supérieure à la base Godot | **OUVERTE** | défaut depth-only corrigé et conservé, mais aucune revue humaine comparative acceptée |
| impact au moins comparable à Cadle | **OUVERTE — PROTOCOLE PRÊT** | protocole A/B aligné acquis et fail-closed ; verdict humain encore dû |
| 60 FPS moyens en 1080p High sur GPU milieu de gamme | **DIFFÉRÉE — ADR-0010** | G1-PERF-001, GPU cible non qualifié ; dernier replay local schema 13 : 26,1661 FPS |
| p95 `<=16,7 ms` | **DIFFÉRÉE — ADR-0010** | G1-PERF-002 ; dernier replay local : 131,956 ms, FAIL inchangé |
| 1 % low `>=45 FPS` | **DIFFÉRÉE — ADR-0010** | G1-PERF-003 ; dernier replay local : 3,4183 FPS, FAIL inchangé |
| absence de stutter récurrent | **QUALIFICATION QUANTITATIVE DIFFÉRÉE — ADR-0010** | G1-PERF-004 ; dernier replay schema 13 FAIL, 279 intervalles >33,3 ms ; revue humaine de jouabilité non dérogée |
| profils qualité fonctionnels | **SATISFAITE** | quatre tiers fixes, clear/rain, adaptation High vers Ultra et benchmark High sur 10/10 ledgers natifs |
| parité de données native/Studio | **SATISFAITE** | commit/arbre exacts communs, 46/46 contrôles et sept hashes communs MATCH |
| code sans dépendance propriétaire obligatoire | **SATISFAITE** | audits provenance/indépendance locaux PASS |
| rapport et captures versionnés | **SATISFAITE** | preuves antérieures préservées ; baseline `6f0e519a`, reçu `06b5fd40`, essais DXC/ETW et leurs hashes consignés ; bruts diagnostiques non versionnés |

## Limites visuelles et qualification humaine

Les captures d9 corrigent nettement la palette rose/moutarde. Le lot initial `a7c9b1e1` rendait les
défauts restants explicites ; le lot `6a24cabe` supprime ensuite les grandes silhouettes/stries
bleu-noir depth-only sur les vues lointaine, humaine, eau et mouvement. Le lot `873618af` conserve la
correction et ajoute le vrai contrôleur/Rapier. La campagne `125ba0b` réacquiert 29 PNG uniques, dont
trois vues eau clair/pluie/retour clair inspectables, et un MP4. La pluie, le changement de wetness et
la surface d'eau sont visibles, mais les pixels seuls ne prouvent pas l'exécution GPU du fallback
Environment IBL ou de `ScreenSpaceTransmission` ; le retour CPU/ECS exact au profil clair n'est pas
une preuve d'identité GPU.

Les acquisitions Low/High `41b49f66` ajoutent bien des mesures des passes de scène attendues aux trois
checkpoints eau, mais leur lecture retardée ne possède pas d'identité de frame de rendu et leur portée
englobe toute la scène. Elles ne peuvent donc pas être réinterprétées comme preuve du draw/matériau eau
ou de la consommation de wetness par le shader.

La matrice Browser `534ce4fa` ajoute six checkpoints et douze captures exactes Low/High dans deux
instances séparées, chacune conservée sur `clear → rain → clear`. Elle confirme les endpoints CPU/ECS,
la présence attendue de Bloom et du color grading et des captures de pixels via CDP, mais conserve
`gpu_presentation=not_observed`, `human_review_performed=false` et les trois routes ouvertes.

Aucune affirmation de supériorité sur Godot ou de parité visuelle Cadle n'est acquise. Les vues
hauteur humaine/lointaine et le MP4 corrigés sont acquis mais non qualifiés. Les runs PresentMon
`9db35a8e` puis Replay2 `49acfd56` montrent une cadence locale élevée sur RX 7800 XT sans fournir la
revue visuelle ni le GPU cible ; Replay2 reste en outre FAIL sur son unique outlier. Les essais
DX12/DXC historiques restent invalides ; leur panne ETW est attribuée à la session orpheline, pas à
une métrique produit. La revue humaine FHD/QHD et les comparaisons matériaux/composition/A-B restent
dues. Aucun asset Cadle n'a été ajouté à Forge.

## Exception CI distante ADR-0006

Le [run d'origine de l'exception `33335683606`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33335683606)
est attaché au commit `7286740c18fe9afa6cc207e388d5f335495fc650`. GitHub affiche la conclusion
`failure`, mais aucun runner n'a été alloué et les quatre jobs ont une liste d'étapes vide :

| Job | ID GitHub | Conclusion affichée | Étapes exécutées | Qualification Forge |
|---|---:|---|---:|---|
| `native (windows-2025)` | `99322040255` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99322040408` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99322040420` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99322040421` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Le [run ultérieur `33584774630`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33584774630),
attaché à `15635b3f85b623654e498b17ed299674928313e6`, reproduit la même frontière avec quatre jobs à zéro
étape. Le [run exact du commit technique qualifié `33731403805`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33731403805)
est attaché à `4e476b31ee33533b6f4023804d8ecd1be02f9c55`. Il est également affiché `failure`, mais ses quatre
jobs ont zéro étape et l'annotation GitHub indique qu'ils n'ont pas démarré à cause des paiements ou du
plafond de dépense :

| Job du commit qualifié | ID GitHub | Conclusion affichée | Étapes exécutées | Qualification Forge |
|---|---:|---|---:|---|
| `wasm` | `100571949306` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `100571949502` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `100571949607` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `100571949654` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Ce run est attaché au commit qualifié, mais il n'a exécuté aucun test et n'a qualifié aucun octet de
ce commit. Le commit qualifié est `4e476b31ee33533b6f4023804d8ecd1be02f9c55`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90` ; il a été rejoué dans le clone de qualification
`Rust` propre, aligné sur `origin/Rust` avec une divergence `0/0`. Le commit documentaire qui
versionne ce rapport ne constitue pas un nouveau candidat logiciel et n'est pas présenté comme rejoué.

Les commandes locales de substitution exécutées dans le clone de qualification propre sont :

```powershell
$env:CARGO_BUILD_JOBS = '1'
pwsh -NoLogo -NoProfile -File .\scripts\Test-Rust.ps1
pwsh -NoLogo -NoProfile -File .\scripts\Get-Cadle.ps1 -RepositoryRoot .
pwsh -NoLogo -NoProfile -File .\scripts\Test-CadleGateC.ps1 -RepositoryRoot . -RequireCheckout
pwsh -NoLogo -NoProfile -File .\scripts\Test-Gate1Provenance.ps1 -Mode SelfTest -RepositoryRoot .
pwsh -NoLogo -NoProfile -File .\scripts\Test-Gate1Provenance.ps1 -Mode Audit -RepositoryRoot . `
  -RequireCleanTree -RequireAssetManifest -CadleReferenceRoot .\upstream\cadle `
  -OutputPath E:\OpenRPG_Forge\.gate1-nongpu-proof-4e476b31\gate1-provenance.json
pwsh -NoLogo -NoProfile -File .\scripts\Test-Gate1BrowserTemporal.ps1
pwsh -NoLogo -NoProfile -File .\scripts\Test-Gate1PresentationAB.ps1 -SelfTest
python -S -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -v
```

Toutes retournent le code `0`. Le portail se termine par
`FORGE_RUST_VALIDATION_PASS wasm=True supply_chain=True`; Gate C et la provenance se terminent par
`CADLE_GATE_C_BOUNDARY_PASS tracked=396 checkout_present=True` et
`FORGE_GATE1_PROVENANCE_AUDIT_PASS manifests=1 files=27 models=9`. Les substitutions locales ciblées
consignées dans la table d'identités passent aussi : Clippy strict moteur/runtime, 141/141 tests
bibliothèque moteur, 69/69 tests runtime, 4/4 tests du binaire de garde, 4/4 tests Rust RenderDoc et
22/22 tests Python RenderDoc.

Hashes des artefacts réellement utilisés pour la décision :

| Artefact | Octets | SHA-256 | Portée |
|---|---:|---|---|
| bundle qualifié `forge_studio_bg.wasm` | 39 330 367 | `a81b41acef355eb5682b016894af7037e86c2771fbb095c0031bf0a9b384d060` | Trunk Release `4e476b31` |
| glue qualifié `forge_studio.js` | 184 652 | `97cd2b67f328d34643a0e7adefabc0a027f393c536b22ce8b241ebfbecc0075e` | Trunk Release `4e476b31` |
| preuve provenance qualifiée | 915 | `7d28d2df849a3243773b92993f4876280088c4fdb967fd33923a5255374923c6` | Gate C / `R1-053` / `R1-054` |
| binaire natif Replay2 schema 8 | 82 333 696 | `0c220f6980457a427dcf4f48c1204da07120fddc265c97e6ed61da2326a75a85` | dernière cadence gouvernée, FAIL strict |
| validation Replay2 schema 8 | 16 111 | `45b87d0d78570c0f6d0e541df040aa008ae49e2ca47b5d02002f223ea50894d1` | verdict cadence FAIL |
| CSV PresentMon Replay2 | 780 640 | `5d810261815fe74d0e556f638bd704ac94119667fa4868aa57716320a124ec22` | données de cadence |
| ledger runtime Replay2 | 53 240 | `88cd702163b3094d22a54fb352db6c75aa742c4cfde0919c86e331c76494605b` | identité et frontière runtime |
| reçu prêt replay schema 12 `83f02928` | 26 804 | `29cfbbee968c1877e0d4f4c6db2ed9e9345860728a10b431ba225b777186379b` | readiness 69/69 avant ABORT |
| stdout PresentMon replay schema 12 `83f02928` | 5 411 | `248a9d0fbdce25f7de8ff4b52dd47b883a56e571a331f7fed4ebc47f462324ab` | 25 lignes Forge, ETW 0/0, non qualifiant |
| reçu d'abandon replay schema 12 `83f02928` | 2 029 | `7db870e2d8050f5177d59d75a270f8ffdfeca744f7ff51980fbc1877205a292f` | frontière finale 743,6604 ms >250 ms, aucun verdict cadence |
| vidéo native eau | 807 168 | `29a5db4cd1f86911d3b43662c70147585eafcbee83ed96b4b8b10b60a2f1b226` | capture automatisée, non revue humainement |
| manifeste Browser historique | 2 503 | `c566fe7d3b8f36ea27e4f428301236e659488d3ed909c9d50ccf2ff26b2b50c7` | acquisition réelle `d9e2670` |
| capture Browser clear | 1 293 570 | `819bd9994fafc8f4694d2e65300528bbf2d2b05f6e8946ae515305b58beb33d4` | acquisition réelle `d9e2670` |
| capture Browser rain | 1 323 858 | `7703f0c4207052010343585d12b4e6bc94ca076114f6e39a4acf4cd10be76e5a` | acquisition réelle `d9e2670` |
| ledger matrice Browser Low/High | 42 460 | `aae5df2efe336e2521d0d9a19a9c77be4484fe82b5a5a6869f2008a4c33c021a` | acquisition externe `534ce4fa`, PASS automatisé borné |
| manifeste matrice Browser Low/High | 5 163 | `40952f814fde7b433a9b443a8157cb20f3bfb59bcfd9bfd044b72aa5dc1e1267` | 28 descripteurs ; manifeste lui-même exclu |
| inventaire canonique matrice Browser | — | `c43c9ef797f47138d29cc437646d5fae311eeceea1cf99b5b5df3bd9dfece5d9` | hash de 29 lignes `relative|bytes|sha256`, hors Git |
| reçu de refus RenderDoc | 351 | `e99fea9e92de493d0c85a3bb1f57d066b29428333509a702b3fa658076eb3b26` | absence de couche Vulkan enregistrée |

Les deux sorties Trunk qualifiées sont conservées localement dans le `dist` ignoré du clone de
qualification ; leurs octets ne sont pas versionnés. Aucun transcript fichier du portail complet
n'est conservé, donc seul son résultat terminal et les artefacts produits sont consignés ici.

La qualification workspace complète du commit et de l'arbre ci-dessus passe localement. RustSec
rapporte zéro vulnérabilité, 20 avertissements et conserve le blocker de distribution connu
`RUSTSEC-2024-0429` pour `glib 0.18.5` ; ce blocker n'est ni supprimé ni présenté comme résolu.
ADR-0006 autorise une dérogation administrative bornée jusqu'à Gate 4 ; elle ne transforme pas la CI
en PASS, ne ferme aucune des 13 routes et n'efface aucune dette locale, visuelle, GPU ou humaine. Les
risques Linux distant et WASM distant restent ouverts : les validations locales Windows et WASM ne
prouvent aucun runner GitHub.

- tests locaux automatisés = PASS ;
- CI distante Windows = NOT RUN — infrastructure billing ;
- CI distante Linux = NOT RUN — infrastructure billing ;
- CI distante WASM = NOT RUN — infrastructure billing ;
- CI distante supply-chain = NOT RUN — infrastructure billing.

Cette exception expire et la condition CI se rouvre dès qu'un runner exécute une étape, que le blocage
de paiement ou de plafond disparaît, qu'un runner distant approuvé devient disponible, que le workflow,
ses images ou ses commandes changent substantiellement, que la roadmap dépasse Gate 4, ou qu'une
distribution, un installateur, une release publique ou une qualification multiplateforme est préparé.
Le commit candidat de gate devra alors passer réellement les quatre jobs distants.

## Verdict et travail suivant

Verdict : **POURSUIVRE GATE 1 HORS AUDITS DE CADENCE**, sous ADR-0010.

La synthèse historique qui suit conserve les anciens résultats, mais n'est plus une prescription de
replay : le dernier verdict est désormais le FAIL schema 13 `61713887` documenté en tête du rapport.

La capture schema 7 `9db35a8e` reste une preuve historique intacte. ADR-0008/schema 8 a désormais deux
replays gouvernés exacts à `49acfd56` : Replay1 est confondu par 182/275 échantillons hors premier plan ;
Replay2 passe focus, résidence et ETW, mais échoue sur un unique `DisplayedTime` de 35,3837 ms. Les
diagnostics A5/A6 PASS ne sont pas qualifiants parce qu'ils réutilisent le target et le binaire
`21228a…`. Le rejeu schema 11 `1912806` suivant est abandonné avant signal pour un gap de sampling
fenêtre `>250 ms` ; il ne remplace pas ce verdict. Le harnais `9b847e99` expose désormais host boundary
schema 5 / validation schema 12 avec restabilisation pré-signal bornée. `9d19cde5` révèle ensuite le
décompte qualité périmé ; `4b283182` le corrige, passe ses tests et franchit la readiness réelle, mais
son replay s'arrête avant signal faute de flux PresentMon. `2ced195b` reproduit exactement cet ABORT
après rebuild propre. Le smoke non qualifiant `41b49f66` confirme ensuite un écran actif capable de
produire des lignes Forge. Le replay propre `83f02928` reçoit 25 lignes Forge ETW 0/0, mais abandonne
avant signal sur la frontière finale `row→sample` de 743,6604 ms. Le durcissement committé
`473abeaac1425f0fb991f88ba0495d3708978ea1` replace ce contrôle avant l'acceptation et rejoue un gap
>250 ms dans le budget inchangé de trois tentatives et 15 secondes ; ses tests légers passent, mais
ce commit n'a pas été rejoué. Aucun replay schema 12 qualifiant
n'existe.
Les acquisitions Low/High `41b49f66` ajoutent des mesures retardées de passes de scène et la matrice
Browser `534ce4fa` ajoute ses pixels/checkpoints automatisés, toujours sans attribution à l'eau ni
identité de frame exacte. `R1-023`, `R1-025`, `R1-026`, `R1-037`, `R1-041` et `R1-048`
restent ouvertes,
`human_review_performed=false`, et le travail ci-dessous reste futur.

Travail restant non couvert par le report :

1. qualifier les effets eau/wetness/post-traitement déjà intégrés, avec les preuves d'exécution GPU
   encore absentes, puis traiter les défauts fonctionnels ou visuels constatés ;
2. obtenir les quatre captures RenderDoc d'ADR-0009, après autorisation explicite séparée de la mutation
   hôte ; aucune modification de registre n'est autorisée par le report de performance ;
3. recueillir les revues humaines des vues corrigées, du déplacement et des comparaisons visuelles ;
4. décider du passage de Gate 1 avec distinction entre preuves acquises et dettes différées.

Le benchmark cible et les seuils stricts de cadence restent dans `G1-PERF-001` à `004`, sans campagne
active ni relance automatique. Aucun seuil, test ou ledger n'est assoupli. Le présent lot documentaire
est vérifié par diff-check, liens de décision, décomptes de roadmap et hashes des six artefacts R1-048 ;
il ne revendique aucun nouveau build ni test runtime.

Vérification du lot ADR-0010 le 3 septembre : **PASS** pour `git diff --check`, huit liens de
décision/preuve, six tailles/SHA-256 d'artefacts, décompte inchangé 42/55 routes et absence de modification
de code. `Test-CadleGateC.ps1` passe avec le checkout Cadle exact et intact, `tracked=439` avant ajout
du nouvel ADR à l'index ; aucun média ajouté. Aucun processus Cargo, rustc, runtime Forge ou PresentMon
n'est observé au contrôle final local. Ces contrôles documentaires ne requalifient pas le produit.

Tant que ces éléments ne sont pas acquis, Gate 1 reste **OUVERTE** et Gate 2 est **INTERDITE** sans
nouvelle dérogation explicite acceptée.
