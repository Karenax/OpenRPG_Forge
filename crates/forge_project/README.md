# forge_project

Définit les documents JSON persistants et moteur-neutres de Forge :

- `forge.scene-proof` version 1 pour la preuve native/WASM de Gate 0 ;
- `forge.terrain` versions 1 à 3 pour les terrains ; la fixture Visual Valley garde sa v1 intacte,
  la v2 accepte les deltas de hauteur authorés et la v3 ajoute les poids de matériaux authorés ;
- `forge.path` version 1 pour les chemins génériques persistants, sans profil de route ni rivière ;
- `forge.road` version 1 pour les profils routiers affectés à des chemins existants ;
- `forge.river` version 1 pour les profils de chenal affectés à des chemins existants ;
- `forge.workspace` version 6 pour les projets multi-modules et multi-zones, avec lecture/migration
  stricte des versions 1 à 5, métadonnées, point de départ et documents source par zone ;
- `forge.visual-valley-quality-profile` version 1 pour les presets Low/Medium/High/Ultra, le mode
  benchmark High fixe et la politique d'adaptation bornée.

`TerrainDocument` conserve les identités projet/zone, la révision, les coordonnées en mètres Y-up,
la seed, l'étendue, les paramètres logiques de height sampling, les identifiants des couches
matériau, les deltas de hauteur et les poids sparse herbe/terre/roche. La désérialisation refuse les
champs inconnus. La validation exige des étendues, un pas et
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
`module(id)` et `area(module_id, area_id)`. La création produit la révision 1 et un terrain v3 de
512 × 512 m, pas de 2 m,
trois matériaux logiques herbe/terre/roche et un relief procédural d'amplitude 12 m. Les identités
projet/zone du terrain doivent correspondre à leurs propriétaires ; sa révision est positive et ne
dépasse pas celle du workspace. Chaque nouvelle zone possède aussi des `PathDocument`,
`RoadDocument` et `RiverDocument` v1 vides. Aucun asset ni chemin de fichier libre n'entre dans ce
contrat.

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

## Modules, zones et migration v6

Le projet contient 1 à 64 modules ; un nouveau module peut être vide. Le total des zones est limité
à 256, avec des IDs de modules uniques et des IDs de zones uniques dans tout le projet. Les noms ne
servent pas d'identités. La limite JSON formaté reste de 1 Mio pour le workspace complet.

`from_json` inspecte la version puis relit les octets d'origine avec le schéma strict correspondant :
aucun champ inconnu, doublon de clé ou mélange de formes v1/v2/v3/v4/v5/v6 n'est supprimé silencieusement.
Les migrations v1/v2 reconstruisent leurs collections et documents absents ; la migration v3 ajoute
un `PathDocument` vide, la migration v4 ajoute un `RoadDocument` vide et la migration v5 ajoute
uniquement un `RiverDocument` vide à chaque zone. Les
migrations embarquées du terrain et de la végétation restent appliquées. IDs, révisions, seed,
hauteurs, matériaux, végétation, chemins, routes et spawn restent inchangés. La lecture n'écrit aucun fichier.
Les consommateurs doivent comparer les hashes canoniques après migration, pas prétendre que les
octets ou le hash historique sont ceux de la représentation v6.

`preview_edit(WorkspaceEdit)` est pur et partagé : ajout de module, ajout de zone extérieure,
renommage projet/module, édition du nom et du départ d'une zone. Chaque demande porte CommandId,
révision de base et paramètres typés. Le résultat validé contient la projection candidate, les hashes
avant/après et le snapshot inverse borné. La source empruntée ne change pas ; une cible inexistante,
une révision périmée/épuisée, un doublon d'identité, un dépassement de quota ou un no-op échoue.
Modifier une zone ne change pas les autres. Son terrain conserve seed/relief et synchronise nom et
révision. Appliquer l'inverse reste une responsabilité transactionnelle du consommateur et ne
constitue jamais une demande d'écrasement ou suppression disque.

`SetTerrainHeightDeltas` et `SetTerrainMaterialWeights` ajoutent des patches source résolus, liés au
hash exact du terrain cible en plus de la révision workspace. Ils conservent la seed, les paramètres
de génération, les autres zones et
les métadonnées, puis valide intégralement le candidat (dont le budget JSON) avant de le retourner.
Le calcul des brosses appartient à `forge_world::preview_terrain_brush` ; `forge_project` ne dépend
pas du générateur ni du renderer. Le reçu de brosse conserve séparément l'intention typée et le
patch résolu ; seuls les deltas de hauteur et les poids matériaux sparse v3 sont persistés dans le
projet.

