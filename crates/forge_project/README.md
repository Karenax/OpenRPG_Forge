# forge_project

Définit les documents JSON persistants et moteur-neutres de Forge :

- `forge.scene-proof` version 1 pour la preuve native/WASM de Gate 0 ;
- `forge.terrain` versions 1 et 2 pour les terrains ; la fixture Visual Valley garde sa v1 intacte,
  tandis que la v2 accepte les deltas de hauteur authorés ;
- `forge.workspace` version 2 pour les projets multi-modules et multi-zones, avec lecture/migration
  stricte de la version 1, métadonnées, point de départ et terrain source v2 ;
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

La source suit `WorkspaceDocument.project.modules[].areas[].terrain`, accessible par identifiants via
`module(id)` et `area(module_id, area_id)`. La création produit la révision 1 et un terrain v2 de 512 × 512 m, pas de 2 m,
trois matériaux logiques herbe/terre/roche et un relief procédural d'amplitude 12 m. Les identités
projet/zone du terrain doivent correspondre à leurs propriétaires ; sa révision est positive et ne
dépasse pas celle du workspace. Il n'existe ni chemin libre ni asset dans ce contrat.

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

## Modules, zones et migration v2

Le projet contient 1 à 64 modules ; un nouveau module peut être vide. Le total des zones est limité
à 256, avec des IDs de modules uniques et des IDs de zones uniques dans tout le projet. Les noms ne
servent pas d'identités. La limite JSON formaté reste de 1 Mio pour le workspace complet.

`from_json` inspecte la version puis relit les octets d'origine avec le schéma strict correspondant :
aucun champ inconnu, doublon de clé ou mélange de formes v1/v2 n'est supprimé silencieusement.
La migration v1 remplace seulement les objets uniques par des collections de longueur 1 et fixe la
version à 2. IDs, révisions, seed, terrain et spawn restent inchangés. Elle n'écrit aucun fichier.
Les consommateurs doivent comparer les hashes canoniques après migration, pas prétendre que les
octets ou le hash historique v1 sont ceux de la représentation v2.

`preview_edit(WorkspaceEdit)` est pur et partagé : ajout de module, ajout de zone extérieure,
renommage projet/module, édition du nom et du départ d'une zone. Chaque demande porte CommandId,
révision de base et paramètres typés. Le résultat validé contient la projection candidate, les hashes
avant/après et le snapshot inverse borné. La source empruntée ne change pas ; une cible inexistante,
une révision périmée/épuisée, un doublon d'identité, un dépassement de quota ou un no-op échoue.
Modifier une zone ne change pas les autres. Son terrain conserve seed/relief et synchronise nom et
révision. Appliquer l'inverse reste une responsabilité transactionnelle du consommateur et ne
constitue jamais une demande d'écrasement ou suppression disque.

`SetTerrainHeightDeltas` ajoute un patch source résolu, lié au hash exact du terrain cible en plus
de la révision workspace. Il conserve la seed, les paramètres de génération, les autres zones et
les métadonnées, puis valide intégralement le candidat (dont le budget JSON) avant de le retourner.
Le calcul des brosses appartient à `forge_world::preview_terrain_brush` ; `forge_project` ne dépend
pas du générateur ni du renderer. Le reçu de brosse conserve séparément l'intention typée et le
patch résolu ; seuls les deltas de hauteur v2 existants sont persistés dans le projet.
