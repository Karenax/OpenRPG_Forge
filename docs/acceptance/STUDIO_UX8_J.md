# Acceptation Studio — UX8-J Sécurité des workers et de l’Assistant

État local au 22 août 2026 : **fonctionnel et qualifié** pour deux routes UX8. La vague atteint
ainsi **40/51** routes accessibles dans Studio.

La page `security` sépare volontairement deux autorités. La politique du worker externe passe par
`SetExternalWorkerSecurityPolicyHandler`, tandis que la protection des réponses Assistant passe
par `SetAssistantWorkspaceProfileHandler` et `AgentResponseSafetyService`. Chaque côté possède son
propre aperçu, consentement, plan, audit, historique et fichier persistant ; aucune atomicité
inter-profils n’est inventée.

## Routes couvertes

<a id="orf-ai-023"></a>
- **ORF-AI-023 — Pas d’exécution Python libre** : Python arbitraire, shell, `eval`, réseau et chemins
  hors workspace restent verrouillés sur refusé. La personne règle seulement la durée, la taille de
  sortie, la permission minimale et une liste blanche d’opérations Blender typées ; validation et
  export GLB restent obligatoires.

<a id="orf-ai-032"></a>
- **ORF-AI-032 — Protection contre prompt injection** : les contenus du projet sont rendus comme
  données non fiables avec provenance. Une sonde locale introduit une instruction hostile, un outil
  `shell.execute` et une élévation de permission ; le signal, l’outil et l’escalade doivent tous être
  refusés avant qu’une application soit proposée.

## Transaction, consentement et préservation

- sélection explicite du module réel ;
- valeurs dangereuses verrouillées, sans faux bouton permettant de les réactiver ;
- consentement humain obligatoire après un aperçu sec réussi ;
- révision, empreinte complète de la source et empreinte du brouillon approuvé vérifiées à nouveau
  lors de l’application ;
- dry-run puis application par les deux handlers canoniques, avec audit appliqué ;
- undo, redo, sauvegarde atomique et réouverture d’un workspace temporaire ;
- restauration exacte de l’absence initiale des fichiers lors de l’undo ;
- fournisseurs, prompts, gouvernance, outils, tests Assistant et profils voisins préservés ;
- projet, historique, révision, journal, dirty, sélection et approbations source inchangés ;
- répertoire temporaire borné supprimé avant PASS.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **35/35** — Domain 5, Application 14, ModuleFormat 15 et MCP 1 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX8_J_PASS` avec routes, sélection, scope,
  consentement, refus Python/shell/eval/réseau, injection, outil/escalade, dry-run, commandes, audit,
  gardes stale, undo/redo, persistance, voisins, nettoyage, UI humaine, source inchangée et surface
  restaurée à `true` ;
- capture Forward+ **1920×1080** inspectée : Crypte des Cendres réel, deux colonnes lisibles, valeurs
  sûres verrouillées, listes et signatures hostiles, diagnostics, deux consentements et dix actions
  visibles sans UUID ni clipping ;
- portail incrémental : **PASS en 472,65 s** — build 1,73 s, tests 1,91 + 3,10 + 3,46 + 5,79 s,
  smoke 446,45 s, capture 9,28 s et matrice 0,86 s ;
- matrice Studio après promotion : **620 `functional` / 97 `planned`**, dont UX8 **40/51** ;
- graphe Roslyn final régénéré par l’outil officiel, frais, déterministe et sans diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-j-security-workers-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-j-security-workers.summary.json`.

## Limites honnêtes

La surface ne lance ni Python, Blender, shell, réseau, fournisseur IA ni outil sémantique. La sonde
anti-injection exerce les parseurs et validateurs canoniques entièrement en mémoire. Le smoke
applique les deux profils seulement dans un workspace jetable ; le module source reste inchangé.
Cette preuve ne certifie pas un sandbox de système d’exploitation ou un fournisseur distant.

Le portail conserve `full_gate=false` et `graph_check=false` ; le contrôle Roslyn est exécuté
séparément après la documentation. Cette qualification locale ne remplace pas `scripts/Verify.ps1`
ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ExternalWorkerSecurityPolicyTests|' +
  'FullyQualifiedName~AgentResponseSafetyProfileTests|' +
  'FullyQualifiedName~ExternalWorkerSecurityPolicyCommandTests|' +
  'FullyQualifiedName~AssistantWorkspaceProfileCommandTests|' +
  'FullyQualifiedName~AgentResponseSafetyServiceTests|' +
  'FullyQualifiedName~ExternalWorkerSecurityPolicyStoreTests|' +
  'FullyQualifiedName~AssistantWorkspaceProfileStoreTests|' +
  'FullyQualifiedName~BlenderWorkerRequestStoreTests|' +
  'FullyQualifiedName~WorkerSecurityMcpRequiresPlanPermissionAndPersistsThenRevertsCanonicalPolicy'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page security `
  -Evidence ux8-j-security-workers `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts/verification/studio-increment/ux8-j-security-workers-forward-plus-fhd.png'
```
