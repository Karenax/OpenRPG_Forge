# File de travail canonique — OpenRPG Forge

## Programme terminé : AURORA-717-CLOSURE

- [x] Conserver les 50 lots P0 `R001` à `R050` fermés et les 196 fonctions P0 `Tested`.
- [x] Fermer les 70 lots P1 `R051` à `R120` dans le périmètre autorisé. Les 298 fonctions P1 sont
  `Tested`.
- [x] Fermer les 20 lots P2 selon `docs/planning/P2_ACCEPTANCE_PROGRAM.md` sans
  ouvrir P3/P4 ni AV7–AV10 et sans commit ou push implicite.
- [x] Réauditer et fermer les 16 lots P3 et le lot P4 selon
  `docs/planning/P3_P4_ACCEPTANCE_PROGRAM.md` : 33 fonctions, 17 lots et 86 tests ciblés verts.
- [x] Conserver les lots Asset/Map AV7–AV10 hors de la complétude Aurora comme
  `OPTIONAL_POST_DEMO`.

La complétude Aurora est fermée à 717/717 fonctions dans 187/187 lots. L'unité d'exécution reste un
lot de une à cinq fonctions lors de toute réouverture pour régression. Les programmes d'acceptation
P0, P1, P2 et P3/P4 conservent le protocole et les preuves de chaque palier.

### Lot courant

- [x] `R121` — `FC-MULTI-P2-01` : découverte directe/LAN/annuaire, favoris/récents/invitations,
  lobby complet, constructeur et autorité de personnage, politiques PvP étendues, UI, MCP,
  transaction, round-trip et tests ciblés fermés.
- [x] `R122` — `FC-MULTI-P2-02` : instances de zones, création dynamique, authentification,
  administration distante et modération fermées avec UI scrollable, transaction, MCP et tests.
- [x] `R123` — `FC-MULTI-P2-03` : persistance monde, SQL sécurisé, métriques/santé,
  profiler serveur et reconnexion.
- [x] `R124` — `FC-MULTI-P2-04` : sauvegarde/rollback serveur, capacité et files d’attente,
  UI, transaction, MCP et intégration aux services réseau.
- [x] `R125` — `FC-DM-P2-01` : client GM séparé, Creator, spawn multiple, Chooser et focus objet,
  profil auteur, UI Live Director, MCP et tests ciblés.
- [x] `R126` — `FC-DM-P2-02` : téléportation, destruction, soin, limbo et possession gouvernés,
  reliés aux services autoritaires, UI, MCP et tests.
- [x] `R127` — `FC-DM-P2-03` : impersonation, protection GM, or, XP et portes gouvernés,
  adaptateurs persistants/runtime, UI, MCP et tests.
- [x] `R128` — `FC-DM-P2-04` : triggers, sélection multiple, mouvement/attaque de groupe et
  téléportation joueur gouvernés avec UI, MCP et adaptateurs runtime.
- [x] `R129` — `FC-DM-P2-05` : recherche, scripts, variables, scènes préparées et brouillard GM
  gouvernés avec UI, MCP, persistance et adaptateurs runtime.
- [x] `R130` — `FC-DM-P2-06` : gestion live des quêtes et rencontres gouvernée avec UI, MCP,
  conséquences RPG et adaptateurs spawn/IA.
- [x] `R131` — `FC-BUILD-P2-01` : bundles reproductibles Windows/Linux/macOS et promotions
  Development/Preview/Stable conditionnées par la matrice de compatibilité et le rollback.
- [x] `R132` — `FC-CONTENT-P2-01` : fournisseur VFX budgété, rotation de confiance signée et
  transport delta reprenable avec retries et contrôle SHA-256.
- [x] `R135` — `FC-AREA-P2-01` : terrain multi-provider, features tessellées, vent relié aux
  matériaux, météo distribuée et transitions de ciel exécutables.
- [x] `R136` — `FC-AREA-P2-02` : eau plan/rivière/volume avec shader, courant,
  flottabilité, navigation, nage, audio et budgets qualité.
- [x] `R137` — `FC-AREA-P2-03` : navigation volumétrique vol/nage/espace avec A*, coûts,
  steering acteur, foule et évitement dynamique.
- [x] `R138` — `FC-PALETTE-P2-01` : collections nommées et palettes de tâche persistantes,
  métriques GLB/PNG automatiques et validation artistique alimentée par les mesures.
- [x] `R139` — `FC-AUDIO-P2-01` : réverbération convolutive par fournisseur runtime,
  fallback algorithmique et sends ordonnés par source.
- [x] `R140` — `FC-WAYPOINT-P2-01` : suivi runtime des routes aériennes, vitesse,
  orientation, inclinaison animée, obstacles et boucles.
- [x] `R142` — `FC-GAMEPLAY-P2-02` : météo autoritaire répliquée par révision et
  dispatch ForgeScript typé avec effets de gameplay.
- [x] `R143` — `FC-AI-P2-01` : simulation déterministe de combat comparant archétypes,
  taux de victoire, durée, ressources, pathing et phases, avec UI et MCP gouvernés.

- [x] `R120` — `FC-AI-P1-03` : agent de test, simulations dialogue/quête, cohérence narrative et
  prompts versionnés fermés avec profil borné, UI inspectée, MCP sans shell, préflight build et
  portail intégré vert à 1 359 tests .NET.
- [x] `R051` — `FC-PROJECT-P1-01` : cinq familles d'événements projet configurables fermées avec
  UI inspectée, transaction, persistance, ForgeScript, MCP, pré-build, smokes et portail intégré vert
  à 1 366 tests .NET.
- [x] `R052` — `FC-PROJECT-P1-02` : événements utilisateur globaux, horloge/calendrier et huit pages
  joueur localisées fermés avec UI inspectée, persistance, ForgeScript, MCP, sauvegarde 1.4,
  pré-build, smokes et portail intégré vert à 1 374 tests .NET.
- [x] `R053` — `FC-AREA-P1-01` : groupes paramétriques et éditables, outils de tuile, auto-fit
  déterministe, propriétés spécialisées et redimensionnement avec repositionnement fermés avec UI
  inspectée, persistance, ForgeScript, MCP, pré-build, smokes et portail vert à 1 382 tests .NET.
- [x] `R054` — `FC-AREA-P1-02` : copie de zone, intérieur/extérieur, jour/nuit, éclairage et brouillard
  fermés avec UI inspectée, transaction, persistance, ForgeScript, MCP, pré-build, smokes et portail
  intégré vert à 1 390 tests .NET.
