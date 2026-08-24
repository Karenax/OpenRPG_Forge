# Acceptation Studio — UX9-O Clôture réseau

État local au 23 août 2026 : **fonctionnel et qualifié** pour les quatre dernières routes UX9.
La vague Exploiter en réseau est close à **68/68** routes accessibles dans Studio.

La page `network-director` réunit quatre cartes directes pour le serveur dédié, le PvP et le tir
ami, les journaux structurés et l’anti-triche. Elle coordonne les profils canoniques
`NetworkFoundationProfile`, `NetworkPlayerExperienceProfile` et `NetworkAssuranceProfile` sans
modifier le projet métier ouvert. L’utilisateur sélectionne explicitement le module, prévisualise
les trois commandes canoniques, approuve les trois révisions et leurs empreintes, applique les seuls
groupes modifiés, annule/rétablit, enregistre chaque document atomiquement puis confirme une sandbox
locale séparée en rôle Administrateur.

## Routes couvertes

<a id="orf-mp-001"></a>
- **ORF-MP-001 — Serveur dédié** : nom, adresse locale, port, capacité, exigence runtime, mode
  console ou service Windows et chemin de santé. Le profil impose un hôte headless sans dépendance
  éditeur ; la sandbox sérialise la configuration, construit l’adaptateur runtime, connecte puis
  déconnecte une session synthétique sans ouvrir de port ni installer de service.

<a id="orf-mp-008"></a>
- **ORF-MP-008 — PvP et friendly fire** : activation PvP et tir ami complètent les politiques
  globale, zone, groupe, consentement, duel, crime, zones sûres et priorité serveur. `PartyService`
  et `CombatAuthority` vérifient réellement le refus d’un allié selon la politique et
  l’autorisation d’une cible hostile.

<a id="orf-mp-029"></a>
- **ORF-MP-029 — Logs structurés** : sévérité, catégories, confidentialité joueur, formats JSON et
  texte, rétention, capacité, rotation et sink nommé. `StructuredLogRouter` produit les sorties,
  retire jeton et identité joueur, puis livre vers un sink mémoire selon la politique.

<a id="orf-mp-031"></a>
- **ORF-MP-031 — Anti-cheat par conception** : vitesses et intentions bornées, seuil d’anomalie,
  contenu signé, empreintes fiables, RNG autoritaire et sauvegardes client non fiables.
  `AntiCheatService` accepte une jointure signée, bloque un déplacement forgé et une sauvegarde
  client, puis reproduit le tirage autoritaire déterministe.

## Transaction, permissions et persistance

- les trois historiques de domaine exécutent un dry-run canonique avant toute application ;
- l’approbation est liée aux trois révisions, aux trois empreintes de source et à l’empreinte du
  brouillon ; une source ou un champ modifié la rend caduque ;
- seul le rôle Administrateur peut prévisualiser et appliquer, et la sandbox demande une
  confirmation distincte ;
- si une commande coordonnée échoue, les groupes déjà appliqués sont compensés ; undo/redo restent
  coordonnés dans l’ordre inverse puis direct ;
- `.forge/network/foundation.json`, `.forge/network/player-experience.json` et
  `.forge/network/assurance.json` sont écrits sans BOM par remplacement atomique de chaque fichier,
  relus puis comparés à leur sérialisation canonique exacte ;
- autorité, groupe, réplication, intérêt, découverte, lobby, constructeur de personnage, autorité
  de personnage et tests déterministes voisins sont comparés par empreinte sémantique et préservés ;
- projet, historique, dirty state, piles undo/redo, zone, sélection et approbations de la session
  source sont restaurés, puis tous les workspaces et handles synthétiques sont libérés.

## Preuves exécutées

- builds `Forge.Studio` Debug et Release : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **37/37** — Network 28 et ModuleFormat 9 ;
- régressions complètes : **99/99** `Forge.Network.Tests` et **385/385**
  `Forge.ModuleFormat.Tests` ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_O_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commandes, gardes stale, undo/redo, persistance, serveur dédié,
  combat, logs, anti-triche, voisins, UI, préservation, restauration et libération à `true` ; le
  shell complet termine aussi par `OPENRPG_FORGE_STUDIO_SHELL_PASS` ;
- capture Godot 4.7.1 Vulkan Forward+ **1920×1080** corrigée puis inspectée : les quatre cartes,
  leurs champs, les deux confirmations, l’aperçu, le journal, l’avertissement, **Fermer** et toute la
  rangée d’actions restent visibles sans chevauchement ni libellé tronqué ;
- portail incrémental : **PASS en 515,32 s** — build 12,74 s, tests 1,98 + 2,38 s, smoke
  390,90 s, capture 8,84 s, matrice 0,83 s, génération du graphe 49,22 s et `check --full`
  48,38 s ;
- matrice Studio après promotion : **700 `functional` / 17 `planned`**, dont UX9 **68/68** ;
- graphe Roslyn du portail : **11 547 nœuds / 89 923 relations / 0 diagnostic** ; graphe final après
  promotion : **11 548 nœuds / 89 923 relations / 0 diagnostic**, frais et déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-o-network-closure-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-o-network-closure.summary.json`.

## Limites honnêtes

Studio n’installe ni service Windows, ni serveur dédié persistant et n’ouvre aucun socket, port,
compte, certificat ou sink distant. La sandbox utilise des sessions, joueurs, empreintes, événements
et sinks synthétiques en mémoire, puis les détruit. Elle ne qualifie pas le débit distribué, la
latence réseau réelle, le transport inter-processus, une infrastructure d’observabilité distante,
une signature de contenu de production ou la résistance à un attaquant réel. Les politiques,
validations, commandes, décisions runtime et fichiers workspace sont réels ; la qualification
externe et la release publique signée restent séparées.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une campagne
multijoueur distante, ni une qualification de service Windows ou de sécurité offensive.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.Network.Tests\Forge.Network.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~NetworkFoundationProfileTests|' +
  'FullyQualifiedName~NetworkPlayerExperienceProfileTests|' +
  'FullyQualifiedName~NetworkAssuranceProfileTests|' +
  'FullyQualifiedName~WorldAuthorityTests|' +
  'FullyQualifiedName~WorkspaceNetworkFoundationStoreTests|' +
  'FullyQualifiedName~WorkspaceNetworkPlayerExperienceStoreTests|' +
  'FullyQualifiedName~WorkspaceNetworkAssuranceStoreTests'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-o-network-closure `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-o-network-closure-forward-plus-fhd.png' `
  -CheckGraph
```
