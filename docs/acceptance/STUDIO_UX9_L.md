# Acceptation Studio — UX9-L Accès et supervision Live Director

État local au 23 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **53/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorAccessProfile` persistant dans une
surface humaine unique : authentification, inspection, liste des joueurs, audit et chat MJ.
L’humain choisit explicitement le module, règle uniquement les bornes sûres, prévisualise la
commande canonique, approuve cette seule révision, applique, annule/rétablit, enregistre puis
confirme une sandbox locale distincte en rôle Administrateur. Le fichier
`.forge/live-director/access.json` n’est créé que sur **Enregistrer** et l’écriture atomique utilise
UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-002"></a>
- **ORF-DM-002 — Authentification MJ** : rôle Administrateur, second facteur par adaptateur nommé,
  expiration bornée, révocation et audit de chaque décision. La sandbox refuse un facteur erroné,
  accepte le facteur synthétique attendu et vérifie la session active.

<a id="orf-dm-011"></a>
- **ORF-DM-011 — Examiner** : composants, statistiques, variables, scripts, propriété et historique
  sont exposés en six sections de lecture seule. La preuve inspecte un objet synthétique sans le
  muter et conserve une limite d’historique réglable.

<a id="orf-dm-025"></a>
- **ORF-DM-025 — Liste des joueurs** : identité, personnage, zone, latence, groupe, permissions,
  voix et modération restent visibles. Le rafraîchissement borné passe par l’adaptateur de latence
  autoritaire et la preuve obtient un joueur à 37 ms.

<a id="orf-dm-031"></a>
- **ORF-DM-031 — Journal d’audit MJ** : chaîne SHA-256 immuable, rétention et volume bornés,
  before/after, motif et corrélation obligatoires. Le scénario produit trois entrées et recalcule la
  chaîne complète avant d’annoncer le résultat.

<a id="orf-dm-034"></a>
- **ORF-DM-034 — Chat et canaux MJ** : sept canaux, longueur bornée, modération, logs immuables et
  motif obligatoire pour l’incarnation. La preuve écrit deux messages synthétiques et termine sur
  le canal d’incarnation.

## Transaction, permissions et préservation

- `LiveDirectorAccessHistory` exige Administrateur et portée serveur, impose dry-run, révision
  attendue et progression d’une seule révision ; la sandbox Studio exige une confirmation distincte ;
- l’approbation est liée aux empreintes du brouillon et de la source, puis révoquée dès qu’un champ
  ou la révision change ;
- les bornes UI et le validateur refusent session, historique, rafraîchissement, rétention ou
  longueur hors plage, ainsi que toute suppression d’un garde-fou obligatoire ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- `LiveDirectorAccessRuntimeAdapter` et `LiveDirectorAccessAcceptanceScenario` exercent les vraies
  autorités d’authentification, inspection, joueurs, audit et chat sur des données synthétiques ;
- `LiveDirectorAccessScriptService` expose cinq handles typés et le service MCP couvre lecture
  filtrée, plan, apply, revert et redo sous rôle et portée serveur ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont
  préservés, puis le workspace temporaire est supprimé.

## Preuves exécutées

- builds `Forge.Studio` Debug et Release : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **13/13** — LiveDirector 7, ModuleFormat 3 et MCP 3 ; régression complète
  `Forge.LiveDirector.Tests` : **58/58** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_L_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance,
  authentification, inspection, joueurs, audit, chat, scripts, UI, préservation, restauration et
  libération à `true` ;
- capture dédiée et capture du portail Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées après
  correction du sous-titre : cinq cartes, cinq bornes, deux confirmations, aperçu, audit, bouton
  **Fermer** et six actions sont visibles, sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 503,26 s** — build 1,09 s, tests 2,17 + 2,38 + 2,50 s,
  smoke 388,98 s, capture 8,14 s, matrice 0,78 s, génération du graphe 49,51 s et
  `check --full` 47,65 s ;
- matrice Studio après promotion : **685 `functional` / 32 `planned`**, dont UX9 **53/68** ;
- graphe Roslyn officiel final : **11 502 nœuds / 89 296 relations / 0 diagnostic**, frais et
  pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-l-live-director-access-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-l-live-director-access.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise un compte, un facteur, un objet, un joueur et des messages synthétiques en mémoire, puis
libère le workspace temporaire ; elle ne dirige pas une partie réelle et ne qualifie ni fournisseur
MFA public, annuaire de comptes, transport chat/voix, SIEM, archive externe, réplication réseau ou
sécurité d’un service exposé. Les adaptateurs MFA, latence, voix et sink d’audit restent des
frontières nommées. L’ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Repos, niveaux, apparence, raccourcis de groupes,
enregistrement et routes réseau avancées restent hors de UX9-L.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorAccessProfileTests|' +
  'FullyQualifiedName~WorkspaceLiveDirectorAccessStoreTests|' +
  'FullyQualifiedName~LiveDirectorAccessAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-l-live-director-access `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-l-live-director-access-forward-plus-fhd.png' `
  -CheckGraph
```
