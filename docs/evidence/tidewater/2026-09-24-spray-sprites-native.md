# Embruns visibles dans la scène native

Référence : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

`prepare-spray-sprites.py` adapte les shaders originaux aux matrices Bevy,
aux quatre buffers GPU (position, vitesse, informations, crêtes) et à la copie
de profondeur opaque déjà utilisée par l'eau. Le matériau conserve les deux
textures et leurs mipmaps, l'ombre des vagues, l'éclairage par type de particule,
les silhouettes étirées, les intersections douces et l'alpha prémultiplié.
Les 366 fichiers des fixtures `water-spray-seaward-001/002` sont identiques.

Le mode scène `--water-spray OCEAN_EXPORT SHORE_EXPORT SPRAY_EXPORT` installe
la simulation native couplée sur le même appareil Bevy. Le ruban lit ses crêtes,
les sprites lisent ses particules, l'eau et le sable lisent son état de rivage.
L'état final est copié sur le GPU vers l'image publique ; le readback de preuve
relit cette image. Aucun résultat de particules ou de rivage source n'est chargé.
Le budget d'émission reste un calendrier source rejoué. Le vent de la scène
doit correspondre au Frame de la simulation ; un désaccord est refusé.

Le probe numérique peut maintenant être installé sans fermer l'application
appelante à la fin de ses readbacks. Sa limite « no spray rendering » concerne
sa propre preuve de calcul ; le rendu est effectué séparément par la scène.

## Capture et comparaison

Capture inspectée : `native-spray-sprites-001.png`. La répétition `002` est
identique, SHA-256 :
`1c1604cdb8af91222a8fe407a788702f481998b18064b141a265bcb6845dc596`.
Référence appariée : `source-water-spray-003.png`, vent global 6/[1 ; 0],
caméra côtière fixe, temps 2 s, résolution 1920×1080.

`spray-sprites-comparison-001.json` compare l'ajout des sprites et de leur
effet sur le rivage aux captures respectives sans embruns :

| Mesure | Valeur |
|---|---:|
| Pixels modifiés de plus de 3 niveaux, source / natif | 14 532 / 14 507 |
| Intersection des régions modifiées | 14 470 |
| Région source dilatée d'un pixel | 18 530 pixels |
| Erreur moyenne par canal dans cette région | 0,150027 / 255 |
| Percentile 99 de l'erreur maximale par pixel | 4 / 255 |
| Erreur locale maximale | 41 / 255 |
| Erreur moyenne sur toute l'image | 0,174586 / 255 |

Les métriques incluent les différences préexistantes et les divergences de
trajectoires/écume ; elles ne prouvent pas une image exactement identique.
Le changement d'écume et les sprites ne sont pas isolés l'un de l'autre ici.

## Régressions et portée

`spray-sprites-regression-001.json` vérifie les deux scènes et un nouvel essai
isolé contre l'historique couplé antérieur : particules actives identiques modulo
emplacements, crêtes, compteurs, dépôts et état public du rivage exacts. Les 21
buffers FFT restent identiques. Le retour au mode ruban seul reproduit exactement
son PNG antérieur ainsi que ses 21 buffers FFT, six buffers rivage et une sortie
de crêtes. Le vent incompatible est refusé sans PNG produit.

Clippy strict et compilation réussis (`spray-sprites-clippy-002.log`,
`spray-sprites-build-001.log`), shaders compilés et exécutés par le rendu réel.
Binaire scène : `ae4609758a4f25780540149bc07b32bde686d28a59c3e9819c5b008a8c833f76`.
Binaire simulation : `d6288807cc80c08f0e384593b753feda9b4b3db662a85b479c1b72a463f7b07a`.

Le maillage du prototype représente les 40 960 slots par 163 840 sommets et
245 760 indices dans un draw ; la source utilise un quad instancié. L'ordre des
slots est conservé, mais cette représentation n'est pas une qualification de
performance. L'état public du rivage est publié au cliché final, pas encore à
chaque frame interactive. Budget adaptatif natif, parcours animés, variantes de
vent, nuages, nuit, CPU emitters, collisions de coque et qualification WebGPU
restent ouverts, comme Studio et la carte complète.
