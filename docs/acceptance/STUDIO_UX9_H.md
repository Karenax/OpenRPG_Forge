# Acceptation Studio — UX9-H Autorité Live Director

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **36/68** routes accessibles dans Studio.

La page `network-director` édite directement un `LiveDirectorAuthorityProfile` persistant et ouvre
trois vues humaines : incarnation/protection, or/expérience, puis portes/portails. L’humain choisit
explicitement le module, règle les plafonds et la sonde, prévisualise la commande canonique,
approuve cette seule révision, applique, annule/rétablit, enregistre puis confirme une sandbox
locale distincte en rôle Administrateur. Le fichier `.forge/live-director/authority.json` n’est
créé que sur **Enregistrer** et l’écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-dm-013"></a>
- **ORF-DM-013 — Impersonner une créature** : incarnation par chat, dialogue, voix ou animation,
  divulgation explicitement visible ou bornée par politique et adaptateur de rendu correspondant.
  La sonde expire puis restitue le canal et l’état de contrôle initiaux.

<a id="orf-dm-014"></a>
- **ORF-DM-014 — Invulnérabilité GM** : durée plafonnée à 1 440 minutes, portée dégâts, effets ou
  tout, indicateur visible obligatoire et adaptateur de dégâts réel. L’expiration restaure l’état
  de protection antérieur et le journal conserve la décision.

<a id="orf-dm-016"></a>
- **ORF-DM-016 — Donner ou retirer de l’or** : plafond d’ajustement, plafond du portefeuille,
  raison obligatoire et refus d’un solde négatif. L’aperçu n’écrit rien ; l’application, la
  compensation et le round-trip du portefeuille persistant sont vérifiés séparément.

<a id="orf-dm-017"></a>
- **ORF-DM-017 — Donner ou retirer de l’expérience** : plafond d’ajustement, raison obligatoire,
  projection de niveau par le ruleset et progression persistante. La sandbox applique puis
  compense l’expérience et compare l’état exporté/réimporté.

<a id="orf-dm-019"></a>
- **ORF-DM-019 — Verrouiller ou déverrouiller une porte** : états verrouillé, déverrouillé,
  ouvert, réparé et relié à un portail. Les adaptateurs de collision et de portail sont exercés,
  puis la compensation restaure l’état verrouillé initial.

## Transaction, permissions et préservation

- `LiveDirectorAuthorityHistory` accepte Maître de jeu ou Administrateur, impose dry-run, révision
  attendue et progression d’une seule révision ; la sonde Studio exige Administrateur et une
  confirmation distincte ;
- l’approbation est liée aux empreintes du brouillon et de la source, puis révoquée dès qu’un champ
  ou la révision change ;
- la validation refuse politique nulle, plafonds incohérents, adaptateurs absents et collections
  non canoniques avant toute application ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- `LiveDirectorAuthorityRuntimeAdapter` charge les vraies autorités de contrôle, économie,
  progression et monde ; `LiveDirectorAuthorityScriptService` expose un handle borné ;
- le service MCP couvre lecture filtrée, plan, apply et revert sous rôle et portée serveur ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont
  préservés, puis le workspace temporaire est supprimé.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **18/18** — LiveDirector 13, ModuleFormat 3 et MCP 2 ; régression complète
  `Forge.LiveDirector.Tests` : **58/58** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_H_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, incarnation,
  protection, or, expérience, porte, compensation, audit, UI, préservation, restauration et
  libération à `true` ;
- trois captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées après correction de la
  hauteur de l’éditeur : les cinq cartes, trois workflows, champs, approbations, aperçu, audit et
  six actions sont visibles, sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 512,27 s** — build 1,22 s, tests 2,20 + 2,49 + 2,63 s,
  smoke 396,69 s, capture 8,16 s, matrice 0,87 s, génération du graphe 48,36 s et
  `check --full` 49,59 s ;
- matrice Studio après promotion : **668 `functional` / 49 `planned`**, dont UX9 **36/68** ;
- graphe Roslyn officiel final : **11 416 nœuds / 88 452 relations / 0 diagnostic**, frais et
  pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-h-impersonation-protection-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-h-currency-experience-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-h-door-portal-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-h-live-director-authority-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-h-live-director-authority.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise les autorités réelles avec joueurs, portefeuilles, progression et porte synthétiques en
mémoire, puis compense toutes les opérations et libère le workspace temporaire ; elle ne dirige
pas une partie réelle et ne qualifie ni transport inter-processus, stockage distant, économie d’un
serveur persistant, collision 3D rendue, portail inter-module réel ou sécurité d’un service exposé.
L’ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Repos forcé, niveau direct, triggers, sélection et
ordres de groupe, scènes et narration restent hors du lot R127.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveDirectorAuthorityProfileTests|' +
  'FullyQualifiedName~LiveDirectorControlTests|' +
  'FullyQualifiedName~LiveDirectorRulesGroupTests|' +
  'FullyQualifiedName~WorkspaceLiveDirectorAuthorityStoreTests|' +
  'FullyQualifiedName~LiveDirectorAuthorityAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-h-live-director-authority `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-h-live-director-authority-forward-plus-fhd.png' `
  -CheckGraph
```
