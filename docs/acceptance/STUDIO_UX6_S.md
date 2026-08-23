# Acceptation Forge Studio — UX6-S

Date de qualification : 21 août 2026.

UX6-S rend accessibles quatre politiques Creator déjà canoniques dans
`CreatorExperienceProductionProfile`. La surface **Creator technique** vit sur la page Runtime et
sépare les quatre familles en onglets, tout en les appliquant ensemble par
`SetRuntimeExtensionsHandler` après aperçu sec et approbation.

## ORF-EE-014

### Requêtes de périphérique

- activation explicite et liste complète des noms de requêtes autorisées ;
- consentement et anonymisation obligatoires dès que la politique est active ;
- liste distincte de requêtes simulées pour montrer le filtrage du résolveur ;
- aucune API système et aucun périphérique ne sont interrogés par Studio.

## ORF-EE-017

### Commandes VM du gestionnaire de ressources

- activation, nombre maximal de résultats, quota d’octets et espaces de noms complets ;
- valeur de requête distincte pour prévisualiser l’acceptation ou le refus du quota ;
- validation canonique de 1 à 10 000 résultats et de 1 024 à 1 073 741 824 octets ;
- aucune ressource réelle n’est listée, chargée ou libérée par l’aperçu.

## ORF-EE-020

### Contrôle de groupe

- activation, bascule directe, capacité de 1 à 64 membres et commandes ordonnées ;
- simulation bornée du nombre de membres demandés via `ResolveRpgDistribution` ;
- décision humaine d’acceptation/refus et conservation des commandes non affichées ailleurs ;
- aucun acteur, ordre IA, transfert d’autorité ou transport réseau n’est exécuté.

## ORF-EE-021

### Jusqu’à huit classes

- minimum canonique de huit parcours data-driven, plafond explicite de 256 ;
- option d’interface adaptée aux grands ensembles ;
- simulation séparée du nombre de classes/parcours demandés ;
- aucune classe, progression, niveau ou interface runtime n’est créé par Studio.

## Transaction et préservation

Le brouillon est lié à la révision et à une empreinte SHA-256 binaire, typée et préfixée par
longueur. L’aperçu appelle la commande en `DryRun`, puis l’application refait le dry-run avant
l’unique mutation. Undo, redo, sauvegarde et réouverture sont prouvés sur un workspace temporaire
copiant le module réel.

La reconstruction remplace uniquement `DeviceQueries`, `ResourceCommands`, `PartyControl` et
`ProgressionTracks`. Eau, zones dynamiques, dépôt, observabilité, graphismes, les autres profils
PostLaunch, les règles/UI/JSON/snapshots des extensions et toutes les racines voisines du projet
restent exactement identiques. Les approbations UX6 voisines, la zone/sélection, l’historique, la
révision et l’état dirty de la source sont restaurés et comparés.

## Validation obtenue

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **19/19** — Domain 10, Application 2, ModuleFormat 7 ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX6_S_PASS`, avec invalidités, dry-run, quatre résolutions,
  révision/approbation obsolètes, commande canonique, undo/redo, round-trip, voisins, texte humain,
  transaction UI, libération des ressources, source inchangée et surface restaurée ;
- portail incrémental : **140,55 s**, build, tests, smoke global, capture Forward+ FHD et contrôle
  des 717 lignes de matrice ; `full_gate=false` reste explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-s-creator-technical.summary.json` ;
- quatre captures Forward+ 1920×1080 inspectées :
  `ux6-s-devices-forward-plus-fhd.png`, `ux6-s-resources-forward-plus-fhd.png`,
  `ux6-s-party-forward-plus-fhd.png` et `ux6-s-progression-forward-plus-fhd.png`.

Les quatre onglets, l’aperçu de décision, le diff humain et les cinq actions transactionnelles sont
visibles sans clipping. Les collections défilent dans leur page, aucun UUID n’est affiché et les
limites runtime restent visibles dans chaque onglet.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~PostLaunchCapabilityResolverTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page runtime `
  -Evidence ux6-s-creator-technical -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux6-s-devices-forward-plus-fhd.png
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-s-resources -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-s-resources-forward-plus-fhd.png -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-s-party -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-s-party-forward-plus-fhd.png -Wait -SkipBuild
& .\scripts\Open-Studio.ps1 -Page runtime -Evidence ux6-s-progression -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux6-s-progression-forward-plus-fhd.png -Wait -SkipBuild
```
