# Alignement des placements sur terrain importe

PlacementSnap expose resolve_sampled et resolve_axis_sampled, avec un
SampledTerrainPlan deja construit depuis un document et un raster verifies.
Les chemins proceduraux existants deleguent au meme calcul de grille et d'axe.
Le document canonique et les octets du raster ne sont jamais modifies.

L'ordre reste grille horizontale, conservation de l'axe inactif, evaluation de la
surface, decalage vertical, validation du transform final. Rotation et echelle
sont conservees. Une manipulation explicite en Y refuse le verrouillage au sol.
Pour un raster, la surface comprend la hauteur bilineaire, l'elevation generale
et les corrections d'auteur. Une demande d'alignement hors de l'etendue centree
est refusee ; le mode sans alignement preserve les coordonnees absolues.

Validation : cargo test -p forge_world --test sampled_authoring --locked --offline
-j 1 : PASS, 6 tests (session 74396). Le nouveau test verifie une hauteur explicite
17.75 m (7.5 de raster + 2 d'elevation + 8 de retouche + 0.25 de decalage), l'axe X,
la preservation du transform, les refus et l'immutabilite des sources.
Regression des quatre tests proceduraux : PASS, session 70671.
Clippy forge_world --lib --test sampled_authoring --no-deps -D warnings : PASS.

Ce lot est moteur-neutre. Les controles de placement de Studio ne l'appellent
pas encore : leur acces au raster de la zone doit etre branche au transport
verifie avant de pouvoir qualifier le placement dans l'interface.
