# P1 R057 — Mise à jour, taxonomie et aperçu de palette

## Périmètre

- `ORF-PAL-004` — diff instance/définition, promotion sélective d’overrides, bump SemVer et aperçu
  des instances impactées.
- `ORF-PAL-005` — migration assistée des instances avec contraintes de version, exclusions et
  rollback transactionnel.
- `ORF-PAL-008` — catégories auteur hiérarchiques, multi-tags, dossiers virtuels et facettes.
- `ORF-PAL-010` — miniature déterministe, turntable 3D, variantes, animations, métriques et licence.
- `ORF-PAL-011` — recherche par texte/tags, dimensions, style, squelette, matériau, polycount,
  licence, auteur, compatibilité et variante.
- Dépendances `BUILD` et `CONTENT` : identité, manifeste, provenance, SPDX, hash, chemins et budget
  restent validés avant distribution.
- Un seul lot actif ; P2, AV7–AV10, commit et push restent hors périmètre.

## Audit intégral des critères source

| Critère source | État avant R057 | Fermeture minimale et preuve attendue |
|---|---|---|
| Mise à jour depuis une instance | Partiel | plan de diff, clés promues, bump SemVer et impact par UUID |
| Propagation des instances | Partiel | migration déterministe, pin/range, exclusions, overrides préservés et undo/redo |
| Identité, schéma, manifeste, licence, hash, provenance, budget | Présent | réutiliser le blueprint gouverné et refuser les métadonnées invalides |
| Catégories hiérarchiques | Partiel | chemins personnalisés persistants, catégories virtuelles et facettes multi-tags |
| Miniature et aperçu 3D | Partiel | plan de miniature, angles turntable, variantes/animations, métriques et gouvernance visibles |
| Recherche avancée | Partiel | métadonnées techniques canoniques et filtres UI/Application/MCP |
| Interface sans édition de JSON | Partiel | commandes visibles, état vide/erreur et aperçu d’impact/asset |
| ForgeScript | Partiel | snapshots typés de lecture ; mutation seulement par commande validée |
| MCP | Partiel | ressources filtrées et mutation blueprint en dry-run/apply/revert |
| Persistance et pré-build | Partiel | round-trip exact et validation localisée avant package |
| Runtime optionnel/requis | Partiel | métadonnées absentes ignorées ; données déclarées requises diagnostiquées |

## Non-objectifs

- synchronisation distante d’un dépôt de packs ou téléchargement réseau implicite ;
- rendu photoréaliste ou génération IA de miniature ;
- modification destructive d’overrides non sélectionnés ;
- fonctions P2 de palette et lots Asset/Map optionnels.

## Validations prévues

- tests ciblés Domain/Application/ModuleFormat/MCP ;
- smoke Toolset R057 couvrant aperçu, commande, restauration et recherche ;
- capture UI inspectée avec états vide, sélectionné et résultat filtré ;
- tracker/backlog/docs/graphe frais ;
- `scripts/Verify.ps1` avant R058.

## Résultat de clôture

- Les cinq fonctions du lot sont `Tested` individuellement, sans promotion hors R057.
- Le plan de mise à jour blueprint couvre diff, sélection des overrides, bump SemVer, impact des
  instances, pins, exclusions, conservation des overrides et rollback transactionnel.
- La taxonomie hiérarchique et les métadonnées techniques traversent Domain, Application,
  sérialisation, UI, CLI, ForgeScript, MCP et runtime optionnel/requis.
- Le navigateur charge un OBJ local réel avec UV et texture dans un turntable à huit angles ; la
  génération de miniature reste confinée au workspace et le GLTF optionnel n'est pas chargé pendant
  les smokes historiques qui ne testent que les métadonnées.
- La preuve visuelle inspectée est `docs/aurora-parity/evidence/r057-palette-assets-ui.png` en
  1240×780, avec filtre avancé, sélection, aperçu 2D, turntable 3D texturé et gouvernance visibles.
- Les suites complètes passent à 1 415 tests .NET et 39 tests Python. Le graphe Roslyn est frais et
  déterministe à 6 436 nœuds / 40 098 relations.
- Le portail intégré termine sur `OPENRPG_FORGE_VERIFICATION=PASS` après smokes Toolset/runtime,
  Blender, assets, packaging, réseau, MCP et sauvegarde/reprise.
