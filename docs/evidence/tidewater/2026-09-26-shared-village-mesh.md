# Chargeur de maillages village partagé

La conversion des buffers de géométrie en `Mesh` Bevy quitte le chargeur privé
du prototype pour `forge_engine::tidewater_mesh::VillageMeshBytes`.
Le prototype utilise désormais cette même API publique, disponible pour un
adaptateur Studio. Les attributs personnalisés `VillageTint` et `VillageData`
gardent leurs identifiants et leurs formats ; positions, normales, UV, paramètres
source et indices sont conservés dans leur ordre original.

L'adaptateur n'effectue aucun accès disque et ne déclare aucune provenance.
Son appelant doit vérifier les hashes et la fermeture des dépendances. Il
vérifie les tailles avant décodage, impose au maximum deux millions de sommets
et 24 Mo d'indices, refuse les composantes non finies et les indices hors limites.

Les matériaux et leurs bindings d'environnement restent dans le prototype.
Cette extraction ne constitue donc pas une disponibilité Studio ni une preuve
de rendu inchangé après refactoring.

Validation : le test ciblé de conservation des paramètres et de rejet des
payloads invalides passe (1 test). Clippy a demandé le remplacement des
`chunks_exact` par des `as_chunks` typés ; correction appliquée et Clippy strict
de la bibliothèque réussi. Le test relancé après cette correction passe également.
Les deux diagnostics de longueur de fonctions terrain ont été résolus en extrayant
la configuration CDLOD inchangée. Clippy strict bibliothèque et prototypes scène
et ombres passe ; compilation du binaire scène réussie.

Capture `native-shared-mesh-001.png`, sous
`E:/OpenRPG_Forge/.tidewater-integration/`, depuis le workspace initial des bâtiments :
zéro pixel différent de `native-buildings-initial-001.png`. Les buffers d'ombre
des objets et du relief sont également identiques. Aucun ERROR dans le journal,
code de sortie 0. Rapport : `shared-mesh-render-proof-001.json`.
Hash PNG : `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
Cette pose vérifie la non-régression du chargement partagé ; Studio reste ouvert.
