# Acceptation Studio — UX0-J Fermer la coque de projet

État local au 15 août 2026 : **fonctionnel et qualifié** pour les deux identifiants de ce lot.
Cette acceptation porte sur les routes actives du Studio autonome. Elle clôt UX0, mais ne qualifie
pas encore les 675 autres routes, les 66 fenêtres historiques, les distributions Windows/Linux ou
une publication Git.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests canoniques ciblés : Application 23/23 et ModuleFormat 5/5, soit 28/28 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX0J_PASS content_rename=true resource_rename=true
  stable_identity=true references_migrated=true dry_run=true atomic_undo_redo=true
  persistence_roundtrip=true invalid_rename_blocked=true diagnostics=true deep_link=true
  quick_fix_preview=true quick_fix_undo=true diagnostic_export=true human_no_uuid=true ui=true
  ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke travaille exclusivement dans un workspace temporaire : il renomme une zone avec son
  adresse interne, rouvre le projet, renomme une ressource ForgeScript, migre une référence textuelle,
  vérifie l’identité stable, annule/rétablit les deux familles de mutations, bloque un nom invalide,
  applique puis annule/rétablit un correctif réel et exporte deux fois le même bundle déterministe ;
- Crypte des Cendres reste le workspace d’inspection, mais n’est jamais modifiée par le smoke ;
- journal headless : `artifacts/verification/studio-shell/ux0-j/headless-smoke.log` ;
- journaux Forward+ : `forwardplus-fhd.log`, `forwardplus-qhd.log` et `forwardplus-4k.log` dans
  `artifacts/verification/studio-shell/ux0-j/`, sous Godot 4.7.1 Mono, Vulkan et
  AMD Radeon RX 7800 XT, sans erreur, échec ni avertissement ;
- captures Forward+ inspectées : `rename-fhd.png` en 1920×1080,
  `diagnostics-qhd.png` en 2560×1440 et `diagnostics-4k.png` en 3840×2160.

La première revue FHD a montré une carte de renommage inutilement haute. Sa hauteur est désormais
bornée à 560 px : contexte, champs, impact, aperçu obligatoire, application, annulation et statut
restent visibles ensemble. Les vues Diagnostics QHD et 4K n’ont ni débordement ni action coupée.

### ORF-SHELL-017

La recherche globale `Ctrl+K` propose maintenant **Renommer en sécurité…** pour les projets,
modules, zones, blueprints, dialogues, quêtes, scripts et assets pris en charge. Le dialogue reste
dans l’unique fenêtre Godot et demande seulement un nom visible et, lorsque le type le permet, une
adresse interne lisible. Aucun UUID, hash, chemin absolu ou document JSON n’est demandé.

Pour le contenu, `SafeRenameHandler` exécute d’abord un `SafeRenameCommand` en dry-run, recalcule le
même aperçu à la révision courante avant application, puis enregistre le résultat dans
`ProjectCommandHistory`. Le renommage conserve toutes les données voisines et passe par la
sauvegarde atomique du projet. Pour une ressource, `WorkspaceResourceRenameService` prévisualise les
fichiers et références touchés, vérifie la dérive SHA, déplace la ressource, migre les références et
conserve l’identité fournie par `WorkspaceResourceIdentityStore`; son historique dédié restaure ou
rétablit exactement le chemin et les références.

L’adresse fondatrice d’un module existant reste volontairement en lecture seule : son nom visible
peut changer, mais UX0-J ne simule pas une migration d’identité que le domaine ne possède pas. Les
refus de doublon, format, dérive ou cible absente deviennent des messages humains et des diagnostics
structurés réutilisables dans la page Validation.

### ORF-SHELL-020

La page existante **Validation & corrections** est maintenant alimentée par `CreatorDiagnostics` et
`CreatorDiagnosticConsole`. Elle expose recherche textuelle, gravité, catégorie et opération
humaine, compteurs, liste des problèmes, cible nommée, emplacement lisible et une référence support
stable. **Aller à la cible** réutilise le routage et la sélection de la coque ; les détails techniques
restent hors du parcours normal.

Un correctif disponible doit être prévisualisé avant application. `ApplyDiagnosticQuickFixHandler`
revalide la révision, applique la commande canonique et l’inscrit dans le même historique projet ;
les commandes Annuler/Rétablir restent accessibles dans la page. La barre d’état conserve un journal
borné des réussites et erreurs récentes sans créer une seconde source de vérité.

**Exporter le diagnostic** passe par `CreatorDiagnosticBundleExporter`. Le bundle local est
atomique, expurgé, déterministe et réutilisé si son contenu n’a pas changé ; le smoke prouve aussi
qu’il contient le journal de session sans exposer de secret dans l’interface.

## Limites honnêtes

- UX0-J rend la navigation de diagnostics et les correctifs canoniques accessibles ; il ne qualifie
  pas par anticipation les autres fonctions métier de validation prévues en UX7 ;
- les types sans stratégie canonique de renommage restent explicitement indisponibles au lieu de
  provoquer une réécriture heuristique ;
- le bundle est un artefact local d’assistance, pas un envoi réseau ni une qualification de support
  distant ;
- les 717 routes, la parité des 66 fenêtres, l’export du Studio et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~CreatorSearchAndRenameTests|FullyQualifiedName~ApplyDiagnosticQuickFixHandlerTests|FullyQualifiedName~CreatorShellModelTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --configuration Debug `
  --filter "FullyQualifiedName~CreatorSearchAndResourceRenameTests|FullyQualifiedName~CreatorDiagnosticBundleExporterTests"
& .\scripts\Open-Studio.ps1 -Page zones -Evidence ux0-j -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Evidence ux0-j-rename -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux0-j/rename-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Evidence ux0-j-diagnostics -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux0-j/diagnostics-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Evidence ux0-j-diagnostics -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux0-j/diagnostics-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX0-J, les 42 fonctions de la vague UX0 disposent désormais d’une preuve Studio
spécifique ; les 675 autres lignes restent explicitement `planned`.
