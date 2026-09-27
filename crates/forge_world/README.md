# forge_world

`VegetationGenerationPlan::from_documents_with_paths` adds validated, derived
path clearances before spacing acceptance, without consuming the 32 authored
exclusions or rewriting masks. Paths retain their separate 64-item quota and
128-point bound. `path_vegetation::prepare` exposes the shared projection identity;
no-path generation keeps the existing identity. The older merged `project`
adapter is retained for reference tests, not current Studio production calls.

`WorldSketchDocument::forest_scatter` opts into region-driven tree generation.
The compiler combines Forest masks by maximum and subtracts Buildable masks,
then projects the result into a renderer-independent `VegetationDensityMask`.
The generator bilinearly samples this field before the existing material, slope,
spacing and exclusion checks. Layer IDs, seeds and candidate entropy are unchanged;
changed population settings intentionally change accepted generated placements.
Authored kit placements and player dimensions are not mutated. Omitted settings
retain the historical recipe. This does not provide Studio authoring or undo yet.

The current valley opts into 0.85 acceptance, eight attempts per cell, 4 m minimum
spacing and scales 1.6–3.15 (9.04–17.80 m for its existing 5.65 m prototypes).
Patch variation supplies stands and feathered region masks supply edges; these
settings are not a claim of ecological realism or a qualified performance preset.
Prototype catalog and preview source hashes must be regenerated for this candidate;
stale previous bundles must continue to fail validation.

`generate_cloud_density_volume` is an experimental periodic 3D candidate: a fixed
table of 64 seeded centers and radii, erosion and coverage, derived once into R8
(32³–128³). Horizontal distances wrap; heights do not. It does not change the 2D
API or author an environment profile. The native capture has less regular spacing
than the former cell grid but remains soft/stylized, not an accepted realistic sky.
See `R1_CLOUD_LAYER_PREVIEW.md` in Gate 1 evidence.

`generate_cloud_density` prepares a deterministic periodic linear R8 field from
`CloudLayerSettings` seed/coverage. Resolution is bounded to powers of two 32–512.
Coverage is a monotonic threshold, not an exact covered-area fraction. Wind,
opacity, altitude and world scale do not alter the field. This prepares data only;
no cloud renderer or persisted environment integration is enabled by this API.

`RoadSurfaceProjection` derives visual earth weights from the existing compiled
road mask, with terrain/carving hash checks. It preserves terrain geometry and
colliders and does not replace canonical material sampling for scatter. Consumers
must retain its additional source hash alongside the terrain hash. This is a
derived renderer input, not a new saved document or an incremental editing API.

Read-only regional scatter diagnosis:
`cargo run -p forge_world --example inspect_grass_region -- terrain.json vegetation.json min_x max_x min_z max_z`.
Reports grass centres in a half-open rectangle for the authored document and independent
in-memory counterfactuals (no exclusions, no material filter, full acceptance, all three).
Input files are never written. Counts are not rendered coverage or additive rejection
statistics; slope, altitude, spacing and candidate identities remain unchanged.

WorldSketch compilation does not inherit the standalone fixture's hardcoded spawn
clearing or movement corridor. It derives a circle from each PlayerSpawn landmark's
authored position and influence radius, with an ID stable under movement, then adds
the existing authored spline corridors. The standalone fixture remains unchanged.
Moving the spawn changes its generated clearing, not the terrain or road geometry.
Evidence: `docs/evidence/gate1/R1_AUTHORED_CLEARINGS.md` at repository root.

The default valley recipe now uses eight grass attempts per cell with the existing
world-space density field. Attempts 0–3 retain their IDs and poses; attempts 4–7 add
patches without moving old placements or changing other layers. Existing exclusions
and filters still apply. The paired embedded High/Ultra policy uses 32 blades per
patch instead of 64, retaining the existing expansion ceiling. Low/Medium costs are
not constant under this change. Saved documents are not rewritten, but recompiling
a WorldSketch changes its derived vegetation hash and invalidates old catalog bindings.
Evidence: `docs/evidence/gate1/R1_MEADOW_COVERAGE.md` at repository root.

