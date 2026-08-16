# Acceptation Studio — UX0-G Compléter les événements globaux du module

État local au 15 août 2026 : **fonctionnel et qualifié** pour les quatre identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle ne qualifie pas encore les
66 fenêtres historiques, les autres lignes de la matrice Studio, une distribution publique ou une
publication Git.

## Preuves communes

- builds `Forge.Studio` et `Forge.Runtime` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 8/8, Application 12/12 et ModuleFormat 15/15, soit 35/35 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX0G_PASS equipment_progression=true rest=true heartbeat=true
  user_event=true runtime_dispatch=true missing_script_blocked=true dry_run=true
  atomic_undo_redo=true persistence_roundtrip=true neighbors_preserved=true human_no_uuid=true
  ui=true ui_transaction=true cards=9 bindings=28 project_restored=true windows=1` ;
- smoke runtime réel, sortie 0 :
  `OPENRPG_FORGE_RUNTIME_UX0G_GLOBAL_EVENTS_PASS equipment_progression=typed-server
  rest=four-stage-resolved heartbeat=bounded-script-path
  user_event=namespaced-typed-script-path forgescript=compiled-executed` ;
- la même commande Application `SetModuleRuntimePoliciesHandler` prévisualise puis applique les
  événements de gameplay, le repos, les rythmes globaux et les signaux utilisateur ; l’opération
  entière entre dans `ProjectCommandHistory` et reste annulable/refaisable ;
- chaque chemin choisi est relatif, borné au dossier `scripts`, réellement présent et contient le
  point d’entrée sélectionné avant le dry-run ; un fichier absent est bloqué avec un message humain ;
- les structures existantes non affichées sont préservées : calendrier du monde, profils de règles,
  extensions, cinématiques, gameplay, abort de séquence et liaisons UX0-F ;
- le format projet `0.6.36` persiste chemin, handler et caractère requis pour repos, heartbeat et
  signaux utilisateur. La lecture d’un projet `0.6.35` initialise ces nouveaux champs comme
  facultatifs sans changer ses entrées ;
- journal headless : `artifacts/verification/studio-shell/ux0-g/headless-smoke.log` ;
- journal runtime : `artifacts/verification/studio-shell/ux0-g/runtime-smoke.log` ;
- journal Forward+ : `artifacts/verification/studio-shell/ux0-g/forwardplus.log`, Godot 4.7.1,
  AMD Radeon RX 7800 XT, Clairval à 291 entités, 278 assets gouvernés et zéro fallback ;
- captures Forward+ inspectées : `heartbeat-fhd.png`, `rest-fhd.png` et `user-event-qhd.png` dans
  `artifacts/verification/studio-shell/ux0-g/`.

La première inspection Full HD a détecté une aide comprimée sur une colonne d’un caractère dans la
carte heartbeat. Le texte reçoit maintenant l’espace horizontal restant et le focus de preuve utilise
le contrôle cible plutôt qu’une valeur de scroll arbitraire ; les captures FHD et QHD rejouées sont
lisibles et gardent toutes les actions principales visibles.

### ORF-PROJ-011

La carte **Équipement et progression** expose les deux événements serveur sans montrer
`equipment.changed`, `progression.changed` ni leur UUID. L’auteur choisit un script nommé pour
chaque événement et décide si ces changements sont synchronisés avec les joueurs.

`ProjectGameplayEventProfile` conserve les payloads typés et l’autorité serveur. Le smoke publie un
changement d’équipement et un changement de niveau dans `ProjectGameplayEventBus`; le runtime réel
résout et exécute la liaison de progression lorsqu’un producteur autoritaire la publie.

### ORF-PROJ-012

La carte **Repos du groupe** configure activation, participants, durée et quatre liaisons nommées :
demande, début, interruption et fin. `RestRuntimeCoordinator` produit ces transitions ; `GameRoot`
les publie, retrouve la liaison correspondante et compile/exécute le script choisi sous quota.

### ORF-PROJ-013

La carte **Rythmes globaux** permet d’ajouter, modifier et retirer plusieurs rythmes. Chaque ligne
nomme la cadence, le budget, la suspension en cas d’inactivité et le script sans afficher l’identité
interne. `HeartbeatScheduler` borne le rattrapage, transporte chemin/handler/caractère requis dans
`HeartbeatDispatch`, puis le runtime compile et exécute la liaison avec le budget de script commun.

### ORF-PROJ-014

La carte **Signaux utilisateur** crée et retire des événements à partir de trois modèles humains :
message texte, compteur ou cible du monde. Le Studio génère en interne le namespace, le nom sûr,
l’identité et le schéma de payload ; les structures existantes plus avancées sont affichées comme
« Structure existante » et conservées si elles ne sont pas modifiées.

`ProjectWorldProfileValidator` exige un payload typé, l’autorité serveur et un chemin sûr lorsque le
script est requis. `CustomWorldEventBus` vérifie la charge utile ; le runtime abonne chaque schéma au
chargement du module et exécute sa liaison quand le signal est publié.

## Limites honnêtes

- le sélecteur humain propose l’entrée conventionnelle `main`; les handlers automatiques existants
  restent préservés et le runtime adapte un handler nommé lorsqu’un fichier le déclare ;
- la démo runtime publie réellement la progression, tandis qu’aucune action de joueur de la démo ne
  change encore son équipement. Le contrat événementiel équipement est néanmoins typé, validé,
  persistant et couvert par le bus autoritaire ;
- les trois modèles de payload couvrent le parcours normal. Un schéma existant plus complexe est
  préservé, mais son édition champ par champ reste une surface avancée à migrer ;
- calendrier, abort de séquence, progression de règles et séquence de démarrage appartiennent aux
  prochains lots ; publication, signature et distributions restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter `
  'FullyQualifiedName~RuntimePolicyProfileTests|FullyQualifiedName~ProjectWorldProfileTests'
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter `
  'FullyQualifiedName~RuntimePolicyServicesTests|FullyQualifiedName~CustomWorldEventBusTests|FullyQualifiedName~SetRuntimePoliciesHandlerTests'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter `
  'FullyQualifiedName~ProjectGlobalEventPersistenceTests|FullyQualifiedName~ProjectLifecycleEventPersistenceTests|FullyQualifiedName~RulesProfilePersistenceTests|FullyQualifiedName~WorldFeaturePersistenceTests|FullyQualifiedName~QuestItemDepthPersistenceTests'
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux0-g-heartbeat -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Resolution 1920x1080 -Evidence ux0-g-rest `
  -Capture artifacts/verification/studio-shell/ux0-g/rest-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page zones -Resolution 2560x1440 -Evidence ux0-g-user-event `
  -Capture artifacts/verification/studio-shell/ux0-g/user-event-qhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX0-G, trente-quatre fonctions disposent désormais d’une preuve Studio spécifique ;
les 683 autres lignes restent explicitement `planned`.
