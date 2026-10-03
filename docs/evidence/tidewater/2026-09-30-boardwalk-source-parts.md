# Tidewater — acquisition des passerelles du village

La paire village/Studio a montré l'absence de la passerelle allant du ponton à
la place et de ses deux embranchements. Dans la source figée
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`, ces
trois objets proviennent de `src/world/village/Boardwalk.js`, appelés par
`Village.js` séparément des 23 bâtiments.

`scripts/tidewater/export-boardwalk-parts.mjs` observe ces trois appels sur un
worktree source détaché et propre. Une construction non instrumentée est
comparée à la construction observée : géométrie fusionnée, indices, matrices,
terrain, bâtiments, empreintes et colliders restent identiques. Le clone
canonique `upstream` n'a pas été modifié. Les deux exports indépendants
`E:/OpenRPG_Forge/.tidewater-integration/boardwalk-parts-001` et `002`
contiennent les mêmes 36 fichiers octet pour octet.

Le [contrôle des plages source](../../../../.tidewater-integration/boardwalk-parts-proof-001.json)
relit la scène acquise `source-017` et les plages de `building-parts-005`.
Chaque plage d'indices de passerelle est exactement celle de la scène source,
et aucune ne chevauche un bâtiment :

| Élément | Sommets | Triangles | Colliders de marche | Lampes |
|---|---:|---:|---:|---:|
| Ponton → place | 9 598 | 4 898 | 44 | 3 |
| Chemin latéral 1 | 2 152 | 1 076 | 10 | 0 |
| Chemin latéral 2 | 3 180 | 1 590 | 15 | 0 |
| Total | 14 930 | 7 564 | 69 | 3 |

`prepare-boardwalk-materials.py` a rapproché chacun des 7 564 triangles des
attributs ordonnés de la scène acquise, après pliage des données shader dans
`material-0` et `material-1`. Ses deux sorties de 31 fichiers sont identiques.
`prepare-boardwalk-frames.py` produit ensuite trois repères locaux déplaçables,
en gardant les positions/normales source de référence, les courbes et les
colliders. L'erreur maximale aller-retour des sommets est de
`9,16 × 10⁻⁷ m` ; ses deux sorties de 36 fichiers sont identiques.

`package-boardwalk-assets.py` rassemble les trois objets, leurs 35 fichiers de
données dédupliquées et la notice MIT dans un paquet portable indépendant de
1 255 517 octets de payload. Les deux constructions `boardwalk-package-001`
et `002` donnent le même manifeste SHA-256
`2d8270d51c0666b256618740a68069e79f6c1dda6c254062194717c937c734e6`.
Le manifeste lie les matériaux à la bibliothèque du paquet de bâtiments
existant, sans changer son empreinte ni les 23 identités déjà enregistrées.
Le lecteur `forge_assets::tidewater_boardwalk::BoardwalkPackage` vérifie
l'empreinte du manifeste, chaque payload, les références du descripteur,
les trois identités source, les matériaux et la notice. Son test ciblé avec le
paquet réel passe et refuse tant un manifeste qu'un payload modifié.

Ce paquet n'est pas encore chargé comme objet du catalogue Studio : le paquet
de bâtiments actuel reste à 23 assets et ne contient pas ces trois passerelles.
Il reste à projeter rendu et collisions dans Studio,
exposer déplacement/édition/sauvegarde, puis refaire la capture appariée et un
essai de marche sur les 69 volumes. Aucun clone de référence, document auteur
ou distribution n'a été modifié par l'acquisition.
