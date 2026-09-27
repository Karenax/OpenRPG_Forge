# Tidewater — retouches sauvegardées dans le rendu natif

Le prototype relit une archive `ZoneEditSession` facultative depuis
`canonical-zone.json`. Il rejoue et valide cette archive, résout la population
canonique, puis applique sa vue auteur (exclusions et densité). Le même adaptateur
alimente les plantes visibles et leurs casters d'ombres. L'ordre source relatif
reste conservé ; la population complète est validée avant filtrage. Les groupes
vides ne créent pas de buffers GPU ni d'entités de rendu.

## Preuves exécutées

Racine des artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.
`prepare_tidewater_authoring_proof` crée les états par les commandes du domaine,
les sauvegarde et recharge chaque archive pour validation. Le script
`prepare-authored-vegetation-scene.py` les ajoute à la fixture canonique sans
réintroduire les 24 buffers d'instances acquis précédemment retirés.

Les captures FHD utilisent le même prototype natif et les mêmes fixtures océan,
rive, embruns et nuages que la preuve précédente. Binaire SHA-256 :
`50846ba781ef415acc8f19e544d0e87ce8cfb5ed8ff962be82ca24c7298dc5e9`.

- `authored-vegetation-states-002/clearing.json` : cercle X250/Z140, rayon110m.
  Les deux consommateurs (visible/ombres) annoncent 64868 plantes restantes et
  515 exclusions. Un comptage indépendant des JSON source confirme 202 arbres
  et 313 arbustes retirés. Tête :
  `39dbf71140b53d88284112db355be1ee661273d4242702ee1ba53dfbc6f6213f`.
- `native-authored-clearing-002.png` : 21506 pixels changés, tous dans le
  rectangle X0..310/Y236..556 de la pente gauche. Capture inspectée : forêt
  retirée localement, reste de l'image inchangé. SHA-256 :
  `82236e2410f1891964fcf8900db85f9315dcbf8368de88101fdd9d0bd762ce5a`.
- `native-authored-undo-002.png` : l'annulation sauvegardée de ce même cercle
  restitue exactement la référence `native-canonical-vegetation-001.png`, zéro
  pixel différent. SHA-256 commun :
  `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
- `native-authored-zero-001.png` : masques de densité nulle sur les dix couches,
  zéro plante restante, 65383 retirées par densité ; pas d'erreur GPU. 21582 pixels
  changent sur ce cadrage. L'herbe distincte des dix couches reste présente.
  SHA-256 : `4e52645424e2d2b7586055b7ea6fbf208dbd8c6ca9f148393c35cd239b9ab628`.

Rapport reproductible : `authored-scene-comparison-003.json`, produit par
`scripts/tidewater/verify-authored-scene-proof.py`. Compilation et Clippy strict
des deux exemples concernés réussis. La première clairière de test (états001,
X-80/Z-100, rayon120m) retirait 1028 plantes hors champ : son image identique
n'est pas une preuve visuelle de suppression ; ces artefacts sont conservés.

## Limites

Il s'agit du chargement d'états sauvegardés dans un prototype, pas d'une édition
interactive Studio. Les promotions sont retirées de la population source par
la vue auteur, mais leurs objets manuels ne sont pas encore rendus ici. Relief,
routes, bâtiments, habitat, sélection par identité, déplacement/ajout de plantes
et rechargement à chaud restent à raccorder. Le rendu ne prétend donc pas
représenter toute correction possible d'une archive. La qualification complète
des ombres, des autres caméras, de l'animation, du navigateur et des performances
reste ouverte. Aucun verdict de parité complète ni clôture de gate.
