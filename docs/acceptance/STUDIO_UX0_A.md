# Acceptation Studio — UX0-A Découverte et navigation humaine

État local au 15 août 2026 : **fonctionnel et qualifié** pour les cinq identifiants de ce lot.
Cette acceptation ne promeut aucune autre fonction Aurora et ne vaut pas qualification finale du
Studio, des 66 fenêtres historiques ou des distributions Windows/Linux.

## Preuves communes

- build : `dotnet build apps/Forge.Studio/Forge.Studio.csproj --no-restore`, sans avertissement ;
- tests Application ciblés : 28/28 ; ModuleFormat : 5/5 ; Studio.Core : 2/2 ;
  EngineAdapter/provenance : 3/3 ; ArchitectureGraph : 13/13 ;
- smoke Godot :
  `OPENRPG_FORGE_STUDIO_UX0A_PASS explorer_nodes=867 synced=true page_search=true
  object_search=true resource_search=true action_search=true references=6 help_sources=2
  filter_visible=1 windows=1 persisted=false` ;
- inspection interactive réelle en Forward+ à 1920×1080 et 2560×1440 : une seule fenêtre,
  `Ctrl+K`, saisie de `maelys`, ouverture par Entrée, sélection de Maëlys synchronisée dans
  l’Explorateur et l’inspecteur ;
- journaux interactifs propres :
  `artifacts/verification/studio-ux0a-interactive-3.log` et
  `artifacts/verification/studio-ux0a-interactive-qhd.log`, avec 80 entités, 67 assets,
  zéro fallback et aucune erreur moteur ;
- captures reproductibles : `artifacts/verification/studio-ux0a/ux0-a-search-fhd.png`,
  `ux0-a-search-qhd.png`, `ux0-a-filter-fhd.png`, `ux0-a-references-fhd.png` et
  `ux0-a-help-fhd.png`.
- portail intégré : `scripts/Verify.ps1` terminé par `OPENRPG_FORGE_VERIFICATION=PASS`, avec
  1 909 tests .NET, 39 tests Python et le smoke Studio UX0-A inclus.

### ORF-SHELL-002

L’Explorateur hiérarchique compact groupe le projet par zone ou par type, conserve la vue 3D et
synchronise sa sélection avec la sélection 3D, la recherche globale et l’inspecteur. Le smoke
compte 867 nœuds et vérifie explicitement la synchronisation. L’inspection FHD et QHD confirme que
la liste reste utilisable sans créer de fenêtre native supplémentaire.

### ORF-SHELL-008

Le panneau intégré **Filtres** combine types, tags, auteur, état/origine, calques, familles de
volumes, isolation et fantômes. L’application passe par `EditorWorkspaceProfileCommandHistory`,
avec dry-run, undo en mémoire et persistance explicite atomique ; **Enregistrer** crée une vue
nommée. Le smoke applique une isolation non persistée, obtient exactement un objet visible puis
annule, tandis que les tests couvrent planification, historique et stockage du profil.

### ORF-SHELL-015

`Ctrl+K` ouvre une surcouche intégrée et recherchable au clavier. La recherche française est
insensible aux accents et couvre pages, contenus, ressources de workspace, objets, diagnostics et
actions de la coque. Le smoke valide séparément page, objet, ressource et action ; les parcours FHD
et QHD ouvrent `maelys` sur **Maëlys, garde forestière** par Entrée.

### ORF-SHELL-016

**Trouver les références** est disponible depuis le clic droit 3D et l’inspecteur. La vue intégrée
affiche la cible stable, les chemins canoniques et ouvre la zone ou la ressource concernée sans
fermer la 3D. Le scénario de preuve utilise le blueprint **Éclaireuse de l’Aube** et retrouve six
utilisations dans six zones.

### ORF-SHELL-021

Le bouton d’aide ouvre un inspecteur contextuel local qui présente la tâche actuelle, l’action
suivante, les propriétés importantes, un exemple ForgeScript, les dépendances et les sources
locales. La preuve `display.filters` cite deux documents canoniques ; le smoke refuse une aide sans
source et confirme `help_sources=2`.

## Reproduction

```powershell
& .\scripts\Open-Studio.ps1 -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Resolution 1920x1080 -Evidence ux0-a `
  -Capture artifacts/verification/studio-ux0a/ux0-a-search-fhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

La matrice générée référence cette page pour ces cinq identifiants. Les 712 autres lignes restent
explicitement `planned` jusqu’à leur propre parcours, tests et preuve spécifique.
