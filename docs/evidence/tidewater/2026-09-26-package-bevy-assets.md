# Préparation Bevy des bâtiments du paquet

`forge_engine::tidewater_assets` consomme `BuildingPackage`, sans résoudre de
chemins extérieurs. Les références d'asset sont contrôlées par identité et hash.
Les maillages passent par le chargeur partagé ; les images reprennent le format
RGBA8 linéaire, les mipmaps d'origine et le sampler Repeat/Linear/aniso 8.
La bibliothèque de matériaux est préparée une fois pour les instances.

Le choix de coordonnées est explicite : `Local` pour une nouvelle pose,
`SourceReference` pour la route de rendu qui applique un delta à la pose source
et évite les arrondis du passage en coordonnées locales. Les positions et
normales de référence restent donc disponibles sans abandonner les pivots.

La bibliothèque et l'exemple `tidewater_package_probe` passent `cargo check`.
Clippy strict sur `forge_engine` avec `--no-deps` passe également ; cela ne
qualifie pas les lints des dépendances. L'exemple prépare les deux géométries
pour chaque asset et compare leur nombre de sommets, indices et matériaux.
Son exécution réussit : 23 bâtiments, 111 morceaux, 112569 sommets, sept matériaux
et quatorze textures avec mipmaps préparés depuis `building-package-002`.
Aucun rendu depuis le paquet ni accès Studio n'est encore revendiqué.

Le prototype scène reçoit une option supplémentaire après le workspace :
`--building-package ROOT EXPECTED_SHA256`. Le code charge le paquet, remplace
les morceaux de bâtiments et les ressources matériau, puis applique les
placements canoniques. Les ombres lisent les mêmes payloads en mémoire.
Ce branchement reste non qualifié : le `cargo check` du 26 septembre s'arrête
dans `forge_world/river_vegetation.rs:33`, appel à `river_carving::validate`
avec un f32 au lieu de `Option<f32>`, avant validation du prototype.
Après inspection, l'appel a été adapté à `Some(river.maximum_cut_m)` : la
validation du budget reste active. Les 13 tests river_carving et trois tests
river_stroke passent, dont le filtrage de végétation. Le check scène et Clippy
ciblé `--no-deps` passent ensuite. Build puis capture sont lancés.

Une fixture dédiée `package-only-buildings-scene-001` retire 598 fichiers de
l'ancienne scène : payloads exclusifs aux bâtiments, ressources de matériau et
descripteurs de catalogue. Les 387 fichiers restants portent le décor résiduel
et les autres systèmes. Les métadonnées d'appartenance des nœuds sont conservées
pour remplacer les bâtiments avant le chargement. Le script
`prepare-package-only-buildings-scene.py` prépare cette fixture sans toucher
à la source ; cela permettra de tester le rendu sans secours de géométrie ancien.
La capture `native-package-buildings-001` échoue explicitement : Bevy cherche
encore `village.wgsl` dans la fixture, où il a été volontairement retiré.
Ce test révèle donc un accès fichier résiduel, et ne qualifie pas le rendu.
Le matériau est corrigé pour référencer un handle de shader stable ; la source
vérifiée du paquet est installée en mémoire par `install_shader`. Le prototype
sans paquet utilise également cette route, depuis ses bytes de fixture vérifiés.
Compilation après correction réussie. Le second lancement
`native-package-buildings-002` échoue sur une autre cause : la texture de
profondeur de fenêtre mesure 2560×1369, alors que la capture d'eau exige
1920×1080. Aucun PNG n'est produit. La fenêtre de qualification est corrigée
avec redimensionnement et bouton de maximisation désactivés ; le check passe
et la nouvelle compilation est lancée. Cette correction ne change pas les
fenêtres Studio. Nouvelle capture encore à qualifier.

La qualification suivante réussit : `native-package-buildings-003.png` depuis
la fixture privée des 598 anciens fichiers a le hash
`579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
La comparaison avec `native-buildings-initial-001` donne zéro pixel différent
et les deux buffers d'ombres identiques (`package-render-proof-001.json`).
Avec la maison 16 déplacée de 12 mètres en X dans le workspace enregistré,
`native-package-moved-001.png` reproduit exactement `native-buildings-moved-002` :
hash `694f93ed45a45a4d54c3097665c5353a9416d698e9697a0beed8e57fdaed101c`,
zéro pixel différent et les deux buffers identiques
(`package-moved-render-proof-001.json`). Les deux logs ne contiennent ni ERROR
ni panic. Cela qualifie le remplacement des bâtiments par leur paquet dans
ces deux captures natives ; le décor restant dépend encore de la fixture.

Pour le raccordement aux racines de placement Studio, `ResidentMeshes` prépare
les géométries locales du paquet avant de les inscrire dans `Assets<Mesh>`.
Chaque asset est résolu par identité et révision exacte ; plusieurs placements
peuvent partager ses handles sans copier les buffers. Cette API ne constitue
pas encore un branchement dans l'interface Studio.
L'exécution de `tidewater_package_probe` réussit avec ce cache : 111 maillages
résidents pour les 23 bâtiments ; les handles clonés sont identiques et toutes
les résolutions avec une mauvaise révision sont refusées. Le nombre d'assets
reste 111 après ces duplications. `rustfmt --check` et Clippy strict ciblé
(`cargo clippy -p forge_engine --example tidewater_package_probe --no-deps -j 1
-- -D warnings`) passent. Cela vérifie la réutilisation des buffers côté CPU,
pas encore le parcours utilisateur de duplication dans Studio.
