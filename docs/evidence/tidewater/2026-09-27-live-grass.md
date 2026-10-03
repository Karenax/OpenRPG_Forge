# Herbe suivant la caméra dans la promenade native

Portée : scène Tidewater native, référence `1438b1ab`, branche Rust locale.
Suite du [parcours jouable](2026-09-27-walk-integration.md) et de la
[cadence des ombres](2026-09-27-shadow-cadence.md).

## Changement

La promenade conservait les cellules d'herbe sélectionnées à la caméra initiale.
`tidewater_plants::grass` conserve maintenant la grille source complète et une
résidence de trois géométries, trois matériaux et trois buffers. Les 628 slots
de cellules sont réutilisés : capacités 48 / 160 / 420, records de 32 octets.
Le déplacement change les données des buffers et la visibilité des slots,
sans recréer les meshes ni les matériaux. Aucun slot par brin d'herbe.

La sélection suit l'ordre z/x et les règles source : cellules de 8 mètres,
rayons 18 / 46 / 88 mètres, proximité au rectangle horizontal, boîte verticale
min−0,5 / max+1,8 et six plans de frustum. Les bornes de boucle sont calculées
avant le clamp : une cellule tangente située hors de la plage d'indices source
n'est pas ajoutée par un parcours naïf de toute la grille. Les valeurs non
finies, dimensions incorrectes et sélections dépassant les budgets sont refusées.

L'adaptateur de promenade sélectionne après la propagation des transforms de
la caméra. Il conserve les seuils source de rafraîchissement : 0,05 mètre par
axe, 0,001 par composante de direction, ou changement de projection. Les
géométries, masque, textures, vent et réception des ombres restent partagés.
L'herbe conserve son absence de projection d'ombre, comme avant ce changement.

Le smoke ajoute un trajet par commandes du contrôleur Forge vers cinq points
intérieurs et trois lectures GPU des buffers d'herbe après immobilisation.
Les usages `COPY_SRC` sont activés au démarrage seulement dans les modes de
test, avant la première extraction de rendu. Aucun événement clavier/souris
du système d'exploitation ni téléportation n'est utilisé pour ce trajet.

## Comparaison avec la source

Artefacts locaux sous `E:/OpenRPG_Forge/.tidewater-integration/`.

- `grass-live-cpu-001.run.log` : 26 tests PASS. Modules réels de sélection et
  résidence de l'herbe, vent, ombres, cadence et récepteurs dans un harnais
  borné ; aucune suite exhaustive du moteur n'est revendiquée.
- `grass-source-oracle-001.cjs/json` : exécution CPU du corps verbatim de
  `GrassField.update(camera)`, lignes 533–608, avec les vrais flags/min/max du
  bundle. 19 vues, positions intérieures et extérieures, joints de cellules,
  limites ±1112 et plans explicites. Les matrices et le frustum sont des
  adaptateurs de test ; ce contrôle ne qualifie pas la projection Three.js.
- `grass-live-cpu-oracle-001.run.log` : les trois listes ordonnées de cellules
  correspondent exactement à `Grid::select` Rust dans les 19 cas. Un test PASS,
  26 autres filtrés ; compilation directe Rust 1.98.0 avec dépendances du projet.
- Module source SHA-256 :
  `d2ca7c8baf4aa995228eabf8dec5dbddfd388eee7f6ede3e218bc43ce4e0c648`.
  Méthode verbatim :
  `7f9785db2736b84dc7c1af5eb8e2db5168aecda1437c3e37247739f88d7e0a25`.
- `grass-live-clippy-007.log` : moteur et quatre exemples PASS avec
  `--no-deps -- -D warnings`, 3,98 s.
- `grass-live-studio-wasm-001.log` : Studio lib, sans features par défaut et
  avec `web`, cible WASM PASS, 8 min 25 s incluant l'attente Cargo.
  Avertissement préexistant `proc-macro-error2` ; aucun navigateur exécuté.

## Qualification native