## Chemins persistants

`PathDocument` conserve une collection canonique de `AuthoredPath` triés par `PathId`. Un chemin
porte uniquement son nom, son caractère ouvert ou fermé, l'interpolation
`centripetal_catmull_rom_v1` et ses points `Vec3` ordonnés en mètres Y-up. Une éventuelle route est
référencée séparément par `PathId` : le chemin générique ne porte ni profil, ni matériau, ni maillage.

Le document accepte au plus 64 chemins, 64 points par chemin et 2 048 points au total. Un chemin
ouvert exige deux points et un chemin fermé trois. X/Z appartiennent à `[-256, 256[`, Y à
`[-10 000, 10 000]`, deux points consécutifs restent séparés horizontalement d'au moins 0,5 m et la
fermeture suit la même règle. La longueur du polygone de contrôle est limitée à 8 192 m par chemin et
32 768 m par document ; le JSON du document est limité à 512 Kio.

`UpsertPath` lie la mutation à la révision du workspace et au hash exact du `PathDocument`. Un ID
absent crée le chemin, un ID présent le remplace entièrement, puis la collection est retriée. Un
succès avance une seule fois les révisions workspace/chemins et conserve le snapshot inverse commun.
Cette commande ne peut pas supprimer implicitement un autre chemin.

## Profils routiers persistants

`RoadDocument` conserve au plus 64 `AuthoredRoad`, strictement triés et uniques par `PathId`. Un
`PathId` est l'identité de la route : un chemin ne reçoit donc qu'un profil routier. Le workspace
refuse une référence absente ou un chemin fermé ; la version 1 cible uniquement les chemins ouverts
créés par le Studio. La longueur cumulée des polygones de contrôle affectés à une route est limitée à
8 192 m et le JSON routier à 64 Kio.

Le profil `terrain_draped_crown_v1` contient une largeur finie de 1 à 16 m et une hauteur de couronne
de 0 à 0,30 m, sans dépasser 5 % de la largeur. La seule surface v1 est
`compacted_earth_v1`. Ces valeurs sont des sources moteur-neutres ; échantillons terrain, mesh,
normales, UV, poids matériau et handles renderer restent dérivés hors de `forge_project`.

`UpsertRoad` porte la révision du workspace ainsi que les hashes exacts des documents chemin et route.
Il ajoute ou remplace seulement l'affectation ciblée, conserve l'ordre canonique, avance une fois les
révisions workspace/route et fournit le snapshot inverse commun. No-op, hash périmé, profil invalide,
chemin absent/fermé et dépassement de limite échouent avant d'exposer le candidat. R2-022 ne fournit
aucune suppression implicite, opération terrain, rivière, carving ou intersection.

## Chenaux de rivière persistants

`RiverDocument` conserve au plus 16 `AuthoredRiver`, strictement triés et uniques par `PathId`. Une
rivière référence un chemin existant et ouvert qui ne peut pas être simultanément affecté à une
route. La longueur cumulée des polylignes de contrôle horizontales affectées aux rivières est limitée
à 8 192 m ; il ne s'agit pas d'une mesure de longueur d'arc. Le JSON rivière est limité à 64 Kio.

Le profil `terrain_carved_channel_v1` porte une largeur de surface finie de 2 à 32 m, une profondeur
de lit de 0,15 à 8 m et une largeur de fondu de berge de 1 à 16 m. La seule surface v1 est
`forge_water_v1`. Ces valeurs restent des sources moteur-neutres : carving terrain, échantillons,
mesh d'eau, matériau, collisions et caches sont dérivés hors de `forge_project`. Aucun delta de
terrain n'est persisté par cette commande.

`UpsertRiver` lie chaque mutation aux hashes exacts des documents terrain, chemins, routes et
rivières. Il ajoute ou remplace uniquement l'affectation ciblée, conserve l'ordre canonique, avance
une fois les révisions workspace/rivière et fournit le snapshot inverse commun. No-op, hash périmé,
profil invalide, chemin absent/fermé, conflit route/rivière et dépassement des plafonds échouent avant
d'exposer le candidat.
