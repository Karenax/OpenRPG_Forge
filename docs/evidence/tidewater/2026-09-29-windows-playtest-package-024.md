# Paquet Windows Tidewater 024 — vérification indépendante du ZIP extrait

Le candidat `OpenRPG-Forge-Aube-Tidewater-Win64-20260929-024.zip` a été
contrôlé depuis une extraction neuve sur E:, sans réutiliser le dossier assemblé
par le script de distribution et sans modifier les paquets précédents.

## Intégrité de distribution

- ZIP : 475 747 352 octets, SHA-256
  `cf237594ba666614efc0037b4a1f280f4cc830a04422fd1aeec9960d291ea603`.
- 2 587 entrées uniques ; lecture CRC intégrale réussie, sans chemin absolu ni
  traversée `..`. Les 2 586 charges utiles du manifeste externe totalisent
  773 088 433 octets. Chaque taille du ZIP et chaque taille et SHA-256 après
  extraction correspondent au manifeste `forge.tidewater.windows-playtest.v1`.
- Extraction neuve :
  `E:/OpenRPG_Forge/.tidewater-integration/package024-extracted-qa-001/OpenRPG-Forge-Aube-Tidewater-Win64-20260929-024`.
  Elle contient exactement 2 587 fichiers, y compris `manifest.json` (SHA-256
  `6dde3059719ab5c3f95e1554b88392b8aae74b971f1a85e87ce1132ca6d80747`).
  Le bilan automatisé est `E:/OpenRPG_Forge/.tidewater-integration/package024-independent-qa.json`.
- Le lanceur extrait (SHA-256
  `70b7ab4157ed862333ca7df6028a984e3774886596e67cf671688754a8e89d7c`)
  retourne `FORGE_LAUNCHER_AUBE_FRONTEND_READY` à
  `OpenRPG-Forge-Tidewater.exe --verify-distributable`. Le runtime extrait a
  le SHA-256 `c295046b0bb2d59daea9c5ec5b49fb8c6967bc6194407cb5661cff18be80790f`.

## FishStand et parcours jouable

`prepare-fish-stand-bundle.py` vérifie le manifeste interne extrait (SHA-256
`c79086369003a4997d64598d878dcaa56a79a714f5b532b59c717c6c4addeef6`),
ses 1 621 fichiers, les sept atlas FishStand et la géométrie de 32 333 sommets /
40 259 triangles. `data/scene/fixture.json` référence
`../fish-stand/fish-stand.json` avec le statut honnête
`bevy_base_array_shader_not_source_parity`.

Le runtime extrait a été lancé depuis ce même dossier avec `data/scene`,
`--water-spray-clouds data/ocean data/shore data/spray data/clouds`,
`--workspace-vegetation data/workspace e620dd19-ead9-5913-a6d6-0f93109148c0`,
`--building-package data/buildings a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6`
et `--walk-smoke`. `LOCALAPPDATA` et `TEMP` pointaient vers un profil isolé sur
E:. Code retour 0 ; reçu `pass=true`, `cadence_pass=true`,
`backend_stable=true`, distance 8,993723869 m et 304 ticks au sol. La capture
`E:/OpenRPG_Forge/.tidewater-integration/package024-extracted-walk-smoke-001.png`
(SHA-256 `b34c578672108fa16eafbdf96ece8cc14b01d733d018cd66e57b006c62628dcc`)
a été inspectée : le stand est visible à gauche de la plage. Le reçu `.walk.json`
et le journal `.log` portent le même préfixe. Aucun processus de cette extraction
ne restait actif après le test.

## Données des réglages graphiques

`LISEZ-MOI.txt` décrit `H`/`F10`, la molette, `R` et le profil
`%LOCALAPPDATA%/OpenRPG Forge/Tidewater/graphics.json`. Le binaire extrait
contient les quinze libellés du menu et `graphics.json`. Pour vérifier la
lecture du profil sans simuler une interaction clavier, deux captures statiques
ont été produites avec `FORGE_TIDEWATER_STATIC_POSTFX=1` depuis la même caméra :

