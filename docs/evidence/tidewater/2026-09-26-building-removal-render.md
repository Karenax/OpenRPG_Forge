# Suppression et Undo rendus depuis le workspace

Les archives `building-placement-states-002/removed.json` et `remove-undo.json`
sont adoptées dans deux workspaces distincts par `verify_imported_adoption`.
Les onze blobs de terrain/végétation sont vérifiés ; les placements sont ceux
du head sauvegardé. Les données de rendu des bâtiments restent dans la fixture.

Captures natives, même binaire et mêmes paramètres que le lot précédent :

- `native-buildings-removed-001.png`, inspectée : la maison au premier plan et
  son ombre disparaissent. 153168 pixels changent, écart maximal 141/255.
  Le buffer d'ombre des objets change ; celui du relief demeure identique.
- `native-buildings-undo-001.png` : zéro pixel différent de l'état initial.
  Buffers d'ombres des objets et du relief exactement rétablis.
- Image initiale et Undo :
  `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
- Les captures terminent avec code 0, sans ERROR dans leurs journaux.

Le script `verify-building-removal-render.py` reproduit les contrôles.
Résultats sous `E:/OpenRPG_Forge/.tidewater-integration/` :
`building-removal-render-proof-001.json` et répétition `002`.

## Suite Studio

Inspection du chemin existant `authoring_assets/placements.rs` : les objets
manuels sont chargés comme scènes GLB, avec `PlacementRoot`, projection physique
et cache par `(AssetId, ContentHash)`. Les matériaux Tidewater sont encore dans
le prototype ; ajouter seulement un EntityId aux morceaux ne suffit pas à les
faire apparaître dans cette route. Il reste à partager le chargeur et ses
matériaux natifs, résoudre leurs dépendances portables, puis raccorder le picking
et la réconciliation aux commandes existantes. La preuve présente couvre la
suppression et l'annulation natives sur une vue, pas ces interactions Studio.
