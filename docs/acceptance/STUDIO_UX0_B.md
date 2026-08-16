# Acceptation Studio — UX0-B Organiser son espace de travail

État local au 15 août 2026 : **fonctionnel et qualifié** pour les cinq identifiants de ce lot.
Cette acceptation ne promeut aucune autre fonction Aurora et ne vaut pas qualification finale du
Studio, des 66 fenêtres historiques ou des distributions Windows/Linux.

## Preuves communes

- build : `dotnet build apps/Forge.Studio/Forge.Studio.csproj --no-restore`, sans avertissement ;
- tests ciblés : Domain 14/14, Application 6/6, ModuleFormat 5/5 et Studio.Core 3/3 ;
- smoke Godot :
  `OPENRPG_FORGE_STUDIO_UX0B_PASS tabs=3 selection_restored=true comparison_read_only=true
  layout_dry_run=true workspace_undo=true keymap_commands=14 conflict_blocked=true
  keymap_undo=true roundtrip=true windows=1 persisted=false` ;
- inspection interactive réelle en Forward+ à 1920×1080 : ouverture de **Clairval** depuis la liste,
  sélection de **Éclaireuse de l’Aube**, passage vers **La Forêt Oubliée** puis retour à Clairval sans
  perdre la sélection ; la vue a chargé 291 entités, 278 assets et zéro fallback ;
- inspection rendue en Forward+ à 1920×1080 et 2560×1440 : trois onglets humains, deux vraies vues
  3D dont une explicitement **lecture seule**, aucune action coupée et une seule fenêtre native ;
- preuves de préférences : les deux fichiers de profil présents avant l’inspection gardent exactement
  leur SHA-256 et aucun fichier de raccourcis n’est créé sans **Enregistrer explicitement** ;
- journaux : `artifacts/verification/studio-ux0b/studio-ux0b-forwardplus.log` et
  `artifacts/verification/studio-ux0b/studio-ux0b-smoke.log` ;
- captures : `artifacts/verification/studio-ux0b/ux0-b-tabs-fhd.png`,
  `ux0-b-tabs-qhd.png`, `ux0-b-shortcuts-fhd.png`, `ux0-b-shortcuts-qhd.png`,
  `ux0-b-conflict-fhd.png` et `ux0-b-profile-fhd.png`.
- portail intégré : `scripts/Verify.ps1` terminé par `OPENRPG_FORGE_VERIFICATION=PASS`, avec
  1 910 tests .NET, 39 tests Python, zéro paquet vulnérable et le smoke UX0-B inclus.

L’interface normale emploie les noms de zones, profils, commandes et ressources. Les UUID complets
de l’inspecteur d’entité restent masqués derrière **Afficher les détails techniques**.

### ORF-SHELL-001

**Espace de travail** ouvre une surcouche intégrée, sans nouvelle fenêtre, avec trois dispositions
nommées et des contrôles explicites pour l’Explorateur, l’inspecteur, la palette, les diagnostics et
l’Assistant. Un changement produit d’abord un dry-run lisible ; **Appliquer** ne change que la session,
undo/redo passe par `EditorWorkspaceProfileCommandHistory` et seule l’action **Enregistrer
explicitement** écrit atomiquement le profil.

### ORF-SHELL-012

La barre de documents ouvre jusqu’à huit zones du même projet, les active sans réordonner les autres
onglets et refuse doublons ou dernier onglet fermé. Chaque zone conserve sa sélection et sa caméra.
La comparaison côte à côte reconstruit une seconde vue 3D réelle, clairement marquée lecture seule,
sans gizmo ni commande de mutation.

### ORF-SHELL-022

Treize commandes sont recherchables par nom humain et modifiables depuis les presets Standard,
Aurora, Blender ou Personnalisé. Un doublon comme `Ctrl+S` est signalé en rouge et désactive le
dry-run ; une attribution valide passe par `SetEditorKeymapCommand`, son historique undo/redo et
`EditorKeymapProfileStore`. Les actions encore planifiées dans une vague ultérieure, comme le
playtest UX7, conservent leur raccourci sans simuler leur disponibilité fonctionnelle.

### ORF-SHELL-023

Les profils Bâtisseur, Auteur, Scripteur, Artiste, MJ, Testeur et Débutant restaurent rôle, panneaux,
zones ouvertes et disposition. Un nom différent crée un profil utilisateur nommé seulement lors de
la sauvegarde explicite ; le module exporté n’est jamais modifié par ces préférences `.forge`.

### ORF-EE-003

La parité **plusieurs onglets de zone** est assurée par la même route que `ORF-SHELL-012` : trois
zones distinctes sont prouvées dans une fenêtre, l’activation restaure le contexte propre à chaque
zone et la comparaison ne duplique ni l’état éditable ni l’historique du projet.

## Reproduction

```powershell
& .\scripts\Open-Studio.ps1 -Headless -Smoke -Evidence ux0-b -Wait
& .\scripts\Open-Studio.ps1 -Resolution 1920x1080 -Evidence ux0-b-tabs `
  -Capture artifacts/verification/studio-ux0b/ux0-b-tabs-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Resolution 1920x1080 -Evidence ux0-b-conflict `
  -Capture artifacts/verification/studio-ux0b/ux0-b-conflict-fhd.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

La matrice générée référence cette page pour ces cinq identifiants. Avec UX0-A, dix fonctions ont
désormais une preuve spécifique ; les 707 autres lignes restent explicitement `planned`.
