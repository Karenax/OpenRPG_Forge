# P1 R071 — Objectifs, récompenses, notes, cast et Plot Wizard

## Périmètre

Lot canonique `R071 / FC-QUEST-P1-01`, limité à cinq fonctions :

- `ORF-QST-006` — objectifs explicites ;
- `ORF-QST-008` — récompenses ;
- `ORF-QST-010` — commentaires auteur ;
- `ORF-QST-011` — Plot Wizard ;
- `ORF-QST-012` — cast du scénario.

Les dépendances déclarées `BUILD`, `CREATURE`, `DIALOGUE`, `FACTION`, `ITEM` et `SCRIPT` sont
consommées par des références projet résolues, un document canonique et une commande transactionnelle.
Le lot n’ouvre ni P2, ni AV7–AV10, ni R072.

## Audit source avant modification

Les cinq blocs complets et leurs critères communs ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/08_QUEST.md` avant modification.

| Critère source | État avant R071 | Limite réelle fermée par R071 |
|---|---|---|
| Objectifs explicites | huit types testés uniquement dans `Forge.RpgKit.Domain` | profil attaché à `QuestDefinition`, UUID, schéma, provenance, ordre, localisation et contrat propre à chaque type |
| Récompenses | bundle et état isolés | bundle canonique et atomique progression/objets/monnaies/réputation/unlocks/choix, rejet sans mutation |
| Commentaires auteur | record unitaire sans projet | design, canon, propriétaire, statut, risques, TODO, sources et reviews versionnées persistés |
| Plot Wizard | preview isolée sans interface principale | plan transparent zones/cast/props/dialogues/objectifs/transitions, cinq scénarios et interdiction du code opaque |
| Cast | bindings isolés | participants à rôle unique, ordre, UUID, blueprint/instance/query, conditions et présence requise résolus par projet |

## Matrice d’acceptation

| Critère commun immuable | Preuve R071 |
|---|---|
| Domaine canonique avant moteur | `QuestAuthoringProfile`, son validateur et son service de récompense ne dépendent pas du moteur |
| Identité et déterminisme | UUID, schéma, provenance, ordre, références et reviews datées sont sérialisés de façon stable |
| Validation structurée | codes et chemins stables pour profil, objectifs, récompenses, notes, cast, Wizard et références projet |
| Interface visible, vide et erreur | onglet intégré, résumé dynamique, huit objectifs, récompenses, notes, cast/Wizard, mini-graphe et simulation |
| UI et commande uniques | `SetQuestHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | snapshot auteur, lecture d’objectif et participant par handle de quête |
| MCP filtré | ressource structurée filtrable par rôle/binding/notes et mutation complète en dry-run |
| Runtime moteur | racine `QuestAuthoringAdapters` avec nœuds objectifs, récompenses, notes, cast et Wizard |
| Optionnel/requis | absence optionnelle ignorée ; profil requis désactivé signalé par diagnostic stable |

## Validation exécutée

- tests ciblés : Domain R071 4/4, Application QuestScript 3/3, ModuleFormat R071 1/1 et MCP R071 1/1 ;
- format intégral sans dérive ;
- suite .NET complète : 1 508/1 508 tests verts ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R071_QUEST_AUTHORING_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R071_QUEST_AUTHORING_PASS` avec nœuds moteur réels ;
- capture `docs/aurora-parity/evidence/r071-quest-authoring-ui.png`, 1450×910, inspectée : résumé
  8 objectifs / 1 participant / 1 zone / 1 prop / 1 dialogue / 5 tests / aucun code opaque,
  récompenses, notes, mini-graphe, simulation et validation visibles sans chevauchement global ;
- capture moteur : `OPENRPG_FORGE_R071_QUEST_AUTHORING_UI_CAPTURE_PASS`.
- portail intégré : 27 projets construits sans vulnérabilité, avertissement ni erreur, 1 508 tests
  .NET et 39 tests Python, smokes Toolset/runtime, assets, Blender, exports, cartes 3D, réseau, MCP,
  sauvegarde/reprise et marqueur terminal `OPENRPG_FORGE_VERIFICATION=PASS` ;
- tracker/backlog régénérés : 490 `Tested`, 226 `Implemented`, 1 `Researched`, 125 lots clos ;
- graphe Roslyn frais : 6 849 nœuds, 43 444 relations et zéro diagnostic.

## Décision

Les limites fonctionnelles des cinq critères sont fermées et reproductibles. Seules ces cinq fonctions
ont été promues individuellement à `Tested`. Le tracker, le backlog, la documentation et le graphe
Roslyn ont été régénérés et le portail `scripts/Verify.ps1` est vert dans ce même lot. `R072` reste non
audité et n’est ouvert qu’après cette clôture.
