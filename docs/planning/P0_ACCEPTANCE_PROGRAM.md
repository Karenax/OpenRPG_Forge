# Programme de fermeture P0

## Objectif borné

Fermer les fondations P0 du catalogue Aurora sans les transformer en un chantier monolithique.
Le catalogue contient 196 fonctions P0 : les 196 sont `Tested`. Les 50 lots `R001` à `R050` du
`COMPLETE_FUNCTION_BACKLOG.md` sont clos, chacun ayant été limité à une à cinq feature IDs.

La demande courante a autorisé l'exécution séquentielle de `R038` à `R050`. Elle est terminée sans
ouvrir de P1/P2, AV7–AV10, commit ou push.

La clôture R050 est confirmée par le tracker frais, le graphe Roslyn déterministe et le portail
intégré `OPENRPG_FORGE_VERIFICATION=PASS`. Le programme reste fermé jusqu'à une nouvelle demande.

Le programme complet est actif, mais un seul lot est exécuté à la fois. L’ordre recommandé est celui
du registre généré. Il combine priorité, premier gate cible, parcours utilisateur, contrat et ID
stable. Le catalogue déclare surtout des dépendances de domaine et contient un grand cycle P0 ; ces
dépendances sont donc réévaluées feature par feature avant chaque lot au lieu de prétendre à un tri
topologique inexistant.

## Protocole obligatoire pour chaque lot

1. Relire intégralement les critères source des une à cinq feature IDs.
2. Comparer chaque phrase aux preuves du tracker, au code et aux tests actuels sans promouvoir de
   statut par inférence.
3. Créer ou actualiser une fiche sous `docs/aurora-parity/tasks/` avec périmètre, dépendances,
   fichiers probables, non-objectifs, risques et tests exacts.
4. Produire une matrice `critère → présent/manquant → preuve` ; les éléments déjà satisfaits sont
   conservés, les limites `Partial` deviennent la liste minimale d’implémentation.
5. Implémenter uniquement les manques du lot, en préservant les frontières Domain/Application, les
   commandes transactionnelles et le prototype 3D jouable.
6. Exécuter les tests ciblés à chaque palier, puis le smoke visible/headless adapté. Une promesse
   visuelle exige une capture inspectée ; une promesse de performance exige un benchmark.
7. Passer une ligne à `Tested` seulement si tous ses critères disposent d’une preuve spécifique et
   reproductible. Les promotions groupées sans audit individuel sont interdites.
8. Régénérer le tracker et le backlog, vérifier le graphe, exécuter `scripts/Verify.ps1`, puis mettre
   à jour `STATUS.md` et `TASKS_MVP.md` avant le lot suivant.

## Contrôle des dépendances

Avant d’ouvrir un lot, examiner les domaines listés dans la colonne « Dépendances déclarées » :

- si une exigence dépendante précise est non close et bloque le comportement, exécuter d’abord son
  lot ou documenter un sous-ensemble minimal dans le lot courant ;
- si l’interface nécessaire est déjà prouvée par une feature `Tested`, lier cette preuve ;
- si la dépendance est cyclique, définir le plus petit contrat traversant permettant de fermer le
  lot sans ouvrir simultanément les autres domaines ;
- ne jamais utiliser un gate historique fermé comme preuve suffisante.

## Critère de fin du programme

Le programme P0 est terminé uniquement lorsque les 196 fonctions P0 sont `Tested`, les 50 lots P0
sont `Clos`, le tracker et le registre généré sont frais, le graphe est déterministe et le portail
global est vert. Une difficulté persistante est signalée avec son blocage exact ; elle n’autorise ni
classe vide, ni test désactivé, ni statut artificiel.

## Périmètre exclu

Les lots `AV7-M*`, `AV8-*`, `AV9-*` et `AV10-*` sont hors catalogue Aurora et hors programme P0.
Leur statut produit est fixé dans `ASSET_SCOPE_DECISION_2026-08-07.md`.
