# Kit d'assets R1-063

Ce dossier contient uniquement l'entree gouvernee du premier kit artistique de Visual Valley.
Les `.blend`, GLB, rendus et rapports generes restent dans un dossier de preuve hors Git tant que la
revue humaine et la politique de publication ne sont pas acceptees.

Le manifeste `asset-kit.v1.json` fixe les identites, dimensions, seeds, budgets LOD, collisions,
direction artistique et provenance. Le worker Blender doit refuser un dossier de sortie existant et
produire :

- un `.blend` reconstructible ;
- un GLB separe par asset avec `LOD0`, `LOD1`, `LOD2` et collisions nommees ;
- des UV et materiaux Principled PBR relies a des PNG 512 px `base_color`, `roughness` et
  `normal`, empaquetes dans le `.blend` et embarques dans les GLB ;
- des silhouettes alpha organiques et des nervures generees pour les feuilles de chene et les
  aiguilles de pin ;
- huit vues de turntable par asset et une planche de comparaison ;
- un manifeste de sortie avec nombres de sommets/triangles, dimensions et SHA-256 ;
- un rapport de limites qui distingue preuve Blender, integration Bevy et qualification artistique.

Le kit ne consomme aucun fichier Cadle, aucun asset extrait d'un jeu commercial et aucun service
reseau. La licence du manifeste reste `LicenseRef-OpenRPG-Forge-Project-Generated` et son statut
`internal_gate_proof_only` jusqu'a une decision explicite du proprietaire.

Exemple de construction :

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background `
  --python scripts\blender\Build-VisualValleyAssetKit.py -- `
  --manifest examples\visual_valley\asset-kits\r1-063\asset-kit.v1.json `
  --output-dir E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-run1
```

Le worker n'autorise pas l'ecrasement d'une preuve existante.

Deux constructions independantes se valident avec `Validate-VisualValleyAssetKit.py`. Le controle
reimporte chaque GLB, verifie les noms, UV, trois images PBR par materiau de rendu, LOD et
collisions, puis compare les pixels decodes. Les GLB, statistiques et 33 PNG de texture doivent etre
strictement identiques. Pour les rendus Eevee FHD, le contrat borne uniquement une variance GPU
localisee : au plus 24 pixels et 64 canaux modifies, huit pas 8 bits au maximum par canal, et une
moyenne absolue globale inferieure ou egale a `1e-7`. Toute variation diffuse ou structurelle est
donc rejetee. Les hashes des conteneurs `.blend` et des PNG de rendu restent informatifs ; ceux des
textures sont obligatoirement identiques.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background `
  --python scripts\blender\Validate-VisualValleyAssetKit.py -- `
  --primary-dir E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-run-a `
  --rebuild-dir E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-run-b `
  --output-report E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-validation.v1.json
```

## Apercu runtime gouverne

`runtime-preview.v1.json` ne contient aucun asset. Il fixe seulement l'identite et la revision du
kit, les hashes du WorldSketch, du terrain sculpte et du manifeste Blender, la camera de validation
et des placements stables. Le runtime refuse un rapport ou un GLB divergent, puis charge les GLB
directement depuis le dossier de preuve externe : aucun asset Blender ou Cadle n'est copie dans
Forge.

```powershell
cargo run -p forge_runtime -- `
  --world-sketch-visual-valley `
  --asset-kit-build E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-run-a\asset-kit-build.v1.json `
  --asset-kit-preview examples\visual_valley\asset-kits\r1-063\runtime-preview.v1.json `
  --capture C:\Temp\r1-064\hero-runtime.png `
  --capture-frame 300 `
  --metrics C:\Temp\r1-064\metrics.v7.json `
  --metrics-warmup-frames 180 `
  --exit-after-frames 420
```

Cette projection est un cache runtime reconstructible. Elle ne qualifie ni la retopologie humaine,
ni la conservation de retouches Blender lors d'une regeneration, ni la publication du kit.

`runtime-preview-authored.v1.json` conserve les memes placements et epingle en plus le SHA-256
du descripteur de source corrigee de la preuve `r1-063-authored-preservation-20260905-b`.
Il est destine au rapport `rebuild-1/asset-kit-build.v1.json` de cette preuve externe.
Le contrat Rust exige une provenance de correction explicite, une source liee au manifeste,
des IDs corriges connus et uniques, et le meme hash de descripteur dans l'apercu. Un apercu ancien
ou lie a une autre source est refuse. Les hashes des GLB restent controles sur leurs octets.
Le `.blend` et son descripteur sont des sources de reconstruction, pas des dependances runtime.
Ce chemin accepte seulement le candidat a revue humaine encore `pending` ; il ne qualifie aucune
promotion ou publication.