- [x] `R055` — `FC-AREA-P1-03` : audio de zone, événement utilisateur, carte et brouillard de guerre,
  occlusion/visibilité et LOD fermés avec UI inspectée, transaction, persistance, ForgeScript, MCP,
  pré-build, smokes et portail intégré vert à 1 399 tests .NET.
- [x] `R056` — `FC-AREA-P1-04` : couches et groupes d’édition fermés avec arbre imbriqué et
  glisser-déposer, sélection viewport, export partiel, UI inspectée, persistance, ForgeScript, MCP,
  pré-build, smokes et portail intégré vert à 1 408 tests .NET.
- [x] `R057` — `FC-PALETTE-P1-01` : mise à jour blueprint/instances, catégories hiérarchiques,
  miniatures/prévisualisation et recherche avancée d’assets fermées avec UI inspectée, turntable
  OBJ texturé, transaction, persistance, ForgeScript, CLI, MCP, pré-build, smokes et portail intégré
  vert à 1 415 tests .NET.
- [x] `R058` — `FC-PALETTE-P1-02` : variantes de prefab, compositions/collections et budget d’asset
  fermés avec UI inspectée, transaction, persistance, ForgeScript, MCP, pré-build, smokes et portail
  intégré vert à 1 420 tests .NET.
- [x] `R059` — `FC-SHELL-P1-01` : filtres par calques et volumes, pinceau continu aligné sur
  surface, édition multi-zones à deux viewports avec copie protégée, aide locale sourcée et sept
  profils métier fermés avec UI inspectée, persistance, MCP, pré-build, smokes et portail intégré
  vert à 1 425 tests .NET.
- [x] `R060` — `FC-SHELL-P1-02` : autosave versionné, détection d’une session interrompue,
  snapshots hashés et confinés, comparaison obligatoire et restauration sélective fermés avec UI
  inspectée, commande atomique, undo/redo, persistance, MCP, smokes et portail vert à 1 429 tests .NET.
- [x] `R061` — `FC-AUDIO-P1-01` : recherche et aperçu gouvernés, quatre modes de sélection,
  modulation avec courbes/tempo/statistiques, mix LUFS et ambiance 2D personnalisée fermés avec UI
  inspectée, commande, undo/redo, persistance, ForgeScript, MCP, runtime et portail vert à
  1 435 tests .NET.
- [x] `R062` — `FC-AUDIO-P1-02` : portée avec sphères 3D, contrôle script typé, ambiance adaptative,
  musique synchronisée et monitoring des bus fermés avec UI inspectée, commande, undo/redo,
  persistance, ForgeScript, MCP, runtime et portail intégré vert à 1 440 tests .NET.
- [x] `R063` — `FC-AUDIO-P1-03` : sous-titres dialogue/effet/ambiance localisés avec repli,
  importance, durée et direction fermés avec gouvernance, UI inspectée, commande, undo/redo,
  persistance, ForgeScript, MCP, HUD runtime et portail intégré vert à 1 444 tests .NET.
- [x] `R064` — `FC-WAYPOINT-P1-01` : apparence d'éditeur, map note, activation de map note, points
  de patrouille ordonnés et patrouille forward/reverse fermés avec profil canonique, validation,
  UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP, HUD, acteur runtime et portail
  intégré vert à 1 452 tests .NET.
- [x] `R065` — `FC-WAYPOINT-P1-02` : orientation au waypoint, ancres audio/VFX, groupes et requêtes,
  visualisation des routes fermés avec profil canonique, validation, UI inspectée, commande,
  undo/redo, persistance, ForgeScript, MCP, nœuds runtime, overlay moteur et portail intégré vert
  à 1 461 tests .NET.
- [x] `R066` — `FC-GAMEPLAY-P1-01` : vol libre, projectiles, physique 3D, ciblage et temps de monde
  fermés avec profil canonique, validation, UI graphique inspectée, commande, undo/redo,
  persistance, ForgeScript, MCP, nœuds moteur et portail intégré vert à 1 469 tests .NET.
- [x] `R067` — `FC-GAMEPLAY-P1-02` : sauvegarde par joueur/partie/serveur, checkpoints et respawn,
  carte/journal/HUD, verrous cinématique/dialogue et replay déterministe fermés avec profil canonique,
  validation, UI graphique inspectée, commande, undo/redo, persistance, ForgeScript, MCP, nœuds
  moteur et portail intégré vert à 1 477 tests .NET.
- [x] `R068` — `FC-DIALOGUE-P1-01` : tokens typés, participants multiples, cinq modes d'évaluation,
  wizard de scripts sourcé et commentaires de nœud fermés avec profil canonique, validation, UI
  inspectée, commande, undo/redo, persistance, ForgeScript, MCP, nœuds moteur et portail intégré
  vert à 1 487 tests .NET.
- [x] `R069` — `FC-DIALOGUE-P1-02` : scripts fin/abandon, recherche/remplacement preview-only,
  six badges et mode review, mémoire typée et checks de compétence fermés avec profil canonique,
  validation, UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP et nœuds moteur ;
  portail intégré vert à 1 494 tests .NET.
- [x] `R070` — `FC-DIALOGUE-P1-03` : graphes imbriqués versionnés, relations et conséquences typées,
  couverture de branches, édition collaborative et génération IA contrôlée fermés avec profil
  canonique, validation, UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP et nœuds
  moteur ; portail intégré vert à 1 501 tests .NET.
- [x] `R071` — `FC-QUEST-P1-01` : huit objectifs explicites, récompenses atomiques, commentaires
  auteur et reviews, Plot Wizard transparent et cast du scénario fermés avec profil canonique,
  validation, UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP et nœuds moteur ;
  portail intégré vert à 1 508 tests .NET.
- [x] `R072` — `FC-QUEST-P1-02` : props du scénario, étapes, visibilité cachée/révélée, chronologie et
  pins de carte fermés avec profil canonique, validation des références, UI inspectée, commande,
  undo/redo, persistance, ForgeScript, MCP et nœuds moteur ; portail intégré vert à 1 516 tests .NET.
- [x] `R073` — `FC-QUEST-P1-03` : tests générés et génération IA de quête fermés avec cinq graphes
  canoniques, runner nominal/échec, plan IA traçable, lint, approbation et transaction, UI inspectée,
  commande, undo/redo, persistance, ForgeScript, MCP et nœuds moteur ; portail intégré vert à
  1 523 tests .NET.
