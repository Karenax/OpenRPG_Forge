# Tidewater — terrain du workspace et reprojection des plantes

Le chargement workspace transmet maintenant le document de terrain effectif et
son bloc de hauteurs vérifié au prototype. La description raster et l'étendue
doivent correspondre à la scène de référence. Terrain visible et profondeur
d'ombres lisent le même jeu de données en mémoire. Les copies de hauteurs dans
la fixture ne sont plus nécessaires pour ce parcours.

## Rendu natif du terrain source

Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/` :
`workspace-terrain-scene-001` dérive de la fixture workspace précédente avec
`heights.bin` retiré physiquement et de son manifeste. Elle ne contient toujours
aucun bloc canonique de végétation ni aucun des 24 anciens buffers d'instances.

`workspace-terrain-render-proof-001/report.json` : rendu de la clairière
sauvegardée identique pixel par pixel à `native-authored-clearing-002.png`,
64868 plantes restantes/515 exclues. Zone absente et source corrompue refusées,
autorité workspace inchangée avant/après. Binaire natif exécuté :
`2162e75fb07edd46e96f805a33cb2eb9444c962b8928c79b3431d015bc24854b`.
Compilation et Clippy strict des exemples scène/ombres réussis.

Le document effectif est maintenant transmis : les corrections de relief ne
sont plus silencieusement ignorées. Les gardes CDLOD existantes refusent encore
un relief édité tant que son évaluation GPU n'est pas raccordée. Les matériaux,
masques, dérivés d'ombre et paramètres aquatiques restent issus des fixtures.

## Projection CPU des plantes sur un relief édité

`ImportedPopulation::terrain_view` applique exclusions et densité, puis ajoute
à Y la différence entre hauteur effective et hauteur de base, évaluées par
`SampledTerrainPlan`. Elle conserve donc le décalage original plante/sol.
Identité, clé source, X/Z, rotation, échelle et paramètres restent inchangés ;
les sources immuables ne sont jamais réécrites. Aucun changement de hauteur
préserve exactement les valeurs d'origine, y compris après annulation.

Quatre tests `imported_population` passent, dont un scénario nouveau :
reprojection locale de +4 m, archive/relecture, annulation exacte, élévation
globale de +3 m et refus d'un bloc corrompu. Les tests de population, exclusions,
densité et promotion préexistants restent verts. Clippy strict du test et du
vérificateur réussit.

`imported-terrain-projection-001.json` vérifie les 65383 placements réels :
élévation de 3 m après sauvegarde/relecture, erreur verticale maximale
`5.684341886080802e-14` m, identité et autres attributs exacts, annulation exacte.
La projection sans retouche égale également chaque record source.

Cette projection reste une étape monde CPU : elle n'est pas encore consommée
par le renderer, qui refuse encore les retouches de relief. Routes, rivières,
bâtiments, placements manuels, inclinaison sur la normale, collisions et édition
interactive Studio restent à raccorder. La parité complète reste ouverte.
