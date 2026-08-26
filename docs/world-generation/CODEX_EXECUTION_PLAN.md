# Mission Codex — qualifier puis industrialiser la génération de mondes

## Mandat

Tu travailles dans `Karenax/OpenRPG_Forge` sur la branche :

```text
feat/worldgen-quality-qualification
```

Cette branche part du commit de fusion WorldGeneration présent dans `main` :

```text
b0a519b1566c4c5dded9db7fe38c0cfc6d99399d
```

L'objectif n'est pas d'ajouter le plus grand nombre possible d'addons Godot. L'objectif est de
produire des mondes RPG plus beaux, plus cohérents, plus rapides à créer et toujours gouvernés par
les contrats OpenRPG Forge.

Tu dois continuer le travail jusqu'à obtenir des preuves mesurables. Une intégration communautaire
n'est acceptée que si elle améliore réellement la carte de référence définie dans
`QUALITY_BENCHMARK.md`.

## État réel à retenir

- L'orchestration MCP, les recettes, les plans déterministes, les jobs persistants, les adaptateurs et
  le fallback natif sont fusionnés dans `main`.
- Terrain3D est le backend terrain retenu.
- L'enum canonique `PcgGodot` existe déjà dans les formats sérialisés. Ne le renomme pas brutalement :
  une migration versionnée serait nécessaire.
- Le fournisseur réellement épinglé derrière ce backend est actuellement `godot-flow`, pas
  `GF-PCGODOT`.
- La branche de qualification ajoute des `providerKey` explicites afin de pouvoir comparer plusieurs
  implémentations sans casser les recettes existantes.
- Les graphes artistiques `biomes.tres`, `roads.tres`, `settlements.tres` et `scatter.tres` attendus
  par le registre ne sont pas encore livrés.
- Le fallback Godot natif garantit les contrats et la CI, mais il n'est pas un générateur visuel de
  production pour les villes, routes, biomes et scatters.
- La promotion atomique d'une scène générée validée vers les données canoniques du module reste à
  construire.

## Documents à lire avant toute modification

Dans cet ordre :

1. `AGENTS.md`
2. `PROJECT_CONTEXT.md`
3. `docs/ADR/ADR-0050-governed-world-generation-orchestration.md`
4. `docs/world-generation/README.md`
5. `docs/world-generation/CODEX_HANDOFF.md`
6. `docs/world-generation/BACKEND_QUALIFICATION.md`
7. `docs/world-generation/QUALITY_BENCHMARK.md`
8. `third_party/world-generation.dependencies.json`

Le dépôt et ses tests font foi. Si ce document contredit le code actuel, établis la cause, mets le
document à jour et conserve la compatibilité des formats persistés.

## Démarrage Git obligatoire

```powershell
git status
git remote -v
git fetch origin --prune
git switch feat/worldgen-quality-qualification
git pull --ff-only origin feat/worldgen-quality-qualification
git log --oneline --decorate --graph -20
```

Ne détruis aucune modification locale. Si l'arbre n'est pas propre, identifie et préserve le travail
avant de continuer.

# Règles d'architecture non négociables

1. `Forge.Domain` et `Forge.Application` ne référencent jamais Godot ni un addon communautaire.
2. Le MCP reste l'orchestrateur. Il manipule des recettes et commandes structurées, jamais du
   GDScript libre fourni par un modèle.
3. Une seule technologie possède chaque responsabilité de production : terrain, graphe PCG,
   routes, topologie de donjon, eau ou végétation.
4. Terrain3D reste l'unique propriétaire du terrain extérieur final.
5. Les fournisseurs concrets sont identifiés par `providerKey`; les backends canoniques restent
   stables tant qu'une migration de format n'est pas explicitement conçue.
6. Tout résultat est déterministe pour une recette, une seed, une version de fournisseur et un
   catalogue d'assets donnés.
7. Toute mutation suit preview, diff, permission, révisions, plan, application et audit.
8. Une régénération partielle ne détruit jamais les conteneurs ou nœuds protégés manuellement.
9. Le résultat d'un worker reste dérivé jusqu'à une commande explicite de promotion canonique.
10. Aucun addon n'est ajouté à la pile de production sur la seule base de ses captures ou de sa liste
    de fonctionnalités.

# Décisions techniques déjà prises

## Confirmé

- `Terrain3D` : backend central du terrain extérieur.
- OpenRPG MCP + recettes canoniques : source de vérité et orchestration.
- Bake hors runtime : les zones majeures sont générées, contrôlées, sauvegardées puis chargées par le
  jeu. La génération complète n'est pas relancée pendant le déplacement du joueur.

## À qualifier par comparaison

- `godot-flow` : fournisseur Graph PCG actuellement épinglé.
- `gf-pcgodot` : concurrent possible, jamais installé en parallèle en production.
- `ProtonScatter` : concurrent limité au scatter de végétation et de petits props.
- `Godot Road Generator` : prototype spécialisé pour routes structurées et intersections.

