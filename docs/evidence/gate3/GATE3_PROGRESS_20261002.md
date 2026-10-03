# Gate 3 — progression Tidewater au 2 octobre 2026

**OUVERT — NON QUALIFIÉ.** État actualisé le 3 octobre ; reprise des validations du pin `8530358…`. Gate 2 a été clôturée
par le propriétaire ; l'objectif actif est la clôture de Gate 3 sur Tidewater.
La [chronologie conservée](E:/OpenRPG_Forge/.gate3-evidence/20261002/progress-doc-historical-20261002-1422.md)
contient les essais antérieurs, y compris leurs échecs. Le présent document
décrit les résultats actuels et les travaux encore nécessaires.

## Résultats actuels du candidat V8 — 3 octobre

Le candidat `875f3731…` et le WASM `cf9ffab8…` sont construits sur le snapshot
propre `8530358…`. La [revue du candidat](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-candidate-root-review-001/review-proof.json)
vérifie les sources appariées et 1 340 fichiers. Campagne machine terminée :
1 507 tests PASS, zéro échec, dix ignorés ; les 50 cas critiques et les quatre
tests sur entrées acquises passent. Contrôles stricts natif/WASM et supply PASS.
La [revalidation Studio](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v8-001/gate3-revalidated-gameplay-authoring-ledger.json)
acquiert l'archive `19b4af30…` et la tête Gameplay `855eb32b…` sur ce candidat.
Les résultats runtime V7 ne sont pas hérités.

| Preuve actuelle | Résultat et limite |
|---|---|
| [P01 V8 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-p01-root-review-003/proof.json) | PASS public local : intérieur ouvert/traversé/quitté/refermé, quête reçue, cinq attaques distinctes de 20 dégâts avec contact, Sceau récupéré, quête terminée, Save et nouveau processus Resume. Harnais exécuté `61ada712…`. Hash d'état non recomputé par le lecteur ; reprise réellement acceptée par le moteur. |
| [EnvironmentQuest V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-environmentquest-001/causal-evidence.json) | PASS public local : gardien vaincu par impact du décor avant tout combat, quête terminée, Save/Resume et conséquences physiques durables. Avec P01, deux approches acquises sur le même contenu. |
| [ForestThrow V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-forestthrow-001/causal-evidence.json) | PASS : lancer, impact, support détruit, chute, champs durables après Save/Resume. |
| [ShorePush V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-shorepush-001/causal-evidence.json) | PASS : poussée, ignition et extinction par l'eau. Ne qualifie pas seul les dégâts de chaleur. |
| [Navigation V8 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-navigation-002/gate3-navigation-public-playtest-ledger.json) | PASS borné : patrouille extérieure, joint de tuiles, porte fermée sans chemin puis sortie et destination atteinte après ouverture, fermeture et Save avec reçus. |
| [P02 V8 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-p02-root-review-002/proof.json) | PASS : réplique et porte retouchées par Preview/Apply/Save Studio, refus ordinaire puis reprise compatible en nouveau processus ; quête Completed, liens, état consommé et intérieur conservés. Retouches uniquement dans la copie isolée. |
| [P03 V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-invalid-quest-p03-001/gate3-invalid-quest-smoke-ledger.json) | PASS : suppression du Sceau refusée, diagnostic lié à l'objet/quête/quantité, sélection pour retouche et Apply désactivé. Aucune correction effectivement commitée n'est revendiquée. |
| [P04 V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-replay-isolation-p04-001/gate3-replay-isolation-smoke-ledger.json) | PASS : deux départs identiques depuis l'état choisi, déplacement et Save du premier run, simulation isolée et même révision publique. |
| [ForestDanger V8 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-forestdanger-003/causal-evidence.json) | ÉCHEC FERMÉ : Ignite-to-first-key 1 126,0985 ms dépasse le budget inchangé de 1 000 ms. Dégâts de chaleur observés sur joueur et gardien ; flux complet, reprise et réaction IA au danger non qualifiés. |
| P05 | Partie complète à observer par le propriétaire pour lisibilité et rythme ; aucune acceptation humaine acquise. |

Les échecs P01 001/002, P02 001, ForestDanger 001/002/003 et les refus de
préflight restent archivés. Les preuves PASS ne les réécrivent pas. Le
[recentrage à la porte](E:/OpenRPG_Forge/.gate3-evidence/20261002/doorway-centering-root-apply-001/proof.json)
`ea0e060f…` est appliqué et SelfTest PASS, mais n'était pas exécuté par P01 003 ;
sa qualification runtime spécifique reste absente. Le corridor P02 corrigé
est en revanche exécuté et validé par P02 002.

Les sources et sauvegardes d'entrée restent intactes ; les processus des preuves
terminées ferment sans force. Le rapport a été restauré après l'erreur d'encodage
022 depuis sa sauvegarde vérifiée ; les nouvelles écritures sont en UTF-8.

La [matrice des 22 critères](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-criteria-review-002/matrix.md)
est relue par le parent, avec seize fichiers de preuve vérifiés : **20 PASS
locaux bornés sur 22**. R3-C est acquis par le parcours EnvironmentQuest complet,
et non par une somme de fragments. Danger_ai et P05 restent en attente.

Le [polling de capture 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/fire-capture-poll-root-apply-005/proof.json)
est appliqué : lecture à 25 ms uniquement pour le dernier témoin de capture du
retrait après ignition ; défaut générique 200 ms et appel historique conservés.
Prédicat exact, geste souris, cooldown, retries et budget de 1 000 ms inchangés.
Deux SelfTests natifs et cinq cas sur la fonction réellement extraite du draft
passent ; capture tardive refusée. [ForestDanger V8 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-fire-root-review-004/proof.json)
termine le flux public et la reprise proprement : retrait 635,5515 ms sous
budget 1 000 ms, dégâts de chaleur sur joueur et gardien, cinq Save vérifiés
et cohérents. Le feu est acquis localement. La campagne physique couvre 7/8.

Danger_ai reste non qualifié : le Veilleur perçoit le danger (fleeing=true,
chaleur et dégâts présents), mais ne bouge pas. Le diagnostic public renvoie
no_path vers (22,181883 ; 56,530506 ; −265,817871), tuiles prêtes, aucun job en
attente, vitesse désirée nulle. Il reste exposé puis meurt. Il faut distinguer
la perception du danger de son évitement physique ; l'analyse du trajet est
en cours, sans attribuer encore le défaut au moteur ou au placement authoré.
Le dossier P05 est préparatoire : les captures ne valent pas observation humaine.

**Gate 3 reste ouverte.** Réaction IA au danger et revue humaine P05 restent
à qualifier. CI distante NOT RUN ; aucune publication ni qualification globale
de l'arbre Primary modifié. Les résultats sont locaux au candidat identifié.

## Historique de la reprise des contrôles du nouveau pin

Les contrôles stricts [natif 026](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-026/proof.json)
et [WASM 011](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-wasm-qualification-011/proof.json)
passent sur `8530358…`, avec sources stables et environnement restauré.
La [première campagne](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-candidate-validation-001/proof.json)
s'arrête pendant la compilation workspace 007 sur la limite de stockage ;
aucun test de cette tentative n'est déclaré réussi. Son arrêt et ses logs
restent conservés. Les deux contrôles stricts terminés gardent leur preuve.

Le [nettoyage borné et vérifié](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-storage-audit-001/cleanup-recovery-proof.json)
retire 304 anciennes bibliothèques Forge régénérables du seul cache Root.
Les neuf exécutables archivés contrôlés conservent leur hash. La suppression
a réussi mais l'écriture du premier rapport a échoué ; une vérification en
lecture seule scelle la récupération, sans répéter la suppression. L'espace
libre observé remonte à 7 952 912 384 octets. Cette variation du volume ne
permet pas d'attribuer toutes les consommations au build Root ; aucun réglage
Windows ni processus tiers n'est modifié.

La [campagne neuve 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-candidate-validation-002/proof.json)
reprend avec workspace 008, puis entrées acquises, dépendances et candidat
natif/WASM apparié. Elle réutilise seulement les deux contrôles stricts
vérifiés sur le même pin exact. Workspace 008 et entrées acquises 005 passent.
La [revue Root 007](E:/OpenRPG_Forge/.gate3-evidence/20261002/current-machine-qualification-root-review-007/review-proof.json)
vérifie 1 507 PASS, zéro échec, dix ignorés, 99 groupes, les 50 cas critiques
chacun une fois et la nouvelle régression float une fois. Les quatre tests sur
entrées acquises passent chacun une fois ; les neuf exécutables de bibliothèque
archivés sont contrôlés et les 967 sources restent identiques.
Supply 009 passe également avec audit/deny exit 0, politique source inchangée,
TLS vérifié et environnement restauré. Les deux bases RustSec propres utilisent
`83278802…`, avec 1 289 avis. À cet instant historique, le candidat était en construction. Les résultats
V8 actuels figurent au début du document ; Gate 3 reste ouverte.

## Nouveau snapshot de validation — précision du Save

