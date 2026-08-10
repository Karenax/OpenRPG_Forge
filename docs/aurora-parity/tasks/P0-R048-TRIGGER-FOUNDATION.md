# P0 R048 — Fondations trigger

Lot stable : `R048` / `FC-TRIGGER-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-TRG-001` — création polygonale ;
- `ORF-TRG-002` — trigger générique ;
- `ORF-TRG-003` — trigger de transition ;
- `ORF-TRG-006` — OnEnter ;
- `ORF-TRG-007` — OnExit.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/12_TRIGGER.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R048 |
|---|---|---|
| volume 2D/3D | modèles et calculs isolés du RPG Kit | document canonique avec sommets, trous, hauteur, snap, transform, preview et fallback runtime |
| trigger générique | filtres, cooldown et actions dans le RPG Kit | profil projet versionné, commande transactionnelle, handle ForgeScript et ressource MCP filtrée |
| transition | composant portail RPG Kit | destination typée, ancre, orientation, preload et permissions persistés dans le catalogue monde |
| OnEnter | payload RPG Kit | événement runtime canonique avec entité, point, direction, layers, state, API et scripts gouvernés |
| OnExit | payload RPG Kit | événement runtime canonique avec durée, raison, API et scripts gouvernés |
| auteur | ancien onglet de comportements avancés | volume, filtres, actions, portail et événements visibles dans l'éditeur sans JSON manuel |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| volume polygonal/3D | `TriggerWorldProfile.FoundationPolicy`, `ResolveTriggerFoundation` | test snap sommets/transform et overlay runtime |
| filtres/actions/cooldown | `EnterTrigger` | tests filtre, condition, fire policy et action typée |
| transition | `PortalPolicy` | round-trip destination/ancre et handle agent |
| OnEnter/OnExit | `TriggerVolumeEntered`, `TriggerVolumeExited` | test payloads, durée, raison et bindings ForgeScript |
| interface | sections `VOLUME CANONIQUE`, `FILTRES, ACTIONS ET TRANSITION`, `EVENEMENTS FORGESCRIPT` | capture inspectée `evidence/r048-trigger-foundation-ui.png` |
| transaction | `SetWorldFeatureCatalogHandler` | dry-run/apply/rejet et undo/redo existants sur le même document canonique |
| MCP | propriétés trigger filtrées | test ressource bornée et plan dry-run sans révision |
| persistance | catalogue monde | round-trip forme, portail, événements et exigence runtime |

## Non-objectifs

- validation exhaustive `ORF-TRG-020`, réservée au lot R049 ;
- triggers P1/P2, AV7–AV10, commit ou push.

## Validation finale

- suites Domain, Application, RpgKit.Domain, ModuleFormat et MCP vertes ;
- capture 1540×940 inspectée : volume canonique et résumé visibles sans chevauchement ;
- smoke `OPENRPG_FORGE_TOOLSET_R048_TRIGGER_FOUNDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R049.
