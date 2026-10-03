# Tidewater — sortie d'eau par l'échelle du ponton

## Portée

La promenade native peut maintenant rejoindre le ponton depuis l'eau par
l'échelle placée dans la carte Tidewater. Le comportement utilise le même
joueur Forge, la même entité, son contrôleur cinématique Rapier et les hauteurs
d'eau GPU. Le visuel de l'échelle vient du village déjà intégré ; un composant
logique séparé porte la zone d'interaction et la sortie sur le pont.

Référence gelée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.

- `src/player/Player.js`, SHA-256
  `1838be4d10e6e4df64fb07b709d75c1c106d02c6b5b6beac24b6e3b644900bbb` ;
- `src/world/Pier.js`, SHA-256
  `d29093d046dcbe6ffb92798320fa9502f33d65e070d816d8949eee17f2469f06` ;
- licence MIT, SHA-256
  `bf7d406711a4cbf9af0b9eb5f2cf464ae88053e999e0e64a92b3423494dd8699`.

Le module dérivé `tidewater_locomotion.rs` conserve l'attribution Tidewater.

## Comportement porté

Les valeurs viennent directement de la source : centre XZ `(62,32 ; 35,10)`,
rayon strict `1,1 m`, vitesse verticale `1,6 m/s`, sommet logique `1,95 m` et
sortie `(61,12 ; 2,30 ; 35,10)`. Espace ou W déclenche la montée quand le joueur
nage dans le rayon. Au franchissement du seuil `sommet - 1 m`, le joueur passe
en marche, retrouve sa capsule de marche, annule sa vitesse et rejoint le pont.

La source exportée a perdu les boîtes sémantiques `walkable=true, solid=false`
de `Pier.js` : seul leur visuel subsistait dans le maillage du village. Une
première exécution native, `native-ladder-001`, a donc correctement trouvé
l'échelle mais est restée bloquée au niveau de la surface, la correction de
flottaison reprenant la main à chaque pas. L'adaptateur mémorise maintenant une
montée engagée jusqu'à sa fin. Cette correction rend l'intention de la source
jouable avec la géométrie exportée ; ce n'est pas une reproduction de son ordre
d'exécution défectueux dans cette configuration.

Le HUD possède le prompt contextuel exact `Space — Climb ladder`. Le présent
lot vérifie son état logique et son texte compilé, mais aucune capture dédiée ne
qualifie encore son rendu à l'écran.

## Vérifications CPU et compilation

Six tests ciblés d'échelle passent : rayon strict, vitesse, sortie exacte,
W maintenu sur le vrai joueur Rapier, absence de capture d'un marcheur et
resynchronisation de la pose et de la capsule backend avant le balayage KCC
suivant. Le dernier point impose une barrière d'un pas fixe après la
téléportation de sortie afin que Rapier reparte de l'ancre et de la capsule de
marche. Le test exact passe en 0,02 s, les six tests d'échelle en 0,01 s et les
huit tests du joueur Tidewater en 0,10 s. Journaux :

- `ladder-backend-sync-test-002.log`, SHA-256
  `d22cd1988535add7ae2991294715524dccdcdf81a3210f388d30af5894ae1da9` ;
- `ladder-backend-sync-ladder-tests-001.log`, SHA-256
  `b984fd73cc8fa527959af907d8545f4082dd0d91e9ea9c4832286f47bd11fad6` ;
- `ladder-backend-sync-player-tests-002.log`, SHA-256
  `36cdca10c7cdeec43f689dc13c7aa1e9f6df081e2c628726964df0cec2c3afcd`.

- `cargo clippy -p forge_engine --lib --no-deps -- -D warnings` : PASS ;
- Clippy strict de l'exemple `tidewater_plant_probe` : PASS ;
- build natif de l'exemple : PASS en 6 min 46 s.

## Parcours natif

`native-ladder-003.walk.json` termine avec `pass: true` en 115 pas. Le joueur
démarre à X 63,80, hors du rayon, entre en nage, s'approche avec la vraie
commande de déplacement, détecte l'échelle, monte puis se stabilise sur le
platelage.

- une entrée en nage, une montée et un retour en marche ;
- transition hors rayon vers rayon détectée ;
- backend Rapier présent et handle `[256, 0]` conservé ;
- hauteur d'eau GPU finale demandée à `(61,12 ; 35,10)`, pas 364,
  temps 3,9129903 s ;
