# Graphe d’architecture

Ce répertoire contient l’index d’architecture déterministe d’OpenRPG Forge. Le code et les
documents du dépôt restent les sources de vérité ; le graphe ne contient que des relations
prouvées statiquement et peut être entièrement régénéré.

## Commandes

Depuis la racine du dépôt :

```powershell
python scripts/architecture_graph.py generate
python scripts/architecture_graph.py check
python scripts/architecture_graph.py stats
python scripts/architecture_graph.py query "AreaService"
python scripts/architecture_graph.py query "AreaService" --format paths
python scripts/architecture_graph.py query "AreaService" --format json
python scripts/architecture_graph.py query "AreaService" --format mermaid
```

Une requête est bornée par défaut à une profondeur de `1` et à `50` nœuds. Utiliser `--depth`
et `--max-nodes` seulement lorsque le premier sous-graphe ne suffit pas. Les options `--root` et
`--output` permettent d’analyser une fixture ou un autre checkout.

`generate` écrit `graph.json` et `overview.mmd` par remplacements atomiques. `check` reconstruit
les deux contenus en mémoire, ne modifie aucun fichier et échoue si un artefact manque ou diffère.
Il n’utilise ni réseau, ni LLM, ni base de données, ni service externe.

## Schéma

`graph.json` utilise actuellement `schema_version: 1` et contient :

- `metadata` : version, analyseurs, nombre de sources et empreinte SHA-256 des fichiers analysés ;
- `nodes` : identifiant stable, type, nom, chemin relatif et ligne lorsque disponibles ;
- `edges` : source, cible, relation et preuve `{ path, line }` ;
- `diagnostics` : syntaxes invalides ou entrées ambiguës ignorées proprement.

Les chemins utilisent toujours `/`. Les listes, clés JSON et preuves sont triées. Aucune heure,
information machine, variable d’environnement ou chaîne issue du contenu métier n’est copiée dans
le graphe.

## Relations prises en charge

| Relation | Preuve utilisée |
|---|---|
| `defined_in` | déclaration de module, type, opération ou route |
| `imports` | `using` C# vers un namespace interne, `ProjectReference` .NET, import Python, include C++ local ou référence Godot/GDScript explicite |
| `references` | lien Markdown ou chemin exact placé entre backticks |
| `calls_api` | référence explicite d’un composant vers un client, ou appel HTTP détecté dans ce client |
| `calls_route` | même verbe HTTP et même route normalisée côté client et serveur |
| `handled_by` | handler explicite d’un `MapGet`/`MapPost`/… ou contrôleur portant un attribut HTTP |
| `uses_service` | type d’interface/handler référençant un service unique |
| `uses_repository` | service référençant un repository unique |
| `accesses_model` | service ou repository référençant un modèle unique |
| `uses_model` | client API ou handler référençant un modèle unique |
| `serializes` | serializer, converter ou adapter référençant un modèle unique |
| `validates` | validator référençant un modèle unique |
| `relationship` | modèle référençant explicitement un autre modèle unique |
| `tests` | import Python de test ou type C# de test référençant un symbole unique |

Les paramètres de route `{id}`, `${id}` et `:id` deviennent `{}`. Le verbe reste dans la clé :
un `POST` client n’est jamais relié à un `GET` voisin.

## Couverture actuelle et limites

Le dépôt contient maintenant le domaine, le bus de commandes, le format de projet, l’adaptateur
moteur, le toolset, le runtime et leurs tests. Le graphe indexe ces projets, leurs namespaces, les
types C# et les références internes observables. L’état de validation fonctionnelle du produit
reste suivi séparément dans `STATUS.md` et `TASKS_MVP.md`.

L’analyse C# est lexicale et volontairement prudente. Elle couvre les namespaces, types, `using`,
clients `HttpClient`, routes ASP.NET usuelles et dépendances nommées selon les conventions du projet.
Elle ignore notamment les routes construites dynamiquement, les alias ambigus, la réflexion, les
appels résolus uniquement à l’exécution et les raw strings complexes. Python est analysé avec
`ast`; C++ ne fournit que ses includes locaux. Les scripts, scènes et configurations Godot relient
les chemins `res://` existants, et `plugin.cfg` relie son script relatif lorsqu’il existe.
Le simple nom d’un test ne suffit pas à créer une relation vers du code : une référence ou un import
doit être observable.

Les répertoires de dépendances, caches et builds, les environnements, secrets, bases et formats
binaires sensibles sont exclus. Les migrations ne sont pas exclues globalement : elles seront
indexées lorsqu’elles feront partie de l’architecture réelle.

## Fichiers générés

- `graph.json` : index machine complet, à interroger plutôt qu’à charger intégralement ;
- `overview.mmd` : agrégation lisible des principaux types de nœuds et relations.

Ne jamais les modifier manuellement. Toute évolution du schéma exige un incrément explicite de
`SCHEMA_VERSION`, une régénération et des tests de compatibilité adaptés.
