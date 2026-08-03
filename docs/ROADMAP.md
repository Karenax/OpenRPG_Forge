# Roadmap

Le plan détaillé de livraison et l’affectation des 717 exigences du dossier de capacités sont dans
[`AURORA_CAPABILITY_DELIVERY_PLAN.md`](AURORA_CAPABILITY_DELIVERY_PLAN.md). Le présent document reste
la vue synthétique du MVP.

## Phase 0 — Fondations

Dépôt, versions, solution C# minimale, tests, extension d’éditeur minimale, CI et ADR. Cette phase est
franchie localement ; l’exécution distante de la CI attend le premier push.

## Phase 1 — Monde minimal

Area/Blueprint/Entity, sérialisation, reconstruction 3D, placement, sauvegarde/chargement et
runtime séparé. Les valeurs de domaine, la sérialisation, la reconstruction runtime et une première
commande transactionnelle sont en place. La session projet et l’aperçu 3D sont fonctionnels ; le
placement, la transformation, la suppression, la grille, l’historique et la sauvegarde sont
fonctionnels. Les instances disposent aussi de propriétés locales typées, versionnées et éditables
sans duplication du blueprint. Le kit Crypte provisoire (sol, murs, porte et piliers), ses conventions et ses
collisions runtime sont en place. Le lancement immédiat depuis un spawn, son snapshot temporaire et
le retour des logs sont fonctionnels ; les interactions directes du viewport restent à enrichir.

## Phase 2 — Gameplay vertical

Joueur, interaction, dialogue, quête, combat simple, objet, pouvoir et sauvegarde. Dialogues, quête,
journal, conditions/actions, combat, capacités, butin et sauvegarde de partie sont data-driven et
validés dans la Crypte.

## Phase 3 — ForgeScript

AngelScript 2.38.0 épinglé, compilation native, API minimale, quotas et script du boss validés.

## Phase 4 — Module

Export `.orpmod`, validation hostile, loader runtime et cache isolé validés.

## Phase 5 — IA/MCP

SDK C# officiel, stdio, pont local authentifié, lecture, plan/dry-run, apply/revert, dialogue, script
et agent de test validés.

## Phase MVP — Démonstration fermée

Zone finale sur primitives originales, export depuis Forge, runtime séparé, sauvegarde/reprise
versionnée, modification IA annulable, rapport de performance, licences et guide créateur sont
livrés. `TASKS_MVP.md` est fermé ; la suite reprend les gates Aurora sans confondre le MVP avec la
parité des 717 capacités.

## Gate Creator Core G7 — en cours

G7-01 a prouvé la création d’un second module générique et G7-02 a livré la coque Creator commune.
G7-03 rend le catalogue Blueprints recherchable et transactionnel, avec dérivation depuis une
instance et références inversées. G7-04 ajoute le glisser-déposer dans le Monde, la validation avant
mutation, le fantôme vert/rouge, la sélection directe et le gizmo Forge X/Z avec undo/redo. G7-05
sépare les zones et leurs transitions. G7-06 ajoute le clipboard sémantique, la duplication avec
nouveaux UUID et la copie complète ou gabarit d’une zone.
G7-07 livre la recherche globale structurée, les références inversées et le renommage sûr avec UUID
stables, prévisualisation et historique partagé.
G7-08 ajoute les métadonnées de publication localisées, les variables typées et scopées, leur écran
Aurora, leur persistance 0.8 et le blackboard runtime validé.
G7-09 sépare les palettes Standard et Custom, verrouille les packs installés et rend provenance,
licence, origine IA, favoris et contenus temporaires filtrables dans l’écran Blueprints Aurora.
G7-10 livre la caméra auteur orbit/pan/zoom, le focus, les vues orthographiques, la vitesse et les
bookmarks persistés hors du document de module.
G7-11 livre les événements Core de chargement projet, entrée de zone, démarrage monde et transition,
avec ordre déterministe, contexte versionné, autorité, diagnostics et observation runtime.
G7-12 livre le cache ForgeScript vérifié, le graphe de dépendances transitif, le plan de rebuild
incrémental et son écran d'analyse dans le workspace Scripts.
G7-13 livre les cinq profils de cible canoniques, le registre de dépendances gouverné, l’écran
Validation & distribution, le manifest ciblé, le NOTICE généré et le blocage des licences ou
provenances inconnues sur les builds distribuables.
G7-14 livre la navigation Core par zone, les profils multi-tailles et vol, les liens hors-maillage,
le rebuild partiel, la reachability depuis les spawns, les contrôles géométriques et l’overlay Aurora.
G7-15 livre le diagnostic canonique v1, sa navigation partagée et des correctifs guidés
prévisualisables qui passent par le bus transactionnel, avec exposition CLI et MCP.
G7-16 livre le catalogue d’assets v1 de 605 entrées, la recherche à facettes, l’aperçu local, la
gouvernance licence/provenance et une planification d’import sans écriture dans l’espace Assets, le
CLI et MCP.
G7-17 livre le catalogue de localisation canonique, l’extraction des textes auteur, les replis par
langue, la pseudo-localisation et l’édition transactionnelle intégrée au Creator.
G7-18 livre l’échelle uniforme/non uniforme, les contrôles précis et viewport, la validation
géométrique et la transaction atomique multi-entités.
G7-19 livre le pathfinding A* déterministe avec coûts, obstacles dynamiques, récupération,
instrumentation et invalidation partielle.
G7-20 livre l'espace de travail créateur avec profils persistants, filtres visuels combinés,
placement répété atomique, zones ouvertes comparables et aide contextuelle sourcée.
G7-21 livre l'autosauvegarde isolée, le journal de récupération vérifié et les instructions joueur
localisées via la configuration transactionnelle du module.
G7-22 livre l'index de bibliothèque Blueprint : catégories hiérarchiques, familles de variantes,
collections et impact exact sur les instances.
G7-23 livre l'analyse de performance par zone : budget d'assets, secteurs d'occlusion et bandes LOD.
G7-24 livre groupes, remplacement, auto-ajustement déterministe, propriétés locales et redimensionnement atomique.
G7-25 livre classification, cycle, éclairage, brouillard et audio adaptatif par zone.
G7-26 livre événements namespacés, minimap/fog multi-étages et calques d'édition imbriqués.
G7-27 livre playlists audio, ordre déterministe, intervalles variables et volume relatif.
G7-28 livre spatialisation 2D/3D, portée, contrôle scripté et ambiance adaptative.
G7-29 livre machine musicale, bus hiérarchiques et sous-titres sonores localisés.
G7-30 livre les primitives déterministes de vol, projectiles, physique, ciblage et temps mondial.
G7-31 livre les portées de sauvegarde, checkpoints, shell runtime, verrous de contrôle et replay déterministe.
G7-32 livre le bus transactionnel des événements joueur, inventaire, capacités, mort et progression.
G7-33 livre les événements globaux à schéma strict et l'horloge mondiale multi-fuseaux astronomique.
G7-34 livre apparences, marqueurs révélables et routes de patrouille déterministes.
G7-35 livre comportements d'arrivée, locators typés, requêtes et diagnostics de routes.

