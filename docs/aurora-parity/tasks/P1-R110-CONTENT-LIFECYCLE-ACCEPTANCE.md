# R110 — Acceptation du cycle de vie des packages

## Périmètre

`R110 / FC-CONTENT-P1-03` couvre exactement deux fonctions P1 :

- `ORF-CONT-026` — migrations de package ;
- `ORF-CONT-027` — désinstallation sûre.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/22_CONTENT.md`, lignes 1913–2059.

## Matrice d’écarts auditée

| Exigence source | Présent avant R110 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Étape de migration exacte dans du code hôte fiable | planificateur qui refuse une étape absente, non fiable ou sans sauvegarde | l’étape approuvée n’était pas exécutée physiquement | `AssetPackageMigrationStore` vérifie l’identité de l’étape compilée avant exécution |
| Dry-run, sauvegarde, changelog et tests de compatibilité | plan déterministe avec dry-run, liste ordonnée de sauvegarde et changelog | aucune sauvegarde, écriture, restauration ou rollback physique | store confiné, dry-run sans écriture, backup, changelog, restauration explicite et rollback automatique |
| Analyse d’impact, dépendants, remplacement et refus | analyseur canonique, store Vault, CLI plan/apply et tests physiques | aucune limite source réelle | smoke R110 vérifie refus avec dépendant puis autorisation avec remplacement installé |
| Diagnostics et chemins sûrs | codes stables du planificateur et validation de chemins du Vault | une entrée de migration non sûre pouvait lever une exception au lieu d’un diagnostic | refus `asset.package.migration_plan_invalid` sans écriture |

## Contrat fermé

- Une migration ne peut exécuter que l’étape compilée dont l’UUID correspond exactement au plan approuvé.
- Les chemins d’entrée et de sauvegarde restent confinés au workspace ; toute traversée est refusée avec
  un diagnostic stable.
- Le dry-run ne crée ni backup, ni changelog et ne modifie aucun asset.
- L’application sauvegarde chaque entrée avant l’étape, produit un changelog versionné, puis retourne
  l’installation à la nouvelle version.
- Une erreur d’I/O, d’autorisation ou de l’étape hôte restaure automatiquement tous les fichiers.
- La restauration explicite relit le backup gouverné ; la désinstallation reste refusée tant qu’un
  dépendant n’a pas de remplacement installé.

## Preuves obtenues avant portail global

- 3/3 tests `AssetPackageMigrationStoreTests` ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r110-toolset-smoke.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R110_CONTENT_LIFECYCLE_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 647 `Tested`, 69 `Implemented`, 1 `Researched`, 163 lots clos ;
- documentation générée en mode `check` sans dérive ;
- graphe Roslyn déterministe : 7 682 nœuds, 49 352 relations, zéro diagnostic ;
- 1 763 tests .NET et 39 tests Python passent ;
- `artifacts/verification/toolset.log` contient le marqueur exact R110 ;
- `.artifacts/r110-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- scripts de migration fournis par un module ou un agent non fiable ;
- éditeur libre de code de migration dans le Toolset ;
- distribution distante, signatures et deltas de packages P2 ;
- Asset/Map AV7–AV10, commit ou push.
