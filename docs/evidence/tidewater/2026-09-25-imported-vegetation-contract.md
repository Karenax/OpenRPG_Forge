# Tidewater — données canoniques de végétation importée

Source figée : `1438b1abfcaee3267092b75573014f4d9b4a983c`, export `source-017`.
Racine des preuves : `E:/OpenRPG_Forge/.tidewater-integration/`.

## Résultat

`VegetationDocument` v2 référence dix blocs immuables, soit **65383 instances**
et **9938216 octets**. Positions, échelles, clés source et cinq paramètres de
prototype sont conservés en f64. L'orientation yaw est convertie en quaternion.
Les paramètres de déformation ne sont pas absorbés dans cette orientation.
Les 14 fichiers des extractions `imported-vegetation-001` et `002` sont identiques.

`imported-vegetation-source-verification-001.json` compare chaque enregistrement
aux JSON source, y compris les hashes de provenance. Le lecteur Rust vérifie
les tailles, hashes, transformations, clés triées uniques et identités des 65383
entités. Hash canonique du document :
`964761707a8e135fa0ef471e31b5a1a88a57414c9a3e83d55ff0b11e8043e0d7`.

La création d'une zone rétrogradait systématiquement le terrain en v3. Elle
préserve maintenant v4 pour les rasters et garde l'évolution v3 des terrains
procéduraux anciens. L'exemple de vérification assemble les vrais documents
Tidewater, archive puis relit les références de terrain et de végétation.
Rapport : `imported-vegetation-zone-verification-001.json` ; archive 33639 octets,
hash du head `71439d2740cb6298806fdb82204fc22c0565c4960b9788282c69ed6f376cf072`.

## Contrôles

- Quatre tests de contrat : version/couverture, format historique sans nouveau
  champ sérialisé, décodage/identité, données corrompues ou sémantiquement invalides.
- Test terrain importé : création de zone, modification du nom, archive,
  réouverture et annulation, sans perdre le format v4 ou la référence du raster.
- 31 tests existants de végétation projet/monde passent.
- Neuf tests existants de migration, session et régénération des zones passent.
- Clippy `forge_project`, `forge_world` et l'exemple, avec `-D warnings`, passe.

Le premier test de hash utilisait les nombres JSON bruts comme oracle des f32
normalisés de Forge : il a été corrigé pour comparer le jeu historique des champs
après la même normalisation typée. Aucun changement de production n'a été fait
pour masquer cette différence de représentation.

## Portée

Il s'agit de données et d'archives de **références**, pas d'une archive autonome
contenant les blocs binaires. Le catalogue, le résolveur de blobs, les corrections
d'instances importées et leur raccordement aux prototypes de rendu restent ouverts.
Le générateur procédural refuse explicitement v2 pour empêcher une régénération
silencieuse de la forêt. Aucun changement du rendu, du placement Studio, des
collisions ou des performances n'est qualifié ici. L'herbe et le village ne sont
pas compris dans ces dix blocs. L'objectif visuel complet reste en cours.
