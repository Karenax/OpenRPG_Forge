# Acceptation Studio — UX1-W Outils dynamiques de zone

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-EE-002`, `ORF-EE-018` et
`ORF-EE-019`. Ce lot rend la mise à l'échelle, les zones dynamiques et les copies runtime
directement accessibles depuis la page **Zones & scène** dans l'unique fenêtre `Forge.Studio`. Il ne
qualifie ni la navigation UX2, ni une distribution publique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `zones` et la preuve
  `ux1-w-dynamic`, avec résumé dans
  `artifacts/verification/studio-increment/ux1-w-dynamic.summary.json` ;
- tests ciblés : Application 10/10, Domain 10/10 et ModuleFormat 7/7, soit 27/27 ;
- smoke Studio headless et capture Forward+ Full HD réussis sur Forgotten Forest, avec preuve
  visuelle inspectée dans `artifacts/verification/studio-shell/ux1-w/dynamic-fhd.png` et journal
  associé `dynamic-fhd.png.log` ;
- la capture 1920×1080 utilise Godot 4.7.1 .NET, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
  zéro fallback et une seule fenêtre ; elle montre les trois sections métier **Échelle**,
  **Zones dynamiques** et **Copies runtime**, sans UUID ni état technique brut dans la surface
  principale ;
- ce lot ne modifiant ni layout global, ni viewport, ni densité générale du shell, les revues QHD et
  4K restent réservées au prochain jalon multi-lots UX1.

### ORF-EE-002

L'onglet **Échelle** permet de redimensionner la sélection courante avec aperçu obligatoire avant
application. Les contraintes, bornes et messages d'erreur restent humains ; l'application passe par
la commande canonique et reste annulable/rétablissable.

### ORF-EE-018

L'onglet **Zones dynamiques** configure une politique d'instanciation temporaire à partir de la zone
active, avec quotas, durée, dépendances et autorité serveur. L'aperçu simule des instances sans créer
de nouveaux documents d'éditeur et bloque toute configuration invalide avant mutation.

### ORF-EE-019

L'onglet **Copies runtime** simule des duplications d'objets avec identités fraîches, références et
état caché préservés. La simulation reste explicitement runtime et n'invente pas de faux objet
persisté dans la scène d'édition ; l'application enregistre la politique canonique puis conserve
undo/redo, sauvegarde et réouverture.

## Limites honnêtes

- ce lot qualifie seulement `ORF-EE-002`, `ORF-EE-018` et `ORF-EE-019` ;
- les aperçus runtime n'exécutent pas une copie réseau réelle ni une instanciation serveur distante ;
- Forgotten Forest sert de rendu et de catalogue réels, pas de témoin d'autorité distribuée ;
- l'application reste en mémoire jusqu'à l'enregistrement explicite.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ScaleEntitiesHandlerTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~ModernRuntimeToolingTests|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page zones -Evidence ux1-w-dynamic `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-w/dynamic-fhd.png
```
