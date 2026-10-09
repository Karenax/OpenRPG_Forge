# Scans — variantes communes et liaison aux zones

Le module Assets `tidewater_debris::scanned::templates` prépare les variantes
et poses communes des 141 instances acquises. Il conserve l'identité d'asset
`acquired-scanned-variation:{source_id}`, la révision du package, le quaternion,
l'échelle et les données de variation aléatoire. Il ne crée pas d'identité
d'objet de zone, de document projet ni d'entité Bevy. Le consommateur Studio
doit toujours attribuer l'identité avec son binding commun, qui inclut l'AreaId.

Le raccord natif `scanned_placements` réutilise désormais ces templates au
lieu de reconstruire séparément les noms et transformations. Son identité
native existante est conservée. Cette modification prépare un raccord Studio
sans introduire un second contrat de placement ni des commandes spécialisées.
Le bootstrap natif consomme directement la géométrie déjà vérifiée par
ScannedInput avec `from_geometry`, sans la redécoder pour les placements.

Le nouveau scénario acquis dans `debris_common_authoring.rs` passe par le vrai
binding Studio et ZoneEditSession. Il contrôle les 141 identités distinctes,
leur séparation entre zones et l'absence d'import automatique dans une zone
non associée. Pour chacun des quatre prototypes, il exerce transformation,
duplication, verrouillage, sauvegarde/réouverture, déverrouillage, suppression
de l'original et de la copie, annuler/rétablir puis une nouvelle réouverture.
La résolution doit conserver la suppression même si la source est de nouveau
disponible. Il s'agit d'une preuve CPU de commandes et persistance, sans UI/GPU.

Session 72408 : résultat acquis PASS, code 0, un test passé, aucun échec
ni test ignoré, un test filtré. Construction 24 min 47 s ; scénario 0,41 s.
Les cinq empreintes du reçu `scanned-common-authoring-20261005-v1.json`
sont revérifiées après le résultat et restent identiques.
Session 51875 : Clippy Assets strict passe code 0 (34 min 08 s avec attente
du verrou). Le warning du cache incremental forge_core signale un hard-link
impossible puis un repli par copie ; aucun diagnostic possédé n'est signalé.
Le check WASM Assets puis celui de l'exemple natif restent actifs/en attente
sur le cache partagé. Ce PASS Clippy n'est pas attribué aux deux checks suivants.

Le candidat Studio 805 décors `studio-debris-candidate-20261005-v2` conserve
ses sources figées et ne contient pas ce nouveau raccord des scans. Son build
frontend termine code 0 sous la session 38360 ; l'assemblage et le contrôle
HTTP statique passent, et la construction native 56358 est lancée.
Ces travaux ne prouvent pas encore
le chargement HTTP, le rendu, le picking ni les gestes Studio des 141 scans,
et ne reconstruisent pas le portable existant.
