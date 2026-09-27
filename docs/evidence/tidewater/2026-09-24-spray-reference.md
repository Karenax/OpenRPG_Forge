# Référence de simulation des embruns et dépôts

`export-spray.mjs` acquiert la chaîne originale FFT → ShoreSim → Breakers →
Spray pendant 540 pas de 1/60 s. Le terrain inclut le village ; les stations
et le champ de propagation sont ceux de la référence figée. Le calcul de hauteur
Eulerienne de WaterQuery est inclus dans le kernel de simulation des particules.
Les émissions CPU, collisions avec le bateau et nuages restent désactivés.

Le buffer compte 32 768 places GPU et 8 192 places CPU. Breakers émet dans un
anneau partagé par opérations atomiques. Spray intègre les particules et dépose
des gouttes dans ShoreSim lorsqu'elles retombent dans l'eau. ShoreSim consomme
ce dépôt au pas suivant, puis les nouvelles retombées remplissent à nouveau le
buffer. Le résidu d'écume sur le sable dépend aussi de l'écume précédente.

## Acquisition et répétition

Exports `spray-source-002/003`, 101 fichiers chacun plus manifeste.
Le manifeste conserve les layouts des kernels, capacités, graines avant/après
simulation et les 540 valeurs du budget d'émission calculées par la logique
asynchrone originale. L'ordre des passes et le réglage de débit sont conservés.

`spray-reference-verification-001.json` confirme 92 fichiers identiques et
le même historique de budget sur ces deux exécutions. Les neuf fichiers de
positions/vitesses/informations diffèrent par l'ordre atomique des emplacements.
Le multiensemble des enregistrements complets des particules actives est
strictement identique aux trois instants. Cette propriété est contrôlée par
`verify-spray.py` sans imposer une permutation artificielle aux sorties.

| Pas | Particules actives | Compteur d'émission | Dépôt en attente | Texels d'écume modifiés par les embruns |
|---|---:|---:|---:|---:|
| 1 | 47 | 47 | 0 | 0 |
| 120 | 6 886 | 14 846 | 349 | 5 504 |
| 540 | 12 462 | 130 155 | 448 | 27 156 |

Le dépôt en attente compte les gouttes pondérées par type, pas toutes les
retombées cumulées. L'humidité et le flux de ShoreSim restent identiques à la
référence sans embruns ; l'écume et, indirectement, son résidu changent.
Les états sont finis et les emplacements CPU restent inactifs.

L'essai 001 a échoué avant export à cause du chemin d'import de RenderTarget,
corrigé vers `Texture.js`. Il n'est pas utilisé comme référence.

## Portée

Il s'agit de données et kernels de référence, pas encore de la simulation ou
du rendu natif des embruns. L'égalité de l'historique de budget observée ici
n'est pas une garantie pour toute cadence asynchrone. La qualification native
devra distinguer permutation des places, population, trajectoires, budget et
dépôts, puis vérifier le rendu et son évolution temporelle.

Acquisitions sous `E:/OpenRPG_Forge/.tidewater-integration/` ; source figée
inchangée. Aucun critère TW complet n'est fermé.