- [x] `R074` — `FC-COMPANION-P1-01` : créature et politique de recrutement, contextes avant/après
  recrutement, transaction atomique et quotas de groupe fermés avec profil canonique, validation,
  UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP et nœuds moteur ; portail intégré
  vert à 1 527 tests .NET.
- [x] `R075` — `FC-COMPANION-P1-02` : propriété et transferts, douze ordres de déplacement/combat,
  autonomie gouvernée et équipement permissionné fermés avec profil canonique, validation, UI
  inspectée, commande, undo/redo, persistance, ForgeScript, MCP et nœuds moteur ; portail intégré
  vert à 1 531 tests .NET.
- [x] `R076` — `FC-COMPANION-P1-03` : six politiques de survie, cohésion navigation/portail/
  téléportation sûre et HUD de groupe accessible fermés avec profil canonique, validation, UI
  inspectée, commande, undo/redo, persistance, ForgeScript, MCP, nœuds moteur et panneau HUD visible ;
  portail intégré vert à 1 535 tests .NET.
- [x] `R077` — `FC-CREATURE-P1-01` : assistant, rôle et menace, espèce et TraitSet, apparence et
  AnimationGraph fermés dans le blueprint canonique avec UI inspectée, commande, undo/redo,
  persistance, ForgeScript, MCP, adaptateur RPG Kit et nœuds du personnage 3D ; portail intégré vert
  à 1 542 tests .NET.
- [x] `R078` — `FC-CREATURE-P1-02` : état initial et pose, attributs, défenses et sauvegardes,
  capacités spéciales et inventaire fermés dans le blueprint canonique avec UI inspectée, commande,
  undo/redo, persistance, ForgeScript, MCP, adaptateur RPG Kit et cinq groupes de nœuds runtime ;
  portail intégré vert à 1 550 tests .NET.
- [x] `R079` — `FC-CREATURE-P1-03` : factions pondérées, perception multi-sens, conversations
  contextuelles, protections narratives et `OnConversation` fermés dans le blueprint canonique avec
  UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP, adaptateur RPG Kit et cinq groupes
  de nœuds runtime ; portail intégré vert à 1 558 tests .NET et 39 tests Python.
- [x] `R080` — `FC-CREATURE-P1-04` : perception détectée/perdue, attaque déclarée/résolue,
  capacité ciblée/impactée, blocage et récupération, contrôleur IA interchangeable et blackboard
  typé fermés dans le blueprint canonique avec UI inspectée, commande, undo/redo, persistance,
  ForgeScript, MCP, adaptateur RPG Kit et cinq groupes de nœuds runtime ; portail intégré vert à
  1 566 tests .NET et 39 tests Python.
- [x] `R081` — `FC-DOOR-P1-01` : verrou et credentials, refus d'ouverture localisé, piège de porte,
  cycle de portail demandé/complété et événements terminaux d'ouverture/fermeture fermés sur le
  profil canonique avec UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP, adaptateur
  RPG Kit et cinq groupes de nœuds runtime ; portail intégré vert à 1 576 tests .NET et 39 tests
  Python.
- [x] `R082` — `FC-DOOR-P1-02` : `DoorLocked` et `DoorUnlocked`, méthode, acteur, credential,
  états et provenance fermés sur le profil canonique avec UI inspectée, commande, undo/redo,
  persistance, ForgeScript, MCP, adaptateur RPG Kit et nœud runtime ; portail intégré vert à
  1 581 tests .NET et 39 tests Python.
- [x] `R083` — `FC-ENCOUNTER-P1-01` : assistant de rencontre, difficulté data-driven, groupes à
  bornes et budgets, sélection par évaluation du groupe et sept politiques d'activation fermés sur
  le profil canonique avec UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP,
  adaptateur RPG Kit et cinq nœuds runtime ; portail intégré vert à 1 591 tests .NET et 39 tests
  Python.
- [x] `R084` — `FC-ENCOUNTER-P1-02` : ancres manuelles transformées, restrictions d'activateur et
  autorité serveur, événements d'entrée/sortie/épuisement et graphe phases/vagues fermés sur le
  profil canonique avec UI inspectée, commande, undo/redo, persistance, ForgeScript, MCP, adaptateur
  RPG Kit et quatre groupes de nœuds runtime ; portail intégré vert à 1 601 tests .NET et 39 tests
  Python.
- [x] `R085` — `FC-ENCOUNTER-P1-03` : prévisualisation statistique N runs, seeds reproductibles,
  estimateurs pilotés par données, variances et percentiles fermés avec profil canonique, UI
  inspectée, commande, undo/redo, persistance, ForgeScript, MCP, adaptateur RPG Kit et nœud runtime ;
  portail intégré vert à 1 608 tests .NET et 39 tests Python.
- [x] `R086` — `FC-FACTION-P1-01` : création, archive et suppression sûre avec migration, matrice
  directionnelle asymétrique, bandes de disposition et réputation personnelle fermées sur le
  catalogue canonique avec UI et heatmap inspectées, commande, undo/redo, persistance, ForgeScript,
  MCP, adaptateur RPG Kit et nœuds runtime ; portail intégré vert à 1 615 tests .NET et 39 tests
  Python.
- [x] `R087` — `FC-FACTION-P1-02` : transactions globales aux cinq scopes, tags sémantiques,
  matrice éditable avec comparaison/simulation/import-export/historique et réaction au joueur
  fermés sur le profil canonique avec UI inspectée, commande, undo/redo, persistance, ForgeScript,
  MCP, services RPG Kit et nœuds runtime ; portail intégré vert à 1 622 tests .NET et 39 tests
  Python.
- [x] `R088` — `FC-ITEM-P1-01` : assistant de création, apparence indépendante, masse/volume/grille,
  piles atomiques répliquées et charges/usages fermés sur le profil canonique avec UI 1440×920
  inspectée, commande, undo/redo, persistance, ForgeScript, MCP, adaptateur RPG Kit et cinq nœuds
  runtime ; portail intégré vert à 1 627 tests .NET et 39 tests Python.
- [x] `R089` — `FC-ITEM-P1-02` : protections d'objet plot, huit destinations de transfert,
  modificateurs data-driven, pouvoir unique et slots d'équipement fermés sur le profil canonique
  avec UI 1440×920 inspectée, commande, undo/redo, persistance, ForgeScript, MCP, services RPG Kit
  et cinq nœuds runtime ; portail intégré vert à 1 632 tests .NET et 39 tests Python.
