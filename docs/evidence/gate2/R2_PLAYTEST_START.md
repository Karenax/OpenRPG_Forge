# Départ temporaire du playtest — 13 septembre 2026

## Implémenté

- Commande partagée `StartPlaytestAt` : hash de tête attendu + X/Z en mètres.
  L'ancien `StartPlaytest` conserve le départ par défaut.
- Host : capture du snapshot validé, coordonnées finies et marge de 1 m à
  l'intérieur du terrain. Rejet explicite, pas de clamp ni de sauvegarde.
- Le choix appartient uniquement au snapshot de simulation et à la réponse
  `AuthoringState.playtest_start`. Il survit au remontage de WebView via Inspect,
  disparaît au retour édition et n'entre pas dans l'archive de zone.
- Le Studio dispose de champs X/Z et de « Jouer à cet endroit ». Jouer/Retour
  sont déplacés dans la barre sticky existante, sans nouveau bus de commandes.
- Le renderer calcule Y sur le terrain effectif, ajoute la demi-hauteur de la
  capsule et 2 cm, puis utilise le point de départ du contrôleur existant.
- La barrière de playtest compare aussi le départ de la projection lancée à
  celui du snapshot actif, en plus de la source et des collisions des placements.

## Vérifié

- Clippy Studio WASM strict PASS (29,29 s), avec avertissement amont futur
  `proc-macro-error2` conservé.
- `manual_placements` : 6 PASS, dont départ temporaire/identité/source inchangées,
  limites de terrain, NaN/infini refusés et borne intérieure admise.
- `zone_host` : 1 scénario intégré PASS. Commande et réponse sérialisées,
  démarrage choisi relu par Inspect, tête/fichier inchangés, Stop, départ hors
  zone et hash périmé refusés sans activation, sauvegarde/réouverture existantes.
- `density_inspection` : 2 PASS. Total du lot : 9 tests, compilation 29,25 s.
- Formatage et diff contrôlés. Aucun processus graphique lancé par ce lot.

## Non vérifié / limites

Mise à jour après replay `AF3EF695…` : bouton « Jouer à cet endroit » exécuté
avec X/Z=0 dans une zone comportant un chemin et deux arches. Capsule visible
sur le chemin, simulation active tick 16873, position (-0.00,3.91,0.00), au sol
true ; retour édition et sauvegarde inchangée vérifiés. Le choix d'un départ
non nul et le parcours de collision près des arches restent non qualifiés.
Voir `R2_SPLINE_EDIT.md` pour les hashes et la fermeture des processus.

Le candidat graphique `77049619…` précède ce changement : il ne prouve pas ces
champs, la barre réorganisée ou l'apparition au point choisi. Construire un
prochain candidat regroupé puis effectuer le parcours près des arches existantes,
sans déplacer celles-ci : départ proposé X=-84, Z=90, puis contact/passage et retour.

Le choix est numérique, pas encore un clic sur le terrain. Il ne recherche pas
automatiquement un volume libre parmi les objets : l'interface demande un espace
libre ; le cas d'un départ dans un solide et les pentes restent à qualifier.
Ce n'est ni un point de spawn gameplay persistant, ni une téléportation de partie.

Le contrôleur utilise les codes physiques W/A/S/D. L'impulsion `w` de l'essai
précédent n'a pas établi le code physique reçu ni une durée maintenue suffisante ;
le prochain parcours doit observer ces entrées, sans conclure à un défaut moteur
sur cette seule absence de déplacement. Aucun correctif clavier spéculatif ajouté.

R2-041 et Gate 2 restent ouverts ; CI distante NOT RUN — infrastructure billing.
