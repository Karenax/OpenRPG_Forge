# Programme de fermeture P1

## Objectif borné

Fermer les 298 fonctions P1 du catalogue Aurora sans ouvrir de P2 ni les lots Asset/Map AV7–AV10.
Le registre généré répartit ces fonctions dans 70 lots stables `R051` à `R120`, chacun limité à une
à cinq fonctions. Au lancement du programme, 41 fonctions étaient `Tested`, 253 `Implemented` avec
une limite `Partial` et 4 `Researched`.

Le travail repris avait déjà ouvert `FC-AI-P1-03` sous un rang local erroné. Son rang canonique est
`R120` ; il est fermé et validé comme unité isolée avant la reprise de la file recommandée à `R051`.
Ensuite, un seul lot est actif à la fois et aucun lot suivant n’est ouvert avant le portail intégré
vert du lot courant.

## Protocole obligatoire pour chaque lot

1. Relire intégralement les critères source des une à cinq feature IDs.
2. Comparer chaque critère aux preuves, au code et aux tests sans promotion par inférence.
3. Créer une fiche sous `docs/aurora-parity/tasks/` avec périmètre, dépendances et non-objectifs.
4. Produire une matrice `critère → présent/manquant → preuve` et n’implémenter que les limites réelles.
5. Préserver les frontières Domain/Application, les commandes transactionnelles et le prototype 3D.
6. Exécuter les tests ciblés puis la preuve visible, headless ou mesurée adaptée au critère.
7. Passer une fonction à `Tested` uniquement lorsque tous ses critères ont une preuve reproductible.
8. Régénérer tracker/backlog/documentation/graphe et passer `scripts/Verify.ps1` avant le lot suivant.

## Ordre et dépendances

La file canonique reste celle de `docs/planning/COMPLETE_FUNCTION_BACKLOG.md`. Les lots déjà clos
restent des points de maintenance et ne sont pas rouverts sans régression réelle. Avant chaque lot,
les dépendances déclarées sont réévaluées ; un cycle est réduit au plus petit contrat traversant
testable et documenté.

## Critère de fin

Le programme est terminé lorsque les 298 fonctions P1 sont `Tested`, les 70 lots P1 sont clos, le
tracker et le registre généré sont frais, le graphe Roslyn est déterministe et le portail global est
vert. Un blocage réel reste explicitement documenté et n’autorise ni classe vide, ni test désactivé,
ni statut artificiel.

## Périmètre exclu

- toutes les fonctions P2 ;
- les lots Asset/Map AV7–AV10 ;
- commit et push sans autorisation explicite.
