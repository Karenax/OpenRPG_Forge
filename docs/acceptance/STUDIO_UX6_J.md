# Acceptation Studio — UX6-J Sécurité, quotas, déterminisme et API ForgeScript

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour cinq routes ForgeScript
P0 du lot R014. UX6 atteint **46/84** ; l’incrément reste local (`full_gate=false`).

La page **ForgeScript · sécurité, budgets et compatibilité R014** conserve la source canonique
UX6-F en lecture seule et édite une politique versionnée séparée dans `forge-script-policy.json`.
L’aperçu passe par `ForgeScriptExecutionPolicyCommand` et `ForgeScriptExecutionPolicyHistory` ;
l’application, l’annulation, le rétablissement et l’enregistrement restent atomiques. Zone, objet et
ressource sont choisis par leurs libellés issus du module ouvert, sans UUID ni pointeur affiché.

<a id="orf-scr-028"></a>
## ORF-SCR-028 — Handles opaques

La simulation enregistre des handles typés `Entity`, `Area`, `Asset`, `Effect` et `MovementTask`.
Elle prouve la résolution du bon type, le refus d’un type erroné, l’invalidation après libération et
l’incrément de génération lors de la réutilisation d’un slot. Aucune adresse mémoire ni identité
technique du module n’est exposée dans l’interface.

<a id="orf-scr-029"></a>
## ORF-SCR-029 — Budget d’instructions

Les limites par handler, tick et module sont saisies comme entiers exacts et validées dans l’ordre
`handler ≤ tick ≤ module`. Une boucle abusive, le quota agrégé, le timeout et l’annulation sont
interrompus avec diagnostic ; un handler sain doit ensuite s’exécuter, ce qui prouve que la VM reste
utilisable après l’abus.

<a id="orf-scr-030"></a>
## ORF-SCR-030 — Budget mémoire

Les plafonds par contexte et module sont validés avant application. Le runtime natif mesure le pic
mémoire, refuse le contexte au-delà de son quota et exécute ensuite un handler sain. La télémétrie
reste une mesure de la sandbox de preuve, pas une promesse de mémoire disponible sur une machine
joueur.

<a id="orf-scr-031"></a>
## ORF-SCR-031 — Déterminisme

Graine, tick et temps de simulation sont injectés explicitement. Deux exécutions de la même source
avec la même politique produisent la même suite d’événements ; une graine différente produit une
suite différente. Aucun accès à l’horloge système ou à un RNG non gouverné n’est utilisé.

<a id="orf-scr-036"></a>
## ORF-SCR-036 — Version d’API

Les versions `1.0` et `1.0.0` négocient les capacités `opaque-handles` et `determinism`. La surface
présente la version sélectionnée, les dépréciations et les migrations ; une version non supportée ou
une capacité manquante bloque l’aperçu avant compilation et écriture.

## Preuves

- build Debug final `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **27/27** — Scripting R014/runtime natif **20**, exposition MCP **7** ;
- portail incrémental final : **156,85 s**, build, 27/27 tests, smoke global, capture Forward+ FHD
  et contrôle des 717 lignes de matrice ;
- graphe Roslyn régénéré et contrôlé intégralement : **10 488 nœuds**, **77 740 relations**,
  **0 diagnostic**, fraîcheur et déterminisme complets ;
- smoke global `OPENRPG_FORGE_STUDIO_UX6_J_PASS` : sélection explicite, catalogues humains,
  politique canonique, handles, quotas d’instructions et mémoire, replay déterministe, compatibilité
  API, dry-run, invalidités et approbation périmée bloquées, apply/undo/redo, persistance, source et
  voisins préservés, transaction UI, isolat libéré, source et surface restaurées ;
- capture Forward+ FHD inspectée :
  `artifacts/verification/studio-increment/ux6-j-script-safety-policy-forward-plus-fhd.png`,
  1920×1080, cinq onglets accessibles, textes repliés dans le panneau, source revue, sujets humains,
  console, diagnostics et six actions transactionnelles visibles sans clipping ni UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-j-script-safety-policy.summary.json`.

## Préservation et limites honnêtes

La preuve copie Crypte des Cendres et la source UX6-F dans un workspace temporaire, puis compare le
projet sérialisé complet, l’historique, la révision, le journal, l’état dirty, la zone active, les
approbations source/politique et les octets du fichier de politique avant/après. L’isolat est supprimé.

La sandbox native exécute réellement la source de sonde et applique réellement les quotas. Studio ne
branche cependant cette politique ni dans un acteur Godot, ni dans un serveur de partie, ni dans un
transport réseau. Aucun fichier arbitraire, socket, processus, pointeur, réflexion, API Godot ou API
.NET n’est rendu accessible au script. L’application écrit uniquement la politique R014 versionnée ;
la source `.as` et le projet restent inchangés.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptR014ContractTests|' +
  'FullyQualifiedName~NativeForgeScriptRuntimeTests|' +
  'FullyQualifiedName~ForgeScriptAgentServiceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-j-script-safety-policy -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-j-script-safety-policy-forward-plus-fhd.png
```
