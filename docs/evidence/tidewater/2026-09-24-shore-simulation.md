# Simulation temporelle du rivage : exécution native, parité ouverte

**Mise à jour :** la divergence majeure du hash décrite ici a ensuite été
corrigée. Voir [arrondi et nouvelles comparaisons](2026-09-24-shore-rounding.md).
Les résultats ci-dessous conservent la trace de la première exécution.

Le prototype `tidewater_shore_sim_probe` exécute le noyau ShoreSim original
sur le GPU Bevy, depuis un état vide pendant 1080 pas à 60 Hz (18 secondes,
deux périodes de houle). Il transporte l'écume, conserve l'humidité, dépose
les résidus et calcule le courant. Il ne charge aucun état calculé par JavaScript.
Ce prototype numérique n'est pas encore raccordé aux matériaux de la scène.

## Référence et entrées

Pin `1438b1abfcaee3267092b75573014f4d9b4a983c`, clones source inchangés.
`export-shore-sim.mjs` construit terrain, village, champ côtier puis ShoreSim.
Les 37 fichiers et le manifeste des acquisitions `shore-sim-source-003/004`
sont identiques. Le vérificateur contrôle les hashes, tailles, états finis,
bornes des trois canaux de densité, dépôts nuls et temps/uniformes des cinq
captures 0/1/120/540/1080. Rapport `shore-sim-source-verification-001.json`.

Entrées acquises : relief canonique 2048² après village, champ côtier 512²,
dentelle 512² et ses dix mips, paramètres terrain/rivage/simulation (32/64/48
octets), noyau original. État RGBA16Float 768² : écume, humidité, résidu,
courant. Le tampon u32 de dépôts est initialisé à zéro ; les embruns ne sont
pas encore exercés.

Le source consomme l'upload du champ terrain sans garder la copie CPU attendue
par `ShoreWaves.buildDirTexture`. Cette fonction revient donc à la direction
terrain (`dirOn=0`, texture placeholder). Ce comportement observé est conservé,
sans inventer une copie CPU qui modifierait la référence.

## Exécution et vérifications

Le premier envoi de 1080 pas en un seul bloc a échoué avec DeviceLost/Out of
Memory ; `native-shore-sim-001` est exclu. Le prototype conserve maintenant
textures et pipeline entre les passages du moteur et soumet quatre pas par
passage. Chaque pas reçoit son propre uniforme : aucun alias de temps.
Les instantanés sont copiés avant que le calcul continue.

Vulkan AMD RX 7800 XT : `native-shore-sim-002/003`, dix fichiers identiques.
La simulation termine sans erreur, les états sont finis et les densités
restent dans [0,1]. Un essai natif DX12 termine également.
Compilation finale `shore-sim-native-build-004.log`, Clippy `-D warnings`
`shore-sim-native-clippy-002.log` réussis. Le garde rejette une acquisition
diagnostique en mode simulation avant création du répertoire de sortie.
Rapport de répétition `shore-sim-native-regression-001.json`.

Binaire : `1a6818387bc07379c1519e97458d029bfde10af2dc076650e6bbfbbc1cd59209`.

## Écart non résolu

Les valeurs suivantes comparent les données brutes, pas une image de la scène.
À 18 secondes sur Vulkan, écarts absolus moyen / maximum :

| Canal | Moyen | Maximum |
|---|---:|---:|
| Écume | 0,00225634 | 0,961670 |
| Humidité | 0,00195483 | 1 |
| Résidu | 0,000232723 | 0,416016 |
| Courant | 0,0217899 | 4,664063 |

Rapport `shore-sim-native-comparison-001.json`. Le test DX12 réduit certains
écarts mais ne les supprime pas (`shore-sim-native-dx12-comparison-001.json`).
Ces maxima excluent toute qualification de parité de la simulation.

Un diagnostic distinct (`--phase-probe`) garde les fonctions originales mais
remplace la sortie par hauteur du terrain, phase, `shoreHash1` et amplitude.
`shore-phase-source-001` et `native-shore-phase-001` ne sont jamais des preuves
de simulation. Au premier pas, 170827 texels ont la même phase enregistrée
(demi-précision) mais un hash différent de plus de 0,01. Exemple : phase
-238,625, hash source 0,5986328 contre natif 0,1762695. Seuls 24 texels ont
une phase enregistrée différente, contre 170842 pour le hash. Rapport
`shore-phase-diagnostic-001.json`.

La fonction `fract(sin(x*127.1+311.7)*43758.5453)` amplifie ainsi des différences
de calcul GPU. Le diagnostic localise une divergence importante avant
l'advection ; il ne distingue pas encore les contributions de contraction
arithmétique, réduction d'argument et approximation trigonométrique. Aucune
table de résultats ni approximation de remplacement n'a été introduite.

## Suite et limites

Il faut corriger/qualifier cette divergence, puis raccorder l'état au sable
et à SurfFoam et comparer des séquences de scène. Le recalcul Rust du champ
côtier, les dépôts d'embruns, les pas variables, Studio et la qualification
WebGPU restent ouverts. Tous les critères TW complets restent ouverts.
Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`, aucune publication.
