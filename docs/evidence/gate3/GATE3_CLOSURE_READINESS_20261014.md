# Gate 3 — reprise du 14 octobre 2026

## Verdict courant — clôture du 15 octobre 2026

**Gate 3 est clôturée.** Le [rapport final](GATE3_CLOSURE_20261015.md) consolide
les 21 critères et leurs preuves locales sur Source013, publiée au commit
`3e0044d5f63b26c678d9db19380a66db832d9de3`.
Le propriétaire retire explicitement la CI GitHub des critères bloquants de
cette clôture dans [ADR-0023](../../ADR/ADR-0023-gate3-owner-removes-github-ci-criterion.md).
Le run `38762140900` reste NOT RUN pour facturation/plafond, sans étape exécutée.
Les réserves de distribution GLib et Windows vierge restent conservées.

Ce document est le journal des acquisitions successives. Les mentions
« ouverte », « non publiée » ou « CI encore due » ci-dessous décrivent leurs
snapshots antérieurs ; elles ne remplacent pas le verdict final ci-dessus.
Les reçus bruts et leurs flags de qualification datés ne sont pas réécrits.

## Journal historique de préparation

**Gate 3 restait ouverte lors de ces préparatifs.** Le propriétaire autorise, lors de la reprise de
l'après-midi du 14 octobre, la qualification de la version commune actuelle
pendant que les conversions de modèles continuent sur F:. Cette décision
remplace l'attente de fin du rendu confirmée le matin. L'acceptation de
la boucle RPG séparée/P05 et Gate 2 restent acquises. Ce relevé complète le
[bilan du 13 octobre](GATE3_CLOSURE_READINESS_20261013.md) ; ses reçus et échecs
historiques ne sont pas remplacés.

État courant du 15 octobre : le correctif de lancement depuis le Studio installé
porte l'empreinte source
`4178ac3b3f7d028cb5b94ca0d870d67c245fc0418a595f375e6a8eb75027b163`.
Native013 est PASS : **2 201 tests, aucun échec, 56 ignorés, 232 suites** et
format/diff/Clippy PASS. Supply, WASM et les trois Release013 sont PASS, sources
inchangées. Le blocage global de distribution GLib reste présent. Les campagnes
A–E, SDK, caméra et parcours publics012 restent attachés à leurs reçus012 ; la
comparaison des composants inchangés est explicite ci-dessous.

Le candidat classique013 est compilé et sa vérification locale d'installation,
réinstallation et désinstallation est PASS. Le trajet public depuis le Studio
installé a produit trois jeux successifs, tous fermés normalement ; les preuves
sont conservées après désinstallation isolée. Les deux packs de contenu séparés
sont exportés et **l'installateur principal est remplacé par les octets013
vérifiés**. Source013 n'est pas publiée et aucune CI sur cette révision exacte
n'est acquise. Ces
limites ne rouvrent pas l'acceptation propriétaire P05 ; elles interdisent un
verdict de clôture Gate 3 ou de distribution qualifiée.
L'option `FORGE_CAMERA_PERF_PREWARM=1` prépare des poses observées avant
l'admission, avec trace sœur séparée et limites explicites ; aucune mesure
qualifiée ni condition officielle Gate3 n'en découle. Le jeu Debug010 est
compilé, session27836 fermée code 0 à **21:56:09 UTC**. Son essai est fermé
sur panic, PID28888, code 101 sans arrêt forcé ; la trace de préparation est
réellement `prepared`, mais aucun bilan de mesure complet n'est acquis.
Le déplacement de la fin de capture hors du `resource_scope<Perf>`, d'abord
préparé après cet échec, est maintenant appliqué dans le lot011 décrit ci-dessous.
Le patch de mise en page du bilan est appliqué, sans vérification visuelle009.
Le jeu Debug009 est compilé, session66838 fermée code 0 à **21:30:09 UTC**.
Son essai caméra est fermé code 1 sans arrêt forcé, `DIAGNOSTIC_ONLY` /
`PROCESS_FAILED` : le journal localise la perte de readiness au compteur de
pipelines/hill, sans identifier un pipeline précis. Aucune qualification009
n'est acquise. Les preuves008 restent
historiques sur l'empreinte
`187c791625d7aec15517c24d3a121e22ce3b3051ddce4f44f483c210c3e60919`.
Native, supply et WASM008 sont PASS, sources stables : 2 195 tests, aucun échec,
56 ignorés et 232 suites, dont les cinq nouvelles régressions du cycle caméra.
Les 34 replays privés, 65 contrats headless et 13 contrats supplémentaires
passent sur la même empreinte. Les trois Release008 sont désormais PASS,
sources/frontend inchangés, driver fermé code 0 à **19:58:45 UTC**. Le SDK008
run-002 rapporte réellement les identités initiale/retouchée, avec pins stables ;
l'échec001 sur `.rustc_info.json` reste conservé. A008 est fermé normalement,
PID 20140, assertions requises PASS et sources/contenu inchangés. B–E et leur
agrégat008 sont désormais fermés PASS. P02 public008 est acquis par deux
retouches, Save/export et reprise native exacte ; Studio7140/helper74635 sont
fermés code 0. P04 conserve trois ouvertures publiques fresh/reprise/fresh,
Save inchangé et feedbacks capturés, avec limites : pas de GameState initial
complet public ni de bilan visible. La baseline caméra008 est fermée
`FAILED_CLOSED`, code 1 sans arrêt forcé : après admission et warmup réussis,
la capture est invalidée au frame1102 par « native gameplay readiness lost ».
Aucune mesure n'est qualifiée. Le lot009 conserve le calcul de
`simulation_ready` et ajoute seulement un journal de diagnostic limité dans
le probe caméra. Les acquisitions publiques012 et le bilan désormais visible
sont détaillés ci-dessous. CI sur la source exacte, publication et distribution
globale GLib restent ouverts. La cohorte007 a échoué à
trois lints Clippy avant les tests ; supply et WASM007 passent, sources
inchangées. Les correctifs012 ne sont pas publiés. La lecture distante du run
connu retrouve quatre jobs en échec sans détails steps/logs ; elle ne confirme
ni qu'il s'agit du dernier run ni un motif actuel de facturation. Le jeton CLI
gh invalide limite la lecture actuelle ; la facturation documentée le 13
reste seulement historique.

Les preuves fermées de la cohorte006 portent le correctif Exit sur l'empreinte
`c3de2a57ad55342daee45d31530d1bcd46ea4a41c745a7ca142aaf338c91e1fc`.
Native, supply, WASM, 34 replays privés, 65 contrats headless et 13 contrats
supplémentaires sont PASS, avec sources inchangées. Le SDK006 est admis.
Les trois Release006 sont désormais PASS sur cette même empreinte, sources et
frontend inchangés. A–E006 sont fermées normalement et leurs assertions requises
passent, de même que l'agrégat des deux approches ; sources et contenu restent
inchangés. C valide une retouche SDK compatible, pas le parcours public P02.
Le parcours public P02 est ensuite acquis réellement sur006, par deux retouches
Studio, Save/export et reprise native exacte ; ses reçus restent attachés à006.
La baseline caméra006 échoue fermée après le délai de 600 s, sans perf-proof ;
aucune mesure FPS/simulation/navigation n'en est déduite. La navigation causale
de porte, les parcours publics P03–P04/Studio WASM, les limites de mesures et une
CI exécutée sur la révision à publier restent à traiter. Le bilan Studio public
est refusé par l'ACL de `native_game_diagnostics` ; le retour sur carte fonctionne.
À **18:59 UTC**, le coordinateur applique le correctif ACL de quatre fichiers
Studio, ouvrant la cohorte007. Ses résultats puis le correctif Clippy008 sont
consignés ci-dessous. Aucun ancien reçu006 n'est réécrit ou promu sur007/008 ;
les preuves finales du nouveau candidat
restent dues. Le collecteur P02,
FireStart et les protocoles corridor002/lecteur003 sont préparés distinctement
des acquisitions. Aucun PASS GPU ou de gate n'est déduit de ces builds,
préparations ou lecteurs techniques. Les
paragraphes suivants conservent les échecs et la chronologie003/004/005.

## Échec caméra008 et correctifs009

Le [résultat caméra008](../../../../.gate3-evidence/20261014/closure-camera-perf-008-001/result.json),
SHA `74dc6404fe5690667c7cd4297c6b352d0de6211e301254d30f2c85a6dc730de3`,
est `FAILED_CLOSED` : PID13592, sortie code 1, `forced_stop=false`, sources
inchangées. Le [diagnostic conservé](../../../../.gate3-evidence/20261014/closure-camera-perf-008-001/camera.perf-lifecycle.json),
SHA `dd5e95cf7078612fedff652287c9ad003e4b5e2152b2d607757e947bfe85a5d7`,
rapporte au frame1102 `state=invalidated`, limite « native gameplay readiness
lost ». L'admission est complète et les 60 frames de warmup sont achevés ;
l'époque de capture a démarré. Les observations partielles ne constituent pas
une mesure qualifiée. Aucune cause matérielle ou GPU n'est déduite de cet échec.

Le [reçu de changement009](../../../../.gate3-evidence/20261014/closure-bilan-readiness-fixes-009/source-change.json),
SHA `24faa443df07e6ec319b6f7b910efa61707079857bb5de2773adc7d02966a4f9`,
enregistre exactement trois fichiers : `authoring_gold.css`,
`authoring_native_game.rs` et l'exemple `tidewater_plant_probe/walk.rs`.
L'empreinte passe de187c à
`b3b3244aa8e03990ad60c16d9c6be4d16236163ca1409a57b00993a247c4bbee`.
Le patch du bilan auparavant préparé est maintenant appliqué. Le journal
ajouté au probe caméra demeure limité ; le corps du calcul `simulation_ready`
est inchangé. Le reçu conserve les trois artefacts Release008 et les preuves
historiques ; il ne rapporte aucune vérification runtime, GPU ou de gate009.

La [compilation diagnostique009](../../../../.gate3-evidence/20261014/closure-debug-readiness-build-009/debug-build-receipt.json),
SHA `e841b989e9292ae94d66ca18d7d97f46e0a7cafb2e4de6810f45746d45e5d991`,
ne concerne que le jeu Debug : `DEBUG_BUILD_COMPLETE`, sources b3b3244…
inchangées, session66838 fermée code 0 à **21:30:09 UTC**. Le candidat
`34bba860a375440f2ee8384f8fad85bafa6fe6e5e5833fc3feb5388c700ea625`
pèse 140 060 672 octets. Ce build diagnostique n'est pas un Release qualifié.

L'[essai Debug009](../../../../.gate3-evidence/20261014/closure-debug-camera-009-diagnostic-001/result.json),
SHA `d45789842e09505a61abc3692deaaa34c065e74122790b6ebce4ae25bdbde20a`,
est `DIAGNOSTIC_ONLY` / `PROCESS_FAILED`, PID25128, sortie code 1 sans arrêt
forcé ; session69335 fermée, inputs protégés inchangés et `guard_errors=[]`.
Le [journal réel](../../../../.gate3-evidence/20261014/closure-debug-camera-009-diagnostic-001/game.log),
SHA `9a3cbf7698bc1ebaf8afd0c3c981eb76bc3acf39bead5adcc090c5fd9a985fd9`,
ligne66, rapporte au frame1201 `probe_ready_frames=0` et
`pipelines_ready=false`, tous les autres booléens de cette décision étant vrais.
La ligne69 conserve l'invalidation au frame1202, phase1, après admission et
warmup réussis. Cela localise la condition au compteur pipelines/hill, sans
prouver un pipeline précis, une cause GPU ou une mesure Release. À la fermeture
de cet essai, la proposition de prewarm est seulement préparée séparément ;
elle est ensuite appliquée dans le lot010 ci-dessous. Aucune acquisition UI009
n'est retenue. Le bilan lisible et les autres
preuves finales doivent porter le nouveau candidat ; les reçus008 ne sont ni
réécrits ni promus sur009.

## Préparation caméra010 appliquée, non qualifiée

