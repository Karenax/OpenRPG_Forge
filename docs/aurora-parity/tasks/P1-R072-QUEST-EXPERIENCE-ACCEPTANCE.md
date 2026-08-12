# P1 R072 — Ressources, étapes, visibilité, chronologie et pins

## Périmètre

Lot canonique `R072 / FC-QUEST-P1-02`, limité à cinq fonctions :

- `ORF-QST-013` — props du scénario ;
- `ORF-QST-014` — étapes de scénario ;
- `ORF-QST-015` — quêtes cachées ou révélées ;
- `ORF-QST-019` — chronologie des événements ;
- `ORF-QST-020` — pins de carte.

Les dépendances déclarées `AREA`, `DIALOGUE`, `DOOR`, `ITEM`, `PLACEABLE`, `QA`, `TRIGGER` et
`WAYPOINT` sont consommées par des références typées, le document de quête, la validation projet et
les adaptateurs moteur. Le lot n'ouvre ni P2, ni AV7–AV10, ni R073.

## Audit source avant modification

Les cinq blocs complets et leurs critères communs ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/08_QUEST.md` avant modification.

| Critère source | État avant R072 | Limite réelle fermée par R072 |
|---|---|---|
| Props du scénario | cinq types testés seulement dans `Forge.RpgKit.Domain` | ressources canoniques attachées à la quête, UUID, ordre, ownership, conditions et résolution projet des références requises |
| Étapes | instanciation isolée sans document principal | templates versionnés en étapes déterministes, modifiables, localisées, reliées aux objectifs/transitions et persistées |
| Visibilité | politique isolée sans interface ni runtime moteur | états Hidden/Rumor/Discovered/Tracked/SecretCompleted, conditions ordonnées et suivi manuel dans le document canonique |
| Chronologie | projection joueur/debug isolée | événements horodatés à UUID, transition, cause, acteur, contexte debug et résumé joueur localisé |
| Pins | calcul de suivi isolé | zone/entité/query/waypoint, objectif, visibilité, précision, suivi, libellé, fallback spatial et `Node3D` runtime |

## Matrice d'acceptation

| Critère commun immuable | Preuve R072 |
|---|---|
| Domaine canonique avant moteur | `QuestExperienceProfile`, son validateur et son service runtime ne dépendent pas du moteur |
| Identité et déterminisme | UUID, schéma, provenance, ordre, versions de templates et chronologie datée sont sérialisés de façon stable |
| Validation structurée | codes et chemins stables pour profil, ressources, étapes, visibilité, historique, pins et références projet |
| Interface visible, vide et erreur | onglet intégré, résumé dynamique, cinq ressources, étape, quatre révélations, deux événements, quatre pins, mini-graphe et simulation |
| UI et commande uniques | `SetQuestHandler`, dry-run/apply/reject, undo/redo, renommage conservatif et round-trip `ProjectSession` |
| ForgeScript typé | snapshot expérience, lecture de pin et chronologie joueur/debug par handle de quête |
| MCP filtré | ressource structurée filtrable par référence, owner, binding et libellé ; mutation complète en dry-run avec rejet localisé |
| Runtime moteur | racine `QuestExperienceAdapters` et nœuds ressources, étapes, visibilité, historique et pins `Node3D` |
| Optionnel/requis | absence optionnelle ignorée ; profil requis désactivé et référence requise absente diagnostiqués |

## Validation exécutée

- tests ciblés : Domain R072 5/5, Application QuestScript 4/4, ModuleFormat R072 1/1 et MCP R072 1/1 ;
- format intégral sans dérive ;
- suite .NET complète : 1 516/1 516 tests verts ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R072_QUEST_EXPERIENCE_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R072_QUEST_EXPERIENCE_PASS` avec nœuds moteur réels ;
- capture `docs/aurora-parity/evidence/r072-quest-experience-ui.png`, 1450×910, inspectée après
  compactage : résumé 5 ressources / 1 étape / 4 révélations / 2 événements / 4 pins, les cinq
  sections, mini-graphe, validation et simulation sont lisibles sans chevauchement global ;
- capture moteur : `OPENRPG_FORGE_R072_QUEST_EXPERIENCE_UI_CAPTURE_PASS` ;
- portail intégré : 27 projets construits sans vulnérabilité, avertissement ni erreur, 1 516 tests
  .NET et 39 tests Python, smokes Toolset/runtime, assets, Blender, exports, cartes 3D, réseau, MCP,
  sauvegarde/reprise et marqueur terminal `OPENRPG_FORGE_VERIFICATION=PASS` ;
- tracker/backlog régénérés : 495 `Tested`, 221 `Implemented`, 1 `Researched`, 126 lots clos ;
- graphe Roslyn frais : 6 874 nœuds, 43 653 relations et zéro diagnostic.

## Décision

Les limites fonctionnelles des cinq critères sont fermées et reproductibles. Seules ces cinq
fonctions ont été promues individuellement à `Tested`. Le tracker, le backlog, la documentation et
le graphe Roslyn ont été régénérés et le portail `scripts/Verify.ps1` est vert dans ce même lot.
`R073` reste non audité et n'est ouvert qu'après cette clôture.
