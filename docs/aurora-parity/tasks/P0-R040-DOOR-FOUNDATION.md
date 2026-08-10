# P0 R040 — fondation de porte

Lot stable : `R040` / `FC-DOOR-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-DOOR-001` — portes de tileset et universelles.
- `ORF-DOOR-002` — états ouverte, fermée et verrouillée.
- `ORF-DOOR-003` — interaction d'ouverture.
- `ORF-DOOR-008` — transition vers zone.
- `ORF-DOOR-009` — transition unidirectionnelle ou bidirectionnelle.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/11_DOOR.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R040 |
|---|---|---|
| définition canonique | modèles RPG Kit isolés, profils monde de dégâts, chargement et scripts | politique de fondation persistante reliée au document projet, avec placement socket ou libre et adaptateur de kit |
| machine d'états | états et transitions RPG Kit | neuf états canoniques, animations, collision, navigation et transitions validées dans le profil réellement édité |
| interaction | service RPG Kit | résolution runtime de portée, côté, permission, file, feedback, animation, collision et autorité |
| portail | lien d'aire générique | destination typée porte/trigger/waypoint, ancre, orientation, préchargement et permission dans le profil de porte |
| lien retour | service de paire RPG Kit | lien one-way explicite ou paire bidirectionnelle atomique avec destination de retour validée |
| auteur/agents | studio monde, commande catalogue, ForgeScript et MCP génériques | formulaire Portes, aperçu transactionnel, dry-run/apply, undo/redo et recherche/plan MCP sur le même document canonique |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| sockets et placement libre | `DoorWorldProfile.FoundationPolicy`, `PlacementMode` et `KitAdapter` | `R040_DoorFoundationExecutesStateInteractionPortalAndBidirectionalLink` et smoke Toolset R040 |
| machine d'états complète | `State`, `Transition`, état initial et validation des références | tests Domain et marker Toolset `states=open-closed-opening-closing-locked-jammed-broken-disabled-custom` |
| interaction autoritaire | `WorldFeatureRuntimeResolver.InteractWithDoor` | tests acceptation/refus et marker `interaction=range-side-clearance-animation-collision-authority-queue-feedback` |
| portail typé | `PortalPolicy` | round-trip ModuleFormat et formulaire inspecté dans `evidence/r040-door-foundation-ui.png` |
| liens one-way/two-way | `LinkPolicy` et validation de la destination retour | test Domain et marker `links=oneway-or-atomic-pair` |
| validation pré-build | `WorldFeatureCatalogValidator.ValidateDoorFoundation` et codes stables | tests Domain de profils valides/invalides et smoke projet |
| commande, historique, script et MCP | `SetWorldFeatureCatalogHandler`, `WorldFeatureScriptService`, `QueryWorldFeatures` et `PreviewWorldFeatures` | suites Application/MCP existantes et smoke Toolset transactionnel |
| persistance | propriété optionnelle `Foundation` du profil | `WorldFeaturePersistenceTests` vérifie neuf états et la destination typée après rechargement |

## Non-objectifs

- ouvrir les fonctions de porte P1/P2 ;
- modifier les règles RPG Kit déjà validées ;
- ajouter P1/P2, AV7–AV10, commit ou push.

## Validation finale

- tests ciblés RpgKit.Domain, Domain, Application, ModuleFormat et MCP verts ;
- capture 1540×940 inspectée, onglet Portes lisible et champs de fondation visibles ;
- smoke `OPENRPG_FORGE_TOOLSET_R040_DOOR_FOUNDATION_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R041.
