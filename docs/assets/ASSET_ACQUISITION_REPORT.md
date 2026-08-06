# Rapport d’acquisition des assets

Date : 2 août 2026.

## Résultat

- Sources interrogées : Poly Haven, ambientCG et Kenney, APIs/pages et téléchargements officiels.
- Résultats unitaires trouvés par les deux APIs : 555.
- Packs 3D gratuits Kenney trouvés : 50.
- Assets/archives acceptés et téléchargés : 555 assets et 50 packs.
- Assets refusés : 0.
- Erreurs finales : 0.
- Poly Haven : 521 assets, dont 520 glTF 1K et un FBX 1K de repli.
- ambientCG : 34 archives `LQ-1K-JPG`, chacune contenant au moins un OBJ et un USDC.
- Kenney : 50 ZIP CC0, 13 717 fichiers de modèles multi-formats et environ 4 681 noms de modèles
  distincts dans ces packs.
- Volume des archives et fichiers sources catalogués : 4 471 668 545 octets, soit environ 4,16 Gio.
- Occupation totale locale du Vault avec extractions et métadonnées : 5 144 018 352 octets, soit
  environ 4,79 Gio.
- Représentations de modèles présentes, USDC inclus : 14 306.
- Modèles logiques estimés à partir des sources et noms uniques : environ 5 236.
- Textures présentes : 10 015.
- Licence rencontrée : CC0 1.0 uniquement.

## Couverture principale Poly Haven

Le catalogue contient notamment 176 entrées `props`, 110 `nature`, 85 `furniture`, 70 `tools`, 68
`containers`, 57 `plants`, 37 `rocks`, 32 `ground cover`, 29 `lighting`, 26 `structures` et 20
`trees`. Ces catégories se chevauchent.

## Intégration

- Originaux et extractions : `E:\OpenRPG_AssetVault`.
- Catalogue portable : `asset_manifests/catalogs/cc0_3d_all.json`.
- Catalogue des packs Kenney : `asset_manifests/catalogs/kenney_3d_packs.json`.
- Manifeste de pack : `asset_manifests/packs/cc0_3d_all.json`.
- Sources et preuves de licence : `asset_manifests/sources/`.
- Téléchargeur reproductible : `tools/asset_pipeline/download_cc0_3d.py`.
- Validateur : `tools/asset_pipeline/validate_cc0_catalog.py`.

## Limites

Le fonds 3D est intégré au Vault et catalogué, mais pas encore déclaré prêt pour une scène
de production. Les contrôles visuels, normalisation d’échelle/pivot, matériaux Godot, collisions,
LOD, miniatures locales et sélection artistique Forgotten Forest restent à réaliser. Le catalogue
complet maximise la matière disponible ; une carte ne doit en employer qu’un sous-ensemble cohérent.

Depuis le 5 août 2026, le pipeline possède reprise HTTP, cache SHA-256, rafraîchissement immuable,
plan de diff et désinstallation locale récupérable. Ces fonctions sont validées sur fixtures sans
réexécuter le miroir de 4,16 Gio ; le volume et les comptes ci-dessus restent ceux du snapshot
d’acquisition du 2 août.