`quality_application::QualityApplicationGuard` implements the pure ADR-0011 acknowledgement
guard. It re-resolves a supported plan, binds sequence/hash/generation, and requires seven
current domain observations plus separate GPU evidence and exactly one active camera.
It rejects identity changes, pipeline failures, backward time and application timeout;
ready evidence is revoked if subsequently lost. A failure is terminal for that request,
not evidence of rollback. Caller supplies a monotonic clock, fresh observations and unique
session sequences. This does not probe the GPU or install a profile.
`QualityAdaptationController::observe_application` now binds that guard to its pending
decision (same policy, destination and sequence), then retains the exact plan identity.
CPU-only snapshots stay pending; duplicate readiness does not reset cooldown. A guarded
request cannot use the legacy tier-only acknowledgement. Failure/revocation stops this
controller and clears current proof; the remembered tier is not a rollback claim.
Reconstruction requires a newly observed installation. The optional renderer integration is
now in `forge_engine::global_quality`; these domain tests remain synthetic, not GPU proof.
The controller owns a copy of the first matching guard's progress. Later guard arguments
are read-only identity templates, not replacements for deadline, clock or readiness history.
The copy is made once per new pending application, not every observation. Timeout and
post-readiness invalidation therefore cannot be reset by reconstructing an equal-identity guard.

`from_observed_application` initializes the controller from a freshly observed effective
installation, copying the guard's actual history rather than trusting a remembered tier.
It requires the matching policy and complete current domain/GPU evidence, starts a cooldown,
and seeds future decision sequences after the installed sequence. `bind_pending_application`
lets the coordinator explicitly bind a newer installation sequence after intermediate snapshots;
binding is one-shot, policy/tier checked, and never resets the guard's history. Subsequent decisions
continue after that sequence. Once effective evidence is used, tier-only acknowledgement remains
forbidden even after rejection. The caller remains responsible for fresh observations and
exclusive coordination of session sequences. Engine adaptation calls these APIs only on opt-in;
fixed/manual requests remain the default.

`VegetationGenerationPlan::generate_render_density` fournit une sélection de présentation
distincte de la génération canonique : éclaircissement déterministe ou candidats supplémentaires
bornés, sans déplacer les survivants. Les couches avec collision restent intégralement protégées.
Les sélections sont imbriquées lorsque la densité augmente ; la valeur 1 reproduit exactement le
cache canonique. Aucun document ou cache canonique n'est modifié. Le renderer consomme désormais
cette API au démarrage optionnel et lors de transitions ECS ; leur qualification native reste ouverte. Voir les preuves
`R1_040_VEGETATION_RENDER_DENSITY.md` et `R1_040_VEGETATION_INITIAL_QUALITY.md`.

`QualityAdaptationController` interprète maintenant la politique du document de qualité sans
Bevy ni mutation persistante. L'adapter fournit le niveau réellement installé ; les décisions
restent en attente jusqu'à confirmation d'application. Hystérésis, temporisation, refus,
accusés périmés, bornes et verrouillage High des benchmarks sont testés. Aucun système de rendu
n'utilisait ce contrôleur lors de cette première preuve ; le raccord natif opt-in est maintenant
décrit plus haut. La qualification dynamique Runtime/Studio reste ouverte.
Voir `docs/evidence/gate1/R1_040_QUALITY_DECISIONS.md`.

Produit de façon déterministe la scène de preuve Gate 0 (sol, cube, lumière et caméra) depuis une
recette versionnée. La sortie est un `SceneProofDocument` moteur-neutre ; aucun mesh ou asset externe
n'est stocké dans la recette.

Pour `R1-001`, la crate produit aussi la fixture originale
`examples/visual_valley/terrain.v1.json` depuis `VisualValleyTerrainRecipe`. Le document persistant
reste défini par `forge_project::TerrainDocument` ; `forge_world` ne possède pas une seconde source de
vérité.

`TerrainGenerationPlan` interprète ensuite ce document avec une sémantique pure et versionnée :

- origine centrée `[-256, +256]` sur X et Z, mètres, Y vertical, système droitier ;
- `FractalValueNoiseV1` = valeurs de lattice SplitMix64 signées, interpolation quintique, somme
  d'octaves normalisée, calculs `f64` puis une conversion `f32` ;
- grille unique de `257 × 257` échantillons, découpée en `8 × 8` chunks de `32 × 32` intervalles et
  `33 × 33` sommets ;
- positions globales, UV globaux, normales calculées depuis la source globale et coutures identiques ;
- `surface_with_shading_normal_at` renvoie le même échantillon géométrique que `surface_at`
  plus l'interpolation normalisée des normales de sommets du maillage canonique ; cette seconde
  normale sert au rendu, jamais aux pentes ou collisions. Aucun format de document ne change ;
- indices et triangles collider en ordre explicite, avec winding `+Y` ;
- trois poids logiques herbe/terre/roche finis et normalisés ;
- plan de sélection partielle qui expose le rectangle de dépendance et retient exactement l'union en
  croix requise par les normales.

