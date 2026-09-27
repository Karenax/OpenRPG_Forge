# Forêt lointaine : cause du blocage et contraintes vérifiées

## 9 septembre — limite du regroupement proche sur le kit courant

Après comparaison dev/release (`R1_RELEASE_PREPARATION.md`), inspection directe
des deux GLB du kit colorimétrique courant, sans conversion ni rendu supplémentaire.
Chaque arbre/LOD utilise quatre primitives visuelles : bois opaque et trois
matériaux de feuillage BLEND distincts. Les meshes de collision sont séparés et
ne doivent pas être inclus dans ces décomptes visuels.

| Prototype | Triangles LOD0 bois + feuillage | LOD1 | LOD2 |
| --- | ---: | ---: | ---: |
| chêne | 5038 + 29120 | 690 + 7462 | 116 + 546 |
| conifère | 14836 + 31672 | 1530 + 5992 | 372 + 856 |

Valeurs lues dans les accessors d'indices, pas triangles GPU effectivement
dessinés. GLB chêne `7d33ecbdc2e3c022fb93e1e465d125f6e119316c1e886c470bcfc6cfb455e213`,
conifère `6ebab9a71b7c9cc0ffb734491f037284f06887f994a25315510b4a07214a9ba9`.
Les trois variantes feuille ont chacune des images couleur/normale/roughness
distinctes par SHA-256 des octets embarqués. Il n'existe pas de primitive feuille
redondante de même matériau à fusionner simplement dans ces LOD. Les trois images
d'écorce sont communes aux deux espèces, pas aux variantes de feuillage ; ce petit
doublon ne justifie pas une optimisation de cadence à lui seul.

Le chemin proche/moyen partage déjà les handles de prototypes. Généraliser la
fusion géométrique des lots lointains multiplierait les sommets détaillés par les
placements et modifierait l'unité de tri BLEND. Ne pas le faire pour promettre
une réduction gratuite des soumissions : mémoire, vent, LOD et transparence doivent
rester corrects. Aucun code de batching proche ni nouveau dérivé introduit.
Une consolidation de matériaux demanderait un vrai dérivé conservant les trois
jeux de textures et une revue de transparence ; ce n'est pas une déduplication
de matériaux identiques. Cette inspection n'apporte aucun gain FPS ou passage de gate.

## Historique du 7 septembre

7 septembre 2026 — inspection du code courant Rust, sans modification du renderer.

## Cause actuelle

`global_quality/effective.rs::observe_capabilities` (branche végétation) refuse
tout `prototype_preview` ou `prototype_preview_error`. Ce refus est volontaire et
inconditionnel : améliorer les compteurs seuls ne peut pas lever le blocker.
Il ne constitue pas une détection dynamique d'un défaut GPU ou de performance.

`vegetation/prototype_projection.rs::prepare` émet encore une entité par
placement/LOD/primitive. Il retire les arbres des HLOD procéduraux, en gardant
arbustes/fougères, puis utilise les primitives importées LOD2 à la distance HLOD.
Il n'existe donc pas de regroupement HLOD des arbres gouvernés dans ce chemin.

## Pourquoi une fusion brute serait incorrecte

Inspection de `shaders/visual_valley_vegetation_deform.wgsl` :

- phase et réponse au vent proviennent du `MeshTag` de chaque instance ;
- le poids de flexion dépend de la hauteur locale depuis la racine normalisée ;
- l'amplitude conserve l'échelle de l'instance ;
- le dither du prototype utilise l'origine de cette instance ;
- UV0, UV1, couleurs et alpha restent des données PBR authorées ; contrairement
  aux HLOD procéduraux, leurs UV ne peuvent pas encoder une racine ;
- le prépass de mouvement réutilise ces paramètres avec le temps précédent.

La fusion sous une seule transformation et un seul tag changerait donc au moins
vent, ancrage et transitions. Elle dupliquerait aussi la géométrie aujourd'hui
partagée : moins d'entités n'implique ni moins de triangles ni moins de mémoire.
Les 30 468 104 triangles toutes instances/LOD de la télémétrie High ne sont pas
les triangles effectivement dessinés ; ne pas les présenter comme coût GPU.

## Suite d'implémentation, non réalisée par cette inspection

Le chemin regroupé doit disposer de données dérivées par placement distinctes des
UV/couleurs authorées : racine, échelle, phase/réponse et poids de flexion. Les
passes couleur et mouvement doivent employer le même contrat. Le regroupement
doit respecter matériau, chunk et bornes de déformation, conserver sources,
colliders et plages LOD, puis être comparé au chemin individuel sur mêmes poses.
Un simple regroupement sans réduction géométrique n'est pas une qualification HLOD.

