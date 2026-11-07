# forge_engine

`source_atmosphere`, `source_clouds`, `source_sky` and `source_environment`
own the existing source environment execution: LUTs, temporal cloud history,
solar/readback publication, sky material, HDR cube/SH programs and their ordered
render graph. Studio and the reference import these owners directly. Prepared
and resident scene aggregates receive explicit verified inputs; package readers,
acquisition identities and source pins remain outside the engine. Shader handles,
layouts, resident generations and GPU publication semantics are retained. Common
program admission validates all kernels, dispatches, binding groups and uniform
sizes before compilation on the scene's render device (ADR-0046).
`source_environment::host` installs explicitly admitted module inputs, follows the
real scene camera and reuses these cloud, sky and HDR graphs. Its readiness keeps
encoded work, HDR publication and completed renderer fences separate (ADR-0047,
native qualification pending). Source-lit surface material consumption still
requires connection; moving this execution alone proves no parity.

`source_shadows::heightfield` owns the existing height mip preparation and GPU
shadow kernel host. Studio and the reference call it directly, preserving the
source half conversion, reduction, layouts and shader handle (ADR-0048).
Its `PreparedInput::from_heights` selects the attributed engine kernel using
the scene's heights. `source_vegetation::scene::shadow` owns the existing
plant-caster preparation and selection used directly by Studio and the reference.
The world plant material now retains its source BRDF/translucency functions in
an explicit lighting specialization alongside its historical Bevy lighting.
`bind_source_scene` requires the actual resident environment and both shadow
generations before adopting their resources. This interface alone does not
qualify consumption by the autonomous module; that integration remains in work.

`source_vegetation::grass` owns the source-profile cell selector, bounded draw
slots and buffer publication shared directly by Studio and the reference host.
Static oracle captures also use this resident instead of constructing separate
draw entities. Source traversal order, 48/160/420 slots, distances, flags, bounds
and identity model bindings are preserved. Package/schema validation belongs to
`forge_assets::source_vegetation_package::grass`; the source layout remains
256 by 256 cells over 2 km.

`grass::fields` binds that profile to an admitted 2048² unit raster: source-height
hash, origin, masks and immutable package geometry are explicit. Raw cell-centred
heights and exact authored corner deltas stay separate. The shared
`sampled_terrain_corrections` evaluator supplies the existing edit semantics;
resampling corrections into source centres is not an equivalent representation.
`grass::host` uses the same generic grass resident with `WorldPlantMaterial`,
the existing world frame and a scoped renderer inventory. The canonical imported
source must explicitly declare its grass profile under version 2, with the
matching closed manifest pin. Version 1 modules keep their authored plants and
do not acquire a field from merely using a prototype library. Warm-up does not admit
the scene. The first publication requires a current GPU token and then a new
completed inventory generation; a module declaring a field also requires its grass readiness.
Ownership checks retain slot identity, visibility, material fields and CPU buffer
payloads. Status reports selected cells and dated GPU resource counts separately.
The source-profile layout, procedural grass presentation, other terrain formats,
Studio interactions, visual parity and quality profiles need their own validation.

`RenderVegetationPrototype::streaming_envelope` reads retained, normalized mesh
positions from every LOD and returns a pivot-centred bound for the shared
imported-plant selector. It scans each unique mesh once per call and refuses
missing/nonfinite geometry, incomplete LODs, more than 256 unique meshes or one
million vertices. An immutable preparation must recompute it after source edits.
This CPU bound does not certify provenance, materials, GPU uploads or rendering.

Gate 7 adds the opt-in `cell_streaming::SampledCellStreamingPlugin` for an
existing canonical sampled terrain. One bounded background job prepares exact
cell geometry and near colliders. The exclusive admission step rejects stale
requests before changing the world; retired cells are despawned and their owned
mesh assets removed. No authored source, gameplay or save is a residency cache.
The caller supplies a finite observer position and may explicitly retry a
failed request. Native raster/GPU/physics measurements remain separate from CPU
mesh byte budgets. This is a terrain projection foundation, not a qualified
generic HLOD scene or an automatically enabled product setting.

