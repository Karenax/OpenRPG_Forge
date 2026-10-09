# Cailloux — sélection source des cellules suivant la caméra

`export-pebble-cell-reference.mjs` exécute le véritable constructeur et
`PebbleField.update` de la référence propre 1438b1ab. Il vérifie la source
avant et après, le masque acquis et le package géométrique
`0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a`.
Les flags et les deux tableaux de hauteurs reconstruits doivent correspondre
octet pour octet à l'acquisition existante avant toute capture de caméra.

Le premier essai 7202 échoue avant création de sortie : TerrainData brut ne
contient pas les plateformes du village. Le constructeur original Village
est ajouté dans le même ordre que l'export d'assemblage ; aucun payload ni
repère n'est remplacé. Les hauteurs correspondent alors exactement.

Deux acquisitions distinctes réussissent ensuite : première commande
`271a45`, puis session 13023, code 0. Leurs reference.json sont identiques,
SHA-256 `afdd67ff99ccb2db1b4b94007df140fba6e330ebc2b00c4dde7991ed19245ac6`.
Elles couvrent 150 poses : 25 positions réparties sur le terrain, trois
orientations et deux hauteurs. 72 poses voient des cellules, maximum observé
51. Le rejeu immédiat d'une caméra identique est effectivement sauté ; les
poses au-delà de 25 m au-dessus du terrain ne sélectionnent aucune cellule.

Le contrôle indépendant relit les slots capturés et les tableaux acquis :
indices entiers et bornés, cellules occupées sans doublon, rayon maximal
25 m, limites de hauteurs et intersection avec les six plans du frustum.
Il passe pour toutes les cellules sélectionnées. Reçu :
`.tidewater-integration/pebble-cell-reference-20261006-repeat.json`.

La source utilise des cellules de 4 m, un tampon de 240 cellules maximum et
un patch de 4 704 triangles. La couche suit la caméra, reçoit les ombres et
ne devient pas caster. Les 10 415 cellules occupées ne sont pas des objets
dessinés simultanément ni des placements manuels à sérialiser un par un.

Cette preuve qualifie seulement la référence CPU de sélection. Le décodeur,
le raccord au runtime, le vertex shader de densité/terrain/fade, le matériau,
le dessin natif/WebGPU et la comparaison visuelle restent à intégrer et
qualifier. Le message device was destroyed intervient dans le finally de
fermeture du device après acquisition ; aucune capture de scène n'est produite.
Le candidat Studio figé des 805 décors n'est pas modifié.

Le module Engine `tidewater_debris::pebble_cells` prépare ensuite les slots
iCell depuis la grille acquise et une caméra/frustum fournis par l'adapter.
Il conserve ordre des cellules, rayon 25 m, seuil de hauteur, marge verticale
des bounds, plafond 240, conversion float32 et cache source. L'invalidation
explicite permet au consommateur de remplacer le terrain sans conserver une
sélection périmée. Les entrées invalides sont refusées avant mutation du cache.

Le harness std inclut le vrai fichier de production, avec les flags et bornes
acquis et les poses source indépendantes. V1 passe le test natif et la
compilation des métadonnées WASM, mais Clippy relève collapsible_if. V2
répare cette condition avec un let-chain puis rejoue : un test PASS, 150 poses,
2 553 cellules comparées exactement, caméra identique sautée, invalidation
et refus de NaN sans modification du résultat précédent. Temps scénario
0,04 s ; aucune dépendance Bevy/GPU ou copie de l'algorithme dans le harness.

Le fichier V2 a SHA-256
`79ce3308083207cbd3e436eab87e60d9dc64d69542a95a8be692ecfd3a195816`.
Clippy standalone all/pedantic avec -D warnings passe également. Reçus
pebble-cells-rust-20261006-v2/result.json et clippy-result.json ; V1 est conservé.
Le check de métadonnées WASM ne lie pas l'application ; le check Engine
complet, le décodeur du package, le raccord au buffer/material runtime et le
dessin de cailloux restent à qualifier. Ce module n'est pas dans le candidat
Studio figé des 805 décors ni dans le portable déjà joué.

Le module Engine `pebble_patch` décode aussi les buffers little-endian du
patch acquis : 2 864 sommets, normales et aSlot, 4 704 triangles uint16 et
masque linéaire RGBA8 de 1 024 × 1 024. Il ne régénère ni graines, formes,
indices, couleurs du masque ni mips. Il vérifie dimensions, valeurs finies,
slots, normales non nulles et indices bornés avant de retourner les données.
L'appelant doit toujours fournir des payloads vérifiés par le package Assets.

La session 95262 passe le test du vrai module sur les cinq buffers acquis.
Le réencodage des attributs et indices est identique octet pour octet, ainsi
que le masque. Six entrées invalides sont refusées : attribut ou masque
tronqué, index hors limites, NaN, type de slot inconnu et normale nulle.
Un test passé, zéro échec/ignoré, 0,03 s. Métadonnées WASM et Clippy
standalone all/pedantic -D warnings passent également ; sources et fixtures
restent inchangées après exécution. Empreinte du décodeur :
`7a74444172fbbfe772ff55ff44eeb13269af78d68cf05746ae2c4916215bc6ef`.
Reçu : pebble-patch-rust-20261006-v1/result.json.

Ce test ne qualifie pas encore l'adapter entre DebrisPackage et ces buffers,
le Mesh Bevy, le matériau, le tampon de cellules, les textures/samplers du
runtime ni le dessin. Le candidat Studio 805 reste inchangé et continue sa
construction native ; ces nouveaux modules n'y sont pas annoncés intégrés.

Le raccord Engine `pebbles::prepare` résout ensuite les cinq buffers du patch
et les trois buffers de cellules depuis un unique DebrisPackage vérifié.
Il contrôle les paramètres acquis (cellule 4 m, rayon 25 m, grille 512,
4 704 triangles et 10 415 cellules occupées), puis prépare Patch et CellMap
avant toute publication renderer. L'origine vient du terrain consommateur.

Le harness de paquet inclut les trois vrais modules Engine et utilise le
lecteur Assets précédemment construit `libforge_assets-337e3eb88689de5e.rlib`,
SHA-256 `d406b267a5a30639a0c75c8e2746ab5b20a439f777d650ce196d014a90da0cb0`.
Son dep-info contient le module scanned/templates ; l'artefact et le dep-info
sont identifiés dans le reçu. Cela ne constitue pas un nouveau build complet
Engine ni une qualification du binaire joué.

V1 passe le test natif, puis le script refuse son invocation Clippy mal
formée (deux fichiers d'entrée). La commande corrigée révèle deux demandes
de style ; V2 les corrige, puis Clippy demande is_none_or. V3 corrige cette
condition et rejoue entièrement le scénario : un test passé, zéro échec ou
ignoré, 0,29 s, 150 poses et 2 553 cellules identiques à la référence, patch
complet, origine NaN et masque altéré refusés. Clippy standalone all/pedantic
-D warnings passe également. Les trois modules et le lecteur restent
inchangés après les contrôles. V1/V2 et leurs diagnostics sont conservés.

Empreinte du raccord V3 :
`a9cd2f25b5fb2da64b28309f6b938eecfad6e4c8b604c6f9580ff03c62a99139`.
Reçu : pebble-package-rust-20261006-v3/result.json. La préparation CPU depuis
le vrai paquet est maintenant testée ; le check Engine complet, les meshes,
textures/samplers, tampon d'instances, shader et dessin restent ouverts.
