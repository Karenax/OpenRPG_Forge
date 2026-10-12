# Tidewater — bords de vue et scintillement des toitures, 2026-10-10

Le correctif local 015 est installé dans `E:\OpenRPG_Forge\Tidewater-Launcher-Fullscreen-20261008\runtime\tidewater.exe`. La comparaison native reproduit les triangles noirs sur les côtés au sommet avec la version 014 et les supprime avec la version corrigée. Le filtre d'ombre des bâtiments reste désormais stable entre images lorsque TAA est désactivé. La confirmation visuelle du propriétaire en jeu reste à faire.

## Causes et modifications

- `examples/tidewater_plant_probe/terrain_lod.rs` construisait les plans de visibilité avec un rapport constant 1920/1080. La sélection CDLOD utilise maintenant `PerspectiveProjection.aspect_ratio`, comme la caméra qui rend effectivement l'image.
- `examples/tidewater_plant_probe/walk.rs` recalcule la sélection après `CameraUpdateSystems` et prend en compte les changements de champ de vision, rapport d'écran et plans proche/lointain dans son cache. La projection initiale dans `examples/tidewater_plant_probe.rs` conserve le rapport du premier rendu de référence, avant publication du format réel de la fenêtre.
- `src/tidewater_object_shadow/{graph,receiver}.rs` conserve un indice d'échantillonnage PCSS constant sans TAA. Les shaders des bâtiments faisaient tourner le disque d'échantillonnage à chaque image, alors que l'accumulation temporelle attendue était désactivée. L'indice des récepteurs est séparé du temps des objets animés et de la cadence des cascades. La rotation des échantillons reste active avec TAA. Le test de décodage dans `receiver_observation.rs` suit la nouvelle signature.

Les six fichiers ont été intégrés par patch vérifié, avec sauvegardes et empreintes avant/après. Les modifications préexistantes du fichier principal et l'annotation Clippy du graphe d'ombre sont préservées. Aucun réglage utilisateur, contenu de carte ou shader du paquet n'a été modifié.

## Vérifications locales

| Vérification | Résultat |
| --- | --- |
| Régression des plans de visibilité, grand écran et portrait tourné | 2 échecs sur l'ancien corps de fonction, 2 réussites sur le nouveau |
| Récepteur d'ombre réel : disposition mémoire, adaptation, décodage et stabilité temporelle | 4 tests réussis |
| Compilation optimisée du moteur et du probe avec les dépendances natives vérifiées | réussie, sources et SDK inchangés pendant les compilations |
| `rustfmt --check`, sans formatage récursif des modules | réussi sur les six fichiers intégrés |
| Clippy sur la copie vérifiée incluant les six fichiers intégrés | moteur et exemple : sortie 0, aucun avertissement dans les fichiers modifiés ; 120 et 16 avertissements existants ailleurs |
| `git diff --check` sur les six fichiers | réussi |
| Probe de production 014 puis 015 au village | sorties natives 0, preuves automatiques réussies, 82 captures chacune |
| Vues natives déterministes avant/après au sommet et au village, 3440×1440 | quatre sorties natives 0, quatre preuves réussies, 82 captures chacune |
| Surveillance des six exécutions retenues | terminaison normale, aucun arrêt forcé, aucun descendant possédé encore actif |

Les vues déterministes emploient une surcharge de diagnostic identique avant/après : fenêtre 3440×1440, rotation contrôlée puis caméra immobile aux images 246–300. La caméra du sommet est placée à `[-35,315,-500]`, au-dessus du point culminant du raster, d'environ 306 m. Celle du village est à `[53.6,4.43445,-77]`. Ces modifications du témoin ne sont pas dans l'exécutable installé. Le rendu utilise les assets et la chaîne graphique natifs, avec vérification de la résidence effective des meshes, matériaux et pipelines.

Sur les bandes extérieures de l'océan, sous l'horizon et au-dessus de l'île, l'image 248 contient 11 567 pixels presque noirs avant correction et aucun après. Les captures ont été inspectées. Une bande sombre à l'horizon lointain reste visible dans les deux versions ; elle n'est pas corrigée par ce changement.

Sur la zone intérieure de toiture de l'étal, rectangle `[1730,535,2170,570]`, la variation RGB absolue moyenne entre les images stationnaires 248/256/264 passe de 0,872 à 0,182 sur l'échelle 0–255. Le percentile 95 passe de 6 à 1. Les autres zones mesurées ne présentent pas toutes le même gain. Cette mesure locale ne prouve pas l'absence de scintillement sur toutes les toitures pendant les déplacements. Le grain reste actif à 0,012 ; les témoins ont TAA=0 et FXAA=0. Les nuages, l'eau et la végétation continuent d'évoluer.

## Livraison et portée

- Exécutable 014 sauvegardé : SHA-256 `d2697ea98520c81d305b4995cc7ec663fc02a76bc2d24959758e056623cb3b3d`.
- Exécutable 015 installé et vérifié : SHA-256 `caf1387976a5eae2822c7eb763b946ff7c2a746ab2aa6806c9b91a3cd5fadef5`.
- Bibliothèque moteur corrigée : SHA-256 `240c72ff8cb46bff16966a81d7180633e428ec058ce9032274ca2164c8b76e78`.
- Lanceur et fichiers `graphics.json` / `display.json` inchangés pendant l'installation. Aucun processus utilisateur n'a été terminé.

La compilation réutilise le SDK fermé vérifié des réparations précédentes. Elle ne constitue pas une qualification Cargo de l'ensemble du workspace courant. Les changements propres au checkout préexistant dans les deux fichiers concernés sont conservés et contrôlés par Clippy ; l'exécutable livré vient du snapshot de réparation décrit par ses reçus. Pas de commit, push ou CI distante. Gate 3 reste ouverte et non qualifiée.

Les tentatives antérieures sont conservées : échec initial de l'oracle de démarrage avec un rapport de projection implicite de 1, corrigé avant livraison ; échec du contrôle de stationnarité sur la pente du sommet, malgré un rendu prêt et une sortie native 0, remplacé par le témoin de caméra fixe ; erreurs d'identité du chemin du driver de surveillance avant usage d'un chemin Windows absolu. Aucun de ces essais n'est compté comme réussite.

## Preuves

Racine locale : `E:\OpenRPG_Forge\.tidewater-integration\visual-bugs-20261010-015`.

- `source-integration.json`, `integrate-source.patch`, `primary-backup/` : intégration ciblée et préservation.
- `build-001/`, `build-002/`, `rust-check-final/`, `rustfmt-final.log` : compilation, empreintes et diagnostics.
- `frustum-test-results.json`, `frustum-tests-{before,after}/`, `receiver-tests-002/` : tests du code réel.
- `terrain-proof-{before,after}-village/` : probe de production.
- `terrain-proof-view-{peak,village}-{before,after}/` et moniteurs correspondants : captures natives contrôlées.
- `capture-analysis.json`, `analyze-captures.py`, `visual-comparison.png` : régions, méthode et comparaison inspectée.
- `deployment.json`, `runtime-backup/`, et `REPAIR-20261010-015.json` dans le paquet du lanceur : livraison locale et retour arrière.
