# Acceptation Studio — UX9-K Narration Live Director

État local au 23 août 2026 : **fonctionnel et qualifié** pour deux routes UX9. La vague Exploiter
en réseau atteint **48/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorNarrativeProfile` persistant et ouvre
deux workflows humains : quête en direct et rencontre en direct. L’humain choisit explicitement le
module, la portée ou la commande, prévisualise la commande canonique, approuve cette seule révision,
applique, annule/rétablit, enregistre puis confirme une sandbox locale distincte en rôle
Administrateur. Le fichier `.forge/live-director/narrative.json` n’est créé que sur **Enregistrer**
et l’écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-035"></a>
- **ORF-DM-035 — Gérer une quête en direct** : inspection de la quête, transition validée par le
  graphe d’états pour tous les joueurs ou une sélection explicite, aperçu des joueurs et de quatre
  conséquences, puis projection `rpg-kit:quest-journal`. L’application conserve l’état par joueur,
  écrit l’audit et le rollback restitue l’état synthétique précédent.

<a id="orf-dm-036"></a>
- **ORF-DM-036 — Gérer une rencontre en direct** : inspection puis commandes `activate`, `pause`,
  `wave`, `scale` et `despawn`. Le scaling est borné de 0,25 à 4, les adaptateurs d’autorité spawn
  et d’IA sont annoncés avant mutation, et cinq rollbacks ramènent la rencontre synthétique à son
  état initial.

## Transaction, permissions et préservation

- `LiveDirectorNarrativeHistory` accepte Maître de jeu ou Administrateur, impose dry-run, révision
  attendue et progression d’une seule révision ; la sandbox Studio exige Administrateur et une
  confirmation distincte ;
- l’approbation est liée aux empreintes du brouillon et de la source, puis révoquée dès qu’un champ
  ou la révision change ;
- la validation refuse profil nul, portée de quête non canonique, sélection vide, transition
  interdite, commande de rencontre inconnue, scaling hors bornes ou capacité runtime manquante ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- `LiveDirectorNarrativeRuntimeAdapter` charge les vrais `AuthorizationService`,
  `LiveAuditJournal`, `LiveQuestService` et `LiveEncounterService` ;
- `LiveDirectorNarrativeScriptService` expose un handle borné et le service MCP couvre lecture
  filtrée, plan, apply et revert sous rôle et portée serveur ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont
  préservés, puis le workspace temporaire est supprimé.

## Preuves exécutées

- builds `Forge.Studio` Debug et Release : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **10/10** — LiveDirector 5, ModuleFormat 3 et MCP 2 ; régression complète
  `Forge.LiveDirector.Tests` : **58/58** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_K_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, quête,
  rencontre, compensation, audit, UI, préservation, restauration et libération à `true` ;
- deux captures dédiées et la capture du portail Godot 4.7.1 Vulkan Forward+ **1920×1080**
  inspectées : deux cartes, deux workflows, portées, commandes, valeurs, confirmations, aperçu,
  audit et six actions sont visibles, sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 525,88 s** — build 1,26 s, tests 5,06 + 4,62 + 6,56 s,
  smoke 388,70 s, capture 8,02 s, matrice 0,95 s, génération du graphe 61,27 s et
  `check --full` 49,37 s ;
- matrice Studio après promotion : **680 `functional` / 37 `planned`**, dont UX9 **48/68** ;
- graphe Roslyn officiel final : **11 484 nœuds / 89 114 relations / 0 diagnostic**, frais et
  pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-k-quest-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-k-encounter-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-k-live-director-narrative-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-k-live-director-narrative.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise une quête, deux joueurs et une rencontre synthétiques en mémoire, puis restaure leurs états
et libère le workspace temporaire ; elle ne dirige pas une partie réelle et ne qualifie ni journal
de quête distribué, moteur de scripts de quête, spawn 3D, navigation, IA de combat, réplication
réseau, stockage serveur ou sécurité d’un service exposé. Les projections RPG, spawn et IA sont des
résultats d’adaptateurs vérifiables, pas un rendu de jeu. L’ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Apparence, journal MJ, chat, enregistrement de
session et autres routes réseau restent hors du lot R130.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorNarrativeProfileTests|' +
  'FullyQualifiedName~LiveDirectorNarrativeEncounterTests|' +
  'FullyQualifiedName~WorkspaceLiveDirectorNarrativeStoreTests|' +
  'FullyQualifiedName~LiveDirectorNarrativeAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-k-live-director-narrative `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-k-live-director-narrative-forward-plus-fhd.png' `
  -CheckGraph
```
