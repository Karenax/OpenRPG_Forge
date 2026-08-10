# P0 R030 — blueprints et palettes Standard/Projet

Lot stable : `R030` / `FC-PALETTE-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PAL-001` — Blueprint réutilisable.
- `ORF-PAL-002` — Instance de blueprint.
- `ORF-PAL-003` — Création depuis une instance.
- `ORF-PAL-006` — Palette standard.
- `ORF-PAL-007` — Palette projet.
- Source immuable relue intégralement : sections 50–563 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/04_PALETTE.md`.
- Les fonctions P1 `ORF-PAL-004/005` traversées entre les sections P0 restent explicitement hors lot.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle à fermer |
|---|---|---|
| Blueprint réutilisable | définition canonique typée, UUID, provenance v1, licence/hash, commandes dry-run/apply, undo/redo, sérialisation et export déterministe `.orpblueprints` avec manifeste/licences/migration/aperçu | contrat d’auteur versionné et adressable (namespace, manifeste, paramètres, dépendances, miniature, budget) non attaché à la définition |
| Instance de blueprint | référence UUID, overrides explicites, index de références, runtime et persistance | exigence de version/plage absente de l’instance et non validée contre la définition |
| Création depuis une instance | dérivation atomique et retarget sans perte des overrides | options d’auteur (paramètres exposés, dépendances, miniature, package/version) non persistées |
| Palette standard | provenance `pack://`, licence/hash obligatoires, lecture seule, build graph et remplacement par installation gouvernée | identité de package/version/manifeste non visible comme contrat blueprint unifié |
| Palette projet | origine éditable, recherche, favoris, recents, content workspace, export Git-friendly | namespace et chemin `content/blueprints` non visibles dans l’écran spécialisé |

## Contrat minimal de fermeture

- ajouter un contrat d’auteur v1 au blueprint, rétrocompatible, avec version sémantique, namespace,
  manifeste sûr sous `content/blueprints` ou `pack://`, paramètres exposés, dépendances, miniature et budget ;
- lier chaque instance à une contrainte de version explicite, valider sa résolution et préserver cette
  contrainte lors des mutations d’entité ;
- faire porter à la commande « créer depuis l’instance » les options choisies, sans mutation parallèle ;
- afficher ces données dans l’écran Blueprints et distinguer clairement Standard en lecture seule de Projet ;
- prouver dry-run, apply, undo/redo, round-trip, diagnostics localisés, export gouverné, smoke Toolset,
  puis le portail intégré `scripts/Verify.ps1`.

## Non-objectifs

- propagation/migration P1 des versions (`ORF-PAL-004/005`), R031+, P1/P2, AV7–AV10, commit ou push Git.

## Fermeture prouvée

Les cinq fonctions sont `Tested`. Le contrat `BlueprintAuthoringMetadata` v1 ajoute version sémantique,
namespace, manifeste sûr, paramètres exposés, dépendances, miniature et budget ; l’instance conserve une
contrainte exacte ou une plage et le validateur produit un diagnostic localisé si elle ne résout pas la
version. La création depuis une sélection persiste toutes les options et épingle l’instance sans perdre ses
overrides. Standard reste versionné et en lecture seule via `pack://`; Projet expose son namespace et son
manifeste `content/blueprints` dans l’écran spécialisé et conserve l’export déterministe gouverné.

Preuves ciblées : 7 tests Domain, 11 Application et 3 ModuleFormat, plus les régressions navigation/MCP
découvertes puis corrigées ; build Toolset sans avertissement ; marqueurs G703, G709, G11E et
`OPENRPG_FORGE_TOOLSET_R030_BLUEPRINT_PALETTES_PASS`. Tracker/backlog : 335 `Tested`, 377
`Implemented`, 5 `Researched`, 20 lots P0 ouverts. Graphe : 5 991 nœuds, 36 579 relations, zéro
diagnostic. Portail final : 27 projets sans vulnérabilité, build sans avertissement ni erreur, 1 258 tests
.NET, 6 + 33 tests Python, smokes Toolset/Runtime/MCP, packaging, réseau et Live Director verts, puis
`OPENRPG_FORGE_VERIFICATION=PASS`; le prototype est restauré à l’octet près.
