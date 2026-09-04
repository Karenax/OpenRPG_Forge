# forge_project

Définit les documents JSON persistants et moteur-neutres de Forge :

- `forge.scene-proof` version 1 pour la preuve native/WASM de Gate 0 ;
- `forge.terrain` versions 1 et 2 pour les terrains ; la fixture Visual Valley garde sa v1 intacte,
  tandis que la v2 accepte les deltas de hauteur authorés ;
- `forge.workspace` version 1 pour un projet créé par l'utilisateur, contenant exactement un module
  et une zone extérieure initiale avec métadonnées, point de départ et terrain source v2 ;
- `forge.visual-valley-quality-profile` version 1 pour les presets Low/Medium/High/Ultra, le mode
  benchmark High fixe et la politique d'adaptation bornée.

`TerrainDocument` conserve les identités projet/zone, la révision, les coordonnées en mètres Y-up,
la seed, l'étendue, les paramètres logiques de height sampling et les identifiants des couches
matériau. La désérialisation refuse les champs inconnus. La validation exige des étendues, un pas et
une fréquence finis et strictement positifs, une élévation finie, une amplitude finie positive ou
nulle, 1 à 12 octaves, une lacunarité finie supérieure à 1 et une persistance dans `]0, 1]`. Un
document contient entre 1 et 32 couches ; leurs identifiants uniques contiennent 1 à 64 caractères
ASCII minuscules, chiffres ou `_`, en commençant par une lettre. Les noms non vides sont limités à
128 octets. L'étendue doit être un multiple du pas d'échantillonnage dans la tolérance flottante
documentée par le code, et chaque axe est limité à 8 192 intervalles. Le round-trip et l'empreinte JSON
canonique sont testés.

Ces données ne contiennent aucun type Bevy, handle GPU, mesh, texture ou chemin d'asset. Les caches et
liaisons renderer sont produits par les crates d'exécution.

Le profil qualité global est également moteur-neutre. Son preset High s'aligne sur les budgets déjà
authorés lorsqu'ils existent dans les documents végétation, environnement, eau et météo de Visual
Valley ; les distances LOD terrain restent des cibles documentaires. Il impose une progression
monotone des coûts entre Low et Ultra. Le benchmark est toujours High avec adaptation explicitement
désactivée. `forge_engine` consomme désormais les sélections fixes et la politique d'hystérésis
bornée dans le runtime et le viewport Studio. Son ledger distingue explicitement les champs projetés
sur des APIs Bevy, ceux appliqués seulement à la construction d'un mesh ou d'une ressource, et ceux
qui restent non applicables faute d'adapter qualifié.

## Création du premier workspace

`WorkspaceDocument::create(NewProjectCommand)` est une opération pure, partagée par les consommateurs
natifs/WASM. Le demandeur fournit les identités `CommandId`, `ProjectId`, `ModuleId` et `AreaId` :
aucune horloge, génération aléatoire implicite ou identité Bevy n'entre dans le document. Les noms
sont non blancs, sans caractères de contrôle et limités à 128 octets UTF-8. Toute seed `u64` est
acceptée. Le point de départ conserve exactement son Y explicite ; X/Z sont bornés à ±256 m,
Y à ±1 024 m et toutes les composantes doivent être finies. Il s'agit de métadonnées, pas d'une
preuve de placement effectif du joueur ou d'accessibilité du terrain.

La source imbriquée suit `WorkspaceDocument.project.module.area.terrain`, également accessible via
`module()` et `area()`. La création produit la révision 1 et un terrain v2 de 512 × 512 m, pas de 2 m,
trois matériaux logiques herbe/terre/roche et un relief procédural d'amplitude 12 m. Les identités
projet/zone du terrain doivent correspondre à leurs propriétaires ; sa révision est positive et ne
dépasse pas celle du workspace. Il n'existe ni chemin libre, ni asset, ni collection multi-zones
prétendument disponible dans cette première version.

Le reçu expose la commande, l'absence d'état/révision antérieure, la nouvelle révision, le hash
résultant et un résumé borné. L'inverse `DiscardDraft { expected_after_hash }` autorise uniquement
l'abandon d'un brouillon en mémoire dont le hash correspond encore : il ne demande jamais de
supprimer ou d'écraser un fichier. L'exécution de cet inverse, la sauvegarde, la réouverture et la
récupération après interruption restent des responsabilités explicites des consommateurs.

`validate`, `from_json`, `to_pretty_json` et `content_hash` conservent les conventions strictes du
dépôt. Le JSON est limité à 1 Mio ; la limite d'entrée est vérifiée avant désérialisation et un
document valide doit aussi tenir dans ce budget une fois formaté pour sauvegarde. Les champs
inconnus et versions non prises en charge sont refusés. Les tests de contrat couvrent création,
cohérence des identités, noms, seed, spawn, round-trip/hash, reçu et limites de format. Leur présence
ne vaut pas preuve d'une sauvegarde native ou d'un parcours Studio fonctionnel.
