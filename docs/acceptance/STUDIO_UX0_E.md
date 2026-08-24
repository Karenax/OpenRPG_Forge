# Acceptation Studio — UX0-E Créer un projet sans identifiants techniques

État local au 15 août 2026 : **fonctionnel et qualifié** pour les cinq identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle ne qualifie pas encore les
66 fenêtres historiques, les autres lignes de la matrice Studio, une distribution publique ou une
publication Git.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Application 14/14, Domain 3/3 et ModuleFormat 17/17, soit 34/34 ;
- portail intégré : `OPENRPG_FORGE_VERIFICATION=PASS`, avec 1 920 tests .NET et 39 tests Python ;
- smoke Godot 4.7.1 .NET :
  `OPENRPG_FORGE_STUDIO_UX0E_PASS wizard_preview=true invalid_rpg_blocked=true
  core_without_area=true core_shell=true localized_metadata=true stable_identity=true
  player_start=true optional_waypoint=true custom_choices=true atomic_undo_redo=true persistence_roundtrip=true
  human_no_uuid=true ui=true ui_transaction=true project_restored=true windows=1` ;
- l’assistant et la page Projet utilisent uniquement des `Control` intégrés ; la preuve compte une
  seule fenêtre produit. Le sélecteur natif de dossier n’apparaît que si la personne le demande ;
- le projet RPG de preuve est créé dans un dossier temporaire, configuré par commande, sauvegardé,
  fermé puis rouvert avec ses deux langues, son identité et son orientation exactes ; le dossier de
  preuve est ensuite supprimé ;
- un projet Core sans zone est créé, sérialisé, rouvert par `StudioSessionController`, puis rendu par
  un `StudioWorkspaceShell` complet sur la page Projet sans `ActiveAreaId` ni viewport de zone ;
- la route du formulaire Projet exécute un dry-run, une application, puis un undo qui restaure la
  même instance canonique ; Crypte des Cendres reste non modifié et non sauvegardé ;
- une preuve UI dédiée charge une famille de règles, un mode de jeu et un rôle personnalisés : les
  listes affichent seulement « Profil existant (préservé) » et restituent les trois valeurs exactes ;
- graphe Roslyn régénéré depuis les sources : 8 543 nœuds, 54 009 relations et zéro diagnostic ;
- journal headless : `artifacts/verification/studio-shell/ux0-e/headless-smoke.log` ;
- journal Forward+ : `artifacts/verification/studio-shell/ux0-e/forwardplus.log`, avec Clairval à
  291 entités, 278 assets gouvernés et zéro aperçu de secours ;
- captures inspectées : `wizard-fhd.png`, `summary-qhd.png`, `module-fhd.png`,
  `localization-qhd.png` et `player-start-qhd.png` dans
  `artifacts/verification/studio-shell/ux0-e/`.

Le smoke headless utilise volontairement le renderer de compatibilité et ne sert pas de preuve
visuelle. Les cinq captures ont été produites par le Studio rendu en Forward+ ; aucune capture ne
montre d’UUID, de hash ou de JSON dans le parcours normal.

### ORF-PROJ-001

Le bouton **Créer un nouveau module** du hub ouvre un assistant en cinq étapes dans la fenêtre
existante : type, identité publique, monde de départ, technologie, puis résumé. Les choix couvrent
le modèle RPG/menu/simulation, les kits, les plateformes, la famille de règles, le rendu, le réseau,
la zone et le point de départ. **Prévisualiser** appelle `NewModuleHandler` en dry-run et ne crée
aucun dossier. **Créer et ouvrir** rejoue la même commande, appelle `ProjectSession.Create`, vérifie
immédiatement la réouverture, puis ouvre le workspace créé. Un dossier cible non vide est refusé
sans modification.

Route : `StudioNewProjectWizard` → `StudioSessionController.PreviewNewProject` ou
`CreateNewProject` → `NewModuleHandler` → `ProjectSession.Create/Open`.

### ORF-PROJ-002