Ces données sont des caches dérivés sans Bevy, wgpu ou Rapier. Le rendu PBR et la création des
colliders moteur restent la responsabilité des adapters. Le module ne contient aucun asset ni code
Cadle. Le plan sale ne régénère et n'applique pas encore les chunks ; il ne qualifie donc pas à lui
seul `R1-007`.

`examples/visual_valley/world-sketch.v1.json` ajoute le pont éditable entre références visuelles et
documents Forge. `WorldSketchDocument` valide calibration, sources, régions, splines, landmarks,
incertitudes, verrous et caméras. Le premier `compile_world_sketch` reste borné à Visual Valley
512 × 512 m : il matérialise terrain, végétation, cinq masques sémantiques feathered, un cache de
carving des trois splines, un `TerrainDocument` v2 sculpté, un audit de praticabilité et une entrée
Blender dérivée. Le terrain v2 conserve 8 722 deltas clairsemés et alimente l'adapter heightfield
Rapier existant. L'audit v2 calcule la pente maximale de chaque triangle adjacent, applique la limite
de 45° du profil joueur, bloque l'eau et détecte les croisements route/eau. Le compilateur dérive
aussi `forge.world-bridge-placements` v1 : atterrissages, pente, dimensions, appuis et contrat de
collision du pont sud, liés aux hashes exacts du terrain et de l'audit. L'adapter runtime le
matérialise et un probe avec le vrai contrôleur Rapier qualifie ce franchissement. L'audit n'est ni
un navmesh ni un pathfinder ; le fournisseur de navigation reste à choisir. Le CLI vérifie aussi les
hashes et dimensions des PNG avant compilation et refuse d'écraser un dossier de sortie.

Le mode runtime WorldSketch dérive désormais l'eau de la rivière principale depuis les points et
hauteurs exacts du carving via `water_from_carved_spline`. Le contrat `forge.water` v1 accepte
`piecewise_linear_v1` (2 048 points maximum) en complément du Catmull-Rom historique (64 points) :
une seconde interpolation courbe ne doit pas décaler l'eau du lit déjà sculpté. La source reste
WorldSketch, le document d'eau est dérivé ; les profils optiques sont conservés. La politique
explicite `authored_surface_v1` préserve les hauteurs sculptées, même si le terrain les masque quand
le carving est insuffisant. Les anciens documents gardent leur ajustement de hauteur par défaut
et leurs hashes. Ce raccord ne
matérialise pas encore le ruisseau secondaire, ni une simulation hydrologique ou une édition Studio.

Chaque spline peut déclarer `maximum_cut_m` dans `(0, 64]` ; l'absence conserve le budget
historique par type (rivière 5 m). Ce paramètre est une source authorée, non une augmentation
automatique du budget. Le candidat de diagnostic à 40 m reste extérieur au workspace et non promu,
car il produit un canyon non qualifié artistiquement. Le runtime accepte ce fichier seulement
avec `--world-sketch-visual-valley --world-sketch-input candidat.json` ; il ne l'enregistre pas.
Les aperçus d'assets et le pont doivent correspondre aux hashes réellement recompilés du candidat.

Les splines d'eau acceptent aussi `surface_heights_m`, une altitude Y par point XZ, finie dans
[-1024, 1024] m et non croissante en aval. L'interpolation est linéaire entre contrôles : aucun
re-lissage ou relèvement caché. `maximum_fill_m` dans [0, 64] m autorise explicitement le remblai
du corridor uniquement avec ce profil. Sans ces champs, les documents et la recette historique
restent identiques. Un lecteur v1 plus ancien et strict rejettera ces nouveaux champs.
Le candidat local du 5 septembre préserve exactement les autres données authorées et le terrain
hors corridor, mais ses remblais et berges restent artificiels : il n'est pas promu malgré le
passage des tests d'altitude et du franchissement natif du pont. Une eau dégagée de son lit ne
prouve ni des berges naturelles ni une continuité hydrologique. Voir
`docs/evidence/gate1/R1_WORLDSKETCH_RIVER_AUTHORED_ALTITUDES.md`.

