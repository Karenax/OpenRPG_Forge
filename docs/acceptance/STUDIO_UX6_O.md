# Acceptation Studio — UX6-O Qualité ForgeScript

État local au 21 août 2026 : **fonctionnel et qualifié localement** pour `ORF-SCR-034`,
`ORF-SCR-035`, `ORF-SCR-037`, `ORF-SCR-038` et `ORF-SCR-040`. UX6 atteint **61/84** ;
l’incrément reste local (`full_gate=false`).

La page **ForgeScript · Qualité R100** conserve l’unique source canonique et l’historique UX6-F.
Ses cinq onglets éditent un profil qualité distinct, versionné et persistant. L’aperçu sec, la
simulation, l’approbation liée à la révision et aux empreintes, l’écriture atomique, undo/redo et la
sauvegarde/réouverture utilisent les services R100 existants sans réécrire la source `.as`.

<a id="orf-scr-034"></a>
## ORF-SCR-034 — Tests unitaires ForgeScript

L’auteur configure graine déterministe, nombre maximal de cas, monde de test copié et rapport CI.
L’aperçu exécute le plan de test du profil sur la source gouvernée, expose les cas et refus humains,
et conserve les identités internes sans afficher d’UUID. Aucun monde de jeu live n’est lancé.

<a id="orf-scr-035"></a>
## ORF-SCR-035 — Profilage de scripts

Les seuils d’instructions, allocations et CPU sont édités sans clamp silencieux. La simulation R100
produit échantillons déterministes, hotspots, graphe d’appels et alertes localisées. Ces mesures
portent sur la sandbox copiée ; elles ne décrivent ni un acteur Godot ni un processus distant.

<a id="orf-scr-037"></a>
## ORF-SCR-037 — Documentation générée

La surface gouverne une destination `docs/generated/*.md`, les publics serveur/client/RPG Kit et la
projection des ressources MCP. Le document est généré en mémoire pendant l’aperçu et affiché dans
Studio. Aucun fichier de documentation produit n’est écrit avant l’application explicite du profil.

<a id="orf-scr-038"></a>
## ORF-SCR-038 — Analyse statique

Cinq règles configurables contrôlent erreurs, warnings-as-errors et diagnostics localisés avec ligne
et suggestion. Les règles et leurs seuils persistent dans le profil R100 ; la source canonique reste
inchangée et les diagnostics libres sont humanisés sans exposer d’identifiant technique.

<a id="orf-scr-040"></a>
## ORF-SCR-040 — Sérialisation d’état script

L’auteur édite les composants `nom=version`, le quota et l’exigence d’enregistrement préalable. La
simulation sérialise uniquement l’état enregistré et exclut explicitement les pointeurs de VM. Elle
ne constitue pas une sauvegarde gameplay persistante et ne restaure aucun acteur live.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **16/16** — Scripting **7**, adaptateur RPG Kit **2**, MCP **7** ;
- portail incrémental : **135,51 s**, build, trois projets de tests, smoke global, capture Forward+
  FHD et contrôle des 717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_O_PASS` : sélection explicite, profil canonique, tests,
  profiler, documentation, analyse statique, état, adaptateur, sandbox native, profils invalides,
  dry-run, source et approbation obsolètes, apply/undo/redo, persistance, préservation, transaction
  UI, isolat libéré, source inchangée et surface restaurée ;
- cinq captures Forward+ FHD inspectées :
  `artifacts/verification/studio-increment/ux6-o-tests-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux6-o-profiler-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux6-o-documentation-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux6-o-analysis-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux6-o-state-forward-plus-fhd.png` ; les cinq onglets,
  leurs diagnostics, scrolls et actions sont lisibles sans clipping bloquant ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-o-script-quality.summary.json`.

## Préservation et limites honnêtes

La preuve ouvre une copie temporaire du module réel, de la source ForgeScript gouvernée et du profil
R100. Elle compare les octets source et métadonnées, projet, historique, révision, journal, état
dirty, sélection et approbations, puis supprime l’isolat. Le brouillon est protégé par les empreintes
de la source, du profil et de l’approbation ; une modification concurrente est refusée.

Les tests, échantillons de profilage et états sérialisés sont déterministes et copiés. La génération
de documentation reste en mémoire pendant l’aperçu. L’exécution native est confinée à la sandbox ;
Studio ne lance aucun monde de jeu, acteur, processus distant, fichier arbitraire ou transport
réseau et ne revendique aucune sauvegarde gameplay live.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptQualityServiceTests|' +
  'FullyQualifiedName~ForgeScriptQualityRuntimeAdapterTests|' +
  'FullyQualifiedName~ForgeScriptAgentServiceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-o-script-quality -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-o-tests-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page scripts -Evidence ux6-o-profile `
  -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-o-profiler-forward-plus-fhd.png `
  -Wait -SkipBuild
```
