# P0 R045 — transactions et validation marchand

Lot stable : `R045` / `FC-MERCHANT-P0-01` — deux fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-MER-012` — transactions atomiques Buy, Sell et Barter.
- `ORF-MER-019` — validation complète d’un marchand.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/16_MERCHANT.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R045 |
|---|---|---|
| transaction atomique | service Buy/Sell/Barter et retour de l’état original en échec | validation agrégée des lignes, forme canonique du type de transaction, monnaie et prix du stock avant toute mutation |
| fonds, propriété, stock, capacité | contrôles isolés par ligne | totaux par monnaie, item et variante ; aucun contournement par lignes dupliquées |
| identité et binding marchand | profil économique du monde vivant | politique UUID/schéma/provenance, interaction et choix de dialogue persistants |
| monnaies, trésorerie, stock et prix | monnaies, comptes et services du profil historique | stock typé vers un blueprint Item, variante, quantité, prix et monnaie vérifiés au pré-build |
| restrictions, localisation et scripts | présents dans le RPG Kit isolé | contrat projet persistant, diagnostics stables et chemins précis |
| auteur/agents/runtime | onglet marchand économique et MCP générique | sections transactionnelles visibles, handle ForgeScript/MCP filtré, dry-run et runtime optionnel/requis autoritaire |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| Buy/Sell/Barter atomiques | `MerchantService.ValidateTrade` et `Execute` | tests nominal, formes invalides, multi-lignes et rollback par identité |
| stock et validation canonique | `MerchantWorldProfile.TransactionPolicy`, `WorldFeatureCatalogValidator` | test R045 des dix diagnostics marchands |
| runtime | `MerchantWorldTransactionService`, `ResolveMerchantTransactions` | handle valide/périmé et fallback optionnel/requis |
| interface | onglet Marchands du `WorldFeatureStudioWindow` | capture inspectée `evidence/r045-merchant-transactions-ui.png` |
| transaction auteur | `SetWorldFeatureCatalogHandler` | smoke dry-run/apply/rejet, undo/redo |
| ForgeScript/MCP | propriétés `transactions`, `stock`, `requiredAtRuntime` | test MCP filtré et dry-run sans changement de révision |
| persistance | sérialisation du catalogue monde | `WorldFeaturePersistenceTests` et smoke project/workspace |

## Non-objectifs

- historique de prix avancé, enchères, boutique distante ou autres fonctions marchand P1/P2 ;
- P1/P2, AV7–AV10, commit ou push.

## Validation finale

- tests ciblés RpgKit.Domain, Application, ModuleFormat et MCP verts ;
- capture 1540×940 inspectée : sections, bindings, stock et aperçu lisibles sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R045_MERCHANT_TRANSACTIONS_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R046.
