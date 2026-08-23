# Acceptation Studio — UX6-Q Compilateur moderne et création ForgeScript assistée

État local au 21 août 2026 : **fonctionnel et qualifié localement** pour `ORF-EE-031` et
`ORF-AI-018`. UX6 atteint **64/84** ; l’incrément reste local (`full_gate=false`).

La page **ForgeScript · Compiler & assister** sépare deux transactions. Le profil compilateur
modifie uniquement la politique `ModernTooling.Compiler` du projet. L’assistant hors ligne prépare,
valide puis écrit une source canonique `.as` et sa métadonnée gouvernée seulement après une
approbation explicite. Aucun fournisseur distant, modèle génératif ou second éditeur de source n’est
présenté comme exécuté.

<a id="orf-ee-031"></a>
## ORF-EE-031 — Améliorations compilateur

L’auteur sélectionne explicitement le profil du module, puis règle la compilation incrémentale, les
diagnostics stables, la télémétrie, le parallélisme, le quota de sources et les suites de conformité.
L’aperçu sec passe par `SetRuntimeExtensionsHandler` et compile les sources canoniques copiées avec
le compilateur natif et un cache temporaire.

La preuve utilise deux scripts autonomes reliés par leur graphe de dépendances : le premier passage
compile deux unités, le second en réutilise deux, puis une modification de la dépendance recompile
les deux. Chaque source `.as` reste un script ForgeScript autonome avec son propre `main` ; le graphe
exprime l’ordre et l’invalidation, pas une édition de liens entre bibliothèques.

L’application est gardée par la révision et les empreintes du projet et du brouillon. Elle fournit
dry-run, apply, undo, redo et sauvegarde/réouverture sans modifier les autres politiques runtime.

<a id="orf-ai-018"></a>
## ORF-AI-018 — Création de ForgeScript

L’auteur sélectionne une source existante ou prépare explicitement une nouvelle source canonique,
décrit l’intention, choisit le modèle local déterministe et génère une proposition **hors ligne**.
La proposition contient source, version d’API, diagnostics de compilation, tests sandbox, citations,
style et provenance. Elle ne devient applicable qu’après aperçu sec, approbation et niveau de
permission suffisant.

Le handler `ApplyAssistedCanonicalForgeScriptHandler` écrit atomiquement le `.as` et son
`.meta.json`, conserve leurs octets pour undo/redo exacts et refuse un brouillon, un disque ou une
approbation obsolète. Les anciennes sources `.forge` restent lisibles et protégées ; elles ne sont
jamais renommées ou écrasées implicitement.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **16/16** — Scripting compilateur/cache **8**, Application runtime extensions
  **2**, ModuleFormat assistant historique/canonique **6** ;
- portail incrémental : **137,50 s**, build, trois projets de tests, smoke global, capture Forward+
  FHD et contrôle des 717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_Q_PASS` : sélection explicite, profil canonique, options invalides,
  dry-run, dépendances, cache, invalidation, diagnostics stables, télémétrie, conformité,
  apply/undo/redo/persistance, proposition compilée, sandbox, mode hors ligne, approbation,
  permission, brouillon et disque obsolètes, métadonnée source, voisins, transaction UI, isolat
  libéré, source inchangée et surface restaurée ;
- captures Forward+ FHD inspectées :
  `artifacts/verification/studio-increment/ux6-q-compiler-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux6-q-assistant-forward-plus-fhd.png` ; les deux onglets,
  paramètres, source proposée, diagnostics, résultats et actions utilisent la largeur disponible
  sans clipping bloquant ni UUID visible ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-q-compiler-assistant.summary.json`.

## Préservation et limites honnêtes

Les preuves utilisent une copie temporaire du module, des sources et des métadonnées, puis comparent
projet, historique, révision, journal, état dirty, sélection, approbations et octets avant de libérer
l’isolat. Le cache compilateur temporaire est également supprimé.

Le compilateur compile réellement les sources copiées. L’assistant est une proposition locale et
déterministe : aucun appel réseau, fournisseur externe ou modèle d’IA n’est exécuté. Studio ne pilote
aucun acteur, bus runtime, objet Godot, distribution de bytecode ou transport réseau depuis cette
surface. Le portail incrémental ne remplace pas le portail transversal complet.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptCompilerServiceTests|' +
  'FullyQualifiedName~ForgeScriptArtifactCacheTests|' +
  'FullyQualifiedName~AssistedForgeScriptWorkflowTests|' +
  'FullyQualifiedName~AssistedCanonicalForgeScriptWorkflowTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-q-compiler-assistant -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-q-compiler-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page scripts -Evidence ux6-q-assistant `
  -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-q-assistant-forward-plus-fhd.png `
  -Wait -SkipBuild
```
