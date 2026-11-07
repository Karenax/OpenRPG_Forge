# Intégration Tidewater

Composant facultatif de carte, séparé du moteur générique.

L'atmosphère, les nuages, le ciel et l'environnement HDR s'exécutent dans
`forge_engine::source_atmosphere`, `source_clouds`, `source_sky` et
`source_environment`. Les consommateurs les nomment directement. Cette crate
conserve les lecteurs d'acquisition et de programmes, leurs pins, gardes de
dataset et construction d'entrées communes ; elle ne réexporte pas leur exécution
GPU. Le raccordement de ces capacités aux modules autonomes reste un travail
explicite, distinct du transfert et de la qualification de la référence (ADR-0046).

Les ombres du relief et leur préparation de hauteurs s'exécutent désormais dans
`forge_engine::source_shadows::heightfield`, employé directement par Studio et
la référence. La crate de carte n'en garde ni implémentation ni façade. Le raccord
des surfaces du module à l'éclairage/récepteurs source suit ADR-0048 et reste à
qualifier sur ses ressources réelles.

```text
Studio / application de carte
  ├─ forge_engine : capacités génériques
  └─ forge_tidewater : projections et extensions de la carte
       ├─ forge_engine
       └─ forge_tidewater_assets : acquisitions et recettes
            └─ forge_assets : contrats génériques d'assets
```

Le moteur et les contrats d'assets n'importent et ne réexportent aucun de ces
deux composants facultatifs. Les appels aux API moteur sont explicites ; aucune
façade de compatibilité ne réexporte le moteur depuis cette crate.

Ce composant contient encore des projections de bâtiments, végétation, océan,
atmosphère, matériaux et rendu temporel, ainsi que les ports de référence et
leurs notices. Leur présence ici ne rend pas ces mécanismes spécifiques à la
carte : la consigne propriétaire des 24 et 25 octobre exige de transférer leur
partie réutilisable au moteur, puis de raccorder référence et modules au même
chemin. Cette extraction reste partielle ; la séparation des dépendances seule
ne la qualifie pas. Les données et acquisitions restent dans
`forge_tidewater_assets` et les paquets externes gouvernés. Les shaders et caches
GPU ne deviennent pas des documents auteur.

La déformation, la préparation des maillages, l'horloge et le type de matériau
des poissons sont dans `forge_engine::source_fish` ; les modèles sont validés
une fois puis réutilisés par les instances. Les liaisons d'éclairage, la table
de peau, l'assemblage des shaders et le masque d'ombre sont également communs.
Les chargeurs fournissent leurs octets déjà vérifiés au moteur. Cette crate
conserve les données de référence et la gestion de ses instances ; le transport
et le raccordement des autres modules restent à terminer. Ces transferts ne
qualifient ni le rendu ni les performances.

L'extension de joueur emploie le contrôleur, la caméra, les collisions Rapier et
le contrat `forge_engine::player_motion_extension` du moteur. Le moteur ne lit
aucun état, nom ou type propre à la carte. Les caméras de capture utilisent le
marqueur générique `OffscreenSceneCamera`.

Les noms des exemples et leurs arguments sont conservés ; leur paquet Cargo a
changé :

```powershell
cargo run -p forge_tidewater --example tidewater_plant_probe --locked --offline -- <arguments>
cargo run -p forge_tidewater --example generic_scene_render_probe --features qualification --locked --offline -- output/optical-proof
```

Les notices des fichiers portés suivent ces fichiers lors de leur transfert.
Le texte de référence est dans [third_party/tidewater/LICENSE](../../third_party/tidewater/LICENSE).
Ces conditions concernent le composant correspondant ; elles n'ajoutent aucune
dépendance de carte au moteur. Les licences des dépendances génériques demeurent
propres à ces dépendances.

Le [guide des entrées](COMMON_SCENE_INPUTS.md) décrit les paramètres des ports.
L'[ancien README](PRE_SPLIT_ENGINE_README.md) conserve le détail historique des
projections ; ses commandes et emplacements antérieurs à la séparation sont datés.