Le snapshot isolé `8530358bee9a0d4bca7fe89e1bdb81c589ad6f74`, dans
`source-snapshot-002`, est propre et épingle 967 sources. Deux entrées diffèrent
du pin V7 `002b2e82…` : activation centrale de `serde_json/float_roundtrip` et
régression Save→Resume après impact. La ligne Cargo existait déjà dans Primary
et reste préservée ; seul le test y est ajouté après contrôle du préfixe ancien.
Le commit est privé, sans commit Primary ni publication. La [preuve de promotion](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-roundtrip-regression-001/promotion-recovery-proof.json)
conserve aussi le refus initial de l'assertion de propreté du clone : une
renormalisation de son index, sans modification des fichiers ni second commit,
aboutit à un status vide et aucun diff staged. Les 965 autres pins restent exacts.

Le [ForestThrow V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-forestthrow-001/gate3-physics-environment-quest-ledger.json)
acquitte réellement Lancer, puis refuse Save avec « save incompatible: state
integrity mismatch ». Le message générique du harnais sur la stabilisation ne
décrit pas cette cause. launch-before est acquis ; launch-after et Resume ne le
sont pas. La fermeture est propre sans force, sources préservées. Le harnais ENV
est désormais `25234b3d…` après le [delta de fenêtre de contact](E:/OpenRPG_Forge/.gate3-evidence/20261002/forestthrow-contact-window-root-apply-001/apply-proof.json),
qui garde tolérance et délais, et place le décalage X à 0,575 m.

Le [diagnostic du candidat exact](E:/OpenRPG_Forge/.gate3-evidence/20261002/forestthrow-save-integrity-analysis-001/diagnostic-proof.json)
lie son EXE `acb91235…` au serde_json natif sans float_roundtrip. Le [probe offline](E:/OpenRPG_Forge/.gate3-evidence/20261002/forestthrow-save-integrity-probe-001/execution-proof.json)
utilise les API réelles Save/Resume avec contacts synthétiques : 251 divergences
sur 2 048 sans l'option, zéro avec ; le contrôle sans contact passe dans les deux.
Premier témoin : énergie 0,09339775299865849→0,09339775299865848, bits
`3fb7e8ea46a62d00`→`3fb7e8ea46a62cff`, qui reproduit le refus d'intégrité.
Le champ exact du Save réel après lancer n'a pas été acquis : ce témoin établit
le défaut générique du codec, pas une mesure de l'énergie de ForestThrow.
La [régression négative puis positive](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-roundtrip-regression-001/execution-proof.json)
échoue avant correction sur ce même refus et passe après, avec énergie, hash
et état identiques. Suite Core/Gameplay : 49 PASS, zéro échec, sept groupes ;
Clippy strict ciblé PASS. Les contrôles machine globaux passent ; les résultats du build et des replays
V8 acquis ensuite figurent au début du document. Tous les résultats V7
ci-dessous gardent leur ancien candidat et ne sont pas hérités par `8530358…`.

Les probes natifs [003](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-003/execution-proof.json),
[004](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-004/execution-proof.json)
et [005](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-005/execution-proof.json)
passent chacun leur test réel et conservent leur EXE. 003 emploie un sol plat
valide ; 004 et 005 le raster canonique exact et les 256 tuiles de production.
005 ne change que le seuil d'entrée public et journalise l'arrêt obtenu :
(45,854263 ; 6,649617 ; −147,21243), à 0,013481972 m de la pose P01, puis
sortie réelle à Z −149,29642. Aucun collider importé ni PNJ n'est inclus, aucune
pose postspawn n'est imposée. Le blocage public reste non reproduit ; aucun
skinfix ni qualification de P01 n'est déduit. Ces probes sont natifs, alors
que le viewport public utilise le moteur WASM. Ils omettent aussi la transition
Gameplay SetDoor→ColliderDisabled ; leur PASS ne valide pas ce chemin public.

Le [lecteur causal renforcé](E:/OpenRPG_Forge/.gate3-evidence/20261002/physics-causal-persistence-root-apply-001/apply-proof.json)
exige, pour les completions applicables, released/destroyed/initiator présents,
typés et conservés entre after et Resume, avec leur cause attendue. Les huit
lignes officielles et leurs seuils restent identiques, les observations des
flows partiels restent visibles. Dix-sept contrôles synthétiques passent ;
aucune qualification runtime n'en est déduite.

## Qualification du candidat V7 précédent

- Strict natif 025 : **LOCAL_CHECK_PASS** sur `002b2e82…`.
- Strict WASM 010 : **LOCAL_CHECK_PASS** sur `002b2e82…`.
- Workspace 006 : **LOCAL_TEST_PASS** sur `002b2e82…`.
- Entrées acquises 004 : **LOCAL_TEST_PASS** sur `002b2e82…`.
- Dépendances 008 : **LOCAL_CHECK_PASS** sur `002b2e82…`, audit et deny exit 0.

Sur le pin historique `002b2e82…`, le [contrôle Root indépendant](E:/OpenRPG_Forge/.gate3-evidence/20261002/current-machine-qualification-root-review-006/review-proof.json)
confirme 1 506 tests réussis, zéro échec, dix ignorés et 99 groupes, ainsi que
50 cas critiques chacun exécuté une fois et réussi. Les quatre tests acquis
Tidewater passent également, avec inputs inchangés et environnement restauré.
La campagne 005 conserve son arrêt sur les verrous des bases en lecture
seule. La reprise [Supply 008](E:/OpenRPG_Forge/.gate3-evidence/20261002/supply-qualification-008/proof.json)
passe sur le même pin : 1 288 avis RustSec, zéro vulnérabilité détectée,
aucune erreur advisories/bans/licenses/sources. La politique existante reste
identique, avec 48 avertissements de doublons et 18 notes advisories.
Les deux bases officielles propres attestent la révision `f8dee89e…`.
Supply 007 conserve son échec Schannel ; la réparation utilise OpenSSL avec
vérification des certificats active, limitée au processus du contrôle et
restaurée ensuite. Aucun ignore ni contrôle désactivé n'est ajouté.
La [revue Root](E:/OpenRPG_Forge/.gate3-evidence/20261002/supply008-root-review-001/review-proof.json)
confirme les résultats et les 967 sources identiques. La construction V7
et la revalidation publique authoring 004 passent dans leurs périmètres
respectifs ; le COMMON est figé, NAV passe dans le périmètre observé et
P03/P04 passent localement. Les replays finaux restent incomplets. Gate 3 reste ouverte.

La campagne 004 sur `e4d8edf3…` a passé les contrôles stricts natif 024 et
WASM 009, puis [workspace 005 a échoué](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-005/proof.json) :
1 505 tests réussis, un échoué, dix ignorés, 99 groupes. L'échec porte sur
`danger_motion_uses_real_los_kcc_obstacle_and_saves_actor_pose`. Les exécutables
effectivement mesurés, les 967 sources et les logs sont archivés ; aucune
qualification globale n'est déduite des 49 tests critiques réussis.

Le [diagnostic natif réel](E:/OpenRPG_Forge/.gate3-evidence/20261002/danger-motion-native-diagnostic-001/result.json)
a reproduit l'assertion de contact imposé avec le mur. Les 180 observations
montrent un déplacement latéral d'environ deux mètres, avec appui réel,
sans contact avec le mur, puis l'arrêt de la fuite hors de portée du feu
(3,0165 m pour une portée de 3 m). Le PNJ reste devant le mur ; aucun détour
complet autour du mur n'est revendiqué. L'instrumentation peut modifier
l'ordonnancement. Ses sources originales ont été restaurées, sans erreur.

La [correction revue du test](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-009/apply-proof.json)
vérifie chaque pose par rapport au volume réel du mur, le déplacement latéral
physique, l'arrêt après sortie de portée et la position XYZ sauvegardée puis
reprise dans un nouveau runtime. Elle ne modifie pas la production et ne
supprime aucune garde produit. La campagne 005 requiert le workspace complet
PASS et [50 tests critiques réellement exécutés](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-candidate-critical-coverage-004/apply-proof.json),
chacun une fois et réussi, avant build Studio et replays sur la base finale
commune. Aucun nouveau critère officiel de Gate 3 n'est ajouté.


La [reprise effective](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-product-candidate-resume-001/proof.json)
est **CHECKS_PASS_BUILD_ONLY**. Frontend release et worker de navigation release
concordent avec le candidat natif debug `acb91235…`, sur `002b2e82…` propre.
Le premier build natif conserve son échec de restauration d'environnement ;
une seconde sortie utilise le wrapper revu, restaure exactement onze variables
et conserve les mêmes 967 sources. Aucun nouveau résultat runtime n'en est déduit.

La [revalidation publique V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-001/gate3-revalidated-gameplay-authoring-ledger.json)
conserve son échec avant tout input authoring ou Save sous `CodexSandboxOnline`.
Les [annotations des dumps conservés](E:/OpenRPG_Forge/.gate3-evidence/20261002/startup-breakpoint-annotation-review-001/findings.json)
identifient le breakpoint `80000003` : GPU inutilisable, dernier code
`STATUS_ACCESS_DENIED`. Le dump simulé `0517a7ed` précédent porte un échec
de création de token restreint pour un service, erreur 87. Le lien causal
service→GPU et l'objet Windows refusé restent indéterminés. Le [même essai V6](E:/OpenRPG_Forge/.gate3-evidence/20261002/startup-cdp-diagnostic-v6-001/proof.json)
échoue dans ce contexte ; cela ne démontre pas une régression de code V7.

