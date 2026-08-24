# AV6-E — habillage naturel gouverné

Dernière mise à jour : 5 août 2026.

> Statut historique : cette première passe Kenney a été remplacée le 6 août 2026 par la sélection
> `asset-vault-polyhaven-environment`. Les douze modèles Nature Kit ne sont plus référencés ni
> distribués dans Asset Vault ; ce document conserve la preuve du jalon AV6-E initial.

## Résultat

Asset Vault réutilise maintenant douze modèles CC0 du pack Kenney Nature Kit déjà présent dans
le Vault externe. La sélection est bornée à cinq silhouettes d’arbres et sept éléments rapprochés :
buisson, plante haute, champignons, fleurs, herbe, tronc mort et rocher.

`asset_manifests/packs/asset_vault_nature_dressing.json` épingle les chemins sources, tags,
pack et licence. L’installateur vérifie le catalogue, recalcule les SHA-256, préserve le Vault en
lecture seule et accepte désormais les GLB dont la palette est embarquée. Les crédits sont générés
par pack puis agrégés dans `assets/CREDITS_ASSETS.md`.

## Composition et preuves

Les cinq recettes historiques restent intactes. Douze recettes supplémentaires produisent une
dispersion déterministe partagée entre Creator et runtime, avec exclusions chemins/rivière et
culling adapté :

- 17 couches de biome et 7 112 instances MultiMesh ;
- 45 manifestes, 67 chunks et 56 références d’asset gouvernées ;
- 40 assets avec LOD, 108 niveaux LOD et 17 groupes de culling ;
- package `.orpmod` valide de 126 fichiers ;
- 32 454 polygones navigables et trajet camp–sanctuaire de 70 points inchangés.

Preuves reproductibles :

- `artifacts/verification/asset-vault-av6e-world-forward-plus.png` ;
- `artifacts/verification/asset-vault-av6e-close-forward-plus.png` ;
- `OPENRPG_FORGE_AV6E_WORLD_VISUAL_PASS ... sectors=64 scatter=7112` ;
- smoke complet terminant dialogue, collecte, passage, boss, quête et caméra orbitale.

## Limites explicites

La variété de silhouettes et les détails de premier plan sont visibles, mais la direction reste
stylisée et issue de deux kits cohérents. Cette tranche ne prétend pas atteindre une qualité AAA :
textures photogrammétriques, eau avancée, placement artistique manuel secteur par secteur et
retargeting entre rigs incompatibles restent des travaux distincts.

## Remplacement Poly Haven du 6 août 2026

Le manifeste `asset_manifests/packs/asset_vault_polyhaven_environment.json` gouverne désormais
17 modèles CC0 issus de l'OpenRPG Asset Atlas. Le worker Blender sélectionne les variantes utiles,
réduit les sapins et éléments lourds, recentre le pivot au sol, produit un GLB autonome et écrit un
crédit par asset avec page source, licence, identité stable et SHA-256. Les GLTF du Vault restent
inchangés.

La carte courante contient 13 couches et 4 617 instances, un navire central, trois ponts en ponton,
deux haches de camp, 45 manifestes, 67 chunks, 43 modèles placés et trois traversées navigables. Le
smoke complet et la capture aérienne Forward+ passent ; l'habillage rapproché reste à affiner.
