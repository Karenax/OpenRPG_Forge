# Acceptation Studio — UX9-D Persistance réseau et observabilité locale

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **19/68** routes accessibles dans Studio.

La page `network-director` édite directement un `NetworkPersistenceObservabilityProfile`
persistant. L'humain choisit explicitement le module et l'un des cinq workflows, prévisualise la
commande canonique, approuve cette seule révision, applique, annule/rétablit, enregistre puis
confirme une sonde locale distincte en rôle Administrateur. Le fichier
`.forge/network/persistence-observability.json` n'est créé que sur **Enregistrer** et l'écriture
atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-mp-022"></a>
- **ORF-MP-022 — Persistance de monde** : Embedded et relationnel, transactions, migrations,
  backups et failover restent imposés ; la rétention est éditable. La sonde construit le runtime
  réel et sauvegarde un snapshot révisionné avec son hash SHA-256.

<a id="orf-mp-023"></a>
- **ORF-MP-023 — SQL sécurisé** : dépôts côté serveur, paramètres typés et interdiction du SQL
  brut restent imposés. La surface configure une allowlist d'opérations nommées ; la sonde exécute
  `world_flag.upsert`, relit par `world_flag.by_scope` et refuse une opération non listée.

<a id="orf-mp-028"></a>
- **ORF-MP-028 — Métriques et health checks** : métriques, endpoints santé/métriques,
  Prometheus, OpenTelemetry et filtrage privé sont persistés. La sonde publie deux signaux autorisés,
  obtient un état `Healthy` et refuse une métrique hors politique, sans ouvrir d'endpoint externe.

<a id="orf-mp-030"></a>
- **ORF-MP-030 — Profiler serveur** : sampling, instrumentation, traces, breakdown du tick et spans
  script/DB/réseau restent imposés ; le plafond de captures est éditable. La sonde capture
  `server.tick` via le profiler configuré et vérifie le budget.

<a id="orf-mp-032"></a>
- **ORF-MP-032 — Reconnexion** : délai de grâce, ticket single-use, handoff d'entité, état de groupe,
  transactions pendantes et repli sûr sont persistés. La sonde accepte le premier ticket puis
  refuse explicitement sa réutilisation.

## Transaction, permissions et préservation

- `NetworkPersistenceObservabilityHistory` impose Administrateur, dry-run, révision attendue et
  progression d'une seule révision ;
- l'approbation est liée à l'empreinte du brouillon et de la source, puis révoquée dès qu'un champ
  ou la révision change ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- la sonde manuelle exige une confirmation distincte et refuse tout rôle autre qu'Administrateur ;
- les services runtime restent en mémoire, les adaptateurs configurés appliquent réellement les
  allowlists et plafonds, puis toutes leurs ressources temporaires sont libérées ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont préservés.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **12/12** — Network 7, ModuleFormat 3 et MCP 2 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_D_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, snapshot, SQL,
  observabilité, profiler, reconnexion, audit, UI, préservation, restauration et libération à `true` ;
- cinq captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées : cartes, workflows, champs,
  limites, approbations, preuve et six actions lisibles, sans UUID ni clipping bloquant ;
- portail incrémental : **PASS en 534,22 s** — build 1,30 s, tests 2,51 + 2,26 + 2,52 s,
  smoke 413,51 s, capture 9,48 s, matrice 0,84 s, génération du graphe 49,54 s et
  `check --full` 52,20 s ;
- matrice Studio après promotion : **651 `functional` / 66 `planned`**, dont UX9 **19/68** ;
- graphe Roslyn officiel régénéré après promotion, sans diagnostic, frais et pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-d-persistence-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-d-sql-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-d-observability-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-d-profiler-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-d-reconnection-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-d-network-persistence-observability.summary.json`.

## Limites honnêtes

Studio n'ouvre aucun port, socket, endpoint Prometheus/OpenTelemetry, serveur public, base SQL ou
stockage relationnel distant. Les sondes utilisent les adaptateurs réels en mémoire et un workspace
temporaire supprimé ; elles ne qualifient pas un monde persistant public, une haute disponibilité
multi-processus, une base de production, un exporter externe ou une reconnexion distante réelle.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Sauvegarde serveur, rollback et file de connexion
restent au lot R124 comme prévu par le contrat source.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkPersistenceObservabilityProfileTests|' +
  'FullyQualifiedName~WorkspaceNetworkPersistenceObservabilityStoreTests|' +
  'FullyQualifiedName~NetworkPersistenceObservabilityAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-d-network-persistence-observability `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-d-network-persistence-observability-forward-plus-fhd.png' `
  -CheckGraph
```
