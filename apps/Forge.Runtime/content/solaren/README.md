# Solaren, Les Vallées Libres

Ce workspace est le premier morceau jouable du jeu **Solaren**. La zone canonique `Le Val d'Aubeciel`
est une carte extérieure de 512 × 512 m avec relief révisable, matériaux peints, route, seize couches
de biome et un village composé de quatre maisons modulaires, d'un marché, de clôtures et d'habitants.
La prairie combine 30 126 instances, treize couches animées par le vent et des accents saisonniers.

La Crypte des Cendres reste séparée dans `../crypt-of-ashes` et conserve son rôle de module de test.

## Lanceur solaire

Le profil de distribution référence `branding/launcher-solaren-aubeciel-v1.png` et
`branding/solaren-aubeciel-calm-v1.wav`. Le premier reprend la lumière, la végétation, les pierres
claires, les colombages et les toits de la zone réelle ; le second est une composition calme originale
de 64 secondes. Le lanceur démarre la musique en boucle et expose Play/Stop ainsi qu'un volume partagé
avec le réglage Musique. Les sources, références, prompts et SHA-256 sont consignés dans
`branding/ASSET_PROVENANCE.md`.

Pour régénérer uniquement la piste déterministe puis construire un export de jeu isolé :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Generate-SolarenLauncherMusic.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-GameDistribution.ps1 `
  -Workspace apps/Forge.Runtime/content/solaren `
  -OutputDirectory artifacts/verification/solaren-windows-x64
```

## Sources CC0

- Quaternius, Medieval Village MegaKit Standard — maisons modulaires et accessoires de village ;
- Quaternius, Fantasy Props MegaKit Standard — étals, bancs, tonneaux et cagettes ;
- Quaternius, Stylized Nature MegaKit Standard — arbres, buissons, rochers, fleurs et herbes ;
- Quaternius, Modular Character Outfits - Fantasy Standard — silhouette du paysan ;
- Quaternius, Universal Animation Library 2 Standard — six clips humanoïdes embarqués.

Tous ces éléments proviennent du pack logique `medieval`, sous licence `CC0-1.0`. Les fichiers du
Vault restent immuables. Les manifestes gouvernés, hashes SHA-256 et provenances sont enregistrés dans
`assets/asset-manifests.json`.

## Reconstruction et lancement

```powershell
pwsh -NoProfile -File scripts/Build-SolarenContent.ps1
pwsh -NoProfile -File scripts/Run-Solaren.ps1
```

Le workspace présent dans ce dossier est la source de vérité éditable par Forge Studio. Le premier script
le valide avec les contrats canoniques puis produit un package reproductible ; il ne régénère ni la carte,
ni les réglages, ni les assets. Les opérations de préparation d'assets restent des opérations génériques du
pipeline et leur provenance reste portée par les manifestes. Le second script compile le runtime et charge
explicitement Solaren via `--workspace`, sans changer le module de test lancé par `Run-Prototype.ps1`.
