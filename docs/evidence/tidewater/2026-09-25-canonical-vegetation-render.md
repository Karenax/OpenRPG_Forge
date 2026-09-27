# Tidewater — végétation canonique dans le rendu natif

L'import conserve désormais `source_order`, ordinal au sein de chacun des six
groupes amont. Les blocs restent triés par clé source ; les identités d'entité
ne changent pas. `imported-vegetation-003/004` contiennent dix blocs, 65383 plantes,
10461280 octets : les 14 fichiers répétés sont identiques. Le vérificateur de
source contrôle aussi les ordinaux. Les anciens blocs à cinq paramètres restent
lisibles ; le rendu canonique exige l'ordinal et refuse doublons/trous.

## Données et rendu

Racine : `E:/OpenRPG_Forge/.tidewater-integration/`.
`canonical-packed-002` et `canonical-packing-verification-002.json` prouvent
l'égalité bit à bit des 1634575 valeurs f32 ET des six séquences de groupe dans
l'ordre d'origine. Aucune permutation par identité ne masque un écart d'ordre.

`canonical-vegetation-scene-001` dérive de `cloud-overcast-shadow-001` : les
24 buffers acquis `matrices/iPos/iDat/qr2` sont physiquement absents. Le document
canonique et ses dix blocs les remplacent. Le renderer visible et les casters
de végétation partagent le même adaptateur Rust, sans repli sur ces anciens buffers.
Géométries, textures, matériaux et autres paramètres de la scène restent les
fixtures précédemment qualifiées ; ce n'est pas encore un module Studio autonome.

Commande exécutée avec le binaire construit : `tidewater_plant_probe`
`canonical-vegetation-scene-001 native-canonical-vegetation-001.png`
`--water-spray-clouds ocean-native-inputs-001 shore-native-inputs-001`
`spray-native-budget-inputs-001 cloud-controller-inputs-001` (chemins sous la racine).

`native-canonical-vegetation-001.png`, inspecté visuellement, est **strictement
identique** à `native-cloud-stepped-scene-002.png` : 1920×1080 RGBA, zéro pixel
différent. SHA-256 commun :
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
Rapport : `canonical-scene-comparison-001.json`. Compilation et Clippy strict de
l'exemple natif passent. Le contrôle d'ordre rejette les ordinaux fractionnaires,
négatifs, non finis ou hors budget tout en conservant le contrat historique.
SHA-256 du binaire exécuté :
`76e386eb46f74b1b17f305c073170cf5c9ecc9d9c5cd71c82039248b663c2b2c`.

## Portée

Cette paire de captures prouve la non-régression de ce cadrage avec la population
source intacte. Elle ne qualifie pas les autres caméras, la reproductibilité
générale des embruns, le rendu des retouches, la navigation interactive, les
prototypes plaçables, Studio, WebGPU navigateur ou les performances. Le chargement
depuis une zone de workspace active et la projection des corrections d'auteur
dans ces groupes restent à raccorder. La parité visuelle complète reste ouverte.
