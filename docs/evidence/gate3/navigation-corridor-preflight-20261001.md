# Preflight du corridor de navigation Tidewater — 1er octobre 2026

Statut : **préflight documentaire, NO-GO — aucun fournisseur sélectionné**.

Ce document décrit la plus petite preuve à produire sur une géométrie Tidewater
réelle. Il ne lance ni Cargo ni benchmark, ne modifie pas l'upstream et ne
transforme pas une capacité du laboratoire en capacité de production. Le
raccord moteur doit rester générique : aucune condition sur un slug de module,
un nom de zone ou une étiquette Tidewater ne doit entrer dans `forge_engine` ou
`forge_runtime`.

## Autorité et accès constatés

Les chemins suivants existent sur la machine et sont les références de travail
déjà suivies par l'intégration :

- source amont intacte :
  `E:/OpenRPG_Forge/.tidewater-integration/upstream/`, documentée comme
  `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c` dans
  `docs/planning/TIDEWATER_INTEGRATION.md` ;
- archive Studio canonique de revue :
  `E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-review-20260927-001/e620dd19-ead9-5913-a6d6-0f93109148c0/authoring.json` ;
- dernier paquet Windows local connu :
  `E:/OpenRPG_Forge/.tidewater-integration/OpenRPG-Forge-Aube-Tidewater-Win64-20260929-026.zip` ;
