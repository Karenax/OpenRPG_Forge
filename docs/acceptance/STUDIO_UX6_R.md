# Acceptation Studio — UX6-R Données et UI runtime

État local au 21 août 2026 : **fonctionnel et qualifié localement** pour `ORF-EE-011`,
`ORF-EE-012`, `ORF-EE-013`, `ORF-EE-015` et `ORF-EE-016`. UX6 atteint **69/84** ;
l’incrément reste local (`full_gate=false`).

La page **Données & UI runtime** expose quatre onglets humains sous une seule commande canonique
`SetRuntimeExtensionsHandler`. L’auteur sélectionne explicitement le profil du module, modifie les
collections complètes, lance un aperçu sec, puis applique, annule, rétablit ou enregistre. La
révision du projet et les empreintes du projet et du brouillon gardent l’approbation.

<a id="orf-ee-011"></a>
## ORF-EE-011 — Règles plus data-driven

L’onglet **Règles data-driven** sélectionne, crée ou retire une définition, puis édite sa clé
humaine, sa famille, son caractère requis, son schéma JSON et ses valeurs par défaut. Le preview
résout réellement les handles avec `RuntimeExtensionRuntimeService`, sans recompiler ou muter le
moteur. Changer de règle sauvegarde d’abord le brouillon courant ; le smoke exerce deux règles et
refuse toute perte silencieuse.

<a id="orf-ee-012"></a>
## ORF-EE-012 — NUI scriptable

L’onglet **Interface & événements** authorise des documents UI complets avec titre, thème,
obligation runtime et widgets ordonnés. Chaque ligne garde en interne son identité stable, tandis
que la surface affiche uniquement clé, type, parent, texte, binding, interactivité, ordre et
permission. La preuve multi-document vérifie que passer à une seconde interface conserve la
première.

<a id="orf-ee-013"></a>
## ORF-EE-013 — Événements GUI

Les bindings relient un widget, un événement typé, une commande, un contexte d’entrée et une
permission. L’aperçu publie réellement un événement sur un bus `ForgeScriptUiEventService` en
mémoire et rapporte sa livraison ; aucun bus produit ni nœud Godot n’est modifié.

<a id="orf-ee-015"></a>
## ORF-EE-015 — JSON runtime

L’auteur sélectionne, crée ou retire un document JSON versionné, règle son schéma, sa valeur
initiale, ses quotas UTF-8 et de profondeur ainsi que sa mutabilité. Le preview enregistre le
document dans `ForgeScriptJsonService`, le lit puis réalise une mutation révisionnée en mémoire.
Les documents multiples conservent leur brouillon lorsqu’on change de sélection.

<a id="orf-ee-016"></a>
## ORF-EE-016 — Sérialisation d’objet avec état

L’onglet **État sérialisable** édite les composants autorisés, les plafonds de références,
d’inventaire et de blackboard, ainsi que la restauration des références résolues. L’aperçu valide
la politique uniquement : il ne crée aucune sauvegarde produit et ne restaure aucun acteur.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **24/24** — Domain **3**, Application **4**, Scripting **2**,
  ModuleFormat **15** ;
- portail incrémental : **140,90 s**, build, quatre projets de tests, smoke global, capture
  Forward+ FHD et contrôle des 717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_R_PASS` : sélection explicite, module réel, cinq contrats,
  invalidités, dry-run, handles runtime, événement UI, mutation JSON révisionnée, politique de
  snapshot, source et approbation obsolètes, commande canonique, undo/redo, round-trip, profils
  voisins, identités stables, texte humain, limites runtime, transaction UI multi-document,
  ressources libérées, source inchangée et surface restaurée ;
- quatre captures Forward+ FHD inspectées :
  `artifacts/verification/studio-increment/ux6-r-rules-forward-plus-fhd.png`,
  `ux6-r-ui-forward-plus-fhd.png`, `ux6-r-json-forward-plus-fhd.png` et
  `ux6-r-snapshot-forward-plus-fhd.png` ; les aperçus, diffs, statuts et cinq actions restent
  visibles, les contenus longs défilent et aucun UUID n’est exposé ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-r-runtime-data-ui.summary.json`.

## Préservation et limites honnêtes

La preuve travaille dans une copie temporaire du module réel, applique un candidat contenant
plusieurs règles, interfaces et documents JSON, puis vérifie undo/redo et sauvegarde/réouverture.
Elle compare projet, historique, révision, journal, état dirty, zone active et approbations avant de
libérer l’isolat. `ModernTooling`, `OperationalReadiness`, `PostLaunch` et les autres racines du
projet restent identiques.

Les services de résolution, le bus UI et le store JSON sont réellement exercés en mémoire. Studio
ne crée toutefois aucun nœud Godot, acteur, sauvegarde produit, bus runtime live, processus ou
transport réseau. Le portail incrémental ne remplace pas le portail transversal complet.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ModuleRuntimeExtensionProfileTests|' +
  'FullyQualifiedName~RuntimeExtensionRuntimeServiceTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~ForgeScriptJsonAndUiTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests|' +
  'FullyQualifiedName~RuntimeSaveStoreTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-r-runtime-data-ui -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-r-rules-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-r-ui -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-r-ui-forward-plus-fhd.png -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-r-json -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-r-json-forward-plus-fhd.png -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-r-snapshot -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-r-snapshot-forward-plus-fhd.png -Wait -SkipBuild
```
