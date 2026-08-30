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
        |
        v
Scène dérivée neutre + rapport de validation
        |
        v
MCP -- preview, acceptation explicite, promotion atomique, inverse
        |
        v
Scène canonique référencée par AreaWorldProfile
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
- `world/generation/artifacts/<planId>/derived-world.tscn` : candidat neutre produit par le worker ;
- `world/generation/artifacts/<planId>/validation-report.json` : preuve liée au job, aux révisions,
  aux empreintes et aux fournisseurs ;
- `world/generation/promotions/<areaId>.json` : manifeste de la promotion canonique ;
- `world/areas/<areaId>/generated-world.tscn` : copie canonique référencée par le profil de chargement.

Le plugin Godot lit les jobs `queued`, vérifie schéma, identifiants, révisions, empreintes, ordre,
dépendances et opérations, écrit `running`, exécute séquentiellement, puis écrit un état terminal.
Après une interruption, un job resté `running` est repris et réexécuté de manière déterministe avec
une entrée d'audit `resumed`. Une annulation persistée est contrôlée entre les étapes et n'est pas
écrasée par le worker. Un job déjà terminal n'est pas rejoué. La preview et l'inverse d'une mutation de recette restent, elles, limitées à la session MCP ;
le catalogue appliqué et sa révision sont bien restaurés.

## Promotion canonique

Le worker ne promeut jamais par effet secondaire. Après un job complet réussi, il bake une scène
sans référence Terrain3D, Godot Flow ou GF-PCGODOT et écrit un rapport de validation. La commande MCP
de promotion vérifie le job, les révisions projet/recette, les empreintes recette/plan/scène/contenu,
la collision, la navigation, la préservation des retouches et exactement un fournisseur prouvé par
étape. Une première promotion exige toutes les étapes du plan.

Le rapport publie aussi deux preuves spatiales liées par empreinte : une surface de placement régulière
extraite du mesh final et une occupation environnementale agrégée en cellules de quatre mètres. Elles
sont validées puis persistées dans `AreaWorldProfile`. Elles restent disponibles même lorsque le
provider de terrain est désactivé au profit de la scène neutre ; l'authoring ne doit donc jamais relire
ou approximer la géométrie du `.tscn` pour décider d'une hauteur ou d'un site.

Une promotion canonique est un livrable de production, pas la validation d'une preview technique.
La recette déclare donc un `artDirection` de niveau `Production` et une zone extérieure de référence.
La promotion matérialise sur le relief final ses couches de terrain réellement texturées, ses biomes
dotés de ressources de scatter, son environnement et les routes texturées demandées par la recette.
Elle refuse notamment une surface trop peu échantillonnée pour sa taille, une peinture matière
incomplète, une référence sans trois strates de végétation, une route demandée sans matériau, un point
joueur dont le corridor caméra de 64 m est trop accidenté, ou une fondation générée éloignée de plus de
1,25 m de la surface canonique. Si la recette autorise `adaptToStructures`, une dérive locale inférieure
ou égale à 2 m est corrigée de façon déterministe sous l'emprise complète du groupe de bâtiment puis
revalidée ; une dérive plus grande reste bloquante. La surface de placement persistée est toujours cette
surface finale. Les proxies du worker restent utiles au diagnostic, mais sont supprimés au runtime dès
que ces preuves de production sont présentes.

Pour un établissement généré, les couches de couverture du sol et d'arbustes sont concentrées autour
des fondations. Leur budget est calculé sur cette emprise locale et les candidats y sont tirés
directement ; agrandir la carte ne peut donc ni diluer l'herbe près du village ni multiplier les
tentatives hors zone. L'emprise réelle de chaque maison produit aussi des exclusions invisibles pour la
végétation basse et une marge distincte pour les arbres ; une liste de tags vide signifie ici « toute
strate compatible » et non « aucune exclusion ». La promotion refuse aussi une patrouille lente qui ne
possède qu'une animation de course. Au runtime, les acteurs terrestres recalculent leur hauteur sur le
terrain pendant toute la patrouille et leur visuel est aligné sur ses pieds, pas sur le pivot arbitraire
du fichier importé.

La preview exige `ApplyApprovedPlan` et une provenance d'acceptation explicite. L'application recopie
atomiquement la scène vers le chemin stable de l'area, conserve le manifeste de provenance, ajoute la
ressource au `AreaWorldProfile.Loading.RequiredResources`, sauvegarde le projet puis vérifie sa
réouverture. Le remplacement d'une scène existante exige son SHA-256 courant. L'inverse restaure à la
fois les fichiers et l'état projet ; les retouches concurrentes non reconnues sont refusées.

Configurer dans Godot :

- `openrpg/world_generation/workspace_root` vers la racine réelle du module Forge ;
- `openrpg/world_generation/auto_process_jobs` à `true` pour le polling automatique ;
- `openrpg/world_generation/poll_seconds` selon la fréquence souhaitée, minimum 0,25 s.

## Utilisation depuis Studio

La page **Monde > Génération de monde** est le parcours humain du même workflow. Elle affiche la
zone, la recette, la seed, les backends, les révisions, le plan, le job et la promotion courante.
Les actions de preview, application, exécution et promotion utilisent la session persistante du
workspace : une mutation MCP concurrente provoque donc un conflit de révision au lieu d'être
écrasée. Le worker est lancé hors processus et ses journaux, rapports et captures restent liés au
plan affiché. Studio ne transforme jamais un texte libre en permission d'application.

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
9. inspecter le candidat dans le vrai renderer Forward+ puis appeler `PlanWorldGenerationPromotion`
   avec une provenance `human-forward-plus:<revue>` et, en cas de
   remplacement, le SHA-256 canonique actuellement observé ;
