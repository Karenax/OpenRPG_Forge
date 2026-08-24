# Acceptation Studio — UX9-G Actions Live Director

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **31/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorActionProfile` persistant et ouvre
trois vues humaines : téléportation/destruction, soin/limbo, puis possession/restitution. L'humain
choisit explicitement le module, règle les champs, prévisualise la commande canonique, approuve
cette seule révision, applique, annule/rétablit, enregistre puis confirme une sandbox locale
distincte en rôle Administrateur. Le fichier `.forge/live-director/actions.json` n'est créé que sur
**Enregistrer** et l'écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-007"></a>
- **ORF-DM-007 — Faire venir ou téléporter une cible** : destination contrôlée par la navigation,
  consentement d'un joueur protégé, politique cible/groupe explicite, aperçu sans mutation et
  conservation de l'orientation de la cible. La compensation permissionnée restaure zone,
  instance, position, orientation et état d'origine.

<a id="orf-dm-008"></a>
- **ORF-DM-008 — Tuer ou détruire** : cause et confirmation obligatoires, politique de butin,
  impact quête prévisualisé et limite de retour visible. Le retour est honnêtement borné à la
  session courante et refuse une compensation inconnue, périmée ou en conflit.

<a id="orf-dm-009"></a>
- **ORF-DM-009 — Soigner** : portée santé, ressources, effets ou tout, avec raison obligatoire et
  aperçu. Le vrai `LiveWorldActionService` applique l'adaptateur runtime puis restaure l'état
  précédent lors de la compensation de sandbox.

<a id="orf-dm-010"></a>
- **ORF-DM-010 — Limbo ou désactivation de l'IA** : états IA, mouvement, collision et visibilité
  explicitement choisis. L'objet runtime existant est modifié sans remplacement : identifiant,
  nom, balise et identité restent inchangés, puis son état initial est restauré.

<a id="orf-dm-012"></a>
- **ORF-DM-012 — Posséder une créature** : durée bornée par le profil, contexte de commandes,
  point de retour et consentement du joueur protégé. À l'expiration, le contrôle est libéré et
  l'entrée originale retrouve son état ; le runtime refuse toute durée supérieure au profil.

## Transaction, permissions et préservation

- `LiveDirectorActionHistory` impose Maître de jeu/Administrateur, dry-run, révision attendue et
  progression d'une seule révision ; `RequiredAtRuntime` appartient au diff sémantique ;
- l'approbation est liée aux empreintes du brouillon et de la source, puis révoquée dès qu'un champ
  ou la révision change ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- la sandbox d'application exige une confirmation distincte et refuse tout rôle autre
  qu'Administrateur ;
- `LiveDirectorActionRuntimeAdapter.Create` charge les vraies autorités `AuthorizationService`,
  `LiveWorldChooser`, `LiveWorldActionService` et `LiveDirectorControlService` ;
- chaque compensation vérifie permission, révision et absence de conflit ; la destruction est
  restaurable uniquement tant que son jeton de session est présent ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont préservés.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **18/18** — LiveDirector 13, ModuleFormat 3 et MCP 2 ; régression complète
  `Forge.LiveDirector.Tests` : **56/56** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_G_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, téléportation,
  destruction, soin, limbo, possession, compensation, audit, UI, préservation, restauration et
  libération à `true` ;
- trois captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées après correction de la
  hauteur du panneau : les cinq cartes, trois workflows, champs, limites, approbations, résumé et
  six actions sont visibles, sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 514,28 s** — build 1,16 s, tests 2,21 + 2,56 + 2,72 s,
  smoke 397,70 s, capture 8,14 s, matrice 0,82 s, génération du graphe 50,26 s et
  `check --full` 48,67 s ;
- matrice Studio après promotion : **663 `functional` / 54 `planned`**, dont UX9 **31/68** ;
- graphe Roslyn officiel : **11 391 nœuds / 88 214 relations / 0 diagnostic**, frais et pleinement
  déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-g-relocation-destruction-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-g-healing-limbo-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-g-possession-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-g-live-director-actions-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-g-live-director-actions.summary.json`.

## Limites honnêtes

Studio n'ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise les autorités réelles avec des zones et objets synthétiques en mémoire, puis compense les
actions et restitue la possession avant la sortie ; elle ne dirige pas une partie réelle et ne
qualifie ni transport inter-processus, comportement de groupe multi-client, intégration quête d'un
module réel, loot physique, navigation 3D rendue ou sécurité d'un service exposé. L'ancien Toolset
reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Impersonation, protection, économie, portes,
contrôle de groupe avancé, scènes et narration restent hors du lot R126.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorActionProfileTests|' +
  'FullyQualifiedName~LiveWorldActionTests|' +
  'FullyQualifiedName~LiveDirectorControlTests|' +
  'FullyQualifiedName~WorkspaceLiveDirectorActionStoreTests|' +
  'FullyQualifiedName~LiveDirectorActionAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-g-live-director-actions `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-g-live-director-actions-forward-plus-fhd.png' `
  -CheckGraph
```
