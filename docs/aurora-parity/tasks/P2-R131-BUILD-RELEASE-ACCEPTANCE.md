# P2 R131 — Build multi-plateforme et canaux de release

## Critères audités

| Fonction | Preuve de fermeture |
|---|---|
| `ORF-BUILD-016` | La matrice canonique combine cinq cibles et Windows x64, Linux x64, macOS arm64. Chaque plateforme produit un bundle reproductible ; Linux/macOS incluent installation, désinstallation, contrôle plateforme et SHA-256. La configuration reste visible, transactionnelle, persistante et validée avant build. |
| `ORF-BUILD-024` | Les promotions Development → Preview → Stable sont séquentielles et refusées sans adaptateur de publication, signature de confiance pour Stable, ni preuve clean-install/upgrade/rollback pour chaque couple cible/plateforme. |

Tests ciblés : `ModuleDistributionPipelineTests` (7 tests) et build Toolset. Preuve intégrée :
`OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS` avec `platforms=3`, matrice 5×3,
cycle Unix vérifié et promotion gouvernée.

## Non-objectifs

Le service n’embarque aucun secret ni client réseau arbitraire : le transport distant, les clés
de signature et les runners natifs CI sont des adaptateurs de déploiement explicites. P3/P4,
AV7–AV10, commit et push sont exclus.
