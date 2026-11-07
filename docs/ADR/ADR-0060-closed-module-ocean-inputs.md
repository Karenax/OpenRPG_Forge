# ADR-0060 — Entrées de simulation d'océan explicites des modules, API 9

Date : 28 octobre 2026. Statut : transport et réexport qualifiés localement ; rendu en cours.

## Décision

L'API 9 ajoute `ocean_inputs`, une liaison facultative par zone vers les entrées
FFT explicitement choisies. Elle ne déduit aucun paquet du nom de la carte ni de
son terrain. Le paquet reste le format admis par le lecteur commun
`forge_assets::source_ocean_package` : l'index et chacun de ses fichiers sont
épinglés, bornés et fermés ; la licence de la source accompagne le module. Les
programmes ne peuvent pas être déclarés comme licences. Les 524 ressources
maximales correspondent aux neuf noyaux, aux trois fichiers communs et aux deux
fichiers par étape pour 256 étapes. La limite totale de ressources uniques est
256 Mio par module, dans les bornes de l'inventaire existant.

Le résolveur de livraison ne lit que l'inventaire du module admis, vérifie la
fermeture de l'index avant le transport des charges et réutilise le lecteur FFT.
L'absence de liaison ne déclenche aucun accès à une bibliothèque globale. Le
réexport depuis une origine conserve la liaison, ses pins, ses fichiers et sa
licence, y compris pour une zone secondaire. Les API 1–8 conservent leur forme
vide d'origine et refusent le nouveau champ, même vide. Les autres compagnons
restent valides sous l'API 9 ; les consommateurs antérieurs refusent cette API.

Le jeu de modules et le producteur d'espace de travail Studio refusent encore
un module qui demande ces entrées tant que leur rendu d'eau complet n'est pas
raccordé. Le producteur refuse avant de créer son espace temporaire ; le lanceur
Studio fournit une erreur avant de lancer le jeu. Ce refus explicite est une
étape transitoire, pas l'activation de l'océan dans les modules.

## Qualification et limites

Sur les 2 548 entrées 121 inchangées, 42 tests du contrat, 26 tests de livraison
(un essai privé ignoré dans cette suite), 26 tests de construction (cinq essais
privés ignorés), six tests du runtime installé et 17 tests du producteur Studio
passent. L'essai privé `shared-module-ocean-acquired-tests-003.log` est ensuite
exécuté explicitement : il emballe et relit le paquet source acquis à 124 étapes,
avec ses 260 ressources et le pin de manifeste
`b54f26065b796c6e4731a415048739263c3416ab8bdd3a364927c26961f1656e`.
Le paquet de qualification est créé dans une fixture temporaire isolée et ne
constitue ni une livraison produit ni une preuve de rendu.

Clippy strict livraison, natif et Web, le format et 29 frontières passent. Six
tests du lecteur mutualisé et deux tests de paquets d'effets passent sur le même
état ; cela ne qualifie pas une migration de stockage parallèle encore en cours.
Les reçus sont dans la campagne centrale `gate7-monde-distribution` ; le
contrôle `shared-module-ocean-after-checks-002.json` constate les entrées stables.
La seule différence des sources 122 est l'ajout du test multizone : les deux
tests de réexport d'océan passent, dont une liaison uniquement sur une zone
secondaire, tandis que la zone principale demeure explicitement sans océan.
Clippy livraison et 29 frontières passent ; les entrées restent inchangées
(`shared-module-ocean-after-checks-003.json`).
Il reste à transporter la surface et le ressac, raccorder l'optique et le rendu
dans le jeu et Studio, puis qualifier images, performances et distribution.
