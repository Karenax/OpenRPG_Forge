# Contexte canonique — OpenRPG Forge Rust/WebGPU

Pour reprendre le développement hors Codex, lire le [guide de reprise](docs/HANDOFF.md).
Il décrit les procédures et données locales sans redéfinir l'état canonique ci-dessous.
Les sections datées sont des archives : leurs anciennes attentes et autorisations
ne remplacent pas les décisions plus récentes ni une demande actuelle.

## État courant — Gate 4 clôturée localement au 16 octobre 2026

Le [rapport final Gate 4](docs/evidence/gate4/GATE4_CLOSURE_20261016.md) porte le verdict
**clôturée localement**, la matrice des seize critères et les preuves du lot
`0.1.0-preview.20261016.019`, avec leurs composants réemployés et leurs limites.
Les récits datés ci-dessous restent historiques et ne remplacent pas cet état.
La qualification de distribution, Windows vierge, Linux et la réserve GLib
restent distincts. Aucun PASS distant n'est déduit, aucun commit/push ni démarrage
d'une gate suivante n'est implicite.

## Baseline — Gate 3 clôturée le 15 octobre 2026

**Gate 3 est clôturée sur la version commune**, après audit des critères
R3-A, R3-B et R3-P01 à R3-P05. Le candidat publié est
`3e0044d5f63b26c678d9db19380a66db832d9de3`, empreinte Source013
`4178ac3b3f7d028cb5b94ca0d870d67c245fc0418a595f375e6a8eb75027b163`.
Les 2 201 tests natifs, WASM, supply et trois Release passent localement ;
les campagnes natives, parcours publics Studio et sauvegarde/reprise du jeu
installé sont reliés à cette version par leurs composants vérifiés.

L'acceptation propriétaire de la boucle RPG séparée du 13 octobre reste acquise.
Le propriétaire retire le 15 octobre la CI GitHub des critères bloquants de
cette clôture, selon [ADR-0023](docs/ADR/ADR-0023-gate3-owner-removes-github-ci-criterion.md).
Le run `38762140900` reste `NOT RUN — infrastructure billing`, sans PASS distant.
Le [rapport de clôture](docs/evidence/gate3/GATE3_CLOSURE_20261015.md) contient
la matrice complète, les empreintes, les preuves et leurs limites.

Gate 4 — assets gouvernés et `.orpmod` v2 — est ouverte par la demande suivante ; ses
critères, la qualification de distribution, Windows vierge et la réserve GLib
restent distincts. Le chantier global de généricité/parité du rendu n'est pas
déclaré achevé par cette clôture. Aucun nouveau commit ou push n'est implicite.
Les sections datées suivantes gardent leurs constats historiques et ne
remplacent pas cet état courant.

## Objectif propriétaire du 13 octobre — clôture Gate 3

