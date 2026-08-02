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

La direction d’interface associée est documentée dans [`UX_AURORA_2026.md`](UX_AURORA_2026.md).
La prochaine tranche est **G7-10 — navigation caméra d’auteur**.

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