`with_terrain_hlod` adds a balanced quadtree over the exact complement of the
fine resident cells, including protected simulation islands. One distant mesh
aggregates multiple base cells; neighbouring levels share exact canonical
edge positions, normals and paint weights. The same background job and exclusive
admission own both selections, retain unchanged tiles and remove retired mesh
assets. Their combined CPU buffers share the existing resident/staging budgets;
`TerrainHlodStats` reports distant geometry separately and the combined observed
staging peak. These meshes have no colliders. The generic raster game and
authoring adapter enable this terrain hierarchy; GPU/native qualification is
recorded separately. Static objects and vegetation require their own HLOD work.

`static_hlod_geometry::prepare_aggregate` prepares owned static proxy meshes
through the engine-neutral bounded reducer. It transforms compatible primitives,
welds exact duplicates within each primitive, preserves discrete attributes and
compacts the selected original vertices through public Bevy operations. Floating
normal/tangent/UV tolerances are explicit; no collision source is changed. CPU
data, animation, layout, pose and preparation-budget refusals are typed. Packed
output bytes are not a VRAM or allocator measurement.

`static_hlod::StaticHlodPlugin` selects admitted immutable object primitives
through the terrain controller's read-only partition. It keeps an entire object
detailed when any of its bounds touches a full-detail cell, groups compatible
material/layout/render flags, and prepares one bounded asynchronous transaction.
Original entities, mesh assets, identities and colliders remain available;
retained source buffers and CPU snapshots are reported separately from proxies.
Every original stays visible until the complete replacement has actual GPU
allocations, current camera resources, stable pipelines and a completed queue
observation. A changed partition, source or owned projection restores detail;
stale jobs cannot publish. Qualification captures wait for completed selection.
The module game marks its admitted manual primitives and canonical room parts.
StandardMaterial is registered by default; other shader routes require explicit
pose-baking review and preservation of their extra render flags. UV error is
numeric and bounded, not a texture-fidelity promise. Native object/vegetation
HLOD, visual fidelity, profiles and performance remain separate qualifications.

`CellStreamingProtection` adds bounded full-detail neighbourhoods for active
simulation cells. Changing that protection invalidates in-flight work even if
the observer stays in the same cell. `PlayerCellStreamingPlugin` is an opt-in
host barrier for one exclusive default Rapier context: it derives conservative
body coverage, waits for actual synchronized colliders, and holds both dynamics
and KCC translations when coverage is unavailable. Additional motion actuators
must run before `PlayerCellStreamingSet::Enforce`. Multiple contexts, unsupported
compound bodies/custom character shapes and out-of-grid current or predicted centres are explicit
errors; the host must provide recovery before exposing that profile to players.
Headless tests cover continuous controller motion and distant-body support;
these do not qualify the installed game or arbitrary forces and body profiles.

Four owned Rapier half-spaces bound a finite playable grid at every height.
The barrier verifies their original shapes, poses and live backend registrations.
Once synchronized, conservative body margins protect their in-map cell coverage;
they do not freeze a valid start near an edge. A held KCC receives no translation
request, because even a zero request can apply contact corrections. The private
vertical limits contribute no walkable navigation triangles; actual terrain
triangles still bound navigation, with no floor outside the admitted map.
Tests cover four edges, a corner, turning back, altered limits and unsafe sweeps.
Native traversal and the general HLOD/performance qualification remain separate.

Readiness also requires the original shared collision shape, identity transform
and root placement of every owned cell. Synchronizing a substituted shape or a
moved/reparented cell in Rapier cannot certify the original terrain. Native
command integration tests verify that held simulation does not advance canonical
gameplay, accept actions or overwrite a committed save, and can recover when the
original projection is restored. Mesh content edits through an unchanged asset
handle are outside this identity check; the host must keep these derived assets
owned by the streaming adapter.