- `grass-live-build-003.log` : build natif PASS, 2 min 01 s. SHA-256 du binaire :
  `489636c1eb79f0cbdb12c7dd1e872b0d3b4be68decc725c46d942fc6133b48dd`.
  Le build initial précédait l'ajout de `COPY_SRC` au test ; seul ce candidat
  final sert aux résultats ci-dessous. Le formatage final ne modifie ensuite
  que des assertions sous `cfg(test)`, sans changement sémantique.
- `native-walk-grass-live-001` : PASS, même parcours côtier de 14,887203 m,
  1843 ticks dont 1838 au sol. 178 mises à jour d'herbe, deux échantillons
  spatiaux différents ; trois buffers GPU conformes aux données attendues
  (1536 / 5120 / 13440 octets). 4222 soumissions d'ombres / 4222 frames main,
  périodes 1/2/4 et zéro récupération. Eau : ratio temps simulé/réel 1,001757.
- `native-walk-grass-inland-001` : PASS, trajet cumulé de 159,993454 m et
  déplacement net de 143,086731 m. Les cinq points sont atteints ; 3647 ticks
  dont 3642 au sol. Position finale environ (−60,1194 ; 10,1209 ; −190,2270).
  629 mises à jour d'herbe, vingt échantillons espacés d'au moins 8 m, cellules
  différentes le long du trajet. Les trois lectures GPU correspondent aux
  buffers CPU. 5591 soumissions d'ombres / 5591 frames, zéro récupération,
  périodes 1/2/4 ; cinq poses GPU de cascades valides. Vent A/A identique,
  A/B modifie [2930114,394316,18427] texels. Eau : 94,403699 pas/s, ratio
  temps simulé/réel 1,001075 malgré la baisse de fréquence du rendu.
- Captures des deux promenades inspectées : scène côtière conservée ; sous-bois
  dense, relief, troncs, feuillage et herbe visibles au point intérieur.
  Le joueur est encore représenté par la capsule de prototype. Aucun verdict
  de parité artistique ou d'animation n'est déduit de ces PNG seuls.
- `native-grass-live-bootstrap-001` : replay statique PASS, sortie 0, 6,78 s.
  PNG, hill shadow, profondeur objets et IDs plantes identiques octet par
  octet à `native-shadow-rate-bootstrap-001`. SHA-256 comparés dans
  `grass-live-static-comparison-001.json`. Les 47 IDs restent résolus ; les
  occluders de picking ne sont pas qualifiés par ce lot.

Essais natifs séquentiels sur RX 7800 XT/Vulkan, AMD 26.8.1, 1920×1080,
sans autre probe ni compilation. Les deux promenades signalent un canal de
readback fermé lors de l'arrêt, après leur verdict PASS. Aucun processus de
compilation ou de test n'est laissé actif. Formatage et `git diff --check`
contrôlés ; aucun commit ni push.

## Performance observée

| Parcours | Moyenne FPS | p95 frame (ms) | 1% low FPS |
|---|---:|---:|---:|
| Côte, 14,9 m | 143,517224 | 7,8118 | 73,691088 |
| Intérieur, 160 m | 95,320536 | 31,4760 | 24,970040 |

Mesures des intervalles de frames main, pas de timestamps GPU. Le seuil moyen
de 60 FPS passe sur les deux parcours, mais les frames lentes en intérieur
restent à optimiser. L'écart avec le précédent essai côtier à 120,36 FPS ne
prouve pas à lui seul une amélioration causée par ce changement : aucune
mesure appariée répétée n'isole la cause de cette variation.

## Limites

La grille demeure celle du terrain importé. Son recalcul après modification du
relief ou des masques dans Studio, l'édition de l'herbe dans le produit, le
parcours intégral de l'île, la parité animée et l'exécution WebGPU navigateur
restent ouverts. Une lecture des buffers valide leurs données, pas à elle seule
leur apparence ni leur animation.

L'inspection de `Vegetation.js`, `Scatter.js`, `App.js` et `Player.js` montre
que les troncs de végétation de cette référence ne sont pas des obstacles du
contrôleur. Les cercles d'occupation servent au placement. Aucun collider de
tronc supplémentaire n'est introduit au titre de la fidélité ; les collisions
du relief, des bâtiments et des autres objets conservent leur portée propre.
