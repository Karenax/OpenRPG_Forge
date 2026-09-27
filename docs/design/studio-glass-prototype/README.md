# Studio Glass — prototype de présentation

Prototype indépendant HTML/CSS/JS, sans dépendance ni build. Ouvrir `index.html`
dans un navigateur, ou servir ce seul dossier sur localhost. Aucun backend,
stockage, accès aux modules Forge, import réseau ou commande métier. Le JavaScript
est réservé à cette maquette jetable, pas à la logique de production Studio.

Direction demandée : lumineuse, dock coloré animé, panneaux flottants translucides,
effets courts, carte dominante. Référence observée :
https://github.com/orime/macos27-simulator/tree/main (24 septembre 2026).
Pas de code, police, icône, fond d'écran ou asset copié de ce dépôt ; aucune licence
explicite identifiée et README indiquant une rétro-ingénierie. CSS, SVG, icônes et
carte schématique créés pour cette proposition.

Interactions de démonstration : changement d'outil, bibliothèque repliable, scène
repliable, placement d'illustrations avec aperçu/appliquer/annuler, tracés par points,
annulation de la dernière application, zoom, grille, accent et réduction des effets.
L'enregistrement ne fait qu'indiquer l'absence de sauvegarde. Le relief est une
emprise illustrative, pas une sculpture. Aucun playtest ni moteur 3D.

Ce prototype ne qualifie ni la beauté (avis utilisateur requis), ni la performance
WebGPU, ni Gate 2. Le port éventuel conserve Rust/Leptos/Tauri et les commandes Forge.

## Vérifications locales — 24 septembre 2026

- `node --check app.js` : réussi ; aucun build Cargo.
- Navigateur intégré : capture inspectée à 1280 × 720, dock au survol,
  sélection Maison, aperçu sur carte puis annulation, changement d'outil,
  tracé deux points, largeur 8 → 16, validation puis annulation historique.
- Repli bibliothèque/scène, zoom 100 → 110 %, ouverture/fermeture des préférences,
  désactivation des animations et du flou vérifiés dans l'interface.
- À 1920 × 1080 : bornes DOM des quatre panneaux dans la surface ; la capture
  avec override a présenté un cadrage incohérent, pas une preuve visuelle Full HD.
- À 800 px : largeur de document mesurée à 800 px, pas de débordement horizontal.
  Override retiré ; capture finale inspectée au viewport normal.
- Un bug de sélecteur dans le clic de tracé a été corrigé puis le parcours rejoué.
- Non qualifiés : toutes tailles/DPI, clavier exhaustif, contraste mesuré,
  performances des effets avec WebGPU et intégration au produit.

La session remise utilise http://127.0.0.1:8766/ (serveur Python lié uniquement à
127.0.0.1, PID 35880 au lancement). Onglet conservé pour essai utilisateur.
Sans serveur, `index.html` reste ouvrable directement. Aucun stockage persistant :
recharger la page réinitialise les essais.
