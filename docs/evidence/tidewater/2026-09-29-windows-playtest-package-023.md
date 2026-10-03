# Paquet Windows Tidewater 023 — intégrité de distribution

Le candidat Windows `023` a été construit le 29 septembre 2026 depuis les
binaires déjà validés de `021` et le bundle source
`preview-scene-bundle-clear-004`, avec le script de packaging qui répare le
manifeste interne après redaction des chemins locaux.

## Entrées et résultat

- Lanceur source : `OpenRPG-Forge-Aube-Tidewater-Win64-20260928-021/OpenRPG-Forge-Tidewater.exe`,
  SHA-256 `70b7ab4157ed862333ca7df6028a984e3774886596e67cf671688754a8e89d7c`.
- Runtime source : `.../runtime/tidewater.exe`, SHA-256
  `cbbd3c9676436ab138deafb25d436947aa313ac8ca55218ce78c11283d30b722`.
- `LauncherSource` de collecte des notices :
  `E:/OpenRPG_Forge/OpenRPG_Forge-Launcher-Prototype/prototypes/forge_launcher`
  (`forge_launcher_prototype`).
- Dossier final :
  `E:/OpenRPG_Forge/.tidewater-integration/OpenRPG-Forge-Aube-Tidewater-Win64-20260929-023`.
- Archive finale : `...-023.zip`, 422 554 843 octets, SHA-256
  `e7a66a74ba6501fb1fce55d57426268e6c2134037b2fcb247c87454e435dfa4d`.

## Vérifications exécutées

- `OpenRPG-Forge-Tidewater.exe --verify-distributable` retourne
  `FORGE_LAUNCHER_AUBE_FRONTEND_READY` avant et après packaging.
- Le vérificateur interne `package-preview-scene.py --verify` accepte le
  bundle redigé : 1 542 fichiers, 295 672 652 octets, manifeste SHA-256
  `1fb44c51d22d58957e812acaa453b482172ad2b7c6efb543d273d38f8c66979a`.
- `data/scene/fixture.json` fait 58 562 octets et a le SHA-256
  `c96ad5b2abc4d7ffe17dc899733cd2bf4578264ad49f5dbb0f37c77d72ac2ba4`.
  Son entrée dans `data/preview-scene.json` porte exactement ces mêmes valeurs.
  Les trois chemins de captures locales ont été remplacés par
  `reference-capture-not-bundled`; aucun chemin `E:\\OpenRPG_Forge` ne reste
  dans la fixture distribuée.
- Le manifeste externe couvre 2 507 fichiers et 656 224 685 octets ; chaque
  taille et SHA-256 a été recalculé.
- Le ZIP contient 2 508 entrées (les 2 507 fichiers manifestés et
  `manifest.json`), avec CRC présent pour chaque charge utile. Son extraction
  indépendante dans `package023-extracted-check` a été comparée SHA-256 par
  SHA-256 aux 2 507 fichiers du paquet.

## Limite connue

La fixture conserve une `camera_reference` `view: "village"` (et le flag
`lighting_and_scene_not_matched: true`) alors que `camera_pose` est la pose
seaward. Cette métadonnée existante n'a pas été modifiée pendant la capture :
le paquet `023` est une correction d'intégrité de distribution, pas une
nouvelle paire de captures ou une qualification visuelle seaward.

Après la capture source, un parcours `--walk-smoke` a été lancé avec le runtime
du ZIP `023` extrait indépendamment dans `package023-extracted-check`, et ses
données extraites. Le reçu
`E:/OpenRPG_Forge/.tidewater-integration/package023-extracted-walk-smoke-20260929-001.walk.json`
indique `pass=true`, `cadence_pass=true`, `backend_stable=true` et 8,994 m ;
la capture PNG et le journal existent à côté. Le profil LOCALAPPDATA était
isolé sur E:, sans modifier celui du propriétaire. Ceci qualifie localement le
chargement et le parcours du paquet extrait, pas l'installation sur une autre
machine ni la parité visuelle. Le paquet `021` n'a pas été modifié ; aucun
commit ni push n'a été effectué.
