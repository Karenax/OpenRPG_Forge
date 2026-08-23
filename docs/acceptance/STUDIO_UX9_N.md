# Acceptation Studio — UX9-N Production réseau avancée

État local au 23 août 2026 : **fonctionnel et qualifié** pour six routes UX9. La vague Exploiter
en réseau atteint **64/68** routes accessibles dans Studio.

La page `network-director` édite directement les profils canoniques `GameplayProductionProfile`
et `NetworkProductionProfile` du module dans une surface humaine unique. L’utilisateur choisit
explicitement le module, configure le contenu curaté, la voix, un shard, le hot reload, les
extensions serveur et le protocole, prévisualise la commande canonique, approuve cette seule
révision, applique, annule/rétablit, enregistre puis confirme une sandbox locale distincte en rôle
Administrateur. Aucun document parallèle n’est créé : `project.json` persiste les politiques avec
les autres extensions runtime.

## Routes couvertes

<a id="orf-mp-014"></a>
- **ORF-MP-014 — Contenu curaté** : canal et compatibilité nommés, liste de licences normalisée,
  signature fiable exigible et repli hors ligne explicite. Le résolveur accepte une licence
  allowlistée et refuse réellement un signataire non fiable.

<a id="orf-mp-021"></a>
- **ORF-MP-021 — Voix** : activation, codec `opus` ou `pcm`, débit de 8 à 512 kb/s, position,
  push-to-talk et modération. La sandbox restitue la décision et le débit gouvernés sans ouvrir de
  périphérique audio ni de canal vocal.

<a id="orf-mp-024"></a>
- **ORF-MP-024 — Sharding et zones serveur** : shard optionnel à nom normalisé, zone existante,
  capacité de 1 à 100 000 joueurs et région explicite. La zone reste une référence canonique du
  module et aucune instance serveur n’est lancée.

<a id="orf-mp-025"></a>
- **ORF-MP-025 — Hot reload de contenu serveur** : activation avec paquet borné, drain de 1 à
  3 600 secondes, bascule atomique et rollback obligatoires. `ServerProductionService` exerce
  aperçu, application révisionnée, empreinte SHA-256, refus stale et anti-rejeu dans l’isolat.

<a id="orf-mp-026"></a>
- **ORF-MP-026 — Extensibilité serveur** : capacités allowlistées et budget CPU de 1 à 10 000 ms,
  avec signature et isolation toujours requises lorsque la politique est active. La négociation
  filtre les capacités et bloque une extension non signée sans démarrer de processus.

<a id="orf-mp-035"></a>
- **ORF-MP-035 — Protocole cross-platform** : famille, plage de versions ordonnée, plateformes
  normalisées et refus des champs inconnus. La négociation choisit la version commune et refuse une
  plateforme non supportée.

## Transaction, permissions et préservation

- `SetRuntimeExtensionsHandler` exécute dry-run puis mutation canonique dans
  `ProjectCommandHistory` ; aucune écriture métier directe n’est utilisée ;
- l’approbation est liée à la révision, à l’empreinte du projet complet et à l’empreinte du
  brouillon, puis révoquée dès qu’un champ ou la source change ;
- les bornes UI et les constructeurs du domaine refusent licence/capacité vide, débit hors plage,
  shard sans zone, paquet ou drain nul, extension sans capacité et versions invalides ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- Live Director, expérience Creator, automatisation, documents d’extension et politiques runtime
  voisines sont comparés à un projet attendu et préservés ;
- `PostLaunchCapabilityResolver` et `ServerProductionService` exercent les décisions réelles sur
  contenu fiable/non fiable, négociation signée/non signée et hot reload révisionné ;
- projet, historique, dirty state, piles undo/redo, zone, sélection et approbation de la session
  source sont préservés, puis les workspaces temporaires sont supprimés.

## Preuves exécutées

- builds `Forge.Studio` Debug et Release : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **8/8** — Network 4, Domain G13AG–AI 3 et ModuleFormat 1 ;
- régression complète `Forge.Network.Tests` : **99/99** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_N_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, contenu, voix,
  shard, hot reload, extensions, protocole, voisins, UI, préservation, restauration et libération à
  `true` ;
- capture Godot 4.7.1 Vulkan Forward+ **1920×1080** inspectée : six cartes et tous leurs champs,
  deux confirmations, aperçu, journal, avertissement, bouton **Fermer** et six actions sont visibles
  sans UUID, chevauchement ni clipping ;
- portail incrémental : **PASS en 507,54 s** — build 1,28 s, tests 1,95 + 1,39 + 3,81 s,
  smoke 390,91 s, capture 8,40 s, matrice 0,79 s, génération du graphe 48,87 s et
  `check --full` 50,09 s ;
- matrice Studio après promotion : **696 `functional` / 21 `planned`**, dont UX9 **64/68** ;
- graphe Roslyn du portail : **11 528 nœuds / 89 644 relations / 0 diagnostic** ; régénération finale
  fraîche et déterministe après promotion.

Artefacts :

- `artifacts/verification/studio-increment/ux9-n-network-production-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-n-network-production.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant, périphérique audio, compte ou
service externe. La sandbox utilise un projet, une zone, un shard, une empreinte et des négociations
synthétiques en mémoire, puis supprime son workspace temporaire. Elle ne télécharge ni ne charge de
pack curaté, ne vérifie pas une signature cryptographique réelle, ne transfère pas d’audio, ne
déploie pas de shard, ne remplace aucun paquet d’un serveur en cours et ne lance aucune extension.
Le profil et les décisions de gouvernance sont réels, mais transport, charge distribuée, bascule
inter-processus, compatibilité entre exécutables et isolation OS restent à qualifier séparément.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Les quatre routes UX9 encore planifiées restent
hors de UX9-N.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ProductionNetworkTests|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AG|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AH|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AI|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests.Post_launch_capabilities_round_trip'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-n-network-production `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-n-network-production-forward-plus-fhd.png' `
  -CheckGraph
```
