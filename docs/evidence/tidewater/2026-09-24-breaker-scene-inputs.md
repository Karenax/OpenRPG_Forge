# Crêtes dans la scène et référence visuelle des déferlantes

Le module compute expose maintenant `install` et ses handles de buffers GPU.
Il s'attache à l'App existante après l'océan. Le mode partagé ne ferme pas
l'application à la fin de ses readbacks : la scène attend explicitement sa
fin avant de capturer. Aucun device supplémentaire ni transfert CPU des crêtes
vers le futur matériau n'est nécessaire.

Invocation : `tidewater_plant_probe FIXTURE NEW_PNG --water-breaker-compute
OCEAN_EXPORT SHORE_EXPORT BREAKERS_EXPORT`. Ce mode prépare et vérifie le
buffer ; il n'affiche pas encore le ruban.

## Validation native

`native-breaker-scene-compute-001.png` est strictement identique à la capture
SeaDetail précédente. Le buffer de crêtes de la scène est strictement identique
au calcul natif partagé isolé, SHA-256
`d7200dcefaec5248d176e6cefe9447a473d5017de0ee8daa3fc4f9bc6e0d0164`.
Les entrées FFT/crêtes utilisées restent les packs dépourvus de résultats source.

`breaker-scene-regression-001.json` atteste également :

- 21 fichiers FFT inchangés dans la scène et dans le test partagé indépendant ;
- six fichiers ShoreSim inchangés dans la scène ;
- les trois buffers du mode isolé historique inchangés après extraction du module.

Clippy strict et compilation réussis (`breaker-scene-clippy-002.log`,
`breaker-scene-build-002.log`). Binaire scène :
`3faf63c9ddaeb505314fbe4fecb1efbb46db4adadf6cd4f4aed392d68d003ff3`.
Binaire compute : `156e0a6065fd25a6bbaebbec892c567566c1a1dd877e332c7e817e4e8b503313`.

## Référence originale avec ruban

`render-source-vegetation.mjs --water-breakers` ajoute Breakers à la configuration
SeaDetail, calcule 120 pas puis dessine son matériau original après l'océan avec
le même buffer de profondeur. Les embruns et nuages restent désactivés.
Le rendu `source-water-breakers-002.png` utilise la fixture
`water-detail-seaward-001`, `--shadows=environment-village-003`, `--clear-sky`.
Capture inspectée ; SHA-256
`2f2b00c7f0b73157a8b81e1c2e5c7b85276d0c0b1a4d9c96e54a5efb82e9146a`.

Par rapport à `source-water-detail-001.png`, 1 485 pixels changent ; l'erreur
moyenne absolue de canal vaut 0,020603/255, le maximum 148/255. Cette mesure
décrit l'effet ajouté dans cette pose, pas une comparaison natif/source.
Frame, ShoreSim et paramètres SeaDetail restent identiques à la capture sans
ruban ; le buffer de crêtes est identique à l'acquisition numérique source.
Une deuxième exécution reproduit exactement PNG et quatre fichiers associés :
`breaker-source-render-repeat-001.json`.

L'essai source 001 avait une portée d'ombres différente dans sa commande ;
il n'est pas retenu comme référence appariée, même si la capture paraît proche.
Tous les résultats sont sous `E:/OpenRPG_Forge/.tidewater-integration/`.

Le matériau natif du ruban reste à construire et comparer. Aucun critère TW
complet n'est fermé ; animation, embruns, Studio et qualification globale restent ouverts.
