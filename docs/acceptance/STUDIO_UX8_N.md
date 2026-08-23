# Acceptation Studio — UX8-N Cycle de vie des packages

État local au 22 août 2026 : **fonctionnel et qualifié** pour les quatre dernières routes UX8.
La vague Préparer et publier atteint ainsi **51/51** routes accessibles dans Studio.

La page `packages` expose un atelier humain unique à quatre onglets. Elle maintient trois autorités
distinctes : la politique de packs curatés dans `GameplayProductionProfile.CuratedPacks`, les URI
et références dans le registre de contenu, puis les migrations et désinstallations dans les stores
de cycle de vie des packages. Aucun regroupement ne simule une transaction atomique entre ces
autorités indépendantes.

## Routes couvertes

<a id="orf-cont-010"></a>
- **ORF-CONT-010 — Packs curatés** : canal, compatibilité, licences SPDX, signature de confiance et
  repli local sont prévisualisés puis appliqués par la commande runtime canonique.

<a id="orf-cont-019"></a>
- **ORF-CONT-019 — Resource namespaces** : une ressource explicitement sélectionnée expose son
  adresse canonique `package:type/path`, ses alias et ses références, avec détection des collisions
  par le registre de contenu avant mutation du catalogue.

<a id="orf-cont-026"></a>
- **ORF-CONT-026 — Migrations de package** : une version installée est migrée vers une cible
  explicite par une étape hôte compilée, après dry-run, avec backup, changelog et restauration.

<a id="orf-cont-027"></a>
- **ORF-CONT-027 — Désinstallation sûre** : l'impact sur les dépendants est affiché, un remplacement
  installé peut être choisi, le package externe peut être sauvegardé puis retiré et seuls les blobs
  devenus orphelins sont collectés.

## Transactions, persistance et préservation

- sélection explicite du module, de la ressource et du package avant activation des actions ;
- quatre brouillons, aperçus secs, approbations et historiques séparés ;
- gardes de révision et d'empreinte revérifiées à l'application, avec refus des approbations
  obsolètes ;
- commandes canoniques `SetRuntimeExtensionsHandler` et `SetContentWorkspaceHandler` pour les
  documents projet ;
- planificateur/store de migration et analyseur/store de désinstallation pour les packages
  externes, avec backup réel et restore déterministe ;
- undo/redo couplé à l'autorité concernée, sauvegarde atomique puis réouverture vérifiée ;
- catalogue voisin, ressources, packages non ciblés, blobs encore référencés, état de sélection et
  source de travail strictement préservés ;
- copies de preuve et Vault temporaire supprimés avant le marqueur PASS.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **22/22** — Domain 13, Application 3 et ModuleFormat 6 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX8_N_PASS` avec quatre routes, sélection, dry-run,
  commande, gardes stale, undo/redo, persistance, voisins, libération des ressources, transaction
  UI, source inchangée et surface restaurée à `true` ;
- captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées pour Packs, Namespaces, Migrations
  et Désinstallation : onglets, consentement, aperçu et cinq actions visibles, aucun UUID ni
  clipping ;
- portail incrémental : **PASS en 581,21 s** — build 1,36 s, tests 4,82 + 6,09 + 2,62 s, smoke
  433,84 s, capture 8,77 s, matrice 0,84 s, génération du graphe 70,29 s et `check --full`
  52,52 s ;
- matrice Studio après promotion : **632 `functional` / 85 `planned`**, dont UX8 **51/51**.
- graphe Roslyn officiel : **11 234 nœuds / 86 636 relations / 0 diagnostic**, frais et pleinement
  déterministe après `check --full`.

Artefacts :

- `artifacts/verification/studio-increment/ux8-n-package-lifecycle-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-n-resource-namespaces-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-n-package-migration-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-n-safe-uninstall-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-n-package-lifecycle.summary.json`.

## Limites honnêtes

La politique curatée ne télécharge ni n'installe aucun pack. Les namespaces gouvernent des
métadonnées et ne chargent pas les ressources. Une migration exécute uniquement une étape de
compatibilité compilée et approuvée par l'hôte : aucun script fourni par un package, agent, shell
ou réécriture arbitraire du contenu n'est autorisé. La désinstallation ne touche pas au module
source ; le package du Vault externe n'est retiré que si l'utilisateur le demande explicitement.
Aucun réseau, watcher, registre distant, signature privée, commande Git ou publication n'est lancé.

Le portail conserve `full_gate=false` malgré `graph_check=true` ; il ne remplace ni
`scripts/Verify.ps1`, ni une qualification de distribution publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~PostLaunchCapabilityResolverTests|' +
  'FullyQualifiedName~ContentResourceRegistryTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~SetContentWorkspaceHandlerTests|' +
  'FullyQualifiedName~AssetPackageMigrationStoreTests|' +
  'FullyQualifiedName~AssetVaultPackageStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page packages `
  -Evidence ux8-n-package-lifecycle `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux8-n-package-lifecycle-forward-plus-fhd.png' `
  -CheckGraph
```
