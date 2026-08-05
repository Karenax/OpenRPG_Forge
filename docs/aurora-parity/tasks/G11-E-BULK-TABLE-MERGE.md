# G11-E — Contenu en masse, tables et fusion assistée

## Périmètre

- `ORF-CONT-004` — import de ressources en masse ;
- `ORF-CONT-005` — export de blueprints ;
- `ORF-CONT-018` — tables de données typées ;
- `ORF-BUILD-007` — commandes d’import/export en masse ;
- `ORF-BUILD-012` — fusion assistée.

## Résultat livré

- configuration de contenu canonique v1 attachée au module, UUID stable, provenance et politique de
  collision/fusion, persistée par la commande transactionnelle `set_content_workspace` avec dry-run,
  diff, audit et undo/redo ;
- tables de données avec UUID de table, colonne et ligne, colonnes `Text`, `Integer`, `Decimal`,
  `Boolean` ou `Reference`, champs requis, références résolues, overrides de package, licence SPDX,
  provenance et SHA-256 ;
- migration déterministe des projets antérieurs vers une configuration vide et conservation exacte au
  round-trip JSON ;
- service d’import/export en masse confiné : glob, filtres de type, budgets, rejet des reparse points,
  hashes, rapport structuré et stratégies `Fail`, `Skip`, `Replace` ou `PreserveBoth` ;
- dry-run sans écriture, staging vérifié par hash, application fichier par fichier et rollback des ajouts
  ou remplacements en cas d’erreur ;
- pack `.orpblueprints` reproductible avec définitions sélectionnées, sources de packs requises,
  licences, migration de schéma et aperçus textuels ;
- fusion de tables à trois voies par UUID : ajouts, suppressions et modifications indépendants sont
  fusionnés automatiquement ; chaque conflit conserve base, notre variante et la variante distante ;
  `PreserveBoth` crée un UUID dérivé stable au lieu d’écraser une variante ;
- espace visuel **Données** dans le Creator avec paramètres de transfert, sélection de blueprints,
  éditeur de tables, vue des conflits et enregistrement par commande de projet ;
- CLI structurée `content batch`, `content export-blueprints` et `content merge-table`.

## Preuves

- `ContentWorkspaceTests` couvre les types, références, overrides et conflits conservés ;
- `SetContentWorkspaceHandlerTests` couvre dry-run, conflit de révision, application, undo et redo ;
- `ContentWorkflowTests` couvre filtres, absence d’écriture en dry-run, collision conservant les deux
  fichiers et reproductibilité du pack de blueprints ;
- les tests de sérialisation couvrent round-trip canonique et migration depuis `0.6.6` ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11E_CONTENT_PASS` : table typée, commande, batch réel,
  collision, pack de blueprint, fusion conflictuelle, persistance et historique ;
- smoke CLI `OPENRPG_FORGE_G11E_CONTENT_SMOKE_PASS` : prévisualisation, copie, seconde copie
  conservée sous suffixe hashé et inspection des entrées de gouvernance du pack.

## Limites explicites

- OpenRPG Forge ne lit pas le conteneur propriétaire historique mentionné par la recherche : les
  ressources doivent être converties vers JSON, ForgeScript, GLB, PNG, OGG ou un autre format ouvert
  autorisé avant import ;
- les dépendances transitives de blueprints couvrent actuellement les sources de packs installés ; le
  futur graphe de ressources généralisé ajoutera les références d’assets et de tables ;
- les changements concurrents de schéma de table sont refusés : une migration explicite doit aligner
  les colonnes avant la fusion des lignes ;
- les formules, index secondaires, éditeurs spécialisés par kit et l’accès ForgeScript typé restent aux
  lots data-driven G11-O/G11-P ;
- les transferts distants, parallèles et reprenables ne sont pas simulés par ce lot local.

## Critère de passage

G11-E est fermé lorsque les tests ciblés, les deux smokes, le round-trip des projets exemples, le
tracker, le graphe et `scripts/Verify.ps1` sont verts sur le même état Git.
