# G7-01 — Second module générique

Statut : terminé le 2 août 2026.

## Features

- `ORF-PROJ-001` — assistant de création de projet ;
- `ORF-PROJ-003` — identité technique stable ;
- `ORF-PROJ-004` — première zone obligatoire ;
- `ORF-PROJ-005` — point de départ du joueur ;
- `ORF-QA-010` — tests runtime headless.

## Résultat

Créer une seconde fixture originale avec le même bus de commandes, la sauvegarder, l’exporter puis
la charger dans le runtime sans chemin, slug, personnage ou logique propre à la Crypte dans le flux
de démarrage générique.

## Fichiers autorisés

- `src/Forge.Application/Projects/` ;
- `src/Forge.Domain/Projects/`, `Modules/`, `Areas/` et `Blueprints/` si un invariant l’exige ;
- `src/Forge.ModuleFormat/Playtesting/` et `Serialization/` ;
- `apps/Forge.Runtime/Runtime/` ;
- une nouvelle fixture sous `tests/fixtures/` ou `apps/Forge.Runtime/content/` ;
- tests correspondants, documentation et tracker projet.

## Non-objectifs

- extraction complète du RPG Kit ;
- nouvelle coque d’interface ;
- réseau ou Live Director ;
- assets de production ;
- modification du moteur 3D ou nouvelle dépendance ;
- généralisation d’un framework sans second cas réel.

## Acceptation

1. La commande de nouveau module produit un projet avec UUID, slug, première zone et PlayerSpawn.
2. Une seconde fixture ne contenant aucune référence à `crypt-of-ashes`, Elyra ou au Gardien est
   créée exclusivement depuis les contrats Forge.
3. Le runtime charge cette fixture par argument explicite et démarre au PlayerSpawn.
4. Le scénario headless générique vérifie reconstruction, déplacement ou interaction minimale et
   sortie propre sans exécuter le scénario de la Crypte.
5. La fixture est sauvegardée, rechargée, exportée, validée et chargée depuis `.orpmod`.
6. Le scénario complet de la Crypte reste vert.

## Tests

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj -c Release
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj -c Release
.\scripts\Verify.ps1
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
```

## Preuves

- fixture canonique `apps/Forge.Runtime/content/broken-isles-starter/` ;
- égalité avec la sortie de `NewModuleHandler` et round-trip `.orpmod` dans
  `BrokenIslesStarterTests` ;
- scénario `generic-starter` dans `scripts/Verify.ps1` ;
- démarrage sans profil de combat, sans atmosphère ni acteur propre à la Crypte ;
- interaction vide propre et retour du processus avec succès ;
- non-régression complète de la Crypte dans le même gate.
