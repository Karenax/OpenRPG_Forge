# Acceptation Studio — UX9-I Groupes Live Director

État local au 23 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **41/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorGroupProfile` persistant et ouvre
trois vues humaines : trigger/sélection, déplacement/attaque, puis voyage vers un joueur. L’humain
choisit explicitement le module, règle le plafond et la sonde, prévisualise la commande canonique,
approuve cette seule révision, applique, annule/rétablit, enregistre puis confirme une sandbox
locale distincte en rôle Administrateur. Le fichier `.forge/live-director/group.json` n’est créé
que sur **Enregistrer** et l’écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-020"></a>
- **ORF-DM-020 — Déclencher une entrée ou sortie de trigger** : événement explicite, acteur
  obligatoire, payload lisible et trace ForgeScript en trois étapes. L’aperçu et l’application
  utilisent le vrai `LiveProgressionWorldService` sans modifier le trigger synthétique.

<a id="orf-dm-021"></a>
- **ORF-DM-021 — Sélection multiple** : lasso représenté par une recherche humaine, filtrage par
  permissions, plafond de 1 à 1 024 objets et identifiant stable. La preuve sélectionne exactement
  une garde mobile et une garde bloquée sans exposer leur UUID.

<a id="orf-dm-023"></a>
- **ORF-DM-023 — Ordonner le déplacement d’un groupe** : formations ligne, coin, cercle ou libre,
  aperçu du chemin, adaptateur de pathfinding et rapport d’échec partiel. La garde mobile rejoint
  la destination tandis que la garde bloquée conserve un diagnostic localisé.

<a id="orf-dm-024"></a>
- **ORF-DM-024 — Ordonner l’attaque d’un groupe** : avertissement de sécurité de faction
  obligatoire, cible explicite, objectif autoritaire et adaptateur combat. L’application est
  refusée sans acquittement, puis les états synthétiques sont restaurés.

<a id="orf-dm-026"></a>
- **ORF-DM-026 — Voyager vers un joueur** : observer, téléporter le MJ, faire venir un joueur ou
  rappeler son groupe. Confirmation et consentement collectif sont distincts ; l’aperçu annonce la
  transition caméra et la sandbox restitue toutes les positions.

## Transaction, permissions et préservation

- `LiveDirectorGroupHistory` accepte Maître de jeu ou Administrateur, impose dry-run, révision
  attendue et progression d’une seule révision ; la sandbox Studio exige Administrateur et une
  confirmation distincte ;
- l’approbation est liée aux empreintes du brouillon et de la source, puis révoquée dès qu’un champ
  ou la révision change ;
- la validation refuse profil nul, formation divergente de l’adaptateur, collections non
  canoniques, plafond hors bornes ou capacités runtime manquantes ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- `LiveDirectorGroupRuntimeAdapter` charge les vrais `AuthorizationService`, `LiveWorldChooser`,
  `LiveProgressionWorldService`, `LiveGroupCommandService` et `LivePlayerService` ;
- `LiveDirectorGroupScriptService` expose un handle borné et le service MCP couvre lecture filtrée,
  plan, apply et revert sous rôle et portée serveur ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont
  préservés, puis le workspace temporaire est supprimé.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **13/13** — LiveDirector 8, ModuleFormat 3 et MCP 2 ; régression complète
  `Forge.LiveDirector.Tests` : **58/58** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_I_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, consentement du groupe, dry-run, commande, gardes stale, undo/redo,
  persistance, trigger, multi-sélection, déplacement, attaque, voyage, compensation, audit, UI,
  préservation, restauration et libération à `true` ;
- trois captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées après correction du repli des
  cartes de routes : cinq cartes, trois workflows, champs, consentements, aperçu, audit et six
  actions sont visibles, sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 517,61 s** — build 1,26 s, tests 2,22 + 2,49 + 2,70 s,
  smoke 399,52 s, capture 8,17 s, matrice 0,81 s, génération du graphe 50,25 s et
  `check --full` 50,13 s ;
- matrice Studio après promotion : **673 `functional` / 44 `planned`**, dont UX9 **41/68** ;
- graphe Roslyn officiel final : **11 438 nœuds / 88 669 relations / 0 diagnostic**, frais et
  pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-i-trigger-selection-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-i-movement-attack-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-i-player-travel-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-i-live-director-group-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-i-live-director-group.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise les autorités réelles avec trigger, groupes, cible, joueurs et positions synthétiques en
mémoire, puis restaure les entrées et libère le workspace temporaire ; elle ne dirige pas une
partie réelle et ne qualifie ni navmesh 3D rendu, IA de combat, dégâts, faction d’un module réel,
caméra `Camera3D`, transport inter-processus ou sécurité d’un service exposé. La transition caméra
est un résultat d’adaptateur vérifiable, pas un rendu de jeu. L’ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Raccourcis de groupes, liste complète des joueurs,
recherche, scripts, variables, scènes et narration restent hors du lot R128.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorGroupProfileTests|' +
  'FullyQualifiedName~LiveDirectorRulesGroupTests|' +
  'FullyQualifiedName~Group_attack_requires_faction_warning|' +
  'FullyQualifiedName~Player_panel_and_confirmed_travel|' +
  'FullyQualifiedName~WorkspaceLiveDirectorGroupStoreTests|' +
  'FullyQualifiedName~LiveDirectorGroupAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-i-live-director-group `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-i-live-director-group-forward-plus-fhd.png' `
  -CheckGraph
```
