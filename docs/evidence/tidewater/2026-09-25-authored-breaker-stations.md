# Tidewater — reconstruction des stations de déferlement

La simulation couplée reconstruit ses stations depuis le terrain effectif
validé, après vérification des fixtures sources. Le nombre logique, le buffer
des crêtes, la limite du noyau, la géométrie des lèvres et la limite de lecture
des ombres d'embruns suivent ce résultat. Aucun ancien point n'est conservé
lorsque la plage disparaît de la recherche source.

Sans station, une allocation inerte protège les liaisons GPU ; aucun ruban de
déferlement n'est créé. Le nombre logique reste zéro. Les matériaux d'océan
et la simulation du ressac continuent leur propre calcul.

## Vérifications

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- `verify-authored-stations.mjs` extrait la fonction pure `buildStations` du
  fichier source épinglé, hash
  `0f0119cd0f24a87b46d296f6847aec8145aa5048334b1f50869c08be13ee1d3b`.
- `tidewater_station_probe` compare nombre et hash de toutes les coordonnées.
  `stations-{raised,global,shallow}-{oracle,native}-001.json` : égalité exacte.
  Relief local : 577 stations, hash
  `0c8d25b4f743529a97a9264daafc17559184112025087d170a1da4a3fa99fe2b`.
  Élévation +0,5 m : 555 stations, hash
  `2071d567419af8568275781ff1cf81959aa0b0be67901b2580953e9c76a246a2`.
  Élévation +3 m : zéro station, conformément à la source.
- `authored-relief-states-003/shallow.json` est issu des commandes canoniques,
  adopté puis sauvegardé/réouvert dans `workspace-relief-shallow-001` avec ses
  11 blobs immuables. Les trois archives précédentes conservent leurs hashes.
- `relief-stations-proof-001` : rendu local et Undo achevés ; Undo reste
  pixel-identique à la référence. Captures globale et +0,5 m inspectées.
- `verify-station-render.py`, `station-render-proof-001.json` : buffers de
  crêtes de 55392 et 53280 octets pour 577 et 555 points ; 7014 et 6739
  particules actives. À zéro point, buffer inerte de 96 octets, aucune particule,
  aucun dépôt, historique du compteur nul sur 120 pas. Données flottantes finies,
  aucun ERROR GPU dans les journaux.
- Compilation et Clippy strict réussis sur les exemples concernés.
  Binaire de rendu :
  `ecaba1045a7397251e8f1f14a26c052691d84f0e3b2834cddfb884492395392f`.

## Limites

La recherche conserve les bornes exactes de Tidewater : plage principale dans
x=-175..195 et z=-110..12. Une élévation +3 m sort de ses conditions de recherche ;
le résultat vide ne signifie pas l'absence de toute côte ailleurs dans la carte.
L'égalité des coordonnées CPU et les captures natives ne prouvent pas une
parité GPU complète des scènes éditées. Directions filtrées, autres modes,
horloge continue, bâtiments, édition Studio et WebGPU restent à qualifier.
Les objets enterrés visibles après élévation rappellent le raccordement restant
du village. Aucun commit ni publication ; objectif global toujours incomplet.
