# Acceptation Studio — UX6-K Permissions ForgeScript par module

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour `ORF-SCR-039`, route P0
du lot R015. UX6 atteint **47/84** ; l’incrément reste local (`full_gate=false`).

La page **ForgeScript · Permissions par module R015** édite le manifeste versionné
`scripts/forge-script-permissions.json` sans afficher de JSON, UUID ou pointeur. La source
canonique UX6-F reste en lecture seule. Le module possède une allowlist explicite ; chaque kit est
un sous-ensemble de cette allowlist et chaque liaison d’événement est à son tour un sous-ensemble
du kit choisi. Deny-by-default et l’autorité serveur restent protégés.

<a id="orf-scr-039"></a>
## ORF-SCR-039 — Permissions par module

Les six familles du manifeste ForgeScript sont présentées par libellé humain avec autorité, quota
et API : zones/objets, mouvement, math/chaînes, variables locales, handles opaques et
aléatoire/horloge de simulation. L’auteur peut :

- autoriser ou refuser explicitement chaque famille au niveau du module ;
- créer, retirer et réordonner des kits à identité interne stable ;
- créer, retirer et réordonner des liaisons événement/handler vers un kit ;
- régler la version d’API, l’exigence de signature, l’exception de développement explicite et le
  caractère obligatoire du manifeste ;
- prévisualiser le diff humain, appliquer, annuler, rétablir et enregistrer sans modifier la source
  ForgeScript ni le projet.

La simulation passe par `ForgeScriptModuleAuthorizationService` et
`ForgeScriptModuleExecutionService`. Elle prouve l’intersection exacte module/kit/liaison,
l’exécution d’un serveur fiable, le refus d’une capacité non déclarée, d’une signature non fiable,
d’une autorité locale et d’un autre module, les contrats manifeste optionnel/obligatoire, puis
l’arrêt d’un script abusif par quota et l’exécution saine suivante.

## Preuves

- build Debug final `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **14/14** — Scripting R015 **6**, packaging ModuleFormat **1**, exposition MCP
  **7** ;
- portail incrémental final : **159,24 s**, build, 14/14 tests, smoke global, capture Forward+ FHD
  et contrôle des 717 lignes de matrice ;
- smoke global `OPENRPG_FORGE_STUDIO_UX6_K_PASS` : sélection explicite, six capacités humaines,
  manifeste canonique, deny-by-default, serveur, signature, intersection, autorité/sandbox,
  invalidités et approbation périmée bloquées, dry-run, apply/undo/redo, persistance, source et
  voisins préservés, transaction UI, isolat libéré, source et surface restaurées ;
- capture principale Forward+ FHD inspectée :
  `artifacts/verification/studio-increment/ux6-k-script-module-permissions-forward-plus-fhd.png`,
  1920×1080, source revue, six familles, onglets Module/Kits/Liaisons, diff humain, console et six
  actions transactionnelles visibles sans clipping ni UUID ;
- captures complémentaires inspectées : `ux6-k-kits-forward-plus-fhd.png` et
  `ux6-k-bindings-forward-plus-fhd.png`, 2560×1369, champs, capacités, ordre et actions visibles ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-k-script-module-permissions.summary.json`.

## Préservation et limites honnêtes

La preuve copie Crypte des Cendres et la source UX6-F dans un workspace temporaire. Elle compare le
projet sérialisé complet, l’historique, la révision, le journal, l’état dirty, la zone active, les
approbations UX6-F/J/K et les octets du manifeste R015 source avant/après, puis supprime l’isolat.
Le packageur refuse aussi un manifeste appartenant à un autre module avant l’export.

La sandbox native compile et exécute réellement la source de preuve et applique réellement le
quota. Studio ne branche cependant pas cette simulation dans un acteur Godot, une session de jeu
ou un transport réseau. La signature est une décision d’autorisation à partir du statut fiable
fourni par l’appelant ; Studio ne signe aucun package et ne remplace pas la future qualification de
chaîne de distribution.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptModulePermissionPolicyTests|' +
  'FullyQualifiedName~Export_includes_valid_module_script_permissions_and_rejects_wrong_module_before_build|' +
  'FullyQualifiedName~ForgeScriptAgentServiceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-k-script-module-permissions -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-k-script-module-permissions-forward-plus-fhd.png
```
