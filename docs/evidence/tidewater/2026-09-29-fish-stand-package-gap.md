# Échoppe du pêcheur — constat de disponibilité

La petite échoppe visible près du ponton dans la paire seaward n'est pas un des
23 bâtiments du village importé. Dans le source Tidewater épinglé
`1438b1abfcaee3267092b75573014f4d9b4a983c`, elle est construite à l'exécution
par `src/game/FishStand.js` : `FishStandStall` se place à `x = 49,9`,
`z = -74,6`, `yaw = 1,45`, avec la hauteur lue du terrain.

Sa géométrie et son matériau viennent de `StallKit.js`. Ce kit charge
`public/models/props/props.bin`, les textures de surfaces et de props, et
`signs.png`; il fusionne ensuite une géométrie propre à l'échoppe. Le source
référence la provenance CC0 des surfaces et props Poly Haven dans
`public/models/props/CREDITS.md`; `props.bin` source a le SHA-256
`f6dbe1c53a94a330f1aae852964aec1d8e4bcd57b04b145df9b1552aa899d486`.

Le bundle existant `preview-scene-bundle-clear-004/buildings` ne fournit pas
ces entrées : il ne contient ni `FishStand.js`, ni `StallKit.js`, ni
`props.bin`, ni atlas/texture de l'échoppe. Son catalogue ne contient que les
23 identifiants `building:0` à `building:22` du mesh `village`; aucun n'est
une échoppe ou un stand. Le runtime Rust ne contient pas de référence à
`FishStand`, `StallKit` ou `FishStandStall`.

Le bundle complet est néanmoins cohérent dans son périmètre actuel :
`package-preview-scene.py --verify` passe avec 1 542 fichiers et
295 672 776 octets. L'échoppe n'est donc pas un placement omis auquel il
suffirait de raccorder un asset packageable existant. Son port nécessite un
nouveau lot explicite d'import/packaging des sources de `StallKit`, de ses
provenances et d'un adaptateur de matériau Rust. Aucun substitut, aucun
changement runtime et aucune capture GPU n'ont été réalisés dans ce constat.
