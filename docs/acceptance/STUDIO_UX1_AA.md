# Acceptation Studio — UX1-AA Assemblage et validation du placeable

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-025`,
`ORF-PLC-026` et `ORF-PLC-027`. Le lot expose sockets, assemblages, actions contextuelles et
validation depuis **Objets & interactions**. Il ne crée jamais silencieusement les prérequis
collision, dégâts ou événements d'un placeable.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-aa-validation` en 54,91 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-aa-validation.summary.json` ;
- tests ciblés : Application 2/2 et RpgKit Domain 6/6, soit 8/8 ;
- smoke Studio headless réussi : sélection explicite, refus humain des prérequis manquants, trois
  dry-runs, garde de révision, commande canonique, préservation, undo/redo, sauvegarde/réouverture
  et nettoyage de la session de preuve isolée ;
- captures Forward+ Full HD, QHD et 4K inspectées dans
  `artifacts/verification/studio-shell/ux1-aa/` : formulaire scrollable, trois parcours et panneau
  fixe **Contrats avant application** restent lisibles sans chevauchement ;
- portail transversal `scripts/Verify.ps1` réussi au jalon intégré UX1-AA/AB/AC : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe et smokes moteur complets.

### ORF-PLC-025

L'auteur choisit explicitement un placeable, un socket existant ou une nouvelle entrée, puis le
blueprint enfant. Type, tags, snap, capacité, prefab et transformation complète sont éditables.
L'aperçu de l'adaptateur fournit patch et patch inverse sans modifier le module ; les autres sockets,
assemblages et le parent de transformation restent préservés.

### ORF-PLC-026

Inspecter, utiliser, ouvrir, parler, prendre, pousser, grimper et une action personnalisée sont
présentés comme cartes ordonnées. Chaque action possède libellé, priorité, contextes requis/interdits
et événement typé. L'aperçu montre uniquement les actions disponibles pour le contexte saisi.

### ORF-PLC-027

Collision/navigation, animations, verrous, pièges, inventaires, scripts, physique et budgets sont
validés ensemble avec diagnostics localisés. La commande canonique est la seule voie d'application
et son dry-run conserve le projet, l'historique et la révision.

## Limites honnêtes

- un placeable sans Foundation, collision active, Operations damageable ou DamageEvents cohérents
  reste bloqué avec une explication ; la surface produit ne synthétise aucun de ces voisins ;
- Forgotten Forest n'ayant pas encore de profil éligible, la preuve utilise un workspace temporaire
  isolé, supprimé à la fermeture, tandis que le projet source reste inchangé ;
- la surface édite une entrée socket et une entrée assemblage à la fois ; elle n'ajoute pas encore de
  boutons dédiés pour créer ou supprimer plusieurs entrées au cours de la même ouverture ;
- l'aperçu exerce les contrats canoniques, pas une simulation moteur ou réseau d'instances runtime.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
)
$filter = 'FullyQualifiedName~R095_PlaceableCompositionValidation|' +
  'FullyQualifiedName~R047_PlaceableValidation|' +
  'FullyQualifiedName~PlaceableCompositionEventsRuntimeAdapterTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-aa-validation `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-aa/ux1-aa-validation-fhd.png
```
