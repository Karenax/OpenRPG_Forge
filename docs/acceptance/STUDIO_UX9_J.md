# Acceptation Studio — UX9-J Opérations Live Director

État local au 23 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **46/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorOperationsProfile` persistant et
ouvre trois vues humaines : recherche/ForgeScript, variables/scène, puis visibilité MJ. L’humain
choisit explicitement le module, configure la sonde, prévisualise la commande canonique, approuve
cette seule révision, applique, annule/rétablit, enregistre puis confirme une sandbox locale
distincte en rôle Administrateur. Le fichier `.forge/live-director/operations.json` n’est créé que
sur **Enregistrer** et l’écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-027"></a>
- **ORF-DM-027 — Rechercher par nom et avancer** : recherche indexée par nom ou tag, requête
  enregistrée, cycle déterministe des résultats, lien profond stable et focus visuel du viewport.
  La preuve parcourt deux gardes synthétiques sans exposer leur UUID dans l’interface.

<a id="orf-dm-028"></a>
- **ORF-DM-028 — Exécuter un script** : seules une commande enregistrée ou un snippet signé sont
  acceptés avec budget de 1 à 10 000 instructions et adaptateur ForgeScript serveur. Shell,
  processus, fichiers, réseau, sockets, threads et chargement natif sont refusés avant exécution.

<a id="orf-dm-029"></a>
- **ORF-DM-029 — Modifier des variables** : booléen, nombre borné, énumération et texte sont
  validés par leur schéma. L’aperçu affiche le diff ; l’application exporte puis restaure l’état
  persistant canonique avant de compenser la variable et d’écrire l’audit chaîné.

<a id="orf-dm-032"></a>
- **ORF-DM-032 — Lancer des macros et scènes préparées** : la scène Tempête sur la Crypte relie
  spawns, dialogue, musique, météo et triggers à cinq adaptateurs vérifiables. Un plan de rollback
  est obligatoire et la sandbox ramène le nombre de scènes actives à zéro.

<a id="orf-dm-033"></a>
- **ORF-DM-033 — Projeter le brouillard de guerre MJ** : l’humain compare la vue omnisciente MJ à
  celle d’un joueur sélectionné. L’overlay viewport projette objets cachés, secrets, navigation et
  rencontres sans modifier le brouillard autoritaire.

## Transaction, permissions et préservation

- `LiveDirectorOperationsHistory` accepte Maître de jeu ou Administrateur, impose dry-run,
  révision attendue et progression d’une seule révision ; la sandbox Studio exige Administrateur
  et une confirmation distincte ;
- l’approbation est liée aux empreintes du brouillon et de la source, puis révoquée dès qu’un champ
  ou la révision change ;
- la validation refuse profil nul, collections non canoniques, capacités manquantes, option de
  sonde invalide, script non signé ou API interdite ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- `LiveDirectorOperationsRuntimeAdapter` charge les vrais `AuthorizationService`,
  `LiveWorldChooser`, `LiveAuditJournal`, `LiveSearchService`, `LiveScriptService`,
  `LiveVariableService`, `LiveSceneService` et `LiveVisibilityService` ;
- `LiveDirectorOperationsScriptService` expose un handle borné et le service MCP couvre lecture
  filtrée, plan, apply et revert sous rôle et portée serveur ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont
  préservés, puis le workspace temporaire est supprimé.

## Preuves exécutées

- builds `Forge.Studio` Debug et Release : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **16/16** — LiveDirector 11, ModuleFormat 3 et MCP 2 ; régression complète
  `Forge.LiveDirector.Tests` : **58/58** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_J_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, recherche,
  script, variables, scènes, visibilité, compensation, audit, UI, préservation, restauration et
  libération à `true` ;
- trois captures Godot 4.7.1 Vulkan Forward+ **1920×1080** et la capture du portail inspectées :
  cinq cartes, trois workflows, champs, confirmations, aperçu, audit et six actions sont visibles,
  sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 519,91 s** — build 1,25 s, tests 3,93 + 6,31 + 5,16 s,
  smoke 393,72 s, capture 8,03 s, matrice 0,83 s, génération du graphe 49,33 s et
  `check --full` 51,29 s ;
- matrice Studio après promotion : **678 `functional` / 39 `planned`**, dont UX9 **46/68** ;
- graphe Roslyn officiel final : **11 461 nœuds / 88 901 relations / 0 diagnostic**, frais et
  pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-j-search-script-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-j-variables-scene-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-j-visibility-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-j-live-director-operations-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-j-live-director-operations.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise les autorités réelles avec monde, objets, variable, scène et joueur synthétiques en mémoire,
puis restaure la variable et la scène et libère le workspace temporaire ; elle ne dirige pas une
partie réelle et ne qualifie ni index distribué, compilation AngelScript native, bytecode, moteur de
scène 3D, rendu réel du brouillard, synchronisation réseau, stockage serveur ou sécurité d’un
service exposé. Le focus et l’overlay sont des résultats d’adaptateurs vérifiables, pas un rendu de
jeu. L’ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Narration live, quêtes, rencontres, conséquences
et IA restent hors du lot R129.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorOperationsProfileTests|' +
  'FullyQualifiedName~LiveDirectorOperationsTests|' +
  'FullyQualifiedName~LiveDirectorGovernanceTests|' +
  'FullyQualifiedName~WorkspaceLiveDirectorOperationsStoreTests|' +
  'FullyQualifiedName~LiveDirectorOperationsAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-j-live-director-operations `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-j-live-director-operations-forward-plus-fhd.png' `
  -CheckGraph
```
