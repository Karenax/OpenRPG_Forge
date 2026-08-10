# P0 R044 — fondations d’objet

Lot stable : `R044` / `FC-ITEM-P0-01` — quatre fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-ITEM-002` — type de base immuable.
- `ORF-ITEM-003` — blueprint d’objet.
- `ORF-ITEM-004` — identité et localisation.
- `ORF-ITEM-032` — validation d’objet.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/09_ITEM.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R044 |
|---|---|---|
| type référencé et migration assistée | modèle RPG Kit isolé avec aperçu | profil canonique du blueprint, type versionné, aperçu des incompatibilités et acceptation explicite |
| blueprint et instance séparés | `ItemDefinition`/`ItemInstance` RPG Kit | `ItemFoundationProfile` persistant et état mutable `ItemFoundationInstance` avec allowlist |
| identité localisée complète | textes RPG Kit non reliés au projet | nom, descriptions courte/longue, texte inconnu, label de type et notes dans le document projet |
| validation exhaustive | validateur RPG Kit isolé | identité, type, slots, rig, valeurs, effets, scripts, localisation, asset, licence, hash et économie au pré-build |
| auteur/agents/runtime | profil avancé seulement | écran spécialisé, commande atomique, undo/redo, ForgeScript typé, MCP filtré/dry-run et extension runtime optionnelle/requise |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| type immuable | `ItemTypeDefinition`, `PreviewTypeMigration`, `MigrateType` | `TypeMigrationRequiresPreviewAndRemovesOnlyIncompatibleData` |
| blueprint/instance | `ItemFoundationProfile`, `ItemFoundationInstance` | `CanonicalFoundationValidatesAndSeparatesMutableInstanceState` |
| identité localisée | `ItemFoundationText` | capture inspectée `evidence/r044-item-foundation-ui.png` et round-trip |
| validation complète | `ItemFoundationValidator` et `ProjectValidator` | test Domain invalide et rejet UI/commande avant mutation |
| transaction auteur | `SetItemFoundationHandler`, `ItemFoundationWindow` | smoke Toolset R044, dry-run/apply et undo/redo |
| ForgeScript/MCP | `ItemFoundationScriptService`, ressources/plans MCP | tests MCP filtrés et dry-run sans révision |
| persistance | propriété `BlueprintDefinition.ItemFoundation` | `ItemFoundationPersistenceTests` |

## Non-objectifs

- apparence avancée, piles, charges, équipement, craft, loot ou autres fonctions item P1/P2 ;
- P1/P2, AV7–AV10, commit ou push.

## Validation finale

- tests ciblés Domain, Application, ModuleFormat et MCP verts ;
- capture 1440×920 inspectée : type, gouvernance, résumé et validation lisibles sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R045.
