# Tidewater — projection Bevy de Debris et scans conservés

Suite de [l'acquisition et du lecteur Rust](2026-10-05-debris-inventory.md).
L'objectif complet reste inchangé ; aucun verdict de parité ni clôture de gate.

## Projection CPU Bevy

`crates/forge_engine/src/tidewater_debris.rs` prépare les 805 objets du paquet
intermédiaire dans des structures de prototypes séparées des documents auteur.
Les positions locales, normales, UV, tints de contact et paramètres source
passent par l'adaptateur de mesh commun déjà utilisé pour Village.
Les rôles wood/hard/fabric/nature restent explicites.

L'attribut `DebrisSourceRestPosition` (ID `0x7457_0020`) conserve les coordonnées
monde source pour contrôler la conversion et les phases éventuelles. Sa présence
ne constitue pas le raccord d'un shader animé. Le contrôle GPU ci-dessous établit
que les tissus de cette couche précise sont statiques dans la source. Les triangles d'ombre sont préparés
dans des meshes séparés ; fabric n'en reçoit pas et les petits objets Nature
ne sont pas promus en casters. Le staging des matériaux et des entités reste
à réaliser, sans remplacer les matériaux source par un rendu approximatif.

`cargo check -p forge_engine --locked` termine code 0 en 6 min 43 s sur le
worktree courant, avec Rust 1.98.0 explicitement fixé et cache natif existant.
Le module est formaté avec rustfmt 1.98.0. Cela prouve la compilation native
de l'adaptateur ; aucun upload, draw GPU ou geste Studio n'est prouvé.

Le test réel suivant est lancé après la fin du check et reste en construction
à cette étape (session de commande 93654, activité de compilation Bevy observée) :

```text
cargo test -p forge_engine --lib tidewater_debris --locked -- --include-ignored
```

`FORGE_DEBRIS_PACKAGE_FIXTURE` pointe sur `debris-frames-20261005-v3`.
Le test est prévu pour vérifier les 865 parties, 112 669 triangles principaux,
50 522 triangles d'ombre, identités et présence des attributs source. Il n'est
pas déclaré PASS avant sa terminaison. Aucun redémarrage du build n'est requis.
Clippy de l'adaptateur et compilation WASM moteur restent à exécuter.

## Scans : conservation source, pas conversion qualifiée

`scripts/tidewater/acquire-debris-scans.py` conserve les quatre GLB et les
douze JPEG 1024×1024, crédits et licence, depuis la référence propre épinglée.
L'ordre des assets est celui de ScannedDebris : trunk, branch_02, branch_01,
shell. Les 141 transformations d'instance et tailles déclarées restent liées
à l'inventaire initial. Les SHA des fichiers et du contrat ScannedDebris sont
contrôlés ; aucune source upstream n'est modifiée, aucun téléchargement.

Le contrôle structurel vérifie en-tête/chunks GLB 2.0, buffer embarqué,
absence de skins/animations et présence des noms de nodes pour les trois LOD.
Les images sont ouvertes et vérifiées comme JPEG 1024×1024. Ce contrôle ne
normalise pas la géométrie, ne vérifie pas tous les accessors, ne cuit pas les
textures dans des GLB autonomes et ne qualifie pas les pixels ou le rendu.

Les acquisitions v2/v3 terminent code 0 et leurs 19 fichiers (descripteur
compris) sont identiques. Reçu `debris-scans-20261005-repeat.json` : PASS
pour la conservation source et l'inspection structurelle seulement.
Les 18 fichiers source conservés représentent 8 396 336 octets. La v1,
antérieure à la vérification Git et au hash du contrat, reste une étape
intermédiaire conservée.

Les crédits déclarent Poly Haven CC0 ; cette déclaration n'est pas promue
en nouvelle qualification de redistribution. La conversion doit conserver
les transforms de nodes, l'ordre Euler source et les scales d'instance.
Restent atlas/mips, politique LOD/culling/crossfade, shader scanné, édition
générique des instances et qualification native/WebGPU dans la scène complète.

## Géométries des scans et transformations acquises

`scripts/tidewater/export-debris-scan-geometry.mjs` appelle le parseur GLB
source audité sur les quatre fichiers conservés. Les transforms de nodes
appliqués par ce parseur sont conservés dans les positions/normales ; positions,
normales, UV originaux et indices sont retenus pour chacun des trois LOD.
Les UV d'atlas sont conservés séparément avec le clamp exact [0,002 ; 0,998]
et le placement des quatre tiles. Aucune cuisson ou qualification GLB autonome.

Les 141 instances conservent position, quaternion et matrice colonne-major,
avec la convention source `Euler(roll, yaw, pitch, 'YXZ')`, scale non uniforme
et rayon source. Le code n'instancie pas ScannedDebris et ne lance pas son
chargement réseau/asynchrone. Le device headless utilisé pour l'environnement
d'import est fermé explicitement en fin d'acquisition.

Deux acquisitions `debris-scan-geometry-20261005-v1` / `v2` terminent code 0.
Leurs 63 fichiers, descripteur compris, sont identiques. Le vérificateur
`scripts/tidewater/verify-debris-scan-geometry.py` réouvre les payloads,
vérifie SHA/tailles, dimensions/finitude, bornes d'indices, couverture des
12 couples asset/LOD, UV d'atlas exacts et fermeture des références. Il
reconstruit indépendamment chaque matrice avec Ry×Rx×Rz et les scales,
et vérifie normalisation des quaternions et rayons.

Reçu `debris-scan-geometry-20261005-repeat.json` : PASS dans cette portée CPU.

- 12 maillages, 141 matrices d'instance ;
- triangles par niveau, somme des quatre modèles : 10 598 / 1 999 / 478 ;
- écart matriciel maximal : 8,881784197001252e−16 ;
- 62 payloads, sans atlas pixel ou rendu acquis.

La vérification ne compare pas un second décodeur GLB indépendant ni les
pixels avec le navigateur. Atlas/mips, shader scanné, politique LOD/crossfade,
placements éditables et rendu de la scène entière restent à porter/qualifier.
Le test Bevy de la couche procédurale reste distinct de cette acquisition.

## Tissus Debris : programme source exécuté sur GPU

`scripts/tidewater/verify-debris-wind-gpu.mjs` lit les références adressées par
contenu du paquet `debris-frames-20261005-v3`, contrôle leurs SHA/tailles et exécute
le corps vertex Fabric acquis sans changement sur le device headless source.
Le clone source doit être propre et épinglé à 1438b1ab avant/après l'essai.
Les buffers utilisent les positions source, positions locales, pivots et vdata
réels des 966 sommets Fabric. Huit cas combinent les temps 0/1/4/124 secondes
et deux vents explicites : direction (1,0), vitesse 4 ; direction (0,6 ; 0,8),
vitesse 12. Ce sont des paramètres de sonde, pas un relevé du vent jouable.

Le résultat `debris-wind-gpu-20261005-v1/result.json` termine code 0 :

- aucun sommet n'a de poids de vent non nul ni de marqueur de drapeau ;
- aucun sommet ne se déplace sous ces huit cas dans le programme source ;
- restauration après passage aux pivots locaux : écart maximal 7,62939453125e-6 m ;
- outputs float32 et shader compute conservés avec empreintes dans le reçu.

Le premier essai de la sonde échouait parce qu'il exigeait un déplacement,
avant inspection des poids source. La condition corrigée vérifie maintenant
le comportement attendu d'après les poids réels : mouvement si population
animée, absence de mouvement si population statique. Aucun poids de mesh ni
corps de shader n'a été changé pour obtenir ce résultat. Le contrôle de phase
locale ne révèle donc pas de défaut de vent pour ces filets Debris ; il ne
requalifie pas les vêtements/drapeaux d'autres couches.

Il s'agit d'un dispatch compute réel, sans dessin de scène, matériau complet,
ombres ou verdict de parité visuelle. La fermeture du device après réussite
émet le diagnostic de destruction déjà observé dans les autres acquisitions.

## Dépendances exactes de trois matériaux partagés

`scripts/tidewater/prepare-debris-shared-materials.py` réouvre le paquet Debris
et la bibliothèque de bâtiments acquise `a53b6987…99e6` du candidat natif v3.
Le raccord choisit le matériau par contrat de textures unique, puis vérifie
ses corps vertex/surface source, modules dépendants, valeurs uniformes,
ordre des bindings, dimensions et bytes retenus des textures. Le module common
conserve l'adaptation bornée de caméra/profondeur du préparateur Village ;
les empreintes des payloads originaux restent conservées.

`debris-shared-materials-20261005-v2/dependencies.json` termine code 0 pour
Wood, Hard et Fabric. La v1 précède le contrôle des modules et reste conservée.
Les identités de matériau ne sont pas déduites d'un ordinal supposé : une
première tentative avec l'ordinal Hard erroné a été refusée par la comparaison
du programme source. Le résolveur final exige un contrat unique vérifié.

Nature, PebbleField et ScannedDebris restent explicitement non résolus. Le
descripteur est une préparation de dépendances : il n'instancie pas de rendu
partiel, ne normalise pas les assets et ne les qualifie pas dans Vault/Studio.
Prochaine étape : rendre les rôles spécifiques avec leurs ressources source,
puis adopter ensemble les meshes, matériaux, ombres et placements éditables.
Le test natif Bevy 93654, repollé actif lors de ces contrôles, termine ensuite
code 0 : un test PASS, 738 filtrés, après 35 min 24 s de construction puis
1,43 s d'exécution. Il vérifie la projection CPU réelle et les populations
de triangles ; il ne dessine pas Debris et ne qualifie pas la parité.

Suite : [raccord du programme Nature au matériau partagé](2026-10-05-debris-nature-material.md).
