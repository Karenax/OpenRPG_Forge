# Gate 3 — préparation de clôture, 13 octobre 2026

Décision du propriétaire, reçue le 13 octobre 2026 : **« Terminer les mises à jour
du rendu, puis qualifier »**. Les campagnes de qualification restent différées
jusqu'à la fin de ce chantier, puis seront exécutées sur les inputs communs
stables. L'objectif de clôture reste à atteindre ; aucune dérogation au garde source
ni copie du produit n'est introduite. Les diagnostics indépendants conservent
leur portée propre.

État des reçus lus : 13 octobre 2026, 21:52 UTC. Le contrôle natif
`closure-common-003` est terminé avec succès. Les derniers correctifs Studio,
runner, driver et rendu ont ensuite changé les inputs. Le contrôle natif004 a
échoué ; le build debug002 et le parcours diagnostic A ont réussi sur une autre
empreinte inchangée. Le module de tests a ensuite été déplacé mécaniquement.
D006 a terminé en erreur après saturation du journal d'observations. Les trois
correctifs moteur sont appliqués et inventoriés ; native007 a réussi ses étapes
Cargo mais finit refusé par le garde source après un changement de README.
Native008 est terminé PASS : format/diff/Clippy/tests exit 0, sources inchangées.
Supply008, les 34 replays privés, le supplément CPU de quatre contrats et la
lecture des 65 contrats headless sont PASS sur cette même empreinte. WASM008
finit refusé par son garde source malgré ses compilations et posthooks exit 0.
Le raccord de mesures opt-in est appliqué et gelé ; native009 a réussi
ses étapes Cargo mais son garde source a refusé un changement externe d'exemple.
Native010 est PASS, sources inchangées, avec 2 143 tests réussis ; supply010,
les 34 replays privés, le supplément de quatre contrats et les 65 contrats
headless sont PASS sur ses mêmes pins. Un nouveau delta externe dans l'exemple
de draw a ensuite changé la source : ces PASS010 sont historiques sur `7e4f0da5…`.
WASM010 finit FAILED de garde malgré ses étapes exit 0 ; la campagne Release010
de qualification n'a pas été lancée. Les anciens reçus gardent leurs identités.
Native011 est ensuite PASS sur `6a6a7ca3…`, avant un nouveau changement de
README. Native012 réussit ses quatre étapes Cargo mais finit FAILED de garde
sur l'exemple externe et un nouveau sous-module. Supply011/012 restent des
PASS historiques sur leurs propres empreintes. WASM011 finit aussi FAILED
de garde malgré ses deux étapes exit 0.
Le helper SDK compilé finit refusé pour changement de sources avant toute
inspection API ; P02 public géré et caméra sont préparés, sans acquisition réelle.
Ces résultats conservent leurs identités propres et ne constituent pas une clôture.
Le diagnostic du jeu Release mis en cache a depuis quitté normalement avant
readiness, avec un rapport vide et sans marqueur terminal ; sa cause reste à établir.
Le correctif d'ordre des bilans de sortie a réussi ses trois tests CPU ciblés ;
Clippy lib/tests ciblé est aussi PASS. Il reste à intégrer aux contrôles et à
l'exécutable Release de la campagne finale.

**Gate 3 reste ouverte.** Le propriétaire a accepté la boucle RPG dans le jeu
séparé et son parcours créé depuis Studio. Le raccord du noyau RPG est maintenant
implémenté dans les sources communes ; une première acquisition native debug
atteste certaines observations RPG/P01. Les campagnes Release et la qualification
GPU restent à produire. Les contrôles machine et la condition CI restent
distincts de cette acceptation.

