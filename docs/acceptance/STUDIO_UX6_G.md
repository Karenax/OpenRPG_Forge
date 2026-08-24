# Acceptation Studio — UX6-G Éditeur, Wizard et APIs gameplay ForgeScript

État local au 20 août 2026 : **fonctionnel et qualifié localement** pour cinq routes ForgeScript.
UX6 atteint **31/84** ; le portail de ce lot reste incrémental (`full_gate=false`).

La page **ForgeScript · éditeur, Wizard et APIs gameplay** complète la fondation UX6-F sans créer
une seconde autorité. La source `.as`, sa métadonnée, son historique documentaire, son dry-run et
son écriture atomique restent ceux de `ForgeScriptSourceHistory` et `ForgeScriptSourceStore`.
L’analyse, le Wizard et les simulations travaillent sur une copie en mémoire jusqu’à l’action
explicite **Appliquer et enregistrer**.

<a id="orf-scr-004"></a>
## ORF-SCR-004 — Éditeur de scripts

Le `CodeEdit` expose numéros de ligne, coloration, complétions versionnées, documentation,
signature active, symboles, snippets, formatage déterministe et diagnostics cliquables. Les longues
propositions sont contenues avec ellipse et infobulle ; elles ne déforment plus la page en Full HD.
Un diagnostic repositionne le curseur sur sa ligne et sa colonne sans modifier la source.

<a id="orf-scr-005"></a>
## ORF-SCR-005 — Script Wizard

Le Wizard construit une fonction typée à partir d’un nom, d’une condition relue et d’une action
Message, Action différée, Attaque ou Effet. Il affiche la source produite avant insertion et refuse
les identifiants ou expressions invalides. **Insérer dans le brouillon** ne sauvegarde rien : un
nouvel aperçu sec, une compilation et une approbation encore fraîche restent nécessaires.

<a id="orf-scr-008"></a>
## ORF-SCR-008 — API d’actions différées

La surface prépare une action nommée avec priorité, l’enfile dans le planificateur déterministe,
observe son démarrage et sa complétion, puis prouve le refus d’une autorité cliente. Cette preuve
utilise le handle canonique projeté par `ForgeScriptP1RuntimeAdapter` et ne crée aucun acteur moteur.

<a id="orf-scr-010"></a>
## ORF-SCR-010 — API combat

L’aperçu exécute `ScriptGameplayService` sur un état copié : capacité, dégâts, pénétration d’armure,
seed, attribution et vie restante sont affichés. L’autorité non serveur est refusée avant
application. Aucun dégât n’est injecté dans le runtime, aucune cible Godot ni session réseau n’est
créée.

<a id="orf-scr-011"></a>
## ORF-SCR-011 — API effets

Effet, magnitude, durée, période et six politiques d’empilement sont éditables. L’aperçu applique
et avance l’effet sur une copie, puis vérifie son round-trip par JSON canonique avec le même instant
capturé. Le Studio ne pilote ni boucle moteur ni ticking réseau.

## Preuves

- build Debug final `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **25/25** — Forge.Scripting 5, Forge.RpgKit.Domain 13, Forge.Mcp 7 ;
- portail incrémental avec graphe : **251,59 s**, build, tests, smoke global, capture Forward+ FHD,
  contrôle des 717 lignes de matrice, génération et contrôle complet du graphe ;
- portail transversal complet du jalon UX6-E/F/G : **PASS**, avec **2 035/2 035** tests .NET,
  **39/39** tests Python et les smokes Studio, Toolset, Runtime/Forward+, Crypte des Cendres, Blender,
  MCP, réseau, sauvegarde/reprise et distribution ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_G_PASS` : sélection explicite, autorité UX6-F partagée,
  éditeur, complétion/signature, formatage, diagnostics cliquables, Wizard et rejet invalide,
  actions, combat, effets, refus d’autorité, dry-run, compilation, sandbox, approbation obsolète,
  apply/undo/redo, persistance, voisins, transaction UI, libération de l’isolat, source et surface
  restaurées ;
- capture corrigée puis inspectée :
  `artifacts/verification/studio-increment/ux6-g-script-authoring-gameplay-forward-plus-fhd.png`,
  1920×1080, Godot 4.7.1 .NET, Vulkan Forward+, trois colonnes lisibles, défilement borné du Wizard
  et des APIs, console et six actions visibles sans clipping horizontal ni UUID ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-g-script-authoring-gameplay.summary.json`.

## Préservation et limites honnêtes

La preuve copie le projet et la source dans un workspace temporaire, exerce aperçu, compilation,
sandbox, apply, undo, redo et réouverture, puis compare projet, historique projet, révision, journal,
dirty, zone active et approbation. Le workspace temporaire est supprimé et la page d’origine est
restaurée.

La compilation et la sandbox native sont réelles et bornées. En revanche, les actions, le combat
et les effets restent des simulations déterministes en mémoire : aucun acteur, objet Godot, fichier
du module, événement moteur ou transport réseau n’est muté. Le débogage pas à pas, les points
d’arrêt, le hot reload et les autres routes ForgeScript restent `planned`.

Le champ `full_gate=false` du résumé incrémental décrit uniquement ce profil de route ; le portail
transversal complet a été rejoué séparément et a réussi après la qualification visuelle finale.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~ForgeScriptAuthoringServiceTests|' +
  'FullyQualifiedName~ScriptGameplayServiceTests|' +
  'FullyQualifiedName~ForgeScriptP1RuntimeAdapterTests|' +
  'FullyQualifiedName~ForgeScriptAgentServiceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-g-script-authoring-gameplay -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-g-script-authoring-gameplay-forward-plus-fhd.png `
  -CheckGraph
```