`with_authored_material_layers` reuses the existing Forge three-role PBR shader
and canonical grass/earth/rock paint weights. Full and coarse cells retain the
same boundary samples and paint overrides. Weight buffers are included in the
owned CPU mesh budget; uniform diagnostic materials release unused weights.
Rendered qualification and integration in the shipped module game remain separate.

`cell_streaming::request_source_update` queues the latest canonical terrain edit
in the existing world. It pins the currently installed source and refuses a
different project, zone or grid. A replacement prepares every requested cell
while keeping the old cells; their actual owned CPU mesh bytes remain in the
staging budget. Superseded work is discarded. Failed preparation or admission
retains the previous source and projections. Passing the current source cancels
a pending edit without restarting the scene or removing unrelated entities.
`ResidentSampledTerrain` shares the adapter's immutable plan and changes with
successful replacement admission. At startup no residency is implied until
`CellStreamingStats` confirms the first admission. Neither this resource nor
queue acceptance certifies GPU upload or Rapier backend synchronization.
Plugin build transfers its bootstrap source to the active adapter; Bevy's
retained plugin metadata therefore does not keep the initial raster alive
after replacement. References deliberately held by the caller remain its responsibility.

`cell_streaming::sampled_policy` validates the shared initial module-game/editor
grid profile from raster metadata. `source_is_synchronized` additionally checks
live owned cell identity, detail, source, meshes and the actual near Rapier
shapes/poses, including after source replacement. It rejects queued edits,
pending work and reported streaming failures. GPU readiness remains separate;
the authoring host must observe fresh stable render pipelines before admitting
terrain gestures. The brush cursor can sample `ResidentSampledTerrain` and pick
only the admitted source's full-detail colliders, without a duplicate raster.

The shared-runtime diagnostic example `cell_streaming_region` exercises a
synthetic 2 km region with three explicit camera positions and PNG/report output.
It does not qualify continuous player traversal, Studio authoring or saves.
Its opt-in `--walk ABSOLUTE_FRESH_OUTPUT_DIRECTORY` mode instead drives the real
player controller over a painted 640 m return route and retains a distant dynamic
body's ground. It emits departure/destination/return and paint-crossing captures,
bounded trajectory/timing data (including streaming waits during motion) and an
explicit diagnostic scope; it still does not qualify packaged gameplay or saves.
The diagnostic installs `CaptureReadinessPlugin` and uses
`observe_render_readiness` before sampling. Readiness requires fresh stable
observations of a nonempty pipeline cache and resets when the scene changes.
It proves preparation of known pipelines, not pixel quality or GPU timestamps.

Adaptateur générique des documents Forge vers Bevy, le rendu et Rapier.
Le stockage natif des parties conserve l'enveloppe canonique `GameSession` dans
un emplacement distinct par module et zone. Il vérifie aussi cette appartenance
avant écriture : une erreur de dispatch ne peut pas remplacer une autre partie.
Les tests A→B→A réutilisent volontairement les mêmes IDs locaux et contrôlent
inventaires, objets consommés, positions et journal de commandes après reprise.
Un staging interrompu conserve la version engagée et bloque seulement la nouvelle
écriture de sa zone ; la reprise ne promeut pas ce candidat implicitement.
Ces contrôles de stockage ne qualifient ni passage en jeu entre zones ni transfert
d'un inventaire partagé ; aucun nouveau format de campagne n'est introduit.
Cette crate ne contient ni carte intégrée, ni acquisition de carte, ni shader
porté d'un contenu externe. Ses dépendances de production n'incluent aucun
composant de carte. Elle compile sans composant de carte, en natif et en WASM.

Les documents restent la source de vérité. Terrain, végétation, eau, physique,
navigation, contrôleur, gameplay et programmes de matériaux admis sont projetés
par les API de cette crate. Les instances ECS et ressources GPU sont dérivées.

