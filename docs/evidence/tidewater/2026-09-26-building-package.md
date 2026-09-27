# Dépendances locales des 23 bâtiments

`package-building-assets.py` rassemble les descripteurs épinglés et leurs
géométries, la bibliothèque explicite des sept matériaux, les quatorze textures
avec mipmaps, le shader Bevy adapté et la notice MIT originale.
Les références `material-N` sont résolues explicitement vers leurs paramètres.
Les fichiers sont copiés, sans dépendance de chemin vers le clone ou les fixtures.

Sortie retenue : `E:/OpenRPG_Forge/.tidewater-integration/building-package-002`.
Elle contient 821 fichiers de payload (55782087 octets), plus `package.json`.
Le premier essai `001` est conservé mais remplacé par `002`, qui explicite les
identités de matériaux au lieu d'une correspondance implicite par indice.

Après écriture, tous les payloads sont relus et vérifiés par taille et SHA-256.
Une vérification séparée des descripteurs depuis la seule racine du paquet
confirme 23 assets, 888 références de géométrie, sept liaisons de matériaux et
quatorze liaisons de textures, toutes présentes.
Rapport : `building-package-proof-001.json`.
Hash de `package.json` :
`0d494ec9fc15d77c876661f4e5bcabf807e58425cb3d12e41197bce92445cb39`.

Ce paquet ne fige pas les textures calculées d'atmosphère, d'environnement et
d'ombres. Ces ressources doivent être fournies par le moteur, avec les uniforms
de frame et d'ombres. Il ne représente pas la carte complète, un GLB standard,
un import Studio qualifié ni une autorisation de publication. Le branchement
du chargeur produit sur ce paquet reste ouvert.
