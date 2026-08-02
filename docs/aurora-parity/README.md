# Gouvernance de la couverture Aurora

Le dossier de recherche reçu reste immuable dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/`. Le manifeste du dossier couvre 74 fichiers et les
tests vérifient leurs hashes SHA-256.

Les fichiers opérationnels du projet sont :

- `tracking/project/AURORA_CORE_P0_AUDIT.csv` : audit manuel des 76 fondations P0 ;
- `tracking/project/AURORA_PARITY_COVERAGE.csv` : tracker projet généré des 717 capacités ;
- `tracking/project/AURORA_GATE_MATRIX.csv` : affectation feature vers gate et jalon ;
- `PROJECT_GAP_REPORT.md` : synthèse des écarts d’architecture et de produit ;
- `tasks/` : incréments bornés à une à cinq feature IDs.

## Régénération

```powershell
.\scripts\Update-AuroraParityTracker.ps1
.\scripts\Update-AuroraParityTracker.ps1 -Check
```

Le générateur valide le nombre et l’unicité des features, le périmètre des 76 P0, les statuts
autorisés et la présence d’une preuve de test pour chaque ligne `Tested`.

## Règle de statut

`Researched` signifie uniquement que la capacité est documentée dans le snapshot. Une couverture
partielle reçoit un statut conservateur et une note commençant par `Partial:`. Aucun nom de fichier,
lot MVP ou gate global ne suffit seul pour passer une feature à `Tested`.
