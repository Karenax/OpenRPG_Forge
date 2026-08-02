# G7-07 — Recherche globale, références et renommage sûr

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-SHELL-015` — recherche globale plein texte et structurée ;
- `ORF-SHELL-016` — inspection de toutes les références Core connues ;
- `ORF-SHELL-017` — renommage sûr sans rupture d’identité.

## Résultat livré

`CreatorSearchIndex` construit une vue dérivée et déterministe du projet canonique. Son schéma v1
indexe le projet, le module, les zones, entités, transitions, blueprints, dialogues, nœuds, choix,
quêtes, états, transitions et diagnostics. La recherche est insensible aux accents, exige tous les
termes saisis et accepte des filtres exacts de type et de tag. Chaque résultat expose son UUID, son
chemin documentaire, sa provenance et, lorsque possible, une cible dans la sélection Creator.

`CreatorReferenceIndex` devient l’index générique des usages inversés. Il recalcule depuis le projet
les zones d’entrée, blueprints d’instances et de locuteurs, ancres et destinations de transitions,
entrées et cibles de dialogues, puis états initiaux et transitions de quêtes. L’ancien index du
catalogue Blueprints est maintenant une projection de cette source commune.

La commande `safe_rename` prend en charge le projet, le module, les zones, blueprints, dialogues et
quêtes. Elle valide la révision, la cible, le nom, le slug canonique et les doublons, puis reconstruit
l’agrégat en conservant son UUID et toutes les références. Le dry-run, le patch, l’inverse, l’audit,
l’historique et la validation du projet sont produits avant toute écriture.

La fenêtre Aurora « Recherche et références » aligne trois cartes : résultats filtrables, contenu et
usages, renommage sûr. Le bouton principal reste désactivé avant une prévisualisation valide, et
toute modification du nom ou du slug invalide cette prévisualisation. Le ciblage rejoint la
sélection partagée du Creator ; aucune mutation distincte n’est portée par l’interface.

## Preuves

- `tests/Forge.Application.Tests/Creator/CreatorSearchAndRenameTests.cs` couvre recherche accentuée,
  filtres, diagnostics, usages inversés, toutes les racines renommables, validations, dry-run,
  UUID stables et undo/redo ;
- les 65 tests `Forge.Application.Tests` et les 172 tests .NET sont verts ;
- le Toolset compile en Release sans avertissement ni erreur ;
- `OPENRPG_FORGE_TOOLSET_G707_SEARCH_PASS search=global` prouve le workflow UI, l’index des
  références, le renommage stable, l’historique et le round-trip de persistance ;
- `scripts/Verify.ps1` constitue le gate intégré final du lot.

## Limites conservées

- scripts, graphes auteur, assets, terrains, navigation, audio et futurs documents de gameplay ne
  sont pas inventés dans l’index avant l’existence de leurs contrats canoniques ;
- les sous-objets tels qu’un nœud de dialogue ou un état de quête sont inspectables mais leur nom se
  modifie encore dans l’éditeur de leur agrégat parent ;
- la recherche est reconstruite en mémoire à chaque rafraîchissement ; l’index incrémental et ses
  budgets de performance seront ajoutés lorsque les volumes réels le justifieront ;
- le renommage conserve naturellement les références UUID ; la migration de chemins d’assets ou de
  scripts attend leurs schémas versionnés.

Ces limites conservent les trois capacités au statut `Implemented` partiel.