## À reprendre comme concepts, pas comme dépendances immédiates

- `SimpleDungeons` : grille, pièces préfabriquées, connecteurs, A* et étages pour un futur
  `OpenRPG DungeonAssembler`.
- `SimpleXTerrain` : érosion, falaises, terrasses, plages et splatmaps pour le compilateur de terrain.
- `Procedural Forest Demo` : vent, billboards, variations et atmosphère, sans importer son projet ou
  ses assets.
- `Foliage3D` : promotion d'une instance lointaine en véritable entité interactive proche du joueur.

## Écarté du cœur actuel

- Gaea : doublon et maturité insuffisante pour le besoin.
- Waterways.NET : ne pas intégrer avant que l'hydrologie, le lit et les berges OpenRPG soient fiables.
- Voxel Tools : réservé plus tard à des scènes séparées de grotte ou de terrain destructible.
- Roommate : réservé plus tard aux intérieurs secondaires, jamais aux lieux héroïques.

# Lots de travail ordonnés

## Lot 0 — identité des fournisseurs et compatibilité

État : commencé sur cette branche.

Travail attendu :

- conserver l'enum sérialisé `PcgGodot` ;
- exposer l'identité concrète du fournisseur via `providerKey` ;
- distinguer au minimum `terrain3d`, `godot-flow`, `gf-pcgodot` et `openrpg-native` ;
- répercuter cette identité dans les ressources MCP, diagnostics, audits et rapports de qualification
  lorsque cela est pertinent ;
- détecter et signaler plusieurs fournisseurs Graph PCG simultanés ;
- ajouter les tests de compatibilité et de sélection déterministe.

Critère de fin : les formats existants se relisent sans migration, tandis que les outils et rapports
peuvent nommer le fournisseur réellement utilisé.

## Lot 1 — qualification réelle des addons sous Godot 4.7.1

Ajouter un projet Godot jetable ou une fixture dédiée et un job CI
`world-generation-addon-integration` qui :

1. installe les versions épinglées avec vérification d'empreinte ;
2. charge Terrain3D et `godot-flow` sous Godot 4.7.1 ;
3. exécute un vrai graphe minimal, pas un simple test de présence de fichier ;
4. produit un terrain Terrain3D et un ensemble d'instances PCG ;
5. sauvegarde la scène dérivée ;
6. ferme puis rouvre la scène ;
7. vérifie l'empreinte et le nombre d'objets ;
8. vérifie les collisions et une navigation minimale ;
9. produit un rapport JSON et des captures ;
10. exécute au moins un smoke Windows et un smoke Linux lorsque les binaires des addons le permettent.

Ne marque pas ce lot terminé si seul le fallback natif a fonctionné.

## Lot 2 — carte de référence et harness de mesure

Implémenter la carte fixe décrite dans `QUALITY_BENCHMARK.md` :

- 2 km × 2 km ;
- vallée tempérée ;
- rivière centrale ;
- forêt à l'ouest ;
- village de 25 maisons ;
- château au nord ;
- mine au pied de la montagne ;
- réseau routier village, château et mine ;
- donjon de 20 à 30 salles ;
- seed, catalogue d'assets et réglages identiques pour chaque essai.

Le harness doit produire :

- manifeste des fournisseurs et versions ;
- fingerprints recette, plan, catalogue et scène ;
- temps de génération par étape ;
- allocations et tailles de scène ;
- nombre de nœuds, instances, collisions et triangles ;
- navigation et reachability ;
- violations géométriques ;
- captures normalisées ;
- temps de correction manuelle mesuré ;
- rapport final comparable entre fournisseurs.

## Lot 3 — choix du fournisseur Graph PCG

Comparer d'abord `godot-flow` et `gf-pcgodot` avec exactement la même carte, la même seed et les mêmes
assets. Tester ProtonScatter uniquement pour la couche scatter si le besoin persiste.

Chaque candidat doit être isolé derrière l'adaptateur existant. Aucun type spécifique au fournisseur
ne doit remonter dans Domain, Application ou MCP.

À la fin :

- choisir un fournisseur Graph PCG de production ;
- conserver un fallback natif de contrat ;
- supprimer ou laisser expérimental le candidat perdant ;
- documenter la décision dans une nouvelle ADR ;
- épingler version, licence, empreinte et plateformes ;
- ne jamais charger deux propriétaires du même scatter en production.

## Lot 4 — premier monde visuellement crédible

Avant d'ajouter d'autres addons, construire :

- un registre sémantique d'assets avec style, catégorie, footprint, sockets, LOD, collisions,
  matériaux, provenance et qualité ;
- un kit médiéval tempéré cohérent ;
- des matériaux Terrain3D PBR, détilement, humidité et falaises ;
- un compilateur de terrain avec bruit multi-échelle, érosion, hydrologie, masques et splatmaps ;
- un graphe de forêt de production ;
- un graphe de village de production ;
- un graphe de décoration routière ;
- éclairage, atmosphère, vent et variations ;
- budgets de répétition ;
- captures automatiques et validation visuelle.