Le noyau végétal est désormais dans `forge_engine::source_vegetation` :
layouts/attributs GPU, frame courante/précédente, matériau sous éclairage monde,
sélection, résident de buffers/entités et snapshot de draws/identités. Studio,
la référence et le résident de module appellent directement le même code.
Les anciens fichiers `scene/resident.rs` et `scene/resident/picking.rs` sont
retirés, sans façade de réexport. La préparation canonique, les bornes,
l'hôte `scene::streamed`, la publication, les textures et la dérivation du
shader sous éclairage monde sont également transférés au moteur. Le packing
et les descriptions du profil source appartiennent au codec commun
`forge_assets::source_vegetation_package`. Les identifiants, pins et gardes du
dataset y restent conservés. Le matériau sous éclairage capturé et son staging,
le buffer de vent, la lumière directe/SH et ses baux GPU, ainsi que les
récepteurs d'ombres appartiennent maintenant au moteur commun. Les appels de
la référence et du Studio utilisent directement ces API ; les anciens fichiers
et types de carte correspondants sont retirés. Cette crate conserve les
chargeurs de référence et les ports encore couplés. Le graphe de profondeur à
cascades, ses uploads et sa cadence sont dans `forge_engine::source_shadows::casters`.
La collecte d'éligibilité des casters de la carte reste ici ; les codecs fournissent
les flux masqués, prototypes et poses aux types et kernels GPU communs.
La sélection/résidence d'herbe et son raccordement au module sont
dans le moteur ; son activation canonique suit ADR-0042. Le matériau ID,
shader, passage hors écran et snapshot d'occlusion du picking sont aussi dans
le moteur. L'intégration fournit explicitement ses matériaux de terrain et de
surface aux API communes ; aucune façade de picking n'est conservée ici.
AO et flare consomment `forge_engine::view_depth`. Toutes les caméras temporaires
et leurs filtres emploient le marqueur commun `OffscreenSceneCamera` (ADR-0043).
Les qualifications Studio et de parité complète restent distinctes.

Le temps/vent de scène et l'horloge de rafales source résident dans
`forge_engine::source_wind`. La référence et le Studio appellent ce propriétaire,
comme l'hôte de modules importés. `scene_wind` conserve seulement l'acquisition
des phases source ; il ne réexporte pas l'implémentation du temps. L'ancien
fichier de vent sous le graphe de casters est retiré (ADR-0045).

`forge_engine::source_vegetation::scene::streamed` projette la population canonique d'un module
sur la sélection de cellules terrain déjà admise. Il réutilise le packing,
les attributs, paramètres et modèles du profil source vérifié. La préparation
de la scène de référence conserve ses gardes de six groupes / 65 383 instances ;
ce nombre ne limite pas la population auteur de cette projection de module.
Le Studio appelle aussi `forge_engine::source_vegetation::scene::from_population`,
qui utilise cette même préparation et accepte une petite population auteur.
Cette préparation CPU ne vaut pas publication GPU ou qualification visuelle.
Les identités, poses f64, retouches et ordre source sont épinglés avant staging.
La population canonique reste entière ; le rendu sélectionne les instances
avec `forge_engine::source_vegetation_selection`, partagé avec la référence.
Les recherches proche/lointaine, capacités, ordre source, distances et fades
du profil sont conservés. Cette sélection ne démontre pas une agrégation HLOD.

Le résident chauffe un enregistrement auteur réel par niveau et demande une
barrière `forge_engine::render_inventory`, y compris pour la première
génération. Il n'accepte aucune publication non vide depuis les seuls handles
CPU. La disponibilité finale exige une fence courante de l'inventaire réel.
Les sélections reprennent les mêmes modèles immuables et créent leurs buffers
d'instances, sans neutraliser les fades source. Le contexte source de lumière,
mouvement et ombres reste fourni par l'hôte.

La physique utilise `forge_engine::physics::imported_vegetation` indépendamment
de la caméra et des LOD. Le nombre de sources canoniques et celui des formes
résidentes sont observés séparément ; la disponibilité exige les formes et
poses requises dans Rapier ainsi que le retrait réel des anciens handles.
Les horloges d'observation de la référence et du jeu de modules sont dans
`forge_engine::{cpu_timing, render_timing}`. Les essais natifs de Gate 7 restent
distincts des performances release, de la qualification de tous les profils
et de l'extraction restante des adapters réutilisables vers le moteur.


Le matériau de surfaces et le décodeur de géométrie sont transférés à
`forge_engine::source_surface`, avec les paramètres d'échantillonnage sous l'eau
et le relais solaire. Les consommateurs emploient les types moteur directement,
sans réexportation des anciens types. `tidewater_material` ne garde que le raccord
des shaders acquis et des ressources encore intégrées ici ; les autres mécanismes
réutilisables restent à transférer. Voir ADR-0049 et le bilan Gate 7 associé.


Le cache de géométrie, la projection des bâtiments triangulés et la synchronisation
des collisions résident dans `forge_engine::source_surface::{geometry,objects}`.
`tidewater_assets::prepare_meshes` ne conserve que le décodage/admission du paquet
vers les parties moteur. Les consommateurs utilisent les types moteur directement ;
les anciens fichiers `tidewater_assets/scene.rs` et `placements.rs` sont retirés.
Les autres projections et l'acquisition explicite des objets restent à terminer.


