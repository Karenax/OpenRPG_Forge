# Gate 4 — clôture locale du 16 octobre 2026

**Verdict : Gate 4 clôturée localement sous Windows, sur les seize critères officiels.**
Le lot final est `0.1.0-preview.20261016.019`, construit depuis le checkout Rust
commun. Les preuves auteur, les parcours Aube/Studio installés, les huit cas RPG
isolés et les installations sont rapprochés ci-dessous. La qualification de
distribution générale, Linux et Windows vierge reste distincte.

La [revue indépendante finale](../../../../.gate4-evidence/20261016/gate4-final-review-019-001/FINAL_16_CRITERIA_REVIEW_019_001.md) et son
[index des preuves](../../../../.gate4-evidence/20261016/gate4-final-review-019-001/EVIDENCE_INDEX_FINAL_019_001.json) donnent les chemins, tailles et SHA-256.
Les reçus bruts restent immuables : leur champ `gate_qualified=false` décrit leur
portée individuelle ; le verdict agrégé du présent rapport repose sur la matrice
complète, la promotion effective et les contrôles finaux. L'ancien rapport ouvert
est conservé dans [l'archive avant clôture](../../../../.gate4-evidence/20261016/final-docs-verification-019-001/GATE4_CLOSURE_BEFORE_FINAL_001.md).

## Version qualifiée et livrables

Branche `Rust`, HEAD `3e0044d5f63b26c678d9db19380a66db832d9de3`, checkout modifié
commun conservé. Ce commit seul ne représente donc pas le lot qualifié.
Les **2 717 entrées compilées** sont inventoriées dans le
[gel Source014](../../../../.gate4-evidence/20261016/qualification-014/SOURCE_FREEZE.json),
empreinte `14d8adec43c03da8b4099539900c1093119fda03989a05ee6209bdd33b9595f8`.
Les six documents de clôture sont hors de ce gel compilé ; leur contrôle final
compare séparément les 735 Markdown inventoriés et les 2 717 entrées compilées.

| Livrable effectif | Taille et SHA-256 |
|---|---|
| [Setup principal unique](../../../../OpenRPG-Forge-Setup.exe) | 1 185 667 396 octets ; `4573abba6f8471991b913855d85f9122dcf04029d04853c71e64d3f57fb6d127` |
| [ZIP des contenus séparés](../../../../Forge-Contenus-20261016-019.zip) | 645 540 546 octets ; `7e14eb33dcbb62eb9908c42c386b6c5098de5597a6757ef59154a38ad113215f` |
| [Dossier des contenus](../../../../Forge-Contenus-20261016-019/README.md) | 13 fichiers, 645 538 642 octets ; inventaire et sommes livrés |

Le Setup contient les applications, les packs nécessaires et le prérequis WebView2
hors ligne. Le launcher Aube, le gestionnaire de contenus, Studio et le jeu séparé
conservent leurs rôles. Le choix du répertoire et le thème de l'installateur ne sont
pas remplacés par une page d'installation dans Aube. Un seul EXE d'installation
reste à la racine. Le précédent Setup013 est conservé en sauvegarde `.bin` ignorée.

Le dossier et le ZIP livrent les packs assets/module de référence, leurs manifestes,
les index, le README et deux `.orpmod` privés optionnels : Cairns 1.0.1 et Relais 1.0.0.
Ils ne contiennent pas les EXE ni le Vault complet. Les archives et leurs droits
restent `private_local_only` ; la réussite de la qualification ne donne aucun droit
de publication publique supplémentaire. Le README corrigé utilise un chemin de
plan absolu pour `plan-file`, avec archives relatives au dossier extrait.

Les [octets des contenus 002](../../../../.gate4-evidence/20261016/content-export-014-019-002/COPY_VERIFICATION_002.json)
gardent les quatre archives et leurs descripteurs identiques à la copie001 ; seuls
README et SHA256SUMS changent. Les 8 276 membres des quatre archives ont été vérifiés
avant cette copie. Le ZIP final relit taille, CRC et SHA-256 de ses treize membres,
avec noms relatifs uniques. La [promotion réelle](../../../../.gate4-evidence/20261016/final-promotion-019-preparation-001/PROMOTION_001.json)
porte le SHA `d18abae485a6ebe7881824a39118b1a8e80c7cbea36eb1445978f612d42da40c`. Elle conserve les contenus du 15 octobre et le
[Lancer-Editeur.cmd](../../../../Lancer-Editeur.cmd), SHA
`de93abbdce4dc209ce312e860bee1056a9daf4e27d7e3c62feaf962a20fedd0e`.

Les binaires installés et ceux des packs livrés ont les mêmes empreintes :

| Composant | Octets | SHA-256 |
|---|---:|---|
| CLI | 6 739 456 | `bd659e35cf62b27b08a5cead96cb70263b012a2cb091079e5f099c0e4cc6c4c2` |
| Hub | 17 773 568 | `0f33fbacfba8efff59199d1d84ffac2e77abbe4e7c4bd1777bb326879f854eab` |
| Aube | 236 436 992 | `58a3da4ed903667de227d7f67488e207860eec89b2cf39d5688ab6eaae45c7c1` |
| Studio | 187 928 064 | `2bdd1a21b763696b272f03e22cc2715fb663e01945bd9c1d9b3f6b09d8aa53c6` |
| Jeu | 110 317 568 | `d9a937e0b953fbce319a715cc29d13d9b5b869804b7c73075257bd2baaaf949d` |

## Matrice des seize critères

Les intitulés suivent la [roadmap](../../planning/MIGRATION_ROADMAP.md) et le
[protocole](../../testing/GATE4_PACKAGE_ACCEPTANCE.md). Chaque PASS conserve les
conditions de sa preuve, y compris les réemplois datés et les refus attendus.

| N° | Critère officiel | Verdict et preuve |
|---|---|---|
| 1 | catalogue d'assets | **PASS** — [imports/réouverture réels](../../../../.gate4-evidence/20261016/ui-authoring-001/UI_AUTHORING_QUALIFICATION_001.md), puis catalogue gouverné rev8/six entrées rouvert dans Studio019. |
| 2 | import GLB | **PASS** — GLB bleu et variante orange, provenance, source autonome, parent et aperçu acquis dans le rapport auteur ; orange retrouvée dans le vrai jeu019, avec gameplay/collider. |
| 3 | import textures | **PASS** — [PNG/source, aperçu damier et vignette](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-imports-001/result.json) réellement observés ; composants d'import inchangés. |
| 4 | import audio | **PASS** — WAV original 12 s, PCM mono16bits/16kHz, import et progression UI0→12 observés dans le rapport auteur. Aucune qualité acoustique humaine déduite. |
| 5 | provenance et licence | **PASS** — fiches, preuves, filiation et droits conservés jusque dans les exports ; licence CC0 et parent orange relus dans Studio019. Droits privés et positif public original restent distincts. |
| 6 | hash et dérivés | **PASS** — [binding Undo/Redo](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-binding-redo-001/result.json), [réimport même ID](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-reimport-applied-001/result.json) et [annulation exacte](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-reimport-undo-001/result.json), pins confirmés dans le runtime019. |
| 7 | thumbnails | **PASS** — vignette choisie, épinglée et affichée dans les imports réels. Aucun générateur automatique de captures GLB revendiqué. |
| 8 | validation publication | **PASS** — [refus droits audio](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-public-refusal-001/result.json), [export original CC0](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-public-export-001/result.json) puis [admission publique](../../../../.gate4-evidence/20261016/ui-authoring-001/export-inspection-protocol-003/results-public-001/inspection.json). Aucun envoi extérieur effectué. |
| 9 | package `.orpmod` v2 | **PASS** — véritables exports UI Cairns1.0.1 et Relais1.0.0, admis/installés/joués dans008 ; identité, head/archive/fonctionnel et dépendances confirmés. Le transport `.forgepack` v1 garde son contrat distinct. |
| 10 | protections archive | **PASS** — [refus CLI](../../../../.gate4-evidence/20261016/archive-negative-protocol-008-004/results-001/verification.json), [staging interrompu/reprise](../../../../.gate4-evidence/20261016/staging-interruption-results-008-003/verification.json), [upgrade/rollback](../../../../.gate4-evidence/20261016/cli-upgrade-008-002/verification.json), et [GLB Relais corrompu refusé](../../../../.gate4-evidence/20261016/relais-dependency-negative-016-001/results-001/verification.json) sur lecteurs/CLI inchangés. |
| 11 | installation | **PASS** — NSIS019 Unicode/réinstallation/désinstallation et upgrade019 ordinaire rev6, données utilisateur/modules/fichier étranger conservés ; installation008 confinée des mêmes packs. Voir les reçus ci-dessous. |
| 12 | chargement runtime | **PASS** — huit cas019, deux aventures complètes, reprises/états neufs/retour ; vrais clics Aube019 et Studio019, scènes affichées et rapports de retour. |
| 13 | second module de régression | **PASS** — Relais complet/reprise/nouvelle partie019 ; deux dégâts20, récompense, Save séparée ; retour Cairns après les trois processus Relais sans altérer sa Save. |
| 14 | import expérimental de données Solaren sélectionnées | **PASS** — [retouche Maélis](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-relais-dialogue-001/result.json), [export réel](../../../../.gate4-evidence/20261016/ui-authoring-001/proof-relais-export-001/result.json), dialogue retrouvé dans Relais-full019. Portée sélectionnée, sans migration générale ni code Godot. |
| 15 | `R4-P01` installer et jouer sans source/Vault/IA | **PASS** — client008 neuf, entrées copiées exactes, SID/0capacité, trois paires de contrôles d'accès/réseau, installation et huit cas RPG ; vrai launcher019 qualifié séparément dans le contexte ordinaire. |
| 16 | `R4-P02` variante gouvernée et retouches durables | **PASS** — variante/source/retouche/export UI durables ; décor bloquant puis contournement, combat/récompense/Save/reprise019 ; Studio019→jeu→même éditeur. Refus F9 cross-edit motivé et Save intacte selon le contrat inchangé. |

## Parcours auteur et jeux effectivement exécutés

Les 39 observations originales du [rapport auteur](../../../../.gate4-evidence/20261016/ui-authoring-001/UI_AUTHORING_QUALIFICATION_001.md)
restent datées : import, provenance, filiation, source retouchable, binding,
Undo/Redo, réimport même identité, sauvegarde, réouverture et export. La
[retouche du décor](../../../../.gate4-evidence/20261016/ui-authoring-decor-001/QUALIFICATION.json)
place Cairn2 en `(2.5,0.6,-2)`, échelle `(0.6,1.2,0.6)`, sans changer son identité
gameplay. Les composants auteur/format/CLI/sauvegarde sont inchangés selon le
[raccord Source013→014](../../../../.gate4-evidence/20261016/source013-014-reuse-001/REUSE_MAP_013_014.md).
Ce réemploi ne rebaptise pas leurs anciens reçus en observations019.

Les vrais exports admis par le client et fournis séparément sont Cairns1.0.1,
931885 octets/SHA `c0bd046f9b3d509553953690eaaa2504106d437225935ee7bcf1f043a686a16e`,
et Relais1.0.0,106526 octets/SHA
`3da63067ef5a70c21ccb6f770619e08571740d4aed096de54556565f88144d1a`.
Leurs hashes fonctionnels sont respectivement
`7c377759312c88bde4b2ed03ff71e3ed2b7d555022a4fdfac9ce686739827aff`
et `00726d817e6fac1f201034fc0c0af60c80a2a50e1f087fad1b4592c446c79445`.

La [consolidation RPG019](../../../../.gate4-evidence/20261016/runtime019-replay-consolidation-001/RPG019_CONSOLIDATION-001.json),
SHA `40b5ca4199238d24891a6bfe15d95f32fcc13667b80f0ea4982850ab19e66bed`,
relie les huit processus et leurs rapports/captures originaux :

| Cas, dans l'ordre réel | Résultat |
|---|---|
| Cairns derrière le décor | Refus attendu step17 `action refused: target is occluded`, OS1 ; succès RPG faux. Neuf frames de blocage, pas de Save créée. |
| Cairns complet | OS0 ; deux attaques distinctes/dégâts20/contact vrai, ennemi40→20→0, endurance100→95→90 ; quête terminée, objet requis consommé, récompense×1, Save et ticket. |
| Cairns reprise | Nouveau processus OS0, quête terminée/récompense×1/ennemi0/endurance90 restaurés ; ticket consommé. |
| Cairns neuf | OS0, quête disponible/ennemi40/endurance100/inventaire vide ; ancienne Save conservée et ticket de retour explicitement demandé. |
| Relais complet | OS0 ; dialogue Maélis retouché, deux dégâts20/contact vrai et mêmes conséquences ; Save/ticket propres au Relais, Cairns inchangé. |
| Relais reprise | Nouveau processus OS0, état terminé restauré, ticket Relais consommé ; Cairns inchangé. |
| Relais neuf | OS0, état initial neuf, Save Relais terminée et Save/ticket Cairns conservés. |
| Retour Cairns | Nouveau processus OS0 après les trois processus Relais ; état terminé Cairns restauré/ticket consommé ; Save Relais conservée. |

Save Cairns :3845 octets/SHA
`27b34fe6bb4840a3225d26d32e1458445a1e73e42eb82e214c144b72a87c1347` ;
Save Relais :4548 octets/SHA
`5147071b584109fa84597c240d8c1cfc703dd7f85af541dfa4752fb1b68d448d`.
Les captures natives ont été inspectées techniquement ; les HP d'ennemi sont
prouvés par les journaux, sans prétendre les lire dans un HUD qui ne les affiche pas.
Les replays utilisent le hook natif existant, sans geste clavier humain inventé.

Le [refus F9 après retouche](../../../../.gate4-evidence/20261016/controller006-install-f9-audit-001/QUALIFICATION.json)
reste acquis sur le contrat inchangé : `save incompatible: source changed;
explicitly review compatible edits`, ancienne Save intacte, aucun ticket.
Les reprises positives019 portent sur la même version exacte ; aucune reprise
cross-edit compatible n'est annoncée.

Les [vrais clics Aube019](../../../../.gate4-evidence/20261016/aube019-consolidation-001/AUBE019_CONSOLIDATION_001.json)
ouvrent Cairns1.0.1, Relais1.0.0 et Tidewater depuis l'installation rev6.
Les scènes ont été affichées et fermées normalement. Le primaire Aube36132 sort
avec code OS0 tenu ; les enfants34956/6716 ont des rapports de jeu/projection
réussis, sans mesure numérique de leur sortie OS. Tidewater est un smoke optionnel,
avec trois warnings `gpu_readback` de canal fermé lors de sa fermeture conservés.
Aucune boucle RPG complète n'est déduite de ces trois consultations.

La [consolidation Studio019](../../../../.gate4-evidence/20261016/studio019-consolidation-001/STUDIO019_CONSOLIDATION_001.json) relie la réouverture de Cairn2,
le catalogue gouverné rev8/six entrées et la fiche orange avec parent bleu/licence,
puis le vrai bouton « Lancer le jeu ». La scène native affiche la pyramide orange ;
après sa fermeture, le même Studio indique « Le jeu est fermé ». Studio31240 est
quitté normalement sans nouvelle sauvegarde, code OS0 tenu. Les deux documents
auteur conservent leurs octets. Le [journal Root019](../../../../.gate4-evidence/20261016/runtime019-root-ui-001/observations.jsonl)
est figé à33lignes/214089 octets/SHA
`d81dd9c42958560e9f510650c80cfcd2dd014280108bf2efcd575db73a3faab2`.
Il conserve texte/identités des captures affichées ; il n'archive pas leurs pixels.

## Isolation, installations et conservation

Le client008 utilise un profil AppContainer neuf `fg4q8c019a`, zéro capacité,
les mêmes packs019 physiquement copiés et un environnement reconstruit.
Trois paires de témoins vérifient avant/après que l'hôte ouvre source/Vault et
le témoin réseau, tandis que le client reçoit refus5 sur source/Vault,
10060 sur loopback et10013 sur la cible externe. Aucun fournisseur IA n'est requis.
L'isolation utilise le GPU/pilote de l'hôte ; elle ne prouve pas un Windows vierge.
Studio/WebView et Aube sont qualifiés dans le contexte ordinaire, séparément du
jeu confiné ; le bridge Aube et `module-launch` partagent l'admission et le départ
`installed_modules::start`, selon la revue de leurs composants inchangés.

La collecte008 comprend58phases/58identités (19hôte/39confinées),57sorties0 et
l'unique refus1 attendu. Tous les Jobs possédés sont vides, handles signalés,
sans arrêt forcé. Les431copies de preuves,162783841octets, ont été rehashées
indépendamment sur E après collecte. Le [nettoyage réel](../../../../.gate4-evidence/20261016/runtime019-replay-consolidation-001/CLEANUP_CONSOLIDATION-001.json)
confirme suppression du profil et du namespace008, sans recréation ni nouveau rejeu.
Le scan borné lit296tokens et conserve158processus tiers inaccessibles comme limite ;
il n'est pas un recensement global de la machine.

Le [NSIS019 réel](../../../../.gate4-evidence/20261016/silent-classic-014-review-001/CONSOLIDATION-001.json)
couvre les destinations Unicode/espaces, installation, réinstallation et
désinstallation silencieuses :8Jobs/24identités, sorties0, nettoyage normal,
91fichiers utilisateur conservés. Deux fichiers étrangers ajoutés après la
réinstallation sont conservés à la désinstallation ; ils ne prouvent pas leur
conservation lors de la réinstallation antérieure. `/NOINTEGRATION` conserve
le snapshot des intégrations shell. WebView2 était déjà présent155.0.4283.45 ;
ce reçu ne prouve pas sa pose sur une machine vierge ni une UI NSIS interactive019.

La [mise à jour ordinaire019](../../../../.gate4-evidence/20261016/ordinary-upgrade-019-001/CONSOLIDATION-001.json)
atteint réellement rev6/head300afe…653db. Les110fichiers historiques, sous-ensembles
104/97, trois répertoires de modules29+21+29fichiers et témoin externe98octets restent
intacts. Les deux fichiers sous `saves` de ce baseline sont des verrous vides,
distincts des vraies Saves RPG008. Six Jobs/12identités ferment normalement0.
Cette mise à jour ne constitue pas un nouveau test d'idempotence ; sa preuve
historique reste reliée au CLI inchangé.

## Correction qualifiée, validations et limites

Le défaut de contact rencontré dans le candidat018 est corrigé dans le moteur
générique : le test de proximité utilise les poses/formes Rapier réelles et
`query::contact` pour la bande de contact du contrôleur, avec gardes fini/activé.
L'occlusion, la portée, la position courante du joueur et les exclusions restent
appliquées. Aucun nom de module, asset spécifique ou dépendance n'est ajouté.
Les tests de géométrie verrouillent le cas où l'ancien cast directionnel renvoyait
`None` ; les deux vrais probes GPU hôte ont précédé le build019 puis les huit cas
isolés ont confirmé le comportement. Les échecs et acquisitions partiels014–018
restent archivés ; aucun échec n'est rebaptisé PASS.

Les [quatre chaînes authentifiées](../../../../.gate4-evidence/20261016/qualification-014/AUTHENTICATED_FOUR_RECEIPTS.json)
portent toutes sur Source014 : natif2 349PASS/0FAIL/56ignorés/248suites,
fmt/diff/Clippy réussis ; WASM moteurWebGPU et StudioTrunk réussis ; trois builds
Release réussis ; deny/audit réussis selon leurs politiques,0vulnérabilité et
10warnings conservés. Le [builder normal019](../../../../.gate4-evidence/20261016/classic-build-014-001/BUILD_ONLY_HANDOFF.json)
vérifie ses9 262membres, sans modification des entrées consommées.

La réserve `RUSTSEC-2024-0429`, GLib0.18.5/unsound, demeure le bloqueur de
distribution globale consigné par la supply-chain ; cette clôture locale ne
résout pas ce point. Aucun PASS Linux, Windows vierge, CI distante, performance
générale ni acceptation artistique propriétaire n'est déduit. GitHub demeure
hors des critères bloquants selon la décision propriétaire, sans inventer de run
réussi. Aucun commit/push Gate4 ni ouverture de Gate5 n'est effectué.

Le contrôle documentaire final vérifie les seize cases d'origine cochées,
les liens locaux nouveaux, l'absence de placeholders, exactement six documents
modifiés parmi 735 Markdown et les 2 717 entrées compilées inchangées. Le manifeste
du builder scellé avant édition contient ces 735 chemins ; les 718 hashes du premier
inventaire et les six pins documentaires indépendants lui correspondent exactement.
Le premier contrôle incomplet et le résultat corrigé sont conservés dans le
[dossier de contrôle final](../../../../.gate4-evidence/20261016/final-docs-verification-019-001/).
