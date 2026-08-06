# G12-H — Création de boss assistée

## Portée

- exigence : `ORF-AI-019` ;
- composer un boss à partir des domaines canoniques créature, combat, rencontre, dialogue et loot ;
- rendre explicites les capacités, phases, seuils de vie, scripts de transition, invocations et récompenses ;
- simuler l'équilibrage avant toute mutation ;
- appliquer la composition comme un lot atomique partagé entre Toolset et MCP.

## Résultat livré

Le service G12-H produit un paquet typé avec UUID stable, schéma v1, provenance, blueprint de
créature, profil de combat, capacités, rencontre bornée, phases ordonnées, introduction dialoguée,
loot et scripts ForgeScript résolus. La représentation runtime est un document JSON canonique
immuable, borné par schéma et quotas, stocké dans les extensions du module. Elle complète le
blueprint et le dialogue réellement consommés par le projet sans introduire de mutation directe de
la scène moteur.

Chaque proposition exécute 32 simulations déterministes : un seul boss doit être composé, la durée
estimée doit rester dans la fenêtre admise, la récompense doit être positive et la variance doit
être stable. Un script, une invocation, une capacité ou une phase non résolus produisent un code et
un chemin localisés et bloquent l'application.

La fenêtre **Boss assisté** expose identité, statistiques, capacités, phases, scripts, invocations,
récompenses, style, citations, coût, diagnostics, simulation et diff. Le handler lie approbation,
permission, révision, transaction, plan et empreinte du lot. Blueprint, dialogue et orchestration
runtime sont écrits et annulés ensemble. Les quatre outils MCP propose/plan/apply/revert utilisent
le même handler et la même persistance.

## Non-objectifs

- aucune génération de modèle 3D ou d'animation dans ce lot ;
- aucun script absent, non compilé ou inventé silencieusement ;
- aucune mutation directe de nœud Godot ;
- la création de capacité autonome reste le périmètre de G12-I (`ORF-AI-020`).

## Preuves

- 5 tests RPG Kit : composition déterministe, diagnostics localisés, simulation, gouvernance,
  concurrence et undo/redo ;
- test MCP : propose/plan/apply/revert, round-trip disque du blueprint, dialogue et document runtime ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12H_BOSS_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
