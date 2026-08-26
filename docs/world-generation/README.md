# Génération de monde gouvernée

La génération de monde est une fondation transversale, pilotée par le MCP et indépendante des addons
Godot dans ses couches canoniques. Elle transforme une recette approuvée en plan déterministe, puis en
job durable consommé séquentiellement par Godot.

```text
Utilisateur ou Codex
        |
        v
MCP OpenRPG Forge -- dry-run, révisions, permission, audit
        |
        v
Recette versionnée -- intention, seed, règles, backends
        |
        v
Plan d'exécution -- étapes, dépendances, empreinte stable
        |
        v
Job JSON durable -- queued/running/succeeded/failed/cancelled
        |
        v
Addon Godot OpenRPG
   | Terrain3D       terrain, hydrologie
   | Godot Flow      biomes, routes, établissements, scatter
   ` Godot natif     repli pour toutes les étapes
        |
        v
Gameplay, navigation et validation
```

## Recette et exécution

Une recette décrit l'intention : zone cible, seed, dimensions, relief, rivières, biomes, routes,
établissements, scatter, tables de gameplay, navigation, qualité, backends et protection des retouches.
Elle est sérialisée dans `world/generation/recipes.json` avec sa révision.

Un plan est un instantané d'exécution. Il contient `planId`, `transactionId`, révision projet,
révision recette, empreinte de recette, étapes ordonnées et `planFingerprint`. Deux previews
équivalentes ont la même empreinte même si leurs identifiants de suivi diffèrent. Changer une seed,
une révision, un paramètre ou le sous-ensemble d'étapes change l'empreinte.

Une mutation de recette suit obligatoirement : lecture des révisions, dry-run, examen du diff, puis
application du `planId` si les révisions n'ont pas changé. Une exécution suit : preview persistée,
examen des diagnostics, puis mise en file. Le worker ne reçoit jamais de code à exécuter, seulement
une opération issue d'une allowlist fermée.

## Étapes et backends

| Ordre canonique | Étape | Backend automatique | Sortie actuelle |
|---:|---|---|---|
| 0 | Terrain | Terrain3D | heightfield spécialisé ou maillage/collision natifs |
| 1 | Hydrology | Terrain3D | import spécialisé ou ancres natives de rivières |
| 2 | Biomes | Godot Flow | graphe PCG ou ancres natives de biome |
| 3 | Roads | Godot Flow | graphe PCG ou ancres natives de route |
| 4 | Settlements | Godot Flow | graphe PCG ou ancres natives d'établissement |
| 5 | Scatter | Godot Flow | graphe PCG ou ancres natives de scatter |
| 6 | Gameplay | Godot natif | contrat/ancres de gameplay |
| 7 | Navigation | Godot natif | contrat de bake de navigation |
| 8 | Validation | Godot natif | présence du terrain, budget de nœuds et empreintes de validation |

Une preview peut préciser `stages` pour une régénération partielle. Les étapes sélectionnées sont
triées dans l'ordre canonique, dédupliquées et reliées par une chaîne de dépendances contiguë. Une
liste vide ou une valeur inconnue est rejetée.

`Terrain3D` et `PcgGodot` sont des préférences explicites, pas des dépendances du domaine. Une
association incohérente — par exemple Terrain3D pour Gameplay — invalide la recette. Si un addon ou
un graphe manque au moment de l'exécution, le bridge emploie le backend natif et inscrit ce repli dans
l'audit du job.

## Déterminisme et régénération manuelle

- `WorldGenerationRecipe.CreateDefault` dérive ses identifiants de l'area, du titre et de la seed.
- Les collections sont normalisées et triées avant calcul de l'empreinte de recette.
- Le plan emploie une sérialisation canonique bornée pour son empreinte SHA-256.
- Le backend natif emploie la seed de recette et un décalage stable par étape.
- `regeneration.preserveManualEdits` vaut `true` par défaut.
- Une racine ou un nœud peut porter `openrpg_manual_lock = true`.
- `regeneration.protectedNodePaths` accepte uniquement des chemins de nœud relatifs, bornés et sans
  traversée `..`.

La protection évite de remplacer le conteneur déclaré ; elle ne fusionne pas encore finement deux
géométries concurrentes. Une retouche à préserver doit donc rester sous un conteneur protégé ou hors
de `OpenRPGGeneratedWorld_<recipeId>`.

## Persistance et reprise

Les écritures C# sont atomiques et les fichiers sont confinés au workspace :

- `world/generation/recipes.json` : catalogue et révision ;
- `world/generation/plans/<planId>.json` : preview déterministe ;
- `world/generation/jobs/<planId>.json` : état et audit du worker ;
- `world/generation/artifacts/` : requêtes de bridge produites par les adaptateurs.

Le plugin Godot lit les jobs `queued`, vérifie schéma, identifiants, révisions, empreintes, ordre,
dépendances et opérations, écrit `running`, exécute séquentiellement, puis écrit un état terminal.
Après une interruption, un job resté `running` est repris et réexécuté de manière déterministe avec
une entrée d'audit `resumed`. Une annulation persistée est contrôlée entre les étapes et n'est pas
écrasée par le worker. Un job déjà terminal n'est pas rejoué. La preview et l'inverse d'une mutation de recette restent, elles, limitées à la session MCP ;
le catalogue appliqué et sa révision sont bien restaurés.

Configurer dans Godot :

- `openrpg/world_generation/workspace_root` vers la racine réelle du module Forge ;
- `openrpg/world_generation/auto_process_jobs` à `true` pour le polling automatique ;
- `openrpg/world_generation/poll_seconds` selon la fréquence souhaitée, minimum 0,25 s.

## Utilisation depuis Codex

Codex doit employer les outils MCP, sans modifier directement les JSON :

1. appeler `GetWorldGenerationRecipes` et conserver les deux révisions ;
2. construire un catalogue borné et appeler `PlanWorldGenerationRecipes` ;
3. présenter ou examiner diff, diagnostics et provenance ;
4. appeler `ApplyWorldGenerationRecipePlan(planId)` après approbation ;
5. appeler `PreviewWorldGenerationExecution`, avec `stages` si la régénération est partielle ;
6. vérifier `readyToApply`, les backends, étapes et empreintes ;
7. appeler `QueueWorldGenerationExecutionPlan(planId)` ;
8. suivre `GetWorldGenerationJob(s)` et ne déclarer le résultat produit qu'après l'état terminal ;
9. utiliser `CancelWorldGenerationJob` pour un job actif ou
   `RevertWorldGenerationRecipePlan` pour la dernière mutation de recette encore réversible dans la
   session.

Toute proposition IA doit conserver une provenance explicite. L'approbation d'une recette et la
permission `ApplyApprovedPlan` ne sont pas déduites d'un texte libre.

## Étendre le système

### Nouveau générateur

1. Ajouter ou étendre l'enum et `WorldGenerationBackendPolicy` dans Domain.
2. Définir l'opération bornée dans `WorldGenerationOperations` et le planificateur.
3. Ajouter l'adaptateur dans EngineAdapter sans référence depuis Domain/Application/MCP.
4. Ajouter la capacité au probe, au manifeste épinglé et à l'installateur si elle est externe.
5. Allowlister exactement les couples backend/étape côté GDScript.
6. Ajouter tests de déterminisme, sérialisation, fallback et smoke Godot, puis mettre à jour l'ADR.

### Nouveau graphe PCG

Créer une ressource Godot Flow sous `res://world/generation/graphs/`, puis associer l'étape à son
chemin dans `addons/openrpg_world_generation/graph_registry.json`. Le graphe doit consommer les
overrides nommés du plan, rester déterministe pour la seed reçue et produire ses nœuds sous le
conteneur fourni. Un graphe absent ou illisible déclenche le fallback ; il ne doit jamais provoquer
le chargement d'un script fourni dans une recette.

