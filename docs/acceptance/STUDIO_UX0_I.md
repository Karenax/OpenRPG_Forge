# Acceptation Studio — UX0-I Compléter les données globales du projet

État local au 15 août 2026 : **fonctionnel et qualifié** pour les trois identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle ne qualifie pas encore les
66 fenêtres historiques, les autres lignes de la matrice Studio, une distribution publique ou une
publication Git.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Domain 1/1, Application 2/2, ModuleFormat 1/1 et Scripting 1/1, soit 5/5 ;
- suites complètes concernées : Scripting 72/72, Domain 490/490, Application 481/481 et
  ModuleFormat 326/326 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX0I_PASS variables=true invalid_variable_blocked=true player_guide=true
  script_cache=true cache_refresh=true dry_run=true atomic_undo_redo=true
  persistence_roundtrip=true neighbors_preserved=true human_no_uuid=true ui=true
  ui_transaction=true guide_pages=8 scripts=2 project_restored=true windows=1` ;
- le smoke crée un workspace temporaire, applique les trois parcours, sauvegarde, ferme, rouvre et
  compare les identités et valeurs canoniques. Crypte des Cendres reste inchangée et aucun profil de
  build absent avant le smoke n’est laissé derrière lui ;
- `SetModuleConfigurationCommand` reste l’unique voie Application pour les variables et les pages
  localisées. Dry-run, application, `ProjectCommandHistory`, undo et redo opèrent sur le document
  complet et préservent distribution, contenu, politiques runtime et données non affichées ;
- `SetWorkspaceBuildProfileCommand` reste la voie Application du cache. Le profil v1 à identité et
  provenance stables est écrit atomiquement par `WorkspaceBuildProfileStore`; ses boutons dédiés
  annulent et rétablissent aussi la présence exacte du fichier de profil ;
- `ForgeScriptArtifactCache` compile réellement par `NativeForgeScriptRuntime`, vérifie un hit au
  second passage puis recompile après changement de source. Le test Scripting couvre séparément les
  invalidations source, API et compilateur ;
- les smokes runtime existants restent obligatoires dans le portail :
  `OPENRPG_FORGE_RUNTIME_R025_PROJECT_OPERATIONS_PASS` pour blackboard strict et cache, puis
  `OPENRPG_FORGE_FORGESCRIPT_CACHE` sur le chargement des scripts de module ;
- journal headless : `artifacts/verification/studio-shell/ux0-i/headless-smoke.log` ;
- journaux Forward+ : `forwardplus-fhd.log`, `forwardplus-qhd.log` et `forwardplus-4k.log` dans
  `artifacts/verification/studio-shell/ux0-i/`, sous Godot 4.7.1, Vulkan et
  AMD Radeon RX 7800 XT ;
- captures Forward+ inspectées : `variables-fhd.png` en 1920×1080, `cache-qhd.png` en 2560×1440 et
  `guide-4k.png` en 3840×2160 dans le même dossier.

La première inspection QHD a détecté les trois garanties du cache comprimées en colonnes d’une
lettre. Les lignes possèdent maintenant une largeur minimale et utilisent tout l’espace horizontal.
Le rendu 4K exact réutilise le `SubViewport` de preuve lorsque Windows borne la fenêtre physique.

### ORF-PROJ-019

La section **Variables du projet** présente une liste par noms humains et un formulaire direct :
nom visible, type Texte/Oui-non/Entier/Décimal, conservation Session/Sauvegarde/Joueur/Groupe/Serveur,
valeur initiale, partage réseau, explication, visibilité diagnostic et caractère requis. Une nouvelle
variable reçoit son UUID et sa clé sûre en interne ; une variable existante conserve son identité si
son nom visible n’est pas changé. UUID, clé brute, schéma et provenance ne sont jamais demandés dans
le parcours normal.

Le constructeur `ProjectVariableDefinition` normalise la valeur typée et
`ProjectVariableValidator` refuse doublons, variable requise transitoire et autorité serveur sur une
portée incompatible. Le smoke couvre les cinq portées, les modes Owner/Party/AllClients/Server et le
round-trip exact. `ProjectBlackboardScriptService` demeure la frontière ForgeScript typée et le MCP
continue d’utiliser le même document canonique.

### ORF-PROJ-021

La section **Cache des scripts** expose deux organisations sûres par libellés humains, sans chemin à
saisir. Elle rend visibles les garanties obligatoires : build incrémental, recompilation limitée aux
éléments affectés et reproductibilité avant export. Une configuration personnalisée préexistante est
affichée comme préservée au lieu d’être écrasée.

**Vérifier et réchauffer le cache** découvre les ForgeScript du dossier canonique, compile leur
contenu natif, puis refait un passage qui doit être entièrement réutilisé. Le cache reste un artefact
jetable exclu du `.orpmod`; seules ses politiques canoniques sont persistées. Aucun faux statut, cache
distant ou clé cryptographique n’est présenté à l’auteur.

### ORF-PROJ-022

La section **Guide du joueur** remplace le repli générique « textes avancés » par huit pages
directement sélectionnables dans chaque langue : Synopsis, Installation, Règles, Contrôles, Crédits,
Licences, Avertissements et Compatibilité. Un compteur indique les pages renseignées et l’état vide
reste explicite.

Ces champs sont les `ProjectLocalizedMetadata` déjà consommées par le lanceur, l’export et le
runtime. Le Studio ne crée aucun second document : aperçu, application, undo/redo et réouverture
passent par la même commande que l’identité localisée et préservent nom, résumé et langues voisines.

## Limites honnêtes

- les types de variables structurés ou propres à un kit futur ne sont pas inventés : UX0-I expose
  exactement les quatre types et cinq portées du domaine actuel ;
- le cache distant/partagé et les imports transitifs ForgeScript restent hors contrat. La preuve
  porte sur le cache local source/API/compilateur et le build reproductible déjà canoniques ;
- `ORF-PROJ-020`, contenu personnalisé ordonné, reste planifié dans UX8 et
  `ORF-PROJ-023`, ruleset associé, dans UX6 ;
- les deux dernières routes de coque UX0, renommage sûr et messages/erreurs/statut, restent à traiter
  dans UX0-J ;
- la qualification des 717 routes, la parité des 66 fenêtres, l’export Windows/Linux et le retrait du
  plugin historique restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --filter 'FullyQualifiedName~Ux0I'
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --filter 'FullyQualifiedName~Ux0I'
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --filter 'FullyQualifiedName~Ux0I'
dotnet test tests/Forge.Scripting.Tests/Forge.Scripting.Tests.csproj --filter 'FullyQualifiedName~Ux0I'
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux0-i-variables -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 1920x1080 -Evidence ux0-i-variables `
  -Capture artifacts/verification/studio-shell/ux0-i/variables-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 2560x1440 -Evidence ux0-i-cache `
  -Capture artifacts/verification/studio-shell/ux0-i/cache-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page module -Resolution 3840x2160 -Evidence ux0-i-guide `
  -Capture artifacts/verification/studio-shell/ux0-i/guide-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX0-I, quarante fonctions disposent désormais d’une preuve Studio spécifique ; les
677 autres lignes restent explicitement `planned`.
