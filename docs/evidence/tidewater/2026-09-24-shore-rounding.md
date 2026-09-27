# Rivage : arrondi du hash corrigé et propagé au rendu

La divergence principale relevée dans la simulation ShoreSim venait de la
contraction de `x*127.1+311.7`. La référence Dawn arrondit ici le produit
intermédiaire en binary32 ; la compilation native pouvait fusionner produit et
somme. Le hash trigonométrique amplifiait fortement ce petit changement d'angle.
Ce résultat remplace le diagnostic provisoire de
[la première exécution ShoreSim](2026-09-24-shore-simulation.md), sans constituer
une qualification de la scène entière.

## Diagnostic sans perte

Le mode `--hash-probe` conserve les dépendances WGSL originales et remplace
seulement la sortie du calcul par des valeurs intermédiaires : numéro de vague,
angle, sinus, produit, hash et témoins avec `fma` explicite. Chaque binary32 est
encodé en quatre octets stockés exactement dans RGBA16Float. Le diagnostic ne
confond donc plus précision de stockage et différence de calcul.

Sur le premier domaine [-512,255], 213 angles et 173 hashes sur 768 valeurs
uniques différaient. Pour -239, l'angle était -30065,201171875 dans la référence
et -30065,19921875 en natif ; les hashes étaient 0,5986328125 et 0,17626953125.
Rapport `shore-hash-before-comparison-001.json` (chaque cas y est répété 96 fois).

`native-shore-hash.wgsl` conserve les opérations originales et impose le point
d'arrondi du produit par un passage en bits avec masque uniforme nul. Le champ
Frame.pad0 doit avoir le motif 0 : le calcul natif le construit ainsi, la
préparation du matériau et son chargement Rust le contrôlent. Il n'y a ni
table de résultats, ni nouvelle graine, ni changement des paramètres de houle.
La substitution refuse une définition source inattendue et le hash de
l'adaptation figure dans les fixtures de rendu préparées.

La vérification élargie couvre **73 728 numéros distincts**, de -65536 à 8191.
L'angle, le sinus, le produit et le hash sont identiques bit à bit à la référence
sur Vulkan et DX12, AMD RX 7800 XT. Les témoins `fma` diffèrent encore, comme
attendu : ils servent à distinguer les chemins arithmétiques. Cela ne garantit
pas l'identité sur d'autres GPU/compilateurs ou hors du domaine exercé.

- Acquisitions `shore-hash-source-002/003`, 37 fichiers et manifeste identiques.
- Natifs `native-shore-hash-003`, `native-shore-hash-dx12-001`.
- Rapports `shore-hash-comparison-001.json`, `shore-hash-dx12-comparison-001.json`.
- Vérificateur reproductible `scripts/tidewater/verify-shore-hash.py`.

## Simulation temporelle corrigée

Le noyau corrigé recalcule les 1080 pas depuis zéro. Les dix fichiers de
`native-shore-sim-004/005` sont identiques. Aucun état source n'est chargé.
À 18 s, les erreurs absolues moyennes / maximales sont :

| Canal | Moyenne avant | Moyenne après | Maximum après |
|---|---:|---:|---:|
| Écume | 0,00225634 | 0,00000189784 | 0,00537109 |
| Humidité | 0,00195483 | 0,000000197882 | 0,00219727 |
| Résidu | 0,000232723 | 0,000000197229 | 0,00512695 |
| Courant | 0,0217899 | 0,00000581898 | 0,0126953 |

Rapport `shore-sim-native-comparison-002.json`, incluant également les pas
0/1/120/540. Toutes les valeurs sont finies et les trois densités dans [0,1].
L'état complet n'est pas identique bit à bit ; la correction du hash ne suffit
pas à supprimer tous les écarts arithmétiques du noyau et de l'advection.

## Application aux deux vues de carte

Le même fichier WGSL est utilisé par `prepare-water-optics.py`. Les fixtures
`water-shore-seaward-003` et `water-shore-island-002` ont été rendues et les PNG
inspectés. Les captures restent à 2 s et ne montrent pas encore l'état ShoreSim.

| Vue | Erreur moyenne RGB entière /255 | Zone de l'effet avant → après /255 | P99 zone | Maximum zone |
|---|---:|---:|---:|---:|
| Village vers la mer | 0,1772415 | 0,2399666 → 0,2386878 | 5 | 113 |
| Île | 0,1465241 | 0,1823442 → 0,1741019 | 2 | 62 |

Rapports `shore-effect-rounded-comparison-001.json` et
`shore-effect-rounded-island-comparison-001.json`. Les écarts visuels restants
ne sont pas masqués par ces moyennes : les maxima et la chaîne incomplète
empêchent de revendiquer l'égalité exacte de la carte finale.

- PNG plage `native-water-shore-rounded-001.png` :
  `b8976a634587c04fca4447e86d2c2c2a467b06d7503193886009e002321d1176`.
- PNG île `native-water-shore-rounded-island-001.png` :
  `2dea05997e3a1714aa7f6c6635a28bca74b98c9f194208384d582c50d30a2a1f`.

La répétition de plage est identique. Le rendu sans ShoreWaves reproduit
exactement le PNG optique précédent. Les 21 sorties FFT des quatre captures
restent identiques à `native-ocean-default-fft-frame120-003`. Le test d'un
Frame.pad0 non nul échoue avant création de la fixture de sortie.
Rapport `shore-rounding-regression-001.json`.

Compilations `shore-sim-native-build-006.log`, `shore-rounding-scene-build-001.log`
et Clippy des deux exemples `shore-rounding-clippy-001.log` réussis.
Binaires :
- simulation `0a1e8f333b6e99c5ba06ee39e34d1d11f535fc9a80ebf802175e2c8b23c6ed45` ;
- scène `05281809c11525827b400e757f2827bd62a274dadc7f3002fdde9e2f3fa71deb`.

## Suite

Raccorder l'état natif ShoreSim au sable et à SurfFoam, puis comparer des
séquences de scène. La génération native du champ côtier, les embruns,
l'interactivité, Studio, les autres effets et le package autonome restent à
terminer. Tous les critères TW complets restent ouverts ; aucune publication.
Artefacts sous `E:/OpenRPG_Forge/.tidewater-integration/`.
