# G7-02 — Coque Creator Forge

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-SHELL-001` — disposition multi-panneaux ;
- `ORF-SHELL-002` — explorateur hiérarchique ;
- `ORF-SHELL-006` — sélection et inspection ;
- `ORF-SHELL-020` — messages, erreurs et statut.

## Résultat livré

La surface Forge expose neuf workspaces stables et une coque à trois panneaux : explorateur à
gauche, édition Monde et aperçu au centre, inspecteur, diagnostics et playtests à droite. Les
read-models de l’explorateur, de la sélection et des diagnostics vivent dans `Forge.Application` et
ne dépendent pas du moteur 3D.

L’explorateur classe le projet par zones, entités, types de blueprints, dialogues et quêtes. Une
sélection d’entité se propage entre l’arbre, l’inspecteur et le sélecteur d’édition existant. Le
contexte accepte plusieurs éléments au niveau applicatif et peut être verrouillé. Les diagnostics
ont un identifiant stable, une sévérité, un chemin, une corrélation facultative et une cible ; un
clic sélectionne cette cible lorsqu’elle existe.

## Preuves

- `tests/Forge.Application.Tests/Creator/CreatorShellModelTests.cs` couvre hiérarchie, unicité,
  sélection multiple/verrouillée et navigation diagnostique stable ;
- `OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS` prouve les neuf workspaces, l’explorateur réel, la sélection
  partagée et le clic diagnostic dans l’éditeur headless ;
- `scripts/Verify.ps1` compile la solution, exécute 137 tests .NET, 22 tests Python et les scénarios
  complets du prototype.

## Limites conservées

- les panneaux sont redimensionnables mais pas encore détachables ou enregistrables par profil ;
- l’interface de multi-sélection et l’édition des propriétés communes ne sont pas encore exposées ;
- les vues par tag, dépendance et statut arrivent avec les catalogues et l’index de références ;
- catégories, filtres et export d’un bundle de diagnostic restent à livrer ;
- la capture visuelle automatisée de cette version doit être rejouée : le service Windows a retourné
  une erreur de propriétaire de fenêtre incohérente malgré une unique fenêtre `OpenRPG_Forge`.