- document terrain raster produit par l'intégration :
  `terrain-v4-001.json`, empreinte documentaire
  `f385a0e90d7c74dea6b3dec4c78901d63d3f7e42840e71247f7ceb2499dd4e3b`, avec
  raster `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.
  Les preuves et limites de ce document sont dans
  `docs/evidence/tidewater/2026-09-24-raster-document.md`.

La source amont donne une géométrie et des repères réels, mais aucune preuve
existante ne fournit encore un fixture de navigation Tidewater borné, hashé et
accepté par le laboratoire. La preuve de collisions raster (`256` heightfields
Rapier) ne qualifie pas un maillage de navigation ni un geste Studio ; voir
`docs/evidence/tidewater/2026-09-27-sampled-terrain-collisions.md`.

Repères canoniques observés dans la source, en mètres Forge `[x, y, z]` ou
`[x, z]` lorsque `y` doit être échantillonné :

- `src/world/WorldLayout.js` : départ `[53.6, 0, -77]`, spawn `[18, 0, -60]`,
  centre du village `[40, 0, -118]`, rayon du village `95` ;
- `src/world/terrain/IslandShape.js` : chemin plage → village de
  `[21, -56]` à `[35.5, -108.5]`, puis chemin de randonnée vers le massif de
  `[42, -116.5]` à `[4, -322]` ;
- `src/world/Village.js` : maisons et huttes réelles, par exemple la maison H
  `[57.5, -128.5]`, les huttes S1/S2/S3 et le boathouse `[92.5, -57.5]`.
  `src/world/village/Buildings.js` construit leurs surfaces et leurs boîtes de
  collision ; `src/world/Colliders.js` expose `addBox`, `addCylinder` et
  `raycast`.

Ces valeurs sont des candidats d'ancrage pour une capture autorisée, pas une
validation d'itinéraire. Le mot « forêt » ne doit pas être inventé à partir du
seul nom d'une étape : `src/world/Vegetation.js` décrit une canopée, des lisières
et des bosquets, mais le corridor doit conserver l'empreinte de la source
végétation/chemin qui justifie l'étiquette. `y` doit toujours venir de la
surface source (`TerrainData.heightAt` ou `SampledTerrainPlan::height_at`) ; le
`y = 0` des repères de layout n'est pas une hauteur de collision.

Le test `crates/forge_gameplay/tests/first_loop.rs` est un contrat de boucle
logique. Ses positions `(0,0,0)`, `(10,0,0)` et `(20,0,0)` sont fictives et ne
doivent pas alimenter ce fixture.

## Source de géométrie de collision à exporter

Le fixture doit être dérivé, sans écrire dans l'upstream, d'une révision exacte
du document ouvert et des blobs autorisés. Il doit conserver dans son en-tête :

`source_revision`, `document_hash`, `source_hash` du raster si présent, repère
Forge, boîte de corridor dilatée, cellules/chunks inclus, points de départ et
d'arrivée, IDs des colliders statiques, politique eau, profils d'agents,
empreinte de la géométrie dérivée et version de l'exporteur. Aucune coordonnée
ne doit être recalée autour de l'origine du fixture.

### Terrain raster Tidewater

La voie canonique existante est :

1. lire le document actif par l'autorité d'authoring ;
2. assembler les réponses de `TerrainSourceAssembly` dans
   `crates/forge_project/src/authoring/terrain_source.rs` ;
3. obtenir les blocs par `AuthoringHost::read_terrain_source` dans
   `crates/forge_project/src/authoring/host/terrain_source.rs` ;
4. refuser toute longueur, séquence ou SHA-256 incohérente ;
5. construire `SampledTerrainPlan::from_document` dans
   `crates/forge_world/src/sampled_terrain/plan.rs` ;
6. produire chaque portion par `SampledTerrainPlan::build_patch` dans
   `crates/forge_world/src/sampled_terrain/mesh.rs`.

`build_patch` partage les positions et le découpage de triangles entre rendu et
collision, borne chaque appel à 129² sommets, et transporte le
`document_hash`. C'est la petite surface réutilisable pour un corridor raster.
Elle ne crée pas une nouvelle source persistante et ne justifie pas un fallback
procédural lorsque le blob manque.

### Terrain procédural du laboratoire

Pour le contrat Visual Valley seulement, la voie existante est
`TerrainGenerationPlan::from_document` puis `generate` dans
`crates/forge_world/src/terrain_generation`. Le résultat
`GeneratedTerrain.chunks[*].collider` est le payload de collision canonique :
positions absolues, ordre de sommets
`RowMajorZThenX`, triangles
`CellRowMajorZThenXCounterClockwiseFromAbove`, IDs et coordonnées de chunks.
`GeneratedTerrain::content_hash` doit être conservé avec
`source_document_hash`. `TerrainGenerationPlan` refuse volontairement le
terrain raster Tidewater hors de son contrat fixe 512 m / 2 m ; il ne faut pas
forcer ce chemin pour la carte importée.

### Sols et bâtiments du corridor

Le terrain seul ne suffit pas au trajet. Inclure les surfaces marchables et les
colliders statiques autorisés de la même révision : pads aplatis, chemins,
escaliers, planchers, pont/quai et murs. Pour l'upstream, les fonctions
`TerrainData.heightAt`, `TerrainData.flatten`, `Village`/`buildHouse` et les
appels `Colliders.addBox`/`addCylinder` montrent les données à exporter ; elles
ne doivent pas être exécutées comme une mutation de la source pendant le
preflight. Une primitive graphique ou une capture ne remplace pas une collision
identifiée et hashée.

L'eau doit être retirée ou marquée non marchable à partir d'une règle source
conservée dans le fixture (`coastDistance`, masque d'eau auteur ou audit déjà
validé). Une décision fondée sur une image seule est un NO-GO. Les colliders de
terrain Rapier sont une preuve physique séparée, pas un navmesh.

## Découpe conservatrice d'un corridor réel

La première exécution doit enregistrer les points réels depuis la source, par
exemple départ `WorldLayout.start`, place du village, une entrée de maison et
un point du chemin de randonnée ; elle ne doit pas prendre un endpoint de
`first_loop`. Une sélection « village → lisière/chemin → intérieur » est
acceptable comme scénario de mesure si la source prouve chacun des trois lieux.
Le gardien, l'objet à récupérer et les deux issues combat/chute de charge sont
des objets de gameplay superposés au corridor ; ils ne doivent pas changer la
géométrie terrain canonique pendant le benchmark.

Pour chaque patch/chunk, exporter tous les triangles dont la boîte englobante
intersecte l'enveloppe du corridor, avec une marge au moins égale au rayon de
l'agent, à la hauteur de marche et au bord de tuile. Le crop actuel de
`tools/forge_navigation_benchmark/src/geometry.rs::exterior_bridge_mesh` ne
garde qu'un triangle dont le centroïde tombe dans la fenêtre ; c'est utile pour
le diagnostic Visual Valley mais peut couper un triangle au bord. Le preflight
Tidewater doit donc utiliser une intersection triangle/AABB conservatrice, sans
retriangulation.

Avant tout appel fournisseur, rejeter : sommets non finis, indices hors plage,
triangles dégénérés, couture non partageable, hauteur hors document, matériau
ou collider sans provenance, et endpoint qui ne se projette pas sur une surface
exportée. Vérifier les coutures par position canonique et par hash des bandes de
bord ; les deux côtés d'une tuile doivent avoir les mêmes coordonnées, normales
et indices attendus. Le hash du mesh dérivé est calculé après cette validation.

## Benchmark minimal à réaliser plus tard

Ces étapes sont une spécification de preuve ; elles n'ont pas été exécutées
dans ce preflight.

1. **Îlot de référence.** Générer trois fois le mesh complet du corridor réel
   avec mêmes paramètres Rerecast, mêmes profils d'agents et mêmes endpoints.
   Comparer hashes de sortie, polygones, détail et chemin départ → destination.
   Rejouer avec l'eau et les colliders statiques présents ; la route doit être
   trouvée uniquement si la géométrie canonique l'autorise.
2. **Tuiles/îlots réels.** Partitionner le même mesh selon les chunks/paches
   source avec bord commun et marge explicite. Construire les tuiles, les
   enregistrer comme plusieurs îlots Landmass ou par une API de tuiles du
   fournisseur, puis comparer le chemin traversant les coutures au chemin de
   l'îlot complet. Vérifier qu'un passage absent reste absent.
3. **Reconstruction locale.** Dans une copie de fixture, modifier un seul
   chunk terrain ou collider autorisé. Reconstruire seulement sa tuile et
   prouver que les hashes des tuiles intactes et le chemin hors tuile restent
   inchangés. `TerrainGenerationPlan::plan_dirty_region`,
   `TerrainPartialUpdate::apply_partial_update` et les types de
   `terrain_generation/partial.rs` prouvent seulement la mise à jour du cache
   terrain ; ils ne prouvent pas une reconstruction navmesh fournisseur.
4. **Obstacle dynamique.** Ajouter, déplacer puis retirer le même obstacle
   déterministe dans une tuile. Le chemin doit passer / être bloqué / repasser,
   avec une borne de temps et une preuve que les tuiles non touchées ne sont
   pas régénérées. Une régénération complète doit être signalée comme écart,
   pas comme reconstruction partielle.
5. **Liaison inter-tuiles et liens.** Mesurer un vrai chemin par couture, avec
   identité de tuile/îlot et, si nécessaire, un lien hors maillage explicitement
   autorisé. Le lien doit être refusé avant ajout et accepté après ajout, sur la
   géométrie Tidewater, avec coût et direction consignés.
6. **Profils et navigateur.** Rejouer avec au moins deux rayons d'agent, puis
   exécuter l'API réelle dans le navigateur `wasm32-unknown-unknown`, pas une
   simple compilation. Les hashes d'entrée et endpoints doivent être les mêmes
   que pour le natif ; rapporter les limites de temps, mémoire et disponibilité
   de l'évitement.

## Ce que prouvent les sources actuelles

Le laboratoire `tools/forge_navigation_benchmark` appelle
`provider::generate_navmesh` sur un `TriMesh` et convertit la sortie en
Landmass. Le README et `src/main.rs` consignent déjà les capacités suivantes :
génération complète, profils, portes, coûts, liens, évitement local et debug
passent dans leurs probes ; génération native par tuiles, reconstruction
partielle et carving dynamique sont explicitement `false`. La conversion
globale échoue encore sur le winding du polygone 2 819 ; la normalisation dans
`navigation.rs` est diagnostique et reste `winding_normalization_accepted=false`.
La pile est donc `blocked_pending_contract_and_governance_gaps`, avec en plus
le backend Rapier non maintenu dans le candidat et l'avis
`RUSTSEC-2025-0141` sur `bincode 2.0.1`.

Il n'existe pas de preuve de liaison de tuiles/îlots du fournisseur actuel :
`navigation::off_mesh_link_probe` fabrique deux rectangles disjoints, appelle
`archipelago_with_mesh`, puis ajoute un seul `AnimationLink`. Cette fonction
crée un unique `Island::new` dans un `Archipelago`; elle ne construit ni deux
îlots issus de chunks Tidewater, ni bord partagé, ni tile cache. Les tests
`interior_mesh`, `disconnected_platforms_mesh`, `area_cost_probe` et
`local_avoidance_probe` restent des fixtures synthétiques.

## Oracles, conditions d'arrêt et verdict

Le rapport du corridor ne peut passer que si toutes les preuves suivantes sont
présentes :

- document/raster et colliders statiques authentifiés par hashes ;
- endpoints et hauteurs tirés de la carte réelle et retrouvés sur le mesh ;
- trois générations identiques, avec géométrie finie, coutures sans trou et
  route réelle village → extérieur → intérieur ;
- équivalence mesurée îlot complet / tuiles, franchissement explicite des
  coutures et liens identifiés ;
- reconstruction limitée à la tuile modifiée ;
- obstacle dynamique bloquant puis libérant le chemin ;
- profils d'agents, coûts, portes et eau vérifiés sur ces données ;
- native **et** exécution navigateur réelles, licences/advisories examinées ;
- décision d'ADR et nouvelle exécution reproductible après correction.

Un échec d'une seule condition est un **NO-GO**. En particulier, un fallback
de winding, une régénération complète étiquetée « partial », un lien prouvé
seulement sur rectangles synthétiques, ou une compilation WASM sans navigateur
ne permet pas la sélection. Le verdict courant reste donc
`blocked_pending_contract_and_governance_gaps` ; ce document ne proclame pas
Rerecast, Landmass ou une alternative comme fournisseur de production.
