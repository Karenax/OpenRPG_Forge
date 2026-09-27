# Budget des embruns piloté par le GPU natif

Source de référence : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

Le probe accepte désormais `--native-budget` à la fin de la commande
`--native-history OCEAN_EXPORT [--coupled-shore SHORE_EXPORT]`.
La scène utilise ce même mode avec `--water-spray-native-budget` à la place de
`--water-spray`, avec les mêmes trois arguments d'acquisition.

Le contrôleur Rust reçoit le compteur d'émission lu sur le GPU natif. L'émission
du pas n utilise le budget déjà acquis ; ensuite, le contrôleur calcule celui
du pas suivant avec le compteur du pas n−1. Le FFT attend la réception du
compteur avant de faire avancer la simulation. Le rendu peut continuer pendant
cette attente. Ce protocole reproduit la latence d'un pas de l'acquisition
source contrôlée, sans prétendre qualifier une simulation interactive libre.

`native-budget.json` enregistre chaque décision, l'instant et le compteur qui
l'ont alimentée. Le compteur final de 120 pas est aussi lu indépendamment dans
`history-head.u32.bin`. Aucun calendrier d'émission n'est lu dans le mode natif.
Les anciens modes appariés restent disponibles pour le diagnostic.

## Preuves de calcul

`spray-native-budget-inputs-001` contient 14 fichiers d'entrée. Son manifeste
ne contient plus de calendrier, et aucun fichier de compteurs ou de particules
source n'est présent. Il conserve le hash du manifeste d'acquisition pour les
comparaisons. Le FFT et le rivage utilisent également leurs entrées réduites.

`native-spray-native-budget-001/002` terminent les 120 pas. Le rapport
`spray-native-budget-feedback-001.json` reconstruit indépendamment le calcul
depuis la trace GPU : 119 retours vérifiés, aucun retour du futur, valeurs f32
identiques à la reconstruction, premier ajustement après l'émission 62,
budget final 1. Les deux exécutions répètent budgets et compteurs exactement.

Comparaison à la source (`spray-native-budget-comparison-001.json`) :

| Au pas 120 | Source | Natif autonome |
|---|---:|---:|
| Émissions cumulées | 14 846 | 14 843 |
| Particules actives | 7 013 | 7 014 |
| Dépôt pondéré final | 282 | 282 |

Le dépôt final est identique texel par texel. Il reste 13 identités manquantes
et 14 supplémentaires, et les divergences de trajectoires/écume déjà suivies.
Le passage au contrôleur natif ne les efface pas : l'écart maximal de position
sur identités communes reste 269,512 mm, celui du canal écume 0,056641.

## Scène et régressions

`native-spray-autonomous-001.png` a été inspectée. `002` répète exactement la
capture, et les deux sont identiques à la capture native avec budget rejoué :
SHA-256 `1c1604cdb8af91222a8fe407a788702f481998b18064b141a265bcb6845dc596`.
Cela concerne cette pose à 2 s ; les états numériques ne sont pas tous identiques
au mode rejoué. L'effet de leur différence n'est pas visible dans ce PNG.

La comparaison à Tidewater reste donc inchangée : moyenne 0,150027/255 dans
les 18 530 pixels de la région concernée, percentile 99 maximal par pixel 4/255,
maximum local 41/255 (`spray-autonomous-scene-comparison-001.json`).
Les deux rapports `spray-autonomous-scene-feedback-001/002.json` valident les
retours de compteur dans la scène et leur correspondance au calcul isolé.

`spray-autonomous-regression-001.json` consigne la répétition des particules
modulo ordre atomique, crêtes, dépôts, état public du rivage et compteurs,
les 21 buffers FFT inchangés, ainsi que la conservation de l'image et des états
du mode historique à budget rejoué. Ce dernier refuse l'entrée privée de son
calendrier (`spray-budget-missing-schedule-001.log`), alors que le mode natif
fonctionne avec elle.

Clippy strict et compilation réussis (`spray-native-budget-clippy-004.log`,
`spray-native-budget-build-003.log`). Binaires :

- scène : `1983b23226e99d4a4dfd8eac8e76eaafd3c6581b26ae1b259e7e7a385672639d` ;
- simulation : `feb191c1aedeccdc3a199ff04cda10ea0526e9d79ee17ce2348490f550331104`.

La simulation native de ce cliché ne dépend plus d'un calendrier ni de résultats
de simulation source. Les constantes, shaders et assets restent acquis depuis
la source figée. Performance en temps réel, readbacks à latence libre, séquences
longues, variations de mer et parité animée restent ouverts. Cette étape ne
clôture ni la fidélité complète de la carte ni son édition Studio.