Les extensions de locomotion publient `player_motion_extension::PlayerMotionExtension`
avant la caméra et les observations de gameplay. Le moteur ne connaît que ce
contrat : point de vue, synchronisation du collider et contact humide local.
L'absence de cette ressource conserve le contrôleur standard.

Les caméras temporaires portent `scene_camera::OffscreenSceneCamera` pour être
exclues des outils interactifs de terrain.

`render_inventory` fournit une barrière de ressources pour une projection
possédant ses meshes, matériaux et buffers immuables. Après installation du
plugin, `request<M>` enregistre un propriétaire, une identité, une génération
et les handles réels. Le matériau doit déclarer toutes ses dépendances : les
images et buffers de stockage sont admis, les autres types sont refusés.
Les allocations, bind groups, vue unique, pipelines stables et fence de queue
sont observés dans le renderer. `acquire` ne retourne un token opaque que pour
des observations fraîches ; le consommateur le revalide avant publication et
attend `covers_frame` pour une fence postérieure à la publication.

La barrière borne les inventaires à 512 meshes, 256 matériaux, 4 096 images,
2 048 buffers et 256 Mio de payloads de stockage. Elle refuse les changements
de ressources sous une génération inchangée, les régressions et un second
propriétaire. `release` conserve le compteur de durée de vie interne : recréer
la même requête ne réactive aucun ancien reçu. Les uniformes dynamiques sont
permis ; identité auteur, géométrie, bindings et contrôles de LOD exigent une
nouvelle génération. Les modifications de payload sous un handle inchangé
restent sous le contrat d'immuabilité du propriétaire. Le token ne qualifie
ni pixels, ni couverture des instances, ni physique, ni performances.

`physics::imported_vegetation` prépare les proxies canoniques d'une sélection
de plantes indépendamment des LOD. `Prepared::from_selection` valide l'ensemble
avant `Resident::stage` ; un propriétaire déjà présent est refusé. Les poses,
profils et bits f64 d'origine sont épinglés. Le résident conserve ses formes
réelles canoniques ; `matches` rapproche une nouvelle sélection auteur,
`ready` vérifie les poses, groupes, activation et bindings du backend par défaut.
L'hôte attend ces deux conditions et sa barrière GPU avant de libérer le jeu.
Une sélection explicitement vide ne qualifie aucune autre physique de scène.

`ImportedPlantCollisionPlugin` actualise la résidence avant `SyncBackend`, avec
le même prédicat d'activation conservateur. Seules les formes requises par les
enveloppes des corps existent dans ECS/Rapier. Les sources lointaines restent
préparées et reviennent sous leur identité canonique. Les entités et handles
physiques peuvent changer lors d'un retour ; un changement de LOD seul conserve
ceux des formes résidentes. `source_count` compte toutes les sources,
`resident_count(world)` les formes actuellement projetées. La disponibilité
exige le jeu exact requis, ses formes/poses et bindings réels, ainsi que la
disparition effective des anciens mappings et handles dans Rapier, y compris
lors du remplacement par une sélection vide. Les contrôles ne remplacent pas
un acquittement backend par la seule disparition d'une entité ECS.
L'hôte place son contrôle après `ImportedPlantActivationSet`, puis rejoue sa
barrière finale après les actuateurs. Les enveloppes conservatrices des corps
mobiles sont partagées avec le streaming terrain : formes, hiérarchies, vitesses
sur deux pas et commandes KCC déjà publiées sont prises en compte. Les obstacles
restent actifs sans observateur ou si le mouvement est invalide ; ce dernier cas
ferme la disponibilité. Une panne, forme remplacée, déplacement, reparentage ou
contexte supplémentaire ferme aussi le contrôle. `retire` ne retire que les
entités encore possédées ; une perte de marqueur retourne une erreur et conserve
l'entité réassignée. L'hôte traite cette erreur au lieu de reprendre une scène
partiellement retirée.

