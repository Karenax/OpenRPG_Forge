# Programme de fermeture P2

## Objectif borné

Fermer les 190 fonctions P2 du catalogue Aurora sans ouvrir P3/P4 ni les lots Asset/Map
AV7–AV10. Au démarrage du programme, 128 fonctions sont `Tested`, 61 sont `Implemented`
avec une limite réelle documentée et une est `Researched`.

Le registre généré contient 50 lots P2 stables de une à cinq fonctions. Trente sont déjà
clos. Le programme, terminé le 12 août 2026, comprenait vingt lots : `R121` à `R132`, `R135` à
`R140`, `R142` et `R143`. Les rangs `R133`, `R134` et `R141` avaient disparu de la file ouverte parce que leurs
lots étaient déjà clos avant l’autorisation P2.

## Protocole obligatoire pour chaque lot

1. Relire intégralement les critères source des une à cinq fonctions.
2. Comparer chaque critère aux preuves, au code et aux tests sans promotion par inférence.
3. Créer une fiche sous `docs/aurora-parity/tasks/` avec matrice de critères et non-objectifs.
4. Fermer uniquement les limites `Partial` ou `Researched` réellement constatées.
5. Préserver les frontières Domain/Application, les commandes transactionnelles et les
   parcours 3D existants.
6. Exécuter les tests ciblés et une preuve Toolset, runtime, serveur, CLI ou MCP adaptée.
7. Promouvoir une fonction à `Tested` uniquement lorsque tous ses critères ont une preuve
   reproductible.
8. Régénérer tracker, backlog et graphe, puis passer `scripts/Verify.ps1` avant le lot suivant.

## Critère de fin

Le programme est terminé lorsque les 190 fonctions P2 et les 50 lots P2 sont clos, le
tracker et le registre sont frais, le graphe Roslyn est déterministe et le portail global
est vert. Un blocage réel reste documenté et ne permet aucune promotion artificielle.

## Périmètre exclu

- toutes les fonctions P3 et P4 ;
- les lots Asset/Map AV7–AV10 ;
- commit et push sans autorisation explicite.
