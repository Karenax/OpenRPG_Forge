# Tidewater — décodage navigateur des textures scanned

Les douze JPEG acquis des troncs, branches et coquillages sont désormais
convertis en RGBA par la fonction `loadPixels` littérale de ScannedDebris,
source figée 1438b1ab. L'acquisition emploie createImageBitmap, OffscreenCanvas
2d et getImageData dans Chromium 154 sous Windows. Le hook de remplacement
headless `__debrisImage` n'est pas défini ; aucun décodeur Pillow/macOS sips
n'est substitué au chemin navigateur. Les deux profils ICC déclarés sRGB des
albedos de branches restent présents dans les JPEG servis au navigateur.

`capture-debris-browser-pixels.mjs` vérifie HEAD propre, le SHA du contrat
ScannedDebris et chaque JPEG retenu. Il expose uniquement ces images et la
fonction source extraite sur un serveur loopback temporaire. Les POST de
capture ont un token de session, un contrôle Origin, une liste fermée de noms,
une limite exacte de 4 194 304 bytes et un refus d'écrasement. Le serveur
ferme après les douze captures et le reçu final. Le source canonique est
contrôlé à nouveau et reste inchangé.

Les sessions 36293 et 78685 terminent code 0. Deux sorties neuves v1/v2,
`E:/OpenRPG_Forge/.tidewater-integration/debris-browser-pixels-20261005-v1`
et v2, sont identiques bytes pour bytes, descripteur et fonction compris.
Le navigateur montre PASS 12/12 pour la première acquisition ; les fichiers
réels des deux acquisitions sont ensuite relus indépendamment.

`verify-debris-browser-pixels.py` vérifie les références JPEG, provenance,
tailles et SHA des douze payloads, alpha opaque, absence de sorties plates,
ordre de lignes top-first et égalité des deux acquisitions. Reçu
`debris-browser-pixels-20261005-repeat.json` : PASS, 50 331 648 bytes RGBA.

- descripteur : 729d37b13d5f3635dcd8030cb9666e89e1aedcd0047a9c09e4a054740b5a2c4b ;
- fonction source normalisée LF : 64d0fe559f3ed1b6378fab694d5df716be1ae0bdd9b255ca4afb50aea3ce10ff.

Cette preuve porte sur le décodage source et sa reproductibilité dans ce
navigateur. Elle ne couvre pas d'autres navigateurs, les atlas 2×2, les mips
GPU, les matériaux scanned, les contacts Rapier, les gestes Studio ou la
parité visuelle complète. Les payloads serviront au constructeur d'atlas et
de mips source, puis au matériau Rust ; aucune texture plate n'est adoptée.

En parallèle, le check natif 19794 termine code 1 sur E0596 dans le nouveau
bootstrap Debris : le guard d'Assets::get_mut doit être mutable pour modifier
sa base. Ce point est corrigé et le check de l'exemple seul est relancé dans
48006. Ce run reste pending lors de cette note ; le test Rapier perdu 10107
reste sans verdict, aucun PASS physique n'est déduit du check des types.
