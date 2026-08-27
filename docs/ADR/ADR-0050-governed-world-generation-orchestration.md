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
- Un job complet réussi produit une scène dérivée provider-free et un rapport de validation lié aux
  révisions, empreintes et fournisseurs réellement exécutés. Cette sortie reste non canonique.
- La promotion canonique est une seconde mutation MCP explicite : preview liée à la transaction,
  permission `ApplyApprovedPlan`, provenance d'acceptation, contrôle du SHA-256 de destination,
  copie atomique vers `world/areas/<areaId>/generated-world.tscn`, manifeste de provenance, référence
  depuis `AreaWorldProfile`, sauvegarde/réouverture et inverse fichier/projet.
- Les dépendances facultatives sont installées par un bootstrap : archive Terrain3D vérifiée par
  SHA-256 et fournisseur Graph PCG fixé à un commit immuable. Godot Flow et GF-PCGODOT sont installés
  uniquement dans des fixtures séparées. Aucun binaire tiers volumineux n'entre dans Git.

## Preuves

- Tests : `tests/Forge.Application.Tests/WorldGeneration/WorldGenerationWorkflowTests.cs`
- Tests : `tests/Forge.ModuleFormat.Tests/Workspaces/WorldGenerationStoreTests.cs`
- Tests : `tests/Forge.EngineAdapter.Tests/WorldGeneration/WorldGenerationBridgeTests.cs`
- Tests : `tests/Forge.Mcp.Tests/Projects/WorldGenerationAgentServiceTests.cs`
- Smoke Godot : `tests/Godot/WorldGenerationNativeSmoke.gd`
- Qualification addon : `scripts/Invoke-WorldGenerationAddonQualification.ps1`
- Comparaison provider-neutral : `scripts/Compare-WorldGenerationAddonQualifications.ps1`
- Bootstrap : `scripts/Install-WorldGenerationDependencies.ps1 -WhatIf`

## Conséquences

Le même contrat fonctionne avec les addons spécialisés ou uniquement avec Godot. Les recettes,
plans, jobs, rapports et promotions peuvent être audités indépendamment de la scène moteur active.
`AreaWorldProfile` demeure la source de vérité : le worker n'y écrit jamais et seule la commande de
promotion validée peut ajouter la référence canonique stable.

Terrain3D, Godot Flow et GF-PCGODOT ont franchi la qualification technique locale Windows sous Godot
4.7.1 sur le proxy de référence, mais cette fondation ne qualifie toujours pas un générateur
artistique complet. Les graphes et assets d'un module de production ne sont pas livrés, les étapes
natives autres que le terrain produisent surtout des ancres déterministes, et les gates Linux,
exports, scoring visuel aveugle et effort de correction restent nécessaires avant un choix final du
fournisseur Graph PCG.

## Remplacement

Cette décision ne remplace aucun ADR. Elle applique les frontières de l'ADR-0023 pour le profil de
monde générique et de l'ADR-0049 entre le moteur et le contenu des jeux modules.
