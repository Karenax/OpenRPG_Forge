# Acceptation Studio — UX1-AC Trigger avancé

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-TRG-004`,
`ORF-TRG-011`, `ORF-TRG-017` et `ORF-TRG-019`. Le lot expose piège, filtre de cible, dix volumes de
gameplay et occupation depuis **Objets & interactions**. La Foundation UX1-Y demeure un prérequis
explicite et n'est jamais créée par le parcours produit UX1-AC.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-ac-volumes` en 59,92 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-ac-volumes.summary.json` ;
- tests ciblés : Application 1/1, RpgKit Domain 8/8 et ModuleFormat 1/1, soit 10/10 ;
- smoke Studio headless réussi : sélection explicite, Foundation exigée, quatre familles
  d'invalidités, doubles gardes de révision, adaptateur runtime, commande, préservation, undo/redo,
  round-trip et transaction UI isolée ;
- captures Forward+ Full HD, QHD et 4K inspectées dans
  `artifacts/verification/studio-shell/ux1-ac/` : les trois onglets, les cartes de volumes, le résumé
  fixe et les actions restent lisibles et bornés ;
- portail transversal `scripts/Verify.ps1` réussi au jalon intégré UX1-AA/AB/AC : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe et smokes moteur complets.

### ORF-TRG-004

Détection, désamorçage, réarmement, payload et visualisation MJ configurent le piège. Les valeurs
invalides sont refusées avant la commande et l'adaptateur runtime exerce détection, désamorçage,
réinitialisation et effet.

### ORF-TRG-011

Le filtre expose joueurs uniquement, tags, factions par nom, composants, groupe, équipe et prédicat
borné. Les contradictions et références inconnues produisent un diagnostic humain sans mutation.

### ORF-TRG-017

Les dix types de volumes canoniques sont présents ensemble avec activation, valeur principale et
événements d'entrée/sortie typés. Les paramètres authored supplémentaires et l'ordre restent
préservés lors de l'édition.

### ORF-TRG-019

Occupants, derniers déclenchements, délais, filtres, trace et maximum d'entrées composent la vue
d'occupation. L'aperçu annonce exactement les facettes actives sans prétendre rendre un gizmo 3D.

## Limites honnêtes

- un Trigger sans Foundation reste bloqué dans le produit avec renvoi vers UX1-Y ; la preuve sur
  Forgotten Forest utilise uniquement une session temporaire isolée et supprimée à la fermeture ;
- Foundation, Portal, Validation, transform, overrides et profils voisins sont préservés mais non
  éditables ; `ORF-TRG-003` n'est pas promu ;
- factions et événements sont saisis par noms lisibles, mais le lot ne construit pas de relation de
  faction ni de script absent ;
- les cartes éditent les contrats de volume ; aucun gizmo ou effet moteur 3D n'est revendiqué.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~R096_AdvancedTriggerUsesCanonicalCommandScriptProjection|' +
  'FullyQualifiedName~TriggerAdvancedRuntimeAdapterTests|' +
  'FullyQualifiedName~TriggerAdvancedServiceTests|' +
  'FullyQualifiedName~SampleProject_WorldFeatureFixtureIsCanonicalAndRuntimeResolvable'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-ac-volumes `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-ac/ux1-ac-volumes-fhd.png
```