- sortie demandée exacte à Y 2,30 m, puis appui stable à Y 2,276103 m sur le
  maillage du pont, soit 0,023897 m sous l'ancre de sortie ;
- contact au sol final, aucun timeout.

Le reçu n'est écrit qu'après l'événement Bevy `ScreenshotCaptured`. Il confirme
`capture_event_received: true`, `screenshot_nonempty: true`, 2 489 971 octets,
aucune erreur et aucun timeout. La capture FHD finale a été inspectée : la
caméra se trouve sur la tête du ponton, avec l'océan animé, les accessoires du
quai, la côte et la végétation visibles, sans clipping évident. Empreintes
SHA-256 :

- `native-ladder-003.png` :
  `4cd14fcdcd0d46a6975de9daf377acd48eeba212f10aa0211846848a7d67d4e0` ;
- `native-ladder-003.walk.json` :
  `bc3736e94754cb6d9075865ec5486899fbc503d9a9d3bb4bd4e2e306cce6dd86` ;
- `native-ladder-003.water-query.json` :
  `3ebe9e70453177f15d4db11cf279381fefc9733e9ddef338bbd0fbdb131521c`.

La régression `native-ladder-water-speed-125-001` rejoue ensuite le même
parcours avec l'échelle temporelle de l'eau réglée à `1,25×`. Elle passe en
114 pas : une montée, un retour en marche et un appui final au sol sur le
platelage. Rapier est présent, la sortie reste à `0,0238969 m` de l'ancre
attendue et le reçu confirme l'événement de capture, un PNG non vide de
2 479 811 octets, aucune erreur et aucun timeout. Empreintes SHA-256 :

- `native-ladder-water-speed-125-001.png` :
  `a11f56372ac9c4f89adc1216cb6e202d6d1ba04e28461455cae54fbeeb448c9e` ;
- `native-ladder-water-speed-125-001.walk.json` :
  `8a901382472f1c998dbef143b0a924fbd51279e96698fdb5f16791cff6a181df` ;
- `native-ladder-water-speed-125-001.water-query.json` :
  `3ebe9e70453177f15d4db11cf279381fefc9733e9ddef338bbd0fdbdb131521c` ;
- `native-ladder-water-speed-125-001.log` :
  `7787aff3e1f97ed9b3e25aa056756e2058b5cddc160ddf869df340d480225245`.

La régression de marche `native-walk-ladder-regression-001` passe sur
8,994282 m, avec 1 835 pas au sol et un backend stable. Sa capture finale a été
inspectée. SHA-256 du reçu :
`cfe871818035ce68e375fe2d9484fb0df3a5b5ff1bcc711ade0641f93a1f0021`.

La régression de nage `native-swim-ladder-regression-001` passe en 3 739 pas :
entrée, flottement, plongée, neutralité, remontée, surface et retour en marche.
Elle conserve un backend stable, mesure 0 m de dérive neutre, 0,059203 m de pas
vertical maximal de caméra et 885 pas au sol. La capture finale a été inspectée.
SHA-256 du reçu :
`5cee3c2accd4a9fed1b03a762a5fff4de32810eb4f4316fa5757d388884f59eb`.

Les avertissements de canal readback fermé surviennent à l'arrêt, après les
PASS et les écritures. Les processus se terminent avec le code zéro.

## Limites

Ce lot qualifie la sortie de l'eau vers le pont par commandes injectées dans
Forge. La descente pont vers eau n'est pas portée. Le maillage visible reste un
collider solide alors que la source séparait le décor des barreaux logiques non
solides ; la projection fidèle de ces proxies reste à faire.

`TidewaterLadder` est modifiable dans l'ECS au runtime, mais il n'a pas encore
d'identifiant documentaire, sérialisation, version, validation de commande,
save/open, Undo/Redo ou gizmo Studio. L'échelle et la carte ne sont donc pas
encore qualifiées comme éditables dans Studio.

La preuve injecte les commandes au contrôleur ; elle ne qualifie pas le clavier
au niveau du système. WebGPU dans un navigateur, distribution autonome et revue
visuelle propriétaire restent ouverts. Les artefacts sont locaux sous
`E:/OpenRPG_Forge/.tidewater-integration/`. Aucun commit ni push.
