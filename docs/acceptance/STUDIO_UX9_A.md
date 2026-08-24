# Acceptation Studio — UX9-A Session réseau locale autoritaire

État local au 22 août 2026 : **fonctionnel et qualifié** pour quatre routes UX9. La vague Exploiter
en réseau atteint **4/68** routes accessibles dans Studio.

La page `network-director` configure un `NetworkFoundationProfile` persistant, exige une sélection
de module, produit un aperçu sec, puis peut ouvrir une session autoritaire réelle dans le processus
Studio sur un profil strictement loopback. La connexion opérateur est toujours déconnectable et son
journal ne montre aucun identifiant technique.

## Routes couvertes

<a id="orf-mp-002"></a>
- **ORF-MP-002 — Serveur autoritaire** : les clients envoient uniquement des intentions ; chaque
  mutation est validée par `AuthoritativeSession`, l'état accepté est répliqué et les refus sont
  audités.

<a id="orf-mp-007"></a>
- **ORF-MP-007 — Parties et groupes** : invitation, acceptation, rôles, capacité, butin, partage de
  quêtes et marqueurs ainsi que politique de séparation passent par `PartyService`.

<a id="orf-mp-011"></a>
- **ORF-MP-011 — Réplication d’entités** : composants allowlistés, budget de delta, compression,
  prédiction propriétaire et réconciliation sont exercés par `ReplicationService`.

<a id="orf-mp-012"></a>
- **ORF-MP-012 — Interest management** : zone, distance, visibilité, groupe, propriété et
  abonnements explicites alimentent réellement le calcul du delta observateur.

## Permission, confirmation, audit et déconnexion

- l'aperçu passe par `NetworkFoundationHistory` avec rôle Administrateur, dry-run et révision
  attendue ;
- l'application revérifie le brouillon et l'empreinte de la source approuvée ;
- undo/redo et sauvegarde atomique dans `.forge/network/foundation.json`, puis réouverture canonique ;
- le démarrage local exige une confirmation humaine distincte et refuse un rôle non administrateur ;
- la sonde connecte une identité locale, refuse une mutation cliente interdite, crée un groupe,
  réplique une entité et vérifie le filtrage d'intérêt ;
- `AuthoritativeSession.Disconnect` retire réellement le membre, avance la révision et produit une
  entrée d'audit ; la fermeture de la surface libère aussi toute session encore active ;
- workspace de preuve, projet, historique, dirty state, zone, sélection et approbation source sont
  préservés puis la copie temporaire est supprimée.

## Preuves exécutées

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **17/17** — Network 14 et ModuleFormat 3 ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_A_PASS` avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, autorité,
  groupe, réplication, intérêt, déconnexion, audit, UI, préservation et libération à `true` ;
- capture Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectée après correction : quatre cartes,
  limites loopback, consentements, aperçu et sept actions visibles, sans UUID ni clipping ;
- portail incrémental : **PASS en 643,09 s** — build 5,47 s, tests 6,25 + 7,50 s, smoke 449,50 s,
  capture 9,52 s, matrice 1,04 s, génération du graphe 82,44 s et `check --full` 81,30 s ;
- matrice Studio après promotion : **636 `functional` / 81 `planned`**, dont UX9 **4/68**.
- graphe Roslyn officiel : **11 253 nœuds / 86 841 relations / 0 diagnostic**, frais et pleinement
  déterministe après `check --full`.

Artefacts :

- `artifacts/verification/studio-increment/ux9-a-network-foundation-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-a-network-foundation.summary.json`.

## Limites honnêtes

La session vit dans le processus Studio et ne publie aucun port. Elle ne constitue pas un serveur
dédié inter-processus, un transport client distant, un hébergement public ou un monde persistant.
`ORF-MP-001 — Serveur dédié` reste donc `planned`. Aucun compte réel, secret, annuaire, NAT,
service Windows, réseau externe, commande Git ou publication n'est utilisé.

Le portail conserve `full_gate=false` malgré `graph_check=true` ; il ne remplace ni
`scripts/Verify.ps1`, ni une qualification réseau inter-processus, ni une release publique signée.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~AuthoritativeSessionTests|' +
  'FullyQualifiedName~NetworkFoundationProfileTests|' +
  'FullyQualifiedName~WorkspaceNetworkFoundationStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-a-network-foundation `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-a-network-foundation-forward-plus-fhd.png' `
  -CheckGraph
```
