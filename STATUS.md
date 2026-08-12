# État du projet — OpenRPG Forge

Dernière mise à jour : 12 août 2026.

## Lecture canonique

Les parcours verticaux G0 à G13 et les prototypes 3D sont livrés. Les programmes P0 à P4 ont été
réaudités et fermés selon leurs critères source et leurs preuves reproductibles. La clôture finale
P3/P4 est consignée dans `docs/planning/P3_P4_ACCEPTANCE_PROGRAM.md`.

Le registre `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` couvre chaque ID exactement une fois dans
187 lots stables de une à cinq fonctions : 187 lots clos, aucune fonction restante.
Chaque lot affiche désormais sa progression, ses gates, son parcours et ses dépendances déclarées.

## Produit effectivement disponible

- Crypte des Cendres et Forgotten Forest jouables en 3D ;
- Creator/Toolset, édition transactionnelle, undo/redo et playtest ;
- runtime séparé, sauvegarde/reprise et export `.orpmod` ;
- ForgeScript borné, MCP transactionnel, serveur autoritaire et Live Director ;
- pipeline de contenu gouverné, Asset Vault, Asset Atlas et modèles 3D animés ;
- vérification intégrée du build, des tests, smokes, graphes, exports et preuves.

Ces parcours ne valaient pas preuve automatique des 717 critères détaillés ; le tracker, les fiches
d'acceptation et les tests liés constituent maintenant cette preuve fonction par fonction.

## Programmes P3 et P4 — terminés

Les 16 lots P3 regroupant 32 fonctions et le lot P4 `FC-MULTI-P4-01` ont été réaudités contre leurs
sections source, leurs implémentations, leurs tests et leurs fiches G13. Les marqueurs Toolset et
runtime cités sont réellement émis par le produit et observés dans les journaux du portail. Une
passe ciblée rejoue 86 tests sur les 17 lots : 17/17 lots et 86/86 tests réussissent. Aucun manque
fonctionnel supplémentaire n'a été constaté ; la fermeture nécessaire portait sur la traçabilité
explicite et reproductible.

Cette clôture prouve les contrats auteur/runtime actuels. Elle ne prétend pas qu'une infrastructure
cloud mondiale, un marketplace ou un service public massif sont déjà déployés. Le détail et les
limites sont consignés dans `docs/planning/P3_P4_ACCEPTANCE_PROGRAM.md`.

## Programme P2 — terminé

Les 196 fonctions P0 et les 298 fonctions P1 restent closes. Les lots
`R121 / FC-MULTI-P2-01` à `R124 / FC-MULTI-P2-04` sont fermés. Connexion, lobby,
personnages, PvP, instances et zones dynamiques, comptes, administration et modération
disposent de contrats auteur transactionnels, round-trips, surfaces Toolset, accès MCP bornés
et tests serveur. Persistance monde, SQL sécurisé, métriques/santé, profilage et reconnexion
disposent maintenant du même contrat gouverné ; sauvegarde/rollback, caps et file d’admission
sont également configurables et testés. `R125 / FC-DM-P2-01` ferme le client GM séparé, le Creator,
le spawn multiple, le Chooser et le focus caméra. `R126 / FC-DM-P2-02` ferme téléportation,
destruction, soin, limbo et possession. `R127 / FC-DM-P2-03` ferme impersonation, protection,
or, XP et portes. `R128 / FC-DM-P2-04` ferme triggers, sélection, groupes et voyage joueur.
`R129 / FC-DM-P2-05` ferme recherche, scripts, variables, scènes et visibilité GM.
`R130 / FC-DM-P2-06` ferme les quêtes et rencontres live. `R131 / FC-BUILD-P2-01` ferme
les bundles Windows/Linux/macOS et la promotion gouvernée des canaux. `R132 / FC-CONTENT-P2-01`
ferme le fournisseur VFX budgété, la rotation de clés fiable et la livraison delta reprenable.
`R135 / FC-AREA-P2-01` ferme terrain, features, vent, météo et ciel de production. Le lot suivant
`R136 / FC-AREA-P2-02` ferme le rendu et la simulation d’eau de production. Le lot suivant est
`R137 / FC-AREA-P2-03` ferme le steering et l’évitement dynamique de navigation volumétrique.
`R138 / FC-PALETTE-P2-01` ferme les collections nommées, palettes de tâche et métriques
artistiques GLB/PNG automatiques. `R139 / FC-AUDIO-P2-01` ferme la convolution gouvernée,
le fallback par capacité et les sends par source. `R140 / FC-WAYPOINT-P2-01` ferme le suivi
runtime des routes aériennes, l'inclinaison animée et les politiques d'obstacle. Le lot suivant est
`R142 / FC-GAMEPLAY-P2-02` ferme la réplication réseau intègre et ordonnée de la météo ainsi que
son événement ForgeScript typé. `R143 / FC-AI-P2-01` ferme la simulation déterministe de combat,
ses comparaisons d'archétypes, son UI et son contrat MCP. Le programme P2 est terminé. Les lots
Asset/Map AV7–AV10 restent séparés et optionnels.

## Programme P1 — terminé

