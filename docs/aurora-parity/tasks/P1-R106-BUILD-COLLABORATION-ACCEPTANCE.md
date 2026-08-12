# R106 — Acceptation build et collaboration

## Périmètre

`R106 / FC-BUILD-P1-01` couvre exactement cinq fonctions P1 :

- `ORF-BUILD-007` — import/export en masse ;
- `ORF-BUILD-012` — merge assisté ;
- `ORF-BUILD-017` — packaging Windows ;
- `ORF-BUILD-020` — documentation générée, déjà `Tested` et maintenue ;
- `ORF-BUILD-021` — task manifests, déjà `Tested` et maintenue.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/25_BUILD.md`, lignes 447–524,
772–849, 1097–1174, 1292–1369 et 1357–1434.

## Matrice d’écarts auditée

| Exigence source | Présent avant R106 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| Batch glob/type/filter, dry-run, conflits, provenance et résumé | configuration canonique, service confiné/atomique, CLI, UI, tests et rollback projet | aucune ; parallélisme et transfert distant ne figurent pas dans la source | tests content, smoke G11E et marqueur R106 |
| Merge trois voies de listes/graphes UUID, conflits et PreserveBoth | listes tabulaires UUID, UI de conflits et variantes déterministes | aucun merge de graphe UUID générique | `UuidGraphThreeWayMerger`, remap d’arêtes et tests déterministes |
| Portable/installeur Windows reproductible, signature optionnelle, prérequis, logs et uninstall | ZIP déterministe, signature, manifeste, scripts install/uninstall | le mode portable produisait aussi l’installeur ; prérequis seulement déclarés | modes honorés, détection dans `install.ps1`, tests et exécution intégrée |
| Six familles de documentation générées depuis métadonnées | générateur, CLI/MCP, mode check et anti-dérive | aucune | statut `Tested` maintenu et `docs check` |
| Manifestes goal/scope/interdits/tests/owner/dépendances | profil UUID/schéma, validation, UI, persistance, CLI/MCP | aucune | statut `Tested` maintenu et smokes readiness |
| Configuration, roundtrip, validation, parité UI/commande, Undo/Redo | commandes Application et fenêtres Content/Distribution/Runtime Extensions | preuve transversale dispersée | marqueur Toolset R106 et fiche consolidée |

## Critères de sortie

- Le batch reste gouverné par `ContentWorkspaceConfiguration` et une commande transactionnelle ;
  les dry-runs/tests ne modifient pas la source.
- Le merge de graphe valide UUID, schéma, provenance et arêtes résolues avec diagnostics localisés ;
  `PreserveBoth` remappe déterministement nœuds et arêtes.
- Le bundle Windows portable n’embarque aucun installateur ; les modes installateur incluent détection,
  log conditionnel, hash post-copie et désinstallation.
- Les interfaces existantes restent la représentation des mêmes commandes Domain/Application et le
  roundtrip projet conserve exactement les cinq configurations.
- Les outils build restent CLI/MCP structurés et n’exécutent aucun shell arbitraire fourni par un agent.

## Preuves obtenues avant portail global

- 2/2 tests ciblés `UuidGraphThreeWayMergerTests` et test ciblé du mode Windows ;
- 455/455 tests Domain et 305/305 tests ModuleFormat ;
- marqueur headless exact `.artifacts/r106-toolset-smoke.log` :
  `OPENRPG_FORGE_TOOLSET_R106_BUILD_COLLABORATION_PASS` ;
- les écrans existants `ContentWorkflowWindow`, `DistributionProfilesWindow` et
  `RuntimeExtensionsWindow` sont chargés par le smoke et appellent les commandes canoniques ;
- `scripts/Verify.ps1` exécute réellement `install.ps1` puis `uninstall.ps1` dans un répertoire isolé.

## Portail consolidé

- tracker/backlog régénérés : 636 `Tested`, 80 `Implemented`, 1 `Researched`, 160 lots clos ;
- documentation générée en mode `check` sans dérive ;
- graphe Roslyn déterministe : 7 653 nœuds, 49 171 relations, zéro diagnostic ;
- 1 756 tests .NET et 39 tests Python passent ;
- le smoke Windows PowerShell 5.1 exécute détection, install, vérification SHA-256 et uninstall ;
- `artifacts/verification/toolset.log` contient le marqueur exact R106 ;
- `.artifacts/r106-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- transfert distant reprenable ou ferme de build distribuée ;
- installeur complet de l’application Toolset elle-même et certificat Authenticode de livraison ;
- lot R107+, P2, Asset/Map AV7–AV10, commit ou push avant portail R106 vert.
