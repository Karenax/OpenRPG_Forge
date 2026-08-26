# ADR-0050 — Orchestration gouvernée de la génération de monde

Date : 26 août 2026
Statut : accepté
UUID : `489cb798-7d3f-4fce-8496-ed2e4446133d`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

La génération d'une grande zone requiert plusieurs moteurs spécialisés sans transformer Terrain3D,
Godot Flow ou un LLM en source de vérité du module. Elle doit rester reproductible, réversible au
niveau de ses recettes, reprenable après redémarrage et compatible avec un poste hors ligne ne
possédant aucun addon facultatif. Les ADR-0023 et ADR-0049 imposent par ailleurs que les données de
monde canoniques restent dans les contrats Forge et que les scènes moteur soient reconstructibles.

## Décision

- `Forge.Domain.WorldGeneration` porte les recettes versionnées, les seeds, les préférences de
  backend et la politique de préservation des retouches humaines, sans dépendance Godot.
- `Forge.Application.WorldGeneration` produit un plan ordonné dont l'empreinte dépend des révisions,
  de la recette et des étapes, mais pas des identifiants aléatoires de plan ou de transaction.
- Le host MCP est le point d'orchestration : lecture, dry-run d'une mutation de recette, application
  gouvernée, preview d'exécution, mise en file et inspection ou annulation des jobs. Il n'expose ni
  shell ni source GDScript arbitraire.
- `Forge.ModuleFormat` persiste atomiquement catalogue et révision de recette, plans et jobs. Les
  chemins sont confinés au workspace et les liens symboliques ou jonctions traversés sont refusés.
- `Forge.EngineAdapter` traduit uniquement le protocole. Terrain3D et Godot Flow restent derrière
  des adaptateurs et ne sont jamais référencés par Domain, Application ou MCP.
- L'addon `openrpg_world_generation` exécute une liste fermée d'opérations. Terrain3D prend en charge
  terrain et hydrologie ; Godot Flow prend en charge biomes, routes, établissements et scatter ; le
  backend Godot natif reste disponible pour toutes les étapes et sert de repli déterministe.
- Une régénération peut sélectionner un sous-ensemble d'étapes. Les nœuds marqués par la métadonnée
  `openrpg_manual_lock` ou déclarés dans `regeneration.protectedNodePaths` sont préservés lorsque
  `preserveManualEdits` est actif.
- Les dépendances facultatives sont installées par un bootstrap : archive Terrain3D vérifiée par
  SHA-256 et dépôt Godot Flow fixé à un commit immuable. Aucun binaire tiers volumineux n'entre dans
  Git.

## Preuves

- Tests : `tests/Forge.Application.Tests/WorldGeneration/WorldGenerationWorkflowTests.cs`
- Tests : `tests/Forge.ModuleFormat.Tests/Workspaces/WorldGenerationStoreTests.cs`
- Tests : `tests/Forge.EngineAdapter.Tests/WorldGeneration/WorldGenerationBridgeTests.cs`
- Tests : `tests/Forge.Mcp.Tests/Projects/WorldGenerationAgentServiceTests.cs`
- Smoke Godot : `tests/Godot/WorldGenerationNativeSmoke.gd`
- Bootstrap : `scripts/Install-WorldGenerationDependencies.ps1 -WhatIf`

## Conséquences

Le même contrat fonctionne avec les addons spécialisés ou uniquement avec Godot. Les recettes,
plans et jobs peuvent être audités indépendamment de la scène. Cette fondation ne qualifie pas encore
un générateur artistique complet : les graphes PCG propres à un module ne sont pas livrés, les étapes
natives autres que le terrain produisent surtout des ancres déterministes, et le worker ne sauvegarde
pas encore automatiquement une scène générée dans le modèle canonique. `AreaWorldProfile` demeure la
source de vérité existante tant qu'une commande explicite de promotion n'a pas été conçue et validée.

## Remplacement

Cette décision ne remplace aucun ADR. Elle applique les frontières de l'ADR-0023 pour le profil de
monde générique et de l'ADR-0049 entre le moteur et le contenu des jeux modules.
