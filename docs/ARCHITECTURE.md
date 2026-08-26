# Architecture de référence

Ce document résume les frontières à préserver. Le contexte produit et les principes structurants se
trouvent dans `PROJECT_CONTEXT.md`; les décisions détaillées sont dans `docs/ADR/`.

## Flux principal

```text
Forge Editor / Runtime / MCP
            |
            v
    Forge.Application
  commandes et validation
            |
            v
      Forge.Domain
     modèle RPG pur
       /          \
      v            v
Persistence     Engine adapters
JSON/.orpmod    client/headless
                     |
                     v
              ForgeScript host
```

Le Blender Worker reste un processus Python séparé. Le MCP Host reste un processus C# séparé et communique avec l’éditeur par loopback authentifié ou pipe local.

Le prototype MCP emploie exclusivement un pipe nommé limité à l'utilisateur courant pour le pont
éditeur et `stdio` pour le client MCP. Les plans de mutation passent par les handlers de
`Forge.Application`; aucun accès générique aux fichiers, au shell ou à une écoute TCP n'est exposé.

## Génération de monde

```text
MCP
 |
 v
WorldGenerationRecipe (Domain)
 |
 v
WorldGenerationExecutionPlan + Job (Application)
 |
 +--> JSON atomique et borné (ModuleFormat)
 |
 `--> requête de bridge (EngineAdapter)
          |
          +--> Terrain3D
          +--> Godot Flow / PCG
          `--> Godot natif
```

Le MCP conserve l'orchestration, les permissions et les révisions. Il ne référence aucun type des
addons. Le protocole contient une opération allowlistée et deux empreintes, jamais du code source.
Les plans et jobs sont reconstructibles après redémarrage ; les scènes moteur qu'ils produisent sont
dérivées. Le contrat détaillé et ses limites sont dans `docs/world-generation/README.md` et
`docs/ADR/ADR-0050-governed-world-generation-orchestration.md`.

## Règles de dépendance

- `Forge.Domain` ne dépend d’aucun framework moteur, stockage ou transport.
- `Forge.Application` orchestre les commandes et dépend du domaine, jamais du moteur 3D.
- Persistence et ModuleFormat traduisent les données versionnées.
- Les adaptateurs moteur convertissent explicitement les valeurs du domaine.
- Toolset, Runtime et MCP appellent l’application ; ils ne modifient pas directement l’état persistant.
- Les objets de scène sont des représentations reconstructibles.
- Les dépendances de génération externes restent derrière EngineAdapter et l'addon Godot ; leur
  absence active un fallback natif plutôt que de contaminer Domain ou Application.

## Profils RPG avancés

Les opérations avancées de quête et d’objet restent des profils optionnels attachés aux agrégats
canoniques. Le domaine pur valide et résout ordre, seed, contexte et autorité ; l’application porte
les transactions ; Toolset, ForgeScript, MCP et runtime ne sont que des consommateurs. Les registres
d’unicité et recettes utilisent des opérations atomiques explicites. Voir
`docs/ADR/ADR-0039-canonical-quest-item-depth.md`.

## Bootstrap minimal

Le premier lot ne crée que :

- `Forge.Domain` ;
- `Forge.Application` ;
- `Forge.ModuleFormat` ;
- `Forge.Domain.Tests` ;
- `Forge.Toolset` comme application de création .NET et extension C# minimale.

Les autres projets de la structure cible sont ajoutés lorsqu’une tâche testable les exige.