Les 196 fonctions P0 et les 50 lots `R001` à `R050` restent clos. Le programme P1 est autorisé et
clos sous le protocole `docs/planning/P1_ACCEPTANCE_PROGRAM.md` : les 298 fonctions P1 sont
`Tested`, dont les cinq fonctions de `R120 / FC-AI-P1-03`, les cinq de `R051 / FC-PROJECT-P1-01`,
les trois de `R052 / FC-PROJECT-P1-02`, les cinq de `R053 / FC-AREA-P1-01`, les cinq de
`R054 / FC-AREA-P1-02`, les cinq de `R055 / FC-AREA-P1-03`, la fonction de
`R056 / FC-AREA-P1-04`, les cinq de `R057 / FC-PALETTE-P1-01` et les trois de
`R058 / FC-PALETTE-P1-02`, les cinq de `R059 / FC-SHELL-P1-01` et la fonction de
`R060 / FC-SHELL-P1-02`, les cinq de `R061 / FC-AUDIO-P1-01` et les cinq de
`R062 / FC-AUDIO-P1-02`, la fonction de `R063 / FC-AUDIO-P1-03` et les cinq fonctions de
`R064 / FC-WAYPOINT-P1-01`, les quatre de `R065 / FC-WAYPOINT-P1-02` et les cinq de
`R066 / FC-GAMEPLAY-P1-01`, les cinq de `R067 / FC-GAMEPLAY-P1-02` et les cinq de
`R068 / FC-DIALOGUE-P1-01`, les cinq de `R069 / FC-DIALOGUE-P1-02` et les cinq de
`R070 / FC-DIALOGUE-P1-03`, les cinq de `R071 / FC-QUEST-P1-01`, les cinq de
`R072 / FC-QUEST-P1-02`, les deux de `R073 / FC-QUEST-P1-03` et les cinq de
`R074 / FC-COMPANION-P1-01`, les cinq de `R075 / FC-COMPANION-P1-02` et les trois de
`R076 / FC-COMPANION-P1-03`, les cinq de `R077 / FC-CREATURE-P1-01` et les cinq de
`R078 / FC-CREATURE-P1-02`, les cinq de `R079 / FC-CREATURE-P1-03` et les cinq de
`R080 / FC-CREATURE-P1-04`, les cinq de `R081 / FC-DOOR-P1-01` et la fonction de
`R082 / FC-DOOR-P1-02`, les cinq de `R083 / FC-ENCOUNTER-P1-01` et les cinq de
`R084 / FC-ENCOUNTER-P1-02`, la fonction de `R085 / FC-ENCOUNTER-P1-03` et les cinq de
`R086 / FC-FACTION-P1-01`, les quatre de `R087 / FC-FACTION-P1-02` et les cinq de
`R088 / FC-ITEM-P1-01`, les cinq de `R089 / FC-ITEM-P1-02` et les trois de
`R090 / FC-ITEM-P1-03`, les cinq de `R091 / FC-MERCHANT-P1-01`, les trois de
`R092 / FC-MERCHANT-P1-02`, les cinq de `R093 / FC-PLACEABLE-P1-01` et les cinq de
`R094 / FC-PLACEABLE-P1-02`, les trois de `R095 / FC-PLACEABLE-P1-03`, les quatre de
`R096 / FC-TRIGGER-P1-01`, les cinq de `R097 / FC-SCRIPT-P1-01` et les cinq de
`R098 / FC-SCRIPT-P1-02`, les cinq de `R099 / FC-SCRIPT-P1-03` et les cinq de
`R100 / FC-SCRIPT-P1-04`, la fonction de `R101 / FC-SCRIPT-P1-05` et les cinq de
`R102 / FC-MULTI-P1-01`, les cinq de `R103 / FC-MULTI-P1-02` et les trois de
`R104 / FC-MULTI-P1-03`, les cinq de `R105 / FC-DM-P1-01` et les cinq de
`R106 / FC-BUILD-P1-01`, les cinq de `R108 / FC-CONTENT-P1-01` et les cinq de
`R109 / FC-CONTENT-P1-02`, les deux de `R110 / FC-CONTENT-P1-03`, les cinq de
`R114 / FC-EE-P1-01`, les cinq de `R115 / FC-EE-P1-02`, les cinq de
`R116 / FC-EE-P1-03` et les quatre de `R117 / FC-EE-P1-04`. Les 70 lots P1 `R051` à `R120`
sont donc clos. La file P2 a ensuite été ouverte explicitement ; AV7–AV10, commit et push restent exclus.

## Périmètre Asset/Map

AV1 à AV6-F suffisent à la démonstration 3D actuelle. Les 25 lots `AV7-M*`, `AV8-*`, `AV9-*` et
`AV10-*` sont `OPTIONAL_POST_DEMO` : ils ne bloquent ni les 717 fonctions Aurora, ni le programme P1.
La décision est documentée dans `docs/planning/ASSET_SCOPE_DECISION_2026-08-07.md`.

## Preuves de santé

