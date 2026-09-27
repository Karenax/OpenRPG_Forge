# Tidewater — séparation des bâtiments avant fusion des matériaux

Le village source fusionne les bâtiments et accessoires en lots par matériau.
L'export historique de ces lots ne suffit donc pas à produire des objets
individuellement sélectionnables ou plaçables.

`export-village-parts.mjs` observe les appels originaux à `buildHouse`,
`buildShed`, `buildBoathouse` et `buildMarketStall`. Il encadre leurs émissions
par des relevés de plages de sommets et d'indices, puis copie les attributs
générés. Les corps des générateurs, leurs paramètres et l'ordre des appels
aléatoires restent ceux de la source. L'instrumentation est un module en mémoire ;
le checkout épinglé n'est pas modifié.

Chaque pièce conserve ses positions mondiales exactes en f32, ses normales,
UV, teintes, paramètres shader et indices locaux. Le descripteur conserve
l'identité source, la spécification du bâtiment et les résultats du générateur
(notamment emprise et hauteurs lorsqu'ils sont disponibles).

## Preuves

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- `building-parts-001` et `002` : 23 bâtiments, 111 portions de matériau,
  112569 sommets et 65363 triangles. Les 667 fichiers sont bit-identiques
  entre les deux exécutions.
- Chaque exécution construit également un village sans instrumentation et
  compare toutes les géométries finales : attributs, indices, matrices, noms,
  plages de dessin, emprises et métadonnées de bâtiments. Résultats identiques.
  Digest `3d069145925bc18bb4db0bb1c49967066e423cb4f1561e973315519986b3d264`.
- Le terrain après création des fondations conserve le hash source
  `be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3`.
- `verify-village-parts.py`, rapport `building-parts-proof-001.json` : hashes
  de tous les payloads vérifiés, attributs finis, indices locaux valides,
  plages de sommets disjointes entre bâtiments. L'inventaire fournit leurs
  limites spatiales et leurs nombres de triangles.
- Checkout de référence toujours propre après les acquisitions.

## Suite nécessaire

Les clés de matériau sont celles d'avant fusion : verre vers bois, corde vers
matériau dur, drapeau vers tissu et filet dédié nécessitent les mêmes remappages
de paramètres que `_assemble`. Il faut ensuite préserver l'ordre des triangles
en séparant les bâtiments et le décor restant, relier les pièces aux matériaux
natifs et intégrer des pivots/instances canoniques au catalogue et au Studio.
Les accessoires statiques émis dans un bâtiment sont inclus ; les objets
dynamiques et les accessoires ajoutés plus tard restent à traiter séparément.
Cet export n'est pas encore un import de catalogue ni un parcours Studio.
Aucun commit ni publication ; la parité complète reste ouverte.
