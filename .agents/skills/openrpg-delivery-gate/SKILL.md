---
name: openrpg-delivery-gate
description: Valider une modification bornée, une clôture de lot ou une préparation de livraison dans OpenRPG Forge. Utiliser après une implémentation, avant d'annoncer une capacité terminée, ou lorsqu'une demande exige compilation, tests, vérification du graphe et preuves reproductibles. Ne pas utiliser pour une simple question de statut en lecture seule qui n'autorise aucune régénération.
---

# Valider une livraison OpenRPG

Appliquer un contrôle proportionné au risque, puis produire des preuves exactes sans confondre test automatisé, revue visuelle et publication distante.

## Établir le périmètre

1. Confirmer la racine Git `E:\OpenRPG_Forge\OpenRPG_Forge`.
2. Lire intégralement `AGENTS.md`, puis `STATUS.md`, `TASKS_MVP.md` et les ADR ou documents du lot concerné.
3. Examiner `git status --short`. Traiter les modifications préexistantes comme appartenant à l'utilisateur ; ne jamais les nettoyer, les restaurer ou les inclure par défaut.
4. Identifier les fichiers touchés, les critères d'acceptation et les preuves attendues avant de modifier le projet.

## Utiliser le graphe d'architecture

Pour une modification transversale, appliquer le contrat courant de `AGENTS.md` :

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- check
dotnet run --project tools/Forge.ArchitectureGraph -- query "<module ou symbole>" --mode impact --format context --depth 2 --max-nodes 30
dotnet run --project tools/Forge.ArchitectureGraph -- query "<module ou symbole>" --mode tests --format paths --depth 2 --max-nodes 40
```

Ne jamais charger entièrement `docs/architecture/graph.json` et ne jamais éditer les artefacts générés à la main.

Après la dernière modification d'une entrée indexée, quand l'arbre est stable :

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- generate
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
```

Pour une demande de diagnostic ou de statut uniquement, exécuter `check` sans `generate` et rapporter l'état observé.

## Valider par paliers

1. Exécuter d'abord les tests ciblés déterminés par le graphe et le sous-système.
2. Exécuter les builds ou smoke tests directement liés au changement.
3. Pour du code, une clôture de gate ou une préparation de publication, exécuter le gate intégré :

```powershell
& '.\scripts\Verify.ps1'
```

4. Pour un changement documentaire seulement, limiter la validation aux contrôles applicables, à `git diff --check` et au graphe si le document est indexé.
5. Arrêter sur l'échec pertinent, conserver son message exact et diagnostiquer avant de poursuivre. Ne jamais déduire un succès global d'un test partiel.

## Rendre compte

Indiquer séparément :

- périmètre réellement validé ;
- commandes exécutées et résultats chiffrés ;
- état du graphe ;
- preuve runtime, visuelle ou manuelle réellement observée ;
- limites et vérifications non effectuées ;
- publication distante effectuée ou non.

N'annoncer la tâche terminée que si les critères, le build, les tests, la documentation et le diff sont cohérents.