La préparation est bornée à 100 000 proxies et un million de sommets de hulls
dérivés. Le scaling est celui de Rapier (pas 1e-4, subdivision 20 pour les formes
non uniformes non représentables comme primitives lisses). La forme effectivement
préparée est ensuite figée avec une échelle de racine unité. Ce budget ne mesure
pas la mémoire interne de Rapier et ces tests ne qualifient pas le rendu natif.
Une forme de taille nulle renvoyée en fallback par le scaling reste un refus,
même lorsque les dimensions d'entrée et l'échelle enregistrée sont positives.

```powershell
cargo check -p forge_engine --lib --locked --offline
cargo test -p forge_engine --lib --tests --locked --offline -- --test-threads=1
cargo check -p forge_engine --target wasm32-unknown-unknown --no-default-features --features web --lib --locked --offline
```


`source_vegetation` owns the shared source GPU layouts (`Record`/`Params`),
vertex attributes and material key. `source_vegetation::world` owns the
world-lit material and the atomic current/previous geometry frame. Wire sizes
remain 32 bytes per record and 96 bytes for the parameter/frame payloads;
shader UUIDs, attribute IDs and formats remain stable.

`source_vegetation::scene` accepts caller-decoded derived groups/levels. These
inputs are not authored documents. `validate` checks bounded counts, stream
lengths, identities, finite values, transforms and selection policy before
allocation. No reference group count or reference population size is required.
The current adapter bounds are 256 groups, eight levels per group and the
existing imported-instance/placement limits. Source codecs retain resource
pins, wire schemas and dataset qualification.

`scene::resident::Resident<M: SourceMaterial>` owns shared immutable source
buffers/models and reuses surviving entities across selections. It is called
by the reference adapter, Studio and module adapters. Its generic draw/identity
snapshot accepts the caller's ID-material factory. The old map-owned resident
implementation is removed. Asset presence and `is_intact` are CPU checks;
callers still require actual render inventory fences and physics admission.
`scene::streamed` also owns canonical preparation, spatial bounds, the host,
render publication and backend admission. `scene::from_population` uses that
same preparation for the editor, including small authored populations. Its
CPU-only result is not GPU admission. The host retains actual render inventory
fences, physical residency acknowledgements and retirement checks.

`canonical` retains source order and the original f64-to-f32 packing behavior;
the resource codec and packing live in `forge_assets::source_vegetation_package`.
Reference dataset counts, schema IDs and source shader digests stay in that
codec. `textures` owns source texture upload and sampling; `world::shader`
derives the world-lit shader after codec qualification. The retained WGSL
surface, shader IDs and derived render identity are unchanged by this transfer.
`source_vegetation::captured::CapturedPlantMaterial` now owns the existing
captured-light material, its fifteen resource dependencies and shader staging.
`source_vegetation::motion` owns the shared live wind buffer. Studio and the
reference adapter call these common APIs directly; the world-lit material
remains another lighting capability of the same engine.

`source_lighting` owns the aligned direct-light/SH payload, source shader seam,
shared lighting buffer and generation leases. A CPU publication cannot overwrite
the current GPU owner's buffer, and an old lease cannot release a newer one.
`source_shadows` owns the aligned receiver settings, shared encoded buffer and
held-view readback. The depth encoder supplies the receiver bytes in the same
submission as its layers. Disabling temporal accumulation retains the existing
stable filter phase. Layouts, resource UUIDs and shader bindings are preserved;
only the captured shader's diagnostic filename changes.

`source_vegetation::grass` owns the source selector and bounded resident for
both captured and world-lit materials. A versioned canonical declaration enables
the imported field; a prototype library alone does not request original map
content. The module host admits the original height/mask images, separate corner
corrections and an owned completed GPU inventory after publication. The source
profile remains constrained to its raster/grid/mask formats (ADR-0042).

