# Tidewater — prévisualisation optique de l'océan dans Studio

Le Playtest importé dispose maintenant d'un mode explicite
`forge_water_optics=1` (qui active aussi `forge_water_geometry=1`). Le maillage
CDLOD et le FFT résident alimentent le shader optique de la révision source
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le matériau reçoit la profondeur opaque à la taille réelle du viewport, les
dérivées FFT, la mousse, les textures de rive, les normales du terrain, les
ombres d'objets, l'atmosphère et les nuages. La caméra met à jour ses matrices
et sa position à chaque image. Le champ de rive utilise la fonction de
production `tidewater_shore_field::bake` ; après correction des hauteurs,
il est recalculé depuis le terrain effectif. L'oracle source a comparé
1 048 576 valeurs sans différence (SHA-256 attendu et obtenu
`573846d4d3bfb0145951b9b560ccfa8d2b2cf6cb64a2f19d1866895cfe6ef833`).

Le premier essai WebGPU est resté noir. Son journal montrait 13 tampons
uniformes pour une limite de 12 et une copie de profondeur 4× MSAA vers 1×.
Les constantes ont été regroupées dans un seul tampon avec remplissage WGSL
explicite. Naga 29 valide le shader ainsi transformé avec une entrée
`vertex` et une entrée `fragment`, et les offsets attendus 0, 32, 112, 208,
272, 320, 352 dans un bloc de 432 octets. La caméra optique utilise un
échantillon et autorise `COPY_SRC` ; la copie saute une image dont la taille
ou le nombre d'échantillons ne correspond pas. Le script WebGPU restaure la
directive `derivative_uniformity` pour l'eau comme pour les autres matériaux
Tidewater. Le flou de mouvement Bevy est suspendu **uniquement** dans ce mode
optique expérimental, et le diagnostic du Playtest l'indique.

Le candidat local isolé est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-water-optics-candidate-047/forge-studio-candidate.exe`,
SHA-256 `bd8b73ffabc0d7fad9a8996c88cb46c4a5779ee5b7cc1275d1e4a3aa0628e6ef`.
Son WASM porte le SHA-256
`395caa2015be5efa4ba6fa0a5b8d1cc88d4a4acead81f6b32614d4b7fbdef984`.
Le [reçu de parcours](../../../../.tidewater-integration/studio-tidewater-water-optics-smoke-047/fish-stand-studio-smoke-ledger.json)
est `passed` : source intacte, zone éditée puis sauvée et rouverte, Playtest
actif, 101 nœuds océaniques au démarrage, FFT 130 → 139 puis étape optique
248 pendant la marche, retour à l'édition, aucune erreur navigateur capturée.
La [capture vers le large](../../../../.tidewater-integration/studio-tidewater-water-optics-smoke-047/fish-stand-ocean-optics-seaward.png)
montre de l'eau bleue et une ligne de déferlement à la place de la
[nappe blanche de diagnostic](../../../../.tidewater-integration/studio-tidewater-water-smoke-028/fish-stand-ocean-geometry-seaward.png).

Ce résultat qualifie la présence et la progression de l'optique dans ce
Playtest, pas une parité visuelle complète avec le site public. La capture
Studio est prise depuis le personnage sur la plage ; la paire source/native
du 29 septembre utilise une caméra haute fixe et ne permet donc pas une
mesure pixel à pixel avec elle. La simulation de ressac `ShoreSim` est encore
alimentée par un état zéro. La direction de rive utilise la texture neutre du
mode source `original_terrain_fallback` ; le shader tire alors l'exposition
des dérivées du terrain. Le flou de mouvement optique, la comparaison
appariée Studio/source,
les corrections de détails de côte et l'activation sans drapeau restent à
faire. Aucune publication ni qualification de Gate n'a été effectuée.

Contrôles : `cargo check` natif et WebGPU, build WASM release, `wasm-bindgen`,
validation Naga du shader optique, oracle de rive, self-test du parcours,
formatage ciblé et `git diff --check` réussis. Les candidats intermédiaires
en échec et leurs journaux restent sous `.tidewater-integration` pour le
diagnostic ; le candidat 047 est le premier de cette série à passer le
parcours optique.
