# Tidewater — ressac source dans le Playtest Studio

Le Playtest de la carte importée dispose d'un rendu WebGPU de l'eau. Il utilise le noyau ShoreSim de
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c` sur le
device de rendu Bevy. Les entrées sont emballées à partir des sources, vérifiées
par SHA-256 et chargées avant publication de la bibliothèque. Le paquet de
19 fichiers et 22 437 416 octets porte le SHA-256
`2dda3c022af1f62a3148e811155ecafc83b825d4e7e0c4069124ff69e7fca754`.
La texture GPU est calculée au lancement : elle n'est pas pré-rendue dans le
paquet. Le champ de rive et les corrections de hauteur proviennent du document
de terrain éditable ; la sortie alimente le sable et l'eau.

Le premier candidat, `048`, révélait une synchronisation bloquée : ShoreSim
terminait ses 120 pas de démarrage alors que l'océan avait déjà dépassé le pas
121. Le parcours a échoué volontairement après 180 s avec les journaux et la
capture conservés sous
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-shore-sim-smoke-048/`.
Le calcul rejoint désormais la dernière image océanique complète après son
démarrage, puis avance avec elle.

Le candidat isolé `049` est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-shore-sim-candidate-049/forge-studio-candidate.exe`,
SHA-256 `4c4cc428a24e0c4a9de05e1bb37dff2b99696a8325db0744c1dbd48df1e8ea62`.
Le WASM porte le SHA-256
`e179470e242bdc38507d44254f97bb2dda9583b09ea3d366a0db34c9757ba1ff`.
Son [reçu de parcours](../../../../.tidewater-integration/studio-tidewater-shore-sim-smoke-049/fish-stand-studio-smoke-ledger.json)
est `passed` : pose puis sauvegarde et réouverture de l'échoppe, entrée en
Playtest, FFT 129 → 137, ShoreSim 153 → 161, déplacement d'environ 3,77 m,
FFT et ShoreSim à l'étape 277 pendant la marche, retour à l'édition et aucune
erreur navigateur capturée. La fixture source est restée inchangée.

La [capture vers le large](../../../../.tidewater-integration/studio-tidewater-shore-sim-smoke-049/fish-stand-ocean-optics-seaward.png)
montre une vague et une écume de rive plus structurées que la
[capture optique précédente](../../../../.tidewater-integration/studio-tidewater-water-optics-smoke-047/fish-stand-ocean-optics-seaward.png).
Le [source à caméra haute](../../../../.tidewater-integration/source-seaward-clear004-20260929-021753/seaward-clear004.png)
reste plus nuancé, notamment pour les nuages, le déferlement, les ombres et les
détails de plage. Les caméras et instants diffèrent ; ces captures ne sont pas
une comparaison pixel à pixel et ne prouvent pas la parité visuelle.

Compilation moteur native et Studio WASM réussies, build WASM release,
`wasm-bindgen`, vérification des entrées, contrôle de formatage ciblé et
`git diff --check` réussis. La retouche du terrain suivie d'un lancement avec
ce ressac précis, la continuité visuelle sur longue durée, les effets d'embruns
et la comparaison appariée avec la démo publique restent à qualifier. Le mode
demeure expérimental ; aucune publication ni qualification de Gate n'est revendiquée.

Le candidat final de cet incrément, `050`, active l'eau optique et ShoreSim
automatiquement en Playtest lorsque les trois paquets vérifiés (FFT, surface,
ressac) sont présents. Les paramètres `forge_water_optics=0` et
`forge_shore_sim=0` permettent de les désactiver séparément pour un diagnostic.
L'exécutable local est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-shore-sim-candidate-050/forge-studio-candidate.exe`,
SHA-256 `8ae2d392468fc632805c3c5c571c7a069f34e7e79c17b79bcfa797f8054ff921` ;
son WASM porte
`1633d93b98feb3c97f07fc0cd7b6b93cee03d5150e20f4e499aebd91d46354d0`.
Le [parcours sans activation par URL](../../../../.tidewater-integration/studio-tidewater-shore-sim-smoke-050/fish-stand-studio-smoke-ledger.json)
est `passed` : édition/réouverture, FFT 138 → 147, ShoreSim 155 → 164,
marche avec les deux à l'étape 280, source intacte et aucune erreur navigateur
capturée. La [capture du candidat 050](../../../../.tidewater-integration/studio-tidewater-shore-sim-smoke-050/fish-stand-ocean-optics-seaward.png)
confirme la présence de l'eau et de l'écume au lancement normal de ce candidat.