La validation de clôture R117 termine le programme P1 à 298/298 fonctions et 70/70 lots. NUI,
interpolation de transforms et compilateur sont reconfirmés. La chaîne TrueType/scaling relie
familles et fallbacks gouvernés, shaping, locales, paliers DPI, échelles éditeur/runtime, reflow,
focus clavier/manette et suite réelle de huit captures à deux résolutions. Le portail exécute
1 767 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 698 nœuds et 49 447 relations ;
le marqueur Toolset R117 est conservé et le portail termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R116 reconfirme les services JSON, snapshots, copie runtime et indicateur
AOE, puis ferme le chargement de zone amélioré. Le coordinateur respecte maintenant le mode
asynchrone ou séquentiel et la politique d’annulation du profil. La gate de temps retourne
`area.loading.target_exceeded` avec chemin canonique et UUID lorsque la cible est dépassée. Le
portail exécute 1 767 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 697 nœuds et
49 444 relations ; le marqueur Toolset R116 est conservé et le portail termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R115 ferme les budgets de lumières et le LOD/HLOD tout en reconfirmant
les règles data-driven, la NUI et les événements GUI. Les warnings de lumières sont capturés par le
profileur intégré sans plafond Aurora codé en dur. Le plan HLOD groupe les entités de manière
déterministe, expose un hook typé confiné, conserve crossfade et mesure du popping et refuse le
dépassement de clusters avant génération. Le portail exécute 1 765 tests .NET et 39 tests Python.
Le graphe Roslyn est frais à 7 691 nœuds et 49 412 relations ; le marqueur Toolset R115 est conservé
et le portail termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R114 reconfirme les transformations runtime et ferme le workspace
multi-zones, le PBR, la colorimétrie/HDR et l’éclairage par pixel. Deux viewports 3D distincts
existent déjà avec comparaison protégée. Le moteur charge désormais la LUT gouvernée, signale son
absence par `render.color.lut_missing` et applique le profil HDR aux viewports Toolset/runtime.
Forward+, les matériaux metallic-roughness, les budgets lumières/ombres et les diagnostics restent
partagés. Le portail exécute 1 763 tests .NET et 39 tests Python. Le graphe Roslyn est frais à
7 684 nœuds et 49 355 relations ; le marqueur Toolset R114 est conservé et le portail termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R110 relie le planificateur de migrations au store physique confiné.
L’étape compilée doit correspondre exactement au plan fiable ; le dry-run n’écrit rien, tandis que
l’application sauvegarde les fichiers, produit le changelog et permet une restauration explicite.
Toute erreur contrôlée déclenche le rollback automatique. La désinstallation refuse toujours les
dépendants non remplacés. Le portail exécute 1 763 tests .NET et 39 tests Python. Le graphe Roslyn
est frais à 7 682 nœuds et 49 352 relations ; le marqueur Toolset R110 est conservé et le portail
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R109 relie animations, tables typées, édition externe, pont Blender et
variantes plateforme aux contrats contenus existants. L’audit confirme bibliothèque de clips,
retarget, root motion, événements, compression et aperçu moteur ; les tables couvrent types,
références, migrations et overrides. Le nouveau watcher confiné détecte les changements réels,
recalcule SHA-256/taille, produit un diff sémantique et un reimport dry-run limité aux opérations
allowlistées, puis acquitte sa baseline. Le portail exécute 1 760 tests .NET et 39 tests Python. Le
graphe Roslyn est frais à 7 672 nœuds et 49 290 relations ; le marqueur Toolset R109 est conservé et
le portail termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R108 relie import/export en masse, export blueprint transitif, manifeste
content-addressed, polices TrueType et navigation/collision à la commande canonique d’assets. Les
polices principale et de repli portent désormais chunks, SHA-256, shaping, DPI, bornes de taille et
locales de test ; navigation et collision utilisent des chunks séparés avec génération/édition,
matériaux et cuisson plateforme. Le store atomique conserve ces extensions, le résolveur runtime
vérifie leurs chunks requis et les écrans Asset Browser, Content Workflow et Workspace exposent le
même état. Le portail exécute 1 758 tests .NET et 39 tests Python. Le graphe Roslyn est frais à
7 663 nœuds et 49 238 relations ; le marqueur Toolset R108 est conservé et le portail termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R106 relie import/export en masse, merge assisté, packaging Windows,
documentation générée et task manifests aux mêmes commandes Domain/Application que leurs écrans.
L’audit source confirme que le batch existant est confiné, atomique et gouverné ; le nouveau merge
trois voies traite aussi les graphes UUID et remappe déterministement les arêtes en `PreserveBoth`.
Le packaging honore désormais les modes portable/installeur, détecte les prérequis avec Windows
PowerShell 5.1, vérifie le SHA-256 après copie, journalise conditionnellement et désinstalle. Le
portail exécute réellement install/uninstall, 1 756 tests .NET et 39 tests Python. Le graphe Roslyn
est frais à 7 653 nœuds et 49 171 relations ; le marqueur Toolset R106 est conservé et le portail
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R105 relie authentification GM, inspection, liste des joueurs, journal
d’audit et chat GM à un profil canonique partagé par Toolset, runtime, ForgeScript, MCP et serveur.
Le scénario prouve MFA, expiration/révocation, six sections d’inspection en lecture seule, huit
colonnes joueur avec latence serveur rafraîchie, recalcul intégral de la chaîne SHA-256, sept canaux
modérés et refus d’une mutation auteur par un joueur. La preuve visuelle 1500×920 montre les cinq
politiques, leurs champs, permissions, transactions, audit et résultats serveur sans chevauchement.
La suite consolidée exécute 1 753 tests .NET et 39 tests Python. Le graphe Roslyn est frais à
7 642 nœuds et 49 114 relations ; le portail intégré conserve les marqueurs
Toolset/runtime/serveur R105 et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R104 relie logs JSON/texte, rotation et sinks distants, anti-cheat
autoritaire et campagnes réseau déterministes à un profil assurance canonique partagé par Toolset,
runtime, ForgeScript, MCP et serveur. Le serveur vérifie contenu signé, cadence et intentions,
RNG autoritaire, seuil d'anomalie et refus des sauvegardes client ; le runner simule trois clients,
latence, perte, duplication, réordonnancement, trois replays et un soak borné avec assertions d'état.
La preuve visuelle 1500×920 montre les trois familles, leurs champs, permissions, transactions,
audit et résultats serveur sans chevauchement. La suite consolidée exécute 1 743 tests .NET et
39 tests Python. Le graphe Roslyn est frais à 7 602 nœuds et 48 861 relations ; le portail intégré
conserve les marqueurs Toolset/runtime/serveur R104 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R103 relie synchronisation de contenu, verrou de version, six rôles avec
ABAC, chat accessible et bus d’événements prioritaire à un profil opérations canonique partagé par
Toolset, runtime, ForgeScript, MCP et serveur. Les transferts sont chunkés, repris, adressés par hash
et soumis aux licences, quotas et signataires ; les mismatches avant join sont lisibles. Le chat
couvre local, groupe, tell, shout, DM, langues et bridges ; le bus trace les handlers, refuse le
rejeu et applique une backpressure bornée. La preuve visuelle 1500×920 montre les cinq familles,
leurs champs, permissions, transactions et résultats serveur sans chevauchement. La suite consolidée
exécute 1 733 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 551 nœuds et 48 537
relations ; le portail intégré conserve les marqueurs Toolset/runtime/serveur R103 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R102 relie serveur dédié et autoritaire, groupes, réplication et interest
management à un profil réseau canonique partagé par Toolset, runtime, ForgeScript, MCP et serveur.
Le cycle de groupe couvre invitations, rôles, leader, loot, quêtes, marqueurs et séparation ; la
réplication borne composants et deltas, ownership, prédiction et réconciliation, tandis que
l’intérêt combine zone, distance, visibilité, groupe, ownership et abonnements explicites. La
preuve visuelle 1500×920 expose configuration, politiques, permissions, audit et digest sans
chevauchement. La suite consolidée exécute 1 724 tests .NET et 39 tests Python. Le graphe Roslyn est
frais à 7 501 nœuds et 48 210 relations ; le portail intégré conserve les marqueurs
Toolset/runtime/serveur R102 et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R101 relie l’éditeur ForgeScript et MCP à une façade commune pour l’API,
les sources, la compilation, les diagnostics, les tests et les bindings d’événement. Le profil
interop canonique porte UUID, schéma, provenance, API, binding et quotas ; preview/apply/reject,
Undo/Redo et snapshot exact partagent la même frontière. Le runtime optionnel/requis réutilise la
sandbox native, refuse les handles obsolètes et récupère après dépassement de quota. La preuve
visuelle 1680×1000 montre sources, outils, transactions, événements, console et diagnostic sans
chevauchement. La suite consolidée exécute 1 715 tests .NET et 39 tests Python. Le graphe Roslyn est
frais à 7 447 nœuds et 47 871 relations ; le portail intégré conserve les marqueurs Toolset/runtime
R101, restaure les modules exemples et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R100 relie tests déterministes, profilage par handler, référence API
générée, analyse statique et état script migrable à un profil canonique transactionnel, à MCP et à
un adaptateur runtime unique. Le Toolset expose les cinq outils dans l’éditeur ForgeScript existant
avec diagnostics cliquables ; le runtime projette les mêmes résultats dans un panneau accessible.
La preuve visuelle 1680×1000 montre les cinq surfaces, la console compilée et quatre diagnostics
localisés visibles sans chevauchement. La suite consolidée exécute 1 708 tests .NET et 39 tests
Python. Le graphe Roslyn compte 7 426 nœuds et 47 767 relations. Le portail intégré conserve les
marqueurs Toolset/runtime R100, prouve l’arrêt sur quota et la récupération, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R099 relie audio, temps, événements custom, hot reload et débogueur à un
service ForgeScript partagé, à des handles natifs opaques et à un adaptateur runtime unique. Le
Toolset expose les cinq outils dans l'éditeur existant ; le runtime projette les mêmes états dans un
panneau accessible avec états vide, erreur bornée, console et commandes focusables. La preuve
visuelle 1680×1000 montre les cinq surfaces sans chevauchement. La suite consolidée exécute 1 704
tests .NET et 39 tests Python. Le graphe Roslyn est frais ; le portail intégré conserve les marqueurs
Toolset/runtime R099, prouve l'arrêt sur quota et la récupération, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R098 relie les transactions d'inventaire, transitions de quête, sessions de
dialogue, adhésions/réputations de faction et requêtes de perception à un service RPG Kit partagé, à
des handles natifs opaques et à un adaptateur runtime unique. Le Toolset expose les cinq API et leur
état dans l'éditeur ForgeScript existant ; le runtime projette les mêmes résultats dans un panneau
accessible avec états vide, erreur bornée, console et commandes focusables. La preuve visuelle
1680×1000 montre les cinq surfaces sans chevauchement. La suite consolidée exécute 1 694 tests .NET
et 39 tests Python. Le graphe Roslyn est frais ; le portail intégré conserve les marqueurs
Toolset/runtime R098, valide le playtest T32, restaure le module exemple à l'octet près et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R097 relie l'éditeur complet, le Script Wizard, les actions différées, le
combat et les effets au document source canonique, à la sandbox native et aux services RPG Kit. Le
Toolset expose coloration, analyse, source relisible et diagnostics ; le runtime projette les tâches,
attaques et effets dans un panneau accessible. La preuve visuelle 1680×1000 est lisible sans
chevauchement. La suite consolidée exécute 1 684 tests .NET et 39 tests Python. Le graphe Roslyn est
frais ; le portail intégré conserve les marqueurs Toolset/runtime R097, valide le playtest
T32, restaure le module exemple à l'octet près et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R092 relie le stock infini, les sessions StoreSessionOpened/Closed et
l'interface boutique accessible au profil marchand canonique. Le Studio expose API, bindings, payload
et configuration clavier/manette/comparaison/filtres/panier/confirmation ; ForgeScript, MCP et le runtime
réutilisent les services RPG Kit existants et un panneau boutique réellement focusable. La preuve
visuelle 1440×920 est lisible sans chevauchement. La suite consolidée exécute 1 648 tests .NET et 39 tests
Python. Le graphe Roslyn est frais à 7 290 nœuds / 46 787 relations ; le portail intégré conserve les
marqueurs Toolset/runtime R092, valide le playtest T32, restaure le module exemple à l'octet près et
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R091 relie le magasin indépendant, le wizard complet, le stock fini/infini,
les courbes achat/vente et les restrictions multi-facettes au profil marchand canonique. Le Studio
transactionnel expose ces sections avec aperçu sémantique ; ForgeScript, MCP et le runtime réutilisent
les services RPG Kit existants. La preuve visuelle 1440×920 est lisible sans chevauchement. La suite
consolidée exécute 1 643 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 278 nœuds /
46 700 relations ; le portail intégré conserve les marqueurs Toolset/runtime R091, valide le playtest
T32, restaure le module exemple à l'octet près et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R090 relie les tables de loot pondérées, l'inspecteur d'inventaire et les neuf
événements de cycle de vie au blueprint objet canonique. Le Studio transactionnel expose le tirage seedé,
les filtres, la comparaison et la prévisualisation multi-édition ; ForgeScript, MCP et le runtime partagent
le même profil via les services RPG Kit existants. La preuve visuelle 1440×920 est lisible sans
chevauchement. La suite consolidée exécute 1 637 tests .NET et 39 tests Python. Le graphe Roslyn est frais
à 7 262 nœuds / 46 579 relations ; le portail intégré conserve les marqueurs Toolset/runtime R090,
restaure le module exemple à l'octet près et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R089 relie les protections d'objet plot, les huit destinations de transfert,
les modificateurs data-driven, le pouvoir unique et les slots d'équipement au blueprint objet
canonique. Le Studio transactionnel expose les cinq sections avec aperçu live des modificateurs et
conflits ; ForgeScript, MCP et le runtime partagent le même profil via les services RPG Kit existants.
La preuve visuelle 1440×920 est lisible sans chevauchement. La suite consolidée exécute 1 632 tests
.NET et 39 tests Python. Le graphe Roslyn est frais à 7 240 nœuds / 46 408 relations ; le portail
intégré conserve les marqueurs Toolset/runtime R089, restaure le module exemple à l'octet près et
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R088 relie l'assistant à huit sections, l'apparence indépendante avec
meshes, matériaux, sockets, icône, VFX et aperçu équipé, l'encombrement, les piles atomiques et la
ressource d'usage au profil objet canonique. Le Studio conserve la commande transactionnelle,
l'Undo/Redo et un aperçu live ; ForgeScript, MCP et le runtime consomment les mêmes données via
l'adaptateur RPG Kit existant. La preuve visuelle 1440×920 expose les cinq capacités et leurs règles
sans chevauchement. La suite consolidée exécute 1 627 tests .NET et 39 tests Python. Le graphe Roslyn
est frais à 7 218 nœuds / 46 244 relations ; le portail intégré conserve les marqueurs
Toolset/runtime R088, restaure le module exemple à l'octet près et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R087 persiste les transactions globales aux scopes monde, serveur,
campagne, groupe et joueur, valide les tags sémantiques, conserve un historique de heatmap
comparable/importable/exportable et applique une politique de réaction ordonnée réputation,
déguisement, crime, dialogue, zone puis ordre MJ. Le Studio réutilise la commande faction
transactionnelle ; ForgeScript, MCP et le runtime partagent le même profil canonique. La preuve
visuelle 1540×940 montre cinq transactions, deux captures, sept contrôles de réaction, une
différence et une simulation sans chevauchement. La suite consolidée exécute 1 622 tests .NET et
39 tests Python. Le graphe Roslyn est frais à 7 200 nœuds / 46 126 relations ; le portail intégré
conserve les marqueurs Toolset/runtime R087, restaure le module exemple à l'octet près et termine
sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R086 rend authorables et persistantes la création, l'archive et la
suppression sûre avec migration, la matrice directionnelle asymétrique, les bandes de disposition
et les surcharges personnelles par acteur, joueur ou groupe. Le Studio affiche ces données et leur
heatmap dans la commande transactionnelle existante ; ForgeScript, MCP et le runtime partagent le
même catalogue validé. La preuve visuelle montre trois factions, les deux directions, trois bandes
et une surcharge joueur sans chevauchement. La suite consolidée exécute 1 615 tests .NET et 39 tests
Python. Le graphe Roslyn est frais à 7 184 nœuds / 45 988 relations ; le portail intégré conserve
les marqueurs Toolset/runtime R086, restaure le module exemple à l'octet près et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R085 rend authorables et persistants le nombre de runs, le seed de base,
les estimateurs de durée/récompense et leurs coefficients, les percentiles et les overrides. Le
Studio prévisualise les 64 seeds consécutifs de l'échantillon ; ForgeScript/MCP lisent le même profil,
l'adaptateur réutilise `EncounterWaveService.Simulate` et le runtime matérialise un nœud statistique
explicite. La preuve visuelle montre la politique requise, 64 runs, le seed 840, les deux estimateurs
et le résumé des percentiles sans chevauchement. La suite consolidée exécute 1 608 tests .NET et 39
tests Python. Le graphe Roslyn est frais à 7 169 nœuds / 45 886 relations ; le portail intégré
conserve les marqueurs Toolset/runtime R085 et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R084 relie les ancres manuelles transformées, les restrictions joueur et
l'autorité réseau, `EncounterEntered`, `EncounterExited`, `EncounterExhausted` et le graphe UUID des
phases/vagues au profil canonique, au studio transactionnel, à ForgeScript/MCP et aux services RPG
Kit existants. Le runtime matérialise quatre groupes de nœuds explicites et le bilan d'épuisement
transporte cause, kills, échappés, vagues et récompenses. La preuve visuelle montre capacité,
navigation, visibilité, fallback, cinq exclusions, autorité serveur et résumé cycle/graphe. La suite
consolidée exécute 1 601 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 156 nœuds /
45 812 relations ; le portail intégré conserve les marqueurs Toolset/runtime R084 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R083 relie l'assistant de rencontre, la difficulté pilotée par ruleset,
les bornes minimum/maximum et budgets de groupes, la sélection selon un fournisseur d'évaluation
du groupe et les sept modes d'activation au profil canonique, au studio transactionnel,
à ForgeScript/MCP, à l'adaptateur RPG Kit et à cinq nœuds runtime. La preuve visuelle montre la
difficulté, le ruleset, l'évaluateur de groupe, le budget et le résumé sémantique. La suite
consolidée exécute 1 591 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 131 nœuds /
45 612 relations ; le portail intégré conserve les marqueurs Toolset/runtime R083 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R082 relie `DoorLocked` et `DoorUnlocked`, avec méthode, acteur,
credential, états et provenance, au profil canonique de porte, au studio transactionnel,
à ForgeScript/MCP, à l'adaptateur RPG Kit et à un nœud runtime explicite. La preuve visuelle a été
inspectée avec les deux bindings et la politique runtime requis visibles ensemble. La suite
consolidée exécute 1 581 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 103 nœuds /
45 406 relations ; le portail intégré conserve les marqueurs Toolset/runtime R082 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R081 relie verrou et credentials, refus d'ouverture localisé, piège de
porte, cycle de portail demandé/complété et événements terminaux d'ouverture/fermeture au profil de
porte canonique, au studio transactionnel, à ForgeScript, au MCP, à l'adaptateur RPG Kit et à cinq
groupes de nœuds runtime. La preuve visuelle a été inspectée avec asset, transform, état verrouillé,
credential et résumé sémantique des cinq fonctions lisibles sans chevauchement. Les tests ciblés et
les smokes Toolset/runtime sont verts. La suite consolidée exécute 1 576 tests .NET et 39 tests
Python. Le graphe Roslyn est frais à 7 099 nœuds / 45 383 relations ; le portail intégré conserve
les marqueurs Toolset/runtime R081 et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R080 relie les cycles `OnPerception`, `OnPhysicalAttacked`,
`OnSpellCastAt` et `OnBlocked`, ainsi
qu'un contrôleur IA interchangeable avec blackboard typé, au blueprint canonique, à l'interface
transactionnelle, à ForgeScript, au MCP, à l'adaptateur RPG Kit et à cinq groupes de nœuds runtime.
La preuve visuelle 1500×930 a été inspectée avec les quatre cycles d'événements, le contrôleur
Utility, l'ordre de récupération et la simulation déterministe lisibles sans chevauchement. Les
tests ciblés Domain/RPG Kit/Application/ModuleFormat/MCP et les smokes Toolset/runtime sont verts.
La suite consolidée exécute 1 566 tests .NET et 39 tests Python. Le graphe Roslyn est frais à
7 073 nœuds / 45 178 relations ; le portail intégré conserve les marqueurs Toolset/runtime R080 et
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R079 relie factions pondérées, perception vision/ouïe/odorat/magie,
conversations contextuelles, protections narratives et `OnConversation` au blueprint canonique, à
l’interface transactionnelle, à ForgeScript, au MCP, à l’adaptateur RPG Kit et à cinq groupes de
nœuds runtime. La preuve visuelle 1500×930 a été inspectée avec deux appartenances, quatre sens,
sélection de dialogue, protections et événement lisibles sans chevauchement. La suite consolidée
exécute 1 558 tests .NET et 39 tests Python. Le graphe Roslyn est frais à 7 040 nœuds / 44 899
relations ; le portail intégré conserve les marqueurs Toolset/runtime R079 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R078 relie état initial et pose, attributs, défenses et sauvegardes,
capacités spéciales et inventaire au blueprint canonique, à l’interface transactionnelle, à
ForgeScript, au MCP, à l’adaptateur RPG Kit et aux cinq groupes de nœuds du personnage 3D runtime.
La preuve visuelle 1500×930 a été inspectée avec état assis, pose, attributs calculés, défense,
capacité et inventaire lisibles. La suite consolidée exécute 1 550 tests .NET et 39 tests Python.
Le graphe Roslyn est frais à 7 008 nœuds / 44 606 relations ; le portail intégré conserve les
marqueurs Toolset/runtime R078 et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R077 relie l’assistant, rôle et menace, espèce et TraitSet, apparence et
AnimationGraph au blueprint canonique, à l’interface transactionnelle, à ForgeScript, au MCP, à
l’adaptateur RPG Kit et aux nœuds du personnage 3D runtime. La suite consolidée exécute 1 542 tests
.NET et 39 tests Python. La preuve visuelle 1500×930 a été inspectée avec rôle Boss/menace 12,
espèce, traits, modèle, mini-carte, simulation et résumé lisibles. Le graphe Roslyn est frais à
6 975 nœuds / 44 346 relations ; le portail intégré conserve les marqueurs Toolset/runtime R077 et
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R076 relie les six politiques de survie, le rattrapage par navigation,
portail ou téléportation sûre et le HUD de groupe accessible au document canonique, à l’interface
transactionnelle, à ForgeScript, au MCP et au runtime. Le HUD est un panneau moteur visible avec
état, ressources, statuts, commandes rapides et réordonnancement. La suite consolidée exécute
1 535 tests .NET sans échec. La preuve visuelle 1450×940 a été inspectée avec seuils 10/30/60,
ancre sûre, HUD, clavier/manette et résumé accessibilité lisibles sans chevauchement global. Le
portail intégré conserve les marqueurs Toolset/runtime R076 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