- [x] `R090` — `FC-ITEM-P1-03` : tables de loot pondérées, inspecteur d'inventaire et neuf événements
  de cycle de vie fermés sur le profil canonique avec UI 1440×920 inspectée, commande, undo/redo,
  persistance, ForgeScript, MCP, services RPG Kit et trois nœuds runtime ; portail intégré vert à
  1 637 tests .NET et 39 tests Python.
- [x] `R091` — `FC-MERCHANT-P1-01` : magasin indépendant, wizard définition/vendeur/dialogue/
  interaction/tests, stock fini/infini, courbes achat/vente et restrictions type/tags/qualité/
  provenance/état/propriétaire/règle fermés sur le profil canonique avec UI 1440×920 inspectée,
  commande, undo/redo, persistance, ForgeScript, MCP, services RPG Kit et cinq nœuds runtime ; portail
  intégré vert à 1 643 tests .NET et 39 tests Python.
- [x] `R092` — `FC-MERCHANT-P1-02` : stock infini non décrémenté, sessions OnOpen/OnClose avec
  client/vendeur/prix/bilan et interface boutique clavier/manette/comparaison/filtres/panier/
  confirmation/accessibilité fermés sur le profil canonique avec UI 1440×920 inspectée, commande,
  undo/redo, persistance, ForgeScript, MCP, services RPG Kit et panneau runtime accessible ; portail
  intégré vert à 1 648 tests .NET et 39 tests Python.
- [x] `R093` — `FC-PLACEABLE-P1-01` : conteneur complet, machine visuelle à six états,
  destructibilité, protection plot auditée et verrouillage fermés sur le profil canonique avec UI
  1540×940 inspectée, commande, undo/redo, persistance, ForgeScript, MCP, services RPG Kit réutilisés
  et panneau runtime accessible ; portail intégré vert à 1 653 tests .NET et 39 tests Python.
- [x] `R094` — `FC-PLACEABLE-P1-02` : piège complet, trésor initial déterministe et persistant,
  OnOpen/OnClose, OnLock/OnUnlock avec veto contrôlé et OnDisturbed transactionnel fermés sur le
  profil canonique avec UI 1540×940 inspectée, commande, undo/redo, persistance, ForgeScript, MCP,
  services RPG Kit réutilisés et panneau runtime accessible ; portail intégré vert à 1 659 tests .NET
  et 39 tests Python.
- [x] `R095` — `FC-PLACEABLE-P1-03` : DamageApplied/EntityDestroyed uniformes,
  sockets/attachements avec snap, capacité, prefab et patch inverse, plus huit verbes contextuels
  fermés sur le profil canonique avec UI 1540×940 inspectée, commande, undo/redo, persistance,
  ForgeScript, MCP, services RPG Kit réutilisés et panneau runtime accessible ; portail intégré vert à
  1 664 tests .NET et 39 tests Python.
- [x] `R096` — `FC-TRIGGER-P1-01` : piège avec détection/désarmement/payload/reset,
  filtre de cible typé, dix volumes de gameplay et visualisation d'occupation bornée fermés sur le
  profil trigger canonique avec UI 1540×940 inspectée, commande, undo/redo, persistance, ForgeScript,
  MCP, service RPG Kit réutilisé et panneau runtime accessible ; portail intégré vert à 1 669 tests
  .NET et 39 tests Python.
- [x] `R097` — `FC-SCRIPT-P1-01` : éditeur avec coloration/complétion/signature/docs/snippets/
  symboles/formatage, Script Wizard typé, actions différées, combat autoritaire et effets périodiques
  sérialisables fermés sur le document source canonique avec UI 1680×1000 inspectée, commande,
  undo/redo, persistance, sandbox native, MCP, services RPG Kit réutilisés et panneau runtime
  accessible ; portail intégré vert à 1 684 tests .NET et 39 tests Python.
- [x] `R098` — `FC-SCRIPT-P1-02` : transactions d'inventaire, transitions de quête, sessions de
  dialogue, adhésion/réputation de faction et perception déterministe fermées sur le service monde
  RPG Kit partagé avec UI ForgeScript 1680×1000 inspectée, historique, sandbox native, MCP,
  adaptateur unique et panneau runtime accessible ; portail intégré vert à 1 694 tests .NET et
  39 tests Python.
- [x] `R099` — `FC-SCRIPT-P1-03` : API audio/temps, événements custom, hot reload sûr et débogueur
  distant permissionné fermés sur le service ForgeScript partagé avec UI 1680×1000 inspectée,
  historique, sandbox native, MCP, adaptateur unique et panneau runtime accessible ; portail intégré
  vert à 1 704 tests .NET et 39 tests Python.
- [x] `R100` — `FC-SCRIPT-P1-04` : tests unitaires déterministes, profilage par handler,
  documentation générée, analyse statique à cinq règles et état script enregistré/migrable fermés
  sur un profil canonique transactionnel avec UI 1680×1000 inspectée, diagnostics cliquables, MCP,
  sandbox nominale/abusive, adaptateur unique et panneau runtime accessible ; portail intégré vert à
  1 708 tests .NET et 39 tests Python.
- [x] `R101` — `FC-SCRIPT-P1-05` : interop éditeur/MCP fermée sur un profil canonique UUID/schéma/
  provenance/API/binding, validation localisée et quotas, sources et bindings transactionnels avec
  preview/apply/reject, Undo/Redo et snapshot exact, UI ForgeScript 1680×1000 inspectée, sandbox
  nominale/abusive récupérable, MCP partagé et adaptateur runtime optionnel/requis ; portail intégré
  vert à 1 715 tests .NET et 39 tests Python.
- [x] `R102` — `FC-MULTI-P1-01` : serveur dédié et autoritaire, groupes complets, réplication par
  composant et interest management fermés sur un profil canonique transactionnel, avec UI
  1500×920 inspectée, permissions/audit, ForgeScript, MCP, adaptateurs runtime/serveur et scénario
  intégré à deux clients ; portail vert à 1 724 tests .NET et 39 tests Python.
- [x] `R103` — `FC-MULTI-P1-02` : synchronisation par manifeste/chunks/cache/reprise/delta/confiance,
  lockfile pré-join avec rapport lisible, six rôles RBAC/ABAC, sept canaux de chat accessibles et
  bus typé prioritaire/tracé/borné fermés sur un profil canonique transactionnel ; UI 1500×920
  inspectée, ForgeScript, MCP et scénario serveur intégrés, portail vert à 1 733 tests .NET et
  39 tests Python.
