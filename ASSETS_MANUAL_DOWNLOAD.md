# Archives manuelles Itch.io importées

État au 24 août 2026. Les neuf lots `AV7-M1` à `AV7-M9` ont été téléchargés par l'utilisateur depuis
les pages officielles, sans contournement du flux Itch.io, puis importés dans le Vault externe.

Les archives déposées sous `E:\OpenRPG_AssetVault\inbox\manual\` restent intactes. Les originaux
immuables, métadonnées, hashes et copies normalisées sont produits par :

```powershell
python tools/asset_pipeline/import_manual_cc0_packs.py --vault E:\OpenRPG_AssetVault
```

Le neuvième lot a été fusionné sans réextraire les huit précédents :

```powershell
python tools/asset_pipeline/import_manual_cc0_packs.py `
  --vault E:\OpenRPG_AssetVault `
  --inbox 'E:\3d unity' `
  --pack universal-animation-library-2-standard-2026-06-16 `
  --merge-existing
```

| Lot | Source | Pack | Archive gratuite | État |
|---|---|---|---|---|
| `AV7-M1` | Quaternius | Universal Base Characters | `Universal Base Characters[Standard].zip` | importé, CC0 vérifiée |
| `AV7-M2` | Quaternius | Modular Character Outfits - Fantasy | `Modular Character Outfits - Fantasy[Standard].zip` | importé, CC0 vérifiée |
| `AV7-M3` | Quaternius | LowPoly Animated Easy Enemies | `Easy Animated Enemy Pack - Jan 2019.zip` | importé, CC0 vérifiée sur la page officielle |
| `AV7-M4` | KayKit | Forest Nature Pack | `KayKit_Forest_Nature_Pack_1.0_FREE.zip` | importé, CC0 vérifiée |
| `AV7-M5` | KayKit | Character Animations | `KayKit_Character_Animations_1.1.zip` | importé, CC0 vérifiée |
| `AV7-M6` | KayKit | Fantasy Weapons Bits | `KayKit_FantasyWeaponsBits_1.0_FREE.zip` | importé, CC0 vérifiée |
| `AV7-M7` | KayKit | RPG Tools Bits | `KayKit_RPGToolsBits_1.0_FREE.zip` | importé, CC0 vérifiée |
| `AV7-M8` | KayKit | Resource Bits | `KayKit_ResourceBits_1.0_FREE.zip` | importé, CC0 vérifiée |
| `AV7-M9` | Quaternius | Universal Animation Library 2, version Standard avec root motion | `Universal Animation Library 2[Standard].zip` | importé, CC0 interne et page officielle vérifiées |

## Collection Médiéval

`asset_manifests/packs/medieval.json` regroupe les cinq archives fournies sous un pack visible
**Médiéval** dans le Studio et l’Asset Atlas. Fantasy Props, Medieval Village, Modular Character
Outfits et Stylized Nature étaient déjà présents avec exactement les mêmes SHA-256 ; ils ont été
réutilisés. Universal Animation Library 2 est la seule nouvelle extraction.

La collection expose 365 modèles logiques locaux issus de 1 136 représentations glTF/GLB/FBX/OBJ,
pour 728 387 757 octets d’archives épinglées. Les cinq fichiers de licence internes déclarent
explicitement CC0 1.0 Universal.

Les niveaux `Pro`, `Extra` et `Source` restent exclus. Aucun nouveau pack Kenney, Synty, Fab ou
contenu sous licence CC-BY, CC-NC, CC-SA ou propriétaire n'a été importé.
