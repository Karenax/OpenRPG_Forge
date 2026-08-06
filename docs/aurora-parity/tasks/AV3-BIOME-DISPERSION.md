# AV3 — Brosses de biome et dispersion d’assets

## Périmètre

Cette tranche relie les blueprints d’assets gouvernés au terrain canonique. Elle réutilise la brosse
`PaintMaterial`, les `BiomeLayerDefinition`, `GodotBiomeScatterAdapter` et `GodotAssetModelLibrary` ;
aucune scène générée, ressource de biome parallèle ou nœud moteur ne devient source de vérité.

## Résultat livré

- sélecteurs **Asset prairie** et **Asset roche** alimentés par les blueprints ProjectContent
  installés depuis le Content Browser ;
- conservation explicite d’une référence de recette momentanément manquante au lieu de l’effacer à
  la sauvegarde ;
- brosse renommée **Matériau / biome** : une cellule peinte sélectionne la recette liée à son
  matériau, une cellule automatique conserve les règles altitude/pente ;
- `BiomeScatterPlanBuilder` déterministe et indépendant du rendu, avec limites de densité, distance,
  altitude, pente, trous, exclusions de routes/rivières et masque de matériau ;
- plan partagé entre Creator et runtime, rendu par `MultiMesh` et résolu depuis les mêmes manifestes
  SHA-256 ;
- conversion glTF officielle des `ImporterMeshInstance3D` en maillages runtime avant mise en cache,
  afin qu’un asset chargé soit réellement visible et utilisable par la dispersion.

## Preuves

- deux tests EngineAdapter couvrent la stabilité du plan et la sélection de recette par peinture ;
- les 11 tests EngineAdapter passent ;
- `OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_PASS` prouve que les ressources de recette survivent à
  sauvegarde, peinture, undo/redo et round-trip ;
- `OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_PASS` charge le GLB autonome gouverné, vérifie son hash et
  prouve dans le viewport un modèle manuel, une couche de dispersion et deux instances.

## Limites explicites

- la recette de cette tranche choisit un modèle par biome ; les mélanges pondérés multi-modèles et
  les brosses de densité spécialisées restent des améliorations, pas une nouvelle source de vérité ;
- les mutations IA/MCP d’assets doivent encore être raccordées aux commandes et contrôles de licence ;
- l’habillage audio, les animations et les VFX de Forgotten Forest relèvent des finitions AV5.

## Critère de passage

Le lot est fermé lorsque compilation, tests ciblés, smoke Toolset avec maillage réellement converti,
graphe d’architecture et gate global passent sur le même état Git, puis que le commit est publié sur
`main`.
