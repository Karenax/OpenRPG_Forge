# Acceptation Studio — UX6-N Hot reload et débogueur ForgeScript

État local au 21 août 2026 : **fonctionnel et qualifié localement** pour `ORF-SCR-032` et
`ORF-SCR-033`. UX6 atteint **56/84** ; l’incrément reste local (`full_gate=false`).

La page **ForgeScript · Outils runtime R099** conserve l’unique source et l’historique UX6-F. Les
deux nouveaux onglets construisent un bloc versionné à relire, l’insèrent explicitement dans le
brouillon, puis partagent compilation, aperçu sec, approbation liée à la révision, écriture atomique,
undo/redo et sauvegarde/réouverture avec les outils audio, temps et événements custom UX6-L.

<a id="orf-scr-032"></a>
## ORF-SCR-032 — Hot reload

L’auteur renseigne module logique, versions active et candidate, empreintes de source, composant
d’état typé, dépendances et frontière de handler. La simulation R099 exerce report hors frontière,
migration compatible, invalidation des dépendances, rollback et stratégie de redémarrage ; une
version non croissante est refusée. L’intention native `hot_reload_request` est réellement compilée
et exécutée dans la sandbox copiée, mais aucun module de jeu live n’est remplacé.

<a id="orf-scr-033"></a>
## ORF-SCR-033 — Débogueur

La session expose autorité auteur local ou administrateur serveur, permission distante, breakpoint,
pile, locale, watch, inspection d’entité, trace d’événement et pas `into/over/out/continue`. Les
permissions invalides sont refusées, l’adaptateur RPG Kit et les intentions natives `debug_break` et
`debug_step` sont exercés. Studio ne contrôle aucun processus, acteur ou serveur distant.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **28/28** — Scripting **23**, adaptateur RPG Kit **5** ;
- portail incrémental : **131,98 s**, build, tests, smoke global, capture Forward+ FHD et contrôle
  des 717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_N_PASS` : sélection explicite, autorité source partagée, bloc revu,
  hot reload, débogueur, adaptateur, exécution native, refus version/permission, dry-run, compilation,
  approbation obsolète, apply/undo/redo, persistance, voisins, transaction UI, isolat libéré, source
  et surface restaurées ;
- captures Forward+ FHD inspectées :
  `artifacts/verification/studio-increment/ux6-n-hot-reload-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux6-n-debugger-forward-plus-fhd.png` ; aucun clipping
  bloquant, action cachée ou erreur de journal ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-n-hot-reload-debugger.summary.json`.

## Préservation et limites honnêtes

La preuve ouvre une copie temporaire du module réel et d’une source ForgeScript gouvernée. Elle
compare projet, historique, révision, journal, état dirty, zone active et approbations UX6-F/J/K/M,
vérifie le round-trip de la source et les voisins projet, puis supprime l’isolat.

Le hot reload et le débogueur restent des simulations de contrats C# sur état copié, complétées par
des intentions natives exécutées dans la sandbox. Aucun runtime Godot, module chargé, horloge de jeu,
acteur, processus distant ou transport réseau n’est muté par Studio.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptRuntimeToolsTests|' +
  'FullyQualifiedName~NativeForgeScriptRuntimeTests|' +
  'FullyQualifiedName~ForgeScriptRuntimeToolAdapterTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-n-hot-reload-debugger -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-n-debugger-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page scripts -Evidence ux6-n-hot-reload `
  -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-n-hot-reload-forward-plus-fhd.png `
  -Wait -SkipBuild
```
