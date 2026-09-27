# Ombres des objets editables — 27 septembre 2026

## Raccordement

Le module moteur `tidewater_object_shadow::scene` prepare les trois cascades
source et extrait les casters des instances `ResidentScene`. Les poses locales
des racines incluent l'aperçu du gizmo, avant propagation de GlobalTransform.
Les enfants doivent appartenir directement a leur racine, qui reste sans parent.
Les materiaux 5/6 (filets/poissons) sont exclus du passage opaque source.

Studio conserve un Receiver correspondant a la derniere generation encodee.
Une generation en attente n'est pas remplacee a chaque image : le graphe a
besoin d'une image pour preparer les ressources, puis d'une autre pour dessiner.
La generation terminee est publiee avant d'en preparer une nouvelle a la pose
courante. Un changement de terrain, de sortie d'ombre de relief, d'environnement,
de bibliotheque ou d'identites de racines invalide une ancienne generation en
attente. Le mouvement continu camera/gizmo admet une latence de generation ;
la borne temporelle reste a mesurer. `ready` signifie commandes encodees, pas
fence de completion GPU.

Les recepteurs terrain/CDLOD/batiments adoptent ensemble la texture et ses
matrices. Les mises a jour lumineuses preservent les handles de materiaux,
les racines, les meshes et les collisions. Le shader village est reinstalle
uniquement lors du changement de bibliotheque. Les modifications canoniques
du lot de placements reconstruisent encore ce lot.

## Verification

- Clippy moteur/probe --no-deps strict PASS (log building-live-shadows-clippy-002).
- Build natif PASS : 5 min 35 s. Execution PASS en 2,66 s ; cette duree est
  celle du petit programme de preuve, pas un benchmark Studio.
- Regression physique PASS : 23 batiments, 111 colliders, transformations,
  duplication, suppression/restauration, raycasts et partage des meshes.
- Readback GPU PASS, 107 parties opaques reelles (quatre parties de materiaux
  exclus), trois cascades de 2048 x 2048. Generation initiale : 265 517 texels
  occupes ; apres translation +45 m en X, rotation Y 0,7 et echelle non uniforme :
  44 476 ; apres suppression : zero dans les trois couches. Echantillons tous
  finis et dans [0,1]. Les sorties initiale/modifiee sont distinctes et les
  anciennes textures conservees ne sont pas ecrasees par la suivante.
- Visualisation des masques d'occupation inspectee : silhouettes presentes,
  deplacees puis absentes ; il ne s'agit pas du rendu eclaire du village.
- Studio WASM compile ; Clippy strict Studio reste en echec uniquement sur le
  diagnostic preexistant atelier_ui.rs:122, bind_main 103 lignes au lieu de 100.
  Aucun diagnostic local sur ce raccordement.

Preuves locales : E:/OpenRPG_Forge/.tidewater-integration/building-live-shadows-001/
(proof.json, trois generation-N.depth.bin, cascade-occupancy.png).
Executable : `5bfd48d3f69ec775ac8c9e05497c81efd60cf11c606d6c4a152e082546f345a6`.
Log d'execution : building-live-shadows-run-001.log dans le dossier integration.

## Limites conservees

Aucune capture du canvas Studio ni qualification de gestes GUI dans ce lot.
Le probe GPU porte les casters des vrais batiments, mais pas une comparaison
photometrique du village complet, ni la politique de publication propre a Studio.
Le nouveau chemin prepare encore les buffers de casters, le raster et les bornes
terrain pour chaque generation : couts CPU/GPU et reutilisation a optimiser et
mesurer avant qualification interactive. Pas encore de casters vegetaux dans
ce raccordement. Les anciennes ombres restent visibles pendant un remplacement
jusqu'a ce que la nouvelle generation soit disponible.

La vegetation editable, l'eau, les decors residuels, le playtest importe, les
effets/animes manquants et la parite complete restent dans l'objectif original.
Aucune nouvelle distribution Studio, aucun commit ni push.

Suite : la reconstruction CPU du raster, des corrections et des bornes par
mouvement de camera a ete remplacee par un cache de revision ; voir
[Cache ombres terrain](2026-09-27-shadow-terrain-cache.md). Les autres couts GPU
et la qualification interactive restent ouverts.
