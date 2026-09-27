# R1-065 — Corriger la caméra enterrée, pas les racines d'herbe

2026-09-05, branche Rust locale, Gate 1 ouverte.

## Cause établie

La collecte de quatre angles a révélé une apparence de végétation suspendue dans la vue
`human_bridge`. Le contrôle géométrique parcourt les racines des meshes d'herbe des chunks
translatés, puis compare leur altitude mondiale à `TerrainGenerationPlan::surface_at` sur le
terrain sculpté : erreur inférieure à 1 mm. Ce contrôle inclut plus de 1 000 racines et ne se
limite plus au chunk d'origine ou au terrain historique.

La caméra était à Y=2 m, le terrain à Y=3,6872048 m : **1,6872048 m sous le sol**. Le terrain
vu depuis son envers masquait incorrectement les végétaux du point de vue de cette capture.
L'assertion de hauteur humaine 1,6–1,8 m a été ajoutée puis exécutée avant correction : RED,
avec `human camera clearance=-1.6872048`. Après correction : GREEN.

## Changement effectué

WorldSketch passe de révision 2 à 3. La caméra conserve ses XZ, FOV et direction :
position `[-35, 5.4, -205]`, cible `[-40, 11.4, -90]`, soit une translation verticale de 3,4 m.
La hauteur réelle est maintenant **1,7127952 m**. Aucun déplacement automatique implicite
dans l'adapter : les coordonnées authorées restent explicites et vérifiables.

L'aperçu du kit met à jour uniquement ses pins WorldSketch/terrain. Aucun asset, pose, fichier
Blender, spline, landmark ou verrou n'est modifié. Le test de conservation existant vérifie
explicitement les nouvelles coordonnées puis compare tout le reste à la recette historique,
en ne normalisant que les changements riverains déjà autorisés et la révision.

Trois compilations produisent des manifestes identiques (hashes de toutes leurs sorties).
Comparaison du terrain compilé révision 2/3 après normalisation du seul champ `revision` :
identité exacte du JSON, y compris les deltas sculptés.

Hashes canoniques courants :

- WorldSketch : `42fe7157fb8b939348fc48dcb729e2df6837aae20bcbc71c57b8acceda2e1ff1` ;
- terrain sculpté : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208` ;
- pont : `e7535d7487bdf04cf6d4772b0388c548b5f32713048b9b8d17e2909512fcb216`.

## Preuve visuelle ciblée

Racine externe : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-065-camera-height-20260905`.
Même commande de collecte que R1-065 initial, avec `--camera human_bridge` et sortie `captures`.
Ce filtre est ajouté au script pour ne pas multiplier les captures non affectées.

Deux lancements natifs code 0, clair/pluie, PNG Full HD inspectés. L'apparence de végétation
suspendue disparaît et le sol est visible devant la caméra. Contrôles sources, terrain, caméra,
20 instances de kit, 36 colliders, pont, météo et journaux sans ERROR passent.
Le runtime utilisé pour ces captures est celui précédemment vérifié, alimenté par le **fichier
WorldSketch révision 3 explicite**, pas une affirmation sur une ancienne source embarquée.

SHA-256 octets :

- runtime de capture : `a7ecca1cc67683bf5d1ab236244cdabc9a3a56bde77ccea91650dd08eca7d854` ;
- manifeste de capture : `8adcc1592b1df9360a5f2ad32c774dc3ab317bbaf023b6211d74cbd48c2e2510` ;
- PNG clair : `19ee61c2e04afa5eb7ba24e7c076ae8d47a9082af3bd849658e61d86c46c442d` ;
- PNG pluie : `802318f8b2cf39c90e597d666bd2387c135b1a6233324558d1f825bdaa90e548` ;
- source JSON révision 3 : `5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.

Ces captures ont tourné pendant une compilation : leurs métriques ne sont pas comparables à un
benchmark isolé. Aucune amélioration de performance n'est revendiquée. L'eau reste bruitée en vue
rasante, l'herbe et les arbres prototypaux, le sol peu détaillé. Ce sont des limites distinctes.

## Validations et limites

318 tests workspace PASS (Assets 14, CLI 7, Core 11, Engine 103, Project 45, Runtime 47,
Studio 25, World 66). Deux attentes CLI de révision 2 ont été actualisées à 3 après échecs,
puis la suite entière rejouée. Clippy workspace strict PASS après remplacement de deux
comparaisons de flottants par une comparaison exacte de leurs bits ; test World concerné rejoué.

Build runtime final PASS. Le binaire reconstruit
`1ac500af03396f866ad9a4973e267893da66384d0bd9eb43c72deb386c4a1a5a` charge ensuite la source
**embarquée** sans fichier externe : `--world-sketch-visual-valley --bridge-crossing-probe`.
Journal `embedded-probe.jsonl`, événement du 19:03:55.937779 UTC : code 0, hashes révision 3
ci-dessus, 42,5282 m parcourus, 518/518 ticks au sol, 435 sur le tablier, aucune perte de contact,
bindings conservés et aucune erreur runtime. Le fournisseur de navigation reste `not_selected`.
Formatage, diff et deux tests Python du collecteur (huit anciennes vues et sept corruptions)
passent également. Les processus de compilation et de probe sont terminés.

Les anciennes preuves Chrome ne qualifient pas automatiquement les hashes révision 3.
Douze vues, vidéo, comparaison Cadle et acceptation artistique restent ouvertes. Aucun
commit, push, nouvelle gate ou asset Cadle.