En contexte Windows normal `FIXEFRED\karen`, le [run V7 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-002/gate3-revalidated-gameplay-authoring-ledger.json)
observe CDP, Shell et UI, puis échoue après quatre acteurs relus, avant Save,
sur le calcul des paires de dimensions du harnais. Le [run 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-003/gate3-revalidated-gameplay-authoring-ledger.json)
relit les 20 éléments publics et acquitte Save sur archive `39c8f9d8…` et
head `80e87c31…` inchangés, puis refuse à tort la fermeture en édition.
Ces deux échecs restent conservés. Les corrections de harnais revues
(parenthèses des dimensions `5d09464b…`, distinction édition/playtest
`ab1b0672…`) conservent les autres gardes et passent leurs [tests offline de dimensions](E:/OpenRPG_Forge/.gate3-evidence/20261002/checkpoint-physical-dimensions-preparation-001/test-result.json)
et [de fermeture](E:/OpenRPG_Forge/.gate3-evidence/20261002/checkpoint-authoring-zone-close-preparation-001/test-result-v2.json).

Le [run V7 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-004/gate3-revalidated-gameplay-authoring-ledger.json)
est **PUBLIC_AUTHORING_PASS** : 20 éléments relus (4 acteurs, 12 objets,
4 lieux), Save public acquitté, archive/head complets inchangés, original
`50bfd655…` préservé et fermeture propre sans force. Ledger SHA-256
`b6509a01a291fbe7fafae2396735c040a8e2c7d311e2b59605388cec2b0c6969`.
Le candidat `acb91235…`, le pin `002b2e82…` et ses 967 sources restent
inchangés ; aucun résultat runtime antérieur n'est hérité.

Les [conditions machine 004 réellement acquises](E:/OpenRPG_Forge/.gate3-evidence/20261002/final-machine-conditions-004/machine-conditions.json)
passent les cinq requêtes CIM en contexte normal : Windows 11 Professionnel
build 26300, Ryzen 5 7600 (6 cœurs/12 logiques), 64 GiB de capacité des
modules déclarés, Radeon RX 7800 XT et Radeon Graphics déclarés, WebView2
154.0.4258.37 installé. L'adaptateur actif, la résolution/réglages du playtest
et tous les timings/FPS restent `null`. Les refus machine 001/002 et l'échec
d'affectation PowerShell 003 sont conservés séparément.

L'[extension authoring NAV 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-navigation-authoring-v7-001/gate3-final-gameplay-authoring-ledger.json)
passe, avec [revue indépendante authoring](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-public-authoring-independent-review-001/review-proof.json)
`c72f8550…`. Le [gel Root COMMON](E:/OpenRPG_Forge/.gate3-evidence/20261002/final-common-freeze-v7-001/proof.json)
atteste **PUBLIC_AUTHORING_COMMON_PINNED** : ledger `ca2ad216…`, archive
`19b4af30…`, head `855eb32b…`, 65 fichiers et inventaire `1e8a77aa…`.
Les 20 éléments parent sont préservés, avec deux PNJ dédiés supplémentaires
(6 acteurs, 12 objets, 4 lieux), révision 34→40 et reçus 33→39 après six
commandes publiques. L'assertion initiale de gel qui incluait la révision
a refusé avant toute écriture ; la comparaison corrigée conserve identité,
base, schéma/version, contenu hors gameplay et préfixe des reçus parent.
Aucune source ni fixture n'est modifiée par ce gel. La fermeture authoring
est propre ; la restauration exacte null/chaîne vide de son environnement
enfant n'est pas attestée. Aucun résultat runtime n'est hérité.

Le [parcours NAV V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-navigation-001/gate3-navigation-public-playtest-ledger.json)
porte **observed_complete**, ledger SHA-256
`1d46f968f1bfc88d4f521aa1478a992e2c269c9cb36ee5828ad4efc62c22ffc8`.
Save `21872d42…` est acquis, source et clone authoring sont préservés, et les
processus propres sont fermés sans force. La [revue indépendante runtime](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-public-runtime-independent-review-001/review-proof.json)
`fc767d74…` passe 70 contrôles sur 36 échantillons réels : patrouille extérieure,
raccord de tuiles, NoPath porte fermée, sortie/Reached après ouverture,
révisions de porte et Save public. Ce **PASS_RECORDED_BOUNDED_RUNTIME** utilise
une politique explicitement vide ; il ne qualifie ni toutes les routes,
ni une politique eau/coûts, ni un nouveau processus Resume, ni Gate 3.

Le [rejeu P03 V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p03-001/gate3-invalid-quest-smoke-ledger.json)
est **PASS local Studio**, ledger SHA-256
`1935ca86d9025d2938bf9720f69436856ff4bafa1e821d651bb1f59190d5bda1`.
La suppression du Sceau est refusée avec diagnostic quête, objet et quantité ;
Apply reste désactivé. Archives, révisions et séquences sont inchangées,
sans nouveau reçu, avec source préservée et fermeture propre sans force.
Le [rejeu P04 V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p04-001/gate3-replay-isolation-smoke-ledger.json)
est **PASS local reset/isolation**, ledger SHA-256
`af7948f63acd29ea00b3498f5c519bc6c2884bdf23c99acb09367304473a7be2`.
Source, clone et Save source sont préservés ; isolation, même départ choisi
et même révision publique sont vérifiés. Les deux processus sont terminés
et fermés sans force. Ce reset ne qualifie pas une reprise de partie.
Le [rejeu P01 V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p01-001/gate3-gameplay-playtest-smoke-ledger.json) conserve son échec avant l'entrée : le messager ne satisfait pas l'arrêt hors du passage dans le délai prévu, ledger `b5170694…`.
Le [rejeu P01 V7 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p01-002/gate3-gameplay-playtest-smoke-ledger.json) reste **FAILED_CLOSED**. Après le recul public du joueur revu dans le harnais `95b91a97…`, le messager atteint une pose extérieure au sol et stationnaire, avec retours du même pas aux ticks 1681, 1698 et 1715. Le joueur entre ensuite dans la Room canonique au tick 1843. Sa sortie KCC reste bloquée douze secondes : position HUD (45,86 ; 7,52 ; −147,20), commande Z −0,05 m, déplacement X/Z résolu nul et un contact, alors que simulation, appui et entrée sont actifs. Les sources sont préservées, fermeture propre sans force ; aucun parcours intérieur complet, combat, Save final ou Resume n'est qualifié par ce préfixe.

Le [rejeu ShorePush V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-shorepush-001/gate3-physics-environment-quest-ledger.json) conserve son échec d'approche avant Pousser, avec HP 67 et 33 dégâts de contact réellement enregistrés. Le [rejeu ShorePush V7 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-shorepush-002/gate3-physics-environment-quest-ledger.json), ledger `d90439b4…`, échoue plus tôt : le hash de la transaction Save push-before est verrouillé après le clic public. Pré-hash réussi, destination Copy absente, Saves before et failure-state acquis ; aucun pulse de contact ni Push exécuté. Les sources et la fermeture propre sans force sont conservées.

Le [rejeu ShorePush V7 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-shorepush-003/gate3-physics-environment-quest-ledger.json), ledger `1ab5534a…`, reste **FAILED_CLOSED** avant le contact de rive et Pousser. Le segment public `shore-approach-waypoint-4-step-4` reste sans progrès horizontal pendant douze secondes ; au tick 2164, simulation, appui et entrée sont actifs, Z demandé +0,05 m, X/Z résolu nul et un contact. L'anchor public des pieds est (29,658571 ; 3,4564757 ; −110,921844) ; le Save d'échec porte le centre du joueur à Y 4,3314757 au tick 2190. Les Saves before `978a7fa3…` (tick 843) et failure-state `b23ff1b1…` sont acquis, acquittés et cohérents, avec vitalité 100 dans les deux. Aucun Push, pulse de contact ou Resume n'est qualifié. Sources préservées et fermeture détenue sans force. Aucune cause commune avec P01 n'est présumée ; la fermeture WebSocket du cleanup après le Save d'échec n'est pas attribuée au blocage KCC. Le compteur sharing32 n'est pas exposé dans ces captures : charger le helper corrigé ne démontre pas que cette branche a été rencontrée.

