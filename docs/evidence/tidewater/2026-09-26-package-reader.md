# Chargement Rust du paquet de bâtiments

`forge_assets::tidewater_package::BuildingPackage` charge le paquet à partir
d'un manifeste explicitement épinglé et d'un lecteur de bytes fourni par
l'adaptateur. Il ne dépend ni du disque, ni de Bevy, ni de Node.

Le manifeste est limité à 2 Mio, l'index à 4096 fichiers, chaque fichier à
32 Mio et le total déclaré à 128 Mio. Les noms et limites de tout l'index sont
vérifiés avant appel du lecteur. Tous les payloads sont vérifiés par longueur
et SHA-256, puis les références catalogue/descripteurs/matériaux/textures sont
résolues. Le chargeur GPU reste responsable des formats de sommets, dimensions
de textures et paramètres de rendu ; cette API valide la fermeture du paquet.

L'exemple `verify_tidewater_package` a été exécuté sur `building-package-002`,
avec le hash de manifeste `0d494ec9fc15d77c876661f4e5bcabf807e58425cb3d12e41197bce92445cb39`.
Il charge les 23 bâtiments depuis le paquet seul, puis vérifie le refus d'un
shader dont un octet est corrompu, d'une bibliothèque de matériaux absente et
d'un hash de manifeste incorrect. Dans ce dernier cas aucun lecteur n'est appelé.
Les erreurs sont injectées dans le lecteur ; le paquet source reste inchangé.

Le vérificateur compile et termine avec code 0. Clippy incluant l'exemple reste
arrêté par les deux diagnostics préexistants dans `forge_project/session.rs`.
Ce lot n'ajoute pas encore le chargement du paquet au viewport Studio et ne
constitue pas une preuve de rendu depuis le paquet.
