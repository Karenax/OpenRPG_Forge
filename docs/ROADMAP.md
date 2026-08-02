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

## Phase 6 — 3D assistée

Blender Worker, validation, LOD/collision, providers abstraits et génération de brouillons.

## Phase 7 — Multijoueur

Runtime 3D headless autoritaire, intentions, réplication, coopération, persistance et outils de maître de jeu.

Chaque phase doit laisser un produit exécutable et testable. La cible de démonstration reste la tranche verticale **Crypte des Cendres** décrite dans `MEGACONTEXT.md` et `TASKS_MVP.md`.