La validation de clôture R075 relie les cinq modes de propriété et leurs transferts, les douze
ordres de déplacement/combat, l’IA autonome gouvernée et l’équipement permissionné au document de
monde canonique, à l’interface transactionnelle, à ForgeScript, au MCP et à de vrais nœuds moteur.
La suite consolidée exécute 1 531 tests .NET sans échec. La preuve visuelle 1450×940 a été inspectée
avec les 12 commandes, la propriété et les transferts, l’IA et l’équipement lisibles sans
chevauchement global. Le graphe Roslyn est frais à 6 936 nœuds / 44 065 relations ; le portail
intégré termine sur `OPENRPG_FORGE_VERIFICATION=PASS` et conserve les marqueurs Toolset/runtime R075.

La validation de clôture R074 relie composant et politique de recrutement, contextes avant/après
recrutement, transaction atomique et quotas de groupe au document de monde canonique, à l'interface
transactionnelle, à ForgeScript, au MCP et à de vrais nœuds moteur. La suite consolidée exécute
1 527 tests .NET sans échec. La preuve visuelle 1450×940 a été inspectée avec politique, voies de
recrutement, contextes, atomicité, quotas C/I/F/A, rôle et limite serveur lisibles sans
chevauchement global. Le graphe Roslyn est frais à 6 923 nœuds / 43 969 relations ; son portail
intégré est vert.