Les correctifs externes revus concernent le harnais, sans changer le candidat `acb91235…` ni le COMMON. La [fenêtre de contact et l'anchor public précis](E:/OpenRPG_Forge/.gate3-evidence/20261002/shorepush-contact-window-root-apply-001/apply-proof.json) gardent douze secondes et les gestes existants ; séparation 0,575 m et tolérance 0,025 m décrivent seulement une fenêtre nominale [0,55 ; 0,60]. Un anchor présent malformé est refusé. L'acquittement et les reçus du vrai Push restent nécessaires, sans promesse d'approche sans dégâts ni PASS hérité du V6.
La [proposition de lecture sous sharing32](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-commit-sharing-read-preparation-001/proposal-proof.json) ne relit que le commit déjà demandé sous IOException Windows 32, dans le même délai. Si la dernière lecture est toujours verrouillée à l'échéance, son ErrorRecord est relancé pour empêcher un second Save dans l'appelant. Les dix tests offline passent, dont un unique clic Save simulé dans les deux fonctions réelles sous verrou persistant ; aucune réussite Save/Push runtime n'en est déduite. Copy, les gardes publiques et FIRE restent inchangés : premier recul avant attente et capture brute. La [preuve d'application Root](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-commit-sharing-read-root-apply-001/apply-proof.json) épingle PHYS `1785fb0a…` et ENV `ff5f65e4…`, sans changement produit, candidat ou COMMON. Root a répété les tests dans la préparation, remplaçant le premier rapport temporel `770e892c…` ; ses octets ne sont pas récupérables localement. La proposition originale reste intacte. La [nouvelle preuve Root conservée](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-commit-sharing-read-root-apply-001/root-rerun-proof.json), SHA `d3ad1af5…`, référence le vrai rapport `8cdb19b4…` : dix tests PASS, trois lectures sous verrou et un clic public Save simulé. L'ancien pin n'est pas présenté comme un rapport encore disponible ; aucune qualification runtime n'est déduite de ce rerun offline.

Le [probe natif isolé 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-001/execution-proof.json) échoue en compilation, exit 101, sans exécutable ni résultat physique. La copie des 967 entrées omettait dix JSON embarqués à la compilation par include_str, soit 39 913 octets. Le probe 002 reprend la même copie et le même overlay de test, avec ces [entrées supplémentaires épinglées](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-002/additional-compile-inputs.json) depuis le baseline `002b2e82…` propre. Le [probe 002 terminé](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-002/execution-proof.json) finit à 05:41:10 UTC, exit 101. Son exécutable réellement compilé est archivé, SHA `0841d92582554d19d93b052c303162d18b6892215cc01820902143d897785efc`. Le test refuse son SampledTerrainPlan avec « raster domain must match the centred area and authored spacing », avant construction de l'App, démarrage ou simulation. Il s'agit d'un défaut du montage de test ; aucun résultat physique, correctif produit ou PASS n'est annoncé. L'observation précédente de compilation en cours reste conservée comme historique. Les montages corrigés suivants sont acquis séparément dans le bilan du nouveau snapshot ci-dessus.

P02, les trois modes physiques et leurs lecteurs causaux/campaign restent à acquérir sur le COMMON pour prouver les huit capacités physiques, les deux approches de quête et Save/nouveau processus Resume. P05 attend une partie complète concrète observée par le propriétaire. **Gate 3 reste ouverte, non qualifiée.**

## Sources et candidats

- Pin courant propre : `002b2e82a2f34a61f9ec1ed0d4757e2afa66169c`, dans
  [source-snapshot-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/source-snapshot-001).
  [Pins et résultats](E:/OpenRPG_Forge/.gate3-evidence/20261002/source-snapshot-001-current.json).
  Navigation ADR0009, impact, appui et synchronisation des contacts sont intégrés.
  La [campagne V7 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-product-candidate-validation-005)
  a passé les tests puis s’est arrêtée aux verrous des bases de dépendances ; les résultats sont ci-dessus.
  NAV V7 001 passe dans son périmètre observé, P03/P04 passent localement ;
  les autres replays restent ouverts, sans qualification globale de ce pin.
  Le dernier pin qualifié antérieur reste `1d002107e54a5b4fb6b5f41df6fd49951aa72058`.
- Candidat joué V4 : source `776e392f82ef0faf1b5403463026d3b355a620d9`,
  SHA-256 `bd86bb3c175b0f077c46be2613ac8ecff506fa2b62d43e55639f19de21ed9926`.
  Il précède la source figée actuelle. Le candidat V5 est désormais construit
  sur `07a5fb19…`, avec frontend/natif concordants et source stable ; SHA-256
  `7ced17b5f177f6361063377bf7ac2ed6a5fe301e078a9b46da12478b338560cc`.
  La source actuelle corrige le setup de test puis la résolution tardive des
  objets importés du playtest ; le candidat V5 ne contient pas ce correctif.
  Le démarrage runtime V5 échoue dans les runs 001 et 003 : la simulation
  reste au Tick 0, avec projection d'objets/collisions non prête. Ce blocage
  est conservé comme échec ; les tests réussis ne qualifient pas ce candidat.
- Candidat V6 : frontend et natif concordants sur `1d002107…`, source propre
  et stable. SHA-256 natif `3c0ee5d3270edf7e53730e829a1d2eb427b42f9f86bacd4fe5ef0364edadbdc9`,
  WASM `8cad9a60eff38f128e08d3ac369a78ef26424534924edae7552dbb9a800de8c9`.
  La [campagne de construction](E:/OpenRPG_Forge/.gate3-evidence/20261002/readiness-fix-validation-001/proof.json)
  reste BUILD_ONLY ; une capture publique distincte confirme le démarrage
  sur la fixture V5 inchangée. Le natif est un build debug ; aucun résultat
  de performance release ou verdict Gate 3 n'en est déduit.
- Primaire : HEAD `53d9ef794f69ba4b5d76d370b256997735fea7c9` inchangé,
  travail concurrent conservé, sans commit ni publication de ce chantier.
  Après la réconciliation 004, les [57 fichiers intégrés en 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-005/apply-proof.json)
  et les [cinq corrections revues en 006](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-006/apply-proof.json)
  préservent les travaux concurrents ; leurs diffs ciblés passent.
  Les évolutions concurrentes du Vault et du bateau restent dans le primaire
  et hors du candidat privé. Aucun PASS global du primaire n'est revendiqué.
