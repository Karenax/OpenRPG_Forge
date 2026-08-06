# Architecture de référence

Ce document résume les frontières à préserver. Le détail complet se trouve dans les sections 5 à 8 de `MEGACONTEXT.md` et les décisions dans `docs/ADR/`.

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

## Règles de dépendance

- `Forge.Domain` ne dépend d’aucun framework moteur, stockage ou transport.
- `Forge.Application` orchestre les commandes et dépend du domaine, jamais du moteur 3D.
- Persistence et ModuleFormat traduisent les données versionnées.
- Les adaptateurs moteur convertissent explicitement les valeurs du domaine.
- Toolset, Runtime et MCP appellent l’application ; ils ne modifient pas directement l’état persistant.
- Les objets de scène sont des représentations reconstructibles.

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
