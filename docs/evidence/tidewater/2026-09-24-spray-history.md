# Embruns — historique natif de 120 pas

Source figée : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

Le mode `tidewater_spray_probe SPRAY_EXPORT NEW_OUTPUT --native-history
OCEAN_EXPORT` conserve les buffers de particules pendant 120 pas. Le FFT natif
avance avant chaque émission et intégration. `Targets.permitted_step` empêche
le FFT de dépasser un consommateur dont les ressources GPU ne sont pas prêtes ;
`completed_steps` désigne le travail encodé, pas un acquittement CPU du GPU.

Les essais `native-spray-history-003/004` n'importent ni particules initiales,
ni résultats de particules, ni textures FFT source. Le second utilise les
entrées réduites `spray-native-history-inputs-001` (14 fichiers) et
`ocean-native-inputs-001` (13 fichiers), sans ces résultats. Les paramètres,
le terrain et le calendrier du budget d'émission restent acquis depuis la source.

## Correction du calendrier

Breakers met à jour son budget après avoir envoyé les paramètres de l'émission.
Il faut donc enregistrer le budget avant cet appel. `spray-source-010` ajoute
`emitter_budget` à chaque entrée du calendrier. La graine Spray, dont le buffer
est réécrit avant soumission, conserve au contraire sa valeur GPU finale du pas.
Les essais natifs `001/002`, qui utilisaient le budget suivant, sont exclus du
verdict courant. Les acquisitions source `009/010` conservent les mêmes états
physiques modulo ordre atomique des emplacements.

## Résultats et limites

Rapport : `spray-history-comparison-001.json`.

| Mesure au pas 120 | Source | Natif |
|---|---:|---:|
| Particules actives | 6 886 | 6 886 |
| Compteur cumulé d'émission | 14 846 | 14 841 |
| Dépôt pondéré | 349 | 353 |

6 873 identités communes ; 13 identités manquantes et 13 supplémentaires.
La première divergence du compteur apparaît au pas 8. Sur les identités
communes, l'écart moyen par coordonnée vaut 0,176 mm, le percentile 99 vaut
0,986 mm, mais le maximum atteint 269,516 mm. Un texel de dépôt diffère de
quatre unités. La cause des divergences résiduelles n'est pas encore établie.
Le même nombre de particules actives ne constitue donc pas une parité.

Les deux exécutions natives répètent exactement les particules actives modulo
emplacements, la trace des compteurs et les dépôts. Leurs 21 buffers FFT sont
identiques à `native-ocean-default-fft-frame120-003`. La régression du mode
`--replay-update 120`, sur les états appariés `spray-source-010`, conserve les
décisions de vie/mort et chaque texel du dépôt exacts ; erreur maximale de
position 0,075221 mm. Les emplacements de cette acquisition diffèrent des
anciennes acquisitions : une comparaison brute entre leurs buffers ne convient pas.
Rapports `spray-history-regression-001.json` et
`spray-history-impact-regression-001.json`.

Clippy strict et compilation réussis (`spray-history-clippy-004.log`,
`spray-history-build-004.log`). Binaire SHA-256 :
`77b1ed69a845cb3c3d4c876458893b994b95c0024ea6b4f3308e986021ac16be`.

Le contrôleur adaptatif du budget n'est pas encore natif : le calendrier source
est rejoué. Le dépôt est remis à zéro à chaque pas sans être consommé par
ShoreSim. Ce mode ne dessine pas les sprites d'embruns et n'établit aucune
parité de scène. Le raccordement du rivage, les écarts numériques, le rendu et
la qualification temporelle complète restent ouverts.