### Nouveau biome

Ajouter une `BiomeGenerationRecipe` avec ID stable, `biomeKey`, règle de région, densité et tags
d'assets gouvernés. Adapter ensuite le graphe `biomes.tres` pour interpréter cette clé. Les assets et
leur provenance restent gérés par le catalogue d'assets, pas par la recette de génération.

### Nouveau type d'établissement

Ajouter une `SettlementGenerationRecipe` avec ID stable, type, style, règle de placement, budget de
bâtiments, enceinte et landmarks requis. Étendre le graphe `settlements.tres` et ses tests de budget,
collision, navigation et accessibilité. Ne coder aucun nom de module ou de jeu dans le moteur.

## Validation

Les tests ciblés couvrent déterminisme, sélection partielle, fallback, persistance/réouverture,
altération d'empreinte, audit, bridge et exécution native Godot. Les commandes reproductibles sont
réunies dans [CODEX_HANDOFF.md](CODEX_HANDOFF.md).

## Limites actuelles

- Les graphes `biomes.tres`, `roads.tres`, `settlements.tres` et `scatter.tres` sont des points
  d'extension attendus dans chaque module ; aucun graphe artistique générique n'est livré ici.
- Le fallback natif fournit un terrain réellement maillé et collisionné. Les autres étapes sont une
  fondation déterministe à ancres, pas un générateur artistique de production.
- Le worker modifie l'arbre de scène actif mais ne promeut ni ne sauvegarde encore automatiquement
  ces résultats dans `AreaWorldProfile` ou une scène canonique du workspace.
- Le contrat Navigation ne constitue pas encore une preuve de bake et de reachability sur une vraie
  carte générée ; cette qualification doit accompagner le premier module consommateur.
- Terrain3D annonce officiellement Godot 4.4 à 4.6+ pour la version épinglée. Le smoke natif et le
  parsing du bridge passent sous 4.7.1, mais Terrain3D 4.7.1 doit être validé avec l'addon installé.

Voir aussi [les dépendances](DEPENDENCIES.md), [le handoff Codex](CODEX_HANDOFF.md) et
[l'ADR-0050](../ADR/ADR-0050-governed-world-generation-orchestration.md).