La validation de clôture R073 attache cinq scénarios de test déterministes et un plan de génération
IA gouverné à la quête canonique, à l'interface transactionnelle, à ForgeScript, au MCP et à de vrais
nœuds moteur. Le portail construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 523 tests .NET et 39 tests Python, valide les smokes Toolset/runtime R073, Blender, assets,
exports, cartes 3D, réseau, MCP et sauvegarde/reprise, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`. La preuve visuelle 1450×910 a été inspectée avec les cinq chemins,
11 nœuds, 6 arêtes, les seeds, le plan approuvé, ses dépendances, citations et règles de style visibles.
Le graphe Roslyn est frais à 6 908 nœuds / 43 873 relations.

La validation de clôture R072 relie ressources typées, étapes éditables, visibilité conditionnelle,
chronologie joueur/debug et pins de carte à la quête canonique, à l'interface transactionnelle, à
ForgeScript, au MCP et à de vrais nœuds moteur. Le portail construit les 27 projets sans
vulnérabilité, avertissement ni erreur, exécute 1 516 tests .NET et 39 tests Python, valide les
smokes Toolset/runtime R072, Blender, assets, exports, cartes 3D, réseau, MCP et sauvegarde/reprise,
puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. La preuve visuelle 1450×910 corrigée a été
inspectée avec 5 ressources, 1 étape, 4 révélations, 2 événements et 4 pins visibles. Le graphe
Roslyn est frais à 6 874 nœuds / 43 653 relations.

La validation de clôture R071 relie huit types d’objectifs, récompenses atomiques, notes et reviews,
cast et Plot Wizard transparent à la quête canonique, à l’interface transactionnelle, à ForgeScript,
au MCP et à de vrais nœuds moteur. Le portail construit les 27 projets sans vulnérabilité,
avertissement ni erreur, exécute 1 508 tests .NET et 39 tests Python, valide les smokes
Toolset/runtime R071, Blender, assets, exports, cartes 3D, réseau, MCP et sauvegarde/reprise, puis
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. La preuve visuelle 1450×910 a été inspectée avec
résumé des objectifs/cast/Wizard, récompenses, notes, mini-graphe et simulation visibles sans
chevauchement global. Le graphe Roslyn est frais à 6 849 nœuds / 43 444 relations.

La validation de clôture R070 relie graphes imbriqués versionnés, conséquences typées, couverture de
branches, collaboration et génération IA contrôlée au dialogue canonique, à l’interface
transactionnelle, à ForgeScript, au MCP et à de vrais nœuds moteur. Le portail construit les 27
projets sans vulnérabilité, avertissement ni erreur, exécute 1 501 tests .NET et 39 tests Python,
valide les smokes Toolset/runtime R070, Blender, assets, exports, cartes 3D, réseau, MCP et
sauvegarde/reprise, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Le graphe Roslyn est frais à
6 824 nœuds / 43 232 relations. La preuve visuelle 1440×950 a été inspectée avec sous-dialogue,
quatre impacts, couverture, collaboration, preview IA, simulation et validation visibles sans
chevauchement global.

La validation de clôture R069 relie fin/abandon, recherche et remplacement preview-only, six badges de
revue, mémoire typée et checks de compétence au dialogue canonique, à l’interface transactionnelle,
à ForgeScript, au MCP et à de vrais nœuds moteur. Le portail construit les 27 projets sans
vulnérabilité, avertissement ni erreur, exécute 1 494 tests .NET et 39 tests Python, valide les smokes
Toolset/runtime R069, Blender, assets, exports, cartes 3D, réseau, MCP et sauvegarde/reprise, puis
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Le graphe Roslyn est frais à 6 778 nœuds / 42 931
relations. La preuve visuelle 1440×950 corrigée a été inspectée avec 4 résultats de recherche, tous
les libellés, les six badges, la mémoire et le check visibles sans chevauchement ni défilement
horizontal global.

La validation de clôture R068 relie les tokens typés, les participants multiples, les cinq modes
d'évaluation, le wizard de scripts sourcé et les notes éditoriales au dialogue canonique, à
l'interface transactionnelle, à ForgeScript, au MCP et à de vrais nœuds moteur. Le portail construit
les 27 projets sans vulnérabilité, avertissement ni erreur, exécute 1 487 tests .NET et 39 tests
Python, valide les smokes Toolset/runtime R068, Blender, assets, exports, cartes 3D, réseau, MCP et
sauvegarde/reprise, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Le graphe Roslyn est frais à
6 744 nœuds / 42 676 relations. La preuve visuelle 1440×950 du panneau auteur de dialogues a été
inspectée avec participants, token, cinq modes, wizard, sources, notes, simulation et validation
lisibles sans chevauchement.

La validation de clôture R067 relie les sauvegardes player/game/server, les checkpoints et le
respawn, le shell carte/journal/HUD, les verrous de contrôle et le replay déterministe au profil
module canonique, à l'interface transactionnelle, à ForgeScript, au MCP et aux adaptateurs moteur.
Le portail construit les 27 projets sans vulnérabilité, avertissement ni erreur, exécute 1 477 tests
.NET et 39 tests Python, valide les smokes Toolset/runtime R067, Blender, assets, exports, cartes 3D,
réseau et sauvegarde/reprise, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Le graphe Roslyn est
frais à 6 706 nœuds / 42 391 relations. La preuve visuelle 1440×900 de l'onglet
`Session & replay P1` a été inspectée avec le parcours et les cinq cartes entièrement visibles.

La validation de clôture R066 relie vol 3D, projectiles, physique, huit modes de ciblage et temps du
monde au profil module canonique, à l'interface transactionnelle, à ForgeScript, au MCP et à de vrais
nœuds moteur. Le portail construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 469 tests .NET et 39 tests Python, valide les smokes Toolset/runtime R066, Blender, assets,
exports, cartes 3D, réseau et sauvegarde/reprise, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`. Le graphe Roslyn est frais à 6 680 nœuds / 42 177 relations.
La preuve visuelle 1440×900 de l'onglet `Simulation 3D P1` a été inspectée sans rognage de
l'aperçu, chevauchement ni valeur numérique décalée.

