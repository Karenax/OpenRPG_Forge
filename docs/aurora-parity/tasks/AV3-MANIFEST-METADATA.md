# AV3 — Métadonnées 3D gouvernées dans le Content Browser

## Périmètre

Cette tranche ferme l’enrichissement des manifestes demandé par AV3 sans modifier le schéma du
catalogue source ni le Vault immuable. Elle relie les sorties réelles du worker Blender au manifeste
d’asset persistant, puis les rend lisibles dans le Content Browser.

## Résultat livré

- métadonnées modèle optionnelles et rétrocompatibles dans le manifeste v1 : dimensions X/Y/Z en
  mètres, métriques géométriques, score qualité, styles, variantes et compatibilité ;
- chemins, hashes et taille de la miniature ainsi que hash du rapport qualité conservés comme preuves ;
- commande CLI `assets finalize-roundtrip` appliquée par `SetAssetPackageManifestHandler`, donc avec
  validation transactionnelle avant sauvegarde atomique ;
- finaliseur confiné au workspace qui vérifie UUID du job, asset/manifeste/configuration, licence,
  provenance, hash source, chemins relatifs, tailles et SHA-256 du GLB et de la miniature ;
- refus d’une sortie modifiée après la réponse du worker et conservation du manifeste précédent ;
- Content Browser capable d’afficher miniature générée, dimensions, triangles, score, réalisme,
  styles et nombre de variantes depuis le manifeste du projet ;
- collision et niveaux LOD reliés au GLB final uniquement après passage du gate qualité.

## Preuves

- tests domaine du validateur de métadonnées et de ses sidecars gouvernés ;
- round-trip de sérialisation canonique dans `AssetPackageManifestStoreTests` ;
- tests du finaliseur avec sortie valide puis GLB altéré après la réponse worker ;
- build complet sans avertissement ;
- smoke réel `ambientcg:3DApple001` via Blender 4.5.10 : 500 triangles, deux LOD, une collision,
  miniature 512 × 512 et score 99/100 persistés dans `assets/asset-manifests.json` ;
- marqueurs `OPENRPG_FORGE_ASSET_ROUNDTRIP_FINALIZE_PASS` et
  `OPENRPG_FORGE_AV3_MANIFEST_METADATA_SMOKE_PASS` dans `scripts/Verify.ps1`.

## Limites explicites

- `RuntimeImportVerified` reste faux : cette tranche prouve la fabrication et la gouvernance du GLB,
  pas encore son import/placement interactif depuis le Content Browser ;
- l’installation à la demande, le glisser-déposer et les brosses de dispersion sont les incréments
  AV3 suivants ;
- les tags de style viennent du catalogue gouverné existant ; un futur éditeur pourra les enrichir
  par commande transactionnelle, sans réécrire le Vault.

## Critère de passage

Le lot est fermé lorsque compilation, tests ciblés, smoke Blender réel, graphe d’architecture et gate
global passent sur le même état Git, puis que le commit est publié sur `main`.
