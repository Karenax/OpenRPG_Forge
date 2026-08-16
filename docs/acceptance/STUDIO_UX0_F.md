# Acceptation Studio — UX0-F Relier les événements de vie du module

État local au 15 août 2026 : **fonctionnel et qualifié** pour les cinq identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle ne qualifie pas encore les
66 fenêtres historiques, les autres lignes de la matrice Studio, une distribution publique ou une
publication Git.

## Preuves communes

- builds `Forge.Studio` et `Forge.Runtime` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 21/21, Application 8/8 et ModuleFormat 2/2, soit 31/31 ;
- portail intégré : `OPENRPG_FORGE_VERIFICATION=PASS`, avec 1 933 tests .NET et 39 tests Python ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX0F_PASS script_discovery=true missing_script_blocked=true
  lifecycle_contexts=true gameplay_groups=true server_authority=true dry_run=true
  atomic_undo_redo=true persistence_roundtrip=true neighbors_preserved=true human_no_uuid=true
  ui=true ui_transaction=true cards=5 bindings=20 project_restored=true windows=1` ;
- smoke runtime réel :
  `OPENRPG_FORGE_RUNTIME_R025_PROJECT_OPERATIONS_PASS ... scripts=context-bound` et
  `OPENRPG_FORGE_RUNTIME_R051_PROJECT_EVENTS_PASS ... scripts=path-handler-resolved` ;
- le sélecteur intégré recherche uniquement `scripts/**/*.forge`, refuse les chemins dangereux,
  ignore les fichiers de plus de 1 Mio ou sans entrée `void main(...)`, humanise leurs noms et ne
  montre ni chemin, ni UUID, ni hash, ni JSON dans le parcours normal ;
- un script supprimé après sélection ou un handler absent est refusé avant le dry-run avec un
  diagnostic humain ;
- une seule commande Application reconstruit les profils de cycle de vie et d’événements de jeu,
  puis entre dans `ProjectCommandHistory` pour l’application, undo et redo ;
- le smoke crée un workspace temporaire, configure les vingt liaisons visibles, sauvegarde, ferme,
  rouvre et compare les valeurs avant de supprimer le dossier de preuve ;
- les profils voisins de rendu, règles, monde, extensions et gameplay sont conservés, tout comme les
  deux hooks équipement/progression que ce lot n’affiche pas ;
- le format projet `0.6.35` conserve chemin, handler et caractère requis. La lecture d’un projet
  `0.6.34` recrée les huit liaisons de cycle de vie de manière déterministe et laisse les chemins
  absents facultatifs ;
- graphe Roslyn régénéré depuis les sources : 8 566 nœuds, 54 234 relations et zéro diagnostic ;
- journal headless : `artifacts/verification/studio-shell/ux0-f/headless-smoke.log` ;
- journal runtime : `artifacts/verification/studio-shell/ux0-f/runtime-smoke.log` ;
- journal Forward+ : `artifacts/verification/studio-shell/ux0-f/forwardplus.log`, avec Clairval à
  291 entités, 278 assets gouvernés et zéro aperçu de secours ;
- captures Forward+ inspectées : `lifecycle-qhd.png`, `life-fhd.png` et `script-picker-fhd.png` dans
  `artifacts/verification/studio-shell/ux0-f/`.

Le smoke headless utilise volontairement le renderer de compatibilité et ne sert pas de preuve
visuelle. Les captures ont été produites par le Studio rendu en Forward+ sur le GPU AMD Radeon
RX 7800 XT. La première inspection Full HD a révélé des actions de ligne comprimées ; elles ont été
replacées dans chaque ligne, puis les vues Full HD et QHD ont été rejouées et acceptées.

### ORF-PROJ-006

La page **Projet / Événements du module** expose deux cartes de cycle de vie : chargement du module
et démarrage du monde. Chacune présente quatre contextes nommés — Éditeur, Solo, Client et Serveur —
soit huit liaisons distinctes. L’auteur choisit un script par son nom humain, peut revenir au
comportement automatique et peut marquer chaque liaison comme indispensable au runtime.

`ProjectLifecycleEventProfile` porte l’identité stable, l’API ForgeScript, le handler, le chemin
relatif sûr et le caractère requis. `RuntimeLifecycleCoordinator` résout la liaison correspondant à
l’événement et au contexte, puis joint le chemin, le handler et le caractère requis à l’événement
runtime émis. Une configuration requise absente ou invalide produit un diagnostic stable.

Route : `StudioProjectEventsPage` → `StudioSessionController.ApplyProjectEventConfiguration` →
`SetModuleRuntimePoliciesHandler` → `ProjectLifecycleEventProfile` → sérialisation du projet →
`RuntimeLifecycleCoordinator`.

### ORF-PROJ-007

La carte **Connexion des joueurs** expose quatre moments nommés : arrivée, prêt à jouer, départ
annoncé et déconnexion. Les quatre bindings restent sous autorité serveur. Le bus canonique refuse
un événement falsifié avec une autorité locale, journalise la révision et restitue le handler associé.

### ORF-PROJ-008

La carte **Inventaire** expose le hook unique de changement d’inventaire, qui transporte le type de
variation acquis/perdu dans son payload canonique. L’interface ne demande pas à l’auteur de saisir
le nom technique `inventory.changed` ; le script est choisi dans le même sélecteur intégré.

### ORF-PROJ-009

La carte **Utilisation et capacités** expose l’utilisation d’un objet et l’activation d’une capacité.
Le dry-run valide l’existence du fichier et de l’entrée configurée avant toute mutation. Le runtime
résout le handle typé du profil et le bus publie les événements canoniques sous autorité serveur.

### ORF-PROJ-010

La carte **Vie, mort et retour en jeu** expose cinq étapes : à terre, en train de mourir, mort,
demande de retour et retour effectué. Deux options nommées règlent l’état « à terre » et le retour
automatique. Le smoke vérifie la séquence complète, la désactivation des deux options, la liaison
requise de la mort, puis le round-trip sauvegarde/réouverture.

## Limites honnêtes

- ce lot configure et résout des liaisons ForgeScript typées ; il ne revendique pas encore un nouvel
  ordonnanceur exécutant automatiquement chaque script depuis le bus d’événements ;
- le sélecteur humain propose actuellement l’entrée conventionnelle `main`. Les handlers automatiques
  existants restent préservés et un handler configuré doit réellement être déclaré dans son fichier ;
- les hooks équipement et progression sont compris par le domaine et préservés mais ne sont pas
  promus ici : ils appartiennent à `ORF-PROJ-011` ;
- repos, heartbeat global, événement utilisateur, publication, signature et qualification de
  distribution sont hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter `
  'FullyQualifiedName~ProjectLifecycleEventProfileTests|FullyQualifiedName~ProjectGameplayEventProfileTests|FullyQualifiedName~RuntimeLifecycleCoordinatorTests'
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter `
  'FullyQualifiedName~SetRuntimePoliciesHandlerTests|FullyQualifiedName~ProjectGameplayEventBusTests'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter `
  'FullyQualifiedName~ProjectLifecycleEventPersistenceTests'
& .\scripts\Open-Studio.ps1 -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 2560x1440 -Evidence ux0-f-lifecycle `
  -Capture artifacts/verification/studio-shell/ux0-f/lifecycle-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 1920x1080 -Evidence ux0-f-picker `
  -Capture artifacts/verification/studio-shell/ux0-f/script-picker-fhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX0-F, trente fonctions disposent désormais d’une preuve Studio spécifique ; les
687 autres lignes restent explicitement `planned`.