`source_vegetation::picking` owns the source ID material, alpha/LOD-preserving
shader adapter and offscreen pass. `occluders::Prepared<T,V>` captures caller
terrain/source-surface materials plus opaque standard surfaces, preserving real
affine hierarchy and skin palettes. The supplied vertex/layout/alpha contracts
must match the admitted source shader; arbitrary custom shaders are not inferred.
The pass retains its linear RGBA8/no-MSAA/no-tonemapping policy, original IDs,
resource UUIDs and revision-bound snapshot. `OffscreenSceneCamera` is the common
marker used by all producers and scene-tool filters. The caller still owns
readback freshness, pipeline completion and retirement (ADR-0043).

`view_depth` augments extracted effect cameras with sampleable depth before
texture preparation, preserving authored usage flags. `source_shadows::casters`
owns the source cascade depth graph, opaque/terrain/plant uploads, retained
animation poses, cadence and recovery. `masked` and `textured` hold transient
caster frames and their resident GPU kernels; source codecs supply the admitted
geometry, masks and poses. Prototype indices use the admitted inventory; the
four-prototype acquisition guard remains in its source codec. Scene eligibility
collectors remain in their integrations. Render-world camera selection uses the
extracted common offscreen marker (ADR-0044). Studio authoring, full profiles and complete visual
or performance parity remain separate qualifications. Held readback is not a fence.

The shared shadow receiver returns neutral transmittance before sampling when
cloud shadow strength is exactly zero. A disabled source can have a zero extent;
evaluating its coordinates and then mixing an invalid sample with zero strength
can poison the final lighting. Positive strengths retain the source sampling body.
This guard is used by source plants, terrain and scene materials alike.

`source_lighting::scene_buffer::observation::observe_while_held` provides an
optional 176-byte GPU payload observation for a held diagnostic. It enables copy
usage before rendering, or uses an existing allocation already allowing copies.
It refuses to recreate a GPU-owned allocation from its potentially stale CPU
seed. `ownership::Owners::owns` exposes only ownership status, without the lease.
Readback is asynchronous and optional, with no source-value edits or permanent
readback cost. It does not certify a draw or replace render admission fences.

`cpu_timing::Span` and `render_timing::RenderTimingPlugin` share the reference
benchmark's observational clocks with module hosts. Set
`FORGE_RUNTIME_CPU_PROFILE=1` before launching the native module host to enable
cumulative CPU scope logging and the render clocks. The reference performance
walk uses the same clocks; its scenario/admission metadata stays in its adapter.
The diagnostic plugin is installed once, regardless of scene/timing plugin order.
The main scope ends after all `Last` systems, and fixed/render bands are separate.
CPU scopes include waits and are nested. Asynchronous GPU values retain their
publication age and are not current-frame completion evidence. No readiness,
input, presentation mode or authored document is changed by the observer.
`MainClock::elapsed_ms` is elapsed time since its current `First` marker when
read; the cumulative main scope closes in the final timing phase. Caller-owned
graph bands use `begin_graph`/`mark_graph`. These clocks do not replace render
inventory fences or physics backend acknowledgements, nor qualify performance.

`source_wind::WindFrame` owns source scene time and bounded wind updates;
`source_wind::vegetation::Clock` owns the existing vegetation gust transport and
source phase counter. Studio and reference consume these owners directly.
The playable imported host now calls this same clock, replacing its separate
gust integrator and modulo-16 counter. Authored strength/frequency and module
initial phase remain inputs; waveform frequency does not replace physical gust
transport. Candidate clocks commit only after successful frame publication,
with the actual previous pose retained for motion vectors (ADR-0045).
The host ignores common offscreen views and still refuses ambiguous interactive
cameras. These contracts do not qualify the remaining source lighting/shadow
receiver integration or every profile's native behavior.