- profil par défaut : `package024-static-default-001.png`, SHA-256
  `7e7c4943b418dad0010995ca0115b1705004b4f82969612029df0faa83f64b4a` ;
- profil isolé sur E: avec `auto_exposure=0` et `exposure_ev=-1.0` :
  `package024-static-custom-001.png`, SHA-256
  `2a44d129e4ca9dc1ce9168545c682bd9b3bcb56c6e815fe3648e94a5c5a790b0`.

Les deux captures ont été inspectées. Leur luminance moyenne est 126,638 puis
93,211, avec 33,428 de différence absolue moyenne. Le reçu
`E:/OpenRPG_Forge/.tidewater-integration/package024-static-graphics-profile-ab.json`
enregistre le calcul. Cela vérifie que le rendu statique lit les paramètres du
profil distribué. Le mode `--walk-smoke` ne charge volontairement pas le module
graphique interactif ; il vérifie la locomotion et le chargement de la scène.
L'ouverture du panneau par `H`/`F10`, son défilement et sa sauvegarde par clic
n'ont pas été exercés dans ce lot.

Ces preuves sont locales à cette machine. Elles n'établissent ni une installation
sur un autre PC ni l'équivalence visuelle avec Tidewater source. Aucun commit ni
push n'a été effectué.

La collecte de notices du paquet 024 signale encore 42 dépendances Rust sans
texte de licence copié (36 crates publiées et six crates Forge locales).
L'index embarque leurs noms, versions et expressions SPDX, mais la revue des
textes manquants reste ouverte avant une diffusion publique. Un correctif
ultérieur du collecteur retrouve le fichier `SDL_GameControllerDB/LICENSE`
de `gilrs`; il n'a pas modifié cette archive déjà vérifiée.

## Recontrôle borné des notices — 2026-10-03

Les sources locales des 36 crates publiées signalées sans texte dans l'index
historique ont été examinées à leurs versions exactes. Toutes sont présentes
dans le registre Cargo local. La recherche récursive des fichiers nommés
LICENSE, LICENCE, COPYING ou NOTICE (et leurs suffixes) retrouve uniquement
`gilrs-0.11.2/SDL_GameControllerDB/LICENSE`, SHA-256
`97f35b302b361680ec1e891e95d2d52097bb95abff361434916d99dc1305f127`.
Les 35 autres archives ne contiennent aucun fichier correspondant à ce filtre.
La notice retrouvée concerne un composant tiers : sa présence ne prouve pas
que les obligations de licence de la crate entière sont couvertes.

Le reçu borné est
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v8/package024-missing-notices-source-audit.json`.
Il conserve le hash de l'index historique et les chemins exacts examinés.
Il ne revalide ni le graphe complet actuel, ni les six crates locales, ni les
textes éventuellement déclarés sous un autre nom dans les manifestes.
Les quatre tests de sélection du collecteur passent avec
`python -m unittest discover -s scripts/tidewater/tests -p test_collect_rust_notices.py`.
Le paquet 024 et son extraction restent inchangés ; la revue des notices reste
ouverte pour la prochaine distribution.

Une nouvelle exécution du collecteur actuel, distincte de l'archive024, termine
avec le code0 sur les manifestes Rust et
`OpenRPG_Forge-Launcher-Prototype/prototypes/forge_launcher/Cargo.toml` :
596 dépendances,48 entrées sans texte copié. Les manifestes/locks racines et le
collecteur sont inchangés entre les deux contrôles. Aucun doublon nom/version
n'est observé dans cet index. Les notices et le reçu sont conservés dans
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v8/current-portable-notices-v1/`.
Cette collecte sous `cargo metadata --locked --offline --filter-platform
x86_64-pc-windows-msvc` ne qualifie pas le graphe d'un nouveau binaire réellement
construit ni les obligations de licence ; les48 entrées doivent encore être
revues. Elle ne remplace ni ne modifie l'index historique à42 entrées manquantes.