10. examiner le diff, l'inverse, la scène cible et les diagnostics de preuve ;
11. appeler `ApplyWorldGenerationPromotion(promotionPlanId)` sans changer les révisions ;
12. vérifier avec `GetCanonicalWorldGenerationPromotion`, puis appeler
   `FindTerrainPlacementCandidates` avant tout plan extérieur contenant des structures ; conserver les
   empreintes de surface et d'occupation, la variation du sol, la densité voisine et le compte nul dans
   l'emprise ;
13. construire le plan en mode `TerrainSurface`, avec un `environmentIntent` explicite ; le dry-run
   doit refuser toute emprise pentue, hors surface, végétalisée, tout placeable tagué PNJ et toute maison
   sans composition `foundation` + `wall` + `roof` ;
14. employer
   `RevertWorldGenerationPromotion` si l'application doit être annulée ;
15. utiliser `CancelWorldGenerationJob` pour un job actif ou
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
altération d'empreinte, audit, bridge, exécution native Godot et promotion preview/apply/reopen/revert.
Le harnais qualifie en plus les addons réels, trois exécutions déterministes, collision, navigation,
scène neutre, cinq captures techniques et candidat de promotion. Les commandes reproductibles sont
réunies dans [CODEX_HANDOFF.md](CODEX_HANDOFF.md).

La qualification de la carte finale intervient après la construction assistée, lorsque terrain,
bâtiments, personnages, patrouilles et caméra sont présents ensemble. Lancer
`scripts/Invoke-MapQualityAudit.ps1` avec le workspace, le spawn et les minima issus du brief. Le
runtime Forward+ Vulkan écrit `map-quality-machine-audit.json` et au minimum trois PNG. Aux vues
`overview`, `overhead` et `ground-level` s'ajoutent obligatoirement `patrol-character` si le brief
demande un PNJ et, s'il demande un bâtiment, `building-detail`, `door-closed`, puis `door-open`
sur le même sujet après exécution réelle de l'interaction. L'audit mesure la résolution et la
couverture matière du terrain, la densité locale, les contacts racine/pieds, la cohérence
vitesse-animation, le bas visuel des fondations, les intersections végétation-bâtiments, la marge des
arbres, le centre et le plan du maillage réel de la porte fermée par rapport à l'encadrement, le centre
visuel de l'escalier, le raccord escalier-seuil, le caractère cliquable de chaque
porte, son ouverture puis sa fermeture, et l'absence d'occlusion dans les corridors de capture. Le marqueur machine ne vaut pas
acceptation : le réviseur inspecte chaque image, puis appelle
`scripts/Confirm-MapQualityVisualReview.ps1` avec la baseline produit réellement comparée et
l'attestation `EntrancesAlignedAndOperable` en plus de l'absence de régression. L'acceptation finale
est liée aux SHA-256 des captures.
Une carte visible ne doit jamais être déclarée réussie à partir des seules captures techniques du
worker ou de compteurs globaux de scène.

La tranche Solaren du 28 août 2026 prouve en plus le cycle produit complet : recette ouverte dans le
workspace, exécution réelle GPU, promotion vers une seconde zone, chargement par Runtime, transition
avec état de session conservé, revue Forward+ et empaquetage `.orpmod`. Le runtime vérifie la scène
canonique `generated-world.tscn` et émet son SHA-256, le nombre de meshes, MultiMeshes, collisions et
navigations. Les preuves reproductibles sont sous
`artifacts/world-generation/solaren-vertical-slice/` et la décision dans
[`ADR-0055`](../ADR/ADR-0055-runtime-world-generation-vertical-slice.md).

## Limites actuelles

- Les graphes `biomes.tres`, `roads.tres`, `settlements.tres` et `scatter.tres` restent détenus par
  les modules. Le worker de référence sait produire une composition artistique bornée, mais ce style
  n'est ni une bibliothèque universelle ni un remplacement de la direction artistique humaine.
- Le fallback natif fournit un terrain réellement maillé et collisionné. Hors de la recette Solaren,
  les étapes natives restent principalement déterministes et techniques.
- Solaren exerce une vraie zone jouable de référence de 2 048 m avec 5 904 instances, quatre rôles
  visuels, collision et navigation. Sa capture Forward+ prouve que le runtime charge bien le monde
  généré ; elle ne prouve pas une qualité finale, un gain visuel aveugle ni un coût de retouche réduit.
- Terrain3D `v1.0.2-stable`, Godot Flow et GF-PCGODOT ont été exécutés avec leurs binaires réels sous
  Godot `4.7.1-stable` sur Windows. Le job Linux est configuré mais n'a pas encore produit de preuve
  GitHub, et les exports provider-free Windows/Linux restent à qualifier.
- La promotion canonique hérite désormais un vocabulaire visuel de production explicite ; elle ne crée
  toutefois pas une direction artistique originale et ne remplace pas la passe corrective humaine en
  Forward+.

Voir aussi [les dépendances](DEPENDENCIES.md), [le handoff Codex](CODEX_HANDOFF.md) et
[l'ADR-0050](../ADR/ADR-0050-governed-world-generation-orchestration.md) et
[l'ADR-0055](../ADR/ADR-0055-runtime-world-generation-vertical-slice.md), puis
[l'ADR-0058](../ADR/ADR-0058-canonical-generated-world-placement-evidence.md).
Voir aussi [l'ADR-0059](../ADR/ADR-0059-production-world-generation-art-direction.md) pour les gates
visuels de production.