`source_surface` owns the existing source-profile material, mesh attribute decoder,
underwater sampling parameters and solar relay. Studio, reference and native module
consumers use these types directly. Shader UUID, vertex IDs/formats and bind groups
are retained. `SurfaceSunPublication` orders consumers without a map-system dependency.
Shader admission/acquisition and remaining source-object hosts still need extraction.
See `src/source_surface/README.md` and ADR-0049; this transfer does not qualify parity.


`source_surface::geometry` owns revision-keyed CPU/resident mesh parts; admitted
acquisition adapters construct it through `PreparedMeshes::from_parts`. With Rapier,
`source_surface::objects` owns the existing transactional preparation, placement
entities, triangle colliders and live synchronization/retirement checks. These
owners have no package reader or map dependency. See the local source_surface
README and ADR-0050 for admission responsibilities and qualification limits.


`source_surface::package` owns the existing admitted surface-package geometry and
texture preparation (local/source-reference attributes, mip layout and sampling).
The bytes come from `forge_assets::source_surface_package::SurfacePackage`; no
map crate or reference path supplies this execution. The module API-7 object binding
and resolver are transport contracts; Studio/native host consumption remains pending.

## Source object module integration (26 October)

The native API-7 consumer now uses `source_surface::package::host` and the same
source material constructor as the reference. The source shader integration still
has an acquired adapter outside this owner. Source HLOD retains detailed originals
without claiming a proxy; coverage requires resident materials, including ordinary
modules without a hierarchy host. Scoped render inventories are bounded to six
owners for four display families, sky and grass; all per-inventory checks remain.
The independent native fixture has two visible source houses and real GPU/collision
evidence. This does not establish full fidelity or performance. See central
`module-object-runtime-validation-001.json` and the Gate 7 progress report.

## Authored paint in the common source terrain (27 October)

`source_terrain::paint` projects explicit canonical grass/earth/rock weights into
the existing source material. The authored palette shader is shared with the
existing PBR terrain extension, with its original calculations retained. Empty
paint keeps the source program bytes; sparse crops retain neutral gaps and use
a bounded storage buffer. No additional texture slot, geometry or collider is
introduced. The source shader's macro normal and authored raster diagonal are
preserved. CPU-only apps register the embedded module without requiring a renderer.

Central `module-terrain-paint-validation-001.json` retains 2,757 passing workspace
tests, strict native/Web Clippy, 25 architecture checks, the CPU assembly probe and
inspected native GPU runs. Unpainted pixels and PNG bytes are unchanged; authored
paint is visible, and the existing module renderer also passes its regression.
Canonical module viewport/game adoption, actual Studio editing and performance
parity remain unqualified. The source_terrain README records the exact scope.


## Shared environment preparation (26 October)

The existing environment package preparation now belongs to
`source_environment::package`; atmosphere acquisition conversion belongs to
`source_atmosphere::acquisition`, cloud-controller replay to
`source_clouds::acquisition`, and program decoding to
`source_environment::programs::package`. Reference probes, Studio and the native
module adapter call these owners directly. The previous map-owned modules are
removed. Historical schema identifiers remain data-format constants in
`forge_assets`; they never select a module or map.

`source_surface::PROFILE_PROGRAM` retains the admitted source program's exact
64,207-byte body with its legal notice. It supplies shader capability when an
editor has no object palette; it does not supply reference geometry or textures.
The shared wind-kind predicate covers cloth and foliage (5/6). This correction
does not qualify their full animated shadow fidelity.

The workspace has 2,740 passing tests, strict native/Web Clippy passes, and the
independent native API-7 replay completes six steps with both captures inspected.
The Studio resource-channel test also passes, but the canonical Studio viewport
has not adopted these resources yet. Full visual/performance parity and Gate 7
closure remain open. Evidence: central `studio-module-resources-validation-001.json`
and `module-object-runtime-native-004/`.

## Studio uses the shared module hosts (26 October, studio-common-runtime)