La validation de clôture R065 relie l’orientation d’arrivée, les locators audio/VFX/spawn/objectif,
les collections filtrées et le diagnostic de routes au même profil canonique, à la commande de zone,
à la persistance, à ForgeScript, au MCP et au runtime moteur. Le portail construit les 27 projets
sans vulnérabilité, avertissement ni erreur, exécute 1 461 tests .NET et 39 tests Python, valide les
smokes Toolset/runtime R065, Blender, assets, exports, cartes 3D, réseau et sauvegarde/reprise, puis
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Le graphe Roslyn est frais à 6 648 nœuds / 41 900
relations. La preuve visuelle 1440×900 de l’onglet `Locators & diagnostic` a été inspectée après
correction du rognage, du pas numérique et des chevauchements.

La validation de clôture R064 construit les 27 projets sans avertissement ni erreur, exécute
1 452 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes Toolset/runtime et
le graphe Roslyn frais à 6 629 nœuds / 41 734 relations, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`. Apparence d’éditeur, note de carte et révélation, routes ordonnées
et six politiques de traversée convergent vers la même commande, persistance, API ForgeScript/MCP,
HUD et acteur moteur autoritaire. La preuve visuelle 1440×820 de l’onglet
`Waypoints & patrouilles` a été inspectée après correction du rognage horizontal.

La validation de clôture R063 construit les 27 projets sans avertissement ni erreur, exécute
1 444 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe Roslyn frais
à 6 594 nœuds / 41 453 relations, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les sous-titres
dialogue, effet et ambiance sont localisés avec repli, importance, durée de lecture et direction.
Le HUD runtime les affiche réellement et distingue absence optionnelle et exigence invalide. La
preuve visuelle 1320×820 de la carte `Accessibilité audio` a été inspectée sans chevauchement.

La validation de clôture R062 construit les 27 projets sans avertissement ni erreur, exécute
1 440 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe Roslyn frais
à 6 581 nœuds / 41 356 relations,
puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les rayons interne/externe et leurs sphères 3D,
le handle ForgeScript Start/Stop/Pause/Resume/Fade, les crossfades adaptatifs, les transitions
musicales synchronisées à la mesure et le monitoring pic/RMS/LUFS pilotent de vrais lecteurs et
bus moteur. La preuve visuelle 1320×820 de l'onglet `Orchestration audio` a été inspectée sans
chevauchement.

La validation de clôture R061 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 435 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R061, Blender, assets, exports, sauvegardes, parcours 3D et le graphe Roslyn frais,
puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Le navigateur audio recherche nom, chemin, tag
et transcript, rend une waveform et prépare l'écoute simple ou par lot. Sélection séquentielle,
aléatoire, shuffle bag ou pondérée, modulation par distributions/courbe/tempo, normalisation LUFS,
bus, ducking, plafond d'accessibilité et gains par auditeur convergent vers la même transaction,
persistance, API MCP/ForgeScript et lecture runtime 2D. La preuve visuelle 1320×820 a été inspectée.

La validation de clôture R060 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 429 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R060, Blender, assets, exports, sauvegardes, parcours 3D et le graphe Roslyn frais
6 536/41 002, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. La politique d’autosauvegarde
versionnée, la détection de session interrompue, les snapshots hashés et confinés, la comparaison
obligatoire et la restauration sélective de cinq sections passent par les mêmes commandes, historique,
persistance et plans MCP. La preuve visuelle 1180×760 a été inspectée.

La validation de clôture R059 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 425 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R059, Blender, assets, exports, sauvegardes, parcours 3D et le graphe Roslyn frais
6 521/40 857, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Filtres par calques et volumes,
pinceau continu aligné sur surface, vue partagée à deux viewports, copie inter-zones protégée, aide
locale sourcée et sept profils métier convergent vers les mêmes profils versionnés, transactions,
MCP, persistance et replis runtime optionnel/requis. La preuve visuelle 1760×900 a été inspectée.

La validation de clôture R058 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 420 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R058, Blender, assets, exports, sauvegardes, parcours 3D et le graphe Roslyn frais
6 479/40 525, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Variantes héritées par deltas,
compositions hiérarchiques, collections auteur, placement groupé et budgets à sept axes par
plateforme convergent vers les mêmes contrats UI/ForgeScript/MCP avec persistance, préflight,
rollback et repli runtime optionnel/requis. La preuve visuelle 1760×900 a été inspectée.

La validation de clôture R057 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 415 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R057, Blender, assets, exports, sauvegardes, parcours 3D et le graphe Roslyn frais
6 436/40 098, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Mise à jour des blueprints et
instances, catégories auteur hiérarchiques, recherche technique avancée et turntable OBJ texturé à
huit angles convergent vers les mêmes contrats UI/CLI/ForgeScript/MCP, avec persistance,
préflight, rollback et repli runtime optionnel/requis. La preuve visuelle 1240×780 a été inspectée.

La validation de clôture R056 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 408 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R056, les exports, sauvegardes, parcours 3D et le graphe frais 6 395/39 784, puis
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les calques possèdent UUID, schéma, provenance,
hiérarchie, visibilité et verrouillage hérités, ownership, sélection de groupe synchronisée au
viewport et aperçu d’export partiel gouverné ; UI, commande, ForgeScript, MCP, persistance,
préflight et repli runtime convergent sur le même profil canonique.

La validation de clôture R055 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 399 tests .NET et 39 tests Python, valide le transport stdio MCP, les smokes
Toolset/runtime R055, les exports, sauvegardes, parcours 3D et le graphe frais 6 377/39 631, puis
termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Audio adaptatif, événements de zone, cartes 2D/3D,
brouillard de guerre, visibilité par salles/portails/occluders et LOD convergent vers une transaction
persistante UI/ForgeScript/MCP avec repli runtime explicite. Le constructeur de sérialisation de la
carte expose un UUID requis, ce qui garde le schéma MCP lançable tout en conservant l'ancien appel.

La validation de clôture R054 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 390 tests .NET et 39 tests Python, restaure le prototype après le smoke Toolset, valide les
smokes Toolset/runtime R054, MCP, réseau, exports, sauvegarde/reprise et parcours 3D ainsi que le
graphe frais 6 340/39 300, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. La copie de zone
préserve terrain, navigation et éclairage tout en renouvelant les identités ; classification,
jour/nuit, éclairage physique et brouillard avancé convergent vers une transaction persistante
UI/ForgeScript/MCP avec fallback runtime explicite.

La validation de clôture R053 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 382 tests .NET et 39 tests Python, restaure le prototype après le smoke Toolset, valide les
smokes Toolset/runtime R053, MCP, réseau, exports, sauvegarde/reprise et parcours 3D ainsi que le
graphe frais 6 309/39 047, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les groupes de tuiles
portent paramètres, empreinte, sockets et variantes ; les outils erase/replace/remap/fill/select,
l'auto-fit déterministe, les propriétés spécialisées et le redimensionnement avec repositionnement
convergent vers une transaction persistante UI/ForgeScript/MCP avec fallback runtime explicite.

La validation de clôture R052 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 374 tests .NET et 39 tests Python, restaure le prototype byte pour byte après le smoke
Toolset, valide les smokes Toolset/runtime R052, MCP, réseau, exports, sauvegarde/reprise 1.4 et
parcours 3D ainsi que le graphe frais 6 283/38 818, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`. Les événements utilisateur globaux sont namespacés, typés et
versionnés ; l'horloge conserve vitesse, calendrier, pause, fuseaux et événements astronomiques ;
les huit pages joueur localisées sont persistantes, inspectées et rejetées au pré-build si invalides.

