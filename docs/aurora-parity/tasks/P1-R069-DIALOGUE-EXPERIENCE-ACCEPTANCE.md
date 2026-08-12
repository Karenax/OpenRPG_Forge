# P1 R069 — Expérience de dialogue

## Périmètre

Lot canonique `R069 / FC-DIALOGUE-P1-02`, limité à cinq fonctions :

- `ORF-DLG-017` — scripts de fin et d’abandon ;
- `ORF-DLG-018` — recherche dans les conversations ;
- `ORF-DLG-019` — mise en évidence et filtres ;
- `ORF-DLG-022` — mémoire de conversation ;
- `ORF-DLG-024` — checks de compétence.

Le lot ferme uniquement les limites P1 de ces fonctions. Il n’ouvre ni P2, ni AV7–AV10,
ni le lot R070.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/07_DIALOGUE.md` avant modification.

| Critère source | État avant R069 | Limite réelle fermée par R069 |
|---|---|---|
| Fin et abandon | événements isolés dans le RPG Kit | politique persistée `OnDialogueCompleted/Aborted`, raison typée, actions, rollback optionnel et reprise avec mémoire et seed |
| Recherche | service isolé | recherche multi-dialogues ordonnée, texte/choix, regex `NonBacktracking` bornée, filtres locuteur/condition/action et remplacement preview-only |
| Mise en évidence | calcul isolé | six badges, légende, filtres et mode review visibles et persistés |
| Mémoire | dictionnaire isolé | slots canoniques typés pour dialogue, participant, joueur, groupe ou monde avec durée explicite et valeur par défaut validée |
| Checks de compétence | résolution isolée | difficulté, opposant, avantage, résultat, visibilité, explication et branche succès/échec déterministes attachés aux nœuds persistés |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R069 |
|---|---|
| Domaine canonique avant moteur | `DialogueExperienceProfile`, son validateur et son runtime ne dépendent pas du moteur |
| Identité et déterminisme | UUID, schéma, provenance, ordre, seed, références de nœuds et clés mémoire sont persistés et validés |
| Validation structurée | codes et chemins stables pour profil, cycle, recherche, revue, mémoire et checks |
| Interface visible, vide et erreur | panneau intégré au dialogue, recherche preview, six badges, mémoire, checks, minicarte, validation et simulation |
| UI et commande uniques | `SetDialogueHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | snapshot expérience et lecture du check par handle de graphe |
| MCP filtré | ressource et plan enrichis, schéma JSON explicite, diff, apply, persistance et revert |
| Runtime moteur | racine `DialogueExperienceAdapters`, cycle, recherche/revue, nœuds mémoire et checks |
| Optionnel/requis | absence optionnelle ignorée ; profil exigé absent signalé par diagnostic stable |

## Validation exécutée

- compilation Debug des 27 projets : 0 avertissement, 0 erreur ;
- tests ciblés : Domain 4/4, Application 3/3, ModuleFormat 1/1 et MCP 2/2 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R069_DIALOGUE_EXPERIENCE_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R069_DIALOGUE_EXPERIENCE_PASS` avec nœuds moteur réels ;
- capture `docs/aurora-parity/evidence/r069-dialogue-experience-ui.png`, 1440×950, inspectée après
  correction : panneau complet, libellés, cycle fin/abandon, 4 résultats de recherche preview-only,
  six badges sur deux lignes, mémoire et check visibles, sans chevauchement ni défilement horizontal global ;
- format intégral vérifié sans changement ;
- tracker, backlog et documentation régénérés : 480 `Tested`, 236 `Implemented`, 1 `Researched`
  et 123 lots clos ;
- graphe Roslyn frais à 6 778 nœuds et 42 931 relations, sans diagnostic ;
- portail `scripts/Verify.ps1` vert : 27 projets sans vulnérabilité, avertissement ni erreur,
  1 494 tests .NET, 39 tests Python, smokes Toolset/runtime R069, Blender, assets, exports, cartes 3D,
  réseau, MCP et sauvegarde/reprise ;
- verdict terminal : `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les limites réelles des cinq fonctions sont fermées et le portail intégré est vert. Ces cinq
fonctions, et uniquement elles, passent à `Tested`. R070 reste non audité et non promu.
