# Métadonnées de catalogue importé

Le contrat existant `AssetCatalog` possède déjà `AssetSource::External`.
Il reçoit une validation explicite `validate_import_metadata` et une résolution
`resolve_import_pin` pour les consommateurs d'import. Le catalogue est borné à
4096 records ; noms, identités, provenance déclarée et hashes sont contrôlés.
La résolution refuse un asset absent ou un contenu différent de la version
sauvegardée. Elle ne résout aucun URI et ne vérifie pas les fichiers référencés.

Le validateur procédural Gate 0 et les restrictions du kit Blender restent
inchangés. Tidewater conserve une origine externe et sa déclaration MIT : il
n'est pas enregistré comme création procédurale Forge.

L'exemple `prepare_tidewater_catalog` produit 23 descripteurs canoniques et
leur catalogue depuis les repères vérifiés. Chaque hash épingle le descripteur
du bâtiment, ses références de géométrie et ses matériaux natifs. Ces fichiers
ne prétendent pas être des GLB. Les identités dérivent des clés source stables.

## Preuves

- `cargo test -p forge_assets -j1` : 30 tests réussis, dont sauvegarde/relecture,
  référence absente, contenu modifié, provenance invalide et doublon.
- `cargo clippy -p forge_assets --all-targets -j1 -- -D warnings` : réussi.
- Exemple exécuté sur `building-frames-001/buildings.json` ; sortie dans
  `E:/OpenRPG_Forge/.tidewater-integration/building-catalog-001`.
- Catalogue : `73d29acb060eb991d7b94cd7468c3a764e8e0c440e486fa328c4748b6c5113b1`.
  Les 23 références sont résolues après sérialisation/relecture.

## Limites

Ce lot valide des métadonnées, pas un import complet : ni octets des dépendances,
ni droits, ni qualification visuelle ne sont inférés du validateur. L'acquisition
source apporte ses preuves séparées. Le navigateur Studio utilise encore son kit
gouverné ; raccordement UI, stockage portable des dépendances et commandes de
placement restent ouverts. Aucun rendu n'est modifié par ce lot.
