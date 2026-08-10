---
name: openrpg-aurora-increment
description: Auditer, planifier ou livrer un incrément de parité Aurora dans OpenRPG Forge. Utiliser pour les demandes mentionnant Aurora, une gate G0-G13, un lot de parité, le tracker de couverture ou des feature IDs. Borner toute implémentation à un ensemble cohérent de une à cinq exigences et exiger des preuves avant de passer une ligne à Tested.
---

# Livrer un incrément Aurora borné

Préserver la démarche clean-room et distinguer recherche, plan, implémentation et preuve.

## Lire les sources de vérité

1. Lire `AGENTS.md` et `docs/AURORA_CAPABILITY_DELIVERY_PLAN.md`.
2. Lire les exigences pertinentes dans `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/` sans jamais modifier ce snapshot.
3. Lire `docs/planning/COMPLETE_FUNCTION_BACKLOG.md`, puis le lot `FC-*` explicitement activé.
4. Lire `tracking/project/AURORA_PARITY_COVERAGE.csv`, le rapport d'écart et les documents de gate ou de tâche concernés dans `docs/aurora-parity/`.
5. Vérifier l'état du code et des tests avant d'accepter le statut du tracker comme preuve actuelle.

## Choisir le mode de travail

- Pour une question de statut, rester en lecture seule et rapporter les écarts sans modifier le tracker.
- Pour une demande de plan, partir d'un seul lot `FC-*` ou le scinder sans dépasser cinq feature IDs, puis préciser critères d'acceptation, fichiers probables, tests et dépendances.
- Pour une demande d'implémentation, sélectionner une à cinq feature IDs cohérentes. Ne pas détourner une tâche P0/P1 en cours au profit d'une priorité inférieure.

## Implémenter l'incrément

1. Définir le comportement utilisateur et les preuves attendues pour chaque feature ID.
2. Interroger le graphe d'architecture selon `AGENTS.md` pour l'impact et les tests.
3. Préserver les frontières Domain/Application, le bus de commandes transactionnel et l'indépendance vis-à-vis de Godot.
4. Refuser les classes vides, marqueurs artificiels et adaptations qui copient code, texte, assets, API ou formats propriétaires.
5. Tester chaque palier et maintenir un prototype fonctionnel.

## Mettre à jour les preuves

1. Ne marquer `Implemented` qu'avec une correspondance concrète dans le produit.
2. Ne marquer `Tested` qu'avec un test automatisé ou une procédure reproductible liée.
3. Utiliser `scripts/Update-AuroraParityTracker.ps1` pour régénérer le tracker lorsqu'une mise à jour est autorisée ; ne pas éditer le snapshot de recherche.
4. Régénérer `docs/planning/COMPLETE_FUNCTION_BACKLOG.md` avec `scripts/Update-CompleteFunctionBacklog.ps1`, puis exécuter son mode `-Check`.
5. Mettre à jour les documents de clôture, `STATUS.md` et `TASKS_MVP.md` lorsque l'état réel change.
6. Terminer avec `$openrpg-delivery-gate` et rapporter explicitement les exigences restant partielles.