Les lieux héroïques doivent être générés comme base, corrigés, validés puis verrouillés.

## Lot 5 — promotion canonique

Concevoir une commande explicite de promotion qui :

- exige un job réussi et un rapport de validation accepté ;
- vérifie fingerprints et révisions ;
- importe atomiquement le résultat dans les données canoniques ou une scène canonique référencée ;
- conserve provenance, fournisseur, version, seed et catalogue d'assets ;
- protège les retouches manuelles ;
- fournit preview, diff, inverse et audit ;
- permet sauvegarde, fermeture, réouverture et reconstruction déterministe.

Une scène dérivée ne doit jamais devenir canonique par simple effet secondaire du worker.

## Lot 6 — prototype Godot Road Generator

Créer une branche ou un feature flag expérimental. Le planificateur OpenRPG garde le graphe logique des
routes ; l'addon ne reçoit que des splines et profils compilés.

Comparer au backend Graph PCG actuel :

- raccords et intersections ;
- UV et matériaux ;
- adaptation Terrain3D ;
- collisions ;
- voies de navigation ;
- temps de génération ;
- temps de correction manuelle ;
- performances ;
- stabilité de l'API script.

Adopter uniquement si les gates de `QUALITY_BENCHMARK.md` sont franchies.

## Lot 7 — OpenRPG DungeonAssembler

Ne branche pas SimpleDungeons comme boîte noire. Construis un composant OpenRPG interne inspiré de ses
concepts :

- pièces préfabriquées et connecteurs typés ;
- grille spatiale et contrôle des chevauchements ;
- connexion A* ;
- plusieurs étages ;
- seed déterministe ;
- chemin critique ;
- boucles facultatives ;
- salles secrètes ;
- clés et portes ;
- salle de quête et boss obligatoires ;
- budgets de rencontres ;
- navigation réelle ;
- streaming par groupes de salles.

Le Graph PCG retenu ne fait ensuite que l'habillage : props, débris, torches, variations et loot.

# Tests et commandes minimales

Commence par les tests ciblés :

```powershell
dotnet restore OpenRPGForge.sln --locked-mode
dotnet test tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WorldGeneration
```

Puis applique les gates du dépôt :

```powershell
dotnet format OpenRPGForge.sln --verify-no-changes --no-restore
dotnet build OpenRPGForge.sln -c Release --no-restore
dotnet test OpenRPGForge.sln -c Release --no-build
dotnet run --project tools/Forge.ArchitectureGraph -c Release --no-build -- check --full
./scripts/Test-ProjectStatusConsistency.ps1
./scripts/Test-MaintainabilityBudgets.ps1
```

Inspecte les scripts et `AGENTS.md` avant d'ajouter ou retirer une commande de vérification.

# Interdictions

- Ne pas intégrer simultanément Godot Flow et GF-PCGODOT comme propriétaires du même graphe.
- Ne pas ajouter ProtonScatter en plus du scatter existant sans benchmark comparatif.
- Ne pas remplacer Terrain3D par un autre terrain.
- Ne pas importer les assets du Procedural Forest Demo sans audit individuel.
- Ne pas qualifier un addon uniquement parce que son projet s'ouvre.
- Ne pas appeler une ancre ou un marker natif « monde final ».
- Ne pas contourner preview, permissions, révisions ou audit.
- Ne pas écrire des chemins absolus de poste dans le dépôt.
- Ne pas commiter les binaires tiers quand l'installateur épinglé suffit.
- Ne pas annoncer qu'une amélioration visuelle est acquise sans captures et mesures comparables.

# Définition globale de terminé

Le programme est terminé lorsque :

- le fournisseur Graph PCG est choisi par benchmark ;
- Terrain3D et ce fournisseur passent une qualification réelle sous Godot 4.7.1 ;
- les graphes de production existent ;
- la carte de référence atteint les gates fonctionnelles, visuelles et de performance ;
- le résultat peut être promu, sauvegardé, rouvert et reconstruit ;
- les retouches verrouillées survivent aux régénérations ;
- les lieux héroïques sont validés et verrouillés ;
- les exports cibles passent ;
- licences et provenances sont complètes ;
- une ADR enregistre le choix final ;
- `README.md`, `PROJECT_CONTEXT.md`, `STATUS.md`, `TASKS_MVP.md`, `CHANGELOG.md` et la documentation
  WorldGeneration reflètent l'état réel.

# Rapport exigé de Codex

À la fin de chaque lot, fournir :

- SHA de départ et SHA final ;
- fichiers modifiés ;
- tests exécutés et résultats ;
- artefacts et captures ;
- mesures avant/après ;
- régressions ou limites ;
- décision d'adoption ou de rejet ;
- prochaine étape exacte ;
- état de la PR et des checks GitHub.

Continue le travail réel après avoir lu ce document. Ne te limite pas à produire un nouveau plan.
