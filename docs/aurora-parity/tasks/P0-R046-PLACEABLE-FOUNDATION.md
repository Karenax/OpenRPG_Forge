# P0 R046 — fondations de placeable

Lot stable : `R046` / `FC-PLACEABLE-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PLC-001` — blueprint de placeable composable.
- `ORF-PLC-002` — décor statique optimisé.
- `ORF-PLC-003` — objet utilisable.
- `ORF-PLC-005` — identité et apparence.
- `ORF-PLC-006` — transformation précise.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/10_PLACEABLE.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R046 |
|---|---|---|
| blueprint composable | modèle RPG Kit isolé | extension canonique du profil projet avec visual, collision, interaction, composants, transform et overrides |
| décor statique | batching, instancing, occluder et fusion dans le RPG Kit | rôle exclusif persistant et validation collision/fusion au pré-build |
| objet utilisable | verbes, portée, angle, focus, durée et autorité dans le RPG Kit | bindings UUID, prompt localisé, conditions, événement et runtime typé dans le projet |
| identité et apparence | identité du blueprint cible | UUID/schéma/provenance, slug, textes localisés, asset, meshes, icône, portrait, tags et notes auteur |
| transformation précise | manipulation d’entité et modèle RPG Kit | position/rotation/échelle/pivot/snap/parent/surface/animation indépendants du moteur, persistants et bornés |
| auteur/agents/runtime | onglet Placeables historique et MCP générique | sections spécialisées, aperçu, overlay pivot/snap, handle filtré, dry-run et fallback optionnel/requis |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| document canonique | `PlaceableWorldProfile.FoundationPolicy` | round-trip projet/workspace et tests ModuleFormat |
| statique/utilisable | `StaticPropPolicy`, `InteractionPolicy` | tests RPG Kit et diagnostics `role/static/interaction` |
| identité/visuel | `LocalizedText`, `VisualDefinition` | test pré-build invalide et capture inspectée |
| transform/overlay | `TransformPolicy`, `ResolvePlaceableFoundation` | snap déterministe, overlay invariant et limites de géométrie |
| interface/transaction | onglet Placeables du Studio du monde vivant | capture `evidence/r046-placeable-foundation-ui.png`, smoke dry-run/apply/rejet, undo/redo |
| ForgeScript/MCP | propriétés `foundation`, `role`, `asset`, `requiredAtRuntime` | test MCP filtré et dry-run sans révision |
| runtime | `PlaceableFoundationResolution` | nominal, fallback optionnel et diagnostic requis |

## Non-objectifs

- inventaire avancé, verrou/piège détaillés, destructibilité ou autres fonctions placeable P1/P2 ;
- P1/P2, AV7–AV10, commit ou push.

## Validation finale

- suites Domain, Application, RpgKit.Domain, ModuleFormat et MCP vertes ;
- capture 1540×940 inspectée : identité, asset et aperçu des rôles lisibles sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R046_PLACEABLE_FOUNDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R047.
