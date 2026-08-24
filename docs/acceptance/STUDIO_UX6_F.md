# Acceptation Studio — UX6-F Fondation ForgeScript

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour cinq routes ForgeScript.
UX6 atteint **26/84** ; le portail de ce lot reste incrémental (`full_gate=false`).

La page **ForgeScript · fondation de source** fournit un atelier direct dans l’unique fenêtre Studio.
Elle distingue les sources canoniques `.as` gouvernées des scripts historiques `.forge`, qui restent
visibles mais protégés. Une mutation de source passe par `ForgeScriptSourceHistory`, puis
`ForgeScriptSourceStore` écrit atomiquement le couple `.as` + `.meta.json`. Le projet et son
`ProjectCommandHistory` ne sont pas réécrits par cette transaction documentaire séparée.

<a id="orf-scr-001"></a>
## ORF-SCR-001 — Langage proche du C

Le `CodeEdit` expose AngelScript 2.38 avec numéros de ligne, coloration C-like, fonctions typées et
diagnostics localisés. Le texte source reste l’autorité ; Studio ne génère pas un graphe ou un JSON
à sa place et ne normalise pas silencieusement une source existante.

<a id="orf-scr-002"></a>
## ORF-SCR-002 — Sources et bytecode

Le chemin relatif sûr, l’API, la distribution, le caractère requis, les dépendances et les bindings
sont persistés dans la métadonnée versionnée. Le mode `Source + bytecode` produit un artefact dérivé
dans le cache ; les scripts `.forge` Crypte des Cendres restent en lecture seule et ne sont jamais
migrés ou écrasés par l’atelier.

<a id="orf-scr-003"></a>
## ORF-SCR-003 — Compilateur intégré

`NativeForgeScriptRuntime` et `ForgeScriptArtifactCache` compilent la copie en mémoire et rapportent
VM, API, taille du bytecode, diagnostics et réutilisation du cache. Compiler ne modifie pas la
source. La transaction d’enregistrement exige d’abord un dry-run compilé et une approbation encore
liée à la révision, au brouillon et à l’empreinte disque.

<a id="orf-scr-006"></a>
## ORF-SCR-006 — Événements par objet

La collection ordonnée de handlers expose composant, fonction, priorité, condition, autorité et
activation tout en protégeant les identités stables. Ce lot branche le contrat canonique existant
`creature.damaged` avec charge `int` et autorité serveur ; plusieurs handlers et leurs priorités
sont conservés. Les autres catalogues d’événements restent hors de cette fondation.

<a id="orf-scr-007"></a>
## ORF-SCR-007 — Fonction d’entrée

La fonction `void main()` est éditée et compilée dans la même source que les entrées typées des
bindings. Le test sandbox exécute le bytecode avec quotas déterministes et sans exposer fichier,
réseau, processus, moteur Godot ou API .NET.

## Preuves

- build Debug `Forge.Studio` final : **0 avertissement, 0 erreur** ;
- portail incrémental avec graphe : **284,26 s**, **34/34** tests ciblés — Forge.Scripting 25,
  ModuleFormat 9 — puis smoke global, capture Forward+ FHD et contrôle de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_F_PASS` : sélection explicite, source canonique, legacy protégé,
  bindings typés, `main`, chemins/handlers invalides bloqués, dry-run, compilation, cache bytecode,
  sandbox et quota, empreinte disque/approbation obsolètes bloquées, apply/undo/redo, persistance,
  voisins projet, transaction UI isolée, ressources libérées, source et surface restaurées ;
- capture corrigée puis inspectée :
  `artifacts/verification/studio-increment/ux6-f-script-foundation-forward-plus-fhd.png`,
  1920×1080, Godot 4.7.1 .NET, Vulkan Forward+, sélection/source/éditeur/bindings/diagnostics et six
  actions visibles sans clipping, UUID ni fenêtre native supplémentaire ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-f-script-foundation.summary.json`.

## Préservation et limites honnêtes

Les preuves copient Crypte des Cendres et ses deux sources historiques `.forge` dans un workspace
temporaire, y ajoutent une seule source `.as` canonique, puis comparent projet, historique projet,
révision, journal, dirty, zone active et approbation avant/après. L’isolat et son cache sont supprimés.
Le smoke altère aussi le fichier source hors Studio afin de prouver que l’empreinte disque bloque une
approbation devenue obsolète.

Ce lot ne revendique pas encore l’assistant de scripts, la complétion API, la navigation symbolique,
le formatage, le débogage pas à pas, les points d’arrêt, le hot reload, les permissions de module ou
les autres contrats d’événements. Il ne migre pas les `.forge`. Le sandbox exécute réellement le
bytecode natif, mais aucun objet gameplay, événement moteur, réseau ou fichier du module n’est mis à
disposition ; les routes ForgeScript restantes demeurent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptSourceDocumentTests|' +
  'FullyQualifiedName~ForgeScriptArtifactCacheTests|' +
  'FullyQualifiedName~ForgeScriptCompilerServiceTests|' +
  'FullyQualifiedName~NativeForgeScriptRuntimeTests|' +
  'FullyQualifiedName~WorkspaceBuildGraphTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts -Evidence ux6-f-script-foundation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-f-script-foundation-forward-plus-fhd.png
```
