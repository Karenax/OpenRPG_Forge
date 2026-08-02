# G7-03 — Catalogue Blueprints Forge

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-PAL-001` — blueprint réutilisable ;
- `ORF-PAL-002` — instance de blueprint et overrides ;
- `ORF-PAL-003` — création depuis une instance ;
- `ORF-PAL-007` — palette du projet ;
- `ORF-BUILD-010` — références inversées.

## Résultat livré

Le workspace Blueprints ouvre un catalogue Forge dédié, cohérent avec la coque Creator. La colonne
de gauche recherche les définitions du projet par nom, slug ou type. La colonne centrale crée et
édite une définition typée, ou la dérive de l’instance Monde sélectionnée. La colonne de droite
reconstruit les usages de cette définition dans les instances et les locuteurs de dialogues.

Les créations et modifications passent par les commandes applicatives `upsert_blueprint` et
`create_blueprint_from_entity`. Elles contrôlent la révision et les slugs, produisent patch, inverse
et audit, acceptent le dry-run et utilisent l’historique commun. La dérivation clone la définition,
retargete uniquement l’instance choisie et conserve sa transform, ses tags et ses overrides.

`BlueprintReferenceIndex` est un read-model moteur-indépendant et reconstructible depuis le projet
canonique. Le catalogue publie une `CreatorSelection` de type Blueprint ; l’inspecteur partagé reste
donc synchronisé avec les autres workspaces.

## Preuves

- `tests/Forge.Application.Tests/Blueprints/BlueprintCatalogTests.cs` couvre création, édition,
  validation, dry-run, undo/redo, dérivation, conservation des overrides et reconstruction des
  références ;
- `OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_PASS catalog=10 derived=1 references=1 undo=restored`
  prouve le workflow dans l’éditeur headless puis restaure le projet d’exemple ;
- la solution compile sans avertissement et les 143 tests .NET ciblés sont verts ; les 22 tests
  Python du dépôt restent verts.

## Limites conservées

- la définition ne possède pas encore de version, plage de compatibilité, paramètres exposés,
  dépendances, miniature ou métadonnées de package ;
- la provenance, la licence et les namespaces seront reliés au chantier Asset Vault et au format de
  contenu, sans inventer de preuve dans ce lot ;
- l’index couvre les instances et locuteurs mais pas encore tous les agrégats, ni cache incrémental,
  renommage sûr, CLI ou MCP ;
- les blueprints restent sérialisés dans `project.json` au lieu de documents séparés Git-friendly ;
- la capture visuelle automatisée reste à rejouer : le service Windows n’a pas pu rattacher l’unique
  fenêtre `OpenRPG_Forge`, alors que le smoke test headless du workflow est vert.

Ces limites maintiennent les cinq capacités au statut `Implemented` partiel, et non `Tested`.