- [x] `R104` — `FC-MULTI-P1-03` : logs JSON/texte avec confidentialité, rotation et sinks,
  anti-cheat par contenu signé/intents/RNG/anomalies/sauvegardes client non fiables et campagnes
  réseau déterministes multi-clients avec défauts, replay, soak et assertions d'état fermés sur un
  profil canonique transactionnel ; UI 1500×920 inspectée, ForgeScript, MCP et scénario serveur
  intégrés, portail vert à 1 743 tests .NET et 39 tests Python.
- [x] `R105` — `FC-DM-P1-01` : authentification GM avec MFA/expiration/révocation, inspection à
  six sections en lecture seule, roster à huit colonnes avec latence rafraîchie, journal dont les
  hash sont intégralement recalculés et sept canaux GM modérés fermés sur un profil canonique ; UI
  1500×920 inspectée, ForgeScript, MCP et scénario serveur intégrés, portail vert à 1 753 tests .NET
  et 39 tests Python.
- [x] `R106` — `FC-BUILD-P1-01` : import/export en masse, merge assisté de listes et graphes UUID,
  packaging Windows portable/installeur, documentation générée et task manifests fermés après
  audit source ; modes reproductibles, prérequis PowerShell 5.1, installation/désinstallation
  réelle, parité UI/commandes et portail vert à 1 756 tests .NET et 39 tests Python.
- [x] `R108` — `FC-CONTENT-P1-01` : import/export en masse, blueprints transitifs, manifeste
  content-addressed, TTF gouvernées avec replis/shaping/DPI/locales et navigation/collision séparées
  fermés après audit source ; commande, undo/redo, roundtrip, runtime et portail vert à 1 758 tests
  .NET et 39 tests Python.
- [x] `R109` — `FC-CONTENT-P1-02` : animations, tables typées, roundtrip externe avec watcher et
  diff, pont Blender borné et variantes plateforme fermés après audit source ; reimport dry-run,
  UI/commandes, persistance et portail vert à 1 760 tests .NET et 39 tests Python.
- [x] `R110` — `FC-CONTENT-P1-03` : migration de package et désinstallation sûre fermées après
  audit source ; exécution d’étape hôte exacte, dry-run sans écriture, backup, changelog,
  restauration/rollback et refus des dépendants non remplacés, portail vert à 1 763 tests .NET et
  39 tests Python.
- [x] `R114` — `FC-EE-P1-01` : transformations runtime reconfirmées, deux viewports 3D multi-zones,
  PBR metallic-roughness, LUT/HDR et éclairage Forward+ par pixel fermés après audit source ; LUT
  réelle, repli diagnostiqué, persistance et portail vert à 1 763 tests .NET et 39 tests Python.
- [x] `R115` — `FC-EE-P1-02` : budgets de lumières reliés au profileur, LOD/HLOD avec hooks de
  génération déterministes et refus de dépassement fermés ; règles data-driven, NUI et événements
  GUI reconfirmés, portail vert à 1 765 tests .NET et 39 tests Python.
- [x] `R116` — `FC-EE-P1-03` : JSON, snapshots, copie runtime et AOE reconfirmés ; chargement de
  zone fermé avec modes asynchrone/séquentiel, annulation configurable, cache/prélecture/progression
  et gate de temps diagnostiquée ; portail vert à 1 767 tests .NET et 39 tests Python.
- [x] `R117` — `FC-EE-P1-04` : NUI, interpolation et compilateur reconfirmés ; TrueType/scaling
  fermé avec shaping, DPI, échelles éditeur/runtime, reflow, focus clavier/manette et huit cas de
  capture à deux résolutions ; portail vert à 1 767 tests .NET et 39 tests Python. Programme P1
  terminé à 298/298 fonctions et 70/70 lots.

### Lots clos historiques P0

- [x] `R036` — `FC-EE-P0-01` : `ORF-EE-002`, `ORF-EE-004` et `ORF-EE-028` sont fermés après audit
  source, tests ciblés, captures inspectées, benchmark 3D et portail intégré vert.
- [x] `R037` — `FC-COMPANION-P0-01` : `ORF-CMP-024` est fermé après audit source, tests ciblés,
  capture inspectée et smokes Toolset/runtime.
- [x] `R038` — `FC-CREATURE-P0-01` : cinq fonctions fondation créature fermées après audit source,
  tests ciblés, captures inspectées et smokes Toolset/runtime.
- [x] `R039` — `FC-CREATURE-P0-02` : dégâts, mort et validation créature reliés au projet, à
  l'interface, ForgeScript, MCP et au runtime autoritaire.
- [x] `R040` — `FC-DOOR-P0-01` : fondation porte reliée au projet, au studio transactionnel,
  à la persistance et au runtime avec preuve visuelle.
- [x] `R041` — `FC-DOOR-P0-02` : obstacle/lien synchronisés aux animations, visualisation auteur,
  validation pré-build et round-trip prouvés.
- [x] `R042` — `FC-ENCOUNTER-P0-01` : volume 2D/3D, candidats pondérés, activation runtime,
  validation pré-build et round-trip prouvés.
- [x] `R043` — `FC-FACTION-P0-01` : cycles, parents absents, seuils chevauchants et membres
  orphelins prouvés avec diagnostics stables, rejet avant mutation et round-trip.
- [x] `R044` — `FC-ITEM-P0-01` : type immuable/migration, blueprint/instance, identité localisée et
  validation item reliés au projet, à l’UI, au runtime, à ForgeScript et au MCP.
- [x] `R045` — `FC-MERCHANT-P0-01` : transactions atomiques et validation marchand fermées avec
  agrégats, rollback exact, UI, persistance, ForgeScript, MCP et runtime autoritaire.
- [x] `R046` — `FC-PLACEABLE-P0-01` : blueprint composable, décor statique, objet utilisable,
  identité/visuel et transform précis fermés avec UI, overlay, persistance, agents et runtime.
- [x] `R047` — `FC-PLACEABLE-P0-02` : OnUsed typé et validation exhaustive placeable fermés avec
  UI, rejet pré-build, persistance, ForgeScript, MCP et runtime optionnel/requis.
- [x] `R048` — `FC-TRIGGER-P0-01` : volumes 2D/3D, trigger générique, transition et événements
  OnEnter/OnExit reliés au projet, à l'UI, au runtime, à ForgeScript et au MCP.
- [x] `R049` — `FC-TRIGGER-P0-02` : validation exhaustive trigger reliée au document canonique,
  à l'UI, au runtime, à la persistance et aux agents.
