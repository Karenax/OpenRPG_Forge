# Données Tidewater

Acquisition, validation et transport des paquets de cette carte. Cette crate
utilise `forge_assets` et les types de domaine ; le moteur n'en dépend pas.
Les données auteur, hashes, provenance et formats acquis restent inchangés.

Les ports de référence CDLOD, bornes de culling du terrain et empaquetage de
végétation résident également ici. Ils consomment les documents et rasters
validés de `forge_world` en lecture seule ; le domaine ne dépend pas de cette
crate. Le code source de la fermeture générique du moteur est contrôlé pour
exclure aussi les provenances et implémentations propres à cette carte.

Les outils de vérification spécifiques se lancent avec
`cargo run -p forge_tidewater_assets --example <nom> -- <arguments>`.
Les anciennes API `forge_assets::tidewater_*` ont été retirées, sans alias dans
la bibliothèque générique. Les appels utilisent `forge_tidewater_assets::tidewater_*`.

L'[ancien README](PRE_SPLIT_ASSETS_README.md) conserve les détails historiques de
transport. Les licences et provenances de la carte restent avec ses paquets,
indépendamment de celles du moteur.
