# P1 R070 — Production narrative de dialogue

## Périmètre

Lot canonique `R070 / FC-DIALOGUE-P1-03`, limité à cinq fonctions :

- `ORF-DLG-026` — graphes imbriqués ;
- `ORF-DLG-028` — relations et conséquences ;
- `ORF-DLG-030` — couverture de branches ;
- `ORF-DLG-031` — édition collaborative ;
- `ORF-DLG-032` — génération IA contrôlée.

Les dépendances déclarées `BUILD`, `QUEST`, `FACTION`, `QA` et `AI` sont consommées par des contrats
typés, une analyse sans mutation et une transaction explicite. Le lot n’ouvre ni P2, ni AV7–AV10,
ni R071.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/07_DIALOGUE.md` avant modification.

| Critère source | État avant R070 | Limite réelle fermée par R070 |
|---|---|---|
| Graphes imbriqués | types et validation isolés dans `Forge.RpgKit.Domain`, sans document principal | contrats `SubDialogue` persistés avec version, paramètres entrée/sortie typés, variables requises, bindings et test isolé |
| Relations et conséquences | calcul d’impact unitaire isolé | conséquences canoniques Quest/Faction/Item/World attachées aux nœuds, affichées et analysées via dépendances inverses |
| Couverture | énumération unitaire de chemins et cycles | politique persistée avec profondeur bornée, états requis, chemins, branches impossibles, preuves Test/Session, nœuds couverts/non couverts et ratio |
| Collaboration | records unitaires non reliés au projet | JSON canonique par nœud, IDs, révisions, locks expirables, commentaires, diff sémantique et résolution de conflits persistés |
| IA contrôlée | preview/apply isolés sans plan canonique | brief, beats, style, citations, lint, autorisation, `transactionId`, `planId`, diff, preview et apply explicite non répétable |

## Matrice d’acceptation

| Critère commun immuable | Preuve R070 |
|---|---|
| Domaine canonique avant moteur | `DialogueProductionProfile`, son validateur et son runtime ne dépendent pas du moteur |
| Identité et déterminisme | UUID, schéma, provenance, ordre, versions, références et profondeur sont persistés et validés |
| Validation structurée | codes et chemins stables pour identité, sous-dialogues, conséquences, couverture, collaboration et IA |
| Interface visible, vide et erreur | panneau intégré avec champs des cinq familles, aperçu couverture/impact, minicarte, validation et simulation |
| UI et commande uniques | `SetDialogueHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | snapshot production, lecture de contrat et conséquences par handle de graphe |
| MCP filtré | ressource et plan enrichis, schéma JSON explicite, recherche, diff, apply, persistance et revert |
| Runtime moteur | racine `DialogueProductionAdapters` et nœuds contrats, conséquences, couverture, collaboration et plans IA |
| Optionnel/requis | absence optionnelle ignorée ; profil exigé absent signalé par diagnostic stable |

## Validation exécutée

- tests ciblés : Domain 4/4, Application 4/4, ModuleFormat 1/1, MCP R070 1/1 et schéma MCP 1/1 ;
- format intégral sans dérive ;
- suite .NET complète : 1 501/1 501 tests verts ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R070_DIALOGUE_PRODUCTION_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R070_DIALOGUE_PRODUCTION_PASS` avec nœuds moteur réels ;
- capture `docs/aurora-parity/evidence/r070-dialogue-production-ui.png`, 1440×950, inspectée : panneau
  complet, sous-dialogue, quatre impacts, couverture, collaboration, preview IA, simulation et validation
  visibles sans chevauchement global ; les longues lignes structurées défilent uniquement dans leur champ ;
- capture moteur : `OPENRPG_FORGE_R070_DIALOGUE_PRODUCTION_UI_CAPTURE_PASS`.
- portail intégré : 27 projets construits sans vulnérabilité, avertissement ni erreur, 1 501 tests
  .NET et 39 tests Python, smokes Toolset/runtime, assets, Blender, exports, cartes 3D, réseau, MCP,
  sauvegarde/reprise et marqueur terminal `OPENRPG_FORGE_VERIFICATION=PASS` ;
- tracker/backlog régénérés : 485 `Tested`, 231 `Implemented`, 1 `Researched`, 124 lots clos ;
- graphe Roslyn frais : 6 824 nœuds, 43 232 relations et zéro diagnostic.

## Décision

Les limites fonctionnelles des cinq critères sont fermées et reproductibles. Seules ces cinq fonctions
ont été promues individuellement à `Tested`. Le tracker, le backlog, la documentation et le graphe
Roslyn ont été régénérés et le portail `scripts/Verify.ps1` est vert dans ce même lot. `R071` reste non
audité et n’est ouvert qu’après cette clôture.
