# Roadmap de parité Aurora

La roadmap complète le vertical slice existant. Elle ne le remplace pas.

## Epic A — Fondation du Toolset

Domaines : SHELL, PROJECT, AREA, PALETTE, BUILD, QA.  
Sortie : création d’un projet, zone modulaire, instances de blueprints, sauvegarde JSON, validation, build et Play From Here.

## Epic B — ForgeScript

Domaine : SCRIPT.  
Sortie : compilation, événements de base, API entités/mouvement/variables, sandbox, quotas, tests et hot reload minimal.

## Epic C — Narration

Domaines : DIALOGUE, QUEST, WAYPOINT, AUDIO.  
Sortie : dialogue conditionnel, quête à états, map marker, voix/sous-titres et simulation headless.

## Epic D — Monde interactif RPG

Domaines : CREATURE, FACTION, ITEM, PLACEABLE, DOOR, TRIGGER, ENCOUNTER.  
Sortie : créatures, inventaire, combat minimal, pièges, transitions, rencontres et butin.

## Epic E — Société et groupe

Domaines : MERCHANT, COMPANION.  
Sortie : magasin transactionnel, économie minimale, recrutement, commandes et équipement de compagnon.

## Epic F — IA transactionnelle

Domaine : AI.  
Sortie : ressources MCP, lecture, plan, dry-run, ajout d’entité, dialogue et script compilé, undo et audit.

## Epic G — Network Kit

Domaine : MULTI.  
Sortie : serveur headless autoritaire, connexion, groupe, réplication de la tranche verticale, content fingerprint et reconnexion.

## Epic H — Live Director

Domaine : DM.  
Sortie : connexion GM, chooser, creator, spawn, inspect, teleport, possession et audit.

## Epic I — Contenu et EE moderne

Domaines : CONTENT, EE.  
Sortie : package repository, sync, PBR/LOD, UI déclarative, serialization, profiling et outils d’assets.

## Règle de passage

Un epic se ferme lorsque toutes ses features P0 sont Tested, ses P1 nécessaires au scénario sont Implemented/Tested, et aucun risque critique sans mitigation ne subsiste.
