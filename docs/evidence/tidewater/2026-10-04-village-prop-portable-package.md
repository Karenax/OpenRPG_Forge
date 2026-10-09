# Tidewater — paquets portables des accessoires de cour

Suite de l'[acquisition exacte](2026-10-04-village-prop-source-acquisition.md).
Le convertisseur `scripts/tidewater/package-village-props.py` replie les
matériaux émetteurs selon `Village::_assemble`, conserve les paramètres
float64 source jusqu'à leur conversion explicite float32 et retrouve chaque
triangle avec tous ses attributs dans le maillage original `source-017`.
Il prépare des assets de la bibliothèque procédurale village avec géométrie,
matériaux source, pivots, bounds et collisions locaux séparés des placements.

Les trois lampes de chemin gardent leur paquet et leurs identités canoniques
déjà acquis. Le nouveau paquet contient donc **27 objets**, 51 parties de
matériau et **11 949 triangles**. Aucun nouvel objet lampe en double n'est
créé. Les fichiers source bruts restent intacts.

Les conversions indépendantes `village-package-20261004-v1` et
`village-package-20261004-v2` sont identiques, 297 fichiers chacune, dont
296 payloads et le manifeste. SHA-256 du manifeste :
`d03cd51f1ee894a46a8e84c5e3b5b3763c52d933bac9226ee99478425e4a02da`.
Les ressources binaires sont adressées par contenu et la licence MIT source
est conservée. La bibliothèque de matériaux village reste celle du paquet
bâtiments contrôlé ; ses ressources ne sont pas recopiées ni recalibrées.

Le pivot est mesuré au centre XZ des bounds de repos, à leur minimum Y,
avec axes identité. Cette définition dérivée est consignée pour chaque
prototype. Les cordes à linge conservent leurs poteaux ; la barque conserve
ses tréteaux. Les orientations source sont déjà portées par les sommets et
les collisions ; le placement source ajoute seulement la translation de
ce pivot. Cette recette ne prétend pas fournir un point de préhension ou
une ancre fonctionnelle qualifiée.

`verify-village-package.py` réouvre indépendamment les deux paquets et
vérifie tailles, SHA-256, fermeture exacte des références, matériaux connus,
identités/arguments, attributs finis, indices et bounds. Il restaure les
positions et collisions en coordonnées source. Le reçu
`village-package-proof-20261004-v1.json` est PASS : erreur maximale de
position mesurée **0 m**, normales identiques, retour des collisions dans
la tolérance 10⁻¹² m. Ce résultat ne valide pas les déformations animées.

## Suite requise

Le paquet n'est pas encore disponible dans Studio. Il reste le lecteur
Rust avec refus des fichiers altérés, le transport des ressources,
le catalogue/prototypes, puis le raccordement aux commandes communes de
placements et à leur persistance. Le registre de crate existant comporte
des modifications concurrentes ; son contenu a été inspecté sans écrasement
et aucun lecteur partiel n'y a été ajouté à cette étape.

Les matières tissu/filet et leurs animations doivent rester fidèles dans
le consommateur moteur. Les instances `FishProps` du séchoir restent à
acquérir séparément ; les accessoires de plage restent également ouverts.
Ni disponibilité Studio, ni rendu natif de ce nouveau contenu, ni édition
persistante, ni parité complète ne sont revendiqués par ce paquet.
