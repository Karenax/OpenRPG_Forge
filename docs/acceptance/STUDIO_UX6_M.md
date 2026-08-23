# Acceptation Studio — UX6-M Automatisation ForgeScript

État local au 21 août 2026 : **fonctionnel et qualifié localement** pour `ORF-SCR-018`,
`ORF-SCR-024`, `ORF-SCR-025` et `ORF-SCR-026`. UX6 atteint **54/84** ; l’incrément reste local
(`full_gate=false`).

La page **Automatisation ForgeScript UX6-M** expose les quatre collections ordonnées de
`ScriptAutomationProfile` sans UUID ni JSON. L’auteur choisit explicitement le module, travaille sur
un brouillon complet, puis passe validation, aperçu sec, approbation liée à la révision et aux
empreintes, commande `SetWorldFeatureCatalog`, undo/redo et sauvegarde/réouverture.

<a id="orf-scr-018"></a>
## ORF-SCR-018 — API caméra et cutscenes

Chaque plan conserve son identité stable et expose rig humain, plan, durée, fondu, verrou du contrôle,
récupération, restauration finale et skip accessible. Le resolver produit un plan déterministe et
vérifie verrou/récupération ; il ne déplace aucune caméra live.

<a id="orf-scr-024"></a>
## ORF-SCR-024 — Tag-based scripting

Les routes ordonnées associent exactement tag de blueprint, événement et script canonique. L’aperçu
prouve la première correspondance active sans exécuter le script ni synthétiser une route implicite.

<a id="orf-scr-025"></a>
## ORF-SCR-025 — Module switches

Les flags typés booléen, entier, nombre, texte ou choix conservent nom, valeur par défaut, portée,
documentation et version d’introduction. Les valeurs invalides sont bloquées avant la commande.

<a id="orf-scr-026"></a>
## ORF-SCR-026 — AI behavior switches

Chaque réglage IA expose clé, type, valeur par défaut, valeurs autorisées, aperçu humain et autorité
serveur. Le resolver accepte une valeur autorisée et retombe explicitement sur le défaut pour une
valeur hors contrat ; aucun agent IA n’est instancié.

## Preuves

- build Debug `Forge.Studio` : **0 avertissement, 0 erreur** ;
- tests ciblés : **60/60** — Domain **22**, Application **27**, ModuleFormat **11** ;
- portail incrémental : **162,07 s**, build, tests, smoke global, capture Forward+ FHD et contrôle des
  717 lignes de matrice ;
- smoke `OPENRPG_FORGE_STUDIO_UX6_M_PASS` : caméra, tags, flags module, réglages IA, invalidités,
  dry-run, gardes source/approbation, commande canonique, apply/undo/redo, persistance, voisins,
  transaction UI, isolat libéré, source et surface restaurées ;
- captures Forward+ FHD inspectées, toutes en 1920×1080 :
  `ux6-m-script-automation-forward-plus-fhd.png`, `ux6-m-tags-forward-plus-fhd.png`,
  `ux6-m-module-switches-forward-plus-fhd.png` et `ux6-m-ai-switches-forward-plus-fhd.png` sous
  `artifacts/verification/studio-increment/` ; aucun clipping bloquant ni erreur de journal ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-m-script-automation.summary.json`.

## Préservation et limites honnêtes

La preuve dérive un profil canonique du module réel dans un workspace temporaire. Elle compare projet,
historique, révision, journal, état dirty, zone active et approbations UX6-F/J/K/M, vérifie la
préservation des autres familles `WorldFeatures`, le round-trip complet, puis supprime l’isolat.

Studio n’instancie aucun `Camera3D`, acteur, arbre de comportement, script ou session réseau. Les
résultats sont des résolutions de politique C# en mémoire ; leur exécution demeure l’autorité du
runtime produit.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~WorldFeatureRuntimeResolverTests|' +
  'FullyQualifiedName~WorldFeatureCatalogValidatorTests|' +
  'FullyQualifiedName~WorldFeatureCatalogWorkflowTests|' +
  'FullyQualifiedName~WorldFeaturePersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page scripts `
  -Evidence ux6-m-script-automation -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-m-script-automation-forward-plus-fhd.png
```
