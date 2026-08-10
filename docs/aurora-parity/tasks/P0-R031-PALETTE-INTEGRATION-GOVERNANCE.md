# P0 R031 — intégration contextuelle et gouvernance des palettes

Lot stable : `R031` / `FC-PALETTE-P0-02` — trois fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PAL-012` — Drag and drop.
- `ORF-PAL-015` — Dépendances automatiques.
- `ORF-PAL-016` — Provenance et licence.
- Source immuable relue intégralement : sections 864–933, 1086–1155 et 1160–1229 de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/04_PALETTE.md`.
- Les fonctions P1 `ORF-PAL-013/014` traversées entre les sections P0 restent explicitement hors lot.

## Audit avant fermeture

| Fonction | Déjà présent | Limite réelle à fermer |
|---|---|---|
| Drag and drop | payload UUID depuis la palette, réception dans le viewport, aperçu fantôme, grille, collision, diagnostic stable et placement via commande avec undo/redo | aucun contrat canonique commun ne décrit/valide les cibles graphe, inventaire, propriété et package ; le payload ne porte pas le schéma ni la gouvernance nécessaires à une validation contextuelle uniforme |
| Dépendances automatiques | graphe déterministe du workspace, scripts référencés par les overrides, packs installés réellement instanciés ou utilisés comme locuteurs, manifeste trié et build incrémental | les dépendances déclarées par les blueprints ne créent pas d’arêtes locales et les dépendances requises des packages de contenu ne sont pas fermées transitivement |
| Provenance et licence | source, auteur, SPDX, hash, modifications, origine IA, persistance, recherche, inspection et blocage global de distribution pour licence/source/auteur/hash incomplets | preuve de permission explicite absente du contrat canonique, de l’interface et du blocage distribution ; aucun index de crédits blueprint n’est émis par l’export |

## Contrat minimal de fermeture

- ajouter un contrat de drop v1 typé et testable pour `Viewport`, `Graph`, `Inventory`, `Property` et
  `Package`, avec UUID, schéma, provenance, chemin cible, diagnostic stable et plan sémantique ;
- enrichir le payload Toolset avec la gouvernance canonique et faire valider les cibles par le même
  planificateur applicatif avant qu’une commande de la surface cible ne mute le projet ;
- dériver les arêtes locales depuis les dépendances/miniatures/manifeste des blueprints utilisés et fermer
  transitivement les packages de contenu requis, sans inclure les packages inutilisés ;
- ajouter la preuve de permission à `BlueprintProvenance`, l’exposer dans l’écran Blueprints, la contrôler
  avant distribution et agréger des crédits déterministes dans l’export ;
- prouver validation contextuelle, fermeture transitive, minimalité, round-trip, distribution, export,
  smoke Toolset puis le portail intégré `scripts/Verify.ps1`.

## Non-objectifs

- propagation P1, inventaire RPG complet, éditeur de graphe générique P1, R032+, P1/P2, AV7–AV10,
  commit ou push Git.

## Fermeture prouvée

Les trois fonctions sont `Tested`. `BlueprintDropPlanner` fournit un plan v1 sans mutation parallèle
pour viewport, graphe, inventaire, propriété et package ; la vue 3D consomme le même contrat avant sa
commande de placement. Le graphe suit les manifests, miniatures, scripts et assets déclarés par les
blueprints utilisés, ferme les packages requis transitivement et exclut le package inutilisé de la
fixture. L'export ferme aussi les blueprints dépendants et produit un index de crédits déterministe.
La provenance ajoute la preuve de permission, visible et transactionnelle dans le Toolset, conservée
au round-trip et bloquante pour Shipping lorsqu'elle est absente.

Preuves ciblées : 8 tests Application, 9 Domain et 13 ModuleFormat ; suites complètes concernées à
317/319/256 tests ; build des 27 projets sans avertissement ; marqueurs G704, G709, G712 et
`OPENRPG_FORGE_TOOLSET_R031_PALETTE_INTEGRATION_PASS`. Tracker/backlog : 338 `Tested`, 374
`Implemented`, 5 `Researched`, 19 lots P0 ouverts. Portail final : 1 267 tests .NET, 6 + 33 tests
Python, smokes Toolset/Runtime/MCP, packaging, réseau et Live Director verts, puis
`OPENRPG_FORGE_VERIFICATION=PASS` ; le prototype est restauré à l'octet près.
