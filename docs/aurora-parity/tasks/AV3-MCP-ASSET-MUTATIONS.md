# AV3 — Mutations IA/MCP d’assets gouvernés

## Périmètre

Cette tranche expose au MCP l’installation d’un asset déjà catalogué et préparé dans le workspace.
Elle ne télécharge, ne normalise et ne copie aucun fichier, n’écrit jamais directement dans le
projet et ne donne aucune commande générique à l’agent. Le pont éditeur inter-processus reste en
lecture seule.

## Résultat livré

- `PlanAssetInstall` produit un dry-run lié à la révision et conserve diff, inverse et audit ;
- le plan exige un UUID du catalogue, une licence vérifiée, une identité et un SPDX identiques au
  manifeste, ainsi qu’un unique modèle GLB requis ;
- `GovernedAssetModelVerifier` confine le chemin au workspace et vérifie existence, taille et
  SHA-256 du GLB avant de rendre le plan applicable ;
- `ApplyAssetInstallPlan` réutilise `InstallAssetBlueprintHandler`, sauvegarde par `ProjectSession`
  et enregistre le résultat dans `ProjectCommandHistory` ;
- `RevertAssetInstallPlan` emprunte l’inverse de l’historique et refuse une révision plus récente ;
- chaque entrée d’audit conserve plan, action, révision, horodatage, résultat, UUID d’asset et SPDX.

## Preuves ciblées

- `Forge.Application` et `Forge.Toolset` compilent en Release sans erreur ;
- `Forge.McpHost` compile avec les trois outils officiels ;
- 29 tests `Forge.Mcp.Tests` passent ; trois scénarios nouveaux prouvent le parcours complet,
  le refus d’une licence discordante et le refus d’un modèle absent sans changement de révision ;
- gate global validé le 5 août 2026 : 718 tests .NET, 24 tests Python généraux, 22 tests du pipeline
  d’assets, graphe frais, Toolset/runtime réels, Blender 4.5 et
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites explicites

- l’acquisition, la normalisation et la préparation du manifeste restent des workflows manuels ou
  CLI distincts avant l’installation dans le projet ;
- les plans vivent dans la session MCP courante et ne constituent pas une file de travail durable ;
- la permission interactive d’un agent distant reste hors du pont éditeur minimal actuel.

## Critère de passage

Le lot est fermé lorsque formatage, builds/tests ciblés, graphe déterministe et gate global passent
sur le même état Git, puis que le commit est publié sur `main`. La tranche suivante est le benchmark
multi-biome AV4A, avant les finitions de livraison AV5.