La direction d’interface associée est documentée dans [`UX_AURORA_2026.md`](UX_AURORA_2026.md).
G7 est fermé avec portail global vert et preuves dans `docs/aurora-parity/G7_CLOSURE_EVIDENCE.md`.
L'audit G8 et ses 40 lots sont définis dans `docs/aurora-parity/G8_DELIVERY_PLAN.md`.
La prochaine tranche est **G8 CREATURE-01**, fondation du module La Veillée des Éclats.
G8 CREATURE-01 livre la frontière `Forge.RpgKit.Domain`, les créatures composables, ressources,
déplacements et l'événement de spawn autoritaire. La prochaine tranche est **CREATURE-02**.
G8 CREATURE-02 livre assistant transactionnel, rôle/menace, dégâts, mort et validation structurée.
La prochaine tranche est **CREATURE-03**.
G8 CREATURE-03 livre espèce, visuel, animations, état initial et attributs inspectables.
La prochaine tranche est **CREATURE-04**.
G8 CREATURE-04 livre défenses, capacités, inventaire, factions et perception multisensorielle.
La prochaine tranche est **CREATURE-05**.
G8 CREATURE-05 livre dialogues contextuels, protections, perception différentielle et attaques typées.
La prochaine tranche est **CREATURE-06**.
G8 CREATURE-06 livre capacités ciblées, récupération navigation et quatre contrôleurs IA. La famille
Créature G8 est complète ; la prochaine tranche est **FACTION-01**.
G8 FACTION-01 livre définitions versionnées, matrice asymétrique, dispositions et validation
structurelle. La prochaine tranche est **FACTION-02**.
G8 FACTION-02 livre réputation personnelle et globale, tags, historique heatmap et résolution expliquée
des réactions. La famille Faction G8 est complète ; la prochaine tranche est **ITEM-01**.
G8 ITEM-01 livre blueprints et instances séparés, identité localisée, wizard, migration de type et
validation structurée. La prochaine tranche est **ITEM-02**.
G8 ITEM-02 livre visuels découplés, encombrement, piles transactionnelles, usages et protection des
objets critiques. La prochaine tranche est **ITEM-03**.
G8 ITEM-03 livre transferts par destination, modificateurs, capacités, équipement par rig et loot
déterministe. La prochaine tranche est **ITEM-04**.
G8 ITEM-04 livre commandes d’inspecteur d’inventaire et cycle de vie complet. La famille Item G8 est
complète ; la prochaine tranche est **PLACEABLE-01**.
G8 PLACEABLE-01 livre blueprints composables, décors optimisés, interactions et transformations
précises. La prochaine tranche est **PLACEABLE-02**.
G8 PLACEABLE-02 livre conteneurs, états visuels, dégâts, InteractionExecuted et validation croisée.
La prochaine tranche est **PLACEABLE-03**.
G8 PLACEABLE-03 livre protection narrative, locks, traps, loot persistant et événements de conteneur.
La prochaine tranche est **PLACEABLE-04**.
G8 PLACEABLE-04 livre événements de verrou, contenu et dégâts, sockets composables et verbes
contextuels. La famille Placeable G8 est complète ; la prochaine tranche est **DOOR-01**.
G8 DOOR-01 livre portes sur socket ou libres, machine d’états, interaction, portails et liens atomiques.
La prochaine tranche est **DOOR-02**.
G8 DOOR-02 livre navigation dynamique, verrou partagé, rejets localisés, pièges et validation croisée.
La prochaine tranche est **DOOR-03**.
G8 DOOR-03 livre événements contrôlés de portail, ouverture, fermeture, verrouillage et déverrouillage.
La famille Door G8 est complète ; la prochaine tranche est **TRIGGER-01**.
G8 TRIGGER-01 livre formes 2D/3D, édition, filtres, politiques de tir, portail partagé et événements
d’entrée/sortie. La prochaine tranche est **TRIGGER-02**.
G8 TRIGGER-02 livre pièges, requêtes typées, dix volumes gameplay, debug runtime et validation.
La famille Trigger G8 est complète ; la prochaine tranche est **ENCOUNTER-01**.
G8 ENCOUNTER-01 livre volume d'activation, pool pondéré, difficulté data-driven, assistant en preview
et validation localisée. La prochaine tranche est **ENCOUNTER-02**.
G8 ENCOUNTER-02 livre composition selon le groupe, bornes et budgets, politiques de cycle, ancres
manuelles et filtre d'activation réseau. La prochaine tranche est **ENCOUNTER-03**.
G8 ENCOUNTER-03 livre événements de cycle, phases, vagues et simulation statistique reproductible.
La famille Encounter sélectionnée pour G8 est complète ; la prochaine tranche est **MERCHANT-01**.
G8 MERCHANT-01 livre magasin indépendant, assistant, stock validé et transactions atomiques avec
rollback exact. La prochaine tranche est **MERCHANT-02**.
G8 MERCHANT-02 livre prix explicables, requêtes d'objets, approvisionnement, événements de session
et contrat d'interface accessible. La famille Merchant sélectionnée pour G8 est complète ; la
prochaine tranche est **COMPANION-01**.
G8 COMPANION-01 livre composant canonique, contextes de dialogue, recrutement/renvoi atomiques et
validation localisée. La prochaine tranche est **COMPANION-02**.
G8 COMPANION-02 livre politiques de groupe, ownership audité, ordres de mouvement/combat et profil
d'autonomie. La prochaine tranche est **COMPANION-03**.
G8 COMPANION-03 livre équipement permissionné, politiques de mort, cohésion sûre et HUD accessible.
La famille Companion sélectionnée pour G8 est complète ; la prochaine tranche est **DIALOGUE-01**.
G8 DIALOGUE-01 livre graphe canonique, tokens sûrs, participants multiples, cinq modes d'évaluation,
builders typés et notes éditoriales. La prochaine tranche est **DIALOGUE-02**.
G8 DIALOGUE-02 livre événements de cycle, recherche/remplacement preview, badges review, mémoire
typée et checks data-driven. La prochaine tranche est **DIALOGUE-03**.

