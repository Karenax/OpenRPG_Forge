# Forge Hub

Launcher et installateur graphique Tauri 2, partageant les commandes de
[`forge_delivery`](../../tools/forge_delivery/README.md).

`ForgeSetup.exe` est ce binaire avec un index `delivery.json` et les packages voisins.
Il présente le plan avant installation ; le launcher installé permet jeu, éditeur,
vérification et rollback. La fenêtre reste réactive : opérations longues hors du thread UI,
commandes sérialisées et permissions Tauri minimales. Aucun métier dans JavaScript.

`forge_hub --root ABSOLUTE_INSTALL_ROOT` ouvre une installation spécifique.
Sans argument, utiliser la racine standard par utilisateur. Windows x64 et WebView2
sont nécessaires ; ni Python, Node ni Cargo ne sont requis pour les utilisateurs.
La sélection de fichiers utilise un dialogue Windows exécuté par une commande fixe.

Les icônes sont les dérivés Forge existants, copiés depuis le Studio et documentés
dans `icons/PROVENANCE.md`. Le shell et les graphismes du Studio ne sont pas remplacés.