La validation de clôture R051 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 366 tests .NET et 39 tests Python, restaure le prototype byte pour byte après le smoke
Toolset, valide les smokes Toolset/runtime R051, MCP, réseau, exports et parcours 3D ainsi que le
graphe frais 6 262/38 627, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les 14 événements
projet configurables couvrent connexion, inventaire, utilisation, mort/respawn, équipement et
progression avec autorité serveur, transaction, UI, ForgeScript, MCP et rejet pré-build localisé.

La validation de clôture R120 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 359 tests .NET et 39 tests Python, valide les smokes Toolset/runtime/MCP, les exports, les
parcours 3D et le graphe frais 6 252/38 537, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.
L'agent de test reste en processus et sans shell, limite ses actions au gameplay, produit couverture,
reproduction et citations, refuse un profil invalide avant build et ne réécrit jamais la narration.

La validation de clôture R050 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 352 tests .NET et 39 tests Python, valide les smokes Toolset/runtime/MCP, les exports
reproductibles et le graphe frais 6 238/38 423, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`. Le NOTICE agrège désormais module, blueprints, tables, packages,
sources, assets et dépendances ; une licence inconnue bloque Shipping avant compilation ou écriture.

La validation de clôture R049 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 348 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 237/38 389, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Auto-intersections, filtres
contradictoires, cibles, portail, cooldown, scripts et accessibilité des triggers sont désormais
rejetés avant mutation/build avec contexte persistant et fallback runtime optionnel/requis.

La validation de clôture R048 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 346 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 234/38 376, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les triggers canoniques portent
désormais volume 2D/3D, filtres, cooldown, actions et portail typés, ainsi que les événements
VolumeEntered/VolumeExited ; UI, transaction, persistance, ForgeScript, MCP et fallback runtime sont prouvés.

La validation de clôture R047 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 343 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 217/38 271, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. OnUsed porte désormais verbe,
acteur, cible, résultat, provenance et API typés ; collision/navigation/animation, locks, traps,
inventaire, scripts, physique et budgets sont rejetés au pré-build lorsqu'ils sont incohérents.

La validation de clôture R046 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 341 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 213/38 256, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. La fondation placeable relie au
projet identité/visuel, rôle statique ou utilisable, collision, composants et transform complet ;
l'éditeur, l'overlay pivot/snap, ForgeScript, MCP, la persistance et le runtime optionnel/requis sont prouvés.

La validation de clôture R045 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 338 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 202/38 200, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les transactions marchand
Buy/Sell/Barter valident désormais les agrégats de fonds, propriété, stock et capacité avant une
mutation atomique ; l'onglet Marchands, le handle typé, MCP et le runtime requis/optionnel sont prouvés.

La validation de clôture R037 construit les 27 projets sans vulnérabilité, avertissement ni erreur
et exécute les suites complètes Domain/Application/RpgKit.Domain/ModuleFormat/MCP à
320/341/144/268/72 tests. Le portail intégré confirme 1 306 tests .NET, 39 tests Python, les smokes
Toolset/runtime/export/terrain/MCP/réseau, le graphe déterministe à 6 101 nœuds et 37 471 relations,
puis `OPENRPG_FORGE_VERIFICATION=PASS`.

## Sources opérationnelles

1. `TASKS_MVP.md` pour le programme actif et la prochaine unité de travail ;
2. `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` pour les 187 lots et leur ordre ;
3. `tracking/project/AURORA_PARITY_COVERAGE.csv` pour le statut et les preuves des 717 fonctions ;
4. `docs/planning/P1_ACCEPTANCE_PROGRAM.md` pour le contrat d'exécution P1 ;
5. `docs/audits/COMPLETE_FUNCTION_COVERAGE_AUDIT_2026-08-07.md` pour l'audit et ses limites.

Le journal détaillé antérieur est archivé dans
`docs/history/STATUS_DELIVERY_JOURNAL_2026-08-07.md`.
