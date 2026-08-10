# Gouvernance de la couverture Aurora

Le dossier de recherche reçu reste immuable dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/`. Le manifeste du dossier couvre 74 fichiers et les
tests vérifient leurs hashes SHA-256.

Les fichiers opérationnels du projet sont :

- `tracking/project/AURORA_CORE_P0_AUDIT.csv` : audit manuel des 76 fondations P0 ;
- `tracking/project/AURORA_PARITY_COVERAGE.csv` : tracker projet généré des 717 capacités ;
- `tracking/project/AURORA_GATE_MATRIX.csv` : affectation feature vers gate et jalon ;
- `../planning/COMPLETE_FUNCTION_BACKLOG.md` : plan exhaustif généré en lots `FC-*` de une à cinq
  exigences ;
- `../planning/P0_ACCEPTANCE_PROGRAM.md` : protocole séquentiel des 50 lots P0 `R001` à `R050` ;
- `PROJECT_GAP_REPORT.md` : synthèse des écarts d’architecture et de produit ;
- `tasks/` : incréments bornés à une à cinq feature IDs.

## Régénération

```powershell
.\scripts\Update-AuroraParityTracker.ps1
.\scripts\Update-AuroraParityTracker.ps1 -Check
.\scripts\Update-CompleteFunctionBacklog.ps1
.\scripts\Update-CompleteFunctionBacklog.ps1 -Check
```

Le générateur valide le nombre et l’unicité des features, le périmètre des 76 P0, les statuts
autorisés et la présence d’une preuve de test pour chaque ligne `Tested`.

## Règle de statut

`Researched` signifie uniquement que la capacité est documentée dans le snapshot. Une couverture
partielle reçoit un statut conservateur et une note commençant par `Partial:`. Aucun nom de fichier,
lot MVP ou gate global ne suffit seul pour passer une feature à `Tested`.

La fermeture des gates G0 à G13 démontre les tranches verticales du produit ; elle ne signifie pas
que les 717 exigences sont toutes prouvées. La complétude fonctionnelle est atteinte uniquement
lorsque les 717 lignes sont `Tested` et que les 187 lots générés sont `Clos`.

Le programme actif ferme d'abord les 191 P0 non closes. Il autorise l'ensemble de la file, mais
jamais plusieurs lots simultanés ni une promotion de statut sans audit sémantique des critères.
