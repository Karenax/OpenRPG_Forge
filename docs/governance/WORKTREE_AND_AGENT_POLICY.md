# Worktrees, ownership et intégration

Schéma de gouvernance : v1. Provenance : `openrpg-forge:p0-r002`.

Chaque tâche utilise une branche ou un worktree dédié. Deux agents ne modifient jamais le même
worktree. Avant tout changement, le propriétaire déclare dans `.forge/task-ownership.json` la tâche,
la révision de base et les périmètres de fichiers exclusifs. Ce manifeste local est jetable et ne
contient aucun secret.

Une intégration n'est recevable que si elle conserve les changements hors périmètre et passe, dans
cet ordre, les contrôles tracker, backlog, graphe puis `scripts/Verify.ps1`. Les conflits de périmètre
sont résolus avant édition ; un agent ne contourne pas l'ownership en créant une seconde voie de
mutation.

Les branches automatisées utilisent le préfixe `codex/`. La publication distante reste une action
séparée qui exige une autorisation explicite.