Le propriétaire accepte explicitement la boucle RPG dans le jeu séparé :
dialogue, quête, combat, sauvegarde/reprise et parcours complet créé depuis
Studio. Il demande ensuite de lancer le travail avec pour objectif de clôturer
Gate 3. Cette acceptation propriétaire est acquise ; les preuves machine et
la condition CI restent distinctes. Le noyau RPG est désormais raccordé dans
les sources du jeu séparé, avec retour de diagnostic au Studio résident.
La qualification native/GPU et environnementale de ce raccord reste à acquérir,
sur la version commune, en préservant son lanceur. Voir
[l'acceptation et le protocole de clôture](docs/evidence/gate3/GATE3_OWNER_RPG_ACCEPTANCE_20261013.md).
La [matrice de préparation courante](docs/evidence/gate3/GATE3_CLOSURE_READINESS_20261013.md)
distingue les contrôles source, les fixtures SDK, l'acceptation propriétaire,
les parcours publics Studio, les campagnes natives et la CI.
Gate 3 reste ouverte pendant cette finalisation ; aucune ouverture de Gate 4
ni publication Git supplémentaire n'est implicite.

## Publication Studio et CI — 13 octobre 2026

Le lot Studio de 52 fichiers est publié sur `origin/Rust` par le commit
`fadd790dd7dfb82dd84198744ac25e58de9226b8` : chargement, mémoire, lancement natif
et retour sur carte résidente, tests et documentation. Les travaux indépendants
de bibliothèque d'assets, scripts `hivemind` et configuration Codex restent locaux.
La synchronisation Git est vérifiée, sans divergence.

La CI [Rust gate 38394515234](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394515234)
échoue avant le démarrage des jobs Windows, Linux, WASM et supply-chain, avec
zéro étape exécutée. L'annotation GitHub indique des paiements échoués ou un
plafond de dépenses à augmenter. Ce constat courant est distinct du run du
11 octobre qui avait exécuté les runners. Aucun PASS distant n'est revendiqué ;
Gate 3 reste ouverte. Voir [le périmètre publié](docs/evidence/gate3/STUDIO_PUBLICATION_20261013.md).

## Décisions propriétaire du 13 octobre — accès stable et retour du jeu

Le propriétaire confirme que `E:\OpenRPG_Forge\Lancer-Editeur.cmd` fonctionne
et donne accès à Tidewater : **ne pas modifier ce lanceur**. Ce chemin demeure
le point d'entrée canonique, vers le seul EXE commun
`E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\forge-studio.exe` et son
profil voisin `forge-studio.local.json`. Préserver leur raccord au workspace et
aux assets Tidewater lors des mises à jour. Ne pas fournir un autre lien par
conversation. Une validation du lancement doit couvrir le `.cmd` réellement
fourni au propriétaire, et pas seulement un helper de test.

Après « Lancer le jeu », la carte doit rester chargée dans le Studio pendant
le test natif séparé. Le retour conserve la caméra, les ressources de rendu et
les retouches appliquées ; il ne doit plus recharger la page ou reconstruire
la carte. Mettre le rendu de l'éditeur en pause pendant la partie, puis reprendre
le même viewport après fermeture du jeu. Les ressources de carte restent donc
en mémoire pendant le test. Le build commun et les huit tests de pause/reprise
passent ; le propriétaire confirme le 13 octobre que le retour fonctionne.
Les 88 fichiers auteur, le lanceur et son profil sont inchangés après le test. Voir
[le suivi du retour sans rechargement](docs/evidence/gate3/STUDIO_RESIDENT_GAME_RETURN_20261013.md).

## Demande propriétaire du 12 octobre — chargement, mémoire et jeu séparé

Ajouter un indicateur animé pendant le chargement de carte ; améliorer la
fluidité et la consommation mémoire de la vue 3D. Le propriétaire précise ensuite
que la sélection au clic fonctionne et qu'il faut appliquer la retouche avant
de sélectionner un autre modèle : conserver ce parcours, en clarifiant son état.
La saturation signalée est reproduite dans le Studio commun : environ 14,3 Go
GPU dédiés, puis croissance au repos. Le correctif du 13 octobre évite les
réuploads de matériaux inchangés et borne les demandes d'images du navigateur
à 60 Hz pendant les interactions et le chargement, deux recalculs/s au repos.
Sur Tidewater, les premiers relevés du Studio corrigé montrent environ
3,1 Go de RAM et 1,6 à 1,9 Go GPU dédiés. La reprise après interaction est
observée ; ces mesures locales ne qualifient ni une session de plusieurs heures
ni les FPS présentés. Voir
[les résultats et leurs limites](docs/evidence/gate3/STUDIO_LOADING_NATIVE_GAME_20261012.md).

Le propriétaire demande de rétablir « Lancer le jeu » exclusivement vers une
fenêtre native séparée. Le test intégré demeure retiré. Le lancement transmet
une archive immuable de la carte courante, y compris ses retouches appliquées
non sauvegardées et son départ auteur, sans sauvegarde implicite ni gel du Studio.
Au début du chantier, le runtime natif existant ne fournissait qu'une promenade.
Le raccord RPG et le rapport de retour sont maintenant implémentés dans les
sources communes ; ils ne sont pas déclarés qualifiés ou livrés par le seul
ancien raccord de lancement. Voir la matrice de préparation du 13 octobre.
Gate 3 reste ouverte.

## Lancement du Studio — correctif du 12 octobre 2026

Le Studio commun lit sa configuration de workspace et d'assets dans
`forge-studio.local.json`, près de son EXE, et se lance directement sans variables
de shell. Le point d'entrée à double-clic est `E:\OpenRPG_Forge\Lancer-Editeur.cmd`.
Les configurations explicites des installateurs restent prioritaires ; les gardes
des racines et hashes ne sont pas retirées. Voir
[le correctif de lancement](docs/evidence/gate3/STUDIO_DIRECT_LAUNCH_20261012.md).
Le test intégré demeure retiré et Gate 3 reste ouverte.

## Décision propriétaire du 12 octobre 2026 — retrait du test intégré

Le propriétaire constate le mode test du Studio injouable, autour de 1 FPS, et
demande sa correction ou son retrait. Le test intégré est retiré des parcours
Studio : aucun lancement, départ temporaire, panneau de partie ou reprise RPG.
Le backend refuse aussi les anciennes commandes de lancement avant d'ouvrir ou
de figer une session. L'édition de carte, le départ enregistré et les définitions
de gameplay sont conservés. Ce retrait ne constitue pas une optimisation mesurée.

Au début de ce chantier, le jeu livré ne fournissait qu'une promenade native,
sans dialogue, quête, combat ni sauvegarde RPG raccordés. Le raccord des mêmes
documents et commandes génériques est désormais implémenté dans les sources
communes. La revue P05 du jeu séparé est acceptée par le propriétaire le
13 octobre ; celle du Studio retiré n'est plus proposée. Gate 3 reste ouverte
et les preuves techniques courantes du jeu séparé restent à acquérir. Les résultats datés
sur le précédent Studio sont historiques, sans qualification du Studio modifié.
Voir [le retrait et sa validation](docs/evidence/gate3/STUDIO_PLAYTEST_RETIRED_20261012.md).

## Instruction propriétaire du 10 octobre 2026 — suppression du seuil mémoire de lancement

Le propriétaire demande explicitement de supprimer la limite de 16 Gio de marge mémoire avant lancement. Cette instruction remplace les anciennes consignes de garde à 16 Gio pour la poursuite de ce chantier. Les scripts actifs ne refusent plus le lancement selon ce seuil ; les mesures mémoire restent informatives. Les preuves historiques et leurs anciens refus restent conservés.


## État actuel vérifié — 11 octobre 2026

Consigne propriétaire du 11 octobre : une seule version de travail, dans
`E:\OpenRPG_Forge\OpenRPG_Forge-Rust`, avec reprise des changements des autres
conversations. Les builds et validations utilisent cette base commune.
Le [suivi de consolidation](docs/evidence/gate3/GATE3_SHARED_VERSION_20261011.md)
identifie les corrections et preuves fraîches. Gate 3 reste ouverte pendant cette finalisation.

[Consolidation locale du 10 octobre](docs/evidence/gate3/GATE3_CONSOLIDATION_20261010.md) : corrections du candidat intégrées aux sources courantes ; format, Clippy, 2003 tests natifs, 13 replays privés et check WebGPU PASS sur le périmètre identifié. La CI distante sur cette correction et la clôture Gate 3 restent à obtenir.

Gate C et Gate 0 sont passées localement sous leurs exceptions ; Gate 1 est
clôturée localement sous dérogations ; Gate 2 est clôturée par décision
propriétaire du 1er octobre (ADR-0018). **Gate 3 reste ouverte** ; Gate 4 et
suivantes ne sont pas formellement ouvertes.

Le [test humain Gate 3 est accepté le 8 octobre](docs/evidence/gate3/GATE3_OWNER_HUMAN_VALIDATION_20261008.md)
sur le correctif Tidewater 005. Ce verdict ne vaut ni acceptation des versions
ultérieures ni clôture technique complète. Le candidat RPG isolé Source075
(`7a0c770f1eac0afcc8fb67accaae539c4da10747`) possède des preuves locales
(1951 tests natifs PASS, 52 ignorés et parcours intégrés) ; ces preuves historiques restent attachées à ce snapshot. Les corrections
retenues ont désormais leur propre validation locale dans le bilan de consolidation.

Dernier constat CI revérifié le 13 octobre : [run 38394829662](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38394829662)
sur `4799ebb3dfa98795d97862f0fed3cd4f6a876887`, de nouveau refusé avant toute étape
pour facturation/plafond : quatre jobs, `steps=[]`, **NOT RUN — infrastructure billing**.
Le [run historique du 11 octobre 38123749165](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38123749165)
sur `48a894d550c5770a5ea50bc54707113e85ab5218` avait exécuté les runners : WASM et
supply-chain PASS ; Windows/Linux FAIL à Clippy, sans tests/builds natifs suivants.
ADR-0006 ne couvre plus ces erreurs et n'est pas reconduit par ce nouveau refus
pour facturation. Une CI complète PASS sur la révision finale reste nécessaire ;
les résultats locaux et les anciens runs conservent leurs révisions propres.

Le [candidat installable initial du 10 octobre](docs/planning/INSTALLABLE_DELIVERY_20261010.md) a été rejeté par le propriétaire et remplacé par la [correction R3](docs/planning/INSTALLABLE_DELIVERY_20261010_R3.md). R3 conserve les données et couvre deux relancements natifs Tidewater réels. Windows vierge et acceptation visuelle humaine restent à vérifier ; ses binaires moteur du 9 octobre ne comprennent pas le lot moteur/CI consolidé ici.
La reprise du 10 octobre conserve l'exigence de moteur générique décrite ci-dessous.
Ses modules actuels et leur qualification indépendante restent à vérifier ; la
consolidation Gate 3 ne constitue pas une preuve de cette généralisation.

Les bilans datés ci-dessous conservent leurs preuves et réserves historiques.
Leur vocabulaire « actif », « courant », « ouvert » ou « en attente » décrit
la date indiquée ; il ne remplace pas cet état actuel.

## Exigence propriétaire du 8 octobre 2026 — moteur générique

Objectif enrichi, avec toute la portée précédente conservée :

> Intégrer Tidewater dans OpenRPG Forge Rust/Bevy pour obtenir la même carte complète et les mêmes effets visuels que la référence figée, avec assets réutilisables et carte éditable dans Studio, comportements prévus au plan, validation native/WebGPU et comparaisons visuelles documentées. Préserver le travail existant et ne pas publier implicitement. Les éléments liés au moteur doivent être intégrés aux systèmes communs et utilisables par les futures cartes. Leurs noms de modules, fichiers, types, API, shaders, paramètres et contrats moteur ne doivent pas contenir d’indicatif Tidewater. Les références Tidewater sont réservées au contenu de cette carte, aux acquisitions, aux scènes de référence, aux preuves et aux mentions de provenance/licence nécessaires.

Critères ajoutés : intégrer réellement les fonctions réutilisables aux systèmes et réglages communs ; retirer les indicatifs Tidewater des noms et contrats moteur ; supprimer toute dépendance au nom/slug/assets de la carte ; vérifier leur fonctionnement sur une autre carte ou scène indépendante, sur les parcours natif/WebGPU concernés. Un simple renommage ne suffit pas. Préserver le contenu spécifique, les licences et les preuves historiques lors des déplacements et renommages.

Cette exigence reste à réaliser pour les modules actuels `tidewater_*`. L’objectif reste en pause à la demande du propriétaire ; cet amendement ne relance ni développement ni tests.

Reprise du 10 octobre 2026 : l’état de gestion de l’objectif est de nouveau ACTIVE. La pause du 8 octobre ci-dessus est historique ; la poursuite conserve intégralement l’exigence de moteur générique. Aucun statut de qualification supplémentaire n’est acquis.


## Gate 3 active — décision propriétaire du 1er octobre 2026

Gate 2 est clôturée pour la poursuite locale par validation explicite du
propriétaire des points restants, selon [ADR-0018](docs/ADR/ADR-0018-gate2-owner-validation-gate3-tidewater.md).
Les résultats machine antérieurs restent inchangés et gardent leurs limites.
Objectif actif : clôturer Gate 3 avec une aventure jouable créée dans Studio
sur la map Tidewater Rust. Les contrats et comportements de gameplay restent
génériques ; le contenu de l'aventure appartient au module. Aucune publication
Git implicite. Les états datés ci-dessous décrivent leurs étapes historiques.

Revalidation CI du 5 octobre 2026 : le run Rust du 3 octobre
`37125741497`, sur `6b001dbed0fdf1c002d43cf507c60eece1119130`, a réellement
exécuté ses quatre jobs. Supply-chain passe ; Windows/Linux échouent à Clippy
et WASM au hook de construction du worker de navigation. L’exception de
facturation ADR-0006 est donc automatiquement expirée pour la condition CI.
Le candidat local Gate 3 doit encore recevoir une CI distante réelle PASS sur
son commit exact avant clôture ; les preuves locales ne remplacent pas ces
résultats. Les récits historiques de facturation ci-dessous restent datés.
Cette vérification n’a lancé aucun workflow et n’autorise aucune publication.

## Chantier Tidewater autorisé — 24 septembre 2026

Le propriétaire a demandé l'exécution du plan complet avec la même carte et les
mêmes effets visuels que `dgreenheck/tidewater@1438b1ab`. Le chantier maritime
précédemment différé est explicitement autorisé dans cette portée, selon
[ADR-0017](docs/ADR/ADR-0017-tidewater-fidelity-integration.md).
Le [suivi Tidewater](docs/planning/TIDEWATER_INTEGRATION.md) distingue extraction,
port Rust, édition Studio et qualification visuelle. Aucune parité ni clôture de
gate n'est acquise par cette autorisation. Rust/Bevy reste la pile produit.
Le travail existant et les preuves antérieures sont conservés ; aucune publication
Git implicite. Les paragraphes datés ci-dessous conservent leur portée propre.

## Historique de reprise Gate 2 — 19 septembre 2026

Objectif actif de la tâche de reprise : finir Gate 2, sur le worktree et la branche
Rust existants, sans publication. R2-020 conserve son verdict local ci-dessous.
R2-004 qualifié localement : parcours 7D630852 puis régression 9317EE50, départ
runtime X40/Z0/cap90 et archive finale 89E0DCB8 inchangée autour du playtest.
R2-006 qualifié localement sur candidat apparié 4DB02C62 / WASM 8FE20704 :
transport u64 Tauri lossless, pagination 44 objets, quatre catégories de
sélection, reset A→B, continuité même AreaId, filtre/retouche, picking,
annulation et suppression/Undo acquis. R2-007 à R2-014 sont aussi qualifiés
localement sur ce candidat : six gestes de relief, matériaux, cercle/rayon/
intensité, historique, persistance et recalcul partiel CPU/Rapier. R2-015 est
qualifié localement : point/trait de densité, annulation/réarmement, historique,
déverrouillage et masques persistants après réouverture. R2-016 est qualifié
localement : catalogue gouverné, mélange/retrait, propagation deux couches,
Undo/Redo et persistance sans doublon. R2-017 est qualifié localement sur
candidat DA859D53 : exclusions auteur Circle/Corridor, historique, suppression
et persistance, sans seconde source canonique. R2-018 reste acquis. R2-019 est
qualifié localement : Preview/Abandon/Apply, historique et réouverture de la
végétation, avec acquittement CPU/Rapier. R2-020 reste acquis. R2-021 est
qualifié localement : création puis édition d'un chemin accepté, Preview/Apply,
Undo/Redo, refus invalide et persistance disque acquis ; une seconde
réouverture UI a rencontré un sélecteur désactivé et reste consignée comme
limite. R2-022 est qualifié localement sur candidat 6C1AB803 : profil vertical,
dévers, limite cut/fill, collider Rapier, historique, refus atomique et
persistance/reprise acquis. GPU et intersections profilées restent séparément
non qualifiés. R2-023 est qualifié localement sur le même candidat : rivière
quatre points, carving et eau concordants, Preview/Apply, historique,
Save/Close/Open, playtest et retour édition acquis. Le viewport noir immédiat
correspond au warmup WebGPU (~11 à 14,5 s) et devient stable après attente.
R2-024 est le prochain critère ouvert.
La Gate 2 et sa qualification transversale restent ouvertes.
Tests et Computer Use confiés à Luna max ; implémentations bornées à Terra medium,
deux sous-agents maximum. [État et preuves de reprise](docs/evidence/gate2/GATE2_RESUME_20260919.md).

## Candidat historique — bilan du 16 septembre 2026

R2-020 « promotion et retouche » terminé localement sur candidat Studio 1303A840.
Le 16 septembre, parcours réel sur copies isolées : promotion, abandon,
Apply/Undo/Redo, densité/habitat, refus d'habitat incompatible, Save/Open final
et entrée/sortie du playtest. Panne réelle GLB injectée dans une instance jetable :
arbre généré conservé, Apply et Ctrl+Entrée refusés, archive intacte.
Archive finale 3F539AD8 : 29 reçus, 24 commandes antérieures intactes, tête df8808ae ;
round-trip sans perte, placements initiaux strictement conservés, terrain inchangé.
Test ajouté pour les deux quotas de promotion ; cinq tests projet et un test de
pins assets PASS, formatage et Clippy ciblés PASS. Processus d'essai terminés.
Gate 2 reste ouverte ; aucune garantie frame-par-frame GPU ou validation artistique
nouvelle, aucun benchmark, commit ou push. Détails, hashes et limites dans
`docs/evidence/gate2/R2_GENERATED_PROMOTION.md`.

## Contrôle complémentaire Hermes — 17 septembre 2026

Harnais externe Apply/Undo : neuf contrôles CPU réussis via les commandes réelles,
avec assets synthétiques et sauvegarde/réouverture disque. Essais visuels sur
copies isolées et candidat 1303A840 : clic par token frais finalement confirmé
(ouverture du panneau « Projets et modules »), mais parcours visuel
aperçu/Apply/Undo/sauvegarde/réouverture NON EXÉCUTÉ. Le problème de ciblage
ne démontre pas un défaut Forge. Dernière instance d'essai PID 18244 fermée.
La commande `cargo test -p forge_studio reconciliation --offline` a compilé puis
sélectionné zéro test (76 + 4 filtrés) : résultat NON PROBANT, exclu des preuves
fonctionnelles. Arrêt des essais Hermes à la demande du propriétaire, relais Codex.
Aucun code produit modifié, aucun commit/push, aucune preuve d'assistant IA
intégré ni changement de gate. [Rapport et limites](docs/evidence/gate2/HERMES_APPLY_UNDO_VISUAL_ATTEMPT_20260917.md).

## État de référence et lecture des preuves

- Gate C qualifiée ; Gate 0 passée localement sous dérogations.
- Gate 1 clôturée localement : [rapport du 12 septembre](docs/evidence/gate1/GATE1_CLOSURE_20260912.md).
- Gate 2 clôturée par validation propriétaire le 1er octobre 2026 (ADR-0018).
- Gate 3 active sur Tidewater ; Gate 4 et suivantes non ouvertes.
- CI distante actuelle : WASM/supply-chain PASS et Windows/Linux FAIL à Clippy
  sur le run du 9 octobre identifié en tête ; les anciens `NOT RUN` restent historiques.
- La vision et les contrats ci-dessous restent normatifs ; un objectif n'est pas
  une capacité déjà implémentée. La [roadmap](docs/planning/MIGRATION_ROADMAP.md)
  porte les critères, les dossiers de preuves leurs résultats et limites.

Les étapes anciennes sont conservées sans suppression dans
[PROJECT_HISTORY_20260916.md](PROJECT_HISTORY_20260916.md). Cette archive n'est
pas un second contexte canonique. Ses mentions « courant », « à faire » ou
« non implémenté » décrivent leur étape historique, pas l'état actuel.
Un verdict ne vaut que pour son périmètre et son candidat : une date plus
récente ne remplace pas automatiquement toutes les preuves antérieures.

### État Git constaté le 16 septembre 2026

Workspace `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche `Rust`, modifications
locales suivies et non suivies conservées. HEAD :
`f786a38aca2023f08d708687f99a47d14f34e6d4`. Ce commit seul ne représente pas
le logiciel local : les candidats sont identifiés par leurs hashes de build.
La référence locale `origin/Rust` indique 103 commits d'avance sur HEAD ;
aucun fetch n'a été fait pour vérifier l'état distant actuel. Cet inventaire
n'est ni une qualification d'un arbre propre ni une autorisation de nettoyage.
Aucun pull, reset, fusion, changement de branche, commit ou push pour cette
mise au clair documentaire. Une synchronisation Git sera une opération distincte,
avec inspection des différences et préservation préalable du travail.

## 1. Vision du produit

OpenRPG Forge doit devenir un environnement complet de création de RPG 3D modulaires, utilisable par
une personne qui ne maîtrise ni Rust, ni la 3D, ni la programmation moteur.

### Parcours directeur : création IA et maîtrise manuelle

L'objectif final est de créer un RPG avec l'assistance de l'IA et de pouvoir en retoucher chaque
détail dans un éditeur visuel, sans programmation obligatoire. La boucle produit de référence est :

**décrire → proposer/générer → retoucher à la main → régénérer localement sans perdre les corrections
→ jouer → corriger → sauvegarder → exporter et distribuer.**

Cette orientation est adoptée pour le développement ; elle ne décrit pas des fonctionnalités déjà
livrées. Gate 1 est clôturée localement sous dérogations ; Gate 2 est clôturée par décision propriétaire et Gate 3 est active. L'édition manuelle se construit en
Gate 2, l'aventure jouable en Gate 3, le package en Gate 4 et l'assistant produit en Gate 6. Anticiper
une implémentation d'une autre gate nécessite toujours la décision correspondante.

Les priorités produit, dans cet ordre, sont :

1. préserver les retouches, verrous et liens persistants pendant une régénération et une réouverture ;
2. offrir une retouche précise par sélection directe, gizmos, valeurs numériques, masques et splines ;
3. produire une petite aventure complète depuis le Studio avant d'élargir le catalogue de fonctions ;
4. faire utiliser à l'IA les mêmes commandes, validations et annulations que l'édition manuelle ;
5. relier les diagnostics du playtest aux objets et règles à corriger ;
6. réduire le temps de création et le coût des itérations par reconstruction locale et reprise fiable.

Le Studio est commun aux modules, configurable par leurs données et profils ; créer un RPG ne doit
pas nécessiter un nouvel éditeur exécutable pour chaque jeu. L'assistance au développement de Forge
par Codex et l'assistant de création livré au joueur-auteur sont deux capacités distinctes. Une scène
fabriquée par des scripts de développement ne prouve pas le parcours d'un utilisateur dans le Studio.
Le produit reste utilisable hors ligne et sans fournisseur IA obligatoire.

Le scénario de référence associe village, forêt, intérieur, quête avec deux approches possibles et
conséquence persistante. L'auteur retouche une porte et une réplique, régénère une région sans les
perdre, joue, sauvegarde et reprend. Le package est ensuite testé indépendamment du workspace.
Chaque portion est qualifiée dans sa gate ; aucun nom de module n'est codé dans le moteur.

Mesurer le temps jusqu'au premier parcours jouable, les actions correctives, les régénérations,
les attentes médiane/p95, les blocages de quête, le coût IA et les pertes de retouches (objectif zéro).
Fixer les budgets de temps et de coût à partir de mesures, sans promesse de durée arbitraire.
La roadmap porte les critères vérifiables ; les choix de schémas, de fusion et de migration restent
à formaliser par ADR avant implémentation. L'audit daté ayant motivé cette orientation est conservé
dans `docs/planning/AI_RPG_CREATOR_PRODUCT_REVIEW_2026-09-05.md`, comme historique d'analyse.

### Capacités cibles

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

ADR-0011 (2026-09-06, accepté) autorise des profils effectifs explicitement dégradés,
distincts des presets demandés : teinte au lieu de réfraction écran, brouillard de distance
au lieu de volumétrie et plafond SSR non garanti déclaré. Aucune capacité inconnue n'est
validée par défaut. Résolution, projection CPU et readiness GPU restent séparées ; adaptation
non activée tant que l'intégration n'est pas qualifiée. Gate 1 et ses revues restent ouvertes.

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

Les retouches persistantes doivent survivre à la régénération, à la fermeture de l'historique undo
et au redémarrage. Distinguer source génératrice, correction authorée et cache dérivé. Un conflit
entre nouvelle génération et propriété protégée est présenté explicitement ; aucun écrasement
silencieux n'est admissible. Les verrous peuvent porter sur les propriétés concernées sans figer
toutes les dépendances. Une retouche Blender doit être conservée dans une source d'asset authorée
ou une recette reproductible, jamais uniquement dans un cache destiné à être régénéré.

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

La navigation est derrière un contrat Forge. Le 2 octobre 2026, le propriétaire a accepté
Rerecast 0.4.0 avec le fork testé maintenu par le projet, Landmass 0.9.2 et l'adaptateur Forge
vers le monde Rapier existant, selon `docs/ADR/ADR-0009-navigation-provider-benchmark.md`.
Les preuves LAB NAV019 motivent cette sélection ; l'intégration produit et sa qualification
native/WASM sur Tidewater restent en cours. Cette décision ne clôture pas Gate 3.
R1-068 et ses refus antérieurs restent des preuves historiques avec leurs limites propres.

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
- éléments générés : identité reproductible et filiation conservée après modification locale ;
  recette, seed, étape et index local décrivent la génération, mais un index seul ne suffit pas
  à garantir la stabilité des références authorées après insertion ou réordonnancement ;
- assets : identifiant logique + hash de contenu ;
- révisions : monotones dans un workspace ;
- aucune identité persistante ne dépend d’un `Entity` Bevy.

Une insertion locale ne doit pas renuméroter les objets non affectés. Une suppression ou un changement
de recette/seed exige un remappage explicite ou un diagnostic de référence orpheline, jamais une
réaffectation silencieuse d'une quête, d'un verrou ou d'une sauvegarde. Ce sont des exigences cibles ;
le mécanisme et sa migration devront être documentés par ADR et prouvés en Gate 2.

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

### 8.9 Références visuelles, WorldSketch et Blender

Le pipeline de production assistée part de plans top-down, de perspectives et de multivues d'assets,
mais aucune image n'est une topographie métrique ou une source runtime. Le document intermédiaire
`forge.world-sketch` conserve calibration, relief, régions sémantiques, splines, landmarks,
incertitudes, verrous humains, caméras fixes et provenance. Il est ensuite compilé vers les documents
Forge canoniques et une greybox jouable.

Blender 4.5 LTS intervient comme DCC et worker reproductible pour terrain détaillé, retopologie, UV,
PBR, Geometry Nodes, LOD, collisions, pivots, turntables et GLB. Les `.blend`, meshes, navmeshes et
caches générés restent dérivés ; terrain, masques, splines, placements et règles Forge restent les
sources de vérité. Une image ou un grand GLB opaque ne peut pas être promu implicitement.

Les acquisitions successives du pilote Visual Valley et de ses profils sont
conservées dans [l'historique](PROJECT_HISTORY_20260916.md). Elles documentent
leurs candidats respectifs ; le verdict Gate 1 est celui du rapport de clôture
lié en tête, sans réouverture implicite de ses réserves soldées ou dérogées.

## 9. Modèle RPG cible

La spécification normative historique est désormais conservée dans
`docs/design/OPENRPG_FORGE_RULESET_FR.md`, avec l’identifiant
`openrpg-forge.systemic-rpg.v1`. Il s’agit d’une copie documentaire traçable de
`archive/main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`, pas d’une implémentation Rust ni d’une
ouverture anticipée des Gates 3 à 5. Les références à la Crypte dans sa section 25 restent des preuves
historiques et ne créent aucune dépendance produit envers ce module.

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
cause du blocage de facturation couvert par ADR-0006. Gate 1 a depuis été clôturée
localement sous dérogations ; voir le rapport lié en tête du document.

### Gate 1 — Preuve visuelle et performance

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

La gate passe si :

- l’objectif de performance O2 est tenu sur machine de référence ;
- la revue humaine juge le rendu au moins comparable en impact à la référence WebGPU ;
- les mêmes données s’affichent dans le runtime natif et le viewport Studio ;
- le code reste modulaire et documenté ;
- aucune dépendance propriétaire obligatoire n’a été introduite.

Si cette gate échoue durablement, ne pas commencer la reconstruction des 717 capacités. Diagnostiquer
le renderer, le pipeline artistique ou le choix de shell, puis décider de poursuivre, remplacer une
couche ou arrêter le reboot.

### Gate 2 — Éditeur de monde

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

Une extension maritime pourra ensuite ajouter des mers, de grands lacs et une navigation en bateau.
Le projet `Token-Gremlin/natural-disasters@d2bae38301ff43bc1d43bfdfd9477a552ca5420b`
(MIT) est retenu comme référence technique possible pour le rendu de ces grandes masses d'eau :
simulation FFT multi-cascade, houle, mousse et shading PBR. Il ne constitue ni une dépendance ni une
fonction livrée. Une éventuelle reprise devra être réécrite pour Rust/Bevy/wgpu/WGSL, conserver les
contrats Forge moteur-neutres et passer par un ADR, un audit de licence/provenance et un benchmark
natif/WebGPU. Les rivières, ruisseaux et petites étendues restent servis par le système d'eau borné de
Forge ; la physique des bateaux, la flottabilité, les sillages, les courants et le gameplay maritime
formeront une tranche distincte du rendu océanique.

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

Les commandes exactes ne doivent être documentées qu’après vérification réelle. Sélectionner les
contrôles selon le risque conformément à `AGENTS.md` ; conserver intégralement les protocoles de gate
et les exigences transversales d'ADR-0006. Ne pas relancer ce catalogue après chaque correction locale.

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

Appliquer ces critères au type de tâche et aux éléments affectés : une analyse se clôt par son
rapport ; une implémentation par ses validations pertinentes ; une qualification par son protocole
complet. Une revue humaine explicitement requise reste une condition de qualification : son attente
ne bloque pas les travaux indépendants autorisés et ne permet pas de déclarer la qualification achevée.

Une implémentation est terminée seulement si :

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

### 15.2 Contraintes du produit Forge

Ces règles concernent les mutations des documents et modules réalisées par le produit Forge,
ses interfaces et son MCP. Elles ne prescrivent pas le mécanisme d'édition du code source par
l'agent de développement. Les éditions de code autorisées préservent ces garanties ; les opérations
réalisées au moyen des outils Forge restent soumises à leur gouvernance.


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

## 18. Procédure initiale historique de lancement

Cette procédure et son instruction d'arrêt concernent le lancement initial, déjà exécuté pour
Gate C et Gate 0. Elles ne constituent pas des instructions actives pour les tâches suivantes.
Ne rejouer une étape d'initialisation que sur demande explicite. Pour le travail courant, suivre
la demande utilisateur, l'état canonique, la roadmap et les prérequis de la gate concernée.
Les règles d'autonomie et de clarification sont définies dans `AGENTS.md`.

### Étape 0 — Préflight documentaire et Git

1. vérifier la branche `Rust`, le worktree et ses modifications ; préserver le travail préexistant.
   Pour le développement autorisé, un arbre modifié n'impose pas un arrêt si le périmètre est isolable.
   Pour les acquisitions et qualifications, respecter le protocole d'arbre propre, notamment ADR-0006 ;
2. lire `AGENTS.md`, ce document, ADR-0001 à ADR-0006 et la roadmap ;
3. vérifier les références `main` et Cadle épinglées ;
4. si un commit est explicitement autorisé, le centrer sur un résultat cohérent ; aucun push implicite ;
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
outils de comparaison prêts. À la date du passage de Gate 0, la CI distante était
`NOT RUN` sous ADR-0006 ; son état actuel figure en tête de ce document.

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

### Archive de l’instruction initiale — non active

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

- fournisseur de navigation (`ADR-0009` proposé, sélection encore bloquée) ;
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

PARCOURS PRODUIT
Créer un RPG avec l'IA et le retoucher précisément dans le Studio. Préserver corrections et identités
lors des régénérations ; prouver une aventure complète, sauvegardable et exportable. L'IA utilise les
mêmes commandes que l'auteur ; le produit reste utilisable sans IA. Décliner ce parcours dans les
gates existantes, sans changer leur ordre ni annoncer une capacité avant sa preuve.

INTERDIT
Recréer les 717 fonctions avant la preuve visuelle ; écrire un renderer complet brut sans limite
mesurée ; ajouter des dépendances ou assets sans licence ; annoncer une fonction sans preuve.

UPSTREAM DE RÉFÉRENCE
Cadle v0.6.0 @ ea0da625 : copie locale épinglée, intacte et mesurée. Code MIT réutilisable avec
attribution ; assets exclus. Aucune dépendance Three.js/JavaScript dans le produit Forge.

TRAVAIL ACTIF
Gate C et Gate 0 sont passées ; Gate 1 clôturée localement sous dérogations,
Gate 2 clôturée par décision propriétaire. Gate 3 reste ouverte. Test humain
accepté sur le correctif 005 du 8 octobre ; intégration du candidat RPG et CI
finale restent distinctes. CI du 9 octobre : WASM/supply-chain PASS,
Windows/Linux FAIL à Clippy. La généralisation du moteur reste en pause.
```