- [x] `R050` — `FC-BUILD-P0-04` : notices tierces déterministes, licences inconnues bloquées avant
  build, aperçu UI, CLI/MCP, persistance, historique et validation `.orpmod` prouvés.

La file `R020` à `R050` est déjà découpée ; ne pas créer une tâche regroupant plusieurs lots. Une
fiche détaillée sous `docs/aurora-parity/tasks/` est créée seulement lors de l'activation du lot.

## Règles de progression

- une feature ne passe `Tested` qu'après satisfaction de tous ses critères source ;
- un gate historique, un fichier ou un test générique ne constitue pas seul une preuve ;
- les dépendances cycliques sont réduites au plus petit contrat traversant documenté ;
- chaque lot conserve le prototype 3D jouable et exécute tests ciblés puis validation globale ;
- `STATUS.md`, le tracker et le registre généré sont mis à jour avant le lot suivant ;
- aucun P2 ni lot Asset/Map n'interrompt le programme P1 sans décision explicite.

## Lots différés hors programme

Les 25 lots Asset/Map restants sont `OPTIONAL_POST_DEMO`. Ils sont conservés dans la décision
`docs/planning/ASSET_SCOPE_DECISION_2026-08-07.md` et ne sont pas des tâches ouvertes.

## Historique

`R037` / `FC-COMPANION-P0-01` est clos : `ORF-CMP-024` est `Tested`. Une politique canonique
UUID/schéma/provenance réunit recrutement, propriété, commandes, équipement, mort, contextes de
dialogue et navigation inter-zones. L’onglet Compagnons expose quatre sections et un aperçu de
règles ; la commande refuse les bindings invalides avant build, conserve dry-run/apply et
undo/redo, puis persiste au round-trip. ForgeScript et MCP lisent un handle filtré et le runtime
distingue absence optionnelle sûre et absence requise diagnostiquée. La capture R037 est inspectée ;
les suites complètes Domain/Application/RpgKit.Domain/ModuleFormat/MCP passent à
320/341/144/268/72 et les smokes Toolset/runtime sont verts. Le portail final construit 27 projets
sans vulnérabilité, avertissement ni erreur, exécute 1 306 tests .NET et 39 tests Python, valide le
graphe 6 101/37 471 et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

`R036` / `FC-EE-P0-01` est clos : trois fonctions `Tested`. Le viewport expose quatre poignées
X/Y/Z/XYZ reliées à la commande canonique, la collision et la navigation sont reconstruites après
échelle, et MCP fournit lecture filtrée plus dry-run/apply/revert. Le build matérialise ses fichiers
par tâches parallèles bornées avec ordre stable ; l’adapter Godot, les obstacles, profils, recovery
et benchmarks ferment le pathfinding avancé. Deux captures ont été inspectées. Les suites complètes
Domain/Application/ModuleFormat/EngineAdapter/MCP passent à 320/341/266/20/71 ; le portail global
exécute 1 303 tests .NET et 39 tests Python, valide le graphe 6 090/37 396 et termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R035` / `FC-SHELL-P0-04` est clos : deux fonctions `Tested`. Le build conserve une pipeline unique
pour validation, scripts, assets, dépendances, cache incrémental, packaging reproductible et rapports
UI/CLI/MCP. La console classe et filtre désormais les diagnostics par catégorie, sévérité, texte et
corrélation, puis exporte atomiquement un bundle déterministe avec logs runtime expurgés. Les suites
complètes Application/ModuleFormat/MCP passent à 340/264/70. Le portail global construit 27 projets
sans avertissement, exécute 1 299 tests .NET et 39 tests Python, valide les smokes Toolset/runtime,
le graphe frais et termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

`R034` / `FC-SHELL-P0-03` est clos : cinq fonctions `Tested`. Le clipboard gère multi-sélection,
coupe atomique et copie indépendante ; recherche et références couvrent données, textes, scripts et
assets avec profil persistant ; les chemins auteur se renomment avec UUID stable, migration et
rollback ; Play From Here cible monde, quête ou dialogue et supporte headless. Les suites complètes
Domain/Application/ModuleFormat passent à 320/339/262 et le portail global à 1 296 tests .NET,
39 tests Python, graphe frais et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R033` / `FC-SHELL-P0-02` est clos : cinq fonctions `Tested`. Le viewport supporte sélection
simple/multiple, focus, cadrage, verrouillage et inspection des propriétés communes ; caméra,
placement et gizmos partagent des contrats configurables et atomiques. Les mutations humaines et
assistées alimentent un journal UUID/schéma/provenance persistant relié à l'undo/redo natif. Les
suites complètes à 320/336/258, le smoke Toolset R033 et le portail global passent sur 1 289 tests
.NET, 39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R032` / `FC-SHELL-P0-01` est clos : cinq fonctions `Tested`. Un profil shell versionné pilote la
disposition des panneaux, les six regroupements de l'explorateur, les six modes du viewport et les
filtres contextuels type/kit/style/licence/compatibilité/tags/favoris. La même commande supporte
dry-run, erreurs localisées, sauvegarde, annuler/rétablir et round-trip ; les origines Standard et
Custom restent gouvernées. 37 tests ciblés, les suites complètes à 319/328/256, le smoke Toolset
R032 et le portail global passent sur 1 278 tests .NET, 39 tests Python et
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R031` / `FC-PALETTE-P0-02` est clos : trois fonctions `Tested`. Le drag blueprint porte un contrat
canonique v1 validé pour viewport, graphe, inventaire, propriété et package ; les dépendances locales
et les packages requis sont fermés transitivement sans inclure les packages inutilisés ; source,
auteur, SPDX, permission, hash, modifications et origine IA persistent, alimentent les crédits et
bloquent Shipping si incomplets. Les suites ciblées et complètes Domain/Application/ModuleFormat,
le smoke Toolset R031 et le portail global passent sur 1 267 tests .NET, 39 tests Python et
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R030` / `FC-PALETTE-P0-01` est clos : cinq fonctions `Tested`. Blueprints et instances portent
version, namespace, manifeste, paramètres, dépendances, miniature, budget et contrainte de version ;
les palettes Standard/Projet restent distinguées, gouvernées et exportables. 21 tests ciblés, les
régressions navigation/MCP, les smokes Toolset et le portail global ont passé ; celui-ci termine sur
1 258 tests .NET, 39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R028` / `FC-AREA-P0-01` est clos : les cinq fonctions sont `Tested` individuellement. L'assistant
de zone produit un profil canonique prévisualisable ; le document indépendant porte UUID, schéma,
provenance, volumes et dépendances ; les kits modulaires réunissent grille, sockets, règles, groupes,
pinceaux, rotations, miroirs et variantes effectivement reconstruits dans l'aperçu et le runtime 3D.
Les bindings `area.entered`/`area.exited` sont validés avant build et dispatchés avec leur contexte.
La capture 1280×840 a été inspectée, 28 tests ciblés et les smokes Toolset/runtime ont passé.
Le portail global a ensuite construit 27 projets sans avertissement, exécuté 1 241 tests .NET et
39 tests Python, puis terminé sur `OPENRPG_FORGE_VERIFICATION=PASS`.

