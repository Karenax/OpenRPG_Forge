# Provenance des icônes Forge Studio

## Source autoritative

- fichier : `apps/forge_studio/app-icon.svg` ;
- origine : création propre à OpenRPG Forge pour Gate 0 ;
- SHA-256 : `490e588d264424fdaab8736561fc79205d477317adc2b81e15aeb09d1f922870` ;
- contenu Cadle ou tiers : aucun.

Le SVG est la seule source graphique à modifier. Les 52 fichiers de ce dossier sont des dérivés de
packaging produits par Tauri CLI `2.11.4` :

```powershell
cargo tauri icon apps/forge_studio/app-icon.svg --output apps/forge_studio/icons
```

## Contrôle de reproduction du 30 août 2026

La commande a été rejouée vers un dossier local ignoré, puis chaque chemin et SHA-256 a été comparé :

- 52 chemins attendus et 52 chemins reproduits ;
- 51 fichiers strictement identiques octet par octet ;
- `icon.icns` diffère au niveau du conteneur généré, bien qu'il provienne du même SVG et de la même
  commande. Son empreinte binaire n'est donc pas considérée reproductible par ce protocole.

Les icônes sont des assets Forge suivis pour les shells desktop/mobile Tauri. Elles ne proviennent ni
du snapshot Cadle, ni d'un pack externe, et ne sont pas des preuves visuelles de Cadle.
