# Tidewater — correction de la disparition des troncs au sol, 2026-10-09

Le runtime 014 corrige la lecture d'un ancien buffer GPU par le matériau des débris scannés lorsque la population visible change. Le défaut a été reproduit localement : un tronc disparaît selon l'angle, tandis que son ombre reste présente. Les captures avant/après montrent son retour et celui des petits morceaux de bois.

## Cause et correction

`ResidentDraws::synchronize` reconstruisait un `ShaderBuffer` dont la longueur dépendait du nombre de dessins visibles. Bevy 0.19.1 remplace l'allocation GPU lorsque cette longueur change (`bevy_render/src/storage.rs`, `GpuShaderBuffer::prepare_asset`). Le matériau conserve son binding tant qu'il n'est pas repréparé (`bevy_pbr/src/material.rs`, `prepare_asset`). Depuis l'optimisation des mises à jour de matériau, tourner la caméra pouvait donc décaler les tags des meshes par rapport aux anciens enregistrements lus par le shader : mauvaise variation, fade ou sélection d'asset, jusqu'à suppression des triangles.

Le correctif réserve deux LOD par placement (4 512 octets pour les 141 instances initiales) et complète chaque upload jusqu'à cette capacité. Une image vide conserve aussi l'allocation. Seul l'ajout de placements au-delà de la réserve provoque une croissance et une repréparation explicite du matériau. Les modifications des draws, tags et assets sont publiées après le calcul du frustum et avant les événements d'assets et le contrôle de visibilité. Le culling et les LOD restent actifs ; les documents et collisions ne sont pas modifiés.

## Validation locale

- Native `build-004` : moteur et exemple compilés en opt-level 3, assertions désactivées, SDK fermé inchangé ; SHA-256 `d2697ea98520c81d305b4995cc7ec663fc02a76bc2d24959758e056623cb3b3d`.
- Trois tests CPU utilisant le module de production : capacité stable avec populations 4/1/0/3, croissance puis maintien, rejet sans upload partiel. Les deux tests de capacité échouent sur le code précédent ; les trois passent sur le code final.
- Parcours natif : douze poses répétées, trois prototypes de bois, 24 captures. Un angle est normalement masqué par le mur d'un bâtiment. Revue des vues dégagées et de la comparaison `visual-review-after.png` : PASS local.
- Transitions buffer changé/binding inchangé : 20 avant, 0 après. Allocations observées après chauffe : 21 avant, 1 après.
- Deux essais FPS de 32 secondes, même résolution 2560×1440, Fifo, réglages utilisateur copiés, aucune capture ni compilation propre pendant la mesure. Deux premières secondes exclues par phase. En mouvement : **91.13 → 88.34 FPS** ; p95 **12.74 → 13.51 ms** ; 1 % low **71.82 → 61.27 FPS**. Il s'agit d'une paire de mesures locales, pas d'une garantie globale de FPS.
- Démarrage ordinaire avec la commande du launcher, sans mode de preuve : 40 secondes ; runtime installé : 20 secondes ; initialisations océan/spray terminées, fermeture normale, aucun arrêt forcé. Un premier essai de 40 secondes est conservé comme verdict incomplet : le jeu survit et ferme normalement, mais son log hérité `RUST_LOG=warn` masque les marqueurs INFO attendus par le driver ; l'essai est rejoué en INFO, sans modifier le runtime ni les flags de jeu.
- Contrôles Rust/Clippy locaux : aucune erreur et aucun nouvel avertissement par rapport à 013 ; un avertissement de l'adaptateur a été supprimé. Les avertissements existants du reste du snapshot restent présents. Formatage des fichiers créés/modifiés vérifié, style préexistant de `escape_menu.rs` conservé ; diff de publication ciblé vérifié.
- Tests séquentiels, profils réels conservés. Les gardes de ressources locales n'ont déclenché aucun arrêt ; aucun seuil de RAM disponible ou de headroom système n'a été réintroduit.

## Installation et portée

Le binaire testé est installé dans `Tidewater-Launcher-Fullscreen-20261008/runtime/tidewater.exe`. Le launcher reste inchangé. Le runtime 013 et les fichiers remplacés sont sauvegardés dans `backups/`. Cinq fichiers de source sont intégrés, dont les tests et la preuve opt-in ; les autres travaux locaux sont préservés. Aucun commit ni push.

Les sources intégrées sont normalisées en LF pour respecter le diff Git. Leur texte normalisé est identique au snapshot compilé conservé dans le dossier de preuve ; les empreintes brutes peuvent différer uniquement par CRLF/LF. Le snapshot compilé et le binaire testé restent inchangés. Les empreintes avant/après figurent dans `deployment.json` et `line-ending-normalization.json`.

Les corrections précédentes de ciel, de performance et de bootstrap sont conservées. Les limites temporelles de démarrage restent supprimées et les retours GPU dupliqués restent idempotents. Cette validation locale du défaut de rendu ne clôture pas la Gate 3 ; la CI distante, les contrôles workspace/WASM et une nouvelle acceptation humaine de 014 ne sont pas revendiqués.

Preuves : `E:\OpenRPG_Forge\.tidewater-integration\launcher-repair-20261009-014` ; `deployment.json`, `summary.json`, `warning-comparison.json`, `performance-comparison.json`, `logs-proof-*/`, `buffer-tests-*/`, `normal-*/`, `source-review.diff`.
