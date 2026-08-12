# Plan courant de complétude Aurora

Date de référence : 7 août 2026.
Catalogue clean-room immuable : 717 fonctions, 26 domaines.

## Cible

Les gates G0 à G13 prouvent des parcours verticaux utilisables. La cible initiale est toutefois
atteinte uniquement lorsque les 717 fonctions sont `Tested` avec une preuve spécifique et que le
portail global reste vert.

État actuel au 12 août 2026 :

- 717 `Tested` ;
- 0 `Implemented` partiel ;
- 0 `Designed` ;
- 0 `Researched` ;
- 0 fonction non close.

## Découpage et ordre

`planning/COMPLETE_FUNCTION_BACKLOG.md` est généré depuis le catalogue et le tracker. Il affecte
chaque fonction exactement une fois à 187 lots `FC-*` stables de une à cinq IDs. La file `R###`
ordonne les lots par priorité, premier gate cible, parcours utilisateur puis identifiant stable.

Les dépendances du catalogue sont principalement des dépendances de domaine et forment des cycles.
Elles sont affichées, puis réévaluées feature par feature avant activation. Un seul lot est actif à
la fois ; une fiche d'exécution détaillée n'est créée qu'à ce moment.

## Programmes d'acceptation terminés

Les 196 fonctions P0, 298 fonctions P1, 190 fonctions P2, 32 fonctions P3 et la fonction P4 sont
fermées. Les protocoles et limites restent consignés dans `planning/P0_ACCEPTANCE_PROGRAM.md`,
`planning/P1_ACCEPTANCE_PROGRAM.md`, `planning/P2_ACCEPTANCE_PROGRAM.md` et
`planning/P3_P4_ACCEPTANCE_PROGRAM.md`. Aucun statut n'a été promu par simple correspondance de
code ou parce qu'un ancien gate était fermé.

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

L’historique détaillé G0 à G13 reste traçable dans Git et dans les fiches d’acceptation liées aux
fonctions. Aucun second contexte narratif n’est maintenu.
