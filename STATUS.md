# État du projet — OpenRPG Forge

Dernière mise à jour : 9 août 2026.

## Lecture canonique

Les parcours verticaux G0 à G13 et les prototypes 3D sont livrés. La complétude fonctionnelle reste
distincte : 398 fonctions sur 717 sont `Tested`, 314 sont `Implemented` avec une limite `Partial`,
aucune n'est `Designed` et 5 sont `Researched`.

Le registre `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` couvre chaque ID exactement une fois dans
187 lots stables de une à cinq fonctions : 103 lots clos, 82 à compléter/prouver et 2 à livrer.
Chaque lot affiche désormais sa progression, ses gates, son parcours et ses dépendances déclarées.

## Produit effectivement disponible

- Crypte des Cendres et Forgotten Forest jouables en 3D ;
- Creator/Toolset, édition transactionnelle, undo/redo et playtest ;
- runtime séparé, sauvegarde/reprise et export `.orpmod` ;
- ForgeScript borné, MCP transactionnel, serveur autoritaire et Live Director ;
- pipeline de contenu gouverné, Asset Vault, Asset Atlas et modèles 3D animés ;
- vérification intégrée du build, des tests, smokes, graphes, exports et preuves.

Ces parcours ne valent pas preuve automatique des 717 critères détaillés.

## Programme actif — fermeture P0

Les 196 fonctions P0 et les 50 lots `R001` à `R050` sont clos. Le programme P0 est terminé ; aucun
P1/P2, AV7–AV10, commit ni push n'a été ouvert par cette exécution. Toute reprise de développement
doit faire l'objet d'une nouvelle autorisation explicite et conserver l'unité de lot bornée décrite
dans `docs/planning/P0_ACCEPTANCE_PROGRAM.md`.

## Périmètre Asset/Map

AV1 à AV6-F suffisent à la démonstration 3D actuelle. Les 25 lots `AV7-M*`, `AV8-*`, `AV9-*` et
`AV10-*` sont `OPTIONAL_POST_DEMO` : ils ne bloquent ni les 717 fonctions Aurora, ni le programme P0.
La décision est documentée dans `docs/planning/ASSET_SCOPE_DECISION_2026-08-07.md`.

## Preuves de santé

La validation de clôture R050 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 352 tests .NET et 39 tests Python, valide les smokes Toolset/runtime/MCP, les exports
reproductibles et le graphe frais 6 238/38 423, puis termine sur
`OPENRPG_FORGE_VERIFICATION=PASS`. Le NOTICE agrège désormais module, blueprints, tables, packages,
sources, assets et dépendances ; une licence inconnue bloque Shipping avant compilation ou écriture.

La validation de clôture R049 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 348 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 237/38 389, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Auto-intersections, filtres
contradictoires, cibles, portail, cooldown, scripts et accessibilité des triggers sont désormais
rejetés avant mutation/build avec contexte persistant et fallback runtime optionnel/requis.

La validation de clôture R048 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 346 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 234/38 376, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les triggers canoniques portent
désormais volume 2D/3D, filtres, cooldown, actions et portail typés, ainsi que les événements
VolumeEntered/VolumeExited ; UI, transaction, persistance, ForgeScript, MCP et fallback runtime sont prouvés.

La validation de clôture R047 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 343 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 217/38 271, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. OnUsed porte désormais verbe,
acteur, cible, résultat, provenance et API typés ; collision/navigation/animation, locks, traps,
inventaire, scripts, physique et budgets sont rejetés au pré-build lorsqu'ils sont incohérents.

La validation de clôture R046 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 341 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 213/38 256, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. La fondation placeable relie au
projet identité/visuel, rôle statique ou utilisable, collision, composants et transform complet ;
l'éditeur, l'overlay pivot/snap, ForgeScript, MCP, la persistance et le runtime optionnel/requis sont prouvés.

La validation de clôture R045 construit les 27 projets sans vulnérabilité, avertissement ni erreur,
exécute 1 338 tests .NET et 39 tests Python, valide les smokes Toolset/runtime et le graphe frais
6 202/38 200, puis termine sur `OPENRPG_FORGE_VERIFICATION=PASS`. Les transactions marchand
Buy/Sell/Barter valident désormais les agrégats de fonds, propriété, stock et capacité avant une
mutation atomique ; l'onglet Marchands, le handle typé, MCP et le runtime requis/optionnel sont prouvés.

La validation de clôture R037 construit les 27 projets sans vulnérabilité, avertissement ni erreur
et exécute les suites complètes Domain/Application/RpgKit.Domain/ModuleFormat/MCP à
320/341/144/268/72 tests. Le portail intégré confirme 1 306 tests .NET, 39 tests Python, les smokes
Toolset/runtime/export/terrain/MCP/réseau, le graphe déterministe à 6 101 nœuds et 37 471 relations,
puis `OPENRPG_FORGE_VERIFICATION=PASS`.

## Sources opérationnelles

1. `TASKS_MVP.md` pour le programme actif et la prochaine unité de travail ;
2. `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` pour les 187 lots et leur ordre ;
3. `tracking/project/AURORA_PARITY_COVERAGE.csv` pour le statut et les preuves des 717 fonctions ;
4. `docs/planning/P0_ACCEPTANCE_PROGRAM.md` pour le contrat d'exécution P0 ;
5. `docs/audits/COMPLETE_FUNCTION_COVERAGE_AUDIT_2026-08-07.md` pour l'audit et ses limites.

Le journal détaillé antérieur est archivé dans
`docs/history/STATUS_DELIVERY_JOURNAL_2026-08-07.md`.
