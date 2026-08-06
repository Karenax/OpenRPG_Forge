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

Le téléchargement est idempotent : un fichier existant avec le MD5 attendu est réutilisé. Un
téléchargement interrompu conserve un fichier `.part` et reprend avec HTTP Range. Chaque résultat
alimente `cache/downloads/sha256/`. Avec `--update`, un contenu distant différent ne remplace jamais
l’original : il rejoint `versions/<hash>/` et les extractions ambientCG/Kenney suivent la même
version. Poly Haven est téléchargé en glTF 1K, avec repli FBX 1K quand glTF n’existe pas. ambientCG
est téléchargé en archive `LQ-1K-JPG`, conservée intacte puis extraite dans `normalized/` après
contrôle des chemins.

Pour rafraîchir réellement les sources après revue du plan :

```powershell
python tools/asset_pipeline/asset_vault_lifecycle.py update-plan `
  --current asset_manifests/catalogs/cc0_3d_all.json `
  --incoming E:\OpenRPG_AssetVault\metadata\catalog-next.json

python tools/asset_pipeline/download_cc0_3d.py `
  --source all --vault E:\OpenRPG_AssetVault --update

python tools/asset_pipeline/download_kenney_3d.py `
  --vault E:\OpenRPG_AssetVault --update
```

Le plan distingue ajouts, changements, retraits et entrées inchangées. Le gate simule le transport :
il ne redéclenche pas le miroir complet.

Les 50 packs 3D gratuits Kenney se téléchargent séparément :

```powershell
python tools/asset_pipeline/download_kenney_3d.py `
  --vault E:\OpenRPG_AssetVault `
  --jobs 6 `
  --catalog-output asset_manifests/catalogs/kenney_3d_packs.json
```

L’adaptateur refuse automatiquement une page ne déclarant pas `Creative Commons CC0` ou ne
fournissant pas un ZIP officiel.

## Désinstaller une acquisition locale

La commande suivante ne modifie rien sans `--apply` :

```powershell
python tools/asset_pipeline/asset_vault_lifecycle.py uninstall `
  --vault E:\OpenRPG_AssetVault `
  --catalog asset_manifests/catalogs/cc0_3d_all.json `
  --id ambientcg:3DApple001

python tools/asset_pipeline/asset_vault_lifecycle.py uninstall `
  --vault E:\OpenRPG_AssetVault `
  --catalog asset_manifests/catalogs/cc0_3d_all.json `
  --id ambientcg:3DApple001 `
  --apply

python tools/asset_pipeline/asset_vault_lifecycle.py report `
  --vault E:\OpenRPG_AssetVault
```

L’application vérifie à nouveau tous les hashes puis déplace l’acquisition sous
`cache/uninstalled/<plan_id>/`. Elle produit un reçu et un registre v1. Il n’existe aucune purge
automatique : les octets restent récupérables tant qu’une politique de rétention n’a pas été choisie.

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
extraits.

## Normaliser, prévisualiser et auditer un modèle

Le CLI prépare le manifeste et une requête Blender typée ; le worker n’accepte aucun script fourni
par l’asset. Il garde le Vault en lecture seule et n’écrit que sous la racine du workspace :

```powershell
dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- `
  assets prepare . artifacts/asset-workspace `
  --id ambientcg:3DApple001 --kind model --vault E:\OpenRPG_AssetVault

dotnet run --project tools/Forge.Cli/Forge.Cli.csproj -- `
  assets roundtrip . artifacts/asset-workspace `
  --id ambientcg:3DApple001 --vault E:\OpenRPG_AssetVault

python tools/asset_pipeline/blender_worker.py `
  --request artifacts/asset-workspace/assets/roundtrip/96ce5ad8-a1e9-8255-99ce-11cf8d593506.worker-request.json `
  --response artifacts/asset-workspace/assets/roundtrip/worker-response.json `
  --source-root E:\OpenRPG_AssetVault `
  --workspace-root artifacts/asset-workspace `
  --blender 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe'
```

Une exécution réussie publie depuis des fichiers temporaires `model.glb`, `model.preview.png` et
`model.quality.json`. La miniature transparente fait 512 × 512 ; le worker retire les métadonnées
variables du PNG afin que deux rendus identiques aient le même SHA-256. Le rapport inventorie
géométrie, triangles, matériaux, textures, dimensions, normales, collision et LOD, puis expose les
scores `license`, `sourceTrust`, `technical`, `visual`, `performance`, `metadata` et `overall`.
Une erreur structurelle conserve le rapport mais ne remplace pas le dernier GLB ou aperçu valide.
Blender est lancé avec l’auto-exécution désactivée, y compris pour les fichiers `.blend` externes.

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

## Mesurer la livraison complète

Après export et smoke runtime, `assets delivery-report` consolide catalogue, Vault, contenu utilisé,
taille du module, licences, intégrité et performances. La commande reproductible et les mesures de
référence sont documentées dans
[`AV5_DELIVERY_AND_EXTENSION.md`](AV5_DELIVERY_AND_EXTENSION.md). Pour ajouter une source ou un pack,
suivre [`ASSET_SOURCE_AND_PACK_EXTENSION.md`](ASSET_SOURCE_AND_PACK_EXTENSION.md) avant de modifier
le plafond du catalogue.