- Tidewater original reste épinglé à
  `50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
  L'authoring et les parties utilisent des copies isolées, avec hashes avant/après.
  Les lieux, acteurs, objets et règles sont des données génériques du module.

Sur `13568d53…`, le strict natif 021 a refusé neuf diagnostics distincts
(commentaires, style de tests et longueur de `resolve`). Cet échec reste
[archivé](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-021/proof.json).
Les cinq fichiers corrigés ne changent pas la logique : le calcul de support
est extrait en fonction privée et les autres corrections sont de forme.
Le [checkpoint](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-physical-strict-lint-apply-001/checkpoint.json)
est propre et passe formatage/diff ; la qualification complète reste en cours.
Les 54 tests ciblés communs 003 passent sur les sources archivées avant ces
corrections ; ils ne qualifient pas automatiquement la nouvelle source.

Le strict natif 022 a ensuite révélé deux diagnostics dans le test de budget
navigation (longueur et comparaison flottante). Ils restent
[archivés](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-022/proof.json).
L'extraction des trois assertions de cancellation et la classification exacte
du zéro préservent le contrat, y compris les deux signes du zéro ; aucune
tolérance n'est ajoutée. Cette [correction du test](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-007/apply-proof.json)
est intégrée au primaire avec ses travaux concurrents conservés.
Sur le checkpoint propre `e2bc4679…`, [formatage et Clippy natif 023 passent](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-023/proof.json),
workspace, toutes cibles et features, avertissements refusés. Tests complets,
WASM, dépendances, builds et replays restent à réunir sur cette source.

## Contrôles et correctif vérifiés

Le correctif fonctionnel `53f595d4…` passe workspace 003 : 1 469 tests,
zéro échec, dix ignorés et quatre nouvelles régressions exécutées.
WASM 006 refuse seulement le nom local `resolved` trop proche de `resolver`.
Le [renommage sans changement de logique](E:/OpenRPG_Forge/.gate3-evidence/20261002/playtest-source-resolution-style-fix-001/apply-proof.json)
donne le pin final `1d002107…`, sur lequel les contrôles stricts natif 020 et
WASM 007 passent. La [campagne V6 terminée](E:/OpenRPG_Forge/.gate3-evidence/20261002/readiness-fix-validation-001/proof.json)
confirme workspace 004, les quatre tests acquis 003 et les builds concordants.
Supply 005 passe également sur le pin final ; les résultats antérieurs
restent attachés à leurs sources et ne sont pas réétiquetés.

| Contrôle | Résultat et limite | Preuve |
|---|---|---|
| Formatage et Clippy natif | PASS 020 sur `1d002107…`, workspace, toutes cibles et tous features, avertissements refusés ; ne lance pas les tests | [Strict natif 020](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-020/proof.json) |
| Clippy WASM | PASS 007 sur `1d002107…`, bibliothèques Web pertinentes ; ne lance pas le viewport | [Strict WASM 007](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-wasm-qualification-007/proof.json), [refus de style 006](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-wasm-qualification-006/proof.json) |
| Tests workspace | LOCAL_TEST_PASS 004 sur `1d002107…` : 1 469 réussis, zéro échec, dix ignorés, 95 groupes, quatre nouvelles régressions exécutées ; source stable et environnement restauré | [Proof 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-004/proof.json), [totaux et hashes](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-004/summary.json), [échec 001 conservé](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-001/proof.json) |
| Dépendances | PASS 005 sur `1d002107…`, audit et deny exit 0, aucune nouvelle exception, inputs et source stables ; ne couvre pas le laboratoire navigation | [Supply 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/supply-qualification-005/proof.json) |
| Entrées acquises Tidewater | LOCAL_TEST_PASS 003 sur `1d002107…` : quatre tests explicites (océan, ressac, surface avec rejet d'un octet falsifié, vrais shaders Bevy), avec 74 payloads, exécutables et source inchangés, environnement restauré | [Proof acquis 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/acquired-input-tests-003/proof.json), [préflight](E:/OpenRPG_Forge/.gate3-evidence/20261002/acquired-input-preflight-001.json) |
| Frontend et candidat V5 | Build concordant sur source `07a5fb19…` stable, WASM `1f9812c8…` ; runtime FAILED_CLOSED au Tick 0, gardes de projection/collisions non satisfaites | [Source V5](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-authoring-005-candidate/source-proof.json), [runtime 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-playtest-v5-runtime-003/gate3-gameplay-playtest-smoke-ledger.json) |
| Frontend et candidat V6 | BUILD_ONLY sur `1d002107…`, source stable et imports complets épinglés ; capture publique séparée Ready/Tick 30 sur fixture V5 inchangée, aucun parcours complet encore déduit | [Source V6](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-authoring-006-candidate/source-proof.json), [capture readiness](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-v6-public-readiness-001/proof.json) |

Le [correctif d'impact KCC](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-velocity-fix-001/apply-proof.json)
est vérifié séparément : le [négatif causal 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-regression-negative-002/proof.json)
fait passer la vitalité de 76 à 0 en maintenant une commande bloquée contre un
objet stationnaire. Le calcul utilisait la vitesse demandée au lieu du mouvement
résolu. Le correctif prend `effective_translation / fixed_timestep` pour le
joueur et conserve la vitesse backend pré-solver des objets. Le [positif 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-regression-positive-001/proof.json)
passe les six tests physiques : commande bloquée, objet en chute réellement
dommageable, rupture/support, rotation, gardes Save et nouvelle reprise.
Inputs compilés et exécutables archivés stables, environnement restauré.
L'essai négatif 001 échouait sur une assertion de mouvement de la fixture,
avant les dégâts ; sa [limite causale](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-regression-negative-001/causal-assessment.json)
reste conservée. Aucun rejeu Tidewater corrigé ni PASS du futur pin complet
n'est encore déduit de ces tests. Les deux fichiers ont été réintégrés au
primaire seulement après égalité exacte avec leur baseline sauvegardée.
Le [prédicat de stabilisation extrait](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-adapter-lint-refactor-001/apply-proof.json)
conserve les mêmes bornes et évite de dépasser la limite Clippy du système
fixe ; sa validation reste à réunir avec le prochain pin complet.

Run workspace 001 a exécuté 604 tests moteur réussis, un échoué et trois
ignorés, puis s'est arrêté avant les autres packages. Le test de reçu gouverné
attendait un refus de batches absents sans activer leur option ; le contrat
et un autre test vérifient explicitement que l'option inactive ne bloque pas
les arbres individuels. La [correction de setup](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-fixture-fix-001/apply-proof.json)
ajoute ce contrôle positif, puis active l'option avant de vérifier le refus
des batches demandés mais non projetés. Les gardes produit sont inchangées.

Les échecs stricts antérieurs restent conservés. Les corrections WASM 004
portent notamment sur les paramètres de projection, requêtes et gardes.
Huit callbacks longs ont une exception locale de style motivée ; aucun refus
fonctionnel n'a été supprimé. Les prérequis temporels compilent mais leur rendu
n'est pas qualifié par ces checks. Les deux régressions de freinage 002 ont
passé sur le pin antérieur `e9b6f97`, avec arrêt borné sur pentes ±40 degrés.
Les deux régressions passent également dans workspace 004 sur le pin final.

Quatre des dix tests ignorés du workspace sont exécutés explicitement dans
acquired-input-tests 003. Six restent optionnels et non exécutés : trois probes
CPU sans seuil de qualification, une reprise d'archive Studio locale et deux
lectures du vrai Vault. Run acquis 001 est interrompu avant les tests parce que
la sélection par crate recompilait une combinaison de features différente ;
le [motif et les seuls processus possédés arrêtés](E:/OpenRPG_Forge/.gate3-evidence/20261002/acquired-input-tests-001/interruption.json)
restent conservés. Run 003 appelle les binaires exacts du workspace 004, avec
hashes et logs de provenance ; il ne reconstruit rien. Aucun succès CI distant,
renderer ou primaire global n'en est déduit.

Le [négatif commun exécuté](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-contact-corner-negative-001/result.json)
a donné deux résultats distincts. L’interaction périmée est causale : un vrai
contact KCC autorise encore une poussée après morphing de la capsule, séquence
1 au lieu de 0. Le coin oblique passe sous le correctif d’appui existant : deux
faces confirmées, 120 poses consécutives, étendue 3D nulle, appui conservé et
vitesse verticale nulle. Les sources 671, les exécutables archivés, les logs et
l’environnement sont vérifiés ; le [dossier scellé](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-contact-corner-negative-001/final-inventory.json)
ne sera pas réécrit. Le cône proposé reste non appliqué, faute de défaut runtime
reproduit par ce cas borné.

La [correction du contact périmé appliquée dans la copie privée](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-contact-resync-apply-001/apply-proof.json)
exige la capsule installée courante et une sortie KCC fraîche ; elle diffère
les actions et Save pendant la transition tout en conservant les frames et
les contacts des autres acteurs/objets. La [visibilité de l’indicateur existant](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-sync-visibility-apply-001/apply-result.json)
est le seul delta supplémentaire du contrôleur. Le contrôle positif restaure
un vrai contact après une étape d’installation sans sweep et exige que la
frame monde continue pendant le report. Formatage/diff ciblés passent. La
[compilation commune positive](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-navigation-common-positive-001/command.json)
a compilé et exécuté 54 tests depuis quatre exécutables archivés : 52 PASS,
2 FAIL. Les sept contrôles physiques, les 22 régressions Tidewater, les trois
contrôles courbes/heightfield et les sept contrats fournisseur passent ; la
navigation passe 13/15. Les échecs conservés portent sur la vitesse KCC réelle
bornée à 2 m/s et le remplacement de la tuile après un changement de source
pendant un job. Les 843 sources et le gel NAV002 sont inchangés, l’environnement
est restauré. Le [résultat commun](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-navigation-common-positive-001/result.json)
reste FAIL ; aucune qualification du candidat ni aucun rejeu Tidewater corrigé
n’est déduit. Les brouillons 002/003 avaient
un encodage altéré ; ils restent archivés, non appliqués, et le littéral UTF8
final a été vérifié avant application.

Le [positif commun 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-navigation-common-positive-003/result.json)
passe les 54 tests nommés : 25 moteur, sept physiques, 15 navigation et sept
fournisseur. Les quatre exécutables et les 843 sources sont archivés et stables ;
l’environnement est restauré. La patrouille fournit 1 530 retours KCC mobiles
courants et une demande initiale sans retour, avec déplacement déclaré nul.
Les maxima mesurés sont 2,0000005 m/s demandés en XZ, 2,0066721 m/s demandés en
3D avec gravité et 2,0034387 m/s résolus en XZ. Le contrôle compare le retour à
sa demande physique courante ; il ne revendique pas une borne absolue XZ de
2 m/s après projection de gravité. Le test d’invalidation observe le retrait
avant synchronisation puis le nouveau reçu Ready de la tuile concernée.

Le [checkpoint privé propre](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-physical-clean-checkpoint-001/proof.json)
`13568d5367d2f932443c35c740c65ab4359102ee` réunit 59 fichiers ; un seul fichier
cache est normalisé par rustfmt après le positif 003. La [réconciliation 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-005/apply-proof.json)
intègre 57 fichiers dans Primary et préserve les options de précision JSON,
exemples et exports des travaux concurrents. Aucun commit Primary ni push
n’est effectué ; la qualification globale de Primary reste distincte.

La [pipeline V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-product-candidate-validation-001/proof.json)
reste FAILED_CLOSED sur ce checkpoint : fmt passe, Clippy strict refuse neuf
diagnostics distincts (cinq commentaires NAV, longueur de resolve et trois
styles du test fournisseur). Les sources et l’environnement sont stables.
WASM, workspace final, supply et construction du candidat n’ont pas été
atteints. Les corrections préparées hors checkout exigent un nouveau pin et
un nouveau contrôle ; aucun PASS actuel ni replay corrigé n’est déduit.

Le [Clippy séparé 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-005/clippy-cargo-result.json)
a échoué sur le commentaire de `TileInput::areas` dans le nouveau fournisseur,
avant de valider le moteur. Ce commentaire est corrigé dans le gel NAV002 ;
la validation stricte sur le prochain pin reste à exécuter. La couverture finale
exigera [49 tests critiques nommés](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-candidate-critical-coverage-003/apply-proof.json),
exécutés une seule fois chacun dans le vrai log workspace, dont les deux
nouvelles régressions ; aucun build seul ne suffit.

Les [quatre scripts publics de navigation intégrés et auto-testés](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-public-replay-promotion-003/review-result.json)
préparent le raccord réel entre tuiles, la porte, les reçus et la fraîcheur
post-writeback. Les scripts de feu et de parcours déjà corrigés sont conservés.
Ces auto-tests synthétiques n’ont lancé ni Studio ni le playtest et n’héritent
d’aucune qualification runtime.

## Parcours réellement joués

L'[authoring V5-002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-authoring-v5-runtime-002/gate3-gameplay-authoring-smoke-ledger.json)
est PASS sur une copie isolée, mais le playtest V5 ne démarre pas. Les
[runs 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-playtest-v5-runtime-001/gate3-gameplay-playtest-smoke-ledger.json)
et 003 conservent après 180 secondes « Simulation suspendue · objets/collisions
en reconstruction ou incohérents · Tick 0 ». L'eau, le ressac et les nuages
continuent de calculer ; cela ne prouve ni readiness des objets ni gameplay.
Les sources sont préservées et les instances fermées proprement. La
[capture publique 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-v5-public-readiness-002/public-readiness.json)
observe ce refus avec 24 placements et 116 primitives enregistrées, zéro
primitive en attente. Le diagnostic du projecteur GLB générique reste stale
(epoch 5/0, zéro objet), car ces imports sont projetés par leurs adapters.
Ce compteur seul ne qualifie pas l'égalité avec les objets du playtest.

La [correction locale](E:/OpenRPG_Forge/.gate3-evidence/20261002/playtest-source-resolution-fix-001/apply-proof.json)
remplace la liste figée avant chargement par une résolution sur le même head
canonique, renouvelée à l'arrivée des imports vérifiés et de sa population.
Un échec de résolution refuse la simulation et est réessayé sans renvoyer
l'ancienne liste. Les contrôles exacts de source, transform et Rapier sont
conservés. Quatre régressions couvrent l'arrivée tardive, le refus/retry,
l'owner étranger et le snapshot sans head.

La [capture causale V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-v6-public-readiness-001/proof.json)
reprend exactement la fixture V5-002, archive `f5a89455…` et head `584a38d5…`.
Le HUD public indique « Simulation active », Tick 30, au sol true et simulation
prête true, à `(40 ; 4,57 ; -115)`, sans geste ni mouvement. Le diagnostic
GLB générique reste stale 5/0 : cette observation confirme qu'il ne remplace
pas la garde effective du playtest. Les quatre archives source et clone
restent inchangées ; fermeture propre, sans arrêt forcé. Cette capture
confirme le démarrage, sans qualifier P01, le GPU, la fluidité ou P05.

L'authoring V6-001 puis l'extension physique V6-002 passent par les commandes
publiques Studio. Le [ledger final v2](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-complete-authoring-v6-002/gate3-final-gameplay-authoring-ledger-v2.json)
épingle l'archive `39c8f9d8…`, head `80e87c31…`, et conserve les attestations
de la salle à X 46 avec provenance vers le parent. Les douze commandes de
l'extension préservent les entrées du parent, sauf le flag de fuite du Gardien
explicitement authoré false ; le Veilleur reste configuré pour fuir le danger.
La base commune comprend les objets des deux approches, le piège, le feu et
le bloc de rive. Source préservée, fermeture propre et aucun résultat runtime
hérité. Les essais V6 sont en cours ; un lancer puis Save sont observés dans
EnvQuest 001, mais le projectile manque sa cible et la frappe reste hors
contact : ce run est FAILED_CLOSED, sans impact ni piège qualifié.

Le [combat V4-005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-room-retouch-v4-runtime-005/gate3-gameplay-playtest-smoke-ledger.json)
est PASS : dialogue, quête acceptée, cinq attaques, gardien vaincu, Sceau
récupéré puis consommé au retour, quête Completed, Save publique et reprise
dans un nouveau processus. Gardien et pickup restent absents à la revisite.
La source et le checkpoint authorés restent inchangés, les fermetures sont
propres. Le cap caméra initial et celui de la reprise sont observés à 0 degré ;
aucune persistance du cap n'en est déduite. Le pickup reste stable, avec support
Rapier confirmé et sans glissement après relâchement.

La [retouche compatible V4-010](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-room-retouch-v4-compatible-runtime-010/gate3-compatible-retouch-smoke-ledger.json)
est PASS : réplique modifiée et porte décalée de 0,05 m par Preview/Apply/Save,
reprise ordinaire refusée pour changement de source, puis reprise compatible
explicite. Deux processus conservent le texte, la quête Completed et le Sceau
consommé. L'archive retouchée vaut `abc59e9765ec0a709356b8bd1ae59fcf44f04389b1f91b37e0e33e2238dba285`.
Un franchissement KCC du seuil ouvert est observé au sol. Il prouve un segment
intérieur de salle générique ; il ne découpe pas le collider plein de House H.
La correction finale du harnais compare la réplique dans le paragraphe du HUD
préfixé par le nom du PNJ. Aucun correctif produit n'en est déduit.

La [campagne physique V4-012](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-playtest-retouched-v4-012/gate3-physics-playtest-smoke-ledger.json)
reste **FAILED_CLOSED**, mais deux résultats bornés sont observés : lancer
public avec objet déplacé et `released=true`, puis Ignite public avec
`burning=true`. Les deux actions, leurs conséquences et la pose du joueur
survivent à une Save réelle puis une reprise dans un nouveau processus.
Les hashes de partie avant/après/reprise sont distincts et deux receipts
sont conservés. Poussée, dégâts d'impact, piège/destruction, extinction et
réaction IA au danger ne sont pas établis. Le déplacement borné du PNJ ne
prouve pas une fuite ; `fleeing` reste false dans les sauvegardes observées.

Le [scénario eau/feu de rive V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-shore-water-v6-001/gate3-physics-environment-quest-ledger.json)
acquiert un PASS ciblé sur le candidat et la base finale communs : Ignite/Save,
quatre queries GPU fraîches `4413 < 4428 < 4484 < 4516`, puis Extinguish/Save et
Resume/Save dans un nouveau processus. Les sauvegardes restent concordantes,
la source est préservée et les deux fermetures sont propres, sans arrêt forcé.
Le [contrôle causal](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-shore-water-v6-001/causal-assessment.json)
valide feu et eau/extinction ; les autres capacités R3-B et la quête
environnementale restent explicitement false. Ces parcours devront être
rejoués sur le candidat issu du correctif d'impact en préparation.

Le [scénario eau/feu de rive V4-009](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-playtest-water-shore-009/gate3-physics-playtest-smoke-ledger.json)
est PASS ciblé, sur V4 et WebView2 `154.0.4258.37`. Quatre queries GPU de l'eau
progressent de 3167 à 3306 avec position concordante et surface au-dessus du
pied, autour de `(39,469 ; -32,381)`. Les actions publiques Ignite puis
Extinguish sont acceptées ; une Save conserve le foyer allumé entre les deux,
puis une autre Save et un nouveau processus conservent son état éteint.
L'authoring isolé règle seulement les dégâts du feu de rive à zéro pour ce
contrôle ; le feu forestier reste à un dégât par tick. Source originale,
fermetures propres et vitalité 100 sont vérifiées. Les échecs 007 (activation
native) et 008 (mort avant extinction) restent conservés. Le PASS ne couvre
aucune autre action R3-B, aucun parcours narratif ni le candidat final V5.

L'[essai feu/quête V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-envfire-quest-v6-001/gate3-physics-environment-quest-ledger.json)
reste FAILED_CLOSED : le Gardien subit des dégâts de chaleur attestés par
exposition/LOS et événements `Damage(contact=false)`, puis est vaincu ; le
Veilleur passe en fuite et se déplace de 0,905 m dans la direction opposée.
Le joueur meurt également par chaleur avant la collecte. Les sauvegardes
causales et la fermeture propre sont conservées ; la quête environnementale
n'est pas qualifiée. Le préflight retrouve un glissement avant Ignite et
une attente de focus doublée dans le harnais. Les replays préparés conservent
100 de vitalité, le contrat de chaleur et les gardes produit.

Le [parcours V6-005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-playtest-v6-runtime-005/gate3-gameplay-playtest-smoke-ledger.json)
observe l'entrée et la sortie de la salle, l'appui sur son sol puis sa porte
refermée. Il reste FAILED_CLOSED : la frappe est refusée faute de contact
physique confirmé avec le Gardien. Le nombre de collisions KCC inclut le sol
et les objets voisins ; il ne prouve pas ce contact. Les états de pente
montrent aussi un appui Rapier avec perte de l'état au sol dans l'adaptation
Tidewater et accumulation de gravité. Le [négatif causal d'appui](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-support-regression-001/negative-cargo-result.json)
reproduit le défaut sur de vrais colliders à 35 et 40 degrés : le KCC confirme
le sol tandis que la source reste en chute sous commande bloquée. L'exécutable
négatif est archivé. Le [premier positif](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-support-regression-001/positive-cargo-result.json)
passe 14 tests sur 15 ; l'appui et la vitesse verticale sont corrigés, mais la
garde de stabilité à 40 degrés échoue. Le mouvement vertical de capsule est
encore étudié, sans qualification du correctif. Saut, vraie pente trop raide,
nage/marche et synchronisation backend passent dans cette suite ciblée.
Le [négatif causal de stabilité](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-001/negative-cargo-result.json)
échoue sur la fenêtre complète de 120 pas consécutifs : oscillation verticale
22,76 mm à 35 degrés et 23,70 mm à 40 degrés. Le premier lancement positif a
réutilisé l'exécutable beta à cause du mtime conservé lors de la copie des
sources ; sa [qualification est invalidée](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-001/positive-cached-binary-invalid.json).
Après recompilation attestée, le [positif réel 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-002/positive-cargo-result.json)
passe 20 tests sur 21 : les deux arrêts sur pente ne varient plus en Y,
mais longer le mur conserve 19,36 mm de variation verticale. Retrait de
l'obstacle, déplacement de l'obstacle, saut, autostep et objets dynamiques
passent dans cette suite. Le défaut tangent reste à corriger ; aucune preuve
runtime Tidewater ni qualification globale n'en est déduite.
Le [positif 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-004/positive-cargo-result.json)
reste un échec archivé, 19 tests sur 21 : tangent et autostep refusés.
Le [positif réel 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-005/positive-cargo-result.json)
passe ensuite les 21 mêmes tests du négatif beta, sans relever leurs seuils.
Les fenêtres de 120 pas à 35 et 40 degrés restent immobiles ; longer le mur
sur 0,714 m produit au plus 0,719 mm de variation X et 0,713 mm en Y ; la marche
de 20 cm est réellement franchie. L'exécutable est archivé, SHA `2049b412…`,
sur la source `53faf50d…` et les tests `774fa7d7…`, stables avant/après.
La validation Clippy, le pin combiné et le rejeu Tidewater restent à établir.
L'[index V6 v2](E:/OpenRPG_Forge/.gate3-evidence/20261002/map-replay-preparation-001/map-v6-replay-index-v2.json)
conserve les cinq échecs P01, sans promouvoir ce préfixe de parcours en PASS.

Le [harnais de preuve du décor avant combat](E:/OpenRPG_Forge/.gate3-evidence/20261002/environment-before-combat-promotion-001/review-and-apply-proof.json)
est revu et appliqué dans les scripts publics : snapshots nommés avant/après,
source/frame/tick/séquence exacts, contact objet vers gardien initié par le joueur,
Impact et Damage contact positif, ou chaleur/LOS/Damage sans contact. Le gardien
doit passer de vivant à zéro avant toute frappe ; un décor seulement blessant
suivi de combat ne qualifie plus cette branche. Les 42 cas synthétiques Python
et PowerShell passent ; les helpers de retraite feu et de combat direct sont
byte-identiques. La lecture des anciens formats V6 conserve les échecs de quête
et de reprise, même lorsqu’une neutralisation thermique bornée est observée.
Aucun nouveau replay ni verdict runtime n’est acquis par cette préparation.

## Couverture des critères

| Critère officiel | Résultat acquis | Reste à établir |
|---|---|---|
| R3-A joueur, stats, inventaire | Déplacement, endurance, pickup et consommation repris V4-005 | Rejeu sur candidat final et qualification transversale |
| R3-A PNJ/navigation | NAV V7 001 revu : patrouille, raccord, NoPath, sortie/Reached, révisions et Save ; 70 contrôles PASS sur 36 échantillons | Périmètre observé avec politique vide ; aucune qualification de toutes les routes ou eau/coûts |
| R3-A interaction ciblée | Interactions publiques et pickup dans la boucle V4-005 | Rejeu final avec cible physique confirmée |
| R3-A dialogue | Dialogue joué V4-005 ; texte retouché conservé V4-010 | Rejeu final et reprise du texte retouché |
| R3-A quête | Quête Completed V4-005 et progression reprise | Approche environnementale complète sur candidat final |
| R3-A ennemi | Ennemi vaincu et disparition persistante V4-005 | Neutralisation par décor sur candidat final |
| R3-A combat | Combat direct joué V4-005 | Contact causal et approche environnementale sur candidat final |
| R3-A sauvegarde/reprise | Nouveau processus V4-005 et retouche compatible V4-010 | Rejeu final et conséquences physiques persistantes |
| R3-B objet poussable | Primitives de domaine disponibles | Poussée publique et mouvement observé sur Tidewater |
| R3-B objet lançable | Lancer public V4-012, objet déplacé et libéré, conséquence sauvegardée/reprise | Campagne R3-B complète et rejeu final |
| R3-B dégâts d'impact | Adapter Rapier et tests ciblés | Contact, dégâts et causalité observés dans la partie |
| R3-B chute ou piège | Support/charge authorés en essais | Conséquence jouée et persistante |
| R3-B feu | PASS ciblé V6 ShoreWater001 : Ignite/Save/Extinguish/Save/Resume sur base finale commune | Dégâts de chaleur, campagne R3-B complète et rejeu après correctifs |
| R3-B eau/extinction | PASS ciblé V6 ShoreWater001, quatre queries GPU fraîches concordantes, extinction réelle sauvegardée et reprise | Rejeu après correctifs et qualification transversale |
| R3-B objet destructible | Primitives et support authoré | Destruction publique et état repris |
| R3-B réaction IA au danger | V6 EnvFireQuest001 : exposition avec LOS, fleeing=true et déplacement réel de 0,905 m du Veilleur ; le parcours échoue ensuite par chaleur | Rejeu du PNJ avec navigation intégrée sur le candidat corrigé |
| R3-C parcours narratif | Village, forêt, combat, retour, Save/Resume V4-005 ; seuil intérieur V4-010 | Approche environnementale complète, candidat final |
| R3-P01 | Trois lieux authorés, approche combat et conséquence persistante, franchissement intérieur | Deuxième approche par le décor et rejeu final |
| R3-P02 | PASS local V4-010, texte/porte retouchés et deux reprises compatibles | Rejeu sur candidat final |
| R3-P03 | PASS local Studio V7-001 sur COMMON 19b4/855e : suppression du Sceau refusée, diagnostic quête/item/quantité, Apply désactivé ; aucun nouveau reçu ni mutation | Rejeu affecté si le candidat ou les liens de la fixture changent |
| R3-P04 | PASS local V7-001 reset/isolation sur COMMON : même départ choisi et révision, source/clone/Save source préservés, deux fermetures sans force | Rejeu affecté si candidat ou fixture changent ; ce reset ne qualifie pas une reprise de partie |
| R3-P05 | Aucun jugement humain consigné | Partie complète observée, lisibilité, rythme, erreurs et reprise |

La preuve historique [P03 V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-invalid-quest-v6-runtime-001/gate3-invalid-quest-smoke-ledger.json),
SHA-256 `7e45a41d6413312767d460389db832beda5f40323ae3a73a927946b4ec32cd83`,
épingle le candidat V6 et la base finale `39c8f9d8…` / `80e87c31…`.
La fermeture est propre, sans arrêt forcé. La preuve historique P03 V2-002
reste attachée à son candidat. P04 V6-001 attendait après un clic Save trop
précoce : le refus de stabilisation physique est conservé ; le run ne prouve
pas un mouvement persistant au moment du timeout. Le [rejeu P04 V6-002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-replay-isolation-v6-runtime-002/gate3-replay-isolation-smoke-ledger.json)
passe : trois transactions Save publiques, deux refus transitoires puis
acquittement au Tick 637. Le nouveau processus retrouve `(40 ; 4,57 ; -115)`
et la même révision 34 ; sources, clone et partie source restent inchangés.
Les deux fermetures sont propres, sans arrêt forcé. La garde produit reste
inchangée ; la qualification porte sur reset et isolation.
Le ledger P04 historique V1 a été retrouvé
directement sur F: exFAT et [copié avec provenance](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-replay-isolation-runtime-ntfs-002-f-provenance-001/source-provenance.json)
sur E: sans modifier l'original ; les 67 617 octets conservent le SHA-256
`0af28abac668bb2317c555e6d449d3fce6c184cb4680003a28bdd580c8f760ca`.
Les tests de domaine ou Rapier synthétiques ne remplacent aucune ligne de
parcours Tidewater. Aucun flag `gate_qualified` n'est promu par un build.

## Navigation et mesures

Les échecs navigation 011 sont conservés : index de détail hors buffer puis
63 erreurs MissingStartSpan. Le correctif utilise les sommets source complets
et refuse les indices invalides. Les campagnes suivantes remplacent ces
preuves pour leur périmètre, sans effacer les échecs.

La [campagne KCC 015](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-015/runtime-execution.json)
utilise la capsule 1,80 m et la vraie géométrie canonique, avec gravité intégrée
et collisions Rapier ; elle ne projette pas directement le joueur sur le Y
du navmesh. Le parcours atteint sa cible avec 1 800 ticks au sol, et l'obstacle
inséré bloque la capsule avant retrait. La
[campagne 016](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-016/runtime-execution.json)
reprend une capsule de 1,90 m correspondant au profil PNJ inspecté. Les deux
sweeps natifs font 63/63 cas bruts chacun ; Node fait 18/18 générations.
Le lancement Edge 016 échoue et n'est pas cité comme preuve navigateur.

Le [natif 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/runtime-execution.json)
est rattaché à l'archive source `ce628764…` et à ses entrées inchangées. Le
[rapport Edge réel 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/browser-report.json)
passe 18 générations brutes et recalcule le parcours Landmass/Rapier KCC avec
insertion puis retrait d'obstacle dans un worker WASM ;
[exécution](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/edge-execution.json)
27,31 secondes, sans injection d'état. Node recalcule aussi ce parcours.
Ces résultats sont ceux du laboratoire, pas des PNJ intégrés au playtest.
La vitesse 4 m/s du probe est déclarée ; elle ne devient pas une constante PNJ.

Le [benchmark global 512 m 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/navigation-provider-benchmark.v1.json)
ne panique plus mais reste **NO-GO** : le triangle/polygone 606 possède un
winding invalide et Landmass refuse la sortie brute. Le chemin obtenu après
normalisation est explicitement non accepté. Une correction causale de la
triangulation et son rejeu sont nécessaires. Les preuves de reconstruction
locale et de dépendances doivent être réunies avec ce benchmark dans le
dossier actuel, avec les limites de couverture intérieur/eau.

Le [benchmark brut 018](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-018/navigation-provider-benchmark.v1.json)
sur l'archive source `7207044b…` corrige ce défaut : height mesh accepté,
chemin trouvé, aucun winding invalide et trois hashes identiques ; aucune
normalisation ni fallback accepté. La correction vérifie les diagonales à
leurs deux extrémités et conserve des sommets exacts du contour brut lorsque
la simplification crée des cordes croisées. Le [dossier NAV019 prêt](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-019/decision-packet.md)
réunit désormais les générations globales brutes et la reconstruction du seul
tile 1 sur les mêmes 74 337 triangles VisualValley, les 126 cas natifs et
18 cas Node/Edge, le KCC physique Tidewater et les audits actuels.
L'archive source vaut `1e69a943…`, l'inventaire final `ca65d0f9…` et les sources
restent stables. Les exécutables natifs mesurés ne sont pas archivés : les
tests ont remplacé les binaires du cache ; leurs hashes et rapports restent
conservés et tout nouveau replay exige une reconstruction/revalidation.

ADR0009 est **accepté par le propriétaire le 2 octobre**. Les cinq conditions
machine passent dans leur périmètre LAB ; la [réponse explicite](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-owner-acceptance-001/proof.json)
sélectionne Rerecast 0.4.0 direct avec un fork testé entretenu par le projet,
Landmass 0.9.2 direct et adapter Forge vers Rapier. Le raccord produit et les
vrais PNJ sont en cours d'implémentation puis doivent être qualifiés. Aucun
PASS runtime produit ni verdict Gate 3 n'est déduit de cette sélection.

Les mesures de partie G3-10 se préparent sur le vrai playtest Tidewater V6.
Le précédent probe WorldSketch n'est pas un résultat Tidewater. Le canvas
de la capture readiness vaut 1 314 × 797, avec réglages individuels ; aucun
profil High ni rendu 1 920 × 1 080 n'est observé. La cadence du collecteur CDP,
un compteur de simulation ou d'étapes océan ne devient pas un FPS de présentation.
L'overlay donne une cadence d'images et des passes CPU/GPU bornées, pas un
benchmark complet. La cible du contexte reste 1080p High après préchauffage :
moyenne 60 FPS, p95 ≤16,7 ms, 1 % low ≥45 FPS. Matériel, workload, dimensions,
préparation et limites doivent accompagner chaque mesure ; les lots G3-01
à G3-10 du plan sont proposés, sans création de nouveaux critères officiels.

## Exception CI distante ADR0006

La [lecture distante du 2 octobre](E:/OpenRPG_Forge/.gate3-evidence/20261002/remote-ci-observation-001.json)
confirme que le dernier run Rust
[36300940284](https://github.com/Karenax/OpenRPG_Forge/actions/runs/36300940284)
porte sur `53d9ef79…` : quatre jobs failure, `runner_id=0`, `steps=[]`.
Il n'a testé aucun code et ne porte pas sur le snapshot Gate 3 actuel.
Le run [33335683606](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33335683606)
reste l'origine de l'exception acceptée pour facturation. Ses annotations
récentes n'ont pas été accessibles par le connecteur ; leur cause n'est pas
revendiquée comme vérifiée à nouveau. CI distante Windows/Linux/WASM/supply-chain :
**NOT RUN avant runner**. Aucun succès distant n'est annoncé.

## Suite nécessaire

Les contrôles machine, Supply 008 et le build concordant du pin `002b2e82…`
passent. La revalidation publique V7 004 et l'extension NAV 001 conduisent au
COMMON figé `19b4af30…` / `855eb32b…`, ledger `ca2ad216…`, sans résultat hérité.
NAV V7 passe dans son périmètre observé, P03 passe localement en Studio et
P04 passe localement pour reset/isolation. Diagnostiquer et corriger le parcours public P01, le rejouer, puis P02 et les modes
ShorePush, ForestThrow et EnvironmentFireQuest sur cette même base finale,
avec Save et Resume dans un nouveau processus. Leur couverture des huit
capacités environnementales et des deux approches de quête dépend des reçus
réels et des lecteurs causaux/campaign complets. Aucun résultat préparatoire,
V6 ou statut observed_complete n'est promu en qualification globale.
Conserver tout échec et réexécuter uniquement les contrôles affectés si une
correction devient nécessaire. Après ces preuves concrètes, présenter la
partie complète au propriétaire pour P05, puis consigner son verdict de
clôture. Aucun nouveau critère Gate 1/2 ou FPS n'est ajouté.

Sources d'autorité : [roadmap Gate 3](../../planning/MIGRATION_ROADMAP.md#gate-3--première-boucle-rpg-environnementale),
[plan détaillé](../../planning/GATE3_PREPARATION_PLAN.md),
[ADR0006](../../ADR/ADR-0006-remote-ci-exception-gates-0-4.md),
[ADR0009](../../ADR/ADR-0009-navigation-provider-benchmark.md),
[ADR0018](../../ADR/ADR-0018-gate2-owner-validation-gate3-tidewater.md),
[ADR0019](../../ADR/ADR-0019-gate3-authored-gameplay-simulation.md).


### Diagnostic du mouvement de fuite — copie Source3 isolée

La campagne V8 reste à 20/22 critères locaux bornés, avec danger IA non qualifié et P05 en attente. Fire004 mesure la perception du feu puis `no_path`, vitesse horizontale nulle et mort du Veilleur. Ses douze tuiles sont prêtes ; la sous-cause Landmass n’est pas exposée par V8.

La copie `source-snapshot-003-navigation-escape` part des 2009 fichiers byte-identiques du HEAD privé `8530358bee9a0d4bca7fe89e1bdb81c589ad6f74` (preuve `navigation-escape-preparation-001/proof.json`). Un diagnostic de route y est en préparation : état fournisseur exact, échantillonnage départ/cible et requête de chemin dans le même Archipelago après update ; aucune modification de destination ni d’actuation. Ce diff isolé n’est pas appliqué au Primary, pas committé et pas qualifié.

Le premier test natif s’est arrêté avant compilation sur `os error 112` (disque E: plein). Le cache Cargo inactif de cette campagne a ensuite été audité et 662 fichiers PDB régénérables retirés (3 890 135 040 octets ; inventaire et preuve dans `navigation-escape-preparation-001`). Sources, rapports et exécutables archivés sont conservés. La relance du test ciblé est en cours ; aucun PASS nouveau n’est déduit. Une session Studio extérieure à cette campagne est ouverte : aucune nouvelle UI de qualification n’est lancée à ce stade.


### Diagnostic Source3 — contrôles natifs ciblés PASS

Après l’échec disque, une deuxième tentative de compilation s’est arrêtée sur `rustc-LLVM ERROR: out of memory`, avant toute exécution du test. La tentative suivante passe après retour de la marge mémoire. Clippy avec avertissements interdits, le test de destination déconnectée et les 15 tests de `gameplay_navigation` passent. L’exécutable exact est archivé, SHA-256 `19914d144040407d3d1a6202a889246fd85bd4d7c0803d69e99f5cbe4d1f2bdc`.

La copie privée est propre au HEAD `9411091277b85ec618b62a9a9c8577f0634b4976`, avec seulement trois fichiers modifiés par rapport à Source2 ; les 2009 fichiers de Source2 sont recontrôlés inchangés. Preuve : `navigation-escape-preparation-001/validation-proof.json`. Le cache diagnostic est rafraîchi sur changement de génération ou déplacement départ/cible supérieur au demi-rayon (.175 m) ; le stamp intègre aussi la révision de policy. Il enregistre ses points effectivement interrogés. Aucun mouvement ni choix de destination n’est changé.

Le frontend correspondant `tidewater-navigation-diagnostic-009-frontend` est en construction avec les imports figés de V8. Cette copie n’est pas appliquée au Primary ; aucun commit Primary ni publication. Ces tests natifs ciblés ne constituent ni une qualification du nouveau candidat complet ni une preuve publique sur Tidewater. Le score V8 demeure 20/22, danger IA et P05 non qualifiés.