La correction élargie autorisée ensuite est intégrée à la révision 2 du WorldSketch embarqué.
`valley_blend_width_m` (16 à 192 m, avec profil et budgets explicites) prépare une plaine riveraine
avant le carving fin. Le raccord large conserve les emprises des villages/forteresses verrouillés
pendant le grading et n'élargit pas les exclusions de végétation. Routes et ruisseaux dérivés sont
rééchantillonnés sur cette base ; leurs tracés authorés sont inchangés. Les nouveaux profils runtime
conservent l'identité projet/zone et prennent la révision du WorldSketch lors de leur dérivation.
La fixture `tests/fixtures/world-sketch-pre-valley.v1.json` maintient les régressions de l'ancienne
recette sans remplacer leurs assertions par des tolérances. La revue artistique de Gate 1 reste due.
Voir `docs/evidence/gate1/R1_RIVER_VALLEY_REGRADING.md`.

La compilation relie également le noyau dur du masque d'exclusion des splines au scatter runtime :
trois `PolylineCorridor` sont dérivés des tracés sculptés (route, rivière, ruisseau), avec une
demi-largeur égale à la demi-largeur authorée plus 55 % de la berge. Le bord adouci reste un indice
de densité DCC, non encore une densité runtime. Les deux exclusions historiques sont conservées.
Chaque corridor a un ID déterministe indépendant de l'ordre et 2 à 2 048 points validés. Sa zone
peut déborder aux limites de carte, mais ses points doivent rester dans la zone. Les instances
restantes gardent leurs IDs et transformations ; les placements manuels du kit ne sont pas filtrés.

`TerrainLodTopology` produit des caches de présentation aux pas 1/2/4/8/16/32 en conservant
tous les échantillons du périmètre des chunks. Les cellules de bord utilisent un éventail autour
d'un échantillon central existant ; les positions, normales, UV et poids retenus sont copiés
exactement. `TerrainLodMesh` ne contient aucun collider ou document canonique. Les raccords de
LOD mixtes et la source sculptée sont testés ; le renderer utilise désormais ces caches
via `VisualValleyTerrainQualityPlugin`. Voir `docs/evidence/gate1/R1_040_TERRAIN_LOD_RENDERER.md`.

`TerrainLodTopology::project_morph_target` prépare les hauteurs, normales et poids du
LOD cible aux sommets du LOD courant. Topologie, XZ et UV sont conservés ; les sommets
communs sont copiés exactement. Aucun document/collider modifié. Cette préparation n'est
pas encore raccordée au renderer et ne prouve pas la coïncidence des triangles : les
éventails de bord peuvent croiser les arêtes du LOD cible. Leur compatibilité et le
raccord des passes GPU restent à qualifier avant suppression du fondu existant.

`project_transition` découpe maintenant les intersections des triangles des deux LOD
pour produire deux maillages de même topologie, avec les attributs de chaque surface.
Ce raffinement traite les croisements des éventails, avec un plafond de 65 536 sommets.
Le test de relief irrégulier compare aussi les centres des triangles aux deux surfaces
originales, l'aire couverte et le winding. Préparation CPU non raccordée au rendu :
continuité en mouvement, coût, jonctions de chunks et passes GPU restent à qualifier.

Les sommets communs des transitions sont partagés seulement si les 22 composantes
positions/normales/UV/poids des deux extrémités sont identiques bit à bit. Indices remappés,
aucune quantification ; une couture distincte à l'arrivée reste distincte. Le raccord
natif Bevy a ensuite été réalisé : voir `R1_DISTANT_PATTERN_ISOLATION.md` pour les preuves
et réserves de rendu, notamment couleurs, berges et mouvement/Web.

La couche de végétation accepte désormais un `density_pattern` optionnel : bruit de valeur lissé
en coordonnées monde, taille de zone de 4 à 512 m et multiplicateurs de 0 à 2. Sans ce champ,
le scatter uniforme historique est conservé. Le même champ gouverne les ajouts de densité qualité,
sans réactiver une zone de probabilité nulle. La recette de vallée active seulement l'herbe :
probabilité de base 0,8, zones de 32 m, multiplicateurs 0,15–1,6. Cela change la source végétation
et ses caches, pas le WorldSketch, le terrain ou les placements manuels du kit. Les candidats
conservés gardent leur identité et leur pose ; les exclusions et plafonds existants s'appliquent.

Validation ciblée :

`RoadSurfaceProjection::coverage_at` échantillonne le masque de route dérivé sur
les triangles de la grille terrain supportée (257 × 257, pas de 2 m). Il refuse
les coordonnées non finies/hors terrain et permet aux surfaces superposées de
réutiliser la route sans modifier les documents ni la géométrie canonique.

```text
cargo fmt --package forge_world -- --check
cargo test -p forge_world --locked
cargo clippy -p forge_world --all-targets --locked -- -D warnings
```
