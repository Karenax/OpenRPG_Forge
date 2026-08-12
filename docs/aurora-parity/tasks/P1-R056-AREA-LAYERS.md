# P1 R056 — Couches et groupes d’édition

## Périmètre

- `ORF-AREA-035` — couches imbriquées, visibilité, verrouillage, ownership, sélection de groupe et
  export partiel.
- Dépendance `BUILD` : le validateur et le préflight package doivent refuser les références,
  identités, hiérarchies ou métadonnées requises invalides.
- Un seul lot actif ; P2, AV7–AV10, commit et push restent hors périmètre.

## Audit intégral du critère source

| Critère source | État avant R056 | Fermeture minimale et preuve attendue |
|---|---|---|
| Modèle canonique, UUID, schéma, provenance et références | Partiel | enrichir chaque couche, valider identité, hiérarchie, ownership et entités |
| Layers imbriqués, visibilité et verrouillage | Présent mais incomplet | cycles profonds, verrouillage hérité et résolution de visibilité testés |
| Sélection de groupe | Manquant | sélection déterministe d’une couche et de ses descendants vers le viewport |
| Export partiel | Manquant | plan canonique filtrant couches, entités et blueprints sans écriture implicite |
| Interface sans JSON, état vide/erreur, aperçu | Partiel | arbre, réassignation/drag-drop, aperçu d’export et diagnostics visibles |
| Commande, dry-run, diff, undo/redo | Présent | même profil pour UI et commande, patch sémantique enrichi et smoke dédié |
| ForgeScript | Manquant pour les couches | handle typé de lecture/sélection/export ; mutation par commande uniquement |
| MCP | Partiel via l’auteur de zone | lecture filtrée et plan/apply/revert prouvés avec les couches enrichies |
| Persistance et pré-build | Partiel | round-trip exact et rejet localisé des données requises invalides |
| Runtime optionnel/requis | Manquant | absence optionnelle ignorée, absence requise diagnostiquée |

## Fichiers probables

- `src/Forge.Domain/Areas/AreaAuthoringProfile.cs`
- `src/Forge.Domain/Areas/AreaLayerAuthoring.cs`
- `src/Forge.Application/Areas/AreaLayerAuthoringService.cs`
- `src/Forge.Application/Areas/SetAreaAuthoringHandler.cs`
- `src/Forge.Domain/Validation/ProjectValidator.cs`
- `apps/Forge.Toolset/addons/openrpg_forge/AreaAuthoringWindow.cs`
- `apps/Forge.Toolset/addons/openrpg_forge/ForgeEditorPlugin.cs`
- tests Domain/Application/ModuleFormat/MCP et `scripts/Verify.ps1`

## Non-objectifs

- collaboration réseau temps réel et fusion multi-auteur ;
- export d’un module complet ou nouveau format de package ;
- fonctions P2 de zone et lots Asset/Map optionnels.

## Validations prévues

- tests ciblés domaine, application, sérialisation/package et MCP ;
- smoke Toolset R056 avec arbre, sélection, export, transaction et restauration ;
- capture UI inspectée ;
- tracker/backlog/docs/graphe frais ;
- `scripts/Verify.ps1` avant R057.
