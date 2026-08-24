# Acceptation Studio — UX9-B Opérations réseau locales gouvernées

État local au 22 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **9/68** routes accessibles dans Studio.

La page `network-director` édite directement un `NetworkOperationsProfile` persistant. Le module
est sélectionné explicitement ; un aperçu sec valide la révision et le brouillon, puis cinq sondes
locales exercent les autorités de domaine avant que l'application soit autorisée. Aucune sonde ne
publie de port ou ne conserve une connexion : elle rend la main seulement après suppression de son
cache temporaire.

## Routes couvertes

<a id="orf-mp-013"></a>
- **ORF-MP-013 — Synchronisation de contenu** : l'humain règle blocs, cache, deltas et licences ;
  `ContentRepositorySyncService` planifie, reprend un fichier partiel, vérifie manifest, empreinte,
  licence et signataire, puis réutilise réellement le cache adressé par contenu.

<a id="orf-mp-015"></a>
- **ORF-MP-015 — Version locking** : module, version, protocole et empreinte SHA-256 sont verrouillés
  avant admission ; la sonde accepte la correspondance exacte et refuse une version divergente avec
  un diagnostic lisible.

<a id="orf-mp-017"></a>
- **ORF-MP-017 — Rôles et permissions** : les six rôles, capacités standard, capacités nommées et
  portée de zone sont éditables ; `AuthorizationService` applique désormais les grants du profil
  runtime au lieu d'une table implicite indépendante.

<a id="orf-mp-020"></a>
- **ORF-MP-020 — Chat** : rayon local, longueur, langues, filtre et canaux optionnels alimentent
  réellement `ChatService`; canaux requis, journal structuré, texte accessible et bridge d'audit
  restent imposés, tandis qu'un canal désactivé est refusé.

<a id="orf-mp-027"></a>
- **ORF-MP-027 — Bus d'événements serveur** : limite de file, abonnement nommé et priorité sont
  persistés ; les handlers configurés sont ordonnés, tracés et la répétition d'un même événement est
  refusée par la protection anti-rejeu.

## Transaction, permission et libération

- `NetworkOperationsHistory` impose Administrateur, dry-run, révision attendue et progression d'une
  seule révision ;
- l'application revérifie le brouillon et l'empreinte de la source approuvée ;
- undo/redo, sauvegarde atomique UTF-8 sans BOM dans `.forge/network/operations.json`, puis
  réouverture canonique ;
- la sonde manuelle exige une confirmation distincte et refuse un rôle non administrateur ;
- le cache de synchronisation, le workspace de preuve et toutes les ressources locales sont
  supprimés avant le retour ; comme aucune connexion persistante n'est créée, il n'existe aucun
  état réseau caché à déconnecter ;
- projet, historique, dirty state, zone, sélection et approbation source sont préservés.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **25/25** — Network 22 et ModuleFormat 3 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_B_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, contenu,
  version, rôles, chat, événements, audit, UI, préservation et libération à `true` ;
- cinq captures Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectées : cartes, champs, consentements,
  limites et six actions visibles, sans UUID ni clipping ;
- portail incrémental : **PASS en 532,65 s** — build 1,13 s, tests 2,00 + 2,40 s, smoke
  414,56 s, capture 8,06 s, matrice 0,82 s, génération du graphe 52,71 s et `check --full`
  50,92 s ;
- matrice Studio après promotion : **641 `functional` / 76 `planned`**, dont UX9 **9/68**.
- graphe Roslyn officiel : **11 276 nœuds / 87 071 relations / 0 diagnostic**, frais et pleinement
  déterministe après `check --full`.

Artefacts :

- `artifacts/verification/studio-increment/ux9-b-network-operations-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-b-version-lock-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-b-roles-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-b-chat-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-b-event-bus-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-b-network-operations.summary.json`.

## Limites honnêtes

La synchronisation porte sur un transport mémoire et un cache temporaire de preuve ; le chat et le
bus vivent dans le processus Studio. Il n'existe encore ni transport client distant, serveur dédié
inter-processus, dépôt de contenu distant, compte réel, bridge externe, voix, administration
publique ou monde persistant. `ORF-MP-001` ainsi que les routes adjacentes restent `planned`.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau inter-processus, ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkOperationsProfileTests|' +
  'FullyQualifiedName~ContentRepositorySynchronizationTests|' +
  'FullyQualifiedName~OperationsChatPersistenceTests|' +
  'FullyQualifiedName~StorageEventsObservabilityTests|' +
  'FullyQualifiedName~WorkspaceNetworkOperationsStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-b-network-operations `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-b-network-operations-forward-plus-fhd.png' `
  -CheckGraph
```
