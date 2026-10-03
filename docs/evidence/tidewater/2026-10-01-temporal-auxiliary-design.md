# Tidewater — conception des entrées GPU auxiliaires du TAAU

Date : 2026-10-01. Statut : **design-read-only**, aucune intégration ni qualification GPU.

Cette note formalise la lecture déjà effectuée de la conception 184, du module
purement CPU 187, des adapters Forge et du code primaire Bevy 0.19.1 installé.
La seule écriture de cette tranche est ce document. Aucun fichier Engine,
export `lib.rs`, système ECS, shader, build, serveur ou processus n'a été modifié
pour cette analyse. Les preuves GPU natives en cours ont un autre propriétaire.

La proposition est de produire les entrées fidèles du resolve sur le renderer
Bevy existant : vitesse RGBA16Float, masque RGBA8Unorm et profondeur finale après
eau. Ajouter des composants de prépass standard ne suffit pas à ces contrats.
Aucun fallback de vitesse caméra pour tous les objets ne doit permettre une
qualification complète.

## Sources locales effectivement inspectées

Clone source autoritatif :
`E:/OpenRPG_Forge/.tidewater-integration/upstream`, commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`.
Les références JS ci-dessous sont relatives à ce chemin local exact.

| Source et lignes vérifiées | Contrat constaté |
|---|---|
| `src/App.js:148` | `useStaticVelocity(this.vegetation.group)` pour toute la végétation. |
| `src/App.js:272-273` | Même choix pour terrain et rochers. |
| `src/post/CameraVelocity.js:13-17,41-45` | Reprojection d'un même P avec les VP courante/précédente sans jitter ; flag récursif. |
| `src/engine/render/MeshRenderer.js:109` | La matrice précédente objet est la matrice courante si `staticVelocity`. |
| `src/engine/render/MeshShader.js:174-195` | Déformation courante, choix de la position précédente, puis projections sans jitter. |
| `src/engine/render/MeshShader.js:306-331` | Vitesse UV current-minus-previous, Y vers le bas ; sorties tardives prémultipliées par couverture/poids. |
| `src/engine/render/MeshRenderer.js:348-351` | Vitesse tardive avec blend prémultiplié ; masque avec blend normal dans toutes les phases. |
| `src/engine/render/SceneRenderer.js:24-32` | Opaques/ciel, copie opaque pour réfraction, hulls, puis eau/transparents dans les mêmes MRT. |
| `src/ocean/WaterMaterial.js:51-63,559-561` | Vitesse caméra seule de la surface déformée courante ; eau opaque, profondeur écrite, masque `(seenFromBelow,1,0,1)`. |
| `src/world/reef/ReefMaterials.js:102-103,207-222` | Cas distinct : surge évalué à `frame.time` et `frame.time-frame.dt`, position précédente explicitement fournie. |

Autres sources déjà inspectées : `src/engine/render/Frame.js`,
`src/post/PostFX.js`, `src/post/TemporalUpscale.js`, `src/sky/Sky.js`.
La conception détaillée précédente est conservée dans
`E:/OpenRPG_Forge/.tidewater-integration/tidewater-taau-next-design-184.md`.
Les shaders Forge acquis réellement inspectés sont :

- `E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-clear-004/scene/plant.wgsl` ;
- `E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-clear-004/scene/terrain-lod.wgsl` ;
- `E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-clear-004/scene/water-optics.wgsl` ;
- `E:/OpenRPG_Forge/.tidewater-integration/preview-scene-bundle-clear-004/buildings/village.wgsl`.

La notice MIT et l'attribution DRG Software Solutions LLC accompagnent les
adaptations source ; les notices Bevy restent distinctes pour les éléments repris
de ses shaders.

## Mouvement source et objets Forge édités

Pour les arbres et l'herbe de 1438, le shader doit dessiner les vrais sommets
après `sourceVertex`, avec le vent courant, l'impostor ou la lame d'herbe,
et conserver les discards de `sourceSurface`. La position déformée courante est
reprojetée avec la VP précédente sans jitter. Réévaluer le vent à `t-dt`
changerait ce contrat source. Le terrain CDLOD et l'eau suivent le même principe :
reprojeter le P final courant, et non la grille brute ou un ancien déplacement FFT.
Le ciel doit reprojeter une direction homogène W=0, sans translation de caméra.

Cela ne couvre pas automatiquement les mouvements d'objets Forge. Pour un mesh
dont le `GlobalTransform` change réellement, utiliser la matrice précédente
maintenue par Bevy, via `MeshUniform.previous_world_from_local` et
`mesh_functions::get_previous_world_from_local`. Un mode de déformation dynamique
Forge peut conserver sa pose précédente complète ; il doit rester explicite et
distinct du mode source des plantes/eau. Les ports reef, poissons, skinning ou
autres matériaux ayant une vraie position précédente doivent conserver leur
contrat propre, notamment le `time-dt` vérifié pour ReefMaterials.

**Risque principal d'éditabilité :** les plantes actuelles ne placent pas leurs
sommets par le `GlobalTransform` ECS. Dans
`crates/forge_engine/src/tidewater_plants/scene/resident.rs:47-58`, les matrices et
records sont des `ShaderBuffer` du matériau. Les entities créées aux lignes
131-145 portent `Transform::IDENTITY`, un `MeshTag` et éventuellement l'UUID
`ImportedPlant`. Dans `plant.wgsl:770-782`, le vertex lit
`sourceModels[mesh_functions::get_tag(instance)]` et `sourceInstances[tag]`.
La matrice précédente ECS seule ne détecte donc pas une édition de ces buffers.

Il faut suivre les données dérivées précédentes par UUID stable, puis aligner les
anciens models/records avec les indices du buffer courant. Une regénération
discontinue peut demander un reset explicite d'historique. Un ajout ou une
suppression ne doit jamais emprunter la pose d'un autre index devenu voisin.
Ces caches sont dérivés ; les documents auteur et leurs placements restent la
source de vérité. La preuve doit inclure un arbre effectivement déplacé dans
Studio, caméra immobile, et pas seulement une caméra passant devant des arbres
immobiles.

## Masque et profondeur finale

La source écrit les opaques/ciel puis l'eau/transparents dans le MRT de scène.
L'eau écrit sa profondeur même si elle est dessinée dans la phase tardive. Le
resolve consomme la profondeur **finale après ces draws**. La copie opaque pour
réfraction et AO reste une autre entrée.

La vitesse opaque est `(velocity.xy,0,1)` ; la vitesse tardive est prémultipliée
par couverture et `velocityWeight`. Le masque est mélangé avec le blend normal.
Un transparent à masque zéro/alpha zéro conserve celui derrière lui. Il ne faut
donc pas remplacer le contrat par une règle « tout transparent efface l'eau ».
La profondeur, la couverture alpha, les discards shoreline/hull et la face
réellement visible doivent décider du masque, pas la présence d'eau au sol ou le
seul état `camera_underwater`.

Forge conserve déjà une observation de la surface eau dans l'alpha HDR :
`crates/forge_engine/src/tidewater_underwater.rs:54` adapte le fragment pour écrire
0,25 côté air et 0,75 côté eau, à partir du `mask.x` de `shadeWater`.
`tidewater_underwater.wgsl:48-56` lit ce raster sans interpolation. Cette entrée
peut servir de contrôle indépendant des nouveaux pixels eau ; elle ne remplace
pas le MRT RGBA8 source complet avec les blends tardifs.

La copie actuelle de `tidewater_ocean_depth.rs:45-47` est après les opaques et
avant les transmissifs. Elle doit rester opaqueDepth. La copie TAAU distincte
doit lire `ViewDepthTexture` après les passes main eau/transparents et être
comparée aux surfaces auxiliaires réellement rendues.

## Raccordement proposé : douze fichiers principaux

Tous les paths de ce tableau sont relatifs à
`E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/`. « Nouveau » signifie
proposé, pas créé ni installé par cette note.

| Fichier | Responsabilité proposée |
|---|---|
| `src/tidewater_temporal_frame.rs` | Réutiliser Halton32, VP précédente sans jitter, inverse VP précédente jitterée, seed/resize et commit après encodage. |
| Nouveau `src/tidewater_temporal.rs` | Plugin partagé, activation par caméra principale ; exclusion des vues de réfraction/capture. |
| Nouveau `src/tidewater_temporal/frame.rs` | Packet cohérent après propagation des transforms, avant synchronisation réfraction ; acknowledgement de l'image encodée et propriétaire unique de phase. |
| Nouveau `src/tidewater_temporal/targets.rs` | Velocity RGBA16Float, masque RGBA8Unorm, depth auxiliaire Depth32Float, final-depth et previous-depth distinctes ; dimensions internes explicites. |
| Nouveau `src/tidewater_temporal/phase.rs` | Phases auxiliaires opaques puis eau/transparents, sur les mêmes meshes, visibilités, ordre et bindings matériels que le rendu principal. |
| Nouveau `src/tidewater_temporal/shaders.rs` | Dérivation stricte des shaders acquis, conservation des fonctions vertex/surface et discards ; entrypoints auxiliaires vitesse/masque. |
| `src/tidewater_terrain/lod.rs` | Adapter CDLOD et corrections éditées ; reprojeter le P final morphé, pas la grille brute. |
| `src/tidewater_plants.rs` | Adapter les quatre PLANT_KIND, impostors et herbe ; mêmes sourceVertex/sourceSurface, alpha, dither et LOD. |
| `src/tidewater_plants/scene/resident.rs` | Suivi dérivé des poses/records par UUID lors des éditions/regénérations ; ne pas confondre indices de buffer et identités auteur. |
| `src/tidewater_material.rs` | Village, tissus, drapeaux, filets et verre : mêmes sommets/discards, modèle précédent réel, couverture tardive et velocityWeight source. |
| `src/tidewater_ocean_optics.rs` | Même waterSurfaceVertex/shadeWater, FFT/shore/bindings/discards ; vitesse caméra seule et masque source exact. |
| `src/tidewater_underwater.rs` | Ordre du beauty HDR et lecture du masque avant écrasement par postpasses ; branchement du futur resolve. |

Les entrypoints auxiliaires doivent réutiliser les fonctions présentes dans les
shaders acquis. Un premier chemin eau qui appelle le même `shadeWater` préserve
les discards ; son coût doit être mesuré avant une extraction optimisée des seules
fonctions de couverture. Une optimisation future doit garder une comparaison
de silhouettes, pas seulement des positions CPU.

## Interfaces publiques Bevy vérifiées

Racine exacte du code primaire installé :
`C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/`.
Les paths de ce tableau sont relatifs à cette racine.

| Code Bevy 0.19.1 et lignes vérifiées | Interface utilisable |
|---|---|
| `bevy_pbr-0.19.1/src/material.rs:193,208,217,273` | Material::enable_prepass, prepass_vertex_shader, prepass_fragment_shader, specialize ; équivalents MaterialExtension. |
| `bevy_pbr-0.19.1/src/material.rs:443,510-515,520` | MaterialPipeline avec MeshPipeline ; DrawMaterial et commandes publiques de binding/draw. |
| `bevy_pbr-0.19.1/src/material.rs:571,600,1442-1444` | RenderMaterialInstances, RenderMaterialInstance, PreparedMaterial et properties publiques. |
| `bevy_material-0.19.1/src/lib.rs:53,76,83,100,108` | MaterialProperties, layout matériel, shaders et accès aux shaders ; user_specialize est également public. |
| `bevy_pbr-0.19.1/src/render/mesh.rs:510,518` | Données previous_world_from_local dans les uniforms mesh. |
| `bevy_pbr-0.19.1/src/render/mesh_functions.wgsl:21-22` | get_previous_world_from_local(instance_index). |
| `bevy_pbr-0.19.1/src/prepass/mod.rs:216,675` | PreviousGlobalTransform et préparation des uniforms de vue précédente. |
| `bevy_core_pipeline-0.19.1/src/prepass/mod.rs:59,78` | Motion target standard RG16Float et composant MotionVectorPrepass. |

Également inspectés : `bevy_core_pipeline-0.19.1/src/schedule.rs` pour Core3d et
Core3dSystems ; `core_3d/mod.rs` pour phases et ordre main ;
`core_3d/main_transparent_pass_3d_node.rs` pour chargement de la profondeur ;
`bevy_render-0.19.1/src/view/mod.rs` pour ViewDepthTexture.texture/view,
ViewTarget::post_process_write, préparation des vues et textures HDR.
RenderContext, TextureCache, PipelineCache, RenderVisibleEntities,
RenderMeshInstances, RenderAssets<RenderMesh> et MeshAllocator sont réutilisables
sur le device Bevy actuel.

Limite concrète : `PrepassPipeline::specialize` est privé ; le motion prepass
standard écrit RG16Float, sans masque. Les hooks publics de Material permettent
des vertex/fragment custom, mais leur activation seule ne crée pas les MRT source
ni la phase eau finale. Le shader Forge `source_fragment_prepass.wgsl` écrit déjà
des motions Bevy pour son ExtendedMaterial à vertex standard ; il ne couvre pas
les quatre shaders Tidewater à vertex propre.

Une phase auxiliaire propre peut dériver son pipeline de MeshPipeline::specialize
et de la spécialisation publique du matériau existant, puis remplacer les
entrypoints et targets en conservant les layouts de sommets et bindings. Elle ne
doit pas dépendre d'un constructeur privé de MaterialPipelineSpecializer.
Pour rester dans quatre bind groups sur WebGPU, conserver 0 pour la vue,
2 pour le mesh, 3 pour le matériau, et employer 1 pour le packet temporel dans
les entrypoints auxiliaires de géométrie/couverture. Une commande dédiée remplace
alors SetMeshViewBindingArrayBindGroup<1>. Vérifier d'abord qu'aucune fonction
accessible de ces entrypoints n'utilise le binding-array original ; ce point est
une condition technique à tester, pas une compatibilité déjà qualifiée.

## Ordre GPU et limites actuelles

Le packet caméra/simulation doit être cohérent pour le dessin principal, la
phase auxiliaire et la réfraction. La phase Halton ne doit pas avancer une seconde
fois pour une caméra dérivée. Les previous poses/records correspondent à l'image
précédemment encodée ; elles ne sont pas reconstruites à partir d'un FPS estimé.

Ordre visé : terminer les draws main, dont eau/transparents ; copier la profondeur
finale ; composer le beauty HDR source (AO, haze/shafts, underwater/waterline) ;
effectuer le resolve TAAU ; copier previous-depth ; committer le frame. Le
resolve est avant bloom, exposition/tonemapping et effets de lentille.

L'ordre Forge actuel n'est pas celui de la source :
`tidewater_gtao.rs:89-91` composite après CAS/tonemapping, tandis que
`tidewater_air_haze.rs:67-69` est après PostProcess. Underwater est déjà après
MainPass et avant EarlyPostProcess (`tidewater_underwater.rs:435-436`).
Le déplacement AO/haze dans le beauty HDR est une tranche candidate distincte,
à qualifier après les entrées GPU. Ne pas masquer ces différences en recalibrant
artistiquement l'exposition.

La projection Bevy à far infini et la projection source finie 60000 ne permettent
pas une comparaison naïve des profondeurs standard. La conception 184 décrit le
besoin de reconstruction/conversion commune. Les tailles internes 0,5–1,
l'historique à taille de sortie, les viewports et le demi-target de réfraction
doivent également rester cohérents. Une première preuve à scale1 ne réduit pas
le scope final du TAAU.

## État CPU 187 et preuves GPU encore nécessaires

Le module `src/tidewater_temporal_frame.rs` a **cinq tests CPU PASS** dans un
harness isolé avec les vrais types Glam 0.32.1 réexportés par BevyMath : phases
Halton, prepare/commit unique, matrices de mouvement sans jitter, demi-jitter
à taille impaire, resize/reset et rejets d'entrées invalides. Les résultats et
la provenance des types sont dans
`E:/OpenRPG_Forge/.tidewater-integration/temporal-frame-cpu-187/`.
Ces résultats ne sont pas un export Cargo/Engine ni une preuve d'intégration :
aucun export lib.rs, système ECS, target, pipeline ou mouvement GPU n'est installé
par ce module à ce stade.

La première preuve des entrées doit exécuter **Native et Studio WebGPU** avec
source, caméra, phase, résolution, simulation/FFT/vent et paramètres appariés :

- Readbacks réels RGBA16F, RGBA8 et Depth32F : dimensions/strides/empreintes,
  finitude, compteur de frame et surface visible vérifiés. Contrôle du final-depth
  main et du depth auxiliaire, feuilles alpha, terrain, eau dessus/dessous,
  shoreline, piles et ciel.
- Caméra immobile puis translation/rotation : UV current-minus-previous mesurés
  sur pixels rendus et reprojection indépendante. Vent/FFT animés avec caméra
  immobile doivent conserver le contrat source caméra seule des plantes/eau.
- Objet réellement déplacé, notamment arbre placé dans Studio par UUID,
  ajout/suppression/regénération et changement d'index : précédent modèle/record
  correct, aucun emprunt de pose d'un autre objet, reset explicite si nécessaire.
- Transparents et verre : couverture prémultipliée, velocityWeight, blend de
  masque source et profondeur finale après eau ; mêmes discards que le main.
- Édition/playtest, changement de caméra, resize impair, hide/resume, absence de
  contamination par caméra de capture ; mêmes preuves de nage, ombres et
  réfraction conservées.
- Pour le resolve ultérieur : séquences HDR/thin features/désocclusion/eau/noir,
  comparaison temporelle source, conservation des détails, absence de traînées,
  niveaux 1/0,75/0,5, coût CPU/GPU et mémoire séparés.

Les matériaux visibles non adaptés doivent être recensés et signalés. Un
fallback caméra général, des seules matrices CPU, des captures à clocks
différentes ou la compilation des pipelines ne suffisent pas à déclarer les
entrées complètes ni le TAAU fidèle. Cette note reste une conception vérifiée en
lecture seule ; toute intégration et toute preuve GPU sont encore à réaliser.
