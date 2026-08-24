# Acceptation Studio — UX9-M Interventions et enregistrement Live Director

État local au 23 août 2026 : **fonctionnel et qualifié** pour cinq routes UX9. La vague Exploiter
en réseau atteint **58/68** routes accessibles dans Studio.

La page `network-director` édite directement le `LiveDirectorProductionProfile` canonique du
module dans une surface humaine unique. L’utilisateur choisit explicitement le module, configure
repos forcé, ajustement de niveau, apparence, groupe sauvegardé et enregistrement, prévisualise la
commande canonique, approuve cette seule révision, applique, annule/rétablit, enregistre puis
confirme une sandbox locale distincte en rôle Administrateur. Aucun fichier parallèle n’est créé :
la sauvegarde atomique du projet persiste le profil avec les autres politiques runtime.

## Routes couvertes

<a id="orf-dm-015"></a>
- **ORF-DM-015 — Repos forcé** : activation et plafond de 1 à 256 cibles. Le service exige
  permission de gestion du monde, révision attendue et motif non vide, puis produit aperçu,
  changement d’état et entrée d’audit.

<a id="orf-dm-018"></a>
- **ORF-DM-018 — Ajouter ou retirer niveau** : amplitude bornée de 1 à 100, confirmation explicite,
  reconstruction réglée par le profil et snapshot de retour obligatoire lorsqu’il est demandé.

<a id="orf-dm-030"></a>
- **ORF-DM-030 — Changer apparence** : échelles minimum et maximum ordonnées entre 0,1 et 10,
  durée gouvernée et validation des assets conservée. La sandbox applique une échelle comprise
  dans les bornes sans charger de ressource graphique.

<a id="orf-dm-022"></a>
- **ORF-DM-022 — Groupes sur raccourcis** : nom, requête, raccourci unique de 1 à 12 et plafond de
  1 à 512 membres. Le résolveur déduplique la sélection synthétique avant l’enregistrement.

<a id="orf-dm-037"></a>
- **ORF-DM-037 — Enregistrement de session** : activation, export `jsonl` ou `orp-replay`, durée de
  1 à 1 440 minutes et expurgation des données privées. La preuve injecte une identité
  d’enregistrement déterministe et refuse le rôle Joueur.

## Transaction, permissions et préservation

- `SetRuntimeExtensionsHandler` exécute le dry-run puis la mutation canonique dans
  `ProjectCommandHistory` ; aucune écriture métier directe n’est utilisée ;
- l’approbation est liée à la révision, à l’empreinte du projet complet et à l’empreinte du
  brouillon, puis révoquée dès qu’un champ ou la source change ;
- les bornes UI et les constructeurs du domaine refusent plafond nul, échelles inversées, requête
  vide, raccourci hors plage, format inconnu ou durée invalide ;
- undo/redo, sauvegarde atomique et réouverture comparent le JSON canonique exact ;
- gameplay, réseau, expérience Creator, automatisation, documents d’extension et politiques
  runtime voisines sont comparés à un projet attendu et préservés ;
- `LiveProductionService` et `PostLaunchCapabilityResolver` exercent les vraies autorités runtime
  sur un Administrateur, un Joueur, une zone et une créature synthétiques ;
- projet, historique, dirty state, piles undo/redo, zone, sélection et approbation de la session
  source sont préservés, puis les workspaces temporaires sont supprimés.

## Preuves exécutées

- builds `Forge.Studio` Debug et Release : **0 avertissement, 0 erreur** ;
- tests .NET ciblés : **7/7** — LiveDirector 4, Domain 2 et ModuleFormat 1 ;
- régression complète `Forge.LiveDirector.Tests` : **60/60** ;
- smoke Godot global : `OPENRPG_FORGE_STUDIO_UX9_M_PASS`, avec routes, sélection, invalidité,
  permission, confirmation, dry-run, commande, gardes stale, undo/redo, persistance, repos, niveau,
  apparence, groupe, enregistrement, confidentialité, voisins, UI, préservation, restauration et
  libération à `true` ;
- première capture FHD rejetée car les cartes repoussaient les actions hors écran ; contrainte
  verticale corrigée, rebuild effectué, puis capture dédiée et capture du portail Godot 4.7.1
  Vulkan Forward+ **1920×1080** réinspectées : cinq cartes et tous leurs champs, deux confirmations,
  aperçu, journal, avertissement, bouton **Fermer** et six actions sont visibles sans UUID,
  chevauchement ni clipping ;
- portail incrémental : **PASS en 507,90 s** — build 1,12 s, tests 2,16 + 1,41 + 3,86 s,
  smoke 393,62 s, capture 8,27 s, matrice 0,81 s, génération du graphe 48,96 s et
  `check --full` 47,64 s ;
- matrice Studio après promotion : **690 `functional` / 27 `planned`**, dont UX9 **58/68** ;
- graphe Roslyn officiel final : **11 515 nœuds / 89 467 relations / 0 diagnostic**, frais et
  pleinement déterministe.

Artefacts :

- `artifacts/verification/studio-increment/ux9-m-live-director-production-forward-plus-fhd.png` ;
- `artifacts/verification/studio-increment/ux9-m-live-director-production.summary.json`.

## Limites honnêtes

Studio n’ouvre aucun port, socket, serveur public, client distant ou compte externe. La sandbox
utilise un Administrateur, un Joueur, une zone, une créature, un groupe et un enregistrement
synthétiques en mémoire, puis libère le workspace temporaire ; elle ne dirige pas une partie
réelle et ne qualifie ni transport réseau, synchronisation inter-processus, stockage vidéo/audio,
archive distante, confidentialité d’un service exposé ou restitution d’un replay dans le runtime.
Le profil gouverne des décisions réelles, mais cette surface ne charge ni asset d’apparence ni
snapshot de monde. L’ancien Toolset reste disponible.

Le portail conserve `full_gate=false` ; il ne remplace ni `scripts/Verify.ps1`, ni une qualification
réseau distante, ni une release publique signée. Les dix routes UX9 encore planifiées restent hors
de UX9-M.

## Reproduction

```powershell
$projects = @(
  'tests\Forge.LiveDirector.Tests\Forge.LiveDirector.Tests.csproj'
  'tests\Forge.Domain.Tests\Forge.Domain.Tests.csproj'
  'tests\Forge.ModuleFormat.Tests\Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~LiveProductionServiceTests|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AE|' +
  'FullyQualifiedName~PostLaunchCapabilityResolverTests.G13AF|' +
  'FullyQualifiedName~RuntimeExtensionPersistenceTests.Post_launch_capabilities_round_trip'

.\scripts\Verify-StudioIncrement.ps1 `
  -Page network-director `
  -Evidence ux9-m-live-director-production `
  -TestProject $projects `
  -TestFilter $filter `
  -Capture 'artifacts\verification\studio-increment\ux9-m-live-director-production-forward-plus-fhd.png' `
  -CheckGraph
```