`R027` / `FC-QA-P0-01` est clos : les cinq fonctions sont `Tested` individuellement. Une politique
canonique persistée réunit validation de schéma, références typées, graphes, géométrie et navigation ;
les catégories optionnelles restent visibles sans casser les projets legacy et deviennent bloquantes
sur choix explicite. Quality Lab, CLI, MCP et build partagent le même rapport déterministe. La capture
1380×860 a été inspectée, les smokes Toolset/runtime ont passé, puis le portail global a terminé sur
1 230 tests .NET, 39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R026` / `FC-PROJECT-P0-03` est clos : `ORF-PROJ-025` est `Tested` individuellement. Le profil de
compatibilité v1 relie minimum runtime, format projet et migrations automatiques au build canonique ;
le minimum rejoint le manifeste, un module requis trop récent est refusé avant extraction et le mode
optionnel reste sûr. UI/commande/historique, ForgeScript et MCP filtré plan/apply/revert sont prouvés.
La capture 1440×980 a été inspectée, puis le portail global a terminé sur 1 224 tests .NET, 39 tests
Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R025` / `FC-PROJECT-P0-02` est clos : les cinq fonctions sont `Tested` individuellement. Le cycle
de vie est ordonné et explicite par autorité, le blackboard devient strict dès qu’un schéma est
présent tout en conservant un mode legacy borné, et les contrats packages/lock, cache reproductible
et cinq profils à secrets séparés sont prouvés ensemble. La capture 1440×920 a été corrigée puis
inspectée, 39 tests ciblés et les smokes Toolset/runtime ont passé, puis le portail global a terminé
sur 1 218 tests .NET, 39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R024` / `FC-PROJECT-P0-01` est clos : les cinq fonctions sont `Tested` individuellement. Le profil
de fondation v1 réunit template, kits, cible, règles, rendu, réseau, identité stable et départs joueur ;
l’assistant affiche un résumé avant écriture, RPG exige sa zone tandis que Core peut démarrer sans
zone, et les contrats Domain/Application, ForgeScript, MCP, persistance et runtime restent bornés.
La capture 980×900 a été inspectée, 16 tests ciblés et les smokes Toolset/runtime ont passé, puis le
portail global a terminé sur 1 213 tests .NET, 39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R023` / `FC-BUILD-P0-02` est clos : les cinq fonctions sont `Tested` individuellement. Le cache
est explicite, jetable et reconstructible ; la normalisation JSON est transactionnelle ; les
migrations utilisent dry-run, sauvegarde bornée, restauration et idempotence ; les références
avant/inverses et le diff sémantique multi-domaines sont exposés dans l'éditeur, la CLI et MCP. La
capture 1440×980 a été inspectée, 20 tests ciblés ont passé et le portail global a terminé sur
1 203 tests .NET, 39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R022` / `FC-AI-P0-03` est clos : `ORF-AI-023` a été réaudité sans promotion et les trois fonctions
ouvertes sont `Tested`, avec résultats JSON/schema et tool calls stricts, réparation bornée, contenu
projet isolé comme données avec provenance, absence d'escalade cachée et core manuel/build/runtime
indépendant du réseau et des métadonnées IA. La preuve 1600×960 a été inspectée ; les tests ciblés
Domain/Application/ModuleFormat/MCP, la régression worker sans Python libre et les smokes Toolset/
runtime ont été validés, puis le portail global a terminé sur 1 194 tests .NET, 6 tests Python
généraux, 33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R021` / `FC-AI-P0-02` est clos : cinq fonctions `Tested`, cycle agent atomique begin/preview/
commit/rollback, diff sémantique typé et hashé, autorisations cumulativement bornées, audit
persistant expurgé et politiques provider de budget/confidentialité. La preuve 1600×960 a été
inspectée, les tests ciblés Domain/Application/ModuleFormat/MCP et les smokes Toolset/runtime ont
été validés, puis le portail global a terminé sur 1 184 tests .NET, 6 tests Python généraux,
33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R020` / `FC-AI-P0-01` est clos : cinq fonctions `Tested`, contexte structuré borné couvrant dix
surfaces, graphe de références typées avec impact inverse, cinq outils sémantiques, plan de
changement complet et dry-run sans écriture. La preuve 1600×960 a été inspectée, les tests ciblés
Domain/Application/ModuleFormat/MCP et les smokes Toolset/runtime ont été validés, puis le portail
global a terminé sur 1 178 tests .NET, 6 tests Python généraux, 33 tests asset et
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R019` / `FC-QA-P0-02` est clos : validation de licence, tests de sauvegarde et logs structurés
promus individuellement à `Tested`, tests domaine/headless reconfirmés, profil QA canonique et
transactionnel, sauvegarde multi-version résiliente et déterministe, logs typés avec vie privée et
rétention, UI et MCP. La preuve 1380×860 a été inspectée, puis le portail global a terminé sur
1 173 tests .NET, 6 tests Python généraux, 33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R018` / `FC-CONTENT-P0-03` est clos : provenance d’asset, quarantaine d’import et frontière IP
promues individuellement à `Tested`, crédits/NOTICE reconfirmés, registre UUID persistant et
transactionnel, staging confiné avec scanner injectable et approbation humaine, refus Shipping
avant archive des imports en attente ou NWN/CEP propriétaires, ForgeScript/MCP et UI visible. La
preuve 1240×780 a été inspectée, puis le portail global a terminé sur 1 162 tests .NET, 6 tests
Python généraux, 33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R001` / `FC-BUILD-P0-01` est clos : cinq fonctions `Tested`, preuve UI inspectée, CLI/MCP/Toolset,
build déterministe et incrémental validés, puis portail global vert sur 1 005 tests .NET.

`R002` / `FC-BUILD-P0-03` est clos : cinq fonctions `Tested`, gouvernance worktree/CI/fixtures/
SemVer/ADR persistée par la transaction canonique, preuve UI inspectée, CLI/MCP/Toolset et audit
du dépôt validés, puis portail global vert sur 1 010 tests .NET.

