# forge_world

Produit de façon déterministe la scène de preuve Gate 0 (sol, cube, lumière et caméra) depuis une
recette versionnée. La sortie est un `SceneProofDocument` moteur-neutre ; aucun mesh ou asset externe
n'est stocké dans la recette.

Pour `R1-001`, la crate produit aussi la fixture originale
`examples/visual_valley/terrain.v1.json` depuis `VisualValleyTerrainRecipe`. Le document persistant
reste défini par `forge_project::TerrainDocument` ; `forge_world` ne possède pas une seconde source de
vérité. Cette première tranche fixe seulement la seed, l'étendue 512 × 512 m, les paramètres de height
sampling et trois couches matériau logiques. Chunks, mesh, rendu PBR et collisions restent dus aux
lots suivants.
