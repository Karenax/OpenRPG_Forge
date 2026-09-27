# Initialisation native du spectre océanique

Source Tidewater `1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/OceanFFT.js`, MIT DRG Software Solutions LLC.
Artifacts locaux : `E:/OpenRPG_Forge/.tidewater-integration/`.

## Référence acquise

L'export `scripts/tidewater/export-ocean.mjs` exécute le moteur original sous
Dawn, 120 soumissions distinctes à 1/60 s, sans modifier la source épinglée.
Deux cas : constructeur par défaut ; paramètres océaniques et whitecaps du
preset Storm de `AppUI.js` (sans instancier son interface DOM).

Chaque acquisition contient 250 fichiers : neuf kernels, module de sampling,
paramètres de huit variantes (1–4 cascades, deux météos), puis snapshots aux
images 1, 60, 120 : paramètres/frame, h0, waveData, foam et deux textures
rgba16 de quatre couches avec leurs neuf niveaux mipmap.

Les paires définitives sont `ocean-source-003/004` et
`ocean-storm-source-003/004`. Le vérificateur contrôle les hashes, tailles,
finitude des 225 sorties GPU, stabilité du spectre et limites de l'écume.
Les 250 fichiers de chaque paire sont identiques.
Rapports : `ocean-default-repeat-final.json`, `ocean-storm-repeat-final.json`.

L'écume du cas par défaut est nulle pendant ces deux secondes. En tempête,
9 744, 56 837 et 86 799 texels portent de l'écume aux trois instants ; maximum
respectivement 0,4091, 1,5 et 1,5. Ce second cas exerce donc réellement la
persistance que devra reproduire le calcul natif.

**Acquisitions rejetées :** les variantes 001/002 des deux cas partageaient
le stockage ArrayBuffer des uniformes ; les temps exportés devenaient tous
le temps final. La comparaison Rust l'a détecté. L'export copie maintenant
les octets immédiatement, et le vérificateur contrôle explicitement le temps
initial ainsi que chacun des temps packés. Les sorties GPU de ces anciennes
variantes ne constituent pas les références finales, même si elles répétaient.

## Port Rust et GPU

`forge_engine::ocean_spectrum` calcule les bandes de fréquences et les deux
systèmes JONSWAP en f64 avant écriture f32 dans les 224 octets de `OceanParams`.
Il conserve notamment w=1 dans les vecteurs cuts inutilisés, comme Vector4()
dans la source. Les paramètres invalides sont refusés avant allocation GPU.
Ce module contient des réglages de rendu, pas un nouveau format canonique.

`verify_ocean_spectrum` compare douze blocs byte à byte dans chacun des deux
exports, dont les temps après additions successives de 1/60. Six contrôles
d'entrées invalides passent. Le temps JSON diagnostique tolère quatre epsilons
f64 (arrondi du parseur JSON) ; les octets GPU sont toujours strictement exacts.
Logs : `ocean-parameters-final.log`, `ocean-parameters-storm-final.log`.

`tidewater_ocean_spectrum_probe` exécute initSpectrum, conjugate et copyH0
dans le graphe Bevy avant camera_driver. Buffers initialement nuls,
paramètres calculés en Rust, shaders originaux hashés ; aucune donnée h0 ou
waveData de référence chargée comme entrée native. Le calcul utilise le même
device de rendu Bevy. Readback h0/waveData et sortie après fin de lecture.

## Comparaison numérique

Natif Vulkan AMD RX 7800 XT ; source Dawn D3D12.
Chaque buffer compte 1 048 576 scalaires f32, tous finis.

| Buffer | Cas | Erreur absolue max | Erreur absolue moyenne |
|---|---|---:|---:|
| h0 | défaut | 3,92646e-6 | 3,04563e-10 |
| h0 | tempête | 5,78165e-6 | 4,56637e-10 |
| waveData | les deux | 7,62939e-6 | 8,42424e-8 |

Rapports `ocean-spectrum-comparison-001.json`,
`ocean-storm-spectrum-comparison-001.json`. Ces écarts ne sont pas une parité
bit à bit et leur propagation dans la FFT animée reste à mesurer.

SHA-256 h0 natif défaut :
`14630b99b8e25dd7ad5adfb4f243eff07d28d3eee75b07b2200b5288b59ab106`.
SHA-256 h0 natif tempête :
`320864a01a2cc15793670623c412b604bfde8566d924efa170c7c70b41b64b19`.
SHA-256 waveData natif :
`1110050bf1ed5b38eca0dda556383c076f3a7701394b4b1cca63bed8aa61e667`.

Compilation `ocean-build-002.log` et Clippy avec `-D warnings`
`ocean-clippy-002.log` réussis. Exemple GPU exécuté avec succès dans les deux
cas (`native-ocean-spectrum-001.log`, `native-ocean-storm-spectrum-001.log`).
Binaire GPU SHA-256 :
`73003519b16d7ee41ecf88138315c6bdcb83ee291e2bea8a6f94f0980b697f2f`.

## Suite requise

Exécuter nativement les passes lignes/colonnes, mises à jour d'écume et mips
sur les mêmes instants, puis raccorder WaterSurface/WaterMaterial, rivage,
optique et interactions. Aucune surface d'eau native ni parité de carte complète
n'est encore livrée. TW-007 reste ouvert, ainsi que Studio et WebGPU navigateur.
Aucun commit ou publication.
