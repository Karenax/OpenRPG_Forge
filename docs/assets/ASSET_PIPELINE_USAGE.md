# Utilisation du premier pipeline Asset Vault

## Emplacement local

Le snapshot du 2 août 2026 est installé dans `E:\OpenRPG_AssetVault`. Pour utiliser un autre disque :

```powershell
$env:OPENRPG_ASSET_VAULT = 'D:\OpenRPG_AssetVault'
```

Le Vault reste extérieur à Git. Le dépôt conserve son catalogue portable dans
`asset_manifests/catalogs/cc0_3d_all.json`.

## Télécharger ou reprendre

```powershell
python tools/asset_pipeline/download_cc0_3d.py `
  --source all `
  --vault E:\OpenRPG_AssetVault `
  --jobs 6 `
  --catalog-output asset_manifests/catalogs/cc0_3d_all.json
```

Le téléchargement est idempotent : un fichier existant avec le MD5 attendu est réutilisé. Poly
Haven est téléchargé en glTF 1K, avec repli FBX 1K quand glTF n’existe pas. ambientCG est téléchargé
en archive `LQ-1K-JPG`, conservée intacte puis extraite dans `normalized/` après contrôle des chemins.

Les 50 packs 3D gratuits Kenney se téléchargent séparément :

```powershell
python tools/asset_pipeline/download_kenney_3d.py `
  --vault E:\OpenRPG_AssetVault `
  --jobs 6 `
  --catalog-output asset_manifests/catalogs/kenney_3d_packs.json
```

L’adaptateur refuse automatiquement une page ne déclarant pas `Creative Commons CC0` ou ne
fournissant pas un ZIP officiel.

## Valider l’inventaire local

```powershell
python tools/asset_pipeline/validate_cc0_catalog.py `
  asset_manifests/catalogs/cc0_3d_all.json `
  --vault E:\OpenRPG_AssetVault

python tools/asset_pipeline/validate_cc0_catalog.py `
  asset_manifests/catalogs/kenney_3d_packs.json `
  --vault E:\OpenRPG_AssetVault
```

La validation recalcule SHA-256, contrôle tailles, licences CC0, dépendances locales glTF et modèles
extraits. Le pipeline de normalisation Godot/Blender, les collisions, LOD et previews restent le
prochain incrément avant usage de production dans une carte.
