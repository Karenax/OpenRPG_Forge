# R108 — Acceptation contenu gouverné

## Périmètre

`R108 / FC-CONTENT-P1-01` couvre exactement cinq fonctions P1 :

- `ORF-CONT-004` — import ERF en masse ;
- `ORF-CONT-005` — export de blueprints ;
- `ORF-CONT-008` — manifeste d’assets ;
- `ORF-CONT-012` — police TrueType et UI scalable ;
- `ORF-CONT-015` — walkmeshes et collisions.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/22_CONTENT.md`, lignes 285–356,
359–430, 581–652, 877–948 et 1099–1170.

## Matrice d’écarts auditée

| Exigence source | Présent avant R108 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Batch avec filtres, collisions, provenance, dry-run et rapport | configuration projet, service atomique confiné, CLI, écran et tests | aucune ; la lecture d’un conteneur ERF propriétaire n’est pas exigée par l’adaptation OpenRPG | tests `ContentWorkflowTests` et marqueur R108 |
| Sélection de blueprints, dépendances transitives, licences, migrations et aperçus | archive déterministe et gouvernée | la note tracker ne reflétait pas encore la fermeture transitive déjà implémentée | test de fermeture à deux blueprints et inspection d’archive |
| Manifeste content-addressed, chunks, compression, plateformes et provenance | UUID, schéma, SPDX, SHA-256, budgets, commande, store, runtime et UI | aucune sur le cœur du manifeste | suites Domain/Application/ModuleFormat et roundtrip R108 |
| TTF, replis, shaping, DPI, taille et tests de localisation | familles/replis, échelles UI/texte, application globale et Quality Lab | aucun asset de police gouverné ne portait chunks, shaping, DPI et locales | `AssetFontManifest`, chunks dédiés, validation et roundtrip |
| Navigation/collision séparées, génération/édition, matériaux et cuisson plateforme | collision et plateformes dans le manifeste | navigation absente et collision parfois liée au chunk modèle | `AssetNavigationManifest`, chunks distincts et auteur d’extensions |

## Contrat fermé

- Les nouveaux champs restent dans `AssetPackageManifest` et passent par
  `SetAssetPackageManifestHandler`; aucun état parallèle n’est créé.
- Collision, navigation, police principale et replis utilisent des chunks distincts avec SHA-256,
  compression et variantes plateforme.
- Les diagnostics stables localisent référence absente, navigation invalide et profil de police
  invalide avant sauvegarde ou distribution.
- `AssetPackageManifestStore` normalise et recharge exactement les extensions ; le résolveur runtime
  vérifie la disponibilité et les hashes des chunks requis.
- `AssetContentBrowserWindow` expose navigation et police dans le résumé du manifeste ; les écrans
  Content Workflow et Workspace conservent import/export et échelles UI/texte.

## Preuves obtenues avant portail global

- 6/6 tests ciblés `AssetPackageManifestTests` ;
- 3/3 tests `AssetPackageManifestStoreTests` ;
- 3/3 tests `ContentWorkflowTests` ;
- 439/439 tests Application et 305/305 tests ModuleFormat ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r108-toolset-smoke-28e439d2ccb9453c87efd404a57df5df.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R108_CONTENT_ACCEPTANCE_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 641 `Tested`, 75 `Implemented`, 1 `Researched`, 161 lots clos ;
- documentation générée en mode `check` sans dérive ;
- graphe Roslyn déterministe : 7 663 nœuds, 49 238 relations, zéro diagnostic ;
- 1 758 tests .NET et 39 tests Python passent ;
- `artifacts/verification/toolset.log` contient le marqueur exact R108 ;
- `.artifacts/r108-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- prise en charge directe d’un format ERF propriétaire ;
- génération géométrique navmesh/collision par un nouveau worker ; les artefacts produits ou édités
  manuellement sont gouvernés ici ;
- lots R109+, P2, Asset/Map AV7–AV10, commit ou push avant portail R108 vert.
