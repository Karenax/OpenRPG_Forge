# P0 R037 — validation complète des compagnons

Lot stable : `R037` / `FC-COMPANION-P0-01` — une fonction, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-CMP-024` — Validation compagnon.
- Source immuable relue intégralement : lignes 1724–1791 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/17_COMPANION.md`.
- Dépendances déclarées RPG Kit et QA réévaluées. Les services de recrutement, propriété,
  ordres, équipement et mort existaient dans `Forge.RpgKit.Domain`; la commande monde vivant,
  la validation projet et le Quality gate étaient déjà transactionnels.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R037 |
|---|---|---|
| recrutement, propriété, commandes, équipement, mort et dialogues | services RPG Kit et tests unitaires locaux | politique canonique réunie au profil monde, diagnostics stables et chemins de propriété explicites |
| navigation inter-zones | portails, streaming et runtime de transition | zones autorisées, graphe de portails, timeout et ancre de repli validés ensemble |
| identité et persistance | catalogue monde UUID/schéma/provenance, sérialisation projet/workspace | identité propre de la politique, mode runtime requis/optionnel et round-trip spécifique |
| UI et aperçu | onglet Compagnons limité aux comportements G13-Y/Z | sections recrutement/propriété, commandes/équipement/mort, navigation, aperçu de règles, état de synthèse et retours transactionnels |
| ForgeScript et MCP | handle monde générique, lecture filtrée et dry-run générique | propriétés compagnon typées exposant la politique complète et preuve MCP filtrée sans mutation |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| UUID, schéma, provenance | `CompanionWorldProfile.ValidationPolicy` et validation d’identité | test de round-trip et validation du fixture canonique |
| chemins de recrutement | dialogue/script, binding script, monnaie, coût, réputation bornée | `WorldFeatureCatalogValidator`, fixture Crypte et smoke Toolset |
| propriété et commandes | cinq modes de propriété, autorité, transfert, commandes uniques et sûres | diagnostic `world.companion.ownership_invalid` / `world.companion.commands_invalid` et refus pré-build |
| équipement et mort | inventaire, politique, slots, six modes de mort, récupération | diagnostics stables, persistance et synthèse UI |
| contextes de dialogue | au moins deux contextes uniques, dialogues résolus, bindings sûrs | diagnostic `world.companion.dialogue_contexts_invalid` et fixture canonique |
| navigation inter-zones | zones typées, portails connectés, repli, ancre et timeout | test graphe connecté, test zone inconnue et resolver runtime |
| absence optionnelle/requise | fallback legacy sûr ou diagnostic explicite | `CompanionValidationRuntimeResolver` et marqueur runtime R037 |
| formulaire et aperçu visuel | quatre sections, carte de règles, synthèse et scroll | capture inspectée `evidence/r037-companion-validation-ui.png` |
| commande atomique | même `SetWorldFeatureCatalogHandler`, dry-run/apply et validation avant écriture | smoke Toolset, refus du doublon, undo/redo et round-trip |
| ForgeScript sûr | handle de lecture sans objet moteur ni accès fichier | `WorldFeatureScriptService` et smoke Toolset |
| MCP filtré/dry-run | `QueryWorldFeatures("companion", id, 1)` et `PreviewWorldFeatures` | test MCP dédié, révision inchangée |

## Non-objectifs

- réécrire les systèmes RPG Kit déjà testés ;
- imposer le suivi inter-zones aux modules à une seule zone ;
- ajouter une mutation MCP hors dry-run au contrat demandé ;
- ouvrir R038, P1/P2, AV7–AV10, commit ou push Git.

## Preuves de palier

- tests ciblés : `CryptOfAshesSampleTests`, `WorldFeaturePersistenceTests` et
  `ForgeProjectAgentServiceTests.Companion_validation_resource_is_typed_filtered_and_plan_is_dry_run` ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_R037_COMPANION_PASS` ;
- smoke runtime : `OPENRPG_FORGE_RUNTIME_R037_COMPANION_PASS` ;
- capture 1528×940 inspectée : `docs/aurora-parity/evidence/r037-companion-validation-ui.png` ;
- tracker, backlog, documentation générée, graphe Roslyn et portail intégré sont régénérés avant
  promotion finale de `ORF-CMP-024` vers `Tested`.

## Validation finale

- tracker/backlog : 359 `Tested`, 353 `Implemented`, 5 `Researched`, 157/196 P0 closes et 13 lots
  P0 ouverts `R038` à `R050` ;
- suites complètes : Domain 320, Application 341, RpgKit.Domain 144, ModuleFormat 268 et MCP 72 ;
- graphe Roslyn frais : 6 101 nœuds, 37 471 relations, zéro diagnostic ;
- portail intégré : 27 projets sans vulnérabilité, avertissement ni erreur, 1 306 tests .NET,
  39 tests Python, smokes Toolset/runtime/packages/terrain/MCP/réseau et
  `OPENRPG_FORGE_VERIFICATION=PASS` ;
- une seule promotion, `ORF-CMP-024`; aucun commit, aucun push et aucun lot R038 ouvert.
