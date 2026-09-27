# Tidewater — normales corrigées et mipmaps GPU

Le prototype recalcule les canaux X/Z des normales macro à partir des hauteurs
effectives, avec la formule centrale de `TerrainBake.js` : différences sur deux
texels, normalisation en f64, quantification RGBA8. Les canaux roche/AO demeurent
les données source ; leur recalcul n'est pas inclus dans cet incrément.

Une texture partagée alimente désormais le matériau terrain et l'optique de
l'eau. Après une retouche de hauteur, onze passes de rendu sur le même appareil
Bevy/wgpu filtrent les mipmaps avec le shader de `Mipmaps.js`. Chaque passe lit
le niveau précédent avec échantillonnage linéaire. Le masque d'écriture RG et
`LoadOp::Load` préservent les canaux B/A déjà présents. Sans retouche, le chemin
conserve les douze niveaux acquis, sans recalcul ni remplacement des données.
La capture attend le signal d'encodage des passes, exécutées avant la caméra.

## Validation native

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/` :
`relief-normal-render-proof-002`, sur les workspaces relief/Undo existants.
Compilation et Clippy strict du prototype réussis, capture modifiée inspectée.
Binaire SHA-256 :
`bfc35bda49c335c1307a5e4b7c8a740caf84cec1112d6eef6a83866611b7e33a`.

`scripts/tidewater/verify-authored-normals.py` calcule indépendamment les texels
depuis le raster source et les corrections de l'archive : 11462 texels changent,
hash de base RGBA8 exact annoncé par Rust :
`4871b2c813eb39b1b3fe66507a215bc0e0ff6eb875589b65b1f487e4d6c30c58`.
Les onze passes GPU sont exécutées une seule fois. Le shader utilise le filtre
source ; l'égalité de chaque niveau GPU avec un oracle séparé reste à qualifier.

Par rapport au test précédent `relief-shadow-render-proof-001`, les sorties de
profondeur d'ombres proches et d'ombre lointaine restent byte-identiques.
L'image change sur 26169 pixels, maximum 37/255, moyenne de canal 0,025624/255.
Ces différences incluent une éventuelle variation d'embruns ; elles ne sont pas
un verdict de fidélité matérielle absolue. Rapport : `normal-verification.json`.

L'annulation redonne exactement `native-canonical-vegetation-001.png`, SHA-256
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`, zéro pixel différent.
Les 203 plantes continuent de suivre la bosse dans l'état modifié.

## Limites

Les mipmaps dérivés sont exécutés nativement mais pas encore comparés niveau par
niveau à la référence. AO, roche, splats, masques côtiers, simulation aquatique,
herbe, bâtiments et collisions doivent encore suivre le relief effectif.
Le partage de normales avec l'optique ne qualifie pas à lui seul la réaction
physique de l'eau. Le prototype charge un état sauvegardé au démarrage ; édition
interactive, rechargement à chaud et parcours Studio restent ouverts. Aucun
verdict de parité complète, commit ou push.
