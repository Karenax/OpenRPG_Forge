# Repos sur pente — contrôleur générique

11 septembre 2026, branche Rust, pas de publication.

## Reproduction et correction

Le test `stationary_player_does_not_creep_on_walkable_slopes` utilise le vrai
contrôleur Bevy/Rapier sur quatre boîtes inclinées (0°, 15°, 30°, -15°), 120 ticks
de stabilisation puis 1800 ticks sans entrée. Aucun asset, coordonnée de carte,
renderer ou temps physique supprimé. Échec avant correction : 0,048266236 m
de dérive à 15° en 30 secondes, au-delà du seuil inchangé de 0,02 m.

Le contrôleur conserve sa translation verticale et la détection du sol, mais
désactive `slide` au repos stable. Marche, montée, saut, chute ou glissade raide
déjà observée restaurent le glissement. Pas de correction forcée du Transform,
pas de suppression de gravité, pas de modification de Rapier ou du profil auteur.

Le chemin Rapier 0.35.0-glamx0.2 `handle_slopes` ajoute une poussée de normale
après le calcul du déplacement autorisé ; continuer les itérations de glissement
au repos peut accumuler ce déplacement. La correction borne ce comportement
via l'API existante, sans remplacer le moteur physique.

## Validation

17 tests du contrôleur PASS après correction : quatre pentes au repos,
marche/freinage/sprint, saut/atterrissage, marches basses/hautes, pentes praticables
et raides, glissade au-delà du seuil, identité backend et caméra. Clippy strict
workspace PASS. 701 tests globaux PASS / un timing préexistant ignoré, build natif PASS.
Les journaux sont sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-idle-slope-20260911/`.
La préparation contient 278 hashes de sources, pas une révision Git propre.

## Rejeu réel acquis

Nouveau binaire SHA-256
`206b647eae5aa286b47156bef2ace00c2833f1b44918c6ebc79a80e3e4440484`.
`Run.ps1 -Run measure-a` reprend exactement les entrées et le parcours du refus
précédent, avec le nouveau runtime : **300/300 échantillons acquis**, grounded,
temps virtuel non figé, sans franchir le seuil de déplacement inchangé. Position
finale (-132,49760 ; 2,60032 ; -203,51364). Sortie 0, aucune erreur.

Mesure native High clair 1920×1080 RX 7800 XT/Vulkan, simulation vivante :
56,1424 ms moyens / **17,8119 FPS**, p95 **57,554 ms**, 1 % low **16,0782 FPS**,
durée 16,8427 s pour 300 frames. Les 300 frames dépassent 33,3 ms. Coût moyen
hors First–PostUpdate : 51,2749 ms, pas un temps GPU identifié. Aucun gain de
performance revendiqué ; ce résultat valide la stabilité de la fenêtre et
confirme la lenteur. La dérogation de performance antérieure ne transforme pas
ces chiffres en PASS mesuré. Les optiques/fenêtres Cadle ne sont pas appariées.

Capture séparée frame 2000, sortie 0 à 2010, Full HD inspectée : personnage et
intérieur forestier présents, aucun changement artistique attendu. SHA-256 PNG
`8342545d1a30f22512169b3b9b38d1e7baa88a4869bf084b1701de6d23754dcc`.
PID mesure 24316/conhost 10456, capture 35092/conhost 624 : tous fermés et absence
vérifiée. Ni mesure GPU simultanée ni compilation pendant les deux essais.
Rapport dérivé `measurement-verification.json`, logs bruts conservés.

Le test physique ne qualifie ni les performances, ni le rendu du Studio.
Clippy Web/WASM avec `--no-default-features --features web` PASS (24,34 s).
Premier appel erroné avec les features desktop par défaut : refus Tauri/WASM
conservé dans `clippy-web.log`, corrigé dans la commande seulement. Aucun manifeste
ou dépendance modifié. Warning futur proc-macro-error2 conservé. Pas de nouveau
bundle ou essai graphique Studio ; 278 hashes source revérifiés sans écart.
R1-052 et Gate 1 ne sont pas fermés par cette correction.
