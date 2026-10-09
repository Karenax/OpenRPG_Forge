# Tidewater — acquisition des accessoires de plage

L'acquisition complète de la scène source identifiait aussi `_beachProps`,
distinct des accessoires de cour déjà raccordés. Le nouvel exporteur
`scripts/tidewater/export-beach-props.mjs` observe les limites des objets
logiques dans la source figée `1438b1abfcaee3267092b75573014f4d9b4a983c`.
La licence MIT DRG Software Solutions LLC accompagne les données. Le clone
canonique n'est pas modifié ; les appels aléatoires restent dans leur ordre.

## Acquisition vérifiée

Deux sorties neuves, `beach-parts-20261004-v4` et `beach-parts-20261004-v5`,
contiennent 548 fichiers identiques, 43 objets, 78 parties matériau et
18 807 triangles :

| Contenu | Nombre |
|---|---:|
| Barques tirées sur le sable | 4 |
| Épave | 1 |
| Rames | 2 |
| Séchoirs à filets | 3 |
| Séchoir à poissons | 1 |
| Barils | 5 |
| Caisses | 8 |
| Casiers | 11 |
| Bouées | 5 |
| Rouleaux de cordage | 2 |
| Ensemble de cinq poteaux avec cordages | 1 |

Le vérificateur indépendant compare positions, normales, UV, teintes,
paramètres matériau et indices aux buffers de `source-017`. Les folds matériau
sont calculés en float64 avant float32, comme la source : aucune tolérance
élargie. Les plages ne chevauchent ni les bâtiments, ni les accessoires de
cour, ni un autre objet de plage. Les collisions restent attachées à leurs
groupes source. Reçu final `beach-parts-proof-20261004-v4.json`, statut `passed`.
Il s'agit d'une preuve d'acquisition ; `full_scene_qualified` reste faux.

## Runtime du générateur et essais conservés

Les deux premières extractions sous Node 24.19.0 retrouvaient les attributs
et indices exacts de la plage, mais leur empreinte globale de scène différait
de l'acquisition initiale. Ce verdict partiel reste conservé ; le vérificateur
refuse de qualifier une empreinte globale différente. Un rejeu du village
sous ce runtime conserva également tous ses fichiers binaires et ses
métadonnées d'objets ; seule son empreinte globale différait.

Le rejeu sous Node 22.22.3 retrouve exactement l'empreinte initiale
`e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1`.
La cause est donc isolée à la condition de runtime du générateur ; aucune
modification de source ou approximation de géométrie n'est utilisée pour
obtenir ce résultat. L'exporteur exige désormais ce runtime et cette empreinte
avant de créer une sortie. Le contrôle négatif refuse Node 24 avant toute
création de dossier : `beach-runtime-proof-20261004-v1/result.json`.
Les versions V1/V2 de l'acquisition et leurs reçus de diagnostic sont conservés.

## Suite requise

Ces objets ne font pas encore partie du candidat Studio village actuellement
en compilation. Conversion portable, lecteur, consommateur moteur, transport,
placements communs, catalogue réutilisable et rendu restent à intégrer.
Les instances `FishProps` sont un contenu distinct, pas inclus dans cette
acquisition. Aucun nouveau verdict natif, WebGPU, édition persistante, parité,
performance ou clôture de gate n'est revendiqué.
