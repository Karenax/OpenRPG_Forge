# Repères locaux des bâtiments Tidewater

`prepare-building-frames.py` dérive les repères des 23 bâtiments depuis les
matériaux séparés vérifiés. La convention vient de `Buildings.js/frameFns` :
translation X/Z, rotation autour de Y ; les hauteurs de construction restent
en coordonnées monde. Le plancher est une ancre distincte lorsqu'il est fourni
par le constructeur. Aucun plancher n'est inventé pour le hangar à bateaux ou
l'étal. Ces repères ne constituent pas une qualification d'ancres fonctionnelles.

Positions et normales sont ramenées dans ce repère. UV, teintes, paramètres
de shaders et indices sont conservés. Les positions et normales originales
restent également disponibles : la transformation locale f32 n'est pas
bit-identique et ne doit pas remplacer silencieusement la route de référence.

## Vérification exécutée

Dans `E:/OpenRPG_Forge/.tidewater-integration/`, les sorties
`building-frames-001` et `building-frames-002` contiennent 781 fichiers identiques.
Les 112569 sommets sont vérifiés, ainsi que les hashes des entrées et indices.
Un second calcul, développé à partir du quaternion exporté et des composantes
explicites de rotation, reconstitue les coordonnées source : erreur maximale
4,728548e-7 m ; composantes normales : 3,270437e-8.
Résultat : `building-frames-proof-001.json`.
Hash du descripteur :
`ca54db1e2651cd7ca7fa6c91bd8bfeb62aca87dfc465f0896650d8f6a981fe71`.

## Raccordement restant

Le navigateur d'assets actuel épingle le hash d'un GLB dans un kit gouverné
dont la provenance est limitée aux créations Forge. On ne peut donc pas y
enregistrer ces bâtiments MIT en les déclarant générés par Forge. Le contrat
d'import doit représenter leur provenance réelle et leurs matériaux natifs.
Les placements canoniques, les commandes Studio, les captures après mouvement
et l'export GLB restent à réaliser. Ce lot prépare les données de transformation,
sans revendiquer un bâtiment déjà plaçable dans le produit.
