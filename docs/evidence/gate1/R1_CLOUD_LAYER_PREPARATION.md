# R1 — Préparation de la couche nuageuse

7 septembre 2026, Rust local. Gate 1 ouverte.

## État constaté

Le ciel courant utilise Atmosphere/ScatteringMedium de Bevy et l'IBL associée,
pas un shader de ciel Forge. Le profil d'environnement v1 ne contient aucun nuage.
Le retour propriétaire « pas très réaliste » n'est pas une panne de chargement
d'un système nuageux existant : celui-ci reste à construire.

## Implémenté et vérifié

`forge_project::CloudLayerSettings` prépare les paramètres moteur-neutres d'une
couche horizontale : seed, altitude, échelle spatiale, couverture, opacité et vent X/Z.
Valeurs finies et bornées, champs JSON inconnus refusés. Le déplacement de texture
est calculé à partir du temps fourni, en mètres/seconde et période spatiale ; aucun
temps système ou hasard global. Enroulement périodique avant conversion en f32.

63 tests forge_project PASS, dont trois nouveaux : round-trip, valeurs invalides,
déplacement périodique déterministe, vent nul/négatif et temps extrême. Premier
Clippy refusé pour comparaisons flottantes dans les tests ; assertions converties
en comparaisons de bits, sans affaiblir les contrôles.
Clippy strict forge_project/all-targets PASS sur la version finale. Pas de nouveau
total workspace déduit ; l'exécution de 584 tests précède ce module préparatoire.

## Pas encore implémenté

Ces paramètres ne sont pas encore un document persistant ni une extension acceptée
par EnvironmentProfileDocument v1. Aucun profil existant ne change, aucun nuage
n'est activé implicitement ou rendu. Aucun asset, shader ou dépendance ajouté.

Suite nécessaire : génération déterministe du champ de couverture, projection
éclairée cohérente avec le soleil, horizon sans couture, intégration explicite au
profil/qualité et diagnostics. Une couche horizontale n'est pas une simulation de
nuages volumétriques ; ne pas la présenter comme telle. Exiger ensuite capture
native/Web, animation et contrôle du coût avant toute acceptation artistique.

## Champ de densité implémenté ensuite

`forge_world::generate_cloud_density` génère un champ linéaire R8 périodique depuis
seed/couverture. Quatre fréquences de bruit de valeur lissé, coordonnées de grille
enroulées : aucune image externe et aucun appel réseau. Résolutions puissance de
deux 32–512, au plus 262 144 octets de résultat. Génération destinée au changement
de paramètres, pas à chaque frame. Opacité, vent, altitude et échelle spatiale
ne changent pas les échantillons et ne nécessitent pas de régénération.

La couverture est un seuil monotone, pas une promesse de pourcentage exact de ciel
occupé. Zéro donne un champ vide, un donne un champ plein. La densité n'est ni une
couleur sRGB ni l'opacité finale. Le rendu devra utiliser un sampler repeat et des
mips/filtrage appropriés ; la continuité mathématique seule ne prouve pas l'absence
de couture visible après projection.

Trois tests ciblés PASS : déterminisme/seed/monotonie/budget, continuité et égalité
des bords périodiques, indépendance des paramètres de présentation. Clippy strict
forge_world/all-targets PASS. Pas de total workspace supplémentaire déduit.
Toujours aucun nuage rendu : projection, éclairage, contrat persistant, contrôles
de qualité et diagnostics restent à raccorder. Aucun profil existant modifié.
