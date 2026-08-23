# Acceptation Studio — UX6-P Interop éditeur/MCP ForgeScript

État local au 21 août 2026 : **fonctionnel et qualifié localement** pour `ORF-SCR-041`. UX6
atteint **62/84** ; l’incrément reste local (`full_gate=false`).

La page **ForgeScript · Interop éditeur/MCP R101** conserve l’unique source canonique UX6-F.
Elle persiste uniquement le profil R101 versionné ; les plans source et liaison sont exercés via le
service MCP existant dans une copie temporaire, puis l’adaptateur RPG Kit relit exactement la même
source. Il n’existe ni second éditeur, ni voie de mutation directe, ni fichier source parallèle.

<a id="orf-scr-041"></a>
## ORF-SCR-041 — Interop éditeur/MCP

L’auteur sélectionne explicitement une source `.as`, règle les quotas de sources, octets et sandbox,
puis prépare une liaison humaine vers un composant, un événement, un handler, un payload, une
priorité, une condition et une autorité. Les valeurs exactes ne sont jamais clampées silencieusement.

L’aperçu sec valide le profil avec `ForgeScriptInteropHistory`, planifie puis applique la source dans
un `ForgeScriptInteropService` isolé et demande le plan de liaison typée. La simulation exerce :

- API versionnée, create/update, compile, diagnostics et tests sandbox ;
- preview/apply/revert/redo des plans source et liaison avec révisions ;
- export/restauration d’un snapshot exact sources + bindings + profil + révision ;
- résolution optionnelle/requise, handle et exécution native via l’adaptateur RPG Kit ;
- interruption d’un script abusif par quota puis reprise saine dans le même runtime.

Seul le profil interop est appliqué au workspace, avec approbation liée à la révision et aux
empreintes du profil et de la source, écriture atomique, undo/redo et sauvegarde/réouverture.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **10/10** — profil Scripting **2**, service MCP **6**, adaptateur RPG Kit **2** ;
- portail incrémental : **135,87 s**, build, trois projets de tests, smoke global, capture Forward+
  FHD et contrôle des 717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_P_PASS` : sélection explicite, profil canonique, API, transactions
  source et binding, compilation, tests, snapshot, adaptateur, sandbox native, quota/récupération,
  profils/source/liaison invalides, dry-run, source et approbation obsolètes, apply/undo/redo,
  persistance, préservation, transaction UI, isolat libéré, source inchangée et surface restaurée ;
- captures Forward+ FHD inspectées :
  `artifacts/verification/studio-increment/ux6-p-script-interop-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux6-p-binding-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux6-p-transactions-forward-plus-fhd.png` ; profil,
  liaison, transactions, diagnostics, résumé et actions restent lisibles sans clipping bloquant ni
  UUID visible ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-p-script-interop.summary.json`.

## Préservation et limites honnêtes

La preuve ouvre une copie temporaire du module et de la source gouvernée, compare les octets source
et métadonnées, projet, historique, révision, journal, état dirty, sélection et approbations, puis
supprime l’isolat. Les plans MCP ne touchent jamais le workspace source ; seule l’application
explicite du profil écrit `forge-script-interop.json`.

La sandbox compile et exécute réellement le script copié, mais Studio ne pilote aucun acteur, bus
d’événements live, objet Godot, fichier arbitraire, réseau, processus ou objet .NET depuis MCP. La
cible de liaison est un contexte d’auteur pour le plan isolé, pas une mutation d’entité gameplay.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptInteropProfileTests|' +
  'FullyQualifiedName~ForgeScriptInteropServiceTests|' +
  'FullyQualifiedName~ForgeScriptInteropRuntimeAdapterTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-p-script-interop -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-p-script-interop-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page scripts -Evidence ux6-p-binding `
  -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-p-binding-forward-plus-fhd.png `
  -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Page scripts -Evidence ux6-p-transactions `
  -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-p-transactions-forward-plus-fhd.png `
  -Wait -SkipBuild
```