Le [reçu d'application010](../../../../.gate3-evidence/20261014/closure-camera-prewarm-application-010/source-change.json),
SHA `31b61d406b7cc59ebd7f9bc65f5b4418d85b339ceb9cdc32b18dfce019f6d441`,
enregistre exactement trois fichiers de l'exemple : `camera_perf.rs`, le nouveau
`camera_prewarm.rs` de 605 lignes et `walk.rs`. L'empreinte b3b3244… devient
`ad11604222b7a0fe05448e67d83df97e09aea44034cb9a72eabf08c05c0922aa`.
Le [patchv2 relu](../../../../.gate3-evidence/20261014/closure-camera-pose-prewarm-proposal-001/camera-pose-prewarm-v2.patch),
SHA `6a3605fd29b8d5de060e618d9c640363ee4e98dcdc8227e2921f0aca947bf6d7`,
est appliqué par Root. `walk.rs` centralise seulement la sensibilité existante
de 0,0022 rad/unité ; le corps de `simulation_ready` demeure inchangé.

La préparation s'active uniquement avec `FORGE_CAMERA_PERF_PREWARM=1`.
Elle utilise les entrées caméra ordinaires et les poses réellement observées,
puis écrit une trace sœur `camera.prewarm.json` avant le début de capture.
Cette trace séparée reste diagnostique : limite de 120 s, 4 096 observations
maximum, 17 positions de yaw observées par direction et contrôle du retour à
la pose d'origine. Sa couverture ne prouve pas toutes les spécialisations GPU.
Les décisions de readiness et l'invalidation d'une capture active restent
conservées. Aucun résultat de mesure n'est acquis, aucune nouvelle condition
officielle de clôture Gate3 n'est ajoutée.

La [compilation Debug010 terminée](../../../../.gate3-evidence/20261014/closure-debug-prewarm-build-010/debug-build-receipt.json),
SHA `d53012dd79885eb1167da54e58d71d48ce4128673a621c3f2098b798370a34b9`,
rapporte `DEBUG_BUILD_COMPLETE`, sources010 inchangées, session27836 fermée
code 0 à **21:56:09 UTC**. Le candidat
`19c85d1156dbc6c42e012c80f575a086204c853b4d81b08f70b0d7bb43fc6865`
pèse 140 101 120 octets. Ce build reste diagnostique.

L'[essai Debug010](../../../../.gate3-evidence/20261014/closure-debug-camera-prewarm-010-diagnostic-001/result.json),
SHA `b4d5b130d66f4c88c16a43a175cd94f28f6a110ff5c5cf8ae07792c3b59280f3`,
conserve `DIAGNOSTIC_ONLY` / `PROCESS_FAILED`, PID28888, code 101 et
`forced_stop=false`, inputs préservés et `guard_errors=[]`. Root75675 est
fermé code 1. Le [résultat du wrapper](../../../../.gate3-evidence/20261014/closure-debug-camera-prewarm-010-diagnostic-001/prewarm-wrapper-result.json),
SHA `f46537861b2cfc092b16bd48ee856ea5bfccb2afb381862569fb7962ed8841ca`,
reste `FAILED_CLOSED`, mode Debug, une seule ouverture et verdicts de mesure,
Release et gate à false.

La [trace réelle de préparation](../../../../.gate3-evidence/20261014/closure-debug-camera-prewarm-010-diagnostic-001/camera.prewarm.json),
SHA `78c83cb972a557e38339cc8983853a118bed0322b0739c07ca6be5f6ae6291b8`,
est `prepared` : fin au frame818 / GameState tick263, masks outbound/inbound
`131071`, retour observé, capture0 et aucune mesure caméra/render commencée à
cette frontière. Admission et warmup réussissent ensuite ; l'époque mesurée
démarre à **22:01:31.162 UTC**. À **22:02:03.314 UTC**, le jeu panique dans
`camera_perf.rs:485`, sans perf-proof complet ni rapport final valide.

La cause lue dans le chemin de fin est l'appel à `finish_gameplay_capture`,
puis `update_gameplay_timing` et `observe_lifecycle`, depuis un
`resource_scope<Perf>` : `observe_lifecycle` tente de relire le `Perf`
temporairement retiré du World par ce scope. Le patch minimal est préparé hors
source après cet échec, puis appliqué dans le lot011 ci-dessous. Aucune mesure,
qualification runtime/GPU,
Release ou gate010 n'est retenue. La date locale a passé le 15 octobre,
mais ces dossiers014 et horaires UTC du 14 restent historiques. Les anciens
reçus et échecs demeurent attachés à leurs candidats.

## Correctif de fin de capture011 et diagnostic fermé

Le [reçu d'application011](../../../../.gate3-evidence/20261015/closure-camera-finish-application-011/source-change.json),
SHA `f83acd9ae4115316a3c768e986ec578becdf81fc5c3d01694598e1752771edb2`,
enregistre un seul fichier : `camera_perf.rs`, **+7/−1 lignes**. L'empreinte
ad116… devient
`75ba397d48704308ddae3dfe1d00e021db118dc89d9fe17f9ca5cd6c27332071`.
L'arrêt et le snapshot de la vraie capture sont effectués avant le
`resource_scope<Perf>` ; le finisher et ses conditions restent inchangés.
Le reçu conserve les preuves historiques et ne rapporte aucune vérification
runtime, GPU ou de gate.

La [compilation Debug011 terminée](../../../../.gate3-evidence/20261015/closure-debug-finish-build-011/debug-build-receipt.json),
SHA `74413957c1ea1a845e8f3aaf3e0eb1a2e1359d9bd68531d37fcda906cfe5fc67`,
est `DEBUG_BUILD_COMPLETE`, sources inchangées, code 0 à **22:10:19 UTC**.
Le candidat `0109feca07b06f366338f70689407c3f8cb5658b6763930c1cee7b8972ced1bb`
pèse 140 147 200 octets ; il reste un jeu Debug diagnostique.

L'[essai Debug011 fermé](../../../../.gate3-evidence/20261015/closure-debug-camera-prewarm-011-diagnostic-001/result.json),
SHA `91241d54fc33d12629d53634c9bca06fb3f4db088a613a7c3c022dd309e889f3`,
est `DIAGNOSTIC_ONLY` / `PROCESS_CLOSED`, PID34928, code 0 sans arrêt forcé,
inputs inchangés et `guard_errors=[]`. Root33285 est fermé code 0.
Le [bilan réel](../../../../.gate3-evidence/20261015/closure-debug-camera-prewarm-011-diagnostic-001/camera.perf-proof.json),
SHA `4994f47592e9fb829cba263625eda3aaecb0c993dd3861697a4293b4ab657e5b`,
rapporte `pass=true`, les quatre phases 6/10/6/10 s, 2 142 frames caméra et
2 142 render, 1 200 frames de mouvement et translation maximale 0 m.
Le timing RPG est `stopped`, limite nulle, frames784→3634 et ticks332→2253,
warmup de 60 frames achevé. Ces observations Debug ne sont ni une qualification
métrique, un Release qualifié ou un passage de gate.

Le [collecteur externe de préparation](../../../../.gate3-evidence/20261014/closure-camera-prewarm-collector-001/collect-camera-prewarm.py),
SHA `645e42f8644988b0c0c9840d895fc829ff5e24d27a9717083751eeac36952f47`,
est gelé par l'agent gameplay. Neuf gardes négatives passent ; elles valident
uniquement ses refus de preuves malformées ou incompatibles. Aucune collecte
réelle, mesure ou preuve Release n'est acquise par ce lot préparatoire.

## Cohorte013 : validations locales et candidat installable

Le [correctif013](../../../../.gate3-evidence/20261015/closure-installed-game-environment-013/source-change.json),
SHA `3709791191b3c1ba8d06f7aae5b0765fbf79049188816393f9f77882c8f62129`,
ne change que `launch_configuration.rs` et `build_delivery.py`. Le Studio
accepte le triplet complet de chemins absolus de jeu/contenu/sessions transmis
par l'installation ; un triplet partiel, vide ou relatif est refusé. Sans
override, le profil local reste utilisé. Quatre tests purs supplémentaires
passent dans la suite native réelle.

Les quatre [manifestes013](../../../../.gate3-evidence/20261015/closure-common-013/native-source-manifest.json)
portent la même source4178ac… ; les reçus sont PASS, avec
`source_unchanged=true`, `changed_inputs=[]` et `working_tree_changes=[]`.

| contrôle réellement fermé | résultat | SHA-256 du reçu |
|---|---|---|
| [Native013](../../../../.gate3-evidence/20261015/closure-common-013/native-result.json) | format/diff/Clippy et 2 201 tests PASS ; 56 ignorés, 232 suites | `794f43438cd1f2f62bc6ad769e5a94131319635064ea281bfd2300dbda48aa7b` |
| [Supply013](../../../../.gate3-evidence/20261015/closure-common-013/supply-result.json) | deny/audit code 0 ; 0 vulnérabilité, 10 avertissements | `ef0b5d53ceb9b330856b7188c62cb777a323dbd29f7afcad641fce0836f9966a` |
| [WASM013](../../../../.gate3-evidence/20261015/closure-common-013/wasm-result.json) | engine WebGPU et bundle Studio code 0 | `36938c6022673d33572b5833c05d54c9601c2024b325cf56a68cc6930f1b9296` |
| [Release013](../../../../.gate3-evidence/20261015/closure-common-013/release-result.json) | Runtime, Studio et Game code 0 | `1bcf86f3af67d974f3d37e4c12a846dfbce4168e3272acb14dd9fd8ed45b64bf` |

Supply conserve explicitement `distribution_blockers=1` : GLib 0.18.5,
`RUSTSEC-2024-0429`. Un PASS de ce contrôle ne lève pas ce blocage global et
n'accorde aucune dérogation de distribution Windows. Le frontend conserve
quatre avertissements de compilation connus.

La [comparaison Release réelle](../../../../.gate3-evidence/20261015/closure-impact-012-to-013-001/release-addendum-001/release-impact.json),
SHA `53d83cfba8640f204a2491a97b4489bdb90ac1f9ae34c75174f41d43dfadef92`,
confirme Runtime `a2f53b22…` et Game `1d485821…` bit-identiques à012 ; Studio
change comme prévu, SHA `22347277bc6f999adfd11d85e93211d84d1d08f5c2527d004a2ed22c53683fce`.
L'[addendum WASM](../../../../.gate3-evidence/20261015/closure-impact-012-to-013-001/wasm-addendum-001/wasm-impact.json),
SHA `72f925073536daef79553e4b6efbf4c8675b73c960373385247c8aaa4f9e5098`,
compare 3 025 fichiers : 3 023 identiques, deux reçus diagnostiques régénérés.
Les JS/WASM, HTML/CSS, worker et 3 011 assets sont identiques. Le fingerprint
complet du dist diffère ; aucune égalité littérale des anciens JSON non
archivés ni nouvelle mesure navigateur013 n'est inventée. Les observations
runtime/UI/navigateur012 restent sous leurs identités initiales, avec ce lien
borné de composants. Le nouveau trajet installé de Studio013 reste distinct.

Le [build classique013](../../../../.gate3-evidence/20261015/closure-classic-build-013/installer-build-proof.json),
SHA `41ce90ea74182d452ca631f0d6c1d0cbff247c7db56a7fdd6d6edcd776f9cff9`,
est fermé `BUILD_ONLY`, source stable, version `0.1.0-preview.20261015.013`.
Le candidat `closure-classic-candidate-013/OpenRPG-Forge-Setup.exe` contient
1 170 618 151 octets, SHA
`0eca18e3e632ca108b841feb2d540f1ee9bded5fa01dc6cd1686561b6ea17ad0`.
Il n'a pas remplacé le principal par ce build.

La [vérification classique013](../../../../.gate3-evidence/20261015/closure-classic-verify-013/verification.json),
SHA `21d06b4b9263ebde36929ffeebc8622991866e7287859afce93cbc8471e9b9ec`,
est `PASS_LOCAL_CLASSIC_INSTALLER` : chemin Unicode choisi, payload embarqué,
vérification complète, vrai WebView/IPC Aube, trois recettes natives et probes
de démarrage launcher/éditeur. Réinstallation puis désinstallation conservent
90 fichiers personnels et les fichiers étrangers. Le mode `NOINTEGRATION` ne
teste pas registre/raccourcis ; Windows propre, installation initiale WebView2,
acceptation visuelle humaine et qualification de release restent non prouvés.
L'avertissement de nettoyage `Chrome_WidgetWin_0 error1412` reste consigné.
Le probe WebView ne lance pas le jeu. Le parcours installé réel, l'export
séparé et la promotion acquis ensuite sont décrits ci-dessous.

Le [reçu du parcours Studio installé013](../../../../.gate3-evidence/20261015/closure-installed-studio-ui-013/relaunch-002/result.json),
SHA `3266bce04fb771abe93b75482da27dc32933c1d5b6b3a287cc5a23c94e53fa5c`,
est `CLOSED_OBSERVATIONS_RETAINED_AND_UNINSTALLED`. Studio PID25832 et les jeux
successifs PID14108, PID4160 et PID19448 terminent code 0 sans arrêt forcé.
Les trois rapports conservés ont `ready=true`, `exit_success=true` et les
erreurs commande/simulation/reprise nulles. Les retours réels sont demande de
reprise, sauvegarde reprise puis nouvelle partie. La sauvegarde F5 conservée
porte le SHA `be3e215e88626a20ab1fd84e2b6d24d4972e7cf0dc282b816eddfffc5d8c2664`.
La demande F9 est consommée à la deuxième session et absente à la troisième ;
les octets Save restent inchangés. La source, les 89 fichiers d'auteur
originaux et le profil sont inchangés. La désinstallation isolée retire les
applications possédées et conserve **350 fichiers utilisateur**. L'incident
antérieur de rapport vide reste exclu, sans reçu de sortie ni qualification.
Le reçu ne prétend ni observer l'état initial complet du jeu, ni apporter une
nouvelle acceptation visuelle humaine ou une qualification de gate.

L'[audit visuel installé013](../../../../.gate3-evidence/20261015/closure-installed-studio-ui-013-audit-001/audit.json),
SHA `d3dae63b5eebf0ba4f0963387ea68ac2b68310c84eb2307200ed4f9db42f892a`,
conserve **12 JPEG originaux et 12 états Sky** : HUD nouvelle partie, retour F5
« Partie sauvegardée », retour F9 « Reprise demandée », HUD repris puis HUD
nouvelle partie. Les trois bilans publics ont réellement été ouverts ; les
détails du premier montrent PID, source et Save. La demande stricte
`d49a1383…` lie le Save `be3e215e…`, avec `allow_compatible_edits=false` ; les
trois snapshots conservent les mêmes octets Save et des comptes de demandes
**1 → 0 → 0**. Cet audit qualifie localement le raccord installé et les actions
publiques de sauvegarde/reprise. Il ne rejoue pas tout dialogue/quête/combat
et ne transforme ni FPS instantané, ni observation Codex en acceptation humaine.

L'[export séparé013](../../../../.gate3-evidence/20261015/closure-content-export-013-001/result.json),
SHA `073b87f686793fb7e5d7502e4c19cbbcc49565591c9ba4b242330d56388d7584`,
est `CONTENT_PACKS_EXPORTED`, sources et pins d'entrée inchangés. Le dossier
`E:/OpenRPG_Forge/Forge-Contenus-20261015` contient les deux `.forgepack`
`reference-assets` et `reference-module`, leurs manifestes originaux,
`CONTENUS.json` et `LISEZ-MOI.md`. Les packs sont copiés exactement depuis le
payload vérifié, sans reconstruction ni copie du package applicatif. Ils
couvrent les données de l'aventure de référence, pas la bibliothèque Asset
Vault complète. Leurs IDs, révisions, dépendances, licences et SHA sont
conservés pour intégration par un futur client. Cet export ne qualifie pas
la distribution et ne remplace pas la CI exacte.

La [promotion effective du principal](../../../../.gate3-evidence/20261015/closure-main-promotion-013-001/result.json),
SHA `64c2309a6d02f08cd127751e3ad64b44fb3969b8da3e78bd56036485b123bd88`,
termine `MAIN_INSTALLER_UPDATED` à **06:35:04 UTC le 15 octobre**.
`E:/OpenRPG_Forge/OpenRPG-Forge-Setup.exe` est désormais exactement le candidat
vérifié013 : 1 170 618 151 octets, SHA `0eca18e3…`, version
`0.1.0-preview.20261015.013`. Le remplacement est atomique ; la sauvegarde des
anciens octets `5b07587d…` est conservée dans la preuve sous `previous-main.bin`.
Il reste un seul installateur Setup EXE à la racine. Le candidat et les
2 630 pins source consommés sont préservés, sans reconstruction ni mutation
Git. Ce remplacement local ne qualifie ni Windows propre, ni la distribution,
ni Gate 3.

## Contrôles011 échoués et reprise012

Le [natif011](../../../../.gate3-evidence/20261015/closure-common-011/native-result.json),
SHA `c006e4184d78f5ecd41c12178b46754a92fe82610b36e3157738dd5fc1a79ed2`,
est `FAILED`, source75ba stable : format et diff passent, puis Clippy refuse
`collapsible_if` à `camera_perf.rs:450`. Aucun test011 n'est exécuté ; Root11191
est fermé code 1. Le [contrôle supply011](../../../../.gate3-evidence/20261015/closure-common-011/supply-result.json),
SHA `2c2f9b60ac7b39c1f0b45d76425a1c5c99aaf78460cfe9a8dc16f43a3d864e46`,
échoue sur la connexion à la base RustSec/GitHub, sources inchangées ; Root10143
est fermé code 1. Cet échec de connexion ne constitue pas un résultat d'audit
de sécurité ni une nouvelle vulnérabilité constatée.

Le [reçu syntaxique012](../../../../.gate3-evidence/20261015/closure-camera-clippy-012/source-change.json),
SHA `6a405a4693788d840e168a51b35830595bea82634868a053a3a9923e9a659c0d`,
enregistre seulement `camera_perf.rs`, de75ba vers
`69e56a2ce46a7d96f66f4417d5d7cf451800bb790a429ff5fb793178089a6a80`.
La correction utilise le let-chain demandé, avec le même court-circuit et le
même corps ; elle n'assouplit pas le cycle de capture ou les lecteurs.
Le [natif012](../../../../.gate3-evidence/20261015/closure-common-012/native-result.json),
SHA `6a2478bff418770f5aee8ccab962636fd587bd3eb893a1e3ab66482807bbeb51`,
est PASS, sources inchangées et `changed_inputs=[]`. Format, diff et Clippy
passent ; le journal réel décompte 2 197 tests réussis, aucun échec, 56 ignorés
et 232 suites. Root43819 est fermé code 0.

Le [contrôle supply012](../../../../.gate3-evidence/20261015/closure-common-012/supply-result.json),
SHA `a14208ffcca4975ec1ca07e409286805fd00f5c2064cf752fc131331753d840c`,
est PASS, deny/audit codes 0, sources inchangées ; Root75479 est fermé code 0.
L'audit rapporte 0 vulnérabilité et 10 avertissements. Le blocage global de
distribution GLib `RUSTSEC-2024-0429`, nombre1, reste conservé.

Le [WASM012 terminé](../../../../.gate3-evidence/20261015/closure-common-012/wasm-result.json),
SHA `2bc60a1cfefe0da3b6103e7226bb1e807e9d4847b193db2e106776811d3df88a`,
est PASS : engine WebGPU et bundle Studio codes 0, sources inchangées,
`changed_inputs=[]`. Les [replays privés012](../../../../.gate3-evidence/20261015/closure-common-012/private-replays-current.json),
SHA `01bb943614972c2545035a9b703948e63f51195392cfeb6d768047e0bfecc806`,
rapportent 34/34 PASS dans 24 groupes, sources et inputs inchangés. Il s'agit
de véritables réexécutions des EXE sélectionnés par Native012 ;
`new_gpu_execution=false`.

Le [lecteur headless012](../../../../.gate3-evidence/20261015/closure-common-012/native-gameplay-headless.json),
SHA `4a3b51a3f3ae909c95013f4034c4c3416d1f6dedbcf56aeba7dc838cdce734fa`,
rapporte 65 contrats PASS, et le [supplément012](../../../../.gate3-evidence/20261015/closure-common-012/native-gameplay-supplementary.json),
SHA `c9156105ce6357e74cee256b99ff020ec8bed06d40ff94f0686449c29a9d0a14`,
les 13 sélecteurs réels PASS du journal Native012. Ces lecteurs pointent vers
le résultat natif `6a2478bf…`, la même empreinte69e56… et les gardes actuelles.
Ils ne déclarent ni runtime/GPU qualifié ni passage de gate. Release012 est
ensuite fermé PASS ; les acquisitions SDK et caméra012 terminées sont consignées
ci-dessous, sans promouvoir les résultats historiques des autres candidats.
Les contrôles échoués011 et le diagnostic Debug fermé restent historiques.

## Release012 fermé, preuves locales associées

Le [reçu Release012](../../../../.gate3-evidence/20261015/closure-common-012/release-result.json),
SHA `9de9898a1cad0313e4bd98a77d5148ef21e57f8e96e5ad6b5e028884d9d0f268`,
rapporte les trois stages code 0, `status=PASS`, sources69e56… inchangées et
`changed_inputs=[]`. Root97755 est fermé code 0.

| artefact du cache commun | SHA-256 | octets |
|---|---|---:|
| `release/forge_runtime.exe` | `a2f53b22c6a5747bfbd3b95f82faa5af26302ce9efe553cbea17c53ff4e081c4` | 88 428 032 |
| `release/forge-studio.exe` | `4d858b73d05024d04d4e7727b6468e0841d36886176cb08ff913904d1b8407c8` | 184 526 848 |
| `release/examples/tidewater_plant_probe.exe` | `1d4858213e74e64ead31332a65a633e7baa1c951645616137de04b3733271d27` | 105 556 992 |

Ces builds ne qualifient pas le runtime012 et ne remplacent pas les acquisitions
publiques, campagnes et mesures attachées à leurs anciens candidats.
Aucun Cargo supplémentaire ou packaging n'est lancé par ce suivi.
La livraison future préparera un nouveau candidat dans son propre dossier de
preuves, puis sa vérification concrète avant tout remplacement du Setup
principal dont le SHA courant sera vérifié. La vérification d'installation
reste distincte d'une inspection statique d'archive ; aucune nouvelle obligation
Gate4 n'est ajoutée au cahier Gate3.

## SDK et caméra Release012 terminés

Le [SDK012 réel](../../../../.gate3-evidence/20261015/closure-sdk-identity-012/run-001/result.json),
SHA `cefada4ade5e6f504d84ac5894cbf404ad0dd772dbdfbbc089c60b3940819694`,
rapporte `SDK_IDENTITY_REPORTED`, deux snapshots, `failure=null` et sources69e56…
inchangées. Root59445 est fermé code 0. Ce reçu authentifie les identités SDK ;
`metric_qualified=false` et `gate_qualified=false`.

La [caméra Release012](../../../../.gate3-evidence/20261015/closure-camera-prewarm-012-001/result.json),
SHA `fa3cbdd716e3c7f1bea9d7c7cdcb2bf7265f551f5b09e03ea264f752f0638b84`,
rapporte `MEASUREMENTS_REPORTED`, `failure=null`, intégrité de mesure vérifiée
et sources inchangées. Root77770 est fermé ; le vrai PID27492 termine code 0,
sans arrêt forcé. Les mesures appartiennent à cette acquisition Release et à
son périmètre caméra ; elles ne constituent pas une qualification métrique ou
GPU globale de la gate.

Le [collecteur de séparation012](../../../../.gate3-evidence/20261015/closure-camera-prewarm-012-collection-001/result.json),
SHA `e78e017ff282732ea72ea3223e66677d29a41a955c8c7bbc63d3a52cfb7390b1`,
rapporte `PREWARM_SEPARATION_REPORTED`, source69e56… et inputs inchangés.
La préparation finit au frame771 / tick271, avant la capture RPG au frame831 /
tick328, puis la capture caméra/render au frame896. Les 32 waypoints observés
et le retour à la pose sont vérifiés. Aucun readiness forcé ni remise à zéro
d'une capture active n'est introduit ; cette couverture de poses ne prouve pas
toutes les spécialisations GPU possibles. Les champs `runtime_verified`,
`gpu_verified`, `metric_qualified` et `gate_qualified` restent faux dans ce
collecteur.

Les campagnes et acquisitions publiques012 closes sont détaillées ci-dessous ;
aucun ancien PASS UI ou runtime n'est renommé en012.

## Campagnes A–E012 et agrégat clos

Les vrais processus A–E012 sont tous fermés code 0, sans arrêt forcé, sur le
Game Release `1d485821…`. Chaque lecteur rapporte `NATIVE_OBSERVATIONS_READ`,
`missing_required_observations=[]` et la source 69e56… ; son garde externe est
PASS, `source_and_content_unchanged=true`.

| parcours | PID réels clos | observations requises acquises | SHA du lecteur |
|---|---|---|---|
| [A012](../../../../.gate3-evidence/20261013/closure-content-guard-A-015012/assertions.json) | 29312 | boucle RPG, trois lieux, quête/loot complétés | `a72d594ff783bbccc0614db557abcb695261c035290350cbe995143e86d5f38e` |
| [B012](../../../../.gate3-evidence/20261013/closure-content-guard-B-015012/assertions.json) | 21952 | reprise dans un autre processus, état exact et isolation fresh/reprise | `ee0e5afcb3b42b73d875808c45106e6a269273c7643b871f3466016873103611` |
| [C012](../../../../.gate3-evidence/20261013/closure-content-guard-C-015012/assertions.json) | 35176 | reprise compatible après retouche SDK, état préservé | `b0cdaf819cdddfdbeaa292630ca24f16b885158587a5ab428a66569752dc45c6` |
| [D012](../../../../.gate3-evidence/20261013/closure-content-guard-D-015012/assertions.json) | 33412, 22336 | pousser/lancer/impact/chute/destruction/feu/extinction/IA, ACK de retraite ≤1 000 ms et sauvegarde/reprise | `df2bc234a3d8db342bd23370c37b27fa2b39bdb50f5b0e3f963f2853dcacd6b9` |
| [E012](../../../../.gate3-evidence/20261013/closure-content-guard-E-015012/assertions.json) | 3820 | feu, réaction IA causale au même feu et ACK de retraite ≤1 000 ms | `fe09d5ac569cda27f639035a0938e239c12b84aa852795b45605d53b84cb696d` |

L'[agrégat012](../../../../.gate3-evidence/20261013/closure-content-guard-aggregate-015012/assertions.json),
SHA `7aff0029424aff005382a415104429a17192263814d5b84f58295f3f164cc49a`,
satisfait `p01_two_approaches`, sans observation requise manquante et avec
garde source/contenu PASS. La sauvegarde A est réellement conservée,
SHA `5ee00e85aebf9151ada1b10946b0844f8ededc0d2fe6bcace60d2b1b5de119f3`,
12 142 octets. Ces observations techniques ne constituent ni présentation GPU
qualifiée ni verdict de gate ; C reste une retouche SDK, distincte du P02 public.

## P02, P03 et P04 publics012 conservés

La [provenance P02 publique012](../../../../.gate3-evidence/20261015/closure-public-p02-provenance-012.json),
SHA `df2d9373729fc6df53266d0416e91249d3f6e3169b692f1f54cc794f2b4c61f3`,
rapporte `PUBLIC_UI_RETOUCH`, Studio PID 12344 fermé code 0, deux retouches
réelles dialogue puis porte X46.05, Save et export par « Lancer le jeu ».
Le collecteur 56039 et le contrôle 46272 sont fermés code 0. La source, le profil
et les 89 fichiers originaux restent conservés.

Le [runtime P02 public012](../../../../.gate3-evidence/20261015/closure-public-p02-runtime-012-001/result.json),
SHA `775e846240c1ab7ab826f9f50eaf4901a977402f453526f1ca9aac0eb3a6d122`,
rapporte `PUBLIC_P02_OBSERVATIONS_READ`, `failure=null`, sources inchangées.
Root77748 est fermé code 0 ; PID 21512 termine code 0 sans arrêt forcé. La
sauvegarde A012 exacte est reprise après les retouches publiques, avec état
compatible préservé et `physics_revalidated=true`. Ce parcours est acquis
sur012 ; il ne prétend pas qualifier un installateur ou un candidat013.

La [provenance P03 publique012](../../../../.gate3-evidence/20261015/closure-public-p03-ui-012/provenance.json),
SHA `418a1e24e33f95e6d6de08972f133de6f5ab054a610c73afe2ae04aaeabe1663`,
rapporte `PUBLIC_P03_OBSERVATIONS_READ` : sélection réelle du Sceau, refus
visible de X2048 (« Gameplay placement lies outside its canonical terrain »),
correction X18.05, examen, application et Save dans l'autorité workspace gérée.
Le refus client n'est pas transformé en reçu backend ; son champ reste null.
Le seul delta accepté conserve nom/Y/Z/quête/item. Studio PID 34260 est fermé
code 0 ; `source_and_original89_unchanged=true` et `studio_ui_verified=true`.

L'[audit P04 public012](../../../../.gate3-evidence/20261015/closure-public-p04-ui-012/P04-AUDIT.md),
SHA `fd4492f2a9bf18bf4d2128e10efc53f0fed5f03e08c7df3768d413a17747a5b8`,
et son [résultat conservé](../../../../.gate3-evidence/20261015/closure-public-p04-ui-012/local-result.json),
SHA `6c8c34746f7206a4c44a4e5771eb7e5693bade09eccab625a1d95e90d76ea71a`,
attestent les choix publics nouvelle partie → sauvegarder/demander la reprise
stricte → reprise → nouvelle partie sans demande. Les PID 26792, 34456 et 34124
sont réellement clos ; le Save DA9 de 8 625 octets reste préservé après sa
création et la demande stricte est consommée. Le bilan Studio et ses détails
sont ouverts et lisibles, avec retour à la carte. Les captures sont acquises
à Studio 1602×932 et jeu 2560×1440. L'état initial complet n'est pas reconstruit
depuis le HUD : B012 conserve séparément cette preuve technique exacte.

## Smoke navigateur012 acquis, serveur arrêté

Le [résultat navigateur012](../../../../.gate3-evidence/20261015/closure-browser-smoke-012-001/browser-result.json),
SHA `4de9ce6a51e12783bd88b13e42ec90a97ae3dc57273c0be76d047a796cc641c3`,
et l'[audit borné](../../../../.gate3-evidence/20261015/closure-browser-smoke-012-001/BROWSER-AUDIT.md)
conservent les observations et captures Root : WebGPU prêt sans injection,
sélection effective du cube `ae91d914…`, rerenders UI 0→1, viewport démarré une
fois, canvas 768×606 stable 1/1, manifeste de scène apparié et caméras 1/1.
Les logs warn/error après sélection et en mode authoring sont vides. Le
préflight après fermeture rapproche exactement Source012, Studio et les
3 025 fichiers frontend avec les pins avant acquisition.

Le mode authoring autonome affiche l'absence attendue du backend Tauri ; il
ne qualifie pas les commandes natives. Les 120 frames brutes, moyenne 1001.009 ms
dans le contexte IAB en arrière-plan rapporté par Root, restent conservées sans
qualification de performance. Aucun job worker n'est inféré de son chargement
HTTP et la télémétrie conserve `gpu_presentation=not_observed`. Les captures
nommées .png contiennent du JPEG réel 1280×720 ; les octets originaux sont épinglés.

L'onglet 1 est fermé à 23:52:58.785Z, liste restante vide. Le [reçu d'arrêt serveur](../../../../.gate3-evidence/20261015/closure-browser-smoke-012-001/server-stopped.json),
SHA `e961cd0866e3df1aac992391300328e6dbc192ffeddc49d7f9232ea7ce27f1b5`,
conserve l'identité exacte du PID 30652 et son arrêt autorisé par `Stop-Process`
après cette fermeture, code serveur -1 et logs réels. Le port 1420 est libéré ;
aucun tiers n'est interrompu. Aucun verdict global de gate n'est déduit.

## Inventaire de publication013 et limites CI

L'[inventaire013](../../../../.gate3-evidence/20261015/closure-ci-publication-inventory-013-001/inventory.json),
SHA `4c82363cd273b0981d4ef07f35ed814dd1398e4f7f739aa8db80217d42ad3084`,
décrit **175 deltas consommés : 95 fichiers suivis modifiés et 80 non suivis**
sur les 2 630 entrées source4178ac…. Les deux ajouts par rapport à012 sont
exactement `launch_configuration.rs` et `build_delivery.py` ; aucun delta012
n'est retiré. Les 1 347 autres changements ne sont pas inclus implicitement.
Le [CSV révisable](../../../../.gate3-evidence/20261015/closure-ci-publication-inventory-013-001/consumed-deltas.csv)
porte le SHA `e7a1c60cd4b7b54ca7c39fe461e9a70dc3d0f4d3ec03d07f6e024bc602d91701`.
Le tracker documentaire, hors inputs de build, est inventorié séparément.
Le lot proposé est donc **176 fichiers : ces 175 deltas et le présent tracker**,
avec les 1 346 autres changements hors lot préservés. Une nouvelle lecture
locale confirme les 2 630 pins et l'égalité exacte des 175 lignes avec le CSV,
HEAD et snapshot Git stables ; aucun staging n'est effectué.

Le `ls-remote origin refs/heads/Rust` réel de cet inventaire retrouve HEAD
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. Source013 n'est pas publiée ; aucune
indexation, commit, push, fetch ou relance CI n'est réalisée. L'état reste
`INVENTORIED_ONLY_NO_PUBLICATION_AUTHORITY`, `ci_qualified=false` et
`gate_qualified=false`. Il faut une publication explicitement autorisée puis
une CI réellement exécutée sur la révision finale. L'expiration d'ADR-0006
n'est pas annulée par les anciens incidents de facturation.

### Historique012 conservé

L'[inventaire livré](../../../../.gate3-evidence/20261015/closure-ci-publication-inventory-012-001/inventory.json),
SHA `0bfbf9558230995a4bc1a1287cebf8d962fc216b3d66a40c0994d79e6cc6eaf7`,
décrit **173 deltas consommés : 93 fichiers suivis modifiés et 80 non suivis**.
Les 1 349 autres changements ne sont pas inclus implicitement. Le statut reste
`INVENTORIED_ONLY_NO_PUBLICATION_AUTHORITY` : aucune indexation, commit, push
ou relance CI n'est exécuté. Source012 n'est pas publiée.

La [référence distante relue](../../../../.gate3-evidence/20261015/closure-ci-publication-inventory-012-001/remote-ref-admitted.json)
confirme `refs/heads/Rust` et HEAD local à
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. L'[observation CI limitée](../../../../.gate3-evidence/20261015/closure-ci-publication-inventory-012-001/ci-readonly-observation.json),
SHA `4a0a5de7dc2dd9fee620ba61efe967e2d68c391be586abf9278fe16d674e1663`,
retrouve le run connu `38394829662`, quatre jobs `failure`, `steps=null` et
`logs_url=null`. Le jeton CLI gh actif est invalide ; la tentative de lecture
du dernier run retourne HTTP404. Le constat ne confirme ni que ce run est le
dernier ni un motif actuel de facturation. Les anciennes annotations billing
restent historiques ; aucun échec API actuel n'est attribué à la facturation.
Aucun PASS CI, dérogation ou verdict de gate n'est déduit de cet inventaire.

## Correctifs de qualification007 et reprise008

Le [reçu du lot007](../../../../.gate3-evidence/20261014/closure-qualification-fixes-007/source-change.json),
SHA `ed05201c010c2f9721135fac644fcb93f18faafe0a80c150ff30fc03d90f9e91`,
rapporte exactement six fichiers modifiés depuis006, empreinte c3de→
`25f5f236c1ca53af5111bd97b16b191ce10134d9a032d2d5f77fc3d536319c91` :
les quatre fichiers Studio `build.rs`, `capabilities/default.json`,
`permissions/autogenerated/native_game_diagnostics.toml` et `src/main.rs`,
le helper `scripts/delivery/complete_rebuild.py` et l'exemple `camera_perf.rs`.
Le lot expose le RPC de diagnostic déjà enregistré à main/atelier, admet
l'intention bornée des jobs Cargo sans retirer les autres comparaisons
d'environnement, et corrige le cycle de capture caméra : admission stable
durant Idle, invalidations terminales conservées et sortie en erreur avec un
diagnostic séparé. Cinq régressions CPU sont ajoutées ; leur présence ne prouve
pas leur exécution. Le reçu reste `SOURCE_CORRECTIONS_RECORDED_NOT_YET_QUALIFIED`,
runtime/GPU/gate faux.

Le [natif007](../../../../.gate3-evidence/20261014/closure-common-007/native-result.json),
SHA `178b85e51a544159ebb9eedf5f82900cbf91ffffd39a1fc181effcb960680ae5`,
est FAILED, sources stables : format et diff passent, Clippy sort 101 sur
`struct_excessive_bools` et deux `semicolon_if_nothing_returned` dans
`camera_perf.rs`. Aucun stage tests n'est exécuté. Le
[supply007](../../../../.gate3-evidence/20261014/closure-common-007/supply-result.json),
SHA `a339e1aac4c440203b3f12899a72528fdef2d678622d1f97ebb21224a1bfe58c`,
est PASS, sources inchangées : zéro vulnérabilité, dix avertissements et un
blocage global de distribution GLib conservé. Le
[WASM007](../../../../.gate3-evidence/20261014/closure-common-007/wasm-result.json),
SHA `c9c4a5d8a55597b54eca358c56138758b20ac9891811eb73ed1d601a847da05c`,
termine PASS à **19:26:36 UTC**, source stable et inputs inchangés. Les sept
handles de007 sont fermés par le coordinateur ; aucun PASS de test ou de
parcours réel n'est déduit des stages terminés.

Après la fin de007, le [reçu Clippy008](../../../../.gate3-evidence/20261014/closure-camera-clippy-fix-008/source-change.json),
SHA `43e4aa9f4d29e2fac0299d0dd01537e30e71988b1b23441d1464cc047349f8f3`,
documente uniquement `camera_perf.rs` modifié : les phases d'admission
mutuellement exclusives deviennent `AdmissionState`, et les deux instructions
de journalisation reçoivent leur point-virgule. Aucun seuil de délai, garde de
qualification ou contrat de rapport n'est diminué. La nouvelle empreinte est
`187c791625d7aec15517c24d3a121e22ce3b3051ddce4f44f483c210c3e60919`.
Native008/3428, WASM008/55683 et supply008/94770 sont lancés sur cette version ;
leurs résultats terminaux sont désormais acquis ci-dessous. Release et parcours
réels restent à obtenir. Le
P02 public006 et le timeout caméra006 restent attachés à leurs sources/binaires.
Le correctif ACL007 est implémenté et repris dans008 ; son bilan public n'est
pas encore vérifié.

## Contrôles008 terminés, Release et SDK distincts

Le [natif008](../../../../.gate3-evidence/20261014/closure-common-008/native-result.json),
SHA `6abbbff80dc7702cabe21e534097bc2f15ef5724c6930db8c6edabc4e0e4f463`,
est PASS : format, diff et Clippy sortent avec le code 0, puis les tests
terminent à **19:35:38 UTC**. Les résumés réellement présents dans `tests.log`
donnent **2 195 PASS, zéro échec, 56 ignorés, 232 suites**. Source stable,
`changed_inputs=[]`. Les cinq cas `walk::perf::lifecycle_tests` sont réellement
`ok` : admission une seconde/60 frames distinctes, invalidation post-start
terminale, perte de readiness avant capture, changement source/graphique/
viewport/policy, et deadline/primauté des erreurs terminales. Ces PASS CPU
ne qualifient ni une capture caméra réelle ni le bilan public Studio.

Les [34 replays privés008](../../../../.gate3-evidence/20261014/closure-common-008/private-replays-current.json),
SHA `98229810bb7c92b9c1f417dbc7fdabb3445f9c475844706c5d303a6eeb243942`,
terminent PASS dans **24 groupes**, avec sources inchangées et aucune nouvelle
exécution GPU. Les [65 contrats headless008](../../../../.gate3-evidence/20261014/closure-common-008/native-gameplay-headless.json),
SHA `9c84bf041c17e51b72c9f44f2a24801fadd555311a5f2aeabe9bcb5ec06716c2`,
et le [supplément13](../../../../.gate3-evidence/20261014/closure-common-008/native-gameplay-supplementary.json),
SHA `d24c0f246f08180caac037b912439dff2ec7151b26fd7ccaac583b7c0dd68305`,
lisent les tests réellement exécutés sur l'empreinte187c complète donnée en
tête ; les pins sont confirmés avant/après par l'agent responsable. Aucun ancien
PASS006 n'est copié en résultat008. Ces reçus restent runtime/GPU/gate non qualifiés.

Le [supply008](../../../../.gate3-evidence/20261014/closure-common-008/supply-result.json),
SHA `33f400ac467c403726ff299d37466566491f171223acc9fab451aa2a95779d32`,
est PASS, source stable : zéro vulnérabilité, dix avertissements et un blocage
global de distribution GLib conservé. Le
[WASM008](../../../../.gate3-evidence/20261014/closure-common-008/wasm-result.json),
SHA `ff56c0783d077987e08834ae85e5dd677f9506d902840e17a9c336a56382eddf`,
est PASS, source inchangée. Sa fermeture est confirmée par le coordinateur à
**19:43:44 UTC** ; le dernier stage bundle termine à 19:43:01 UTC.
Quatre avertissements connus et la réserve future-compat proc-macro sont consignés.
Le driver [Release008](../../../../.gate3-evidence/20261014/closure-common-008/release-result.json)
démarre à **19:44 UTC**, sous le handle 89226 ; runtime, Studio et jeu sont à
produire et admettre ensemble avant les prochains parcours. Un stage terminé
ne constitue pas encore le PASS des trois Release.

Le [SDK008/run-001](../../../../.gate3-evidence/20261014/closure-sdk-identity-008/run-001/result.json),
SHA `ca1947896e26f8023084325385f6f52633443ab2418703dad2a325a38c967694`,
reste `FAILED_CLOSED` : compilation du helper avec code 0, une sortie brute
de 512 octets, puis refus `Retained bytes changed` sur
`E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002/.rustc_info.json` modifié pendant
le WASM. `snapshots=[]` et `source_unchanged=false` sur la garde de tous les pins ;
aucune identité SDK008 n'est retenue. Ce résultat ne prétend pas que le source
produit187c a changé : le refus concerne la métadonnée Cargo épinglée. La
préparation et l'exécution002 ont attendu la fin des Cargo Release et un GO
explicite ; leur nouveau résultat est donné ci-dessous. Tous les outputs001
restent conservés, sans réparation de leur reçu.

Code, profil et frontend source n'ont pas d'autre changement déclaré dans cette
cohorte. Les parcours publics P02/P03/P04, le bilan ACL et les mesures008 restent
à qualifier après les trois Release ; aucune preuve UI ou runtime008 n'est
déduite des seuls contrôles locaux terminés.

## Release008, SDK002 et A008 acquis

Le [reçu Release008 fermé](../../../../.gate3-evidence/20261014/closure-common-008/release-result.json),
SHA `c7ba978d005531faad0925559cb624f4eac30da7db4fdcc87fb8d796ed9e1490`,
est PASS. Les trois stages sortent avec le code 0, sans erreur de validation,
avec `frontend_unchanged=true` ; le dernier stage Game termine à 19:58:35 UTC,
puis le driver propre 89226 est fermé code 0 à **19:58:45 UTC**. Le manifeste
Release, SHA `dc02e3a6ccc6b8c7a1c35587f546f19ae3a8b5d57489d9a89280c0e95aaedd31`,
porte l'empreinte187c complète donnée en tête, `source_unchanged=true` et
`changed_inputs=[]`. Les pins sont rapprochés physiquement des trois EXE du cache commun :

| Application | SHA-256 Release008 | Octets |
| --- | --- | ---: |
| Runtime de socle | `a2f53b22c6a5747bfbd3b95f82faa5af26302ce9efe553cbea17c53ff4e081c4` | 88 428 032 |
| Studio | `24ba8eff85a2d37558fc0e3ef42aa4c0963bc0843faee54e32e4dde49aa52a60` | 184 526 336 |
| Jeu séparé | `f6b4f4db56612080b125fe979b2c4421d64d174eb01056255ad1523cc9e4e5b5` | 105 505 792 |

Le [SDK008/run-002](../../../../.gate3-evidence/20261014/closure-sdk-identity-008/run-002/result.json),
SHA `a2894f74bf19b51eb8eaa57060f9864859ed49e9d43ba0a369baf94f39958f8f`,
est `SDK_IDENTITY_REPORTED`, `failure=null`, source187c et tous les pins stables.
La compilation du helper et les deux exécutions réelles sortent avec le code 0.
Les archives initiale `fd9ae9bb…`/head `5c5c59e9…` et retouchée
`ed4e1faf…`/head `68e2ad26…` sont inchangées ; archives et deux sorties brutes
de 512 octets correspondent physiquement à leurs pins. Les identités terrain/
végétation réellement rapportées sont respectivement `f8b74156…`/`54f4a4aa…`
et `eeb6759b…`/`bdbaccef…`. Aucune revue UI, mesure ou qualification de gate
n'est déduite de ces deux appels SDK ; le refus001 reste distinct.

Le [résultat A008](../../../../.gate3-evidence/20261013/closure-native-A-014008/result.json),
SHA `452f0fe04c78e7ac25fc7ba2d7fdd2ff132c491896a29d76e4f957e18328678b`,
porte `NATIVE_PROCESSES_CLOSED`, `failure=null`, source inchangée. Le PID 20140
sort normalement avec le code 0, sans arrêt forcé. Ses
[assertions strictes](../../../../.gate3-evidence/20261013/closure-content-guard-A-014008/assertions.json),
SHA `dedeb9a6646612efaaaca7443ef27825d6bdc13f095496bc52e1f95a1eaa83fc`,
lient le candidat Game `f6b4f4db…` à l'empreinte187c et valident commandes RPG,
trois lieux, quête terminée et loot ; aucune observation requise ne manque.
La [garde A008](../../../../.gate3-evidence/20261013/closure-content-guard-A-014008/result.json)
est PASS, `source_and_content_unchanged=true`. Le bilan final, physiquement pinné,
est `ready=true`, tick 17589/séquence 10, quête terminée, santé 100/stamina 75,
sans erreur de commande, de simulation ou de reprise. Le Save réellement
commis `ea3a6faacb6afc4c6d64ffd273510c69839317060d353c028e852cda9e21d1ed`,
12 142 octets, est conservé ; il n'est pas remplacé par le Save historique006.
Les champs GPU/gate du lecteur restent faux.

À ce point de la chronologie, B–E008/agrégat et les parcours publics étaient
encore en cours. Leurs nouveaux résultats acquis ensuite sont donnés ci-dessous,
sans promotion de PASS006 sur008. CI distante exécutée, blocage global GLib et
livraison installable demeurent ouverts.

## Campagnes et parcours publics008 fermés

Les [résultats B](../../../../.gate3-evidence/20261013/closure-native-B-014008/result.json),
[C](../../../../.gate3-evidence/20261013/closure-native-C-014008/result.json),
[D](../../../../.gate3-evidence/20261013/closure-native-D-014008/result.json)
et [E](../../../../.gate3-evidence/20261013/closure-native-E-014008/result.json)
sont `NATIVE_PROCESSES_CLOSED`, sans erreur ni arrêt forcé, tous code 0.
Les rapports terminaux sont `ready=true` et erreurs nulles. Chaque lecteur
porte l'empreinte187c et n'a aucune observation requise manquante ; les gardes
de source/contenu sont PASS. Champs GPU/gate toujours faux.

| Campagne | PID | Tick/séquence terminaux | Assertions acquises |
| --- | --- | --- | --- |
| B | 36776 | 17709/10 | [Reprise du Save A008 dans un nouveau processus, parcours RPG et isolation fresh/reprise](../../../../.gate3-evidence/20261013/closure-content-guard-B-014008/assertions.json) |
| C | 24408 | 17850/10 | [Retouche compatible SDK et état conservé](../../../../.gate3-evidence/20261013/closure-content-guard-C-014008/assertions.json) |
| D | 33816 puis 35884 | 22902/8 puis 23024/8 | [Pousser/lancer/impact/chute ou piège/destruction/feu/extinction/IA, recul ≤1 000 ms et reprise](../../../../.gate3-evidence/20261013/closure-content-guard-D-014008/assertions.json) |
| E | 18128 | 7698/1 | [Feu, IA liée au même danger thermique et recul ≤1 000 ms](../../../../.gate3-evidence/20261013/closure-content-guard-E-014008/assertions.json) |

La [lecture agrégée008](../../../../.gate3-evidence/20261013/closure-content-guard-aggregate-014008/assertions.json),
SHA `2296eec13c2335604e45d26be98f9aae953ffae49d3bc671c5829ab5d89ef872`,
valide `p01_two_approaches`, `missing_required_observations=[]` ; sa
[garde](../../../../.gate3-evidence/20261013/closure-content-guard-aggregate-014008/result.json)
est PASS, source/contenu inchangés. Les deux approches et leurs conséquences
persistantes sont donc rapprochées sur008, distinctement des preuves publiques.

Le [P02 public008 collecté](../../../../.gate3-evidence/20261013/closure-public-p02-ui-014008/provenance.json),
SHA `d1681a3f9ece49d0c7ab7dc36e3629ed23b092eb5d581f2412a3835c5dd3041a`,
conserve deux receipts réels, quatre captures réelles physiquement pinnées,
`studio_ui_verified=true`, source187c inchangée. Studio PID 7140 et son helper
74635 sont fermés code 0 ; le reçu Studio garde source/original/profil inchangés.
Le [résultat de reprise public008](../../../../.gate3-evidence/20261013/closure-public-p02-runtime-014008/result.json),
SHA `4fe03c42b66df4b0ad9022fe3ea754663632dc34f3d6573e0a3f53e7868ba099`,
est `PUBLIC_P02_OBSERVATIONS_READ`, `failure=null`, source inchangée. Le PID 4172
reprend le Save réel `ea3a6faa…` du PID A008/20140, avec physique revalidée et
état conservé ; il sort normalement code 0, sans arrêt forcé. Rapport pinné
`ready=true`, tick 17849/séquence 10, erreurs nulles. GPU/gate non qualifiés par
ce lecteur ; C SDK n'est pas utilisé comme substitut du parcours UI.

L'[audit public P04](../../../../.gate3-evidence/20261014/closure-public-p04-ui-008/P04-AUDIT.md),
SHA `a3dcb282b1b33a88ac14479c9866a68a2b1304dbd84cfee5207e96c3e274da6b`,
conserve les trois fenêtres réellement lancées depuis Studio : PID 23904 fresh,
35200 reprise stricte, puis 9884 fresh. Toutes ferment normalement avec
`ready=true`, erreurs nulles et mêmes source/archive/fonction. Le Save
`052f7236b2d5433563d65bf4e5225bc3ef3a87d3be396678bde1d9c640458b94`,
8 631 octets, reste byte-identique après les trois sessions ; la demande F9
stricte est consommée par l'application, pas par le collecteur. Captures et
feedbacks réels montrent « Sauvegarde reprise » puis « Nouvelle partie ».
Limites explicites : pas de GameState initial complet sur ces sessions
publiques, ni lecture publique du bilan Studio masqué. L'égalité complète du
boot et un PASS global P04 ne sont pas inventés à partir des seuls rapports.

Le masquage du bilan est observé sur le vrai Studio008. Le
[patch externe préparé](../../../../.gate3-evidence/20261014/closure-native-game-report-layout-patch-001/bilan-toolbar.patch),
SHA `1b3cb7d0ed4596699066581ec22a0658736d4b113c48a48fe0089630648dbfaf`,
cible deux fichiers et a été revu par le coordinateur. Après la fermeture en
échec de la baseline caméra008, il est appliqué dans le lot009 décrit plus
haut ; sa vérification visuelle reste due. Aucun résultat de performance
qualifié n'est retenu ici.

Le [staging/adoption de livraison008](../../../../.gate3-evidence/20261014/closure-delivery-adoption-008/adoption-summary.json),
SHA `dba2483485ade97c08030ad60b9465c09871738e4691bfeaf4e3a611f831d749`,
reste `BUILD_ONLY`, source187c stable. Les données préparées portent
**8 226 fichiers / 641 102 992 octets**, workspace P05 géré révision64 et ses
89 fichiers auteur originaux. Les pins du manifeste et du reçu de staging
correspondent physiquement ; le frontend et les deux EXE Studio/Game sont
adoptés depuis les outputs008 authentifiés, avec `commands=[]`, sans Cargo.
Aucun installateur ou packaging n'est exécuté, aucun pack créé ; les familles
assets/module/applications sont seulement préparées. Cette adoption ne prouve
ni installation, runtime distribué, release qualifiée ou passage de gate.
P03 reste dû ; CI/facturation et blocage global de distribution GLib demeurent ouverts.

Deux [copies auteur neuves DATA007](../../../../.gate3-evidence/20261014/closure-studio-ui-data-007/data-preparation.json)
préparent les prochains parcours P02/P03 : chacune contient exactement les
89 fichiers originaux du profil commun, hashes identiques avant/après, sans
lien/reparse ni code/frontend copié. Le reçu `DATA_PREPARED_ONLY`, SHA
`3f8fc9345010b88c1a618ed5a6de99e6fa69106c16339a0efe738abaa086fff0`,
conserve le profil `7dcbcde6…`, le source auteur et les deux anciennes copies008
inchangés, dont P02 réellement retouchée. Ces données sont compatibles avec le
launcher UI008 immuable ; aucune nouvelle preuve UI n'en est déduite.

Le [rafraîchissement CI002](../../../../.gate3-evidence/20261014/closure-ci-refresh-002/README.md),
SHA `396738d297fdab5bca92c7bc41f3b535ccd88a2b8599c63298190f3aa70326ac`,
confirme le run `38394829662` du **13 octobre**, quatre jobs `steps=[]`,
`runner_id=0` et annotations historiques de facturation. La lecture du 14
octobre ne révèle pas un nouveau run ou de nouvelles étapes ; elle ne date pas
les anciennes annotations du 14. `origin/Rust` reste `4799ebb3…`, sans les
correctifs locaux ACL/jobs/caméra. Il faut une publication explicitement
autorisée puis une CI réellement exécutée sur la révision finale ; aucun PASS
distant ou prolongement implicite d'ADR-0006 n'est revendiqué.

Sur la cohorte historique003, B puis sa reproduction identique sont refusés : le Save est chargé exactement,
les quatre étapes terminent et les deux processus sortent normalement, mais
leur bilan final porte `ready=false`. Ce constat a motivé le diagnostic et le
correctif désormais compilé dans006. Ces refus restent conservés ; la reprise
réelle B006 passe désormais avec un bilan final `ready=true`. Ce nouveau succès
ne fournit pas la cause physique initiale des transients de readiness.

Le correctif est désormais dans la cohorte004, empreinte
`d033d51f8e6f4daf2ea3c8cca38a0224defa088356c90bbdb3b99be6f45ca7d1`.
Seuls `native_gameplay.rs` et `native_gameplay/qualification.rs` changent.
Format et diff passent ; Clippy/tests, WASM et sécurité sont lancés sur cette
empreinte. Les builds Release et les nouveaux parcours restent à obtenir.

Native004 s'arrête ensuite à Clippy : `match_same_arms`, branches WaitReady et
Exit identiques après le correctif. Aucun test n'est exécuté dans cette cohorte.
Le regroupement des patterns ne change ni comportement ni délais ; il est
différé jusqu'à la fin du WASM004 pour conserver ses inputs stables. Supply004
est PASS, avec zéro vulnérabilité, dix avertissements et le blocage GLib intact.

WASM004 termine PASS, sources inchangées : quatre avertissements, bundle et
3 000 imports/11 kits authentifiés. Le regroupement WaitReady|Exit est ensuite
appliqué, sans autre changement. La cohorte finale005 est lancée sur
`c3de2a57ad55342daee45d31530d1bcd46ea4a41c745a7ca142aaf338c91e1fc` ;
format et diff passent, Clippy/tests, WASM et supply sont en cours. Le
[relevé final](../../../../.gate3-evidence/20261014/closure-resume-exit-fix-002/source-change.json)
confirme seulement les deux fichiers autorisés modifiés depuis003. Aucun
nouveau binaire ou PASS de reprise n'est encore acquis sur005.

À **16:34 UTC**, format/diff/Clippy et supply005 sont PASS, sources inchangées.
Le check moteur WebGPU est PASS ; le bundle Studio et la suite native sont en
compilation sous les handles propres du driver. Les régressions ne sont pas
encore déclarées exécutées. Aucun nouveau Release, SDK005 ou parcours natif005
n'est lancé avant l'admission des contrôles correspondants.

## Dépendance relancée

État de la reprise suivante : l'objectif est ACTIVE. Les handles native005 et
WASM005 sont absents ; l'inventaire des processus ne trouve aucun Cargo, rustc
ou Trunk de ces compilations. Leurs reçus restent IN_PROGRESS et ne sont pas
réécrits ni présentés comme terminés. La cohorte006 reprend la même empreinte
`c3de2a57ad55342daee45d31530d1bcd46ea4a41c745a7ca142aaf338c91e1fc`.
Format, diff, Clippy et supply006 passent, sources inchangées ; les tests sont
en compilation et le WASM attend le cache partagé. Aucun nouveau SDK, Release
ou parcours006 n'est encore admis. L'absence des anciens handles a été
corroborée par les processus avant toute reprise, sans attribuer une cause
non observée à leur arrêt.

Les trois nouvelles régressions sont maintenant réellement `ok` dans le journal
tests006 : attente de projection, attente du joueur et deadline conservée comme
échec. Le check moteur WebGPU006 termine PASS ; le bundle Studio est lancé.
La suite native complète reste en cours de fin d'exécution et n'est pas admise
avant son reçu final. Ces trois PASS CPU ne remplacent pas la reprise réelle du
jeu Release corrigé.

Native006 termine PASS à **17:01:20 UTC** : **2 190 tests, aucun échec,
56 ignorés et 232 suites**, sur l'empreinte finale c3de, sources inchangées.
Le SDK frais `closure-sdk-identity-002/run-001/result.json` rapporte deux
exécutions réelles de l'API sur les archives initiale/retouchée avec les libs
authentifiés006, statut SDK_IDENTITY_REPORTED, sans erreur ni changement de
source. Les replays privés, la lecture headless et le supplément13 sont lancés
séparément ; ils ne sont pas encore revendiqués PASS ici. Le bundle WASM est
encore en compilation. Aucun jeu Release006 n'est lancé avant son admission.

Les reçus006 des **34 replays privés**, des **65 contrats headless** et des
**13 contrats supplémentaires** terminent PASS, inputs et sources inchangés.
Les treize couvrent observations, refus/retry, les trois chemins de rapport
et les trois nouvelles régressions readiness/Exit. Aucun test CPU ou replay
privé ne qualifie le GPU, les pixels Studio ou la reprise réelle. Le refus de
lecture sandbox initial du pointeur F: est conservé ; la même commande a ensuite
pu lire les inputs autorisés, sans diminuer les gardes.

WASM006 termine PASS à **17:12:37 UTC**, sources inchangées : moteur WebGPU,
bundle Studio, 3 000 imports et 11 kits authentifiés. Quatre avertissements
frontend restent consignés. Le driver des trois Release006 démarre son stage
runtime à **17:19:11 UTC** dans le cache partagé, sans copie du produit.
À ce démarrage, le jeu Release du cache était encore celui de003 : aucun succès historique
de003 n'est relabellisé en preuve du correctif006.

Le [reçu Release006 complet](../../../../.gate3-evidence/20261014/closure-common-006/release-result.json)
est désormais PASS, SHA-256
`65869e04069a94c813898c241631f5d3e1448576aa1e06e1307a42047a5bf483`.
Les trois stages sortent avec le code 0, sans erreur de validation, avec frontend inchangé ;
le dernier termine à **17:51:15 UTC**. Le driver propre 98006 est fermé avec
code 0. Le [manifeste Release](../../../../.gate3-evidence/20261014/closure-common-006/release-source-manifest.json),
SHA `c9785767e65ea03b9a5a68eea298797adebee7eda9018e1434fbfb3633a0978c`,
porte l'empreinte c3de complète donnée en tête et le cache commun
`E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002` ; `source_unchanged=true` et
`changed_inputs=[]`. Les pins effectivement produits sont :

| Application | SHA-256 Release006 | Octets |
| --- | --- | ---: |
| Runtime de socle, `release/forge_runtime.exe` | `a2f53b22c6a5747bfbd3b95f82faa5af26302ce9efe553cbea17c53ff4e081c4` | 88 428 032 |
| Studio, `release/forge-studio.exe` | `2ae49295a5562a27187de64a1bc7e9388b27275c685aef89ea4013a7f2e9c982` | 184 519 680 |
| Jeu séparé, `release/examples/tidewater_plant_probe.exe` | `ace7c41a79dd8c18702665b9757f981e35a446f1d3dcde07967a641c4451ebd9` | 105 476 608 |

A006 a été lancé par le coordinateur sur ce nouveau jeu, sous son handle propre
24740. Il est maintenant fermé, ainsi que B–E et la lecture d'agrégat. Ce relevé
lit leurs reçus terminaux sans lancer de processus. Les anciens résultats A/B
de003 restent attachés à leurs propres EXE et ne qualifient pas006.

Une [observation Sky réelle de A006](../../../../.gate3-evidence/20261014/closure-common-006/ui-observation-A-001/observation.json),
SHA `e4e73e4f640fd2f905b0def2b0928881172759d5246200c2a5cb292fc9144746`,
relie la fenêtre au PID 3596 et au jeu `ace7c41a…`, sans input UI. Sa
[capture JPEG native](../../../../.gate3-evidence/20261014/closure-common-006/ui-observation-A-001/game.jpg),
SHA `820c85d7ff25f01297e83ecd0ac16006607400eb985772cbb5be4b98ee8d06c6`,
montre la quête « Rapporter le Sceau : En cours ». Le compteur ponctuel
96 FPS ne constitue pas une mesure qualifiée. Cette observation intermédiaire
portait tick 13010/séquence 8, avant la fermeture de A ; aucun
lecteur terminal, PASS GPU, réussite de parcours ou acceptation humaine
supplémentaire n'en est déduit.

## Campagnes natives006 fermées

Les résultats des cinq campagnes restent acquis le 14 octobre dans la racine
historique du wrapper du 13 octobre.
Chaque résultat porte `NATIVE_PROCESSES_CLOSED`, `failure=null`,
`source_unchanged=true`, code 0 et `forced_stop=false`. Le candidat est le même
jeu Release `ace7c41a…` de 105 476 608 octets, sur l'empreinte c3de. Tous les
rapports terminaux ont `ready=true`, sans erreur de commande, de simulation ou
de reprise. Les lecteurs portent `NATIVE_OBSERVATIONS_READ`, leurs tableaux
`missing_required_observations` sont vides ; les gardes de sources et contenu
passent. Leurs champs `gpu_verified` et `gate_qualified` restent `false`.

| Campagne et résultat clos | PID | Rapport final tick/séquence | Assertions requises acquises |
| --- | --- | --- | --- |
| [A](../../../../.gate3-evidence/20261013/closure-native-A-014006/result.json) | 3596 | 17658/10 | [Commandes RPG, trois lieux, quête terminée et loot](../../../../.gate3-evidence/20261013/closure-content-guard-A-014006/assertions.json) |
| [B](../../../../.gate3-evidence/20261013/closure-native-B-014006/result.json) | 12640 | 17778/10 | [Reprise dans un nouveau processus et isolation fresh/reprise, avec le parcours A](../../../../.gate3-evidence/20261013/closure-content-guard-B-014006/assertions.json) |
| [C](../../../../.gate3-evidence/20261013/closure-native-C-014006/result.json) | 32728 | 17917/10 | [État conservé après retouche compatible SDK](../../../../.gate3-evidence/20261013/closure-content-guard-C-014006/assertions.json) |
| [D](../../../../.gate3-evidence/20261013/closure-native-D-014006/result.json) | 8112 puis 6012 | 22859/8 puis 22979/8 | [Pousser, lancer, impact, chute/piège, destruction, feu, extinction, IA au danger, recul acquitté ≤1 000 ms et reprise dans un nouveau processus](../../../../.gate3-evidence/20261013/closure-content-guard-D-014006/assertions.json) |
| [E](../../../../.gate3-evidence/20261013/closure-native-E-014006/result.json) | 1796 | 7620/1 | [Feu, IA réagissant au même danger thermique et recul acquitté ≤1 000 ms](../../../../.gate3-evidence/20261013/closure-content-guard-E-014006/assertions.json) |

Les gardes fermées sont conservées pour [A](../../../../.gate3-evidence/20261013/closure-content-guard-A-014006/result.json),
[B](../../../../.gate3-evidence/20261013/closure-content-guard-B-014006/result.json),
[C](../../../../.gate3-evidence/20261013/closure-content-guard-C-014006/result.json),
[D](../../../../.gate3-evidence/20261013/closure-content-guard-D-014006/result.json)
et [E](../../../../.gate3-evidence/20261013/closure-content-guard-E-014006/result.json),
toutes avec `source_and_content_unchanged=true`. Le Save réellement commis dans
A, [copie exacte archivée](../../../../.gate3-evidence/20261013/closure-native-A-014006/run-1/saved-captures/d00004d19ebe3d9452fa0a5e47c6ce73785d973579201d70f8155c81a1b2be2d.json),
a pour SHA-256 `d00004d19ebe3d9452fa0a5e47c6ce73785d973579201d70f8155c81a1b2be2d`,
12 156 octets et état `43a3578d23575b5618ae56fd64b1d5d335ced0ba5ee8e391c43b94d0b31f8674`.
B reprend ces octets dans un autre processus, avec le même head `5c5c59e9…` et
la fonction RPG `b041bae1…`. Son [rapport final](../../../../.gate3-evidence/20261013/closure-native-B-014006/run-1/game-report.json),
SHA `003d2c7096eaace9715687821a6df5e870392a0ef582490b0b054f41701b52ba`,
confirme `ready=true`. Le cas de reprise/sortie corrigé est donc réellement
validé sur006 ; les refus de003 et la cause initiale non identifiée restent visibles.

C reprend le même Save après changement SDK de la réplique et de la porte,
sur head `68e2ad26…`, avec hash fonctionnel inchangé. Le lecteur vérifie les
normalisations autorisées et la conservation du texte, des liens et de l'état.
Son champ `studio_ui_verified=false` reste explicite : aucun PASS de retouche
depuis l'interface publique P02, de diagnostic public P03 ou de sélection de
replay/révision publique P04 n'en est déduit. D reprend son propre dernier Save
`37524b46550d4eefe0bd5e2907a895a348f9048713b1e92f5e5d2f46f5a466d1`
dans le PID 6012 ; les deux bilans conservent santé 88 et quête terminée.

La [lecture agrégée A+B+D](../../../../.gate3-evidence/20261013/closure-content-guard-aggregate-014006/assertions.json),
SHA `4200ced5b4bcbf63cdf93ed0b69ac28e88033b3223bc143c363f1eba1c3c9ebf`,
valide `p01_two_approaches`, sans observation requise manquante. Elle lie la
défaite directe et la défaite environnementale du même ennemi à des sessions
fresh distinctes, avec conséquence persistante et états auteur initiaux exacts.
Sa [garde](../../../../.gate3-evidence/20261013/closure-content-guard-aggregate-014006/result.json)
est PASS, sources et contenu inchangés. L'isolation technique fresh/reprise est
acquise. À ce stade de la chronologie, les contrôles publics P02–P04, le smoke
Studio et la CI finale étaient encore dus ; le P02 acquis ensuite sur006 est
consigné ci-dessous et reste distinct du nouveau candidat007.

## Baseline caméra006 et portée des mesures

Le [résultat caméra006](../../../../.gate3-evidence/20261014/closure-camera-perf-006-001/result.json),
SHA `a1d69eb63559d77ce4f542d72e533c9c56c4ab0a19105dbda77fb12c3050f3a6`,
est `FAILED_CLOSED` : `TimeoutError: Camera performance exceeded 600 s`.
Le PID propre 14704 a été arrêté par le wrapper (`forced_stop=true`, code 1),
après environ 606,94 s comprenant la fermeture. Les sources sont inchangées et
le candidat reste `ace7c41a…`. Aucun `camera.perf-proof.json` n'est produit ;
le rapport de jeu fait 0 octet et `game_report=null`. Intégrité de mesure,
qualification métrique et qualification de gate restent `false`. Aucune cause
GPU, valeur FPS/p95/1 % low, durée de simulation ou coût de navigation n'est
déduit de cet échec ; il n'invalide pas les campagnes A–E et l'agrégat clos.

La [roadmap R3](../../planning/MIGRATION_ROADMAP.md#gate-3--première-boucle-rpg-environnementale)
exige PNJ/navigation, IA au danger et les parcours P01–P05. Elle n'impose ni
séquence caméra de 32 s, ni timestamps dispatch/publication de tuiles, ni
nouveau seuil FPS propre à Gate 3. Le [plan du 10 octobre](../../planning/GATE3_PREPARATION_PLAN.md)
précise que G3-01 à G3-10 sont des lots proposés, pas de nouveaux critères
officiels (ligne 15). Ses mesures de génération/reconstruction (151) et de
fluidité/attentes/budgets de simulation/coût de navigation (204) détaillent une
méthode de qualification, sans rendre ce profiler précis obligatoire.

Le [contexte canonique](../../../PROJECT_CONTEXT.md) conserve toutefois les
budgets et leur visibilité : p95 total 16,7 ms, sous-budgets CPU/GPU indicatifs
et mesures après warm-up avec p95/1 % low (1694–1720). Les propriétés tuiles,
reconstruction partielle et requêtes asynchrones bornées restent de vrais
contrats de navigation (1052–1062), repris par
[ADR-0009 accepté](../../ADR/ADR-0009-navigation-provider-benchmark.md) (28–39).
La garde causale de recul ≤1 000 ms passe sur D/E avec les exigences existantes ;
elle ne mesure pas les FPS, le budget de simulation ou le coût de navigation.
Les valeurs encore indisponibles restent déclarées, sans ajout de critère
automatique ni abandon du diagnostic fonctionnel de porte. L'acceptation du
[jeu séparé du 13 octobre](GATE3_OWNER_RPG_ACCEPTANCE_20261013.md) ne recrée ni
le playtest intégré retiré le 12 octobre, ni une qualification CI ou métrique.
Le retrait n'accorde pas de dérogation de fluidité (roadmap, ligne 27). Les
objectifs O2 de la tranche de référence restent dans leur périmètre canonique
(contexte 630–644, critère Gate 1 à 1597) ; ils ne sont ni déclarés satisfaits
par D/E, ni transformés en une nouvelle case numérique R3.

La sous-fixture FireStart est réellement créée par une seule commande publique
`prepare_start`, avec preview identique à apply, révision 63→64, gameplay et
11 blobs inchangés. Le départ candidat `(18,-271)` est à environ 1,893 m du
foyer selon l'échantillonnage SDK ; cette distance ne prouve pas l'occupation
Rapier. Archive `1690ac48…`, head `7e88f72f…`, identité terrain `eeb6759b…`
et végétation `bdbaccef…`, fonction RPG `b041bae1…`. Son reçu reste une preuve
SDK/données, sans jeu, collision runtime, mesure GPU ou clôture.
Le [reçu de création FireStart](../../../../.gate3-evidence/20261014/closure-fire-start-fixture-001/run-001/result.json),
SHA `6f96472d2035f081b8a3e80e36153c96410914665a5dba4b7a24f8f915bae5f8`,
et son [identité SDK006 réelle](../../../../.gate3-evidence/20261014/closure-fire-start-fixture-001/run-001/sdk-identity-result.json),
SHA `cf394e0de330e26bee25ba7ecfe1bd40a9f9ce1a40f987be994518c94d547ab0`,
conservent les sources c3de inchangées. Le [plan FireStart de cinq étapes](../../../../.gate3-evidence/20261014/closure-fire-start-measurement-plan-001/README.md)
est gelé au SHA `dd95ee68df88f2e4b2cccd4724b20639eb5e01192a98ffe4026af4efb1e43cd8`.
Il prépare une ignition dans la fenêtre du profiler, puis attend son auto-exit
32 s via le [wrapper feu002](../../../../.gate3-evidence/20261014/closure-fire-replan-protocol-002/README.md),
SHA `cb6b61ae8e775218eafafbbdd3614745df6c1325ef73e6dec1f7b0a6d39f9e96`.
Ce préfixe ne prouve ni campagne complète, ni population vivante constante,
ni retrait acquitté en 1 000 ms, ni attribution temporelle job→feu. L'ignition,
les phases mesurées et la population réellement observée restent à acquérir.

L'archive de corridor historique est également relue par l'API publique réelle
avec le consumer et les bibliothèques006, sous
`closure-corridor-sdk-identity-006/run-001`. Ses pins et identités restent
inchangés (`9dc6eb22…`, `98cfedcb…`, `6d25566d…`) ; aucun authoring n'est rejoué
et aucun NoPath/ouverture/Reached natif n'en est déduit.
Ce [reçu SDK corridor006](../../../../.gate3-evidence/20261014/closure-corridor-sdk-identity-006/run-001/result.json)
porte le SHA `1a4b449307a9ff0c6c9de142e634c01d1fa83e5ea16c1778095804a658e3a641`.
Le [wrapper corridor002](../../../../.gate3-evidence/20261014/closure-door-replan-protocol-002/README.md),
SHA `6290ee4a29a369742009022caa672d972d04c7ebed37d0fd88127933e1ebd712`,
et son plan `7b8f86346935c0795209bf0a3da727c502095f1f4e5591fb936dfbc3e2214c79`
préparent une observation de porte avec timing réel, bornée par l'auto-exit
caméra. Le [lecteur ordinaire003](../../../../.gate3-evidence/20261014/closure-door-replan-protocol-003/README.md),
SHA `d1f8ea7895e4eda0d39d3f9d3b5d19b7d716069ea821e09deca2c6ace082ec7a`,
corrige la déstructuration du tuple réel `(rows, sha256)` et contrôle le hash
des octets lus, sans modifier les gardes pures du protocole001 utilisé par002.
Les NoPath à échantillons valides, ouverture commise, mouvement KCC et
Reached du même Messager restent des observations dues. Les jobs hors capture,
paires absentes et champs dispatch/publication non exportés ne sont pas
inventés ; `replan_delay_ms` reste indisponible sans preuve temporelle.

Le [collecteur public P02](../../../../.gate3-evidence/20261014/closure-public-ui-collector-001/README.md)
est gelé, SHA `b2bd1d51314359ff5768db8b312eb21136ed4b189194f0c75a8db834a787dd9f`.
Ses six tests de garde Python réussissent, après conservation du premier refus
Temp sandbox ; la contrelecture indépendante ne trouve plus de défaut concret.
Lors de cette préparation, il n'avait pas collecté de provenance UI réelle.
Le protocole sélectionne, après vrais clics et fermeture normale de Studio,
les deux receipts effectivement ajoutés
(réplique, puis porte X=46→46.05), vérifiera audit/captures Sky, archive gérée,
sources, Release, frontend, profil et Save/F9 réel de A006 avec les validateurs
publics immuables, puis écrira un JSON neuf. La racine prévue est
`E:/OpenRPG_Forge/.gate3-evidence/20261013/closure-public-p02-ui-014006` ;
son parent englobe le launcher008 d'origine sans copie de code. Aucun audit,
receipt, capture UI, sauvegarde synthétique, reprise native ou PASS P02–P04
n'est produit par cette préparation.
La surface actuelle ne présente pas de bouton « Export » trouvé par le
coordinateur. L'opération d'audit `export` peut désigner le vrai geste
« Lancer le jeu », qui écrit le snapshot via `native_game.rs`, seulement après
observation de ce geste et vérification de l'égalité entre Save, référence
gérée, objet adressé et export réel. Aucun bouton ou succès implicite n'est
créé pour satisfaire le contrat ; le collecteur gelé reste inchangé.

## P02 public acquis sur006

Le coordinateur a effectué les deux retouches dans le Studio public PID 5280,
puis « Sauvegarder » et « Lancer le jeu », avec l'entrée UI immuable008 et les
EXE Release006. La fenêtre native enfant PID 22844 a été réellement observée
puis fermée ; Studio est également fermé normalement, code 0, sans arrêt forcé.
Le [résultat de session Studio](../../../../.gate3-evidence/20261013/closure-public-p02-ui-014006/studio-session/result.json)
conserve `source_original_and_profile_unchanged=true`. Les captures et actions
sont liées par [l'audit Sky002](../../../../.gate3-evidence/20261013/closure-public-p02-ui-014006/sky-ui-audit-002.json),
SHA `4480a02e6adfbdeec1f555c862fd06eaeda8ab720e57bc5e136f3c62552d3c94`,
avec `official_computer_use_sky_only`, sans modification du produit ni authoring SDK.

La [provenance réellement collectée](../../../../.gate3-evidence/20261013/closure-public-p02-ui-014006/provenance.json),
SHA `5370ba868220d156780bde03a90402deb42d65ab41dce482a80eb8ed22ee034a`,
porte `PUBLIC_UI_RETOUCH`, `studio_ui_verified=true` et source c3de inchangée
pendant l'acquisition. La contrelecture indépendante vérifie physiquement les
pins ciblés Studio/Game, profil, launcher, audit, collecteur, session, captures,
autorité gérée, objet exporté, rapport et Save. Le diff est limité à la réplique
du nœud `0468a357…`, à la porte `49ca258b…` X=46→46.04999923706055 et à la
révision 63→65 : les 62 receipts antérieurs sont identiques, les deux ajouts
sont exactement ceux de la provenance. Objet du workspace géré et snapshot
exporté ont les mêmes octets, SHA
`5816cee7cf5f25f6714225c92cb874871563d1ea4fcbfe710020d270590b2a96`,
head `8965db132eb605d45af6d6d398b644666cdb909d1408d2d83e5ca79ea4b2908e`,
fonction RPG `b041bae1…` inchangée ; le legacy area JSON n'est pas utilisé comme autorité.

Le [résultat de reprise P02 public](../../../../.gate3-evidence/20261013/closure-public-p02-runtime-014006/result.json),
SHA `cbb66af8befcc7aa9677d216702a737b11033c53b75984f41659f76136a0a093`,
est `PUBLIC_P02_OBSERVATIONS_READ`, `failure=null`, source inchangée. Le nouveau
PID 12452 charge explicitement le Save A006 `d00004d1…` du PID 3596 :
`resumed=true`, refus nul, état exactement conservé après les seules
normalisations documentées de physique/pose de porte. Les liens, le texte et la
quête restent conservés. Le bilan final est `ready=true`, tick 17918/séquence 10,
quête terminée, santé 100 et stamina 75, sans erreur de commande ou de simulation.
Le processus sort normalement avec code 0 ; les octets du nouveau Save
`ae55467c0e966f49caaaa6a9e2ff54e8a50190aab99d3498941d947725d918ed`
sont conservés. `studio_ui_provenance_verified` et
`gameplay_observations_verified` sont vrais ; les champs `runtime_verified`,
`gpu_verified` et `gate_qualified` restent faux, selon le périmètre du protocole.

L'audit public conserve le défaut observé « Command native_game_diagnostics not
allowed by ACL » : le bilan du dernier jeu est inaccessible dans Studio,
malgré le retour sur la carte. Ce défaut ne devient pas un PASS de bilan public.
Le P02 ci-dessus était entièrement fermé avant la modification ACL de 18:59 UTC
et reste une preuve historique exacte du candidat006 c3de. Après l'échec
Clippy007, la cohorte008 doit produire ses propres contrôles et preuves finales,
sans retaguer006. P03/P04
publics, smoke Studio et CI finale restent distincts ; aucune clôture de gate
n'est déduite de ce seul P02.

La revue datée des critères conserve la décision propriétaire du 12 octobre :
pas de réactivation du playtest intégré ni de nouveau client Web pour R3.
Les formulations navigateur du plan du 10 octobre sont antérieures à ce retrait
et aux exigences du [jeu séparé acceptées le 13 octobre](GATE3_OWNER_RPG_ACCEPTANCE_20261013.md).
Le smoke public du viewport WASM/WebGPU et de l'authoring Studio courant était
alors dû avec captures réelles ; son acquisition012 est consignée plus haut.
PNJ/navigation est qualifié dans le jeu séparé.
Le transport Worker conservé n'est pas exercé par le viewport seul : sa création
attend un dispatch, et le bridge gameplay intégré est retiré. L'absence de job
Worker navigateur reste une limite de composant déclarée, sans réutilisation
du PASS intégré historique ni transformation en critère Gate 4.

À la reprise, le chat « Évaluer modèles Unreal pour OpenRPG » était interrompu
et aucun Cargo, compilateur, jeu ou exemple de rendu recherché n'était actif.
Sur autorisation explicite du propriétaire, la consigne de reprise lui a été
envoyée : vérifier les derniers replays, terminer les raccords restants de son
objectif puis consigner la fin des mises à jour pour permettre Gate 3.

La reprise est confirmée par le nouveau tour
`01a13929-1312-7c93-acfb-baf0a6fc5db8`, dans le chat
`01a0d46d-3557-7382-83d8-303cd622e619`. Le chantier confirme le travail sur la
version commune et la conservation du lot Gate 3 gelé. Cette activation ne
constitue pas une preuve de fin du rendu ni une qualification de ses assets.
L'objectif Gate 3 a été bloqué le matin sur cette dépendance prioritaire.
Le chantier de rendu relancé est resté actif et n'a pas été interrompu par ce
statut historique. La reprise de l'après-midi est décrite ci-dessous.

Revalidation du 14 octobre à **07:08:03 UTC** : le test Cargo PID25588 et son
compilateur ont été observés directement, puis le test s'est terminé. Clippy
PID30584 a ensuite été confirmé LIVE par une attente de 30 secondes, avant
terminaison. Le nouveau contrôle WASM du moteur PID10436, créé à 07:07:41 UTC,
est actif. Le chantier indique encore les vérificateurs GPU, la publication
stable et le raccord du dessin continu à prouver. Une étape terminée ne prouve
pas la fin des mises à jour de rendu.

La condition de version commune finalisée persiste sur le tour de reprise et
ses trois continuations. Les inspections et préparations indépendantes sont
terminées. La qualification ne peut reprendre avant cette évolution externe,
conformément à l'ordre confirmé par le propriétaire. Ce statut ne déclare ni
échec du rendu, ni clôture de Gate 3, ni réouverture de P05.

## Préparations revérifiées

Checkout unique : `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`. HEAD, `origin/Rust` et
branche distante demeurent `4799ebb3dfa98795d97862f0fed3cd4f6a876887`, avec
travaux locaux conservés. Les nouveaux changements d'assets et de rendu imposent
une nouvelle cohorte de sources et de binaires ; HEAD ne suffit pas à identifier
le candidat final.

- Les deux fichiers corrigés et le reste du lot RPG/mesures sont inchangés.
  Les [pins et trois tests CPU/Clippy ciblé du 13](../../../../.gate3-evidence/20261013/closure-window-exit-order-002/owned-files-frozen.json)
  sont conservés. Aucun nouveau PASS global n'est déduit de cette comparaison.
- Profil commun `7dcbcde6…`, launcher `de93abbd…`, catalogue `1f6df0c3…` et
  archive Studio originale `fd9ae9bb…` sont inchangés. Les racines authoring,
  contenu, données, sessions et AssetVault sont disponibles ; authoring conserve
  89 fichiers. Le jeu Release présent reste `62e59699…`, antérieur au correctif
  d'ordre des bilans ; il n'est pas un nouveau candidat qualifié.
- Les douze fichiers de chacune des
  [fixtures](../../../../.gate3-evidence/20261013/closure-native-fixtures-001/initial/fixture-provenance.json)
  et les six [plans](../../../../.gate3-evidence/20261013/closure-native-scenarios-001/preparation-v4.json)
  conservent leurs pins : 68/4/87/4/9/24 étapes, 600 s maximum et Exit final.
- Runner, Validator, lecteur JSONL, driver et replay privé sont inchangés.
  Les contrats runtime/report/save et les trois manifestes privés préparés
  gardent leurs identités. Aucune adaptation de protocole n'est nécessaire à ce
  stade ; les replays, contrats et gardes existants restent exigés.
- Les préparations actives ne dépendent pas des anciennes références
  `.tidewater-integration`. Aucun profil ni contenu auteur n'a été modifié par
  cette inspection ; aucun inventaire exhaustif des milliers d'assets n'est
  revendiqué.

## CI et reprise de qualification

Le [constat CI/sécurité du 14 octobre](../../../../.gate3-evidence/20261014/closure-ci-refresh-001/README.md)
confirme le même run distant, quatre jobs sans étapes exécutées. Les annotations
ne sont pas accessibles aujourd'hui ; le motif billing documenté le 13 pour
ce même run n'est pas présenté comme une nouvelle lecture.
Les 24 inputs du correctif ruzstd sont identiques ; le blocage de distribution
GLib est conservé. Cette inspection ne rafraîchit pas les advisories et ne
constitue pas un nouveau PASS supply.

Sur les inputs communs actuels désormais autorisés : native/supply/WASM,
trois builds Release et pins SDK/bibliothèques courants, inspection SDK réelle,
campagnes A–E, parcours publics Studio et mesures sur cette même version.
La fermeture prématurée D001 reste à revérifier avec le jeu recompilé.
Les preuves de publication et de CI réellement exécutée restent dues selon
leurs règles. Aucun build de qualification ou publication n'est lancé par
la présente inspection.

## Qualification de la version actuelle autorisée l'après-midi

À **14:06 UTC**, le propriétaire répond « Qualifier la version actuelle » à
la clarification portant sur le report des modifications moteur/Studio après
la conversion des modèles. Le chantier « Évaluer modèles Unreal pour OpenRPG »
est actif et travaille sur les modèles externes, avec les lots moteur/Studio
placés en dernière phase. Gate 3 ne doit plus attendre cette phase tardive.
Son objectif est actif ; cette autorisation ne vaut pas clôture de gate.

L'audit de compatibilité n'identifie aucune régénération nécessaire des fixtures
ou plans préparés. Les douze fichiers de chaque fixture conservent leurs pins.
Le champ optionnel `ManualPlacement.particles` est absent de ces archives et
possède les protections serde `default` et `skip_serializing_if=None` ; les
placements restent sur leur chemin sans paquet particules. Les contrats RPG,
sauvegardes, observations et rapports sont inchangés par rapport aux pins du
correctif WindowExit002. Les deux fichiers principaux ont été revérifiés :
`native_gameplay.rs` = `d33ef0c0…`, `native_gameplay/tests.rs` = `76ff6866…`.
Il s'agit d'un contrôle de sources et de pins, pas d'une preuve runtime actuelle.

La relecture CI de **13:56:45 UTC** retrouve le même run et les quatre jobs sans
étapes exécutées. La référence distante est confirmée par l'API GitHub ; la
lecture `git ls-remote` échoue dans le gestionnaire d'identifiants Windows.
La lecture renouvelée des annotations retourne HTTP 404. Aucun nouveau PASS
distant, motif billing nouvellement lu ou dérogation n'est déduit de ce relevé.

La nouvelle cohorte de qualification est
`E:/OpenRPG_Forge/.gate3-evidence/20261014/closure-common-001`. Elle utilise le
checkout et le cache Cargo communs, des inventaires complets avant/après et les
gardes existantes. Les nouveaux reçus ne remplacent aucun résultat historique.
Les compilations puis les parcours doivent produire leurs propres résultats
sur les mêmes entrées admises ; toute modification consommée invalide le
contrôle concerné.

La première cohorte conserve `format` PASS et `diff` FAILED : 13 525 fins de
ligne CRLF dans 33 fichiers Rust, sans espaces finaux supplémentaires. Sa
sécurité passe avec `vulnerabilities=0`, `warnings=10` et un blocage de
distribution GLib conservé. Aucun Clippy ni test natif n'a été exécuté après
l'arrêt de `diff`. La correction est strictement CRLF → LF, avec hashes et
vérification de chaque input avant écriture dans
`E:/OpenRPG_Forge/.gate3-evidence/20261014/closure-line-endings-001/result.json`.
Les contrôles reprennent dans une nouvelle cohorte `closure-common-002` ; les
reçus de `closure-common-001` restent attachés à leurs octets initiaux.

`closure-common-002` passe format, diff, Clippy et sécurité. Sa suite native
est encore en compilation lors du raccord final de livraison : l'adoption du
jeu qualifié se fait maintenant depuis l'EXE Release authentifié, avec les
mêmes gardes de chaîne que Studio. Le fallback Studio sélectionne explicitement
`studio-release` ; le dernier stage de la liste est le jeu et ne doit pas servir
de substitut. Ces corrections concernent uniquement
`scripts/delivery/complete_rebuild.py` et `build_classic_installer.py`, sans
modification d'application, de launcher ou de politique de distribution.
Les assertions Python portent sur le dispatch et les gardes de ces scripts,
avec commandes simulées ; elles ne qualifient aucun EXE.

Ces deux inputs consommés ayant changé après l'inventaire de la cohorte002,
ses reçus restent historiques. La cohorte finale devient `closure-common-003`
après gel des scripts ; les binaires du cache commun pourront être réutilisés
par Cargo, tandis que les reçus et contrôles seront frais sur ce manifeste.

## Résultats frais sur la cohorte003

Le manifeste consommé est `a45c325148545d7d0e181c51d43f4a829cb920a7e82a9b1b868f4d3272533294`.
Format, diff, Clippy et suite native sont PASS, avec **2 187 tests, aucun échec,
56 ignorés et 232 suites**, sources inchangées. La sécurité est également PASS ;
son blocage de distribution GLib reste distinct de ce verdict.
Les **34 replays privés**, les **65 contrats headless** et
les **10 contrats supplémentaires réellement lus dans la suite engine** passent
sur ce même manifeste. Les dix derniers couvrent observations, refus/retry
d'actions et les trois chemins de sortie ; aucun test n'est relancé pour ce
relevé et aucune exécution GPU n'en est déduite.

Le [contrôle SDK réel](../../../../.gate3-evidence/20261014/closure-sdk-identity-001/run-001/result.json)
réussit avec les bibliothèques natives et le compilateur authentifiés : archives
initiale et retouchée intactes, deux appels de l'API publique, sources inchangées.
Les identités sont terrain `f8b74156…`/végétation `54f4a4aa…` pour l'initiale,
terrain `eeb6759b…`/végétation `bdbaccef…` pour la retouchée. Cette différence est
attendue : `effective_unchecked()` reporte les révisions 63/64 dans ces deux
documents, dont les hashes comprennent la révision. Le `functional_hash` RPG
`b041bae1…` reste commun parce que textes et pose initiale des portes en sont
exclus. La reprise C doit vérifier sa propre identité, revalider sa frame
physique et conserver le Save A authentique ; elle reste à exécuter réellement.

Le moteur WebGPU/WASM et le bundle Studio sont désormais PASS sur ces mêmes
sources, avec 3 000 fichiers `forge-imports` et 11 fichiers `forge-kit` vérifiés
par SHA-256. La compilation frontend signale quatre avertissements, sans échec ;
le reçu WASM complet confirme les sources inchangées et les assets canoniques.
Les trois applications Release sont lancées dans le cache commun : le stage
`runtime-release` a démarré à **15:00:18 UTC**. Studio et jeu seront ensuite
compilés par ce même driver. Le court diagnostic d'arrêt, A–E, les parcours
publics Studio et les mesures restent à acquérir. Gate 3 reste ouverte.

Reprise de compilation vérifiée à **15:17 UTC** : `runtime-release` PASS,
sources et frontend inchangés, EXE `forge_runtime.exe` de 88 428 032 octets,
SHA-256 `223baf04de723fe2678e4d23c7c2e47efce0a27e8fe4c59f338aa09d2f60b0ac`.
`studio-release` est actif depuis 15:13:54 UTC ; le driver terminera ensuite
`native-game-release`. Le reçu Release demeure `IN_PROGRESS` : un seul artefact
ne remplace pas l'admission des trois applications.

La revue indépendante des critères ajoute à la liste d'exécution une preuve
distincte de navigation : porte fermée NoPath → ouverture/replanification →
Reached, vraie KCC et révisions géométrie/navigation. Le trajet historique du
Messager contourne la porte et n'est pas une preuve de ce blocage. La préparation
de cette observation n'est pas un PASS ; aucune trace, pose ou conséquence
synthétique ne complète les parcours A–E.

À la reprise suivante, `studio-release` termine PASS, sources inchangées :
EXE `forge-studio.exe` de 184 519 680 octets, SHA-256
`2344d48f281f33759e66bdfa6d37d369527e60f0194b127ef7d4ec04fda4e4b8`.
Le stage jeu a commencé à **15:18:33 UTC**. À **15:29 UTC**, le handle du driver
est encore actif et le compilateur PID13336, enfant de Cargo PID24004, compile
effectivement `tidewater_plant_probe` depuis 15:25:02 UTC. Aucun restart n'est
déduit d'un journal silencieux ; le reçu Release reste incomplet jusqu'à cette
troisième sortie.

Le [plan exploratoire de porte](../../../../.gate3-evidence/20261014/closure-door-navigation-plan-001/README.md)
prépare 17 étapes et les assertions causales attendues. Il ne garantit pas NoPath
avec la géométrie initiale. La sortie ordinaire expose les acteurs et KCC, mais
pas les révisions de tiles ; l'export existant `camera_perf.rpg_timing.jobs`
peut fournir des publications réelles dans une session opt-in distincte. Les
tiles du World Studio ne remplacent pas ceux du jeu séparé. Toute sous-fixture
authoring devra garder ses propres pins et une nouvelle partie ; A/C restent
intacts. Aucun nouveau témoin causal ou résultat GPU n'est acquis par ce plan.

Le troisième stage Release termine PASS : `tidewater_plant_probe.exe`,
105 476 096 octets, SHA-256
`35ef02e6c9b2e1547bc33323448913afdc1b5dd306dfc8ba7171abf7155bf1a8`.
Le reçu Release complet admet les trois applications sur le manifeste003,
avec sources et frontend inchangés.

Le diagnostic réel `closure-common-003/lifecycle-001` termine sans arrêt forcé,
PID6488, rapport `ready=true`, sortie réussie au tick123. Le lecteur strict
`reader-002.json` confirme les pins archive/head, scénario, processus et reçu,
avec 15 frames prêtes. Le premier appel du lecteur avait reçu le dossier de
campagne au lieu de `result.json` et est conservé comme erreur d'orchestration ;
le jeu n'a pas été relancé. Ce diagnostic ne revendique aucun contrat RPG,
parcours Studio, performance ou clôture de gate.

Le parcours A est lancé sous `closure-native-A-014001`, avec garde de contenu
`closure-content-guard-A-014001` et reçu stdout dans la cohorte003. Une capture
native de sa fenêtre, conservée dans `ui-observation-A-001`, montre Tidewater,
le HUD et la quête « Rapporter le Sceau » en cours. Le compteur ponctuel
103 FPS / 9,7 ms par image n'est pas une mesure qualifiée. Aucun input UI,
acceptation humaine supplémentaire ou verdict de parcours n'en est déduit.

Le parcours A termine PASS, PID17800, sortie normale tick16991, 10 actions
commises, quête `c266343e…` complétée et trois lieux observés. Les assertions
`rpg_commands`, `p01_three_places` et `p01_completed_loot_quest` sont vraies ;
le lecteur n'infère ni seconde approche environnementale ni reprise. Le Save
authentique est `8a009814ef6fd5b76a6b22bff1ea6b308ffb6c1d6fb9be38665ff43a90635957`,
état `f17decf8…`, avec capture byte-exacte liée au reçu. Les 68 étapes sont
terminées et les sources/contenu communs restent inchangés. B utilise ces mêmes
octets dans un autre processus ; son lancement ne constitue pas encore un PASS.

B-014001 (PID11288) et `closure-resume-diagnostic-002` (PID12760) reproduisent
le même refus du rapport final : tick17111, séquence10, `ready=false`, sans
erreur de simulation ou de reprise, avec le Save A et la quête complétée
intacts. Chacun observe 14 frames prêtes jusque tick17110 et termine les quatre
étapes sans arrêt forcé. La première garde externe reste PASS pour les inputs,
mais sa campagne reste FAILED. Le [diagnostic](../../../../.gate3-evidence/20261014/closure-resume-readiness-diagnostic-001/result.json)
conserve les pins de ces deux acquisitions et des systèmes consommés.

Le drive du scénario est explicitement placé avant le recalcul de readiness ;
Exit valide donc l'état du fixed précédent sans garde de disponibilité courante.
L'observateur retourne sur un tick déjà vu avant de tester le changement de
readiness : une désactivation à tick gelé peut manquer dans le JSONL. Le bit
précis côté renderer/projection n'est pas exporté et n'est pas inventé. Le
lecteur et le booléen du rapport restent stricts. La correction ciblée et sa
régression devront avoir une nouvelle cohorte ; la cohorte003 garde ces échecs.

Le [correctif source](../../../../.gate3-evidence/20261014/closure-resume-exit-fix-001/source-change.json)
déplace le drive après la vraie readiness, avant le contrôleur joueur. Exit
attend `is_ready()` courant, sans prolonger son délai d'une seconde ou le budget
global. Trois régressions utilisent la registration commune prod/tests, les
vrais systèmes de readiness, stockage et rapports : projection suspendue,
joueur suspendu puis retour prêt, et deadline refusée. Leur compilation et
exécution sont dues dans native004 ; leur création n'est pas un PASS. Une trace
debug `forge::gameplay` au changement d'activation expose tick, readiness joueur
et projection pour les prochains diagnostics. Rapports, observateur, runner,
lecteur et leurs gardes sont inchangés ; aucune disponibilité n'est forcée.

La [sous-fixture de corridor](../../../../.gate3-evidence/20261014/closure-door-corridor-fixture-001/run-001/result.json)
est préparée sous native003 avec deux vrais preview/apply 63→64→65, huit Props
Fixed, départ/but du Messager sur terrain échantillonné et départ joueur en
alcôve. Porte et objets originaux sont inchangés ; hauteurs et dix blobs sont
byte-identiques. Archive `74596155…`, head `9dc6eb22…`, fonction `774bafcc…` ;
cette nouvelle fonction exige une partie fraîche. Aucun jeu ni NoPath n'est
observé. Cette acquisition SDK reste historique003 et devra être admise sur
la nouvelle cohorte avant toute qualification de navigation.

Le [protocole du corridor](../../../../.gate3-evidence/20261014/closure-door-corridor-protocol-001/README.md)
prépare dix étapes et un lecteur causal indépendant ; ses dix tests Python
portent sur des témoins/refus de protocole, pas sur le jeu. La capture optionnelle
des jobs demande encore son acquisition : l'Exit du scénario et l'auto-exit
caméra 32 s ne garantissent pas ensemble une campagne complète. Aucun dispatch
tick/sequence, timestamp de publication ou lien causal absent n'est fabriqué.

Le [relevé matériel](../../../../.gate3-evidence/20261014/closure-hardware-inventory-001/result.json)
consigne Ryzen 5 7600 (6 cœurs/12 threads), RX 7800 XT et affichage Windows
2560×1440/144 Hz. L'adaptateur et la résolution effectivement rendus restent à
lier aux mesures du jeu ; ce relevé n'ajoute aucune garde mémoire ni verdict de
performance. Le protocole feu/replan préparé reste distinct du baseline caméra
idle/yaw ; les limites temporelles des jobs ne deviennent pas un nouveau critère
de gate et les mesures sous charge demeurent dues.
