---
name: openrpg-delivery-gate
description: Valider un jalon transversal, une clôture de vague ou une préparation de livraison dans OpenRPG Forge. Utiliser après plusieurs lots liés, après un changement de contrat partagé, avant d'annoncer un jalon terminé, ou lorsqu'une demande exige le portail global et des preuves reproductibles. Pour une modification courante, utiliser d'abord openrpg-targeted-verification. Ne pas utiliser pour une simple question de statut en lecture seule.
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

1. Utiliser d'abord `$openrpg-targeted-verification` pour les tests, builds et smokes directement liés.
2. À un jalon de sous-système, exécuter le profil concerné :

```powershell
& '.\scripts\Verify.ps1' -Profile <Core|Studio|Runtime|Assets|Network|Distribution>
```

3. Exécuter le portail transversal uniquement après trois à cinq lots liés, dix à vingt routes, une
   fin de vague, un changement de contrat partagé important ou une préparation de publication :

```powershell
& '.\scripts\Verify.ps1'
```

4. Pour un changement documentaire seulement, limiter la validation aux contrôles applicables, à `git diff --check` et au graphe si le document est indexé.
5. Si le tracker Aurora ou son plan est concerné, exécuter `scripts/Update-AuroraParityTracker.ps1 -Check` et `scripts/Update-CompleteFunctionBacklog.ps1 -Check`.
6. Arrêter sur l'échec pertinent, conserver son message exact et diagnostiquer avant de poursuivre. Ne jamais déduire un succès global d'un test partiel.

## Rendre compte

Indiquer séparément :

- périmètre réellement validé ;
- commandes exécutées et résultats chiffrés ;
- état du graphe ;
- preuve runtime, visuelle ou manuelle réellement observée ;
- limites et vérifications non effectuées ;
- publication distante effectuée ou non.

N'annoncer la tâche terminée que si les critères, le build, les tests, la documentation et le diff sont cohérents.
