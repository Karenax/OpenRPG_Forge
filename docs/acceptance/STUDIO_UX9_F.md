# Acceptation Studio — UX9-F Espace Live Director

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **26/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorWorkspaceProfile` persistant et ouvre
trois vues humaines : client/Creator, spawn spatial, puis Chooser/focus. L'humain choisit
explicitement le module, règle les champs, prévisualise la commande canonique, approuve cette seule
révision, applique, annule/rétablit, enregistre puis confirme une sandbox locale distincte en rôle
Administrateur. Le fichier `.forge/live-director/workspace.json` n'est créé que sur
**Enregistrer** et l'écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-001"></a>
- **ORF-DM-001 — Client maître de jeu séparé** : client indépendant du moteur, compte à rôle,
  second facteur MFA obligatoire, expiration configurable, session révocable et auditée. La sonde
  crée un compte temporaire, refuse l'absence de MFA, ouvre le client puis révoque la session.

<a id="orf-dm-003"></a>
- **ORF-DM-003 — Vue Creator** : recherche, package, type et coût maximal sont éditables. Le vrai
  `LiveCatalogService` applique aussi allowlist serveur, zone, permission et miniature ; une entrée
  interdite n'apparaît pas.

<a id="orf-dm-004"></a>
- **ORF-DM-004 — Création d'une ou plusieurs instances** : quantité, mode single/formation/scatter,
  instance, origine XYZ et espacement sont directs. `LiveSpawnService` conserve les mêmes IDs entre
  aperçus, construit les coordonnées avant mutation, applique le lot atomiquement et offre une
  compensation permissionnée qui retire tous les objets du lot.

<a id="orf-dm-005"></a>
- **ORF-DM-005 — Vue Chooser** : recherche hiérarchique zone/instance/type/texte/état, diagnostics
  et confidentialité. Les entrées privées sont masquées par défaut ; l'override Administrateur est
  explicite et vérifié par `AuthorizationService`.

<a id="orf-dm-006"></a>
- **ORF-DM-006 — Aller à un objet** : focus spectateur sur une cible d'une autre zone, avec override
  privé permissionné et audit. L'adaptateur `LiveWorldActionService` prouve que le focus conserve
  intégralement position, instance et état de la cible.

## Transaction, permissions et préservation

- `LiveDirectorWorkspaceHistory` impose Maître de jeu/Administrateur, dry-run, révision attendue et
  progression d'une seule révision ;
- l'approbation est liée à l'empreinte du brouillon et de la source, puis révoquée dès qu'un champ
  ou la révision change ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- la sandbox d'application exige une confirmation distincte et refuse tout rôle autre
  qu'Administrateur ;
- le profil charge les vrais services d'authentification, client, catalogue, spawn, Chooser et
  actions monde ; le spawn est compensé et la session est révoquée avant la sortie ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont préservés.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **12/12** — LiveDirector 7, ModuleFormat 3 et MCP 2 ; régression complète
  `Forge.LiveDirector.Tests` : **54/54** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_F_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, client,
  Creator, spawn, Chooser, focus, audit, UI, préservation, restauration et libération à `true` ;
- trois captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées : les cinq cartes, trois
  workflows, champs, limites, approbations, preuve et six actions sont lisibles, sans UUID,
  chevauchement ni clipping bloquant ;
- portail incrémental : **PASS en 549,52 s** — build 1,17 s, tests 2,36 + 2,66 + 2,68 s,
  smoke 424,17 s, capture 8,89 s, matrice 0,97 s, génération du graphe 53,93 s et
  `check --full` 52,64 s ;
- matrice Studio après promotion : **658 `functional` / 59 `planned`**, dont UX9 **26/68** ;
- graphe Roslyn officiel : **11 367 nœuds / 87 994 relations / 0 diagnostic**, frais et pleinement
  déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-f-client-creator-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-f-spawn-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-f-chooser-focus-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-f-live-director-workspace-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-f-live-director-workspace.summary.json`.

## Limites honnêtes

Studio n'ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise les autorités réelles avec des comptes, zones, blueprints et objets synthétiques en mémoire,
puis révoque et compense tout ; elle ne dirige pas une partie réelle et ne qualifie ni transport
inter-processus, latence réseau, caméra runtime rendue, charge multi-client ou sécurité d'un service
exposé. L'ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Les actions destructives, contrôle de groupe,
recherche avancée, scripts, scènes et narration restent hors du lot R125.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorWorkspaceProfileTests|' +
  'FullyQualifiedName~LiveDirectorFoundationTests|' +
  'FullyQualifiedName~WorkspaceLiveDirectorStoreTests|' +
  'FullyQualifiedName~LiveDirectorWorkspaceAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-f-live-director-workspace `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-f-live-director-workspace-forward-plus-fhd.png' `
  -CheckGraph
```
