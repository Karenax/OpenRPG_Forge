# Contrôleur CPU des nuages en Rust

`forge_engine::cloud_controller` porte le contrôleur de `SkyProClouds.js`
épinglé à `1438b1ab`, avec attribution MIT : caméra courante/précédente,
détection des coupures, couverture, cycles de trace 4×4 et de panorama,
horloge/vent/évolution, cône lumineux, centre d'ombre arrondi comme JavaScript,
validité d'historique et sortie sous l'eau. Les calculs CPU restent en f64
avant empaquetage f32, conformément à la source.

Les entrées invalides sont refusées avant mutation. Trois tests indépendants
du renderer passent : démarrage/cycle complet/zéro négatif, invalidations et
immersion/redimensionnement, refus atomique et bornage du temps. Exécutés par
`rustc --edition 2024 --test crates/forge_engine/src/cloud_controller.rs`.

## Comparaison indépendante

Le probe `tidewater_cloud_controller_probe` utilise uniquement les métadonnées
caméra/vent/soleil/couverture pour calculer les blocs. Les blocs source servent
de cibles de comparaison, jamais d'entrées du contrôleur.

Sous `E:/OpenRPG_Forge/.tidewater-integration/` :

- `cloud-controller-seaward-comparison-001.json` : 360/360 fichiers exacts
  sur 120 pas, vue côtière normale ;
- `cloud-controller-overcast-comparison-001.json` : 360/360, couverture 0,85 ;
- `cloud-controller-island-comparison-001.json` : 360/360, caméra île ;
- `cloud-controller-dynamic-source-001/002` : oracle CPU original sur 32 pas,
  avec 96 fichiers répétés exactement. Les dispatches sont interceptés ;
  l'appareil source sert à construire ses objets, sans exécuter ces kernels.

La séquence dynamique inclut mouvement, téléportation, rotation, FOV,
couverture, lumière, vent, immersion/sortie, redimensionnement, échelle,
dt long/négatif, vent nul et invalidations explicites. Le probe Cargo donne
84/96 fichiers strictement identiques ; les 12 autres diffèrent uniquement
par un résidu 1,5407439555097887e-33 au lieu de zéro dans la composante X de
l'axe haut de caméra, sur les six premiers pas. Paramètres d'historique,
horloges et settings concordent. Voir `cloud-controller-dynamic-comparison-001.json`.
Le même module compilé avec les valeurs d'entrée matérialisées en littéraux
f64 donne 96/96 (`cloud-controller-dynamic-fast-comparison-001.json`). Cette
distinction de transport numérique est conservée, sans déclarer le chemin
JSON dynamique strictement identique.

## Raccordement GPU et scène

Le graphe partagé de la scène produit maintenant ses Frame/Settings nuageux
avec ce contrôleur. Le mode headless `--native-controller ATMOSPHERE_INPUT`
fait de même. Les modes de replay précédents restent disponibles.

`cloud-controller-inputs-001` contient 255 fichiers, dont les six kernels,
bruit bleu et sept niveaux du volume. Aucun fichier local `cloud-step*-frame`,
`settings` ou `public` n'y figure. Les 120 blocs Frame globaux et paramètres
atmosphériques restent acquis. Le manifeste original est conservé, même si
ses entrées non consommées désignent des fichiers absents du pack.

`native-cloud-controller-headless-001` fonctionne avec ce pack réduit.
Ses dix textures de sortie sont exactement identiques au calcul utilisant
les paramètres enregistrés (`native-cloud-controller-headless-replay-001`).
`cloud-controller-gpu-regression-001.json` conserve les hashes et contrôle
l'absence physique des paramètres locaux enregistrés.

`native-cloud-controller-scene-001.png`, avec eau/embruns et ombres reçues,
est strictement identique à `native-cloud-overcast-shadow-001.png` :
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
La fixture scène conserve son bloc public d'échantillonnage, vérifié exact
contre celui calculé par Rust ; son remplacement physique reste séparé.

Clippy strict des trois exemples et compilation passent. Binaires SHA-256 :

- scène : `c219195e15945bfdeb8fc6e9c09c9c8878c69f2a20e989336315130d8ff7abaf` ;
- graphe : `14687af5c26d360d21752f8aebff73cb77f2344b8f54bdfc024536b6fd059f4a` ;
- contrôleur : `6d463411d95332f2b86e3e509a75b532a3fe263e661de5e076cfd22e8c561bd5`.

## Limites

Le graphe exécute encore un lot à pose fixe. Le contrôleur sait traiter les
transitions CPU testées ; cela ne prouve pas leur raccordement à une boucle
GPU interactive. Frame global, soleil/irradiance, environnement ambiant,
animation longue, performance, Studio et navigateur restent ouverts.
Pas de parité complète de la carte, de clôture de gate ni de publication Git.
