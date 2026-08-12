# P1 R067 — Session, sauvegarde et replay gameplay

## Périmètre

Lot canonique `R067 / FC-GAMEPLAY-P1-02`, limité à cinq fonctions :

- `ORF-GAME-021` — sauvegarde par joueur, partie et serveur ;
- `ORF-GAME-025` — checkpoints et respawn ;
- `ORF-GAME-026` — carte, journal et HUD ;
- `ORF-GAME-027` — verrous de contrôle des cinématiques et dialogues ;
- `ORF-GAME-029` — test de replay déterministe.

Le lot ferme uniquement les limites P1 de ces fonctions. Il n’ouvre ni P2, ni AV7–AV10,
ni le lot R068.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/19_GAMEPLAY.md` avant modification.

| Critère source | État avant R067 | Limite réelle fermée par R067 |
|---|---|---|
| Sauvegarde par portée | profils et résolution de conflit isolés | agrégat module persistant, autorité explicite, révisions, adaptateur cloud optionnel, confidentialité, chiffrement, transaction et nœud runtime |
| Checkpoints et respawn | coût, politique de groupe, cadavre et rayon sûr modélisés | waypoint validé, spawn sûr, délai, hooks avant/après, autorité, résolution de respawn et `Area3D` moteur |
| Carte, journal et HUD | shell data-driven isolé | kit, skin, échelle, actions d’entrée, visibilité des trois surfaces, résolution runtime et `CanvasLayer` moteur |
| Verrous dialogue/cinématique | pile de contextes calculable | propriétaire, curseur, caméra, actions, priorité, remplacement atomique et restauration garantie dans l’éditeur et le runtime |
| Replay déterministe | enregistrement tick/input/événement/seed/hash isolé | replay headless borné, détection et rapport de divergence, manifeste de reproduction minimal et expurgé, adaptateur moteur |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R067 |
|---|---|
| Domaine canonique avant moteur | `GameplaySessionProfile` agrège persistance, checkpoints, shell, contrôles et replay sans dépendre de Godot |
| Identité et déterminisme | UUID, schéma, provenance, révision, ordre des contextes, ticks, seeds et hashes sont persistés et validés |
| Validation structurée | codes et chemins stables pour identité, persistance, checkpoints, waypoint, shell, contrôles, replay et contenu requis |
| Interface visible, vide et erreur | onglet `Session & replay P1`, parcours graphique, cinq cartes compactes, statut et rejet visible |
| UI et commande uniques | `SetGameplayControlProfileHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | handles générationnels pour session, sauvegarde, checkpoint, shell, verrou et replay ; autorité exigée pour les mutations |
| MCP filtré | recherche par texte/type et plan gameplay avec diff sémantique, apply, persistance et revert |
| Runtime moteur | adaptateurs de sauvegarde, `Area3D` de checkpoint, shell `CanvasLayer`, pile de contrôles et adaptateur de replay |
| Optionnel/requis | absence optionnelle ignorée ; profil requis absent signalé par diagnostic stable |

## Validation exécutée

- compilation Debug du Toolset et de ses dépendances : 0 avertissement, 0 erreur ;
- tests ciblés : Domain 3/3, Application 7/7, ModuleFormat 1/1, MCP 1/1 et Scripting 59/59 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R067_GAMEPLAY_SESSION_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R067_GAMEPLAY_SESSION_PASS` avec nœuds moteur réels ;
- capture `docs/aurora-parity/evidence/r067-gameplay-session-ui.png`, 1440×900, inspectée :
  titre, parcours, cinq cartes, statut et actions lisibles, sans chevauchement ni défilement horizontal ;
- tracker, backlog et documentation régénérés : 470 `Tested`, 246 `Implemented`, 1 `Researched`
  et 121 lots clos ;
- graphe Roslyn frais à 6 706 nœuds et 42 391 relations, sans diagnostic ;
- portail `scripts/Verify.ps1` vert : 27 projets sans vulnérabilité, avertissement ni erreur,
  1 477 tests .NET, 39 tests Python, smokes Toolset/runtime, Blender, assets, exports, cartes 3D,
  réseau, MCP et sauvegarde/reprise ;
- verdict terminal : `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les limites réelles des cinq fonctions sont fermées et le portail intégré est vert. Ces cinq
fonctions, et uniquement elles, passent à `Tested`. R068 reste non audité et non promu.
