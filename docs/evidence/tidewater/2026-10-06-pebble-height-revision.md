# Tidewater — limites des galets et révisions de terrain

Le runtime natif courant refuse une révision de terrain après publication du
champ de galets : ses boîtes de cellules resteraient autrement celles du paquet
source. Cette limite reste active. La préparation ci-dessous ne modifie pas
les sources utilisées par les compilations native et Studio en cours.

## Recalcul préparé et vérifié CPU

`pebble-height-revision-20261006-v1/staged` ajoute une méthode immutable
`CellMap::with_height_bounds` à la copie du module réel. Elle conserve les
cellules occupées, leur origine et leur ordre, et refait les limites avec le
même voisinage de pixels que `PebbleField.js` à `1438b1ab` : borne inférieure
`floor(cell*hpc)-1`, borne supérieure `ceil((cell+1)*hpc)`, pixels inclusifs
bornés au raster. Une origine ou une étendue différente est refusée, ainsi que
des dimensions incohérentes et des valeurs non finies. Le source immuable reste
intact ; le consommateur doit invalider son `Selector` après remplacement.

Le contrôle réel `cpu-validation-v1/result.json` compile le code préparé avec
Rust 1.98 et exécute quatre tests, tous PASS, zéro filtré, en 0,11 s :

- Les 262 144 cellules du paquet acquis, dont 10 415 occupées, retrouvent
  exactement les bits des minima/maxima acquis avec le raster réel de
  2048 × 2048, SHA-256 `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.
- Une élévation de trois mètres dans ce raster conserve l'occupation et met
  à jour les limites ; après invalidation, une caméra inchangée refait la
  sélection avec les nouvelles hauteurs et les mêmes identités de cellule.
- Des révisions invalides sont refusées sans mutation de la grille originale.
- Les comparaisons conservent le zéro signé comme le programme source.

Les hashes du code, des fixtures réelles, du raster et du module de production
restent inchangés pendant le contrôle. Le harnais n'utilise ni Engine précompilé,
ni renderer, ni substitut de matériau.

## Raccord auteur encore ouvert

L'inspection de `tidewater_terrain::lod::PreparedLod` montre que sa texture
`height` conserve le raster source. Les retouches sont appliquées séparément
par `LodExtension::corrections` et `tidewater_terrain_edits::Corrections`, sur
la grille auteur aux coins ; le matériau Pebble courant lit seulement le raster.
Remplacer ses boîtes à partir de cette seule texture ne suffirait donc pas à
faire suivre aux galets les retouches affichées par le terrain.

Le raccord final doit ajouter aux évaluations de hauteur du shader Pebble la
même correction bilinéaire et l'élévation, publier cette texture avec la révision
du terrain, et calculer des limites conservatrices couvrant aussi les extrema
des corrections aux coins. Recuire simplement ces corrections aux centres du
raster peut perdre leurs extrema et modifier leur interpolation ; ce raccourci
ne constitue pas un raccord fidèle. Les bindings doivent aussi être conservés
dans les variantes temporelles et de réfraction.

La méthode préparée couvre le recalcul d'un raster courant, pas encore cette
composition complète de la grille source et des retouches auteur. Aucun dessin
GPU, terrain retouché dans Studio, adoption de production ou verdict de parité
n'est attribué à ces quatre tests.

## Composition conservatrice aux coins — huit tests CPU PASS

La copie `corner-bounds-v2/pebble_cells.rs` ajoute `with_authoring_bounds`.
Elle compose les minima/maxima du raster source avec l'élévation et les extrema
de la grille de corrections aux coins, séparément. Le voisinage inclut les
coins couvrant le demi-mètre de bord des pixels source et est borné aux limites
de l'île. Les cellules non occupées restent vides. La texture neutre d'un pixel
est ignorée comme dans l'évaluateur actuel du shader ; dimensions, domaine,
finitude et dépassements de hauteur sont refusés sans modifier l'original.

`corner-bounds-v2/cpu-validation-v1/result.json` termine compilation et tests
à code 0, inputs inchangés. Huit tests sont exécutés, zéro filtré, en 0,11 s :
les quatre précédents, égalité de toute la grille source sous correction neutre,
pic auteur situé entre les centres du raster, bords de l'île/texture neutre,
et refus de corrections invalides ou débordantes. Le pic de 7,5 m reste entier
dans les limites, alors que son échantillonnage aux centres n'en conserverait
qu'un quart. Ces tests vérifient le calcul conservateur CPU ; ils ne lisent
pas les sommets déplacés par le GPU.

## Shader et adoption natifs préparés — qualification en attente

`pebble-authored-height-20261006-v1/staged` ajoute la texture de corrections au
binding 107 de `PebbleExtension`, puis compose le shader acquis avec la méthode
existante `tidewater_terrain_edits::Corrections::shader`. Le shader original de
hauteur est conservé ; le même évaluateur des corrections aux coins s'applique
aux positions et aux lectures de hauteur de surface. Les données non finies
et les descriptions de correction incohérentes sont refusées avant publication.

Le contrôle `shader-validation-v1` est lancé sous 83619 : compilation du code
de composition réel et des imports Bevy/Naga, avec variantes principale,
réfraction et auxiliaires temporelles pour les profils neutre/retouché. Aucun
PASS ou dessin GPU n'est encore attribué ; le résultat n'est pas disponible.

La copie `native-initial-v1/staged` prépare le raccord initial dans le vrai
probe natif. Elle exige le même hash de révision CPU/terrain affiché et les
mêmes octets de texture R32 que les corrections du document, fournit la texture
au nouveau matériau et les limites conservatrices au sélecteur. Cette copie
n'est ni adoptée ni compilée dans l'application complète. Les révisions natives
ultérieures restent explicitement refusées et le champ Studio reste à raccorder.
Ces préparations ne remplacent pas les résultats des deux builds actifs.

Le contrôle de types `debris-refraction-20261006-v1/material-check-authored-v1`
est lancé sous 54092. Son harnais conserve les vraies définitions courantes
de Village/Nature/Scanned et les captures imbriquées préparées, en remplaçant
uniquement la définition Pebble par celle portant la texture de corrections
107. Il compile les derives/bindings et les types d'enregistrement, sans Engine
ancien ni exécution de plugin. Ce contrôle reste actif sur son handle ; aucun
résultat terminal ni qualification du loader natif n'est encore attribué.

Les quatre handles 19763 (runtime natif), 24461 (Studio web courant), 83619
(variantes du shader auteur) et 54092 (types de matériau auteur) sont repollés
et encore actifs. Les logs Studio progressent dans les dépendances Bevy ;
le lien du runtime n'a pas encore produit son reçu terminal. Une période sans
sortie ne déclenche aucun redémarrage ni modification des inputs épinglés.

Le contrôle de types auteur 54092 termine ensuite PASS à code 0, inputs
inchangés, dans `material-check-authored-v1/result.json`. Le matériau Pebble
avec texture 107 et les types de capture imbriqués compilent contre les vraies
définitions courantes. Les 42 avertissements du harnais sont conservés ; aucune
qualification Clippy, du loader natif complet, de plugin exécuté ou de dessin
GPU n'est déduite de ce contrôle de métadonnées Rust. Les trois autres handles
restent actifs lors de cette vérification.

Le contrôle shader 83619 termine ensuite PASS : `shader-validation-v1/result.json`
porte `compile_exit_code=0`, `test_exit_code=0` et `inputs_unchanged=true`.
Les 32 compositions utilisent l'adaptation auteur préparée, l'évaluateur
`Corrections::shader` courant et les dérivations courantes de réfraction et
de rendu temporel, pour les profils neutre et retouché. Les refus de payloads
invalides restent inclus dans le harnais. Aucun dessin GPU, changement de
révision terrain publié ou adoption en production n'est déduit de ce PASS.
Les builds natif 19763, Studio 24461 et le contrôle de types de retrait scanné
68461 restent actifs au dernier polling de leurs handles.