Ne retirer le refus global qu'après remplacement par des observations courantes
du contrat complet et preuves natif/Web, avec refus sur données manquantes ou erreur.
ADR-0011 n'accorde aucune exception forêt. Aucun gain, shader nouveau, regroupement
implémenté ou passage de Gate 1 n'est revendiqué ici.

## Première implémentation CPU ultérieure

`vegetation_far_geometry::build_far_geometry` prépare maintenant une géométrie
fusionnée à partir de primitives normalisées, sans écrire dans les assets sources
ou dans l'ECS. Prévalidation de la topologie, des indices, des layouts identiques,
des transformations yaw/échelle uniforme positives et finies, et budgets de
1 000 000 sommets / 6 000 000 indices. Les indices sont normalisés U32. Les UV et
couleurs restent intactes ; deux attributs distincts portent racine/échelle et
poids/phase/réponse au vent. La phase 24 bits et la réponse 8 bits sont décodées
séparément comme dans le WGSL courant, sans conversion flottante du tag entier.

Ce helper attend des primitives statiques normalisées et regroupées par le futur
appelant dans un même chunk/matériau. Il n'est pas un importeur général ni un
réducteur de triangles. Il n'est pas raccordé au shader ou au projecteur : aucun
changement du candidat affiché, aucune allocation GPU et aucune capacité promue.

Deux tests synthétiques vérifient conservation UV1/couleurs, racines et tags de
deux instances, sources intactes, indices fusionnés, refus de layouts/indices/poses
invalides et entrée vide. Passage workspace : **572 PASS / 1 timing ignoré**.
Clippy strict natif/Web PASS après correction de notation/comparaisons de tests ;
premier échec conservé. Extraction de la prévalidation en fonction séparée après
le passage workspace ; les deux tests ciblés finaux passent également, sortie 0
consignée dans le log dédié. `cargo fmt --all --check` et `git diff --check` passent.

