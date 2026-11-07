# Données Tidewater

Acquisition, validation et transport des paquets de cette carte. Cette crate
utilise `forge_assets` et les types de domaine ; le moteur n'en dépend pas.
Les données auteur, hashes, provenance et formats acquis restent inchangés.

Le sélecteur CDLOD et les bornes de culling résident désormais dans
`forge_world::terrain_lod` et `terrain_lod_bounds`. Les codecs et le packing
des plantes sont dans `forge_assets::source_vegetation_package`. Les acquisitions
et gardes propres à la carte restent ici ; le moteur n'en dépend pas.

Les codecs d'environnement et des programmes HDR résident dans
`forge_assets::source_environment_package`. Les schémas, pins et gardes de
provenance acquis sont conservés. L'acquisition d'animation propre à la carte
reste ici. L'API de module 4 déclare explicitement les ressources par aire ;
leur admission n'implique aucune qualification de parité du rendu.

Les outils de vérification spécifiques se lancent avec
`cargo run -p forge_tidewater_assets --example <nom> -- <arguments>`.
Les anciennes API `forge_assets::tidewater_*` ont été retirées, sans alias dans
la bibliothèque générique. Les appels utilisent `forge_tidewater_assets::tidewater_*`.

L'[ancien README](PRE_SPLIT_ASSETS_README.md) conserve les détails historiques de
transport. Les licences et provenances de la carte restent avec ses paquets,
indépendamment de celles du moteur.
