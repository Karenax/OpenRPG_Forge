# Graphe d’architecture Roslyn

Ce répertoire contient l’index d’architecture déterministe d’OpenRPG Forge. Le code et les
documents du dépôt restent les sources de vérité. Le graphe est produit localement par
`Forge.ArchitectureGraph` avec Roslyn pour les projets C#, puis complété par des liens syntaxiques
prudents pour Markdown, C++, Godot/GDScript et Python.

## Commandes

Depuis la racine du dépôt :

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- generate
dotnet run --project tools/Forge.ArchitectureGraph -- check
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
dotnet run --project tools/Forge.ArchitectureGraph -- stats
dotnet run --project tools/Forge.ArchitectureGraph -- query "AreaService" --mode impact --format context
dotnet run --project tools/Forge.ArchitectureGraph -- query "AreaService" --mode tests --format paths
```

`generate` remplace atomiquement `graph.json` et `overview.mmd`. `check` compare rapidement le
manifeste de sources et l’aperçu sans reconstruire les compilations Roslyn. `check --full` reconstruit
également le graphe canonique et vérifie son déterminisme octet par octet. Ces commandes n’utilisent
ni réseau, ni LLM, ni base de données, ni service externe.

## Contexte destiné au LLM

Le graphe complet ne doit jamais être chargé dans le contexte d’un agent. Une requête produit un
sous-graphe borné avec les fichiers, les relations typées, leur niveau de confiance et la preuve
`fichier:ligne`.

Avant une modification transversale :

1. vérifier que le graphe est frais avec `check` ;
2. interroger l’impact avec `--mode impact --format context --depth 2 --max-nodes 30` ;
3. interroger séparément les tests avec `--mode tests --format paths --depth 2 --max-nodes 40` ;
4. ouvrir les fichiers obtenus et confirmer les relations dans le code avant de modifier.

Les modes disponibles sont :

- `dependencies` : ce dont le symbole dépend ;
- `dependents` : ce qui dépend du symbole ;
- `tests` : tests directement ou transitivement reliés ;
- `impact` : vue bidirectionnelle priorisant tests, héritage, routes et dépendances métier.

Les formats `text`, `paths`, `json`, `mermaid` et `context` sont bornés par `--depth` et
`--max-nodes`. `context` est le format recommandé pour le raisonnement du LLM ; `paths` est le
format recommandé pour établir rapidement la liste de fichiers à examiner.

## Schéma

`graph.json` utilise `schema_version: 2` :

- `metadata` : moteur Roslyn, version, empreinte SHA-256 normalisée, nombre et manifeste des sources ;
- `nodes` : identifiant stable, type, nom, chemin relatif, ligne et métadonnées ;
- `edges` : source, cible, relation, confiance `semantic` ou `syntactic`, et preuve `{path,line}` ;
- `diagnostics` : entrée invalide ou ambiguë ignorée sans interrompre la génération.

Les chemins utilisent `/`, les fins de ligne sont normalisées et toutes les collections sont triées.
Le graphe ne dépend donc ni du chemin absolu du checkout, ni de la machine, ni du format LF/CRLF.

## Relations prises en charge

| Relation | Signification |
|---|---|
| `defined_in`, `belongs_to`, `contains` | appartenance fichier, symbole et projet |
| `imports`, `references` | référence de compilation ou lien source explicite |
| `implements`, `inherits` | contrat ou héritage résolu par Roslyn |
| `calls_api`, `calls_route`, `handled_by` | client HTTP, route normalisée et handler |
| `uses_service`, `uses_repository` | dépendance applicative résolue |
| `accesses_model`, `uses_model` | consommation d’un modèle |
| `serializes`, `validates`, `relationship` | sérialisation, validation ou relation de domaine |
| `tests` | test référençant un symbole de production |

Les relations C# s’appuient sur les symboles Roslyn à travers les `ProjectReference`. Les références
externes provenant de paquets NuGet ne sont pas chargées dans cette première version : aucune arête
spéculative n’est créée pour une API externe non résolue. Les routes construites dynamiquement, la
réflexion et les dépendances exclusivement runtime restent hors couverture statique.

Les dépendances, caches, sorties de build, secrets, bases sensibles et états de travail `.forge/`
sont exclus. Les workspaces de modules sous `apps/Forge.Runtime/content/` sont également exclus :
leur contenu (cartes, placements, assets et réglages) est validé par `forge validate`, pas par le
graphe du moteur. Une évolution de contrat, de schéma, de runtime ou d'interface Studio reste en
revanche une évolution d'architecture et impose la régénération du graphe. Les smoke tests peuvent
ainsi modifier leurs états locaux sans rendre l’index d’architecture périmé.

## Fichiers générés

- `graph.json` : index machine complet à interroger avec la CLI ;
- `overview.mmd` : agrégation Mermaid lisible des principaux types et relations.

Ces deux fichiers sont générés et ne doivent jamais être modifiés manuellement.
