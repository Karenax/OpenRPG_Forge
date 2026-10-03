# Tidewater — réglages, comparaison visuelle et FishStand (candidat 025)

La référence de mesure reste `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le site public a changé depuis cet épinglage. Son README décrit un moteur
WebGPU/WGSL propre au projet, sans Three.js. Les deux images de la demande
montrent un rendu source plus fini que notre prototype ; elles ne sont pas des
prises appariées à la caméra native. Les chiffres ci-dessous comparent donc la
référence figée, avec une pose et une météo nominalement identiques, et non la
version publique du jour.

## Réglages réellement exposés

Le menu natif `H`/`F10` possède quinze commandes reliées au rendu : adaptation,
occlusion ambiante, FXAA, TAA expérimental, exposition, bloom, saturation,
contraste, netteté, vignette, grain photo, brume marine, lumière rebond, flou de
mouvement et flare solaire. Les dix effets visibles dans la capture utilisateur
ont une valeur et des boutons `−`/`+` ; la molette parcourt le panneau et `R`
rétablit les valeurs par défaut. Le profil est lu et écrit dans
`%LOCALAPPDATA%/OpenRPG Forge/Tidewater/graphics.json`.

Captures GPU 1920×1080, panneau ouvert par un mode de preuve de capture statique :

- partie haute : `E:/OpenRPG_Forge/.tidewater-integration/fish-stand-graphics-top-009-prejpeg.png`, SHA-256 `6d17ea5334f63e28ec141bc693dd545b0d34d203a55b8d5b8d1c9118b8761037` ;
- partie basse : `E:/OpenRPG_Forge/.tidewater-integration/fish-stand-graphics-bottom-011.png`, SHA-256 `e45fa64d393ba9db75ee2fc6172ef15b3d4809786423842ad6619a2ec2501a9d`.

Ensemble, ces images rendent visibles toutes les lignes, y compris les dix
effets source. Elles prouvent le dessin du panneau et la lecture des valeurs,
pas le clic, le défilement à la molette ni la sauvegarde interactive. La
lecture d'un profil modifié et son effet sur l'image étaient déjà qualifiés
dans le [paquet 024](2026-09-29-windows-playtest-package-024.md).

## Scène et comparaison

Le bundle complet 011 contient 1 655 entrées manifestées ; son manifeste vaut
`c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335`.
Il inclut les cinq poissons source avec indices u32 corrigés et Joe : original
MIT conservé dans les sources, dérivé PNG pour le chargement Bevy. Les six
images PNG gardent les pixels RGBA décodés des JPEG ; le validateur Khronos
rapporte zéro erreur et trois avertissements hérités. Le shader du stand utilise
maintenant le repère tangent dérivé de la source pour ses normal maps. La
capture finale ne contient pas l'échec JPEG observé avec l'ancien bundle.

Capture de référence figée :
`E:/OpenRPG_Forge/.tidewater-integration/source-seaward-clear004-20260929-021753/seaward-clear004.png`.
Capture native sans panneau :
`E:/OpenRPG_Forge/.tidewater-integration/fish-stand-seaward-tbn-012.png`,
SHA-256 `0ef8e28a85c547ac5e27c972be8c858ab77b0a5aed264bf9fda4f2598ec9ce2b`.
Même pose nominale, 1920×1080, post-traitement/HDR/ACES et adaptation actifs ;
profil isolé avec les valeurs par défaut. Le reçu reproductible est
`E:/OpenRPG_Forge/.tidewater-integration/fish-stand-seaward-tbn-012.metrics.json`.
Luminance sRGB pondérée, échantillon tous les huit pixels :

| Région | Source | Forge 025 | Écart absolu moyen |
|---|---:|---:|---:|
| Ciel | 117,79 | 111,22 | 7,22 |
| Eau ouverte | 102,31 | 101,49 | 6,00 |
| Plage | 122,27 | 117,77 | 9,95 |
| Image entière | 127,14 | 125,65 | 8,32 |

La luminance globale proche ne prouve aucune identité visuelle : l'eau, le
sable et les arêtes sont plus durs côté natif, avec un horizon et des objets
légèrement décalés. Le stand est désormais présent et Joe se charge, mais le
matériau du bois reste trop sombre. Sur le rectangle du stand
`x=460..619, y=840..999` (tous les pixels), la moyenne est 79,06 dans la
source, 43,16 sur la première capture native et 49,76 sur la capture finale.
Le passage des normales en repère tangent corrige une erreur de calcul, mais
n'apporte pas à lui seul la réponse PBR/environnement source. La matière des
poissons reste également une approximation plane.

## Paquet Windows extrait

Archive locale :
`E:/OpenRPG_Forge/.tidewater-integration/OpenRPG-Forge-Aube-Tidewater-Win64-20260929-025.zip`,
488 913 773 octets, SHA-256
`776954254221c3d7603b7c9b180fcf66e610bca699707ccdc5c87d56bd534cb2`.
Le contrôle indépendant du ZIP a validé son CRC, ses chemins, l'absence de
liens symboliques et les 2 624 fichiers du manifeste externe (taille et SHA-256).
Le lanceur extrait passe `--verify-distributable` et porte le marqueur du
frontend Aube actuel. Le moteur extrait a passé `--walk-smoke` avec profil et
fichiers temporaires isolés sur E: : `pass`, `functional_pass`, `cadence_pass`,
`backend_stable` et `shadow_cadence_pass` sont vrais ; 303 ticks au sol sur 311
ticks en 8,99 minutes. Aucun `ERROR`, panic ou perte de périphérique GPU n'est
relevé. Deux avertissements de readback vers un canal fermé surviennent après
le reçu `TIDEWATER_WALK_PASS`, pendant la fermeture. La capture de cette
promenade est
`E:/OpenRPG_Forge/.tidewater-integration/package025-independent-qa-20260929-01/package025-extracted-walk-smoke-001.png`,
SHA-256 `a9ebabc77cc5335267663f7142cf1776d8e6d37799a4417b1cf5395c143476b5`.

## Limites ouvertes

Le TAAU temporel, certains traitements de brume et d'eau de la version
publique récente, l'éclairage PBR complet du stand, les matières des poissons,
les interactions avec Joe, la qualification des commandes à la souris et
l'édition de cet objet dans Studio restent à faire. Les FPS du site et de
l'exécutable ne sont pas comparables à partir de ces captures fixes. La licence
Rocketbox et la provenance de la conversion sont conservées avec le bundle ;
la collecte du candidat Windows 025 signale encore 41 dépendances Rust sans
texte de licence copié ; les exécutables ne sont pas signés. Il s'agit d'un
candidat local testé depuis l'archive, pas d'une diffusion publique qualifiée.
