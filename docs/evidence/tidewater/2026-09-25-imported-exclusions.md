# Tidewater — exclusions d'auteur sur les instances importées

La population dérivée propose une vue d'exclusion référencée au hash du head de
zone. Elle exige la même base canonique, utilise les exclusions effectives (donc
les remplacements d'auteur) et retire les origines promues persistantes. Une
origine promue introuvable refuse la projection et exige une réconciliation.
Les enregistrements immuables ne sont jamais supprimés ou déplacés.

Les formes cercle, segment et polyligne réutilisent le même calcul que la
végétation procédurale, désormais accessible avec des coordonnées f64. Le chemin
historique f32 conserve sa conversion explicite ; les positions importées ne sont
pas arrondies avant le calcul d'appartenance.

Preuve complète des données :
`E:/OpenRPG_Forge/.tidewater-integration/imported-exclusions-verification-001.json`.
Sur les 65383 plantes, le cercle de centre (27, -379), rayon 50 m, exclut
**669 instances** et en conserve **64714**, après archivage et réouverture.
Le nombre est vérifié indépendamment sur toutes les positions source. Le premier
cercle essayé, centré en (0, 0), ne contenait aucune plante ; il ne qualifiait pas
une exclusion non vide et a été remplacé par cette zone réellement peuplée.

Trois tests d'intégration passent, dont le scénario correction → archive →
réouverture → annulation. Le même test couvre une origine promue retenue après
suppression du placement manuel, son archivage et le refus d'une origine absente.
Les 22 tests historiques de végétation monde passent après partage du calcul.
Clippy module/exemple passe avec `-D warnings`.

Il s'agit de la projection des exclusions, pas encore de la totalité des
corrections. Densité, habitat, dégagements des routes/bâtiments, reprojection sur
terrain retouché et création des placements manuels restent à raccorder, ainsi
que l'utilisation de cette vue dans le rendu Studio. Aucune preuve visuelle
nouvelle n'est revendiquée ici.
