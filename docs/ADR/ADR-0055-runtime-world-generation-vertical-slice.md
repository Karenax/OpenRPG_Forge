# ADR-0055 — Tranche verticale WorldGeneration chargée par le runtime

Date : 28 août 2026
Statut : accepté
UUID : `f720e3fb-2a07-4f3a-8e32-b5a4726ee009`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

L'ADR-0050 avait établi les recettes, plans, jobs, backends et promotions de génération, mais le
résultat restait un proxy de qualification extérieur au produit. Aucun module jouable ne chargeait
encore une scène canonique générée et Studio ne proposait pas de parcours humain pour la piloter.
Une promotion devait aussi rester transportable dans un `.orpmod` sans élargir l'autorisation aux
scènes Godot arbitraires.

## Décision

- Studio expose une page **Génération de monde** qui partage la même session persistante et les
  mêmes révisions que le MCP : recette, preview, job, suivi, promotion et annulation restent gouvernés.
- Le worker Godot est lancé comme processus borné. Il consomme seulement un job allowlisté et rend
  une scène neutre, un rapport et des captures ; il n'exécute aucun script fourni par le module.
- Solaren porte une recette artistique déterministe et une seconde zone canonique. Sa promotion
  contient terrain, quatre familles MultiMesh, collision et navigation, et reste détenue par le
  workspace du module.
- Le runtime résout la promotion à partir de `AreaWorldProfile`, contrôle chemin, manifeste et
  SHA-256, charge `generated-world.tscn` sous la zone active et applique son décalage spatial.
- Le format `.orpmod` autorise uniquement
  `content/world/areas/<area-guid-n>/generated-world.tscn`, si l'area existe, si la ressource est
  requise, et si un manifeste de promotion correspondant lie exactement chemin, zone et SHA-256.
  La scène doit rester provider-free, sans script, chemin externe ni buffer sérialisé interdit.

## Preuves

- Tests : `tests/Forge.ModuleFormat.Tests/Workspaces/WorldGenerationStoreTests.cs`
- Tests : `tests/Forge.ModuleFormat.Tests/Packages/ModulePackageTests.cs`
- Tests : `tests/Forge.Studio.Core.Tests/Navigation/StudioPageCatalogTests.cs`
- Worker : `scripts/Invoke-WorldGenerationWorkspaceWorker.ps1`
- Runtime : `artifacts/world-generation/solaren-vertical-slice/runtime-generated-direct.log`
- Transition : `artifacts/world-generation/solaren-vertical-slice/runtime-area-state.log`
- Package : `artifacts/world-generation/solaren-vertical-slice/runtime-generated-package.log`
- Revue Forward+ :
  `artifacts/world-generation/solaren-vertical-slice/runtime-generated-forward-plus-1920x1080.png`

## Conséquences

WorldGeneration n'est plus seulement un contrat ou un laboratoire : une zone générée est ouverte,
promue, empaquetée et chargée dans un vrai jeu à travers les mêmes données canoniques. La scène
Solaren promue contient 5 904 instances, quatre rôles artistiques, une collision et une navigation ;
son SHA-256 est
`99a2cd3fcb17427c63ea4e09068d256b062516636eef87e37b25ce9d62dde7df`.

Cette preuve qualifie une tranche verticale et son cycle de vie, pas une chaîne artistique universelle.
Le module doit fournir ses graphes, assets et corrections humaines. Les exports Linux et la CI
distante des fournisseurs facultatifs restent à prouver.

## Remplacement

Cette décision complète l'ADR-0050 sans le remplacer.
