# ADR 0042 — Cycle de vie unique des outils Creator

## Contexte

Le plug-in Creator construisait environ soixante-dix `Window` spécialisées pendant son entrée dans
l'arbre d'éditeur. Plusieurs classes reposaient sur la visibilité par défaut du moteur : leur ajout
créait donc une rafale de fenêtres natives ou embarquées avant toute action de l'utilisateur. Les
politiques de fermeture, de redimensionnement et d'exclusivité restaient aussi dupliquées entre les
outils.

## Décision

Le Creator conserve son espace principal comme unique surface visible au démarrage. Un
`CreatorToolWindowManager` prépare toutes les fenêtres secondaires avant que leur parent entre dans
l'arbre, impose `Visible = false`, puis arbitre leur visibilité : zéro ou un outil secondaire peut
être affiché à un instant donné.

Ouvrir un outil masque l'outil précédent et réutilise son instance. Le bouton de fermeture masque
l'outil sans détruire son état de travail. Toutes les fenêtres gérées restent bordées,
redimensionnables, non exclusives et sans maintien forcé au premier plan. L'icône de marque n'est
appliquée qu'après la création de la fenêtre native.

Le smoke test du shell vérifie le démarrage sans outil visible et la propriété « un seul outil
actif ». Aucun format de projet, document métier ou flux de commande n'est modifié.

## Conséquences

- ouvrir le Creator ne crée plus de mur de fenêtres ;
- les neuf espaces restent la navigation primaire et les outils spécialisés deviennent des surfaces
  temporaires réutilisables ;
- une ouverture imbriquée (par exemple Zone vers Navigation) obéit au même arbitre ;
- les outils existants peuvent migrer progressivement vers des panneaux ou onglets internes sans
  changer leur logique métier, le gestionnaire constituant la frontière de compatibilité ;
- une fenêtre réellement simultanée devra être justifiée comme inspecteur secondaire et obtenir une
  politique explicite au lieu de contourner le gestionnaire.
