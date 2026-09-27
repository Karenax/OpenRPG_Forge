# Vagues plongeantes — référence des crêtes et géométrie native

La géométrie dérivée de la plage est reproduite en Rust, sans importer les
positions des stations comme résultat natif. `breaker_geometry::beach_stations`
retrouve la ligne de côte sur le terrain, choisit sa plus longue section
continue, la lisse trente fois et l'échantillonne par longueur d'arc à 0,6 m.
Le callback de qualification utilise la convention bilinéaire à demi-texel de
TerrainData et conserve le calcul f64 avant stockage f32, comme la source.
Il ne crée pas une seconde représentation canonique de carte.

## Comparaison native exacte

L'exemple `verify_breaker_geometry` lit le terrain de la référence après village,
vérifie ses hashes, régénère les stations et construit les deux rubans par
segment (deux parités de vague, profil de vingt sommets). Les résultats sont
identiques octet pour octet à ceux du constructeur original :

| Donnée | Taille | SHA-256 |
|---|---:|---|
| 577 stations | 9 232 octets | `0c8d25b4f743529a97a9264daafc17559184112025087d170a1da4a3fa99fe2b` |
| 46 080 identifiants de sommets | 737 280 octets | `72525103c084cb6537ed315b78056dcde80aa9635dc82043714871b6c0a547ec` |
| 131 328 indices | 525 312 octets | `49ccc7aeaaba995db98935a2acf9f281801c5a9938c3fe675168fb538eeea59a` |

Rapport `native-breaker-geometry-001/verification.json` et compilation/exécution
`breaker-geometry-build-001.log` réussis.

Les deux tests ciblés réussissent (`breaker-geometry-tests-001.log`) : absence
de stations sans front de mer et orientation/espacement sur une plage droite.
Clippy final `-D warnings` réussit (`breaker-geometry-clippy-003.log`). Les
passes 001/002 ont signalé l'écriture du milieu et des noms locaux trop courts :
les noms sont clarifiés, et l'arithmétique originale du milieu est conservée
avec une exemption locale justifiée par les bornes -110..12,5. Ces ajustements
finaux ne changent pas les calculs qualifiés.

## Oracle GPU original

`export-breakers.mjs` capture le kernel original de recherche de crêtes, ses
bindings, les paramètres, la géométrie des rubans, les shaders du matériau,
la dentelle et les entrées FFT. La simulation originale avance pendant
540 pas à 60 Hz. Spray et nuages sont explicitement désactivés dans cet oracle.
Les exports `breakers-source-002/003` ont 72 fichiers et manifestes identiques.
Le premier essai `001` a échoué avant écriture (nom de propriété FFT incorrect)
et ne constitue pas une acquisition.

| Pas | Temps | Emplacements de crête actifs | Segments dessinables selon les tests du vertex shader |
|---|---:|---:|---:|
| 1 | 1/60 s | 471 | 108 |
| 120 | 2 s | 690 | 232 |
| 540 | 9 s | 731 | 303 |

`verify-breakers.py` contrôle répétition, terrain épinglé, valeurs finies,
normales des stations, indices, temps et crêtes non vides. Les quatre couches
des neuf mips de déplacement au pas 120 sont identiques à l'oracle FFT
précédent `ocean-source-003`. Rapport `breakers-reference-verification-001.json`.
Les autres attributs d'un emplacement désactivé peuvent conserver leur ancienne
valeur ; son identifiant nul gouverne sa visibilité, comme dans la source.

## Suite et limites

Le calcul GPU natif des crêtes reste à raccorder au FFT/terrain/ShoreWaves du
graphe commun, puis son buffer doit alimenter le vertex shader du ruban.
Le matériau devra conserver alpha prémultiplié, double face, test de profondeur,
absence d'écriture de profondeur et ordre après l'eau. L'oracle ci-dessus ne
prouve pas ce rendu, ni les embruns et leurs dépôts dans ShoreSim.

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`. Source figée intacte,
travail existant conservé et aucune publication. Tous les critères TW complets
restent ouverts.
