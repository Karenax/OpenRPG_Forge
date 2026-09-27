# R1-052 — essai Forge vivant refusé, dérive au repos

11 septembre 2026. **Acquisition de performance non qualifiée**, pas comparaison
A/B acquise. Aucun changement de carte, asset ou shader ; branche Rust sans push.

## Correction du mode d'acquisition

Premier essai, ancien runtime préchauffage SHA-256
`643bb8af9531d3616a42a515e0442735250921d59a768645409c7226958c0157`,
refusé avant rendu (code 2) : le garde du profil optique acceptait uniquement
la mesure en déplacement, pas la mesure immobile après parcours avec simulation
vivante. `water_medium_input::validate` admet maintenant cette seconde mesure.
Temps figé, capture/metrics simultanés, isolation et adaptation restent refusés.
Le seuil de déplacement du harnais est inchangé.

Cinq tests ciblés PASS, fmt et Clippy runtime all-targets/all-features PASS,
build natif PASS. Tests étendus pour les deux modes vivants et les exclusions.
Ce n'est pas une nouvelle qualification globale des 700 tests précédents.
Après fermeture graphique, suite runtime complète rejouée : 100 tests unitaires
et un test d'intégration PASS ; fmt/diff-check PASS.

Nouveau runtime : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forge-cost-20260911/runtime.exe`,
SHA-256 `402f02c586562403a104c410e83abce5c403f1ba48e02fca87acc991bec403d2`.
278 fichiers épinglés par `source-inputs.json`, aucun écart après compilation.
HEAD de base f786a38aca2023f08d708687f99a47d14f34e6d4, arbre modifié, pas révision clean.

## Essai vivant

`Run.ps1 -Run measure-a` sous cette racine externe : binaire et six entrées
épinglés, eau optique/reflets/nuages/forêt intégrés, High, no-vsync demandé,
parcours normal `1800:1:0.22`, freinage puis 120 frames de stabilisation.
Aucune compilation ou autre renderer de test simultané. Pas de temps virtuel figé.

Après parcours : position (-132,49434 ; 2,598663 ; -203,5185), grounded true,
vitesse horizontale commandée nulle. Puis refus `walk_measurement_moved` après
167 échantillons sur les 300 exigés :

- origine (-132,48811 ; 2,594663 ; -203,53094) ;
- position (-132,47945 ; 2,5891385 ; -203,54811) ;
- vitesse horizontale commandée toujours nulle ; déplacement total > 0,02 m.

Sortie 1 correctement conservée. Pas de FPS calculé à partir de cette fenêtre
invalide, pas de seuil augmenté ou de gel de la physique pour la faire passer.
Acquittement GPU `plan_application_ready` observé avant la mesure, sans en
déduire une qualification de cadence. Le garde révèle une dérive au repos sur
ce terrain ; une vitesse commandée nulle ne prouve pas une translation Rapier nulle.
La gravité est toujours envoyée au contrôleur chaque tick ; son rôle précis dans
la dérive nécessite un test de contact/pente, ce n'est pas encore une cause prouvée.

Essais conservés : `measure-1` (refus arguments) et `measure-a` (refus mouvement).
PID 13124 et 4420 terminés, conhost 32396 absent. Aucun échec effacé.

## Capture séparée

Acquisition séparée terminée avec le même binaire et les mêmes entrées. Parcours
terminé à la même position que la mesure ; PNG Full HD inspecté, sous-bois réel
avec personnage, troncs proches, feuillage et ombres. Capture frame 2000,
sortie 0 à 2010, zéro ERROR ; PID 30192 et conhost 8644 absents après fermeture.
`capture/capture.png` SHA-256
`45694866536b0b9d887098e6166ca6da232ea26076fcb40d2a831afd6967fef3` ;
`capture/stderr.log` SHA-256
`ab78c2d9ae9cf8f9f1b32e341900ed9c82dfd6f97b1b2a1d3c52be21bed7eae7`.
Overlay glissant ~17,6 FPS et p95 ~58,2 ms : observation de lenteur, pas benchmark
remplaçant la mesure refusée. Ne pas confondre le défaut de stabilité de pose
avec une explication de cette lenteur. Ce lot ne modifie pas le profil de
caméra : troisième personne FOV vertical 60°, contre FOV rapporté 67,7° Cadle.
Même après correction de la dérive, ne pas présenter ces optiques différentes
comme une comparaison finale appariée.

R1-052 et Gate 1 restent ouverts. Priorité suivante : reproduire et corriger
la dérive dans le contrôleur générique, sans retouche ponctuelle de cette carte.
