# Construction partagée des matériaux

`forge_engine::tidewater_assets::materials::prepare` construit le matériau de
village à partir des paramètres de l'asset, des handles de texture et des
ressources d'éclairage fournies par le renderer. Les textures absentes et les
paramètres non finis sont refusés avant toute modification d'un magasin d'assets.
Les emplacements de texture inutilisés reprennent la première texture, comme
dans le renderer de référence porté.

Le prototype `tidewater_plant_probe::village` utilise cette fonction pour ses
sept matériaux, avec les mêmes paramètres de vent, cartes d'environnement et
ombres. Les propriétés dynamiques restent sous la responsabilité du renderer ;
le paquet ne devient pas propriétaire du temps ni des ressources de scène.
Le check/Clippy strict ciblé et le build natif réussissent. La capture
`native-package-materials-001.png`, depuis la fixture privée des anciens
fichiers de bâtiments et `building-package-002`, reproduit exactement
`native-package-buildings-003.png` : zéro pixel différent, hash
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
Les buffers `object-depth.bin` et `shadow.bin` sont également identiques et
le log ne contient aucune erreur. Résultat local enregistré dans
`package-materials-render-proof-001.json`. Le chargement dans le viewport
Studio reste à raccorder ; cette capture ne le qualifie pas.
