# ADR 0004 — IA et interfaces via un bus de commandes transactionnel

- Statut : accepté
- Date : 1 août 2026

## Contexte

Une IA ou une interface qui écrit directement dans les fichiers rend les modifications difficiles à valider, prévisualiser, annuler et auditer. Le même problème se pose pour l’éditeur et les futures fonctions collaboratives.

## Décision

Toute mutation métier passe par `Forge.Application` et un bus de commandes transactionnel.

Une commande porte au minimum un identifiant et une révision de base. Son résultat contient des diagnostics, la nouvelle révision éventuelle, un patch prévisualisable, une opération inverse ou un snapshot et une entrée d’audit.

- Les mutations IA sont `dry_run` par défaut.
- L’application d’un plan exige un `plan_id` lié à une révision du projet.
- Les conflits de révision sont détectés explicitement.
- Le MCP Host est un processus C# séparé relié à l’éditeur par un canal local authentifié.
- Les outils MCP sont métier et de haut niveau ; aucun outil générique d’écriture de fichier ou d’exécution arbitraire n’est livré aux créateurs.
- Lecture, planification et application utilisent des niveaux d’autorisation distincts.

## Conséquences

- Undo/Redo, audit, tests et futures collaborations partagent le même mécanisme.
- Le bus de commandes doit être introduit tôt dans le domaine monde.
- L’intégration MCP attend qu’un workflow manuel réel soit stable.
