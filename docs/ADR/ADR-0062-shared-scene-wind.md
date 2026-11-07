# ADR-0062 — Vent de scène commun et données de zone, API 11

Date : 2 novembre 2026. Statut : parcours local partiellement qualifié ; Gate 7 ouverte.

## Décision

La direction et la vitesse du vent sont des données facultatives de la zone,
indépendantes de la végétation et du nom du module. La direction décrit le sens
du déplacement : 0° vers le nord (−Z), 90° vers l'est (+X) ; la vitesse va de
0 à 25 m/s. Le domaine vérifie ces valeurs avant toute publication. Le moteur
possède le vent accepté et les consommateurs raccordés utilisent cette même
valeur. En son absence, chaque chemin historique conserve son entrée existante.

Le Studio prépare cette retouche par les commandes de zone existantes : aperçu,
application, annulation, rétablissement et sauvegarde. Les archives complètes,
compactes et différentielles conservent le vent et son historique, sans changer
les octets des anciennes archives sans vent. Un export qui contient du vent dans
la tête ou son historique exige l'API de module 11. Un ancien export sans vent
garde sa version ; un export multi-zones conserve ses autres données et pins.

La valeur commune est raccordée aux surfaces source, aux déformations de la
végétation procédurale et importée et au déplacement des nuages concernés. Le
Studio et le jeu de module la publient depuis la zone admise ; les anciens
composants Tidewater conservent leurs données particulières. Cela ne signifie pas
que tous les effets ou objets animés d'un module sont déjà raccordés.

## Qualification et limites

Les tests de domaine vérifient les bornes, directions, archives et opérations
réversibles ; les contrôles de contrat vérifient les anciennes archives et le
refus d'un export rétrogradé, y compris après annulation. Le rejeu privé d'un
paquet réel à deux zones conserve environnement, terrain, végétation et objets.
Le Studio natif a été observé sur une copie isolée : aperçu, application,
annulation, rétablissement, sauvegarde, fermeture et réouverture après
redémarrage. La valeur 7 m/s vers 90° est retrouvée dans l'interface et dans
l'objet sauvegardé (`scene-wind-studio-assessment-001.json`).

Sous Windows/Vulkan, un module exporté distinct a démarré avec cette valeur ;
les observations du moteur la retrouvent dans la végétation et les nuages.
La carte de ce paquet conserve toutefois des données et le nom Tidewater : ce
rejeu ne démontre pas le même résultat sur une carte entièrement différente.
Il ne qualifie ni l'animation visible, ni les effets encore non raccordés, ni
une parité de rendu ou de cadence. Linux, machine propre et Gate 7 restent à
qualifier ; les résultats détaillés figurent dans le suivi de la gate.