Artefacts externes sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907` :
`Validate-Far-Geometry.ps1`, `far-geometry-workspace.log`,
`far-geometry-clippy.log` (échec), `far-geometry-clippy-v2.log`,
`far-geometry-wasm.log`, `far-geometry-targeted-final.log`.
Raccord shader, groupement, bornes, simplification et preuves GPU restent à réaliser.

### Raccord shader commencé ensuite — non qualifié

La spécialisation du matériau transmet les deux attributs aux locations 10/11
uniquement si le mesh porte le payload. La présence d'un seul attribut est refusée
par la construction de layout exigeant les deux. Le buffer standard conserve ses
attributs, y compris les locations différentes du prépass Bevy.
Le chemin WGSL `FORGE_FAR_BATCH` emploie poids/phase/réponse, racine et échelle par
arbre, dans les passes courante/précédente et le dither lointain. Le chemin individuel
continue à décoder le tag ; la fonction de déformation reçoit désormais ces valeurs.

`cargo check -p forge_engine` et `cargo clippy -p forge_engine --all-targets
--all-features -- -D warnings` : sorties 0. Le premier check a échoué sur le namespace
de `SpecializedMeshPipelineError`, puis a été corrigé vers `material::specialize`.
Ces commandes ne compilent pas les variantes WGSL sur GPU. Pas de nouvel acquittement,
de qualification optique, de preuve Web ou de passage workspace attribué à ce raccord.
Le projecteur ne crée pas encore de lot utilisant ces attributs. Groupement/bornes,
réduction géométrique et qualification des deux variantes graphiques restent ouverts.

### Projecteur et premier smoke Web ultérieurs

`GovernedFarBatchPreview` active maintenant un projecteur de lots par chunk/matériau,
après la projection individuelle. Préparation fallible avant remplacement ; racines
canoniques, matériaux et colliders conservés. Les dessins LOD2 sont retirés, pas leurs
entités sources ; les autres LOD restent individuels. Le projecteur retire uniquement
ses propres meshes lors d'un remplacement. L'erreur explicite restaure le chemin
procédural via la récupération existante. Pas de hot-reload/removal qualifié.

Les bornes incluent la déformation maximale multipliée par l'échelle. Les plages CPU
sont élargies par le rayon des racines, avec origine de chunk et marge non abrupte
pour conserver le varying de dither ; le shader filtre ensuite par racine réelle.
La télémétrie inclut les lots dans le compartiment lointain sans les confondre avec
les bindings procéduraux du réconciliateur qualité.

Studio accepte `forge_kit=governed&forge_forest=batches`, sans changer son lien/menu
par défaut. Le test de parsing refuse ce mode sans kit. Test ECS : regroupement
de 636 arbres, seuls les LOD2 perdent leur mesh individuel, poses conservées,
et erreur injectée avec restauration. Premier test échoué sur emprunts mutables
de fixture, corrigé ; premier Clippy échoué sur longueur de la transaction, exception
locale justifiée pour conserver préparation/application/récupération ensemble.
**573 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict natif/Web,
build Web et contrôles fmt/diff PASS. Aucun test désactivé.

Smoke Chrome, même bundle/caméra fixe/source, modes batches puis preview :

| Compteur CPU | Lots | Individuel |
|---|---:|---:|
| Entités mesh | 7738 | 10026 |
| Assets mesh uniques | 394 | 142 |
| Triangles uniques | 2549914 | 1907294 |
| Triangles toutes instances/LOD | 30468104 | 30468104 |
| Triangles visibles CPU | 880190 | 811374 |

Le différentiel correspond à 2544 primitives LOD2 remplacées par 256 lots. Les
compteurs visibles CPU plus élevés reflètent le culling conservateur du lot, pas
une mesure des triangles après dither GPU. Aucun mesh non comptable, blocker
`vegetation` conservé, readiness false. Deux PNG 1408×741 inspectés : silhouettes,
terrain et eau présents, aspect proche sur cette pose ; pas de preuve pixel exacte,
de vent animé, de transition ou d'équivalence artistique. Logs navigateur sans erreur.
Un timeout initial de lecture pendant chargement est suivi d'une lecture réussie
du même onglet ; aucun redémarrage déduit du timeout.

Bundle `web-far-batches`, WASM SHA-256
`6A3CE297174E640C3D8EADC5C335AE2BE9E5137B19840CEC4023B347BD09BE69`.
Sous la même racine externe : `Validate-Far-Batches.ps1`, logs `far-batches-*`,
`Collect-Far-Batches.ps1` exécuté PASS et copies hash-vérifiées dans
`far-batches-smoke/`. Lots : PNG `1788778883185`, JSON `1788778890761` ; individuel :
PNG `1788778949353`, JSON `1788778959659` (préfixes habituels forge-viewport/diagnostic).
Le script contrôle sources, compteurs, association des captures et réserves conservées.

Le regroupement augmente la géométrie unique et ne réduit pas les triangles : aucun
gain FPS/mémoire ni HLOD optimisé revendiqué. Qualification dynamique/native,
transitions, réduction géométrique et coût réel restent ouverts. Pas de Gate 1 passée.

### Cycle Web High → Low → High

Rejeu par les boutons du même onglet `forge_forest=batches`, sans rebuild : Low
projette 6 619 entités mesh, 394 assets uniques, 927 394 triangles uniques et
429 260 triangles visibles CPU. High restaure exactement les six compteurs
`live_meshes` initiaux (dont 7 738 entités et 880 190 triangles visibles CPU).
Sources constantes, aucun mesh non comptable, logs navigateur sans erreur.
Le blocker végétation et readiness false restent présents aux trois étapes.

`Collect-Far-Batch-Cycle.ps1` exécuté PASS, copies hash-vérifiées dans
`far-batch-cycle/` sous la racine externe précitée. Diagnostics :
`1788778890761` (High initial du smoke), `1788779137390` (Low),
`1788779169280` (High restauré). Aucun PNG répété ni benchmark lancé pour ce relevé.
Le temps d'un appel d'automatisation n'est pas une durée de transition renderer.
Reconstruction fonctionnelle observée, fluidité, vent animé et fuite mémoire non
qualifiés par ce seul cycle. Les meshes de lots sont encore reconstruits à chaque
changement de profil : leur coût reste à mesurer avant d'optimiser ou promouvoir.

### Scène Web non figée — smoke, vent lointain non isolé

Même bundle, URL sans `forge_view` ni candidat caméra, avec `forge_forest=batches`.
Trois captures 1408×797 ouvertes et inspectées : pose de l'herbe différente entre
les instants, joueur et cadrage visuellement stables ; arbres présents. Les trois
fichiers ont des hashes différents. Ce constat n'isole pas le vent des primitives
lointaines : ni ancrage exact GPU, ni amplitude, ni fluidité de ces arbres qualifiés.
Pas de benchmark navigateur déduit des intervalles entre les appels d'automatisation.

Diagnostics initial/final : source hashes identiques, une caméra active, observation
végétation 34 → 99 frames, 7738 entités mesh et kit 20/20 avec 36 colliders prêts.
`fixed_view=null`, aucun geste de déplacement envoyé, logs navigateur sans erreur.
La stabilité numérique de la pose caméra n'est pas prouvée par cet export : les
images et l'absence d'entrée sont des observations, pas un verrou caméra instrumenté.

`Collect-Far-Animated-Smoke.ps1` exécuté PASS ; copies hash-vérifiées sous la même
racine externe, dossier `far-animated-smoke/`. PNG : `1788780148084`, `1788780173997`,
`1788780196336` ; JSON : `1788780160724`, `1788780210626`. Aucune nouvelle compilation.
La prochaine preuve de vent doit isoler les arbres lointains à caméra/temps contrôlés,
pas répéter ce smoke global et le transformer artificiellement en qualification.

## Reprise du diagnostic sur les GLB déployés — 7 septembre

Le regroupement conserve les triangles de son entrée LOD2 ; cela ne signifie pas
que cette entrée est dépourvue de simplification. Contrôle en lecture seule des
deux GLB de `r1-conifer-sprays-20260907/runtime/assets/`, sous la racine externe
`E:/OpenRPG_Forge/.gate1-evidence-runtime/` : hashes identiques au manifeste du kit
rev.4 **et** aux deux bindings du catalogue v2. En-têtes GLB v2/longueur/chunk JSON
contrôlés ; primitives TRIANGLES indexées, comptes d'indices divisibles par trois.
Somme des accessors d'indices par meshes Wood/FoliageCandidate portant le même LOD :

| Prototype déployé | LOD0 | LOD1 | LOD2 |
|---|---:|---:|---:|
| Chêne | 34 158 | 8 152 | 662 |
| Pin | 46 508 | 7 522 | 1 228 |

Colliders nommés `Cylindre*` exclus des totaux visuels. Les résultats concordent
avec `R1_WOOD_LOD_DERIVATIVE.md` et `R1_CONIFER_SPRAYS.md`, pas seulement avec une
intention de recette. Hash chêne :
`be9abd360ef86af4a08fc97573ba7b107998657da228926b47508cd406ba63ee` ; pin :
`edda28bb2dcafb23cddb7b769ef93014949b5ec9d17c332e244277a6d37e34e9`.

Ce relevé compte la topologie déclarée des meshes, pas les triangles dessinés GPU,
la couverture alpha ou le coût par frame. Il ne remplace ni le validateur glTF,
ni un réimport, ni un contrôle de tous les indices/binaires ; aucun de ces contrôles
n'a été rejoué ici. Aucun asset, shader, prototype ou source authorée modifié.

**Conséquence pour la reprise** : ne pas traiter « regroupement sans réduction »
comme « aucun LOD réduit disponible », ni imposer une nouvelle réduction avant toute
qualification. La priorité forêt reste la continuité/silhouette/vent du LOD2 actuel
aux distances de jeu, puis son coût dans le candidat complet selon ADR-0010.
Une réduction supplémentaire devra être motivée par cette qualification, pas par
le seul nombre de lots. Le statut Unqualified, la revue humaine, le matériel cible
et les critères Gate 1 restent inchangés. Pas de nouveau build ou capture.

## Contrat racine/fondu relu — 7 septembre

Inspection du code Forge courant et de `bevy_pbr-0.19.1` installé, sans modification
du renderer : les primitives individuelles utilisent la translation de placement,
le regroupement soustrait le centre du chunk puis le shader le réintroduit pour
retrouver la racine. Le rayon conservatif du batch élargit son admission CPU ;
le fondu final reste par racine dans `hlod_placement_dither`.

Les marges proviennent du même `VegetationLodRanges` : 64–72 et 172–180 m dans
le profil High courant. Le fondu entrant Forge utilise `round(fraction*16)-16`,
le sortant Bevy `round(fraction*16)` dans la bande. Vérification arithmétique
exhaustive des 17 niveaux et 16 seuils du masque : **272 cas complémentaires**, ni
trou ni recouvrement dans ce contrat discret à seuil/coordonnée écran identiques.
Sortie `DITHER_SIGNED_PARTITION_PASS cases=272 scope=integer_contract_not_gpu`.

Ce contrôle n'évalue pas la différence de silhouettes entre meshes, les écarts
flottants aux demi-niveaux, l'occlusion, le vent ni les passes réellement dessinées.
Les tests existants de marges et racines ont été relus, pas réexécutés dans ce lot
d'inspection. Aucun gain visuel/performance ou qualification GPU revendiqué.

Conclusion bornée : aucun décalage de racine ou de convention de fondu établi par
cette inspection ; ne pas réécrire ce mécanisme ni relancer le même audit sans
nouvel échec. La suite doit porter sur l'observation artistique des silhouettes
et du mouvement, pas sur une correction présumée du contrat de tramage.

SHA-256 des sources inspectées : shader
`2b3747d3270e84d306dbb76338a582d5fd91c47fd7f00c52c6eee6671ec0a3a9`,
`far_batches.rs` `7747ad6ccf9f012b6a2621e177dc3a03b548937bc673e127a780c7287272e98f`,
`vegetation_far_geometry.rs` `4aa6529c7944e524d1df0b6ec20983af59eab119514ed1baf013c15e8e0379d4`.