`R003` / `FC-QA-P0-03` est clos : quatre fonctions `Tested`, diagnostics configurables, catalogue
de sept fixtures hostiles, couverture des 717 IDs et gates G0–G13 persistés par la transaction
canonique, preuve UI inspectée et audit CLI intégré, puis portail global vert sur 1 015 tests .NET.

`R004` / `FC-AUDIO-P0-01` est clos : cinq fonctions `Tested`, objets et clips sonores canoniques,
boucles et position 3D, validation avant export, édition transactionnelle, ForgeScript/MCP et
runtime `AudioStreamPlayer3D`, preuve UI inspectée, puis portail global vert sur 1 026 tests .NET.

`R005` / `FC-DIALOGUE-P0-01` est clos : cinq fonctions `Tested`, document de graphe v1, rôles
explicites, branches paginées sans plafond de domaine, références sûres et ordre stable avec
glisser-déposer, ForgeScript/MCP/runtime et roundtrip exact. La preuve UI 1440×950 a été inspectée,
puis le portail global a terminé sur 1 034 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R006` / `FC-DIALOGUE-P0-02` est clos : cinq fonctions `Tested`, variantes localisées
langue/genre/pluriel, conditions `ALL/ANY/NOT`, actions typées transactionnelles, simulateur sandbox
avec journal et linter structurel complet. La preuve UI 1440×950 a été inspectée, puis le portail
global a terminé sur 1 045 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R007` / `FC-QUEST-P0-01` est clos : cinq fonctions `Tested`, métadonnées de journal localisées,
états UUID ordonnés avec migration numérique, instances joueur/groupe/monde, fins typées et
réactivation explicite, résumé/détails/objectifs/liens contextuels, ForgeScript/MCP/runtime et
roundtrip exact. La preuve UI 1450×910 a été inspectée, puis le portail global a terminé sur
1 059 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R008` / `FC-QUEST-P0-02` est clos : trois fonctions `Tested`, transitions versionnées avec
événement, priorité, idempotence et audit, scénario sandbox persistant avec trace/effets/deadlocks,
linter narratif pour cycles, conflits, objectifs impossibles et récompenses répétables, ForgeScript,
MCP et atelier transactionnel. La preuve UI 1450×910 a été inspectée, puis le portail global a
terminé sur 1 067 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R009` / `FC-WAYPOINT-P0-01` est clos : quatre fonctions `Tested`, ancres nommées localisées,
points de départ prioritaires avec clearance et repli, destinations inter-zones avec politiques
d'orientation/vélocité/préchargement, validation projet, ForgeScript/MCP, migration et roundtrip.
La preuve UI 1240×790 a été inspectée, puis le portail global a terminé sur 1 075 tests .NET et
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R010` / `FC-GAMEPLAY-P0-01` est clos : cinq fonctions `Tested`, profil canonique de déplacement
au sol, interactions contextuelles, huit caméras configurables, actions/bindings multi-device et
sauvegarde versionnée par chunks/checksum/autosave/rollback. La preuve UI 1260×820 a été inspectée,
les lectures ForgeScript/MCP, la commande dry-run/apply/undo/redo et le runtime ont été validés,
puis le portail global a terminé sur 1 085 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R011` / `FC-GAMEPLAY-P0-02` est clos : deux fonctions `Tested`, blackboard typé avec réplication,
debug et handles sûrs, plus catalogue de plugins de règles enregistrant attributs, effets, capacités,
inventaires, progression et UI. Les deux preuves UI ont été inspectées, les plans MCP et le runtime
optionnel/requis validés, puis le portail global a terminé sur 1 093 tests .NET et
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R012` / `FC-SCRIPT-P0-01` est clos : cinq fonctions `Tested`, source `.as` canonique, bytecode
jetable, compilation incrémentale parallèle, événements multi-handlers et entrées typées. L'éditeur
ForgeScript, les transactions/MCP et la capture 1440×880 ont été validés, puis le portail global a
terminé sur 1 101 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R013` / `FC-SCRIPT-P0-02` est clos : cinq fonctions `Tested`, API zones/objets et mouvement par
handles opaques, intents appliqués aux zones canoniques, vecteurs/quaternions, chaînes/regex bornées,
variables locales typées et allowlist sans fichier/réseau/processus/moteur/.NET. La capture 1440×880,
les smokes Toolset/runtime et les transactions ont été validés, puis le portail pré-promotion a
terminé sur 1 114 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R014` / `FC-SCRIPT-P0-03` est clos : cinq fonctions `Tested`, handles opaques typés et
générationnels, budgets d'instructions/mémoire/temps, annulation, reprise saine, RNG et horloge
déterministes, plus négociation SemVer. La capture 1440×880 a été inspectée et le portail global
a terminé sur 1 120 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R015` / `FC-SCRIPT-P0-04` est clos : `ORF-SCR-039` est `Tested` avec manifeste de permissions
canonique par module/kit/événement, refus par défaut, autorité serveur, confiance de signature,
packaging validé avant build, MCP documenté et atelier transactionnel. La capture 1280×760 a été
inspectée sans rognage et le portail global a terminé sur 1 127 tests .NET et
`OPENRPG_FORGE_VERIFICATION=PASS`.

`R016` / `FC-CONTENT-P0-01` est clos : cinq fonctions `Tested`, catalogue de packages versionnés,
priorités et ressources scellées, dépendances required/optional/peer résolues hors ligne, sources
textuelles gouvernées, lockfile et compilation reproductible par `forge pack`. La capture 1420×900
a été inspectée, les transactions ForgeScript/MCP et le runtime ont été validés, puis le portail
global a terminé sur 1 141 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

`R017` / `FC-CONTENT-P0-02` est clos : cinq fonctions `Tested`, catalogue localisé avec ICU et
échange traducteur atomique, profils textures/modèles gouvernés, identités `package:type/path`
adossées aux UUID avec aliases/références, et navigateur unifié projet/localisation/Vault. La preuve
UI 1240×780 a été inspectée, les handles ForgeScript et plans MCP ont été validés, puis le portail
global a terminé sur 1 151 tests .NET et `OPENRPG_FORGE_VERIFICATION=PASS`.

Les lots MVP, G0 à G13 et AV1 à AV7 déjà livrés sont archivés dans
`docs/history/TASKS_DELIVERY_JOURNAL_2026-08-07.md`. Cet historique ne constitue plus une file active.
