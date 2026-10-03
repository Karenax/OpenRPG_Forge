# Tidewater — village source face au Playtest Studio

La comparaison porte sur la référence figée
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`E:/OpenRPG_Forge/.tidewater-integration/baseline-003/village.png` (1920 ×
1080, caméra Village, temps source 2 s, exposition 0,55). Elle ne prétend pas
mesurer le `main` public du jour. Le Playtest Studio utilise la même pose et le
même champ de vision avec `forge_tidewater_reference_village=1`. Ce mode de
diagnostic fixe la caméra, pas l'horloge : figer celle-ci dès l'ouverture
bloquait l'initialisation du terrain et de l'océan. La capture Studio a lieu à
environ 3,47 s ; animation, météo et convergence temporelle ne sont donc pas
appariées image par image.

Le [reçu du parcours isolé](../../../../.tidewater-integration/studio-tidewater-village-grass-smoke-072/tidewater-studio-village-reference.json)
atteste l'ouverture de la zone, le placement/enregistrement/réouverture de
l'échoppe, l'entrée en Playtest et la capture 1920 × 1080 avec le candidat
`forge-studio-candidate.exe` SHA-256
`BA89DFFBF1AA9A5282D8664F222673CE423A1E11E8A5C018DD06F85F867EC0DE`.
Le frontend WASM associé a pour SHA-256
`72CED1BA731FAED37A12040ED5892BA18D0167A2FD3B48CEE335D658099C8CC5`.
La [capture à 0,55 EV](../../../../.tidewater-integration/studio-tidewater-village-grass-smoke-072/tidewater-studio-village-reference.png)
montre les 9 / 28 / 87 cellules d'herbe sélectionnées dans les trois niveaux
de détail ; le statut seul ne prouvait pas leur affichage. Le matériau de
l'herbe référençait un tampon GPU `models` inexistant. L'allocation d'un
tampon partagé de matrices identité a rendu l'herbe visible : entre les
captures avant/après, 5,61 % des pixels échantillonnés changent de plus de
3 niveaux de luminance, dont 14,8 % dans la région des maisons. [Mesure du
correctif](../../../../.tidewater-integration/studio-tidewater-village-grass-smoke-072/comparison-studio-grass-buffer-fix.json).

Mesure reproductible avec `scripts/tidewater/Compare-PairedCapture.ps1
-Preset Village`, luminance sRGB pondérée, un pixel sur huit dans chaque axe :

| Image entière | Source | Studio | Écart absolu moyen |
|---|---:|---:|---:|
| Réglage source 0,55 EV | 109,23 | 122,92 | 17,70 |
| Calibrage Studio 0,40 EV | 109,23 | 117,75 | 14,02 |

[Reçu 0,55 EV](../../../../.tidewater-integration/studio-tidewater-village-grass-smoke-072/comparison-source-studio-055.json) ·
[capture 0,40 EV](../../../../.tidewater-integration/studio-tidewater-village-grass-smoke-072/tidewater-studio-village-calibrated-040.png) ·
[reçu 0,40 EV](../../../../.tidewater-integration/studio-tidewater-village-grass-smoke-072/comparison-source-studio-040.json).
À 0,40 EV, l'écart moyen est encore 8,90 dans la colline, 19,27 dans les
maisons et 16,71 au premier plan. Le Studio reste visiblement trop clair et la
passerelle avec ses chemins latéraux manque. Dans la source, `Village.js`
construit cette passerelle séparément avec `buildBoardwalk` ; l'archive des
bâtiments actuellement importée par Studio ne la contient pas. C'est un
écart de contenu à exporter/importer, pas un curseur d'effet à ajuster.

Le build WASM/WebGPU, le build release Windows, `wasm-bindgen` et le parcours
Playtest isolé ont réussi. Le test ciblé
`resident_keeps_assets_handles_and_entities_across_a_b_a_updates` passe aussi
(1 test réussi, 448 filtrés) et vérifie le tampon partagé. Ce candidat local
n'est ni publié ni qualifié pour une parité
visuelle finale : il reste à intégrer la passerelle, appairer le temps/la
météo/les effets, puis refaire une paire source/Studio examinée visuellement.
