# Utilisation du premier pipeline Asset Vault

## Emplacement local

Le snapshot du 2 août 2026 est installé dans `E:\OpenRPG_AssetVault`. Pour utiliser un autre disque :

```powershell
$env:OPENRPG_ASSET_VAULT = 'D:\OpenRPG_AssetVault'
```

Le Vault reste extérieur à Git. Le dépôt conserve son catalogue portable dans
`asset_manifests/catalogs/cc0_3d_all.json`.

## Installer et désinstaller des packs locaux

Le registre installé et les blobs dédupliqués restent dans le Vault externe. Les commandes sont en
mode aperçu tant que `--apply true` n’est pas fourni :

```powershell
dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- `
  assets install-pack . food-props `
  --id ambientcg:3DApple001,ambientcg:3DApple002

dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- `
  assets install-pack . food-props `
  --id ambientcg:3DApple001,ambientcg:3DApple002 `
  --apply true

dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- assets report .
dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- assets credits . --format markdown

dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- `
  assets uninstall-pack . food-props --apply true
```

L’installation recalcule chaque SHA-256, refuse une licence non vérifiée ou non CC0, copie une seule
fois chaque contenu sous `packages/objects/sha256/`, puis écrit registre, reçu et crédits agrégés.
La désinstallation sauvegarde le registre et conserve tout objet encore référencé par un autre pack.
Les répertoires `originals/` et `normalized/` ne sont jamais modifiés.

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

## Pack curaté Forgotten Forest

La première sélection cohérente est décrite par
`asset_manifests/packs/forgotten_forest_core.json`. Elle expose individuellement les 22 GLB du pack
Kenney Mini Forest au lieu de traiter le ZIP comme un seul asset. L’authoring vérifie la licence
CC0-1.0 du catalogue, recalcule chaque SHA-256, copie le modèle et sa palette PNG externe dans le
workspace, puis écrit profils, reçus, manifestes et `assets/CREDITS_ASSETS.md`.

```powershell
dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- `
  author-forgotten-forest apps/Forge.Runtime/content/forgotten-forest `
  --vault E:\OpenRPG_AssetVault
```

La commande est idempotente et ne modifie jamais `normalized/kenney/mini-forest`. Pour authoriser,
compiler et lancer la carte en une fois, utiliser `scripts/Run-ForgottenForest.ps1`.
