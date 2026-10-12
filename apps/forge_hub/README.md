# Forge Hub

Gestionnaire de contenu et installateur graphique Tauri 2, partageant les commandes de
[`forge_delivery`](../../tools/forge_delivery/README.md).

La livraison classique utilise désormais un seul EXE NSIS hors ligne, produit par
`scripts/delivery/build_classic_installer.py`. Il embarque ce hub, le gestionnaire
de packages, le jeu, le Studio, les contenus et l’installateur Microsoft WebView2.
Le hub installé est `Forge-ContentManager.exe`. `Forge.exe` ouvre le launcher
canonique L’Aube des royaumes (`apps/forge_launcher`) ; le Hub permet jeu, éditeur,
vérification et rollback. La fenêtre reste réactive : opérations longues hors du thread UI,
commandes sérialisées et permissions Tauri minimales. Aucun métier dans JavaScript.

Le mode de développement avec `delivery.json` et packages voisins reste disponible :
il présente une relecture avant application. Le bouton de livraison jointe n’est pas
affiché dans le hub déjà installé; les contenus supplémentaires s’ajoutent avec le
sélecteur de packages.

`forge_hub --root ABSOLUTE_INSTALL_ROOT` ouvre une installation spécifique.
Sans argument, le hub reprend d'abord le dossier qui contient l'exécutable si un
`installation.json` voisin y est présent ; sinon il utilise la racine standard par
utilisateur. L'étape Destination permet de saisir un chemin ou d'ouvrir le dialogue
Windows de sélection de dossier. Le chemin doit être absolu et sans lien ou reparse
point ; changer de destination efface toute relecture en attente jusqu'à validation.
Windows x64 et WebView2 sont nécessaires ; ni Python, Node ni Cargo ne sont requis
pour les utilisateurs. La sélection de fichiers utilise un dialogue Windows exécuté
par une commande fixe.

Les icônes sont les dérivés Forge existants, copiés depuis le Studio et documentés
dans `icons/PROVENANCE.md`. Le shell et les graphismes du Studio ne sont pas remplacés.
