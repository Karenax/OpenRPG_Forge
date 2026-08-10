# Plan courant de complétude Aurora

Date de référence : 7 août 2026.
Catalogue clean-room immuable : 717 fonctions, 26 domaines.

## Cible

Les gates G0 à G13 prouvent des parcours verticaux utilisables. La cible initiale est toutefois
atteinte uniquement lorsque les 717 fonctions sont `Tested` avec une preuve spécifique et que le
portail global reste vert.

État actuel :

- 207 `Tested` ;
- 432 `Implemented` mais `Partial` ;
- 1 `Designed` ;
- 77 `Researched` ;
- 510 fonctions non closes.

## Découpage et ordre

`planning/COMPLETE_FUNCTION_BACKLOG.md` est généré depuis le catalogue et le tracker. Il affecte
chaque fonction exactement une fois à 187 lots `FC-*` stables de une à cinq IDs. La file `R###`
ordonne les lots par priorité, premier gate cible, parcours utilisateur puis identifiant stable.

Les dépendances du catalogue sont principalement des dépendances de domaine et forment des cycles.
Elles sont affichées, puis réévaluées feature par feature avant activation. Un seul lot est actif à
la fois ; une fiche d'exécution détaillée n'est créée qu'à ce moment.

## Programme P0 autorisé

Le programme courant couvre `R001` à `R050` : 196 fonctions P0, dont 5 déjà `Tested` et 191 à fermer.
Il est exécuté séquentiellement selon `planning/P0_ACCEPTANCE_PROGRAM.md`. Aucun statut n'est promu
par simple correspondance de code ou parce qu'un ancien gate est fermé.

## Preuves de sortie par lot

Chaque lot doit fournir :

1. matrice sémantique entre critères source, état réel et preuves ;
2. implémentation minimale des manques ;
3. tests de domaine/application et intégrations nécessaires ;
4. smoke Toolset/runtime/MCP, capture ou benchmark lorsque le critère l'exige ;
5. tracker et backlog régénérés ;
6. graphe frais et `scripts/Verify.ps1` vert.

## Périmètre séparé

Les lots Asset/Map restants sont `OPTIONAL_POST_DEMO` conformément à
`planning/ASSET_SCOPE_DECISION_2026-08-07.md`. Ils ne bloquent pas la complétude des 717 fonctions.

Le plan historique G0 à G13 est archivé dans
`history/AURORA_CAPABILITY_DELIVERY_PLAN_2026-08-07.md`.
