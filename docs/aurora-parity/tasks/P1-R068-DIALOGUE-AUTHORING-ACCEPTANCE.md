# P1 R068 — Auteur de dialogues avancé

## Périmètre

Lot canonique `R068 / FC-DIALOGUE-P1-01`, limité à cinq fonctions :

- `ORF-DLG-007` — tokens de texte ;
- `ORF-DLG-008` — participants multiples ;
- `ORF-DLG-010` — évaluation dans l’ordre ;
- `ORF-DLG-012` — assistant de scripts de dialogue ;
- `ORF-DLG-013` — commentaires de nœud.

Le lot ferme uniquement les limites P1 de ces fonctions. Il n’ouvre ni P2, ni AV7–AV10,
ni le lot R069.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/07_DIALOGUE.md` avant modification.

| Critère source | État avant R068 | Limite réelle fermée par R068 |
|---|---|---|
| Tokens de texte | contrat isolé dans le RPG Kit | profil canonique attaché au dialogue persisté, types explicites, valeur requise ou défaut, auto-complétion, aperçu contextuel et échappement sûr |
| Participants multiples | bindings isolés par rôle, requête, tag ou entité | participants stables, fallback résolu, locuteur sélectionnable par nœud, validation, UI et adaptateurs moteur |
| Évaluation dans l’ordre | cinq stratégies calculables mais non reliées au dialogue principal | `FirstValid`, `AllValid`, `Weighted`, `RandomDeterministic` et `Manual` visibles, ordonnés, seedés, persistés et exécutés au runtime |
| Assistant de scripts | builders typés isolés | wizard visible générant les conditions/actions canoniques avec source `builder:` inspectable, cibles validées et même transaction que l’édition manuelle |
| Commentaires de nœud | notes éditoriales isolées | intention, émotion, source, statut, TODO et auteur attachés à chaque nœud, persistés et exposés aux outils |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R068 |
|---|---|
| Domaine canonique avant moteur | `DialogueAuthoringProfile` et son validateur ne dépendent pas du moteur |
| Identité et déterminisme | UUID, schéma, provenance, ordre des choix, poids et seed sont persistés et validés |
| Validation structurée | codes et chemins stables pour profil requis, identité, participants, nœuds, tokens, évaluation, wizard et notes |
| Interface visible, vide et erreur | panneau auteur intégré au dialogue, minicarte, validation, simulation et erreurs visibles |
| UI et commande uniques | `SetDialogueHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | handles de graphe et snapshots auteur/nœud ; catalogue API dédié aux tokens, participants, évaluations et notes |
| MCP filtré | ressource filtrée et plan enrichi, diff sémantique, apply, persistance et revert ; schéma JSON explicite |
| Runtime moteur | racine `DialogueAuthoringAdapters`, bindings participants et un nœud moteur par politique de nœud |
| Optionnel/requis | absence optionnelle ignorée ; profil exigé absent signalé par diagnostic stable |

## Validation exécutée

- compilation Debug des 27 projets : 0 avertissement, 0 erreur ;
- tests ciblés : Domain 4/4, Application 5/5, ModuleFormat 1/1 et MCP 2/2 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R068_DIALOGUE_AUTHORING_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R068_DIALOGUE_AUTHORING_PASS` avec nœuds moteur réels ;
- capture `docs/aurora-parity/evidence/r068-dialogue-authoring-ui.png`, 1440×950, inspectée :
  panneau auteur, participants, token, cinq modes, wizard et sources, notes, simulation et validation lisibles,
  sans chevauchement ni défilement horizontal ;
- tracker, backlog et documentation régénérés : 475 `Tested`, 241 `Implemented`, 1 `Researched`
  et 122 lots clos ;
- graphe Roslyn frais à 6 744 nœuds et 42 676 relations, sans diagnostic ;
- portail `scripts/Verify.ps1` vert : 27 projets sans vulnérabilité, avertissement ni erreur,
  1 487 tests .NET, 39 tests Python, smokes Toolset/runtime R068, Blender, assets, exports, cartes 3D,
  réseau, MCP et sauvegarde/reprise ;
- verdict terminal : `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les limites réelles des cinq fonctions sont fermées et le portail intégré est vert. Ces cinq
fonctions, et uniquement elles, passent à `Tested`. R069 reste non audité et non promu.
