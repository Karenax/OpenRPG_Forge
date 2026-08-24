# Acceptation Studio — UX9-C Monde réseau et administration gouvernée

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **14/68** routes accessibles dans Studio.

La page `network-director` édite directement un `NetworkWorldAdministrationProfile` persistant.
L'humain choisit explicitement le module et l'un des cinq workflows, prévisualise la commande
canonique, approuve cette seule révision, applique, annule/rétablit, enregistre puis confirme une
sonde locale distincte. Le fichier `.forge/network/world-administration.json` n'est créé que sur
Enregistrer et l'écriture atomique utilise UTF-8 sans BOM.

## Routes couvertes

<a id="orf-mp-009"></a>
- **ORF-MP-009 — Instances de zones** : templates, plafond global, capacité par instance et délai
  de collecte sont configurables ; allocation de groupe, persistance de métadonnées et
  instanciation runtime restent imposées. La sonde enregistre un template, crée une instance puis
  y fait entrer un joueur du groupe.

<a id="orf-mp-010"></a>
- **ORF-MP-010 — Création dynamique de zone** : template, clone, allowlist de surcharges et budgets
  d'entités/mémoire sont persistés. `DynamicAreaRuntimeService` refuse les clés ou budgets hors
  politique et crée la zone sous `ZoneInstanceService` quand la demande est autorisée.

<a id="orf-mp-016"></a>
- **ORF-MP-016 — Authentification et comptes** : Local, OpenID Connect et Platform restent requis ;
  OAuth et Anonymous sont optionnels. Identité stable, confidentialité, secrets hachés et échecs
  non énumérables sont imposés ; la sonde enregistre et authentifie un compte local, puis vérifie
  que compte absent et mauvais secret produisent le même refus.

<a id="orf-mp-018"></a>
- **ORF-MP-018 — Administration à distance** : API/CLI, authentification, débit, audit, santé,
  joueurs, configuration et allowlist sont configurés dans le profil canonique. Le runtime applique
  réellement débit et commandes sûres ; le service MCP existant fournit lecture filtrée,
  plan/dry-run, apply, revert, rôle, portée serveur et audit. Studio teste ces autorités localement
  sans ouvrir lui-même de listener ni prétendre qu'un endpoint public est actif.

<a id="orf-mp-019"></a>
- **ORF-MP-019 — Modération** : compte, IP, appareil, mute, timeout, kick, ban, signalement, preuve,
  note d'appel et rétention privée bornée sont persistés. La sonde applique un mute via un rôle
  Modérateur, vérifie la restriction et l'unique entrée d'audit associée.

## Transaction, permissions et préservation

- `NetworkWorldAdministrationHistory` impose Administrateur, dry-run, révision attendue et
  progression d'une seule révision ;
- l'approbation est liée à l'empreinte du brouillon et de la source, puis révoquée dès qu'un champ
  ou la révision change ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- la sonde manuelle exige une confirmation distincte et refuse tout rôle autre qu'Administrateur ;
- les services runtime restent en mémoire, le workspace de preuve est supprimé et aucune connexion
  persistante n'est créée ; l'invariant de déconnexion est donc satisfait par l'absence de listener
  et la libération vérifiée de toutes les ressources ;
- projet, historique, dirty state, zone, sélection et approbation de la session source sont préservés.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **17/17** — Network 12, ModuleFormat 3 et MCP 2 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_C_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, cinq runtimes,
  audit, UI, préservation, restauration et libération à `true` ;
- cinq captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées : cartes, workflow, champs,
  limites, approbations, preuve et six actions lisibles, sans UUID ni clipping ;
- portail incrémental : **PASS en 548,58 s** — build 1,37 s, tests 2,46 + 2,24 + 2,49 s,
  smoke 426,45 s, capture 7,88 s, matrice 0,83 s, génération du graphe 51,61 s et
  `check --full` 53,20 s ;
- matrice Studio après promotion : **646 `functional` / 71 `planned`**, dont UX9 **14/68** ;
- graphe Roslyn officiel : **11 299 nœuds / 87 300 relations / 0 diagnostic**, frais et pleinement
  déterministe après `check --full`.

Artefacts :

- `artifacts/verification/studio-increment/ux9-c-instances-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-c-dynamic-areas-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-c-authentication-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-c-administration-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-c-moderation-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-c-network-world-administration.summary.json`.

## Limites honnêtes

Studio n'ouvre aucun port, socket, compte externe ou serveur public. Les sondes utilisent les
adaptateurs réels en mémoire et le service MCP autoritaire existant ; elles ne qualifient pas un
transport distant inter-processus, une identité OAuth réelle, une base SQL ou un monde persistant
public. Les routes correspondantes non couvertes par UX9-C restent `planned`.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
  'tests\Forge.Mcp.Tests\Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkWorldAdministrationProfileTests|' +
  'FullyQualifiedName~OperationsChatPersistenceTests|' +
  'FullyQualifiedName~WorkspaceNetworkWorldAdministrationStoreTests|' +
  'FullyQualifiedName~NetworkWorldAdministrationAgentServiceTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-c-network-world-administration `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-c-network-world-administration-forward-plus-fhd.png' `
  -CheckGraph
```
