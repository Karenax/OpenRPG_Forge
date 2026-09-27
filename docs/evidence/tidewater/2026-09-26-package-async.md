# Acquisition asynchrone du paquet

`BuildingPackage::load_async` permet au navigateur de fournir des lectures
asynchrones sans dépendance de transport dans `forge_assets`. Le lecteur natif
synchrone et cette voie partagent la validation du manifeste, des payloads et
de leurs dépendances. Le pin vient toujours de l'appelant ; l'index complet est
validé avant la première lecture. Les fichiers sont demandés séquentiellement
et vérifiés individuellement avant la requête suivante. L'annulation de la
requête en cours appartient à l'adaptateur de transport.

`verify_tidewater_package` réussit sur `building-package-002`, manifeste
`0d494ec9fc15d77c876661f4e5bcabf807e58425cb3d12e41197bce92445cb39`.
Son exécuteur borné suspend une fois chaque lecture avant de fournir les bytes :
23 bâtiments résolus et shader identique. Un pin incorrect ne provoque aucune
lecture ; un premier payload corrompu provoque une seule lecture et arrête
l'acquisition. Les contrôles synchrones de corruption et de dépendance absente
passent également. Check `wasm32-unknown-unknown` et Clippy strict ciblé
`--example verify_tidewater_package --no-deps -D warnings` réussissent.

Ces vérifications couvrent le lecteur et sa suspension, pas les requêtes HTTP
réelles ni le chargement dans Studio. Le transport navigateur existant fournit
déjà des lectures bornées avec abandon et délai ; il reste à brancher le paquet
sélectionné puis ses assets au contrôleur du viewport.