Le lecteur du paquet de surfaces réside dans
`forge_assets::source_surface_package::SurfacePackage`. La préparation des géométries,
textures et paramètres de bibliothèque réside dans `forge_engine::source_surface::package`.
Les anciens propriétaires ne sont pas réexportés. Le raccord de matériau/éclairage
encore présent dans l'intégration et l'adoption des ressources du module restent
à poursuivre ; le nouveau contrat de transport ne prouve pas leur affichage.

## Native source objects in installed modules (26 October)

The ordinary module entry point resolves only the selected area's explicit API-7
object palette. Source placements use the engine's mesh cache, material constructor,
object projection and physics; the acquired reference supplies shader preparation
and its scene lighting bindings. Both consumers use the same parameter constructor.
The independent native replay `module-object-runtime-native-003` passed with two
source house placements, current GPU resources and inspected captures. Source
shaders remain detailed under HLOD until their baking route is qualified. This is
not full reference/Studio/performance qualification; Gate 7 remains open.


## Environment preparation uses the engine (26 October)

Environment preparation, atmosphere conversion, cloud acquisition replay and
environment-program decoding have moved to the common engine owners. Reference
probes and the independent module game now call those same owners, without
compatibility reexports from this crate. Acquired package bytes, provenance and
source-profile guards remain preserved. The independent native replay
`module-object-runtime-native-004` passes after the transfer, with both captures
inspected. The surface shader integration still has map-owned seams; full Studio
adoption and comparable performance measurements remain due. See central
`studio-module-resources-validation-001.json`.

## Studio adoption of the common hosts (26 October, studio-common-runtime)

The retained-origin module editor now calls the same engine environment, streamed
plant, object and placement-light hosts as the native module game. Native replay
`module-object-runtime-native-005` passes after the shared cell-admission correction,
with both captures inspected. Actual Tauri UI qualification covers source-house
editing, undo/redo, saved-state reopening and area isolation without reference assets.
The shader seams in `tidewater_material`, other reusable effects and full parity still
need extraction/qualification. See central `studio-common-runtime-validation-001.json`.


## Surface receiver owner transferred — UI replay pending (26 October)

Shader assembly, ground bounce and water-light receivers now belong to the engine.
`tidewater_material` and four map-owned receiver WGSL files are removed; callers use
the engine directly, with exact program bytes and notices preserved. Native replay
006 passes with inspected captures. Real Studio repetition first found a workspace
command-ID collision; the common host fix passes its regression, but its new UI
replay is still pending. See `shared-surface-receivers-checkpoint-001.json`.
Terrain/CDLOD ownership, other reusable effects and full parity remain to finish.


## Terrain/CDLOD and capture demand use common owners (26 October)

The old `tidewater_terrain` module, package decoder, tile-cache example module and
capture-demand module are removed. Reference consumers now call engine/asset owners.
The private AO and terrain assembly probes move with their implementation. No
compatibility export preserves the old terrain, caustic-input or capture-consumer API.
Native module replay 007 remains intact, and the source terrain renders in diagnostic
scene-lighting mode with an inspected capture. The existing fixed raster guards,
programs, notices and resident tile policy are preserved. Canonical module terrain
adoption and full parity remain pending. Central evidence:
`shared-terrain-owners-validation-001.json` (ADR-0053).

## Shared fish clock for authored copies (2 November)

Authored fish clones use a caller-owned engine clock; a time-only update no
longer rewrites each material and an unchanged clock emits no buffer asset
event. The pinned package replay and native/Web checks pass. The resident host,
group dormancy and installed-module integration still need common ownership and
runtime qualification. See `shared-fish-runtime-assessment-001.json` in the
central Gate 7 evidence campaign.

## Common fish-group dormancy (2 November)

The reference and authored fish call the engine's group bounds and dormancy
logic with the acquired profile's policy. Targeted tests cover wakeup, nearby
shadows, moved members and independent groups. The editor still has separate
lighting and stand paths, and no new GPU or performance parity claim follows.
See `shared-fish-group-assessment-001.json` in the central Gate 7 campaign.

## Authored fish residency belongs to the engine (2 November)

Tidewater validates its village fish IDs and content pin, then supplies
verified templates and its distance policy to `forge_engine::source_fish::resident`.
Studio uses the engine resident directly. An independent synthetic caller and
the real Tidewater package pass targeted checks; this does not qualify an
installed module, current GPU rendering or comparable performance. See
`shared-fish-residency-assessment-001.json` in the Gate 7 campaign.

## Parent-attached fish share the engine resident (2 November)

The same engine resident now handles fish attached to a moving parent, including
stand LOD, lighting, dormancy and caller-owned clocks and meshes. A real GameFish
package replay verifies five fish, three LODs each and propagated parent-relative
poses. This CPU replay is not a Studio UI or current GPU qualification; see
`shared-fish-attachments-assessment-001.json` in the Gate 7 campaign.