## Phase 6 — 3D assistée

Blender Worker, validation, LOD/collision, providers abstraits et génération de brouillons.

## Phase 6A — Asset Vault et Forgotten Forest

Après l’audit de l’existant, construire progressivement une bibliothèque d’assets libre et
traçable : politique CC0 pour le noyau artistique, catalogue local, packs installables à la demande,
acquisition contrôlée, normalisation GLB, optimisation, miniatures, déduplication, crédits et
interface intégrée au toolset. Utiliser ce pipeline pour livrer **Forgotten Forest**, carte extérieure
jouable servant de démonstration publique, benchmark, exemple créateur et test de non-régression.

La mission complète et ses critères sont dans
[`assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md`](assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md) ;
le découpage opérationnel initial est suivi dans `TASKS_MVP.md`. Ce chantier ne remplace pas les
priorités P0/P1 Aurora en cours et doit respecter les ADR, le bus transactionnel et les règles de
licence du dépôt.

## Phase 7 — Multijoueur

Runtime 3D headless autoritaire, intentions, réplication, coopération, persistance et outils de maître de jeu.

Chaque phase doit laisser un produit exécutable et testable. La cible de démonstration reste la tranche verticale **Crypte des Cendres** décrite dans `MEGACONTEXT.md` et `TASKS_MVP.md`.
