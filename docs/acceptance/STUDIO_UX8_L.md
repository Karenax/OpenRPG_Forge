# Acceptation Studio — UX8-L Synchronisation de dépôt et métadonnées NWSync

État local au 22 août 2026 : **fonctionnel et qualifié** pour les deux routes UX8-L. La vague
Préparer et publier atteint ainsi **43/51** routes accessibles dans Studio.

La page `packages` expose un atelier humain unique pour configurer la synchronisation avant
connexion. La politique persistée et les métadonnées NWSync appartiennent au même
`ModuleRuntimeExtensionProfile` et passent donc par une seule commande canonique
`SetRuntimeExtensionsHandler`. Le chemin de dépôt et le filtre utilisés pour la vérification locale
restent explicitement non persistés.

## Routes couvertes

<a id="orf-cont-009"></a>
- **ORF-CONT-009 — Synchronisation automatique** : reprise partielle, déduplication SHA-256,
  vérification de chaque contenu, signataires approuvés, répertoire de cache relatif, quota et
  licences SPDX sont éditables après sélection explicite du module. L’aperçu liste les ressources,
  tailles et blocs du manifeste avant tout consentement.

<a id="orf-ee-025"></a>
- **ORF-EE-025 — Métadonnées NWSync** : canal, compatibilité, signature obligatoire et notes de
  changement obligatoires sont persistés avec la politique de synchronisation et visibles dans le
  diff sec.

## Transaction, vérification et préservation

- chemins de dépôt et cache relatifs au workspace, refus des sorties de périmètre et des réparse
  points ;
- manifeste canonique SHA-256, licences et signataire de confiance vérifiés avant application ;
- vérification locale bornée à 128 fichiers et 32 Mio pour la preuve interactive ;
- consentement explicite seulement après un dry-run sans diagnostic bloquant ;
- révision, empreinte complète du projet et empreinte binaire typée du brouillon approuvé revérifiées
  au moment de l’application ;
- `SetRuntimeExtensionsHandler`, undo/redo, sauvegarde atomique et réouverture ;
- second passage de `ContentRepositorySyncService` sans téléchargement et entièrement servi depuis
  le cache adressé par contenu ;
- autres politiques opérationnelles, queues Creator/PostLaunch, extensions et racines du projet
  préservées ;
- projet, historique, révision, journal, état dirty, sélection et approbation source inchangés ;
- dépôt et cache de preuve temporaires supprimés avant PASS.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **61/61** — Domain 4, Application 2, Network 3 et ModuleFormat 52 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX8_L_PASS` avec routes, sélection, consentement,
  confinement, manifeste, confiance/licence, falsification, reprise/réutilisation, dry-run,
  commande, gardes stale, undo/redo, persistance, voisins, nettoyage, transaction UI, source
  inchangée et surface restaurée à `true` ;
- capture Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectée : module réel, politiques,
  métadonnées NWSync, dépôt local non persisté, manifeste, cache, diagnostics et six actions visibles
  sans UUID ni clipping ;
- portail incrémental : **PASS en 419,84 s** — build 1,61 s, tests 2,08 + 2,55 + 2,11 + 4,59 s,
  smoke 397,68 s, capture 8,29 s et matrice 0,86 s ;
- matrice Studio après promotion : **623 `functional` / 94 `planned`**, dont UX8 **43/51** ;
- graphe Roslyn final régénéré par l’outil officiel, frais, déterministe et sans diagnostic.

Artefacts :

- `artifacts/verification/studio-increment/ux8-l-repository-sync-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux8-l-repository-sync.summary.json`.

## Limites honnêtes

Studio configure et vérifie la politique, mais ne lance aucun réseau, serveur NWSync, watcher ou
téléchargement distant. La vérification concrète utilise le vrai service de synchronisation avec un
transport de dépôt local et un cache jetable ; elle prouve manifeste, confiance, licences, reprise
et déduplication, pas la disponibilité d’un hébergeur externe. Aucun package n’est installé ou
publié et le module source n’est pas modifié par la preuve UI.

Le portail conserve `full_gate=false` et `graph_check=false` ; le contrôle Roslyn est exécuté
séparément après la documentation. Cette qualification locale ne remplace pas `scripts/Verify.ps1`
ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.Application.Tests\Forge.Application.Tests.csproj'
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~OperationalReadinessProfileTests|' +
  'FullyQualifiedName~SetRuntimeExtensionsHandlerTests|' +
  'FullyQualifiedName~ContentRepositorySynchronizationTests|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests|' +
  'FullyQualifiedName~ProjectJsonSerializerTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page packages `
  -Evidence ux8-l-repository-sync `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts/verification/studio-increment/ux8-l-repository-sync-forward-plus-fhd.png'
```