La page **Projet / Identité & configuration** remplace la lecture générique par un formulaire
transactionnel. Une liste permet d’ajouter ou retirer des langues sans pouvoir supprimer la langue
principale. Chaque langue conserve nom, résumé, description, crédits, installation, règles,
contrôles, licences, avertissements et compatibilité. Auteur, version sémantique, licence, image
relative, mots-clés et classification restent communs. Le Studio ne prétend pas traduire les textes :
la fiche anglaise proposée par l’assistant est explicitement à compléter.

L’application passe par `SetModuleConfigurationHandler`, synchronise explicitement le titre public et
la description du module avec la langue principale, préserve les variables et les champs non modifiés,
puis entre dans `ProjectCommandHistory`. Les anciens appels qui ne demandent pas ce renommage restent
inchangés. La sauvegarde reste une action séparée et atomique.

### ORF-PROJ-003

Le formulaire affiche seulement **Identité gérée automatiquement · compatibilité 1.x · révision de
travail**. `ProjectId`, `ModuleId`, slug, namespace de package, UUID de fondation et identités de
documents sont dérivés de façon déterministe à l’intérieur de la commande et ne sont ni saisis ni
affichés. Deux dry-runs du même brouillon produisent exactement la même identité. Modifier les
métadonnées ou le point de départ conserve l’identité existante ; un projet historique sans
extension de fondation ne reçoit cette extension qu’au moment d’une application explicite.

### ORF-PROJ-004

Le modèle **Aventure RPG** force la création et le nom d’une première zone. Une demande RPG sans
zone est refusée avant toute écriture avec le message humain « Une aventure RPG doit commencer dans
une première zone ». Les modèles **Interface ou menu** et **Simulation** peuvent commencer sans
zone. Le Studio sait les rouvrir : le sélecteur affiche un état vide, l’inspecteur explique la
prochaine action et la page Projet reste entièrement utilisable sans appel à `Areas.First()`.

### ORF-PROJ-005

La section **Point de départ du joueur** utilise les noms visibles des zones, des blueprints
`PlayerSpawn` et des waypoints. Elle configure la zone, l’entité de spawn, un waypoint facultatif,
l’orientation, le mode de jeu, le rôle d’entrée et une règle humaine pour tous les joueurs, un joueur
nommé ou un tag. `ProjectFoundationValidator` refuse les références absentes et vérifie que l’entité
est bien un `PlayerSpawn`. Les profils supplémentaires et leurs champs non montrés sont préservés ;
le formulaire modifie seulement le profil principal sélectionné par défaut.

Le smoke exerce dry-run, application, undo/redo, sauvegarde, fermeture et rechargement. L’orientation
de 120 degrés, les langues et l’identité technique reviennent exactement après réouverture. Les
familles de règles, modes de jeu et rôles personnalisés sont conservés sans exposer leur clé technique.

## Limites honnêtes

- le sol de la première zone et le point de départ créés par l’assistant sont des éléments provisoires
  fonctionnels, destinés à être remplacés ou déplacés dans la vue Monde ;
- le sélecteur de dossier peut ouvrir le dialogue système natif demandé explicitement, mais aucun
  écran métier ne crée une deuxième fenêtre ;
- les profils de départ multiples sont compris et préservés par le domaine ; UX0-E édite le profil
  principal. Une surface de gestion de variantes multiples n’est pas revendiquée ici ;
- aucune traduction automatique, publication, signature ou qualification de distribution n’est
  incluse dans ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter `
  'FullyQualifiedName~NewModuleHandlerTests|FullyQualifiedName~SetModuleConfigurationHandlerTests|FullyQualifiedName~SetProjectFoundationHandlerTests'
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter `
  'FullyQualifiedName~ProjectFoundationProfileTests'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter `
  'FullyQualifiedName~ProjectFoundationPersistenceTests|FullyQualifiedName~ProjectSessionTests'
& .\scripts\Open-Studio.ps1 -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Resolution 1920x1080 -Evidence ux0-e-wizard `
  -Capture artifacts/verification/studio-shell/ux0-e/wizard-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 2560x1440 -Evidence ux0-e-player-start `
  -Capture artifacts/verification/studio-shell/ux0-e/player-start-qhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX0-D, vingt-cinq fonctions disposent désormais d’une preuve Studio spécifique ; les
692 autres lignes restent explicitement `planned`.
