# Acceptation Studio — UX9-E Résilience et capacité réseau

État local au 22 août 2026 : **fonctionnel et qualifié** pour deux routes UX9. La vague Exploiter
en réseau atteint **21/68** routes accessibles dans Studio.

La page `network-director` édite directement un `NetworkResilienceCapacityProfile` persistant.
L'humain choisit explicitement le module et l'un des deux workflows, prévisualise la commande
canonique, approuve cette seule révision, applique, annule/rétablit, enregistre puis confirme une
sonde locale distincte en rôle Administrateur. Le fichier
`.forge/network/resilience-capacity.json` n'est créé que sur **Enregistrer** et l'écriture atomique
utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-mp-033"></a>
- **ORF-MP-033 — Sauvegarde/rollback serveur** : intervalle de snapshot et rétention sont éditables ;
  WAL, répétition de restauration, migrations, récupération à un instant donné et confirmation
  destructive restent imposés. La sonde sauvegarde un vrai snapshot mémoire, prévisualise sa
  restauration puis, seulement après confirmation séparée, répète le rollback sur cette copie.

<a id="orf-mp-034"></a>
- **ORF-MP-034 — Capacité et files d'attente** : plafond, slots Administrateur réservés et durée de
  réservation sont éditables ; file FIFO, position visible, délestage et métriques restent imposés.
  La sonde remplit la capacité publique, observe les positions 1 et 2, libère une place et vérifie
  la promotion de la première réservation sans annoncer de capacité publique fixe.

## Transaction, permissions et préservation

- `NetworkResilienceCapacityHistory` impose Administrateur, dry-run, révision attendue et
  progression d'une seule révision ;
- l'approbation est liée à l'empreinte du brouillon et de la source, puis révoquée dès qu'un champ
  ou la révision change ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- la répétition manuelle de rollback exige une confirmation distincte et refuse tout rôle autre
  qu'Administrateur ;
- `NetworkResilienceCapacityRuntimeAdapter` construit les vrais `InMemoryWorldSnapshotStore`,
  `RollbackService` et `ConnectionQueue`, puis toutes les ressources temporaires sont libérées ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont préservés.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **11/11** — Network 6, ModuleFormat 3 et MCP 2 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_E_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, rollback,
  capacité, audit, UI, préservation, restauration et libération à `true` ;
- deux captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées : cartes, deux workflows,
  champs, limites, approbations, preuve et six actions lisibles, sans UUID ni clipping bloquant ;
- portail incrémental : **PASS en 546,18 s** — build 1,45 s, tests 2,67 + 2,35 + 2,84 s,
  smoke 420,45 s, capture 8,26 s, matrice 0,86 s, génération du graphe 52,79 s et
  `check --full` 54,47 s ;
- matrice Studio après promotion : **653 `functional` / 64 `planned`**, dont UX9 **21/68** ;
- graphe Roslyn officiel régénéré après promotion, sans diagnostic, frais et pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-e-backup-rollback-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-e-capacity-queue-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-e-network-resilience-capacity-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-e-network-resilience-capacity.summary.json`.

## Limites honnêtes

Studio n'ouvre aucun port, socket, serveur public, stockage distant, orchestrateur de sauvegarde ou
base externe. Les sondes utilisent les adaptateurs réels en mémoire et un workspace temporaire
supprimé ; elles ne qualifient ni stockage durable de production, haute disponibilité
multi-processus, reprise après sinistre distante, monde persistant public ou capacité de charge
réseau réelle.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Sharding, hot reload serveur, extensibilité et
protocole cross-platform restent hors du lot R124.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkResilienceCapacityProfileTests|' +
  'FullyQualifiedName~WorkspaceNetworkResilienceCapacityStoreTests|' +
  'FullyQualifiedName~NetworkResilienceCapacityAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-e-network-resilience-capacity `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-e-network-resilience-capacity-forward-plus-fhd.png' `
  -CheckGraph
```