The canonical raster editor now installs the same environment, streamed vegetation,
source-object and placement-light hosts as the native module game. Explicit authoring
holds and replacements preserve the expected head, admitted terrain, resource owner,
immutable geometry and readiness barriers. Plant installation validates the actual
cell controller's admitted source; game-launcher metadata alone cannot admit it.

The common local-light transport decoder belongs to `forge_assets`. The current
workspace passes 2,743 tests (67 ignored), strict native/Web Clippy and 23 boundaries.
Native replay `module-object-runtime-native-005` passes with inspected captures.
The real Tauri editor also passes source-house preview/abandon/apply/undo/redo,
save/process-restart/reopen and an empty-companion area round trip. These are partial
qualification results, catalogued in `studio-common-runtime-validation-001.json`.
World-lit Nature picking, remaining shader/effect extraction, modules without a
retained origin and comparable performance remain open; Gate 7 is not closed.


## Shared surface receivers — native proof, Studio replay pending (26 October)

The engine owns surface shader assembly, ground bounce and terrain/surface underwater
receivers. All consumers name these owners directly; the old map material module is
removed. Four WGSL files retain exact bytes/notices and four existing tests move with
their implementation. The neutral frame preserves its 32-byte layout and behavior.
The frozen tree passes 2,744 tests (67 ignored), strict native/Web Clippy and 24
boundaries. Native replay 006 has inspected captures. A repeated Studio retouch exposed
a command-ID collision after module undo/reopen; its common host repair passes a
red/green regression, including exact replay and misuse refusal. The rebuilt Studio's
UI replay is pending after Computer Use was stopped with Escape. See central
`shared-surface-receivers-checkpoint-001.json`; this is not completed UI, parity,
terrain/CDLOD transfer or Gate 7 qualification.


## Shared terrain owners qualified locally (26 October)

`source_terrain` now owns the existing terrain material, textures, CDLOD, normal/AO
preparation, scene replacement and bounded resident tile cache. The package reader
belongs to `forge_assets`; capture demand and underwater bindings/material inputs
also have common owners. Old map modules/types and compatibility exports are removed.
Four tile tests formerly confined to the reference example now run as engine tests.

The frozen tree passes 2,748 workspace tests (67 ignored), strict native/Web Clippy
and 25 boundaries. Real package admission, exact AO over 4,194,304 samples and atomic
terrain replacement pass. API-7 module replay 007 and the source-terrain reference
render pass, with inspected captures. The reference uses scene-lighting diagnostic
mode; its black background does not qualify sky or live ocean rendering.
See central `shared-terrain-owners-validation-001.json` and the source-terrain README.
Canonical module adoption, other reusable effects, Studio replay, profiles and matched
performance remain open; this owner transfer does not close Gate 7.

## Native modules consume the shared source terrain (27 October)

`source_terrain::host` now consumes the explicitly admitted terrain profile and
the same environment/surface scene. It calls the existing scene, CDLOD and tile
cache. Partition cells keep their identities and colliders while their previous
draw is hidden. Resource ownership and renderer/physics readiness remain separate;
late or revoked normal generation needs a later GPU completion observation.

The dependency-aware composition already used by lit surfaces is generalized
into `material_composition::ComposedMaterial`. Lit surfaces and terrain LOD use
that one implementation; the old surfaces/material module is removed. Bevy's
plain composed material does not visit nested fields: the first native report
correctly refused zero declared images/buffers. The corrected native replay
observes 57 terrain meshes, 13 images and 5 buffers with completed typed inventories.

The actual API-8 module retains its houses, vegetation and synchronized physics.
The reference/paint PNGs remain byte-identical, and the module without a terrain
profile also passes. Central `module-source-terrain-runtime-validation-001.json`
records 2,764 root tests, 87 native consumer tests, 126 delivery tests, strict
native/Web Clippy and 25 boundaries. This qualifies initial native consumption
of the fixed profile. Studio adoption, edits/replacements, camera traversal,
remaining effects, profiles and matched performance remain open.
