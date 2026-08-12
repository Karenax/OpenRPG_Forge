# P1 R051 — Événements gameplay du projet

Lot stable : `R051` / `FC-PROJECT-P1-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-PROJ-007` — événements de connexion client ;
- `ORF-PROJ-008` — événements d’objet acquis/perdu ;
- `ORF-PROJ-009` — événements d’utilisation d’objet ;
- `ORF-PROJ-010` — événements de mort et respawn ;
- `ORF-PROJ-011` — événements d’équipement et niveau.

Source immuable relue intégralement : sections correspondantes de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/02_PROJECT.md`.

## Audit avant implémentation

| Critère source | Présent | Limite réelle R051 |
|---|---|---|
| événements typés | `ProjectGameplayEventBus` couvre connexion, inventaire, utilisation, vie, équipement et progression | conserver et relier au profil canonique |
| autorité et transaction | autorité, révision, veto et journal atomique | valider l’allowlist complète de bindings serveur |
| configuration canonique | `EventBinding` UUID/schéma/provenance/API existe | profil module persistant avec politiques mort/respawn et réplication |
| interface | écran général des politiques d’exécution | ajouter un onglet spécialisé, états vide/erreur et édition sans JSON |
| commande/historique | `SetRuntimePoliciesCommand` dry-run/apply et historique projet | inclure le profil événementiel sans voie parallèle ni perte d’extensions |
| build/runtime | le projet et le runtime connaissent les politiques d’exécution | diagnostic stable pré-build et résolution optionnelle/requise |
| MCP/agent | lecture et plans de politiques existants | vue filtrée par UUID, nom d’événement ou handler |

## Dépendances réévaluées

Les contrats nécessaires ITEM, CREATURE, GAMEPLAY, MULTI et SCRIPT existent déjà : événements typés,
autorité serveur, règles de progression, bindings ForgeScript et commande projet. R051 n’ouvre aucun
lot P1 dépendant et se limite au contrat traversant de configuration/validation/exécution.

## Fichiers probables

- `src/Forge.Domain/Lifecycle/ProjectGameplayEvents.cs` ;
- `src/Forge.Domain/RuntimePolicies/ModuleRuntimePolicyProfile.cs` ;
- `src/Forge.Application/Lifecycle/ProjectGameplayEventBus.cs` ;
- `apps/Forge.Toolset/addons/openrpg_forge/RuntimePoliciesWindow.cs` ;
- `src/Forge.Mcp/Projects/*`, `apps/Forge.McpHost/ForgeMcpTools.cs` ;
- tests Domain, Application, ModuleFormat et MCP associés.

## Preuves requises

- validation des 14 bindings, UUID, schéma, provenance, API, handler et autorité ;
- round-trip projet et package, migration legacy et refus pré-build localisé ;
- même commande pour UI et MCP, dry-run/apply/revert, undo/redo ;
- bus et pipeline de mort configurés, fallback runtime optionnel/requis ;
- capture inspectée de l’onglet événementiel et smokes Toolset/runtime ;
- tracker/backlog/docs/graphe frais puis `scripts/Verify.ps1` vert.

## Non-objectifs

- événements de repos P2 (`ORF-PROJ-012`) ou autres fonctions R052 ;
- transport réseau P2 supplémentaire ;
- accès direct aux fichiers/nœuds moteur ou exécution de shell ;
- P2, AV7–AV10, commit ou push.

## Fermeture et preuves

- profil canonique `ProjectGameplayEventProfile` avec 14 bindings serveur, identité stable,
  schéma v1, provenance, API ForgeScript et politiques de mort/respawn/réplication ;
- validation Domain et pré-build avec codes et chemins sources préservés dans l'erreur d'export ;
- transaction unique `SetRuntimePoliciesCommand` pour l'interface et le MCP, avec dry-run,
  apply, revert, audit et historique undo/redo ;
- bus runtime configuré, pipeline de mort configurable et résolution optionnelle/requise ;
- round-trip projet legacy/courant, lecture MCP filtrée et package bloqué avant écriture si invalide ;
- fixture jouable `crypt-of-ashes` migrée vers le profil canonique afin qu'un save du Toolset reste
  byte-stable pendant le portail de vérification ;
- capture inspectée : `docs/aurora-parity/evidence/r051-project-events-ui.png` ;
- marqueurs attendus : `OPENRPG_FORGE_TOOLSET_R051_PROJECT_EVENTS_PASS` et
  `OPENRPG_FORGE_RUNTIME_R051_PROJECT_EVENTS_PASS` ;
- tests cibles R051 : Domain 2, Application 2, ModuleFormat 2 et MCP 1, tous verts.

Le lot est promu `Tested` uniquement après régénération des suivis, du backlog, de la documentation
et du graphe, puis validation du portail intégré.