Ce document est une matrice de préparation, sans verdict de clôture, publication
Git ni ouverture de Gate 4. Il complète
[l'acceptation propriétaire](GATE3_OWNER_RPG_ACCEPTANCE_20261013.md),
[la version commune](GATE3_SHARED_VERSION_20261011.md) et
[l'audit CI](GATE3_CI_READINESS_20261013.md).

## Révision et reçus

Checkout unique : `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche `Rust`, HEAD
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. Les modifications locales et les
fichiers non suivis font partie du candidat ; HEAD seul ne désigne pas ses sources.

L'empreinte testée de `closure-common-003` est
`094a8833b878e95692ceee19b557589e184e0dd541adee1cbc8277059d306545`.
Le manifeste source SHA-256 est
`4ae47c15f7fd45597ddbc59b95b1ee8374a763c2a9f2abaf2ccb3f9abac7c484`.
Le [reçu natif complet](../../../../.gate3-evidence/20261013/closure-common-003/native-result.json)
atteste `source_unchanged=true`, 2 130 tests réussis, zéro échec, 56 ignorés et
222 suites terminées. Ce PASS précède les derniers correctifs Studio/runner/driver.

Le candidat de départ du contrôle supply `closure-common-004` et du build debug
lancé à 16:51 UTC porte l'empreinte
`667252aa47e87683558dcb3aaa780ed46da75f99e65f51f445671bff603e9aa8`.
Son [manifeste supply](../../../../.gate3-evidence/20261013/closure-common-004/supply-source-manifest.json)
a pour SHA-256 `25a595a331b3261b1cf30fbb8798d932171d050ca78486da28f1e9869daffbb5`.
Le contrôle natif `closure-common-004`, commencé à 16:56 UTC, inventorie déjà
une autre empreinte :
`5505eaadf6aef31fc35409faf5a91a6496dc44073461a2cb0e27ae7c971ed3e7`,
[manifeste natif](../../../../.gate3-evidence/20261013/closure-common-004/native-source-manifest.json)
SHA-256 `e65e85792c98d31d001589ff6efd9324a37542bae229e431537f69b43ca2122c`.
Trois inputs diffèrent entre ces deux acquisitions : `source_graph.rs`,
`source_graph_state_buffer.rs` et `source_unlit_forward.rs`. Quatre inputs ont
ensuite changé pendant native004 : `crates/forge_engine/Cargo.toml`,
`examples/verify_source_graph_state_binding.rs`, `source_graph_state_buffer.rs`
et `source_unlit_forward.rs`. Son [reçu final](../../../../.gate3-evidence/20261013/closure-common-004/native-result.json)
est `FAILED`, `source_unchanged=false` ; ses PASS intermédiaires ne qualifient
pas le candidat courant. Le PASS supply reste attaché à sa propre empreinte.
Les changements de rendu concurrents sont conservés.

Le [build debug002](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-002/debug-build-receipt.json),
terminé à 17:25 UTC avec exit 0 et `source_unchanged=true`, porte l'empreinte
`58c1f417fe253a675a9052cc3224e185e6bda50e6d696b9f6cc3485b480687fd`.
Son EXE `tidewater_plant_probe.exe`, 139 913 728 octets, a pour SHA-256
`f6eb0b6ec73c291789171dd8ec313da549446c4e0f5c2c0b4a70621d5a1e2e56`.
C'est la version du parcours diagnostic A, acquis avant la correction Clippy.
Le reçu de build reste `diagnostic_only=true`, `runtime_verified=false`,
`gpu_verified=false`, `gate_qualified=false`. Les contrôles globaux et supply
du candidat final devront être acquis sur leurs propres pins, sans écraser les
reçus ni recopier une ancienne version du code.

Après A, de nouveaux changements externes dans `crates/forge_engine/Cargo.toml`
et le nouvel exemple `verify_source_graph_state_draw.rs` ont fait diverger le
candidat. Le coordinateur a constaté le refus de D par le garde de source
**avant création de sortie ou spawn** : aucun PID, processus ni résultat runtime
D n'existe pour cette tentative. La [trace du refus D](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-001-refusal.json)
conserve son message exact et exit 1 ; l'heure de l'événement est inconnue,
seule l'heure d'enregistrement est renseignée. Ces changements sont conservés.
Le déplacement mécanique autorisé
du module de tests a ensuite changé uniquement l'ordre des items dans
`source_graph_state_buffer.rs` : SHA-256 avant `8e56e1caa950cb6d61b86da3ba25d1d56243840c1d1fd8147c53dd50086f54ce`,
après `50d0866a9d27c33f4045fa58cd983840d0afc431ce5555b73a00caf5dd16f83f`.
La [preuve externalstate-layout-001](../../../../.gate3-evidence/20261013/externalstate-layout-001/receipt.json)
atteste les trois tests et tous les items de production conservés à l'octet
près, rustfmt ciblé et diff-check exit 0, ainsi que les autres inputs contrôlés
inchangés pendant cette opération. Aucun Cargo ni jeu n'y a été exécuté.
Les builds suivants ont capturé leurs propres empreintes complètes ; le
parcours A reste attaché à sa source historique et n'est pas réétiqueté.

Le [contrôle supply005](../../../../.gate3-evidence/20261013/closure-common-005/supply-result.json)
est terminé à 17:49 UTC : PASS, deny et audit exit 0,
`source_unchanged=true`, aucun input changé. Son empreinte est
`a391c43eff29cab0e20b7d95bd3658443fed9c0bec84fc0d7ec0d981cc655adf`,
son [manifeste](../../../../.gate3-evidence/20261013/closure-common-005/supply-source-manifest.json)
SHA-256 `e9bd4eeaa079cf3a940c330f2a9271b37c5234672151068f27b96f45bee21d0a`.
Les zéro vulnérabilité, dix warnings et un blocage glib conservé restent des
résultats de politique supply, sans qualification runtime ni autorisation de
distribution. Le départ de DEBUG003 a capturé `434919daeefb491c5358251c64fd1757e82291bddd2fc87340dbb90f24071380` :
le seul écart avec supply005 est `crates/forge_engine/README.md`, inclus dans
les manifestes du driver. [DEBUG003](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-003/debug-build-receipt.json)
finit donc `DIAGNOSTIC_BUILD_FAILED`, malgré Cargo exit 0 :
`source_unchanged=false`. Les gardes et identités sont conservés ; le PASS
supply005 ne devient pas un PASS Native005, WASM ou Release.

[DEBUG004](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-004/debug-build-receipt.json)
finit PASS, exit 0 et `source_unchanged=true`, sur la même empreinte
`a391c43e…` que supply005. Son EXE, 139 913 728 octets, est épinglé SHA-256
`ce89d27e8dec06d3a7980648018039bb8bab71143f81ef35679a4529b189b8f1`.
[DEBUG005](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-005/debug-build-receipt.json)
et [DEBUG006](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-006/debug-build-receipt.json)
ont ensuite réussi avec cache, exit 0 et sources inchangées, sur leurs
empreintes distinctes `f9b97b3b12c801f0d4ed0b241301ff83d112740583763497a756cf8e45a39762`
et `cb9fe710f5bcfeb479e6b823b2f9964e7abb4d3ae7f30d4101247f280610d49d`.
L'EXE est identique `ce89d27e…` dans ces trois builds ; chaque reçu conserve
pour autant son propre inventaire et ses limites diagnostic. Aucun PASS
Native005, WASM, Release, runtime ou GPU n'en est déduit.

Le [lot correctif gelé](../../../../.gate3-evidence/20261013/closure-native-corrections-001/owned-files-frozen.json),
SHA-256 `41bc187a3cd785e097cdd357ac656c6b50320c73ec2f2d8072269971ad2042cd`,
porte cinq fichiers moteur : échantillonnage des seules situations connues de
PNJ mort, conservation des événements/contacts et chaleur vivante ou état
inconnu important ; corrélation ActorRequest par identité et sequence, avec
autorité du receipt et ACK de commande exact ; HUD sur fond sombre décalé à
48 px, padding 10 px, masqué quand le panneau G est ouvert et queries
disjointes. Sept tests utiles sont ajoutés ; les vingt tests de la famille
`native_gameplay` sont retrouvés PASS dans le log011 historique, y compris les
guards d'observation et de corrélation. Aucun parcours ni verdict de présentation n'est déduit
de ce gel source.

[Native006](../../../../.gate3-evidence/20261013/closure-common-006/native-result.json)
est conservé `FAILED`, Clippy exit 101 pour `type_complexity` dans
`presentation.rs:175`, `source_unchanged=false` sur ce fichier. Son empreinte
initiale était `5309588733f7d11d5fd98065a636720c8dfcf87b3ac82269610bc53819448d16`.
L'[adoption en lecture seule](../../../../.gate3-evidence/20261013/alias-adoption-001/read-only-adoption.json)
constate un alias `HudQuery` équivalent, les mêmes queries disjointes et aucune
dérogation de lint ajoutée ; l'auteur de cette modification externe n'est pas
établi. Cette adoption ne réécrit pas le FAIL native006.

La validation historique [common007](../../../../.gate3-evidence/20261013/closure-common-007/native-result.json)
porte l'empreinte `c96365916028c9760052bd64f10c32372dd7f3c0d87d5dd333c215926b2edab3`
et le [manifeste](../../../../.gate3-evidence/20261013/closure-common-007/native-source-manifest.json)
SHA-256 `4dc59a23bacbcd0aa92968bec3d4f58fa5af09d72b73b93ecf22cfdd2f69a8e1`.
Format/diff et Clippy strict workspace/all-targets sont PASS ; Clippy finit à
18:54:39 UTC, exit 0. Le [reçu supply007](../../../../.gate3-evidence/20261013/closure-common-007/supply-result.json)
est PASS, deny/audit exit 0, `source_unchanged=true`, sur ce même manifeste.
Les tests Cargo finissent à 19:14:44 UTC, exit 0, 2 143 réussis, zéro échec,
56 ignorés, 222 suites. Format/diff/Clippy/tests ont tous exit 0, mais le reçu
global finit **FAILED**, `source_unchanged=false` : seul
`crates/forge_engine/README.md` a changé pendant les tests, de SHA-256
`342c3062649708197aa37530c25c5d562ee9866cb30c9d097cc1ad90f384dbb8` à
`78656098f0fd2a0886f070c548d3e1f37961f59ccab5debd8b829883416ce2b3`.
Aucun Rust, probe ou source d'application n'a changé dans cet inventaire.
Le garde inclut ce README : son échec reste conservé, et les étapes Cargo
réussies ne sont pas réétiquetées comme qualification de l'empreinte courante.

[Native008](../../../../.gate3-evidence/20261013/closure-common-008/native-result.json)
commence à 19:16:45 UTC, empreinte
`c8eed83cbf93971aae122a7c91e7e3762ed28c6d03f116bc78a0b4af7d0ada03`,
[manifeste](../../../../.gate3-evidence/20261013/closure-common-008/native-source-manifest.json)
SHA-256 `0fdc046465ac4dda0b0b0691c3a0d04447d2ba2b3bd84f219d8cffad88484b14`.
Format/diff/Clippy/tests finissent tous exit 0 ; le reçu global est **PASS**,
`source_unchanged=true`, `changed_inputs=[]`. Les tests terminent à
19:20:07,343832 UTC : 2 143 réussis, zéro échec, 56 ignorés, 222 suites.
Ce décompte a été relu dans le reçu008 et recalculé depuis son propre
[log](../../../../.gate3-evidence/20261013/closure-common-008/tests.log),
SHA-256 `a2897e22d09b0382ead47a8034309419de34c4659bd9d8e21a74c0a24082a3a1`,
sans reprendre le décompte007. Le reçu conserve `runtime_verified=false` et
`gate_qualified=false`. Le
[reçu supply008](../../../../.gate3-evidence/20261013/closure-common-008/supply-result.json)
est PASS, deny/audit exit 0, `source_unchanged=true`, sur le même manifeste.
Le cache et les flags restent coordonnés : jobs 2, threads de tests 2,
incremental 0 et `-C debuginfo=0`. Les
[34 replays privés](../../../../.gate3-evidence/20261013/closure-common-008/private-replays-current.json)
sont désormais PASS, 34/34, `source_unchanged=true`, liés au manifeste008,
au résultat natif SHA-256 `60a8e61d41466ed80049e58f478e3b6698efed5199a0636d6f34e96f81877478`
et au log `a2897e22…`. Le
[supplément CPU](../../../../.gate3-evidence/20261013/closure-ignored-test-audit-001/replay-common008-001/result.json)
est `PASS_CPU_SUPPLEMENT`, quatre contrats sur quatre, sources inchangées,
lié à ce même résultat natif. Le protocole privé canonique n'est pas modifié ;
aucune nouvelle exécution GPU ni UI Studio n'en est déduite.
La [lecture headless](../../../../.gate3-evidence/20261013/closure-common-008/native-gameplay-headless.json)
est `HEADLESS_CONTRACTS_PASS`, 65 tests requis effectivement retrouvés dans le
log008, source `c8eed83c…`, même manifeste/résultat/log. Les flags runtime,
GPU et gate restent faux : cette lecture ne lance pas le jeu.

[WASM008](../../../../.gate3-evidence/20261013/closure-common-008/wasm-result.json)
finit **FAILED**, `source_unchanged=false`, malgré ses deux étapes exit 0.
Le check `webgpu-engine` termine à 19:24:07 UTC ; le bundle Studio, Cargo et
posthooks compris, termine à 20:06:51,879760 UTC. Cinq inputs ont changé :
`crates/forge_engine/README.md`, les deux fichiers de mesures `camera_perf.rs`
et `walk.rs`, `verify_source_graph_state_draw.rs` et son sous-module
`texture_mips.rs`. Le [log du bundle](../../../../.gate3-evidence/20261013/closure-common-008/studio-webgpu-bundle.log)
conserve ses warnings et l'attente du verrou. Les succès de compilation et des
posthooks ne deviennent pas un PASS global sur une autre empreinte.
Le manifeste WASM initial
`a88b867f06bc64112d41cf7b85ab1302113fc26aec0aac57ebc0dfa72babcbe5`
porte le même fingerprint `c8eed83c…` et les mêmes inputs que CPU008.

Le coordinateur et le mapper ont raccordé les mesures existantes
`PlaytestTiming` au mode natif `CAMERA_PERF` opt-in, avec identité réelle de
l'adapter GPU, dans `camera_perf.rs` et `walk.rs`. Ce travail ne réintroduit
pas le test intégré Studio retiré. Les deux fichiers sont gelés :
`camera_perf.rs` SHA-256 `050294d0a06b159e180e7b6873e94e6cc08f3dd275fbbf5a098b112417eeab9a`,
`walk.rs` `7b0b1371bbf0a9b6a7fd93f2e12e165b1c245d2cad7cd0b2429cb541fec47e3c`.
CPU008 garde son PASS historique sur `c8eed83c…` ; le FAIL du garde WASM008
reste conservé. Le [préchauffage runtime Release008](../../../../.gate3-evidence/20261013/closure-common-008/runtime-release-prewarm-diagnostic.log)
termine la compilation du seul `forge_runtime` en 22 min 50 s, exit 0 selon
le coordinateur. Ce build diagnostic n'atteste ni les trois applications, ni
le jeu présenté, ni une qualification Release ou GPU.

[Native009](../../../../.gate3-evidence/20261013/closure-common-009/native-result.json),
empreinte initiale `68ad17c03e236bbb90ec80934d734839bd4c106319b2a82ac427f50b5645420d`,
finit **FAILED**, `source_unchanged=false`, malgré ses quatre étapes Cargo exit 0.
Les tests vont de 19:54:57 à 19:59:14 UTC : 2 143 réussis, 56 ignorés, 222 suites.
L'unique delta inventorié est l'exemple externe
`crates/forge_engine/examples/verify_source_graph_state_draw.rs`, de SHA-256
`b52d4fe678108d879442ca3f5cff798055b46770d12804763c0a568a94c26bfe` à
`d3e1dcc438707cc2017129fba1397c97f4aac0603329a3c2725c2f16a7e858a2`.
Les deux fichiers de mesures sont identiques aux pins gelés ci-dessus dans
les manifestes009 et010. Aucun PASS courant n'est déduit de native009 ; le
reçu et son garde restent conservés.

[Native010](../../../../.gate3-evidence/20261013/closure-common-010/native-result.json)
commence à 20:02:16 UTC sur l'empreinte
`7e4f0da5ab41749936fb3f1c7810a83080e668331825e5066f72d2f2c784604a`,
[manifeste](../../../../.gate3-evidence/20261013/closure-common-010/native-source-manifest.json)
SHA-256 `e4909b6cf253c29e871ab360f14722dddca76eea92f0ee250d102ba6bc6eca49`.
Entre les manifestes de départ009 et010, l'exemple ci-dessus et
`crates/forge_engine/README.md` diffèrent ; ce dernier passe de `78656098…` à
`fdec5280bdb9d85a329c9559a5cfebfeb8bd6975a5e31a42b1751c03a59d3555`.
Ce changement de README est capturé avant010 et n'apparaît pas dans les
`changed_inputs` du run009 ; il n'est pas attribué à son échec de garde.
Format/diff/Clippy/tests sont tous exit 0 ; le reçu global est **PASS**,
`source_unchanged=true`, `changed_inputs=[]`. Les tests vont de
20:02:27,593272 à 20:05:20,158108 UTC : **2 143 réussis, zéro échec,
56 ignorés, 222 suites**. Ce décompte est relu dans le reçu010 et recalculé
depuis son propre [log](../../../../.gate3-evidence/20261013/closure-common-010/tests.log),
SHA-256 `71a799bad2eccd79ec47a3bae9a2953da9aee75afc65d27a35b783152b7475ea`.
Le reçu natif SHA-256 est
`5b6e02457f61ab9b2037c3c38c36561c5bfc4ebeaf9c4e4f9bf2b8b8891c32c6`.
[Supply010](../../../../.gate3-evidence/20261013/closure-common-010/supply-result.json)
est PASS, deny/audit exit 0, `source_unchanged=true`, sur le même manifeste.
Les [replays privés010](../../../../.gate3-evidence/20261013/closure-common-010/private-replays-current.json)
sont **PASS**, 34/34, `source_unchanged=true`, épinglés au manifeste natif010,
à son résultat `5b6e0245…` et à son log `71a799ba…`.
Le [supplément010](../../../../.gate3-evidence/20261013/closure-ignored-test-audit-001/replay-common010-001/result.json)
est `PASS_CPU_SUPPLEMENT`, quatre contrats sur quatre, sources inchangées,
même résultat natif ; le protocole privé canonique de 34 tests reste inchangé.
La [lecture headless010](../../../../.gate3-evidence/20261013/closure-common-010/native-gameplay-headless.json)
est `HEADLESS_CONTRACTS_PASS`, 65 tests requis retrouvés dans le log010,
avec ces mêmes source, manifeste, résultat et log. Aucun jeu, GPU ni parcours
UI n'est exécuté par ces trois contrôles CPU.

[WASM010](../../../../.gate3-evidence/20261013/closure-common-010/wasm-result.json)
finit **FAILED**, `source_unchanged=false`, sur son manifeste de départ `7e4f0da5…`, SHA-256
`663e72191576a51edbb868f961400fc2d6cab09bbed752f98f2236e0c67fda16`.
Le check moteur termine exit 0 à 20:08:27,692531 UTC ; le bundle Studio,
Cargo et posthooks compris, termine exit 0 à 20:23:36,798760 UTC. Les deux
`validation_error` sont null. L'unique `changed_inputs` est
`crates/forge_engine/examples/verify_source_graph_state_draw.rs`, de
`d3e1dcc438707cc2017129fba1397c97f4aac0603329a3c2725c2f16a7e858a2` à
`3b802249c75dff66950cc84aea99ab748dd90b659947b08c4b93218e235a07af`.
Le coordinateur a relevé à 20:27 UTC la nouvelle empreinte
`6a6a7ca392ce3ba22e8b68275dae465145151a3dcef688f3750eff2a4e883e20`.
Ce delta est conservé ; native010, supply010 et leurs lecteurs restent des
PASS de leurs inputs historiques. Aucun build Release010 de qualification,
mesure présentée ou verdict runtime/GPU/UI n'est déduit du build WASM refusé.

Le [préchauffage du jeu Release010](../../../../.gate3-evidence/20261013/closure-common-010/native-game-release-prewarm-diagnostic.log)
termine la compilation du seul exemple `tidewater_plant_probe` en **23 min 10 s**,
exit 0 au constat du coordinateur. Ce build diagnostic ne remplace pas les
trois étapes du reçu Release courant, son frontend épinglé ou un parcours natif.

[Native011](../../../../.gate3-evidence/20261013/closure-common-011/native-result.json)
est **PASS**, `source_unchanged=true`, quatre étapes exit 0, sur la source
`6a6a7ca392ce3ba22e8b68275dae465145151a3dcef688f3750eff2a4e883e20`.
Son manifeste SHA-256 est
`0c5b0bd6cfedc87ac6ddfec257d5ef4bd11efaebb339bc400a02d4917c333073`.
Les tests terminent à **20:47:54,764743 UTC** : 2 143 réussis, zéro échec,
56 ignorés, 222 suites, recalculés depuis le
[log011](../../../../.gate3-evidence/20261013/closure-common-011/tests.log)
SHA-256 `c20e7030547ae731f099d511400484a94bac35a45567fb6bb1c8f0d7f3e2d4cc`.
[Supply011](../../../../.gate3-evidence/20261013/closure-common-011/supply-result.json)
est PASS, deny/audit exit 0 et sources inchangées sur ce même manifeste.

Après cette clôture CPU011, `crates/forge_engine/README.md` passe de
`fdec5280bdb9d85a329c9559a5cfebfeb8bd6975a5e31a42b1751c03a59d3555` à
`d482c62c50f82e63747cbb268a1b4a10fbb9bb51bab542b94623a8dae69bfd20`.
La source devient `617b0e947d3c833a45361ae7e3f3c096f174db114b7a27c554d50b665b5ef2fa`.
Le PASS011 reste historique. Les replays privés011 et le supplément011 sont
refusés avant exécution, leurs outputs n'existent pas. Le
[lecteur headless011](../../../../.gate3-evidence/20261013/closure-common-011/headless-reader-console.log)
refuse « native receipt belongs to different sources or validation driver » ;
aucun JSON de contrats011 n'est produit. Aucun résultat PASS010 n'est transféré
à ces tentatives refusées.

[Native012](../../../../.gate3-evidence/20261013/closure-common-012/native-result.json)
commence sur `617b0e94…`, manifeste SHA-256
`6763690751b1670998ec39270edfbd6b68943825700431b916b103e1751c9f4f`,
et finit **FAILED**, `source_unchanged=false`, malgré format/diff/Clippy/tests
exit 0. Les tests terminent à **20:56:44,663369 UTC**, 2 143 réussis, zéro échec,
56 ignorés, 222 suites, recalculés depuis son propre
[log012](../../../../.gate3-evidence/20261013/closure-common-012/tests.log)
SHA-256 `ebf960bc7c0da1edaa8b7a4b89a30f61616e69b529107c413d24165afa55b281`.
Pendant cette acquisition, `verify_source_graph_state_draw.rs` change de
`3b802249c75dff66950cc84aea99ab748dd90b659947b08c4b93218e235a07af` à
`faad943cf4fb628d2c021f3046700326a5cc7ca76dd1894e2215f24413fd08b2`,
et le nouveau `verify_source_graph_state_draw/vertex_projection.rs` apparaît,
SHA-256 `66705f463d7744887cd8ac8c0d5b84a4d80f88674b1a5b293e99d725538aec7c`.
Ces deux `changed_inputs` sont conservés sans copie/recul de code.
[Supply012](../../../../.gate3-evidence/20261013/closure-common-012/supply-result.json)
finit PASS, sources inchangées, avant le delta d'exemple ; il reste donc
historique sur `617b0e94…`, avec la même politique et le blocage glib conservé.

[WASM011](../../../../.gate3-evidence/20261013/closure-common-011/wasm-result.json)
finit **FAILED**, `source_unchanged=false`. Check moteur exit 0 à
20:47:03,582524 UTC ; bundle Studio, Cargo et posthooks compris, exit 0 à
**21:01:33,023557 UTC** ; les deux `validation_error` sont null. Son manifeste
reste celui de départ `6a6a7ca3…`, SHA-256 `0c5b0bd6…`. Les trois
`changed_inputs` sont `crates/forge_engine/README.md`, l'exemple
`verify_source_graph_state_draw.rs` et son nouveau sous-module
`vertex_projection.rs`. Aucun frontend ou Release courant n'est qualifié par
ces compilations réussies dont le garde a refusé les changements de source.

Les wrappers externes sont prêts : [lancement d'un Studio possédé](../../../../.gate3-evidence/20261013/closure-studio-ui-008/run-studio-ui.py) avec
isolation d'authoring et cleanup, et [campagnes A–E](../../../../.gate3-evidence/20261013/closure-execution-plan-007/run-current-campaign.py) réutilisant runner/lecteurs
communs avec inventaire du contenu avant/après. Ils ne créent aucune preuve UI
ou runtime par leur simple préparation. Le protocole P02 public géré a
[18 tests de parser/lifecycle PASS](../../../../.gate3-evidence/20261013/closure-public-ui-protocol-001/preparation-managed-001.json)
en 4,550 s ; il authentifie `workspace-v1/authoring.json`, sa référence unique
project/module/area/archive/head et `objects/<archive_hash>.json` identique à
l'export. L'ancien fichier de zone n'est jamais un fallback. Les pins d'autorité
et d'objet restent contrôlés avant/après. Le contrat exige les deux vrais
receipts UI, leurs captures/audit, Save/Export et la reprise explicite exacte.
Les fichiers sont externes aux inputs compilés ; aucune acquisition publique
P02 ou native C n'a encore été réalisée par ce protocole.

Le helper SDK dispose de
[12 contrats Python distincts validés](../../../../.gate3-evidence/20261013/closure-sdk-identity-001/preparation-002.json)
(dix initiaux et deux contrôles durcis), sans que ces tests exécutent son API.
Son [adoption001](../../../../.gate3-evidence/20261013/closure-sdk-identity-001/adoption-001.json)
est préparée sur native011 alors valide. Le
[run001](../../../../.gate3-evidence/20261013/closure-sdk-identity-001/run-001/result.json)
compile le helper exit 0, puis finit **FAILED_CLOSED**, `source_unchanged=false`,
`failure="ValueError: Common sources changed"`, `snapshots=[]` : l'API d'identité
n'a pas été appelée et aucune identité SDK n'est adoptable. Les bibliothèques
sont sélectionnées par fingerprints Cargo exacts, avec désambiguïsation et pins
du compiler/stdlib/closure ; le refus de source reste appliqué.

Le protocole externe caméra a
[15 tests PASS](../../../../.gate3-evidence/20261013/closure-camera-perf-protocol-001/protocol-tests-005.log),
13,914 s, et exige maintenant un résultat SDK complet et courant pour les hashes
effectifs terrain/végétation. Ses guards vérifient identité, contexte, capture
arrêtée, huit phases RPG effectivement mesurées, statut des observations et
cleanup du PID possédé. Aucun profiler n'a été exécuté ; les métriques ne sont
pas inventées à partir d'une compilation ou d'un receipt SDK refusé.

Les reçus sont conservés sous `E:/OpenRPG_Forge/.gate3-evidence/20261013`.
Une modification ultérieure d'un input nécessite les contrôles correspondants
sur sa nouvelle empreinte ; aucun ancien PASS n'est réétiqueté.

Le [diagnostic Release D001](../../../../.gate3-evidence/20261013/closure-release-runtime-diagnostic-001/environment-D-001/result.json)
du 13 octobre, 21:15 UTC, lance le vrai jeu commun `62e59699…` sur le plan D
de 87 étapes. Le PID possédé `20712` quitte en environ sept secondes, code 0,
sans arrêt forcé. Les observations attestent boot, plan préparé et une frame
`ready=false`, tick/sequence 0 ; aucun marqueur terminal n'est émis et le fichier
réservé pour le rapport reste à zéro octet. Le lecteur strict refuse donc le résultat : `FAILED_CLOSED`,
`Native observations lack their boot or terminal marker`. Le log contient
deux avertissements de canal de readback fermé ; ils ne déterminent pas la
cause de sortie. Binaire, protocole et données restent épinglés et inchangés.
Ce préchauffage n'a pas de reçu Release complet rattaché aux sources courantes
(`UNPROVEN_PREWARM_ONLY`) ; aucune qualification runtime, GPU ou Gate 3 n'est
déduite, même si les inputs sont restés stables pendant ces sept secondes.
La fermeture prématurée doit être expliquée avant la campagne finale.
L'inspection des sources a identifié un défaut d'ordre : les rédacteurs du bilan
étaient dans `Last`, où le plugin fenêtre, le profiler caméra et le diagnostic
de stand peuvent encore produire `AppExit`. Ils pouvaient manquer ce signal
avant que Winit ne quitte. Le correctif installe maintenant un schedule privé
`NativeGameplayExitReports` après tout `Last`. Il ne déplace aucun système de
physique, navigation, commande ou menu. L'API Bevy 0.19.1 et ce diff ont été
revus : le schedule finit avant le retour d'`app.update` et le contrôle de sortie
de Winit.

Le [contrôle ciblé002](../../../../.gate3-evidence/20261013/closure-window-exit-order-002/result.json)
est **PASS CPU**, trois tests réussis en 0,67 s après compilation en 3 min 18 s,
sources communes et deux fichiers corrigés inchangés pendant ce contrôle,
empreinte `0c787dc7…`. Les tests réutilisent le helper de production et couvrent
le vrai cycle `WindowCloseRequested` de Bevy, la vraie commande native Quit
via `process_commands`, et un producteur tardif dans `Last`. Les bilans sont
écrits lors de la mise à jour qui produit la sortie, avec identités et état
cohérents. Le cas Quit est une fixture CPU dans `Update`, pas une acquisition
UI ou Fixed/physique du jeu.
Le [Clippy ciblé002](../../../../.gate3-evidence/20261013/closure-window-exit-order-002/clippy-result.json),
`cargo clippy -p forge_engine --lib --tests --locked --offline -- -D warnings`,
est aussi PASS, exit 0 en 1 min 58 s, sur cette même empreinte et ces deux pins,
sans delta source. Ce contrôle reste limité à lib/tests moteur ; il ne remplace
pas Clippy workspace/all-targets, WASM ou les builds Release finaux.

Les pins courants de `native_gameplay.rs` et `tests.rs` sont respectivement
`d33ef0c0888487aa2a14fc178bb3cc3becd411a85c5cc574197545a0c0d4c13e`
et `76ff6866a5ec6f58850312f3df2a701308b8434c9d63e0025aa62cc42c9f7fc6`.
Les gels et reçus précédents gardent leurs pins historiques. Le
[premier essai001](../../../../.gate3-evidence/20261013/closure-window-exit-order-001/result.json)
est conservé : un PASS et un FAIL dû à l'hypothèse erronée d'une fermeture
immédiate dans la fixture, corrigée pour respecter les deux mises à jour du
plugin fenêtre. Le test002 vérifie le bilan dans la seconde, où la fermeture
est effective. Aucun GPU, nouveau jeu ou campagne de qualification n'est lancé
pendant le chantier de rendu. Ce correctif de bilan ne détermine pas pourquoi
la fenêtre D001 aurait fermé ; le lancement réel reste à revérifier ensuite.

| Contrôle | Résultat observé | Portée exacte |
| --- | --- | --- |
| Format et `git diff --check` | Étapes012 exit 0 ; reçu global012 FAILED de garde | [Native012](../../../../.gate3-evidence/20261013/closure-common-012/native-result.json), départ `617b0e94…`, delta d'exemple et nouveau sous-module pendant le contrôle. Aucun PASS de l'empreinte ultérieure n'est transféré. |
| Clippy workspace/all-targets, `-D warnings` | Étape012 exit 0 ; reçu global012 FAILED de garde | Les FAIL 004/006/007/009/012 restent conservés avec leurs causes exactes. Le dernier PASS global011 est historique sur `6a6a7ca3…`. |
| Tests natifs workspace/all-targets | Étape012 exit 0 : 2 143 réussis, zéro échec, 56 ignorés, 222 suites ; garde global FAILED | [Log012](../../../../.gate3-evidence/20261013/closure-common-012/tests.log) relu et total recalculé. [Native011](../../../../.gate3-evidence/20261013/closure-common-011/native-result.json) garde son PASS historique sur `6a6a7ca3…` ; aucune preuve globale actuelle après les deltas. |
| Replays privés canoniques | PASS historique010, 34/34, sources inchangées pendant ce contrôle | [Reçu privé010](../../../../.gate3-evidence/20261013/closure-common-010/private-replays-current.json), mêmes manifeste, résultat natif et log010. Pas de GPU ni de preuve UI. |
| Supplément Debris/SourceCommon | PASS CPU historique010, 4/4, sources inchangées pendant ce contrôle | [Supplément010](../../../../.gate3-evidence/20261013/closure-ignored-test-audit-001/replay-common010-001/result.json), protocole canonique34 inchangé ; pas de GPU/UI. |
| Contrats headless RPG/physique | 65 PASS historiques lus dans le log010 | [Reçu headless010](../../../../.gate3-evidence/20261013/closure-common-010/native-gameplay-headless.json), pins010 et source `7e4f0da5…` ; aucune exécution native du jeu ni qualification gate déduite. |
| Supply : deny licences/bans/sources/advisories et politique RustSec | PASS historique012 ; zéro vulnérabilité, dix warnings, blocage de distribution glib conservé | [Reçu supply012](../../../../.gate3-evidence/20261013/closure-common-012/supply-result.json), deny/audit exit 0, `source_unchanged=true`, source `617b0e94…`, avant le delta d'exemple. Aucun PASS ultérieur, WASM, Release ou autorisation de distribution n'en est déduit. |
| Contrats Python du protocole | 47 PASS, 6,827 s, exit 0 | [Reçu protocol-tests-002](../../../../.gate3-evidence/20261013/closure-protocol-tests-002/receipt.json) et [log](../../../../.gate3-evidence/20261013/closure-protocol-tests-002/tests.log). Les tests vérifient les lecteurs, le runner et leurs refus ; aucun processus RPG, rendu GPU ou parcours Studio n'en est déduit. |
| P02 public géré / caméra / adoption SDK : protocoles externes | P02 18 PASS ; caméra 15 PASS ; SDK 12 contrats distincts validés | Parser/lifecycle et gardes de provenance uniquement. P02 et caméra ne sont pas exécutés réellement. Le helper SDK compile exit 0 dans run001, puis refuse la source modifiée avant toute inspection API ; aucun hash SDK courant adoptable. |
| Bilans de sortie native | Trois tests CPU ciblés PASS, 0,67 s ; Clippy lib/tests ciblé PASS | [Contrôle002](../../../../.gate3-evidence/20261013/closure-window-exit-order-002/result.json) et [Clippy002](../../../../.gate3-evidence/20261013/closure-window-exit-order-002/clippy-result.json), helper de production après tout `Last`, vraie WindowPlugin, vraie commande Quit et signal tardif. Ni lancement du jeu, GPU, UI ou preuve Release finale ; la cause de fermeture D001 reste ouverte. |
| API native Bevy image / ruzstd corrigé | PASS | `closure-ruzstd-security-001/api-build-receipt.json`, KTX2 et `zstd_rust` effectivement compilés. Portée dépendance native uniquement, pas une qualification du jeu ou WASM. |
| Treize unités `native_gameplay` | PASS diagnostic, 2,36 s, dans `closure-native-targeted-005` | Acquisition antérieure aux dernières retouches du menu et au patch de dépendance. Ne qualifie pas l'empreinte finale actuelle. |
| Test ciblé terrain corrigé | 1 PASS, 1,35 s | `closure-terrain-current-test-001`, binaire et deux sources terrain épinglés. Diagnostic ciblé ; ni suite workspace finale ni runtime/GPU. |
| Build du jeu debug | 001 FAIL ; 002 PASS ; 003 FAIL de garde source malgré Cargo 0 ; 004/005/006 PASS, sources inchangées | [Debug002](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-002/debug-build-receipt.json) reste source `58c1f417…`, EXE `f6eb0b6e…`. [Debug006](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/build-006/debug-build-receipt.json) est source `cb9fe710…`, EXE `ce89d27e…`, identique aux EXE 004/005. Chaque acquisition est diagnostic ; aucune qualification Release/GPU n'en est déduite. |
| WASM et builds Release | WASM008/010/011 FAILED de garde malgré étapes exit 0 ; aucune Release complète courante | [WASM011](../../../../.gate3-evidence/20261013/closure-common-011/wasm-result.json) finit refusé sur trois deltas, départ `6a6a7ca3…`. Préchauffages diagnostic runtime008 en 22 min 50 s et jeu010 en 23 min 10 s : aucun des deux ne qualifie les trois applications ou le jeu présenté. |
| Parcours RPG natif debug A | `DIAGNOSTIC_ONLY`, `OBSERVATIONS_COMPLETE`, failure null, processus exit 0 et `source_unchanged=true` | [Reçu direct-A-001](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/direct-A-001/result.json), EXE `f6eb0b6e…`, source `58c1f417…`. `rpg_commands`, `p01_three_places` et `p01_completed_loot_quest` vrais. Deux approches, reprise/isolation et R3-B non attestés ; runtime/GPU/gate restent faux dans le reçu diagnostic. |
| Diagnostics environnement D | 002, 004 et 006 `FAILED_CLOSED`, exit 1, sans arrêt forcé, sources inchangées | [D002](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-002/result.json) expire à la poussée step 18 ; [D004](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-004/result.json) expire sur déplacement step 35 après Push/Ignite/Extinguish. [D006](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-006/result.json), DEBUG006/DATA04, finit sur quota d'observations à step 78 ; ses effets physiques partiels ne qualifient pas une campagne complète. |
| Nouveau lot et contrôles communs | Raccords observation/corrélation/HUD/mesures appliqués ; PASS011 historique puis native012 FAILED de garde | Privé/supplément/headless011 refusés avant acquisition. Native005 non exécuté et anciens FAIL conservés. Après les deltas, contrôles courant, SDK réel, Release, HUD et campagnes du candidat final restent dus. |
| Campagnes Release et qualification GPU courantes | Pas encore acquises dans ce relevé | Le debug A et ses captures ne remplacent pas les processus Release épinglés, les observations restantes, la revue visuelle ou les mesures de performance. D a été refusé avant lancement après divergence de sources. |

Les premiers FAIL de `closure-common-001`, des diagnostics Clippy/WASM et des
tentatives ciblées sont conservés dans leurs dossiers d'origine. Ils ne sont
ni supprimés ni remplacés par le dernier résultat. Le correctif de dépendance
est décrit dans [ADR-0022](../../ADR/ADR-0022-bevy-image-ruzstd-security-update.md).
`RUSTSEC-2024-0429`, glib 0.18.5, reste un blocage de distribution explicite.

L'[audit des tests ignorés](../../../../.gate3-evidence/20261013/closure-ignored-test-audit-001/memo.md)
et son [inventaire nominatif](../../../../.gate3-evidence/20261013/closure-ignored-test-audit-001/audit.json)
expliquent les 56 ignorés de native003. Cet audit initial est historique et
n'exécutait aucun test : ses 24 groupes sélectionnaient 34 tests sans doublon,
un sélecteur n'étant pas un PASS. Les 22 autres se
répartissent en Debris/SourceCommon (4), Beach/Village (6), transports
Ocean/Shore/Water distincts (3), Temporal/Stall (2), probes CPU optionnels (3),
delta d'archive (1) et Vault privé (3). Les acquisitions008 ferment maintenant
les 34 replays canoniques et les quatre contrats Debris/SourceCommon
supplémentaires avec leurs propres reçus PASS CPU, sources inchangées, sans
modifier le protocole canonique. Les 18 autres tests ne reçoivent aucun PASS
par déduction ; leurs périmètres et limites restent ceux de l'inventaire.
Ces résultats ne qualifient ni la bibliothèque privée entière, ni Studio, ni
le GPU. Le statut initial NON EXÉCUTÉ reste conservé dans l'audit d'origine.

## Matrice des critères

Les critères normatifs restent ceux de
[la roadmap](../../planning/MIGRATION_ROADMAP.md#gate-3--première-boucle-rpg-environnementale).
« Source prête » ou « protocole PASS » ne signifie pas « critère intégré qualifié ».

| Critère | Sources, machine et acceptation disponibles | Preuve restant à produire ou limite |
| --- | --- | --- |
| R3-A : joueur/stats/inventaire, PNJ/navigation, interaction, dialogue, quête, ennemi, combat, sauvegarde/reprise | Bridge générique installé dans le vrai jeu séparé ; commandes, HUD/menu, persistance et rapport de fermeture implémentés. Suite native003 PASS sur son empreinte ; protocole courant 47 PASS. Debug A vérifie `rpg_commands`, quête/loot consommé et défaite directe d'un ennemi ; acceptation fonctionnelle propriétaire acquise. | Conclure les contrôles du nouveau candidat puis démontrer le parcours dans les processus Release épinglés et la sauvegarde reprise dans un autre processus. Le debug A ne prouve pas cette reprise ni une qualification Release. |
| R3-B : poussée, lancer, impact, chute/piège, feu, eau/extinction, destruction et IA au danger | Protocole PASS ; D004 observe Push/Ignite/Extinguish. D006 observe aussi lancer, impact ennemi, destruction/libération/chute de charge et retraite acquittée ; il finit `FAILED_CLOSED` sur quota d'observations. | Conclure un scénario complet après correction du journal, puis produire la campagne Release. Aucun PASS R3-B n'est déduit du flux incomplet ; le feu/IA doit encore être acquis dans E frais. Conserver contacts/chaleur/états et attribution causale, garde 1 000 ms et navigation `NoPath`/ouverture/`Reached` avec révisions géométrie/nav. |
| R3-P01 : trois lieux reliés, deux approches, conséquence persistante | Fixture SDK avec lieux/références et scénario préparés ; debug A observe trois lieux visités, la quête terminée, le loot consommé et une défaite directe. `p01_three_places=true`, `p01_completed_loot_quest=true`, `p01_two_approaches=false`. Origine Studio acceptée par le propriétaire. | Acquérir la seconde approche, la reprise durable et la campagne Release. Une fixture SDK ou le debug A n'est pas une nouvelle preuve du parcours public Studio ni le PASS complet P01. |
| R3-P02 : retouche réplique/porte depuis Studio puis reprise | Fixtures SDK historiques initiale/retouchée prêtes. Le protocole public géré possède 18 tests de guards PASS et authentifie les deux receipts UI, l'autorité workspace et son objet exact. Les lecteurs comparent l'ancien save à la reprise, avec seulement la revalidation physique et les poses des portes actuelles. | Acquérir les vrais clics/save/export, A fermé avec demande compatible acceptée, puis le nouveau processus sur le Release courant. La préparation du protocole ne prouve pas l'UI. Le refus strict ou les règles incompatibles sont couverts par les contrats Rust ; aucun replay natif du menu de récupération n'est déduit de ce cas compatible. |
| R3-P03 : référence absente/objectif inaccessible et diagnostic orientant la correction | Refus SDK de référence et retour diagnostic Studio implémentés, avec leurs limites. Un cas public hors monde sur copie possédée est préparé : Item de quête Sceau à X=2048, refus de candidat par terrain canonique, correction UI puis apply/save attendus. | Aucun PASS UI encore acquis. P03 accepte un objectif inaccessible général, pas exclusivement NoPath ou MissingReference. Attester l'erreur, la guidance vers objet/condition, la correction réelle et l'original conservé ; la prévention de suppression et le rapport du jeu seuls ne ferment pas P03. |
| R3-P04 : état choisi, isolation et révision de reproduction | Lecteur contrôle l'état initial complet d'une nouvelle partie, la sauvegarde exacte et le nouveau PID de reprise. Runner inventorie archives et racines auteur avant/après ; heads, scénario, EXE, processus et rapport final sont liés. Debug A atteste un état initial fresh conforme et une sauvegarde capturée ; `p04_fresh_resume_isolation=false`. | Exécuter la reprise dans un autre processus et prouver les conséquences conservées sans mutation auteur, puis acquérir la campagne Release. Les choix/revisions visibles dans Studio ne sont pas attestés par le lecteur. Le sélecteur de l'ancien test intégré retiré ne constitue plus une procédure actuelle. |
| R3-P05 : lisibilité et rythme du parcours complet dans le jeu séparé | **ACCEPTÉ PROPRIÉTAIRE** le 13 octobre : dialogue, quête, combat, sauvegarde/reprise et parcours complet créé depuis Studio. Cette décision reste acquise. | L'acceptation ne fournit pas l'identité d'un nouveau binaire ni une campagne technique supplémentaire. Les preuves natives/GPU actuelles et leurs limites doivent être jointes, sans réclamer une nouvelle acceptation sur la seule base de tests machine. |

## Fixtures et protocole actuel

Les reçus `closure-native-fixtures-001/initial/fixture-provenance.json` et
`retouched/fixture-provenance.json` annoncent honnêtement
`DATA_ONLY_APPLICATION_FIXTURE`, `application_commands_used=true`,
`source_unchanged=true`, `copied_product_code=false`,
`studio_ui_verified=false`, `runtime_verified=false`, `gate_qualified=false`.
Le helper est une application du SDK existant, sans copie du code produit.

Le lecteur de campagne normale impose une preuve indépendante de processus
(`--campaign`), les pins d'EXE/source/archive/scénario, une fermeture normale,
et un rapport complet strict lié au PID et au tick/sequence de l'Exit.
Les assertions attendues doivent être indiquées explicitement avec `--require` :
un stream simplement lu avec des checks faux n'est pas un PASS de critère.

Le mode `retouch` est réservé aux deux révisions P02 et à
`compatible_retouch_preserved_state`. Il n'agrège pas les capacités R3-B entre
deux archives différentes. La reprise conserve les octets du save et la demande
réelle du jeu ; aucun état ou fichier de reprise n'est fabriqué pour réussir.

Le [diagnostic natif A](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/direct-A-001/result.json)
a réellement exécuté l'EXE debug `f6eb0b6e…` dans le PID 37112, fermé normalement
avec exit 0, sans arrêt forcé, et ses sources sont restées inchangées. Ses trois
exigences explicites sont satisfaites : `rpg_commands`, `p01_three_places` et
`p01_completed_loot_quest`. Le lecteur relève 1 778 frames ready, dix actions
commises, trois lieux visités par le joueur, une défaite directe, une quête
terminée et le loot consommé. Le rapport et la sauvegarde sont capturés et
épinglés dans ce reçu. Les captures existent, mais leur existence ne vaut pas
revue visuelle. Le résultat est `DIAGNOSTIC_ONLY` / `OBSERVATIONS_COMPLETE`,
failure null : ni seconde approche, ni reprise/isolation, ni R3-B ne sont
attestés par A. `runtime_verified`, `gpu_verified` et `gate_qualified` restent
faux dans ce protocole diagnostic ; aucune qualification Release n'est déduite.

Le coordinateur a inspecté les PNG A de [dialogue](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/direct-A-001/run-1/native-output/rpg-direct-02-dialogue.png)
et de [quête terminée](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/direct-A-001/run-1/native-output/rpg-quest-completed.png).
Le panneau G, les choix et les indications F5/F9 sont lisibles. Le HUD blanc
sur le ciel clair, et son recouvrement par le panneau, rendent sa lecture
difficile dans ces images. Ce constat demande une vérification de présentation
sur le candidat final ; il n'est ni une acceptation artistique propriétaire,
ni un verdict Release/GPU. L'acceptation fonctionnelle P05 reste acquise.

Les tentatives environnementales conservent leurs échecs. Dans D002, la demande
Push a été acceptée en entrée mais aucun receipt canonique Push n'a été commis :
le contrôle de contact effectif ne l'a pas confirmé, puis step 18 a expiré.
Le jeu a fermé exit 1, sans arrêt forcé et avec sources inchangées. DATA03 a
resserré l'approche et ajouté une attente de 30 ticks, sans modifier ce contrat.
Dans D004, les observations montrent les receipts canoniques Push à tick
3 693/sequence 2, Ignite à 4 219/3 et Extinguish à 4 744/4. Le déplacement
step 35 vers `(30, -120)` a ensuite expiré ; la dernière position observée
`(29,4514 ; -114,9781)` reste bloquée en amont. Le processus a fermé exit 1,
sans arrêt forcé, `source_unchanged=true` : aucun PASS global ou rapport de
campagne normale n'est inféré de ces actions partielles.

Les refus D001, [D003](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-003-refusal.json)
et [D005](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-005-refusal.json)
précèdent toute sortie/spawn, suite à des changements externes du candidat ou
de l'exemple privé de dessin. Le message exact et exit 1 sont conservés dans
leurs traces ; l'heure de l'événement est inconnue, l'heure d'enregistrement
est explicitement distincte. Aucun PID ni résultat runtime n'est attribué à
ces tentatives refusées, et les reçus A/builds ne sont pas réécrits.

DATA04, [environment-full-04.json](../../../../.gate3-evidence/20261013/closure-native-scenarios-001/environment-full-04.json),
SHA-256 `16aa929e24ac8b3a28a43caf3cbbe5c4e42ceb24ed202525b016f1a73e7b77b6`,
remplace seulement les deux MoveTo 35–36 par trois MoveTo 35–37 passant par
le corridor `(38,5 ; -100)`, `(38,5 ; -117)`, `(38,5 ; -120)`.
Toutes les étapes autres que MoveTo et tous les champs hors steps sont
inchangés ; maximum de 600 s, attentes et garde feu de 1 000 ms conservés.
D006 utilise ce DATA04 et DEBUG006, empreinte `cb9fe710…`, EXE `ce89d27e…`.
Son [reçu terminé](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-006/result.json)
est `DIAGNOSTIC_ONLY` / `FAILED_CLOSED`, exit 1, sans arrêt forcé,
`source_unchanged=true`. Le PID 37196 est fermé. Le
[log](../../../../.gate3-evidence/20261013/closure-native-debug-preflight-001/environment-D-006/run-1/game.log)
signale la limite d'observations atteinte à 18:29:17 UTC puis le refus de
scénario step 78 à 18:29:55 UTC. Le fichier fait 67 109 398 octets pour un
quota de 64 Mio ; ses observations restent **incomplètes**, sans rapport final
de campagne normale ni qualification native/GPU/Release.

Ce flux contient néanmoins des faits physiques épinglés : receipt Throw à
tick 13 736/sequence 5, projectile vers l'ennemi à tick 13 767 avec contact à
5,197 m/s, 405,19 J et 100 dégâts ; impact d'environ 375 J sur le support,
destruction et libération de la charge, puis chute supérieure à 0,05 m.
L'ignition du feu à tick 17 550/sequence 6 et l'ACK de retraite à 17 552 ont
un délai conservateur de 42,3043 ms, inférieur à la garde de 1 000 ms. Le
coordinateur relève chaleur, dégâts, fuite et mouvement KCC courant du
Veilleur vivant dans D ; un scénario E frais reste nécessaire pour isoler
la causalité propre au feu. Ces observations partielles ne sont pas un PASS
de lecteur sur une campagne fermée normalement.

Après cette ignition, 2 798 snapshots ont été enregistrés. La lecture technique
attribue la fréquence excessive à la chaleur et au danger encore projetés
chaque tick pour un PNJ mort. Le correctif d'échantillonnage est désormais
appliqué et gelé avec la corrélation des acteurs et le HUD. Événements
significatifs, chaleur vivante, contacts, quota 64 Mio et garde 1 000 ms sont
conservés. Common007 a réussi ses étapes Cargo mais a échoué au garde source
du README ; native008 et supply008 sont PASS, sans preuve de
nouveau parcours ou de présentation du HUD. Les empreintes
debug005/006 et supply005 restent historiques si le prochain lot change les
inputs ; leurs reçus ne seront pas réétiquetés.

Le rapport optionnel `FORGE_GAME_REPORT_PATH` revient au Studio après fermeture.
Son bilan se lit aussi après reprise du viewport résident. Les sources contrôlent
taille, fichier régulier, PID, hashes et succès réel du processus ; un rapport
absent ou refusé reste visible comme tel. Sa réception n'est pas un verdict Gate.

Le flux public P02 prévu utilise la racine d'auteur exacte du parcours A auquel
il sera apparié, puis conserve les reçus des deux retouches réelles dans Studio
(réplique et porte), distincts des reçus SDK. L'API officielle Sky est accessible
au coordinateur ; ces gestes restent à acquérir sur le binaire Release. Les
archives initiale/retouchée et la reprise doivent garder leurs pins propres,
sans réétiqueter le diagnostic A historique.

P03 attend encore une référence absente **ou un objectif inaccessible**, avec
diagnostic UI guidant l'objet ou la condition à corriger. MissingReference et
NoPath sont des exemples ; la roadmap 4063 ne limite pas l'inaccessibilité à
ces catégories. Le coordinateur prépare un cas sur copie de données possédée :
Item de la quête Sceau positionné hors monde à X=2048, préparation d'un vrai
candidat attendue refusée par le terrain canonique, puis correction de position
dans l'UI et apply/save. L'erreur, la guidance, la correction et l'intégrité de
l'original doivent être observées : **aucun PASS n'est déclaré avant ce parcours
UI**. La simple prévention de suppression du Sceau conserve un document valide ;
elle reste un contrôle de prévention et ne ferme pas P03 à sa place.

Les derniers correctifs Studio conservent le motif d'un arrêt en erreur et le
chemin du log dans le dernier diagnostic, y compris si le rapport est absent ou
invalide. Le résumé reste visible après retour à Studio et décrit le statut
effectif du lancement ; une erreur de polling ne devient pas une fausse
confirmation de fermeture. Le test Rust existant a été étendu pour vérifier la
persistance après plusieurs polls inactifs, mais cette extension est postérieure
au PASS native003 : son verdict appartient à la nouvelle campagne native.

Le lancement ordinaire nettoie les variables héritées `FORGE_TIDEWATER_*`,
`FORGE_NATIVE_GAMEPLAY_*` et `FORGE_GAME_OBSERVATIONS`, puis transmet ses
chemins de sauvegarde et de rapport propres. Il évite ainsi qu'une session
Studio ordinaire hérite d'un scénario de qualification. Le runner direct
conserve ses variables explicites. Une demande de reprise corrompue peut encore
être refusée avant la création du rapport ; le diagnostic Studio conserve alors
l'erreur, sans promettre un menu de récupération pour ce cas.

Le runner crée désormais le répertoire `native-output` avant de lancer le jeu :
l'initialisation moteur le canonicalise et exige sa présence. Un test
d'intégration à processus simulé vérifie que le dossier existe déjà au moment
du spawn ; il ne constitue pas une exécution native. Aux frontières de propriété
Save/Capture, le runner reconnaît l'alias Windows de lecteur canonisé `\\?\E:\…`
et le chemin `E:\…`, tout en conservant les refus de chemins hors du répertoire
possédé, de voisins et de traversées. Les tests du protocole utilisent aussi de
vrais fichiers temporaires Windows. Les JSON observés du jeu restent intacts.

Ces corrections sont source et protocole à ce relevé. Le hash source contenu
dans le rapport du jeu désigne le document d'authoring ; l'identité des inputs
communs et du binaire provient séparément des manifestes et reçus de campagne.
Aucun rapport, test synthétique ou alias de chemin ne remplace cette chaîne de
preuves, ni une acquisition GPU ou une vérification UI.

## Performance et documentation

Les seuils 1080p/qualité élevée, moyenne 60 FPS, p95 au plus 16,7 ms et
1 % bas au moins 45 FPS appartiennent à O2 dans
[PROJECT_CONTEXT.md, lignes 630–644](../../../PROJECT_CONTEXT.md), avec une
condition de passage explicite de Gate 1 aux lignes 1574 et 1595–1601.
Ils ne sont pas ajoutés comme nouvelle case de Gate 3 à la roadmap R3.
La performance interactive Gate 2 reste acceptée par le propriétaire
([roadmap, 4002–4017](../../planning/MIGRATION_ROADMAP.md),
[ADR-0018, 13–18](../../ADR/ADR-0018-gate2-owner-validation-gate3-tidewater.md)) ;
ces preuves et leurs limites sont conservées, sans rouvrir Gate 2 ni P05.

Pour le RPG courant, l'exigence pertinente reste de mesurer fluidité, attentes,
simulation et navigation sur matériel/résolution/profil définis, puis comparer
aux budgets du contexte ([G3-10, ligne 204](../../planning/GATE3_PREPARATION_PLAN.md),
[AGENTS.md, ligne 449](../../../AGENTS.md),
[version commune, 1151–1167](GATE3_SHARED_VERSION_20261011.md)). Ces mesures
restent dues ; déclarer les écarts et données indisponibles, sans revendiquer
une qualification O2 ou High absent de la preuve. Les dérogations Gate 1
conservent leur périmètre. Aucun High complet WorldSketch ni seuil supplémentaire
de navigation n'est ajouté à R3 ; CPU simulation/préparation 4 ms et GPU 14 ms
restent des budgets indicatifs avec dépassements visibles.

Le profiler existant `FORGE_TIDEWATER_CAMERA_PERF=1` peut fonctionner dans une
session indépendante du scénario, sur le vrai walk et son gameplay chargé.
Il attend readiness et 240 frames, puis mesure quatre phases de 6/10/6/10 s.
Son `perf-proof.json` précise résolution, present mode, valeurs graphiques et
limites CPU/GPU. Il produit zéro capture ; son booléen `pass` ne teste pas les
seuils FPS et ne prouve pas un profil High. Les captures utiles à la revue
visuelle sont une acquisition distincte.

Les procédures doivent conserver les limites de leurs reçus, expliquer les
diagnostics et la reprise/compatibilité, et permettre une reproduction sur la
révision actuelle. L'installateur antérieur n'est pas déclaré à jour par les
présents changements source ou par le contrôle supply.

## Condition CI et suite

Constat distant revérifié en lecture seule le 13 octobre, à partir de 18:58 UTC :
[Rust gate 38394829662](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662),
HEAD local et `origin/Rust` `4799ebb3dfa98795d97862f0fed3cd4f6a876887`, unique
run de ce workflow sur ce HEAD, quatre jobs refusés avant toute étape pour
facturation/plafond. Les jobs Windows `115287273553`, Linux `115287273463`,
WASM `115287273027` et supply-chain `115287273652` ont chacun `steps=[]` et la
même annotation de paiement/plafond. Verdict : **NOT RUN — infrastructure billing**.
Les détails et annotations sont dans [l'audit CI](GATE3_CI_READINESS_20261013.md).
ADR-0006 a expiré ; cette acceptation RPG ne réactive aucune exemption.

P02 reste une retouche de réplique et porte **depuis Studio**, suivie d'une
reprise sans perte (roadmap 4062 ; plan G3-09, 195). P03 exige objectif
inaccessible **ou** référence absente et un diagnostic menant à l'objet ou
à la condition à corriger (roadmap 4063 ; plan G3-10, 202). Ces preuves publiques
restent à acquérir sur le candidat Release ; une opération SDK ou un rapport
backend ne prouve pas le parcours UI ([contexte, ligne 327](../../../PROJECT_CONTEXT.md)).
L'[audit du transport UI](../../../../.gate3-evidence/20261013/closure-execution-plan-007/commands-20261013T1901Z.md)
constate l'IPC Tauri et l'absence de backend HTTP authoring. L'inventaire CUA
vide et son API native désactivée ne signifient pas une impossibilité globale
de contrôler Windows : le coordinateur a chargé l'API officielle `@oai/sky`
et listé les applications. Les clics réels P02/P03 restent PENDING après le
build Release ; aucune acquisition publique n'est déclarée dans ce relevé.

La publication exige une autorisation explicite (AGENTS 39–42 ; ADR-0018,
32–36). La CI finale réellement exécutée Windows/Linux/WASM/supply-chain reste
nécessaire (AGENTS 76–77 ; ADR-0006, 19–20 et 165–183). Les modifications locales
actuelles ne sont pas la révision distante déjà contrôlée ; aucun push ni
workflow n'a été lancé par cet audit.

Le chantier externe de rendu continue de modifier les inputs inventoriés.
Le propriétaire a choisi de le terminer avant la qualification. Le coordinateur
ne relance donc pas native013/WASM012 pendant ce chantier ; il conserve les
contrôles terminés et prépare la reprise sur la version commune finale.
La compilation WASM011 déjà possédée est terminée dans son dossier ; son FAIL
de garde reste conservé. Aucun autre processus ni travail n'est interrompu.

Revalidation de dépendance à **21:51:56 UTC** : les processus de compilation
et de rendu recherchés ne sont plus présents, mais le chantier externe n'est
pas terminé. Son relevé courant conserve quatre écarts d'une ULP sur les
normales monde, encore en cours d'analyse. L'absence de processus ne constitue
ni un PASS ni une déclaration de fin de chantier. Le
[relevé précédent](../../../../.gate3-evidence/20261013/closure-render-dependency-observation-001.md)
atteste l'attente du vrai Cargo et sa progression, avant disparition du handle.

**Blocage externe de la qualification : fin du rendu attendue**, conformément
à la décision du propriétaire. Le même besoin de version commune finalisée
persiste depuis le tour initial et ses continuations ; les corrections CPU,
revues et préparations indépendantes sont maintenant achevées. La reprise des
contrôles exige cette évolution externe, puis des inputs communs stables.
Ce blocage de l'objectif ne clôture pas Gate 3 et ne remet pas en cause P05.

Obstacles concrets et preuves restantes :

- Stabiliser les inputs du candidat commun assez longtemps pour fermer ses
  contrôles native/supply/WASM sans delta, puis ses trois builds Release avec
  frontend/EXEs exacts. Le code concurrent est conservé ; aucune copie/recul
  ne contourne le garde.
- Réussir la vraie inspection SDK sur cette même source, pour les identités
  terrain/végétation utilisées par le profiler. Une compilation du helper suivie
  d'un refus ne fournit pas ces identités.
- Exécuter les campagnes A–E complètes et leurs lecteurs stricts sur les mêmes
  EXEs Release : R3-A/B, trois lieux et deux approches P01, Save/reprise distincte,
  compatibilité P02, état choisi et isolation P04. Les diagnostics debug partiels
  ne sont pas réétiquetés.
- Acquérir les vrais clics/captures publics P02/P03 et le diagnostic Studio après
  retour du jeu ; le cas P03 d'objectif hors monde demeure préparé, sans PASS UI.
- Produire les mesures RPG actuelles et captures pertinentes avec leur contexte,
  en distinguant acquisition CPU/GPU, lecture machine et acceptation visuelle.
- Réunir ensuite l'autorisation explicite de publication requise, la CI finale
  réellement exécutée et le verdict de clôture. Le dernier run distant observé
  reste NOT RUN billing ; aucune nouvelle exception ADR-0006 n'est accordée.

P05 et Gate 2 restent acceptés par le propriétaire. Chaque résultat conserve
son périmètre et ses échecs. Gate 3 n'est pas déclarée complète.
Ni commit, push, release, facturation ni Gate 4 ne sont autorisés par ce document.
