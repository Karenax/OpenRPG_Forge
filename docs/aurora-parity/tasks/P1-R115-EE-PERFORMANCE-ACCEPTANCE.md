# R115 — Acceptation EE performance et runtime extensible

## Périmètre

`R115 / FC-EE-P1-02` couvre exactement cinq fonctions P1 :

- `ORF-EE-008` — nombre accru de lumières dynamiques ;
- `ORF-EE-010` — LOD ;
- `ORF-EE-011` — règles plus data-driven, déjà `Tested` et reconfirmée ;
- `ORF-EE-012` — NUI scriptable, déjà `Tested` et reconfirmée ;
- `ORF-EE-013` — événements GUI, déjà `Tested` et reconfirmée.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/23_EE.md`, lignes 535–602 et
669–940.

## Matrice d’écarts auditée

| Exigence source | Présent avant R115 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Budgets de lumières data-driven par plateforme, sans cible de parité codée en dur, avec warnings profileur | budgets dynamiques/ombres/atlas, seuils, diagnostics, écran auteur, runtime et profileur intégré | l’ancienne note séparait à tort les warnings du profileur déjà présent | smoke R115 capture le plan et ses métriques via `IntegratedQualityProfiler` |
| Import LOD/HLOD, hooks de génération, seuils, crossfade et validation de performance | niveaux LOD d’assets, tâches de génération, seuils zone, budgets triangles, crossfade, popping et runtime | les paramètres HLOD existaient sans plan de génération de clusters | `AreaHlodGenerationPlanner` produit des hooks déterministes confinés et refuse le dépassement du budget |
| Règles, NUI et événements GUI sans recompilation moteur | profils schématisés, documents UI réactifs, bindings/permissions et commandes d’événement typées | aucune limite source réelle | preuves G11-O maintenues et smoke R115 de non-régression |

## Contrat fermé

- Les budgets de lumières restent par plateforme, configurables et indépendants d’une valeur Aurora
  codée en dur ; avertissements et dépassements alimentent les traces du profileur.
- Le plan HLOD groupe les entités par cellule stable, n’écrit rien en dry-run, émet une opération
  typée `area.hlod.generate` vers `.forge/generated/hlod` et conserve les IDs sources.
- Un nombre de clusters supérieur au budget est refusé par
  `area.hlod.cluster_budget_exceeded` avant toute génération.
- Les ratios LOD, budgets de triangles, distances, crossfade et mesure du popping restent dans les
  profils canoniques, interfaces et validations existantes.
- Les règles data-driven, la NUI et les événements GUI conservent leurs commandes, permissions,
  persistance et replis runtime existants.

## Preuves obtenues avant portail global

- 2/2 tests `AreaHlodGenerationPlannerTests` ;
- 1/1 test `AreaPerformanceAnalyzerTests` ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r115-toolset-smoke-bea931e459ed4ec0b0f228466dc37662.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R115_EE_PERFORMANCE_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 653 `Tested`, 63 `Implemented`, 1 `Researched`, 165 lots clos ;
- documentation générée en mode `check` sans dérive ;
- graphe Roslyn déterministe : 7 691 nœuds, 49 412 relations, zéro diagnostic ;
- 1 765 tests .NET et 39 tests Python passent ;
- `artifacts/verification/toolset.log` contient le marqueur exact R115 ;
- `.artifacts/r115-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- fusion géométrique HLOD dans le domaine ou exécution d’un outil arbitraire ;
- seuil matériel universel de lumières ou benchmark dépendant d’un GPU précis ;
- P2, Asset/Map AV7–AV10, commit ou push.
